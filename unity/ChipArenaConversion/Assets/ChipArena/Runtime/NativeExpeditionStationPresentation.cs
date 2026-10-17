using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Central Station's visuals (docs/EXPEDITION-CENTRAL-STATION-VISUALS.md), drawn only while the map is the station
    /// (NativeExpeditionStation.Active on the map id), in the arena and the killcam replay (SourceWorld, on the arena's
    /// presentation clock). No live map has that id today, so nothing here runs until the station is imported; every other
    /// map is untouched, and nothing runs while intros, previews or the chip draft own the screen.
    ///  - The dressing kit (Core NativeExpeditionStationKit, built once per map content with the station's records from
    ///    Resources/ExpeditionLayout.json): parts posed with their parent props, made once at rest and only re-posed when a block
    ///    moves or shudders; driven parts (the board's rows and blink, the signals, beacons, the live rail's glints, the flood,
    ///    the cabinets, the levers, the train's windows, the lamps' flicker) coloured each frame from the state; parts of a block
    ///    the breach presentation hides hide with it; fading parts between the camera and the pilot are hidden; the low tier
    ///    and compatibility rendering draw the low tier's parts (detail out, the sleepers as one strip a track). Instanced
    ///    NativeBlockLit draws of 500 on layer 0, the big parts casting shadows, the rest not.
    ///  - The express (the kit's Express rig), posed at the state's nose in its own draws while it is in the station.
    ///  - The state (Core NativeStationState) from the view Astra's replicated fields fill (<see cref="NativeStationLive"/>),
    ///    or from the DEV preview's synthetic view (NativeStationPreview, DEV panel · Camera · "Station hazards preview").
    ///  - Practical lights: the eight (four on the low tier) that matter to the camera (Core NativeExpeditionStationLights)
    ///    through NativeLighting.PickedLights with their beams (the express's headlight, the beacons).
    ///  - Atmosphere and the hazards' telegraphs (Core NativeExpeditionStationFx) through one NativeFxDraw: a solid and an
    ///    effects batch, scaled by the effects quality and the low tier; the telegraphs are never thinned.
    /// Nothing here writes sim data, collides or enters the network frame. Allocation free after the one-time builds.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        private Material stationMaterial;private Mesh stationMesh;private bool stationFailed;private NativeFxDraw stationDraw;private NativeExpeditionStationFx stationFx;
        private ContentMap stationMap;private NativeExpeditionStationKit stationKit;private NativeStationFxScene stationScene;private NativeExpeditionStationLights stationLights;
        private ExpeditionAreaLayout stationArea;private bool stationAreaRead;
        private NativePropBody[] stationParents=Array.Empty<NativePropBody>();private bool[] stationOut=Array.Empty<bool>(),stationMoved=Array.Empty<bool>();private Vector3[] stationNudge=Array.Empty<Vector3>();private float[] stationSeeds=Array.Empty<float>();
        private Matrix4x4[] stationRest=Array.Empty<Matrix4x4>();private Vector4[] stationRestColor=Array.Empty<Vector4>(),stationRestSpot=Array.Empty<Vector4>();private bool stationColorLinear;
        private readonly List<MetroBatch> stationCast=new List<MetroBatch>(),stationTrim=new List<MetroBatch>();private int stationCastUsed,stationTrimUsed;
        private readonly Dictionary<string,int> stationIndex=new Dictionary<string,int>();private MaterialPropertyBlock stationOne;
        private readonly NativeStationView stationPreview=new NativeStationView();private readonly NativeStationState stationState=new NativeStationState();
        private float stationLast=-1;private NativeArena stationArena;private int stationGeneration=-1;
        /// <summary>The last frame's kit and express instances and draws (main pass), for the budget lines.</summary>
        public int StationInstances{get;private set;}public int StationDraws{get;private set;}

        // SourceWorld: the arena or the killcam replay arena.
        private void StationScenery(NativeArena arena)
        {
            var map=arena.Map;if(!NativeExpeditionStation.Active(arena.MapId)||map==null||!EnsureStation())return;
            if(!ReferenceEquals(map,stationMap))StationBuild(map);
            if(!ReferenceEquals(arena,stationArena)||arena.Generation!=stationGeneration){stationArena=arena;stationGeneration=arena.Generation;stationLast=-1;}
            float now=PresentationClock(arena);
            // The view: the DEV preview's while it is on, else what the authority's replicated fields put in NativeStationLive.
            var view=NativeStationLive.View;if(NativeStationPreview.On){NativeStationPreview.Fill(stationPreview,now);view=stationPreview;}
            stationState.Evaluate(view,now);NativeStationLive.Shown=view;NativeStationLive.ShownClock=now;
            Array.Clear(stationParents,0,stationParents.Length);foreach(var p in arena.AllBlocks){if(p==null||p.Definition?.id==null)continue;if(stationIndex.TryGetValue(p.Definition.id,out int i))stationParents[i]=p;}
            var blocks=map.blocks;if((QualitySettings.activeColorSpace==ColorSpace.Linear)!=stationColorLinear)StationRestColors();
            for(int i=0;i<stationParents.Length;i++){var p=stationParents[i];if(p==null){stationMoved[i]=false;continue;}var t=p.transform;var b=blocks[i];var r=b.rotation;
                stationMoved[i]=t.position!=new Vector3(b.pos.x,b.pos.y,b.pos.z)||t.rotation!=(r==null?Quaternion.identity:new Quaternion(r.x,r.y,r.z,r.w));}
            Array.Clear(stationOut,0,stationOut.Length);Array.Clear(stationNudge,0,stationNudge.Length);
            if(NativeArenaDressing.Hidden.Count>0)foreach(var id in NativeArenaDressing.Hidden)if(id!=null&&stationIndex.TryGetValue(id,out int k))stationOut[k]=true;
            if(NativeArenaDressing.Nudged.Count>0)foreach(var pair in NativeArenaDressing.Nudged)if(pair.Key!=null&&stationIndex.TryGetValue(pair.Key,out int k)){stationNudge[k]=pair.Value;stationMoved[k]=true;}
            var eye=WorldEye;StationInstances=StationDraws=0;DrawStationKit(arena,now,eye);
            if(eye!=null){var c=eye.transform.position;float dt=stationLast<0?0:Mathf.Clamp(now-stationLast,0,.1f);stationLights.Update(c.x,c.y,c.z,stationState,now,dt,LowGraphics?NativeExpeditionStationKit.LowSlots:NativeExpeditionStationKit.Slots);NativeLighting.PickedLights(stationLights.Pos,stationLights.Rgb,stationLights.Beam);}
            DrawStationFx(now,eye,view);stationLast=now;
        }
        private bool EnsureStation()
        {
            if(stationMaterial!=null)return true;if(stationFailed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit shader is missing");
                stationMaterial=new Material(shader){name="Central Station dressing",enableInstancing=true};stationMesh=MetroMeshOf(NativeBlockLooks.Build(0));stationDraw=new NativeFxDraw();stationFx=new NativeExpeditionStationFx();
                // The presentation's lifetime: released with this component (no shared teardown line to edit).
                var life=gameObject.GetComponent<NativeStationLifetime>();if(life==null)life=gameObject.AddComponent<NativeStationLifetime>();life.Gone=DisposeStation;return true;
            }
            catch(Exception e){Debug.LogWarning("Central Station dressing disabled: "+e.Message);DisposeStation();stationFailed=true;return false;}
        }
        // Once per map content (the killcam's replay arena shares it): the kit with the station's records, its atmosphere and lights.
        private void StationBuild(ContentMap map)
        {
            if(!stationAreaRead)
            {
                stationAreaRead=true;
                try{var asset=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName);if(asset!=null){var layout=NativeExpeditionLayout.Parse(asset.text);foreach(var a in layout.areas)if(a.map==map.id){stationArea=a;break;}}}
                catch(Exception e){Debug.LogWarning("Central Station dressing: no layout records ("+e.Message+"); entrances undressed");}
            }
            stationMap=map;var blocks=map.blocks??Array.Empty<ContentBlock>();stationKit=NativeExpeditionStationKit.Build(blocks,stationArea);var look=NativeLighting.Look(map);
            stationScene=NativeStationFxScene.Build(stationKit,look);stationLights=NativeExpeditionStationLights.Build(stationKit,look);
            stationParents=new NativePropBody[blocks.Length];stationOut=new bool[blocks.Length];stationNudge=new Vector3[blocks.Length];stationMoved=new bool[blocks.Length];
            stationIndex.Clear();for(int i=0;i<blocks.Length;i++)if(blocks[i]?.id!=null)stationIndex[blocks[i].id]=i;
            var parts=stationKit.Parts;stationSeeds=new float[parts.Length];stationRest=new Matrix4x4[parts.Length];stationRestSpot=new Vector4[parts.Length];stationRestColor=new Vector4[parts.Length];
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];var b=blocks[part.Parent];stationSeeds[i]=NativeBlockLooks.Seed(b.id+"#"+i);var r=b.rotation;var q=r==null?Quaternion.identity:new Quaternion(r.x,r.y,r.z,r.w);
                stationRest[i]=Matrix4x4.TRS(new Vector3(b.pos.x,b.pos.y,b.pos.z)+q*new Vector3(part.X,part.Y,part.Z),q*new Quaternion(part.Qx,part.Qy,part.Qz,part.Qw),Vector3.one);stationRestSpot[i]=new Vector4(b.pos.x+part.X,b.pos.y+part.Y,b.pos.z+part.Z,0);
            }
            StationRestColors();
        }
        // The steady parts' colours for the active colour space (driven parts are coloured each frame).
        private void StationRestColors()
        {
            stationColorLinear=QualitySettings.activeColorSpace==ColorSpace.Linear;var parts=stationKit?.Parts;if(parts==null)return;
            for(int i=0;i<parts.Length;i++){NativeExpeditionStationKit.Drive(parts[i],null,0,out float r,out float g,out float b,out float level);stationRestColor[i]=MetroColor(r,g,b,level);}
        }
        private void DrawStationKit(NativeArena arena,float now,Camera eye)
        {
            stationCastUsed=stationTrimUsed=0;Vector3 from=eye!=null?eye.transform.position:Vector3.zero,to=from;bool fade=eye!=null&&!FirstPerson&&arena.ViewPilot?.Body!=null;if(fade)to=arena.ViewPilot.Body.position+Vector3.up*.9f;
            bool lean=LowGraphics||!UsesInstancing;byte skip=lean?NativeExpeditionStationKit.TierFull:NativeExpeditionStationKit.TierLow;var parts=stationKit.Parts;
            for(int i=0;i<parts.Length;i++)
            {
                var part=parts[i];if(part.Tier==skip)continue;var prop=stationParents[part.Parent];if(prop==null||stationOut[part.Parent])continue;
                Matrix4x4 m;if(stationMoved[part.Parent]){var t=prop.transform;Quaternion q=t.rotation;m=Matrix4x4.TRS(t.position+stationNudge[part.Parent]+q*new Vector3(part.X,part.Y,part.Z),q*new Quaternion(part.Qx,part.Qy,part.Qz,part.Qw),Vector3.one);}else m=stationRest[i];
                if(fade&&part.Fade&&MetroBetween(from,to,new Vector3(m.m03,m.m13,m.m23),new Vector3(part.Hx+.2f,part.Hy+.2f,part.Hz+.2f)))continue;
                Vector4 color=stationRestColor[i];if(part.Drive!=NativeExpeditionStationKit.DriveSteady){if(!NativeExpeditionStationKit.Drive(part,stationState,now,out float r,out float g,out float bl,out float level))continue;color=MetroColor(r,g,bl,level);}
                StationPut(part.Shadow,m,new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),stationRestSpot[i],color,new Vector4(part.Look,1,stationSeeds[i],part.Flags));
            }
            // The express: posed at the nose while it is in the station (its parts cull beyond the shell).
            var ex=stationKit.Express;byte e=stationState.Express;
            if(e==NativeStationState.ExpressPass||e==NativeStationState.ExpressAfter)
            {
                float line=NativeExpeditionStation.LineZ[stationState.Line-1];
                for(int i=0;i<ex.Length;i++)
                {
                    var part=ex[i];if(part.Tier==skip)continue;if(!NativeExpeditionStationKit.Drive(part,stationState,now,out float r,out float g,out float bl,out float level))continue;
                    NativeExpeditionStationKit.ExpressPlace(part,stationState.Nose,line,out float x,out float y,out float z);if(Math.Abs(x)>NativeExpeditionStation.ShellX+.6f+part.Hx)continue;
                    StationPut(part.Shadow,Matrix4x4.TRS(new Vector3(x,y,z),new Quaternion(part.Qx,part.Qy,part.Qz,part.Qw),Vector3.one),new Vector4(part.Hx,part.Hy,part.Hz,NativeBlockLooks.Chamfer),new Vector4(part.X,part.Y,part.Z,0),MetroColor(r,g,bl,level),new Vector4(part.Look,1,.29f,part.Flags));
                }
            }
            for(int i=0;i<stationCastUsed;i++)StationDraw(stationCast[i],ShadowCastingMode.On);for(int i=0;i<stationTrimUsed;i++)StationDraw(stationTrim[i],ShadowCastingMode.Off);
        }
        private void StationPut(bool shadow,Matrix4x4 m,Vector4 size,Vector4 rest,Vector4 color,Vector4 look)
        {
            var list=shadow?stationCast:stationTrim;ref int used=ref shadow?ref stationCastUsed:ref stationTrimUsed;
            if(used==0||list[used-1].Count==NativeBlockLooks.Chunk){if(used==list.Count)list.Add(new MetroBatch());list[used].Count=0;used++;}
            Put(list[used-1],m,size,rest,color,look);
        }
        private void StationDraw(MetroBatch b,ShadowCastingMode shadows)
        {
            if(b.Count==0)return;MetroUpload(b);StationInstances+=b.Count;
            if(UsesInstancing){Graphics.DrawMeshInstanced(stationMesh,0,stationMaterial,b.M,b.Count,b.Block,shadows,true,0,null);StationDraws++;return;}
            // Compatibility rendering: one by one (detail parts already left out), reusing one block made on this main-thread path.
            var one=stationOne??=new MaterialPropertyBlock();
            for(int i=0;i<b.Count;i++){one.Clear();one.SetVector(MetroSizeId,b.Size[i]);one.SetVector(MetroRestId,b.Rest[i]);one.SetVector(MetroColorId,b.Color[i]);one.SetVector(MetroLookId,b.Look[i]);Graphics.DrawMesh(stationMesh,b.M[i],stationMaterial,0,null,0,one,shadows,true);StationDraws++;}
        }
        private void DrawStationFx(float time,Camera camera,NativeStationView view)
        {
            if(camera==null)return;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;stationFx.GlassView=view;
            stationFx.Build(stationScene,stationState,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(EffectsQuality),LowGraphics);
            var q=stationFx.Quads;stationDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?stationDraw.Solid:stationDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), as the town's effects do.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            stationDraw.Draw(camera,null,time);
        }
        private void DisposeStation()
        {
            if(stationMesh!=null)CapturedAnimation.Release(stationMesh);if(stationMaterial!=null)CapturedAnimation.Release(stationMaterial);stationMesh=null;stationMaterial=null;stationDraw?.Dispose();stationDraw=null;
            stationCast.Clear();stationTrim.Clear();stationMap=null;stationKit=null;stationScene=null;stationLights=null;stationArena=null;stationGeneration=-1;stationLast=-1;
        }
    }
}
