using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The match camera's bloom: a soft HDR halo round additive glow cores, sparks and neon (parameters and the level plan:
    // Core/NativeBloomRules.cs; the passes: Resources/NativeBloom.shader). Unity-only, cosmetic, screen space.
    //  * Where: only on the match camera (ConversionGame.Awake attaches it right after creating that camera, which renders
    //    HDR: allowHDR is set there). The portrait camera (its own target, LDR), the Raging Demon victim screen and the
    //    first-person view-model camera never get it; the view model draws over the bloomed world.
    //  * Order: Unity runs a camera's OnRenderImage effects in component order. This one is added with the camera, before
    //    any other image effect can exist, and is only ever enabled or disabled after that (never removed and re-added), so
    //    it always runs first: NativeDamageScreenPass adds NativeDamageScreenImage later, on demand, behind it on the match
    //    camera (third person), or on the view-model camera, which renders after the match camera (first person). The damage
    //    screen therefore always reads the bloomed frame (and clamps it to 0..1). NativeUiBlur's AfterEverything copy comes
    //    after both. IMGUI (the HUD, menus, crosshair, numbers) draws after every camera has rendered, so it never blooms.
    //  * Path: OnRenderImage -> Graphics.Blit only (the orientation-safe single-source path the damage screen uses; no
    //    command buffer, no CameraTarget copy): prefilter to half size, 5 downs, 5 ups, composite. The two passes that read
    //    a second texture flip its v when the blit's source is stored top-down (the shader's BloomUv).
    //  * Cost: ARGBHalf temporaries from RenderTexture.GetTemporary (half to 1/64 size; the down and up chains together hold
    //    about two thirds of the frame's pixel count), every one released in the same frame (finally); steady frames
    //    allocate nothing else.
    //  * Quality: Quality(lowGraphics, reducedEffects) from ConversionGame.ApplySettings: disabled on the low graphics tier
    //    (Unity then skips it entirely), intensity x.6 with Reduced effects. No saved preference of its own. Without the
    //    shader (or ARGBHalf targets) it disables itself and the frame passes through untouched.
    [DisallowMultipleComponent]
    public sealed class NativeBloom:MonoBehaviour
    {
        // The shader's passes, in order.
        public const int PrefilterPass=0,DownPass=1,UpPass=2,CompositePass=3;
        private static readonly int idCurve=Shader.PropertyToID("_BloomCurve"),idParams=Shader.PropertyToID("_BloomParams"),idBloom=Shader.PropertyToID("_BloomTex");
        private readonly RenderTexture[] down=new RenderTexture[NativeBloomRules.Levels],up=new RenderTexture[NativeBloomRules.Levels];
        private Material material;private bool missing;
        public bool LowGraphics{get;private set;}
        public bool ReducedEffects{get;private set;}
        // The composite intensity in use (0 while the low graphics tier has it off).
        public float Strength=>NativeBloomRules.Strength(LowGraphics,ReducedEffects);
        // The camera's bloom: the one already on it, or a new one appended after the components it has now.
        public static NativeBloom Attach(Camera camera)
        {
            if(camera==null)return null;var bloom=camera.GetComponent<NativeBloom>();
            return bloom!=null?bloom:camera.gameObject.AddComponent<NativeBloom>();
        }
        public void Quality(bool lowGraphics,bool reducedEffects){LowGraphics=lowGraphics;ReducedEffects=reducedEffects;enabled=!lowGraphics&&!missing;}
        private bool Ready()
        {
            if(material!=null)return true;if(missing)return false;
            var shader=Resources.Load<Shader>("NativeBloom");
            if(shader==null||!shader.isSupported||!SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)){missing=true;enabled=false;Debug.LogWarning("NativeBloom: shader or half-float targets unavailable; bloom off");return false;}
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};return true;
        }
        private static RenderTexture Temporary(int width,int height)
        {
            var t=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear);
            t.filterMode=FilterMode.Bilinear;t.wrapMode=TextureWrapMode.Clamp;return t;
        }
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            float strength=Strength;int levels=source==null?0:NativeBloomRules.LevelCount(source.width,source.height);
            if(strength<=0||levels==0||!Ready()){Graphics.Blit(source,destination);return;}
            try
            {
                material.SetVector(idCurve,new Vector4(NativeBloomRules.Threshold,NativeBloomRules.Knee,NativeBloomRules.FireflyClamp,0));
                material.SetVector(idParams,new Vector4(strength,0,0,0));
                for(int i=0;i<levels;i++)
                {
                    down[i]=Temporary(NativeBloomRules.LevelSize(source.width,i),NativeBloomRules.LevelSize(source.height,i));
                    Graphics.Blit(i==0?source:down[i-1],down[i],material,i==0?PrefilterPass:DownPass);
                }
                var low=down[levels-1];
                for(int i=levels-2;i>=0;i--){up[i]=Temporary(down[i].width,down[i].height);material.SetTexture(idBloom,low);Graphics.Blit(down[i],up[i],material,UpPass);low=up[i];}
                material.SetTexture(idBloom,low);Graphics.Blit(source,destination,material,CompositePass);
            }
            finally{material.SetTexture(idBloom,null);Release();}
        }
        // Every temporary back to Unity's pool (each frame, and when the component goes away).
        private void Release()
        {
            for(int i=0;i<down.Length;i++)
            {
                if(down[i]!=null){RenderTexture.ReleaseTemporary(down[i]);down[i]=null;}
                if(up[i]!=null){RenderTexture.ReleaseTemporary(up[i]);up[i]=null;}
            }
        }
        private void OnDisable()=>Release();
        private void OnDestroy(){Release();if(material!=null){Destroy(material);material=null;}}
    }
}
