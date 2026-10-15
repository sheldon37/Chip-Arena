using System;
using System.Collections.Generic;
using System.Globalization;
namespace ChipArena.UnityConversion
{
    // The DEV tools' engine-free rules (docs/DEV-TOOLS.md; the authority is Astra's, docs/DEV-ABUSE-AUTHORITY.md): who gets the
    // DEV panel where (the offline Developer Sandbox, or any match with Settings · Developer · DEV Abuse on), what each panel
    // action routes to (the existing NativeArena sandbox call, a DEV command through ConversionGame.SendDevCommand, or the
    // local view only), the commands' argument limits (NativeDevRules.Validate's), the reveal from the replicated
    // NativeSeat.devRevealed, and the presentation-only helpers the panel draws from (event log, graphs, fly camera, chip grid
    // query, forced hazard pulse looks, layout, cached labels). Nothing here touches the simulation.
    public enum NativeDevTab{Pilot,Interact,Monsters,Dummies,Chips,Arena,Hazards,Previews,Camera,Simulation}
    // Sandbox: an existing sandbox control (SandboxAllowed on the authority) with no command counterpart.
    // Contract: a DEV command; in the sandbox it runs the existing sandbox call when it has one.
    // Preview: a local presentation preview that has only ever run in the sandbox (galleries, draft previews, structure FX).
    // Presentation: read-only local view tools (fly camera, overlays, log, graphs, hide HUD): live wherever the panel is.
    public enum NativeDevKind{Sandbox,Contract,Preview,Presentation}
    // NeedsOptIn: a DEV command while ConversionGame.DevAbuseEnabled is off. NotNow: a DEV command outside an active round.
    // NeedsHost: a proposed command (the monster commands) the match's authority does not offer yet (devProtocol below 2).
    public enum NativeDevState{Live,NeedsOptIn,SandboxOnly,NotNow,Unavailable,NeedsHost}
    public enum NativeDevMode{Off,Sandbox,Abuse}

    /// <summary>What this client knows about the match it is in. Sandbox: this client runs the offline Developer Sandbox's
    /// authority (arena.Sandbox, offline, not a replica). Developer: the local account's verified, unexpired Developer claim.
    /// Abuse: the saved Settings · Developer · DEV Abuse opt-in. Commands: ConversionGame.DevAbuseEnabled, the session opt-in
    /// SendDevCommand needs (never trusted by a host: it checks the verified seat). Fight: the match is in an active round,
    /// which every DEV command needs. Monsters: the match's authority offers the proposed monster commands (a guest: the host
    /// advertised devProtocol 2 or later; offline or hosting: NativeDevRules knows spawn-monster).</summary>
    public struct NativeDevContext
    {
        public bool InMatch,Sandbox,Online,Hosting,Developer,Abuse,Commands,Fight,Monsters;
        public static NativeDevContext Of(bool inMatch,bool sandbox,bool online,bool hosting,bool developer,bool abuse,bool commands,bool fight=true,bool monsters=false)
            =>new NativeDevContext{InMatch=inMatch,Sandbox=sandbox&&!online,Online=online,Hosting=hosting,Developer=developer,Abuse=abuse,Commands=commands,Fight=fight,Monsters=monsters};
    }

    public static class NativeDevGate
    {
        /// <summary>Settings · Developer shows the DEV Abuse toggle only to a verified, unexpired Developer account.</summary>
        public static bool ToggleVisible(bool verifiedDeveloper)=>verifiedDeveloper;
        /// <summary>The session opt-in (ConversionGame.DevAbuseEnabled) follows the saved toggle, for a verified developer only.</summary>
        public static bool CommandsEnabled(bool saved,bool verifiedDeveloper)=>saved&&verifiedDeveloper;
        /// <summary>The offline sandbox keeps its tools for a developer as today (no opt-in); any other match needs DEV Abuse.
        /// Non-developers never get the panel, whatever the saved preference says.</summary>
        public static NativeDevMode Mode(in NativeDevContext c)=>!c.Developer||!c.InMatch?NativeDevMode.Off:c.Sandbox?NativeDevMode.Sandbox:c.Abuse?NativeDevMode.Abuse:NativeDevMode.Off;
        public static bool PanelAvailable(in NativeDevContext c)=>Mode(c)!=NativeDevMode.Off;
        /// <summary>The local "DEV ABUSE ON" HUD tag: the opt-in is on and in effect in this match.</summary>
        public static bool Indicator(in NativeDevContext c)=>c.Developer&&c.InMatch&&c.Abuse;
    }

    /// <summary>The DEV command names and the limits the panel clamps to (NativeDevRules.Validate's; DevToolsChecks probes them).</summary>
    public static class NativeDevContract
    {
        public const string Gate="NativeDevRules.DevAllowed",Command="dev-command",Reply="dev-reply",Accepted="accepted",Refused="refused",Done="done";
        public const string RevealedField="devRevealed";
        public const int MaxArgs=4,MaxArgLength=64,ChipCountMax=100,AroundMin=1,AroundMax=7,TeamMax=7;
        public const float TimeScaleMin=.1f,TimeScaleMax=2,DebrisMassMin=1,DebrisMassMax=500,DebrisSpeedMin=1,DebrisSpeedMax=100,FreezeMax=30,LaunchMin=1,LaunchMax=100;
        public static readonly string[] SpawnInputs={"ai","idle","primary","q","e","block","dash","jump","1","2","3","4","5"};
        /// <summary>Personal actions: any dev seat, on itself or a target.</summary>
        public static readonly string[] Personal={"restore","down","god","infinite-ammo","no-cooldowns","chip-add","chip-remove","weapon","teleport","spawn-dummy","clear-dummies","reveal"};
        /// <summary>Room-wide actions: any dev seat, host or guest (owner 2026-10-14: devs are not limited to rooms they host).</summary>
        public static readonly string[] Room={"time-scale","arena-reset","round-end","hazard-trigger","debris"};
        /// <summary>Interact actions on a target.</summary>
        public static readonly string[] Interact={"launch","freeze","bring","send","swap-weapon","shrink","grow","debris-rain","hazard-at","spawn-dummies-around"};
        public static readonly string[] Monsters={"spawn-monster","clear-monsters"};
        public static bool Known(string action)=>Array.IndexOf(NativeDevRules.Actions,action)>=0;
        /// <summary>The client's shape check before a send: Astra's own validator (actions, at most four arguments of at most 64
        /// characters, the per-action ranges); a proposed monster command the validator does not know yet is checked against
        /// the proposed contract (NativeDevMonsters). The host validates again with the verified grant; this never stands in for it.</summary>
        public static bool ShapeOk(string action,int target,string[] args)
        {
            if(!Known(action)&&NativeDevMonsters.Proposed(action))return target>=0&&NativeDevMonsters.ArgsOk(action,args);
            return NativeDevRules.Validate(new NativeDevCommand{transaction="panel",action=action,target=target,args=args??Array.Empty<string>()})==null;
        }
    }

