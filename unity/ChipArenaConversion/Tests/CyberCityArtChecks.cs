using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Cyber City's stage art (docs/arenas/CYBERCITY-ART.md, docs/arenas/STAGE-ART.md): the train's art pose against the kit's box
// train at every moment of two rounds, which kit parts and backdrop boxes the art replaces and which stay boxes (the driven
// parts: every telegraph), the pilot columns under the no-invisible-walls rule, the layout export, the runtime's wiring and its
// bank-absent fallback; and, where the bank is on this machine (it is git-ignored), the bank itself against the map.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --cybercity-art` (also in the full run).
internal static class CyberCityArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Cyber City art: "+why);}
        Check(NativeStageArt.BankOf("cybercity")=="ChipArenaStageCyberCity"&&NativeCyberCityArt.Bank=="ChipArenaStageCyberCity"&&NativeStageArt.BankOf("sanctum")=="ChipArenaStageSanctum","Cyber City's bank is named beside the Sanctum's");
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("cybercity");
        var hazards=map.hazards;var exits=NativeCyberCityExits.Build(map.blocks,map.nav,hazards);var kit=NativeCyberCityKit.Build(map.blocks,hazards,exits);var pose=new NativeStageArtPose();

        // ---- the slots ----
        var slots=NativeCyberCityArt.Slots;
        Check(slots.SequenceEqual(new[]{"train-nose","train-mid","train-tail"})&&Enumerable.Range(0,3).All(NativeCyberCityArt.Casts),"the rig slots: the train's three cars, all casting");
        Check(NativeCyberCityArt.Cars.SequenceEqual(new double[]{14,13,14})&&Math.Abs(NativeCyberCityArt.Cars.Sum()+2*NativeCyberCityArt.Gap-NativeCyberCityArt.Length)<1e-9&&Math.Abs(hazards[kit.Maglev].length-NativeCyberCityArt.Length)<1e-6,"the cars add up to the train the hazard sweeps");
        Check(NativeCyberCityArt.Front(0)==0&&NativeCyberCityArt.Front(1)==14.5&&NativeCyberCityArt.Front(2)==28,"each car's front end behind the nose");
        // every box of the kit's train lies in one car's span
        foreach(var p in kit.Train){int c=Car(p.X);Check(c>=0&&p.X-p.Hx>=-(NativeCyberCityArt.Front(c)+NativeCyberCityArt.Cars[c])-1e-3&&p.X+p.Hx<=-NativeCyberCityArt.Front(c)+1e-3&&Math.Abs(p.Z)+p.Hz<=2.5&&p.Y+p.Hy<=2.75,"a train box outside the cars ("+p.X+")");}

        // ---- the pose agrees with the box train at every moment ----
        var states=new NativeHazardState[hazards.Length];double worst=0;int seen=0,drawn=0,east=0,west=0,idle=0,partial=0;
        for(int round=0;round<2;round++)for(double t=0;t<70;t+=.05)
        {
            for(int i=0;i<hazards.Length;i++)states[i]=NativeCyberCityHazards.At(hazards[i],NativePhase.Fight,round,0,t);
            NativeCyberCityArt.Pose(hazards,states,kit.Maglev,pose);seen++;var s=states[kit.Maglev];
            if(s.Phase==NativeHazardPhase.Idle||double.IsNaN(s.Head)){Check(pose.Count==0,"nothing is posed while the line is idle");idle++;continue;}
            if(pose.Count>0){drawn++;if(s.Direction>0)east++;else west++;if(pose.Count<3)partial++;}
            var has=new bool[3];for(int i=0;i<pose.Count;i++){Check(pose.Slot[i]>=0&&pose.Slot[i]<3&&!has[pose.Slot[i]],"a car is posed once");has[pose.Slot[i]]=true;}
            float track=hazards[kit.Maglev].center.z;
            foreach(var p in kit.Train)
            {
                NativeCyberCityKit.TrainPlace(p,s.Head,s.Direction,track,out float x,out float y,out float z);int c=Car(p.X);
                int at=-1;for(int i=0;i<pose.Count;i++)if(pose.Slot[i]==c)at=i;
                if(at<0){Check(Math.Abs(s.Head-s.Direction*NativeCyberCityArt.Front(c))>NativeCyberCityArt.Reach,"a car within reach is posed");continue;}
                // the box's centre in the car's frame, through the pose, is where the kit puts the box
                At(pose,at,p.X+NativeCyberCityArt.Front(c),p.Y,p.Z,out double wx,out double wy,out double wz);
                worst=Math.Max(worst,Math.Sqrt((wx-x)*(wx-x)+(wy-y)*(wy-y)+(wz-z)*(wz-z)));
            }
        }
        Check(worst<2e-3,"every car stands where the kit's boxes do (worst "+worst.ToString("0.0000")+" m)");
        Check(drawn>100&&east>20&&west>20&&idle>seen/2,"the train is seen passing both ways ("+drawn+" of "+seen+" moments, "+east+" east, "+west+" west, "+idle+" idle)");
        NativeCyberCityArt.Pose(hazards,null,kit.Maglev,pose);Check(pose.Count==0,"no states, no train");NativeCyberCityArt.Pose(hazards,states,-1,pose);Check(pose.Count==0,"no maglev, no train");
        for(int i=0;i<hazards.Length;i++)states[i]=NativeCyberCityHazards.At(hazards[i],NativePhase.Fight,0,0,18.4);NativeCyberCityArt.Pose(hazards,states,kit.Maglev,pose);int n=pose.Count;var copy=pose.Place.Take(n*12).ToArray();
        NativeCyberCityArt.Pose(hazards,states,kit.Maglev,pose);Check(n==3&&pose.Count==n&&pose.Place.Take(n*12).SequenceEqual(copy),"the same moment poses the same three cars");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++)NativeCyberCityArt.Pose(hazards,states,kit.Maglev,pose);
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- what stays the kit's boxes: every driven part (the telegraphs), nothing steady ----
        int kept=0,steady=0;var drives=new HashSet<int>();
        foreach(var p in kit.Parts){bool k=NativeCyberCityArt.Kept(p);Check(k==(p.Drive!=NativeCyberCityKit.DriveSteady),"a part is kept exactly when it is driven");if(k){kept++;drives.Add(p.Drive);Check(NativeCyberCityArt.Modelled(map.blocks[p.Parent]),"a driven part rides a static block");}else steady++;}
        Check(kept>800&&steady>1800&&new[]{NativeCyberCityKit.DriveFlicker,NativeCyberCityKit.DriveCoil,NativeCyberCityKit.DriveEdge,NativeCyberCityKit.DriveGridBar,NativeCyberCityKit.DriveGridLamp,NativeCyberCityKit.DriveSafe,NativeCyberCityKit.DriveGridDeck,NativeCyberCityKit.DriveExit,NativeCyberCityKit.DriveExitChase}.All(d=>drives.Contains(d)),
            "the edge strips, coils, SAFE and EXIT lights, the couplings' rails, lamps and decks and the flickering signs stay boxes ("+kept+" kept, "+steady+" modelled)");
        Check(map.blocks.All(b=>NativeCyberCityArt.Modelled(b)==(b.role=="static"))&&map.blocks.Count(b=>!NativeCyberCityArt.Modelled(b))==34&&!NativeCyberCityArt.Modelled(null),"the static blocks are in the chunks; the 34 breakable and carried blocks have models");

        // ---- the backdrop: the near ring and the tunnels give way, the rest stays ----
        var back=NativeCyberCityScenery.Build();int near=0,tunnel=0;
        foreach(var b in back)
        {
            bool nr=NativeCyberCityArt.Near(b);if(!nr){Check(Math.Abs(b.X)+b.Hx>56.5||Math.Abs(b.Z)+b.Hz>48.5,"a box left to the backdrop lies beyond the near ring");continue;}
            near++;if(b.Y<-.3f&&b.Hz<5){tunnel++;Check(Math.Abs(b.X)>=40&&Math.Abs(b.X)-b.Hx>=39.9&&Math.Abs(b.Z)<4.3,"a tunnel box lies beyond a portal");}
            else Check(Math.Abs(b.X)-b.Hx>=NativeCyberCityScenery.InnerX-.6||Math.Abs(b.Z)-b.Hz>=NativeCyberCityScenery.InnerZ-.6,"a near-ring box stands behind the frontage");
            Check(b.Kind!=3&&b.Hy<13,"no tower, no beacon in the near ring");
        }
        Check(near>250&&near<420&&tunnel>50&&back.Length-near>3500,"the near ring and the tunnels ("+near+" boxes, "+tunnel+" of the tunnels) of "+back.Length);

        // ---- no invisible walls: the pilot columns left are the map's edge, the tunnel mouths and the spires ----
        var cols=map.pilotBlocks;
        Check(cols.Length==8&&cols.All(c=>!c.bots),"eight pilot columns, none for bots only (no lethal drop)");
        Check(cols.Count(c=>c.id.StartsWith("cybercity-frontage-top-"))==4&&cols.Count(c=>c.id.StartsWith("cybercity-tunnel-"))==2&&cols.Count(c=>c.id.StartsWith("cybercity-spire-top-"))==2,"the frontage's tops, the tunnel mouths, the spires' tops");
        foreach(var c in cols)
        {
            if(c.id.StartsWith("cybercity-spire-top-"))Check(map.blocks.Any(b=>b.id.EndsWith("-spire")&&Math.Abs(b.pos.x-c.center.x)<.01&&Math.Abs(b.pos.z-c.center.z)<.01&&Math.Abs(b.pos.y+b.size.y/2-map.bounds.ceiling)<.01),"a spire's column caps a block at the ceiling (its crown and mast stand on it)");
            else Check(Math.Abs(c.center.x)+c.size.x/2>=map.bounds.maxX-.01||Math.Abs(c.center.z)+c.size.z/2>=map.bounds.maxZ-.01,"an edge column lies on the map's bounds");
        }
        string gen=File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py"));
        Check(gen.Contains("EDGES['cybercity']=('cybercity-frontage-top','cybercity-tunnel-');OVERHANG['cybercity']=('cybercity-spire-top',)")&&gen.Contains("if name in EDGES:open_stage(m)"),"the generator keeps Cyber City's columns by the rule (open_stage: EDGES and OVERHANG)");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"cybercity",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("parts").GetArrayLength()==kit.Parts.Length&&j.GetProperty("hazards").GetArrayLength()==hazards.Length&&j.GetProperty("train").GetArrayLength()==kit.Train.Length,"the layout carries every block, hazard, kit part and train box");
            Check(j.GetProperty("backdrop").GetArrayLength()==back.Length&&j.GetProperty("backdrop").EnumerateArray().Count(b=>b.GetProperty("near").GetBoolean())==near&&j.GetProperty("exits").GetArrayLength()==exits.Exits.Length,"the backdrop with its near ring marked, the exits");
            Check(j.GetProperty("parts").EnumerateArray().Count(p=>p.GetProperty("drive").GetInt32()!=0)==kept&&j.GetProperty("parts").EnumerateArray().All(p=>map.blocks.Any(b=>b.id==p.GetProperty("o").GetString())&&p.GetProperty("look").GetString()!=null),"each part with its block, look and drive");
            var city=j.GetProperty("city");Check(Math.Abs(city.GetProperty("railTop").GetDouble()-NativeCyberCityKit.RailTop)<1e-6&&city.GetProperty("cars").GetArrayLength()==3&&Math.Abs(city.GetProperty("floorY").GetDouble()-exits.FloorY)<1e-6,"the train's and the Cut's shared numbers");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the runtime's wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var pres=R("Assets/ChipArena/Runtime/NativeCyberCityPresentation.cs");var art=R("Assets/ChipArena/Runtime/NativeCyberCityArtPresentation.cs");
        Check(art.Contains("metroArt.Prepare(map,NativeCyberCityArt.Slots,NativeCyberCityArt.Casts);")&&art.Contains("metroArtDrawn=metroArt.Render(arena,WorldEye,LowGraphics,metroPose);")&&pres.Contains("MetroArtPrepare(map);")&&pres.Contains("MetroArtRender(arena);DrawMetroKit(arena,now);"),"the presentation prepares the art with its kit (behind the loading screen) and renders it before the kit");
        Check(pres.Contains("if(prop==null||MetroArtHas(part,prop))continue;")&&art.Contains("if(!metroArtDrawn||NativeCyberCityArt.Kept(part)||")&&art.Contains("<=NativeStageArt.Keep;"),"under the art only the driven parts draw as boxes; a breakable's steady parts come back with its stub");
        Check(pres.Contains("if(!metroArtDrawn&&metroKit.Maglev>=0&&metroKit.Maglev<metroStates.Length)")&&pres.Contains("if(menu||!metroArtDrawn)foreach(var bt in metroNear)MetroDraw(bt,layer,ShadowCastingMode.Off);")&&pres.Contains("NativeCyberCityArt.Near(b)?metroNear:metroBack"),"the box train and the near ring draw whenever the art does not (and behind the menu)");
        Check(art.Contains("metroArtDrawn=false;if(metroArt==null||!metroArt.Ready||metroKit==null)return false;")&&!art.Contains("Collider")&&!art.Contains("Rigidbody")&&!art.Contains("new GameObject"),"without the bank nothing is drawn by the art; it makes no colliders or bodies");
        foreach(var f in new[]{"Assets/ChipArena/Core/NativeCyberCityArt.cs","Assets/ChipArena/Runtime/NativeCyberCityArtPresentation.cs"})Check(File.Exists(Path.Combine(root,f+".meta")),f+" has its meta");
        foreach(var f in new[]{"tools/stages/cybercity_art.py","tools/stages/cybercity_kit.py","tools/stages/cybercity_street.py","tools/stages/cybercity_city.py","tools/stages/cybercity_props.py"})Check(File.Exists(Path.Combine(root,f)),f+" is in the tools");
        Check(!R("Assets/ChipArena/Core/NativeCyberCityArt.cs").Contains("UnityEngine"),"the art pose is engine-free Core");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Cyber City art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);double mb=new FileInfo(bankPath).Length/1048576.0;
            Check(mb<=21,"the bank keeps to its budget ("+mb.ToString("0.0")+" MB)");
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var loose=map.blocks.Where(b=>b.role!="static").ToArray();
            Check(loose.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)&&sp.Blocks[b.id].Distinct().Count()==3),"every breakable and carried block has its model in three tiers");
            Check(sp.Blocks.Keys.All(k=>loose.Any(b=>b.id==k)),"no model for a static block");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()==3),"every car in every tier");
            Check(sp.Static.Count>=40&&sp.Static.All(c=>c.Props.All(p=>p>=0))&&sp.Static.Count(c=>c.Id.StartsWith("near-"))==6,"the static chunks ("+sp.Static.Count+"), the near ring's four sides and the two tunnels among them");
            // the cars reach as far as the kit's train (and no farther than a car's length and the body's width)
            for(int c=0;c<3;c++){var p=bank.Props[sp.Rig[slots[c]][0]];Check(p.Min.X>=-NativeCyberCityArt.Cars[c]-.05&&p.Max.X<=.05&&p.Min.Y>=0&&p.Max.Y<=2.95&&p.Min.Z>=-2.55&&p.Max.Z<=2.55,"car "+slots[c]+" keeps to its envelope");}
            // the play space: below 2 m over the floor under it (the highest floor block's top at or under the point: the street,
            // the Cut, the pits, the decks, the podiums' storeys and roofs, the stairs, the capsules' terrace) the Fine art keeps
            // to the blocks (within .25 m, the stage rule; a thin prop such as a lantern pole within .9 m); higher is overhead
            // (the kit's own rule: signs and lattices stand over 3 m); nothing stands in a spawn's capsule
            bool Floor(string id){string s=id.Substring("cybercity-".Length);return s.StartsWith("ground-")||s=="trench-floor"||s.StartsWith("trench-wall-")||s.StartsWith("pit-")&&!s.Contains("-rail")||s=="plaza-deck"||s.StartsWith("grid-")&&s.EndsWith("-deck")
                ||s=="station-deck"||s.StartsWith("station-landing")||s.Contains("-stair-")||s=="skybridge-ne"||s=="skybridge-sw"||s.Contains("-floor-")||s.Contains("-roof-")&&!s.Contains("-roof-unit-")||s.Contains("-box-");}
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2,floor:Floor(b.id))).ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            foreach(var c in sp.Static)
            {
                if(c.Id.StartsWith("near-"))continue;
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    if(Math.Abs(v.X)>map.bounds.maxX||Math.Abs(v.Z)>map.bounds.maxZ)continue;
                    double floor=double.MinValue,best=double.MaxValue;
                    foreach(var b in boxes)
                    {
                        if(b.floor&&v.X>=b.x0-.3&&v.X<=b.x1+.3&&v.Z>=b.z0-.3&&v.Z<=b.z1+.3&&b.y1<=v.Y+.05&&b.y1>floor)floor=b.y1;
                        double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));double d=dx*dx+dy*dy+dz*dz;if(d<best)best=d;
                    }
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                    if(floor==double.MinValue||v.Y-floor>2)continue;total++;best=Math.Sqrt(best);
                    if(best>.25){proud++;var key=((int)Math.Floor(v.X))+","+((int)Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                }
            }
            Console.WriteLine("Cyber City art: bank "+mb.ToString("0.00")+" MB, "+bank.Props.Count+" props, "+sp.Static.Count+" chunks; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>total/200)Console.WriteLine("Cyber City art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(30).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS Cyber City art ("+count+" checks)");
    }
    // The car a train-frame x lies in (0 nose .. 2 tail), or -1 in a gap.
    private static int Car(double x){for(int c=0;c<3;c++)if(x<=-NativeCyberCityArt.Front(c)+1e-6&&x>=-(NativeCyberCityArt.Front(c)+NativeCyberCityArt.Cars[c])-1e-6)return c;return -1;}
    // The point (lx, ly, lz) of draw i's frame in the world.
    private static void At(NativeStageArtPose p,int i,double lx,double ly,double lz,out double x,out double y,out double z)
    {int o=i*12;var m=p.Place;x=m[o]*lx+m[o+1]*ly+m[o+2]*lz+m[o+3];y=m[o+4]*lx+m[o+5]*ly+m[o+6]*lz+m[o+7];z=m[o+8]*lx+m[o+9]*ly+m[o+10]*lz+m[o+11];}
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageCyberCity.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageCyberCity.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
}
