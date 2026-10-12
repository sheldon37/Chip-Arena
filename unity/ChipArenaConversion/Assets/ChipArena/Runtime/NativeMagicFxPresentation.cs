using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws the magic effects' meshes (Core/NativeMagicFx): each wave body as its own draw of a reused dynamic mesh with its
    /// own bounds (queue Transparent-1: just before the transparent queue's effects batch, so the wave's own spray, foam and
    /// splashes draw over the water, while Unity still sorts it against the other draws by where it is), and every floor and
    /// surface mark in one dynamic mesh before every transparent draw (Transparent-50). Resources/NativeMagicFx.shader; the
    /// shared effects renderer's noise tile; soft particles where the camera renders a depth texture (not on the low graphics
    /// tier). Meshes are created once per surface and refilled in place: no per-frame allocation once warm.
    /// </summary>
    public sealed class NativeMagicFxDraw : IDisposable
    {
        private static readonly VertexAttributeDescriptor[] Layout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4)
        };
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private static readonly int NoiseId=Shader.PropertyToID("_MagicNoise"),SoftId=Shader.PropertyToID("_MagicSoft"),LowId=Shader.PropertyToID("_MagicLow");
        private readonly Material body,marks;private readonly Dictionary<NativeWaveSurface,Mesh> bodies=new Dictionary<NativeWaveSurface,Mesh>();
        private readonly Mesh decals;private int decalVertices=-1,decalIndices=-1;private int[] quadIndices=new int[0];private Texture noise;
        /// <summary>Draws issued at the last Submit (checks).</summary>
        public int BodyDraws{get;private set;}public int MarkQuads{get;private set;}
        private NativeMagicFxDraw(Shader shader)
        {
            body=new Material(shader){name="Native magic wave",renderQueue=(int)RenderQueue.Transparent-1,hideFlags=HideFlags.HideAndDontSave};body.SetOverrideTag("RenderType","Transparent");
            marks=new Material(shader){name="Native magic marks",renderQueue=(int)RenderQueue.Transparent-50,hideFlags=HideFlags.HideAndDontSave};marks.SetOverrideTag("RenderType","Transparent");
            decals=new Mesh{name="Native magic marks",hideFlags=HideFlags.HideAndDontSave};decals.MarkDynamic();
        }
        /// <summary>The drawer, or null when the shader is missing or unsupported (the projectiles then keep their source look).</summary>
        public static NativeMagicFxDraw Create(){var shader=Resources.Load<Shader>("NativeMagicFx");return shader!=null&&shader.isSupported?new NativeMagicFxDraw(shader):null;}
        public void Submit(NativeMagicFx fx,Camera camera,bool low,Texture noiseTile)
        {
            BodyDraws=0;MarkQuads=0;if(fx==null||camera==null)return;
            if(noiseTile!=null&&!ReferenceEquals(noise,noiseTile)){noise=noiseTile;body.SetTexture(NoiseId,noise);marks.SetTexture(NoiseId,noise);}
            float soft=!low&&(camera.depthTextureMode&DepthTextureMode.Depth)!=0?1:0;body.SetFloat(SoftId,soft);body.SetFloat(LowId,low?1:0);marks.SetFloat(LowId,low?1:0);
            var list=fx.Tide.Bodies;
            for(int i=0;i<list.Count;i++)
            {
                var s=list[i];if(!bodies.TryGetValue(s,out var mesh)){mesh=Body(s);bodies.Add(s,mesh);}
                mesh.SetVertexBufferData(s.Vertices,0,0,s.VertexCount,0,Quiet);
                var b=new Bounds();b.SetMinMax(new Vector3(s.Min.X,s.Min.Y,s.Min.Z),new Vector3(s.Max.X,s.Max.Y,s.Max.Z));
                mesh.SetSubMesh(0,new SubMeshDescriptor(0,s.Triangles.Length){bounds=b,firstVertex=0,vertexCount=s.VertexCount},Quiet);mesh.bounds=b;
                Graphics.DrawMesh(mesh,Matrix4x4.identity,body,0,camera,0,null,ShadowCastingMode.Off,false);BodyDraws++;
            }
            var d=fx.Decals;int count=d.Count;if(count==0)return;
            if(decalVertices<d.Vertices.Length){decalVertices=d.Vertices.Length;decals.SetVertexBufferParams(decalVertices,Layout);}
            int indices=count/4*6;
            if(decalIndices<indices)
            {
                int quads=Math.Max(64,d.Vertices.Length/4);quadIndices=new int[quads*6];for(int q=0;q<quads;q++){int i=q*6,v=q*4;quadIndices[i]=v;quadIndices[i+1]=v+1;quadIndices[i+2]=v+2;quadIndices[i+3]=v;quadIndices[i+4]=v+2;quadIndices[i+5]=v+3;}
                decalIndices=quadIndices.Length;decals.SetIndexBufferParams(decalIndices,IndexFormat.UInt32);decals.SetIndexBufferData(quadIndices,0,0,decalIndices,Quiet);decals.subMeshCount=1;
            }
            decals.SetVertexBufferData(d.Vertices,0,0,count,0,Quiet);
            decals.SetSubMesh(0,new SubMeshDescriptor(0,indices){bounds=Everywhere,firstVertex=0,vertexCount=count},Quiet);decals.bounds=Everywhere;
            Graphics.DrawMesh(decals,Matrix4x4.identity,marks,0,camera,0,null,ShadowCastingMode.Off,false);MarkQuads=count/4;
        }
        // One wave surface's mesh: its fixed triangles uploaded once.
        private static Mesh Body(NativeWaveSurface s)
        {
            var mesh=new Mesh{name="Native magic wave body",hideFlags=HideFlags.HideAndDontSave};mesh.MarkDynamic();
            mesh.SetVertexBufferParams(s.VertexCount,Layout);mesh.SetIndexBufferParams(s.Triangles.Length,IndexFormat.UInt16);
            var tris=new ushort[s.Triangles.Length];for(int i=0;i<tris.Length;i++)tris[i]=(ushort)s.Triangles[i];
            mesh.SetIndexBufferData(tris,0,0,tris.Length,Quiet);mesh.subMeshCount=1;return mesh;
        }
        public void Dispose()
        {
            foreach(var m in bodies.Values)CapturedAnimation.Release(m);bodies.Clear();CapturedAnimation.Release(decals);CapturedAnimation.Release(body);CapturedAnimation.Release(marks);
        }
    }

    // The water and darkness magic presentation's arena hook (Core/NativeMagicFx; owner 2026-10-12). RenderGlow calls MagicFx right
    // after the projectile models: the replicated Tidal Wave shots (at the shot tracker's presented positions), the snapshot's
    // actors, their posed rigs, the world's floors and walls under them, the replicated slow and the event buffer go to the
    // Core effects, whose particles go into the shared effects renderer and whose wave bodies and marks are drawn here. Host,
    // guests and the killcam replay draw the same frame. Effects quality (Reduced) and the low graphics tier thin them as the
    // other effects do. Presentation only: nothing here reaches the authority, hit timing, hitboxes, damage, knockback or the
    // network frame.
    public sealed partial class ArenaPresentation
    {
        private readonly NativeMagicFx magicFx=new NativeMagicFx();
        private NativeMagicFxDraw magicDraw;private MagicScene magicScene;private MagicSink magicSink;private NativeArena magicArena;private int magicGeneration=-1;private float magicClock=-1;
        /// <summary>Whether the magic effects draw this frame (the drawer exists): the projectile visuals and the combat glow skip
        /// the looks it replaces only then.</summary>
        private bool MagicLooks=>magicDraw!=null;
        private void CreateMagicFx(){magicDraw=NativeMagicFxDraw.Create();if(shots!=null)shots.MagicLooks=MagicLooks;if(combatGlow!=null)combatGlow.MagicLooks=MagicLooks;}
        /// <summary>RenderGlow, after the projectile models (on the field clock the shots are presented at).</summary>
        private void MagicFx(NativeArena arena,Camera camera,float now)
        {
            if(magicDraw==null||vfx==null||camera==null||arena==null)return;
            if(!ReferenceEquals(magicArena,arena)||magicGeneration!=arena.Generation||now<magicClock-.25f){magicFx.Clear();magicArena=arena;magicGeneration=arena.Generation;}
            magicClock=Math.Max(magicClock,now);
            magicScene??=new MagicScene(this);magicSink??=new MagicSink();magicScene.Bind(arena,kitSnapshot,now,camera);magicSink.Bind(vfx);
            magicFx.Observe(arena.Events,now,magicScene.Actors);
            magicFx.Draw(magicSink,magicScene,now,Mathf.Clamp01(BlastQuality),LowGraphics);
            magicDraw.Submit(magicFx,camera,LowGraphics,vfx.NoiseTexture);
        }
        private void ClearMagicFx(){magicFx.Clear();magicArena=null;magicGeneration=-1;magicClock=-1;}
        private void DisposeMagicFx(){ClearMagicFx();magicDraw?.Dispose();magicDraw=null;}

        // What the Core effects read of this presentation (allocation-free once bound).
        private sealed class MagicScene:IMagicFxScene
        {
            private readonly ArenaPresentation p;private NativeArena arena;private float time;private Camera camera;private PhysicsScene physics;private bool world;
            private readonly List<Actor> actors=new List<Actor>();private readonly List<NativeMagicShot> waves=new List<NativeMagicShot>();private readonly RaycastHit[] hits=new RaycastHit[16];
            public MagicScene(ArenaPresentation owner){p=owner;}
            public void Bind(NativeArena a,Snapshot s,float t,Camera c)
            {
                arena=a;time=t;camera=c;actors.Clear();if(s?.Actors!=null)actors.AddRange(s.Actors);world=NativeBlastPresentation.World(a,out physics);
                waves.Clear();
                foreach(var shot in a.AbilityShots)
                {
                    if(shot.Definition==null||shot.Definition.id!="tidal-wave")continue;NativeShotViews.Ability(a,shot,p.shotTracker,t,out var v);
                    waves.Add(new NativeMagicShot{Key=NativeShotScene.Key(v.Kind,v.Id),Id=v.Id,Owner=shot.Owner?.Seat?.id??0,Position=new NVector((float)v.X,(float)v.Y,(float)v.Z),Velocity=new NVector((float)v.VX,(float)v.VY,(float)v.VZ),Radius=(float)v.Radius});
                }
            }
            public IReadOnlyList<Actor> Actors=>actors;
            public int Waves=>waves.Count;
            public bool Wave(int i,out NativeMagicShot shot){shot=waves[i];return true;}
            public bool Feet(int id,out NVector feet)
            {
                if(p.visualPositions.TryGetValue(id,out var at)){feet=new NVector(at.x,at.y,at.z);return true;}
                foreach(var a in actors)if(a!=null&&a.Id==id){feet=new NVector(a.Position.x,a.Position.y,a.Position.z);return true;}
                feet=default;return false;
            }
            public bool Chest(int id,out NVector chest)
            {
                chest=default;
                if(p.FirstPerson&&id==p.LocalId&&camera!=null){var c=camera.transform.position-Vector3.up*.45f+camera.transform.forward*.1f;chest=new NVector(c.x,c.y,c.z);return true;}
                if(!p.rigs.TryGetValue(id,out var rig))return false;var b=rig.Bones[CapturedRig.Chest].position+Vector3.up*.12f;chest=new NVector(b.x,b.y,b.z);return true;
            }
            public int Viewer=>p.FirstPerson?p.LocalId:0;
            public NVector Eye{get{var e=camera!=null?camera.transform.position:Vector3.zero;return new NVector(e.x,e.y,e.z);}}
            public float Floor(NVector at)
            {
                if(!world)return float.NaN;double f=NativeBlastPresentation.FloorUnder(physics,new Vector3(at.X,at.Y,at.Z),hits);
                return f<=NativeBlastSim.NoFloor+1?float.NaN:(float)f;
            }
            public bool Surface(NVector from,NVector direction,float reach,out NVector point,out NVector normal)
            {
                point=normal=default;if(!world)return false;var d=new Vector3(direction.X,direction.Y,direction.Z);if(d.sqrMagnitude<1e-6f)return false;
                if(!physics.Raycast(new Vector3(from.X,from.Y,from.Z),d.normalized,out var hit,reach,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return false;
                point=new NVector(hit.point.x,hit.point.y,hit.point.z);normal=new NVector(hit.normal.x,hit.normal.y,hit.normal.z);return true;
            }
            public bool Slowed(int id,out float until,out bool shackled)
            {
                until=0;shackled=false;
                foreach(var pilot in arena.Pilots)if(pilot.Seat.id==id){if(!(pilot.SlowEnd>time&&pilot.SlowFactor<1))return false;until=pilot.SlowEnd;shackled=NativeStatusVisuals.Shackled(pilot.SlowFactor);return true;}
                return false;
            }
        }
        // The Core effects' particles onto the shared effects renderer.
        private sealed class MagicSink:IMagicFxSink
        {
            private NativeVfxDraw vfx;
            public void Bind(NativeVfxDraw v){vfx=v;}
            private static Vector3 U(NVector v)=>new Vector3(v.X,v.Y,v.Z);private static Color C(NVector c,float a=0)=>new Color(c.X,c.Y,c.Z,a);
            public void Glow(NVector at,NVector color,float coverage,float size,int profile,float u,float seed)=>vfx.Glow(U(at),C(color,coverage),size,profile,u,seed);
            public void Streak(NVector head,NVector velocity,NVector color,float width,float minLength,float blur,float taper,float u,float seed)=>vfx.Streak(U(head),U(velocity),C(color),width,minLength,blur,taper,u,seed);
            public void Smoke(NVector at,NVector albedo,float opacity,float size,float u,float seed,float floor)=>vfx.Smoke(U(at),C(albedo),opacity,size,u,seed,floor);
            public void Water(NVector at,NVector lit,int shade,float opacity,float size,NVector segment,float u,float seed,float floor)=>vfx.Water(U(at),C(lit),shade,opacity,size,U(segment),u,seed,floor);
            public void Drop(NVector head,NVector segment,NVector color,float coverage,float width,float u,float seed)=>vfx.Drop(U(head),U(segment),C(color,coverage),width,u,seed);
        }
    }
}
