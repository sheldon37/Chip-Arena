using System;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The shared effects renderer of the 2026-10-11 effects overhaul (owner: "The effects seem a bit outdated and there still
    /// seems to be cube placeholders for certain impact effects"). The blast pools (NativeBlastSim: blasts, impacts, field
    /// emitters and every other NativeImpactCues draw), the burst sparks (NativeBurstPool), the field cores
    /// (NativeFieldPresentation), the melee hit sparks and slam dust (NativeMeleeFlashPresentation) and the impact contact
    /// flashes (NativeCombatGlow) hand their particles here each frame; each blast particle is drawn as the primitive its
    /// Core <see cref="NativeFxRole"/> names instead of one flat cube:
    ///  - Mote, Line points, Bolt points: a soft HDR glow sprite; Core: a larger, softer one; Bubble: a ring; Snow: a pale one.
    ///  - Spark, Ember, Streak and the burst sparks: a streak along the velocity (a capsule with a hot core), length from the
    ///    speed with a minimum; Bolt and Line seed neighbours within one strand (NativeBlastPool.Linked) are also joined by an
    ///    HDR segment.
    ///  - Smoke, Cloud: a lit atlas smoke puff that erodes over its life (the raw size, not the cube's end shrink), its
    ///    albedo lifted to a minimum luminance so soot still reads on the navy floor; saturated clouds (toxic, acid) glow a
    ///    little in their own colour. Fireball, Flame: an emissive atlas puff in its own hue (the Core glows are the white-hot
    ///    centre), drawn from its start size and billowing as it erodes, handing off into soot. Dark (Void): an opaque black
    ///    orb with an HDR fresnel rim in its hue, like the black hole's core.
    ///  - Debris, Rock: a lit irregular rubble chunk (NativeVfxMeshes.Chunks by seed); HotDebris (fire-family): the same,
    ///    glowing hot out of the fireball and cooling; Shard: a lit crystal that settles into a seeded resting pose on the floor.
    ///  - Shell: not drawn per voxel; each front is one expanding fresnel shell from NativeBlastSim.Shocks (front faces only
    ///    while the camera is outside it); Quake's front is not drawn (its slabs and dust show the radius).
    /// Three draws a frame, each one dynamic mesh refilled from reused arrays (no per-frame allocation): every sprite, streak
    /// and puff in one premultiplied mesh sorted back to front (Resources/NativeVfx.shader, queue Transparent with bounds
    /// round what it holds, so Unity sorts the batch among the field domes, the tidal wave and the dust by where it is), the
    /// lit meshes (NativeVfxLit.shader, opaque) and the shock shells (NativeVfxLit.shader, additive, Transparent+4).
    /// Graphics.DrawMesh to the given camera, layer 0, no shadows. Soft particles: the puffs and glows fade where they meet the
    /// scene's depth (the camera's depth texture, which the shadowed key light already makes Unity render in forward); not on
    /// the low graphics tier (<see cref="Low"/>: no shadows there, so no depth pass is added; the puffs are also drawn at .8
    /// size and the shells skip their noise tap; the GPU cost of either tier is not measured). Known limit (M1): draws in later
    /// transparent queues (NativeCombatGlow's halos at +10, the beacon, repair and kill effects) are not dimmed by smoke in
    /// front of them. The smoke atlas and noise tile (Core NativeVfxTextures) are built once per process on a worker thread
    /// and uploaded when ready; until then the puffs draw a round stand-in. Everything here is cosmetic and a pure function of
    /// the particles handed in (seeded in Core, aged on the presentation clock), so host, guests and the killcam draw the same
    /// frame; nothing reads back into the match.
    /// </summary>
    public sealed class NativeVfxDraw : IDisposable
    {
        /// <summary>Billboard modes (NativeVfx.shader TEXCOORD1.z): glow profiles 0-3, then streak, smoke, fireball.</summary>
        public const float Mote=0,CoreGlow=1,Ring=2,Snow=3,StreakMode=4,SmokeMode=5,FireballMode=6;
        /// <summary>The floor of a puff that has none (no floor fade).</summary>
        public const float NoFloor=-1e9f;
        // ---- the look: sprite size per voxel edge and HDR gain per role (stylized, punchy; the bloom takes what passes 1) ----
        private const float MoteSize=2.6f,MoteGain=1.7f,CoreSize=2.4f,CoreGain=1.3f,RingSize=2.6f,RingGain=1.4f,SnowSize=2.4f,SnowGain=.9f,SnowCover=.3f;
        private const float SparkWidth=1.3f,SparkGain=3.2f,SparkBlur=.045f,EmberWidth=1.6f,EmberGain=2.6f,EmberBlur=.03f,StreakWidth=1.5f,StreakGain=2.2f,StreakBlur=.05f;
        private const float BoltGain=3.6f,LineGain=2.6f,PointGlow=.6f,PointSize=2.2f;
        // Smoke albedo is lifted (hue kept) to at least SmokeLift luminance, by at most SmokeLiftMax: the palette's soot
        // (Kinetic 0x3b4650, Fire's near-black end) otherwise vanished into the navy floor (review 2026-10-11).
        private const float SmokeSize=2.6f,SmokeOpacity=.85f,SmokeAlbedo=1.25f,SmokeLift=.12f,SmokeLiftMax=4f,CloudSize=2.6f,CloudOpacity=.62f,CloudGlow=.35f;
        private const float DarkRim=3f;
        private const float ChunkSelf=.3f,DebrisScale=1.45f,RockScale=1.3f,HotLife=.35f,HotGain=2.2f;
        private static readonly Color HotColor=new Color(1,.42f,.12f,0);
        // Flames burn in their own hue (FlameTint) and age slower in the puff (FlameAge), so a tongue stays hot for most of its
        // short life instead of handing off into soot at a third of it (the fireball formula alone left fire fields dull brown).
        private const float FireballSize=2.4f,FireballBirth=.85f,FireballBillow=.5f,FlameSize=2.1f,FlameAspect=1.5f,FlameTint=1.1f,FlameAge=.7f;
        private const float CrystalAlbedo=.55f,CrystalSpec=1.1f,CrystalGloss=.92f,CrystalGlow=.5f,CrystalRim=2.6f;
        private const float BurstGain=2.4f,BurstBlur=.05f;
        private const float BlastShock=1.2f,ImpactShock=.5f,NovaShock=1.9f;
        // Core glows sort this many of their sizes nearer the camera, so the white-hot centre draws over the fireball puffs.
        private const float CoreLead=.75f;
        // The low graphics tier draws the puffs at this size.
        private const float LowPuff=.8f;
        private const int NoiseSize=64;

        [StructLayout(LayoutKind.Sequential)]
        private struct BillboardVertex{public Vector3 Centre;public Color Color;public Vector4 Shape,Life,Axis;}
        [StructLayout(LayoutKind.Sequential)]
        private struct MeshVertex{public Vector3 Position,Normal;public Color Color;public Vector4 A,B;}
        private struct Queued{public Vector3 Centre;public Color Color;public float Size,Roll,U,Seed,Mode,Floor;public Vector4 Axis;}
        private static readonly VertexAttributeDescriptor[] BillboardLayout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord2,VertexAttributeFormat.Float32,4)
        };
        private static readonly VertexAttributeDescriptor[] MeshLayout=
        {
            new VertexAttributeDescriptor(VertexAttribute.Position,VertexAttributeFormat.Float32,3),new VertexAttributeDescriptor(VertexAttribute.Normal,VertexAttributeFormat.Float32,3),
            new VertexAttributeDescriptor(VertexAttribute.Color,VertexAttributeFormat.Float32,4),new VertexAttributeDescriptor(VertexAttribute.TexCoord0,VertexAttributeFormat.Float32,4),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord1,VertexAttributeFormat.Float32,4)
        };
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private static readonly int SoftId=Shader.PropertyToID("_VfxSoft"),LowId=Shader.PropertyToID("_VfxLow");
        private const MeshUpdateFlags Quiet=MeshUpdateFlags.DontValidateIndices|MeshUpdateFlags.DontRecalculateBounds|MeshUpdateFlags.DontNotifyMeshUsers|MeshUpdateFlags.DontResetBoneBounds;

        // One dynamic mesh: vertex and index arrays that only grow, uploaded with the advanced Mesh API. Quad batches keep a
        // fixed index pattern (uploaded only when it grows); the others upload this frame's indices.
        private sealed class Batch<T> where T:struct
        {
            public readonly Mesh Mesh;public T[] Vertices;public int[] Indices;public int VertexCount,IndexCount;
            private readonly VertexAttributeDescriptor[] layout;private readonly bool quads;private int vertexCapacity=-1,indexCapacity=-1;private bool indicesDirty=true;
            public Batch(string name,VertexAttributeDescriptor[] layout,int vertices,int indices,bool quads)
            {
                Mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};Mesh.MarkDynamic();this.layout=layout;this.quads=quads;
                Vertices=new T[vertices];Indices=new int[quads?vertices/4*6:indices];if(quads)Quads(0);
            }
            public void Clear(){VertexCount=0;IndexCount=0;}
            // Room for more vertices and indices: the first new vertex, and (out) the first new index.
            public int Add(int vertices,int indices,out int indexStart)
            {
                int need=VertexCount+vertices;
                if(need>Vertices.Length){Array.Resize(ref Vertices,Math.Max(need,Vertices.Length*2));if(quads){int old=Indices.Length;Array.Resize(ref Indices,Vertices.Length/4*6);Quads(old/6);}}
                if(!quads&&IndexCount+indices>Indices.Length)Array.Resize(ref Indices,Math.Max(IndexCount+indices,Indices.Length*2));
                int first=VertexCount;VertexCount=need;indexStart=IndexCount;IndexCount+=indices;return first;
            }
            private void Quads(int from){for(int q=from;q<Indices.Length/6;q++){int i=q*6,v=q*4;Indices[i]=v;Indices[i+1]=v+1;Indices[i+2]=v+2;Indices[i+3]=v;Indices[i+4]=v+2;Indices[i+5]=v+3;}indicesDirty=true;}
            public void Draw(Material material,Camera camera)=>Draw(material,camera,Everywhere);
            public void Draw(Material material,Camera camera,Bounds bounds)
            {
                if(VertexCount==0||IndexCount==0||material==null||camera==null)return;
                if(vertexCapacity!=Vertices.Length){Mesh.SetVertexBufferParams(Vertices.Length,layout);vertexCapacity=Vertices.Length;}
                if(indexCapacity!=Indices.Length){Mesh.SetIndexBufferParams(Indices.Length,IndexFormat.UInt32);indexCapacity=Indices.Length;indicesDirty=true;Mesh.subMeshCount=1;}
                Mesh.SetVertexBufferData(Vertices,0,0,VertexCount,0,Quiet);
                if(!quads)Mesh.SetIndexBufferData(Indices,0,0,IndexCount,Quiet);else if(indicesDirty){Mesh.SetIndexBufferData(Indices,0,0,Indices.Length,Quiet);indicesDirty=false;}
                Mesh.SetSubMesh(0,new SubMeshDescriptor(0,IndexCount){bounds=bounds,firstVertex=0,vertexCount=VertexCount},Quiet);Mesh.bounds=bounds;
                Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);
            }
        }
        // A Core mesh in floats.
        private sealed class Shape
        {
            public readonly Vector3[] P,N;public readonly float[] E;public readonly int[] T,Parts;
            public Shape(NativeVfxMesh m)
            {
                int n=m.VertexCount;P=new Vector3[n];N=new Vector3[n];E=new float[n];Parts=(int[])m.Parts.Clone();T=(int[])m.Triangles.Clone();
                for(int i=0;i<n;i++){P[i]=new Vector3((float)m.Positions[i*3],(float)m.Positions[i*3+1],(float)m.Positions[i*3+2]);N[i]=new Vector3((float)m.Normals[i*3],(float)m.Normals[i*3+1],(float)m.Normals[i*3+2]);E[i]=(float)m.Emissive[i];}
            }
        }
        private static readonly Shape[] chunks=Array.ConvertAll(NativeVfxMeshes.Chunks,m=>new Shape(m));
        // The shock shells use one more subdivision than Core's Shell (642 vertices): a front can fill the screen, and its rim
        // should read as a smooth circle, not a polygon. The dome is its upper half, open exactly on its floor.
        private static readonly NativeVfxMesh round=NativeVfxMeshes.Icosphere(3,"shock shell");
        private static readonly Shape shard=new Shape(NativeVfxMeshes.Shard),bomb=new Shape(NativeVfxMeshes.Bomb),shell=new Shape(round),dome=new Shape(NativeVfxMeshes.Hemisphere(round,"shock dome")),orb=new Shape(NativeVfxMeshes.Shell);

        // ---- the textures: built once per process off the main thread (0 idle, 1 building, 2 ready, 3 failed) ----
        private static byte[] atlasBytes,noiseBytes;private static int built;
        private static void StartBuild()
        {
            if(Interlocked.CompareExchange(ref built,1,0)!=0)return;
            // A platform without a thread pool builds here once (at startup, where this renderer is created).
            try{if(!ThreadPool.QueueUserWorkItem(BuildTextures))BuildTextures(null);}catch(NotSupportedException){BuildTextures(null);}
        }
        private static void BuildTextures(object _)
        {
            try{var noise=NativeVfxTextures.NoiseTile(NoiseSize);var atlas=NativeVfxTextures.SmokeAtlas();Volatile.Write(ref noiseBytes,noise);Volatile.Write(ref atlasBytes,atlas);Volatile.Write(ref built,2);}
            catch(Exception){Volatile.Write(ref built,3);}
        }
        private static readonly int AtlasId=Shader.PropertyToID("_VfxAtlas"),ReadyId=Shader.PropertyToID("_VfxAtlasReady"),NoiseId=Shader.PropertyToID("_VfxNoise");

        private readonly Material billboardMaterial,litMaterial,shockMaterial;
        private readonly Batch<BillboardVertex> billboards=new Batch<BillboardVertex>("Native vfx billboards",BillboardLayout,4096,0,true);
        private readonly Batch<MeshVertex> lit=new Batch<MeshVertex>("Native vfx lit",MeshLayout,8192,8192,false),shocks=new Batch<MeshVertex>("Native vfx shocks",MeshLayout,2048,4096,false);
        private Texture2D atlas,noise;
        private Queued[] puffs=new Queued[1024];private float[] keys=new float[1024];private int[] order=new int[1024];private int puffCount;
        private int[] linkSeed=new int[128],linkIndex=new int[128];private int links;
        /// <summary>Sprites, streaks and puffs queued this frame (checks).</summary>
        public int Billboards=>puffCount;
        /// <summary>Lit mesh and shock shell vertices queued this frame (checks).</summary>
        public int LitVertices=>lit.VertexCount;public int ShockVertices=>shocks.VertexCount;
        /// <summary>The smoke atlas and noise tile are uploaded (until then the puffs draw their round stand-in).</summary>
        public bool AtlasReady=>atlas!=null;
        /// <summary>The low graphics tier (ArenaPresentation.LowGraphics): no soft particles (no depth texture is asked for),
        /// puffs at .8 size, no noise tap on the shells.</summary>
        public bool Low;
        /// <summary>The billboards' bounds at the last Submit (checks).</summary>
        public Bounds BillboardBounds{get;private set;}
        // The camera this renderer asked for a depth texture (soft particles), so it can take the request back.
        private Camera depthCamera;

        public NativeVfxDraw()
        {
            var vfx=Resources.Load<Shader>("NativeVfx");if(vfx==null)throw new InvalidOperationException("NativeVfx shader is missing");
            var lighting=Resources.Load<Shader>("NativeVfxLit");if(lighting==null)throw new InvalidOperationException("NativeVfxLit shader is missing");
            // In the Transparent queue with the field domes, the Siren warnings, the tidal wave and the dust; the batch's bounds
            // (Submit) put it in Unity's back-to-front order among them. Before the combat glow halos (+10).
            billboardMaterial=new Material(vfx){name="Native vfx billboards",renderQueue=(int)RenderQueue.Transparent,hideFlags=HideFlags.HideAndDontSave};
            litMaterial=new Material(lighting){name="Native vfx lit",renderQueue=(int)RenderQueue.Geometry,hideFlags=HideFlags.HideAndDontSave};
            litMaterial.SetFloat("_Src",(float)BlendMode.One);litMaterial.SetFloat("_Dst",(float)BlendMode.Zero);litMaterial.SetFloat("_ZWrite",1);litMaterial.SetFloat("_Cull",(float)CullMode.Back);litMaterial.SetFloat("_Shock",0);
            // The shells draw over the puffs inside them (both faces: a rim from outside, the surrounding shell from inside).
            shockMaterial=new Material(lighting){name="Native vfx shocks",renderQueue=(int)RenderQueue.Transparent+4,hideFlags=HideFlags.HideAndDontSave};shockMaterial.SetOverrideTag("RenderType","Transparent");
            shockMaterial.SetFloat("_Src",(float)BlendMode.One);shockMaterial.SetFloat("_Dst",(float)BlendMode.One);shockMaterial.SetFloat("_ZWrite",0);shockMaterial.SetFloat("_Cull",(float)CullMode.Off);shockMaterial.SetFloat("_Shock",1);
            StartBuild();
        }
        /// <summary>Starts a frame: nothing queued.</summary>
        public void Begin(){puffCount=0;links=0;lit.Clear();shocks.Clear();billboards.Clear();}
        /// <summary>Drops everything queued (rewinds, resets).</summary>
        public void Clear()=>Begin();

        // ---- queueing ----
        private void Add(Vector3 centre,Color color,float size,float roll,float u,float seed,float mode,float floor,Vector4 axis)
        {
            if(puffCount==puffs.Length){int grow=puffs.Length*2;Array.Resize(ref puffs,grow);Array.Resize(ref keys,grow);Array.Resize(ref order,grow);}
            puffs[puffCount++]=new Queued{Centre=centre,Color=color,Size=size,Roll=roll,U=u,Seed=seed,Mode=mode,Floor=floor,Axis=axis};
        }
        /// <summary>A soft HDR glow sprite (profile: <see cref="Mote"/>, <see cref="CoreGlow"/>, <see cref="Ring"/> or
        /// <see cref="Snow"/>). color: linear, may exceed 1; its alpha is coverage (0: purely additive).</summary>
        public void Glow(Vector3 at,Color color,float size,float profile,float u=0,float seed=0)=>Add(at,color,size,0,u,seed,profile,NoFloor,new Vector4(0,0,0,1));
        /// <summary>A streak whose head is at <paramref name="head"/>, trailing along -velocity by velocity x blur (at least
        /// minLength). A still particle is a glow sprite instead. taper: how much dimmer the tail is (0..1).</summary>
        public void Streak(Vector3 head,Vector3 velocity,Color color,float width,float minLength,float blur,float taper,float u=0,float seed=0)
        {
            Vector3 axis=velocity*blur;float len=axis.magnitude;
            if(len<1e-4f){Glow(head,color,width*PointSize,Mote,u,seed);return;}
            if(len<minLength)axis*=minLength/len;
            Add(head-axis*.5f,color,width,0,u,seed,StreakMode,NoFloor,new Vector4(axis.x,axis.y,axis.z,taper));
        }
        // A puff's roll: a seeded tilt (small, so the atlas's baked light stays on top) drifting slowly over its life.
        private static float Roll(float seed,float u){uint h=(uint)seed*2654435761u;h^=h>>15;h*=0x2c1b3c6du;h^=h>>12;return ((h&0xffff)/65536f-.5f)*.9f+u*((h&0x10000)!=0?.35f:-.35f);}
        // glow: a smoke puff's own-colour emission (NativeVfx.shader mode 5, TEXCOORD2.x).
        private void Puff(Vector3 at,Color albedo,float opacity,float size,float mode,float u,float seed,float floor,float aspect=1,bool upright=false,float glow=0)
        {albedo.a=opacity;Add(at,albedo,size*(Low?LowPuff:1),upright?0:Roll(seed,u),u,seed,mode,floor,new Vector4(glow,0,0,aspect));}
        /// <summary>A lit, eroding smoke puff (the blast smoke's look) for other effects: albedo linear, opacity 0..1, size the
        /// quad's width (m), u its normalized age, seed its atlas tile and roll, floor the height it fades out below
        /// (<see cref="NoFloor"/> for none).</summary>
        public void Smoke(Vector3 at,Color albedo,float opacity,float size,float u,float seed,float floor)=>Puff(at,albedo,opacity,size,SmokeMode,u,seed,floor);
        // Smoke albedo lifted to at least SmokeLift luminance (hue kept, at most x SmokeLiftMax).
        private static Color Lift(Color c){float lum=.2126f*c.r+.7152f*c.g+.0722f*c.b;return c*Mathf.Clamp(SmokeLift/Mathf.Max(lum,1e-3f),1,SmokeLiftMax);}
        // A cloud's own glow: CloudGlow for a saturated colour (toxic gas, acid fumes; sRGB saturation above about .5), none
        // for a grey one (storm clouds), ramped so a colour drifting across the edge never pops.
        private static float CloudGlowOf(Color c)
        {
            float r=(float)Math.Pow(Math.Max(c.r,0),1/2.2),g=(float)Math.Pow(Math.Max(c.g,0),1/2.2),b=(float)Math.Pow(Math.Max(c.b,0),1/2.2),max=Math.Max(r,Math.Max(g,b)),min=Math.Min(r,Math.Min(g,b));
            float sat=max>1e-4f?(max-min)/max:0;return CloudGlow*Mathf.Clamp01((sat-.45f)/.1f);
        }

        /// <summary>A blast sim's frame: both pools' draw lists and its shock fronts.</summary>
        public void Blasts(NativeBlastSim sim){if(sim==null)return;Pool(sim.Solid);Pool(sim.Glow);Shocks(sim);}
        /// <summary>One pool's draw list (NativeBlastPool.Output after Update), each particle as its role's primitive.</summary>
        public void Pool(NativeBlastPool pool)
        {
            var o=pool.Output;int n=Math.Min(pool.Drawn,o.Length/NativeBlastPool.Stride);links=0;
            for(int i=0;i<n;i++)
            {
                int w=i*NativeBlastPool.Stride;var role=(NativeFxRole)(int)(o[w+NativeBlastPool.RoleAt]+.5f);
                var at=new Vector3(o[w],o[w+1],o[w+2]);var color=new Color(o[w+10],o[w+11],o[w+12],0);
                float edge=o[w+3],u=o[w+NativeBlastPool.AgeAt],seed=o[w+NativeBlastPool.SeedAt],raw=o[w+NativeBlastPool.RawSizeAt],floor=o[w+NativeBlastPool.FloorAt];
                int v=w+NativeBlastPool.VelocityAt;var velocity=new Vector3(o[v],o[v+1],o[v+2]);
                switch(role)
                {
                    // Not drawn voxel by voxel: NativeBlastSim.Shocks draws each front as one shell.
                    case NativeFxRole.Shell:break;
                    case NativeFxRole.Mote:Glow(at,color*MoteGain,edge*MoteSize,Mote,u,seed);break;
                    case NativeFxRole.Core:Glow(at,color*CoreGain,edge*CoreSize,CoreGlow,u,seed);break;
                    case NativeFxRole.Bubble:Glow(at,color*RingGain,edge*RingSize,Ring,u,seed);break;
                    case NativeFxRole.Snow:{var c=color*SnowGain;c.a=SnowCover;Glow(at,c,edge*SnowSize,Snow,u,seed);break;}
                    // The glow pool's stretched edge (Output 4) is the source's streak length; motion blur may lengthen it.
                    case NativeFxRole.Spark:Streak(at,velocity,color*SparkGain,edge*SparkWidth,o[w+4],SparkBlur,.85f,u,seed);break;
                    case NativeFxRole.Ember:Streak(at,velocity,color*EmberGain,edge*EmberWidth,edge*2,EmberBlur,.9f,u,seed);break;
                    case NativeFxRole.Streak:Streak(at,velocity,color*StreakGain,edge*StreakWidth,o[w+4],StreakBlur,.8f,u,seed);break;
                    case NativeFxRole.Bolt:case NativeFxRole.Line:
                    {
                        Link(i,(int)seed);Glow(at,color*((role==NativeFxRole.Bolt?BoltGain:LineGain)*PointGlow),edge*PointSize,Mote,u,seed);break;
                    }
                    case NativeFxRole.Smoke:Puff(at,Lift(color)*SmokeAlbedo,SmokeOpacity,raw*SmokeSize,SmokeMode,u,seed,floor);break;
                    case NativeFxRole.Cloud:Puff(at,Lift(color)*SmokeAlbedo,CloudOpacity,raw*CloudSize,SmokeMode,u,seed,floor,glow:CloudGlowOf(color));break;
                    // The edge carries the opaque pool's end shrink, so the orb goes small instead of popping out.
                    case NativeFxRole.Dark:DarkCore(at,edge,color);break;
                    // From the start size (size0), billowing as it erodes, not the source's shrink to size1 (review 2026-10-11:
                    // the puffs shrank to 12% and the fireball broke apart).
                    case NativeFxRole.Fireball:Puff(at,color,1,o[w+NativeBlastPool.StartSizeAt]*FireballSize*(FireballBirth+FireballBillow*u),FireballMode,u,seed,floor);break;
                    case NativeFxRole.Flame:Puff(at,color*FlameTint,1,edge*FlameSize,FireballMode,u*FlameAge,seed,floor,FlameAspect,true);break;
                    case NativeFxRole.Debris:case NativeFxRole.Rock:case NativeFxRole.HotDebris:Chunk(o,w,(int)seed,u,color,role);break;
                    case NativeFxRole.Shard:Crystal(o,w,(int)seed,color);break;
                    default:Glow(at,color*MoteGain,edge*MoteSize,Mote,u,seed);break;
                }
            }
            Segments(o);
        }
        // Bolt and Line points join their neighbours within their strand (NativeBlastPool.Linked: seed neighbours, a Bolt may
        // skip one, its side spark; close by).
        private void Link(int index,int seed)
        {
            if(links==linkSeed.Length){Array.Resize(ref linkSeed,links*2);Array.Resize(ref linkIndex,links*2);}
            linkSeed[links]=seed;linkIndex[links]=index;links++;
        }
        private void Segments(float[] o)
        {
            if(links<2)return;
            Array.Sort(linkSeed,linkIndex,0,links);
            for(int k=0;k+1<links;k++)
            {
                int a=linkIndex[k]*NativeBlastPool.Stride,b=linkIndex[k+1]*NativeBlastPool.Stride;
                if(!NativeBlastPool.Linked(o,a,b))continue;bool bolt=(int)(o[a+NativeBlastPool.RoleAt]+.5f)==(int)NativeFxRole.Bolt;
                var pa=new Vector3(o[a],o[a+1],o[a+2]);var pb=new Vector3(o[b],o[b+1],o[b+2]);Vector3 d=pb-pa;
                var color=new Color(o[a+10]+o[b+10],o[a+11]+o[b+11],o[a+12]+o[b+12],0)*(.5f*(bolt?BoltGain:LineGain));
                float width=Mathf.Min(o[a+3],o[b+3])*(bolt?1.1f:.9f),u=(o[a+NativeBlastPool.AgeAt]+o[b+NativeBlastPool.AgeAt])*.5f;
                Add((pa+pb)*.5f,color,width,0,u,linkSeed[k],StreakMode,NoFloor,new Vector4(d.x,d.y,d.z,0));
            }
        }
        // A lit mesh at a voxel's transform: Output position, edge lengths and rotation (normals by the inverse scale).
        private void LitMesh(Shape s,Vector3 at,Quaternion q,Vector3 scale,Color albedo,float spec,float gloss,Color emissive,Color rim)
        {
            int first=lit.Add(s.P.Length,s.T.Length,out int ti);var verts=lit.Vertices;var tris=lit.Indices;
            var inv=new Vector3(1/Mathf.Max(scale.x,1e-4f),1/Mathf.Max(scale.y,1e-4f),1/Mathf.Max(scale.z,1e-4f));
            var color=new Color(albedo.r,albedo.g,albedo.b,spec);var a=new Vector4(emissive.r,emissive.g,emissive.b,gloss);var b=new Vector4(rim.r,rim.g,rim.b,0);
            for(int k=0;k<s.P.Length;k++)verts[first+k]=new MeshVertex{Position=at+q*Vector3.Scale(s.P[k],scale),Normal=(q*Vector3.Scale(s.N[k],inv)).normalized,Color=color,A=a,B=b};
            for(int k=0;k<s.T.Length;k++)tris[ti+k]=first+s.T[k];
        }
        private static Vector3 At(float[] o,int w)=>new Vector3(o[w],o[w+1],o[w+2]);
        private static Vector3 Edges(float[] o,int w)=>new Vector3(o[w+3],o[w+4],o[w+5]);
        private static Quaternion Turn(float[] o,int w)=>new Quaternion(o[w+6],o[w+7],o[w+8],o[w+9]);
        // Rubble: flat-shaded chunks by seed, lit, scaled up so a chunk covers about what the cube did (a hull is a third to a
        // half of its box). Fire-family debris (HotDebris) leaves the fireball glowing orange-hot and cools over its first
        // HotLife of life; other bright (impact) pieces glow and cool by their colour; a little of its own colour as emission
        // keeps the stylized rubble readable against a night arena.
        private void Chunk(float[] o,int w,int seed,float u,Color color,NativeFxRole role)
        {
            bool rock=role==NativeFxRole.Rock;var shape=chunks[(seed%NativeVfxMeshes.ChunkVariants+NativeVfxMeshes.ChunkVariants)%NativeVfxMeshes.ChunkVariants];Color emissive;
            if(role==NativeFxRole.HotDebris){float hot=Mathf.Clamp01(1-u/HotLife);emissive=HotColor*(HotGain*hot*hot)+color*ChunkSelf;}
            else{float lum=.2126f*color.r+.7152f*color.g+.0722f*color.b,hot=Mathf.Clamp01((lum-.3f)*2.2f)*(1-u)*1.2f;emissive=color*(hot+ChunkSelf);}
            LitMesh(shape,At(o,w),Turn(o,w),Edges(o,w)*(rock?RockScale:DebrisScale),color*(rock?.9f:.85f),rock?.1f:.15f,rock?.2f:.25f,emissive,color*.6f+new Color(.04f,.04f,.05f,0));
        }
        // Ice: a crystal along the voxel's long axis, a bright HDR fresnel edge in its tint (the bloom catches it), a glint and
        // some emission. A landed crystal bounces every frame (the pool's floor bounce flips its velocity, so its velocity pose
        // swings), so near its floor it settles into a resting pose: lying on its side (+Y along +X), turned about +Y by its
        // seed (the golden angle), blended in over the last half edge of height.
        private void Crystal(float[] o,int w,int seed,Color color)
        {
            var q=Turn(o,w);var e=Edges(o,w);float height=o[w+1]-o[w+NativeBlastPool.FloorAt],rest=Mathf.Clamp01(1-(height-.03f)/(.5f*Mathf.Max(e.x,1e-4f)));
            if(rest>0){var lie=Quaternion.AngleAxis(((long)seed*137508L%360000L)*.001f,Vector3.up)*Quaternion.AngleAxis(-90,Vector3.forward);q=Quaternion.Slerp(q,lie,rest);}
            LitMesh(shard,At(o,w),q,e,color*CrystalAlbedo,CrystalSpec,CrystalGloss,color*CrystalGlow,color*CrystalRim);
        }
        // Void's light-eating core: an opaque black orb (radius the particle's edge) with an HDR fresnel rim in its own hue,
        // the black hole's core look (NativeFieldPresentation).
        private void DarkCore(Vector3 at,float radius,Color color)
        {
            if(radius<1e-3f)return;float max=Mathf.Max(color.r,Mathf.Max(color.g,color.b));var hue=max>1e-5f?color*(1/max):new Color(1,1,1,0);hue.a=0;
            Sphere(at,radius,new Color(.004f,.002f,.008f,0),hue*DarkRim,Color.clear);
        }

        /// <summary>The shock fronts: one fresnel shell (a dome on the floor or a sphere) per record, on the sim's clock.</summary>
        public void Shocks(NativeBlastSim sim)
        {
            var list=sim.Shocks;double t=sim.Time;
            for(int i=0;i<list.Count;i++)
            {
                // Quake's front is not drawn: its heaved slabs (.25-.95 R) and its dust already show the radius.
                var s=list[i];if(s.Style==NativeBlastStyle.Quake)continue;double fade=s.Fade(t);float r=(float)s.RadiusAt(t);if(!(fade>0)||r<1e-3f)continue;
                float gain=(s.Kind==NativeShockKind.Nova?NovaShock:s.Kind==NativeShockKind.Impact?ImpactShock:BlastShock)*(float)fade;
                var hot=new Color((float)s.HotR*gain,(float)s.HotG*gain,(float)s.HotB*gain,(float)s.Progress(t));var cool=new Vector4((float)s.CoolR*gain,(float)s.CoolG*gain,(float)s.CoolB*gain,(float)s.U(t));
                var shape=s.Dome?dome:shell;var centre=new Vector3((float)s.X,(float)s.Y,(float)s.Z);float seed=s.Seed,isDome=s.Dome?1:0;
                int first=shocks.Add(shape.P.Length,shape.T.Length,out int ti);var verts=shocks.Vertices;var tris=shocks.Indices;
                // B.w: the radius, so the shader finds the centre and draws only front faces while the camera is outside.
                for(int k=0;k<shape.P.Length;k++){var n=shape.N[k];verts[first+k]=new MeshVertex{Position=centre+shape.P[k]*r,Normal=n,Color=hot,A=cool,B=new Vector4(seed,isDome,n.y,r)};}
                for(int k=0;k<shape.T.Length;k++)tris[ti+k]=first+shape.T[k];
            }
        }
        /// <summary>The burst sparks (NativeBurstPool, the source's BurstPool): HDR streaks along their velocity.</summary>
        public void Sparks(NativeBurstPool pool)
        {
            if(pool==null)return;var s=pool.Sparks;
            for(int i=0;i<pool.Count;i++)
            {
                int k=i*NativeBurstPool.SparkStride;float edge=(float)s[k+6],u=(float)s[k+NativeBurstPool.SparkAgeAt];
                var c=new Color((float)s[k+8],(float)s[k+9],(float)s[k+10],0)*(BurstGain*(1-.55f*u));
                Streak(new Vector3((float)s[k],(float)s[k+1],(float)s[k+2]),new Vector3((float)s[k+3],(float)s[k+4],(float)s[k+5]),c,edge*1.25f,edge*2.2f,BurstBlur,.9f,u,(float)s[k+NativeBurstPool.SparkSeedAt]);
            }
        }
        /// <summary>
        /// The modelled chip bomb (NativeVfxMeshes.Bomb at NativeFieldRules.Bomb's pose): a dark lit metal casing, its band and
        /// fuse tip glowing in the field's colour with the fuse pulse (pose.Emissive), and a hot glow on the tip while the
        /// source's fuse cube would show.
        /// </summary>
        public void Bomb(in NativeBombPose pose,int glowHex)
        {
            var at=new Vector3((float)pose.X,(float)pose.Y,(float)pose.Z);var q=new Quaternion((float)pose.Qx,(float)pose.Qy,(float)pose.Qz,(float)pose.Qw);var scale=new Vector3((float)pose.Sx,(float)pose.Sy,(float)pose.Sz);
            Color glow=Linear(glowHex),body=Linear(pose.Body),white=new Color(1,.92f,.78f,0);float pulse=(float)pose.Emissive,band=.2f+2.6f*pulse,tip=.15f+4.5f*pulse;
            // The mine is the source's flat body: its band glows less, so it reads as a puck, not a ring on the floor.
            if(pose.Look==NativeFieldLook.Mine)band*=.45f;
            var s=bomb;int first=lit.Add(s.P.Length,s.T.Length,out int ti);var verts=lit.Vertices;var tris=lit.Indices;
            var inv=new Vector3(1/Mathf.Max(scale.x,1e-4f),1/Mathf.Max(scale.y,1e-4f),1/Mathf.Max(scale.z,1e-4f));var tipGlow=Color.Lerp(glow,white,.55f);
            for(int k=0;k<s.P.Length;k++)
            {
                var part=(NativeBombPart)s.Parts[k];Color albedo,emissive=Color.clear,rim=glow*.18f;float spec=.8f,gloss=.72f;
                switch(part)
                {
                    case NativeBombPart.Band:albedo=body*.6f;emissive=glow*(s.E[k]*band);spec=.5f;gloss=.6f;break;
                    case NativeBombPart.Tip:albedo=white*.3f;emissive=tipGlow*(s.E[k]*tip);spec=.3f;gloss=.5f;break;
                    case NativeBombPart.Fuse:albedo=new Color(.03f,.03f,.03f,0);spec=.2f;gloss=.3f;break;
                    default:albedo=body*1.4f;break;
                }
                verts[first+k]=new MeshVertex{Position=at+q*Vector3.Scale(s.P[k],scale),Normal=(q*Vector3.Scale(s.N[k],inv)).normalized,Color=new Color(albedo.r,albedo.g,albedo.b,spec),A=new Vector4(emissive.r,emissive.g,emissive.b,gloss),B=new Vector4(rim.r,rim.g,rim.b,0)};
            }
            for(int k=0;k<s.T.Length;k++)tris[ti+k]=first+s.T[k];
            if(pose.FuseLit)Glow(at+q*new Vector3(0,(float)NativeVfxMeshes.BombTipY*scale.y,0),tipGlow*(.4f+2.4f*pulse),.26f+.12f*pulse,CoreGlow);
        }
        /// <summary>A smooth lit sphere (the black hole's core): albedo, a fresnel rim (HDR) and emission, all linear.</summary>
        public void Sphere(Vector3 at,float radius,Color albedo,Color rim,Color emissive)=>LitMesh(orb,at,Quaternion.identity,Vector3.one*radius,albedo,.6f,.85f,emissive,rim);
        /// <summary>A linear colour from an sRGB hex (exactly Core's NativeBlastSim.Linear).</summary>
        public static Color Linear(int hex){NativeBlastSim.Linear(hex,out double r,out double g,out double b);return new Color((float)r,(float)g,(float)b,0);}

        // ---- submission ----
        private void Upload()
        {
            if(atlas!=null||Volatile.Read(ref built)!=2)return;
            var a=Volatile.Read(ref atlasBytes);var n=Volatile.Read(ref noiseBytes);if(a==null||n==null)return;
            // Linear data (not colour), mipmapped, clamped per tile (every tile border is 0); the noise tiles.
            atlas=new Texture2D(NativeVfxTextures.SmokeSize,NativeVfxTextures.SmokeSize,TextureFormat.RGBA32,true,true){name="Native vfx smoke atlas",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
            atlas.SetPixelData(a,0);atlas.Apply(true,true);
            noise=new Texture2D(NoiseSize,NoiseSize,TextureFormat.RGBA32,false,true){name="Native vfx noise",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};
            noise.SetPixelData(n,0);noise.Apply(false,true);
            billboardMaterial.SetTexture(AtlasId,atlas);billboardMaterial.SetFloat(ReadyId,1);shockMaterial.SetTexture(NoiseId,noise);
        }
        // Soft particles need the camera's depth texture: asked for on the normal tiers (the shadowed key light already makes
        // forward render it), taken back on the low tier or when the camera changes; only a request this renderer made.
        private void SoftDepth(Camera camera)
        {
            if(depthCamera!=null&&(depthCamera!=camera||Low)){depthCamera.depthTextureMode&=~DepthTextureMode.Depth;depthCamera=null;}
            if(!Low&&(camera.depthTextureMode&DepthTextureMode.Depth)==0){camera.depthTextureMode|=DepthTextureMode.Depth;depthCamera=camera;}
            billboardMaterial.SetFloat(SoftId,!Low&&(camera.depthTextureMode&DepthTextureMode.Depth)!=0?1:0);shockMaterial.SetFloat(LowId,Low?1:0);
        }
        /// <summary>Draws this frame's queue to <paramref name="camera"/>: the billboards sorted far to near (view depth; a
        /// core glow CoreLead of its size nearer), bounded by what they hold, the lit meshes and the shock shells; then the
        /// queue is empty for the next frame.</summary>
        public void Submit(Camera camera)
        {
            if(camera==null){Begin();return;}
            Upload();SoftDepth(camera);
            var view=camera.transform;Vector3 eye=view.position,forward=view.forward,lo=Vector3.one*float.MaxValue,hi=Vector3.one*float.MinValue;
            for(int i=0;i<puffCount;i++)
            {
                ref var p=ref puffs[i];keys[i]=-Vector3.Dot(p.Centre-eye,forward)+(p.Mode==CoreGlow?CoreLead*p.Size:0);order[i]=i;
                // A billboard reaches half its size from its centre; a streak also half its segment.
                float reach=p.Size*.5f+(p.Mode==StreakMode?new Vector3(p.Axis.x,p.Axis.y,p.Axis.z).magnitude*.5f:0);
                lo=Vector3.Min(lo,p.Centre-Vector3.one*reach);hi=Vector3.Max(hi,p.Centre+Vector3.one*reach);
            }
            if(puffCount>1)Array.Sort(keys,order,0,puffCount);
            var bounds=puffCount>0?new Bounds((lo+hi)*.5f,hi-lo):Everywhere;BillboardBounds=bounds;
            billboards.Clear();
            if(puffCount>0)
            {
                int first=billboards.Add(puffCount*4,puffCount*6,out _);var verts=billboards.Vertices;
                for(int k=0;k<puffCount;k++)
                {
                    ref var p=ref puffs[order[k]];var life=new Vector4(p.U,p.Seed,p.Mode,p.Floor);int v=first+k*4;
                    verts[v]=new BillboardVertex{Centre=p.Centre,Color=p.Color,Shape=new Vector4(-1,-1,p.Size,p.Roll),Life=life,Axis=p.Axis};
                    verts[v+1]=new BillboardVertex{Centre=p.Centre,Color=p.Color,Shape=new Vector4(1,-1,p.Size,p.Roll),Life=life,Axis=p.Axis};
                    verts[v+2]=new BillboardVertex{Centre=p.Centre,Color=p.Color,Shape=new Vector4(1,1,p.Size,p.Roll),Life=life,Axis=p.Axis};
                    verts[v+3]=new BillboardVertex{Centre=p.Centre,Color=p.Color,Shape=new Vector4(-1,1,p.Size,p.Roll),Life=life,Axis=p.Axis};
                }
            }
            lit.Draw(litMaterial,camera);billboards.Draw(billboardMaterial,camera,bounds);shocks.Draw(shockMaterial,camera);
            Begin();
        }
        public void Dispose()
        {
            Begin();if(depthCamera!=null){depthCamera.depthTextureMode&=~DepthTextureMode.Depth;depthCamera=null;}
            CapturedAnimation.Release(billboards.Mesh);CapturedAnimation.Release(lit.Mesh);CapturedAnimation.Release(shocks.Mesh);
            CapturedAnimation.Release(billboardMaterial);CapturedAnimation.Release(litMaterial);CapturedAnimation.Release(shockMaterial);
            if(atlas!=null)CapturedAnimation.Release(atlas);if(noise!=null)CapturedAnimation.Release(noise);atlas=null;noise=null;
        }
    }
}
