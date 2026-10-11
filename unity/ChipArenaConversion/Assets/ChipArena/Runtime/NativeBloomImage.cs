using UnityEngine;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Bloom on the match camera (effects overhaul, owner 2026-10-11): the HDR effect glows (NativeVfxDraw's sprites, streaks,
    /// fireballs and shock shells above 1) spill into a soft halo instead of clipping flat. An OnRenderImage effect drawn only
    /// through Graphics.Blit, which orients its single source itself (the damage screen's rule, NativeDamageScreenImage); the
    /// composite samples a second texture and flips its uv for a flipped source (Resources/NativeBloom.shader). ConversionGame
    /// adds it to the match camera when it creates the camera, before any other image effect, so it runs first and the damage
    /// screen (which clamps its scene tap to 0..1) composes on top of the bloomed image. The first-person view model is a later
    /// camera and is not bloomed. Low graphics starts the chain at quarter resolution with fewer levels. Below the threshold
    /// (1.0 linear on an HDR target) the image is unchanged; an LDR target uses a lower threshold. Cosmetic only.
    /// </summary>
    public sealed class NativeBloomImage:MonoBehaviour
    {
        /// <summary>Low graphics (NativeSettings): quarter-resolution start, four levels.</summary>
        public bool Low;
        /// <summary>Total bloom added at the composite, spread over the levels.</summary>
        public float Intensity=.7f;
        public float Threshold=1,Knee=.5f;
        private const int MaxLevels=6;
        private readonly RenderTexture[] levels=new RenderTexture[MaxLevels];
        private Material material;private bool missing;
        private static readonly int CurveId=Shader.PropertyToID("_BloomCurve"),BloomId=Shader.PropertyToID("_BloomTex");
        private static bool Hdr(RenderTextureFormat f)=>f==RenderTextureFormat.ARGBHalf||f==RenderTextureFormat.ARGBFloat||f==RenderTextureFormat.RGB111110Float||f==RenderTextureFormat.DefaultHDR||f==RenderTextureFormat.RGFloat;
        private bool Ready()
        {
            if(material!=null)return true;if(missing)return false;
            var shader=Resources.Load<Shader>("NativeBloom");if(shader==null||!shader.isSupported){missing=true;return false;}
            material=new Material(shader){name="Native bloom",hideFlags=HideFlags.HideAndDontSave};return true;
        }
        private void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            if(Intensity<=0||!Ready()){Graphics.Blit(source,destination);return;}
            bool hdr=Hdr(source.format);var format=hdr?source.format:RenderTextureFormat.Default;
            int divide=Low?4:2,w=Mathf.Max(1,source.width/divide),h=Mathf.Max(1,source.height/divide),count=0,limit=Low?4:MaxLevels;
            float threshold=hdr?Threshold:Mathf.Min(Threshold,.8f),knee=Mathf.Max(1e-3f,Knee*threshold);
            for(;count<limit&&Mathf.Min(w,h)>=8;count++){levels[count]=RenderTexture.GetTemporary(w,h,0,format,RenderTextureReadWrite.Linear);levels[count].filterMode=FilterMode.Bilinear;w/=2;h/=2;}
            if(count==0){Graphics.Blit(source,destination);return;}
            material.SetVector(CurveId,new Vector4(threshold,knee,.25f/knee,Intensity/count));
            Graphics.Blit(source,levels[0],material,0);
            for(int i=1;i<count;i++)Graphics.Blit(levels[i-1],levels[i],material,1);
            // Each level adds onto the one above it (pass 2 blends One One), so levels[0] ends as the sum of them all.
            for(int i=count-2;i>=0;i--)Graphics.Blit(levels[i+1],levels[i],material,2);
            material.SetTexture(BloomId,levels[0]);Graphics.Blit(source,destination,material,3);
            for(int i=0;i<count;i++){RenderTexture.ReleaseTemporary(levels[i]);levels[i]=null;}
        }
        private void OnDestroy(){if(material!=null)CapturedAnimation.Release(material);material=null;}
    }
}
