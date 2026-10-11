#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Effects overhaul, base layer (owner 2026-10-11: "The effects seem a bit outdated and there still seems to be cube
    // placeholders for certain impact effects", "that pancake ring effect is still there"). Core gives every pooled blast
    // particle a draw role, a stable seed and its age, velocity, raw size and floor in the draw list; records one shock front
    // per shell, impact and Nova front; fixes light styles' smoke fading to black; and builds the smoke atlas, noise tile and
    // low-poly meshes (rubble, shard, chip bomb, shock shell) and the bomb pose. All cosmetic and deterministic; the source
    // parity frames (BlastChecks) still hold with the two documented owner cuts.
    internal static class VfxChecks
    {
        private const double Dt=1.0/60;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Vfx check failed: "+what);}
        private static void Near(double a,double b,double tolerance,string what){if(!(Math.Abs(a-b)<=tolerance))throw new Exception("Vfx check failed: "+what+": "+a+" != "+b);}
        public static void Run(string root)
        {
            int sites=Sites(root),roles=Roles(),seeds=Seeds(),shocks=Shocks(),smoke=Smoke(root),pool=DrawList(),sparks=Sparks();
            Console.WriteLine("PASS effects base layer: "+sites+" blast spawn sites name a role, "+roles+" pinned role histograms and sets (11 styles, tints, impacts, lines, paddles, 20 fields), "+seeds+" seed and draw-order checks, "+shocks+" shock front checks, "+smoke+" smoke colour checks, "+pool+" draw-list and determinism checks, "+sparks+" burst spark checks");
            int atlas=Atlas(out double ms),meshes=Meshes(),bomb=Bomb();
            Console.WriteLine("PASS effects base layer assets: smoke atlas (512 px, 16 puffs, built in "+ms.ToString("0.0")+" ms on this runtime; not a timed gate), noise tile, "+atlas+" texture checks, "+meshes+" mesh checks (4 rubble chunks, crystal shard, chip bomb, shock shell and dome) and "+bomb+" bomb pose checks");
        }

        // ---- every NativeBlastPool.Spawn call names its role ----
        // The argument text of each pool spawn (Glow.Spawn / Solid.Spawn, also through a sim: x.Glow.Spawn) up to its closing parenthesis.
        private static List<string> SpawnCalls(string text)
        {
            var calls=new List<string>();
            foreach(Match m in Regex.Matches(text,@"\b(Glow|Solid)\.Spawn\("))
            {
                int depth=1,i=m.Index+m.Length;for(;i<text.Length&&depth>0;i++){if(text[i]=='(')depth++;else if(text[i]==')')depth--;}
                calls.Add(text.Substring(m.Index,i-m.Index));
            }
            return calls;
        }
        private static int Sites(string root)
        {
            int n=0,total=0;
            foreach(var dir in new[]{"Assets/ChipArena/Core","Assets/ChipArena/Runtime","Assets/ChipArena/Editor"})foreach(var file in Directory.GetFiles(Path.Combine(root,dir),"*.cs"))
            {
                var calls=SpawnCalls(File.ReadAllText(file));
                foreach(var call in calls){Check(Regex.IsMatch(call,@",role:NativeFxRole\.[A-Z][a-z]+\)$"),Path.GetFileName(file)+": a pool spawn without its role: "+call);n++;}
                if(Path.GetFileName(file)=="NativeBlastSim.cs")total=calls.Count;
            }
            Check(total==42,"NativeBlastSim's 42 spawn sites ("+total+")");
            // The spawn default (no role) is Mote in the glow pool and Debris in the opaque pool.
            var pool=new NativeBlastPool(4,true);pool.Spawn(0,0,0,0,0,0,1,.1,.1,0xffffff,0xffffff);var solid=new NativeBlastPool(4,false);solid.Spawn(0,0,0,0,0,0,1,.1,.1,0xffffff,0xffffff);
            Check(pool.Identity(0).role==NativeFxRole.Mote&&solid.Identity(0).role==NativeFxRole.Debris,"the default roles");
            return n;
        }

        // ---- role histograms ----
        private static string Histogram(NativeBlastSim sim)
        {
            var h=new SortedDictionary<NativeFxRole,int>();
            foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Count;i++){var r=pool.Identity(i).role;h[r]=h.TryGetValue(r,out int c)?c+1:1;}
            var sb=new StringBuilder();foreach(var kv in h)sb.Append(kv.Key).Append(' ').Append(kv.Value).Append(' ');return sb.ToString().TrimEnd();
        }
        // Pinned 2026-10-11: each style at r 3, quality 1, on the default floor, every spawned particle (delayed ones included).
        private static readonly (string what,string roles)[] Pinned={
            ("Fire","Core 20 Fireball 46 Shell 76 Smoke 17 Debris 15 Spark 20"),("Shock","Core 13 Shell 78 Spark 28"),
            ("Implosion","Core 17 Shell 33 Smoke 9 Spark 17 Streak 48"),("Frost","Core 13 Shell 35 Smoke 11 Shard 28"),
            ("Toxic","Core 11 Fireball 22 Shell 64 Debris 11 Cloud 20"),("Void","Dark 11 Streak 44"),("Nova","Core 20 Shell 98 Spark 22"),
            ("Kinetic","Core 11 Shell 88 Smoke 7 Debris 39 Spark 15"),("Quake","Shell 33 Smoke 15 Rock 28"),
            ("Meteor","Core 20 Fireball 46 Shell 76 Smoke 17 Debris 15 Spark 20 Ember 11"),("Mine","Core 20 Fireball 46 Shell 76 Smoke 17 Debris 15 Spark 20"),
            ("ball-lightning",PinnedBall),("impact air",PinnedImpactAir),("impact ground",PinnedImpactGround),("line",PinnedLine),("paddle",PinnedPaddle)};
        private const string PinnedBall="Core 13 Shell 78 Spark 34 Bolt 54",PinnedImpactAir="Mote 5 Core 5 Shell 18 Debris 6",PinnedImpactGround="Mote 5 Core 4 Shell 34 Debris 6",PinnedLine="Line 24",PinnedPaddle="Mote 16";
        // The set of roles each fixture field spawns over 2.4 s (its arrival impact included), as BlastChecks runs them.
        private static readonly (string kind,string fx,string roles)[] FieldRoles={
            ("black-hole",null,"Mote Core Shell Debris Streak"),("acid",null,"Mote Core Shell Debris Cloud Bubble"),
            ("fire",null,"Mote Core Shell Debris Flame Ember"),("frost",null,"Mote Core Shell Debris Shard Snow"),
            ("time",null,"Mote Core Shell Debris"),("repulsion",null,"Mote Core Shell Debris Streak"),("heal",null,"Mote Core Shell Debris"),
            ("storm",null,"Mote Core Shell Debris Cloud"),("bomb",null,"Mote Core Shell Debris Spark"),("implosion",null,"Mote Streak"),
            ("mine",null,"Mote Core Shell Debris"),("bomb","artillery","Mote Core Smoke"),("bomb","meteor-strike","Mote Core Smoke"),
            ("bomb","orbital-mark","Mote Core Shell Debris Streak"),("bomb","geyser-wall","Mote"),("bomb","thunderstep","Mote Spark"),
            ("bomb","comet","Mote Flame"),("black-hole","singularity","Mote Core Shell Debris Streak"),
            ("mine","proximity-mail","Mote Core Shell Debris"),("bomb","bubble-mine","Mote Core Shell Debris Spark")};
        private static int Roles()
        {
            int n=0;var got=new StringBuilder();bool ok=true;
            foreach(var (what,roles) in Pinned)
            {
                var sim=new NativeBlastSim();
                if(Enum.TryParse<NativeBlastStyle>(what,out var style))sim.Blast(style,0,.95,0,3);
                else if(what=="ball-lightning")sim.Blast(NativeBlastStyle.Shock,0,.95,0,3,1,false,NativeBlastSim.TintOf(what));
                else if(what=="impact air")sim.Impact(0,.95,0,1.5,0xffaa64,1);
                else if(what=="impact ground")sim.Impact(0,.95,0,1.2,0x93db67,1,false,true);
                else if(what=="line")sim.Line(0,.95,0,4,1.5,2,0xffffff,0xb66cff,1,.3,.22,.08);
                else sim.Paddle(0,1,0,1,.2,.5,0xff5269,1);
                string h=Histogram(sim);got.Append("(\"").Append(what).Append("\",\"").Append(h).Append("\"),");if(h!=roles)ok=false;n++;
                // A tint never changes the roles (only colours): the tinted looks match their style.
            }
            foreach(var (style,tag) in new[]{(NativeBlastStyle.Kinetic,"frag"),(NativeBlastStyle.Kinetic,"tidal-wave"),(NativeBlastStyle.Frost,"geyser-wall"),(NativeBlastStyle.Shock,"blink-strike"),(NativeBlastStyle.Fire,"flame-lance")})
            {var a=new NativeBlastSim();a.Blast(style,0,.95,0,3,1,false,NativeBlastSim.TintOf(tag));var b=new NativeBlastSim();b.Blast(style,0,.95,0,3);Check(Histogram(a)==Histogram(b),tag+" has its style's roles");n++;}
            var fields=new StringBuilder();
            foreach(var (kind,fx,roles) in FieldRoles)
            {
                var sim=new NativeBlastSim();var f=new NativeBlastField{Id=7,Kind=kind,Fx=fx,X=0,Y=.95,Z=0,Radius=3,Born=0,Until=2.5,LockAt=1.9};var seen=new SortedSet<NativeFxRole>();
                for(int i=0;i<144;i++){double t=i*Dt;sim.Field(f,t,1);foreach(var pool in new[]{sim.Solid,sim.Glow})for(int k=0;k<pool.Count;k++)seen.Add(pool.Identity(k).role);sim.Update(Dt);}
                string s=string.Join(" ",seen);fields.Append("(\"").Append(kind).Append("\",").Append(fx==null?"null":"\""+fx+"\"").Append(",\"").Append(s).Append("\"),");if(s!=roles)ok=false;n++;
            }
            Check(ok,"role histograms changed; now:\n"+got+"\n"+fields);
            // Every role is used somewhere.
            var all=new HashSet<string>();foreach(var (_,r) in Pinned)foreach(var w in r.Split(' '))all.Add(w);foreach(var (_,_,r) in FieldRoles)foreach(var w in r.Split(' '))all.Add(w);
            foreach(NativeFxRole role in Enum.GetValues(typeof(NativeFxRole))){Check(all.Contains(role.ToString()),role+" is used by a spawn site");n++;}
            return n;
        }

        // ---- seeds and draw order ----
        private static void Euler(double ex,double ey,out double qx,out double qy,out double qz,out double qw)
        {double c1=Math.Cos(ex/2),c2=Math.Cos(ey/2),s1=Math.Sin(ex/2),s2=Math.Sin(ey/2);qx=s1*c2;qy=c1*s2;qz=s1*s2;qw=c1*c2;}
        private static int Seeds()
        {
            int n=0;
            // Particles that die at different times: every survivor keeps the seed it was born with (its x encodes its spawn index).
            var pool=new NativeBlastPool(8,false);var random=new NativeBlastRandom(77);
            for(int i=0;i<40;i++)pool.Spawn(i,0,0,0,0,0,.05+random.Next()*.6,.1,.1,0xffffff,0xffffff,0,0,0,-1e9,1,0,NativeFxRole.Rock);
            Check(pool.Capacity>=40,"the pool grew");for(int i=0;i<40;i++){Check(pool.Identity(i).seed==i,"seeds count spawns");n++;}
            for(int f=0;f<30;f++)
            {
                pool.Update(Dt);
                for(int i=0;i<pool.Count;i++){var id=pool.Identity(i);Check(id.seed==(int)Math.Round(pool.At(i).x)&&id.role==NativeFxRole.Rock,"seed and role survive swap-remove (slot "+i+")");n++;}
                for(int i=0;i<pool.Drawn;i++){int w=i*NativeBlastPool.Stride;Check(pool.Output[w+NativeBlastPool.SeedAt]==(float)Math.Round(pool.Output[w]),"the drawn seed is the particle's");n++;}
            }
            Check(pool.Count<40&&pool.Count>0,"some expired, some live");
            // A spinning voxel's tumble follows its seed: when the voxel before it expires and it is swapped into slot 0, its
            // rotation stays Euler(age*spin*1.3 + seed, age*spin + .7 seed), not the source's slot-index tumble.
            var spin=new NativeBlastPool(4,false);spin.Spawn(0,0,0,0,0,0,.1,.2,.2,0xffffff,0xffffff,0,0,0,-1e9,1,6,NativeFxRole.Debris);spin.Spawn(1,0,0,0,0,0,2,.2,.2,0xffffff,0xffffff,0,0,0,-1e9,1,6,NativeFxRole.Debris);
            double age=0;bool swapped=false;
            for(int f=0;f<20;f++)
            {
                spin.Update(Dt);age+=Dt;int slot=-1;for(int i=0;i<spin.Drawn;i++)if(spin.Output[i*NativeBlastPool.Stride]==1)slot=i;Check(slot>=0,"the survivor draws");
                int w=slot*NativeBlastPool.Stride;float a=0;for(int i=0;i<spin.Count;i++)if(spin.At(i).x==1)a=(float)spin.At(i).age;
                Euler(a*6.0*1.3+1,a*6.0+.7,out double qx,out double qy,out double qz,out double qw);
                double dot=qx*spin.Output[w+6]+qy*spin.Output[w+7]+qz*spin.Output[w+8]+qw*spin.Output[w+9];Check(Math.Abs(dot)>1-1e-6,"rotation follows the seed (frame "+f+", slot "+slot+")");
                if(slot==0&&spin.Count==1)swapped=true;n++;
            }
            Check(swapped,"the survivor was swapped into slot 0");
            // A resting shard (no speed, stretched) keeps its seeded tumble while others expire around it.
            var shards=new NativeBlastPool(8,false);for(int i=0;i<6;i++)shards.Spawn(i,0,0,0,0,0,i==5?3:.04*(i+1),.1,.1,0xffffff,0xffffff,0,0,0,-1e9,3.5,0,NativeFxRole.Shard);
            float[] before=null;for(int f=0;f<20;f++){shards.Update(Dt);int w=-1;for(int i=0;i<shards.Drawn;i++)if(shards.Output[i*NativeBlastPool.Stride]==5)w=i*NativeBlastPool.Stride;var q=new[]{shards.Output[w+6],shards.Output[w+7],shards.Output[w+8],shards.Output[w+9]};if(before!=null){Check(before[0]==q[0]&&before[1]==q[1]&&before[2]==q[2]&&before[3]==q[3],"a resting shard does not snap (frame "+f+")");n++;}before=q;}
            Check(shards.Count==1,"the others expired");
            // Clear restarts the seeds, so a cleared presentation (killcam start and end) replays the same seeds.
            pool.Clear();pool.Spawn(0,0,0,0,0,0,1,.1,.1,0xffffff,0xffffff);Check(pool.Identity(0).seed==0&&pool.Capacity==8,"Clear restarts the seeds and the capacity");n++;
            // Seeds wrap below 2^24 so they stay exact as floats.
            Check(NativeBlastPool.SeedMask==0xffffff&&(float)NativeBlastPool.SeedMask==16777215f,"the seed range is exact in a float");n++;
            return n;
        }

        // ---- shock fronts ----
        private static int Within(NativeBlastPool pool,double cx,double cy,double cz,double radius,double tolerance)
        {int n=0;for(int i=0;i<pool.Count;i++){var p=pool.At(i);if(p.age<0||pool.Identity(i).role!=NativeFxRole.Shell)continue;double dx=p.x-cx,dy=p.y-cy,dz=p.z-cz;if(Math.Abs(Math.Sqrt(dx*dx+dy*dy+dz*dz)-radius)<=tolerance)n++;}return n;}
        private static int Shocks()
        {
            int n=0,steps=(int)NativeBlastSim.Round(NativeBlastSim.FrontSeconds/Dt);
            Check(NativeBlastSim.ShockLife>NativeBlastSim.FrontSeconds&&Math.Abs(NativeBlastSim.ShockLife-.45)<1e-12,"a front lives its travel plus a short fade (.45 s)");
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))foreach(double R in new[]{.4,1.2,3,6})foreach(var (y,floor) in new (double,double?)[]{(.95,null),(3,0),(5,NativeBlastSim.NoFloor)})
            {
                var sim=new NativeBlastSim();sim.Blast(style,1,y,-2,R,1,false,null,floor);string what=style+" r"+R+" at "+y;
                int expected=style==NativeBlastStyle.Void?0:1;Check(sim.Shocks.Count==expected,what+": one shock record per front ("+sim.Shocks.Count+")");n++;if(expected==0)continue;
                var s=sim.Shocks[0];double g=floor??y-.95;bool near=y-g<=NativeBlastSim.NearGround;
                Check(s.Radius==Math.Max(.4,R)&&s.Born==0&&s.Life==NativeBlastSim.ShockLife&&s.Floor==g&&s.Seed==0,what+": record radius, birth, life, floor and seed");
                Check(style==NativeBlastStyle.Nova?(s.Kind==NativeShockKind.Nova&&!s.Dome&&s.Y==Math.Max(g+.35,y)&&s.Start==.25):(s.Kind==NativeShockKind.Blast&&s.Dome==near&&s.Y==(near?g:y)&&s.Start==Math.Min(.25,s.Radius*.2)),what+": record origin and shape");
                Check(s.Style==style&&s.Voxels>=NativeBlastSim.MinVoxels,what+": record style and voxels");
                for(int i=0;i<steps;i++){sim.Update(Dt);if(i<steps-1)Check(s.RadiusAt(sim.Time)<s.Radius,what+": still growing");}
                // The sim clock sums frame steps, so it lands on FrontSeconds to within rounding; the record itself is exact there.
                Check(Math.Abs(sim.Time-NativeBlastSim.FrontSeconds)<1e-12&&s.RadiusAt(s.Born+NativeBlastSim.FrontSeconds)==s.Radius&&s.Progress(s.Born+NativeBlastSim.FrontSeconds)==1&&Math.Abs(s.RadiusAt(sim.Time)-s.Radius)<1e-9&&s.Fade(sim.Time)==1,what+": the record ends exactly on the gameplay radius at FrontSeconds");
                Check(Within(sim.Glow,s.X,s.Y,s.Z,s.Radius,.005)>=NativeBlastSim.MinVoxels,what+": its Shell voxels are on the record's radius");
                Check(sim.Shocks.Count==1,what+": alive through the fade");
                for(int i=0;i<12;i++)sim.Update(Dt);Check(sim.Shocks.Count==0,what+": expired after ShockLife");n+=5;
            }
            // Shell records match their voxels mid-flight too (the front is linear).
            {
                var sim=new NativeBlastSim();sim.Blast(NativeBlastStyle.Fire,0,.95,0,3);for(int i=0;i<9;i++)sim.Update(Dt);var s=sim.Shocks[0];
                Check(Within(sim.Glow,s.X,s.Y,s.Z,s.RadiusAt(sim.Time),.01)>=NativeBlastSim.MinVoxels,"half way the record and its voxels agree");n++;
            }
            // Impacts: one front each, dome on the ground, sphere in the air; Line and Paddle draw none; field arrivals and the
            // Orbital Mark lock record theirs.
            foreach(bool ground in new[]{false,true})foreach(var (y,floor) in new (double,double?)[]{(.95,null),(2.2,0)})foreach(double R in new[]{.2,.6,1.5})
            {
                var sim=new NativeBlastSim();sim.Impact(1,y,-2,R,0x93db67,1,false,ground,null,floor);var s=sim.Shocks[0];double g=floor??y-.95;bool dome=ground&&y-g<=NativeBlastSim.NearGround;
                Check(sim.Shocks.Count==1&&s.Kind==NativeShockKind.Impact&&s.Style==null&&s.Dome==dome&&s.Y==(dome?g+.12:y)&&s.Radius==Math.Max(.3,R)&&s.Start==.1,"impact r"+R+(ground?" ground":"")+" at "+y+": its record");
                NativeBlastSim.Linear(0x93db67,out double r,out double gg,out double b);NativeBlastSim.Linear(0xffffff,out double wr,out double wg,out double wb);Check(s.CoolR==r&&s.CoolG==gg&&s.CoolB==b&&s.HotR==wr&&s.HotG==wg&&s.HotB==wb&&s.CoreR==wr,"impact record colours: white to hot");
                for(int i=0;i<steps;i++)sim.Update(Dt);Check(s.RadiusAt(sim.Time)==s.Radius&&Within(sim.Glow,s.X,s.Y,s.Z,s.Radius,.005)>=2,"impact record ends on its radius with its voxels");n+=3;
            }
            {
                var sim=new NativeBlastSim();sim.Line(0,1,0,3,1,0,0xffffff,0xb66cff,1);sim.Paddle(0,1,0,1,0,0,0xff5269,1);Check(sim.Shocks.Count==0,"lines and paddles have no front");
                sim.Field(new NativeBlastField{Id=1,Kind="acid",Fx="acid-rain",X=0,Y=.95,Z=0,Radius=3,Born=0,Until=4},0,1);Check(sim.Shocks.Count==1&&sim.Shocks[0].Kind==NativeShockKind.Impact,"a field arrival records its impact front");
                sim.Field(new NativeBlastField{Id=2,Kind="bomb",Fx="orbital-mark",X=4,Y=.95,Z=0,Radius=3.5,Born=0,Until=2.5,LockAt=1.9},1.9,1);Check(sim.Shocks.Count==2&&sim.Shocks[1].Seed==1,"the Orbital Mark lock records its front, seeds count fronts");
                sim.Blast(NativeBlastStyle.Fire,0,.95,0,3);sim.Update(Dt);Check(sim.Shocks.Count==3,"fronts accumulate");sim.Clear();Check(sim.Shocks.Count==0,"Clear empties the fronts");
                sim.Blast(NativeBlastStyle.Fire,0,.95,0,3);Check(sim.Shocks[0].Seed==0,"Clear restarts front seeds");sim.Reset();Check(sim.Shocks.Count==0,"Reset empties the fronts");
                // Fade: smooth from 1 at FrontSeconds to exactly 0 at the end of life.
                var s=new NativeBlastShock{Born=2,Life=NativeBlastSim.ShockLife,Start=.25,Radius=3};
                Check(s.Fade(2)==1&&s.Fade(2+NativeBlastSim.FrontSeconds)==1&&s.Fade(2+NativeBlastSim.ShockLife)==0&&s.Fade(2.4)>0&&s.Fade(2.4)<1&&s.RadiusAt(1)==.25&&s.RadiusAt(9)==3&&s.U(2)==0&&s.U(9)==1,"record envelope");n+=7;
            }
            return n;
        }

        // ---- smoke colour ----
        private static int Smoke(string root)
        {
            int n=0;
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))
            {
                int end=NativeBlastSim.SmokeEnd(style),smoke=NativeBlastSim.SmokeColor(style);double l=NativeBlastSim.Luminance(smoke),le=NativeBlastSim.Luminance(end);
                if(!NativeBlastSim.LightSmoke(style)){Check(end==NativeBlastSim.DarkSmoke,style+" smoke stays sooty");n++;continue;}
                Check(le>=NativeBlastSim.SmokeFloor*l&&le<=.7*l,style+" smoke ends darker but at least "+NativeBlastSim.SmokeFloor+" of its luminance ("+le/l+")");
                NativeBlastSim.Linear(smoke,out double r,out double g,out double b);NativeBlastSim.Linear(end,out double er,out double eg,out double eb);
                Check(Math.Max(er,Math.Max(eg,eb))-Math.Min(er,Math.Min(eg,eb))<=(Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b)))*.7+1e-3,style+" smoke ends less saturated");n+=2;
                // Behaviour: every drawn Smoke particle of the style (and its tints) stays above the floor through its life.
                foreach(var tag in new string[]{null,"geyser-wall","tidal-wave","frag"})
                {
                    if(tag!=null&&NativeBlastSim.StyleOf(tag)!=style)continue;
                    var sim=new NativeBlastSim();sim.Blast(style,0,.95,0,3,1,false,NativeBlastSim.TintOf(tag));int seen=0;double low=1e9;
                    for(int f=0;f<200;f++){sim.Update(Dt);var o=sim.Solid.Output;for(int i=0;i<sim.Solid.Drawn;i++){int w=i*NativeBlastPool.Stride;if((NativeFxRole)o[w+NativeBlastPool.RoleAt]!=NativeFxRole.Smoke)continue;seen++;low=Math.Min(low,.2126*o[w+10]+.7152*o[w+11]+.0722*o[w+12]);}}
                    // Shock and Nova blasts draw no smoke (the source's); the others do.
                    if(seen==0){Check(style==NativeBlastStyle.Shock||style==NativeBlastStyle.Nova,style+" draws smoke");continue;}
                    Check(low>=NativeBlastSim.SmokeFloor*l*(1-1e-5),style+(tag!=null?" "+tag:"")+" smoke never darkens below "+NativeBlastSim.SmokeFloor+" of its own luminance (lowest "+low/l+")");n++;
                }
            }
            Check(NativeBlastSim.LightSmoke(NativeBlastStyle.Frost)&&NativeBlastSim.LightSmoke(NativeBlastStyle.Kinetic)&&!NativeBlastSim.LightSmoke(NativeBlastStyle.Fire)&&!NativeBlastSim.LightSmoke(NativeBlastStyle.Void),"light and dark styles");
            Check(NativeBlastSim.SmokeEnd(NativeBlastStyle.Frost)!=NativeBlastSim.DarkSmoke&&NativeBlastSim.Luminance(NativeBlastSim.SmokeEnd(NativeBlastStyle.Frost))>.25,"frost steam is no longer black");
            // The fixture exporter applies the same cut (and the seed cut) to the source, and the fixture says so.
            string exporter=File.ReadAllText(Path.Combine(root,"tools/export_blast_fixtures.mjs"));
            Check(exporter.Contains("const FX_CUTS=[")&&exporter.Contains("function __ownerSmokeEnd(style,smoke)")&&exporter.Contains("['frost','kinetic','shock','nova','implosion']")&&exporter.Contains("e=c=>(c+(l-c)*.4)*.5")&&NativeBlastSim.SmokeGrey==.4&&NativeBlastSim.SmokeDim==.5,"the exporter mirrors SmokeEnd");
            Check(exporter.Contains("this.euler.set(age*sp*1.3+this.seed[i],age*sp+this.seed[i]*.7,0);")&&exporter.Contains("this.seed[i]=this.seed[last];"),"the exporter mirrors the stable tumble");
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/blast-parity.json"))))
            {var fx=doc.RootElement.GetProperty("owner").GetProperty("fx");Check(fx.GetProperty("date").GetString()=="2026-10-11"&&fx.GetProperty("smoke").GetBoolean()&&fx.GetProperty("seed").GetBoolean()&&fx.GetProperty("cuts").GetInt32()==7,"the parity frames carry the effects overhaul cuts");}
            return n+5;
        }

        // ---- the extended draw list ----
        private static void Script(NativeBlastSim sim,int steps)
        {
            sim.Blast(NativeBlastStyle.Fire,1,.95,-2,3);sim.Impact(-1,1.2,0,1.5,0xffaa64,1,false,true);sim.Blast(NativeBlastStyle.Shock,4,.95,4,2.4,.6,false,NativeBlastSim.TintOf("ball-lightning"));sim.Blast(NativeBlastStyle.Frost,-3,3,1,2,1,false,null,0);
            var field=new NativeBlastField{Id=3,Kind="storm",X=0,Y=.95,Z=3,Radius=4,Born=0,Until=1};
            for(int i=0;i<steps;i++){if(i*Dt<field.Until)sim.Field(field,i*Dt,1);if(i==20)sim.Blast(NativeBlastStyle.Void,0,.95,0,5);if(i==30)sim.Blast(NativeBlastStyle.Quake,2,.95,2,3);sim.Update(Dt);}
        }
        private static bool Same(NativeBlastSim a,NativeBlastSim b)
        {
            foreach(var (x,y) in new[]{(a.Solid,b.Solid),(a.Glow,b.Glow)}){if(x.Drawn!=y.Drawn||x.Count!=y.Count)return false;for(int i=0;i<x.Drawn*NativeBlastPool.Stride;i++)if(BitConverter.SingleToInt32Bits(x.Output[i])!=BitConverter.SingleToInt32Bits(y.Output[i]))return false;}
            if(a.Shocks.Count!=b.Shocks.Count)return false;for(int i=0;i<a.Shocks.Count;i++)if(!a.Shocks[i].Equals(b.Shocks[i]))return false;return true;
        }
        private static int DrawList()
        {
            int n=0;Check(NativeBlastPool.Stride==21&&NativeBlastPool.RoleAt==13&&NativeBlastPool.AgeAt==14&&NativeBlastPool.SeedAt==15&&NativeBlastPool.VelocityAt==16&&NativeBlastPool.RawSizeAt==19&&NativeBlastPool.FloorAt==20,"the draw list layout");
            // Determinism with every new field and the shock records: two runs, a Reset replay, an injected generator.
            var a=new NativeBlastSim();var b=new NativeBlastSim();Script(a,45);Script(b,45);Check(a.Solid.Drawn+a.Glow.Drawn>100&&a.Shocks.Count>0&&Same(a,b),"two seeded runs draw identical lists and fronts");
            b.Reset();Script(b,45);Check(Same(a,b),"Reset replays the same lists, seeds and fronts");
            var c=new NativeBlastSim(new NativeBlastRandom(4242).Next);Script(c,45);Check(!Same(a,c),"another generator differs");n+=4;
            // Each drawn entry agrees with its particle: role, age, velocity, raw size and floor.
            var sim=new NativeBlastSim();sim.Blast(NativeBlastStyle.Fire,0,.95,0,3,1,false,null,0);
            for(int f=0;f<50;f++)
            {
                sim.Update(Dt);
                foreach(var pool in new[]{sim.Solid,sim.Glow})
                {
                    var o=pool.Output;int drawn=0;
                    for(int i=0;i<pool.Count;i++)
                    {
                        var p=pool.At(i);if(p.age<0)continue;int w=drawn++*NativeBlastPool.Stride;var m=pool.Motion(i);var id=pool.Identity(i);double u=Math.Min(1,p.age/p.life);
                        Check(o[w]==(float)p.x&&o[w+NativeBlastPool.RoleAt]==(float)(int)id.role&&o[w+NativeBlastPool.SeedAt]==id.seed&&Math.Abs(o[w+NativeBlastPool.AgeAt]-u)<1e-6,"entry identity and age");
                        Check(o[w+NativeBlastPool.VelocityAt]==(float)m.vx&&o[w+NativeBlastPool.VelocityAt+1]==(float)m.vy&&o[w+NativeBlastPool.VelocityAt+2]==(float)m.vz&&o[w+NativeBlastPool.FloorAt]==(float)m.floor,"entry velocity and floor");
                        double raw=o[w+NativeBlastPool.RawSizeAt],edge=o[w+3];Check(o[w+NativeBlastPool.AgeAt]>=0&&o[w+NativeBlastPool.AgeAt]<=1&&(pool.Glow?Math.Abs(edge-raw)<1e-6:Math.Abs(edge-raw*Math.Min(1,(1-u)*4))<1e-5),"raw size before the opaque pool's end shrink");n+=3;
                    }
                    Check(drawn==pool.Drawn,"every drawn entry checked");
                }
            }
            return n;
        }

        // ---- burst sparks ----
        private static int Sparks()
        {
            int n=0;var pool=new NativeBurstPool(new NativeShotRandom(5).Next);pool.Spawn(0,1,0,0xffaa33,300,2,1);var s=pool.Sparks;
            Check(NativeBurstPool.SparkStride==15&&pool.Count==300,"spark layout and count");
            for(int i=0;i<pool.Count;i++){int k=i*NativeBurstPool.SparkStride;Check(s[k+NativeBurstPool.SparkRoleAt]==(int)NativeFxRole.Spark&&s[k+NativeBurstPool.SparkSeedAt]==i&&s[k+NativeBurstPool.SparkAgeAt]==0&&s[k+NativeBurstPool.SparkLifeAt]==s[k+7],"spark role, seed, age and life");n++;}
            var lives=new Dictionary<int,double>();for(int i=0;i<pool.Count;i++){int k=i*NativeBurstPool.SparkStride;lives[(int)s[k+NativeBurstPool.SparkSeedAt]]=s[k+7];}
            for(int f=0;f<30;f++)
            {
                pool.Update(Dt);s=pool.Sparks;
                for(int i=0;i<pool.Count;i++){int k=i*NativeBurstPool.SparkStride;int seed=(int)s[k+NativeBurstPool.SparkSeedAt];double u=s[k+NativeBurstPool.SparkAgeAt];Check(lives.ContainsKey(seed)&&Math.Abs(s[k+NativeBurstPool.SparkLifeAt]-lives[seed])<1e-12&&u>0&&u<1&&Math.Abs(u-(1-s[k+7]/lives[seed]))<1e-9,"a spark keeps its seed through compaction and ages 0..1");n++;}
            }
            pool.Clear();pool.Spawn(0,0,0,0xffffff,1,1,1);Check(pool.Sparks[NativeBurstPool.SparkSeedAt]==0,"Clear restarts spark seeds");
            return n+2;
        }

        // ---- textures ----
        private const ulong AtlasHash=0x69479d3d84412f11UL,NoiseHash=0x61c745a2d208c5b6UL;
        private static int Atlas(out double ms)
        {
            int n=0;var sw=Stopwatch.StartNew();var atlas=NativeVfxTextures.SmokeAtlas();sw.Stop();ms=sw.Elapsed.TotalMilliseconds;
            int size=NativeVfxTextures.SmokeSize,tiles=NativeVfxTextures.SmokeTiles,T=size/tiles;Check(atlas.Length==size*size*4&&tiles*tiles==NativeVfxTextures.SmokeVariants&&T==128,"atlas layout");
            Check(NativeVfxTextures.Hash(NativeVfxTextures.SmokeAtlas())==NativeVfxTextures.Hash(atlas),"the atlas is deterministic");
            ulong hash=NativeVfxTextures.Hash(atlas);Check(hash==AtlasHash,"pinned atlas hash (now 0x"+hash.ToString("x16")+")");n+=3;
            for(int t=0;t<tiles*tiles;t++)
            {
                int ox=(t%tiles)*T,oy=(t/tiles)*T,maxR=0,cover=0;double gTop=0,gBottom=0,top=0,bottom=0;int bLow=255,bHigh=0;
                for(int y=0;y<T;y++)for(int x=0;x<T;x++)
                {
                    int w=((oy+y)*size+ox+x)*4;int R=atlas[w];
                    if(x==0||y==0||x==T-1||y==T-1){Check(atlas[w]==0&&atlas[w+1]==0&&atlas[w+2]==0&&atlas[w+3]==0,"tile "+t+" border texel ("+x+","+y+") is 0");n++;}
                    double cx=(x+.5)/T*2-1,cy=(y+.5)/T*2-1;if(Math.Sqrt(cx*cx+cy*cy)>=NativeVfxTextures.SmokeEdge)Check(R==0,"tile "+t+" is 0 beyond its round falloff");
                    if(R==0){Check(atlas[w+1]==0&&atlas[w+2]==0&&atlas[w+3]==0,"G, B and A are 0 outside the puff");continue;}
                    maxR=Math.Max(maxR,R);if(R>128){cover++;bLow=Math.Min(bLow,atlas[w+2]);bHigh=Math.Max(bHigh,atlas[w+2]);}
                    if(R>200){if(y>=T/2){gTop+=atlas[w+1];top++;}else{gBottom+=atlas[w+1];bottom++;}}
                }
                Check(maxR==255&&cover>1500&&cover<9000,"tile "+t+" has a dense puff of a sensible size ("+cover+" texels)");
                Check(top>0&&bottom>0&&gTop/top>gBottom/bottom+25,"tile "+t+": lit from above, the top is brighter ("+(gTop/top).ToString("0")+" vs "+(gBottom/bottom).ToString("0")+")");
                Check(bLow<90&&bHigh>220,"tile "+t+": erosion thresholds spread from the edges to the core ("+bLow+".."+bHigh+")");n+=3;
            }
            // Erosion: full at birth, monotone, gone at the end of life.
            foreach(double D in new[]{.1,.5,1})foreach(double B in new[]{0,.3,.7,1})
            {
                Check(Math.Abs(NativeVfxTextures.Dissolve(D,B,0)-D)<1e-12&&NativeVfxTextures.Dissolve(D,B,1)==0,"dissolve ends");double last=D;
                for(int i=1;i<=20;i++){double v=NativeVfxTextures.Dissolve(D,B,i/20.0);Check(v<=last+1e-12,"dissolve is monotone");last=v;}n++;
            }
            NativeVfxTextures.TileRect(5,out double u0,out double v0,out double u1,out double v1);Check(u0==.25&&v0==.25&&u1==.5&&v1==.5,"tile 5 is column 1, row 1 from the bottom");
            NativeVfxTextures.TileRect(16+3,out u0,out v0,out _,out _);Check(u0==.75&&v0==0,"tile indices wrap");
            // The noise tile wraps seamlessly: the jump across the seam is no larger than between neighbours.
            var noise=NativeVfxTextures.NoiseTile();int N=64;Check(noise.Length==N*N*4&&NativeVfxTextures.Hash(NativeVfxTextures.NoiseTile())==NativeVfxTextures.Hash(noise),"noise tile layout and determinism");
            ulong nh=NativeVfxTextures.Hash(noise);Check(nh==NoiseHash,"pinned noise hash (now 0x"+nh.ToString("x16")+")");
            for(int ch=0;ch<4;ch++)
            {
                double seam=0,inner=0;int lo=255,hi=0;
                for(int y=0;y<N;y++){seam+=Math.Abs(noise[(y*N+N-1)*4+ch]-noise[(y*N)*4+ch]);for(int x=0;x<N-1;x++)inner+=Math.Abs(noise[(y*N+x+1)*4+ch]-noise[(y*N+x)*4+ch]);}
                for(int x=0;x<N;x++)seam+=Math.Abs(noise[((N-1)*N+x)*4+ch]-noise[x*4+ch]);for(int y=0;y<N-1;y++)for(int x=0;x<N;x++)inner+=Math.Abs(noise[((y+1)*N+x)*4+ch]-noise[(y*N+x)*4+ch]);
                for(int i=0;i<N*N;i++){lo=Math.Min(lo,noise[i*4+ch]);hi=Math.Max(hi,noise[i*4+ch]);}
                seam/=2*N;inner/=2*N*(N-1);Check(seam<=inner*2+1&&hi-lo>80,"noise channel "+ch+" tiles seamlessly and has contrast (seam "+seam.ToString("0.0")+", neighbours "+inner.ToString("0.0")+")");n++;
            }
            return n+4;
        }

        // ---- meshes ----
        // A vertex position to 1e-6 m (adding 0 turns a rounded -0 into 0, so the seam of a closed ring matches).
        private static string Key(double[] p,int v)=>(Math.Round(p[v*3]*1e6)+0.0)+","+(Math.Round(p[v*3+1]*1e6)+0.0)+","+(Math.Round(p[v*3+2]*1e6)+0.0);
        // Closed: every edge (by position) is used exactly once in each direction. Outward: positive volume, and each face normal
        // points away from the given interior point. Returns the triangle count checked.
        private static int Solid(NativeVfxMesh m,int[] triangles,double cx,double cy,double cz,string what,bool flat,bool convex=true)
        {
            var p=m.Positions;var edges=new Dictionary<string,int>();double volume=0;
            for(int i=0;i<triangles.Length;i+=3)
            {
                int a=triangles[i],b=triangles[i+1],c=triangles[i+2];
                double ux=p[b*3]-p[a*3],uy=p[b*3+1]-p[a*3+1],uz=p[b*3+2]-p[a*3+2],vx=p[c*3]-p[a*3],vy=p[c*3+1]-p[a*3+1],vz=p[c*3+2]-p[a*3+2];
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx,area=Math.Sqrt(nx*nx+ny*ny+nz*nz);Check(area>1e-9,what+": no degenerate triangle");
                double fx=(p[a*3]+p[b*3]+p[c*3])/3-cx,fy=(p[a*3+1]+p[b*3+1]+p[c*3+1])/3-cy,fz=(p[a*3+2]+p[b*3+2]+p[c*3+2])/3-cz;
                if(convex)Check(nx*fx+ny*fy+nz*fz>0,what+": triangle "+i/3+" faces outward (Unity winding)");
                volume+=(p[a*3]*(p[b*3+1]*p[c*3+2]-p[b*3+2]*p[c*3+1])-p[a*3+1]*(p[b*3]*p[c*3+2]-p[b*3+2]*p[c*3])+p[a*3+2]*(p[b*3]*p[c*3+1]-p[b*3+1]*p[c*3]))/6;
                if(flat)foreach(int v in new[]{a,b,c}){double d=(m.Normals[v*3]*nx+m.Normals[v*3+1]*ny+m.Normals[v*3+2]*nz)/area;Check(Math.Abs(d-1)<1e-9,what+": flat normals are the face normal");}
                foreach(var (s,e) in new[]{(a,b),(b,c),(c,a)}){string k=Key(p,s)+">"+Key(p,e);edges[k]=edges.TryGetValue(k,out int q)?q+1:1;}
            }
            foreach(var kv in edges){var parts=kv.Key.Split('>');Check(kv.Value==1&&edges.TryGetValue(parts[1]+">"+parts[0],out int back)&&back==1,what+": closed, each edge once each way");}
            Check(volume>0,what+": positive volume");return triangles.Length/3;
        }
        private static int Meshes()
        {
            int n=0;var counts=new HashSet<int>();
            Check(NativeVfxMeshes.Chunks.Length==NativeVfxMeshes.ChunkVariants&&NativeVfxMeshes.ChunkVariants==4,"four rubble chunks");
            foreach(var m in NativeVfxMeshes.Chunks)
            {
                var p=m.Positions;double cx=0,cy=0,cz=0,span=0;for(int i=0;i<m.VertexCount;i++){cx+=p[i*3];cy+=p[i*3+1];cz+=p[i*3+2];}cx/=m.VertexCount;cy/=m.VertexCount;cz/=m.VertexCount;
                var hull=new HashSet<string>();for(int i=0;i<m.VertexCount;i++){hull.Add(Key(p,i));for(int a=0;a<3;a++){Check(Math.Abs(p[i*3+a])<=.5+1e-9,m.Name+" fits the unit box");span=Math.Max(span,Math.Abs(p[i*3+a]));}}
                Check(hull.Count>=8&&hull.Count<=14&&Math.Abs(span-.5)<1e-9&&m.VertexCount==m.TriangleCount*3,m.Name+": "+hull.Count+" hull points, flat-shaded, spanning the box");counts.Add(hull.Count);
                n+=Solid(m,m.Triangles,cx,cy,cz,m.Name,true);foreach(double e in m.Emissive)Check(e==0,"chunks do not glow");
            }
            Check(counts.Count==4,"the chunk variants differ");
            {
                var m=NativeVfxMeshes.Shard;var p=m.Positions;double top=-1,bottom=1;var hull=new HashSet<string>();
                for(int i=0;i<m.VertexCount;i++){top=Math.Max(top,p[i*3+1]);bottom=Math.Min(bottom,p[i*3+1]);hull.Add(Key(p,i));for(int a=0;a<3;a++)Check(Math.Abs(p[i*3+a])<=.5+1e-9,"the shard fits the unit box");}
                Check(top==.5&&bottom==-.5&&hull.Count==8&&m.TriangleCount==12,"the shard is a hexagonal bipyramid with tips at +-.5");n+=Solid(m,m.Triangles,0,-.12,0,"crystal shard",true);
            }
            {
                var m=NativeVfxMeshes.Shell;Check(m.VertexCount==162&&m.TriangleCount==320,"the shell is a 2-subdivision icosphere");
                for(int i=0;i<m.VertexCount;i++){double r=Math.Sqrt(m.Positions[i*3]*m.Positions[i*3]+m.Positions[i*3+1]*m.Positions[i*3+1]+m.Positions[i*3+2]*m.Positions[i*3+2]);Check(Math.Abs(r-1)<1e-12&&m.Normals[i*3]==m.Positions[i*3]&&m.Normals[i*3+1]==m.Positions[i*3+1]&&m.Normals[i*3+2]==m.Positions[i*3+2],"unit radius, smooth normals");}
                n+=Solid(m,m.Triangles,0,0,0,"shock shell",false);
                // Shared vertices: each edge (by index) is in exactly two triangles.
                var used=new Dictionary<long,int>();for(int i=0;i<m.Triangles.Length;i+=3)for(int k=0;k<3;k++){int a=m.Triangles[i+k],b=m.Triangles[i+(k+1)%3];long key=Math.Min(a,b)*1000L+Math.Max(a,b);used[key]=used.TryGetValue(key,out int c)?c+1:1;}
                foreach(var kv in used)Check(kv.Value==2,"the shell is welded");
                var d=NativeVfxMeshes.Dome;Check(d.TriangleCount==160&&d.VertexCount==91,"the dome is the upper half ("+d.TriangleCount+" triangles, "+d.VertexCount+" vertices)");
                var rim=new Dictionary<long,int>();for(int i=0;i<d.Triangles.Length;i+=3)for(int k=0;k<3;k++){int a=d.Triangles[i+k],b=d.Triangles[i+(k+1)%3];Check(d.Positions[a*3+1]>=0,"the dome is above its base");long key=Math.Min(a,b)*1000L+Math.Max(a,b);rim[key]=rim.TryGetValue(key,out int c)?c+1:1;}
                int open=0;foreach(var kv in rim)if(kv.Value==1){open++;Check(d.Positions[(int)(kv.Key/1000)*3+1]==0&&d.Positions[(int)(kv.Key%1000)*3+1]==0,"the dome's open rim lies exactly on y = 0");}
                Check(open==20,"the dome's rim is 20 edges ("+open+")");
                for(int i=0;i<d.Triangles.Length;i+=3){var q=d.Positions;int a=d.Triangles[i],b=d.Triangles[i+1],c=d.Triangles[i+2];double ux=q[b*3]-q[a*3],uy=q[b*3+1]-q[a*3+1],uz=q[b*3+2]-q[a*3+2],vx=q[c*3]-q[a*3],vy=q[c*3+1]-q[a*3+1],vz=q[c*3+2]-q[a*3+2];Check((uy*vz-uz*vy)*(q[a*3]+q[b*3]+q[c*3])+(uz*vx-ux*vz)*(q[a*3+1]+q[b*3+1]+q[c*3+1])+(ux*vy-uy*vx)*(q[a*3+2]+q[b*3+2]+q[c*3+2])>0,"the dome faces outward");n++;}
            }
            {
                var m=NativeVfxMeshes.Bomb;var parts=new Dictionary<int,List<int>>();
                for(int i=0;i<m.Triangles.Length;i+=3){int part=m.Parts[m.Triangles[i]];Check(m.Parts[m.Triangles[i+1]]==part&&m.Parts[m.Triangles[i+2]]==part,"a bomb triangle stays in one part");if(!parts.ContainsKey(part))parts[part]=new List<int>();parts[part].AddRange(new[]{m.Triangles[i],m.Triangles[i+1],m.Triangles[i+2]});}
                Check(parts.Count==5,"casing, band, cap, fuse and tip");double casing=0,tipTop=0;int glowing=0;
                for(int v=0;v<m.VertexCount;v++)
                {
                    var part=(NativeBombPart)m.Parts[v];double e=m.Emissive[v];
                    Check(part==NativeBombPart.Band?e==NativeVfxMeshes.BandGlow:part==NativeBombPart.Tip?e==NativeVfxMeshes.TipGlow:e==0,"only the band and the fuse tip glow");if(e>0)glowing++;
                    if(part==NativeBombPart.Casing)casing=Math.Max(casing,Math.Sqrt(m.Positions[v*3]*m.Positions[v*3]+m.Positions[v*3+1]*m.Positions[v*3+1]+m.Positions[v*3+2]*m.Positions[v*3+2]));
                    if(part==NativeBombPart.Tip)tipTop=Math.Max(tipTop,m.Positions[v*3+1]);
                }
                Check(glowing>0&&Math.Abs(casing*2-NativeVfxMeshes.BombDiameter)<1e-9&&Math.Abs(tipTop-(NativeVfxMeshes.BombTipY+.03))<1e-9,"the bomb's casing is "+NativeVfxMeshes.BombDiameter+" m across with glowing band and fuse tip");
                // Every part is closed with each edge once each way (consistent winding) and positive volume, so it faces outward;
                // the band is an annulus (not star-shaped), the other parts also face away from their own centres.
                foreach(var kv in parts)
                {
                    var tris=kv.Value.ToArray();double cx=0,cy=0,cz=0;foreach(int v in tris){cx+=m.Positions[v*3];cy+=m.Positions[v*3+1];cz+=m.Positions[v*3+2];}cx/=tris.Length;cy/=tris.Length;cz/=tris.Length;
                    n+=Solid(m,tris,cx,cy,cz,"bomb "+(NativeBombPart)kv.Key,true,(NativeBombPart)kv.Key!=NativeBombPart.Band);
                }
            }
            return n;
        }

        // ---- bomb pose ----
        private static int Bomb()
        {
            int n=0;var cubes=new NativeFieldCube[NativeFieldRules.MaxCubes];
            foreach(var (kind,fx) in new (string,string)[]{("bomb",null),("bomb","sticky-bomb"),("implosion","implosion-bomb"),("mine",null),("mine","proximity-mail"),("bomb","bubble-mine")})
            {
                var f=new NativeBlastField{Kind=kind,Fx=fx,X=2,Y=.95,Z=-1,Radius=3,Born=1,Until=kind=="mine"?7:3.4};var look=NativeFieldRules.LookOf(kind,fx);
                for(double t=1;t<f.Until;t+=1/97.0)
                {
                    Check(NativeFieldRules.Bomb(f,t,out var pose)&&pose.Look==look,kind+" "+fx+": a modelled body");int c=NativeFieldRules.Cubes(f,0x123456,t,cubes);var body=cubes[0];
                    Check(pose.X==body.X&&pose.Y==body.Y&&pose.Z==body.Z&&pose.Qx==body.Qx&&pose.Qy==body.Qy&&pose.Qz==body.Qz&&pose.Qw==body.Qw&&pose.Body==body.Color,"pose follows the source body cube");
                    Near(pose.Sx*.38,body.Sx,1e-12,"scale x");Near(pose.Sy*.38,body.Sy,1e-12,"scale y");Near(pose.Sz*.38,body.Sz,1e-12,"scale z");
                    bool fuse=c>1&&cubes[1].Sx==.1&&Math.Abs(cubes[1].Y-pose.FuseY)<1e-12;Check(fuse==pose.FuseLit,"fuse shows exactly when the source's fuse cube does");
                    Check(pose.FuseLit==(pose.Emissive>0)&&pose.Emissive<=1&&pose.Emissive>=0,"the band and tip glow exactly while the fuse shows");
                    Check(pose.BlinkRate==6+pose.Progress*20&&pose.Age==t-f.Born&&Math.Abs(pose.Left-(f.Until-t))<1e-12,"blink rate and clocks");n+=6;
                }
            }
            var g=new NativeBlastField{Kind="bomb",X=0,Y=.95,Z=0,Radius=3,Born=0,Until=4};
            Check(!NativeFieldRules.Bomb(new NativeBlastField{Kind="acid",X=0,Y=.95,Z=0,Radius=3,Born=0,Until=4},1,out _)&&!NativeFieldRules.Bomb(new NativeBlastField{Kind="black-hole",X=0,Y=.95,Z=0,Radius=3,Born=0,Until=4},1,out _)&&!NativeFieldRules.Bomb(new NativeBlastField{Kind="bomb",Fx="orbital-mark",X=0,Y=.95,Z=0,Radius=3,Born=0,Until=4},1,out _),"no bomb body for areas, black holes or ability bombs");
            // Blinks speed up with the fuse, and the final half second flickers.
            int Blinks(double from,double to){int k=0;bool last=false;for(double t=from;t<to;t+=.002){NativeFieldRules.Bomb(g,t,out var p);if(p.FuseLit&&!last)k++;last=p.FuseLit;}return k;}
            Check(Blinks(2.5,3.5)>Blinks(0,1)+2,"the blink speeds up ("+Blinks(0,1)+" -> "+Blinks(2.5,3.5)+" per second)");
            int flicker=0;for(double t=3.5;t<4;t+=.002){NativeFieldRules.Bomb(g,t,out var p);if(!p.FuseLit&&Math.Sin(t*(6+p.Progress*20))>-.2)flicker++;}Check(flicker>0,"the last half second flickers off");
            NativeFieldRules.Bomb(g,0,out var p0);Check(p0.Sx==1&&p0.Sy==1&&p0.FuseLit&&p0.Emissive>0&&Math.Abs(p0.FuseY-p0.Y-.26)<1e-12&&Math.Abs(NativeVfxMeshes.BombTipY-.262)<1e-12,"at rest: scale 1, fuse lit, tip where the source's fuse cube sat");
            double peak=0;for(double t=3;t<3.5;t+=.001){NativeFieldRules.Bomb(g,t,out var p);peak=Math.Max(peak,p.Emissive);}Check(peak>.95,"the glow peaks near full late in the fuse ("+peak+")");
            return n+5;
        }
    }
}
#endif
