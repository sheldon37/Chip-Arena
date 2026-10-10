#if ARENA_TESTS
using System;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Marked (the Siren's fourth ranged move): the "You...." crosshair the pinned client/siren/fx.ts updateMarks puts on the
    // marked player, its markBeeps schedule and the siren-audio.ts tones (Tests/siren-mark-parity.json, exported by
    // tools/export_siren_mark_fixtures.mjs), the native timing it hangs on, who wears it on host, guests and the killcam
    // (NativeSirenMarkTracker), and scans of the runtime hooks so the telegraph cannot silently drop out again.
    internal static class SirenMarkChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren mark parity: "+what);count++;}
        private static void Near(double a,double b,string what,double tolerance=2e-6){Check(Math.Abs(a-b)<=tolerance+2e-7*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        // Quaternion (x, y, z, w) applied to a vector.
        private static void Rotate(double[] q,double x,double y,double z,out double rx,out double ry,out double rz)
        {
            double qx=q[0],qy=q[1],qz=q[2],qw=q[3],tx=2*(qy*z-qz*y),ty=2*(qz*x-qx*z),tz=2*(qx*y-qy*x);
            rx=x+qw*tx+(qy*tz-qz*ty);ry=y+qw*ty+(qz*tx-qx*tz);rz=z+qw*tz+(qx*ty-qy*tx);
        }
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-mark-parity.json")));var json=document.RootElement;

            // Timing: the contract's voice and lock, the native move (windup = voice + lock, the seeker thrown at its end).
            var timing=json.GetProperty("timing");var rule=NativeSirenRules.Rule("mark");
            Near(D(timing,"voice"),NativeSirenMark.Voice,"voice");Near(D(timing,"lock"),NativeSirenMark.Lock,"lock");Near(D(timing,"recover"),NativeSirenMark.Recover,"recover");
            Check(rule!=null&&Math.Abs(rule.Windup-(NativeSirenMark.Voice+NativeSirenMark.Lock))<1e-5&&Math.Abs(rule.Recovery-NativeSirenMark.Recover)<1e-5,"native Marked windup 2.4 s = voice + lock, recovery .5 s");
            Check(json.GetProperty("palette").GetProperty("warn").GetInt32()==NativeSirenMark.Warn,"PALETTE.warn");

            // markBeeps, and the tones siren-audio.ts plays at them.
            var beeps=new double[NativeSirenMark.BeepCap];
            foreach(var s in json.GetProperty("beeps").EnumerateArray())
            {
                double lockAt=D(s,"lockAt"),throwAt=D(s,"throwAt");var at=A(s.GetProperty("at"));int n=NativeSirenMark.Beeps(lockAt,throwAt,beeps);
                Check(n==at.Length,"beep count "+lockAt+".."+throwAt+": "+n+" != "+at.Length);for(int i=0;i<n;i++)Near(beeps[i],at[i],"beep "+i+" of "+lockAt+".."+throwAt);
            }
            {
                int n=NativeSirenMark.Beeps(11.2,12.4,beeps);var audio=json.GetProperty("audio");Check(audio.GetArrayLength()==n&&n==7,"seven beeps over the 1.2 s lock, one tone each");int i=0;
                foreach(var b in audio.EnumerateArray()){Near(NativeSirenMark.Tone(11.2,12.4,beeps[i]),D(b,"freq"),"beep tone "+i,1e-4);Near(beeps[i]-11,D(b,"when"),"beep instant "+i);i++;}
            }

            // updateMarks frame by frame: when it shows, where, how big, its turn, swing, colour and opacity, the geometry.
            var cam=A(json.GetProperty("camera").GetProperty("quat"));Rotate(cam,1,0,0,out double rx,out double ry,out double rz);Rotate(cam,0,1,0,out double ux,out double uy,out double uz);
            var tracker=new NativeSirenMarkTracker();int shownFrames=0;
            foreach(var f in json.GetProperty("frames").EnumerateArray())
            {
                double time=D(f,"time");bool gone=f.TryGetProperty("targetGone",out _),noMark=f.TryGetProperty("markGone",out _);
                tracker.Begin();if(!noMark)tracker.Record(7,2,11.2,12.4,!gone,time);tracker.End();
                bool shown=f.GetProperty("shown").GetBoolean();Check((tracker.Count==1)==shown,"shown at "+time+": "+(tracker.Count==1)+" != "+shown);if(!shown)continue;shownFrames++;
                Check(tracker[0].Target==2&&tracker[0].Owner==7,"only the marked player wears it");
                NativeSirenMark.Look(11.2,12.4,time,out double scale,out double spin,out double turn,out double opacity,out bool white);
                Near(scale,D(f,"scale"),"scale at "+time);Near(spin,D(f.GetProperty("ring"),"z"),"ring turn at "+time,1e-5);Near(opacity,D(f,"opacity"),"opacity at "+time);
                Check((f.GetProperty("color").GetInt32()==NativeSirenMark.Flash)==white&&(white||f.GetProperty("color").GetInt32()==NativeSirenMark.Warn),"white beep flash at "+time);
                var ring=f.GetProperty("ring");Near(D(ring,"inner"),NativeSirenMark.RingInner,"ring inner");Near(D(ring,"outer"),1,"ring outer");Check(ring.GetProperty("segments").GetInt32()==NativeSirenMark.RingSegments,"ring segments");
                var inner=f.GetProperty("inner");Near(D(inner,"inner"),NativeSirenMark.SquareInner,"square inner");Near(D(inner,"outer"),NativeSirenMark.SquareOuter,"square outer");
                Check(inner.GetProperty("segments").GetInt32()==NativeSirenMark.SquareSegments,"square: a 4-segment ring");Near(D(inner,"z"),NativeSirenMark.SquareTurn,"square turned 45 degrees");
                Check(!f.GetProperty("depthTest").GetBoolean()&&!f.GetProperty("depthWrite").GetBoolean()&&f.GetProperty("transparent").GetBoolean()&&f.GetProperty("doubleSide").GetBoolean()&&f.GetProperty("renderOrder").GetInt32()==999&&f.GetProperty("sharedMaterial").GetBoolean(),"one see-through glow material over everything");
                // Centred Lift above the target's body centre, turned with the camera.
                // The exporter's target stands at (2, .95, -3) and steps to (2.4, .95, -2.6) at 12 s; the crosshair follows.
                var pos=A(f.GetProperty("pos"));bool moved=time>=12;
                Near(pos[0],moved?2.4:2,"follows the target x");Near(pos[1],.95+NativeSirenMark.Lift,"lift above the body centre");Near(pos[2],moved?-2.6:-3,"follows the target z");
                var quat=A(f.GetProperty("quat"));for(int k=0;k<4;k++)Near(quat[k],cam[k],"billboard turned with the camera");
                int t=0;
                foreach(var tick in f.GetProperty("ticks").EnumerateArray())
                {
                    NativeSirenMark.Tick(t,turn,out double x,out double y,out double a);var p=A(tick.GetProperty("p"));var s=A(tick.GetProperty("s"));
                    Near(x,p[0],"tick "+t+" x at "+time);Near(y,p[1],"tick "+t+" y at "+time);Near(-a,D(tick,"z"),"tick "+t+" turn at "+time,1e-5);
                    Near(s[0],NativeSirenMark.TickWidth,"tick width");Near(s[1],NativeSirenMark.TickLength,"tick length");
                    // The world point as the runtime lays it (centre + camera right·x + camera up·y, scaled) against the source group.
                    Rotate(quat,p[0]*scale,p[1]*scale,p[2]*scale,out double wx,out double wy,out double wz);
                    Near(pos[0]+(rx*x+ux*y)*scale,pos[0]+wx,"tick "+t+" world x");Near(pos[1]+(ry*x+uy*y)*scale,pos[1]+wy,"tick "+t+" world y");Near(pos[2]+(rz*x+uz*y)*scale,pos[2]+wz,"tick "+t+" world z");
                    t++;
                }
                Check(t==NativeSirenMark.TickCount,"four ticks");
            }
            Check(shownFrames==17,"17 frames from the lock to the seeker's flight: "+shownFrames);
            // Closing from 1.6 to .85 over the lock, held at .85 once thrown; the ticks reach the cardinal points at the throw.
            NativeSirenMark.Look(11.2,12.4,11.2+.2,out double s0,out _,out double turn0,out _,out _);NativeSirenMark.Look(11.2,12.4,12.39,out double s1,out _,out _,out _,out _);NativeSirenMark.Look(11.2,12.4,15,out double s2,out _,out double turn2,out _,out bool w2);
            Check(s0>1.4&&s1<.9&&Math.Abs(s2-NativeSirenMark.ThrownSize)<1e-9&&turn0>.6&&turn2==0&&!w2,"the crosshair closes in, then holds after the throw");

            // Who wears it (host, guests, killcam): records from lockAt, the seeker after the record, never a bystander.
            {
                var m=new NativeSirenMarkTracker();
                m.Begin();m.Record(1,2,11.2,12.4,true,11.1);m.End();Check(m.Count==0,"nothing during the voice line");
                m.Begin();m.Record(1,2,11.2,12.4,true,11.2);m.Record(3,4,20,21.2,true,11.2);m.End();Check(m.Count==1&&m[0].Target==2,"from lockAt, only the marked player");
                m.Begin();m.Record(1,2,11.2,12.4,true,12.6);m.Seeker(1,m.Remembered(1),12.4,true,12.6);m.End();Check(m.Count==1,"record and seeker together draw one crosshair");
                // The record ends .5 s after the throw; a guest's seeker carries no target, the remembered one keeps the crosshair.
                m.Begin();m.Seeker(1,m.Remembered(1),12.4,true,14);m.End();Check(m.Count==1&&m[0].Target==2&&m[0].LockAt==11.2&&m[0].ThrowAt==12.4,"the seeker keeps the crosshair on the marked player");
                m.Begin();m.Seeker(1,m.Remembered(1),12.4,false,14.1);m.End();Check(m.Count==0,"none on a dead target");
                m.Begin();m.End();Check(m.Count==0&&m.Remembered(1)==0,"gone with the seeker");
                // A killcam that opens after the throw: the player the seeker flies at, already thrown.
                m.Begin();m.Seeker(5,6,30,true,31);m.End();Check(m.Count==1&&m[0].Target==6&&m[0].LockAt==30&&m[0].ThrowAt==30,"no record seen: the seeker's target, thrown");
                m.Begin();m.Seeker(5,0,30,true,31.1);m.End();Check(m.Count==1&&m[0].Target==6,"a guest's seeker without a target keeps the remembered one");
                m.Begin();m.Record(1,2,40,41.2,false,40.5);m.End();Check(m.Count==0,"no crosshair on a marked player who died");
                m.Clear();Check(m.Count==0&&m.Remembered(5)==0,"clear");
                Near(NativeSirenMark.Aim(0,1,0,0,0,3,0,1.1,8),Math.Cos(Math.Atan2(.1,8)),"seeker aim cosine");
                Check(NativeSirenMark.Aim(0,1,0,0,0,3,0,1.1,8)>NativeSirenMark.AimCos&&NativeSirenMark.Aim(0,1,0,0,0,3,3,1.1,8)<NativeSirenMark.AimCos&&NativeSirenMark.Aim(0,1,0,0,0,0,0,1,8)<0,"seeker target: the player ahead, not one beside it");
            }
            // First person: the source's billboard sits .17 m under the eye (inside the near plane); the native view holds it in
            // front of the eye, the ring at 90% of the half-height at the lock and closing to .85/1.6 of that.
            Check(NativeViewRules.EyeHeight-NativeSirenMark.Lift>.16,"the source crosshair centre is under the first-person eye");
            Near(NativeSirenMark.StartSize*NativeSirenMark.ScreenUnit(75)/(NativeSirenMark.ScreenDistance*Math.Tan(37.5*Math.PI/180)),NativeSirenMark.ScreenReach,"first-person ring at lock");

            // The runtime hooks that draw it, and the stand-in it replaced.
            string Read(string file)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",file));
            string dev=Read("Runtime/NativeDevEffects.cs"),draw=Read("Runtime/NativeFxDraw.cs"),shader=Read("Resources/NativeSirenFx.shader"),world=Read("Runtime/NativeWorldEffects.cs"),stage=Read("Runtime/NativeSirenStage.cs"),sound=Read("Runtime/NativeAudio.cs");
            Check(dev.Contains("DrawSirenFx(camera);SirenMarks(arena,camera,now);")&&dev.Contains("arena.LiveMarks()")&&dev.Contains("NativeSirenMark.Look(")&&dev.Contains("NativeSirenMark.Tick(")&&dev.Contains("fxDraw.Overlay")&&dev.Contains("markTracker.Clear()"),"DevEffects draws the crosshair for every viewer");
            Check(dev.Contains("NativeSirenMark.ScreenUnit(camera.fieldOfView)")&&dev.Contains("FirstPerson&&view!=null&&view.Seat.id==m.Target"),"the first-person target sees it");
            Check(draw.Contains("overlay.SetFloat(\"_ZTest\",(float)CompareFunction.Always)")&&draw.Contains("Overlay.Draw(overlay,world,0)")&&draw.Contains("RenderQueue.Overlay"),"the overlay layer draws over everything");
            Check(shader.Contains("ZTest [_ZTest]")&&shader.Contains("_ZTest(\"Depth test\",Float)=4"),"NativeSirenFx depth test is a property (LEqual by default)");
            Check(!world.Contains("arena.Marks()")&&!world.Contains("Rule.Id==\"mark\""),"the cube-ring stand-in is gone");
            Check(stage.Contains("lockAt=p.Siren.Move.At+1.2f,throwAt=p.Siren.Move.HitAt")&&stage.Contains("v.lockAt=m.At+1.2f;v.throwAt=m.HitAt;"),"mark records: lock at the cast + voice, throw at the hit");
            Check(sound.Contains("if(!menu)MarkBeeps(arena);")&&sound.Contains("float at=mark.lockAt,gap=.3f;for(int i=0;i<40&&at<mark.throwAt-.02f;i++,at+=gap,gap=Math.Max(.07f,gap*.8f))")&&sound.Contains("550+Mathf.Clamp01((at-mark.lockAt)/(mark.throwAt-mark.lockAt))*850"),"the lock-on beeps play");
            Console.WriteLine("PASS Siren Marked crosshair: "+count+" checks against fx.ts updateMarks/markBeeps, siren-audio.ts and the native hooks");
        }
    }
}
#endif
