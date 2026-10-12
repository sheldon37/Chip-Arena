using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;

// The game's own class and weapon icons (Assets/Conversion/Resources/ClassIcons.json, tools/export_class_icons.mjs),
// rendered through the real engine-free rasteriser (Core/NativeIconRaster.cs) that NativeIcons draws with: every class
// and weapon has its own icon, each drawing (full and small) renders with sensible coverage at 16 and 64 px, no two
// weapons look alike, the fallback still resolves and renders, the art features stay out of the Lucide and markup modes,
// and the Runtime call sites use the art (source checks). Pixels on screen are checked by the offline renders, not here.
internal static class ClassIconChecks
{
    private static int count;
    private static void Check(bool ok,string what){if(!ok)throw new Exception("Class icons: "+what);count++;}
    private sealed class Art {public string id,markup,small;}
    private static float[] Cover(string markup,int px)=>NativeIconRaster.Coverage(NativeIconRaster.Parse(markup,1,NativeIconRaster.Mode.Art),px,24);
    private static float Iou(float[] a,float[] b){double i=0,u=0;for(int k=0;k<a.Length;k++){i+=Math.Min(a[k],b[k]);u+=Math.Max(a[k],b[k]);}return u>0?(float)(i/u):1;}
    public static void Run(string root,ConversionContent data)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json")));var set=doc.RootElement;
        using var lucideDoc=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/Icons.json")));var lucide=lucideDoc.RootElement;
        var art=set.GetProperty("icons").EnumerateArray().Select(e=>new Art{id=e.GetProperty("id").GetString(),markup=e.GetProperty("markup").GetString(),small=e.TryGetProperty("small",out var s)?s.GetString():null}).ToDictionary(a=>a.id);
        Dictionary<string,string> Map(JsonElement e,string group)=>e.GetProperty(group).EnumerateArray().ToDictionary(p=>p.GetProperty("id").GetString(),p=>p.GetProperty("icon").GetString());
        var classes=Map(set,"classes");var specs=Map(set,"specs");var lucideClasses=Map(lucide,"classes");var lucideSpecs=Map(lucide,"specs");
        var lucideMarkup=lucide.GetProperty("icons").EnumerateArray().ToDictionary(e=>e.GetProperty("id").GetString(),e=>e.GetProperty("markup").GetString());
        string fallback=set.GetProperty("fallback").GetString();int smallPx=set.GetProperty("smallPx").GetInt32();
        Check(set.GetProperty("box").GetDouble()==24&&smallPx==NativeIconRaster.SmallPx,"24-unit view box; the small threshold matches NativeIconRaster.SmallPx");

        // 1. Every class and every weapon resolves to its own art icon, through the lookup NativeIcons uses.
        foreach(var c in NativeLoadoutText.Classes){string icon=NativeIconRaster.Resolve(classes,lucideClasses,fallback,c);Check(art.ContainsKey(icon)&&classes.ContainsKey(c),"class "+c+" has an art icon");}
        foreach(var w in data.weapons){string icon=NativeIconRaster.Resolve(specs,lucideSpecs,fallback,w.id);Check(art.ContainsKey(icon)&&specs.ContainsKey(w.id),"weapon "+w.id+" has an art icon");}
        // The Bow's and the DEV Cyber Lord's art ship ahead of their content entries (docs/NEW-KITS.md sections 2 and 3; the
        // rules are Astra's): until GameContent lists one it is a mapping without a weapon, afterwards none.
        var ahead=new[]{NativeBowWire.Spec,CyberLordSpec.Id}.Where(id=>!data.weapons.Any(w=>w.id==id)).ToArray();Check(specs.ContainsKey(CyberLordSpec.Id)&&specs.ContainsKey(NativeBowWire.Spec),"the Bow and the DEV Cyber Lord have art icons");
        Check(specs.Keys.OrderBy(k=>k).SequenceEqual(data.weapons.Select(w=>w.id).Concat(ahead).OrderBy(k=>k),StringComparer.Ordinal)&&classes.Count==NativeLoadoutText.Classes.Length,"no mapping for an unknown class or weapon");
        Check(specs.Values.Distinct().Count()==specs.Count,"no two weapons share an icon");
        Check(classes.Values.Distinct().Count()==classes.Count&&!classes.Values.Intersect(specs.Values).Any(),"classes have their own icons, not weapon ones");
        Check(art.Keys.All(id=>!lucideMarkup.ContainsKey(id))&&art.Values.All(a=>!lucideMarkup.ContainsValue(a.markup)),"art ids and drawings are not Lucide's");
        Console.WriteLine("PASS Class icons: "+classes.Count+" classes and "+specs.Count+" weapons resolve to distinct art icons");

