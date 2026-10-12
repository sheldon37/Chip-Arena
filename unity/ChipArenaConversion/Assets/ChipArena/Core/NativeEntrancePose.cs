using System;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    /// <summary>A baked entrance clip (Resources/SourceMotion.json "intro-*": 22 joints x 7 per frame, root position, rotation and scale x 10).</summary>
    public sealed class NativeEntranceClip
    {
        public float[] Values,Roots;public int Frames;public float Duration;
        public NativeEntranceClip(float[] values,float[] roots,int frames,float duration){Values=values;Roots=roots;Frames=frames;Duration=duration;}
        /// <summary>The bake's root at `age` (source frame).</summary>
        public void Root(float age,out Vector3 position,out Quaternion rotation)
        {
            float index=NativeEntranceMath.Clamp01(age/Duration)*(Frames-1);int a=Math.Min(Frames-1,(int)index)*10,b=Math.Min(Frames-1,(int)index+1)*10;float f=index-(int)index;
            position=Vector3.Lerp(new Vector3(Roots[a],Roots[a+1],Roots[a+2]),new Vector3(Roots[b],Roots[b+1],Roots[b+2]),f);
            rotation=Quaternion.Normalize(Quaternion.Slerp(new Quaternion(Roots[a+3],Roots[a+4],Roots[a+5],Roots[a+6]),new Quaternion(Roots[b+3],Roots[b+4],Roots[b+5],Roots[b+6]),f));
        }
    }
    /// <summary>
    /// Presentation only: keyed full-body poses for the procedural entrances. A key holds the root (position, yaw, pitch,
    /// roll), the body drop and Euler XYZ rotations (three.js order, source frame) for twenty joints; channels are sampled
    /// with the intro curve (NativeEntranceMath.Curve: monotone, flowing through `pass` keys, ringing out after `impact`
    /// keys), so every pose is a pure function of time. Built once per entrance from its script.
    /// </summary>
    public sealed class NativeEntranceKeys
    {
        public const int RootX=0,RootY=1,RootZ=2,Yaw=3,Pitch=4,Roll=5,BodyY=6,PlantL=7,PlantR=8,Joints=9,Channels=Joints+20*3;
        /// <summary>The rig bones of the twenty keyed joints (every bone but the toes).</summary>
        public static readonly int[] Bones={0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,18,19,20};
        private static int Slot(int bone){for(int i=0;i<Bones.Length;i++)if(Bones[i]==bone)return Joints+i*3;throw new ArgumentException("no keyed joint "+bone);}
        public float[] Times=new float[0],Values=new float[0];public uint Pass,Impact;private int count;
        private float[] current;
        /// <summary>Starts a key at `t` from `basis` (a full channel set, e.g. Stand), copying it.</summary>
        public NativeEntranceKeys Key(float t,float[] basis)
        {
            Array.Resize(ref Times,count+1);Array.Resize(ref Values,(count+1)*Channels);Times[count]=t;Array.Copy(basis,0,Values,count*Channels,Channels);current=Values;count++;return this;
        }
        private int Row=>(count-1)*Channels;
        public NativeEntranceKeys Root(float x,float y,float z,float yaw){var v=current;int r=Row;v[r+RootX]=x;v[r+RootY]=y;v[r+RootZ]=z;v[r+Yaw]=yaw;return this;}
        public NativeEntranceKeys Spin(float pitch,float roll){int r=Row;current[r+Pitch]=pitch;current[r+Roll]=roll;return this;}
        public NativeEntranceKeys Drop(float bodyY){current[Row+BodyY]=bodyY;return this;}
        /// <summary>How strongly the feet are planted on their stance (0: the keyed legs, 1: IK onto the floor).</summary>
        public NativeEntranceKeys Planted(float weight){current[Row+PlantL]=weight;current[Row+PlantR]=weight;return this;}
        public NativeEntranceKeys Planted(float left,float right){current[Row+PlantL]=left;current[Row+PlantR]=right;return this;}
        public NativeEntranceKeys J(int bone,float x,float y=0,float z=0){int s=Row+Slot(bone);current[s]=x;current[s+1]=y;current[s+2]=z;return this;}
        public NativeEntranceKeys PassKey(){Pass|=1u<<(count-1);return this;}
        public NativeEntranceKeys ImpactKey(){Impact|=1u<<(count-1);return this;}
        /// <summary>The value of channel `c` at `t`.</summary>
        public float At(int c,float t)=>NativeEntranceMath.Curve(Times,Values,c,Channels,t,Pass,Impact);
        public float End=>count>0?Times[count-1]:0;
        /// <summary>A basis: standing tall, arms relaxed (player-intros.ts neutral), feet planted.</summary>
        public static float[] Stand()
        {
            var v=new float[Channels];v[RootY]=.88f;v[PlantL]=1;v[PlantR]=1;
            void S(int bone,float x,float y,float z){int s=Slot(bone);v[s]=x;v[s+1]=y;v[s+2]=z;}
            S(CapturedRig.LArm,-.10f,0,.13f);S(CapturedRig.RArm,-.10f,0,-.13f);S(CapturedRig.LElbow,-.18f,0,0);S(CapturedRig.RElbow,-.18f,0,0);
            S(CapturedRig.LHip,0,0,.1f);S(CapturedRig.RHip,0,0,-.1f);return v;
        }
        /// <summary>A basis: the hero hold (player-intros.ts neon-drop's last key): weight back, a fist at the hip, chin up.</summary>
        public static float[] Hero()
        {
            var v=Stand();void S(int bone,float x,float y,float z){int s=Slot(bone);v[s]=x;v[s+1]=y;v[s+2]=z;}
            S(CapturedRig.Chest,0,-.14f,0);S(CapturedRig.Head,0,.12f,0);S(CapturedRig.RArm,-.33f,0,-.25f);S(CapturedRig.RElbow,-1.28f,0,0);S(CapturedRig.LArm,-.12f,0,.18f);S(CapturedRig.LElbow,-.35f,0,0);
            S(CapturedRig.LHip,0,0,.12f);S(CapturedRig.RHip,0,0,-.12f);v[Yaw]=-.22f;return v;
        }
        public static float[] With(float[] basis,Action<float[]> edit){var v=(float[])basis.Clone();edit(v);return v;}
        public static void Set(float[] v,int bone,float x,float y,float z){int s=Slot(bone);v[s]=x;v[s+1]=y;v[s+2]=z;}
    }
    /// <summary>
    /// Presentation only: the intro rig's pose for a directed entrance. Weapon signatures keep their baked clip (the
    /// choreography, footwork and the hit synced to the sting) and its weapon-intro layer (NativeIntroMotion), sampled on the
    /// entrance's presentation clock (the slow-motion beat); the procedural entrances sample their keys and plant the feet
    /// with leg IK. Allocation-free per frame (fixed scratch, main thread only).
    /// </summary>
    public static class NativeEntrancePose
    {
        /// <summary>Ankles rest this far above the floor top (the soles' depth below the ankle joint).</summary>
        public const float Ankle=.07f;
        private static readonly CapturedRig.Pose fk=new CapturedRig.Pose();
        /// <summary>
        /// The keyed pose at `t` into `pose` (rig frame) and the root (source frame). Feet planted (PlantL/PlantR) are solved
        /// onto `stance` (per foot: ankle x, z, yaw and lift above the floor; world), so a crouch bends the knees
        /// instead of sinking the feet. A living hold (breath, weight shift, head drift) grows in from `idleFrom`.
        /// </summary>
        public static void Keyed(NativeEntranceKeys k,float t,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation,ReadOnlySpan<float> stance,float idleFrom=3.2f,Func<float,Vector3,Vector3> rootAt=null)
        {
            pose.Reset();
            root=new Vector3(k.At(NativeEntranceKeys.RootX,t),k.At(NativeEntranceKeys.RootY,t),k.At(NativeEntranceKeys.RootZ,t));if(rootAt!=null)root=rootAt(t,root);
            rotation=Quaternion.CreateFromYawPitchRoll(k.At(NativeEntranceKeys.Yaw,t),k.At(NativeEntranceKeys.Pitch,t),k.At(NativeEntranceKeys.Roll,t));
            pose.Position[CapturedRig.Body]=new Vector3(0,k.At(NativeEntranceKeys.BodyY,t),0);
            for(int j=0;j<NativeEntranceKeys.Bones.Length;j++)
            {
                int c=NativeEntranceKeys.Joints+j*3,bone=NativeEntranceKeys.Bones[j];
                // Kinetic chain: elbows, chest and head trail the pose they belong to.
                float lag=bone==CapturedRig.LElbow||bone==CapturedRig.RElbow?.05f:bone==CapturedRig.Chest?.03f:bone==CapturedRig.Head?.06f:0;
                pose.Rotation[bone]=CapturedRig.Euler(k.At(c,t-lag),k.At(c+1,t-lag),k.At(c+2,t-lag));
            }
            Idle(pose,t,idleFrom);
            float left=NativeEntranceMath.Clamp01(k.At(NativeEntranceKeys.PlantL,t)),right=NativeEntranceMath.Clamp01(k.At(NativeEntranceKeys.PlantR,t));
            if(stance.Length>=8){if(left>.001f)Plant(pose,root,rotation,stance,true,left);if(right>.001f)Plant(pose,root,rotation,stance,false,right);}
        }
        /// <summary>heroIdle (intro-curves.ts): a slow weight shift, breathing and a settling head, fading in over .6 s from `from`.</summary>
        public static void Idle(CapturedRig.Pose pose,float t,float from)
        {
            float u=t-from;if(u<=0)return;float weight=NativeEntranceMath.Smooth(u/.6f),w=2*(float)Math.PI/3.2f,lagged=Math.Max(0,u-.15f);
            float shift=.018f*(float)Math.Sin(w*u)*weight,roll=.025f*(float)Math.Sin(w*u)*weight,breath=.012f*(float)Math.Sin(2*Math.PI*u/2.6f)*weight;
            pose.Position[CapturedRig.Body]+=new Vector3(shift,0,0);
            pose.Rotation[CapturedRig.Pelvis]=pose.Rotation[CapturedRig.Pelvis]*CapturedRig.Euler(0,0,roll);
            pose.Rotation[CapturedRig.Chest]=pose.Rotation[CapturedRig.Chest]*CapturedRig.Euler(0,0,-roll*1.1f);
            pose.Rotation[CapturedRig.Spine]=pose.Rotation[CapturedRig.Spine]*CapturedRig.Euler(breath,0,0);
            pose.Rotation[CapturedRig.Head]=pose.Rotation[CapturedRig.Head]*CapturedRig.Euler(-breath*.5f,.035f*(float)Math.Sin(w*.5f*lagged+.4f)*weight*NativeEntranceMath.Smooth(u/1.2f),-.02f*(float)Math.Sin(w*lagged)*weight);
        }
        /// <summary>Plants foot `left` at world (x, z) with heading `yaw` by leg IK, its ankle `lift` above where it stands on the
        /// floor, blended with the posed leg by `weight`.</summary>
        public static void PlantFoot(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,float x,float z,float yaw,float weight,float lift=0)
        {
            Span<float> stance=stackalloc float[8];int o=left?0:4;stance[o]=x;stance[o+1]=z;stance[o+2]=yaw;stance[o+3]=lift;Plant(pose,root,rotation,stance,left,weight);
        }
        // Leg `left` onto its world stance (CapturedRig.Leg in the rig frame), blended with the keyed leg by `weight`.
        private static void Plant(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,ReadOnlySpan<float> stance,bool left,float weight)
        {
            var inv=Quaternion.Inverse(rotation);var f=Vector3.Transform(Vector3.UnitZ,rotation);float yaw=(float)Math.Atan2(f.X,f.Z);
            int hip=left?CapturedRig.LHip:CapturedRig.RHip,o=left?0:4;
            var keep0=pose.Rotation[hip];var keep1=pose.Rotation[hip+1];var keep2=pose.Rotation[hip+2];
            var world=new Vector3(stance[o],NativeEntranceStage.FloorY+Ankle+stance[o+3],stance[o+1]);var local=Vector3.Transform(world-root,inv);
            CapturedRig.Leg(pose,left,local,stance[o+2]-yaw);
            if(weight<.999f){pose.Rotation[hip]=Quaternion.Slerp(keep0,pose.Rotation[hip],weight);pose.Rotation[hip+1]=Quaternion.Slerp(keep1,pose.Rotation[hip+1],weight);pose.Rotation[hip+2]=Quaternion.Slerp(keep2,pose.Rotation[hip+2],weight);}
        }
        /// <summary>A planned foot step (world): the foot lifts at Lift, lands at Land on (X, Z) with heading Yaw; H is its lift height.</summary>
        public struct Step{public bool Left;public float Lift,Land,X,Z,Yaw,H;public Step(bool left,float lift,float land,float x,float z,float yaw,float h=.08f){Left=left;Lift=lift;Land=land;X=x;Z=z;Yaw=yaw;H=h;}}
        /// <summary>
        /// The feet at `t` (into: per foot x, z, yaw, lift) from their starting stance `start` (left x, z, yaw, right x, z, yaw)
        /// through `steps`: a stepping foot travels on a smooth arc; a planted one keeps its world spot.
        /// </summary>
        public static void Feet(float t,ReadOnlySpan<float> start,Step[] steps,Span<float> into)
        {
            for(int side=0;side<2;side++)
            {
                bool left=side==0;float x=start[side*3],z=start[side*3+1],yaw=start[side*3+2],lift=0;
                foreach(var s in steps)
                {
                    if(s.Left!=left)continue;if(t>=s.Land){x=s.X;z=s.Z;yaw=s.Yaw;continue;}
                    if(t>s.Lift){float u=(t-s.Lift)/(s.Land-s.Lift),e=NativeEntranceMath.Smooth(u);x+=(s.X-x)*e;z+=(s.Z-z)*e;yaw+=(s.Yaw-yaw)*e;lift=4*s.H*u*(1-u);}
                    break;
                }
                into[side*4]=x;into[side*4+1]=z;into[side*4+2]=yaw;into[side*4+3]=lift;
            }
        }
        /// <summary>Re-solves arm `left` so its wrist reaches the world point `target` (source frame) with the world rotation
        /// `wrist`, blended in by `weight` (the elbow stays on its side).</summary>
        public static void Reach(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,bool left,Vector3 target,Quaternion wrist,float weight)
        {
            if(weight<=.001f)return;pose.World();int hand=left?CapturedRig.LHand:CapturedRig.RHand;var inv=Quaternion.Inverse(rotation);
            var local=Vector3.Transform(target-root,inv);var q=Quaternion.Normalize(inv*wrist);
            if(weight<.999f){local=Vector3.Lerp(pose.WorldPosition[hand],local,weight);q=Quaternion.Slerp(pose.WorldRotation[hand],q,weight);}
            NativeIntroMotion.Reach(pose,left,local,q);
        }
        /// <summary>A weapon signature: the bake and its weapon-intro layer at `time` (the presentation clock) and the bake's root.</summary>
        public static void Baked(string style,NativeEntranceClip clip,float time,CapturedRig.Pose pose,out Vector3 root,out Quaternion rotation)
        {
            NativeIntroMotion.Pose(style,clip.Values,clip.Frames,clip.Duration,time,pose);clip.Root(time,out root,out rotation);
        }
        /// <summary>The world position (source frame) of point `local` on bone `bone` of `pose` under the root.</summary>
        public static Vector3 Point(CapturedRig.Pose pose,Vector3 root,Quaternion rotation,int bone,Vector3 local)=>root+Vector3.Transform(pose.WorldPosition[bone]+Vector3.Transform(local,pose.WorldRotation[bone]),rotation);
        public static Quaternion WorldRotation(CapturedRig.Pose pose,Quaternion rotation,int bone)=>Quaternion.Normalize(rotation*pose.WorldRotation[bone]);
    }
}
