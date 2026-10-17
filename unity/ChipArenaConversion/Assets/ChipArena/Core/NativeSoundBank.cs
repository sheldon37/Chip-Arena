using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ChipArena.UnityConversion
{
    public enum NativeSoundSpace{Positional,Flat,Loop}
    public enum NativeSoundPriority{High,Normal,Low}

    /// <summary>
    /// The effects sound bank's key contract (owner-licensed pack audio, docs/ASSET-CREDITS.md). Each key is one logical cue
    /// with its space, hard length cap (seconds) and default voice priority, from the 2026-10-11 audio study's mapping. The
    /// clips and their manifest are never in this repository: they live in a private assets repository that the owner's
    /// builder copies to Assets/LocalAssets/Resources/EffectsAudio (tools/install_local_assets.py), and Unity merges every
    /// Resources folder, so they load as EffectsAudio/&lt;key&gt;/&lt;clip&gt;. Without that folder the bank is empty and every cue
    /// plays today's source clip and routing (NativeSoundPlan, NativeAudio.Cue). The intro.&lt;style&gt; keys are the match
    /// entrances' music (owner 2026-10-12, every non-DEV entrance, and the owner's DEV Sheldon Intro, cut to its beat marks in
    /// NativeSheldonIntro): 2D, 4.8 s (the entrance's length), played from its first listed clip on the intro voice by
    /// NativeAudio.Entrance (later clips are alternates: reorder the list to switch).
    /// </summary>
    public static class NativeSoundKeys
    {
        public struct Def{public string Key;public NativeSoundSpace Space;public float Cap;public NativeSoundPriority Priority;}
        public const string Folder="EffectsAudio/",Manifest="EffectsAudio/manifest",LocalRoot="Assets/LocalAssets/Resources/EffectsAudio";
        public const int Schema=1,MaxClips=16;
        /// <summary>Repository/build budget for the whole EffectsAudio folder (clips, metas and manifest), in bytes: 12 MB until
        /// the hazard kit's 46 clips (0.72 MB, docs/HAZARD-KIT.md), 13 MB until the zombies' voices and footfalls (12 keys and
        /// their requested variants, docs/audio/SOUND-REQUESTS.md), 16 MB since.</summary>
        public const long BudgetBytes=16000000;
        private const NativeSoundSpace P=NativeSoundSpace.Positional,F=NativeSoundSpace.Flat,O=NativeSoundSpace.Loop;
        private const NativeSoundPriority H=NativeSoundPriority.High,N=NativeSoundPriority.Normal,L=NativeSoundPriority.Low;
        private static Def K(string key,NativeSoundSpace space,float cap,NativeSoundPriority priority)=>new Def{Key=key,Space=space,Cap=cap,Priority=priority};
        public static readonly Def[] All={
            K("blast.debris",P,2f,L),K("blast.fire",P,2.5f,N),K("blast.fireworks",P,2f,N),K("blast.frost",P,2f,N),K("blast.implosion",P,2.5f,N),K("blast.kinetic",P,2f,N),K("blast.leet-nuke",P,5f,N),K("blast.meteor",P,3f,N),
            K("blast.mine",P,1.5f,N),K("blast.nova",P,2f,N),K("blast.quake",P,2f,N),K("blast.shock",P,2f,N),K("blast.static",P,.8f,N),K("blast.toxic",P,2f,N),K("blast.transient",P,.09f,L),K("blast.void",P,2f,N),
            K("break.glass",P,1.6f,N),K("break.metal",P,1.6f,N),K("break.stone",P,1.6f,N),K("break.wood",P,1.6f,N),
            K("chip.bomb.attach",P,.5f,N),K("chip.bomb.fuse-beep",P,.12f,N),K("chip.bomb.fuse-hiss",O,2f,L),K("chip.critical",F,.6f,H),K("chip.delayed.charge",P,2.3f,N),K("chip.heal",F,1f,N),K("chip.mine.arm",P,.3f,N),K("chip.pong",P,.4f,N),
            K("chip.split",P,.4f,N),K("chip.summon",P,2f,N),K("chip.teleport",P,1.2f,N),K("chip.vacuum",P,1.2f,N),
            K("death.broadcast",F,2.5f,N),K("death.pilot",P,1.8f,N),K("death.style.confetti",P,2f,N),K("death.style.gib",P,1.5f,N),K("death.style.launch",P,1.5f,N),K("death.style.matrix",P,2f,N),K("death.style.soul",P,2.5f,N),
            K("dev.fist.hit",P,.6f,N),K("dev.fist.swing",P,.5f,N),K("dev.leet.glitch",P,1.2f,N),
            K("field.acid",O,6f,L),K("field.black-hole",O,6f,L),K("field.fire",O,6f,L),K("field.frost",O,6f,L),K("field.heal",O,5f,L),K("field.repulsion",O,4f,L),K("field.storm",O,5f,L),K("field.time",P,2.5f,L),
            K("gun.ads",F,.4f,H),K("gun.dryfire",F,.3f,H),K("gun.flyby",P,.6f,L),K("gun.frag.throw",P,.6f,N),K("gun.machinegun.shot",P,.32f,N),K("gun.orbital.mark",P,2.5f,N),K("gun.pistol.shot",P,.6f,N),K("gun.reload.end",F,.8f,H),
            K("gun.reload.start",F,.8f,H),K("gun.ricochet",P,.9f,N),K("gun.rifle.shot",P,1.4f,N),K("gun.rocket.dud",P,.8f,N),K("gun.rocket.launch",P,1.2f,N),
            K("hazard.entry",F,1.5f,N),K("hazard.lava",P,4f,N),K("hazard.saw",P,1.2f,N),K("hazard.spikes",P,1f,N),
            K("hit.body",P,.35f,L),K("hit.confirm",F,.2f,H),K("hit.dodge",P,.5f,L),K("hit.headshot",F,.5f,H),K("hit.hurt",F,.45f,H),K("hit.kill-confirm",F,1f,H),
            K("impact.ceiling",P,.7f,L),K("impact.debris",P,.6f,L),K("impact.energy",P,.5f,L),K("impact.glass",P,.6f,L),K("impact.ground",P,.4f,L),K("impact.metal",P,.5f,L),K("impact.stone",P,.45f,L),K("impact.water",P,.6f,L),
            K("impact.wood",P,.45f,L),
            K("magic.ball-lightning",O,6f,L),K("magic.blink-strike",P,1.2f,N),K("magic.darkness.hit",P,.9f,N),K("magic.darkness.shot",P,1f,N),K("magic.fire.hit",P,.8f,N),K("magic.fire.shot",P,.9f,N),K("magic.flame-lance",P,1.5f,N),K("magic.geyser",P,2.5f,N),
            K("magic.lightning.hit",P,.6f,N),K("magic.lightning.shot",P,.55f,N),K("magic.meteor.incoming",P,2.5f,N),K("magic.shackles",P,1.5f,N),K("magic.singularity",O,6f,L),K("magic.tidal-wave",P,2f,N),K("magic.water.hit",P,.8f,N),K("magic.water.shot",P,.8f,N),
            K("melee.block",P,.9f,N),K("melee.daggers.hit",P,.5f,N),K("melee.daggers.swing",P,.4f,N),K("melee.daggers.throw",P,.8f,N),K("melee.greatsword.hit",P,.9f,N),K("melee.greatsword.spin",P,1.5f,N),K("melee.greatsword.swing",P,1f,N),K("melee.miss",P,.6f,N),
            K("melee.shield.bash",P,.7f,N),K("melee.shield.block",P,.9f,N),K("melee.shield.throw",P,1f,N),K("melee.sword.hit",P,.7f,N),K("melee.sword.swing",P,.6f,N),
            K("move.jump",P,.4f,N),K("move.land",P,.4f,N),K("move.roll",P,.6f,N),K("move.step.concrete",P,.35f,L),K("move.step.gravel",P,.35f,L),K("move.step.metal",P,.35f,L),K("move.step.snow",P,.4f,L),K("move.step.stone",P,.35f,L),
            K("move.step.wood",P,.35f,L),
            K("pve.add.death",P,1.2f,N),K("pve.zombie.moan",P,2.5f,L),K("pve.zombie.shriek",P,1.6f,N),
            // The zombies' voices and footfalls (docs/audio/SOUND-REQUESTS.md; Core NativeZombieVoice, NativeFootfalls): each cap is at
            // least the request's targetSeconds.max. Each has a built-in synthesized fallback (NativeCreatureSynth).
            K("pve.zombie.groan",P,1.2f,L),K("pve.zombie.attack",P,.8f,N),K("pve.zombie.hurt",P,.7f,L),K("pve.zombie.death",P,1.7f,N),K("pve.zombie.runner.snarl",P,1.1f,L),
            K("pve.zombie.spitter.gurgle",P,1.1f,N),K("pve.zombie.spitter.spit",P,.7f,N),K("pve.zombie.brute.grunt",P,1.1f,N),
            K("pve.zombie.step.shuffle",P,.5f,L),K("pve.zombie.step.run",P,.35f,L),K("pve.zombie.step.heavy",P,.7f,L),K("pve.zombie.step.crawl",P,.7f,L),
            // The Central Station's enemies (docs/EXPEDITION-CENTRAL-STATION.md section 10; Core NativeStationKinds.CueKeys, routed by
            // Runtime/NativeStationKindsPresentation.cs): positional, each cap the brief's length with a little margin.
            K("pve.station.linesman.idle",P,2f,L),K("pve.station.linesman.charge",P,1.2f,N),K("pve.station.linesman.arc",P,.7f,N),K("pve.station.linesman.ground",P,1f,N),K("pve.station.linesman.hurt",P,.5f,L),K("pve.station.linesman.death",P,1.7f,N),
            K("pve.station.conductor.inhale",P,.9f,N),K("pve.station.conductor.whistle",P,1.1f,N),K("pve.station.conductor.bell",P,1.3f,N),K("pve.station.conductor.mutter",P,2.1f,L),K("pve.station.conductor.lantern",P,.7f,N),K("pve.station.conductor.death",P,1.5f,N),
            K("pve.station.undertow.bubble",P,2.1f,L),K("pve.station.undertow.surface",P,.9f,N),K("pve.station.undertow.grab",P,.7f,N),K("pve.station.undertow.drag",P,1.6f,N),K("pve.station.undertow.death",P,1.5f,N),
            K("pve.station.pileup.moan",P,2.6f,L),K("pve.station.pileup.brace",P,.8f,N),K("pve.station.pileup.shove",P,.7f,N),K("pve.station.pileup.ricochet",P,.35f,L),K("pve.station.pileup.split",P,1.6f,N),
            K("pve.buy",F,1.5f,N),K("pve.clear",F,2.5f,N),K("pve.gib",P,.7f,N),K("pve.money",F,.6f,L),K("pve.pickup",F,1f,N),K("pve.revive",F,2f,N),K("pve.wave",F,3f,N),
            K("siren.claw",P,.8f,N),K("siren.damnation",P,1.5f,N),K("siren.dissipate",P,4f,N),K("siren.eruption",P,2.5f,N),K("siren.hellfire",P,.9f,N),K("siren.laugh",P,3f,N),K("siren.mark",P,2.5f,N),K("siren.reveal",F,4f,N),
            K("siren.rush",P,1.2f,N),K("siren.slam",P,2.5f,N),K("siren.vents",P,2f,N),
            K("status.burn-tick",P,.35f,L),K("status.freeze",P,1.5f,N),K("status.frost-shatter",P,1.5f,N),K("status.poison-tick",P,.35f,L),K("status.shock-zap",P,.4f,N),
            K("structure.boom",P,2f,N),K("structure.crack",P,.8f,N),K("structure.creak",P,1.5f,N),K("structure.debris",P,3.8f,N),K("structure.glass",P,1.8f,N),K("structure.gravel",P,1.5f,N),K("structure.groan",P,2.5f,N),K("structure.land.metal",P,1.5f,N),
            K("structure.land.timber",P,1.5f,N),K("structure.rebar",P,1.5f,N),K("structure.rumble",P,3f,N),K("structure.snap",P,.6f,N),K("structure.thud",P,1.2f,N),K("structure.whoosh",P,2f,N),
            K("ui.back",F,.4f,H),K("ui.click",F,.25f,H),K("ui.confirm",F,1f,H),K("ui.countdown",F,.5f,H),K("ui.draft.hover",F,.3f,H),K("ui.draft.pick",F,.8f,H),K("ui.draft.plant",F,1f,H),K("ui.error",F,.7f,H),
            K("ui.go",F,1.5f,H),K("ui.hover",F,.15f,H),K("ui.loadout.select",F,1f,H),K("ui.notice",F,1f,H),K("ui.round-lost",F,3f,H),K("ui.round-won",F,3f,H),K("ui.toggle",F,.4f,H),
            // Entrance music (NativeIntroRules.Entrances without the DEV ones): the 12 weapon signatures, then the 7 procedural entrances.
            K("intro.weapon-sword",F,4.8f,H),K("intro.weapon-daggers",F,4.8f,H),K("intro.weapon-shield",F,4.8f,H),K("intro.weapon-greatsword",F,4.8f,H),K("intro.weapon-pistol",F,4.8f,H),K("intro.weapon-rifle",F,4.8f,H),
            K("intro.weapon-machinegun",F,4.8f,H),K("intro.weapon-bow",F,4.8f,H),K("intro.weapon-fire",F,4.8f,H),K("intro.weapon-water",F,4.8f,H),K("intro.weapon-lightning",F,4.8f,H),K("intro.weapon-darkness",F,4.8f,H),
            K("intro.orbital-drop",F,4.8f,H),K("intro.grid-rez",F,4.8f,H),K("intro.holo-projection",F,4.8f,H),K("intro.blackout",F,4.8f,H),K("intro.skyline-zip",F,4.8f,H),K("intro.hero-landing",F,4.8f,H),
            K("intro.overclock",F,4.8f,H),K("intro.sheldon-intro",F,4.8f,H),
            // The hazard kit (docs/HAZARD-KIT.md, Core NativeHazardKitSound): alarm loops per look, the klaxon blast, the trip sweep and
            // the beacon motor; the sentry turret; the kit saws. Each has a built-in synthesized fallback (NativeKitSynth).
            K("alarm.biohazard",O,3f,N),K("alarm.lockdown",O,3.2f,N),K("alarm.evacuation",O,4.5f,N),K("alarm.klaxon",P,1.2f,N),K("alarm.trip",P,1.5f,N),K("alarm.beacon",O,3.2f,L),
            K("turret.servo",P,.9f,L),K("turret.scan",O,2.4f,L),K("turret.lock",P,.35f,H),K("turret.spinup",P,1.2f,N),K("turret.fire",P,.35f,N),K("turret.overheat",P,2.5f,N),
            K("turret.break",P,1.8f,N),K("turret.spark",P,.6f,L),
            K("saw.whine",O,2.4f,N),K("saw.spinup",P,1.6f,N),K("saw.spindown",P,1.8f,N),K("saw.scrape",P,1f,L),K("saw.hit",P,.8f,N),
        };
        private static Dictionary<string,int> index;
        public static bool Find(string key,out Def def)
        {
            if(index==null){var map=new Dictionary<string,int>(StringComparer.Ordinal);for(int i=0;i<All.Length;i++)map[All[i].Key]=i;index=map;}
            if(key!=null&&index.TryGetValue(key,out int at)){def=All[at];return true;}def=default;return false;
        }
        public static bool Known(string key)=>Find(key,out _);
        public const string IntroPrefix="intro.";
        /// <summary>Whether <paramref name="key"/> is an entrance-music key (intro.&lt;style&gt;).</summary>
        public static bool IsIntro(string key)=>key!=null&&key.StartsWith(IntroPrefix,StringComparison.Ordinal);
        /// <summary>The entrance-music key of <paramref name="style"/> (intro.&lt;style&gt;), or null when the style has none (the dev-
        /// entrances, retired or unknown styles): those always play their built-in music.</summary>
        public static string Intro(string style){if(string.IsNullOrEmpty(style))return null;string key=IntroPrefix+style;return Known(key)?key:null;}
    }

    /// <summary>One manifest entry: the clip names under EffectsAudio/&lt;key&gt;/, a gain trim on the routed gain, a base rate and
    /// a +- pitch jitter, the voice priority, the space, the hard length cap and a per-key dedup window (seconds).</summary>
    public sealed class NativeSoundEntry
    {
        public string Key;public string[] Clips=Array.Empty<string>();public float Gain=1,Rate=1,Jitter=.03f,MaxLength,Cooldown;
        public NativeSoundPriority Priority;public NativeSoundSpace Space;
        public bool Positional=>Space!=NativeSoundSpace.Flat;public bool Loop=>Space==NativeSoundSpace.Loop;public int Count=>Clips.Length;
        public string Path(int variation)=>NativeSoundKeys.Folder+Key+"/"+Clips[Math.Max(0,Math.Min(Clips.Length-1,variation))];
    }

    /// <summary>
    /// The data-driven effects bank (presentation only): the parsed manifest, a seeded non-repeating variation bag per key and
    /// the per-key dedup clock. An absent, empty or invalid manifest gives an empty bank, which <see cref="Has"/> nothing, so
    /// every caller keeps today's clip. Invalid entries are dropped one by one (<see cref="Errors"/>), never the whole bank.
    /// </summary>
    public sealed class NativeSoundBank
    {
        private sealed class Bag{public int[] Order=Array.Empty<int>();public int At,Last=-1;public uint State;}
        private readonly Dictionary<string,NativeSoundEntry> entries=new Dictionary<string,NativeSoundEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string,Bag> bags=new Dictionary<string,Bag>(StringComparer.Ordinal);
        private readonly Dictionary<string,double> played=new Dictionary<string,double>(StringComparer.Ordinal);
        private readonly uint seed;
        public readonly List<string> Errors=new List<string>();
        public NativeSoundBank(uint seed=0x5eed1234u){this.seed=seed;}
        public int Count=>entries.Count;
        public IEnumerable<NativeSoundEntry> Entries=>entries.Values;
        /// <summary>Whether <paramref name="key"/> has at least one clip; false for null, unknown or empty keys.</summary>
        public bool Has(string key)=>key!=null&&entries.TryGetValue(key,out var e)&&e.Count>0;
        public NativeSoundEntry Entry(string key)=>key!=null&&entries.TryGetValue(key,out var e)?e:null;
        /// <summary>The resource path of entrance <paramref name="style"/>'s music, its intro key's first clip (the others are
        /// alternates), with the entry's gain trim; null (gain 1) when the style has no intro key (DEV, retired, unknown) or no
        /// clip is installed, so the caller plays today's sting exactly as before.</summary>
        public string IntroClip(string style,out float gain){var e=Entry(NativeSoundKeys.Intro(style));if(e==null||e.Count==0){gain=1;return null;}gain=e.Gain;return e.Path(0);}
        /// <summary>The next variation of <paramref name="key"/>: a seeded shuffle bag, so every clip plays once per round of the
        /// bag and the same clip never plays twice in a row (with two or more clips).</summary>
        public int Next(string key)
        {
            var e=Entry(key);int n=e?.Count??0;if(n<=1)return 0;
            if(!bags.TryGetValue(key,out var bag)){bag=new Bag{State=Mix(seed^Hash(key))};bags[key]=bag;}
            if(bag.Order.Length!=n){bag.Order=new int[n];bag.At=n;}
            if(bag.At>=n)
            {
                for(int i=0;i<n;i++)bag.Order[i]=i;
                for(int i=n-1;i>0;i--){int j=(int)(Step(ref bag.State)%(uint)(i+1));int t=bag.Order[i];bag.Order[i]=bag.Order[j];bag.Order[j]=t;}
                if(bag.Order[0]==bag.Last){int j=1+(int)(Step(ref bag.State)%(uint)(n-1));int t=bag.Order[0];bag.Order[0]=bag.Order[j];bag.Order[j]=t;}
                bag.At=0;
            }
            int v=bag.Order[bag.At++];bag.Last=v;return v;
        }
        /// <summary>The per-key dedup window: false while <paramref name="key"/> played less than its cooldown ago; else stamps it.
        /// A clock that went backwards (a new round, a rewind) admits.</summary>
        public bool Admit(string key,double now)
        {
            var e=Entry(key);if(e==null||e.Cooldown<=0)return e!=null;
            if(played.TryGetValue(key,out double at)&&now>=at&&now-at<e.Cooldown)return false;
            played[key]=now;return true;
        }
        public void ResetClock()=>played.Clear();
        /// <summary>A play's rate: the entry's base rate times a uniform +- jitter, times the caller's scale (fuse beeps rise).</summary>
        public static float Rate(NativeSoundEntry e,float random01,float scale=1)=>Math.Max(.25f,Math.Min(4,e.Rate*(1+(random01*2-1)*e.Jitter)*(scale>0?scale:1)));
        private static uint Hash(string s){unchecked{uint h=2166136261;foreach(char c in s)h=(h^c)*16777619;return h;}}
        private static uint Mix(uint x){unchecked{x^=x>>16;x*=0x7feb352d;x^=x>>15;x*=0x846ca68b;x^=x>>16;return x==0?1u:x;}}
        private static uint Step(ref uint s){s^=s<<13;s^=s>>17;s^=s<<5;return s;}

        /// <summary>Parses manifest text (null or empty: an empty bank). Schema errors are listed in <see cref="Errors"/>.</summary>
        public static NativeSoundBank Parse(string json,uint seed=0x5eed1234u)
        {
            var bank=new NativeSoundBank(seed);if(string.IsNullOrWhiteSpace(json))return bank;
            object root;try{root=NativeSoundJson.Parse(json);}catch(FormatException x){bank.Errors.Add("manifest: "+x.Message);return bank;}
            if(!(root is Dictionary<string,object> top)){bank.Errors.Add("manifest: not an object");return bank;}
            foreach(var field in top.Keys)if(field!="schema"&&field!="events"&&field!="generated"&&field!="note")bank.Errors.Add("manifest: unknown field "+field);
            if(!(top.TryGetValue("schema",out var schema)&&schema is double s&&s==NativeSoundKeys.Schema)){bank.Errors.Add("manifest: schema must be "+NativeSoundKeys.Schema);return bank;}
            if(!(top.TryGetValue("events",out var events)&&events is Dictionary<string,object> list)){bank.Errors.Add("manifest: events must be an object");return bank;}
            foreach(var pair in list)
            {
                var e=Entry(pair.Key,pair.Value,bank.Errors);if(e!=null)bank.entries[pair.Key]=e;
            }
            return bank;
        }
        private static NativeSoundEntry Entry(string key,object value,List<string> errors)
        {
            int before=errors.Count;void Fail(string what)=>errors.Add(key+": "+what);
            if(!NativeSoundKeys.Find(key,out var def)){Fail("unknown key");return null;}
            if(!(value is Dictionary<string,object> o)){Fail("entry must be an object");return null;}
            var e=new NativeSoundEntry{Key=key,Space=def.Space,Priority=def.Priority,MaxLength=def.Cap};
            foreach(var field in o.Keys)if(Array.IndexOf(Fields,field)<0)Fail("unknown field "+field);
            if(o.TryGetValue("clips",out var clips))
            {
                if(!(clips is List<object> names)){Fail("clips must be an array");}
                else
                {
                    var seen=new HashSet<string>(StringComparer.Ordinal);var kept=new List<string>();
                    foreach(var n in names){if(!(n is string name)||!ClipName(name)){Fail("clip names are 1-40 of [a-z0-9_-]");continue;}if(!seen.Add(name)){Fail("duplicate clip "+name);continue;}kept.Add(name);}
                    if(kept.Count>NativeSoundKeys.MaxClips)Fail("more than "+NativeSoundKeys.MaxClips+" clips");e.Clips=kept.ToArray();
                }
            }
            float Number(string field,float fallback,float min,float max)
            {
                if(!o.TryGetValue(field,out var v))return fallback;
                if(!(v is double d)||double.IsNaN(d)||d<min||d>max){Fail(field+" must be a number in ["+min.ToString(CultureInfo.InvariantCulture)+", "+max.ToString(CultureInfo.InvariantCulture)+"]");return fallback;}
                return (float)d;
            }
            e.Gain=Number("gain",1,0,4);e.Rate=Number("rate",1,.25f,4);e.Jitter=Number("jitter",.03f,0,.25f);e.Cooldown=Number("cooldown",0,0,10);
            e.MaxLength=Number("maxLength",def.Cap,.01f,def.Cap+.001f);
            if(o.TryGetValue("priority",out var p)){if(p is string ps&&Priority(ps,out var pr))e.Priority=pr;else Fail("priority must be high, normal or low");}
            if(o.TryGetValue("space",out var sp)&&!(sp is string ss&&ss==SpaceName(def.Space)))Fail("space must be "+SpaceName(def.Space));
            if(o.TryGetValue("note",out var note)&&!(note is string))Fail("note must be a string");
            return errors.Count==before?e:null;
        }
        private static readonly string[] Fields={"clips","gain","rate","jitter","priority","space","maxLength","cooldown","note"};
        public static bool ClipName(string s){if(string.IsNullOrEmpty(s)||s.Length>40)return false;foreach(char c in s)if(!(c>='a'&&c<='z'||c>='0'&&c<='9'||c=='_'||c=='-'))return false;return true;}
        public static string SpaceName(NativeSoundSpace s)=>s==NativeSoundSpace.Flat?"2d":s==NativeSoundSpace.Loop?"loop":"3d";
        public static bool Priority(string s,out NativeSoundPriority p){p=s=="high"?NativeSoundPriority.High:s=="low"?NativeSoundPriority.Low:NativeSoundPriority.Normal;return s=="high"||s=="normal"||s=="low";}
    }

    /// <summary>A small strict JSON reader for the manifest (objects, arrays, strings, numbers, true/false/null), shared by
    /// Unity and the managed checks so both read the same data. Depth and size are bounded: the file is outside data.</summary>
    public static class NativeSoundJson
    {
        public const int MaxDepth=16,MaxLength=1<<20;
        public static object Parse(string s)
        {
            if(s.Length>MaxLength)throw new FormatException("too large");
            int i=0;var v=Value(s,ref i,0);Skip(s,ref i);if(i!=s.Length)throw new FormatException("trailing text at "+i);return v;
        }
        private static void Skip(string s,ref int i){while(i<s.Length&&(s[i]==' '||s[i]=='\t'||s[i]=='\n'||s[i]=='\r'))i++;}
        private static object Value(string s,ref int i,int depth)
        {
            if(depth>MaxDepth)throw new FormatException("too deep");Skip(s,ref i);if(i>=s.Length)throw new FormatException("unexpected end");
            char c=s[i];
            if(c=='{')
            {
                var o=new Dictionary<string,object>(StringComparer.Ordinal);i++;Skip(s,ref i);if(i<s.Length&&s[i]=='}'){i++;return o;}
                while(true)
                {
                    Skip(s,ref i);if(i>=s.Length||s[i]!='"')throw new FormatException("expected a key at "+i);string key=Text(s,ref i);
                    Skip(s,ref i);if(i>=s.Length||s[i]!=':')throw new FormatException("expected ':' at "+i);i++;
                    if(o.ContainsKey(key))throw new FormatException("duplicate key "+key);o[key]=Value(s,ref i,depth+1);Skip(s,ref i);
                    if(i<s.Length&&s[i]==','){i++;continue;}if(i<s.Length&&s[i]=='}'){i++;return o;}throw new FormatException("expected ',' or '}' at "+i);
                }
            }
            if(c=='[')
            {
                var a=new List<object>();i++;Skip(s,ref i);if(i<s.Length&&s[i]==']'){i++;return a;}
                while(true)
                {
                    a.Add(Value(s,ref i,depth+1));Skip(s,ref i);
                    if(i<s.Length&&s[i]==','){i++;continue;}if(i<s.Length&&s[i]==']'){i++;return a;}throw new FormatException("expected ',' or ']' at "+i);
                }
            }
            if(c=='"')return Text(s,ref i);
            if(Word(s,ref i,"true"))return true;if(Word(s,ref i,"false"))return false;if(Word(s,ref i,"null"))return null;
            int start=i;if(i<s.Length&&s[i]=='-')i++;while(i<s.Length&&(char.IsDigit(s[i])||s[i]=='.'||s[i]=='e'||s[i]=='E'||s[i]=='+'||s[i]=='-'))i++;
            if(i==start||!double.TryParse(s.Substring(start,i-start),NumberStyles.Float,CultureInfo.InvariantCulture,out double d)||double.IsInfinity(d))throw new FormatException("bad value at "+start);
            return d;
        }
        private static bool Word(string s,ref int i,string w){if(string.CompareOrdinal(s,i,w,0,w.Length)!=0)return false;i+=w.Length;return true;}
        private static string Text(string s,ref int i)
        {
            var b=new StringBuilder();i++;
            while(i<s.Length)
            {
                char c=s[i++];if(c=='"')return b.ToString();if(c<' ')throw new FormatException("control character in a string");
                if(c!='\\'){b.Append(c);continue;}if(i>=s.Length)break;char x=s[i++];
                switch(x)
                {
                    case '"':b.Append('"');break;case '\\':b.Append('\\');break;case '/':b.Append('/');break;case 'b':b.Append('\b');break;
                    case 'f':b.Append('\f');break;case 'n':b.Append('\n');break;case 'r':b.Append('\r');break;case 't':b.Append('\t');break;
                    case 'u':if(i+4>s.Length||!int.TryParse(s.Substring(i,4),NumberStyles.HexNumber,CultureInfo.InvariantCulture,out int u))throw new FormatException("bad \\u escape");b.Append((char)u);i+=4;break;
                    default:throw new FormatException("bad escape");
                }
            }
            throw new FormatException("unterminated string");
        }
    }

    /// <summary>
    /// Voice choice for bank sounds on NativeAudio's existing 24 voices (no new AudioSource): gated cues still hold at most 8
    /// voices; a low-priority sound (distant impacts, ticks, steps, field loops) never takes one of the last
    /// <see cref="Reserve"/> free voices, which stay for the local pilot's own shot, hurt, hit confirm and UI; a high-priority
    /// sound with no free voice takes over a playing low-priority bank voice instead of being dropped. At most
    /// <see cref="MaxLoops"/> field loops sound at once. held[i]: the priority a busy voice holds (Normal for every non-bank play).
    /// </summary>
    public static class NativeVoicePool
    {
        public const int Voices=24,KeyedCap=8,Reserve=4,MaxLoops=3;
        public static int Pick(bool[] busy,bool[] keyed,NativeSoundPriority[] held,bool gated,NativeSoundPriority priority,out bool steal)
        {
            steal=false;int free=-1,freeCount=0,keyedCount=0;
            for(int i=0;i<busy.Length;i++){if(!busy[i]){freeCount++;if(free<0)free=i;}else if(keyed[i])keyedCount++;}
            if(gated&&keyedCount>=KeyedCap)return -1;
            if(free>=0)return priority!=NativeSoundPriority.Low||freeCount>Reserve?free:-1;
            if(priority!=NativeSoundPriority.High)return -1;
            for(int i=0;i<busy.Length;i++)if(held[i]==NativeSoundPriority.Low){steal=true;return i;}
            return -1;
        }
    }
}
