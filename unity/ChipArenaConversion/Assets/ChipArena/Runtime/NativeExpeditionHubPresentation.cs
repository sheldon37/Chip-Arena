using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using C=ChipArena.UnityConversion.NativeCyberTheme;
using H=ChipArena.UnityConversion.NativeHudChrome;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The hub's state for this frame (Claude, 2026-10-15; docs/EXPEDITION-HUB.md). `Authority` is the binding point for the
    /// director's PROPOSED replicated gate state and sanctuary rules: nothing sets it yet, so the gates rest open, no ward
    /// shimmers and no holster tag shows. The DEV preview (NativeExpeditionHubPreview, DEV panel · Camera) stands in for it,
    /// labelled on screen. `Shown` is what the picture drew this frame, so the sound plays the same gates.
    /// </summary>
    public static class NativeExpeditionHubLive
    {
        /// <summary>Set by the Expedition binding from the authority's view (gate id, open, changedAt; holster; ward) with the
        /// authority clock the closures use. Null: no hub state.</summary>
        public static ExpeditionHubView Authority;public static double AuthorityClock;
        public static ExpeditionHubView Shown;public static double ShownClock;public static ExpeditionHubLayout Layout;public static ExpeditionVolume[] Sanctuary=Array.Empty<ExpeditionVolume>();
        public static bool Inside(Vector3 feet){foreach(var v in Sanctuary)if(v!=null&&v.Contains(new V3(feet.x,feet.y+.05f,feet.z)))return true;return false;}
    }

    /// <summary>Draws Core NativeExpeditionHubGates: the gates' boxes with NativeBlockLit, instanced in chunks with their full
    /// turns (the leaves swing about their hinges), casting shadows; the dust, sparks and ward as one NativeSirenFx effects batch.</summary>
    public sealed class NativeExpeditionHubDraw : IDisposable
    {
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        private sealed class Batch
        {
            public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];
            public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];
            public int Count;public MaterialPropertyBlock Block;
        }
        private readonly List<Batch> batches=new List<Batch>();private int used;private Material material;private Mesh mesh;private NativeFxDraw fx;private MaterialPropertyBlock one;
        private NativeExpeditionHubDraw(){}
        public static NativeExpeditionHubDraw Create()
        {
            var d=new NativeExpeditionHubDraw();
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null||!shader.isSupported)return null;
                d.material=new Material(shader){name="Expedition hub gates",enableInstancing=true};d.mesh=MeshOf(NativeBlockLooks.Build(0));d.fx=new NativeFxDraw();d.one=new MaterialPropertyBlock();return d;
            }
            catch(Exception e)when(e is InvalidOperationException||e is ArgumentException){Debug.LogWarning("Expedition hub gates disabled: "+e.Message);d.Dispose();return null;}
        }
        public void Draw(NativeExpeditionHubGates scene,Camera camera,bool instancing,float time)
        {
            used=0;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var parts=scene.Parts;
            for(int i=0;i<parts.Count;i++)
            {
                var p=parts[i];if(used==0||batches[used-1].Count==NativeBlockLooks.Chunk){if(used==batches.Count)batches.Add(new Batch());batches[used].Count=0;used++;}
                var b=batches[used-1];int k=b.Count++;var c=NativeArena.ColorOf(p.Hex);if(linear)c=c.linear;
                b.M[k]=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw),Vector3.one);b.Size[k]=new Vector4(p.Hx,p.Hy,p.Hz,NativeBlockLooks.Chamfer);
                b.Rest[k]=new Vector4(p.RestX,p.RestY,p.RestZ,0);b.Color[k]=new Vector4(c.r*p.Gain,c.g*p.Gain,c.b*p.Gain,p.Level);b.Look[k]=new Vector4(p.Look,1,p.Seed,p.Flags);
            }
            for(int i=0;i<used;i++)
            {
                var b=batches[i];if(b.Count==0)continue;
                if(instancing)
                {
                    b.Block??=new MaterialPropertyBlock();b.Block.Clear();b.Block.SetVectorArray(SizeId,b.Size);b.Block.SetVectorArray(RestId,b.Rest);b.Block.SetVectorArray(ColorId,b.Color);b.Block.SetVectorArray(LookId,b.Look);
                    Graphics.DrawMeshInstanced(mesh,0,material,b.M,b.Count,b.Block,ShadowCastingMode.On,true,0,null);continue;
                }
                for(int k=0;k<b.Count;k++){one.Clear();one.SetVector(SizeId,b.Size[k]);one.SetVector(RestId,b.Rest[k]);one.SetVector(ColorId,b.Color[k]);one.SetVector(LookId,b.Look[k]);Graphics.DrawMesh(mesh,b.M[k],material,0,null,0,one,ShadowCastingMode.On,true);}
            }
            var q=scene.Quads;fx.Clear();var layer=fx.Effects;
            for(int i=0;i<q.Count;i++)
            {
                int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            fx.Draw(camera,null,time);
        }
        private static Mesh MeshOf(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new List<Vector4>(n);var b=new List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Expedition hub gate block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2000);mesh.UploadMeshData(true);return mesh;
        }
        public void Dispose(){if(mesh!=null)CapturedAnimation.Release(mesh);if(material!=null)CapturedAnimation.Release(material);fx?.Dispose();mesh=null;material=null;fx=null;batches.Clear();}
    }

    public sealed partial class ArenaPresentation
    {
        private ExpeditionHubLayout townHub;private readonly NativeExpeditionHubGates hubGates=new NativeExpeditionHubGates();private readonly ExpeditionHubView hubPreview=new ExpeditionHubView();
        private readonly List<float[]> hubWard=new List<float[]>();private NativeExpeditionHubDraw hubDraw;private bool hubDrawFailed;private readonly ExpeditionHubClock hubClock=new ExpeditionHubClock();
        /// <summary>The hub gates drawn last frame (budget lines).</summary>
        public int HubGatesDrawn=>hubGates.GatesDrawn;
        // The ward's segments: the outer face of every fence section, a line on the street side of the fence (once per map).
        private void HubWardSegments(ContentMap map)=>NativeExpeditionHubGates.WardSegments(map?.blocks,hubWard);
        /// <summary>From ExpeditionCityScenery: the gates at their state's pose (or resting open), their dust, and the ward when
        /// its rule is on; the DEV preview's synthetic state when that tool is on.</summary>
        private void ExpeditionHubFrame(Camera eye,float now)
        {
            if(townHub==null||eye==null)return;ExpeditionHubView v;double t;
            if(NativeExpeditionHubPreview.On){t=Time.unscaledTime;NativeExpeditionHubPreview.Fill(hubPreview,townHub,t);v=hubPreview;}
            // The authority's clock carried forward between view updates (ExpeditionHubClock), so the leaves move every frame and
            // keep step with the collider instead of stepping at the 20 Hz projection.
            else{v=NativeExpeditionHubLive.Authority;t=hubClock.Read(NativeExpeditionHubLive.AuthorityClock,Time.unscaledTimeAsDouble);}
            NativeExpeditionHubLive.Shown=v;NativeExpeditionHubLive.ShownClock=t;NativeExpeditionHubLive.Layout=townHub;
            var c=eye.transform;Vector3 e=c.position,r=c.right,u=c.up;var view=new ExpeditionEye{X=e.x,Y=e.y,Z=e.z,Rx=r.x,Ry=r.y,Rz=r.z,Ux=u.x,Uy=u.y,Uz=u.z};
            hubGates.Build(townHub,v,hubWard,t,view,LowGraphics);
            if(hubDraw==null&&!hubDrawFailed){hubDraw=NativeExpeditionHubDraw.Create();hubDrawFailed=hubDraw==null;}
            hubDraw?.Draw(hubGates,eye,UsesInstancing,now);
        }
        private void DisposeExpeditionHub(){hubDraw?.Dispose();hubDraw=null;hubGates.Forget();hubClock.Reset();NativeExpeditionHubLive.Shown=null;}
    }

    public sealed partial class ConversionGame
    {
        /// <summary>SANCTUARY · WEAPONS HOLSTERED under the SAFE chip, only while the authority's view says the holster rule is
        /// live and I am safe (the proposed rule; nothing shows until it exists).</summary>
        private void ExpeditionHolsterHud(bool safe)
        {
            if(safe&&NativeExpeditionHubLive.Authority?.Holster==true&&!NativeExpeditionHubPreview.On)NativeExpeditionStatesHud.Holster(24,124,false);
        }
        /// <summary>The DEV preview's on-screen label and its holster tag (my pilot's feet in the sanctuary volume).</summary>
        private void ExpeditionHubPreviewHud(float width)
        {
            if(!NativeExpeditionHubPreview.On||Event.current.type!=EventType.Repaint||arena==null||!NativeExpeditionCityKit.Active(arena.MapId))return;
            const string label="DEV PREVIEW · HUB GATES, WARD AND HOLSTER TAG · NOT GAMEPLAY";var size=U.Measure(label,11,true,true,.12f);
            var r=new Rect(width/2-size.x/2-14,64,size.x+28,24);U.Fill(r,C.Void(.75f));H.Outline(r,C.Magenta(.9f),0,H.Corners.None);U.Text(r,label,11,C.Magenta(),TextAnchor.MiddleCenter,bold:true,mono:true,tracking:.12f);
            var me=arena.ViewPilot;if(me?.Body!=null&&NativeExpeditionHubLive.Inside(me.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight))NativeExpeditionStatesHud.Holster(24,124,true);
        }
    }
}
