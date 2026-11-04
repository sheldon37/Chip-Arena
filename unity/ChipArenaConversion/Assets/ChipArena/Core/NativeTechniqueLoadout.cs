using System;
using System.Collections.Generic;
using System.Linq;
namespace ChipArena.UnityConversion
{
    // One optional replacement per standard weapon; empty preserves both original abilities.
    public static class NativeTechniqueLoadout
    {
        public const int MaxLength=256;
        public static readonly string[] Weapons={"pistol","rifle","machinegun","fire","water","lightning","darkness","sword","daggers","shield","greatsword","bow"};
        public static bool Allowed(string spec,string slot)=>Array.IndexOf(Weapons,spec)>=0&&(slot=="e"||slot=="q"&&spec!="rifle");
        public static string Normalize(string map)
        {
            if(string.IsNullOrEmpty(map)||map.Length>MaxLength)return "";var picks=new Dictionary<string,string>();
            foreach(var entry in map.Split(',')){var pair=entry.Split(':');if(pair.Length!=2)continue;string spec=pair[0].Trim(),slot=pair[1].Trim();if(Allowed(spec,slot))picks[spec]=slot;else if(slot=="default")picks.Remove(spec);}
            return string.Join(",",Weapons.Where(picks.ContainsKey).Select(w=>w+":"+picks[w]));
        }
        public static bool Valid(string map)=>map==null||map==Normalize(map);
        public static string Slot(string map,string spec)
        {
            if(string.IsNullOrEmpty(map)||map.Length>MaxLength)return "";
            // Stored and wire maps are canonical; exact tokens prevent weapon-prefix matches.
            for(int i=0;i<map.Length;){int end=map.IndexOf(',',i);if(end<0)end=map.Length;int n=spec?.Length??0;
                if(end-i==n+2&&string.Compare(map,i,spec,0,n,StringComparison.Ordinal)==0&&map[i+n]==':')
                {char slot=map[end-1];if(slot=='e'&&Allowed(spec,"e"))return "e";if(slot=='q'&&Allowed(spec,"q"))return "q";}i=end+1;}return "";
        }
        public static string With(string map,string spec,string slot)
        {var keep=Normalize(map).Split(',').Where(x=>!x.StartsWith(spec+":",StringComparison.Ordinal));return Normalize(string.Join(",",keep)+(Allowed(spec,slot)?","+spec+":"+slot:""));}
        public static ContentAbility Ability(ConversionContent content,string map,string spec,string slot)
        =>content.Ability(spec,Slot(map,spec)==slot?"technique":slot);
    }
}
