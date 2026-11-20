using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The base of the five generic intros (owner 2026-11-20: "5 new intros ... a much higher quality standard, great animations and
    /// effects, non-block based background and designs ... With great camera work"; docs/INTROS-GENERIC.md): Cloudbreak (the new
    /// default), Whiteout, Slipstream, Threshold and Flashbulb. Weapon-agnostic: empty hands, the body keyed on the intro rig (Keys and
    /// Steps) and drawn on the model. Each has its own modelled set in the local bank ChipArenaIntroGeneric (tools/intros/generic_sets.py,
    /// never committed), authored in Unity space with the hero mark at the origin: <see cref="U"/> and the turn helpers carry its
    /// coordinates into the source frame the set is drawn in. Without the bank each falls back to the dark lit void, tinted to it.
    /// Presentation only: pure functions of the presentation clock, nothing kept between frames, nothing allocated per frame.
    /// </summary>
    public abstract class NativeIntroGenericScript:NativeIntroV2Script
    {
        /// <summary>The five generic intros' set bank (Resources/ChipArenaIntroGeneric.bytes, local).</summary>
        public const string Bank="ChipArenaIntroGeneric";
        protected NativeIntroGenericScript(string id,float total,float cardAt,float hit,int accent):base(id,total,cardAt,hit,accent){}
        public override string SetBank=>Bank;
        /// <summary>The intro's name (the Locker card and the docs; the name card shows the pilot's).</summary>
        public abstract string Title{get;}

        // ---- the body on the model: feet where the model's land ----
        /// <summary>
        /// The drawn model's legs (Runtime VisualIntroPose binds the keyed pose bone for bone; docs/INTROS-V2.md 2b) take the keyed legs'
        /// turns on bones LegScale times as long (the measured skeleton: hip to ankle .851 m against the rig's .5), hung from hips closer
        /// in (.093 m out, not .13) under hips that move LegScale times as far from their rest as the keyed ones do, on the shared root.
        /// So a keyed foot held still while the root travels slid the drawn one back at (LegScale-1) times the root's speed (the walks
        /// moonwalked a quarter of a metre a step), a keyed stride drew 1.7 times as long, and a keyed drop sank the drawn hips 1.7 times
        /// as deep (the kneels sat on the floor with a knee through it). Here every stance and step is written where the MODEL's ankle
        /// goes (on the floor, the source frame; lifts in the model's metres), and each frame the keyed foot is put where the drawn one
        /// lands there; drops are written in the model's metres too (Sink).
        /// </summary>
        public const float LegScale=1.702f;
        /// <summary>The model's pelvis at rest (the rig frame) and its left hip from it (the pelvis's frame; the right mirrored).</summary>
        private static readonly Vector3 ModelPelvis=new Vector3(0,.122f,0),ModelHip=new Vector3(.093f,-.026f,.02f);
        private static readonly Vector3 GamePelvis=CapturedRig.Bones[CapturedRig.Body].Position+CapturedRig.Bones[CapturedRig.Pelvis].Position;
        /// <summary>A drop of the drawn hips by `metres` (the model's) as the keyed body's drop (NativeEntranceKeys.Drop).</summary>
        protected static float Sink(float metres)=>metres/LegScale;
        private readonly float[] plan=new float[8],pitch=new float[2];
        /// <summary>How high (m, the model's) a walking heel rises onto the ball before the foot leaves the floor, and over how long.</summary>
        protected virtual float HeelRise=>.055f;
        protected virtual float RollTime=>.16f;
        public override void Pose(float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            Feet(t,plan,pitch);Shift(t,plan);
            // (First the pose as keyed, for the hips; then the feet put where the model's land, from those hips.)
            NativeEntrancePose.Keyed(Keys,t,pose,out root,out rotation,plan,HeroAt,RootPath,HingedLegs);
            for(int side=0;side<2;side++){float w=Kneel(t,side==0);if(w>.001f){OnKnee(pose,root,rotation,plan,side==0,w);pitch[side]+=(KneelToe-pitch[side])*w;}}
            OnModel(pose,root,rotation,plan,Ground(t)-NativeEntranceStage.FloorY);
            NativeEntrancePose.Keyed(Keys,t,pose,out root,out rotation,plan,HeroAt,RootPath,HingedLegs);
            Roll(pose,pitch);Arms(t,pose,root,rotation);
        }
        /// <summary>How far leg `left` kneels at `t` (0..1; it must be planted then): its knee down on the floor under the hips, the shin
        /// along the floor behind, the toes tucked under (OnKnee), whatever the hips' height; blended with its plan by the weight.</summary>
        protected virtual float Kneel(float t,bool left)=>0;
        /// <summary>The kneeling foot's pitch: the toes tucked under, the sole near upright.</summary>
        protected const float KneelToe=.82f;
        // The model's leg: thigh and shin (m), the knee's centre over the floor when the knee is down, the ankle's over it with the toes
        // tucked, and how far the shin slopes up from the knee to the ankle.
        private const float Thigh=.425f,Shin=.426f,KneeUp=.075f,TuckUp=.165f;
        /// <summary>
        /// Leg `left`'s plan put where its MODEL knee rests on the floor: from the drawn hip (the keyed hips of `pose` as the model's), the
        /// knee straight down (or ahead of it, as far as the thigh needs to reach the floor) on the floor at the hip's side, the ankle a
        /// shin's length behind it along the pelvis's heading (its toes tucked, TuckUp up), blended into the plan by `w`.
        /// </summary>
        private static void OnKnee(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,float[] feet,bool left,float w)
        {
            pose.World();var turn=pose.WorldRotation[CapturedRig.Pelvis];var model=ModelPelvis+(pose.WorldPosition[CapturedRig.Pelvis]-GamePelvis)*LegScale;
            var hip=root+Vector3.Transform(model+Vector3.Transform(left?ModelHip:new Vector3(-ModelHip.X,ModelHip.Y,ModelHip.Z),turn),rotation);
            var f=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rotation*turn));f.Y=0;f=f.LengthSquared()>1e-6f?Vector3.Normalize(f):Vector3.UnitZ;
            float floor=NativeEntranceStage.FloorY,h=hip.Y-(floor+KneeUp),ahead=(float)Math.Sqrt(Math.Max(0,Thigh*Thigh-h*h));
            float rise=TuckUp-KneeUp,back=(float)Math.Sqrt(Math.Max(0,Shin*Shin-rise*rise));
            var ankle=new Vector3(hip.X,0,hip.Z)+f*(ahead-back);int o=left?0:4;
            feet[o]+= (ankle.X-feet[o])*w;feet[o+1]+=(ankle.Z-feet[o+1])*w;feet[o+3]+=((TuckUp-.11f)-feet[o+3])*w;
        }
        /// <summary>Moves this frame's plan (per foot x, z, yaw, lift; the model's ankles) before it is put on the model: Slipstream rides
        /// its feet on the moving roof.</summary>
        protected virtual void Shift(float t,float[] feet){}
        /// <summary>The height of what the feet stand on at `t` (the floor; Slipstream's roof while it rides): the plan's lifts are the
        /// model's above it.</summary>
        protected virtual float Ground(float t)=>NativeEntranceStage.FloorY;
        /// <summary>Whether a foot on the floor may move at `t` (a slide on the boots or a knee, a cut that puts the feet down elsewhere,
        /// a knee snapping down in a landing): Tests/IntroGenericChecks holds every other planted foot of the model still.</summary>
        public virtual bool Sliding(float t)=>false;
        /// <summary>The framework's own feet check (Tests/IntroPoseChecks, FeetSliding) reads the same slides.</summary>
        public override bool FeetSliding(float t)=>Sliding(t);
        /// <summary>
        /// The feet at `t` (per foot x, z, yaw and lift, the model's) from Start through Steps, as NativeEntrancePose.Feet, with a
        /// walking foot's roll: before a step (a swing of .2 s or more over .25 m or more) the heel rises onto the ball (HeelRise over
        /// RollTime), the toe trails as it leaves, and the heel strikes with the toe a little up, flattening as the weight comes on.
        /// `toe` takes each foot's pitch (rad, toe down positive).
        /// </summary>
        protected void Feet(float t,float[] into,float[] toe)
        {
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float x=Start[side*3],z=Start[side*3+1],yaw=Start[side*3+2],lift=0,p=0;float landed=-10;bool rolled=false;
                foreach(var s in Steps)
                {
                    if(s.Left!=left)continue;
                    float swing=s.Land-s.Lift,reach=(float)Math.Sqrt((s.X-x)*(s.X-x)+(s.Z-z)*(s.Z-z));bool walk=swing>=.2f&&reach>=.25f;
                    if(t>=s.Land){x=s.X;z=s.Z;yaw=s.Yaw;landed=s.Land;rolled=walk;continue;}
                    if(t>s.Lift)
                    {
                        float u=(t-s.Lift)/swing,e=NativeEntranceMath.Smooth(u);x+=(s.X-x)*e;z+=(s.Z-z)*e;yaw+=(s.Yaw-yaw)*e;lift=4*s.H*u*(1-u);
                        if(walk){float off=(1-u)*(1-u);lift+=HeelRise*off;p=Toe(HeelRise)*off*(1-u)-.22f*NativeEntranceMath.Smooth((u-.55f)/.45f);}
                    }
                    else if(walk&&t>s.Lift-RollTime){float r=NativeEntranceMath.Smooth((t-(s.Lift-RollTime))/RollTime);lift=HeelRise*r;p=Toe(lift);}
                    break;
                }
                // the heel strike: the toe a little up as it lands, flattening as the weight comes on
                if(rolled&&t>=landed&&t<landed+.14f)p=-.22f*(1-NativeEntranceMath.Smooth((t-landed)/.14f));
                // (a heel off the floor turns about the ball, so the ankle comes forward over it as well as up)
                if(p>0){float f=Ahead(p);x+=(float)Math.Sin(yaw)*f;z+=(float)Math.Cos(yaw)*f;}
                into[side*4]=x;into[side*4+1]=z;into[side*4+2]=yaw;into[side*4+3]=lift;toe[side]=p;
            }
        }
        /// <summary>The model foot's pitch (toe down) that keeps its ball on the floor with the ankle `rise` m up (the measured foot:
        /// the ball .097 m under the ankle and .126 m ahead).</summary>
        private static float Toe(float rise){float a=.097f,b=.126f,want=a+rise;float r=(float)Math.Sqrt(a*a+b*b);return Math.Max(0,(float)(Math.Asin(Math.Min(1,want/r))-Math.Atan2(a,b)));}
        /// <summary>How far the ankle comes forward over the ball when the foot pitches `toe` down (the ball kept where it was).</summary>
        private static float Ahead(float toe)=>.126f-(.126f*(float)Math.Cos(toe)-.097f*(float)Math.Sin(toe));
        /// <summary>The plan's feet (the model's ankles) as the keyed feet that put the drawn ones there, from the keyed hips of `pose`.</summary>
        private static void OnModel(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,float[] feet,float ground)
        {
            pose.World();var inv=Quaternion.Inverse(rotation);var turn=pose.WorldRotation[CapturedRig.Pelvis];
            var model=ModelPelvis+(pose.WorldPosition[CapturedRig.Pelvis]-GamePelvis)*LegScale;
            for(int side=0;side<2;side++)
            {
                int o=side*4;bool left=side==0;var want=Vector3.Transform(new Vector3(feet[o],root.Y,feet[o+1])-root,inv);
                var hip=model+Vector3.Transform(left?ModelHip:new Vector3(-ModelHip.X,ModelHip.Y,ModelHip.Z),turn);
                var k=pose.WorldPosition[left?CapturedRig.LHip:CapturedRig.RHip]+(want-hip)/LegScale;
                var w=root+Vector3.Transform(new Vector3(k.X,0,k.Z),rotation);feet[o]=w.X;feet[o+1]=w.Z;feet[o+3]=feet[o+3]/LegScale+ground;
            }
        }
        /// <summary>A straight walk's root along +z (the world's frame): at rest at `z0` until `t0`, up to speed over `accel` s, steady,
        /// and slowing to rest at `z1` at `t1` over `decel` s (a trapezoid of speed).</summary>
        protected sealed class Track
        {
            public readonly float T0,T1,Z0,Z1,A,D,V;
            public Track(float t0,float t1,float z0,float z1,float accel,float decel){T0=t0;T1=t1;Z0=z0;Z1=z1;A=accel;D=decel;V=(z1-z0)/(t1-t0-.5f*(accel+decel));}
            public float At(float t)
            {
                if(t<=T0)return Z0;if(t>=T1)return Z1;float s=t-T0;if(s<A)return Z0+.5f*V*s*s/A;
                if(t<T1-D)return Z0+V*(.5f*A+s-A);float r=T1-t;return Z1-.5f*V*r*r/D;
            }
        }
        /// <summary>A walk's steps into `steps`: `count` of them, the first with the left foot when `left`, one lifting every `cadence` s
        /// from `from`, each swinging for `swing` s, `width` m either side of the line with its toes `toe` rad out, H `h`; each lands where
        /// `line` puts the hips half way through its stance, so it lands ahead of the hips and leaves the floor behind them.</summary>
        protected static void Walk(System.Collections.Generic.List<NativeEntrancePose.Step> steps,Track line,bool left,int count,float from,float cadence,float swing,float width,float toe,float h)
        {
            for(int i=0;i<count;i++)
            {
                bool l=(i%2==0)==left;float lift=from+i*cadence,land=lift+swing,mid=.5f*(land+from+(i+2)*cadence);
                steps.Add(new NativeEntrancePose.Step(l,lift,land,l?width:-width,line.At(mid),l?toe:-toe,h));
            }
        }
        /// <summary>Each foot pitched by `toe` (toe down positive) with its toes kept level.</summary>
        private static void Roll(CapturedRig.Pose pose,float[] toe)
        {
            for(int side=0;side<2;side++)
            {
                float p=toe[side];if(Math.Abs(p)<1e-4f)continue;int foot=side==0?CapturedRig.LFoot:CapturedRig.RFoot;
                pose.Rotation[foot]=pose.Rotation[foot]*CapturedRig.Rot(CapturedRig.X,p);if(p>0)pose.Rotation[foot+1]=CapturedRig.Rot(CapturedRig.X,-p);
            }
        }

        // ---- the set's frame: authored in Unity space (x mirrored against the source frame) ----
        /// <summary>A point of the set bank's Unity space in the source frame.</summary>
        protected static Vector3 U(float x,float y,float z)=>new Vector3(-x,y,z);
        protected static Vector3 U(Vector3 unity)=>new Vector3(-unity.X,unity.Y,unity.Z);
        /// <summary>A Unity-space turn (a quaternion of the bank's frame) as the source frame's (the runtime mirrors it back).</summary>
        protected static Quaternion UTurn(Quaternion unity)=>new Quaternion(unity.X,-unity.Y,-unity.Z,unity.W);
        /// <summary>A Unity-space yaw about +y (Unity's turn: +z toward +x) as the source frame's rotation.</summary>
        protected static Quaternion UYaw(float yaw)=>Quaternion.CreateFromAxisAngle(Vector3.UnitY,-yaw);
        /// <summary>A Unity-space turn about +z (a ring's spin seen from +z) as the source frame's rotation.</summary>
        protected static Quaternion URoll(float roll)=>Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-roll);
        protected static readonly Quaternion Still=Quaternion.Identity;
        protected static readonly Vector3 White=Vector3.One;

        // ---- the closing frame and the head ----
        /// <summary>The name card's height (a screen share from the top; Runtime/NativeIntroCardScreen.cs centres the name there, its
        /// kicker and line above and below): a band under the feet. The closing frames show the whole figure above it (the crown near
        /// .2 of the height, the soles near .72), so the name sits on the floor in front of the pilot, never on its legs.</summary>
        public const float CardY=.855f;
        /// <summary>A point `distance` m out from the mark toward the pilot's front, turned side*.42 rad toward its left (the
        /// framework's hero angle; side .524 is straight down +z), `height` m up.</summary>
        protected static Vector3 Front(float side,float distance,float height)
        {
            float yaw=NativeProceduralEntrance.Facing+side*.42f;return new Vector3((float)Math.Sin(yaw)*distance,height,(float)Math.Cos(yaw)*distance);
        }
        /// <summary>The closing push for the name, added as the rig's next shot: from `side` (as Hero's) a slow push in to `distance`,
        /// the eye `eye` m up, looking at the mark `look` m up, easing out to stillness; it comes in on a blend of `blend` s.</summary>
        protected void Closing(float at,float side,float distance,float eye,float look,float fov,float blend)
        {
            Rig.Dolly(at,Front(side,distance+.45f,eye-.06f),Front(side,distance,eye),new Vector3(0,look-.04f,0),new Vector3(0,look,0),fov+2,fov)
               .Ease(NativeIntroEase.Out).In(NativeIntroCut.Blend,blend);
        }

        // ---- the camera this frame, for streaks laid onto their head's camera plane ----
        private NativeEntranceView view;private bool viewSet;
        /// <summary>Reads this frame's camera (the rig at `t`, as Camera draws it) before effects that lay long streaks.</summary>
        protected void Look(float t){float settle=Clock(SettleAt);Rig.Evaluate(this,t<settle?t:settle,Context.Reduced,out view);viewSet=true;}
        /// <summary>A streak from `head` back along `axis` (its whole length; any length): one longer than
        /// NativeCacheOpenFx.ExactStreak is laid onto the plane of its head facing this frame's camera (Look first), so its ends
        /// stay on its line however the camera looks.</summary>
        protected void Line(INativeEntranceFx fx,Vector3 head,Vector3 axis,Vector3 color,float width,float taper,float u,float seed)
        {
            if(viewSet&&axis.LengthSquared()>NativeCacheOpenFx.ExactStreak*NativeCacheOpenFx.ExactStreak)
            {
                var f=view.Look-view.Eye;float l=f.Length();
                if(l>1e-5f)
                {
                    f/=l;float ax=axis.X,ay=axis.Y,az=axis.Z;
                    if(NativeCacheOpenFx.OntoHeadPlane(view.Eye.X,view.Eye.Y,view.Eye.Z,f.X,f.Y,f.Z,.2f,head.X,head.Y,head.Z,ref ax,ref ay,ref az,ref taper))axis=new Vector3(ax,ay,az);
                }
            }
            fx.Streak(head,axis,color,width,.01f,1,taper,u,seed);
        }
        /// <summary>The eye of this frame's camera and the point it looks at (after Look).</summary>
        protected Vector3 Eye=>view.Eye;
        protected Vector3 LookAt=>view.Look;
        /// <summary>Whether point p lies within r m of this frame's eye (after Look): an effect that close would fill the lens.</summary>
        protected bool Near(Vector3 p,float r)=>viewSet&&(p-view.Eye).LengthSquared()<r*r;

        // ---- small shared shapes ----
        /// <summary>0..1 on a beat `at`, eased in over `rise` and out over `fall` (a light's or a glow's level).</summary>
        protected static float Swell(float t,float at,float rise,float hold,float fall)=>NativeEntranceMath.Ramp(t,at,at+rise)*(1-NativeEntranceMath.Ramp(t,at+rise+hold,at+rise+hold+fall));
        /// <summary>A slow blink (period `period`, lit for `duty` of it), soft-edged so it never flashes (well under 3 Hz).</summary>
        protected static float Blink(float t,float period,float duty,float phase=0)
        {
            float u=((t+phase)/period)%1f;if(u<0)u+=1;float edge=.08f;
            return NativeEntranceMath.Smooth(u/edge)*(1-NativeEntranceMath.Smooth((u-duty)/edge));
        }
        /// <summary>
        /// The fallback without the bank, in the intro's own light (its Lights never depend on the bank, so the frame is the same
        /// whichever draws): a floor far wider than the fog's reach (no horizon line), a softer skirt under it, and a layered pool
        /// of light where the pilot stands, with a faint glow on the horizon behind.
        /// </summary>
        protected static void Plain(INativeIntroSet set,float t,Vector3 floor,Vector3 pool,float poolLevel,Vector3 horizon)
        {
            float y=NativeEntranceStage.FloorY;
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,y-.03f,0),Quaternion.Identity,new Vector3(900,.06f,900),floor);
            set.Primitive(WeaponShape.Sphere,false,new Vector3(0,y-1.2f,0),Quaternion.Identity,new Vector3(1400,2,1400),floor*.7f);
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.002f,0),Quaternion.Identity,new Vector3(3.6f,.002f,3.6f),pool*(.03f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.003f,0),Quaternion.Identity,new Vector3(2.0f,.002f,2.0f),pool*(.05f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,y+.004f,0),Quaternion.Identity,new Vector3(.95f,.002f,.95f),pool*(.07f*poolLevel));
            set.Primitive(WeaponShape.Sphere,true,new Vector3(0,6,-160),Quaternion.Identity,new Vector3(260,22,.5f),horizon*.04f);
        }
        /// <summary>The fraction `quality` keeps of `n` effect elements (at least `min`).</summary>
        protected static int Thin(int n,float quality,int min=1)=>Math.Max(min,(int)(n*(.35f+.65f*NativeEntranceMath.Clamp01(quality))));
    }
}
