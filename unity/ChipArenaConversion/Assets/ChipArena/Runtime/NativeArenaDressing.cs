using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// ArenaDressing (ARENA_PROPOSAL.md 2.2 "Block geometry"; rules in Core/NativeBlockLooks.cs). For a map whose look opts in
    /// (MapLooks.json `blocks.dressed`; only Brickfall, which has no map data yet), or for every map under the dev preview
    /// (the `--ca-dress-blocks` command line), each prop in arena.Props is drawn as an instanced bevelled block through
    /// Resources/NativeBlockLit.shader and its authority cube and trim renderers are switched off with
    /// Renderer.forceRenderingOff. That flag is separate from Renderer.enabled, which is all the appearance preview, killcam
    /// and Siren-pick hide paths toggle (NativePreview, NativeReplay, NativeDraftPresentation) and which instanced draws
    /// ignore; so the dressing honours those states itself by drawing only where ArenaPresentation.RenderAbilities runs: the
    /// live view, and the killcam with its replay arena in place of the live one. The preview, entrance intros, the chip
    /// draft and the Siren pick return before RenderAbilities. Maps that do not opt in are never touched.
    /// Every frame each instance follows its prop's transform (falling props and fragments), takes its damage stage from
    /// health over its material's durability, and joins that stage's mesh batch (NativeBlockLooks.MeshFor; 5 meshes): draws
    /// of up to 500 blocks of any size and
    /// look (NativeBlockLooks.Draws; the main pass, repeated by each shadow cascade and the depth pass). A fragment takes the
    /// look of the block it broke from: the prop that left Props in the same frame within reach of it. Break and Collapse
    /// events in the live arena dust a NativeDustCoat for the round. No allocations per frame in steady state; new props
    /// allocate their entry once. Static blocks that Astra splits out of Props (5.3) are not drawn here yet.
    /// </summary>
    public sealed class NativeArenaDressing : MonoBehaviour
    {
        /// <summary>The dev preview: every map's blocks dressed (MapLooks.json `preview` looks after the map's own). Command line only.</summary>
        public static readonly bool Preview=Array.IndexOf(Environment.GetCommandLineArgs(),"--ca-dress-blocks")>=0;
        /// <summary>The last frame's instanced draws and blocks (main pass).</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}
        private const float BoundsSize=200;
        private sealed class Entry {public int Look,Color,Flags,Seen;public uint Hash;public float Seed,Max,Reach;public bool Pending;public Vector3 Last;public Vector4 Size,Rest,Linear;}
        private sealed class Batch {public readonly Matrix4x4[] Matrices=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];}
        private struct Gone {public Vector3 Position;public float Reach;public int Look,Color,Flags,Frame;public Vector4 Linear;}
        private readonly Dictionary<NativePropBody,Entry> entries=new Dictionary<NativePropBody,Entry>();private readonly Stack<Entry> spare=new Stack<Entry>();
        private readonly List<NativePropBody> removed=new List<NativePropBody>();private readonly List<Renderer> renderers=new List<Renderer>();
        private readonly Dictionary<string,ContentMaterial> materials=new Dictionary<string,ContentMaterial>();
        private readonly List<Batch>[] batches={new List<Batch>(),new List<Batch>(),new List<Batch>(),new List<Batch>(),new List<Batch>()};private readonly int[] counts=new int[NativeBlockLooks.Meshes];
        private readonly Gone[] gone=new Gone[32];private int goneNext;
        private readonly NativeDustCoat coat=new NativeDustCoat();private NativeArena liveArena;private int liveGeneration=-1,lastEvent;
        private NativeArena drawn;private int generation=-1;private string mapId;private MapLook look;private MapLookBlock[] preview;private bool dressed;private int floor;
        private Mesh[] meshes;private Material material;private MaterialPropertyBlock block;private bool instancing;
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        private static readonly int HazeId=Shader.PropertyToID("_ArenaHaze"),HazeTopId=Shader.PropertyToID("_ArenaHazeTop"),DustId=Shader.PropertyToID("_ArenaDust");
        private static readonly int FlashPosId=Shader.PropertyToID("_ArenaFlashPos"),FlashColorId=Shader.PropertyToID("_ArenaFlashColor");
        private static readonly Vector4[] flashPos=new Vector4[NativeBlastSim.LightCapacity],flashColor=new Vector4[NativeBlastSim.LightCapacity];

        /// <summary>
        /// The blast flashes for NativeBlockLit (NativeBlastPresentation.Render, every frame it runs): each flash's position and
        /// range, and its colour times intensity as Unity applies them to the matching point light (NativeEntranceLight.Applied);
        /// the shader's fall-off follows that light's (NativeBlockLooks.FlashFalloff). A dark flash sits far below with no colour.
        /// </summary>
        public static void Flashes(NativeBlastFlash[] flashes)
        {
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear,linearIntensity=GraphicsSettings.lightsUseLinearIntensity;
            for(int i=0;i<flashPos.Length;i++)
            {
                var f=i<flashes.Length?flashes[i]:null;float intensity=f!=null?NativeBlastSim.UnityLightIntensity(f.Intensity):0;
                if(!(intensity>.001f)){flashPos[i]=new Vector4(0,-10000,0,1);flashColor[i]=Vector4.zero;continue;}
                var c=NativeArena.ColorOf(f.Color);flashPos[i]=new Vector4((float)f.X,(float)f.Y,(float)f.Z,(float)f.Distance);
                flashColor[i]=new Vector4((float)NativeEntranceLight.Applied(c.r,intensity,linear,linearIntensity),(float)NativeEntranceLight.Applied(c.g,intensity,linear,linearIntensity),(float)NativeEntranceLight.Applied(c.b,intensity,linear,linearIntensity),0);
            }
            Shader.SetGlobalVectorArray(FlashPosId,flashPos);Shader.SetGlobalVectorArray(FlashColorId,flashColor);
        }
        public void Render(NativeArena arena)
        {
            Draws=Instances=0;if(arena?.Root==null)return;
            if(!ReferenceEquals(arena,drawn)||arena.Generation!=generation||!ReferenceEquals(arena.MapId,mapId))Rebind(arena);
            if(!dressed)return;if(material==null)Build();int frame=Time.frameCount;
            if(ReferenceEquals(arena,liveArena))Coat(arena);
            foreach(var p in arena.Props){if(p==null||p.Definition==null)continue;if(!entries.TryGetValue(p,out var e)){e=Create(p);entries.Add(p,e);}e.Seen=frame;}
            foreach(var pair in entries)if(pair.Value.Seen!=frame)removed.Add(pair.Key);
            foreach(var p in removed){var e=entries[p];entries.Remove(p);gone[goneNext]=new Gone{Position=e.Last,Reach=e.Reach,Look=e.Look,Color=e.Color,Flags=e.Flags,Linear=e.Linear,Frame=frame};goneNext=(goneNext+1)%gone.Length;spare.Push(e);}
            removed.Clear();for(int s=0;s<counts.Length;s++)counts[s]=0;
            foreach(var p in arena.Props)
            {
                if(p==null||p.Definition==null||!entries.TryGetValue(p,out var e))continue;var t=p.transform;Vector3 pos=t.position;Quaternion rot=t.rotation;
                if(e.Pending){e.Pending=false;Inherit(e,pos,frame);}e.Last=pos;
                float f=NativeBlockLooks.Fraction(p.Health,e.Max,p.Indestructible);int stage=NativeBlockLooks.Stage(f);Vector4 size=e.Size,rest=e.Rest;
                // S3 is a stub of the block's lower 24% (same base, same rest-space pattern).
                if(stage==3){float drop=size.y*(1-NativeBlockLooks.StubShare);pos-=rot*new Vector3(0,drop,0);size.y*=NativeBlockLooks.StubShare;rest.y-=drop;}
                rest.w=coat.Sample(pos.x,pos.z);
                Add(NativeBlockLooks.MeshFor(stage,size.y),Matrix4x4.TRS(pos,rot,Vector3.one),size,rest,e.Linear,new Vector4(e.Look,f,e.Seed,e.Flags|NativeBlockLooks.Spin(e.Hash,stage)));
            }
            NativeLighting.ArenaRim();var k=look?.blocks;Color haze=k!=null?Linear(NativeArena.ColorOf(k.hazeColor)):Color.black;
            Shader.SetGlobalVector(HazeId,new Vector4(haze.r,haze.g,haze.b,k!=null?k.haze:0));Shader.SetGlobalFloat(HazeTopId,k!=null&&k.hazeTop>0?k.hazeTop:1);Shader.SetGlobalFloat(DustId,k!=null?k.dust:0);
            for(int s=0;s<counts.Length;s++)for(int start=0,i=0;start<counts[s];start+=NativeBlockLooks.Chunk,i++){int n=Math.Min(NativeBlockLooks.Chunk,counts[s]-start);Draw(meshes[s],batches[s][i],n);Draws++;Instances+=n;}
        }
        private void Rebind(NativeArena arena)
        {
            foreach(var e in entries.Values)spare.Push(e);entries.Clear();drawn=arena;generation=arena.Generation;mapId=arena.MapId;
            var map=arena.Content.Map(mapId);look=NativeLighting.Look(map);preview=NativeLighting.LookSet().preview;dressed=NativeBlockLooks.Dressed(look,Preview);floor=map.floor;
            materials.Clear();foreach(var m in arena.Content.materials)if(m!=null&&m.id!=null)materials[m.id]=m;
            // The killcam's replay arena (in its own scene) shares the live round's dust coat; a new round or arena starts a clean one.
            if(!arena.ReplicaScene.IsValid()&&(!ReferenceEquals(arena,liveArena)||arena.Generation!=liveGeneration)){liveArena=arena;liveGeneration=arena.Generation;lastEvent=0;coat.Clear();for(int i=0;i<gone.Length;i++)gone[i].Frame=-1;}
        }
        private Entry Create(NativePropBody prop)
        {
            var e=spare.Count>0?spare.Pop():new Entry();var b=prop.Definition;materials.TryGetValue(b.material??"",out var m);int content=b.material=="foundation"?floor:m!=null?m.color:0xffffff;
            var c=NativeBlockLooks.Resolve(look?.blockLooks,Preview?preview:null,b.id,b.material,content);
            e.Look=c.Look;e.Color=c.Color;e.Flags=c.Flags|(prop.Fragment?NativeBlockLooks.Fragment:0);e.Hash=NativeBlockLooks.Hash(b.id);e.Seed=NativeBlockLooks.Seed(b.id);e.Max=m!=null?m.durability:0;e.Pending=prop.Fragment;e.Seen=0;
            e.Size=new Vector4(b.size.x*.5f,b.size.y*.5f,b.size.z*.5f,NativeBlockLooks.Chamfer);e.Rest=new Vector4(b.pos.x,b.pos.y,b.pos.z,0);e.Reach=e.Size.magnitude+.3f;e.Last=prop.transform.position;
            var lin=Linear(NativeArena.ColorOf(e.Color));e.Linear=new Vector4(lin.r,lin.g,lin.b,0);
            prop.GetComponentsInChildren(true,renderers);foreach(var r in renderers)r.forceRenderingOff=true;renderers.Clear();
            return e;
        }
        // A new fragment takes the look of the closest block that left Props this frame within that block's reach (no band or tag).
        private void Inherit(Entry e,Vector3 at,int frame)
        {
            int best=-1;float near=float.MaxValue;
            for(int i=0;i<gone.Length;i++){if(gone[i].Frame!=frame)continue;float d=(gone[i].Position-at).magnitude;if(d<=gone[i].Reach&&d<near){near=d;best=i;}}
            if(best<0)return;var g=gone[best];e.Look=g.Look;e.Color=g.Color;e.Linear=g.Linear;e.Flags=(g.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag))|NativeBlockLooks.Fragment;
        }
        // Break and Collapse events dust the coat once each (event ids only grow within a round; a lower newest id restarts).
        private void Coat(NativeArena arena)
        {
            int newest=0;foreach(var ev in arena.Events)newest=Math.Max(newest,ev.Id);if(newest<lastEvent)lastEvent=0;
            foreach(var ev in arena.Events)if(ev.Id>lastEvent&&(ev.Kind==EventKind.Break||ev.Kind==EventKind.Collapse))coat.Add(ev.Position.x,ev.Position.z);
            lastEvent=Math.Max(lastEvent,newest);
        }
        private void Add(int mesh,Matrix4x4 matrix,Vector4 size,Vector4 rest,Vector4 color,Vector4 code)
        {
            int n=counts[mesh],chunk=n/NativeBlockLooks.Chunk,i=n%NativeBlockLooks.Chunk;var list=batches[mesh];if(chunk>=list.Count)list.Add(new Batch());var b=list[chunk];
            b.Matrices[i]=matrix;b.Size[i]=size;b.Rest[i]=rest;b.Color[i]=color;b.Look[i]=code;counts[mesh]=n+1;
        }
        private void Draw(Mesh mesh,Batch b,int count)
        {
            if(instancing)
            {
                block.Clear();block.SetVectorArray(SizeId,b.Size);block.SetVectorArray(RestId,b.Rest);block.SetVectorArray(ColorId,b.Color);block.SetVectorArray(LookId,b.Look);
                try{Graphics.DrawMeshInstanced(mesh,0,material,b.Matrices,count,block,ShadowCastingMode.On,true,0,null);return;}
                catch(InvalidOperationException){instancing=false;}catch(ArgumentException){instancing=false;}
            }
            for(int i=0;i<count;i++){block.Clear();block.SetVector(SizeId,b.Size[i]);block.SetVector(RestId,b.Rest[i]);block.SetVector(ColorId,b.Color[i]);block.SetVector(LookId,b.Look[i]);Graphics.DrawMesh(mesh,b.Matrices[i],material,0,null,0,block,ShadowCastingMode.On,true);}
        }
        private void Build()
        {
            material=new Material(Resources.Load<Shader>("NativeBlockLit")){name="Arena blocks",enableInstancing=true};block=new MaterialPropertyBlock();instancing=SystemInfo.supportsInstancing;
            meshes=new Mesh[NativeBlockLooks.Meshes];for(int s=0;s<meshes.Length;s++)meshes[s]=MeshOf(NativeBlockLooks.Build(s),s);
        }
        // A stage mesh: the preview cube in POSITION, the lattice in TEXCOORD0 and TEXCOORD1, the baked shade in COLOR; the
        // bounds cover any block (the shader moves the vertices out to the instance's size).
        private static Mesh MeshOf(NativeBlockLooks.StageMesh s,int stage)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new List<Vector4>(n);var b=new List<Vector4>(n);
            for(int i=0;i<n;i++)
            {
                v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);
                a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));
            }
            var mesh=new Mesh{name="Arena block mesh "+stage};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;
            mesh.bounds=new Bounds(Vector3.zero,Vector3.one*BoundsSize);mesh.UploadMeshData(true);return mesh;
        }
        private static Color Linear(Color c)=>QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;
        private void OnDestroy(){if(meshes!=null)foreach(var m in meshes)if(m!=null)CapturedAnimation.Release(m);if(material!=null)CapturedAnimation.Release(material);meshes=null;material=null;}
    }
    public sealed partial class ArenaPresentation
    {
        private NativeArenaDressing arenaDressing;
        // The arena's dressed blocks (NativeArenaDressing): RenderAbilities runs this for the live arena and the killcam's replay arena.
        private void ArenaDressing(NativeArena arena){if(arenaDressing==null)arenaDressing=gameObject.AddComponent<NativeArenaDressing>();arenaDressing.Render(arena);}
    }
}
