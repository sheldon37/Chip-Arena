using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The stage art's loader and drawer (docs/arenas/STAGE-ART.md; Core NativeStageArt reads the bank's plan), one per stage
    /// presentation that has art (the Sanctum's: NativeSanctumDraw). With the stage's bank (Resources/ChipArenaStage&lt;Name&gt;, from
    /// the builder's Drive zip) it draws the modelled art instead of the boxes:
    ///  - the static chunks once, as renderers (ChipArena/StageLit, two materials shared by every chunk: solid and glow), each
    ///    chunk's tier chosen each frame by the eye's distance to it (the Low tier on the low graphics tier);
    ///  - the breakable blocks' models where their blocks stand, while they stand at NativeStageArt.Keep or better, soot growing
    ///    with their damage;
    ///  - the rig parts each frame where the stage's Core art pose puts them (NativeStageArtPose);
    ///  - a reflection probe over the stage, rendered once a few frames after the art first shows (the metals and the polish).
    /// While it draws, <see cref="Hides"/> tells the dressing which map blocks it replaces (their boxes are left undrawn) and
    /// <see cref="Ready"/> tells the stage's presentation to leave out its box kit and its box rig. Without the bank (CI, a build
    /// without the Drive zip, `--ca-no-pack-banks`) nothing is created, Ready stays false and every box draws exactly as before.
    /// Never sim data and never a collider. Allocation free per frame once prepared.
    /// </summary>
    public sealed class NativeStageArtDraw : MonoBehaviour
    {
        private static string activeMap;private static HashSet<string> activeCovers;private static int activeFrame=-10;
        /// <summary>Whether the art drew this map block's place this frame (the dressing leaves its box out). A breakable block
        /// is replaced only at its model's kept stages.</summary>
        public static bool Hides(string mapId,string blockId,int stage)
        {
            if(blockId==null||activeCovers==null||mapId!=activeMap||Time.frameCount-activeFrame>1)return false;
            return activeCovers.Contains(blockId)&&stage<=NativeStageArt.Keep;
        }

        private sealed class ChunkDraw{public NativeStageArt.Chunk Plan;public Renderer[] Tiers=new Renderer[NativeStageArt.Tiers];public Bounds Box;public int Shown=-1;}
        private sealed class Piece{public Mesh Mesh;public Matrix4x4 Place;public int Subs;}
        private sealed class BlockDraw{public Piece[] Tiers;public float S1,S2,Max;}
        private string bankName,mapId;private NativeStageArt.Plan plan;private bool failed;
        private Material solid,glow;private MaterialPropertyBlock block;private Shader shader;
        /// <summary>Set before <see cref="Prepare"/>: where the reflection probe stands (default: the centre of the art's bounds) and the
        /// colour it sees where nothing is drawn (default: near black; an open-air stage gives its sky's).</summary>
        public Vector3? ProbeAt;public Color? ProbeSky;
        private GameObject root;private ReflectionProbe probe;private int probeFrame=-1,probeRendered=-1,probeSize;
        private readonly List<ChunkDraw> chunks=new List<ChunkDraw>();
        private readonly Dictionary<string,BlockDraw> blocks=new Dictionary<string,BlockDraw>(StringComparer.Ordinal);
        private Piece[][] rig=Array.Empty<Piece[]>();private bool[] rigShadow=Array.Empty<bool>();
        // Rig parts a pose draws InstanceFrom times or more in a frame are drawn instanced (the glow per instance): made on first use
        private const int InstanceFrom=8,BatchSize=256;
        private sealed class Batch{public readonly Matrix4x4[] M=new Matrix4x4[BatchSize];public readonly Vector4[] Glow=new Vector4[BatchSize];public int Count;public Piece Piece;public ShadowCastingMode Shadow;}
        private int[] slotCount=Array.Empty<int>();private Batch[] batches=Array.Empty<Batch>();private bool instanced;
        private int lastFrame=-10;private readonly HashSet<int> built=new HashSet<int>();
        private static readonly int GlowId=Shader.PropertyToID("_StageGlow"),TintId=Shader.PropertyToID("_StageTint"),GlowFlag=Shader.PropertyToID("_Glow");
        /// <summary>The art is loaded for the prepared map (its boxes give way to it).</summary>
        public bool Ready=>plan!=null&&root!=null;
        /// <summary>Last frame: the static chunks shown at each tier, the block models and rig parts drawn.</summary>
        public int ChunksFine{get;private set;}public int ChunksMid{get;private set;}public int ChunksLow{get;private set;}public int Drawn{get;private set;}

        /// <summary>Loads the map's art (once per map; false without a bank). rigSlots: the stage's rig prop names (its Core art's
        /// slots); shadowless: the slots that cast no shadow.</summary>
        public bool Prepare(ContentMap map,string[] rigSlots,Func<int,bool> casts)
        {
            if(map==null)return false;var name=NativeStageArt.BankOf(map.id);if(name==null||failed)return false;
            if(name==bankName&&map.id==mapId&&plan!=null&&root!=null)return true;
            Release();
            try
            {
                var bank=NativePackBanks.Bank(name);if(bank==null)return false;var p=NativeStageArt.Read(bank);if(p==null||p.Static.Count==0)return false;
                shader=Resources.Load<Shader>("NativeStageLit");if(shader==null||!shader.isSupported)throw new InvalidOperationException("ChipArena/StageLit is missing or unsupported");
                solid=new Material(shader){name="Stage art",enableInstancing=true};solid.SetFloat(GlowFlag,0);
                glow=new Material(shader){name="Stage art glow",enableInstancing=true};glow.SetFloat(GlowFlag,1);block=new MaterialPropertyBlock();
                bankName=name;mapId=map.id;plan=p;
                root=new GameObject("Stage art "+map.id);
                foreach(var c in p.Static)
                {
                    var cd=new ChunkDraw{Plan=c};bool any=false;
                    // a chunk wholly above the play space's ceiling (a roof over the hall) casts no shadow: it would shade the look's key
                    bool roof=map.bounds!=null&&map.bounds.ceiling>0&&c.Min.Y>=map.bounds.ceiling-.05f;
                    for(int t=0;t<NativeStageArt.Tiers;t++)
                    {
                        var piece=PieceOf(bank,c.Props[t]);if(piece==null)continue;
                        // a tier that shares the previous one's prop shares its renderer
                        if(t>0&&c.Props[t]==c.Props[t-1]&&cd.Tiers[t-1]!=null){cd.Tiers[t]=cd.Tiers[t-1];continue;}
                        var go=new GameObject("chunk "+c.Id+" "+t);go.transform.SetParent(root.transform,false);
                        go.AddComponent<MeshFilter>().sharedMesh=piece.Mesh;var r=go.AddComponent<MeshRenderer>();
                        r.sharedMaterials=piece.Subs>1?new[]{solid,glow}:new[]{solid};r.shadowCastingMode=roof?ShadowCastingMode.Off:ShadowCastingMode.TwoSided;r.receiveShadows=true;
                        r.lightProbeUsage=LightProbeUsage.Off;r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;r.enabled=false;cd.Tiers[t]=r;any=true;
                    }
                    if(!any)continue;cd.Box=new Bounds(new Vector3((c.Min.X+c.Max.X)*.5f,(c.Min.Y+c.Max.Y)*.5f,(c.Min.Z+c.Max.Z)*.5f),new Vector3(c.Max.X-c.Min.X,c.Max.Y-c.Min.Y,c.Max.Z-c.Min.Z));chunks.Add(cd);
                }
                var materials=new Dictionary<string,ContentMaterial>(StringComparer.Ordinal);
                foreach(var b in map.blocks??Array.Empty<ContentBlock>())
                {
                    if(b?.id==null||!p.Blocks.TryGetValue(b.id,out var props))continue;var bd=new BlockDraw{Tiers=new Piece[NativeStageArt.Tiers]};
                    for(int t=0;t<NativeStageArt.Tiers;t++)bd.Tiers[t]=PieceOf(bank,props[t]);bd.Max=NativeBlockLooks.StageMax(b,null,out bd.S1,out bd.S2);blocks[b.id]=bd;
                }
                rig=new Piece[rigSlots?.Length??0][];rigShadow=new bool[rig.Length];slotCount=new int[rig.Length];batches=new Batch[rig.Length*NativeStageArt.Tiers];instanced=SystemInfo.supportsInstancing;
                for(int s=0;s<rig.Length;s++){var props=p.RigProps(rigSlots[s]);rig[s]=new Piece[NativeStageArt.Tiers];rigShadow[s]=casts==null||casts(s);if(props!=null)for(int t=0;t<NativeStageArt.Tiers;t++)rig[s][t]=PieceOf(bank,props[t]);}
                probe=new GameObject("Stage art probe").AddComponent<ReflectionProbe>();probe.transform.SetParent(root.transform,false);
                probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
                probe.boxProjection=true;probe.hdr=true;probe.nearClipPlane=.3f;probe.farClipPlane=90;probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=ProbeSky??new Color(.02f,.02f,.035f);
                var all=new Bounds(cd0(chunks),Vector3.zero);foreach(var c in chunks)all.Encapsulate(c.Box);probe.transform.position=ProbeAt??new Vector3(all.center.x,Mathf.Clamp(all.center.y,1,8),all.center.z);
                probe.size=all.size;probe.center=all.center-probe.transform.position;probeFrame=-1;probeRendered=-1;
                // the meshes are on the GPU now (Runtime NativePackBanks keeps them): the bank's copies of their vertices go
                foreach(int m in built){var src=bank.Meshes[m];src.Positions=src.Normals=null;src.Uv0=src.Uv1=null;src.Colors=null;foreach(var sm in src.Submeshes)sm.Indices=Array.Empty<int>();}
                built.Clear();root.SetActive(false);return true;
            }
            catch(Exception e){Debug.LogWarning("Stage art disabled: "+e.Message);Release();failed=true;return false;}
        }
        private static Vector3 cd0(List<ChunkDraw> c)=>c.Count>0?c[0].Box.center:Vector3.zero;
        private Piece PieceOf(NativePackBank.Bank bank,int prop)
        {
            if(prop<0||prop>=bank.Props.Count)return null;var p=bank.Props[prop];if(p.Parts.Length==0)return null;var part=p.Parts[0];
            // (a mesh Unity let go after the bank's copies were freed cannot be made again: that piece is left out)
            Mesh mesh;try{mesh=NativePackBanks.Mesh(bankName??NativeStageArt.BankOf(mapId),part.Mesh);}catch(NullReferenceException){return null;}if(mesh==null)return null;built.Add(part.Mesh);
            return new Piece{Mesh=mesh,Place=NativePackBanks.Place(part.Place),Subs=mesh.subMeshCount};
        }

        /// <summary>One frame of the art: the chunks' tiers, the blocks' models, the rig. False when there is no art (the boxes
        /// draw). eye: the world eye; low: the low graphics tier.</summary>
        public bool Render(NativeArena arena,Camera eye,bool low,NativeStageArtPose pose)
        {
            if(!Ready||arena?.Map==null||arena.Map.id!=mapId)return false;
            if(!root.activeSelf)root.SetActive(true);lastFrame=Time.frameCount;activeMap=mapId;activeCovers=plan.Covers;activeFrame=Time.frameCount;
            Vector3 e=eye!=null?eye.transform.position:Vector3.zero;int fine=0,mid=0,lowN=0;Drawn=0;
            foreach(var c in chunks)
            {
                int t=NativeStageArt.TierAt(Mathf.Sqrt(c.Box.SqrDistance(e)),low);
                // a little hysteresis: a chunk keeps its finer tier until it is a metre past the reach
                if(c.Shown>=0&&c.Shown<t&&!low&&Mathf.Sqrt(c.Box.SqrDistance(e))<(c.Shown==0?NativeStageArt.FineReach:NativeStageArt.MidReach)+1)t=c.Shown;
                if(c.Shown!=t){for(int k=0;k<NativeStageArt.Tiers;k++)if(c.Tiers[k]!=null)c.Tiers[k].enabled=false;var r=c.Tiers[t]??c.Tiers[NativeStageArt.Mid]??c.Tiers[NativeStageArt.Fine];if(r!=null)r.enabled=true;c.Shown=t;}
                if(t==0)fine++;else if(t==1)mid++;else lowN++;
            }
            ChunksFine=fine;ChunksMid=mid;ChunksLow=lowN;
            // the breakable blocks' models where they stand
            var props=arena.Props;
            for(int i=0;i<props.Count;i++)
            {
                var b=props[i];if(b==null||b.Definition?.id==null||b.Fragment||!blocks.TryGetValue(b.Definition.id,out var bd))continue;
                int stage=NativeStageArt.Stage(b.Health,bd.Max,b.Indestructible,bd.S1,bd.S2);if(stage>NativeStageArt.Keep)continue;
                var t=b.transform;var at=t.position;int tier=NativeStageArt.TierAt(Vector3.Distance(at,e),low);var piece=bd.Tiers[tier];if(piece==null)continue;
                float dmg=stage==0?0:stage==1?.45f:.85f;block.Clear();block.SetVector(TintId,new Vector4(0,0,0,dmg));
                var m=Matrix4x4.TRS(at,t.rotation,Vector3.one)*piece.Place;
                for(int s=0;s<piece.Subs;s++)Graphics.DrawMesh(piece.Mesh,m,s==0?solid:glow,0,null,s,block,ShadowCastingMode.TwoSided,true);Drawn++;
            }
            // the rig
            if(pose!=null){Array.Clear(slotCount,0,slotCount.Length);for(int i=0;i<pose.Count;i++){int sl=pose.Slot[i];if(sl>=0&&sl<slotCount.Length)slotCount[sl]++;}}
            if(pose!=null)for(int i=0;i<pose.Count;i++)
            {
                int slot=pose.Slot[i];if(slot<0||slot>=rig.Length)continue;int o=i*12;var at=new Vector3(pose.Place[o+3],pose.Place[o+7],pose.Place[o+11]);
                int tier=NativeStageArt.TierAt(Vector3.Distance(at,e),low);var piece=rig[slot][tier];if(piece==null)continue;
                var m=new Matrix4x4();m.m00=pose.Place[o];m.m01=pose.Place[o+1];m.m02=pose.Place[o+2];m.m03=pose.Place[o+3];m.m10=pose.Place[o+4];m.m11=pose.Place[o+5];m.m12=pose.Place[o+6];m.m13=pose.Place[o+7];
                m.m20=pose.Place[o+8];m.m21=pose.Place[o+9];m.m22=pose.Place[o+10];m.m23=pose.Place[o+11];m.m33=1;m*=piece.Place;
                int k=i*4;block.Clear();block.SetVector(GlowId,new Vector4(pose.Tint[k],pose.Tint[k+1],pose.Tint[k+2],1));
                var shadow=rigShadow[slot]?ShadowCastingMode.On:ShadowCastingMode.Off;
                // a part drawn many times a frame (a crowd's figures) goes out instanced, in batches by slot and tier
                if(instanced&&slotCount[slot]>=InstanceFrom)
                {
                    var b=batches[slot*NativeStageArt.Tiers+tier]??=new Batch();if(b.Count<BatchSize){b.M[b.Count]=m;b.Glow[b.Count]=new Vector4(pose.Tint[k],pose.Tint[k+1],pose.Tint[k+2],1);b.Count++;b.Piece=piece;b.Shadow=shadow;Drawn++;continue;}
                }
                for(int s=0;s<piece.Subs;s++)Graphics.DrawMesh(piece.Mesh,m,s==0?solid:glow,0,null,s,block,s==0?shadow:ShadowCastingMode.Off,true);Drawn++;
            }
            for(int i=0;i<batches.Length;i++)
            {
                var b=batches[i];if(b==null||b.Count==0)continue;block.Clear();block.SetVectorArray(GlowId,b.Glow);
                for(int s=0;s<b.Piece.Subs;s++)Graphics.DrawMeshInstanced(b.Piece.Mesh,s,s==0?solid:glow,b.M,b.Count,block,s==0?b.Shadow:ShadowCastingMode.Off,true);
                b.Count=0;
            }
            Probe(low);
            return true;
        }
        // The reflection probe: rendered once, a few frames after the art first shows (the look applied, the glass and lights up),
        // and again when the graphics tier changes its size.
        private void Probe(bool low)
        {
            if(probe==null)return;int size=low?64:256;
            if(probeSize!=size){probeSize=size;probe.resolution=size;probeFrame=Time.frameCount+8;probeRendered=-1;}
            if(probeFrame<0){probeFrame=Time.frameCount+8;return;}
            if(probeRendered<0&&Time.frameCount>=probeFrame){probe.RenderProbe();probeRendered=Time.frameCount;}
        }
        private void LateUpdate()
        {
            // nothing drew the art last frame (another map, the menu): it hides, and the boxes come back
            if(root!=null&&root.activeSelf&&Time.frameCount-lastFrame>1){root.SetActive(false);foreach(var c in chunks)c.Shown=-1;foreach(var c in chunks)for(int k=0;k<NativeStageArt.Tiers;k++)if(c.Tiers[k]!=null)c.Tiers[k].enabled=false;}
        }
        private void Release()
        {
            if(root!=null)Destroy(root);root=null;probe=null;chunks.Clear();blocks.Clear();rig=Array.Empty<Piece[]>();plan=null;bankName=mapId=null;
            if(solid!=null)Destroy(solid);if(glow!=null)Destroy(glow);solid=glow=null;
        }
        private void OnDestroy()=>Release();
    }
}
