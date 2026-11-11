using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// The stage art's layout export (docs/arenas/STAGE-ART.md): a stage's map blocks and its Core kit's parts as JSON for the Blender
// stage scripts (tools/stages/), so the modelled art lands exactly on today's pieces and colliders.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout <map> <out.json>`.
// Units are the game's (metres, Unity axes: +y up); rotations Unity quaternions (x, y, z, w); colours sRGB hex.
internal static class StageArtLayout
{
    public static void Write(string root,string mapId,string path)
    {
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);
        var map=content.Map(mapId)??throw new Exception("no map "+mapId);
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,mapId);
        using var fs=File.Create(path);using var w=new Utf8JsonWriter(fs,new JsonWriterOptions{Indented=false});
        void V(string name,double x,double y,double z){w.WriteStartArray(name);w.WriteNumberValue(Math.Round(x,5));w.WriteNumberValue(Math.Round(y,5));w.WriteNumberValue(Math.Round(z,5));w.WriteEndArray();}
        void Q(string name,double x,double y,double z,double qw){w.WriteStartArray(name);w.WriteNumberValue(Math.Round(x,6));w.WriteNumberValue(Math.Round(y,6));w.WriteNumberValue(Math.Round(z,6));w.WriteNumberValue(Math.Round(qw,6));w.WriteEndArray();}
        w.WriteStartObject();w.WriteString("map",map.id);w.WriteString("look",map.look);
        w.WriteStartObject("bounds");w.WriteNumber("minX",map.bounds.minX);w.WriteNumber("maxX",map.bounds.maxX);w.WriteNumber("minZ",map.bounds.minZ);w.WriteNumber("maxZ",map.bounds.maxZ);w.WriteNumber("ceiling",map.bounds.ceiling);w.WriteNumber("killY",map.bounds.killY);w.WriteEndObject();
        w.WriteStartArray("blocks");
        foreach(var b in map.blocks)
        {
            w.WriteStartObject();w.WriteString("id",b.id);w.WriteString("role",b.role);w.WriteString("material",b.material);w.WriteNumber("hp",b.hp);
            V("pos",b.pos.x,b.pos.y,b.pos.z);V("size",b.size.x,b.size.y,b.size.z);Q("rot",b.rotation.x,b.rotation.y,b.rotation.z,b.rotation.w);w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("hazards");
        foreach(var h in map.hazards??Array.Empty<MapHazard>())
        {
            w.WriteStartObject();w.WriteString("id",h.id);w.WriteString("kind",h.kind);V("a",h.a.x,h.a.y,h.a.z);V("b",h.b.x,h.b.y,h.b.z);V("center",h.center.x,h.center.y,h.center.z);V("size",h.size.x,h.size.y,h.size.z);
            w.WriteNumber("carry",h.carry);w.WriteNumber("radius",h.radius);w.WriteStartArray("zones");foreach(var z in h.zones??Array.Empty<MapVolume>()){w.WriteStartObject();V("center",z.center.x,z.center.y,z.center.z);V("size",z.size.x,z.size.y,z.size.z);w.WriteEndObject();}w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();
        if(NativeSanctumLooks.Active(map.id))Sanctum(w,map,look,V,Q);
        if(NativeClubLooks.Active(map.id))ClubArtLayout.Write(w,map,look,V,Q);
        if(NativeCausewayLooks.Active(map.id))CausewayArtChecks.Layout(w);
        if(NativePierLooks.Active(map.id))PierArtChecks.Layout(w,map);
        if(NativeEmporiumLooks.Active(map.id))Emporium(w,map,look,V,Q);
        if(NativeCyberCityLooks.Active(map.id))CyberCityArtLayout.Write(w,map,V);
        if(NativePolarLooks.Active(map.id))PolarArtChecks.Layout(w,map,look);
        if(NativeBrickfallScenery.Active(map.id))BrickfallArtLayout.Write(w,map,look,V);
        w.WriteEndObject();w.Flush();
        Console.WriteLine("stage layout "+map.id+": "+map.blocks.Length+" blocks -> "+path);
    }

    private delegate void Vec(string name,double x,double y,double z);private delegate void Quat(string name,double x,double y,double z,double w);
    // The Sanctum's kit (Core NativeSanctumKit: every part with its family, the block it dresses, its role, kind and colour), its
    // candles and warm lights, the windows, the rigs' fixed numbers (Core NativeSanctumRig, NativeSanctumLooks) and each chandelier's
    // candles as the rig places them, so the models' wicks stand where the effects light them.
    private static void Sanctum(Utf8JsonWriter w,ContentMap map,MapLook look,Vec V,Quat Q)
    {
        var looks=NativeSanctumLooks.Resolve(look?.kitLooks);var kit=NativeSanctumKit.Build(map,looks);
        w.WriteStartArray("kinds");foreach(var k in NativeSanctumLooks.Kinds)w.WriteStringValue(k);w.WriteEndArray();
        w.WriteStartArray("kindColors");for(int k=0;k<NativeSanctumLooks.Kinds.Length;k++)w.WriteNumberValue(looks.Color[k]);w.WriteEndArray();
        w.WriteStartArray("parts");
        for(int i=0;i<kit.Parts.Count;i++)
        {
            ref var p=ref kit.Parts.Items[i];w.WriteStartObject();w.WriteString("f",kit.Family[i]);if(kit.Owner[i]!=null)w.WriteString("o",kit.Owner[i]);w.WriteNumber("r",kit.Role[i]);w.WriteNumber("k",kit.Kind[i]);
            V("c",p.X,p.Y,p.Z);V("h",p.Hx,p.Hy,p.Hz);Q("q",p.Qx,p.Qy,p.Qz,p.Qw);w.WriteBoolean("s",p.Shadow);w.WriteNumber("col",p.Color);w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartArray("candles");for(int i=0;i+4<kit.Candles.Count;i+=5){w.WriteStartArray();for(int j=0;j<5;j++)w.WriteNumberValue(Math.Round(kit.Candles[i+j],5));w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("lights");for(int i=0;i+6<kit.Lights.Count;i+=7){w.WriteStartArray();for(int j=0;j<7;j++)w.WriteNumberValue(Math.Round(kit.Lights[i+j],5));w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("windows");
        foreach(var x in NativeSanctumLooks.Windows){w.WriteStartObject();V("c",x.X,x.Y,x.Z);w.WriteNumber("half",x.Half);w.WriteNumber("halfY",x.HalfY);w.WriteNumber("nx",x.Nx);w.WriteNumber("nz",x.Nz);w.WriteBoolean("rose",x.Rose);w.WriteNumber("design",x.Design);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartObject("rig");
        w.WriteNumber("anchor",NativeSanctumRig.Anchor);w.WriteNumber("crown",NativeSanctumLooks.CrownHeight);w.WriteNumber("hoop",NativeSanctumRig.Hoop);
        V("bell",NativeSanctumLooks.BellX,NativeSanctumLooks.BellY,NativeSanctumLooks.BellZ);w.WriteNumber("bellMouth",NativeSanctumLooks.BellMouth);w.WriteNumber("bellHeight",NativeSanctumLooks.BellHeight);
        w.WriteNumber("censerPivot",NativeSanctumLooks.CenserPivotY);w.WriteNumber("gallery",NativeSanctumLooks.Gallery);w.WriteNumber("vault",NativeSanctumLooks.Vault);
        w.WriteNumber("belfryFloor",NativeSanctumLooks.BelfryFloor);w.WriteNumber("belfryTop",NativeSanctumLooks.BelfryTop);w.WriteNumber("oculus",NativeSanctumLooks.Oculus);
        var crane=map.hazards.FirstOrDefault(h=>h.kind==NativeFactoryHazards.Crane);
        w.WriteStartArray("chandeliers");
        for(int k=0;k<NativeSanctumRig.Chandeliers&&crane!=null&&k<crane.zones.Length;k++)
        {
            var z=crane.zones[k];var tilt=NativeSanctumRig.DownTilt(k);w.WriteStartObject();w.WriteNumber("k",k);V("zone",z.center.x,z.center.y,z.center.z);w.WriteNumber("carry",crane.carry);Q("tilt",tilt.X,tilt.Y,tilt.Z,tilt.W);
            w.WriteStartArray("candles");
            for(int i=0;i<NativeSanctumRig.ChandelierCandles;i++){NativeSanctumRig.Candle(k,i,out double a,out double tall,out bool stands,out double sa,out double sr,out bool glows);w.WriteStartObject();w.WriteNumber("a",a);w.WriteNumber("tall",tall);w.WriteBoolean("stands",stands);w.WriteNumber("sa",sa);w.WriteNumber("sr",sr);w.WriteEndObject();}
            w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();w.WriteEndObject();
    }

    // The Emporium's kit (Core NativeEmporiumKit: every part with its family, the block it dresses, whether it is overhead, its
    // kind, colour and light level or HDR gain) and the places its rig, effects and models agree on (Core NativeEmporiumLooks,
    // NativeEmporiumRig).
    private static void Emporium(Utf8JsonWriter w,ContentMap map,MapLook look,Vec V,Quat Q)
    {
        var looks=NativeEmporiumLooks.Resolve(look?.kitLooks);var kit=NativeEmporiumKit.Build(map,look);
        w.WriteStartArray("kinds");foreach(var k in NativeEmporiumLooks.Kinds)w.WriteStringValue(k);w.WriteEndArray();
        w.WriteStartArray("kindColors");for(int k=0;k<NativeEmporiumLooks.Kinds.Length;k++)w.WriteNumberValue(looks.Color[k]);w.WriteEndArray();
        w.WriteStartArray("parts");
        for(int i=0;i<kit.Parts.Count;i++)
        {
            ref var p=ref kit.Parts.Items[i];w.WriteStartObject();w.WriteString("f",kit.Family[i]);if(kit.Owner[i]!=null)w.WriteString("o",kit.Owner[i]);w.WriteNumber("r",kit.Overhead[i]?1:0);w.WriteNumber("k",kit.Kind[i]);
            V("c",p.X,p.Y,p.Z);V("h",p.Hx,p.Hy,p.Hz);Q("q",p.Qx,p.Qy,p.Qz,p.Qw);w.WriteBoolean("s",p.Shadow);w.WriteNumber("col",p.Color);w.WriteNumber("w",Math.Round(p.Window,3));w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartObject("rig");
        V("sign",NativeEmporiumLooks.SignX,NativeEmporiumLooks.SignY,NativeEmporiumLooks.SignZ);w.WriteNumber("signLength",NativeEmporiumLooks.SignLength);
        w.WriteNumber("eaves",NativeEmporiumLooks.Eaves);w.WriteNumber("ridge",NativeEmporiumLooks.Ridge);w.WriteNumber("roofHalfZ",NativeEmporiumLooks.RoofHalfZ);w.WriteNumber("roofHalfX",NativeEmporiumLooks.RoofHalfX);
        w.WriteNumber("conveyorY",NativeEmporiumRig.ConveyorY);w.WriteNumber("conveyorX0",NativeEmporiumRig.ConveyorX0);w.WriteNumber("conveyorX1",NativeEmporiumRig.ConveyorX1);w.WriteNumber("conveyorZ",NativeEmporiumRig.ConveyorZ);
        w.WriteNumber("mascotBase",NativeEmporiumRig.MascotBase);w.WriteNumber("bunHeight",NativeEmporiumArt.BunHeight);
        w.WriteStartArray("lanes");
        foreach(var h in map.hazards??Array.Empty<MapHazard>())if(h.kind=="maglev"){w.WriteStartObject();w.WriteString("id",h.id);w.WriteNumber("radius",NativeEmporiumRig.Radius(h));w.WriteNumber("long",NativeEmporiumRig.Long(h));w.WriteEndObject();}
        w.WriteEndArray();w.WriteEndObject();
    }
}
