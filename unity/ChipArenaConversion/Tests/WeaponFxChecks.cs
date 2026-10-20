#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Effects overhaul, weapons milestone (owner 2026-10-11): Unity-owned looks with no source fixture (the source drew a single
    // glow cone, flat trails and blade ribbons), so these pin the Unity rules in Core/NativeWeaponFx.cs and scan the Runtime
    // hooks, drawer and shaders: muzzle flashes (timing, petals, sparks, wisps, the light pulse; since 2026-10-20 the layered
    // flash and its own light, held in full by MuzzleFlashChecks), tracers, hard-surface impacts
    // (the cooling ramp, the hemisphere, the floor bounce), energy trails, slash crescents (driven by NativeMeleeRules.Angle over
    // the hit window), roll afterimages, the hit flash code and colour, and the charge streaks and rings; plus determinism
    // (pinned seeded values), the quality tiers, the GLSL-compatible shared blocks and the build gate. No Unity render, GPU or
    // Windows run happens here.
    internal static class WeaponFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Weapon fx: "+what);count++;}
        private static bool Near(double a,double b,double e=1e-4)=>Math.Abs(a-b)<=e;
        private static string Code(string text)=>string.Join("\n",text.Split('\n').Select(l=>{int c=l.IndexOf("//",StringComparison.Ordinal);return c<0?l:l.Substring(0,c);}));
        private static string Between(string text,string begin,string end){int a=text.IndexOf(begin,StringComparison.Ordinal),b=text.IndexOf(end,a<0?0:a,StringComparison.Ordinal);Check(a>=0&&b>a,"block "+begin);return text.Substring(a,b-a);}
        public static void Run(string root,ConversionContent content)
        {
            count=0;
            Muzzle();Tracers();Impacts();Trails();Slashes(content);Afterimages();HitFlash();Charge();
            Text(root);
            Console.WriteLine("PASS weapon effects ("+count+" checks: muzzle flashes, tracers, impacts and their cooling ramp, energy trails, slash crescents on the swing rules, roll afterimages, hit flash code and colour, charge orb, determinism, quality tiers, hooks, shaders and the build gate; text scans, no Unity render)");
        }
        private static void Muzzle()
        {
            // Owner 2026-10-20: the three-petal star became the layered flash of Core/NativeMuzzleFlash.cs (core, fireball, plume,
            // device petals and vents, pods, seeded sparks, wisps) and its light NativeMuzzleLight's own shader slots;
            // MuzzleFlashChecks holds them in full (anchors, variation, the light's envelope, cap, determinism, reach). Here the
            // milestone's timing rules, re-measured on the new looks: the flash lasts 40-90 ms, the petals keep full opacity to
            // two thirds of it and shrink to .35, the sparks leave in a 6 degree cone and follow the tiers, the wisps too.
            foreach(var gun in new[]{"pistol","rifle","machinegun"})
            {
                Check(NativeMuzzleFx.Look(gun,NativeWeaponSkins.StandardIndex,default,null,MuzzleKit.None,0,0,out var look),gun+" has a muzzle flash");
                Check(look.Life>=.04f&&look.Life<=.09f,gun+" flash lasts 40-90 ms ("+look.Life+")");
                long seed=NativeWeaponFx.Seed(12.5,3);
                for(int k=0;k<look.Petals;k++)
                {
                    Check(!NativeMuzzleFx.Petal(look,seed,k,-.001,out _,out _,out _,out _)&&!NativeMuzzleFx.Petal(look,seed,k,look.Life,out _,out _,out _,out _),gun+" petal only inside the flash");
                    Check(NativeMuzzleFx.Petal(look,seed,k,0,out float r0,out float l0,out float w0,out float a0)&&a0==1&&l0>=look.Star*.75f-1e-5f&&l0<=look.Star*1.25f+1e-5f&&Near(w0,l0*look.StarWidth),gun+" petal at the shot");
                    Check(NativeMuzzleFx.Petal(look,seed,k,look.Life*.6,out _,out _,out _,out float am)&&am==1,gun+" petals keep full opacity to two thirds");
                    Check(NativeMuzzleFx.Petal(look,seed,k,look.Life*.999,out _,out float l1,out _,out float a1)&&a1<.01f&&l1<l0*.36f,gun+" petals shrink to .35 and fade");
                    NativeMuzzleFx.Petal(look,seed,k,0,out float again,out _,out _,out _);Check(again==r0,gun+" petals are a pure function of the shot");
                }
                // The petals spread round the barrel: consecutive rolls a share of a turn apart (seeded jitter at most +-.35 rad each).
                NativeMuzzleFx.Petal(look,seed,0,0,out float p0,out _,out _,out _);NativeMuzzleFx.Petal(look,seed,1,0,out float p1,out _,out _,out _);
                Check(Math.Abs(Math.IEEERemainder((p1-p0)-Math.PI*2/look.Petals,Math.PI*2))<.71,gun+" petals form a star");
                Check(NativeMuzzleFx.Plume(look,seed,0,out float plume,out float plumeWidth,out _)&&plume>look.Star&&plumeWidth<plume,gun+" the plume runs along the barrel, longer than a petal");
                Check(NativeMuzzleFx.Core(look,seed,0,out float core,out var gc)&&core<=.25f&&gc.Max>1&&!NativeMuzzleFx.Core(look,seed,look.Life,out _,out _),gun+" core glow is HDR, modest and short");
                int full=NativeMuzzleFx.SparkCount(look,seed,1,false);Check(full<=look.Sparks&&NativeMuzzleFx.SparkCount(look,seed,.3f,false)<=1&&NativeMuzzleFx.SparkCount(look,seed,1,true)<=1,gun+" sparks follow the quality tiers");
                for(int k=0;k<look.Sparks;k++)for(double age=.012;age<.25;age+=.03)
                    if(NativeMuzzleFx.Spark(seed,k,age,out var local,out var velocity,out _,out _))Check(local.Z>0&&Math.Atan2(Math.Sqrt(local.X*local.X+local.Y*local.Y),local.Z)<=.106,gun+" sparks leave forward in a 6 degree cone");
                Check(NativeMuzzleFx.WispCount(look,1,false)==look.Wisps&&NativeMuzzleFx.WispCount(look,.3f,false)==1&&NativeMuzzleFx.WispCount(look,.3f,true)==0,gun+" smoke wisps follow the tiers");
                Check(NativeMuzzleLight.Look(gun,MuzzleKit.None,out var light)&&NativeMuzzleLight.Pulse(light,0)==0&&NativeMuzzleLight.Pulse(light,light.Attack)>.99f&&NativeMuzzleLight.Pulse(light,.08)<.6f&&NativeMuzzleLight.Pulse(light,light.Attack+NativeMuzzleLight.Tail*light.Decay)==0,gun+" the light pulse rises in under 5 ms and is gone after its tail");
            }
            Check(!NativeMuzzleFx.Look("sword",out _)&&!NativeMuzzleFx.Look("fire",out _),"melee and staffs have no muzzle flash");
            Check(NativeMuzzleFx.Wisp(5,0,.1,out _,out float rise,out float size,out float cover)&&rise>0&&size>.07f&&cover>0&&cover<=.2f&&!NativeMuzzleFx.Wisp(5,0,NativeMuzzleFx.WispLife,out _,out _,out _,out _),"a light wisp rises and is gone after its life");
            Check(NativeMuzzleFx.Span>=NativeMuzzleFx.WispLife+.03,"the span covers the last wisp");
            // Network round trip: a timestamp that moved by a float rounding still seeds the same star.
            Check(NativeWeaponFx.Seed(12.3456789,2)==NativeWeaponFx.Seed((float)12.3456789,2)&&NativeWeaponFx.Seed(1,2)!=NativeWeaponFx.Seed(1.01,2)&&NativeWeaponFx.Seed(1,2)!=NativeWeaponFx.Seed(1,3),"seeds survive a float round trip and differ per shot and owner");
            // Pinned values (Unity-owned): a change to the hash or the seed would change every peer's effects at once.
            Check(Near(NativeWeaponFx.Hash(0,0),0.157361426543,1e-11)&&Near(NativeWeaponFx.Hash(42,7),0.723001620585,1e-11)&&Near(NativeWeaponFx.Hash(42,7),PinnedHash(42,7),0),"pinned hash");
        }
        // The SplitMix64 finaliser, written out again so a drift in the Core copy shows.
        private static double PinnedHash(long seed,long k){unchecked{ulong h=(ulong)seed*0x9E3779B97F4A7C15UL+(ulong)k*0xD1B54A32D192ED03UL+0x94D049BB133111EBUL;h^=h>>30;h*=0xBF58476D1CE4E5B9UL;h^=h>>27;h*=0x94D049BB133111EBUL;h^=h>>31;return (h>>11)*(1.0/9007199254740992.0);}}
        private static void Tracers()
        {
            Check(NativeTracerFx.Traced(new NativeShotView{Kind=NativeShotKind.Bullet,Class="ranged"})&&!NativeTracerFx.Traced(new NativeShotView{Kind=NativeShotKind.Bullet,Class="magic"})&&!NativeTracerFx.Traced(new NativeShotView{Kind=NativeShotKind.Bullet,Class="ranged",Bowling=true})&&!NativeTracerFx.Traced(new NativeShotView{Kind=NativeShotKind.Melee,Class="ranged"}),"tracers on gun rounds only");
            Check(NativeTracerFx.Length(150,10)==2.2f&&NativeTracerFx.Length(49,10)==(float)Math.Max(.4,49*.03)&&NativeTracerFx.Length(150,.3)==.3f&&NativeTracerFx.Width("rifle")<=.04f,"thin streaks, capped, never longer than the flight");
        }
        private static void Impacts()
        {
            var c0=NativeWeaponFx.Cooling(0);var c1=NativeWeaponFx.Cooling(.09);var c2=NativeWeaponFx.Cooling(.62);var c3=NativeWeaponFx.Cooling(1);
            bool Is(FxColor c,int hex){var l=NativeWeaponFx.Linear(hex);return Near(c.R,l.R)&&Near(c.G,l.G)&&Near(c.B,l.B);}
            Check(Near(c0.R,1)&&Near(c0.G,1)&&Near(c0.B,1)&&Is(c1,0xff7900)&&Is(c2,0xdf3c00)&&Is(c3,0x180700),"sparks cool white, #FF7900, #DF3C00, #180700");
            Check(NativeImpactFx.ShardCount(1,false)==6&&NativeImpactFx.ShardCount(.3f,false)==3&&NativeImpactFx.SparkCount(1,false)==8&&NativeImpactFx.SparkCount(.3f,true)==3,"counts follow the tiers");
            foreach(var n in new[]{Vector3.UnitY,-Vector3.UnitZ,Vector3.Normalize(new Vector3(1,.3f,-.5f))})
                for(int k=0;k<6;k++)
                {
                    Check(NativeImpactFx.Shard(77,k,.02,n,out var offset,out var dir,out float length,out float width,out float alpha)&&Vector3.Dot(dir,n)>0&&Vector3.Dot(offset,n)>0&&length>0&&width<length&&alpha>0,"shards fly out of the surface");
                    Check(!NativeImpactFx.Shard(77,k,.21,n,out _,out _,out _,out _,out _),"shards live .15-.2 s");
                    Check(NativeImpactFx.Spark(77,k,.01,n,out var so,out _,out var sc)&&Vector3.Dot(so,n)>0&&sc.Max>1,"sparks start out of the surface, hot");
                }
            Check(NativeImpactFx.Glow(0,out float gs,out var g)&&g.Max>1&&!NativeImpactFx.Glow(NativeImpactFx.GlowLife,out _,out _),"the quick glow");
            // The floor bounce: a spark never ends below a floor it hit.
            for(int k=0;k<8;k++)for(double age=0;age<.6;age+=.05)if(NativeImpactFx.Spark(91,k,age,Vector3.UnitY,out var o,out _,out _))Check(o.Y>=0,"floor sparks bounce, never sink");
        }
        private static void Trails()
        {
            var magic=new NativeShotView{Kind=NativeShotKind.Bullet,Class="magic",Spec="fire"};
            Check(NativeTrailFx.Energy(magic)&&!NativeTrailFx.Energy(new NativeShotView{Kind=NativeShotKind.Bullet,Class="ranged"})&&!NativeTrailFx.Energy(new NativeShotView{Kind=NativeShotKind.Bullet,Class="magic",Arcade=1})&&!NativeTrailFx.Energy(new NativeShotView{Kind=NativeShotKind.Bullet,Class="magic",Siren=true}),"energy trails on magic bolts, not guns, hadoukens or Siren shots");
            Check(NativeTrailFx.Energy(new NativeShotView{Kind=NativeShotKind.Ability,Ability="ball-lightning"})&&!NativeTrailFx.Energy(new NativeShotView{Kind=NativeShotKind.Ability,Ability="frag-grenade"}),"energy abilities only");
            Check(NativeTrailFx.Spacing(1,false)<=.17f&&NativeTrailFx.Spacing(.3f,false)>NativeTrailFx.Spacing(1,true)&&NativeTrailFx.Spacing(1,true)>NativeTrailFx.Spacing(1,false),"distance emission, sparser on the reduced tiers");
            Check(NativeTrailFx.SpriteSize(0)==.5f&&Near(NativeTrailFx.SpriteSize(.09),.91)&&NativeTrailFx.SpriteSize(1)==0&&NativeTrailFx.SpriteAlpha(0)==0&&NativeTrailFx.SpriteAlpha(.5)==1,"sprite size and opacity curves");
            NativeTrailFx.Sprite(5,9,.14f,out float life,out float size,out var offset,out var drift);NativeTrailFx.Sprite(5,9,.14f,out float life2,out _,out _,out _);
            Check(life==life2&&life>=.08f&&life<=.2f&&size>0&&offset.Length()<=.14f*.6f+1e-5f&&drift.Length()>0,"sprites are seeded by the shot and their index");
        }
        private static void Slashes(ConversionContent content)
        {
            var stats=new ChipStats(content.baseStats);
            foreach(var spec in new[]{"sword","daggers","greatsword","shield"})
            {
                bool has=NativeSlashFx.Look(spec,out var look);Check(NativeSlashFx.Covers(spec)&&has,spec+" has a slash look");
                var falling=NativeMeleeRules.Begin(content.Weapon(spec),stats,1,1,0);
                NativeAirAttackSimulation.Apply(falling);
                var air=new Actor{Hp=100,SourceSpec=spec,AirAttack=true,ImpactAt=-1,SwingAt=falling.HitAt,SwingUntil=falling.HitUntil};
                Check(!NativeSlashFx.CanCapture(air,2)&&!NativeSlashFx.Frame(falling,look,2,false,out _,out _,out _,out _),spec+" falling timeout never becomes a ten-second crescent");
                NativeAirAttackSimulation.Land(falling,2);air.ImpactAt=2;air.SwingAt=falling.HitAt;air.SwingUntil=falling.HitUntil;
                Check(NativeSlashFx.CanCapture(air,2)&&NativeSlashFx.Frame(falling,look,2.004f,false,out _,out _,out _,out _),spec+" landed attack has a short impact crescent");
                Check(!NativeSlashFx.Frame(falling,look,3,false,out _,out _,out _,out _)&&!NativeSlashFx.CanCapture(air,3),spec+" walking after landing cannot retain or recreate slash");
                for(int index=1;index<=3;index++)
                {
                    var s=NativeMeleeRules.Begin(content.Weapon(spec),stats,1,index,.4f);
                    Check(!NativeSlashFx.Frame(s,look,s.HitAt-.001f,false,out _,out _,out _,out _),spec+" nothing before the hit window");
                    // Since 2026-10-12 the crescent follows the visible blade of the cut the pilot plays (MeleeSwing.VisibleSweep): its
                    // tail at the arc's end where the blade enters, its head where the blade has swept to, opening the blade's way.
                    float span=Math.Min(look.MaxArc,s.Arc),dir=NativeSlashFx.Direction(s);var visible=NativeSlashFx.Visible(s);
                    Check(visible!=null&&(dir==1||dir==-1),spec+" the cut's visible blade is known");
                    Check(NativeSlashFx.Frame(s,look,s.HitAt+.004f,false,out float tail,out float head,out _,out _)&&Near(tail,s.Yaw-dir*span/2,1e-4),spec+" tail at the arc's end where the blade enters");
                    float mid=(s.HitAt+s.HitUntil)/2;NativeSlashFx.Frame(s,look,mid,false,out float t2,out float h2,out float a2,out _);
                    Check(Near(h2,t2+dir*span*MeleeSwing.Swept(visible,.5f,span),1e-4),spec+" the head follows the visible blade");
                    NativeSlashFx.Frame(s,look,s.HitUntil-.001f,false,out float t3,out float h3,out float a3,out _);
                    Check((h3-t3)*dir>0&&(h3-h2)*dir>=-1e-5&&(h2-t2)*dir>=-1e-5,spec+" the crescent opens and grows the blade's way");
                    Check(NativeSlashFx.Frame(s,look,s.HitUntil+look.Fade*.5f,false,out _,out _,out float fading,out _)&&fading<a3&&!NativeSlashFx.Frame(s,look,s.HitUntil+look.Fade+.001f,false,out _,out _,out _,out _),spec+" a fast fade after the window");
                    Check(Math.Abs(h2-t2)<=Math.Min(look.MaxArc,s.Arc)+1e-4,spec+" never wider than the swing's arc");
                    if(look.Twin)Check(!NativeSlashFx.Frame(s,look,s.HitAt+.001f,true,out _,out _,out _,out _)&&NativeSlashFx.Frame(s,look,s.HitAt+(s.HitUntil-s.HitAt)*.3f,true,out _,out _,out _,out _),spec+" the twin arc trails a quarter window");
                }
                Check(NativeSlashFx.Radius(look,10)<=look.MaxRadius&&NativeSlashFx.Radius(look,.1f)>=look.MinRadius,spec+" radius bounded");
            }
            NativeSlashFx.Look("daggers",out var d);NativeSlashFx.Look("sword",out var sw);NativeSlashFx.Look("greatsword",out var g);NativeSlashFx.Look("shield",out var sh);
            Check(d.Twin&&!sw.Twin&&d.Fade<sw.Fade&&sw.Fade<g.Fade&&g.Thickness>sw.Thickness&&g.Under>sw.Under&&sh.MaxArc<=1&&sh.Thickness>=g.Thickness,"daggers fast twins, sword clean, greatsword wide and heavy, shield a short bash");
            var fist=new Actor{SourceSpec="emperor"};var launch=new Actor{SourceSpec="sword",SpecialReleased=true,SpecialKind="sword-launch",SpecialUntil=5};var strike=new Actor{SourceSpec="greatsword"};
            Check(!NativeSlashFx.Replaces(fist,1)&&!NativeSlashFx.Replaces(launch,1)&&NativeSlashFx.Replaces(launch,6)&&NativeSlashFx.Replaces(strike,1)&&!NativeSlashFx.Covers("siren"),"crescents replace the ribbon for standard strikes only");
        }
        private static void Afterimages()
        {
            var a=new Actor{SourceSpec="sword",Hp=100,RollAt=1,VisualRollDuration=.3f};
            Check(NativeAfterimages.Eligible(a,1.1f)&&!NativeAfterimages.Eligible(a,1.31f)&&!NativeAfterimages.Eligible(new Actor{SourceSpec="ansatsuken",Hp=100,RollAt=1,VisualRollDuration=.3f},1.1f)&&!NativeAfterimages.Eligible(new Actor{SourceSpec="sword",Hp=0,RollAt=1,VisualRollDuration=.3f},1.1f),"afterimages on the replicated roll window; not the Demon Step, not the dead");
            var ghosts=new NativeAfterimages();
            Check(!ghosts.Due(1,Vector3.Zero,1,1.01,1),"the first frame anchors the roll");
            Check(!ghosts.Due(1,new Vector3((float)NativeAfterimages.Spacing*.9f,0,0),1,1.02,1)&&ghosts.Due(1,new Vector3((float)NativeAfterimages.Spacing,0,0),1,1.03,1),"a copy every Spacing metres");
            int slot=ghosts.Take(1,new Vector3(.6f,0,0),1,1.03,1);
            Check(ghosts.Live(slot,1.03)&&ghosts.Live(slot,1.4)&&!ghosts.Live(slot,1.03+NativeAfterimages.Life),"a copy lives .4 s");
            Check(!ghosts.Due(1,new Vector3(.7f,0,0),1,1.04,1)&&ghosts.Due(1,new Vector3(.7f,0,0),1,1.04,.3f)==false&&!ghosts.Due(1,new Vector3(1.6f,0,0),1,1.05,.3f)&&ghosts.Due(1,new Vector3(1.7f,0,0),1,1.05,.3f),"reduced effects: sparser copies");
            Check(!ghosts.Due(1,new Vector3(5,0,0),2,2.01,1),"a new roll anchors again");
            Check(Near(NativeAfterimages.Fade(0),NativeAfterimages.Opacity)&&NativeAfterimages.Fade(NativeAfterimages.Life)==0&&NativeAfterimages.Fade(.2)<NativeAfterimages.Opacity,"copies fade over their life");
            var c=NativeAfterimages.Color("#70e1d0");var t=NativeGlowRules.ArmorTint("#70e1d0");Check(Near(c.R,t.X*NativeAfterimages.Gain)&&Near(c.B,t.Z*NativeAfterimages.Gain),"the accent colour");
            ghosts.Clear();Check(!ghosts.Live(slot,1.1),"clear hides every copy");
        }
        private static void HitFlash()
        {
            Check(NativeHitFlash.Life>=.06&&NativeHitFlash.Life<=.1&&NativeHitFlash.HeadLife>=.06&&NativeHitFlash.HeadLife<=.1&&NativeHitFlash.HeadLife>NativeHitFlash.Life,"60-100 ms flashes, the headshot's longer");
            Check(NativeHitFlash.Code(-.01,false)==1&&NativeHitFlash.Code(NativeHitFlash.Life,false)==1&&NativeHitFlash.Code(NativeHitFlash.HeadLife,true)==1,"no flash outside its window (code 1)");
            Check(NativeHitFlash.Code(0,false)==2&&NativeHitFlash.Code(0,true)==4,"codes: 1 + k a hit, 3 + k a headshot");
            for(double age=0;age<.1;age+=.01)foreach(bool head in new[]{false,true})
            {
                float code=NativeHitFlash.Code(age,head);NativeHitFlash.Decode(code,out float k,out bool h);
                if(code>1)Check(h==head&&Near(k,NativeHitFlash.Strength(age,head),1e-5),"the code decodes");
            }
            Check(NativeHitFlash.Code(0,false,.3f)<NativeHitFlash.Code(0,false,1),"reduced effects: a softer flash");
            var white=NativeHitFlash.Color(1,false);var head0=NativeHitFlash.Color(1,true);var redHead=NativeHitFlash.Color(.2f,true);
            Check(Near(white.R,NativeHitFlash.Gain)&&Near(white.B,NativeHitFlash.Gain)&&Near(head0.R,NativeHitFlash.HeadGain)&&head0.R>white.R&&redHead.G<redHead.R*.3f,"white at the hit, brighter headshots decaying through red");
            Check(NativeHitFlash.Blend(1,true,0)>NativeHitFlash.Blend(1,false,0)&&NativeHitFlash.Blend(1,false,1)>NativeHitFlash.Blend(1,false,0)&&NativeHitFlash.Blend(0,false,1)==0,"blend: stronger for headshots and on the silhouette");
        }
        private static void Charge()
        {
            double rate=NativeChargeFx.StreakRate(1,false);NativeChargeFx.Window(rate,2,out long first,out long last);int live=0;
            for(long k=first;k<=last;k++)if(NativeChargeFx.Streak(3,k,rate,2,out var dir,out float u,out float alpha,out float white)){live++;Check(Near(dir.Length(),1,1e-4)&&u>=0&&u<1&&alpha>=0&&alpha<=1&&white>=0&&white<=1,"streak state");}
            Check(live>=rate*NativeChargeFx.LifeMin*.7&&live<=rate*NativeChargeFx.LifeMax+1,"about rate x life streaks alive ("+live+")");
            Check(!NativeChargeFx.Streak(3,last+1,rate,2,out _,out _,out _,out _),"no streak before its slot");
            Check(NativeChargeFx.StreakRate(.3f,false)<rate&&NativeChargeFx.StreakRate(1,true)<rate,"fewer streaks on the reduced tiers");
            Check(NativeChargeFx.Ring(10,10*NativeChargeFx.RingEvery,out float s0,out float a0)&&s0==1&&a0==0&&NativeChargeFx.Ring(10,10*NativeChargeFx.RingEvery+NativeChargeFx.RingLife*.5,out float s1,out float a1)&&a1>.99f&&s1<1&&!NativeChargeFx.Ring(10,10*NativeChargeFx.RingEvery+NativeChargeFx.RingLife,out _,out _),"rings shrink 1 to .33 and pulse in and out");
        }
        private static void Text(string root)
        {
            string R(string p)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",p));
            string draw=Code(R("Runtime/NativeWeaponFxDraw.cs")),present=Code(R("Runtime/NativeWeaponFxPresentation.cs")),weapon=Code(R("Runtime/NativeWeaponPresentation.cs")),arena=Code(R("Runtime/ArenaPresentation.cs"));
            string source=Code(R("Runtime/NativeSourcePresentation.cs")),models=Code(R("Runtime/NativeSourceModels.cs")),body=Code(R("Runtime/NativeBodyChipPresentation.cs")),glow=Code(R("Runtime/NativeCombatGlow.cs"));
            string fx=R("Resources/NativeWeaponFx.shader"),ghost=R("Resources/NativeWeaponGhost.shader"),lit=R("Resources/NativeSourceLit.shader"),gate=R("Editor/NativeBuildChecks.cs");
            // Hooks.
            Check(weapon.Contains("MuzzleFx(rig,a,time,first);")&&!weapon.Contains("NativeWeaponDressing.MuzzleFlash("),"the muzzle flash replaces the source cone on the same socket, layer and camera");
            Check(weapon.Contains("NativeSlashFx.Replaces(a,time)")&&weapon.Contains("WeaponFxFrame(time);")&&weapon.Contains("ClearWeaponFx();")&&weapon.Contains("DisposeWeaponFx();"),"crescents replace the standard ribbon; the frame, clear and dispose hooks");
            Check(arena.Contains("ObserveHits(s);BeginWeaponFx(s);")&&arena.Contains("meleeFlash?.Render(arena,camera,fields,WeaponEffectsQuality);WeaponFx(arena,camera,fields);"),"Render begins the drawer; RenderGlow queues the projectile effects and submits on both paths");
            Check(source.Contains("float flash=dressed&&!first?HitFlashCode(a.Id,time):1;")&&source.Contains("look:look,hitFlash:flash);")&&source.Contains("hitFlash:flash);"),"the hit flash on arena rigs' bodies and hats, never the first-person arms");
            Check(models.Contains("float hitFlash=1)")&&models.Contains("d.Accents.Add(new Vector4(ac.r,ac.g,ac.b,hitFlash));"),"the flash code rides the accent tint's w: no new per-instance array, batches unchanged");
            Check(body.Contains("ChargeFx(socket,a,color,time,first);"),"the charge streaks follow the orb's socket, layer and camera");
            // Owner 2026-10-20: the muzzle light left the Unity light pool for its own shader slots (MuzzleFlashChecks); the pool keeps its seven.
            Check(!Regex.IsMatch(glow,@"\bPulse\(")&&!glow.Contains("NativeMuzzleFx")&&weapon.Length>0,"no muzzle pulse takes a projectile light (the muzzle light has its own shader slots)");
            Check(glow.Contains("projectileLights=new Light[3],impactLights=new Light[2],armorLights=new Light[2];")&&Regex.Matches(glow,@"AddComponent<Light>").Count==1&&!present.Contains("Light>")&&!draw.Contains("Light>"),"no light beyond the seven pooled ones");
            Check(present.Contains("NativeMeleeRules.Begin(w,pilot.Stats,a.AttackAt,index,yaw)")&&present.Contains("sw.HitAt=a.SwingAt;sw.HitUntil=a.SwingUntil;")&&present.Contains("float yaw=a.CommitYaw*Mathf.Deg2Rad;int index=a.Combo+1;"),"slashes from the replicated swing window, committed yaw and attack index");
            Check(present.Contains("NativeAfterimages.Eligible(a,time)")&&present.Contains("afterimages.Due(a.Id,at,a.RollAt,time,q)")&&present.Contains("FirstPerson&&a.Id==LocalId"),"afterimages on the replicated roll, not the first-person self");
            Check(present.Contains("draw.Submit(camera,FirstPerson&&!ScopeActive?ViewCamera:null);"),"first person: the view mesh to the view-model camera");
            Check(!present.Contains("arena.Time=")&&!present.Contains(".Hp=")&&!present.Contains("Events.Add(")&&!draw.Contains("arena."),"presentation only: nothing writes the match");
            // The drawer.
            Check(draw.Contains("world.Draw(material,0,camera,worldProperties)")&&draw.Contains("view.Draw(material,10,viewCamera,viewProperties)")&&draw.Contains("renderQueue=(int)RenderQueue.Transparent+6"),"two draws: the match camera on layer 0, the view-model camera on layer 10");
            Check(draw.Contains("worldProperties.SetFloat(SoftId,!Low&&(camera.depthTextureMode&DepthTextureMode.Depth)!=0?1:0)")&&draw.Contains("viewProperties.SetFloat(SoftId,0)"),"soft particles only where the match camera has a depth texture, not on the low tier");
            var queue=Between(draw,"public void Begin()","public void Submit(");var structs=new HashSet<string>{"Vector3","Vector4","Vector2","Color","Vertex"};
            var made=Regex.Matches(queue,@"new\s+(\w+)\s*([\[({])").Cast<Match>().Where(m=>m.Groups[2].Value=="["||!structs.Contains(m.Groups[1].Value)).Select(m=>m.Value).ToList();
            Check(made.Count==0,"queueing allocates nothing ("+string.Join(", ",made)+")");
            Check(draw.Contains("CapturedAnimation.Release(world.Mesh)")&&draw.Contains("CapturedAnimation.Release(view.Mesh)")&&draw.Contains("CapturedAnimation.Release(material)"),"dispose releases");
            Check(draw.Contains("public const float Petal=0,Crescent=1,Ribbon=2,GlowMode=3,WispMode=4,StreakMode=5,FlameMode=6,ShardMode=7;")&&fx.Contains("if(mode<0.5)shape=FxPetal(uv);")&&fx.Contains("else if(mode<1.5)shape=FxCrescent(uv,w,i.data.y);")&&fx.Contains("else if(mode<4.5)shape=float2(FxWisp(uv,w),0.0);")&&fx.Contains("shape=mode<6.5?FxFlame(t,uv.y,w):FxShard(t,uv.y);"),"drawer modes match the shader");
            // Shaders.
            Check(fx.Contains("Blend One OneMinusSrcAlpha")&&fx.Contains("ZWrite Off")&&fx.Contains("\"Queue\"=\"Transparent+6\"")&&fx.Contains("#pragma target 3.5")&&fx.Contains("UNITY_APPLY_FOG_COLOR(i.fogCoord,c,float4(unity_FogColor.rgb*c.a,0));")&&fx.Contains("UNITY_COLORSPACE_GAMMA"),"weapon fx: premultiplied, no depth write, fog, gamma-safe");
            Check(fx.Contains("c*=_WeaponFxSoft>0.5?FxSoftFade(scene,i.screen.z,i.data.z):1.0;"),"weapon fx: soft particles selected by the draw");
            Check(ghost.Contains("Blend One One")&&ghost.Contains("#pragma multi_compile_instancing")&&ghost.Contains("UNITY_DEFINE_INSTANCED_PROP(float4,_BodyTint)")&&ghost.Contains("GhostFresnel(facing)*tint.a"),"ghost: additive, instanced, fresnel by the copy's opacity");
            Check(lit.Contains("void StageTone(Input i,SurfaceOutput o,inout fixed4 color)")&&lit.Contains("StageGrade(i,color);\n  HitFlash(i,color);")&&lit.Contains("float code=UNITY_ACCESS_INSTANCED_PROP(Props,_AccentTint).w;if(code<1.001)return;"),"pilot shader: the flash after the grade, a no-op at code 1");
            Check(lit.Contains("float3 red=head>0.5?float3(1.0,0.16,0.1):float3(1.0,0.42,0.32);")&&lit.Contains("(k-(head>0.5?0.45:0.2))/(head>0.5?0.55:0.8)")&&lit.Contains("*(head>0.5?2.6:1.6);")&&lit.Contains("k*(head>0.5?0.95:0.82)*(0.82+0.18*rim)"),"the shader's flash is NativeHitFlash.Color and Blend");
            Check(Near(NativeHitFlash.Gain,1.6)&&Near(NativeHitFlash.HeadGain,2.6),"the shader's gains are Core's");
            foreach(var (name,text,begin,end) in new[]{("NativeWeaponFx",fx,"WEAPON FX SHARED BEGIN","WEAPON FX SHARED END"),("NativeWeaponGhost",ghost,"GHOST SHARED BEGIN","GHOST SHARED END"),("NativeSourceLit hit flash",lit,"HIT FLASH SHARED BEGIN","HIT FLASH SHARED END")})
            {
                var block=Code(Between(text,begin,end));
                Check(!block.Contains("mul(")&&!block.Contains("tex2D")&&!Regex.IsMatch(block,@"\bline\b|\bsample\b|\bpoint\b"),name+" SHARED: no matrices, texture reads or HLSL-reserved names");
                var ints=Regex.Matches(block,@"(?<![\w.])\d+(?![\w.])").Cast<Match>().Select(m=>m.Value).ToList();Check(ints.Count==0,name+" SHARED: float literals only ("+string.Join(", ",ints)+")");
            }
            Check(gate.Contains("foreach(var name in new[]{\"NativeWeaponFx\",\"NativeWeaponGhost\"}){var shader=Resources.Load<Shader>(name);if(shader==null||ShaderUtil.ShaderHasError(shader))"),"both shaders are build-checked");
            foreach(var name in new[]{"Resources/NativeWeaponFx.shader","Resources/NativeWeaponGhost.shader","Runtime/NativeWeaponFxDraw.cs","Runtime/NativeWeaponFxPresentation.cs","Core/NativeWeaponFx.cs"})
                Check(Regex.IsMatch(R(name+".meta"),@"^guid: [0-9a-f]{32}$",RegexOptions.Multiline),name+" has its .meta");
        }
    }
}
#endif
