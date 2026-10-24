using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    /// <summary>What a stage load brings in (it picks the loading screen's eyebrow, mode tag and tips).</summary>
    public enum StageLoadKind{Match,Online,Boss,Sandbox,Expedition,Area,Change,Return}
    public enum StageLoadPhase{Idle,Cover,Hold,Reveal}
    /// <summary>The presentation preload, in order (Runtime NativeStageLoadingUI drives it behind the covered screen).</summary>
    public enum StagePreloadStep{Stage,Kit,Frames,Shaders,Audio,Done}

    /// <summary>What the game says this frame, for the loading screen's hold (Runtime NativeStageLoadingUI computes it).</summary>
    public struct StageLoadSignals
    {
        /// <summary>The stage this load waits for is in place (the arena on its map; the menu, for a return).</summary>
        public bool Stage;
        /// <summary>The requested action produced no stage (a refused start) or the game stopped with an error: show what is there.</summary>
        public bool Failed;
        /// <summary>The local presentation finished its preload (StagePreloadPlan.Done).</summary>
        public bool Ready;
        /// <summary>The game itself holds the stage (an Expedition area barrier; Astra's load gate, StageLoadGate.Waiting).</summary>
        public bool Waiting;
        /// <summary>Gameplay runs and nothing holds it (the fight phase): never cover it.</summary>
        public bool Live;
        /// <summary>The longest the screen may still hold for the preload while the game's clock runs (Budget below).</summary>
        public float Budget;
    }

    /// <summary>
    /// The stage loading screen's state machine (owner 2026-10-15: "Introduce loading on stages ... we load in when we enter an
    /// instance that way we get time to load all assets prior to entering"). Cover: the shutters close over what is there (a
    /// requested load runs its action, such as StartMatch, only once the screen is covered). Hold: the stage is built and its
    /// presentation preloaded behind the screen. Reveal: the screen opens on the stage. It never sticks: a failed or refused
    /// start reveals at once, live gameplay reveals at once (fast), a game hold (area barrier, load gate) is honoured up to
    /// WaitTimeout, and a slow preload up to the clock's Budget. Pure state: Runtime draws it and feeds the signals.
    /// </summary>
    public sealed class NativeStageLoading
    {
        public const float CoverSeconds=.3f,ReducedCover=.18f,MinHold=.35f,RevealSeconds=.45f,ReducedReveal=.25f,LiveReveal=.2f;
        /// <summary>Preload hold without a running game clock (a return to the menu, a stage the authority has not started).</summary>
        public const float ReadyTimeout=6;
        /// <summary>The longest a game hold (Waiting) keeps the screen up; the existing Expedition transfer screen stays after it.</summary>
        public const float WaitTimeout=20;
        /// <summary>The longest the screen waits for the stage itself to appear.</summary>
        public const float StageTimeout=4;
        public const string Ready="ready",Live="live",Failed="failed",Timeout="timeout",Waited="wait-timeout",Missing="no-stage";

        public StageLoadPhase Phase{get;private set;}
        public StageLoadKind Kind{get;private set;}
        public bool Reduced{get;private set;}
        /// <summary>Seconds in the current phase.</summary>
        public float Age{get;private set;}
        /// <summary>Seconds the screen has been fully covered in this load.</summary>
        public float Held{get;private set;}
        /// <summary>One per load (the loading screen's tip, once-per-load work and the readiness report key on it).</summary>
        public int Serial{get;private set;}
        /// <summary>A deferred action (StartMatch, a return to the menu) runs once the cover closes.</summary>
        public bool Requested{get;private set;}
        /// <summary>Why the last reveal began (Ready, Live, Failed, Timeout, Waited, Missing).</summary>
        public string Reason{get;private set;}
        /// <summary>The progress shown: eased toward the preload's work, never backwards within a load, full on the reveal.</summary>
        public float Shown{get;private set;}
        public bool FastReveal{get;private set;}
        private bool actionPending;

        public bool Visible=>Phase!=StageLoadPhase.Idle;
        /// <summary>Clicks and keys are held while the screen closes and while it is closed.</summary>
        public bool BlocksInput=>Phase==StageLoadPhase.Cover||Phase==StageLoadPhase.Hold;
        public bool Covered=>Phase==StageLoadPhase.Hold;
        public bool ActionPending=>actionPending;
        public float CoverTime=>Reduced?ReducedCover:CoverSeconds;
        public float RevealTime=>FastReveal?LiveReveal:Reduced?ReducedReveal:RevealSeconds;
        public float CoverProgress=>Phase==StageLoadPhase.Cover?Clamp01(Age/CoverTime):Phase==StageLoadPhase.Idle?0:1;
        public float RevealProgress=>Phase==StageLoadPhase.Reveal?Clamp01(Age/RevealTime):0;
        /// <summary>How much of the screen the loading screen holds (1 covered; eased on the way in and out).</summary>
        public float Coverage=>Phase==StageLoadPhase.Idle?0:Phase==StageLoadPhase.Cover?CoverProgress:Phase==StageLoadPhase.Hold?1:1-RevealProgress;

        /// <summary>A user's start or return: the shutters close first, then the action runs (TakeAction) under them.</summary>
        public void Request(StageLoadKind kind,bool reduced)=>Begin(kind,reduced,true,false);
        /// <summary>A stage that appeared in one frame (a guest's first frame, an area swap, a return): covered at once.</summary>
        public void Cover(StageLoadKind kind,bool reduced)=>Begin(kind,reduced,false,true);
        private void Begin(StageLoadKind kind,bool reduced,bool request,bool instant)
        {
            if(Phase==StageLoadPhase.Cover||Phase==StageLoadPhase.Hold)
            {
                // Already closing or closed: the same screen carries on (a request adds its action; the newest kind names it).
                Kind=kind;if(request){Requested=true;actionPending=true;}return;
            }
            float from=Phase==StageLoadPhase.Reveal?Coverage:0;
            Serial++;Kind=kind;Reduced=reduced;Requested=request;actionPending=request;Held=0;Shown=0;Reason=null;FastReveal=false;
            if(instant){Phase=StageLoadPhase.Hold;Age=0;}else{Phase=StageLoadPhase.Cover;Age=from*CoverTime;}
        }
        /// <summary>True once per request, when the screen is covered: run the deferred action now.</summary>
        public bool TakeAction(){if(Phase!=StageLoadPhase.Hold||!actionPending)return false;actionPending=false;return true;}
        /// <summary>Drops the load at once (the game shut down, a dedicated host).</summary>
        public void Cancel(){Phase=StageLoadPhase.Idle;Age=Held=0;actionPending=false;Requested=false;}

        public void Step(float dt,in StageLoadSignals s,float work)
        {
            if(!(dt>=0))dt=0;if(dt>.25f)dt=.25f;
            switch(Phase)
            {
                case StageLoadPhase.Cover:Age+=dt;if(Age>=CoverTime){Phase=StageLoadPhase.Hold;Age=0;}break;
                case StageLoadPhase.Hold:
                {
                    Age+=dt;Held+=dt;if(actionPending)break;
                    string why=RevealReason(s);
                    if(why!=null){Reason=why;FastReveal=why==Live;Phase=StageLoadPhase.Reveal;Age=0;}
                    break;
                }
                case StageLoadPhase.Reveal:Age+=dt;if(Age>=RevealTime){Phase=StageLoadPhase.Idle;Age=0;}break;
            }
            float target=Phase==StageLoadPhase.Reveal?1:Clamp01(float.IsNaN(work)?0:work);
            if(Phase==StageLoadPhase.Idle)target=Shown;
            if(target>Shown)Shown=Math.Min(target,Shown+Math.Max((target-Shown)*(1-(float)Math.Exp(-12*dt)),.35f*dt));
        }
        private string RevealReason(in StageLoadSignals s)
        {
            if(s.Failed)return Failed;
            if(!s.Stage)return Held>=StageTimeout?Missing:null;
            if(s.Waiting)return Held>=WaitTimeout?Waited:null;
            if(s.Live)return Live;
            if(!s.Ready)return Held>=Math.Max(MinHold,s.Budget)?Timeout:null;
            return Held>=MinHold?Ready:null;
        }

        /// <summary>
        /// How long the screen may hold for the preload while the authority's clock runs, by what the game shows now: the
        /// countdown 1 s (it must read 2 and 1), an entrance intro or a chip draft 2 s, a round result 1 s; the fight none
        /// (Live); no running clock (the menu, a lobby, the end panel) ReadyTimeout. Astra's load gate (StageLoadGate) removes
        /// the need: the authority starts the clock once every client is ready.
        /// </summary>
        public static float Budget(NativePhase? phase)
        {
            if(phase==null)return ReadyTimeout;
            switch(phase.Value)
            {
                case NativePhase.Countdown:return 1;
                case NativePhase.Intro:case NativePhase.Draft:return 2;
                case NativePhase.Result:return 1;
                case NativePhase.Fight:return 0;
                default:return ReadyTimeout;
            }
        }
        public static float Clamp01(float v)=>v<0?0:v>1?1:v;
    }

    /// <summary>
    /// The presentation preload behind the covered screen, step by step with its progress share. Stage: wait for the arena
    /// (or the menu). Kit: build the stage's lazily built scenery, dressing, fx scenes and lights (ArenaPresentation.PrewarmStage).
    /// Frames: render the stage behind the cover (the first frames upload meshes, create rigs and compile the stage's shader
    /// variants). Shaders: draw every Resources shader once into a hidden target (NativeShaderWarmup). Audio: drain the effects
    /// bank's preload queue. A step with nothing to do is skipped and its share goes to the others.
    /// </summary>
    public sealed class StagePreloadPlan
    {
        public static readonly string[] Labels={"LOCKING STAGE","BUILDING STAGE KIT","RENDERING STAGE","WARMING SHADERS","LOADING AUDIO","STAGE READY"};
        /// <summary>The Resources shaders compiled with GPU instancing (multi_compile_instancing; the managed checks hold this to the
        /// sources): the shader warm-up draws these instanced too, every shader plain.</summary>
        public static readonly string[] InstancedShaders={"ChipArena/NativeBlockLit","ChipArena/NativeMolten","ChipArena/NativeDevGhost","ChipArena/NativeEntranceGhost","ChipArena/NativeHologram","ChipArena/KillBody","ChipArena/KillGhost","ChipArena/NativeLit","ChipArena/NativeRepair","ChipArena/NativeReviveHolo","ChipArena/NativeSmoke","ChipArena/SourceLit","ChipArena/SourceTransparent","ChipArena/NativeStatusUnlit","ChipArena/NativeWeaponGhost","ChipArena/PrototypeLit","ChipArena/ZombieLit"};
        public static bool Instanced(string shader){foreach(var s in InstancedShaders)if(s==shader)return true;return false;}
        public static readonly float[] Weights={.10f,.30f,.15f,.35f,.07f,.03f};
        /// <summary>Frames rendered behind the cover before the stage counts as drawn.</summary>
        public const int WarmFrames=2;
        public StagePreloadStep Step{get;private set;}
        public float Partial{get;private set;}
        private readonly bool[] skip=new bool[Labels.Length];
        public bool Done=>Step==StagePreloadStep.Done;
        public string Label=>Labels[(int)Step];
        public bool Skipped(StagePreloadStep s)=>skip[(int)s];
        /// <summary>A new load: <paramref name="world"/> false (a return to the menu) skips the kit, shaders and audio.</summary>
        public void Reset(bool world)
        {
            Step=StagePreloadStep.Stage;Partial=0;for(int i=0;i<skip.Length;i++)skip[i]=false;
            if(!world){skip[(int)StagePreloadStep.Kit]=skip[(int)StagePreloadStep.Shaders]=skip[(int)StagePreloadStep.Audio]=true;}
        }
        public void Skip(StagePreloadStep s){if(s!=StagePreloadStep.Done&&s!=Step)skip[(int)s]=true;else if(s==Step&&s!=StagePreloadStep.Done)Next();}
        public void Set(float partial)=>Partial=Clamp01(partial);
        /// <summary>Completes the current step and moves to the next one not skipped.</summary>
        public void Next()
        {
            if(Done)return;int i=(int)Step+1;while(i<(int)StagePreloadStep.Done&&skip[i])i++;Step=(StagePreloadStep)i;Partial=0;
        }
        /// <summary>The share of the preload done (0..1): the steps before this one and this one's partial, over the steps kept.</summary>
        public float Work
        {
            get
            {
                if(Done)return 1;float total=0,done=0;
                for(int i=0;i<Weights.Length;i++){if(skip[i])continue;total+=Weights[i];if(i<(int)Step)done+=Weights[i];else if(i==(int)Step)done+=Weights[i]*Partial;}
                return total>0?Clamp01(done/total):1;
            }
        }
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
    }

    /// <summary>
    /// The authority's stage-load gate as this client sees it (Astra's side fills it from the network; without a gate it stays
    /// clear and the loading screen covers only the local load). While Waiting the screen holds (up to WaitTimeout) and shows
    /// Ready of Required. Contract: a client reports StageReadiness (ConversionGame.StagePresentation) for the authority's current
    /// epoch and map; the authority holds the stage's clock until every presenting client is Ready or its own timeout runs out.
    /// </summary>
    public sealed class StageLoadGate
    {
        /// <summary>The authority has not started this stage: it waits for its clients' presentation (or its timeout).</summary>
        public bool Waiting;
        /// <summary>Clients whose StageReadiness for this stage the authority has (display only).</summary>
        public int Ready,Required;
        /// <summary>The authority's remaining wait in seconds (NaN: none shown).</summary>
        public float Remaining=float.NaN;
        public void Clear(){Waiting=false;Ready=Required=0;Remaining=float.NaN;}
    }

    /// <summary>The local presentation's readiness for the stage now shown: what a client reports to the authority's gate.</summary>
    public struct StageReadiness
    {
        /// <summary>The loading screen's load (NativeStageLoading.Serial); changes with every stage load.</summary>
        public int Serial;
        /// <summary>The map the arena holds, its generation (it changes every round reset) and the session epoch of the stage.</summary>
        public string Map;public int Generation;public int Epoch;
        /// <summary>The stage shows (or may show as soon as the authority allows): the preload finished, or the screen opened early
        /// on live play or a timeout.</summary>
        public bool Ready;
        /// <summary>The whole preload ran behind the screen (false: it opened early and the rest builds on first use, as before).</summary>
        public bool Preloaded;
        /// <summary>Seconds from the start of the load to ready (unscaled; 0 until ready).</summary>
        public float Seconds;
    }

    /// <summary>The loading screen's words: cached constants and one display name per map, so a frame builds no string.</summary>
    public static class NativeStageLabels
    {
        public const string Scan="STAGE SCAN",Blocks=" BLOCKS",TipKey="TIP",PartyWaiting="WAITING FOR THE PARTY",PilotsWaiting="WAITING FOR PILOTS",PilotsReady="PILOTS READY",Locking="LOCKING STAGE";
        private static readonly string[] eyebrows={"ENTERING STAGE","ENTERING STAGE","ENTERING STAGE","DEPLOYING · SANDBOX","EXPEDITION · DEPLOYING","AREA TRANSFER","NEXT STAGE","RETURNING"};
        private static readonly string[] modes={"LOCAL MATCH","ONLINE MATCH","PVE · BOSS FIGHT","DEV SANDBOX","EXPEDITION","EXPEDITION · AREA","STAGE CHANGE","MAIN MENU"};
        public static string Eyebrow(StageLoadKind k)=>eyebrows[(int)k];
        public static string Mode(StageLoadKind k)=>modes[(int)k];
        public const string ReturnLobby="LOBBY",ReturnMenu="MAIN MENU";

        private static readonly Dictionary<string,string> names=new Dictionary<string,string>(StringComparer.Ordinal);
        /// <summary>The map's display name in capitals, " (Expedition)" dropped (built once per map).</summary>
        public static string StageName(ContentMap map)
        {
            if(map==null)return "";string key=map.id??"";
            if(names.TryGetValue(key,out var s))return s;
            string n=string.IsNullOrEmpty(map.name)?key:map.name;int p=n.IndexOf(" (",StringComparison.Ordinal);if(p>0)n=n.Substring(0,p);
            s=n.Trim().ToUpperInvariant();if(names.Count>128)names.Clear();names[key]=s;return s;
        }
        private static readonly string[] percents=MakePercents();
        private static string[] MakePercents(){var p=new string[101];for(int i=0;i<=100;i++)p[i]=i+"%";return p;}
        public static string Percent(float f){if(!(f>0))return percents[0];if(f>=1)return percents[100];return percents[Math.Min(100,(int)Math.Floor(f*100+1e-4))];}
        private static readonly string[] counts=MakeCounts();
        private static string[] MakeCounts(){var p=new string[17*17];for(int a=0;a<17;a++)for(int b=0;b<17;b++)p[a*17+b]=a+" / "+b;return p;}
        /// <summary>"a / b" for 0..16 (cached), else a short fallback.</summary>
        public static string Count(int a,int b)=>a>=0&&b>=0&&a<17&&b<17?counts[a*17+b]:b>16?"16+":"";

        // ---- tips (true to today's controls: the pause menu's hints, the PvE and Expedition prompts) --------------------
        public static readonly string[] GeneralTips=
        {
            "Double-tap W to sprint. Shift rolls the way you hold.",
            "Right mouse blocks. Block in time and left click to riposte.",
            "Hold E to aim your ability, release to cast it. Q is your second.",
            "V switches first and third person. X swaps the shoulder.",
            "Tab locks on and opens the board. G shows the scores.",
            "Escape opens the match menu. M mutes the game anywhere.",
        };
        public static readonly string[] BossTips=
        {
            "Press C to pick your chips during the boss fight.",
            "Stay near the party: downed pilots can be revived.",
            "Ward Zero's floor vents open into black pits. Watch the grates.",
        };
        public static readonly string[] ExpeditionTips=
        {
            "F opens doors, caches and Ward machines. J opens the Expedition screen.",
            "The sanctuary is the party's checkpoint and its way out.",
            "The run clock pauses while the party loads an area.",
        };
        public static readonly string[] SandboxTips={"F3 opens the DEV panel. Escape opens the sandbox's match menu."};
        /// <summary>This load's tip: the kind's own set first on odd loads, the general set otherwise; null for a return.</summary>
        public static string Tip(StageLoadKind k,int serial)
        {
            if(k==StageLoadKind.Return)return null;
            var own=k==StageLoadKind.Boss?BossTips:k==StageLoadKind.Expedition||k==StageLoadKind.Area?ExpeditionTips:k==StageLoadKind.Sandbox?SandboxTips:null;
            int s=serial<0?-serial:serial;
            if(own!=null&&(s%2==1||k==StageLoadKind.Area))return own[(s/2)%own.Length];
            return GeneralTips[s%GeneralTips.Length];
        }
    }

    /// <summary>A box on the UI canvas (canvas units).</summary>
    public struct LoadingBox
    {
        public float X,Y,W,H;public LoadingBox(float x,float y,float w,float h){X=x;Y=y;W=w;H=h;}
        public float Right=>X+W;public float Bottom=>Y+H;
        public bool Inside(float width,float height)=>X>=-.01f&&Y>=-.01f&&Right<=width+.01f&&Bottom<=height+.01f&&W>=0&&H>=0;
        public bool Overlaps(LoadingBox o)=>X<o.Right&&o.X<Right&&Y<o.Bottom&&o.Y<Bottom;
    }

    /// <summary>
    /// The loading screen's layout on the UI canvas (NativeUiMotion.Measure: 900 tall at 16:9, wider at 21:9, smaller with a
    /// large UI scale). Everything sits in the centred 16:9 band (SafeX/SafeWidth), the shutters and the backdrop span the whole
    /// width. Wide canvases: the title block at the left, the stage scan at the right; narrow ones drop the scan. The progress
    /// row (step label, percentage, bar, tip) runs along the foot.
    /// </summary>
    public struct NativeLoadingLayout
    {
        public LoadingBox Text,Panel,Bar,Label,Tip,Spinner;public float Eyebrow,Title,TitleSize,Rule,Mode,Tag,Party;public bool Scan;
        /// <summary>With a party (an Expedition load): its panel under the title block (Party is its top) and whether the map's tag
        /// line made room for it (NativePartyLayout.Fit).</summary>
        public NativePartyLayout Roster;public bool TagHidden;
        public static NativeLoadingLayout Measure(float width,float height,float safeX,float safeWidth,bool scan=true,int party=0)
        {
            var l=new NativeLoadingLayout();float m=Math.Max(28,safeWidth*.055f),left=safeX+m,right=safeX+safeWidth-m,span=right-left;
            float foot=height-Math.Max(66,height*.1f);
            l.Bar=new LoadingBox(left,foot,span,4);
            l.Spinner=new LoadingBox(left,foot-34,22,22);
            l.Label=new LoadingBox(left+30,foot-31,span-30,16);
            l.Tip=new LoadingBox(left,foot+16,span,18);
            float top=Math.Max(56,height*.14f),bottom=foot-48;
            l.Scan=scan&&safeWidth>=900&&bottom-top>=260;
            if(l.Scan)
            {
                float pw=span*.47f,ph=Math.Min(pw*.66f,bottom-top);
                l.Panel=new LoadingBox(right-pw,top+(bottom-top-ph)/2,pw,ph);
                l.Text=new LoadingBox(left,top,l.Panel.X-left-m*.6f,bottom-top);
            }
            else{l.Panel=new LoadingBox(right,top,0,0);l.Text=new LoadingBox(left,top,span,bottom-top);}
            l.TitleSize=Math.Max(34,Math.Min(76,l.Text.W*.11f));
            float block=22+l.TitleSize*1.1f+18+30+26+40,y=l.Text.Y+Math.Max(0,(l.Text.H-block)/2);
            l.Eyebrow=y;l.Title=y+22;l.Rule=l.Title+l.TitleSize*1.1f+8;l.Mode=l.Rule+14;l.Tag=l.Mode+36;l.Party=l.Tag+28;
            if(party>0)NativePartyLayout.Fit(ref l,party);
            return l;
        }
    }

    /// <summary>
    /// What the loading screen shows for the current load, as cached strings (built when the load begins and when its stage is
    /// known, never per frame): the eyebrow, the stage's name (with the moment it arrived, for its type-on), the mode tag, the
    /// map's tag line, this load's tip, the stage scan and its block count, and the stage's accent.
    /// </summary>
    public sealed class StageLoadView
    {
        public const int Cyan=0x22e6ff;
        public StageLoadKind Kind{get;private set;}
        public string Eyebrow,Mode,Title,Tag,Tip,Count;public NativeStageSchematic Scan;public int Accent=Cyan;
        /// <summary>When the title arrived (unscaled seconds; NaN none yet) and its fitted size (0 until the draw measures it).</summary>
        public float TitleAt=float.NaN,TitleSize;
        public void Begin(StageLoadKind kind,int serial)
        {
            Kind=kind;Eyebrow=NativeStageLabels.Eyebrow(kind);Mode=NativeStageLabels.Mode(kind);Tip=NativeStageLabels.Tip(kind,serial);
            Title=Tag=Count=null;Scan=null;Accent=Cyan;TitleAt=float.NaN;TitleSize=0;
        }
        /// <summary>The stage is known: its name, tag line, scan and accent (an Expedition area keeps its own name, `area`).</summary>
        public void Stage(ContentMap map,float now,string area=null)
        {
            if(map==null)return;string t=string.IsNullOrEmpty(area)?NativeStageLabels.StageName(map):area;
            if(t!=Title){Title=t;TitleAt=now;TitleSize=0;}
            Tag=string.IsNullOrEmpty(map.tag)?null:map.tag;Scan=NativeStageSchematic.For(map);Count=Scan?.Label;Accent=map.accent!=0?map.accent&0xffffff:Cyan;
        }
        /// <summary>A return: the room's lobby or the main menu.</summary>
        public void Returning(bool lobby,float now)
        {string t=lobby?NativeStageLabels.ReturnLobby:NativeStageLabels.ReturnMenu;if(t!=Title){Title=t;TitleAt=now;TitleSize=0;}Tag=null;Scan=null;Count=null;Tip=null;}
    }
}
