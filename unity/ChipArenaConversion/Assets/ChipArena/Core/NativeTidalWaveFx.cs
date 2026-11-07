using System;
using System.Collections.Generic;
using System.Numerics;
using static ChipArena.UnityConversion.NativeMagicMath;

namespace ChipArena.UnityConversion
{
    /// <summary>What one wave body looks like this frame: its floor point and travel direction, size and curl, its crash
    /// (slump), opacity, flow clock and near-camera fade.</summary>
    public struct NativeWavePose
    {
        /// <summary>The floor point under the wave's hit centre (moved a little ahead) and the horizontal travel direction.</summary>
        public Vector3 Origin,Forward;
        /// <summary>Crest height and full width (m); curl 0 (a swell) to 1 (a breaking lip); slump 0 to 1 (the crash's collapse).</summary>
        public float Height,Width,Curl,Slump,Lean,Opacity,Flow,Foam,NearStart,NearLength;
        public Vector3 Tint;
    }

    /// <summary>
    /// The Tidal Wave body: a curling sheet, Columns across and Rows along its profile (back toe, up the back, over the crest,
    /// the lip curling forward and down, into the barrel and down the face to the front toe), built once: its triangles and
    /// the profile tables resampled at construction; a frame only blends the tables, places the vertices and their normals in
    /// the reused vertex array. The ends are lower, lag behind (a crescent front seen from above) and fade out. Data carries
    /// the across and along-profile distances (m) for the flowing surface, a foam weight (crest, lip, toes, ends) and a
    /// thickness (dark core, light edges).
    /// </summary>
    public sealed class NativeWaveSurface
    {
        public const int Columns=33,Rows=26;
        /// <summary>How many surfaces were ever built (checks: a pool builds a few once, never per frame).</summary>
        public static int Built;
        public readonly int[] Triangles;public readonly NativeMagicVertex[] Vertices=new NativeMagicVertex[Columns*Rows];
        public Vector3 Min,Max;
        public int VertexCount=>Columns*Rows;
        // The profile in units of the crest height: (forward, up) per control point, for a swell, a breaking wave and the crash.
        private static readonly float[] Swell={-1.30f,0,-.98f,.20f,-.66f,.47f,-.38f,.74f,-.14f,.93f,.08f,1,.26f,.95f,.38f,.82f,.44f,.66f,.45f,.58f,.45f,.50f,.45f,.42f,.46f,.28f,.50f,.13f,.58f,0};
        private static readonly float[] Break={-1.45f,0,-1.06f,.21f,-.72f,.49f,-.42f,.77f,-.14f,.96f,.12f,1,.38f,.93f,.58f,.76f,.66f,.56f,.56f,.47f,.40f,.53f,.27f,.47f,.22f,.30f,.27f,.13f,.38f,0};
        private static readonly float[] Slumped={-1.60f,0,-1.15f,.14f,-.70f,.27f,-.30f,.36f,.10f,.40f,.48f,.38f,.82f,.30f,1.08f,.18f,1.22f,.08f,1.12f,.05f,.95f,.07f,.80f,.06f,.70f,.04f,.72f,.02f,.80f,0};
        private static readonly float[] Thick={.25f,.55f,.75f,.85f,.80f,.70f,.55f,.35f,.18f,.12f,.30f,.50f,.75f,.60f,.30f};
        private static readonly float[] FoamOf={.34f,.06f,0,0,.04f,.22f,.36f,.68f,1,.82f,.34f,.1f,.04f,.12f,.62f};
        /// <summary>The row nearest the lip's outer edge (the spray leaves from it).</summary>
        public static readonly int LipRow=(int)Math.Round(7.0/(Swell.Length/2-1)*(Rows-1));
        private static readonly float[] sz=new float[Rows],sy=new float[Rows],bz=new float[Rows],by=new float[Rows],kz=new float[Rows],ky=new float[Rows],th=new float[Rows],fo=new float[Rows];
        static NativeWaveSurface()
        {
            int n=Swell.Length/2;
            for(int j=0;j<Rows;j++)
            {
                float t=j/(float)(Rows-1)*(n-1);
                sz[j]=Spline(Swell,t,0);sy[j]=Math.Max(0,Spline(Swell,t,1));bz[j]=Spline(Break,t,0);by[j]=Math.Max(0,Spline(Break,t,1));kz[j]=Spline(Slumped,t,0);ky[j]=Math.Max(0,Spline(Slumped,t,1));
                th[j]=Linear(Thick,t);fo[j]=Linear(FoamOf,t);
            }
        }
        // Catmull-Rom through the control points (pairs), at parameter t (0..n-1), coordinate c (0 forward, 1 up).
        private static float Spline(float[] p,float t,int c)
        {
            int n=p.Length/2,k=Math.Min(n-2,(int)Math.Floor(t));float f=t-k;
            float P(int i)=>p[Math.Max(0,Math.Min(n-1,i))*2+c];
            float p0=P(k-1),p1=P(k),p2=P(k+1),p3=P(k+2);
            return .5f*(2*p1+(-p0+p2)*f+(2*p0-5*p1+4*p2-p3)*f*f+(-p0+3*p1-3*p2+p3)*f*f*f);
        }
        private static float Linear(float[] p,float t){int k=Math.Min(p.Length-2,(int)Math.Floor(t));float f=t-k;return p[k]+(p[k+1]-p[k])*f;}
        public NativeWaveSurface()
        {
            Built++;Triangles=new int[(Columns-1)*(Rows-1)*6];int w=0;
            for(int i=0;i<Columns-1;i++)for(int j=0;j<Rows-1;j++)
            {int a=i*Rows+j,b=a+Rows,c=b+1,d=a+1;Triangles[w++]=a;Triangles[w++]=b;Triangles[w++]=c;Triangles[w++]=a;Triangles[w++]=c;Triangles[w++]=d;}
        }
        /// <summary>The profile point of row j for a column's curl and slump, in units of the crest height (z forward, y up).</summary>
        public static void Profile(int j,float curl,float slump,out float z,out float y)
        {
            float c=Clamp01(curl);z=sz[j]+(bz[j]-sz[j])*c;y=sy[j]+(by[j]-sy[j])*c;
            if(slump>0){z+=(kz[j]-z)*slump;y+=(ky[j]-y)*slump;}
        }
        // The across shares at |s|: a flat-topped wall whose ends drop away, lag behind (a crescent front) and curl less.
        private static float Shares(float a,out float lag,out float curl){float a2=a*a;lag=.42f*a2;curl=1-.55f*a2;float end=Smooth((a-.42f)/.58f);return 1-.84f*end*(float)Math.Sqrt(end);}
        /// <summary>Column i's across coordinate (-1..1), height share, lag behind the centre (units of the height) and curl share.</summary>
        public static void Column(int i,out float s,out float height,out float lag,out float curl)
        {
            s=i/(float)(Columns-1)*2-1;float a=Math.Abs(s);height=Shares(a,out lag,out curl);
        }
        /// <summary>The world point of row j in column s of a pose (the spray's origin on the lip).</summary>
        public static Vector3 Point(in NativeWavePose p,float s,int j)
        {
            float hf=Shares(Math.Abs(s),out float lag,out float cshare),cs=p.Curl*cshare;Profile(j,cs,p.Slump,out float z,out float y);
            var f=p.Forward;var r=Right(f);float H=p.Height,depth=.8f*H*(.55f+.45f*hf);
            return p.Origin+r*(s*p.Width*.5f)+Vector3.UnitY*(y*H*hf)+f*(z*depth+y*H*hf*p.Lean-lag*H);
        }
        /// <summary>Places the body for a pose: positions, normals and data in Vertices, its bounds in Min/Max.</summary>
        public void Fill(in NativeWavePose p)
        {
            var f=p.Forward;var r=Right(f);float H=p.Height,half=p.Width*.5f;Min=new Vector3(float.MaxValue);Max=new Vector3(float.MinValue);
            var color=new Vector4(p.Tint,p.Opacity);
            for(int i=0;i<Columns;i++)
            {
                Column(i,out float s,out float hf,out float lag,out float cshare);float a=Math.Abs(s);
                float cs=p.Curl*cshare,depth=.8f*H*(.55f+.45f*hf),endFade=1-Smooth((a-.82f)/.18f),endFoam=.4f*Smooth((a-.68f)/.3f),along=0;
                Vector3 prev=default;
                for(int j=0;j<Rows;j++)
                {
                    Profile(j,cs,p.Slump,out float z,out float y);
                    var at=p.Origin+r*(s*half)+Vector3.UnitY*(y*H*hf)+f*(z*depth+y*H*hf*p.Lean-lag*H);
                    if(j>0)along+=Vector3.Distance(at,prev);prev=at;
                    float foam=Clamp01(fo[j]*(.6f+.4f*Clamp01(p.Curl))+endFoam+p.Foam*(.35f+.65f*fo[j]));
                    float thick=th[j]*(.45f+.55f*hf)*(1-.6f*p.Slump);
                    ref var v=ref Vertices[i*Rows+j];v.Position=at;v.Color=new Vector4(color.X,color.Y,color.Z,color.W*endFade);
                    v.Data=new Vector4(s*half,along,foam,thick);v.Extra=new Vector4(p.Flow,0,p.NearStart,p.NearLength);
                    Min=Vector3.Min(Min,at);Max=Vector3.Max(Max,at);
                }
            }
            for(int i=0;i<Columns;i++)for(int j=0;j<Rows;j++)
            {
                var du=Vertices[Math.Min(Columns-1,i+1)*Rows+j].Position-Vertices[Math.Max(0,i-1)*Rows+j].Position;
                var dv=Vertices[i*Rows+Math.Min(Rows-1,j+1)].Position-Vertices[i*Rows+Math.Max(0,j-1)].Position;
                var n=Vector3.Cross(dv,du);float l=n.Length();Vertices[i*Rows+j].Normal=l>1e-6f?n/l:Vector3.UnitY;
            }
        }
    }

