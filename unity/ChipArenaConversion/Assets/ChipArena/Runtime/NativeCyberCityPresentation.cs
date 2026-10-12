using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Cyber City's presentation (docs/arenas/CYBERCITY_PROPOSAL.md §8-§9), drawn only while the map id is "cybercity"
    /// (NativeCyberCityLooks.Active): in the arena and the killcam replay (SourceWorld, on the arena's presentation clock, so the
    /// hazard rigs, traffic and rain match on host, guests and in the killcam) and behind the main menu (RenderBackdrop, on the
    /// render clock: the skyline and its life only). Every other map is untouched, and nothing runs while intros, previews or the
    /// chip draft own the screen, as none of them reach SourceWorld.
    ///  - Dressing kit (Core NativeCyberCityKit): parts posed with their parent props from arena.AllBlocks each frame (a part of a
    ///    block that left goes with it), driven by the hazard states (signals, coils, edge strips, SAFE lights, coupling rails and
    ///    lamps), hidden where they sit between the camera and the pilot (§5.5); instanced NativeBlockLit draws of 500 on layer 0,
    ///    big parts casting shadows, trims not.
    ///  - The maglev train (the kit's train rig), posed from the hazard's head position while a pass is near.
    ///  - Backdrop (Core NativeCyberCityScenery): static instanced NativeBlockLit batches on NativeSceneryBuilder.BackdropLayer, no
    ///    shadows cast. Behind the menu a plate fills the slab.
    ///  - Atmosphere and hazard FX (Core NativeCyberCityFx) through one NativeFxDraw: a solid and an effects batch.
    /// Hazards: the map's `hazards` in UnityMaps.json (CYBERCITY-SIMULATION-CONTRACT.md §3), read once by the map id; absent or
    /// empty, every hazard rig rests (steady looks, no telegraph FX). Their state is the pure Core function of (round index,
    /// FightAt, presentation clock): NativeCyberCityHazards.Evaluate, the timing the contract pins. Nothing here writes sim data,
    /// collides, damages or enters the network frame. Allocation free after the one-time builds.
    /// </summary>
    /// <summary>
    /// A map's `hazards` from Resources/UnityMaps.json (CYBERCITY-SIMULATION-CONTRACT.md §3), read once per map id for the
    /// presentation and the sound until the content loader carries them (ContentMap.hazards); empty when the map or its hazards
    /// are absent, which leaves every hazard rig at rest.
    /// </summary>
    internal static class NativeCyberCityHazardSource
    {
        [Serializable] private sealed class File{public Entry[] maps;}
        [Serializable] private sealed class Entry{public string id;public MapHazard[] hazards;}
        private static readonly Dictionary<string,MapHazard[]> cache=new Dictionary<string,MapHazard[]>();
        public static MapHazard[] For(string mapId)
        {
            if(mapId==null)return Array.Empty<MapHazard>();if(cache.TryGetValue(mapId,out var known))return known;var found=Array.Empty<MapHazard>();
            var asset=Resources.Load<TextAsset>("UnityMaps");var file=asset!=null?JsonUtility.FromJson<File>(asset.text):null;
            if(file?.maps!=null)foreach(var m in file.maps)if(m!=null&&m.id==mapId){found=m.hazards??Array.Empty<MapHazard>();break;}
            cache[mapId]=found;return found;
        }
    }
    public sealed partial class ArenaPresentation
    {
        private sealed class CyberBatch{public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();}
        private Material cyberMaterial;private Mesh cyberMesh;private bool cyberFailed;private NativeFxDraw cyberDraw;private NativeCyberCityFx cyberFx;private readonly NativeFxLight cyberLight=new NativeFxLight();
        private NativeCyberBox[] cyberBackdrop;private readonly List<CyberBatch> cyberBack=new List<CyberBatch>(),cyberCast=new List<CyberBatch>(),cyberTrim=new List<CyberBatch>();private int cyberCastUsed,cyberTrimUsed;
        private NativeCyberCityKit cyberKit;private NativeCyberFxScene cyberScene;private MapHazard[] cyberHazards;private NativeHazardState[] cyberStates=Array.Empty<NativeHazardState>();
        private NativePropBody[] cyberParents=Array.Empty<NativePropBody>();private float[] cyberSeeds=Array.Empty<float>();private readonly MaterialPropertyBlock cyberOne=new MaterialPropertyBlock();private readonly Dictionary<string,int> cyberIndex=new Dictionary<string,int>();
        private NativeCyberScreen[] cyberScreens=new NativeCyberScreen[4];private int[] cyberScreenBlock=Array.Empty<int>();private string[] cyberScreenSegment=Array.Empty<string>();
        private NativeArena cyberArena;private int cyberGeneration=-1;private Mesh cyberPlate;private Matrix4x4 cyberPlateMatrix;
        private static readonly int CyberSizeId=Shader.PropertyToID("_BlockSize"),CyberRestId=Shader.PropertyToID("_BlockRest"),CyberColorId=Shader.PropertyToID("_BlockColor"),CyberLookId=Shader.PropertyToID("_BlockLook");

        // SourceWorld: the arena or the killcam replay arena.
        private void CyberCityScenery(NativeArena arena)
        {
            if(!NativeCyberCityLooks.Active(arena.MapId)||!EnsureCyberCity())return;
            if(!ReferenceEquals(arena,cyberArena)||arena.Generation!=cyberGeneration)CyberRebind(arena);
            float now=PresentationClock(arena);var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            for(int i=0;i<cyberStates.Length;i++)cyberStates[i]=NativeCyberCityHazards.At(cyberHazards[i],match.Phase,round,match.FightAt,now);
            CyberParents(arena);DrawCyberKit(arena,now);DrawCyberBackdrop(false);
            int screens=CyberBillboards(arena);DrawCyberFx(now,WorldEye,true,screens);
        }
        // RenderBackdrop: behind the main menu, on the render clock; the skyline, its life and a plate over the slab only.
        private void CyberCityBackdrop(string mapId,float time,Camera camera)
        {
            if(!NativeCyberCityLooks.Active(mapId)||!EnsureCyberCity())return;
            if(cyberScene==null)cyberScene=NativeCyberFxScene.Build(null,null,null,cyberBackdrop);
            NativeLighting.ArenaRim();DrawCyberBackdrop(true);DrawCyberFx(time,camera,false,0);
        }
        private bool EnsureCyberCity()
        {
            if(cyberMaterial!=null)return true;if(cyberFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                cyberMaterial=new Material(shader){name="Cyber City dressing",enableInstancing=true};cyberMesh=CyberMeshOf(NativeBlockLooks.Build(0));
                cyberBackdrop=NativeCyberCityScenery.Build();cyberDraw=new NativeFxDraw();cyberFx=new NativeCyberCityFx();
                for(int i=0;i<cyberBackdrop.Length;i++){var b=cyberBackdrop[i];if(i%NativeBlockLooks.Chunk==0)cyberBack.Add(new CyberBatch());var bt=cyberBack[cyberBack.Count-1];
                    var lin=CyberLinear(b.Color)*b.Gain;Put(bt,Matrix4x4.TRS(new Vector3(b.X,b.Y,b.Z),Quaternion.Euler(0,b.Yaw,0),Vector3.one),new Vector4(b.Hx,b.Hy,b.Hz,NativeBlockLooks.Chamfer),new Vector4(b.X,b.Y,b.Z,0),new Vector4(lin.r,lin.g,lin.b,1),new Vector4(b.Look,1,NativeBlockLooks.Seed("cyber-backdrop-"+i),b.Flags));}
                foreach(var bt in cyberBack)CyberUpload(bt);
                cyberPlate=cyberMesh;cyberPlateMatrix=Matrix4x4.TRS(new Vector3(0,-.25f,0),Quaternion.identity,Vector3.one);return true;
            }
            catch(Exception e){Debug.LogWarning("Cyber City presentation disabled: "+e.Message);DisposeCyberCity();cyberFailed=true;return false;}
        }
        private void CyberRebind(NativeArena arena)
        {
            cyberArena=arena;cyberGeneration=arena.Generation;var map=arena.Map;if(cyberHazards==null)cyberHazards=NativeCyberCityHazardSource.For(arena.MapId);
            cyberKit=NativeCyberCityKit.Build(map.blocks,cyberHazards);cyberScene=NativeCyberFxScene.Build(map.blocks,cyberHazards,cyberKit,cyberBackdrop);
            cyberStates=new NativeHazardState[cyberHazards.Length];cyberSeeds=new float[cyberKit.Parts.Length];for(int i=0;i<cyberSeeds.Length;i++)cyberSeeds[i]=NativeBlockLooks.Seed(map.blocks[cyberKit.Parts[i].Parent].id+"#"+i);cyberParents=new NativePropBody[map.blocks.Length];cyberIndex.Clear();for(int i=0;i<map.blocks.Length;i++)if(map.blocks[i]?.id!=null)cyberIndex[map.blocks[i].id]=i;
            // The billboards: each segment's screen block (the presentation's holo content follows it down).
            var screens=new List<int>();var segments=new List<string>();
            foreach(var s in map.segments??Array.Empty<StructureDefinition>())foreach(var id in s.blocks??Array.Empty<string>())if(id.EndsWith("-screen",StringComparison.Ordinal)&&cyberIndex.TryGetValue(id,out int k)){screens.Add(k);segments.Add(s.id);}
            cyberScreenBlock=screens.ToArray();cyberScreenSegment=segments.ToArray();if(cyberScreens.Length<cyberScreenBlock.Length)cyberScreens=new NativeCyberScreen[cyberScreenBlock.Length];
        }
        // Each block's live prop this frame (null once it broke, or before rubble swaps in).
        private void CyberParents(NativeArena arena)
        {
            Array.Clear(cyberParents,0,cyberParents.Length);
            foreach(var p in arena.AllBlocks){if(p==null||p.Definition?.id==null)continue;if(cyberIndex.TryGetValue(p.Definition.id,out int i))cyberParents[i]=p;}
        }
        private int CyberBillboards(NativeArena arena)
        {
            int n=0;
            for(int i=0;i<cyberScreenBlock.Length;i++)
            {
                var p=cyberParents[cyberScreenBlock[i]];int state=0;float t0=0;
                if(arena.Structures!=null){var f=arena.Structures.Get(cyberScreenSegment[i]);state=f.state;t0=f.t0;}
                if(p==null&&state<3)continue;var b=arena.Map.blocks[cyberScreenBlock[i]];var t=p!=null?p.transform:null;
                Vector3 c=t!=null?t.position:new Vector3(b.pos.x,.25f,b.pos.z),u=t!=null?t.right*(b.size.x/2):new Vector3(b.size.x/2,0,0),v=t!=null?t.up*(b.size.y/2):new Vector3(0,0,b.pos.z>0?-b.size.y/2:b.size.y/2);
                Vector3 normal=t!=null?t.forward*(b.pos.z>0?-1:1):Vector3.up;c+=normal*(t!=null?b.size.z/2+.02f:.27f);
                cyberScreens[n++]=new NativeCyberScreen{X=c.x,Y=c.y,Z=c.z,Ux=u.x,Uy=u.y,Uz=u.z,Vx=v.x,Vy=v.y,Vz=v.z,Nx=normal.x,Ny=normal.y,Nz=normal.z,Seed=cyberScreenSegment[i].Length*97,State=state,T0=t0,Live=true};
            }
            return n;
        }
        private void DrawCyberKit(NativeArena arena,float now)
        {
            cyberCastUsed=cyberTrimUsed=0;var eye=WorldEye;Vector3 from=eye!=null?eye.transform.position:Vector3.zero,to=from;bool fade=eye!=null&&!FirstPerson&&arena.ViewPilot?.Body!=null;
            if(fade)to=arena.ViewPilot.Body.position+Vector3.up*.9f;
            var parts=cyberKit.Parts;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];var prop=cyberParents[part.Parent];if(prop==null)continue;var t=prop.transform;Quaternion q=t.rotation;Vector3 at=t.position+q*new Vector3(part.X,part.Y,part.Z);
                if(fade&&part.Fade&&CyberBetween(from,to,at,new Vector3(part.Hx+.2f,part.Hy+.2f,part.Hz+.2f)))continue;
                NativeCyberCityKit.Drive(part,cyberStates,now,out float r,out float g,out float bl,out float level);var rest=arena.Map.blocks[part.Parent].pos;
                CyberAdd(part.Shadow,Matrix4x4.TRS(at,q,Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(rest.x+part.X,rest.y+part.Y,rest.z+part.Z,0),CyberColor(r,g,bl,level),new Vector4(part.Look,1,cyberSeeds[i],part.Flags));
            }
            if(cyberKit.Maglev>=0&&cyberKit.Maglev<cyberStates.Length)
            {
                var s=cyberStates[cyberKit.Maglev];
                if(s.Phase!=NativeHazardPhase.Idle&&!double.IsNaN(s.Head))
                {
                    var q=s.Direction>0?Quaternion.identity:Quaternion.Euler(0,180,0);float track=cyberHazards[cyberKit.Maglev].center.z;
                    foreach(var part in cyberKit.Train)
                    {
                        NativeCyberCityKit.TrainPlace(part,s.Head,s.Direction,track,out float x,out float y,out float z);if(Math.Abs(x)>125)continue;var c=CyberLinear(part.Color)*part.Gain;
                        CyberAdd(part.Shadow,Matrix4x4.TRS(new Vector3(x,y,z),q,Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(part.X,part.Y,part.Z,0),new Vector4(c.r,c.g,c.b,1),new Vector4(part.Look,1,.37f,part.Flags));
                    }
                }
            }
            for(int i=0;i<cyberCastUsed;i++)CyberDraw(cyberCast[i],0,ShadowCastingMode.On);for(int i=0;i<cyberTrimUsed;i++)CyberDraw(cyberTrim[i],0,ShadowCastingMode.Off);
        }
        // Whether a box (centre, half extents) touches the segment from the eye to the pilot (slab test).
        private static bool CyberBetween(Vector3 a,Vector3 b,Vector3 c,Vector3 h)
        {
            float lo=0,hi=1;
            for(int k=0;k<3;k++){float o=a[k],d=b[k]-a[k],min=c[k]-h[k],max=c[k]+h[k];if(Mathf.Abs(d)<1e-6f){if(o<min||o>max)return false;continue;}float t0=(min-o)/d,t1=(max-o)/d;if(t0>t1){float x=t0;t0=t1;t1=x;}lo=Mathf.Max(lo,t0);hi=Mathf.Min(hi,t1);if(lo>hi)return false;}
            return true;
        }
        private void DrawCyberBackdrop(bool menu)
        {
            const int layer=NativeSceneryBuilder.BackdropLayer;foreach(var bt in cyberBack)CyberDraw(bt,layer,ShadowCastingMode.Off);
            if(!menu)return;
            // Behind the menu (no arena blocks there yet): a wet plate over the slab, and the look's ground haze (in a match the
            // arena dressing publishes it every frame).
            if(cyberPlateBlock==null){cyberPlateBlock=new MaterialPropertyBlock();var lin=CyberLinear(0x22232A);cyberPlateBlock.SetVector(CyberSizeId,new Vector4(40,.25f,32,NativeBlockLooks.Chamfer));cyberPlateBlock.SetVector(CyberRestId,Vector4.zero);cyberPlateBlock.SetVector(CyberColorId,new Vector4(lin.r,lin.g,lin.b,1));cyberPlateBlock.SetVector(CyberLookId,new Vector4(NativeCyberCityLooks.Road,1,.5f,NativeCyberCityLooks.Bank));}
            var look=NativeMapLooks.Find(NativeLighting.LookSet(),NativeCyberCityLooks.MapId)?.blocks;
            if(look!=null){var h=CyberLinear(look.hazeColor);Shader.SetGlobalVector(CyberHazeId,new Vector4(h.r,h.g,h.b,look.haze));Shader.SetGlobalFloat(CyberHazeTopId,look.hazeTop>0?look.hazeTop:1);Shader.SetGlobalFloat(CyberDustId,look.dust);}
            Graphics.DrawMesh(cyberPlate,cyberPlateMatrix,cyberMaterial,layer,null,0,cyberPlateBlock,ShadowCastingMode.Off,true);
        }
        private MaterialPropertyBlock cyberPlateBlock;
        private static readonly int CyberHazeId=Shader.PropertyToID("_ArenaHaze"),CyberHazeTopId=Shader.PropertyToID("_ArenaHazeTop"),CyberDustId=Shader.PropertyToID("_ArenaDust");
        private void DrawCyberFx(float time,Camera camera,bool atmosphere,int screens)
        {
            if(camera==null)return;ReadCyberLight();var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            cyberFx.Build(cyberScene,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,cyberLight,Mathf.Clamp01(EffectsQuality),LowGraphics,atmosphere,atmosphere?cyberStates:null,cyberScreens,screens);
            var q=cyberFx.Quads;cyberDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?cyberDraw.Solid:cyberDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), so fog blends in linear light.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            cyberDraw.Draw(camera,null,time);
        }
        // The scene's key light and trilight ambient in linear values, as Unity lights the arena around these pieces.
        private void ReadCyberLight()
        {
            var sun=RenderSettings.sun;var l=cyberLight;
            if(sun!=null&&sun.isActiveAndEnabled){var d=-sun.transform.forward;Color c=GraphicsSettings.lightsUseLinearIntensity?sun.color.linear*sun.intensity:(sun.color*sun.intensity).linear;l.Lx=d.x;l.Ly=d.y;l.Lz=d.z;l.Sr=c.r;l.Sg=c.g;l.Sb=c.b;}
            else{l.Lx=0;l.Ly=1;l.Lz=0;l.Sr=l.Sg=l.Sb=0;}
            bool tri=RenderSettings.ambientMode==AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,eq=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,gr=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            l.Ambient[0]=sky.r;l.Ambient[1]=sky.g;l.Ambient[2]=sky.b;l.Ambient[3]=eq.r;l.Ambient[4]=eq.g;l.Ambient[5]=eq.b;l.Ambient[6]=gr.r;l.Ambient[7]=gr.g;l.Ambient[8]=gr.b;
        }
        private static Color CyberLinear(int hex){var c=NativeArena.ColorOf(hex);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
        // A drive's linear colour (already linear, gain folded in) for the active colour space, its light level in w.
        private static Vector4 CyberColor(float r,float g,float b,float level){if(QualitySettings.activeColorSpace!=ColorSpace.Linear){var c=new Color(r,g,b).gamma;return new Vector4(c.r,c.g,c.b,level);}return new Vector4(r,g,b,level);}
        private void CyberAdd(bool shadow,Matrix4x4 m,Vector4 size,Vector4 rest,Vector4 color,Vector4 look)
        {
            var list=shadow?cyberCast:cyberTrim;ref int used=ref shadow?ref cyberCastUsed:ref cyberTrimUsed;
            if(used==0||list[used-1].Count==NativeBlockLooks.Chunk){if(used==list.Count)list.Add(new CyberBatch());list[used].Count=0;used++;}
            Put(list[used-1],m,size,rest,color,look);
        }
        private static void Put(CyberBatch b,Matrix4x4 m,Vector4 size,Vector4 rest,Vector4 color,Vector4 look){int i=b.Count++;b.M[i]=m;b.Size[i]=size;b.Rest[i]=rest;b.Color[i]=color;b.Look[i]=look;}
        private static void CyberUpload(CyberBatch b){b.Block.Clear();b.Block.SetVectorArray(CyberSizeId,b.Size);b.Block.SetVectorArray(CyberRestId,b.Rest);b.Block.SetVectorArray(CyberColorId,b.Color);b.Block.SetVectorArray(CyberLookId,b.Look);}
        private void CyberDraw(CyberBatch b,int layer,ShadowCastingMode shadows)
        {
            if(b.Count==0)return;
            // Static backdrop batches keep the arrays they were uploaded with; kit batches upload this frame's.
            if(layer==0)CyberUpload(b);
            if(UsesInstancing){Graphics.DrawMeshInstanced(cyberMesh,0,cyberMaterial,b.M,b.Count,b.Block,shadows,true,layer,null);return;}
            // Compatibility rendering: the kit one by one; the backdrop (thousands of boxes) is left out.
            if(layer!=0)return;var one=cyberOne;
            for(int i=0;i<b.Count;i++){one.Clear();one.SetVector(CyberSizeId,b.Size[i]);one.SetVector(CyberRestId,b.Rest[i]);one.SetVector(CyberColorId,b.Color[i]);one.SetVector(CyberLookId,b.Look[i]);Graphics.DrawMesh(cyberMesh,b.M[i],cyberMaterial,0,null,0,one,shadows,true);}
        }
        // The stage-0 block mesh as NativeArenaDressing builds it: the preview cube in POSITION, the lattice in TEXCOORD0/1, the
        // baked shade in COLOR; bounds that cover any block (the shader moves the vertices out to the instance's size).
        private static Mesh CyberMeshOf(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new List<Vector4>(n);var b=new List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Cyber City block mesh"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2000);mesh.UploadMeshData(true);return mesh;
        }
        private void DisposeCyberCity()
        {
            CapturedAnimation.Release(cyberMesh);CapturedAnimation.Release(cyberMaterial);cyberMesh=null;cyberMaterial=null;cyberPlate=null;cyberDraw?.Dispose();cyberDraw=null;
            cyberBack.Clear();cyberCast.Clear();cyberTrim.Clear();cyberKit=null;cyberScene=null;cyberArena=null;cyberGeneration=-1;cyberHazards=null;cyberPlateBlock=null;
        }
    }
}
