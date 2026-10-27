using System;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    // The menu portraits on the model (owner 2026-10-26: "Player model still shows voxel in customization?"; "Model
    // everywhere"). While the model draws (the bake installed, the Player model not Classic) a pilot's portrait (the Locker's
    // live preview, the home pilot card, the lobby and results cards) is its visual body:
    //  - posed from the lobby pose the voxel portrait samples (Core/NativeLobbyPoses.cs, the standing fighters on their idle),
    //    turned on the turntable and framed exactly as the voxel portrait frames it (Core/NativePortraitRules.cs), so the
    //    card's backdrop and ring sit where they did; seated and reclining poses put the model's hips on the pose's pelvis;
    //  - dressed in the pilot's outfit and wearing its body skin's face gear, its hat, the held weapon and its gear (this
    //    client's own cards show its picks; another pilot's the default outfit until the seat carries a look);
    //  - on the podium (and the chair), lit by a key and a rim light that reach only the portrait layer.
    // Each slot's body stands far from the arena on the voxel portraits' layer (NativePortraitRenderer.Layer), which the arena
    // camera never draws; one orthographic camera renders the seen slots once a frame (NativeModelPortraitRenderer's
    // LateUpdate) into a multisampled texture, its coverage written by a second, alpha-only render (Resources/
    // NativeVisualPortraitMask.shader), resolved to straight alpha as the voxel portraits are. The Siren and Classic
    // keep the voxel portraits.
    public sealed partial class ArenaPresentation
    {
        public const int ModelPortraitSlots=12,ModelPortraitIdTop=-300;
        private const int PortraitLayer=NativePortraitRenderer.Layer;
        private sealed class ModelPortraitSlot
        {
            public int Key=int.MinValue,W,H,Frame=-10,Body=-1,Accent=-1;public float Seen=-10,Aspect=1,Top,Yaw;public PortraitLook Look;public string Outfit="",Skins,BodyHex,AccentHex;
            public readonly MeleeRig Rig=new MeleeRig();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();public readonly Actor Actor=new Actor();
            public CapturedAnimation.Player Player;public VisualBody Model;public RenderTexture Texture;public bool Ready;
        }
        private readonly ModelPortraitSlot[] modelPortraits=new ModelPortraitSlot[ModelPortraitSlots];
        private Transform modelPortraitHolder;private Camera modelPortraitView;private Material modelPortraitResolve;private float modelPortraitClock;private bool modelPortraitFailed;
        private Light modelPortraitKey;private UnityEngine.Rendering.SphericalHarmonicsL2 modelPortraitAmbient;private Shader modelPortraitMask;
        // VisualGear's layer and camera while a portrait draws (0 and every camera otherwise).
        private int gearLayer;private Camera gearCamera;
        /// <summary>The portraits' Reduced effects (the menu sets it with each request).</summary>
        public bool ModelPortraitReduced;
        /// <summary>Whether a look's portrait draws on the model (else the voxel portrait).</summary>
        public bool ModelPortraitFor(in PortraitLook look)=>!modelPortraitFailed&&!ClassicBodies&&VisualBodiesOn&&look.Spec!="siren"&&SystemInfo.graphicsDeviceType!=UnityEngine.Rendering.GraphicsDeviceType.Null;
        // A portrait body's outfit and skin map, by its id (VisualLookOf, SeatWeaponSkins); false for any other id.
        private bool ModelPortraitOf(int id,out ModelPortraitSlot slot)
        {
            int i=ModelPortraitIdTop-id;slot=i>=0&&i<ModelPortraitSlots?modelPortraits[i]:null;return slot!=null;
        }

        /// <summary>The model portrait for card `key` (its look, the outfit and skin map it wears, the card's size in kit units):
        /// last frame's render, or null until one is ready; `top` is the framing's top as the voxel portraits give it.</summary>
        public Texture ModelPortrait(int key,in PortraitLook look,string outfit,string skins,float width,float height,float pixelsPerUnit,out float top)
        {
            top=0;if(Event.current==null||Event.current.type!=EventType.Repaint)return null;
            if(!NativePortraitRules.TextureSize(width,height,pixelsPerUnit,NativePortraitRenderer.TextureCap,out int w,out int h))return null;
            ModelPortraitSlot slot=null,free=null;float now=Time.unscaledTime;
            for(int i=0;i<ModelPortraitSlots;i++){var s=modelPortraits[i]??=new ModelPortraitSlot();if(s.Key==key){slot=s;break;}if(free==null&&(s.Key==int.MinValue||now-s.Seen>.5f))free=s;}
            if(slot==null){if(free==null)return null;slot=free;ModelPortraitClear(slot);slot.Key=key;}
            slot.Look=look;slot.Outfit=outfit??"";slot.Skins=skins;slot.W=w;slot.H=h;slot.Aspect=width/height;slot.Yaw=look.Yaw;slot.Frame=Time.frameCount;slot.Seen=now;
            if(modelPortraitHolder==null){var go=new GameObject("Model portraits");go.transform.SetParent(transform,false);modelPortraitHolder=go.transform;go.AddComponent<NativeModelPortraitRenderer>().Presentation=this;}
            if(!slot.Ready||slot.Texture==null)return null;top=slot.Top;return slot.Texture;
        }
        private void ModelPortraitClear(ModelPortraitSlot s)
        {
            if(s.Texture!=null)RenderTexture.ReleaseTemporary(s.Texture);s.Texture=null;s.Ready=false;s.Key=int.MinValue;s.Frame=-10;
            if(s.Model!=null){VisualStrideRelease(s.Model);VisualMotionRelease(s.Model);s.Model=null;}
            if(s.Player!=null){s.Player.Dispose();s.Player=null;}
        }

        // Every seen slot, once a frame (NativeModelPortraitRenderer.LateUpdate); slots unseen for half a second release theirs.
        internal void RenderModelPortraits()
        {
            float now=Time.unscaledTime;bool any=false;
            foreach(var s in modelPortraits){if(s==null)continue;if(s.Key!=int.MinValue&&now-s.Seen>.5f)ModelPortraitClear(s);any|=s.Key!=int.MinValue&&s.Frame>=Time.frameCount-1;}
            if(!any||modelPortraitFailed||sourceModels==null)return;
            modelPortraitClock+=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(!ModelPortraitEnsure())return;
            if(VisualEye!=null)VisualEye.cullingMask&=~(1<<PortraitLayer);
            // The portraits' own light while they render: no fog, the key light as the sun, the hemisphere as the ambient
            // (the menu's scene keeps its own).
            var active=RenderTexture.active;bool fog=RenderSettings.fog;var sun=RenderSettings.sun;var ambient=RenderSettings.ambientProbe;
            RenderSettings.fog=false;RenderSettings.sun=modelPortraitKey;RenderSettings.ambientProbe=modelPortraitAmbient;
            try{for(int i=0;i<ModelPortraitSlots;i++){var s=modelPortraits[i];if(s!=null&&s.Key!=int.MinValue&&s.Frame>=Time.frameCount-1)ModelPortraitRender(s,i);}}
            catch(Exception e){modelPortraitFailed=true;Debug.LogWarning("Model portraits disabled: "+e.Message);}
            finally{RenderSettings.fog=fog;RenderSettings.sun=sun;RenderSettings.ambientProbe=ambient;RenderTexture.active=active;modelPortraitView.targetTexture=null;gearLayer=0;gearCamera=null;}
        }
        private bool ModelPortraitEnsure()
        {
            if(modelPortraitView!=null)return true;
            var blit=Resources.Load<Shader>("NativePortraitResolve");modelPortraitMask=Resources.Load<Shader>("NativeVisualPortraitMask");
            if(blit==null||!blit.isSupported||modelPortraitMask==null||!modelPortraitMask.isSupported){modelPortraitFailed=true;Debug.LogWarning("Model portraits disabled: a portrait shader is missing");return false;}
            modelPortraitResolve=new Material(blit){hideFlags=HideFlags.HideAndDontSave};modelPortraitResolve.SetVector("_PortraitToon",Vector4.zero);
            var go=new GameObject("Model portrait camera"){layer=PortraitLayer};go.transform.SetParent(modelPortraitHolder,false);var v=go.AddComponent<Camera>();v.enabled=false;
            v.cullingMask=1<<PortraitLayer;v.clearFlags=CameraClearFlags.SolidColor;v.backgroundColor=new Color(0,0,0,0);v.orthographic=true;v.nearClipPlane=NativePortraitRules.Near;v.farClipPlane=NativePortraitRules.Far;
            v.renderingPath=RenderingPath.Forward;v.allowHDR=false;v.allowMSAA=true;v.useOcclusionCulling=false;v.depthTextureMode=DepthTextureMode.None;
            // The source portraits' key and cool rim (NativePortraitRules), as directional lights on the portrait layer only.
            modelPortraitKey=Lamp("Model portrait key",NativePortraitRules.KeyColor,1.15f,NativePortraitRules.KeyPosition);Lamp("Model portrait rim",NativePortraitRules.RimColor,.55f,NativePortraitRules.RimPosition);
            // The hemisphere (sky over ground) as an ambient probe.
            Color sky=Rgb(NativePortraitRules.HemiSky)*.75f,ground=Rgb(NativePortraitRules.HemiGround)*.75f;var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();
            sh.AddAmbientLight((sky+ground)*.5f);sh.AddDirectionalLight(Vector3.up,(sky-ground)*.5f,1);modelPortraitAmbient=sh;
            modelPortraitView=v;return true;
        }
        private static Color Rgb(int rgb)=>new Color((rgb>>16&255)/255f,(rgb>>8&255)/255f,(rgb&255)/255f);
        private Light Lamp(string name,int rgb,float intensity,System.Numerics.Vector3 from)
        {
            var go=new GameObject(name){layer=PortraitLayer};go.transform.SetParent(modelPortraitHolder,false);var l=go.AddComponent<Light>();
            l.type=LightType.Directional;l.color=Rgb(rgb);l.intensity=intensity;l.cullingMask=1<<PortraitLayer;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.ForcePixel;
            go.transform.rotation=Quaternion.LookRotation(-CapturedAnimation.Vec(CapturedRig.Mirror(from)).normalized);return l;
        }

        private void ModelPortraitRender(ModelPortraitSlot s,int index)
        {
            var look=s.Look;string spec=string.IsNullOrEmpty(look.Spec)?"pistol":look.Spec,hat=string.IsNullOrEmpty(look.Hat)?"none":look.Hat;
            int pose=NativeLobbyPoses.Effective(spec,look.Pose),id=ModelPortraitIdTop-index;float time=ModelPortraitReduced?0:modelPortraitClock;var view=modelPortraitView;
            // The pose, as the voxel portrait samples it.
            if(NativeLobbyPoses.UsesIdle(spec,pose)){s.Pose.Reset();if(NativeSourceMotion.Sample(spec+"-idle",time,0,s.Pose))s.Rig.ReadPose(s.Pose);else s.Rig.Reset();}
            NativeLobbyPoses.Sample(s.Rig,spec,pose,time,hat,ModelPortraitReduced,s.Key,out var weapon,out _,out _);s.Rig.WritePose(s.Pose);s.Pose.World();
            // Its stage far from the arena and the other slots; the turntable turns the pilot about the podium's axis.
            var origin=new Vector3(3000+index*60,-500,0);var spin=Quaternion.Euler(0,s.Yaw,0);
            s.Player??=captured.Create(modelPortraitHolder,id);
            s.Player.Root.transform.SetPositionAndRotation(origin+Vector3.up*NativePortraitRules.GroupY,spin);
            NativePoseWriter.Write(s.Player.Bones,s.Pose,"a model portrait");
            // The pilot as an actor (its colours derived once per change), its weapon away when the pose holds none.
            var a=s.Actor;a.Id=id;a.SourceSpec=spec;a.Hat=hat;a.HatColor=look.HatColor;a.WeaponAway=!weapon.Visible;a.Grounded=true;
            if(s.Body!=look.Body||s.BodyHex==null){s.Body=look.Body;s.BodyHex="#"+look.Body.ToString("x6");}
            if(s.Accent!=look.Accent||s.AccentHex==null){s.Accent=look.Accent;s.AccentHex="#"+look.Accent.ToString("x6");}
            a.BodyColor=s.BodyHex;a.AccentColor=s.AccentHex;
            // The body in its outfit (made again when the outfit changes), on the portrait layer, casting no shadow.
            if(s.Model!=null&&(s.Model.Root==null||s.Model.Hood!=(hat==NativeSheldonHead.Hat)||!string.Equals(s.Model.Look,s.Outfit,StringComparison.Ordinal))){VisualStrideRelease(s.Model);VisualMotionRelease(s.Model);if(s.Model.Root!=null){s.Model.Root.SetActive(false);Destroy(s.Model.Root);}s.Model=null;}
            if(s.Model==null)
            {
                s.Model=CreateVisualBody(s.Player,id,hat==NativeSheldonHead.Hat);if(s.Model==null){modelPortraitFailed=true;return;}
                foreach(var t in s.Model.Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=PortraitLayer;
                foreach(var r in s.Model.All)r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            var body=s.Model;body.Root.transform.localPosition=new Vector3(0,NativeVisualRig.Floor,0);
            // Bound while it draws (VisualJointFrame and the gear find it), then out of the arena's bodies again.
            visualBodies[s.Player]=body;
            try
            {
                var held=VisualBodyBind(body,s.Player,a,time,s.Pose,false,false);
                if(pose==NativeLobbyPoses.Chair||pose==NativeLobbyPoses.Sleep)
                {
                    int hips=CapturedRig.Pelvis<body.Binding.Rig.Of.Length?body.Binding.Rig.Of[CapturedRig.Pelvis]:-1;
                    if(hips>=0&&body.Binding.Bones[hips]!=null){var shift=s.Player.Bones[CapturedRig.Pelvis].position-body.Binding.Bones[hips].position;body.Root.transform.position+=shift;for(int side=0;side<2;side++)s.Player.Bones[side==0?CapturedRig.LHand:CapturedRig.RHand].position+=shift;}
                }
                var right=s.Player.Bones[CapturedRig.RHand];body.RightScale=right.localScale;VisualWeaponFit(right,held);
                // The held weapon where the lobby pose holds it (on the hand, now on the model's wrist), else none.
                if(weapon.Visible)VisualWeaponDrawn(s.Player,s.Player.Bones[weapon.Bone].localToWorldMatrix*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(weapon.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(weapon.Rotation)),Vector3.one));
                if(body.Shown!=null&&body.ShownFrame!=Time.frameCount){if(body.Shown.Model!=null)body.Shown.Model.SetActive(false);body.Shown=null;}
                // The scene: the podium, the chair when seated, the hat, the gear (core, device, face gear, emblem).
                sourceModels.Begin();gearLayer=PortraitLayer;gearCamera=view;
                sourceModels.QueuePiece(WeaponShape.Sphere,false,Matrix4x4.TRS(origin+Vector3.up*NativePortraitRules.PodiumY,Quaternion.identity,new Vector3(NativePortraitRules.PodiumTop+NativePortraitRules.PodiumBottom,NativePortraitRules.PodiumHeight,NativePortraitRules.PodiumTop+NativePortraitRules.PodiumBottom)),PortraitPodium,PortraitLayer,view);
                if(pose==NativeLobbyPoses.Chair)foreach(var piece in NativePortraitRules.Chair)
                {
                    int c=piece.Color;var at=origin+spin*CapturedAnimation.Vec(CapturedRig.Mirror(piece.Position));
                    sourceModels.QueuePiece(WeaponShape.Cube,false,Matrix4x4.TRS(at,spin,CapturedAnimation.Vec(piece.Size)),new Color((c>>16&255)/255f,(c>>8&255)/255f,(c&255)/255f).linear,PortraitLayer,view);
                }
                VisualHatHair(body,hat);
                if(hat!="none"&&hat!=NativeSheldonHead.Hat)
                {
                    var hb=VisualHatBone(s.Player,hat);
                    if(!AuditorHalo(hat,hb(CapturedRig.Head),time,1,ModelPortraitReduced,PortraitLayer,null,view,look.HatColor))sourceModels.Queue(HatModel(hat,look.HatColor),s.Player.Root.transform.localToWorldMatrix,hb,null,PortraitLayer,view,body:HatBody(hat,look.HatColor));
                }
                else if(hat==NativeSheldonHead.Hat)SheldonHoodStill(body,hat,look.HatColor,s.Player.Root.transform.localToWorldMatrix,VisualHatBone(s.Player,hat),time,id,ModelPortraitReduced,PortraitLayer,view);
                VisualGear(body,a,time,null,null,1,false);
                sourceModels.Flush(UsesInstancing);
                // The framing: the voxel portrait's, to the model's crown (its hair, and a hat over it).
                var head=VisualHeadOf(body);float crown=head!=null?head.position.y-origin.y+(hat!="none"?.38f:.26f):1.72f;
                s.Top=pose==NativeLobbyPoses.Sleep?0:Mathf.Max(1.72f,crown);
                var frame=NativePortraitRules.Frame(pose,s.Aspect,s.Top);
                var target=RenderTexture.GetTemporary(s.W,s.H,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,ModelPortraitSamples());target.wrapMode=TextureWrapMode.Clamp;
                try
                {
                    view.targetTexture=target;view.aspect=s.Aspect;view.orthographicSize=frame.Half;
                    view.transform.SetPositionAndRotation(origin+CapturedAnimation.Vec(CapturedRig.Mirror(frame.Position)),Quaternion.LookRotation(CapturedAnimation.Vec(CapturedRig.Mirror(frame.Target-frame.Position)),Vector3.up));
                    view.Render();
                    // The coverage: the body's lit passes keep the cleared alpha, so a second render (depth kept) writes it.
                    view.clearFlags=CameraClearFlags.Nothing;view.RenderWithShader(modelPortraitMask,"RenderType");view.clearFlags=CameraClearFlags.SolidColor;
                    if(s.Texture==null||s.Texture.width!=s.W||s.Texture.height!=s.H){if(s.Texture!=null)RenderTexture.ReleaseTemporary(s.Texture);s.Texture=RenderTexture.GetTemporary(s.W,s.H,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,1);s.Texture.wrapMode=TextureWrapMode.Clamp;}
                    Graphics.Blit(target,s.Texture,modelPortraitResolve);s.Ready=true;
                }
                finally{view.targetTexture=null;RenderTexture.ReleaseTemporary(target);}
            }
            finally{visualBodies.Remove(s.Player);gearLayer=0;gearCamera=null;}
        }
        private static readonly Color PortraitPodium=new Color(.2f,.2f,.25f).linear;
        private static int modelPortraitMsaa;
        private static int ModelPortraitSamples()
        {
            if(modelPortraitMsaa==0){var d=new RenderTextureDescriptor(256,256,RenderTextureFormat.ARGB32,24){msaaSamples=4};modelPortraitMsaa=Math.Max(1,SystemInfo.GetRenderTextureSupportedMSAASampleCount(d));}
            return modelPortraitMsaa;
        }
    }
    // Renders the model portraits after the frame's updates (ArenaPresentation.RenderModelPortraits).
    public sealed class NativeModelPortraitRenderer : MonoBehaviour
    {
        public ArenaPresentation Presentation;
        private void LateUpdate(){if(Presentation!=null)Presentation.RenderModelPortraits();}
    }
}
