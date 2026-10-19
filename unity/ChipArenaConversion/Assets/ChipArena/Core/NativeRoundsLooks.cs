using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>The family a ROUNDS pack field is drawn as (NativeRoundsFields).</summary>
    public enum RoundsStyle:byte{None=0,Fire=1,Frost=2,Time=3,Push=4,Pull=5,Storm=6,Pulse=7}

    /// <summary>The bespoke proc and buff looks a pilot's pack chips give (NativeRoundsProcs), as flags.</summary>
    [Flags]
    public enum RoundsProc
    {
        None=0,Phase=1,Packet=2,Grapple=4,Tractor=8,Stasis=16,Execute=32,LastRound=64,Reclaim=128,Transit=256,
        GuardPulse=512,ChargePulse=1024,ArcFault=2048,Fragment=4096,Thorns=8192,BlockHeal=16384,Regen=32768,ReloadHeal=65536,Vent=131072
    }

    /// <summary>One field look: its family, the pack chip it belongs to and its palette (linear: hot, core, cool, dark; and the
    /// same as sRGB hex, which the field surfaces carry to their shader in the vertex normal slot).</summary>
    public struct RoundsFieldLook
    {
        public RoundsStyle Style;public int Variant;public string Chip,Name;public Vector3 Hot,Core,Cool,Dark;public int HotHex,CoreHex,CoolHex,DarkHex;
    }

    /// <summary>
    /// The ROUNDS adaptation packs' presentation catalogue (owner 2026-10-19: "Go ahead and do the visuals for this thing as well:
    /// Some effects may have no visuals of their own yet, such as Escort Array's orbiting satellites, Drone Nest's combat toys and
    /// the fire, frost and slow-time fields."). Astra's packs (Core NativeRoundsCards, NativeRoundsPacks) reuse vanilla mechanics
    /// by alias: a pack field carries the vanilla mechanic id as its effect (hot-foot, blizzard, time-warp, ...), a Drone Nest toy
    /// is a vanilla "toy" minion, the satellites are only a position at firing. What tells a pack effect from the vanilla one is
    /// the owner's replicated seat chips: the alias rules exclude a vanilla counterpart and its aliases from each other, so a
    /// pilot holds at most one chip per mechanic. This resolves which pack chip (if any) made an effect, in a fixed order, so
    /// host, guests, the killcam and the DEV preview pick the same look. A pilot who holds the vanilla chip itself keeps the
    /// vanilla look. Also the 138 pack chips' card icons (Runtime NativeIcons.Chip, through the native chip icon table). Presentation
    /// only: nothing here is read by the authority, the rules or the network.
    /// </summary>
    public static class NativeRoundsLooks
    {
        public const string Cards="rounds-cardsplus:";
        private static Vector3 L(int hex,float gain=1){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b)*gain;}
        private static RoundsFieldLook F(RoundsStyle s,int v,string chip,string name,int hot,int core,int cool,int dark)=>new RoundsFieldLook{Style=s,Variant=v,Chip=chip,Name=name,Hot=L(hot),Core=L(core),Cool=L(cool),Dark=L(dark),HotHex=hot,CoreHex=core,CoolHex=cool,DarkHex=dark};
        /// <summary>Every field look; its index is what the presenter passes on (Looks[i]).</summary>
        public static readonly RoundsFieldLook[] Looks=
        {
            F(RoundsStyle.Fire,0,Cards+"hot-potato","Ember Footprint",0xffb347,0xfff1c4,0xff4a12,0x120806),
            F(RoundsStyle.Fire,1,"rounds-arcana:the-empress","Thornwake",0xd9ff6a,0xfff8c8,0xff7a1c,0x10140a),
            F(RoundsStyle.Frost,0,Cards+"hamper","Cryo Snare",0x9fe9ff,0xf2fdff,0x3f9be0,0x0b2236),
            F(RoundsStyle.Frost,1,"rounds-arcana:the-hierophant","Binding Lattice",0xffe2a0,0xfffbef,0x9fd8ff,0x16202c),
            F(RoundsStyle.Time,0,Cards+"smoke-grenade","Suppression Cloud",0x7f9cff,0xe4ecff,0x3a4fd8,0x070a1c),
            F(RoundsStyle.Time,1,"rounds-arcana:the-hanged-man","Temporal Sacrifice",0xff6a9a,0xffe2ec,0x8a2bd8,0x14061a),
            F(RoundsStyle.Time,2,Cards+"reboot-optics","Disruption Pulse",0x5ff6ff,0xeaffff,0xff4fd8,0x04121a),
            F(RoundsStyle.Time,3,"rounds-arcana:the-moon","Lunar Interdict",0xc9d8ff,0xffffff,0x7f8fd8,0x0a0d1a),
            F(RoundsStyle.Time,4,"rounds-wwc:banishment","Shadow Interdict",0xa46bff,0xe6d6ff,0x3b1478,0x05020b),
            F(RoundsStyle.Push,0,"rounds-vecs:anti-gravity-wells","Repulsor Well",0xff6fae,0xffe0ee,0xd0247a,0x1a0610),
            F(RoundsStyle.Push,1,"rounds-vecs:panic","Emergency Vent",0xff5a3a,0xfff0e0,0xd8261a,0x1c0604),
            F(RoundsStyle.Pull,0,"rounds-vecs:gravity-wells","Attractor Well",0xb47bff,0xf2e8ff,0x5b2fd8,0x030108),
            F(RoundsStyle.Storm,0,"rounds-arcana:the-tower","Storm Relay",0xd7a6ff,0xfbf0ff,0x8a4cff,0x0c0818),
        };
        // The vanilla mechanic a pack field's effect names, and the looks of its pack aliases in resolve order.
        private static readonly (string effect,int[] looks)[] fieldAliases=
        {
            ("hot-foot",new[]{0,1}),("blizzard",new[]{2,3}),("time-warp",new[]{4,5}),("freezer-block",new[]{6,7,8}),
            ("repulsion-pad",new[]{9}),("panic-button",new[]{10}),("black-hole",new[]{11}),("lightning-rod",new[]{12}),
        };
        private static int Count(IReadOnlyDictionary<string,int> chips,string id)=>chips!=null&&id!=null&&chips.TryGetValue(id,out int n)&&n>0?n:0;
        /// <summary>
        /// The look of a chip field whose effect is `effect` (the replicated field effect: the vanilla mechanic id) made by an owner
        /// holding `chips`, or -1 when it is not a pack field (the vanilla presentation draws it). A pilot holding the vanilla chip
        /// keeps the vanilla look; otherwise the first pack alias it holds, in table order.
        /// </summary>
        public static int FieldLook(string effect,IReadOnlyDictionary<string,int> chips)
        {
            if(effect==null||chips==null||Count(chips,effect)>0)return -1;
            for(int i=0;i<fieldAliases.Length;i++)
            {
                if(fieldAliases[i].effect!=effect)continue;var looks=fieldAliases[i].looks;
                for(int k=0;k<looks.Length;k++)if(Count(chips,Looks[looks[k]].Chip)>0)return looks[k];
                return -1;
            }
            return -1;
        }
        /// <summary>A ground pulse ring's look (style Pulse; variant 0 ring, 1 arcs, 2 fragments, 3 freeze).</summary>
        public static RoundsFieldLook Pulse(int variant,int hot,int core,int cool)=>F(RoundsStyle.Pulse,variant,null,"pulse",hot,core,cool,0x000000);
        /// <summary>The look index of chip `id`'s field, or -1 (checks, the DEV preview).</summary>
        public static int LookOf(string id){for(int i=0;i<Looks.Length;i++)if(Looks[i].Chip==id)return i;return -1;}

        // ---- satellites (the orbitals alias) ----
        /// <summary>The satellite looks: Escort Array (Excalibur: blade escorts), Twin Escort (Twin Scythe: crescent blades),
        /// Manifest Array (The Magician: arcane prisms), Sentry Array (Sentry Gun: turret pods).</summary>
        public static readonly string[] SatelliteChips={Cards+"excalibur","rounds-rsc:twin-scythe","rounds-arcana:the-magician","rounds-cpc:sentry-gun"};
        public static readonly string[] SatelliteNames={"Escort Array","Twin Escort","Manifest Array","Sentry Array"};
        /// <summary>Hot, core and dark tones per satellite look (linear).</summary>
        public static readonly Vector3[] SatelliteHot={L(0xffd27a),L(0xff5a6a),L(0xc28bff),L(0x9dff8a)},SatelliteCore={L(0xfff6dc),L(0xffe3e6),L(0xf3e8ff),L(0xeaffe0)},SatelliteBody={L(0x2b2f3a),L(0x2a1c22),L(0x221a33),L(0x1f2a24)};
        /// <summary>The satellite look of a pilot holding `chips`, or -1 (none, or the vanilla Satellite Office).</summary>
        public static int SatelliteLook(IReadOnlyDictionary<string,int> chips)
        {
            if(chips==null||Count(chips,"orbitals")>0)return -1;
            for(int i=0;i<SatelliteChips.Length;i++)if(Count(chips,SatelliteChips[i])>0)return i;return -1;
        }
        /// <summary>
        /// Where the authority puts satellite `side` (-1 or 1) of a pilot whose body is at `body` at time `t`, exactly as
        /// NativeChips.SpawnChipShots places a homing shot (centre body + .55 m up; angle 4t, the -1 side half a turn on; .75 m out,
        /// a .25 m figure-eight bob). The presenter draws the satellites here on the presentation clock, so every homing shot leaves
        /// its satellite. The source scan in the checks keeps this mirror in step with the authority.
        /// </summary>
        public static Vector3 SatelliteAt(Vector3 body,float t,int side)
        {
            float a=t*4+(side>0?0:(float)Math.PI);return new Vector3(body.X+(float)Math.Sin(a)*.75f,body.Y+.55f+.25f*(float)Math.Sin(a*2),body.Z+(float)Math.Cos(a)*.75f);
        }
        public const float SatelliteRadius=.75f,SatelliteLift=.55f,SatelliteSpin=4;

        // ---- the drone (the puppet-master alias) ----
        public const string DroneChip=Cards+"snake-attack";
        /// <summary>A pilot holding Drone Nest (and not the vanilla Toy Soldier): its "toy" minions are drawn as combat drones.</summary>
        public static bool Drone(IReadOnlyDictionary<string,int> chips)=>chips!=null&&Count(chips,"puppet-master")==0&&Count(chips,DroneChip)>0;
        /// <summary>Drone Nest's toy life (NativeChips.ImpactChips: Summon(owner,1,"toy",point,6)), its strike windup and the toy
        /// stats the drone's timing mirrors (NativeSummons.StepMinions: windup .45 s, recover .55 s; shots from .25 m above it).</summary>
        public const float DroneLife=6,DroneWindup=.45f,DroneRecover=.55f,DroneMuzzle=.25f;

        // ---- procs and buffs ----
        private static readonly (string chip,RoundsProc proc)[] procs=
        {
            (Cards+"phantom",RoundsProc.Phase),(Cards+"adware",RoundsProc.Packet),(Cards+"burnout",RoundsProc.Execute),(Cards+"short-circuit",RoundsProc.ArcFault),
            (Cards+"reboot-optics",RoundsProc.Stasis),("rounds-arcana:the-moon",RoundsProc.Stasis),("rounds-wwc:banishment",RoundsProc.Stasis),
            ("rounds-bsc:get-over-here",RoundsProc.Grapple),("rounds-pce:tractor-beam",RoundsProc.Tractor),("rounds-bsc:omega",RoundsProc.LastRound),
            ("rounds-arcana:death",RoundsProc.Reclaim),("rounds-pce:mulligan",RoundsProc.Reclaim),("rounds-vecs:teleporter",RoundsProc.Transit),("rounds-vecs:panic",RoundsProc.Vent),
            ("rounds-rsc:harming-field",RoundsProc.GuardPulse),("rounds-arcana:the-chariot",RoundsProc.ChargePulse),("rounds-pce:fragmentation",RoundsProc.Fragment),
            ("rounds-arcana:justice",RoundsProc.Thorns),("rounds-bsc:thorns",RoundsProc.Thorns),
            ("rounds-vecs:consume",RoundsProc.BlockHeal),("rounds-arcana:the-lovers",RoundsProc.BlockHeal),("rounds-keys:crimson-potion",RoundsProc.BlockHeal),("rounds-cpc:healing-block",RoundsProc.BlockHeal),
            ("rounds-rsc:regenerator",RoundsProc.Regen),("rounds-keys:giants-gauntlet",RoundsProc.Regen),("rounds-keys:iron-heart",RoundsProc.Regen),("rounds-cpc:nano-machines",RoundsProc.Regen),("rounds-cpc:red-velvet-cake",RoundsProc.Regen),
            ("rounds-keys:mending-reload",RoundsProc.ReloadHeal),
        };
        // The vanilla chip a proc look's mechanic belongs to: holding it keeps the vanilla look (the aliases exclude each other).
        private static string VanillaOf(RoundsProc p)=>p==RoundsProc.Packet?"pickpocket":p==RoundsProc.Stasis?"freezer-block":p==RoundsProc.Grapple||p==RoundsProc.Tractor?"grappling-shot":p==RoundsProc.Reclaim?"second-chance":p==RoundsProc.Transit?"wormhole":p==RoundsProc.Vent?"panic-button":null;
        /// <summary>The pack proc looks of a pilot holding `chips` (no allocation).</summary>
        public static RoundsProc Procs(IReadOnlyDictionary<string,int> chips)
        {
            if(chips==null||chips.Count==0)return RoundsProc.None;var o=RoundsProc.None;
            for(int i=0;i<procs.Length;i++){var p=procs[i];if(Count(chips,p.chip)>0&&Count(chips,VanillaOf(p.proc))==0)o|=p.proc;}
            return o;
        }
        /// <summary>The stasis look (0 Disruption Pulse, 1 Lunar Interdict, 2 Shadow Interdict), as their field looks 6..8.</summary>
        public static int StasisLook(IReadOnlyDictionary<string,int> chips)=>Count(chips,"rounds-arcana:the-moon")>0?1:Count(chips,"rounds-wwc:banishment")>0?2:0;
        /// <summary>Every chip this catalogue gives a bespoke effect look (fields, satellites, the drone, procs and buffs).</summary>
        public static IEnumerable<string> Bespoke()
        {
            foreach(var l in Looks)yield return l.Chip;foreach(var s in SatelliteChips)yield return s;yield return DroneChip;
            foreach(var p in procs)yield return p.chip;
        }

        // ---- card icons ----
        // Each pack chip's card icon, from the pinned line icons (Resources/Icons.json "icons"): its main effect, distinct within
        // its pack, never the cpu fallback or the content's placeholder sparkles. The content's own icon field, hash and revision
        // are untouched (presentation only).
        private static readonly Dictionary<string,string> icons=Icons();
        private static Dictionary<string,string> Icons()
        {
            var d=new Dictionary<string,string>();void Pack(string prefix,string pairs){var p=pairs.Split(' ');for(int i=0;i+1<p.Length;i+=2)d[prefix+p[i]]=p[i+1];}
            Pack(Cards,"hare move-right turtle shield terminator split slow-poke anchor phantom ghost low-gravity moon quick-reflexes shield-check snake-attack bot excalibur orbit hot-potato flame smoke-grenade hourglass adware download short-circuit zap hamper snowflake burnout skull contagion biohazard reboot-optics scan ram-upgrade layers overclock activity cyber-psychosis brain");
            Pack("rounds-vecs:","anti-gravity-wells expand gravity-wells shrink bigger-magazine boxes blood-magic shield-alert capture shuffle consume heart-plus extra-magazine rotate-cw inner-peace crosshair mag-of-holding package-check panic circle-alert teleporter circle-dashed tennis volleyball");
            Pack("rounds-rsc:","bounce-absorption chevrons-down-up changeup gauge harming-field radio hitscan scan-line mortar target open-chamber rows-3 rebound corner-up-right reckless-attack swords reflection-accelerant spline regenerator heart-pulse repentance heart slug hammer spare-shotgun split split copy twin-scythe orbit");
            Pack("rounds-arcana:","the-chariot radio death undo-2 the-devil flame the-empress sprout the-hanged-man hourglass the-hermit eclipse the-hierophant network justice shield-alert the-lovers heart the-magician wand-sparkles the-moon moon the-sun sun the-tower cloud-lightning");
            Pack("rounds-bsc:","double-jump move-up elite-sneakers footprints bolt-action crosshair boing volleyball compacted-shot boxes drum-magazine circle-dot foldable-stock layers futuristic-stock gem get-over-here anchor attract magnet repel wind higher-caliber target le-monk person-standing long-fall-boots feather morning-coffee flask-conical omega diamond overclocked-flywheels rotate-cw rolling-thunder cloud-lightning sawblade-bullets settings sluggish-rounds spline spread-the-love heart sneeze droplets thorns shield-alert whale waves wooden-stock axe yeetus rocket yin eclipse yin-yang dna");
            Pack("rounds-keys:","angelic feather bottomless-pockets package-check boxing-glove hand-fist crimson-potion flask-conical giants-gauntlet hand grovetenders-shield sprout iron-carapace shield iron-heart heart-pulse iron-pincers swords mending-reload heart-plus mirror copy moonwalker moon new-sneakers footprints nimble move raid-boss skull the-eggs-blessing gem zephyrs-blessing wind");
            Pack("rounds-pce:","ant shrink moon-shoes moon jetpack rocket old-jetpack wind super-jump move-up straight-shot move-right tractor-beam magnet fragmentation bomb mulligan undo-2 super-ball volleyball");
            Pack("rounds-wwc:","ammo-cache boxes boomerang undo-2 bounce volleyball boots-of-leaping move-up running-shoes footprints aggressive-venting flame gatling-gun rows-3 shotgun split vampirism droplets wild-aim dices strong-body hand-fist dimensional-shuffle shuffle banishment eclipse");
            Pack("rounds-cpc:","healing-block shield-check ammo-chest piggy-bank balloon-bullets expand duplicator copy double-duplicator layers plant-like-bullets sprout infectious-bullets biohazard nano-machines dna red-velvet-cake heart sentry-gun orbit");
            return d;
        }
        /// <summary>The card icon of pack chip `id`, or null for any other chip.</summary>
        public static string Icon(string id)=>id!=null&&icons.TryGetValue(id,out var i)?i:null;
        /// <summary>Every pack chip with an icon here (checks).</summary>
        public static IEnumerable<KeyValuePair<string,string>> AllIcons=>icons;
    }
}