    public sealed class NativeDevAction
    {
        public readonly string Id,Label,Contract,Key;public readonly NativeDevTab Tab;public readonly NativeDevKind Kind;
        /// <summary>True when the sandbox already has the call (NativeSandbox.cs DevRun): a Contract action runs it there.</summary>
        public readonly bool SandboxRoute;
        /// <summary>A command of the proposed monster contract (NativeDevMonsters): live only where the authority offers it.</summary>
        public bool Proposed=>Kind==NativeDevKind.Contract&&NativeDevMonsters.Proposed(Contract);
        public NativeDevAction(string id,NativeDevTab tab,NativeDevKind kind,string label,string contract=null,bool sandboxRoute=false,string key=null)
        {Id=id;Tab=tab;Kind=kind;Label=label;Contract=contract;SandboxRoute=sandboxRoute||kind==NativeDevKind.Sandbox||kind==NativeDevKind.Preview;Key=key;}
    }

    public static class NativeDevActions
    {
        public const string OptIn="Turn on DEV Abuse (Settings · Developer) to send DEV commands";
        public const string NotNow="DEV commands run during an active round";
        public const string SandboxOnly="Sandbox only: runs in the offline Developer Sandbox";
        public const string Off="DEV tools are not available in this match";
        public const string InSandbox="Live in the sandbox";
        public const string Local="Live: your view only, changes nothing in the match";
        public const string ToHost="Live: the match's DEV authority checks your verified grant, then runs it";
        public const string NeedsHost="HOST NEEDS MONSTER COMMANDS: the match's authority does not offer them yet (devProtocol 2)";
        private const NativeDevKind S=NativeDevKind.Sandbox,C=NativeDevKind.Contract,P=NativeDevKind.Preview,V=NativeDevKind.Presentation;
        private const NativeDevTab Mo=NativeDevTab.Monsters,Pi=NativeDevTab.Pilot,In=NativeDevTab.Interact,Du=NativeDevTab.Dummies,Ch=NativeDevTab.Chips,Ar=NativeDevTab.Arena,Ha=NativeDevTab.Hazards,Pr=NativeDevTab.Previews,Ca=NativeDevTab.Camera,Si=NativeDevTab.Simulation;
        /// <summary>Every panel action. The ids with a sandbox route are the cases of NativeSandbox.cs DevRun.</summary>
        public static readonly NativeDevAction[] All=
        {
            // Pilot (self).
            new NativeDevAction("me-restore",Pi,C,"Restore me","restore",true,"F5"),
            new NativeDevAction("me-hit",Pi,S,"Hit me (18)"),
            new NativeDevAction("me-down",Pi,C,"Down me","down",true),
            new NativeDevAction("me-summons",Pi,S,"Clear enemy summons"),
            new NativeDevAction("me-weapon",Pi,C,"Swap my weapon","weapon",true),
            new NativeDevAction("me-god",Pi,C,"God mode","god"),
            new NativeDevAction("me-ammo",Pi,C,"Infinite ammo","infinite-ammo"),
            new NativeDevAction("me-cooldowns",Pi,C,"No cooldowns","no-cooldowns"),
            new NativeDevAction("me-teleport",Pi,C,"Teleport to aim","teleport"),
            new NativeDevAction("me-reveal",Pi,C,"Reveal: a developer is here","reveal"),
            // Interact (a target pilot or bot).
            new NativeDevAction("launch",In,C,"Launch","launch"),
            new NativeDevAction("freeze",In,C,"Freeze","freeze"),
            new NativeDevAction("bring",In,C,"Bring to me","bring"),
            new NativeDevAction("send",In,C,"Send to aim","send"),
            new NativeDevAction("swap-weapon",In,C,"Swap weapon","swap-weapon"),
            new NativeDevAction("shrink",In,C,"Shrink","shrink"),
            new NativeDevAction("grow",In,C,"Grow","grow"),
            new NativeDevAction("debris-rain",In,C,"Debris rain","debris-rain"),
            new NativeDevAction("hazard-at",In,C,"Hazard at target","hazard-at"),
            new NativeDevAction("spawn-dummies-around",In,C,"Dummies around","spawn-dummies-around"),
            new NativeDevAction("target-restore",In,C,"Restore target","restore"),
            new NativeDevAction("target-down",In,C,"Down target","down"),
            new NativeDevAction("target-debris",In,C,"Debris at target","debris"),
            // Monsters (the proposed spawn-monster / clear-monsters contract, docs/DEV-TOOLS.md) and Ward Zero's sandbox entries.
            new NativeDevAction("spawn-monster",Mo,C,"Spawn monsters","spawn-monster"),
            new NativeDevAction("clear-monsters",Mo,C,"Clear DEV monsters","clear-monsters"),
            new NativeDevAction("ward-spawn",Mo,S,"Spawn at entry"),
            // Dummies.
            new NativeDevAction("dummy-cast",Du,S,"Make dummy cast",key:"F7"),
            new NativeDevAction("dummy-restore",Du,C,"Restore dummy","restore",true),
            new NativeDevAction("dummy-hit",Du,S,"Hit dummy (18)"),
            new NativeDevAction("dummy-down",Du,C,"Down dummy","down",true,"F6"),
            new NativeDevAction("dummy-summons",Du,S,"Clear summons (dummy)"),
            new NativeDevAction("dummy-respawn",Du,S,"Dummy respawn"),
            new NativeDevAction("dummy-repeat",Du,S,"Repeat cast"),
            new NativeDevAction("dummy-difficulty",Du,S,"Bot difficulty"),
            new NativeDevAction("spawn-dummy",Du,C,"Spawn dummy","spawn-dummy"),
            new NativeDevAction("clear-dummies",Du,C,"Clear DEV dummies","clear-dummies"),
            // Chips.
            new NativeDevAction("chip-add",Ch,C,"Add chip","chip-add",true),
            new NativeDevAction("chip-remove",Ch,C,"Remove chip","chip-remove",true),
            new NativeDevAction("chip-cast",Ch,S,"Dummy casts with chip"),
            new NativeDevAction("chip-cycle",Ch,S,"Cast every chip"),
            // Arena.
            new NativeDevAction("restore-all",Ar,S,"Restore all"),
            new NativeDevAction("arena-reset",Ar,C,"Arena Reset","arena-reset",true),
            new NativeDevAction("round-end",Ar,C,"End round","round-end"),
            new NativeDevAction("test-block",Ar,S,"Test Block"),
            new NativeDevAction("load-map",Ar,S,"Load map"),
            new NativeDevAction("debris",Ar,C,"Launch at Opponent","debris",true),
            new NativeDevAction("fx-collapse",Ar,P,"Play Collapse"),
            new NativeDevAction("fx-bursts",Ar,P,"Material Bursts"),
            new NativeDevAction("fx-clear",Ar,P,"Clear Effects"),
            // Hazards.
            new NativeDevAction("hazard-trigger",Ha,C,"Trigger","hazard-trigger"),
            new NativeDevAction("me-vent",Ha,S,"Vent dive (me)"),
            new NativeDevAction("dummy-vent",Ha,S,"Vent dive (dummy)"),
            new NativeDevAction("splat-delay",Ha,S,"Vent splat delay"),
            // Previews and galleries.
            new NativeDevAction("preview-draft",Pr,P,"Preview Draft"),
            new NativeDevAction("preview-rarity",Pr,P,"Preview Rarities"),
            new NativeDevAction("preview-knockout",Pr,P,"Preview Knockout"),
            new NativeDevAction("preview-death",Pr,P,"Preview My Death"),
            new NativeDevAction("gallery-minions",Pr,P,"Siren Minions"),
            new NativeDevAction("gallery-cyber-jump",Pr,P,"Cyber Double Jump"),
            new NativeDevAction("gallery-saw",Pr,P,"Saw Split"),
            new NativeDevAction("gallery-revive",Pr,P,"Revive"),
            new NativeDevAction("gallery-chip-select",Pr,P,"Chip Selecting"),
            new NativeDevAction("gallery-hazard-kit",Pr,P,"Hazard Kit"),
            new NativeDevAction("gallery-hazard-music",Pr,P,"Hazard Kit music lock"),
            new NativeDevAction("gallery-cyber-kit2-ranged",Pr,P,"Cyber Kit v2 ranged"),
            new NativeDevAction("gallery-cyber-kit2-melee",Pr,P,"Cyber Kit v2 melee"),
            new NativeDevAction("preview-reveal",Pr,P,"Preview DEV Reveal"),
            // Camera and overlays (presentation only: live wherever the panel is).
            new NativeDevAction("fly",Ca,V,"Fly camera",key:"F10"),
            new NativeDevAction("fly-snap",Ca,V,"Snap to pilot",key:"Home"),
            new NativeDevAction("overlay-hit",Ca,V,"Pilot hit volumes"),
            new NativeDevAction("overlay-melee",Ca,V,"Melee hit volumes"),
            new NativeDevAction("overlay-hazards",Ca,V,"Hazard boxes"),
            new NativeDevAction("overlay-nav",Ca,V,"Nav graph"),
            new NativeDevAction("overlay-anchors",Ca,V,"Expedition anchors"),
            new NativeDevAction("overlay-shots",Ca,V,"Shot paths"),
            new NativeDevAction("log",Ca,V,"Event log",key:"F1"),
            new NativeDevAction("graph",Ca,V,"FPS / network graph",key:"F2"),
            new NativeDevAction("hide-hud",Ca,V,"Hide HUD (screenshot)",key:"F11"),
            // Simulation.
            new NativeDevAction("time-scale",Si,C,"Simulation speed","time-scale",true),
            new NativeDevAction("safe-mode",Si,S,"Safe mode"),
        };
        private static readonly Dictionary<string,NativeDevAction> byId=Index();
        private static Dictionary<string,NativeDevAction> Index(){var d=new Dictionary<string,NativeDevAction>(StringComparer.Ordinal);foreach(var a in All)d[a.Id]=a;return d;}
        public static NativeDevAction Find(string id)=>id!=null&&byId.TryGetValue(id,out var a)?a:null;
        /// <summary>Whether `a` can run here and why: sandbox calls in the sandbox, the view tools wherever the panel is, DEV
        /// commands with the DEV Abuse opt-in during an active round (the host then checks the verified grant and says done or
        /// refused with its reason; the panel never reports success on its own).</summary>
        public static NativeDevState Resolve(NativeDevAction a,in NativeDevContext c,out string reason)
        {
            var mode=NativeDevGate.Mode(c);
            if(a==null||mode==NativeDevMode.Off){reason=Off;return NativeDevState.Unavailable;}
            switch(a.Kind)
            {
                case NativeDevKind.Presentation:reason=Local;return NativeDevState.Live;
                case NativeDevKind.Sandbox:case NativeDevKind.Preview:
                    if(mode==NativeDevMode.Sandbox){reason=InSandbox;return NativeDevState.Live;}reason=SandboxOnly;return NativeDevState.SandboxOnly;
                default:
                    if(mode==NativeDevMode.Sandbox&&a.SandboxRoute){reason=InSandbox;return NativeDevState.Live;}
                    if(!c.Commands){reason=OptIn;return NativeDevState.NeedsOptIn;}
                    if(a.Proposed&&!c.Monsters){reason=NeedsHost;return NativeDevState.NeedsHost;}
                    if(!c.Fight){reason=NotNow;return NativeDevState.NotNow;}
                    reason=ToHost;return NativeDevState.Live;
            }
        }
        /// <summary>True when a Live action runs the existing sandbox call (else, Live, it is a DEV command or a view tool).</summary>
        public static bool RunsInSandbox(NativeDevAction a,in NativeDevContext c)=>a!=null&&NativeDevGate.Mode(c)==NativeDevMode.Sandbox&&a.SandboxRoute&&a.Kind!=NativeDevKind.Presentation;
        /// <summary>The badge a disabled button wears.</summary>
        public static string Badge(NativeDevState s)=>s==NativeDevState.NeedsHost?"HOST":s==NativeDevState.NeedsOptIn?"OPT-IN":s==NativeDevState.NotNow?"ROUND":s==NativeDevState.SandboxOnly?"SBX":s==NativeDevState.Unavailable?"OFF":null;
    }

