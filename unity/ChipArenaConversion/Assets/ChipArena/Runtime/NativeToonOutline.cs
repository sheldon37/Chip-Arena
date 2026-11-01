using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The anime map style's outline (docs/MAP-STYLE-ANIME.md "Outlines"; Resources/NativeToonEdge.shader, Core NativeMapStyle):
    /// one full-screen pass on the match camera, from a command buffer at CameraEvent.BeforeForwardAlpha, so it lines the opaque
    /// scene before the transparent effects, the bloom and the grade, and never the first-person view model (its own camera).
    ///  - When: a map whose look turns the style on with a line strength (NativeLighting.StyleActive and Style.Outline), on the
    ///    normal graphics tiers. The low tier (graphics=performance, or auto on a low-end GPU) and every other map, stage, draft
    ///    and the rooftop take the buffer off the camera, so nothing runs.
    ///  - Cost: one full-screen pass of nine depth taps per pixel, no geometry and no extra draw of the scene: it reads the
    ///    camera's depth texture, which the normal tiers already render (the shadowed key light and the soft particles ask for
    ///    it); this asks for it too while it runs and gives back only a request it made. The inverted-hull alternative would
    ///    draw every block batch twice (the city's 26 draws become 52, its 2.7 M vertices 5.4 M) and split at the blocks'
    ///    hard edges, so it is not used for the environment.
    ///  - Characters: the stencil bit the environment's shaders set and the characters' shaders clear (NativeToonEdge.shader).
    ///  - Width: one pixel per 1080 lines of the screen (rounded), and the pixel's footprint per metre of depth (the creases'
    ///    fade, NativeMapStyle.Foot), set only when the screen height or the field of view changes (aiming zooms it).
    /// Allocation free after the one-time setup (the buffer, material and triangle are made once and kept).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NativeToonOutline:MonoBehaviour
    {
        private static readonly int WidthId=Shader.PropertyToID("_EdgeWidth"),FootId=Shader.PropertyToID("_EdgeFoot");
        private Camera view;private CommandBuffer buffer;private Material material;private Mesh triangle;private bool attached,missing,ownsDepth;private int height=-1;private float fov=-1;
        /// <summary>The low graphics tier (NativeSettings.ApplySettings): no outline.</summary>
        public bool LowGraphics{get;private set;}
        /// <summary>Whether the pass is on the camera now.</summary>
        public bool Attached=>attached;
        /// <summary>The camera's outline: the one already on it, or a new one.</summary>
        public static NativeToonOutline Attach(Camera camera)
        {
            if(camera==null)return null;var outline=camera.GetComponent<NativeToonOutline>();
            if(outline==null){outline=camera.gameObject.AddComponent<NativeToonOutline>();outline.view=camera;}
            return outline;
        }
        public void Quality(bool lowGraphics){LowGraphics=lowGraphics;}
        private bool Ready()
        {
            if(material!=null)return true;if(missing)return false;
            var shader=Resources.Load<Shader>("NativeToonEdge");
            if(shader==null||!shader.isSupported){missing=true;Debug.LogWarning("NativeToonOutline: shader unavailable; outlines off");return false;}
            material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
            // A triangle that covers the screen in clip space (the vertex stage passes it through).
            triangle=new Mesh{name="Toon outline triangle",hideFlags=HideFlags.HideAndDontSave};triangle.vertices=new[]{new Vector3(-1,-1,0),new Vector3(3,-1,0),new Vector3(-1,3,0)};triangle.triangles=new[]{0,1,2};
            triangle.bounds=new Bounds(Vector3.zero,Vector3.one*100000);triangle.UploadMeshData(true);
            buffer=new CommandBuffer{name="Anime map style outline"};buffer.DrawMesh(triangle,Matrix4x4.identity,material,0,0);return true;
        }
        private void LateUpdate()
        {
            if(view==null)view=GetComponent<Camera>();
            bool want=view!=null&&NativeLighting.StyleActive&&NativeLighting.Style.Outline&&!LowGraphics&&Ready();
            if(want!=attached){if(want)On();else Off();}
            if(want&&(Screen.height!=height||view.fieldOfView!=fov))
            {height=Screen.height;fov=view.fieldOfView;material.SetFloat(WidthId,Mathf.Max(1,Mathf.Round(height/1080f)));material.SetFloat(FootId,NativeMapStyle.Foot(fov,Mathf.Max(1,view.pixelHeight)));}
        }
        private void On()
        {
            view.AddCommandBuffer(CameraEvent.BeforeForwardAlpha,buffer);attached=true;
            if((view.depthTextureMode&DepthTextureMode.Depth)==0){view.depthTextureMode|=DepthTextureMode.Depth;ownsDepth=true;}
        }
        private void Off()
        {
            if(view!=null&&buffer!=null)view.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha,buffer);attached=false;
            if(ownsDepth&&view!=null){view.depthTextureMode&=~DepthTextureMode.Depth;ownsDepth=false;}
        }
        private void OnDisable(){if(attached)Off();}
        private void OnDestroy()
        {
            if(attached)Off();buffer?.Release();buffer=null;
            if(material!=null){Destroy(material);material=null;}if(triangle!=null){Destroy(triangle);triangle=null;}
        }
    }
}
