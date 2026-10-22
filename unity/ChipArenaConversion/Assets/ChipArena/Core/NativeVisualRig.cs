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
    // arms-down, legs-down rest; spine, head and feet keep their authored posture). Then the root height scales by the leg ratio, hands
    // inside the game's shoulder line swing to their chest-relative place (Inside), planted feet and two-handed support grips take
    // two-bone IK (a far grip's shoulder reaches first), twist bones take a share of their limb's roll, and the weapon rides the
    // visual right hand (P1).
    public static class NativeVisualRig
    {
        public const float Floor=-.865f, PlantedHeight=.03f, LiftedHeight=.10f;
        // The arms' spread: the game hangs its arms straight down from voxel shoulders .35 m out; a body with narrower
        // shoulders would bury its forearms and hands in the hips. While a game arm hangs, the visual shoulder turns it this far
        // out (in the clavicle's frame, so the arm's own roll is kept), fading out as the arm rises past 60 degrees from down.
        public const float ArmSpread=15*(float)Math.PI/180;
        public static Quaternion Spread(CapturedRig.Pose pose,bool left)
        {
            int arm=left?CapturedRig.LArm:CapturedRig.RArm;
            float hang=-Vector3.Transform(-CapturedRig.Y,pose.WorldRotation[arm]).Y; // 1 while the game upper arm hangs straight down
            float w=CapturedClip.Ease((hang-.5f)/.5f);
            return CapturedRig.Rot(CapturedRig.Z,(left?1:-1)*ArmSpread*w);
        }
        public enum Align { Keep, Follow, Limb }

        // One visual bone as the builder reads it: rest local transform (source frame) and how the game drives it.
        public struct BoneSpec
        {
            public string Name; public int Parent; public Vector3 Position; public Quaternion Rotation;
            public int Joint; public float Share; public Align Align; public int TwistJoint; public float TwistShare;
            public int Finger, Side; // finger segment 1-3 (proximal to tip), thumb 11-13, 0 none; side 0 left, 1 right
        }

        public sealed class Rig
        {
            public readonly int Count;
            public readonly string[] Names;
            public readonly int[] Parent, Joint, TwistJoint, Finger, Side;
            public readonly Vector3[] FingerAxis; // each finger chain's curl axis in the aligned rest (from its own rest curl)
            public readonly float[] Share, TwistShare, Contact;
            public readonly bool[] ChainStart;
            public readonly Vector3[] RestPosition, AlignedPosition, LimbRest; // LimbRest: an aligned limb's rest direction
            public readonly Quaternion[] RestRotation, AlignedWorld;
            public readonly int[] Of=new int[CapturedRig.Bones.Length]; // the last visual bone of each joint's chain, -1 when unmapped
            public readonly float LegRatio, ArmUpper, ArmLower, LegUpper, LegLower, HeadRise;
            public readonly Vector3 PelvisRest, GamePelvisRest;

            public Rig(BoneSpec[] bones)
            {
                Count=bones.Length; Names=new string[Count]; Parent=new int[Count]; Joint=new int[Count]; TwistJoint=new int[Count];
                Share=new float[Count]; TwistShare=new float[Count]; Contact=new float[Count]; ChainStart=new bool[Count];
                Finger=new int[Count]; Side=new int[Count]; FingerAxis=new Vector3[Count];
                RestPosition=new Vector3[Count]; AlignedPosition=new Vector3[Count]; LimbRest=new Vector3[Count]; RestRotation=new Quaternion[Count]; AlignedWorld=new Quaternion[Count];
                for(int j=0;j<Of.Length;j++) Of[j]=-1;
                var restWorld=new Quaternion[Count]; var restPos=new Vector3[Count]; var turn=new Quaternion[Count];
                for(int v=0;v<Count;v++)
                {
                    var b=bones[v]; int p=b.Parent;
                    if(p>=v) throw new ArgumentException("visual bones must follow their parents: "+b.Name);
                    Names[v]=b.Name; Parent[v]=p; Joint[v]=b.Joint; Share[v]=b.Joint<0?0:b.Share; TwistJoint[v]=b.TwistJoint; TwistShare[v]=b.TwistShare;
                    Finger[v]=b.Finger; Side[v]=b.Finger==0?-1:b.Side;
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
                        LimbRest[v]=want;
                        turn[v]=CapturedRig.FromTo(CapturedRig.Unit(along,-CapturedRig.Y),want)*parentTurn;
                    }
                    AlignedWorld[v]=Quaternion.Normalize(turn[v]*restWorld[v]);
                    AlignedPosition[v]=p<0?restPos[v]:AlignedPosition[p]+Vector3.Transform(RestPosition[v],AlignedWorld[p]);
                }
                FingerAxes();
                // Floor-fit radii: a bone's rest height over the floor, capped (soles and toes touch at rest, the rest clear it).
                for(int v=0;v<Count;v++) Contact[v]=Math.Max(0,Math.Min(.12f,AlignedPosition[v].Y-Floor));
                int pelvis=Of[CapturedRig.Pelvis];
                PelvisRest=pelvis<0?Vector3.Zero:AlignedPosition[pelvis];
                GamePelvisRest=CapturedRig.Bones[CapturedRig.Body].Position+CapturedRig.Bones[CapturedRig.Pelvis].Position;
                ArmUpper=Segment(CapturedRig.RArm,CapturedRig.RElbow); ArmLower=Segment(CapturedRig.RElbow,CapturedRig.RHand);
                int head=Of[CapturedRig.Head],arm=Of[CapturedRig.RArm];
                HeadRise=head<0||arm<0?GameHeadRise:AlignedPosition[head].Y-AlignedPosition[arm].Y;
                LegUpper=Segment(CapturedRig.RHip,CapturedRig.RKnee); LegLower=Segment(CapturedRig.RKnee,CapturedRig.RFoot);
                int hip=Of[CapturedRig.RHip],foot=Of[CapturedRig.RFoot];
                LegRatio=hip<0||foot<0?1:(AlignedPosition[hip].Y-AlignedPosition[foot].Y)/(CapturedRig.Bones[CapturedRig.RKnee].Position.Length()+CapturedRig.Bones[CapturedRig.RFoot].Position.Length());
            }

            // Curl axes: a finger chain bends the way its rest already curls (the turn from each segment into the next), so no
            // pack-specific bone axes are assumed. A straight chain borrows its hand's other chains; failing that, +x.
            private void FingerAxes()
            {
                var sum=new Vector3[Count];
                for(int v=0;v<Count;v++)
                {
                    if(Finger[v]==0) continue;
                    int child=Next(v); if(child<0) continue;
                    int p=Parent[v]; Vector3 into=AlignedPosition[v]-AlignedPosition[p],on=AlignedPosition[child]-AlignedPosition[v];
                    bool thumbBase=Finger[v]==11; // the thumb's base turns across the palm; its curl starts at the next joint
                    if(!thumbBase&&into.LengthSquared()>1e-10f) sum[Root(v)]+=Vector3.Cross(into,on);
                }
                for(int v=0;v<Count;v++)
                {
                    if(Finger[v]!=1&&Finger[v]!=11) continue;
                    Vector3 axis=sum[v].LengthSquared()>1e-12f?Vector3.Normalize(sum[v]):Vector3.Zero;
                    for(int u=v;u>=0;u=Next(u)) FingerAxis[u]=axis;
                }
                for(int v=0;v<Count;v++)
                {
                    if(Finger[v]==0||FingerAxis[v].LengthSquared()>.5f) continue;
                    Vector3 hand=Vector3.Zero;
                    for(int u=0;u<Count;u++) if(Finger[u]!=0&&Side[u]==Side[v]&&FingerAxis[u].LengthSquared()>.5f) hand+=FingerAxis[u];
                    FingerAxis[v]=hand.LengthSquared()>1e-10f?Vector3.Normalize(hand):CapturedRig.X;
                }
            }
            private int Next(int v) { for(int c=v+1;c<Count;c++) if(Parent[c]==v&&Finger[c]==Finger[v]+1) return c; return -1; }
            private int Root(int v) { while(Finger[v]!=1&&Finger[v]!=11&&Parent[v]>=0&&Finger[Parent[v]]!=0) v=Parent[v]; return v; }

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
            public float LeftFootWeight, RightFootWeight, SupportWeight, LeftArmReach, LeftLegReach, RightLegReach, LeftGrip, RightGrip, LeftInside, RightInside, ShoulderTurn, GiveWay;
            public State(Rig rig)
            {
                Rig=rig; int n=rig.Count;
                Turn=new Quaternion[n]; World=new Quaternion[n]; Local=new Quaternion[n]; Position=new Vector3[n]; LocalPosition=new Vector3[n];
            }
        }

        // Poses the visual skeleton from a game pose (pose.World() already run). support: 0..1, whether the weapon in hand may
        // be held two-handed (the grip then follows the game's own hands, HandsTogether); plantFeet: off for rolls and airborne
        // takes; fitFloor: the roll's and tuck's Lowest() fit. rightGrip, leftGrip: 0..1, a hand closed on what it holds (the
        // weapon, a shield, an off-hand blade); the left also closes while it supports a two-handed grip; open hands relax.
        public static void Pose(State s,CapturedRig.Pose pose,float support,bool plantFeet,bool fitFloor,float rightGrip=0,float leftGrip=0)
        {
            var r=s.Rig; int n=r.Count;
            support=Math.Min(1,support)*HandsTogether(pose);
            s.RightGrip=CapturedClip.Clamp(rightGrip,0,1); s.LeftGrip=Math.Max(CapturedClip.Clamp(leftGrip,0,1),support);
            for(int v=0;v<n;v++)
            {
                int p=r.Parent[v]; Quaternion parentTurn=p<0?Quaternion.Identity:s.Turn[p];
                int j=r.Joint[v];
                if(j>=0)
                {
                    int gp=CapturedRig.Bones[j].Parent;
                    // A chain hangs from its visual parent when that parent carries the game parent joint (so a turn added
                    // above, like the arms' spread, carries down the limb); split chains and roots start on the game's own.
                    bool attached=p>=0&&gp>=0&&r.Joint[p]==gp;
                    Quaternion start=r.ChainStart[v]?(attached?parentTurn:gp<0?Quaternion.Identity:pose.WorldRotation[gp]):parentTurn;
                    if(j==CapturedRig.LArm||j==CapturedRig.RArm) start=start*Spread(pose,j==CapturedRig.LArm);
                    Quaternion local=gp<0?pose.WorldRotation[j]:Quaternion.Inverse(pose.WorldRotation[gp])*pose.WorldRotation[j];
                    if(j==CapturedRig.LHand||j==CapturedRig.RHand) local=Wrist(local);
                    s.Turn[v]=Quaternion.Normalize(start*(r.Share[v]>=.9999f?local:Quaternion.Slerp(Quaternion.Identity,local,r.Share[v])));
                }
                else if(r.TwistJoint[v]>=0) s.Turn[v]=Quaternion.Normalize(parentTurn*CapturedRig.Rot(CapturedRig.Y,r.TwistShare[v]*Twist(pose.Rotation[r.TwistJoint[v]])));
                else if(r.Finger[v]!=0) s.Turn[v]=Quaternion.Normalize(parentTurn*CapturedRig.Rot(r.FingerAxis[v],Curl(r.Finger[v],r.Side[v]==1?s.RightGrip:s.LeftGrip)));
                else s.Turn[v]=parentTurn;
                s.World[v]=Quaternion.Normalize(s.Turn[v]*r.AlignedWorld[v]);
            }
            Positions(s,pose);
            s.LeftFootWeight=s.RightFootWeight=s.SupportWeight=0; s.LeftArmReach=s.LeftLegReach=s.RightLegReach=s.ShoulderTurn=s.GiveWay=0;
            s.LeftInside=Inside(s,pose,true); s.RightInside=Inside(s,pose,false);
            if(plantFeet)
            {
                s.LeftFootWeight=Foot(s,pose,true); s.RightFootWeight=Foot(s,pose,false);
            }
            if(support>0)
            {
                s.SupportWeight=support; SupportGrip(s,pose,support);
            }
            if(plantFeet||support>0||s.LeftInside>0||s.RightInside>0) Positions(s,pose);
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
            Vector3 pole=s.Position[vk]-(s.Position[vh]+target)*.5f,fallback=pose.WorldPosition[knee]-(pose.WorldPosition[hip]+game)*.5f;
            float reach=TwoBone(s,vh,vk,va,target,pole,fallback,r.LegUpper,r.LegLower);
            if(left) s.LeftLegReach=reach; else s.RightLegReach=reach;
            return weight;
        }

        // Finger curl per joint (radians): relaxed when the hand is open, closed round a weapon's grip (about 3-4 cm across).
        // Entries 0-2 are a finger's proximal, middle and tip joints, 3-5 the thumb's.
        public static readonly float[] Relaxed={.26f,.44f,.26f,.09f,.17f,.17f}, Closed={.96f,1.31f,.79f,.35f,.52f,.52f};
        public static float Curl(int finger,float grip)
        {
            int i=finger>=11?finger-8:finger-1; if(i<0||i>5) return 0;
            return Relaxed[i]+(Closed[i]-Relaxed[i])*grip;
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

        // Hands inside the shoulder line. The game's shoulders sit .35 m out from its spine; a body with narrower shoulders that
        // copies the arm directions crosses its forearms whenever the game brings a hand in (two-handed holds, guards, the rifle
        // carry), and a support arm then folds back out to its grip: elbows that read as bent the wrong way. Inside the line a
        // visual hand keeps its sideways place from the chest's middle, scaled from the game's shoulder width to the visual one.
        // Its height does the same between the shoulder and the head: the game's head joint rises GameHeadRise over its
        // shoulders and a short-necked body's far less, so a hold at the voxel's chest (a rifle .34 m under the head box) put
        // the visual hands at the mouth. That drop peaks at the head's height and is gone by twice it, so raised swings stay
        // raised. The hand keeps the copied arm's extension (how straight the arm is: a shift alone drew aimed hands back to the
        // chest), taking up the change in depth. The whole arm swings at the shoulder by the shortest turn that brings the hand
        // there, so the elbow keeps the game's bend exactly and the hand its turn. The shift fades in over InsideBand, so a hand
        // at or outside the shoulder line keeps the copied arm. Returns the weight.
        public const float InsideBand=.10f, GameArm=.52f;
        public static readonly float GameHeadRise=CapturedRig.Bones[CapturedRig.Neck].Position.Y+CapturedRig.Bones[CapturedRig.Head].Position.Y
            -CapturedRig.Bones[CapturedRig.RClav].Position.Y-CapturedRig.Bones[CapturedRig.RArm].Position.Y;
        // How far the visual hand drops for a game hand rising `gameRise` over its shoulder (the tent described above).
        public static float HeadDrop(Rig r,float gameRise)
        {
            float tent=CapturedClip.Clamp(Math.Min(gameRise,2*GameHeadRise-gameRise),0,GameHeadRise);
            return tent*Math.Max(0,1-r.HeadRise/GameHeadRise)*(r.ArmUpper+r.ArmLower)/GameArm;
        }
        private static float Inside(State s,CapturedRig.Pose pose,bool left)
        {
            var r=s.Rig;
            int ga=left?CapturedRig.LArm:CapturedRig.RArm,ge=ga+1,gh=ga+2;
            int va=r.Of[ga],ve=r.Of[ge],vh=r.Of[gh],lc=r.Of[CapturedRig.LClav],rc=r.Of[CapturedRig.RClav];
            if(va<0||ve<0||vh<0||lc<0||rc<0) return 0;
            Vector3 side=Vector3.Transform(left?CapturedRig.X:-CapturedRig.X,pose.WorldRotation[CapturedRig.Chest]);
            Vector3 gameMiddle=(pose.WorldPosition[CapturedRig.LClav]+pose.WorldPosition[CapturedRig.RClav])*.5f,middle=(s.Position[lc]+s.Position[rc])*.5f;
            float gameShoulder=Vector3.Dot(pose.WorldPosition[ga]-gameMiddle,side),shoulder=Vector3.Dot(s.Position[va]-middle,side);
            float gameHand=Vector3.Dot(pose.WorldPosition[gh]-gameMiddle,side),hand=Vector3.Dot(s.Position[vh]-middle,side);
            if(gameShoulder<.05f) return 0;
            float w=CapturedClip.Ease((gameShoulder-gameHand)/InsideBand);
            if(w<=0) return 0;
            Vector3 copied=s.Position[vh]-s.Position[va];
            Vector3 up=Vector3.Transform(CapturedRig.Y,pose.WorldRotation[CapturedRig.Chest]),ahead=Vector3.Transform(CapturedRig.Z,pose.WorldRotation[CapturedRig.Chest]);
            float across=hand-shoulder+(gameHand*shoulder/gameShoulder-hand)*w;
            float rise=Vector3.Dot(copied,up)-HeadDrop(r,Vector3.Dot(pose.WorldPosition[gh]-pose.WorldPosition[ga],up))*w;
            float depth=Vector3.Dot(copied,ahead),keep=copied.LengthSquared()-across*across-rise*rise;
            if(keep>0) depth=(depth<0?-1:1)*(float)Math.Sqrt(keep);
            Quaternion swing=CapturedRig.FromTo(CapturedRig.Unit(copied,-CapturedRig.Y),CapturedRig.Unit(side*across+up*rise+ahead*depth,-CapturedRig.Y));
            Rotate(s,va,ve,swing); Rotate(s,ve,vh,swing);
            return w;
        }

        // Two-handed holds: the left hand keeps the game's right-to-left wrist offset, carried onto the visual right hand. Where
        // that grip lies past SupportReach of the left arm (narrower shoulders gain less reach from the game's torso turn, and the
        // weapon hand keeps the game's extension), the left shoulder reaches first, as a body does: the clavicle turns its
        // shoulder end toward the grip, up to ShoulderReach (ShoulderTurn). Only what is still out of reach makes the weapon
        // hand give way toward the left shoulder (GiveWay, metres), so both hands stay on the weapon.
        public const float SupportReach=.97f, ShoulderReach=25*(float)Math.PI/180;
        private static void SupportGrip(State s,CapturedRig.Pose pose,float weight)
        {
            var r=s.Rig;
            int va=r.Of[CapturedRig.LArm],ve=r.Of[CapturedRig.LElbow],vh=r.Of[CapturedRig.LHand],right=r.Of[CapturedRig.RHand];
            if(va<0||ve<0||vh<0||right<0) return;
            Positions(s,pose);
            Vector3 offset=pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],target=s.Position[right]+offset;
            Vector3 grip=target-s.Position[va]; float far=grip.Length(),most=(r.ArmUpper+r.ArmLower)*SupportReach;
            int ra=r.Of[CapturedRig.RArm],re=r.Of[CapturedRig.RElbow],lc=r.Of[CapturedRig.LClav];
            float left=far; // the grip's distance once the shoulder has reached in full
            if(far>most&&lc>=0)
            {
                // Turn the clavicle in the plane of its shoulder end and the grip, by the angle that brings the shoulder to
                // SupportReach of the grip (the law of cosines), capped.
                Vector3 pivot=s.Position[lc],end=s.Position[va]-pivot,to=target-pivot,axis=Vector3.Cross(end,to);
                float a=end.Length(),d=to.Length();
                if(a>1e-4f&&d>1e-4f&&axis.LengthSquared()>1e-12f)
                {
                    float between=(float)Math.Acos(CapturedClip.Clamp(Vector3.Dot(end,to)/(a*d),-1,1));
                    float need=between-(float)Math.Acos(CapturedClip.Clamp((a*a+d*d-most*most)/(2*a*d),-1,1));
                    float turn=CapturedClip.Clamp(need,0,ShoulderReach);
                    left=(float)Math.Sqrt(Math.Max(0,a*a+d*d-2*a*d*Math.Cos(between-turn)));
                    s.ShoulderTurn=turn*weight;
                    Rotate(s,lc,va,CapturedRig.Rot(Vector3.Normalize(axis),s.ShoulderTurn));
                    Positions(s,pose);
                    grip=target-s.Position[va]; far=grip.Length();
                }
            }
            if(left>most+1e-5f&&ra>=0&&re>=0)
            {
                Vector3 hand=s.Position[right]-grip*((left-most)/far*weight);
                Vector3 bend=s.Position[re]-(s.Position[ra]+hand)*.5f;
                Vector3 gameBend=pose.WorldPosition[CapturedRig.RElbow]-(pose.WorldPosition[CapturedRig.RArm]+pose.WorldPosition[CapturedRig.RHand])*.5f;
                s.GiveWay=(left-most)*weight;
                TwoBone(s,ra,re,right,hand,bend,gameBend,r.ArmUpper,r.ArmLower);
                Positions(s,pose);
                target=s.Position[right]+offset;
            }
            target=Vector3.Lerp(s.Position[vh],target,weight);
            // Bend toward the visual elbow's own place (the copied arm's, swung by Inside); the game's elbow side is the fallback.
            // The game's wider shoulders can put its elbow on another side of the visual shoulder-to-hand line.
            Vector3 pole=s.Position[ve]-(s.Position[va]+target)*.5f;
            Vector3 fallback=pose.WorldPosition[CapturedRig.LElbow]-(pose.WorldPosition[CapturedRig.LArm]+pose.WorldPosition[CapturedRig.LHand])*.5f;
            s.LeftArmReach=TwoBone(s,va,ve,vh,target,pole,fallback,r.ArmUpper,r.ArmLower);
        }

        // Analytic two-bone IK in world space: turns the upper and lower bones (and the twist bones that follow them) so the
        // end reaches the target, bending toward the pole. The end bone keeps its world turn. Returns the reach used, 0..1.
        private static float TwoBone(State s,int upper,int lower,int end,Vector3 target,Vector3 pole,Vector3 fallback,float a,float b)
        {
            var r=s.Rig; if(a<=0||b<=0) return 0;
            Vector3 root=s.Position[upper];
            Vector3 d=target-root; float dist=d.Length();
            float reach=CapturedClip.Clamp(dist,Math.Abs(a-b)+1e-4f,(a+b)*.9999f);
            d=CapturedRig.Unit(d,-CapturedRig.Y);
            Vector3 bend=pole-d*Vector3.Dot(pole,d);
            if(bend.LengthSquared()<1e-6f) bend=fallback-d*Vector3.Dot(fallback,d);
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

        private static readonly string[] Digits={"thumb","index","middle","ring","pinky"};
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
            // Fingers: the thumb's and the four fingers' three joints (metacarpals ride the hand).
            for(int v=0;v<n;v++)
            {
                string name=names[v]; int side=name.EndsWith("_l")?0:name.EndsWith("_r")?1:-1; if(side<0) continue;
                foreach(string f in Digits)
                    for(int k=1;k<=3;k++)
                        if(name==f+"_0"+k+(side==0?"_l":"_r")) { specs[v].Finger=(f=="thumb"?10:0)+k; specs[v].Side=side; }
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
