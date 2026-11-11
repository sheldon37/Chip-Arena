using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Polaris Station's stage art (docs/arenas/STAGE-ART.md, docs/arenas/POLAR.md "Stage art"): its bank's name, the art poses against
// the box rig at every moment (each model stands where the rig's own tagged part does), the layout export's kit and rig numbers,
// the presentation's wiring and its bank-absent fallback; and, where the bank is on this machine (it is git-ignored), the bank
// itself against the map: every block covered, the breakables, the thin ice and every rig slot modelled in three tiers, the
// budget, the play-space rule and the spawns. `dotnet run --project Tests/CoreTests.csproj -c Release -- --polar-art` (also in
// the full run).
internal static class PolarArtChecks
{
    /// <summary>Polaris's part of the stage layout (Tests/StageArtLayout.cs): the box kit's parts (family, owner, role, kind) and
    /// the places the kit, the rig and the models share (Core NativePolarLooks, NativePolarRig, NativePolarArt), for
    /// tools/stages/polar_art.py.</summary>
    public static void Layout(Utf8JsonWriter w,ContentMap map,MapLook look)
    {
        void V(string name,double x,double y,double z){w.WriteStartArray(name);w.WriteNumberValue(Math.Round(x,5));w.WriteNumberValue(Math.Round(y,5));w.WriteNumberValue(Math.Round(z,5));w.WriteEndArray();}
        void N(string n,double v)=>w.WriteNumber(n,Math.Round(v,5));
        var looks=NativePolarLooks.Resolve(look?.kitLooks);var kit=NativePolarKit.Build(map,look);
        w.WriteStartArray("kinds");foreach(var k in NativePolarLooks.Kinds)w.WriteStringValue(k);w.WriteEndArray();
        w.WriteStartArray("kindColors");for(int k=0;k<NativePolarLooks.Kinds.Length;k++)w.WriteNumberValue(looks.Color[k]);w.WriteEndArray();
        w.WriteStartArray("parts");
        for(int i=0;i<kit.Parts.Count;i++)
        {
            ref var p=ref kit.Parts.Items[i];w.WriteStartObject();w.WriteString("f",kit.Family[i]);if(kit.Owner[i]!=null)w.WriteString("o",kit.Owner[i]);w.WriteNumber("r",kit.Role[i]);w.WriteNumber("k",kit.Kind[i]);
            V("c",p.X,p.Y,p.Z);V("h",p.Hx,p.Hy,p.Hz);w.WriteNumber("col",p.Color);w.WriteNumber("w",Math.Round(p.Window,3));w.WriteEndObject();
        }
        w.WriteEndArray();
        w.WriteStartObject("polar");
        N("sea",NativePolarLooks.Sea);N("shelfZ",NativePolarLooks.ShelfZ);N("shelfX",NativePolarLooks.ShelfX);N("shelfN",NativePolarLooks.ShelfN);N("channelHalf",NativePolarLooks.ChannelHalf);N("channelN",NativePolarLooks.ChannelN);N("channelS",NativePolarLooks.ChannelS);
        V("dish",NativePolarLooks.DishX,NativePolarLooks.DishY,NativePolarLooks.DishZ);N("dishRadius",NativePolarLooks.DishRadius);N("dishDepth",NativePolarLooks.DishDepth);N("dishFeed",NativePolarLooks.DishFeed);N("dishBack",NativePolarLooks.DishBack);
        N("dishLowest",NativePolarLooks.DishLowest());N("yokeDrop",2.65);N("petals",NativePolarRig.Petals);
        V("lamp",NativePolarLooks.LampX,NativePolarLooks.LampY,NativePolarLooks.LampZ);N("balloonRadius",NativePolarLooks.BalloonRadius);N("sondeDrop",NativePolarLooks.SondeDrop);N("floes",NativePolarRig.Floes);
        w.WriteStartArray("slots");foreach(var s in NativePolarArt.Slots)w.WriteStringValue(s);w.WriteEndArray();
        w.WriteStartArray("sheets");
        foreach(var s in map.segments){w.WriteStartObject();w.WriteString("id",s.id);w.WriteString("seam",s.supports[0].member);w.WriteStartArray("plates");foreach(var b in s.blocks)w.WriteStringValue(b);w.WriteEndArray();V("center",s.crush.center.x,0,s.crush.center.z);V("size",s.crush.size.x,0,s.crush.size.z);w.WriteEndObject();}
        w.WriteEndArray();
        w.WriteStartArray("spawns");foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points))){w.WriteStartArray();w.WriteNumberValue(p.pos.x);w.WriteNumberValue(p.pos.y);w.WriteNumberValue(p.pos.z);w.WriteEndArray();}w.WriteEndArray();
        w.WriteEndObject();
    }

    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Polaris art: "+why);}
        Check(NativeStageArt.BankOf("polar")=="ChipArenaStagePolar","Polaris's bank is named");
        var slots=NativePolarArt.Slots;
        Check(slots.Length==8&&slots.Distinct().Count()==8&&slots[NativePolarArt.Dish]=="dish"&&slots[NativePolarArt.Yoke]=="yoke"&&slots[NativePolarArt.Floe]=="floe"&&slots[NativePolarArt.VentLamp]=="vent-lamp"&&slots[NativePolarArt.Cups]=="cups","the rig slots");
        Check(NativePolarArt.Casts(NativePolarArt.Dish)&&NativePolarArt.Casts(NativePolarArt.Yoke)&&NativePolarArt.Casts(NativePolarArt.Balloon)&&!NativePolarArt.Casts(NativePolarArt.VentLamp)&&!NativePolarArt.Casts(NativePolarArt.Floe),"the dish, its yoke and the balloon cast shadows; the lamps and floes do not");
        NativePolarArt.LampLight(0,out double r0,out _,out _);NativePolarArt.LampLight(.6,out double r1,out double g1,out double b1);NativePolarArt.LampLight(2,out double r2,out double g2,out double b2);
        Check(r0<.1&&r1>1&&g1<r1*.5&&b1<g1&&r2>3&&b2>r2*.7,"a vent lamp is dark when idle, amber through the warning, white-hot through the burst");

        var map=PolarChecks.Load(root,out _,out _);var o=new JsonSerializerOptions{IncludeFields=true};
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,"polar");var looks=NativePolarLooks.Resolve(look.kitLooks);
        var rig=new NativePolarRig(looks,map);var pose=new NativeStageArtPose();int[] st=new int[8];float[] t0=new float[8];

        // ---- the poses: every model stands where the box rig's own part does, at every moment of two rounds ----
        var seen=new int[slots.Length];int most=0;double worst=0;int masts=map.blocks.Count(b=>b.id.StartsWith("polar-mast-",StringComparison.Ordinal)),vents=map.hazards.Length;
        for(int round=0;round<2;round++)for(double t=0;t<120;t+=.05)
        {
            // the second round breaks two sheets: one cracks at 30 s and is gone, another at 70 s
            st[0]=round==1&&t>31.4?(t>33.1?3:2):round==1&&t>30?1:0;t0[0]=31.4f;st[3]=round==1&&t>71.4?(t>73.1?3:2):round==1&&t>70?1:0;t0[3]=71.4f;
            rig.Build(map,st,t0,round,NativePhase.Fight,0,t);NativePolarArt.Pose(rig,pose);most=Math.Max(most,pose.Count);int j=0;var per=new int[slots.Length];
            for(int i=0;i<rig.Parts.Count;i++)
            {
                int slot=rig.Slot[i];if(slot<0)continue;Check(j<pose.Count&&pose.Slot[j]==slot,"draw "+j+" is part "+i+"'s slot at "+t);ref var p=ref rig.Parts.Items[i];int at=j*12;per[slot]++;seen[slot]++;
                worst=Math.Max(worst,Math.Abs(pose.Place[at+3]-p.X)+Math.Abs(pose.Place[at+7]-p.Y)+Math.Abs(pose.Place[at+11]-p.Z));
                double sx=Col(pose,j,0),sy=Col(pose,j,1),sz=Col(pose,j,2);
                if(slot==NativePolarArt.Balloon)Check(Math.Abs(sx-rig.Level[i])<1e-4&&Math.Abs(sy-sx)<1e-4&&Math.Abs(sz-sx)<1e-4&&sx>=1&&sx<1.6,"the balloon swells as it climbs");
                else if(slot==NativePolarArt.Floe)Check(Math.Abs(sx-p.Hx)<1e-4&&Math.Abs(sy-1)<1e-4&&Math.Abs(sz-p.Hz)<1e-4&&p.Y+p.Hy<NativePolarKit.BelowY,"a floe takes its part's size, in the water");
                else
                {
                    Check(Math.Abs(sx-1)<1e-4&&Math.Abs(sy-1)<1e-4&&Math.Abs(sz-1)<1e-4,slots[slot]+" draws at its modelled size");
                    var q=NativePolarArt.Turn(p);q.Rotate(0,0,1,out double ux,out double uy,out double uz);worst=Math.Max(worst,Math.Abs(pose.Place[at+2]-ux)+Math.Abs(pose.Place[at+6]-uy)+Math.Abs(pose.Place[at+10]-uz));
                }
                if(slot==NativePolarArt.Dish)
                {
                    // the model's boresight (+z) points where the looks say, and its bearing sits on the pedestal's axis
                    double az=NativePolarLooks.DishAzimuth(t),el=NativePolarLooks.DishElevation(t);
                    Check(Math.Abs(pose.Place[at+2]-Math.Sin(az)*Math.Cos(el))<1e-4&&Math.Abs(pose.Place[at+6]-Math.Sin(el))<1e-4&&Math.Abs(pose.Place[at+10]-Math.Cos(az)*Math.Cos(el))<1e-4,"the dish looks along its azimuth and elevation at "+t);
                    Check(Math.Abs(p.X-NativePolarLooks.DishX)<1e-4&&Math.Abs(p.Y-NativePolarLooks.DishY)<1e-4&&Math.Abs(p.Z-NativePolarLooks.DishZ)<1e-4,"the dish tips about its bearing");
                }
                if(slot==NativePolarArt.Yoke)Check(Math.Abs(pose.Place[at+5]-1)<1e-5&&Math.Abs(pose.Place[at+7]-(NativePolarLooks.DishY-2.65))<1e-4,"the yoke turns upright on the pedestal");
                if(slot==NativePolarArt.VentLamp){NativePolarArt.LampLight(rig.Level[i],out double lr,out double lg,out double lb);Check(Math.Abs(pose.Tint[j*4]-lr)<1e-5&&Math.Abs(pose.Tint[j*4+1]-lg)<1e-5&&Math.Abs(pose.Tint[j*4+2]-lb)<1e-5,"a vent lamp takes its heat's light");}
                j++;
            }
            Check(j==pose.Count,"a draw for every tagged part and no other ("+pose.Count+" / "+j+") at "+t);
            bool up=NativePolarLooks.BalloonAt(t,out _,out _,out _,out _,out _);
            Check(per[NativePolarArt.Dish]==1&&per[NativePolarArt.Yoke]==1&&per[NativePolarArt.Searchlight]==1&&per[NativePolarArt.Cups]==masts&&per[NativePolarArt.VentLamp]==vents*4,"the dish, its yoke, the searchlight, an anemometer a mast and four lamps a vent at "+t);
            Check(per[NativePolarArt.Balloon]==(up?1:0)&&per[NativePolarArt.Sonde]==(up?1:0),"the balloon and its sonde while one is up at "+t);
            int open=(st[0]>=2&&rig.Sink[0]>=.6?1:0)+(st[3]>=2&&rig.Sink[3]>=.6?1:0);Check(per[NativePolarArt.Floe]==open*NativePolarRig.Floes,"floes where the ice has gone ("+per[NativePolarArt.Floe]+") at "+t);
        }
        Check(worst<1e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.00000")+")");
        Check(seen.All(n=>n>0),"two rounds show every rig model ("+string.Join(",",slots.Where((s,i)=>seen[i]==0))+")");
        Check(masts==2&&most>=22&&most<NativeStageArtPose.Capacity-16,"a frame's rig draws fit the pose buffer (up to "+most+" of "+NativeStageArtPose.Capacity+")");
        st[0]=st[3]=0;rig.Build(map,st,t0,0,NativePhase.Fight,0,37.3);NativePolarArt.Pose(rig,pose);int n0=pose.Count;var copy=pose.Place.Take(n0*12).ToArray();
        rig.Build(map,st,t0,0,NativePhase.Fight,0,37.3);NativePolarArt.Pose(rig,pose);Check(pose.Count==n0&&pose.Place.Take(n0*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=20+f/30.0;rig.Build(map,st,t0,1,NativePhase.Fight,0,t);NativePolarArt.Pose(rig,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"polar",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block and hazard");
            var pl=j.GetProperty("polar");Check(pl.GetProperty("slots").GetArrayLength()==slots.Length&&pl.GetProperty("sheets").GetArrayLength()==map.segments.Length&&pl.GetProperty("spawns").GetArrayLength()==16&&pl.GetProperty("dishRadius").GetDouble()==NativePolarLooks.DishRadius,"and the rig's numbers");
            Check(j.GetProperty("parts").GetArrayLength()==NativePolarKit.Build(map,look).Count&&j.GetProperty("kinds").GetArrayLength()==NativePolarLooks.Kinds.Length,"and the kit's parts");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));var draw=R("Assets/ChipArena/Runtime/NativePolarPresentation.cs");
        Check(draw.Contains("art.Prepare(map,NativePolarArt.Slots,NativePolarArt.Casts);")&&draw.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation loads and draws the art");
        Check(draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}"),"the box kit and rig draw whenever the art does not");
        Check(R("Assets/ChipArena/Core/NativePolarKit.cs").Contains("int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow);if(Parts.Count>n){Role[n]=roleNow;Slot[n]=slotNow;Level[n]=levelNow;slotNow=-1;}"),"the rig tags its parts beside the very add that builds them");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Polaris art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);long bytes=new FileInfo(bankPath).Length;
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var moving=map.blocks.Where(b=>b.role!="static").ToList();
            foreach(var b in moving)Check(sp.Blocks.TryGetValue(b.id,out var tiers)&&tiers.All(p=>p>=0),"a model for the breakable or sinking block "+b.id);
            Check(sp.Blocks.Count==moving.Count,"block models only for the blocks that move or break ("+sp.Blocks.Count+" / "+moving.Count+")");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)),"every rig slot has its model ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            Check(new[]{"dish","yoke","balloon","searchlight"}.All(s=>sp.Rig[s].Distinct().Count()==3),"the dish, its yoke, the balloon and the searchlight in three tiers");
            var near=sp.Static.Where(c=>int.TryParse(c.Id,out _)).ToList();var far=sp.Static.Where(c=>!int.TryParse(c.Id,out _)).ToList();
            Check(near.Count>=20&&near.All(c=>c.Props.Distinct().Count()>=2),"the map's chunks in their tiers ("+near.Count+")");
            Check(far.Count>=3&&far.All(c=>c.Props.Distinct().Count()==1),"the backdrop's far chunks, one tier each ("+far.Count+")");
            Check(bytes<=20500000,"the bank stays within its budget of about 20 MB ("+(bytes/1e6).ToString("0.0")+" MB)");
            var lo=new System.Numerics.Vector3(float.MaxValue);var hi=new System.Numerics.Vector3(float.MinValue);foreach(var c in sp.Static){lo=System.Numerics.Vector3.Min(lo,c.Min);hi=System.Numerics.Vector3.Max(hi,c.Max);}
            double px=(lo.X+hi.X)/2,pz=(lo.Z+hi.Z)/2,py=Math.Max(1,Math.Min(8,(lo.Y+hi.Y)/2));
            Check(Math.Abs(px)<3&&Math.Abs(pz)<12,"the art's bounds are centred on the field ("+px.ToString("0.0")+", "+pz.ToString("0.0")+")");
            Check(!map.blocks.Any(b=>Math.Abs(b.pos.x-px)<b.size.x/2+.4&&Math.Abs(b.pos.z-pz)<b.size.z/2+.4&&Math.Abs(b.pos.y-py)<b.size.y/2+.4),"the reflection probe stands clear of every block");
            // the play space: within 2 m over the floor under it, the Fine art keeps to the blocks (within .25 m, the stage rule: what
            // looks like cover is cover); higher is overhead; under the shelf (the sea, the channel's walls) nothing can be stood on.
            // Nothing stands in a spawn's capsule.
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double farthest=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;long verts=0;
            foreach(var c in near)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    verts++;if(v.Y<NativePolarKit.BelowY)continue;double floor=double.NegativeInfinity;foreach(var b in boxes)if(v.X>=b.x0&&v.X<=b.x1&&v.Z>=b.z0&&v.Z<=b.z1&&b.y1<=v.Y+.05&&b.y1>floor)floor=b.y1;
                    if(double.IsNegativeInfinity(floor)||v.Y-floor>2)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25+1e-3){proud++;var key=((int)Math.Floor(v.X))+","+(Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}farthest=Math.Max(farthest,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Polaris art: bank "+(bytes/1e6).ToString("0.00")+" MB, "+bank.Props.Count+" props, "+near.Count+" map chunks ("+verts+" Fine vertices) and "+far.Count+" far; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+farthest.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Polaris art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
            Check(farthest<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+farthest.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS polar art ("+count+" checks; rig draws up to "+most+" a frame)");
    }
    // The length of column c of draw i's place (its scale along that axis).
    private static double Col(NativeStageArtPose p,int i,int c){int o=i*12+c;return Math.Sqrt(p.Place[o]*p.Place[o]+p.Place[o+4]*p.Place[o+4]+p.Place[o+8]*p.Place[o+8]);}
    internal static string Bank(string root)
    {
        const string name="ChipArenaLocal/Resources/ChipArenaStagePolar.bytes";var dir=Environment.GetEnvironmentVariable("CHIPARENA_STAGE_BANKS");
        foreach(var p in new[]{dir==null?null:Path.Combine(dir,name),Path.Combine(root,"Assets/LocalAssets/"+name),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/"+name})
            if(p!=null&&File.Exists(p))return p;
        return null;
    }
}
