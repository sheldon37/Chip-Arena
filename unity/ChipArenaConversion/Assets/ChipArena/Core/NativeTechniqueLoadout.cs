using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // Two optional replacements per weapon. Legacy weapon:slot selects its first catalog technique.
    public static class NativeTechniqueLoadout
    {
        public const int MaxLength=2048;
        public static readonly string[] Weapons={"pistol","rifle","machinegun","fire","water","lightning","darkness","sword","daggers","shield","greatsword","bow"};
        public static bool Allowed(string spec,string slot)=>Array.IndexOf(Weapons,spec)>=0&&(slot=="e"||slot=="q"&&spec!="rifle");
        private static string First(string spec)=>NativeTechniques.Of(spec).FirstOrDefault()?.Id??"";
        public static string Normalize(string map)
        {
            if(string.IsNullOrEmpty(map)||map.Length>MaxLength)return "";var picks=new Dictionary<string,string>();
            foreach(var entry in map.Split(','))
            {
                var pair=entry.Split(':');if(pair.Length!=2)continue;string spec=pair[0].Trim();var choice=pair[1].Split('=');if(choice.Length>2)continue;string slot=choice[0].Trim();
                if(slot=="default"){picks.Remove(spec+":q");picks.Remove(spec+":e");continue;}if(!Allowed(spec,slot))continue;
                string id=choice.Length==1?First(spec):choice[1].Trim();if(id==""){picks.Remove(spec+":"+slot);continue;}
                if(NativeTechniques.Find(id)?.Spec!=spec)continue;
                string other=spec+":"+(slot=="q"?"e":"q");if(picks.TryGetValue(other,out var old)&&old==id)picks.Remove(other);picks[spec+":"+slot]=id;
            }
            return string.Join(",",Weapons.SelectMany(w=>new[]{w+":q",w+":e"}).Where(picks.ContainsKey).Select(k=>k+"="+picks[k]));
        }
        public static bool Valid(string map)=>map==null||map==Normalize(map);
        // Allocation-free for canonical saved/wire maps; also understands the old single-technique token.
        public static string In(string map,string spec,string slot)
        {
            if(!Allowed(spec,slot)||string.IsNullOrEmpty(map)||map.Length>MaxLength)return "";
            for(int i=0;i<map.Length;){int end=map.IndexOf(',',i);if(end<0)end=map.Length;int n=spec.Length;
                if(end-i>=n+2&&string.CompareOrdinal(map,i,spec,0,n)==0&&map[i+n]==':'&&map[i+n+1]==slot[0])
                {if(end-i==n+2)return First(spec);if(map[i+n+2]=='=')foreach(var d in NativeTechniques.Of(spec))if(end-i==n+3+d.Id.Length&&string.CompareOrdinal(map,i+n+3,d.Id,0,d.Id.Length)==0)return d.Id;}
                i=end+1;
            }return "";
        }
        public static string Slot(string map,string spec,string id=null)
        {var q=In(map,spec,"q");if(q!=""&&(id==null||q==id))return "q";var e=In(map,spec,"e");return e!=""&&(id==null||e==id)?"e":"";}
        public static string With(string map,string spec,string slot,string id)
        {
            if(!Allowed(spec,slot)||id!=""&&NativeTechniques.Find(id)?.Spec!=spec)return Normalize(map);
            return Normalize(Normalize(map)+","+spec+":"+slot+"="+id);
        }
        // Old callers select the first technique, or reset the weapon. New picker calls the id overload.
        public static string With(string map,string spec,string slot)=>slot==""?Normalize(Normalize(map)+","+spec+":default"):With(map,spec,slot,First(spec));
        public static ContentAbility Ability(ConversionContent content,string map,string spec,string slot)
        {string id=In(map,spec,slot);if(id=="")return content.Ability(spec,slot);foreach(var a in content.abilities)if(a.spec==spec&&a.slot=="technique"&&a.id==id)return a;return null;}
    }
}
