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
    // (the wave). The Tidal Wave is replayed on the simulation's own numbers (NativeAbilities: released at body + forward .6 +
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
            public ulong Hash=14695981039346656037UL;public int Calls,Waters,Drops,Glows;
            private void F(float v){unchecked{Hash^=(uint)BitConverter.SingleToInt32Bits(v);Hash*=1099511628211UL;}}
            private void V(Vector3 v){F(v.X);F(v.Y);F(v.Z);}
            public void Reset(){Hash=14695981039346656037UL;Calls=Waters=Drops=Glows=0;}
            public void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed){Calls++;Glows++;V(at);V(color);F(coverage);F(size);F(profile);F(u);F(seed);}
            public void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed){Calls++;V(head);V(velocity);V(color);F(width);F(u);F(seed);}
            public void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor){Calls++;V(at);V(albedo);F(opacity);F(size);F(u);F(seed);}
            public void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor){Calls++;Waters++;V(at);V(lit);F(shade);F(size);V(segment);F(u);F(seed);}
            public void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed){Calls++;Drops++;V(head);V(segment);F(coverage);F(width);F(u);F(seed);}
        }
        // The presented frame a test scripts: flat floor at y 0, optional wall, actors, wave shots and slows (allocation free).
        private sealed class TestScene:IMagicFxScene
        {
            public readonly List<Actor> List=new List<Actor>();public readonly List<NativeMagicShot> Shots=new List<NativeMagicShot>(4);
            public float WallZ=float.NaN;public int ViewerId;public Vector3 EyeAt=new Vector3(0,30,0);public float SlowUntil=-1;public int SlowId;public bool Shackles;
            public IReadOnlyList<Actor> Actors=>List;public int Waves=>Shots.Count;public bool Wave(int i,out NativeMagicShot s){s=Shots[i];return true;}
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
            Console.WriteLine("PASS Magic presentation ("+count+" checks: the Tidal Wave body follows the replicated shot at its 24 m/s at a 2.5-3.5 m x 1.6-2.2 m size round the 1 m hit sphere, curls as it travels, crashes where and when the shot leaves the replicated list at range end or a wall, built-once meshes and allocation-free warm frames, a pure function of replicated shots/actors/events (public-field copy, no authority reads), Reduced and low tiers thin it, splashes keyed to replicated wave hits, drips only while the replicated water slow lasts, windup swell from the replicated cast; Unity rendering not run)");
        }
        // The body tracks the replicated position at the shot's speed, at the visual size round the hit sphere.
        private static void Follows()
        {
            var fx=new NativeMagicFx();var scene=new TestScene();var sink=new HashSink();var caster=Caster(1,"water",Vector3.Zero,45);scene.List.Add(caster);
            var dir=Vector3.Normalize(new Vector3(1,0,1));float at=1,release=1.15f;caster.AbilityId="tidal-wave";caster.AbilityAt=at;caster.AbilityRelease=release;
            var centres=new List<(float t,Vector3 c,Vector3 shot)>();var origins=new List<Vector3>();
            for(float t=release;t<release+NativeTidalWaveFx.Life-1e-4f;t+=1/60f)
            {
                scene.Shots.Clear();var p=WaveAt(Vector3.Zero,dir,release,t);scene.Shots.Add(Shot(9,1,p,dir));fx.Draw(sink,scene,t,1,false);
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
            for(;t<end-1e-4f;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(5,1,WaveAt(Vector3.Zero,Vector3.UnitZ,release,t),Vector3.UnitZ));fx.Draw(sink,scene,t,1,false);
                Check(fx.Tide.Waves.All(w=>!w.Used||w.Live),"no crash while the shot is replicated");}
            var last=WaveAt(Vector3.Zero,Vector3.UnitZ,release,t-1/60f);
            scene.Shots.Clear();sink.Reset();fx.Draw(sink,scene,t,1,false);
            var gone=fx.Tide.Waves.First(w=>w.Used&&w.Id==5);
            Check(!gone.Live&&Math.Abs(gone.GoneAt-t)<1e-5f,"crash fires the frame the shot is gone");
            float range=.6f+NativeTidalWaveFx.Range;Check(Math.Abs(gone.CrashAt.Z-range)<.6f&&Math.Abs(gone.CrashAt.Y)<1e-4f,"crash at end of range on the floor ("+gone.CrashAt.Z.ToString("0.00")+" m vs "+range.ToString("0.00")+")");
            Check(!gone.Wall,"open floor: a range-end crash, not a wall");Check(sink.Waters>10&&sink.Drops>5,"splash burst and droplets at the crash");
            float before=gone.Surface.Max.Y;fx.Draw(sink,scene,t+.25f,1,false);float later=gone.Surface.Max.Y;Check(later<before-.25f,"slumps as it crashes ("+before.ToString("0.00")+" -> "+later.ToString("0.00")+" m)");
            fx.Draw(sink,scene,t+NativeTidalWaveFx.CrashSeconds+.01f,1,false);Check(fx.Tide.Bodies.Count==0,"body gone after the crash");
            Check(fx.Decals.Count>0,"wet streak and puddles remain");
            fx.Draw(sink,scene,t+NativeTidalWaveFx.StreakSeconds+.5f,1,false);Check(fx.Decals.Count==0&&fx.Tide.Waves.All(w=>!w.Used),"the floor dries and the record is freed");
            // Into a wall 6 m ahead: the shot leaves the list at the wall; the crash is a wall crash in front of it.
            fx.Clear();scene.WallZ=6;float hitWall=release+(6-1-.6f)/NativeTidalWaveFx.Speed;
            for(t=release;t<hitWall;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(6,1,WaveAt(Vector3.Zero,Vector3.UnitZ,release,t),Vector3.UnitZ));fx.Draw(sink,scene,t,1,false);}
            scene.Shots.Clear();fx.Draw(sink,scene,t,1,false);var wall=fx.Tide.Waves.First(w=>w.Used&&w.Id==6);
            Check(wall.Wall&&wall.CrashAt.Z<6&&wall.CrashAt.Z>4,"a wall crash before the wall ("+wall.CrashAt.Z.ToString("0.00")+")");
        }
        // Meshes are built once; a warm frame with several waves, splashes and marks allocates nothing.
        private static void Budget()
        {
            int built=NativeWaveSurface.Built;var fx=new NativeMagicFx();Check(NativeWaveSurface.Built-built==NativeTidalWaveFx.MaxWaves+4,"a fixed pool of surfaces built with the effects");built=NativeWaveSurface.Built;
            var scene=new TestScene();var sink=new HashSink();for(int i=1;i<=3;i++){var a=Caster(i,"water",new Vector3(i*4,0,0),0);a.AbilityId="tidal-wave";a.AbilityAt=1;a.AbilityRelease=1.15f;scene.List.Add(a);}
            var events=new List<ArenaEvent>{new ArenaEvent{Id=1,Kind=EventKind.Hit,Time=1.3f,Source=1,Target=7,Position=new V3(4,1.6f,3),Amount=22,Detail="ability"}};
            void Frame(float t){scene.Shots.Clear();if(t<1.85f)for(int i=1;i<=3;i++)scene.Shots.Add(Shot(20+i,i,WaveAt(new Vector3(i*4,0,0),Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,1,false);}
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
                for(float t=1;t<3.2f;t+=1/60f){scene.Shots.Clear();if(t>=1.15f&&t<1.85f)scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Observe(events,t,scene.List);sink.Reset();fx.Draw(sink,scene,t,1,false);
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
                for(float t=1.15f;t<2.6f;t+=1/60f){scene.Shots.Clear();if(t<1.85f)scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Draw(sink,scene,t,quality,low);total+=fx.Tide.Particles;}
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
                for(float t=1.15f;t<1.4f;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));fx.Observe(events,t,scene.List);fx.Draw(sink,scene,t,1,false);}
                // With and without the hit: the difference is the splash.
                scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,1.5f),Vector3.UnitZ));fx.Observe(events,1.5f,scene.List);sink.Reset();fx.Draw(sink,scene,1.5f,1,false);int with=sink.Calls;
                var bare=new NativeMagicFx();var sink2=new HashSink();for(float t=1.15f;t<1.4f;t+=1/60f){scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,t),Vector3.UnitZ));bare.Draw(sink2,scene,t,1,false);}
                scene.Shots.Clear();scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,1.5f),Vector3.UnitZ));sink2.Reset();bare.Draw(sink2,scene,1.5f,1,false);
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
                fx.Observe(events,at,scene.List);fx.Draw(sink,scene,at,1,false);return sink.Drops+sink.Waters;
            }
            Check(Drips(true,2.5f,false,1.4f)>0,"a soaked victim drips while slowed");Check(Drips(false,2.5f,false,1.4f)==0,"no drips without a water hit");
            Check(Drips(true,2.5f,true,1.4f)==0,"Shadow Shackles' slow is not soaked");Check(Drips(true,1.3f,false,1.4f)==0,"no drips after the slow ends");
        }
        // The windup swell rises from the replicated cast before the shot exists; the caster's own first-person view keeps it low.
        private static void Windup()
        {
            var fx=new NativeMagicFx();var scene=new TestScene();var caster=Caster(1,"water",Vector3.Zero,0);caster.AbilityId="tidal-wave";caster.AbilityAt=1;caster.AbilityRelease=1.15f;scene.List.Add(caster);var sink=new HashSink();
            fx.Draw(sink,scene,.99f,1,false);Check(fx.Tide.Bodies.Count==0,"nothing before the cast");
            fx.Draw(sink,scene,1.12f,1,false);Check(fx.Tide.Bodies.Count==1&&fx.Tide.Poses[0].Height>.8f,"a swell rises during the windup");float third=fx.Tide.Poses[0].Height;
            scene.ViewerId=1;fx.Draw(sink,scene,1.12f,1,false);Check(fx.Tide.Poses[0].Height<third*.6f&&fx.Tide.Poses[0].NearStart>=1.5f,"low and faded near the eye in the caster's own first person");
            scene.Shots.Add(Shot(3,1,WaveAt(Vector3.Zero,Vector3.UnitZ,1.15f,1.16f),Vector3.UnitZ));fx.Draw(sink,scene,1.16f,1,false);
            Check(fx.Tide.Bodies.Count==1&&fx.Tide.Waves.Any(w=>w.Used&&w.Live&&w.Own),"the shot takes over from the swell (one body)");
            Check(fx.Tide.Poses[0].NearStart>=NativeTidalWaveFx.OwnNearStart-1e-4f,"the caster's own wave fades near the camera");
        }
    }
}
#endif
