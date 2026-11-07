using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws client/standard-melee-effects.ts in the arena: the weight-scaled spark flash of every standard melee hit
    /// (Core NativeMeleeFlash, fed from Hit events whose attacker is a greatsword, shield, sword or daggers pilot) and the
    /// dust and stones of a slam landing (Core NativeSlamDust, from the replicated MeleeImpacts). Owner cut (2026-10-11 effects
    /// overhaul: "there still seems to be cube placeholders for certain impact effects"; review 2026-10-11): the source's thin
    /// spark boxes and hard-edged dust squares are drawn through the shared effects renderer (NativeVfxDraw) instead, each
    /// spark as an HDR streak along its box's long axis (same place, length and fade), each dust point as a lit, eroding smoke
    /// puff (same place, colour and fade, growing as it spreads). The stones stay one lit NativeSourceLit mesh
    /// (MeshLambertMaterial), refilled from reused buffers each frame. Host, guests and the killcam run it on the snapshot
    /// clock (the source's s.time); counts follow the effects quality (WeaponEffectsQuality: Full 1, Reduced .3). Cosmetic
    /// only: nothing here touches the match.
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
        private readonly Layer stones;private readonly NativeVfxDraw vfx;private readonly bool ownsVfx;
        /// <summary>The look through the shared renderer: spark HDR gain, width (x the source's box width) and tail taper; dust
        /// albedo gain, opacity (x the source's), size (m at birth, growing by DustGrow x age) and the atlas seed offset.</summary>
        public const float SparkGain=2.6f,SparkWidth=1.3f,SparkTaper=.85f,DustAlbedo=1.25f,DustOpacity=1.6f,DustSize=.55f,DustGrow=.8f;
        // IcosahedronGeometry(1, 0): 20 faces, flat normals (three computes them per face on the non-indexed polyhedron).
        private readonly NativeShotMesh ico=NativeShotGeometry.Icosahedron(1,0);private readonly Vector3[] icoNormals;
        // Vertex colours reach the target unconverted: linear in a linear-space project, sRGB in a gamma-space one.
        private readonly bool gamma;private readonly Color sparkColor,dustColor,stoneColor;
        private NativeArena last;private int generation=-1,lastEvent;private float lastTime=-1;
        public int LiveFlashes=>flashes.Count;
        /// <summary>vfx: the shared effects renderer the owner (ArenaPresentation) begins and submits each frame; null makes
        /// this presentation own one and submit it at the end of its own Render.</summary>
        public NativeMeleeFlashPresentation(NativeVfxDraw vfx=null)
        {
            ownsVfx=vfx==null;this.vfx=vfx??new NativeVfxDraw();
            var lit=Resources.Load<Shader>("NativeSourceLit");if(lit==null)throw new InvalidOperationException("NativeSourceLit shader is missing");
            gamma=QualitySettings.activeColorSpace==ColorSpace.Gamma;
            var lambert=new Material(lit){name="Native slam stones"};lambert.SetInt("_Cull",2);lambert.SetFloat("_Ink",0);
            stones=new Layer("Native slam stones",lambert);
            var p=ico.Positions;icoNormals=new Vector3[p.Length/9];
            for(int f=0;f<icoNormals.Length;f++){int k=f*9;icoNormals[f]=new Vector3(p[k]+p[k+3]+p[k+6],p[k+1]+p[k+4]+p[k+7],p[k+2]+p[k+5]+p[k+8]).normalized;}
            // The shared renderer takes linear colours in either colour space; the stones' vertex colours reach the target as written.
            sparkColor=NativeVfxDraw.Linear(NativeMeleeFlash.Color);dustColor=NativeVfxDraw.Linear(NativeSlamDust.DustColor);stoneColor=Shade(NativeSlamDust.StoneColor);
        }
        private Color Shade(int hex){var c=NativeArena.ColorOf(hex);return gamma?c:c.linear;}
        public void Clear(){flashes.Clear();lastEvent=0;lastTime=-1;stones.Clear();stones.Mesh.Clear();if(ownsVfx)vfx.Clear();}
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
            stones.Clear();if(ownsVfx)vfx.Begin();
            if(camera!=null)
            {
                for(int f=0;f<flashes.Count;f++)Flash(flashes[f],now);
                Slams(arena,now,quality);
            }
            Draw(stones,camera);if(ownsVfx)vfx.Submit(camera);
        }
        // Each spark: an HDR streak along its box's long axis (local z), centred where the box was, its length and fade.
        private void Flash(in NativeMeleeFlash.Flash f,float now)
        {
            double age=now-f.At;if(age<0)return;var origin=NativeArena.Vec(f.Position);float u=(float)Math.Min(1,age/NativeMeleeFlash.Life);
            for(int i=0;i<f.Sparks;i++)
            {
                NativeMeleeFlash.Spark(f.At,f.Weight,i,age,out double x,out double y,out double z,out double qx,out double qy,out double qz,out double qw,out double length,out double alpha);
                NativeImpactMath.Rotate(qx,qy,qz,qw,0,0,1,out double dx,out double dy,out double dz);var dir=new Vector3((float)dx,(float)dy,(float)dz);float l=(float)length;
                var color=sparkColor*(SparkGain*(float)alpha);color.a=0;
                vfx.Streak(origin+new Vector3((float)x,(float)y,(float)z)+dir*(l*.5f),dir*l,color,(float)NativeMeleeFlash.SparkWidth*SparkWidth,l,1,SparkTaper,u,i);
            }
        }
        // standard-melee-effects.ts update(): each landing's dust points and stones at its age.
        // Each landing's dust points as lit smoke puffs (the source's colour and opacity fade, growing as they spread, eroding
        // over the dust's life) and its Lambert stones.
        private void Slams(NativeArena arena,float now,float quality)
        {
            if(arena.MeleeImpacts.Count==0)return;
            int dust=NativeSlamDust.Dust(quality),count=NativeSlamDust.Stones(quality);var p=ico.Positions;var tris=ico.Indices;var albedo=dustColor*DustAlbedo;
            foreach(var impact in arena.MeleeImpacts)
            {
                double age=now-impact.At;if(age<0||age>NativeSlamDust.Life)continue;
                Vector3 root=impact.Position+Vector3.up*(float)NativeSlamDust.Lift;
                float opacity=(float)NativeSlamDust.DustOpacity(age)*DustOpacity,u=(float)(age/NativeSlamDust.Life),size=DustSize*(1+DustGrow*(float)age);
                if(opacity>0)for(int i=0;i<dust;i++)
                {
                    NativeSlamDust.DustPoint(i,age,out double x,out double y,out double z);
                    vfx.Smoke(root+new Vector3((float)x,(float)y,(float)z),albedo,opacity,size,u,i,impact.Position.y);
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
        public void Dispose(){Clear();CapturedAnimation.Release(stones.Mesh);CapturedAnimation.Release(stones.Material);if(ownsVfx)vfx.Dispose();}
    }
}
