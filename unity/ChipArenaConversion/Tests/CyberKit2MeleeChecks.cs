using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The Cyber Lord's kit v2 melee and shared presentation (docs/CYBER-LORD.md "Kit v2", "Presentation status"; owner 2026-10-15:
// "make sure the animations are great, beautiful fx, better basic attacks, (much better combo animation on melee)"):
//  1. the proposed record's helpers: the clones' formation, the zip, yank and dash-slash bodies, the meteor's leap arc and dive;
//  2. the feed: only a Version 2 record maps (through CyberLordFeed alone); anything else leaves v1 exactly as it was, poses
//     and effects included; Kernel Panic is untouched by a live v2 record;
//  3. the hardlight string (v1's three steps live today, v2's five): every pose finite and continuous across the steps, on
//     uneven and rewound clocks; each blow's apex on its replicated hit; the feet planted (no slide on the floor);
//  4. every v2 ability's poses, effects (deterministic from the view, nothing flat on the floor, the low tiers lighter, no
//     allocation), HUD cards with their art icons, screen overlays and sound cues on existing bank keys;
//  5. the Runtime wiring (source text; Unity cannot run here).
// Pixels are checked by the offline renders; rules, damage, timing and movement are the authority's and untouched.
internal static class CyberKit2MeleeChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber Lord kit v2 melee: "+what);count++;}
    private const float Dt=1/60f,C=CyberLordFeed.BodyCentre;
    private static readonly Vector3 Up=Vector3.UnitY;

    // ---- a recording sink and a fixed scene (as CyberLordChecks') ----
    private sealed class Sink:ICyberFxSink
    {
        public int Count;public int Flat;public string FlatWhat;public double Hash;
        private void Floor(float top,float thick,float across,string what){if(top<.12f&&thick<.03f&&across>.1f){Flat++;FlatWhat??=what+" (top "+top.ToString("0.000")+", "+thick.ToString("0.000")+" thick, "+across.ToString("0.00")+" across)";}}
        private static Vector3 Abs(Vector3 v)=>new Vector3(Math.Abs(v.X),Math.Abs(v.Y),Math.Abs(v.Z));
        private void Mix(Vector3 a,float k){Hash=Hash*1.0000003+a.X*.37+a.Y*.61+a.Z*.89+k*1.3;if(!(float.IsFinite(a.X)&&float.IsFinite(a.Y)&&float.IsFinite(a.Z)&&float.IsFinite(k)))throw new Exception("Cyber Lord kit v2 melee: a non-finite effect");}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Count++;Mix(at,size+color.X+u);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Count++;Mix(head,width+velocity.Y+color.Y);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Count++;Mix(at,opacity+size);}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){Count++;Mix(at,size+color.Z+profile);}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){Count++;Mix(tail+head*.5f,width+color.X);if(!view)Floor(Math.Max(tail.Y,head.Y),Math.Abs(tail.Y-head.Y)+width,new Vector2(head.X-tail.X,head.Z-tail.Z).Length(),"line");}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color)
        {
            Count++;Mix(a+c*.5f,ua+ub+color.Y);if(view)return;float top=Math.Max(Math.Max(a.Y,b.Y),Math.Max(c.Y,d.Y)),bot=Math.Min(Math.Min(a.Y,b.Y),Math.Min(c.Y,d.Y));
            float across=Math.Max(new Vector2(a.X-c.X,a.Z-c.Z).Length(),new Vector2(b.X-d.X,b.Z-d.Z).Length());Floor(top,top-bot,across,"band");
        }
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color)
        {
            Count++;Mix(at,scale.X+rotation.W+color.Z);if(view)return;var h=Abs(Vector3.Transform(Vector3.UnitX,rotation))*scale.X*.5f+Abs(Vector3.Transform(Vector3.UnitY,rotation))*scale.Y*.5f+Abs(Vector3.Transform(Vector3.UnitZ,rotation))*scale.Z*.5f;
            Floor(at.Y+h.Y,2*h.Y,2*Math.Max(h.X,h.Z),"voxel");
        }
        public void Reset(){Count=0;Hash=0;}
    }
    private sealed class Scene:ICyberFxScene
    {
        public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public int ViewerId,LocalId;
        private static Vector3 P(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public Vector3 Feet(Actor a)=>P(a);
        public bool Chest(Actor a,out Vector3 c){c=P(a)+new Vector3(0,1.25f,0);return true;}
        public bool Fist(Actor a,bool left,out Vector3 f){var fw=KernelPanicPath.Forward(a.Yaw);var r=new Vector3(fw.Z,0,-fw.X);f=P(a)+new Vector3(0,1.3f,0)+fw*.35f+r*(left?-.18f:.18f);return true;}
        public int Viewer=>ViewerId;public int Local=>LocalId;
        public bool ViewFist(bool left,out Vector3 f){f=new Vector3(left?-.15f:.15f,1.35f,-5.55f);return ViewerId!=0;}
        public Vector3 Eye=>new Vector3(0,1.6f,-6);public float Floor(Vector3 at)=>0;
    }
    // A scene whose fists are the rig's drawn fists (third person, the pilot at the origin facing +z: BodyToWorld's frame).
    private sealed class RigScene:ICyberFxScene
    {
        private readonly Rig rig;public RigScene(Rig r){rig=r;}
        public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;
        public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public bool Chest(Actor a,out Vector3 c){c=World(rig.Mo.Rig.ChainPoint(CapturedRig.Chest,-1));return true;}
        public bool Fist(Actor a,bool left,out Vector3 f){f=World(rig.Fist(left));return true;}
        public int Viewer=>0;public int Local=>0;public bool ViewFist(bool left,out Vector3 f){f=default;return false;}
        public Vector3 Eye=>new Vector3(3,1.6f,2);public float Floor(Vector3 at)=>0;
    }
    /// <summary>A rig's group point in the world of a pilot at the origin facing +z (NativeCyberLordFx.BodyToWorld).</summary>
    private static Vector3 World(Vector3 group)=>NativeCyberLordFx.BodyToWorld(Vector3.Zero,0,group);
    // A sink keeping where it drew (band and line corners, sprites; voxels apart) to measure how close an effect rides a limb.
    private sealed class NearSink:ICyberFxSink
    {
        private readonly List<Vector3> pts=new List<Vector3>(),vox=new List<Vector3>();
        public void Reset(){pts.Clear();vox.Clear();}
        public float Nearest(Vector3 p){float d=10;foreach(var q in pts)d=Math.Min(d,(q-p).Length());return d;}
        public float NearestVoxel(Vector3 p){float d=10;foreach(var q in vox)d=Math.Min(d,(q-p).Length());return d;}
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){pts.Add(head);}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){if(!view)pts.Add(at);}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){if(!view){pts.Add(tail);pts.Add(head);}}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){if(!view){pts.Add(a);pts.Add(b);pts.Add(c);pts.Add(d);}}
        public void Voxel(bool view,WeaponShape shape,Vector3 at,Quaternion rotation,Vector3 scale,Vector3 color){if(!view)vox.Add(at);}
    }
    private static ArenaEvent Ev(int id,EventKind kind,float time,int source,int target,Vector3 at,string detail,float amount=10)=>new ArenaEvent{Id=id,Kind=kind,Time=time,Source=source,Target=target,Position=new V3(at.X,at.Y,at.Z),From=new V3(at.X,at.Y-.4f,at.Z-1),Amount=amount,Detail=detail};

    // ---- one pilot through the fighter route (FighterBase, the Cyber Lord's style, FighterFinish) ----
    private sealed class Rig
    {
        public readonly MeleeMotion Mo=new MeleeMotion();public readonly CyberLordMotion Cy=new CyberLordMotion();public readonly MeleeView Mv=new MeleeView{Id="1",Spec=CyberLordSpec.Id,Grounded=true,Hp=100};
        public string Step(Actor a,float t,float dt){Mo.Rig.GroupPosition=new Vector3(0,.865f,0);Mo.Rig.GroupYaw=0;Mo.FighterBase(Mv,t,dt);var st=Cy.Style(Mo,a,t,false);Mo.FighterFinish(Mv,t,dt);return st;}
        private static readonly Vector3 FistLocal=new Vector3(0,-.06f,.03f);
        public Vector3 Fist(bool left){var r=Mo.Rig;int b=left?CapturedRig.LHand:CapturedRig.RHand;return r.ChainPoint(b,-1)+Vector3.Transform(FistLocal,r.ChainQuat(b,-1));}
        public Vector3 Ankle(bool left)=>Mo.Rig.ChainPoint(left?CapturedRig.LFoot:CapturedRig.RFoot,-1);
        public bool Finite(){var r=Mo.Rig;for(int b=0;b<MeleeRig.N;b++){var q=r.Q[b];var p=r.P[b];if(!(float.IsFinite(q.X)&&float.IsFinite(q.Y)&&float.IsFinite(q.Z)&&float.IsFinite(q.W)&&float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)))return false;}return true;}
        public Vector3 Knee(bool left)=>Mo.Rig.ChainPoint(left?CapturedRig.LKnee:CapturedRig.RKnee,-1);
        private Vector3 SolePoint(bool left,Vector3 local){var r=Mo.Rig;int b=left?CapturedRig.LFoot:CapturedRig.RFoot;return r.ChainPoint(b,-1)+Vector3.Transform(local,r.ChainQuat(b,-1));}
        private static readonly Vector3 HeelLocal=new Vector3(0,-MeleeFootwork.Sole,MeleeFootwork.HeelZ),BallLocal=new Vector3(0,-MeleeFootwork.Sole,MeleeFootwork.BallZ),TipLocal=new Vector3(0,MeleeFootwork.ToeTipY,MeleeFootwork.ToeTipZ);
        /// <summary>The frame's fists, ankles, knees, sole points (left heel, ball, toe tip, then the right's), drawn body and bones.</summary>
        public Pose Capture(float t)=>new Pose{T=t,LF=Fist(true),RF=Fist(false),LA=Ankle(true),RA=Ankle(false),LK=Knee(true),RK=Knee(false),LB=Mo.Rig.ChainPoint(CapturedRig.LToe,-1),RB=Mo.Rig.ChainPoint(CapturedRig.RToe,-1),Body=Mo.Rig.P[CapturedRig.Body],
            Sole=new[]{SolePoint(true,HeelLocal),SolePoint(true,BallLocal),SolePoint(true,TipLocal),SolePoint(false,HeelLocal),SolePoint(false,BallLocal),SolePoint(false,TipLocal)},Snap=Snapshot()};
        public float[] Snapshot(){var r=Mo.Rig;var o=new float[MeleeRig.N*7];for(int b=0;b<MeleeRig.N;b++){var q=r.ChainQuat(b,-1);var p=r.ChainPoint(b,-1);o[b*7]=q.X;o[b*7+1]=q.Y;o[b*7+2]=q.Z;o[b*7+3]=q.W;o[b*7+4]=p.X;o[b*7+5]=p.Y;o[b*7+6]=p.Z;}return o;}
    }
    private sealed class Pose{public float T;public Vector3 LF,RF,LA,RA,LK,RK,LB,RB,Body;public Vector3[] Sole;public float[] Snap;}
    /// <summary>A foot's ball (the toe joint) resting on the floor: a planted foot's pinned point, flat or pivoting on it.</summary>
    private const float BallFloor=MeleeRig.FLOOR+MeleeFootwork.BallY;
    private static bool OnFloor(Vector3 ball)=>ball.Y<BallFloor+.006f;
    /// <summary>The floor under the rig (group space: the ankle of a flat foot is MeleeRig.FLOOR, its sole .07 m lower).</summary>
    private const float Floor=-CyberLordFeed.BodyCentre;
    private static float MaxDiff(float[] a,float[] b){float m=0;for(int i=0;i<a.Length;i++)m=Math.Max(m,Math.Abs(a[i]-b[i]));return m;}
    /// <summary>The largest bone turn (rad, sign-free) and bone move (m) between two snapshots (group space).</summary>
    private static (float turn,float shift) BoneStep(float[] a,float[] b,int first=0,int last=MeleeRig.N-1)=>BoneStep(a,b,out _,first,last);
    /// <summary>As BoneStep, also naming the bone that turned furthest.</summary>
    private static (float turn,float shift) BoneStep(float[] a,float[] b,out int worst,int first=0,int last=MeleeRig.N-1)
    {
        float turn=0,shift=0;worst=-1;
        for(int k=first*7;k<=last*7&&k<a.Length;k+=7){float d=Math.Abs(a[k]*b[k]+a[k+1]*b[k+1]+a[k+2]*b[k+2]+a[k+3]*b[k+3]),tu=2*(float)Math.Acos(Math.Min(1,d));if(tu>turn){turn=tu;worst=k/7;}shift=Math.Max(shift,new Vector3(a[k+4]-b[k+4],a[k+5]-b[k+5],a[k+6]-b[k+6]).Length());}
        return (turn,shift);
    }

    // The whole v2 timeline on pilot 1 (a dummy, 2, 3 m ahead): the five-step string, an Uplink zip, an Uplink yank on the
    // dummy, the Overclock and a fast string, the Fork Bomb with a string its clones copy and their burst, a Glitch Cut
    // marking the dummy, and a Meteor Byte. The record is written as the proposed host would, frame by frame.
    private const float ComboT=1,ZipT=3,YankT=5,OverT=7,OverComboT=7.5f,ForkT=10,ForkComboT=10.6f,BurstT=12.5f,CutT=14,MeteorT=16,EndT=18;
    private static readonly Vector3 Dummy=new Vector3(0,0,3);
    private static float[] Steps(float at,int n,float rate){var o=new float[n];for(int i=0;i<n;i++){o[i]=at;at+=CyberKit2Spec.ComboSeconds[i]/rate;}return o;}
    private static readonly float[] String1=Steps(ComboT,5,1),String2=Steps(OverComboT,3,1+CyberKit2Spec.OverclockRate),String3=Steps(ForkComboT,5,1);
    private static void Write(CyberKit2MeleeRecord r,float t)
    {
        r.Version=CyberKit2Spec.Version;
        void Strings(float[] at,float rate){for(int i=at.Length-1;i>=0;i--)if(t>=at[i]){if(r.At[CyberKit2Spec.MeleeLmb]!=at[i])CyberKit2MeleeFixture.Combo(r,i,at[i],rate);break;}}
        if(t>=ComboT&&t<ZipT)Strings(String1,1);
        if(t>=ZipT&&r.At[CyberKit2Spec.Q]<ZipT-.01f)CyberKit2MeleeFixture.Uplink(r,ZipT,new Vector3(0,C,0),new Vector3(0,C+.6f,12),CyberKit2Melee.Zip);
        if(t>=YankT&&r.At[CyberKit2Spec.Q]<YankT-.01f)CyberKit2MeleeFixture.Uplink(r,YankT,new Vector3(0,C,0),Dummy+Up*C,CyberKit2Melee.Yank,2);
        if(t>=OverT&&r.At[CyberKit2Spec.E]<OverT-.01f)CyberKit2MeleeFixture.Overclock(r,OverT);
        if(t>=OverComboT&&t<ForkT)Strings(String2,1+CyberKit2Spec.OverclockRate);
        if(t>=ForkT&&r.At[CyberKit2Spec.Fork]<ForkT-.01f)CyberKit2MeleeFixture.Fork(r,ForkT,Vector3.Zero,0);
        if(t>=ForkComboT&&t<CutT){float before=r.At[CyberKit2Spec.MeleeLmb];Strings(String3,1);if(r.At[CyberKit2Spec.MeleeLmb]!=before&&t<BurstT)for(int k=0;k<CyberKit2Spec.Clones;k++)CyberKit2MeleeFixture.ForkStrike(r,k,r.Release[CyberKit2Spec.MeleeLmb]+NativeCyberLordFx.ForkLag+NativeCyberLordFx.ForkLagStep*k,2,Dummy);}
        if(t>=ForkT&&t<r.ForkUntil)CyberKit2MeleeFixture.ForkFollow(r,Vector3.Zero,0);
        if(t>=BurstT&&r.CloneBurstAt[0]<ForkT)CyberKit2MeleeFixture.ForkBurst(r,BurstT,new[]{Dummy+new Vector3(-.5f,0,0),Dummy+new Vector3(.5f,0,0),Dummy+new Vector3(0,0,.5f)});
        if(t>=CutT&&r.At[CyberKit2Spec.Cut]<CutT-.01f)CyberKit2MeleeFixture.GlitchCut(r,CutT,new Vector3(0,C,0),new Vector3(0,C,8),2);
        if(t>=MeteorT&&r.At[CyberKit2Spec.Meteor]<MeteorT-.01f)CyberKit2MeleeFixture.Meteor(r,MeteorT,new Vector3(0,C,0),new Vector3(0,0,8));
    }
    private static List<ArenaEvent> Events()
    {
        var e=new List<ArenaEvent>();int id=1;var at=Dummy+Up*1.2f;
        e.Add(Ev(id++,EventKind.Hit,String1[2]+CyberKit2Spec.ComboWindups[2],1,2,at,"cyber-spin",12));e.Add(Ev(id++,EventKind.Hit,String1[3]+CyberKit2Spec.ComboWindups[3],1,2,at,"cyber-rise",12));
        e.Add(Ev(id++,EventKind.Hit,String1[4]+CyberKit2Spec.ComboWindups[4],1,2,at,"cyber-palm",18));e.Add(Ev(id++,EventKind.AbilityHit,YankT+.3f,1,2,at,"uplink-yank",5));
        e.Add(Ev(id++,EventKind.Hit,String2[1]+.06f,1,3,new Vector3(3,1.2f,5),"overclock",8));e.Add(Ev(id++,EventKind.Hit,String3[1]+.2f,1,2,at,"fork-strike",3));
        e.Add(Ev(id++,EventKind.AbilityHit,BurstT+.3f,1,2,at,"fork-burst",20));e.Add(Ev(id++,EventKind.AbilityHit,CutT+.15f,1,2,at,"glitch-mark",0));
        e.Add(Ev(id++,EventKind.AbilityHit,CutT+.14f+CyberKit2Spec.MarkDelay,1,2,at,"glitch-detonate",25));e.Add(Ev(id++,EventKind.AbilityHit,MeteorT+CyberKit2Spec.MeteorTotal,1,2,new Vector3(0,.4f,8),"meteor-impact",35));
        return e;
    }

    public static IEnumerable<string> Run(string root)
    {
        var old=CyberLordFeed.Kit2MeleeSource;
        try{foreach(var r in RunAll(root))yield return r;}
        finally{CyberLordFeed.Kit2MeleeSource=old;CyberKit2Melee.Clones.Clear();}
    }
    private static IEnumerable<string> RunAll(string root)
    {
        // ---- 1. the record's helpers ----
        int c0=count;
        for(int yaw=-180;yaw<=180;yaw+=45)
        {
            var feet=new Vector3(3,0,-2);var fw=KernelPanicPath.Forward(yaw);var seen=new List<Vector3>();
            for(int i=0;i<CyberKit2Spec.Clones;i++)
            {
                var p=CyberKit2Melee.Formation(i,feet,yaw);CyberKit2Spec.ForkSlot(i,out float x,out float z);seen.Add(p);
                Check(Math.Abs((p-feet).Length()-(float)Math.Sqrt(x*x+z*z))<1e-4f&&p.Y==feet.Y&&Vector3.Dot(p-feet,fw)<0,"clone "+i+" stands in its slot behind the caster at yaw "+yaw);
            }
            Check(seen.Distinct().Count()==CyberKit2Spec.Clones&&(seen[0]-seen[1]).Length()>2,"the three slots are apart at yaw "+yaw);
        }
        Check(CyberKit2Spec.LeapArc(0)==0&&Math.Abs(CyberKit2Spec.LeapArc(1)-CyberKit2Spec.LeapHeight)<1e-5f&&CyberKit2Spec.DiveShare(0)==0&&CyberKit2Spec.DiveShare(1)==1,"the leap arc and the dive share span their ranges");
        {
            var from=new Vector3(1,C,1);var target=new Vector3(4,.5f,9);float at=20,prevH=0;
            Check(CyberKit2Melee.MeteorBody(from,target,at,at+.05f)==from,"the meteor crouches where it stood");
            for(float t=CyberKit2Melee.LeapAt(at);t<=CyberKit2Melee.HoverAt(at);t+=.01f){float h=CyberKit2Melee.MeteorBody(from,target,at,t).Y-from.Y;Check(h>=prevH-1e-4f,"the leap rises monotonically");prevH=h;}
            Check((CyberKit2Melee.MeteorBody(from,target,at,CyberKit2Melee.HoverAt(at)+.1f)-(from+Up*CyberKit2Spec.LeapHeight)).Length()<1e-4f,"the hover holds the apex");
            Check((CyberKit2Melee.MeteorBody(from,target,at,CyberKit2Melee.DiveAt(at)+CyberKit2Spec.DiveSeconds)-(target+Up*C)).Length()<1e-4f,"the dive lands its body centre over the target");
            Vector3 prev=CyberKit2Melee.MeteorBody(from,target,at,at);float fastest=0;
            for(float t=at;t<at+CyberKit2Spec.MeteorTotal+.1f;t+=Dt){var p=CyberKit2Melee.MeteorBody(from,target,at,t);fastest=Math.Max(fastest,(p-prev).Length()/Dt);prev=p;}
            Check(fastest<120,"the meteor's body never teleports ("+fastest.ToString("0")+" m/s at most)");
            var anchor=new Vector3(0,C+1,14);float latch=CyberKit2Melee.LatchAt(1,from+Up*CyberKit2Melee.EmitterHeight,anchor),arrive=CyberKit2Melee.ZipArrive(latch,from,anchor);
            Check(latch>1&&arrive>latch&&arrive<=latch+CyberKit2Spec.ZipMax+1e-4f,"the zip latches after the release and arrives within ZipMax");
            Check(CyberKit2Melee.ZipBody(from,anchor,latch,arrive,latch)==from&&Math.Abs((anchor-CyberKit2Melee.ZipBody(from,anchor,latch,arrive,arrive)).Length()-CyberKit2Melee.ZipStandoff)<1e-3f,"the zip runs from the caster to the standoff before the anchor");
            var vic=new Vector3(0,C,6);var yanked=CyberKit2Melee.YankBody(vic,from,Vector3.UnitZ,2,2+CyberKit2Spec.YankSeconds);
            Check(CyberKit2Melee.YankBody(vic,from,Vector3.UnitZ,2,2)==vic&&Math.Abs(yanked.Z-(from.Z+CyberKit2Spec.YankStop))<1e-3f&&yanked.Y==vic.Y,"the yank hauls the victim to YankStop ahead of the caster");
            Check(CyberKit2Melee.CutBody(from,anchor,1,1.14f,1)==from&&(CyberKit2Melee.CutBody(from,anchor,1,1.14f,1.14f)-anchor).Length()<1e-4f,"the dash-slash runs its path");
            Check(CyberKit2Melee.CloneId(5,0)!=CyberKit2Melee.CloneId(5,1)&&CyberKit2Melee.IsClone(CyberKit2Melee.CloneId(5,2))&&!CyberKit2Melee.IsClone(5)&&!CyberLordMotion.LegacyCombo(CyberKit2Melee.CloneId(5,0))&&CyberLordMotion.LegacyCombo(-1000),
                "the fork clones' stand-in ids are their own (Kernel Panic's clones keep v1's takes)");
        }
        yield return $"Cyber Lord kit v2 helpers: {count-c0} checks (the clones' formation at eight headings, the leap arc, hover and dive, the zip, yank and dash-slash bodies, the clones' ids)";

        // ---- 2. the feed: Version 2 only, through CyberLordFeed ----
        c0=count;
        {
            var src=new CyberKit2MeleeSource();CyberLordFeed.Kit2MeleeSource=src;var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);
            CyberLordFixture.Combo(a,1,5);CyberLordFixture.DataDash(a,4.9f);var bare=new CyberLordView();CyberLordFeed.Kit2MeleeSource=null;CyberLordFeed.Read(a,bare);CyberLordFeed.Kit2MeleeSource=src;
            foreach(int version in new[]{0,1,3})
            {
                var r=src.For(1);r.Version=version;CyberKit2MeleeFixture.Combo(r,3,5);r.Version=version;var v=new CyberLordView();CyberLordFeed.Read(a,v);
                Check(!v.Kit2.Live&&v.ComboStep==bare.ComboStep&&v.ComboAt==bare.ComboAt&&v.ComboHit==bare.ComboHit&&v.At.SequenceEqual(bare.At)&&v.Until.SequenceEqual(bare.Until),"a record of Version "+version+" leaves the v1 view as it was");
            }
            {
                var r=src.For(1);r.Version=CyberKit2Spec.Version;CyberKit2MeleeFixture.Combo(r,3,5);var v=new CyberLordView();CyberLordFeed.Read(a,v);
                Check(v.Kit2.Live&&v.Kit2.ComboStep==3&&v.Kit2.ComboAt==5&&Math.Abs(v.Kit2.ComboHit-(5+CyberKit2Spec.ComboWindups[3]))<1e-5f&&v.ComboAt<-90&&v.At[CyberLordSpec.Q]<-90,"Version 2 maps the string and clears v1's combo and Q to 3");
                var other=new CyberLordView();CyberLordFeed.Read(CyberLordFixture.Pilot(9,Vector3.Zero,0),other);Check(!other.Kit2.Live,"another pilot without a record stays v1");
                src.Remove(1);CyberLordFeed.Read(a,v);Check(!v.Kit2.Live&&v.ComboAt==bare.ComboAt,"a removed record clears the v2 view");
            }
            {
                var k=CyberLordFixture.Pilot(1,Vector3.Zero,0);CyberLordFixture.KernelPanic(k,8,2,new Vector3(0,0,1.4f));var r=src.For(1);r.Version=CyberKit2Spec.Version;CyberKit2MeleeFixture.Overclock(r,7.5f);
                var v=new CyberLordView();CyberLordFeed.Read(k,v);var w=new CyberLordView();CyberLordFeed.Kit2MeleeSource=null;CyberLordFeed.Read(k,w);CyberLordFeed.Kit2MeleeSource=src;
                Check(v.Kit2.Live&&v.PanicStart==w.PanicStart&&v.At[CyberLordSpec.Four]==w.At[CyberLordSpec.Four]&&v.Ready[CyberLordSpec.Four]==w.Ready[CyberLordSpec.Four],"Kernel Panic's slot and sequence stay mapped under a live v2 record");
                // Kernel Panic's poses are untouched by it.
                src.Remove(1);src.For(1);var ra=new Rig();var rb=new Rig();float worst=0;bool kernel=false;
                for(float t=7;t<12;t+=Dt){CyberLordFeed.Kit2MeleeSource=src;var sa=ra.Step(k,t,Dt);var pa=ra.Snapshot();CyberLordFeed.Kit2MeleeSource=null;var sb=rb.Step(k,t,Dt);var pb=rb.Snapshot();
                    if(t>=8&&t<8+KernelPanicPath.End){kernel|=sa.StartsWith("cyber-kernel");worst=Math.Max(worst,MaxDiff(pa,pb));Check(sa==sb,"Kernel Panic's state at "+t.ToString("0.00")+" is the same with v2 live ("+sa+" / "+sb+")");}}
                Check(kernel&&worst<1e-5f,"Kernel Panic poses identically with v2 live (max "+worst+")");
            }
            src.Clear();
            // v1 draws exactly as before for any record that is not Version 2 (poses and effects).
            var actor=CyberLordFixture.Pilot(1,Vector3.Zero,0);var dummy=CyberLordFixture.Pilot(2,Dummy,180);dummy.SourceSpec="sword";
            CyberLordFixture.Combo(actor,0,1);CyberLordFixture.DataDash(actor,2.2f);CyberLordFixture.Firewall(actor,3.2f);CyberLordFixture.SystemCrash(actor,4.2f);
            var junk=src.For(1);junk.Version=1;CyberKit2MeleeFixture.Combo(junk,4,1);junk.Version=1;
            var ev=new List<ArenaEvent>{Ev(1,EventKind.Hit,1.1f,1,2,Dummy+Up,"cyber-jab",9)};
            var r1=new Rig();var r2=new Rig();var f1=new NativeCyberLordFx();var f2=new NativeCyberLordFx();var s1=new Sink();var s2=new Sink();var scene=new Scene();scene.List.Add(actor);scene.List.Add(dummy);
            float poseDiff=0;double fxDiff=0;
            for(float t=.5f;t<6;t+=Dt)
            {
                CyberLordFeed.Kit2MeleeSource=null;r1.Step(actor,t,Dt);f1.Observe(ev,t);s1.Reset();f1.Draw(s1,scene,t,1,false);
                CyberLordFeed.Kit2MeleeSource=src;r2.Step(actor,t,Dt);f2.Observe(ev,t);s2.Reset();f2.Draw(s2,scene,t,1,false);
                poseDiff=Math.Max(poseDiff,MaxDiff(r1.Snapshot(),r2.Snapshot()));fxDiff=Math.Max(fxDiff,Math.Abs(s1.Hash-s2.Hash)+Math.Abs(s1.Count-s2.Count));
            }
            Check(poseDiff==0&&fxDiff==0,"a Version 1 record changes nothing: v1's poses and effects are bit-identical (pose "+poseDiff+", effects "+fxDiff+")");
            src.Clear();CyberLordFeed.Kit2MeleeSource=null;
            // The one mapping: only CyberLordFeed reads a record into the view; only the Dev Sandbox preview sets the source.
            var core=Path.Combine(root,"Assets/ChipArena/Core");var runtime=Path.Combine(root,"Assets/ChipArena/Runtime");
            var all=Directory.GetFiles(core,"*.cs").Concat(Directory.GetFiles(runtime,"*.cs")).ToArray();
            foreach(var f in all)
            {
                var text=File.ReadAllText(f);var name=Path.GetFileName(f);
                if(name!="CyberKit2Melee.cs"&&name!="NativeCyberLord.cs")Check(!text.Contains("CyberKit2MeleeFeed.Read(")&&!text.Contains("CyberKit2MeleeFeed.Map("),name+" maps no v2 record itself");
                if(name!="NativeCyberKit2MeleePresentation.cs")Check(!Regex.IsMatch(text,@"Kit2MeleeSource\s*=[^=]"),name+" never sets the v2 record source");
            }
            var feed=File.ReadAllText(Path.Combine(core,"NativeCyberLord.cs"));
            Check(Regex.Matches(feed,@"CyberKit2MeleeFeed\.Read\(a,v,").Count==2&&feed.Contains("public static ICyberKit2MeleeSource Kit2MeleeSource;"),"CyberLordFeed.Read maps the v2 record on both of its returns");
            Check(CyberKit2MeleeFeed.Details.Select(CyberKit2MeleeFeed.Hit).SequenceEqual(Enumerable.Range(1,CyberKit2MeleeFeed.Details.Length).Select(i=>(Kit2Hit)i))&&CyberKit2MeleeFeed.Hit("cyber-palm")==Kit2Hit.None&&CyberLordFeed.Hit("cyber-palm")!=CyberHit.None&&CyberKit2MeleeFeed.Hit(null)==Kit2Hit.None,
                "the v2 event Details map in order; the string's jab, cross and palm keep v1's");
            foreach(var d in CyberKit2MeleeFeed.Details)Check(CyberLordFeed.Hit(d)==CyberHit.None,"v2 Detail "+d+" is not a v1 one");
        }
        yield return $"Cyber Lord kit v2 feed: {count-c0} checks (only Version 2 maps, through CyberLordFeed alone; v1's poses and effects bit-identical otherwise; Kernel Panic untouched; the event Details)";

        // ---- 3. the hardlight martial string: v1's three steps, v2's five (at rate 1 and Overclocked), reduced effects ----
        // (glitch-step jab, rear roundhouse kick, spinning hook kick, jumping knee into a rising uppercut, horse-stance Pulse Palm)
        c0=count;
        string strings="";float worstSlide=0;float baseTurn=0,baseShift=0,baseAnkle=0;
        foreach(var (v2,rate,reduced) in new[]{(false,1f,false),(true,1f,false),(true,1+CyberKit2Spec.OverclockRate,false),(true,1f,true)})
        {
            string tag=(v2?"v2":"v1")+(rate>1?" overclocked":"")+(reduced?" reduced":"");
            var src=new CyberKit2MeleeSource();CyberLordFeed.Kit2MeleeSource=v2?src:null;var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var rec=src.For(1);
            int n=v2?5:3;var at=new float[n];float t0=.5f;for(int i=0;i<n;i++){at[i]=t0;t0+=(v2?CyberKit2Spec.ComboSeconds[i]:CyberLordSpec.ComboDurations[i])/rate;}
            float Hit(int i)=>at[i]+(v2?CyberKit2Spec.ComboWindups[i]:CyberLordSpec.ComboWindups[i])/rate;
            float End(int i)=>at[i]+(v2?CyberKit2Spec.ComboSeconds[i]:CyberLordSpec.ComboDurations[i])/rate;
            int Kind(int i)=>v2?CyberCombo.V2Kind(i):CyberCombo.V1Kind(i);
            void Cast(float t){for(int i=n-1;i>=0;i--)if(t>=at[i]){if(v2){if(rec.At[CyberKit2Spec.MeleeLmb]!=at[i])CyberKit2MeleeFixture.Combo(rec,i,at[i],rate);}else if(a.AttackAt!=at[i])CyberLordFixture.Combo(a,i,at[i]);break;}}
            var rig=new Rig();rig.Cy.Reduced=reduced;var states=new List<string>();var trace=new List<Pose>();
            for(float t=0;t<t0+1.2f;t+=Dt){Cast(t);var st=rig.Step(a,t,Dt);if(states.Count==0||states[^1]!=st)states.Add(st);Check(rig.Finite(),tag+": a finite pose at "+t.ToString("0.000"));trace.Add(rig.Capture(t));}
            var want=Enumerable.Range(0,n).Select(i=>CyberCombo.States[Kind(i)]).ToArray();
            Check(want.All(states.Contains)&&states.IndexOf(want[0])<states.IndexOf(want[n-1]),tag+" plays every step in order ("+string.Join(",",states)+")");
            // continuity: no bone pops between frames, the fists never jump
            float fistStep=0,boneTurn=0,boneShift=0,turnAt=0;int turnBone=-1;
            for(int k=1;k<trace.Count;k++){fistStep=Math.Max(fistStep,Math.Max((trace[k].LF-trace[k-1].LF).Length(),(trace[k].RF-trace[k-1].RF).Length()));var (tu,sh)=BoneStep(trace[k].Snap,trace[k-1].Snap,out int wb);if(tu>boneTurn){boneTurn=tu;turnAt=trace[k].T;turnBone=wb;}boneShift=Math.Max(boneShift,sh);}
            if(v2&&rate==1&&!reduced){baseTurn=boneTurn;baseShift=boneShift;for(int k=1;k<trace.Count;k++)baseAnkle=Math.Max(baseAnkle,Math.Max((trace[k].LA-trace[k-1].LA).Length(),(trace[k].RA-trace[k-1].RA).Length()));}
            // (the bounds scale with the attack rate: the Overclock compresses the same motion into fewer frames)
            Check(fistStep<.62f*rate&&boneTurn<2.4f*rate&&boneShift<.62f*rate,tag+": the string is continuous (per 60 Hz frame a fist moves at most "+fistStep.ToString("0.000")+" m, a bone turns "+boneTurn.ToString("0.000")+" rad (bone "+turnBone+" at "+turnAt.ToString("0.000")+" s) and shifts "+boneShift.ToString("0.000")+" m)");
            // each strike's apex on its replicated hit (within a frame and a half; the windows compress with the Overclock)
            for(int i=0;i<n;i++)
            {
                int kind=Kind(i);float from=at[i],to=i+1<n?at[i+1]:at[i]+.5f;var seg=trace.Where(x=>x.T>=from&&x.T<to).ToList();var name=tag+" "+CyberCombo.Techniques[kind];
                Pose At(float t)=>seg.OrderBy(x=>Math.Abs(x.T-t)).First();
                bool Peak(Func<Pose,float> f,float min,out Pose best){best=seg.OrderByDescending(f).First();return Math.Abs(best.T-Hit(i))<=1.5f*Dt+1e-4f&&f(best)>min;}
                // a kick arrives at its full extension on the hit and holds it (the frame it first comes within 3 cm of its furthest)
                bool Arrive(Func<Pose,float> f,float min,out Pose first){float top=seg.Max(f);first=seg.First(x=>f(x)>=top-.03f);return Math.Abs(first.T-Hit(i))<=1.5f*Dt+1e-4f&&top>min;}
                switch(kind)
                {
                    case CyberCombo.Jab:Check(Peak(x=>x.LF.Z,.7f,out var j),name+": the lead fist's reach peaks on the hit ("+j.T.ToString("0.000")+" vs "+Hit(i).ToString("0.000")+", "+j.LF.Z.ToString("0.00")+" m)");break;
                    case CyberCombo.Cross:
                    {
                        Check(Arrive(x=>x.RA.Z,.35f,out var r),name+": the rear foot reaches full extension on the hit ("+r.T.ToString("0.000")+" vs "+Hit(i).ToString("0.000")+", "+r.RA.Z.ToString("0.00")+" m out)");
                        var h=At(Hit(i));Check(h.RA.Y>MeleeRig.FLOOR+.45f&&h.LA.Y<MeleeRig.FLOOR+.05f,name+": kicked at belt height off the planted lead foot ("+(h.RA.Y-MeleeRig.FLOOR).ToString("0.00")+" m up)");break;
                    }
                    case CyberCombo.Spin:
                    {
                        Check(Arrive(x=>x.RA.Z,.35f,out var r),name+": the rear heel reaches full extension toward the front on the hit ("+r.T.ToString("0.000")+" vs "+Hit(i).ToString("0.000")+", "+r.RA.Z.ToString("0.00")+" m out)");
                        var h=At(Hit(i));Check(h.RA.Y>MeleeRig.FLOOR+.4f&&h.LA.Y<MeleeRig.FLOOR+.05f,name+": kicked off the pivoting lead foot ("+(h.RA.Y-MeleeRig.FLOOR).ToString("0.00")+" m up)");break;
                    }
                    case CyberCombo.Rise:
                    {
                        var h=At(Hit(i));var prev=seg[Math.Max(0,seg.IndexOf(h)-1)];var top=seg.OrderByDescending(x=>x.RF.Y).First();
                        Check(h.RF.Y>prev.RF.Y&&h.RF.Y>.3f&&top.T>=h.T-Dt&&top.T<=Hit(i)+.12f,name+": the uppercut is climbing through its hit and peaks just after ("+h.RF.Y.ToString("0.00")+" m, top at "+top.T.ToString("0.000")+")");
                        var knee=seg.OrderByDescending(x=>x.RK.Y).First();Check(knee.T>=Hit(i)-1.5f*Dt&&knee.T<=Hit(i)+.08f&&knee.RK.Y>-.2f,name+": the jumping knee is up at the hit ("+knee.RK.Y.ToString("0.00")+" m at "+knee.T.ToString("0.000")+")");break;
                    }
                    default:Check(Peak(x=>(x.LF.Z+x.RF.Z)*.5f,.7f,out var p),name+": both palms reach furthest on the hit ("+p.T.ToString("0.000")+" vs "+Hit(i).ToString("0.000")+")");break;
                }
                // the striking leg drawn on its keys on the hit's first frame (the reach the keys ask for is reachable)
                int leg=CyberCombo.Leg[kind];
                if(CyberCombo.Kicks(kind)||kind==CyberCombo.Rise)
                {
                    var sw=new CyberCombo.Swing(kind,at[i],Hit(i),End(i));var h=seg.First(x=>x.T>=Hit(i)-1e-4f);var scratch=new float[CyberCombo.N];CyberCombo.Sample(sw,h.T,scratch);var keyed=CyberCombo.KickAnkle(scratch);float off=Vector3.Distance(keyed,leg==0?h.LA:h.RA);
                    Check(off<.04f,name+": the striking leg is drawn on its keyed path at the hit ("+(off*100).ToString("0.0")+" cm off: keyed "+keyed+" drawn "+(leg==0?h.LA:h.RA)+" knee "+(leg==0?h.LK:h.RK)+" body "+h.Body+")");
                }
                // only the striking leg leaves the floor (the jab's and the palm's steps, the launcher's hop are low); every foot lands by the window's end
                var win=trace.Where(x=>x.T>=at[i]&&x.T<End(i)).ToList();
                float maxL=win.Max(x=>x.LA.Y)-MeleeRig.FLOOR,maxR=win.Max(x=>x.RA.Y)-MeleeRig.FLOOR;
                switch(kind)
                {
                    case CyberCombo.Cross:Check(maxR>.45f&&maxL<.05f,name+": only the kicking leg lifts (rear "+maxR.ToString("0.00")+" m, lead "+maxL.ToString("0.000")+" m)");break;
                    case CyberCombo.Spin:Check(maxR>.4f&&maxL<.05f,name+": only the kicking leg lifts (rear "+maxR.ToString("0.00")+" m, lead "+maxL.ToString("0.000")+" m)");break;
                    case CyberCombo.Rise:Check(maxR>.35f&&maxL<.11f,name+": the knee drives up, the lead foot only hops (rear "+maxR.ToString("0.00")+" m, lead "+maxL.ToString("0.000")+" m)");break;
                    case CyberCombo.Palm:Check(maxL>.1f&&maxR<.05f,name+": the lead knee rises for the stomp, the rear foot stays planted (lead "+maxL.ToString("0.00")+" m, rear "+maxR.ToString("0.000")+" m)");break;
                    default:Check(maxL<.07f&&maxR<.07f,name+": stepping in, both feet stay low ("+maxL.ToString("0.000")+", "+maxR.ToString("0.000")+" m)");break;
                }
                var end=trace.Last(x=>x.T<=End(i)+1e-4f);
                Check(end.LA.Y<MeleeRig.FLOOR+.006f&&end.RA.Y<MeleeRig.FLOOR+.006f&&Math.Min(end.Sole[0].Y,Math.Min(end.Sole[1].Y,end.Sole[2].Y))<Floor+.012f&&Math.Min(end.Sole[3].Y,Math.Min(end.Sole[4].Y,end.Sole[5].Y))<Floor+.012f,name+": both feet are down by the window's end ("+(end.LA.Y-MeleeRig.FLOOR).ToString("0.000")+", "+(end.RA.Y-MeleeRig.FLOOR).ToString("0.000")+" m)");
            }
            // the feet: a foot on the floor never slides along it (its ball stays put, flat or pivoting on it); nothing goes
            // through the floor
            float slide=0,lowest=1,lowAnkle=1,slideAt=0;string slideFoot="";
            for(int k=1;k<trace.Count;k++)foreach(bool left in new[]{true,false})
            {
                var p=left?trace[k-1].LB:trace[k-1].RB;var q=left?trace[k].LB:trace[k].RB;
                if(OnFloor(p)&&OnFloor(q)){float d=new Vector2(q.X-p.X,q.Z-p.Z).Length();if(d>slide){slide=d;slideAt=trace[k].T;slideFoot=left?"lead":"rear";}}
            }
            foreach(var x in trace){lowest=Math.Min(lowest,x.Sole.Min(p=>p.Y)-Floor);lowAnkle=Math.Min(lowAnkle,Math.Min(x.LA.Y,x.RA.Y)-MeleeRig.FLOOR);}
            worstSlide=Math.Max(worstSlide,slide);
            Check(slide<.002f,tag+": no planted foot slides (at most "+(slide*1000).ToString("0.0")+" mm per frame, the "+slideFoot+" at "+slideAt.ToString("0.000")+" s)");
            Check(lowest>-.006f&&lowAnkle>-1e-3f,tag+": no foot goes through the floor (lowest sole point "+(lowest*1000).ToString("0.0")+" mm, ankle "+(lowAnkle*1000).ToString("0.0")+" mm)");
            // the drawn body stays near the capsule (small step-ins and hops) and is back over it once the string has settled
            float bodyOff=trace.Max(x=>new Vector2(x.Body.X,x.Body.Z).Length());var settled=trace.Last();
            Check(bodyOff<.11f&&new Vector2(settled.Body.X,settled.Body.Z).Length()<.03f,tag+": the drawn body stays within "+(bodyOff*100).ToString("0.0")+" cm of the capsule and returns ("+(new Vector2(settled.Body.X,settled.Body.Z).Length()*100).ToString("0.0")+" cm)");
            // the lead foot steps in on the jab (real footwork, not a stance hold)
            var jab=trace.First(x=>x.T>=Hit(0)+.05f);Check(jab.LA.Z-trace[0].LA.Z>.1f,tag+": the jab steps the lead foot in ("+(jab.LA.Z-trace[0].LA.Z).ToString("0.000")+" m)");
            // the glitch-step's frame-skip: the jab's windup holds and snaps (never with reduced effects, which move every frame)
            if(rate==1)
            {
                var wind=trace.Where(x=>x.T>at[0]+1e-4f&&x.T<Hit(0)-1e-4f).ToList();int held=0;for(int k=1;k<wind.Count;k++)if((wind[k].LF-wind[k-1].LF).Length()<.002f)held++;
                Check(reduced?held==0:held>=1,tag+": the jab's windup "+(reduced?"runs smooth":"holds and snaps (frame-skip)")+" ("+held+" held frames of "+wind.Count+")");
            }
            // uneven and rewound clocks: finite throughout, and a rewound clock poses as a fresh rig does once it settles
            var uneven=new Rig();uneven.Cy.Reduced=reduced;float tt=0;int f=0;var fresh=new Rig();fresh.Cy.Reduced=reduced;
            while(tt<t0+1){Cast(tt);uneven.Step(a,tt,f%3==0?1/24f:f%3==1?1/144f:1/60f);Check(uneven.Finite(),"finite on an uneven clock");tt+=f%3==0?1/24f:f%3==1?1/144f:1/60f;f++;}
            float back=at[1]+.03f;for(float t=back-.6f;t<back;t+=Dt){Cast(t);fresh.Step(a,t,Dt);}
            for(float t=back;t<back+.12f;t+=Dt){Cast(t);uneven.Step(a,t,Dt);fresh.Step(a,t,Dt);Check(uneven.Finite(),"finite after a rewind");}
            // (the upper body exactly, over the body; the feet start the swing over from where the gait stood them, so the legs,
            // and the body's sink to keep a planted foot in reach, may differ)
            var (rt,rs)=BoneStep(uneven.Snapshot(),fresh.Snapshot(),CapturedRig.Spine,CapturedRig.RHand);Vector3 ub=uneven.Mo.Rig.P[CapturedRig.Body],fb=fresh.Mo.Rig.P[CapturedRig.Body];
            float ft=Math.Max(Vector3.Distance(uneven.Fist(true)-ub,fresh.Fist(true)-fb),Vector3.Distance(uneven.Fist(false)-ub,fresh.Fist(false)-fb));
            Check(rt<.05f&&ft<.02f,tag+": a rewound clock poses the upper body as a fresh rig does within .12 s ("+rt.ToString("0.000")+" rad, fists "+ft.ToString("0.000")+" m)");
            if(v2){var ra=new Rig();var rb=new Rig();ra.Cy.Reduced=rb.Cy.Reduced=reduced;for(float t=0;t<t0;t+=Dt){Cast(t);ra.Step(a,t,Dt);rb.Step(a.ViewCopy(),t,Dt);}Check(MaxDiff(ra.Snapshot(),rb.Snapshot())==0,tag+": two rigs of one replicated string pose identically");}
            strings+=(strings.Length>0?"; ":"")+tag+" "+n+" steps, fists <= "+fistStep.ToString("0.00")+" m/frame, feet <= "+(slide*1000).ToString("0.0")+" mm/frame, body <= "+(bodyOff*100).ToString("0")+" cm";
            CyberLordFeed.Kit2MeleeSource=null;
        }
        // chained at any interval, reset past the .7 s idle, cut by an interrupt: no new pops. The steps' starts are moved apart
        // by 0 .. .69 s (each gap a new chain), then a .75 s pause restarts at the jab, then the roundhouse is cut at its hit.
        {
            var src=new CyberKit2MeleeSource();CyberLordFeed.Kit2MeleeSource=src;var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var rec=src.For(1);
            var plan=new List<(int step,float at)>();float t=.5f;
            foreach(float gap in new[]{0,.04f,.12f,.3f,.69f}){for(int i=0;i<5;i++){plan.Add((i,t));t+=CyberKit2Spec.ComboSeconds[i]+(i<4?gap:.75f);}}
            plan.Add((0,t));t+=CyberKit2Spec.ComboSeconds[0];float cutAt=t+CyberKit2Spec.ComboWindups[1];plan.Add((1,t));t+=1.2f;
            var rig=new Rig();float worstTurn=0,worstShift=0,worstFoot=0,worstAt=0,shiftAt=0,footAt=0;int worstBone=-1;Pose prev=null;
            for(float s=0;s<t;s+=Dt)
            {
                for(int i=plan.Count-1;i>=0;i--)if(s>=plan[i].at){if(rec.At[CyberKit2Spec.MeleeLmb]!=plan[i].at)CyberKit2MeleeFixture.Combo(rec,plan[i].step,plan[i].at);break;}
                if(s>=cutAt&&rec.CutAt[CyberKit2Spec.MeleeLmb]<cutAt-1e-3f)rec.CutAt[CyberKit2Spec.MeleeLmb]=cutAt;
                rig.Step(a,s,Dt);Check(rig.Finite(),"finite through chains, resets and a cut");var cur=rig.Capture(s);
                if(prev!=null)
                {
                    var (tu,sh)=BoneStep(cur.Snap,prev.Snap,out int wb);float foot=Math.Max((cur.LA-prev.LA).Length(),(cur.RA-prev.RA).Length());
                    if(tu>worstTurn){worstTurn=tu;worstAt=s;worstBone=wb;}if(sh>worstShift){worstShift=sh;shiftAt=s;}if(foot>worstFoot){worstFoot=foot;footAt=s;}
                    if(OnFloor(prev.LB)&&OnFloor(cur.LB))Check(new Vector2(cur.LB.X-prev.LB.X,cur.LB.Z-prev.LB.Z).Length()<.002f,"the planted lead foot holds through chains and resets at "+s.ToString("0.000"));
                    if(OnFloor(prev.RB)&&OnFloor(cur.RB))Check(new Vector2(cur.RB.X-prev.RB.X,cur.RB.Z-prev.RB.Z).Length()<.002f,"the planted rear foot holds through chains and resets at "+s.ToString("0.000"));
                }
                prev=cur;
            }
            // (no pop: nothing moves faster in a frame than the string chained at its own pace already does)
            Check(worstTurn<=baseTurn*1.15f+.02f&&worstShift<=baseShift*1.15f+.02f&&worstFoot<=baseAnkle*1.15f+.02f,"chains at 0 .. .69 s, a reset past .7 s and a cut kick pop nothing: a bone turns at most "+worstTurn.ToString("0.000")+" rad (bone "+worstBone+" at "+worstAt.ToString("0.000")+" s; the string's own "+baseTurn.ToString("0.000")+") and shifts "+worstShift.ToString("0.000")+" m (at "+shiftAt.ToString("0.000")+" s; "+baseShift.ToString("0.000")+"), an ankle "+worstFoot.ToString("0.000")+" m (at "+footAt.ToString("0.000")+" s; "+baseAnkle.ToString("0.000")+") per frame");
            var last=rig.Capture(t);Check(last.LA.Y<MeleeRig.FLOOR+.006f&&last.RA.Y<MeleeRig.FLOOR+.006f,"the cut kick's leg is back on the floor after its settle");
            CyberLordFeed.Kit2MeleeSource=null;
        }
        yield return $"Cyber Lord hardlight martial string: {count-c0} checks ({strings}; every apex on its replicated hit; only the striking leg lifts; no slide or floor penetration; chains, resets and a cut; uneven and rewound clocks)";
        // ---- 3b. the string's effects on the striking limb, reduced effects (no strobe) and the first-person view model ----
        c0=count;
        {
            var src=new CyberKit2MeleeSource();CyberLordFeed.Kit2MeleeSource=src;var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var rec=src.For(1);
            var at=Steps(.5f,5,1);float end=at[4]+CyberKit2Spec.ComboSeconds[4]+.4f;float Hit(int i)=>at[i]+CyberKit2Spec.ComboWindups[i];
            void Cast(float t){for(int i=4;i>=0;i--)if(t>=at[i]){if(rec.At[CyberKit2Spec.MeleeLmb]!=at[i])CyberKit2MeleeFixture.Combo(rec,i,at[i]);break;}}
            var rig=new Rig();var scene=new RigScene(rig);scene.List.Add(a);var fx=new NativeCyberLordFx();var calm=new NativeCyberLordFx();var near=new NearSink();var quiet=new NearSink();
            var ghosts=new List<float>();var calmGhosts=new List<float>();bool[] seen=new bool[5];float stompNear=1;int fistInKick=0;
            for(float t=0;t<end;t+=Dt)
            {
                Cast(t);rig.Step(a,t,Dt);var p=rig.Capture(t);
                near.Reset();fx.Draw(near,scene,t,1,false);float g=0;for(int i=0;i<fx.Ghosts;i++){var h=fx.Ghost(i);if(h.Kind==CyberGhost.Split&&h.Actor==1)g+=h.Opacity;}ghosts.Add(g);
                quiet.Reset();calm.Draw(quiet,scene,t,.3f,false);float c=0;for(int i=0;i<calm.Ghosts;i++){var h=calm.Ghost(i);if(h.Kind==CyberGhost.Split&&h.Actor==1){c+=h.Opacity;Check(h.Offset.Length()<.13f,"reduced splits stay small");}}calmGhosts.Add(c);
                for(int i=0;i<5;i++)
                {
                    if(seen[i]||t<Hit(i)-1e-4f)continue;seen[i]=true;int kind=CyberCombo.V2Kind(i);string name=CyberCombo.Techniques[kind];
                    if(CyberCombo.Kicks(kind))
                    {
                        var foot=World(CyberCombo.Leg[kind]==0?p.LA:p.RA);float d=near.Nearest(foot);
                        Check(d<.12f,name+": the hardlight trail rides the kicking foot on the hit ("+(d*100).ToString("0.0")+" cm from the drawn ankle)");
                        // (no fist ribbon on a kick: nothing is drawn round the fists but their aura)
                        if(near.Nearest(World(p.LF))<.05f&&near.Nearest(World(p.RF))<.05f)fistInKick++;
                    }
                    else if(kind==CyberCombo.Jab){float d=near.Nearest(World(p.LF));Check(d<.12f,name+": the hardlight trail rides the lead fist on the hit ("+(d*100).ToString("0.0")+" cm)");}
                    else if(kind==CyberCombo.Rise){float d=near.Nearest(World(p.RF));Check(d<.15f,name+": the climbing crescent and smear ride the uppercut ("+(d*100).ToString("0.0")+" cm)");}
                }
                if(t>=Hit(4)+.04f&&t<Hit(4)+.1f)stompNear=Math.Min(stompNear,near.NearestVoxel(World(p.LA)-Vector3.UnitY*.07f));
            }
            Check(seen.All(x=>x)&&fistInKick==0,"every step's effects drawn on their limb");
            Check(stompNear<.45f,"the palm's stomp blocks pop round the lead foot where it lands ("+(stompNear*100).ToString("0")+" cm)");
            // the RGB hold on each impact (and the jab's step-in afterimages); with reduced effects small and steady: each hold
            // rises once and fades (no flicker), and it is fainter
            int Holds(List<float> o){int n=0;for(int k=1;k<o.Count;k++)if(o[k]>.005f&&o[k-1]<=.005f)n++;return n;}
            int Flicker(List<float> o){int n=0;for(int k=2;k<o.Count;k++){float d1=o[k-1]-o[k-2],d2=o[k]-o[k-1];if(d1<-.004f&&d2>.004f)n++;}return n;}
            Check(Holds(ghosts)>=5&&ghosts.Max()>.2f,"the impacts hold an RGB split ("+Holds(ghosts)+" holds, opacity up to "+ghosts.Max().ToString("0.00")+")");
            Check(Holds(calmGhosts)>=5&&calmGhosts.Max()<ghosts.Max()*.6f&&Flicker(calmGhosts)<=5,"reduced effects: the splits are fainter ("+calmGhosts.Max().ToString("0.00")+") and never strobe ("+Flicker(calmGhosts)+" re-brightenings for 5 holds)");
            // first person: the view model only (no legs, no root: the camera never moves); punches and palms drive the striking
            // hand out, kicks keep the lead hand guarding while the rear hand counter-swings, the spin keeps both hands in view
            var fp=new CyberLordMotion();var pose=new CapturedRig.Pose();float[] prevSnap=null;float fpTurn=0;Vector3 V(int b)=>pose.WorldPosition[b];
            var fpl=new List<(float t,Vector3 l,Vector3 r)>();Vector3 guard=Vector3.Zero;
            for(float t=0;t<end;t+=Dt)
            {
                Cast(t);fp.FirstPerson(pose,a,t);pose.World();var snap=new float[22*4];for(int b=0;b<22;b++){var q=pose.WorldRotation[b];snap[b*4]=q.X;snap[b*4+1]=q.Y;snap[b*4+2]=q.Z;snap[b*4+3]=q.W;}
                if(prevSnap!=null)for(int b=0;b<22;b++){float d=Math.Abs(snap[b*4]*prevSnap[b*4]+snap[b*4+1]*prevSnap[b*4+1]+snap[b*4+2]*prevSnap[b*4+2]+snap[b*4+3]*prevSnap[b*4+3]);fpTurn=Math.Max(fpTurn,2*(float)Math.Acos(Math.Min(1,d)));}
                prevSnap=snap;for(int b=0;b<22;b++)Check(float.IsFinite(V(b).X)&&float.IsFinite(V(b).Y)&&float.IsFinite(V(b).Z),"a finite first-person pose");
                if(t<at[0]-.1f)guard=V(CapturedRig.LHand);fpl.Add((t,V(CapturedRig.LHand),V(CapturedRig.RHand)));
            }
            for(int i=0;i<5;i++)
            {
                int kind=CyberCombo.V2Kind(i);string name="first person "+CyberCombo.Techniques[kind];var h=fpl.First(x=>x.t>=Hit(i)-1e-4f);var wind=fpl.Where(x=>x.t>=at[i]-1e-4f&&x.t<Hit(i)).ToList();
                float lDrive=h.l.Z-wind.Min(x=>x.l.Z),rDrive=h.r.Z-wind.Min(x=>x.r.Z);
                switch(kind)
                {
                    case CyberCombo.Jab:Check(lDrive>.06f,name+": the lead hand drives out from its windup ("+lDrive.ToString("0.00")+" m)");break;
                    case CyberCombo.Cross:Check(h.l.Y>h.r.Y+.15f,name+": the lead hand guards high as the rear hand counter-swings down ("+h.l.Y.ToString("0.00")+" / "+h.r.Y.ToString("0.00")+")");break;
                    case CyberCombo.Spin:Check(h.l.Z>0&&h.r.Z>0,name+": both hands stay in front of the view, guarding ("+h.l.Z.ToString("0.00")+", "+h.r.Z.ToString("0.00")+")");break;
                    case CyberCombo.Rise:Check(h.r.Y>wind.Min(x=>x.r.Y)+.1f,name+": the rear hand rises up through the view ("+(h.r.Y-wind.Min(x=>x.r.Y)).ToString("0.00")+" m)");break;
                    default:Check(lDrive>.06f&&rDrive>.06f,name+": both palms drive out from the chamber ("+lDrive.ToString("0.00")+", "+rDrive.ToString("0.00")+" m)");break;
                }
            }
            Check(fpTurn<2.4f,"first person: the view model never whirls (a bone turns at most "+fpTurn.ToString("0.000")+" rad per frame)");
            CyberLordFeed.Kit2MeleeSource=null;
        }
        yield return $"Cyber Lord martial string effects and views: {count-c0} checks (trails on the kicking foot and the striking fists, the palm's stomp at its foot, RGB holds steady and fainter with reduced effects, the first-person view model's drive, guard and counter-swing)";

        // ---- 4. the abilities: poses, effects, HUD, overlays, sounds ----
        c0=count;
        {
            var src=new CyberKit2MeleeSource();CyberLordFeed.Kit2MeleeSource=src;var a=CyberLordFixture.Pilot(1,Vector3.Zero,0);var dummy=CyberLordFixture.Pilot(2,Dummy,180);dummy.SourceSpec="sword";
            var other=CyberLordFixture.Pilot(3,new Vector3(3,0,5),220);other.SourceSpec="sword";var r=src.For(1);var events=Events();
            var rig=new Rig();var seen=new HashSet<string>();var fx=new NativeCyberLordFx();var fxLow=new NativeCyberLordFx();var sink=new Sink();var low=new Sink();var scene=new Scene{LocalId=1};scene.List.AddRange(new[]{a,dummy,other});
            var view=new CyberLordView();var hashes=new List<double>();int fullCount=0,lowCount=0,forkGhosts=0,flats=0;string flatWhat=null;
            var cloneRig=new Rig();var cloneActor=new Actor();var cloneStates=new HashSet<string>();Pose cloneLast=null;float cloneSlide=0,cloneKick=0,cloneLow=1;
            var cardStates=new HashSet<string>[5];for(int i=0;i<5;i++)cardStates[i]=new HashSet<string>();float edge=0,flashMe=0,flashMarked=0;var o=new NativeKitSound[NativeCyberLordSounds.Max];var cues=new HashSet<string>();
            for(float t=0;t<EndT;t+=Dt)
            {
                Write(r,t);seen.Add(rig.Step(a,t,Dt));Check(rig.Finite(),"a finite pose at "+t.ToString("0.000"));
                fx.Observe(events,t);sink.Reset();fx.Draw(sink,scene,t,1,false);hashes.Add(sink.Hash);fullCount+=sink.Count;flats+=sink.Flat;flatWhat??=sink.FlatWhat;sink.Flat=0;
                for(int g=0;g<fx.Ghosts;g++)if(fx.Ghost(g).Kind==CyberGhost.Fork)forkGhosts++;
                // the first clone posed on a rig of its own from its stand-in record (the caster's string with the clone's lag)
                for(int g=0;g<fx.Ghosts;g++){var gh=fx.Ghost(g);if(gh.Kind!=CyberGhost.Fork||gh.Index!=0)continue;NativeCyberLordFx.ForkActor(cloneActor,a,gh);cloneStates.Add(cloneRig.Step(cloneActor,t,Dt));Check(cloneRig.Finite(),"a finite clone pose");
                    var cp=cloneRig.Capture(t);if(cloneLast!=null)foreach(bool left in new[]{true,false}){var p0=left?cloneLast.LB:cloneLast.RB;var p1=left?cp.LB:cp.RB;if(OnFloor(p0)&&OnFloor(p1))cloneSlide=Math.Max(cloneSlide,new Vector2(p1.X-p0.X,p1.Z-p0.Z).Length());}
                    if(t>=ForkComboT&&t<BurstT){cloneKick=Math.Max(cloneKick,cp.RA.Y-MeleeRig.FLOOR);cloneLow=Math.Min(cloneLow,cp.Sole.Min(q=>q.Y)-Floor);}cloneLast=cp;break;}
                fxLow.Observe(events,t);low.Reset();fxLow.Draw(low,scene,t,.3f,true);lowCount+=low.Count;flats+=low.Flat;low.Flat=0;
                CyberLordFeed.Read(a,view);
                for(int i=0;i<5;i++){Check(CyberKit2MeleeHud.Replaces(view,i),"the v2 card replaces v1's in slot "+i);var card=CyberKit2MeleeHud.Of(view,i,t,t>CutT-.5f&&t<CutT?5:100);cardStates[i].Add(card.State.EndsWith("s")&&card.State.Contains('.')?"COOLDOWN":card.State);Check(float.IsFinite(card.Dial)&&card.Dial>=0&&card.Dial<=1,"a card dial in range");}
                Check(!CyberKit2MeleeHud.Replaces(view,CyberLordSpec.Four),"Kernel Panic keeps its card");
                edge=Math.Max(edge,CyberKit2MeleeHud.EdgeGlitch(view,t,false));flashMe=Math.Max(flashMe,NativeCyberLordFx.FreezeFlash(view,1,t,false));flashMarked=Math.Max(flashMarked,NativeCyberLordFx.FreezeFlash(view,2,t,false));
                Check(NativeCyberLordFx.FreezeFlash(view,3,t,false)==0,"an unmarked bystander sees no freeze flash");
                if(t<OverT)Check(CyberKit2MeleeHud.EdgeGlitch(view,t,false)==0,"no edge glitch before the Overclock");
                for(int s=0;s<CyberKit2MeleeSounds.Slots;s++){int n=CyberKit2MeleeSounds.State(view,s,o,out float when);if(n>0&&Math.Abs(t-when)<Dt*.5f)for(int k=0;k<n;k++)cues.Add(o[k].Key);}
            }
            foreach(var st in CyberKit2MeleePoses.States.Concat(CyberCombo.States))Check(seen.Contains(st),"the timeline shows "+st+" ("+string.Join(",",seen)+")");
            // first person: the local pilot's own string and casts draw on the view layer, finite
            {scene.ViewerId=1;var fp=new NativeCyberLordFx();var vs=new Sink();int drawn=0;for(float t=ComboT;t<EndT;t+=Dt){Write(r,t);fp.Observe(events,t);vs.Reset();fp.Draw(vs,scene,t,1,false);drawn+=vs.Count;}scene.ViewerId=0;Check(drawn>0,"the first-person view draws the string and casts");}
            Check(flats==0,"nothing glows flat on the floor ("+flatWhat+")");
            Check(forkGhosts>0&&lowCount<fullCount,"the clones draw as ghosts; the low tier draws less ("+lowCount+" < "+fullCount+")");
            Check(CyberCombo.States.All(cloneStates.Contains)&&cloneKick>.4f&&cloneSlide<.002f&&cloneLow>-.006f,"a fork clone copies the whole martial string on its own rig, kicks included ("+string.Join(",",cloneStates.Where(x=>x.StartsWith("cyber-")))+"; rear foot up to "+cloneKick.ToString("0.00")+" m, planted feet slide <= "+(cloneSlide*1000).ToString("0.0")+" mm, lowest sole "+(cloneLow*1000).ToString("0.0")+" mm)");
            Check(edge>.5f&&flashMe>.5f&&flashMarked>.5f,"the Overclock's edge glitch and the Glitch Cut's freeze flash (caster and marked) show");
            {var rv=new CyberLordView();CyberLordFeed.Read(a,rv);float red=0;for(float t=CutT;t<CutT+1.2f;t+=Dt)red=Math.Max(red,NativeCyberLordFx.FreezeFlash(rv,1,t,true));Check(red<flashMe,"reduced effects soften the freeze flash");}
            foreach(var (i,want) in new[]{(0,"ACTIVE"),(1,"ACTIVE"),(2,"ACTIVE"),(3,"ACTIVE"),(4,"ACTIVE"),(0,"COOLDOWN"),(2,"READY")})Check(cardStates[i].Contains(want),"card "+CyberKit2MeleeHud.Labels[i]+" shows "+want+" ("+string.Join(",",cardStates[i])+")");
            Check(cardStates[3].Contains("LOW MANA")||cardStates[3].Contains("COOLDOWN"),"a card dims for mana or a cooldown");
            // deterministic from the view: a second presenter, fed copies of the record and the actors, draws the same
            {
                var src2=new CyberKit2MeleeSource();var fx2=new NativeCyberLordFx();var s2=new Sink();var sc2=new Scene{LocalId=1};var a2=a.ViewCopy();sc2.List.AddRange(new[]{a2,dummy.ViewCopy(),other.ViewCopy()});int k=0;double worst=0;
                var r2=new CyberKit2MeleeRecord();CyberKit2Melee.Clones.Clear();
                for(float t=0;t<EndT;t+=Dt,k++){Write(r2,t);var copy=r2.ViewCopy();src2.Remove(1);var slot=src2.For(1);Copy(copy,slot);CyberLordFeed.Kit2MeleeSource=src2;fx2.Observe(events,t);s2.Reset();fx2.Draw(s2,sc2,t,1,false);worst=Math.Max(worst,Math.Abs(s2.Hash-hashes[k]));}
                Check(worst<1e-6,"the effects are a pure function of the view: a second presenter on copied records draws the same ("+worst+")");
                CyberLordFeed.Kit2MeleeSource=src;
            }
            // no allocation per frame (effects, poses, HUD, overlays, sounds) once warmed up
            {
                void Frame(float t){Write(r,t);rig.Step(a,t,Dt);fx.Observe(events,t);sink.Reset();fx.Draw(sink,scene,t,1,false);CyberLordFeed.Read(a,view);for(int i=0;i<5;i++)CyberKit2MeleeHud.Of(view,i,t,100);CyberKit2MeleeHud.EdgeGlitch(view,t,false);NativeCyberLordFx.FreezeFlash(view,2,t,false);for(int s=0;s<CyberKit2MeleeSounds.Slots;s++)CyberKit2MeleeSounds.State(view,s,o,out _);}
                for(float t=0;t<EndT;t+=Dt)Frame(t);
                long before=GC.GetAllocatedBytesForCurrentThread();for(float t=0;t<EndT;t+=Dt)Frame(t);long bytes=GC.GetAllocatedBytesForCurrentThread()-before;
                Check(bytes==0,"a 60 Hz playthrough of the v2 timeline allocates "+bytes+" bytes");
            }
            // sounds: existing bank keys and shipped fallback clips only; every ability makes its cue
            var clips=new HashSet<string>(Directory.GetFiles(Path.Combine(root,"Assets/Conversion/Resources/Audio/audio"),"*.wav").Select(Path.GetFileNameWithoutExtension));
            foreach(var key in CyberKit2MeleeSounds.Keys)Check(NativeSoundKeys.Known(key),"bank key "+key+" exists");
            foreach(var key in new[]{"magic.shackles","move.roll","chip.delayed.charge","chip.split","magic.blink-strike","blast.shock","move.jump","blast.meteor"})Check(cues.Contains(key),"the timeline cues "+key);
            foreach(var e in events){var h=CyberKit2MeleeFeed.Hit(e.Detail);int n=h==Kit2Hit.None?0:CyberKit2MeleeSounds.Event(e.Kind,h,false,o);for(int k=0;k<n;k++)Check(CyberKit2MeleeSounds.Keys.Contains(o[k].Key)&&NativeCyberLordSounds.Clips.Contains(o[k].Clip)&&clips.Contains(o[k].Clip)&&o[k].Gain>0&&o[k].Gain<=1,"event cue "+o[k].Key);}
            for(int s=0;s<CyberKit2MeleeSounds.Slots;s++){var v=new CyberLordView();CyberLordFeed.Read(a,v);int n=CyberKit2MeleeSounds.State(v,s,o,out _);for(int k=0;k<n;k++)Check(NativeCyberLordSounds.Clips.Contains(o[k].Clip)&&clips.Contains(o[k].Clip),"state cue clip "+o[k].Clip+" ships");}
            // the cards' art icons
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json"))))
            {
                var art=new HashSet<string>(doc.RootElement.GetProperty("icons").EnumerateArray().Select(i=>i.GetProperty("id").GetString()));
                foreach(int s in new[]{CyberKit2Spec.Q,CyberKit2Spec.E,CyberKit2Spec.MeleeLmb,CyberKit2Spec.Fork,CyberKit2Spec.Cut,CyberKit2Spec.Meteor})Check(art.Contains(CyberKit2Spec.Icons[s]),"icon "+CyberKit2Spec.Icons[s]+" is in the class icon art");
            }
            Check(CyberKit2MeleeHud.Labels.SequenceEqual(new[]{"Q · UPLINK","E · OVERCLOCK","1 · FORK BOMB","2 · GLITCH CUT","3 · METEOR BYTE"}),"the cards' labels ("+string.Join(", ",CyberKit2MeleeHud.Labels)+")");
            CyberLordFeed.Kit2MeleeSource=null;CyberKit2Melee.Clones.Clear();
        }
        yield return $"Cyber Lord kit v2 abilities: {count-c0} checks (every v2 pose state over an 18 s timeline; effects deterministic, none flat on the floor, the low tier lighter, 0 bytes per frame; cards, edge glitch, freeze flash and cues)";

        // ---- 5. the Runtime wiring (source text) ----
        c0=count;
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var pres=R("NativeCyberLordPresentation.cs");var mine=R("NativeCyberKit2MeleePresentation.cs");var screen=R("NativeCyberLordScreen.cs");var arena=R("ArenaPresentation.cs");var sandbox=R("NativeSandbox.cs");
            Check(pres.Contains("CyberRangedStep(s,time);CyberKit2Step(s,time);")&&pres.Contains("CyberKit2Clear();"),"the preview steps before the views are read, and clears with the cyber state");
            Check(pres.Contains("g.Kind==CyberGhost.Fork&&pose<cyberGhostPoses.Length&&CyberForkGhost(g,s,pilot,cyberGhostPoses[pose],time)")&&mine.Contains("NativeCyberLordFx.ForkActor(clone,caster,g)"),"the fork clones pose on rigs of their own");
            Check(pres.Contains("cyberFx.FlareOf(a.Id,time,v)")&&!pres.Contains("cyberFx.FlareOf(a.Id,time)"),"the look and the aura read the Overclock's white heat");
            Check(pres.Contains("CyberKit2Cues(id,pos,mine,t);")&&pres.Contains("if(hit==CyberHit.None){CyberKit2Event(e,pos,targetMe,mine);return;}"),"the v2 state and event cues");
            Check(screen.Contains("CyberKit2MeleeHud.Replaces(v,slot)")&&screen.Contains("CyberKit2Screen();"),"the v2 cards and the screen overlays");
            Check(arena.Contains("CyberKit2Unshift(a.Id,ref old)")&&arena.Contains("p=SpawnPlace(a,p,renderTime);p=CyberKit2Place(a,p);"),"the preview's drawn-body shift goes on after the smoothing and off before it");
            Check(sandbox.Contains("\"gallery-cyber-kit2-melee\"")&&sandbox.Contains("presentation.CyberKit2MeleePreviewOn=!presentation.CyberKit2MeleePreviewOn")&&NativeDevActions.Find("gallery-cyber-kit2-melee")!=null,"the Dev Sandbox gallery toggles the preview");
            Check(mine.Contains("if(!CyberKit2MeleePreviewOn||me==null)")&&mine.Contains("CyberLordFeed.Kit2MeleeSource=null"),"the preview is off unless toggled and hands the source back");
            Check(!Regex.IsMatch(mine,@"\b(me|a)\.(Position|Velocity|Yaw)\s*=[^=]"),"the preview never moves the authority's pilot");
        }
        yield return $"Cyber Lord kit v2 Runtime wiring: {count-c0} checks (preview step and clear, fork clone rigs, Overclock heat, cards, overlays, cues, the drawn-body shift, the DEV gallery)";
        yield return $"Cyber Lord kit v2 melee: {count} checks in all";
    }
    private static void Copy(CyberKit2MeleeRecord from,CyberKit2MeleeRecord to)
    {
        to.Version=from.Version;Array.Copy(from.At,to.At,to.At.Length);Array.Copy(from.Release,to.Release,to.Release.Length);Array.Copy(from.Until,to.Until,to.Until.Length);Array.Copy(from.Ready,to.Ready,to.Ready.Length);Array.Copy(from.CutAt,to.CutAt,to.CutAt.Length);
        to.Combo=from.Combo;to.UplinkFrom=from.UplinkFrom;to.UplinkAnchor=from.UplinkAnchor;to.UplinkTarget=from.UplinkTarget;to.UplinkOutcome=from.UplinkOutcome;to.UplinkLatchAt=from.UplinkLatchAt;to.UplinkArriveAt=from.UplinkArriveAt;
        to.OverclockAt=from.OverclockAt;to.OverclockUntil=from.OverclockUntil;to.ForkAt=from.ForkAt;to.ForkUntil=from.ForkUntil;
        Array.Copy(from.ClonePos,to.ClonePos,to.ClonePos.Length);Array.Copy(from.CloneYaw,to.CloneYaw,to.CloneYaw.Length);Array.Copy(from.CloneStrikeAt,to.CloneStrikeAt,to.CloneStrikeAt.Length);Array.Copy(from.CloneBurstAt,to.CloneBurstAt,to.CloneBurstAt.Length);
        Array.Copy(from.CloneTarget,to.CloneTarget,to.CloneTarget.Length);Array.Copy(from.CloneBurst,to.CloneBurst,to.CloneBurst.Length);
        to.CutFrom=from.CutFrom;to.CutTo=from.CutTo;Array.Copy(from.Marked,to.Marked,to.Marked.Length);to.MarkedCount=from.MarkedCount;to.DetonateAt=from.DetonateAt;to.MeteorFrom=from.MeteorFrom;to.MeteorTarget=from.MeteorTarget;to.LandAt=from.LandAt;
    }
}