    /// <summary>The panel's hotkeys (none clashes with a binding in the code or docs; F3 matches the Electron DEV panel key).</summary>
    public static class NativeDevKeys
    {
        public const string Panel="F3",Cursor="F4",RestoreMe="F5",DownDummy="F6",DummyCast="F7",Log="F1",Graph="F2",Fly="F10",HideHud="F11",Snap="Home";
        public static readonly string[] All={Panel,Cursor,RestoreMe,DownDummy,DummyCast,Log,Graph,Fly,HideHud,Snap};
        /// <summary>The footer legend: key, then what it does.</summary>
        public static readonly string[] Legend={Panel,"panel",Cursor,"cursor",RestoreMe,"restore me",DownDummy,"down dummy",DummyCast,"dummy cast",Fly,"fly",HideHud,"hide HUD",Log,"log",Graph,"graph"};
        /// <summary>While flying: WASD move, E / Space up, Q down, Shift fast, Ctrl slow, mouse wheel speed, Home snaps back.</summary>
        public const string FlyHelp="WASD move · E/Space up · Q down · Shift fast · Ctrl slow · wheel speed · Home snap";
    }

    /// <summary>The authority's hit-volume numbers the overlays draw, read from the rules they mirror (DevToolsChecks probes
    /// NativeCombatRules.Headshot and NativeMeleeRules.Contact at these values): the head box (+.64 m over the body, .17 m
    /// half extents, yaw-aligned) and the melee sector (origin +.15 m, ±1.6 m tall, .24 rad plus the target's .45 m).</summary>
    public static class NativeDevVolumes{public const float HeadY=.64f,HeadHalf=.17f,MeleeLift=.15f,MeleeHeight=1.6f,Cone=.24f,PilotExtra=.45f;}

