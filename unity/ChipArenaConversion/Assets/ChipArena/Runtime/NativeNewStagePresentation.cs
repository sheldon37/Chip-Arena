using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The five M1 stages' presentation (docs/arenas/NEW-STAGES.md "Stage looks (presentation)"), drawn only while the map is
    /// Breakwater, Pier 9, Skyframe, Deepwell or Polaris Station: in the arena and the killcam replay (SourceWorld, on the drawn
    /// arena's field clock, so the rigs stand where the simulation's states put them on every client and in the killcam). Every
    /// other map is untouched: nothing is created until such a frame, and another map's frame draws nothing. Never sim data and
    /// never a collider.
    ///  - The look (light rig, sky, fog, practical lights, block looks, the style and the advanced lighting and weather) is data:
    ///    MapLooks.json's entry for the map, applied by NativeLighting and NativeArenaDressing like any other dressed map.
    ///  - The static kit (Core NativeNewStageKit: detail on the blocks and the overhead structure) and the rigs (Core
    ///    NativeNewStageRig, posed from the replicated hazard state) as NativeBlockLit instances: the kit packed once per map into
    ///    casting and shadowless batches of 500 (the parts of blocks the kit bank dresses stop drawing with their boxes), the rig
    ///    refilled each frame into its own five batches (allocation free).
    ///  - The backdrop (Core NativeNewStageScenery): ChipArena/SourceLit sector meshes on NativeSceneryBuilder.BackdropLayer
    ///    without shadows; with or without the kit bank (its yards, NativePackStage, only add to it).
    ///  - The effects (Core NativeNewStageFx): one NativeFxDraw batch; the volumetric beams take the lighthouse, floodlights and
    ///    telegraph cones while the look's beams are on above the low tier (ArenaPresentation.AdvancedBeams).
    /// </summary>
    public sealed class NativeNewStageDraw : MonoBehaviour
    {
        private sealed class Batch {public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];public int Count;public bool Shadow;}
        // The anime map style draws the rounded box (Core NativeToonMesh) in place of the legacy S0 mesh, made once on first use.
        private Mesh Block=>NativeLighting.StyleActive?(blockToon??=MeshOf(NativeToonMesh.Build())):block;private Mesh blockToon;
        private Mesh block;private Material lit,sceneryLit;private MaterialPropertyBlock props;private bool failed;
        private Batch[] kit=Array.Empty<Batch>();private readonly Batch[] rig={new Batch{Shadow=true},new Batch{Shadow=true},new Batch{Shadow=true},new Batch(),new Batch()};
        private ContentMap built;private NativeNewStageRig rigBuilder;private Mesh[] sectors=Array.Empty<Mesh>();
        private NativeFxDraw fxDraw;private readonly NativeNewStageFx fx=new NativeNewStageFx();
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        /// <summary>The frame's volumetric beam list (null: the effects' billboards).</summary>
        public NativeBeamList Beams;
        /// <summary>The last frame's NativeBlockLit draws and instances (kit and rig), the effect quads.</summary>
        public int Draws{get;private set;}public int Instances{get;private set;}public int Quads=>fx.Quads.Count;
        private bool Ensure()
        {
            if(lit!=null)return true;if(failed)return false;
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");var source=Resources.Load<Shader>("NativeSourceLit");if(shader==null||source==null)throw new InvalidOperationException("NativeBlockLit or NativeSourceLit is missing");
                lit=new Material(shader){name="New stage kit",enableInstancing=true};props=new MaterialPropertyBlock();block=MeshOf(NativeBlockLooks.Build(0));
                sceneryLit=new Material(source){name="New stage scenery",enableInstancing=true};sceneryLit.SetFloat("_StageLit",0);sceneryLit.SetFloat("_ToonEdgeRef",16);fxDraw=new NativeFxDraw();return true;
            }
            catch(Exception e){Debug.LogWarning("New stage presentation disabled: "+e.Message);Release();failed=true;return false;}
        }
        /// <summary>The map's kit, rig builder, backdrop and lamps (once per map content; ArenaPresentation.PrewarmStage calls it
        /// behind the loading screen). Nothing is drawn.</summary>
        public bool Prepare(NativeArena arena)
        {
            if(arena==null||!Ensure())return false;var map=arena.Map;if(map==null||!NativeNewStageLooks.Active(map.id))return false;if(ReferenceEquals(map,built))return true;
            built=map;
            try
            {
                var look=NativeLighting.Look(map);var k=NativeNewStageKit.Build(map,look,arena.Content?.materials);NativePackStage.Strip(map,k.Parts);Pack(k.Parts);
                rigBuilder=new NativeNewStageRig(NativeNewStageLooks.Resolve(look.kitLooks));fx.Prepare(map);
                foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);var meshes=NativeNewStageScenery.Build(map,NativePackStage.Entry(map.id)?.yards?.Length>0);sectors=new Mesh[meshes.Length];for(int i=0;i<meshes.Length;i++)sectors[i]=SceneryMesh(meshes[i],"New stage scenery "+map.id+" "+i);
            }
            catch(Exception e){Debug.LogWarning("New stage dressing disabled: "+e.Message);kit=Array.Empty<Batch>();rigBuilder=null;sectors=Array.Empty<Mesh>();}
            return true;
        }
        /// <summary>One arena frame. now: the drawn arena's field clock; quality: effects quality; low: graphics=performance.</summary>
        public void Render(NativeArena arena,float now,Camera camera,float quality,bool low,bool instancing)
        {
            Draws=Instances=0;if(!Prepare(arena))return;var map=arena.Map;var match=arena.Match;int round=NativeStructureFx.RoundIndex(match.Set,match.Round);
            const int layer=NativeSceneryBuilder.BackdropLayer;
            foreach(var m in sectors)Graphics.DrawMesh(m,Matrix4x4.identity,sceneryLit,layer,null,0,null,ShadowCastingMode.Off,true);
            foreach(var b in kit)Draw(b,instancing);
            if(rigBuilder!=null){rigBuilder.Build(map.id,map.hazards,map.blocks,round,match.Phase,match.FightAt,now,now);Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}
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
        // The static kit into batches of 500 by shadow mode, packed once per map.
        private void Pack(NativeFactoryKit.Writer parts)
        {
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
            var mesh=new Mesh{name="New stage kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*400);mesh.UploadMeshData(true);return mesh;
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
            foreach(var m in sectors)if(m!=null)CapturedAnimation.Release(m);sectors=Array.Empty<Mesh>();
            foreach(var o in new UnityEngine.Object[]{block,blockToon,lit,sceneryLit})if(o!=null)CapturedAnimation.Release(o);block=blockToon=null;lit=sceneryLit=null;fxDraw?.Dispose();fxDraw=null;built=null;
        }
        private void OnDestroy()=>Release();
    }
    public sealed partial class ArenaPresentation
    {
        private NativeNewStageDraw newStage;
        // SourceWorld (the arena or the killcam replay arena): the five M1 stages' kit, rigs, backdrop and effects; nothing elsewhere.
        private void NewStageScenery(NativeArena arena)
        {
            if(!NativeNewStageLooks.Active(arena.MapId))return;if(newStage==null)newStage=gameObject.AddComponent<NativeNewStageDraw>();
            newStage.Beams=AdvancedBeams;newStage.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);
        }
        // PrewarmStage: build them behind the loading screen.
        private bool PrewarmNewStage(NativeArena arena)
        {
            if(!NativeNewStageLooks.Active(arena.MapId))return false;if(newStage==null)newStage=gameObject.AddComponent<NativeNewStageDraw>();return newStage.Prepare(arena);
        }
    }
}
