using System;
using System.Collections.Generic;
using System.Text;

namespace ChipArena.UnityConversion
{
    // Weapon skins (owner 2026-10-12: new higher quality weapon models; "we can move our current weapon models over into skins
    // as the Prototype Series"). Every weapon with a model has two skins: Standard (the default: NativeWeaponModels, sculpted
    // in code) and the Prototype Series (the exported source models, "weapon-"+spec and "loose-"+spec in SourceModels,
    // unchanged). The pick is per weapon and travels as one compact, normalized string: "pistol:prototype,sword:prototype"
    // (only non-default picks, in weapon order, unknown weapons and skins dropped). It is saved in prefs.weaponSkins and the
    // account cosmetics (NativeCosmetics.weaponSkins) and carried in the seat frames like lobbyBack, so every client draws the
    // owner's skin; a missing or malformed value (older clients, old saves, bots) reads as Standard everywhere.
    // Presentation only: a skin never changes reach, hit volumes, timing or any rule.
    public static class NativeWeaponSkins
    {
        public const string Standard="standard",Prototype="prototype";
        public static readonly string[] Keys={Standard,Prototype};
        public static readonly string[] Labels={"Standard","Prototype Series"};
        public static readonly string[] Texts={"The current production model, with accent lights in your colour.","The original blocky build the arena started with."};
        public const int StandardIndex=0,PrototypeIndex=1,Count=2;
        /// <summary>The weapons that carry skins (every weapon with a model; the Siren has none), in content order.</summary>
        public static readonly string[] Weapons={"pistol","rifle","machinegun","fire","water","lightning","darkness","sword","daggers","shield","greatsword","emperor","ansatsuken","leet"};
        /// <summary>The longest normalized map (every weapon on the Prototype Series) is 249 characters; frames refuse longer.</summary>
        public const int MaxLength=320;
        public static bool Skinned(string spec)=>spec!=null&&Array.IndexOf(Weapons,spec)>=0;
        public static int SkinIndex(string skin)=>skin==Prototype?PrototypeIndex:skin==Standard?StandardIndex:-1;

        /// <summary>The skin picked for `spec` in a map (StandardIndex when absent or malformed). Allocation free.</summary>
        public static int Index(string map,string spec)
        {
            if(string.IsNullOrEmpty(map)||string.IsNullOrEmpty(spec))return StandardIndex;
            int i=0,n=map.Length,result=StandardIndex;
            while(i<n)
            {
                int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);
                if(colon>i&&colon-i==spec.Length&&string.CompareOrdinal(map,i,spec,0,spec.Length)==0)
                {
                    int len=end-colon-1;
                    for(int k=0;k<Count;k++)if(Keys[k].Length==len&&string.CompareOrdinal(map,colon+1,Keys[k],0,len)==0){result=k;break;}
                }
                i=end+1;
            }
            return result;
        }
        public static string Of(string map,string spec)=>Keys[Index(map,spec)];
        /// <summary>
        /// The canonical form: entries "spec:skin" for known weapons on a non-default known skin, in Weapons order, each weapon
        /// once (the last entry for a weapon wins), joined by commas; "" when every weapon is Standard. Never longer than MaxLength.
        /// </summary>
        public static string Normalize(string map)
        {
            if(string.IsNullOrEmpty(map))return "";
            if(map.Length>4096)map=map.Substring(0,4096);
            var picks=new int[Weapons.Length];
            foreach(var entry in map.Split(','))
            {
                int colon=entry.IndexOf(':');if(colon<=0)continue;string spec=entry.Substring(0,colon).Trim(),skin=entry.Substring(colon+1).Trim();
                int w=Array.IndexOf(Weapons,spec),k=SkinIndex(skin);if(w<0||k<0)continue;picks[w]=k;
            }
            return Join(picks);
        }
        private static string Join(int[] picks)
        {
            var sb=new StringBuilder();
            for(int w=0;w<Weapons.Length;w++)if(picks[w]!=StandardIndex){if(sb.Length>0)sb.Append(',');sb.Append(Weapons[w]).Append(':').Append(Keys[picks[w]]);}
            return sb.ToString();
        }
        /// <summary>The map with `spec` set to `skin` (normalized).</summary>
        public static string With(string map,string spec,string skin)
        {
            int w=Array.IndexOf(Weapons,spec),k=SkinIndex(skin);if(w<0||k<0)return Normalize(map);
            var picks=new int[Weapons.Length];for(int i=0;i<Weapons.Length;i++)picks[i]=Index(map,Weapons[i]);picks[w]=k;return Join(picks);
        }

