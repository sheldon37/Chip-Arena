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
    // Presentation only: a skin never changes reach, hit volumes, timing or any rule. The map also carries the pilot's body
    // skin as one more entry after the weapons ("body:prototype", "body:sheldon"; Core/NativeBodySkins.cs), so the body pick
    // travels on exactly the same path; an older client drops the entry it does not know and draws its own body.
    // The unarmed DEV Cyber Lord's entry ("cyberlord", owner 2026-10-13) picks its look, not a model: Standard is the glitch
    // aura round the hands (Core/NativeCyberLordAura.cs), the Prototype Series the hardlight gauntlets and fist shells it wore
    // before (Core/NativeCyberLordLook.cs). It has no model ids (Modelled is false), and a missing or unknown pick reads as
    // the aura like every other Standard.
    // The "face" entry (owner 2026-10-26) picks the model's face gear (Runtime/NativeVisualBodyPresentation.cs VisualFaceGear):
    // the robot's helmet (the default since owner 2026-10-30: "I think we will make the default outfit the new designed armor
    // and that full helmet") or half mask (Core/NativeRobotHelm), the concept's tech mask and glowing visor both, the visor
    // alone, the mask alone, or none (the model's own face). It no longer depends on the body skin, which picks the voxel bodies
    // (Classic). Classic ignores it; an older client draws its own face gear only on the Prototype body and reads a key it does
    // not know as its own default.
    // The "suit" entry after it (owner 2026-10-30: "design clothing that resembles our previous player model (Robotic style)")
    // puts the robot suit on the model (Core/NativeRobotSuit; Runtime/NativeRobotSuitPresentation.cs): the robot's (the default
    // outfit, never written) or none. Classic ignores it; an older client draws no suit.
    public static class NativeWeaponSkins
    {
        public const string Standard="standard",Prototype="prototype";
        public static readonly string[] Keys={Standard,Prototype};
        public static readonly string[] Labels={"Standard","Prototype Series"};
        public static readonly string[] Texts={"The current production model, with accent lights in your colour.","The original blocky build the arena started with."};
        public const int StandardIndex=0,PrototypeIndex=1,Count=2;
        /// <summary>The weapons that carry skins (every weapon with a model; the Siren has none), in content order, then the
        /// Cyber Lord, whose skin is its look.</summary>
        public static readonly string[] Weapons={"pistol","rifle","machinegun","fire","water","lightning","darkness","sword","daggers","shield","greatsword","emperor","ansatsuken","leet","bow",CyberLordSpec.Id};
        /// <summary>The longest normalized map (every weapon and the Cyber Lord on the Prototype Series, the body on the Prototype,
        /// the face gear on the visor alone, no suit) is 319 characters; frames refuse longer than 320.</summary>
        public const int MaxLength=320;
        public static bool Skinned(string spec)=>spec!=null&&Array.IndexOf(Weapons,spec)>=0;
        /// <summary>A skinned weapon with weapon models (a Prototype Series bake or build and usually a Standard design): every
        /// entry but the Cyber Lord's, whose two skins are drawn looks (NativeCyberLordLook.PiecesIn).</summary>
        public static bool Modelled(string spec)=>Skinned(spec)&&spec!=CyberLordSpec.Id;
        public static int SkinIndex(string skin)=>skin==Prototype?PrototypeIndex:skin==Standard?StandardIndex:-1;
        /// <summary>The face gear's entry, its keys (the first the default: the robot's helmet; none longer than "visor", so
        /// MaxLength holds), labels and indices.</summary>
        public const string FaceEntry="face";
        public static readonly string[] FaceKeys={"helm","half","both","visor","mask","none"},FaceLabels={"Helmet","Half mask","Mask + visor","Visor","Mask","No mask"};
        public const int FaceHelm=0,FaceHalf=1,FaceBoth=2,FaceVisor=3,FaceMask=4,FaceNone=5;
        /// <summary>The robot suit's entry, its keys (the robot's, the default; none) and labels.</summary>
        public const string SuitEntry="suit";
        public static readonly string[] SuitKeys={"robot","none"},SuitLabels={"Robot suit","None"};
        public const int SuitRobot=0,SuitNone=1;
        /// <summary>Every entry the map carries, in its canonical order: the weapons, then the pilot's body, the face gear and the
        /// suit.</summary>
        public static readonly string[] Entries=Join(Join(Join(Weapons,NativeBodySkins.Entry),FaceEntry),SuitEntry);
        private static string[] Join(string[] a,string b){var o=new string[a.Length+1];Array.Copy(a,o,a.Length);o[a.Length]=b;return o;}
        /// <summary>The skin keys an entry accepts: a weapon's two finishes, the body's three skins, the face gear's five or the suit's two.</summary>
        private static string[] KeysOf(string entry)=>entry==NativeBodySkins.Entry?NativeBodySkins.Keys:entry==FaceEntry?FaceKeys:entry==SuitEntry?SuitKeys:Keys;
        private static int SkinIndex(string entry,string skin){var keys=KeysOf(entry);for(int k=0;k<keys.Length;k++)if(keys[k]==skin)return k;return -1;}

        /// <summary>The skin picked for `spec` (a weapon, or the body entry) in a map (StandardIndex when absent or malformed). Allocation free.</summary>
        public static int Index(string map,string spec)
        {
            if(string.IsNullOrEmpty(map)||string.IsNullOrEmpty(spec))return StandardIndex;
            int i=0,n=map.Length,result=StandardIndex;var keys=KeysOf(spec);
            while(i<n)
            {
                int end=map.IndexOf(',',i);if(end<0)end=n;int colon=map.IndexOf(':',i,end-i);
                if(colon>i&&colon-i==spec.Length&&string.CompareOrdinal(map,i,spec,0,spec.Length)==0)
                {
                    int len=end-colon-1;
                    for(int k=0;k<keys.Length;k++)if(keys[k].Length==len&&string.CompareOrdinal(map,colon+1,keys[k],0,len)==0){result=k;break;}
                }
                i=end+1;
            }
            return result;
        }
        public static string Of(string map,string spec)=>KeysOf(spec)[Index(map,spec)];
        /// <summary>
        /// The canonical form: entries "spec:skin" for known entries (the weapons, then the body) on a non-default skin that entry
        /// accepts, in Entries order, each once (the last entry wins), joined by commas; "" when everything is Standard. Never
        /// longer than MaxLength. A non-DEV account's Sheldon Clothes is dropped separately (NativeBodySkins.Gate).
        /// </summary>
        public static string Normalize(string map)
        {
            if(string.IsNullOrEmpty(map))return "";
            if(map.Length>4096)map=map.Substring(0,4096);
            var picks=new int[Entries.Length];
            foreach(var entry in map.Split(','))
            {
                int colon=entry.IndexOf(':');if(colon<=0)continue;string spec=entry.Substring(0,colon).Trim(),skin=entry.Substring(colon+1).Trim();
                int w=Array.IndexOf(Entries,spec);if(w<0)continue;int k=SkinIndex(spec,skin);if(k<0)continue;picks[w]=k;
            }
            return Join(picks);
        }
        private static string Join(int[] picks)
        {
            var sb=new StringBuilder();
            for(int w=0;w<Entries.Length;w++)if(picks[w]!=StandardIndex){if(sb.Length>0)sb.Append(',');sb.Append(Entries[w]).Append(':').Append(KeysOf(Entries[w])[picks[w]]);}
            return sb.ToString();
        }
        /// <summary>The map with `spec` (a weapon or the body entry) set to `skin` (normalized).</summary>
        public static string With(string map,string spec,string skin)
        {
            int w=Array.IndexOf(Entries,spec),k=w<0?-1:SkinIndex(spec,skin);if(w<0||k<0)return Normalize(map);
            var picks=new int[Entries.Length];for(int i=0;i<Entries.Length;i++)picks[i]=Index(map,Entries[i]);picks[w]=k;return Join(picks);
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
