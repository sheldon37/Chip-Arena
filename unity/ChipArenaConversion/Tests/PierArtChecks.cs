using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Pier 9's stage art (docs/arenas/STAGE-ART.md, docs/arenas/HARBOR.md "Stage art"): its bank's name, the art poses against the
// box rig at every moment (each model stands where the rig's own tagged part does; the chasing bulbs' groups are lit every frame),
// the layout export's numbers, the presentation's wiring and its bank-absent fallback; and, where the bank is on this machine (it
// is git-ignored), the bank itself against the map: every block covered, every rig slot in three tiers, the chunks' tiers, the
// budget, the play-space rule and the spawns. `dotnet run --project Tests/CoreTests.csproj -c Release -- --pier-art` (also in the
// full run).
internal static class PierArtChecks
{
    /// <summary>The numbers the Blender scripts share (tools/stages/harbor_*.py), into the stage layout.</summary>
    public static void Layout(Utf8JsonWriter w,ContentMap map)
    {
        void N(string n,double v)=>w.WriteNumber(n,Math.Round(v,6));void A(string n,double[] v){w.WriteStartArray(n);foreach(double x in v)w.WriteNumberValue(Math.Round(x,6));w.WriteEndArray();}
        w.WriteStartObject("pier");
        N("sea",NativePierLooks.Sea);N("under",NativePierLooks.Under);N("wheelX",NativePierLooks.WheelX);N("wheelY",NativePierLooks.WheelY);N("wheelZ",NativePierLooks.WheelZ);N("wheelR",NativePierLooks.WheelR);N("wheelGap",NativePierLooks.WheelGap);
        N("gondolas",NativePierLooks.Gondolas);N("carouselX",NativePierLooks.CarouselX);N("carouselZ",NativePierLooks.CarouselZ);N("bandX",NativePierLooks.BandX);N("bandZ",NativePierLooks.BandZ);N("rideDeck",NativePierLooks.RideDeck);
        N("rideEave",NativePierLooks.RideEave);N("mounts",NativePierLooks.Mounts);N("mountLift",NativePierLooks.MountLift);N("dolls",NativePierLooks.Dolls);N("starY",NativePierLooks.StarY);
        N("trackLevel",NativePierLooks.TrackLevel);N("trackR",NativePierLooks.TrackR);N("trackTilt",NativePierLooks.TrackTilt);N("trackTop",NativePierLooks.TrackTop);N("railY",NativePierLooks.RailY);
        N("cars",NativePierLooks.Cars);N("carLength",NativePierLooks.CarLength);N("bargeX",NativePierScenery.BargeX);N("bargeZ",NativePierScenery.BargeZ);N("shoreZ",NativePierScenery.ShoreZ);
        A("strings",NativePierKit.Strings);A("pileX",NativePierKit.PileX);A("pileZ",NativePierKit.PileZ);A("boats",NativePierRig.Boats);
        // the mounts' rest places on the turntable (the table's own frame: its centre on the upper deck, unturned)
        w.WriteStartArray("mountAt");for(int i=0;i<NativePierLooks.Mounts;i++){NativePierLooks.Mount(i,0,out double x,out _,out double z,out double yaw,out _);w.WriteStartArray();foreach(double v in new[]{x-NativePierLooks.CarouselX,z-NativePierLooks.CarouselZ,yaw})w.WriteNumberValue(Math.Round(v,5));w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("dollAt");for(int i=0;i<NativePierLooks.Dolls;i++){NativePierLooks.Doll(i,0,out double x,out double y,out double z,out _,out _);w.WriteStartArray();foreach(double v in new[]{x,y,z})w.WriteNumberValue(Math.Round(v,5));w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("slots");foreach(var s in NativePierArt.Slots)w.WriteStringValue(s);w.WriteEndArray();
        w.WriteStartArray("spawns");foreach(var s in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points))){w.WriteStartArray();w.WriteNumberValue(s.pos.x);w.WriteNumberValue(s.pos.y-1.1);w.WriteNumberValue(s.pos.z);w.WriteEndArray();}w.WriteEndArray();
        w.WriteStartArray("columns");foreach(var p in map.pilotBlocks.Where(p=>!p.bots))w.WriteStringValue(p.id);w.WriteEndArray();
        w.WriteEndObject();
    }

    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Pier 9 art: "+why);}
        Check(NativeStageArt.BankOf("harbor")==NativePierArt.Bank&&NativePierArt.Bank=="ChipArenaStagePier","Pier 9's bank is named");
        var slots=NativePierArt.Slots;
        Check(slots.Length==23&&slots.Distinct().Count()==23&&slots[NativePierArt.Wheel]=="wheel"&&slots[NativePierArt.WheelBulbs+2]=="wheel-bulbs-2"&&slots[NativePierArt.Cabin+3]=="cabin-3"&&slots[NativePierArt.Table]=="table"&&slots[NativePierArt.Mount+1]=="mount-1"
            &&slots[NativePierArt.Star]=="star"&&slots[NativePierArt.Doll]=="doll"&&slots[NativePierArt.Boat]=="boat"&&slots[NativePierArt.Car]=="car"&&slots[NativePierArt.CarEnd]=="car-end"&&slots[NativePierArt.Wigwag]=="wigwag"
            &&slots[NativePierArt.LaneLamp]=="lane-lamp"&&slots[NativePierArt.Ring]=="ring"&&slots[NativePierArt.Beacon]=="beacon"&&slots[NativePierArt.Marquee+2]=="marquee-2","the rig slots");
        Check(NativePierArt.Casts(NativePierArt.Car)&&NativePierArt.Casts(NativePierArt.Ring)&&NativePierArt.Casts(NativePierArt.Mount)&&!NativePierArt.Casts(NativePierArt.Wheel)&&!NativePierArt.Casts(NativePierArt.LaneLamp)&&!NativePierArt.Casts(NativePierArt.Marquee),"the train, the gondolas and the mounts cast shadows; the far wheel and the lamps do not");

        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("harbor");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var looks=NativePierLooks.Resolve(NativeMapLooks.Find(set,"harbor").kitLooks);
        var rig=new NativePierRig(looks,map);var pose=new NativeStageArtPose();

