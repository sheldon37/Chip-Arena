using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

// What a stage draws beside its static art, at one moment, as JSON for the coplanar-face analyser (tools/stages/zfight.py;
// docs/arenas/STAGE-ART.md "Depth fighting"): the rig's posed models (slot name and its 3 x 4 place, as the art's drawer gets
// them) and the kit boxes a stage keeps drawing over its art (Ward Zero's lit boxes, Cyber City's driven parts, the Factory's
// skylight panes and telegraph lamps), each with its centre, half extents, rotation and the layers it draws nearer the eye.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-art-rest <map> <out.json> [seconds]`.
internal static class StageArtRest
{
    public static void Write(string root,string mapId,string path,double t)
    {
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);
        var map=content.Map(mapId)??throw new Exception("no map "+mapId);
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,mapId);
        var pose=new NativeStageArtPose();string[] slots=Array.Empty<string>();
        using var fs=File.Create(path);using var w=new Utf8JsonWriter(fs,new JsonWriterOptions{Indented=false});
        w.WriteStartObject();w.WriteString("map",map.id);w.WriteNumber("time",t);w.WriteStartArray("boxes");
        void Box(string kind,double x,double y,double z,double hx,double hy,double hz,double qx,double qy,double qz,double qw,int layer=0)
        {
            w.WriteStartObject();w.WriteString("kind",kind);w.WriteStartArray("c");w.WriteNumberValue(x);w.WriteNumberValue(y);w.WriteNumberValue(z);w.WriteEndArray();
            w.WriteStartArray("h");w.WriteNumberValue(hx);w.WriteNumberValue(hy);w.WriteNumberValue(hz);w.WriteEndArray();
            w.WriteStartArray("q");w.WriteNumberValue(qx);w.WriteNumberValue(qy);w.WriteNumberValue(qz);w.WriteNumberValue(qw);w.WriteEndArray();w.WriteNumber("layer",layer);w.WriteEndObject();
        }
        // a kit part in its block's frame
        void Child(string kind,ContentBlock b,double x,double y,double z,double hx,double hy,double hz,int layer)
        {
            double qx=b.rotation?.x??0,qy=b.rotation?.y??0,qz=b.rotation?.z??0,qw=b.rotation==null?1:b.rotation.w;
            // v' = v + 2w(q x v) + 2 q x (q x v)
            double cx=qy*z-qz*y,cy=qz*x-qx*z,cz=qx*y-qy*x,dx=qy*cz-qz*cy,dy=qz*cx-qx*cz,dz=qx*cy-qy*cx;
            Box(kind,b.pos.x+x+2*(qw*cx+dx),b.pos.y+y+2*(qw*cy+dy),b.pos.z+z+2*(qw*cz+dz),hx,hy,hz,qx,qy,qz,qw,layer);
        }
        switch(mapId)
        {
            case "harbor":{var rig=new NativePierRig(NativePierLooks.Resolve(look.kitLooks),map);rig.Build(map.hazards,0,NativePhase.Fight,0,t,t);NativePierArt.Pose(rig,pose);slots=NativePierArt.Slots;break;}
            case "rig":{var rig=new NativeDeepwellRig(NativeDeepwellLooks.Resolve(look.kitLooks),map);rig.Build(map.hazards,0,NativePhase.Fight,0,t,t);NativeDeepwellArt.Pose(rig,pose);slots=NativeDeepwellArt.Slots;break;}
            case "skyframe":{var rig=new NativeSkyframeRig(NativeSkyframeLooks.Resolve(look.kitLooks),map);rig.Build(map.hazards,0,NativePhase.Fight,0,t,t);NativeSkyframeArt.Pose(rig,pose);slots=NativeSkyframeArt.Slots;break;}
            case "breakwater":{var rig=new NativeBreakwaterRig(NativeBreakwaterLooks.Resolve(look.kitLooks),map);rig.Build(map.hazards,0,NativePhase.Fight,0,t,t);NativeBreakwaterArt.Pose(rig,pose);slots=NativeBreakwaterArt.Slots;break;}
            case "polar":{var rig=new NativePolarRig(NativePolarLooks.Resolve(look.kitLooks),map);rig.Build(map,new int[8],new float[8],0,NativePhase.Fight,0,t);NativePolarArt.Pose(rig,pose);slots=NativePolarArt.Slots;break;}
            case "factory":
            {
                var looks=NativeFactoryKit.Resolve(look.kitLooks);var rig=new NativeFactoryRig(looks);FactoryHazard[] hz=map.hazards;rig.Build(hz,0,t);NativeFactoryArt.Pose(hz,map.blocks,0,t,t,pose);slots=NativeFactoryArt.Slots;
                for(int i=0;i<rig.Parts.Count;i++){if(!rig.Kept[i])continue;var p=rig.Parts.Items[i];Box("lamp",p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,p.Qx,p.Qy,p.Qz,p.Qw,NativeStageArt.KitLayers);}
                var kit=NativeFactoryKit.Build(map.blocks,hz,looks);int g=NativeFactoryKit.Glass;
                for(int i=0;i<kit.Count;i++){var p=kit.Items[i];if(p.Shadow||p.Look!=looks.Look[g]||p.Color!=looks.Color[g]||p.Flags!=looks.Flags[g])continue;Box("pane",p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,p.Qx,p.Qy,p.Qz,p.Qw,NativeStageArt.KitLayers);}
                break;
            }
            case "cybercity":
            {
                var hazards=map.hazards;var exits=NativeCyberCityExits.Build(map.blocks,map.nav,hazards);var kit=NativeCyberCityKit.Build(map.blocks,hazards,exits);var states=new NativeHazardState[hazards.Length];
                for(int i=0;i<hazards.Length;i++)states[i]=NativeCyberCityHazards.At(hazards[i],NativePhase.Fight,0,0,t);NativeCyberCityArt.Pose(hazards,states,kit.Maglev,pose);slots=NativeCyberCityArt.Slots;
                var up=NativeCyberCityArt.KeptLayers(kit,map.blocks);
                for(int i=0;i<kit.Parts.Length;i++){var p=kit.Parts[i];if(!NativeCyberCityArt.Kept(p)||p.Parent<0||p.Parent>=map.blocks.Length)continue;Child("driven-"+p.Drive,map.blocks[p.Parent],p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,NativeStageArt.KitLayers+up[i]);}
                break;
            }
            case "siren":case "expedition-ward-zero":
            {
                var entries=WardZeroArtLayout.Entries(root);var kit=NativeWardZeroKit.Build(map.blocks,map.hazards,entries);var art=NativeWardZeroArt.Build(kit,map.blocks,entries);
                var states=new NativeHazardState[map.hazards.Length];for(int i=0;i<states.Length;i++)states[i]=NativeWardZeroHazards.At(map.hazards[i],NativePhase.Fight,0,0,t);
                art.Pose(kit,map.blocks,states,new float[entries.Length],NativeWardZeroMood.Preset("calm"),new bool[entries.Length],new bool[map.blocks.Length],new float[map.blocks.Length*3],pose);slots=art.Slots;
                var up=NativeWardZeroArt.KeptLayers(kit,map.blocks);
                for(int i=0;i<kit.Parts.Length;i++){if(!kit.Keeps[i])continue;var p=kit.Parts[i];if(p.Parent<0||p.Parent>=map.blocks.Length)continue;Child(kit.Family[i],map.blocks[p.Parent],p.X,p.Y,p.Z,p.Hx,p.Hy,p.Hz,NativeStageArt.KitLayers+up[i]);}
                break;
            }
            case "brickfall":break;
            default:throw new Exception("no rest export for "+mapId);
        }
        w.WriteEndArray();w.WriteStartArray("rig");
        for(int i=0;i<pose.Count;i++)
        {
            int s=pose.Slot[i];if(s<0||s>=slots.Length)continue;w.WriteStartObject();w.WriteString("slot",slots[s]);w.WriteStartArray("place");
            for(int k=0;k<12;k++)w.WriteNumberValue(pose.Place[i*12+k]);w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();w.WriteEndObject();
    }
}
