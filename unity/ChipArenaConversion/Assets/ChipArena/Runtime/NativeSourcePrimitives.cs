using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // The four primitives weapon-visuals.ts:9 builds chip modules, enchants and the muzzle flash from (three r180 BoxGeometry,
    // SphereGeometry(1,8,6), ConeGeometry(1,1,5), TorusGeometry(1,.055,4,16)), built once at runtime and converted like the
    // weapon exports (tools/export_models.mjs: x=-x, nx=-nx, swapped winding). They share the source-model batches:
    // NativeSourceLit with a per-instance _SceneTint. GLOW pieces carry vertex colour 0 and emission 1, so they draw exactly
    // the tint, unlit (MeshBasicMaterial); LIT pieces carry colour 1 and no emission (MeshLambertMaterial).
    public sealed partial class NativeSourceModels
    {
        private Part[] pieces;
        private void BuildPieces()
        {
            var shader=Resources.Load<Shader>("NativeSourceLit");if(shader==null)throw new System.InvalidOperationException("Source material shader is missing");
            var material=new Material(shader){name="Native weapon pieces",enableInstancing=true};material.SetInt("_Cull",2);material.SetFloat("_Ink",0);materials.Add(material);
            var points=new List<System.Numerics.Vector3>();var normals=new List<System.Numerics.Vector3>();var indices=new List<int>();pieces=new Part[8];
            for(int shape=0;shape<4;shape++)
            {
                NativeWeaponDressing.Primitive((WeaponShape)shape,points,normals,indices);int count=points.Count;
                var v=new Vector3[count];var n=new Vector3[count];var uv=new Vector2[count];var triangles=new int[indices.Count];
                for(int i=0;i<count;i++){v[i]=new Vector3(-points[i].X,points[i].Y,points[i].Z);n[i]=new Vector3(-normals[i].X,normals[i].Y,normals[i].Z);}
                for(int i=0;i+2<indices.Count;i+=3){triangles[i]=indices[i];triangles[i+1]=indices[i+2];triangles[i+2]=indices[i+1];}
                for(int glow=0;glow<2;glow++)
                {
                    var colors=new Color[count];var emission=new List<Vector4>(count);
                    for(int i=0;i<count;i++){colors[i]=glow==1?new Color(0,0,0,1):Color.white;emission.Add(glow==1?new Vector4(1,1,1,0):Vector4.zero);}
                    var mesh=new Mesh{name="Weapon piece "+(WeaponShape)shape+(glow==1?" glow":" lit")};mesh.vertices=v;mesh.normals=n;mesh.colors=colors;mesh.uv=uv;mesh.SetUVs(1,emission);mesh.triangles=triangles;mesh.RecalculateBounds();mesh.UploadMeshData(true);
                    var part=new Part{Name=mesh.name,Bone=-1,Mesh=mesh,Material=material,NoShadow=glow==1};pieces[shape*2+glow]=part;
                    // Registered as models so Dispose releases the meshes with the rest; never queued by id.
                    models.Add("native-weapon-piece-"+(shape*2+glow),new[]{part});
                }
            }
        }
        // One piece for this frame's batches (flushed with the source models). `tint` is linear; glow pieces never cast shadows.
        public void QueuePiece(WeaponShape shape,bool glow,Matrix4x4 matrix,Color tint,int layer=0,Camera camera=null)
        {
            if(pieces==null)BuildPieces();var part=pieces[(int)shape*2+(glow?1:0)];var key=(part,layer,camera);
            if(!draws.TryGetValue(key,out var d)){d=new Draws{Part=part,Layer=layer,Camera=camera};draws.Add(key,d);}
            d.Matrices.Add(matrix);d.Bodies.Add(Vector4.one);d.Accents.Add(Vector4.one);d.Tints.Add(new Vector4(tint.r,tint.g,tint.b,1));d.Glows.Add(Vector4.one);
        }
    }
}
