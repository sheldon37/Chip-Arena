using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Brickfall's stage art (docs/arenas/BRICKFALL-ART.md, docs/arenas/STAGE-ART.md): the rules its bank, runtime and telegraph share
// (which segment a block answers to, which members wear the band, each segment's heap, the lamp's colours), the pilot columns
// to the owner's rule (only the frontage's caps), the layout export, the runtime's wiring and its bank-absent fallback; and,
// where the bank is on this machine (it is git-ignored), the bank itself against the map: every block covered, every moving
// block modelled in three tiers within its own box, every segment's heap, the play-space rule and the spawns.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --brickfall-art` (also in the full run).
internal static class BrickfallArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Brickfall art: "+why);}
        Check(NativeStageArt.BankOf("brickfall")=="ChipArenaStageBrickfall"&&NativeBrickfallArt.Bank=="ChipArenaStageBrickfall"&&NativeStageArt.BankOf("sanctum")=="ChipArenaStageSanctum","Brickfall's bank is named beside the others");
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("brickfall");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,"brickfall");

        // ---- the rules ----
        var moving=map.blocks.Where(b=>b.role!="static").ToArray();var statics=map.blocks.Where(b=>b.role=="static").ToArray();
        Check(map.blocks.Length==507&&moving.Length==144&&map.segments.Length==10,"the map the art is modelled on: 507 blocks, 144 of them moved or broken, ten segments");
        Check(statics.All(b=>NativeBrickfallArt.Segment(map,b)==null),"a static block answers to no segment");
        foreach(var s in map.segments)
        {
            Check(map.blocks.Where(b=>b.segment==s.id).All(b=>NativeBrickfallArt.Segment(map,b)==s.id),"segment "+s.id+"'s blocks answer to it");
            foreach(var p in s.supports)if(!string.IsNullOrEmpty(p.member))Check(NativeBrickfallArt.Segment(map,map.blocks.First(b=>b.id==p.member))==s.id,"member "+p.member+" answers to "+s.id);
            Check(NativeBrickfallArt.Heap(s)==s.rubble[0].id&&s.rubble.All(r=>r.role=="rubble"),"segment "+s.id+"'s heap is its first rubble block");
        }
        Check(moving.Where(b=>b.role=="cover").All(b=>NativeBrickfallArt.Segment(map,b)==null)&&moving.Count(b=>NativeBrickfallArt.Segment(map,b)!=null)==114,"the cover props answer to no segment; the 114 segment blocks do");
        var banded=map.blocks.Where(b=>NativeBrickfallArt.Banded(look,b)).Select(b=>b.id).ToArray();
        Check(banded.Length==14&&banded.SequenceEqual(map.blocks.Where(b=>b.role=="structural").Select(b=>b.id)),"the band and its lamp are the fourteen structural members'");
        NativeBrickfallArt.Lamp(0,out float r,out float g,out float b0);Check(r==NativeBrickfallArt.LampDark&&g==r&&b0==r,"a dark lamp");
        NativeBrickfallArt.Lamp(NativeBlockLooks.LampAmber,out r,out g,out b0);Check(Math.Abs(r-1.5f)<1e-4&&g>.55f&&g<.7f&&b0==0,"the amber lamp is the dressing's (#FFB000 x 1.5, linear)");
        NativeBrickfallArt.Lamp(NativeBlockLooks.LampRed|NativeBlockLooks.Band,out r,out g,out b0);Check(r>.95f&&r<1.05f&&g<.06f&&b0<.05f,"the red lamp is the dressing's (#D6332A x 1.5, linear)");
        Check(NativeBlockLooks.Lamp(0,false,true,false)==NativeBlockLooks.LampAmber&&NativeBlockLooks.Lamp(0,true,false,true)==NativeBlockLooks.LampRed&&NativeBlockLooks.Lamp(2,false,false,true)==NativeBlockLooks.LampRed&&NativeBlockLooks.Lamp(0,true,false,false)==0,"the lamp's states are the dressing's");

        // ---- no invisible walls: only the frontage's caps keep a pilot column ----
        var columns=map.pilotBlocks;
        Check(columns.Length==4&&columns.All(p=>p.id.StartsWith("frontage-",StringComparison.Ordinal)&&!p.bots&&p.bottom>=13.9f),"four pilot columns, all the frontage's caps ("+string.Join(",",columns.Select(p=>p.id))+")");
        Check(columns.All(p=>Math.Abs(p.center.x)+p.size.x/2>=map.bounds.maxX+1.99f&&Math.Abs(p.center.x)>=30||Math.Abs(p.center.z)>=26),"every column stands on the map's edge, over a frontage wall");
        // (the chimneys stand in the frontage: its cap's column covers their crowns too)
        foreach(var id in new[]{"hotel-stairhouse-roof","hotel-nw-slab-2","shop-kessels","market-kiosk-w","yard-truck-sw","cover-monument-ne","cover-tram-shelter-n"})
        {var k=map.blocks.First(x=>x.id==id);var at=new V3(k.pos.x,k.pos.y+k.size.y/2+.9f,k.pos.z);Check((NativeMapRules.ExcludeTops(map,at)-at).Length<.001,"a pilot may stand on "+id);}
        var gen=File.ReadAllText(Path.Combine(root,"tools/generate_brickfall.py"));
        Check(!gen.Contains("hotel-stairhouse-top")&&!gen.Contains("sid+'-top'")&&gen.Contains("frontage-top-")&&File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py")).Contains("if old['id']=='brickfall':old['pilotBlocks']=[p for p in old['pilotBlocks'] if p['id'].startswith('frontage-')]"),"the generators author the same columns");

        // ---- the layout export ----
        string tmp=Path.Combine(Path.GetTempPath(),"brickfall-art-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            var stdout=Console.Out;Console.SetOut(TextWriter.Null);try{StageArtLayout.Write(root,"brickfall",tmp);}finally{Console.SetOut(stdout);}
            using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var L=doc.RootElement;
            Check(L.GetProperty("blocks").GetArrayLength()==507&&L.GetProperty("looks").GetArrayLength()==507&&L.GetProperty("segments").GetArrayLength()==10,"the layout lists the blocks, their looks and the segments");
            Check(L.GetProperty("looks").EnumerateArray().Count(x=>x.GetProperty("band").GetBoolean())==14&&L.GetProperty("looks").EnumerateArray().Count(x=>x.TryGetProperty("segment",out _))==114,"the layout's bands and segment blocks");
            Check(L.GetProperty("segments").EnumerateArray().Sum(x=>x.GetProperty("rubble").GetArrayLength())==map.segments.Sum(s=>s.rubble.Length)&&L.GetProperty("segments").EnumerateArray().All(x=>x.GetProperty("heap").GetString()==x.GetProperty("rubble")[0].GetProperty("id").GetString()),"the layout's rubble and heaps");
            Check(L.GetProperty("spawns").GetArrayLength()==16&&L.GetProperty("pilotBlocks").GetArrayLength()==4&&L.GetProperty("lights").GetArrayLength()==look.lights.Length,"the layout's spawns, columns and practical lights");
        }
        finally{try{File.Delete(tmp);}catch{}}

        // ---- the runtime's wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var pres=R("Assets/ChipArena/Runtime/NativeBrickfallArtPresentation.cs");var scenery=R("Assets/ChipArena/Runtime/NativeBrickfallPresentation.cs");
        Check(pres.Contains("brickfallArt=gameObject.AddComponent<NativeStageArtDraw>();brickfallArt.Tell=BrickfallTell;")&&pres.Contains("if(!brickfallArt.Prepare(map,null,null))return false;")&&pres.Contains("BrickfallArtDrawn=brickfallArt.Render(arena,WorldEye,LowGraphics,null);"),"the presentation prepares and renders the art");
        Check(scenery.Contains("BrickfallCover(arena);DrawBrickfall(now,WorldEye,true,roundStart);BrickfallArt(arena);")&&R("Assets/ChipArena/Runtime/NativeStagePrewarm.cs").Contains("if(NativeBrickfallScenery.Active(id)&&EnsureBrickfall()){BrickfallArtPrepare(map);built++;}"),"the art draws with the arena's scenery and is prepared behind the loading screen");
        Check(pres.Contains("stressed=fx.TelegraphFor(NativeStructureFx.Seed(arena.MapId,k.segment,NativeStructureFx.RoundIndex(match.Set,match.Round)),new V3(at.x,at.y,at.z),brickfallArtNow,out window,out var sag,out float qx,out float qy,out float qz,out float qw);")
            &&pres.Contains("if(stressed){at+=new Vector3(sag.x,sag.y,sag.z);rotation=new Quaternion(qx,qy,qz,qw)*rotation;}")&&pres.Contains("if(!k.band){glow=new Vector4(window,window,window,1);return;}")
            &&pres.Contains("NativeBrickfallArt.Lamp(NativeBlockLooks.Lamp(stage,stressed,brickfallArtLocked,brickfallArtRed),out float r,out float g,out float b);"),"the models take the dressing's telegraph: the sag and lean, the window light, the band lamp");
        var dress=R("Assets/ChipArena/Runtime/NativeArenaDressing.cs");
        Check(dress.Contains("if(arena.Map.unity){if(e.Segment!=null)stressed=structures.TelegraphFor(NativeStructureFx.Seed(arena.MapId,e.Segment,NativeStructureFx.RoundIndex(match.Set,match.Round)),point,now,out window,out sag,out qx,out qy,out qz,out qw);}")
            &&dress.Contains("if(stageArt&&NativeStageArtDraw.Hides(arena.MapId,p.Definition.id,stage))continue;"),"the dressing's own telegraph and its give-way to the art are as the models copy them");
        var draw=R("Assets/ChipArena/Runtime/NativeStageArt.cs");
        Check(draw.Contains("public delegate void BlockTell(NativePropBody body,ref Vector3 at,ref Quaternion rotation,ref Vector4 glow);")&&draw.Contains("var rot=t.rotation;if(Tell!=null){var lit=Vector4.one;Tell(b,ref at,ref rot,ref lit);block.SetVector(GlowId,lit);}"),"the drawer lets a stage move and light a block's model");
        Check(draw.Contains("foreach(var sd in map.segments??Array.Empty<StructureDefinition>())foreach(var b in sd.rubble??Array.Empty<ContentBlock>())")&&draw.Contains("if(b==null||b.Definition?.id==null||b.Definition.role!=\"rubble\"||!blocks.TryGetValue(b.Definition.id,out var bd))continue;"),"the drawer draws a landed segment's rubble models");
        Check(draw.Contains("var bank=NativePackBanks.Bank(name);if(bank==null)return false;"),"without the bank nothing is made (the dressed blocks draw as before)");
        var shader=R("Assets/ChipArena/Resources/NativeStageLit.shader");Check(shader.Contains("#define DETAIL_BRICK 21")&&shader.Contains("else if(detail==DETAIL_BRICK)")&&R("tools/stages/stagekit.py").Contains("DETAIL_BRICK = 21"),"the brickwork surface, in the shader and the stage kit");
        foreach(var f in new[]{"Assets/ChipArena/Core/NativeBrickfallArt.cs","Assets/ChipArena/Runtime/NativeBrickfallArtPresentation.cs"})Check(File.Exists(Path.Combine(root,f+".meta")),f+" has its meta");
        foreach(var f in new[]{"tools/stages/brickfall_art.py","tools/stages/brickfall_parts.py","tools/stages/brickfall_props.py","tools/stages/brickfall_kit.py"})Check(File.Exists(Path.Combine(root,f)),f+" is in the tools");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Brickfall art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);double mb=new FileInfo(bankPath).Length/1048576.0;
            Check(mb<=21,"the bank keeps to its budget ("+mb.ToString("0.0")+" MB)");
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            foreach(var s in map.segments)foreach(var rb in s.rubble)Check(sp.Covers.Contains(rb.id),"the art replaces rubble "+rb.id);
            Check(moving.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)&&sp.Blocks[b.id].Distinct().Count()==3),"every block the simulation moves or breaks has its model in three tiers ("+string.Join(",",moving.Where(b=>!sp.Blocks.ContainsKey(b.id)).Take(5).Select(b=>b.id))+")");
            Check(statics.All(b=>!sp.Blocks.ContainsKey(b.id)),"a static block has no model of its own (it is in the chunks)");
            Check(map.segments.All(s=>sp.Blocks.ContainsKey(NativeBrickfallArt.Heap(s))&&sp.Blocks[NativeBrickfallArt.Heap(s)].Distinct().Count()==3)&&map.segments.Sum(s=>s.rubble.Count(rb=>sp.Blocks.ContainsKey(rb.id)))==10,"each segment's heap in three tiers, on its first rubble block only");
            Check(sp.Static.Count>=12&&sp.Static.All(c=>c.Props.All(p=>p>=0)),"the static chunks, every tier present");
            Check(sp.Rig.Count==0,"Brickfall's art has no rig (its movers are its blocks)");
            // a moving block's model keeps to its block: within .3 m of its box (the hotel's blade sign and a deck's girders are overhead)
            var byId=map.blocks.ToDictionary(x=>x.id);int out_=0,seen=0;double worst=0;string worstId="";
            foreach(var kv in sp.Blocks)
            {
                if(!byId.TryGetValue(kv.Key,out var blk))continue;var prop=bank.Props[kv.Value[0]];
                foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    var q=System.Numerics.Vector3.Transform(v,part.Place);double x=q.X,y=q.Y,z=q.Z;seen++;
                    bool low=blk.pos.y+y<2.0;double d=Math.Max(Math.Max(Math.Abs(x)-blk.size.x/2,Math.Abs(z)-blk.size.z/2),low?Math.Abs(y)-blk.size.y/2:-1);
                    if(low&&d>.3)out_++;if(low&&d>worst){worst=d;worstId=kv.Key;}
                }
            }
            Check(seen>20000&&out_==0,"the block models keep to their blocks below 2 m ("+out_+" vertices out, the farthest "+worst.ToString("0.00")+" m on "+worstId+")");
            // the play space: below 2 m over the yard and the floors the Fine chunks keep to the blocks (within .25 m; a lamp or a
            // strewn brick within .9 m); higher is overhead; nothing stands in a spawn's capsule
            var boxes=map.blocks.Select(k=>(x0:k.pos.x-k.size.x/2,x1:k.pos.x+k.size.x/2,y0:k.pos.y-k.size.y/2,y1:k.pos.y+k.size.y/2,z0:k.pos.z-k.size.z/2,z1:k.pos.z+k.size.z/2)).ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            foreach(var c in sp.Static)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    if(v.Y>2||v.Y<-.05)continue;total++;
                    double best=double.MaxValue;foreach(var k in boxes){double dx=Math.Max(0,Math.Max(k.x0-v.X,v.X-k.x1)),dy=Math.Max(0,Math.Max(k.y0-v.Y,v.Y-k.y1)),dz=Math.Max(0,Math.Max(k.z0-v.Z,v.Z-k.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25){proud++;var key=((int)Math.Floor(v.X))+","+((int)Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Brickfall art: bank "+mb.ToString("0.00")+" MB, "+bank.Props.Count+" props, "+sp.Static.Count+" chunks, "+sp.Blocks.Count+" block models; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>total/200)Console.WriteLine("Brickfall art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(30).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS Brickfall art ("+count+" checks)");
    }
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageBrickfall.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageBrickfall.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
}
