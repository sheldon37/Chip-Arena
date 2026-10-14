using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using U=ChipArena.UnityConversion.NativeUi;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Draws Core NativeExpeditionStateScene (Claude, 2026-10-14; docs/EXPEDITION-PRESENTATION.md "Doors, gate, sanctuary, transfer
    /// and boss arrival"): the solid kit boxes with Resources/NativeBlockLit.shader, instanced in chunks as the dressing kits are
    /// (lit by the arena look, casting shadows), and the hardlight, glow and dust quads as one NativeSirenFx effects batch
    /// (NativeFxDraw). Reused arrays and lists: no allocation per frame once the batches exist.
    /// </summary>
    public sealed class NativeExpeditionStatesDraw : IDisposable
    {
        private static readonly int SizeId=Shader.PropertyToID("_BlockSize"),RestId=Shader.PropertyToID("_BlockRest"),ColorId=Shader.PropertyToID("_BlockColor"),LookId=Shader.PropertyToID("_BlockLook");
        private sealed class Batch
        {
            public readonly Matrix4x4[] M=new Matrix4x4[NativeBlockLooks.Chunk];
            public readonly Vector4[] Size=new Vector4[NativeBlockLooks.Chunk],Rest=new Vector4[NativeBlockLooks.Chunk],Color=new Vector4[NativeBlockLooks.Chunk],Look=new Vector4[NativeBlockLooks.Chunk];
            public int Count;public MaterialPropertyBlock Block;
        }
        private readonly List<Batch> batches=new List<Batch>();private int used;
        private Material material;private Mesh mesh;private NativeFxDraw fx;private MaterialPropertyBlock one;
        private NativeExpeditionStatesDraw(){}
        /// <summary>Null when the block shader is missing or unsupported (the HUD door holograms still stand in).</summary>
        public static NativeExpeditionStatesDraw Create()
        {
            var d=new NativeExpeditionStatesDraw();
            try
            {
                var shader=Resources.Load<Shader>("NativeBlockLit");if(shader==null||!shader.isSupported)return null;
                d.material=new Material(shader){name="Expedition state kit",enableInstancing=true};d.mesh=MeshOf(NativeBlockLooks.Build(0));d.fx=new NativeFxDraw();d.one=new MaterialPropertyBlock();return d;
            }
            catch(Exception e)when(e is InvalidOperationException||e is ArgumentException){Debug.LogWarning("Expedition state pieces disabled: "+e.Message);d.Dispose();return null;}
        }
        public void Draw(NativeExpeditionStateScene scene,Camera camera,bool instancing,float time)
        {
            used=0;bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;var parts=scene.Parts;
            for(int i=0;i<parts.Count;i++)
            {
                var p=parts[i];if(used==0||batches[used-1].Count==NativeBlockLooks.Chunk){if(used==batches.Count)batches.Add(new Batch());batches[used].Count=0;used++;}
                var b=batches[used-1];int k=b.Count++;var c=NativeArena.ColorOf(p.Hex);if(linear)c=c.linear;
                b.M[k]=Matrix4x4.TRS(new Vector3(p.X,p.Y,p.Z),Quaternion.Euler(0,p.Yaw,0),Vector3.one);b.Size[k]=new Vector4(p.Hx,p.Hy,p.Hz,NativeBlockLooks.Chamfer);
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
                // Compatibility rendering: one by one with a reused block.
                for(int k=0;k<b.Count;k++){one.Clear();one.SetVector(SizeId,b.Size[k]);one.SetVector(RestId,b.Rest[k]);one.SetVector(ColorId,b.Color[k]);one.SetVector(LookId,b.Look[k]);Graphics.DrawMesh(mesh,b.M[k],material,0,null,0,one,ShadowCastingMode.On,true);}
            }
            var q=scene.Quads;fx.Clear();var layer=fx.Effects;
            for(int i=0;i<q.Count;i++)
            {
                int o=i*12,k=i*4;float flags=NativeFxDraw.Authored+(q.Additive[i]!=0?NativeFxDraw.Additive:0),mode=q.Modes[i];
                // Linear colours go in as authored sRGB (NativeSirenFx linearises them back), as the Ward Zero effects do.
                var color=new Color(q.Colors[k],q.Colors[k+1],q.Colors[k+2],q.Colors[k+3]).gamma;int a=layer.Count;
                for(int v=0;v<4;v++)layer.Vertex(new Vector3(q.Corners[o+v*3],q.Corners[o+v*3+1],q.Corners[o+v*3+2]),Vector3.up,color,new Vector4(v==1||v==2?1:-1,v>=2?1:-1,mode,flags),Vector4.zero,Vector4.zero);
                layer.Triangle(a,a+1,a+2);layer.Triangle(a,a+2,a+3);
            }
            fx.Draw(camera,null,time);
        }
        // The stage-0 block mesh as the dressing kits build it (preview cube, lattice in TEXCOORD0/1, baked shade in COLOR).
        private static Mesh MeshOf(NativeBlockLooks.StageMesh s)
        {
            int n=s.Vertices;var v=new Vector3[n];var normals=new Vector3[n];var colors=new Color[n];var a=new List<Vector4>(n);var b=new List<Vector4>(n);
            for(int i=0;i<n;i++){v[i]=new Vector3(s.Preview[i*3],s.Preview[i*3+1],s.Preview[i*3+2]);normals[i]=new Vector3(s.Normal[i*3],s.Normal[i*3+1],s.Normal[i*3+2]);colors[i]=new Color(s.Shade[i*4],s.Shade[i*4+1],s.Shade[i*4+2],s.Shade[i*4+3]);a.Add(new Vector4(s.Lattice[i*4],s.Lattice[i*4+1],s.Lattice[i*4+2],0));b.Add(new Vector4(s.Inset[i*4],s.Inset[i*4+1],s.Inset[i*4+2],0));}
            var mesh=new Mesh{name="Expedition state kit block"};mesh.vertices=v;mesh.normals=normals;mesh.colors=colors;mesh.SetUVs(0,a);mesh.SetUVs(1,b);mesh.triangles=s.Triangles;mesh.bounds=new Bounds(Vector3.zero,Vector3.one*2000);mesh.UploadMeshData(true);return mesh;
        }
        public void Dispose(){if(mesh!=null)CapturedAnimation.Release(mesh);if(material!=null)CapturedAnimation.Release(material);fx?.Dispose();mesh=null;material=null;fx=null;batches.Clear();}
    }

    public sealed partial class ConversionGame
    {
        // Doors, the final gate, the sanctuary, the area transfer screen and the boss arrival, from the bound view only.
        private ExpeditionLayout expeditionLayout;private string expeditionLayoutFingerprint;private bool expeditionLayoutRead;private ExpeditionStateRig expeditionRig;
        private readonly ExpeditionPresentationClock expeditionClock=new ExpeditionPresentationClock();
        private readonly ExpeditionStateCues expeditionCues=new ExpeditionStateCues();
        private readonly NativeExpeditionStateScene expeditionStates=new NativeExpeditionStateScene();
        private readonly ExpeditionLabels expeditionStateLabels=new ExpeditionLabels();
        private readonly ExpeditionTransferModel expeditionTransfer=new ExpeditionTransferModel();
        private readonly ExpeditionTransferFade expeditionTransferFade=new ExpeditionTransferFade();
        private readonly ExpeditionSafeModel expeditionSafe=new ExpeditionSafeModel();
        private readonly ExpeditionBossModel expeditionBoss=new ExpeditionBossModel();
        private NativeExpeditionStatesDraw expeditionStatesDraw;private bool expeditionStatesFailed;private double expeditionStateTime;

        /// <summary>
        /// The rig of the view's area from the layout manifest every client holds (parsed once), only when the view names this
        /// exact layout: areaBarrier.layout is the authority's validated fingerprint (manifest bytes plus content revision). Any
        /// other layout, or none yet, binds nothing, and the doors keep their HUD holograms.
        /// </summary>
        private ExpeditionStateRig ExpeditionRigFor(ExpeditionView v)
        {
            if(v==null)return null;
            if(!expeditionLayoutRead&&content!=null)
            {
                expeditionLayoutRead=true;
                try{var asset=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName);if(asset!=null){expeditionLayout=NativeExpeditionLayout.Parse(asset.text);expeditionLayoutFingerprint=NativeExpeditionLayout.Compatibility(asset.text,content);}}
                catch(Exception e)when(e is FormatException||e is ArgumentException||e is InvalidOperationException){Debug.LogWarning("Expedition layout presentation disabled: "+e.Message);expeditionLayout=null;}
            }
            if(expeditionLayout==null||v.areaBarrier?.layout==null||v.areaBarrier.layout!=expeditionLayoutFingerprint)return null;
            if(expeditionRig==null||expeditionRig.Area!=v.area)expeditionRig=ExpeditionStateRig.For(expeditionLayout,v.area);
            return expeditionRig;
        }
        /// <summary>LateUpdate (from ExpeditionWorldFrame): the presentation clock, the cue memory, the transfer screen's fade, and
        /// the doors, gate and sanctuary of the loaded area when the arena holds that area's map.</summary>
        private void ExpeditionStatesFrame()
        {
            var v=LiveExpedition;double t=expeditionStateTime=expeditionClock.Observe(v,Time.unscaledTime);expeditionCues.Observe(v,t);
            var rig=ExpeditionRigFor(v);bool mapReady=rig==null||arena!=null&&arena.MapId==rig.Map;
            expeditionTransfer.From(v,expeditionStateLabels);expeditionTransferFade.Step(expeditionTransfer.Active,mapReady,Time.unscaledDeltaTime);
            if(!ExpeditionWorldShown||rig==null||!mapReady){expeditionStates.Clear();if(!ExpeditionWorldShown)expeditionCues.ForgetPilots();return;}
            // Sanctuary crossings: every connected pilot standing (feet just over the floor).
            foreach(var p in arena.Pilots){if(p?.Body==null||!p.Seat.connected||p.Hp<=0)continue;var at=p.Body.position;expeditionCues.Pilot(p.Seat.id,at.x,at.y-.765f,at.z,rig,t);}
            var c=cameraView.transform;Vector3 e=c.position,r=c.right,u=c.up;
            var eye=new ExpeditionEye{X=e.x,Y=e.y,Z=e.z,Rx=r.x,Ry=r.y,Rz=r.z,Ux=u.x,Uy=u.y,Uz=u.z};
            expeditionStates.Build(v,rig,expeditionCues,t,eye,prefs.reducedEffects,presentation.LowGraphics);
            if(expeditionStatesDraw==null&&!expeditionStatesFailed){expeditionStatesDraw=NativeExpeditionStatesDraw.Create();expeditionStatesFailed=expeditionStatesDraw==null;}
            if(expeditionStatesDraw==null)expeditionStates.Clear();else expeditionStatesDraw.Draw(expeditionStates,cameraView,presentation.UsesInstancing,Time.unscaledTime);
        }
        /// <summary>The in-scene HUD pieces: the SAFE chip, the boss plates, the boss intro and the final gate's notice.</summary>
        private void ExpeditionStatesHud(ExpeditionView v,float width,float height,float countdownBottom)
        {
            if(Event.current.type!=EventType.Repaint)return;double t=expeditionStateTime;bool reduced=prefs.reducedEffects;
            expeditionSafe.From(v,expeditionStateLabels);NativeExpeditionStatesHud.Safe(v,expeditionSafe,24,90);
            expeditionBoss.From(v,expeditionCues,t);NativeExpeditionStatesHud.BossPlates(expeditionBoss,width,countdownBottom,Time.unscaledTime,reduced);
            if(expeditionBoss.Intro>=0)NativeExpeditionStatesHud.BossIntro(expeditionBoss,width,height,(float)expeditionBoss.Age,reduced);
            var rig=ExpeditionRigFor(v);if(rig==null)return;
            foreach(var g in rig.Gates)if(expeditionCues.Cue(g.Id,t,out double age)&&age<NativeExpeditionStates.GateNoticeSeconds){NativeExpeditionStatesHud.GateNotice(width,height,(float)age,reduced);break;}
        }
        /// <summary>OnGUI, over everything (menus included): the area transfer screen while the barrier is up and its fade.</summary>
        private void ExpeditionTransferGui()
        {
            if(Event.current.type!=EventType.Repaint||error.Length>0||expeditionTransferFade.Alpha<=0||LiveExpedition==null)return;
            GUI.matrix=Matrix4x4.Scale(Vector3.one*U.S);NativeExpeditionStatesHud.Transfer(expeditionTransfer,U.W,U.H,expeditionTransferFade.Alpha,Time.unscaledTime,prefs.reducedEffects);
        }
    }
}
