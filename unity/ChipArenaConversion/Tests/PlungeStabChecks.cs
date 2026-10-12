using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using ChipArena.UnityConversion;

/// <summary>
/// The plunging stab's presentation (owner 2026-10-12: "Can we add a downward jump attack as well? If the player is facing
/// downwards?", choice "Plunging stab"; docs/NEW-KITS.md section 1, docs/MELEE-FEEL.md "Plunging stab"; Core/NativePlungeStab,
/// NativePlungePose, NativePlungeFx). Astra's rule does not exist yet, so every check drives fixtures and timelines built from the
/// spec's numbers (.08 s windup, a -24 m/s drop keeping .15 of the horizontal speed, rings of .9/1/1.2/1.4 m, stuck recoveries of
/// .3/.45/.5/.7 s) through the one field mapping's struct (NativePlungeData). Covers: the classification (never the forward jump
/// attack or a sprint attack, and the forward one never this); the pose timeline against the spec; blade-down poses with the legs
/// drawn up; foot and blade ground contact through the stuck recovery and the pull-out (the greatsword free last); the first-person
/// blade pointing down at the view's pitch; effects sized by the fall height; the reduced and low tiers; keying on replicated data
/// only (the presented root and events only stand in for missing times; host, guest and killcam agree); no allocation per frame.
/// Presentation only: no simulation, hitbox, damage, timing or network change.
/// </summary>
internal static class PlungeStabChecks
{
    private const float Dt=1/60f;
    private static readonly string[] Specs={"sword","daggers","shield","greatsword"};
    private static void Check(bool ok,string what){if(!ok)throw new InvalidOperationException("Plunging stab: "+what);}
    public static void Run(string root,ConversionContent data)
    {
        var log=new List<string>();
        Mapping(root,log);Classification(log);Timeline(log);Poses(log);Contact(log);View(log);Sizes(log);Tiers(log);Keying(log);Allocation(log);Hooks(root,log);
        foreach(var line in log)Console.WriteLine("PASS plunging stab: "+line);
    }

    // ---- the spec timeline as the authority would replicate it ----
    private struct Spec{public float At,PlungeAt,ImpactAt,Until,Height,Y0,Recovery;}
    private static Spec Plan(string spec,float h,float at=1)
    {
        // Pressed at the top of an h metre drop: the windup keeps falling under gravity (no hang), then a straight -24 m/s drop.
        float pa=at+NativePlungeRules.Windup,y0=h-11.5f*NativePlungeRules.Windup*NativePlungeRules.Windup,ia=pa+y0/-NativePlungeRules.DropSpeed,r=NativePlungeRules.Recovery(spec);
        return new Spec{At=at,PlungeAt=pa,ImpactAt=ia,Until=ia+r,Height=h,Y0=y0,Recovery=r};
    }
    private static float RootY(in Spec s,float t)=>t<s.At?s.Height:t<s.PlungeAt?s.Height-11.5f*(t-s.At)*(t-s.At):t<s.ImpactAt?s.Y0+NativePlungeRules.DropSpeed*(t-s.PlungeAt):0;
    private static NativePlungeData DataAt(in Spec s,float t,bool down=true,bool height=true)
        =>new NativePlungeData{Down=down,At=s.At,PlungeAt=s.PlungeAt,ImpactAt=t>=s.ImpactAt?s.ImpactAt:float.NaN,Until=t>=s.ImpactAt?s.Until:float.NaN,Height=t>=s.ImpactAt&&height?s.Height:float.NaN};
    // The authority's swing fields (NativeMeleePresentation for an air attack): the hit from the plunge to the landing.
    private static void Swing(MeleeView v,in Spec s,float t,bool air=true)
    {
        bool falling=t<s.ImpactAt;float hitUntil=falling?s.At+10:s.ImpactAt+Dt*.5f,until=falling?s.At+10:s.Until;
        v.LastAttack=s.At;v.Windup=s.PlungeAt-s.At;v.AttackRate=1;v.AttackIndex=1;bool win=t<=hitUntil;v.SwingAt=win?s.PlungeAt:float.NaN;v.SwingUntil=win?hitUntil:float.NaN;
        v.Commit=t<until;v.CommitAt=s.At;v.CommitUntil=until;v.CommitYaw=0;v.StepAt=v.StepUntil=s.At;v.StepDistance=0;v.AirAttack=air?1:0;
    }
    private sealed class Frame{public float T;public string State;public Vector3 Contact,LContact,Hand,Blade,LAnkle,RAnkle,LToe,RToe,Head;public float Body;public NativePlungePhase Phase;}
    private sealed class Run_{public readonly List<Frame> Frames=new List<Frame>();public Spec S;public int ForwardImpacts;public MeleeMotion M;}
    /// <summary>Drives one weapon's plunge (or, with plunge off, the same swing as a forward jump attack) through MeleeMotion with the
    /// jump layer, as the arena rigs pose it; `sprint` flags the swing as a sprint attack, `stun` makes a stun own the pilot.</summary>
    private static Run_ Drive(string spec,float h,bool plunge=true,bool sprint=false,bool stun=false,float rootLag=0,bool dataImpact=true,MeleeMotion reuse=null)
    {
        var s=Plan(spec,h);var m=reuse??new MeleeMotion{Impacts=new MeleeImpacts(),JumpLayer=new NativeJumpMotion()};var v=new MeleeView{Id="p",Spec=spec};var w=NativePlungePose.For(spec);var o=new Run_{S=s,M=m};float last=-100;
        for(int f=0;f*Dt<s.Until+.4f;f++)
        {
            float t=.6f+f*Dt,y=RootY(s,t-rootLag);bool g=t-rootLag>=s.ImpactAt;
            v.Grounded=g;v.Vel=new Vector3(0,t>=s.PlungeAt&&!g?NativePlungeRules.DropSpeed:0,0);v.SprintAttack=sprint;
            if(t>=s.At)Swing(v,s,t);else{v.LastAttack=-100;v.Commit=false;v.SwingAt=v.SwingUntil=float.NaN;}
            v.HitStunUntil=stun?s.At+5:0;
            var d=plunge?DataAt(s,t):NativePlungeData.None;if(plunge&&!dataImpact){d.ImpactAt=d.Until=d.Height=float.NaN;}v.Plunge=t>=s.At?d:NativePlungeData.None;
            var r=m.Rig;r.GroupPosition=new Vector3(0,y+.865f,0);r.GroupYaw=m.PresentedYaw(v,t,0);float dt=last<-90?Dt:t-last;last=t;m.Sample(v,t,dt);
            var fr=new Frame{T=t-s.At,State=m.State,Body=r.P[CapturedRig.Body].Y,LAnkle=r.AnkleInGroup(true),RAnkle=r.AnkleInGroup(false),LToe=r.ChainPoint(CapturedRig.LToe,-1),RToe=r.ChainPoint(CapturedRig.RToe,-1),Head=r.ChainPoint(CapturedRig.Head,-1),Hand=r.ChainPoint(CapturedRig.RHand,-1)};
            fr.Blade=Vector3.Transform(Vector3.UnitZ,r.ChainQuat(CapturedRig.RHand,-1));
            if(w!=null){fr.Contact=NativePlungePose.ContactPoint(r,w);if(w.Dual)fr.LContact=NativePlungePose.ContactPoint(r,w,true);}
            fr.Phase=m.PlungeWatch.Clock(t,out var c)&&m.PlungeWatch.Is(s.At)?c.Phase:NativePlungePhase.None;
            if(t>=s.At)o.Frames.Add(fr);
        }
        o.ForwardImpacts=m.AirWatch.Impacts;return o;
    }

