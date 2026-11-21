using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// Brickfall's part of the stage art's layout export (Tests/StageArtLayout.cs, docs/arenas/BRICKFALL-ART.md): Brickfall has no Core
// kit, its art is its blocks, so the layout adds what the blocks' own list lacks: each block's look (the map look's blockLooks as
// the dressing resolves them: look key, colour, band) and the segment it answers to, and every segment with its mode, its blocks,
// its members and its rubble (the blocks that exist once it has landed), so tools/stages/brickfall_art.py models every piece the
// simulation moves in that piece's own frame and each landed segment's heap on its rubble.
internal static class BrickfallArtLayout
{
    public static void Write(Utf8JsonWriter w,ContentMap map,MapLook look,Action<string,double,double,double> V)
    {
        void Look(ContentBlock b)
        {
            var c=NativeBlockLooks.Resolve(look?.blockLooks,null,b.id,b.material,0xffffff);
            w.WriteString("look",NativeBlockLooks.Keys[c.Look]);w.WriteNumber("color",c.Color);w.WriteBoolean("band",(c.Flags&NativeBlockLooks.Band)!=0);
        }
        w.WriteStartArray("looks");
        foreach(var b in map.blocks){w.WriteStartObject();w.WriteString("id",b.id);Look(b);var s=NativeBrickfallArt.Segment(map,b);if(s!=null)w.WriteString("segment",s);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("segments");
        foreach(var s in map.segments??Array.Empty<StructureDefinition>())
        {
            w.WriteStartObject();w.WriteString("id",s.id);w.WriteString("mode",s.mode);w.WriteString("heap",NativeBrickfallArt.Heap(s));V("direction",s.direction.x,s.direction.y,s.direction.z);
            w.WriteStartArray("members");foreach(var p in s.supports??Array.Empty<StructureSupport>())if(!string.IsNullOrEmpty(p.member))w.WriteStringValue(p.member);w.WriteEndArray();
            w.WriteStartArray("blocks");foreach(var b in map.blocks)if(b.segment==s.id)w.WriteStringValue(b.id);w.WriteEndArray();
            w.WriteStartArray("rubble");
            foreach(var b in s.rubble??Array.Empty<ContentBlock>()){w.WriteStartObject();w.WriteString("id",b.id);Look(b);V("pos",b.pos.x,b.pos.y,b.pos.z);V("size",b.size.x,b.size.y,b.size.z);w.WriteEndObject();}
            w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("lights");
        foreach(var l in look?.lights??Array.Empty<MapLookPractical>()){w.WriteStartObject();w.WriteString("id",l.id);V("pos",l.pos.x,l.pos.y,l.pos.z);w.WriteNumber("color",l.color);w.WriteNumber("range",l.range);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("spawns");
        void P(MapSpawn p){w.WriteStartArray();w.WriteNumberValue(p.pos.x);w.WriteNumberValue(p.pos.y);w.WriteNumberValue(p.pos.z);w.WriteEndArray();}
        foreach(var p in map.spawns?.ffa??Array.Empty<MapSpawn>())P(p);
        foreach(var t in map.spawns?.teams??Array.Empty<MapTeamSpawns>())foreach(var p in t.points)P(p);
        w.WriteEndArray();
        w.WriteStartArray("pilotBlocks");
        foreach(var p in map.pilotBlocks??Array.Empty<MapPilotBlock>()){w.WriteStartObject();w.WriteString("id",p.id);V("center",p.center.x,p.center.y,p.center.z);V("size",p.size.x,p.size.y,p.size.z);w.WriteNumber("bottom",p.bottom);w.WriteEndObject();}
        w.WriteEndArray();
    }
}
