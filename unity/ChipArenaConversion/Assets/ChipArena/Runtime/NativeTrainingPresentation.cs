using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The Training Room's presentation (docs/arenas/NEW-STAGES.md "Training Room presentation"), drawn only while the map id is
    /// "training", in the arena and the killcam replay (SourceWorld). Every other map is untouched: nothing is created before
    /// a training frame and a non-training frame draws nothing. Presentation only: never sim data, never a collider, never in
    /// the network frame; it reads the map's authored pads, blocks and hazards and the replicated readouts
    /// (NativeArena.TrainingReadouts(): the host's meters, a guest's TrainingRemote), so host and guests draw the same boards.
    ///  - The look (studio light, fog, block looks, the hazard kit's part looks) is data: MapLooks.json's "training" entry,
    ///    applied by NativeLighting and NativeArenaDressing like any dressed map.
    ///  - Static marks (Core NativeTrainingMarks, built once per map): the floor and wall paint mesh (no outlines) and the
    ///    fixtures mesh (board housings and lettering, hangers, roof troffers; outlined), both ChipArena/SourceLit on
    ///    NativeSceneryBuilder.BackdropLayer; the hall's metre grid and the range's grid from the firing line
    ///    (Resources/NativeTrainingStudio.shader, mode 0, multiplied over the floor).
    ///  - Readouts: one 256 x 168 texture per pad, repainted (Core NativeTrainingReadoutRaster) only when the target's drawn
    ///    values change (NativeTrainingReadoutKey), polled at <see cref="PollHz"/>; shown on the range boards (a cell over each
    ///    lane at each distance) and the ring's board (mode 1), and as a camera-facing tag over a recently hit target (mode 2).
    ///  - The hazard bay's samples (Core NativeTrainingBay): static fittings packed once, the moving rigs and lamps refilled
    ///    each frame, as NativeBlockLit instances with the Factory kit's part looks; their effects as one NativeFxDraw.
    /// Allocation free per frame once built (the host's TrainingReadouts() itself allocates its rows, at the poll rate only).
    /// </summary>
    public sealed class NativeTrainingStage : MonoBehaviour
    {
        /// <summary>How often the readouts are read (Hz): a guest's arrive with each network frame anyway.</summary>
        public const float PollHz=10;
        private sealed class Batch{public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=KitMesh(NativeToonMesh.Build())):block;
        private Mesh block,blockToon,paint,fixtures,quad;private Material lit,paintLit,fixtureLit,grid,screen,tagLit;private MaterialPropertyBlock props,cellProps;private bool failed;
        private ContentMap built;private NativeTrainingMarks marks;private NativeTrainingBay bay;private Batch[] fit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch()};
        private NativeFxDraw fxDraw;private Texture2D[] cells=Array.Empty<Texture2D>();private NativeTrainingReadoutKey[] keys=Array.Empty<NativeTrainingReadoutKey>();private bool[] painted=Array.Empty<bool>();
        private TrainingReadout[] rows=Array.Empty<TrainingReadout>();private readonly NativeTrainingReadoutRaster raster=new NativeTrainingReadoutRaster();
        private NativeArena polled;private float nextPoll;private int strafe=-1;private readonly int[] tagged=new int[NativeTrainingTags.MaxTags];private readonly float[] taggedAt=new float[NativeTrainingTags.MaxTags];
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook"),
            TexId=Shader.PropertyToID("_MainTex"),FadeId=Shader.PropertyToID("_Fade"),FlashId=Shader.PropertyToID("_Flash"),RectId=Shader.PropertyToID("_GridRect"),OriginId=Shader.PropertyToID("_GridOrigin"),CutId=Shader.PropertyToID("_GridCut");
        /// <summary>Readout textures repainted since the stage was built (diagnostics and the native checks).</summary>
        public int Repaints{get;private set;}
        private bool Ensure()
        {
            if(lit!=null)return true;if(failed)return false;
            try
            {
                var blockShader=Resources.Load<Shader>("NativeBlockLit");var source=Resources.Load<Shader>("NativeSourceLit");var studio=Resources.Load<Shader>("NativeTrainingStudio");
                if(blockShader==null||source==null||studio==null||!studio.isSupported)throw new InvalidOperationException("NativeBlockLit, NativeSourceLit or NativeTrainingStudio is missing or unsupported");
                lit=new Material(blockShader){name="Training bay kit",enableInstancing=true};props=new MaterialPropertyBlock();cellProps=new MaterialPropertyBlock();block=KitMesh(NativeBlockLooks.Build(0));
                paintLit=new Material(source){name="Training paint",enableInstancing=true};paintLit.SetFloat("_StageLit",0);paintLit.SetFloat("_ToonEdgeRef",0);
                fixtureLit=new Material(source){name="Training fixtures",enableInstancing=true};fixtureLit.SetFloat("_StageLit",0);fixtureLit.SetFloat("_ToonEdgeRef",16);
                grid=new Material(studio){name="Training grid",renderQueue=2550};grid.SetFloat("_Mode",0);grid.SetFloat("_SrcBlend",(float)BlendMode.DstColor);grid.SetFloat("_DstBlend",(float)BlendMode.Zero);grid.SetFloat("_ZWrite",0);grid.SetOverrideTag("RenderType","Transparent");
                screen=new Material(studio){name="Training readout screens",renderQueue=(int)RenderQueue.Geometry};screen.SetFloat("_Mode",1);screen.SetFloat("_SrcBlend",(float)BlendMode.One);screen.SetFloat("_DstBlend",(float)BlendMode.Zero);screen.SetFloat("_ZWrite",1);
                tagLit=new Material(studio){name="Training readout tags",renderQueue=(int)RenderQueue.Transparent+5};tagLit.SetFloat("_Mode",2);tagLit.SetFloat("_SrcBlend",(float)BlendMode.One);tagLit.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);tagLit.SetFloat("_ZWrite",0);tagLit.SetOverrideTag("RenderType","Transparent");
                quad=new Mesh{name="Training quad"};quad.vertices=new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)};
                quad.uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(1,1),new Vector2(0,1)};quad.normals=new[]{Vector3.back,Vector3.back,Vector3.back,Vector3.back};quad.triangles=new[]{0,2,1,0,3,2};quad.UploadMeshData(true);
                fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Training presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>Builds this map's marks, readout textures and the bay's fittings now (behind the stage loading screen).</summary>
        public bool Prepare(ContentMap map)
        {
            if(map==null||!Ensure())return false;if(ReferenceEquals(map,built))return true;
            try
            {
                ReleaseMap();built=map;marks=NativeTrainingMarks.Build(map);paint=SceneryMesh(marks.Paint,"Training paint");fixtures=SceneryMesh(marks.Fixtures,"Training fixtures");
                var looks=NativeFactoryKit.Resolve(NativeLighting.Look(map).kitLooks);bay=new NativeTrainingBay(looks);Pack(NativeTrainingBay.Fittings(map.hazards,looks));
                int n=marks.Plan.Pads.Length;cells=new Texture2D[n];keys=new NativeTrainingReadoutKey[n];painted=new bool[n];rows=new TrainingReadout[n];strafe=-1;
                for(int i=0;i<n;i++){cells[i]=new Texture2D(NativeTrainingReadoutRaster.Width,NativeTrainingReadoutRaster.Height,TextureFormat.RGBA32,true){name="Training readout "+marks.Plan.Pads[i].Id,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=4};if(marks.Plan.Pads[i].Pad.kind=="strafe")strafe=i;}
                for(int i=0;i<n;i++)Repaint(i,default);
                polled=null;return true;
            }
            catch(Exception e){Debug.LogWarning("Training marks disabled: "+e.Message);ReleaseMap();built=map;marks=null;return false;}
        }
        /// <summary>One arena frame. now: the drawn arena's field clock; positions: the drawn actors' positions (ArenaPresentation's).</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool instancing,Dictionary<int,Vector3> positions)
        {
            if(!Prepare(arena.Map)||marks==null)return;const int backdrop=NativeSceneryBuilder.BackdropLayer;
            Graphics.DrawMesh(paint,Matrix4x4.identity,paintLit,backdrop,null,0,null,ShadowCastingMode.Off,true);
            Graphics.DrawMesh(fixtures,Matrix4x4.identity,fixtureLit,backdrop,null,0,null,ShadowCastingMode.Off,true);
            foreach(var g in marks.Grids)
            {
                props.Clear();props.SetVector(RectId,new Vector4(g.X0,g.Z0,g.X1,g.Z1));props.SetVector(OriginId,new Vector4(g.OriginX,g.OriginZ,g.LinesX,g.LinesZ));props.SetVector(CutId,new Vector4(g.CutX0,g.CutZ0,g.CutX1,g.CutZ1));
                var m=Matrix4x4.TRS(new Vector3((g.X0+g.X1)/2,NativeTrainingLook.GridY,(g.Z0+g.Z1)/2),Quaternion.Euler(90,0,0),new Vector3(g.X1-g.X0,g.Z1-g.Z0,1));
                Graphics.DrawMesh(quad,m,grid,0,null,0,props,ShadowCastingMode.Off,false);
            }
            Readouts(arena,camera,positions);
            // the hazard bay: fittings, the frame's rigs and the strafing carriage, then the effects
            var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);double clock=NativeFactoryHazards.Clock(match.Phase,match.FightAt,now);
            double cx=double.NaN,cz=double.NaN;if(strafe>=0){var at=Anchor(strafe,arena,positions);cx=at.x;cz=at.z;}
            bay.Build(arena.Map.hazards,round,clock,match.Phase,match.FightAt,now,cx,cz);Fill(bay.Parts);
            foreach(var b in fit)Draw(b,instancing);foreach(var b in rig)Draw(b,instancing);
            if(camera==null)return;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;
            bay.Effects(arena.Map.hazards,round,clock,match.Phase,match.FightAt,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,quality);
            var q=bay.Quads;fxDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var layer=fxDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            fxDraw.Draw(camera,null,now);
        }
        // A target's drawn capsule centre: its actor's (smoothed feet, as its body is drawn), else its pad's rail position.
        private Vector3 Anchor(int pad,NativeArena arena,Dictionary<int,Vector3> positions)
        {
            if(positions!=null&&positions.TryGetValue(NativeTrainingRules.FirstDummy+pad,out var p))return p+Vector3.up*NativeTrainingTags.FeetToCentre;
            var v=NativeTrainingRules.Position(marks.Plan.Pads[pad].Pad,Math.Max(0,arena.Time-arena.Match.FightAt));return new Vector3(v.x,v.y,v.z);
        }
        private void Readouts(NativeArena arena,Camera camera,Dictionary<int,Vector3> positions)
        {
            // read the replicated rows at the poll rate; repaint a pad's texture only when what it shows changed
            if(!ReferenceEquals(arena,polled)||Time.unscaledTime>=nextPoll)
            {
                polled=arena;nextPoll=Time.unscaledTime+1/PollHz;for(int i=0;i<rows.Length;i++)rows[i]=null;
                var read=arena.Training?arena.TrainingReadouts():Array.Empty<TrainingReadout>();
                for(int i=0;i<read.Length;i++){var r=read[i];int pad=r==null?-1:r.actor-NativeTrainingRules.FirstDummy;if(pad>=0&&pad<rows.Length&&r.pad==marks.Plan.Pads[pad].Id)rows[pad]=r;}
                for(int i=0;i<rows.Length;i++){var key=NativeTrainingReadoutKey.Of(rows[i]);if(!painted[i]||!key.Same(keys[i]))Repaint(i,key);}
            }
            float time=arena.Time;
            foreach(var c in marks.Cells)
            {
                var r=rows[c.Pad];float flash=keys[c.Pad].Any&&r!=null?NativeTrainingTags.Flash(time-r.at):0;
                cellProps.Clear();cellProps.SetTexture(TexId,cells[c.Pad]);cellProps.SetFloat(FlashId,flash);
                var m=Matrix4x4.TRS(new Vector3(c.Center.x,c.Center.y,c.Center.z),Quaternion.LookRotation(new Vector3(-c.Normal.x,-c.Normal.y,-c.Normal.z),Vector3.up),new Vector3(c.Width,c.Height,1));
                Graphics.DrawMesh(quad,m,screen,0,null,0,cellProps,ShadowCastingMode.Off,false);
            }
            if(camera==null)return;var eye=camera.transform;Vector3 ep=eye.position;
            // the tags: the most recently hit targets still within their hold and fade
            for(int k=0;k<tagged.Length;k++)tagged[k]=-1;
            for(int i=0;i<rows.Length;i++){var r=rows[i];if(r!=null&&keys[i].Any&&NativeTrainingTags.Alpha(true,time-r.at)>.005f)NativeTrainingTags.Keep(tagged,taggedAt,i,r.at);}
            for(int k=0;k<tagged.Length;k++)
            {
                int i=tagged[k];if(i<0)continue;var r=rows[i];float age=time-r.at,alpha=NativeTrainingTags.Alpha(true,age);
                var at=Anchor(i,arena,positions);float h=NativeTrainingTags.Height(Vector3.Distance(ep,at),camera.fieldOfView)*NativeTrainingTags.Scale(age),w=h*NativeTrainingLook.CellAspect;
                cellProps.Clear();cellProps.SetTexture(TexId,cells[i]);cellProps.SetFloat(FadeId,alpha);cellProps.SetFloat(FlashId,NativeTrainingTags.Flash(age));
                var m=Matrix4x4.TRS(at+Vector3.up*(NativeTrainingTags.TagLift+h/2),eye.rotation,new Vector3(w,h,1));
                Graphics.DrawMesh(quad,m,tagLit,0,camera,0,cellProps,ShadowCastingMode.Off,false);
            }
        }
        private void Repaint(int pad,in NativeTrainingReadoutKey key)
        {
            var info=marks.Plan.Pads[pad];raster.Paint(info.Header,info.Tag,key,info.Accent);
            cells[pad].SetPixelData(raster.Pixels,0);cells[pad].Apply(true,false);keys[pad]=key;painted[pad]=true;Repaints++;
        }
        // The static fittings into batches of 500 by shadow mode, packed once.
        private void Pack(NativeFactoryKit.Writer parts)
        {
            int casters=0,flat=0;for(int i=0;i<parts.Count;i++)if(parts.Items[i].Shadow)casters++;else flat++;
            int nc=(casters+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk,nf=(flat+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;fit=new Batch[nc+nf];
            for(int i=0;i<fit.Length;i++)fit[i]=new Batch{Shadow=i<nc};int ci=0,fi=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];var b=p.Shadow?fit[ci/NativeBlockLooks.Chunk]:fit[nc+fi/NativeBlockLooks.Chunk];if(p.Shadow)ci++;else fi++;Put(b,b.Count++,ref p);}
        }
        // The frame's rigs: casters into the first batch, shadowless parts into the second (the bay's capacity fits).
        private void Fill(NativeFactoryKit.Writer parts)
        {
            foreach(var b in rig)b.Count=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];var b=p.Shadow?rig[0]:rig[1];if(b.Count>=NativeBlockLooks.Chunk)continue;Put(b,b.Count++,ref p);}
        }
        private static void Put(Batch b,int i,ref FactoryPart p)
        {
            b.M[i]=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw),Vector3.one);
            float c=NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz);b.Size[i]=new Vector4(p.Hx,p.Hy,p.Hz,c);b.Rest[i]=new Vector4(p.Rx,p.Ry,p.Rz,0);
            var col=NativeArena.ColorOf(p.Color);if(QualitySettings.activeColorSpace==ColorSpace.Linear)col=col.linear;b.Color[i]=new Vector4(col.r,col.g,col.b,p.Window);b.Look[i]=new Vector4(p.Look,1,p.Seed,p.Flags);
        }
        private void Draw(Batch b,bool instancing)
        {
            if(b.Count==0)return;var shadow=b.Shadow?ShadowCastingMode.On:ShadowCastingMode.Off;
            if(instancing)
            {
                props.Clear();props.SetVectorArray(SizeId,b.Size);props.SetVectorArray(RestId,b.Rest);props.SetVectorArray(ColorId,b.Color);props.SetVectorArray(LookId,b.Look);
                try{Graphics.DrawMeshInstanced(Block,0,lit,b.M,b.Count,props,shadow,true,0,null);return;}catch(InvalidOperationException){}catch(ArgumentException){}
            }
            for(int i=0;i<b.Count;i++){props.Clear();props.SetVector(SizeId,b.Size[i]);props.SetVector(RestId,b.Rest[i]);props.SetVector(ColorId,b.Color[i]);props.SetVector(LookId,b.Look[i]);Graphics.DrawMesh(Block,b.M[i],lit,0,null,0,props,shadow,true);}
        }
        private static Mesh KitMesh(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new List<Vector4>(n);var b=new List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);
                a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Training kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh SceneryMesh(NativeSceneryMesh m,string name)
        {
            int n=m.Vertices;var p=new Vector3[n];var nr=new Vector3[n];var c=new Color[n];var e=new Vector4[n];
            for(int i=0;i<n;i++){p[i]=new Vector3(m.Positions[i*3],m.Positions[i*3+1],m.Positions[i*3+2]);nr[i]=new Vector3(m.Normals[i*3],m.Normals[i*3+1],m.Normals[i*3+2]);c[i]=new Color(m.Colors[i*4],m.Colors[i*4+1],m.Colors[i*4+2],m.Colors[i*4+3]);e[i]=new Vector4(m.Emission[i*4],m.Emission[i*4+1],m.Emission[i*4+2],m.Emission[i*4+3]);}
            var mesh=new Mesh{name=name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=p;mesh.normals=nr;mesh.colors=c;mesh.SetUVs(1,e);mesh.triangles=m.Indices;
            mesh.bounds=new Bounds(new Vector3((m.MinX+m.MaxX)/2,(m.MinY+m.MaxY)/2,(m.MinZ+m.MaxZ)/2),new Vector3(m.MaxX-m.MinX,m.MaxY-m.MinY,m.MaxZ-m.MinZ));mesh.UploadMeshData(true);return mesh;
        }
        private void ReleaseMap()
        {
            foreach(var t in cells)if(t!=null)CapturedAnimation.Release(t);cells=Array.Empty<Texture2D>();
            if(paint!=null)CapturedAnimation.Release(paint);if(fixtures!=null)CapturedAnimation.Release(fixtures);paint=fixtures=null;fit=Array.Empty<Batch>();built=null;marks=null;bay=null;polled=null;
        }
        private void Release()
        {
            ReleaseMap();foreach(var o in new UnityEngine.Object[]{block,blockToon,quad,lit,paintLit,fixtureLit,grid,screen,tagLit})if(o!=null)CapturedAnimation.Release(o);
            block=blockToon=quad=null;lit=paintLit=fixtureLit=grid=screen=tagLit=null;fxDraw?.Dispose();fxDraw=null;
        }
        private void OnDestroy()=>Release();
    }
    public sealed partial class ArenaPresentation
    {
        private NativeTrainingStage trainingStage;
        // SourceWorld (the arena or the killcam replay arena): the Training Room's marks, boards, readouts and hazard samples.
        private void TrainingScenery(NativeArena arena)
        {
            if(!NativeTrainingLook.Active(arena.MapId))return;if(trainingStage==null)trainingStage=gameObject.AddComponent<NativeTrainingStage>();
            trainingStage.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,UsesInstancing,visualPositions);
        }
        // PrewarmStage: the marks, readout textures and fittings behind the stage loading screen.
        private bool TrainingPrepare(NativeArena arena)
        {
            if(!NativeTrainingLook.Active(arena.MapId))return false;if(trainingStage==null)trainingStage=gameObject.AddComponent<NativeTrainingStage>();
            return trainingStage.Prepare(arena.Map);
        }
    }
}
