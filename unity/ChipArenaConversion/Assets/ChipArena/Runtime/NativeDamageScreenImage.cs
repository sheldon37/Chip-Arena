using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The damage screen's image effect on the last camera that draws to the screen (NativeDamageScreenPass adds it there and
    // enables it only while the effect is active). OnRenderImage hands Graphics.Blit the camera's finished image: Unity's
    // single-source image effect path, which samples the source the right way up on every graphics API, a D3D back-buffer
    // or anti-aliased resolve included (NativeDamageScreen.shader orients its own overlay by _MainTex_TexelSize). Without a
    // material the frame is copied through unchanged. While disabled Unity never calls it, so the camera renders as if it
    // were not there.
    public sealed class NativeDamageScreenImage:MonoBehaviour
    {
        public Material Material;
        private void OnRenderImage(RenderTexture source,RenderTexture destination){if(Material!=null)Graphics.Blit(source,destination,Material);else Graphics.Blit(source,destination);}
    }
}
