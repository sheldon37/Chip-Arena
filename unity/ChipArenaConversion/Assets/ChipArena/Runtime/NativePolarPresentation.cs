using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Polaris Station's presentation (docs/arenas/POLAR.md), drawn only while the map is `polar`: in the arena and the killcam
    /// replay (SourceWorld, on the drawn arena's field clock, so the dish, the balloon, the vents and the thin ice stand where
    /// the replicated state puts them on every client and in the killcam). Every other map is untouched. Never sim data and never
    /// a collider.
    ///  - The look (light rig, sky, fog, practical lights, block looks, the anime style, the advanced lighting, the snow) is data:
    ///    MapLooks.json's `polar-aurora`, applied by NativeLighting and NativeArenaDressing like any other dressed map.
    ///  - The stage art (docs/arenas/STAGE-ART.md): with the stage bank ChipArenaStagePolar, NativeStageArtDraw draws the modelled
    ///    station, its rig (posed by Core NativePolarArt from the rig's own parts) and the modelled backdrop in place of every box
    ///    below; without the bank the boxes draw.
    ///  - The box kit (Core NativePolarKit) and rig (Core NativePolarRig) as NativeBlockLit instances: the kit packed once per map
    ///    into casting and shadowless batches of 500, the rig refilled each frame into its own five batches (allocation free).
    ///  - The box backdrop (Core NativePolarScenery): ChipArena/SourceLit sector meshes on NativeSceneryBuilder.BackdropLayer
    ///    without shadows.
    ///  - The effects (Core NativePolarFx: the aurora, snow, steam, the hazards' telegraphs): one NativeFxDraw batch; the
    ///    volumetric beams take the searchlight's shaft while the look's beams are on above the low tier.
    /// </summary>
    public sealed class NativePolarDraw : MonoBehaviour
    {
        private sealed class Batch {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;private Mesh blockToon;
        private Mesh block;private Material lit,sceneryLit;private MaterialPropertyBlock props;private bool failed;
        private Batch[] kit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch{Shadow=true},new Batch(),new Batch(),new Batch()};
        private ContentMap built;private NativePolarRig rigBuilder;private Mesh[] sectors=Array.Empty<Mesh>();
        private NativeFxDraw fxDraw;private readonly NativePolarFx fx=new NativePolarFx();
        private NativeStageArtDraw art;private readonly NativeStageArtPose artPose=new NativeStageArtPose();private readonly int[] segState=new int[8];private readonly float[] segT0=new float[8];
        /// <summary>The stage art drew last frame (docs/arenas/STAGE-ART.md): the kit's and the rig's boxes and the box-built
        /// backdrop sectors did not.</summary>
        public bool ArtDrawn{get;private set;}
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        /// <summary>The frame's volumetric beam list (null: the effects' billboards).</summary>
        public NativeBeamList Beams;
        /// <summary>Whether the map's kit is built.</summary>
        public bool KitReady{get;private set;}
        /// <summary>The last frame's NativeBlockLit draws and instances (kit and rig), the effect quads and the kit's part count.</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}public int Quads=>fx.Quads.Count;public int KitParts{get;private set;}
        private bool Ensure()
        {
            if(lit!=null)return true;if(failed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");var source=Resources.Load<Shader>("NativeSourceLit");if(shader==null||source==null)throw new InvalidOperationException("NativeBlockLit or NativeSourceLit is missing");
                lit=new Material(shader){name="Polaris kit",enableInstancing=true};props=new MaterialPropertyBlock();block=MeshOf(NativeBlockLooks.Build(0));
                sceneryLit=new Material(source){name="Polaris scenery",enableInstancing=true};sceneryLit.SetFloat("_StageLit",0);sceneryLit.SetFloat("_ToonEdgeRef",16);fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Polaris presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>The map's kit, rig builder, backdrop and lamps (once per map content; ArenaPresentation.PrewarmStage calls it
        /// behind the loading screen). Nothing is drawn.</summary>
        public bool Prepare(NativeArena arena)
        {
            if(arena==null||!Ensure())return false;var map=arena.Map;if(map==null||!NativePolarLooks.Active(map.id))return false;
            if(art==null)art=gameObject.AddComponent<NativeStageArtDraw>();art.Prepare(map,NativePolarArt.Slots,NativePolarArt.Casts);if(ReferenceEquals(map,built))return true;
            built=map;KitReady=false;
            try
            {
                var look=NativeLighting.Look(map);var k=NativePolarKit.Build(map,look);Pack(k.Parts);KitParts=k.Count;
                rigBuilder=new NativePolarRig(NativePolarLooks.Resolve(look.kitLooks),map);fx.Prepare(map);
                foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);var meshes=NativePolarScenery.Build(map);sectors=new Mesh[meshes.Length];for(int i=0;i<meshes.Length;i++)sectors[i]=SceneryMesh(meshes[i],"Polaris scenery "+i);
                KitReady=kit.Length>0;
            }
            catch(Exception e){Debug.LogWarning("Polaris dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;sectors=Array.Empty<Mesh>();KitReady=false;}
            return true;
        }
        /// <summary>One arena frame. now: the drawn arena's field clock; quality: effects quality; low: graphics=performance.</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing)
        {
            Draws=Instances=0;if(!Prepare(arena))return;var map=arena.Map;var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            const int layer=NativeSceneryBuilder.BackdropLayer;
            // the thin ice's replicated state (its segments' own schedule), read once for the rig and the effects
            int segs=Math.Min(segState.Length,map.segments?.Length??0);
            for(int i=0;i<segState.Length;i++){segState[i]=0;segT0[i]=0;if(i<segs&&arena.Structures!=null){var f=arena.Structures.Get(map.segments[i].id);segState[i]=f.state;segT0[i]=f.t0;}}
            if(rigBuilder!=null)rigBuilder.Build(map,segState,segT0,round,match.Phase,match.FightAt,now);
            // The stage art (the bank ChipArenaStagePolar): the modelled station, its rig and its backdrop, in place of the kit's and
            // the rig's boxes and the box-built backdrop sectors.
            ArtDrawn=false;if(art!=null&&art.Ready&&rigBuilder!=null){NativePolarArt.Pose(rigBuilder,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);}
            if(!ArtDrawn)for(int i=0;i<sectors.Length;i++)Graphics.DrawMesh(sectors[i],Matrix4x4.identity,sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);
            if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}
            Effects(arena,round,now,camera,quality,low);
        }
        private void Effects(NativeArena arena,int round,float now,Camera camera,float quality,bool low)
        {
            if(camera==null||fxDraw==null)return;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;var match=arena.Match;
            fx.Beams=Beams;fx.Build(arena.Map,segState,segT0,round,match.Phase,match.FightAt,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(quality),low);
            var q=fx.Quads;fxDraw.Clear();
            for(int i=0;i<q.Count;i++)
            {
                var target=q.Layers[i]==0?fxDraw.Solid:fxDraw.Effects;int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=target.Count;
                for(int v=0;v<4;v++)target.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                target.Triangle(a,a+1,a+2);target.Triangle(a,a+2,a+3);
            }
            fxDraw.Draw(camera,null,now);
        }
        private void Pack(NativeFactoryKit.Writer parts)
        {
            int casters=0,flat=0;for(int i=0;i<parts.Count;i++)if(parts.Items[i].Shadow)casters++;else flat++;
            int nc=(casters+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk,nf=(flat+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;kit=new Batch[nc+nf];
            for(int i=0;i<kit.Length;i++)kit[i]=new Batch{Shadow=i<nc};int ci=0,fi=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];var b=p.Shadow?kit[ci/NativeBlockLooks.Chunk]:kit[nc+fi/NativeBlockLooks.Chunk];if(p.Shadow)ci++;else fi++;Put(b,b.Count++,ref p);}
        }
        // The frame's rig: casters into the first two batches, shadowless parts into the last three.
        private void Fill(NativeFactoryKit.Writer parts)
        {
            foreach(var b in rig)b.Count=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];Batch b=null;if(p.Shadow){for(int k=0;k<2;k++)if(rig[k].Count<NativeBlockLooks.Chunk){b=rig[k];break;}}else{for(int k=2;k<5;k++)if(rig[k].Count<NativeBlockLooks.Chunk){b=rig[k];break;}}if(b==null)continue;Put(b,b.Count++,ref p);}
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
            var mesh=new Mesh{name="Polaris kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
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
            foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);sectors=Array.Empty<Mesh>();KitReady=false;
            foreach(var o in new UnityEngine.Object[]{block,blockToon,lit,sceneryLit})if(o!=null)CapturedAnimation.Release(o);block=blockToon=null;lit=sceneryLit=null;fxDraw?.Dispose();fxDraw=null;built=null;
        }
        private void OnDestroy()=>Release();
    }
    public sealed partial class ArenaPresentation
    {
        private NativePolarDraw polar;
        // SourceWorld (the arena or the killcam replay arena): Polaris Station's art or kit, rig, backdrop and effects; nothing elsewhere.
        private void PolarScenery(NativeArena arena)
        {
            if(!NativePolarLooks.Active(arena.MapId))return;if(polar==null)polar=gameObject.AddComponent<NativePolarDraw>();
            polar.Beams=AdvancedBeams;polar.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);
        }
        // PrewarmStage: build them behind the loading screen.
        private bool PrewarmPolar(NativeArena arena)
        {
            if(!NativePolarLooks.Active(arena.MapId))return false;if(polar==null)polar=gameObject.AddComponent<NativePolarDraw>();return polar.Prepare(arena);
        }
    }

    // Polaris Station's vent cues on NativeAudio's existing voices and clips: Core NativePolarLooks.Cues for the window since the
    // last frame (a vent's hiss as its warning begins, its roar as it bursts), each with a "polar." bank key the owner's effects
    // bank may fill. The thin ice cracks and falls through the destruction kit's own structure cues. Map `polar` only; a new round,
    // a rewind or another map restarts the window (each cue lies in exactly one window). Allocation free.
    public sealed partial class NativeAudio
    {
        private readonly FactoryCue[] polarCues=new FactoryCue[16],polarScratch=new FactoryCue[32];private double polarTime=double.NaN;private int polarGeneration=-1;
        private void PolarCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativePolarLooks.Active(arena.MapId)){polarTime=double.NaN;return;}
            var match=arena.Match;double time=arena.Time;if(polarGeneration!=arena.Generation){polarGeneration=arena.Generation;polarTime=double.NaN;}
            bool live=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result;
            if(live&&!double.IsNaN(polarTime)&&time>polarTime&&time-polarTime<1)
            {
                int n=NativePolarLooks.Cues(arena.Map.hazards,NativeStructureFx.RoundIndex(match.Set,match.Round),match.FightAt,polarTime,time,polarCues,polarScratch);
                for(int i=0;i<n;i++)
                {
                    var c=polarCues[i];var at=new Vector3(c.At.x,c.At.y,c.At.z);
                    if(c.Kind==NativePolarLooks.CueVentHiss){Cue("polar.vent.hiss","water",at,.42f,1.8f);Cue(null,"darkness",at,.16f,2.2f);}
                    else{Cue("polar.vent.burst","water",at,.8f,1.1f);Cue(null,"fire",at,.34f,.55f);Cue(null,"dash",at,.3f,.5f);}
                }
            }
            polarTime=live?time:double.NaN;
        }
    }
}
