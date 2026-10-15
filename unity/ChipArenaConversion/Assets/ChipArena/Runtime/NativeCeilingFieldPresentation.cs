using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The ceiling-field hit holograms (client/ceiling-field.ts; rules in Core/NativeCeilingField.cs, look in
    /// Resources/NativeCeilingField.shader). Driven by the CeilingHit events every client holds (host, guests through the
    /// snapshot's event buffer, the killcam replay), aged on the presentation clock. Every live hologram is one quad of one
    /// dynamic world-space mesh refilled from reused lists: one draw, no per-frame allocation.
    /// </summary>
    public sealed class NativeCeilingFieldPresentation : IDisposable
    {
        private readonly NativeCeilingField field=new NativeCeilingField();
        private readonly Mesh mesh=new Mesh{name="Native ceiling field holograms"};private readonly Material material;
        private readonly List<Vector3> vertices=new List<Vector3>(NativeCeilingField.Cap*4);private readonly List<Color> colors=new List<Color>(NativeCeilingField.Cap*4);
        private readonly List<Vector4> uv=new List<Vector4>(NativeCeilingField.Cap*4);private readonly List<int> indices=new List<int>(NativeCeilingField.Cap*6);
        private NativeArena last;private int generation=-1,lastEvent;private float lastTime;
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private NativeCeilingFieldPresentation(Shader shader){material=new Material(shader){name="Native ceiling field holograms",renderQueue=(int)RenderQueue.Transparent};mesh.MarkDynamic();}
        /// <summary>Null when the shader is missing or unsupported (the holograms then simply do not draw).</summary>
        public static NativeCeilingFieldPresentation Create(){var shader=Resources.Load<Shader>("NativeCeilingField");return shader!=null&&shader.isSupported?new NativeCeilingFieldPresentation(shader):null;}
        // `this.`: in C# 14 a bare `field` inside a property accessor is the property's backing-field keyword (CI's .NET 10 compiler).
        public int Count=>this.field.Count;
        public void Clear(){field.Clear();lastEvent=0;mesh.Clear();}
        /// <summary>now: the presentation clock (the source's state.time: FieldClock, continuous on a guest).</summary>
        public void Render(NativeArena arena,Camera camera,float now)
        {
            // Another arena (the killcam replay's), a new round or a rewound clock starts over, as the source's clear().
            if(arena!=last||arena.Generation!=generation||now<lastTime-.25f){Clear();last=arena;generation=arena.Generation;lastTime=now;}
            lastTime=Math.Max(lastTime,now);
            // The source ages and drops holograms (update) before it adds this frame's events (renderer.ts order).
            field.Update(now);
            var events=arena.Events;
            for(int i=0;i<events.Count;i++)
            {
                var e=events[i];if(e.Id<=lastEvent)continue;lastEvent=e.Id;
                if(e.Kind==EventKind.CeilingHit&&NativeCeilingField.FreshEvent(now,e.Time))field.Add(e.Position.x,e.Position.z,now,NativeCeilingField.ColorOf(e.Color));
            }
            vertices.Clear();colors.Clear();uv.Clear();indices.Clear();
            for(int i=0;i<field.Count;i++)
            {
                var h=field[i];float age=now-h.Born;NativeCeilingField.Display(h.Color,1,out double r,out double g,out double b);var c=new Color((float)r,(float)g,(float)b,1);
                int a=vertices.Count;
                for(int k=0;k<4;k++)
                {
                    // Plane coordinates p are source world x and -z from the hit; Unity's x is mirrored.
                    NativeCeilingField.Corner(k,out float px,out float py);
                    vertices.Add(new Vector3(h.X-px,NativeCeilingField.FieldHeight-NativeCeilingField.Drop,h.Z-py));colors.Add(c);uv.Add(new Vector4(px,py,age,0));
                }
                indices.Add(a);indices.Add(a+1);indices.Add(a+2);indices.Add(a);indices.Add(a+2);indices.Add(a+3);
            }
            mesh.Clear();if(vertices.Count==0)return;
            mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0,false);mesh.bounds=Everywhere;
            Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        public void Dispose(){CapturedAnimation.Release(mesh);CapturedAnimation.Release(material);}
    }

    public sealed partial class ArenaPresentation
    {
        private NativeCeilingFieldPresentation ceilingField;private bool ceilingFieldFailed;private Camera worldEye;
        /// <summary>
        /// The camera the world effects are drawn for (RenderGlow's: the match view, the killcam replay's), kept from the last
        /// RenderGlow for the CPU-built camera-facing text drawn earlier in the frame (the SPLAT! pop). The native match camera
        /// is not tagged MainCamera, so Camera.main is only the fallback before the first RenderGlow.
        /// </summary>
        private Camera WorldEye=>worldEye!=null?worldEye:Camera.main;
        /// <summary>
        /// The event-driven world effects that replace the early stand-ins (placeholder batch 5b): the ceiling-field hit
        /// holograms and the summons' minionHit puffs. now: FieldClock, the source's state.time.
        /// </summary>
        private void EventWorldFx(NativeArena arena,Camera camera,float now)
        {
            if(camera!=null)worldEye=camera;
            if(ceilingField==null&&!ceilingFieldFailed){ceilingField=NativeCeilingFieldPresentation.Create();ceilingFieldFailed=ceilingField==null;}
            ceilingField?.Render(arena,camera,now);SummonHitPuffs(arena,now);
        }
        private void DisposeEventWorldFx(){ceilingField?.Dispose();ceilingField=null;ceilingFieldFailed=false;pickupGlow?.Dispose();pickupGlow=null;pickupGlowFailed=false;summonBreakups.Clear();}
    }
}
