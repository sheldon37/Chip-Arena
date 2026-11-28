#if ARENA_TESTS
using System;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // C12 part 1: the black-flame FlameField, every siren-* event, the claw slashes, projectile trails, fire patches,
    // eruptions, rain, the body fire, the first-person claws and the Ward Zero gore flames, replayed through the Core port
    // with the seeded random stream tools/export_siren_fx_fixtures.mjs fed the pinned source (Tests/siren-fx-parity.json).
    internal static class SirenFxChecks
    {
        /// <summary>The exporter's mulberry32.</summary>
        private sealed class Mulberry
        {
            private uint a;public Mulberry(uint seed){a=seed;}
            public double Next(){unchecked{a+=0x6D2B79F5u;uint t=(a^(a>>15))*(1u|a);t=(t+(t^(t>>7))*(61u|t))^t;return (t^(t>>14))/4294967296.0;}}
        }
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Siren fx parity: "+what);count++;}
        private static void Near(double a,double b,string what){Check(Math.Abs(a-b)<=2e-5+2e-6*Math.Abs(b)&&!double.IsNaN(a),what+": "+a+" != "+b);}
        private static double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
        private static double[] A(JsonElement e){var r=new double[e.GetArrayLength()];int i=0;foreach(var v in e.EnumerateArray())r[i++]=v.GetDouble();return r;}
        private static void Arrays(float[] actual,JsonElement expected,string what){var e=A(expected);for(int i=0;i<e.Length;i++)Near(actual[i],e[i],what+"["+i+"]");}
        private static void State(NativeFlameField f,JsonElement s,string what)
        {
            Check(f.Cursor==s.GetProperty("cursor").GetInt32(),what+" cursor "+f.Cursor+" != "+s.GetProperty("cursor").GetInt32());Check(f.Live==s.GetProperty("live").GetInt32(),what+" live "+f.Live);
            Arrays(f.Pos,s.GetProperty("pos"),what+" pos");Arrays(f.Vel,s.GetProperty("vel"),what+" vel");Arrays(f.Rot,s.GetProperty("rot"),what+" rot");Arrays(f.Spin,s.GetProperty("spin"),what+" spin");
            Arrays(f.Age,s.GetProperty("age"),what+" age");Arrays(f.Life,s.GetProperty("life"),what+" life");Arrays(f.Size,s.GetProperty("size"),what+" size");Arrays(f.Buoy,s.GetProperty("buoy"),what+" buoy");
            Arrays(f.Drag,s.GetProperty("drag"),what+" drag");Arrays(f.C0,s.GetProperty("c0"),what+" c0");Arrays(f.C1,s.GetProperty("c1"),what+" c1");
        }
        // three.js Matrix4.compose, column-major, from the Core's pose of slot i.
        private static void Instances(NativeFlameField f,JsonElement inst,int n,string what)
        {
            var m=A(inst.GetProperty("m"));var c=A(inst.GetProperty("c"));
            for(int i=0;i<n;i++)
            {
                bool shown=f.Pose(i,out double px,out double py,out double pz,out double x,out double y,out double z,out double w,out double sx,out double sy,out double sz,out double r,out double g,out double b);
                if(!shown){Near(m[i*16]*m[i*16]+m[i*16+5]*m[i*16+5]+m[i*16+10]*m[i*16+10],0,what+" hidden "+i);continue;}
                double x2=x+x,y2=y+y,z2=z+z,xx=x*x2,xy=x*y2,xz=x*z2,yy=y*y2,yz=y*z2,zz=z*z2,wx=w*x2,wy=w*y2,wz=w*z2;
                double[] e={(1-(yy+zz))*sx,(xy+wz)*sx,(xz-wy)*sx,0,(xy-wz)*sy,(1-(xx+zz))*sy,(yz+wx)*sy,0,(xz+wy)*sz,(yz-wx)*sz,(1-(xx+yy))*sz,0,px,py,pz,1};
                for(int k=0;k<16;k++)Near(e[k],m[i*16+k],what+" matrix "+i+"."+k);
                Near(r,c[i*3],what+" r "+i);Near(g,c[i*3+1],what+" g "+i);Near(b,c[i*3+2],what+" b "+i);
            }
        }
        private static NativeSirenEvent Type(string type)=>type switch
        {
            "siren-slam"=>NativeSirenEvent.Slam,"siren-rush"=>NativeSirenEvent.Rush,"siren-laugh"=>NativeSirenEvent.Laugh,"siren-mark"=>NativeSirenEvent.Laugh,"siren-lunge"=>NativeSirenEvent.Lunge,
            "siren-catch"=>NativeSirenEvent.Catch,"siren-crush"=>NativeSirenEvent.Crush,"siren-whiff"=>NativeSirenEvent.Whiff,"siren-cast"=>NativeSirenEvent.Cast,"siren-explode"=>NativeSirenEvent.Explode,
            "siren-eruption"=>NativeSirenEvent.Eruption,"siren-drop"=>NativeSirenEvent.Drop,"siren-seeker-out"=>NativeSirenEvent.SeekerOut,"siren-seeker-hit"=>NativeSirenEvent.SeekerHit,"siren-hit"=>NativeSirenEvent.Hit,
            _=>throw new Exception("unknown event "+type)
        };
        // The one-shot pieces after the last update, as the source's transients show them.
        private static void Transients(NativeSirenFx fx,JsonElement list,string what)
        {
            Check(fx.Count==list.GetArrayLength(),what+" transient count "+fx.Count+" != "+list.GetArrayLength());var m=new double[16];int i=0;
            foreach(var e in list.EnumerateArray())
            {
                var t=fx[i++];Near(t.Born,D(e,"born"),what+" born");Near(t.Life,D(e,"life"),what+" life");double u=Math.Max(0,(fx.Time-t.Born)/t.Life);string type=e.GetProperty("type").GetString();
                switch(type)
                {
                    case "shock":{Check(t.Kind==NativeSirenFxKind.Shock&&t.Dark==(D(e,"inner")<.7)&&t.Dark!=e.GetProperty("additive").GetBoolean(),what+" shock style");NativeSirenFx.ShockLook(t,u,out double s,out double o);Near(s,D(e,"scale"),what+" shock scale");Near(o,D(e,"opacity"),what+" shock opacity");var p=A(e.GetProperty("p"));Near(t.X,p[0],"shock x");Near(t.Y+.04,p[1],"shock y");Near(t.Z,p[2],"shock z");break;}
                    case "flash":{Check(t.Kind==NativeSirenFxKind.Flash,what+" flash");NativeSirenFx.FlashLook(t,u,out double s,out double o);Near(s,D(e,"scale"),what+" flash scale");Near(o,D(e,"opacity"),what+" flash opacity");var p=A(e.GetProperty("p"));Near(t.X,p[0],"flash x");Near(t.Y,p[1],"flash y");Near(t.Z,p[2],"flash z");break;}
                    case "explosion":{Check(t.Kind==NativeSirenFxKind.Explosion,what+" explosion");NativeSirenFx.ExplosionLook(t,u,out double s,out double turn,out double fade);var sc=A(e.GetProperty("s"));Near(s,sc[0],"shell x");Near(s*.85,sc[1],"shell y");Near(s,sc[2],"shell z");Near(turn,D(e,"turn"),"shell turn");Near(fade,D(e,"fade"),"shell fade");Near(2.2,D(e,"speed"),"shell speed");var p=A(e.GetProperty("p"));Near(t.Y+.2,p[1],"shell y");break;}
                    case "cracks":
                    {
                        Check(t.Kind==NativeSirenFxKind.Cracks,what+" cracks");NativeSirenFx.CrackLook(u,out double s,out double o);Near(s,D(e,"scale"),"cracks scale");Near(o,D(e,"opacity"),"cracks opacity");var p=A(e.GetProperty("p"));Near(t.Y,p[1],"cracks y");
                        int k=0;foreach(var box in e.GetProperty("boxes").EnumerateArray()){var v=A(box);int c=(t.Crack*NativeSirenFx.Cracks+k++)*3;Near(fx.CrackBoxes[c+2],v[0],"crack width");Near(fx.CrackBoxes[c+1],v[1],"crack length");Near(fx.CrackBoxes[c],v[2],"crack angle");}
                        break;
                    }
                    case "slash":{Check(t.Kind==NativeSirenFxKind.Slash,what+" slash");NativeSirenFx.SlashUniforms(t.Rend,u,out double sweep,out double fade);Near(sweep,D(e,"sweep"),"slash sweep");Near(fade,D(e,"fade"),"slash fade");fx.SlashMatrix(t,m);var mm=A(e.GetProperty("matrix"));for(int k=0;k<16;k++)Near(m[k],mm[k],what+" slash matrix "+k);break;}
                    default:throw new Exception("unknown transient "+type);
                }
            }
        }
        public static void Run(string root)
        {
            count=0;using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/siren-fx-parity.json")));var runs=document.RootElement.GetProperty("runs");

            // 1. The FlameField alone.
            {
                var rng=new Mulberry(11);var f=new NativeFlameField(40,rng.Next);var tones=new[]{NativeFlameTone.Black,NativeFlameTone.Soot,NativeFlameTone.Smoke,NativeFlameTone.Ember,NativeFlameTone.White,NativeFlameTone.Warn};
                for(int n=0;n<6;n++){double i=n;f.Emit(i*.3,1+i*.1,-i*.2,.4-i*.1,1+i*.2,-.3+i*.05,.5+i*.2,.06+i*.01,tones[n],2.2-i*.3,1.6+i*.2);}
                f.Burst(1,1.2,-.5,10,3,.8,.12,NativeFlameTone.Black,1.2,2.2);f.Burst(-1,.4,2,8,2,.6,.09,NativeFlameTone.Ember,.5);var run=runs.GetProperty("field");
                for(int i=0;i<30;i++){f.Update(i==12?.05:1.0/60);if(i==9)Instances(f,run.GetProperty("frames")[0].GetProperty("instances"),40,"field");}
                f.Burst(0,0,0,20,1.5,.4,.1,NativeFlameTone.Smoke,1,1.4);f.Update(1.0/60);State(f,run.GetProperty("state"),"field");
            }
            // 2. Every event, with its one-shot pieces and flames.
            int index=0;
            foreach(var e in runs.GetProperty("events").EnumerateArray())
            {
                var rng=new Mulberry((uint)(100+index++));var fx=new NativeSirenFx(rng.Next,1600);double q=D(e,"quality");string name=e.GetProperty("name").GetString();var ev=e.GetProperty("event");var p=ev.GetProperty("pos");
                fx.Begin(10,q);fx.Tick();double radius=ev.TryGetProperty("radius",out var r)?r.GetDouble():double.NaN;bool damnation=ev.TryGetProperty("message",out var msg)&&msg.GetString()=="damnation";
                fx.Event(Type(e.GetProperty("type").GetString()),D(p,"x"),D(p,"y"),D(p,"z"),radius,damnation);var counts=A(e.GetProperty("counts"));
                for(int k=1;k<=8;k++){fx.Begin(10+k/60.0,q);fx.Tick();Check(fx.Fire.Cursor==(int)counts[k-1],name+" cursor at "+k+": "+fx.Fire.Cursor+" != "+counts[k-1]);}
                Transients(fx,e.GetProperty("transients"),name);State(fx.Fire,e.GetProperty("state"),name);
            }
            // 3. Claw slashes.
            index=0;
            foreach(var s in runs.GetProperty("slashes").EnumerateArray())
            {
                var rng=new Mulberry((uint)(200+index++));var fx=new NativeSirenFx(rng.Next,1600);double q=D(s,"quality"),theta=D(s,"theta");var src=s.GetProperty("src");bool rend=s.GetProperty("type").GetString()=="siren-rend";
                fx.Begin(20,q);fx.Tick();double px=D(src,"x")+Math.Sin(theta)*.5,pz=D(src,"z")+Math.Cos(theta)*.5,yaw=Math.Atan2(px-D(src,"x"),pz-D(src,"z"));
                fx.Slash(rend,s.GetProperty("side").GetInt32(),D(src,"x"),D(src,"y")+.38,D(src,"z"),yaw);int look=0;
                for(int k=1;k<=30;k++){fx.Begin(20+k/60.0,q);fx.Tick();if(k==3||k==8){var l=s.GetProperty("looks")[look++];Check(fx.Fire.Cursor==l.GetProperty("cursor").GetInt32(),"slash flames laid by "+k);Transients(fx,l.GetProperty("transients"),"slash "+k);}}
                State(fx.Fire,s.GetProperty("state"),"slash");
            }
            // 4. The Last Laugh aura.
            {
                var rng=new Mulberry(260);var fx=new NativeSirenFx(rng.Next,1600);
                for(int k=0;k<=120;k++){double t=29.9+k/60.0;fx.Begin(t,1);if(t>=30&&t<32)fx.Aura(1,0,.95-.83*1,0,1);fx.Tick();}
                Transients(fx,runs.GetProperty("aura").GetProperty("transients"),"aura");
            }
            // 5. Projectiles.
            {
                var rng=new Mulberry(300);var fx=new NativeSirenFx(rng.Next,1600);var run=runs.GetProperty("shots");fx.Begin(40,1);fx.Tick();int k=0;
                foreach(var frame in run.GetProperty("frames").EnumerateArray())
                {
                    k++;fx.Begin(D(frame,"t"),k>12?.3:1);
                    foreach(var shot in frame.GetProperty("shots").EnumerateArray())
                    {
                        string id=shot.GetProperty("id").GetString(),kind=shot.GetProperty("kind").GetString();var sp=A(shot.GetProperty("p"));var sv=A(shot.GetProperty("v"));
                        fx.Shot(id=="h"?1:id=="d"?2:id=="s"?3:4,kind=="hellfire"?NativeSirenShotKind.Hellfire:kind=="damnation"?NativeSirenShotKind.Damnation:kind=="seeker"?NativeSirenShotKind.Seeker:NativeSirenShotKind.Drop,D(shot,"born"),sp[0],sp[1],sp[2],sv[0],sv[1],sv[2]);
                    }
                    fx.EndShots();fx.Tick();Check(fx.Fire.Cursor==frame.GetProperty("cursor").GetInt32(),"shots cursor at "+k+": "+fx.Fire.Cursor);
                }
                State(fx.Fire,run.GetProperty("state"),"shots");
            }
            // 6. Fire patches, the eruption and the rain.
            {
                var rng=new Mulberry(400);var fx=new NativeSirenFx(rng.Next,1600);var run=runs.GetProperty("ground");var world=run.GetProperty("world");
                void Step(double t,double q)
                {
                    fx.Begin(t,q);int id=0;
                    foreach(var f in world.GetProperty("patches").EnumerateArray()){var p=f.GetProperty("pos");fx.Patch(++id,f.GetProperty("kind").GetString()=="trail"?1:0,D(p,"x"),D(p,"y"),D(p,"z"),D(f,"radius"),D(f,"at"),D(f,"until"));}
                    fx.EndPatches();
                    foreach(var f in world.GetProperty("eruptions").EnumerateArray()){var p=f.GetProperty("pos");fx.Eruption(10,D(p,"x"),D(p,"y"),D(p,"z"),D(f,"radius"),D(f,"at"),D(f,"burstAt"),D(f,"until"));}
                    fx.EndEruptions();
                    foreach(var f in world.GetProperty("rains").EnumerateArray()){var p=f.GetProperty("center");fx.Rain(20,D(p,"x"),D(p,"y"),D(p,"z"),D(f,"radius"),D(f,"at"),D(f,"until"));}
                    fx.EndRains();fx.Tick();
                }
                Step(50,1);
                foreach(var frame in run.GetProperty("frames").EnumerateArray())
                {
                    Step(D(frame,"t"),D(frame,"quality"));Check(fx.Fire.Cursor==frame.GetProperty("cursor").GetInt32(),"ground cursor at "+D(frame,"t")+": "+fx.Fire.Cursor+" != "+frame.GetProperty("cursor").GetInt32());
                    var c=frame.GetProperty("column");var v=fx.Eruptions[0];Check(v.Column==c.GetProperty("visible").GetBoolean(),"eruption column visible");
                    if(v.Column){var s=A(c.GetProperty("s"));Near(v.Width,s[0],"column width");Near(v.Height,s[1],"column height");Near(v.Spin,D(c,"turn"),"column turn");Near(v.Fade,D(c,"fade"),"column fade");}
                }
                State(fx.Fire,run.GetProperty("state"),"ground");
            }
            // 7. Body fire.
            index=0;
            foreach(var b in runs.GetProperty("body").EnumerateArray())
            {
                var rng=new Mulberry((uint)(500+index++));var f=new NativeFlameField(420,rng.Next);var body=new NativeSirenBody();var cfg=b.GetProperty("cfg");var joints=A(b.GetProperty("joints"));
                foreach(var frame in b.GetProperty("frames").EnumerateArray())
                {
                    double dt=D(frame,"dt");body.Emit(f,joints,dt,D(cfg,"q"),D(cfg,"vx"),D(cfg,"vz"),cfg.GetProperty("grab").GetBoolean(),D(cfg,"flare"),D(cfg,"ranged"),D(frame,"t"),D(cfg,"lean"));f.Update(dt);
                    Check(f.Cursor==frame.GetProperty("cursor").GetInt32(),"body cursor at "+D(frame,"t")+": "+f.Cursor);
                }
                State(f,b.GetProperty("state"),"body "+index);
            }
            // 8. First-person claws, orbs and hand flames.
            {
                var rng=new Mulberry(600);var f=new NativeFlameField(NativeSirenFx.ViewCapacity,rng.Next);double acc=0;var run=runs.GetProperty("view");
                foreach(var frame in run.GetProperty("frames").EnumerateArray())
                {
                    double t=D(frame,"t"),ranged=NativeSirenView.Ranged(frame.GetProperty("mode").GetString()=="ranged",t,D(frame,"modeAt"));
                    for(int side=1;side>=-1;side-=2)
                    {
                        NativeSirenView.Arm(side,t,ranged,out double ax,out double az,out double ex,out double hx,out double hz);string s=side>0?"left":"right";
                        var arm=A(frame.GetProperty(s+"Arm"));Near(ax,arm[0],"arm x");Near(0,arm[1],"arm y");Near(az,arm[2],"arm z");var hand=A(frame.GetProperty(s+"Hand"));Near(hx,hand[0],"hand x");Near(hz,hand[2],"hand z");
                        if(side>0){var elbow=A(frame.GetProperty("leftElbow"));Near(ex,elbow[0],"elbow x");}
                    }
                    var claw=frame.GetProperty("claw");Check((ranged<.95)==claw.GetProperty("visible").GetBoolean(),"claws visible");Near(1-.75*ranged,D(claw,"y"),"claw retract");
                    var orb=frame.GetProperty("orb");Check((ranged>.05)==orb.GetProperty("visible").GetBoolean(),"orb visible");Near(NativeSirenView.Orb(0,t,ranged),D(orb,"s"),"orb scale");Near(NativeSirenView.Orb(1,t,ranged),D(frame,"orbRight"),"right orb scale");
                    acc=NativeSirenView.Flames(f,acc,1.0/60,ranged,A(frame.GetProperty("left")),A(frame.GetProperty("right")));f.Update(Math.Max(0,Math.Min(.05,1.0/60)));
                }
                Instances(f,run.GetProperty("instances"),NativeSirenFx.ViewCapacity,"view flames");
            }
            // 9. Ward Zero gore flames.
            {
                var rng=new Mulberry(700);var f=new NativeFlameField(220,rng.Next);var run=runs.GetProperty("gore");
                foreach(var h in run.GetProperty("hits").EnumerateArray()){var v=A(h);NativeGoreFlames.Hit(f,v[0],v[1],v[2],v[3]);}
                foreach(var g in run.GetProperty("gibs").EnumerateArray())
                {
                    var e=g.GetProperty("event");var p=e.GetProperty("pos");bool from=e.TryGetProperty("from",out var fr);
                    NativeGoreFlames.Gib(f,(uint)D(g,"seed"),D(p,"x"),D(p,"y"),D(p,"z"),from,from?D(fr,"x"):0,from?D(fr,"y"):0,from?D(fr,"z"):0,D(g,"quality"));
                }
                double acc=.25;var drips=A(run.GetProperty("drips"));
                for(int k=0;k<20;k++){acc=NativeGoreFlames.Drip(f,0,.6,1,1.0/60,k<10?1:.3,acc);Near(acc,drips[k],"drip accumulator");f.Update(Math.Max(0,Math.Min(.05,k==5?.08:1.0/60)));}
                State(f,run.GetProperty("state"),"gore");
                Check(NativeGoreFlames.Phase(0,0)>=0&&NativeGoreFlames.Phase(7,3)<1,"drip phase range");
            }
            // Pool bounds the runtime relies on.
            {
                var rng=new Mulberry(1);var fx=new NativeSirenFx(rng.Next);Check(fx.Fire.Capacity==NativeSirenFx.FireCapacity,"world pool 2048");
                fx.Begin(1,1);for(int i=0;i<400;i++)fx.Shock(0,0,0,1,5,true);Check(fx.Count==NativeSirenFx.MaxTransients,"transient cap");fx.Fire.Burst(0,0,0,5000,1,1,.1);Check(fx.Fire.Cursor==5000%NativeSirenFx.FireCapacity,"ring cursor wraps");
                fx.Clear();Check(fx.Count==0&&fx.Shots.Count==0,"clear");fx.Fire.Update(.01);Check(fx.Fire.Live==0,"cleared pool is empty");
                Check(NativeSirenFx.ArcPositions.Length==37*2*3&&NativeSirenFx.ArcIndices.Length==36*6,"crescent 37x2 vertices");
                NativeSirenFx.SlashUniforms(false,.067/.24,out double sweep,out _);Near(sweep,1.005,"swipe crosses the arc by .067 s");NativeSirenFx.SlashUniforms(true,.131/.42,out sweep,out _);Near(sweep,.99809523809,"rend crosses the arc by .131 s");
            }
            Console.WriteLine("PASS Siren effects (C12): "+count+" checks against flames.ts, fx.ts, motion.ts, first-person.ts and gore.ts");
        }
    }
}
#endif