    /// <summary>
    /// The Tidal Wave (water Q; NativeAbilities: speed 24, gravity 0, range 16.8 over .7 s, hit radius 1, .15 s windup) drawn as
    /// a breaking water wall instead of the source's translucent box, entirely from what every peer holds:
    ///  - the windup (the caster's replicated AbilityId/AbilityAt/AbilityRelease): a swell rising from the floor in front of
    ///    the caster (low and see-through in the caster's own first-person view);
    ///  - travel (the replicated ability shot's presented position and velocity, its floor under it): a wave about 2.2 m growing
    ///    to 3.2 m wide and 1.15 m growing to 2 m tall around the 1 m hit sphere, its lip curling forward as it goes, white foam
    ///    on the crest, lip, toes and ends, spray thrown off the lip, foam puffs left behind and a wet streak with puddles on
    ///    the floor it passed (fading over 2.6 s);
    ///  - the crash (the shot gone from the replicated list: range end, a wall, a shield or the ceiling): the lip pitches
    ///    forward and the wall slumps and fades over .45 s with a splash burst, droplets and a foam ring (against a wall the
    ///    spray goes up it);
    ///  - hits (a replicated Hit with Detail "ability" from a water caster at a wave): a splash burst on the victim, and while
    ///    the victim's replicated slow lasts after a water hit, drips falling off them.
    /// The age is from the caster's replicated release (first sight for a late joiner). Spray, splashes, foam and drips are
    /// pure functions of time slots and seeds (no pool to step), so host, guests and the killcam draw the same frame.
    /// Effects quality (Reduced .3) and the low graphics tier thin them. Cosmetic only.
    /// </summary>
    public sealed class NativeTidalWaveFx
    {
        // ---- the simulation's numbers (GameContent tidal-wave; NativeAbilities.ExecuteAbility) ----
        public const float Speed=24,Range=16.8f,Life=.7f,HitRadius=1,Windup=.15f;
        // ---- the look ----
        public const float StartHeight=1.15f,FullHeight=2f,StartWidth=2.4f,FullWidth=3.4f,GrowSeconds=.28f,CurlSeconds=.45f,OriginLead=.1f;
        /// <summary>The caster's own wave in first person: invisible within this many metres of the eye, full beyond start + length.</summary>
        public const float OwnNearStart=1.8f,OwnNearLength=4.2f;
        public const float CrashSeconds=.45f,StreakSeconds=2.6f,SplashSeconds=.9f,DripSeconds=1.6f,PathStep=.3f,FloorReach=1.6f,CenterHeight=1.02f;
        public const int Tint=0x3fb6f2,SprayLit=0xeafcff,SprayShade=0x4f9fd6,FoamLit=0xf2fdff,FoamShade=0x7fb9dd;
        public const int MaxWaves=8,MaxSamples=72,MaxHits=24,MaxDrips=16;
        /// <summary>A wave this presentation remembers: live (its shot replicated this frame) or crashing and drying.</summary>
        public sealed class Wave
        {
            public long Key;public int Id,Owner;public bool Used,Live,Seen,Own,Wall;
            public float Born,LastSeen,GoneAt,Floor;public Vector3 Last,Velocity,Forward,CrashAt,WallNormal;
            public NativeWavePose Pose;public NativeWaveSurface Surface;
            public int Samples;public readonly Vector3[] Path=new Vector3[MaxSamples];public readonly float[] PathTime=new float[MaxSamples],PathDistance=new float[MaxSamples],PathWidth=new float[MaxSamples];
            public float Travel=>Samples>0?PathDistance[Samples-1]:0;
        }
        private struct HitRecord{public int Id,Source,Target;public float Time;public Vector3 At;public bool Water,Wave,Checked;}
        private struct Drip{public int Target;public float Hit;}
        private readonly Wave[] waves=new Wave[MaxWaves];private readonly NativeWaveSurface[] surfaces=new NativeWaveSurface[MaxWaves];
        private readonly HitRecord[] hits=new HitRecord[MaxHits];private int hitNext;private readonly Drip[] drips=new Drip[MaxDrips];
        /// <summary>The wave bodies drawn this frame (the surface and its pose), for the Runtime to upload; checks read them.</summary>
        public readonly List<NativeWaveSurface> Bodies=new List<NativeWaveSurface>(MaxWaves+4);
        public readonly List<NativeWavePose> Poses=new List<NativeWavePose>(MaxWaves+4);
        /// <summary>Particles queued this frame (checks).</summary>
        public int Particles;
        public IReadOnlyList<Wave> Waves=>waves;
        public NativeTidalWaveFx()
        {
            for(int i=0;i<MaxWaves;i++){waves[i]=new Wave();surfaces[i]=new NativeWaveSurface();}
            // Windup swells use their own surfaces (one per caster in view, up to four at once).
            for(int i=0;i<windups.Length;i++)windups[i]=new NativeWaveSurface();
        }
        private readonly NativeWaveSurface[] windups=new NativeWaveSurface[4];
        public void Clear()
        {
            foreach(var w in waves){w.Used=false;w.Live=false;w.Samples=0;}
            for(int i=0;i<hits.Length;i++)hits[i]=default;hitNext=0;for(int i=0;i<drips.Length;i++)drips[i]=default;Bodies.Clear();Poses.Clear();
        }

