using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>One moment of the idle Cyber Lord's glitch (CyberLordIdlePose.Glitch): what the pose, the hardlight parts and
    /// the RGB-split ghosts show. All zero between the glitch moments.</summary>
    public struct CyberIdleGlitch
    {
        /// <summary>The upper body torn sideways off the hips, and the head band torn on its own (m, rig frame: +x the rig's left).</summary>
        public float Slice,HeadSlice;
        /// <summary>The RGB-split ghosts' strength (0..1) and the gap between the two (m).</summary>
        public float Split,SplitOffset;
        /// <summary>The scanline band on the hardlight parts: its strength (0..1) and its centre (m, rig rest space: the feet at -.795).</summary>
        public float Scan,ScanY;
        /// <summary>A rare one- or two-step jolt (-1..1).</summary>
        public float Twitch;
        /// <summary>The glitch step (seeds the flicker pattern; it holds between steps).</summary>
        public int Step;
        /// <summary>The idle's fist clench twitches (0..1, the rig's left and right fist; CyberTwitch): the hardlight fist shells
        /// flare with them (NativeCyberLordLook.Flare). Not a glitch: Visible ignores them.</summary>
        public float LClench,RClench;
        public bool Visible=>Math.Abs(Slice)>1e-4f||Math.Abs(HeadSlice)>1e-4f||Split>1e-3f||Scan>1e-3f||Math.Abs(Twitch)>1e-3f;
    }

    /// <summary>
    /// When the DEV Cyber Lord settles into its idle stance (owner 2026-10-12: "Id like to add a idle pose for the DEV Cyber
    /// Lord class (Matching the sheldon look) slight glitch effects"). One per rig, like the gait under it: a third-person
    /// pilot that has stood still, unhurt and out of every move for Delay seconds blends in over BlendIn; any move, attack,
    /// cast, guard, roll, hit reaction, stun, jump or step blends it out within BlendOut. Every window is read from the
    /// replicated actor and its CyberLordView; the only memory is when the pilot last moved (the gait's travel, the actor's
    /// velocity), so a fresh rig (a late join, the killcam) of a pilot already standing still shows the idle at once.
    /// Presentation only: it reads the actor and never writes it, and the hitboxes are the capsule, not the pose.
    /// </summary>
    public sealed class CyberLordIdle
    {
        /// <summary>The still time before the stance (s), its blend in, and its blend out on any action.</summary>
        public const float Delay=.9f,BlendIn=.6f,BlendOut=.08f;
        /// <summary>Above this ground speed (m/s), or with the gait still carrying (travel), the pilot is moving.</summary>
        public const float StillSpeed=.35f,StillTravel=.02f;
        public float Weight{get;private set;}
        private float last=float.NaN,quietSince=float.NegativeInfinity;
        public void Reset(){Weight=0;last=float.NaN;quietSince=float.NegativeInfinity;}
        /// <summary>This frame's weight (0..1). `travel` is the gait's carry (0 standing), `stun` the route's hit stun.</summary>
        public float Update(Actor a,CyberLordView v,float time,float travel,bool stun)
        {
            bool busy=Busy(a,v,time,out float end)|stun|travel>StillTravel;
            bool fresh=float.IsNaN(last)||time<last||time-last>.35f;float dt=fresh?0:time-last;last=time;
            if(busy)quietSince=time;else if(fresh)quietSince=end;else quietSince=Math.Max(quietSince,end);
            float target=busy?0:MeleeMath.Ease((time-quietSince-Delay)/BlendIn);
            Weight=fresh||target>=Weight?target:Math.Max(target,Weight-dt/BlendOut);
            return Weight;
        }
        /// <summary>
        /// Whether something keeps the pilot out of the idle at `time` (dead, airborne, moving or sprinting, a combo swing or a
        /// cast with its settle and blend out, Kernel Panic, a guard, a roll, a hit reaction or stun, an attack window), and
        /// the latest end of those windows already over (`end`; -inf for none). Windows starting after `time`, and the never-used
        /// ones (ending at or before the match's start), are ignored.
        /// </summary>
        public static bool Busy(Actor a,CyberLordView v,float time,out float end)
        {
            end=float.NegativeInfinity;if(a==null||!a.Alive||!a.Grounded||a.Sprinting)return true;
            if(a.Velocity.x*a.Velocity.x+a.Velocity.z*a.Velocity.z>StillSpeed*StillSpeed)return true;
            bool busy=false;float t=time,e=end;
            void Window(float from,float until){if(!(from>-90)||!(until>0))return;if(t>=from&&t<until)busy=true;else if(until<=t&&until>e)e=until;}
            float o=CyberLordMotion.BlendOut;
            if(v!=null)
            {
                Window(v.ComboAt,v.ComboUntil+o);
                for(int s=0;s<CyberLordSpec.Slots;s++)Window(v.At[s],Math.Max(v.Until[s],v.RecoverUntil[s])+o);
                if(v.PanicConnect>-90)Window(v.PanicConnect,(v.PanicOutcome==CyberLordSpec.EndedEarly&&v.PanicEndedAt>-90?v.PanicEndedAt:v.PanicStart+KernelPanicPath.End)+o);
            }
            Window(a.BlockAt>-90?a.BlockAt:a.BlockUntil-Rules.BlockDuration,a.BlockUntil);
            Window(a.RollAt,a.RollAt+a.VisualRollDuration);Window(a.ReactionAt,a.ReactionUntil);Window(a.AttackAt,a.AttackUntil);Window(a.SpecialAt,a.SpecialUntil);
            if(a.StunUntil>0){if(t<a.StunUntil)busy=true;else if(a.StunUntil>e)e=a.StunUntil;}
            end=e;return busy;
        }
    }

    /// <summary>
    /// One moment of the idle Cyber Lord's twitching (CyberLordIdlePose.Twitch): the "psycho" layer over the sword idle. Angles
    /// in radians in the rig's joint frames (source frame: +x the rig's left, + pitch forward), the shift in metres; every
    /// field 0 at rest.
    /// </summary>
    public struct CyberTwitch
    {
        /// <summary>The head's snapped jerk (pitch + chin down, turn + to the rig's left, tilt + the left ear down) and the neck's spasm.</summary>
        public Vector3 Head,Neck;
        /// <summary>The shoulders' hitch (+ up) and the fists' clench (0..1).</summary>
        public float LShoulder,RShoulder,LClench,RClench;
        /// <summary>The ragged breath (0 out .. about 1.3 at a gasp), the full-body flinch (0..1) and the weight shift that stops short
        /// (m, + to the rig's left).</summary>
        public float Breath,Flinch,Shift;
        /// <summary>A glitch flicker synced to a big twitch (0..1: the flinch and the largest head jerks), read by Glitch.</summary>
        public float Sync;
    }

    /// <summary>
    /// The DEV Cyber Lord's idle stance and its glitch (owner 2026-10-12: "Change Cyberlord idle to be similar to the sword
    /// idle, but more "psycho" appearance, like they're twitching, etc. Same will apply to their idle"): the sword's own
    /// standing idle (the owner's Explosive Armed-Idle, MeleeSet "sword" Idle: hunched, knees bent, arms hanging out wide) with
    /// the empty hands closed into fists, and over it a twitching layer: the head jerking and tilting, snapping and holding;
    /// shoulder and neck spasms; the fists clenching (their hardlight shells flaring); an uneven, ragged breath with gasps; an
    /// occasional full-body flinch; weight shifts that stop short. Every twitch is a fast attack, a short hold and an eased
    /// release, on its own irregular, seeded schedule per pilot (Twitch: a pure function of the seed and the time; no
    /// per-frame noise). In a match it layers over the posed fighter rig with the gait's planted legs (Base, Layer); in the
    /// lobby, Locker and pilot-card portraits it is the same stance on the fighter stance's feet (Stand, NativeLobbyPoses).
    /// The glitch stays slight and occasional, seeded per pilot (a brief slice, an RGB-split shimmer, a scanline band over the
    /// hardlight parts, a rare jolt, all on 24 Hz steps like the Sheldon Intro's), and the flinch and the biggest head jerks
    /// flicker it in sync. Reduced effects: fewer and smaller jerks, softer attacks, no tear or jolt, the split eased and the
    /// band a smooth glow, no strobing. Pure functions of the time, the seed and the weight; no allocation.
    /// </summary>
    public static class CyberLordIdlePose
    {
        public static bool Handles(string spec)=>spec==CyberLordSpec.Id;
        /// <summary>Whether the hat is the Sheldon Head (kept for the portrait callers; the stance no longer reaches for it).</summary>
        public static bool Hood(string hat)=>hat==NativeSheldonHead.Hat;
        /// <summary>The sword's standing idle (Core/CapturedClips.Generated "sword/idle", the owner's Explosive Armed-Idle).</summary>
        public static CapturedClip Clip=>MeleeSet.For("sword").Idle;
        /// <summary>
        /// The empty hands closed into fists over the sword idle's grip (chest space, m): the right fist (the sword hand) drawn
        /// in from the empty blade's reach toward the hip and turned knuckles forward, the left fist a little in; the fists'
        /// knuckle line (the grip's blade axis) and their facing (its edge).
        /// </summary>
        public static readonly Vector3 RightFist=new Vector3(.09f,.02f,-.05f),LeftFist=new Vector3(-.05f,.01f,-.02f),FistAxis=Vector3.Normalize(new Vector3(.25f,-.35f,.9f)),FistEdge=Vector3.Normalize(new Vector3(0,-.9f,-.35f));
        /// <summary>The base row (CyberLordMotion's capture channels) at `time`: the sword idle with the fists.</summary>
        public static void Base(float time,float[] into)
        {
            Clip.Sample(time,into);
            into[MeleeCapture.Grip]+=RightFist.X;into[MeleeCapture.Grip+1]+=RightFist.Y;into[MeleeCapture.Grip+2]+=RightFist.Z;
            into[MeleeCapture.LHand]+=LeftFist.X;into[MeleeCapture.LHand+1]+=LeftFist.Y;into[MeleeCapture.LHand+2]+=LeftFist.Z;
            into[MeleeCapture.Blade]=FistAxis.X;into[MeleeCapture.Blade+1]=FistAxis.Y;into[MeleeCapture.Blade+2]=FistAxis.Z;
            into[MeleeCapture.Edge]=FistEdge.X;into[MeleeCapture.Edge+1]=FistEdge.Y;into[MeleeCapture.Edge+2]=FistEdge.Z;into[MeleeCapture.TwoHand]=0;
        }

        // ---- the twitches ----
        /// <summary>A twitch's envelope at `age`: a fast attack (out-cubic over `attack`), held for `hold`, eased out over `release`.</summary>
        public static float Envelope(float age,float attack,float hold,float release)
        {
            if(!(age>0))return 0;if(age<attack){float u=1-age/attack;return 1-u*u*u;}age-=attack;if(age<hold)return 1;age-=hold;return age<release?1-MeleeMath.Ease(age/release):0;
        }
        /// <summary>
        /// One schedule: windows of `window` s (each pilot's offset), a window holding an event with chance `chance`, at a seeded
        /// moment inside it. The event of window `n` (n = the current one, then the one before: an event may run past its own
        /// window): its start and its key for the hashes. False when that window holds none.
        /// </summary>
        private static bool Event(int seed,float time,float window,float chance,float span,int salt,int back,out float start,out int key)
        {
            float phase=H(seed,salt)*window;int n=(int)Math.Floor((time+phase)/window)-back;key=seed*7919+n*131+salt*1009;start=n*window-phase+H(key,1)*Math.Max(0,window-span);
            return H(key,2)<chance;
        }
        /// <summary>The schedules (s): each kind's window, chance (full, reduced) and its timing ranges.</summary>
        public const float HeadWindow=1.45f,HeadChance=.75f,ReducedHeadChance=.4f,SpasmWindow=2.05f,SpasmChance=.6f,ReducedSpasmChance=.3f,ClenchWindow=1.15f,ClenchChance=.5f,ReducedClenchChance=.3f,
            FlinchWindow=5.3f,FlinchChance=.55f,ReducedFlinchChance=.3f,ShiftWindow=3.7f,ShiftChance=.6f,GaspWindow=2.6f,GaspChance=.55f;
        /// <summary>The largest jerks (full effects; Reduced keeps ReducedScale of them, with attacks ReducedAttack times as long).</summary>
        public const float HeadTilt=.42f,HeadTurn=.3f,HeadPitch=.2f,SpasmShoulder=.26f,SpasmNeck=.16f,ShiftMax=.045f,ReducedScale=.45f,ReducedAttack=2.2f,SyncHead=.33f;
        /// <summary>The breath: its mean period (s), the warp of its cycles, its depth's swing and a gasp's catch; at a full in-breath the
        /// spine's counter-turn, the chest's lift and the shoulders' rise (radians).</summary>
        public const float BreathPeriod=3f,BreathSpine=.035f,BreathChest=.02f,BreathShoulder=.11f,Gasp=.55f;
        private static float H(int n,int salt)=>NativeSheldonHead.Hash01(n,salt);
        private static float Sign(float h)=>h<.5f?-1:1;
        /// <summary>A big twitch's glitch sync at `age`: up with its attack, held `hold`, off over `fade` (Reduced: one slow swell
        /// at .6, never a flicker).</summary>
        private static float SyncOf(float age,float attack,float hold,float fade,bool reduced)
            =>reduced?.6f*(float)Math.Sin(Math.PI*MeleeMath.Clamp(age/(attack+hold+fade+.25f),0,1)):Envelope(age,attack,hold,fade);
        /// <summary>The twitching at `time` for the pilot `seed` (no weight: the caller scales it).</summary>
        public static CyberTwitch Twitch(int seed,float time,bool reduced)
        {
            var t=new CyberTwitch();if(float.IsNaN(time)||float.IsInfinity(time))return t;
            float scale=reduced?ReducedScale:1,slow=reduced?ReducedAttack:1;
            // The head: a snap to a tilted, turned stare that holds, some with a second, smaller snap further over.
            for(int back=0;back<2;back++)
            {
                if(!Event(seed,time,HeadWindow,reduced?ReducedHeadChance:HeadChance,1.3f,21,back,out float at,out int k))continue;
                float a=(.035f+.035f*H(k,3))*slow,hold=.2f+.45f*H(k,4),rel=.25f+.25f*H(k,5),e=Envelope(time-at,a,hold,rel);if(e<=0)continue;
                float big=.45f+.55f*H(k,6);var head=new Vector3(Sign(H(k,7))*HeadPitch*(.25f+.75f*H(k,8)),Sign(H(k,9))*HeadTurn*(.3f+.7f*H(k,10)),Sign(H(k,11))*HeadTilt*big)*scale;
                if(H(k,12)<.3f){float e2=Envelope(time-at-a-hold*.45f,a*.8f,hold*.55f,rel);head*=1+.4f*e2;}
                t.Head+=head*e;if(big*HeadTilt>SyncHead)t.Sync=Math.Max(t.Sync,SyncOf(time-at,a,.06f,.18f,reduced));
            }
            // The shoulder and neck spasm: one shoulder hitches up, the neck pulled toward it.
            for(int back=0;back<2;back++)
            {
                if(!Event(seed,time,SpasmWindow,reduced?ReducedSpasmChance:SpasmChance,.6f,22,back,out float at,out int k))continue;
                float e=Envelope(time-at,(.03f+.02f*H(k,3))*slow,.05f+.09f*H(k,4),.18f+.12f*H(k,5));if(e<=0)continue;
                bool left=H(k,6)<.5f;float up=SpasmShoulder*(.55f+.45f*H(k,7))*scale*e;if(left)t.LShoulder+=up;else t.RShoulder+=up;
                t.Neck+=new Vector3(-.03f,0,(left?1:-1)*SpasmNeck*(.5f+.5f*H(k,8)))*scale*e;
            }
            // The fists clench (one, the other or both), held, let go.
            for(int back=0;back<2;back++)
            {
                if(!Event(seed,time,ClenchWindow,reduced?ReducedClenchChance:ClenchChance,1f,23,back,out float at,out int k))continue;
                float e=Envelope(time-at,.04f*slow,.15f+.35f*H(k,3),.2f+.15f*H(k,4))*(reduced?.5f:1);if(e<=0)continue;
                float side=H(k,5);if(side<.35f||side>=.7f)t.LClench=Math.Max(t.LClench,e);if(side>=.35f)t.RClench=Math.Max(t.RClench,e);
            }
            // The flinch: the whole body jolts back and up, shoulders hunched, and slowly lets go.
            for(int back=0;back<2;back++)
            {
                if(!Event(seed,time,FlinchWindow,reduced?ReducedFlinchChance:FlinchChance,1f,24,back,out float at,out int k))continue;
                float a=.045f*slow,e=Envelope(time-at,a,.1f+.08f*H(k,3),.4f+.2f*H(k,4));if(e<=0)continue;
                t.Flinch=Math.Max(t.Flinch,e*scale);t.Head+=new Vector3(-.12f,Sign(H(k,5))*.1f,Sign(H(k,6))*.08f)*scale*e;
                t.Sync=Math.Max(t.Sync,SyncOf(time-at,a,.1f,.22f,reduced));
            }
            // The weight shift that stops short: the hips run toward one side, accelerating, and halt dead; held, then eased back.
            for(int back=0;back<2;back++)
            {
                if(!Event(seed,time,ShiftWindow,ShiftChance,3f,25,back,out float at,out int k))continue;
                float go=.28f+.12f*H(k,3),hold=.7f+.9f*H(k,4),rel=.6f+.3f*H(k,5),age=time-at;if(age<=0||age>=go+hold+rel)continue;
                float u=age<go?(float)Math.Pow(age/go,2.2):age<go+hold?1:1-MeleeMath.Ease((age-go-hold)/rel);
                t.Shift+=Sign(H(k,6))*ShiftMax*(.55f+.45f*H(k,7))*u*(reduced?.7f:1);
            }
            // The ragged breath: warped, uneven cycles of a swinging depth, with sudden gasps.
            float ph=time/BreathPeriod+.3f*(float)Math.Sin(2*Math.PI*time/7.7f+6.28f*H(seed,31))+.18f*(float)Math.Sin(2*Math.PI*time/4.3f+6.28f*H(seed,32));
            float depth=.75f+.35f*(float)Math.Sin(2*Math.PI*time/5.9f+6.28f*H(seed,33));t.Breath=depth*(.5f-.5f*(float)Math.Cos(2*Math.PI*ph));
            for(int back=0;back<2;back++)
            {
                if(!Event(seed,time,GaspWindow,GaspChance,.6f,26,back,out float at,out int k))continue;
                t.Breath+=Gasp*(reduced?.5f:1)*Envelope(time-at,.06f*slow,.08f,.35f);
            }
            return t;
        }

        // ---- the pose ----
        /// <summary>The flinch's jolt at full strength: the spine and chest thrown back (rad), the shoulders hunched up, the elbows
        /// jerked in, the hips dropped (m; downward only, so the planted feet always reach).</summary>
        public const float FlinchBack=.11f,FlinchShoulder=.18f,FlinchElbow=.3f,FlinchDip=.022f;
        /// <summary>The fist's clench at full strength: the wrist curled and the elbow tensed (rad).</summary>
        public const float ClenchWrist=.38f,ClenchElbow=.16f;
        /// <summary>The share of the view's pitch the head follows (rad, clamped).</summary>
        public const float PitchShare=.35f,PitchMax=.28f;
        /// <summary>The weight shift's pelvis roll per metre of shift and the chest's counter.</summary>
        public const float ShiftRoll=1.2f,ShiftCounter=.7f;
        /// <summary>The lobby stance's turn (rad, toward the card camera).</summary>
        public const float LobbyYaw=-.18f;
        /// <summary>
        /// The match layer over a posed fighter rig at weight `w` (group space; the legs stay the gait's planted feet): the
        /// twitches, then the glitch's tear and jolt. `pitch` is the view's pitch (rad, + up).
        /// </summary>
        public static void Layer(MeleeRig r,float w,float time,int seed,bool reduced,float pitch,in CyberIdleGlitch g)
        {
            if(w<=1e-4f)return;Twitches(r,w,Twitch(seed,time,reduced),pitch,g);
        }
        /// <summary>
        /// The lobby stance from rest (NativeLobbyPoses, the portraits): the same sword idle and twitches at full weight, on the
        /// fighter stance's feet (CyberLordStance: the match's planted feet), turned LobbyYaw; returns the glitch for the
        /// renderer's ghosts and band (and the clench for its fist shells). `time` is the portrait clock (0 with reduced motion:
        /// the stance holds still, nothing twitches or glitches).
        /// </summary>
        private static readonly float[] standRow=new float[CapturedClip.Channels];
        public static CyberIdleGlitch Stand(MeleeRig r,float time,int seed,bool reduced)=>Stand(r,time,seed,reduced,true,out _);
        /// <summary>The same with the glitch optional (`glitch` false: no tear or jolt, a zero glitch back; the Sheldon Idle of the
        /// other pilots, NativeLobbyPoses) and the frame's twitching out.</summary>
        public static CyberIdleGlitch Stand(MeleeRig r,float time,int seed,bool reduced,bool glitch,out CyberTwitch twitch)
        {
            r.Reset();r.WeaponReach=1;Base(time,standRow);standRow[MeleeCapture.Yaw]=0;CyberLordMotion.Posture(standRow);
            r.SetE(CapturedRig.Body,0,LobbyYaw,0);
            MeleeCapture.Apply(r,CyberLordMotion.Unarmed,standRow,CaptureOptions.Of(1,CyberLordMotion.Unarmed.Fit,MeleeClear.None,legs:0,root:1));CyberLordMotion.FaceForward(r,CyberLordMotion.FaceWeight);
            bool live=time>0;var tw=live?Twitch(seed,time,reduced):default;var g=live&&glitch?Glitch(seed,time,1,reduced):default;
            Twitches(r,1,tw,0,g);g.LClench=tw.LClench;g.RClench=tw.RClench;twitch=tw;
            var st=CyberLordStance.Stance;
            r.Foot(true,new Vector3(st.LeadX,MeleeRig.FLOOR,st.LeadZ),st.LeadToe,0,1,null,NativeLobbyPoses.StandingReach);r.Foot(false,new Vector3(st.RearX,MeleeRig.FLOOR,st.RearZ),-st.RearToe,0,1,null,NativeLobbyPoses.StandingReach);
            return g;
        }
        /// <summary>The fists' clench for the hardlight look (NativeCyberLordLook.Flare's left and right, at ClenchFlare of a hit's
        /// flare) at the idle's weight.</summary>
        public const float ClenchFlare=.6f;
        public static NativeCyberLordLook.Flare Clench(in CyberIdleGlitch g,NativeCyberLordLook.Flare f)
        {f.Left=Math.Max(f.Left,ClenchFlare*g.LClench);f.Right=Math.Max(f.Right,ClenchFlare*g.RClench);return f;}
        private static Quaternion E(float x,float y,float z)=>MeleeMath.Euler(x,y,z);
        private static void Twitches(MeleeRig r,float w,in CyberTwitch t,float pitch,in CyberIdleGlitch g)
        {
            const int Sp=CapturedRig.Spine,C=CapturedRig.Chest,N=CapturedRig.Neck,Hd=CapturedRig.Head;
            // The weight shift (the hips over the planted feet, rolled toward the weight leg) and the flinch's dip.
            var b=r.P[CapturedRig.Body];r.P[CapturedRig.Body]=new Vector3(b.X+t.Shift*w,b.Y-FlinchDip*t.Flinch*w,b.Z);
            r.Premultiply(CapturedRig.Pelvis,E(0,0,-t.Shift*ShiftRoll*w));r.Multiply(Sp,E(0,0,t.Shift*ShiftRoll*ShiftCounter*w));
            // The breath lifts the chest and shoulders (the spine counters so the neck stays put); the flinch throws them back.
            float br=t.Breath*w,fl=t.Flinch*w;
            r.Multiply(Sp,E(BreathSpine*br-FlinchBack*.5f*fl,0,0));r.Multiply(C,E(-(BreathChest+BreathSpine)*br-FlinchBack*.5f*fl,0,0));
            float ls=BreathShoulder*br+FlinchShoulder*fl+t.LShoulder*w,rs=BreathShoulder*br+FlinchShoulder*fl+t.RShoulder*w;
            r.Multiply(CapturedRig.LClav,E(0,0,ls));r.Multiply(CapturedRig.RClav,E(0,0,-rs));
            // The arms jerk in on the flinch; the fists clench (the wrist curls, the forearm tenses).
            r.Multiply(CapturedRig.LElbow,E(-FlinchElbow*fl-ClenchElbow*t.LClench*w,0,0));r.Multiply(CapturedRig.RElbow,E(-FlinchElbow*fl-ClenchElbow*t.RClench*w,0,0));
            r.Multiply(CapturedRig.LHand,E(ClenchWrist*t.LClench*w,0,-.1f*t.LClench*w));r.Multiply(CapturedRig.RHand,E(ClenchWrist*t.RClench*w,0,.1f*t.RClench*w));
            // The neck spasm and the head's snapped jerk, with a share of the view's pitch (the stare follows the aim).
            float look=-PitchShare*MeleeMath.Clamp(pitch,-PitchMax/PitchShare,PitchMax/PitchShare);
            r.Multiply(N,E(t.Neck.X*w,t.Neck.Y*w,t.Neck.Z*w));r.Multiply(Hd,E((t.Head.X+look)*w,t.Head.Y*w,t.Head.Z*w));
            // The glitch's jolt (a step or two) and tear (the joint offsets; the next frame's rig starts from rest).
            if(g.Twitch!=0){r.Premultiply(N,MeleeMath.AxisAngle(MeleeMath.Y,.07f*g.Twitch*w));r.Multiply(CapturedRig.RClav,MeleeMath.AxisAngle(MeleeMath.Z,-.1f*g.Twitch*w));}
            if(g.Slice!=0)r.P[Sp]=new Vector3(r.P[Sp].X+g.Slice*w,r.P[Sp].Y,r.P[Sp].Z);
            if(g.HeadSlice!=0)r.P[Hd]=new Vector3(r.P[Hd].X+g.HeadSlice*w,r.P[Hd].Y,r.P[Hd].Z);
        }

        // ---- the glitch ----
        /// <summary>Glitch steps per second (the Sheldon Intro's): full and reduced effects.</summary>
        public const float StepRate=NativeSheldonIntro.StepRate,ReducedStepRate=NativeSheldonIntro.ReducedStepRate;
        /// <summary>The moments' schedule: one slot every Slot s holds a moment with chance Chance, kind split (below
        /// SplitShare), slice (below SliceShare) or scan, lasting SplitSeconds, SliceSeconds or ScanSeconds; a twitch slot every
        /// TwitchSlot s with chance TwitchChance, TwitchSteps steps long. Reduced effects keep the split and the scan, eased.</summary>
        public const float Slot=2.3f,Chance=.6f,SplitShare=.45f,SliceShare=.75f,SplitSeconds=.2f,SliceSeconds=.125f,ScanSeconds=.5f,ReducedSplitSeconds=.6f,ReducedScanSeconds=.9f;
        public const float TwitchSlot=6.5f,TwitchChance=.55f;public const int TwitchSteps=2;
        /// <summary>The largest slice (m), the split's gap (m) and the split ghosts' strength with reduced effects.</summary>
        public const float SliceMax=.055f,HeadSliceMax=.035f,SplitGap=.03f,ReducedSplit=.45f;
        /// <summary>The glitch only shows once the stance has settled (the weight above this).</summary>
        public const float Settled=.95f;
        private static int Slot0(int seed,float time,float slot,int salt,out float start)
        {
            float phase=H(seed,salt)*slot;int n=(int)Math.Floor((time+phase)/slot);start=n*slot-phase;return n;
        }
        /// <summary>The glitch at `time` for the pilot `seed` at stance weight `weight`.</summary>
        public static CyberIdleGlitch Glitch(int seed,float time,float weight,bool reduced)
        {
            var g=new CyberIdleGlitch();if(!(weight>=Settled)||float.IsNaN(time))return g;
            g.Step=(int)Math.Floor(time*(reduced?ReducedStepRate:StepRate));
            int n=Slot0(seed,time,Slot,11,out float slot);int key=seed*7919+n*131;
            if(H(key,1)<Chance)
            {
                float kind=H(key,3),length=kind<SplitShare?(reduced?ReducedSplitSeconds:SplitSeconds):kind<SliceShare?SliceSeconds:(reduced?ReducedScanSeconds:ScanSeconds);
                float at=slot+.2f+H(key,2)*(Slot-length-.4f),age=time-at;
                if(age>=0&&age<length)
                {
                    float u=age/length;
                    if(kind<SplitShare)
                    {
                        // Full: the ghosts flash on most steps, at a stepped strength; reduced: one eased swell, no steps.
                        if(reduced){g.Split=ReducedSplit*(float)Math.Sin(Math.PI*u);g.SplitOffset=SplitGap*.6f;}
                        else{float on=H(g.Step*31+seed,5);if(on>.25f){g.Split=.55f+.45f*H(g.Step*17+seed,6);g.SplitOffset=SplitGap*(.7f+.6f*H(g.Step*13+seed,7));}}
                    }
                    else if(kind<SliceShare)
                    {
                        // A tear (full effects only): the upper body jumps sideways each step, the head on its own some steps.
                        if(!reduced)
                        {
                            float s=H(g.Step*29+seed,8)-.5f;g.Slice=(s<0?-1:1)*SliceMax*(.4f+.6f*Math.Abs(s)*2);
                            if(H(g.Step*23+seed,9)>.5f)g.HeadSlice=-(g.Slice>0?1:-1)*HeadSliceMax*(.5f+.5f*H(g.Step*19+seed,10));
                            g.Split=.35f;g.SplitOffset=SplitGap*.5f;
                        }
                    }
                    else
                    {
                        // The scanline band sweeps the hardlight parts, up or down.
                        bool up=H(key,4)<.6f;float y=-.85f+1.85f*u;g.ScanY=up?y:.15f-y;g.Scan=reduced?(float)Math.Sin(Math.PI*u):1;
                    }
                }
            }
            if(!reduced)
            {
                int m=Slot0(seed,time,TwitchSlot,12,out float tslot);int tk=seed*104729+m*977;
                if(H(tk,1)<TwitchChance)
                {
                    float at=tslot+.5f+H(tk,2)*(TwitchSlot-1),age=time-at;
                    if(age>=0&&age<TwitchSteps/StepRate)g.Twitch=H(tk,3)<.5f?-1:1;
                }
            }
            // The flinch and the biggest head jerks flicker the split with them (full: on the steps; reduced: one eased swell).
            float sy=Twitch(seed,time,reduced).Sync;
            if(sy>1e-3f)
            {
                if(reduced){g.Split=Math.Max(g.Split,ReducedSplit*.6f*sy);g.SplitOffset=Math.Max(g.SplitOffset,SplitGap*.6f);}
                else if(H(g.Step*41+seed,14)>.3f){g.Split=Math.Max(g.Split,(.6f+.4f*H(g.Step*43+seed,15))*sy);g.SplitOffset=Math.Max(g.SplitOffset,SplitGap*(.8f+.7f*H(g.Step*47+seed,16)));}
            }
            return g;
        }
        /// <summary>The rest height of each joint (rig rest space; CapturedRig.Bones are rest offsets with no rest turn).</summary>
        private static readonly float[] restY=RestHeights();
        private static float[] RestHeights(){var y=new float[CapturedRig.Bones.Length];for(int i=0;i<y.Length;i++){int p=CapturedRig.Bones[i].Parent;y[i]=CapturedRig.Bones[i].Position.Y+(p>=0?y[p]:0);}return y;}
        public const float ScanHalf=.12f;
        /// <summary>A hardlight piece's gain under the scanline band (1 outside it). Full effects flicker the pieces in the band
        /// dim and bright per step; reduced effects only brighten them softly.</summary>
        public static float ScanGain(in CyberIdleGlitch g,in CyberPiece p,int index,bool reduced)
        {
            if(!(g.Scan>1e-3f)||p.Bone<0||p.Bone>=restY.Length)return 1;
            float d=Math.Abs(restY[p.Bone]+p.Position.Y-g.ScanY),band=1-MeleeMath.SmoothStep(d,0,ScanHalf);if(band<=0)return 1;
            if(reduced)return 1+.45f*band*g.Scan;
            float flick=H(g.Step*37+index,13)<.5f?.25f:1.9f;return 1+(flick-1)*band*g.Scan;
        }

    }
}