    // ---- The DEV command link (Astra's ConversionGame.SendDevCommand / DevReplyReceived, docs/DEV-ABUSE-AUTHORITY.md) -------------
    public enum NativeDevOutcome{None,Sent,Accepted,Done,Refused,Unknown}
    /// <summary>
    /// The panel's view of the DEV commands it sent. Sender is Astra's ConversionGame.SendDevCommand (it returns the transaction
    /// id and may reply at once: the offline host runs the command synchronously, a guest is refused locally by an older host);
    /// Receive takes DevReplyReceived. Status says only what the authority answered: done, refused with its reason, accepted,
    /// or, after the ten-second timeout, that the outcome is unknown (never undone, never success). The panel draws the effect
    /// from replicated state, not from this line.
    /// </summary>
    public sealed class NativeDevLink
    {
        public const float Timeout=10.5f;public const string TimeoutMarker="outcome is unknown";
        public Func<string,int,string[],string> Sender;
        public bool Available=>Sender!=null;
        private readonly Dictionary<string,string> pending=new Dictionary<string,string>();private readonly Dictionary<string,float> sentAt=new Dictionary<string,float>();
        private string sending;private NativeDevReply early;
        public int Pending=>pending.Count;
        public string Status{get;private set;}="";public NativeDevOutcome Outcome{get;private set;}public string StatusTransaction{get;private set;}
        public bool StatusBad=>Outcome==NativeDevOutcome.Refused||Outcome==NativeDevOutcome.Unknown;
        /// <summary>Sends `action` for seat `target` (0: the sender); the transaction, or null when nothing could be sent.</summary>
        public string Send(string action,int target,string[] args,string label,float now=0)
        {
            if(Sender==null||!NativeDevContract.ShapeOk(action,target,args))return null;label??=action;sending=label;early=null;string t;
            try{t=Sender(action,target,args??Array.Empty<string>());}finally{sending=null;}
            if(t==null)return null;
            if(early!=null&&early.transaction==t){var r=early;early=null;Apply(label,t,r);return t;}
            pending[t]=label;sentAt[t]=now;Status=label+" · sent, waiting for the host";Outcome=NativeDevOutcome.Sent;StatusTransaction=t;return t;
        }
        /// <summary>A reply for a transaction this link sent (false otherwise; the line is unchanged).</summary>
        public bool Receive(NativeDevReply r)
        {
            if(r==null||r.transaction==null)return false;
            if(sending!=null&&!pending.ContainsKey(r.transaction)){early=r;return true;}
            if(!pending.TryGetValue(r.transaction,out var label))return false;
            if(r.state!=NativeDevContract.Accepted){pending.Remove(r.transaction);sentAt.Remove(r.transaction);}
            Apply(label,r.transaction,r);return true;
        }
        /// <summary>The backstop: a command with no reply after Timeout seconds reads as unknown.</summary>
        public void Tick(float now)
        {
            if(sentAt.Count==0)return;string stale=null;foreach(var kv in sentAt)if(now-kv.Value>Timeout){stale=kv.Key;break;}if(stale==null)return;
            string label=pending.TryGetValue(stale,out var l)?l:"DEV command";pending.Remove(stale);sentAt.Remove(stale);
            Status=label+" · outcome unknown: no host reply in 10 s";Outcome=NativeDevOutcome.Unknown;StatusTransaction=stale;
        }
        private void Apply(string label,string transaction,NativeDevReply r)
        {
            string reason=string.IsNullOrEmpty(r.reason)?null:r.reason;bool unknown=r.state==NativeDevContract.Refused&&reason!=null&&reason.IndexOf(TimeoutMarker,StringComparison.OrdinalIgnoreCase)>=0;
            Outcome=unknown?NativeDevOutcome.Unknown:r.state==NativeDevContract.Done?NativeDevOutcome.Done:r.state==NativeDevContract.Accepted?NativeDevOutcome.Accepted:r.state==NativeDevContract.Refused?NativeDevOutcome.Refused:NativeDevOutcome.Unknown;
            Status=label+" · "+(unknown?"outcome unknown: no host reply in 10 s":Outcome==NativeDevOutcome.Done?(reason!=null?"done: "+reason:"done"):Outcome==NativeDevOutcome.Accepted?"accepted, waiting for the result":Outcome==NativeDevOutcome.Refused?"refused: "+(reason??"no reason given"):"unknown reply");
            StatusTransaction=transaction;
        }
        public static string Number(float v)=>v.ToString("0.###",CultureInfo.InvariantCulture);
    }

    // ---- The reveal (NativeSeat.devRevealed) -------------------------------------------------------------------------------
    public static class NativeDevTags
    {
        /// <summary>Whether a pilot's DEV tag shows: the replicated NativeSeat.devRevealed (Astra's reveal command sets it, for
        /// the match), never the access grant.</summary>
        public static bool Revealed(NativeSeat seat)=>seat!=null&&seat.devRevealed;
        public const string Tag="DEV",Banner="A DEVELOPER IS HERE",Indicator="DEV ABUSE ON",NoProgress="NO PROGRESSION THIS MATCH",DevDummy="DEV DUMMY",DevMonster="DEV MONSTER";
    }
    /// <summary>
    /// The "a developer is here" moment everyone sees when a seat's devRevealed turns true (false to true only: a pilot
    /// already revealed when first seen gets the tag, not the moment), or a local sandbox preview. Pure timing and a
    /// deterministic shard burst; the Runtime draws it in the cyber style.
    /// </summary>
    public sealed class NativeDevReveal
    {
        public const float Seconds=3.6f,PreviewTag=6f;public const int Shards=36;
        private readonly Dictionary<int,bool> seen=new Dictionary<int,bool>();
        public int Seat{get;private set;}=-1;public string Name{get;private set;}="";public float At{get;private set;}=float.NaN;public bool Preview{get;private set;}
        public void Reset(){seen.Clear();Seat=-1;Name="";At=float.NaN;Preview=false;}
        public bool Observe(int seat,string name,bool revealed,float now)
        {
            bool had=seen.TryGetValue(seat,out bool was);if(!had||was!=revealed)seen[seat]=revealed;
            if(revealed&&had&&!was){Start(seat,name,now,false);return true;}return false;
        }
        public void Start(int seat,string name,float now,bool preview){Seat=seat;Name=name??"";At=now;Preview=preview;}
        public bool Active(float now)=>!float.IsNaN(At)&&now>=At&&now-At<Seconds;
        public float Age(float now)=>float.IsNaN(At)?-1:now-At;
        /// <summary>The sandbox preview also shows the tag on that pilot for a while (no seat is revealed for real).</summary>
        public bool Previewing(int seat,float now)=>Preview&&seat==Seat&&!float.IsNaN(At)&&now>=At&&now-At<Seconds+PreviewTag;
        // Timeline (seconds from the start): a scan flash, the banner opening from the centre, the eyebrow typed in, the
        // name rising, a hold, then out.
        public static float Flash(float age)=>age<0||age>.32f?0:age<.06f?age/.06f:1-(age-.06f)/.26f;
        public static float Open(float age)=>Ease((age-.08f)/.38f);
        public static float NameIn(float age)=>Ease((age-.55f)/.45f);
        public static float Out(float age)=>1-Ease((age-(Seconds-.5f))/.5f);
        public static float Alpha(float age)=>age<0||age>=Seconds?0:Math.Min(Clamp01(age/.08f),Out(age));
        public static int Typed(float age,int length,bool reduced)=>reduced?length:Math.Max(0,Math.Min(length,(int)Math.Floor((age-.3f)/.035f)));
        /// <summary>A shard of the burst: its offset from the banner centre in canvas units, alpha, size and colour slot (0 cyan,
        /// 1 magenta, 2 violet, 3 ink). None with Reduced effects.</summary>
        public static bool Shard(int i,float age,bool reduced,out float x,out float y,out float alpha,out float size,out int tone)
        {
            x=y=alpha=size=0;tone=0;if(reduced||i<0||i>=Shards)return false;float t=age-.42f-Hash(i,5)*.18f;if(t<0||t>1.7f)return false;
            float angle=(float)(Hash(i,1)*Math.PI*2),speed=260+Hash(i,2)*520,k=1-(float)Math.Pow(1-Math.Min(1,t/1.7f),3);
            x=(float)Math.Cos(angle)*speed*k;y=(float)Math.Sin(angle)*speed*k*.42f+t*t*60;alpha=Clamp01(1-t/1.7f)*Clamp01(t/.06f);size=3+Hash(i,3)*7;tone=(int)(Hash(i,4)*4)%4;return true;
        }
        /// <summary>The marker over the revealed pilot: a ring that pulses out, 0..1.</summary>
        public static float Ring(float age)=>age<0?0:(age*1.25f)%1;
        public static float Hash(int i,int salt){uint h=(uint)(i*73856093)^(uint)(salt*19349663)^0x9e3779b9u;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return (h&0xffffff)/16777216f;}
        private static float Clamp01(float v)=>v<0?0:v>1?1:v;
        private static float Ease(float t){t=Clamp01(t);return 1-(1-t)*(1-t)*(1-t);}
    }

