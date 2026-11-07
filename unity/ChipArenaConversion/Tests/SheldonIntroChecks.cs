using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using ChipArena.UnityConversion;

// The DEV Sheldon Intro (Core/NativeSheldonIntro.cs): its DEV gate on real NativeAccess values (live DEV access only, as the
// Sheldon Head), its replication through the existing intro id, its music key and timing, the beat marks the glitch, the
// rez-in, the figure, the face and the camera land on, the Sheldon Idle entrance (frozen stillness, snaps on the beats, the
// overclocked projection skipping place, and from the hand-off exactly the Sheldon Idle, frame for frame), its set
// (built once, cheaper tiers, dimmer than the face), determinism under scrubbing, the reduced-effects tier (every beat kept, nothing strobing)
// and the low graphics tier, the glitch-typed card text, and source scans of the runtime hooks (Runtime is not compiled here). The
// general entrance rules (camera clearance and smoothness, purity, budgets, no allocation) run on it in EntranceDirectorChecks.
internal static class SheldonIntroChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Sheldon Intro: "+what);count++;}
    private static NativeAccess Access(bool developer,double expires,params string[] grants)
    {
        var a=(NativeAccess)Activator.CreateInstance(typeof(NativeAccess),true);var t=typeof(NativeAccess);
        t.GetProperty("Developer").SetValue(a,developer);t.GetProperty("Expires").SetValue(a,expires);
        var set=(HashSet<string>)t.GetField("grants",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(a);foreach(var g in grants)set.Add(g);return a;
    }
    public static IEnumerable<string> Run(string root)
    {
        const string S=NativeSheldonIntro.Style;double now=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        // 1. The gate: live DEV access only; a grant, an expired DEV token or a guest falls back to the weapon signature.
        var dev=Access(true,now+60000);var granted=Access(false,now+60000,"intro:"+S);var expired=Access(true,now-1000);
        Check(S=="sheldon-intro"&&NativeCosmeticGate.DevOnly(S)&&NativeCosmeticGate.Restricted(S),"the id is DEV-only");
        Check(NativeCosmeticGate.Allowed(dev,"intro",S,now)&&!NativeCosmeticGate.Allowed(granted,"intro",S,now)&&!NativeCosmeticGate.Allowed(expired,"intro",S,now)&&!NativeCosmeticGate.Allowed(NativeAccess.Guest,"intro",S,now)&&!NativeCosmeticGate.Allowed(null,"intro",S,now),"only live DEV access may pick it");
        foreach(var (a,ok) in new[]{(dev,true),(granted,false),(expired,false),(NativeAccess.Guest,false)})
        {
            var seat=new NativeSeat{spec="sword",intro=S,overrideIntro=true,access=a};Check(NativeIntroRules.Style(seat)==(ok?S:"weapon-sword"),"the host stages it for "+(ok?"DEV":"a non-DEV seat")+" ("+NativeIntroRules.Style(seat)+")");
        }
        {string intro=S;bool on=true;NativeIntroRules.Migrate(ref intro,ref on);Check(intro==S&&on,"a saved or received pick survives migration");}
        Check(NativeIntroRules.Pickable(S)&&NativeIntroRules.Entrance(S,"pistol")==S&&Array.IndexOf(NativeIntroRules.Entrances,S)<0&&NativeEntranceDirector.Directed(S),"every client stages the replicated id (the director), outside the public entrance list");
        Check(NativeIntroRules.Duration(S)==4.8&&NativeIntroRules.NameAt(S)==3.2&&NativeIntroRules.OverlayDuration(S)==4.8&&NativeIntroRules.IntroWeapon(S)==null&&NativeIntroRules.IntroSong(S)==null,"standard timing (4.8 s, name card 3.2 s), empty hands, no boss song");
        Check(NativeSoundKeys.Intro(S)=="intro.sheldon-intro"&&NativeSoundKeys.Find("intro.sheldon-intro",out var key)&&Math.Abs(key.Cap-4.8f)<1e-5f&&!key.Key.Contains("dev-"),"its music key (2D, 4.8 s; no dev- prefix)");
        Check(File.Exists(Path.Combine(root,"Assets/Conversion/Resources/Audio/"+NativeIntroRules.IntroSting(S)+".mp3")),"a built-in sting without LocalAssets");
        Check(NativeLocker.DevItem(S)&&NativeLocker.IntroText(S).Length>0&&NativeLocker.IntroIcon(S)=="scan-line"&&NativeIntroRules.StyleName(S)=="DEV · Sheldon Intro","the Locker card (DEV badge, text, icon)");
        yield return $"Sheldon Intro gate: live DEV access only (grants, expired tokens and guests get their weapon signature); every client stages the replicated id; standard 4.8 s timing; music key intro.sheldon-intro";

        // 2. The beat marks: the music's moments (s from the intro start: the opening of the owner's track, re-cut 2026-10-12),
        // and what lands on each.
        var marks=new[]{.15f,.27f,.39f,.51f,.86f,1.05f,1.09f,1.20f,1.21f,1.56f,1.83f,2.17f,2.38f,2.44f,3.32f,3.79f,4.26f};
        Check(NativeSheldonIntro.Beats.SequenceEqual(marks)&&marks.Zip(marks.Skip(1),(a,b)=>a<b).All(x=>x),"the beat marks are the music's (in order)");
        var ticks=NativeSheldonIntro.Ticks;
        Check(ticks.SequenceEqual(new[]{.15f,.27f,.39f,.51f})&&ticks[0]==NativeSheldonIntro.FirstTick&&ticks[3]==NativeSheldonIntro.Slam&&ticks.Zip(ticks.Skip(1),(a,b)=>Math.Abs(b-a-.12f)<1e-4f).All(x=>x),"four glitch ticks .12 s apart, the last the slam");
        var beats=NativeSheldonIntro.Downbeats;
        Check(beats.SequenceEqual(new[]{2.38f,2.85f,3.32f,3.79f,4.26f,4.73f})&&beats[0]==NativeSheldonIntro.Resolve&&beats.Zip(beats.Skip(1),(a,b)=>Math.Abs(b-a-NativeSheldonIntro.Downbeat)<1e-4f).All(x=>x)
            &&NativeSheldonIntro.BuildFrom==beats[2]&&NativeSheldonIntro.BuildTo==beats[3]&&NativeSheldonIntro.Flare==beats[4]&&NativeSheldonIntro.Stutter==.12f&&Math.Abs(NativeSheldonIntro.BreathPeriod-4*NativeSheldonIntro.Downbeat)<1e-5f,"the groove grid (.47 s from the resolve): the build, the flare and the breath on its downbeats, the stutters on its hi-hats");
        Check(NativeSheldonIntro.Cut==4.6f&&NativeSheldonIntro.End==4.8f,"the exit cut and the hand-off");
        var sc=NativeEntranceDirector.Script(S) as NativeSheldonIntroScript;Check(sc!=null&&sc.Ready&&sc.Style==S,"the director makes the script");sc.Reduced=false;sc.Low=false;sc.Prepare();
        for(float age=0;age<=5;age+=1/240f)Check(sc.Clock(age)==Math.Min(age,NativeEntranceScript.Duration+1),"the clock is the age (no slow-motion beat) at "+age);
        NativeSheldonGlitch G(float t,bool r=false,bool low=false)=>NativeSheldonIntro.Glitch(t,r,low);
        // The stepped rez-in: each tick tears the frame and lifts it a step; the bands lock on their ticks (the first draws the outline).
        for(int i=0;i<3;i++)Check(G(ticks[i]+.004f).Tear>G(ticks[i]-.02f).Tear+.3f&&G(ticks[i]+.004f).Black<G(ticks[i]-.02f).Black-.15f,"tick "+ticks[i]+" tears the frame and lifts it a step");
        Check(G(.1f).Black>.75f&&!sc.Holo(.14f,out _,out _,out _)&&sc.Holo(NativeSheldonIntro.FirstTick+.01f,out _,out float outline,out _)&&outline>.1f,"silence, then the outline on the first tick");
        foreach(var (bone,band) in new[]{(CapturedRig.LKnee,1),(CapturedRig.RFoot,1),(CapturedRig.Pelvis,2),(CapturedRig.RHand,2),(CapturedRig.Chest,3),(CapturedRig.LArm,3)})
            Check(NativeSheldonIntroScript.Band(bone)==band&&sc.Solid(ticks[band]-.01f,bone)==0&&sc.Solid(ticks[band]+.004f,bone)==1,"bone "+bone+" locks on tick "+ticks[band]);
        Check(G(NativeSheldonIntro.Slam-.02f).Tear<.35f&&G(NativeSheldonIntro.Slam+.004f).Tear>.9f&&G(NativeSheldonIntro.Slam+.004f).Blocks>.7f,"the slam tears the frame on .51 s");
        Check(G(NativeSheldonIntro.Jolt+.004f).Tear>G(NativeSheldonIntro.Jolt-.02f).Tear+.4f&&G(NativeSheldonIntro.Silence-.02f).Black>.3f,"the jolt on .86 s, the frame darkening into the gap");
        for(float t=NativeSheldonIntro.Silence;t<NativeSheldonIntro.Return;t+=.005f){var g=G(t);Check(g.Black==1&&g.Tear==0&&g.Split==0&&sc.Solid(t,CapturedRig.Chest)==0,"the gap is a black cut frame at "+t);}
        Check(G(NativeSheldonIntro.Click+.005f).Noise>G(NativeSheldonIntro.Click-.01f).Noise+.3f,"the dead pixels flare on the click at 1.09 s");
        Check(G(NativeSheldonIntro.Return+.004f).Tear>.75f&&G(NativeSheldonIntro.Return+.02f).Black>.3f&&G(NativeSheldonIntro.Return+.02f).Black<.7f&&sc.Holo(NativeSheldonIntro.Return+.05f,out _,out float holo,out _)&&holo>.1f,"the hard re-entry on 1.21 s: a torn, dim frame and a projection");
        Check(G(NativeSheldonIntro.SwellPeak).Chroma>G(NativeSheldonIntro.SwellFrom-.02f).Chroma+.2f&&NativeSheldonIntro.Swell(NativeSheldonIntro.SwellPeak)==1,"the glitch thickens to the swell's peak");
        Check(G(NativeSheldonIntro.Resolve+.004f).Tear>.75f&&G(NativeSheldonIntro.Resolve+.004f).Black<.05f&&G(NativeSheldonIntro.Resolve+.004f).Chroma>.9f,"the resolve on the first downbeat, 2.38 s");
        for(float t=0;t<NativeSheldonIntro.Resolve;t+=1/240f)Check(sc.Solid(t,CapturedRig.Head)<=.5f||!(t>=NativeSheldonIntro.Return)&&t>NativeSheldonIntro.Slam-.03f&&t<NativeSheldonIntro.Silence,"the hood stays off until the resolve (but for glitch flickers before the gap) at "+t);
        for(int b=0;b<22;b++)Check(sc.Solid(NativeSheldonIntro.Resolve+.001f,b)==1&&sc.Solid(4.79f,b)==1,"the figure resolves solid on 2.38 s (bone "+b+")");
        float build0=G(NativeSheldonIntro.BuildFrom-.05f).Chroma,build=G((NativeSheldonIntro.BuildFrom+NativeSheldonIntro.BuildTo)*.5f).Chroma;Check(build>build0+.3f,"the push-in's chromatic aberration ("+build0+" -> "+build+")");
        Check(G(NativeSheldonIntro.Flare+.004f).Split>.7f&&G(NativeSheldonIntro.Flare+NativeSheldonIntro.Stutter+.004f).Split>.55f&&G(NativeSheldonIntro.Flare+.06f).Split<G(NativeSheldonIntro.Flare+.004f).Split,"the flare's three stutters from 4.26 s");
        Check(G(NativeSheldonIntro.Cut-.05f).Black<.3f&&G(4.799f).Black>.95f&&G(4.75f).Tear>.7f,"the final glitch cut to black at the hand-off");
        var rest=NativeSheldonIntro.Face(3.0f,false);float jaw=0,wide=0;for(float t=NativeSheldonIntro.Flare;t<NativeSheldonIntro.Flare+.55f;t+=1/240f){var f=NativeSheldonIntro.Face(t,false);jaw=Math.Max(jaw,f.JawOpen);wide=Math.Max(wide,f.GrinWide);}
        Check(jaw>.6f&&wide>rest.GrinWide+.12f&&NativeSheldonIntro.Face(4.79f,false).JawOpen<.3f,"the grin flares in stutters on 4.26 s and closes (jaw "+jaw+", width "+wide+")");
        Check(NativeSheldonIntro.Face(NativeSheldonIntro.BuildTo,false).GrinWide>rest.GrinWide+.05f,"the grin builds through the name");
        var snap=Enumerable.Range(0,80).Select(i=>NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+i/240f,false).RingGrow).Distinct().Count();
        Check(NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+.005f,false).RingGrow>.2f&&NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+.3f,false).RingGrow==0&&snap==5,"the ring clicks into place in four steps ("+snap+" values)");
        Check(NativeSheldonIntro.Face(NativeSheldonIntro.Resolve+.01f,false).Glow>1.8f,"the face flashes on as it snaps on");
        // The push-in: square on the hood, closing in from the build to the hand-off (a bust of the Sheldon Idle).
        sc.Camera(NativeSheldonIntro.BuildFrom+.01f,out var v0);sc.Camera(NativeEntranceScript.Settle,out var v1);var head0=sc.Anchor(NativeEntranceAnchor.Head,NativeSheldonIntro.BuildFrom+.01f);var head1=sc.Anchor(NativeEntranceAnchor.Head,NativeEntranceScript.Settle);
        float d0=(v0.Eye-head0).Length(),d1=(v1.Eye-head1).Length(),prev=99;
        for(float t=NativeSheldonIntro.BuildFrom+.01f;t<=NativeEntranceScript.Settle;t+=1/60f){sc.Camera(t,out var v);float d=(v.Eye-head0).Length();Check(d<=prev+1e-4f,"the push-in never backs off at "+t);prev=d;}
        Check(d1<d0*.85f&&v1.Fov<v0.Fov,"the push-in closes in ("+d0.ToString("0.00")+" -> "+d1.ToString("0.00")+" m, fov "+v0.Fov+" -> "+v1.Fov+")");
        var facing=StareForward(sc,NativeSheldonIntro.Resolve+.2f);var toEye=v1.Eye-head1;Check(Vector3.Dot(Vector3.Normalize(new Vector3(facing.X,0,facing.Z)),Vector3.Normalize(new Vector3(toEye.X,0,toEye.Z)))>.97f,"the push-in ends square on the face");
        yield return $"Sheldon Intro beats: ticks .15/.27/.39 and slam .51 (outline, legs, waist and hands, chest and arms), jolt .86, black 1.05-1.20 (click 1.09), re-entry 1.21, rise 1.56, swell 1.83-2.44 (peak 2.17), resolve 2.38 on the groove (.47 s downbeats), build 3.32-3.79 (push-in {d0:0.00} -> {d1:0.00} m), flare 4.26 (stutters .12 s), cut to black by 4.8";

        // 2b. The Sheldon Idle entrance (owner 2026-10-13: "utilize an animation fitting for the "Sheldon Idle" glitchy, psycho
        // entrance"): the figure is the Sheldon Idle throughout (CyberLordIdlePose.Stand), frozen still through the rez-in,
        // the slam and the stillness after the resolve, snapping on the beats, overclocked in stop motion and skipping place as
        // the projection, and from HandOff exactly the Sheldon Idle on a continuous clock. Both tiers.
        string psycho="";
        {
            var pose=new CapturedRig.Pose();var idle=new CapturedRig.Pose();var rig=new MeleeRig();
            Quaternion HeadQ(float t){sc.Pose(t,pose,out _,out var rot);pose.World();return NativeEntrancePose.WorldRotation(pose,rot,CapturedRig.Head);}
            Vector3 RootAt(float t){sc.Pose(t,pose,out var root,out _);return root;}
            float Turn(Quaternion a,Quaternion b){var d=Quaternion.Inverse(a)*b;return 2*(float)Math.Asin(Math.Min(1,new Vector3(d.X,d.Y,d.Z).Length()));}
            float Worst(CapturedRig.Pose a,CapturedRig.Pose b){float w=0;for(int i=0;i<22;i++){w=Math.Max(w,(a.Position[i]-b.Position[i]).Length());w=Math.Max(w,Turn(a.Rotation[i],b.Rotation[i]));}return w;}
            var snaps=new float[3];var stills=new float[2];
            foreach(bool reduced in new[]{false,true})
            {
                sc.Reduced=reduced;
                // The hand-off: from HandOff the pose, bone for bone and bit for bit, is the Sheldon Idle (an independent Stand on a
                // fresh rig) at the idle clock, which runs at the intro's own rate from Wake; its root is the socket's.
                int matched=0;
                for(float t=NativeSheldonIntroScript.HandOff;t<=NativeEntranceScript.Duration+1;t+=1/240f)
                {
                    sc.Pose(t,pose,out var at,out var rot);float tau=NativeSheldonIntroScript.Still+(t-NativeSheldonIntroScript.Wake);
                    Check(Math.Abs(sc.IdleTime(t)-tau)<1e-5f,"the idle clock runs with the intro's from Wake ("+t+")");
                    CyberLordIdlePose.Stand(rig,sc.IdleTime(t),NativeSheldonIntroScript.Seed,reduced,true,out _);rig.WritePose(idle);
                    for(int b=0;b<22;b++)Check(pose.Position[b]==idle.Position[b]&&pose.Rotation[b]==idle.Rotation[b],"the intro is the Sheldon Idle from the hand-off (bone "+b+" at "+t.ToString("0.000")+")");
                    Check(at==new Vector3(0,NativePackIntro.RootHeight,0)&&rot==NativeEntranceMath.Yaw(NativeSheldonIntroScript.RootYaw),"the idle stands on the socket at "+t);matched++;
                }
                // It blends in: the authored snaps over the idle (the pose against the idle at the same idle time) ease out to
                // nothing by the hand-off, a little each 240 Hz frame (the idle's own twitches and glitch run under them).
                float Off(float t){sc.Pose(t,pose,out _,out _);sc.Idle(sc.IdleTime(t),idle,out _,out _);return Worst(pose,idle);}
                float step=0,offLast=Off(NativeSheldonIntro.BuildFrom+.12f);for(float t=NativeSheldonIntro.BuildFrom+.12f+1/240f;t<NativeSheldonIntroScript.HandOff;t+=1/240f){float o=Off(t);step=Math.Max(step,Math.Abs(o-offLast));offLast=o;}
                Check(step<.03f&&offLast<.002f,"the snaps ease into the hand-off ("+step.ToString("0.0000")+" a 240 Hz frame at most, "+offLast.ToString("0.0000")+" left a frame before)");
                // The stillness: frozen (no breath, no drift) after the slam's throw until the jolt, and from the resolve to Wake.
                var q0=HeadQ(NativeSheldonIntro.Slam+.45f);float still0=0;for(float t=NativeSheldonIntro.Slam+.45f;t<NativeSheldonIntro.Jolt;t+=1/240f)still0=Math.Max(still0,Turn(q0,HeadQ(t)));
                sc.Pose(NativeSheldonIntro.Resolve,idle,out _,out _);float still1=0;for(float t=NativeSheldonIntro.Resolve;t<NativeSheldonIntroScript.Wake;t+=1/240f){sc.Pose(t,pose,out _,out _);still1=Math.Max(still1,Worst(pose,idle));}
                Check(still0<1e-4f&&still1<1e-5f,"deadly still before the jolt and from the resolve to Wake ("+still0+" / "+still1+")");
                Check(sc.IdleTime(NativeSheldonIntro.Resolve)==NativeSheldonIntroScript.Still&&sc.IdleTime(.3f)==NativeSheldonIntroScript.Still,"the stillness holds the idle's frame Still");
                {var z=CyberLordIdlePose.Twitch(NativeSheldonIntroScript.Seed,NativeSheldonIntroScript.Still,reduced);Check(z.Head.Length()<.02f&&z.Flinch<.005f&&Math.Abs(z.Shift)<.004f,"the frozen frame is a calm one");}
                // The snaps: the head hangs limp until the jolt snaps it up, Wake snaps it over, the build snaps it back.
                Check(sc.Limp(.3f)==1&&sc.Limp(NativeSheldonIntro.Jolt+.1f)==0,"the head hangs limp until the jolt");
                {var down=Vector3.Transform(Vector3.UnitZ,HeadQ(.4f));Check(down.Y<-.8f,"limp: the chin on the chest ("+down.Y.ToString("0.00")+")");}
                float jolt=Turn(HeadQ(NativeSheldonIntro.Jolt-.01f),HeadQ(NativeSheldonIntro.Jolt+.1f)),wake=Turn(HeadQ(NativeSheldonIntroScript.Wake-.01f),HeadQ(NativeSheldonIntroScript.Wake+.1f)),snapBuild=Turn(HeadQ(NativeSheldonIntro.BuildFrom-.01f),HeadQ(NativeSheldonIntro.BuildFrom+.1f));
                float jFast=Turn(HeadQ(NativeSheldonIntro.Jolt-.01f),HeadQ(NativeSheldonIntro.Jolt+(reduced?.12f:.06f)))/jolt;
                if(!reduced){snaps[0]=jolt;snaps[1]=wake;snaps[2]=snapBuild;Check(jolt>1&&wake>.35f&&snapBuild>.6f&&jFast>.85f,"the head snaps on the jolt, Wake and the build ("+jolt.ToString("0.00")+" / "+wake.ToString("0.00")+" / "+snapBuild.ToString("0.00")+" rad, "+(jFast*100).ToString("0")+"% in .06 s)");}
                else Check(wake<snaps[1]*.8f&&snapBuild<snaps[2]*.8f&&wake>.15f&&snapBuild>.25f,"reduced: softer snaps ("+wake.ToString("0.00")+" / "+snapBuild.ToString("0.00")+" rad)");
                // The projection: overclocked in stop motion and skipping place (full effects); smooth and in place with reduced effects.
                float hold=0;int skips=0;
                for(float t=NativeSheldonIntro.Return;t<NativeSheldonIntro.Resolve-.01f;t+=1/240f)
                {
                    float jump=(RootAt(t+1/240f)-RootAt(t)).Length();if(jump>.3f)skips++;
                    float frame=(float)Math.Floor((t-NativeSheldonIntro.Return)*NativeSheldonIntroScript.RushFrames),next=(float)Math.Floor((t+1/240f-NativeSheldonIntro.Return)*NativeSheldonIntroScript.RushFrames);
                    if(frame==next&&!reduced){sc.Pose(t,pose,out _,out _);sc.Pose(t+1/240f,idle,out _,out _);for(int b=CapturedRig.Spine;b<=CapturedRig.RHand;b++)if(b!=CapturedRig.Head&&b!=CapturedRig.Neck)hold=Math.Max(hold,Turn(pose.Rotation[b],idle.Rotation[b]));}
                }
                if(!reduced)Check(skips==NativeSheldonIntroScript.SkipAt.Length-1&&(RootAt(NativeSheldonIntro.Return+.01f)-new Vector3(0,NativePackIntro.RootHeight,0)).Length()>.3f&&hold<1e-5f,"full: the projection skips place on the rise and the peak ("+skips+") and holds each 1/"+NativeSheldonIntroScript.RushFrames+" s frame");
                else Check(skips==0&&RootAt(NativeSheldonIntro.Return+.3f)==new Vector3(0,NativePackIntro.RootHeight,0)&&sc.IdleTime(NativeSheldonIntro.Return+.07f)>sc.IdleTime(NativeSheldonIntro.Return+.06f),"reduced: the projection stays in place and runs smoothly");
                Check(sc.IdleTime(NativeSheldonIntro.Resolve-.01f)>NativeSheldonIntroScript.Rush+1.5f,"the projection runs the idle overclocked ("+sc.IdleTime(NativeSheldonIntro.Resolve-.01f).ToString("0.00")+" s of idle in "+(NativeSheldonIntro.Resolve-NativeSheldonIntro.Return).ToString("0.00")+" s)");
                // The anchors the camera and the effects follow are this tier's pose (rebuilt when the tier changes).
                {sc.Pose(1.25f,pose,out var pr,out var pq);pose.World();var want=NativeEntrancePose.Point(pose,pr,pq,CapturedRig.Pelvis,Vector3.Zero);Check((sc.Anchor(NativeEntranceAnchor.Pelvis,1.25f)-want).Length()<1e-4f,"the anchors follow the "+(reduced?"reduced":"full")+" tier's projection");}
                // The idle's own big head jerk lands on the flare (its schedule, seeded for the intro).
                float start=-1;for(float t=NativeSheldonIntroScript.HandOff;t<=NativeEntranceScript.Duration;t+=1/240f)if(CyberLordIdlePose.Twitch(NativeSheldonIntroScript.Seed,sc.IdleTime(t),reduced).Head.Length()>.06f){start=t;break;}
                if(!reduced)Check(Math.Abs(start-NativeSheldonIntro.Flare)<.05f,"the idle's own head jerk lands on the flare ("+start.ToString("0.000")+" s)");
                // The idle's split ghosts after the hand-off, as the match idle's (its glitch, IdleGhost opacity, across the pilot).
                var gh=new NativeEntranceGhost[8];int shown=0;
                for(float t=NativeSheldonIntroScript.HandOff;t<=NativeEntranceScript.Duration;t+=1/240f)
                {
                    var g=sc.IdleGlitch(t);int n=sc.Ghosts(t,gh);bool snapNear=Math.Abs(t-NativeSheldonIntroScript.Wake)<.6f||Math.Abs(t-NativeSheldonIntro.BuildFrom)<.6f;
                    if(NativeSheldonIntroScript.IdleGhost*g.Split>.003f&&!snapNear){Check(n==2&&Math.Abs(gh[0].Opacity-NativeSheldonIntroScript.IdleGhost*g.Split)<1e-5f&&Math.Abs(gh[0].Offset.Length()-g.SplitOffset*.5f)<1e-4f&&gh[0].Lag==0&&gh[1].Lag==0,"the idle's split ghosts at "+t);shown++;}
                    else if(!snapNear)Check(n==0,"no ghost between the idle's split steps at "+t);
                }
                Check(shown>0,"the idle's split ghosts show after the hand-off");
                if(!reduced){stills[0]=still0;stills[1]=still1;psycho=$"hand-off frame-matched to the Sheldon Idle over {matched} samples (to 5.8 s), {step:0.0000} rad/m a 240 Hz frame into it; still before the jolt ({still0:0.0e0}) and from the resolve to Wake ({still1:0.0e0}); snaps {snaps[0]:0.00}/{snaps[1]:0.00}/{snaps[2]:0.00} rad; projection skips {skips}, idle head jerk on {start:0.000} s, {shown} split-ghost samples";}
            }
            sc.Reduced=false;
        }
        yield return "Sheldon Intro psycho entrance (both tiers): "+psycho;

        // 2c. The set (NativeSheldonSet): built once with the script, drawn by reading it (no allocation, no rebuild), cheaper on
        // the reduced and low tiers, no glitch lines but with full effects, and dimmer than the face.
        {
            var set=sc.Set;Check(set!=null&&ReferenceEquals(set,((NativeSheldonIntroScript)NativeEntranceDirector.Script(S)).Set),"the set is the script's (made once with it)");
            var arrays=new object[]{set.StreakR,set.StreakA,set.StreakY,set.StreakColor,set.TracePoints,set.TraceStart,set.ColumnR,set.GlyphWidth,set.DashFrom,set.DashTo};
            var count=new Counter();int[] peak=new int[3];float brightest=0;
            foreach(var (red,low,tier) in new[]{(false,false,0),(true,false,1),(false,true,2)})
            {
                sc.Reduced=red;sc.Low=low;for(float t=0;t<=4.8f;t+=1/30f){count.N=0;sc.DrawSet(t,count,red?.3f:1);if(t>2.6f&&t<4.1f)peak[tier]=Math.Max(peak[tier],count.N);}
                long before=GC.GetAllocatedBytesForCurrentThread();for(float t=0;t<=4.8f;t+=1/60f){count.N=0;sc.DrawSet(t,count,red?.3f:1);}
                long bytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(bytes==0,"drawing the set allocates "+bytes+" bytes (tier "+tier+")");
            }
            sc.Reduced=false;sc.Low=false;var rec=new Recorder();
            for(float t=NativeSheldonIntro.Resolve+.35f;t<NativeSheldonIntro.Flare-.05f;t+=1/30f){rec.Max=0;sc.DrawSet(t,rec,1);brightest=Math.Max(brightest,rec.Max);}
            var newer=new object[]{set.StreakR,set.StreakA,set.StreakY,set.StreakColor,set.TracePoints,set.TraceStart,set.ColumnR,set.GlyphWidth,set.DashFrom,set.DashTo};
            Check(arrays.Zip(newer,ReferenceEquals).All(x=>x),"the set's arrays are never rebuilt");
            Check(peak[1]<peak[0]*.7f&&peak[2]<peak[0]*.5f,"the reduced and low tiers draw a cheaper set ("+peak[0]+" / "+peak[1]+" / "+peak[2]+" pieces)");
            sc.Reduced=true;rec.Lines=0;for(float t=0;t<=4.8f;t+=1/60f)sc.DrawSet(t,rec,.3f);sc.Reduced=false;Check(rec.Lines==0,"no glitch lines with reduced effects ("+rec.Lines+")");
            float face=Recorder.Luma(NativeSheldonIntro.Cyan*NativeSheldonHead.FaceGlow);Check(brightest<face*.6f,"the set stays well under the face's glow in the stare (luminance "+brightest.ToString("0.00")+" vs "+face.ToString("0.00")+")");
            var rig=new NativeEntranceLights();for(float t=0;t<=4.8f;t+=.05f){sc.Lights(t,rig);Check(rig.Background.X+rig.Background.Y+rig.Background.Z<.01f&&rig.Background==rig.Fog,"the void is navy black at "+t);}
            yield return $"Sheldon Intro set: built once with the script and never rebuilt; 0 bytes per frame in every tier; {peak[0]} / {peak[1]} / {peak[2]} pieces in the stare (full / reduced / low); its brightest piece {brightest:0.00} against the face's {Recorder.Luma(NativeSheldonIntro.Cyan*NativeSheldonHead.FaceGlow):0.00} (luminance)";
        }

        // 3. Determinism: pure functions of the age (any order, repeated).
        var ages=Enumerable.Range(0,577).Select(i=>i/120f).ToArray();var first=ages.Select(a=>(G(a),NativeSheldonIntro.Face(a,false),G(a,true),NativeSheldonIntro.Face(a,true))).ToArray();
        var rng=new Random(37);foreach(int i in Enumerable.Range(0,ages.Length).OrderBy(_=>rng.Next())){var a=ages[i];Check(first[i].Equals((G(a),NativeSheldonIntro.Face(a,false),G(a,true),NativeSheldonIntro.Face(a,true))),"the same frame at "+a);}
        var buf=new char[64];var buf2=new char[64];
        for(float t=0;t<1.5f;t+=.01f){int n=NativeSheldonIntro.Typed("SHELDON",t,false,19,buf,out _),m=NativeSheldonIntro.Typed("SHELDON",t,false,19,buf2,out _);Check(n==m&&new string(buf,0,n)==new string(buf2,0,m),"the typed name is deterministic");}
        // Scrubbing: the pose, root, ghosts, reveal, hologram and camera at an age are the same whatever was drawn before it
        // (forward, reversed, shuffled, and with the shared idle scratch and another entrance posed in between).
        int scrubbed=0;
        foreach(bool reduced in new[]{false,true})
        {
            sc.Reduced=reduced;var pose=new CapturedRig.Pose();var gh=new NativeEntranceGhost[8];var other=new MeleeRig();var hero=NativeEntranceDirector.Script("hero-landing");var heroPose=new CapturedRig.Pose();
            string Frame(float a)
            {
                float t=sc.Clock(a);sc.Pose(t,pose,out var root,out var rot);var sb=new System.Text.StringBuilder();sb.Append(root).Append(rot);
                for(int b=0;b<22;b++)sb.Append(pose.Position[b]).Append(pose.Rotation[b]).Append(sc.Solid(t,b));
                int n=sc.Ghosts(t,gh);for(int k=0;k<n;k++)sb.Append(gh[k].Offset).Append(gh[k].Opacity).Append(gh[k].Lag);
                sc.Holo(t,out var hc,out float ho,out var hoff);sc.Camera(a,out var v);sb.Append(ho).Append(hoff).Append(v.Eye).Append(v.Look).Append(v.Fov);return sb.ToString();
            }
            var want=ages.Select(Frame).ToArray();var r2=new Random(reduced?41:43);
            foreach(int i in Enumerable.Range(0,ages.Length).Reverse())Check(Frame(ages[i])==want[i],"reversed scrub to "+ages[i]);
            foreach(int i in Enumerable.Range(0,ages.Length).OrderBy(_=>r2.Next()))
            {
                CyberLordIdlePose.Stand(other,(float)r2.NextDouble()*9,r2.Next(1000),!reduced,true,out _);hero.Pose(hero.Clock((float)r2.NextDouble()*4.8f),heroPose,out _,out _);
                Check(Frame(ages[i])==want[i],"shuffled scrub to "+ages[i]+(reduced?" (reduced)":""));scrubbed++;
            }
        }
        sc.Reduced=false;
        yield return $"Sheldon Intro determinism: {ages.Length} ages evaluated twice in shuffled order give the same glitch and face (both tiers); {scrubbed} shuffled scrubs (plus reversed) give the same pose, root, ghosts, reveal, hologram and camera with the shared idle and another entrance posed in between";

        // 4. Reduced effects: every beat, no strobing (soft steps at 6 Hz, ramped blackouts, no blocks, specks or flicker).
        float worstStep=0,worstFull=0;int flips=0,seeds=0,last=-1;float lastBlack=0,lastDir=0;
        for(int i=0;i<=(int)(4.8f*60);i++)
        {
            float t=i/60f;var r=G(t,true);var f=G(t);
            Check(r.Blocks==0&&r.Noise==0&&r.Tear<=.3f&&r.Split<=.35f&&r.Chroma<=.4f&&r.Scan<=.3f,"reduced glitch stays soft at "+t);
            if(i>0){worstStep=Math.Max(worstStep,Math.Abs(r.Black-lastBlack));float dir=Math.Sign(r.Black-lastBlack);if(dir!=0&&lastDir!=0&&dir!=lastDir)flips++;if(dir!=0)lastDir=dir;}
            worstFull=Math.Max(worstFull,i>0?Math.Abs(f.Black-G((i-1)/60f).Black):0);lastBlack=r.Black;if(r.Seed!=last){seeds++;last=r.Seed;}
        }
        Check(worstStep<=.2f&&worstFull>=.6f,"reduced blackouts ramp ("+worstStep+" a frame at most; full effects cut: "+worstFull+")");
        Check(flips<=6,"reduced blackouts never flicker ("+flips+" turns)");Check(seeds<=30,"reduced glitch steps at 6 Hz ("+seeds+" in 4.8 s)");
        Check(G(NativeSheldonIntro.Slam+.04f,true).Tear>.2f&&G(NativeSheldonIntro.Resolve+.06f,true).Split>.2f&&G(NativeSheldonIntro.Silence+.05f,true).Black>.8f&&G(NativeSheldonIntro.Flare+.08f,true).Split>.2f
            &&G(NativeSheldonIntro.Jolt+.06f,true).Chroma>G(NativeSheldonIntro.Jolt-.02f,true).Chroma+.1f&&G(NativeSheldonIntro.Return+.06f,true).Split>.2f&&ticks.Take(3).All(k=>G(k+.04f,true).Tear>G(k-.01f,true).Tear+.08f),"reduced effects keep every beat (the ticks, the jolt and the re-entry too)");
        // Reduced: the bands fade in on their ticks and never drop out (each bone's solidity only rises until the gap), the
        // RGB ghosts stay close and no light flickers on (point lights only fade out between the slam and the gap).
        sc.Reduced=true;int solidDrops=0;float solidStep=0;
        for(int b=0;b<22;b++){float was=-1;for(float t=NativeSheldonIntro.FirstTick;t<NativeSheldonIntro.Silence-.01f;t+=1/60f){float s=sc.Solid(t,b);if(was>=0){if(s<was-1e-6f)solidDrops++;solidStep=Math.Max(solidStep,s-was);}was=s;}}
        var ghosts=new NativeEntranceGhost[8];float apart=0;for(float t=0;t<4.8f;t+=1/60f){int n=sc.Ghosts(t,ghosts);for(int k=0;k<n;k++)apart=Math.Max(apart,ghosts[k].Offset.Length());}
        var lights=new NativeEntranceLights();int lightTurns=0,lastPoints=-1;for(float t=NativeSheldonIntro.Slam+.02f;t<NativeSheldonIntro.Silence-.02f;t+=1/60f){sc.Lights(t,lights);if(lastPoints>=0&&lights.Points>lastPoints)lightTurns++;lastPoints=lights.Points;}
        sc.Reduced=false;
        Check(solidDrops==0&&solidStep<=.25f&&apart<=.03f&&lightTurns==0,"reduced: a steady build ("+solidDrops+" drop-outs, "+solidStep.ToString("0.00")+" a frame at most), close RGB ghosts ("+apart.ToString("0.000")+" m), no flickering light ("+lightTurns+")");
        foreach(bool r in new[]{false,true}){sc.Reduced=r;for(float t=0;t<4.8f;t+=1/120f)Check(sc.Ghosts(t,ghosts)==0||!sc.Holo(t,out _,out _,out _),"no RGB ghost over the hologram (its depth pass would hide it) at "+t);}sc.Reduced=false;
        int fullChanges=0;float lastSolid=-1;for(float t=NativeSheldonIntro.Slam;t<NativeSheldonIntro.Silence-.01f;t+=1/240f){float s=sc.Solid(t,CapturedRig.Chest);if(lastSolid>=0&&s!=lastSolid)fullChanges++;lastSolid=s;}
        Check(fullChanges>5,"full effects: the figure glitches ("+fullChanges+" drop-outs)");
        // The low graphics tier: one shader stage, fewer set pieces.
        for(float t=0;t<4.8f;t+=.02f){var l=G(t,false,true);Check(l.Passes==1&&l.Blocks==0&&l.Scan==0&&l.Noise==0,"low graphics runs one stage at "+t);}
        Check(G(1).Passes==3,"full graphics runs every stage");
        var sink=new Counter();int hi=0,lo=0;for(float t=0;t<4.8f;t+=1/30f){sink.N=0;sc.Effects(t,sink,1);hi+=sink.N;sink.N=0;sc.Effects(t,sink,.3f);lo+=sink.N;}
        Check(lo<hi*.8f,"the effects setting thins the effects ("+lo+" of "+hi+")");
        yield return $"Sheldon Intro tiers: reduced effects keep every beat with ramped blackouts ({worstStep:0.00} a frame), {seeds} soft steps, a steady build with no drop-outs and no flicker; low graphics runs one shader stage";

        // 5. The card's glitch-typed text.
        Check(NativeSheldonIntro.Typed("SHELDON",-1,false,1,buf,out _)==0&&NativeSheldonIntro.Typed("",1,false,1,buf,out _)==0,"nothing before the card");
        {int n=NativeSheldonIntro.Typed("SHELDON",5,false,1,buf,out bool done);Check(done&&n==7&&new string(buf,0,n)=="SHELDON","the name ends typed");}
        {int n=NativeSheldonIntro.Typed("SHELDON",.1f,false,1,buf,out bool done);int shown=(int)(.1f*NativeSheldonIntro.TypeRate);Check(!done&&n==shown+4&&new string(buf,0,shown)=="SH","revealed letters then junk glyphs");for(int i=shown;i<n;i++)Check("#%&@$01<>/\\|=+*?!".IndexOf(buf[i])>=0,"junk ahead of the cursor");Check(NativeSheldonIntro.Typed("SHELDON",.1f,true,1,buf,out _)==shown+2,"reduced effects show fewer junk glyphs");}
        yield return "Sheldon Intro card: the name glitch-types in (revealed letters, junk glyphs ahead of the cursor, done by the end); deterministic";

        // 6. Runtime hooks (source scans).
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string intros=R("NativeIntros.cs"),hat=R("NativeHatPresentation.cs"),pres=R("NativeSheldonIntroPresentation.cs"),overlay=R("NativeMatchOverlays.cs"),game=R("ConversionGame.cs"),profile=R("NativeProfileUI.cs");
        Check(intros.Contains("var sheldon=script as NativeSheldonIntroScript;if(sheldon!=null)actor=SheldonIntroBegin(sheldon,actor,pilot,t);")&&intros.Contains("try{sheldonScripted=sheldon!=null;SourceCharacter(introRig,actor,arena.Time,weapon:false,halo:age,reveal:entranceReveal);}finally{sheldonScripted=false;}"),"the directed intro dresses the pilot and scripts the face only while it draws the pilot");
        Check(pres.Contains("bool reduced=HaloReduced;script.Reduced=reduced;script.Low=LowGraphics;"),"the presentation sets the script's reduced and low tiers");
        Check(pres.Contains("var look=actor.ViewCopy();look.Hat=NativeSheldonHead.Hat;look.HatColor=pilot.Seat.hatColor;look.BodyColor=NativeSheldonIntro.Suit;look.AccentColor=NativeSheldonIntro.Accent(pilot.Seat.hatColor);")&&NativeSheldonIntro.Accent("")=="#22e6ff"&&NativeSheldonIntro.Accent("#ff3dcf")=="#ff3dcf"&&hat.Contains("var face=sheldonScripted?sheldonScriptFace:NativeSheldonHead.Evaluate("),"the intro wears the whole Sheldon Head look in the pilot's hat colour (a copy: the pilot keeps its own hat and colours)");
        Check(overlay.Contains("if(!boss&&style==NativeSheldonIntro.Style){SheldonCard(pilot,rawAge,index,total,team);return;}"),"the corrupted card replaces the standard one");
        Check(game.Contains("try{LateFrame();DamageScreen();GlitchScreen();}finally{FinishHitchFrame(HitchMs(at));}")&&game.Contains("NativeGlitchScreenPass.Release(true);")&&pres.Contains("bool on=presentation!=null&&presentation.GlitchFrame==Time.frameCount;"),"the glitch pass runs only on frames the intro staged, and is released");
        var shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeGlitchScreen.shader"));
        Check(shader.Contains("Shader \"Hidden/ChipArena/NativeGlitchScreen\"")&&shader.Contains("// BEGIN SHARED")&&shader.Contains("// END SHARED")&&shader.Contains("float4 _GlA;")&&shader.Contains("float4 _GlB;")&&shader.Contains("float4 _GlC;")&&shader.Contains("float4 _GlTint;")&&!shader.Contains("tex2D(")&&File.Exists(Path.Combine(root,"Assets/ChipArena/Resources/NativeGlitchScreen.shader.meta")),"the glitch shader (no derivative texture reads)");
        Check(pres.Contains("Resources.Load<Shader>(\"NativeGlitchScreen\")")&&File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Editor/NativeBuildChecks.cs")).Contains("\"NativeGlitchScreen\"}"),"the pass loads it and the Editor build check covers it");
        Check(profile.Contains("\"dev-siren\",\"sheldon-intro\"};")&&profile.Contains("{\"sheldon-intro\",\"DEV · Sheldon Intro\"}"),"the Locker lists it (gated)");
        yield return $"Sheldon Intro runtime: the pilot copy in the Sheldon Head, the scripted face, the glitch pass on staged frames only, the corrupted card and the Locker entry ({count} checks in all)";
    }
    // The head's world forward at `t` (the stare: the hood's face toward the lens).
    private static Vector3 StareForward(NativeSheldonIntroScript sc,float t){var p=new CapturedRig.Pose();sc.Pose(t,p,out _,out var rot);p.World();return Vector3.Transform(Vector3.UnitZ,NativeEntrancePose.WorldRotation(p,rot,CapturedRig.Head));}
    // The brightest glow piece of a frame (its luminance, linear) and how many long glitch lines it drew (NativeSheldonSet: only they reach 3 m).
    private sealed class Recorder:INativeEntranceProps
    {
        public float Max;public int Lines;
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color){if(!glow)return;Max=Math.Max(Max,Luma(color));if(scale.X>=3&&scale.X<60)Lines++;}
        public static float Luma(Vector3 c)=>.2126f*c.X+.7152f*c.Y+.0722f*c.Z;
    }
    private sealed class Counter:INativeEntranceFx,INativeEntranceProps
    {
        public int N;
        public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed)=>N++;
        public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed)=>N++;
        public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor)=>N++;
        public void Piece(WeaponShape shape,bool glow,Vector3 position,Quaternion rotation,Vector3 scale,Vector3 color)=>N++;
    }
}
