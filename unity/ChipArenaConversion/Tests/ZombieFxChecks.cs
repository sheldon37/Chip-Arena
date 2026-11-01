#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The spitter's acid and the heavy kinds' impacts, high-quality pass (owner 2026-10-14: "Improve visual fx from spitter/hulk
    // zombie (make them high quality) acid needs to look better. Slam/landing/wall break [effects] also needs improvements from
    // hulk/brute"). Presentation only: the shapes keep to the authority's sizes and times (the glob on the validated arc, the
    // pool inside NativeSpitterRules.Radius for NativeSpitterRules.Duration, every impact inside the smash disc or the drop's slam
    // radius, on the strike or the touchdown), the tiers within their budgets, deterministic, no allocation, and a runtime that
    // only draws, through the shared renderer, its streams thinned by tier.
    internal static class ZombieFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie fx: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static float Flat(Vector3 v)=>new Vector2(v.X,v.Z).Length();
        private static readonly NativeZombieFx.Impact[] Impacts={NativeZombieFx.Smash(NativeHordeKinds.Brute),NativeZombieFx.Smash(NativeHordeKinds.Hulk),NativeZombieFx.Landing(NativeHordeDrop.Brute),NativeZombieFx.Landing(NativeHordeDrop.Hulk),NativeZombieFx.Crash};
        public static void Run(string root)
        {
            Glob();Windup();Pool();NaturalPool();SpitEvents();Corrosion();ImpactSizes();ImpactTiming();Breach();Tiers();Determinism();Allocation();Runtime(root);
            Console.WriteLine("PASS zombie fx ("+count+" checks: the glob on the authority's arc, the pool's lobed body on its radius for its duration with no ring, deterministic per seed, splash drops landing inside it, satellites outside it, the spit's events lodging no arrow, impacts inside the smash disc and the slam radius from the strike and the touchdown, breach chips, dust and remnants on the breach clock, tier budgets, deterministic, no allocation, a draw-only runtime)");
        }
        // ---- the glob: on the validated arc, its tail behind it, its drips falling from it ----
        private static void Glob()
        {
            var from=new Vector3(0,1.45f,0);
            foreach(float d in new[]{1.5f,4f,8f,NativeSpitterRules.Range})
            {
                var to=new Vector3(d*.6f,.025f,d*.8f);float flight=NativeHordeFx.Flight(Flat(to-from));
                for(float t=0;t<=flight;t+=flight/60)
                {
                    NativeHordeFx.Glob(from,to,flight,t,out var p);var v=NativeZombieFx.GlobVelocity(from,to,flight,t);
                    float e=1e-3f;if(t>e&&t<flight-e){NativeHordeFx.Glob(from,to,flight,t-e,out var a);NativeHordeFx.Glob(from,to,flight,t+e,out var b);Check(Vector3.Distance((b-a)/(2*e),v)<.05f*Math.Max(1,v.Length()),"the glob's velocity is its arc's ("+d+" m)");}
                    NativeZombieFx.GlobShape(5,t,v.Length(),out float along,out float across);Check(along>=.7f&&along<=1.75f&&Math.Abs(along*across*across-1)<1e-3f,"its viscous stretch keeps its volume");
                    for(int k=0;k<NativeZombieFx.TailBeads;k++)if(NativeZombieFx.TailBead(from,to,flight,t,k,out var bp,out float share))
                    {float lag=.016f*(k+1)+.003f*k*k;NativeHordeFx.Glob(from,to,flight,t-lag,out var q);Check(Vector3.Distance(bp,q)<1e-5f&&share>0&&share<.62f,"the tail beads ride the arc behind it");}
                    long k0=(long)Math.Floor((t-NativeZombieFx.FumeLife)*NativeZombieFx.FumeRate),k1=(long)Math.Floor(t*NativeZombieFx.FumeRate);int fumes=0;
                    for(long k=Math.Max(0,k0);k<=k1;k++)if(NativeZombieFx.GlobFume(from,to,flight,5,k,t,out _,out float fs,out float fa,out float fu)){fumes++;Check(fs>0&&fa>=0&&fa<=.55f&&fu>=0&&fu<1,"its fumes are small and short");}
                    Check(fumes<=(int)(NativeZombieFx.FumeRate*NativeZombieFx.FumeLife)+2,"a bounded fume stream ("+fumes+")");
                }
                Check(!NativeZombieFx.TailBead(from,to,flight,flight+.01f,0,out _,out _),"no tail after the landing");
                for(int i=0;i<NativeZombieFx.Drips;i++){bool seen=false;for(float t=0;t<flight+1;t+=.01f)if(NativeZombieFx.Drip(from,to,flight,7,i,t,out var p,out var v)){if(!seen){float leave=t;NativeHordeFx.Glob(from,to,flight,leave,out var q);Check(Vector3.Distance(p,q)<.12f,"a drip leaves from the glob");}seen=true;Check(v.Y<=.3f*NativeZombieFx.GlobVelocity(from,to,flight,0).Y+1e-3f&&p.Y>=Math.Min(from.Y,to.Y)-.03f,"it falls, never through the floor");}Check(seen,"every drip falls");}
            }
        }
        // ---- the wind-up: the clot and the strings only through the wind-up; the glob takes over at the release ----
        private static void Windup()
        {
            float S=NativeSpitterRules.Hit;int clots=0;
            for(float a=-.1f;a<S+.6f;a+=.01f)
            {
                if(NativeZombieFx.MouthGlob(3,a,S,out float r,out float bulge,out float glow)){clots++;Check(a<S&&r>0&&r<=.075f&&bulge<.05f&&glow<=1,"the clot gathers in the mouth before the release only");}
                float jaw=NativeHordeJaw.Open(NativeHordeKinds.Spitter,NativeHordeKinds.Attack.Spit,a,S,float.NaN,float.NaN,a,3);
                for(int k=0;k<NativeZombieFx.MouthStrings;k++)if(NativeZombieFx.MouthString(3,k,a,S,jaw,out var t,out var b,out _,out float w))Check(a<S+.05f&&t.Y>b.Y&&w>0&&w<.01f&&Math.Abs(t.X)<.07f,"strings span the jaw in the wind-up");
            }
            Check(clots>40,"the clot is seen through the wind-up");Check(!NativeZombieFx.MouthGlob(3,S,S,out _,out _,out _),"at the release it is the launched glob");
            int snapped=0;for(int k=0;k<NativeZombieFx.MouthStrings;k++)if(!NativeZombieFx.MouthString(3,k,S*.9f,S,.98f,out _,out _,out _,out _))snapped++;Check(snapped==NativeZombieFx.MouthStrings,"a wide jaw snaps the strings");
        }
        // ---- the pool: on the authority's radius, ending with its duration ----
        private static void Pool()
        {
            float D=NativeSpitterRules.Duration;
            foreach(float Rad in new[]{NativeSpitterRules.Radius,NativeSpitterRules.DirectRadius,2.5f})foreach(uint seed in new uint[]{1,77,4000000001})
            {
                for(float t=-.1f;t<D+NativeZombieFx.EtchSeconds+.5f;t+=.02f)
                {
                    bool live=NativeZombieFx.Pool(t,D,out float spread,out float wet);Check(live==(t>=0&&t<=D),"the pool lives exactly its duration");
                    if(live){Check(spread<=1&&spread>0&&wet>=0&&wet<=1,"its spread and wetness are shares");for(int k=0;k<NativeZombieFx.PoolEdge;k++){float e=NativeZombieFx.EdgeAt(seed,k*Tau/NativeZombieFx.PoolEdge,t,D);Check(e>0&&e<=1+NativeZombieFx.Lobes+1e-5f,"its liquid never passes its widest lobe");}}
                    for(int i=0;i<NativeZombieFx.PoolBubbles;i++)if(NativeZombieFx.PoolBubble(seed,i,t,D,Rad,out var o,out float s,out float pop))Check(live&&Flat(o)+s<=Rad&&pop>=0&&pop<=1,"bubbles inside the pool");
                    for(int i=0;i<NativeZombieFx.PoolFumes;i++)if(NativeZombieFx.PoolFume(seed,i,t,D,Rad,out var o,out float s,out float a,out float u))Check(t<=D+.5f&&Flat(o)<=Rad&&a<=.1f&&u>=0&&u<1,"faint fumes rise from inside it and clear");
                    for(int i=0;i<NativeZombieFx.Glints;i++)if(NativeZombieFx.Glint(seed,i,t,D,Rad,out var o,out float ga))Check(live&&Flat(o)<=Rad&&ga<=.45f,"soft glints on the film");
                    if(NativeZombieFx.Etch(t,D,out float etch))Check(etch<=NativeZombieFx.StainMax&&t>=D-NativeZombieFx.EvaporateSeconds&&t<=D+NativeZombieFx.EtchSeconds,"the residue stain shows as it dries and fades after");
                }
                for(int k=0;k<NativeZombieFx.PoolEdge;k++){float e=NativeZombieFx.EtchEdge(seed,k*Tau/NativeZombieFx.PoolEdge,D);Check(e>=1-NativeZombieFx.Lobes-.02f&&e<=1+NativeZombieFx.Lobes,"the stain keeps the body's outline");}
                // Every splash drop lands inside the pool, and flies inside it.
                for(int i=0;i<NativeZombieFx.SplashDrops;i++)
                {
                    var at=NativeZombieFx.SpatterAt(seed,i,Rad);Check(at.Length()<=Rad*.9f+1e-4f,"a splash drop lands inside the radius");float land=NativeZombieFx.SplashLand(seed,i,Rad);Check(land>.05f&&land<1.2f,"and soon ("+land+" s)");
                    for(float t=0;t<land+.1f;t+=.01f)if(NativeZombieFx.SplashDrop(seed,i,t,Rad,out var o,out _,out float s))Check(t<=land+1e-4f&&Flat(o)<=Rad*.9f+1e-3f&&o.Y>=0,"it flies over the pool");
                    Check(!NativeZombieFx.SplashDrop(seed,i,land+.01f,Rad,out _,out _,out _),"and is gone once it has landed (its spatter takes over)");
                }
                for(int k=0;k<NativeZombieFx.CrownLobes;k++)for(float t=0;t<.4f;t+=.01f)if(NativeZombieFx.Crown(seed,k,t,out var o,out float s,out _))Check(t<=NativeZombieFx.CrownSeconds&&Flat(o)+s<=Math.Max(.6f,Rad),"the crown is a small burst at the landing");
                // The satellites: past the widest lobe, not far, landing soon; drying before the body.
                for(int i=0;i<NativeZombieFx.Satellites;i++)
                {
                    NativeZombieFx.Satellite(seed,i,Rad,out var at,out var along,out float length,out float width,out float land);float d=at.Length();
                    Check(d>Rad*(1+NativeZombieFx.Lobes)&&d<=Rad*1.35f&&Math.Abs(along.Length()-1)<1e-4f&&length>0&&length<.2f&&width>0&&width<=length,"a satellite splat lies just outside the body ("+d/Rad+" of the radius)");
                    Check(land>.05f&&land<1.2f&&!NativeZombieFx.SatelliteDrop(seed,i,land+.01f,Rad,out _,out _,out _)&&NativeZombieFx.SatelliteDrop(seed,i,land*.5f,Rad,out var o,out _,out _)&&Flat(o)<d,"its drop flies out and lands");
                }
                Check(NativeZombieFx.SatelliteWet(0,D)==1&&NativeZombieFx.SatelliteWet(D*.8f,D)==0,"satellites dry well before the body");
            }
            // The presentation's pool is the authority's: the records carry no radius, so it is the rule's.
            Check(NativeSpitterRules.Radius==1.3f&&NativeSpitterRules.Duration==4&&NativeSpitterRules.DirectRadius==.6f,"the rules' pool (1.3 m, 4 s, .6 m direct) as drawn");
        }
        private const float Tau=(float)(Math.PI*2);
        // ---- the natural pool (owner 2026-10-14: "make the pool look more natural (No circle showing either, currently theres a
        // green circle)"): a lobed outline, its own per seed, whose mean extent is the radius and whose lobes stay within the
        // tolerance; viscous spread; uneven thickness; drying ----
        private static void NaturalPool()
        {
            float D=NativeSpitterRules.Duration,evap=NativeZombieFx.EvaporateSeconds;
            Check(NativeZombieFx.Lobes>0&&NativeZombieFx.Lobes<=.12f,"the lobes swing within ±12% of the radius");
            float widest=0;
            for(uint seed=1;seed<=400;seed++)
            {
                foreach(float t in new[]{0f,.5f,1.5f,3f})foreach(int n in new[]{NativeZombieFx.PoolEdgeLow,NativeZombieFx.PoolEdge,60})
                {
                    double sum=0;float lo=9,hi=0;
                    for(int k=0;k<n;k++){float o=NativeZombieFx.Outline(seed,k*Tau/n,t);sum+=o;lo=Math.Min(lo,o);hi=Math.Max(hi,o);}
                    Check(Math.Abs(sum/n-1)<1e-4,"the outline's mean extent is the authority's radius (seed "+seed+", "+n+" points: "+sum/n+")");
                    Check(lo>=1-NativeZombieFx.Lobes-1e-5f&&hi<=1+NativeZombieFx.Lobes+1e-5f,"every lobe within the tolerance");
                    Check(hi-lo>=.05f,"no clean circle: the outline is lobed (seed "+seed+", swing "+(hi-lo)+")");widest=Math.Max(widest,hi-lo);
                }
                // Deterministic per seed (host, guests and the killcam draw the same pool); each record its own outline.
                float diff=0;for(int k=0;k<36;k++){float a=k*Tau/36;Check(NativeZombieFx.Outline(seed,a,1.2f)==NativeZombieFx.Outline(seed,a,1.2f)&&NativeZombieFx.EdgeAt(seed,a,2,D)==NativeZombieFx.EdgeAt(seed,a,2,D),"the outline is a pure function of the seed and the clock");diff=Math.Max(diff,Math.Abs(NativeZombieFx.Outline(seed,a,1.2f)-NativeZombieFx.Outline(seed+1,a,1.2f)));}
                Check(diff>.01f,"another seed lies another outline");
                // Spread to the radius, viscous: the lobes creep out and arrive by SpreadSeconds; the body's mean extent is then the radius
                // until it starts to dry; drying recedes it a little, unevenly.
                float prev=0;for(float t=0;t<=NativeZombieFx.SpreadSeconds+.01f;t+=.05f){double m=0;for(int k=0;k<NativeZombieFx.PoolEdge;k++)m+=NativeZombieFx.Reach(seed,k*Tau/NativeZombieFx.PoolEdge,t);m/=NativeZombieFx.PoolEdge;Check(m>=prev-1e-6f&&m>=NativeZombieFx.SpreadStart-1e-6f,"the liquid only creeps outward");prev=(float)m;if(Math.Abs(t-.2f)<.01f)Check(m<.86f,"and slowly ("+m+" of its outline at .2 s)");}
                for(int k=0;k<NativeZombieFx.PoolEdge;k++)Check(NativeZombieFx.Reach(seed,k*Tau/NativeZombieFx.PoolEdge,NativeZombieFx.SpreadSeconds)==1,"every lobe has arrived by SpreadSeconds");
                foreach(float t in new[]{NativeZombieFx.SpreadSeconds,1.8f,D-evap})
                {double m=0;for(int k=0;k<NativeZombieFx.PoolEdge;k++)m+=NativeZombieFx.EdgeAt(seed,k*Tau/NativeZombieFx.PoolEdge,t,D);Check(Math.Abs(m/NativeZombieFx.PoolEdge-1)<1e-4,"wet, the body's mean extent is the radius ("+m/NativeZombieFx.PoolEdge+" at "+t+" s)");}
                for(int k=0;k<NativeZombieFx.PoolEdge;k++){float a=k*Tau/NativeZombieFx.PoolEdge,r=NativeZombieFx.Recede(seed,a,0);Check(NativeZombieFx.Recede(seed,a,1)==0&&r>0&&r<=.22f+1e-6f,"drying recedes it a little, unevenly");}
                // Uneven thickness: a deep core, a thin lip; drying thins it; the sheen fades with the wet.
                double core=0,lip=0,dried=0;for(int k=0;k<24;k++){float a=k*Tau/24;core+=NativeZombieFx.Thickness(seed,.1f,(float)Math.Cos(a)*.13f,(float)Math.Sin(a)*.13f,1.5f,1);lip+=NativeZombieFx.Thickness(seed,.96f,(float)Math.Cos(a)*1.25f,(float)Math.Sin(a)*1.25f,1.5f,1);dried+=NativeZombieFx.Thickness(seed,.5f,(float)Math.Cos(a)*.65f,(float)Math.Sin(a)*.65f,3.9f,.05f);}
                Check(core/24>.7&&lip/24<.4&&core>lip,"thick and deep in its core, thin at its lip");Check(dried/24<.3,"drying thins it into patches");
                for(int k=0;k<24;k++){float a=k*Tau/24;Check(NativeZombieFx.Sheen(seed,.7f,a,0)==0&&NativeZombieFx.Sheen(seed,1,a,1)<.05f,"no sheen once dry, and none drawn along the very edge (no bright outline)");}
            }
            Check(widest<=2*NativeZombieFx.Lobes+1e-4f,"the outline never swings past its tolerance");
            for(float u=0;u<=1;u+=.05f){float f=NativeZombieFx.Fresnel(u);Check(f>=.1f-1e-6f&&f<=1,"a soft sheen, stronger at grazing views");}
            Check(NativeZombieFx.Fresnel(.2f)>NativeZombieFx.Fresnel(.9f),"stronger at grazing views");
            // Bubbles now and then: a pop at most every couple of seconds per slot, and a Reduced pool has half the slots.
            int pops=0;bool[] was=new bool[NativeZombieFx.PoolBubbles];
            for(float t=0;t<=D;t+=1/120f)for(int i=0;i<NativeZombieFx.PoolBubbles;i++){bool p=NativeZombieFx.PoolBubble(9,i,t,D,1.3f,out _,out _,out float pop)&&pop>0;if(p&&!was[i])pops++;was[i]=p;}
            Check(pops>=2&&pops<=NativeZombieFx.PoolBubbles*3,"bubbles pop occasionally ("+pops+" over the pool's life)");
            Check(NativeZombieFx.Count(NativeZombieFx.PoolBubbles,NativeZombieFx.Reduced)<NativeZombieFx.PoolBubbles&&NativeZombieFx.Count(NativeZombieFx.Satellites,NativeZombieFx.Reduced)<NativeZombieFx.Satellites&&NativeZombieFx.Count(NativeZombieFx.PoolFumes,NativeZombieFx.Low)<NativeZombieFx.PoolFumes,"Reduced effects and the low tier: fewer bubbles, satellites and fumes");
            Check(NativeZombieFx.PoolEdgeLow<NativeZombieFx.PoolEdge&&NativeZombieFx.PoolEdgeLow>=16,"the low tier draws a coarser outline (its mean checked above)");
        }
        // ---- the spit's events (owner 2026-10-14: "their pool produces an arrow in the middle"): an add's Impact events carry
        // its owner as Source (the DEV who spawned a DEV monster); none of them is a bow arrow or a weapon's impact ----
        private static void SpitEvents()
        {
            var spit=new ArenaEvent{Id=41,Kind=EventKind.Impact,Source=3,Target=10007,Position=new V3(0,.025f,5),From=new V3(0,1.2f,10),Amount=1,Detail="minion-spit",Radius=NativeSpitterRules.Radius};
            foreach(var d in new[]{"minion-spit","minion-spit-cancel","minion-drop","minion-slam"}){var e=spit;e.Detail=d;Check(!NativeBowWire.Lodges(e,"bow"),"a bow DEV's add's "+d+" lodges no arrow");Check(NativeImpactCues.AddEvent(d),d+" is an add's own event");}
            var arrow=new ArenaEvent{Kind=EventKind.Impact,Source=3,Projectile=12,Detail="bow-full"};Check(NativeBowWire.Lodges(arrow,"bow"),"an arrow's own prop impact still lodges");
            arrow.Detail=null;Check(NativeBowWire.Lodges(arrow,"bow"),"so does a projectile impact with no detail");arrow.Detail="bit-bolt";Check(!NativeBowWire.Lodges(arrow,"bow"),"a non-arrow projectile lodges none");
            arrow.Detail="volley";Check(!NativeBowWire.Lodges(arrow,"rifle"),"only a bow pilot's");arrow.Projectile=0;Check(!NativeBowWire.Lodges(arrow,"bow"),"an Impact naming no projectile lodges none");
            Check(!NativeImpactCues.AddEvent(null)&&!NativeImpactCues.AddEvent("bow")&&!NativeImpactCues.AddEvent("bit-bolt"),"a pilot's own impacts are not an add's");
            var plan=new NativeImpactCommand[NativeImpactCues.MaxCommands];
            foreach(var d in new[]{"minion-spit","minion-spit-cancel"})Check(NativeImpactCues.Plan(new NativeImpactEvent{Kind=EventKind.Impact,Detail=d,Position=spit.Position,From=spit.From,Trails=true},plan)==0,"the "+d+" notification draws no streak or sparks at the landing");
            Check(NativeImpactCues.Plan(new NativeImpactEvent{Kind=EventKind.Impact,Detail="minion-drop",Position=spit.Position,From=spit.Position},plan)>0,"a landing's impact keeps its burst");
        }
        // ---- corrosion: on the face, through the acid's time and a fade ----
        private static void Corrosion()
        {
            float D=NativeSpitterRules.Duration;
            for(float t=-.1f;t<D+2;t+=.02f)
            {
                bool on=NativeZombieFx.Blotch(t,D,.32f,out float reach,out float heat,out float a);Check(on==(t>=0&&t<=D+NativeHordeFx.CorrodeFade),"the blotch lasts the acid's time and its fade");
                if(on)Check(reach<=.32f&&heat>=0&&heat<=1&&a<=.8f,"it stays round the contact, its rim cooling");
                for(int i=0;i<NativeHordeFx.CorrodeDrips;i++)if(NativeZombieFx.FaceDrip(9,i,t,D,.32f,out float x,out float run,out float sp,out _))Check(Math.Abs(x)<=.25f&&run<=.8f&&sp>=0,"drips run down the face, then stop");
                for(int i=0;i<NativeZombieFx.BodyDrips;i++)if(NativeZombieFx.BodyDrip(9,i,t,1.8f,out float down,out _,out _))Check(t<=1.8f&&down<=.9f,"a splashed pilot's drips run down a moment");
                for(int i=0;i<NativeZombieFx.BodyDrops;i++)if(NativeZombieFx.BodyDrop(9,i,t,1.2f,out var o,out _,out _))Check(o.Y>=-1.2f,"thrown drops fall to the feet, no further");
            }
            for(int k=0;k<NativeZombieFx.Edge;k++){float e=NativeZombieFx.BlotchEdge(3,k);Check(e>.4f&&e<=1,"a ragged edge inside its reach");}
        }
        // ---- impacts: the authority's radius, nothing past it on the floor ----
        private static void ImpactSizes()
        {
            Check(Impacts[0].Radius==NativeHordeKinds.SmashRadiusOf(NativeHordeKinds.Brute)&&Impacts[1].Radius==NativeHordeKinds.SmashRadiusOf(NativeHordeKinds.Hulk),"the smashes' radius is the authority's disc");
            Check(Impacts[2].Radius==NativeHordeDrop.Brute.SlamRadius&&Impacts[3].Radius==NativeHordeDrop.Hulk.SlamRadius&&Impacts[3].Heavy&&Impacts[3].Crater&&!Impacts[2].Heavy,"the landings' radius is the drop profile's slam");
            foreach(var p in Impacts)foreach(uint seed in new uint[]{2,991,3000000017})
            {
                float life=NativeZombieFx.Life(p),most=0;
                for(float t=-.05f;t<life+.2f;t+=.01f)
                {
                    for(int w=0;w<2;w++)if(NativeZombieFx.Wave(p,w,t,out float r,out float h,out float pr,out float f)){Check(r<=p.Radius+1e-4f&&h>0&&h<=p.WaveHeight+1e-4f&&f>=0&&f<=1&&pr>=0&&pr<=1,"the slam wave stays inside the radius");if(w==0)most=Math.Max(most,r);}
                    for(int i=0;i<p.Cracks;i++){for(int j=0;j<=NativeZombieFx.CrackSegments;j++){var c=NativeZombieFx.CrackPoint(seed,i,p.Cracks,j,t,p,out float cw);Check(c.Length()<=p.Radius+1e-4f&&cw>0&&cw<.15f,"cracks run inside the radius");}
                        for(int b=0;b<NativeZombieFx.Branches;b++){NativeZombieFx.CrackBranch(seed,i,p.Cracks,b,t,p,out var a,out var c,out _);Check(a.Length()<=p.Radius+1e-4f&&c.Length()<=p.Radius+1e-4f,"and so do their branches");}}
                    for(int i=0;i<p.Dust;i++)if(NativeZombieFx.HugDust(seed,i,p.Dust,t,p,out var o,out float s,out float a,out float u))Check(t<=p.DustLife&&Flat(o)+.4f*s<=p.Radius+1e-4f&&o.Y<1.2f*Math.Sqrt(p.Radius/1.6f)+.2f&&a<=.92f,"the dust rolls out low, its body inside the radius");
                    for(int i=0;i<p.Column;i++)if(NativeZombieFx.ColumnDust(seed,i,t,p,out var o,out _,out _,out _))Check(Flat(o)<=p.Radius*.12f+1e-4f,"the column rises out of the strike");
                    for(int i=0;i<p.Debris;i++)if(NativeZombieFx.Chunk(seed,i,p.Debris,t,p,out var o,out var ax,out _,out float s,out float left))Check(Flat(o)<=p.Radius+1e-4f&&o.Y>=s*.5f-1e-4f&&Math.Abs(ax.Length()-1)<1e-3f&&left>=0&&left<=1,"debris flies and lands inside the radius, never through the floor");
                    for(int i=0;i<p.Sparks;i++)if(NativeZombieFx.Spark(seed,i,t,p,out var o,out _,out float heat))Check(o.Y>=.01f-1e-5f&&heat>=0&&heat<=1&&t<.5f,"sparks are short");
                }
                Check(most>=p.Radius*.99f,"the wave reaches the radius ("+most+" of "+p.Radius+")");
                // Debris comes to rest: two bounces, then still.
                for(int i=0;i<p.Debris;i++){NativeZombieFx.Chunk(seed,i,p.Debris,p.DebrisLife-.55f,p,out var a,out _,out _,out float s,out _);NativeZombieFx.Chunk(seed,i,p.Debris,p.DebrisLife-.5f,p,out var b,out _,out _,out _,out _);Check(Vector3.Distance(a,b)<.01f&&Math.Abs(a.Y-s*.5f)<1e-3f,"debris rests on the floor before it sinks away");}
            }
        }
        // ---- timing: from the strike or the touchdown on, never before ----
        private static void ImpactTiming()
        {
            foreach(var p in Impacts)
            {
                Check(!NativeZombieFx.Wave(p,0,-.001f,out _,out _,out _,out _)&&NativeZombieFx.Wave(p,0,0,out _,out _,out _,out float f0)&&f0>.99f,"the wave starts on the strike, at its strongest");
                Check(!NativeZombieFx.ImpactFlash(-.001f,p.Power,out _,out _)&&NativeZombieFx.ImpactFlash(0,p.Power,out _,out float fa)&&fa==1,"so does the flash");
                Check(!NativeZombieFx.Wave(p,0,p.WaveSeconds+.001f,out _,out _,out _,out _),"the wave is over by its length");
                for(int i=0;i<p.Debris;i++)Check(!NativeZombieFx.Chunk(3,i,p.Debris,-.001f,p,out _,out _,out _,out _,out _),"no debris before the strike");
                Check(NativeZombieFx.CrackAlpha(-.01f,p.CrackStay)==0&&NativeZombieFx.CrackAlpha(p.CrackStay+.01f,p.CrackStay)==0&&NativeZombieFx.CrackAlpha(.05f,p.CrackStay)>.9f,"the cracks from the strike for their stay");
            }
            // The presentation's marks are the authority's (the take's impact is the hulk-motion branch's to meet).
            var st=NativeHordeKinds.ProposedRow(NativeHordeKinds.Hulk);Check(NativeHordeKinds.Strike(st.hit,false,0)==st.hit,"the smash's strike is LastAttack + the rules' hit");
            Check(NativeHordeDrop.LandAt(10,NativeHordeDrop.Hulk)==10-NativeHordeDrop.Hulk.Recover&&NativeHordeDrop.LandAt(10,NativeHordeDrop.Brute)==10-NativeHordeDrop.Brute.Recover,"the landing's impact is the drop's touchdown");
        }
        // ---- the breach: chips to the floor, the curtain off the gap, the remnants until the restore ----
        private static void Breach()
        {
            for(float t=-.1f;t<3.5f;t+=.01f)
            {
                for(int b=0;b<NativeBreach.Blows.Length-1;b++)for(int i=0;i<NativeZombieFx.SpallChips;i++)if(NativeZombieFx.Spall(5,b,i,t,1.6f,out var l,out _,out float s,out float left))Check(l.Y>=-(1.6f-s*.5f)-1e-4f&&l.Z>=0&&t<=1.6f&&left>=0,"a blow's chips spall off its face and fall to the floor");
                for(int i=0;i<NativeZombieFx.BreakChips;i++)if(NativeZombieFx.BreakChip(5,i,t,1.4f,1.15f,out var l,out _,out _,out float s,out _))Check(l.Y>=-(1.4f-s*.5f)-1e-4f&&l.Z>=-1e-4f&&t<=3.2f,"the burst's grit is thrown out along the facing and lands");
                for(int i=0;i<NativeZombieFx.BreakSparks;i++)if(NativeZombieFx.BreakSpark(5,i,t,out var l,out _,out float heat))Check(heat>=0&&heat<=1&&t<1.2f,"sparks out of the struck spot, briefly");
                for(int i=0;i<NativeZombieFx.CurtainCount;i++)if(NativeZombieFx.Curtain(5,i,t,2.4f,3.6f,out var l,out float s,out float a,out _))Check(Math.Abs(l.X)<=1.2f&&l.Y>=.15f-1e-4f&&l.Y<=3.6f&&l.Z>0&&t<=NativeZombieFx.CurtainSeconds,"the curtain pours off the gap's top edge to the floor");
            }
            for(int k=0;k<NativeZombieFx.Remnants;k++){Check(NativeZombieFx.Remnant(5,k,.3f,5,out _,out _)==0&&NativeZombieFx.Remnant(5,k,3,0,out _,out _)==0,"the remnants only after the burst and before the restore");float most=0;for(float t=.6f;t<12;t+=.05f){float on=NativeZombieFx.Remnant(5,k,t,12-t,out float st,out float len);Check(on>=0&&on<=1&&st>=0&&st<1&&len>0&&len<.1f,"a fragment of the outline");most=Math.Max(most,on);}Check(most>0,"each fragment glitches on");}
            for(float into=0;into<2.6f;into+=.05f)for(int m=0;m<NativeZombieFx.Motes;m++)if(NativeZombieFx.Mote(5,3,m,Math.Min(1,into),Math.Min(1,into*.8f),into,out var o,out float a))Check(o.Length()<.5f&&a<=1,"motes ride the piece home");
        }
        // ---- tiers: every stream thinned, each effect within its budget ----
        private static int Peak(NativeZombieFx.Effect e,int tier)
        {
            int N(int n)=>NativeZombieFx.Count(n,tier);bool x=NativeZombieFx.Extras(tier);int fumes=(int)(NativeZombieFx.FumeRate*NativeZombieFx.FumeLife)+2;
            switch(e)
            {
                // Core, halo, membrane, the tail's strings, drips, fumes.
                case NativeZombieFx.Effect.Glob:return 2+(x?1:0)+N(NativeZombieFx.TailBeads)+N(NativeZombieFx.Drips)+(x?fumes:0);
                // Stain, body film, sheen; satellites; bubbles (dome, or ripple and two pop drops), glints, fumes; the splash: flash, crown,
                // drops, the satellites' drops, spatter, sizzle.
                case NativeZombieFx.Effect.Pool:return 3+N(NativeZombieFx.Satellites)+N(NativeZombieFx.PoolBubbles)*(x?3:1)+(x?NativeZombieFx.Glints:0)+N(NativeZombieFx.PoolFumes)+1+N(NativeZombieFx.CrownLobes)+N(NativeZombieFx.SplashDrops)+N(NativeZombieFx.Satellites)+N(NativeZombieFx.SplashDrops)+N(4);
                case NativeZombieFx.Effect.Body:return 1+N(NativeZombieFx.BodyDrops)+(x?4*2:2)+N(NativeZombieFx.BodyDrips);
                case NativeZombieFx.Effect.Impact:case NativeZombieFx.Effect.HeavyImpact:
                {
                    var p=e==NativeZombieFx.Effect.Impact?Impacts[2]:Impacts[3];int cracks=N(p.Cracks)*(NativeZombieFx.CrackSegments*2+(x?NativeZombieFx.Branches:1)*2);
                    return 2+2+3+cracks+N(p.Dust)+N(p.Column)+N(p.Debris)+(x?p.Sparks:0)+(p.Crater?N(NativeHordeFx.CraterChunks):0);
                }
                default:return 1+N(16)+N(NativeZombieFx.BreakChips)+(x?NativeZombieFx.BreakSparks:0)+N(NativeZombieFx.CurtainCount)+N(12)+N(NativeZombieFx.Remnants)*4;
            }
        }
        private static void Tiers()
        {
            Check(NativeZombieFx.Tier(false,1)==NativeZombieFx.Full&&NativeZombieFx.Tier(false,.3f)==NativeZombieFx.Reduced&&NativeZombieFx.Tier(true,1)==NativeZombieFx.Low,"the tiers: full, Reduced effects, the low graphics tier");
            for(int n=0;n<40;n++){Check(NativeZombieFx.Count(n,NativeZombieFx.Full)==n,"full draws every stream");foreach(int t in new[]{NativeZombieFx.Reduced,NativeZombieFx.Low})Check(NativeZombieFx.Count(n,t)==(n==0?0:Math.Max(1,(n+1)/2)),"the others half of it");}
            Check(NativeZombieFx.Extras(NativeZombieFx.Full)&&!NativeZombieFx.Extras(NativeZombieFx.Reduced)&&!NativeZombieFx.Extras(NativeZombieFx.Low),"the extras on the full tier only");
            foreach(NativeZombieFx.Effect e in Enum.GetValues(typeof(NativeZombieFx.Effect)))
            {
                int full=Peak(e,NativeZombieFx.Full),low=Peak(e,NativeZombieFx.Low);
                Check(full<=NativeZombieFx.Budget(e,NativeZombieFx.Full),e+" within its full budget ("+full+" of "+NativeZombieFx.Budget(e,NativeZombieFx.Full)+")");
                Check(Peak(e,NativeZombieFx.Reduced)<=NativeZombieFx.Budget(e,NativeZombieFx.Reduced)&&low<=NativeZombieFx.Budget(e,NativeZombieFx.Low),e+" within its reduced and low budgets ("+low+")");
                Check(low<=full*.65f,e+" thins on the low tiers ("+low+" of "+full+")");
            }
        }
        // ---- determinism: the same frame on every peer ----
        private static byte[] Frames()
        {
            var ms=new MemoryStream();var bw=new BinaryWriter(ms);void W(Vector3 v){bw.Write(v.X);bw.Write(v.Y);bw.Write(v.Z);}
            var from=new Vector3(1,1.4f,2);var to=new Vector3(6,.025f,-3);float flight=NativeHordeFx.Flight(Flat(to-from));
            for(int f=0;f<400;f++)
            {
                float t=f/60f;
                if(NativeHordeFx.Glob(from,to,flight,t,out var g)){W(g);W(NativeZombieFx.GlobVelocity(from,to,flight,t));NativeZombieFx.GlobShape(11,t,5,out float a,out float c);bw.Write(a);bw.Write(c);}
                for(int k=0;k<NativeZombieFx.TailBeads;k++)if(NativeZombieFx.TailBead(from,to,flight,t,k,out var p,out float s)){W(p);bw.Write(s);}
                for(int i=0;i<NativeZombieFx.SplashDrops;i++)if(NativeZombieFx.SplashDrop(11,i,t,1.3f,out var o,out var v,out float s)){W(o);W(v);bw.Write(s);}
                if(NativeZombieFx.Pool(t,4,out float sp,out float wet))for(int k=0;k<NativeZombieFx.PoolEdge;k++){float an=k*Tau/NativeZombieFx.PoolEdge;bw.Write(NativeZombieFx.EdgeAt(11,an,t,4));bw.Write(NativeZombieFx.Thickness(11,k/(float)NativeZombieFx.PoolEdge,an,1-an,t,wet));bw.Write(NativeZombieFx.Sheen(11,.8f,an,wet));}
                for(int i=0;i<NativeZombieFx.Satellites;i++){NativeZombieFx.Satellite(11,i,1.3f,out var sa,out _,out float sl,out _,out float sland);bw.Write(sa.X);bw.Write(sa.Y);bw.Write(sl);bw.Write(sland);if(NativeZombieFx.SatelliteDrop(11,i,t,1.3f,out var so,out _,out _))W(so);}
                for(int i=0;i<NativeZombieFx.PoolBubbles;i++)if(NativeZombieFx.PoolBubble(11,i,t,4,1.3f,out var o,out float s,out float pop)){W(o);bw.Write(s);bw.Write(pop);}
                foreach(var p in Impacts){for(int i=0;i<p.Debris;i++)if(NativeZombieFx.Chunk(11,i,p.Debris,t,p,out var o,out var ax,out float an,out float s,out float l)){W(o);W(ax);bw.Write(an);}for(int i=0;i<p.Dust;i++)if(NativeZombieFx.HugDust(11,i,p.Dust,t,p,out var o,out float s,out float a,out _)){W(o);bw.Write(s);bw.Write(a);}
                    for(int i=0;i<p.Cracks;i++)for(int j=0;j<=NativeZombieFx.CrackSegments;j++){var c=NativeZombieFx.CrackPoint(11,i,p.Cracks,j,t,p,out float w);bw.Write(c.X);bw.Write(c.Y);bw.Write(w);}}
                for(int i=0;i<NativeZombieFx.CurtainCount;i++)if(NativeZombieFx.Curtain(11,i,t,2,3,out var l,out float s,out _,out _)){W(l);bw.Write(s);}
                for(int k=0;k<NativeZombieFx.Remnants;k++)bw.Write(NativeZombieFx.Remnant(11,k,t,6-t,out _,out _));
            }
            return ms.ToArray();
        }
        private static void Determinism(){var a=Frames();var b=Frames();Check(a.Length>1000&&a.SequenceEqual(b),"every shape is the same on every run (host, guests, the killcam)");}
        // ---- no allocation ----
        private static void Allocation()
        {
            var from=new Vector3(0,1.4f,0);var to=new Vector3(0,.025f,6);float flight=NativeHordeFx.Flight(6);
            void Frame(int f)
            {
                float t=(f%300)/60f;NativeHordeFx.Glob(from,to,flight,t,out _);NativeZombieFx.GlobVelocity(from,to,flight,t);NativeZombieFx.GlobShape(3,t,5,out _,out _);
                for(int k=0;k<NativeZombieFx.TailBeads;k++)NativeZombieFx.TailBead(from,to,flight,t,k,out _,out _);for(int i=0;i<NativeZombieFx.Drips;i++)NativeZombieFx.Drip(from,to,flight,3,i,t,out _,out _);
                for(long k=0;k<12;k++)NativeZombieFx.GlobFume(from,to,flight,3,k,t,out _,out _,out _,out _);NativeZombieFx.MouthGlob(3,t%1,.8f,out _,out _,out _);for(int k=0;k<3;k++)NativeZombieFx.MouthString(3,k,t%1,.8f,.5f,out _,out _,out _,out _);
                NativeZombieFx.SplashFlash(t,out _,out _);for(int k=0;k<NativeZombieFx.CrownLobes;k++)NativeZombieFx.Crown(3,k,t,out _,out _,out _);for(int i=0;i<NativeZombieFx.SplashDrops;i++){NativeZombieFx.SplashDrop(3,i,t,1.3f,out _,out _,out _);NativeZombieFx.SpatterAt(3,i,1.3f);NativeZombieFx.SplashLand(3,i,1.3f);NativeZombieFx.SpatterSize(3,i,out _);}
                NativeZombieFx.Pool(t,4,out float sp,out float wet);for(int k=0;k<NativeZombieFx.PoolEdge;k++){float an=k*Tau/NativeZombieFx.PoolEdge;NativeZombieFx.EdgeAt(3,an,t,4);NativeZombieFx.EtchEdge(3,an,4);NativeZombieFx.Residue(3,an);NativeZombieFx.Thickness(3,.5f,an,an,t,wet);NativeZombieFx.Sheen(3,.8f,an,wet);NativeZombieFx.Fresnel(.3f);}
                for(int k=0;k<NativeZombieFx.Edge;k++)NativeZombieFx.BlotchEdge(3,k);NativeZombieFx.Etch(t,4,out _);for(int i=0;i<NativeZombieFx.Satellites;i++){NativeZombieFx.Satellite(3,i,1.3f,out _,out _,out _,out _,out _);NativeZombieFx.SatelliteDrop(3,i,t,1.3f,out _,out _,out _);NativeZombieFx.SatelliteWet(t,4);}
                for(int i=0;i<NativeZombieFx.PoolBubbles;i++)NativeZombieFx.PoolBubble(3,i,t,4,1.3f,out _,out _,out _);for(int i=0;i<NativeZombieFx.PoolFumes;i++)NativeZombieFx.PoolFume(3,i,t,4,1.3f,out _,out _,out _,out _);for(int i=0;i<NativeZombieFx.Glints;i++)NativeZombieFx.Glint(3,i,t,4,1.3f,out _,out _);
                NativeZombieFx.Blotch(t,4,.32f,out _,out _,out _);for(int i=0;i<4;i++){NativeZombieFx.FaceDrip(3,i,t,4,.32f,out _,out _,out _,out _);NativeZombieFx.BodyDrip(3,i,t,1.8f,out _,out _,out _);NativeZombieFx.BodyDrop(3,i,t,1.2f,out _,out _,out _);}
                foreach(var p in Impacts)
                {
                    NativeZombieFx.ImpactFlash(t,p.Power,out _,out _);NativeZombieFx.Wave(p,0,t,out _,out _,out _,out _);NativeZombieFx.Wave(p,1,t,out _,out _,out _,out _);NativeZombieFx.Crush(t,p,out _,out _);NativeZombieFx.CrackAlpha(t,p.CrackStay);NativeZombieFx.CrackGlow(t);
                    for(int i=0;i<p.Cracks;i++){for(int j=0;j<=NativeZombieFx.CrackSegments;j++)NativeZombieFx.CrackPoint(3,i,p.Cracks,j,t,p,out _);NativeZombieFx.CrackBranch(3,i,p.Cracks,0,t,p,out _,out _,out _);}
                    for(int i=0;i<p.Dust;i++)NativeZombieFx.HugDust(3,i,p.Dust,t,p,out _,out _,out _,out _);for(int i=0;i<p.Column;i++)NativeZombieFx.ColumnDust(3,i,t,p,out _,out _,out _,out _);
                    for(int i=0;i<p.Debris;i++)NativeZombieFx.Chunk(3,i,p.Debris,t,p,out _,out _,out _,out _,out _);for(int i=0;i<p.Sparks;i++)NativeZombieFx.Spark(3,i,t,p,out _,out _,out _);
                }
                for(int i=0;i<NativeZombieFx.BreakChips;i++){NativeZombieFx.BreakChip(3,i,t,1.4f,1,out _,out _,out _,out _,out _);NativeZombieFx.BreakSpark(3,i,t,out _,out _,out _);}for(int i=0;i<NativeZombieFx.SpallChips;i++)NativeZombieFx.Spall(3,1,i,t,1.5f,out _,out _,out _,out _);
                for(int i=0;i<NativeZombieFx.CurtainCount;i++)NativeZombieFx.Curtain(3,i,t,2,3,out _,out _,out _,out _);for(int k=0;k<NativeZombieFx.Remnants;k++)NativeZombieFx.Remnant(3,k,t,5-t,out _,out _);for(int m=0;m<NativeZombieFx.Motes;m++)NativeZombieFx.Mote(3,2,m,.5f,.3f,t,out _,out _);
            }
            for(int i=0;i<300;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=300;i<1500;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1200 frames of globs, pools (outline, thickness, sheen, satellites, stain), splashes, corrosion, five impacts and a breach ("+used+" bytes)");
        }
        // ---- the runtime: draws only, the authority's numbers, streams by tier, no per-frame allocation ----
        private static string Body(string text,string head)
        {int i=text.IndexOf(head,StringComparison.Ordinal);if(i<0)return "";int b=text.IndexOf('{',i),depth=0;for(int k=b;k<text.Length;k++){if(text[k]=='{')depth++;else if(text[k]=='}'&&--depth==0)return text.Substring(i,k-i+1);}return "";}
        private static void Runtime(string root)
        {
            var draw=R(root,"Assets/ChipArena/Runtime/NativeZombieFxDraw.cs");var acid=R(root,"Assets/ChipArena/Runtime/NativeHordeAcidPresentation.cs");var kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            var zombie=R(root,"Assets/ChipArena/Runtime/NativeHordeZombiePresentation.cs");var breach=R(root,"Assets/ChipArena/Runtime/NativeHordeBreachPresentation.cs");var core=R(root,"Assets/ChipArena/Core/NativeZombieFx.cs");var vfx=R(root,"Assets/ChipArena/Runtime/NativeVfxDraw.cs");
            foreach(var (name,text) in new[]{("drawer",draw),("acid",acid),("zombie",zombie),("breach",breach)})
                foreach(var bad in new[]{".Hp=",".Health=","Damage(","Emit(","StaggerAt=","LastAttack=","linearVelocity=","AddComponent<","Collider>().enabled","isTrigger","Body.position=","Time.timeScale"})Check(!text.Contains(bad),name+": never writes `"+bad+"`");
            // Every draw method of the drawer: no per-frame allocation (no LINQ, closures, strings, collections or native objects).
            foreach(var m in new[]{"public bool Glob(","public void Windup(","public void Splash(","public void Pool(","public void PoolAfter(","public void Corrode(","public void Body(","public void Fizzle(","public void Impact(","private void ImpactDecals(","public void StompCracks(","public void Footfall(","public void WallCrack(","public void Knock(","public void Burst(","public void Remnants(","public void Motes(","private void Fan(","private void Band(","private void Strip(",
                "private void BodyOutline(","private void LiquidBody(","private Color Sheened(","private static void Grid(","private void Stain(","private void SatelliteSplat(","private void Ripple(","private static Color Shade("})
            {var b=Body(draw,m);Check(b.Length>0,"the drawer has "+m);foreach(var bad in new[]{".Select(",".Where(",".Any(",".First",".ToArray(",".ToList(","=>","\"+","+\"","$\"","new List<","new Dictionary<","new Mesh","new Material","new HashSet<","new int[","new float[","new Vector3[","new Color[","new NVector2["})Check(!b.Contains(bad),m+" allocates nothing per frame (`"+bad+"`)");}
            Check(!Regex.IsMatch(draw,@"private\s+(readonly\s+)?(Mesh|Material)\s+\w+\s*=\s*new"),"no native object in a field initializer of the drawer");
            // Every stream is thinned by tier (N(...)) and the extras gated.
            foreach(var stream in new[]{"N(NativeZombieFx.TailBeads)","N(NativeZombieFx.Drips)","N(NativeZombieFx.MouthStrings)","N(NativeZombieFx.CrownLobes)","N(NativeZombieFx.SplashDrops)","N(NativeZombieFx.PoolBubbles)","N(NativeZombieFx.PoolFumes)","N(p.Dust)","N(p.Debris)","N(p.Cracks)","N(NativeZombieFx.BreakChips)","N(NativeZombieFx.CurtainCount)","N(NativeZombieFx.Remnants)","N(NativeZombieFx.SpallChips)"})Check(draw.Contains(stream),"the stream "+stream+" follows the tier");
            Check(draw.Contains("if(extras)for(int i=0;i<p.Sparks;i++)")&&draw.Contains("if(extras)vfx.Water(")&&draw.Contains("if(extras)for(int i=0;i<NativeZombieFx.Glints;i++)")&&draw.Contains("for(int b=0;b<(extras?4:2);b++)"),"the extras on the full tier only");
            Check(draw.Contains("public const int DecalQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent-2;")&&draw.Contains("new NativeFxDraw(DecalQueue)"),"the decals draw before the effect billboards");
            Check(vfx.Contains("public void SlamWave(")&&vfx.Contains("public void Blob(")&&vfx.Contains("public void Rubble(")&&!Regex.IsMatch(Body(vfx,"public void SlamWave(")+Body(vfx,"public void Rubble("),@"new\s+\w+(<[^>]*>)?\s*\[|new List<"),"the shared renderer draws the upright wave, the glossy blob and the rubble (into its reused buffers)");
            Check(!Regex.IsMatch(Body(draw,"public void Impact(")+Body(draw,"private void ImpactDecals("),@"decals\.Ring\("),"an impact draws no flat ring (the wave is upright)");
            // No ring at the pool's radius (owner 2026-10-14: "No circle showing either, currently theres a green circle"): no ring or
            // annulus anywhere in the acid's pool, splash, stain or fizzle, no rim alpha in Core, and the body's outline is the lobed
            // EdgeAt on the record's radius (Outline for the fizzle's small puddle), its edge feathered out, its lip no thicker than
            // the liquid inside it, its sheen fading to nothing along the very edge.
            var acidDraw=Body(draw,"public void Splash(")+Body(draw,"public void Pool(")+Body(draw,"public void PoolAfter(")+Body(draw,"public void Fizzle(")+Body(draw,"private void LiquidBody(")+Body(draw,"private void Stain(")+Body(draw,"private void SatelliteSplat(");
            Check(acidDraw.Length>0&&!Regex.IsMatch(acidDraw,@"\.Ring\(|Annulus\(|RimAlpha"),"the pool draws no ring at its radius");Check(!core.Contains("RimAlpha"),"Core has no radius ring left to draw");
            Check(draw.Contains("radius*(spreading?NativeZombieFx.EdgeAt(seed,a,since,duration):NativeZombieFx.Outline(seed,a,since))")&&draw.Contains("if(j==rings-2)th=Mathf.Min(LipThickness,inner);")&&draw.Contains("j==rings-1?A(LipTone,0)"),"the body is the lobed outline, its lip never more than the liquid inside, feathered out");
            Check(draw.Contains("Tier==NativeZombieFx.Low?NativeZombieFx.PoolEdgeLow:NativeZombieFx.PoolEdge")&&draw.Contains("N(NativeZombieFx.Satellites)")&&draw.Contains("if(extras)vfx.Blob(p,Quaternion.identity,new Vector3(s,s*.7f,s)")&&draw.Contains("if(extras&&pop<.4f)"),"Reduced effects and the low tier: calmer pools (fewer satellites and bubbles, no domes, pop drops or glints; a coarser low outline)");
            Check(Regex.IsMatch(draw,@"private readonly float\[\] bodyEdge=new float\[NativeZombieFx\.PoolEdge\];"),"the outline's scratch is allocated once");
            // The spit's events: the acid comes from the records on every map, the bow lodges and thunks only arrows, the impact cues
            // and a DEV owner's procs skip an add's events.
            var bowHud=R(root,"Assets/ChipArena/Runtime/NativeBowHud.cs");var blast=R(root,"Assets/ChipArena/Runtime/NativeBlastPresentation.cs");var spits=Body(kinds,"private void HordeSpitEvents(");
            Check(spits.Contains("int recordCount=arena.PveAcidCount;")&&spits.Contains("record:true")&&!spits.Contains("HordeLaunchGlob(from,")&&spits.IndexOf("if(arena.Pve!=null)return;",StringComparison.Ordinal)>spits.IndexOf("record:true",StringComparison.Ordinal),"PvE and the DEV monsters' devAcid draw the same pools, from the records");
            Check(kinds.Contains("if(!hordeGlobs[i].Record)continue;")&&acid.Contains("Record=record"),"only a record's glob goes with its record (the DEV preview's stay)");
            Check(bowHud.Contains("if(NativeBowWire.Lodges(e,source.Seat.spec))Cue(NativeBowSounds.Thunk.Key")&&blast.Contains("e.Kind==EventKind.Impact&&!NativeImpactCues.AddEvent(e.Detail)")&&blast.Contains("e.Source<=0||NativeImpactCues.AddEvent(e.Detail))continue;"),"an add's events take no owner's arrow, thunk, weapon colour or surface procs");
            Check(kinds.Contains("if(flat.magnitude>NativeSpitterRules.Range)to=floor+flat.normalized*NativeSpitterRules.Range;"),"the DEV preview's glob keeps to the rule's range");
            // The authority's numbers: the records' pool, the smash disc, the drop's slam, the breach's clock.
            Check(acid.Contains("z.Pool(g.To,g.Floor,landed,g.Radius,g.Duration,g.Seed)")&&kinds.Contains("NativeSpitterRules.Radius,NativeSpitterRules.Duration")&&kinds.Contains("HordeLaunchGlob(NativeArena.Vec(r.from),NativeArena.Vec(r.to),r.at,r.flight,(uint)r.id,r.actor,NativeSpitterRules.Radius,NativeSpitterRules.Duration,record:true)"),"the pool is the record's, on the rule's radius and duration");
            Check(acid.Contains("if(!z.Within(")&&acid.Contains("z.Glob(g.From,g.To,g.Flight,since,g.Seed,g.Floor)"),"the glob flies the record's arc");
            Check(core.Contains("NativeHordeKinds.SmashRadiusOf(kind)")&&core.Contains("p.SlamRadius")&&kinds.Contains("NativeZombieFx.Smash(j.Kind)")&&zombie.Contains("NativeZombieFx.Landing(p)")&&zombie.Contains("NativeZombieFx.Crash"),"impacts on the smash disc and the drop profile's slam radius");
            Check(kinds.Contains("if(a>=H)")&&kinds.Contains("HordeImpactAt(j.Id,c,now-(a-H),")&&zombie.Contains("float born=hordeClock-(age-landAge);"),"the impacts are born on the strike and the touchdown");
            Check(Body(breach,"private void DrawBlockBreachFx(").Contains("zf.Burst(w,into,h.Strike.X,h.Strike.Y,")&&breach.Contains("z.Remnants(w,h.Center,h.Rim,NativeBreach.Outline,")&&breach.Contains("b.RestoreAt-now"),"the breach's effects on its own clock and replicated restore");
            Check(!core.Contains("UnityEngine")&&!core.Contains("NativeArena")&&!core.Contains("new Random(")&&!core.Contains("DateTime"),"NativeZombieFx.cs is engine free and clock free");
        }
    }
}
#endif
