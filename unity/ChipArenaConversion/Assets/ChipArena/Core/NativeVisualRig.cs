using System;
using System.Collections.Generic;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // A presentation-only skeleton with its own proportions, posed from CapturedRig poses (docs/BOZO-INTEGRATION.md step 3).
    // Source frame (+x = left) and the Pose's body space: motion-root rest at the origin, floor at -.865. Authority never
    // reads it: the capsule, head box, shot origin and weapon frames stay on the 22 joints; the drawn body follows them.
    // Per frame: every bone's world turn is the game joint's turn since its rest (a split chain shares it; wrists ease the
    // fist's deep bend, see Wrist), applied to the bone's rest aligned once at bind (limbs straightened to the game's
    // arms-down, legs-down rest; spine, head and feet keep their authored posture). Then the root height scales by the leg ratio, planted feet and two-handed support grips
    // take two-bone IK, twist bones take a share of their limb's roll, and the weapon rides the visual right hand (P1).
    public static class NativeVisualRig
    {
        public const float Floor=-.865f, PlantedHeight=.03f, LiftedHeight=.10f;
        public enum Align { Keep, Follow, Limb }

        // One visual bone as the builder reads it: rest local transform (source frame) and how the game drives it.
        public struct BoneSpec
        {
            public string Name; public int Parent; public Vector3 Position; public Quaternion Rotation;
            public int Joint; public float Share; public Align Align; public int TwistJoint; public float TwistShare;
        }

        public sealed class Rig
        {
            public readonly int Count;
            public readonly string[] Names;
            public readonly int[] Parent, Joint, TwistJoint;
            public readonly float[] Share, TwistShare, Contact;
            public readonly bool[] ChainStart;
            public readonly Vector3[] RestPosition, AlignedPosition;
            public readonly Quaternion[] RestRotation, AlignedWorld;
            public readonly int[] Of=new int[CapturedRig.Bones.Length]; // the last visual bone of each joint's chain, -1 when unmapped
            public readonly float LegRatio, ArmUpper, ArmLower, LegUpper, LegLower;
            public readonly Vector3 PelvisRest, GamePelvisRest;

            public Rig(BoneSpec[] bones)
            {
                Count=bones.Length; Names=new string[Count]; Parent=new int[Count]; Joint=new int[Count]; TwistJoint=new int[Count];
                Share=new float[Count]; TwistShare=new float[Count]; Contact=new float[Count]; ChainStart=new bool[Count];
                RestPosition=new Vector3[Count]; AlignedPosition=new Vector3[Count]; RestRotation=new Quaternion[Count]; AlignedWorld=new Quaternion[Count];
                for(int j=0;j<Of.Length;j++) Of[j]=-1;
                var restWorld=new Quaternion[Count]; var restPos=new Vector3[Count]; var turn=new Quaternion[Count];
                for(int v=0;v<Count;v++)
                {
                    var b=bones[v]; int p=b.Parent;
                    if(p>=v) throw new ArgumentException("visual bones must follow their parents: "+b.Name);
                    Names[v]=b.Name; Parent[v]=p; Joint[v]=b.Joint; Share[v]=b.Joint<0?0:b.Share; TwistJoint[v]=b.TwistJoint; TwistShare[v]=b.TwistShare;
                    RestPosition[v]=b.Position; RestRotation[v]=Quaternion.Normalize(b.Rotation);
                    restWorld[v]=p<0?RestRotation[v]:restWorld[p]*RestRotation[v];
                    restPos[v]=p<0?b.Position:restPos[p]+Vector3.Transform(b.Position,restWorld[p]);
                    ChainStart[v]=b.Joint>=0&&(p<0||Joint[p]!=b.Joint);
                    if(b.Joint>=0) Of[b.Joint]=v;
                }
                // Bind: straighten the limbs to the game's rest directions, top down so each correction carries to the children.
                for(int v=0;v<Count;v++)
                {
                    int p=Parent[v]; Quaternion parentTurn=p<0?Quaternion.Identity:turn[p];
                    Align mode=bones[v].Align;
                    if(mode==Align.Keep) turn[v]=Quaternion.Identity;
                    else if(mode==Align.Follow) turn[v]=parentTurn;
                    else
                    {
                        int child=ChainChild(bones,v); if(child<0) throw new ArgumentException("limb without a chain child: "+Names[v]);
                        Vector3 along=Vector3.Transform(Vector3.Transform(bones[child].Position,restWorld[v]),parentTurn);
                        Vector3 want=CapturedRig.Unit(CapturedRig.Bones[bones[child].Joint].Position,-CapturedRig.Y);
                        turn[v]=CapturedRig.FromTo(CapturedRig.Unit(along,-CapturedRig.Y),want)*parentTurn;
                    }
                    AlignedWorld[v]=Quaternion.Normalize(turn[v]*restWorld[v]);
                    AlignedPosition[v]=p<0?restPos[v]:AlignedPosition[p]+Vector3.Transform(RestPosition[v],AlignedWorld[p]);
                }
                // Floor-fit radii: a bone's rest height over the floor, capped (soles and toes touch at rest, the rest clear it).
                for(int v=0;v<Count;v++) Contact[v]=Math.Max(0,Math.Min(.12f,AlignedPosition[v].Y-Floor));
                int pelvis=Of[CapturedRig.Pelvis];
                PelvisRest=pelvis<0?Vector3.Zero:AlignedPosition[pelvis];
                GamePelvisRest=CapturedRig.Bones[CapturedRig.Body].Position+CapturedRig.Bones[CapturedRig.Pelvis].Position;
                ArmUpper=Segment(CapturedRig.RArm,CapturedRig.RElbow); ArmLower=Segment(CapturedRig.RElbow,CapturedRig.RHand);
                LegUpper=Segment(CapturedRig.RHip,CapturedRig.RKnee); LegLower=Segment(CapturedRig.RKnee,CapturedRig.RFoot);
                int hip=Of[CapturedRig.RHip],foot=Of[CapturedRig.RFoot];
                LegRatio=hip<0||foot<0?1:(AlignedPosition[hip].Y-AlignedPosition[foot].Y)/(CapturedRig.Bones[CapturedRig.RKnee].Position.Length()+CapturedRig.Bones[CapturedRig.RFoot].Position.Length());
            }

            private float Segment(int from,int to)
            {
                int a=Of[from],b=Of[to]; return a<0||b<0?0:Vector3.Distance(AlignedPosition[a],AlignedPosition[b]);
            }

            private static int ChainChild(BoneSpec[] bones,int v)
            {
                int j=bones[v].Joint; if(j<0) return -1;
                for(int c=v+1;c<bones.Length;c++)
                    if(bones[c].Parent==v&&bones[c].Joint>=0&&bones[c].Joint!=j&&CapturedRig.Bones[bones[c].Joint].Parent==j) return c;
                return -1;
            }

            public int Find(string name) { for(int v=0;v<Count;v++) if(Names[v]==name) return v; return -1; }
        }

        // Per-character working set: allocated once, reused every frame.
        public sealed class State
        {
            public readonly Rig Rig;
            public readonly Quaternion[] Turn, World, Local;
            public readonly Vector3[] Position, LocalPosition;
            public float LeftFootWeight, RightFootWeight, SupportWeight, LeftArmReach, LeftLegReach, RightLegReach;
            public State(Rig rig)
            {
                Rig=rig; int n=rig.Count;
                Turn=new Quaternion[n]; World=new Quaternion[n]; Local=new Quaternion[n]; Position=new Vector3[n]; LocalPosition=new Vector3[n];
            }
        }

        // Poses the visual skeleton from a game pose (pose.World() already run). support: 0..1, whether the weapon in hand may
        // be held two-handed (the grip then follows the game's own hands, HandsTogether); plantFeet: off for rolls and airborne
        // takes; fitFloor: the roll's and tuck's Lowest() fit.
        public static void Pose(State s,CapturedRig.Pose pose,float support,bool plantFeet,bool fitFloor)
        {
            var r=s.Rig; int n=r.Count;
            for(int v=0;v<n;v++)
            {
                int p=r.Parent[v]; Quaternion parentTurn=p<0?Quaternion.Identity:s.Turn[p];
                int j=r.Joint[v];
                if(j>=0)
                {
                    int gp=CapturedRig.Bones[j].Parent;
                    Quaternion start=r.ChainStart[v]?(gp<0?Quaternion.Identity:pose.WorldRotation[gp]):parentTurn;
                    Quaternion local=gp<0?pose.WorldRotation[j]:Quaternion.Inverse(pose.WorldRotation[gp])*pose.WorldRotation[j];
                    if(j==CapturedRig.LHand||j==CapturedRig.RHand) local=Wrist(local);
                    s.Turn[v]=Quaternion.Normalize(start*(r.Share[v]>=.9999f?local:Quaternion.Slerp(Quaternion.Identity,local,r.Share[v])));
                }
                else if(r.TwistJoint[v]>=0) s.Turn[v]=Quaternion.Normalize(parentTurn*CapturedRig.Rot(CapturedRig.Y,r.TwistShare[v]*Twist(pose.Rotation[r.TwistJoint[v]])));
                else s.Turn[v]=parentTurn;
                s.World[v]=Quaternion.Normalize(s.Turn[v]*r.AlignedWorld[v]);
            }
            Positions(s,pose);
            s.LeftFootWeight=s.RightFootWeight=s.SupportWeight=0; s.LeftArmReach=s.LeftLegReach=s.RightLegReach=0;
            if(plantFeet)
            {
                s.LeftFootWeight=Foot(s,pose,true); s.RightFootWeight=Foot(s,pose,false);
            }
            support=Math.Min(1,support)*HandsTogether(pose);
            if(support>0)
            {
                s.SupportWeight=support; SupportGrip(s,pose,support);
            }
            if(plantFeet||support>0) Positions(s,pose);
            if(fitFloor)
            {
                float low=float.MaxValue;
                for(int v=0;v<n;v++) low=Math.Min(low,s.Position[v].Y-r.Contact[v]);
                if(low<Floor) for(int v=0;v<n;v++) s.Position[v].Y+=Floor-low;
            }
            for(int v=0;v<n;v++)
            {
                int p=r.Parent[v];
                if(p<0) { s.Local[v]=s.World[v]; s.LocalPosition[v]=s.Position[v]; continue; }
                Quaternion inverse=Quaternion.Inverse(s.World[p]);
                s.Local[v]=Quaternion.Normalize(inverse*s.World[v]);
                s.LocalPosition[v]=Vector3.Transform(s.Position[v]-s.Position[p],inverse);
            }
        }

        // The weapon frame on the visual right hand (P1): the game's wrist turn at the visual wrist. Weapons draw at the wrist
        // frame with the grip baked into their geometry, so this frame replaces the game wrist's for the drawn weapon.
        public static void WeaponFrame(State s,CapturedRig.Pose pose,out Vector3 position,out Quaternion rotation)
        {
            rotation=pose.WorldRotation[CapturedRig.RHand]; position=s.Position[s.Rig.Of[CapturedRig.RHand]];
        }

        // The grip point in that frame (NativeKillBoxes.RightHandSocket), for drift measures.
        public static void Grip(State s,CapturedRig.Pose pose,out Vector3 position,out Quaternion rotation)
        {
            var socket=NativeKillBoxes.RightHandSocket; WeaponFrame(s,pose,out var wrist,out rotation);
            position=wrist+Vector3.Transform(new Vector3(socket[0],socket[1],socket[2]),rotation);
        }

        // The visual wrist. The game's wrist frame is the voxel fist's and the weapon's (the grip is baked into the weapon at
        // the wrist), so holds bend it 90-150 degrees off the forearm; an open human hand there points at the floor. The
        // visual hand keeps the game wrist's roll and its bend up to WristKnee, and a WristExcess share of any bend beyond
        // (107 degrees on a rifle hold -> 45). The weapon frame keeps the game wrist turn (WeaponFrame), so aim is unchanged.
        public const float WristKnee=25*(float)Math.PI/180, WristExcess=.25f;
        public static Quaternion Wrist(Quaternion local)
        {
            Quaternion twist=CapturedRig.Rot(CapturedRig.Y,Twist(local));
            Quaternion swing=Quaternion.Normalize(local*Quaternion.Inverse(twist));
            if(swing.W<0) swing=new Quaternion(-swing.X,-swing.Y,-swing.Z,-swing.W);
            float angle=2*(float)Math.Acos(Math.Min(1f,swing.W));
            if(angle<=WristKnee) return local;
            Vector3 axis=new Vector3(swing.X,swing.Y,swing.Z);
            if(axis.LengthSquared()<1e-12f) return local;
            return Quaternion.Normalize(CapturedRig.Rot(Vector3.Normalize(axis),WristBend(angle))*twist);
        }
        public static float WristBend(float game)=>game<=WristKnee?game:WristKnee+(game-WristKnee)*WristExcess;

        // Twist about the bone's own axis (+y in the game's joint frames): the swing-twist split of a local turn.
        public static float Twist(Quaternion q)
        {
            float angle=2*(float)Math.Atan2(q.Y,q.W);
            if(angle>Math.PI) angle-=2*(float)Math.PI; else if(angle<-Math.PI) angle+=2*(float)Math.PI;
            return angle;
        }

        private static void Positions(State s,CapturedRig.Pose pose)
        {
            var r=s.Rig; int pelvis=r.Of[CapturedRig.Pelvis];
            Vector3 lift=(pose.WorldPosition[CapturedRig.Pelvis]-r.GamePelvisRest)*r.LegRatio;
            for(int v=0;v<r.Count;v++)
            {
                int p=r.Parent[v];
                if(v==pelvis) s.Position[v]=r.PelvisRest+lift;
                else s.Position[v]=p<0?r.RestPosition[v]:s.Position[p]+Vector3.Transform(r.RestPosition[v],s.World[p]);
            }
        }

        // Planted feet: the visual ankle goes to the game ankle's ground point, the knee toward the game knee's side.
        private static float Foot(State s,CapturedRig.Pose pose,bool left)
        {
            var r=s.Rig;
            int hip=left?CapturedRig.LHip:CapturedRig.RHip,knee=hip+1,ankle=hip+2;
            int vh=r.Of[hip],vk=r.Of[knee],va=r.Of[ankle];
            if(vh<0||vk<0||va<0) return 0;
            Vector3 game=pose.WorldPosition[ankle];
            float above=game.Y-AnkleRest(hip);
            float weight=1-CapturedClip.Ease((above-PlantedHeight)/(LiftedHeight-PlantedHeight));
            if(weight<=0) return 0;
            Vector3 target=new Vector3(game.X,r.AlignedPosition[va].Y+above,game.Z);
            target=Vector3.Lerp(s.Position[va],target,weight);
            Vector3 pole=pose.WorldPosition[knee]-(pose.WorldPosition[hip]+game)*.5f;
            float reach=TwoBone(s,vh,vk,va,target,pole,r.LegUpper,r.LegLower);
            if(left) s.LeftLegReach=reach; else s.RightLegReach=reach;
            return weight;
        }

        // How firmly the game's own hands hold together: 1 while its wrists are within the support grips' reach of each other
        // (RangedPose and the greatsword put them .11-.12 m apart), 0 once a run or roll swings the left hand free.
        public const float TogetherNear=.18f, TogetherFar=.30f;
        public static float HandsTogether(CapturedRig.Pose pose)
        {
            float d=Vector3.Distance(pose.WorldPosition[CapturedRig.LHand],pose.WorldPosition[CapturedRig.RHand]);
            return 1-CapturedClip.Ease((d-TogetherNear)/(TogetherFar-TogetherNear));
        }

        // The game ankle's rest height in the Pose's body space (-.795: floor + .07).
        public static float AnkleRest(int hip)
        {
            var b=CapturedRig.Bones;
            return b[CapturedRig.Body].Position.Y+b[CapturedRig.Pelvis].Position.Y+b[hip].Position.Y+b[hip+1].Position.Y+b[hip+2].Position.Y;
        }

        // Two-handed holds: the left hand keeps the game's right-to-left wrist offset, carried onto the visual right hand.
        private static void SupportGrip(State s,CapturedRig.Pose pose,float weight)
        {
            var r=s.Rig;
            int va=r.Of[CapturedRig.LArm],ve=r.Of[CapturedRig.LElbow],vh=r.Of[CapturedRig.LHand],right=r.Of[CapturedRig.RHand];
            if(va<0||ve<0||vh<0||right<0) return;
            Positions(s,pose);
            Vector3 target=s.Position[right]+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand];
            target=Vector3.Lerp(s.Position[vh],target,weight);
            Vector3 pole=pose.WorldPosition[CapturedRig.LElbow]-(pose.WorldPosition[CapturedRig.LArm]+pose.WorldPosition[CapturedRig.LHand])*.5f;
            s.LeftArmReach=TwoBone(s,va,ve,vh,target,pole,r.ArmUpper,r.ArmLower);
        }

        // Analytic two-bone IK in world space: turns the upper and lower bones (and the twist bones that follow them) so the
        // end reaches the target, bending toward the pole. The end bone keeps its world turn. Returns the reach used, 0..1.
        private static float TwoBone(State s,int upper,int lower,int end,Vector3 target,Vector3 pole,float a,float b)
        {
            var r=s.Rig; if(a<=0||b<=0) return 0;
            Vector3 root=s.Position[upper];
            Vector3 d=target-root; float dist=d.Length();
            float reach=CapturedClip.Clamp(dist,Math.Abs(a-b)+1e-4f,(a+b)*.9999f);
            d=CapturedRig.Unit(d,-CapturedRig.Y);
            Vector3 bend=pole-d*Vector3.Dot(pole,d);
            if(bend.LengthSquared()<1e-10f) bend=Vector3.Transform(CapturedRig.Z,s.World[upper])-d*Vector3.Dot(Vector3.Transform(CapturedRig.Z,s.World[upper]),d);
            bend=CapturedRig.Unit(bend,CapturedRig.Z);
            float along=(a*a-b*b+reach*reach)/(2*reach),height=(float)Math.Sqrt(Math.Max(0,a*a-along*along));
            Vector3 elbow=root+d*along+bend*height,hand=root+d*reach;
            Vector3 oldElbow=s.Position[upper]+Vector3.Transform(r.RestPosition[lower],s.World[upper]);
            Quaternion first=CapturedRig.FromTo(CapturedRig.Unit(oldElbow-root,-CapturedRig.Y),CapturedRig.Unit(elbow-root,-CapturedRig.Y));
            Rotate(s,upper,lower,first);
            Vector3 oldHand=elbow+Vector3.Transform(r.RestPosition[end],s.World[lower]);
            Quaternion second=CapturedRig.FromTo(CapturedRig.Unit(oldHand-elbow,-CapturedRig.Y),CapturedRig.Unit(hand-elbow,-CapturedRig.Y));
            Rotate(s,lower,end,second);
            return reach/(a+b);
        }

        // Applies a world turn to a bone and the twist and held bones that follow it, stopping at the next chain bone.
        private static void Rotate(State s,int bone,int stop,Quaternion turn)
        {
            var r=s.Rig;
            s.World[bone]=Quaternion.Normalize(turn*s.World[bone]);
            for(int v=bone+1;v<r.Count;v++)
            {
                if(v==stop) continue;
                int p=r.Parent[v];
                if(p==bone&&r.Joint[v]<0) s.World[v]=Quaternion.Normalize(turn*s.World[v]);
            }
        }

        // The Unreal-mannequin naming many humanoid asset packs use, mapped onto the 22 joints. Names only: no pack data.
        public static BoneSpec[] Mannequin(string[] names,int[] parents,Vector3[] positions,Quaternion[] rotations)
        {
            var map=new Dictionary<string,(int joint,float share,Align align)>
            {
                {"pelvis",(CapturedRig.Pelvis,1,Align.Keep)},
                {"spine_01",(CapturedRig.Spine,.5f,Align.Keep)},{"spine_02",(CapturedRig.Spine,.5f,Align.Keep)},
                {"spine_03",(CapturedRig.Chest,.34f,Align.Keep)},{"spine_04",(CapturedRig.Chest,.33f,Align.Keep)},{"spine_05",(CapturedRig.Chest,.33f,Align.Keep)},
                {"neck_01",(CapturedRig.Neck,.5f,Align.Keep)},{"neck_02",(CapturedRig.Neck,.5f,Align.Keep)},{"head",(CapturedRig.Head,1,Align.Keep)},
            };
            foreach(bool left in new[]{true,false})
            {
                string side=left?"_l":"_r"; int c=left?CapturedRig.LClav:CapturedRig.RClav,hip=left?CapturedRig.LHip:CapturedRig.RHip;
                map["clavicle"+side]=(c,1,Align.Keep); map["upperarm"+side]=(c+1,1,Align.Limb);
                map["lowerarm"+side]=(c+2,1,Align.Limb); map["hand"+side]=(c+3,1,Align.Follow);
                map["thigh"+side]=(hip,1,Align.Limb); map["calf"+side]=(hip+1,1,Align.Limb);
                map["foot"+side]=(hip+2,1,Align.Keep); map["ball"+side]=(hip+3,1,Align.Follow);
            }
            int n=names.Length; var specs=new BoneSpec[n];
            for(int v=0;v<n;v++)
            {
                var spec=new BoneSpec{Name=names[v],Parent=parents[v],Position=positions[v],Rotation=rotations[v],Joint=-1,Share=0,Align=Align.Follow,TwistJoint=-1};
                if(map.TryGetValue(names[v],out var m)) { spec.Joint=m.joint; spec.Share=m.share; spec.Align=m.align; }
                specs[v]=spec;
            }
            // Twist helpers: roll shares by their place along the limb. Upper arm and thigh helpers counter the limb's own
            // roll (none at the far end, all of it at the root); forearm and calf helpers take the hand's and foot's roll.
            for(int v=0;v<n;v++)
            {
                if(!names[v].Contains("twist")) continue;
                int p=parents[v]; if(p<0||specs[p].Joint<0) continue;
                int joint=specs[p].Joint; int child=-1;
                for(int c=0;c<n;c++) if(parents[c]==p&&specs[c].Joint>=0&&specs[c].Joint!=joint) { child=c; break; }
                if(child<0) continue;
                float length=positions[child].Length();
                float t=length<1e-5f?0:CapturedClip.Clamp(Vector3.Dot(positions[v],positions[child])/(length*length),0,1);
                bool counter=joint==CapturedRig.LArm||joint==CapturedRig.RArm||joint==CapturedRig.LHip||joint==CapturedRig.RHip;
                specs[v].Align=Align.Follow;
                specs[v].TwistJoint=counter?joint:specs[child].Joint;
                specs[v].TwistShare=counter?-(1-t):t;
            }
            return specs;
        }
    }
}
