using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Pilot body skins (owner 2026-10-12: "Sharper armored voxel" — the same blocky silhouette and proportions with panel lines,
    // bevelled plates, knee and elbow joints, a glowing visor, accent strips and three detail tiers like the weapons; the current
    // body stays selectable as "Prototype"; later the same day the DEV-only "Sheldon Clothes", a sleeker robed design).
    //  - Standard (the default): Core/NativeBodyModels.cs, sculpted in code.
    //  - Prototype: the exported source pilot ("pilot" in SourceModels.json), byte-identical.
    //  - Sheldon Clothes: a long dark coat over a slimmer frame (DEV only: NativeCosmeticGate, like the Sheldon Head).
    // Every skin has exactly the exported pilot's parts (names, joints, order), so animation, hats, wounds, corpses, portraits and
    // every per-part rule see the same rig; the new detail lives inside the old part volumes plus a few cm of relief. The pick
    // travels in the weapon-skin map as the "body" entry ("body:prototype", "body:sheldon"; NativeWeaponSkins), so prefs, the
    // account profile, the seat frames and old clients carry it with no new field. Presentation only: no skin is ever read by
    // hit volumes, colliders, damage, timing, movement or networking.
    public static class NativeBodySkins
    {
        /// <summary>The skin map's entry for the body (after the weapons in NativeWeaponSkins.Entries).</summary>
        public const string Entry="body";
        public const string Standard="standard",Prototype="prototype",Sheldon="sheldon";
        public static readonly string[] Keys={Standard,Prototype,Sheldon};
        public static readonly string[] Labels={"Standard","Prototype","Sheldon Clothes"};
        public static readonly string[] Texts={"Sharper armoured plates, a glowing visor and accent strips in your colours.","The original blocky build the arena started with.","A long dark coat with glowing seams. DEV only."};
        public const int StandardIndex=0,PrototypeIndex=1,SheldonIndex=2,Count=3;
        /// <summary>The DEV cosmetic id of the Sheldon Clothes (NativeCosmeticGate.DevOnly: live DEV access, no grant).</summary>
        public const string SheldonClothes="sheldon-clothes";
        public const int Full=0,Reduced=1,Low=2,Tiers=3;
        /// <summary>Triangle budgets per tier (Full, Reduced, Low) for every built skin; eight pilots at Full stay under 8 x Budget[0].</summary>
        public static readonly int[] Budget={10000,6000,3200};
        /// <summary>Most a part may grow past the exported part's box (m): the Standard armour's relief, the Sheldon coat's drape.</summary>
        public const float StandardRelief=.015f,SheldonRelief=.03f;

        public const string PrototypeModel="pilot";
        private static readonly string[][] ids={new[]{"pilot-std","pilot-std-reduced","pilot-std-low"},new[]{PrototypeModel,PrototypeModel,PrototypeModel},new[]{"pilot-sheldon","pilot-sheldon-reduced","pilot-sheldon-low"}};
        /// <summary>A skin key's index (Standard for null, empty or anything unknown).</summary>
        public static int Index(string skin)=>skin==Prototype?PrototypeIndex:skin==Sheldon?SheldonIndex:StandardIndex;
        /// <summary>A key's index or -1 when it is not a body skin (map parsing).</summary>
        public static int KeyIndex(string skin)=>skin==Standard?StandardIndex:skin==Prototype?PrototypeIndex:skin==Sheldon?SheldonIndex:-1;
        /// <summary>The body skin a map picks (Standard when absent or malformed). Allocation free.</summary>
        public static int Of(string map)=>NativeWeaponSkins.Index(map,Entry);
        /// <summary>The bank model id of a skin at a tier: cached strings, so choosing a model allocates nothing.</summary>
        public static string Model(int skin,int tier)=>ids[skin<0||skin>=Count?StandardIndex:skin][Math.Max(Full,Math.Min(Low,tier))];
        public static string Model(string map,int tier)=>Model(Of(map),tier);
        /// <summary>Every bank id a pilot body draws with (the Prototype first).</summary>
        public static IEnumerable<string> Models(){yield return PrototypeModel;for(int s=0;s<Count;s++)if(s!=PrototypeIndex)for(int t=0;t<Tiers;t++)yield return ids[s][t];}
        /// <summary>True for any pilot body model (the Prototype "pilot" and every Standard / Sheldon tier).</summary>
        public static bool IsBody(string id)=>id==PrototypeModel||id!=null&&id.StartsWith("pilot-",StringComparison.Ordinal)&&SkinOf(id)>=0;
        /// <summary>The skin of a body model id, -1 for any other id.</summary>
        public static int SkinOf(string id)
        {
            if(id==null)return -1;for(int s=0;s<Count;s++)for(int t=0;t<Tiers;t++)if(ids[s][t]==id)return s;return -1;
        }
        /// <summary>The detail tier, by the weapon skins' rule (NativeWeaponSkins.Tier): graphics tier, Reduced effects, distance.</summary>
        public static int Tier(bool lowGraphics,float effectsQuality,float distance,bool firstPerson)=>NativeWeaponSkins.Tier(lowGraphics,effectsQuality,distance,firstPerson);

        // ---- the DEV gate ----
        /// <summary>
        /// The map with a Sheldon Clothes pick dropped back to Standard unless `allowed` (live DEV access; NativeCosmeticGate):
        /// run on save, on an account load and when the host builds a seat, so no other client ever sees it on a non-DEV pilot.
        /// </summary>
        public static string Gate(string map,bool allowed)
        {
            map=NativeWeaponSkins.Normalize(map);if(allowed||Of(map)!=SheldonIndex)return map;
            return NativeWeaponSkins.With(map,Entry,Standard);
        }
        public static bool Allowed(NativeAccess access,double now)=>NativeCosmeticGate.Allowed(access,"body",SheldonClothes,now);
        /// <summary>The same answer without building the grant key (a DEV-only id needs live DEV access, which grants everything):
        /// for the cards that ask every frame.</summary>
        public static bool LiveDeveloper(NativeAccess access,double now)=>access!=null&&access.Developer&&access.Expires>now;

        // ---- colours ----
        /// <summary>The cyber palette's cyan and magenta (Runtime NativeCyberTheme.CyanHex / MagentaHex): the Standard body's
        /// fixed status lights, where the game has no colour of its own.</summary>
        public const int CyberCyan=0x22e6ff,CyberMagenta=0xff3dcf;
        /// <summary>
        /// The colour the Sheldon Clothes' seams glow in (they ride the accent tint): the Sheldon Head's glow when it is worn (the
        /// hat colour, cyan on Auto), else a picked hat colour, else the pilot's accent. Returns one of its inputs or a constant,
        /// so a draw allocates nothing.
        /// </summary>
        public static string SheldonGlow(string hat,string hatColor,string accent)
        {
            bool picked=!NativeHatLooks.IsAuto(hatColor);
            if(hat==NativeHatLooks.SheldonHead)return picked?hatColor:SheldonCyan;
            return picked?hatColor:accent;
        }
        public const string SheldonCyan="#22e6ff";
        /// <summary>The same choice as an RGB value (the portraits' integer colours).</summary>
        public static int SheldonGlowRgb(string hat,string hatColor,int accent)
        {
            int picked=NativeHatLooks.Rgb(NativeHatLooks.Normalize(hatColor));
            if(hat==NativeHatLooks.SheldonHead)return picked>=0?picked:CyberCyan;
            return picked>=0?picked:accent;
        }

        // ---- the exported pilot's parts (SourceModels.json "pilot", in section order; every skin builds exactly these) ----
        public readonly struct PartSpec
        {
            public readonly string Name;public readonly int Bone;public readonly bool Emitter;
            public PartSpec(string name,int bone,bool emitter){Name=name;Bone=bone;Emitter=emitter;}
        }
        public static readonly PartSpec[] Parts=
        {
            new PartSpec("blank-head",5,false),new PartSpec("crown",5,false),new PartSpec("jaw",5,false),new PartSpec("neck-joint",4,false),
            new PartSpec("ribcage",3,false),new PartSpec("upper-chest",3,false),new PartSpec("collar",3,false),new PartSpec("sternum",3,true),new PartSpec("back-panel",3,false),
            new PartSpec("left-cuff",9,true),new PartSpec("left-palm",9,false),new PartSpec("left-fingers",9,false),new PartSpec("left-thumb",9,false),
            new PartSpec("left-elbow-joint",8,false),new PartSpec("left-forearm",8,false),new PartSpec("left-upper-arm",7,false),new PartSpec("left-shoulder-cap",6,true),
            new PartSpec("right-cuff",13,true),new PartSpec("right-palm",13,false),new PartSpec("right-fingers",13,false),new PartSpec("right-thumb",13,false),
            new PartSpec("right-elbow-joint",12,false),new PartSpec("right-forearm",12,false),new PartSpec("right-upper-arm",11,false),new PartSpec("right-shoulder-cap",10,true),
            new PartSpec("armor-back-light",3,true),new PartSpec("abdomen",2,false),new PartSpec("abdomen-shell",2,false),new PartSpec("hip-shell",1,false),new PartSpec("waist-band",1,true),
            new PartSpec("left-foot",17,false),new PartSpec("left-heel",16,false),new PartSpec("left-knee-joint",15,false),new PartSpec("left-kneecap",15,true),new PartSpec("left-shin",15,false),
            new PartSpec("left-calf",15,false),new PartSpec("armor-shin-light",15,true),new PartSpec("left-hip-joint",14,false),new PartSpec("left-thigh",14,false),
            new PartSpec("right-foot",21,false),new PartSpec("right-heel",20,false),new PartSpec("right-knee-joint",19,false),new PartSpec("right-kneecap",19,true),new PartSpec("right-shin",19,false),
            new PartSpec("right-calf",19,false),new PartSpec("armor-shin-light",19,true),new PartSpec("right-hip-joint",18,false),new PartSpec("right-thigh",18,false),
        };
        /// <summary>The index of the part named `name` on joint `bone` (the shin lights share one name), -1 if none.</summary>
        public static int PartIndex(string name,int bone){for(int i=0;i<Parts.Length;i++)if(Parts[i].Name==name&&Parts[i].Bone==bone)return i;return -1;}
    }
}