    // ---- the field mapping ----
    private static void Mapping(string root,List<string> log)
    {
        // Today's actors: the jump attack's fields map through; no air-attack kind exists yet, so nothing reads as down.
        var a=new Actor{Id=4,SourceSpec="sword",AttackAt=2,AirAttack=true,PlungeAt=2.08f,ImpactAt=-1,CommitAt=2,CommitUntil=12};
        var d=NativePlungeFields.Of(a);Check(!d.Down&&d.At==2&&Math.Abs(d.PlungeAt-2.08f)<1e-6f&&float.IsNaN(d.ImpactAt)&&float.IsNaN(d.Until)&&float.IsNaN(d.Height),"the mapping reads the jump attack's replicated fields; no kind yet: nothing is a plunge");
        a.ImpactAt=2.3f;a.CommitUntil=2.75f;d=NativePlungeFields.Of(a);Check(Math.Abs(d.ImpactAt-2.3f)<1e-6f&&Math.Abs(d.Until-2.75f)<1e-6f,"the landing and the recovery's end map from ImpactAt and the commitment");
        a.AirAttack=false;d=NativePlungeFields.Of(a);Check(!d.Down&&float.IsNaN(d.PlungeAt)&&float.IsNaN(d.ImpactAt),"a ground swing maps to no air data");
        Check(!NativePlungeFields.Of(null).Down&&!NativePlungeFields.Of(new Actor{AttackAt=-100}).Down,"no actor or no swing is no plunge");
        Check(NativePlungeFields.Impact(new ArenaEvent{Kind=EventKind.Land,Detail="melee-down"})&&!NativePlungeFields.Impact(new ArenaEvent{Kind=EventKind.Land,Detail="melee-air"})&&!NativePlungeFields.Impact(new ArenaEvent{Kind=EventKind.Hit,Detail="melee-down"}),"the impact event is Land with Detail melee-down, never the jump attack's melee-air");
        // Direct vs ring: a hit before the landing (the blade passing through) or under the blade is direct; one out in the ring is not.
        var feet=new V3(0,0,0);
        Check(NativePlungeFields.Direct(new ArenaEvent{Kind=EventKind.Hit,Source=1,Target=2,Time=1.1f,Position=new V3(0,1,3)},1,1.08f,1.2f,feet)&&NativePlungeFields.Direct(new ArenaEvent{Kind=EventKind.Hit,Source=1,Target=2,Time=1.2f,Position=new V3(.2f,1,.6f)},1,1.08f,1.2f,feet)
            &&!NativePlungeFields.Direct(new ArenaEvent{Kind=EventKind.Hit,Source=1,Target=3,Time=1.205f,Position=new V3(1.1f,1,0)},1,1.08f,1.2f,feet)&&!NativePlungeFields.Direct(new ArenaEvent{Kind=EventKind.Hit,Source=5,Target=2,Time=1.1f},1,1.08f,1.2f,feet),"direct hits: through the drop or under the blade; the ring's and others' are not");
        // The one place: only NativePlungeFields names the proposed fields; every presenter reads its struct.
        foreach(var f in Directory.EnumerateFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories))
        {
            string text=File.ReadAllText(f),name=Path.GetFileName(f);if(name=="NativePlungeStab.cs")continue;
            Check(!text.Contains("\"melee-down\"")&&!text.Contains("AirKind")&&!text.Contains("FallHeight"),name+" names no proposed plunge field or event (NativePlungeFields does)");
        }
        log.Add("the mapping (NativePlungeFields) reads AirAttack, PlungeAt, ImpactAt and the commitment and names the proposed kind, height and melee-down event in one place; no actor reads as down until the kind lands; direct and ring hits split");
    }

    // ---- classification ----
    private static void Classification(List<string> log)
    {
        Check(NativePlungeRules.Classify(true,1,true,false,false)==NativeAirStrike.Down&&NativePlungeRules.Classify(true,-1,false,false,false)==NativeAirStrike.Down,"the replicated kind makes the plunge, airborne or not");
        Check(NativePlungeRules.Classify(false,1,false,false,false)==NativeAirStrike.Forward&&NativePlungeRules.Classify(false,-1,true,false,false)==NativeAirStrike.Forward,"the forward jump attack stays as it was (flag or airtime)");
        Check(NativePlungeRules.Classify(true,1,true,true,false)==NativeAirStrike.None&&NativePlungeRules.Classify(false,1,true,true,false)==NativeAirStrike.None,"a sprint attack is never an air attack of either kind");
        Check(NativePlungeRules.Classify(true,1,true,false,true)==NativeAirStrike.None,"a swing a special, stun, roll, juggle or knockdown owns is neither");
        Check(NativePlungeRules.ForwardFlag(1,false,true)==0&&NativePlungeRules.ForwardFlag(-1,false,true)==0&&NativePlungeRules.ForwardFlag(1,true,false)==0&&NativePlungeRules.ForwardFlag(1,false,false)==1&&NativePlungeRules.ForwardFlag(-1,false,false)==-1,"the forward watch's flag is 0 for a plunge or a sprint attack, unchanged otherwise");
        foreach(var spec in Specs)
        {
            var p=Drive(spec,3);bool any=false;
            foreach(var f in p.Frames){if(f.T<p.S.Until-p.S.At-.01f){Check(!f.State.StartsWith("air attack")&&!f.State.StartsWith("sprint attack")&&!f.State.StartsWith("cut"),spec+" a plunge never shows the jump attack, a sprint attack or a cut ("+f.State+" at "+f.T.ToString("0.000")+")");any|=f.State.StartsWith("plunge");}}
            Check(any&&p.ForwardImpacts==0,spec+" the plunge plays and leaves no forward jump attack impact");
            var fw=Drive(spec,3,plunge:false);bool air=false;foreach(var f in fw.Frames){Check(!f.State.StartsWith("plunge"),spec+" the forward jump attack never shows the plunge ("+f.State+")");air|=f.State.StartsWith("air attack");}
            Check(air&&!fw.M.PlungeWatch.Down,spec+" the same swing without the kind is the forward jump attack");
            var sp=Drive(spec,3,sprint:true);foreach(var f in sp.Frames)Check(!f.State.StartsWith("plunge"),spec+" a sprint attack is never shown as the plunge");
            var st=Drive(spec,3,stun:true);foreach(var f in st.Frames)Check(!f.State.StartsWith("plunge"),spec+" a stunned pilot's swing is no plunge");
        }
        log.Add("Classify and ForwardFlag: the replicated kind alone makes the plunge; the forward jump attack and sprint attacks keep theirs; for every weapon the rig shows only plunge states for a plunge (no forward impact), only the jump attack without the kind, never the plunge for a sprint attack or a stunned swing");
    }

    // ---- the pose timeline against the spec ----
    private static void Timeline(List<string> log)
    {
        float freeGs=0,freeOther=0;
        foreach(var spec in Specs)
        foreach(float h in new[]{3f,8f})
        {
            var s=Plan(spec,h);
            Check(NativePlungeRules.Clock(spec,s.At,s.PlungeAt,float.NaN,float.NaN,float.NaN,s.At+.04f,out var c)&&c.Phase==NativePlungePhase.Windup&&c.Tuck>.3f&&c.Tuck<.7f,spec+" mid windup");
            Check(NativePlungeRules.Clock(spec,s.At,float.NaN,float.NaN,float.NaN,float.NaN,s.At+NativePlungeRules.Windup+1e-4f,out c)&&c.Phase==NativePlungePhase.Drop,spec+" without a replicated plunge time the spec's .08 s windup ends it");
            NativePlungeRules.Clock(spec,s.At,s.PlungeAt,s.ImpactAt,s.Until,h,s.ImpactAt+1e-4f,out c);
            Check(c.Phase==NativePlungePhase.Plant&&Math.Abs(c.Until-s.Until)<1e-5f&&c.PullAt>s.ImpactAt&&c.FreeAt>c.PullAt&&c.FreeAt<c.Until,spec+" the stuck recovery runs plant, stuck, pull-out, free, settle inside the replicated recovery");
            float free=c.FreeAt-s.ImpactAt;if(spec=="greatsword")freeGs=Math.Max(freeGs,free);else freeOther=Math.Max(freeOther,free);
            Check(!NativePlungeRules.Clock(spec,s.At,s.PlungeAt,s.ImpactAt,s.Until,h,s.Until+1e-4f,out _),spec+" the plunge ends with the recovery");
            // The rig's states follow the same timeline.
            var p=Drive(spec,h);NativePlungePhase prev=NativePlungePhase.None;
            foreach(var f in p.Frames)
            {
                float t=f.T+s.At;if(t>=s.Until)break;
                NativePlungeRules.Clock(spec,s.At,s.PlungeAt,s.ImpactAt,s.Until,h,t,out var e);
                Check(f.Phase==e.Phase&&f.State==NativePlungeRules.Label(e.Phase),spec+" "+h+" m: the rig is in "+e.Phase+" at "+f.T.ToString("0.000")+" ("+f.State+")");
                Check(f.Phase>=prev,spec+" phases never run back");prev=f.Phase;
            }
            Check(prev==NativePlungePhase.Settle,spec+" reaches the settle");
        }
        Check(freeGs>freeOther+.1f,"the greatsword wrenches free last ("+freeGs.ToString("0.00")+" s after the impact against "+freeOther.ToString("0.00")+")");
        log.Add("the timeline (windup .08 s to the replicated PlungeAt, drop until ImpactAt, plant, stuck, pull-out, free, settle within the replicated recovery) is what the rig shows frame by frame at 3 and 8 m for every weapon; the greatsword comes free "+freeGs.ToString("0.00")+" s after landing, the others by "+freeOther.ToString("0.00")+" s");
    }

    // ---- the poses ----
    private static void Poses(List<string> log)
    {
        float worstDown=1,lowestTuck=0;
        foreach(var spec in Specs)
        {
            var p=Drive(spec,8);var w=NativePlungePose.For(spec);int drop=0;
            foreach(var f in p.Frames)
            {
                if(f.Phase!=NativePlungePhase.Drop||f.T+p.S.At<p.S.PlungeAt+NativePlungeRules.CommitBlend)continue;drop++;
                // Blade first: the blade's point (the shield's bottom rim) below the hand and the feet, the blade pointing down.
                float down=w.Shield?(f.Contact-f.Hand).Y/w.Contact.Length():f.Blade.Y;worstDown=Math.Min(worstDown,-down);
                Check(f.Contact.Y<f.Hand.Y-.25f&&f.Contact.Y<Math.Min(f.LAnkle.Y,f.RAnkle.Y)-.05f&&-down>(w.Shield?.75f:.85f),spec+" the drop leads blade first ("+f.Contact+", blade "+f.Blade+")");
                // The legs drawn up: both ankles well above the standing floor.
                float tuck=Math.Min(f.LAnkle.Y,f.RAnkle.Y)-MeleeRig.FLOOR;lowestTuck=lowestTuck==0?tuck:Math.Min(lowestTuck,tuck);
                Check(Math.Min(f.LAnkle.Y,f.RAnkle.Y)>MeleeRig.FLOOR+.22f,spec+" the drop draws the legs up (ankles "+f.LAnkle.Y.ToString("0.00")+", "+f.RAnkle.Y.ToString("0.00")+")");
                if(w.Dual)Check(f.LContact.Y<f.Hand.Y-.2f,spec+" both daggers point down");
            }
            Check(drop>=4,spec+" the 8 m drop is drawn over several frames ("+drop+")");
        }
        log.Add("the committed drop leads blade first for every weapon (blade within "+(Math.Acos(worstDown)*180/Math.PI).ToString("0")+" deg of straight down, the point under the feet), the legs drawn up (ankles at least "+lowestTuck.ToString("0.00")+" m over the standing floor), both daggers down, the shield rim first");
    }

    // ---- foot and blade ground contact ----
    private static void Contact(List<string> log)
    {
        float deepest=0,drift=0,ankle=0;
        foreach(var spec in Specs)
        foreach(float h in new[]{3f,8f})
        {
            var p=Drive(spec,h);var s=p.S;var w=NativePlungePose.For(spec);float sole=NativePlungePose.Sole;Vector3? planted=null;
            NativePlungeRules.Clock(spec,s.At,s.PlungeAt,s.ImpactAt,s.Until,h,s.ImpactAt+1e-4f,out var c);bool freed=false;
            foreach(var f in p.Frames)
            {
                float t=f.T+s.At;if(t<s.ImpactAt||t>=s.Until)continue;
                float plantEnd=s.ImpactAt+NativePlungeRules.PlantSeconds+NativePlungeRules.HeavyPlant*NativePlungeRules.Heavy(h);
                // The soles on the floor from the landing on: the lead foot flat, the rear on its toes.
                float lo=Math.Min(f.LAnkle.Y,f.RAnkle.Y),hi=Math.Max(f.LAnkle.Y,f.RAnkle.Y);
                if(t>=plantEnd){Check(Math.Abs(lo-MeleeRig.FLOOR)<.012f&&hi<MeleeRig.FLOOR+.06f&&Math.Min(f.LToe.Y,f.RToe.Y)<MeleeRig.FLOOR+.03f,spec+" "+h+" m: feet planted at "+f.T.ToString("0.000")+" (ankles "+lo.ToString("0.000")+", "+hi.ToString("0.000")+")");ankle=Math.Max(ankle,Math.Abs(lo-MeleeRig.FLOOR));}
                // The blade in the floor from the plant until it comes free, held still while stuck.
                if(t>=plantEnd&&t<c.PullAt)
                {
                    Check(f.Contact.Y<sole-.05f&&f.Contact.Y>sole-w.Bury-w.HeavyBury-.12f,spec+" "+h+" m: the blade is planted in the floor ("+(f.Contact.Y-sole).ToString("0.00")+" m at "+f.T.ToString("0.000")+")");
                    if(w.Dual)Check(f.LContact.Y<sole-.05f,spec+" both daggers are planted");
                    deepest=Math.Min(deepest,f.Contact.Y-sole);if(planted==null)planted=f.Contact;else drift=Math.Max(drift,(f.Contact-planted.Value).Length());
                }
                if(t>=c.FreeAt+Dt){Check(f.Contact.Y>sole,spec+" "+h+" m: free of the floor after the pull-out ("+(f.Contact.Y-sole).ToString("0.00")+")");freed=true;}
            }
            Check(planted!=null&&freed,spec+" "+h+" m plants and frees its blade");
        }
        Check(drift<.01f,"the stuck blade holds still (drift "+(drift*100).ToString("0.0")+" cm)");
        log.Add("from the plant the lead sole is on the floor (within "+(ankle*100).ToString("0.0")+" cm) and the rear on its toes; the blade (both daggers, the shield's rim) stays in the floor down to "+(-deepest).ToString("0.00")+" m, still within "+(drift*1000).ToString("0")+" mm, until the pull-out frees it");
    }

    // ---- first person ----
    private static void View(List<string> log)
    {
        float worst=1;
        foreach(var spec in Specs)
        foreach(float pitch in new[]{-45f,-60f,-75f})
        {
            var set=MeleeSet.For(spec);var row=new float[CapturedClip.Channels];set.Idle.Sample(1,row);var rig=new MeleeRig();var pose=new CapturedRig.Pose();var w=NativePlungePose.For(spec);
            var c=new NativePlungeClock{Phase=NativePlungePhase.Drop,Tuck=1,Commit=1,Fall=.2f,Land=float.NaN};
            rig.Reset();rig.WeaponVisible=true;rig.WeaponReach=1;float guard=NativePlungePose.ViewPose(rig,set,spec,c,row,pitch);rig.WritePose(pose);NativeFirstPersonPose.SettleTorso(pose);
            Vector3 hand=pose.WorldPosition[CapturedRig.RHand],tip=hand+Vector3.Transform(w.Contact,pose.WorldRotation[CapturedRig.RHand]);
            // In the camera's frame (the view rig's, mirrored, scaled and offset): in front, below the eye, the point further down the view.
            Vector3 Cam(Vector3 q)=>new Vector3(-q.X,q.Y,q.Z)*NativeFirstPersonPose.Scale+NativeViewBase.Melee;var ch=Cam(hand);var ct=Cam(tip);
            float tan=(float)Math.Tan(NativeViewRules.FirstFov*Math.PI/360);
            Check(guard==0&&ch.Z>.25f&&ct.Z>ch.Z&&ch.Y<0&&Math.Abs(ct.Y/ct.Z)<tan&&Math.Abs(ct.X/ct.Z)<tan*16/9,spec+" at "+pitch+": the view blade's point is in view ahead and below ("+ct+")");
            var down=NativePlungePose.ViewDown(pitch);var axis=Vector3.Normalize(tip-hand);if(!w.Shield)worst=Math.Min(worst,Vector3.Dot(axis,down));
            // World down in the camera's frame at that pitch: the blade leans no further than its view lean from it.
            float phi=-pitch*(float)Math.PI/180;var worldDown=new Vector3(0,-(float)Math.Cos(phi),(float)Math.Sin(phi));Check(Vector3.Dot(down,worldDown)>.85f,"the view's down follows the pitch");
        }
        Check(worst>.9f,"the view blades point along the view's down ("+worst.ToString("0.00")+")");
        log.Add("first person at -45/-60/-75 degrees: each view blade points down the view (within "+(Math.Acos(worst)*180/Math.PI).ToString("0")+" deg), its point ahead and below in frame; the speed lines take the drop (NativePlungeRules.ScreenWeight)");
    }

    // ---- effects sized by height ----
    private sealed class Counter:IKitFxSink
    {
        public int Glows,Streaks,Smokes;public float Reach,High,Brightest;public Vector3 Centre;
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Glows++;Brightest=Math.Max(Brightest,color.X+color.Y+color.Z);}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Streaks++;}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Smokes++;Reach=Math.Max(Reach,new Vector2(at.X-Centre.X,at.Z-Centre.Z).Length());High=Math.Max(High,at.Y);}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){}
        public void Reset(){Glows=Streaks=Smokes=0;Reach=High=Brightest=0;}
    }
    private sealed class Scene:IKitFxScene
    {
        public readonly List<Actor> List=new List<Actor>();public IReadOnlyList<Actor> Actors=>List;public int ViewerId;
        public Vector3 Feet(Actor a)=>new Vector3(a.Position.x,a.Position.y,a.Position.z);
        public bool Chest(Actor a,out Vector3 c){c=Feet(a)+new Vector3(0,1.3f,0);return true;}
        public bool Blade(Actor a,bool left,out Vector3 root,out Vector3 tip){root=Feet(a)+new Vector3(0,1,.4f);tip=Feet(a)+new Vector3(0,-.4f,.6f);return !left||a.SourceSpec=="daggers";}
        public int Viewer=>ViewerId;
        public bool ViewBlade(bool left,out Vector3 root,out Vector3 tip){root=tip=default;return false;}
        public int WeaponColor(Actor a)=>0;public int Shots=>0;
        public bool Shot(int i,out Vector3 at,out Vector3 v,out string fx,out int owner){at=v=default;fx=null;owner=0;return false;}
    }
    /// <summary>One pilot plunging at (2, 0, -1) facing +x, the effect drawn every frame; returns the counter at its busiest frame.</summary>
    private static (Counter peak,NativePlungeFx fx,int bursts,int impacts,int frees) Burst(string spec,float h,float quality,bool low,List<ArenaEvent> events=null,bool down=true,NativeStructureFx st=null,int viewer=0,Counter sink=null)
    {
        var s=Plan(spec,h);var fx=new NativePlungeFx();var scene=new Scene{ViewerId=viewer};var a=new Actor{Id=1,SourceSpec=spec,Hp=100,Yaw=90};scene.List.Add(a);var sinkA=sink??new Counter();var peak=new Counter();int impacts=0,frees=0;
        for(int f=0;f*Dt<s.Until+1;f++)
        {
            float t=.6f+f*Dt;a.Position=new V3(2,RootY(s,t),-1);a.AttackAt=t>=s.At?s.At:-100;a.Grounded=t>=s.ImpactAt;
            fx.Begin(t);fx.Track(a.Id,spec,t>=s.At?DataAt(s,t,down):NativePlungeData.None,scene.Feet(a),90,a.Grounded,t,false);
            if(events!=null){var now=new List<ArenaEvent>();foreach(var e in events)if(e.Time<=t)now.Add(e);fx.Events(now,t);}
            foreach(var b in fx.Fresh){if(b.Pull)frees++;else impacts++;NativePlungeFx.Structures(st,b,t);}
            sinkA.Reset();sinkA.Centre=new Vector3(2,0,-1);fx.Draw(sinkA,scene,t,quality,low);
            if(sinkA.Glows+sinkA.Streaks+sinkA.Smokes>peak.Glows+peak.Streaks+peak.Smokes){peak.Glows=sinkA.Glows;peak.Streaks=sinkA.Streaks;peak.Smokes=sinkA.Smokes;}
            peak.Reach=Math.Max(peak.Reach,sinkA.Reach);peak.High=Math.Max(peak.High,sinkA.High);peak.Brightest=Math.Max(peak.Brightest,sinkA.Brightest);
        }
        return (peak,fx,fx.Live,impacts,frees);
    }
    private static void Sizes(List<string> log)
    {
        Check(NativePlungeRules.Heavy(2)==0&&NativePlungeRules.Heavy(8)==1&&Math.Abs(NativePlungeRules.Heavy(5)-.5f)<1e-5f&&NativePlungeRules.Heavy(1)==0&&NativePlungeRules.Heavy(12)==1,"the height weight follows the 2-8 m bonus range");
        foreach(var r in new[]{.9f,1f,1.2f,1.4f})
        {
            Check(NativePlungeFx.Crater(r,1)>NativePlungeFx.Crater(r,0)*2&&NativePlungeFx.CrackReach(r,1)>NativePlungeFx.CrackReach(r,0)*2.5f&&NativePlungeFx.DustReach(r,1)>NativePlungeFx.DustReach(r,0)*1.5f,"crater, cracks and dust grow with the fall ("+r+" m ring)");
            Check(NativePlungeFx.Crater(r,1)<=r+1e-5f&&NativePlungeFx.DustReach(r,0)>=r,"the crater stays inside the ring, the dust wave reaches it");
        }
        Check(NativePlungeFx.Rubble(1)>NativePlungeFx.Rubble(0)*2.5f&&NativePlungeFx.ShakePeak(1)>NativePlungeFx.ShakePeak(0)*2.5f&&NativePlungeFx.ShakeSeconds(1)>NativePlungeFx.ShakeSeconds(0),"rubble and the camera kick grow with the fall");
        Check(NativePlungeFx.ShakePeak(0)>NativeAirAttackFx.ShakePeak(0)&&NativePlungeFx.ShakePeak(1)>NativeAirAttackFx.ShakePeak(1)*1.5f&&NativePlungeFx.ShakePeak(1)<=NativeStructureShake.MaxOffset,"the kick is heavier than the jump attack's and inside the shake cap");
        Check(NativePlungeRules.Radius("daggers")==.9f&&NativePlungeRules.Radius("sword")==1&&NativePlungeRules.Radius("shield")==1.2f&&NativePlungeRules.Radius("greatsword")==1.4f,"the spec's rings");
        // Drawn: the dust wave rolls further for a big fall; the destruction kit gets more marks and rubble only from a real drop.
        var small=Burst("sword",2,1,false);var big=Burst("sword",8,1,false);
        Check(small.impacts==1&&big.impacts==1&&small.frees==1&&big.frees==1,"one impact and one pull-out per plunge");
        Check(big.peak.Reach>small.peak.Reach*1.4f&&big.peak.High>small.peak.High,"the drawn dust wave rolls further and higher from 8 m ("+big.peak.Reach.ToString("0.00")+" m) than from 2 m ("+small.peak.Reach.ToString("0.00")+" m)");
        var st2=new NativeStructureFx();var st8=new NativeStructureFx();Burst("greatsword",2,1,false,st:st2);Burst("greatsword",8,1,false,st:st8);
        Check(st8.LiveMarks>st2.LiveMarks&&st2.LiveMarks>=4,"the crater leaves more cracks from 8 m ("+st8.LiveMarks+" marks) than from 2 m ("+st2.LiveMarks+")");
        var q2=new NativeStructureFx();var q8=new NativeStructureFx();float t0=0;
        foreach(var (q,h) in new[]{(q2,2f),(q8,8f)}){var b=new NativePlungeFx.Burst{Seed=3,Spec="sword",At=1,Height=h,Heavy=NativePlungeRules.Heavy(h),Radius=1,Floor=0,Feet=Vector3.Zero,Point=new Vector3(0,0,.95f)};NativePlungeFx.Structures(q,b,1);t0=1;}
        Check(q2.LiveBursts==0&&q8.LiveBursts==1,"rubble only from a real drop (8 m: one burst, 2 m: none)");
        float Kick(NativeStructureFx q){float k=0;for(float t=t0;t<t0+.3f;t+=.005f)k=Math.Max(k,q.ShakeOffset(t,new V3(0,1.6f,0)).Length);return k;}
        Check(Kick(q8)>Kick(q2)*1.5f&&Kick(q8)<=NativeStructureShake.MaxOffset,"the camera kick at the pilot is heavier from 8 m ("+(Kick(q8)*100).ToString("0.0")+" against "+(Kick(q2)*100).ToString("0.0")+" cm)");
        log.Add("by the fall's 2-8 m weight: crater "+NativePlungeFx.Crater(1,0).ToString("0.00")+"-"+NativePlungeFx.Crater(1,1).ToString("0.00")+" m, cracks "+NativePlungeFx.CrackReach(1,0).ToString("0.00")+"-"+NativePlungeFx.CrackReach(1,1).ToString("0.00")+" m, dust wave "+NativePlungeFx.DustReach(1,0).ToString("0.0")+"-"+NativePlungeFx.DustReach(1,1).ToString("0.0")+" m (sword ring), rubble from 3.5 m, kick "+(NativePlungeFx.ShakePeak(0)*100).ToString("0")+"-"+(NativePlungeFx.ShakePeak(1)*100).ToString("0")+" cm under the "+(NativeStructureShake.MaxOffset*100).ToString("0")+" cm cap; drawn dust reaches "+small.peak.Reach.ToString("0.0")+" m from 2 m and "+big.peak.Reach.ToString("0.0")+" m from 8 m");
    }
    private static void Tiers(List<string> log)
    {
        var full=Burst("greatsword",8,1,false).peak;var reduced=Burst("greatsword",8,.3f,false).peak;var low=Burst("greatsword",8,1,true).peak;var both=Burst("greatsword",8,.3f,true).peak;
        int F(Counter c)=>c.Glows+c.Streaks+c.Smokes;
        Check(F(reduced)<F(full)*.6f&&F(low)<F(full)*.7f&&F(both)<=F(reduced)&&F(both)>0,"Reduced effects and Low graphics thin it (full "+F(full)+", reduced "+F(reduced)+", low "+F(low)+", both "+F(both)+")");
        Check(reduced.Brightest<full.Brightest*.75f,"Reduced effects dim the flashes");
        // The first-person screen: the drop's weight drives the sprint's lines and vignette, Reduced dimming it as the sprint's.
        var s=Plan("sword",8);NativePlungeRules.Clock("sword",s.At,s.PlungeAt,float.NaN,float.NaN,float.NaN,s.PlungeAt+.15f,out var c);float wgt=NativePlungeRules.ScreenWeight(c);
        Check(wgt>.95f&&NativeSprintFx.Vignette(wgt,.3f)<NativeSprintFx.Vignette(wgt,1)&&NativeSprintFx.Count(NativeSprintFx.Lines,NativeSprintFx.ReducedLines,.3f,true)<NativeSprintFx.Count(NativeSprintFx.Lines,NativeSprintFx.ReducedLines,1,false),"the speed lines and vignette take the drop under the Reduced and Low rules");
        NativePlungeRules.Clock("sword",s.At,s.PlungeAt,s.ImpactAt,s.Until,8,s.ImpactAt+.2f,out c);Check(NativePlungeRules.ScreenWeight(c)==0,"the screen clears after the landing");
        // The camera kick is the destruction kit's shake: the setting gates it and Reduced halves it (ConversionGame).
        log.Add("Reduced effects and Low graphics thin the effect ("+F(full)+" -> "+F(reduced)+" / "+F(low)+" / "+F(both)+" primitives at the busiest frame) and Reduced dims the flashes; the first-person lines and vignette follow the sprint screen's Reduced and Low rules");
    }

    // ---- replicated data only ----
    private static void Keying(List<string> log)
    {
        foreach(var spec in new[]{"sword","greatsword"})
        {
            // Host, guest (its root drawn .1 s late) and killcam (a fresh rig) agree on the phases: they come from the replicated times.
            var host=Drive(spec,6);var guest=Drive(spec,6,rootLag:.1f);var kill=Drive(spec,6);
            for(int i=0;i<host.Frames.Count&&i<guest.Frames.Count;i++)
            {
                Check(host.Frames[i].Phase==guest.Frames[i].Phase,spec+" a late presented root does not move the replicated timeline ("+host.Frames[i].T.ToString("0.000")+")");
                Check(host.Frames[i].Phase==kill.Frames[i].Phase&&(host.Frames[i].Contact-kill.Frames[i].Contact).Length()<1e-5f,spec+" two rigs on the same data pose the same");
            }
            // With no replicated impact time, the presented landing stands in.
            var late=Drive(spec,6,dataImpact:false);bool planted=false;foreach(var f in late.Frames)planted|=f.Phase==NativePlungePhase.Stuck;Check(planted,spec+" the presented landing stands in for a missing ImpactAt");
        }
        // The impact event stands in too, with its height; and no kind means no plunge whatever the root does.
        var w=new NativePlungeWatch();var s=Plan("shield",5);
        for(int f=0;f<60;f++){float t=1+f*Dt;var d=DataAt(s,t);d.ImpactAt=d.Until=d.Height=float.NaN;w.Observe(d,"shield",10,false,t);if(f==20)w.ImpactEvent(t,5);}
        Check(w.Impacts==1&&Math.Abs(w.Height-5)<1e-5f,"the melee-down event's time and height stand in for missing fields");
        var none=new NativePlungeWatch();for(int f=0;f<80;f++){float t=1+f*Dt;none.Observe(DataAt(s,t,down:false),"shield",RootY(s,t),t>=s.ImpactAt,t);}
        Check(!none.Down&&none.Impacts==0,"without the replicated kind there is no plunge");
        var fake=Burst("sword",8,1,false,down:false);Check(fake.impacts==0&&fake.frees==0,"the effects never fire without the kind");
        // Hits: the direct victim flashes, the ring victim does not.
        var sp=Plan("sword",4);var ev=new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Hit,Source=1,Target=2,Time=sp.ImpactAt-.005f,Position=new V3(2.95f,1.2f,-1)},new ArenaEvent{Id=2,Kind=EventKind.Land,Source=1,Time=sp.ImpactAt,Detail="melee-down",Radius=1,Position=new V3(2,0,-1)},new ArenaEvent{Id=3,Kind=EventKind.Hit,Source=1,Target=3,Time=sp.ImpactAt+.004f,Position=new V3(2,1.1f,.2f)}};
        var hits=new Counter();var r=Burst("sword",4,1,false,ev,sink:hits);Check(r.fx.LiveFlashes==0&&r.impacts==1,"the hit flashes expire with their life");
        var probe=new NativePlungeFx();var a=new Actor{Id=1,SourceSpec="sword",Hp=100,AttackAt=sp.At};float tt=sp.ImpactAt+.01f;probe.Begin(sp.At);probe.Track(1,"sword",DataAt(sp,sp.At),new Vector3(2,RootY(sp,sp.At),-1),90,false,sp.At,false);probe.Begin(sp.PlungeAt+.01f);probe.Track(1,"sword",DataAt(sp,sp.PlungeAt+.01f),new Vector3(2,RootY(sp,sp.PlungeAt+.01f),-1),90,false,sp.PlungeAt+.01f,false);
        probe.Begin(tt);probe.Track(1,"sword",DataAt(sp,tt),new Vector3(2,0,-1),90,true,tt,false);probe.Events(ev,tt);Check(probe.LiveFlashes==1,"only the direct victim flashes ("+probe.LiveFlashes+")");
        log.Add("host, a guest drawing the root .1 s late and a fresh killcam rig show the same phases and poses from the replicated times; the presented landing and the melee-down event (time, height) only stand in for missing fields; no kind, no plunge, no effect; only the direct victim flashes");
    }

    // ---- no allocation ----
    private sealed class Null:IKitFxSink
    {
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){}
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){}
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){}
        public void Sprite(bool view,Vector3 at,Vector3 color,float size,int profile){}
        public void Line(bool view,Vector3 tail,Vector3 head,Vector3 color,float width,bool shard){}
        public void Band(bool view,Vector3 a,Vector3 b,Vector3 c,Vector3 d,float ua,float ub,Vector3 color){}
    }
    private static void Allocation(List<string> log)
    {
        long used=0,rigPlunge=0,rigForward=0;int frames=0;
        foreach(var spec in Specs)
        {
            // Warm (the dictionaries and rigs built), then a whole plunge: the view pose, the field mapping, the watch, the effects and
            // the destruction kit's share must allocate nothing; the rig no more than the same swing shown as the forward jump attack
            // (MeleeMotion.Sample clones a committed swing's view each frame: that is the existing rig's, not the plunge's).
            var fx=new NativePlungeFx();var scene=new Scene();var a=new Actor{Id=1,SourceSpec=spec,Hp=100,AirAttack=true};scene.List.Add(a);var sink=new Null();var st=new NativeStructureFx();var events=new List<ArenaEvent>();
            var set=MeleeSet.For(spec);var row=new float[CapturedClip.Channels];var view=new MeleeRig();var watch=new NativePlungeWatch();
            var rigs=new[]{new MeleeMotion{Impacts=new MeleeImpacts(),JumpLayer=new NativeJumpMotion()},new MeleeMotion{Impacts=new MeleeImpacts(),JumpLayer=new NativeJumpMotion()}};var views=new[]{new MeleeView{Id="p",Spec=spec},new MeleeView{Id="q",Spec=spec}};
            for(int pass=0;pass<2;pass++)
            {
                var sp=Plan(spec,8,1+pass*10);float[] last={-100,-100};
                for(int f=0;f*Dt<sp.Until-sp.At+.4f;f++)
                {
                    float t=sp.At-.4f+f*Dt,y=RootY(sp,t);bool g=t>=sp.ImpactAt;var d=t>=sp.At?DataAt(sp,t):NativePlungeData.None;
                    for(int k=0;k<2;k++)
                    {
                        var v=views[k];var m=rigs[k];v.Grounded=g;if(t>=sp.At)Swing(v,sp,t);else{v.LastAttack=-100;v.Commit=false;v.SwingAt=v.SwingUntil=float.NaN;}v.Plunge=k==0?d:NativePlungeData.None;
                        long b0=GC.GetAllocatedBytesForCurrentThread();m.Rig.GroupPosition=new Vector3(0,y+.865f,0);m.Rig.GroupYaw=m.PresentedYaw(v,t,0);m.Sample(v,t,last[k]<-90?Dt:t-last[k]);last[k]=t;long b1=GC.GetAllocatedBytesForCurrentThread();
                        if(pass==1){if(k==0)rigPlunge+=b1-b0;else rigForward+=b1-b0;}
                    }
                    long before=GC.GetAllocatedBytesForCurrentThread();
                    a.AttackAt=sp.At;a.Position=new V3(0,y,0);a.Grounded=g;var mapped=NativePlungeFields.Of(a);
                    fx.Begin(t);fx.Track(1,spec,d,new Vector3(0,y,0),0,g,t,false);fx.Events(events,t);foreach(var b in fx.Fresh)NativePlungeFx.Structures(st,b,t);fx.Draw(sink,scene,t,1,false);
                    watch.Observe(d,spec,y,g,t);if(watch.Clock(t,out var c)){set.Idle.Sample(sp.At,row);view.Reset();NativePlungePose.ViewPose(view,set,spec,c,row,-60);}
                    if(pass==1){used+=GC.GetAllocatedBytesForCurrentThread()-before;frames++;}
                }
            }
        }
        Check(used==0,"no allocation per frame in the view pose, mapping, watch, effects and destruction kit ("+used+" bytes over "+frames+" warm frames)");
        Check(rigPlunge<=rigForward,"the plunge adds no allocation to the rig ("+rigPlunge+" bytes against "+rigForward+" for the same swing as the forward jump attack)");
        log.Add("no allocation per frame once warm: the first-person view pose, the field mapping, the watch, the effects and the destruction kit's share through whole plunges of every weapon ("+frames+" frames); the rig allocates no more than the same swing shown as the forward jump attack ("+(rigForward/Math.Max(1,frames))+" bytes a frame, the existing committed-view clone)");
    }

    // ---- the runtime hooks (compiled against Unity elsewhere; their wiring is checked here) ----
    private static void Hooks(string root,List<string> log)
    {
        string Read(string f)=>File.ReadAllText(Path.Combine(root,f));
        string mm=Read("Assets/ChipArena/Core/MeleeMotion.cs"),ca=Read("Assets/ChipArena/Runtime/CapturedAnimation.cs"),fx=Read("Assets/ChipArena/Runtime/NativeAirAttackFxPresentation.cs"),sp=Read("Assets/ChipArena/Runtime/NativeSprintFxPresentation.cs"),vp=Read("Assets/ChipArena/Runtime/NativeViewPresentation.cs"),cg=Read("Assets/ChipArena/Runtime/ConversionGame.cs");
        Check(mm.Contains("if(plunge)PlungeCut(set,p,time);else if(air)AirCut(")&&mm.Contains("JumpLayer.Leap=leapLive||plungeLive;")&&mm.Contains("||plungeStuck)return;"),"the rig plays the plunge before the jump attack, owns the legs and leaves the planted blade in the floor");
        Check(ca.Contains("v.Plunge=NativePlungeFields.Of(a);")&&ca.Contains("bool plunge=!magic&&!GunRules.Ranged(a.Weapon)&&PlungeView(")&&ca.Contains("!plunge&&!NativeFighterPose.Handles(a.SourceSpec)) CapturedRig.BladeFloor")&&vp.Contains("viewRig.ViewPitch=source.Pitch;"),"the arena rigs read the mapping, the first-person view plays it at the camera's pitch");
        Check(fx.Contains("plungeFx.Track(a.Id,a.SourceSpec,NativePlungeFields.Of(a),")&&fx.Contains("plungeFx.Events(s.Events,t);")&&fx.Contains("NativePlungeFx.Structures(st,b,t);")&&fx.Contains("pw.Observe(NativePlungeFields.Of(a),"),"the effects, the destruction kit's share and the cues read the mapping and the events");
        Check(fx.Split("NativePlungeRules.ForwardFlag(a.AirAttack?1:0,a.SprintAttack,NativePlungeFields.Of(a).Down)").Length==3&&ca.Contains("NativePlungeRules.ForwardFlag(a.AirAttack?1:0,a.SprintAttack,NativePlungeFields.Of(a).Down),time,blocked"),"the forward jump attack's watches never take a plunge");
        Check(sp.Contains("Mathf.Max(viewSway.Sprint,plunge)")&&cg.Contains("if(prefs.cameraShake&&")&&cg.Contains("presentation.StructureShake(cameraView.transform.position,prefs.reducedEffects?.5f:1)"),"the drop takes the first-person screen; the kick follows the camera-shake setting and Reduced effects");
        foreach(var f in new[]{"Assets/ChipArena/Core/NativePlungeStab.cs","Assets/ChipArena/Core/NativePlungePose.cs","Assets/ChipArena/Core/NativePlungeFx.cs"})
        {
            string t=Read(f);foreach(var bad in new[]{"NativeArena","NativePilot","NativeMeleeSwing","ArenaWorld","NativePrediction","Simulation.","Snapshot"})Check(!t.Contains(bad),f+" reads no authority or simulation state ("+bad+")");
        }
        foreach(var f in new[]{"Assets/ChipArena/Core/ArenaWorld.cs","Assets/ChipArena/Core/NativePrediction.cs","Assets/ChipArena/Core/NativeAirAttackSimulation.cs","Assets/ChipArena/Runtime/NativeMelee.cs","Assets/ChipArena/Runtime/NativeNetworkFrame.cs"})
            Check(!Read(f).Contains("NativePlunge"),f+" has no plunge presentation dependency");
        foreach(var key in new[]{"structure.whoosh","impact.ground","move.land","structure.thud","impact.debris"})Check(Read("Assets/ChipArena/Core/NativeSoundBank.cs").Contains("K(\""+key+"\""),"the cue "+key+" is an existing bank key");
        var cues=new NativePlungeFx.Cue[4];foreach(var spec in Specs)foreach(NativePlungeMoment mo in Enum.GetValues(typeof(NativePlungeMoment))){int n=NativePlungeFx.Sounds(mo,spec,1,cues);for(int i=0;i<n;i++)Check(Read("Assets/ChipArena/Core/NativeSoundBank.cs").Contains("K(\""+cues[i].Key+"\""),spec+" "+mo+": "+cues[i].Key+" is an existing bank key");}
        log.Add("the hooks: the rig, the first-person view (camera pitch), the effects, the destruction kit, the cues and the screen read the mapping; the forward jump attack's watches never take a plunge; the kick keeps the camera-shake setting; the Core files read no authority state and the simulation none of them; every cue is an existing bank key");
    }
}
