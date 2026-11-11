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
        void T(string what,Vector3 p){w.WriteStartObject();w.WriteString("what",what);V("at",p);w.WriteEndObject();}
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
