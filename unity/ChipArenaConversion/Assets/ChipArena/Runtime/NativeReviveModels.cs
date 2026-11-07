using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    // The holographic revive's draws (Core NativeReviveHolo; NativeReviveHoloPresentation.cs) through the shared instanced
    // batches, on one additive material (Resources/NativeReviveHolo.shader): any model's meshes as hardlight (its own meshes,
    // shared), their wireframes (a line mesh of each part's feature edges from its Core arrays, built the first time a revive
    // draws that part), the floor ring and the column. Per instance: _BodyTint the colour and opacity, _SceneTint the sweep
    // plane, _GlowTint the band, fill, rim and scanlines (see the shader).
    public sealed partial class NativeSourceModels
    {
        private Material holoMaterial;private bool holoFailed;private Part holoRing,holoColumn;
        private readonly Dictionary<Part,Part> holoParts=new Dictionary<Part,Part>(),wireParts=new Dictionary<Part,Part>();private readonly List<int> wireEdges=new List<int>();
        private static readonly int ReviveTimeId=Shader.PropertyToID("_ReviveTime");
        private Material HoloMaterial()
        {
            if(holoMaterial!=null||holoFailed)return holoMaterial;var shader=Resources.Load<Shader>("NativeReviveHolo");if(shader==null){holoFailed=true;return null;}
            holoMaterial=new Material(shader){name="Revive hardlight",enableInstancing=true};materials.Add(holoMaterial);return holoMaterial;
        }
        /// <summary>Whether the hardlight shader is present (without it a revive draws the C17 voxel boxes as before).</summary>
        public bool HoloReady=>HoloMaterial()!=null;
        /// <summary>The scanlines' clock (presentation time), once per frame.</summary>
        public void HoloClock(float time){var m=HoloMaterial();if(m!=null)m.SetFloat(ReviveTimeId,time);}
        private Part HoloTwin(Part p){if(!holoParts.TryGetValue(p,out var t)){t=new Part{Name=p.Name,Bone=p.Bone,Mesh=p.Mesh,Material=holoMaterial,NoShadow=true};holoParts[p]=t;}return t;}
        /// <summary>A part's wireframe (null: no Core arrays or no edges), built once.</summary>
        private Part WireTwin(Part p)
        {
            if(wireParts.TryGetValue(p,out var t))return t;
            if(p.Core!=null&&p.Core.Positions.Count>0)
            {
                NativeReviveHolo.Edges(p.Core,wireEdges);
                if(wireEdges.Count>0)
                {
                    int n=p.Core.Positions.Count;var vertices=new Vector3[n];var normals=new Vector3[n];
                    for(int i=0;i<n;i++){var v=p.Core.Positions[i];vertices[i]=new Vector3(v.X,v.Y,v.Z);var k=i<p.Core.Normals.Count?p.Core.Normals[i]:System.Numerics.Vector3.UnitY;normals[i]=new Vector3(k.X,k.Y,k.Z);}
                    var mesh=new Mesh{name="Revive wire "+p.Name,indexFormat=n>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=vertices;mesh.normals=normals;
                    mesh.SetIndices(wireEdges.ToArray(),MeshTopology.Lines,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);
                    t=new Part{Name=p.Name+" wire",Bone=p.Bone,Mesh=mesh,Material=holoMaterial,NoShadow=true};
                }
            }
            wireParts[p]=t;return t;
        }
        /// <summary>
        /// Model `id`'s meshes as hardlight on `bone`'s matrices (or their wireframes), each joint at `opacity`(bone) x the colour's
        /// alpha (null: all at 1; joints at 0 are skipped). `sweep` (xyz the unit normal, w the offset) places the bright band and
        /// `shape` is (band half width m, fill outside the band, rim share, scanlines per m).
        /// </summary>
        public void QueueHolo(string id,System.Func<int,Matrix4x4> bone,System.Func<int,float> opacity,Color color,Vector4 sweep,Vector4 shape,bool wire=false)
        {
            if(id==null||color.a<=.002f||!models.TryGetValue(id,out var model)||HoloMaterial()==null)return;
            foreach(var part in model)
            {
                if(part.Bone<0||part.Bone>=22)continue;float a=opacity==null?1:opacity(part.Bone);if(a<=.002f)continue;
                var twin=wire?WireTwin(part):HoloTwin(part);if(twin==null)continue;
                Draw(twin,bone!=null?bone(part.Bone):Matrix4x4.identity,new Vector4(color.r,color.g,color.b,color.a*a),new Color(sweep.x,sweep.y,sweep.z,sweep.w),shape,Vector4.one);
            }
        }
        /// <summary>One hardlight ring (Core NativeReviveHolo.Ring: a flat annulus, outer radius 1) or column (Column: an open
        /// cylinder, radius 1, height 1 from its base).</summary>
        public void ReviveGlow(int kind,Matrix4x4 matrix,Color color,Vector4 sweep,Vector4 shape)
        {
            if(color.a<=.002f||HoloMaterial()==null)return;
            if(holoRing==null){holoRing=new Part{Name="revive-ring",Bone=-1,Mesh=Annulus(.92f,1,48),Material=holoMaterial,NoShadow=true};holoColumn=new Part{Name="revive-column",Bone=-1,Mesh=Tube(40),Material=holoMaterial,NoShadow=true};models["revive-ring"]=new[]{holoRing};models["revive-column"]=new[]{holoColumn};}
            Draw(kind==NativeReviveHolo.Ring?holoRing:holoColumn,matrix,new Vector4(color.r,color.g,color.b,color.a),new Color(sweep.x,sweep.y,sweep.z,sweep.w),shape,Vector4.one);
        }
        private static Mesh Annulus(float inner,float outer,int segments)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments,c=Mathf.Cos(a),s=Mathf.Sin(a);v.Add(new Vector3(c*inner,0,s*inner));v.Add(new Vector3(c*outer,0,s*outer));n.Add(Vector3.up);n.Add(Vector3.up);}
            for(int i=0;i<segments;i++){int k=i*2;t.Add(k);t.Add(k+1);t.Add(k+3);t.Add(k);t.Add(k+3);t.Add(k+2);}
            var mesh=new Mesh{name="Revive ring"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private static Mesh Tube(int segments)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var t=new List<int>();
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments,c=Mathf.Cos(a),s=Mathf.Sin(a);v.Add(new Vector3(c,0,s));v.Add(new Vector3(c,1,s));n.Add(new Vector3(c,0,s));n.Add(new Vector3(c,0,s));}
            for(int i=0;i<segments;i++){int k=i*2;t.Add(k);t.Add(k+1);t.Add(k+3);t.Add(k);t.Add(k+3);t.Add(k+2);}
            var mesh=new Mesh{name="Revive column"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetTriangles(t,0);mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        /// <summary>Releases the wireframes (the ring and column are models, released with them).</summary>
        private void ClearReviveModels(){foreach(var t in wireParts.Values)if(t!=null)CapturedAnimation.Release(t.Mesh);wireParts.Clear();holoParts.Clear();}
    }
}