        // ---- the shape over the wave's life ----
        /// <summary>The body's shape at an age (s since the caster's release): height, width, curl and forward lean.</summary>
        public static void Shape(float age,out float height,out float width,out float curl,out float lean)
        {
            float g=Smooth(age/GrowSeconds);height=StartHeight+(FullHeight-StartHeight)*g+.04f*(float)Math.Sin(age*9)*g;
            width=StartWidth+(FullWidth-StartWidth)*Smooth(age/.35f);curl=.25f+.75f*Smooth((age-.05f)/CurlSeconds);lean=.12f*curl;
        }
        /// <summary>The crash at u (0..1 of CrashSeconds): the height kept, the slump, the extra curl, the slide (m) and opacity.</summary>
        public static void Crash(float u,bool wall,out float keep,out float slump,out float slide,out float opacity)
        {
            u=Clamp01(u);keep=1-.62f*Smooth(u*1.1f);slump=Smooth(u*1.4f);slide=(wall?.3f:1.7f)*(1-(float)Math.Exp(-5*u));opacity=1-Smooth((u-.25f)/.75f);
        }
        /// <summary>The age a wave is drawn at: from its caster's replicated release, else its first sight.</summary>
        public static float BirthOf(Actor caster,float firstSeen)
        {
            if(caster!=null&&caster.AbilityId=="tidal-wave"){float since=firstSeen-caster.AbilityRelease;if(since>=-.05f&&since<Life+.1f)return Math.Min(caster.AbilityRelease,firstSeen);}
            return firstSeen;
        }

