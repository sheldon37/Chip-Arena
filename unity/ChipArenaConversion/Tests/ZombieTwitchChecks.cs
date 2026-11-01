#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
namespace ChipArena.UnityConversion
{
    // The horde's twitch toward the pilot (owner 2026-10-20: "their movement still seems too fast (Not talking about their
    // movement speed, rather their animations, they look like they're 'twitching' towards the player)"; docs/ZOMBIE-LOCOMOTION.md,
    // "Twitching under fire"). A Central Station chase under pistol fire: the authority emulated at 60 Hz from StepPveHordeCore and
    // HitPveAdd (a pistol hit staggers a shambler or a runner for the content's .45 s and knocks it back at 175/80 m/s; the add
    // lunges into its blow), steered as StructureNavMoveCore steers it on a 1 m grid of four links a node (the Unity maps' empty
    // hazard list keeps it off the straight line), presented as Runtime/NativeHordeKindPresentation drives the horde (the clock,
    // the presented root on Unity's interpolated transform, the reactions from the hits, the gait, the pop killer). The scene uses
    // only what the shared base f930684 already had, so the same measurement compiled against it gives the before numbers quoted.
    internal static class ZombieTwitchChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie twitch: "+what);count++;}
#if !ZOMBIE_TWITCH_BASE
        public static void Run(string root)
        {
            Stumble(out string stumble);Heading(out string heading);Restart(out string restart);Hurt(out string hurt);Chases(out string chases);Contact(out string contact);Allocation();Source(root);
            Console.WriteLine("PASS zombie twitch ("+count+" checks: "+chases+"; "+stumble+"; "+restart+"; "+hurt+"; "+heading+"; "+contact+"; 0 bytes per frame; presentation only)");
        }

