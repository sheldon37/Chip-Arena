using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Starfall Causeway's presentation (docs/arenas/CAUSEWAY.md), drawn only while the map is `causeway`: in the arena and the
    /// killcam replay (SourceWorld, on the drawn arena's field clock, so the falling stars, the comet and the wind stand where the
    /// simulation's states put them on every client and in the killcam). Every other map is untouched. Never sim data and never
    /// a collider.
    ///  - The look (light rig, sky, fog, practical lights, block looks, the anime style, the advanced lighting) is data: MapLooks.json's
    ///    `causeway-starfall`, applied by NativeLighting and NativeArenaDressing like any other dressed map.
    ///  - The static kit (Core NativeCausewayKit) and the rig (Core NativeCausewayRig) as NativeBlockLit instances: the kit packed
    ///    once per map into casting and shadowless batches of 500, the rig refilled each frame into its own five batches (allocation
    ///    free). While the kit draws, the dressing leaves the sculpted blocks' own boxes undrawn (<see cref="ArenaPresentation.CausewayKitReady"/>).
    ///  - The stage art (docs/arenas/STAGE-ART.md): with the stage bank ChipArenaStageCauseway, NativeStageArtDraw draws the modelled
    ///    bridge, its rig (posed by Core NativeCausewayArt from the same rig parts) and the modelled void in place of the kit's and
    ///    the rig's boxes and the backdrop's box-built sectors; without the bank everything below draws exactly as before.
    ///  - The backdrop (Core NativeCausewayScenery) and the planet: ChipArena/SourceLit meshes on NativeSceneryBuilder.BackdropLayer
    ///    without shadows.
    ///  - The effects (Core NativeCausewayFx): one NativeFxDraw batch; the volumetric beams take the shafts while the look's beams
    ///    are on above the low tier (ArenaPresentation.AdvancedBeams).
    /// </summary>
    public sealed class NativeCausewayDraw : MonoBehaviour
    {
        private sealed class Batch {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;private Mesh blockToon;
        private Mesh block;private Material lit,sceneryLit;private MaterialPropertyBlock props;private bool failed;
        private Batch[] kit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch{Shadow=true},new Batch(),new Batch(),new Batch()};
        private ContentMap built;private NativeCausewayRig rigBuilder;private Mesh[] sectors=Array.Empty<Mesh>();
        private NativeFxDraw fxDraw;private readonly NativeCausewayFx fx=new NativeCausewayFx();
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
                var shader=Resources.Load<Shader>("NativeBlockLit");var source=Resources.Load<Shader>("NativeSourceLit");if(shader==null||source==null)throw new InvalidOperationException("NativeBlockLit or NativeSourceLit is missing");
                lit=new Material(shader){name="Causeway kit",enableInstancing=true};props=new MaterialPropertyBlock();block=MeshOf(NativeBlockLooks.Build(0));
                sceneryLit=new Material(source){name="Causeway scenery",enableInstancing=true};sceneryLit.SetFloat("_StageLit",0);sceneryLit.SetFloat("_ToonEdgeRef",16);fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("Causeway presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>The map's kit, rig builder, backdrop and lamps (once per map content; ArenaPresentation.PrewarmStage calls it
        /// behind the loading screen). Nothing is drawn.</summary>
        public bool Prepare(NativeArena arena)
        {
            if(arena==null||!Ensure())return false;var map=arena.Map;if(map==null||!NativeCausewayLooks.Active(map.id))return false;
            if(art==null)art=gameObject.AddComponent<NativeStageArtDraw>();art.Prepare(map,NativeCausewayArt.Slots,NativeCausewayArt.Casts);if(ReferenceEquals(map,built))return true;
            built=map;KitReady=false;
            try
            {
                var look=NativeLighting.Look(map);var k=NativeCausewayKit.Build(map,look);Pack(k.Parts);KitParts=k.Count;
                rigBuilder=new NativeCausewayRig(NativeCausewayLooks.Resolve(look.kitLooks),map);fx.Prepare(map);
                foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);var meshes=NativeCausewayScenery.Build(map);sectors=new Mesh[meshes.Length];for(int i=0;i<meshes.Length;i++)sectors[i]=SceneryMesh(meshes[i],"Causeway scenery "+i);
                KitReady=kit.Length>0;
            }
            catch(Exception e){Debug.LogWarning("Causeway dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;sectors=Array.Empty<Mesh>();KitReady=false;}
            return true;
        }
        /// <summary>One arena frame. now: the drawn arena's field clock; quality: effects quality; low: graphics=performance.</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing)
        {
            Draws=Instances=0;if(!Prepare(arena))return;var map=arena.Map;var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            const int layer=NativeSceneryBuilder.BackdropLayer;
            if(rigBuilder!=null)rigBuilder.Build(map.hazards,round,match.Phase,match.FightAt,now,now);
            // The stage art (the bank ChipArenaStageCauseway): the modelled bridge, its rig and what hangs in the void, in place of
            // the kit's and the rig's boxes and the box-built backdrop sectors; the planet and its rings stay their own meshes.
            ArtDrawn=false;if(art!=null&&art.Ready&&rigBuilder!=null){NativeCausewayArt.Pose(rigBuilder,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);}
            for(int i=0;i<sectors.Length;i++){if(ArtDrawn&&i<NativeCausewayScenery.Sectors)continue;Graphics.DrawMesh(sectors[i],Matrix4x4.identity,sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);}
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
            var mesh=new Mesh{name="Causeway kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
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
        private NativeCausewayDraw causeway;
        /// <summary>Whether the Causeway's kit is drawing (NativeArenaDressing then leaves the sculpted blocks' boxes undrawn).</summary>
        internal static bool CausewayKitReady{get;private set;}
        // SourceWorld (the arena or the killcam replay arena): the Causeway's kit, rig, backdrop and effects; nothing elsewhere.
        private void CausewayScenery(NativeArena arena)
        {
            if(!NativeCausewayLooks.Active(arena.MapId))return;if(causeway==null)causeway=gameObject.AddComponent<NativeCausewayDraw>();
            causeway.Beams=AdvancedBeams;causeway.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);CausewayKitReady=causeway.KitReady;
        }
        // PrewarmStage: build them behind the loading screen.
        private bool PrewarmCauseway(NativeArena arena)
        {
            if(!NativeCausewayLooks.Active(arena.MapId))return false;if(causeway==null)causeway=gameObject.AddComponent<NativeCausewayDraw>();bool ok=causeway.Prepare(arena);CausewayKitReady=causeway.KitReady;return ok;
        }
    }

    // The Causeway's hazard cues on NativeAudio's existing voices and clips: Core NativeCausewayLooks.Cues for the window since the
    // last frame (a falling star's kindling, fall and impact; the comet's warning, danger and pass; the solar wind's rising and its
    // gust), each with a "causeway." bank key the owner's effects bank may fill. Map `causeway` only; a new round, a rewind or another
    // map restarts the window (each cue lies in exactly one window). Allocation free.
    public sealed partial class NativeAudio
    {
        private readonly FactoryCue[] causewayCues=new FactoryCue[48],causewayScratch=new FactoryCue[96];private double causewayTime=double.NaN;private int causewayGeneration=-1;
        private void CausewayCues(NativeArena arena,bool menu)
        {
            if(menu||arena==null||!NativeCausewayLooks.Active(arena.MapId)){causewayTime=double.NaN;return;}
            var match=arena.Match;double time=arena.Time;if(causewayGeneration!=arena.Generation){causewayGeneration=arena.Generation;causewayTime=double.NaN;}
            bool live=match.Phase==NativePhase.Fight||match.Phase==NativePhase.Result;
            if(live&&!double.IsNaN(causewayTime)&&time>causewayTime&&time-causewayTime<1)
            {
                int n=NativeCausewayLooks.Cues(arena.Map.hazards,NativeStructureFx.RoundIndex(match.Set,match.Round),match.FightAt,causewayTime,time,causewayCues,causewayScratch);
                for(int i=0;i<n;i++)CausewayCue(causewayCues[i]);
            }
            causewayTime=live?time:double.NaN;
        }
        private void CausewayCue(in FactoryCue c)
        {
            var at=new Vector3(c.At.x,c.At.y,c.At.z);
            switch(c.Kind)
            {
                case NativeCausewayLooks.CueStarWarn:Cue("causeway.star.warn","shield",at,.55f,1.7f);Cue(null,"darkness",at,.22f,1.9f);break;
                case NativeCausewayLooks.CueStarFall:Cue("causeway.star.fall","dash",at,.85f,.42f);Cue(null,"fire",at,.45f,1.35f);break;
                case NativeCausewayLooks.CueStarImpact:StructureCue(NativeStructureSound.Land,at,NativeStructureMaterial.Masonry,1f);Cue("causeway.star.impact","explosion",at,.9f,.72f);Cue(null,"stone",at,.6f,.5f);break;
                case NativeCausewayLooks.CueCometWarn:Cue("causeway.comet.warn","darkness",at,.55f,.55f);Cue(null,"install",at,.3f,.6f);break;
                case NativeCausewayLooks.CueCometDanger:Cue("causeway.comet.danger","lightning",at,.5f,.7f);Cue(null,"countdown",at,.45f,.8f);break;
                // the pass is heard where it crosses the listener's line along the span
                case NativeCausewayLooks.CueCometPass:{var p=new Vector3(Mathf.Clamp(listener.x,-20,20),at.y,at.z);Cue("causeway.comet.pass","dash",p,1f,.6f);Cue(null,"explosion",p,.6f,1.25f);Cue(null,"lightning",p,.5f,1.4f);break;}
                case NativeCausewayLooks.CueWindWarn:Cue("causeway.wind.warn","water",at,.4f,.6f);Cue(null,"darkness",at,.2f,2.1f);break;
                case NativeCausewayLooks.CueWindGust:Cue("causeway.wind.gust","dash",at,.75f,.5f);Cue(null,"water",at,.55f,.45f);Cue(null,"fire",at,.3f,.6f);break;
            }
        }
    }

    // The solar wind's push (docs/arenas/CAUSEWAY.md "Rules change for Astra"): a vent with `push` > 0 shoves every live pilot inside
    // its column along its `axis` at `push` m/s² (scaled by the burst's level and the pilot's knockback taken) through the burst,
    // through the host's knockback as fields and blasts do. No vent on any other map has a push, so nothing else changes.
    public sealed partial class NativeArena
    {
        private void VentPush(MapHazard h,in FactoryHazardState s)
        {
            if(h.pushMode!=NativeStageHazards.PushWind||s.Phase!=NativeFactoryHazards.Bursting||!(h.push>0))return;var dir=Vec(h.axis);if(dir.sqrMagnitude<1e-6f)return;dir.Normalize();
            float k=(float)(h.push*s.U/60);
            foreach(var p in Pilots)
            {
                if(p.Hp<=0||!p.Seat.connected||KernelLocked(p))continue;if(!NativeStageHazards.Touch(Vec(p.Body.position),s.BoxCenter,s.BoxSize))continue;
                p.Knockback+=dir*k*(float)p.Stats["knockbackTaken"];
            }
        }
    }
}
