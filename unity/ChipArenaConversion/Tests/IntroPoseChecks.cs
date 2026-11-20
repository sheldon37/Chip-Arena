using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ChipArena.UnityConversion;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Intros v2's poses (owner 2026-11-20: "almost all have weird orientations ... Like the rifle one you did, arms looked really
    /// odd?"; docs/INTROS-V2.md 2b and "Pose craft"): for every registered intro, every 1/30 s (its keys and everything between),
    /// 1. **the mapping, bone for bone:** the keyed pose bound onto the model's skeleton (the measured one, as VisualIntroPose binds
    ///    a v2 intro: its free arms as keyed) turns every bone as its game joint turns: the legs, the spine, the neck, the head and the
    ///    feet exactly; each arm segment's direction within the binding's own corrections (the hanging arm's 15° spread, a hand kept out
    ///    of the body) and its twist about its own length within a few degrees (no axis or twist flip);
    /// 2. **anatomical limits** on the keyed pose itself: elbows and knees bend one way only and within range, wrists within flexion and
    ///    twist, the forearm's twist within about 90°, shoulders and hips within their ranges, the spine, neck and head too, and no hand,
    ///    elbow or knee inside the torso. Each finding names the intro, the time span and the joint, so its author can fix the keys.
    /// The limits report everywhere; they fail only under `--intro-limits` (Strict) until the authors' current poses are fixed.
    /// </summary>
    internal static class IntroPoseChecks
    {
        private static int count;private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro poses: "+what);count++;}
        private const float Deg=57.29578f;
        // ---- the mapping ----
        private static readonly (string name,int joint,int child,bool limb)[] Bones=
        {
            ("pelvis",CapturedRig.Pelvis,-1,false),("spine",CapturedRig.Spine,-1,false),("chest",CapturedRig.Chest,-1,false),("neck",CapturedRig.Neck,-1,false),("head",CapturedRig.Head,-1,false),
            ("L upper arm",CapturedRig.LArm,CapturedRig.LElbow,true),("L forearm",CapturedRig.LElbow,CapturedRig.LHand,true),("L hand",CapturedRig.LHand,-1,false),
            ("R upper arm",CapturedRig.RArm,CapturedRig.RElbow,true),("R forearm",CapturedRig.RElbow,CapturedRig.RHand,true),("R hand",CapturedRig.RHand,-1,false),
            ("L thigh",CapturedRig.LHip,CapturedRig.LKnee,true),("L shin",CapturedRig.LKnee,CapturedRig.LFoot,true),("L foot",CapturedRig.LFoot,-1,false),
            ("R thigh",CapturedRig.RHip,CapturedRig.RKnee,true),("R shin",CapturedRig.RKnee,CapturedRig.RFoot,true),("R foot",CapturedRig.RFoot,-1,false),
        };
        /// <summary>The binding's own tolerances: a leg, the spine, the neck, the head or a foot turns as keyed (deg); an arm segment's twist
        /// about its length; an arm segment's direction (the hanging spread, a hand kept out of the body, a fist out of the head); a hand's turn.</summary>
        // (Measured 2026-11-20 over the 21 registered intros with the arms as keyed: the legs, spine, neck, head and feet 0.0; each arm 13-15
        // deg in the unarmed intros (the hanging spread), up to 41 deg and 50 deg of twist where a keyed hand passes inside the body (Inside
        // and Clear move it out); a hand up to 101 deg where the keyed wrist is bent past the model's wrist (Wrist eases it). With the match's
        // relax on the free arms, as before 2026-11-20, an arm moved up to 111 deg off its keys.) Past these, a bone maps wrong.
        public const float BodyTurn=1,ArmTwist=60,ArmDirection=60,HandTurn=110;
        private static float Angle(Vector3 a,Vector3 b)=>(float)(Math.Acos(Math.Clamp(Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)),-1,1))*Deg);
        private static float Angle(Quaternion q){q=Quaternion.Normalize(q);return (float)(2*Math.Acos(Math.Min(1,Math.Abs(q.W)))*Deg);}
        private static float TwistAbout(Quaternion q,Vector3 axis){q=Quaternion.Normalize(q);var v=new Vector3(q.X,q.Y,q.Z);float a=2*(float)Math.Atan2(Vector3.Dot(v,Vector3.Normalize(axis)),q.W)*Deg;while(a>180)a-=360;while(a<-180)a+=360;return a;}
        public sealed class MapResult{public string Id;public readonly float[] Worst=new float[Bones.Length],Twist=new float[Bones.Length];public readonly float[] At=new float[Bones.Length];}
        public static MapResult Mapping(NativeIntroEntry e,NativeVisualRig.Rig rig)=>Mapping(e.Id,e.Make(),e.Duration,rig);
        public static MapResult Mapping(string id,NativeEntranceScript s,float duration,NativeVisualRig.Rig rig)
        {
            var pose=new CapturedRig.Pose();var st=new NativeVisualRig.State(rig);var r=new MapResult{Id=id};
            for(int i=0;i<=duration*30;i++)
            {
                float t=s.Clock(i/30f);s.Pose(t,pose,out _,out _);pose.World();float relax=s is NativeIntroV2Script v2s?Math.Clamp(v2s.ArmsRelax(t),0,1):0;
                NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Relax=relax,Exact=Packed(s,t,NativeIntroPack.Arms)},false,false);
                for(int k=0;k<Bones.Length;k++)
                {
                    var b=Bones[k];int v=rig.Of[b.joint];if(v<0)continue;var drawn=Quaternion.Normalize(st.World[v]*Quaternion.Inverse(rig.AlignedWorld[v]));var keyed=pose.WorldRotation[b.joint];
                    float worst,twist=0;
                    if(b.limb)
                    {
                        int vc=rig.Of[b.child];if(vc<0)continue;var kd=pose.WorldPosition[b.child]-pose.WorldPosition[b.joint];var dd=st.Position[vc]-st.Position[v];worst=Angle(kd,dd);
                        // (the twist: what the drawn turn adds about the bone's own length, once the swing between the two directions is taken off)
                        var swing=CapturedRig.FromTo(Vector3.Normalize(kd),Vector3.Normalize(dd));twist=Math.Abs(TwistAbout(Quaternion.Inverse(swing)*drawn*Quaternion.Inverse(keyed),kd));
                    }
                    else worst=Angle(drawn*Quaternion.Inverse(keyed));
                    if(worst>r.Worst[k]){r.Worst[k]=worst;r.At[k]=t;}if(twist>r.Twist[k])r.Twist[k]=twist;
                }
            }
            return r;
        }
        private static float Limit(string bone)=>bone.Contains("arm")?ArmDirection:bone.Contains("hand")?HandTurn:BodyTurn;

        // ---- the anatomical limits (the keyed pose: each joint's local turn in its parent's frame; the rig's rest is identity, a limb
        // points down its -y, the body faces +z, +x is its left) ----
        /// <summary>Elbow: flexion (the forearm forward of the upper arm's line) up to ElbowFlex, never back past ElbowHyper; its side swing and
        /// its twist. Knee: flexion (the shin behind the thigh's line) up to KneeFlex, never forward past KneeHyper. Wrist: its bend off the
        /// forearm and its own twist. Forearm twist: the elbow's twist and the wrist's together. Shoulder: the arm behind and up, across the
        /// body, and its twist. Hip: flexion, extension, abduction, adduction and twist. Spine, chest, neck, head: each one's bend and twist.
        /// Torso: a hand, elbow or knee no deeper than TorsoMargin inside the torso's boxes (degrees and metres).</summary>
        public const float ElbowFlex=155,ElbowHyper=8,ElbowSide=25,KneeFlex=160,KneeHyper=8,KneeSide=20,KneeTwist=45,WristBend=85,WristTwist=50,ForearmTwist=100,
            ShoulderBack=70,ShoulderAcross=55,ShoulderTwist=110,HipFlex=140,HipExtend=40,HipOut=65,HipIn=40,HipTwist=60,SpineBend=50,SpineTwist=50,NeckBend=55,NeckTwist=70,HeadBend=60,HeadTwist=80,
            TorsoMargin=.04f;
        public struct Finding{public string Intro,Joint,What;public float From,To,Worst,WorstAt;public int Last;public string Unit;public override string ToString()=>Intro+" "+From.ToString("0.00")+"-"+To.ToString("0.00")+" s "+Joint+": "+What+" (worst "+Worst.ToString("0")+Unit+" at "+WorstAt.ToString("0.00")+" s)";}
        private static void Swing(Quaternion local,out Vector3 down,out float twist)
        {
            // the limb's direction in its parent's frame, and its twist about its own length (the rest -y)
            down=Vector3.Transform(-Vector3.UnitY,local);var swing=CapturedRig.FromTo(-Vector3.UnitY,down);twist=TwistAbout(Quaternion.Inverse(swing)*local,-Vector3.UnitY);
        }
        public static List<Finding> Limits(NativeIntroEntry e)=>Limits(e.Id,e.Make(),e.Duration,e.Spec);
        /// <summary>The limits of script `s` (named `id`, `duration` s, holding weapon `spec` or none) every 1/30 s. A hand that holds or supports
        /// a weapon is read in that weapon's convention (Core NativeIntroGrip.Natural: a gun's, the bow's and the shield's frame lie a quarter
        /// turn from the hand, so a level aim is a straight wrist); a hand supports while the intro's grip (INativeIntroGrip) is over half on.</summary>
        /// <summary>How much of `mask` the script's pack segments play at `t` (NativeProceduralEntrance.PackWeight; 0 for any other script).</summary>
        public static float Packed(NativeEntranceScript s,float t,int mask)=>s is NativeProceduralEntrance p?p.PackWeight(t,mask):0;
        /// <summary>(`packed`: a joint a pack clip plays in full is the model's own motion, held to the clip on the model by IntroPackChecks
        /// rather than to these keyed limits: the keyed wrist is the weapon's frame, which the binding eases, and the keyed rig's torso is
        /// the voxel body's. False lists them too.)</summary>
        public static List<Finding> Limits(string id,NativeEntranceScript s,float duration,string spec,bool packed=true)
        {
            var pose=new CapturedRig.Pose();var open=new Dictionary<string,Finding>();var done=new List<Finding>();
            int frame=0;var kind=NativeIntroGrip.Of(spec);var natural=new Quaternion[2];
            bool clipLegs=false,clipTorso=false,clipLeft=false,clipRight=false;
            void Hit(float t,string joint,string what,float value,string unit)
            {
                // (a joint a pack clip plays in full: held to the clip on the model instead)
                bool leg=joint.EndsWith("knee",StringComparison.Ordinal)||joint.EndsWith("hip",StringComparison.Ordinal),torsoJoint=joint=="spine"||joint=="chest"||joint=="neck"||joint=="head";
                if(leg?clipLegs:torsoJoint?clipTorso:joint.StartsWith("L ",StringComparison.Ordinal)?clipLeft:joint.StartsWith("R ",StringComparison.Ordinal)&&clipRight)return;
                // (one finding per joint and fault over consecutive frames: its span and its worst)
                string key=joint+"|"+what;
                if(open.TryGetValue(key,out var f)&&frame-f.Last<=1){f.To=t;f.Last=frame;if(value>f.Worst){f.Worst=value;f.WorstAt=t;}open[key]=f;return;}
                if(open.TryGetValue(key,out var old))done.Add(old);
                open[key]=new Finding{Intro=id,Joint=joint,What=what,From=t,To=t,Worst=value,WorstAt=t,Last=frame,Unit=unit};
            }
            var torso=CapturedRig.Boxes.Where(b=>(b.Bone==CapturedRig.Chest||b.Bone==CapturedRig.Spine||b.Bone==CapturedRig.Pelvis)&&b.Size.X>.3f).ToArray();
            for(int i=0;i<=duration*30;i++)
            {
                float t=s.Clock(i/30f);s.Pose(t,pose,out _,out _);pose.World();frame=i;
                float grip=s is INativeIntroGrip g&&g.GripAt(t,out var at)?at.Weight:0;
                for(int side=0;side<2;side++)
                {
                    bool holds=spec!=null&&NativeIntroWeapon.Holds(spec,side==0),supports=kind!=NativeIntroGrip.Kind.None&&grip>.5f&&NativeIntroGrip.SupportLeft(kind)==(side==0);
                    natural[side]=holds?NativeIntroGrip.Natural(kind,false):supports?NativeIntroGrip.Natural(kind,true):Quaternion.Identity;
                }
                clipLegs=packed&&Packed(s,t,NativeIntroPack.Legs)>.99f;clipTorso=packed&&Packed(s,t,NativeIntroPack.Torso)>.99f;
                clipLeft=packed&&Packed(s,t,NativeIntroPack.LeftArm)>.99f;clipRight=packed&&Packed(s,t,NativeIntroPack.RightArm)>.99f;
                for(int side=0;side<2;side++)
                {
                    bool left=side==0;string S=left?"L ":"R ";int arm=left?CapturedRig.LArm:CapturedRig.RArm,hip=left?CapturedRig.LHip:CapturedRig.RHip;float outward=left?1:-1;
                    // elbow
                    Swing(pose.Rotation[arm+1],out var f,out float et);float flex=(float)Math.Atan2(f.Z,-f.Y)*Deg,side_=(float)Math.Asin(Math.Clamp(f.X,-1,1))*Deg;
                    if(flex<-ElbowHyper)Hit(t,S+"elbow","bent backward (hyperextended)",-flex,"°");if(flex>ElbowFlex)Hit(t,S+"elbow","bent past its range",flex,"°");
                    if(Math.Abs(side_)>ElbowSide)Hit(t,S+"elbow","bent sideways",Math.Abs(side_),"°");
                    // wrist
                    Swing(Quaternion.Normalize(pose.Rotation[arm+2]*Quaternion.Inverse(natural[side])),out var h,out float wt);float bend=Angle(-Vector3.UnitY,h);
                    if(bend>WristBend)Hit(t,S+"wrist","bent past its range",bend,"°");if(Math.Abs(wt)>WristTwist)Hit(t,S+"wrist","twisted (turn the forearm instead)",Math.Abs(wt),"°");
                    if(Math.Abs(et+wt)>ForearmTwist)Hit(t,S+"forearm","twisted past about 90°",Math.Abs(et+wt),"°");
                    // shoulder (the arm in the chest's frame, the clavicle included)
                    var inChest=Quaternion.Normalize(Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest])*pose.WorldRotation[arm]);Swing(inChest,out var a,out float st);
                    float back=(float)Math.Atan2(-a.Z,Math.Sqrt(a.X*a.X+a.Y*a.Y))*Deg,across=(float)Math.Asin(Math.Clamp(-a.X*outward,-1,1))*Deg;
                    if(back>ShoulderBack)Hit(t,S+"shoulder","arm reaching behind past its range",back,"°");if(across>ShoulderAcross&&a.Z<.35f)Hit(t,S+"shoulder","arm across the body past its range",across,"°");
                    if(Math.Abs(st)>ShoulderTwist)Hit(t,S+"shoulder","upper arm twisted past its range",Math.Abs(st),"°");
                    // knee
                    Swing(pose.Rotation[hip+1],out var k,out float kt);float kflex=(float)Math.Atan2(-k.Z,-k.Y)*Deg,kside=(float)Math.Asin(Math.Clamp(k.X,-1,1))*Deg;
                    if(kflex<-KneeHyper)Hit(t,S+"knee","bent forward (hyperextended)",-kflex,"°");if(kflex>KneeFlex)Hit(t,S+"knee","bent past its range",kflex,"°");
                    if(Math.Abs(kside)>KneeSide)Hit(t,S+"knee","bent sideways",Math.Abs(kside),"°");if(Math.Abs(kt)>KneeTwist)Hit(t,S+"knee","twisted",Math.Abs(kt),"°");
                    // hip (the thigh in the pelvis's frame)
                    Swing(pose.Rotation[hip],out var th,out float ht);float hflex=(float)Math.Atan2(th.Z,-th.Y)*Deg,hout=(float)Math.Asin(Math.Clamp(th.X*outward,-1,1))*Deg;
                    if(hflex>HipFlex)Hit(t,S+"hip","flexed past its range",hflex,"°");if(hflex<-HipExtend)Hit(t,S+"hip","thigh behind past its range",-hflex,"°");
                    if(hout>HipOut)Hit(t,S+"hip","thigh out past its range",hout,"°");if(hout<-HipIn)Hit(t,S+"hip","thigh across past its range",-hout,"°");
                    if(Math.Abs(ht)>HipTwist)Hit(t,S+"hip","thigh twisted past its range",Math.Abs(ht),"°");
                }
                // the spine, the neck and the head (each on its parent)
                foreach(var (name,j,limitBend,limitTwist) in new[]{("spine",CapturedRig.Spine,SpineBend,SpineTwist),("chest",CapturedRig.Chest,SpineBend,SpineTwist),("neck",CapturedRig.Neck,NeckBend,NeckTwist),("head",CapturedRig.Head,HeadBend,HeadTwist)})
                {
                    var q=pose.Rotation[j];var up=Vector3.Transform(Vector3.UnitY,q);float b=Angle(Vector3.UnitY,up);var sw=CapturedRig.FromTo(Vector3.UnitY,up);float tw=Math.Abs(TwistAbout(Quaternion.Inverse(sw)*q,Vector3.UnitY));
                    if(b>limitBend)Hit(t,name,"bent past its range",b,"°");if(tw>limitTwist)Hit(t,name,"twisted past its range",tw,"°");
                }
                // a hand, an elbow or a knee inside the torso
                foreach(var (name,bone,local) in new[]{("L hand",CapturedRig.LHand,new Vector3(0,-.05f,0)),("R hand",CapturedRig.RHand,new Vector3(0,-.05f,0)),("L elbow",CapturedRig.LElbow,Vector3.Zero),("R elbow",CapturedRig.RElbow,Vector3.Zero),("L knee",CapturedRig.LKnee,Vector3.Zero),("R knee",CapturedRig.RKnee,Vector3.Zero)})
                {
                    var p=pose.WorldPosition[bone]+Vector3.Transform(local,pose.WorldRotation[bone]);float deepest=0;
                    foreach(var box in torso)
                    {
                        var c=pose.WorldPosition[box.Bone]+Vector3.Transform(box.Position,pose.WorldRotation[box.Bone]);var l=Vector3.Transform(p-c,Quaternion.Inverse(pose.WorldRotation[box.Bone]));
                        float dx=box.Size.X*.5f-Math.Abs(l.X),dy=box.Size.Y*.5f-Math.Abs(l.Y),dz=box.Size.Z*.5f-Math.Abs(l.Z);if(dx>0&&dy>0&&dz>0)deepest=Math.Max(deepest,Math.Min(dx,Math.Min(dy,dz)));
                    }
                    if(deepest>TorsoMargin)Hit(t,name,"inside the torso",deepest*100,"cm");
                }
            }
            done.AddRange(open.Values);return done.OrderBy(f=>f.From).ThenBy(f=>f.Joint,StringComparer.Ordinal).ToList();
        }

        // ---- the feet on the model (docs/INTROS-V2.md 9c; I2b's IntroGenericChecks.Feet, for every intro) ----
        /// <summary>A body point under the floor more than this (m) is through it: the ball, the ankle (its sole .05 m under it), the knee (its
        /// cap .055 m), the hip and the pelvis (.09 m of seat under the joint).</summary>
        public const float Through=.04f,SlideMost=.45f;
        private static readonly string[] FeetBones={"ball_l","ball_r","foot_l","foot_r","calf_l","calf_r","thigh_l","thigh_r","pelvis"};
        private static readonly float[] FeetUnder={0,0,.05f,.05f,.055f,.055f,.09f,.09f,.09f};
        public struct FeetResult{public float Slide,SlideAt,Deepest,DeepestAt;public string SlideFoot,DeepBone;}
        /// <summary>
        /// The body as the model draws it (the keyed pose bound bone for bone on the measured skeleton, the feet unplanted, as Runtime
        /// VisualIntroPose binds it) every 1/30 s while it is shown: a foot flat on the floor (its ball within 1.2 cm of it, its ankle within
        /// 13 cm) stays put (its ball under SlideMost; a keyed foot held still while the root moved slid the drawn one back at about 1 m/s)
        /// unless the entrance slides it (NativeProceduralEntrance.FeetSliding); and no ball, ankle, knee, hip or pelvis goes more than Through
        /// into the floor (keyed drops sank the drawn hips 1.7 times as deep: a kneel's knee 30 cm through it).
        /// </summary>
        public static FeetResult Feet(NativeEntranceScript s,float duration,NativeVisualRig.Rig rig)
        {
            var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();float floor=NativeEntranceStage.FloorY;var at=new int[FeetBones.Length];
            for(int b=0;b<FeetBones.Length;b++)at[b]=Array.IndexOf(rig.Names,FeetBones[b]);
            int frames=(int)(duration*30);var p=new Vector3[frames+1,FeetBones.Length];var t=new float[frames+1];var r=new FeetResult();var proc=s as NativeProceduralEntrance;
            for(int i=0;i<=frames;i++)
            {
                t[i]=s.Clock(i/30f);s.Pose(t[i],pose,out var root,out var rot);pose.World();float relax=s is NativeIntroV2Script v2?Math.Clamp(v2.ArmsRelax(t[i]),0,1):0;
                NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Relax=relax,Exact=Packed(s,t[i],NativeIntroPack.Arms)},false,false);
                for(int b=0;b<FeetBones.Length;b++)p[i,b]=at[b]<0?new Vector3(0,1e3f,0):root+Vector3.Transform(st.Position[at[b]],rot);
            }
            for(int i=1;i<frames;i++)
            {
                if(s.Solid(t[i],0)<.5f)continue;
                for(int b=0;b<FeetBones.Length;b++){float d=floor+FeetUnder[b]-p[i,b].Y;if(d>r.Deepest){r.Deepest=d;r.DeepestAt=t[i];r.DeepBone=FeetBones[b];}}
                if(proc!=null&&proc.FeetSliding(t[i]))continue;
                for(int side=0;side<2;side++)
                {
                    var ball=p[i,side];if(ball.Y-floor>.012f||p[i,2+side].Y-floor>.13f)continue;
                    var d=p[i+1,side]-p[i-1,side];d.Y=0;float v=d.Length()*15;if(v>r.Slide){r.Slide=v;r.SlideAt=t[i];r.SlideFoot=side==0?"left":"right";}
                }
            }
            return r;
        }

        // ---- the grip on the model (Core NativeIntroGrip, docs/INTROS-V2.md 9a) ----
        /// <summary>How far the drawn off fist may sit from the drawn support point (m) once the long guns' fist has slid back along its
        /// handguard (NativeVisualRig's support grip: SupportSlide), and how far a fist may slide.</summary>
        public const float GripNear=.03f;
        public struct GripResult{public float Worst,WorstAt,Frames;public float Slide;}
        /// <summary>
        /// Script `s` holding `spec` with its grip on, bound as Runtime VisualBodyBind binds it (Core NativeIntroGrip.Grasp: the intro's
        /// weapon's hold, the support bound, the elbow to the grip's pole) every 1/30 s where the grip is fully on: how far the drawn off
        /// fist's centre is from the drawn support point (the main hand's drawn weapon frame, less the support's slide along its handle).
        /// </summary>
        public static GripResult Grip(NativeEntranceScript s,float duration,string spec,NativeVisualRig.Rig rig)
        {
            var r=new GripResult();var k=NativeIntroGrip.Of(spec);if(k==NativeIntroGrip.Kind.None||!(s is INativeIntroGrip g))return r;
            var st=new NativeVisualRig.State(rig);var pose=new CapturedRig.Pose();bool left=NativeIntroGrip.SupportLeft(k);int side=left?0:1,vh=rig.Of[left?CapturedRig.LHand:CapturedRig.RHand];
            for(int i=0;i<=duration*30;i++)
            {
                float t=s.Clock(i/30f);s.Pose(t,pose,out _,out _);pose.World();if(!g.GripAt(t,out var at)||at.Weight<.999f){st=new NativeVisualRig.State(rig);continue;}
                var grasp=new NativeVisualRig.Grasp{Exact=Packed(s,t,NativeIntroPack.Arms)};NativeIntroGrip.Grasp(spec,at,pose,ref grasp);NativeVisualRig.Pose(st,pose,grasp,false,false);
                NativeIntroGrip.Model(k,spec,at.Along,at.Pull,pose,out var hold);var weapon=pose.WorldRotation[left?CapturedRig.RHand:CapturedRig.LHand];
                var want=(left?st.RightAnchor:st.LeftAnchor)+Vector3.Transform(hold.Center-hold.Axis*st.SupportSlide,weapon);
                var turn=Quaternion.Normalize(st.World[vh]*Quaternion.Inverse(rig.AlignedWorld[vh]));var fist=st.Position[vh]+Vector3.Transform(rig.FistCenter[side],turn);
                float d=Vector3.Distance(fist,want);r.Frames++;if(d>r.Worst){r.Worst=d;r.WorstAt=t;}r.Slide=Math.Max(r.Slide,st.SupportSlide);
            }
            return r;
        }

        /// <summary>
        /// Every registered intro and every canonical pose of the guide (IntroPoseGallery): the bone-for-bone mapping (checked); the limits,
        /// the feet on the model and the grip on the model (the gallery's checked; the intros' reported, failed only when `strict`).
        /// </summary>
        public static void Run(bool strict,bool printAll)
        {
            // (the local intro motion bank, where there is one, so an intro's pack segments play here as in the game: IntroPackChecks)
            bool pack=IntroPackChecks.LoadLocal(IntroPackChecks.Root(),out var packPath);if(printAll)Console.WriteLine(pack?"PACK bank "+packPath:"PACK no local intro motion bank");
            count=0;var rig=ModelPoseChecks.Measured();int findings=0,intros=0;var worstMap=new float[Bones.Length];var failed=new List<string>();float worstSlide=0,worstDeep=0;
            // (`armed`: a guide pose whose arms hold a weapon: they are solved onto it, by the hold and the grip, on the model as on the keyed rig,
            // so only the body, the neck, the head and the legs are held to the keyed turns there)
            void Map(string id,MapResult m,bool armed=false)
            {
                if(printAll)Console.WriteLine("MAP "+id.PadRight(26)+string.Join(" ",Bones.Select((b,k)=>b.name.Replace(" ","")+" "+m.Worst[k].ToString("0")+(b.limb?"/t"+m.Twist[k].ToString("0"):"")+"@"+m.At[k].ToString("0.0"))));
                for(int k=0;k<Bones.Length;k++)
                {
                    var b=Bones[k];if(armed&&(b.name.Contains("arm")||b.name.Contains("hand")))continue;worstMap[k]=Math.Max(worstMap[k],m.Worst[k]);
                    Check(m.Worst[k]<=Limit(b.name),id+": the drawn "+b.name+" turns as keyed (worst "+m.Worst[k].ToString("0.0")+" deg at "+m.At[k].ToString("0.00")+" s; tolerance "+Limit(b.name)+")");
                    if(b.limb)Check(m.Twist[k]<=(b.name.Contains("arm")?ArmTwist:BodyTurn),id+": the drawn "+b.name+" keeps its keyed twist (worst "+m.Twist[k].ToString("0.0")+" deg)");
                }
            }
            // (each finding as text: the limits', the feet's and the grip's)
            List<string> Findings(string id,NativeEntranceScript s,float duration,string spec)
            {
                var all=Limits(id,s,duration,spec).Select(f=>f.ToString()).ToList();var feet=Feet(s,duration,rig);worstSlide=Math.Max(worstSlide,feet.Slide);worstDeep=Math.Max(worstDeep,feet.Deepest);
                if(feet.Slide>SlideMost)all.Add(id+" "+feet.SlideAt.ToString("0.00")+" s the drawn "+feet.SlideFoot+" foot slides on the floor at "+feet.Slide.ToString("0.00")+" m/s (keyed for the model: ModelFeet, docs/INTROS-V2.md 9c; or FeetSliding where it is meant)");
                if(feet.Deepest>Through)all.Add(id+" "+feet.DeepestAt.ToString("0.00")+" s the drawn "+feet.DeepBone+" is "+(feet.Deepest*100).ToString("0")+" cm into the floor (drops in the model's metres: ModelFeet)");
                var grip=Grip(s,duration,spec,rig);if(grip.Worst>GripNear)all.Add(id+" "+grip.WorstAt.ToString("0.00")+" s the drawn off fist is "+(grip.Worst*100).ToString("0")+" cm off the drawn weapon's support point");
                return all;
            }
            foreach(var e in NativeIntroCatalog.Entries)
            {
                Map(e.Id,Mapping(e,rig));intros++;
                var l=Findings(e.Id,e.Make(),e.Duration,e.Spec);findings+=l.Count;
                if(printAll){Console.WriteLine("LIMITS "+e.Id+": "+(l.Count==0?"none":l.Count+" finding"+(l.Count==1?"":"s")));foreach(var f in l)Console.WriteLine("  "+f);}
                if(l.Count>0&&failed.Count<40)failed.Add(e.Id+" ("+l.Count+")");
            }
            // The guide's canonical poses: inside the limits, on the floor, still where planted and holding their weapons, always.
            int poses=0;
            foreach(var name in IntroPoseGallery.Names)
            {
                var s=IntroPoseGallery.Make(name);Map("pose "+name,Mapping("pose "+name,s,s.Seconds,rig),s.Spec!=null);
                var l=Findings("pose "+name,IntroPoseGallery.Make(name),s.Seconds,s.Spec);if(printAll){Console.WriteLine("POSE "+name+": "+(l.Count==0?"clean":string.Join("; ",l)));}
                Check(l.Count==0,"the guide's "+name+" (Tests/IntroPoseGallery): "+string.Join("; ",l));
                if(s.Spec!=null&&NativeIntroGrip.Of(s.Spec)!=NativeIntroGrip.Kind.None){var g=Grip(IntroPoseGallery.Make(name),s.Seconds,s.Spec,rig);Check(g.Frames>0,"the guide's "+name+" holds its grip");}
                poses++;
            }
            if(strict)Check(findings==0,findings+" finding(s) in "+failed.Count+" intro(s): "+string.Join(", ",failed)+" (listed above)");
            Console.WriteLine("PASS intro poses: "+count+" checks ("+intros+" intros and "+poses+" guide poses bone for bone on the model: worst "+string.Join(", ",Bones.Select((b,k)=>b.name+" "+worstMap[k].ToString("0")))+" deg; drawn feet: slide at most "+worstSlide.ToString("0.00")+" m/s, at most "+(worstDeep*100).ToString("0")+" cm into the floor); limits, feet and grips: "+findings+" finding(s)"+(strict?"":" reported (--intro-limits lists and fails them)"));
        }
    }
}