        // ---- the scenes: the offenders measured through the whole presentation ----
        private static void Chases(out string summary)
        {
            // Measured with this scene at the shared base f930684 (ZombieTwitchScene compiled against it): head p50/p95/max, chest
            // p95, upper joints p95 (rad/s, world), the largest one-frame turn of a trunk or head joint within .3 s of a hit.
            var sh=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Shambler,Seconds=40,Seed=3});
            var ru=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Runner,Seconds=40,Seed=5});
            var gu=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Shambler,Seconds=40,Seed=7,Guest=true});
            var lo=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Shambler,Seconds=40,Seed=9,Fps=30});
            var calm=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Shambler,Seconds=40,Seed=11,NoFire=true});
            foreach(var (m,n) in new[]{(sh,"shambler"),(ru,"runner"),(gu,"guest shambler"),(lo,"30 fps shambler")})Check(m.Hits>40&&m.Frames>(n=="30 fps shambler"?400:1000),n+": the scene shoots and chases ("+m.Hits+" hits, "+m.Frames+" frames)");
            // (Thresholds: the values measured here with about a fifth to spare; each sits far under the base's.)
            Check(sh.HeadP95<8.5f&&sh.HeadMax<15&&sh.ChestP95<4f&&sh.UpperP95<4.2f,"shambler under pistol fire: head "+sh.HeadP95.ToString("0.0")+" rad/s p95 ("+sh.HeadMax.ToString("0.0")+" max), chest "+sh.ChestP95.ToString("0.0")+", upper joints "+sh.UpperP95.ToString("0.0")+" (base 25.0 / 88.8, 10.9, 13.2)");
            Check(ru.HeadP95<9.5f&&ru.HeadMax<17&&ru.ChestP95<4.5f&&ru.UpperP95<4.4f,"runner under pistol fire: head "+ru.HeadP95.ToString("0.0")+" p95 ("+ru.HeadMax.ToString("0.0")+" max), chest "+ru.ChestP95.ToString("0.0")+", upper "+ru.UpperP95.ToString("0.0")+" (base 25.9 / 97.2, 8.8, 12.4)");
            Check(gu.HeadP95<9f&&gu.HeadMax<17,"a 20 Hz guest's shambler under fire: head "+gu.HeadP95.ToString("0.0")+" p95 ("+gu.HeadMax.ToString("0.0")+" max; base 26.0 / 113.0)");
            Check(lo.HeadP95<8.5f,"at 30 fps: head "+lo.HeadP95.ToString("0.0")+" p95 (base 19.6)");
            Check(sh.TrunkPop<.09f&&ru.TrunkPop<.09f&&gu.TrunkPop<.09f&&lo.TrunkPop<.09f,"no trunk or head joint pops on a hit: at most "+Math.Max(sh.TrunkPop,Math.Max(ru.TrunkPop,gu.TrunkPop)).ToString("0.000")+" rad in a 144 fps frame within .3 s of one (base 0.458, 0.343 and 0.434: a reel started over at a pistol's every hit)");
            // Under fire the pose now moves about as it does in a plain chase; what is left is the authority's root (see the note).
            Check(sh.HeadP95<calm.HeadP95*1.9f,"the shambler's head under fire is within twice a plain chase's ("+sh.HeadP95.ToString("0.0")+" against "+calm.HeadP95.ToString("0.0")+" rad/s p95; base 25.0 against 5.9)");
            Check(sh.YawP95<1.1f&&calm.YawP95<2.4f,"the facing through the grid route's zig-zag: "+calm.YawP95.ToString("0.00")+" rad/s p95 chasing, "+sh.YawP95.ToString("0.00")+" under fire (base 3.11 and 1.37)");
            // (The root follows the authority's body: its knock-back and stop-start are the authority's, kept within the body's reach.)
            Check(sh.DevP95<.13f&&sh.DevMax<.16f,"the drawn root stays on the authority's body as before ("+(sh.DevP95*100).ToString("0.0")+" cm p95, "+(sh.DevMax*100).ToString("0.0")+" max; base 10.5, 13.0) while the knock-back turns it round at "+sh.RootAccP95.ToString("0")+" m/s^2 p95 (the authority's)");
            var again=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Shambler,Seconds=40,Seed=3});Check(again.Hash==sh.Hash,"the scene is a pure function of its inputs (two runs agree to the bit)");
            summary="a Central Station chase under pistol fire (a hit every .42 s staggers and knocks back; the grid route's zig-zag; 144 Hz +-15% host): shambler head "+sh.HeadP50.ToString("0.0")+"/"+sh.HeadP95.ToString("0.0")+"/"+sh.HeadMax.ToString("0.0")+" rad/s p50/p95/max (base 8.7/25.0/88.8), chest p95 "+sh.ChestP95.ToString("0.0")+" (base 10.9), upper joints p95 "+sh.UpperP95.ToString("0.0")+" (base 13.2); runner head "+ru.HeadP95.ToString("0.0")+" p95, "+ru.HeadMax.ToString("0.0")+" max (base 25.9, 97.2); 20 Hz guest "+gu.HeadP95.ToString("0.0")+" (base 26.0); 30 fps "+lo.HeadP95.ToString("0.0")+" (base 19.6); the worst one-frame trunk or head turn near a hit "+Math.Max(sh.TrunkPop,Math.Max(ru.TrunkPop,gu.TrunkPop)).ToString("0.000")+" rad (base 0.458); the facing "+calm.YawP95.ToString("0.00")+" rad/s p95 chasing (base 3.11); the root within "+(sh.DevP95*100).ToString("0")+" cm of the body p95 (as before)";
        }

        // ---- the authority's stagger: one continuous stumble, not a reel started over on each hit ----
        private static void Stumble(out string summary)
        {
            var r=new NativeHordeReact();r.Bind(NativeHordeKinds.Shambler);var w=StaggerWeight.Flinch;
            // A pistol's string: six hits .42 s apart, each a stagger of the content's .45 s.
            for(int i=0;i<6;i++)r.Add(1+.42f*i,new Vector2(0,-1),w,23,false,false,true,.45f);
            float dt=1/144f,lastW=0,lastClip=0,mostW=0,mostClip=0,peak=0,oldDrop=0,oldJump=0,lastOld=float.NaN,lastOldClip=float.NaN;bool first=true;
            for(float t=.9f;t<5;t+=dt)
            {
                float k=r.Stumble(t,out float clip);peak=Math.Max(peak,k);
                if(!first){mostW=Math.Max(mostW,Math.Abs(k-lastW));if(k>0&&lastW>0){Check(clip>=lastClip-1e-6f,"the stumble's take never runs backward");mostClip=Math.Max(mostClip,clip-lastClip);}}
                Check(clip<=NativeHordeReact.StumbleHold+1e-6f,"the take holds at its furthest point");
                // The old reel for comparison (Reel: the newest stagger's progress; its weight and take time as the gait read them).
                float reel=r.Reel(t),ow=float.IsNaN(reel)?0:Sm(reel/.12f)*(1-Sm((reel-.75f)/.45f))*.85f,oc=float.IsNaN(reel)?0:Math.Min(1.15f,reel*.9f);
                if(!float.IsNaN(lastOld)){oldDrop=Math.Max(oldDrop,lastOld-ow);oldJump=Math.Max(oldJump,lastOldClip-oc);}lastOld=ow;lastOldClip=oc;
                lastW=k;lastClip=clip;first=false;
            }
            Check(oldDrop>.5f&&oldJump>.5f,"the old reel dropped its weight and threw its take back at each new stagger ("+oldDrop.ToString("0.00")+", "+oldJump.ToString("0.00")+" s)");
            // (The steepest a link's own ease-in climbs: the quintic's 1.875 over the rise, at a pistol's share.)
            float slope=1.875f*NativeHordeReact.StumbleLight/NativeHordeReact.StumbleRise*dt*1.01f;
            Check(mostW<=slope,"the stumble's weight never steps on a new stagger (at most "+mostW.ToString("0.000")+" a 144 fps frame, its ease-in's own slope "+slope.ToString("0.000")+")");
            Check(mostClip<=dt+1e-5f,"its take runs at its capture's own pace, never jumping ("+(mostClip/dt).ToString("0.00")+" times real time at most; the old reel ran at "+(.9f/.45f).ToString("0.0")+")");
            Check(Math.Abs(peak-NativeHordeReact.StumbleLight)<1e-4f,"a pistol's (flinch-weight) stagger plays "+NativeHordeReact.StumbleLight+" of the full reel");
            Check(r.Stumble(.95f,out float c0)==0&&c0==0&&r.Stumble(1+.42f*5+.45f+NativeHordeReact.StumbleRelease+.01f,out _)==0,"it is not there before the first stagger and lets go after the last");
            // The weight by the hit: a heavy blow the full reel, a step-weight one three quarters.
            foreach(var (wt,want) in new[]{(StaggerWeight.Heavy,1f),(StaggerWeight.Step,NativeHordeReact.StumbleMedium)})
            {var h=new NativeHordeReact();h.Bind(0);h.Add(1,new Vector2(1,0),wt,48,true,false,true,.45f);float most=0;for(float t=1;t<2;t+=dt)most=Math.Max(most,h.Stumble(t,out _));Check(Math.Abs(most-want)<1e-3f,wt+" plays "+want+" of the reel ("+most.ToString("0.000")+")");}
            // A pure function of the hits and the time: asked in any order, two instances agree; a hit not yet reached is not read.
            var a=new NativeHordeReact();var b=new NativeHordeReact();a.Bind(0);b.Bind(0);
            for(int i=0;i<4;i++){a.Add(2+.3f*i,new Vector2(0,1),w,20,false,i==2,true,.45f);b.Add(2+.3f*i,new Vector2(0,1),w,20,false,i==2,true,.45f);}
            for(int i=0;i<200;i++){float t=2+(i*37%200)/100f;Check(a.Stumble(t,out float ca)==b.Stumble(t,out float cb)&&ca==cb,"two peers fed the same hits agree");}
            Check(a.Stumble(2.1f,out _)==Ref(2.1f),"a later hit does not reach back before it (a guest learns of hits ahead of its clock)");
            // A long string past the hit ring (a stagger every .1 s for 3 s): the ring forgets the oldest, the stumble holds on.
            var c=new NativeHordeReact();c.Bind(0);float prev=float.NaN,worst=0;
            for(int i=0;i<300;i++){float t=5+i/100f;if(i%10==0)c.Add(t,new Vector2(0,1),w,20,false,false,true,.45f);c.Step(t);float k=c.Stumble(t,out float cc);if(!float.IsNaN(prev))worst=Math.Max(worst,Math.Abs(k-prev));prev=k;if(t>5.6f)Check(Math.Abs(cc-NativeHordeReact.StumbleHold)<1e-5f,"a long string holds the take at its furthest point");}
            Check(worst<=1.875f*NativeHordeReact.StumbleLight/NativeHordeReact.StumbleRise*.01f*1.01f,"a long string never steps the weight as the ring forgets its oldest hits ("+worst.ToString("0.000")+")");
            c.Step(4);Check(c.Count==0&&c.Stumble(4,out _)==0,"a rewind forgets the stumble");
            summary="a string of pistol staggers is one stumble: its weight steps at most "+mostW.ToString("0.000")+" a frame and its take never jumps (the old reel dropped "+oldDrop.ToString("0.00")+" of its weight and threw its take back "+oldJump.ToString("0.00")+" s at each, playing it at 2 times its pace), "+NativeHordeReact.StumbleLight+" of the reel for a pistol";
            float Ref(float t){var x=new NativeHordeReact();x.Bind(0);x.Add(2,new Vector2(0,1),w,20,false,false,true,.45f);return x.Stumble(t,out _);}
        }

        // ---- the facing: the grid route's zig-zag smoothed; a wind-up still faces the authority's aim ----
        private static void Heading(out string summary)
        {
            Check(new BossRootFollow().HeadingTau==0,"the boss motion library leaves its heading smoothing off (every boss unchanged)");
            float Turns(bool off,out float spread)
            {
                var route=ZombieTwitchScene.ZigZag(3.4f,6);var flow=new HordeFlow();flow.Configure(NativeHordeKinds.Shambler);var rates=new List<float>();float prev=float.NaN;spread=0;double w=0;var rnd=new Random(4);
                while(w<5.8)
                {
                    float dt=1/144f*(1+.3f*(float)(rnd.NextDouble()-.5));w+=dt;int k=(int)Math.Floor(w*60);var tk=route[Math.Min(route.Count-1,k)];
                    if(off)flow.Follow.HeadingTau=0;flow.Host(w,k/60.0,tk.p,tk.v,true,tk.yaw,dt,false);
                    if(w>1.2){if(!float.IsNaN(prev))rates.Add(Math.Abs(BossMath.WrapDeg(flow.Yaw-prev))*BossMath.Deg/dt);spread=Math.Max(spread,Math.Abs(BossMath.WrapDeg(flow.Yaw-45)));}prev=flow.Yaw;
                }
                return ZombieTwitchScene.P(rates,.95);
            }
            float raw=Turns(true,out float rawSpread),smooth=Turns(false,out float smoothSpread);
            Check(smooth<raw*.6f,"the facing through a 45 degree grid route's zig-zag turns at "+smooth.ToString("0.0")+" rad/s p95 (the authority's facing taken as it is: "+raw.ToString("0.0")+")");
            Check(smoothSpread<rawSpread*.75f&&smoothSpread<24,"and keeps within "+smoothSpread.ToString("0")+" degrees of the route's heading ("+rawSpread.ToString("0")+")");
            // A wind-up turns fast onto the authority's aim, as before: standing 50 degrees off, the blow faces it by the strike.
            float Aim(bool off){var flow=new HordeFlow();flow.Configure(NativeHordeKinds.Shambler);var p=new Vector3(0,.85f,0);double w=0;for(int i=0;i<60;i++){w+=1/144.0;if(off)flow.Follow.HeadingTau=0;flow.Host(w,w,p,Vector3.Zero,true,0,1/144f,false);}
                for(int i=0;i<49;i++){w+=1/144.0;if(off)flow.Follow.HeadingTau=0;flow.Host(w,w,p,Vector3.Zero,true,50,1/144f,true);}return Math.Abs(BossMath.WrapDeg(flow.Yaw-50));}
            float aimNow=Aim(false),aimRaw=Aim(true);
            Check(aimNow<6&&Math.Abs(aimNow-aimRaw)<.5f,"a wind-up still turns onto the authority's aim by the strike ("+aimNow.ToString("0.0")+" degrees left, "+aimRaw.ToString("0.0")+" with the smoothing off)");
            summary="the facing through the grid route's zig-zag "+smooth.ToString("0.0")+" rad/s p95 (the authority's facing as it is: "+raw.ToString("0.0")+"), within "+smoothSpread.ToString("0")+" degrees of the route ("+rawSpread.ToString("0")+"), a wind-up still on the aim";
        }

        // ---- the restart after a stagger: the take crosses from standing to the chase in a third of a second, feet planted ----
        private static void Restart(out string summary)
        {
            float most=ZombieTwitchScene.Restart(out float slip,out int planted);
            Check(most<.06f,"a stop and a restart at full speed turn no trunk or arm joint more than "+most.ToString("0.000")+" rad a 144 fps frame (base: 0.291, the take crossing from standing to the chase in two or three frames)");
            Check(planted>400&&slip<.015f,"and planted feet hold on the drawn rig through it ("+(slip*1000).ToString("0.00")+" mm per frame over "+planted+" planted frames)");
            summary="a stop and a restart at full speed: the stand-to-move blend crosses in "+(1/NativeHordeGait.MoveSlew).ToString("0.00")+" s, no trunk or arm joint over "+most.ToString("0.000")+" rad a frame (base 0.291), planted feet within "+(slip*1000).ToString("0.00")+" mm";
        }

        // ---- a hit's sag and snap come in, never in one frame ----
        private static void Hurt(out string summary)
        {
            float most=ZombieTwitchScene.Headshot(out float first);var p=new Vector3(0,.85f,0);
            Check(most<.105f&&first<.03f,"a headshot that takes a third of the health whips the head at most "+most.ToString("0.000")+" rad a 144 fps frame, from rest ("+first.ToString("0.000")+" in its first three frames; base 0.131 and 0.131: the snap and the sag landed in one frame)");
            // The sag still settles where it did.
            float Head(float hp){var r=new ZombieTwitchScene.Rig(NativeHordeKinds.Shambler,71003);double t=0;float sum=0;int n=0;while(t<3){t+=1/60.0;r.Frame(t,t,p,Vector3.Zero,0,1/60f,hp);if(t>2.5){r.Pose.World();sum+=r.Pose.WorldPosition[CapturedRig.Head].Y;n++;}}return sum/n;}
            Check(Head(.15f)<Head(1)-.02f,"a hurt add still sags");
            summary="a headshot's sag and snap turn the head at most "+most.ToString("0.000")+" rad a frame (eased over "+NativeHordeGait.HurtHalfLife+" s and "+NativeHordeGait.HitSpread+" s)";
        }

        // ---- the blow still lands on the authority's hit, through a stumble ----
        private static void Contact(out string summary)
        {
            int cases=0;float latest=0;
            foreach(int seed in new[]{1,2,3})
            {
                var add=new ZombieTwitchScene.Rig(NativeHordeKinds.Shambler,72000+seed);var p=new Vector3(0,.85f,0);double w=0;float last=2.2f+.01f*seed,strike=.34f;bool hit=false;float prevClip=-1;
                add.React.Add(1.2f,new Vector2(0,-1),StaggerWeight.Flinch,23,false,false,true,.45f);add.React.Add(1.62f,new Vector2(0,-1),StaggerWeight.Flinch,23,false,false,true,.45f);
                while(w<last+1.1f)
                {
                    float dt=1/144f;w+=dt;add.Frame(w,w,p,Vector3.Zero,0,dt,.6f,last,strike,1.05f);
                    if(add.Gait.AttackClip<0)continue;var c=NativeZombieClips.Clip(add.Gait.AttackClip);float t=(float)w-last;
                    Check(add.Gait.AttackClipTime>=prevClip-1e-5f,"the blow's take never runs backward");prevClip=add.Gait.AttackClipTime;
                    if(!hit&&add.Gait.AttackClipTime>=c.Marks[2]-1e-5f){hit=true;float late=t-strike;Check(late>=-1e-4f&&late<dt+1e-4f,"a blow started as the stumble lets go lands on the authority's hit within a frame ("+(late*1000).ToString("0.0")+" ms)");latest=Math.Max(latest,late);cases++;}
                }
                Check(hit,"the blow lands");
            }
            summary="a blow started as a stumble lets go still lands within one frame of LastAttack + strike ("+cases+" blows, latest "+(latest*1000).ToString("0.0")+" ms)";
        }

        // ---- no allocation per frame ----
        private static void Allocation()
        {
            var m=ZombieTwitchScene.Run(new ZombieTwitchScene.Setup{Kind=NativeHordeKinds.Shambler,Seconds=8,Seed=13,CountBytes=true});
            Check(m.Bytes==0,"the clock, the flow with its heading, the stumble, the reactions, the gait and the pop killer allocate nothing per frame ("+m.Bytes+" bytes over "+m.Frames+" frames)");
        }

        // ---- presentation only ----
        private static void Source(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena",f));
            foreach(var f in new[]{"Runtime/NativePveHorde.cs","Runtime/NativeStructureNavigation.cs","Runtime/NativeExpeditionZoneEnemies.cs","Core/NativeZoneCombat.cs","Core/NativeHordeKinds.cs","Core/NativePveParity.cs"})
            {var s=R(f);foreach(var api in new[]{"Stumble(","HeadingTau","MoveSlew","HurtHalfLife","HitSpread","LifeAccelEase"})Check(!s.Contains(api),f+" (the authority's) never reads the presentation's "+api);}
            foreach(var f in new[]{"Core/NativeHordeReact.cs","Core/NativeHordeLife.cs","Core/NativeHordeFlow.cs"}){var s=R(f);Check(!s.Contains("UnityEngine")&&!s.Contains("System.Linq")&&!s.Contains("new Random("),f+" stays pure Core");}
            // (The heading smoothing is the horde's alone: no boss rig turns it on.)
            foreach(var f in new[]{"Core/NativeBossMotionRigs.cs","Core/NativeStationBossRig.cs","Core/NativeIronworksBossRig.cs"})Check(!R(f).Contains("HeadingTau"),f+" keeps the boss's facing as it was");
        }
