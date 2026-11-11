using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Breakwater's stage art (docs/arenas/STAGE-ART.md, docs/arenas/BREAKWATER.md "Stage art"): its bank's name, the art poses against
// the box rig at every moment (each model stands where the rig's own tagged part does), the layout export's rig numbers, the
// presentation's wiring and its bank-absent fallback; and, where the bank is on this machine (it is git-ignored), the bank itself
// against the map. `dotnet run --project Tests/CoreTests.csproj -c Release -- --breakwater-art` (also in the full run).
internal static class BreakwaterArtChecks
{
    /// <summary>The rig's numbers the Blender scripts share (tools/stages/breakwater_*.py), into the stage layout.</summary>
    public static void Layout(Utf8JsonWriter w,ContentMap map)
    {
        var rig=new NativeBreakwaterRig(NativeBreakwaterLooks.Resolve(null),map);
        void N(string k,double v)=>w.WriteNumber(k,Math.Round(v,5));
        w.WriteStartObject("rig");w.WriteStartArray("slots");foreach(var s in NativeBreakwaterArt.Slots)w.WriteStringValue(s);w.WriteEndArray();
        w.WriteStartArray("posts");
        for(int i=0;i<rig.Posts;i++){NativeBreakwaterRig.Bracket(rig.PostX(i),rig.PostTop(i),rig.PostZ(i),out double px,out double py,out double pz);w.WriteStartArray();foreach(double v in new[]{rig.PostX(i),rig.PostTop(i),rig.PostZ(i),px,py,pz})w.WriteNumberValue(Math.Round(v,5));w.WriteEndArray();}
        w.WriteEndArray();
        N("seaY",NativeBreakwaterLooks.SeaY);N("wallZ",NativeBreakwaterLooks.WallZ);N("quayZ",NativeBreakwaterLooks.QuayZ);N("lightX",NativeBreakwaterLooks.LightX);N("lightZ",NativeBreakwaterLooks.LightZ);N("lightFoot",NativeBreakwaterLooks.LightFoot);
        N("galleryY",NativeBreakwaterLooks.GalleryY);N("lanternY",NativeBreakwaterLooks.LanternY);N("lanternDrop",NativeBreakwaterRig.LanternDrop);N("bellX",NativeBreakwaterRig.BellX);N("bellY",NativeBreakwaterRig.BellY);N("bellZ",NativeBreakwaterRig.BellZ);
        N("bellDrop",NativeBreakwaterRig.BellDrop);N("gateFlagX",NativeBreakwaterRig.GateFlagX);N("gateFlagY",NativeBreakwaterRig.GateFlagY);N("gateFlagZ",NativeBreakwaterRig.GateFlagZ);N("turretFlagY",NativeBreakwaterRig.TurretFlagY);
        N("flagLength",NativeBreakwaterRig.FlagLength);N("flagHeight",NativeBreakwaterRig.FlagHeight);N("buoyX",NativeBreakwaterLooks.BuoyX);N("buoyZ",NativeBreakwaterLooks.BuoyZ);N("boatX",NativeBreakwaterLooks.BoatX);N("boatZ",NativeBreakwaterLooks.BoatZ);
        N("dinghyX",NativeBreakwaterLooks.DinghyX);N("dinghyZ",NativeBreakwaterLooks.DinghyZ);N("swellFar",NativeBreakwaterLooks.SwellFar);
        w.WriteEndObject();
    }

    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Breakwater art: "+why);}
        Check(NativeStageArt.BankOf("breakwater")=="ChipArenaStageBreakwater"&&NativeBreakwaterArt.Bank=="ChipArenaStageBreakwater","Breakwater's bank is named");
        var slots=NativeBreakwaterArt.Slots;
        Check(slots.Length==15&&slots.Distinct().Count()==15&&slots[NativeBreakwaterArt.Flag]=="flag"&&slots[NativeBreakwaterArt.Optic]=="optic"&&slots[NativeBreakwaterArt.WashFall]=="wash-fall"&&slots[NativeBreakwaterArt.GullWingR]=="gull-wing-r","the rig slots");
        Check(NativeBreakwaterArt.Casts(NativeBreakwaterArt.Bell)&&NativeBreakwaterArt.Casts(NativeBreakwaterArt.Boat)&&!NativeBreakwaterArt.Casts(NativeBreakwaterArt.Swell)&&!NativeBreakwaterArt.Casts(NativeBreakwaterArt.Wash)&&!NativeBreakwaterArt.Casts(NativeBreakwaterArt.Lantern)&&!NativeBreakwaterArt.Casts(NativeBreakwaterArt.Flag),"the bell and the boats cast shadows; the water, the lights and the cloth do not");

        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("breakwater");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var looks=NativeBreakwaterLooks.Resolve(NativeMapLooks.Find(set,"breakwater").kitLooks);
        var rig=new NativeBreakwaterRig(looks,map);var pose=new NativeStageArtPose();

