using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Source rig-v2 proportions and chest-space IK. Math uses the source frame (+x = left),
    // reflected across x once at the Unity boundary. Animation never writes Actor or ArenaWorld.
    public static class CapturedRig
    {
        public sealed class Bone
        {
            public readonly string Name, Path; public readonly int Parent; public readonly Vector3 Position;
            public Bone(string name,int parent,Vector3 position,string path) { Name=name; Parent=parent; Position=position; Path=path; }
        }
        public struct Box
        {
            public int Bone, Color; public Vector3 Position, Size;
            public Box(int bone,int color,Vector3 position,Vector3 size) { Bone=bone; Color=color; Position=position; Size=size; }
        }
        public const int Body=0, Pelvis=1, Spine=2, Chest=3, Neck=4, Head=5,
            LClav=6,LArm=7,LElbow=8,LHand=9,RClav=10,RArm=11,RElbow=12,RHand=13,
            LHip=14,LKnee=15,LFoot=16,LToe=17,RHip=18,RKnee=19,RFoot=20,RToe=21;
        public static readonly Bone[] Bones=MakeBones();
        public static readonly Box[] Boxes=MakeBoxes();
        public static readonly Vector3 X=Vector3.UnitX,Y=Vector3.UnitY,Z=Vector3.UnitZ;
        public static Vector3 V(float x,float y,float z=0) => new Vector3(x,y,z);
        public static Vector3 Mirror(Vector3 v) => V(-v.X,v.Y,v.Z);
        public static Quaternion Mirror(Quaternion q) => new Quaternion(q.X,-q.Y,-q.Z,q.W);
        public static Quaternion Rot(Vector3 axis,float angle) => Quaternion.CreateFromAxisAngle(axis,angle);
        public static Quaternion Euler(float x,float y=0,float z=0) => Rot(X,x)*Rot(Y,y)*Rot(Z,z); // Three.js XYZ
        public static Vector3 Unit(Vector3 v,Vector3 fallback) => v.LengthSquared()<1e-10f?fallback:Vector3.Normalize(v);
        public static Quaternion Basis(Vector3 x,Vector3 y,Vector3 z) => Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(
            new Matrix4x4(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1)));
        public static Quaternion Look(Vector3 forward,Vector3 up)
        {
            Vector3 z=Unit(forward,Z),x=Unit(Vector3.Cross(up,z),X),y=Vector3.Cross(z,x); return Basis(x,y,z);
        }
        private static Bone[] MakeBones()
        {
            var b=new List<Bone>();
            void Add(string name,int parent,float x,float y,float z=0) { b.Add(new Bone(name,parent,V(x,y,z),(parent<0?"":b[parent].Path+"/")+name)); }
            Add("motion-root",-1,0,0); Add("pelvis",Body,0,-.24f); Add("spine",Pelvis,0,.105f);
            Add("chest",Spine,0,.19f); Add("neck",Chest,0,.32f); Add("head",Neck,0,.1f);
            foreach(bool left in new[]{true,false})
            {
                string side=left?"left":"right"; float sign=left?1:-1; int clav=b.Count;
                Add(side+"-clavicle",Chest,sign*.275f,.18f); Add(side+"-shoulder",clav,sign*.07f,0);
                Add(side+"-elbow",clav+1,0,-.27f); Add(side+"-wrist",clav+2,0,-.25f);
            }
            foreach(bool left in new[]{true,false})
            {
                string side=left?"left":"right"; float sign=left?1:-1; int hip=b.Count;
                Add(side+"-hip",Pelvis,sign*.13f,-.055f); Add(side+"-knee",hip,0,-.25f);
                Add(side+"-ankle",hip+1,0,-.25f); Add(side+"-toe",hip+2,0,-.02f,.09f);
            }
            return b.ToArray();
        }
        private static Box[] MakeBoxes()
        {
            // Color -1 = team accent, 3 = flex, 4 = pale shell, 5 = edge. Source low-detail rig geometry.
            var b=new List<Box>();
            void Add(int bone,int color,float x,float y,float z,float sx,float sy,float sz) => b.Add(new Box(bone,color,V(x,y,z),V(sx,sy,sz)));
            Add(Pelvis,4,0,0,0,.43f,.22f,.28f); Add(Pelvis,-1,0,.08f,0,.44f,.055f,.29f);
            Add(Spine,3,0,.075f,0,.35f,.18f,.25f); Add(Spine,4,0,.07f,0,.36f,.13f,.275f);
            Add(Chest,4,0,.10f,0,.53f,.30f,.30f); Add(Chest,4,0,.235f,0,.47f,.075f,.28f);
            Add(Chest,4,0,.29f,-.01f,.28f,.05f,.22f); Add(Chest,-1,0,.13f,.158f,.075f,.22f,.022f);
            Add(Chest,5,0,.1f,-.157f,.30f,.21f,.025f); Add(Chest,-1,0,.12f,-.174f,.24f,.032f,.015f);
            Add(Neck,3,0,.025f,0,.15f,.15f,.15f); Add(Head,4,0,.165f,0,.34f,.34f,.32f);
            Add(Head,4,0,.34f,0,.30f,.04f,.28f); Add(Head,5,0,-.015f,.01f,.28f,.04f,.28f);
            foreach(bool left in new[]{true,false})
            {
                int c=left?LClav:RClav,a=c+1,e=c+2,h=c+3; float sign=left?1:-1;
                Add(c,-1,sign*.065f,-.01f,0,.19f,.12f,.21f); Add(a,4,0,-.15f,0,.175f,.23f,.185f);
                Add(e,3,0,0,0,.14f,.10f,.145f); Add(e,4,0,-.13f,0,.16f,.20f,.17f);
                Add(h,-1,0,.015f,0,.175f,.055f,.19f); Add(h,5,0,-.045f,.005f,.145f,.10f,.15f);
                Add(h,5,0,-.085f,.07f,.13f,.075f,.10f); Add(h,4,-sign*.078f,-.04f,.035f,.055f,.08f,.085f);
                int hip=left?LHip:RHip,k=hip+1,f=hip+2,t=hip+3;
                Add(hip,3,0,-.02f,0,.16f,.12f,.17f); Add(hip,4,0,-.13f,0,.195f,.23f,.22f);
                Add(k,3,0,0,0,.17f,.09f,.18f); Add(k,-1,0,-.01f,.105f,.16f,.12f,.075f);
                Add(k,4,0,-.14f,0,.175f,.22f,.20f); Add(k,4,0,-.095f,-.10f,.15f,.13f,.06f);
                Add(k,-1,0,-.15f,.108f,.035f,.15f,.018f); Add(f,3,0,-.025f,-.025f,.195f,.09f,.22f);
                Add(t,5,0,-.01f,.045f,.21f,.08f,.22f);
            }
            return b.ToArray();
        }
        public sealed class Pose
        {
            public readonly Quaternion[] Rotation=new Quaternion[Bones.Length], WorldRotation=new Quaternion[Bones.Length];
            public readonly Vector3[] Position=new Vector3[Bones.Length], WorldPosition=new Vector3[Bones.Length];
            public Pose() { Reset(); }
            public void Reset() { for(int i=0;i<Bones.Length;i++) { Position[i]=Bones[i].Position; Rotation[i]=Quaternion.Identity; } }
            public void World()
            {
                for(int i=0;i<Bones.Length;i++)
                {
                    int p=Bones[i].Parent;
                    WorldRotation[i]=p<0?Rotation[i]:WorldRotation[p]*Rotation[i];
                    WorldPosition[i]=p<0?Position[i]:WorldPosition[p]+Vector3.Transform(Position[i],WorldRotation[p]);
                }
            }
            public float Lowest()
            {
                World(); float min=float.MaxValue;
                foreach(Box b in Boxes)
                {
                    Vector3 center=WorldPosition[b.Bone]+Vector3.Transform(b.Position,WorldRotation[b.Bone]);
                    Quaternion q=WorldRotation[b.Bone];
                    float radius=(Math.Abs(Vector3.Transform(X,q).Y)*b.Size.X+Math.Abs(Vector3.Transform(Y,q).Y)*b.Size.Y+Math.Abs(Vector3.Transform(Z,q).Y)*b.Size.Z)*.5f;
                    min=Math.Min(min,center.Y-radius);
                }
                return min;
            }
        }
        private static Vector3 Row(float[] f,int i) => V(f[i],f[i+1],f[i+2]);
        private static Quaternion Rotation(float[] f,int i) { Vector3 v=Row(f,i); float a=v.Length(); return a<1e-8f?Quaternion.Identity:Rot(v/a,a); }
        public static void Sample(float[] f,int set,Pose p)
        {
            p.Reset(); p.Position[Body]=V(0,f[2]-.025f,0); // Horizontal capture travel stripped: gameplay owns root motion.
            p.Rotation[Pelvis]=Rot(Y,f[0])*Rotation(f,4);
            int[] bones={Spine,Chest,Neck,Head,LClav,RClav,LHip,RHip,LFoot,RFoot};
            for(int i=0;i<bones.Length;i++) p.Rotation[bones[i]]=Rotation(f,7+i*3);
            p.Rotation[LKnee]=Rot(X,Math.Max(0,f[37])); p.Rotation[RKnee]=Rot(X,Math.Max(0,f[38]));
            Vector3 blade=Unit(Row(f,42),Z),edge=Row(f,45); edge=Unit(edge-blade*Vector3.Dot(blade,edge),X);
            Quaternion right=Basis(edge,Vector3.Cross(blade,edge),blade);
            Arm(p,false,Row(f,39),right,Row(f,48));
            Vector3 left=Row(f,51); Quaternion leftQ=Look(Row(f,54),Row(f,57));
            if(set==2) // Greatsword support grip follows the reached right wrist, matching the rig's socket contract.
            {
                p.World(); Quaternion chest=p.WorldRotation[Chest];
                Vector3 reached=Vector3.Transform(p.WorldPosition[RHand]-p.WorldPosition[Chest],Quaternion.Inverse(chest));
                float weight=CapturedClip.Clamp(f[63],0,1);
                left=Vector3.Lerp(left,reached+Vector3.Transform(V(0,0,-.11f),right),weight);
                leftQ=Quaternion.Slerp(leftQ,right,weight);
            }
            Arm(p,true,left,leftQ,Row(f,60));
            // Captured legs in the inspection clips. Runtime locomotion re-solves these against travel direction.
            float floor=p.Lowest(); if(floor<-.865f) p.Position[Body].Y+=-.865f-floor;
            p.World();
        }
        private static Vector3 Bend(Vector3 d,Vector3 pole)
        {
            Vector3 p=pole-d*Vector3.Dot(d,pole);
            if(p.LengthSquared()<1e-8f) p=Z-d*d.Z;
            if(p.LengthSquared()<1e-8f) p=X-d*d.X;
            return Vector3.Normalize(p);
        }
        public static void Arm(Pose p,bool left,Vector3 target,Quaternion wrist,Vector3 pole)
        {
            int c=left?LClav:RClav,a=c+1,e=c+2,h=c+3;
            Quaternion inverse=Quaternion.Inverse(p.Rotation[c]);
            Vector3 end=Vector3.Transform(target-p.Position[c],inverse),delta=end-p.Position[a];
            Vector3 d=Unit(delta,-Y); float reach=CapturedClip.Clamp(delta.Length(),.20f,.515f);
            pole=Vector3.Transform(Unit(pole,left?V(1,-.8f,-.25f):V(-1,-.7f,-.2f)),inverse);
            Vector3 bend=Bend(d,pole);
            float along=(.27f*.27f-.25f*.25f+reach*reach)/(2*reach),height=(float)Math.Sqrt(Math.Max(0,.27f*.27f-along*along));
            float cos=along/.27f,sin=height/.27f;
            Vector3 u=d*cos+bend*sin,w=d*sin-bend*cos,y=-u,x=Vector3.Cross(y,w);
            p.Rotation[a]=Basis(x,y,w);
            Vector3 fore=d*reach-u*.27f;
            p.Rotation[e]=Rot(X,-(float)Math.Atan2(Vector3.Dot(fore,w),Vector3.Dot(fore,u)));
            p.Rotation[h]=Quaternion.Inverse(p.Rotation[c]*p.Rotation[a]*p.Rotation[e])*wrist;
        }
        public static void Leg(Pose p,bool left,Vector3 target,float yaw=0)
        {
            p.World(); int hip=left?LHip:RHip,knee=hip+1,foot=hip+2;
            Quaternion parent=p.WorldRotation[Pelvis];
            Vector3 d=Vector3.Transform(target-p.WorldPosition[hip],Quaternion.Inverse(parent));
            float reach=CapturedClip.Clamp(d.Length(),.10f,.497f); d=Unit(d,-Y);
            Vector3 bend=Bend(d,Vector3.Transform(Z,Quaternion.Inverse(parent)));
            Vector3 upper=d*(reach*.5f)+bend*(float)Math.Sqrt(Math.Max(0,.25f*.25f-reach*reach*.25f));
            upper=Unit(upper,-Y); p.Rotation[hip]=FromTo(-Y,upper);
            Vector3 lower=Vector3.Transform(d*reach-upper*.25f,Quaternion.Inverse(p.Rotation[hip]));
            p.Rotation[knee]=FromTo(-Y,Unit(lower,-Y));
            p.Rotation[foot]=Quaternion.Inverse(parent*p.Rotation[hip]*p.Rotation[knee])*Rot(Y,yaw);
        }
        public static Quaternion FromTo(Vector3 a,Vector3 b)
        {
            float dot=Vector3.Dot(a,b); if(dot<-.9999f) return Rot(Unit(Vector3.Cross(a,Z),X),(float)Math.PI);
            return Quaternion.Normalize(new Quaternion(Vector3.Cross(a,b),1+dot));
        }
        public static void BladeFloor(Pose p,bool left,float length)
        {
            p.World(); int h=left?LHand:RHand;
            Vector3 wrist=p.WorldPosition[h],direction=Vector3.Transform(Z,p.WorldRotation[h]);
            const float floor=-.78f;
            if(wrist.Y+direction.Y*length>=floor||wrist.Y<=floor) return;
            float want=CapturedClip.Clamp((floor-wrist.Y)/length,-1,1);
            float angle=(float)(Math.Asin(want)-Math.Asin(CapturedClip.Clamp(direction.Y,-1,1)));
            Vector3 axis=Unit(Vector3.Cross(direction,Y),X);
            Quaternion world=Rot(axis,angle)*p.WorldRotation[h];
            p.Rotation[h]=Quaternion.Inverse(p.WorldRotation[Bones[h].Parent])*world;
        }
        // The roll's tucked joints and their bend (shared tables: no allocation per frame).
        private static readonly int[] RollBones={Pelvis,Spine,Chest,Neck,Head,LClav,RClav,LArm,RArm,LElbow,RElbow,LHand,RHand,LHip,RHip,LKnee,RKnee,LFoot,RFoot};
        private static readonly float[] RollBend={-.30f,.42f,.30f,.42f,.6f,.1f,.1f,-1.10f,-1f,-1.60f,-1.55f,.2f,.2f,-1.80f,-1.62f,2.25f,2.15f,-.45f,-.5f};
        public static void Roll(Pose p,float phase)
        {
            float tuck=CapturedClip.Ease(phase/.2f)*(1-CapturedClip.Ease((phase-.62f)/.26f));
            var bones=RollBones;var xs=RollBend;
            for(int i=0;i<bones.Length;i++) p.Rotation[bones[i]]=Quaternion.Slerp(p.Rotation[bones[i]],Euler(xs[i]),tuck);
            p.Rotation[Body]=Euler(CapturedClip.Ease((phase-.07f)/.73f)*(float)Math.PI*2);
            p.Position[Body].Y+=-.865f-p.Lowest(); p.World();
        }
    }
}
