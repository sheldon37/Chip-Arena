using System;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Central Station's four enemies as the presentation draws them (owner 2026-10-16, docs/EXPEDITION-CENTRAL-STATION.md
    /// sections 7, 8 and 14: "Keep all four and their names"): the Linesman, the Conductor, the Undertow and the Pileup.
    /// Presentation only, like Core/NativeHordeKinds: nothing here is read by the authority. Astra owns their rules, stats, AI,
    /// spawning and network fields (the PveContent rows and NativePveHorde); this holds what the presentation needs:
    /// <list type="bullet">
    /// <item>Their registration as zone kinds after the City Ironworks' (<see cref="Kinds"/>, Core/NativeHordeZones: ids, names, looks, the proposed capsules, weight, seam
    /// hues, takes and the proposed content rows of section 8, which the DEV gallery times its attacks with).</item>
    /// <item>The telegraph and special timings of sections 7 and 8 (<see cref="ArcWindup"/> and the rest), and how the replicated
    /// <c>LastAttack</c> and the add's <see cref="StationView"/> map onto them (<see cref="SpecialOf"/>,
    /// <see cref="SpecialStrike"/>).</item>
    /// <item>The audio keys of section 10 (<see cref="CueKeys"/>), routed by Runtime/NativeStationKindsPresentation.cs; each falls
    /// back to an existing bank key where one fits (<see cref="StandIn"/>), else stays silent until the owner's clips exist.</item>
    /// </list>
    /// Until Astra adds the content rows nothing spawns them, so today's content, maps and checks draw exactly as before
    /// (Tests/StationEnemyChecks holds the Siren's seven to a baseline taken before these kinds existed).
    /// </summary>
    public static class NativeStationKinds
    {
        /// <summary>The design's map id and the live one: exp-layout-5 runs the Central Station as area `exp-transit` on map
        /// `expedition-transit` (PR #255); the draft fixture has the same id.</summary>
        public const string Map="expedition-transit",LiveMap="expedition-transit";
        // ---- the four, in the zone's order (local index); their horde kind indices follow the City Ironworks' five
        // (Core/NativeHordeZones registration order: <see cref="First"/>) ----
        public const int Linesman=0,Conductor=1,Undertow=2,Pileup=3,KindCount=4;
        public const string LinesmanId="linesman",ConductorId="conductor",UndertowId="undertow",PileupId="pileup";
        /// <summary>Identity hues (sRGB; section 7's table): electric blue, cold white (and the lantern's red), aqua; the Pileup
        /// has no seam hue (its lit carriage window is the read), so its few seams take the window's dying tube light.</summary>
        public const int LinesmanBlue=0x6FD8FF,ConductorWhite=0xE8F0FF,LanternRed=0xFF2A1A,UndertowAqua=0x2EF0C0,WindowTube=0xD8FFF0,HiVis=0xFF6A10;

        /// <summary>The four as zone kinds (section 8's proposal; Astra owns every value). Bodies: the brute rig for the Linesman
        /// and the Pileup, the shambler's for the Conductor, the runner's for the Undertow; each drawn at its capsule's height over
        /// the add's 1.7 m (its scale), so the rig stands at the capsule's half height.</summary>
        public static readonly HordeZoneKind[] Kinds={
            new HordeZoneKind{Id=LinesmanId,Name="Linesman",Zone=Map,Body=NativeHordeKinds.Brute,Looks=2,Radius=.48f,Height=1.75f,Scale=1.75f/NativeHordeKinds.AddHeight,Mass=1.6f,Seam=LinesmanBlue,SeamClass=2,Attack=NativeHordeKinds.Attack.Swipe,
                Tempo=.86f,Walks=new[]{"walk-6"},Runs=new[]{"chase-2"},Idles=new[]{"idle-3"},Wanders=new[]{"walk-4"},
                Proposed=Row(LinesmanId,"swipe",260,14,1.2f,2.2f,2.0f,1.6f,.4f,1.4f,.3f,240,70,.45f,.5f,.3f,0)},
            new HordeZoneKind{Id=ConductorId,Name="Conductor",Zone=Map,Body=NativeHordeKinds.Shambler,Looks=2,Radius=.40f,Height=1.9f,Scale=1.9f/NativeHordeKinds.AddHeight,Mass=.9f,Seam=ConductorWhite,SeamClass=2,Attack=NativeHordeKinds.Attack.Swipe,
                Tempo=.94f,Walks=new[]{"walk-4"},Runs=new[]{"chase-1"},Idles=new[]{"idle-1"},Wanders=new[]{"walk-4"},
                Proposed=Row(ConductorId,"swipe",140,8,1.4f,2.6f,1.8f,1.45f,.34f,1.2f,.35f,90,90,.35f,.4f,.45f,0)},
            new HordeZoneKind{Id=UndertowId,Name="Undertow",Zone=Map,Body=NativeHordeKinds.Runner,Looks=2,Radius=.42f,Height=1.7f,Scale=1,Mass=.85f,Seam=UndertowAqua,SeamClass=2,Attack=NativeHordeKinds.Attack.Lunge,
                Tempo=1,Walks=new[]{"chase-2"},Runs=new[]{"chase-5","sprint-1"},Idles=new[]{"shamble-2"},Wanders=new[]{"walk-4"},
                Proposed=Row(UndertowId,"grab",120,6,1.6f,3.2f,2.6f,2.2f,GrabTelegraph,1.2f,1.1f,0,60,.35f,.45f,.4f,0)},
            new HordeZoneKind{Id=PileupId,Name="Pileup",Zone=Map,Body=NativeHordeKinds.Brute,Looks=1,Radius=.75f,Height=2.1f,Scale=2.1f/NativeHordeKinds.AddHeight,Mass=4,Seam=WindowTube,SeamClass=1,Attack=NativeHordeKinds.Attack.Smash,
                Tempo=.7f,Walks=new[]{"walk-4"},Runs=new[]{"walk-4"},Idles=new[]{"idle-3"},Wanders=new[]{"walk-4"},
                Proposed=Row(PileupId,"crush",700,24,.9f,1.6f,2.2f,1.8f,.55f,2.0f,.15f,600,160,.55f,.6f,.15f,NativeHordeKinds.SmashRadius)}};
        /// <summary>Their heads (width scale and drop on the head joint, by local index; the shared tables keep the Siren's seven).</summary>
        public static readonly float[] HeadWidth={.82f,.88f,.86f,.74f},HeadDrop={-.03f,-.02f,-.01f,-.07f};
        private static PveAddStats Row(string id,string attack,float hp,float damage,float walk,float run,float reach,float start,float hit,float cycle,float lunge,float impulse,float reward,float limb,float head,float stagger,float radius)
            =>new PveAddStats{id=id,attack=attack,hp=hp,damage=damage,walk=walk,run=run,reach=reach,startRange=start,hit=hit,cycle=cycle,lunge=lunge,impulse=impulse,reward=reward,limbFrac=limb,headFrac=head,stagger=stagger,smashRadius=radius};

        // ---- indices: the four follow the zones before them (Core/NativeHordeZones registration order) ----
        private static class At{internal static readonly int First=NativeHordeZones.First(Kinds);}
        /// <summary>The horde kind index of the Linesman (the others follow in the zone's order).</summary>
        public static int First=>At.First;
        public static int Kind(int local)=>At.First+local;
        /// <summary>The zone's local index of a horde kind (-1: not one of the four).</summary>
        public static int Local(int kind){int i=kind-At.First;return At.First>=0&&i>=0&&i<KindCount?i:-1;}
        public static bool Is(int kind)=>Local(kind)>=0;

        // ---- telegraphs and specials (sections 7 and 8; the authority's own times once Astra has them) ----
        /// <summary>The Linesman's arc: 1.1 s wind-up (the headlamp flares white to blue, arcs climb the raised arm, a rising hum,
        /// the floor crackles), then the bolt to the nearest pilot in sight within 9 m and chains to 2 more within 4 m of each
        /// other; each target jolted for .35 s; a .25 s whip of the arm; cooldown 6 s (3 s grounded).</summary>
        public const float ArcWindup=1.1f,ArcWhip=.25f,ArcRecover=.65f,ArcRange=9,ArcChain=4,ArcJolt=.35f,ArcCooldown=6,ArcGroundedCooldown=3,BoltSeconds=.32f;
        public const int ArcTargets=3;
        /// <summary>Grounded: the kneel that grips the rail (.8 s recharge), the brighter state's glow.</summary>
        public const float GroundKneel=.8f,GroundedGlow=1.55f;
        /// <summary>The Linesman's weak point: the battery pack, hits from his rear 120 degrees.</summary>
        public const float BackCone=120;
        /// <summary>The Conductor's whistle ("All aboard"): .8 s wind-up (he inhales, the lantern flares), then every add within
        /// 14 m runs faster for 6 s; cooldown 12 s. Last call: the lantern raised and rung for 1.2 s; cooldown 20 s.</summary>
        public const float WhistleWindup=.8f,WhistleRadius=14,AboardSeconds=6,WhistleCooldown=12,LastCallTelegraph=1.2f,LastCallCooldown=20;
        /// <summary>The Undertow's grab: .6 s of bubbles converging on the target's feet, the lunge (2.6 m) at .6 s, the hold up to
        /// 1.5 s (1.0 s with one pilot in the instance; owner decision 5) dragged at 2.7 m/s; cooldown 5 s after a release.</summary>
        public const float GrabTelegraph=.6f,GrabReach=2.6f,DragSpeed=2.7f,HoldSeconds=1.5f,HoldSolo=1f,GrabCooldown=5;
        /// <summary>Submerged: the body sunk .75 m (only the head above the water); surfacing on the crawl-to-stand at 1.3 times;
        /// its death sinks it 1 m over 1.2 s.</summary>
        public const float Submerged=.75f,SurfaceRate=1.3f,DeathSink=1,DeathSinkSeconds=1.2f;
        /// <summary>The Pileup's shove: a .7 s brace (it leans back, the door scrapes the floor), then a 2.4 m, 70 degree cone with a
        /// 6 m/s knockback; cooldown 4 s. The door covers its front 70 degrees; the second head is the weak point.</summary>
        public const float ShoveWindup=.7f,ShoveReach=2.4f,ShoveCone=70,ShoveKnock=6,ShoveCooldown=4,FrontCone=70;
        /// <summary>How long a shove plays out after its impact (the push's follow-through and return).</summary>
        public const float ShoveRecover=.75f;

        // ---- the replicated attack (section 8: LastAttack plus Attack, the existing pattern) ----
        public enum Special{None,Arc,Whistle,Grab,Shove}
        /// <summary>The special the replicated attack name starts (the view's <c>attack</c>: "arc", "whistle", "grab", "shove";
        /// anything else, null or "" is the content's melee). The Undertow's grab is its content attack.</summary>
        public static Special SpecialOf(int kind,string attack)
        {
            if(string.IsNullOrEmpty(attack))return Special.None;
            switch(Local(kind))
            {
                case Linesman:return attack=="arc"?Special.Arc:Special.None;
                case Conductor:return attack=="whistle"?Special.Whistle:Special.None;
                case Undertow:return attack=="grab"?Special.Grab:Special.None;
                case Pileup:return attack=="shove"?Special.Shove:Special.None;
            }
            return Special.None;
        }
        /// <summary>When a special strikes after LastAttack (its wind-up); NaN for none.</summary>
        public static float SpecialStrike(Special s)=>s==Special.Arc?ArcWindup:s==Special.Whistle?WhistleWindup:s==Special.Grab?GrabTelegraph:s==Special.Shove?ShoveWindup:float.NaN;
        /// <summary>When a special has played out after LastAttack (the gait takes the body back).</summary>
        public static float SpecialEnd(Special s)=>s==Special.Arc?ArcWindup+ArcRecover:s==Special.Whistle?WhistleWindup+.55f:s==Special.Grab?GrabTelegraph+.5f:s==Special.Shove?ShoveWindup+ShoveRecover:0;

        // ---- audio (section 10's enemy cues, positional; the owner generates them later) ----
        /// <summary>The cue keys pve.station.&lt;kind&gt;.&lt;cue&gt; (constants: no string is built per frame).</summary>
        public const string LinesmanIdle="pve.station.linesman.idle",LinesmanCharge="pve.station.linesman.charge",LinesmanArc="pve.station.linesman.arc",LinesmanGround="pve.station.linesman.ground",LinesmanHurt="pve.station.linesman.hurt",LinesmanDeath="pve.station.linesman.death",
            ConductorInhale="pve.station.conductor.inhale",ConductorWhistle="pve.station.conductor.whistle",ConductorBell="pve.station.conductor.bell",ConductorMutter="pve.station.conductor.mutter",ConductorLantern="pve.station.conductor.lantern",ConductorDeath="pve.station.conductor.death",
            UndertowBubble="pve.station.undertow.bubble",UndertowSurface="pve.station.undertow.surface",UndertowGrab="pve.station.undertow.grab",UndertowDrag="pve.station.undertow.drag",UndertowDeath="pve.station.undertow.death",
            PileupMoan="pve.station.pileup.moan",PileupBrace="pve.station.pileup.brace",PileupShove="pve.station.pileup.shove",PileupRicochet="pve.station.pileup.ricochet",PileupSplit="pve.station.pileup.split";
        /// <summary>The keys' common prefix (written so the code scan of key literals does not read it as a key).</summary>
        public const string Prefix="pve"+".station.";
        /// <summary>Every enemy cue key of section 10, with its hard length cap (s): the bank contract (NativeSoundKeys) lists them.</summary>
        public static readonly string[] CueKeys={LinesmanIdle,LinesmanCharge,LinesmanArc,LinesmanGround,LinesmanHurt,LinesmanDeath,ConductorInhale,ConductorWhistle,ConductorBell,ConductorMutter,ConductorLantern,ConductorDeath,
            UndertowBubble,UndertowSurface,UndertowGrab,UndertowDrag,UndertowDeath,PileupMoan,PileupBrace,PileupShove,PileupRicochet,PileupSplit};
        /// <summary>The cue a kind's hurt and death play (null: the zombie voice alone).</summary>
        public static string HurtCue(int kind)=>Local(kind)==Linesman?LinesmanHurt:null;
        public static string DeathCue(int kind){switch(Local(kind)){case Linesman:return LinesmanDeath;case Conductor:return ConductorDeath;case Undertow:return UndertowDeath;case Pileup:return PileupSplit;}return null;}
        public static readonly float[] CueCaps={2,1.2f,.7f,1,.5f,1.7f, .9f,1.1f,1.3f,2.1f,.7f,1.5f, 2.1f,.9f,.7f,1.6f,1.5f, 2.6f,.8f,.7f,.35f,1.6f};
        /// <summary>The existing bank key a cue borrows while it has no clips (a fitting sound only: the arc's lightning, the
        /// lantern's glass, the water, the door's metal and the split's gore); null leaves it silent until the owner's clip.</summary>
        public static string StandIn(string key)
        {
            switch(key)
            {
                case "pve.station.linesman.charge":return "chip.delayed.charge";case "pve.station.linesman.arc":return "magic.lightning.hit";
                case "pve.station.linesman.ground":case "pve.station.linesman.hurt":return "status.shock-zap";case "pve.station.linesman.death":return "blast.static";
                case "pve.station.conductor.lantern":return "break.glass";
                case "pve.station.undertow.surface":return "magic.water.hit";case "pve.station.undertow.grab":case "pve.station.undertow.death":return "impact.water";
                case "pve.station.pileup.brace":return "structure.creak";case "pve.station.pileup.shove":return "melee.shield.bash";
                case "pve.station.pileup.ricochet":return "gun.ricochet";case "pve.station.pileup.split":return "pve.gib";
            }
            return null;
        }
    }

    /// <summary>
    /// What the presentation reads of a station add beyond today's minion frame (section 8's "Networking: what the presentation
    /// needs"; the names are section 8's, so Astra's binding is one line: Runtime/NativeStationKindsPresentation.cs
    /// StationViewOf, as the City Ironworks' IronworksView). Times are the arena clock (NaN: not happening); ids -1 when unset;
    /// <see cref="StationView.None"/> is every add's view until the rules replicate them. Authority state, replicated by Astra; the presentation only reads it. Until it is replicated no add
    /// carries one, and every station kind draws its gait, melee, hits and death from the existing fields alone; the DEV gallery
    /// fills its own views.
    /// <list type="bullet">
    /// <item><c>attack</c>: which move the replicated LastAttack started ("arc", "whistle", "grab", "shove"; null the melee).</item>
    /// <item>Linesman: <c>arcTargets</c> (the pilots' seat ids the bolt struck, the first the primary, then the chain) and
    /// <c>arcAt</c> (the strike's time), one event per strike; <c>grounded</c> (on live rail or in the flood).</item>
    /// <item>Conductor: <c>lanternBroken</c>, <c>lastCallAt</c>; on every add the whistle buffed: <c>aboardUntil</c>.</item>
    /// <item>Undertow: <c>submerged</c>, <c>surfaceAt</c>, <c>grabTarget</c> (the held pilot's seat id), <c>grabAt</c>,
    /// <c>releaseAt</c> (the authority's hold limit, moved forward on an early release).</item>
    /// <item>Pileup: <c>splitAt</c> and <c>childIds</c> (the death event: the adds that stand out of it).</item>
    /// </list>
    /// </summary>
    public struct StationView
    {
        public string attack;
        public int[] arcTargets;public float arcAt;public bool grounded;
        public bool lanternBroken;public float lastCallAt;public float aboardUntil;
        public bool submerged;public float surfaceAt;public int grabTarget;public float grabAt,releaseAt;
        public float splitAt;public int[] childIds;
        /// <summary>Every add's view until the rules replicate the fields (and every other kind's): no special, nothing held.</summary>
        public static readonly StationView None=new StationView{attack=null,arcTargets=null,arcAt=float.NaN,grounded=false,lanternBroken=false,lastCallAt=float.NaN,aboardUntil=float.NaN,
            submerged=false,surfaceAt=float.NaN,grabTarget=-1,grabAt=float.NaN,releaseAt=float.NaN,splitAt=float.NaN,childIds=null};
        /// <summary>Whether the whistle's buff holds on this add at `time`.</summary>
        public bool Aboard(float time)=>time<aboardUntil&&time>=aboardUntil-NativeStationKinds.AboardSeconds;
        /// <summary>Whether this add holds a pilot at `time` (from the grab's landing to its release).</summary>
        public bool Holding(float time)=>grabTarget>=0&&time>=grabAt&&time<releaseAt;
    }
}
