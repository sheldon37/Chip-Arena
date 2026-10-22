using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NVector4=System.Numerics.Vector4;
namespace ChipArena.UnityConversion
{
    public sealed partial class ArenaPresentation
    {
        // The chip draft's animated cyber backdrop (Core NativeDraftBackdrop: the state, its easing and the uniforms;
        // Resources/NativeDraftBackdrop.shader: the corridor it ray-casts). Presentation only. One quad and one material, built on
        // the first draft frame and kept: the material goes into `materials` (released with the presentation), the quad is the
        // app's (HideAndDontSave). Each draft frame steps the Core state on the stage's frame step and writes its uniforms to the
        // material (no allocation), then draws the quad for the draft camera alone, first in the frame (Background queue, no
        // depth write), so the pilot, the held chip and the plant pulse draw over it. Nothing here touches the fog, the lights,
        // the ambient or the shadows: NativeLighting.Draft keeps owning (and EndIntro restoring) the stage's lighting state.
        // Without the shader (or on a device that cannot run it) the draft keeps its plain dark clear colour. The anime style
        // (Core NativeMenuStyle.DraftToon, docs/MAP-STYLE-ANIME.md section 6) is a switch set once on the material: on unless
        // the `--ca-map-style legacy` command line asks for the legacy look.
        /// <summary>Set by ConversionGame.DraftFrame before RenderDraft: the heading's bottom row (a fraction of the screen
        /// height; NativeDraftScreen.DraftHeader), whether this is the PvE / Siren personal pick and whether it is the rainbow pick.</summary>
        public float DraftHeadLine=-1;public bool DraftWard,DraftPrism;
        private static Mesh draftBackQuad;private Material draftBackMaterial;private bool draftBackFailed,draftBackLow;private int draftBackFrame=-10;
        private readonly DraftBackdropState draftBack=new DraftBackdropState();
        private static readonly int BackCamId=Shader.PropertyToID("_BackCam"),BackClockId=Shader.PropertyToID("_BackClock"),BackFwdId=Shader.PropertyToID("_BackFwd"),BackUpId=Shader.PropertyToID("_BackUp"),BackRightId=Shader.PropertyToID("_BackRight");
        private static readonly int BackAccentId=Shader.PropertyToID("_BackAccent"),BackFrameId=Shader.PropertyToID("_BackFrame"),BackCounterId=Shader.PropertyToID("_BackCounter"),BackDeepId=Shader.PropertyToID("_BackDeep"),BackVoidId=Shader.PropertyToID("_BackVoid");
        private static readonly int BackPulseId=Shader.PropertyToID("_BackPulse"),BackStyleId=Shader.PropertyToID("_BackStyle"),BackFxId=Shader.PropertyToID("_BackFx"),BackSafeId=Shader.PropertyToID("_BackSafe"),BackCapId=Shader.PropertyToID("_BackCap"),BackGlowId=Shader.PropertyToID("_BackGlow");
        private static readonly int BackToonId=Shader.PropertyToID("_BackToon");
        private const string BackLowKeyword="DRAFT_BACKDROP_LOW";
        // The backdrop for this draft frame: `tint` the stage tint (sRGB), `age` the smoothed pick age, `pulse` the plant pulse
        // (-1 outside it and with Reduced effects), `trayLine` the tray's top row (negative before the layout), `dt` the stage's step.
        private void DraftBackdrop(Camera camera,Color tint,float age,float pulse,float trayLine,bool reduced,bool siren,float dt)
        {
            if(draftBackFailed||camera==null)return;
            if(draftBackMaterial==null)
            {
                var shader=Resources.Load<Shader>("NativeDraftBackdrop");
                if(shader==null||!shader.isSupported){draftBackFailed=true;Debug.LogWarning("The chip draft's backdrop needs shader NativeDraftBackdrop; the draft keeps its plain backdrop.");return;}
                draftBackMaterial=new Material(shader){name="Native draft backdrop",hideFlags=HideFlags.HideAndDontSave};materials.Add(draftBackMaterial);draftBackLow=!LowGraphics;
                draftBackMaterial.SetVector(BackToonId,BackV(NativeMenuStyle.DraftToon(NativeLighting.LegacyStyle)));
            }
            if(draftBackQuad==null)draftBackQuad=BackdropQuad();
            // The low graphics tier's cheaper pass (no motes or streams), switched only when the tier changes.
            if(draftBackLow!=LowGraphics){draftBackLow=LowGraphics;if(draftBackLow)draftBackMaterial.EnableKeyword(BackLowKeyword);else draftBackMaterial.DisableKeyword(BackLowKeyword);}
            // A cold start (the frame before drew the arena, a killcam or a menu) fades the hall in and runs the power-on front.
            bool entering=Time.frameCount!=draftBackFrame+1;draftBackFrame=Time.frameCount;
            var stage=draftStage;var eye=camera.transform;float line=stage.ShadeLine(trayLine>=0?trayLine:NativeDraftStage.DefaultTray),tanY=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);
            var input=new DraftBackdropInput{Dt=dt,Age=age,Pulse=pulse,Tint=new NVector(tint.r,tint.g,tint.b),Entering=entering,Reduced=reduced,Low=LowGraphics,Ward=DraftWard,Siren=siren,Prism=DraftPrism,
                HeadRow=DraftHeadLine,TrayRow=line,DeepRow=stage.ShadeDeep(line),CamPos=BackN(eye.position),CamForward=BackN(eye.forward),CamUp=BackN(eye.up),CamRight=BackN(eye.right),TanY=tanY,Aspect=camera.aspect,Near=camera.nearClipPlane,Far=camera.farClipPlane,
                Glow=BackN(Stage(NativeDraftBackdrop.GlowPoint(stage)))};
            var s=draftBack;NativeDraftBackdrop.Step(s,input);var m=draftBackMaterial;
            m.SetVector(BackCamId,BackV(s.Cam));m.SetVector(BackClockId,BackV(s.Clock));m.SetVector(BackFwdId,BackV(s.Fwd));m.SetVector(BackUpId,BackV(s.Up));m.SetVector(BackRightId,BackV(s.Right));
            m.SetVector(BackAccentId,BackV(s.Accent));m.SetVector(BackFrameId,BackV(s.Frame));m.SetVector(BackCounterId,BackV(s.Counter));m.SetVector(BackDeepId,BackV(s.Deep));m.SetVector(BackVoidId,BackV(s.Void));
            m.SetVector(BackPulseId,BackV(s.Pulse));m.SetVector(BackStyleId,BackV(s.Style));m.SetVector(BackFxId,BackV(s.Fx));m.SetVector(BackSafeId,BackV(s.Safe));m.SetVector(BackCapId,BackV(s.Cap));m.SetVector(BackGlowId,BackV(s.Glow));
            // The unit quad stretched over the view at the quad's distance, from just above the top to under the opaque shade.
            var frame=NativeDraftBackdrop.QuadFrame(s,tanY,camera.aspect);
            var matrix=eye.localToWorldMatrix*Matrix4x4.TRS(new Vector3(0,frame.Y,s.Distance),Quaternion.identity,new Vector3(frame.X,frame.Z,1));
            Graphics.DrawMesh(draftBackQuad,matrix,m,0,camera,0,null,ShadowCastingMode.Off,false);
        }
        private static NVector BackN(Vector3 v)=>new NVector(v.x,v.y,v.z);
        private static Vector4 BackV(NVector4 v)=>new Vector4(v.X,v.Y,v.Z,v.W);
        // A unit quad (corners at x, y = +-1) facing the camera's -z; both faces draw (Cull Off), its bounds generous so the
        // camera never culls it.
        private static Mesh BackdropQuad()
        {
            var q=new Mesh{name="Native draft backdrop quad",hideFlags=HideFlags.HideAndDontSave};
            q.vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};q.triangles=new[]{0,2,1,0,3,2};q.bounds=new Bounds(Vector3.zero,new Vector3(4,4,4));q.UploadMeshData(true);return q;
        }
    }
}
