using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Starfall Causeway's stage art (docs/arenas/STAGE-ART.md, docs/arenas/CAUSEWAY.md "Stage art"): its bank's name, the art poses
// against the box rig at every moment (each model stands where the rig's own tagged part does), the layout export's rig numbers,
// the presentation's wiring and its bank-absent fallback; and, where the bank is on this machine (it is git-ignored), the bank
// itself against the map. `dotnet run --project Tests/CoreTests.csproj -c Release -- --causeway-art` (also in the full run).
internal static class CausewayArtChecks
{
    /// <summary>The rig's numbers the Blender script shares (tools/stages/causeway_props.py), into the stage layout.</summary>
    public static void Layout(Utf8JsonWriter w)
    {
        w.WriteStartObject("rig");w.WriteStartArray("crownStones");
        for(int k=0;k<NativeCausewayRig.CrownStones;k++){NativeCausewayRig.CrownStone(1,k,0,out _,out _,out _,out double size,out _);w.WriteNumberValue(Math.Round(size,5));}
        w.WriteEndArray();w.WriteNumber("archStones",NativeCausewayRig.ArchStones);w.WriteStartArray("slots");foreach(var s in NativeCausewayArt.Slots)w.WriteStringValue(s);w.WriteEndArray();w.WriteEndObject();
    }

    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Causeway art: "+why);}
        Check(NativeStageArt.BankOf("causeway")=="ChipArenaStageCauseway","the Causeway's bank is named");
        var slots=NativeCausewayArt.Slots;
        Check(slots.Length==23&&slots.Distinct().Count()==23&&slots[NativeCausewayArt.Comet]=="comet"&&slots[NativeCausewayArt.Crown+6]=="crown-6"&&slots[NativeCausewayArt.Asteroid]=="asteroid-0"&&slots[NativeCausewayArt.VoussoirKey]=="voussoir-key","the rig slots");
        Check(NativeCausewayArt.Casts(NativeCausewayArt.Star)&&NativeCausewayArt.Casts(NativeCausewayArt.Crown+3)&&!NativeCausewayArt.Casts(NativeCausewayArt.Glyph)&&!NativeCausewayArt.Casts(NativeCausewayArt.Asteroid)&&!NativeCausewayArt.Casts(NativeCausewayArt.Banner),"the stones cast shadows; the lights, cloth and far rocks do not");

        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("causeway");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var looks=NativeCausewayLooks.Resolve(NativeMapLooks.Find(set,"causeway").kitLooks);
        var rig=new NativeCausewayRig(looks,map);var pose=new NativeStageArtPose();

