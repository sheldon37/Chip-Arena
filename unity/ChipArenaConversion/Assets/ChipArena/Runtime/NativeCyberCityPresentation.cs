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
        private sealed class MetroBatch{public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();}
        private Material metroMaterial;private Mesh metroMesh;private bool metroFailed;private NativeFxDraw metroDraw;private NativeCyberCityFx metroFx;private readonly NativeFxLight metroLight=new NativeFxLight();
        private NativeCyberBox[] metroBackdrop;private readonly List<MetroBatch> metroBack=new List<MetroBatch>(),metroCast=new List<MetroBatch>(),metroTrim=new List<MetroBatch>();private int metroCastUsed,metroTrimUsed;
        private NativeCyberCityKit metroKit;private NativeCyberFxScene metroScene;private MapHazard[] metroHazards;private NativeHazardState[] metroStates=Array.Empty<NativeHazardState>();
        private NativePropBody[] metroParents=Array.Empty<NativePropBody>();private float[] metroSeeds=Array.Empty<float>();private readonly MaterialPropertyBlock metroOne=new MaterialPropertyBlock();private readonly Dictionary<string,int> metroIndex=new Dictionary<string,int>();
        private NativeCyberScreen[] metroScreens=new NativeCyberScreen[4];private int[] metroScreenBlock=Array.Empty<int>();private string[] metroScreenSegment=Array.Empty<string>();private Vector3[] metroRestCenter=Array.Empty<Vector3>();private Vector2[] metroRestHalf=Array.Empty<Vector2>();
        private NativeArena metroArena;private int metroGeneration=-1;private Mesh metroPlate;private Matrix4x4 metroPlateMatrix;
        private static readonly int MetroSizeId=Shader.PropertyToID("_BlockSize"),MetroRestId=Shader.PropertyToID("_BlockRest"),MetroColorId=Shader.PropertyToID("_BlockColor"),MetroLookId=Shader.PropertyToID("_BlockLook");

        // SourceWorld: the arena or the killcam replay arena.
        private void CyberCityScenery(NativeArena arena)
        {
            if(!NativeCyberCityLooks.Active(arena.MapId)||!EnsureCyberCity())return;
            if(!ReferenceEquals(arena,metroArena)||arena.Generation!=metroGeneration)MetroRebind(arena);
            float now=PresentationClock(arena);var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            for(int i=0;i<metroStates.Length;i++)metroStates[i]=NativeCyberCityHazards.At(metroHazards[i],match.Phase,round,match.FightAt,now);
            MetroParents(arena);DrawMetroKit(arena,now);DrawCyberBackdrop(false);
            int screens=MetroBillboards(arena);DrawCyberFx(now,WorldEye,true,screens);
        }
        // RenderBackdrop: behind the main menu, on the render clock; the skyline, its life and a plate over the slab only.
        private void CyberCityBackdrop(string mapId,float time,Camera camera)
        {
            if(!NativeCyberCityLooks.Active(mapId)||!EnsureCyberCity())return;
            if(metroScene==null)metroScene=NativeCyberFxScene.Build(null,null,null,metroBackdrop);
            NativeLighting.ArenaRim();DrawCyberBackdrop(true);DrawCyberFx(time,camera,false,0);
        }
        private bool EnsureCyberCity()
        {
            if(metroMaterial!=null)return true;if(metroFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                metroMaterial=new Material(shader){name="Cyber City dressing",enableInstancing=true};metroMesh=MetroMeshOf(NativeBlockLooks.Build(0));
                metroBackdrop=NativeCyberCityScenery.Build();metroDraw=new NativeFxDraw();metroFx=new NativeCyberCityFx();
                for(int i=0;i<metroBackdrop.Length;i++){var b=metroBackdrop[i];if(i%NativeBlockLooks.Chunk==0)metroBack.Add(new MetroBatch());var bt=metroBack[metroBack.Count-1];
                    var lin=MetroLinear(b.Color)*b.Gain;Put(bt,Matrix4x4.TRS(new Vector3(b.X,b.Y,b.Z),Quaternion.Euler(0,b.Yaw,0),Vector3.one),new Vector4(b.Hx,b.Hy,b.Hz,NativeBlockLooks.Chamfer),new Vector4(b.X,b.Y,b.Z,0),new Vector4(lin.r,lin.g,lin.b,1),new Vector4(b.Look,1,NativeBlockLooks.Seed("cyber-backdrop-"+i),b.Flags));}
                foreach(var bt in metroBack)MetroUpload(bt);
                metroPlate=metroMesh;metroPlateMatrix=Matrix4x4.TRS(new Vector3(0,-.25f,0),Quaternion.identity,Vector3.one);return true;
            }
            catch(Exception e){Debug.LogWarning("Cyber City presentation disabled: "+e.Message);DisposeCyberCity();metroFailed=true;return false;}
        }
        private void MetroRebind(NativeArena arena)
        {
            metroArena=arena;metroGeneration=arena.Generation;var map=arena.Map;if(metroHazards==null)metroHazards=NativeCyberCityHazardSource.For(arena.MapId);
            metroKit=NativeCyberCityKit.Build(map.blocks,metroHazards);metroScene=NativeCyberFxScene.Build(map.blocks,metroHazards,metroKit,metroBackdrop);
            metroStates=new NativeHazardState[metroHazards.Length];metroSeeds=new float[metroKit.Parts.Length];for(int i=0;i<metroSeeds.Length;i++)metroSeeds[i]=NativeBlockLooks.Seed(map.blocks[metroKit.Parts[i].Parent].id+"#"+i);metroParents=new NativePropBody[map.blocks.Length];metroIndex.Clear();for(int i=0;i<map.blocks.Length;i++)if(map.blocks[i]?.id!=null)metroIndex[map.blocks[i].id]=i;
            // The billboards: each segment's screen block (the presentation's holo content follows it down).
            // Once it lands, the content lies on the segment's fallen-screen rubble plate (its `-0` rubble box, else the first).
            var screens=new List<int>();var segments=new List<string>();var rest=new List<Vector3>();var half=new List<Vector2>();
            foreach(var s in map.segments??Array.Empty<StructureDefinition>())foreach(var id in s.blocks??Array.Empty<string>())if(id.EndsWith("-screen",StringComparison.Ordinal)&&metroIndex.TryGetValue(id,out int k))
            {
                ContentBlock r=null;foreach(var x in s.rubble??Array.Empty<ContentBlock>())if(x?.id!=null&&(r==null||x.id.EndsWith("-0",StringComparison.Ordinal))){r=x;if(x.id.EndsWith("-0",StringComparison.Ordinal))break;}
                var b=map.blocks[k];screens.Add(k);segments.Add(s.id);
                rest.Add(r!=null?new Vector3(r.pos.x,r.pos.y+r.size.y/2+.02f,r.pos.z):new Vector3(b.pos.x,.27f,b.pos.z));half.Add(r!=null?new Vector2(r.size.x/2,r.size.z/2):new Vector2(b.size.x/2,b.size.y/2));
            }
            metroScreenBlock=screens.ToArray();metroScreenSegment=segments.ToArray();metroRestCenter=rest.ToArray();metroRestHalf=half.ToArray();if(metroScreens.Length<metroScreenBlock.Length)metroScreens=new NativeCyberScreen[metroScreenBlock.Length];
        }
        // Each block's live prop this frame (null once it broke, or before rubble swaps in).
        private void MetroParents(NativeArena arena)
        {
            Array.Clear(metroParents,0,metroParents.Length);
            foreach(var p in arena.AllBlocks){if(p==null||p.Definition?.id==null)continue;if(metroIndex.TryGetValue(p.Definition.id,out int i))metroParents[i]=p;}
        }
        private int MetroBillboards(NativeArena arena)
        {
            int n=0;
            for(int i=0;i<metroScreenBlock.Length;i++)
            {
                var p=metroParents[metroScreenBlock[i]];int state=0;float t0=0;
                if(arena.Structures!=null){var f=arena.Structures.Get(metroScreenSegment[i]);state=f.state;t0=f.t0;}
                if(p==null&&state<3)continue;var b=arena.Map.blocks[metroScreenBlock[i]];var t=p!=null?p.transform:null;Vector3 c,u,v,normal;
                if(t!=null){c=t.position;u=t.right*(b.size.x/2);v=t.up*(b.size.y/2);normal=t.forward*(b.pos.z>0?-1:1);c+=normal*(b.size.z/2+.02f);}
                else{c=metroRestCenter[i];u=new Vector3(metroRestHalf[i].x,0,0);v=new Vector3(0,0,(b.pos.z>0?-1:1)*metroRestHalf[i].y);normal=Vector3.up;}
                metroScreens[n++]=new NativeCyberScreen{X=c.x,Y=c.y,Z=c.z,Ux=u.x,Uy=u.y,Uz=u.z,Vx=v.x,Vy=v.y,Vz=v.z,Nx=normal.x,Ny=normal.y,Nz=normal.z,Seed=metroScreenSegment[i].Length*97,State=state,T0=t0,Live=true};
            }
            return n;
        }
        private void DrawMetroKit(NativeArena arena,float now)
        {
            metroCastUsed=metroTrimUsed=0;var eye=WorldEye;Vector3 from=eye!=null?eye.transform.position:Vector3.zero,to=from;bool fade=eye!=null&&!FirstPerson&&arena.ViewPilot?.Body!=null;
            if(fade)to=arena.ViewPilot.Body.position+Vector3.up*.9f;
            var parts=metroKit.Parts;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];var prop=metroParents[part.Parent];if(prop==null)continue;var t=prop.transform;Quaternion q=t.rotation;Vector3 at=t.position+q*new Vector3(part.X,part.Y,part.Z);
                if(fade&&part.Fade&&MetroBetween(from,to,at,new Vector3(part.Hx+.2f,part.Hy+.2f,part.Hz+.2f)))continue;
                NativeCyberCityKit.Drive(part,metroStates,now,out float r,out float g,out float bl,out float level);var rest=arena.Map.blocks[part.Parent].pos;
                MetroAdd(part.Shadow,Matrix4x4.TRS(at,q,Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(rest.x+part.X,rest.y+part.Y,rest.z+part.Z,0),MetroColor(r,g,bl,level),new Vector4(part.Look,1,metroSeeds[i],part.Flags));
            }
            if(metroKit.Maglev>=0&&metroKit.Maglev<metroStates.Length)
            {
                var s=metroStates[metroKit.Maglev];
                if(s.Phase!=NativeHazardPhase.Idle&&!double.IsNaN(s.Head))
                {
                    var q=s.Direction>0?Quaternion.identity:Quaternion.Euler(0,180,0);float track=metroHazards[metroKit.Maglev].center.z;
                    foreach(var part in metroKit.Train)
                    {
                        NativeCyberCityKit.TrainPlace(part,s.Head,s.Direction,track,out float x,out float y,out float z);if(Math.Abs(x)>125)continue;var c=MetroLinear(part.Color)*part.Gain;
                        MetroAdd(part.Shadow,Matrix4x4.TRS(new Vector3(x,y,z),q,Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(part.X,part.Y,part.Z,0),new Vector4(c.r,c.g,c.b,1),new Vector4(part.Look,1,.37f,part.Flags));
                    }
                }
            }
            for(int i=0;i<metroCastUsed;i++)MetroDraw(metroCast[i],0,ShadowCastingMode.On);for(int i=0;i<metroTrimUsed;i++)MetroDraw(metroTrim[i],0,ShadowCastingMode.Off);
        }
        // Whether a box (centre, half extents) touches the segment from the eye to the pilot (slab test).
        private static bool MetroBetween(Vector3 a,Vector3 b,Vector3 c,Vector3 h)
        {
            float lo=0,hi=1;
            for(int k=0;k<3;k++){float o=a[k],d=b[k]-a[k],min=c[k]-h[k],max=c[k]+h[k];if(Mathf.Abs(d)<1e-6f){if(o<min||o>max)return false;continue;}float t0=(min-o)/d,t1=(max-o)/d;if(t0>t1){float x=t0;t0=t1;t1=x;}lo=Mathf.Max(lo,t0);hi=Mathf.Min(hi,t1);if(lo>hi)return false;}
            return true;
        }
        private void DrawCyberBackdrop(bool menu)
        {
            const int layer=NativeSceneryBuilder.BackdropLayer;foreach(var bt in metroBack)MetroDraw(bt,layer,ShadowCastingMode.Off);
            if(!menu)return;
            // Behind the menu (no arena blocks there yet): a wet plate over the slab, and the look's ground haze (in a match the
            // arena dressing publishes it every frame).
            if(metroPlateBlock==null){metroPlateBlock=new MaterialPropertyBlock();var lin=MetroLinear(0x22232A);metroPlateBlock.SetVector(MetroSizeId,new Vector4(40,.25f,32,NativeBlockLooks.Chamfer));metroPlateBlock.SetVector(MetroRestId,Vector4.zero);metroPlateBlock.SetVector(MetroColorId,new Vector4(lin.r,lin.g,lin.b,1));metroPlateBlock.SetVector(MetroLookId,new Vector4(NativeCyberCityLooks.Road,1,.5f,NativeCyberCityLooks.Bank));}
            var look=NativeMapLooks.Find(NativeLighting.LookSet(),NativeCyberCityLooks.MapId)?.blocks;
            if(look!=null){var h=MetroLinear(look.hazeColor);Shader.SetGlobalVector(MetroHazeId,new Vector4(h.r,h.g,h.b,look.haze));Shader.SetGlobalFloat(MetroHazeTopId,look.hazeTop>0?look.hazeTop:1);Shader.SetGlobalFloat(MetroDustId,look.dust);}
            Graphics.DrawMesh(metroPlate,metroPlateMatrix,metroMaterial,layer,null,0,metroPlateBlock,ShadowCastingMode.Off,true);
        }
        private MaterialPropertyBlock metroPlateBlock;
        private static readonly int MetroHazeId=Shader.PropertyToID("_ArenaHaze"),MetroHazeTopId=Shader.PropertyToID("_ArenaHazeTop"),MetroDustId=Shader.PropertyToID("_ArenaDust");
        private void DrawCyberFx(float time,Camera camera,bool atmosphere,int screens)
        {
            if(camera==null)return;ReadCyberLight();var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            metroFx.Build(metroScene,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,metroLight,Mathf.Clamp01(EffectsQuality),LowGraphics,atmosphere,atmosphere?metroStates:null,metroScreens,screens);
            var q=metroFx.Quads;metroDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?metroDraw.Solid:metroDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), so fog blends in linear light.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            metroDraw.Draw(camera,null,time);
        }
        // The scene's key light and trilight ambient in linear values, as Unity lights the arena around these pieces.
        private void ReadCyberLight()
        {
            var sun=RenderSettings.sun;var l=metroLight;
            if(sun!=null&&sun.isActiveAndEnabled){var d=-sun.transform.forward;Color c=GraphicsSettings.lightsUseLinearIntensity?sun.color.linear*sun.intensity:(sun.color*sun.intensity).linear;l.Lx=d.x;l.Ly=d.y;l.Lz=d.z;l.Sr=c.r;l.Sg=c.g;l.Sb=c.b;}
            else{l.Lx=0;l.Ly=1;l.Lz=0;l.Sr=l.Sg=l.Sb=0;}
            bool tri=RenderSettings.ambientMode==AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,eq=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,gr=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            l.Ambient[0]=sky.r;l.Ambient[1]=sky.g;l.Ambient[2]=sky.b;l.Ambient[3]=eq.r;l.Ambient[4]=eq.g;l.Ambient[5]=eq.b;l.Ambient[6]=gr.r;l.Ambient[7]=gr.g;l.Ambient[8]=gr.b;
        }
        private static Color MetroLinear(int hex){var c=NativeArena.ColorOf(hex);return QualitySettings.activeColorSpace==ColorSpace.Linear?c.linear:c;}
        // A drive's linear colour (already linear, gain folded in) for the active colour space, its light level in w.
        private static Vector4 MetroColor(float r,float g,float b,float level){if(QualitySettings.activeColorSpace!=ColorSpace.Linear){var c=new Color(r,g,b).gamma;return new Vector4(c.r,c.g,c.b,level);}return new Vector4(r,g,b,level);}
        private void MetroAdd(bool shadow,Matrix4x4 m,Vector4 size,Vector4 rest,Vector4 color,Vector4 look)
        {
            var list=shadow?metroCast:metroTrim;ref int used=ref shadow?ref metroCastUsed:ref metroTrimUsed;
            if(used==0||list[used-1].Count==NativeBlockLooks.Chunk){if(used==list.Count)list.Add(new MetroBatch());list[used].Count=0;used++;}
            Put(list[used-1],m,size,rest,color,look);
        }
        private static void Put(MetroBatch b,Matrix4x4 m,Vector4 size,Vector4 rest,Vector4 color,Vector4 look){int i=b.Count++;b.M[i]=m;b.Size[i]=size;b.Rest[i]=rest;b.Color[i]=color;b.Look[i]=look;}
        private static void MetroUpload(MetroBatch b){b.Block.Clear();b.Block.SetVectorArray(MetroSizeId,b.Size);b.Block.SetVectorArray(MetroRestId,b.Rest);b.Block.SetVectorArray(MetroColorId,b.Color);b.Block.SetVectorArray(MetroLookId,b.Look);}
        private void MetroDraw(MetroBatch b,int layer,ShadowCastingMode shadows)
        {
            if(b.Count==0)return;
            // Static backdrop batches keep the arrays they were uploaded with; kit batches upload this frame's.
            if(layer==0)MetroUpload(b);
            if(UsesInstancing){Graphics.DrawMeshInstanced(metroMesh,0,metroMaterial,b.M,b.Count,b.Block,shadows,true,layer,null);return;}
            // Compatibility rendering: the kit one by one; the backdrop (thousands of boxes) is left out.
            if(layer!=0)return;var one=metroOne;
            for(int i=0;i<b.Count;i++){one.Clear();one.SetVector(MetroSizeId,b.Size[i]);one.SetVector(MetroRestId,b.Rest[i]);one.SetVector(MetroColorId,b.Color[i]);one.SetVector(MetroLookId,b.Look[i]);Graphics.DrawMesh(metroMesh,b.M[i],metroMaterial,0,null,0,one,shadows,true);}
        }
        // The stage-0 block mesh as NativeArenaDressing builds it: the preview cube in POSITION, the lattice in TEXCOORD0/1, the
        // baked shade in COLOR; bounds that cover any block (the shader moves the vertices out to the instance's size).
        private static Mesh MetroMeshOf(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new List<Vector4>(n);var b=new List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Cyber City block mesh"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2000);mesh.UploadMeshData(true);return mesh;
        }
        private void DisposeCyberCity()
        {
            CapturedAnimation.Release(metroMesh);CapturedAnimation.Release(metroMaterial);metroMesh=null;metroMaterial=null;metroPlate=null;metroDraw?.Dispose();metroDraw=null;
            metroBack.Clear();metroCast.Clear();metroTrim.Clear();metroKit=null;metroScene=null;metroArena=null;metroGeneration=-1;metroHazards=null;metroPlateBlock=null;
        }
    }
}