        // ---- model ids (cached strings: drawing allocates nothing) ----
        public const int Tiers=NativeWeaponModels.Tiers;
        private static readonly Dictionary<string,string[]> held=new Dictionary<string,string[]>(StringComparer.Ordinal),loose=new Dictionary<string,string[]>(StringComparer.Ordinal);
        private static readonly Dictionary<string,(string spec,bool loose)> prototypes=new Dictionary<string,(string,bool)>(StringComparer.Ordinal);
        /// <summary>The Standard model id of `spec` at a tier ("weapon-std-sword", "-reduced", "-low"); null without a design.</summary>
        public static string StandardModel(string spec,int tier)
        {
            if(!NativeWeaponModels.Designed(spec))return null;
            if(!held.TryGetValue(spec,out var ids)){ids=new[]{"weapon-std-"+spec,"weapon-std-"+spec+"-reduced","weapon-std-"+spec+"-low"};held[spec]=ids;}
            return ids[Math.Max(0,Math.Min(Tiers-1,tier))];
        }
        public static string StandardLoose(string spec)
        {
            if(!NativeWeaponModels.Designed(spec))return null;
            if(!loose.TryGetValue(spec,out var ids)){ids=new[]{"loose-std-"+spec};loose[spec]=ids;}
            return ids[0];
        }
        /// <summary>
        /// The id to draw for a Prototype model id ("weapon-"+spec or "loose-"+spec) under `skin`: the id itself for the
        /// Prototype Series, else the Standard id (held: at `tier`), falling back to the Prototype id where no design exists.
        /// </summary>
        public static string Model(string prototypeId,int skin,int tier=0)
        {
            if(skin==PrototypeIndex||string.IsNullOrEmpty(prototypeId))return prototypeId;
            if(!prototypes.TryGetValue(prototypeId,out var key))
            {
                bool l=prototypeId.StartsWith("loose-",StringComparison.Ordinal),w=prototypeId.StartsWith("weapon-",StringComparison.Ordinal)&&!prototypeId.StartsWith("weapon-std-",StringComparison.Ordinal);
                key=(w?prototypeId.Substring(7):l&&!prototypeId.StartsWith("loose-std-",StringComparison.Ordinal)?prototypeId.Substring(6):null,l);prototypes[prototypeId]=key;
            }
            if(key.spec==null||!Skinned(key.spec))return prototypeId;
            return (key.loose?StandardLoose(key.spec):StandardModel(key.spec,tier))??prototypeId;
        }
        /// <summary>A Prototype model id in the skin the map picks for its weapon (intros, drops: the id names the weapon).</summary>
        public static string ModelIn(string prototypeId,string map,int tier=0)
        {
            if(string.IsNullOrEmpty(prototypeId))return prototypeId;Model(prototypeId,StandardIndex,tier);
            return prototypes.TryGetValue(prototypeId,out var key)&&key.spec!=null?Model(prototypeId,Index(map,key.spec),tier):prototypeId;
        }
        /// <summary>The model id `spec` draws in a hand under the skin map.</summary>
        public static string Held(string spec,string map,int tier=0)=>Model(PrototypeHeld(spec),Index(map,spec),tier);
        private static readonly Dictionary<string,string> prototypeHeld=new Dictionary<string,string>(StringComparer.Ordinal);
        public static string PrototypeHeld(string spec)
        {
            if(string.IsNullOrEmpty(spec))return null;if(!prototypeHeld.TryGetValue(spec,out var id)){id="weapon-"+spec;prototypeHeld[spec]=id;}return id;
        }

        // ---- presentation tier ----
        /// <summary>
        /// The Standard detail tier for one draw: the low graphics tier draws Low, Reduced effects draws Reduced, and a rig far
        /// from the camera steps down (Reduced beyond 16 m, Low beyond 30 m). The first-person view model keeps Full detail
        /// unless the low graphics tier is on (then Reduced).
        /// </summary>
        public static int Tier(bool lowGraphics,float effectsQuality,float distance,bool firstPerson)
        {
            if(firstPerson)return lowGraphics?NativeWeaponModels.Reduced:NativeWeaponModels.Full;
            int tier=lowGraphics?NativeWeaponModels.Low:effectsQuality<1?NativeWeaponModels.Reduced:NativeWeaponModels.Full;
            if(distance>30)tier=NativeWeaponModels.Low;else if(distance>16)tier=Math.Max(tier,NativeWeaponModels.Reduced);
            return tier;
        }
    }
}
