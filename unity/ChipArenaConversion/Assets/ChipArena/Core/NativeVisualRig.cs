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
    // two-bone IK (a far grip's shoulder reaches first), fists and held weapons keep out of the head and torso (Clear), the
    // weapons ride the closed fists (HoldWeapon; a braced shield stays on the wrist), every arm the hands move keeps a hinged
    // elbow (Reach), twist bones take a share of their limb's final roll, and the weapon rides the visual right hand (P1).
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
            // The body the fists and held weapons keep out of (Clear) and a shouldered stock rests on (Shouldered): the head an
            // ellipsoid about HeadCenter in the head bone's aligned frame, HeadRadii across, up and forward; the torso an
            // elliptic cylinder round the pelvis-neck line up to the shoulders, TorsoHalfWidth across and TorsoHalfDepth deep;
            // ShoulderFront, the body's front ahead of the shoulder joint at the stock's pocket. Set from the skeleton by the
            // proportions measured on the first outfit's head (.22 x .27 x .23 m round a point .08 m over its head bone, which
            // rises .148 m over the shoulder joints) and hooded top (.30 x .22 m round the spine, its front .13 m ahead of
            // shoulder joints .265 m apart); a caller that has the meshes may set its own.
            public Vector3 HeadCenter, HeadRadii; public float TorsoHalfWidth, TorsoHalfDepth, ShoulderFront;
            public readonly Vector3 PelvisRest, GamePelvisRest;
            // The closed fist per side (0 left, 1 right; Fists): where a handle runs through it, from the hand bone (FistCenter),
            // its direction toward the index side (FistAxis) and the hand's forward square to it (FistAhead), in the aligned rest.
            public readonly Vector3[] FistCenter=new Vector3[2], FistAxis=new Vector3[2], FistAhead=new Vector3[2];
            public readonly bool[] HasFist=new bool[2];

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
                Fists();
                // Floor-fit radii: a bone's rest height over the floor, capped (soles and toes touch at rest, the rest clear it).
                for(int v=0;v<Count;v++) Contact[v]=Math.Max(0,Math.Min(.12f,AlignedPosition[v].Y-Floor));
                int pelvis=Of[CapturedRig.Pelvis];
                PelvisRest=pelvis<0?Vector3.Zero:AlignedPosition[pelvis];
                GamePelvisRest=CapturedRig.Bones[CapturedRig.Body].Position+CapturedRig.Bones[CapturedRig.Pelvis].Position;
                ArmUpper=Segment(CapturedRig.RArm,CapturedRig.RElbow); ArmLower=Segment(CapturedRig.RElbow,CapturedRig.RHand);
                int head=Of[CapturedRig.Head],arm=Of[CapturedRig.RArm];
                HeadRise=head<0||arm<0?GameHeadRise:AlignedPosition[head].Y-AlignedPosition[arm].Y;
                int leftArm=Of[CapturedRig.LArm];
                float shoulders=arm<0||leftArm<0?.265f:Vector3.Distance(AlignedPosition[arm],AlignedPosition[leftArm]);
                HeadCenter=new Vector3(0,.54f,.225f)*HeadRise; HeadRadii=new Vector3(.73f,.91f,.79f)*HeadRise;
                TorsoHalfWidth=.57f*shoulders; TorsoHalfDepth=.42f*shoulders; ShoulderFront=.5f*shoulders;
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

            // The closed fist (each finger at its Closed curl, the hand at the aligned rest). A curled finger's three joints wrap
            // the handle, so the handle runs through the centres of their circles: the fist's centre is their mean and its axis
            // the fingers' mean curl axis, turned to the thumb's side. Needs at least one finger with three joints.
            private void Fists()
            {
                for(int side=0;side<2;side++)
                {
                    int hand=Of[side==0?CapturedRig.LHand:CapturedRig.RHand]; if(hand<0) continue;
                    Vector3 centres=Vector3.Zero,axis=Vector3.Zero,knuckles=Vector3.Zero,thumb=Vector3.Zero; int fingers=0; bool hasThumb=false;
                    for(int v=0;v<Count;v++)
                    {
                        if(Side[v]!=side) continue;
                        if(Finger[v]==11) { thumb=AlignedPosition[v]; hasThumb=true; }
                        if(Finger[v]!=1) continue;
                        int b=Next(v),c=b<0?-1:Next(b); if(c<0) continue;
                        Quaternion first=CapturedRig.Rot(FingerAxis[v],Curl(1,1)),second=first*CapturedRig.Rot(FingerAxis[b],Curl(2,1));
                        Vector3 p1=AlignedPosition[v],p2=p1+Vector3.Transform(RestPosition[b],first*AlignedWorld[v]),p3=p2+Vector3.Transform(RestPosition[c],second*AlignedWorld[b]);
                        centres+=Circumcentre(p1,p2,p3); axis+=FingerAxis[v]; knuckles+=p1; fingers++;
                    }
                    if(fingers==0||axis.LengthSquared()<1e-10f) continue;
                    centres/=fingers; knuckles/=fingers; axis=Vector3.Normalize(axis);
                    if(hasThumb&&Vector3.Dot(axis,thumb-knuckles)<0) axis=-axis;
                    Vector3 ahead=knuckles-AlignedPosition[hand]; ahead-=axis*Vector3.Dot(ahead,axis);
                    if(ahead.LengthSquared()<1e-10f) continue;
                    FistCenter[side]=centres-AlignedPosition[hand]; FistAxis[side]=axis; FistAhead[side]=Vector3.Normalize(ahead); HasFist[side]=true;
                }
            }
            private static Vector3 Circumcentre(Vector3 a,Vector3 b,Vector3 c)
            {
                Vector3 ab=b-a,ac=c-a,n=Vector3.Cross(ab,ac); float d=2*n.LengthSquared();
                if(d<1e-14f) return (a+b+c)/3;
                return a+(Vector3.Cross(n,ab)*ac.LengthSquared()+Vector3.Cross(ac,n)*ab.LengthSquared())/d;
            }
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
            // How far each fist (with its weapon) moved out of the head and torso this frame (Clear), metres.
            public float LeftClear, RightClear;
            // Where each hand's weapon is drawn (P1; WeaponFrame), and whether its fist is seated on a handle this frame.
            public Vector3 LeftAnchor, RightAnchor; public bool LeftSeated, RightSeated;
            // Shouldered: 0..1, a long gun's stock at the shoulder pocket. SupportSlide: how far the support fist slid back along
            // its handle to reach. ElbowSwing: each arm's elbow turn off the animation's side about the shoulder-wrist line (Reach).
            public float Shouldered, SupportSlide; public readonly float[] ElbowSwing=new float[2]; internal readonly float[] ElbowPhi=new float[2];
            internal readonly Vector3[] RefElbow=new Vector3[2];
            internal float LastSupport, LastShouldered; // last frame's two-handed and shouldered weights (WeightStep)
            internal readonly Vector3[] LastElbow=new Vector3[2]; internal readonly Quaternion[] LastHand=new Quaternion[2]; internal bool HasLast; // last frame's elbows and hands (Reach, FistTurn)
            internal readonly float[] LastSwing=new float[2]; // last frame's elbow swings off the animation's side (Reach)
            internal readonly Vector3[] LastClear=new Vector3[2]; // last frame's moves out of the body (Clear)
            internal readonly bool[] Under;
            // Drops last frame's elbows and hands (a cut, a respawn, a weapon swap): the next pose is solved afresh.
            public void Forget() { HasLast=false; }
            public State(Rig rig)
            {
                Rig=rig; int n=rig.Count;
                Turn=new Quaternion[n]; World=new Quaternion[n]; Local=new Quaternion[n]; Position=new Vector3[n]; LocalPosition=new Vector3[n]; Under=new bool[n];
            }
        }

        // A held thing's handle in its game wrist's frame (the hand bone its model is drawn on): Center, where the fist closes on
        // it; Axis, the direction the fist's index side faces along the handle (toward the blade, barrel or top; toward the
        // pommel for a reverse grip; zero: nothing to seat).
        // Slide: how far the fist may slide back along the handle (against Axis) to reach it (a long gun's handguard). Ahead:
        // where a hand that reaches for the weapon points (wrist to knuckles; a pistol grip's knuckles toward the muzzle, a
        // handguard's cupping palm up), rolling about the handle by up to Spread toward the straightest wrist; zero: the
        // straightest wrist (a riding hand's own forearm; a reaching hand's, from the forearm its reach makes).
        // Lay: where the fist's axis lies when not along the handle (a palm cradling a handguard lays it across; FistAxis).
        // Tail to Tip and CrossA to CrossB: the held thing's lines (a blade's pommel to point and crossguard, a gun's back to
        // muzzle, a shield's height and breadth), Half and CrossHalf wide either side, kept out of the body with the fist
        // (Clear); none: the fist only. Braced: the weapon keeps the game's place on the wrist and the fist reaches for the
        // handle (a shield: the game's forearm runs along its back, 15 cm behind the grip bar); unbraced, it rides the fist.
        public struct Hold
        {
            public Vector3 Center, Axis, Ahead, Lay, Tail, Tip, CrossA, CrossB; public float Slide, Spread, Half, CrossHalf; public bool Braced;
            public Hold(Vector3 center,Vector3 axis,float slide=0,Vector3 ahead=default,float spread=0) { Center=center; Axis=Vector3.Normalize(axis); Slide=slide; Ahead=ahead.LengthSquared()>0?Vector3.Normalize(ahead):Vector3.Zero; Spread=spread; Lay=Tail=Tip=CrossA=CrossB=Vector3.Zero; Half=CrossHalf=0; Braced=false; }
            public bool Seated=>Axis.LengthSquared()>.25f;
            public Vector3 FistAxis=>Lay.LengthSquared()>.25f?Lay:Axis;
            public bool Spans=>(Tip-Tail).LengthSquared()>1e-6f;
            public bool Crosses=>(CrossB-CrossA).LengthSquared()>1e-6f;
            public Hold Spanning(Vector3 tail,Vector3 tip,float half,Vector3 crossA=default,Vector3 crossB=default,float crossHalf=0) { var h=this; h.Tail=tail; h.Tip=tip; h.Half=half; h.CrossA=crossA; h.CrossB=crossB; h.CrossHalf=crossHalf; return h; }
        }

        // What the hands hold this frame. RightGrip, LeftGrip: finger curl 0..1 (open hands relax). Right, Left: each hand's own
        // weapon. Support: 0..1, whether the right hand's weapon may be held two-handed (HandsTogether gates it); SupportHold:
        // where the left fist closes on that weapon, in the right wrist's frame (none: the left keeps the game's wrist offset).
        // Stock: a long gun's stock butt behind its frame's origin along -z (0: none); shouldered while the game holds it up.
        public struct Grasp { public float RightGrip, LeftGrip, Support, Stock; public Hold Right, Left, SupportHold; }

        // The Standard weapons' handles (NativeWeaponModels). The sword family draws at the hand turned by
        // NativeSpecialPose.HeldRotation (`held`) with its grip along +z through the wrist; the shield's grip bar stands up behind
        // its face. Guns draw at the hand: the fist closes on the raked pistol grip under the bore, its index finger under the
        // grip's top. The long guns' support fist holds the handguard (rifle) or the barrel shroud (machine gun) from below,
        // sliding back toward the receiver as far as a short arm needs; the pistol's support fist closes where the game's own
        // free fist is this frame (the voxel fist's grip point, NativeKillBoxes.LeftHandSocket, on the game's left wrist),
        // round the pistol's grip. Long guns shoulder their stock butt .30 m behind the frame.
        // reverse: the blade leaves the fist's little-finger side (the daggers: their clips' forearms roll naturally only so).
        public static Hold Blade(Quaternion held,bool reverse=false)=>new Hold(Vector3.Transform(new Vector3(0,0,.01f),held),Vector3.Transform(reverse?-CapturedRig.Z:CapturedRig.Z,held));
        public static Hold GreatswordSupport(Quaternion held)=>new Hold(Vector3.Transform(new Vector3(0,0,-.10f),held),Vector3.Transform(CapturedRig.Z,held));
        public static Hold ShieldBar(Quaternion held) { var h=new Hold(Vector3.Transform(new Vector3(0,.09f,.15f),held),Vector3.Transform(CapturedRig.Y,held)); h.Braced=true; return h; }
        // A melee set's weapon hold with its extent (NativeWeaponModels.Melee: pommels, blade widths, crossguard bars, the
        // heater outline): sword, daggers (reverse; both hands), greatsword (its support: GreatswordSupport), shield (its height
        // and its breadth across the upper field). A blade counts from its pommel to BladeRoot past its guard: the hilt moves
        // with the hands, while a long blade's outer part sweeps past the head within a frame or two of a cut, and clearing it
        // jerked the hands 13 cm in a frame.
        public const float BladeRoot=.2f;
        public static Hold Melee(string spec,Quaternion held)
        {
            Vector3 At(float x,float y,float z)=>Vector3.Transform(new Vector3(x,y,z),held);
            if(spec=="sword") return Blade(held).Spanning(At(0,0,-.19f),At(0,0,.215f+BladeRoot),.04f,At(-.165f,0,.215f),At(.165f,0,.215f),.02f);
            if(spec=="daggers") return Blade(held,true).Spanning(At(0,0,-.19f),At(0,0,.168f+BladeRoot),.035f);
            if(spec=="greatsword") return Blade(held).Spanning(At(0,0,-.27f),At(0,0,.25f+BladeRoot),.06f,At(-.24f,0,.25f),At(.24f,0,.25f),.02f);
            if(spec=="shield") return ShieldBar(held).Spanning(At(0,-.34f,.25f),At(0,.42f,.25f),.04f,At(-.30f,.30f,.25f),At(.30f,.30f,.25f),.04f);
            return default;
        }
        public static Hold PistolGrip(WeaponId w)
        {
            // NativeWeaponModels.Guns' PistolGrip(centre, rake, length): its axis runs down (0,-cos rake,-sin rake).
            Vector3 centre=w==WeaponId.Pistol?new Vector3(0,-.15f,-.05f):w==WeaponId.MachineGun?new Vector3(0,-.18f,-.045f):new Vector3(0,-.135f,-.04f);
            float rake=w==WeaponId.Pistol?.22f:.2f,length=w==WeaponId.Pistol?.215f:w==WeaponId.MachineGun?.16f:.2f;
            var up=new Vector3(0,(float)Math.Cos(rake),(float)Math.Sin(rake));
            // The gun's line, its back to its muzzle (NativeWeaponModels.Guns), along the bore.
            float back=w==WeaponId.Pistol?.10f:.30f,muzzle=w==WeaponId.Pistol?.498f:w==WeaponId.MachineGun?.896f:1.205f;
            return new Hold(centre+up*(length*.5f-.045f),up,0,new Vector3(0,-(float)Math.Sin(rake),(float)Math.Cos(rake)),GripSpread).Spanning(new Vector3(0,0,-back),new Vector3(0,0,muzzle),.04f);
        }
        public static float Stock(WeaponId w)=>w==WeaponId.Rifle||w==WeaponId.MachineGun?.30f:0;
        public static Hold GunSupport(WeaponId w,CapturedRig.Pose pose)
        {
            if(w==WeaponId.Rifle) return Cradled(new Hold(new Vector3(0,-.035f,.32f),CapturedRig.Z,.14f,Cup,CupSpread));
            if(w==WeaponId.MachineGun) return Cradled(new Hold(new Vector3(0,-.04f,.38f),CapturedRig.Z,.18f,Cup,CupSpread));
            var socket=NativeKillBoxes.LeftHandSocket;
            Vector3 fist=pose.WorldPosition[CapturedRig.LHand]+Vector3.Transform(new Vector3(socket[0],socket[1],socket[2]),pose.WorldRotation[CapturedRig.LHand]);
            Vector3 local=Vector3.Transform(fist-pose.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(pose.WorldRotation[CapturedRig.RHand]));
            return new Hold(local,PistolGrip(w).Axis,0,PistolGrip(w).Ahead,GripSpread);
        }
        // The support hand cupping a handguard from below: palm up, its fingers round the weapon's right side (source -x),
        // rolling up to CupSpread either way; a hand on a pistol grip, knuckles toward the muzzle, up to GripSpread.
        private static readonly Vector3 Cup=Vector3.Normalize(new Vector3(-1,.4f,0));
        public const float CupSpread=60*(float)Math.PI/180, GripSpread=25*(float)Math.PI/180;
        // A palm cradling a handguard lays it across the hand, not along a closed fist's axis: the fist's axis turns Cradle
        // toward the weapon's left about its up (the knuckles toward the muzzle), so a forearm reaching along the barrel meets
        // the hand at an easy wrist instead of the square bend a fist closed along the barrel needs (91 degrees on the first outfit's rifle).
        // The fist still slides along the barrel (Axis).
        public const float Cradle=40*(float)Math.PI/180;
        private static Hold Cradled(Hold h) { var turn=CapturedRig.Rot(CapturedRig.Y,Cradle); h.Lay=Vector3.Transform(h.Axis,turn); h.Ahead=Vector3.Transform(h.Ahead,turn); return h; }


        // Poses the visual skeleton from a game pose (pose.World() already run). support: 0..1, whether the weapon in hand may
        // be held two-handed (the grip then follows the game's own hands, HandsTogether); plantFeet: off for rolls and airborne
        // takes; fitFloor: the roll's and tuck's Lowest() fit. rightGrip, leftGrip: 0..1, a hand closed on what it holds (the
        // weapon, a shield, an off-hand blade); the left also closes while it supports a two-handed grip; open hands relax.
        // This form seats no fists; the Grasp form adds each weapon's handles.
        public static void Pose(State s,CapturedRig.Pose pose,float support,bool plantFeet,bool fitFloor,float rightGrip=0,float leftGrip=0)
        {
            var grasp=new Grasp{RightGrip=rightGrip,LeftGrip=leftGrip,Support=support};
            Pose(s,pose,grasp,plantFeet,fitFloor);
        }
        public static void Pose(State s,CapturedRig.Pose pose,in Grasp grasp,bool plantFeet,bool fitFloor)
        {
            var r=s.Rig; int n=r.Count;
            float support=Math.Min(1,grasp.Support)*HandsTogether(pose);
            // The game can let go of (or take up) a two-handed grip within a frame; the visual hands ease over WeightStep.
            if(s.HasLast) support=s.LastSupport+CapturedClip.Clamp(support-s.LastSupport,-WeightStep,WeightStep);
            s.LastSupport=support;
            s.RightGrip=CapturedClip.Clamp(grasp.RightGrip,0,1); s.LeftGrip=Math.Max(CapturedClip.Clamp(grasp.LeftGrip,0,1),support);
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
            s.ElbowSwing[0]=s.ElbowSwing[1]=0;
            s.LeftInside=Inside(s,pose,true); s.RightInside=Inside(s,pose,false);
            Positions(s,pose);
            s.LeftClear=Clear(s,pose,true,grasp.Left); s.RightClear=Clear(s,pose,false,grasp.Right);
            if(plantFeet)
            {
                s.LeftFootWeight=Foot(s,pose,true); s.RightFootWeight=Foot(s,pose,false);
            }
            // The weapons ride the hands the arms put there (P1); a held one is placed so its handle sits in the closed fist.
            Positions(s,pose);
            int lh=r.Of[CapturedRig.LHand],rh=r.Of[CapturedRig.RHand],le=r.Of[CapturedRig.LElbow],re=r.Of[CapturedRig.RElbow];
            s.LeftAnchor=lh<0?Vector3.Zero:s.Position[lh]; s.RightAnchor=rh<0?Vector3.Zero:s.Position[rh];
            s.RefElbow[0]=le<0?Vector3.Zero:s.Position[le]; s.RefElbow[1]=re<0?Vector3.Zero:s.Position[re];
            s.Shouldered=s.SupportSlide=0;
            s.RightSeated=grasp.Right.Seated&&r.HasFist[1]&&rh>=0; s.LeftSeated=grasp.Left.Seated&&r.HasFist[0]&&lh>=0&&support<1;
            if(s.RightSeated) HoldWeapon(s,pose,false,grasp.Right,grasp.Stock);
            if(s.LeftSeated) HoldWeapon(s,pose,true,grasp.Left,0);
            if(support>0)
            {
                s.SupportWeight=support; SupportGrip(s,pose,support,grasp);
            }
            ArmTwists(s,pose);
            Positions(s,pose);
            s.LastElbow[0]=le<0?Vector3.Zero:s.Position[le]; s.LastElbow[1]=re<0?Vector3.Zero:s.Position[re];
            s.LastHand[0]=lh<0?Quaternion.Identity:Eff(s,lh); s.LastHand[1]=rh<0?Quaternion.Identity:Eff(s,rh); s.HasLast=true;
            s.LastSwing[0]=s.ElbowSwing[0]; s.LastSwing[1]=s.ElbowSwing[1];
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

        // The weapon frame on the visual right hand (P1): the game's wrist turn, placed so the weapon's handle sits in the
        // visual fist (RightAnchor; HoldWeapon), or where the visual wrist is when nothing is seated. Weapons draw at the wrist
        // frame with the grip baked into their geometry, so this frame replaces the game wrist's for the drawn weapon.
        // LeftWeaponFrame: the off-hand's (daggers).
        public static void WeaponFrame(State s,CapturedRig.Pose pose,out Vector3 position,out Quaternion rotation)
        {
            rotation=pose.WorldRotation[CapturedRig.RHand]; position=s.RightAnchor;
        }
        public static void LeftWeaponFrame(State s,CapturedRig.Pose pose,out Vector3 position,out Quaternion rotation)
        {
            rotation=pose.WorldRotation[CapturedRig.LHand]; position=s.LeftAnchor;
        }

        // A held weapon. The game's wrist frame is the voxel fist's and the weapon's; a jointed hand closes round the handle
        // instead: its fist's axis along the handle and its centre on the handle's grip point, the hand turned about the handle
        // to point as nearly along the forearm as the handle allows (the straightest wrist). The weapon rides that fist (P1):
        // the arm keeps the place the animation gave the wrist (Reach may swing the elbow) and the weapon frame is set so the
        // grip sits in the fist (moving the hand to a weapon left at the old wrist bent the aiming arm instead). A long gun the
        // game holds up to its shoulder is shouldered (Shouldered): its stock butt at the visual shoulder pocket, the fist
        // reaching for its pistol grip (Fist), blended in by the eased weight. A braced weapon (a shield) instead keeps the game's
        // place on the wrist: its frame at the fist the animation gives, as the game's is at its own; the fist then reaches for
        // the handle, the arm moving toward it (the game's arm runs along the shield's back 15 cm behind its grip bar, so a
        // shield moved onto the fist instead ran the forearm through it).
        private static void HoldWeapon(State s,CapturedRig.Pose pose,bool left,in Hold hold,float stock)
        {
            var r=s.Rig; int side=left?0:1,ga=left?CapturedRig.LArm:CapturedRig.RArm,vh=r.Of[ga+2]; if(vh<0) return;
            Quaternion wrist=pose.WorldRotation[ga+2],game=Eff(s,vh);
            Vector3 axis=Vector3.Transform(hold.FistAxis,wrist),centre=Vector3.Transform(hold.Center,wrist);
            Reach(s,pose,left,s.Position[vh],axis,game,true,out var turn);
            Positions(s,pose);
            Vector3 fist=s.Position[vh]+Vector3.Transform(r.FistCenter[side],turn);
            if(hold.Braced)
            {
                if(left) s.LeftAnchor=fist; else s.RightAnchor=fist;
                Fist(s,pose,left,fist+centre,axis,Vector3.Zero,0,turn);
                return;
            }
            Vector3 ride=fist-centre;
            if(left) { s.LeftAnchor=ride; return; }
            s.RightAnchor=ride;
            if(stock<=0) return;
            float shouldered=Shouldered(s,pose,stock,out var butted);
            if(s.HasLast) shouldered=s.LastShouldered+CapturedClip.Clamp(shouldered-s.LastShouldered,-WeightStep,WeightStep);
            s.LastShouldered=shouldered;
            if(shouldered<=0) return;
            // Blend from the riding arm to the shouldered one by the weight; the weapon rides the fist either way.
            s.Shouldered=shouldered;
            Vector3 rideWrist=s.Position[vh]; float rideSwing=s.ElbowSwing[side];
            Fist(s,pose,false,butted+centre,axis,Vector3.Transform(hold.Ahead,wrist),hold.Spread,turn);
            if(shouldered<.9999f) BlendArm(s,pose,false,rideWrist,turn,rideSwing,shouldered);
            Positions(s,pose);
            s.RightAnchor=s.Position[vh]+Vector3.Transform(r.FistCenter[side],s.Turn[vh])-centre;
        }

        // Blends an arm from a hinged pose (its wrist, hand turn and elbow swing) to the one it now has, by `w`: the wrists
        // lerped, the hands slerped and the swings lerped, the arm posed by Reach, so the elbow stays a hinge throughout.
        private static void BlendArm(State s,CapturedRig.Pose pose,bool left,Vector3 wrist,Quaternion hand,float swing,float w)
        {
            var r=s.Rig; int side=left?0:1,vh=r.Of[(left?CapturedRig.LArm:CapturedRig.RArm)+2];
            Positions(s,pose);
            Vector3 toWrist=s.Position[vh]; Quaternion toHand=s.Turn[vh]; float toSwing=s.ElbowSwing[side];
            Reach(s,pose,left,Vector3.Lerp(wrist,toWrist,w),Vector3.Zero,hand,false,out _,swing+(toSwing-swing)*w);
            TurnHand(s,vh,Quaternion.Normalize(Quaternion.Slerp(hand,toHand,w)));
        }

        // A long gun the game holds up to the shoulder (its stock butt within ShoulderedNear of the game shoulder, fading by
        // ShoulderedFar; a low carry stays held): the frame that puts the butt at the visual shoulder pocket, just inside and
        // under the shoulder joint on the body's front (Rig.ShoulderFront, less PocketPress where the butt presses into the
        // clothes; .05 m ahead of the joint put it 8 cm inside the first outfit's chest), along the game's aim. Returns the weight.
        public const float ShoulderedNear=.30f, ShoulderedFar=.45f, PocketIn=.045f, PocketDown=.035f, PocketPress=.02f;
        private static float Shouldered(State s,CapturedRig.Pose pose,float stock,out Vector3 anchor)
        {
            var r=s.Rig; int va=r.Of[CapturedRig.RArm]; anchor=Vector3.Zero; if(va<0) return 0;
            Quaternion w=pose.WorldRotation[CapturedRig.RHand],chest=pose.WorldRotation[CapturedRig.Chest];
            Vector3 gameButt=pose.WorldPosition[CapturedRig.RHand]+Vector3.Transform(new Vector3(0,0,-stock),w);
            float weight=1-CapturedClip.Ease((Vector3.Distance(gameButt,pose.WorldPosition[CapturedRig.RArm])-ShoulderedNear)/(ShoulderedFar-ShoulderedNear));
            Vector3 outward=Vector3.Transform(-CapturedRig.X,chest),up=Vector3.Transform(CapturedRig.Y,chest),ahead=Vector3.Transform(CapturedRig.Z,chest);
            Vector3 pocket=s.Position[va]-outward*PocketIn-up*PocketDown+ahead*(r.ShoulderFront-PocketPress);
            anchor=pocket+Vector3.Transform(new Vector3(0,0,stock),w);
            return weight;
        }

        // Puts a reaching fist on a grip point: aimed (Aim) for the forearm the animation's elbow gives, the arm reaches (Reach)
        // for the wrist that puts the fist's centre there; then FistPasses re-aim for the forearm the last reach made, turning
        // the hand halfway there each time (the fist sits ~9 cm off the wrist, so its turn swings the wrist and the forearm
        // with it), the elbow kept at the first reach's swing. A fixed number of smooth steps, not a chase to the end: hand and
        // forearm can't always agree (some two-handed cuts have no turn straightest for the forearm it causes), and this stays
        // smooth frame to frame. The last places the wrist for the turn it keeps, so the fist sits on the grip. Returns the reach.
        public const int FistPasses=4;
        private static float Fist(State s,CapturedRig.Pose pose,bool left,Vector3 grip,Vector3 axis,Vector3 ahead,float spread,Quaternion game)
        {
            var r=s.Rig; int side=left?0:1,ga=left?CapturedRig.LArm:CapturedRig.RArm,ve=r.Of[ga+1],vh=r.Of[ga+2];
            Quaternion turn=Aim(s,side,axis,grip-s.RefElbow[side],ahead,spread,game);
            float reach=Reach(s,pose,left,grip-Vector3.Transform(r.FistCenter[side],turn),axis,turn,false,out _);
            for(int pass=0;pass<FistPasses;pass++)
            {
                Positions(s,pose);
                turn=Quaternion.Normalize(Quaternion.Slerp(turn,Aim(s,side,axis,s.Position[vh]-s.Position[ve],ahead,spread,game),.5f));
                reach=Reach(s,pose,left,grip-Vector3.Transform(r.FistCenter[side],turn),axis,turn,false,out _,float.NaN,s.ElbowPhi[side]);
            }
            return reach;
        }
        // A reaching fist's turn for a forearm: the straightest wrist, or the weapon's set direction (ahead, in world) rolled
        // toward it by up to `spread`; from last frame's hand by at most HandStep when there is one (FistTurn).
        private static Quaternion Aim(State s,int side,Vector3 axis,Vector3 forearm,Vector3 ahead,float spread,Quaternion game)
        {
            Vector3 want=forearm;
            if(ahead.LengthSquared()>.25f)
            {
                Vector3 set=ahead-axis*Vector3.Dot(ahead,axis),straight=forearm-axis*Vector3.Dot(forearm,axis);
                float roll=straight.LengthSquared()<1e-8f||set.LengthSquared()<1e-8f?0:(float)Math.Atan2(Vector3.Dot(axis,Vector3.Cross(set,straight)),Vector3.Dot(set,straight));
                want=Vector3.Transform(set,CapturedRig.Rot(axis,CapturedClip.Clamp(roll,-spread,spread)));
            }
            return FistTurn(s.Rig,side,axis,want,s.HasLast?s.LastHand[side]:game,s.HasLast);
        }


        // An arm to a wrist point, kept anatomical. The elbow hinges: the upper arm's fold side (+z of the game-aligned frame,
        // where the game's own elbow folds) faces the forearm and the forearm shares its hinge (HingeTurns), so the crease never
        // turns inside out the way bending each bone the shortest way did. The elbow sits on the circle the two bones allow,
        // on the animation's side (RefElbow), swung about the shoulder-wrist line only to spare the wrist a bend past
        // WristComfort, the forearm a roll past ForearmRoll, the humerus a roll past HumerusRoll and the torso a forearm in it;
        // the swing is a soft minimum over the circle. Candidates are measured from last frame's elbow carried with the
        // animation's (SwingHold prefers it, SwingStep caps the move), so two comparable elbows trading places glide over a few
        // frames instead of snapping; the animation's side counts as far as its own elbow sits off the shoulder-wrist line (a
        // straight arm names no side). A riding fist (`ride`, on `axis`) turns onto its
        // handle for each candidate (FistTurn, steadied by `turn`); any other hand keeps `turn` (a reaching fist on `axis` has
        // its wrist bend weighed too). `swing`, when given, fixes the elbow's swing from the animation's side; `about`, from the
        // side candidates swing about (last frame's elbow; ElbowPhi). Returns the reach used; `hand`, the hand turn.
        public const float ElbowSpread=30*(float)Math.PI/180, WristComfort=60*(float)Math.PI/180, ForearmRoll=90*(float)Math.PI/180,
            HumerusRoll=110*(float)Math.PI/180, TorsoClear=.15f, ForearmClear=.13f;
        private const int Swings=31; private const float SwingRange=90*(float)Math.PI/180, Softness=.1f;
        // SwingStep: the most an elbow's swing may change from one pose to the next (a frame), so a large correction (a cut whose
        // game arm rolls the upper arm past any human's) eases in over a few frames instead of whipping round.
        public const float SwingHold=12*(float)Math.PI/180, SwingStep=4*(float)Math.PI/180, WeightStep=.08f, FollowMost=45*(float)Math.PI/180;
        private static float Reach(State s,CapturedRig.Pose pose,bool left,Vector3 wrist,Vector3 axis,Quaternion turn,bool ride,out Quaternion hand,float swing=float.NaN,float about=float.NaN)
        {
            var r=s.Rig; int side=left?0:1,ga=left?CapturedRig.LArm:CapturedRig.RArm,vc=r.Of[ga-1],va=r.Of[ga],ve=r.Of[ga+1],vh=r.Of[ga+2];
            hand=Eff(s,vh); float a=r.ArmUpper,b=r.ArmLower; if(va<0||ve<0||vh<0||a<=0||b<=0) return 0;
            Vector3 S=s.Position[va],to=wrist-S; float d=CapturedClip.Clamp(to.Length(),Math.Abs(a-b)+1e-4f,(a+b)*.9999f);
            Vector3 n=CapturedRig.Unit(to,-CapturedRig.Y),P=S+n*d;
            float along=(a*a-b*b+d*d)/(2*d),h=(float)Math.Sqrt(Math.Max(0,a*a-along*along));
            Vector3 C=S+n*along,anim=s.RefElbow[side]-C; anim-=n*Vector3.Dot(anim,n);
            float sure=h>1e-4f?CapturedClip.Clamp(anim.Length()/(.25f*h),0,1):0;
            if(anim.LengthSquared()<1e-8f) { anim=pose.WorldPosition[ga+1]-(pose.WorldPosition[ga]+pose.WorldPosition[ga+2])*.5f; anim-=n*Vector3.Dot(anim,n); }
            if(anim.LengthSquared()<1e-8f) anim=Vector3.Cross(n,Math.Abs(n.Y)<.9f?CapturedRig.Y:CapturedRig.X);
            anim=Vector3.Normalize(anim);
            // Candidates swing about last frame's elbow side when there is one, else about the animation's.
            bool keep=s.HasLast; Vector3 was=keep?s.LastElbow[side]-C:Vector3.Zero; was-=n*Vector3.Dot(was,n);
            Vector3 e1=keep&&was.LengthSquared()>1e-8f?Vector3.Normalize(was):anim,e2=Vector3.Cross(n,e1);
            float offset=(float)Math.Atan2(Vector3.Dot(n,Vector3.Cross(anim,e1)),Vector3.Dot(anim,e1)); // e1 from the animation's side
            // Last frame's swing off the animation's side, on this frame's circle: the elbow moves with the animation's own, so
            // only the correction is held (SwingHold) and rate-limited (SwingStep). Held from last frame's elbow alone, a fast
            // cut's elbow trailed the animation's by up to 68 degrees, into the torso. Only while that side is sure (a straight arm
            // names none) and has moved plausibly since last frame (under FollowMost; more is an unsure side jumping round the
            // arm): blending the two otherwise set a support elbow circling 20 degrees a frame.
            float follow=keep?Wrap(s.LastSwing[side]-offset):0;
            float centre=follow*CapturedClip.Ease((sure-.5f)/.4f)*CapturedClip.Clamp((FollowMost-Math.Abs(follow))/(FollowMost/3),0,1);
            Quaternion shoulder=(vc<0?Quaternion.Identity:Eff(s,vc))*Spread(pose,left),upperNow=Eff(s,va);
            if(ride&&s.HasLast) turn=s.LastHand[side]; // a riding fist's steadying reference (FistTurn)
            int pelvis=r.Of[CapturedRig.Pelvis],neck=r.Of[CapturedRig.Neck];
            Vector3 low=pelvis<0?S:s.Position[pelvis],high=neck<0?S:s.Position[neck];
            Vector3 grip=axis;
            float phiAt;
            if(!float.IsNaN(about)) phiAt=about;
            else if(!float.IsNaN(swing)) phiAt=Wrap(swing-offset);
            else
            {
                float best=float.MaxValue;
                for(int i=0;i<Swings;i++) best=Math.Min(best,ArmCost(r,side,S,C,P,h,e1,e2,centre-SwingRange+2*SwingRange*i/(Swings-1),grip,ride,turn,shoulder,upperNow,low,high,offset,sure,keep,centre,out _,out _,out _));
                float sum=0,weighted=0;
                for(int i=0;i<Swings;i++)
                {
                    float phi=centre-SwingRange+2*SwingRange*i/(Swings-1);
                    float w=(float)Math.Exp(-(ArmCost(r,side,S,C,P,h,e1,e2,phi,grip,ride,turn,shoulder,upperNow,low,high,offset,sure,keep,centre,out _,out _,out _)-best)/Softness);
                    sum+=w; weighted+=w*phi;
                }
                phiAt=weighted/sum;
                if(keep) phiAt=CapturedClip.Clamp(phiAt,centre-SwingStep,centre+SwingStep);
            }
            s.ElbowSwing[side]=Wrap(phiAt+offset); s.ElbowPhi[side]=phiAt;
            ArmCost(r,side,S,C,P,h,e1,e2,phiAt,grip,ride,turn,shoulder,upperNow,low,high,offset,sure,keep,centre,out var upper,out var fore,out hand);
            s.Turn[va]=upper; s.World[va]=Quaternion.Normalize(upper*r.AlignedWorld[va]);
            s.Turn[ve]=fore; s.World[ve]=Quaternion.Normalize(fore*r.AlignedWorld[ve]);
            TurnHand(s,vh,hand);
            return d/(a+b);
        }
        // One candidate elbow (swung `phi` from the animation's side): the bones' turns and the cost of the pose.
        private static float ArmCost(Rig r,int side,Vector3 S,Vector3 C,Vector3 P,float h,Vector3 e1,Vector3 e2,float phi,Vector3 axis,bool ride,Quaternion turn,
            Quaternion shoulder,Quaternion upperNow,Vector3 low,Vector3 high,float offset,float sure,bool keep,float centre,out Quaternion upper,out Quaternion fore,out Quaternion hand)
        {
            Vector3 E=C+(e1*(float)Math.Cos(phi)+e2*(float)Math.Sin(phi))*h;
            HingeTurns(S,E,P,upperNow,out upper,out fore);
            bool fist=axis.LengthSquared()>.25f;
            hand=ride&&fist?FistTurn(r,side,axis,P-E,turn,keep):turn;
            float cost=sure*Square(Wrap(phi+offset)/ElbowSpread)+(keep?Square(Wrap(phi-centre)/SwingHold):0);
            if(fist)
            {
                float bend=(float)Math.Acos(CapturedClip.Clamp(Vector3.Dot(CapturedRig.Unit(P-E,-CapturedRig.Y),Vector3.Transform(r.FistAhead[side],hand)),-1,1));
                cost+=Square(Math.Max(0,bend-WristComfort)/.26f);
            }
            cost+=Square(Math.Max(0,Math.Abs(Twist(Quaternion.Inverse(fore)*hand))-ForearmRoll)/.35f);
            cost+=Square(Math.Max(0,Math.Abs(Twist(Quaternion.Inverse(shoulder)*upper))-HumerusRoll)/.35f);
            cost+=Sink(TorsoClear-Segment(E,low,high))+Sink(ForearmClear-Segment((E+P)*.5f,low,high));
            return cost;
        }
        private static float Square(float x)=>x*x;
        // An elbow or forearm `depth` inside the torso line: resting against it costs little (2 cm, as a 24 degree swing off the
        // animation's elbow), sinking in steeply more (5 cm, as a 97 degree swing): the depth over 3 cm squared, plus its fourth
        // power. With the square alone, a dagger cut's elbow sank 9 cm into the belly rather than swing out.
        private static float Sink(float depth) { float x=Math.Max(0,depth)/.03f; return x*x*(1+x*x); }
        private static float Wrap(float a)=>a>Math.PI?a-2*(float)Math.PI:a<-Math.PI?a+2*(float)Math.PI:a;
        private static float Segment(Vector3 p,Vector3 a,Vector3 b)
        {
            Vector3 ab=b-a; float t=ab.LengthSquared()<1e-10f?0:CapturedClip.Clamp(Vector3.Dot(p-a,ab)/ab.LengthSquared(),0,1);
            return Vector3.Distance(p,a+ab*t);
        }

        // A hinged arm's turns (game-aligned frames): the upper arm points down its bone (-y) with its fold side (+z) toward the
        // forearm; the forearm points down its own bone and shares the upper arm's hinge (x). A straight arm keeps `roll`'s.
        public static void HingeTurns(Vector3 S,Vector3 E,Vector3 W,Quaternion roll,out Quaternion upper,out Quaternion fore)
        {
            Vector3 u=CapturedRig.Unit(E-S,-CapturedRig.Y),f=CapturedRig.Unit(W-E,u),fold=f-u*Vector3.Dot(f,u);
            if(fold.LengthSquared()<1e-6f) { fold=Vector3.Transform(CapturedRig.Z,roll); fold-=u*Vector3.Dot(fold,u); }
            Vector3 z=CapturedRig.Unit(fold,Vector3.Normalize(Vector3.Cross(u,Math.Abs(u.X)<.9f?CapturedRig.X:CapturedRig.Z))),x=Vector3.Cross(-u,z);
            upper=CapturedRig.Basis(x,-u,z);
            fore=CapturedRig.Basis(x,-f,Vector3.Cross(x,-f));
        }

        // The arm twist bones follow the final pose: forearm bones take their share of the hand's roll on the forearm, upper-arm
        // bones counter the upper arm's roll on the shoulder (as the copied pose does from the game's), after the hands and IK.
        private static void ArmTwists(State s,CapturedRig.Pose pose)
        {
            var r=s.Rig;
            for(int v=0;v<r.Count;v++)
            {
                int j=r.TwistJoint[v],p=r.Parent[v]; if(j<0||p<0) continue;
                bool handRoll=j==CapturedRig.LHand||j==CapturedRig.RHand,armRoll=j==CapturedRig.LArm||j==CapturedRig.RArm; if(!handRoll&&!armRoll) continue;
                int bone=r.Of[j]; if(bone<0) continue;
                Quaternion parent=Eff(s,p),roll;
                if(handRoll) roll=Quaternion.Inverse(parent)*Eff(s,bone);
                else { int c=r.Of[j-1]; roll=Quaternion.Inverse((c<0?Quaternion.Identity:Eff(s,c))*Spread(pose,j==CapturedRig.LArm))*parent; }
                s.Turn[v]=Quaternion.Normalize(parent*CapturedRig.Rot(CapturedRig.Y,r.TwistShare[v]*Twist(roll)));
                s.World[v]=Quaternion.Normalize(s.Turn[v]*r.AlignedWorld[v]);
            }
        }
        // A bone's turn from its aligned rest as it stands (IK writes world turns only).
        private static Quaternion Eff(State s,int v)=>Quaternion.Normalize(s.World[v]*Quaternion.Inverse(s.Rig.AlignedWorld[v]));

        // The hand turn (from the aligned rest) that lays the fist's axis along `axis` and points the hand as nearly along
        // The hand turn (from the aligned rest) that lays the fist's axis along `axis` and points the hand as nearly along
        // `forearm` as that leaves (the straightest wrist). With `steady`, `reference` is last frame's hand: the hand rolls about
        // the handle from where it was toward the straightest by at most HandStep, so a forearm sweeping across the handle's
        // line bends the wrist through instead of flipping the hand over. Without, `reference` is the game wrist's, taking over
        // only near the handle's line (within 30 degrees), all of it on the line.
        public const float HandStep=8*(float)Math.PI/180;
        public static Quaternion FistTurn(Rig r,int side,Vector3 axis,Vector3 forearm,Quaternion reference,bool steady=false)
        {
            Vector3 k=r.FistAxis[side],d=r.FistAhead[side];
            forearm=CapturedRig.Unit(forearm,-CapturedRig.Y);
            Vector3 ahead=forearm-axis*Vector3.Dot(forearm,axis),before=Vector3.Transform(d,reference);before-=axis*Vector3.Dot(before,axis);
            before=CapturedRig.Unit(before,Vector3.Zero);
            if(steady&&before.LengthSquared()>.5f)
            {
                float roll=ahead.LengthSquared()<1e-8f?0:(float)Math.Atan2(Vector3.Dot(axis,Vector3.Cross(before,ahead)),Vector3.Dot(before,ahead));
                ahead=Vector3.Transform(before,CapturedRig.Rot(axis,CapturedClip.Clamp(roll,-HandStep,HandStep)));
            }
            else ahead+=before*Math.Max(0,.5f-ahead.Length());
            ahead=CapturedRig.Unit(ahead,Vector3.Normalize(Vector3.Cross(axis,Math.Abs(axis.Y)<.9f?CapturedRig.Y:CapturedRig.X)));
            return Quaternion.Normalize(CapturedRig.Basis(axis,ahead,Vector3.Cross(axis,ahead))*Quaternion.Inverse(CapturedRig.Basis(k,d,Vector3.Cross(k,d))));
        }

        // Sets a hand's turn, carrying its fingers (every non-joint bone below it).
        private static void TurnHand(State s,int hand,Quaternion turn)
        {
            var r=s.Rig; Quaternion delta=Quaternion.Normalize(turn*Quaternion.Inverse(s.Turn[hand]));
            s.Turn[hand]=turn; s.World[hand]=Quaternion.Normalize(turn*r.AlignedWorld[hand]);
            for(int v=hand+1;v<r.Count;v++)
            {
                int p=r.Parent[v]; bool under=r.Joint[v]<0&&(p==hand||(p>hand&&s.Under[p])); s.Under[v]=under;
                if(under) { s.Turn[v]=Quaternion.Normalize(delta*s.Turn[v]); s.World[v]=Quaternion.Normalize(delta*s.World[v]); }
            }
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
        // raised. The moved hand is drawn back out to the copied arm's extension (how straight the arm is: a shift alone drew
        // aimed hands back to the chest) along its own direction, which stays smooth where solving the depth alone went steep
        // (a raised greatsword's hand jumped 0.4 m in a frame). The whole arm swings at the shoulder by the shortest turn that brings the hand
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
            Vector3 up=Vector3.Transform(CapturedRig.Y,pose.WorldRotation[CapturedRig.Chest]);
            Vector3 moved=copied+side*((gameHand*shoulder/gameShoulder-hand)*w)-up*(HeadDrop(r,Vector3.Dot(pose.WorldPosition[gh]-pose.WorldPosition[ga],up))*w);
            Quaternion swing=CapturedRig.FromTo(CapturedRig.Unit(copied,-CapturedRig.Y),CapturedRig.Unit(moved,CapturedRig.Unit(copied,-CapturedRig.Y)));
            Rotate(s,va,ve,swing); Rotate(s,ve,vh,swing);
            return w;
        }

        // Fists and what they hold keep out of the visual body (owner review 2026-10-22: the greatsword's fists and crossguard in
        // the head, long-gun stocks in the chest). The game keeps blades off its own blocky body (MeleeClearance), not off this
        // one: a different head and chest, and hands moved inside the shoulder line, still ran fists and weapons into them. Each
        // fist (turned as it will ride its handle; FistHeadClear round its centre against the head, which keeps the back of a
        // closed hand out, FistClear against the torso, where a hand may rest) and its weapon's lines (Hold: Tail to Tip, CrossA
        // to CrossB, Half and CrossHalf round them) are tested against the head ellipsoid and the torso cylinder (Rig.HeadCenter,
        // HeadRadii, TorsoHalfWidth, TorsoHalfDepth). Fist and weapon move as one, straight out by the inside points' pushes
        // averaged by depth (a single deepest point flips between two near-equal contacts frame to frame), and the arm reaches
        // the wrist there (Reach, hinged, its elbow on the animation's side: no frame history here, which would be measured
        // from a different elbow than the hold's reach after it and set the two rocking frame to frame, 5-7 cm an elbow), the
        // hand keeping its turn. Only that arm moves: the weapon rides its fist (a braced one, the fist the animation gives)
        // and a support hand follows the weapon. Three passes, each continuous in the pose. The move changes by at most
        // ClearStep a frame: a cut's hands can cross the head's volume in three frames, and following at once jerked a
        // greatsword's hands 16 cm and its support hand 40 cm in a frame; so a fast pass may graze. Returns the distance moved.
        public const float FistClear=.05f, FistHeadClear=.08f, ClearStep=.03f; private const int ClearPasses=3, ClearSamples=48;
        private static float Clear(State s,CapturedRig.Pose pose,bool left,in Hold hold)
        {
            var r=s.Rig; int side=left?0:1,ga=left?CapturedRig.LArm:CapturedRig.RArm,ve=r.Of[ga+1],vh=r.Of[ga+2];
            if(ve<0||vh<0) return 0;
            Quaternion wrist=pose.WorldRotation[ga+2];
            bool seated=hold.Seated&&r.HasFist[side],spans=seated&&hold.Spans;
            float moved=0;
            // The fist as it will ride its handle from the copied wrist, carried rigidly with the wrist through the passes.
            Quaternion turn=Eff(s,vh);
            if(seated) turn=FistTurn(r,side,Vector3.Transform(hold.FistAxis,wrist),s.Position[vh]-s.Position[ve],turn);
            Vector3 centre=Vector3.Transform(r.FistCenter[side],turn),total=Vector3.Zero;
            for(int pass=0;pass<ClearPasses;pass++)
            {
                Vector3 fist=s.Position[vh]+total+centre,sum=Vector3.Zero; float weight=0;
                Gather(Out(s,fist,FistHeadClear,FistClear),ref sum,ref weight);
                if(spans)
                {
                    Vector3 origin=hold.Braced?fist:fist-Vector3.Transform(hold.Center,wrist);
                    Line(s,origin+Vector3.Transform(hold.Tail,wrist),origin+Vector3.Transform(hold.Tip,wrist),hold.Half,ref sum,ref weight);
                    if(hold.Crosses) Line(s,origin+Vector3.Transform(hold.CrossA,wrist),origin+Vector3.Transform(hold.CrossB,wrist),hold.CrossHalf,ref sum,ref weight);
                }
                if(weight<1e-12f) break;
                Vector3 push=sum/weight; if(push.LengthSquared()<1e-8f) break;
                total+=push;
            }
            if(s.HasLast) { Vector3 change=total-s.LastClear[side]; float most=change.Length(); if(most>ClearStep) total=s.LastClear[side]+change*(ClearStep/most); }
            s.LastClear[side]=total;
            if(total.LengthSquared()<1e-8f) return 0;
            s.RefElbow[side]=s.Position[ve];
            Reach(s,pose,left,s.Position[vh]+total,Vector3.Zero,Eff(s,vh),false,out _,0);
            Positions(s,pose); moved=total.Length();
            return moved;
        }
        // The pushes along a weapon line (sampled every 4 cm), gathered by depth.
        private static void Line(State s,Vector3 a,Vector3 b,float half,ref Vector3 sum,ref float weight)
        {
            int n=Math.Min(ClearSamples,(int)Math.Ceiling(Vector3.Distance(a,b)/.04f)+1);
            for(int i=0;i<n;i++) Gather(Out(s,Vector3.Lerp(a,b,i/(float)(n-1)),half,half),ref sum,ref weight);
        }
        // A push counted by the cube of its depth: the deepest points lead, near-equal ones blend.
        private static void Gather(Vector3 push,ref Vector3 sum,ref float weight) { float d=push.LengthSquared(); if(d<1e-10f) return; float w=d*(float)Math.Sqrt(d); sum+=push*w; weight+=w; }
        // Which way and how far a point must move to stand `head` outside the head and `torso` outside the torso (zero when
        // it does): out along its own direction from the ellipsoid's centre, or from the torso line within its cross-section.
        private static Vector3 Out(State s,Vector3 p,float headMargin,float torsoMargin)
        {
            var r=s.Rig; Vector3 best=Vector3.Zero;
            int head=r.Of[CapturedRig.Head],pelvis=r.Of[CapturedRig.Pelvis],neck=r.Of[CapturedRig.Neck],chest=r.Of[CapturedRig.Chest],la=r.Of[CapturedRig.LArm],ra=r.Of[CapturedRig.RArm];
            if(head>=0)
            {
                Quaternion q=Eff(s,head); Vector3 radii=r.HeadRadii+new Vector3(headMargin);
                Vector3 l=Vector3.Transform(p-s.Position[head]-Vector3.Transform(r.HeadCenter,q),Quaternion.Inverse(q));
                float k=(l/radii).Length();
                if(k<1) best=Vector3.Transform(k<1e-4f?new Vector3(0,0,radii.Z):l*(1/k-1),q);
            }
            if(pelvis>=0&&neck>=0&&chest>=0&&la>=0&&ra>=0)
            {
                // The torso runs from the pelvis up to the shoulders (the neck and head above are the head's).
                Vector3 low=s.Position[pelvis],line=s.Position[neck]-low; float span=Math.Max(line.LengthSquared(),1e-8f);
                float t=Vector3.Dot(p-low,line)/span,top=Vector3.Dot((s.Position[la]+s.Position[ra])*.5f-low,line)/span;
                if(t>=0&&t<=top)
                {
                    Quaternion q=Eff(s,chest); Vector3 x=Vector3.Transform(CapturedRig.X,q),z=Vector3.Transform(CapturedRig.Z,q),d=p-low-line*t;
                    float rx=Vector3.Dot(d,x),rz=Vector3.Dot(d,z),ux=rx/(r.TorsoHalfWidth+torsoMargin),uz=rz/(r.TorsoHalfDepth+torsoMargin),k=(float)Math.Sqrt(ux*ux+uz*uz);
                    if(k<1)
                    {
                        Vector3 o=k<1e-4f?z*(r.TorsoHalfDepth+torsoMargin):(x*rx+z*rz)*(1/k-1);
                        if(o.LengthSquared()>best.LengthSquared()) best=o;
                    }
                }
            }
            return best;
        }

        // Two-handed holds: the left fist closes on the weapon's SupportHold (without one, the left hand keeps the game's
        // right-to-left wrist offset, carried onto the visual right wrist). Where that lies past SupportReach of the left arm
        // (narrower shoulders gain less reach from the game's torso turn, and the weapon hand keeps the game's extension), the
        // fist first slides back along its handle (Slide; SupportSlide), then the left shoulder reaches as a body does (the
        // clavicle turns its shoulder end toward the grip, up to ShoulderReach; ShoulderTurn), and only what is still out of
        // reach makes the weapon give way toward the left shoulder (GiveWay, metres), the right fist staying on it. Both arms
        // pose through Reach.
        public const float SupportReach=.97f, ShoulderReach=25*(float)Math.PI/180;
        private static void SupportGrip(State s,CapturedRig.Pose pose,float weight,in Grasp grasp)
        {
            var r=s.Rig;
            int va=r.Of[CapturedRig.LArm],ve=r.Of[CapturedRig.LElbow],vh=r.Of[CapturedRig.LHand],right=r.Of[CapturedRig.RHand];
            if(va<0||ve<0||vh<0||right<0) return;
            Positions(s,pose);
            bool seated=grasp.SupportHold.Seated&&r.HasFist[0];
            Quaternion weapon=pose.WorldRotation[CapturedRig.RHand],game=Eff(s,vh),fist=game;
            Vector3 axis=seated?Vector3.Transform(grasp.SupportHold.Axis,weapon):Vector3.Zero,lay=seated?Vector3.Transform(grasp.SupportHold.FistAxis,weapon):Vector3.Zero,handle=Vector3.Zero;
            Vector3 target=s.Position[right]+pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand];
            float most=(r.ArmUpper+r.ArmLower)*SupportReach;
            if(seated)
            {
                handle=s.RightAnchor+Vector3.Transform(grasp.SupportHold.Center,weapon);
                fist=Aim(s,0,lay,handle-s.RefElbow[0],Vector3.Transform(grasp.SupportHold.Ahead,weapon),grasp.SupportHold.Spread,game);
                target=handle-Vector3.Transform(r.FistCenter[0],fist);
                // Slide back along the handle as far as the reach needs (the handle's share of the way to the shoulder).
                Vector3 toward=target-s.Position[va]; float far0=toward.Length(),back=Vector3.Dot(axis,toward/Math.Max(far0,1e-5f));
                if(grasp.SupportHold.Slide>0&&far0>most&&back>.2f)
                {
                    s.SupportSlide=Math.Min(grasp.SupportHold.Slide,(far0-most)/back)*weight;
                    handle-=axis*s.SupportSlide; target-=axis*s.SupportSlide;
                }
            }
            Vector3 grip=target-s.Position[va]; float far=grip.Length();
            int lc=r.Of[CapturedRig.LClav];
            float left=far; // the grip's distance once the shoulder has reached in full
            if(far>most&&lc>=0)
            {
                // Turn the clavicle in the plane of its shoulder end and the grip, by the angle that brings the shoulder to
                // SupportReach of the grip (the law of cosines), capped.
                Vector3 pivot=s.Position[lc],end=s.Position[va]-pivot,to=target-pivot,turnAxis=Vector3.Cross(end,to);
                float a=end.Length(),d=to.Length();
                if(a>1e-4f&&d>1e-4f&&turnAxis.LengthSquared()>1e-12f)
                {
                    float between=(float)Math.Acos(CapturedClip.Clamp(Vector3.Dot(end,to)/(a*d),-1,1));
                    float need=between-(float)Math.Acos(CapturedClip.Clamp((a*a+d*d-most*most)/(2*a*d),-1,1));
                    float turn=CapturedClip.Clamp(need,0,ShoulderReach);
                    left=(float)Math.Sqrt(Math.Max(0,a*a+d*d-2*a*d*Math.Cos(between-turn)));
                    s.ShoulderTurn=turn*weight;
                    Rotate(s,lc,va,CapturedRig.Rot(Vector3.Normalize(turnAxis),s.ShoulderTurn));
                    Positions(s,pose);
                    grip=target-s.Position[va]; far=grip.Length();
                }
            }
            if(left>most+1e-5f)
            {
                Vector3 shift=-grip*((left-most)/far*weight);
                s.GiveWay=(left-most)*weight; s.RightAnchor+=shift; target+=shift; handle+=shift;
                Quaternion held=Eff(s,right);
                if(s.RightSeated)
                {
                    Vector3 rightAxis=Vector3.Transform(grasp.Right.FistAxis,weapon),rightGrip=s.RightAnchor+Vector3.Transform(grasp.Right.Center,weapon);
                    Reach(s,pose,false,rightGrip-Vector3.Transform(r.FistCenter[1],held),rightAxis,held,false,out _);
                }
                else Reach(s,pose,false,s.Position[right]+shift,Vector3.Zero,held,false,out _);
                Positions(s,pose);
            }
            if(seated)
            {
                // The fist on its grip, blended from the free arm by the grip's weight.
                Vector3 freeWrist=s.Position[vh]; float freeSwing=s.LeftSeated?s.ElbowSwing[0]:0;
                s.LeftArmReach=Fist(s,pose,true,handle,lay,Vector3.Transform(grasp.SupportHold.Ahead,weapon),grasp.SupportHold.Spread,game);
                if(weight<.9999f) BlendArm(s,pose,true,freeWrist,game,freeSwing,weight);
                return;
            }
            s.LeftArmReach=Reach(s,pose,true,Vector3.Lerp(s.Position[vh],target,weight),Vector3.Zero,game,false,out _);
        }

        // Analytic two-bone IK in world space for the legs (planted feet; arms pose through Reach): turns the upper and lower
        // bones (and the twist bones that follow them) so the end reaches the target, bending toward the pole. The end bone
        // keeps its world turn. Returns the reach used, 0..1.
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
