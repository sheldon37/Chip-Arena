using System;
using System.Text.Json;
using ChipArena.UnityConversion;

// Cyber City's part of the stage art's layout export (Tests/StageArtLayout.cs, docs/arenas/STAGE-ART.md): the dressing kit
// (Core NativeCyberCityKit: every part with the block it rides, its place in the world at rest, its look key, colour, gain and
// drive), the maglev train's parts in the train's frame, the Cut's exits and the backdrop's boxes (Core NativeCyberCityScenery),
// so tools/stages/cybercity_art.py models on today's pieces.
internal static class CyberCityArtLayout
{
    public static void Write(Utf8JsonWriter w,ContentMap map,Action<string,double,double,double> V)
    {
        var hazards=map.hazards??Array.Empty<MapHazard>();var exits=NativeCyberCityExits.Build(map.blocks,map.nav,hazards);var kit=NativeCyberCityKit.Build(map.blocks,hazards,exits);
        void Part(in NativeCyberCityPart p,double x,double y,double z)
        {
            V("c",x,y,z);V("h",p.Hx,p.Hy,p.Hz);w.WriteString("look",NativeCyberCityLooks.KeyOf(p.Look,p.Flags));w.WriteNumber("col",p.Color);w.WriteNumber("gain",Math.Round(p.Gain,3));
            w.WriteNumber("drive",p.Drive);w.WriteNumber("param",Math.Round(p.Param,4));w.WriteBoolean("s",p.Shadow);
        }
        w.WriteStartArray("parts");
        foreach(var p in kit.Parts){var b=map.blocks[p.Parent];w.WriteStartObject();w.WriteString("o",b.id);Part(p,b.pos.x+p.X,b.pos.y+p.Y,b.pos.z+p.Z);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("train");foreach(var p in kit.Train){w.WriteStartObject();Part(p,p.X,p.Y,p.Z);w.WriteEndObject();}w.WriteEndArray();
        w.WriteStartObject("city");w.WriteNumber("railTop",NativeCyberCityKit.RailTop);w.WriteNumber("track",kit.Maglev>=0?hazards[kit.Maglev].center.z:0);w.WriteNumber("trainLength",NativeCyberCityArt.Length);
        w.WriteStartArray("cars");foreach(var c in NativeCyberCityArt.Cars)w.WriteNumberValue(c);w.WriteEndArray();w.WriteNumber("floorY",exits.FloorY);w.WriteNumber("streetY",exits.StreetY);w.WriteEndObject();
        w.WriteStartArray("exits");
        foreach(var e in exits.Exits){w.WriteStartObject();w.WriteNumber("side",e.Side);V("door",e.DoorX,exits.FloorY,e.DoorZ);w.WriteNumber("x0",e.OpenX0);w.WriteNumber("x1",e.OpenX1);w.WriteNumber("top",e.OpenTop);V("up",e.TopX,e.TopY,e.TopZ);V("run",e.RunX,0,e.RunZ);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("backdrop");
        foreach(var b in NativeCyberCityScenery.Build()){w.WriteStartObject();V("c",b.X,b.Y,b.Z);V("h",b.Hx,b.Hy,b.Hz);w.WriteNumber("yaw",b.Yaw);w.WriteString("look",NativeCyberCityLooks.KeyOf(b.Look,b.Flags));w.WriteNumber("col",b.Color);w.WriteNumber("gain",Math.Round(b.Gain,3));w.WriteNumber("kind",b.Kind);w.WriteBoolean("near",NativeCyberCityArt.Near(b));w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("lanterns");foreach(var g in kit.Glows){w.WriteStartArray();foreach(double v in new double[]{g.X,g.Y,g.Z,g.Size})w.WriteNumberValue(Math.Round(v,4));w.WriteEndArray();}w.WriteEndArray();
    }
}
