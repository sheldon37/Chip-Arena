using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// NOCTIEL's body in a live fight (docs/EXPEDITION-BOSS-FINAL.md, "Live motion"): how it moves between and through the
    /// authority's facts, the way the DEV preview's script moves it. Presentation only: it reads the replicated actor (where it
    /// is, its action, that action's times and lanes) and writes a FallenBossFrame's pose; nothing in play reads it, and no
    /// hit, hitbox, damage, cooldown, movement or wire value changes.
    ///  - Between moves it stands with the sword carried and turns to its quarry in steps (Runtime plants the feet). Chasing
    ///    (the authority's 9 m/s) it glides a hand above the water with its wings open, never running on the spot; slower
    ///    travel walks at the clip's own stride.
    ///  - Each move plays the preview's choreography on the authority's clock: the clip's contact lands on the move's hit
    ///    (each of Cleave's three cuts on its own, Severance's second wing on its wave), after a held windup; short windups
    ///    draw the wings in, long charges spread them; the wings are thrown out and the blade trails on the strike only.
    ///    Descent leaps from where it stood to where it lands, landing on the hit; Vow flies its lane on the authority's own
    ///    lerp; Feather Rain and Fall of Light lift it off the water.
    ///  - Clips crossfade (no pops); position and facing follow critically damped springs (no snaps) except where a move puts
    ///    it, and a long jump (a respawned or teleported actor) is taken at once.
    /// </summary>
    public sealed class NativeFallenLiveMotion
    {
        /// <summary>What the authority says this frame (all replicated): the actor, its quarry and its action.</summary>
        public struct Facts
        {
            public float Now;public Vector3 At;public float Yaw;public Vector3 Target;
            public string Action;public float ActionAt,HitAt,Duration,ActionYaw;public Vector3 From,To;public bool SecondWing;
        }
        public const string Stance="Unarmed-Idle",Glide="Armed-Dash-Forward",Walk="Armed-Walk",SlowWalk="Armed-Walk-Slow";
        /// <summary>The owner's reference walk (2026-11-06): about one step a second, heavy and measured. The slow walk plays up to this
        /// rate before the brisker walk takes over; at NOCTIEL's size its 1.6 s cycle at 0.8 is that step a second.</summary>
        public const float SlowWalkMost=1.35f;
        /// <summary>Travel (m/s) that starts a glide, and below which it lands; the walk's floor; the glide's height.</summary>
        public const float GlideFrom=3.2f,GlideUntil=1.8f,WalkFrom=.6f,GlideAir=.5f;
        /// <summary>A standing turn starts past this many degrees off its quarry.</summary>
        public const float TurnFrom=18;
        private const byte Hold=0,Looped=1,Strike=2,Paced=3;
        /// <summary>One clip on the plan: Strike shots retime the clip so its contact lands on T0+Wind (NativeFallenBossPreview.ClipTime).</summary>
        public struct Shot{public string Clip;public byte Mode;public float T0,T1,Wind,Hit,Rate,From,Fade,Flare;public int Key;}
        public const int MaxShots=4;
        private readonly Shot[] plan=new Shot[MaxShots];private int planCount;private string planAction;private float planAt=float.NaN,planEnd=-1,takeoff,leapHeight;
        private Shot cur,prev;private bool curSet;private float switchAt;
        private bool started,gliding,turning;private float last,yaw,yawV,yawGoal,air,airV,speed,carry,kneel,kneelV,track,walkPhase;
        private Vector3 at,atV,lastFact,heading;
        /// <summary>The plan for the action now running (empty between moves), for the checks and the DEV readout.</summary>
        public ReadOnlySpan<Shot> Plan=>new ReadOnlySpan<Shot>(plan,0,planCount);
        public bool Gliding=>gliding;public float Speed=>speed;
        /// <summary>It has stepped since the last Reset (the next Step continues rather than starting over).</summary>
        public bool Active=>started;
        /// <summary>Forget everything: the next Step starts from the facts (a new fight, after the intro or a death).</summary>
        public void Reset(){started=false;curSet=false;planAction=null;planAt=float.NaN;planCount=0;planEnd=-1;}

        /// <summary>
        /// Writes f's pose for this frame. The binding has filled the rest of f (phase, kind, the Slash, the wings' phase opening,
        /// the target and Kneel for a phase change); this replaces Boss, Yaw, the clips and their mix, Air, Gliding, Carry, Kneel
        /// (eased), Track, Trail, Flare and the wings. seedClip/seedTime: the pose shown before the first Step (it crossfades out).
        /// </summary>
        public void Step(FallenBossFrame f,in Facts x,NativeVisualMotionClips.Library clips,string seedClip=null,float seedTime=0)
        {
            float now=x.Now,dt=started?Clamp(now-last,0,.1f):0;
            if(!started)
            {
                started=true;at=x.At;atV=Vector3.Zero;lastFact=x.At;yaw=yawGoal=x.Yaw;yawV=0;air=airV=0;speed=0;gliding=turning=false;carry=1;kneel=f.Kneel;kneelV=0;track=0;walkPhase=0;heading=Vector3.Zero;
                curSet=false;planAction=null;planAt=float.NaN;planCount=0;planEnd=-1;
                if(seedClip!=null){cur=new Shot{Clip=seedClip,Mode=Hold,From=seedTime,Key=-1};curSet=true;switchAt=now-10;}
            }
            last=now;
            // The authority's travel: its speed and heading from where the actor has been (a long jump is a teleport, not travel).
            var moved=x.At-lastFact;moved.Y=0;float jump=moved.Length();lastFact=x.At;
            if(dt>0)
            {
                float k=1-(float)Math.Exp(-dt*8),v=jump>12?0:jump/dt;speed+=(v-speed)*k;
                if(jump>1e-4f&&jump<=12){var h=Vector3.Lerp(heading,moved/jump,heading==Vector3.Zero?1:Math.Min(1,k*2.5f));heading=h.LengthSquared()>1e-6f?Vector3.Normalize(h):moved/jump;}
            }
            // A new action: its plan, on the authority's clock.
            bool live=x.Action!=null&&x.Action!="arrive"&&now>=x.ActionAt-.05f&&now<x.ActionAt+x.Duration+.05f;
            if(live&&(x.Action!=planAction||x.ActionAt!=planAt))Build(x,clips);
            bool inPlan=planCount>0&&now<planEnd&&now>=plan[0].T0-.05f;
            // Down on a knee for a phase change (the binding's Kneel): no move plays through it.
            if(f.Kneel>.5f&&inPlan){planCount=0;inPlan=false;}
            // Chasing again before the plan's last settle is done: the glide takes over.
            if(inPlan&&!live&&speed>GlideFrom){planCount=0;inPlan=false;}
            if(gliding){if(speed<GlideUntil||inPlan)gliding=false;}else if(!inPlan&&speed>GlideFrom)gliding=true;
            // What plays now.
            Shot want;
            if(inPlan){int i=0;for(int j=0;j<planCount;j++)if(now>=plan[j].T0)i=j;want=plan[i];}
            else if(gliding)want=new Shot{Clip=Pick(Glide,clips),Mode=Looped,Rate=1.6f,Fade=.16f,Key=1};
            else if(speed>WalkFrom&&WalkOf(clips,speed,out var w,out float rate))
            {walkPhase=(walkPhase+dt*rate/Math.Max(.1f,w.Length))%1f;want=new Shot{Clip=w.Name,Mode=Paced,Fade=.22f,Key=w.Name==SlowWalk?3:2};}
            else want=new Shot{Clip=Stance,Mode=Looped,Rate=1,Fade=.26f,Key=0};
            if(!curSet){cur=want;curSet=true;switchAt=now-10;}
            else if(want.Key!=cur.Key||want.T0!=cur.T0){prev=cur;cur=want;switchAt=now;}
            float mix=Smooth((now-switchAt)/Math.Max(.04f,cur.Fade));
            f.ClipTo=cur.Clip;f.TimeTo=TimeOf(cur,now,clips);
            if(mix<1&&prev.Clip!=null){f.ClipFrom=prev.Clip;f.TimeFrom=TimeOf(prev,now,clips);f.Mix=mix;}else{f.ClipFrom=f.ClipTo;f.TimeFrom=f.TimeTo;f.Mix=1;}
            // Where it is: the move's own path (Descent's leap, Vow's lane) or the actor's, eased.
            float arc=0;bool placed=false;
            if(inPlan&&x.Action=="descent"&&now>=takeoff&&now<x.HitAt)
            {float u=Clamp01((now-takeoff)/Math.Max(.05f,x.HitAt-takeoff));at=Vector3.Lerp(x.From,x.To,Smooth(u));arc=4*leapHeight*u*(1-u);placed=true;}
            else if(inPlan&&x.Action=="vow"&&now>=x.HitAt&&now<x.HitAt+.5f){at=Vector3.Lerp(x.From,x.To,Clamp01((now-x.HitAt)/.5f));placed=true;}
            if(placed){atV=Vector3.Zero;at.Y=x.At.Y;}
            else if(Vector3.DistanceSquared(at,x.At)>64||dt<=0){at=x.At;atV=Vector3.Zero;}
            else{Spring(ref at.X,ref atV.X,x.At.X,dt,.06f);Spring(ref at.Y,ref atV.Y,x.At.Y,dt,.06f);Spring(ref at.Z,ref atV.Z,x.At.Z,dt,.06f);}
            f.Boss=at;
            // Which way it faces: the move's aim (fast), its glide's heading, or its quarry in standing turns.
            float smooth;
            if(inPlan){yawGoal=x.ActionYaw;smooth=.07f;turning=false;}
            else if(gliding&&heading!=Vector3.Zero){yawGoal=YawOf(heading);smooth=.16f;turning=false;}
            else
            {
                var d=x.Target-at;d.Y=0;if(d.LengthSquared()>.25f){float want2=YawOf(d);if(turning||Math.Abs(Delta(yaw,want2))>TurnFrom){yawGoal=want2;turning=true;}}
                if(turning&&Math.Abs(Delta(yaw,yawGoal))<2.5f&&Math.Abs(yawV)<20)turning=false;smooth=.22f;
            }
            if(dt<=0){yaw=yawGoal;yawV=0;}else{float y=0;Spring(ref y,ref yawV,Delta(yaw,yawGoal),dt,smooth);yaw=Wrap(yaw+y);}
            f.Yaw=yaw;
            // Off the water: the glide, Vow's flight, the hover of Rain and Fall, Descent's arc.
            float airGoal=gliding?GlideAir+.06f*(float)Math.Sin(now*2.3f):0;
            if(inPlan)
            {
                if(x.Action=="rain"&&now>=x.HitAt-.3f&&now<x.HitAt+2.4f)airGoal=1.1f;
                else if(x.Action=="fall"&&now>=x.HitAt-.4f&&now<x.HitAt+3.2f)airGoal=1.4f;
            }
            if(dt<=0){air=airGoal;airV=0;}else Spring(ref air,ref airV,airGoal,dt,airGoal>air?.12f:.18f);
            float flight=0;if(inPlan&&x.Action=="vow"){float u=(now-x.HitAt+.1f)/.7f;if(u>0&&u<1)flight=.45f*(float)Math.Sin(u*Math.PI);}
            f.Air=Math.Max(0,air)+arc+flight;f.Gliding=gliding||f.Air>.2f;
            // The sword carried while it stands; the clip's hand through a move or a glide (Runtime turns the blade out of the body).
            float carryGoal=inPlan||gliding?0:1;carry=MoveTo(carry,carryGoal,dt/Math.Max(.1f,cur.Fade));f.Carry=Smooth(carry);
            if(dt<=0)kneel=f.Kneel;else Spring(ref kneel,ref kneelV,f.Kneel,dt,.16f);f.Kneel=Clamp01(kneel);
            float trackGoal=inPlan?(now<x.HitAt?.5f:0):gliding?.5f:1;track=MoveTo(track,trackGoal,dt*3);f.Track=track;
            // The strike: the trail and the wings' throw from the shot now playing.
            f.Trail=0;f.Flare=0;float wingShift=0;
            if(inPlan&&cur.Mode==Strike)
            {
                float tw=cur.T0+cur.Wind;f.Trail=now<tw-.05f?0:1-Smooth((now-tw-cur.Hit-.14f)/.2f);
                if(now<tw){float u=Smooth((now-cur.T0)/Math.Max(.01f,cur.Wind));if(cur.Wind<1.2f)wingShift=-.3f*u;else wingShift=.35f*u;f.Flare=cur.Wind<1.2f?0:.2f*u;}
                else f.Flare=cur.Flare*(1-Clamp01((now-tw-cur.Hit)/.5f));
            }
            for(int i=0;i<3;i++){float o=f.WingOpen[i];if(gliding)o=Math.Max(o,.92f)+.08f*(float)Math.Sin(now*2.8f-i*.5f);if(inPlan&&(x.Action=="rain"||x.Action=="fall")&&now>=x.HitAt-.4f)o=Math.Max(o,1.05f);f.WingOpen[i]=Clamp(o+wingShift,-.05f,1.12f);}
        }

        // The preview's moves on the authority's times (A the action's start, H its hit, the authority's own windup).
        private void Build(in Facts x,NativeVisualMotionClips.Library clips)
        {
            planAction=x.Action;planAt=x.ActionAt;planCount=0;float A=x.ActionAt,H=x.HitAt;
            switch(x.Action)
            {
                case "cleave":
                    // Three cuts, one on each of the authority's strikes (hit, +0.3, +0.6).
                    Add("Sword-Attack-R1",A,H,.1f,H+.24f,.12f,.6f,clips);Add("Sword-Attack-R2",H+.12f,H+.3f,.1f,H+.54f,.08f,.6f,clips);Add("Sword-Attack-R3",H+.42f,H+.6f,.12f,H+1.2f,.08f,.9f,clips);break;
                case "vow":Add("Armed-Run-Attack-R1",A,H+.12f,.3f,H+.95f,.12f,.8f,clips);break;
                case "severance":
                    Add("Sword-Attack-R4",A,H,.2f,H+.9f,.14f,1,clips);if(x.SecondWing)Add("Sword-Attack-R6",H+.42f,H+.6f,.2f,H+1.5f,.1f,1,clips);break;
                case "descent":
                {
                    // Crouched through the windup, it leaves the water and comes down on the hit, where the authority puts it.
                    float flight=Clamp(H-A-.4f,.55f,1f);takeoff=Math.Max(A+.12f,H-flight);float reach=Vector3.Distance(new Vector3(x.From.X,0,x.From.Z),new Vector3(x.To.X,0,x.To.Z));leapHeight=Clamp(reach*.32f,3.5f,11);
                    Add("2Hand-Sword-Jump",A,takeoff-.06f,.06f,takeoff+.02f,.12f,.5f,clips);Add("2Hand-Sword-Air-Attack1",takeoff,H,.1f,H+.04f,.08f,.7f,clips);Add("2Hand-Sword-Land",H+.04f,H+.06f,.05f,H+.8f,.06f,.8f,clips);break;
                }
                case "rain":Add("Armed-Activate-Left",A,H,.3f,H+.8f,.2f,1,clips);break;
                case "verdict":case "last":Add("2Hand-Sword-Attack11",A,H,.18f,H+1.1f,.22f,.7f,clips);break;
                case "choir":case "penance":case "thrones":Add("Armed-Activate-Left",A,H,.24f,H+.7f,.2f,.6f,clips);break;
                case "sweep":Add("2Hand-Sword-Attack3",A,H,.3f,H+1.05f,.2f,1.1f,clips);break;
                case "eclipse":Add("2Hand-Sword-Activate",A,H,.3f,H+1.1f,.3f,.5f,clips);break;
                case "fall":Add("2Hand-Sword-Activate",A,H,.3f,H+3.2f,.3f,.3f,clips);break;
                default:{var m=NativeFallenBoss.Move(x.Action);Add(m.Clip??"Armed-Activate-Left",A,H,.2f,H+.8f,.2f,.6f,clips);break;}
            }
            planEnd=planCount>0?plan[planCount-1].T1:-1;
        }
        private void Add(string clip,float t0,float hit,float strike,float t1,float fade,float flare,NativeVisualMotionClips.Library clips)
        {
            // ClipTime reaches the clip's own Hit about a quarter of the strike after the windup: the windup ends that much early.
            if(planCount>=MaxShots)return;float wind=Math.Max(.04f,hit-t0-strike*.25f);
            plan[planCount]=new Shot{Clip=Pick(clip,clips),Mode=Strike,T0=t0,Wind=wind,Hit=strike,T1=Math.Max(t1,t0+wind+strike+.05f),Fade=fade,Flare=flare,Key=100+planCount};planCount++;
        }
        // The walk for this speed and its rate, the stride matched to the travel (the clip's own travel times its size): the slow,
        // heavy walk while it can keep up, the brisker one past it. The cycle runs on as a fraction, so a change keeps the feet's phase.
        private static bool WalkOf(NativeVisualMotionClips.Library clips,float speed,out NativeVisualMotionClips.Clip walk,out float rate)
        {
            walk=null;rate=1;if(clips==null)return false;
            bool slow=clips.Clips.TryGetValue(SlowWalk,out var s)&&s.SpeedZ>.1f,brisk=clips.Clips.TryGetValue(Walk,out var b)&&b.SpeedZ>.1f;
            float rs=slow?speed/(s.SpeedZ*NativeFallenBoss.Scale):0;
            if(slow&&(rs<=SlowWalkMost||!brisk)){walk=s;rate=Clamp(rs,.5f,brisk?SlowWalkMost:1.6f);return true;}
            if(brisk){walk=b;rate=Clamp(speed/(b.SpeedZ*NativeFallenBoss.Scale),.5f,1.6f);return true;}
            return false;
        }
        /// <summary>The clip, else the nearest the bank has (a cut for a cut, a raised hand for a summon), else the stance.</summary>
        public static string Pick(string clip,NativeVisualMotionClips.Library clips)
        {
            if(clips==null||clips.Has(clip))return clip;
            if(clip.StartsWith("Sword-Attack-R",StringComparison.Ordinal)&&clips.Has("2Hand-Sword-Attack1"))return "2Hand-Sword-Attack1";
            if(clip==Glide&&clips.Has("Armed-Run-Forward"))return "Armed-Run-Forward";
            if(clips.Has("Armed-Activate-Left")&&clip!=Glide)return "Armed-Activate-Left";
            return Stance;
        }
        private float TimeOf(in Shot s,float now,NativeVisualMotionClips.Library clips)
        {
            switch(s.Mode)
            {
                case Hold:return s.From;
                case Looped:return s.From+now*s.Rate;
                case Paced:return walkPhase*(clips!=null&&clips.Has(s.Clip)?clips.Length(s.Clip):1);
                default:return NativeFallenBossPreview.ClipTime(new NativeFallenBossPreview.Seg{T0=s.T0,T1=s.T1,Wind=s.Wind,Hit=s.Hit,Clip=s.Clip,Mode=Strike,Rate=1},now,clips);
            }
        }
        /// <summary>The contact of a Strike shot: when its clip passes its own Hit (NaN for other shots or without a bank entry).</summary>
        public static float ContactOf(in Shot s,NativeVisualMotion.IClips clips)
        {
            if(s.Mode!=Strike)return float.NaN;float hit=clips!=null&&clips.Has(s.Clip)?clips.Hit(s.Clip):float.NaN;if(float.IsNaN(hit))return float.NaN;
            var seg=new NativeFallenBossPreview.Seg{T0=s.T0,T1=s.T1,Wind=s.Wind,Hit=s.Hit,Clip=s.Clip,Mode=Strike,Rate=1};
            float lo=s.T0,hi=s.T1;if(NativeFallenBossPreview.ClipTime(seg,hi,clips)<hit)return float.NaN;
            for(int i=0;i<40;i++){float m=(lo+hi)*.5f;if(NativeFallenBossPreview.ClipTime(seg,m,clips)<hit)lo=m;else hi=m;}
            return hi;
        }
        // Critically damped (the smooth time is about how long it takes to close the gap).
        private static void Spring(ref float x,ref float v,float target,float dt,float smooth)
        {float w=2f/Math.Max(.001f,smooth),k=1f/(1f+w*dt+.48f*w*w*dt*dt+.235f*w*w*w*dt*dt*dt),c=x-target,t=(v+w*c)*dt;v=(v-w*t)*k;x=target+(c+t)*k;}
        private static float YawOf(Vector3 d)=>(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);
        private static float Delta(float from,float to){float d=(to-from)%360;if(d>180)d-=360;if(d<-180)d+=360;return d;}
        private static float Wrap(float a){a%=360;if(a>180)a-=360;if(a<-180)a+=360;return a;}
        private static float MoveTo(float a,float b,float step)=>a<b?Math.Min(b,a+step):Math.Max(b,a-step);
        private static float Clamp(float x,float lo,float hi)=>x<lo?lo:x>hi?hi:x;
        private static float Clamp01(float x)=>x<0?0:x>1?1:x;
        private static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
    }
}
