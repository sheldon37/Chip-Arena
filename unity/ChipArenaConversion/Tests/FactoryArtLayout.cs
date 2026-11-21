using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// The Factory's part of the stage art's layout export (Tests/StageArtLayout.cs, docs/arenas/STAGE-ART.md): its kit (Core
// NativeFactoryKit: every part with the block, flight or hazard it dresses, its kind, colour and window light) and the numbers its
// rig, its art poses and its models agree on (Core NativeFactoryRig, NativeFactoryArt), so tools/stages/factory_art.py models on
// today's pieces.
internal static class FactoryArtLayout
{
    public static void Write(Utf8JsonWriter w,ContentMap map,MapLook look,Action<string,double,double,double> V,Action<string,double,double,double,double> Q)
    {
        var looks=NativeFactoryKit.Resolve(look?.kitLooks);var kit=NativeFactoryKit.Build(map.blocks,map.hazards,looks,true,true);
        w.WriteStartArray("kinds");foreach(var k in NativeFactoryKit.Kinds)w.WriteStringValue(k.Substring("factory-kit-".Length));w.WriteEndArray();
        w.WriteStartArray("kindColors");for(int k=0;k<NativeFactoryKit.Kinds.Length;k++)w.WriteNumberValue(looks.Color[k]);w.WriteEndArray();
        w.WriteStartArray("parts");
        for(int i=0;i<kit.Count;i++)
        {
            ref var p=ref kit.Items[i];w.WriteStartObject();w.WriteString("o",kit.OwnerLog[i]);w.WriteNumber("k",kit.KindLog[i]);
            V("c",p.X,p.Y,p.Z);V("h",p.Hx,p.Hy,p.Hz);Q("q",p.Qx,p.Qy,p.Qz,p.Qw);w.WriteBoolean("s",p.Shadow);w.WriteNumber("col",p.Color);w.WriteNumber("w",Math.Round(p.Window,3));w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartObject("rig");
        void N(string n,double v)=>w.WriteNumber(n,Math.Round(v,5));
        N("railUp",NativeFactoryRig.RailUp);N("trolleyUp",NativeFactoryRig.TrolleyUp);N("ropeFoot",NativeFactoryRig.RopeFoot);N("bladeAside",NativeFactoryRig.BladeAside);
        N("rodX",NativeFactoryRig.RodX);N("rodZ",NativeFactoryRig.RodZ);N("rodInto",NativeFactoryRig.RodInto);N("slingX",NativeFactoryArt.SlingX);N("slingZ",NativeFactoryArt.SlingZ);
        w.WriteStartArray("chucks");
        foreach(var b in map.blocks.Where(NativeFactoryArt.LatheHead)){int d=NativeFactoryArt.ChuckSide(b);w.WriteStartArray();foreach(double v in new double[]{b.pos.x+d*b.size.x/2,b.pos.y,b.pos.z,d})w.WriteNumberValue(Math.Round(v,5));w.WriteEndArray();}
        w.WriteEndArray();w.WriteEndObject();
        w.WriteStartArray("spawns");foreach(var s in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points))){w.WriteStartArray();w.WriteNumberValue(s.pos.x);w.WriteNumberValue(s.pos.y);w.WriteNumberValue(s.pos.z);w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("lights");foreach(var l in look?.lights??Array.Empty<MapLookPractical>()){w.WriteStartArray();w.WriteNumberValue(l.pos.x);w.WriteNumberValue(l.pos.y);w.WriteNumberValue(l.pos.z);w.WriteEndArray();}w.WriteEndArray();
    }
}
