#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // C17: Ward Zero limb chunks, ink drops, revive beacons and the HELP bubble against values captured from the original
    // gore.ts / beacons.ts / downed.ts (Tests/gore-beacon-parity.json), plus determinism, caps, lifetime and floor rules.
    internal static class GoreBeaconChecks
    {
        private sealed class Collect : NativeBeaconRules.IMeshSink
        {
            public readonly List<float> Positions=new List<float>(),Colors=new List<float>(),Uv=new List<float>();public readonly List<int> Triangles=new List<int>();
            public int Vertex(float x,float y,float z,float u,float v,float flat,float r,float g,float b,float a){Positions.AddRange(new[]{x,y,z});Uv.AddRange(new[]{u,v,flat});Colors.AddRange(new[]{r,g,b,a});return Positions.Count/3-1;}
            public void Triangle(int a,int b,int c){Triangles.Add(a);Triangles.Add(b);Triangles.Add(c);}
        }
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/gore-beacon-parity.json")));var R=document.RootElement;int count=0;
            void Check(bool ok,string what){if(!ok)throw new Exception("Gore/beacon parity: "+what);count++;}
            void Near(double a,double b,double tolerance,string what){Check(Math.Abs(a-b)<=tolerance&&!double.IsNaN(a),what+": "+a+" != "+b);}
            double D(JsonElement e,string name)=>e.GetProperty(name).GetDouble();
            int Limb(string name){int i=Array.IndexOf(NativeGoreRules.Limbs,name);return i<0?-1:i;}

            foreach(var h in R.GetProperty("hash01").EnumerateArray())
                Near(NativeGoreRules.Hash01((uint)h.GetProperty("seed").GetDouble(),h.GetProperty("a").GetInt32(),h.GetProperty("b").GetInt32(),h.GetProperty("c").GetInt32()),D(h,"v"),1e-9,"hash01");
            foreach(var h in R.GetProperty("fnv").EnumerateArray()){string s=h.GetProperty("s").GetString();Check(NativeGoreRules.Fnv(s)==(uint)D(h,"v"),"fnv "+s);}
            foreach(int v in new[]{0,7,10042,-15,int.MaxValue,int.MinValue,1000000})Check(NativeGoreRules.FnvDigits(v)==NativeGoreRules.Fnv(v.ToString(System.Globalization.CultureInfo.InvariantCulture))&&NativeGoreRules.DigitCount(v)==v.ToString(System.Globalization.CultureInfo.InvariantCulture).Length,"digit hash "+v);

            // gib(): seed, velocity, size, spin and the ink count of every captured event.
            var events=new List<JsonElement>();
            foreach(var g in R.GetProperty("gibs").EnumerateArray())
            {
                var e=g.GetProperty("event");events.Add(e);float q=(float)D(g,"quality");string message=e.GetProperty("message").GetString();double time=D(e,"time");
                uint seed=NativeGoreRules.GibSeed(time,NativeGoreRules.Fnv(e.GetProperty("target").GetString()),NativeGoreRules.Fnv(message));Check(seed==(uint)D(g,"seed"),"gib seed "+message);
                var c=Gib(e);var v=g.GetProperty("v");var w=g.GetProperty("spin");var size=g.GetProperty("size");
                Near(c.Vx,D(v,"x"),1e-9,"gib vx");Near(c.Vy,D(v,"y"),1e-9,"gib vy");Near(c.Vz,D(v,"z"),1e-9,"gib vz");Near(c.Wx,D(w,"x"),1e-9,"spin x");Near(c.Wy,D(w,"y"),1e-9,"spin y");Near(c.Wz,D(w,"z"),1e-9,"spin z");
                Near(c.Sx,size[0].GetDouble(),1e-12,"size x");Near(c.Sy,size[1].GetDouble(),1e-12,"size y");Near(c.Sz,size[2].GetDouble(),1e-12,"size z");
                Check(NativeGoreRules.GibInk(q)==g.GetProperty("ink")[0].GetProperty("count").GetInt32(),"ink count");Check(Limb(message)>=0||c.Limb==3,"unknown limb falls back to a forearm");
            }
            foreach(var s in R.GetProperty("chunkAt").EnumerateArray())
            {
                var p=s.GetProperty("p");var v=s.GetProperty("v");var size=s.GetProperty("size");
                var c=new NativeGoreRules.Chunk{Px=D(p,"x"),Py=D(p,"y"),Pz=D(p,"z"),Vx=D(v,"x"),Vy=D(v,"y"),Vz=D(v,"z"),Sx=size[0].GetDouble(),Sy=size[1].GetDouble(),Sz=size[2].GetDouble()};
                bool landed=NativeGoreRules.ChunkAt(c,D(s,"age"),out double x,out double y,out double z);
                Check(landed==s.GetProperty("landed").GetBoolean(),"landed");Near(x,D(s,"x"),1e-9,"chunkAt x");Near(y,D(s,"y"),1e-9,"chunkAt y");Near(z,D(s,"z"),1e-9,"chunkAt z");
            }
            // HordeGore.update(): slot count, live chunks and every slot's matrix (float32 in three) for one event over time.
            void Slots(NativeGoreRules.ChunkList list,double t,JsonElement expected,string what)
            {
                int n=0;var slots=expected.EnumerateArray().GetEnumerator();
                for(int i=0;i<list.Count;i++)
                {
                    if(!NativeGoreRules.Pose(list[i],t,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double sx,out double sy,out double sz))continue;
                    Check(slots.MoveNext(),what+" extra slot");var e=slots.Current;var p=e.GetProperty("p");var q=e.GetProperty("q");var s=e.GetProperty("s");n++;
                    Near(x,p[0].GetDouble(),2e-6,what+" x");Near(y,p[1].GetDouble(),2e-6,what+" y");Near(z,p[2].GetDouble(),2e-6,what+" z");
                    double sign=qw*q[3].GetDouble()+qx*q[0].GetDouble()+qy*q[1].GetDouble()+qz*q[2].GetDouble()<0?-1:1;
                    Near(qx*sign,q[0].GetDouble(),5e-5,what+" qx");Near(qy*sign,q[1].GetDouble(),5e-5,what+" qy");Near(qz*sign,q[2].GetDouble(),5e-5,what+" qz");Near(qw*sign,q[3].GetDouble(),5e-5,what+" qw");
                    Near(sx,s[0].GetDouble(),2e-6,what+" sx");Near(sy,s[1].GetDouble(),2e-6,what+" sy");Near(sz,s[2].GetDouble(),2e-6,what+" sz");
                }
                Check(!slots.MoveNext(),what+" missing slot");
            }
            NativeGoreRules.ChunkList single=null;int lastEvent=-1;
            foreach(var pose in R.GetProperty("poses").EnumerateArray())
            {
                int i=pose.GetProperty("event").GetInt32();if(i!=lastEvent){single=new NativeGoreRules.ChunkList();single.Add(Gib(events[i]));lastEvent=i;}
                double t=D(pose,"t");single.Update(t);Check(single.Count==pose.GetProperty("list").GetInt32(),"list length");Check(single.Live(t)==pose.GetProperty("live").GetInt32(),"live chunks");
                Slots(single,t,pose.GetProperty("slots"),"pose "+i+" @"+D(pose,"age"));
            }
            var several=new NativeGoreRules.ChunkList();for(int i=0;i<4;i++){var e=events[i];several.Add(Gib(e,50+i*.2));}
            foreach(var m in R.GetProperty("multi").EnumerateArray()){double t=D(m,"t");several.Update(t);Slots(several,t,m.GetProperty("slots"),"multi @"+t);}
            // Fifteen gibs within .15 s: the newest twelve live; a rewind past them drops them all (gore.ts:61,71).
            var cap=new NativeGoreRules.ChunkList();var capRef=R.GetProperty("cap");
            for(int i=0;i<15;i++)cap.Add(NativeGoreRules.Gib(200+i*.01,(uint)i,3,i,1,0,true,0,0,1));
            Check(cap.Count==capRef.GetProperty("length").GetInt32()&&Math.Abs(cap[0].At-D(capRef,"first"))<1e-9&&cap.Live(200.2)==capRef.GetProperty("live").GetInt32(),"chunk cap");
            cap.Update(199);Check(cap.Count==capRef.GetProperty("afterRewind").GetInt32(),"rewind clears future chunks");
            foreach(var d in R.GetProperty("drops").EnumerateArray())Check(NativeGoreRules.HitInk((float)D(d,"q"))==d.GetProperty("n").GetInt32(),"hit drops");
            var slotColors=R.GetProperty("slotColors");int Hex(int k){int v=0;for(int c=0;c<3;c++){double l=slotColors[k*3+c].GetDouble(),s=l<=.0031308?l*12.92:1.055*Math.Pow(l,1/2.4)-.055;v=v<<8|(int)Math.Round(s*255);}return v;}
            Check(Hex(0)==NativeGoreRules.ChunkOlive&&Hex(1)==NativeGoreRules.ChunkShell&&Hex(2)==NativeGoreRules.ChunkShell&&Hex(3)==NativeGoreRules.ChunkOlive,"chunk slot colours");

            // Rules the source states but the capture does not pin: determinism, floor, lifetime, the ink pool.
            var rng=new Random(17);
            for(int k=0;k<200;k++)
            {
                uint seed=(uint)rng.Next();int limb=rng.Next(9);double y0=.2+rng.NextDouble()*1.5;
                var a=NativeGoreRules.Gib(10,seed,limb,0,y0,0,true,rng.NextDouble()-.5,rng.NextDouble()-.5,rng.NextDouble()-.5);
                var zero=NativeGoreRules.Gib(10,seed,limb,0,y0,0,true,0,0,0);var none=NativeGoreRules.Gib(10,seed,limb,0,y0,0,false,0,0,0);
                Check(Math.Abs(zero.Vx)<=.6&&Math.Abs(zero.Vz)<=.6&&none.Vz>=2.4-.6&&Math.Abs(none.Vx)<=.6,"a zero direction stays finite; no direction throws along +z");
                var again=NativeGoreRules.Gib(a.At,a.Seed,limb,a.Px,a.Py,a.Pz,true,a.Vx,a.Vy,a.Vz);var twice=NativeGoreRules.Gib(a.At,a.Seed,limb,a.Px,a.Py,a.Pz,true,a.Vx,a.Vy,a.Vz);
                Check(again.Vx==twice.Vx&&again.Vy==twice.Vy&&again.Wz==twice.Wz,"gib is deterministic");
                double rest=a.Sy*.25,previousX=a.Px;bool landedOnce=false;double landX=0,landZ=0;
                for(double age=0;age<4.2;age+=1/60.0)
                {
                    bool landed=NativeGoreRules.ChunkAt(a,age,out double x,out double y,out double z);Check(y>=rest-1e-9,"chunk stays above the floor");
                    if(landed){Check(Math.Abs(y-rest)<1e-12,"landed chunk rests on the floor");if(age>3){if(!landedOnce){landX=x;landZ=z;landedOnce=true;}else Check(x==landX&&z==landZ,"skid stops");}}
                    Check(Math.Abs(x-previousX)<.2,"no jump at landing");previousX=x;
                    bool drawn=NativeGoreRules.Pose(a,10+age,out _,out _,out _,out _,out _,out _,out _,out double sx,out double sy,out _);
                    Check(drawn==age<NativeGoreRules.ChunkLife,"chunk lifetime");if(drawn)Check(sy<=a.Sy+1e-12&&sx>=0&&(age<3.4||sy<a.Sy),"chunk shrinks only in its last .6 s");
                }
                Check(!NativeGoreRules.Pose(a,9.99,out _,out _,out _,out _,out _,out _,out _,out _,out _,out _),"chunk hidden before its event");
            }
            Check(NativeGoreRules.Spawned(NativeGoreRules.GibInk(1),1)==11&&NativeGoreRules.Spawned(NativeGoreRules.GibInk(.3f),.3f)==3&&NativeGoreRules.Spawned(NativeGoreRules.HitInk(.3f),.3f)==3,"ink counts at full and reduced effects");
            Check(NativeGoreRules.RootLimb(2|8,1)&&!NativeGoreRules.RootLimb(2|8,3)&&NativeGoreRules.RootLimb(8,3)&&NativeGoreRules.RootLimb(1,0)&&NativeGoreRules.RootLimb(32|128|64|256,5)&&!NativeGoreRules.RootLimb(32|128|64|256,7),"one chunk per severed part");
            var drops=new NativeGoreRules.DropList();for(int i=0;i<NativeGoreRules.DropMax+40;i++)drops.Add(NativeGoreRules.MakeDrop(5+i*.001,9,i,0,1,0,3.2));Check(drops.Count==NativeGoreRules.DropMax&&drops[0].At>5.03,"ink pool cap keeps the newest");
            for(int i=0;i<drops.Count;i++){var d=drops[i];Check(d.Scale>=.035&&d.Scale<=.1&&d.Life>=.25&&d.Life<=.75&&Math.Abs(d.Vx)<=1.6&&d.Vy>=0&&d.Vy<=3.2,"ink drop ranges");
                Check(NativeGoreRules.DropAt(d,d.At+.1,out _,out double y,out _,out double s)&&Math.Abs(y-(1+d.Vy*.1-.025))<1e-9&&Math.Abs(s-d.Scale*Math.Pow(.98,6))<1e-9&&!NativeGoreRules.DropAt(d,d.At+d.Life,out _,out _,out _,out _),"ink drop motion");}
            drops.Update(6);Check(drops.Count==0,"ink drops expire");

            // beaconState, the ring draw range and its first vertices, HELP size and bob.
            foreach(var b in R.GetProperty("beacon").EnumerateArray())
            {
                var row=b.GetProperty("row");double t=D(b,"t");bool self=row.TryGetProperty("selfReviveAt",out var selfAt);
                if(self){double from=row.TryGetProperty("downAt",out var down)?down.GetDouble():t;Near(NativeBeaconRules.Progress(t,from,selfAt.GetDouble()),D(b,"progress"),1e-6,"self-revive progress");
                    Check(b.GetProperty("label").GetString()==NativeBeaconRules.SelfLabel&&b.GetProperty("sub").GetString()==NativeBeaconRules.Clock(NativeBeaconRules.ClockSeconds(selfAt.GetDouble()-t)),"self-revive label");}
                else Check(b.GetProperty("label").GetString()==NativeBeaconRules.DownLabel&&D(b,"progress")==0,"revive label");
            }
            foreach(var r in R.GetProperty("ring").EnumerateArray())Check(NativeBeaconRules.Segments*6+NativeBeaconRules.ArcSegments(D(r,"p"))*6==r.GetProperty("n").GetInt32(),"ring draw count");
            var ring=new Collect();NativeBeaconRules.Ring(ring,0,0,1,0,1,0xffffff,true);var rv=R.GetProperty("ringVerts");
            Check(ring.Triangles.Count==rv.GetProperty("count").GetInt32(),"ring vertex count");
            void Corner(int tri,JsonElement expected,string what){for(int k=0;k<6;k++){int v=ring.Triangles[tri*3+k];Near(ring.Positions[v*3],expected[k*3].GetDouble(),1e-5,what+" x");Near(ring.Positions[v*3+1]-.03,expected[k*3+1].GetDouble(),1e-6,what+" y");Near(ring.Positions[v*3+2],expected[k*3+2].GetDouble(),1e-5,what+" z");}}
            Corner(0,rv.GetProperty("first"),"base ring");Corner(NativeBeaconRules.Segments*2,rv.GetProperty("arcFirst"),"progress arc");
            Near(ring.Colors[0],rv.GetProperty("levels")[0].GetDouble(),1e-6,"base ring level");Near(ring.Colors[ring.Triangles[NativeBeaconRules.Segments*6]*4],rv.GetProperty("levels")[1].GetDouble(),1e-6,"arc level");
            var column=new Collect();NativeBeaconRules.Column(column,0,0,1,0xffffff,true,.5f);
            Check(column.Positions.Count==50*3&&column.Triangles.Count==24*6&&column.Positions[1]==NativeBeaconRules.Height&&column.Positions[25*3+1]==0&&Math.Abs(column.Positions[2]-.665f)<1e-6&&Math.Abs(column.Positions[25*3+2]-.8075f)<1e-6&&column.Uv[1]==1&&column.Uv[25*3+1]==0,"beacon column geometry");
            NativeBeaconRules.Shade(0xffffff,.35f,false,out float gr,out _,out _);NativeBeaconRules.Shade(NativeBeaconRules.Self,1,false,out float sr,out float sg,out float sb);NativeBeaconRules.Shade(0x808080,.5f,true,out float lr,out _,out _);
            Check(Math.Abs(gr-.6262)<1e-3&&Math.Abs(sr-1)<1e-6&&Math.Abs(sg-0xd2/255f)<1e-6&&Math.Abs(sb-0x7a/255f)<1e-6&&Math.Abs(lr-.5*.21586)<1e-4,"beacon level shading (linear light) for gamma and linear targets");
            foreach(var h in R.GetProperty("helpWidth").EnumerateArray())Near(NativeBeaconRules.HelpWidth((float)D(h,"d")),D(h,"w"),1e-6,"HELP width");
            foreach(var h in R.GetProperty("helpBob").EnumerateArray())Near(NativeBeaconRules.HelpBob(D(h,"t"),NativeGoreRules.Fnv(h.GetProperty("id").GetString())),D(h,"v"),1e-6,"HELP bob");
            Check(NativeBeaconRules.MultiplyLinear(0xdffaff,NativeBeaconRules.Down)==NativeBeaconRules.DownText&&NativeBeaconRules.MultiplyLinear(0xdffaff,NativeBeaconRules.Self)==NativeBeaconRules.SelfText
                &&NativeBeaconRules.MultiplyLinear(0x5fe8ff,NativeBeaconRules.Down)==NativeBeaconRules.DownGlow&&NativeBeaconRules.MultiplyLinear(0x5fe8ff,NativeBeaconRules.Self)==NativeBeaconRules.SelfGlow,"label colours");
            Check(NativeBeaconRules.Clock(4)=="0:04"&&NativeBeaconRules.Clock(75)=="1:15"&&NativeBeaconRules.ClockSeconds(3.01)==4&&NativeBeaconRules.ClockSeconds(-2)==0,"clock");

            // The canvases: the motes map (alpha) and the HELP bubble (2x2 means) against the original's Chromium pixels.
            var motes=new byte[64*256*4];NativeBeaconRules.Motes(motes,false);var motesRef=Convert.FromBase64String(R.GetProperty("motesAlpha").GetString());int worst=0;
            for(int i=0;i<64*256;i++)worst=Math.Max(worst,Math.Abs(motes[i*4+3]-motesRef[i]));Check(worst<=3,"motes map alpha (worst "+worst+")");
            var flipped=new byte[64*256*4];NativeBeaconRules.Motes(flipped,true);Check(flipped[(255*64)*4+3]==motes[3]&&flipped[3]==motes[(255*64)*4+3],"motes map rows bottom-up");
            var help=new byte[256*128*4];NativeBeaconRules.HelpBubble(help,false);var helpRef=Convert.FromBase64String(R.GetProperty("helpShape2x").GetString());double total=0;int worstAlpha=0;
            for(int y=0;y<64;y++)for(int x=0;x<128;x++)for(int c=0;c<4;c++)
            {
                double mean=0;for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)mean+=help[((y*2+dy)*256+x*2+dx)*4+c]/4.0;int diff=(int)Math.Abs(Math.Round(mean)-helpRef[(y*128+x)*4+c]);
                if(c==3){total+=diff;worstAlpha=Math.Max(worstAlpha,diff);}
            }
            Check(total/(128*64)<1&&worstAlpha<=48,"HELP bubble shape (mean alpha diff "+(total/(128*64)).ToString("0.00")+", worst "+worstAlpha+")");
            Check(help[(53*256+128)*4+3]==255&&help[(53*256+128)*4]==255&&help[(53*256+128)*4+1]==248&&help[(8*256+128)*4]==255&&help[(8*256+128)*4+1]==51&&help[(124*256+20)*4+3]<8,"HELP bubble fill, stroke and clear corners");
            Console.WriteLine("PASS Ward Zero gore and revive beacons vs the original (gore.ts chunks/ink, beacons.ts column/ring/label, downed.ts HELP, canvases): "+count+" assertions");
        }
        private static NativeGoreRules.Chunk Gib(JsonElement e,double? time=null)
        {
            double t=time??e.GetProperty("time").GetDouble();string message=e.GetProperty("message").GetString();var p=e.GetProperty("pos");bool from=e.TryGetProperty("from",out var f);
            uint seed=NativeGoreRules.GibSeed(t,NativeGoreRules.Fnv(e.GetProperty("target").GetString()),NativeGoreRules.Fnv(message));
            return NativeGoreRules.Gib(t,seed,Array.IndexOf(NativeGoreRules.Limbs,message),p.GetProperty("x").GetDouble(),p.GetProperty("y").GetDouble(),p.GetProperty("z").GetDouble(),from,from?f.GetProperty("x").GetDouble():0,from?f.GetProperty("y").GetDouble():0,from?f.GetProperty("z").GetDouble():0);
        }
    }
}
#endif
