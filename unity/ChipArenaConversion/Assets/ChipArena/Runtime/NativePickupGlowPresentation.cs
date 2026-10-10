using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The PvE chip pickups' glow sprites and light beams (client/pve/pickups.ts; rules in Core/NativePickupFx.cs, look in
    /// Resources/NativePickupGlow.shader). The glow texture is the source canvas made once: both variants side by side in
    /// one 512 x 256 texture (left: the plain glow, right: the owner's, with "YOURS"), the label rasterised from the
    /// HOLO_FONT glyphs (the SPLAT! pop's OS font) and its blurred shadow composited in Core. Every glow quad and beam
    /// cylinder of a frame is one dynamic world-space mesh refilled from reused lists: one draw, no per-frame allocation.
    /// </summary>
    public sealed class NativePickupGlowPresentation : IDisposable
    {
        private readonly Mesh mesh=new Mesh{name="Native pickup glows"};private readonly Material material;private Texture2D texture;
        private readonly List<Vector3> vertices=new List<Vector3>();private readonly List<Color> colors=new List<Color>();private readonly List<Vector4> uv=new List<Vector4>();private readonly List<int> indices=new List<int>();
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        private NativePickupGlowPresentation(Shader shader){material=new Material(shader){name="Native pickup glows",renderQueue=(int)RenderQueue.Transparent};mesh.MarkDynamic();}
        /// <summary>Null when the shader is missing or unsupported (the glows and beams then simply do not draw).</summary>
        public static NativePickupGlowPresentation Create(){var shader=Resources.Load<Shader>("NativePickupGlow");return shader!=null&&shader.isSupported?new NativePickupGlowPresentation(shader):null;}
        public bool HasLabel{get;private set;}
        private void Vertex(Vector3 p,Color c,Vector4 t){vertices.Add(p);colors.Add(c);uv.Add(t);}
        /// <summary>
        /// A glow sprite: a camera-facing square of GlowSize centred at <paramref name="centre"/> (right and up: the camera's
        /// axes). <paramref name="color"/>: the tier colour as the screen shows it, alpha the opacity.
        /// </summary>
        public void Glow(Vector3 centre,Vector3 right,Vector3 up,Color color,bool mine)
        {
            float h=NativePickupFx.GlowSize*.5f,u0=mine?.5f:0,u1=u0+.5f;int a=vertices.Count;
            Vertex(centre-right*h-up*h,color,new Vector4(u0,0,1,0));Vertex(centre+right*h-up*h,color,new Vector4(u1,0,1,0));
            Vertex(centre+right*h+up*h,color,new Vector4(u1,1,1,0));Vertex(centre-right*h+up*h,color,new Vector4(u0,1,1,0));
            indices.Add(a);indices.Add(a+1);indices.Add(a+2);indices.Add(a);indices.Add(a+2);indices.Add(a+3);
        }
        /// <summary>The light beam standing on <paramref name="floor"/>: the source cylinder, its x mirrored into Unity axes.</summary>
        public void Beam(Vector3 floor,Color color)
        {
            int a=vertices.Count,n=NativePickupFx.BeamSides+1;
            for(int row=0;row<2;row++)for(int column=0;column<n;column++){NativePickupFx.BeamVertex(column,row,out float x,out float y,out float z);Vertex(floor+new Vector3(-x,y,z),color,Vector4.zero);}
            for(int column=0;column<NativePickupFx.BeamSides;column++)
            {int va=a+column,vb=a+n+column,vc=a+n+column+1,vd=a+column+1;indices.Add(va);indices.Add(vb);indices.Add(vd);indices.Add(vb);indices.Add(vc);indices.Add(vd);}
        }
        /// <summary>Draws this frame's glows and beams (the glow texture is made on first use, with <paramref name="font"/>).</summary>
        public void Draw(Font font)
        {
            mesh.Clear();
            if(vertices.Count>0)
            {
                EnsureTexture(font);
                mesh.SetVertices(vertices);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetTriangles(indices,0,false);mesh.bounds=Everywhere;
                Graphics.DrawMesh(mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
            }
            vertices.Clear();colors.Clear();uv.Clear();indices.Clear();
        }
        private void EnsureTexture(Font font)
        {
            if(texture!=null)return;const int n=NativePickupFx.Canvas;
            var other=new float[n*n];NativePickupFx.Glow(other,null);
            var label=LabelCoverage(font);HasLabel=label!=null;var mine=new float[n*n];NativePickupFx.Glow(mine,label);
            var rgba=new byte[n*2*n*4];NativePickupFx.Write(other,rgba,n*2,0);NativePickupFx.Write(mine,rgba,n*2,n);
            // CanvasTexture: sRGB, mipmapped, trilinear, clamped.
            texture=new Texture2D(n*2,n,TextureFormat.RGBA32,true,false){name="Pickup glow (plain | YOURS)",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,hideFlags=HideFlags.HideAndDontSave};
            texture.SetPixelData(rgba,0);texture.Apply(true,true);material.mainTexture=texture;
        }
        /// <summary>
        /// fillText("YOURS", 128, 226) in 700 34 px HOLO_FONT, centred, middle baseline: the glyphs' coverage on the 256 px
        /// canvas, from the font atlas (requested at twice the size). Null without a font or a readable atlas.
        /// </summary>
        private static float[] LabelCoverage(Font font)
        {
            if(font==null)return null;const int request=NativePickupFx.LabelPx*2;string text=NativePickupFx.Label;
            try
            {
                font.RequestCharactersInTexture(text,request,FontStyle.Bold);
                var atlas=font.material!=null?font.material.mainTexture as Texture2D:null;if(atlas==null)return null;
                var alpha=ReadAlpha(atlas);if(alpha==null)return null;
                float k=NativePickupFx.LabelPx/(float)request,width=0;
                for(int i=0;i<text.Length;i++)if(font.GetCharacterInfo(text[i],out var ci,request,FontStyle.Bold))width+=ci.advance*k;
                float pen=NativePickupFx.LabelX-width*.5f,baseline=NativePickupFx.LabelY+NativePickupFx.MiddleBaseline*NativePickupFx.LabelPx;
                var coverage=new float[NativePickupFx.Canvas*NativePickupFx.Canvas];
                for(int i=0;i<text.Length;i++)
                {
                    if(!font.GetCharacterInfo(text[i],out var ci,request,FontStyle.Bold))continue;
                    var g=new NativePickupFx.Glyph{X0=pen+ci.minX*k,X1=pen+ci.maxX*k,Top=baseline-ci.maxY*k,Bottom=baseline-ci.minY*k,
                        TopLeftU=ci.uvTopLeft.x,TopLeftV=ci.uvTopLeft.y,TopRightU=ci.uvTopRight.x,TopRightV=ci.uvTopRight.y,BottomLeftU=ci.uvBottomLeft.x,BottomLeftV=ci.uvBottomLeft.y,BottomRightU=ci.uvBottomRight.x,BottomRightV=ci.uvBottomRight.y};
                    NativePickupFx.Stamp(coverage,alpha,atlas.width,atlas.height,g);pen+=ci.advance*k;
                }
                return coverage;
            }
            catch(Exception e){Debug.LogWarning("Pickup glow: no YOURS label ("+e.Message+")");return null;}
        }
        // The atlas's alpha, bottom row first: directly when readable, else copied through a temporary render texture.
        private static byte[] ReadAlpha(Texture2D atlas)
        {
            int w=atlas.width,h=atlas.height;Color32[] pixels;
            if(atlas.isReadable)pixels=atlas.GetPixels32();
            else
            {
                var target=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);var previous=RenderTexture.active;Texture2D copy=null;
                try{Graphics.Blit(atlas,target);RenderTexture.active=target;copy=new Texture2D(w,h,TextureFormat.RGBA32,false,true);copy.ReadPixels(new Rect(0,0,w,h),0,0,false);copy.Apply(false);pixels=copy.GetPixels32();}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);if(copy!=null)CapturedAnimation.Release(copy);}
            }
            if(pixels==null||pixels.Length!=w*h)return null;var alpha=new byte[w*h];for(int i=0;i<alpha.Length;i++)alpha[i]=pixels[i].a;return alpha;
        }
        public void Dispose(){CapturedAnimation.Release(mesh);CapturedAnimation.Release(material);if(texture!=null)CapturedAnimation.Release(texture);texture=null;}
    }

    public sealed partial class ArenaPresentation
    {
        private NativePickupGlowPresentation pickupGlow;private bool pickupGlowFailed;
        private NativePickupGlowPresentation PickupGlow(){if(pickupGlow==null&&!pickupGlowFailed){pickupGlow=NativePickupGlowPresentation.Create();pickupGlowFailed=pickupGlow==null;}return pickupGlow;}
    }
}
