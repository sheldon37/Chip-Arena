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
            int sites=Sites(root),roles=Roles(),seeds=Seeds(),shocks=Shocks(),smoke=Smoke(root),pool=DrawList(),strands=Strands(),sparks=Sparks();
            Console.WriteLine("PASS effects base layer: "+sites+" blast spawn sites name a role, "+roles+" pinned role histograms and sets (11 styles, tints, impacts, lines, paddles, 20 fields), "+seeds+" seed and draw-order checks, "+shocks+" shock front checks, "+smoke+" smoke colour checks, "+pool+" draw-list and determinism checks, "+strands+" strand checks (no segment between arcs, spokes or lines), "+sparks+" burst spark checks");
            int atlas=Atlas(out double ms),toon=Toon(out double toonMs),meshes=Meshes(),bomb=Bomb();
            Console.WriteLine("PASS effects base layer assets: smoke atlas (512 px, 16 puffs, built in "+ms.ToString("0.0")+" ms on this runtime; not a timed gate), noise tile, "+atlas+" texture checks, "+meshes+" mesh checks (4 rubble chunks, crystal shard, chip bomb, shock shell and dome) and "+bomb+" bomb pose checks");
            int reads=ToonFxChecks.Run(root);
            Console.WriteLine("PASS effects toon pass: toon sheet (1024 x 512, 16 hard-shadow puffs, 8-frame round flash, 4 crescents, spike, lens, star, droplet; built in "+toonMs.ToString("0.0")+" ms, not a timed gate), "+toon+" toon sheet checks and "+reads+" composition, timing, splash and no-disc checks");
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
                // The role is the last argument: one literal role, or a choice between two (fire-family debris).
                foreach(var call in calls){Check(Regex.IsMatch(call,@",role:(?:\w+\?NativeFxRole\.[A-Z][A-Za-z]+:)?NativeFxRole\.[A-Z][A-Za-z]+\)$"),Path.GetFileName(file)+": a pool spawn without its role: "+call);n++;}
                if(Path.GetFileName(file)=="NativeBlastSim.cs")total=calls.Count;
            }
            Check(total==74,"NativeBlastSim's 74 spawn sites (toon pass; "+total+")");
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
        // Pinned 2026-10-11 (toon pass): each style at r 3, quality 1, on the default floor, every spawned particle (delayed ones
        // included), the two water looks, Blink Strike (cyan: no crackle) and a water globe's floor splash.
        private static readonly (string what,string roles)[] Pinned={
            ("Fire","Core 4 Fireball 26 Shell 39 Smoke 5 Spark 24 HotDebris 11 Flash 2 Lick 8 Halo 1"),("Shock","Core 9 Shell 52 Spark 11 Bolt 50 Flash 2"),
            ("Implosion","Core 9 Shell 33 Spark 13 Cloud 11 Streak 39 Flash 2"),("Frost","Core 7 Shell 35 Spark 13 Shard 15 Snow 13 Spike 17 Mist 7 Halo 1"),
            ("Toxic","Core 5 Shell 44 Smoke 112 Debris 7 Spark 9 Cloud 11 Flash 3 Blob 7 Halo 1"),("Void","Smoke 9 Cloud 9 Dark 4 Streak 35 Droplet 13 Halo 1"),("Nova","Core 11 Shell 65 Spark 15 Flash 1 Lick 17"),
            ("Kinetic","Core 7 Shell 52 Smoke 15 Debris 13 Spark 24"),("Quake","Shell 33 Smoke 29 Debris 7 Rock 15"),
            ("Meteor","Core 4 Fireball 31 Shell 39 Smoke 5 Spark 39 Ember 11 HotDebris 15 Flash 3 Lick 8 Halo 1"),("Mine","Core 4 Fireball 20 Shell 39 Smoke 5 Spark 33 HotDebris 11 Flash 2 Lick 8 Halo 1"),
            ("tidal-wave","Core 5 Shell 52 Water 15 Droplet 20 Mist 3"),("geyser-wall","Core 1 Shell 35 Water 46 Droplet 32 Mist 5"),("blink-strike","Core 9 Shell 52 Spark 11 Flash 2"),
            ("splash","Mote 1 Flash 1 Water 9 Droplet 16 Mist 1"),
            ("ball-lightning",PinnedBall),("impact air",PinnedImpactAir),("impact ground",PinnedImpactGround),("line",PinnedLine),("paddle",PinnedPaddle)};
        private const string PinnedBall="Core 9 Shell 52 Spark 17 Bolt 104 Flash 2",PinnedImpactAir="Mote 5 Core 5 Shell 18 Debris 6",PinnedImpactGround="Mote 5 Core 4 Shell 34 Debris 6",PinnedLine="Line 24",PinnedPaddle="Mote 16";
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
                else if(what=="ball-lightning"||what=="blink-strike"||what=="tidal-wave"||what=="geyser-wall")sim.Blast(NativeBlastSim.StyleOf(what),0,.95,0,3,1,false,NativeBlastSim.TintOf(what));
                else if(what=="splash")sim.Splash(0,0,0,1,1);
                else if(what=="impact air")sim.Impact(0,.95,0,1.5,0xffaa64,1);
                else if(what=="impact ground")sim.Impact(0,.95,0,1.2,0x93db67,1,false,true);
                else if(what=="line")sim.Line(0,.95,0,4,1.5,2,0xffffff,0xb66cff,1,.3,.22,.08);
                else sim.Paddle(0,1,0,1,.2,.5,0xff5269,1);
                string h=Histogram(sim);got.Append("(\"").Append(what).Append("\",\"").Append(h).Append("\"),");if(h!=roles)ok=false;n++;
                // A tint never changes the roles (only colours): the tinted looks match their style.
            }
            // The two water looks (tidal-wave, geyser-wall) have their own composition (pinned above); blink-strike (cyan) draws no crackle.
            foreach(var (style,tag) in new[]{(NativeBlastStyle.Kinetic,"frag"),(NativeBlastStyle.Fire,"flame-lance")})
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
                int expected=style==NativeBlastStyle.Void?0:style==NativeBlastStyle.Quake?2:1;Check(sim.Shocks.Count==expected,what+": one shock record per front, and Quake's slam wave ("+sim.Shocks.Count+")");n++;if(expected==0)continue;
                var s=sim.Shocks[0];double g=floor??y-.95;bool near=y-g<=NativeBlastSim.NearGround,implosion=style==NativeBlastStyle.Implosion;double born=implosion?NativeBlastSim.ImplosionPop:0;
                Check(s.Radius==Math.Max(.4,R)&&s.Born==born&&s.Life==NativeBlastSim.ShockLife&&s.Floor==g&&s.Seed==0,what+": record radius, birth (Implosion's at its pop), life, floor and seed");
                if(style==NativeBlastStyle.Quake)
                {
                    // The slam wave: an upright wall on the floor point, from SlamStart to SlamReach of the radius in SlamTravel, never a dome.
                    var w=sim.Shocks[1];double rr=Math.Max(.4,R);
                    Check(w.Kind==NativeShockKind.Slam&&!w.Dome&&w.Y==(near?g:y)&&Math.Abs(w.Start-NativeBlastSim.SlamStart*rr)<1e-12&&Math.Abs(w.Radius-NativeBlastSim.SlamReach*rr)<1e-12&&w.Travel==NativeBlastSim.SlamTravel&&w.FrontTime==NativeBlastSim.SlamTravel&&w.Life==NativeBlastSim.SlamLife&&w.Height==Math.Max(NativeBlastSim.SlamMinHeight,NativeBlastSim.SlamHeight*rr)&&w.Seed==1,what+": the slam wave record");
                    Check(w.RadiusAt(w.Born+NativeBlastSim.SlamTravel)==w.Radius&&w.RadiusAt(w.Born+NativeBlastSim.SlamTravel*.5)<w.Radius&&w.Fade(w.Born+NativeBlastSim.SlamLife)==0,what+": the slam wave reaches its radius at SlamTravel and is gone at SlamLife");n+=2;
                }
                if(implosion){Check(!s.Started(0)&&s.Started(born)&&s.RadiusAt(born)==s.Start,what+": the front waits for the pop");for(int i=0;i<(int)NativeBlastSim.Round(born/Dt);i++)sim.Update(Dt);n++;}
                Check(style==NativeBlastStyle.Nova?(s.Kind==NativeShockKind.Nova&&!s.Dome&&s.Y==Math.Max(g+.35,y)&&s.Start==.25):(s.Kind==NativeShockKind.Blast&&s.Dome==near&&s.Y==(near?g:y)&&s.Start==Math.Min(.25,s.Radius*.2)),what+": record origin and shape");
                Check(s.Style==style&&s.Voxels>=NativeBlastSim.MinVoxels,what+": record style and voxels");
                for(int i=0;i<steps;i++){sim.Update(Dt);if(i<steps-1)Check(s.RadiusAt(sim.Time)<s.Radius,what+": still growing");}
                // The sim clock sums frame steps, so it lands on FrontSeconds to within rounding; the record itself is exact there.
                Check(Math.Abs(sim.Time-born-NativeBlastSim.FrontSeconds)<1e-12&&s.RadiusAt(s.Born+NativeBlastSim.FrontSeconds)==s.Radius&&s.Progress(s.Born+NativeBlastSim.FrontSeconds)==1&&Math.Abs(s.RadiusAt(sim.Time)-s.Radius)<1e-9&&s.Fade(sim.Time)==1,what+": the record ends exactly on the gameplay radius at FrontSeconds");
                Check(Within(sim.Glow,s.X,s.Y,s.Z,s.Radius,.005)>=NativeBlastSim.MinVoxels,what+": its Shell voxels are on the record's radius");
                Check(sim.Shocks.Count>=1&&sim.Shocks[0].Kind!=NativeShockKind.Slam,what+": alive through the fade");
                for(int i=0;i<12;i++)sim.Update(Dt);Check(sim.Shocks.Count==0,what+": expired after ShockLife (and the slam wave after SlamLife)");n+=5;
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
        // Base layer: light styles' smoke no longer fades to near-black. Toon pass (2026-10-11): dark styles' smoke ends slate
        // (never the source's near-black DarkSmoke), and no puff a blast spawns ends near-black except Void's ink, which is black
        // on purpose (its black shapes over the violet glow).
        private static int Smoke(string root)
        {
            int n=0;
            foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))
            {
                int end=NativeBlastSim.SmokeEnd(style),smoke=NativeBlastSim.SmokeColor(style);double l=NativeBlastSim.Luminance(smoke),le=NativeBlastSim.Luminance(end);
                if(!NativeBlastSim.LightSmoke(style)){Check(end!=NativeBlastSim.DarkSmoke&&le>2*NativeBlastSim.Luminance(NativeBlastSim.DarkSmoke)||style==NativeBlastStyle.Void&&end!=NativeBlastSim.DarkSmoke,style+" smoke ends slate, not near-black ("+end.ToString("x6")+")");n++;continue;}
                Check(le>=NativeBlastSim.SmokeFloor*l&&le<=.7*l,style+" smoke ends darker but at least "+NativeBlastSim.SmokeFloor+" of its luminance ("+le/l+")");
                NativeBlastSim.Linear(smoke,out double r,out double g,out double b);NativeBlastSim.Linear(end,out double er,out double eg,out double eb);
                Check(Math.Max(er,Math.Max(eg,eb))-Math.Min(er,Math.Min(eg,eb))<=(Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b)))*.7+1e-3,style+" smoke ends less saturated");n+=2;
            }
            Check(NativeBlastSim.SmokeEnd(NativeBlastStyle.Fire)==NativeBlastSim.Slate&&NativeBlastSim.Slate==0x3e4556&&NativeBlastSim.FireYellow==0xffce00,"fire smoke ends slate #3E4556; fire puffs start #FFCE00");
            // Behaviour: the end colour of every puff (Smoke, Cloud, Mist, Fireball, Water) of every look but Void, which is dark on
            // purpose: black ink over its violet glow, then a lilac ring that sinks into navy.
            double floor=NativeBlastSim.Luminance(0x1d1f2a);
            foreach(var (style,tag) in new (NativeBlastStyle,string)[]{(NativeBlastStyle.Fire,null),(NativeBlastStyle.Mine,null),(NativeBlastStyle.Meteor,null),(NativeBlastStyle.Kinetic,null),(NativeBlastStyle.Kinetic,"frag"),(NativeBlastStyle.Kinetic,"tidal-wave"),(NativeBlastStyle.Frost,null),(NativeBlastStyle.Frost,"geyser-wall"),(NativeBlastStyle.Toxic,null),(NativeBlastStyle.Quake,null),(NativeBlastStyle.Implosion,null)})
            {
                var sim=new NativeBlastSim();sim.Blast(style,0,.95,0,3,1,false,NativeBlastSim.TintOf(tag));int puffs=0;double low=1e9;
                for(int i=0;i<sim.Solid.Count;i++)
                {
                    var role=sim.Solid.Identity(i).role;if(role!=NativeFxRole.Smoke&&role!=NativeFxRole.Cloud&&role!=NativeFxRole.Mist&&role!=NativeFxRole.Fireball&&role!=NativeFxRole.Water)continue;
                    var c=sim.Solid.Colors(i);puffs++;low=Math.Min(low,NativeBlastSim.Luminance(c.to));
                }
                Check(puffs>0&&low>=floor,(tag??style.ToString())+": its puffs end slate or lighter, never near-black (lowest end luminance "+low+", "+puffs+" puffs)");n++;
            }
            // The Electron exporter still mirrors the base layer's cuts for the source frames it records (impacts, fields, lines,
            // paddles), and the fixture says so.
            string exporter=File.ReadAllText(Path.Combine(root,"tools/export_blast_fixtures.mjs"));
            Check(exporter.Contains("const FX_CUTS=[")&&exporter.Contains("function __ownerSmokeEnd(style,smoke)")&&exporter.Contains("e=c=>(c+(l-c)*.4)*.5")&&NativeBlastSim.SmokeGrey==.4&&NativeBlastSim.SmokeDim==.5,"the exporter keeps the light-smoke cut (light styles only; its source blasts are no longer recorded)");
            int light=0;foreach(NativeBlastStyle style in Enum.GetValues(typeof(NativeBlastStyle)))if(NativeBlastSim.LightSmoke(style))light++;Check(light==6&&NativeBlastSim.LightSmoke(NativeBlastStyle.Quake)&&exporter.Contains("['frost','kinetic','shock','nova','implosion','quake']"),"six light smoke styles, Quake's dust among them, as the exporter lists");
            var bombField=new NativeBlastField{Kind="bomb",X=0,Y=.95,Z=0,Radius=3,Born=0,Until=3};
            Check(exporter.Contains("this.glow.spawn(f.pos.x,ground+.472,f.pos.z,")&&Math.Abs(NativeFieldRules.BombTipY(bombField)-(Math.Max(.03,bombField.Y-.95)+.472))<1e-12,"the exporter mirrors the fuse sparks' start at the modelled tip");
            Check(exporter.Contains("this.euler.set(age*sp*1.3+this.seed[i],age*sp+this.seed[i]*.7,0);")&&exporter.Contains("this.seed[i]=this.seed[last];"),"the exporter mirrors the stable tumble");
            Check(exporter.Contains("No blast runs since the toon pass")&&!exporter.Contains("run({kind:'blast'"),"the exporter records no source blast runs since the toon pass");
            using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/blast-parity.json"))))
            {var fx=doc.RootElement.GetProperty("owner").GetProperty("fx");Check(fx.GetProperty("date").GetString()=="2026-10-11"&&fx.GetProperty("smoke").GetBoolean()&&fx.GetProperty("seed").GetBoolean()&&fx.GetProperty("quakeDust").GetBoolean()&&fx.GetProperty("fuseTip").GetBoolean()&&fx.GetProperty("cuts").GetInt32()==8,"the parity frames carry the effects overhaul cuts");}
            return n+7;
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
            int n=0;Check(NativeBlastPool.Stride==25&&NativeBlastPool.RoleAt==13&&NativeBlastPool.AgeAt==14&&NativeBlastPool.SeedAt==15&&NativeBlastPool.VelocityAt==16&&NativeBlastPool.RawSizeAt==19&&NativeBlastPool.FloorAt==20&&NativeBlastPool.StrandAt==21&&NativeBlastPool.StartSizeAt==22&&NativeBlastPool.HexFromAt==23&&NativeBlastPool.HexToAt==24,"the draw list layout (the source's 0-12 first; toon pass: the spawn colours at 23 and 24)");
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
                        double raw=o[w+NativeBlastPool.RawSizeAt],edge=o[w+3];Check(o[w+NativeBlastPool.AgeAt]>=0&&o[w+NativeBlastPool.AgeAt]<=1&&(pool.Glow?Math.Abs(edge-raw)<1e-6:Math.Abs(edge-raw*Math.Min(1,(1-u)*4))<1e-5),"raw size before the opaque pool's end shrink");
                        // The start size is size0: the raw size lerps from it toward size1 (so raw = start at u 0).
                        Check(o[w+NativeBlastPool.StartSizeAt]>0&&o[w+NativeBlastPool.StrandAt]==pool.Strand(i)&&pool.Strand(i)==id.seed,"start size and a lone particle's own strand");
                        var hex=pool.Colors(i);Check(o[w+NativeBlastPool.HexFromAt]==hex.from&&o[w+NativeBlastPool.HexToAt]==hex.to&&hex.from>=0&&hex.from<0x1000000&&(int)(float)hex.from==hex.from,"the spawn colours, exact as floats");n+=5;
                    }
                    Check(drawn==pool.Drawn,"every drawn entry checked");
                }
            }
            return n;
        }

        // ---- strands: Bolt and Line segments never bridge two arcs, spokes or lines ----
        // The renderer's old rule (seed neighbours of one role within the join distance, no strand), to show what it joined.
        private static bool OldLinked(float[] o,int a,int b)
        {
            int role=(int)(o[a+NativeBlastPool.RoleAt]+.5f);if(role!=(int)(o[b+NativeBlastPool.RoleAt]+.5f)||role!=(int)NativeFxRole.Bolt&&role!=(int)NativeFxRole.Line)return false;
            bool bolt=role==(int)NativeFxRole.Bolt;int gap=(int)(o[b+NativeBlastPool.SeedAt]-o[a+NativeBlastPool.SeedAt]);if(gap<1||gap>(bolt?2:1))return false;
            double dx=o[b]-o[a],dy=o[b+1]-o[a+1],dz=o[b+2]-o[a+2],len=Math.Sqrt(dx*dx+dy*dy+dz*dz);return len>=1e-4&&len<=(bolt?NativeBlastPool.BoltJoin:NativeBlastPool.LineJoin);
        }
        // Joins between seed-sorted Bolt/Line points of a pool's draw list: within strands, across strands (must be 0), and what
        // the old rule would have bridged across strands.
        private static (int within,int across,int old,int strands) Joins(NativeBlastPool pool)
        {
            var o=pool.Output;var points=new List<int>();var strands=new HashSet<float>();
            for(int i=0;i<pool.Drawn;i++){int w=i*NativeBlastPool.Stride;var role=(NativeFxRole)(int)(o[w+NativeBlastPool.RoleAt]+.5f);if(role==NativeFxRole.Bolt||role==NativeFxRole.Line){points.Add(w);strands.Add(o[w+NativeBlastPool.StrandAt]);}}
            points.Sort((x,y)=>o[x+NativeBlastPool.SeedAt].CompareTo(o[y+NativeBlastPool.SeedAt]));int within=0,across=0,old=0;
            for(int k=0;k+1<points.Count;k++)
            {
                int a=points[k],b=points[k+1];bool same=o[a+NativeBlastPool.StrandAt]==o[b+NativeBlastPool.StrandAt];
                if(NativeBlastPool.Linked(o,a,b)){if(same)within++;else across++;}
                if(!same&&OldLinked(o,a,b))old++;
            }
            return (within,across,old,strands.Count);
        }
        private static int Strands()
        {
            int n=0;
            // Four arc spokes (an Arc event with no from point and no radius, NativeImpactCues.Plan): four Line strands.
            {
                var sim=new NativeBlastSim();var plan=new NativeImpactCommand[NativeImpactCues.MaxCommands];
                int count=NativeImpactCues.Plan(new NativeImpactEvent{Kind=EventKind.Arc,Position=new V3(0,1,0)},plan);NativeImpactCues.Run(plan,count,sim,null,1);
                int oldTotal=0,within=0;
                for(int f=0;f<6;f++){sim.Update(Dt);var j=Joins(sim.Glow);Check(j.across==0,"spokes: no segment between two spokes (frame "+f+")");oldTotal+=j.old;within+=j.within;if(f==0)Check(j.strands==4,"four spokes, four strands ("+j.strands+")");n++;}
                Check(oldTotal>0&&within>0,"the seed-only rule bridged the spokes ("+oldTotal+"); each spoke still joins its own points ("+within+")");n++;
            }
            // Ball Lightning (the violet Shock look, which draws the sky bolt and ground arcs) at R .8, 1.2, 2 and 3.
            foreach(double R in new[]{.8,1.2,2,3})
            {
                var sim=new NativeBlastSim();sim.Blast(NativeBlastStyle.Shock,0,.95,0,R,1,false,NativeBlastSim.TintOf("ball-lightning"),0);int oldTotal=0,within=0;
                for(int f=0;f<12;f++){sim.Update(Dt);var j=Joins(sim.Glow);Check(j.across==0,"ball lightning R "+R+": no segment between the bolt and an arc or two arcs (frame "+f+")");oldTotal+=j.old;within+=j.within;n++;}
                Check(within>0,"ball lightning R "+R+": the bolt and the arcs still join their own points");if(R==1.2)Check(oldTotal>0,"ball lightning R 1.2: the seed-only rule bridged arcs ("+oldTotal+")");n++;
            }
            // Strands are seeds: a line's points share its first seed, back-to-back lines differ, Clear restarts them.
            {
                var sim=new NativeBlastSim();sim.Line(0,1,0,2,1,0,0xffffff,0xb66cff,1);int first=sim.Glow.Count;sim.Line(2,1,0,4,1,0,0xffffff,0xb66cff,1);
                for(int i=0;i<sim.Glow.Count;i++)Check(sim.Glow.Strand(i)==(i<first?0:first),"line point "+i+" carries its line's first seed");
                var q=new NativeBlastPool(4,true);q.Spawn(0,0,0,0,0,0,1,.1,.1,0,0,strand:77,role:NativeFxRole.Line);q.Spawn(0,0,0,0,0,0,1,.1,.1,0,0,role:NativeFxRole.Line);Check(q.Strand(0)==77&&q.Strand(1)==1&&q.NextSeed==2,"an explicit strand, else the particle's own seed");
                q.Clear();Check(q.NextSeed==0,"Clear restarts the seeds strands come from");n+=3;
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

        // ---- the toon sheet (toon pass) ----
        private const ulong ToonHash=0x4587c2a0eec33824UL;
        private static int Toon(out double ms)
        {
            int n=0;var sw=Stopwatch.StartNew();var t=NativeVfxTextures.ToonSheet();sw.Stop();ms=sw.Elapsed.TotalMilliseconds;int W=NativeVfxTextures.ToonWidth,H=NativeVfxTextures.ToonHeight,T=NativeVfxTextures.ToonTile;
            Check(t.Length==W*H*4&&W==T*NativeVfxTextures.ToonColumns&&H==T*NativeVfxTextures.ToonRows&&T==128&&NativeVfxTextures.ToonColumns*NativeVfxTextures.ToonRows==32,"toon sheet layout: 8 x 4 tiles of 128 px");
            Check(NativeVfxTextures.Hash(NativeVfxTextures.ToonSheet())==NativeVfxTextures.Hash(t),"the toon sheet is deterministic");
            ulong hash=NativeVfxTextures.Hash(t);Check(hash==ToonHash,"pinned toon sheet hash (now 0x"+hash.ToString("x16")+")");n+=3;
            Check(NativeVfxTextures.FlashFirst==NativeVfxTextures.ToonPuffs&&NativeVfxTextures.CrescentFirst==NativeVfxTextures.FlashFirst+NativeVfxTextures.FlashFrames&&NativeVfxTextures.SpikeTile==NativeVfxTextures.CrescentFirst+NativeVfxTextures.Crescents&&NativeVfxTextures.DropTile==31,"tile plan: 16 puffs, 8 flash frames, 4 crescents, spike, lens, star, droplet");n++;
            // Texel (tile, x, y) in tile space (-1..1, y up): its channels.
            byte C(int tile,double x,double y,int ch){int px=(int)Math.Floor((x+1)/2*T),py=(int)Math.Floor((y+1)/2*T);px=Math.Max(0,Math.Min(T-1,px));py=Math.Max(0,Math.Min(T-1,py));int ox=(tile%8)*T,oy=(tile/8)*T;return t[((oy+py)*W+ox+px)*4+ch];}
            bool Inside(int tile,double x,double y)=>C(tile,x,y,0)>=128;
            for(int tile=0;tile<32;tile++)
            {
                // Every shape stays within ToonReach of its tile's centre (the quad spans .92 of the tile): no neighbour shows.
                int inside=0,outside=0,erosion=0;bool reach=true,clean=true;
                for(int py=0;py<T;py++)for(int px=0;px<T;px++)
                {
                    double x=(px+.5)/T*2-1,y=(py+.5)/T*2-1,r=Math.Sqrt(x*x+y*y);int w=(((tile/8)*T+py)*W+(tile%8)*T+px)*4;bool inn=t[w]>=128;
                    if(inn){inside++;if(r>NativeVfxTextures.ToonReach+.02)reach=false;if(tile<16&&t[w+2]==0)erosion++;}else{outside++;if(tile<16&&t[w+2]!=0)clean=false;}
                }
                Check(inside>40&&reach,"toon tile "+tile+": a shape inside its reach ("+inside+" texels)");Check(clean&&erosion==0,"toon tile "+tile+": the erosion field covers the puff and only the puff");n+=2;
                if(tile<16)
                {
                    // The hard shadow: some of a puff is shadowed (G below .5; lower right crescents and cast shadows), at least 40% of it lit.
                    int shadow=0,lit=0;for(int py=0;py<T;py+=2)for(int px=0;px<T;px+=2){int w=(((tile/8)*T+py)*W+(tile%8)*T+px)*4;if(t[w]<128)continue;if(t[w+1]<128)shadow++;else lit++;}
                    Check(shadow>8&&lit*5>(lit+shadow)*2,"toon puff "+tile+": a hard shadow band under a mostly lit body ("+shadow+" shadow, "+lit+" lit)");n++;
                }
            }
            // The erosion eats the puffs into blobs: at the start every puff texel survives, near the end only blob cores, at the end none.
            {
                int all=0,early=0,late=0,end=0;for(int tile=0;tile<16;tile++)for(int py=0;py<T;py++)for(int px=0;px<T;px++){int w=(((tile/8)*T+py)*W+(tile%8)*T+px)*4;if(t[w]<128)continue;all++;double b=t[w+2]/255.0;if(b>NativeVfxTextures.ToonCut(.2))early++;if(b>NativeVfxTextures.ToonCut(.85))late++;if(b>NativeVfxTextures.ToonCut(1))end++;}
                Check(early==all&&late>0&&late<all/3&&end==0,"toon erosion: whole at u .2 ("+early+"/"+all+"), blob cores at u .85 ("+late+"), gone at 1 ("+end+")");n++;
                Check(NativeVfxTextures.ToonCut(0)==0&&NativeVfxTextures.ToonCut(NativeVfxTextures.ToonEatFrom)==0&&NativeVfxTextures.ToonCut(1)>1,"the cut is 0 until ToonEatFrom and passes 1 at the end");n++;
            }
            // The round flash flipbook: frame 0 a full disc; a hole rising from below eats it (its lower middle is open by frame 4)
            // into a thin crescent at the top in the last frame.
            {
                double lowest=9;int last=0;for(int py=0;py<T;py++)for(int px=0;px<T;px++){double x=(px+.5)/T*2-1,y=(py+.5)/T*2-1;if(Inside(NativeVfxTextures.FlashFirst+7,x,y)){last++;lowest=Math.Min(lowest,y);}}
                Check(Inside(NativeVfxTextures.FlashFirst,0,0)&&Inside(NativeVfxTextures.FlashFirst,0,-.3)&&!Inside(NativeVfxTextures.FlashFirst+4,0,-.3)&&Inside(NativeVfxTextures.FlashFirst+4,0,.6)&&last>40&&lowest>.3,"the round flash: a disc eaten from below into a thin crescent (last frame "+last+" texels, lowest "+lowest.ToString("0.00")+")");n++;
            }
            Check(Inside(NativeVfxTextures.SpikeTile,.6,0)&&!Inside(NativeVfxTextures.SpikeTile,-.7,0)&&!Inside(NativeVfxTextures.SpikeTile,.6,.3),"the spike: an arrowhead toward +x with a concave back");
            Check(Inside(NativeVfxTextures.LensTile,0,0)&&Inside(NativeVfxTextures.LensTile,.7,0)&&!Inside(NativeVfxTextures.LensTile,0,.3),"the lens: long along x, thin across");
            Check(Inside(NativeVfxTextures.StarTile,0,.7)&&Inside(NativeVfxTextures.StarTile,.7,0)&&!Inside(NativeVfxTextures.StarTile,.5,.5),"the star: four points");
            Check(Inside(NativeVfxTextures.DropTile,.5,0)&&!Inside(NativeVfxTextures.DropTile,-.95,0)&&!Inside(NativeVfxTextures.DropTile,.5,.5),"the droplet: a round head toward +x");
            for(int c=0;c<NativeVfxTextures.Crescents;c++)Check(Inside(NativeVfxTextures.CrescentFirst+c,.7,0)&&!Inside(NativeVfxTextures.CrescentFirst+c,0,0),"crescent "+c+": bulging toward +x, hollow behind");
            n+=4+NativeVfxTextures.Crescents;
            // Distances decode back to tile units; the UV rectangles tile the sheet.
            Check(Math.Abs(NativeVfxTextures.ToonDistance(128)-(128/255.0-.5)*.5)<1e-12&&Math.Abs(NativeVfxTextures.ToonDistance(255)-NativeVfxTextures.ToonSdfRange)<1e-12,"stored distances decode to tile units");
            NativeVfxTextures.ToonRect(NativeVfxTextures.DropTile,out double u0,out double v0,out double u1,out double v1);Check(u0==.875&&v0==.75&&u1==1&&v1==1,"tile 31 is the top-right tile");n+=2;
            return n;
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
                double ring=1e9;for(int i=0;i<m.VertexCount;i++)if(Math.Abs(p[i*3+1])<.3)ring=Math.Min(ring,Math.Sqrt(p[i*3]*p[i*3]+p[i*3+2]*p[i*3+2]));
                Check(top==.5&&bottom==-.5&&hull.Count==8&&m.TriangleCount==12&&ring>=.44-1e-9,"the shard is a hexagonal bipyramid with tips at +-.5, its ring at least .44 out ("+ring+")");n+=Solid(m,m.Triangles,0,-.12,0,"crystal shard",true);
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
                    // The source body cube's x/z, spin and colour; its height stands the casing on the bomb's floor (owner cut).
                    Check(pose.X==body.X&&pose.Z==body.Z&&pose.Qx==body.Qx&&pose.Qy==body.Qy&&pose.Qz==body.Qz&&pose.Qw==body.Qw&&pose.Body==body.Color,"pose follows the source body cube");
                    Near(pose.Y-pose.Sy*NativeVfxMeshes.BombDiameter*.5,NativeFieldRules.BombBase(f)+NativeFieldRules.BombLift,1e-12,"the casing stands on its floor");
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
            NativeFieldRules.Bomb(g,0,out var p0);NativeFieldRules.Cubes(g,0x123456,0,cubes);Check(p0.Sx==1&&p0.Sy==1&&p0.FuseLit&&p0.Emissive>0&&Math.Abs(p0.FuseY-cubes[0].Y-.26)<1e-12&&Math.Abs(NativeVfxMeshes.BombTipY-.262)<1e-12,"at rest: scale 1, fuse lit, the tip .262 over the casing as the source's fuse cube sat .26 over its body");
            // On the floor (owner cut, review 2026-10-11): a field on a flat floor or a raised platform stands its bomb on it, the
            // tip above it; without a floor, or high above one, the emitters' ground (y - .95, at least .03) is the base.
            foreach(var (y,floor,expect) in new (double,double?,double)[]{(0,0,0),(.95,0,0),(2,2,2),(2.95,2,2),(.95,null,.03),(.5,null,.03),(5,0,4.05),(5,NativeBlastSim.NoFloor,4.05)})
            {
                var f=new NativeBlastField{Kind="bomb",X=1,Y=y,Z=2,Radius=3,Born=0,Until=4,Floor=floor};NativeFieldRules.Bomb(f,0,out var p);
                Near(NativeFieldRules.BombBase(f),expect,1e-12,"bomb base at y "+y+" floor "+floor);Near(p.Y-NativeVfxMeshes.BombDiameter*.5*p.Sy,expect+NativeFieldRules.BombLift,1e-12,"casing bottom at y "+y+" floor "+floor);
                double tip=p.Y+NativeVfxMeshes.BombTipY*p.Sy;Check(tip>expect+.4&&Math.Abs(NativeFieldRules.BombTipY(f)-tip)<1e-12,"the fuse tip is above the floor at y "+y+" floor "+floor+" ("+tip+")");
                // The fuse sparks start at that tip.
                var sim=new NativeBlastSim();f.Id=9;sim.Field(f,0,1);for(int i=1;i<40;i++)sim.Field(f,i*Dt,1);int sparks=0;
                for(int i=0;i<sim.Glow.Count;i++)if(sim.Glow.Identity(i).role==NativeFxRole.Spark){Near(sim.Glow.At(i).y,(float)NativeFieldRules.BombTipY(f),1e-5,"a fuse spark starts at the tip (y "+y+" floor "+floor+")");sparks++;}
                Check(sparks>0,"the bomb throws fuse sparks (y "+y+" floor "+floor+")");n+=4;
            }
            var mineField=new NativeBlastField{Kind="mine",X=0,Y=0,Z=0,Radius=3,Born=0,Until=4,Floor=0};NativeFieldRules.Bomb(mineField,1,out var mp);Near(mp.Y,.1,1e-12,"the flat mine sits on the floor: centre .08 + .02 up");
            double peak=0;for(double t=3;t<3.5;t+=.001){NativeFieldRules.Bomb(g,t,out var p);peak=Math.Max(peak,p.Emissive);}Check(peak>.95,"the glow peaks near full late in the fuse ("+peak+")");
            return n+5;
        }
    }
}
#endif
