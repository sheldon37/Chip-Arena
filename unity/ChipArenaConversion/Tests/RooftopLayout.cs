using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

// The home screen's rooftop as a layout for its modelled art (docs/MENU-ROOFTOP-ART.md): everything Core NativeRooftopCity builds
// that the Blender script tools/stages/rooftop_art.py must stand its models on or keep clear of, so the modelled scene lands on
// today's composition (the pilot's seat, the actors' roofs, the screens, the lights' and sprites' places, the sight lines).
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --rooftop-layout <out.json>`.
// Units are metres in the SOURCE frame Core builds in (+y up; the Unity boundary mirrors x: Unity x = -source x). The script turns
// it into Unity's frame as it reads.
internal static class RooftopLayout
{
    // `--rooftop-slides <dir>`: every holo-screen slide rasterised as the Runtime does (NativeRooftopAds.Compose), raw RGBA bytes
    // (top row first) as <dir>/<screen>_<nn>_<id>.rgba plus the fixed ads, for a look outside the engine.
    public static void Slides(string root,string dir)
    {
        var o=new JsonSerializerOptions{IncludeFields=true};Directory.CreateDirectory(dir);
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        using var icons=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ClassIcons.json")));float box=icons.RootElement.GetProperty("box").GetSingle();
        var art=icons.RootElement.GetProperty("icons").EnumerateArray().ToDictionary(e=>e.GetProperty("id").GetString(),e=>e.GetProperty("markup").GetString());
        var specs=icons.RootElement.GetProperty("specs").EnumerateArray().ToDictionary(e=>e.GetProperty("id").GetString(),e=>e.GetProperty("icon").GetString());
        void Save(string name,float[] rgba){int px=(int)Math.Round(Math.Sqrt(rgba.Length/4));var bytes=new byte[px*px*4];for(int y=0;y<px;y++)for(int x=0;x<px;x++)for(int c=0;c<4;c++)bytes[((px-1-y)*px+x)*4+c]=(byte)Math.Round(Math.Clamp(rgba[(y*px+x)*4+c],0,1)*255);File.WriteAllBytes(Path.Combine(dir,name+".rgba"),bytes);}
        int n=0;
        foreach(var (screen,set) in new[]{("classes",NativeRooftopAds.Classes(content.weapons)),("stages",NativeRooftopAds.Stages),("brand",NativeRooftopAds.Brand)})
        {
            var uv=NativeRooftopArt.AdUv(screen);float y0=uv.v0*100,y1=(uv.v0+uv.dv)*100;
            for(int k=0;k<set.Length;k++){var s=set[k];string icon=s.Kind==RooftopSlide.Class&&specs.TryGetValue(s.Id,out var id)&&art.TryGetValue(id,out var m)?m:null;Save(screen+"_"+k.ToString("00")+"_"+s.Id+"_"+s.Accent.Substring(1),NativeRooftopAds.Compose(s,icon,box,y0,y1));n++;}
        }
        foreach(var kind in new[]{"ricochet","banner","sign"})Save("fixed_"+kind,NativeIconRaster.Raster(NativeIconRaster.Parse(NativeRooftopArt.Ad(kind),1,NativeIconRaster.Mode.Markup),NativeRooftopArt.AdPixels,100));
        Console.WriteLine("rooftop slides: "+n+" slides and 3 fixed ads -> "+dir);
    }
    public static void Write(string path)
    {
        var world=NativeRooftopCity.Build(1,true);
        using var fs=File.Create(path);using var w=new Utf8JsonWriter(fs,new JsonWriterOptions{Indented=false});
        void N(double v)=>w.WriteNumberValue(Math.Round(v,4));
        void V(string name,Vector3 v){w.WriteStartArray(name);N(v.X);N(v.Y);N(v.Z);w.WriteEndArray();}
        void A(Vector3 v){w.WriteStartArray();N(v.X);N(v.Y);N(v.Z);w.WriteEndArray();}
        w.WriteStartObject();w.WriteString("frame","source");
        w.WriteNumber("street",NativeRooftopCity.Street);w.WriteNumber("fov",NativeRooftopCity.FovDegrees);w.WriteNumber("loop",NativeRooftopCity.Loop);
        w.WriteNumber("nearDetail",NativeRooftopCity.NearDetail);w.WriteNumber("midDetail",NativeRooftopCity.MidDetail);
        // the camera through its loop (and its halved drift is inside it)
        w.WriteStartArray("camera");
        for(float t=0;t<NativeRooftopCity.Loop;t+=2){NativeRooftopCity.Camera(t,out var eye,out var fwd);w.WriteStartObject();w.WriteNumber("t",t);V("eye",eye);V("fwd",fwd);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartObject("perch");
        w.WriteNumber("x0",NativeRooftopCity.RoofX0);w.WriteNumber("x1",NativeRooftopCity.RoofX1);w.WriteNumber("z0",NativeRooftopCity.RoofZ0);w.WriteNumber("z1",NativeRooftopCity.RoofZ1);
        w.WriteNumber("lipTop",NativeRooftopCity.LipTop);w.WriteNumber("lipDepth",NativeRooftopCity.LipDepth);V("seat",NativeRooftopCity.PilotSeat);
        V("beacon",world.Beacon);V("signLight",world.SignLight);V("cyanLight",world.CyanLight);V("spark",world.SparkAt);
        w.WriteEndObject();
        w.WriteStartArray("screens");
        foreach(var s in world.Screens){w.WriteStartObject();w.WriteString("kind",s.Kind);V("center",s.Center);w.WriteNumber("width",s.Width);w.WriteNumber("height",s.Height);w.WriteNumber("yaw",s.Yaw);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartObject("sites");
        foreach(var s in world.Sites.Values.OrderBy(s=>s.Id,StringComparer.Ordinal)){w.WriteStartObject(s.Id);V("at",s.Position);w.WriteNumber("yaw",s.Yaw);w.WriteEndObject();}
        w.WriteEndObject();
        // what the loop's camera must keep in clear view (RooftopChecks' composition targets)
        w.WriteStartArray("targets");
        // `clear`: nothing Core builds stands nearer than 2.5 m short of it from the t=0 eye (RooftopChecks' rule); the art must
        // keep every clear target clear.
        NativeRooftopCity.Camera(0,out var eye0,out _);
        float Tri(Vector3 o,Vector3 d,Vector3 a,Vector3 b,Vector3 c){var e1=b-a;var e2=c-a;var p=Vector3.Cross(d,e2);float det=Vector3.Dot(e1,p);if(Math.Abs(det)<1e-9f)return float.PositiveInfinity;float inv=1/det;var t=o-a;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)return float.PositiveInfinity;var q=Vector3.Cross(t,e1);float v=Vector3.Dot(d,q)*inv;if(v<0||u+v>1)return float.PositiveInfinity;float dist=Vector3.Dot(e2,q)*inv;return dist>0?dist:float.PositiveInfinity;}
        void T(string what,Vector3 p)
        {
            var d=p-eye0;float dist=d.Length();d/=dist;float first=float.PositiveInfinity;foreach(var q in world.Quads)first=Math.Min(first,Math.Min(Tri(eye0,d,q.A,q.B,q.C),Tri(eye0,d,q.A,q.C,q.D)));
            w.WriteStartObject();w.WriteString("what",what);V("at",p);w.WriteBoolean("clear",first>=dist-2.5f);w.WriteEndObject();
        }
        foreach(var s in world.Screens)
        {
            if(s.Kind=="sign")continue;var n=new Vector3((float)Math.Sin(s.Yaw),0,(float)Math.Cos(s.Yaw));var t=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,n));
            T("screen "+s.Kind,s.Center);foreach(float sx in new[]{-.46f,.46f})foreach(float sy in new[]{-.46f,.46f})T("screen "+s.Kind+" corner",s.Center+t*(sx*s.Width)+Vector3.UnitY*(sy*s.Height));
        }
        foreach(var id in new[]{"blade","gunner","watch"})T(id,world.Sites[id].Position+new Vector3(0,1.2f,0));
        foreach(var look in NativeRooftopActors.Crowd.Where(c=>c.Script==NativeRooftopActors.Runner))
        {
            var site=world.Sites[look.Id];var fwd=new Vector3((float)Math.Sin(site.Yaw),0,(float)Math.Cos(site.Yaw));float total=look.L1+look.Gap+look.L2;
            for(float along=0;along<=total;along+=2){float drop=along>look.L1+look.Gap?look.Drop:along>look.L1?look.Drop*(along-look.L1)/look.Gap:0;T(look.Id+" running",site.Position+fwd*along+new Vector3(0,1-drop,0));}
        }
        T("the seated pilot",NativeRooftopCity.PilotSeat+new Vector3(0,.9f,0));
        w.WriteEndArray();
        // every piece the city builds, in order (kind, lot, numbers: see each Note call in Core NativeRooftopCity / Towers / Screens)
        w.WriteStartArray("notes");
        foreach(var n in world.Notes){w.WriteStartObject();w.WriteString("k",n.Kind);w.WriteNumber("lot",n.Lot);w.WriteStartArray("v");foreach(var v in n.V)N(v);w.WriteEndArray();w.WriteEndObject();}
        w.WriteEndArray();
        // the sprites that mark a built piece (lamps, beacons, sign glows, the steam's vents): the models stand under them
        w.WriteStartArray("glows");
        foreach(var g in world.Glows){w.WriteStartObject();V("at",g.Position);w.WriteNumber("size",g.Size);w.WriteNumber("kind",g.Kind);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("steam");foreach(var p in world.Steam.Select(s=>s.Position).Distinct())A(p);w.WriteEndArray();
        w.WriteStartArray("lanes");
        foreach(var l in world.Lanes){w.WriteStartObject();w.WriteNumber("count",l.Count);w.WriteBoolean("near",l.Near);w.WriteStartArray("points");foreach(var p in l.Points)A(p);w.WriteEndArray();w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("lights");foreach(var l in world.Lights){w.WriteStartObject();V("at",l.Position);w.WriteNumber("reach",l.Reach);V("color",l.Color);w.WriteEndObject();}w.WriteEndArray();
        w.WriteNumber("quads",world.Quads.Count);w.WriteNumber("perchQuads",world.Quads.Count(q=>q.Part==NativeRooftopCity.PerchPart));w.WriteNumber("cityQuads",world.Quads.Count(q=>q.Part==NativeRooftopCity.CityPart));
        w.WriteEndObject();w.Flush();
        Console.WriteLine("rooftop layout: "+world.Notes.Count+" notes, "+world.Notes.Count(n=>n.Kind=="lot"&&n.V[14]>0)+" lots modelled of "+world.LotCount+", "+world.Quads.Count(q=>q.Part==NativeRooftopCity.CityPart)+" city and "+world.Quads.Count(q=>q.Part==NativeRooftopCity.PerchPart)+" perch quads of "+world.Quads.Count+" -> "+path);
    }
}
