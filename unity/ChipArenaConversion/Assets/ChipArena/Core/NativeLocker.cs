using System;
using System.Collections.Generic;
using System.Globalization;

namespace ChipArena.UnityConversion
{
    // The Locker (the Customize screen, Runtime/NativeLockerUI.cs): its five categories, the short card text for every
    // cosmetic, and each weapon's ability slots read from content. Presentation only: nothing here is saved, sent or
    // validated; the saved look stays the profile's existing fields (prefs: name, colours, hat, lobby pose, kill effect,
    // intro, intro text, preferred weapon), and which cosmetics a player may see stays NativeCosmeticRules.Allowed.
    public static class NativeLocker
    {
        public const int Appearance=0,KillEffects=1,Entrance=2,Loadout=3,WeaponSkins=4,Count=5;
        public static readonly string[] Keys={"appearance","kill-effects","entrance","loadout","weapon-skins"};
        public static readonly string[] Labels={"Appearance","Kill Effects","Entrance","Loadout","Weapon Skins"};
        public static readonly string[] Icons={"palette","skull","sparkles","swords","gem"};
        public static readonly string[] Subtitles=
        {
            "Callsign, colours, headwear, idle pose and backdrop.",
            "How opponents see you finish them.",
            "Your match intro and the line under your name.",
            "Preferred class and weapon, and that weapon's abilities.",
            "Finishes for every weapon. Coming soon.",
        };
        // Weapon skins are a future feature: the tab shows locked silhouettes and nothing in it can be picked or saved.
        public static bool Interactive(int category)=>category!=WeaponSkins;
        public static bool Soon(int category)=>category==WeaponSkins;

        // Restricted cosmetics (shown only when NativeCosmeticRules.Allowed lets them through) carry a DEV badge.
        public static bool DevItem(string id)=>id!=null&&(id.StartsWith("dev-",StringComparison.Ordinal)||id=="auditor-halo");
        // A cosmetic label without its DEV marking (the badge says it instead): "Gibber · DEV ONLY" -> "Gibber",
        // "DEV · Rain Warrior" -> "Rain Warrior".
        public static string Clean(string label)
        {
            if(string.IsNullOrEmpty(label))return "";
            if(label.EndsWith(" · DEV ONLY",StringComparison.Ordinal))label=label.Substring(0,label.Length-11);
            if(label.StartsWith("DEV · ",StringComparison.Ordinal))label=label.Substring(6);
            return label;
        }

        // ---- Card text ---------------------------------------------------------------------------------------------
        // Kill effects, from what each one draws (Core/NativeKillEffectRules.cs, Runtime/NativeDeathPresentation.cs and the
        // baked source death scenes).
        public static string KillText(string id)
        {
            switch(id)
            {
                case "impact-ragdoll":return "Ragdolls on the final hit; heavy blows sever and gib.";
                case "ragdoll":return "A classic limp ragdoll that sinks away.";
                case "ragdoll-ember":return "Ragdoll trailed by rising embers.";
                case "ragdoll-frost":return "Freezes solid, then shatters into frost.";
                case "ragdoll-static":return "Sparks crackle along every limb.";
                case "ragdoll-gravity":return "A violet vortex pulls in, then collapses.";
                case "ragdoll-pinball":return "Rainbow sparks bounce off the body.";
                case "ragdoll-fireworks":return "Bursts in a rainbow firework finale.";
                case "ragdoll-soul":return "Turns spectral and drifts away.";
                case "ragdoll-petals":return "A flurry of petals scatters around.";
                case "voxel-burst":return "Shatters into tumbling voxel cubes.";
                case "confetti":return "Pops in a cloud of confetti.";
                case "ascension":return "Beamed up and away.";
                case "shrink":return "Shrinks down to pocket size.";
                case "launch":return "Blasted off into the sky.";
                case "ghost":return "A ghost slips out and floats away.";
                case "dev-gibber":return "Maximum gore: severed parts and spray.";
                case "dev-singularity":return "A black hole swallows the body.";
                case "dev-raging-demon":return "A flurry of flashes, then a cinematic finish.";
                case "dev-clown":return "Makes a clown-car getaway.";
                case "dev-clown-love":return "The clown getaway, with a love song.";
                case "dev-bass-van":return "The bass van rolls in to collect.";
                default:return "";
            }
        }
        // The effect's emblem colour: the RagdollVfx style colours (NativeKillSwarm) and a tone for the others.
        public static int KillColor(string id)
        {
            switch(id)
            {
                case "impact-ragdoll":return 0xff6a5c;case "ragdoll":return 0xc0c1ca;case "ragdoll-ember":return 0xff762d;case "ragdoll-frost":return 0x9feaff;
                case "ragdoll-static":return 0xffe667;case "ragdoll-gravity":return 0xa47eff;case "ragdoll-pinball":return 0x70ffc9;case "ragdoll-fireworks":return 0xffa1e2;
                case "ragdoll-soul":return 0x95dfed;case "ragdoll-petals":return 0xff98c4;case "dev-singularity":return 0xd7b4ff;case "voxel-burst":return 0xffc590;
                case "confetti":return 0xffd27a;case "ascension":return 0xfff1bb;case "shrink":return 0x93c5fd;case "launch":return 0xffd27a;case "ghost":return 0xe6f4ff;
                case "dev-gibber":return 0xff3b47;case "dev-raging-demon":return 0xff2d55;case "dev-clown":return 0xff6575;case "dev-clown-love":return 0xf18dcc;case "dev-bass-van":return 0x69a6ff;
                default:return 0xc0c1ca;
            }
        }
        public static string HatText(string id)
        {
            switch(id)
            {
                case "none":return "Nothing on top. A clean silhouette.";
                case "cap":return "A sporty ball cap.";
                case "beanie":return "A snug knit beanie.";
                case "top":return "Tall, formal and dapper.";
                case "wizard":return "A pointed hat for spellslingers.";
                case "crown":return "For the arena's royalty.";
                case "cone":return "Borrowed from the roadworks.";
                case "antenna":return "Picks up every signal.";
                case "halo":return "A soft ring above the head.";
                case "cowboy":return "Wide brim, frontier style.";
                case "auditor-halo":return "Red broken arcs ringed by black flames.";
                default:return "";
            }
        }
        // Lobby poses in NativeLobbyPoses.Keys order (weapon, chair, sleep, crossed).
        public static readonly string[] PoseTexts={"A ready stance with your weapon.","Lounging on the lobby chair.","Napping until the match starts.","Arms crossed, waiting."};
        public static string PoseText(int index)=>index>=0&&index<PoseTexts.Length?PoseTexts[index]:"";

