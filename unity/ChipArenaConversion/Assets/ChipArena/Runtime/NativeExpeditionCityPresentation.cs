using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Expedition town's dressing (docs/EXPEDITION-LAYOUT.md "Town dressing"), drawn only while the map id is
    /// "expedition-city" (NativeExpeditionCityKit.Active), in the arena and the killcam replay (SourceWorld, on the arena's
    /// presentation clock). Every other map is untouched, and nothing runs while intros, previews or the chip draft own the screen.
    ///  - Dressing kit (Core NativeExpeditionCityKit, built once per map content with the town's records from
    ///    Resources/ExpeditionLayout.json): parts posed with their parent props from arena.AllBlocks each frame, driven by the
    ///    clock (neon, lamps, fire, beacons, the signals' blink); the town's blocks are static, so each part's matrix and steady
    ///    colour are made once and only a moved or shuddering block's parts are posed again; parts of a block the breach presentation hides
    ///    (NativeArenaDressing.Hidden) hide with it and follow its knock (NativeArenaDressing.Nudged); parts between the camera
    ///    and the pilot that ask to fade are hidden; the low graphics tier and compatibility rendering leave the detail parts out;
    ///    far parts are left out by their size (NativeExpeditionCityKit.Range: what glows to the fog's end, the rest by size,
    ///    squared once per map in townReach), so the bigger town (exp-layout-7) costs per frame what the camera can see; parts on a
    ///    building face its block hides from the camera are left out (NativeExpeditionCityKit.Hide, the back-face rule of the
    ///    city detail pass: the face's ratio once per frame, one compare per part).
    ///    Instanced NativeBlockLit draws of 500 on layer 0, the big parts casting shadows, the rest not.
    ///  - Skyline (Core NativeExpeditionCityScenery): static instanced batches on NativeSceneryBuilder.BackdropLayer, no shadows
    ///    cast, uploaded once; left out under compatibility rendering.
    ///  - Practical lights: the eight that matter to the camera (Core NativeExpeditionCityLights) through
    ///    NativeLighting.PickedLights, eased per frame.
    ///  - Atmosphere (Core NativeExpeditionCityFx) through one NativeFxDraw: a solid and an effects batch, scaled by the effects
    ///    quality and the low tier; thicker, darker fog over the hub's wilds.
    ///  - The hub (exp-layout-3; NativeExpeditionHubPresentation.cs): the sanctuary gates at their state's pose, their dust and
    ///    the ward's shimmer.
    /// Nothing here writes sim data, collides or enters the network frame. Allocation free after the one-time builds.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private Material townMaterial;private Mesh townMesh,townToon;private bool townFailed;private NativeFxDraw townDraw;private NativeExpeditionCityFx townFx;
        private NativeCityBackdropBox[] townBackdrop;private NativeCityBackdropFire[] townFires;private readonly List<MetroBatch> townBack=new List<MetroBatch>(),townCast=new List<MetroBatch>(),townTrim=new List<MetroBatch>();private int townCastUsed,townTrimUsed;
        // The anime style's wrap goes to the parts that need it (Core NativeMapStyle.NeedsWrap), drawn first: the batches holding any
        // of them take the wrap, the rest the rounded box (the same count of batches; a third fewer kit vertices, a quarter of its triangles).
        private int[] townOrder=Array.Empty<int>();private int townWrapParts,townCastWrap,townTrimWrap;
        private bool[] townPack;private ContentMap townMap;private NativeExpeditionCityKit townKit;private NativeCityFxScene townScene;private NativeExpeditionCityLights townLights;private ExpeditionAreaLayout townArea;private bool townAreaRead;private float[] townReach=Array.Empty<float>(),townBehind=Array.Empty<float>();
        private NativePropBody[] townParents=Array.Empty<NativePropBody>();private bool[] townOut=Array.Empty<bool>();private Vector3[] townNudge=Array.Empty<Vector3>();private float[] townSeeds=Array.Empty<float>();
        private readonly Dictionary<string,int> townIndex=new Dictionary<string,int>();private MaterialPropertyBlock townOne;private float townLast=-1;private NativeArena townArena;private int townGeneration=-1;
        // The town's blocks are static: each part's matrix and steady colour at its block's rest pose, made once; a block that
        // moves or shudders (a breach knock) has its parts posed from its transform that frame.
        private Matrix4x4[] townRestMatrix=Array.Empty<Matrix4x4>();private Vector4[] townRestColor=Array.Empty<Vector4>(),townRestSpot=Array.Empty<Vector4>();private bool[] townMoved=Array.Empty<bool>();private bool townColorLinear;
        /// <summary>The last frame's kit and skyline instances and draws (main pass), for the budget lines.</summary>
        public int TownInstances{get;private set;}public int TownDraws{get;private set;}

        // SourceWorld: the arena or the killcam replay arena.
        private void ExpeditionCityScenery(NativeArena arena)
        {
            var map=arena.Map;if(!NativeExpeditionCityKit.Active(arena.MapId)||map==null||!EnsureExpeditionCity())return;
            if(!ReferenceEquals(map,townMap))ExpeditionCityBuild(map);
            if(!ReferenceEquals(arena,townArena)||arena.Generation!=townGeneration){townArena=arena;townGeneration=arena.Generation;townLast=-1;}
            float now=PresentationClock(arena);
            Array.Clear(townParents,0,townParents.Length);foreach(var p in arena.AllBlocks){if(p==null||p.Definition?.id==null)continue;if(townIndex.TryGetValue(p.Definition.id,out int i))townParents[i]=p;}
            var blocks=map.blocks;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;if(linear!=townColorLinear)TownRestColors();
            for(int i=0;i<townParents.Length;i++){var p=townParents[i];if(p==null){townMoved[i]=false;continue;}var t=p.transform;var b=blocks[i];var r=b.rotation;
                townMoved[i]=t.position!=new Vector3(b.pos.x,b.pos.y,b.pos.z)||t.rotation!=(r==null?Quaternion.identity:new Quaternion(r.x,r.y,r.z,r.w));}
            // A block a breach presentation hides takes its parts with it; a knocked block's parts shudder with it.
            Array.Clear(townOut,0,townOut.Length);Array.Clear(townNudge,0,townNudge.Length);
            if(NativeArenaDressing.Hidden.Count>0)foreach(var id in NativeArenaDressing.Hidden)if(id!=null&&townIndex.TryGetValue(id,out int k))townOut[k]=true;
            if(NativeArenaDressing.Nudged.Count>0)foreach(var pair in NativeArenaDressing.Nudged)if(pair.Key!=null&&townIndex.TryGetValue(pair.Key,out int k)){townNudge[k]=pair.Value;townMoved[k]=true;}
            var eye=WorldEye;TownInstances=TownDraws=0;DrawTownKit(arena,now,eye);DrawTownBackdrop();
            if(eye!=null){var c=eye.transform.position;float dt=townLast<0?0:Mathf.Clamp(now-townLast,0,.1f);townLights.Update(c.x,c.y,c.z,now,dt);NativeLighting.PickedLights(townLights.Pos,townLights.Rgb);}
            DrawTownFx(now,eye);ExpeditionHubFrame(eye,now);townLast=now;
        }
        // The anime map style draws its rounded box (Core NativeToonMesh) in place of the legacy S0 mesh, made once on first use.
        private Mesh TownMesh=>NativeLighting.StyleWrap?(townWrap??=MetroMeshOf(NativeToonMesh.Wrap())):NativeLighting.StyleActive?(townToon??=MetroMeshOf(NativeToonMesh.Build())):townMesh;private Mesh townWrap;
        private Mesh TownSmall=>NativeLighting.StyleWrap?(townToon??=MetroMeshOf(NativeToonMesh.Build())):TownMesh;
        private bool EnsureExpeditionCity()
        {
            if(townMaterial!=null)return true;if(townFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                townMaterial=new Material(shader){name="Expedition town dressing",enableInstancing=true};townMesh=MetroMeshOf(NativeBlockLooks.Build(0));townDraw=new NativeFxDraw();townFx=new NativeExpeditionCityFx();
                // With the pack bank's docks (Core NativePackScenery): their lots left empty, the west plate ending at the quay.
                var docks=NativePackStage.Entry(NativeExpeditionCityKit.MapId);double quay=NativePackScenery.WestQuay(docks);
                townBackdrop=docks?.yards!=null?NativeExpeditionCityScenery.Build(out townFires,true,(x0,x1,z0,z1)=>NativePackScenery.Reserved(docks,x0,x1,z0,z1),quay):NativeExpeditionCityScenery.Build(out townFires);
                for(int i=0;i<townBackdrop.Length;i++)
                {
                    var b=townBackdrop[i];if(i%NativeBlockLooks.Chunk==0)townBack.Add(new MetroBatch());var bt=townBack[townBack.Count-1];var lin=MetroLinear(b.Color)*b.Gain;
                    Put(bt,Matrix4x4.TRS(new Vector3(b.X,b.Y,b.Z),Quaternion.Euler(0,b.Yaw,0),Vector3.one),new Vector4(b.Hx,b.Hy,b.Hz,NativeBlockLooks.Chamfer),new Vector4(b.X,b.Y,b.Z,0),new Vector4(lin.r,lin.g,lin.b,b.Level),new Vector4(b.Look,1,NativeBlockLooks.Seed("town-backdrop-"+i),b.Flags));
                }
                foreach(var bt in townBack)MetroUpload(bt);return true;
            }
            catch(Exception e){Debug.LogWarning("Expedition town dressing disabled: "+e.Message);DisposeExpeditionCity();townFailed=true;return false;}
        }
        // Once per map content (the killcam's replay arena shares it): the kit with the town's records, its atmosphere and lights.
        private void ExpeditionCityBuild(ContentMap map)
        {
            if(!townAreaRead)
            {
                townAreaRead=true;
                try{var asset=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName);if(asset!=null){var layout=NativeExpeditionLayout.Parse(asset.text);foreach(var a in layout.areas)if(a.map==map.id){townArea=a;break;}
                    // The hub record (exp-layout-3): the gates, the wilds and the routes; absent from older manifests.
                    if(townArea!=null)townHub=ExpeditionHubLayout.Parse(asset.text,townArea.id);}}
                catch(Exception e){Debug.LogWarning("Expedition town dressing: no layout records ("+e.Message+"); spawns undressed");}
                NativeExpeditionHubLive.Sanctuary=townArea?.sanctuary??Array.Empty<ExpeditionVolume>();
            }
            townMap=map;var blocks=map.blocks??Array.Empty<ContentBlock>();townKit=NativeExpeditionCityKit.Build(blocks,townArea,townHub,map.bounds);var look=NativeLighting.Look(map);
            townScene=NativeCityFxScene.Build(blocks,townKit,townFires,look,townHub);townLights=NativeExpeditionCityLights.Build(townKit,look);HubWardSegments(map);
            townParents=new NativePropBody[blocks.Length];townOut=new bool[blocks.Length];townNudge=new Vector3[blocks.Length];townIndex.Clear();for(int i=0;i<blocks.Length;i++)if(blocks[i]?.id!=null)townIndex[blocks[i].id]=i;
            townSeeds=new float[townKit.Parts.Length];for(int i=0;i<townSeeds.Length;i++)townSeeds[i]=NativeBlockLooks.Seed(blocks[townKit.Parts[i].Parent].id+"#"+i);
            townOrder=new int[townKit.Parts.Length];int w=0;for(int i=0;i<townOrder.Length;i++)if(TownNeedsWrap(i))townOrder[w++]=i;townWrapParts=w;for(int i=0;i<townOrder.Length;i++)if(!TownNeedsWrap(i))townOrder[w++]=i;
            float far=NativeExpeditionCityKit.FarRange(look);townReach=new float[townKit.Parts.Length];for(int i=0;i<townReach.Length;i++){float r=NativeExpeditionCityKit.Range(townKit.Parts[i],far);townReach[i]=r*r;}
            townBehind=new float[townKit.FaceCulls.Length+1];
            townMoved=new bool[blocks.Length];var parts=townKit.Parts;townRestMatrix=new Matrix4x4[parts.Length];townRestSpot=new Vector4[parts.Length];townRestColor=new Vector4[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];var b=blocks[part.Parent];var r=b.rotation;var q=r==null?Quaternion.identity:new Quaternion(r.x,r.y,r.z,r.w);var at=new Vector3(b.pos.x,b.pos.y,b.pos.z);
                townRestMatrix[i]=Matrix4x4.TRS(at+q*new Vector3(part.X,part.Y,part.Z),q*new Quaternion(part.Qx,part.Qy,part.Qz,part.Qw),Vector3.one);townRestSpot[i]=new Vector4(b.pos.x+part.X,b.pos.y+part.Y,b.pos.z+part.Z,0);
            }
            // A part of a block drawn as pack props (docs/arenas/STAGE-KITS.md) stops drawing with its box; parts resting on its top stay.
            townPack=NativePackStage.Covered(map,parts.Length,i=>parts[i].Parent,i=>(new Vector3(parts[i].X,parts[i].Y,parts[i].Z),new Vector3(parts[i].Hx,parts[i].Hy,parts[i].Hz),new Quaternion(parts[i].Qx,parts[i].Qy,parts[i].Qz,parts[i].Qw)),true);
            TownRestColors();
        }
        // The steady parts' colours for the active colour space (driven parts are coloured each frame).
        private void TownRestColors()
        {
            townColorLinear=QualitySettings.activeColorSpace==ColorSpace.Linear;var parts=townKit?.Parts;if(parts==null)return;
            for(int i=0;i<parts.Length;i++){NativeExpeditionCityKit.Drive(parts[i],0,out float r,out float g,out float bl,out float level);townRestColor[i]=MetroColor(r,g,bl,level);}
        }
        private bool TownNeedsWrap(int i){var p=townKit.Parts[i];return NativeMapStyle.NeedsWrap(p.Hx,p.Hy,p.Hz,p.Look,p.Flags);}
        private void DrawTownKit(NativeArena arena,float now,Camera eye)
        {
            townCastUsed=townTrimUsed=townCastWrap=townTrimWrap=0;Vector3 from=eye!=null?eye.transform.position:Vector3.zero,to=from;bool fade=eye!=null&&!FirstPerson&&arena.ViewPilot?.Body!=null;if(fade)to=arena.ViewPilot.Body.position+Vector3.up*.9f;
            var parts=townKit.Parts;bool lean=LowGraphics||!UsesInstancing,near=eye!=null;Vector3 at=from;if(near)townKit.Hide(at.x,at.y,at.z,townBehind);
            for(int k=0;k<parts.Length;k++)
            {
                int i=townOrder[k];var part=parts[i];if(part.Detail&&lean)continue;var prop=townParents[part.Parent];if(prop==null||townOut[part.Parent]||townPack!=null&&townPack[i])continue;
                if(near){var spot=townRestSpot[i];float sx=spot.x-at.x,sy=spot.y-at.y,sz=spot.z-at.z;if(sx*sx+sy*sy+sz*sz>townReach[i]||part.Face!=0&&townBehind[part.Face]<=part.Back)continue;}
                Matrix4x4 m;if(townMoved[part.Parent]){var t=prop.transform;Quaternion q=t.rotation;m=Matrix4x4.TRS(t.position+townNudge[part.Parent]+q*new Vector3(part.X,part.Y,part.Z),q*new Quaternion(part.Qx,part.Qy,part.Qz,part.Qw),Vector3.one);}else m=townRestMatrix[i];
                if(fade&&part.Fade&&MetroBetween(from,to,new Vector3(m.m03,m.m13,m.m23),new Vector3(part.Hx+.2f,part.Hy+.2f,part.Hz+.2f)))continue;
                Vector4 color=townRestColor[i];if(part.Drive!=NativeExpeditionCityKit.DriveSteady){NativeExpeditionCityKit.Drive(part,now,out float r,out float g,out float bl,out float level);color=MetroColor(r,g,bl,level);}
                var list=part.Shadow?townCast:townTrim;ref int used=ref part.Shadow?ref townCastUsed:ref townTrimUsed;
                if(used==0||list[used-1].Count==NativeBlockLooks.Chunk){if(used==list.Count)list.Add(new MetroBatch());list[used].Count=0;used++;}
                Put(list[used-1],m,new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),townRestSpot[i],color,new Vector4(part.Look,1,townSeeds[i],part.Flags));
                if(k<townWrapParts){if(part.Shadow)townCastWrap=used;else townTrimWrap=used;}
            }
            for(int i=0;i<townCastUsed;i++)TownDraw(townCast[i],ShadowCastingMode.On,i<townCastWrap?TownMesh:TownSmall);for(int i=0;i<townTrimUsed;i++)TownDraw(townTrim[i],ShadowCastingMode.Off,i<townTrimWrap?TownMesh:TownSmall);
        }
        private void TownDraw(MetroBatch b,ShadowCastingMode shadows,Mesh mesh)
        {
            if(b.Count==0)return;MetroUpload(b);TownInstances+=b.Count;
            if(UsesInstancing){Graphics.DrawMeshInstanced(mesh,0,townMaterial,b.M,b.Count,b.Block,shadows,true,0,null);TownDraws++;return;}
            // Compatibility rendering: one by one (detail parts already left out), reusing one block made on this main-thread path.
            var one=townOne??=new MaterialPropertyBlock();
            for(int i=0;i<b.Count;i++){one.Clear();one.SetVector(MetroSizeId,b.Size[i]);one.SetVector(MetroRestId,b.Rest[i]);one.SetVector(MetroColorId,b.Color[i]);one.SetVector(MetroLookId,b.Look[i]);Graphics.DrawMesh(mesh,b.M[i],townMaterial,0,null,0,one,shadows,true);TownDraws++;}
        }
        // The skyline: static batches (uploaded once) on the backdrop layer; compatibility rendering leaves it out.
        private void DrawTownBackdrop()
        {
            if(!UsesInstancing)return;const int layer=NativeSceneryBuilder.BackdropLayer;
            foreach(var bt in townBack){Graphics.DrawMeshInstanced(TownMesh,0,townMaterial,bt.M,bt.Count,bt.Block,ShadowCastingMode.Off,true,layer,null);TownDraws++;TownInstances+=bt.Count;}
        }
        private void DrawTownFx(float time,Camera camera)
        {
            if(camera==null)return;ReadCyberLight();var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            townFx.Beams=AdvancedBeams;townFx.Build(townScene,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,metroLight,Mathf.Clamp01(EffectsQuality),LowGraphics);
            var q=townFx.Quads;townDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?townDraw.Solid:townDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), as Cyber City's effects do.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            townDraw.Draw(camera,null,time);
        }
        private void DisposeExpeditionCity()
        {
            CapturedAnimation.Release(townMesh);if(townToon!=null)CapturedAnimation.Release(townToon);townToon=null;if(townWrap!=null)CapturedAnimation.Release(townWrap);townWrap=null;CapturedAnimation.Release(townMaterial);townMesh=null;townMaterial=null;townDraw?.Dispose();townDraw=null;
            townBack.Clear();townCast.Clear();townTrim.Clear();townMap=null;townKit=null;townScene=null;townLights=null;townArena=null;townGeneration=-1;townLast=-1;DisposeExpeditionHub();
        }
    }
}