        // ---- event intake (NativeMagicFx.Observe) ----
        /// <summary>A replicated Hit: a water caster's hit is recorded for the splash (Detail "ability", at a wave) and the drips.</summary>
        public void Hit(in ArenaEvent e,string sourceSpec)
        {
            if(sourceSpec!="water"||e.Amount<=0||e.Target==0)return;
            bool ability=e.Detail=="ability",direct=e.Detail=="direct";if(!ability&&!direct)return;
            hits[hitNext]=new HitRecord{Id=e.Id,Source=e.Source,Target=e.Target,Time=e.Time,At=new Vector3(e.Position.x,e.Position.y,e.Position.z),Water=true,Wave=false,Checked=!ability};hitNext=(hitNext+1)%hits.Length;
            int slot=-1;for(int i=0;i<drips.Length;i++){if(drips[i].Target==e.Target){slot=i;break;}if(slot<0&&drips[i].Target==0)slot=i;}
            if(slot<0){slot=0;for(int i=1;i<drips.Length;i++)if(drips[i].Hit<drips[slot].Hit)slot=i;}
            if(drips[slot].Target!=e.Target||e.Time>drips[slot].Hit)drips[slot]=new Drip{Target=e.Target,Hit=e.Time};
        }

        // ---- the frame ----
        private Wave Find(long key){foreach(var w in waves)if(w.Used&&w.Key==key)return w;return null;}
        private Wave Take(float time)
        {
            foreach(var w in waves)if(!w.Used)return w;
            Wave oldest=null;foreach(var w in waves)if(!w.Live&&(oldest==null||w.GoneAt<oldest.GoneAt))oldest=w;
            if(oldest!=null)return oldest;foreach(var w in waves)if(oldest==null||w.Born<oldest.Born)oldest=w;return oldest;
        }
        private static float FloorAt(IMagicFxScene scene,Vector3 at)
        {float f=scene.Floor(at);return float.IsNaN(f)||at.Y-f>FloorReach||f>at.Y?at.Y-CenterHeight:f;}
        private static void Sample(Wave w,Vector3 floorPoint,float time,float width)
        {
            if(w.Samples>0&&Vector3.Distance(w.Path[w.Samples-1],floorPoint)<PathStep)return;
            if(w.Samples==MaxSamples){Array.Copy(w.Path,1,w.Path,0,MaxSamples-1);Array.Copy(w.PathTime,1,w.PathTime,0,MaxSamples-1);Array.Copy(w.PathDistance,1,w.PathDistance,0,MaxSamples-1);Array.Copy(w.PathWidth,1,w.PathWidth,0,MaxSamples-1);w.Samples--;}
            float d=w.Samples>0?w.PathDistance[w.Samples-1]+Vector3.Distance(w.Path[w.Samples-1],floorPoint):0;
            w.Path[w.Samples]=floorPoint;w.PathTime[w.Samples]=time;w.PathDistance[w.Samples]=d;w.PathWidth[w.Samples]=width;w.Samples++;
        }
        /// <summary>The wave's floor point at a past time, from its path (clamped to the recorded span).</summary>
        public static Vector3 PathAt(Wave w,float time)
        {
            if(w.Samples==0)return w.Last;if(time<=w.PathTime[0])return w.Path[0];
            for(int i=1;i<w.Samples;i++)if(time<=w.PathTime[i]){float span=w.PathTime[i]-w.PathTime[i-1];float f=span>1e-5f?(time-w.PathTime[i-1])/span:1;return Vector3.Lerp(w.Path[i-1],w.Path[i],f);}
            return w.Path[w.Samples-1];
        }
        private static int PathIndex(Wave w,float distance)
        {int i=1;while(i<w.Samples-1&&w.PathDistance[i]<distance)i++;return i;}
        /// <summary>The floor point and the time the wave passed a distance along its path.</summary>
        public static bool AtDistance(Wave w,float distance,out Vector3 at,out float time)
        {
            at=default;time=0;if(w.Samples<2||distance<0||distance>w.PathDistance[w.Samples-1])return false;int i=PathIndex(w,distance);
            float span=w.PathDistance[i]-w.PathDistance[i-1],f=span>1e-5f?Clamp01((distance-w.PathDistance[i-1])/span):1;
            at=Vector3.Lerp(w.Path[i-1],w.Path[i],f);time=w.PathTime[i-1]+(w.PathTime[i]-w.PathTime[i-1])*f;return true;
        }

