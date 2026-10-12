using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Unity-owned footwork of the captured melee swings (owner 2026-10-12: "add more realism to the basic attack animation.
    // Currently~ They slide across with their feet on the ground", docs/MELEE-FEEL.md "Footwork"). Presentation only: the
    // body's travel stays the authority's simulated step (StepAt/StepUntil/StepDistance), never root motion; this only places
    // the feet under it.
    // A foot touching the floor never moves along it. It is pinned in world space on its ball (planted flat, or with its heel
    // raised to push off or to pivot through a turn) or on its heel (a heel strike rolling down flat). Otherwise it is in the
    // air on a step: lifted off its ball, carried on an arc that clears the floor, and landing heel first on a target set from
    // where the body will be when it lands (the simulated travel still to come, scaled by the travel actually observed, so a
    // step blocked by a wall lands where the body stopped).
    // Steps follow the swing's step window: the lead foot steps into the cut and plants as early in the hit window as the leg
    // reaches; the rear foot pushes off on its toes while the hips can still reach it, then follows with a recovery step that
    // re-squares the stance behind the lead. A long step (a lock-on opener, a chained lunge, a greatsword's default step)
    // strides with the rear foot first, then plants the lead. A swing that does not travel keeps both feet planted.
    public sealed class MeleeFootwork
    {
        // The sole about the ankle (CapturedRig's foot and toe boxes): the toe joint (the ball the heel lifts about, its toes
        // bent back flat on the floor), the heel's back edge, the sole's depth below the ankle and the toe box's front-bottom edge.
        public const float BallY=-.02f,BallZ=.09f,HeelZ=-.135f,Sole=.07f,ToeTipY=-.05f,ToeTipZ=.155f;
        private const float FLOOR=MeleeRig.FLOOR;
        // Below this simulated step a swing keeps its feet planted; from this one the long-step (stride, plant) plan is used.
        public const float StandStep=.12f,LongStep=1.15f;
        // A step's sole clears the floor by at least this through its travel.
        public const float Clearance=.055f;
        // Per-weapon footwork: how far ahead of the body the lead ankle can plant (ReachF) and how far behind its hip a planted
        // ankle can push on its toes before the leg runs out (Hold); the lead's plant past its stance spot; the follow step's
        // landing as a share of the recovery (Rho); the longest flight (both feet off) a single step may take before the stride
        // plan is used; the step lift; the heel strike's roll to flat; the follow step's shortest flight; the hips' drop at the
        // plant (the shield's offsets its capture rising through the bash), its hold and rise share of the recovery; the cut's
        // stance width. The greatsword is the heaviest: the deepest, longest plant and the slowest recovery step.
        public struct Tune{public float ReachF,Hold,LeadExtra,Rho,MaxFlight,Lift,Roll,FollowDur,Drop,DropHold,DropRise,Width;}
        public static Tune For(string spec)=>spec=="greatsword"?new Tune{ReachF=.40f,Hold=.40f,LeadExtra=.08f,Rho=.42f,MaxFlight=.045f,Lift=.09f,Roll=.09f,FollowDur=.18f,Drop=.055f,DropHold=.12f,DropRise=.6f,Width=.21f}
            :spec=="daggers"?new Tune{ReachF=.36f,Hold=.38f,LeadExtra=.06f,Rho=.3f,MaxFlight=.07f,Lift=.07f,Roll=.05f,FollowDur=.1f,Drop=.035f,DropHold=.04f,DropRise=.5f,Width=.21f}
            :spec=="shield"?new Tune{ReachF=.34f,Hold=.37f,LeadExtra=.07f,Rho=.28f,MaxFlight=.06f,Lift=.07f,Roll=.06f,FollowDur=.12f,Drop=.075f,DropHold=.06f,DropRise=.5f,Width=.2f}
            :new Tune{ReachF=.36f,Hold=.38f,LeadExtra=.07f,Rho=.22f,MaxFlight=.07f,Lift=.08f,Roll=.06f,FollowDur=.12f,Drop=.03f,DropHold=.05f,DropRise=.45f,Width=.2f};

        // One swing as the footwork reads it (absolute times).
        // Sprint: a sprint attack (Core/NativeSprintAttackVisuals): a longer, lower lunge and a heavier decel; the greatsword leaps
        // with the authority's hop (NativeSprintAttack.Hop).
        public struct Swing{public float At,StepAt,StepUntil,Distance,HitAt,HitUntil,End;public bool LeadLeft,Sprint;public string Spec;}
        // A foot as posed: the ankle in group space, the foot's group heading, pitch (+ heel up) and toe bend; Contact while the
        // sole touches the floor; Landing: how much a lifted foot still needs the hips low (leaving the floor or coming down, 0..1).
        public struct FootPose{public Vector3 Ankle;public float Yaw,Pitch,Toe,Landing;public bool Contact;}
        // The shown feet a swing starts from (world, the source frame): ankle (y in group space), heading, pitch, velocity.
        public struct Shown{public Vector3 Ankle,Velocity;public float Yaw,Pitch;}

        private enum Kind{Lead,Stride,Follow,Catch}
        private struct Step{public float Lift,Land,X,Z,Yaw,Roll,Strike;public Kind Kind;public bool Motion,Reach;}
        private enum Mode{Planted,Flight,Roll}
        private sealed class Foot
        {
            public Mode Mode;public Vector2 Ball;public float Yaw,Pitch,Toe;public int Next,Count;public readonly Step[] Steps=new Step[4];
            // the flight: lift time, landing time, the pose it left from (ankle xz, group height), its velocity, the step's live target
            public float LiftAt,LandAt,FromY,FromYaw,FromPitch,FromToe,Height;public Vector2 From,FromVel,To;public float ToYaw;public int Flying;
            // the heel strike: when it touched, its pitch and roll time
            public float RollAt,RollPitch,RollTime;
            // a pivot on the ball (the body turning over a planted foot): its heel envelope
            public bool Pivoting;public float Pivot;
            public void Add(Step s){if(Count<Steps.Length)Steps[Count++]=s;}
        }
        private readonly Foot[] feet={new Foot(),new Foot()};
        private Swing swing;private Tune tune;private Vector2 startBody;private float last=float.NaN,distance,leadZ;private int lead;private bool stand,leap;
        // The leap's tuck: how far the lead and the trailing foot draw up under the airborne body at the flight's middle (m).
        public const float LeapTuck=.26f,LeapTrail=.2f;
        // The stepping foot's touchdown (scheduled until it lands); NaN for a swing that does not step.
        public float PlantAt{get;private set;}=float.NaN;
        public bool Long{get;private set;}

        private static Vector2 Fwd(float yaw)=>new Vector2((float)Math.Sin(yaw),(float)Math.Cos(yaw));
        private static Vector2 Lat(float yaw)=>new Vector2((float)Math.Cos(yaw),-(float)Math.Sin(yaw));
        private float Travel(float t){float span=Math.Max(.01f,swing.StepUntil-swing.StepAt);return distance*MeleeMath.Smooth((t-swing.StepAt)/span);}
        // The time the simulated travel reaches x (the inverse of its smoothstep).
        private float TravelAt(float x)
        {
            if(distance<=1e-4f||x<=0)return swing.StepAt;if(x>=distance)return swing.StepUntil;
            float y=x/distance,u=.5f-(float)Math.Sin(Math.Asin(1-2*y)/3);return swing.StepAt+u*Math.Max(.01f,swing.StepUntil-swing.StepAt);
        }
        // The ankle over a ball (heel up by pitch >= 0) or over a heel (toe up, pitch <= 0) pinned on the floor: its offset
        // along the heading and its group height.
        public static float BallAnkleZ(float pitch)=>-BallY*(float)Math.Sin(pitch)-BallZ*(float)Math.Cos(pitch);
        public static float BallAnkleY(float pitch)=>FLOOR+BallY-BallY*(float)Math.Cos(pitch)+BallZ*(float)Math.Sin(pitch);
        public static float HeelAnkleZ(float pitch)=>Sole*(float)Math.Sin(pitch)-HeelZ*(float)Math.Cos(pitch);
        public static float HeelAnkleY(float pitch)=>FLOOR-Sole+Sole*(float)Math.Cos(pitch)+HeelZ*(float)Math.Sin(pitch);
        // The sole's lowest point below the ankle at a pitch and toe bend (heel, ball and toe tip edges).
        public static float Lowest(float pitch,float toe)
        {
            float c=(float)Math.Cos(pitch),s=(float)Math.Sin(pitch),ct=(float)Math.Cos(pitch+toe),st=(float)Math.Sin(pitch+toe);
            float heel=-Sole*c-HeelZ*s,ball=-Sole*c-BallZ*s,tip=BallY*c-BallZ*s+ToeTipY*ct-ToeTipZ*st;
            return Math.Min(heel,Math.Min(ball,tip));
        }

        // ---- the plan, made once as the swing starts ----
        public void Begin(in Swing s,Vector3 body,float yaw,Shown left,Shown right)
        {
            swing=s;tune=For(s.Spec);last=s.At;
            if(s.Sprint){tune.LeadExtra+=.05f;tune.Drop*=1.5f;tune.DropHold+=.04f;tune.Rho=Math.Min(.6f,tune.Rho+.12f);if(s.Spec=="daggers")tune.Lift*=.75f;}startBody=new Vector2(body.X,body.Z);distance=Math.Max(0,s.Distance);lead=s.LeadLeft?0:1;
            var st=MeleeStance.For(s.Spec);float sl=s.LeadLeft?1:-1;leadZ=st.LeadZ+tune.LeadExtra;
            // The cut's stance (the lead foot forward on the cut's side, the rear behind it, toes out as the guard turns them).
            float lx=sl*tune.Width,ly=sl*st.LeadToe,rx=-sl*Math.Abs(st.RearX),rz=st.RearZ,ry=-sl*st.RearToe;
            for(int i=0;i<2;i++)Seat(feet[i],i==0?left:right,yaw);
            var L=feet[lead];var R=feet[1-lead];
            Vector2 Local(Vector2 w){Vector2 d=w-startBody;return new Vector2(Vector2.Dot(d,Lat(yaw)),Vector2.Dot(d,Fwd(yaw)));}
            float zbL=Local(Ankle(L)).Y,zbR=Local(Ankle(R)).Y;stand=distance<StandStep;Long=false;PlantAt=float.NaN;leap=false;
            if(!stand)
            {
                float early=swing.At+.01f,leadLift=Math.Max(early,s.StepAt-.35f*(s.StepAt-s.At)),plantReach=TravelAt(distance+leadZ-tune.ReachF);
                float plant=Math.Max(Math.Max(plantReach,s.HitAt-.03f),leadLift+.09f),force=zbR+tune.Hold<distance?TravelAt(zbR+tune.Hold):float.PositiveInfinity;
                float flight=force<plant?plant-force:0;Long=distance>=LongStep||flight>tune.MaxFlight;
                float follow=Math.Max(s.HitUntil+tune.Rho*(s.End-s.HitUntil),0);
                if(s.Sprint&&NativeSprintAttackRules.Leaps(s.Spec))
                {
                    // The leap (the authority hops the body as the swing starts and brings it down with the hit window): both
                    // feet push off at once and ride tucked under the body, the lead reaching forward; they land as the body
                    // comes down (held until it is), the lead first and the rear a moment after, squared behind it.
                    float land=Math.Max(s.At+.12f,s.HitAt-.015f);Long=leap=true;
                    L.Add(new Step{Lift=s.At,Land=land,X=lx,Z=leadZ,Yaw=ly,Kind=Kind.Lead,Roll=tune.Roll,Strike=-.24f});
                    R.Add(new Step{Lift=s.At+.02f,Land=land+.03f,X=rx,Z=rz,Yaw=ry,Kind=Kind.Stride,Roll=.07f,Strike=-.12f});
                    PlantAt=land;
                }
                else if(!Long)
                {
                    L.Add(new Step{Lift=leadLift,Land=plant,X=lx,Z=leadZ,Yaw=ly,Kind=Kind.Lead,Motion=true,Reach=true,Roll=tune.Roll,Strike=-.28f});
                    float lift=Math.Max(plant+.03f,s.HitUntil-.06f),land=Math.Min(Math.Max(follow,Math.Max(lift+tune.FollowDur,plant+.06f)),Math.Max(lift+.08f,s.End-.04f));
                    R.Add(new Step{Lift=lift,Land=land,X=rx,Z=rz,Yaw=ry,Kind=Kind.Follow,Reach=true,Roll=.06f,Strike=-.14f});
                    PlantAt=plant;
                }
                else
                {
                    // The stride: the rear foot passes the lead and lands ahead of the hips while the lead still holds, so the two
                    // flights (both feet off) around it are as short as the leg's reach allows.
                    const float ahead=.2f;float x1=zbL+tune.Hold,x2=distance+leadZ-tune.ReachF,trA=MeleeMath.Clamp((x1+x2-ahead-tune.Hold)/2,.15f*distance,.65f*distance);
                    float strideLift=Math.Max(early,s.StepAt-.3f*(s.StepAt-s.At)),strideLand=Math.Max(TravelAt(trA),strideLift+.08f);
                    float plantL=Math.Max(Math.Max(TravelAt(x2),s.HitAt-.03f),strideLand+.05f),liftL=Math.Max(strideLand-.04f,Math.Min(TravelAt(x1),plantL-.09f));
                    // A fast stride lands nearly flat and rolls down within its short contact.
                    float contact=Math.Max(.02f,TravelAt(trA+ahead+tune.Hold)-strideLand),roll=MeleeMath.Clamp(.35f*contact,.02f,.05f);
                    R.Add(new Step{Lift=strideLift,Land=strideLand,X=rx*.7f,Z=ahead,Yaw=ry*.5f,Kind=Kind.Stride,Motion=true,Reach=true,Roll=roll,Strike=-.2f*MeleeMath.Clamp(contact/.12f,.4f,1)});
                    L.Add(new Step{Lift=liftL,Land=plantL,X=lx,Z=leadZ,Yaw=ly,Kind=Kind.Lead,Reach=true,Roll=tune.Roll,Strike=-.28f});
                    float lift=Math.Max(plantL+.03f,s.HitUntil-.06f),land=Math.Min(Math.Max(follow,Math.Max(lift+tune.FollowDur,plantL+.06f)),Math.Max(lift+.08f,s.End-.04f));
                    R.Add(new Step{Lift=lift,Land=land,X=rx,Z=rz,Yaw=ry,Kind=Kind.Follow,Reach=true,Roll=.06f,Strike=-.14f});
                    PlantAt=plantL;
                }
            }
            // A foot that starts in the air (a run's stride, a chain's recovery step) carries straight into its first step when
            // that leaves at once, else comes down first on a short catch step under the hip.
            for(int i=0;i<2;i++)
            {
                var f=feet[i];if(f.Mode!=Mode.Flight)continue;
                float catchLand=s.At+MeleeMath.Clamp(.06f+.8f*(f.FromY-FLOOR),.06f,.12f);
                if(f.Count>0&&f.Steps[0].Lift<=s.At+.1f&&(leap||f.Steps[0].Land<=s.At+.3f)){f.Flying=0;f.Next=1;f.LiftAt=s.At;f.LandAt=Math.Max(f.Steps[0].Land,s.At+.06f);f.Height=Height(f.Steps[0]);continue;}
                Vector2 ahead=Local(f.From+f.FromVel*.05f);float side=i==0?1:-1;
                var c=new Step{Lift=s.At,Land=catchLand,X=side*Math.Abs(i==lead?lx:rx),Z=MeleeMath.Clamp(ahead.Y-Travel(catchLand),-.22f,.28f),Yaw=i==lead?ly:ry,Kind=Kind.Catch,Roll=.04f,Strike=-.08f};
                Array.Copy(f.Steps,0,f.Steps,1,Math.Min(f.Count,f.Steps.Length-1));f.Steps[0]=c;f.Count=Math.Min(f.Count+1,f.Steps.Length);
                f.Flying=0;f.Next=1;f.LiftAt=s.At;f.LandAt=catchLand;f.Height=.03f;
            }
        }
        // A shown foot as the swing starts: on the floor (pinned on its ball, or on its heel while it rolls down), or in the air.
        private void Seat(Foot f,Shown s,float yaw)
        {
            f.Count=0;f.Next=0;f.Pivoting=false;f.Pivot=0;f.Yaw=s.Yaw;f.Toe=0;
            float pitch=MeleeMath.Clamp(s.Pitch,-.6f,1.2f),clear=s.Ankle.Y-FLOOR+Sole+Lowest(pitch,pitch>0?-pitch:0);Vector2 a=new Vector2(s.Ankle.X,s.Ankle.Z),fw=Fwd(s.Yaw);
            if(clear<.015f&&pitch>=-.02f){f.Mode=Mode.Planted;f.Pitch=Math.Max(0,pitch);f.Ball=a-fw*BallAnkleZ(f.Pitch);}
            else if(clear<.015f){f.Mode=Mode.Roll;f.RollAt=swing.At;f.RollPitch=pitch;f.RollTime=.05f;f.Pitch=pitch;f.Ball=a-fw*HeelAnkleZ(pitch)+fw*(BallZ-HeelZ);}
            else{f.Mode=Mode.Flight;f.From=a;f.FromY=s.Ankle.Y;f.FromYaw=s.Yaw;f.FromPitch=pitch;f.FromToe=0;f.FromVel=new Vector2(s.Velocity.X,s.Velocity.Z);if(f.FromVel.Length()>12)f.FromVel*=12/f.FromVel.Length();}
        }
        private float Height(in Step s)=>leap?(s.Kind==Kind.Lead?LeapTuck:LeapTrail):s.Kind==Kind.Catch?.03f:s.Kind==Kind.Follow?tune.Lift*.75f:s.Kind==Kind.Stride?tune.Lift:tune.Lift*(1+.4f*MeleeMath.Clamp(distance/1.2f,0,1));
        private static float MinFlight(Kind k)=>k==Kind.Lead?.09f:k==Kind.Stride?.08f:k==Kind.Follow?.1f:.05f;

        // ---- the pose at `time` (world body position and heading, the source frame); out: each foot by side (0 left) ----
        private float Progress(Vector2 body,float time)
        {
            float expected=Travel(time);if(expected<.04f)return 1;
            return MeleeMath.Clamp(Vector2.Distance(body,startBody)/expected,0,1);
        }
        // Where the body is at `at` (ahead of `time`, or a moment before it at a touchdown between frames).
        private Vector2 Predict(Vector2 body,float yaw,float time,float at,float k)=>body+Fwd(yaw)*(k*(Travel(at)-Travel(time)));
        private Vector2 Target(Vector2 body,float yaw,float time,float k,in Step s,float land)=>Predict(body,yaw,time,land,k)+Lat(yaw)*s.X+Fwd(yaw)*s.Z;
        // hips: each hip in group space (0 left), from which a planted ankle's reach is measured. air: the body's height over
        // the floor it left (the leap's hop; 0 while grounded): a leap's foot on the floor stays on it, and its feet land as the
        // body does.
        public void Pose(float time,Vector3 body3,float yaw,Vector3[] hips,FootPose[] o,float air=0)
        {
            air=MathUtil.Finite(air)?Math.Max(0,air):0;
            float t=float.IsNaN(last)?time:Math.Max(time,last),dt=float.IsNaN(last)?0:MeleeMath.Clamp(t-last,0,.05f);last=t;
            Vector2 body=new Vector2(body3.X,body3.Z),fw=Fwd(yaw);float k=Progress(body,t),moved=Vector2.Distance(body,startBody);
            for(int i=0;i<2;i++)
            {
                var f=feet[i];
                // lift-off: at its time (a step that carries the body waits until the body moves), or as the hips leave it behind
                if(f.Mode!=Mode.Flight&&f.Next<f.Count)
                {
                    // (looking a frame ahead: at a lunge's peak the hips cover 10 cm a frame)
                    ref var s=ref f.Steps[f.Next];float behind=Behind(f,i,body,yaw,hips,out float hold)+k*(Travel(t+Frame)-Travel(t));
                    bool go=f.Mode==Mode.Planted&&t>=s.Lift&&(!s.Motion||moved>=Math.Min(.03f,.25f*distance)),forced=s.Reach&&behind>=hold+(f.Mode==Mode.Roll?.02f:0)||leap&&air>LeapLeave;
                    if(go||forced)
                    {
                        float p=f.Pitch;Vector2 a=Ankle(f);float y=(f.Mode==Mode.Roll?RollY(f,t,out p,out a):BallAnkleY(p))-(leap?air:0);
                        f.Mode=Mode.Flight;f.Flying=f.Next++;f.From=a;f.FromY=y;f.FromYaw=f.Yaw;f.FromPitch=p;f.FromToe=f.Toe;f.FromVel=Vector2.Zero;
                        f.LiftAt=t;f.LandAt=Math.Max(s.Land,t+MinFlight(s.Kind));f.Height=Height(s);
                        if(s.Kind==Kind.Lead)PlantAt=f.LandAt;
                    }
                }
                if(f.Mode==Mode.Flight)
                {
                    ref var s=ref f.Steps[f.Flying];f.ToYaw=yaw+s.Yaw;f.To=Target(body,yaw,t,k,s,f.LandAt)+Fwd(f.ToYaw)*BallZ;
                    // (a leap's foot held up by a body still coming down strikes as it touches)
                    if(t>=f.LandAt&&!(leap&&air>LeapDown)){float touch=leap&&t>f.LandAt+Frame?t:f.LandAt;f.Mode=Mode.Roll;f.Ball=f.To;f.Yaw=f.ToYaw;f.RollAt=touch;f.RollPitch=s.Strike;f.RollTime=s.Roll;f.Toe=0;if(s.Kind==Kind.Lead)PlantAt=touch;}
                }
                if(f.Mode==Mode.Roll&&t>=f.RollAt+f.RollTime){f.Mode=Mode.Planted;f.Pitch=0;}
                o[i]=PoseOf(f,i,t,dt,body,yaw,hips);
                // A leap's foot still on the floor as the body leaves it stays on the floor (the push-off frame of the hop).
                if(leap&&o[i].Contact)o[i].Ankle.Y-=air;
            }
        }
        // A foot on the floor lifts once the body is this high over it; a leap's feet touch down once it is this low (m).
        public const float LeapLeave=.04f,LeapDown=.02f;
        private static Vector2 Ankle(Foot f)=>f.Mode==Mode.Flight?f.From:f.Ball+Fwd(f.Yaw)*BallAnkleZ(Math.Max(0,f.Pitch));
        // How far a grounded ankle has been left by its hip across the floor (the hip in group space -> world).
        // hold: how far it may be left before it must lift, the tune's Hold or less where the hips stand too high for the leg
        // to reach that far even when seated (MeleeMotion lowers them at most SeatMax).
        public const float SeatMax=.15f,LegReach=MeleeMotion.StepReach,Frame=1/60f;
        private float Behind(Foot f,int side,Vector2 body,float yaw,Vector3[] hips,out float hold)
        {
            Vector3 h=hips[side];float c=(float)Math.Cos(yaw),sn=(float)Math.Sin(yaw);Vector2 hip=body+new Vector2(h.X*c+h.Z*sn,-h.X*sn+h.Z*c);
            float drop=Math.Max(0,h.Y-(FLOOR+.03f)-SeatMax+.02f);hold=Math.Min(tune.Hold,.95f*(float)Math.Sqrt(Math.Max(0,LegReach*LegReach-drop*drop)));
            return Vector2.Distance(Ankle(f),hip);
        }
        // The rolling foot's ankle (heel pinned) and pitch at `t`.
        private static float RollY(Foot f,float t,out float pitch,out Vector2 ankle)
        {
            float u=MeleeMath.Ease((t-f.RollAt)/Math.Max(.01f,f.RollTime));pitch=f.RollPitch*(1-u);Vector2 fw=Fwd(f.Yaw),heel=f.Ball-fw*(BallZ-HeelZ);
            if(f.RollPitch>=0){ankle=f.Ball+fw*BallAnkleZ(pitch);return BallAnkleY(pitch);}
            ankle=heel+fw*HeelAnkleZ(pitch);return HeelAnkleY(pitch);
        }
        private FootPose PoseOf(Foot f,int side,float t,float dt,Vector2 body,float yaw,Vector3[] hips)
        {
            var p=new FootPose();Vector2 ankle;float y;
            if(f.Mode==Mode.Flight)
            {
                float span=Math.Max(.01f,f.LandAt-f.LiftAt),u=MeleeMath.Clamp((t-f.LiftAt)/span,0,1);ref var s=ref f.Steps[f.Flying];float strike=s.Strike;
                Vector2 fw1=Fwd(f.ToYaw),heel=f.To-fw1*(BallZ-HeelZ),end=heel+fw1*HeelAnkleZ(strike);float endY=HeelAnkleY(strike);
                // a foot leaving the floor rises before it travels and comes down onto its spot (an airborne one carries its speed)
                bool air=f.FromVel.LengthSquared()>1e-4f;float h=air?MeleeMath.Clamp(u/.86f,0,1):MeleeMath.Clamp((u-.06f)/.8f,0,1),m=air?.86f*span:0;
                ankle=new Vector2(MeleeMath.Hermite(f.From.X,end.X,f.FromVel.X*m,0,h),MeleeMath.Hermite(f.From.Y,end.Y,f.FromVel.Y*m,0,h));
                p.Yaw=f.FromYaw+MeleeMath.Wrap(f.ToYaw-f.FromYaw)*MeleeMath.Ease(u);
                p.Pitch=f.FromPitch*(1-MeleeMath.Smooth(u/.5f))+strike*MeleeMath.Smooth((u-.5f)/.5f);p.Toe=f.FromToe*(1-MeleeMath.Smooth((u-.08f)/.4f));
                // the arc, kept clear of the floor by the sole's lowest point at this pitch (none at the ends: the ball leaves the
                // floor, the heel meets it)
                float clear=Clearance*MeleeMath.Smooth(u/.12f)*(1-MeleeMath.Smooth((u-.8f)/.2f));
                y=Math.Max(MeleeMath.Lerp(f.FromY,endY,u)+f.Height*(float)Math.Sin(Math.PI*u),FLOOR-Sole-Lowest(p.Pitch,p.Toe)+clear);
                p.Landing=Math.Max(MeleeMath.Smooth((u-.6f)/.4f),1-MeleeMath.Smooth(u/.3f));p.Contact=false;
            }
            else if(f.Mode==Mode.Roll)
            {
                y=RollY(f,t,out float pitch,out ankle);
                p.Yaw=f.Yaw;p.Pitch=pitch;p.Toe=0;p.Contact=true;p.Landing=1;f.Pitch=Math.Max(0,pitch);
            }
            else
            {
                // Planted: the heel rises to push off (toward the next lift, or as the hips draw away), through the cut's drive on
                // the rear foot, and while the foot pivots on its ball under a turning body.
                Vector2 fw=Fwd(yaw);float pitch=0;bool isLead=side==lead;
                if(f.Next<f.Count)
                {
                    ref var s=ref f.Steps[f.Next];float push=s.Kind==Kind.Lead?.3f:s.Kind==Kind.Stride?.45f:.6f;
                    pitch=push*MeleeMath.Ease((t-(s.Lift-.06f))/.06f);
                    if(s.Reach){float behind=Behind(f,side,body,yaw,hips,out float hold);pitch=Math.Max(pitch,.62f*MeleeMath.Smooth((behind-.14f)/Math.Max(.05f,hold-.14f)));}
                }
                if(!isLead)
                {
                    float a=swing.HitAt-.35f*(swing.HitAt-swing.At),b=swing.HitUntil+.3f*(swing.End-swing.HitUntil);
                    pitch=Math.Max(pitch,.22f*MeleeMath.Smooth((t-a)/Math.Max(.02f,swing.HitAt-a))*(1-MeleeMath.Smooth((t-swing.HitUntil)/Math.Max(.02f,b-swing.HitUntil))));
                }
                // The pivot: once the body has turned well past the foot's heading the heel lifts and the foot turns on its ball
                // after it, then settles flat.
                float want=yaw+(isLead?(swing.LeadLeft?1:-1)*MeleeStance.For(swing.Spec).LeadToe:-(swing.LeadLeft?1:-1)*MeleeStance.For(swing.Spec).RearToe),twist=MeleeMath.Wrap(want-f.Yaw);
                if(Math.Abs(twist)>.45f)f.Pivoting=true;else if(Math.Abs(twist)<.12f)f.Pivoting=false;
                f.Pivot+=((f.Pivoting?1:0)-f.Pivot)*MeleeMath.Clamp(dt/.045f,0,1);
                if(f.Pivot>1e-3f){float turn=Math.Min(Math.Abs(twist),9*f.Pivot*dt);f.Yaw+=Math.Sign(twist)*turn;}
                pitch=Math.Max(pitch,.38f*f.Pivot);f.Pitch=pitch;f.Toe=-pitch;
                Vector2 hf=Fwd(f.Yaw);ankle=f.Ball+hf*BallAnkleZ(pitch);y=BallAnkleY(pitch);
                p.Yaw=f.Yaw;p.Pitch=pitch;p.Toe=f.Toe;p.Contact=true;p.Landing=1;
            }
            // world -> group
            Vector2 d=ankle-body;float c=(float)Math.Cos(yaw),sn=(float)Math.Sin(yaw);
            p.Ankle=new Vector3(d.X*c-d.Y*sn,y,d.X*sn+d.Y*c);p.Yaw=MeleeMath.Wrap(p.Yaw-yaw);
            return p;
        }
    }
}
