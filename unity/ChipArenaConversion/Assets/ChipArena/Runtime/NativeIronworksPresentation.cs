using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The City Ironworks' hazard state for the presentation (Claude, 2026-10-16). `Authority` is the binding point for the
    /// instance's hazard clock and phase that Astra's hazard authority will publish (docs/EXPEDITION-CITY-IRONWORKS.md sections 5
    /// and 10.4: the hazards' phase is derived from the instance's hazard clock); nothing sets it yet, so every hazard rests. The
    /// DEV preview (DEV panel · Camera · "Ironworks hazards preview") stands in for it on the presentation clock, labelled on
    /// screen; it never touches gameplay.
    /// </summary>
    public static class NativeIronworksLive
    {
        /// <summary>Set by the instance's hazard binding (clock, wave, boss) with `Bound` true; until then nothing runs.</summary>
        public static IronworksHazardInput Authority;public static bool Bound;
        /// <summary>The DEV preview's stage (0 off, 1..4 a wave, 5 the boss encounter) and when it started (unscaled s).</summary>
        public static int PreviewStage;public static double PreviewStart;
        public static bool Preview=>PreviewStage>0;
        /// <summary>The stages the DEV preview steps through.</summary>
        public static readonly int[] PreviewStages={0,1,3,5};
        /// <summary>This frame's input: the preview's, else the authority's, else nothing running.</summary>
        public static IronworksHazardInput Input()=>Preview?NativeIronworksHazards.Preview(PreviewStage,Time.unscaledTimeAsDouble-PreviewStart):Bound?Authority:default;
    }

    /// <summary>
    /// The City Ironworks' presentation (docs/EXPEDITION-CITY-IRONWORKS.md sections 3, 4, 5 and 9), drawn only while map
    /// `expedition-foundry` is loaded (NativeIronworksKit.Active), in the arena and the killcam replay (SourceWorld, on the
    /// arena's presentation clock). Every other map is untouched.
    ///  - Dressing kit (Core NativeIronworksKit, built once per map content with the instance's records from
    ///    Resources/ExpeditionLayout.json): instanced NativeBlockLit draws of 500 on layer 0, no shadows cast (the look's key casts
    ///    none indoors). Static parts' matrices and steady colours are made once; moving frames (the ladle, the drop hammer's ram,
    ///    the hot slab, the shear blade, the pusher ram, the slag pot, the hook) are posed each frame from the hazard views
    ///    (NativeIronworksKit.FramePose) and driven parts coloured (Drive). Parts of a block a breach hides hide with it and follow
    ///    its knock (NativeArenaDressing.Hidden, Nudged); the low tier and compatibility rendering leave the detail parts out.
    ///  - Molten metal (Resources/NativeMolten.shader): the kit's molten pieces instanced on a unit cube, their state each frame
    ///    from NativeIronworksKit.MoltenState; the global _MoltenClock is the presentation clock.
    ///  - The molten light field (Core NativeIronworksField): baked once per map on a worker thread into the Ward light field's
    ///    3D texture layout and sent each frame through NativeLighting.WardField with each channel's level (the steady glows, the
    ///    three runners). Blocks, kit, molten crust and the source models (pilots, monsters) take it.
    ///  - The eight practical lights (Core NativeIronworksLights: six warm, two always cool) through NativeLighting.PickedLights.
    ///  - Effects (Core NativeIronworksSceneFx) through one NativeFxDraw (a solid and an effects batch), scaled by the effects quality
    ///    and the low tier; the telegraph decals always draw.
    ///  - Heat haze (Resources/NativeHeatHaze.shader): one dynamic mesh of camera-facing quads over the hot sources, one GrabPass;
    ///    none on the low tier.
    /// Hazard views: NativeIronworksLive (the authority's input once bound, or the DEV preview). Until Astra's hazard clock binds,
    /// normal play has every hazard off: the views stay idle (runners crusted, no pour, no telegraphs, the moving frames at rest),
    /// so only the static dressing, lighting, atmosphere and molten props show. Nothing here writes sim data, collides, damages
    /// or enters the network frame. Allocation free after the one-time builds.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private sealed class IronMoltenBatch
        {
            public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Flow=new Vector4[NativeBlockLooks.Chunk],Heat=new Vector4[NativeBlockLooks.Chunk],Style=new Vector4[NativeBlockLooks.Chunk];
            public int Count;public readonly MaterialPropertyBlock Block=new MaterialPropertyBlock();
        }
        private static readonly int MoltenSizeId=Shader.PropertyToID("_MoltenSize"),MoltenFlowId=Shader.PropertyToID("_MoltenFlow"),MoltenHeatId=Shader.PropertyToID("_MoltenHeat"),MoltenStyleId=Shader.PropertyToID("_MoltenStyle"),
            MoltenClockId=Shader.PropertyToID("_MoltenClock"),HeatClockId=Shader.PropertyToID("_HeatClock");
        private Material iwMaterial,iwMolten,iwHaze;private Mesh iwMesh,iwCube,iwHazeMesh,iwToon;
        // The anime map style draws the rounded box (Core NativeToonMesh) in place of the legacy S0 mesh, made once on first use.
        private Mesh IronShape=>NativeLighting.StyleActive?(iwToon??=MetroMeshOf(NativeToonMesh.Build())):iwMesh;
        private bool iwFailed;private NativeFxDraw iwDraw;private readonly NativeIronworksSceneFx iwFx=new NativeIronworksSceneFx();
        private ContentMap iwMap;private NativeIronworksKit iwKit;private NativeIronFxScene iwScene;private NativeIronworksLights iwLights;private ExpeditionAreaLayout iwArea;private bool iwAreaRead;
        private readonly IronworksHazardView[] iwViews=new IronworksHazardView[NativeIronworksHazards.Count];
        private readonly List<MetroBatch> iwBatches=new List<MetroBatch>();private int iwUsed;private readonly List<IronMoltenBatch> iwMelt=new List<IronMoltenBatch>();private int iwMeltUsed;
        private bool[] iwPack;private NativePropBody[] iwParents=Array.Empty<NativePropBody>();private bool[] iwOut=Array.Empty<bool>();private Vector3[] iwNudge=Array.Empty<Vector3>();private bool[] iwMoved=Array.Empty<bool>();
        private readonly Dictionary<string,int> iwIndex=new Dictionary<string,int>();private float[] iwSeeds=Array.Empty<float>();private Matrix4x4[] iwRest=Array.Empty<Matrix4x4>(),iwMeltRest=Array.Empty<Matrix4x4>();
        private Vector4[] iwRestColor=Array.Empty<Vector4>(),iwRestSpot=Array.Empty<Vector4>();private bool iwColorLinear;
        private readonly Matrix4x4[] iwFrames=new Matrix4x4[NativeIronworksKit.Frames];private readonly Quaternion[] iwFrameQ=new Quaternion[NativeIronworksKit.Frames];
        private MaterialPropertyBlock iwOne;private float iwLast=-1;private NativeArena iwArena;private int iwGeneration=-1;private NativeZoneBossBeat iwMusic;
        private System.Threading.Tasks.Task<NativeIronworksField> iwBake;private ContentMap iwBakeMap;private NativeIronworksField iwField;private Texture3D iwFieldTex;private readonly float[] iwLevels=new float[NativeIronworksField.Channels];
        private readonly List<Vector3> iwHazeV=new List<Vector3>(NativeIronworksSceneFx.HeatMax*4);private readonly List<Color> iwHazeC=new List<Color>(NativeIronworksSceneFx.HeatMax*4);private readonly List<Vector2> iwHazeUv=new List<Vector2>(NativeIronworksSceneFx.HeatMax*4);
        /// <summary>The last frame's kit and molten instances and their draws (main pass), for the budget lines.</summary>
        public int IronworksInstances{get;private set;}public int IronworksDraws{get;private set;}

        // SourceWorld: the arena or the killcam replay arena.
        private void IronworksScenery(NativeArena arena)
        {
            var map=arena.Map;if(map==null||!NativeIronworksKit.Active(map.id)||!EnsureIronworks())return;
            if(!ReferenceEquals(map,iwMap))IronworksBuild(map);
            if(!ReferenceEquals(arena,iwArena)||arena.Generation!=iwGeneration){iwArena=arena;iwGeneration=arena.Generation;iwLast=-1;}
            float now=PresentationClock(arena);arena.BindZonePresentation();NativeIronworksHazards.Evaluate(NativeIronworksLive.Input(),iwViews);
            // The zone boss song (NativeIronworksMusic; presentation only): furnaces, work lights, steam, sparks and the parked ladle.
            iwMusic=ZoneBossBeat(arena);NativeLighting.ZoneHouse(iwMusic.House());iwMusic=IronworksBossMusic(iwMusic);
            Shader.SetGlobalFloat(MoltenClockId,(float)(now%1000.0));Shader.SetGlobalFloat(HeatClockId,(float)(now%1000.0));
            var blocks=map.blocks;Array.Clear(iwParents,0,iwParents.Length);foreach(var p in arena.AllBlocks){if(p==null||p.Definition?.id==null)continue;if(iwIndex.TryGetValue(p.Definition.id,out int i))iwParents[i]=p;}
            for(int i=0;i<iwParents.Length;i++){var p=iwParents[i];if(p==null){iwMoved[i]=false;continue;}var t=p.transform;var b=blocks[i];iwMoved[i]=t.position!=new Vector3(b.pos.x,b.pos.y,b.pos.z);}
            Array.Clear(iwOut,0,iwOut.Length);Array.Clear(iwNudge,0,iwNudge.Length);
            if(NativeArenaDressing.Hidden.Count>0)foreach(var id in NativeArenaDressing.Hidden)if(id!=null&&iwIndex.TryGetValue(id,out int k))iwOut[k]=true;
            if(NativeArenaDressing.Nudged.Count>0)foreach(var pair in NativeArenaDressing.Nudged)if(pair.Key!=null&&iwIndex.TryGetValue(pair.Key,out int k)){iwNudge[k]=pair.Value;iwMoved[k]=true;}
            for(int f=0;f<NativeIronworksKit.Frames;f++){NativeIronworksKit.FramePose(f,iwViews,now,out float px,out float py,out float pz,out float qx,out float qy,out float qz,out float qw,iwMusic);iwFrameQ[f]=new Quaternion(qx,qy,qz,qw);iwFrames[f]=Matrix4x4.TRS(new Vector3(px,py,pz),iwFrameQ[f],Vector3.one);}
            var eye=WorldEye;IronworksInstances=IronworksDraws=0;DrawIronKit(arena,now,eye);DrawIronMolten(now);
            IronworksField();
            if(eye!=null){var c=eye.transform.position;float dt=iwLast<0?0:Mathf.Clamp(now-iwLast,0,.1f);iwLights.Update(c.x,c.y,c.z,iwViews,now,dt,4,iwMusic);NativeLighting.PickedLights(iwLights.Pos,iwLights.Rgb,iwLights.Beam);}
            IronworksFieldSend(now);DrawIronFx(now,eye);DrawIronHaze(now,eye);iwLast=now;
        }
        private bool EnsureIronworks()
        {
            if(iwMaterial!=null)return true;if(iwFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                var molten=Resources.Load<Shader>("NativeMolten");if(molten==null||!molten.isSupported)throw new InvalidOperationException("NativeMolten shader is missing");
                iwMaterial=new Material(shader){name="Ironworks dressing",enableInstancing=true};iwMolten=new Material(molten){name="Ironworks molten metal",enableInstancing=true};
                var haze=Resources.Load<Shader>("NativeHeatHaze");if(haze!=null&&haze.isSupported)iwHaze=new Material(haze){name="Ironworks heat haze"};
                iwMesh=MetroMeshOf(NativeBlockLooks.Build(0));iwCube=IronCube();iwDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Ironworks presentation disabled: "+e.Message);DisposeIronworks();iwFailed=true;return false;}
        }
        // A unit cube (+-1) with face normals: the molten pieces' mesh.
        private static Mesh IronCube()
        {
            var v=new List<Vector3>(24);var n=new List<Vector3>(24);var t=new List<int>(36);
            for(int ax=0;ax<3;ax++)for(int sg=-1;sg<=1;sg+=2)
            {
                int u=(ax+1)%3,w=(ax+2)%3,b=v.Count;var nn=Vector3.zero;nn[ax]=sg;
                foreach(var c in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){var p=Vector3.zero;p[ax]=sg;p[u]=c.x;p[w]=c.y;v.Add(p);n.Add(nn);}
                // Front faces: (b - a) x (c - a) points out of the cube (Unity's clockwise seen from outside).
                if(sg>0){t.Add(b);t.Add(b+1);t.Add(b+2);t.Add(b);t.Add(b+2);t.Add(b+3);}else{t.Add(b);t.Add(b+2);t.Add(b+1);t.Add(b);t.Add(b+3);t.Add(b+2);}
            }
            var m=new Mesh{name="Ironworks molten cube"};m.SetVertices(v);m.SetNormals(n);m.SetTriangles(t,0);m.bounds=new Bounds(Vector3.zero,Vector3.one*2000);m.UploadMeshData(true);return m;
        }
        // Once per map content (the killcam's replay arena shares it): the kit with the instance's records, its effects scene,
        // lights and the light field's bake.
        private void IronworksBuild(ContentMap map)
        {
            if(!iwAreaRead)
            {
                iwAreaRead=true;
                try{var asset=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName);if(asset!=null){var layout=NativeExpeditionLayout.Parse(asset.text);foreach(var a in layout.areas)if(a.map==map.id){iwArea=a;break;}}}
                catch(Exception e){Debug.LogWarning("Ironworks dressing: no layout records ("+e.Message+"); dressed without keep-outs");}
            }
            iwMap=map;var blocks=map.blocks??Array.Empty<ContentBlock>();iwKit=NativeIronworksKit.Build(blocks,iwArea);iwScene=NativeIronFxScene.Build(iwKit);iwLights=NativeIronworksLights.Build(iwKit);
            ReleaseIronField();var kit=iwKit;var bd=map.bounds;float x0=bd?.minX??-50,x1=bd?.maxX??50,z0=bd?.minZ??-34,z1=bd?.maxZ??34;iwBakeMap=map;
            iwBake=System.Threading.Tasks.Task.Run(()=>NativeIronworksField.Build(kit,blocks,x0,x1,z0,z1));
            iwParents=new NativePropBody[blocks.Length];iwOut=new bool[blocks.Length];iwNudge=new Vector3[blocks.Length];iwMoved=new bool[blocks.Length];iwIndex.Clear();for(int i=0;i<blocks.Length;i++)if(blocks[i]?.id!=null)iwIndex[blocks[i].id]=i;
            var parts=iwKit.Parts;iwSeeds=new float[parts.Length];iwRest=new Matrix4x4[parts.Length];iwRestSpot=new Vector4[parts.Length];iwRestColor=new Vector4[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                var p=parts[i];var b=blocks[p.Parent];iwSeeds[i]=NativeBlockLooks.Seed(b.id+"#"+i);
                iwRest[i]=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw),Vector3.one);iwRestSpot[i]=new Vector4(b.pos.x+p.X,b.pos.y+p.Y,b.pos.z+p.Z,0);
            }
            // A part of a block drawn as pack props (docs/arenas/STAGE-KITS.md) stops drawing with its box; parts resting on its top stay.
            iwPack=NativePackStage.Covered(map,parts.Length,i=>parts[i].Parent,i=>(new Vector3(parts[i].X,parts[i].Y,parts[i].Z),new Vector3(parts[i].Hx,parts[i].Hy,parts[i].Hz),new Quaternion(parts[i].Qx,parts[i].Qy,parts[i].Qz,parts[i].Qw)),false);
            var melt=iwKit.Molten;iwMeltRest=new Matrix4x4[melt.Length];for(int i=0;i<melt.Length;i++){var m=melt[i];iwMeltRest[i]=Matrix4x4.TRS(new Vector3(m.X,m.Y,m.Z),new Quaternion(m.Qx,m.Qy,m.Qz,m.Qw),Vector3.one);}
            IronRestColors();
        }
        // The steady parts' colours for the active colour space (driven parts are coloured each frame).
        private void IronRestColors()
        {
            iwColorLinear=QualitySettings.activeColorSpace==ColorSpace.Linear;var parts=iwKit?.Parts;if(parts==null)return;
            for(int i=0;i<parts.Length;i++){NativeIronworksKit.Drive(parts[i],null,0,out float r,out float g,out float b,out float level);iwRestColor[i]=MetroColor(r,g,b,level);}
        }
        private void DrawIronKit(NativeArena arena,float now,Camera eye)
        {
            iwUsed=0;if(QualitySettings.activeColorSpace==ColorSpace.Linear!=iwColorLinear)IronRestColors();
            Vector3 from=eye!=null?eye.transform.position:Vector3.zero,to=from;bool fade=eye!=null&&!FirstPerson&&arena.ViewPilot?.Body!=null;if(fade)to=arena.ViewPilot.Body.position+Vector3.up*.9f;
            var parts=iwKit.Parts;bool lean=LowGraphics||!UsesInstancing;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];if(part.Detail&&lean||iwOut[part.Parent]||iwPack!=null&&iwPack[i])continue;Matrix4x4 m;
                if(part.Frame!=NativeIronworksKit.FrameStatic)m=iwFrames[part.Frame]*iwRest[i];
                else if(iwMoved[part.Parent]&&iwParents[part.Parent]!=null){var b=arena.Map.blocks[part.Parent];var off=iwParents[part.Parent].transform.position+iwNudge[part.Parent]-new Vector3(b.pos.x,b.pos.y,b.pos.z);m=Matrix4x4.Translate(off)*iwRest[i];}
                else m=iwRest[i];
                if(fade&&part.Fade&&MetroBetween(from,to,new Vector3(m.m03,m.m13,m.m23),new Vector3(part.Hx+.2f,part.Hy+.2f,part.Hz+.2f)))continue;
                Vector4 color=iwRestColor[i];if(part.Drive!=NativeIronworksKit.DriveSteady||iwMusic.On&&NativeIronworksKit.MusicReactive(part)){NativeIronworksKit.Drive(part,iwViews,now,out float r,out float g,out float bl,out float level,iwMusic);color=MetroColor(r,g,bl,level);}
                if(iwUsed==0||iwBatches[iwUsed-1].Count==NativeBlockLooks.Chunk){if(iwUsed==iwBatches.Count)iwBatches.Add(new MetroBatch());iwBatches[iwUsed].Count=0;iwUsed++;}
                Put(iwBatches[iwUsed-1],m,new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),iwRestSpot[i],color,new Vector4(part.Look,1,iwSeeds[i],part.Flags));
            }
            for(int i=0;i<iwUsed;i++)IronDraw(iwBatches[i]);
        }
        private void IronDraw(MetroBatch b)
        {
            if(b.Count==0)return;MetroUpload(b);IronworksInstances+=b.Count;
            if(UsesInstancing){Graphics.DrawMeshInstanced(IronShape,0,iwMaterial,b.M,b.Count,b.Block,ShadowCastingMode.Off,true,0,null);IronworksDraws++;return;}
            var one=iwOne??=new MaterialPropertyBlock();
            for(int i=0;i<b.Count;i++){one.Clear();one.SetVector(MetroSizeId,b.Size[i]);one.SetVector(MetroRestId,b.Rest[i]);one.SetVector(MetroColorId,b.Color[i]);one.SetVector(MetroLookId,b.Look[i]);Graphics.DrawMesh(IronShape,b.M[i],iwMaterial,0,null,0,one,ShadowCastingMode.Off,true);IronworksDraws++;}
        }
        private void DrawIronMolten(float now)
        {
            iwMeltUsed=0;var melt=iwKit.Molten;bool lean=LowGraphics||!UsesInstancing;
            for(int i=0;i<melt.Length;i++)
            {
                var m=melt[i];if(m.Detail&&lean||iwOut[m.Parent])continue;NativeIronworksKit.MoltenState(m,iwViews,now,out float ha,out float hb,out float crust,out float front,out float flow,out bool shown,iwMusic);if(!shown)continue;
                var mat=m.Frame!=NativeIronworksKit.FrameStatic?iwFrames[m.Frame]*iwMeltRest[i]:iwMeltRest[i];
                if(iwMeltUsed==0||iwMelt[iwMeltUsed-1].Count==NativeBlockLooks.Chunk){if(iwMeltUsed==iwMelt.Count)iwMelt.Add(new IronMoltenBatch());iwMelt[iwMeltUsed].Count=0;iwMeltUsed++;}
                var bt=iwMelt[iwMeltUsed-1];int k=bt.Count++;bt.M[k]=mat;bt.Size[k]=new Vector4(m.Hx,m.Hy,m.Hz,0);bt.Flow[k]=new Vector4(m.Speed,m.Seed,0,0);bt.Heat[k]=new Vector4(ha,hb,crust,front);bt.Style[k]=new Vector4(m.Kind,(m.Axis+1f)*m.Sign,flow,m.Gain);
            }
            for(int i=0;i<iwMeltUsed;i++)
            {
                var b=iwMelt[i];if(b.Count==0)continue;IronworksInstances+=b.Count;
                if(UsesInstancing){b.Block.Clear();b.Block.SetVectorArray(MoltenSizeId,b.Size);b.Block.SetVectorArray(MoltenFlowId,b.Flow);b.Block.SetVectorArray(MoltenHeatId,b.Heat);b.Block.SetVectorArray(MoltenStyleId,b.Style);Graphics.DrawMeshInstanced(iwCube,0,iwMolten,b.M,b.Count,b.Block,ShadowCastingMode.Off,true,0,null);IronworksDraws++;continue;}
                var one=iwOne??=new MaterialPropertyBlock();
                for(int k=0;k<b.Count;k++){one.Clear();one.SetVector(MoltenSizeId,b.Size[k]);one.SetVector(MoltenFlowId,b.Flow[k]);one.SetVector(MoltenHeatId,b.Heat[k]);one.SetVector(MoltenStyleId,b.Style[k]);Graphics.DrawMesh(iwCube,b.M[k],iwMolten,0,null,0,one,ShadowCastingMode.Off,true);IronworksDraws++;}
            }
        }
        private void DrawIronFx(float time,Camera camera)
        {
            if(camera==null)return;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            iwFx.Music=iwMusic;iwFx.Build(iwScene,iwViews,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(EffectsQuality),LowGraphics);
            var q=iwFx.Quads;iwDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?iwDraw.Solid:iwDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), as the other kits' effects do.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            iwDraw.Draw(camera,null,time);
        }
        // The heat haze: the effects' heat sources as camera-facing quads in one dynamic mesh (unused quads collapse); none on the
        // low tier, so the grab never happens there.
        private void DrawIronHaze(float time,Camera camera)
        {
            if(camera==null||iwHaze==null||LowGraphics||!UsesInstancing)return;
            if(iwHazeMesh==null)
            {
                iwHazeMesh=new Mesh{name="Ironworks heat haze"};iwHazeMesh.MarkDynamic();var idx=new int[NativeIronworksSceneFx.HeatMax*6];
                for(int i=0;i<NativeIronworksSceneFx.HeatMax;i++){idx[i*6]=i*4;idx[i*6+1]=i*4+2;idx[i*6+2]=i*4+1;idx[i*6+3]=i*4;idx[i*6+4]=i*4+3;idx[i*6+5]=i*4+2;}
                for(int i=0;i<NativeIronworksSceneFx.HeatMax*4;i++){iwHazeV.Add(Vector3.zero);iwHazeC.Add(Color.clear);iwHazeUv.Add(Vector2.zero);}
                iwHazeMesh.SetVertices(iwHazeV);iwHazeMesh.SetColors(iwHazeC);iwHazeMesh.SetUVs(0,iwHazeUv);iwHazeMesh.SetIndices(idx,MeshTopology.Triangles,0);iwHazeMesh.bounds=new Bounds(Vector3.zero,Vector3.one*2000);
            }
            var t=camera.transform;Vector3 r=t.right,u=t.up;
            for(int i=0;i<NativeIronworksSceneFx.HeatMax;i++)
            {
                int o=i*4;if(i>=iwFx.HeatCount){for(int k=0;k<4;k++){iwHazeV[o+k]=Vector3.zero;iwHazeC[o+k]=Color.clear;}continue;}
                var h=iwFx.Heat[i];var c=new Vector3(h.X,h.Y,h.Z);var a=r*(h.W/2);var b=u*(h.H/2);var col=new Color(1,1,1,h.Strength);
                iwHazeV[o]=c-a-b;iwHazeV[o+1]=c+a-b;iwHazeV[o+2]=c+a+b;iwHazeV[o+3]=c-a+b;iwHazeUv[o]=new Vector2(-1,-1);iwHazeUv[o+1]=new Vector2(1,-1);iwHazeUv[o+2]=new Vector2(1,1);iwHazeUv[o+3]=new Vector2(-1,1);
                for(int k=0;k<4;k++)iwHazeC[o+k]=col;
            }
            iwHazeMesh.SetVertices(iwHazeV);iwHazeMesh.SetColors(iwHazeC);iwHazeMesh.SetUVs(0,iwHazeUv);
            Graphics.DrawMesh(iwHazeMesh,Matrix4x4.identity,iwHaze,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        // Takes the field's bake once it is in (RGBAHalf, bilinear, clamped); a failed bake or a platform without 3D half textures
        // keeps the practical slots alone.
        private void IronworksField()
        {
            if(iwBake==null||!iwBake.IsCompleted)return;var bake=iwBake;iwBake=null;if(!ReferenceEquals(iwBakeMap,iwMap))return;
            if(bake.IsFaulted||bake.IsCanceled){Debug.LogWarning("Ironworks light field disabled: "+(bake.Exception?.GetBaseException().Message??"cancelled"));return;}
            var f=bake.Result;if(f==null||f.Data.Length==0||!SystemInfo.supports3DTextures||!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAHalf))return;
            try
            {
                var tex=new Texture3D(f.Nx,f.Nz,NativeWardZeroField.Slices,TextureFormat.RGBAHalf,false){name="Ironworks light field",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,anisoLevel=0};
                var half=new ushort[f.Data.Length];for(int i=0;i<half.Length;i++)half[i]=Mathf.FloatToHalf(f.Data[i]);tex.SetPixelData(half,0);tex.Apply(false,true);
                iwFieldTex=tex;f.Data=Array.Empty<float>();iwField=f;
            }
            catch(Exception e){Debug.LogWarning("Ironworks light field disabled: "+e.Message);ReleaseIronField();}
        }
        private void IronworksFieldSend(double now)
        {
            if(iwFieldTex==null||iwField==null){NativeLighting.ActorLights(true);return;}
            NativeIronworksField.Levels(iwViews,now,iwLevels,iwMusic);NativeIronworksField.Colour(out float r,out float g,out float b);var f=iwField;
            NativeLighting.WardField(iwFieldTex,new Vector4(f.MinX,f.MinZ,f.Nx*NativeWardZeroField.Cell,f.Nz*NativeWardZeroField.Cell),new Vector4(iwLevels[0],iwLevels[1],iwLevels[2],iwLevels[3]),new Color(r,g,b),true);
        }
        private void ReleaseIronField(){if(iwFieldTex!=null)CapturedAnimation.Release(iwFieldTex);iwFieldTex=null;iwField=null;iwBake=null;iwBakeMap=null;}
        private void DisposeIronworks()
        {
            if(iwFieldTex!=null)NativeLighting.WardFieldOff();ReleaseIronField();
            if(iwMesh!=null)CapturedAnimation.Release(iwMesh);if(iwToon!=null)CapturedAnimation.Release(iwToon);iwToon=null;if(iwCube!=null)CapturedAnimation.Release(iwCube);if(iwHazeMesh!=null)CapturedAnimation.Release(iwHazeMesh);
            if(iwMaterial!=null)CapturedAnimation.Release(iwMaterial);if(iwMolten!=null)CapturedAnimation.Release(iwMolten);if(iwHaze!=null)CapturedAnimation.Release(iwHaze);
            iwMesh=iwCube=iwHazeMesh=null;iwMaterial=iwMolten=iwHaze=null;iwDraw?.Dispose();iwDraw=null;iwBatches.Clear();iwMelt.Clear();iwHazeV.Clear();iwHazeC.Clear();iwHazeUv.Clear();
            iwMap=null;iwKit=null;iwScene=null;iwLights=null;iwArena=null;iwGeneration=-1;iwLast=-1;
        }
    }

    public sealed partial class ConversionGame
    {
        /// <summary>DEV panel · Camera: the Ironworks hazards preview steps off, Wave 1 (the light set), Wave 3 (everything) and the
        /// boss encounter (the pour stopped). Presentation only.</summary>
        private void IronworksPreviewStep()
        {
            var s=NativeIronworksLive.PreviewStages;int i=Array.IndexOf(s,NativeIronworksLive.PreviewStage);NativeIronworksLive.PreviewStage=s[(i+1)%s.Length];NativeIronworksLive.PreviewStart=Time.unscaledTimeAsDouble;
            DevSay(NativeIronworksLive.Preview?"Ironworks hazards preview · "+NativeIronworksHazards.StageName(NativeIronworksLive.PreviewStage)+" (presentation only; nothing burns)":"Ironworks hazards preview · off",false);
        }
        // Cached per stage, so the panel and the label allocate nothing per repaint.
        private static readonly string[] IronworksPreviewButtons=Stages(s=>"Ironworks hazards: "+NativeIronworksHazards.StageName(s)),IronworksPreviewLabels=Stages(s=>"DEV PREVIEW · IRONWORKS HAZARDS · "+NativeIronworksHazards.StageName(s).ToUpperInvariant()+" · NOT GAMEPLAY");
        private static string[] Stages(Func<int,string> f){var a=new string[6];for(int i=0;i<a.Length;i++)a[i]=f(i);return a;}
        private float IronworksPreviewPanel(float x,float y,float w)
        {
            y=DevHead(x,y,w,"CITY IRONWORKS (DEV PREVIEW)");DevToggleAct(new Rect(x,y,w,32),"preview-ironworks",NativeIronworksLive.Preview,IronworksPreviewButtons[Mathf.Clamp(NativeIronworksLive.PreviewStage,0,5)]);y+=38;
            return DevNote(x,y,w,"On the Ironworks map: the hazards run their cycles on the presentation clock (off, Wave 1, Wave 3, boss). Presentation only: nothing burns, crushes or collides until the instance's hazard authority exists.",C.Muted());
        }
        /// <summary>The DEV preview's on-screen label (only on the Ironworks map while the preview runs).</summary>
        private void IronworksPreviewHud(float width)
        {
            if(!NativeIronworksLive.Preview||Event.current.type!=EventType.Repaint||arena==null||!NativeIronworksKit.Active(arena.MapId))return;
            string label=IronworksPreviewLabels[Mathf.Clamp(NativeIronworksLive.PreviewStage,0,5)];var size=U.Measure(label,11,true,true,.12f);
            var r=new Rect(width/2-size.x/2-14,92,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,label,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
        }
    }
}
