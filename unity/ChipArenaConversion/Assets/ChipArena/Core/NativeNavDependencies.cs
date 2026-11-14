using System;
using System.Collections.Generic;
namespace ChipArena.UnityConversion
{
    // Index immutable authored membership, never cache the live structural state. A collapse/rebuild is visible immediately.
    public sealed class NativeNavDependencies
    {
        private readonly Dictionary<string,List<(string segment,int allowed)>> links=new Dictionary<string,List<(string,int)>>(StringComparer.Ordinal);
        public NativeNavDependencies(StructureDefinition[] segments)
        {
            foreach(var d in segments??Array.Empty<StructureDefinition>())
            {
                foreach(var id in d.nav?.remove??Array.Empty<string>())Add(id,d.id,0);
                foreach(var id in d.nav?.add??Array.Empty<string>())Add(id,d.id,3);
            }
        }
        private void Add(string link,string segment,int allowed)
        {if(!links.TryGetValue(link,out var list)){list=new List<(string,int)>();links.Add(link,list);}list.Add((segment,allowed));}
        public bool Enabled(string link,NativeStructureState state)
        {if(links.TryGetValue(link,out var list))foreach(var d in list)if(state.Get(d.segment).state!=d.allowed)return false;return true;}
    }
}
