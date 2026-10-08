#if ARENA_TESTS
using System;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Chip blasts: NativeBlastSim against the pinned source's drawn frames (tools/export_blast_fixtures.mjs runs
    // client/chip-blasts.ts), plus determinism, front timing, pool capacities, minimum voxels, quality scaling, the
    // effect cues and the renderer's cube winding.
    internal static class BlastChecks
    {
        private const double Dt=1.0/60;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Chip blast check failed: "+what);}
        private static NativeBlastStyle Parse(string name){foreach(NativeBlastStyle s in Enum.GetValues(typeof(NativeBlastStyle)))if(string.Equals(s.ToString(),name,StringComparison.OrdinalIgnoreCase))return s;throw new Exception("Unknown blast style "+name);}
        public static void Run(string root)
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/blast-parity.json")));var json=document.RootElement;var k=json.GetProperty("constants");var capacity=k.GetProperty("capacity");
            Check(k.GetProperty("frontSeconds").GetDouble()==NativeBlastSim.FrontSeconds&&k.GetProperty("minVoxels").GetInt32()==NativeBlastSim.MinVoxels,"front seconds and minimum voxels match the source");
            Check(capacity.GetProperty("solid").GetInt32()==NativeBlastSim.SolidCapacity&&capacity.GetProperty("glow").GetInt32()==NativeBlastSim.GlowCapacity&&capacity.GetProperty("lights").GetInt32()==NativeBlastSim.LightCapacity,"pool capacities match the source");
            int cases=0,frames=0;foreach(var c in json.GetProperty("cases").EnumerateArray()){frames+=Replay(c,cases);cases++;}
            Determinism();Front();Capacity();Minimum();Quality();Cues();Winding();
            Console.WriteLine("PASS "+cases+" source chip blast runs ("+frames+" drawn frames: 11 styles x 3 radii, quality/compat, tints, impacts, 20 fields, line, paddle) and blast determinism, front, capacity, minimum-voxel, quality, cue and winding checks");
        }
        private static void Start(NativeBlastSim sim,JsonElement c,out NativeBlastField? field)
        {
            string kind=c.GetProperty("kind").GetString(),effect=c.GetProperty("effect").GetString();double R=c.GetProperty("radius").GetDouble(),q=c.GetProperty("quality").GetDouble();bool compat=c.GetProperty("compat").GetBoolean();field=null;
            if(kind=="blast"){string tag=c.TryGetProperty("tag",out var t)?t.GetString():null;var style=Parse(effect);if(tag!=null)Check(NativeBlastSim.StyleOf(tag)==style,"style of "+tag);sim.Blast(style,0,.95,0,R,q,compat,NativeBlastSim.TintOf(tag));}
            else if(kind=="impact")sim.Impact(0,.95,0,R,c.GetProperty("hot").GetInt32(),q,compat,c.GetProperty("ground").GetBoolean());
            else if(kind=="field"){var fx=c.GetProperty("fx");field=new NativeBlastField{Id=7,Kind=effect,Fx=fx.ValueKind==JsonValueKind.Null?null:fx.GetString(),X=0,Y=.95,Z=0,Radius=R,Born=0,Until=c.GetProperty("until").GetDouble(),LockAt=c.GetProperty("lockAt").GetDouble()};}
            else if(kind=="line")sim.Line(0,.95,0,4,1.5,2,0xffffff,0xb66cff,1,.3,.22,.08);
            else if(kind=="paddle")sim.Paddle(0,1,0,1,.2,.5,0xff5269,1);
            else throw new Exception("Unknown fixture kind "+kind);
        }
        private static void Near(double a,double b,double tolerance,string what){if(!(Math.Abs(a-b)<=tolerance+Math.Abs(b)*2e-5))throw new Exception("Chip blast parity: "+what+": "+a+" != "+b);}
        private static int Replay(JsonElement c,int index)
        {
            var sim=new NativeBlastSim();Start(sim,c,out var field);int frame=0,count=0;double q=c.GetProperty("quality").GetDouble();bool compat=c.GetProperty("compat").GetBoolean();
            string name="#"+index+" "+c.GetProperty("kind").GetString()+":"+c.GetProperty("effect").GetString()+(c.TryGetProperty("fx",out var fxName)&&fxName.ValueKind==JsonValueKind.String?":"+fxName.GetString():"")+" r"+c.GetProperty("radius").GetDouble();
            foreach(var f in c.GetProperty("frames").EnumerateArray())
            {
                int target=(int)NativeBlastSim.Round(f.GetProperty("t").GetDouble()/Dt);
                while(frame<target){double time=frame*Dt;if(field.HasValue&&time<field.Value.Until)sim.Field(field.Value,time,q,compat);sim.Update(Dt);frame++;}
                string at=name+" t="+f.GetProperty("t").GetDouble();
                Check(sim.Solid.Drawn==f.GetProperty("solid").GetInt32()&&sim.Glow.Drawn==f.GetProperty("glow").GetInt32(),at+" voxel counts "+sim.Solid.Drawn+"+"+sim.Glow.Drawn+" vs source "+f.GetProperty("solid").GetInt32()+"+"+f.GetProperty("glow").GetInt32());
                var sums=new double[9];int n=0;float[] first=null;
                foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Drawn;i++)
                {
                    int w=i*NativeBlastPool.Stride;var o=pool.Output;n++;if(first==null){first=new float[13];Array.Copy(o,w,first,0,13);}
                    for(int j=0;j<6;j++)sums[j]+=o[w+j]*(double)n;for(int j=0;j<3;j++)sums[6+j]+=o[w+10+j]*(double)n;
                }
                int s=0;foreach(var v in f.GetProperty("sums").EnumerateArray()){Near(sums[s],v.GetDouble(),1e-3,at+" weighted sum "+s);s++;}
                var expected=f.GetProperty("first");
                if(expected.ValueKind==JsonValueKind.Null)Check(first==null,at+" no voxel");
                else
                {
                    var e=new double[13];int j=0;foreach(var v in expected.EnumerateArray())e[j++]=v.GetDouble();
                    for(int i=0;i<6;i++)Near(first[i],e[i],2e-4,at+" first voxel "+i);for(int i=10;i<13;i++)Near(first[i],e[i],2e-4,at+" first colour "+i);
                    double dot=first[6]*e[6]+first[7]*e[7]+first[8]*e[8]+first[9]*e[9];Check(Math.Abs(dot)>1-1e-4,at+" first rotation "+dot);
                }
                int lit=0;foreach(var light in f.GetProperty("lights").EnumerateArray())
                {
                    NativeBlastFlash flash=null;int seen=0;foreach(var candidate in sim.Flashes)if(candidate.Intensity>0&&seen++==lit){flash=candidate;break;}
                    Check(flash!=null,at+" light "+lit);var l=new double[8];int j=0;foreach(var v in light.EnumerateArray())l[j++]=v.GetDouble();NativeBlastSim.Linear(flash.Color,out double r,out double g,out double b);
                    var got=new[]{flash.X,flash.Y,flash.Z,r,g,b,flash.Intensity,flash.Distance};for(int i=0;i<8;i++)Near(got[i],l[i],2e-4,at+" light "+lit+" value "+i);lit++;
                }
                int on=0;foreach(var flash in sim.Flashes)if(flash.Intensity>0)on++;Check(on==lit,at+" lit lights "+on+" vs "+lit);count++;
            }
            return count;
        }
        private static void Script(NativeBlastSim sim,int steps)
        {
            sim.Blast(NativeBlastStyle.Fire,1,.95,-2,3);sim.Impact(-1,1.2,0,1.5,0xffaa64,1,false,true);sim.Blast(NativeBlastStyle.Shock,4,.95,4,2.4,.6,false,NativeBlastSim.TintOf("ball-lightning"));
            var field=new NativeBlastField{Id=3,Kind="storm",X=0,Y=.95,Z=3,Radius=4,Born=0,Until=1};
            for(int i=0;i<steps;i++){if(i*Dt<field.Until)sim.Field(field,i*Dt,1);if(i==20)sim.Blast(NativeBlastStyle.Void,0,.95,0,5);sim.Update(Dt);}
        }
        private static bool Same(NativeBlastSim a,NativeBlastSim b)
        {
            foreach(var (x,y) in new[]{(a.Solid,b.Solid),(a.Glow,b.Glow)}){if(x.Drawn!=y.Drawn||x.Count!=y.Count)return false;for(int i=0;i<x.Drawn*NativeBlastPool.Stride;i++)if(BitConverter.SingleToInt32Bits(x.Output[i])!=BitConverter.SingleToInt32Bits(y.Output[i]))return false;}
            for(int i=0;i<a.Flashes.Length;i++)if(a.Flashes[i].Intensity!=b.Flashes[i].Intensity||a.Flashes[i].X!=b.Flashes[i].X)return false;return true;
        }
        private static void Determinism()
        {
            var a=new NativeBlastSim();var b=new NativeBlastSim();Script(a,40);Script(b,40);Check(a.Solid.Drawn+a.Glow.Drawn>100&&Same(a,b),"two seeded runs draw identical voxels");
            b.Reset();Script(b,40);Check(Same(a,b),"Reset rewinds the generator and clock");
            var c=new NativeBlastSim(new NativeBlastRandom(12345).Next);Script(c,40);Check(!Same(a,c),"an injected generator changes the voxels");
            var d=new NativeBlastSim(new NativeBlastRandom(12345).Next);Script(d,40);Check(Same(c,d),"an injected seeded generator is repeatable");
        }
        private static int Within(NativeBlastPool pool,double cx,double cy,double cz,double radius,double tolerance,bool horizontal=false)
        {int n=0;for(int i=0;i<pool.Count;i++){var p=pool.At(i);if(p.age<0)continue;double dx=p.x-cx,dy=horizontal?0:p.y-cy,dz=p.z-cz;if(Math.Abs(Math.Sqrt(dx*dx+dy*dy+dz*dz)-radius)<=tolerance)n++;}return n;}
        private static readonly NativeBlastStyle[] ShellStyles={NativeBlastStyle.Fire,NativeBlastStyle.Mine,NativeBlastStyle.Meteor,NativeBlastStyle.Kinetic,NativeBlastStyle.Shock,NativeBlastStyle.Implosion,NativeBlastStyle.Toxic};
        // The volumetric front reaches the gameplay radius exactly at FrontSeconds: the shell from the ground point under the
        // blast, Nova's sphere from its centre; implosions start on the radius.
        private static void Front()
        {
            int steps=(int)NativeBlastSim.Round(NativeBlastSim.FrontSeconds/Dt);Check(Math.Abs(steps*Dt-NativeBlastSim.FrontSeconds)<1e-9,"front time is whole frames");
            foreach(double R in new[]{.4,1.2,3,6})
            {
                foreach(var style in ShellStyles)
                {
                    var sim=new NativeBlastSim();sim.Blast(style,2,1.35,-1,R);for(int i=0;i<steps-1;i++)sim.Update(Dt);
                    int early=Within(sim.Glow,2,.4,-1,R,.005);sim.Update(Dt);int at=Within(sim.Glow,2,.4,-1,R,.005);
                    Check(at>=NativeBlastSim.MinVoxels&&at>early+NativeBlastSim.MinVoxels/2,style+" front reaches r="+R+" at "+NativeBlastSim.FrontSeconds+" s ("+early+" -> "+at+")");
                    sim.Update(Dt);Check(Within(sim.Glow,2,.4,-1,R,.005)<at-NativeBlastSim.MinVoxels/2,style+" front ends on the radius r="+R);
                }
                var nova=new NativeBlastSim();nova.Blast(NativeBlastStyle.Nova,0,.95,0,R);for(int i=0;i<steps;i++)nova.Update(Dt);Check(Within(nova.Glow,0,.95,0,R,.005)>=NativeBlastSim.MinVoxels,"nova front r="+R);
                foreach(var style in new[]{NativeBlastStyle.Implosion,NativeBlastStyle.Void}){var sim=new NativeBlastSim();sim.Blast(style,0,.95,0,R);Check(Within(sim.Glow,0,0,0,R,1e-4,true)>=NativeBlastSim.MinVoxels,style+" starts on the radius r="+R);}
            }
        }
        private static void Capacity()
        {
            var sim=new NativeBlastSim();Check(sim.Solid.Capacity==900&&sim.Glow.Capacity==700&&sim.Flashes.Length==3,"initial pools");
            int spawned=0;for(int i=0;i<20;i++)spawned+=sim.Blast(NativeBlastStyle.Fire,i,.95,0,6);
            Check(sim.Solid.Count+sim.Glow.Count==spawned&&sim.Solid.Count>900&&sim.Glow.Count>700,"pools grow instead of dropping voxels");
            Check(sim.Solid.Capacity%900==0&&IsPowerOfTwo(sim.Solid.Capacity/900)&&sim.Glow.Capacity%700==0&&IsPowerOfTwo(sim.Glow.Capacity/700),"pools double");
            // Past the longest spawn delay (smoke, .18 s) every live voxel is drawn.
            for(int i=0;i<12;i++)sim.Update(Dt);int lit=0;foreach(var f in sim.Flashes)if(f.Intensity>0)lit++;Check(lit==3,"at most three pooled lights");
            Check(sim.Solid.Drawn==sim.Solid.Count&&sim.Glow.Drawn==sim.Glow.Count,"every live voxel is drawn");
            sim.Clear();Check(sim.Solid.Capacity==900&&sim.Glow.Capacity==700&&sim.Solid.Count==0&&sim.Glow.Count==0&&sim.FieldClocks==0,"clear reclaims the pools");
        }
        private static bool IsPowerOfTwo(int v)=>v>0&&(v&(v-1))==0;
        private static void Minimum()
        {
            int steps=(int)NativeBlastSim.Round(NativeBlastSim.FrontSeconds/Dt);
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))
            {
                var sim=new NativeBlastSim();int n=sim.Blast(style,0,.95,0,.4,.01);Check(n>=NativeBlastSim.MinVoxels,style+" keeps "+NativeBlastSim.MinVoxels+" voxels at the lowest quality ("+n+")");
                if(style==NativeBlastStyle.Implosion||style==NativeBlastStyle.Void)Check(Within(sim.Glow,0,0,0,.4,1e-4,true)>=NativeBlastSim.MinVoxels,style+" minimum front");
                for(int i=0;i<steps;i++)sim.Update(Dt);
                if(Array.IndexOf(ShellStyles,style)>=0)Check(Within(sim.Glow,0,0,0,.4,.005)>=NativeBlastSim.MinVoxels,style+" minimum shell on the radius");
                if(style==NativeBlastStyle.Nova)Check(Within(sim.Glow,0,.95,0,.4,.005)>=NativeBlastSim.MinVoxels,"nova minimum front");
                Check(new NativeBlastSim().Blast(style,0,.95,0,3,0)==0,style+" spawns nothing at quality 0");
            }
        }
        private static void Quality()
        {
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))
            {
                int Count(double q,bool compat,out int lit){var sim=new NativeBlastSim();int n=sim.Blast(style,0,.95,0,3,q,compat);sim.Update(Dt);lit=0;foreach(var f in sim.Flashes)if(f.Intensity>0)lit++;return n;}
                int full=Count(1,false,out int litFull),half=Count(.5,false,out int litHalf),reduced=Count(.3,false,out int litReduced),compatible=Count(1,true,out int litCompat);
                Check(full>half&&half>reduced&&full>compatible,style+" scales with quality ("+full+","+half+","+reduced+", compat "+compatible+")");
                Check(litFull==1&&litHalf==1&&litReduced==0&&litCompat==0,style+" lights only at quality >= .5 without compatibility");
            }
        }
        private static void Cues()
        {
            Check(NativeBlastCues.School(1)==NativeBlastStyle.Fire&&NativeBlastCues.School(2)==NativeBlastStyle.Frost&&NativeBlastCues.School(3)==NativeBlastStyle.Shock&&NativeBlastCues.School(4)==NativeBlastStyle.Implosion&&NativeBlastCues.School(0)==NativeBlastStyle.Fire,"school styles");
            Check(NativeBlastCues.Classify(true,1,3,null)==NativeBlastCues.Cue.Teleport&&NativeBlastCues.Classify(true,1,2,null)==NativeBlastCues.Cue.None&&NativeBlastCues.Classify(true,.15f,3,null)==NativeBlastCues.Cue.None,"beams: only blinks are teleports");
            Check(NativeBlastCues.Classify(false,5,4,"implosion-bomb")==NativeBlastCues.Cue.Vacuum&&NativeBlastCues.Classify(false,4,4,"implosion-bomb")==NativeBlastCues.Cue.Blast,"vacuum");
            Check(NativeBlastCues.Classify(false,.3f,0,"rocket")==NativeBlastCues.Cue.Dud&&NativeBlastCues.Classify(false,3.6f,0,"rocket")==NativeBlastCues.Cue.Blast,"rocket dud");
            Check(NativeBlastCues.Style("frag",0)==NativeBlastStyle.Kinetic&&NativeBlastCues.Style(null,2)==NativeBlastStyle.Frost&&NativeBlastCues.Style("bomb",3)==NativeBlastStyle.Fire&&NativeBlastCues.Style("mine",0)==NativeBlastStyle.Mine,"tag beats school");
            Check(NativeBlastCues.Quality("flame-lance")==.5&&NativeBlastCues.Quality(null)==1&&NativeBlastCues.Colocated(3)=="blink-strike"&&NativeBlastCues.Colocated(1)=="wormhole","quality and teleport landings");
            // The source's blastStyle switch (chip-blasts.ts:36-44), every case.
            var table=new (string tag,NativeBlastStyle style)[]{("thunder",NativeBlastStyle.Shock),("thunderstep",NativeBlastStyle.Shock),("static",NativeBlastStyle.Shock),("storm",NativeBlastStyle.Shock),("lightning-rod",NativeBlastStyle.Shock),("orbital-mark",NativeBlastStyle.Shock),("blink-strike",NativeBlastStyle.Shock),("ball-lightning",NativeBlastStyle.Shock),
                ("implosion",NativeBlastStyle.Implosion),("implosion-bomb",NativeBlastStyle.Implosion),("singularity",NativeBlastStyle.Implosion),("frost",NativeBlastStyle.Frost),("blizzard",NativeBlastStyle.Frost),("geyser-wall",NativeBlastStyle.Frost),("corpseParty",NativeBlastStyle.Toxic),("toxic",NativeBlastStyle.Toxic),("acid-rain",NativeBlastStyle.Toxic),
                ("meteor",NativeBlastStyle.Meteor),("artillery",NativeBlastStyle.Meteor),("meteor-strike",NativeBlastStyle.Meteor),("mine",NativeBlastStyle.Mine),("cluster-mine",NativeBlastStyle.Mine),("mirror-mine",NativeBlastStyle.Mine),("bread-crumbs",NativeBlastStyle.Mine),("proximity-mail",NativeBlastStyle.Mine),
                ("dashBlast",NativeBlastStyle.Kinetic),("kinetic",NativeBlastStyle.Kinetic),("bubble-mine",NativeBlastStyle.Kinetic),("frag",NativeBlastStyle.Kinetic),("tidal-wave",NativeBlastStyle.Kinetic),("quake",NativeBlastStyle.Quake),("aftershock",NativeBlastStyle.Quake),("trampoline",NativeBlastStyle.Quake),
                ("vacuum",NativeBlastStyle.Void),("void",NativeBlastStyle.Void),("black-hole",NativeBlastStyle.Void),("wormhole",NativeBlastStyle.Void),("shackles",NativeBlastStyle.Void),("blockPulse",NativeBlastStyle.Nova),("nova",NativeBlastStyle.Nova),("shrapnel",NativeBlastStyle.Fire),("leet-nuke",NativeBlastStyle.Fire),(null,NativeBlastStyle.Fire)};
            foreach(var (tag,style) in table)Check(NativeBlastSim.StyleOf(tag)==style,"source style of "+(tag??"(none)"));
            Check(NativeBlastSim.TintOf("rocket")==null&&NativeBlastSim.TintOf(null)==null&&NativeBlastSim.TintOf("frag").Value.Cool==0x676b72,"tints");
            Check(NativeBlastSim.Round(2.5)==3&&NativeBlastSim.Round(-2.5)==-2&&NativeBlastSim.Hex(1,1,1)==0xffffff,"JavaScript rounding and three.js getHex");
        }
        private static void Winding()
        {
            var c=NativeBlastSim.CubeCorners;var t=NativeBlastSim.CubeTriangles;Check(c.Length==24&&t.Length==36,"cube topology");var used=new bool[8];
            for(int i=0;i<36;i+=3)
            {
                double V(int n,int axis)=>c[t[i+n]*3+axis];
                double ax=V(1,0)-V(0,0),ay=V(1,1)-V(0,1),az=V(1,2)-V(0,2),bx=V(2,0)-V(0,0),by=V(2,1)-V(0,1),bz=V(2,2)-V(0,2);
                double nx=ay*bz-az*by,ny=az*bx-ax*bz,nz=ax*by-ay*bx,mx=V(0,0)+V(1,0)+V(2,0),my=V(0,1)+V(1,1)+V(2,1),mz=V(0,2)+V(1,2)+V(2,2);
                // Unity front faces are clockwise on screen: the numeric cross product points out of the cube.
                Check(nx*mx+ny*my+nz*mz>0,"triangle "+i/3+" faces outward");for(int n=0;n<3;n++)used[t[i+n]]=true;
            }
            Check(Array.TrueForAll(used,u=>u),"every corner used");
        }
    }
}
#endif
