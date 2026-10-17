using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// Engine-free checks for the intro rig pose path (Core/NativeIntroMotion.cs: the bake and the weapon-intro layer the rig
// writes through Runtime/NativePoseWriter.cs) and the entrance stage sampler (NativeIntroRules.StageBlend, used by
// NativeSceneMotion.Matrix), on the real Resources data: every intro yields finite joints and part matrices at every sampled
// time; the weapon-intro layer keeps the bake's legs and timing, clears limbs and weapons out of the body, never moves
// anything more abruptly than the bake, holds two-hand grips and carries the shots; no stage voxel is drawn lerping toward
// the bake's identity filler (the 1 m boxes at the pilot's feet) or across a pool slot's change of particle; the gun smoke is
// soft; and nothing non-finite can reach a bone Transform (the Siren's no-windup cut was the console's NaN).
internal static class IntroMotionChecks
{
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Intro motion: "+what);}
    private sealed class Clip{public string Key;public int Frames,Tracks;public float Duration;public float[] Values;}
    private static readonly string[] Points={"left wrist","right wrist","left elbow","right elbow","left weapon end","right weapon end","head","left ankle","right ankle"};
    public static IEnumerable<string> Run(string root)
    {
        var intros=Clips(root,"SourceMotion","intro-");var stages=Clips(root,"SceneMotion","stage-");
        // The pinned banks keep all twenty source bakes (the five retired generic entrances' clips stay in the pinned export,
        // unreferenced); every staged entrance with a bake (the eleven weapon signatures the director re-stages, the four DEV
        // stages) has its clip and stage, and the procedural entrances and the Bow's Arrowfall (keyed natively) need none.
        Check(intros.Count==20&&stages.Count==20,"twenty intro clips and stages");
        var baked=NativeIntroRules.Entrances.Where(s=>Array.IndexOf(NativeIntroRules.Procedural,s)<0&&s!=NativeBowEntrance.Id).ToArray();Check(baked.Length==15,"fifteen baked entrances");
        foreach(var s in baked)Check(intros.ContainsKey("intro-"+s)&&stages.ContainsKey("stage-"+s),s+" has its clip and stage");
        var weapons=baked.Where(s=>NativeIntroMotion.WeaponIntro(s)>=0).ToArray();Check(weapons.Length==11,"eleven weapon intros");
        // NativeSourceMotion prepares every weapon intro's plan as it loads the clips; the checks run on prepared plans.
        foreach(var s in weapons){var c=intros["intro-"+s];NativeIntroMotion.Prepare(s,c.Values,c.Frames,c.Duration);}
        var pose=new CapturedRig.Pose();var bake=new CapturedRig.Pose();long samples=0;
        // 1. Finite joints and part matrices: every baked frame time, midway between frames, one ulp either side, the ends,
        // past the end (the preview tail), before the start, a match clock on 1/60 s ticks, and non-finite clocks.
        foreach(var s in baked)
        {
            var c=intros["intro-"+s];var ages=new List<float>{0,-0f,-1,c.Duration,c.Duration+.7f,1e6f,float.NaN,float.PositiveInfinity,float.NegativeInfinity};
            for(int f=0;f<c.Frames;f++){float at=f*c.Duration/(c.Frames-1);ages.Add(at);ages.Add(MathF.BitIncrement(at));ages.Add(MathF.BitDecrement(at));ages.Add(at+.5f*c.Duration/(c.Frames-1));}
            for(double t=37.25;t-37.25<c.Duration;t+=1/60.0)ages.Add((float)(t-37.25));
            foreach(float age in ages)
            {
                NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age,pose);Check(NativeIntroMotion.Finite(pose),s+" non-finite joint at "+age);pose.World();
                for(int b=0;b<22;b++)Check(Finite(pose.WorldPosition[b])&&Finite(pose.WorldRotation[b])&&Math.Abs(pose.WorldRotation[b].Length()-1)<1e-3f,s+" non-finite world joint "+b+" at "+age);
                var grip=NativeIntroRules.IntroGrip(s);foreach(int hand in new[]{CapturedRig.LHand,CapturedRig.RHand}){var p=pose.WorldPosition[hand]+Vector3.Transform(grip,pose.WorldRotation[hand]);Check(Finite(p),s+" non-finite weapon part at "+age);}
                samples++;
            }
        }
        yield return "intro poses: "+samples+" samples of all 15 baked entrances (every baked frame, midpoints, +-1 ulp, ends, preview tail, 1/60 s match ticks, NaN/Inf clocks) give finite joints, world matrices and weapon parts";
        // 2. The layer only touches the weapon intros, and there only the arms, spine, chest and head: the root, hips and
        // legs are the bake's own at every sample (planted feet never slide); it is deterministic and allocation-free.
        long legs=0;
        foreach(var s in baked)
        {
            var c=intros["intro-"+s];bool weapon=NativeIntroMotion.WeaponIntro(s)>=0;
            for(int i=0;i<=120;i++)
            {
                float age=i*c.Duration/120;NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age,pose);NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,age,bake);
                if(!weapon){Check(Same(pose,bake,0,22),s+" is not a weapon intro and must stay the bake");continue;}
                Check(Same(pose,bake,0,2)&&Same(pose,bake,CapturedRig.LHip,22),s+" moves the root, hips or legs at "+age.ToString("0.00")+" s");legs++;
            }
        }
        {
            // A plan built afresh from a copy of the clip gives the same poses as the prepared one.
            foreach(var s in weapons)
            {
                var c=intros["intro-"+s];var copy=(float[])c.Values.Clone();var prepared=new List<CapturedRig.Pose>();
                for(float age=0;age<c.Duration;age+=.11f){var p=new CapturedRig.Pose();NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age,p);prepared.Add(p);}
                int n=0;for(float age=0;age<c.Duration;age+=.11f){NativeIntroMotion.Pose(s,copy,c.Frames,c.Duration,age,pose);Check(Same(pose,prepared[n++],0,22),s+" is not deterministic at "+age.ToString("0.00")+" s");}
                NativeIntroMotion.Prepare(s,c.Values,c.Frames,c.Duration);
            }
            var all=baked.Select(s=>(style:s,clip:intros["intro-"+s])).ToArray();
            foreach(var (s,c) in all)NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,0,pose);
            long before=GC.GetAllocatedBytesForCurrentThread();
            foreach(var (s,c) in all)for(float age=0;age<c.Duration;age+=1/60f)NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age,pose);
            long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"the intro pose allocates "+bytes+" bytes over a playthrough");
        }
        yield return $"weapon intros: {legs} samples keep the bake's root, hips and legs exactly; prepared plans equal fresh ones; 0 bytes allocated over 60 Hz playthroughs of all 15 baked entrances";
        // 3. Clearance: no forearm, hand or held weapon sinks deeper than baked (beyond 1 cm, or 2.5 cm of contact), and none sinks more than 2.5 cm
        // into the torso or head wherever its hand is free to move (not anchoring a baked effect; the greatsword's two-hand
        // drag, wind-up and slam stay as baked until the hoist).
        double worstBefore=0,worstAfter=0;int freeSamples=0;var report=new List<string>();
        foreach(var s in weapons)
        {
            var c=intros["intro-"+s];double sb=0,sa=0;
            for(int i=0;i<=480;i++)
            {
                float age=i*c.Duration/480;NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,age,bake);NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age,pose);
                float before=NativeIntroMotion.Clearance(s,bake,out _),after=NativeIntroMotion.Clearance(s,pose,out string what);sb=Math.Max(sb,before);sa=Math.Max(sa,after);
                Check(after<=Math.Max(before+.01f,.025f),s+" "+what+" sinks deeper than baked at "+age.ToString("0.00")+" s ("+(after*100).ToString("0.0")+" vs "+(before*100).ToString("0.0")+" cm)");
                for(int limb=0;limb<2;limb++)
                {
                    float anchored=Math.Max(NativeIntroMotion.Held(s,age,false),NativeIntroMotion.Held(s,age,true));if(anchored>.02f)continue;
                    float d=NativeIntroMotion.Clearance(s,pose,out what,limb);Check(d<=.025f,s+" "+what+" sinks "+(d*100).ToString("0.0")+" cm into the body at "+age.ToString("0.00")+" s");freeSamples++;
                }
            }
            worstBefore=Math.Max(worstBefore,sb);worstAfter=Math.Max(worstAfter,sa);report.Add($"{s.Substring(7)} {sb*100:0.0}->{sa*100:0.0}");
        }
        yield return $"weapon intros: limbs and weapons never sink deeper than baked, and at most 2.5 cm wherever the hand is free ({freeSamples} limb samples); worst depth, cm: "+string.Join(", ",report);
        // 4. Smoothness: wrists, elbows, the weapons' far ends, the head and the ankles never accelerate more than 60 m/s^2
        // beyond the bake (60 Hz second differences), except through a gun's own shot (the designed recoil impulse).
        double worstExcess=0;string excessAt="";
        foreach(var s in weapons)
        {
            var c=intros["intro-"+s];float hit=NativeIntroMotion.Hit(s);var p=new[]{new CapturedRig.Pose(),new CapturedRig.Pose(),new CapturedRig.Pose()};var q=new[]{new CapturedRig.Pose(),new CapturedRig.Pose(),new CapturedRig.Pose()};const float dt=1/60f;
            bool gun=s=="weapon-pistol"||s=="weapon-rifle"||s=="weapon-machinegun";
            for(float age=.05f;age<c.Duration-.05f;age+=dt)
            {
                if(gun&&age>hit-.03f&&age<hit+.2f||s=="weapon-machinegun"&&age>1.9f&&age<2.45f)continue;
                for(int k=0;k<3;k++){NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age+(k-1)*dt,p[k]);p[k].World();NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,age+(k-1)*dt,q[k]);q[k].World();}
                for(int k=0;k<Points.Length;k++)
                {
                    double la=(Point(p[2],k)-2*Point(p[1],k)+Point(p[0],k)).Length()/(dt*dt),ba=(Point(q[2],k)-2*Point(q[1],k)+Point(q[0],k)).Length()/(dt*dt);
                    if(la-ba>worstExcess){worstExcess=la-ba;excessAt=$"{s} {Points[k]} {age:0.00} s";}
                    Check(la<=ba+60,s+" "+Points[k]+" snaps at "+age.ToString("0.00")+" s ("+la.ToString("0")+" vs baked "+ba.ToString("0")+" m/s^2)");
                }
            }
        }
        yield return $"weapon intros: no wrist, elbow, weapon end, head or ankle accelerates more than 60 m/s^2 beyond the bake outside the shots (worst {worstExcess:0}, {excessAt})";
        // 5. Hits: the weapon hand is where the bake has it at each hit (exactly where its effect is anchored).
        foreach(var s in weapons)
        {
            var c=intros["intro-"+s];float hit=NativeIntroMotion.Hit(s);NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,hit,bake);NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,hit,pose);bake.World();pose.World();
            int hand=NativeIntroMotion.Held(s,hit,true)>0?CapturedRig.LHand:CapturedRig.RHand;float limit=Math.Max(NativeIntroMotion.Held(s,hit,false),NativeIntroMotion.Held(s,hit,true))>.99f?.002f:.03f;
            Check((bake.WorldPosition[hand]-pose.WorldPosition[hand]).Length()<=limit,s+" moves the weapon hand at the hit ("+((bake.WorldPosition[hand]-pose.WorldPosition[hand]).Length()*100).ToString("0.0")+" cm)");
        }
        // 6. Grips. Deadeye: the support hand cups the shooting fist from the high ready through the shot and its recoil, the
        // chest squares to the aim and leans in; the shot rides the hand back along the gun, flips the muzzle up and returns.
        double cupped=0,squared=0;
        {
            const string s="weapon-pistol";var c=intros["intro-"+s];float hit=NativeIntroMotion.Hit(s);var cup=NativeIntroMotion.PistolGrip;
            for(float age=hit-.27f;age<=hit+.42f;age+=.01f)
            {
                NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,age,pose);pose.World();var l=Vector3.Transform(pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(pose.WorldRotation[CapturedRig.RHand]));
                double off=(l-cup).Length();cupped=Math.Max(cupped,off);Check(off<.005f,"Deadeye support hand off the grip at "+age.ToString("0.00")+" s ("+(off*1000).ToString("0.0")+" mm)");
            }
            NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,hit,bake);NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,hit,pose);bake.World();pose.World();
            double turnBake=Facing(bake),turnPose=Facing(pose);squared=turnBake-turnPose;Check(squared>.1,"the Deadeye chest squares to the aim ("+turnBake.ToString("0.00")+" -> "+turnPose.ToString("0.00")+" rad off the shooting hand)");
            NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,hit+.06f,bake);NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,hit+.06f,pose);bake.World();pose.World();
            var barrel=Vector3.Transform(Vector3.UnitZ,bake.WorldRotation[CapturedRig.RHand]);float back=Vector3.Dot(pose.WorldPosition[CapturedRig.RHand]-bake.WorldPosition[CapturedRig.RHand],barrel);
            Check(back<-.015f,"the Deadeye shot rides the hand back along the gun ("+(back*100).ToString("0.0")+" cm)");
            Check(Vector3.Transform(Vector3.UnitZ,pose.WorldRotation[CapturedRig.RHand]).Y>barrel.Y+.05f,"the Deadeye shot flips the muzzle up");
            NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,hit+.65f,bake);NativeIntroMotion.Pose(s,c.Values,c.Frames,c.Duration,hit+.65f,pose);bake.World();pose.World();
            Check((pose.WorldPosition[CapturedRig.RHand]-bake.WorldPosition[CapturedRig.RHand]).Length()<.01f,"the Deadeye recoil settles back to the aim");
        }
        // Longwatch: the off hand rides the fore-end (its 1.9 s brace) from 1.3 s instead of pressing into the chest and
        // snapping onto it; Belt Rattle and the greatsword keep the bake's brace exactly whatever moves the weapon.
        double foreEnd=0,brace=0;
        {
            var c=intros["intro-weapon-rifle"];NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,1.9f,bake);bake.World();var gi=Quaternion.Inverse(bake.WorldRotation[CapturedRig.RHand]);var want=Vector3.Transform(bake.WorldPosition[CapturedRig.LHand]-bake.WorldPosition[CapturedRig.RHand],gi);
            for(float age=1.3f;age<c.Duration;age+=.02f)
            {
                // From the shot on the bake braces the rifle itself (braceWeapon), at the same grip, and the layer follows it.
                NativeIntroMotion.Pose("weapon-rifle",c.Values,c.Frames,c.Duration,age,pose);pose.World();var l=Vector3.Transform(pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(pose.WorldRotation[CapturedRig.RHand]));
                if(age>=1.8f){NativeIntroMotion.Sample(c.Values,c.Frames,c.Duration,age,bake);bake.World();want=Vector3.Transform(bake.WorldPosition[CapturedRig.LHand]-bake.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(bake.WorldRotation[CapturedRig.RHand]));}
                double off=(l-want).Length();foreEnd=Math.Max(foreEnd,off);Check(off<.005f,"Longwatch off hand off the fore-end at "+age.ToString("0.00")+" s ("+(off*1000).ToString("0.0")+" mm)");
            }
            foreach(var s in new[]{"weapon-greatsword","weapon-machinegun"})
            {
                var g=intros["intro-"+s];
                for(float age=0;age<g.Duration;age+=.02f)
                {
                    NativeIntroMotion.Sample(g.Values,g.Frames,g.Duration,age,bake);NativeIntroMotion.Pose(s,g.Values,g.Frames,g.Duration,age,pose);bake.World();pose.World();
                    var a=Vector3.Transform(bake.WorldPosition[CapturedRig.LHand]-bake.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(bake.WorldRotation[CapturedRig.RHand]));
                    var b=Vector3.Transform(pose.WorldPosition[CapturedRig.LHand]-pose.WorldPosition[CapturedRig.RHand],Quaternion.Inverse(pose.WorldRotation[CapturedRig.RHand]));
                    double off=(a-b).Length();brace=Math.Max(brace,off);Check(off<.01f,s+" support hand leaves its brace at "+age.ToString("0.00")+" s ("+(off*1000).ToString("0.0")+" mm)");
                }
            }
        }
        yield return $"weapon intros: hands at the bake at every hit; Deadeye cups its grip from the high ready through the recoil (worst {cupped*1000:0.0} mm), squares {squared:0.00} rad to the aim, kicks back and settles; Longwatch rides its fore-end from 1.3 s ({foreEnd*1000:0.0} mm); Belt Rattle and the greatsword hold their brace ({brace*1000:0.0} mm)";
        // 7. Stage voxels: a drawn track (shown at its frame) never moves toward a hidden next row (the identity filler) nor
        // jumps between pool particles; the old frame-lerp would have drawn full-size boxes.
        int tracks=0,held=0,oldBoxes=0,newBoxes=0;long drawn=0;
        foreach(var st in stages.Values)
        {
            for(int t=0;t<st.Tracks;t++)
            {
                bool voxel=NativeIntroRules.TintVaries(st.Values,st.Frames,t);if(!voxel)continue;tracks++;
                for(int f=0;f+1<st.Frames;f++)
                {
                    int a0=(t*st.Frames+f)*20,b0=a0+20;if(!(st.Values[a0+16]>.001f))continue;float sa=Scale(st.Values,a0);
                    foreach(float u in new[]{.3f,.6f,.9f})
                    {
                        float age=(f+u)*st.Duration/(st.Frames-1);float blend=NativeIntroRules.StageBlend(st.Values,st.Frames,t,st.Duration,age,true,out int a,out int b);drawn++;
                        Check(a==a0&&b==b0,st.Key+" samples the wrong rows");
                        if(!(st.Values[b0+16]>.001f)){Check(blend==0,st.Key+" track "+t+" lerps toward a hidden row");held++;}
                        float ns=Lerp(st.Values,a,b,blend,out var np),os=Lerp(st.Values,a0,b0,u,out var op);
                        if(os>Math.Max(.45f,2*sa)&&op.Length()<.6f)oldBoxes++;if(ns>Math.Max(.45f,2*sa)&&np.Length()<.6f)newBoxes++;
                        Check(ns<=Math.Max(sa,Scale(st.Values,b0))+1e-4f,st.Key+" voxel grows past both rows");
                        if(blend>0)Check((np-new Vector3(st.Values[a0+12],st.Values[a0+13],st.Values[a0+14])).Length()<=.35f+1e-4f,st.Key+" voxel slides across the stage");
                    }
                }
            }
        }
        Check(newBoxes==0&&oldBoxes>0,"stage voxels drawn as full-size boxes at the origin: "+newBoxes+" (frame-lerp drew "+oldBoxes+")");
        yield return $"entrance stages: {tracks} voxel tracks, {drawn} drawn samples; {held} hold their row before hiding; full-size boxes at the pilot's feet {oldBoxes} -> {newBoxes}";
        // 8. Gun smoke and dust are soft, densest at the muzzle and thinning as they darken; saturated voxels stay opaque;
        // the rule is only for the gun intros.
        float young=NativeIntroRules.SoftVoxelAlpha(.323f,.341f,.381f),spent=NativeIntroRules.SoftVoxelAlpha(.042f,.047f,.058f),dust=NativeIntroRules.SoftVoxelAlpha(.147f,.155f,.181f);
        Check(young>.6f&&young<=.7f&&spent>.15f&&spent<.3f&&dust>spent&&dust<young&&NativeIntroRules.SoftVoxelAlpha(0,0,0)==0,"smoke alpha "+young+" -> "+spent+", dust "+dust);
        Check(NativeIntroRules.SoftVoxelAlpha(.69f,.41f,.13f)==1&&NativeIntroRules.SoftVoxelAlpha(1f,.45f,.05f)==1,"brass and embers stay opaque");
        Check(NativeIntroRules.SoftSmoke("weapon-pistol")&&NativeIntroRules.SoftSmoke("weapon-rifle")&&NativeIntroRules.SoftSmoke("weapon-machinegun")&&!NativeIntroRules.SoftSmoke("weapon-sword")&&!NativeIntroRules.SoftSmoke("dev-siren"),"soft smoke on the gun stages only");
        yield return $"gun smoke: alpha {young:0.00} at the muzzle -> {spent:0.00} spent (dust {dust:0.00}); brass and embers opaque";
        // 9. Nothing non-finite reaches a bone. The Siren's cuts have no windup (weapon windup 0, so VisualWindup 0): their
        // clip time came out NaN, SampleAnimation wrote NaN joints and the pose history kept them, so every bone write was
        // rejected each frame. Every captured cut now retimes to a finite clip time for a no-windup attack, the base-pose
        // history recovers from a NaN frame or clock, and every Runtime bone write goes through NativePoseWriter.
        int cuts=0;
        foreach(var set in CapturedClips.Sets)foreach(var clip in set)
        {
            if(!clip.StateName.StartsWith("cut",StringComparison.Ordinal))continue;cuts++;
            foreach(float duration in new[]{.6f,.16f,.1f,.017f})
            {
                float recovery=duration-.16f;for(float age=-.05f;age<duration+.1f;age+=.004f)Check(MathUtil.Finite(clip.AttackTime(age,0,.16f,recovery)),clip.Name+" retimes a no-windup cut to NaN at "+age+" (duration "+duration+")");
            }
        }
        Check(cuts>=10,"captured cuts found");
        foreach(var marks in new[]{new[]{0f,.2f,.4f,.6f,.8f,1f},new[]{0f,.1f,.1f,.5f,.9f,1f}})
        {
            var at=new[]{0f,0f,0f,0f,.16f,.16f};for(float t=-.1f;t<.4f;t+=.01f)Check(MathUtil.Finite(CapturedClip.Retime(at,marks,t)),"zero-length cut spans retime to NaN at "+t);
        }
        var history=new NativePoseHistory();var hp=new CapturedRig.Pose();history.BlendBase(hp,1,false);hp.Position[3]=new Vector3(float.NaN,0,0);history.BlendBase(hp,1.016f,false);
        hp.Reset();history.BlendBase(hp,1.033f,false);Check(NativeIntroMotion.Finite(hp),"a NaN frame latches into the pose history");
        hp.Reset();history.BlendBase(hp,float.NaN,false);hp.Reset();history.BlendBase(hp,1.05f,false);Check(NativeIntroMotion.Finite(hp),"a NaN clock latches into the pose history");
        int writers=0;string runtime=Path.Combine(root,"Assets/ChipArena/Runtime");
        foreach(var file in Directory.GetFiles(runtime,"*.cs"))
        {
            if(Path.GetFileName(file)=="NativePoseWriter.cs"){writers++;continue;}
            foreach(var line in File.ReadLines(file))Check(!(line.Contains("].localPosition=")&&line.Contains("Mirror(")),Path.GetFileName(file)+" writes a pose to bones outside NativePoseWriter");
        }
        Check(writers==1,"NativePoseWriter present");
        yield return $"pose writes: {cuts} captured cuts retime a no-windup attack (the Siren's) to a finite clip time, zero-length cut spans stay finite, the base-pose history recovers from NaN frames and clocks, and every Runtime bone write goes through NativePoseWriter";
    }
    // How far (radians) the chest's forward is turned away from the shooting hand, seen from above.
    private static double Facing(CapturedRig.Pose p)
    {
        var f=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.Chest]);var h=p.WorldPosition[CapturedRig.RHand]-p.WorldPosition[CapturedRig.Chest];
        return Math.Abs(Math.Atan2(f.X*h.Z-f.Z*h.X,f.X*h.X+f.Z*h.Z));
    }
    private static Vector3 Point(CapturedRig.Pose q,int k)
    {
        Vector3 End(int hand)=>q.WorldPosition[hand]+Vector3.Transform(new Vector3(0,0,.4f),q.WorldRotation[hand]);
        switch(k){case 0:return q.WorldPosition[CapturedRig.LHand];case 1:return q.WorldPosition[CapturedRig.RHand];case 2:return q.WorldPosition[CapturedRig.LElbow];case 3:return q.WorldPosition[CapturedRig.RElbow];
            case 4:return End(CapturedRig.LHand);case 5:return End(CapturedRig.RHand);case 6:return q.WorldPosition[CapturedRig.Head];case 7:return q.WorldPosition[CapturedRig.LFoot];default:return q.WorldPosition[CapturedRig.RFoot];}
    }
    private static float Scale(float[] v,int o)=>MathF.Sqrt(v[o]*v[o]+v[o+1]*v[o+1]+v[o+2]*v[o+2]);
    private static float Lerp(float[] v,int a,int b,float t,out Vector3 at)
    {
        at=new Vector3(v[a+12]+(v[b+12]-v[a+12])*t,v[a+13]+(v[b+13]-v[a+13])*t,v[a+14]+(v[b+14]-v[a+14])*t);float x=v[a]+(v[b]-v[a])*t,y=v[a+1]+(v[b+1]-v[a+1])*t,z=v[a+2]+(v[b+2]-v[a+2])*t;return MathF.Sqrt(x*x+y*y+z*z);
    }
    private static bool Same(CapturedRig.Pose a,CapturedRig.Pose b,int from,int to){for(int i=from;i<to;i++)if(a.Position[i]!=b.Position[i]||a.Rotation[i]!=b.Rotation[i])return false;return true;}
    private static bool Finite(Vector3 v)=>MathUtil.Finite(v.X)&&MathUtil.Finite(v.Y)&&MathUtil.Finite(v.Z);
    private static bool Finite(Quaternion q)=>MathUtil.Finite(q.X)&&MathUtil.Finite(q.Y)&&MathUtil.Finite(q.Z)&&MathUtil.Finite(q.W);
    private static Dictionary<string,Clip> Clips(string root,string name,string prefix)
    {
        using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/"+name+".json")));
        using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
        using var doc=JsonDocument.Parse(reader.ReadToEnd());var clips=new Dictionary<string,Clip>();
        foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
        {
            string key=c.GetProperty("key").GetString();if(!key.StartsWith(prefix,StringComparison.Ordinal))continue;var values=c.GetProperty("values");var v=new float[values.GetArrayLength()];int i=0;
            foreach(var x in values.EnumerateArray())v[i++]=x.ValueKind==JsonValueKind.Number?(float)x.GetDouble():0;
            clips[key]=new Clip{Key=key,Frames=c.GetProperty("frames").GetInt32(),Tracks=c.TryGetProperty("tracks",out var tr)?tr.GetInt32():22,Duration=(float)c.GetProperty("duration").GetDouble(),Values=v};
        }
        return clips;
    }
}
