#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The water and darkness magic presentation (Claude; owner 2026-10-12: "Water still uses a basic model for Q : Tidal Wave.
    // Also darkness basic attack could use some revamping"): Core/NativeMagicFx (shared intake and marks), Core/NativeTidalWaveFx
    // (the wave), Core/NativeVoidOrbFx (the darkness orb). The Tidal Wave is replayed on the simulation's own numbers (NativeAbilities: released at body + forward .6 +
    // up .25, 24 m/s, gone after .7 s / 16.8 m or at a wall): the body follows the replicated position and speed at the hit
    // sphere's size, the crash fires where and when the shot leaves the replicated list, the meshes are built once and a warm
    // frame allocates nothing, the effects are a pure function of the replicated shots, actors and events (a public-field
    // copy draws the same frame; the Core files read no authority state), and the reduced tiers thin them. Rendering, Unity and
    // Windows are not run here.
    internal static class MagicFxChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Magic presentation: "+what);count++;}

        // A sink that only folds every call into a hash and counts it (allocation free).
        private sealed class HashSink:IMagicFxSink
        {
            public ulong Hash=14695981039346656037UL;public int Calls,Waters,Drops,Glows,Spheres,Views,Streaks;public float LastSphere;
            private void F(float v){unchecked{Hash^=(uint)BitConverter.SingleToInt32Bits(v);Hash*=1099511628211UL;}}
            private void V(Vector3 v){F(v.X);F(v.Y);F(v.Z);}
            public void Reset(){Hash=14695981039346656037UL;Calls=Waters=Drops=Glows=Spheres=Views=Streaks=0;LastSphere=0;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Calls++;Glows++;V(at);V(color);F(coverage);F(size);F(profile);F(u);F(seed);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Calls++;Streaks++;V(head);V(velocity);V(color);F(width);F(u);F(seed);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Calls++;V(at);V(albedo);F(opacity);F(size);F(u);F(seed);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){Calls++;Waters++;V(at);V(lit);F(shade);F(size);V(segment);F(u);F(seed);}
            public void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){Calls++;Drops++;V(head);V(segment);F(coverage);F(width);F(u);F(seed);}
            public void Sphere(Vector3 at,float radius,Vector3 albedo,Vector3 rim,Vector3 emissive){Calls++;Spheres++;LastSphere=radius;V(at);F(radius);V(rim);}
            public void View(Vector3 at,Vector3 color,float coverage,float size,int profile){Calls++;Views++;V(at);V(color);F(coverage);F(size);F(profile);}
        }
        // The presented frame a test scripts: flat floor at y 0, optional wall, actors, wave shots and slows (allocation free).
        private sealed class TestScene:IMagicFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public readonly List<NativeMagicShot> Shots=new List<NativeMagicShot>(4),OrbList=new List<NativeMagicShot>(4);public bool HandView;
            public float WallZ=float.NaN;public int ViewerId;public Vector3 EyeAt=new Vector3(0,30,0);public float SlowUntil=-1;public int SlowId;public bool Shackles;
            public IReadOnlyList<Actor> Actors=>List;public int Waves=>Shots.Count;public bool Wave(int i,out NativeMagicShot s){s=Shots[i];return true;}
            public int Orbs=>OrbList.Count;public bool Orb(int i,out NativeMagicShot s){s=OrbList[i];return true;}
            public bool Hand(int id,out Vector3 hand,out bool view){view=HandView&&id==ViewerId;if(Feet(id,out var f)){hand=f+new Vector3(.25f,1.25f,.45f);return true;}hand=default;return false;}
            public bool Feet(int id,out Vector3 feet){foreach(var a in List)if(a.Id==id){feet=new Vector3(a.Position.x,a.Position.y,a.Position.z);return true;}feet=default;return false;}
            public bool Chest(int id,out Vector3 chest){if(Feet(id,out var f)){chest=f+new Vector3(0,1.3f,0);return true;}chest=default;return false;}
            public int Viewer=>ViewerId;public Vector3 Eye=>EyeAt;public float Floor(Vector3 at)=>0;
            public bool Surface(Vector3 from,Vector3 dir,float reach,out Vector3 point,out Vector3 normal)
            {
                point=normal=default;if(float.IsNaN(WallZ)||dir.Z<=1e-4f)return false;float t=(WallZ-from.Z)/dir.Z;if(t<0||t>reach)return false;
                point=from+dir*t;normal=-Vector3.UnitZ;return true;
            }
            public bool Slowed(int id,out float until,out bool shackled){until=SlowUntil;shackled=Shackles;return id==SlowId&&SlowUntil>0;}
        }
        private static Actor Caster(int id,string spec,Vector3 at,float yaw)=>new Actor{Id=id,SourceSpec=spec,Weapon=WeaponId.Sword,Hp=100,CastRate=1,Yaw=yaw,MagicVisual=spec=="water"?2:spec=="darkness"?4:0,Position=new V3(at.X,at.Y,at.Z),MeleeKit=new NativeMeleeKit()};
        // The simulation's wave from a pilot standing at `feet` facing `dir`, released at `release`: its centre at `time`.
        private static Vector3 WaveAt(Vector3 feet,Vector3 dir,float release,float time)=>feet+new Vector3(0,.865f+.25f,0)+dir*.6f+dir*(NativeTidalWaveFx.Speed*(time-release));
        private static NativeMagicShot Shot(int id,int owner,Vector3 at,Vector3 dir)=>new NativeMagicShot{Key=NativeShotScene.Key(NativeShotKind.Ability,id),Id=id,Owner=owner,Position=at,Velocity=dir*NativeTidalWaveFx.Speed,Radius=1};

        public static void Run(string root)
        {
            Follows();Crash();Budget();Replicated(root);Tiers();Hits();Windup();
            OrbParts();OrbClock();OrbImpacts();Lifesteal(root);OrbTiers();OrbCast();OrbBudget();
            Console.WriteLine("PASS Magic presentation ("+count+" checks: the Tidal Wave body follows the replicated shot at its 24 m/s at a 2.5-3.5 m x 1.6-2.2 m size round the 1 m hit sphere, curls as it travels, crashes where and when the shot leaves the replicated list at range end or a wall, built-once meshes and allocation-free warm frames, a pure function of replicated shots/actors/events (public-field copy, no authority reads), Reduced and low tiers thin it, splashes keyed to replicated wave hits, drips only while the replicated water slow lasts, windup swell from the replicated cast; the darkness orb's parts deterministic from the replicated bullet on the source's pulse and wobble clock, its implosion/burst at the replicated direct hit, a surface it reached (scorch and cracks) or in the air, the lifesteal stream only from a replicated direct Hit by a darkness caster and the heal glint only with its paired Heal, the gather and release on the replicated cast (view layer in first person), tiers thin it, no allocation; Unity rendering not run)");
        }
        // The body tracks the replicated position at the shot's speed, at the visual size round the hit sphere.
        private static void Follows()
        {
            var fx=new NativeMagicFx();var scene=new TestScene();var sink=new HashSink();var caster=Caster(1,"water",Vector3.Zero,45);scene.List.Add(caster);
            var dir=Vector3.Normalize(new Vector3(1,0,1));float at=1,release=1.15f;caster.AbilityId="tidal-wave";caster.AbilityAt=at;caster.AbilityRelease=release;
            var centres=new List<(float t,Vector3 c,Vector3 shot)>();var origins=new List<Vector3>();
            for(float t=release;t<release+NativeTidalWaveFx.Life-1e-4f;t+=1/60f)
            {
                scene.Shots.Clear();var p=WaveAt(Vector3.Zero,dir,release,t);scene.Shots.Add(Shot(9,1,p,dir));fx.Draw(sink,scene,t,t,1,false);
                var w=fx.Tide.Waves.First(x=>x.Used&&x.Id==9);Check(w.Live,"live while replicated");
                var pose=w.Pose;var b=w.Surface;var c=(b.Min+b.Max)*.5f;centres.Add((t,c,p));origins.Add(pose.Origin);
                Check(Vector3.Distance(pose.Origin,new Vector3(p.X,0,p.Z)+dir*NativeTidalWaveFx.OriginLead)<1e-3f,"origin under the replicated centre");
                // The body sits on the floor under the shot, around its hit sphere, and points the way it flies.
                Check(Math.Abs(b.Min.Y)<.05f,"on the floor at "+t.ToString("0.00"));
                Check(Vector3.Dot(Vector3.Normalize(pose.Forward),dir)>.999f,"faces the velocity");
                float age=t-release;
                if(age>.36f)
                {
                    float height=b.Max.Y-b.Min.Y,across=Vector3.Dot(b.Max-b.Min,Vector3.Abs(NativeMagicMath.Right(dir)));
                    Check(height>=1.6f&&height<=2.2f,"1.6-2.2 m tall mid-flight ("+height.ToString("0.00")+")");
                    Check(pose.Width>=2.5f&&pose.Width<=3.5f,"2.5-3.5 m wide mid-flight ("+pose.Width.ToString("0.00")+")");
                    Check(pose.Curl>.8f,"curled by mid-flight");
                }
                // The hit sphere (1 m round the shot centre) lies inside the body's reach across.
                Check(pose.Width*.5f>=NativeTidalWaveFx.HitRadius,"reaches the hit radius each side");
                var flat=new Vector3(c.X-p.X,0,c.Z-p.Z);Check(flat.Length()<2.2f,"body round the replicated centre ("+flat.Length().ToString("0.00")+" m)");
            }
            // It moves with the shot: the origin's speed is the shot's 24 m/s.
            var first=fx.Tide.Waves.First(x=>x.Used&&x.Id==9);
            float span=centres[centres.Count-1].t-centres[0].t,speed=Vector3.Distance(origins[origins.Count-1],origins[0])/span;Check(Math.Abs(speed-NativeTidalWaveFx.Speed)<.05f,"moves at the shot speed ("+speed.ToString("0.00")+" m/s)");
            Check(Math.Abs(first.Travel-NativeTidalWaveFx.Speed*span)<.45f,"its path is the shot's path ("+first.Travel.ToString("0.00")+" m)");
            Check(first.Born==release,"aged from the caster's replicated release");
            // Grows and curls: smaller and straighter at release than at mid-flight.
            NativeTidalWaveFx.Shape(0,out float h0,out float w0,out float c0,out _);NativeTidalWaveFx.Shape(.45f,out float h1,out float w1,out float c1,out _);
            Check(h0<h1&&w0<w1&&c0<c1,"grows and curls while travelling");
            // The source box is replaced: Owns the tidal wave only.
            Check(NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Ability,Ability="tidal-wave"})&&!NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Ability,Ability="rocket"})&&!NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Bullet,Spec="water",Class="magic"}),"replaces the tidal wave's model only");
        }
        // The crash: when the shot leaves the replicated list (range end), at its last position; it slumps, bursts and is gone.
        private static void Crash()
        {
            var fx=new NativeMagicFx();var scene=new TestScene();var sink=new HashSink();var caster=Caster(1,"water",Vector3.Zero,0);scene.List.Add(caster);
            float release=1.15f,end=release+NativeTidalWaveFx.Life;caster.AbilityId="tidal-wave";caster.AbilityAt=1;caster.AbilityRelease=release;float t=release;
            for(;t<end-1e-4f;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(5,1,WaveAt(Vector3.Zero,Vector3.UnitZ,release,t),Vector3.UnitZ));fx.Draw(sink,scene,t,t,1,false);
                Check(fx.Tide.Waves.All(w=>!w.Used||w.Live),"no crash while the shot is replicated");}
            var last=WaveAt(Vector3.Zero,Vector3.UnitZ,release,t-1/60f);
            scene.Shots.Clear();sink.Reset();fx.Draw(sink,scene,t,t,1,false);
            var gone=fx.Tide.Waves.First(w=>w.Used&&w.Id==5);
            Check(!gone.Live&&Math.Abs(gone.GoneAt-t)<1e-5f,"crash fires the frame the shot is gone");
            float range=.6f+NativeTidalWaveFx.Range;Check(Math.Abs(gone.CrashAt.Z-range)<.6f&&Math.Abs(gone.CrashAt.Y)<1e-4f,"crash at end of range on the floor ("+gone.CrashAt.Z.ToString("0.00")+" m vs "+range.ToString("0.00")+")");
            Check(!gone.Wall,"open floor: a range-end crash, not a wall");Check(sink.Waters>10&&sink.Drops>5,"splash burst and droplets at the crash");
            float before=gone.Surface.Max.Y;fx.Draw(sink,scene,t+.25f,t+.25f,1,false);float later=gone.Surface.Max.Y;Check(later<before-.25f,"slumps as it crashes ("+before.ToString("0.00")+" -> "+later.ToString("0.00")+" m)");
            fx.Draw(sink,scene,t+NativeTidalWaveFx.CrashSeconds+.01f,t+NativeTidalWaveFx.CrashSeconds+.01f,1,false);Check(fx.Tide.Bodies.Count==0,"body gone after the crash");
            Check(fx.Decals.Count>0,"wet streak and puddles remain");
            fx.Draw(sink,scene,t+NativeTidalWaveFx.StreakSeconds+.5f,t+NativeTidalWaveFx.StreakSeconds+.5f,1,false);Check(fx.Decals.Count==0&&fx.Tide.Waves.All(w=>!w.Used),"the floor dries and the record is freed");
            // Into a wall 6 m ahead: the shot leaves the list at the wall; the crash is a wall crash in front of it.
            fx.Clear();scene.WallZ=6;float hitWall=release+(6-1-.6f)/NativeTidalWaveFx.Speed;
            for(t=release;t<hitWall;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(6,1,WaveAt(Vector3.Zero,Vector3.UnitZ,release,t),Vector3.UnitZ));fx.Draw(sink,scene,t,t,1,false);}
            scene.Shots.Clear();fx.Draw(sink,scene,t,t,1,false);var wall=fx.Tide.Waves.First(w=>w.Used&&w.Id==6);
            Check(wall.Wall&&wall.CrashAt.Z<6&&wall.CrashAt.Z>4,"a wall crash before the wall ("+wall.CrashAt.Z.ToString("0.00")+")");
        }
        // Meshes are built once; a warm frame with several waves, splashes and marks allocates nothing.
        private static void Budget()
        {
            int built=NativeWaveSurface.Built;var fx=new NativeMagicFx();Check(NativeWaveSurface.Built-built==NativeTidalWaveFx.MaxWaves+4,"a fixed pool of surfaces built with the effects");built=NativeWaveSurface.Built;
            var scene=new TestScene();var sink=new HashSink();for(int i=1;i<=3;i++){var a=Caster(i,"water",new Vector3(i*4,0,0),0);a.AbilityId="tidal-wave";a.AbilityAt=1;a.AbilityRelease=1.15f;scene.List.Add(a);}
            var events=new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Hit,Time=1.3f,Source=1,Target=7,Position=new V3(4,1.6f,3),Amount=22,Detail="ability"}};
            void Frame(float t){scene.Shots.Clear();if(t<1.85f)for(int i=1;i<=3;i++)scene.Shots.Add(Shot(20+i,i,WaveAt(new Vector3(i*4,0,0),Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,t,1,false);}
            float time=1.15f;for(int i=0;i<30;i++,time+=1/60f)Frame(time);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<90;i++,time+=1/60f)Frame(time);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"warm frames allocate nothing ("+used+" bytes over 90 frames through the crash)");
            Check(NativeWaveSurface.Built==built,"no surface built per frame");
        }
        // A pure function of the replicated shots, actors and events: a public-field copy of the actors and events draws the same
        // frames; the Core sources read no authority, engine or random state.
        private static void Replicated(string root)
        {
            ulong Run(List<Actor> actors,List<ArenaEvent> events)
            {
                var fx=new NativeMagicFx();var scene=new TestScene();scene.List.AddRange(actors);scene.SlowId=2;scene.SlowUntil=2.9f;var sink=new HashSink();ulong h=0;
                for(float t=1;t<3.2f;t+=1/60f){scene.Shots.Clear();if(t>=1.15f&&t<1.85f)scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Observe(events,t,scene.List);sink.Reset();fx.Draw(sink,scene,t,t,1,false);
                    unchecked{h=h*31+sink.Hash;foreach(var b in fx.Tide.Bodies)for(int i=0;i<b.VertexCount;i+=7)h=h*31+(uint)BitConverter.SingleToInt32Bits(b.Vertices[i].Position.X+b.Vertices[i].Position.Y*3+b.Vertices[i].Position.Z*7);h=h*31+(ulong)fx.Decals.Count;}}
                return h;
            }
            var caster=Caster(1,"water",Vector3.Zero,0);caster.AbilityId="tidal-wave";caster.AbilityAt=1;caster.AbilityRelease=1.15f;var victim=Caster(2,"sword",new Vector3(0,0,8),180);victim.MagicVisual=0;
            var actors=new List<Actor>{caster,victim};var events=new List<ArenaEvent>{new ArenaEvent{Id=4,Kind=EventKind.Hit,Time=1.4f,Source=1,Target=2,Position=new V3(0,1.665f,8),Amount=22,Detail="ability"}};
            var actors2=actors.Select(x=>(Actor)Fields(x)).ToList();var events2=events.Select(x=>(ArenaEvent)Fields(x)).ToList();
            ulong a=Run(actors,events),b=Run(actors2,events2),c=Run(actors,events);Check(a==b&&a==c,"the same frames from the replicated copy (public fields only, what JsonUtility carries) and on a second run");
            foreach(var file in new[]{"Core/NativeMagicFx.cs","Core/NativeTidalWaveFx.cs"})
            {
                string code=Regex.Replace(File.ReadAllText(Path.Combine(root,"Assets/ChipArena",file)),@"//[^\n]*|/\*.*?\*/","",RegexOptions.Singleline);
                foreach(var banned in new[]{"NativePilot","NativeArena","UnityEngine","System.Random","DateTime","Stopwatch","Environment.Tick","Guid"})Check(!code.Contains(banned),file+" reads no "+banned);
            }
        }

        // ---- the darkness primary (NativeVoidOrbFx) ----
        // The simulation's orb from a pilot at the origin facing +z (NativeProjectiles: 26 m/s, gravity 3, from the muzzle).
        private static Vector3 OrbAt(float born,float time,Vector3 v){float t=time-born;return new Vector3(.18f,1.385f,.68f)+v*t+new Vector3(0,-1.5f*t*t,0);}
        private static NativeMagicShot OrbShot(int id,int owner,float born,float time,Vector3 v)=>new NativeMagicShot{Key=NativeShotScene.Key(NativeShotKind.Bullet,id),Id=id,Owner=owner,Position=OrbAt(born,time,v),Velocity=v+new Vector3(0,-3*(time-born),0),Radius=NativeVoidOrbFx.ShotRadius,Born=born};
        private static readonly Vector3 OrbV=new Vector3(0,1.2f,NativeVoidOrbFx.Speed);
        // Every part of the orb in flight is a pure function of the replicated bullet and the clocks: a second run and a
        // public-field copy draw the same frames; replaces the darkness orb (not hadoukens, sword beams or water globes).
        private static void OrbParts()
        {
            ulong Run(List<Actor> actors,List<ArenaEvent> events)
            {
                var fx=new NativeMagicFx();var scene=new TestScene();scene.List.AddRange(actors);var sink=new HashSink();ulong h=0;
                for(float t=1.2f;t<2.4f;t+=1/60f){scene.OrbList.Clear();if(t<1.75f)scene.OrbList.Add(OrbShot(7,1,1.22f,t,OrbV));fx.Observe(events,t,scene.List);sink.Reset();fx.Draw(sink,scene,t,t*1.01f,1,false);unchecked{h=h*31+sink.Hash;h=h*31+(ulong)fx.Decals.Count;}}
                return h;
            }
            var caster=Caster(1,"darkness",Vector3.Zero,0);caster.AttackAt=1;var victim=Caster(2,"sword",new Vector3(0,0,13),180);victim.MagicVisual=0;
            var actors=new List<Actor>{caster,victim};var events=new List<ArenaEvent>{new ArenaEvent{Id=5,Kind=EventKind.Heal,Time=1.7f,Source=1,Target=1,Amount=6.8f},new ArenaEvent{Id=6,Kind=EventKind.Hit,Time=1.7f,Source=1,Target=2,Position=new V3(0,1.4f,12.6f),Amount=34,Detail="direct"}};
            ulong a=Run(actors,events),b=Run(actors.Select(x=>(Actor)Fields(x)).ToList(),events.Select(x=>(ArenaEvent)Fields(x)).ToList()),c=Run(actors,events);
            Check(a==b&&a==c,"the orb, its cast, impact and drain draw the same frames on a second run and from the replicated copy");
            Check(NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Bullet,Spec="darkness",Class="magic"}),"replaces the darkness orb");
            Check(!NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Bullet,Spec="darkness",Class="magic",Arcade=1})&&!NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Bullet,Spec="darkness",SwordBeam=true})&&!NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Bullet,Spec="water",Class="magic"})&&!NativeMagicFx.Owns(new NativeShotView{Kind=NativeShotKind.Ability,Ability="shadow-shackles"}),"not a hadouken, sword beam, water globe or Shadow Shackles");
            // In flight: a black core with its rim, the lens rings, swirl, wisps, tendrils, motes and the smoky tail behind it.
            var fx2=new NativeMagicFx();var scene2=new TestScene();scene2.List.Add(caster);var sink2=new HashSink();
            for(float t=1.22f;t<1.5f;t+=1/60f){scene2.OrbList.Clear();scene2.OrbList.Add(OrbShot(8,1,1.22f,t,OrbV));sink2.Reset();fx2.Draw(sink2,scene2,t,t,1,false);}
            Check(sink2.Spheres>=1&&sink2.Glows>=14&&sink2.Streaks>=6&&sink2.Drops>=12,"core, lens rings, swirl, motes and ink tendrils in flight ("+sink2.Spheres+" cores, "+sink2.Glows+" glows, "+sink2.Streaks+" streaks, "+sink2.Drops+" drops)");
            var orb=fx2.Void.Orbs.First(o=>o.Used&&o.Id==8);Check(orb.Live&&orb.Samples>5&&Math.Abs(orb.Born-1.22f)<1e-5f,"tracked from its replicated spawn along its path");
            Check(sink2.LastSphere>NativeVoidOrbFx.ShotRadius*.75f&&sink2.LastSphere<NativeVoidOrbFx.ShotRadius*1.15f,"the core at the shot radius ("+sink2.LastSphere.ToString("0.000")+" m)");
        }
        // The core pulse and the tendril wobble keep NativeShotScene's darkness clock (state.time), not the presentation clock.
        private static void OrbClock()
        {
            var scene=new NativeShotScene();float[] times={.3f,1.17f,2.05f,3.9f};
            foreach(float sim in times)
            {
                scene.Begin(5,sim,1/60f,1);var v=new NativeShotView{Kind=NativeShotKind.Bullet,Id=1,Spec="darkness",Class="magic",X=0,Y=1,Z=0,VZ=26,Radius=.2,FuseLeft=1e9,WeaponColor=0xad78d2};scene.Shot(v);scene.End(1/60f);
                var m=scene.Draws.First(d=>d.Shape==NativeShotShape.DarkIco).M;float source=(float)Math.Sqrt(m.M00*m.M00+m.M10*m.M10+m.M20*m.M20)/.2f;
                Check(Math.Abs(source-NativeVoidOrbFx.Pulse(sim))<1e-4f,"the core pulse is the source's darkness pulse at state.time "+sim);
                float Core(float time){var fx=new NativeMagicFx();var ts=new TestScene();var sink=new HashSink();ts.OrbList.Add(new NativeMagicShot{Key=1,Id=1,Owner=0,Position=new Vector3(0,1,0),Velocity=new Vector3(0,0,26),Radius=.2f,Born=time-.5f});fx.Draw(sink,ts,time,sim,1,false);return sink.LastSphere;}
                Check(Math.Abs(Core(5)/(.2f*.95f)-NativeVoidOrbFx.Pulse(sim))<1e-4f,"the drawn core follows that clock ("+sim+")");
                Check(Math.Abs(Core(6.3f)-Core(5))<1e-5f,"not the presentation clock");
            }
            Check(Math.Abs(NativeVoidOrbFx.Wobble(.5f,0,0)-(float)Math.Sin(12)*.2f)<1e-5f,"the tendril wobble is the source's sin(24 t) .2");
        }
        // Impacts: at the replicated direct hit (no surface mark), at a surface it reached when it left the list (scorch and
        // cracks), or a fizzle in the air at range end.
        private static void OrbImpacts()
        {
            (NativeMagicFx fx,HashSink sink) Fly(float wallZ,List<ArenaEvent> events,float gone)
            {
                var fx=new NativeMagicFx();var scene=new TestScene{WallZ=wallZ};var caster=Caster(1,"darkness",Vector3.Zero,0);scene.List.Add(caster);var sink=new HashSink();
                float t=1.22f;for(;t<gone;t+=1/60f){scene.OrbList.Clear();scene.OrbList.Add(OrbShot(9,1,1.22f,t,OrbV));fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,t,1,false);}
                scene.OrbList.Clear();fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,t,1,false);for(int i=0;i<12;i++){t+=1/60f;sink.Reset();fx.Draw(sink,scene,t,t,1,false);}return (fx,sink);
            }
            float reach=1.22f+(10-.68f-.2f)/NativeVoidOrbFx.Speed;
            var (wall,ws)=Fly(10,new List<ArenaEvent>(),reach);var m=wall.Void.Impacts.First(x=>x.Id!=0);
            Check(m.Surface&&!m.Direct&&Math.Abs(m.At.Z-10)<.3f&&m.Normal.Z<-.9f,"a surface impact where it reached the wall ("+m.At.Z.ToString("0.00")+")");
            Check(wall.Decals.Quads>=8,"a void scorch with glowing cracks on the wall ("+wall.Decals.Quads+" quads)");Check(ws.Streaks+ws.Waters+ws.Drops+ws.Glows>10,"an implosion then a dark burst");
            var hitEvents=new List<ArenaEvent>{new ArenaEvent{Id=3,Kind=EventKind.Hit,Time=reach-.01f,Source=1,Target=2,Position=new V3(.18f,1.6f,9.6f),Amount=34,Detail="direct"}};
            var (direct,_)=Fly(10,hitEvents,reach);Check(direct.Void.Impacts.Count(x=>x.Id!=0)==1&&direct.Void.Impacts.First(x=>x.Id!=0).Direct&&direct.Decals.Quads==0,"a direct hit bursts at the hit, no second impact or mark");
            var (air,_)=Fly(float.NaN,new List<ArenaEvent>(),1.22f+36f/NativeVoidOrbFx.Speed);var fizz=air.Void.Impacts.First(x=>x.Id!=0);Check(fizz.Air&&air.Decals.Quads==0,"range end in the air: a fizzle, no mark");
            // The scorch fades; the record ends.
            var scene=new TestScene();var sink=new HashSink();wall.Draw(sink,scene,m.Time+NativeVoidOrbFx.ScorchSeconds+.1f,0,1,false);Check(wall.Decals.Quads==0,"the scorch fades out");
            Check(NativeVoidOrbFx.CrackGlow(.1f)>NativeVoidOrbFx.CrackGlow(1)&&NativeVoidOrbFx.CrackGlow(1)>NativeVoidOrbFx.CrackGlow(2.2f),"the cracks glow bright then dim");
        }
        // The lifesteal stream comes only from a replicated direct Hit by a darkness caster; the heal glint only with its paired
        // replicated Heal (the 20% drain), in either order within the tick.
        private static void Lifesteal(string root)
        {
            (int streaks,bool healed,int drains) Run(string spec,string detail,bool hit,bool heal,bool healFirst=true)
            {
                var fx=new NativeMagicFx();var scene=new TestScene();scene.List.Add(Caster(1,spec,Vector3.Zero,0));var v=Caster(2,"sword",new Vector3(0,0,9),180);v.MagicVisual=0;scene.List.Add(v);var sink=new HashSink();
                var events=new List<ArenaEvent>();var h=new ArenaEvent{Id=10,Kind=EventKind.Heal,Time=1.5f,Source=1,Target=1,Amount=6.8f};var x=new ArenaEvent{Id=11,Kind=EventKind.Hit,Time=1.5f,Source=1,Target=2,Position=new V3(0,1.4f,8.7f),Amount=34,Detail=detail};
                if(heal&&healFirst)events.Add(h);if(hit)events.Add(x);if(heal&&!healFirst){h.Id=12;events.Add(h);}
                int streaks=0;for(float t=1.5f;t<2.2f;t+=1/60f){fx.Observe(events,t,scene.List);sink.Reset();fx.Draw(sink,scene,t,t,1,false);streaks+=sink.Streaks;}
                var d=fx.Void.Drains.FirstOrDefault(r=>r.Id!=0);return (streaks,d.Id!=0&&d.Healed,fx.Void.Drains.Count(r=>r.Id!=0));
            }
            var full=Run("darkness","direct",true,true);Check(full.drains==1&&full.healed&&full.streaks>40,"a direct hit with its Heal: the stream and the heal glint ("+full.streaks+" streak draws)");
            var late=Run("darkness","direct",true,true,false);Check(late.drains==1&&late.healed,"the Heal after the Hit in the same tick pairs too (a minion hit)");
            var noHeal=Run("darkness","direct",true,false);Check(noHeal.drains==1&&!noHeal.healed,"no Heal (the caster at full health): the stream, no heal glint");
            Check(Run("darkness","direct",false,false).drains==0&&Run("darkness","direct",false,true).drains==0,"no stream without a replicated hit, or from a Heal alone");
            Check(Run("darkness","secondary",true,true).drains==0&&Run("darkness","ability",true,true).drains==0,"only a direct primary hit drains (not a chip blast or Shadow Shackles)");
            Check(Run("fire","direct",true,true).drains==0,"only a darkness caster's hit");
            string code=Regex.Replace(File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeVoidOrbFx.cs")),@"//[^\n]*|/\*.*?\*/","",RegexOptions.Singleline);
            foreach(var banned in new[]{"NativePilot","NativeArena","UnityEngine","System.Random","DateTime","Stopwatch","Hp","Stats"})Check(!code.Contains(banned),"NativeVoidOrbFx reads no "+banned);
        }
        // Effects quality (Reduced .3) and the low graphics tier thin the orb, its tail, cast, impact and stream.
        private static void OrbTiers()
        {
            int Particles(float quality,bool low)
            {
                var fx=new NativeMagicFx();var scene=new TestScene{WallZ=10};var c=Caster(1,"darkness",Vector3.Zero,0);c.AttackAt=1;scene.List.Add(c);var sink=new HashSink();int total=0;
                var events=new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Hit,Time=1.4f,Source=1,Target=2,Position=new V3(0,1.4f,5),Amount=34,Detail="direct"}};scene.List.Add(Caster(2,"sword",new Vector3(0,0,5),180));
                for(float t=1;t<2.3f;t+=1/60f){scene.OrbList.Clear();if(t>=1.22f&&t<1.6f)scene.OrbList.Add(OrbShot(4,1,1.22f,t,OrbV));fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,t,quality,low);total+=fx.Void.Particles;}
                return total;
            }
            int full=Particles(1,false),reduced=Particles(.3f,false),low=Particles(1,true);
            Check(reduced<full*.7f,"Reduced effects thin the darkness effects ("+reduced+" vs "+full+")");Check(low<full*.8f,"the low tier thins them ("+low+" vs "+full+")");
        }
        // The cast: the gather at the right-hand socket over the replicated windup, then the release pulse; on the view layer in
        // the caster's own first person.
        private static void OrbCast()
        {
            var fx=new NativeMagicFx();var scene=new TestScene();var c=Caster(1,"darkness",Vector3.Zero,0);c.AttackAt=1;c.CastRate=1;scene.List.Add(c);var sink=new HashSink();
            fx.Draw(sink,scene,.98f,.98f,1,false);Check(sink.Calls==0,"nothing before the cast");
            sink.Reset();fx.Draw(sink,scene,1.1f,1.1f,1,false);Check(sink.Calls>8&&sink.Spheres==1&&sink.Views==0,"the dark gather at the hand during the windup (a forming core)");
            sink.Reset();fx.Draw(sink,scene,1.3f,1.3f,1,false);Check(sink.Calls>4&&sink.Spheres==0,"the release pulse after the windup");
            sink.Reset();fx.Draw(sink,scene,1.6f,1.6f,1,false);Check(sink.Calls==0,"done after the release");
            scene.ViewerId=1;scene.HandView=true;sink.Reset();fx.Draw(sink,scene,1.1f,1.1f,1,false);Check(sink.Views>4&&sink.Spheres==0,"the caster's own first-person gather on the view layer");
            // A faster cast rate shortens the gather with the replicated windup.
            c.CastRate=2;scene.HandView=false;scene.ViewerId=0;sink.Reset();fx.Draw(sink,scene,1.15f,1.15f,1,false);Check(sink.Spheres==0&&sink.Calls>0,"the windup follows the cast rate");
        }
        // A warm frame with orbs in flight, their tails, impacts, marks and a drain allocates nothing.
        private static void OrbBudget()
        {
            var fx=new NativeMagicFx();var scene=new TestScene{WallZ=12};for(int i=1;i<=3;i++){var c=Caster(i,"darkness",new Vector3(i,0,0),0);c.AttackAt=1;scene.List.Add(c);}scene.List.Add(Caster(9,"sword",new Vector3(0,0,8),180));var sink=new HashSink();
            var events=new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Heal,Time=1.45f,Source=1,Target=1,Amount=6},new ArenaEvent{Id=2,Kind=EventKind.Hit,Time=1.45f,Source=1,Target=9,Position=new V3(0,1.4f,7.6f),Amount=34,Detail="direct"}};
            void Frame(float t){scene.OrbList.Clear();for(int i=1;i<=3;i++){float born=1.22f+i*.05f;if(t>=born&&t<born+.42f)scene.OrbList.Add(OrbShot(30+i,i,born,t,OrbV));}fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,t,1,false);}
            float time=1.0f;for(int i=0;i<30;i++,time+=1/60f)Frame(time);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<90;i++,time+=1/60f)Frame(time);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"warm darkness frames allocate nothing ("+used+" bytes over 90 frames through the impacts and the drain)");
        }
        // A public-field copy (what JsonUtility carries on the wire and the killcam records): nothing private survives it.
        private static object Fields(object o)
        {
            if(o==null)return null;var t=o.GetType();if(t.IsPrimitive||t.IsEnum||o is string)return o;
            if(o is Array arr){var c=Array.CreateInstance(t.GetElementType(),arr.Length);for(int i=0;i<arr.Length;i++)c.SetValue(Fields(arr.GetValue(i)),i);return c;}
            var copy=t.IsValueType?Activator.CreateInstance(t):System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(t);
            foreach(var f in t.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))if(!f.IsInitOnly)f.SetValue(copy,Fields(f.GetValue(o)));
            return copy;
        }
        // Effects quality (Reduced .3) and the low graphics tier thin the particles.
        private static void Tiers()
        {
            int Particles(float quality,bool low)
            {
                var fx=new NativeMagicFx();var scene=new TestScene();var caster=Caster(1,"water",Vector3.Zero,0);caster.AbilityId="tidal-wave";caster.AbilityAt=1;caster.AbilityRelease=1.15f;scene.List.Add(caster);var sink=new HashSink();int total=0;
                for(float t=1.15f;t<2.6f;t+=1/60f){scene.Shots.Clear();if(t<1.85f)scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Draw(sink,scene,t,t,quality,low);total+=fx.Tide.Particles;}
                return total;
            }
            int full=Particles(1,false),reduced=Particles(.3f,false),low=Particles(1,true),both=Particles(.3f,true);
            Check(reduced<full*.55f,"Reduced effects thin the particles ("+reduced+" vs "+full+")");Check(low<full*.75f,"the low tier thins the particles ("+low+" vs "+full+")");Check(both<=reduced&&both>0,"both tiers together thinnest, not empty");
        }
        // Splashes are keyed to a replicated wave hit; drips to the replicated slow after a water hit.
        private static void Hits()
        {
            int SplashCalls(string spec,string detail)
            {
                var fx=new NativeMagicFx();var scene=new TestScene();var caster=Caster(1,spec,Vector3.Zero,0);scene.List.Add(caster);var victim=Caster(2,"sword",new Vector3(0,0,8),180);scene.List.Add(victim);var sink=new HashSink();
                var events=new List<ArenaEvent>{new ArenaEvent{Id=8,Kind=EventKind.Hit,Time=1.4f,Source=1,Target=2,Position=new V3(0,1.665f,8),Amount=22,Detail=detail}};
                for(float t=1.15f;t<1.4f;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,t,1,false);}
                // With and without the hit: the difference is the splash.
                scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,1.5f),Vector3.UnitZ));fx.Observe(events,1.5f,scene.List);sink.Reset();fx.Draw(sink,scene,1.5f,1.5f,1,false);int with=sink.Calls;
                var bare=new NativeMagicFx();var sink2=new HashSink();for(float t=1.15f;t<1.4f;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));bare.Draw(sink2,scene,t,t,1,false);}
                scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,1.5f),Vector3.UnitZ));sink2.Reset();bare.Draw(sink2,scene,1.5f,1.5f,1,false);
                return with-sink2.Calls;
            }
            Check(SplashCalls("water","ability")>10,"a replicated wave hit splashes the victim");
            Check(SplashCalls("fire","ability")==0,"another school's ability hit draws no wave splash");
            Check(SplashCalls("water","direct")==0,"a water globe's direct hit is not a wave splash");
            // Drips: only while the replicated slow (not Shadow Shackles) lasts after a water hit.
            int Drips(bool hit,float slowUntil,bool shackles,float at)
            {
                var fx=new NativeMagicFx();var scene=new TestScene{SlowId=2,SlowUntil=slowUntil,Shackles=shackles};scene.List.Add(Caster(1,"water",Vector3.Zero,0));scene.List.Add(Caster(2,"sword",new Vector3(0,0,8),180));var sink=new HashSink();
                var events=hit?new List<ArenaEvent>{new ArenaEvent{Id=3,Kind=EventKind.Hit,Time=1,Source=1,Target=2,Position=new V3(0,1.2f,8),Amount=20,Detail="direct"}}:new List<ArenaEvent>();
                fx.Observe(events,at,scene.List);fx.Draw(sink,scene,at,at,1,false);return sink.Drops+sink.Waters;
            }
            Check(Drips(true,2.5f,false,1.4f)>0,"a soaked victim drips while slowed");Check(Drips(false,2.5f,false,1.4f)==0,"no drips without a water hit");
            Check(Drips(true,2.5f,true,1.4f)==0,"Shadow Shackles' slow is not soaked");Check(Drips(true,1.3f,false,1.4f)==0,"no drips after the slow ends");
        }
        // The windup swell rises from the replicated cast before the shot exists; the caster's own first-person view keeps it low.
        private static void Windup()
        {
            var fx=new NativeMagicFx();var scene=new TestScene();var caster=Caster(1,"water",Vector3.Zero,0);caster.AbilityId="tidal-wave";caster.AbilityAt=1;caster.AbilityRelease=1.15f;scene.List.Add(caster);var sink=new HashSink();
            fx.Draw(sink,scene,.99f,.99f,1,false);Check(fx.Tide.Bodies.Count==0,"nothing before the cast");
            fx.Draw(sink,scene,1.12f,1.12f,1,false);Check(fx.Tide.Bodies.Count==1&&fx.Tide.Poses[0].Height>.8f,"a swell rises during the windup");float third=fx.Tide.Poses[0].Height;
            scene.ViewerId=1;fx.Draw(sink,scene,1.12f,1.12f,1,false);Check(fx.Tide.Poses[0].Height<third*.6f&&fx.Tide.Poses[0].NearStart>=1.5f,"low and faded near the eye in the caster's own first person");
            scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,1.16f),Vector3.UnitZ));fx.Draw(sink,scene,1.16f,1.16f,1,false);
            Check(fx.Tide.Bodies.Count==1&&fx.Tide.Waves.Any(w=>w.Used&&w.Live&&w.Own),"the shot takes over from the swell (one body)");
            Check(fx.Tide.Poses[0].NearStart>=NativeTidalWaveFx.OwnNearStart-1e-4f,"the caster's own wave fades near the camera");
        }
    }
}
#endif