        // Entrances: the procedural entrances by what they stage, the weapon intros as that weapon's signature, the DEV entrances.
        public static string IntroName(string id,string label)=>Clean(label);
        public static string IntroText(string id)
        {
            switch(id)
            {
                case "orbital-drop":return "A drop pod slams down and blows its door open.";
                case "grid-rez":return "Built voxel by voxel out of the arena grid.";
                case "holo-projection":return "Flickers in as a hologram, then locks solid.";
                case "blackout":return "The lights cut. A spotlight slams on.";
                case "skyline-zip":return "Rides a cable in from the skyline.";
                case "hero-landing":return "Falls from the sky into a crater landing.";
                case "overclock":return "Installs a chip and surges with power.";
                case "dev-siren":return "The Siren's boss protocol reveal.";
                case "dev-emperor":return "The Emperor's storm entrance.";
                case "dev-ansatsuken":return "The rain warrior's entrance.";
                case "dev-matrix":return "The L33T throne entrance.";
            }
            if(id!=null&&id.StartsWith("weapon-",StringComparison.Ordinal))return Title(id.Substring(7))+" signature entrance.";
            return "";
        }
        public static string IntroIcon(string id)
        {
            switch(id)
            {
                case "orbital-drop":return "rocket";case "grid-rez":return "boxes";case "holo-projection":return "scan";case "blackout":return "eclipse";
                case "skyline-zip":return "spline";case "hero-landing":return "chevrons-down-up";case "overclock":return "cpu";
                case "dev-siren":return "skull";case "dev-emperor":return "cloud-lightning";case "dev-ansatsuken":return "hand-fist";case "dev-matrix":return "terminal";
            }
            return null;
        }
        private static string Title(string spec)=>spec=="machinegun"?"Machine gun":spec=="greatsword"?"Greatsword":string.IsNullOrEmpty(spec)?"":char.ToUpperInvariant(spec[0])+spec.Substring(1);

