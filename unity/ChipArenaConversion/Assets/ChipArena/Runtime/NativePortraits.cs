using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using NVector=System.Numerics.Vector3;
using NQuaternion=System.Numerics.Quaternion;
namespace ChipArena.UnityConversion
{
    // What a portrait shows: the pilot's weapon, lobby pose, hat and colours (#rrggbb as ints). A value, compared field by
    // field, so a card asks for its portrait every frame without allocating.
    public struct PortraitLook
    {
        public string Spec,Pose,Hat;public int Body,Accent;
        public bool Same(in PortraitLook o)=>Spec==o.Spec&&Pose==o.Pose&&Hat==o.Hat&&Body==o.Body&&Accent==o.Accent;
    }
    // The live 3D pilot portraits of the menu, lobby and results cards (client/menu-portraits.ts MenuPortraits at 05950ea):
    // the pilot's own source model in its colours, with its hat (the Auditor halo animated) and weapon, in its lobby pose
    // (Core/NativeLobbyPoses.cs), on the podium (and the chair), framed and lit as the source frames and lights it.
    //  - One disabled orthographic Camera on its own layer renders each visible portrait by hand (Camera.Render) into a
    //    pooled multisampled RenderTexture the card's physical size (capped), then resolves it to straight alpha in the slot's
    //    pooled texture (empty texels carry the edge colour, so bilinear sampling keeps the silhouette's anti-aliasing),
    //    which the card draws through the kit (so scroll clips apply) the next frame.
    //  - The portrait meshes are queued for that camera only (Graphics.DrawMesh with the camera), each portrait at its own
    //    far-away stage so one render never sees another's pilot; no GameObject, renderer or light is added to the scene.
    //    The portrait shader lights itself (Resources/NativePortrait.shader) and reads no scene light, ambient, fog or shadow.
    //  - Only portraits requested by this frame's cards render, at most once per frame each; slots unseen for half a second,
    //    resized or orphaned by a resolution change release their textures, and everything is released on destroy.
    public sealed class NativePortraitRenderer : MonoBehaviour
    {
        // The portrait layer: 8 actors, 9 props, 10 the first-person view model, 11 summons; 30 is unused elsewhere. The
        // match camera's mask drops it (Initialize) and the view-model camera only draws 10.
        public const int Layer=30,Slots=12,TextureCap=1280;
        private sealed class Slot
        {
            public int Key=int.MinValue,W,H,Frame=-10,TopPose=-1;public float Seen=-10,Aspect=1,Top;public PortraitLook Look;public bool Ready;public RenderTexture Texture;
            public readonly MeleeRig Rig=new MeleeRig();public readonly CapturedRig.Pose Pose=new CapturedRig.Pose();
            // Derived once per look (no per-frame strings or colour maths): model ids, the idle clip and the colour tints.
            public string Spec="pistol",Hat="none",WeaponModel,HatModel,IdleClip;public Vector4 BodyTint,AccentTint,EdgeTint,FlexTint;
            public void Derive(in PortraitLook look)
            {
                Look=look;Spec=string.IsNullOrEmpty(look.Spec)?"pistol":look.Spec;Hat=Spec=="siren"||string.IsNullOrEmpty(look.Hat)?"none":look.Hat;
                WeaponModel="weapon-"+Spec;HatModel="hat-"+Hat;IdleClip=Spec=="siren"?"siren-idle-melee":Spec+"-idle";TopPose=-1;
                BodyTint=V4(NativePortraitRules.BodyTint(look.Body));AccentTint=V4(NativePortraitRules.AccentTint(look.Accent));EdgeTint=V4(NativePortraitRules.EdgeTint(look.Body));FlexTint=V4(NativePortraitRules.FlexTint(look.Body));
            }
        }
        private struct Part {public Mesh Mesh;public int Bone,Class,Joint;public bool Transparent,DoubleSide;public float Ink;public Texture Texture;public Vector4 St;}
        private readonly Slot[] slots=new Slot[Slots];
        private readonly Dictionary<string,Part[]> parts=new Dictionary<string,Part[]>();private readonly Dictionary<string,Bounds> hatBounds=new Dictionary<string,Bounds>();
        private readonly Matrix4x4[] bones=new Matrix4x4[22];
        private ArenaPresentation presentation;private Camera view;private MaterialPropertyBlock block;
        private Material opaque,opaqueDouble,transparent,transparentDouble,additive,resolve;private Mesh box,podium,arc,aura,flame;
        private bool failed;private float clock;private int screenW,screenH;
        private static readonly int ColorId=Shader.PropertyToID("_Color"),ModeId=Shader.PropertyToID("_Mode"),MainTexId=Shader.PropertyToID("_MainTex"),MainStId=Shader.PropertyToID("_MainTex_ST"),
            BodyId=Shader.PropertyToID("_BodyTint"),AccentId=Shader.PropertyToID("_AccentTint"),SceneId=Shader.PropertyToID("_SceneTint"),GlowId=Shader.PropertyToID("_GlowTint"),InkId=Shader.PropertyToID("_Ink");
        public bool Reduced;
        // Whether portraits can render here (a graphics device, render textures, the shaders and the source models).
        public bool Supported=>!failed&&presentation!=null&&presentation.PortraitModels!=null&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null&&SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32);
        public void Initialize(ArenaPresentation source,Camera arenaCamera)
        {
            presentation=source;for(int i=0;i<Slots;i++)if(slots[i]==null)slots[i]=new Slot();
            if(arenaCamera!=null)arenaCamera.cullingMask&=~(1<<Layer);
        }
        // From a card's OnGUI repaint: the portrait texture for `key` showing `look` at `width` x `height` canvas units
        // (`pixelsPerUnit` physical pixels each), or null while it is not ready (the card draws its emblem instead). `top`
        // is the hat top the last render framed with (0: a bare head), so the card's frame can follow the framing.
        public Texture Request(int key,in PortraitLook look,float width,float height,float pixelsPerUnit,out float top)
        {
            top=0;if(!Supported||Event.current==null||Event.current.type!=EventType.Repaint)return null;
            if(!NativePortraitRules.TextureSize(width,height,pixelsPerUnit,TextureCap,out int w,out int h))return null;
            Slot slot=null,free=null;float now=Time.unscaledTime;
            foreach(var s in slots){if(s.Key==key){slot=s;break;}if(free==null&&(s.Key==int.MinValue||now-s.Seen>.5f))free=s;}
            if(slot==null){if(free==null)return null;slot=free;Clear(slot);slot.Key=key;slot.Derive(look);}
            else if(!slot.Look.Same(look))slot.Derive(look);
            slot.W=w;slot.H=h;slot.Aspect=width/height;slot.Frame=Time.frameCount;slot.Seen=now;
            if(!slot.Ready||slot.Texture==null)return null;top=slot.Top;return slot.Texture;
        }
        private static void Clear(Slot s){if(s.Texture!=null)RenderTexture.ReleaseTemporary(s.Texture);s.Texture=null;s.Ready=false;s.Key=int.MinValue;s.TopPose=-1;s.Frame=-10;}
        private void LateUpdate()
        {
            if(slots[0]==null)return;float now=Time.unscaledTime;bool any=false;
            if(Screen.width!=screenW||Screen.height!=screenH){screenW=Screen.width;screenH=Screen.height;foreach(var s in slots)Clear(s);return;}
            foreach(var s in slots){if(s.Key!=int.MinValue&&now-s.Seen>.5f)Clear(s);any|=s.Key!=int.MinValue&&s.Frame>=Time.frameCount-1;}
            if(!any||!Supported)return;
            clock+=Mathf.Min(Time.unscaledDeltaTime,.05f);float time=Reduced?0:clock;
            if(!Ensure())return;
            var active=RenderTexture.active;
            try{for(int i=0;i<Slots;i++){var s=slots[i];if(s.Key!=int.MinValue&&s.Frame>=Time.frameCount-1)Render(s,i,time);}}
            catch(Exception e){failed=true;Debug.LogWarning("Pilot portraits disabled: "+e.Message);}
            finally{RenderTexture.active=active;view.targetTexture=null;}
        }
        private bool Ensure()
        {
            if(view!=null)return true;
            var lit=Resources.Load<Shader>("NativePortrait");var blit=Resources.Load<Shader>("NativePortraitResolve");
            if(lit==null||blit==null||!lit.isSupported||!blit.isSupported){failed=true;Debug.LogWarning("Pilot portraits disabled: portrait shaders missing or unsupported");return false;}
            var go=new GameObject("Pilot portrait camera"){layer=Layer};go.transform.SetParent(transform,false);view=go.AddComponent<Camera>();view.enabled=false;
            view.cullingMask=1<<Layer;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(0,0,0,0);view.orthographic=true;view.nearClipPlane=NativePortraitRules.Near;view.farClipPlane=NativePortraitRules.Far;
            view.renderingPath=RenderingPath.Forward;view.allowHDR=false;view.allowMSAA=true;view.useOcclusionCulling=false;view.depthTextureMode=DepthTextureMode.None;
            Material Make(CullMode cull,BlendMode src,BlendMode dst,BlendMode srcA,BlendMode dstA,bool write,int queue,int alpha,bool both)
            {
                var m=new Material(lit){renderQueue=queue,hideFlags=HideFlags.HideAndDontSave};m.SetFloat("_Cull",(float)cull);m.SetFloat("_SrcBlend",(float)src);m.SetFloat("_DstBlend",(float)dst);m.SetFloat("_SrcAlpha",(float)srcA);m.SetFloat("_DstAlpha",(float)dstA);
                m.SetFloat("_ZWrite",write?1:0);m.SetFloat("_UseAlpha",alpha);m.SetFloat("_DoubleSide",both?1:0);m.SetFloat("_Exposure",NativePortraitRules.Exposure);
                m.SetVector("_HemiSky",Light(NativePortraitRules.HemiSky,NativePortraitRules.HemiIntensity));m.SetVector("_HemiGround",Light(NativePortraitRules.HemiGround,NativePortraitRules.HemiIntensity));
                m.SetVector("_KeyColor",Light(NativePortraitRules.KeyColor,NativePortraitRules.KeyIntensity));m.SetVector("_RimColor",Light(NativePortraitRules.RimColor,NativePortraitRules.RimIntensity));
                m.SetVector("_KeyDir",Direction(NativePortraitRules.KeyPosition));m.SetVector("_RimDir",Direction(NativePortraitRules.RimPosition));return m;
            }
            opaque=Make(CullMode.Back,BlendMode.One,BlendMode.Zero,BlendMode.One,BlendMode.Zero,true,2000,0,false);opaqueDouble=Make(CullMode.Off,BlendMode.One,BlendMode.Zero,BlendMode.One,BlendMode.Zero,true,2000,0,true);
            transparent=Make(CullMode.Back,BlendMode.SrcAlpha,BlendMode.OneMinusSrcAlpha,BlendMode.One,BlendMode.OneMinusSrcAlpha,false,3000,1,false);transparentDouble=Make(CullMode.Off,BlendMode.SrcAlpha,BlendMode.OneMinusSrcAlpha,BlendMode.One,BlendMode.OneMinusSrcAlpha,false,3000,1,true);
            // Additive glow (three AdditiveBlending): the shader writes colour premultiplied by opacity and, as coverage, that
            // colour's brightest channel, so the straight-alpha resolve keeps the glow's full added light over the clear
            // background (the source's premultiplied canvas shows it as added light over the card).
            additive=Make(CullMode.Back,BlendMode.One,BlendMode.One,BlendMode.One,BlendMode.One,false,3010,2,false);
            resolve=new Material(blit){hideFlags=HideFlags.HideAndDontSave};block=new MaterialPropertyBlock();
            var m0=new PortraitMesh();NativePortraitRules.Box(m0);box=Build(m0,"Portrait box");m0.Clear();
            NativePortraitRules.Cylinder(m0,NativePortraitRules.PodiumTop,NativePortraitRules.PodiumBottom,NativePortraitRules.PodiumHeight,NativePortraitRules.PodiumSegments);podium=Build(m0,"Portrait podium");m0.Clear();
            NativePortraitRules.Torus(m0,NativePortraitRules.HaloArcRadius,NativePortraitRules.HaloArcTube,4,22,NativePortraitRules.HaloArc);arc=Build(m0,"Auditor halo arc");m0.Clear();
            NativePortraitRules.Torus(m0,NativePortraitRules.HaloArcRadius,NativePortraitRules.HaloAuraTube,4,22,NativePortraitRules.HaloArc);aura=Build(m0,"Auditor halo glow");m0.Clear();
            NativePortraitRules.Flame(m0);flame=Build(m0,"Auditor halo flame");
            return true;
        }
        private static Vector4 Light(int hex,float intensity){var c=NativePortraitRules.Linear(hex)*intensity;return new Vector4(c.X,c.Y,c.Z,0);}
        private static Vector4 Direction(NVector source){var d=NVector.Normalize(CapturedRig.Mirror(source));return new Vector4(d.X,d.Y,d.Z,0);}
        private static Vector4 Linear(int hex,float alpha=1){var c=NativePortraitRules.Linear(hex);return new Vector4(c.X,c.Y,c.Z,alpha);}
        private static Vector4 V4(NVector v,float w=1)=>new Vector4(v.X,v.Y,v.Z,w);
        // A source-frame mesh into Unity: x mirrored and the winding flipped (as tools/export_models.mjs converts the models).
        private static Mesh Build(PortraitMesh m,string name)
        {
            int n=m.Positions.Count;var p=new Vector3[n];var nn=new Vector3[n];var c=new Color[n];var e=new List<Vector4>(n);
            for(int i=0;i<n;i++){p[i]=CapturedAnimation.Vec(CapturedRig.Mirror(m.Positions[i]));nn[i]=CapturedAnimation.Vec(CapturedRig.Mirror(m.Normals[i]));c[i]=Color.white;e.Add(Vector4.zero);}
            var t=new int[m.Indices.Count];for(int i=0;i+2<t.Length;i+=3){t[i]=m.Indices[i];t[i+1]=m.Indices[i+2];t[i+2]=m.Indices[i+1];}
            var mesh=new Mesh{name=name,hideFlags=HideFlags.HideAndDontSave};mesh.vertices=p;mesh.normals=nn;mesh.colors=c;mesh.SetUVs(1,e);mesh.triangles=t;mesh.RecalculateBounds();mesh.UploadMeshData(true);return mesh;
        }
        private Part[] Parts(string id)
        {
            if(parts.TryGetValue(id,out var list)&&(list.Length==0||list[0].Mesh!=null))return list;
            var models=presentation.PortraitModels;var found=new List<Part>();
            for(int i=0,n=models.PortraitPartCount(id);i<n;i++)
            {
                models.PortraitPart(id,i,out var mesh,out int bone,out string name,out var material);if(mesh==null||material==null)continue;
                int cls=id=="pilot"?NativePortraitRules.PartClass(name):NativePortraitRules.PartOther;if(cls==NativePortraitRules.PartHidden)continue;
                var tex=material.mainTexture;var scale=material.mainTextureScale;var offset=material.mainTextureOffset;
                found.Add(new Part{Mesh=mesh,Bone=bone,Class=cls,Joint=id=="pilot"&&NativeLobbyPoses.Cover(name,out int joint)?joint:-1,Transparent=material.renderQueue>=3000,DoubleSide=material.GetFloat("_Cull")==0,Ink=material.GetFloat("_Ink"),Texture=tex,St=new Vector4(scale.x,scale.y,offset.x,offset.y)});
            }
            list=found.ToArray();parts[id]=list;return list;
        }
        // A hat model's head-space bounds in the source frame (the models are stored x-mirrored).
        private bool HatBounds(string model,out NVector min,out NVector max)
        {
            min=max=NVector.Zero;if(!hatBounds.TryGetValue(model,out var b))
            {
                bool any=false;foreach(var p in Parts(model)){if(p.Mesh==null)continue;if(!any){b=p.Mesh.bounds;any=true;}else b.Encapsulate(p.Mesh.bounds);}
                if(!any)return false;hatBounds[model]=b;
            }
            min=new NVector(-b.max.x,b.min.y,b.min.z);max=new NVector(-b.min.x,b.max.y,b.max.z);return true;
        }
        private void Render(Slot s,int index,float time)
        {
            string spec=s.Spec,hat=s.Hat;bool siren=spec=="siren";int pose=NativeLobbyPoses.Effective(spec,s.Look.Pose);
            // The idle the standing fighters and the Siren keep (the source's own baked idles, as the arena and chip picker use).
            if(NativeLobbyPoses.UsesIdle(spec,pose)){s.Pose.Reset();if(NativeSourceMotion.Sample(s.IdleClip,time,0,s.Pose))s.Rig.ReadPose(s.Pose);else s.Rig.Reset();}
            NativeLobbyPoses.Sample(s.Rig,spec,pose,time,out var weapon,out var offhand);s.Rig.WritePose(s.Pose);s.Pose.World();
            // hatTop() once per outfit and pose (menu-portraits.ts measures it at the outfit's first frame).
            if(s.TopPose!=pose){s.TopPose=pose;s.Top=hat=="none"?0:hat=="auditor-halo"?NativePortraitRules.HaloTop(s.Pose,time):HatBounds(s.HatModel,out var min,out var max)?NativePortraitRules.HatTop(s.Pose,min,max):0;}
            var frame=NativePortraitRules.Frame(pose,s.Aspect,s.Top);
            // Each slot's stage stands far from the others and from the arena, so one render never frames another pilot.
            var origin=new Vector3(2000+index*60,-500,0);var root=origin+Vector3.up*NativePortraitRules.GroupY;
            for(int b=0;b<22;b++)bones[b]=Matrix4x4.TRS(root+CapturedAnimation.Vec(CapturedRig.Mirror(s.Pose.WorldPosition[b])),CapturedAnimation.Quat(CapturedRig.Mirror(s.Pose.WorldRotation[b])),Vector3.one);
            var rootMatrix=Matrix4x4.Translate(root);
            // Scene: the podium, the chair when sitting, the pilot, its hat or halo, the held weapons.
            Basic(podium,Matrix4x4.TRS(origin+Vector3.up*NativePortraitRules.PodiumY,Quaternion.identity,Vector3.one),NativePortraitRules.PodiumColor,0,opaque);
            if(pose==NativeLobbyPoses.Chair)foreach(var piece in NativePortraitRules.Chair)Basic(box,Matrix4x4.TRS(origin+CapturedAnimation.Vec(CapturedRig.Mirror(piece.Position)),Quaternion.identity,CapturedAnimation.Vec(piece.Size)),piece.Color,0,opaque);
            foreach(var p in Parts(siren?"siren":"pilot"))
            {
                var m=p.Bone>=0&&p.Bone<22?bones[p.Bone]:rootMatrix;if(p.Joint>=0)m*=Matrix4x4.Rotate(CapturedAnimation.Quat(CapturedRig.Mirror(MeleeMath.AxisAngle(MeleeMath.X,NativeLobbyPoses.CoverAngle(s.Rig,p.Joint)))));
                var scene=p.Class==NativePortraitRules.PartEdge?s.EdgeTint:p.Class==NativePortraitRules.PartFlex?s.FlexTint:Vector4.one;
                Model(p,m,siren?Vector4.one:s.BodyTint,siren?Vector4.one:s.AccentTint,scene,siren?Vector4.one:Vector4.zero);
            }
            if(!siren&&hat=="auditor-halo")Halo(bones[CapturedRig.Head],time);
            else if(!siren&&hat!="none")foreach(var p in Parts(s.HatModel))Model(p,p.Bone>=0&&p.Bone<22?bones[p.Bone]:rootMatrix,Vector4.one,Vector4.one,Vector4.one,Vector4.one);
            if(weapon.Visible||offhand.Visible)foreach(var p in Parts(s.WeaponModel))
            {
                LobbyHeld held;if(weapon.Visible&&p.Bone==weapon.Bone)held=weapon;else if(offhand.Visible&&p.Bone==offhand.Bone)held=offhand;else continue;
                Model(p,bones[held.Bone]*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(held.Position)),CapturedAnimation.Quat(CapturedRig.Mirror(held.Rotation)),Vector3.one),Vector4.one,Vector4.one,Vector4.one,Vector4.one);
            }
            // The camera: menu-portraits.ts framePortrait (orthographic, looking at the framing centre).
            var target=RenderTexture.GetTemporary(s.W,s.H,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,Samples(s.W,s.H));target.wrapMode=TextureWrapMode.Clamp;
            try
            {
                view.targetTexture=target;view.aspect=s.Aspect;view.orthographicSize=frame.Half;
                view.transform.SetPositionAndRotation(origin+CapturedAnimation.Vec(CapturedRig.Mirror(frame.Position)),Quaternion.LookRotation(CapturedAnimation.Vec(CapturedRig.Mirror(frame.Target-frame.Position)),Vector3.up));
                view.Render();
                if(s.Texture==null||s.Texture.width!=s.W||s.Texture.height!=s.H){if(s.Texture!=null)RenderTexture.ReleaseTemporary(s.Texture);s.Texture=RenderTexture.GetTemporary(s.W,s.H,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,1);s.Texture.filterMode=FilterMode.Bilinear;s.Texture.wrapMode=TextureWrapMode.Clamp;}
                Graphics.Blit(target,s.Texture,resolve);s.Ready=true;
            }
            finally{view.targetTexture=null;RenderTexture.ReleaseTemporary(target);}
        }
        // 4x MSAA where the device supports it for this format (asked once; the answer does not depend on the size).
        private static int msaa;
        private static int Samples(int w,int h)
        {
            if(msaa==0){var d=new RenderTextureDescriptor(256,256,RenderTextureFormat.ARGB32,24){msaaSamples=4};msaa=Math.Max(1,SystemInfo.GetRenderTextureSupportedMSAASampleCount(d));}
            return msaa;
        }
        private void Draw(Mesh mesh,Matrix4x4 m,Material material){Graphics.DrawMesh(mesh,m,material,Layer,view,0,block,ShadowCastingMode.Off,false,null,LightProbeUsage.Off,null);}
        // A plain three material: Lambert (mode 0), MeshBasic (1), MeshBasic not tone mapped (2).
        private void Basic(Mesh mesh,Matrix4x4 m,int hex,int mode,Material material,float alpha=1)
        {
            block.Clear();block.SetVector(ColorId,Linear(hex,alpha));block.SetFloat(ModeId,mode);block.SetTexture(MainTexId,Texture2D.whiteTexture);block.SetVector(MainStId,new Vector4(1,1,0,0));
            block.SetVector(BodyId,Vector4.one);block.SetVector(AccentId,Vector4.one);block.SetVector(SceneId,Vector4.one);block.SetVector(GlowId,Vector4.one);block.SetFloat(InkId,0);Draw(mesh,m,material);
        }
        private void Model(in Part p,Matrix4x4 m,Vector4 body,Vector4 accent,Vector4 scene,Vector4 glow)
        {
            if(p.Mesh==null)return;block.Clear();block.SetVector(ColorId,Vector4.one);block.SetFloat(ModeId,0);block.SetTexture(MainTexId,p.Texture!=null?p.Texture:Texture2D.whiteTexture);block.SetVector(MainStId,p.Texture!=null?p.St:new Vector4(1,1,0,0));
            block.SetVector(BodyId,body);block.SetVector(AccentId,accent);block.SetVector(SceneId,scene);block.SetVector(GlowId,glow);block.SetFloat(InkId,p.Ink);
            Draw(p.Mesh,m,p.Transparent?(p.DoubleSide?transparentDouble:transparent):p.DoubleSide?opaqueDouble:opaque);
        }
        // auditor-halo.ts: three red arcs with additive glow tubes and twelve flickering black flames, turning and bobbing.
        private void Halo(Matrix4x4 head,float time)
        {
            NativePortraitRules.HaloGroup(time,out var gp,out var gq,out float gs);var group=head*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(gp)),CapturedAnimation.Quat(CapturedRig.Mirror(gq)),Vector3.one*gs);
            for(int i=0;i<3;i++)
            {
                NativePortraitRules.HaloArcLocal(i,out var ap,out var aq);var m=group*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(ap)),CapturedAnimation.Quat(CapturedRig.Mirror(aq)),Vector3.one);
                Basic(arc,m,NativePortraitRules.HaloRed,2,opaque);Basic(aura,m,NativePortraitRules.HaloGlow,2,additive,NativePortraitRules.HaloAuraOpacity);
            }
            for(int n=0;n<NativePortraitRules.HaloFlames;n++)
            {NativePortraitRules.HaloFlame(n,time,out var fp,out var fq,out var fs);Basic(flame,group*Matrix4x4.TRS(CapturedAnimation.Vec(CapturedRig.Mirror(fp)),CapturedAnimation.Quat(CapturedRig.Mirror(fq)),CapturedAnimation.Vec(fs)),NativePortraitRules.HaloBlack,1,opaqueDouble);}
        }
        private void OnDestroy()
        {
            foreach(var s in slots)if(s!=null)Clear(s);
            foreach(var o in new UnityEngine.Object[]{opaque,opaqueDouble,transparent,transparentDouble,additive,resolve,box,podium,arc,aura,flame})if(o!=null)CapturedAnimation.Release(o);
            if(view!=null)CapturedAnimation.Release(view.gameObject);view=null;parts.Clear();hatBounds.Clear();
        }
    }
    public sealed partial class ArenaPresentation
    {
        // The loaded source models, shared with the menu portraits (they draw the same meshes).
        internal NativeSourceModels PortraitModels=>sourceModels;
    }
    public sealed partial class NativeSourceModels
    {
        // The parts of model `id` for the portrait renderer: mesh, joint, part name and the source material.
        public int PortraitPartCount(string id)=>id!=null&&models.TryGetValue(id,out var list)?list.Length:0;
        public void PortraitPart(string id,int index,out Mesh mesh,out int bone,out string name,out Material material)
        {var p=models[id][index];mesh=p.Mesh;bone=p.Bone;name=p.Name;material=p.Material;}
    }
}