        // 2. The markup stays in the art subset NativeIconRaster supports: white paths, even-odd fills, opacity, knockouts.
        var allowed=new HashSet<string>{"d","fill","fill-rule","stroke","stroke-width","stroke-linejoin","stroke-linecap","opacity","data-knockout"};
        foreach(var a in art.Values)foreach(var (variant,m) in new[]{("full",a.markup),("small",a.small)})
        {
            if(m==null)continue;var tags=Regex.Matches(m,@"<(\w+)").Select(x=>x.Groups[1].Value).ToList();
            Check(tags.Count>0&&tags.All(t=>t=="path")&&Regex.Matches(m,@"<path\b[^>]*/>").Count==tags.Count,a.id+" "+variant+": self-closed paths only");
            Check(Regex.Matches(m,@"([\w:-]+)=""").Select(x=>x.Groups[1].Value).All(allowed.Contains),a.id+" "+variant+": supported attributes only");
            Check(Regex.Matches(m,@"(?:fill|stroke)=""([^""]*)""").Select(x=>x.Groups[1].Value).All(v=>v=="#fff"||v=="none"),a.id+" "+variant+": one tintable ink");
            Check(Regex.Matches(m,@"stroke-line(?:join|cap)=""([^""]*)""").All(x=>x.Groups[1].Value=="round"),a.id+" "+variant+": strokes round, as the rasteriser draws them");
            Check(Regex.Matches(m,@"<path\b[^>]*/>").Any(x=>!x.Value.Contains("data-knockout")),a.id+" "+variant+": draws something besides cuts");
            Check(Regex.Matches(m,@" d=""([^""]*)""").All(x=>Regex.IsMatch(x.Groups[1].Value,@"^[MLQCAZ0-9.\- ]+$")),a.id+" "+variant+": path data the generator emits");
        }
        Console.WriteLine("PASS Class icons: "+art.Count+" icons ("+art.Values.Count(a=>a.small!=null)+" with small drawings) use the supported art subset");

        // 3. Each drawing renders at 16 and 64 px with real, bounded coverage; the small drawing is the one picked at small sizes.
        foreach(var a in art.Values)foreach(var (variant,m) in new[]{("full",a.markup),("small",a.small)})
        {
            if(m==null)continue;
            foreach(int px in new[]{16,64})
            {
                var cov=Cover(m,px);float sum=cov.Sum()/cov.Length;int solid=cov.Count(v=>v>.5f);bool finite=cov.All(v=>v>=0&&v<=1&&!float.IsNaN(v));
                Check(finite&&sum>.08f&&sum<.75f&&solid>=px*px/20,a.id+" "+variant+" @"+px+": coverage "+sum.ToString("0.000"));
                int x0=px,x1=-1,y0=px,y1=-1;for(int y=0;y<px;y++)for(int x=0;x<px;x++)if(cov[y*px+x]>.3f){x0=Math.Min(x0,x);x1=Math.Max(x1,x);y0=Math.Min(y0,y);y1=Math.Max(y1,y);}
                Check(x1-x0+1>=px*.55f&&y1-y0+1>=px*.4f,a.id+" "+variant+" @"+px+": fills the icon box ("+(x1-x0+1)+"x"+(y1-y0+1)+")");
            }
        }
        foreach(var a in art.Values)
        {
            Check(NativeIconRaster.UseSmall(a.small,16,smallPx)==(a.small!=null)&&!NativeIconRaster.UseSmall(a.small,smallPx+1,smallPx)&&!NativeIconRaster.UseSmall(a.small,64,smallPx),a.id+": small drawing picked only at "+smallPx+" px and below");
            if(a.small!=null){float same=Iou(Cover(a.markup,32),Cover(a.small,32));Check(same>.3f,a.id+": the small drawing is a simplification of the full one (IoU "+same.ToString("0.00")+")");}
        }
        Console.WriteLine("PASS Class icons: every drawing renders at 16 and 64 px with bounded coverage; small drawings below "+(smallPx+1)+" px");

        // 4. No two weapons (or classes) look alike: rendered at 32 px their overlap stays well short of identical (the old
        // Lucide pistol and ranged class were the same glyph, IoU 1).
        var ids=art.Keys.OrderBy(k=>k).ToList();var at32=ids.ToDictionary(k=>k,k=>Cover(art[k].markup,32));float worst=0;string pair="";
        for(int i=0;i<ids.Count;i++)for(int j=i+1;j<ids.Count;j++){float v=Iou(at32[ids[i]],at32[ids[j]]);if(v>worst){worst=v;pair=ids[i]+"/"+ids[j];}}
        Check(worst<.65f,"every pair of icons is visually distinct (closest "+pair+" IoU "+worst.ToString("0.00")+")");
        Console.WriteLine("PASS Class icons: all "+ids.Count+" icons pairwise distinct at 32 px (closest "+pair+", IoU "+worst.ToString("0.00")+")");

