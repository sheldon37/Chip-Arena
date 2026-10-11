#if ARENA_TESTS
using System;
using System.IO;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Chip blasts: NativeBlastSim against the pinned source's drawn frames (tools/export_blast_fixtures.mjs runs
    // client/chip-blasts.ts with the owner's cuts), plus determinism, front timing, pool capacities, minimum voxels, quality
    // scaling, the effect cues, the renderer's cube winding, and the owner's lightning and area-disc cuts.
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
            Determinism();Front();Capacity();Minimum();Quality();Cues();Frames();Winding();StandIns(root);int lightning=NoBlueLightning(root,json),disc=NoAreaDisc(root,json);
            Console.WriteLine("PASS "+cases+" source chip blast runs ("+frames+" drawn frames: 11 styles x 3 radii, quality/compat, tints, impacts, 20 fields, line, paddle) and blast determinism, front, capacity, minimum-voxel, quality, cue, frame-rule and winding checks, the event-to-effect table (no blast without a source blast), "+lightning+" no-blue/cyan-lightning checks (owner 2026-10-10) and "+disc+" no-area-disc and real-floor checks (owner 2026-10-11)");
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
        private static readonly NativeBlastStyle[] ShellStyles={NativeBlastStyle.Fire,NativeBlastStyle.Mine,NativeBlastStyle.Meteor,NativeBlastStyle.Kinetic,NativeBlastStyle.Shock,NativeBlastStyle.Implosion,NativeBlastStyle.Toxic,NativeBlastStyle.Frost,NativeBlastStyle.Quake};
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
            Check(NativeBlastCues.Classify(true,1,3,null)==NativeBlastCues.Cue.Teleport&&NativeBlastCues.Classify(true,1,2,null)==NativeBlastCues.Cue.None&&NativeBlastCues.Classify(true,.15f,3,null)==NativeBlastCues.Cue.None,"beams: only blinks are teleports");
            Check(NativeBlastCues.Classify(false,5,4,"implosion-bomb")==NativeBlastCues.Cue.Vacuum&&NativeBlastCues.Classify(false,4,4,"implosion-bomb")==NativeBlastCues.Cue.Blast,"vacuum");
            Check(NativeBlastCues.Classify(false,.3f,0,"rocket")==NativeBlastCues.Cue.Dud&&NativeBlastCues.Classify(false,3.6f,0,"rocket")==NativeBlastCues.Cue.Blast,"rocket dud");
            Check(NativeBlastCues.Style("frag")==NativeBlastStyle.Kinetic&&NativeBlastCues.Style(null)==NativeBlastStyle.Fire&&NativeBlastCues.Style("bomb")==NativeBlastStyle.Fire&&NativeBlastCues.Style("mine")==NativeBlastStyle.Mine,"the source message picks the style; none is fire");
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
        private static NativeBlastFrame.Candidate At(double x,double radius,string tag,NativeBlastCues.Cue cue)=>new NativeBlastFrame.Candidate{X=x,Y=1.1,Z=-2,Radius=radius,Tag=tag,Cue=cue};
        private static string Collapsed(params NativeBlastFrame.Candidate[] records)
        {var list=new System.Collections.Generic.List<NativeBlastFrame.Candidate>(records);NativeBlastFrame.Collapse(list);return string.Join(",",list.ConvertAll(c=>c.Cue.ToString()));}
        // NativeBlastFrame: the blast step, one cue per source event, and the Siren's records.
        private static void Frames()
        {
            const NativeBlastCues.Cue Blast=NativeBlastCues.Cue.Blast,Dud=NativeBlastCues.Cue.Dud,Teleport=NativeBlastCues.Cue.Teleport;
            Check(NativeBlastFrame.Step(1/144.0,false)==1/144.0&&NativeBlastFrame.Step(.2,false)==NativeBlastFrame.MaxFrame&&NativeBlastFrame.Step(1/60.0,true)==0&&NativeBlastFrame.Step(-1,false)==0&&NativeBlastFrame.Step(double.NaN,false)==0,"step: the render frame, capped at 0.05 s, zero only while the match is paused");
            // An online match behind the menu keeps recording effects; the blasts keep expiring, so the pools stay small.
            var sim=new NativeBlastSim();int peak=0;
            for(int s=0;s<30;s++){sim.Blast(NativeBlastStyle.Fire,s%10,1.35,0,3);for(int i=0;i<4;i++)sim.Impact(i,1.2,s%7,.6,0x8de9ee,1);for(int f=0;f<144;f++){sim.Update(NativeBlastFrame.Step(1/144.0,false));peak=Math.Max(peak,sim.Solid.Drawn+sim.Glow.Drawn);}}
            Check(peak<1000&&sim.Solid.Capacity==NativeBlastSim.SolidCapacity&&sim.Glow.Capacity==NativeBlastSim.GlowCapacity,"blasts expire while the match runs (peak "+peak+" drawn)");
            // The same source event recorded twice in a tick draws once.
            Check(Collapsed(At(4,1,null,Teleport),At(4,1,null,Teleport),At(4,2.5,"blink-strike",Blast))=="Teleport,None,Blast","Blink Strike: one teleport and its blast");
            Check(Collapsed(At(0,3.5,"ball-lightning",Blast),At(0,.6,"ball-lightning",Blast))=="Blast,None"&&Collapsed(At(0,.6,"ball-lightning",Blast),At(0,3.5,"ball-lightning",Blast))=="None,Blast","ball-lightning wall hit: one burst");
            Check(Collapsed(At(0,3.6,"rocket",Blast),At(0,.3,"rocket",Dud))=="Blast,None"&&Collapsed(At(0,.3,"rocket",Dud))=="Dud","armed rocket on a summon: no extra dud; an unarmed rocket keeps it");
            Check(Collapsed(At(0,2,"frag",Blast),At(0,2,"frag",Blast))=="Blast,None","equal records: the first wins");
            Check(Collapsed(At(0,3,null,Blast),At(0,1.8,null,Blast))=="Blast,Blast","untagged chip blasts may share a spot");
            Check(Collapsed(At(0,3,"frag",Blast),At(0,.6,"flame-lance",Blast))=="Blast,Blast"&&Collapsed(At(0,3,"frag",Blast),At(1,.6,"frag",Blast))=="Blast,Blast","other tags and other spots are kept");
            Check(Collapsed(At(0,1,null,Teleport),At(3,1,null,Teleport))=="Teleport,Teleport","separate landings are kept");
            // The Siren's own records draw nothing (siren/fx.ts draws its attacks from the events; the first port's halos are gone).
            Check(NativeBlastCues.Classify(false,1,4,null,true)==NativeBlastCues.Cue.None&&NativeBlastCues.Classify(false,5,4,null,true)==NativeBlastCues.Cue.None&&NativeBlastCues.Classify(true,1,3,null,true)==Teleport&&NativeBlastCues.Classify(false,1,4,null,false,true)==Blast&&NativeBlastCues.Classify(false,1,4,null)==NativeBlastCues.Cue.None,"siren records draw neither halos nor blasts");
            var sites=new System.Collections.Generic.List<NativeBlastFrame.SirenSite>{new NativeBlastFrame.SirenSite{X=0,Y=1.5,Z=0,Reach=.6,Below=true},new NativeBlastFrame.SirenSite{X=10,Y=2,Z=0,Reach=1.2}};
            Check(NativeBlastFrame.AtSiren(sites,.2,.03,.1)&&!NativeBlastFrame.AtSiren(sites,.2,1.4,.1)&&!NativeBlastFrame.AtSiren(sites,1,.03,0),"slam: on the ground under the Siren, not on its body or beside it");
            Check(NativeBlastFrame.AtSiren(sites,10.5,2.3,0)&&!NativeBlastFrame.AtSiren(sites,12,2,0)&&!NativeBlastFrame.AtSiren(new System.Collections.Generic.List<NativeBlastFrame.SirenSite>(),0,0,0),"siren shots and fields");
        }
        // Owner playtest 2026-10-10: "explosion effects seem to play randomly (one of which is killing Siren minions with guns)".
        // Event -> effect fixture: what each Unity effect record (with the Explosion event the match emits beside it, or none)
        // draws, against what event-vfx.ts blastEvent/event draws for the same source event. cue None: no blast at all.
        private struct Record{public string What,Tag,Detail;public double Radius;public int Element;public bool Weapon;public NativeBlastCues.Cue Cue;public NativeBlastStyle Style;public double Quality;}
        private static Record R(string what,double radius,int element,string tag,string detail,NativeBlastCues.Cue cue,NativeBlastStyle style=NativeBlastStyle.Fire,double quality=1,bool weapon=false)
            =>new Record{What=what,Radius=radius,Element=element,Tag=tag,Detail=detail,Weapon=weapon,Cue=cue,Style=style,Quality=quality};
        private static void StandIns(string root)
        {
            const NativeBlastCues.Cue None=NativeBlastCues.Cue.None,Blast=NativeBlastCues.Cue.Blast,Dud=NativeBlastCues.Cue.Dud,Vacuum=NativeBlastCues.Cue.Vacuum;
            var table=new[]{
                // First-port records with no source blast (no event, no tag): a horde gib on a gun kill (pve/gore.ts draws gore
                // only), a summon (`summon` is a green impact), a PvE revive (no world effect), a pong rally (`ricochet`). Their
                // school no longer picks a look, so element 0 or 1 is no fireball.
                R("gun kill of a Siren add: severed limb",.35,1,null,null,None),R("severed limb, school 0",.35,0,null,null,None),
                R("summon",.6,4,null,null,None),R("PvE revive",1,2,null,null,None),R("pong rally bounce",.3,1,null,null,None),R("unknown school",2,0,null,null,None),
                // Explosion events (simulation.ts explode): the message picks the style, a bare weapon blast is fire at .55.
                R("static discharge on a Siren add (ChipBlast 'chip', 3 m): source arc",3,0,null,"chip",Blast,NativeBlastStyle.Shock,.6),
                R("static discharge on a pilot",3,1,null,"static",Blast,NativeBlastStyle.Shock,.6),
                R("corpse party ('chip', 3.5 m)",3.5,4,null,"chip",Blast,NativeBlastStyle.Toxic),
                R("block pulse",3,0,null,"block-pulse",Blast,NativeBlastStyle.Kinetic),R("supernova block pulse",3,2,null,"supernova",Blast,NativeBlastStyle.Nova),
                R("fire weapon blast (no message)",1.5,1,null,"fire",Blast,NativeBlastStyle.Fire,.55,true),R("guest event without a Detail",2,3,null,"",Blast,NativeBlastStyle.Fire,.55),
                R("dash blast",3,0,null,"dash-blast",Blast,NativeBlastStyle.Kinetic),R("trampoline",3,0,null,"trampoline",Blast,NativeBlastStyle.Quake),R("wormhole pulse",3,0,null,"wormhole",Blast,NativeBlastStyle.Void),
                R("shrapnel",1.8,3,null,"shrapnel",Blast,NativeBlastStyle.Fire),R("L33t block (root)",6,0,null,"leet-block",Blast,NativeBlastStyle.Fire),R("L33t nuke",11,0,null,"leet-nuke",Blast,NativeBlastStyle.Fire),
                R("meteor strike",3,1,null,"meteor-strike",Blast,NativeBlastStyle.Meteor),R("frag",3,0,null,"frag",Blast,NativeBlastStyle.Kinetic),R("cluster mine field",2.3,2,null,"cluster-mine",Blast,NativeBlastStyle.Mine),
                R("vacuum",5,4,null,"",Vacuum),
                // Ability hit blasts that are visual only in the source (no event; the tag comes from the ability shot).
                R("flame lance hit",.6,1,"flame-lance",null,Blast,NativeBlastStyle.Fire,.5),R("shackles hit",.6,4,"shackles",null,Blast,NativeBlastStyle.Void),R("unarmed rocket",.3,0,"rocket",null,Dud)};
            foreach(var r in table)
            {
                bool explosion=r.Detail!=null;string tag=r.Tag;double q=NativeBlastCues.Quality(tag);
                if(explosion)tag=NativeBlastCues.ExplosionTag(r.Detail,r.Radius,r.Weapon,out q);
                var cue=NativeBlastCues.Classify(false,r.Radius,r.Element,tag,false,explosion);
                Check(cue==r.Cue,r.What+": cue "+cue+" vs "+r.Cue);if(cue!=Blast)continue;
                Check(NativeBlastCues.Style(tag)==r.Style&&Math.Abs(q-r.Quality)<1e-9,r.What+": "+NativeBlastCues.Style(tag)+" at "+q+" vs "+r.Style+" at "+r.Quality);
            }
            Check(NativeBlastCues.Static("chip",3)&&NativeBlastCues.Static("static",2)&&!NativeBlastCues.Static("chip",3.5)&&!NativeBlastCues.Static(null,3),"static discharge");
            // The stand-in records are gone from the match (presentation feed only) and the presentation draws the source's own looks.
            string Read(string file)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",file));
            Check(!Read("NativePveHorde.cs").Contains("Fx(")&&!Read("NativePve.cs").Contains("Fx(")&&!Read("NativeProjectiles.cs").Contains("Fx("),"no gib, revive or pong ability effect");
            Check(!Read("NativeSummons.cs").Contains("Fx(pos")&&Read("NativeAbilities.cs").Contains("if(d.id==\"flame-lance\")Fx(at,.6f,element);"),"no summon effect; of the surface hits only Flame Lance records one");
            string blasts=Read("NativeBlastPresentation.cs");
            // Summons, pong bounces and rallies plan their source impacts per event (NativeImpactCues; Tests/ImpactCueChecks.cs).
            Check(blasts.Contains("int count=NativeImpactCues.Plan(cue,plan);Run(count,quality);")&&blasts.Contains("ExplosionAt(arena,fx,out string detail)"),"summon and pong impacts per event, records keyed to their Explosion events");
            Check(!Read("NativeWorldEffects.cs").Contains("MinionDeaths")&&Read("NativeSummonPresentation.cs").Contains("if(summon&&age>="),"a horde add or summon death draws no world puff; only a summon breaks up, in SourceMinionDeath");
        }
        // Owner, Windows test build 2026-10-10: "Remove that blue/cyan lightning that shows when an area of effect is happening."
        // A blue or cyan shock blast (Thunder Step, Static, Blink Strike, the storm style) never draws the sky bolt, its side
        // sparks or the ground arcs, and Storm In A Jar never draws strikes. Ball Lightning (violet) and the Orbital Mark (red)
        // keep theirs. Everything else those areas draw still draws. Rules, behaviour and source-text scans; returns the count.
        private static int NoBlueLightning(string root,JsonElement json)
        {
            int n=0;void C(bool ok,string what){Check(ok,"no blue/cyan lightning: "+what);n++;}
            var owner=json.GetProperty("owner");C(owner.GetProperty("lightning").GetString()=="2026-10-10"&&owner.GetProperty("cuts").GetInt32()==15,"the parity frames come from the source with the owner's cuts");
            foreach(int hex in new[]{0xbfe6ff,0x6f8dff,0x8fd6ff,0x9ffcff,0x1fc4e6,0x8fb8ff,0x9fd2ff,0x55dcff,0x80dfff,0x32b8ff})C(NativeBlastSim.BlueCyan(hex),hex.ToString("x6")+" is blue/cyan");
            foreach(int hex in new[]{0xffffff,0xf2ffff,0x9a9a9a,0xd7a6ff,0x8a4cff,0xc2a3ff,0xc4b4ff,0xb66cff,0xff9a8a,0xe8323a,0xff304f,0x55ff88,0xffc23a})C(!NativeBlastSim.BlueCyan(hex),hex.ToString("x6")+" is not blue/cyan");
            foreach(var tag in new[]{"thunder","thunderstep","static","storm","lightning-rod","blink-strike"})C(NativeBlastSim.StyleOf(tag)==NativeBlastStyle.Shock&&!NativeBlastSim.Lightning(NativeBlastStyle.Shock,NativeBlastSim.TintOf(tag)),tag+" draws no lightning");
            foreach(var tag in new[]{"ball-lightning","orbital-mark"})C(NativeBlastSim.Lightning(NativeBlastSim.StyleOf(tag),NativeBlastSim.TintOf(tag)),tag+" keeps its violet or red lightning");
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))if(style!=NativeBlastStyle.Shock)C(!NativeBlastSim.Lightning(style,null),style+" has no lightning");
            // Behaviour. On the first drawn frame the sky bolt is already 9 m up and nothing else in a shock blast is above
            // 2.5 m; a blue/cyan blast spawns exactly the violet one's voxels minus the bolt, its side sparks and the arcs.
            double Top(NativeBlastPool pool){double top=-1e9;for(int i=0;i<pool.Drawn;i++)top=Math.Max(top,pool.Output[i*NativeBlastPool.Stride+1]);return top;}
            foreach(double R in new[]{1.2,2.4,3,6})foreach(double q in new[]{1,NativeBlastCues.ArcQuality,.3})
            {
                int K(double b)=>(int)Math.Max(1,NativeBlastSim.Round(b*q*Math.Min(1.6,.55+R*.18)));
                int Spawn(string tag,out double top,out bool lit){var sim=new NativeBlastSim();int count=sim.Blast(NativeBlastStyle.Shock,0,.95,0,R,q,false,NativeBlastSim.TintOf(tag));sim.Update(Dt);top=Top(sim.Glow);lit=false;foreach(var f in sim.Flashes)lit|=f.Intensity>0;return count;}
                int violet=Spawn("ball-lightning",out double boltTop,out _),steps=K(22),sides=0;for(int i=0;i<steps;i++)if(i%4==2)sides++;
                C(boltTop>8,"the probe sees Ball Lightning's sky bolt r"+R+" q"+q);
                foreach(var tag in new[]{null,"thunderstep","static","blink-strike"})
                {
                    string what=(tag??"shock")+" r"+R+" q"+q;int count=Spawn(tag,out double top,out bool lit);
                    C(top<2.5,what+": no sky bolt (highest voxel "+top+" m)");
                    C(count==violet-steps-sides-K(5)*6,what+": only the bolt, its side sparks and the ground arcs are gone ("+count+" of "+violet+" voxels)");
                    C(count>=NativeBlastSim.MinVoxels&&lit==(q>=.5),what+": core, shock front, sparks and light still draw");
                }
            }
            {
                // Blink Strike's landing still reads its gameplay radius with the shock front.
                var sim=new NativeBlastSim();sim.Blast(NativeBlastStyle.Shock,0,.95,0,2.4,1,false,NativeBlastSim.TintOf("blink-strike"));for(int i=0;i<(int)NativeBlastSim.Round(NativeBlastSim.FrontSeconds/Dt);i++)sim.Update(Dt);
                C(Within(sim.Glow,0,0,0,2.4,.005)>=NativeBlastSim.MinVoxels,"Blink Strike's shock front still reaches its 2.4 m radius");
            }
            {
                // Storm In A Jar: after its arrival burst, the glow never rises above the telegraph motes (the source's bolts start
                // at 3.6 m, the ground flash is .6 m wide); its clouds and motes still draw.
                var sim=new NativeBlastSim();var f=new NativeBlastField{Id=9,Kind="storm",Fx="lightning-rod",X=0,Y=.95,Z=0,Radius=4,Born=0,Until=3};double top=-1e9,edge=0;int clouds=0,motes=0;
                for(int i=0;i<180;i++)
                {
                    double t=i*Dt;sim.Field(f,t,1);sim.Update(Dt);if(t<1)continue;
                    for(int k=0;k<sim.Glow.Drawn;k++){int w=k*NativeBlastPool.Stride;top=Math.Max(top,sim.Glow.Output[w+1]);edge=Math.Max(edge,Math.Max(sim.Glow.Output[w+3],sim.Glow.Output[w+4]));motes++;}
                    for(int k=0;k<sim.Solid.Drawn;k++)if(sim.Solid.Output[k*NativeBlastPool.Stride+1]>3.3)clouds++;
                }
                C(motes>0&&top<1.25&&edge<.3,"Storm In A Jar draws no strikes (glow up to "+top+" m, largest "+edge+" m)");
                C(clouds>0,"Storm In A Jar keeps its clouds");
            }
            // Source text: the bolt and arcs exist only behind Lightning, the storm branch spawns no glow, no other file draws
            // the source's storm or sky bolt, and the fixture exporter applies the same cut and colour band.
            string Text(string file)=>File.ReadAllText(Path.Combine(root,file));
            int Count(string text,string what){int c=0,i=0;while((i=text.IndexOf(what,i,StringComparison.Ordinal))>=0){c++;i+=what.Length;}return c;}
            string Between(string text,string from,string to){int a=text.IndexOf(from,StringComparison.Ordinal);Check(a>=0,"no blue/cyan lightning: the source has "+from);int b=text.IndexOf(to,a+from.Length,StringComparison.Ordinal);return b<0?text.Substring(a):text.Substring(a,b-a);}
            string blast=Text("Assets/ChipArena/Core/NativeBlastSim.cs"),shock=Between(blast,"case NativeBlastStyle.Shock:","case NativeBlastStyle.Implosion:"),storm=Between(blast,"if(kind==\"storm\")","else if(kind==");
            C(shock.Contains("bool lightning=Lightning(style,tint);")&&shock.Contains("if(lightning)SkyBolt(")&&shock.Contains("if(lightning)GroundArcs(")&&!shock.Contains("Glow.Spawn(")&&!shock.Contains("Solid.Spawn("),"the shock case draws its bolt and arcs only through the gated SkyBolt and GroundArcs");
            C(Count(blast,"SkyBolt(")==2&&Count(blast,"GroundArcs(")==2,"SkyBolt and GroundArcs have no other caller");
            C(blast.Contains("=>style==NativeBlastStyle.Shock&&!BlueCyan(Colors(style,tint).Hot);"),"Lightning excludes every blue/cyan look");
            C(!storm.Contains("Glow.Spawn(")&&!storm.Contains("top=")&&storm.Contains("Solid.Spawn("),"the storm field branch spawns only clouds");
            var offenders=new System.Collections.Generic.List<string>();
            foreach(var dir in new[]{"Assets/ChipArena/Core","Assets/ChipArena/Runtime"})foreach(var file in Directory.GetFiles(Path.Combine(root,dir),"*.cs"))
            {string text=File.ReadAllText(file),name=Path.GetFileName(file);if(text.Contains("ground+3.6")||name!="NativeBlastSim.cs"&&text.Contains("top=ground+9"))offenders.Add(name);}
            C(offenders.Count==0,"no other file draws the source's storm strikes or sky bolt ("+string.Join(", ",offenders)+")");
            string exporter=Text("tools/export_blast_fixtures.mjs");
            C(exporter.Contains("OWNER_CUTS")&&exporter.Contains("function __ownerLightning(pal)")&&exporter.Contains("hue>=165&&hue<=250")&&NativeBlastSim.BlueCyanFrom==165&&NativeBlastSim.BlueCyanTo==250,"the fixture exporter applies the same cut and colour band");
            // The lines left on area effects are violet: the teleport (Blink Strike, wormhole) and Ball Lightning's arcs.
            C(!NativeBlastSim.BlueCyan(NativeBlastCues.TeleportColor)&&!NativeBlastSim.BlueCyan(NativeImpactCues.Lit)&&!NativeBlastSim.BlueCyan(NativeImpactCues.ArcColor),"teleport and arc lines are violet, not blue/cyan");
            return n;
        }
        // Owner 2026-10-11: "Can we remove that 'area indicator' for impact effects? If it hits a wall or something, it kind of
        // appears like a floating pancake." No blast or impact slides a flat disc of voxels over the floor any more; its voxels
        // went to the shell, so every blast spawns as many voxels as the source's with its disc (and the lightning cut). Given the real floor
        // (NativeBlastPresentation raycasts it), a blast up to NearGround above it keeps the source's dome, and a wall, ceiling or
        // airburst blast is a sphere around its point: nothing starts under the floor or sinks into it, pieces land on the real
        // floor, and nothing carries the source's assumed floor (y - .95). The same holds for field arrivals and the Orbital Mark lock,
        // and a field high above its floor draws no ground glow in mid-air. Rules, behaviour and source-text scans; returns the count.
        private static readonly NativeBlastStyle[] Falling={NativeBlastStyle.Fire,NativeBlastStyle.Mine,NativeBlastStyle.Meteor,NativeBlastStyle.Kinetic,NativeBlastStyle.Toxic,NativeBlastStyle.Quake};
        private static int NoAreaDisc(string root,JsonElement json)
        {
            int n=0;void C(bool ok,string what){Check(ok,"no area disc: "+what);n++;}
            var owner=json.GetProperty("owner");C(owner.GetProperty("disc").GetString()=="2026-10-11"&&owner.GetProperty("cuts").GetInt32()==15,"the parity frames come from the source with the disc cut");
            // A disc voxel slides flat (no vertical speed, no gravity) away from the centre: below a blast's centre, or outliving an
            // impact's front (the source's sweep lasted 1.1 to 2.1 fronts). Every spawned voxel counts, delayed ones included.
            int Flat(NativeBlastSim sim,double below,double life)
            {int d=0;foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Count;i++){var p=pool.At(i);var m=pool.Motion(i);if(m.vy==0&&m.gravity==0&&(m.vx!=0||m.vz!=0)&&(p.y<below||p.life>life))d++;}return d;}
            var spots=new (double y,double? floor)[]{(.95,null),(.95,0),(.03,0),(1.2,0),(3,0),(5,NativeBlastSim.NoFloor)};
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))foreach(double R in new[]{.4,1.2,3,6})foreach(double q in new[]{1,.3})foreach(bool compat in new[]{false,true})foreach(var (y,floor) in spots)
            {
                var sim=new NativeBlastSim();int count=sim.Blast(style,1,y,-2,R,q,compat,null,floor);double cy=Math.Max((floor??y-.95)+.35,y);
                C(count>=NativeBlastSim.MinVoxels&&Flat(sim,cy-.2,1e9)==0,style+" r"+R+" q"+q+(compat?" compat":"")+" at "+y+" m: no disc voxels");
            }
            foreach(double R in new[]{.3,.6,1.2,1.5,2.4})foreach(bool ground in new[]{true,false})foreach(var (y,floor) in spots)
            {var sim=new NativeBlastSim();sim.Impact(1,y,-2,R,0xffaa64,1,false,ground,null,floor);C(sim.Glow.Count>0&&Flat(sim,-1e9,NativeBlastSim.FrontSeconds+.005)==0,(ground?"ground ":"")+"impact r"+R+" at "+y+" m: no disc voxels");}
            // The disc's voxels are re-spent: spawn counts equal the source's with its disc and the lightning cut (chip-blasts.ts at
            // 05950ea with OWNER_CUTS only; r 1.2, 3, 6 at quality 1 and r 3 at .3; impacts r .6, 1.2, 2 on and off the ground).
            var budget=new (NativeBlastStyle style,int[] counts)[]{(NativeBlastStyle.Fire,new[]{137,194,285,68}),(NativeBlastStyle.Shock,new[]{85,119,177,45}),(NativeBlastStyle.Implosion,new[]{87,124,183,45}),(NativeBlastStyle.Frost,new[]{62,87,128,32}),
                (NativeBlastStyle.Toxic,new[]{89,128,186,51}),(NativeBlastStyle.Void,new[]{39,55,80,19}),(NativeBlastStyle.Nova,new[]{98,140,205,47}),(NativeBlastStyle.Kinetic,new[]{114,160,234,61}),(NativeBlastStyle.Quake,new[]{54,76,112,30}),(NativeBlastStyle.Meteor,new[]{145,205,301,71}),(NativeBlastStyle.Mine,new[]{137,194,285,68})};
            foreach(var (style,counts) in budget){var runs=new[]{(1.2,1.0),(3.0,1.0),(6.0,1.0),(3.0,.3)};for(int i=0;i<runs.Length;i++){int got=new NativeBlastSim().Blast(style,0,.95,0,runs[i].Item1,runs[i].Item2);C(got==counts[i],style+" r"+runs[i].Item1+" q"+runs[i].Item2+" spawns the source's "+counts[i]+" voxels ("+got+")");}}
            foreach(var (ground,R,count) in new[]{(true,.6,42),(true,1.2,49),(true,2.0,58),(false,.6,27),(false,1.2,32),(false,2.0,38)})
            {var sim=new NativeBlastSim();sim.Impact(0,.95,0,R,0xffaa64,1,false,ground);C(sim.Solid.Spawned+sim.Glow.Spawned==count,(ground?"ground ":"")+"impact r"+R+" spawns the source's "+count+" voxels");}
            // Without a floor the source's y - .95 applies, voxel for voxel.
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))
            {
                var a=new NativeBlastSim();var b=new NativeBlastSim();a.Blast(style,1,1.3,-2,3);b.Blast(style,1,1.3,-2,3,1,false,null,1.3-.95);a.Impact(0,2,1,1.2,0x93db67,1,false,true);b.Impact(0,2,1,1.2,0x93db67,1,false,true,null,2-.95);
                for(int i=0;i<30;i++){a.Update(Dt);b.Update(Dt);}C(Same(a,b),style+": the default floor is the source's y - .95");
            }
            // A blast on a wall 3 m up and an airburst 5 m up, over a floor at 0: nothing starts under the floor, no voxel carries a
            // floor other than the real one (or none), nothing sinks under it, and from 3 m the debris land on it (Frost's drifting
            // shards melt in the air, as in the source).
            var looks=new System.Collections.Generic.List<(NativeBlastStyle style,string tag)>();foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))looks.Add((style,null));looks.Add((NativeBlastStyle.Shock,"ball-lightning"));
            bool Floors(NativeBlastSim sim,double floor){foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Count;i++){var p=pool.At(i);var m=pool.Motion(i);if(p.y<floor-1e-4||m.floor>-1e8&&(m.floor<floor-1e-6||m.floor>floor+.06))return false;}return true;}
            foreach(var (style,tag) in looks)foreach(double R in new[]{1.2,3,3.6,6})foreach(double y in new[]{3.0,5})
            {
                var sim=new NativeBlastSim();sim.Blast(style,1,y,-2,R,1,false,NativeBlastSim.TintOf(tag),0);string what=(tag??style.ToString())+" r"+R+" at "+y+" m over a floor at 0";
                C(Floors(sim,0),what+": nothing starts under the floor or rests on the assumed floor at "+(y-.95)+" m");
                double low=1e9;bool landed=false;
                for(int f=0;f<120;f++){sim.Update(Dt);foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Drawn;i++){double at=pool.Output[i*NativeBlastPool.Stride+1];low=Math.Min(low,at);if(pool==sim.Solid&&at<.25)landed=true;}}
                C(low>=-1e-4,what+": nothing sinks under the floor (lowest "+low+" m)");
                if(y<4&&Array.IndexOf(Falling,style)>=0)C(landed,what+": the debris land on the floor");
            }
            foreach(double R in new[]{.6,1.15,1.5})foreach(bool ground in new[]{false,true})
            {
                var sim=new NativeBlastSim();sim.Impact(1,2.2,-2,R,0xffaa64,1,false,ground,null,0);string what=(ground?"ground ":"")+"impact r"+R+" on a wall 2.2 m up";
                C(Floors(sim,0),what+": nothing starts under the floor or rests on the assumed floor at 1.25 m");
                double low=1e9;for(int f=0;f<90;f++){sim.Update(Dt);foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Drawn;i++)low=Math.Min(low,pool.Output[i*NativeBlastPool.Stride+1]);}
                C(low>=-1e-4,what+": nothing sinks under the floor");
            }
            // The shell: a dome on the floor up to NearGround above it, a full sphere around the blast above that.
            int steps=(int)NativeBlastSim.Round(NativeBlastSim.FrontSeconds/Dt);
            int Lower(NativeBlastPool pool,double cx,double cy,double cz,double radius){int d=0;for(int i=0;i<pool.Count;i++){var p=pool.At(i);if(p.age<0||p.y>=cy-.1*radius)continue;double dx=p.x-cx,dy=p.y-cy,dz=p.z-cz;if(Math.Abs(Math.Sqrt(dx*dx+dy*dy+dz*dz)-radius)<=.005)d++;}return d;}
            foreach(var style in ShellStyles)foreach(var (y,R,dome) in new[]{(.95,1.2,true),(.95,3.0,true),(NativeBlastSim.NearGround,1.2,true),(NativeBlastSim.NearGround+.05,1.2,false),(3.0,1.2,false),(3.0,3.0,false)})
            {
                var sim=new NativeBlastSim();sim.Blast(style,1,y,-2,R,1,false,null,0);for(int i=0;i<steps;i++)sim.Update(Dt);string what=style+" r"+R+" at "+y+" m";
                if(dome)C(Within(sim.Glow,1,0,-2,R,.005)>=NativeBlastSim.MinVoxels,what+": a dome on the floor");
                else C(Within(sim.Glow,1,y,-2,R,.005)>=NativeBlastSim.MinVoxels&&Lower(sim.Glow,1,y,-2,R)>0&&Within(sim.Glow,1,0,-2,R,.005)<NativeBlastSim.MinVoxels/2,what+": a sphere around the blast, not a dome");
            }
            foreach(var (y,dome) in new[]{(.95,true),(NativeBlastSim.NearGround+.05,false),(3.0,false)})
            {
                var sim=new NativeBlastSim();sim.Impact(1,y,-2,1.2,0x93db67,1,false,true,null,0);for(int i=0;i<steps;i++)sim.Update(Dt);
                C(dome?Within(sim.Glow,1,.12,-2,1.2,.005)>=8:Within(sim.Glow,1,y,-2,1.2,.005)>=8&&Lower(sim.Glow,1,y,-2,1.2)>0,"ground impact at "+y+" m: "+(dome?"a dome on the floor":"a sphere around the point"));
            }
            // Fields placed on a wall hit (chip fields at the impact point, an Orbital Mark on a wall or an airborne target): given
            // the view's floor, the arrival and lock impacts are a sphere around the field whose pieces carry the real floor (or
            // none), nothing starts under it, and nothing rests on the source's floor .95 m under the field. Near the floor the
            // arrival keeps its dome; without a floor (or given the source's own) the source runs voxel for voxel.
            bool Above(NativeBlastSim sim,double floor,out double lowest){lowest=1e9;foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Count;i++){var p=pool.At(i);var m=pool.Motion(i);lowest=Math.Min(lowest,p.y);if(p.y<floor-1e-4||m.floor>-1e8&&(m.floor<floor-1e-6||m.floor>floor+.06))return false;}return true;}
            var arrivals=new (string kind,string fx)[]{("acid","acid-rain"),("fire","wildfire"),("bomb","sticky-bomb"),("frost","blizzard"),("heal","med-garden"),("bomb","bubble-mine"),("bomb","delayed-gratification"),("bomb","aftershock"),("storm","lightning-rod"),("time","time-warp"),("repulsion","repulsion-pad"),("black-hole","black-hole"),("mine","proximity-mail")};
            foreach(var (kind,fx) in arrivals)foreach(double R in new[]{2.5,3.5,5})foreach(double y in new[]{3.0,5})
            {
                var sim=new NativeBlastSim();var f=new NativeBlastField{Id=3,Kind=kind,Fx=fx,X=1,Y=y,Z=-2,Radius=R,Born=0,Until=4,Floor=0};sim.Field(f,0,1);string what=fx+" r"+R+" field on a wall "+y+" m up";
                bool above=Above(sim,0,out double lowest);C(sim.Glow.Count>0&&above&&lowest>y-.2,what+": its arrival is a sphere around the field (lowest spawn "+lowest+" m), nothing on the assumed floor at "+(y-.95)+" m");
                double low=1e9;for(int i=0;i<60;i++){sim.Update(Dt);foreach(var pool in new[]{sim.Solid,sim.Glow})for(int j=0;j<pool.Drawn;j++)low=Math.Min(low,pool.Output[j*NativeBlastPool.Stride+1]);}
                C(low>=-1e-4,what+": nothing sinks under the floor");
            }
            foreach(double y in new[]{2.2,3.0,5})
            {
                var mark=new NativeBlastField{Id=9,Kind="bomb",Fx="orbital-mark",X=1,Y=y,Z=-2,Radius=3.5,Born=0,Until=2.5,LockAt=1.9,Floor=0};var sim=new NativeBlastSim();sim.Field(mark,1.9,1);
                bool above=Above(sim,0,out double lowest);C(sim.Glow.Count>0&&above&&lowest>y-.2,"Orbital Mark locking "+y+" m up: a sphere around the mark (lowest spawn "+lowest+" m), nothing on the assumed floor at "+(y-.95)+" m");
            }
            {
                var sim=new NativeBlastSim();sim.Field(new NativeBlastField{Id=4,Kind="acid",Fx="acid-rain",X=1,Y=.95,Z=-2,Radius=3,Born=0,Until=4,Floor=0},0,1);for(int i=0;i<steps;i++)sim.Update(Dt);
                C(Within(sim.Glow,1,.12,-2,3,.005)>=8,"a field at a pilot's centre over the floor keeps the arrival dome on the floor");
            }
            foreach(var (kind,fx,y) in new[]{("acid","acid-rain",3.0),("bomb","sticky-bomb",.95),("fire","wildfire",.02),("bomb","orbital-mark",3.0)})
            {
                NativeBlastField F(double? floor)=>new NativeBlastField{Id=5,Kind=kind,Fx=fx,X=1,Y=y,Z=-2,Radius=3,Born=0,Until=2.5,LockAt=1.9,Floor=floor};
                var a=new NativeBlastSim();var b=new NativeBlastSim();
                for(int i=0;i<150;i++){double t=i*Dt;a.Field(F(null),t,1);b.Field(F(Math.Max(.03,y-.95)),t,1);a.Update(Dt);b.Update(Dt);}
                C(Same(a,b),fx+" at "+y+" m: no floor is the source's, max(.03, y - .95)");
            }
            // The field's flat ground glow (NativeFieldRules.GlowShown): drawn as the source on the ground, gone in mid-air.
            C(NativeFieldRules.GlowShown(.95,0)==1&&NativeFieldRules.GlowShown(.02,0)==1&&NativeFieldRules.GlowShown(NativeBlastSim.NearGround,0)==1&&NativeFieldRules.GlowShown(3,null)==1,"a field on the ground (or with no floor known) draws the source's glow");
            C(NativeFieldRules.GlowShown(3,0)==0&&NativeFieldRules.GlowShown(2.2,0)==0&&NativeFieldRules.GlowShown(5,NativeBlastSim.NoFloor)==0&&NativeFieldRules.GlowShown(2.95,2)==1,"a field on a wall, over a pit or high in the air draws no glow disc; a raised floor counts");
            {double last=1;bool down=true;for(double y=0;y<3;y+=.05){double g=NativeFieldRules.GlowShown(y,0);down&=g<=last+1e-12&&g>=0&&g<=1;last=g;}C(down&&Math.Abs(NativeFieldRules.GlowShown(NativeBlastSim.NearGround+NativeFieldRules.GlowFade/2,0)-.5)<1e-9,"the glow fades out smoothly with height");}
            // Source text: no disc in the sim, the presentation passes the raycast floor to every blast and impact and every field
            // view, and the fixture exporter applies the same cut.
            string Text(string file)=>File.ReadAllText(Path.Combine(root,file));
            string blast=Text("Assets/ChipArena/Core/NativeBlastSim.cs"),present=Text("Assets/ChipArena/Runtime/NativeBlastPresentation.cs"),exporter=Text("tools/export_blast_fixtures.mjs"),views=Text("Assets/ChipArena/Runtime/NativeFieldPresentation.cs");
            C(!blast.Contains("Ring(")&&!blast.Contains("if(ground){"),"NativeBlastSim draws no ground disc");
            bool all=true;int calls=0;foreach(var line in present.Split('\n'))if(line.Contains("sim.Blast(")||line.Contains("sim.Impact(")){calls++;all&=line.IndexOf("floor",StringComparison.OrdinalIgnoreCase)>=0;}
            C(calls>=6&&all&&present.Contains("plan[i].Floor=FloorUnder(")&&present.Contains("NativeArena.WorldMask")&&present.Contains("h.normal.y>.5f"),"every blast and impact the presentation draws gets the raycast floor ("+calls+" calls)");
            int fieldViews=0;bool floored=true;foreach(var line in views.Split('\n'))if(line.Contains("new NativeBlastField{")){fieldViews++;floored&=line.Contains("Floor=FloorOf(");}
            C(fieldViews==2&&floored&&views.Contains("NativeBlastPresentation.FloorUnder(physics,at,floorHits)")&&views.Contains("NativeFieldRules.GlowShown(v.Y,v.Floor)")&&present.Contains("sim.Field(views[i],now,quality);"),"every field view the emitters and glow read carries the raycast floor ("+fieldViews+" views)");
            C(blast.Contains("Impact(ax,ay,az,R,pal.Core,q,false,true,pal.Cool,f.Floor)")&&blast.Contains("Impact(ax,ay,az,R,pal.Hot,q*.7,compat,true,pal.Cool,f.Floor)"),"the field arrival and Orbital Mark lock impacts take the view's floor");
            C(exporter.Contains("DISC_CUTS")&&exporter.Contains("disc:'2026-10-11'")&&exporter.Contains("for(let i=0,c=opts.ground?n(14)+n(26):n(20);i<c;i++)"),"the fixture exporter applies the same cut");
            return n;
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
