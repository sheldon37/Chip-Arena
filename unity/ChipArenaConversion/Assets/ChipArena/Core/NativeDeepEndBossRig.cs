using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// THE DEEP END's motion (docs/EXPEDITION-BOSS-BACKROOMS.md section 6): the head follows a path (<see cref="DeepEndRoute"/>, a pure function of time), and every joint traces that path a fixed
    /// arc length behind it, so nothing ever cuts. On top of the trail: a travelling undulation and a slow roll, the neck's look (the last few joints bent on forward kinematics toward what the mouth
    /// aims at), the Flop's lift and the Thrash's sweep as offsets, second-order fins and rails, the iris and the grate on springs of their own. A body that is still in sight never outruns a heavy
    /// body's speed: when the head has gone far ahead out of sight, the hidden joints stretch and the visible strand keeps pouring in at its own pace. The trail is stepped on fixed ticks of the clock
    /// (so two frame rates give one pose); the head between ticks comes straight from the route. Presentation only: no rule reads it. Allocation free per frame.
    /// </summary>
    public sealed class DeepEndBossRig
    {
        public const int TickRate=60;public const float SpeedLimit=9.5f,HiddenSpeed=46;
        private const int RingSize=4096;public const float RingStep=.05f;
        private readonly Vector3[] ringP=new Vector3[RingSize],ringN=new Vector3[RingSize];private readonly float[] ringS=new float[RingSize];private int ringCount;private long ringEnd;
        private readonly float[] arc=new float[NativeDeepEndBossLook.Spine];
        private readonly Vector3[] raw=new Vector3[NativeDeepEndBossLook.Spine],rawN=new Vector3[NativeDeepEndBossLook.Spine],prev=new Vector3[NativeDeepEndBossLook.Spine];
        /// <summary>This frame's pose in the world (the room's frame plus <see cref="Origin"/>). Spine joints: z toward the mouth, y the back.</summary>
        public readonly BossPose Shown=new BossPose(NativeDeepEndBossLook.Count);
        /// <summary>Each joint's velocity this frame (m/s), when it last was under the surface (the sheets pour for seconds after), whether it is out of the water.</summary>
        public readonly Vector3[] Velocity=new Vector3[NativeDeepEndBossLook.Spine];public readonly float[] EmergedAt=new float[NativeDeepEndBossLook.Spine];
        public readonly float[] BladeScale=new float[NativeDeepEndBossLook.Blades];
        public Vector3 Origin;
        /// <summary>The route the head is on: the preview's script, or the rig's own (filled as the record's moves start).</summary>
        public readonly DeepEndRoute Own=new DeepEndRoute(512);public DeepEndRoute Route;
        private long lastTick=long.MinValue;private float lastNow=float.NaN;private int seenAction=-1,seenPhase=1;private float seenDied=float.NaN,seenSpawned=float.NaN,seenSurface=float.NaN;
        // Springs and second order.
        private BossSpring iris,spin;private BossSecondOrder fold,rail,mouthKick;private float grateAngle;
        private BossSpringQ lookQ;private bool lookReady;private BossSpring lookWeight;
        /// <summary>The mouth's opening 0..1, the grate's turn (rad), the fins' fold 0..1, the rails' spread.</summary>
        public float Iris=>iris.Value;public float GrateAngle=>grateAngle;public float FinFold=>fold.Value;public float RailSpread=>rail.Value;
        /// <summary>The head joint, its facing and speed (for the tool's bosscam and the camera).</summary>
        public Vector3 Root;public float Yaw,Speed;
        /// <summary>The mouth's centre, the way it faces, its joint's frame (the jet's origin, the card's framing).</summary>
        public Vector3 MouthAt{get;private set;}public Vector3 MouthDir{get;private set;}
        /// <summary>How many joints are out of the water; the surface used this frame (room frame).</summary>
        public int OutOfWater{get;private set;}public float Surface{get;private set;}
        public float Time{get;private set;}
        /// <summary>Until this clock the body keeps up with the head however fast it comes (the arrival up the shaft, in sight from above).</summary>
        public float FreeUntil=float.NegativeInfinity;
        /// <summary>The flop's lift this frame (m at its highest), the thrash's sweep 0..1 and its side; the roll about the body (rad).</summary>
        public float Lift{get;private set;}
        private float fxRoll;private float undulate;

        public DeepEndBossRig(){iris.Reset(0);spin.Reset(.5f);fold.Configure(.75f,.42f,0);rail.Configure(1.1f,.34f,0);mouthKick.Configure(1.4f,.5f,0);fold.Reset(.8f);rail.Reset(0);mouthKick.Reset(0);lookWeight.Reset(0);}
        /// <summary>Back to nothing (a new arena).</summary>
        public void Clear(){lastTick=long.MinValue;lastNow=float.NaN;seenAction=-1;seenPhase=1;seenDied=float.NaN;seenSpawned=float.NaN;seenSurface=float.NaN;ringCount=0;Route=null;}

        private void RingAdd(Vector3 p)
        {
            if(ringCount==0){var n0=Vector3.UnitY;ringP[0]=p;ringN[0]=n0;ringS[0]=0;ringEnd=0;ringCount=1;return;}
            int li=(int)(ringEnd%RingSize);var q=ringP[li];float d=Vector3.Distance(p,q);if(d<RingStep)return;
            var tan=(p-q)/d;var n=ringN[li];
            // A rotation-minimizing frame (the double reflection in one step), righted slowly toward the world's up where the path isn't steep.
            n=Vector3.Normalize(n-tan*Vector3.Dot(n,tan));float steep=Math.Abs(tan.Y);float right=(1-steep)*(1-steep)*Math.Min(1,d*.16f);
            var up=Vector3.UnitY-tan*tan.Y;float ul=up.Length();
            // (By an angle round the tangent, never a blend of vectors: a blend of a normal and its opposite would flip it all at once and the trail would hold the pop.)
            if(ul>.05f){up/=ul;float ang=(float)Math.Atan2(Vector3.Dot(Vector3.Cross(n,up),tan),Vector3.Dot(n,up));n=Vector3.Normalize(BossMath.Rotate(BossMath.AxisAngle(tan,ang*right),n));}
            long ne=ringEnd+1;int ni=(int)(ne%RingSize);ringP[ni]=p;ringN[ni]=n;ringS[ni]=ringS[li]+d;ringEnd=ne;if(ringCount<RingSize)ringCount++;
        }
        // The trail's point and normal at arc coordinate `s` (between ring samples; clamped to what it holds).
        private void Trail(float s,Vector3 headNow,float headS,out Vector3 p,out Vector3 n)
        {
            int li=(int)(ringEnd%RingSize);
            if(s>=ringS[li]){float u=headS>ringS[li]+1e-5f?Math.Min(1,(s-ringS[li])/(headS-ringS[li])):0;p=Vector3.Lerp(ringP[li],headNow,u);n=ringN[li];return;}
            long lo=ringEnd-ringCount+1,hi=ringEnd;if(s<=ringS[(int)(lo%RingSize)]){int oi=(int)(lo%RingSize);p=ringP[oi];n=ringN[oi];return;}
            while(hi-lo>1){long m=(lo+hi)/2;if(ringS[(int)(m%RingSize)]<=s)lo=m;else hi=m;}
            int a=(int)(lo%RingSize),b=(int)(hi%RingSize);float f=(s-ringS[a])/Math.Max(1e-5f,ringS[b]-ringS[a]);p=Vector3.Lerp(ringP[a],ringP[b],f);n=Vector3.Lerp(ringN[a],ringN[b],f);
        }
        // How hidden a joint is: 0 in sight, 1 two metres and more out of sight under the tile (or deep in the shaft). The speed a hidden joint may catch up at rises with it, so nothing jumps at the line.
        private static float Hiddenness(Vector3 p,float radius,float surface)
        {
            float floor=NativeDeepEndArena.Floor(p.X,p.Z);return BossMath.Smooth((Math.Max(floor,surface-8.5f)-(p.Y+radius))/2f);
        }
        private void Tick(long tick,float surface,float cancelBefore)
        {
            float t=tick/(float)TickRate,dt=1f/TickRate;Route.At(t,out var h,out var hv);RingAdd(h);
            int li=(int)(ringEnd%RingSize);float head=ringS[li];
            float limit=h.Y>surface-1.5f||t<FreeUntil?Math.Max(SpeedLimit,hv.Length()):SpeedLimit;undulate+=dt*1.5f*(.55f+.45f*BossMath.Clamp01(hv.Length()/5f));if(undulate>4000)undulate-=BossMath.Tau*600;
            for(int i=0;i<NativeDeepEndBossLook.Spine;i++)
            {
                float want=head-i*NativeDeepEndBossLook.Seg,s=arc[i];float hide=Hiddenness(raw[i],NativeDeepEndBossLook.Radius(i*NativeDeepEndBossLook.Seg),surface);
                float next=Math.Min(want,s+BossMath.Lerp(limit,HiddenSpeed,hide)*dt);if(i>0)next=Math.Min(next,arc[i-1]-NativeDeepEndBossLook.Seg*.985f);if(next<s)next=s;arc[i]=next;
                Trail(next,h,head,out raw[i],out rawN[i]);
                if(raw[i].Y<surface-.15f)EmergedAt[i]=t;
            }
        }
        // A fresh start at the clock: the trail laid from the route's last dozen seconds (a jump, a new arena, a late join).
        private void Rebuild(long tick,float surface)
        {
            long from=tick-TickRate*14;ringCount=0;Route.At(from/(float)TickRate,out var h,out _);RingAdd(h);
            for(int i=0;i<arc.Length;i++){arc[i]=-i*NativeDeepEndBossLook.Seg;raw[i]=h-new Vector3(0,i*NativeDeepEndBossLook.Seg,0);rawN[i]=Vector3.UnitZ;EmergedAt[i]=from/(float)TickRate;}
            // A long way behind the start, straight down, so the tail has somewhere to come from.
            ringCount=0;for(int k=48;k>=0;k--)RingAdd(h-new Vector3(0,k*1.4f,0));float top=ringS[(int)(ringEnd%RingSize)];for(int i=0;i<arc.Length;i++)arc[i]=top-i*NativeDeepEndBossLook.Seg;
            for(long k=from+1;k<=tick;k++)Tick(k,surface,float.NegativeInfinity);
        }

        /// <summary>The frame's pose at `now` (seconds, the presentation clock) for the record: `origin` the room's centre in the world, `surface` the tide (room frame), `look` where the mouth aims when it aims.</summary>
        public void Update(float now,float dt,in DeepEndBossView v,DeepEndRoute route,Vector3 origin,float surface,bool hasLook,Vector3 lookAt)
        {
            Origin=origin;Surface=surface;Time=now;FreeUntil=DeepEndBossView.At(v.spawned)?v.spawned+NativeDeepEndBossBeats.Current.Break+.2f:float.NegativeInfinity;
            bool own=route==null;Route=own?Own:route;
            if(own)Observe(v,now,surface);
            long tick=(long)Math.Floor(now*TickRate);
            if(lastTick==long.MinValue||tick<lastTick||tick-lastTick>TickRate*2||ringCount==0)Rebuild(tick,surface);
            else for(long k=lastTick+1;k<=tick;k++)Tick(k,surface,float.NegativeInfinity);
            lastTick=tick;
            int li=(int)(ringEnd%RingSize);Route.At(now,out var hp,out var hv);float headS=ringS[li]+Vector3.Distance(hp,ringP[li]);float extra=headS-ringS[li];
            // The death draws the whole body back along its trail.
            float retract=0;if(DeepEndBossView.At(v.diedAt))retract=Retract(now-v.diedAt);
            float sp=hv.Length();Speed=sp;
            var pos=rawScratch;var nrm=rawNScratch;
            for(int i=0;i<NativeDeepEndBossLook.Spine;i++)
            {
                // Between ticks a joint on the head's heels moves with it; one lagging holds where its tick put it.
                float want=ringS[li]-i*NativeDeepEndBossLook.Seg,lag=want-arc[i];float s=arc[i]+extra*BossMath.Clamp01(1-lag/.3f)-retract;Trail(s,hp,headS,out pos[i],out nrm[i]);
            }
            Layers(now,v,surface,sp,pos,nrm);
            // ---- the neck: the head's last joints bent toward where the mouth aims ----
            var headDir=SafeDir(pos[0]-pos[3],new Vector3(0,0,1));
            float lookW=0;Vector3 lookD=headDir;var backFlat=nrm[2];backFlat.Y=0;Aim(v,now,surface,pos[0],hasLook,lookAt,backFlat,ref lookW,ref lookD);
            float dtl=Math.Max(0,dt);lookWeight.Step(lookW,.32f,dtl);
            if(!lookReady){lookQ.Reset(BossMath.LookRotation(lookD,Vector3.UnitY));lookReady=true;}
            lookQ.Step(BossMath.LookRotation(lookD,Vector3.UnitY),.34f,dtl);var goalDir=BossMath.Rotate(lookQ.Value,new Vector3(0,0,1));
            float w=BossMath.Clamp01(lookWeight.Value);
            if(w>.001f)
            {
                const int K=7;for(int i=K;i>0;i--)
                {
                    var d=SafeDir(pos[i-1]-pos[i],headDir);float share=w*BossMath.Smooth(1-(i-1)/(float)K);var nd=Vector3.Normalize(Vector3.Lerp(d,goalDir,share));pos[i-1]=pos[i]+nd*NativeDeepEndBossLook.Seg;
                }
            }
            // ---- the skull is rigid: its joints on one line ----
            var skull=SafeDir(pos[0]-pos[3],headDir);pos[1]=pos[3]+skull*(NativeDeepEndBossLook.Seg*2);pos[2]=pos[3]+skull*NativeDeepEndBossLook.Seg;pos[0]=pos[3]+skull*(NativeDeepEndBossLook.Seg*3);
            // ---- the frames ----
            float rollBase=RollOf(v,now,sp);
            int above=0;
            for(int i=0;i<NativeDeepEndBossLook.Spine;i++)
            {
                var a=pos[Math.Max(0,i-1)];var b=pos[Math.Min(NativeDeepEndBossLook.Spine-1,i+1)];var tan=SafeDir(a-b,skull);
                var n=nrm[i];n-=tan*Vector3.Dot(n,tan);float nl=n.Length();n=nl>1e-3f?n/nl:SafeUp(tan);
                float roll=rollBase*BossMath.Smooth(1-i/(float)NativeDeepEndBossLook.Spine*.6f)+.22f*(float)Math.Sin(now*.23f+i*.16f)+.08f*(float)Math.Sin(now*.61f+i*.31f+1.3f);
                var q=BossMath.AxisAngle(tan,roll)*BossMath.LookRotation(tan,n);
                var world=pos[i]+origin;Velocity[i]=dt>1e-4f&&!float.IsNaN(lastNow)?(world-prev[i])/dt:Vector3.Zero;prev[i]=world;
                Shown.Position[i]=world;Shown.Rotation[i]=q;if(pos[i].Y>surface)above++;
            }
            OutOfWater=above;Root=Shown.Position[0];Yaw=BossMath.YawOf(BossMath.Rotate(Shown.Rotation[0],new Vector3(0,0,1)));Shown.RootPosition=Root;Shown.RootRotation=Shown.Rotation[0];
            var q0=Shown.Rotation[0];MouthDir=BossMath.Rotate(q0,new Vector3(0,0,1));MouthAt=Root+BossMath.Rotate(q0,NativeDeepEndBossLook.Mouth);
            Parts(v,now,dt,surface,hv);
            lastNow=now;
        }
        private readonly Vector3[] rawScratch=new Vector3[NativeDeepEndBossLook.Spine],rawNScratch=new Vector3[NativeDeepEndBossLook.Spine];
        private static Vector3 SafeDir(Vector3 d,Vector3 fallback){float l=d.Length();return l>1e-4f?d/l:fallback;}
        private static Vector3 SafeUp(Vector3 tan){var u=Vector3.UnitY-tan*tan.Y;float l=u.Length();return l>.05f?u/l:Vector3.Normalize(Vector3.UnitZ-tan*tan.Z);}
        /// <summary>The death's draw back along the trail (m) `age` s after dying: a settle while the mouth closes, then sinking, slowly gathering.</summary>
        public static float Retract(float age){float t=age-DeathSettle;if(t<=0)return 0;return Math.Min(70,.5f*1.15f*t*t+.75f*t);}
        public const float DeathSettle=1.4f;
        // A live record's route: appended as its moves start. (The preview's script calls the same code at its start.)
        private void Observe(in DeepEndBossView v,float now,float surface)
        {
            if(!v.live)return;
            if(DeepEndBossView.At(v.spawned)&&!(v.spawned==seenSpawned)){seenSpawned=v.spawned;DeepEndBossPaths.Arrival(Own,v.spawned,NativeDeepEndBossBeats.Current,v.yaw*BossMath.Deg);lastTick=long.MinValue;}
            if(v.phase!=seenPhase&&DeepEndBossView.At(v.phaseAt)){seenPhase=v.phase;DeepEndBossPaths.PhaseChange(Own,v.phaseAt,surface,v.phase);}
            if(DeepEndBossView.At(v.surfaceAt)&&!(v.surfaceAt==seenSurface)){seenSurface=v.surfaceAt;DeepEndBossPaths.Cruise(Own,v.surfaceAt,new Vector3(v.surfaceX,surface-DeepEndBossPaths.SwimDepth,v.surfaceZ),(uint)v.pathSeed);}
            if(v.actionId!=seenAction&&v.Move>=0){seenAction=v.actionId;DeepEndBossPaths.Append(Own,v,surface);}
            if(DeepEndBossView.At(v.diedAt)&&!(v.diedAt==seenDied)){seenDied=v.diedAt;DeepEndBossPaths.Death(Own,v.diedAt,surface,DeathSettle);}
        }
        // The offsets on the trail: the Flop's lift, the Thrash's sweep, the travelling undulation, the stagger's shudder.
        private void Layers(float now,in DeepEndBossView v,float surface,float speed,Vector3[] pos,Vector3[] nrm)
        {
            float seg=NativeDeepEndBossLook.Seg;Lift=0;
            // The body's axes at each joint come from its own neighbours.
            float swim=BossMath.Clamp01(speed/5f);
            float lift=0,thrash=0;int thrashSide=1;
            if(v.Moving(now,out var m,out float age,out float hit))
            {
                if(m.Id=="flop"){float depth=-Math.Min(surface-DeepEndBossPaths.FlopDepth,-3f)+surface;lift=FlopLift(age,hit,depth,m.Strike,m.Follow+m.Recover);Lift=lift;}
                if(m.Id=="thrash"){thrash=BossMath.Smooth(age/hit)*(1-BossMath.Smooth((age-hit-m.Strike-m.Follow)/m.Recover));thrashSide=v.actionId%2==0?1:-1;}
            }
            if(lift>0)for(int i=0;i<NativeDeepEndBossLook.Spine;i++)
            {
                float s=i*seg;float near=LineWeight(pos[i],v);pos[i].Y+=lift*near*BossMath.Smooth((29f-s)/6f);var ld=new Vector3(v.aimX-v.targetX,0,v.aimZ-v.targetZ);float ll=ld.Length();if(ll>.1f){ld/=ll;pos[i]+=new Vector3(ld.Z,0,-ld.X)*(near*BossMath.Smooth(age/(hit*.6f))*1.05f*(float)Math.Sin(s*.26f+.7f));}
                // On dry deck it lies on the deck (stranded), not in it.
                float floor=NativeDeepEndArena.Floor(pos[i].X,pos[i].Z);float rest=floor+NativeDeepEndBossLook.Radius(s)*.82f;if(pos[i].Y<rest&&pos[i].Y>rest-1.6f&&floor>=-.01f)pos[i].Y+=(rest-pos[i].Y)*BossMath.Smooth((pos[i].Y-(rest-1.6f))/.8f);
            }
            if(thrash>0)
            {
                float a=age-.0f;float sweep=thrashSide*(float)Math.Sin(BossMath.Clamp01((age-m.Anticipation*.3f)/(m.Anticipation*.7f+m.Strike))*BossMath.Pi*.5f+(age>hit?(age-hit)*2.1f:0));
                for(int i=10;i<NativeDeepEndBossLook.Spine;i++)
                {
                    float k=(i-10)/(float)(NativeDeepEndBossLook.Spine-11);var tan=SafeDir(pos[i-1]-pos[i],new Vector3(0,0,1));var side=SafeDir(Vector3.Cross(nrm[i],tan),new Vector3(1,0,0));
                    pos[i]+=side*(thrash*sweep*7.5f*k*k)+new Vector3(0,thrash*1.2f*k,0);
                }
            }
            // A slow sway that travels from the head to the tail, growing toward it; a low wave in the vertical, slower still.
            const float wave=.42f;
            for(int i=4;i<NativeDeepEndBossLook.Spine;i++)
            {
                float s=i*seg,k=BossMath.Smooth((s-3.5f)/10f);float grow=.22f+.5f*BossMath.Clamp01(s/30f);
                var tan=SafeDir(pos[i-1]-pos[Math.Min(NativeDeepEndBossLook.Spine-1,i+1)],new Vector3(0,0,1));var side=SafeDir(Vector3.Cross(nrm[i],tan),new Vector3(1,0,0));
                float ph=s*wave-undulate;pos[i]+=side*(k*grow*(.55f+.45f*swim)*(float)Math.Sin(ph))+new Vector3(0,k*.09f*(float)Math.Sin(ph*.61f+1.1f),0);
            }
            // The stagger: a shudder running down the body.
            if(DeepEndBossView.At(v.staggerAt)&&now>=v.staggerAt&&now<v.staggerAt+v.staggerSeconds)
            {
                float e=now-v.staggerAt,g=(1-e/v.staggerSeconds)*(1-e/v.staggerSeconds)*BossMath.Smooth(e/.08f);
                for(int i=2;i<NativeDeepEndBossLook.Spine;i++)pos[i].Y+=g*.55f*(float)Math.Sin(i*.9f-e*14f)*BossMath.Clamp01(i/8f);
            }
            // The arrival's hang: while it is held at the top a slight breath.
        }
        // How far a joint is under the Flop's line: all of it within the lane, nothing a body's width past it.
        private static float LineWeight(Vector3 p,in DeepEndBossView v)
        {
            var a=new Vector2(v.targetX,v.targetZ);var b=new Vector2(v.aimX,v.aimZ);var d=b-a;float l=d.Length();if(l<.01f)return 1;d/=l;var q=new Vector2(p.X,p.Z)-a;float along=Vector2.Dot(q,d),across=Math.Abs(q.X*d.Y-q.Y*d.X);return BossMath.Smooth((3.5f-across)/1.5f)*BossMath.Smooth((along+11)/3f)*BossMath.Smooth((l+5-along)/3f);
        }
        /// <summary>The Flop's lift (m) at `age` of its record, from the depth it runs at: the whole body rising through the telegraph, a hang at its height over the water, falling onto the surface on the hit; lying there through its recovery; sinking away.</summary>
        public static float FlopLift(float age,float hit,float depth,float strike,float recover)
        {
            const float height=4.7f;float up0=hit*.28f,top=hit*.74f;float rest=depth;
            if(age<=up0)return 0;if(age<top){float u=(age-up0)/(top-up0);return (rest+height)*BossMath.Smoother(u);}
            if(age<hit){float u=(age-top)/(hit-top);return rest+height*(1-u*u);}
            return rest*(1-BossMath.Smooth((age-hit-strike)/Math.Max(.1f,recover)));
        }
        // The mouth's aim, by what plays.
        private void Aim(in DeepEndBossView v,float now,float surface,Vector3 head,bool hasLook,Vector3 lookAt,Vector3 backFlat,ref float weight,ref Vector3 dir)
        {
            if(v.Moving(now,out var m,out float age,out float hit))
            {
                float end=v.duration>0?v.duration:m.Duration;
                if(m.Id=="jet")
                {
                    weight=BossMath.Smooth(age/(hit*.8f))*(1-BossMath.Smooth((age-(hit+m.Strike+m.Follow))/.5f));
                    dir=JetDir(v,age,hit,m,head,surface);return;
                }
                if(m.Id=="drain"){weight=.72f*BossMath.Smooth(age/hit)*(1-BossMath.Smooth((age-(hit+m.Strike))/.8f));float a=(now-v.actionAt)*.42f;dir=Vector3.Normalize(new Vector3((float)Math.Sin(a),-.24f,(float)Math.Cos(a)));return;}
                if(m.Id=="breach"&&age<hit+.3f){weight=.7f*BossMath.Smooth((age-(hit-.6f))/.4f);dir=Vector3.Normalize(new Vector3(dir.X*.3f,1,dir.Z*.3f));return;}
            }
            if(DeepEndBossView.At(v.phaseAt)&&now>=v.phaseAt&&now<v.phaseAt+NativeDeepEndBoss.PhaseChangeSeconds)
            {
                float e=now-v.phaseAt;weight=BossMath.Smooth(e/1.2f)*(1-BossMath.Smooth((e-NativeDeepEndBoss.PhaseChangeSeconds+.6f)/.6f));dir=Vector3.Normalize(new Vector3(0,1,.08f));return;
            }
            if(DeepEndBossView.At(v.diedAt)&&now>=v.diedAt){weight=.86f*BossMath.Smooth((now-v.diedAt)/1.1f);float bl=backFlat.Length();var fwd=bl>.05f?-backFlat/bl:new Vector3(0,0,1);dir=Vector3.Normalize(fwd+new Vector3(0,.18f,0));return;}
            // Above the water with nothing else to do it turns its blind face toward whoever it's after, a little.
            if(hasLook&&head.Y>surface+.8f){var to=lookAt-head;float l=to.Length();if(l>.5f){weight=.38f*BossMath.Clamp01((head.Y-surface-.8f)/1.5f);dir=to/l;}}
        }
        /// <summary>The jet's direction at `age`: toward its first aim, swept by the table's arc toward its last, from where the mouth is; level with the water.</summary>
        public static Vector3 JetDir(in DeepEndBossView v,float age,float hit,in DeepEndBossMove m,Vector3 head,float surface)
        {
            float u=BossMath.Clamp01((age-hit)/Math.Max(.01f,m.Strike));float eased=BossMath.Smooth(u);
            var a=new Vector3(v.targetX-head.X,0,v.targetZ-head.Z);float ya=(float)Math.Atan2(a.X,a.Z);float sweep=m.Arc*BossMath.Deg*(float)(v.actionId%2==0?1:-1);
            float yaw=ya-sweep*.5f+sweep*eased;var flat=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));
            float drop=Math.Max(.5f,head.Y-surface)/Math.Max(8,Math.Min(16,a.Length()));
            return Vector3.Normalize(flat+new Vector3(0,-drop,0));
        }
        // The slow extra roll by what plays: rolled to show its flank on Last Lap, on its side when it is stranded or falling.
        private float RollOf(in DeepEndBossView v,float now,float speed)
        {
            float roll=0;
            if(v.Moving(now,out var m,out float age,out float hit))
            {
                if(m.Id=="lap")roll=-1.05f*BossMath.Smooth((age-hit+.4f)/.8f)*(1-BossMath.Smooth((age-hit-m.Strike)/1f));
                if(m.Id=="flop"){float u=BossMath.Smooth((age-hit*.3f)/(hit*.55f));roll=.62f*u*(1-BossMath.Smooth((age-hit)/1.6f));}
            }
            if(DeepEndBossView.At(v.staggerAt)&&now>=v.staggerAt&&now<v.staggerAt+v.staggerSeconds+.6f){float e=now-v.staggerAt;roll+=1.2f*BossMath.Smooth(e/.4f)*(1-BossMath.Smooth((e-v.staggerSeconds+.3f)/.9f));}
            fxRoll=roll;return roll;
        }
        // The frame of the spine at s (between the joints).
        public void SpineFrame(float s,out Vector3 at,out Quaternion q)
        {
            float f=s/NativeDeepEndBossLook.Seg;int i=(int)Math.Floor(f);if(i<0){i=0;f=0;}if(i>=NativeDeepEndBossLook.Segments){i=NativeDeepEndBossLook.Segments-1;f=NativeDeepEndBossLook.Segments;}f-=i;
            at=Vector3.Lerp(Shown.Position[i],Shown.Position[i+1],f);q=BossMath.Slerp(Shown.Rotation[i],Shown.Rotation[i+1],f);
        }
        /// <summary>A flank lamp's frame in the world: its centre on the coping, the rotation that faces it outward (a lamp's prop faces +z), its scale.</summary>
        public void LampFrame(int lamp,out Vector3 at,out Quaternion q,out float scale)
        {
            NativeDeepEndBossLook.Lamp(lamp,out float s,out int side,out scale);SpineFrame(s,out var c,out var r);
            float th=side*NativeDeepEndBossLook.CopingTheta;var local=NativeDeepEndBossLook.Skin(s,th);var outward=new Vector3(side*(float)Math.Sin(th),(float)Math.Cos(th),0);local+=outward*.055f;
            at=c+BossMath.Rotate(r,local);q=r*BossMath.LookRotation(outward,new Vector3(0,0,1));
        }
        private void Parts(in DeepEndBossView v,float now,float dt,float surface,Vector3 headV)
        {
            float goal=IrisGoal(v,now);iris.Step(goal,goal>iris.Value?.16f:.3f,dt);spin.Step(.45f+5.5f*iris.Value,.5f,dt);grateAngle+=spin.Value*dt;if(grateAngle>1e4f)grateAngle-=BossMath.Tau*1000;
            float falling=headV.Y<-1f?BossMath.Clamp01(-headV.Y/5f):0;bool under=Shown.Position[6].Y-Origin.Y<surface-1;float foldGoal=under?BossMath.Lerp(.62f,1,BossMath.Clamp01(Speed/7f)):falling;fold.Step(foldGoal,dt);
            rail.Step(iris.Value,dt);mouthKick.Step(BossMath.Clamp01(Speed/9f),dt);
            var q0=Shown.Rotation[0];var mouth=Shown.Position[0]+BossMath.Rotate(q0,NativeDeepEndBossLook.Mouth);
            // The grate and throat behind the iris.
            Shown.Position[NativeDeepEndBossLook.Grate]=mouth+BossMath.Rotate(q0,new Vector3(0,0,-NativeDeepEndBossLook.GrateDepth));Shown.Rotation[NativeDeepEndBossLook.Grate]=q0*BossMath.AxisAngle(new Vector3(0,0,1),grateAngle);
            Shown.Position[NativeDeepEndBossLook.Throat]=mouth+BossMath.Rotate(q0,new Vector3(0,0,-NativeDeepEndBossLook.ThroatDepth));Shown.Rotation[NativeDeepEndBossLook.Throat]=q0;
            // The nine blades: each about its pivot on the rim (rotated and drawn in as it opens); the whole iris turns slowly the other way from the grate.
            float open=BossMath.Clamp01(iris.Value),turn=-grateAngle*.18f+open*.9f;
            for(int k=0;k<NativeDeepEndBossLook.Blades;k++)
            {
                float a=BossMath.Tau*k/NativeDeepEndBossLook.Blades+turn;var around=BossMath.AxisAngle(new Vector3(0,0,1),a);
                var pivotLocal=BossMath.Rotate(around,new Vector3(NativeDeepEndBossLook.MouthRadius*.98f,0,0));float scale=.98f*(1-.56f*open);
                var bq=around*BossMath.AxisAngle(new Vector3(0,0,1),open*(-46f*BossMath.Deg));
                // The prop's origin is the mouth's centre for a closed blade: the pivot stays on the rim.
                var origin=pivotLocal+BossMath.Rotate(bq,new Vector3(-NativeDeepEndBossLook.MouthRadius*.98f*scale,0,0));
                origin.Z=-(NativeDeepEndBossLook.BladeDepth+k*NativeDeepEndBossLook.BladeStep);
                int j=NativeDeepEndBossLook.BladeFirst+k;Shown.Position[j]=mouth+BossMath.Rotate(q0,origin);Shown.Rotation[j]=q0*bq;BladeScale[k]=scale;
            }
            // The rails: rooted in the cheeks, spreading as the mouth opens, flexing on their own spring.
            for(int side=1;side>=-1;side-=2)
            {
                var root=NativeDeepEndBossLook.Skin(NativeDeepEndBossLook.RailS,side*NativeDeepEndBossLook.RailTheta);root.Z=-NativeDeepEndBossLook.RailS;
                float spread=side*(rail.Value*.34f-.04f+.07f*mouthKick.Value)+side*.02f*(float)Math.Sin(now*1.3f);var q=q0*BossMath.AxisAngle(Vector3.UnitY,spread);
                int j=side>0?NativeDeepEndBossLook.RailR:NativeDeepEndBossLook.RailL;Shown.Position[j]=Shown.Position[0]+BossMath.Rotate(q0,root);Shown.Rotation[j]=q;
            }
            // The fins: second order, folding along the body as it dives, spreading as it rises.
            SpineFrame(NativeDeepEndBossLook.FinS,out var fc,out var fq);
            for(int side=1;side>=-1;side-=2)
            {
                var root=NativeDeepEndBossLook.Skin(NativeDeepEndBossLook.FinS,side*NativeDeepEndBossLook.FinTheta);
                float yaw=side*fold.Value*34f*BossMath.Deg,flap=side*(.13f*(float)Math.Sin(now*.8f+side)+.1f*BossMath.Clamp01(1-fold.Value));
                var q=fq*BossMath.AxisAngle(Vector3.UnitY,yaw)*BossMath.AxisAngle(new Vector3(0,0,1),flap);
                int j=side>0?NativeDeepEndBossLook.FinR:NativeDeepEndBossLook.FinL;Shown.Position[j]=fc+BossMath.Rotate(fq,root);Shown.Rotation[j]=q;
            }
        }
        // Whether the mouth should be open: the intro's beat, Breach's rise, the Jet and the Drain, phase changes; shut as it dies.
        private float IrisGoal(in DeepEndBossView v,float now)
        {
            if(DeepEndBossView.At(v.diedAt)&&now>=v.diedAt)return 0;
            if(v.Moving(now,out var m,out float age,out float hit))
            {
                if(m.Id=="jet")return age<hit+m.Strike+m.Follow?BossMath.Smooth(age/(hit*.7f)):0;
                if(m.Id=="drain")return age<hit+m.Strike?BossMath.Smooth(age/(hit*.8f)):0;
                if(m.Id=="breach")return age>=hit-.5f&&age<hit+.55f?1:0;
                if(m.Id=="thrash")return .25f;
            }
            if(DeepEndBossView.At(v.phaseAt)&&now>=v.phaseAt&&now<v.phaseAt+NativeDeepEndBoss.PhaseChangeSeconds-.4f)return 1;
            if(DeepEndBossView.At(v.spawned)){var b=NativeDeepEndBossBeats.Current;float a=now-v.spawned;if(a>=b.IrisOpen&&a<b.Hold+.8f)return 1;}
            return 0;
        }
    }
}
