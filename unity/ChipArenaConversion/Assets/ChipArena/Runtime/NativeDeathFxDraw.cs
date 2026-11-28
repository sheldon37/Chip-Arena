using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace ChipArena.UnityConversion
{
    /// <summary>
    /// Render primitives of the death presentation (Resources/NativeDeathFx.shader): the sky broadcast's one-pixel lines
    /// (three.js Line / LineSegments: MeshTopology.Lines), its size-attenuated point-sprite stars (three.js Points, expanded in
    /// clip space exactly as gl_PointSize), the rocket sphere and the canvas text (nameplate, SPLAT!) as font-atlas glyph quads.
    /// Every layer is one dynamic world-space
    /// mesh refilled from reused lists each frame: no per-frame allocation, one draw per layer. Unlit, no fog, not tone-mapped,
    /// no depth write, as the source materials. Vertex colours are in the project's colour space.
    /// </summary>
    public sealed class NativeDeathFxDraw : IDisposable
    {
        public sealed class Layer
        {
            public readonly Mesh Mesh=new Mesh{indexFormat=IndexFormat.UInt32};public readonly MeshTopology Topology;
            private readonly List<Vector3> points=new List<Vector3>();private readonly List<Color> colors=new List<Color>();private readonly List<Vector4> uv=new List<Vector4>();private readonly List<Vector2> corners=new List<Vector2>();private readonly List<int> indices=new List<int>();
            public Layer(string name,MeshTopology topology){Mesh.name=name;Mesh.MarkDynamic();Topology=topology;}
            public int Count=>points.Count;
            public int Vertex(Vector3 p,Color c,Vector4 t,Vector2 corner){points.Add(p);colors.Add(c);uv.Add(t);corners.Add(corner);return points.Count-1;}
            public void Index(int i)=>indices.Add(i);
            public void Clear(){points.Clear();colors.Clear();uv.Clear();corners.Clear();indices.Clear();}
            public void Draw(Material material)
            {
                Mesh.Clear();if(points.Count==0||material==null)return;
                Mesh.SetVertices(points);Mesh.SetColors(colors);Mesh.SetUVs(0,uv);Mesh.SetUVs(1,corners);Mesh.SetIndices(indices,Topology,0,false);Mesh.bounds=Everywhere;
                Graphics.DrawMesh(Mesh,Matrix4x4.identity,material,0,null,0,null,ShadowCastingMode.Off,false);
            }
        }
        private static readonly Bounds Everywhere=new Bounds(Vector3.zero,Vector3.one*4000);
        /// <summary>Lines (renderOrder 3-5), the broadcast's triangles (rocket, stars, nameplate glyphs; renderOrder 3-6) and the
        /// SPLAT! pop (depthTest false, renderOrder 6).</summary>
        public readonly Layer Lines=new Layer("Death fx lines",MeshTopology.Lines),Quads=new Layer("Death fx quads",MeshTopology.Triangles),Pop=new Layer("Death fx pop",MeshTopology.Triangles);
        private readonly Material lines,quads,pop;public readonly bool Linear;
        private Font text,popFont;private bool fontsTried;
        private static readonly Vector4 Flat=Vector4.zero;
        private NativeDeathFxDraw(Shader shader)
        {
            Linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Material Make(string name,int queue,BlendMode dst,CompareFunction test,bool scan)
            {var m=new Material(shader){name=name,renderQueue=queue};m.SetOverrideTag("RenderType","Transparent");m.SetFloat("_Src",(float)BlendMode.SrcAlpha);m.SetFloat("_Dst",(float)dst);m.SetFloat("_ZTest",(float)test);m.SetFloat("_Scan",scan?1:0);return m;}
            lines=Make("Death fx lines",(int)RenderQueue.Transparent+4,BlendMode.OneMinusSrcAlpha,CompareFunction.LessEqual,false);
            quads=Make("Death fx quads",(int)RenderQueue.Transparent+6,BlendMode.OneMinusSrcAlpha,CompareFunction.LessEqual,false);
            pop=Make("Death fx pop",(int)RenderQueue.Transparent+60,BlendMode.OneMinusSrcAlpha,CompareFunction.Always,false);
        }
        /// <summary>Null when the shader is missing or unsupported (the effects then simply do not draw).</summary>
        public static NativeDeathFxDraw Create(){var shader=Resources.Load<Shader>("NativeDeathFx");return shader!=null&&shader.isSupported?new NativeDeathFxDraw(shader):null;}
        public void Clear(){Lines.Clear();Quads.Clear();Pop.Clear();}
        public void Draw()
        {
            EnsureFonts();
            if(text!=null)quads.mainTexture=text.material.mainTexture;if(popFont!=null)pop.mainTexture=popFont.material.mainTexture;
            Lines.Draw(lines);Quads.Draw(quads);Pop.Draw(pop);Clear();
        }
        public void Dispose(){foreach(var l in new[]{Lines,Quads,Pop})CapturedAnimation.Release(l.Mesh);foreach(var m in new[]{lines,quads,pop})CapturedAnimation.Release(m);}
        /// <summary>A source colour (0xRRGGBB, sRGB) with an opacity, in the project's colour space.</summary>
        public Color Hex(int hex,float alpha)
        {
            var c=new Color(((hex>>16)&255)/255f,((hex>>8)&255)/255f,(hex&255)/255f,alpha);
            return Linear?new Color(Mathf.GammaToLinearSpace(c.r),Mathf.GammaToLinearSpace(c.g),Mathf.GammaToLinearSpace(c.b),alpha):c;
        }
        /// <summary>A linear colour (three.js Color channels) scaled by <paramref name="k"/>, in the project's colour space.</summary>
        public Color LinearColor(float r,float g,float b,float k,float alpha)=>Linear?new Color(r*k,g*k,b*k,alpha):new Color(Mathf.LinearToGammaSpace(r*k),Mathf.LinearToGammaSpace(g*k),Mathf.LinearToGammaSpace(b*k),alpha);

        public void Line(Vector3 a,Vector3 b,Color c){Lines.Index(Lines.Vertex(a,c,Flat,Vector2.zero));Lines.Index(Lines.Vertex(b,c,Flat,Vector2.zero));}
        /// <summary>A three.js point (square, size-attenuated: size * (height / 2) / depth pixels).</summary>
        public void Point(Vector3 p,float size,Color c)
        {
            float h=size*.5f;int a=Quads.Vertex(p,c,Flat,new Vector2(-h,-h));Quads.Vertex(p,c,Flat,new Vector2(h,-h));Quads.Vertex(p,c,Flat,new Vector2(h,h));Quads.Vertex(p,c,Flat,new Vector2(-h,h));
            Quads.Index(a);Quads.Index(a+1);Quads.Index(a+2);Quads.Index(a);Quads.Index(a+2);Quads.Index(a+3);
        }
        private static readonly List<Vector3> spherePoints=new List<Vector3>(),sphereNormals=new List<Vector3>();private static readonly List<int> sphereIndices=new List<int>();
        /// <summary>A flat-coloured three.js SphereGeometry(1, 8, 5) through <paramref name="m"/>.</summary>
        public void Sphere(Matrix4x4 m,Color c)
        {
            if(spherePoints.Count==0)NativeSourceModels.Sphere(NativeBroadcastFx.RocketWidth,NativeBroadcastFx.RocketHeight,spherePoints,sphereNormals,sphereIndices);
            int start=Quads.Count;foreach(var p in spherePoints)Quads.Vertex(m.MultiplyPoint3x4(p),c,Flat,Vector2.zero);foreach(int i in sphereIndices)Quads.Index(start+i);
        }
        // ---- canvas text as font-atlas glyphs --------------------------------------------------------------------------
        /// <summary>The nameplate's system-ui and the SPLAT! pop's HOLO_FONT ("Rajdhani", "Bahnschrift", "Arial Narrow"), from
        /// the OS like NativeUi's mono font, falling back to the built-in font.</summary>
        private void EnsureFonts()
        {
            if(fontsTried)return;fontsTried=true;
            Font Os(string[] names){try{return Font.CreateDynamicFontFromOSFont(names,32);}catch(Exception){return null;}}
            Font Builtin(){try{return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");}catch(Exception){return null;}}
            text=Os(new[]{"Segoe UI","Arial"})??Builtin();popFont=Os(new[]{"Rajdhani","Bahnschrift","Arial Narrow","Arial"})??Builtin();
        }
        public Font TextFont{get{EnsureFonts();return text;}}
        public Font PopFont{get{EnsureFonts();return popFont;}}
        /// <summary>The width in canvas pixels of <paramref name="value"/> at <paramref name="pixels"/> (glyphs requested at
        /// <paramref name="request"/> and scaled), or 0 without a font.</summary>
        public static float Measure(Font font,string value,int request,FontStyle style,float pixels)
        {
            if(font==null||string.IsNullOrEmpty(value))return 0;font.RequestCharactersInTexture(value,request,style);float w=0;
            for(int i=0;i<value.Length;i++)if(font.GetCharacterInfo(value[i],out var ci,request,style))w+=ci.advance;
            return w*pixels/request;
        }
        /// <summary>
        /// canvas fillText(value, x, y, maxWidth) with textAlign center and textBaseline middle: glyph quads in a plane where one
        /// canvas pixel is <paramref name="right"/> across and <paramref name="down"/> down from <paramref name="origin"/> (the
        /// canvas's top-left corner), squeezed to maxWidth; with <paramref name="angle"/> the canvas is translated to (x, y) and
        /// rotated (ctx.rotate) and the text drawn at (<paramref name="dx"/>, <paramref name="dy"/>). Returns false without a font.
        /// </summary>
        public bool Text(Layer layer,Font font,string value,int request,FontStyle style,float pixels,float x,float y,float maxWidth,Vector3 origin,Vector3 right,Vector3 down,Color color,float dx=0,float dy=0,float angle=0)
        {
            if(font==null||string.IsNullOrEmpty(value))return false;font.RequestCharactersInTexture(value,request,style);
            float k=pixels/request,width=0;for(int i=0;i<value.Length;i++)if(font.GetCharacterInfo(value[i],out var ci,request,style))width+=ci.advance*k;
            float squeeze=maxWidth>0&&width>maxWidth?maxWidth/width:1,pen=-width*squeeze*.5f,baseline=.35f*pixels;float cos=Mathf.Cos(angle),sin=Mathf.Sin(angle);
            Vector3 At(float px,float py){px+=dx;py+=dy;float rx=px*cos-py*sin,ry=px*sin+py*cos;return origin+right*(x+rx)+down*(y+ry);}
            for(int i=0;i<value.Length;i++)
            {
                if(!font.GetCharacterInfo(value[i],out var ci,request,style))continue;
                float x0=pen+ci.minX*k*squeeze,x1=pen+ci.maxX*k*squeeze,top=baseline-ci.maxY*k,bottom=baseline-ci.minY*k;pen+=ci.advance*k*squeeze;
                if(x1<=x0||bottom<=top)continue;int a=layer.Count;
                layer.Vertex(At(x0,bottom),color,new Vector4(ci.uvBottomLeft.x,ci.uvBottomLeft.y,1,0),Vector2.zero);layer.Vertex(At(x1,bottom),color,new Vector4(ci.uvBottomRight.x,ci.uvBottomRight.y,1,0),Vector2.zero);
                layer.Vertex(At(x1,top),color,new Vector4(ci.uvTopRight.x,ci.uvTopRight.y,1,0),Vector2.zero);layer.Vertex(At(x0,top),color,new Vector4(ci.uvTopLeft.x,ci.uvTopLeft.y,1,0),Vector2.zero);
                layer.Index(a);layer.Index(a+1);layer.Index(a+2);layer.Index(a);layer.Index(a+2);layer.Index(a+3);
            }
            return true;
        }
    }
}