#endif
        private static float Sm(float x)=>MeleeMath.Smooth(x);
    }

    /// <summary>The chase scene (Tests only): an authority and the horde presentation, as ZombieTwitchChecks describes.</summary>
    internal static class ZombieTwitchScene
    {
        internal struct Setup{public int Kind;public double Seconds;public int Seed;public bool Guest,CountBytes,NoFire;public float Fps;}
        internal struct Metrics{public float HeadP50,HeadP95,HeadMax,ChestP95,UpperP95,YawP95,RootAccP95,DevP95,DevMax,TrunkPop;public int Hits,Frames;public long Bytes;public ulong Hash;}
        internal static float P(List<float> l,double q){if(l.Count==0)return 0;var a=l.ToArray();Array.Sort(a);return a[Math.Min(a.Length-1,(int)(q*(a.Length-1)+.5))];}
        private static Vector3 Fwd(float yaw){float r=yaw*(float)Math.PI/180;return new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r));}
        private static float YawOf(Vector3 d)=>(float)(Math.Atan2(d.X,d.Z)*180/Math.PI);
        private static Vector3 Flat(Vector3 v)=>new Vector3(v.X,0,v.Z);
        private static Vector3 Unit(Vector3 v){float l=v.Length();return l>1e-6f?v/l:Vector3.Zero;}

        // ---- the floor: a 1 m grid, four links a node (the Unity maps' nav), searched by the authority's own A* ----
        private const int Cells=40;private static NativeNavSearch search;private static MapNavNode[] nodes;
        private static void Grid()
        {
            if(search!=null)return;nodes=new MapNavNode[Cells*Cells];var links=new List<MapNavLink>();
            for(int x=0;x<Cells;x++)for(int z=0;z<Cells;z++)nodes[x*Cells+z]=new MapNavNode{id="g-n"+(x*Cells+z),pos=new V3(x-Cells/2+.5f,.9f,z-Cells/2+.5f)};
            for(int x=0;x<Cells;x++)for(int z=0;z<Cells;z++){int i=x*Cells+z;if(z+1<Cells)links.Add(new MapNavLink{id="g-l"+links.Count,a=nodes[i].id,b=nodes[i+1].id});if(x+1<Cells)links.Add(new MapNavLink{id="g-l"+links.Count,a=nodes[i].id,b=nodes[i+Cells].id});}
            search=new NativeNavSearch(new MapNav{nodes=nodes,links=links.ToArray()});
        }
        private static MapNavNode Near(Vector3 p){int x=Math.Max(0,Math.Min(Cells-1,(int)Math.Floor(p.X+Cells/2f))),z=Math.Max(0,Math.Min(Cells-1,(int)Math.Floor(p.Z+Cells/2f)));return nodes[x*Cells+z];}
        private sealed class Route{public double At=-100;public Vector3 Target;public readonly List<string> Path=new List<string>();public int Step;}
        // StructureNavMoveCore: the route's next waypoint, replanned every .7 s or when the pilot moves 3 m.
        private static Vector3 Steer(Route r,Vector3 pos,Vector3 target,double time)
        {
            if(time-r.At>.7f||(r.Target-target).LengthSquared()>9){search.Find(Near(pos).id,Near(target).id,_=>true,r.Path);r.At=time;r.Target=target;r.Step=0;}
            if(r.Path.Count==0)return Vector3.Zero;
            while(r.Step<r.Path.Count-1&&(Pos(r.Path[r.Step])-pos).Length()<.65f)r.Step++;
            return Unit(Flat(Pos(r.Path[r.Step])-pos));
        }
        private static Vector3 Pos(string id){var n=nodes[int.Parse(id.Substring(3))].pos;return new Vector3(n.x,n.y,n.z);}
        /// <summary>A run at `speed` along a 45 degree grid route for `seconds` (60 Hz ticks: position, velocity, the authority's facing).</summary>
        internal static List<(Vector3 p,Vector3 v,float yaw)> ZigZag(float speed,double seconds)
        {
            Grid();var r=new Route();var p=new Vector3(-12,.9f,-12);var goal=new Vector3(12,.9f,12);var o=new List<(Vector3,Vector3,float)>();float yaw=45;
            for(int k=0;k<=seconds*60;k++){var d=Steer(r,p,goal,k/60.0);var v=d*speed;if(v.LengthSquared()>.001f)yaw=YawOf(v);o.Add((new Vector3(p.X,.85f,p.Z),v,yaw));p+=v/60;}
            return o;
        }

        // ---- the authority's add ----
        private sealed class Auth{public int Id;public PveAddStats St;public Vector3 P,Prev,V;public float Yaw,StaggerAt=-100,RecoverAt=-100,LastAttack=-100,StrikeAt=-100,Hp,MaxHp;public bool Struck=true,Alive=true;public Vector3 Ext;public double Spawn;public Route Route=new Route();}

        /// <summary>One horde add as the runtime presents it (the host's flow on its body, or a guest's on its snapshots).</summary>
        internal sealed class Rig
        {
            public readonly HordeClock Clock=new HordeClock();public readonly HordeFlow Flow=new HordeFlow();public readonly NativeHordeGait Gait=new NativeHordeGait();
            public readonly NativeHordeReact React=new NativeHordeReact();public readonly HordePoseBlend Blend=new HordePoseBlend();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
            public readonly int Kind,Id;public Vector3 Root;public float Yaw,UpperTurn,HeadTurn;private readonly Quaternion[] prev=new Quaternion[22];private Quaternion prevHead;private bool have;
            public float AttackSeen=-100,AttackBefore=-100,CancelAt=float.NaN,CancelFor=-100;
            public Rig(int kind,int id){Kind=kind;Id=id;React.Bind(kind);Flow.Configure(kind);}
            /// <summary>A host frame: the body at `body` (Unity's transform) with the simulation's velocity, at visual time `w`.</summary>
            public void Frame(double w,double sim,Vector3 body,Vector3 velocity,float yaw,float dt,float health,float lastAttack=-100,float strike=.34f,float recover=1.05f)
            {
                Clock.Step(w,dt,false);float now=(float)Clock.Time;float wa=now-lastAttack;Flow.Host(now,sim,body,velocity,true,yaw,dt,wa>=0&&wa<strike);
                Present(now,dt,health,lastAttack,strike,recover,new Vector3(0,.9f,40),false);
            }
            public void Present(float now,float hdt,float health,float lastAttack,float strike,float recover,Vector3 pilot,bool target)
            {
                var pos=Flow.Position;float ground=pos.Y-NativeHordeKinds.RootHeight(Kind,false);
                if(lastAttack!=AttackSeen){AttackBefore=AttackSeen;AttackSeen=lastAttack;}float attackAt=now<lastAttack&&AttackBefore>-99?AttackBefore:lastAttack;
                float age=now-attackAt;if(age<0||age>recover+.3f)age=float.NaN;float poseAge=age;bool cancelled=false;if(!float.IsNaN(age)&&CancelFor==attackAt&&now>=CancelAt){age=float.NaN;cancelled=true;}
                var f=new HordeMotionInput{Kind=Kind,Look=NativeHordeKinds.Look(Kind,Id),Id=Id,Seed=NativeHordeKinds.Seed(Id),Time=now,Yaw=Flow.Yaw,Ground=ground,Root=new Vector3(pos.X,ground+NativeHordeKinds.DrawRoot(Kind,false),pos.Z),Velocity=Flow.Velocity,
                    AttackAge=cancelled?poseAge:age,Strike=strike,Recover=recover,Attack=NativeHordeKinds.Attack.Swipe,Side=((int)(attackAt*7.31f)&1)==0?1:-1,Health=health,
                    Flow=true,YawRate=Flow.YawRate,Bank=Flow.Bank,Pitch=Flow.Pitch,Tier=NativeHordeModels.Full,Cancelled=cancelled,CancelAge=now-CancelAt,HasTarget=target,Target=pilot};
                Gait.Pose(f,Pose,React);float y=Gait.Yaw;var r=f.Root;float lead=Math.Min(NativeHordeGait.SwingLead(f.Attack),strike*.45f),ga=now-attackAt;
                Blend.Apply(Pose,ref r,ref y,hdt,ga>=strike-lead-.03f&&ga<=strike+.08f);Root=r;Yaw=y;Pose.World();
                UpperTurn=0;for(int j=1;j<CapturedRig.LHip;j++){float a=Angle(Pose.Rotation[j],prev[j]);if(have)UpperTurn=Math.Max(UpperTurn,a*(1/144f)/Math.Max(1e-4f,hdt));prev[j]=Pose.Rotation[j];}
                var head=Quaternion.CreateFromAxisAngle(Vector3.UnitY,Yaw*(float)Math.PI/180)*Mirror(Pose.WorldRotation[CapturedRig.Head]);HeadTurn=have?Angle(head,prevHead)*(1/144f)/Math.Max(1e-4f,hdt):0;prevHead=head;have=true;
            }
            public Vector3 Ankle(int i)=>Gait.ToWorld(Pose.WorldPosition[i==0?CapturedRig.LFoot:CapturedRig.RFoot],Root);
        }
        /// <summary>A run at 3.4 m/s stopped dead for .45 s twice (a stagger) and let go at once: the largest one-frame turn of a trunk
        /// or arm joint (144 fps) around the stops and restarts, and the worst planted-foot slip per frame.</summary>
        internal static float Restart(out float slip,out int planted)
        {
            var route=new List<(Vector3 p,Vector3 v)>();var at=new Vector3(0,.85f,0);
            for(int k=0;k<=6*60;k++){double t=k/60.0;var v=t<1?Vector3.Zero:t>=2.5&&t<2.95||t>=4.2&&t<4.65?Vector3.Zero:new Vector3(0,0,3.4f);route.Add((at,v));at+=v/60;}
            var add=new Rig(NativeHordeKinds.Shambler,71001);float most=0;slip=0;planted=0;double w=0;var last=new Vector3[2];var was=new bool[2];
            while(w<6)
            {
                float dt=1/144f;w+=dt;int k=(int)Math.Floor(w*60);var tk=route[Math.Min(route.Count-1,k)];add.Frame(w,k/60.0,tk.p,tk.v,0,dt,1);
                if(w>2.3&&w<5.3)most=Math.Max(most,add.UpperTurn);
                for(int i=0;i<2;i++){bool on=!add.Gait.FootAir[i]&&add.Gait.Heel[i]<=0;var ankle=add.Ankle(i);if(w>1.3&&on&&was[i]){slip=Math.Max(slip,new Vector2(ankle.X-last[i].X,ankle.Z-last[i].Z).Length());planted++;}last[i]=ankle;was[i]=on;}
            }
            return most;
        }
        /// <summary>Standing, a headshot from the front that takes a third of the health: the head's largest one-frame world turn
        /// (144 fps) and its largest in the three frames from the hit.</summary>
        internal static float Headshot(out float first)
        {
            var add=new Rig(NativeHordeKinds.Shambler,71002);double w=0;float most=0,health=1;var p=new Vector3(0,.85f,0);first=0;
            int after=-1;while(w<3){float dt=1/144f;w+=dt;if(w>=1.5&&health==1){health=.66f;add.React.Add((float)w,new Vector2(0,-1),StaggerWeight.Flinch,23,false,true,false,.45f);after=0;}add.Frame(w,w,p,Vector3.Zero,0,dt,health);if(w>1.2)most=Math.Max(most,add.HeadTurn);if(after>=0&&after<3){first=Math.Max(first,add.HeadTurn);after++;}}
            return most;
        }
        internal static float Angle(Quaternion a,Quaternion b){float d=Math.Abs(Quaternion.Dot(a,b));return 2*(float)Math.Acos(Math.Min(1,d));}
        internal static Quaternion Mirror(Quaternion q)=>new Quaternion(q.X,-q.Y,-q.Z,q.W);

        /// <summary>
        /// The chase: an add enters 10 m from the pilot at a seeded bearing and chases it along the grid route; the pilot shoots it
        /// with the pistol every .42 s from 10 m (23 damage, a headshot every fourth, the content's stagger and knock-back) until it
        /// dies or five seconds pass, and the next one comes. Measured on the drawn rig in the world, while it chases (no blow
        /// showing, over 2.2 m off): the head's and chest's turn rates, the upper joints', the facing's; the root's acceleration and
        /// its distance from the body; the largest one-frame turn of a trunk or head joint within .3 s of a hit.
        /// </summary>
        internal static Metrics Run(Setup s)
        {
            Grid();if(s.Fps<=0)s.Fps=144;bool shoot=!s.NoFire;
            var rnd=new Random(s.Seed);var pilot=new Vector3(.5f,.9f,.5f);const float Dt=1f/60;var st=NativeHordeKinds.ProposedRow(s.Kind);
            var head=new List<float>();var chest=new List<float>();var upper=new List<float>();var yawr=new List<float>();var acc=new List<float>();var dev=new List<float>();float trunkPop=0;
            int hits=0,frames=0,nextId=73000+s.Seed*100;long bytes=0;ulong hash=1469598103934665603UL;
            Auth a=null;Rig q=null;double sim=0,wall=0,nextShot=1,deadAt=-10;var visual=new NativeVisualClock();
            // the guest's stream: 20 Hz frames (45 ms, +-14 ms, 5% 120 ms late), each the add's position, facing, attack and health
            var stream=new List<(double arrive,double t,Vector3 p,float yaw,float last,float hp,int id,List<(float t,Vector2 dir,float amount,bool head)> hits)>();
            var pending=new List<(float t,Vector2 dir,float amount,bool head)>();var fresh=new List<(float t,Vector2 dir,float amount,bool head)>();double nextSend=0;int read=0;var snap=new NativeSnapshotClock();bool snapOn=false;double newest=double.NegativeInfinity;
            float netLast=-100,netHp=1;int netId=-1;var guestClock=new HordeClock();
            Quaternion pHead=default,pChest=default;var pRot=new Quaternion[22];Vector3 pRoot=default,pV=default;float pYaw=0;bool have=false,haveV=false;
            void Spawn(double t)
            {
                float ang=(float)(rnd.NextDouble()*2*Math.PI);var p=pilot+new Vector3((float)Math.Sin(ang)*10,0,(float)Math.Cos(ang)*10);p.Y=.85f;
                a=new Auth{Id=nextId++,St=st,P=p,Prev=p,Yaw=YawOf(pilot-p),Hp=st.hp*1.24f,MaxHp=st.hp*1.24f,Spawn=t};q=new Rig(s.Kind,a.Id);have=haveV=false;
            }
            Spawn(0);
            while(wall<s.Seconds)
            {
                float dt=1/s.Fps*(1+.3f*(float)(rnd.NextDouble()-.5));wall+=dt;
                while(sim+Dt<=wall+1e-9)
                {
                    sim+=Dt;float T=(float)sim;
                    if(a.Alive&&(sim-a.Spawn>5)){a.Alive=false;deadAt=sim;}
                    if(!a.Alive&&sim-deadAt>.6)Spawn(sim);
                    if(a.Alive)
                    {
                        // HitPveAdd: the pistol.
                        if(shoot&&sim>=nextShot&&sim-a.Spawn>.5&&(a.P-pilot).Length()<10)
                        {
                            nextShot=sim+.42;hits++;bool hd=hits%4==0;float amount=hd?36.8f:23;var dir=Unit(Flat(a.P-pilot));a.Hp-=amount;
                            if(NativeHordeKinds.Staggers(s.Kind,amount,a.MaxHp,false)){a.StaggerAt=T+st.stagger;a.Struck=true;a.RecoverAt=T;}
                            a.Ext+=dir*NativePveParity.AddImpulse(175);if(a.Ext.Length()>6)a.Ext=Unit(a.Ext)*6;var e=(T,new Vector2(dir.X,dir.Z),amount,hd);pending.Add(e);fresh.Add(e);
                            if(a.Hp<=0){a.Alive=false;deadAt=sim;}
                        }
                        // StepPveHordeCore.
                        if(a.Alive)
                        {
                            if(!a.Struck&&T>=a.StrikeAt)a.Struck=true;
                            var d=pilot-a.P;float dist=new Vector2(d.X,d.Z).Length();
                            if(a.Struck&&T>=a.RecoverAt&&T>=a.StaggerAt&&dist<=st.startRange){a.LastAttack=T;a.StrikeAt=T+st.hit;a.Struck=false;a.RecoverAt=T+Math.Max(st.cycle,st.hit+.05f);a.Yaw=YawOf(d);}
                            var move=Vector3.Zero;
                            if(T<a.StaggerAt){}
                            else if(!a.Struck){a.Yaw=YawOf(d);if(dist>.8f)move=Fwd(a.Yaw)*(st.lunge/Math.Max(.05f,st.hit));}
                            else if(T>=a.RecoverAt){float speed=dist<=7||a.Hp<a.MaxHp*.5f?st.run:st.walk;move=dist>1.1f?Steer(a.Route,new Vector3(a.P.X,.9f,a.P.Z),pilot,sim)*speed:Vector3.Zero;}
                            if(move.LengthSquared()>.001f)a.Yaw=YawOf(move);
                            a.Ext*=(float)Math.Exp(-6*Dt);move+=a.Ext;a.V=Flat(move);
                        }
                        else a.V=Vector3.Zero;
                    }
                    a.Prev=a.P;a.P+=a.V*Dt;
                    if(s.Guest&&sim>=nextSend){nextSend=sim+.05;double lat=.045+Math.Abs(Gauss(rnd))*.014+(sim>1&&rnd.NextDouble()<.05?.12:0);double arrive=Math.Max(stream.Count>0?stream[stream.Count-1].arrive:0,sim+lat);stream.Add((arrive,sim,a.P,a.Yaw,a.LastAttack,a.Alive?a.Hp:0,a.Id,new List<(float,Vector2,float,bool)>(pending)));pending.Clear();}
                    if(!s.Guest)pending.Clear();
                }
                double since=wall-sim;
                // ---- the presentation (the measured part) ----
                long before=s.CountBytes?GC.GetAllocatedBytesForCurrentThread():0;
                float now,hdt,lastAttack,health;bool alive;
                if(!s.Guest)
                {
                    double field=visual.Sample(sim,since,false);double was=q.Clock.Time;q.Clock.Step(field,Math.Min(.1f,dt),false);now=(float)q.Clock.Time;hdt=Math.Max(0,Math.Min(.1f,(float)(q.Clock.Time-was)));
                    for(int i=0;i<fresh.Count;i++)Hit(q,a,a.LastAttack,fresh[i]);fresh.Clear();
                    alive=a.Alive;lastAttack=a.LastAttack;health=Math.Max(0,a.Hp/a.MaxHp);
                    if(alive){var body=Vector3.Lerp(a.Prev,a.P,(float)Math.Min(1,since/Dt));float wa=now-lastAttack;q.Flow.Host(now,sim,body,a.V,true,a.Yaw,hdt,wa>=0&&wa<st.hit);}
                }
                else
                {
                    fresh.Clear();
                    while(read<stream.Count&&stream[read].arrive<=wall)
                    {
                        var f=stream[read++];if(!snapOn){snap.Reset(f.t);snapOn=true;}snap.Observe(f.t);newest=Math.Max(newest,f.t);
                        netId=f.id;if(q.Id!=f.id)continue;
                        netLast=f.last;netHp=f.hp;q.Flow.Observe(f.t,f.p,f.yaw);for(int i=0;i<f.hits.Count;i++)Hit(q,a,netLast,f.hits[i]);
                    }
                    if(!snapOn||netId!=q.Id)continue;
                    double field=snap.Advance(dt);double was=guestClock.Time;guestClock.Step(field,Math.Min(.1f,dt),true,newest);now=(float)guestClock.Time;hdt=Math.Max(0,Math.Min(.1f,(float)(guestClock.Time-was)));
                    alive=netHp>0;lastAttack=netLast;health=Math.Max(0,netHp/a.MaxHp);
                    if(alive){float wa=now-lastAttack;q.Flow.Replica(now,hdt,wa>=0&&wa<st.hit);}
                }
                if(!alive||hdt<=0){have=haveV=false;if(s.CountBytes)bytes+=GC.GetAllocatedBytesForCurrentThread()-before;continue;}
                q.Present(now,hdt,health,lastAttack,st.hit,Math.Max(st.cycle,st.hit+.05f),pilot,true);
                if(s.CountBytes)bytes+=GC.GetAllocatedBytesForCurrentThread()-before;
                // ---- the metrics (the drawn world) ----
                var Y=Quaternion.CreateFromAxisAngle(Vector3.UnitY,q.Yaw*(float)Math.PI/180);var hq=Y*Mirror(q.Pose.WorldRotation[CapturedRig.Head]);var cq=Y*Mirror(q.Pose.WorldRotation[CapturedRig.Chest]);var root=q.Root;
                float dist2=Flat(pilot-root).Length();float ageNow=now-lastAttack;bool chasing=!(ageNow>=0&&ageNow<=Math.Max(st.cycle,st.hit+.05f)+.3f)&&dist2>2.2f;
                if(have&&chasing&&now-(float)a.Spawn>.8f)
                {
                    frames++;head.Add(Angle(hq,pHead)/hdt);chest.Add(Angle(cq,pChest)/hdt);yawr.Add(Math.Abs(BossMath.WrapDeg(q.Yaw-pYaw))*(float)Math.PI/180/hdt);
                    float up=0;for(int j=1;j<CapturedRig.LHip;j++)up+=Angle(q.Pose.Rotation[j],pRot[j]);upper.Add(up/(CapturedRig.LHip-1)/hdt);
                    var v=(root-pRoot)/hdt;if(haveV)acc.Add(Flat(v-pV).Length()/hdt);pV=v;haveV=true;
                    if(!s.Guest){var truth=a.P+a.V*(float)since;dev.Add(Flat(truth-root).Length());}
                    float sinceHit=now-q.React.Newest;if(sinceHit>=0&&sinceHit<.3f)for(int j=1;j<=CapturedRig.Head;j++)trunkPop=Math.Max(trunkPop,Angle(q.Pose.Rotation[j],pRot[j])*(1/144f)/hdt);
                }
                else haveV=false;
                pHead=hq;pChest=cq;pRoot=root;pYaw=q.Yaw;have=true;for(int j=0;j<22;j++){pRot[j]=q.Pose.Rotation[j];var r=q.Pose.Rotation[j];hash=(hash^(ulong)BitConverter.SingleToInt32Bits(r.X+r.Y*3+r.Z*5+r.W*7))*1099511628211UL;}
            }
            return new Metrics{HeadP50=P(head,.5),HeadP95=P(head,.95),HeadMax=P(head,1),ChestP95=P(chest,.95),UpperP95=P(upper,.95),YawP95=P(yawr,.95),RootAccP95=P(acc,.95),DevP95=P(dev,.95),DevMax=P(dev,1),TrunkPop=trunkPop,Hits=hits,Frames=frames,Bytes=bytes,Hash=hash};
        }
        // HordeHit: the felt hit (the pistol's flinch weight, the authority's stagger), and a stagger in the wind-up cancels the blow.
        private static void Hit(Rig q,Auth a,float lastAttack,(float t,Vector2 dir,float amount,bool head) e)
        {
            var w=NativeStaggerRules.Classify("pistol","direct",e.amount);bool stagger=NativeHordeKinds.Staggers(q.Kind,e.amount,a.MaxHp,false);
            q.React.Add(e.t,e.dir,w,e.amount,false,e.head,stagger,a.St.stagger);
            if(q.CancelFor!=lastAttack){float age=e.t-lastAttack;if(age>=0&&age<a.St.hit&&NativeHordeKinds.Interrupts(q.Kind,NativeHordeKinds.Attack.Swipe,e.amount,a.MaxHp,false)){q.CancelAt=e.t;q.CancelFor=lastAttack;}}
        }
        private static double Gauss(Random r){double u=1-r.NextDouble(),v=r.NextDouble();return Math.Sqrt(-2*Math.Log(u))*Math.Cos(2*Math.PI*v);}
    }
}
#endif