        /// <summary>Steps the tracked waves from the scene's replicated shots and draws every live part of the frame.</summary>
        public void Draw(IMagicFxSink sink,IMagicFxScene scene,NativeMagicDecals decals,float time,float quality,bool low)
        {
            Bodies.Clear();Poses.Clear();Particles=0;int viewer=scene.Viewer;var actors=scene.Actors;
            foreach(var w in waves)w.Seen=false;
            for(int i=0;i<scene.Waves;i++)
            {
                if(!scene.Wave(i,out var s))continue;var w=Find(s.Key);
                if(w==null)
                {
                    w=Take(time);w.Used=true;w.Live=true;w.Key=s.Key;w.Id=s.Id;w.Owner=s.Owner;w.Samples=0;w.GoneAt=-1;w.Wall=false;
                    w.Born=BirthOf(NativeMagicFx.Find(actors,s.Owner),time);w.Forward=Flat(s.Velocity,Vector3.UnitZ);
                }
                w.Seen=true;w.Live=true;w.Last=s.Position;w.Velocity=s.Velocity;w.Forward=Flat(s.Velocity,w.Forward);w.LastSeen=time;w.Own=viewer!=0&&w.Owner==viewer;
                w.Floor=FloorAt(scene,s.Position);Shape(time-w.Born,out _,out float width,out _,out _);
                Sample(w,new Vector3(s.Position.X,w.Floor,s.Position.Z),time,width);
            }
            foreach(var w in waves)
            {
                if(!w.Used||!w.Live||w.Seen)continue;
                // Gone from the replicated list: the crash, where it was last presented (a frame's travel on at most).
                w.Live=false;w.GoneAt=time;var at=w.Last+w.Velocity*Math.Min(.034f,Math.Max(0,time-w.LastSeen));
                if(scene.Surface(w.Last,w.Forward,HitRadius+1.1f,out var point,out var normal)&&Math.Abs(normal.Y)<.5f){w.Wall=true;w.WallNormal=normal;at=point-w.Forward*(HitRadius*.55f);}
                w.CrashAt=new Vector3(at.X,w.Floor,at.Z);Shape(w.GoneAt-w.Born,out _,out float width,out _,out _);Sample(w,w.CrashAt,time,width);
                if(w.Samples>0&&Vector3.Distance(w.Path[w.Samples-1],w.CrashAt)>1e-3f){w.Path[w.Samples-1]=w.CrashAt;}
            }
            foreach(var w in waves)
            {
                if(!w.Used)continue;if(!w.Live&&time-w.GoneAt>StreakSeconds+.2f){w.Used=false;continue;}
                Body(w,time);
                if(w.Live)Spray(sink,w,time,quality,low);else CrashBurst(sink,w,time,quality,low);
                FoamTrail(sink,scene,w,time,quality,low);Marks(decals,w,time,quality,low);
            }
            Windups(scene,time,viewer);
            Splashes(sink,scene,time,quality,low);Drips(sink,scene,time,quality,low);
        }
        // The body: shape by age, the crash's slump, the near-camera fade (the caster's own wave fades much nearer the eye).
        private void Body(Wave w,float time)
        {
            float crashU=w.Live?0:(time-w.GoneAt)/CrashSeconds;if(crashU>=1)return;
            float age=(w.Live?time:w.GoneAt)-w.Born;Shape(age,out float height,out float width,out float curl,out float lean);
            Vector3 origin;float slump=0,opacity=Smooth((time-w.Born)/.05f),foam=0;
            if(w.Live)origin=new Vector3(w.Last.X,w.Floor,w.Last.Z)+w.Forward*OriginLead;
            else
            {
                Crash(crashU,w.Wall,out float keep,out slump,out float slide,out float fade);height*=keep;width*=1+.15f*slump;curl=Math.Min(1,curl+.4f*crashU);lean+=.25f*slump;opacity*=fade;foam=.3f*slump+.12f;
                origin=w.CrashAt+w.Forward*(OriginLead+slide);
            }
            var pose=new NativeWavePose{Origin=origin,Forward=w.Forward,Height=height,Width=width,Curl=curl,Slump=slump,Lean=lean,Opacity=.9f*opacity,Flow=time-w.Born,Foam=foam,
                NearStart=w.Own?OwnNearStart:.35f,NearLength=w.Own?OwnNearLength:1.4f,Tint=Lin(Tint)};
            w.Pose=pose;int slot=Array.IndexOf(waves,w);var surface=surfaces[slot];surface.Fill(pose);w.Surface=surface;Bodies.Add(surface);Poses.Add(pose);
        }
        // The windup: a swell rising off the floor in front of each tidal-wave caster until its shot is seen.
        private void Windups(IMagicFxScene scene,float time,int viewer)
        {
            var actors=scene.Actors;int used=0;if(actors==null)return;
            for(int i=0;i<actors.Count&&used<windups.Length;i++)
            {
                var a=actors[i];if(a==null||!a.Alive||a.AbilityId!="tidal-wave")continue;float span=Math.Max(.02f,a.AbilityRelease-a.AbilityAt),p=(time-a.AbilityAt)/span;
                if(p<0||time>a.AbilityRelease+.06f)continue;bool shot=false;foreach(var w in waves)if(w.Used&&w.Owner==a.Id&&w.Born>=a.AbilityAt-.01f){shot=true;break;}if(shot)continue;
                if(!scene.Feet(a.Id,out var feet))feet=new Vector3(a.Position.x,a.Position.y,a.Position.z);var f=Forward(a.Yaw);bool own=viewer!=0&&a.Id==viewer;
                float k=Smooth(p);var pose=new NativeWavePose{Origin=feet+f*(own?2.2f:1.05f),Forward=f,Height=(.18f+.97f*k)*(own?.4f:1),Width=1.2f+1f*k,Curl=.1f+.15f*k,Lean=0,Opacity=.85f*Smooth(p/.35f),Flow=time-a.AbilityAt,Foam=.2f,
                    NearStart=own?OwnNearStart:.35f,NearLength=own?OwnNearLength:1.4f,Tint=Lin(Tint)};
                var surface=windups[used++];surface.Fill(pose);Bodies.Add(surface);Poses.Add(pose);
            }
        }
        // ---- spray off the lip: slot k is born at k / rate on the lip of the pose the wave had then ----
        public const float SprayRate=72,SprayLifeMin=.32f,SprayLifeMax=.62f,Gravity=9.8f;
        private void Spray(IMagicFxSink sink,Wave w,float time,float quality,bool low)
        {
            float rate=SprayRate*Math.Max(.3f,Math.Min(1,quality))*(low?.55f:1);
            for(long k=(long)Math.Floor((time-SprayLifeMax)*rate);k<=(long)Math.Floor(time*rate);k++)
            {
                float born=k/rate,a=time-born,life=SprayLifeMin+(SprayLifeMax-SprayLifeMin)*H(k,1+(uint)w.Id);if(a<0||a>=life||born<w.Born+.05f||born>w.LastSeen)continue;
                Shape(born-w.Born,out float height,out float width,out float curl,out float lean);var o=PathAt(w,born);
                var pose=new NativeWavePose{Origin=o+w.Forward*OriginLead,Forward=w.Forward,Height=height,Width=width,Curl=curl,Lean=lean};
                float s=(H(k,7)*2-1)*.92f;var from=NativeWaveSurface.Point(pose,s,NativeWaveSurface.LipRow+(H(k,8)<.5f?0:-2));var r=Right(w.Forward);
                var v0=w.Forward*(Speed*.42f+4*H(k,9))+Vector3.UnitY*(1.8f+3.6f*H(k,10))+r*(s*2.2f+(H(k,11)-.5f)*1.5f);
                var at=from+v0*a+new Vector3(0,-.5f*Gravity*a*a,0);var v=v0+new Vector3(0,-Gravity*a,0);if(at.Y<w.Floor+.02f)continue;float u=a/life;
                if(k%3==0)sink.Drop(at,v*.035f,Lin(SprayLit),.9f*(1-u*u*u*u),.05f+.03f*H(k,12),u,k);
                else sink.Water(at,Lin(SprayLit),SprayShade,1,(.18f+.2f*H(k,13))*(.7f+.6f*u),v*.02f,u,k,w.Floor);
                Particles++;
            }
        }
        // ---- churned water at the wave's back toe: a pair of low foam puffs every FoamStep metres of its travel, short-lived (the
        // lace it leaves is the wet streak's foam, Marks) ----
        public const float FoamStep=.85f,FoamLife=.45f;
        private void FoamTrail(IMagicFxSink sink,IMagicFxScene scene,Wave w,float time,float quality,bool low)
        {
            float step=FoamStep/Math.Max(.3f,Math.Min(1,quality))*(low?1.6f:1);if(w.Samples<2)return;var eye=scene.Eye;
            int first=Math.Max(0,(int)Math.Floor((DistanceAt(w,time-FoamLife))/step)),last=(int)Math.Floor(w.Travel/step);
            for(int j=first;j<=last;j++)
            {
                if(!AtDistance(w,j*step,out var at,out float born))continue;float a=time-born;if(a<0||a>=FoamLife||born>w.LastSeen+.02f)continue;float u=a/FoamLife;
                var r=Right(w.Forward);Shape(born-w.Born,out float height,out float width,out _,out _);
                for(int side=0;side<2;side++)
                {
                    long k=j*2+side;float s=(side==0?-1:1)*(.15f+.7f*H(k,21));
                    var p=at-w.Forward*(height*1.05f+.3f*H(k,22))+r*(s*width*.42f)+Vector3.UnitY*(.08f+.06f*H(k,23))+r*(s*.9f*a);
                    // The caster's own wave in first person leaves nothing right under the camera.
                    if(w.Own&&Vector3.DistanceSquared(p,eye)<9)continue;
                    sink.Water(p,Lin(FoamLit),FoamShade,.7f,(.26f+.14f*H(k,24))*(.85f+.5f*u),r*(s*.06f),u,k+1000,w.Floor);Particles++;
                }
            }
        }
        private static float DistanceAt(Wave w,float time)
        {
            if(w.Samples==0||time<=w.PathTime[0])return 0;
            for(int i=1;i<w.Samples;i++)if(time<=w.PathTime[i]){float span=w.PathTime[i]-w.PathTime[i-1];float f=span>1e-5f?(time-w.PathTime[i-1])/span:1;return w.PathDistance[i-1]+(w.PathDistance[i]-w.PathDistance[i-1])*f;}
            return w.Travel;
        }
        // ---- the crash: the lip's water thrown on in big plumes, droplets, a white surge spreading on the floor ahead ----
        public const int CrashPuffs=18,CrashDrops=36,CrashRing=14;public const float CrashLife=1.1f;
        private void CrashBurst(IMagicFxSink sink,Wave w,float time,float quality,bool low)
        {
            float a=time-w.GoneAt;if(a<0||a>=CrashLife+.3f)return;var f=w.Forward;var r=Right(f);
            Shape(w.GoneAt-w.Born,out float height,out float width,out _,out _);uint seed=(uint)w.Id*31u+5u;
            // Against a wall the water climbs it and falls back; in the open it carries on forward.
            Vector3 push=w.Wall?-f:f;float climb=w.Wall?2.2f:1,carry=w.Wall?.45f:1;var front=w.CrashAt+f*(w.Wall?.2f:.7f);
            int puffs=Count(CrashPuffs,quality,low),dropsN=Count(CrashDrops,quality,low),ring=Count(CrashRing,quality,low);
            for(int j=0;j<puffs;j++)
            {
                float life=.6f+.35f*H(j,seed);if(a>=life)continue;float u=a/life,s=H(j,seed+1)*2-1;
                var from=front+r*(s*width*.42f)+Vector3.UnitY*(height*(.55f+.4f*H(j,seed+2)));
                var v0=Vector3.UnitY*((.8f+2.6f*H(j,seed+3))*climb)+push*((2.6f+3.6f*H(j,seed+4))*carry)+r*(s*1.8f);
                var at=from+v0*a+new Vector3(0,-.5f*Gravity*a*a,0);if(at.Y<w.Floor+.15f)at.Y=w.Floor+.15f;
                sink.Water(at,Lin(SprayLit),SprayShade,1,(.4f+.3f*H(j,seed+5))*(.75f+.65f*u),(v0+new Vector3(0,-Gravity*a,0))*.06f,u,j+seed*100,w.Floor);Particles++;
            }
            for(int j=0;j<dropsN;j++)
            {
                float life=.45f+.45f*H(j,seed+6);if(a>=life)continue;float u=a/life,s=H(j,seed+7)*2-1;
                var from=front+r*(s*width*.45f)+Vector3.UnitY*(height*(.5f+.5f*H(j,seed+8)));
                var v0=Vector3.UnitY*((1.6f+4f*H(j,seed+9))*climb)+push*((2.5f+6f*H(j,seed+10))*carry)+r*(s*3.4f);
                var at=from+v0*a+new Vector3(0,-.5f*Gravity*a*a,0);if(at.Y<w.Floor)continue;var v=v0+new Vector3(0,-Gravity*a,0);
                sink.Drop(at,v*.045f,Lin(SprayLit),.9f*(1-u*u),.06f+.045f*H(j,seed+11),u,j+seed*200);Particles++;
            }
            // The surge: low white water rushing out over the floor ahead and to the sides, slowing.
            for(int j=0;j<ring;j++)
            {
                float life=.9f+.35f*H(j,seed+12);if(a>=life)continue;float u=a/life,ang=(j/(float)(ring-1)-.5f)*(float)Math.PI*(w.Wall?1.5f:1.35f);
                var dir=f*(float)Math.Cos(ang)+r*(float)Math.Sin(ang);if(w.Wall)dir=-dir;float rad=(.3f+(2.2f+.8f*H(j,seed+15))*(1-(float)Math.Exp(-3.2f*a)))*(w.Wall?.8f:1);
                var at=front+dir*rad+Vector3.UnitY*(.12f+.1f*H(j,seed+13)+.25f*(1-u)*(1-u));
                sink.Water(at,Lin(FoamLit),FoamShade,.92f,(.45f+.3f*H(j,seed+14))*(.85f+.6f*u),dir*.06f,u,j+seed*300,w.Floor);Particles++;
            }
        }
        // ---- the wet streak and puddles on the floor it passed ----
        public const float PuddleStep=2.3f;
        public static float Wetness(float age)=>age<0?0:1-Smooth((age-.25f)/(StreakSeconds-.25f));
        /// <summary>The foam lace on the streak: in as the wave's back passes, gone about 1.2 s later.</summary>
        public static float Lace(float age)=>Smooth((age-.04f)/.12f)*(1-Smooth((age-.2f)/.6f));
        private void Marks(NativeMagicDecals decals,Wave w,float time,float quality,bool low)
        {
            if(w.Samples<2)return;var f=w.Forward;var r=Right(f);const float lift=.015f;
            for(int i=0;i+1<w.Samples;i++)
            {
                float wa=Wetness(time-w.PathTime[i]),wb=Wetness(time-w.PathTime[i+1]);if(wa<=0&&wb<=0)continue;float fa=Lace(time-w.PathTime[i]),fb=Lace(time-w.PathTime[i+1]);
                var dir=Flat(w.Path[i+1]-w.Path[i],f);var side=Right(dir);float ha=w.PathWidth[i]*.42f,hb=w.PathWidth[i+1]*.42f;
                var pa=w.Path[i]+Vector3.UnitY*lift;var pb=w.Path[i+1]+Vector3.UnitY*lift;float da=w.PathDistance[i],db=w.PathDistance[i+1];
                decals.Quad(pa-side*ha,pa+side*ha,pb+side*hb,pb-side*hb,new Vector4(-1,da,wa,fa),new Vector4(1,da,wa,fa),new Vector4(1,db,wb,fb),new Vector4(-1,db,wb,fb),Vector3.UnitY,w.Id,1);
            }
            int last=(int)Math.Floor(w.Travel/PuddleStep);
            for(int j=1;j<=last;j++)
            {
                if(!AtDistance(w,j*PuddleStep-.8f*H(j,(uint)w.Id+40),out var at,out float born))continue;float age=time-born,wet=Wetness(age);if(wet<=0)continue;
                float half=.45f+.4f*H(j,(uint)w.Id+41);var c=at+r*((H(j,(uint)w.Id+42)-.5f)*FullWidth*.5f)+Vector3.UnitY*(lift+.002f);
                decals.Disc(c,Vector3.UnitY,half,H(j,(uint)w.Id+43)*6.283f,wet,age,j+w.Id*17,2);
            }
            if(!w.Live){float age=time-w.GoneAt,wet=Wetness(age);if(wet>0)decals.Disc(w.CrashAt+f*1.2f+Vector3.UnitY*(lift+.004f),Vector3.UnitY,1.5f,w.Id*1.7f,wet,age,w.Id*13+7,2);}
        }
        // ---- a splash on each victim the wave hits ----
        public const int SplashPuffs=16,SplashDrops=14;
        private void Splashes(IMagicFxSink sink,IMagicFxScene scene,float time,float quality,bool low)
        {
            for(int i=0;i<hits.Length;i++)
            {
                ref var h=ref hits[i];if(h.Id==0||!h.Water)continue;float a=time-h.Time;if(a>SplashSeconds+.2f||a<-.05f)continue;
                if(!h.Checked)
                {
                    // A wave of this caster still live, or crashing within a moment of the hit, near the victim: the wave's hit.
                    foreach(var w in waves)if(w.Used&&w.Owner==h.Source&&(w.Live||w.GoneAt>=h.Time-.15f)&&Vector3.Distance(new Vector3(w.Last.X,h.At.Y,w.Last.Z),h.At)<HitRadius+2.6f){h.Wave=true;break;}
                    if(h.Wave||a>.25f)h.Checked=true;
                }
                if(!h.Wave)continue;Vector3 f=Vector3.UnitZ;foreach(var w in waves)if(w.Used&&w.Owner==h.Source){f=w.Forward;break;}var r=Right(f);uint seed=(uint)h.Id*13u+3u;
                int puffs=Count(SplashPuffs,quality,low),dropsN=Count(SplashDrops,quality,low);
                for(int j=0;j<puffs;j++)
                {
                    float life=.45f+.35f*H(j,seed);if(a<0||a>=life)continue;float u=a/life,s=H(j,seed+1)*2-1;
                    var v0=f*(2f+4.5f*H(j,seed+2))+Vector3.UnitY*(1.2f+3.6f*H(j,seed+3))+r*(s*3.2f);var from=h.At+r*(s*.3f)+Vector3.UnitY*((H(j,seed+4)-.5f)*.6f);
                    var at=from+v0*a+new Vector3(0,-.5f*Gravity*a*a,0);sink.Water(at,Lin(SprayLit),SprayShade,1,(.24f+.22f*H(j,seed+5))*(.8f+.6f*u),(v0+new Vector3(0,-Gravity*a,0))*.02f,u,j+seed*10,NoFloor);Particles++;
                }
                for(int j=0;j<dropsN;j++)
                {
                    float life=.4f+.35f*H(j,seed+6);if(a<0||a>=life)continue;float u=a/life,s=H(j,seed+7)*2-1;
                    var v0=f*(2.5f+5f*H(j,seed+8))+Vector3.UnitY*(1.5f+4f*H(j,seed+9))+r*(s*4f);var at=h.At+v0*a+new Vector3(0,-.5f*Gravity*a*a,0);var v=v0+new Vector3(0,-Gravity*a,0);
                    sink.Drop(at,v*.04f,Lin(SprayLit),.9f*(1-u*u),.05f+.035f*H(j,seed+10),u,j+seed*20);Particles++;
                }
                // The slap: a ring of white water round the body, opening out.
                if(a<.35f){float u=a/.35f;int ring=Count(8,quality,low);for(int j=0;j<ring;j++){float ang=j*6.283f/ring+H(j,seed+11);var dir=r*(float)Math.Cos(ang)+Vector3.UnitY*(float)Math.Sin(ang)*.6f+f*.3f;
                    sink.Water(h.At+dir*(.35f+.6f*u),Lin(FoamLit),FoamShade,.95f,.32f*(1+u),dir*.04f,u,j+seed*30,NoFloor);Particles++;}}
            }
        }
        // ---- drips off a soaked victim while its replicated slow lasts after a water hit ----
        public const float DripRate=24;
        private void Drips(IMagicFxSink sink,IMagicFxScene scene,float time,float quality,bool low)
        {
            float rate=DripRate*Math.Max(.3f,Math.Min(1,quality))*(low?.55f:1);
            for(int i=0;i<drips.Length;i++)
            {
                var d=drips[i];if(d.Target==0)continue;if(time-d.Hit>DripSeconds+.6f){drips[i]=default;continue;}
                if(!scene.Slowed(d.Target,out float until,out bool shackled)||shackled||time>=until||until<d.Hit||!scene.Feet(d.Target,out var feet))continue;
                float soak=Math.Min(until,d.Hit+DripSeconds);if(time>soak)continue;
                // Water beads running down the body: one every .12 s, sliding from the shoulders for .4 s.
                for(long k=(long)Math.Floor((time-.4f)/.12f);k<=(long)Math.Floor(time/.12f);k++)
                {
                    float born=k*.12f,a=time-born;if(born<d.Hit||born>soak||a<0||a>=.4f)continue;uint salt=(uint)d.Target*7u+90u;float ang=H(k,salt)*6.283f,u=a/.4f;
                    var at=feet+new Vector3((float)Math.Cos(ang)*.42f,1.45f-.75f*u*u+.1f*H(k,salt+1),(float)Math.Sin(ang)*.42f);sink.Water(at,Lin(SprayLit),SprayShade,.9f,.1f+.04f*H(k,salt+2),new Vector3(0,-.08f,0),u*.6f,k+77,NoFloor);Particles++;
                }
                for(long k=(long)Math.Floor((time-.55f)*rate);k<=(long)Math.Floor(time*rate);k++)
                {
                    float born=k/rate;if(born<d.Hit||born>soak)continue;float a=time-born;uint salt=(uint)d.Target*7u+60u;
                    float ang=H(k,salt)*6.283f,rad=.38f+.14f*H(k,salt+1),h0=.75f+.85f*H(k,salt+2),fall=(float)Math.Sqrt(2*h0/Gravity);if(a>=fall+.12f)continue;
                    var top=feet+new Vector3((float)Math.Cos(ang)*rad,h0,(float)Math.Sin(ang)*rad);
                    if(a<fall){var at=top+new Vector3(0,-.5f*Gravity*a*a,0);sink.Drop(at,new Vector3(0,-Gravity*a-.8f,0)*.045f,Lin(SprayLit),.92f,.06f+.025f*H(k,salt+3),a/fall,k);}
                    else{float u=(a-fall)/.12f;sink.Water(new Vector3(top.X,feet.Y+.04f,top.Z),Lin(SprayLit),SprayShade,.9f,.12f*(1+u),Vector3.Zero,u,k,feet.Y);}
                    Particles++;
                }
            }
        }
    }
}