    // ---- Forced hazard pulses (NativeGameFrame.devHazards / NativeArena.DevHazardPulses) ----------------------------------
    /// <summary>
    /// The look of a DEV-forced hazard pulse (hazard-trigger at the hazard's anchor, hazard-at translated to a target): one
    /// burst of the shared effects renderer's hazard cues (NativeImpactCues' saw sparks and spokes, its lava fire, impacts,
    /// beams and lines), keyed by the authored hazard's kind, sized from its volume, at the record's world position. Each
    /// record plays once (its id and simulation time); the scheduled hazard presentation is untouched.
    /// </summary>
    public static class NativeDevHazardLook
    {
        public const int GridColor=0x7ff3ff,GridCore=0x22e6ff,MaglevColor=0xe8fbff,SteamColor=0xdfe8f0,GasColor=0x9cff57,DustColor=0xb2a89b,ShutterColor=0xffc24d,DangerColor=0xff4d6d;
        /// <summary>The reach of a hazard's pulse: its radius, else half its volume's widest side, clamped to .6-6 m.</summary>
        public static float Reach(MapHazard h)
        {
            if(h==null)return 1.5f;float r=h.radius>0?h.radius:Math.Max(h.size.x,Math.Max(h.size.y,h.size.z))*.5f;
            if(!(r>0)&&h.zones!=null)foreach(var z in h.zones)if(z!=null)r=Math.Max(r,Math.Max(z.size.x,z.size.z)*.5f);return Math.Max(.6f,Math.Min(6,r>0?r:1.5f));
        }
        public static int Plan(string kind,V3 at,float reach,NativeImpactCommand[] o)
        {
            int n=0;double r=Math.Max(.6,Math.Min(6,reach));
            switch(kind)
            {
                case "saw-rail":case "saw-slot":
                    n=NativeImpactCues.Burst(o,n,at,NativeImpactCues.SawColor,NativeImpactCues.SawCount,NativeImpactCues.SawPower);n=NativeImpactCues.Burst(o,n,at,NativeImpactCues.SawSparkColor,NativeImpactCues.SawSparkCount,NativeImpactCues.SawSparkPower);
                    for(int k=0;k<6;k++){double a=k*Math.PI/3;n=NativeImpactCues.Beam(o,n,at,new V3((float)(at.x+Math.Cos(a)*r),(float)(at.y+Math.Sin(a)*r*.5),at.z),NativeImpactCues.SawSpokeColor);}break;
                case "press":case "crane":
                    n=NativeImpactCues.Impact(o,n,at,r,DustColor,true);n=NativeImpactCues.Burst(o,n,at,DustColor,NativeImpactCues.BreakCount,NativeImpactCues.HitPower);n=NativeImpactCues.Burst(o,n,at,NativeImpactCues.SawColor,NativeImpactCues.HitCount*2,NativeImpactCues.HitPower);break;
                case "vent":n=NativeImpactCues.Impact(o,n,at,r,SteamColor,true);n=NativeImpactCues.Burst(o,n,at,SteamColor,NativeImpactCues.BreakCount,NativeImpactCues.HitPower*.6);break;
                case "grid":case "maglev":
                    {
                        int c0=kind=="grid"?GridColor:MaglevColor;n=NativeImpactCues.Impact(o,n,at,r,GridCore,true);
                        for(int k=0;k<8;k++){double a=k*Math.PI/4+.3;n=NativeImpactCues.Line(o,n,at,new V3((float)(at.x+Math.Cos(a)*r),at.y+.2f,(float)(at.z+Math.Sin(a)*r)),c0,GridCore,.35,.3);}
                        n=NativeImpactCues.Burst(o,n,at,c0,NativeImpactCues.HitCount*2,NativeImpactCues.HitPower);break;
                    }
                case "burner":n=NativeImpactCues.Burst(o,n,at,NativeImpactCues.LavaColor,NativeImpactCues.LavaCount*2,NativeImpactCues.LavaPower);n=NativeImpactCues.Impact(o,n,at,r,NativeImpactCues.LavaColor,true);break;
                case "gas":n=NativeImpactCues.Impact(o,n,at,r,GasColor,true);n=NativeImpactCues.Burst(o,n,at,GasColor,NativeImpactCues.BreakCount,NativeImpactCues.HitPower*.5);break;
                case "shutter":n=NativeImpactCues.Impact(o,n,at,r,ShutterColor,true);n=NativeImpactCues.Line(o,n,new V3(at.x-(float)r,at.y+1,at.z),new V3(at.x+(float)r,at.y+1,at.z),ShutterColor,MaglevColor,.1,.4);break;
                default:n=NativeImpactCues.Impact(o,n,at,r,DangerColor,true);n=NativeImpactCues.Burst(o,n,at,DangerColor,NativeImpactCues.BreakCount,NativeImpactCues.HitPower);break;
            }
            return Math.Min(n,o.Length);
        }
    }