        // ---- Ability slots -----------------------------------------------------------------------------------------
        // One choice in a weapon's Q or E slot. Strings are built once per weapon (SlotOptions), never per frame.
        public sealed class AbilityOption
        {
            public string Id,Name,Text,Icon,Meta;public double Cooldown,Cost;
            // True for a slot read from the weapon itself (a melee special or a DEV kit's move), not from content abilities.
            public bool FromWeapon;
        }
        // The options for `slot` ("q" or "e") of weapon `w`: every content ability for that spec and slot, in content order
        // (today exactly one per slot for ranged and magic). Without content abilities, the weapon's own move for that key
        // from its description (melee E: its stamina special; DEV kits: their Q / E moves). Returns the count.
        public static int SlotOptions(ConversionContent content,ContentWeapon w,string slot,List<AbilityOption> into)
        {
            into.Clear();if(w==null)return 0;
            if(content?.abilities!=null)foreach(var a in content.abilities)if(a!=null&&a.spec==w.id&&a.slot==slot)into.Add(FromContent(a,w));
            if(into.Count>0)return into.Count;
            string move=Move(w.description,slot=="q"?"Q: ":"E: ");if(move==null)return 0;
            bool melee=w.@class=="melee"&&!w.devOnly,e=slot=="e";string name=e&&melee?MeleeSpecial(w.id):null;
            int words=move.TrimEnd('.').Split(' ').Length;
            var o=new AbilityOption{Id=e?w.secondary:"q-"+w.id,FromWeapon=true,Icon=AbilityIcon(e?w.secondary:null,w)};
            if(name!=null){o.Name=name;o.Text=move;}else if(words<=3){o.Name=move.TrimEnd('.');o.Text="Signature move of this kit.";}else{o.Name="Signature move";o.Text=move;}
            if(e){o.Cooldown=w.secondaryCooldown;o.Cost=w.secondaryCost;}
            o.Meta=Meta(o.Cooldown,o.Cost,w.@class=="melee"?"stamina":"mana",false);into.Add(o);return 1;
        }
        // Why a slot is empty: melee Q abilities are planned (docs/MELEE-ABILITIES.md); a DEV kit binds its own keys.
        public static string EmptySlot(ContentWeapon w,string slot)
        {
            if(w==null)return "";if(w.devOnly)return "This DEV kit uses its own keys. See the weapon description.";
            if(w.@class=="melee"&&slot=="q")return "Melee Q abilities are on the way.";
            return "No ability in this slot yet.";
        }
        // The HUD's names for the four melee specials (ConversionGame.AbilityStatus).
        public static string MeleeSpecial(string spec)=>spec=="sword"?"Rising slash":spec=="daggers"?"Dagger throw":spec=="shield"?"Shield throw":spec=="greatsword"?"Wolf leap":null;
        private static AbilityOption FromContent(ContentAbility a,ContentWeapon w)
        {
            bool hold=a.kind=="ads";
            return new AbilityOption{Id=a.id,Name=a.name,Text=a.text,Icon=AbilityIcon(a.id,w),Cooldown=a.cooldown,Cost=a.cost,Meta=Meta(a.cooldown,a.cost,w.@class=="melee"?"stamina":"mana",hold)};
        }
        // "8 s cooldown · 16 mana", "Hold · no cost".
        public static string Meta(double cooldown,double cost,string unit,bool hold)
        {
            var inv=CultureInfo.InvariantCulture;string cd=hold?"Hold":cooldown>0?cooldown.ToString("0.#",inv)+" s cooldown":"No cooldown";
            return cd+" · "+(cost>0?cost.ToString("0.#",inv)+" "+unit:"no cost");
        }
        // The "E: ..." (or "Q: ...") sentence of a weapon description, capitalised, its stamina note dropped; null when absent.
        public static string Move(string description,string key)
        {
            if(string.IsNullOrEmpty(description))return null;int at=description.IndexOf(key,StringComparison.Ordinal);if(at<0)return null;
            string s=description.Substring(at+key.Length);int end=s.IndexOf(". ",StringComparison.Ordinal);if(end>=0)s=s.Substring(0,end);s=s.TrimEnd('.').Trim();
            int open=s.IndexOf(" (",StringComparison.Ordinal);if(open>=0&&s.EndsWith(")",StringComparison.Ordinal))s=s.Substring(0,open);
            if(s.Length==0)return null;return char.ToUpperInvariant(s[0])+s.Substring(1)+".";
        }
        public static string AbilityIcon(string id,ContentWeapon w)
        {
            switch(id)
            {
                case "ads":return w!=null&&w.id=="rifle"?"scan-line":"crosshair";case "frag-grenade":return "bomb";case "rocket":return "rocket";case "orbital-mark":return "target";
                case "flame-lance":return "flame";case "meteor-strike":return "sun";case "tidal-wave":return "waves";case "geyser-wall":return "droplets";
                case "blink-strike":return "zap";case "ball-lightning":return "orbit";case "shadow-shackles":return "anchor";case "singularity":return "eclipse";
                case "sword-launch":return "move-up";case "dagger-throw":return "corner-up-right";case "shield-throw":return "shield";case "wolf-spin":return "rotate-cw";
                case "wavedash":return "wind";case "tatsu":return "rotate-cw";case "root":return "terminal";
            }
            return "sparkles";
        }
    }
}