        // ---- the poses: every model stands where the box rig's own part does, at every moment of two rounds ----
        var seen=new int[slots.Length];int most=0;double worst=0;int frames=0,flashes=0;
        for(int round=0;round<2;round++)for(double t=0;t<70;t+=.05)
        {
            rig.Build(map.hazards,round,NativePhase.Fight,0,t,t);NativeBreakwaterArt.Pose(rig,pose);frames++;most=Math.Max(most,pose.Count);
            int j=0;var per=new int[slots.Length];
            for(int i=0;i<rig.Parts.Count;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;Check(j<pose.Count&&pose.Slot[j]==slot,"draw "+j+" is part "+i+"'s slot at "+t);ref var p=ref rig.Parts.Items[i];int at=j*12;per[slot]++;seen[slot]++;
                worst=Math.Max(worst,Math.Abs(pose.Place[at+3]-p.X)+Math.Abs(pose.Place[at+7]-p.Y)+Math.Abs(pose.Place[at+11]-p.Z));
                double sx=Col(pose,j,0),sy=Col(pose,j,1),sz=Col(pose,j,2);var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};q.Rotate(0,1,0,out double ux,out double uy,out double uz);
                switch(NativeBreakwaterArt.Sizing(slot))
                {
                    case 0:Check(Math.Abs(sx-1)<1e-4&&Math.Abs(sy-1)<1e-4&&Math.Abs(sz-1)<1e-4,slots[slot]+" draws at its modelled size");worst=Math.Max(worst,Math.Abs(pose.Place[at+1]-ux)+Math.Abs(pose.Place[at+5]-uy)+Math.Abs(pose.Place[at+9]-uz));break;
                    case 1:Check(Math.Abs(sx-p.Hx)<1e-4&&Math.Abs(sy-p.Hy)<1e-4&&Math.Abs(sz-p.Hz)<1e-4,slots[slot]+" takes its part's half extents");break;
                    default:Check(Math.Abs(sx-rig.Level[i])<1e-4&&Math.Abs(sy-rig.Level[i])<1e-4&&Math.Abs(sz-rig.Level[i])<1e-4,slots[slot]+" takes its part's scale");break;
                }
                if(NativeBreakwaterArt.Lit(slot))Check(Math.Abs(pose.Tint[j*4]-rig.Level[i])<1e-5,slots[slot]+" glows by its level");
                if(slot==NativeBreakwaterArt.BuoyLamp&&rig.Level[i]>.5)flashes++;
                j++;
            }
            Check(j==pose.Count,"a draw for every tagged part and no other ("+pose.Count+" / "+j+") at "+t);
            Check(per[NativeBreakwaterArt.Optic]==1&&per[NativeBreakwaterArt.Bell]==1&&per[NativeBreakwaterArt.Buoy]==1&&per[NativeBreakwaterArt.BuoyLamp]==1&&per[NativeBreakwaterArt.Boat]==1&&per[NativeBreakwaterArt.Dinghy]==1,"the optic, the bell, the buoy and the boats at "+t);
            Check(per[NativeBreakwaterArt.Lantern]==rig.Posts&&per[NativeBreakwaterArt.GullBody]==NativeBreakwaterLooks.Gulls&&per[NativeBreakwaterArt.GullWingL]==NativeBreakwaterLooks.Gulls&&per[NativeBreakwaterArt.GullWingR]==NativeBreakwaterLooks.Gulls,"a lantern on every post, every gull and its wings at "+t);
            Check(per[NativeBreakwaterArt.Flag]==2*NativeBreakwaterRig.FlagLengths&&per[NativeBreakwaterArt.Swell]>=NativeBreakwaterLooks.Swells-2&&per[NativeBreakwaterArt.Swell]<=NativeBreakwaterLooks.Swells,"two flags of three lengths, the swells at "+t);
            int combers=0,washing=0,running=0;foreach(var h in map.hazards){NativeBreakwaterLooks.WashAt(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t),out double warn,out double wash,out _,out _);if(warn>0||wash>=0)combers++;if(wash>=0)running++;if(wash>.1)washing++;}
            Check(per[NativeBreakwaterArt.Comber]==combers&&(washing==0||per[NativeBreakwaterArt.Wash]>0)&&(running>0||per[NativeBreakwaterArt.Wash]+per[NativeBreakwaterArt.WashFall]==0),"a comber for every wash gathering or running, water on the mole only while one runs, at "+t);
        }
        Check(worst<1e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.00000")+")");
        Check(seen.All(n=>n>0),"two rounds show every rig model ("+string.Join(",",slots.Where((s,i)=>seen[i]==0))+")");
        Check(flashes>0&&flashes<frames/3,"the buoy's lamp flashes");
        Check(most>50&&most<NativeStageArtPose.Capacity-16,"a frame's rig draws fit the pose buffer (up to "+most+" of "+NativeStageArtPose.Capacity+")");
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativeBreakwaterArt.Pose(rig,pose);int n0=pose.Count;var copy=pose.Place.Take(n0*12).ToArray();
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativeBreakwaterArt.Pose(rig,pose);Check(pose.Count==n0&&pose.Place.Take(n0*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/30.0;rig.Build(map.hazards,1,NativePhase.Fight,0,t,t);NativeBreakwaterArt.Pose(rig,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"breakwater",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block and hazard");
            Check(j.GetProperty("rig").GetProperty("slots").GetArrayLength()==slots.Length&&j.GetProperty("rig").GetProperty("posts").GetArrayLength()==rig.Posts&&Math.Abs(j.GetProperty("rig").GetProperty("lanternY").GetDouble()-NativeBreakwaterLooks.LanternY)<1e-6,"and the rig's numbers");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));var draw=R("Assets/ChipArena/Runtime/NativeBreakwaterPresentation.cs");
        Check(draw.Contains("art.Prepare(map,NativeBreakwaterArt.Slots,NativeBreakwaterArt.Casts);")&&draw.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation loads and draws the art");
        Check(draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}"),"the box kit and rig draw whenever the art does not");
        Check(draw.Contains("if(!ArtDrawn)for(int i=0;i<sectors.Length;i++)Graphics.DrawMesh(sectors[i]"),"the box-built backdrop gives way to the modelled sea, lighthouse and shore");
        Check(R("Assets/ChipArena/Core/NativeBreakwaterKit.cs").Contains("int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;}"),"the rig tags its parts beside the very add that builds them");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Breakwater art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);long bytes=new FileInfo(bankPath).Length;
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            foreach(var b in map.blocks.Where(b=>b.role!="static"))Check(sp.Blocks.TryGetValue(b.id,out var props)&&props.All(p=>p>=0)&&props.Distinct().Count()==3,"breakable "+b.id+" has its model in three tiers");
            Check(sp.Blocks.Count==map.blocks.Count(b=>b.role!="static"),"a model for every breakable and no other block ("+sp.Blocks.Count+")");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()==3),"every rig slot has its model in three tiers ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            var near=sp.Static.Where(c=>int.TryParse(c.Id,out _)).ToList();var far=sp.Static.Where(c=>!int.TryParse(c.Id,out _)).ToList();
            // (a chunk that holds only the thinnest things, a staff or a vane, may lack its Low tier: the nearest tier stands in)
            Check(near.Count>=8&&near.Count(c=>c.Props.Distinct().Count()==3)>=near.Count-2,"the mole's chunks in three tiers ("+near.Count+")");
            Check(far.Count>=3&&far.All(c=>c.Props.Distinct().Count()==1),"the far chunks (the sea, the lighthouse, the shore), one tier each ("+far.Count+")");
            Check(bytes<=20500000,"the bank stays within its budget of about 20 MB ("+(bytes/1e6).ToString("0.0")+" MB)");
            // the reflection probe stands at the centre of the art's bounds (Runtime NativeStageArtDraw): over the deck, clear of every block
            var lo=new System.Numerics.Vector3(float.MaxValue);var hi=new System.Numerics.Vector3(float.MinValue);foreach(var c in sp.Static){lo=System.Numerics.Vector3.Min(lo,c.Min);hi=System.Numerics.Vector3.Max(hi,c.Max);}
            double px=(lo.X+hi.X)/2,pz=(lo.Z+hi.Z)/2,py=Math.Max(1,Math.Min(8,(lo.Y+hi.Y)/2));
            Check(Math.Abs(px)<1&&Math.Abs(pz)<5,"the art's bounds are centred on the map ("+px.ToString("0.0")+", "+pz.ToString("0.0")+")");
            Check(!map.blocks.Any(b=>Math.Abs(b.pos.x-px)<b.size.x/2+.4&&Math.Abs(b.pos.z-pz)<b.size.z/2+.4&&Math.Abs(b.pos.y-py)<b.size.y/2+.4),"the probe stands clear of every block ("+px.ToString("0.0")+", "+py.ToString("0.0")+", "+pz.ToString("0.0")+")");
            // the play space: within 2 m over the floor under it, the Fine art keeps to the blocks (within .25 m, the stage rule: what
            // looks like cover is cover); higher is overhead; a thin fitting (a mooring ring, a rope's end) within .9 m. Nothing
            // stands in a spawn's capsule.
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            // (a block nobody can stand on, under a pilot column for everyone, is no floor: the turret's belfry and the halls' roofs
            // stand on theirs)
            var columned=new HashSet<string>(map.pilotBlocks.Where(p=>!p.bots).Select(p=>p.id));
            var floors=map.blocks.Where(b=>!columned.Contains(b.id+"-top")).Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            var roofs=map.blocks.Where(b=>columned.Contains(b.id+"-top")).Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double farthest=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;long verts=0;
            foreach(var c in near)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    verts++;double floor=double.NegativeInfinity;foreach(var b in floors)if(v.X>=b.x0&&v.X<=b.x1&&v.Z>=b.z0&&v.Z<=b.z1&&b.y1<=v.Y+.05&&b.y1>floor)floor=b.y1;
                    if(double.IsNegativeInfinity(floor)||v.Y-floor>2)continue;bool over=false;foreach(var b in roofs)if(v.X>=b.x0-.35&&v.X<=b.x1+.35&&v.Z>=b.z0-.35&&v.Z<=b.z1+.35&&b.y1<=v.Y+.05&&b.y1>floor){over=true;break;}if(over)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25+1e-3){proud++;var key=((int)Math.Floor(v.X))+","+(Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}farthest=Math.Max(farthest,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Breakwater art: bank "+(bytes/1e6).ToString("0.00")+" MB, "+bank.Props.Count+" props, "+near.Count+" map chunks ("+verts+" Fine vertices) and "+far.Count+" far; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+farthest.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Breakwater art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
            Check(farthest<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+farthest.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS breakwater art ("+count+" checks; rig draws up to "+most+" a frame)");
    }
    // The length of column c of draw i's place (its scale along that axis).
    private static double Col(NativeStageArtPose p,int i,int c){int o=i*12+c;return Math.Sqrt(p.Place[o]*p.Place[o]+p.Place[o+4]*p.Place[o+4]+p.Place[o+8]*p.Place[o+8]);}
    private static string Bank(string root)
    {
        const string name="ChipArenaLocal/Resources/ChipArenaStageBreakwater.bytes";var dir=Environment.GetEnvironmentVariable("CHIPARENA_STAGE_BANKS");
        foreach(var p in new[]{dir==null?null:Path.Combine(dir,name),Path.Combine(root,"Assets/LocalAssets/"+name),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/"+name})
            if(p!=null&&File.Exists(p))return p;
        return null;
    }
}