        // ---- the poses: every model stands where the box rig's own part does, at every moment of two rounds ----
        var seen=new int[slots.Length];int most=0;double worst=0;int frames=0,lit=0,cold=0;
        var stars=map.hazards.Where(h=>h.kind==NativeFactoryHazards.Press).ToArray();
        for(int round=0;round<2;round++)for(double t=0;t<84;t+=.05)
        {
            rig.Build(map.hazards,round,NativePhase.Fight,0,t,t);NativeCausewayArt.Pose(rig,pose);frames++;most=Math.Max(most,pose.Count);
            int j=0;var per=new int[slots.Length];
            for(int i=0;i<rig.Parts.Count;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;Check(j<pose.Count&&pose.Slot[j]==slot,"draw "+j+" is part "+i+"'s slot at "+t);ref var p=ref rig.Parts.Items[i];int at=j*12;per[slot]++;seen[slot]++;
                worst=Math.Max(worst,Math.Abs(pose.Place[at+3]-p.X)+Math.Abs(pose.Place[at+7]-p.Y)+Math.Abs(pose.Place[at+11]-p.Z));
                double sx=Col(pose,j,0),sy=Col(pose,j,1),sz=Col(pose,j,2);
                switch(NativeCausewayArt.Sizing(slot))
                {
                    case 0:
                    {
                        Check(Math.Abs(sx-1)<1e-4&&Math.Abs(sy-1)<1e-4&&Math.Abs(sz-1)<1e-4,slots[slot]+" draws at its modelled size");
                        var q=NativeCausewayArt.Turn(p);q.Rotate(0,1,0,out double ux,out double uy,out double uz);
                        worst=Math.Max(worst,Math.Abs(pose.Place[at+1]-ux)+Math.Abs(pose.Place[at+5]-uy)+Math.Abs(pose.Place[at+9]-uz));break;
                    }
                    case 1:Check(Math.Abs(sx-p.Hx)<1e-4&&Math.Abs(sy-p.Hx)<1e-4&&Math.Abs(sz-p.Hx)<1e-4,slots[slot]+" takes its part's size");break;
                    default:Check(Math.Abs(sx-p.Hx)<1e-4&&Math.Abs(sy-p.Hy)<1e-4&&Math.Abs(sz-p.Hz)<1e-4,slots[slot]+" takes its part's extents");
                        if(!double.IsNaN(rig.Level[i]))Check(pose.Place[at+5]/sy>Math.Cos(NativeCausewayArt.CrystalLean)-1e-4,"a floating crystal stands upright");break;
                }
                if(slot==NativeCausewayArt.Glyph){Check(rig.Level[i]>.2&&pose.Tint[j*4]>.2,"only lit glyphs draw, in their light");lit++;if(pose.Tint[j*4+2]>pose.Tint[j*4])cold++;}
                j++;
            }
            Check(j==pose.Count,"a draw for every tagged part and no other ("+pose.Count+" / "+j+") at "+t);
            int falling=0;foreach(var h in stars){var s=NativeCausewayLooks.StarAt(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t));if(s.Fall>=0)falling++;}
            Check(per[NativeCausewayArt.Star]==falling,"a burning stone for every falling star at "+t);
            Check(per[NativeCausewayArt.Voussoir]==NativeCausewayRig.ArchStones-1&&per[NativeCausewayArt.VoussoirKey]==1,"the Shattered Arch's nine stones");
            int crowns=0;for(int k=0;k<NativeCausewayRig.CrownStones;k++){Check(per[NativeCausewayArt.Crown+k]==2,"both towers' ruin "+k);crowns+=per[NativeCausewayArt.Crown+k];}
            int rocks=0;for(int k=0;k<4;k++)rocks+=per[NativeCausewayArt.Asteroid+k];Check(rocks==16,"sixteen asteroids");
            Check(per[NativeCausewayArt.Banner]==rig.Pylons*3&&per[NativeCausewayArt.BannerHem]==rig.Pylons&&per[NativeCausewayArt.BannerStar]==rig.Pylons,"a banner of three lengths, its hem and its rune on every pylon");
            Check(per[NativeCausewayArt.Crystal]+per[NativeCausewayArt.CrystalViolet]==rig.Pylons+rig.Braziers*4+rig.Spires*2+1+4,"the floating crystals");
        }
        Check(worst<1e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.00000")+")");
        Check(seen.All(n=>n>0),"two rounds show every rig model ("+string.Join(",",slots.Where((s,i)=>seen[i]==0))+")");
        Check(lit>0&&cold>0&&cold<lit,"the lane's glyphs wake in amber and burn cold");
        Check(most>120&&most<NativeStageArtPose.Capacity-16,"a frame's rig draws fit the pose buffer (up to "+most+" of "+NativeStageArtPose.Capacity+")");
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativeCausewayArt.Pose(rig,pose);int n0=pose.Count;var copy=pose.Place.Take(n0*12).ToArray();
        rig.Build(map.hazards,0,NativePhase.Fight,0,37.3,37.3);NativeCausewayArt.Pose(rig,pose);Check(pose.Count==n0&&pose.Place.Take(n0*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/30.0;rig.Build(map.hazards,1,NativePhase.Fight,0,t,t);NativeCausewayArt.Pose(rig,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");
        var one=new NativeStageArtPose();one.AddScaled(3,1,2,3,FactoryQ.Axis(0,1,0,Math.PI/2),2,3,4,.5,.6,.7);
        Check(one.Count==1&&Math.Abs(Col(one,0,0)-2)<1e-5&&Math.Abs(Col(one,0,1)-3)<1e-5&&Math.Abs(Col(one,0,2)-4)<1e-5&&Math.Abs(one.Place[8]+2)<1e-5&&Math.Abs(one.Tint[1]-.6f)<1e-6,"a scaled draw scales the prop's own axes");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"causeway",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block and hazard");
            Check(j.GetProperty("rig").GetProperty("crownStones").GetArrayLength()==NativeCausewayRig.CrownStones&&j.GetProperty("rig").GetProperty("slots").GetArrayLength()==slots.Length,"and the rig's numbers");
            // the west half is modelled and turned half round for the east: the map must stay point symmetric
            foreach(var b in map.blocks)Check(map.blocks.Any(c=>Math.Abs(c.pos.x+b.pos.x)<1e-3&&Math.Abs(c.pos.z+b.pos.z)<1e-3&&Math.Abs(c.pos.y-b.pos.y)<1e-3&&Math.Abs(c.size.x-b.size.x)<1e-3&&Math.Abs(c.size.y-b.size.y)<1e-3&&Math.Abs(c.size.z-b.size.z)<1e-3),"block "+b.id+" has its twin across the centre");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));var draw=R("Assets/ChipArena/Runtime/NativeCausewayPresentation.cs");
        Check(draw.Contains("art.Prepare(map,NativeCausewayArt.Slots,NativeCausewayArt.Casts);")&&draw.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation loads and draws the art");
        Check(draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}"),"the box kit and rig draw whenever the art does not");
        Check(draw.Contains("if(ArtDrawn&&i<NativeCausewayScenery.Sectors)continue;"),"the box-built backdrop sectors give way to the modelled void; the planet and its rings always draw");
        Check(R("Assets/ChipArena/Core/NativeCausewayKit.cs").Contains("int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;Kind[n]=kind;}"),"the rig tags its parts beside the very add that builds them");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Causeway art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);long bytes=new FileInfo(bankPath).Length;
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            Check(sp.Blocks.Count==0,"no breakable models (every block is static)");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()==3),"every rig slot has its model in three tiers ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            var near=sp.Static.Where(c=>int.TryParse(c.Id,out _)).ToList();var far=sp.Static.Where(c=>!int.TryParse(c.Id,out _)).ToList();
            Check(near.Count>=20&&near.All(c=>c.Props.Distinct().Count()==3),"the map's chunks, each in three tiers ("+near.Count+")");
            Check(far.Count>=8&&far.All(c=>c.Props.Distinct().Count()==1),"the void's far chunks, one tier each ("+far.Count+")");
            Check(bytes<=21500000,"the bank stays within its budget of about 20 MB, and a megabyte for the floors cut along the chunks' cells ("+(bytes/1e6).ToString("0.0")+" MB)");
            // the reflection probe stands at the centre of the art's bounds (Runtime NativeStageArtDraw): on the centre line, clear of every block
            var lo=new System.Numerics.Vector3(float.MaxValue);var hi=new System.Numerics.Vector3(float.MinValue);foreach(var c in sp.Static){lo=System.Numerics.Vector3.Min(lo,c.Min);hi=System.Numerics.Vector3.Max(hi,c.Max);}
            double px=(lo.X+hi.X)/2,pz=(lo.Z+hi.Z)/2,py=Math.Max(1,Math.Min(8,(lo.Y+hi.Y)/2));
            Check(Math.Abs(px)<1&&Math.Abs(pz)<1,"the art's bounds are centred on the map ("+px.ToString("0.0")+", "+pz.ToString("0.0")+")");
            Check(!map.blocks.Any(b=>Math.Abs(b.pos.x-px)<b.size.x/2+.4&&Math.Abs(b.pos.z-pz)<b.size.z/2+.4&&Math.Abs(b.pos.y-py)<b.size.y/2+.4),"the probe stands clear of every block");
            // the play space: within 2 m over the floor under it, the Fine art keeps to the blocks (within .25 m, the stage rule: what
            // looks like cover is cover; small rubble lies on its floor); higher is overhead, and over the void nothing can be stood
            // beside; a thin finial (a gable's pinnacle) within .9 m. Nothing stands in a spawn's capsule.
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double farthest=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;long verts=0;
            foreach(var c in near)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    verts++;double floor=double.NegativeInfinity;foreach(var b in boxes)if(v.X>=b.x0&&v.X<=b.x1&&v.Z>=b.z0&&v.Z<=b.z1&&b.y1<=v.Y+.05&&b.y1>floor)floor=b.y1;
                    if(double.IsNegativeInfinity(floor)||v.Y-floor>2)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25+1e-3){proud++;var key=((int)Math.Floor(v.X))+","+(Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}farthest=Math.Max(farthest,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Causeway art: bank "+(bytes/1e6).ToString("0.00")+" MB, "+bank.Props.Count+" props, "+near.Count+" map chunks ("+verts+" Fine vertices) and "+far.Count+" far; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+farthest.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Causeway art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
            Check(farthest<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+farthest.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS causeway art ("+count+" checks; rig draws up to "+most+" a frame)");
    }
    // The length of column c of draw i's place (its scale along that axis).
    private static double Col(NativeStageArtPose p,int i,int c){int o=i*12+c;return Math.Sqrt(p.Place[o]*p.Place[o]+p.Place[o+4]*p.Place[o+4]+p.Place[o+8]*p.Place[o+8]);}
    private static string Bank(string root)
    {
        const string name="ChipArenaLocal/Resources/ChipArenaStageCauseway.bytes";var dir=Environment.GetEnvironmentVariable("CHIPARENA_STAGE_BANKS");
        foreach(var p in new[]{dir==null?null:Path.Combine(dir,name),Path.Combine(root,"Assets/LocalAssets/"+name),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/"+name})
            if(p!=null&&File.Exists(p))return p;
        return null;
    }
}
