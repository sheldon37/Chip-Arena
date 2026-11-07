using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Pack dressing at runtime (docs/arenas/STAGE-KITS.md; rules in Core NativePackDressing and NativePackScenery): the table
    /// (Resources/PackDressing.json, read once), each map's plan against the kit bank (NativePackBanks.Kit), and the materials:
    /// one NativeBlockLit instance per bank material with the BLOCK_PACK keyword, its atlas, colour, emission gain and cutout.
    /// With no bank (CI, cloud builds, the public source zips, --ca-no-pack-banks), no table or no entry for the map, every plan
    /// is null and every caller keeps today's boxes, kits and backdrops.
    /// </summary>
    public static class NativePackStage
    {
        private static PackDressingSet set;private static bool setRead;
        private static ContentMap planMap;private static PackPlan plan;private static NativePackBank.Bank planBank;
        private static Material[] materials=Array.Empty<Material>();private static NativePackBank.Bank materialBank;
        private static readonly int TexId=Shader.PropertyToID("_PackTex"),ColorId=Shader.PropertyToID("_PackColor"),EmitId=Shader.PropertyToID("_PackEmit"),CutoffId=Shader.PropertyToID("_PackCutoff"),GlowId=Shader.PropertyToID("_ArenaPackGlow");
        public const string Keyword="BLOCK_PACK";
        /// <summary>The dressing table, validated (null when missing or malformed: today's look).</summary>
        public static PackDressingSet Set
        {
            get
            {
                if(setRead)return set;setRead=true;
                try{var asset=Resources.Load<TextAsset>(NativePackDressing.Resource);if(asset!=null){var s=JsonUtility.FromJson<PackDressingSet>(asset.text);NativePackDressing.Validate(s);set=s;}}
                catch(Exception e){Debug.LogWarning("Pack dressing disabled: "+e.Message);set=null;}
                return set;
            }
        }
        /// <summary>The kit bank the table names (null without the licensed-assets zip).</summary>
        public static NativePackBank.Bank Bank=>Set==null?null:NativePackBanks.Bank(Set.bank);
        /// <summary>The map's entry when the kit bank is present (a map without one never loads the bank).</summary>
        public static PackMapDressing Entry(string mapId){var e=NativePackDressing.Entry(Set,mapId);return e!=null&&Bank!=null?e:null;}
        /// <summary>The map's plan (cached per map content; null: every block keeps its box). A map without an entry never loads the bank.</summary>
        public static PackPlan Plan(ContentMap map)
        {
            if(map==null||NativePackDressing.Entry(Set,map.id)==null)return null;var bank=Bank;if(ReferenceEquals(map,planMap)&&ReferenceEquals(bank,planBank))return plan;
            planMap=map;planBank=bank;plan=null;
            try{plan=NativePackDressing.Plan(Set,map,bank);}catch(Exception e){Debug.LogWarning("Pack dressing for "+map.id+" disabled: "+e.Message);plan=null;}
            return plan;
        }
        /// <summary>The map's night level for the lights atlas (the shader's _ArenaPackGlow).</summary>
        public static void Glow(float level)=>Shader.SetGlobalFloat(GlowId,level);
        /// <summary>The material for a bank material (null when the shader is missing).</summary>
        public static Material MaterialFor(int index)
        {
            var bank=Bank;if(bank==null||index<0||index>=bank.Materials.Count)return null;
            if(!ReferenceEquals(bank,materialBank)){Release();materialBank=bank;materials=new Material[bank.Materials.Count];}
            if(materials[index]!=null)return materials[index];
            var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)return null;var bm=bank.Materials[index];
            var m=new Material(shader){name="Pack "+bm.Name,enableInstancing=true};m.EnableKeyword(Keyword);
            m.SetTexture(TexId,NativePackBanks.Texture(Set.bank,bm.Albedo)??Texture2D.whiteTexture);
            // The albedo colour at most 1 (the lights atlas carries its HDR gain as emission); glass drawn opaque and dark.
            bool glass=(bm.Flags&(NativePackBank.MatGlass|NativePackBank.MatTransparent))!=0;
            var c=new Color(Mathf.Min(1,bm.Color.X),Mathf.Min(1,bm.Color.Y),Mathf.Min(1,bm.Color.Z),1);if(glass)c=new Color(c.r*.22f,c.g*.26f,c.b*.3f,1);
            m.SetColor(ColorId,QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c);
            m.SetFloat(EmitId,bm.Emission>=0?Mathf.Max(bm.EmissionColor.X,Mathf.Max(bm.EmissionColor.Y,bm.EmissionColor.Z)):0);
            m.SetFloat(CutoffId,(bm.Flags&NativePackBank.MatCutout)!=0?.5f:-1);
            return materials[index]=m;
        }
        public static void Release(){foreach(var m in materials)if(m!=null)CapturedAnimation.Release(m);materials=Array.Empty<Material>();materialBank=null;}

        /// <summary>Per kit part of a map's dressing kit: whether it stops drawing because its block draws as props (Core
        /// NativePackDressing.Covers; world-space parts). Null when nothing is dressed.</summary>
        public static bool[] Covered(ContentMap map,int count,Func<int,int> parent,Func<int,(Vector3 at,Vector3 half,Quaternion q)> part,bool local)
        {
            var p=Plan(map);if(p==null||p.Dressed==0||map.blocks==null)return null;var o=new bool[count];
            for(int i=0;i<count;i++)
            {
                int k=parent(i);var b=k>=0&&k<map.blocks.Length?map.blocks[k]:null;if(b==null||p.ByBlock[k]==null)continue;var s=part(i);
                o[i]=local?NativePackDressing.CoversLocal(b,s.at.x,s.at.y,s.at.z,s.half.x,s.half.y,s.half.z,s.q.x,s.q.y,s.q.z,s.q.w):NativePackDressing.Covers(b,s.at.x,s.at.y,s.at.z,s.half.x,s.half.y,s.half.z,s.q.x,s.q.y,s.q.z,s.q.w);
            }
            return o;
        }
        /// <summary>The Factory kit's parts (no parent: judged against every dressed block) with the dressed blocks' own parts removed in place.</summary>
        public static void Strip(ContentMap map,NativeFactoryKit.Writer parts)
        {
            var p=Plan(map);if(p==null||p.Dressed==0||parts==null)return;int w=0;
            for(int i=0;i<parts.Count;i++)
            {
                ref var q=ref parts.Items[i];bool covered=false;
                foreach(var d in p.ById.Values){var b=map.blocks[d.Block];double dx=q.X-b.pos.x,dz=q.Z-b.pos.z,r=Math.Max(b.size.x,b.size.z)+NativePackDressing.Proud+Math.Max(q.Hx,q.Hz)*2;if(dx*dx+dz*dz>r*r)continue;
                    if(NativePackDressing.Covers(b,q.X,q.Y,q.Z,q.Hx,q.Hy,q.Hz,q.Qx,q.Qy,q.Qz,q.Qw)){covered=true;break;}}
                if(!covered)parts.Items[w++]=q;
            }
            parts.Count=w;
        }
    }

    /// <summary>
    /// The frame's pack props in blocks' places (NativeArenaDressing): pooled instanced batches per bank mesh, refilled each
    /// frame and drawn per submesh with its material (casting shadows, as the boxes do). No allocation once warm.
    /// </summary>
    public sealed class NativePackDraws
    {
        private sealed class Chunk {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Rest=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk];}
        private sealed class Batch {public readonly List<Chunk> Chunks=new List<Chunk>();public int Count;}
        private Batch[] byMesh=Array.Empty<Batch>();private readonly List<int> used=new List<int>();private NativePackBank.Bank bank;private MaterialPropertyBlock block;
        private static readonly int RestId=Shader.PropertyToID("_BlockRest"),LookId=Shader.PropertyToID("_BlockLook"),ColorId=Shader.PropertyToID("_BlockColor");
        private static readonly Vector4 White=new Vector4(1,1,1,1);
        public int Draws{get;private set;}public int Instances{get;private set;}
        public void Begin(){foreach(int m in used)byMesh[m].Count=0;used.Clear();Draws=Instances=0;}
        /// <summary>One bank mesh at a world matrix: rest y the prop's base height, w its dust; look y health, z seed, w flags.</summary>
        public void Add(NativePackBank.Bank b,int mesh,Matrix4x4 world,Vector4 rest,Vector4 look)
        {
            if(b==null)return;if(!ReferenceEquals(b,bank)){bank=b;byMesh=new Batch[b.Meshes.Count];used.Clear();}
            var batch=byMesh[mesh]??=new Batch();if(batch.Count==0)used.Add(mesh);int c=batch.Count/NativeBlockLooks.Chunk,i=batch.Count%NativeBlockLooks.Chunk;
            if(c>=batch.Chunks.Count)batch.Chunks.Add(new Chunk());var k=batch.Chunks[c];k.M[i]=world;k.Rest[i]=rest;k.Look[i]=look;k.Color[i]=White;batch.Count++;
        }
        public void Flush(bool instancing)
        {
            if(bank==null||used.Count==0)return;block??=new MaterialPropertyBlock();
            foreach(int id in used)
            {
                var mesh=NativePackBanks.Mesh(NativePackStage.Set.bank,id);var src=bank.Meshes[id];var batch=byMesh[id];if(mesh==null)continue;
                for(int s=0;s<src.Submeshes.Length;s++)
                {
                    var mat=NativePackStage.MaterialFor(src.Submeshes[s].Material);if(mat==null)continue;
                    for(int start=0,ci=0;start<batch.Count;start+=NativeBlockLooks.Chunk,ci++)
                    {
                        var k=batch.Chunks[ci];int n=Math.Min(NativeBlockLooks.Chunk,batch.Count-start);Draws++;Instances+=n;
                        if(instancing)
                        {
                            block.Clear();block.SetVectorArray(RestId,k.Rest);block.SetVectorArray(LookId,k.Look);block.SetVectorArray(ColorId,k.Color);
                            try{Graphics.DrawMeshInstanced(mesh,s,mat,k.M,n,block,ShadowCastingMode.On,true,0,null);continue;}
                            catch(InvalidOperationException){instancing=false;}catch(ArgumentException){instancing=false;}
                        }
                        for(int i=0;i<n;i++){block.Clear();block.SetVector(RestId,k.Rest[i]);block.SetVector(LookId,k.Look[i]);block.SetVector(ColorId,k.Color[i]);Graphics.DrawMesh(mesh,k.M[i],mat,0,null,s,block,ShadowCastingMode.On,true);}
                    }
                }
            }
        }
    }

    /// <summary>
    /// A map's harbour yards (Core NativePackScenery) as drawn meshes: the bank props baked into one world-space mesh per bank
    /// material and tier, the ground boxes as SourceLit sector meshes, all on NativeSceneryBuilder.BackdropLayer without shadows,
    /// the detail tier left out on the low tier. Built once per map content; nothing without the bank or yards.
    /// </summary>
    public sealed class NativePackYards
    {
        private ContentMap map;private readonly List<(Mesh mesh,int material,bool detail,bool lowOnly)> props=new List<(Mesh,int,bool,bool)>();private readonly List<(Mesh mesh,bool detail)> ground=new List<(Mesh,bool)>();
        private Material groundLit;private MaterialPropertyBlock block;private float glow,flood;private string mapId;private bool failed;
        private static readonly int RestId=Shader.PropertyToID("_BlockRest"),LookId=Shader.PropertyToID("_BlockLook"),ColorId=Shader.PropertyToID("_BlockColor");
        public int Draws{get;private set;}
        /// <summary>Builds for a map (by content, or by id for the menu's backdrop, which has no arena).</summary>
        private void Bind(ContentMap content,string id)
        {
            if(ReferenceEquals(content,map)&&id==mapId)return;Clear();map=content;mapId=id;failed=false;
            var entry=NativePackStage.Entry(id);var bank=NativePackStage.Bank;if(entry?.yards==null||entry.yards.Length==0||bank==null||content==null)return;
            try
            {
                var scene=NativePackScenery.Build(entry,content,bank);glow=entry.glow;flood=entry.flood;
                foreach(var b in NativePackScenery.Bake(bank,scene.Props))props.Add((MeshOf(b),b.Material,b.Detail,b.LowOnly));
                for(int g=0;g<scene.Ground.Length;g++)if(scene.Ground[g].Positions.Length>0)ground.Add((SceneryMesh(scene.Ground[g],"Pack yard ground "+g),g==NativePackScenery.GroundDetail));
                var source=Resources.Load<Shader>("NativeSourceLit");if(source!=null&&ground.Count>0){groundLit=new Material(source){name="Pack yard ground",enableInstancing=true};groundLit.SetFloat("_StageLit",0);groundLit.SetFloat("_ToonEdgeRef",16);}
            }
            catch(Exception e){Debug.LogWarning("Pack yards for "+id+" disabled: "+e.Message);Clear();failed=true;}
        }
        /// <summary>Builds the map's yards now (the stage loading screen's prewarm), drawing nothing.</summary>
        public void Prepare(ContentMap content,string id)=>Bind(content,id);
        /// <summary>Draws the map's yards this frame (low: the detail tier left out, its lighter stand-ins in).</summary>
        public void Render(ContentMap content,string id,bool low)
        {
            Draws=0;Bind(content,id);if(failed||props.Count==0&&ground.Count==0)return;const int layer=NativeSceneryBuilder.BackdropLayer;
            NativePackStage.Glow(glow);block??=new MaterialPropertyBlock();block.Clear();block.SetVector(RestId,new Vector4(flood,0,0,0));block.SetVector(LookId,new Vector4(0,1,0,0));block.SetVector(ColorId,new Vector4(1,1,1,1));
            foreach(var p in props){if(p.detail&&low||p.lowOnly&&!low)continue;var m=NativePackStage.MaterialFor(p.material);if(m==null)continue;Graphics.DrawMesh(p.mesh,Matrix4x4.identity,m,layer,null,0,block,ShadowCastingMode.Off,true);Draws++;}
            if(groundLit!=null)foreach(var g in ground){if(g.detail&&low)continue;Graphics.DrawMesh(g.mesh,Matrix4x4.identity,groundLit,layer,null,0,null,ShadowCastingMode.Off,true);Draws++;}
        }
        private static Mesh MeshOf(PackBakedMesh b)
        {
            int n=b.Vertices;var p=new Vector3[n];var nr=new Vector3[n];var uv=new Vector2[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(b.Positions[i*3],b.Positions[i*3+1],b.Positions[i*3+2]);nr[i]=new Vector3(b.Normals[i*3],b.Normals[i*3+1],b.Normals[i*3+2]);uv[i]=new Vector2(b.Uv[i*2],b.Uv[i*2+1]);}
            var mesh=new Mesh{name="Pack yard "+b.Material+(b.Detail?" detail":""),indexFormat=IndexFormat.UInt32};mesh.vertices=p;mesh.normals=nr;mesh.uv=uv;mesh.triangles=b.Indices;
            mesh.bounds=new Bounds(new Vector3((b.MinX+b.MaxX)/2,(b.MinY+b.MaxY)/2,(b.MinZ+b.MaxZ)/2),new Vector3(b.MaxX-b.MinX,b.MaxY-b.MinY,b.MaxZ-b.MinZ));mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh SceneryMesh(NativeSceneryMesh m,string name)
        {
            int n=m.Vertices;var p=new Vector3[n];var nr=new Vector3[n];var c=new Color[n];var e=new Vector4[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);nr[i]=new Vector3(m.Normals[i*3],m.Normals[i*3+1],m.Normals[i*3+2]);c[i]=new Color(m.Colors[i*4],m.Colors[i*4+1],m.Colors[i*4+2],m.Colors[i*4+3]);e[i]=new Vector4(m.Emission[i*4],m.Emission[i*4+1],m.Emission[i*4+2],m.Emission[i*4+3]);}
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=p;mesh.normals=nr;mesh.colors=c;mesh.SetUVs(1,e);mesh.triangles=m.Indices;
            mesh.bounds=new Bounds(new Vector3((m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2),new Vector3(m.MaxX-m.MinX,m.MaxY-m.MinY,m.MaxZ-m.MinZ));mesh.UploadMeshData(true);return mesh;
        }
        public void Clear()
        {
            foreach(var p in props)CapturedAnimation.Release(p.mesh);foreach(var g in ground)CapturedAnimation.Release(g.mesh);props.Clear();ground.Clear();
            if(groundLit!=null)CapturedAnimation.Release(groundLit);groundLit=null;map=null;mapId=null;
        }
    }

    public sealed partial class ArenaPresentation
    {
        private NativePackYards packYards;
        // SourceWorld (the arena and the killcam's replay arena): the map's harbour yards, when the bank and the table have any.
        private void PackYards(NativeArena arena){if(arena?.Map==null||NativePackStage.Entry(arena.MapId)?.yards==null)return;(packYards??=new NativePackYards()).Render(arena.Map,arena.MapId,LowGraphics);}
        // RenderBackdrop: behind the main menu (the map content by id; nothing without yards).
        private void PackBackdrop(string mapId)
        {
            if(NativePackStage.Entry(mapId)?.yards==null)return;var content=NativeLighting.CurrentMap;if(content==null||content.id!=mapId)return;
            (packYards??=new NativePackYards()).Render(content,mapId,LowGraphics);
        }
    }
}
