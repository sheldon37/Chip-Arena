using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace ChipArena.UnityConversion
{
    /// <summary>One replicated projectile the magic effects draw, at its presented position (a guest's or the killcam's
    /// snapshot clock, the host's match clock). Born: the replicated spawn time where the shot carries one (bullets), else 0.</summary>
    public struct NativeMagicShot{public long Key;public int Id,Owner;public Vector3 Position,Velocity;public float Radius,Born;}

    /// <summary>
    /// What the magic effects read of the presented frame (Runtime: NativeMagicFxPresentation; the offline render harness and
    /// Tests script it). Everything here is what host, guests, spectators and the killcam replay all hold: the snapshot's
    /// actors, the replicated shots, where each pilot is drawn (its posed rig), the world's floors and walls, and the
    /// replicated slow state. Positions are Unity world metres.
    /// </summary>
    public interface IMagicFxScene
    {
        IReadOnlyList<Actor> Actors{get;}
        /// <summary>The live Tidal Wave ability shots.</summary>
        int Waves{get;}
        bool Wave(int index,out NativeMagicShot shot);
        /// <summary>The live darkness primary orbs (Owner: the firer, kept through a reflect; Born: the replicated spawn time).</summary>
        int Orbs{get;}
        bool Orb(int index,out NativeMagicShot shot);
        /// <summary>The right-hand spell socket of an actor (NativeSpellCharge.Socket); view: it is the local first-person
        /// viewer's view-model hand, drawn on the view layer.</summary>
        bool Hand(int id,out Vector3 hand,out bool view);
        /// <summary>The drawn feet position of an actor (its smoothed root).</summary>
        bool Feet(int id,out Vector3 feet);
        /// <summary>The posed chest of an actor (the first-person viewer's: under its eye).</summary>
        bool Chest(int id,out Vector3 chest);
        /// <summary>The local first-person pilot's id (0: none); its own effects fade near the camera or go to the view layer.</summary>
        int Viewer{get;}
        /// <summary>The camera position.</summary>
        Vector3 Eye{get;}
        /// <summary>The floor height under a point (the nearest upward-facing world surface below it), NaN over a pit.</summary>
        float Floor(Vector3 at);
        /// <summary>The first world surface along a ray within reach.</summary>
        bool Surface(Vector3 from,Vector3 direction,float reach,out Vector3 point,out Vector3 normal);
        /// <summary>A pilot's replicated slow (NativePilotFrame slowEnd/slowFactor): until when, and whether it is Shadow
        /// Shackles' (cuffs, not water).</summary>
        bool Slowed(int id,out float until,out bool shackled);
    }

    /// <summary>
    /// Where the magic effects draw. Glow/Streak/Smoke/Water/Drop/Sphere go to the shared effects renderer (NativeVfxDraw:
    /// world, sorted, soft; glow profiles 0 mote, 1 core, 2 ring, 3 snow); the wave bodies and the floor and surface marks are
    /// meshes the Core fills (NativeMagicFx.Bodies, NativeMagicFx.Decals). Colours are linear (HDR above 1).
    /// </summary>
    public interface IMagicFxSink
    {
        void Glow(Vector3 at,Vector3 color,float coverage,float size,int profile,float u,float seed);
        void Streak(Vector3 head,Vector3 velocity,Vector3 color,float width,float minLength,float blur,float taper,float u,float seed);
        /// <summary>A toon smoke puff (albedo linear, opacity, size m, normalized age u, seed, floor height).</summary>
        void Smoke(Vector3 at,Vector3 albedo,float opacity,float size,float u,float seed,float floor);
        /// <summary>A toon water puff stretched along segment: lit tone (linear) and its shadow tone (sRGB hex).</summary>
        void Water(Vector3 at,Vector3 lit,int shade,float opacity,float size,Vector3 segment,float u,float seed,float floor);
        /// <summary>A blended teardrop along segment (its head at head): water drops, ink threads.</summary>
        void Drop(Vector3 head,Vector3 segment,Vector3 color,float coverage,float width,float u,float seed);
        /// <summary>A smooth matte lit sphere (the black-hole core, no glint): albedo, an HDR fresnel rim and emission, linear.</summary>
        void Sphere(Vector3 at,float radius,Vector3 albedo,Vector3 rim,Vector3 emissive);
        /// <summary>A glow on the first-person view-model layer (the caster's own hand): profile 0 soft, 1 hot core, 2 ring, 3
        /// sparkle; coverage above 0 darkens.</summary>
        void View(Vector3 at,Vector3 color,float coverage,float size,int profile);
    }

    /// <summary>One vertex of a wave body or a mark (Runtime/NativeMagicFxPresentation uploads these as they are;
    /// Resources/NativeMagicFx.shader). Data and Extra by mode (Extra.y): 0 wave body: Data = across (m), along the profile
    /// (m), foam weight, thickness; Extra = flow phase (s), 0, near-camera fade start and length (m). 1 wet streak: Data =
    /// across (-1..1), along (m), wetness; 2 puddle and 3 void scorch: Data = disc coordinates (-1..1), wetness or
    /// intensity, age (s); 4 void crack: Data = across (-1..1), along (0..1), intensity. Extra.x: the mark's seed.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMagicVertex{public Vector3 Position,Normal;public Vector4 Color,Data,Extra;}

    /// <summary>The floor and surface marks of a frame (wet streaks, puddles, void scorches and cracks): quads in a reused,
    /// growing vertex array (four per quad, wound a b c d), drawn in one mesh before the transparent queue.</summary>
    public sealed class NativeMagicDecals
    {
        public NativeMagicVertex[] Vertices;public int Count;
        public int Quads=>Count/4;
        public NativeMagicDecals(int quads){Vertices=new NativeMagicVertex[Math.Max(4,quads*4)];}
        public void Clear(){Count=0;}
        /// <summary>A quad from its four corners, each with its Data, all with one normal, seed and mode.</summary>
        public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector4 da,Vector4 db,Vector4 dc,Vector4 dd,Vector3 normal,float seed,float mode)
        {
            if(Count+4>Vertices.Length)Array.Resize(ref Vertices,Vertices.Length*2);
            var extra=new Vector4(seed,mode,0,0);var color=Vector4.One;
            Vertices[Count++]=new NativeMagicVertex{Position=a,Normal=normal,Color=color,Data=da,Extra=extra};
            Vertices[Count++]=new NativeMagicVertex{Position=b,Normal=normal,Color=color,Data=db,Extra=extra};
            Vertices[Count++]=new NativeMagicVertex{Position=c,Normal=normal,Color=color,Data=dc,Extra=extra};
            Vertices[Count++]=new NativeMagicVertex{Position=d,Normal=normal,Color=color,Data=dd,Extra=extra};
        }
        /// <summary>A square mark of half size `half` on a surface (centre, normal), turned by `roll` about the normal; Data.xy
        /// the disc coordinates, z its intensity, w its age.</summary>
        public void Disc(Vector3 centre,Vector3 normal,float half,float roll,float intensity,float age,float seed,float mode)
        {
            NativeMagicMath.Basis(normal,out var t,out var b);float c=(float)Math.Cos(roll),s=(float)Math.Sin(roll);var x=(t*c+b*s)*half;var y=(b*c-t*s)*half;
            Quad(centre-x-y,centre+x-y,centre+x+y,centre-x+y,new Vector4(-1,-1,intensity,age),new Vector4(1,-1,intensity,age),new Vector4(1,1,intensity,age),new Vector4(-1,1,intensity,age),normal,seed,mode);
        }
    }

    /// <summary>Small deterministic helpers shared by the magic effects (no randomness: every value is a hash of an index).</summary>
    public static class NativeMagicMath
    {
        /// <summary>The floor of a puff that has none (NativeVfxDraw.NoFloor: no floor fade).</summary>
        public const float NoFloor=-1e9f;
        /// <summary>A hash of stream index k to 0..1 (salt picks the value), the same on every machine.</summary>
        public static float H(long k,uint salt){unchecked{uint h=(uint)k*0x9e3779b1u^(uint)(k>>32)*0x7feb352du^salt*0x85ebca6bu;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;h*=0x297a2d39u;h^=h>>15;return (h&0xffffff)/16777216f;}}
        public static float Clamp01(float x)=>x<0?0:x>1?1:x;
        public static float Smooth(float x){x=Clamp01(x);return x*x*(3-2*x);}
        public static float Lerp(float a,float b,float t)=>a+(b-a)*t;
        /// <summary>The horizontal unit direction of v, or fallback.</summary>
        public static Vector3 Flat(Vector3 v,Vector3 fallback){v.Y=0;return v.LengthSquared()>1e-6f?Vector3.Normalize(v):fallback;}
        /// <summary>Unity's right of a horizontal forward (up x forward).</summary>
        public static Vector3 Right(Vector3 f)=>new Vector3(f.Z,0,-f.X);
        /// <summary>Unity's forward for a yaw in degrees (Quaternion.Euler(0,yaw,0) * forward).</summary>
        public static Vector3 Forward(float yawDegrees){double a=yawDegrees*Math.PI/180;return new Vector3((float)Math.Sin(a),0,(float)Math.Cos(a));}
        public static Vector3 Lin(int hex,float gain=1){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Vector3((float)r,(float)g,(float)b)*gain;}
        /// <summary>Effects quality (Reduced .3) and the low graphics tier scale a full count: at least one.</summary>
        public static int Count(int full,float quality,bool low){float n=full*Math.Max(.3f,Math.Min(1,quality));if(low)n*=.55f;return Math.Max(1,(int)Math.Round(n));}
        /// <summary>Two unit vectors perpendicular to n and to each other.</summary>
        public static void Basis(Vector3 n,out Vector3 t,out Vector3 b)
        {
            n=n.LengthSquared()>1e-8f?Vector3.Normalize(n):Vector3.UnitY;var helper=Math.Abs(n.Y)<.9f?Vector3.UnitY:Vector3.UnitX;
            t=Vector3.Normalize(Vector3.Cross(helper,n));b=Vector3.Cross(n,t);
        }
    }

    /// <summary>
    /// The water and darkness magic presentation (owner 2026-10-12: "Water still uses a basic model for Q : Tidal Wave. Also
    /// darkness basic attack could use some revamping"; Claude's visuals over Astra's unchanged simulation). The Tidal Wave
    /// (NativeTidalWaveFx) replaces the source's translucent box, the void orb (NativeVoidOrbFx) the darkness primary's pale
    /// icosahedron, its energy trail and halo. Every effect is a pure function of what host, guests and the
    /// killcam replay all hold (the replicated shots and actors, the replicated events, the world's floors), on the
    /// presentation clock; nothing here reaches the authority, hit timing, hitboxes, damage, knockback or the network frame.
    /// The meshes are built once; a frame allocates nothing once its pools are warm.
    /// </summary>
    public sealed class NativeMagicFx
    {
        public readonly NativeTidalWaveFx Tide=new NativeTidalWaveFx();
        public readonly NativeVoidOrbFx Void=new NativeVoidOrbFx();
        public readonly NativeMagicDecals Decals=new NativeMagicDecals(256);
        private int cursor;private float clock=float.NegativeInfinity;
        /// <summary>The projectiles whose model these effects replace: the Tidal Wave ability shot and the darkness primary orb
        /// (not a hadouken or sword beam). The projectile visuals, the combat glow and the energy trails skip their model, halo
        /// and trail (the spill light and chip modules stay).</summary>
        public static bool Owns(in NativeShotView v)=>!v.Siren&&(v.Kind==NativeShotKind.Ability&&v.Ability=="tidal-wave"||v.Kind==NativeShotKind.Bullet&&v.Spec=="darkness"&&v.Arcade==0&&!v.SwordBeam);
        public void Clear(){Tide.Clear();Void.Clear();Decals.Clear();cursor=0;clock=float.NegativeInfinity;}
        /// <summary>The source spec of a seat among the actors (null when it has none).</summary>
        public static string Spec(IReadOnlyList<Actor> actors,int id){if(actors==null)return null;for(int i=0;i<actors.Count;i++){var a=actors[i];if(a!=null&&a.Id==id)return a.SourceSpec;}return null;}
        public static Actor Find(IReadOnlyList<Actor> actors,int id){if(actors==null)return null;for(int i=0;i<actors.Count;i++){var a=actors[i];if(a!=null&&a.Id==id)return a;}return null;}
        /// <summary>Reads the fresh replicated events (idempotent per frame). Ids restart with a new arena or the killcam's replica,
        /// and its clock rewinds: both start over.</summary>
        public void Observe(IReadOnlyList<ArenaEvent> events,float time,IReadOnlyList<Actor> actors)
        {
            if(events==null)return;int newest=0;for(int i=0;i<events.Count;i++)if(events[i].Id>newest)newest=events[i].Id;
            if(newest>0&&newest<cursor||time<clock-.25f){Clear();}
            clock=time;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=cursor||time-e.Time>2)continue;
                if(e.Kind==EventKind.Hit||e.Kind==EventKind.Headshot){string spec=Spec(actors,e.Source);Tide.Hit(e,spec);Void.Hit(e,spec);}
                else if(e.Kind==EventKind.Heal)Void.Heal(e);
            }
            if(newest>cursor)cursor=newest;
        }
        /// <summary>Draws every live magic effect of the frame at `time` (the presentation clock); simTime: the darkness pulse and
        /// wobble clock (NativeShotScene's state.time: the match clock on the host, the presentation clock on a replica);
        /// quality: effects quality (Reduced .3); low: the low graphics tier.</summary>
        public void Draw(IMagicFxSink sink,IMagicFxScene scene,float time,float simTime,float quality,bool low)
        {
            Decals.Clear();if(sink==null||scene==null)return;
            Tide.Draw(sink,scene,Decals,time,quality,low);Void.Draw(sink,scene,Decals,time,simTime,quality,low);
        }
    }
}
