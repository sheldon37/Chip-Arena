#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Emperor's wavedash (E) and its cancel into the Electric Hook (Q), through the native fighter pose path exactly as
    // CapturedAnimation FighterPose runs it (NativeFighterPose.View, MeleeMotion.FighterBase, the exported style clip,
    // NativeFighterPose.Style, FighterFinish): every recorded frame of the original's timelines (tools/export_fighter_motion.mjs:
    // a standing wavedash, chained wavedashes on the run, cancels from a stand and from a run) against the source joints;
    // the clip route over the whole action and its switch to the hook on the frame the action changes; no snap, continuous clip
    // switches and the wave dip on its marks from 30 to 480 Hz; no allocation per frame.
    internal static class WavedashChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Emperor wavedash: "+what);count++;}
        private sealed class Clip{public float Duration;public bool Loop;public int Frames;public float[] Values;}
        private static readonly Dictionary<string,Clip> clips=new Dictionary<string,Clip>();
        private static void LoadClips(string root)
        {
            if(clips.Count>0)return;
            using var envelope=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/SourceMotion.json")));
            using var bytes=new MemoryStream(Convert.FromBase64String(envelope.RootElement.GetProperty("data").GetString()));using var zip=new GZipStream(bytes,CompressionMode.Decompress);using var reader=new StreamReader(zip);
            using var doc=JsonDocument.Parse(reader.ReadToEnd());
            foreach(var c in doc.RootElement.GetProperty("clips").EnumerateArray())
            {
                string key=c.GetProperty("key").GetString();if(!key.StartsWith("emperor-",StringComparison.Ordinal))continue;
                var v=c.GetProperty("values");var values=new float[v.GetArrayLength()];int i=0;foreach(var x in v.EnumerateArray())values[i++]=(float)x.GetDouble();
                clips[key]=new Clip{Duration=(float)c.GetProperty("duration").GetDouble(),Loop=c.GetProperty("loop").GetBoolean(),Frames=c.GetProperty("frames").GetInt32(),Values=values};
            }
        }
        // Runtime/NativeSourceMotion.Sample, as NativeSourceMotion.Fighter calls it.
        private static void Sample(string key,float age,float duration,CapturedRig.Pose pose)
        {
            var clip=clips[key];float fraction=clip.Loop?(age-(float)Math.Floor(age/clip.Duration)*clip.Duration)/clip.Duration:Math.Clamp(age/(duration>0?duration:clip.Duration),0,1);
            NativeFighterPose.Frames(clip.Frames,fraction,NativeFighterPose.Closed(key),out int a,out int b,out float t);var v=clip.Values;
            for(int bone=0;bone<22;bone++)
            {
                int i=(a*22+bone)*7,j=(b*22+bone)*7;
                pose.Position[bone]=Vector3.Lerp(new Vector3(v[i],v[i+1],v[i+2]),new Vector3(v[j],v[j+1],v[j+2]),t);
                pose.Rotation[bone]=Quaternion.Slerp(new Quaternion(v[i+3],v[i+4],v[i+5],v[i+6]),new Quaternion(v[j+3],v[j+4],v[j+5],v[j+6]),t);
            }
        }
        // One presented Emperor: the native fighter pose path's state for a pilot.
        private sealed class Rig
        {
            public readonly MeleeMotion M=new MeleeMotion();public readonly MeleeView V=new MeleeView{Id="7",Spec="emperor",Windup=.075f};
            public readonly Actor A=new Actor{Id=7,SourceSpec="emperor",Grounded=true,Hp=100,CastRate=1,SourceTotal=1};
            public readonly NativeWaveDash Wave=new NativeWaveDash();public readonly CapturedRig.Pose ClipPose=new CapturedRig.Pose();private float last=-100;public string Key;
            // Source frame: +x the rig's left, yaw in radians; positions are the rig group's (y .865 over the floor).
            public void Pose(float t,float x,float z,float yaw,float vx,float vz,string action,float at,float until,float lastAttack,int attackIndex)
            {
                float dt=last<-90||t<last||t-last>.35f?1/60f:Math.Min(.1f,t-last);last=t;
                A.SourceAction=action;A.SourceAt=at;A.SourceUntil=until;
                V.Yaw=yaw;V.Vel=new Vector3(vx,0,vz);V.Grounded=true;V.LastAttack=lastAttack;V.AttackIndex=attackIndex;V.AttackRate=1;V.RollAt=V.RollUntil=-100;
                M.Rig.GroupYaw=M.PresentedYaw(V,t,V.Yaw);M.Rig.GroupPosition=new Vector3(x,.865f,z);
                NativeFighterPose.View(V,A,Wave,t);
                M.FighterBase(V,t,dt);
                bool acting=NativeFighterPose.Acting(action,until,t);Key=NativeFighterPose.Clip("emperor",acting?action:null,A.SourceTotal,A.SourceIndex,false,false);
                Sample(Key,acting?t-at:t,acting?until-at:0,ClipPose);
                NativeFighterPose.Style(M,ClipPose,A,false,t);
                M.FighterFinish(V,t,dt);
            }
            public Vector3 Point(int b)=>M.Rig.ChainPoint(b,-1);
            public Vector3 Tip(int toe)=>M.Rig.ChainPoint(toe,-1)+Vector3.Transform(new Vector3(0,0,.1f),M.Rig.ChainQuat(toe,-1));
        }
        public static void Run(string root)
        {
            LoadClips(root);Track();Parity(root);Route();Rates();Allocation();
            Console.WriteLine("PASS Emperor wavedash ("+count+" checks: the wave's dash window through a cancel, source joint parity of a standing, chained, standing-cancel and running-cancel wavedash, clip route switch on the cancel frame, no snap and planted wave dips at 30-480 Hz, no allocation)");
        }
        // movement.ts: each wave sets dashTime .18; Q starting the Electric Hook mid-wave clears waveTime, not dashTime.
        private static void Track()
        {
            var w=new NativeWaveDash();
            Check(w.DashTime(null,0,0,1)==0,"no dash before a wave");
            Check(Math.Abs(w.DashTime("wave",1,1.18f,1.05f)-.13f)<1e-5f,"dash left during the wave");
            Check(Math.Abs(w.DashTime("electric",1.08f,1.48f,1.1f)-.08f)<1e-5f,"the dash runs on through the cancel into the Electric Hook");
            Check(w.DashTime("electric",1.08f,1.48f,1.2f)==0&&w.DashTime(null,1.08f,1.48f,1.5f)==0,"the dash ends .18 s after the wave began");
            Check(w.DashTime("wave",1,1.18f,.5f)==0,"a replay step back shows no dash");
            var v=new MeleeView();var a=new Actor{SourceSpec="ansatsuken",SourceAction="",SourceAt=1,SourceUntil=2};NativeFighterPose.View(v,a,w,1.5f);
            Check(v.ActionKind==null&&v.DashTime==0,"an Ansatsuken (or no action) has no wave dash and no action key");
            a.SourceAction="combo";NativeFighterPose.View(v,a,w,1.5f);Check(v.ActionKind=="combo"&&v.ActionAt==1&&v.ActionUntil==2,"the style action keys the pose transitions");
            // emperor.ts confirm clears a struck Emperor's dashTime: a hit ends the dash, and the same wave does not restart it.
            var hit=new NativeWaveDash();var hv=new MeleeView();var e=new Actor{SourceSpec="emperor",SourceAction="wave",SourceAt=1,SourceUntil=1.18f};
            NativeFighterPose.View(hv,e,hit,1.02f);Check(hv.DashTime>.15f,"a wave dashes");
            hv.HitStunUntil=1.5f;NativeFighterPose.View(hv,e,hit,1.05f);Check(hv.DashTime==0,"a hit ends the wave's dash");
            hv.HitStunUntil=0;NativeFighterPose.View(hv,e,hit,1.08f);Check(hv.DashTime==0,"the stopped wave does not dash again");
            e.SourceAt=1.3f;e.SourceUntil=1.48f;NativeFighterPose.View(hv,e,hit,1.35f);Check(Math.Abs(hv.DashTime-.13f)<1e-5f,"the next wave dashes");
        }
        // The original's timelines frame by frame: legs, hips and the whole body against the source joints.
        private static void Parity(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/fighter-motion-parity.json")));
            int total=0;float worstLeg=0,worstHips=0,worstAll=0;
            foreach(var scenario in document.RootElement.GetProperty("scenarios").EnumerateArray())
            {
                string name=scenario.GetProperty("name").GetString();var rig=new Rig();int f=0,compared=0,waveFrames=0;float leg=0,hips=0,all=0,sum=0;int n=0;
                foreach(var frame in scenario.GetProperty("frames").EnumerateArray())
                {
                    var i=frame.GetProperty("i");float In(int k)=>(float)i[k].GetDouble();float t=(float)(10+f/60.0+.0013*((f*.618034)%1));f++;
                    string action=In(6)==1?"wave":In(6)==2?"electric":null;
                    rig.Pose(t,In(0),In(1),In(2),In(3),In(4),action,In(7),In(8),In(9),(int)In(10));
                    if(!frame.TryGetProperty("out",out var expected))continue;
                    Check(Math.Abs(rig.M.Rig.GroupYaw-(float)expected.GetProperty("yaw").GetDouble())<1e-3f,name+" presented yaw");
                    var p=expected.GetProperty("p");compared++;if(action=="wave")waveFrames++;
                    for(int b=0;b<24;b++)
                    {
                        var want=new Vector3((float)p[b*3].GetDouble(),(float)p[b*3+1].GetDouble(),(float)p[b*3+2].GetDouble());var got=b<22?rig.Point(b):rig.Tip(b==22?CapturedRig.LToe:CapturedRig.RToe);
                        float e=Vector3.Distance(got,want);sum+=e;n++;all=Math.Max(all,e);
                        if(b>=CapturedRig.LHip){leg=Math.Max(leg,e);Check(e<.03f,name+" frame "+(f-1)+" leg joint "+b+" is "+(e*100).ToString("0.0")+" cm from the source");}
                        else if(b<=CapturedRig.Pelvis){hips=Math.Max(hips,e);Check(e<.02f,name+" frame "+(f-1)+" hips "+b+" are "+(e*100).ToString("0.0")+" cm from the source");}
                        // Arms: the 60 fps hook clip's interpolation through its fastest drive and the gait-swung hands.
                        else Check(e<.1f,name+" frame "+(f-1)+" joint "+b+" is "+(e*100).ToString("0.0")+" cm from the source");
                    }
                }
                Check(waveFrames>=(name.Contains("cancel")?2:4),name+" records the wave");
                total+=compared;worstLeg=Math.Max(worstLeg,leg);worstHips=Math.Max(worstHips,hips);worstAll=Math.Max(worstAll,all);
                Console.WriteLine("PASS Emperor wavedash parity "+name+": "+compared+" frames, legs within "+(leg*1000).ToString("0.0")+" mm, hips "+(hips*1000).ToString("0.0")+" mm, any joint "+(all*1000).ToString("0.0")+" mm (mean "+(sum/Math.Max(1,n)*1000).ToString("0.0")+" mm)");
            }
            Check(total>150,"enough parity frames");
            // The wave's own pose (the owner's report: the legs ran at the dash speed and dragged flat behind the hips): at the
            // dip the lead ankle reaches forward of the hips and the rear trails on its toes, both on the floor.
            var r=new Rig();for(int k=0;k<=30;k++)r.Pose(20+k/60f,0,0,0,0,0,null,0,0,-10,0);
            float at=20+31/60f;for(int k=0;k<=5;k++){float t=at+k/60f;r.Pose(t,0,(t-at)*9,0,0,9,"wave",at,at+.18f,-10,0);}
            r.Pose(at+.09f,0,.09f*9,0,0,9,"wave",at,at+.18f,-10,0);
            Vector3 lead=r.M.Rig.AnkleInGroup(true),rear=r.M.Rig.AnkleInGroup(false);
            Check(Vector3.Distance(lead,new Vector3(.16f,-.772f,.24f))<.02f&&Vector3.Distance(rear,new Vector3(-.14f,-.765f,-.30f))<.02f,"the wavedash dip plants the lead ankle forward and trails the rear ("+lead+", "+rear+")");
            Check(r.Tip(CapturedRig.RToe).Y<rear.Y-.02f,"the rear foot rides its toes (heel raised)");
            Check(r.Point(CapturedRig.Body).Y<-.25f,"the wavedash crouches deep");
            Console.WriteLine("PASS Emperor wavedash parity: "+total+" frames, legs within "+(worstLeg*1000).ToString("0.0")+" mm and hips within "+(worstHips*1000).ToString("0.0")+" mm of the original (any joint "+(worstAll*1000).ToString("0.0")+" mm)");
        }
        // The style clip plays for the whole wave and switches to the hook on the frame the replicated action changes.
        private static void Route()
        {
            float at=3,cancel=3.08f;
            for(float t=at;t<at+.18f;t+=1/240f){bool acting=NativeFighterPose.Acting("wave",at+.18f,t);Check(acting&&NativeFighterPose.Clip("emperor","wave",1,0,false,false)=="emperor-wave-1-0","wave clip through the wave");}
            Check(!NativeFighterPose.Acting("wave",at+.18f,at+.18f),"the wave ends at its until");
            Check(NativeFighterPose.Acting("electric",cancel+.4f,cancel)&&NativeFighterPose.Clip("emperor","electric",1,0,false,false)=="emperor-electric-1-0","the hook clip from the cancel frame");
            // Through the pipeline: the frame the action turns to 'electric' samples the hook clip and continues from the previous
            // frame's pose (the .055 s transition starts from it, no snap); the hook clip keeps playing to its until.
            var r=new Rig();
            for(int k=0;k<=40;k++)r.Pose(at-40/240f+k/240f,0,0,0,0,0,null,0,0,-10,0);
            var before=new Vector3[22];
            for(float t=at;t<cancel-1e-4f;t+=1/240f){r.Pose(t,0,(t-at)*9,0,0,9,"wave",at,at+.18f,-10,0);Check(r.Key=="emperor-wave-1-0","wave key "+t);for(int b=0;b<22;b++)before[b]=r.Point(b);}
            float c=cancel;r.Pose(c,0,(c-at)*9,0,0,15,"electric",c,c+.4f,c,0);Check(r.Key=="emperor-electric-1-0","the cancel frame routes to the hook");
            float jump=0;for(int b=0;b<22;b++)jump=Math.Max(jump,Vector3.Distance(before[b],r.Point(b)));
            Check(jump<.02f,"the cancel frame continues from the wave pose ("+(jump*100).ToString("0.0")+" cm)");
            for(float t=c+1/240f;t<c+.4f;t+=1/240f){r.Pose(t,0,(c-at)*9+(t-c)*12,0,0,12,"electric",c,c+.4f,t>=c+.1f?c+.1f:c,t>=c+.1f?1:0);Check(r.Key=="emperor-electric-1-0","hook key "+t);}
            Console.WriteLine("PASS Emperor wavedash route: wave clip for the whole wave, hook clip from the cancel frame to its end, largest joint step on the cancel frame "+(jump*1000).ToString("0.0")+" mm");
        }
        // A timeline sampled from a 2400 Hz movement table (the exporter's model: walk input at 35 m/s^2, the wave's push held
        // .18 s then dragging at 3.1/s, the electric step 6 m/s for .1 s) at any frame rate.
        private sealed class Timeline
        {
            public const float Hz=2400;public readonly float Duration;private readonly float[] x,z,vx,vz;public readonly List<(float t,string action)> Events=new List<(float,string)>();
            private readonly float yaw;
            public Timeline(float duration,float yaw,float moveFrom,float moveUntil,(float t,string action)[] events)
            {
                Duration=duration;this.yaw=yaw;int n=(int)(duration*Hz)+2;x=new float[n];z=new float[n];vx=new float[n];vz=new float[n];Events.AddRange(events);
                float fx=(float)Math.Sin(yaw),fz=(float)Math.Cos(yaw),move=0,ext=0,dash=0,step=0,px=0,pz=0,dt=1/Hz;int e=0;
                for(int k=0;k<n;k++)
                {
                    float t=k*dt;while(e<events.Length&&events[e].t<=t){if(events[e].action=="wave"){ext=9;dash=.18f;}else if(events[e].action=="electric")step=.1f;e++;}
                    float want=t>=moveFrom&&t<moveUntil?6.8f:0;move+=Math.Clamp(want-move,-35*dt,35*dt);
                    float speed=move+ext+(step>0?6:0);x[k]=px;z[k]=pz;vx[k]=fx*speed;vz[k]=fz*speed;px+=fx*speed*dt;pz+=fz*speed*dt;
                    dash=Math.Max(0,dash-dt);step=Math.Max(0,step-dt);if(dash<=0)ext*=(float)Math.Exp(-3.1*dt);
                }
            }
            public void At(Rig r,float time,float t)
            {
                int k=Math.Min(x.Length-1,(int)Math.Round(t*Hz));string action=null;float at=0,until=0,lastAttack=-10;int index=0;
                foreach(var (et,ea) in Events)if(et<=t){action=ea;at=et;until=et+(ea=="wave"?.18f:.4f);if(ea=="electric"){lastAttack=t>=et+.1f?et+.1f:et;index=t>=et+.1f?1:0;}}
                if(action!=null&&t>=until)action=null;
                r.Pose(time+t,x[k],z[k],yaw,vx[k],vz[k],action,time+at,time+until,time+lastAttack,index);
            }
        }
        private static Timeline Cancel()=>new Timeline(1.3f,.3f,.15f,1.0f,new[]{(.5f,"wave"),(.58f,"electric")});
        private static Timeline Chain()=>new Timeline(1.4f,.3f,.15f,1.1f,new[]{(.5f,"wave"),(.68f,"wave"),(.86f,"wave")});
        // No snaps from 30 to 480 Hz around the wavedashes and the cancel: a joint's fastest frame-to-frame speed stays put as the
        // rate doubles (a discontinuity jumps the same distance in one frame at any rate, so its measured speed doubles too; the
        // hook's drive is fast but smooth, and the wave's sqrt(sin(pi u)) foot reach is a cusp that grows by sqrt 2 at most);
        // every frame that switches the style clip continues from the frame before (the transition starts from it); and at
        // every rate the wavedash dip plants the feet on the wave's marks.
        private static readonly Vector3 Lead=new Vector3(.16f,-.772f,.24f),Rear=new Vector3(-.14f,-.765f,-.30f);
        private static void Rates()
        {
            foreach(var (name,make) in new (string,Func<Timeline>)[]{("cancel",Cancel),("chain",Chain)})
            {
                var speeds=new Dictionary<int,float[]>();float worstRatio=0,worstSwitch=0,worstPlant=0;int switches=0,plants=0;
                foreach(int hz in new[]{30,60,120,144,240,480})
                {
                    var line=make();var r=new Rig();var prev=new Vector3[22];var now=new Vector3[22];bool has=false;var speed=new float[22];string prevKey=null;
                    float from=line.Events[0].t-.1f,to=line.Events[line.Events.Count-1].t+.5f;int frames=(int)(line.Duration*hz);
                    for(int f=0;f<=frames;f++)
                    {
                        float t=f/(float)hz;line.At(r,40,t);for(int b=0;b<22;b++){now[b]=r.Point(b);Check(float.IsFinite(now[b].X)&&float.IsFinite(now[b].Y)&&float.IsFinite(now[b].Z),name+" finite joints at "+hz+" Hz");}
                        if(has&&t>=from&&t<=to)for(int b=0;b<22;b++)speed[b]=Math.Max(speed[b],Vector3.Distance(now[b],prev[b])*hz);
                        if(has&&r.Key!=prevKey)
                        {
                            float jump=0;for(int b=0;b<CapturedRig.LHip;b++)jump=Math.Max(jump,Vector3.Distance(now[b],prev[b]));switches++;worstSwitch=Math.Max(worstSwitch,jump);
                            Check(jump<.01f,name+" "+hz+" Hz: switching "+prevKey+" to "+r.Key+" at "+t.ToString("0.000")+" s jumps "+(jump*100).ToString("0.0")+" cm");
                        }
                        float u=(40+t-r.A.SourceAt)/NativeFighterPose.WaveSeconds;
                        if(r.A.SourceAction=="wave"&&u>.4f&&u<.6f)
                        {
                            float e=Math.Max(Vector3.Distance(r.M.Rig.AnkleInGroup(true),Lead),Vector3.Distance(r.M.Rig.AnkleInGroup(false),Rear));plants++;worstPlant=Math.Max(worstPlant,e);
                            Check(e<.015f,name+" "+hz+" Hz: the wavedash dip at "+t.ToString("0.000")+" s leaves the feet "+(e*100).ToString("0.0")+" cm off their marks");
                        }
                        Array.Copy(now,prev,22);has=true;prevKey=r.Key;
                    }
                    speeds[hz]=speed;
                }
                for(int b=0;b<22;b++)foreach(var (hz,lower) in new[]{(240,120),(480,240)})
                {
                    float low=speeds[lower][b];if(low<1)continue;float ratio=speeds[hz][b]/low;worstRatio=Math.Max(worstRatio,ratio);
                    Check(ratio<1.7f,name+" joint "+b+" snaps: its fastest speed is "+speeds[hz][b].ToString("0.0")+" m/s at "+hz+" Hz against "+low.ToString("0.0")+" m/s at "+lower+" Hz");
                }
                Check(switches>=12&&plants>=6,name+" exercised its clip switches and wave dips at every rate");
                Console.WriteLine("PASS Emperor wavedash rates "+name+": fastest joint speeds converge from 120 to 480 Hz (worst ratio "+worstRatio.ToString("0.00")+", no snap); "+switches+" clip switches at 30-480 Hz continue from the frame before (upper body within "+(worstSwitch*1000).ToString("0.0")+" mm); "+plants+" wave-dip frames plant the feet within "+(worstPlant*1000).ToString("0.0")+" mm of the marks");
            }
        }
        // The fighter pose path allocates nothing per frame once warm (wavedash chains, cancels and the transitions they start,
        // also those holding the ankles): four continuous cycles, the first one warming up. A one-off allocation by the runtime
        // on this thread lands on whichever frame it coincides with (CI once measured 1496 bytes on a commit that changed none
        // of this; it does not reproduce here, also with the JIT's optimisation off), so a dirty pass is measured once more on a
        // fresh rig: a real per-frame allocation in the pose path shows in both passes.
        private static void Allocation()
        {
            long first=AllocationPass(),used=first==0?0:AllocationPass();
            Check(used==0,"no allocation per frame ("+used+" bytes over three wavedash chains and cancels"+(first!=0?"; first pass "+first+" bytes":"")+")");
        }
        private static long AllocationPass()
        {
            var events=new List<(float,string)>();for(int c=0;c<4;c++){float o=c*1.6f;events.Add((o+.3f,"wave"));events.Add((o+.48f,"wave"));events.Add((o+.66f,"wave"));events.Add((o+1f,"wave"));events.Add((o+1.08f,"electric"));}
            var line=new Timeline(6.4f,.3f,.1f,6.4f,events.ToArray());var r=new Rig();long used=0;
            for(int f=0;f/60f<line.Duration;f++){long before=GC.GetAllocatedBytesForCurrentThread();line.At(r,60,f/60f);if(f/60f>=1.6f)used+=GC.GetAllocatedBytesForCurrentThread()-before;}
            return used;
        }
    }
}
#endif
