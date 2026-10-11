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
    ///    with the viaduct and the Mercer Plaza lot, and four sectors), drawn with Graphics.DrawMesh on layer 0: no GameObjects,
    ///    no colliders, no shadow casting (they receive the arena's shadows). One mesh per sector is cheaper than instancing
    ///    some 15,000 distinct boxes.
    ///  - Moving parts: the viaduct train (one mesh, one draw while it is on the viaduct) and the two crane jibs (one
    ///    instanced draw of two).
    ///  - Life and air: Core/NativeBrickfallFx.cs quads (flocks, aviation lights, smoke columns; in the arena also sun shafts,
    ///    motes, scraps, pigeons, tarps and collapse dust) through one NativeFxDraw: a solid and an effects batch.
    ///  At most 9 draws (5 + train + jibs + 2; while shadows are on, the receiving meshes also enter Unity's camera depth pass),
    ///  the life and air scaled by EffectsQuality and LowGraphics (graphics=performance).
    ///  - The dust coat: Shader global <see cref="NativeBrickfallAtmosphere.DustCoatGlobal"/> (0..1, rising with this round's
    ///    landings; 0 on every other map and behind the menu) for the block shader to read.
    /// Structure events do not exist yet (the destruction kit emits them later); <see cref="BrickfallStructure"/> is the hook.
    /// Allocation free after the one-time bake.
    /// </summary>
    public sealed partial class ArenaPresentation
    {
        /// <summary>graphics=performance, or auto on a low-end GPU (NativeSettings.ApplySettings): atmosphere halves, no sun shafts.</summary>
        public bool LowGraphics;
        private Mesh[] brickfallSectors;private Mesh brickfallTrain,brickfallJib;private Material brickfallLit;private NativeFxDraw brickfallDraw;private bool brickfallFailed;
        private NativeBrickfallFx brickfallFx;private NativeFxLight brickfallLight;private NativeBrickfallMemory brickfallMemory;
        private readonly Matrix4x4[] brickfallJibs=new Matrix4x4[2];private float brickfallCoat=-1;
        private static readonly int DustCoatId=Shader.PropertyToID(NativeBrickfallAtmosphere.DustCoatGlobal);
        /// <summary>The dust coat last published (0..1).</summary>
        public float BrickfallDustCoat=>Mathf.Max(0,brickfallCoat);

        /// <summary>
        /// The destruction kit's hook (ARENA_PROPOSAL.md §5.4): call once per StructureStress, StructureFall and StructureLand
        /// event, as often as the event stays in the list (writes are idempotent on time and place), with the event's
        /// Position, Radius, Amount and Time. Stress and Fall startle the pigeons within 18 m; Land also starts drifting
        /// collapse dust (Amount = energy 0..1) and raises the dust coat. Brickfall only; anything else is ignored. Example:
        /// foreach(var e in arena.Events)if(e.Kind==EventKind.StructureLand)BrickfallStructure(arena,NativeBrickfallCue.Land,Vec(e.Position),e.Radius,e.Amount,e.Time);
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
            PublishDustCoat((float)NativeBrickfallAtmosphere.DustCoat(brickfallMemory,now,roundStart));
            DrawBrickfall(now,WorldEye,true,roundStart);
        }
        // RenderBackdrop: behind the main menu, on the render clock; no atmosphere, no dust.
        private void BrickfallBackdrop(string mapId,float time,Camera camera)
        {
            if(!NativeBrickfallScenery.Active(mapId)){BrickfallDormant();return;}
            if(!EnsureBrickfall())return;PublishDustCoat(0);DrawBrickfall(time,camera,false,0);
        }
        private void BrickfallDormant(){PublishDustCoat(0);brickfallMemory?.Clear();}
        private void PublishDustCoat(float value){if(value==brickfallCoat)return;brickfallCoat=value;Shader.SetGlobalFloat(DustCoatId,value);}
        private bool EnsureBrickfall()
        {
            if(brickfallSectors!=null)return true;if(brickfallFailed)return false;Mesh[] sectors=null;
            try
            {
                var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("NativeSourceLit shader is missing");
                brickfallLit=new Material(shader){name="Brickfall scenery",enableInstancing=true};brickfallLit.SetFloat("_StageLit",0);
                var built=NativeBrickfallScenery.Build();sectors=new Mesh[built.Length];for(int i=0;i<built.Length;i++)sectors[i]=BrickfallMesh(built[i],"Brickfall scenery "+i);
                brickfallTrain=BrickfallMesh(NativeBrickfallScenery.BuildTrain(),"Brickfall train");brickfallJib=BrickfallMesh(NativeBrickfallScenery.BuildJib(),"Brickfall crane jib");
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
            foreach(var mesh in brickfallSectors)Graphics.DrawMesh(mesh,Matrix4x4.identity,brickfallLit,0,null,0,null,ShadowCastingMode.Off,true);
            if(NativeBrickfallScenery.Train(time,out double head,out bool east))
                Graphics.DrawMesh(brickfallTrain,Matrix4x4.TRS(new Vector3((float)head,(float)NativeBrickfallScenery.RailY,(float)(east?NativeBrickfallScenery.TrainNorthZ:NativeBrickfallScenery.TrainSouthZ)),Quaternion.Euler(0,east?0:180,0),Vector3.one),brickfallLit,0,null,0,null,ShadowCastingMode.Off,true);
            for(int c=0;c<2;c++)brickfallJibs[c]=Matrix4x4.TRS(new Vector3((float)NativeBrickfallScenery.CraneX[c],(float)NativeBrickfallScenery.MastHeight,(float)NativeBrickfallScenery.CraneZ[c]),Quaternion.Euler(0,(float)NativeBrickfallScenery.CraneSlew(c,time),0),Vector3.one);
            if(UsesInstancing)Graphics.DrawMeshInstanced(brickfallJib,0,brickfallLit,brickfallJibs,2,null,ShadowCastingMode.Off,true,0,null);
            else for(int c=0;c<2;c++)Graphics.DrawMesh(brickfallJib,brickfallJibs[c],brickfallLit,0,null,0,null,ShadowCastingMode.Off,true);
            if(camera==null)return;
            ReadBrickfallLight();var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            brickfallFx.Build(time,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,brickfallLight,Mathf.Clamp01(EffectsQuality),LowGraphics,atmosphere,brickfallMemory,roundStart);
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
            if(brickfallSectors!=null)foreach(var m in brickfallSectors)CapturedAnimation.Release(m);brickfallSectors=null;CapturedAnimation.Release(brickfallTrain);CapturedAnimation.Release(brickfallJib);CapturedAnimation.Release(brickfallLit);
            brickfallTrain=brickfallJib=null;brickfallLit=null;brickfallDraw?.Dispose();brickfallDraw=null;PublishDustCoat(0);
        }
    }
}
