using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    // The kit's shared backdrop blur (NativeUi.Backdrop, CSS backdrop-filter: blur). While a panel asked for it within the
    // last Hold frames, a command buffer on the last camera that draws to the screen copies the finished 3D frame after that
    // camera renders (before OnGUI, so no overlay is in it): halved twice (bilinear, a 4x4 box), blurred at quarter size into
    // the small copy (CSS ~5 px: the death card's 4 px) and, from that, at eighth size into the large one (the .panel's 16 px),
    // each with two separable Gaussian passes (NativeUiBlur.shader). One copy per frame at most and none while no panel asks:
    // the buffer and textures are released after Hold frames without a request, rebuilt when the screen or UI scale changes,
    // and dropped with the material by Release(true) (ConversionGame.OnDestroy). Without the shader nothing is captured and
    // the panels keep their plain translucent fill. Steady frames allocate nothing.
    public static class NativeUiBlur
    {
        private const int Hold=30;
        private static CommandBuffer buffer;private static Camera attached;private static Material material;private static bool missing;private static int axis;
        private static RenderTexture half,small,smallPass,large,largePass;private static int width,height;private static float scale;
        private static Camera[] cameras=new Camera[8];
        // Once per frame, before the cameras render (ConversionGame.LateUpdate).
        public static void Frame()
        {
            if(Time.frameCount-NativeUi.BlurRequested>Hold){Release(false);return;}
            var camera=LastScreenCamera();if(camera==null){Detach();return;}
            if(material==null)
            {
                if(missing)return;var shader=Resources.Load<Shader>("NativeUiBlur");if(shader==null||!shader.isSupported){missing=true;return;}
                material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};axis=Shader.PropertyToID("_NativeUiBlur");
            }
            int w=Mathf.Max(16,Screen.width),h=Mathf.Max(16,Screen.height);float s=Mathf.Max(.1f,NativeUi.S);
            if(buffer==null||w!=width||h!=height||Mathf.Abs(s-scale)>.01f){Detach();Build(w,h,s);}
            if(attached!=camera){Detach();camera.AddCommandBuffer(CameraEvent.AfterEverything,buffer);attached=camera;}
            NativeUi.BlurSmall=small;NativeUi.BlurLarge=large;
        }
        // The camera Unity renders last into the back buffer: the highest depth among the enabled screen cameras.
        private static Camera LastScreenCamera()
        {
            if(cameras.Length<Camera.allCamerasCount)cameras=new Camera[Mathf.NextPowerOfTwo(Camera.allCamerasCount)];
            int n=Camera.GetAllCameras(cameras);Camera best=null;
            for(int i=0;i<n;i++){var c=cameras[i];cameras[i]=null;if(c==null||c.targetTexture!=null||!c.isActiveAndEnabled)continue;if(best==null||c.depth>=best.depth)best=c;}
            return best;
        }
        private static RenderTexture Target(int w,int h)
        {
            var t=new RenderTexture(Mathf.Max(1,w),Mathf.Max(1,h),0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,useMipMap=false,hideFlags=HideFlags.HideAndDontSave};
            t.Create();var active=RenderTexture.active;RenderTexture.active=t;GL.Clear(false,true,Color.clear);RenderTexture.active=active;return t;
        }
        private static void Build(int w,int h,float s)
        {
            if(w!=width||h!=height||small==null){ReleaseTargets();half=Target(w/2,h/2);small=Target(w/4,h/4);smallPass=Target(w/4,h/4);large=Target(w/8,h/8);largePass=Target(w/8,h/8);}
            width=w;height=h;scale=s;if(buffer==null)buffer=new CommandBuffer{name="NativeUiBlur"};else buffer.Clear();
            // Standard deviations in texels: small CSS 5 px at quarter size; large CSS 16 px at eighth size, less what the small
            // pass already spread (they add in quadrature). The 13-tap kernel holds up to 3 texels.
            float sigmaSmall=Mathf.Clamp(5*s/4,.5f,3),sigmaLarge=Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0,(2*s)*(2*s)-(5*s/8)*(5*s/8))),.5f,3);
            buffer.Blit(BuiltinRenderTextureType.CameraTarget,half);buffer.Blit(half,small);
            Pass(small,smallPass,1,0,sigmaSmall);Pass(smallPass,small,0,1,sigmaSmall);
            buffer.Blit(small,large);Pass(large,largePass,1,0,sigmaLarge);Pass(largePass,large,0,1,sigmaLarge);
        }
        private static void Pass(RenderTexture from,RenderTexture to,float x,float y,float sigma){buffer.SetGlobalVector(axis,new Vector4(x,y,sigma,0));buffer.Blit(from,to,material);}
        private static void Detach(){if(attached!=null&&buffer!=null)attached.RemoveCommandBuffer(CameraEvent.AfterEverything,buffer);attached=null;}
        private static void Drop(Object o){if(o==null)return;if(Application.isPlaying)Object.Destroy(o);else Object.DestroyImmediate(o);}
        private static void Drop(ref RenderTexture t){if(t==null)return;t.Release();Drop(t);t=null;}
        private static void ReleaseTargets()
        {
            NativeUi.BlurSmall=NativeUi.BlurLarge=null;
            Drop(ref half);Drop(ref small);Drop(ref smallPass);Drop(ref large);Drop(ref largePass);width=height=0;
        }
        // Drops the copy (no panel asked for Hold frames); `all` also drops the material (the game is going away).
        public static void Release(bool all)
        {
            if(buffer!=null){Detach();buffer.Release();buffer=null;}
            if(small!=null||half!=null)ReleaseTargets();
            if(all){Drop(material);material=null;missing=false;attached=null;}
        }
    }
}
