using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws a frame's volumetric beams (Core NativeBeamList; Resources/NativeVolumetricBeam.shader; docs/LIGHTING-ADVANCED.md):
    /// one instanced draw of the unit cone, each beam's matrix from its source along its axis with its cross radii, its light,
    /// dust and the camera's outside share per instance. The depth fade runs where the camera draws its depth texture (the
    /// effects renderer, NativeVfxDraw, asks for it on every tier above the low one; beams never draw on the low tier), as the
    /// effects' soft particles do; without it the beams cut the scene as billboards do.
    /// Fixed arrays: nothing allocates once made. A missing shader leaves the beams off (the FX builders then keep their
    /// billboards: the runtime hands them no list).
    /// </summary>
    public sealed class NativeVolumetricBeams : IDisposable
    {
        private readonly Mesh mesh;private readonly Material material;private readonly MaterialPropertyBlock block=new MaterialPropertyBlock();
        private readonly Matrix4x4[] matrices=new Matrix4x4[NativeBeamList.Capacity];private readonly Vector4[] colors=new Vector4[NativeBeamList.Capacity],shapes=new Vector4[NativeBeamList.Capacity];
        private static readonly int ColorId=Shader.PropertyToID("_BeamColor"),ShapeId=Shader.PropertyToID("_BeamShape"),ParamsId=Shader.PropertyToID("_BeamParams");
        public bool Ready=>material!=null;
        public NativeVolumetricBeams()
        {
            var shader=Resources.Load<Shader>("NativeVolumetricBeam");if(shader==null||!shader.isSupported||!SystemInfo.supportsInstancing)return;
            material=new Material(shader){name="Volumetric beams",enableInstancing=true};
            NativeBeamList.UnitMesh(24,10,out var p,out var n,out var t);var v=new Vector3[p.Length/3];var nn=new Vector3[v.Length];for(int i=0;i<v.Length;i++){v[i]=new Vector3(p[i*3],p[i*3+1],p[i*3+2]);nn[i]=new Vector3(n[i*3],n[i*3+1],n[i*3+2]);}
            mesh=new Mesh{name="Volumetric beam"};mesh.vertices=v;mesh.normals=nn;mesh.triangles=t;mesh.bounds=new Bounds(new Vector3(0,.5f,0),new Vector3(64,2,64));mesh.UploadMeshData(true);
        }
        /// <summary>Draws `beams` for `camera` (soft edge power `soft`, dust octaves `samples`, the clock `time`), then clears the list.</summary>
        public void Draw(NativeBeamList beams,Camera camera,float soft,int samples,float time,int layer)
        {
            if(beams==null)return;int n=beams.Count;beams.Clear();if(!Ready||camera==null||n==0)return;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var eye=camera.transform.position;int k=0;
            for(int i=0;i<n;i++)
            {
                float outside=beams.Outside(i,eye.x,eye.y,eye.z);if(outside<=.001f)continue;
                var a=new Vector3(beams.Ax[i],beams.Ay[i],beams.Az[i]);var axis=new Vector3(beams.Bx[i],beams.By[i],beams.Bz[i])-a;float len=axis.magnitude;if(len<.05f)continue;
                var up=axis/len;var wide=new Vector3(beams.Wx[i],beams.Wy[i],beams.Wz[i]);// The cross axes and the axis as the unit mesh's x, z and y: other = wide x axis keeps the frame a rotation (a mirrored
                // frame would turn the cone's outside faces in, and the back-face cull would drop them).
                var other=Vector3.Cross(wide,up);
                var m=new Matrix4x4();m.SetColumn(0,wide*beams.R0[i]);m.SetColumn(1,axis);m.SetColumn(2,other*(beams.R0[i]*beams.Aspect[i]));m.SetColumn(3,new Vector4(a.x,a.y,a.z,1));matrices[k]=m;
                var c=new Color(beams.R[i],beams.G[i],beams.B[i]);if(!linear)c=c.gamma;
                colors[k]=new Vector4(c.r,c.g,c.b,beams.Dust[i]);shapes[k]=new Vector4(beams.R1[i]/beams.R0[i],outside,beams.Seed[i],0);k++;
            }
            if(k==0)return;
            block.Clear();block.SetVectorArray(ColorId,colors);block.SetVectorArray(ShapeId,shapes);
            material.SetVector(ParamsId,new Vector4(soft,samples,time,(camera.depthTextureMode&DepthTextureMode.Depth)!=0?1:0));
            Graphics.DrawMeshInstanced(mesh,0,material,matrices,k,block,ShadowCastingMode.Off,false,layer,camera);
        }
        public void Dispose(){if(mesh!=null)CapturedAnimation.Release(mesh);if(material!=null)CapturedAnimation.Release(material);}
    }
}
