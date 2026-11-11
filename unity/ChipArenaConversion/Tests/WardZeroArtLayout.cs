using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// Ward Zero's part of the stage art's layout export (Tests/StageArtLayout.cs, docs/arenas/STAGE-ART.md): the kit (Core
// NativeWardZeroKit: every part with its family, the block it dresses, its look, colour, gain and drive, whether it stays a lit
// box under the art, and its mover), the movers (the rig props: slot, rest place, travel, their parts), the blocks the art poses
// (Core NativeWardZeroArt.Posed), the PvE entries, the routes and the lights, so tools/stages/wardzero_art.py models on today's
// pieces. Both map records ("siren", "expedition-ward-zero") export alike; the Expedition's adds its exit frame's blocks.
internal static class WardZeroArtLayout
{
    public static NativeWardEntry[] Entries(string root)
    {
        var o=new JsonSerializerOptions{IncludeFields=true};
        return JsonSerializer.Deserialize<NativeWardEntrySet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),o)?.entries??Array.Empty<NativeWardEntry>();
    }
    public static void Write(Utf8JsonWriter w,string root,ContentMap map,Action<string,double,double,double> V,Action<string,double,double,double,double> Q)
    {
        var entries=Entries(root);var blocks=map.blocks;var kit=NativeWardZeroKit.Build(blocks,map.hazards,entries);var art=NativeWardZeroArt.Build(kit,blocks,entries);
        void N(string n,double v)=>w.WriteNumber(n,Math.Round(v,5));
        w.WriteStartObject("ward");N("upper",NativeWardZeroKit.Upper);N("deckUnder",NativeWardZeroKit.DeckUnder);N("roof",NativeWardZeroKit.Roof);w.WriteNumber("moverSlots",kit.MoverSlots);w.WriteEndObject();
        w.WriteStartArray("parts");
        for(int i=0;i<kit.Parts.Length;i++)
        {
            var p=kit.Parts[i];var b=blocks[p.Parent];var q=b.rotation==null?FactoryQ.Identity:new FactoryQ{X=b.rotation.x,Y=b.rotation.y,Z=b.rotation.z,W=b.rotation.w};
            q.Rotate(p.X,p.Y,p.Z,out double x,out double y,out double z);
            w.WriteStartObject();w.WriteString("f",kit.Family[i]);w.WriteString("o",b.id);w.WriteString("look",NativeWardZeroLooks.KeyOf(p.Look,p.Flags)??"");
            V("c",b.pos.x+x,b.pos.y+y,b.pos.z+z);V("h",p.Hx,p.Hy,p.Hz);Q("q",q.X,q.Y,q.Z,q.W);w.WriteNumber("col",p.Color);N("gain",p.Gain);w.WriteNumber("drive",p.Drive);
            w.WriteNumber("hz",p.Hazard);w.WriteNumber("e",p.Entry);if(p.Mx!=0||p.My!=0||p.Mz!=0)V("m",p.Mx,p.My,p.Mz);w.WriteBoolean("kept",kit.Keeps[i]);w.WriteNumber("mover",kit.MoverOf[i]);
            w.WriteBoolean("detail",p.Detail);N("param",p.Param);w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("movers");
        for(int g=0;g<kit.Movers.Length;g++)
        {
            var m=kit.Movers[g];var b=blocks[m.Parent];w.WriteStartObject();w.WriteString("slot","m"+m.Slot);w.WriteString("o",b.id);w.WriteString("f",kit.Family[art.MoverPart[g]]);
            V("at",b.pos.x+m.X,b.pos.y+m.Y,b.pos.z+m.Z);V("m",m.Mx,m.My,m.Mz);w.WriteNumber("e",m.Entry);w.WriteNumber("hz",m.Hazard);w.WriteNumber("drive",m.Drive);
            w.WriteStartArray("parts");for(int i=0;i<kit.Parts.Length;i++)if(kit.MoverOf[i]==g)w.WriteNumberValue(i);w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("posed");foreach(int bi in art.Blocks)w.WriteStringValue(blocks[bi].id);w.WriteEndArray();
        w.WriteStartArray("entries");
        foreach(var e in entries)
        {
            if(e==null)continue;w.WriteStartObject();w.WriteNumber("e",e.e);w.WriteString("id",e.id);w.WriteString("kind",e.kind);w.WriteString("surface",e.Surface);V("from",e.From.x,e.From.y,e.From.z);V("exit",e.Exit.x,e.Exit.y,e.Exit.z);
            N("height",e.height);w.WriteBoolean("hulk",e.hulk);w.WriteNumber("song",e.song);w.WriteBoolean("pit",e.pit);
            if(e.kind=="breach"){w.WriteString("block",e.block);V("center",e.center.x,e.center.y,e.center.z);V("size",e.size.x,e.size.y,e.size.z);V("normal",e.normal.x,e.normal.y,e.normal.z);}
            w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("routes");
        foreach(var r in kit.Routes){w.WriteStartObject();w.WriteString("id",r.Id);w.WriteBoolean("stair",r.Stair);V("foot",r.FootX,r.FootY,r.FootZ);V("top",r.TopX,r.TopY,r.TopZ);N("ux",r.Ux);N("uz",r.Uz);N("width",r.Width);w.WriteNumber("steps",r.Steps);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("glows");
        foreach(var g in kit.Glows){w.WriteStartObject();V("c",g.X,g.Y,g.Z);w.WriteNumber("kind",g.Kind);N("size",g.Size);N("floor",g.Floor);w.WriteNumber("hz",g.Hazard);w.WriteNumber("e",g.Entry);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("spawns");
        foreach(var s in map.spawns.ffa){w.WriteStartArray();w.WriteNumberValue(s.pos.x);w.WriteNumberValue(s.pos.y);w.WriteNumberValue(s.pos.z);w.WriteEndArray();}
        w.WriteEndArray();
    }
}
