using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Skyframe's stage art (docs/arenas/STAGE-ART.md, docs/arenas/SKYFRAME.md "Stage art"): its bank's name, the art poses against the
// box rig at every moment (each model stands where the rig's own tagged part does), the layout export's shared numbers, the
// presentation's wiring and its bank-absent fallback; and, where the bank is on this machine (it is git-ignored), the bank itself
// against the map. `dotnet run --project Tests/CoreTests.csproj -c Release -- --skyframe-art` (also in the full run).
internal static class SkyframeArtChecks
{
    /// <summary>The numbers the Blender scripts share (tools/stages/skyframe_*.py), into the stage layout.</summary>
    public static void Layout(Utf8JsonWriter w,ContentMap map)
    {
        void A(string name,params double[] v){w.WriteStartArray(name);foreach(var x in v)w.WriteNumberValue(Math.Round(x,5));w.WriteEndArray();}
        w.WriteStartObject("skyframe");
        w.WriteStartArray("slots");foreach(var s in NativeSkyframeArt.Slots)w.WriteStringValue(s);w.WriteEndArray();
        w.WriteStartObject("crane");w.WriteNumber("jibY",NativeSkyframeLooks.JibY);w.WriteNumber("jibLength",NativeSkyframeLooks.JibLength);w.WriteNumber("counterJib",NativeSkyframeLooks.CounterJib);
        w.WriteNumber("jibDepth",NativeSkyframeLooks.JibDepth);w.WriteNumber("jibHalf",NativeSkyframeLooks.JibHalf);w.WriteNumber("mastHalf",NativeSkyframeLooks.MastHalf);w.WriteNumber("minReach",NativeSkyframeLooks.MinReach);w.WriteEndObject();
        w.WriteStartObject("frame");w.WriteNumber("half",NativeSkyframeLooks.FrameHalf);w.WriteNumber("y",NativeSkyframeLooks.FrameY);w.WriteNumber("storey",NativeSkyframeLooks.Storey);w.WriteNumber("storeys",NativeSkyframeLooks.Storeys);
        w.WriteNumber("coreTop",NativeSkyframeLooks.CoreTop);w.WriteNumber("coreRise",NativeSkyframeLooks.CoreRise);
        var cols=NativeSkyframeKit.Columns(map);w.WriteStartArray("columns");
        for(int c=0;c<cols.Count;c++)
        {
            w.WriteStartObject();w.WriteNumber("x",cols[c].x);w.WriteNumber("z",cols[c].z);w.WriteNumber("rise",NativeSkyframeKit.FrameRise(c));
            w.WriteStartArray("ring");for(int s=0;s<NativeSkyframeLooks.Storeys;s++)w.WriteBooleanValue(NativeSkyframeKit.FrameRise(c)>s&&NativeSkyframeKit.FrameRise((c+1)%cols.Count)>s&&NativeSkyframeKit.FrameBeam(s,c,(c+1)%cols.Count));w.WriteEndArray();
            w.WriteStartArray("radial");for(int s=0;s<NativeSkyframeLooks.Storeys;s++)w.WriteBooleanValue(NativeSkyframeKit.FrameRise(c)>s&&NativeSkyframeKit.FrameBeam(s,c,c+8));w.WriteEndArray();w.WriteEndObject();
        }
        w.WriteEndArray();w.WriteEndObject();
        NativeSkyframeLooks.Sun(out double sx,out double sy,out double sz);A("sun",sx,sy,sz);w.WriteNumber("cloudY",NativeSkyframeLooks.CloudY);w.WriteNumber("turbineRadius",NativeSkyframeLooks.TurbineRadius);
        w.WriteStartArray("turbines");for(int i=0;i<NativeSkyframeLooks.Turbines;i++){NativeSkyframeLooks.Turbine(i,out double x,out double y,out double z);w.WriteStartArray();w.WriteNumberValue(x);w.WriteNumberValue(y);w.WriteNumberValue(z);w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("socks");foreach(var h in map.hazards)if(h.kind==NativeFactoryHazards.Vent){NativeSkyframeRig.SockAt(h,out double x,out double y,out double z);w.WriteStartArray();w.WriteNumberValue(x);w.WriteNumberValue(y);w.WriteNumberValue(z);w.WriteNumberValue(Math.Atan2(h.axis.x,h.axis.z));w.WriteEndArray();}w.WriteEndArray();
        w.WriteNumber("sockPole",NativeSkyframeRig.SockPole);w.WriteNumber("sockLength",NativeSkyframeRig.SockLength);
        w.WriteStartArray("airships");for(int i=0;i<NativeSkyframeLooks.Airships;i++)w.WriteNumberValue(NativeSkyframeLooks.AirshipLength(i));w.WriteEndArray();
        w.WriteEndObject();
    }

    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Skyframe art: "+why);}
        Check(NativeStageArt.BankOf("skyframe")==NativeSkyframeArt.Bank&&NativeSkyframeArt.Bank=="ChipArenaStageSkyframe","Skyframe's bank is named");
        var slots=NativeSkyframeArt.Slots;
        Check(slots.Length==19&&slots.Distinct().Count()==19&&slots[NativeSkyframeArt.Load]=="load"&&slots[NativeSkyframeArt.Crown+2]=="crown-2"&&slots[NativeSkyframeArt.Windsock]=="windsock"&&slots[NativeSkyframeArt.Rotor]=="rotor"
            &&slots[NativeSkyframeArt.Airship+1]=="airship-1"&&slots[NativeSkyframeArt.WingR]=="wing-r"&&slots[NativeSkyframeArt.Cloud+3]=="cloud-3","the rig slots");
        Check(NativeSkyframeArt.Casts(NativeSkyframeArt.CraneTop)&&NativeSkyframeArt.Casts(NativeSkyframeArt.Load)&&NativeSkyframeArt.Casts(NativeSkyframeArt.Crown+1)&&!NativeSkyframeArt.Casts(NativeSkyframeArt.Cloud)&&!NativeSkyframeArt.Casts(NativeSkyframeArt.Bird)&&!NativeSkyframeArt.Casts(NativeSkyframeArt.Rotor),"the crane, its bundle and the crowns cast shadows; the sky's pieces do not");

        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("skyframe");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var looks=NativeSkyframeLooks.Resolve(NativeMapLooks.Find(set,"skyframe").kitLooks);
        var rig=new NativeSkyframeRig(looks,map);var pose=new NativeStageArtPose();

        // ---- the poses: every model stands where the box rig's own part does, at every moment of two rounds ----
        var seen=new int[slots.Length];int most=0;double worst=0;
        for(int round=0;round<2;round++)for(double t=0;t<90;t+=.05)
        {
            rig.Build(map.hazards,round,NativePhase.Fight,0,t,t);NativeSkyframeArt.Pose(rig,pose);most=Math.Max(most,pose.Count);int j=0;
            for(int i=0;i<rig.Parts.Count;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;Check(j<pose.Count&&pose.Slot[j]==slot,"draw "+j+" is part "+i+"'s slot at "+t);ref var p=ref rig.Parts.Items[i];int at=j*12;seen[slot]++;
                worst=Math.Max(worst,Math.Abs(pose.Place[at+3]-p.X)+Math.Abs(pose.Place[at+7]-p.Y)+Math.Abs(pose.Place[at+11]-p.Z));
                double sx=Col(pose,j,0),sy=Col(pose,j,1),sz=Col(pose,j,2);var q=NativeSkyframeArt.Turn(p);q.Rotate(0,1,0,out double ux,out double uy,out double uz);
                worst=Math.Max(worst,Math.Abs(pose.Place[at+1]/sy-ux)+Math.Abs(pose.Place[at+5]/sy-uy)+Math.Abs(pose.Place[at+9]/sy-uz));
                switch(NativeSkyframeArt.Sizing(slot))
                {
                    case 0:Check(Math.Abs(sx-1)<1e-4&&Math.Abs(sy-1)<1e-4&&Math.Abs(sz-1)<1e-4,slots[slot]+" draws at its modelled size");break;
                    case 1:Check(Math.Abs(sx-p.Hx)<1e-4&&Math.Abs(sy-p.Hx)<1e-4&&Math.Abs(sz-p.Hx)<1e-4,slots[slot]+" takes its part's size");break;
                    default:Check(Math.Abs(sx-p.Hx)<1e-4*Math.Max(1,p.Hx)&&Math.Abs(sy-p.Hy)<1e-4*Math.Max(1,p.Hy)&&Math.Abs(sz-p.Hz)<1e-4*Math.Max(1,p.Hz),slots[slot]+" takes its part's extents");break;
                }
                j++;
            }
            Check(j==pose.Count,"a draw for every tagged part and no other ("+pose.Count+" / "+j+") at "+t);
        }
        Check(worst<1e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.00000")+")");
        Check(seen.All(n=>n>0),"two rounds show every rig model ("+string.Join(",",slots.Where((s,i)=>seen[i]==0))+")");
        Check(most>100&&most<NativeStageArtPose.Capacity-16,"a frame's rig draws fit the pose buffer (up to "+most+" of "+NativeStageArtPose.Capacity+")");
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativeSkyframeArt.Pose(rig,pose);int n0=pose.Count;var copy=pose.Place.Take(n0*12).ToArray();
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativeSkyframeArt.Pose(rig,pose);Check(pose.Count==n0&&pose.Place.Take(n0*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/30.0;rig.Build(map.hazards,1,NativePhase.Fight,0,t,t);NativeSkyframeArt.Pose(rig,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"skyframe",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;var s=j.GetProperty("skyframe");
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block and hazard");
            Check(s.GetProperty("slots").GetArrayLength()==slots.Length&&s.GetProperty("frame").GetProperty("columns").GetArrayLength()==8&&s.GetProperty("turbines").GetArrayLength()==NativeSkyframeLooks.Turbines&&s.GetProperty("socks").GetArrayLength()==2
                &&Math.Abs(s.GetProperty("crane").GetProperty("jibY").GetDouble()-NativeSkyframeLooks.JibY)<1e-9,"and the stage's shared numbers");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));var draw=R("Assets/ChipArena/Runtime/NativeSkyframePresentation.cs");
        Check(draw.Contains("art.Prepare(map,NativeSkyframeArt.Slots,NativeSkyframeArt.Casts);")&&draw.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation loads and draws the art");
        Check(draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}"),"the box kit and rig draw whenever the art does not");
        Check(draw.Contains("if(!ArtDrawn)for(int i=0;i<sectors.Length;i++)"),"the box-built backdrop gives way to the modelled sky");
        Check(R("Assets/ChipArena/Core/NativeSkyframeKit.cs").Contains("int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n)Slot[n]=slot;"),"the rig tags its parts beside the very add that builds them");
        Check(R("Tests/StageArtLayout.cs").Contains("if(NativeSkyframeLooks.Active(map.id))SkyframeArtChecks.Layout(w,map);"),"the layout export writes the stage's numbers");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Skyframe art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);long bytes=new FileInfo(bankPath).Length;
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var breakable=map.blocks.Where(b=>b.role!="static").ToArray();
            foreach(var b in breakable)Check(sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0),"breakable "+b.id+" has its model");
            Check(sp.Blocks.Count==breakable.Length,"a model for every breakable block and no other ("+sp.Blocks.Count+")");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)),"every rig slot has its model ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            var near=sp.Static.Where(c=>int.TryParse(c.Id,out _)).ToList();var far=sp.Static.Where(c=>!int.TryParse(c.Id,out _)).ToList();
            Check(near.Count>=20&&near.All(c=>c.Props.All(p=>p>=0))&&near.Count(c=>c.Props.Distinct().Count()==3)>=near.Count*9/10,"the map's chunks in three tiers (a chunk of fine detail alone borrows its nearest tier) ("+near.Count+")");
            Check(far.Count>=4&&far.All(c=>c.Props.Distinct().Count()==1),"the sky's far chunks, one tier each ("+far.Count+")");
            Check(bytes<=20500000,"the bank stays within its budget of about 20 MB ("+(bytes/1e6).ToString("0.0")+" MB)");
            // the play space: within 2 m over the floor under it, the Fine art keeps to the blocks (within .25 m: what looks like
            // cover is cover); higher is overhead. The core's and the columns' tops (12 m: no jump reaches them, the frame goes on
            // up from them) are no floor. Nothing stands in a spawn.
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double farthest=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;long verts=0;
            foreach(var c in near)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    verts++;double floor=double.NegativeInfinity;foreach(var b in boxes)if(v.X>=b.x0&&v.X<=b.x1&&v.Z>=b.z0&&v.Z<=b.z1&&b.y1<=v.Y+.05&&b.y1>floor&&b.y1<NativeSkyframeLooks.CoreTop-1)floor=b.y1;
                    if(double.IsNegativeInfinity(floor)||v.Y-floor>2)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25+1e-3){proud++;var key=((int)Math.Floor(v.X))+","+(Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}farthest=Math.Max(farthest,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Skyframe art: bank "+(bytes/1e6).ToString("0.00")+" MB, "+bank.Props.Count+" props, "+near.Count+" map chunks ("+verts+" Fine vertices) and "+far.Count+" far; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+farthest.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Skyframe art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
            Check(farthest<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+farthest.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS skyframe art ("+count+" checks; rig draws up to "+most+" a frame)");
    }
    // The length of column c of draw i's place (its scale along that axis).
    private static double Col(NativeStageArtPose p,int i,int c){int o=i*12+c;return Math.Sqrt(p.Place[o]*p.Place[o]+p.Place[o+4]*p.Place[o+4]+p.Place[o+8]*p.Place[o+8]);}
    private static string Bank(string root)
    {
        const string name="ChipArenaLocal/Resources/ChipArenaStageSkyframe.bytes";var dir=Environment.GetEnvironmentVariable("CHIPARENA_STAGE_BANKS");
        foreach(var p in new[]{dir==null?null:Path.Combine(dir,name),Path.Combine(root,"Assets/LocalAssets/"+name),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/"+name})
            if(p!=null&&File.Exists(p))return p;
        return null;
    }
}
