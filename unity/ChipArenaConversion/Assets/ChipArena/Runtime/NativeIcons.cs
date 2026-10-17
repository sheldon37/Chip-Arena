using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    // The source's line icons (client/icons.ts: Lucide 24x24 SVG, 1.7-unit round strokes) and the game's own class and
    // weapon icons (ClassIcons.json, original filled art that replaced the source's generic Lucide picks), rasterised at the
    // exact pixel size they are drawn, so they stay crisp at every UI scale. Parsing and rasterising live engine-free in
    // Core/NativeIconRaster.cs (the managed checks render every icon through it); this class keeps the lazily built,
    // cached textures, which are white and tinted when drawn.
    public static class NativeIcons
    {
        private static readonly Dictionary<string,List<NativeIconRaster.Shape>> parsed=new Dictionary<string,List<NativeIconRaster.Shape>>();
        // Rasterised textures by (id or markup key, markup, pixel size): a value key, so a cached draw builds no string.
        private static readonly Dictionary<(string,bool,int),Texture2D> textures=new Dictionary<(string,bool,int),Texture2D>();
        [Serializable] public sealed class IconEntry {public string id,markup,icon,small;}
        [Serializable] public sealed class IconSet {public string commit,license;public float strokeWidth;public IconEntry[] icons,chips,classes,specs,actions;}
        [Serializable] public sealed class ArtSet {public int schema,smallPx;public float box;public string fallback;public IconEntry[] icons,classes,specs;}
        private static Dictionary<string,string> markup,chips,classes,specs,actions,artClasses,artSpecs;private static Dictionary<string,IconEntry> art;
        private static float strokeWidth=1.7f,artBox=24;private static int artSmallPx=NativeIconRaster.SmallPx;private static string artFallback;
        // Lucide's ISC notice, shipped with the icon data (shown by the settings credits).
        public static string License{get;private set;}
        private static void Ensure()
        {
            if(markup!=null)return;var asset=Resources.Load<TextAsset>("Icons");if(asset==null)throw new InvalidOperationException("Icons.json is missing");var set=JsonUtility.FromJson<IconSet>(asset.text);strokeWidth=set.strokeWidth>0?set.strokeWidth:1.7f;License=set.license;
            Dictionary<string,string> Map(IconEntry[] e,bool paths){var d=new Dictionary<string,string>();foreach(var x in e??new IconEntry[0])d[x.id]=paths?x.markup:x.icon;return d;}
            markup=Map(set.icons,true);chips=Map(set.chips,false);classes=Map(set.classes,false);specs=Map(set.specs,false);actions=Map(set.actions,false);
            // The class and weapon art; without it (an older Resources folder) the source's Lucide picks still resolve.
            art=new Dictionary<string,IconEntry>();var artAsset=Resources.Load<TextAsset>("ClassIcons");var a=artAsset!=null?JsonUtility.FromJson<ArtSet>(artAsset.text):null;
            if(a!=null){foreach(var x in a.icons??new IconEntry[0])art[x.id]=x;artBox=a.box>0?a.box:24;artSmallPx=a.smallPx>0?a.smallPx:NativeIconRaster.SmallPx;artFallback=art.ContainsKey(a.fallback??"")?a.fallback:null;}
            artClasses=Map(a?.classes,false);artSpecs=Map(a?.specs,false);
        }
        public static bool Has(string id){Ensure();return markup.ContainsKey(id??"")||art.ContainsKey(id??"");}
        // icon-map.ts lookups; unmapped ids fall back to cpu as icons.ts does (actions have no fallback). Classes and specs
        // resolve to the game's own art first, and an unmapped one to its fallback (the DEV chip, as cpu was).
        public static string Chip(string id){Ensure();return chips.TryGetValue(id??"",out var i)?i:"cpu";}
        public static string Class(string id){Ensure();return NativeIconRaster.Resolve(artClasses,classes,artFallback,id);}
        public static string Spec(string id){Ensure();return NativeIconRaster.Resolve(artSpecs,specs,artFallback,id);}
        public static string Action(string id){Ensure();return actions.TryGetValue(id??"",out var i)?i:null;}
        // Draw icon `id` filling the virtual rect `r` (square), tinted `c`. Art icons switch to their simplified small
        // drawing at or below the set's small size (physical pixels).
        public static void Draw(Rect r,string id,Color c)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;Ensure();int px=Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(r.width,r.height)*NativeUi.S),8,512);var key=(id,false,px);
            if(art.TryGetValue(id??"",out var e))
            {
                if(!textures.TryGetValue(key,out var at)){if(textures.Count>256)Clear();bool small=NativeIconRaster.UseSmall(e.small,px,artSmallPx);at=Texture(NativeIconRaster.Raster(Shapes("art:"+id+(small?"#small":""),small?e.small:e.markup,NativeIconRaster.Mode.Art),px,artBox),px);textures[key]=at;}
                NativeUi.Image(r,at,c);return;
            }
            if(!markup.TryGetValue(id??"",out string svg)&&!markup.TryGetValue("cpu",out svg))return;
            if(!textures.TryGetValue(key,out var t)){if(textures.Count>256)Clear();t=Texture(NativeIconRaster.Raster(Shapes(id,svg,NativeIconRaster.Mode.Lucide),px,24),px);textures[key]=t;}
            NativeUi.Image(r,t,c);
        }
        // Draw coloured markup (its own fills and strokes; default fill black as in SVG) on a `box`-unit square viewBox.
        // `key` names the drawing for the cache; `c` tints it (white keeps its colours).
        // `maxPx` caps the raster size (soft art drawn large, such as the chip card's flame row, rasterises once at a lower size).
        public static void DrawMarkup(Rect r,string key,string svg,float box,Color c,int maxPx=512)
        {
            if(c.a<=0||Event.current.type!=EventType.Repaint)return;int px=Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(r.width,r.height)*NativeUi.S),8,Mathf.Clamp(maxPx,8,512));var k=(key,true,px);
            if(!textures.TryGetValue(k,out var t)){if(textures.Count>256)Clear();t=Texture(NativeIconRaster.Raster(Shapes("svg:"+key,svg,NativeIconRaster.Mode.Markup),px,box),px);textures[k]=t;}
            NativeUi.Image(r,t,c);
        }
        public static void Clear(){foreach(var t in textures.Values)if(t!=null)UnityEngine.Object.Destroy(t);textures.Clear();}
        private static List<NativeIconRaster.Shape> Shapes(string id,string svg,NativeIconRaster.Mode mode){if(!parsed.TryGetValue(id,out var list))parsed[id]=list=NativeIconRaster.Parse(svg,mode==NativeIconRaster.Mode.Lucide?strokeWidth:1,mode);return list;}
        private static Texture2D Texture(float[] rgba,int px)
        {
            var pixels=new Color[px*px];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(rgba[i*4],rgba[i*4+1],rgba[i*4+2],rgba[i*4+3]);
            var t=new Texture2D(px,px,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,hideFlags=HideFlags.HideAndDontSave};t.SetPixels(pixels);t.Apply(false,true);return t;
        }
        // Coverage (alpha) of a Lucide icon, bottom row first (texture order). Pure managed code so the checks can run it.
        public static float[] Coverage(string svg,int px,float width=1.7f)=>NativeIconRaster.Coverage(svg,px,width);
    }
}
