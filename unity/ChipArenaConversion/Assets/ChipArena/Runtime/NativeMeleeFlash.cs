using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws client/standard-melee-effects.ts in the arena: the weight-scaled spark flash of every standard melee hit
    /// (Core NativeMeleeFlash, fed from Hit events whose attacker is a greatsword, shield, sword or daggers pilot) and the
    /// dust and stones of a slam landing (Core NativeSlamDust, from the replicated MeleeImpacts). The sparks and the dust's
    /// camera-facing points share one alpha-blended unlit NativeBlast mesh (the source's transparent MeshBasic/Points
    /// materials, no depth write); the stones one lit NativeSourceLit mesh (MeshLambertMaterial). Both meshes are refilled from
    /// reused buffers each frame. Host, guests and the killcam run it on the snapshot clock (the source's s.time); counts
    /// follow the effects quality (WeaponEffectsQuality: Full 1, Reduced .3). Cosmetic only: nothing here touches the match.
    /// </summary>
    public sealed class NativeMeleeFlashPresentation : IDisposable
    {
        private sealed class Layer
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};public readonly Material Material;
            public readonly List<Vector3> Vertices=new List<Vector3>(),Normals=new List<Vector3>();public readonly List<Color> Colors=new List<Color>();public readonly List<Vector4> Emission=new List<Vector4>();public readonly List<int> Indices=new List<int>();
            public Layer(string name,Material material){Mesh.name=name;Mesh.MarkDynamic();Material=material;}
            public void Clear(){Vertices.Clear();Normals.Clear();Colors.Clear();Emission.Clear();Indices.Clear();}
        }
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private readonly NativeMeleeFlash flashes=new NativeMeleeFlash();
        private readonly Layer glow,stones;
        // IcosahedronGeometry(1, 0): 20 faces, flat normals (three computes them per face on the non-indexed polyhedron).
        private readonly NativeShotMesh ico=NativeShotGeometry.Icosahedron(1,0);private readonly Vector3[] icoNormals;
        // Vertex colours reach the target unconverted: linear in a linear-space project, sRGB in a gamma-space one.
        private readonly bool gamma;private readonly Color sparkColor,dustColor,stoneColor;
        private NativeArena last;private int generation=-1,lastEvent;private float lastTime=-1;
        public int LiveFlashes=>flashes.Count;
        public NativeMeleeFlashPresentation()
        {
            var unlit=Resources.Load<Shader>("NativeBlast");if(unlit==null)throw new InvalidOperationException("NativeBlast shader is missing");
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)throw new InvalidOperationException("NativeSourceLit shader is missing");
            gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;
            var fade=new Material(unlit){name="Native melee flash",renderQueue=(int)RenderQueue.Transparent};fade.SetOverrideTag("RenderType","Transparent");
            fade.SetFloat("_Src",(float)BlendMode.SrcAlpha);fade.SetFloat("_Dst",(float)BlendMode.OneMinusSrcAlpha);fade.SetFloat("_ZWrite",0);fade.SetFloat("_Additive",0);
            var lambert=new Material(lit){name="Native slam stones"};lambert.SetInt("_Cull",2);lambert.SetFloat("_Ink",0);
            glow=new Layer("Native melee flash",fade);stones=new Layer("Native slam stones",lambert);
            var p=ico.Positions;icoNormals=new Vector3[p.Length/9];
            for(int f=0;f<icoNormals.Length;f++){int k=f*9;icoNormals[f]=new Vector3(p[k]+p[k+3]+p[k+6],p[k+1]+p[k+4]+p[k+7],p[k+2]+p[k+5]+p[k+8]).normalized;}
            sparkColor=Shade(NativeMeleeFlash.Color);dustColor=Shade(NativeSlamDust.DustColor);stoneColor=Shade(NativeSlamDust.StoneColor);
        }
        private Color Shade(int hex){var c=NativeArena.ColorOf(hex);return gamma?c:c.linear;}
        public void Clear(){flashes.Clear();lastEvent=0;lastTime=-1;glow.Clear();stones.Clear();glow.Mesh.Clear();stones.Mesh.Clear();}
        /// <summary>now: the snapshot clock (ArenaPresentation.FieldClock); quality: the effects quality.</summary>
        public void Render(NativeArena arena,Camera camera,float now,float quality)
        {
            if(arena!=last||generation!=arena.Generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;}
            lastTime=Math.Max(lastTime,now);
            // standard-melee-effects.ts event(): a `hit` (a pilot hit above .05; not burn or poison) from a HIT_WEIGHT spec.
            foreach(var e in arena.Events)
            {
                if(e.Id<=lastEvent)continue;lastEvent=e.Id;
                if(e.Kind!=EventKind.Hit&&e.Kind!=EventKind.Headshot||!NativeEventFeedback.SourceHit(e.Kind,e.Amount)||now-e.Time>NativeMeleeFlash.Life)continue;
                NativePilot source=null;bool pilot=false;foreach(var p in arena.Pilots){if(p.Seat.id==e.Source)source=p;if(p.Seat.id==e.Target)pilot=true;}
                if(source==null||!pilot||!NativeMeleeFlash.Flashes(source.Seat.spec,e.Detail))continue;
                flashes.Add(e.Time,NativeMeleeFlash.Weight(source.Seat.spec),e.Position,quality);
            }
            flashes.Prune(now);
            glow.Clear();stones.Clear();
            if(camera!=null)
            {
                for(int f=0;f<flashes.Count;f++)Flash(flashes[f],now);
                Slams(arena,camera,now,quality);
            }
            Draw(glow,camera);Draw(stones,camera);
        }
        private void Flash(in NativeMeleeFlash.Flash f,float now)
        {
            double age=now-f.At;if(age<0)return;var origin=NativeArena.Vec(f.Position);
            for(int i=0;i<f.Sparks;i++)
            {
                NativeMeleeFlash.Spark(f.At,f.Weight,i,age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double length,out double alpha);
                var color=sparkColor;color.a=(float)alpha;
                Box(glow,origin+new Vector3((float)x,(float)y,(float)z),qx,qy,qz,qw,NativeMeleeFlash.SparkWidth*.5,NativeMeleeFlash.SparkWidth*.5,length*.5,color);
            }
        }
        private static void Box(Layer layer,Vector3 center,double qx,double qy,double qz,double qw,double hx,double hy,double hz,Color color)
        {
            var corners=NativeBlastSim.CubeCorners;var pattern=NativeBlastSim.CubeTriangles;int start=layer.Vertices.Count;
            for(int c=0;c<8;c++)
            {
                NativeImpactMath.Rotate(qx,qy,qz,qw,corners[c*3]*hx,corners[c*3+1]*hy,corners[c*3+2]*hz,out double rx,out double ry,out double rz);
                layer.Vertices.Add(center+new Vector3((float)rx,(float)ry,(float)rz));layer.Colors.Add(color);
            }
            for(int t=0;t<pattern.Length;t++)layer.Indices.Add(start+pattern[t]);
        }
        // standard-melee-effects.ts update(): each landing's dust points and stones at its age.
        private void Slams(NativeArena arena,Camera camera,float now,float quality)
        {
            if(arena.MeleeImpacts.Count==0)return;
            float half=(float)(NativeSlamDust.PointWorldSize(NativeSlamDust.DustSize,camera.fieldOfView)*.5);
            Vector3 right=camera.transform.right*half,up=camera.transform.up*half;
            int dust=NativeSlamDust.Dust(quality),count=NativeSlamDust.Stones(quality);var p=ico.Positions;var tris=ico.Indices;
            foreach(var impact in arena.MeleeImpacts)
            {
                double age=now-impact.At;if(age<0||age>NativeSlamDust.Life)continue;
                Vector3 root=impact.Position+Vector3.up*(float)NativeSlamDust.Lift;
                var color=dustColor;color.a=(float)NativeSlamDust.DustOpacity(age);
                if(color.a>0)for(int i=0;i<dust;i++)
                {
                    NativeSlamDust.DustPoint(i,age,out double x,out double y,out double z);var c=root+new Vector3((float)x,(float)y,(float)z);int start=glow.Vertices.Count;
                    // Screen-facing square, wound clockwise on screen (Unity front faces): BL, TL, BR / BR, TL, TR.
                    glow.Vertices.Add(c-right-up);glow.Vertices.Add(c-right+up);glow.Vertices.Add(c+right-up);glow.Vertices.Add(c+right+up);
                    for(int k=0;k<4;k++)glow.Colors.Add(color);
                    glow.Indices.Add(start);glow.Indices.Add(start+1);glow.Indices.Add(start+2);glow.Indices.Add(start+2);glow.Indices.Add(start+1);glow.Indices.Add(start+3);
                }
                for(int i=0;i<count;i++)
                {
                    NativeSlamDust.Stone(i,age,out double x,out double y,out double z,out double scale,out double qx,out double qy,out double qz,out double qw);if(scale<=0)continue;
                    var center=root+new Vector3((float)x,(float)y,(float)z);int start=stones.Vertices.Count;
                    for(int v=0;v<p.Length;v+=3)
                    {
                        NativeImpactMath.Rotate(qx,qy,qz,qw,p[v]*scale,p[v+1]*scale,p[v+2]*scale,out double rx,out double ry,out double rz);
                        var n=icoNormals[v/9];NativeImpactMath.Rotate(qx,qy,qz,qw,n.x,n.y,n.z,out double nx,out double ny,out double nz);
                        stones.Vertices.Add(center+new Vector3((float)rx,(float)ry,(float)rz));stones.Normals.Add(new Vector3((float)nx,(float)ny,(float)nz));stones.Colors.Add(stoneColor);stones.Emission.Add(Vector4.zero);
                    }
                    for(int t=0;t<tris.Length;t++)stones.Indices.Add(start+tris[t]);
                }
            }
        }
        private static void Draw(Layer layer,Camera camera)
        {
            var mesh=layer.Mesh;mesh.Clear();if(layer.Vertices.Count==0||camera==null)return;
            mesh.SetVertices(layer.Vertices);mesh.SetColors(layer.Colors);if(layer.Normals.Count==layer.Vertices.Count)mesh.SetNormals(layer.Normals);if(layer.Emission.Count==layer.Vertices.Count)mesh.SetUVs(1,layer.Emission);
            mesh.SetTriangles(layer.Indices,0,false);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,layer.Material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){Clear();foreach(var layer in new[]{glow,stones}){CapturedAnimation.Release(layer.Mesh);CapturedAnimation.Release(layer.Material);}}
    }
}
