#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
namespace ChipArena.UnityConversion
{
    // Effects toon pass (owner 2026-10-11, after the Asset Store pack study): the Unity-owned rules that replace Electron parity
    // for blasts (Tests/blast-pinned.json pins the frames themselves). Budgets (particles spawned and alive per blast, how long a
    // blast lasts, shock records), what each style's composition must read as (fire puffs from flame to slate, a ground-hugging
    // dust ring and an upright slam wave, a 3-4 m geyser column that falls back, acid blobs with puff trails, black shapes over a
    // violet halo, violet-only shocks with very short arcs), the water globe floor splash and its landing rule, and no flat
    // ground disc or ring. Pure Core checks; the look itself is judged in renders, not here.
    internal static class ToonFxChecks
    {
        private const double Dt=1.0/60;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Toon fx check failed: "+what);}
        private static readonly (string what,NativeBlastStyle style,string tag)[] Looks={
            ("Fire",NativeBlastStyle.Fire,null),("Mine",NativeBlastStyle.Mine,null),("Meteor",NativeBlastStyle.Meteor,null),("Kinetic",NativeBlastStyle.Kinetic,null),("frag",NativeBlastStyle.Kinetic,"frag"),
            ("tidal-wave",NativeBlastStyle.Kinetic,"tidal-wave"),("Frost",NativeBlastStyle.Frost,null),("geyser-wall",NativeBlastStyle.Frost,"geyser-wall"),("Toxic",NativeBlastStyle.Toxic,null),("Void",NativeBlastStyle.Void,null),
            ("Shock",NativeBlastStyle.Shock,null),("ball-lightning",NativeBlastStyle.Shock,"ball-lightning"),("Quake",NativeBlastStyle.Quake,null),("Nova",NativeBlastStyle.Nova,null),("Implosion",NativeBlastStyle.Implosion,null)};
        // Per look: at most this many particles spawned at radius 3 and at radius 6 (quality 1), and gone within this many seconds.
        private static readonly Dictionary<string,(int r3,int r6,double seconds)> Budget=new Dictionary<string,(int,int,double)>{
            {"Fire",(132,195,1.7)},{"Mine",(136,200,1.7)},{"Meteor",(172,250,1.7)},{"Kinetic",(124,180,1.7)},{"frag",(124,180,1.7)},{"tidal-wave",(106,156,1.4)},{"Frost",(120,175,2.6)},
            {"geyser-wall",(132,195,1.8)},{"Toxic",(220,342,3.2)},{"Void",(80,115,1.5)},{"Shock",(138,200,.6)},{"ball-lightning",(204,302,.6)},{"Quake",(94,138,1.8)},{"Nova",(120,178,.7)},{"Implosion",(118,172,1.4)}};
        private static int Count(NativeBlastPool pool,NativeFxRole role){int c=0;for(int i=0;i<pool.Count;i++)if(pool.Identity(i).role==role)c++;return c;}
        private static NativeBlastSim Make(NativeBlastStyle style,string tag=null,double R=3,double y=.95,double? floor=0){var sim=new NativeBlastSim();sim.Blast(style,0,y,0,R,1,false,NativeBlastSim.TintOf(tag),floor);return sim;}
        // A disc particle (the pancake): sliding flat (no vertical speed, no gravity) away from the centre, below the blast.
        private static int Flat(NativeBlastSim sim,double below){int d=0;foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Count;i++){var p=pool.At(i);var m=pool.Motion(i);if(m.vy==0&&m.gravity==0&&(m.vx!=0||m.vz!=0)&&p.y<below)d++;}return d;}
        /// <summary>What each look's composition must read as, the floor splash and its landing rule; returns the checks.</summary>
        public static int Run(string root)
        {
            int n=0;void C(bool ok,string what){Check(ok,what);n++;}
            // ---- no flat ground disc or ring, every look and tint (owner 2026-10-11) ----
            foreach(var (what,style,tag) in Looks)foreach(var (y,floor) in new (double,double?)[]{(.95,0),(.15,0),(3,0)})
            {var sim=Make(style,tag,3,y,floor);C(Flat(sim,Math.Max(.35,y)-.2)==0,what+" at "+y+" m: no particle slides flat over the floor");}
            // ---- Fire: a flash, toon fire puffs from flame to slate blown out from a ground circle, licks, hot debris, sparks ----
            {
                var sim=Make(NativeBlastStyle.Fire,null,3,.15);int puffs=0,ring=0;
                for(int i=0;i<sim.Solid.Count;i++){if(sim.Solid.Identity(i).role!=NativeFxRole.Fireball)continue;puffs++;var c=sim.Solid.Colors(i);C(c.from==NativeBlastSim.FireYellow&&c.to==NativeBlastSim.Slate,"fire puffs run from #FFCE00 to slate #3E4556");var p=sim.Solid.At(i);var m=sim.Solid.Motion(i);if(p.y<.4&&Math.Sqrt(m.vx*m.vx+m.vz*m.vz)>2*Math.Abs(m.vy))ring++;}
                C(puffs>=20&&ring>=12,"fire puffs: a ring blown out over the floor ("+ring+" of "+puffs+")");
                C(Count(sim.Glow,NativeFxRole.Flash)>=2&&Count(sim.Glow,NativeFxRole.Lick)>=5&&Count(sim.Solid,NativeFxRole.HotDebris)>=8&&Count(sim.Glow,NativeFxRole.Spark)>=15&&Count(sim.Glow,NativeFxRole.Halo)==1,"fire: flashes, crescent licks, hot debris, sparks and one halo");
                double far=0;for(int f=0;f<18;f++)sim.Update(Dt);for(int i=0;i<sim.Solid.Drawn;i++){int w=i*NativeBlastPool.Stride;var o=sim.Solid.Output;if((int)o[w+NativeBlastPool.RoleAt]!=(int)NativeFxRole.Fireball)continue;far=Math.Max(far,Math.Sqrt(o[w]*o[w]+o[w+2]*o[w+2]));}
                C(far>=1.6&&far<=4.5,"fire puffs reach about the radius by .3 s ("+far.ToString("0.00")+" m)");
            }
            // ---- Quake: a dust ring of ground-hugging puffs and an upright slam wave ----
            {
                var sim=Make(NativeBlastStyle.Quake,null,3,.15);double top=0,start=0,later=0;int dust=0;
                for(int i=0;i<sim.Solid.Count;i++)if(sim.Solid.Identity(i).role==NativeFxRole.Smoke){dust++;var p=sim.Solid.At(i);start+=Math.Sqrt(p.x*p.x+p.z*p.z);}
                for(int f=0;f<90;f++){sim.Update(Dt);double sum=0;int c=0;for(int i=0;i<sim.Solid.Drawn;i++){int w=i*NativeBlastPool.Stride;var o=sim.Solid.Output;if((int)o[w+NativeBlastPool.RoleAt]!=(int)NativeFxRole.Smoke)continue;top=Math.Max(top,o[w+1]);sum+=Math.Sqrt(o[w]*o[w]+o[w+2]*o[w+2]);c++;}if(f==20&&c>0)later=sum/c;}
                C(dust>=15&&top<2.6,"quake dust hugs the ground (highest "+top.ToString("0.00")+" m)");C(later>start/dust+.5,"quake dust is blown outward ("+(start/dust).ToString("0.00")+" -> "+later.ToString("0.00")+" m)");
                var s=Make(NativeBlastStyle.Quake).Shocks;C(s.Count==2&&s[1].Kind==NativeShockKind.Slam&&s[1].Height>=NativeBlastSim.SlamMinHeight&&!s[1].Dome,"quake: an upright slam wave, not a dome or a disc");
            }
            // ---- Geyser Wall: a 3-4 m water column that tops out quickly and falls back, spray, mist ----
            {
                var sim=Make(NativeBlastStyle.Frost,"geyser-wall",3,.15);double peak=0,peakAt=0,late=0;
                C(Count(sim.Solid,NativeFxRole.Water)>=30&&Count(sim.Glow,NativeFxRole.Droplet)>=25&&Count(sim.Solid,NativeFxRole.Mist)>=4&&Count(sim.Solid,NativeFxRole.Shard)==0,"geyser: water puffs, droplets and mist, no ice crystals");
                for(int f=1;f<=90;f++){sim.Update(Dt);double top=0;for(int i=0;i<sim.Solid.Drawn;i++){int w=i*NativeBlastPool.Stride;var o=sim.Solid.Output;if((int)o[w+NativeBlastPool.RoleAt]==(int)NativeFxRole.Water)top=Math.Max(top,o[w+1]);}if(top>peak){peak=top;peakAt=f*Dt;}if(f==75)late=top;}
                C(peak>=3&&peak<=4.4&&peakAt<=.8,"the geyser column tops out at 3-4 m within .8 s ("+peak.ToString("0.00")+" m at "+peakAt.ToString("0.00")+" s)");C(late<peak-.5,"the column falls back ("+late.ToString("0.00")+" m at 1.25 s)");
            }
            // ---- Tidal Wave: water, not dust and rubble ----
            {var sim=Make(NativeBlastStyle.Kinetic,"tidal-wave");C(Count(sim.Solid,NativeFxRole.Water)>=10&&Count(sim.Glow,NativeFxRole.Droplet)>=10&&Count(sim.Solid,NativeFxRole.Debris)==0&&Count(sim.Solid,NativeFxRole.Smoke)==0,"tidal wave: a water pop, no dust or rubble");}
            // ---- Frost: ice spikes, crystals, faint mist, lingering snow ----
            {var sim=Make(NativeBlastStyle.Frost);double snow=0;for(int i=0;i<sim.Glow.Count;i++)if(sim.Glow.Identity(i).role==NativeFxRole.Snow)snow=Math.Max(snow,sim.Glow.At(i).life);C(Count(sim.Glow,NativeFxRole.Spike)>=12&&Count(sim.Solid,NativeFxRole.Shard)>=10&&Count(sim.Solid,NativeFxRole.Mist)>=4&&snow>=1.4,"frost: spikes, crystals, mist and snow that lingers ("+snow.ToString("0.00")+" s)");}
            // ---- Toxic: dark green puffs, acid blobs trailing puffs, a lingering cloud ----
            {
                var sim=Make(NativeBlastStyle.Toxic,null,3.5);int trails=0;double first=9,last=0;
                for(int i=0;i<sim.Solid.Count;i++){var id=sim.Solid.Identity(i);var p=sim.Solid.At(i);if(id.role==NativeFxRole.Smoke&&p.age<0)trails++;if(id.role==NativeFxRole.Cloud){first=Math.Min(first,-p.age);last=Math.Max(last,-p.age);}}
                C(Count(sim.Solid,NativeFxRole.Blob)>=5&&trails>=10,"toxic: acid blobs leaving delayed puff trails ("+trails+")");C(last-first>=1,"toxic: the lingering cloud keeps coming for over a second ("+(last-first).ToString("0.00")+" s)");
            }
            // ---- Void: black shapes over a violet halo, inward streaks, ink threads ----
            {var sim=Make(NativeBlastStyle.Void);int ink=0;for(int i=0;i<sim.Solid.Count;i++)if(sim.Solid.Identity(i).role==NativeFxRole.Smoke&&sim.Solid.Colors(i).from==NativeBlastSim.VoidInk)ink++;
             C(Count(sim.Glow,NativeFxRole.Halo)==1&&ink>=6&&Count(sim.Solid,NativeFxRole.Dark)>=3&&Count(sim.Glow,NativeFxRole.Streak)>=16&&Count(sim.Glow,NativeFxRole.Droplet)>=8&&sim.Shocks.Count==0,"void: a violet halo, black ink and orbs, inward streaks, ink threads, no front");}
            // ---- Shock: violet only, a strobing glow, very short arcs ----
            {
                var sim=Make(NativeBlastStyle.Shock);int strobe=0;double longest=0;bool blue=false;
                foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Count;i++){var c=pool.Colors(i);blue|=NativeBlastSim.BlueCyan(c.from)||NativeBlastSim.BlueCyan(c.to);var id=pool.Identity(i);var p=pool.At(i);if(id.role==NativeFxRole.Bolt)longest=Math.Max(longest,p.life);if(id.role==NativeFxRole.Core&&p.life<.15)strobe++;}
                C(!blue,"shock: no blue or cyan anywhere");C(Count(sim.Glow,NativeFxRole.Bolt)>=20&&longest<=NativeBlastSim.CrackleLife+1e-9,"shock: arcs live at most "+NativeBlastSim.CrackleLife+" s ("+longest.ToString("0.000")+")");C(strobe>=4,"shock: a strobing glow ("+strobe+" pulses)");
                var cyan=Make(NativeBlastStyle.Shock,"blink-strike");C(Count(cyan.Glow,NativeFxRole.Bolt)==0,"blink strike (cyan): no arcs at all");
            }
            // ---- Implosion: the inrush, then the pop ----
            {var sim=Make(NativeBlastStyle.Implosion);bool late=true;for(int i=0;i<sim.Glow.Count;i++){var id=sim.Glow.Identity(i);if(id.role==NativeFxRole.Flash||id.role==NativeFxRole.Spark)late&=sim.Glow.At(i).age<=-NativeBlastSim.ImplosionPop+1e-6;}for(int i=0;i<sim.Solid.Count;i++)if(sim.Solid.Identity(i).role==NativeFxRole.Cloud)late&=sim.Solid.At(i).age<=-NativeBlastSim.ImplosionPop+1e-6;
             C(late&&Count(sim.Glow,NativeFxRole.Streak)>=16,"implosion: streaks rush in, the flash, puffs and sparks wait for the pop");}
            // ---- the floor splash and its landing rule ----
            {
                var sim=new NativeBlastSim();int spawned=sim.Splash(1,.5,-2,1,1);C(spawned>=20&&Count(sim.Solid,NativeFxRole.Water)>=7&&Count(sim.Glow,NativeFxRole.Droplet)>=12&&Count(sim.Solid,NativeFxRole.Mist)==1,"a splash: a crown of water puffs, droplets and a little mist ("+spawned+")");
                double low=9,high=0;for(int f=0;f<60;f++){sim.Update(Dt);foreach(var pool in new[]{sim.Solid,sim.Glow})for(int i=0;i<pool.Drawn;i++){double y=pool.Output[i*NativeBlastPool.Stride+1];low=Math.Min(low,y);high=Math.Max(high,y);}}
                C(low>=.5-1e-4&&high<.5+1.6&&sim.Solid.Count+sim.Glow.Count==0,"the splash stays on its floor, low, and is gone within a second (lowest "+low.ToString("0.000")+", highest "+high.ToString("0.00")+")");
                C(new NativeBlastSim().Splash(0,0,0,1,0)==0&&new NativeBlastSim().Splash(0,NativeBlastSim.NoFloor,0,1,1)==0,"no splash at quality 0 or without a floor");
                var a=new NativeBlastSim();var b=new NativeBlastSim();a.Splash(0,0,0,1.3,1);b.Splash(0,0,0,1.3,1);for(int f=0;f<20;f++){a.Update(Dt);b.Update(Dt);}bool same=a.Glow.Drawn==b.Glow.Drawn&&a.Solid.Drawn==b.Solid.Drawn;for(int i=0;same&&i<a.Glow.Drawn*NativeBlastPool.Stride;i++)same=a.Glow.Output[i]==b.Glow.Output[i];C(same,"the splash is deterministic");
                double G=10,r=NativeWaterSplash.GlobeRadius;
                C(NativeWaterSplash.Landing(2,r+.1,3,8,-1,0,G,r,0,out double x,out double z,out double t)&&x==2&&z==3&&t==0,"a shot already touching its floor landed where it was");
                C(NativeWaterSplash.Landing(0,1,0,20,-5,10,G,r,0,out x,out z,out t)&&Math.Abs(t-(-5+Math.Sqrt(25+2*G*(1-r)))/G)<1e-12&&Math.Abs(x-20*t)<1e-12&&Math.Abs(z-10*t)<1e-12&&t<=NativeWaterSplash.Lookahead,"a falling shot lands where its arc meets the floor within the lookahead (t "+t.ToString("0.000")+")");
                C(!NativeWaterSplash.Landing(0,3,0,20,0,0,G,r,0,out _,out _,out _),"a shot that vanished high above the floor (out of range, hit a pilot) draws no splash");
                C(!NativeWaterSplash.Landing(0,1,0,0,-5,0,G,r,NativeBlastSim.NoFloor,out _,out _,out _)&&!NativeWaterSplash.Landing(0,-1,0,0,-5,0,G,r,0,out _,out _,out _),"no splash without a floor or from under it");
                C(NativeWaterSplash.Landing(0,1,0,0,-12,0,0,r,0,out _,out _,out t)&&Math.Abs(t-(1-r)/12)<1e-12&&!NativeWaterSplash.Landing(0,1,0,0,3,0,0,r,0,out _,out _,out _),"without gravity a shot lands only while it falls");
                C(NativeWaterSplash.NearPath(0,1,0,4,0,0,2,1.2,.3)&&!NativeWaterSplash.NearPath(0,1,0,4,0,0,2,3,0),"a hit within Clear of the path is the shot's own");
                C(NativeWaterSplash.Size(r)==1&&NativeWaterSplash.Size(.05)==.7&&NativeWaterSplash.Size(1)==1.6,"splash size from the shot radius");
            }
            // The presentation tracks water shots and splashes them (text: Runtime is not compiled here).
            {
                string present=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeBlastPresentation.cs"));
                C(present.Contains("Globes(arena,now,q);Fields(fields,q);")&&present.Contains("NativeWaterSplash.Landing(")&&present.Contains("sim.Splash(x,floor,z,NativeWaterSplash.Size(g.Radius),quality);")&&present.Contains("if(b.Spec!=\"water\"||")&&present.Contains("if(HitNear(arena,now,g.Position,at))continue;")&&present.Contains("globes.Clear();"),"NativeBlastPresentation splashes water shots that vanish at their floor, not those that hit something, and forgets them on a rewind");
            }
            return n;
        }
        /// <summary>Spawn and duration budgets per look and the peak a crowd of blasts reaches; returns the number of checks.</summary>
        public static int Budgets()
        {
            int n=0;var report=new System.Text.StringBuilder();
            foreach(var (what,style,tag) in Looks)
            {
                var b=Budget[what];int r3=new NativeBlastSim().Blast(style,0,.95,0,3,1,false,NativeBlastSim.TintOf(tag),0),r6=new NativeBlastSim().Blast(style,0,.95,0,6,1,false,NativeBlastSim.TintOf(tag),0);
                var sim=new NativeBlastSim();sim.Blast(style,0,.95,0,3,1,false,NativeBlastSim.TintOf(tag),0);double t=0;int peak=0;
                while(sim.Solid.Count+sim.Glow.Count>0&&t<10){sim.Update(Dt);t+=Dt;peak=Math.Max(peak,sim.Solid.Drawn+sim.Glow.Drawn);}
                report.Append(what).Append(": r3 ").Append(r3).Append(", r6 ").Append(r6).Append(", ").Append(t.ToString("0.00")).Append(" s, peak ").Append(peak).Append('\n');
                Check(r3<=b.r3&&r6<=b.r6,what+" spawns at most "+b.r3+" / "+b.r6+" particles at r 3 / 6 ("+r3+" / "+r6+")");
                Check(t<=b.seconds,what+" is gone within "+b.seconds+" s ("+t.ToString("0.00")+")");n+=2;
            }
            if(Environment.GetEnvironmentVariable("CHIP_TOON_REPORT")=="1")Console.Write(report);
            return n;
        }
    }
}
#endif