    // ---- Monsters (proposed contract, Astra to confirm; docs/DEV-TOOLS.md "Monsters") ---------------------------------------
    /// <summary>
    /// The proposed monster commands the panel sends (strings only, through SendDevCommand, so it compiles before the authority
    /// lands): spawn-monster [kind, count, place, behaviour] for the target seat (0: the sender) and clear-monsters with no
    /// arguments. The host clamps to its caps (24 DEV monsters a room, 2 hulks, NativeSpitterRules.Cap spitters) and says in
    /// its reply how many spawned. It advertises devProtocol 2 when it offers them.
    /// </summary>
    public static class NativeDevMonsters
    {
        public const string Spawn="spawn-monster",Clear="clear-monsters";public const int Protocol=2,CountMin=1,CountMax=8,RoomCap=24,HulkCap=2;
        public const float NearMin=4,NearMax=7,AimReach=40,DropHeight=6;
        public static readonly string[] Places={"near","aim","drop","entries"},PlaceNames={"NEAR","AIM","DROP","ENTRIES"};
        public static readonly string[] Behaviours={"hunt","frenzy","idle"},BehaviourNames={"HUNT","FRENZY","IDLE"};
        public static readonly string[] PlaceHelp={"A ring 4-7 m round the target","Where your crosshair hits, up to 40 m","Falls from about 6 m above the target","The map's vent, door, drop and breach entrances nearest the target"};
        public static readonly string[] BehaviourHelp={"Hunts only the target","Goes for the nearest living pilot","Stands until hit, then hunts its attacker"};
        public static bool Proposed(string action)=>action==Spawn||action==Clear;
        public static int SpitterCap=>NativeSpitterRules.Cap;
        /// <summary>The proposed argument shapes: spawn-monster takes a kind id (a content add id, at most 64 characters), a count
        /// of 1-8, a place and a behaviour; clear-monsters takes none.</summary>
        public static bool ArgsOk(string action,string[] args)
        {
            args??=Array.Empty<string>();
            if(action==Clear)return args.Length==0;if(action!=Spawn||args.Length!=4)return false;
            return !string.IsNullOrEmpty(args[0])&&args[0].Length<=NativeDevContract.MaxArgLength&&int.TryParse(args[1],NumberStyles.Integer,CultureInfo.InvariantCulture,out int n)&&n>=CountMin&&n<=CountMax&&Array.IndexOf(Places,args[2])>=0&&Array.IndexOf(Behaviours,args[3])>=0;
        }
        public static int ClampCount(int n)=>Math.Max(CountMin,Math.Min(CountMax,n));
        /// <summary>The kinds the panel offers: the content's add ids (PveContent adds), in their order; none hard-coded.</summary>
        public static string[] Kinds(PveAddStats[] adds){if(adds==null)return Array.Empty<string>();var list=new List<string>(adds.Length);foreach(var a in adds)if(a!=null&&!string.IsNullOrEmpty(a.id))list.Add(a.id);return list.ToArray();}
        /// <summary>A kind's cap for a single spawn (the host's own caps decide; the panel shows them).</summary>
        public static int KindCap(string kind)=>kind=="hulk"?HulkCap:kind=="spitter"?SpitterCap:RoomCap;
        public static string Icon(string kind)=>kind=="shambler"?"footprints":kind=="runner"?"wind":kind=="brute"?"hand-fist":kind=="spitter"?"droplets":kind=="hulk"?"skull":kind=="crawler"?"move":kind=="bloater"?"biohazard":"ghost";
    }
    /// <summary>
    /// The DEV summon-in rift for a monster spawned near, at the aim point or dropped (entry spawns keep their own door, vent,
    /// drop and breach animations): a magenta glitch rift in the DEV reveal's style on the shared effects renderer's cues: a
    /// ground ring, three tall beams (magenta, cyan, violet), glitch streaks up the column and two spark bursts. Reduced
    /// effects keep the ring, one beam and a smaller burst.
    /// </summary>
    public static class NativeDevMonsterLook
    {
        public const int Magenta=0xff3dcf,Cyan=0x22e6ff,CyanSoft=0x7ff3ff,Violet=0x8b5cff;
        public const float Seconds=.9f,FreshAge=.6f;
        public static int Rift(V3 at,float height,bool reduced,NativeImpactCommand[] o)
        {
            float h=Math.Max(1.2f,Math.Min(6,height));int n=0;var foot=new V3(at.x,at.y,at.z);
            n=NativeImpactCues.Impact(o,n,foot,1.3,Magenta,true);
            n=NativeImpactCues.Beam(o,n,foot,new V3(at.x,at.y+h,at.z),Magenta);
            if(reduced)return Math.Min(o.Length,NativeImpactCues.Burst(o,n,new V3(at.x,at.y+h*.5f,at.z),Magenta,8,2));
            n=NativeImpactCues.Beam(o,n,new V3(at.x+.18f,at.y,at.z-.1f),new V3(at.x+.18f,at.y+h*.9f,at.z-.1f),Cyan);
            n=NativeImpactCues.Beam(o,n,new V3(at.x-.16f,at.y,at.z+.12f),new V3(at.x-.16f,at.y+h*.8f,at.z+.12f),Violet);
            for(int k=0;k<6;k++)
            {
                float y=at.y+h*(.12f+k*.15f),w=.45f+NativeDevReveal.Hash(k,9)*.6f,dx=(NativeDevReveal.Hash(k,11)-.5f)*.4f;
                n=NativeImpactCues.Line(o,n,new V3(at.x-w+dx,y,at.z),new V3(at.x+w+dx,y,at.z),k%2==0?Magenta:CyanSoft,Cyan,.08,.22+k*.03);
            }
            n=NativeImpactCues.Burst(o,n,new V3(at.x,at.y+h*.5f,at.z),Magenta,18,3);n=NativeImpactCues.Burst(o,n,new V3(at.x,at.y+h*.3f,at.z),CyanSoft,10,2);
            return Math.Min(n,o.Length);
        }
    }
    /// <summary>When each DEV monster was first seen (the summon moment), by minion id; a new arena or round starts over.
    /// Allocation-free once an id is known.</summary>
    public sealed class NativeDevMonsterSeen
    {
        private readonly Dictionary<int,float> first=new Dictionary<int,float>();private int generation=int.MinValue;
        public int Count=>first.Count;
        /// <summary>True the first time a DEV monster is seen (its summon moment starts at `now`).</summary>
        public bool Observe(int generationNow,int id,bool dev,float now)
        {
            if(generationNow!=generation){generation=generationNow;first.Clear();}
            if(!dev||first.ContainsKey(id))return false;if(first.Count>=256)first.Clear();first[id]=now;return true;
        }
        public float Age(int id,float now)=>first.TryGetValue(id,out float t)?now-t:-1;
        public void Clear(){first.Clear();generation=int.MinValue;}
    }

