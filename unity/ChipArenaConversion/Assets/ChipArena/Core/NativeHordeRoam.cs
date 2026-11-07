using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition town zombies' roaming states as the presentation draws them (owner 2026-10-15: "Monsters outside of the hub
    /// should walk around, every now and then stand idle for moments. Only start chasing the player when they detect them (Either
    /// through vision or audio)"; docs/EXPEDITION-HUB.md "Town zombie roaming and perception"). Mapped from Astra's replicated
    /// HubZombieState (NativeMinionFrame.hubState) in one place (Runtime NativeHordeRoamRecord.Read). None: no record (instance
    /// hordes, DEV monsters, the final encounter, an add still coming in): the presentation infers it from how the add moves.
    /// </summary>
    public enum HordeRoamState:byte{None=0,Wander=1,Idle=2,Alert=3,Chase=4,Search=5}

    /// <summary>
    /// The host's roaming record as the presentation reads it (arena seconds, like LastAttack): the state; when it began (`At`,
    /// NaN: not replicated, so each peer starts it on the frame it sees the change); when the latest alert began (`AlertAt`,
    /// below -50 none: it stays through the chase, so the snap, the scream and the burst replay from it on every peer); the look
    /// direction (`LookYaw`, Unity degrees, +Z = 0: what it noticed while alerted).
    /// </summary>
    public struct HordeRoamWire{public HordeRoamState State;public float At,AlertAt;public bool HasLookYaw;public float LookYaw;}

    /// <summary>
    /// The roaming read one add's pose takes this frame (<see cref="NativeHordeRoamTrack.Read"/>). Off (`On` false, the default)
    /// the gait is exactly today's. Every field is a function of the state's replicated start times and the clock (inferred: of
    /// the add's motion), so host, guests and the killcam draw the same reaction.
    /// </summary>
    public struct HordeRoamPose
    {
        public bool On,Inferred;public HordeRoamState State;
        /// <summary>Seconds in the state; since the notice began (NaN: no notice showing); since the chase broke out of a notice
        /// (NaN: none showing).</summary>
        public float Age,AlertAge,BurstAge;
        /// <summary>The calm states' weights (0..1), eased over <see cref="NativeHordeRoam.Blend"/> from each change.</summary>
        public float Wander,Idle,Search;
        /// <summary>Not hunting: the wander takes, no alert stance, no head on the nearest pilot.</summary>
        public float Calm=>Wander+Idle+Search;
        /// <summary>The record's look direction (Unity degrees, +Z = 0), when it gives one.</summary>
        public bool HasLookYaw;public float LookYaw;
    }

    /// <summary>
    /// The numbers the roaming presentation follows and its pure timing curves (presentation only: nothing here reads or writes
    /// the authority). The behaviour's own numbers are Astra's (the contract in docs/EXPEDITION-HUB.md); the few mirrored here
    /// (<see cref="ReactDelay"/>, <see cref="WanderSpeed"/>) only shape timing and the inference bands.
    /// </summary>
    public static class NativeHordeRoam
    {
        // ---- the authority's numbers the presentation mirrors (Core/NativeHubPerception, Runtime/NativeHubPerceptionRuntime) ----
        /// <summary>The host's alert: the add stands and turns for a host-random .4-.8 s, then chases.</summary>
        public const float AlertMin=.4f,AlertMax=.8f;
        /// <summary>The host's wander pace for a kind's content walk (m/s): min(1.1, walk x .55).</summary>
        public static float WanderSpeed(float walk)=>Math.Min(1.1f,walk*.55f);

        // ---- inference with no record: ground-speed bands with hysteresis and holds ----
        /// <summary>Idle below <see cref="IdleBelow"/>; moving again above <see cref="MoveAbove"/>; a chase above
        /// <see cref="ChaseAbove"/> (every hub kind's authority walk is 1.3 m/s or more), which
        /// settles to a wander below <see cref="ChaseBelow"/> held <see cref="ChaseHold"/> s, or to an idle when it stands
        /// <see cref="StopHold"/> s. An add that attacked within <see cref="EngagedFor"/> s stays in the chase (it stands to
        /// strike). A chase after <see cref="CalmBefore"/> s or more of calm reads as a notice.</summary>
        public const float IdleBelow=.18f,MoveAbove=.32f,ChaseAbove=1.15f,ChaseBelow=.95f,ChaseHold=.8f,StopHold=1.5f,EngagedFor=3f,CalmBefore=1f;

        // ---- presentation timing ----
        /// <summary>A state's blend in (the previous one blends out over the same).</summary>
        public const float Blend=.5f;
        /// <summary>The alert: the head snaps toward what it noticed in <see cref="Snap"/> s, the chest turns with it over
        /// <see cref="Turn"/> s; the scream starts at <see cref="ScreamAt"/> and lasts <see cref="ScreamSeconds"/>; the burst
        /// into the chase leans the trunk for <see cref="BurstSeconds"/>; the notice layer is over by <see cref="NoticeSeconds"/>.</summary>
        public const float Snap=.14f,Turn=.45f,ScreamAt=.2f,ScreamSeconds=.95f,BurstSeconds=.6f,NoticeSeconds=1.7f;

        // ---- idle variants ----
        public const int Sway=0,Twitch=1,LookAround=2,Slump=3,Rock=4,IdleVariants=5;
        public static readonly string[] VariantNames={"sway","twitch","look around","slump","rock"};
        /// <summary>Each add's idle is a schedule of slots (<see cref="VariantPeriod"/> s each), crossfading over
        /// <see cref="VariantEdge"/> s, with a per-add phase so a crowd is never in step. Every five slots are the five variants in
        /// an order hashed from the add's seed (never one twice in a row), so each add shows them all within half a minute.</summary>
        public const float VariantEdge=.8f;
        public static float VariantPeriod(uint seed)=>4.5f+3f*H(seed,1);
        public static int Variant(uint seed,int slot)
        {
            int c=(int)Math.Floor(slot/(double)IdleVariants),j=slot-c*IdleVariants;
            Span<int> order=stackalloc int[IdleVariants],before=stackalloc int[IdleVariants];Order(seed,c,order);Order(seed,c-1,before);
            if(order[0]==before[IdleVariants-1]){int x=order[0];order[0]=order[1];order[1]=x;}
            return order[j];
        }
        // A hashed shuffle of the variants for cycle `c` (Fisher-Yates on the seed's hash).
        private static void Order(uint seed,int c,Span<int> o)
        {
            for(int i=0;i<o.Length;i++)o[i]=i;
            for(int i=o.Length-1;i>0;i--){int k=Math.Min(i,(int)(H(seed,c*8+i,11)*(i+1)));int x=o[i];o[i]=o[k];o[k]=x;}
        }
        /// <summary>The idle variants' weights at `time` into `w` (IdleVariants long, summing to 1); `age` the slot's seconds.</summary>
        public static void Variants(uint seed,float time,float[] w,out float age)
        {
            for(int i=0;i<IdleVariants;i++)w[i]=0;
            float period=VariantPeriod(seed),k=time/period+H(seed,2);int n=(int)Math.Floor(k);age=(k-n)*period;
            float x=Sm(age/VariantEdge);w[Variant(seed,n)]+=x;w[Variant(seed,n-1)]+=1-x;
        }

        // ---- voice (NativeAudio's hub zombie cues) ----
        /// <summary>Moans: sparse and quiet, a pure function of the add's seed and the clock (every peer moans at the same moments):
        /// one per slot of 7-13 s, three slots in ten.</summary>
        public const float MoanGap=.9f,MoanRange=24f,MoanGain=.3f,ShriekGain=.8f;
        public static float MoanPeriod(uint seed)=>7f+6f*H(seed,30);
        /// <summary>When the moan of the slot holding `time` starts (NaN: this slot is silent).</summary>
        public static float MoanAt(uint seed,float time)
        {
            float period=MoanPeriod(seed),k=time/period+H(seed,31);int n=(int)Math.Floor(k);if(H(seed,n,32)<.3f)return float.NaN;
            return (n-H(seed,31))*period+H(seed,n,33)*(period-1.5f);
        }
        /// <summary>The moan's pitch (a crowd never moans in unison).</summary>
        public static float MoanRate(uint seed,int kind)=>(kind==NativeHordeKinds.Brute?.86f:kind==NativeHordeKinds.Runner?1.06f:kind==NativeHordeKinds.Bloater?.82f:1f)*(.92f+.16f*H(seed,34));
        /// <summary>The shriek of a notice that began at `alertAt` (host clock): with the scream's opening.</summary>
        public static float ShriekAt(float alertAt)=>alertAt+ScreamAt;

        /// <summary>
        /// The mapping from the host's record (Astra's HubZombieView, NativePveAdd.Hub; null for adds without it) to the read the
        /// pose takes: the state one for one (the host's alert is the presentation's alert), the latest alert's start and the
        /// look direction. The state's start is not replicated (At NaN: each peer starts it when it sees it).
        /// </summary>
        public static HordeRoamWire FromHub(HubZombieView h)
        {
            if(h==null)return default;HordeRoamState s;
            switch(h.State)
            {
                case HubZombieState.Wander:s=HordeRoamState.Wander;break;case HubZombieState.Idle:s=HordeRoamState.Idle;break;
                case HubZombieState.Alert:s=HordeRoamState.Alert;break;case HubZombieState.Chase:s=HordeRoamState.Chase;break;
                case HubZombieState.Search:s=HordeRoamState.Search;break;default:return default;
            }
            return new HordeRoamWire{State=s,At=float.NaN,AlertAt=h.AlertAt,HasLookYaw=true,LookYaw=h.LookYaw};
        }

        // ---- curves ----
        /// <summary>Whether a state is calm (no hunt: wander, idle, search).</summary>
        public static bool Calm(HordeRoamState s)=>s==HordeRoamState.Wander||s==HordeRoamState.Idle||s==HordeRoamState.Search;
        /// <summary>The head's snap toward what it noticed (0..1, a small overshoot at the snap's end), held through the scream,
        /// handed back to the chase's tracking by <see cref="NoticeSeconds"/>.</summary>
        public static float SnapWeight(float alertAge)
        {
            if(float.IsNaN(alertAge)||alertAge<0||alertAge>=NoticeSeconds)return 0;
            float a=alertAge,s=Sm(a/Snap),over=a<Snap?0:.16f*(float)Math.Sin(Math.Min(1,(a-Snap)/.22f)*Math.PI)*(1-Sm((a-Snap)/.22f));
            return (s+over)*(1-Sm((a-(NoticeSeconds-.5f))/.5f));
        }
        /// <summary>The scream's weight over the body (0..1): in over .15 s from <see cref="ScreamAt"/>, held, out over .35 s.</summary>
        public static float Scream(float alertAge)
        {
            if(float.IsNaN(alertAge))return 0;float a=alertAge-ScreamAt;if(a<-.05f||a>=ScreamSeconds)return 0;
            return Sm((a+.05f)/.2f)*(1-Sm((a-(ScreamSeconds-.35f))/.35f));
        }
        /// <summary>The burst into the chase (0..1): the trunk thrown forward over .12 s, recovering by <see cref="BurstSeconds"/>.</summary>
        public static float Burst(float burstAge)=>float.IsNaN(burstAge)||burstAge<0||burstAge>=BurstSeconds?0:Sm(burstAge/.12f)*(1-Sm((burstAge-.2f)/(BurstSeconds-.2f)));
        /// <summary>The chest's turn toward what it noticed (0..1), following the head.</summary>
        public static float TurnWeight(float alertAge)=>float.IsNaN(alertAge)||alertAge<0||alertAge>=NoticeSeconds?0:Sm((alertAge-.04f)/Turn)*(1-Sm((alertAge-(NoticeSeconds-.6f))/.6f));
        /// <summary>A pulse (0..1) at hashed times: one in each `period` s slot, at a hashed moment, rising over `rise` s, held
        /// `hold`, falling over `fall`; `side` the event's hashed sign (-1 or 1). A pure function of the seed and the clock.</summary>
        public static float Pulse(uint seed,int salt,float time,float period,float rise,float hold,float fall,out float side)
        {
            float k=time/period+H(seed,salt);int n=(int)Math.Floor(k);float u=(k-n)*period,span=rise+hold+fall;
            float start=H(seed,n,salt+1)*Math.Max(0,period-span);side=H(seed,n,salt+2)<.5f?-1:1;
            float a=u-start;if(a<0||a>span)return 0;return a<rise?Sm(a/rise):a<rise+hold?1:1-Sm((a-rise-hold)/fall);
        }
        /// <summary>A held look target (rad yaw and pitch about the body) that changes every `period` s and eases there over .45 s:
        /// the idle's look around, the wander's glances and the search's scan. `wide` the yaw's reach, `pitch` the tilt's.</summary>
        public static void Scan(uint seed,int salt,float time,float period,float wide,float pitch,out float yaw,out float tilt)
        {
            float k=time/period+H(seed,salt);int n=(int)Math.Floor(k);float u=(k-n)*period,x=Sm(u/.45f);
            float y0=(H(seed,n-1,salt+3)*2-1)*wide,y1=(H(seed,n,salt+3)*2-1)*wide,p0=(H(seed,n-1,salt+4)*2-1)*pitch,p1=(H(seed,n,salt+4)*2-1)*pitch;
            yaw=y0+(y1-y0)*x;tilt=p0+(p1-p0)*x;
        }
        private static float Sm(float x)=>MeleeMath.Smooth(x);
        private static float H(uint seed,int k)=>NativeHordeKinds.Hash(seed,900+k);
        private static float H(uint seed,int n,int salt)=>(float)NativeGoreRules.Hash01(seed,n,0x7a0+salt);
    }

    /// <summary>
    /// One add's roaming read, frame to frame (presentation only; no allocation; reset when the add changes, the clock rewinds or
    /// a frame is missed by more than half a second). With the host's record it maps the record to the pose's weights and
    /// reaction ages; without it, it infers the state from the add's ground speed (the authority's velocity, the same input on
    /// every peer): standing reads as idle, a slow shamble as a wander, the authority's walk and run as a chase, and a chase
    /// after a calm second as a notice (its head snap, scream and burst play as it sets off).
    /// </summary>
    public sealed class NativeHordeRoamTrack
    {
        private int id=int.MinValue;private float last=float.NaN;
        private HordeRoamState cur,prev,inferred;private float curAt,inferredAt,calmSince=float.NaN,slowSince=float.NaN,stillSince=float.NaN,noticeAt=float.NaN;
        /// <summary>The presented state's last change (the record's, or the inference's) and the state before it.</summary>
        public HordeRoamState State=>cur;public HordeRoamState Previous=>prev;
        public void Reset(){id=int.MinValue;last=float.NaN;cur=prev=inferred=HordeRoamState.None;curAt=inferredAt=0;calmSince=slowSince=stillSince=noticeAt=float.NaN;}
        /// <summary>
        /// The pose's roaming read at `now`: off unless `on`; the host's `wire` when it carries a state, else the inference from
        /// `speed` (horizontal m/s) with `engaged` (it attacked within <see cref="NativeHordeRoam.EngagedFor"/> s, or a pilot is
        /// within its reach). `roams` lets the inference read a wander and an alert (a roaming population); off, it only tells
        /// standing apart from hunting (an instance horde slowed by a limp or a frost field still reads as a chase).
        /// </summary>
        public HordeRoamPose Read(bool on,in HordeRoamWire wire,int add,float now,float speed,bool engaged,bool roams=true)
        {
            if(!on){if(id!=int.MinValue)Reset();return default;}
            bool fresh=add!=id||float.IsNaN(last)||now<last-.001f||now-last>.5f;if(fresh){Reset();id=add;}last=now;
            bool inferredRead=wire.State==HordeRoamState.None;HordeRoamState state;float at;
            if(inferredRead){Infer(now,roams?speed:speed>NativeHordeRoam.IdleBelow?Math.Max(speed,NativeHordeRoam.ChaseAbove+.01f):speed,engaged,fresh);if(!roams)noticeAt=float.NaN;state=inferred;at=inferredAt;}
            else{state=wire.State;at=float.IsNaN(wire.At)?(state!=cur||fresh?now:curAt):wire.At;}
            if(state!=cur){prev=fresh?HordeRoamState.None:cur;cur=state;curAt=at;}else curAt=at;
            var r=new HordeRoamPose{On=true,Inferred=inferredRead,State=state,Age=Math.Max(0,now-at),AlertAge=float.NaN,BurstAge=float.NaN};
            float k=prev==HordeRoamState.None?1:Sm((now-curAt)/NativeHordeRoam.Blend),j=1-k;
            r.Wander=(cur==HordeRoamState.Wander?k:0)+(prev==HordeRoamState.Wander?j:0);
            r.Idle=(cur==HordeRoamState.Idle?k:0)+(prev==HordeRoamState.Idle?j:0);
            r.Search=(cur==HordeRoamState.Search?k:0)+(prev==HordeRoamState.Search?j:0);
            if(inferredRead)
            {
                // The inferred notice is the burst itself: the authority set off at once.
                if(state==HordeRoamState.Chase&&!float.IsNaN(noticeAt)&&now-noticeAt<NativeHordeRoam.NoticeSeconds){r.AlertAge=now-noticeAt;r.BurstAge=now-noticeAt;}
            }
            else
            {
                r.HasLookYaw=wire.HasLookYaw;r.LookYaw=wire.LookYaw;
                bool hunting=state==HordeRoamState.Alert||state==HordeRoamState.Chase;
                if(hunting&&wire.AlertAt>-50&&now>=wire.AlertAt&&now-wire.AlertAt<NativeHordeRoam.AlertMax+NativeHordeRoam.BurstSeconds)
                {
                    r.AlertAge=now-wire.AlertAt;
                    // The burst: the chase that broke out of this alert, from the chase's start.
                    if(state==HordeRoamState.Chase&&curAt>=wire.AlertAt-1e-3f&&now-curAt<NativeHordeRoam.BurstSeconds)r.BurstAge=Math.Max(0,now-curAt);
                    // Past the alert's own layer, only the burst and the chase's tracking remain.
                    if(r.AlertAge>=NativeHordeRoam.NoticeSeconds)r.AlertAge=float.NaN;
                }
            }
            return r;
        }
        private void Infer(float now,float v,bool engaged,bool fresh)
        {
            if(fresh||inferred==HordeRoamState.None)
            {inferred=v>NativeHordeRoam.ChaseAbove?HordeRoamState.Chase:v>NativeHordeRoam.MoveAbove?HordeRoamState.Wander:HordeRoamState.Idle;inferredAt=now;calmSince=inferred==HordeRoamState.Chase?float.NaN:now;noticeAt=float.NaN;slowSince=stillSince=float.NaN;return;}
            if(v<NativeHordeRoam.ChaseBelow){if(float.IsNaN(slowSince))slowSince=now;}else slowSince=float.NaN;
            if(v<NativeHordeRoam.IdleBelow){if(float.IsNaN(stillSince))stillSince=now;}else stillSince=float.NaN;
            switch(inferred)
            {
                case HordeRoamState.Idle:if(v>NativeHordeRoam.ChaseAbove)ToChase(now);else if(v>NativeHordeRoam.MoveAbove)Set(HordeRoamState.Wander,now);break;
                case HordeRoamState.Wander:if(v>NativeHordeRoam.ChaseAbove)ToChase(now);else if(v<NativeHordeRoam.IdleBelow)Set(HordeRoamState.Idle,now);break;
                default:
                    // (Standing to strike holds the chase; its stop and slow holds count from the last attack's window.)
                    if(engaged){slowSince=stillSince=float.NaN;break;}
                    if(!float.IsNaN(stillSince)&&now-stillSince>=NativeHordeRoam.StopHold)Set(HordeRoamState.Idle,now);
                    else if(!float.IsNaN(slowSince)&&now-slowSince>=NativeHordeRoam.ChaseHold&&v>NativeHordeRoam.MoveAbove)Set(HordeRoamState.Wander,now);
                    break;
            }
        }
        private void ToChase(float now){noticeAt=!float.IsNaN(calmSince)&&now-calmSince>=NativeHordeRoam.CalmBefore?now:float.NaN;Set(HordeRoamState.Chase,now);}
        private void Set(HordeRoamState s,float now){if(s!=HordeRoamState.Chase&&inferred==HordeRoamState.Chase)calmSince=now;if(s==HordeRoamState.Chase)calmSince=float.NaN;inferred=s;inferredAt=now;}
        private static float Sm(float x)=>MeleeMath.Smooth(x);
    }
}