        // 5. The fallback: an unknown class or weapon gets the art fallback (the DEV chip, as cpu was), which renders; without
        // the art set the source's Lucide picks and cpu still resolve.
        Check(fallback==classes["dev"]&&art.ContainsKey(fallback),"the fallback is the DEV chip");
        Check(NativeIconRaster.Resolve(classes,lucideClasses,fallback,"no-such-class")==fallback&&NativeIconRaster.Resolve(specs,lucideSpecs,fallback,null)==fallback,"unknown and null ids fall back to the art fallback");
        Check(Cover(art[fallback].markup,16).Sum()>16,"the fallback renders");
        Check(NativeIconRaster.Resolve(null,lucideClasses,null,"melee")=="swords"&&NativeIconRaster.Resolve(null,lucideSpecs,null,"no-such")=="cpu"&&lucideMarkup.ContainsKey("cpu"),"without ClassIcons.json the Lucide map and cpu still resolve");
        Check(NativeIconRaster.Coverage(lucideMarkup["cpu"],16).Sum()>4,"Lucide cpu still renders");
        Console.WriteLine("PASS Class icons: unknown ids fall back to the DEV chip; without the art the Lucide picks and cpu resolve");

        // 6. Art features are opt-in: Lucide and DrawMarkup parsing ignore knockouts, opacity and even-odd, so their output is
        // unchanged (the bit-for-bit port against the old Runtime rasteriser was verified when it moved to Core).
        string probe="<path d=\"M4 4L20 4L20 20L4 20Z\" fill=\"#fff\" opacity=\"0.3\" fill-rule=\"evenodd\" data-knockout=\"1\"/>";
        foreach(var mode in new[]{NativeIconRaster.Mode.Lucide,NativeIconRaster.Mode.Markup})
        {var s=NativeIconRaster.Parse(probe,1.7f,mode)[0];Check(!s.Erase&&!s.EvenOdd&&!s.Centered&&s.FillAlpha==1&&s.StrokeAlpha==1,mode+" mode ignores the art features");}
        var artShape=NativeIconRaster.Parse(probe,1,NativeIconRaster.Mode.Art)[0];Check(artShape.Erase&&artShape.EvenOdd&&artShape.Centered&&Math.Abs(artShape.FillAlpha-.3f)<1e-6,"art mode reads them");
        var cut=Cover("<path d=\"M2 2L22 2L22 22L2 22Z\" fill=\"#fff\"/><path d=\"M8 8L16 8L16 16L8 16Z\" fill=\"#fff\" data-knockout=\"1\"/>",24);
        var ring=Cover("<path d=\"M2 2L22 2L22 22L2 22ZM8 8L16 8L16 16L8 16Z\" fill-rule=\"evenodd\" fill=\"#fff\"/>",24);
        var tone=Cover("<path d=\"M2 2L22 2L22 22L2 22Z\" fill=\"#fff\" opacity=\"0.42\"/>",24);
        Check(cut[12*24+12]==0&&cut[4*24+4]==1&&ring[12*24+12]==0&&ring[4*24+4]==1&&Math.Abs(tone[12*24+12]-.42f)<1e-4,"knockouts erase, even-odd leaves a hole, opacity gives the second tone");
        Console.WriteLine("PASS Class icons: knockout, even-odd and opacity work in art mode only");

        // 7. The Runtime draws them: NativeIcons loads the art and resolves classes and specs through it, picks the small
        // drawing by pixel size, and the HUD's health box shows the weapon's icon beside its name (as the source's did).
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));string icons=R("NativeIcons.cs"),hud=R("NativeFightHud.cs");
        Check(icons.Contains("Resources.Load<TextAsset>(\"ClassIcons\")")&&icons.Contains("NativeIconRaster.Resolve(artClasses,classes,artFallback,id)")&&icons.Contains("NativeIconRaster.Resolve(artSpecs,specs,artFallback,id)"),"NativeIcons resolves classes and specs to the art");
        Check(icons.Contains("NativeIconRaster.UseSmall(e.small,px,artSmallPx)")&&icons.Contains("NativeIconRaster.Mode.Art")&&icons.Contains("NativeIconRaster.Mode.Lucide")&&icons.Contains("NativeIconRaster.Mode.Markup"),"NativeIcons draws art, Lucide and markup through their own modes");
        // (The cyber HUD restyle tints the icon from the shared palette and fits the upper-case name beside the HP readout.)
        Check(hud.Contains("NativeIcons.Draw(new Rect(ix,y+5,15,15),NativeIcons.Spec(p.Weapon.id),NativeCyberTheme.CyanSoft())")&&hud.Contains("new Rect(ix+22,y+3,room,18),Fit(p.Weapon.name"),"the HUD health box shows the weapon icon (15 px, 7 px gap) before its name");
        Console.WriteLine("PASS Class icons: "+count+" checks (NativeIcons loads and resolves the art; the HUD health box draws the weapon icon)");
    }
}