    // ---- Event log -----------------------------------------------------------------------------------------------------------
    [Flags] public enum NativeDevLogFilter{None=0,Damage=1,Deaths=2,Defense=4,Abilities=8,World=16,Pve=32,Heals=64,All=127}
    public struct NativeDevLogEntry{public int Id,Source,Target;public EventKind Kind;public NativeDevLogFilter Category;public float Time,Amount;public string Text;}
    /// <summary>The replicated event stream (arena.Events, host or guest) as a ring of formatted lines. A line's text is built
    /// once, when its event arrives; drawing and filtering allocate nothing. Read-only.</summary>
    public sealed class NativeDevLog
    {
        public const int Capacity=96;
        public static readonly NativeDevLogFilter[] Filters={NativeDevLogFilter.Damage,NativeDevLogFilter.Deaths,NativeDevLogFilter.Defense,NativeDevLogFilter.Abilities,NativeDevLogFilter.World,NativeDevLogFilter.Pve,NativeDevLogFilter.Heals};
        public static readonly string[] FilterNames={"DAMAGE","DEATHS","DEFENSE","ABILITIES","WORLD","PVE","HEALS"};
        private readonly NativeDevLogEntry[] ring=new NativeDevLogEntry[Capacity];private int head,count,last,generation=int.MinValue;
        private static string[] kindNames;
        public int Count=>count;
        /// <summary>The `index`th newest line (0 the newest).</summary>
        public NativeDevLogEntry this[int index]=>ring[((head-1-index)%Capacity+Capacity)%Capacity];
        public void Clear(){head=count=0;last=0;}
        public static NativeDevLogFilter Category(EventKind k)
        {
            switch(k)
            {
                case EventKind.Hit:case EventKind.Headshot:case EventKind.Critical:case EventKind.BurnTick:case EventKind.PoisonTick:case EventKind.HazardHit:case EventKind.AbilityHit:case EventKind.Explosion:case EventKind.Arc:return NativeDevLogFilter.Damage;
                case EventKind.Death:case EventKind.PveGib:return NativeDevLogFilter.Deaths;
                case EventKind.Block:case EventKind.Dodge:case EventKind.BlockPulse:case EventKind.Absorb:case EventKind.Catch:return NativeDevLogFilter.Defense;
                case EventKind.AbilityCast:case EventKind.Recast:case EventKind.Summon:case EventKind.Field:case EventKind.Teleport:case EventKind.Freeze:case EventKind.Mark:case EventKind.Leet:case EventKind.Emperor:case EventKind.Arcade:case EventKind.Siren:case EventKind.Vacuum:case EventKind.FissureStart:case EventKind.Split:return NativeDevLogFilter.Abilities;
                case EventKind.PveBuy:case EventKind.PvePhase:case EventKind.PveRevive:case EventKind.PveMoney:case EventKind.Install:return NativeDevLogFilter.Pve;
                case EventKind.Heal:return NativeDevLogFilter.Heals;
                default:return NativeDevLogFilter.World;
            }
        }
        public static string KindName(EventKind k)
        {
            if(kindNames==null){var values=(EventKind[])Enum.GetValues(typeof(EventKind));int max=0;foreach(var v in values)max=Math.Max(max,(int)v);var names=new string[max+1];foreach(var v in values)names[(int)v]=v.ToString().ToUpperInvariant();kindNames=names;}
            int i=(int)k;return i>=0&&i<kindNames.Length&&kindNames[i]!=null?kindNames[i]:"EVENT";
        }
        /// <summary>Records the events newer than the last one seen (a new round or a reset stream starts over). `name` turns a
        /// seat id into a name (0 or unknown: the world). Returns how many lines were added.</summary>
        public int Observe(IReadOnlyList<ArenaEvent> events,int gen,Func<int,string> name)
        {
            if(events==null)return 0;if(gen!=generation){generation=gen;last=0;}
            int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;if(newest>0&&newest<last)last=0;
            int added=0;for(int i=0;i<events.Count;i++){var e=events[i];if(e.Id<=last)continue;Add(e,name);added++;}
            if(newest>last)last=newest;return added;
        }
        private void Add(ArenaEvent e,Func<int,string> name)
        {
            var entry=new NativeDevLogEntry{Id=e.Id,Source=e.Source,Target=e.Target,Kind=e.Kind,Category=Category(e.Kind),Time=e.Time,Amount=e.Amount};
            string who=e.Source!=0?name?.Invoke(e.Source)??("#"+e.Source):"world",whom=e.Target!=0&&e.Target!=e.Source?name?.Invoke(e.Target)??("#"+e.Target):null;
            entry.Text=e.Time.ToString("0.0",CultureInfo.InvariantCulture).PadLeft(6)+"  "+KindName(e.Kind)+"  "+who+(whom!=null?" → "+whom:"")+(e.Amount!=0?"  "+e.Amount.ToString("0.#",CultureInfo.InvariantCulture):"")+(string.IsNullOrEmpty(e.Detail)?"":"  · "+e.Detail);
            ring[head]=entry;head=(head+1)%Capacity;if(count<Capacity)count++;
        }
        /// <summary>Whether a line passes the filter (and, with `mine`, involves seat `self`).</summary>
        public static bool Shows(in NativeDevLogEntry e,NativeDevLogFilter filter,bool mine,int self)=>(e.Category&filter)!=0&&(!mine||e.Source==self||e.Target==self);
    }

    /// <summary>A fixed ring of samples (frame time, simulation step, round trip) for the DEV graph; allocation-free.</summary>
    public sealed class NativeDevGraph
    {
        private readonly float[] values;private int head,count;
        public NativeDevGraph(int capacity=120){values=new float[Math.Max(2,capacity)];}
        public int Capacity=>values.Length;public int Count=>count;
        public void Push(float v){if(float.IsNaN(v)||float.IsInfinity(v))v=0;values[head]=v;head=(head+1)%values.Length;if(count<values.Length)count++;}
        /// <summary>Sample `i` from the oldest (0) to the newest (Count-1).</summary>
        public float At(int i)=>values[((head-count+i)%values.Length+values.Length)%values.Length];
        public float Last=>count==0?0:At(count-1);
        public float Max(){float m=0;for(int i=0;i<count;i++)m=Math.Max(m,At(i));return m;}
        public float Average(){if(count==0)return 0;float s=0;for(int i=0;i<count;i++)s+=At(i);return s/count;}
        public void Clear(){head=count=0;}
    }

    /// <summary>The detached fly camera: its own pose, moved by the local keys and mouse only. Pitch is the camera's x Euler
    /// angle (down positive) and yaw its y, as the match camera's; the look mapping is the pilot camera's (x2 / x1.5 by the
    /// mouse sensitivity). It never reads or writes the simulation.</summary>
    public sealed class NativeDevFly
    {
        public const float MinSpeed=1,MaxSpeed=60,PitchLimit=89,FastScale=3,SlowScale=.25f;
        public bool On;public float X,Y,Z,Yaw,Pitch,Speed=10;
        public void Place(float x,float y,float z,float yaw,float pitch){X=x;Y=y;Z=z;Yaw=yaw;Pitch=Clamp(pitch,-PitchLimit,PitchLimit);}
        public void Look(float mouseX,float mouseY,float sensitivity){Yaw=(Yaw+mouseX*2*sensitivity)%360;Pitch=Clamp(Pitch-mouseY*1.5f*sensitivity,-PitchLimit,PitchLimit);}
        public void Wheel(float notches){if(notches!=0)Speed=Clamp(Speed*(float)Math.Pow(1.15,notches),MinSpeed,MaxSpeed);}
        /// <summary>right / up / forward in -1..1 (forward along the view, up world-up), for `dt` seconds.</summary>
        public void Move(float right,float up,float forward,bool fast,bool slow,float dt)
        {
            if(!(dt>0))return;dt=Math.Min(dt,.1f);float v=Clamp(Speed,MinSpeed,MaxSpeed)*(fast?FastScale:1)*(slow?SlowScale:1)*dt;
            Forward(Yaw,Pitch,out float fx,out float fy,out float fz);double r=Yaw*Math.PI/180;float rx=(float)Math.Cos(r),rz=-(float)Math.Sin(r);
            float len=(float)Math.Sqrt(right*right+up*up+forward*forward);if(len>1){right/=len;up/=len;forward/=len;}
            X+=(fx*forward+rx*right)*v;Y+=(fy*forward+up)*v;Z+=(fz*forward+rz*right)*v;
        }
        public static void Forward(float yaw,float pitch,out float x,out float y,out float z)
        {double a=yaw*Math.PI/180,b=pitch*Math.PI/180;x=(float)(Math.Sin(a)*Math.Cos(b));y=-(float)Math.Sin(b);z=(float)(Math.Cos(a)*Math.Cos(b));}
        private static float Clamp(float v,float lo,float hi)=>v<lo?lo:v>hi?hi:v;
    }

