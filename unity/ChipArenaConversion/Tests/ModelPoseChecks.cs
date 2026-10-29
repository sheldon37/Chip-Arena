#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The model's arms in the idles, lobby card poses and intros (owner 2026-10-28: "Fix idle animations, lobby card poses, and
    // intros to fully support the new player model. Since the transfer, a lot of unnatural arm positioning and movements";
    // Core/NativeVisualRig.cs Relax, OnLap). Every lobby pose of every pilot (Standing with weapon, Sitting on chair, Sleeping, Arms
    // crossed, the Sheldon Idle) as the portraits pose it, every entrance (the twenty-one directed ones and the DEV fighters'
    // clip intros) as the intro poses it, and the Cyber Lord's idle in play, through the adapter on a skeleton with the first
    // outfit's measured rest joints (docs/BOZO-INTEGRATION.md step 2: no pack data) and on the visual rig checks' synthetic
    // one. Checked: hanging free arms near the body (abduction, a soft elbow, hands 0-8 cm off the thighs, palms in); seated
    // hands on the lap, palms down, left and right alike; folded arms folded (elbows bent, forearms on the chest); no relaxed arm
    // spread wider than the game's own (or the model's hang); elbows within a human's bend and hinged; fists and forearms out
    // of the torso; the daggers' card fists out of the hips; frame to frame as smooth as the visual rig's transitions; repeats
    // bit for bit and allocates nothing; Relax off leaves the arms as before. The wiring: which callers relax which arms, and
    // Classic's voxel paths untouched (the voxel poses themselves are LobbyPoseChecks' and the entrance checks').
    internal static class ModelPoseChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Model poses: "+what);count++;}
        private const float Deg=57.29578f;

        // ---- the skeleton: the first outfit's rest joints (step 2), A-pose arms 53 degrees down, palms down, frames off identity ----
        private static readonly List<string> names=new List<string>(),parents=new List<string>();
        private static readonly Dictionary<string,Vector3> at=new Dictionary<string,Vector3>();
        private static void Bone(string name,string parent,Vector3 world){names.Add(name);parents.Add(parent);at[name]=world;}
        private static Vector3 P(float x,float h,float z=0)=>new Vector3(x,h-.865f,z);
        public static NativeVisualRig.Rig Measured()
        {
            names.Clear();parents.Clear();at.Clear();
            Bone("root","",P(0,0));Bone("pelvis","root",P(0,.987f));Bone("spine_01","pelvis",P(0,1.01f,.01f));Bone("spine_02","spine_01",P(0,1.075f,.02f));
            Bone("spine_03","spine_02",P(0,1.16f,.025f));Bone("spine_04","spine_03",P(0,1.26f,.02f));Bone("spine_05","spine_04",P(0,1.395f));
            Bone("neck_01","spine_05",P(0,1.491f,-.01f));Bone("neck_02","neck_01",P(0,1.53f));Bone("head","neck_02",P(0,1.573f,.01f));
            foreach(bool left in new[]{true,false})
            {
                float x=left?1:-1;string s=left?"_l":"_r";
                Vector3 S=P(x*.133f,1.425f,-.01f),E=P(x*.291f,1.215f,-.035f),W=P(x*.427f,1.06f,.053f),u=Vector3.Normalize(E-S),f=Vector3.Normalize(W-E);
                Bone("clavicle"+s,"spine_05",P(x*.025f,1.425f,.02f));Bone("upperarm"+s,"clavicle"+s,S);
                Bone("shoulder_twist"+s,"upperarm"+s,S);Bone("upperarm_twist_01"+s,"upperarm"+s,S+u*.09f);Bone("upperarm_twist_02"+s,"upperarm"+s,S+u*.18f);
                Bone("lowerarm"+s,"upperarm"+s,E);Bone("lowerarm_twist_01"+s,"lowerarm"+s,E+f*.08f);Bone("lowerarm_twist_02"+s,"lowerarm"+s,E+f*.16f);Bone("hand"+s,"lowerarm"+s,W);
                Vector3 palm=new Vector3(-x*.8f,-.6f,0);palm-=f*Vector3.Dot(palm,f);palm=Vector3.Normalize(palm);Vector3 t=Vector3.Normalize(Vector3.Cross(f,palm));if(t.Z<0)t=-t;
                foreach(var (n,along,side) in new[]{("index",.095f,.025f),("middle",.10f,.005f),("ring",.095f,-.015f),("pinky",.085f,-.032f)})
                {
                    Vector3 f1=W+f*along+t*side,f2=f1+Vector3.Normalize(f+palm*.09f)*.038f,f3=f2+Vector3.Normalize(f+palm*.18f)*.026f;
                    Bone(n+"_01"+s,"hand"+s,f1);Bone(n+"_02"+s,n+"_01"+s,f2);Bone(n+"_03"+s,n+"_02"+s,f3);
                }
                Vector3 t1=W+f*.025f+t*.025f+palm*.015f,t2=t1+Vector3.Normalize(f*.6f+t*.5f+palm*.3f)*.035f;
                Bone("thumb_01"+s,"hand"+s,t1);Bone("thumb_02"+s,"thumb_01"+s,t2);Bone("thumb_03"+s,"thumb_02"+s,t2+Vector3.Normalize(f*.6f+t*.3f+palm*.45f)*.03f);
            }
            foreach(bool left in new[]{true,false})
            {
                float x=left?1:-1;string s=left?"_l":"_r";
                Bone("thigh"+s,"pelvis",P(x*.093f,.961f,.02f));Bone("thigh_twist_01"+s,"thigh"+s,P(x*.095f,.75f,.025f));Bone("calf"+s,"thigh"+s,P(x*.10f,.536f,.03f));
                Bone("calf_twist_01"+s,"calf"+s,P(x*.105f,.32f,.015f));Bone("foot"+s,"calf"+s,P(x*.11f,.110f,-.01f));Bone("ball"+s,"foot"+s,P(x*.11f,.013f,.116f));
            }
            int count=names.Count;var world=new Vector3[count];var parent=new int[count];var rot=new Quaternion[count];
            for(int i=0;i<count;i++){world[i]=at[names[i]];parent[i]=names.IndexOf(parents[i]);}
            string Chain(string n)=>n.StartsWith("upperarm_")&&!n.Contains("twist")?"lowerarm"+n.Substring(8):n.StartsWith("lowerarm_")&&!n.Contains("twist")?"hand"+n.Substring(8):n.StartsWith("hand_")?"middle_01"+n.Substring(4)
                :n.StartsWith("thigh_")&&!n.Contains("twist")?"calf"+n.Substring(5):n.StartsWith("calf_")&&!n.Contains("twist")?"foot"+n.Substring(4):null;
            for(int i=0;i<count;i++)
            {
                string c=Chain(names[i]);int child=c!=null?names.IndexOf(c):parents.IndexOf(names[i]);Vector3 dir=child<0?Vector3.UnitY:world[child]-world[i];if(dir.LengthSquared()<1e-8f)dir=Vector3.UnitY;
                rot[i]=CapturedRig.Rot(Vector3.Normalize(dir),.37f*i)*CapturedRig.FromTo(CapturedRig.X,Vector3.Normalize(dir));
            }
            var lp=new Vector3[count];var lr=new Quaternion[count];
            for(int i=0;i<count;i++){int p=parent[i];lr[i]=p<0?rot[i]:Quaternion.Inverse(rot[p])*rot[i];lp[i]=p<0?world[i]:Vector3.Transform(world[i]-world[p],Quaternion.Inverse(rot[p]));}
            return new NativeVisualRig.Rig(NativeVisualRig.Mannequin(names.ToArray(),parent,lp,lr));
        }

        // ---- the callers' grasps (Runtime VisualHold, VisualArmsOf, VisualArmedAs) ----
        private enum Arms{Live,Still,Unarmed}
        private static NativeVisualRig.Grasp Grasp(string spec,bool away,CapturedRig.Pose pose,Arms arms)
        {
            var g=new NativeVisualRig.Grasp();
            WeaponId w=spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;int set=spec=="daggers"?1:spec=="greatsword"?2:spec=="shield"?3:0;
            if(!away)
            {
                if(GunRules.Ranged(w)){g.RightGrip=1;g.Support=1;g.Right=NativeVisualRig.PistolGrip(w);g.SupportHold=NativeVisualRig.GunSupport(w,pose);g.Stock=NativeVisualRig.Stock(w);}
                else if(MeleeMotion.Handles(spec)&&!NativeLobbyPoses.Magic(spec)&&!NativeBowWire.Is(spec))
                {
                    string ms=set==0?"sword":set==1?"daggers":set==2?"greatsword":"shield";g.RightGrip=1;g.Right=NativeVisualRig.Melee(ms,Quaternion.Identity);
                    if(set==1){g.LeftGrip=1;g.Left=NativeVisualRig.Melee(ms,Quaternion.Identity);}
                    if(set==2){g.Support=1;g.SupportHold=NativeVisualRig.GreatswordSupport(Quaternion.Identity);}
                }
                else g.RightGrip=NativeWeaponDressing.SocketShown(spec)?1:0;
            }
            if(arms==Arms.Unarmed){bool fists=NativeLobbyPoses.Fighter(spec);g=new NativeVisualRig.Grasp{RightGrip=fists?g.RightGrip:0,LeftGrip=fists?g.LeftGrip:0};}
            else g.LeftCarries=!away&&NativeLobbyPoses.HeldBone(spec)==CapturedRig.LHand;
            g.Relax=1;g.Still=arms==Arms.Still?1:0;return g;
        }

        // ---- the clips: the fighters' idles and the clip intros ----
        private sealed class Clip{public float[] Values,Roots;public int Frames;public float Duration;public bool Loop;}
        private static Dictionary<string,Clip> Clips(string root)
        {
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));
            using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
            using var doc=JsonDocument.Parse(reader.ReadToEnd());var map=new Dictionary<string,Clip>();
            foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
            {
                string key=c.GetProperty("key").GetString();if(!(key.StartsWith("intro-")||key=="emperor-idle"||key=="ansatsuken-idle"))continue;
                var clip=new Clip{Values=c.GetProperty("values").EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray(),Frames=c.GetProperty("frames").GetInt32(),Duration=(float)c.GetProperty("duration").GetDouble(),Loop=c.TryGetProperty("loop",out var l)&&l.GetBoolean()};
                if(c.TryGetProperty("roots",out var r)&&r.ValueKind==JsonValueKind.Array)clip.Roots=r.EnumerateArray().Select(v=>(float)v.GetDouble()).ToArray();
                map[key]=clip;
            }
            return map;
        }
        private static void Sample(Clip clip,float age,CapturedRig.Pose pose)
        {
            float fraction=clip.Loop?age%clip.Duration/clip.Duration:Math.Clamp(age/clip.Duration,0,1);NativeFighterPose.Frames(clip.Frames,fraction,false,out int a,out int b,out float t);var v=clip.Values;
            for(int bone=0;bone<22;bone++){int i=(a*22+bone)*7,j=(b*22+bone)*7;pose.Position[bone]=Vector3.Lerp(new Vector3(v[i],v[i+1],v[i+2]),new Vector3(v[j],v[j+1],v[j+2]),t);pose.Rotation[bone]=Quaternion.Slerp(new Quaternion(v[i+3],v[i+4],v[i+5],v[i+6]),new Quaternion(v[j+3],v[j+4],v[j+5],v[j+6]),t);}
        }
        private struct Frame{public float Time;public CapturedRig.Pose Pose;public NativeVisualRig.Grasp Grasp;public bool Fresh;}
        private static readonly string[] Specs={"pistol","rifle","machinegun","sword","daggers","greatsword","shield","fire","water","lightning","darkness","leet","bow","emperor","ansatsuken",CyberLordSpec.Id};
        private static IEnumerable<Frame> Lobby(Dictionary<string,Clip> clips,string spec,int poseIndex,float seconds,float dt)
        {
            var r=new MeleeRig();var p=new CapturedRig.Pose();bool fresh=true;
            for(float t=0;t<=seconds+1e-4f;t+=dt)
            {
                if(NativeLobbyPoses.UsesIdle(spec,poseIndex)){p.Reset();if(clips.TryGetValue(spec+"-idle",out var idle)){Sample(idle,t,p);r.ReadPose(p);}else r.Reset();}
                NativeLobbyPoses.Sample(r,spec,poseIndex,t,"none",false,7,out var weapon,out _,out _);r.WritePose(p);p.World();
                yield return new Frame{Time=t,Pose=p,Grasp=Grasp(spec,!weapon.Visible,p,Arms.Still),Fresh=fresh};fresh=false;
            }
        }
        private static IEnumerable<Frame> Intro(Dictionary<string,Clip> clips,string style,string spec)
        {
            var p=new CapturedRig.Pose();bool fresh=true;var arms=NativeIntroRules.IntroWeapon(style)!=null||style==NativeBowEntrance.Id?Arms.Live:Arms.Unarmed;
            var sc=NativeEntranceDirector.Script(style);
            if(sc!=null)
            {
                // The director keeps one script a style for the whole run: a clip bound here is unbound again after, so later
                // checks (EntranceDirectorChecks: a weapon signature waits for its clip) find the scripts as they were.
                bool bound=false;
                if(sc is NativeWeaponEntrance&&!sc.Ready){var c=clips["intro-"+style];sc.Bind(new NativeEntranceClip(c.Values,c.Roots,c.Frames,c.Duration));bound=true;}
                try
                {
                    sc.Prepare();
                    for(float age=0;age<=4.8f;age+=1/60f)
                    {
                        float t=sc.Clock(age);sc.Pose(t,p,out _,out _);p.World();if(sc.Solid(t,CapturedRig.Chest)<.5f){fresh=true;continue;}
                        yield return new Frame{Time=age,Pose=p,Grasp=Grasp(spec,false,p,arms),Fresh=fresh};fresh=false;
                    }
                }
                finally{if(bound)sc.Bind(null);}
                yield break;
            }
            var clip=clips["intro-"+style];
            for(float age=0;age<=clip.Duration;age+=1/60f){NativeIntroMotion.Pose(style,clip.Values,clip.Frames,clip.Duration,age,p);p.World();yield return new Frame{Time=age,Pose=p,Grasp=Grasp(spec,false,p,arms),Fresh=fresh};fresh=false;}
        }
        private static IEnumerable<Frame> CyberIdle(float seconds)
        {
            var r=new MeleeRig();var p=new CapturedRig.Pose();bool fresh=true;
            for(float t=0;t<=seconds;t+=1/30f){CyberLordIdlePose.Stand(r,t,3,false);r.WritePose(p);p.World();yield return new Frame{Time=t,Pose=p,Grasp=Grasp(CyberLordSpec.Id,false,p,Arms.Live),Fresh=fresh};fresh=false;}
        }

        // ---- measures ----
        private static Quaternion Eff(NativeVisualRig.State s,int v)=>Quaternion.Normalize(s.World[v]*Quaternion.Inverse(s.Rig.AlignedWorld[v]));
        // The torso the arms are measured against (the first outfit, step 2: hips .35 m, waist .24, chest .30 wide; the hooded top
        // .22 deep), by rest height along the pelvis-to-neck line.
        private static readonly (float h,float a,float b)[] Torso={(.90f,.175f,.10f),(.99f,.165f,.10f),(1.08f,.122f,.088f),(1.20f,.14f,.10f),(1.30f,.15f,.11f),(1.40f,.14f,.105f)};
        private static float TorsoGap(NativeVisualRig.State s,Vector3 p,float radius,float top=1.40f)
        {
            var r=s.Rig;int pv=r.Of[CapturedRig.Pelvis],nk=r.Of[CapturedRig.Neck],ch=r.Of[CapturedRig.Chest];Vector3 low=s.Position[pv],line=s.Position[nk]-low;
            float t=Vector3.Dot(p-low,line)/line.LengthSquared(),h=.987f+t*(1.53f-.987f);if(h<.88f||h>top)return float.NaN;
            Quaternion q=t<.35f?Eff(s,pv):Eff(s,ch);Vector3 up=Vector3.Normalize(line),x=Vector3.Transform(CapturedRig.X,q),z=Vector3.Normalize(Vector3.Cross(x,up));x=Vector3.Cross(up,z);
            if(Vector3.Dot(z,Vector3.Transform(CapturedRig.Z,q))<0){z=-z;x=-x;}
            float a=Torso[^1].a,b=Torso[^1].b;
            for(int i=0;i<Torso.Length;i++){if(h<=Torso[i].h){if(i==0){a=Torso[0].a;b=Torso[0].b;}else{float k=(h-Torso[i-1].h)/(Torso[i].h-Torso[i-1].h);a=Torso[i-1].a+(Torso[i].a-Torso[i-1].a)*k;b=Torso[i-1].b+(Torso[i].b-Torso[i-1].b)*k;}break;}}
            Vector3 d=p-low-line*t;float rx=Vector3.Dot(d,x),rz=Vector3.Dot(d,z),len=(float)Math.Sqrt(rx*rx+rz*rz);if(len<1e-5f)return -Math.Min(a,b)-radius;
            return len-1/(float)Math.Sqrt(rx*rx/(len*len*a*a)+rz*rz/(len*len*b*b))-radius;
        }
        private static float Segment(Vector3 p,Vector3 a,Vector3 b,out float t){var ab=b-a;t=Math.Clamp(Vector3.Dot(p-a,ab)/Math.Max(1e-9f,ab.LengthSquared()),0,1);return Vector3.Distance(p,a+ab*t);}
        private struct Arm{public float Abd,Elbow,PalmIn,PalmUp,ThighGap,TorsoGap,Forearm,Upper,Fold;public Vector3 Fist,ElbowAt;}
        private static Arm Measure(NativeVisualRig.State s,bool left)
        {
            var r=s.Rig;int side=left?0:1,ga=left?CapturedRig.LArm:CapturedRig.RArm,va=r.Of[ga],ve=r.Of[ga+1],vh=r.Of[ga+2],pv=r.Of[CapturedRig.Pelvis],nk=r.Of[CapturedRig.Neck],ch=r.Of[CapturedRig.Chest];
            Vector3 S=s.Position[va],E=s.Position[ve],W=s.Position[vh],up=Vector3.Normalize(s.Position[nk]-s.Position[pv]);
            Vector3 X=Vector3.Transform(CapturedRig.X,Eff(s,ch));X=Vector3.Normalize(X-up*Vector3.Dot(X,up));Vector3 L=left?X:-X;
            Vector3 u=Vector3.Normalize(E-S),f=Vector3.Normalize(W-E);var m=new Arm();
            m.Abd=(float)Math.Asin(Math.Clamp(Vector3.Dot(u,L),-1,1))*Deg;m.Elbow=(float)Math.Acos(Math.Clamp(Vector3.Dot(u,f),-1,1))*Deg;
            Vector3 palm=Vector3.Transform(r.FistPalm[side],Eff(s,vh));m.PalmIn=Vector3.Dot(palm,-L);m.PalmUp=Vector3.Dot(palm,up);
            m.Fist=W+Vector3.Transform(r.FistCenter[side],Eff(s,vh));m.ElbowAt=E;
            int th=r.Of[left?CapturedRig.LHip:CapturedRig.RHip],kn=r.Of[left?CapturedRig.LKnee:CapturedRig.RKnee];
            float d=Segment(m.Fist,s.Position[th],s.Position[kn],out float tt);m.ThighGap=d-(.085f-.025f*tt)-.04f;
            float g=TorsoGap(s,m.Fist,.04f);m.TorsoGap=float.IsNaN(g)?9:g;
            // The deepest point of the forearm and fist, and of the upper arm's lower half, inside the torso.
            m.Forearm=0;m.Upper=0;
            for(int i=0;i<=8;i++){float k=i/8f,depth=-TorsoGap(s,Vector3.Lerp(E,m.Fist,k),.04f-.005f*k);if(depth>m.Forearm)m.Forearm=depth;}
            for(int i=0;i<=6;i++){float k=.5f+.5f*i/6f,depth=-TorsoGap(s,Vector3.Lerp(S,E,k),.045f,1.31f);if(depth>m.Upper)m.Upper=depth;}
            // The hinge: the upper arm's fold side (+z of its aligned frame) faces the forearm.
            Vector3 fold=Vector3.Transform(CapturedRig.Z,Eff(s,va)),bend=f-u*Vector3.Dot(f,u);m.Fold=bend.LengthSquared()>1e-4f?Vector3.Dot(fold,Vector3.Normalize(bend)):1;
            return m;
        }
        // The game arm's own abduction from its chest, degrees.
        private static float GameAbd(CapturedRig.Pose pose,bool left)
        {
            int ga=left?CapturedRig.LArm:CapturedRig.RArm;Vector3 u=Vector3.Normalize(pose.WorldPosition[ga+1]-pose.WorldPosition[ga]);
            return (float)Math.Asin(Math.Clamp(Vector3.Dot(u,Vector3.Transform(left?CapturedRig.X:-CapturedRig.X,pose.WorldRotation[CapturedRig.Chest])),-1,1))*Deg;
        }
        // How far the game hand stands clear of the voxel body's side or front (NativeVisualRig.Relax's measure).
        private static float Clear(CapturedRig.Pose pose,bool left)
        {
            int ga=left?CapturedRig.LArm:CapturedRig.RArm;var mid=(pose.WorldPosition[CapturedRig.LArm]+pose.WorldPosition[CapturedRig.RArm])*.5f;
            var g=Vector3.Transform(pose.WorldPosition[ga+2]-mid,Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest]));float drop=-g.Y;
            return Math.Max(0,Math.Max(Math.Abs(g.X)-NativeVisualRig.GameHalfWidth(drop),Math.Abs(g.Z)-NativeVisualRig.GameHalfDepth(drop)));
        }
        private static float Drop(CapturedRig.Pose pose,bool left)
        {
            int ga=left?CapturedRig.LArm:CapturedRig.RArm;var mid=(pose.WorldPosition[CapturedRig.LArm]+pose.WorldPosition[CapturedRig.RArm])*.5f;
            return -Vector3.Transform(pose.WorldPosition[ga+2]-mid,Quaternion.Inverse(pose.WorldRotation[CapturedRig.Chest])).Y;
        }

        private sealed class Stats
        {
            public int Poses,Relaxed,Hanging,Narrower,DeepForearm,DeepForearmBefore;public float HangBeforeAbd=-99,HangBeforeGap=-99,HangAbdMin=99,HangAbdMax=-99,HangGapMin=99,HangGapMax=-99,HangElbowMax,WorstSpread=-99,WorstForearm,WorstUpper,WorstStep=-99,WorstElbowStep=-99,ElbowMax;
            public string SpreadAt="",ForearmAt="",UpperAt="",StepAt="";
        }
        // Poses a sequence and checks every relaxed arm in it. `hang`: hanging free arms in a still standing pose are checked near
        // the body.
        // Poses a sequence twice, relaxed and as before (Relax off: the copied arm), and checks every relaxed arm in it: hinged; a
        // free arm by the body no wider than the game's own or the model's hang (or than the copied arm); forearm, fist and upper
        // arm out of the torso (or no deeper than the copied arm); in a still standing pose a hanging free arm near the body; frame
        // to frame as smooth as the visual rig's transitions (or as the copied arm).
        private static void Run(NativeVisualRig.Rig rig,string name,IEnumerable<Frame> frames,Stats st,bool still,bool palms)
        {
            var s=new NativeVisualRig.State(rig);var o=new NativeVisualRig.State(rig);var lastFist=new Vector3[2];var lastElbow=new Vector3[2];var lastGame=new Vector3[2];var oldFist=new Vector3[2];var oldElbow=new Vector3[2];bool has=false;
            foreach(var fr in frames)
            {
                if(fr.Fresh){s.Forget();o.Forget();has=false;}
                NativeVisualRig.Pose(s,fr.Pose,fr.Grasp,false,false);var copied=fr.Grasp;copied.Relax=0;copied.Still=0;NativeVisualRig.Pose(o,fr.Pose,copied,false,false);st.Poses++;
                for(int side=0;side<2;side++)
                {
                    bool left=side==0;var m=Measure(s,left);var mo=Measure(o,left);float relax=left?s.LeftRelax:s.RightRelax;string at=name+" "+(left?"left":"right")+" at "+fr.Time.ToString("0.00")+" s";
                    Check(m.Elbow<=150.5f&&float.IsFinite(m.Abd),at+": the elbow bends past a human's ("+m.Elbow.ToString("0")+")");st.ElbowMax=Math.Max(st.ElbowMax,m.Elbow);
                    Vector3 game=fr.Pose.WorldPosition[left?CapturedRig.LHand:CapturedRig.RHand];
                    bool held=left?fr.Grasp.Left.Seated||fr.Grasp.LeftCarries:fr.Grasp.Right.Seated||fr.Grasp.RightCarries;
                    if(relax>.999f)
                    {
                        st.Relaxed++;
                        if(m.Elbow>15)Check(m.Fold>.999f,at+": the relaxed elbow is a hinge, its crease on the fold ("+m.Fold.ToString("0.0000")+")");
                        // A free, nearly straight arm held by the body (the A-pose look) never spreads wider than the game's own, or the
                        // model's hang (its hips need ~15 degrees from its shoulders), or the copied arm. (A held one goes where its hand
                        // is placed, a bent one's elbow stands out as a body's does (a hand on the hip), a hand flung clear of the body
                        // keeps the game arm's extension, and in a fast gesture the elbow follows at its own pace.)
                        if(!held&&m.Elbow<=Straight&&Clear(fr.Pose,left)<=NativeVisualRig.RelaxNear&&(!has||Vector3.Distance(game,lastGame[side])<Gesture))
                        {
                            float spread=m.Abd-Math.Max(Math.Max(GameAbd(fr.Pose,left),RelaxHang),mo.Abd)-SpreadOver;if(spread>st.WorstSpread){st.WorstSpread=spread;st.SpreadAt=at;}
                            Check(spread<=0,at+": a relaxed arm stands out "+m.Abd.ToString("0")+" degrees (the game's "+GameAbd(fr.Pose,left).ToString("0")+", copied "+mo.Abd.ToString("0")+")");
                            if(mo.Abd>m.Abd+1)st.Narrower++;
                        }
                        if(m.Forearm>st.WorstForearm){st.WorstForearm=m.Forearm;st.ForearmAt=at;}
                        if(m.Upper>st.WorstUpper){st.WorstUpper=m.Upper;st.UpperAt=at;}
                        if(m.Forearm>ForearmInside)st.DeepForearm++;if(mo.Forearm>ForearmInside&&!held)st.DeepForearmBefore++;
                        Check(m.Forearm<=Math.Max(ForearmInside,mo.Forearm+.015f),at+": the forearm or fist "+(m.Forearm*100).ToString("0.0")+" cm inside the torso (copied "+(mo.Forearm*100).ToString("0.0")+")");
                        Check(m.Upper<=Math.Max(UpperInside,mo.Upper+.01f),at+": the upper arm "+(m.Upper*100).ToString("0.0")+" cm inside the chest (copied "+(mo.Upper*100).ToString("0.0")+")");
                        if(still&&!held&&Drop(fr.Pose,left)>.42f&&m.ThighGap>-.05f&&m.ThighGap<.30f&&Math.Abs(m.PalmUp)<.6f)
                        {
                            st.Hanging++;st.HangAbdMin=Math.Min(st.HangAbdMin,m.Abd);st.HangAbdMax=Math.Max(st.HangAbdMax,m.Abd);st.HangGapMin=Math.Min(st.HangGapMin,m.ThighGap);st.HangGapMax=Math.Max(st.HangGapMax,m.ThighGap);st.HangElbowMax=Math.Max(st.HangElbowMax,m.Elbow);
                            st.HangBeforeAbd=Math.Max(st.HangBeforeAbd,mo.Abd);st.HangBeforeGap=Math.Max(st.HangBeforeGap,mo.ThighGap);
                            Check(m.Abd>=HangAbdLow&&m.Abd<=HangAbdHigh,at+": a hanging arm "+m.Abd.ToString("0")+" degrees out (natural "+HangAbdLow+"-"+HangAbdHigh+")");
                            Check(m.ThighGap>=-.015f&&m.ThighGap<=HangGap,at+": a hanging hand "+(m.ThighGap*100).ToString("0")+" cm off the thigh (natural 0-"+(HangGap*100).ToString("0")+")");
                            Check(m.Elbow<=40&&(!palms||m.PalmIn>.7f),at+": a hanging arm's elbow soft ("+m.Elbow.ToString("0")+") and its palm toward the thigh ("+m.PalmIn.ToString("0.00")+")");
                        }
                    }
                    // Frame to frame, a relaxed free arm as smooth as the visual rig's transitions (each hand at most three times the
                    // game's move plus 5 cm, an elbow plus 6 cm), or as the copied arm.
                    if(has)
                    {
                        float game1=Vector3.Distance(game,lastGame[side]),fist=Vector3.Distance(m.Fist,lastFist[side]),elbow=Vector3.Distance(m.ElbowAt,lastElbow[side]);
                        float fistBefore=Vector3.Distance(mo.Fist,oldFist[side]),elbowBefore=Vector3.Distance(mo.ElbowAt,oldElbow[side]);
                        if(relax>0&&!held)
                        {
                            if(fist-3*game1>st.WorstStep){st.WorstStep=fist-3*game1;st.StepAt=at;}st.WorstElbowStep=Math.Max(st.WorstElbowStep,elbow-3*game1);
                            Check(fist<=Math.Max(3*game1+.05f,fistBefore+.02f)&&elbow<=Math.Max(3*game1+.06f,elbowBefore+.02f),at+": the arm jumps "+(fist*100).ToString("0")+" / "+(elbow*100).ToString("0")+" cm while the game's hand moves "+(game1*100).ToString("0"));
                        }
                    }
                    lastFist[side]=m.Fist;lastElbow[side]=m.ElbowAt;lastGame[side]=game;oldFist[side]=mo.Fist;oldElbow[side]=mo.ElbowAt;
                }
                has=true;
            }
        }
        public const float SpreadOver=4,Straight=45,RelaxHang=18,Gesture=.03f,HangAbdLow=8,HangAbdHigh=22,HangGap=.08f,ForearmInside=.035f,UpperInside=.06f;

        public static void Run(string root)
        {
            var clips=Clips(root);
            // The director's scripts are kept for the whole run: these checks leave each one bound or unbound as they found it.
            string ReadyStyles()=>string.Join(",",NativeEntranceDirector.Styles.Where(s=>NativeEntranceDirector.Script(s).Ready));
            string readyBefore=ReadyStyles();
            foreach(var (rigName,rig) in new[]{("the measured skeleton",Measured()),("the synthetic skeleton",VisualRigChecks.Synthetic(out _))})
            {
                var st=new Stats();
                // (The synthetic skeleton's palms face forward at rest: its palms are not measured.)
                bool palms=rigName=="the measured skeleton";
                foreach(var spec in Specs)for(int pose=0;pose<=NativeLobbyPoses.SheldonIdle;pose++)Run(rig,"lobby "+spec+"/"+NativeLobbyPoses.Key(pose),Lobby(clips,spec,pose,6,1/30f),st,true,palms);
                foreach(var style in NativeEntranceDirector.Styles.Concat(new[]{"dev-emperor","dev-ansatsuken","dev-matrix"}))
                {
                    string spec=style.StartsWith("weapon-")?style.Substring(7):style=="dev-emperor"?"emperor":style=="dev-ansatsuken"?"ansatsuken":style==CyberLordSpec.Entrance?CyberLordSpec.Id:"pistol";
                    Run(rig,"intro "+style,Intro(clips,style,spec),st,false,palms);
                }
                Run(rig,"the Cyber Lord's idle",CyberIdle(6),st,false,palms);
                Console.WriteLine("PASS model poses ("+rigName+"): "+st.Poses+" poses of every lobby pose, entrance and the Cyber Lord's idle, "+st.Relaxed+" relaxed arms: hinged, elbows at most "+st.ElbowMax.ToString("0")+" degrees; nearly straight free arms by the body no wider than the game's, the model's hang or the copied arm (closest "+st.WorstSpread.ToString("0.0")+" degrees; "+st.Narrower+" narrower than copied); forearms and fists deepest "+(st.WorstForearm*100).ToString("0.0")+" cm inside the torso ("+st.ForearmAt+"; past "+(ForearmInside*100).ToString("0.0")+" cm in "+st.DeepForearm+" relaxed arms against "+st.DeepForearmBefore+" free copied ones), upper arms "+(st.WorstUpper*100).ToString("0.0")+" ("+st.UpperAt+"); "+st.Hanging+" hanging free arms "+st.HangAbdMin.ToString("0")+"-"+st.HangAbdMax.ToString("0")+" degrees out (copied up to "+st.HangBeforeAbd.ToString("0")+"), hands "+(st.HangGapMin*100).ToString("0")+"-"+(st.HangGapMax*100).ToString("0")+" cm off the thighs (copied up to "+(st.HangBeforeGap*100).ToString("0")+"), elbows at most "+st.HangElbowMax.ToString("0")+"; frame to frame within the transition bound (closest "+(st.WorstStep*100).ToString("0.0")+" cm, "+st.StepAt+") ("+count+" checks)");
            }
            Check(ReadyStyles()==readyBefore,"the entrance scripts are left as found (a clip bound for these checks is unbound again)");
            Cards(root,clips);
            Repeats(clips);
            Wiring(root);
        }

        // The lobby card poses on the measured skeleton, each against its own intent: the seated hands on the lap, palms down,
        // left and right alike; folded arms folded; the daggers' fists out of the hips; the hero stance's free arm by its side.
        private static void Cards(string root,Dictionary<string,Clip> clips)
        {
            var rig=Measured();
            foreach(var spec in Specs)
            {
                foreach(var fr in Lobby(clips,spec,NativeLobbyPoses.Chair,4,.25f))
                {
                    var s=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s,fr.Pose,fr.Grasp,false,false);var l=Measure(s,true);var r=Measure(s,false);string at=spec+" chair at "+fr.Time.ToString("0.00");
                    foreach(var m in new[]{l,r})Check(m.ThighGap>=-.03f&&m.ThighGap<=.03f&&m.PalmUp<-.7f&&m.Abd>=0&&m.Abd<=15,at+": a hand on the lap, palm down, the arm by the side ("+(m.ThighGap*100).ToString("0")+" cm, palm "+m.PalmUp.ToString("0.00")+", "+m.Abd.ToString("0")+" degrees)");
                    Check(Math.Abs(l.Abd-r.Abd)<=3&&Math.Abs(l.Elbow-r.Elbow)<=5,at+": the seated arms alike ("+l.Abd.ToString("0")+"/"+r.Abd.ToString("0")+", "+l.Elbow.ToString("0")+"/"+r.Elbow.ToString("0")+")");
                }
                foreach(var fr in Lobby(clips,spec,NativeLobbyPoses.Crossed,4,.25f))
                {
                    var s=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s,fr.Pose,fr.Grasp,false,false);string at=spec+" crossed at "+fr.Time.ToString("0.00");
                    foreach(bool left in new[]{true,false}){var m=Measure(s,left);Check(m.Elbow>=95&&m.Elbow<=125&&m.TorsoGap>=-.03f&&m.TorsoGap<=.04f&&m.Abd>=-15&&m.Abd<=10,at+": a folded arm ("+m.Elbow.ToString("0")+" degrees, fist "+(m.TorsoGap*100).ToString("0")+" cm off the chest, "+m.Abd.ToString("0")+" out)");}
                }
            }
            foreach(var fr in Lobby(clips,"daggers",NativeLobbyPoses.Weapon,2,.25f))
            {
                var s=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s,fr.Pose,fr.Grasp,false,false);
                Check(s.LeftSeated&&s.RightSeated,"the daggers card seats both fists");
                foreach(bool left in new[]{true,false}){var m=Measure(s,left);Check(m.TorsoGap>=-.02f&&m.ThighGap>=-.04f,"the daggers card's fists out of the hips ("+(m.TorsoGap*100).ToString("0")+" cm, thigh "+(m.ThighGap*100).ToString("0")+")");}
            }
            foreach(string spec in new[]{"greatsword","rifle","machinegun","pistol","sword","shield"})
            {
                var fr=Lobby(clips,spec,NativeLobbyPoses.Weapon,0,1).First();var s=new NativeVisualRig.State(rig);NativeVisualRig.Pose(s,fr.Pose,fr.Grasp,false,false);
                Check(s.RightSeated,spec+" card seats the weapon fist on its own handle (the slot's actor carries the pilot's weapon)");
                if(spec=="rifle"||spec=="machinegun")Check(s.SupportWeight>.99f,spec+" card's support hand on the handguard");
            }
            Console.WriteLine("PASS model poses: the cards on the measured skeleton: seated hands on the lap palms down and alike, folded arms folded against the chest, the daggers' fists out of the hips, every weapon card's fist seated on its own handle ("+count+" checks)");
        }

        // Bit for bit on a fresh state, nothing allocated per frame, and Relax off leaves the adapter as it was.
        private static void Repeats(Dictionary<string,Clip> clips)
        {
            var rig=Measured();var frames=Lobby(clips,"pistol",NativeLobbyPoses.Weapon,2,1/30f).Select(f=>{var p=new CapturedRig.Pose();Array.Copy(f.Pose.Position,p.Position,22);Array.Copy(f.Pose.Rotation,p.Rotation,22);p.World();return (p,f.Grasp);}).ToArray();
            frames=frames.Concat(Lobby(clips,"pistol",NativeLobbyPoses.Chair,1,1/30f).Select(f=>{var p=new CapturedRig.Pose();Array.Copy(f.Pose.Position,p.Position,22);Array.Copy(f.Pose.Rotation,p.Rotation,22);p.World();return (p,f.Grasp);})).ToArray();
            var a=new NativeVisualRig.State(rig);var b=new NativeVisualRig.State(rig);
            foreach(var (p,g) in frames){NativeVisualRig.Pose(a,p,g,false,false);NativeVisualRig.Pose(b,p,g,false,false);for(int v=0;v<rig.Count;v++)Check(a.Local[v]==b.Local[v]&&a.LocalPosition[v]==b.LocalPosition[v],"the relaxed pose repeats bit for bit");}
            for(int i=0;i<50;i++)foreach(var (p,g) in frames)NativeVisualRig.Pose(a,p,g,false,false);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20;i++)foreach(var (p,g) in frames)NativeVisualRig.Pose(a,p,g,false,false);
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"relaxing the arms allocates nothing per frame");
            var off=new NativeVisualRig.State(rig);
            foreach(var (p,g) in frames)
            {
                var plain=g;plain.Relax=0;plain.Still=0;NativeVisualRig.Pose(off,p,plain,false,false);
                Check(off.LeftRelax==0&&off.RightRelax==0,"Relax off moves no arm");
            }
            Console.WriteLine("PASS model poses: the relaxed arms repeat bit for bit, allocate nothing per frame, and stay off without Relax ("+count+" checks)");
        }

        // Which callers relax which arms, and the voxel paths untouched.
        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets","ChipArena",f)).Replace("\r\n","\n");
            string body=R("Runtime/NativeVisualBodyPresentation.cs"),portrait=R("Runtime/NativeVisualPortraitPresentation.cs"),roof=R("Runtime/NativeRooftopPresentation.cs"),intros=R("Runtime/NativeIntros.cs");
            Check(body.Contains("private enum VisualArms { Live, Still, Unarmed, Reach }")&&body.Contains("bool planted,bool airborne,VisualArms arms=VisualArms.Live)")&&body.Contains("VisualArmsOf(ref grasp,a,arms,ChipLookOf(a.Id));\n            body.Binding.Apply(pose,grasp,planted,airborne);"),"every model bind sets the arms' mode on its grasp before posing (the arena's default: Live)");
            Check(body.Contains("float relax=arms==VisualArms.Reach?0:1-Mathf.Clamp01(chip);g.Relax=relax;g.Still=arms==VisualArms.Still?relax:0;")&&body.Contains("else g.LeftCarries=!a.WeaponAway&&NativeLobbyPoses.HeldBone(a.SourceSpec)==CapturedRig.LHand;")
                &&body.Contains("if(arms==VisualArms.Unarmed){bool fists=NativeLobbyPoses.Fighter(a.SourceSpec);g=new NativeVisualRig.Grasp{RightGrip=fists?g.RightGrip:0,LeftGrip=fists?g.LeftGrip:0};}"),"the free arms relax (not while the chip-select look is up), held ones in a still stance, the staff and the bow ride a carrying hand, and an unarmed entrance lets go");
            Check(body.Contains("var held=VisualBodyBind(body,rig,a,time,pose,a.Grounded&&!roll&&!juggle,roll||juggle,body.Motion!=null&&body.Motion.Weight>=1?VisualArms.Reach:VisualArms.Live);")&&body.Contains("pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false,armed?VisualArms.Live:VisualArms.Unarmed);")
                &&body.Contains("pose.World();var held=VisualBodyBind(body,rig,a,time,pose,false,false,VisualArms.Reach);")&&body.Contains("var held=VisualBodyBind(body,rig,a,time,standPose,false,false);"),"the arena stands Live (as copied while the pack's clips cover the whole body), a revive Live, an entrance Live or Unarmed, the draft's reaching hands as the game has them");
            Check(intros.Contains("VisualIntroPose(introRig,actor,arena.Time,NativeSourceMotion.IntroPose,null,NativeIntroRules.IntroWeapon(style)!=null);")&&intros.Contains("VisualIntroPose(introRig,actor,arena.Time,entrancePose,entranceReveal,NativeIntroRules.IntroWeapon(style)!=null||style==NativeBowEntrance.Id);"),"an entrance is armed only where it draws a weapon (the procedural ones closed the fists on the pilot's unseen weapon)");
            Check(portrait.Contains("var held=VisualBodyBind(body,s.Player,a,time,s.Pose,false,false,VisualArms.Still);")&&portrait.Contains("a.WeaponAway=!weapon.Visible;a.Grounded=true;VisualArmedAs(a,spec);")
                &&portrait.Contains("a.Weapon=spec==\"pistol\"?WeaponId.Pistol:spec==\"rifle\"?WeaponId.Rifle:spec==\"machinegun\"?WeaponId.MachineGun:WeaponId.Sword;")&&portrait.Contains("a.VisualSet=spec==\"daggers\"?1:spec==\"greatsword\"?2:spec==\"shield\"?3:0;"),"a portrait stands Still, its actor carrying the pilot's weapon as the arena's do");
            Check(roof.Contains("a.WeaponAway=!s.Weapon.Visible;a.Grounded=true;VisualArmedAs(a,look.Spec);")&&roof.Contains("look.Script==NativeRooftopActors.Seated||look.Script==NativeRooftopActors.Watcher?VisualArms.Still:VisualArms.Live"),"the rooftop's seated pilot and watcher stand Still, its runner, blade and gunner Live");
            Check(body.Contains("if(Viewer||sourceModels==null||ClassicBodies||a.SourceSpec==\"siren\"||FirstPerson&&a.Id==LocalId||!rigs.TryGetValue(a.Id,out var rig)||!VisualBodiesOn)return;")&&body.Contains("if(Viewer||sourceModels==null||ClassicBodies||rig==null||a==null||pose==null||a.SourceSpec==\"siren\"||!VisualBodiesOn)return;")
                &&body.Contains("if(Viewer||sourceModels==null||ClassicBodies||rig==null||seat==null||pose==null||seat.spec==\"siren\"||!VisualBodiesOn)return false;")&&portrait.Contains("public bool ModelPortraitFor(in PortraitLook look)=>!modelPortraitFailed&&!ClassicBodies&&VisualBodiesOn")&&roof.Contains("if(Viewer||ClassicBodies||!VisualBodiesOn)return false;"),
                "Classic never reaches the model's binds: the arena, the entrance, the draft, the portraits and the rooftop return to the voxel paths first");
            Check(body.Contains("var grasp=VisualGrasp(a,pose,time,ChipLookOf(a.Id));if(held!=null)VisualWeaponGrasp(ref grasp,held);\n            viewBody.Binding.Apply(pose,grasp,false,false);"),"first person's forearms keep the game's arms (no relax on the view body; the model weapon's grasp only)");
            foreach(string core in new[]{"Core/NativeLobbyPoses.cs","Core/NativeEntranceDirector.cs","Core/NativeEntrancePose.cs","Core/NativeEntranceProcedural.cs","Core/NativeIntroMotion.cs","Core/CyberLordIdle.cs","Core/NativeRooftopActors.cs","Core/MagicPose.cs"})
                Check(!R(core).Contains("NativeVisualRig"),"the voxel poses never read the model's adapter: "+core);
            foreach(string pack in new[]{"Bozo","BoZo","Magica","OutfitSystem","BMAC","BSMC"})Check(!R("Core/NativeVisualRig.cs").Contains(pack),"no pack names in the adapter: "+pack);
            Console.WriteLine("PASS model poses: the wiring: every model bind's arms mode (Live, Still, Unarmed, Reach), portraits and the rooftop carry the pilot's weapon, Classic and first person untouched, the voxel poses independent of the adapter ("+count+" checks)");
        }
    }
}
#endif
