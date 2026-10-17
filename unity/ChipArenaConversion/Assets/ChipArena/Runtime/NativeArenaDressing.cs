using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// ArenaDressing (ARENA_PROPOSAL.md 2.2 "Block geometry"; rules in Core/NativeBlockLooks.cs). For a map whose look opts in
    /// (MapLooks.json `blocks.dressed`; Brickfall), or for every map under the dev preview
    /// (the `--ca-dress-blocks` command line), each block in arena.AllBlocks is drawn as an instanced bevelled block through
    /// Resources/NativeBlockLit.shader and its authority cube and trim renderers are switched off with
    /// Renderer.forceRenderingOff. That flag is separate from Renderer.enabled, which is all the appearance preview, killcam
    /// and Siren-pick hide paths toggle (NativePreview, NativeReplay, NativeDraftPresentation) and which instanced draws
    /// ignore; so the dressing honours those states itself by drawing only where ArenaPresentation.RenderAbilities runs: the
    /// live view, and the killcam with its replay arena in place of the live one. The preview, entrance intros, the chip
    /// draft and the Siren pick return before RenderAbilities. Maps that do not opt in are never touched.
    /// Every frame each instance follows its prop's transform (falling props and fragments), takes its damage stage from
    /// health over its authored hp and stages (NativeBlockLooks.StageMax; material defaults for legacy blocks), and joins that stage's mesh batch (NativeBlockLooks.MeshFor; 5 meshes): draws of up to 500 blocks
    /// of any size and look (NativeBlockLooks.Draws; the main pass, repeated by each shadow cascade and the depth pass). A
    /// fragment inherits its authored parent's look, minus the band and tag, including on late join and replay. Legacy
    /// fragments without a parent retain the nearby removed-block fallback.
    /// The round's one dust coat (Core NativeDustCoat, owned by ArenaPresentation): this feeds it every Break and Collapse of
    /// the live arena (also while the killcam draws its replay arena, as the live round goes on), the destruction kit feeds it
    /// the structure landings (ArenaPresentation.StructureHeard), and each frame it is read at the drawn arena's clock (the
    /// field clock, the destruction kit's), so the killcam shows the coat of the moment it replays: its map-wide level goes
    /// out as the _ArenaDustCoat global (0 while no dressed map is drawn), each block's local level as its instance dust. It is
    /// cleared when the live arena is rebuilt (a new round or arena). The band, tag and lamp keep a tenth of it (the shader).
    /// The collapse telegraph (3.9, 3.10, from Core NativeStructureFx.TelegraphAt on the same clock and records as the
    /// destruction kit's footprint, grit and sound): every block inside a holding stress box sags (down at most 8 cm and
    /// leaning at most 1 degree outward, scaled by its height in the box) and sends its window light (8 Hz strobe through the
    /// telegraph, then black until the segment lands; window-look blocks show it); a banded member's lamp strobes red there
    /// and at S2, glows amber while the members are locked, else stays dark (NativeBlockLooks.Lamp, Locked). With no stress
    /// record the per-block query is skipped (one check a frame; old maps never have one), and no band means no lamp work.
    /// Faces that share a plane (Core NativeSurfaceLayers.Blocks, once per map): of two blocks' faces that face one way, lie in
    /// one plane and overlap, the larger is drawn over the other by whole 2 mm steps (the block grows on that side only), so
    /// the depth test never has to choose between them; colliders and the content never change. The kits do the same for their
    /// parts over these lifted faces (NativeSurfaceLayers.Over).
    /// No allocations per frame in steady state; new props and new fragment looks allocate once.
    /// AllBlocks includes static, carried and rubble geometry. Only a segment's own members receive its visual telegraph;
    /// overlapping warnings do not sag neighbouring static floors or unrelated structures.
    /// </summary>
    public sealed class NativeArenaDressing : MonoBehaviour
    {
        /// <summary>The dev preview: every map's blocks dressed (MapLooks.json `preview` looks after the map's own). Command line only.</summary>
        public static readonly bool Preview=Array.IndexOf(Environment.GetCommandLineArgs(),"--ca-dress-blocks")>=0;
        /// <summary>The last frame's instanced draws and blocks (main pass).</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}
        /// <summary>Block ids left undrawn (a breach panel while a breach presentation shows it broken, through
        /// ArenaPresentation.SetWardBreach, or a wall block a Hulk broke out, SetWardBlockBreach); empty on every map until a breach
        /// happens, cleared when a new live arena binds.</summary>
        public static readonly HashSet<string> Hidden=new HashSet<string>();
        /// <summary>Block ids drawn displaced (world m: a wall block's knock shudder while a Hulk pounds it, through
        /// ArenaPresentation.NudgeWardBlock; the block itself never moves); empty until a breach knocks, cleared with Hidden.</summary>
        public static readonly Dictionary<string,Vector3> Nudged=new Dictionary<string,Vector3>();
        private const float BoundsSize=200;
        private sealed class Entry {public int Look,Color,Flags,Seen;public string Segment;public uint Hash;public float Seed,Max,S1,S2,Reach;public bool Pending,Machine;public Vector3 Last,Offset;public Vector4 Size,Rest,Linear;}
        private sealed class Batch {public readonly Matrix4x4[] Matrices=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];}
        private struct Gone {public Vector3 Position;public float Reach;public int Look,Color,Flags,Frame;public Vector4 Linear;}
        private struct Kept {public int Look,Color,Flags;}
        private readonly Dictionary<NativePropBody,Entry> entries=new Dictionary<NativePropBody,Entry>();private readonly Stack<Entry> spare=new Stack<Entry>();
        private readonly List<NativePropBody> removed=new List<NativePropBody>();private readonly List<Renderer> renderers=new List<Renderer>();
        private readonly Dictionary<string,ContentMaterial> materials=new Dictionary<string,ContentMaterial>();
        private readonly List<Batch>[] batches={new List<Batch>(),new List<Batch>(),new List<Batch>(),new List<Batch>(),new List<Batch>()};private readonly int[] counts=new int[NativeBlockLooks.Meshes];
        private readonly Gone[] gone=new Gone[32];private int goneNext;
        // Fragment looks inherited this round, by fragment id (live and killcam arenas share the ids); cleared with the dust coat.
        private readonly Dictionary<string,Kept> kept=new Dictionary<string,Kept>();
        private NativeArena liveArena;private int liveGeneration=-1,lastEvent;private float published=-1;
        private NativeArena drawn;private int generation=-1;private string mapId;private MapLook look;private MapLookBlock[] preview;private bool dressed;private int floor;
        // The map's blocks' face lifts (Core NativeSurfaceLayers.Blocks: of two faces in one plane the larger is drawn over, by
        // whole 2 mm steps; render only) and each block id's index into them; fragments and rubble take none.
        private float[] lifts=Array.Empty<float>();private readonly Dictionary<string,int> liftIndex=new Dictionary<string,int>();private ContentBlock[] liftBlocks;
        private Mesh[] meshes;private Material material;private MaterialPropertyBlock block;private bool instancing;
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        private static readonly int HazeId=Shader.PropertyToID("_ArenaHaze"),HazeTopId=Shader.PropertyToID("_ArenaHazeTop"),DustId=Shader.PropertyToID("_ArenaDust"),CoatId=Shader.PropertyToID(NativeDustCoat.Global);
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
        /// <summary>Draws the arena's dressed blocks. now: the drawn arena's field clock (the destruction kit's); coat: the round's
        /// dust coat; structures: the destruction kit's records (null before its first frame).</summary>
        public void Render(NativeArena arena,float now,NativeDustCoat coat,NativeStructureFx structures)
        {
            Draws=Instances=0;if(arena?.Root==null||coat==null)return;
            if(!ReferenceEquals(arena,drawn)||arena.Generation!=generation||!ReferenceEquals(arena.MapId,mapId))Rebind(arena,coat);
            if(!dressed){Publish(0);return;}if(material==null)Build();int frame=Time.frameCount;
            if(liveArena!=null&&liveArena.Generation==liveGeneration)Feed(liveArena,coat);
            coat.At(now);Publish(coat.Wide);
            var match=arena.Match;bool tell=structures!=null&&structures.StressHolding(now),locked=match!=null&&NativeBlockLooks.Locked(match.Phase,match.FightAt,now),redOn=NativeStructureFx.LampStrobe(now)>0;
            foreach(var p in arena.AllBlocks){if(p==null||p.Definition==null)continue;if(!entries.TryGetValue(p,out var e)){e=Create(p);entries.Add(p,e);}e.Seen=frame;}
            foreach(var pair in entries)if(pair.Value.Seen!=frame)removed.Add(pair.Key);
            foreach(var p in removed){var e=entries[p];entries.Remove(p);gone[goneNext]=new Gone{Position=e.Last,Reach=e.Reach,Look=e.Look,Color=e.Color,Flags=e.Flags,Linear=e.Linear,Frame=frame};goneNext=(goneNext+1)%gone.Length;spare.Push(e);}
            removed.Clear();for(int s=0;s<counts.Length;s++)counts[s]=0;
            foreach(var p in arena.AllBlocks)
            {
                if(p==null||p.Definition==null||!entries.TryGetValue(p,out var e)||e.Machine)continue;if(Hidden.Count>0&&p.Definition.id!=null&&Hidden.Contains(p.Definition.id))continue;var t=p.transform;Vector3 pos=t.position;Quaternion rot=t.rotation;
                if(e.Pending){e.Pending=false;Inherit(e,p.Definition.id,pos,frame);}e.Last=pos;if(Nudged.Count>0&&p.Definition.id!=null&&Nudged.TryGetValue(p.Definition.id,out var nudge))pos+=nudge;
                float f=NativeBlockLooks.Fraction(p.Health,e.Max,p.Indestructible);int stage=NativeBlockLooks.Stage(f,e.S1,e.S2);Vector4 size=e.Size,rest=e.Rest;
                // S3 is a stub of the block's lower 24% (same base, same rest-space pattern).
                if(stage==3){float drop=size.y*(1-NativeBlockLooks.StubShare);pos-=rot*new Vector3(0,drop,0);size.y*=NativeBlockLooks.StubShare;rest.y-=drop;}
                // The 3.9 telegraph: sag (moving the instance only; the rest-space pattern stays glued) and the window light.
                float window=1;bool stressed=false;
                if(tell)
                {
                    V3 point=new V3(pos.x,pos.y,pos.z),sag=default;float qx=0,qy=0,qz=0,qw=1;
                    if(arena.Map.unity){if(e.Segment!=null)stressed=structures.TelegraphFor(NativeStructureFx.Seed(arena.MapId,e.Segment,NativeStructureFx.RoundIndex(match.Set,match.Round)),point,now,out window,out sag,out qx,out qy,out qz,out qw);}
                    else stressed=structures.TelegraphAt(point,now,out window,out sag,out qx,out qy,out qz,out qw);
                    if(stressed){pos+=new Vector3(sag.x,sag.y,sag.z);rot=new Quaternion(qx,qy,qz,qw)*rot;}
                }
                int flags=e.Flags|NativeBlockLooks.Spin(e.Hash,stage);if((e.Flags&NativeBlockLooks.Band)!=0)flags|=NativeBlockLooks.Lamp(stage,stressed,locked,redOn);
                rest.w=coat.Local(pos.x,pos.z);var color=e.Linear;color.w=window;
                Add(NativeBlockLooks.MeshFor(stage,size.y),Matrix4x4.TRS(pos+rot*e.Offset,rot,Vector3.one),size,rest,color,new Vector4(e.Look,NativeBlockLooks.Normalised(f,e.S1,e.S2),e.Seed,flags));
            }
            NativeLighting.ArenaRim();var k=look?.blocks;Color haze=k!=null?Linear(NativeArena.ColorOf(k.hazeColor)):Color.black;
            Shader.SetGlobalVector(HazeId,new Vector4(haze.r,haze.g,haze.b,k!=null?k.haze:0));Shader.SetGlobalFloat(HazeTopId,k!=null&&k.hazeTop>0?k.hazeTop:1);Shader.SetGlobalFloat(DustId,k!=null?k.dust:0);
            for(int s=0;s<counts.Length;s++)for(int start=0,i=0;start<counts[s];start+=NativeBlockLooks.Chunk,i++){int n=Math.Min(NativeBlockLooks.Chunk,counts[s]-start);Draw(meshes[s],batches[s][i],n);Draws++;Instances+=n;}
        }
        private void Rebind(NativeArena arena,NativeDustCoat coat)
        {
            foreach(var e in entries.Values)spare.Push(e);entries.Clear();drawn=arena;generation=arena.Generation;mapId=arena.MapId;
            var map=arena.Content.Map(mapId);look=NativeLighting.Look(map);preview=NativeLighting.LookSet().preview;dressed=NativeBlockLooks.Dressed(look,Preview);floor=map.floor;
            var source=map.blocks;if(dressed&&!ReferenceEquals(source,liftBlocks)){liftBlocks=source;lifts=NativeSurfaceLayers.Blocks(source);liftIndex.Clear();if(source!=null)for(int i=0;i<source.Length;i++)if(source[i]?.id!=null&&!liftIndex.ContainsKey(source[i].id))liftIndex[source[i].id]=i;}
            materials.Clear();foreach(var m in arena.Content.materials)if(m!=null&&m.id!=null)materials[m.id]=m;
            // The killcam's replay arena (in its own scene) shares the live round's dust coat and fragment looks; a new round or arena
            // starts clean ones.
            if(!arena.ReplicaScene.IsValid()&&(!ReferenceEquals(arena,liveArena)||arena.Generation!=liveGeneration)){liveArena=arena;liveGeneration=arena.Generation;lastEvent=0;coat.Clear();kept.Clear();Hidden.Clear();Nudged.Clear();for(int i=0;i<gone.Length;i++)gone[i].Frame=-1;}
        }
        private Entry Create(NativePropBody prop)
        {
            var e=spare.Count>0?spare.Pop():new Entry();var b=prop.Definition;materials.TryGetValue(b.material??"",out var m);int content=b.material=="foundation"?floor:m!=null?m.color:0xffffff;
            ContentBlock parent=null;if(prop.Fragment&&!string.IsNullOrEmpty(b.parent))foreach(var candidate in drawn.Map.blocks)if(candidate.id==b.parent){parent=candidate;break;}
            var c=NativeBlockLooks.Resolve(look?.blockLooks,Preview?preview:null,parent?.id??b.id,parent?.material??b.material,content);
            e.Segment=null;if(!prop.Fragment&&b.role!="rubble"&&b.role!="static")foreach(var segment in drawn.Map.segments??Array.Empty<StructureDefinition>())
                if(segment.id==b.segment||Array.Exists(segment.supports,s=>s.member==b.id)){e.Segment=segment.id;break;}
            // A loose fragment never carries the band or tag; one whose look this round already knows takes it back.
            e.Look=c.Look;e.Color=c.Color;e.Flags=prop.Fragment?c.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag)|NativeBlockLooks.Fragment:c.Flags;e.Pending=prop.Fragment&&parent==null;
            if(prop.Fragment&&kept.TryGetValue(b.id??"",out var k)){e.Look=k.Look;e.Color=k.Color;e.Flags=k.Flags;e.Pending=false;}
            e.Hash=NativeBlockLooks.Hash(b.id);e.Seed=NativeBlockLooks.Seed(b.id);e.Max=NativeBlockLooks.StageMax(b,m,out e.S1,out e.S2);e.Seen=0;
            e.Size=new Vector4(b.size.x*.5f,b.size.y*.5f,b.size.z*.5f,NativeBlockLooks.Chamfer);e.Rest=new Vector4(b.pos.x,b.pos.y,b.pos.z,0);e.Offset=Vector3.zero;
            // A map block's lifted faces: the centre moves half the lift along its own axes, the half extent grows by half and the rest
            // centre moves with it (the pattern stays put).
            if(!prop.Fragment&&b.id!=null&&liftIndex.TryGetValue(b.id,out int lifted))
            {NativeSurfaceLayers.Apply(lifts,lifted,out float sx,out float sy,out float sz,out float gx,out float gy,out float gz);e.Size+=new Vector4(gx,gy,gz,0);e.Offset=new Vector3(sx,sy,sz);e.Rest+=new Vector4(sx,sy,sz,0);}
            e.Reach=e.Size.magnitude+.3f;e.Last=prop.transform.position;
            var lin=Linear(NativeArena.ColorOf(e.Color));e.Linear=new Vector4(lin.r,lin.g,lin.b,1);
            // A PvE machine's collider stays hidden (its cube off, no dressed block): the sculpted model is the machine
            // (NativeMachinePresentation), and a block over it would hide its front.
            e.Machine=prop.LocalFixture&&!prop.Fragment&&NativeMachineModels.IsCollider(b.id);
            prop.GetComponentsInChildren(true,renderers);foreach(var r in renderers)r.forceRenderingOff=true;renderers.Clear();
            return e;
        }
        // A new fragment takes the look of the closest block that left Props this frame within that block's reach (no band or
        // tag), and the round keeps it under the fragment's id.
        private void Inherit(Entry e,string id,Vector3 at,int frame)
        {
            int best=-1;float near=float.MaxValue;
            for(int i=0;i<gone.Length;i++){if(gone[i].Frame!=frame)continue;float d=(gone[i].Position-at).magnitude;if(d<=gone[i].Reach&&d<near){near=d;best=i;}}
            if(best<0)return;var g=gone[best];e.Look=g.Look;e.Color=g.Color;e.Linear=g.Linear;e.Flags=(g.Flags&~(NativeBlockLooks.Band|NativeBlockLooks.Tag))|NativeBlockLooks.Fragment;
            if(id!=null)kept[id]=new Kept{Look=e.Look,Color=e.Color,Flags=e.Flags};
        }
        // The live arena's Break and Collapse events into the coat at their match times and places, height included (event ids
        // only grow within a round; a lower newest id restarts the cursor, and the coat ignores an entry it already has).
        private void Feed(NativeArena arena,NativeDustCoat coat)
        {
            int newest=0;foreach(var ev in arena.Events)newest=Math.Max(newest,ev.Id);if(newest<lastEvent)lastEvent=0;
            foreach(var ev in arena.Events)if(ev.Id>lastEvent&&(ev.Kind==EventKind.Break||ev.Kind==EventKind.Collapse))coat.Break(ev.Time,ev.Position.x,ev.Position.y,ev.Position.z);
            lastEvent=Math.Max(lastEvent,newest);
        }
        private void Publish(float wide){if(wide==published)return;published=wide;Shader.SetGlobalFloat(CoatId,wide);}
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
        private NativeArenaDressing arenaDressing;private readonly NativeDustCoat dustCoat=new NativeDustCoat();
        /// <summary>The round's one dust coat (Core NativeDustCoat): breaks from NativeArenaDressing, landings from StructureHeard.</summary>
        public NativeDustCoat DustCoat=>dustCoat;
        // The arena's dressed blocks (NativeArenaDressing): RenderAbilities runs this for the live arena and the killcam's replay
        // arena, on its field clock (the destruction kit's), with the kit's records for the collapse telegraph.
        private void ArenaDressing(NativeArena arena){if(arenaDressing==null)arenaDressing=gameObject.AddComponent<NativeArenaDressing>();arenaDressing.Render(arena,FieldClock(arena),dustCoat,structureFx?.Fx);}
        /// <summary>
        /// One structure event (ARENA_PROPOSAL.md 5.4) for the kits beside the destruction kit, which calls this from its intake
        /// and its dev Sandbox preview: the atmosphere (pigeons and drifting dust, Brickfall only: BrickfallStructure) and the
        /// round's dust coat (landings). Both are idempotent on time and place, so an event fed again never doubles.
        /// </summary>
        internal void StructureHeard(NativeArena arena,NativeStructureCue cue,Vector3 position,float radius,float amount,float time)
        {
            BrickfallStructure(arena,cue==NativeStructureCue.Stress?NativeBrickfallCue.Stress:cue==NativeStructureCue.Fall?NativeBrickfallCue.Fall:NativeBrickfallCue.Land,position,radius,amount,time);
            if(cue==NativeStructureCue.Land)dustCoat.Land(time,position.x,position.z,radius,amount);
        }
    }
}
