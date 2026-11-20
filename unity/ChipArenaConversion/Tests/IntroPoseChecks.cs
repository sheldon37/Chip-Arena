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
        public static MapResult Mapping(NativeIntroEntry e,NativeVisualRig.Rig rig)
        {
            var s=e.Make();var pose=new CapturedRig.Pose();var st=new NativeVisualRig.State(rig);var r=new MapResult{Id=e.Id};
            for(int i=0;i<=e.Duration*30;i++)
            {
                float t=s.Clock(i/30f);s.Pose(t,pose,out _,out _);pose.World();float relax=s is NativeIntroV2Script v2s?Math.Clamp(v2s.ArmsRelax(t),0,1):0;
                NativeVisualRig.Pose(st,pose,new NativeVisualRig.Grasp{Relax=relax},false,false);
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
        public static List<Finding> Limits(NativeIntroEntry e)
        {
            var s=e.Make();var pose=new CapturedRig.Pose();var open=new Dictionary<string,Finding>();var done=new List<Finding>();
            int frame=0;
            void Hit(float t,string joint,string what,float value,string unit)
            {
                // (one finding per joint and fault over consecutive frames: its span and its worst)
                string key=joint+"|"+what;
                if(open.TryGetValue(key,out var f)&&frame-f.Last<=1){f.To=t;f.Last=frame;if(value>f.Worst){f.Worst=value;f.WorstAt=t;}open[key]=f;return;}
                if(open.TryGetValue(key,out var old))done.Add(old);
                open[key]=new Finding{Intro=e.Id,Joint=joint,What=what,From=t,To=t,Worst=value,WorstAt=t,Last=frame,Unit=unit};
            }
            var torso=CapturedRig.Boxes.Where(b=>(b.Bone==CapturedRig.Chest||b.Bone==CapturedRig.Spine||b.Bone==CapturedRig.Pelvis)&&b.Size.X>.3f).ToArray();
            for(int i=0;i<=e.Duration*30;i++)
            {
                float t=s.Clock(i/30f);s.Pose(t,pose,out _,out _);pose.World();frame=i;
                for(int side=0;side<2;side++)
                {
                    bool left=side==0;string S=left?"L ":"R ";int arm=left?CapturedRig.LArm:CapturedRig.RArm,hip=left?CapturedRig.LHip:CapturedRig.RHip;float outward=left?1:-1;
                    // elbow
                    Swing(pose.Rotation[arm+1],out var f,out float et);float flex=(float)Math.Atan2(f.Z,-f.Y)*Deg,side_=(float)Math.Asin(Math.Clamp(f.X,-1,1))*Deg;
                    if(flex<-ElbowHyper)Hit(t,S+"elbow","bent backward (hyperextended)",-flex,"°");if(flex>ElbowFlex)Hit(t,S+"elbow","bent past its range",flex,"°");
                    if(Math.Abs(side_)>ElbowSide)Hit(t,S+"elbow","bent sideways",Math.Abs(side_),"°");
                    // wrist
                    Swing(pose.Rotation[arm+2],out var h,out float wt);float bend=Angle(-Vector3.UnitY,h);
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

        /// <summary>Every registered intro: the bone-for-bone mapping (checked), the limits (reported; failed only when `strict`).</summary>
        public static void Run(bool strict,bool printAll)
        {
            count=0;var rig=ModelPoseChecks.Measured();int findings=0,intros=0;var worstMap=new float[Bones.Length];
            foreach(var e in NativeIntroCatalog.Entries)
            {
                var m=Mapping(e,rig);intros++;
                if(printAll)Console.WriteLine("MAP "+e.Id.PadRight(26)+string.Join(" ",Bones.Select((b,k)=>b.name.Replace(" ","")+" "+m.Worst[k].ToString("0")+(b.limb?"/t"+m.Twist[k].ToString("0"):"")+"@"+m.At[k].ToString("0.0"))));
                for(int k=0;k<Bones.Length;k++)
                {
                    var b=Bones[k];worstMap[k]=Math.Max(worstMap[k],m.Worst[k]);
                    Check(m.Worst[k]<=Limit(b.name),e.Id+": the drawn "+b.name+" turns as keyed (worst "+m.Worst[k].ToString("0.0")+" deg at "+m.At[k].ToString("0.00")+" s; tolerance "+Limit(b.name)+")");
                    if(b.limb)Check(m.Twist[k]<=(b.name.Contains("arm")?ArmTwist:BodyTurn),e.Id+": the drawn "+b.name+" keeps its keyed twist (worst "+m.Twist[k].ToString("0.0")+" deg)");
                }
                var l=Limits(e);findings+=l.Count;
                if(printAll){Console.WriteLine("LIMITS "+e.Id+": "+(l.Count==0?"none":l.Count+" finding"+(l.Count==1?"":"s")));foreach(var f in l)Console.WriteLine("  "+f);}
                if(strict)Check(l.Count==0,e.Id+": "+l.Count+" anatomical limit finding(s), the first: "+(l.Count>0?l[0].ToString():""));
            }
            Console.WriteLine("PASS intro poses: "+count+" checks ("+intros+" intros bone for bone on the model: worst "+string.Join(", ",Bones.Select((b,k)=>b.name+" "+worstMap[k].ToString("0")))+" deg); anatomical limits: "+findings+" finding(s)"+(strict?"":" reported (--intro-limits lists and fails them)"));
        }
    }
}