        // ---- the poses: every model stands where the box rig's own part does, at every moment of two rounds ----
        var seen=new int[slots.Length];int most=0;double worst=0;int red=0,amber=0;double chaseLo=9,chaseHi=0;
        for(int round=0;round<2;round++)for(double t=0;t<84;t+=.05)
        {
            rig.Build(map.hazards,round,NativePhase.Fight,0,t,t);NativePierArt.Pose(rig,pose);most=Math.Max(most,pose.Count);
            for(int g=0;g<NativePierArt.Groups;g++){Check(pose.Slot[g]==NativePierArt.Marquee+g&&pose.Place[g*12+3]==0&&pose.Place[g*12+7]==0&&pose.Place[g*12+11]==0,"the marquees' groups draw in the world's frame");seen[NativePierArt.Marquee+g]++;chaseLo=Math.Min(chaseLo,pose.Tint[g*4]);chaseHi=Math.Max(chaseHi,pose.Tint[g*4]);}
            int j=NativePierArt.Groups;var per=new int[slots.Length];
            for(int i=0;i<rig.Parts.Count;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;Check(j<pose.Count&&pose.Slot[j]==slot,"draw "+j+" is part "+i+"'s slot at "+t);ref var p=ref rig.Parts.Items[i];int at=j*12;per[slot]++;seen[slot]++;
                worst=Math.Max(worst,Math.Abs(pose.Place[at+3]-p.X)+Math.Abs(pose.Place[at+7]-p.Y)+Math.Abs(pose.Place[at+11]-p.Z));
                Check(Math.Abs(Col(pose,j,0)-1)<1e-4&&Math.Abs(Col(pose,j,1)-1)<1e-4&&Math.Abs(Col(pose,j,2)-1)<1e-4,slots[slot]+" draws at its modelled size");
                var q=NativePierArt.Turn(p);q.Rotate(0,1,0,out double ux,out double uy,out double uz);worst=Math.Max(worst,Math.Abs(pose.Place[at+1]-ux)+Math.Abs(pose.Place[at+5]-uy)+Math.Abs(pose.Place[at+9]-uz));
                q.Rotate(1,0,0,out ux,out uy,out uz);worst=Math.Max(worst,Math.Abs(pose.Place[at]-ux)+Math.Abs(pose.Place[at+4]-uy)+Math.Abs(pose.Place[at+8]-uz));
                if(slot==NativePierArt.Wigwag||slot==NativePierArt.Beacon)Check(pose.Tint[j*4]>pose.Tint[j*4+1]*5,"a wig-wag and a beacon burn red");
                if(slot==NativePierArt.LaneLamp){if(pose.Tint[j*4+1]<pose.Tint[j*4]*.2)red++;else amber++;}
                j++;
                if(slot==NativePierArt.Wheel)for(int g=0;g<NativePierArt.Groups;g++,j++){Check(pose.Slot[j]==NativePierArt.WheelBulbs+g&&pose.Place[j*12+3]==pose.Place[at+3]&&pose.Place[j*12+5]==pose.Place[at+5],"the wheel's bulbs turn with the wheel");seen[NativePierArt.WheelBulbs+g]++;}
            }
            Check(j==pose.Count,"a draw for every tagged part and no other ("+pose.Count+" / "+j+") at "+t);
            Check(per[NativePierArt.Wheel]==1&&Enumerable.Range(0,4).Sum(k=>per[NativePierArt.Cabin+k])==NativePierLooks.Gondolas&&per[NativePierArt.Table]==1&&per[NativePierArt.Mount]+per[NativePierArt.Mount+1]==NativePierLooks.Mounts,"the wheel, its cabins, the turntable and its mounts at "+t);
            Check(per[NativePierArt.Star]==1&&per[NativePierArt.Doll]==NativePierLooks.Dolls&&per[NativePierArt.Boat]==4,"the star, the dolls and the boats");
            Check(per[NativePierArt.Car]==3&&per[NativePierArt.CarEnd]==2&&per[NativePierArt.Ring]==2&&per[NativePierArt.Beacon]==2&&per[NativePierArt.Wigwag]==rig.Signals*2&&per[NativePierArt.LaneLamp]==64,"the train, both gondolas and beacons, the wig-wags and the lane's lamps");
        }
        Check(worst<1e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.00000")+")");
        Check(seen.All(n=>n>0),"two rounds show every rig model ("+string.Join(",",slots.Where((s,i)=>seen[i]==0))+")");
        Check(red>0&&amber>red,"the lane's lamps run amber and burn red in the danger");
        Check(chaseLo>.25&&chaseHi>.95&&chaseLo<.4,"the marquees chase and never go dark ("+chaseLo.ToString("0.00")+".."+chaseHi.ToString("0.00")+")");
        Check(most>120&&most<NativeStageArtPose.Capacity-16,"a frame's rig draws fit the pose buffer (up to "+most+" of "+NativeStageArtPose.Capacity+")");
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativePierArt.Pose(rig,pose);int n0=pose.Count;var copy=pose.Place.Take(n0*12).ToArray();
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativePierArt.Pose(rig,pose);Check(pose.Count==n0&&pose.Place.Take(n0*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/30.0;rig.Build(map.hazards,1,NativePhase.Fight,0,t,t);NativePierArt.Pose(rig,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"harbor",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block and hazard");
            var pier=j.GetProperty("pier");Check(pier.GetProperty("slots").GetArrayLength()==slots.Length&&pier.GetProperty("mountAt").GetArrayLength()==NativePierLooks.Mounts&&pier.GetProperty("spawns").GetArrayLength()==16&&Math.Abs(pier.GetProperty("wheelR").GetDouble()-NativePierLooks.WheelR)<1e-9,"and the pier's numbers");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));var draw=R("Assets/ChipArena/Runtime/NativePierPresentation.cs");
        Check(draw.Contains("art.Prepare(map,NativePierArt.Slots,NativePierArt.Casts);")&&draw.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation loads and draws the art");
        Check(draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}"),"the box kit and rig draw whenever the art does not");
        Check(draw.Contains("if(ArtDrawn&&i==NativePierScenery.Near)continue;"),"the backdrop's near sector gives way to the modelled wheel frame, spikes and neck; the sea and the shore always draw");
        Check(R("Assets/ChipArena/Core/NativePierKit.cs").Contains("int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;Kind[n]=kind;}"),"the rig tags its parts beside the very add that builds them");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Pier 9 art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);long bytes=new FileInfo(bankPath).Length;
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            Check(sp.Blocks.Count==0,"no breakable models (every block is static)");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()==3),"every rig slot has its model in three tiers ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)||sp.Rig[s].Distinct().Count()!=3))+")");
            var near=sp.Static.Where(c=>int.TryParse(c.Id,out _)).ToList();var far=sp.Static.Where(c=>!int.TryParse(c.Id,out _)).ToList();
            Check(near.Count>=20&&near.All(c=>c.Props.Distinct().Count()==3),"the map's chunks, each in three tiers ("+near.Count+")");
            Check(far.Count>=3,"the near backdrop's chunks (the wheel's frame, the spikes, the neck: "+far.Count+")");
            Check(bytes<=20500000,"the bank stays within its budget of about 20 MB ("+(bytes/1e6).ToString("0.0")+" MB)");
            // the play space: within 2 m over the standable floor under it, the Fine art keeps to the blocks (within .25 m, the stage
            // rule: what looks like cover is cover); higher is overhead; a thin post (a lamp post on its planter, a carousel pole
            // on its base) within .9 m. Nothing stands in a spawn's capsule.
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            var floors=PierChecks.Floors(map);
            int proud=0,total=0;double farthest=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;long verts=0;
            foreach(var c in near)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    verts++;double floor=double.NegativeInfinity;foreach(var f in floors)if(v.X>=f.x0&&v.X<=f.x1&&v.Z>=f.z0&&v.Z<=f.z1&&f.top<=v.Y+.05&&f.top>floor)floor=f.top;
                    if(double.IsNegativeInfinity(floor)||v.Y-floor>2)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25+1e-3){proud++;var key=((int)Math.Floor(v.X))+","+(Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}farthest=Math.Max(farthest,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Pier 9 art: bank "+(bytes/1e6).ToString("0.00")+" MB, "+bank.Props.Count+" props, "+near.Count+" map chunks ("+verts+" Fine vertices) and "+far.Count+" backdrop; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+farthest.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Pier 9 art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
            Check(farthest<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+farthest.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS pier 9 art ("+count+" checks; rig draws up to "+most+" a frame)");
    }
    // The length of column c of draw i's place (its scale along that axis).
    private static double Col(NativeStageArtPose p,int i,int c){int o=i*12+c;return Math.Sqrt(p.Place[o]*p.Place[o]+p.Place[o+4]*p.Place[o+4]+p.Place[o+8]*p.Place[o+8]);}
    private static string Bank(string root)
    {
        const string name="ChipArenaLocal/Resources/ChipArenaStagePier.bytes";var dir=Environment.GetEnvironmentVariable("CHIPARENA_STAGE_BANKS");
        foreach(var p in new[]{dir==null?null:Path.Combine(dir,name),Path.Combine(root,"Assets/LocalAssets/"+name),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/"+name})
            if(p!=null&&File.Exists(p))return p;
        return null;
    }
}
