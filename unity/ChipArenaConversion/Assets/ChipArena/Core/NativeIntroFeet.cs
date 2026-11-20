using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's feet on the model (docs/INTROS-V2.md 9c; the framework's form of I2b's NativeIntroGenericScript machinery, claude/intro-
    /// generic a7c8cdec). The drawn model takes the keyed legs' turns (Runtime VisualIntroPose, bone for bone, the feet unplanted) on bones
    /// <see cref="LegScale"/> times as long, hung from hips closer in, under hips that move LegScale times as far from their rest as the
    /// keyed ones, on the shared root. So a keyed foot held still while the root travelled slid the drawn foot back at (LegScale - 1) times
    /// the root's speed (about 1 m/s in a walk, 25 cm a step), and a keyed drop sank the drawn hips 1.7 times as deep (a kneel put a knee
    /// 30 cm through the floor). An entrance that opts in (NativeProceduralEntrance.ModelFeet) writes its stances and steps where the
    /// MODEL's ankles go (world x, z on the floor; lifts in the model's metres) and its drops in the model's metres; each frame the keyed
    /// foot is put where the drawn one then lands (<see cref="OnModel"/>), a walking foot rolls heel to toe (<see cref="Plan"/>), and a
    /// kneeling leg puts the drawn knee on the floor (<see cref="OnKnee"/>). Allocation-free; pure in t.
    /// </summary>
    public static class NativeIntroFeet
    {
        /// <summary>The drawn leg's length over the keyed one's (the measured skeleton: hip to ankle .851 m against the rig's .5).</summary>
        public const float LegScale=1.702f;
        /// <summary>The model's pelvis at rest (the rig frame) and its left hip from it (the pelvis's frame; the right mirrored).</summary>
        public static readonly Vector3 ModelPelvis=new Vector3(0,.122f,0),ModelHip=new Vector3(.093f,-.026f,.02f);
        private static readonly Vector3 GamePelvis=CapturedRig.Bones[CapturedRig.Body].Position+CapturedRig.Bones[CapturedRig.Pelvis].Position;
        /// <summary>A drop of the drawn hips by `metres` (the model's) as the keyed body's (what ModelFeet does to every Drop).</summary>
        public static float Sink(float metres)=>metres/LegScale;
        /// <summary>How high (m, the model's) a walking heel rises onto the ball before the foot leaves the floor, and over how long (s).</summary>
        public const float HeelRise=.055f,RollTime=.16f;
        /// <summary>The kneeling foot's pitch: the toes tucked under, the sole near upright.</summary>
        public const float KneelToe=.82f;
        // The model's leg: thigh and shin (m), the knee's centre over the floor when the knee is down, the ankle's over it with the toes
        // tucked.
        private const float Thigh=.425f,Shin=.426f,KneeUp=.075f,TuckUp=.165f;

        /// <summary>
        /// The feet at `t` (per foot x, z, yaw and lift, the model's ankles) from `start` (left x, z, yaw, right x, z, yaw) through `steps`, as
        /// NativeEntrancePose.Feet, with a walking foot's roll: before a step (a swing of .2 s or more over .25 m or more) the heel rises onto
        /// the ball (HeelRise over RollTime), the toe trails as it leaves, and the heel strikes with the toe a little up, flattening as the
        /// weight comes on. `toe` takes each foot's pitch (rad, toe down positive).
        /// </summary>
        public static void Plan(float t,ReadOnlySpan<float> start,NativeEntrancePose.Step[] steps,Span<float> into,Span<float> toe,float heelRise=HeelRise,float rollTime=RollTime)
        {
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float x=start[side*3],z=start[side*3+1],yaw=start[side*3+2],lift=0,p=0;float landed=-10;bool rolled=false;
                foreach(var s in steps)
                {
                    if(s.Left!=left)continue;
                    float swing=s.Land-s.Lift,reach=(float)Math.Sqrt((s.X-x)*(s.X-x)+(s.Z-z)*(s.Z-z));bool walk=swing>=.2f&&reach>=.25f;
                    if(t>=s.Land){x=s.X;z=s.Z;yaw=s.Yaw;landed=s.Land;rolled=walk;continue;}
                    if(t>s.Lift)
                    {
                        float u=(t-s.Lift)/swing,e=NativeEntranceMath.Smooth(u);x+=(s.X-x)*e;z+=(s.Z-z)*e;yaw+=(s.Yaw-yaw)*e;lift=4*s.H*u*(1-u);
                        if(walk){float off=(1-u)*(1-u);lift+=heelRise*off;p=Toe(heelRise)*off*(1-u)-.22f*NativeEntranceMath.Smooth((u-.55f)/.45f);}
                    }
                    else if(walk&&t>s.Lift-rollTime){float r=NativeEntranceMath.Smooth((t-(s.Lift-rollTime))/rollTime);lift=heelRise*r;p=Toe(lift);}
                    break;
                }
                // the heel strike: the toe a little up as it lands, flattening as the weight comes on
                if(rolled&&t>=landed&&t<landed+.14f)p=-.22f*(1-NativeEntranceMath.Smooth((t-landed)/.14f));
                // (a heel off the floor turns about the ball, so the ankle comes forward over it as well as up)
                if(p>0){float f=Ahead(p);x+=(float)Math.Sin(yaw)*f;z+=(float)Math.Cos(yaw)*f;}
                into[side*4]=x;into[side*4+1]=z;into[side*4+2]=yaw;into[side*4+3]=lift;toe[side]=p;
            }
        }
        /// <summary>The model foot's pitch (toe down) that keeps its ball on the floor with the ankle `rise` m up (the measured foot: the ball
        /// .097 m under the ankle and .126 m ahead).</summary>
        private static float Toe(float rise){float a=.097f,b=.126f,want=a+rise;float r=(float)Math.Sqrt(a*a+b*b);return Math.Max(0,(float)(Math.Asin(Math.Min(1,want/r))-Math.Atan2(a,b)));}
        /// <summary>How far the ankle comes forward over the ball when the foot pitches `toe` down (the ball kept where it was).</summary>
        private static float Ahead(float toe)=>.126f-(.126f*(float)Math.Cos(toe)-.097f*(float)Math.Sin(toe));

        /// <summary>The drawn hip of leg `left` (world, source frame) for the keyed hips of `pose` (World() current).</summary>
        public static Vector3 DrawnHip(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left)
        {
            var turn=pose.WorldRotation[CapturedRig.Pelvis];var model=ModelPelvis+(pose.WorldPosition[CapturedRig.Pelvis]-GamePelvis)*LegScale;
            return root+Vector3.Transform(model+Vector3.Transform(left?ModelHip:new Vector3(-ModelHip.X,ModelHip.Y,ModelHip.Z),turn),rotation);
        }
        /// <summary>The plan's feet (the model's ankles) as the keyed feet that put the drawn ones there, from the keyed hips of `pose`;
        /// `ground` is how far what the feet stand on is above the floor top.</summary>
        public static void OnModel(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,Span<float> feet,float ground)
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
        /// <summary>
        /// Leg `left`'s plan put where its MODEL knee rests on the floor: from the drawn hip (the keyed hips of `pose` as the model's), the knee
        /// straight down (or ahead of it, as far as the thigh needs to reach the floor) on the floor at the hip's side, the ankle a shin's
        /// length behind it along the pelvis's heading (its toes tucked, TuckUp up), blended into the plan by `w`.
        /// </summary>
        public static void OnKnee(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,Span<float> feet,bool left,float w)
        {
            pose.World();var turn=pose.WorldRotation[CapturedRig.Pelvis];var hip=DrawnHip(pose,root,rotation,left);
            var f=Vector3.Transform(Vector3.UnitZ,Quaternion.Normalize(rotation*turn));f.Y=0;f=f.LengthSquared()>1e-6f?Vector3.Normalize(f):Vector3.UnitZ;
            float floor=NativeEntranceStage.FloorY,h=hip.Y-(floor+KneeUp),ahead=(float)Math.Sqrt(Math.Max(0,Thigh*Thigh-h*h));
            float rise=TuckUp-KneeUp,back=(float)Math.Sqrt(Math.Max(0,Shin*Shin-rise*rise));
            var ankle=new Vector3(hip.X,0,hip.Z)+f*(ahead-back);int o=left?0:4;
            feet[o]+=(ankle.X-feet[o])*w;feet[o+1]+=(ankle.Z-feet[o+1])*w;feet[o+3]+=((TuckUp-.11f)-feet[o+3])*w;
        }
        /// <summary>Each foot pitched by `toe` (toe down positive) with its toes kept level.</summary>
        public static void Roll(CapturedRig.Pose pose,ReadOnlySpan<float> toe)
        {
            for(int side=0;side<2;side++)
            {
                float p=toe[side];if(Math.Abs(p)<1e-4f)continue;int foot=side==0?CapturedRig.LFoot:CapturedRig.RFoot;
                pose.Rotation[foot]=pose.Rotation[foot]*CapturedRig.Rot(CapturedRig.X,p);if(p>0)pose.Rotation[foot+1]=CapturedRig.Rot(CapturedRig.X,-p);
            }
        }

        /// <summary>
        /// A walk's steps into `steps` (the model's ankles: use with ModelFeet): `count` steps along heading `yaw` from `from` (the hips' line),
        /// the first with the left foot when `left`, one lifting every `cadence` s from `t0`, each swinging `swing` s, `width` m either side of
        /// the line with its toes `toe` rad out and lifting `h` m; the hips travel `length` m a step at the steady speed of `line` (Track), so
        /// each foot lands ahead of the hips and leaves the floor behind them. A model's natural step is .6-.75 m at .5-.6 s.
        /// </summary>
        public static void Walk(List<NativeEntrancePose.Step> steps,Track line,bool left,int count,float t0,float cadence,float swing,float width=.1f,float toe=.08f,float h=.07f)
        {
            for(int i=0;i<count;i++)
            {
                bool l=(i%2==0)==left;float lift=t0+i*cadence,land=lift+swing,mid=.5f*(land+t0+(i+2)*cadence);
                var at=line.At(mid)+line.Side*(l?width:-width);
                steps.Add(new NativeEntrancePose.Step(l,lift,land,at.X,at.Z,line.Yaw+(l?toe:-toe),h));
            }
        }
        /// <summary>A straight walk's hips (the root's floor point) along heading `Yaw` from `From`: at rest until `T0`, up to speed over `A` s,
        /// steady, and slowing to rest `Length` m on at `T1` over `D` s (a trapezoid of speed). Key the root from it (Root(At(t)...)) or use
        /// it as RootPath.</summary>
        public sealed class Track
        {
            public readonly float T0,T1,Length,A,D,V,Yaw;public readonly Vector3 From,Dir,Side;
            public Track(Vector3 from,float yaw,float t0,float t1,float length,float accel,float decel)
            {
                From=new Vector3(from.X,0,from.Z);Yaw=yaw;T0=t0;T1=t1;Length=length;A=accel;D=decel;V=length/(t1-t0-.5f*(accel+decel));
                Dir=new Vector3((float)Math.Sin(yaw),0,(float)Math.Cos(yaw));Side=new Vector3(Dir.Z,0,-Dir.X);
            }
            /// <summary>How far along the line the hips are at `t`.</summary>
            public float Along(float t)
            {
                if(t<=T0)return 0;if(t>=T1)return Length;float s=t-T0;if(s<A)return .5f*V*s*s/A;
                if(t<T1-D)return V*(.5f*A+s-A);float r=T1-t;return Length-.5f*V*r*r/D;
            }
            /// <summary>The hips' floor point at `t`.</summary>
            public Vector3 At(float t)=>From+Dir*Along(t);
        }
    }
}
