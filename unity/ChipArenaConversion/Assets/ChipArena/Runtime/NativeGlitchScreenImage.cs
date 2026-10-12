using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The Sheldon Intro's glitch image effect on the last screen camera (NativeGlitchScreenPass adds it there and enables it
    // only while the glitch shows). OnRenderImage hands Graphics.Blit the camera's finished image (after the bloom, which was
    // added with the camera), as NativeDamageScreenImage does. Without a material the frame is copied through unchanged.
    public sealed class NativeGlitchScreenImage:MonoBehaviour
    {
        public Material Material;
        private void OnRenderImage(RenderTexture source,RenderTexture destination){if(Material!=null)Graphics.Blit(source,destination,Material);else Graphics.Blit(source,destination);}
    }
}
