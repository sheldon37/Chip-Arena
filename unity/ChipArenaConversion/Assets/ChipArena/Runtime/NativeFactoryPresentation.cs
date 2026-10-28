using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Factory's presentation (docs/arenas/FACTORY_PROPOSAL.md 3.10, 5.3), drawn only while the map id is "factory": in the
    /// arena and the killcam replay (SourceWorld, on the drawn arena's field clock, so the saws, rams and crane stand where the
    /// simulation's states put them on every client and in the killcam) and behind the main menu (RenderBackdrop, render
    /// clock: the backdrop and its life only). Every other map is untouched: nothing is created until a factory frame, and a
    /// non-factory frame draws nothing. Never sim data and never a collider.
    ///  - The look (light rig, sky, fog, practical lights, block looks) is data: MapLooks.json's "factory" entry, applied by
    ///    NativeLighting and NativeArenaDressing like any other dressed map.
    ///  - Dressing kit (Core NativeFactoryKit, by block id prefix and hazard fittings) and the hazard rigs (Core
    ///    NativeFactoryRig, posed from NativeFactoryHazards.State) as NativeBlockLit instances: the kit is packed once per map
    ///    into casting and shadowless batches of 500, the rig refilled each frame into its own (allocation free).
    ///  - Backdrop (Core NativeFactoryScenery): five SourceLit sector meshes and the goliath trolley on
    ///    NativeSceneryBuilder.BackdropLayer, no shadow casting.
    ///  - Effects (Core NativeFactoryFx): one solid and one effects NativeFxDraw batch.
    /// Hazard definitions are UnityMaps.json's `hazards[]` for the map (read with Core FactoryHazardContent until ContentMap
    /// carries them): with no factory map or no hazards the rigs, their fittings and their effects simply do not draw.
    /// Crane impacts also dust the round's coat (NativeDustCoat.Land, idempotent on time and place).
    /// </summary>
    public sealed partial class NativeFactoryStage : MonoBehaviour
    {
        private sealed class Batch {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        // The anime map style draws the rounded box (Core NativeToonMesh) in place of the legacy S0 mesh, made once on first use.
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;private Mesh blockToon;
        private Mesh block;private Material lit;private MaterialPropertyBlock props;private bool failed;
        private Batch[] kit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch{Shadow=true},new Batch{Shadow=true},new Batch(),new Batch()};
        private ContentMap kitMap;private static FactoryHazard[] hazards;private static bool hazardsRead;private static string hazardsMap;private NativeFactoryKit.Looks looks;private NativeFactoryRig rigBuilder;
        private Mesh[] sectors;private Mesh trolley,plate;private Material sceneryLit;private NativeFxDraw fxDraw;private readonly NativeFactoryFx fx=new NativeFactoryFx();private readonly NativeFxLight fxLight=new NativeFxLight();
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        /// <summary>The last frame's NativeBlockLit draws (kit and rig, main pass) and instances, the effect quads.</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}public int Quads=>fx.Quads.Count;
        /// <summary>The hazard definitions of map `id` in Resources/UnityMaps.json (null when none), read and validated once.</summary>
        public static FactoryHazard[] Hazards(string id)
        {
            if(hazardsRead&&hazardsMap==id)return hazards;hazardsRead=true;hazardsMap=id;hazards=null;
            try{var asset=Resources.Load<TextAsset>("UnityMaps");if(asset==null)return null;var set=JsonUtility.FromJson<FactoryHazardContent>(asset.text);
                foreach(var m in set?.maps??Array.Empty<FactoryHazardMap>())if(m!=null&&m.id==id&&m.hazards!=null&&m.hazards.Length>0){NativeFactoryHazards.Validate(m.hazards);hazards=m.hazards;}}
            catch(Exception e){Debug.LogWarning("Factory hazards disabled: "+e.Message);hazards=null;}
            return hazards;
        }
        private bool Ensure()
        {
            if(lit!=null)return true;if(failed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");var source=Resources.Load<Shader>("NativeSourceLit");if(shader==null||source==null)throw new InvalidOperationException("NativeBlockLit or NativeSourceLit is missing");
                lit=new Material(shader){name="Factory kit",enableInstancing=true};props=new MaterialPropertyBlock();block=MeshOf(NativeBlockLooks.Build(0));
                sceneryLit=new Material(source){name="Factory scenery",enableInstancing=true};sceneryLit.SetFloat("_StageLit",0);sceneryLit.SetFloat("_ToonEdgeRef",16);
                var built=NativeFactoryScenery.Build();sectors=new Mesh[built.Length];for(int i=0;i<built.Length;i++)sectors[i]=SceneryMesh(built[i],"Factory scenery "+i);
                trolley=SceneryMesh(NativeFactoryScenery.BuildTrolley(),"Factory goliath trolley");plate=SceneryMesh(NativeFactoryScenery.BuildMenuPlate(),"Factory menu plate");fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Factory presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>Each hazard's blood (the hazard clock of the last pilot its saw cut this round, NaN none; null: no blood, gore off), set before Render.</summary>
        public double[] Blood;
        /// <summary>The frame's volumetric beam list (ArenaPresentation.AdvancedBeams; null: the skylights' billboard sheets).</summary>
        public NativeBeamList Beams;
        /// <summary>One arena frame. now: the drawn arena's field clock; quality: effects quality; low: graphics=performance.</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing,NativeDustCoat coat)
        {
            Draws=Instances=0;if(!Ensure())return;var map=arena.Map;var match=arena.Match;var hz=Hazards(arena.MapId);
            if(!ReferenceEquals(map,kitMap))
            {
                kitMap=map;try{looks=NativeFactoryKit.Resolve(NativeLighting.Look(map).kitLooks);rigBuilder=new NativeFactoryRig(looks);Pack(NativeFactoryKit.Build(map.blocks,hz,looks));}
                catch(Exception e){Debug.LogWarning("Factory dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;}
            }
            int round=NativeStructureFx.RoundIndex(match.Set,match.Round);double clock=NativeFactoryHazards.Clock(match.Phase,match.FightAt,now);
            DrawScenery(now,true);foreach(var b in kit)Draw(b,instancing);
            if(hz!=null&&rigBuilder!=null)
            {
                rigBuilder.Build(hz,round,clock,Blood);Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);
                // a crane impact this cycle dusts the round's coat once (match time = hazard clock + FightAt + the arm delay)
                foreach(var h in hz)if(h.kind==NativeFactoryHazards.Crane){var st=NativeFactoryHazards.State(h,round,clock);if(!double.IsNaN(st.ImpactAt)&&clock>=st.ImpactAt&&st.Zone>=0){var z=h.zones[st.Zone];coat?.Land(st.ImpactAt+match.FightAt+NativeFactoryHazards.ArmDelay,z.center.x,z.center.z,3,.6f);}}
            }
            Effects(hz,round,clock,now,camera,quality,low,true);
        }
        /// <summary>Behind the main menu: the backdrop, the slab plate and the backdrop's life (no arena blocks exist there).</summary>
        public void Backdrop(float time,Camera camera,float quality,bool low)
        {
            Draws=Instances=0;if(!Ensure())return;DrawScenery(time,false);Effects(null,0,double.NegativeInfinity,time,camera,quality,low,false);
        }
        private void DrawScenery(float time,bool arena)
        {
            const int layer=NativeSceneryBuilder.BackdropLayer;
            foreach(var m in sectors)Graphics.DrawMesh(m,Matrix4x4.identity,sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);
            if(!arena)Graphics.DrawMesh(plate,Matrix4x4.identity,sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);
            NativeFactoryScenery.Trolley(time,out double x,out double y,out double z);Graphics.DrawMesh(trolley,Matrix4x4.Translate(new Vector3((float)x,(float)y,(float)z)),sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);
        }
        private void Effects(FactoryHazard[] hz,int round,double clock,float time,Camera camera,float quality,bool low,bool atmosphere)
        {
            if(camera==null)return;ReadLight();var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            fx.Beams=Beams;fx.Build(hz,round,clock,time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,fxLight,Mathf.Clamp01(quality),low,atmosphere);
            var q=fx.Quads;fxDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?fxDraw.Solid:fxDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            fxDraw.Draw(camera,null,time);
        }
        // The scene's key light and trilight ambient in linear values, as Unity lights the SourceLit meshes round the effects.
        private void ReadLight()
        {
            var sun=RenderSettings.sun;var l=fxLight;
            if(sun!=null&&sun.isActiveAndEnabled){var d=-sun.transform.forward;Color c=GraphicsSettings.lightsUseLinearIntensity?sun.color.linear*sun.intensity:(sun.color*sun.intensity).linear;l.Lx=d.x;l.Ly=d.y;l.Lz=d.z;l.Sr=c.r;l.Sg=c.g;l.Sb=c.b;}
            else{l.Lx=0;l.Ly=1;l.Lz=0;l.Sr=l.Sg=l.Sb=0;}
            bool tri=RenderSettings.ambientMode==AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,eq=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,gr=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            l.Ambient[0]=sky.r;l.Ambient[1]=sky.g;l.Ambient[2]=sky.b;l.Ambient[3]=eq.r;l.Ambient[4]=eq.g;l.Ambient[5]=eq.b;l.Ambient[6]=gr.r;l.Ambient[7]=gr.g;l.Ambient[8]=gr.b;
        }
        // The static kit into batches of 500 by shadow mode, packed once.
        private void Pack(NativeFactoryKit.Writer parts)
        {
            // The parts of blocks drawn as pack props (docs/arenas/STAGE-KITS.md) stop drawing with their boxes.
            NativePackStage.Strip(kitMap,parts);
            int casters=0,flat=0;for(int i=0;i<parts.Count;i++)if(parts.Items[i].Shadow)casters++;else flat++;
            int nc=(casters+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk,nf=(flat+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;kit=new Batch[nc+nf];
            for(int i=0;i<kit.Length;i++)kit[i]=new Batch{Shadow=i<nc};int ci=0,fi=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];var b=p.Shadow?kit[ci/NativeBlockLooks.Chunk]:kit[nc+fi/NativeBlockLooks.Chunk];if(p.Shadow)ci++;else fi++;Put(b,b.Count++,ref p);}
        }
        // The frame's rig: casters into the first three batches, shadowless parts into the last two (Capacity 1400 fits).
        private void Fill(NativeFactoryKit.Writer parts)
        {
            foreach(var b in rig)b.Count=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];Batch b=null;if(p.Shadow){for(int k=0;k<3;k++)if(rig[k].Count<NativeBlockLooks.Chunk){b=rig[k];break;}}else{for(int k=3;k<5;k++)if(rig[k].Count<NativeBlockLooks.Chunk){b=rig[k];break;}}if(b==null)continue;Put(b,b.Count++,ref p);}
        }
        private static void Put(Batch b,int i,ref FactoryPart p)
        {
            b.M[i]=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw),Vector3.one);
            float c=NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz);b.Size[i]=new Vector4(p.Hx,p.Hy,p.Hz,c);b.Rest[i]=new Vector4(p.Rx,p.Ry,p.Rz,0);
            var col=NativeArena.ColorOf(p.Color);if(QualitySettings.activeColorSpace==ColorSpace.Linear)col=col.linear;b.Color[i]=new Vector4(col.r,col.g,col.b,p.Window);b.Look[i]=new Vector4(p.Look,1,p.Seed,p.Flags);
        }
        private void Draw(Batch b,bool instancing)
        {
            if(b.Count==0)return;var shadow=b.Shadow?ShadowCastingMode.On:ShadowCastingMode.Off;Draws++;Instances+=b.Count;
            if(instancing)
            {
                props.Clear();props.SetVectorArray(SizeId,b.Size);props.SetVectorArray(RestId,b.Rest);props.SetVectorArray(ColorId,b.Color);props.SetVectorArray(LookId,b.Look);
                try{Graphics.DrawMeshInstanced(Block,0,lit,b.M,b.Count,props,shadow,true,0,null);return;}catch(InvalidOperationException){}catch(ArgumentException){}
            }
            for(int i=0;i<b.Count;i++){props.Clear();props.SetVector(SizeId,b.Size[i]);props.SetVector(RestId,b.Rest[i]);props.SetVector(ColorId,b.Color[i]);props.SetVector(LookId,b.Look[i]);Graphics.DrawMesh(Block,b.M[i],lit,0,null,0,props,shadow,true);}
        }
        private static Mesh MeshOf(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new System.Collections.Generic.List<Vector4>(n);var b=new System.Collections.Generic.List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);
                a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Factory kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh SceneryMesh(NativeSceneryMesh m,string name)
        {
            int n=m.Vertices;var p=new Vector3[n];var nr=new Vector3[n];var c=new Color[n];var e=new Vector4[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);nr[i]=new Vector3(m.Normals[i*3],m.Normals[i*3+1],m.Normals[i*3+2]);c[i]=new Color(m.Colors[i*4],m.Colors[i*4+1],m.Colors[i*4+2],m.Colors[i*4+3]);e[i]=new Vector4(m.Emission[i*4],m.Emission[i*4+1],m.Emission[i*4+2],m.Emission[i*4+3]);}
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=p;mesh.normals=nr;mesh.colors=c;mesh.SetUVs(1,e);mesh.triangles=m.Indices;
            mesh.bounds=new Bounds(new Vector3((m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2),new Vector3(m.MaxX-m.MinX,m.MaxY-m.MinY,m.MaxZ-m.MinZ));mesh.UploadMeshData(true);return mesh;
        }
        private void Release()
        {
            if(sectors!=null)foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);sectors=null;
            foreach(var o in new UnityEngine.Object[]{trolley,plate,block,blockToon,lit,sceneryLit})if(o!=null)CapturedAnimation.Release(o);trolley=plate=block=blockToon=null;lit=sceneryLit=null;fxDraw?.Dispose();fxDraw=null;
        }
        private void OnDestroy()=>Release();
    }
    public sealed partial class ArenaPresentation
    {
        private NativeFactoryStage factoryStage;
        // SourceWorld (the arena or the killcam replay arena): the Factory's kit, rigs, backdrop and effects; nothing elsewhere.
        private void FactoryScenery(NativeArena arena)
        {
            if(!NativeFactoryScenery.Active(arena.MapId))return;if(factoryStage==null)factoryStage=gameObject.AddComponent<NativeFactoryStage>();
            factoryStage.Blood=SawBlood(arena,NativeFactoryStage.Hazards(arena.MapId));factoryStage.Beams=AdvancedBeams;
            factoryStage.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing,dustCoat);
        }
        // RenderBackdrop: behind the main menu, on the render clock.
        private void FactoryBackdrop(string mapId,float time,Camera camera)
        {
            if(!NativeFactoryScenery.Active(mapId))return;if(factoryStage==null)factoryStage=gameObject.AddComponent<NativeFactoryStage>();
            factoryStage.Beams=null;factoryStage.Backdrop(time,camera,EffectsQuality,LowGraphics);
        }
    }
}
