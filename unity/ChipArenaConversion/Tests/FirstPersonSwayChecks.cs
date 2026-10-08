#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Reference samples from the pinned source's poseFirstPersonView (tools/export_first_person.mjs): look sway, stride bob,
    // landing kick, recoil, reload dip, run-low, rocket kick, mark flick and the ADS blend over 30-240 Hz clocks, plus the
    // frame conventions the runtime uses to put that layer on the Unity view model.
    internal static class FirstPersonSwayChecks
    {
        private static WeaponId Weapon(string spec)=>spec=="pistol"?WeaponId.Pistol:spec=="rifle"?WeaponId.Rifle:spec=="machinegun"?WeaponId.MachineGun:WeaponId.Sword;
        public static List<string> Run(string root,ConversionContent content)
        {
            var results=new List<string>();
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/first-person-sway-parity.json")));int frames=0;float worst=0,worstAngle=0;
            float swayX=0,swayRoll=0,landDip=0,reload=0,rocket=0,flick=0,motion=0,aimedKick=0;bool planted=false,pilotChange=false;
            foreach(var gun in content.ads)Check(Math.Abs(NativeViewSway.AdsKick(Weapon(gun.spec))-(float)gun.kick)<1e-6f,"ADS kick differs from content: "+gun.spec);
            foreach(var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
            {
                string name=scenario.GetProperty("name").GetString(),spec=scenario.GetProperty("spec").GetString(),cls=scenario.GetProperty("cls").GetString();
                var weapon=Weapon(spec);
                if(cls=="ranged")Check(Math.Abs(NativeViewSway.AdsKick(weapon)-scenario.GetProperty("kick").GetSingle())<1e-6f,"ADS kick differs from the source profile: "+spec);
                var sway=new NativeViewSway();var ads=new NativeAimMotion();int index=0,lastPilot=0;
                foreach(var f in scenario.GetProperty("frames").EnumerateArray())
                {
                    float F(string key)=>f.GetProperty(key).GetSingle();var o=f.GetProperty("out");float O(string key)=>o.GetProperty(key).GetSingle();
                    void Near(float actual,float expected,float tolerance,string what,ref float max){float e=Math.Abs(actual-expected);max=Math.Max(max,e);if(!MathUtil.Finite(actual)||e>tolerance)throw new Exception($"Source first-person {name} frame {index} {what}: {actual} != {expected}");}
                    float time=F("t"),weight=ads.Sample(time,F("aim")>0);Near(weight,O("a"),.00005f,"ADS blend",ref worst);
                    var ability=f.GetProperty("ab");int pilot=f.GetProperty("pid").GetInt32();pilotChange|=lastPilot!=0&&pilot!=lastPilot;lastPilot=pilot;
                    var input=new NativeViewSwayInput{Pilot=pilot,Time=time,Yaw=F("yaw"),Pitch=F("pitch"),VelocityX=F("vx"),VelocityY=F("vy"),VelocityZ=F("vz"),Grounded=F("g")>0,Planted=F("pl")>0,Gait=spec!="siren",Ads=weight,
                        Ranged=cls=="ranged",Weapon=weapon,AdsKick=NativeViewSway.AdsKick(weapon),LastShot=F("shot"),ReloadAt=F("ra"),ReloadUntil=F("ru"),
                        AbilityId=ability.ValueKind==JsonValueKind.Null?null:ability.GetString(),AbilityAt=F("aa"),AbilityRelease=F("ar")};
                    var pose=sway.Sample(input);
                    swayX=Math.Max(swayX,Math.Abs(sway.SwayX));swayRoll=Math.Max(swayRoll,Math.Abs(sway.SwayRoll));landDip=Math.Min(landDip,sway.Land);motion=Math.Max(motion,sway.Motion);planted|=input.Planted&&sway.Motion>.3f;
                    if(input.Ranged)
                    {
                        reload=Math.Max(reload,NativeViewSway.ReloadEnvelope(time,input.ReloadAt,input.ReloadUntil));if(input.AbilityId=="rocket")rocket=Math.Max(rocket,NativeViewSway.RocketKick(time-input.AbilityRelease));
                        if(input.AbilityId=="orbital-mark")flick=Math.Max(flick,pose.Pitch);if(weight>.99f&&time-input.LastShot<.02f)aimedKick=Math.Max(aimedKick,pose.Z);
                    }
                    Near(sway.Motion,O("motion"),.0002f,"gait motion",ref worst);Near(sway.Stride,O("stride"),.002f,"stride phase",ref worstAngle);
                    Near(pose.X,O("dx"),.00005f,"x offset",ref worst);Near(pose.Y,O("dy"),.00005f,"y offset",ref worst);Near(pose.Z,O("dz"),.00005f,"z offset",ref worst);
                    Near(pose.Pitch,O("rx"),.0001f,"view pitch",ref worstAngle);Near(pose.Roll,O("rz"),.0001f,"view roll",ref worstAngle);
                    index++;frames++;
                }
            }
            Check(swayX>.0295f&&swayRoll>.045f&&landDip<-.01f&&reload>.99f&&rocket>.85f&&flick>.05f&&motion>1.1f&&planted&&aimedKick>0&&pilotChange,$"Fixture no longer covers every view layer: sway {swayX} roll {swayRoll} land {landDip} reload {reload} rocket {rocket} flick {flick} motion {motion} planted {planted} aimed kick {aimedKick} pilot change {pilotChange}");
            results.Add($"{frames} source first-person view-layer frames (look sway, stride bob, landing kick, recoil, reload dip, run-low, rocket/mark kicks, ADS damping, pilot change; 30-240 Hz, gaps, rewinds, pause); worst {worst*1000:0.000} mm / {worstAngle:0.00000} rad");
            results.Add(Frames());
            results.Add(HeldFrames());
            results.Add(GuestClock());
            results.Add(StaffUnderSway(content));
            return results;
        }
        // A frame on which the presentation clock does not advance (a pause, or a guest clock at the newest host frame) must be
        // a no-op that keeps the stored look and ground contact: a turn and a landing made meanwhile count on the next step.
        private static string HeldFrames()
        {
            NativeViewSwayInput At(int f,float time){float u=f/60f;bool air=f>=40&&f<52;return new NativeViewSwayInput{Pilot=1,Time=time,Yaw=3*u+.4f*(float)Math.Sin(7*u),Pitch=.3f*(float)Math.Sin(5*u),VelocityZ=6.8f,VelocityY=air?-9:0,Grounded=!air,Gait=true,Ranged=true,Weapon=WeaponId.Pistol,AdsKick=.5f,LastShot=.3f,ReloadAt=-100};}
            var held=new NativeViewSway();var plain=new NativeViewSway();int skipped=0;float worst=0,landed=0;var last=default(NativeViewSwayPose);
            for(int f=0;f<=90;f++)
            {
                // Every third frame advances the clock (as a 20 Hz staircase at 60 Hz); the frames between hold it, landing included.
                float time=1+(f/3)*.05f;var pose=held.Sample(At(f,time));
                if(f%3!=0){skipped++;Check(pose.X==last.X&&pose.Y==last.Y&&pose.Z==last.Z&&pose.Pitch==last.Pitch&&pose.Roll==last.Roll,"A held view-layer frame moved the view model");continue;}
                var expected=plain.Sample(At(f,time));last=pose;landed=Math.Min(landed,plain.Land);
                worst=Math.Max(worst,Math.Max(Math.Abs(pose.X-expected.X),Math.Max(Math.Abs(pose.Y-expected.Y),Math.Max(Math.Abs(pose.Roll-expected.Roll),Math.Abs(held.Land-plain.Land)))));
            }
            Check(worst<1e-6f&&landed<-.01f,$"Held frames dropped the look or the landing: {worst} (landing {landed})");
            return $"{skipped} held view-layer frames (look turning, landing on a held frame) are no-ops; the next step carries their turn (worst {worst*1000:0.0000} mm)";
        }
        // The guest path: the host sends a frame every 50 ms of wall time stamped with its match clock; NativeNetwork.Update observes
        // them and advances the replica's NativeSnapshotClock by the unscaled frame time, which is the guest's render time
        // (arena.ReplicaRenderTime); the camera look is local and moves every frame. A steady 3 rad/s turn and a 6.8 m/s run must
        // give the host's look sway and stride bob, and late frames or a slow host clock must not drop the look.
        private struct GuestRun{public int Held;public float SwayX,BobStep,BobRange;}
        private static GuestRun Guest(int hz,bool guest,bool look,double hostRate,double latency,double jitter,bool stall)
        {
            var random=new Random(11);var arrivals=new List<(double at,double time)>();
            for(int k=0;k*.05<7;k++){double at=k*.05+latency+random.NextDouble()*jitter;if(stall&&at>=3&&at<3.25)at=3.25;arrivals.Add((at,k*.05*hostRate));}
            arrivals.Sort((a,b)=>a.at.CompareTo(b.at));
            var clock=new NativeSnapshotClock();var sway=new NativeViewSway();bool started=false;double latest=double.NaN,previous=double.NaN,wall=0;int next=0;
            var run=new GuestRun();double sum=0;int count=0;float lastY=float.NaN,lo=1,hi=-1;
            for(int f=0;wall<5.5;f++)
            {
                double now=f/(double)hz+.0013*60/hz*((f*.618034)%1),delta=now-wall;wall=now;
                while(next<arrivals.Count&&arrivals[next].at<=now){var a=arrivals[next++];if(started&&a.time<=latest)continue;latest=a.time;if(!started){clock.Reset(a.time);started=true;}else clock.Observe(a.time);}
                if(!started)continue;
                double time=guest?clock.Advance((float)delta):now;
                var pose=sway.Sample(new NativeViewSwayInput{Pilot=1,Time=(float)time,Yaw=look?(float)(3*now):0,VelocityZ=look?0:6.8f,Grounded=true,Gait=true});
                if(now>=2&&now<5){if(time<=previous)run.Held++;sum+=Math.Abs(pose.X);count++;if(!float.IsNaN(lastY))run.BobStep=Math.Max(run.BobStep,Math.Abs(pose.Y-lastY));lo=Math.Min(lo,pose.Y);hi=Math.Max(hi,pose.Y);}
                previous=time;lastY=pose.Y;
            }
            run.SwayX=(float)(sum/Math.Max(1,count));run.BobRange=hi-lo;return run;
        }
        private static string GuestClock()
        {
            var report=new List<string>();int lateHeld=0;
            foreach(int hz in new[]{60,144,240})
            {
                GuestRun host=Guest(hz,false,true,1,.04,0,false),hostRun=Guest(hz,false,false,1,.04,0,false);
                GuestRun look=Guest(hz,true,true,1,.04,.03,false),stride=Guest(hz,true,false,1,.04,.03,false);
                Check(Math.Abs(host.SwayX-.012f)<.0002f,$"Host look sway at {hz} Hz is not the source's 12 mm: {host.SwayX*1000}");
                Check(look.Held==0&&stride.Held==0,$"Guest render clock held {look.Held} frames at {hz} Hz with on-time host frames");
                Check(Math.Abs(look.SwayX-host.SwayX)<.0003f,$"Guest look sway at {hz} Hz is {look.SwayX*1000:0.00} mm, host {host.SwayX*1000:0.00} mm");
                Check(stride.BobStep<=hostRun.BobStep*1.1f+.0002f&&Math.Abs(stride.BobRange-hostRun.BobRange)<.001f,$"Guest stride bob at {hz} Hz steps {stride.BobStep*1000:0.00} mm per frame (host {hostRun.BobStep*1000:0.00}), range {stride.BobRange*1000:0.00} mm");
                // A 250 ms stall of host frames and a host match clock running 5 % slower than wall time hold the guest clock at
                // the newest frame; the turn made on those frames still reaches the view model.
                GuestRun stalled=Guest(hz,true,true,1,.04,.03,true),slow=Guest(hz,true,true,.95,.04,.03,true);lateHeld+=stalled.Held+slow.Held;
                Check(stalled.Held>0&&stalled.SwayX>host.SwayX*.97f&&slow.SwayX>host.SwayX*.97f,$"Late host frames dropped the guest look sway at {hz} Hz: {stalled.SwayX*1000:0.00} / {slow.SwayX*1000:0.00} mm over {stalled.Held} held frames");
                report.Add($"{hz} Hz {look.SwayX*1000:0.0} mm, bob step {stride.BobStep*1000:0.0} mm (host {hostRun.BobStep*1000:0.0}), stalled {stalled.SwayX*1000:0.0} mm");
            }
            return $"Guest view layer on the replica snapshot clock (20 Hz host frames, 3 rad/s turn, 6.8 m/s run): no held frames on time, look sway and bob as the host; {lateHeld} held frames from late frames keep the look ({string.Join("; ",report)})";
        }
        private static Vector3 Rx(Vector3 v,float a){float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.X,v.Y*c-v.Z*s,v.Y*s+v.Z*c);}
        private static Vector3 Ry(Vector3 v,float a){float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.X*c+v.Z*s,v.Y,-v.X*s+v.Z*c);}
        private static Vector3 Rz(Vector3 v,float a){float c=(float)Math.Cos(a),s=(float)Math.Sin(a);return new Vector3(v.X*c-v.Y*s,v.X*s+v.Y*c,v.Z);}
        // The runtime's frame mapping: the source's camera-space root (offset, Euler XYZ (pitch, π, roll)) on a source rig equals
        // the Unity root (offset with z flipped, Quaternion.Euler(UnityPitch, 0, UnityRoll)) on the x-mirrored Unity rig seen
        // through the z-mirrored Unity camera; the source look reads back from a Unity Euler(pitch, yaw, 0) camera; the pilot
        // view maps velocity, planted actions and the siren's missing gait; a new pilot restarts the gait only.
        private static string Frames()
        {
            var random=new Random(7);float worst=0;
            for(int n=0;n<400;n++)
            {
                var pose=new NativeViewSwayPose{X=(float)random.NextDouble()*.1f-.05f,Y=(float)random.NextDouble()*.1f-.05f,Z=(float)random.NextDouble()*.1f-.05f,Pitch=(float)random.NextDouble()*.6f-.3f,Roll=(float)random.NextDouble()*.6f-.3f};
                var unity=new Vector3((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f);
                Vector3 s=Rx(Ry(Rz(CapturedRig.Mirror(unity),pose.Roll),(float)Math.PI),pose.Pitch)+new Vector3(pose.X,pose.Y,pose.Z);
                var rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,pose.UnityPitchDegrees*(float)Math.PI/180)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,pose.UnityRollDegrees*(float)Math.PI/180);
                Vector3 u=Vector3.Transform(unity,rotation)+new Vector3(pose.X,pose.Y,pose.UnityZ);
                worst=Math.Max(worst,(u-new Vector3(s.X,s.Y,-s.Z)).Length());
                float yawDegrees=(float)random.NextDouble()*720-360,pitchDegrees=(float)random.NextDouble()*140-70;
                Vector3 forward=Vector3.Transform(Vector3.UnitZ,Quaternion.CreateFromAxisAngle(Vector3.UnitY,yawDegrees*(float)Math.PI/180)*Quaternion.CreateFromAxisAngle(Vector3.UnitX,pitchDegrees*(float)Math.PI/180));
                Check(Math.Abs(NativeViewSway.Wrap(NativeViewSway.SourceYaw(forward.X,forward.Z)+yawDegrees*(float)Math.PI/180))<1e-4f&&Math.Abs(NativeViewSway.SourcePitch(forward.Y)+pitchDegrees*(float)Math.PI/180)<1e-4f,"Camera look does not map to the source yaw/pitch");
            }
            Check(worst<1e-5f,"Unity view-layer rotation differs from the source frame: "+worst);
            var a=new Actor{Id=3,SourceSpec="pistol",Weapon=WeaponId.Pistol,Velocity=new V3(2,-3,4),Grounded=false,LastShot=1.5f,ReloadAt=1.6f,ReloadUntil=2.9f,AbilityId="rocket",AbilityAt=1.1f,AbilityRelease=1.22f};
            var i=NativeViewSway.Input(a,2,.1f,.2f,.5f);
            Check(i.Pilot==3&&i.VelocityX==-2&&i.VelocityY==-3&&i.VelocityZ==4&&!i.Grounded&&i.Gait&&i.Ranged&&i.AdsKick==.5f&&i.LastShot==1.5f&&i.ReloadAt==1.6f&&i.ReloadUntil==2.9f&&i.AbilityId=="rocket"&&i.AbilityRelease==1.22f&&!i.Planted,"Pilot view does not map to the view-layer inputs");
            a.Velocity=new V3(float.NaN,float.PositiveInfinity,1);i=NativeViewSway.Input(a,2,0,0,0);Check(i.VelocityX==0&&i.VelocityY==0&&i.VelocityZ==1,"Non-finite velocity reached the view layer");
            Check(NativeViewSway.Planted(new Actor{CommitAt=1,CommitUntil=2.5f},2)&&!NativeViewSway.Planted(new Actor{CommitAt=1,CommitUntil=2.5f},2.5f)&&NativeViewSway.Planted(new Actor{StunUntil=2.1f},2)&&NativeViewSway.Planted(new Actor{RollAt=1.8f},2)&&!NativeViewSway.Planted(new Actor(),2),"Planted actions differ from souls-combat committed/hitstun/rolling");
            Check(!NativeViewSway.Input(new Actor{SourceSpec="siren"},0,0,0,0).Gait&&!NativeViewSway.Input(new Actor{SourceSpec="fire",Weapon=WeaponId.Sword},0,0,0,0).Ranged,"Siren gait or staff recoil enabled");
            var sway=new NativeViewSway();for(int f=0;f<=60;f++)sway.Sample(new NativeViewSwayInput{Pilot=1,Time=f/60f,Yaw=f/60f,VelocityZ=6.8f,Grounded=true,Gait=true});
            float swayed=sway.SwayX;Check(sway.Motion>.9f&&sway.Stride>3,"Gait did not run");sway.Sample(new NativeViewSwayInput{Pilot=2,Time=61/60f,Yaw=61/60f,VelocityZ=6.8f,Grounded=true,Gait=true});
            Check(sway.Motion<.25f&&sway.Stride<.6f&&Math.Abs(sway.SwayX-swayed)<.005f,"A new pilot must restart the gait and keep the view springs");
            sway.Reset();var rest=sway.Sample(new NativeViewSwayInput{Pilot=2,Time=5,Yaw=3,Gait=true,Grounded=true});Check(rest.X==0&&rest.Y==0&&rest.Z==0&&rest.Pitch==0&&rest.Roll==0&&sway.Motion==0,"Reset view layer is not at rest");
            return $"First-person view layer frames: source root Rx·Ry(π)·Rz equals the Unity Euler root within {worst*1000:0.0000} mm over 400 poses; camera look, pilot view inputs, planted actions and pilot change";
        }
        private static Vector2 Project(Vector3 p){float f=1/(float)Math.Tan(NativeViewRules.FirstFov*Math.PI/360);return new Vector2(p.X/p.Z*f/(16f/9),p.Y/p.Z*f);}
        private static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.X-a.X)*(c.Y-a.Y)-(b.Y-a.Y)*(c.X-a.X);
        // NativeVisualChecks' crosshair test: the convex hull of a part's projected corners against the |x|<.1, |y|<.15 NDC box.
        private static bool CoversCrosshair(List<Vector2> points)
        {
            points.Sort((a,b)=>a.X==b.X?a.Y.CompareTo(b.Y):a.X.CompareTo(b.X));var h=new List<Vector2>();
            foreach(var p in points){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],p)<=0)h.RemoveAt(h.Count-1);h.Add(p);}
            int lower=h.Count;for(int i=points.Count-2;i>=0;i--){var p=points[i];while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p)<=0)h.RemoveAt(h.Count-1);h.Add(p);}
            h.RemoveAt(h.Count-1);if(h.Count<3)return false;var axes=new List<Vector2>{Vector2.UnitX,Vector2.UnitY};
            for(int i=0;i<h.Count;i++){var d=h[(i+1)%h.Count]-h[i];axes.Add(new Vector2(-d.Y,d.X));}
            foreach(var a in axes){float lo=float.MaxValue,hi=float.MinValue;foreach(var p in h){float v=Vector2.Dot(p,a);lo=Math.Min(lo,v);hi=Math.Max(hi,v);}float extent=.1f*Math.Abs(a.X)+.15f*Math.Abs(a.Y);if(hi<=-extent||lo>=extent)return false;}
            return true;
        }
        // The staff schools' idle, primary, guard and Q/E view models under the source's own view layer, composed as the runtime
        // does: camera-space offset on the root, then the X/Z view-root rotation, with the staff rolled about the camera axis.
        // The allowances are NativeVisualChecks': idle/primary/guard fully clear, ability phrases at most 5 % and clear at release.
        private static string StaffUnderSway(ConversionContent content)
        {
            string[] specs={"fire","water","lightning","darkness"};var row=new float[64];var channels=new float[13];int samples=0,steadyCross=0,swayCross=0,envelopeCross=0,envelopeSamples=0;
            float[] signs={-1,1};
            for(int element=1;element<=4;element++)foreach(string action in new[]{"idle","primary","guard"}.Concat(content.abilities.Where(a=>a.spec==specs[element-1]).Select(a=>a.id)))
            {
                var definition=content.abilities.FirstOrDefault(a=>a.id==action);var guard=new NativeGuardMotion();var sway=new NativeViewSway();
                float interval=(float)content.Weapon(specs[element-1]).interval,release=definition==null?NativeAbilityRules.PrimaryWindup(element,1):NativeAbilityRules.CastTime(definition.windup,new ChipStats(new[]{new NumberValue{name="attackRate",value=1}}));
                int count=0,cross=0,releaseCross=0;
                for(int frame=0;frame<=240;frame++)
                {
                    float time=frame/120f,attack=(float)Math.Floor(time/interval)*interval,shot=time>=attack+release?attack+release:attack>0?attack-interval+release:-100;
                    var pose=new CapturedRig.Pose();CapturedClips.Sets[0][0].Sample(time,row);CapturedRig.Sample(row,0,pose);
                    var actor=new Actor{MagicVisual=element,CastRate=1,AttackAt=action=="primary"?attack:-100,LastShot=action=="primary"?shot:-100,AbilityAt=definition!=null?0:-100,AbilityId=definition?.id,AbilityRelease=release};
                    float g=guard.Sample(time,action=="guard");MagicPose.Apply(pose,actor,time,true,channels,g);NativeFirstPersonPose.SettleTorso(pose);
                    // A hard flick (peak 9 rad/s yaw, 4 rad/s pitch) while sprinting at 6.8 m/s, landing from a jump at 1.2 s.
                    float yaw=(float)Math.Sin(time*6)*1.5f,pitch=(float)Math.Sin(time*4)*.5f;bool air=time>=.9f&&time<1.2f;
                    var swayed=sway.Sample(new NativeViewSwayInput{Pilot=1,Time=time,Yaw=yaw,Pitch=pitch,VelocityZ=6.8f,VelocityY=air?-9:0,Grounded=!air,Gait=true});
                    bool Covers(float x,float y,float z,float pitchDeg,float rollDeg)
                    {
                        var rootRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,pitchDeg*(float)Math.PI/180)*Quaternion.CreateFromAxisAngle(Vector3.UnitZ,rollDeg*(float)Math.PI/180);
                        Vector3 root=NativeFirstPersonPose.MagicViewOffset(g)+new Vector3(x,y,z),wrist=root+Vector3.Transform(CapturedRig.Mirror(pose.WorldPosition[CapturedRig.LHand])*NativeFirstPersonPose.Scale,rootRotation);
                        Quaternion rotation=NativeFirstPersonPose.StaffRotation(rootRotation*CapturedRig.Mirror(pose.WorldRotation[CapturedRig.LHand]),Vector3.UnitZ,g);float scale=NativeFirstPersonPose.Scale*NativeFirstPersonPose.StaffSize(g);
                        foreach(var part in NativeStaffGeometry.Parts[element-1])
                        {
                            var corners=new List<Vector2>();
                            foreach(float cx in signs)foreach(float cy in signs)foreach(float cz in signs){Vector3 p=wrist+Vector3.Transform(CapturedRig.Mirror(part.Position+Vector3.Transform(part.Size*new Vector3(cx,cy,cz)*.5f,part.Rotation))*scale,rotation);corners.Add(Project(p));}
                            if(CoversCrosshair(corners))return true;
                        }
                        return false;
                    }
                    if(definition==null&&Covers(0,0,0,0,0))steadyCross++;
                    if(Covers(swayed.X,swayed.Y,swayed.UnityZ,swayed.UnityPitchDegrees,swayed.UnityRollDegrees)){cross++;if(Math.Abs(time-release)<=.15f)releaseCross++;}
                    count++;samples++;
                    // Envelope: the look-sway clamps (.03 / .025 m, .05 rad) at full stride bob, every sign combination.
                    if(definition==null&&frame%8==0)foreach(float sxSign in signs)foreach(float sySign in signs)foreach(float rSign in signs)
                    {if(Covers(sxSign*(.03f+.012f),sySign>0?.025f:-.025f-.015f,0,0,-rSign*.05f*180/(float)Math.PI))envelopeCross++;envelopeSamples++;}
                }
                string name=$"{specs[element-1]} {action}";
                Check(cross<=(definition==null?0:count*.05f),"Source view sway moves the staff across the crosshair: "+name+" "+cross+" / "+count);
                if(definition!=null)Check(releaseCross==0,"Source view sway moves the staff across the crosshair at release: "+name);
                swayCross+=cross;
            }
            Check(steadyCross==0,"Steady staff covers the crosshair: "+steadyCross);
            Check(envelopeCross==0,"Look-sway clamp envelope moves a staff across the crosshair: "+envelopeCross+" / "+envelopeSamples);
            return $"{samples} staff view models (four schools, idle/primary/guard/Q/E) under a 9 rad/s flick, sprint bob and landing: {swayCross} crosshair intersections; {envelopeCross} of {envelopeSamples} at the look-sway clamp envelope";
        }
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    }
}
#endif
