using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Brickfall's backdrop and atmosphere (docs/arenas/ARENA_PROPOSAL.md §3.10), drawn only while the map id is "brickfall":
    /// in the arena and the killcam replay (SourceWorld, on the arena's presentation clock, so the train, cranes, birds, smoke,
    /// pigeons and dust match on host, guests and in the killcam) and behind the main menu (RenderBackdrop, on the render
    /// clock, backdrop life only). Everything else is untouched, and nothing runs while intros, previews or the chip draft
    /// own the screen, as none of them reach SourceWorld.
    ///  - Static city: Core/NativeBrickfallScenery.cs baked once into five ChipArena/SourceLit meshes (the near street ring
    ///    with the viaduct and the Mercer Plaza lot, and four sectors), drawn with Graphics.DrawMesh on
    ///    NativeSceneryBuilder.BackdropLayer, which the pixel point lights leave out: no GameObjects, no colliders, no shadow
    ///    casting (they receive the arena's shadows). One mesh per sector is cheaper than instancing some 15,000 distinct boxes.
    ///    Behind the menu (no arena blocks there yet) a plate fills the slab's footprint.
    ///  - Moving parts: the viaduct train (one mesh, one draw while it is on the viaduct) and the two crane jibs (one
    ///    instanced draw of two).
    ///  - Life and air: Core/NativeBrickfallFx.cs quads (flocks, aviation lights, smoke columns; in the arena also sun shafts,
    ///    motes, scraps, pigeons, tarps and collapse dust) through one NativeFxDraw: a solid and an effects batch. The scraps
    ///    fade at the arena's props (refilled each frame, <see cref="BrickfallCover"/>); the drifting dust leaves the destruction
    ///    kit's dust wall its share of the 120 dust billboards (<see cref="BrickfallDustInUse"/>).
    ///  At most 9 draws in the arena (5 + train + jibs + 2; the menu adds the plate). Passes: each lit mesh (sectors, train, jibs)
    ///  draws its forward base pass plus one add pass for the rim light when Unity renders it per pixel (directional lights
    ///  reach every layer), and no point-light passes; while shadows are on, the receiving meshes also enter the camera depth
    ///  pass. The two effect batches are unlit, one pass each. The life and air scale with EffectsQuality and LowGraphics
    ///  (graphics=performance).
    ///  - Not the dust coat: the round's one coat is Core NativeDustCoat (landings and breaks), which NativeArenaDressing
    ///    publishes and NativeBlockLit reads; the same landings reach it next to <see cref="BrickfallStructure"/>.
    /// Structure events do not exist yet (Astra emits them later); the destruction kit's intake, and its dev Sandbox preview,
    /// call <see cref="BrickfallStructure"/> through ArenaPresentation.StructureHeard. Allocation free after the one-time bake.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>graphics=performance, or auto on a low-end GPU (NativeSettings.ApplySettings): atmosphere halves, no sun shafts.</summary>
        public bool LowGraphics;
        /// <summary>
        /// The destruction kit's live landing-dust billboards (NativeStructureFx.PuffCount, read from the kit's last built frame
        /// just before the atmosphere builds): the drifting collapse dust takes only what is left of the §3.10 cap of 120.
        /// </summary>
        public int BrickfallDustInUse;
        private Mesh[] brickfallSectors;private Mesh brickfallTrain,brickfallJib,brickfallPlate;private Material brickfallLit;private NativeFxDraw brickfallDraw;private bool brickfallFailed;
        private NativeBrickfallFx brickfallFx;private NativeFxLight brickfallLight;private NativeBrickfallMemory brickfallMemory;
        private readonly Matrix4x4[] brickfallJibs=new Matrix4x4[2];

        /// <summary>
        /// The structure events' hook for the atmosphere (ARENA_PROPOSAL.md §5.4), called by ArenaPresentation.StructureHeard for
        /// every StructureStress, StructureFall and StructureLand the destruction kit takes in (and its dev Sandbox preview), as
        /// often as the event stays in the list (writes are idempotent on time and place), with the event's Position, Radius,
        /// Amount and Time. Stress and Fall startle the pigeons within 18 m; Land also starts drifting collapse dust (Amount =
        /// energy 0..1). Brickfall only; anything else is ignored. The dust coat takes the same landings in StructureHeard.
        /// </summary>
        public void BrickfallStructure(NativeArena arena,NativeBrickfallCue cue,Vector3 position,float radius,float amount,float time)
        {
            if(arena==null||!NativeBrickfallScenery.Active(arena.MapId)||!EnsureBrickfall())return;
            if(cue==NativeBrickfallCue.Land)brickfallMemory.Collapse(time,position.x,position.y,position.z,radius,amount);else brickfallMemory.Spook(time,position.x,position.y,position.z);
        }
        // SourceWorld: the arena or the killcam replay arena.
        private void BrickfallScenery(NativeArena arena)
        {
            if(!NativeBrickfallScenery.Active(arena.MapId)){BrickfallDormant();return;}
            if(!EnsureBrickfall())return;float now=PresentationClock(arena);
            // The live arena's clock running back means a new match; the killcam replays the past on the same clock and keeps it.
            if(!arena.ReplicaScene.IsValid()&&now+5<brickfallMemory.Newest)brickfallMemory.Clear();
            foreach(var e in arena.Events)if(e.Kind==EventKind.Explosion)brickfallMemory.Spook(e.Time,e.Position.x,e.Position.y,e.Position.z);
            var match=arena.Match;double roundStart=NativeBrickfallAtmosphere.RoundStart(match.Phase,match.FightAt,match.PhaseEnd);
            BrickfallCover(arena);DrawBrickfall(now,WorldEye,true,roundStart);
        }
        /// <summary>
        /// The paper scraps' cover: every prop's world bounding box (props are unit cubes scaled to their block size), refilled
        /// each frame from the arena drawn, so falling pieces and the killcam replay's props count too. If static blocks later
        /// leave arena.Props (ARENA_PROPOSAL.md §5.3), they belong here as well.
        /// </summary>
        private void BrickfallCover(NativeArena arena)
        {
            var cover=brickfallFx.Cover;cover.Clear();
            foreach(var p in arena.Props)
            {
                if(p==null)continue;var m=p.transform.localToWorldMatrix;
                float ex=.5f*(Mathf.Abs(m.m00)+Mathf.Abs(m.m01)+Mathf.Abs(m.m02)),ey=.5f*(Mathf.Abs(m.m10)+Mathf.Abs(m.m11)+Mathf.Abs(m.m12)),ez=.5f*(Mathf.Abs(m.m20)+Mathf.Abs(m.m21)+Mathf.Abs(m.m22));
                cover.Add(m.m03-ex,m.m13-ey,m.m23-ez,m.m03+ex,m.m13+ey,m.m23+ez);
            }
        }
        // RenderBackdrop: behind the main menu, on the render clock; no atmosphere, no dust.
        private void BrickfallBackdrop(string mapId,float time,Camera camera)
        {
            if(!NativeBrickfallScenery.Active(mapId)){BrickfallDormant();return;}
            if(!EnsureBrickfall())return;brickfallFx.Cover.Clear();DrawBrickfall(time,camera,false,0);
        }
        private void BrickfallDormant(){brickfallMemory?.Clear();}
        private bool EnsureBrickfall()
        {
            if(brickfallSectors!=null)return true;if(brickfallFailed)return false;Mesh[] sectors=null;
            try
            {
                var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("NativeSourceLit shader is missing");
                brickfallLit=new Material(shader){name="Brickfall scenery",enableInstancing=true};brickfallLit.SetFloat("_StageLit",0);
                var built=NativeBrickfallScenery.Build();sectors=new Mesh[built.Length];for(int i=0;i<built.Length;i++)sectors[i]=BrickfallMesh(built[i],"Brickfall scenery "+i);
                brickfallTrain=BrickfallMesh(NativeBrickfallScenery.BuildTrain(),"Brickfall train");brickfallJib=BrickfallMesh(NativeBrickfallScenery.BuildJib(),"Brickfall crane jib");brickfallPlate=BrickfallMesh(NativeBrickfallScenery.BuildMenuPlate(),"Brickfall menu plate");
                brickfallDraw=new NativeFxDraw();brickfallFx=new NativeBrickfallFx();brickfallLight=new NativeFxLight();brickfallMemory=new NativeBrickfallMemory();brickfallSectors=sectors;return true;
            }
            catch(System.Exception e)
            {
                Debug.LogWarning("Brickfall scenery disabled: "+e.Message);if(sectors!=null)foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);
                DisposeBrickfall();brickfallFailed=true;return false;
            }
        }
        private static Mesh BrickfallMesh(NativeSceneryMesh m,string name)
        {
            int n=m.Vertices;var p=new Vector3[n];var nr=new Vector3[n];var c=new Color[n];var e=new Vector4[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);nr[i]=new Vector3(m.Normals[i*3],m.Normals[i*3+1],m.Normals[i*3+2]);c[i]=new Color(m.Colors[i*4],m.Colors[i*4+1],m.Colors[i*4+2],m.Colors[i*4+3]);e[i]=new Vector4(m.Emission[i*4],m.Emission[i*4+1],m.Emission[i*4+2],m.Emission[i*4+3]);}
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=p;mesh.normals=nr;mesh.colors=c;mesh.SetUVs(1,e);mesh.triangles=m.Indices;
            mesh.bounds=new Bounds(new Vector3((m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2),new Vector3(m.MaxX-m.MinX,m.MaxY-m.MinY,m.MaxZ-m.MinZ));mesh.UploadMeshData(true);return mesh;
        }
        private void DrawBrickfall(float time,Camera camera,bool atmosphere,double roundStart)
        {
            const int backdrop=NativeSceneryBuilder.BackdropLayer;
            foreach(var mesh in brickfallSectors)Graphics.DrawMesh(mesh,Matrix4x4.identity,brickfallLit,backdrop,null,0,null,ShadowCastingMode.Off,true);
            if(!atmosphere)Graphics.DrawMesh(brickfallPlate,Matrix4x4.identity,brickfallLit,backdrop,null,0,null,ShadowCastingMode.Off,true);
            if(NativeBrickfallScenery.Train(time,out double head,out bool east))
                Graphics.DrawMesh(brickfallTrain,Matrix4x4.TRS(new Vector3((float)head,(float)NativeBrickfallScenery.RailY,(float)(east?NativeBrickfallScenery.TrainNorthZ:NativeBrickfallScenery.TrainSouthZ)),Quaternion.Euler(0,east?0:180,0),Vector3.one),brickfallLit,backdrop,null,0,null,ShadowCastingMode.Off,true);
            for(int c=0;c<2;c++)brickfallJibs[c]=Matrix4x4.TRS(new Vector3((float)NativeBrickfallScenery.CraneX[c],(float)NativeBrickfallScenery.MastHeight,(float)NativeBrickfallScenery.CraneZ[c]),Quaternion.Euler(0,(float)NativeBrickfallScenery.CraneSlew(c,time),0),Vector3.one);
            if(UsesInstancing)Graphics.DrawMeshInstanced(brickfallJib,0,brickfallLit,brickfallJibs,2,null,ShadowCastingMode.Off,true,backdrop,null);
            else for(int c=0;c<2;c++)Graphics.DrawMesh(brickfallJib,brickfallJibs[c],brickfallLit,backdrop,null,0,null,ShadowCastingMode.Off,true);
            if(camera==null)return;
            ReadBrickfallLight();BrickfallDustInUse=structureFx!=null?structureFx.Fx.PuffCount:0;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            brickfallFx.Build(time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,brickfallLight,Mathf.Clamp01(EffectsQuality),LowGraphics,atmosphere,brickfallMemory,roundStart,BrickfallDustInUse);
            var q=brickfallFx.Quads;brickfallDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=q.Layers[i]==0?brickfallDraw.Solid:brickfallDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), so fog blends in linear light.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            brickfallDraw.Draw(camera,null,time);
        }
        // The scene's key light and trilight ambient in linear values, as Unity lights the SourceLit meshes around these pieces.
        private void ReadBrickfallLight()
        {
            var sun=RenderSettings.sun;var l=brickfallLight;
            if(sun!=null&&sun.isActiveAndEnabled){var d=-sun.transform.forward;Color c=GraphicsSettings.lightsUseLinearIntensity?sun.color.linear*sun.intensity:(sun.color*sun.intensity).linear;l.Lx=d.x;l.Ly=d.y;l.Lz=d.z;l.Sr=c.r;l.Sg=c.g;l.Sb=c.b;}
            else{l.Lx=0;l.Ly=1;l.Lz=0;l.Sr=l.Sg=l.Sb=0;}
            bool tri=RenderSettings.ambientMode==AmbientMode.Trilight;Color sky=(tri?RenderSettings.ambientSkyColor:RenderSettings.ambientLight).linear,eq=(tri?RenderSettings.ambientEquatorColor:RenderSettings.ambientLight).linear,gr=(tri?RenderSettings.ambientGroundColor:RenderSettings.ambientLight).linear;
            l.Ambient[0]=sky.r;l.Ambient[1]=sky.g;l.Ambient[2]=sky.b;l.Ambient[3]=eq.r;l.Ambient[4]=eq.g;l.Ambient[5]=eq.b;l.Ambient[6]=gr.r;l.Ambient[7]=gr.g;l.Ambient[8]=gr.b;
        }
        private void DisposeBrickfall()
        {
            if(brickfallSectors!=null)foreach(var m in brickfallSectors)CapturedAnimation.Release(m);brickfallSectors=null;CapturedAnimation.Release(brickfallTrain);CapturedAnimation.Release(brickfallJib);CapturedAnimation.Release(brickfallPlate);CapturedAnimation.Release(brickfallLit);
            brickfallTrain=brickfallJib=brickfallPlate=null;brickfallLit=null;brickfallDraw?.Dispose();brickfallDraw=null;
        }
    }
}