    /// <summary>The Chips tab's query: search terms, sort, filter (all, favourites, installed, recent) over the content's chips;
    /// recomputed only when an input changes, so a steady frame allocates nothing.</summary>
    public sealed class NativeDevChips
    {
        public const int SortRarity=0,SortName=1,SortInstalled=2,FilterAll=0,FilterFavourites=1,FilterInstalled=2,FilterRecent=3,RecentMax=8;
        public static readonly string[] SortNames={"RARITY","NAME","INSTALLED"},FilterNames={"ALL","FAVOURITES","INSTALLED","RECENT"};
        private static readonly string[] Ranks={"common","uncommon","rare","epic","legendary","mythic","mishima","ansatsuken","l44t","siren"};
        public readonly List<int> Shown=new List<int>(256);public readonly HashSet<string> Favourites=new HashSet<string>(StringComparer.Ordinal);public readonly List<string> Recent=new List<string>(RecentMax);
        public string Search="";public int Sort,Filter;private int revision;
        private string keySearch;private int keySort=-1,keyFilter=-1,keyRevision=-1,keyInstalled=int.MinValue,keyLength=-1;private ContentChip[] keyChips;
        private ContentChip[] sortChips;private Func<string,int> sortInstalled;private Comparison<int> comparison;
        public static int Rank(string rarity){int r=Array.IndexOf(Ranks,rarity);return r<0?Ranks.Length:r;}
        public void Favourite(string id){if(id==null)return;if(!Favourites.Remove(id))Favourites.Add(id);revision++;}
        public void Touch(string id){if(id==null)return;Recent.Remove(id);Recent.Insert(0,id);if(Recent.Count>RecentMax)Recent.RemoveAt(Recent.Count-1);revision++;}
        public string FavouritesText(){var list=new List<string>(Favourites);list.Sort(StringComparer.Ordinal);return string.Join(",",list);}
        public void LoadFavourites(string text){Favourites.Clear();if(!string.IsNullOrEmpty(text))foreach(var id in text.Split(','))if(id.Length>0&&id.Length<=64)Favourites.Add(id);revision++;}
        /// <summary>Refreshes Shown when the query or the target's chips (`installedSignature`) changed; true when it did.</summary>
        public bool Update(ContentChip[] chips,Func<string,int> installed,int installedSignature)
        {
            chips=chips??Array.Empty<ContentChip>();
            if(ReferenceEquals(keySearch,Search)&&keySort==Sort&&keyFilter==Filter&&keyRevision==revision&&keyInstalled==installedSignature&&keyLength==chips.Length&&ReferenceEquals(keyChips,chips))return false;
            keySearch=Search;keySort=Sort;keyFilter=Filter;keyRevision=revision;keyInstalled=installedSignature;keyLength=chips.Length;keyChips=chips;
            Shown.Clear();var terms=(Search??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            for(int i=0;i<chips.Length;i++)
            {
                var c=chips[i];if(c==null)continue;bool ok=true;
                foreach(var term in terms)if((c.name+" "+c.id+" "+c.description+" "+c.rarity).IndexOf(term,StringComparison.OrdinalIgnoreCase)<0){ok=false;break;}
                if(!ok)continue;
                if(Filter==FilterFavourites&&!Favourites.Contains(c.id)||Filter==FilterInstalled&&(installed==null||installed(c.id)<=0)||Filter==FilterRecent&&!Recent.Contains(c.id))continue;
                Shown.Add(i);
            }
            sortChips=chips;sortInstalled=installed;comparison??=Compare;recentComparison??=CompareRecent;
            Shown.Sort(Filter==FilterRecent?recentComparison:comparison);
            return true;
        }
        private Comparison<int> recentComparison;
        private int CompareRecent(int a,int b)=>Recent.IndexOf(sortChips[a].id).CompareTo(Recent.IndexOf(sortChips[b].id));
        private int Compare(int a,int b)
        {
            var x=sortChips[a];var y=sortChips[b];int r=0;
            if(Sort==SortRarity)r=Rank(x.rarity).CompareTo(Rank(y.rarity));
            else if(Sort==SortInstalled&&sortInstalled!=null)r=-Math.Sign(sortInstalled(x.id)).CompareTo(Math.Sign(sortInstalled(y.id)));
            if(r==0)r=string.CompareOrdinal(x.name??"",y.name??"");return r!=0?r:a.CompareTo(b);
        }
    }

    /// <summary>The DEV panel's frame on the canvas (NativeUiMotion.Measure units): right-anchored, at most 720 wide, so a
    /// 21:9 or 32:9 window keeps the fight in view; it fits a 1066x600 canvas (interface scale 1.5 at 1080p).</summary>
    public struct NativeDevRects{public float X,Y,W,H,Rail,Header,Footer;public float BodyX=>X+Rail;public float BodyY=>Y+Header;public float BodyW=>W-Rail;public float BodyH=>H-Header-Footer;}
    public static class NativeDevLayout
    {
        public const float Rail=84,Header=62,Footer=72,Top=60,Margin=18;
        public static NativeDevRects Panel(float canvasW,float canvasH)
        {
            float w=Math.Max(420,Math.Min(720,canvasW*.36f));w=Math.Min(w,canvasW-2*Margin);float top=Math.Min(Top,canvasH*.08f),h=Math.Min(canvasH-top-Margin,860);
            return new NativeDevRects{X=canvasW-Margin-w,Y=top,W=w,H=h,Rail=Rail,Header=Header,Footer=Footer};
        }
        public const int Tabs=10;
        public static readonly string[] TabNames={"PILOT","INTERACT","MONSTERS","DUMMIES","CHIPS","ARENA","HAZARDS","PREVIEW","CAMERA","SIM"};
        public static readonly string[] TabTitles={"Pilot","Interact","Monsters","Dummies","Chips","Arena","Hazards","Previews & galleries","Camera & overlays","Simulation"};
        public static readonly string[] TabIcons={"person-standing","hand","biohazard","bot","cpu","brick-wall","radiation","sparkles","scan","gauge"};
    }

    /// <summary>Cached number labels for the panel (a steady frame builds no strings).</summary>
    public static class NativeDevText
    {
        private static readonly string[] Units={""," kg"," m/s"," s","×"," ms"," fps"," m"," kB"};
        private static readonly Dictionary<long,string> cache=new Dictionary<long,string>();
        private static void Keep(long key,string s){if(cache.Count>8192)cache.Clear();cache[key]=s;}
        public static string Int(int n)
        {
            if(n>=0&&n<1000)return NativeHudText.Int(n);long key=((long)1<<40)|(uint)n;if(cache.TryGetValue(key,out var s))return s;
            s=n.ToString(CultureInfo.InvariantCulture);Keep(key,s);return s;
        }
        /// <summary>`v` with `decimals` (0..2) places and a unit suffix (one of Units), keyed by the rounded value.</summary>
        public static string Value(float v,int decimals,string unit)
        {
            if(float.IsNaN(v)||float.IsInfinity(v))v=0;decimals=Math.Max(0,Math.Min(2,decimals));long scaled=(long)Math.Round(v*(decimals==0?1:decimals==1?10:100));
            int u=Array.IndexOf(Units,unit);if(u<0)u=0;long key=((long)(2+decimals)<<40)|((long)u<<32)|(uint)(int)scaled;if(cache.TryGetValue(key,out var s))return s;
            s=(scaled/(decimals==0?1.0:decimals==1?10.0:100.0)).ToString(decimals==0?"0":decimals==1?"0.0":"0.00",CultureInfo.InvariantCulture)+Units[u];Keep(key,s);return s;
        }
        public static string Count(int n,int max)
        {
            n=Math.Max(0,Math.Min(65535,n));max=Math.Max(0,Math.Min(65535,max));long key=((long)9<<40)|((long)n<<16)|(uint)max;if(cache.TryGetValue(key,out var s))return s;
            s=n.ToString(CultureInfo.InvariantCulture)+"/"+max.ToString(CultureInfo.InvariantCulture);Keep(key,s);return s;
        }
    }
}
