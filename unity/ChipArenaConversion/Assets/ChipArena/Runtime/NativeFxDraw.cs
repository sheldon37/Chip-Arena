using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Shared C12 render primitives (Resources/NativeSirenFx.shader): dynamic world-space meshes refilled from reused
    /// buffers each frame, so a frame's flames, rings, sprites, slashes, flame shells and orbs cost one draw per layer and
    /// no per-frame allocation. Solid layers (flame cubes, orbs, claws) write depth in the opaque queue; the effect layer
    /// is premultiplied and sorted after the scene. The DEV fighter effects of C12 part 2 reuse these.
    /// </summary>
    public sealed class NativeFxDraw : IDisposable
    {
        public const float Flat=0,Radial=1,SlashMode=2,ShellMode=3,OrbMode=4;
        /// <summary>Vertex flags: additive blending, and authored sRGB colour (else a raw ShaderMaterial colour).</summary>
        public const float Additive=1,Authored=2;
        public sealed class Layer
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};
            private readonly List<Vector3> points=new List<Vector3>(),normals=new List<Vector3>();private readonly List<Color> colors=new List<Color>();
            private readonly List<Vector4> uv=new List<Vector4>(),p=new List<Vector4>(),q=new List<Vector4>();private readonly List<int> indices=new List<int>();
            public Layer(string name){Mesh.name=name;Mesh.MarkDynamic();}
            public int Count=>points.Count;
            public void Clear(){points.Clear();normals.Clear();colors.Clear();uv.Clear();p.Clear();q.Clear();indices.Clear();}
            public int Vertex(Vector3 position,Vector3 normal,Color color,Vector4 uv0,Vector4 p0,Vector4 q0)
            {points.Add(position);normals.Add(normal);colors.Add(color);uv.Add(uv0);p.Add(p0);q.Add(q0);return points.Count-1;}
            public void Triangle(int a,int b,int c){indices.Add(a);indices.Add(b);indices.Add(c);}
            public void Draw(Material material,Camera camera,int layer)
            {
                Mesh.Clear();if(points.Count==0||camera==null)return;
                Mesh.SetVertices(points);Mesh.SetNormals(normals);Mesh.SetColors(colors);Mesh.SetUVs(0,uv);Mesh.SetUVs(1,p);Mesh.SetUVs(2,q);
                Mesh.SetTriangles(indices,0,false);Mesh.bounds=Everywhere;
                Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,layer,camera,0,null,ShadowCastingMode.Off,false);
            }
        }
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        public readonly Layer Solid=new Layer("Native Siren flames"),Effects=new Layer("Native Siren effects"),ViewSolid=new Layer("Native Siren view model");
        private readonly Material solid,effects;
        // Unit sphere for orbs (positions = normals) and the source's shell and jet geometry.
        private readonly Vector3[] orbPoints;private readonly int[] orbIndices;
        private readonly double[] shellPositions,shellUv,jetPositions,jetNormals,jetUv;private readonly int[] shellIndices,jetIndices;
        public NativeFxDraw()
        {
            var shader=Resources.Load<Shader>("NativeSirenFx");if(shader==null)throw new InvalidOperationException("NativeSirenFx shader is missing");
            solid=new Material(shader){name="Native Siren solid",renderQueue=(int)RenderQueue.Geometry+10};solid.SetFloat("_Src",(float)BlendMode.One);solid.SetFloat("_Dst",(float)BlendMode.Zero);solid.SetFloat("_ZWrite",1);
            effects=new Material(shader){name="Native Siren effects",renderQueue=(int)RenderQueue.Transparent+1};effects.SetOverrideTag("RenderType","Transparent");
            effects.SetFloat("_Src",(float)BlendMode.One);effects.SetFloat("_Dst",(float)BlendMode.OneMinusSrcAlpha);effects.SetFloat("_ZWrite",0);
            NativeSirenFx.Sphere(out var sp,out _,out var si,12,8);orbPoints=new Vector3[sp.Length/3];for(int i=0;i<orbPoints.Length;i++)orbPoints[i]=new Vector3((float)sp[i*3],(float)sp[i*3+1],(float)sp[i*3+2]);orbIndices=si;
            NativeSirenFx.Sphere(out shellPositions,out shellUv,out shellIndices);NativeSirenFx.Jet(out jetPositions,out jetNormals,out jetUv,out jetIndices);
        }
        public void Clear(){Solid.Clear();Effects.Clear();ViewSolid.Clear();}
        public void Draw(Camera world,Camera view,float time)
        {
            solid.SetFloat("_FxTime",time);effects.SetFloat("_FxTime",time);
            Solid.Draw(solid,world,0);Effects.Draw(effects,world,0);ViewSolid.Draw(solid,view,10);
        }
        private static Vector4 Uv(float u,float v,float mode,float flags)=>new Vector4(u,v,mode,flags);
        /// <summary>A flat-coloured cube (8 corners) with full edges <paramref name="scale"/>.</summary>
        public void Cube(Layer layer,Vector3 center,Quaternion rotation,Vector3 scale,Color color,float flags=0)
        {
            var c=NativeBlastSim.CubeCorners;var t=NativeBlastSim.CubeTriangles;Vector4 uv=Uv(0,0,Flat,flags);int start=layer.Count;
            for(int k=0;k<8;k++)layer.Vertex(center+rotation*new Vector3(c[k*3]*scale.x*.5f,c[k*3+1]*scale.y*.5f,c[k*3+2]*scale.z*.5f),Vector3.up,color,uv,Vector4.zero,Vector4.zero);
            for(int k=0;k<t.Length;k+=3)layer.Triangle(start+t[k],start+t[k+1],start+t[k+2]);
        }
        /// <summary>A box with flat face normals for the rim modes (claw blades) or a flat colour (claw edges).</summary>
        public void Box(Layer layer,Matrix4x4 m,Color color,float mode,float flags,Vector4 p,Vector4 q)
        {
            for(int f=0;f<6;f++)
            {
                int axis=f/2;float sign=f%2==0?1:-1;Vector3 n=Vector3.zero;n[axis]=sign;Vector3 u=Vector3.zero,v=Vector3.zero;u[(axis+1)%3]=1;v[(axis+2)%3]=1;
                Vector3 worldNormal=m.MultiplyVector(n).normalized;int start=layer.Count;Vector4 uv=Uv(0,0,mode,flags);
                for(int k=0;k<4;k++){Vector3 local=n*.5f+u*((k&1)==0?-.5f:.5f)+v*((k&2)==0?-.5f:.5f);layer.Vertex(m.MultiplyPoint3x4(local),worldNormal,color,uv,new Vector4(local.x,local.y,local.z,p.w),q);}
                layer.Triangle(start,start+1,start+3);layer.Triangle(start,start+3,start+2);
            }
        }
        /// <summary>A camera-facing sprite of full width <paramref name="size"/> (mode Radial: the source's radialTexture).</summary>
        public void Sprite(Layer layer,Vector3 center,Vector3 right,Vector3 up,float size,Color color,float flags)
        {
            right*=size*.5f;up*=size*.5f;int a=layer.Count;
            layer.Vertex(center-right-up,Vector3.up,color,Uv(-1,-1,Radial,flags),Vector4.zero,Vector4.zero);layer.Vertex(center+right-up,Vector3.up,color,Uv(1,-1,Radial,flags),Vector4.zero,Vector4.zero);
            layer.Vertex(center-right+up,Vector3.up,color,Uv(-1,1,Radial,flags),Vector4.zero,Vector4.zero);layer.Vertex(center+right+up,Vector3.up,color,Uv(1,1,Radial,flags),Vector4.zero,Vector4.zero);
            layer.Triangle(a,a+1,a+3);layer.Triangle(a,a+3,a+2);
        }
        /// <summary>A flat floor ring (RingGeometry inner..1, 64 segments, laid in XZ) of radius <paramref name="radius"/>.</summary>
        public void Ring(Layer layer,Vector3 center,float inner,float radius,Color color,float flags)
        {
            const int segments=64;Vector4 uv=Uv(0,0,Flat,flags);int start=layer.Count;
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments,s=Mathf.Sin(a),c=Mathf.Cos(a);layer.Vertex(center+new Vector3(c*inner*radius,0,s*inner*radius),Vector3.up,color,uv,Vector4.zero,Vector4.zero);layer.Vertex(center+new Vector3(c*radius,0,s*radius),Vector3.up,color,uv,Vector4.zero,Vector4.zero);}
            for(int i=0;i<segments;i++){int a=start+i*2;layer.Triangle(a,a+1,a+3);layer.Triangle(a,a+3,a+2);}
        }
        /// <summary>A flat quad from four corners (crack tops).</summary>
        public void Quad(Layer layer,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Color color,float flags)
        {
            Vector4 uv=Uv(0,0,Flat,flags);int s=layer.Count;layer.Vertex(a,Vector3.up,color,uv,Vector4.zero,Vector4.zero);layer.Vertex(b,Vector3.up,color,uv,Vector4.zero,Vector4.zero);
            layer.Vertex(c,Vector3.up,color,uv,Vector4.zero,Vector4.zero);layer.Vertex(d,Vector3.up,color,uv,Vector4.zero,Vector4.zero);layer.Triangle(s,s+1,s+2);layer.Triangle(s,s+2,s+3);
        }
        private static Vector3 Mirror(double[] m,double x,double y,double z){NativeFxMath.Point(m,0,x,y,z,out double wx,out double wy,out double wz);return new Vector3(-(float)wx,(float)wy,(float)wz);}
        /// <summary>The claw slash crescent: arc vertices through the source-frame matrix m, mirrored into Unity.</summary>
        public void Crescent(Layer layer,double[] m,float sweep,float fade,Color ink,Color edge)
        {
            var pos=NativeSirenFx.ArcPositions;var uvs=NativeSirenFx.ArcUv;int start=layer.Count;var p=new Vector4(sweep,fade,0,0);var q=new Vector4(edge.r,edge.g,edge.b,0);
            for(int i=0;i<pos.Length/3;i++)layer.Vertex(Mirror(m,pos[i*3],pos[i*3+1],pos[i*3+2]),Vector3.up,ink,Uv((float)uvs[i*2],(float)uvs[i*2+1],SlashMode,0),p,q);
            var idx=NativeSirenFx.ArcIndices;for(int i=0;i<idx.Length;i+=3)layer.Triangle(start+idx[i],start+idx[i+1],start+idx[i+2]);
        }
        /// <summary>A flame shell (explosion sphere when jet is false, the eruption column when true) through the source-frame m.</summary>
        public void Shell(Layer layer,double[] m,bool jet,float fade,float speed,Color ink,Color edge)
        {
            var pos=jet?jetPositions:shellPositions;var uvs=jet?jetUv:shellUv;var idx=jet?jetIndices:shellIndices;int start=layer.Count;var p=new Vector4(fade,speed,0,0);var q=new Vector4(edge.r,edge.g,edge.b,0);
            // Normals through the inverse-transpose of the scaled transform (the jet is far from uniformly scaled).
            double sx=Math.Sqrt(m[0]*m[0]+m[4]*m[4]+m[8]*m[8]),sy=Math.Sqrt(m[1]*m[1]+m[5]*m[5]+m[9]*m[9]),sz=Math.Sqrt(m[2]*m[2]+m[6]*m[6]+m[10]*m[10]);
            for(int i=0;i<pos.Length/3;i++)
            {
                double nx=jet?jetNormals[i*3]:pos[i*3],ny=jet?jetNormals[i*3+1]:pos[i*3+1],nz=jet?jetNormals[i*3+2]:pos[i*3+2];nx/=sx*sx;ny/=sy*sy;nz/=sz*sz;
                double wx=m[0]*nx+m[1]*ny+m[2]*nz,wy=m[4]*nx+m[5]*ny+m[6]*nz,wz=m[8]*nx+m[9]*ny+m[10]*nz;var n=new Vector3(-(float)wx,(float)wy,(float)wz).normalized;
                layer.Vertex(Mirror(m,pos[i*3],pos[i*3+1],pos[i*3+2]),n,ink,Uv((float)uvs[i*2],(float)uvs[i*2+1],ShellMode,0),p,q);
            }
            for(int i=0;i<idx.Length;i+=3)layer.Triangle(start+idx[i],start+idx[i+1],start+idx[i+2]);
        }
        /// <summary>An orb (orbMaterial): unit sphere through m (Unity frame), churning in its own space.</summary>
        public void Orb(Layer layer,Matrix4x4 m,Color core,Color rim,float strength,float flags=0)
        {
            int start=layer.Count;var uv=Uv(0,0,OrbMode,flags);var q=new Vector4(rim.r,rim.g,rim.b,1);
            for(int i=0;i<orbPoints.Length;i++){var o=orbPoints[i];layer.Vertex(m.MultiplyPoint3x4(o),m.MultiplyVector(o).normalized,core,uv,new Vector4(-o.x,o.y,o.z,strength),q);}
            for(int i=0;i<orbIndices.Length;i+=3)layer.Triangle(start+orbIndices[i],start+orbIndices[i+1],start+orbIndices[i+2]);
        }
        public void Dispose(){Clear();CapturedAnimation.Release(Solid.Mesh);CapturedAnimation.Release(Effects.Mesh);CapturedAnimation.Release(ViewSolid.Mesh);CapturedAnimation.Release(solid);CapturedAnimation.Release(effects);}
    }
}
