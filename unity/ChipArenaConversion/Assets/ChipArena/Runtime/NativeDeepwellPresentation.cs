using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Deepwell's presentation (docs/arenas/DEEPWELL.md), drawn only while the map is `rig`: in the arena and the killcam replay
    /// (SourceWorld, on the drawn arena's field clock, so the drill's piston and the vents stand where the simulation's states put
    /// them on every client and in the killcam; the sea outside runs on the same clock). Every other map is untouched. Never sim
    /// data and never a collider.
    ///  - The look (light rig, fog, practical lights, block looks, the anime style, the advanced lighting) is data: MapLooks.json's
    ///    `rig-abyss`, applied by NativeLighting and NativeArenaDressing like any other dressed map.
    ///  - The stage art (docs/arenas/STAGE-ART.md): with the stage bank ChipArenaStageDeepwell, NativeStageArtDraw draws the modelled
    ///    station, the seabed round it and its rig (posed by Core NativeDeepwellArt from the same rig parts) in place of every box.
    ///  - Without the bank: the box kit (Core NativeDeepwellKit) and the box rig (Core NativeDeepwellRig) as NativeBlockLit
    ///    instances, the kit packed once per map into casting and shadowless batches of 500, the rig refilled each frame into its
    ///    own batches (allocation free). While the kit or the art draws, the dressing leaves the glass blocks' own boxes undrawn
    ///    (<see cref="ArenaPresentation.DeepwellKitReady"/>): the panes are the effects'.
    ///  - The effects (Core NativeDeepwellFx): one NativeFxDraw batch; the volumetric beams take the shafts while the look's beams
    ///    are on above the low tier (ArenaPresentation.AdvancedBeams).
    /// </summary>
    public sealed class NativeDeepwellDraw : MonoBehaviour
    {
        private sealed class Batch {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;private Mesh blockToon;
        private Mesh block;private Material lit;private MaterialPropertyBlock props;private bool failed;
        private Batch[] kit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch(),new Batch()};
        private ContentMap built;private NativeDeepwellRig rigBuilder;
        private NativeFxDraw fxDraw;private readonly NativeDeepwellFx fx=new NativeDeepwellFx();
        private NativeStageArtDraw art;private readonly NativeStageArtPose artPose=new NativeStageArtPose();
        /// <summary>The stage art drew last frame (docs/arenas/STAGE-ART.md): the kit's and the rig's boxes and the box-built
        /// backdrop sectors did not.</summary>
        public bool ArtDrawn{get;private set;}
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        /// <summary>The frame's volumetric beam list (null: the effects' billboards).</summary>
        public NativeBeamList Beams;
        /// <summary>Whether the map's kit is built and drawing (the dressing hides the sculpted blocks only then).</summary>
        public bool KitReady{get;private set;}
        /// <summary>The last frame's NativeBlockLit draws and instances (kit and rig), the effect quads and the kit's part count.</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}public int Quads=>fx.Quads.Count;public int KitParts{get;private set;}
        private bool Ensure()
        {
            if(lit!=null)return true;if(failed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null)throw new InvalidOperationException("NativeBlockLit is missing");
                lit=new Material(shader){name="Deepwell kit",enableInstancing=true};props=new MaterialPropertyBlock();block=MeshOf(NativeBlockLooks.Build(0));
                fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Deepwell presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>The map's kit, rig builder and effects (once per map content; ArenaPresentation.PrewarmStage calls it
        /// behind the loading screen). Nothing is drawn.</summary>
        public bool Prepare(NativeArena arena)
        {
            if(arena==null||!Ensure())return false;var map=arena.Map;if(map==null||!NativeDeepwellLooks.Active(map.id))return false;
            if(art==null)art=gameObject.AddComponent<NativeStageArtDraw>();art.Prepare(map,NativeDeepwellArt.Slots,NativeDeepwellArt.Casts);if(ReferenceEquals(map,built))return true;
            built=map;KitReady=false;
            try
            {
                var look=NativeLighting.Look(map);var k=NativeDeepwellKit.Build(map,look);Pack(k.Parts);KitParts=k.Count;
                rigBuilder=new NativeDeepwellRig(NativeDeepwellLooks.Resolve(look.kitLooks),map);fx.Prepare(map);
                KitReady=kit.Length>0;
            }
            catch(Exception e){Debug.LogWarning("Deepwell dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;KitReady=false;}
            return true;
        }
        /// <summary>One arena frame. now: the drawn arena's field clock; quality: effects quality; low: graphics=performance.</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing)
        {
            Draws=Instances=0;if(!Prepare(arena))return;var map=arena.Map;var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            if(rigBuilder!=null)rigBuilder.Build(map.hazards,round,match.Phase,match.FightAt,now,now);
            // The stage art (the bank ChipArenaStageDeepwell): the modelled station, the seabed and the rig in place of every box.
            ArtDrawn=false;if(art!=null&&art.Ready&&rigBuilder!=null){NativeDeepwellArt.Pose(rigBuilder,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);}
            if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}
            Effects(arena,round,now,camera,quality,low);
        }
        private void Effects(NativeArena arena,int round,float now,Camera camera,float quality,bool low)
        {
            if(camera==null||fxDraw==null)return;var t=camera.transform;Vector3 e=t.position,r=t.right,u=t.up;var match=arena.Match;
            fx.Beams=Beams;fx.Build(arena.Map.hazards,round,match.Phase,match.FightAt,now,now,e.x,e.y,e.z,r.x,r.y,r.z,u.x,u.y,u.z,Mathf.Clamp01(quality),low);
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
        // The frame's rig: casters into the first batch, shadowless parts into the last two.
        private void Fill(NativeFactoryKit.Writer parts)
        {
            foreach(var b in rig)b.Count=0;
            for(int i=0;i<parts.Count;i++){ref var p=ref parts.Items[i];Batch b=null;if(p.Shadow){if(rig[0].Count<NativeBlockLooks.Chunk)b=rig[0];}else{for(int k=1;k<3;k++)if(rig[k].Count<NativeBlockLooks.Chunk){b=rig[k];break;}}if(b==null)continue;Put(b,b.Count++,ref p);}
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
            var mesh=new Mesh{name="Deepwell kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
        }
        private void Release()
        {
            KitReady=false;
            foreach(var o in new UnityEngine.Object[]{block,blockToon,lit})if(o!=null)CapturedAnimation.Release(o);block=blockToon=null;lit=null;fxDraw?.Dispose();fxDraw=null;built=null;
        }
        private void OnDestroy()=>Release();
    }
    public sealed partial class ArenaPresentation
    {
        private NativeDeepwellDraw deepwell;
        /// <summary>Whether the Deepwell's kit is drawing (NativeArenaDressing then leaves the sculpted blocks' boxes undrawn).</summary>
        internal static bool DeepwellKitReady{get;private set;}
        // SourceWorld (the arena or the killcam replay arena): the Deepwell's kit, rig, backdrop and effects; nothing elsewhere.
        private void DeepwellScenery(NativeArena arena)
        {
            if(!NativeDeepwellLooks.Active(arena.MapId))return;if(deepwell==null)deepwell=gameObject.AddComponent<NativeDeepwellDraw>();
            deepwell.Beams=AdvancedBeams;deepwell.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);DeepwellKitReady=deepwell.KitReady;
        }
        // PrewarmStage: build them behind the loading screen.
        private bool PrewarmDeepwell(NativeArena arena)
        {
            if(!NativeDeepwellLooks.Active(arena.MapId))return false;if(deepwell==null)deepwell=gameObject.AddComponent<NativeDeepwellDraw>();bool ok=deepwell.Prepare(arena);DeepwellKitReady=deepwell.KitReady;return ok;
        }
    }

    // Deepwell's hazard cues on NativeAudio's existing voices and clips: Core NativeDeepwellLooks.Cues for the window since the last
    // frame (the piston's klaxon and its strike; a vent's rattle and its blast), each with a "deepwell." bank key the owner's effects
    // bank may fill. Map `rig` only; a new round, a rewind or another map restarts the window (each cue lies in exactly one window).
    // Allocation free.
    public sealed partial class NativeAudio
    {
        private readonly FactoryCue[] deepwellCues=new FactoryCue[48],deepwellScratch=new FactoryCue[96];private double deepwellTime=double.NaN;private int deepwellGeneration=-1;
        private void DeepwellCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativeDeepwellLooks.Active(arena.MapId)){deepwellTime=double.NaN;return;}
            var match=arena.Match;double time=arena.Time;if(deepwellGeneration!=arena.Generation){deepwellGeneration=arena.Generation;deepwellTime=double.NaN;}
            bool live=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result;
            if(live&&!double.IsNaN(deepwellTime)&&time>deepwellTime&&time-deepwellTime<1)
            {
                int n=NativeDeepwellLooks.Cues(arena.Map.hazards,NativeStructureFx.RoundIndex(match.Set,match.Round),match.FightAt,deepwellTime,time,deepwellCues,deepwellScratch);
                for(int i=0;i<n;i++)DeepwellCue(deepwellCues[i]);
            }
            deepwellTime=live?time:double.NaN;
        }
        private void DeepwellCue(in FactoryCue c)
        {
            var at=new Vector3(c.At.x,c.At.y,c.At.z);
            switch(c.Kind)
            {
                case NativeDeepwellLooks.CuePistonWarn:Cue("deepwell.piston.warn","countdown",at,.6f,.7f);Cue(null,"install",at,.35f,.55f);break;
                case NativeDeepwellLooks.CuePistonSlam:StructureCue(NativeStructureSound.Land,at,NativeStructureMaterial.Metal,1f);Cue("deepwell.piston.slam","explosion",at,.9f,.6f);Cue(null,"stone",at,.55f,.45f);break;
                case NativeDeepwellLooks.CueVentWarn:Cue("deepwell.vent.warn","water",at,.5f,1.5f);Cue(null,"darkness",at,.2f,2f);break;
                case NativeDeepwellLooks.CueVentBlast:Cue("deepwell.vent.blast","dash",at,.75f,.55f);Cue(null,"water",at,.6f,.5f);break;
            }
        }
    }
}
