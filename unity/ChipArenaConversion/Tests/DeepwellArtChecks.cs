#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Deepwell's stage art (docs/arenas/DEEPWELL.md "Stage art", docs/arenas/STAGE-ART.md; Core NativeDeepwellArt, tools/stages/rig_*.py,
    // the bank ChipArenaStageDeepwell): the slots; at every 0.05 s of two rounds every draw is its tagged box part's (place, turn, size),
    // one ram and its rods, the klaxons, the core, four grates, two whales, every jellyfish, fish and kelp joint, the drone; the lit
    // props take their light; deterministic and allocation free; the layout export; the wiring and the bank-absent fallback; and,
    // where the bank is on the machine: every static block covered, every supply case modelled in three tiers, every rig slot in
    // three tiers, the chunks' tiers, the budget, the play-space rule (within 2 m over a floor a pilot may stand on, the art keeps
    // within .25 m of a block) and the spawns.
    internal static class DeepwellArtChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Deepwell art: "+what);count++;}
        public static void Run(string root)
        {
            count=0;var slots=NativeDeepwellArt.Slots;
            Check(slots.Length==14&&slots.Distinct().Count()==slots.Length&&slots[NativeDeepwellArt.Ram]=="ram"&&slots[NativeDeepwellArt.RamRod]=="ram-rod"&&slots[NativeDeepwellArt.Kelp]=="kelp"&&slots[NativeDeepwellArt.Drone]=="drone"&&slots[NativeDeepwellArt.WhaleFinR]=="whale-fin-r","the rig slots");
            Check(NativeDeepwellArt.Casts(NativeDeepwellArt.Ram)&&!NativeDeepwellArt.Casts(NativeDeepwellArt.WhaleBody)&&!NativeDeepwellArt.Casts(NativeDeepwellArt.JellyBell)&&!NativeDeepwellArt.Casts(NativeDeepwellArt.Fish),"the ram casts its shadow; the sea's creatures do not");
            Check(NativeStageArt.BankOf(NativeDeepwellLooks.MapId)==NativeDeepwellArt.Bank&&NativeDeepwellArt.Bank=="ChipArenaStageDeepwell","the bank's name");
            var map=DeepwellChecks.Map(root,out _,out var content);map=content.Map("rig");
            var set=MapLookChecks.Load(root);var looks=NativeDeepwellLooks.Resolve(NativeMapLooks.Find(set,"rig").kitLooks);
            var rig=new NativeDeepwellRig(looks,map);var pose=new NativeStageArtPose();

            // ---- the poses: every model stands where the box rig's own part does, at every moment of two rounds ----
            var seen=new int[slots.Length];int most=0;double worst=0;int rods=0,lit=0;double klaxon=0;
            for(int round=0;round<2;round++)for(double t=0;t<44;t+=.05)
            {
                rig.Build(map.hazards,round,NativePhase.Fight,0,t,t);NativeDeepwellArt.Pose(rig,pose);most=Math.Max(most,pose.Count);
                int j=0;var per=new int[slots.Length];
                for(int i=0;i<rig.Parts.Count;i++)
                {
                    int slot=rig.Slot[i];if(slot<0)continue;Check(j<pose.Count&&pose.Slot[j]==slot,"draw "+j+" is part "+i+"'s slot at "+t);ref var p=ref rig.Parts.Items[i];int at=j*12;per[slot]++;seen[slot]++;
                    worst=Math.Max(worst,Math.Abs(pose.Place[at+3]-p.X)+Math.Abs(pose.Place[at+7]-p.Y)+Math.Abs(pose.Place[at+11]-p.Z));
                    double sx=Col(pose,j,0),sy=Col(pose,j,1),sz=Col(pose,j,2),l=rig.Level[i];
                    switch(NativeDeepwellArt.Sizing(slot))
                    {
                        case 0:
                        {
                            Check(Math.Abs(sx-1)<1e-4&&Math.Abs(sy-1)<1e-4&&Math.Abs(sz-1)<1e-4,slots[slot]+" draws at its modelled size");
                            var q=NativeDeepwellArt.Turn(p);q.Rotate(0,1,0,out double ux,out double uy,out double uz);
                            worst=Math.Max(worst,Math.Abs(pose.Place[at+1]-ux)+Math.Abs(pose.Place[at+5]-uy)+Math.Abs(pose.Place[at+9]-uz));break;
                        }
                        case 1:Check(l>0&&Math.Abs(sx-l)<1e-4&&Math.Abs(sy-l)<1e-4&&Math.Abs(sz-l)<1e-4,slots[slot]+" takes its part's scale");break;
                        case 2:Check(Math.Abs(sx-p.Hx)<1e-4&&Math.Abs(sy-p.Hy)<1e-4&&Math.Abs(sz-p.Hz)<1e-4,slots[slot]+" takes its part's extents");break;
                        case 3:Check(l>0&&Math.Abs(sx-l)<1e-4&&Math.Abs(sy-p.Hy)<1e-4&&Math.Abs(sz-l)<1e-4,slots[slot]+" takes its radius and half height");break;
                        default:Check(Math.Abs(sx-1)<1e-4&&Math.Abs(sy-p.Hy)<1e-4&&Math.Abs(sz-1)<1e-4,slots[slot]+" stretches along y alone");break;
                    }
                    if(NativeDeepwellArt.Lit(slot)){Check(pose.Tint[j*4]>=0&&pose.Tint[j*4+1]>=0&&pose.Tint[j*4+2]>=0&&Math.Abs(pose.Tint[j*4]-rig.Tint[i*3])<1e-6,"a lit prop takes its part's light");lit++;if(slot==NativeDeepwellArt.Klaxon)klaxon=Math.Max(klaxon,pose.Tint[j*4]);}
                    else Check(pose.Tint[j*4]==1&&pose.Tint[j*4+1]==1&&pose.Tint[j*4+2]==1,"an unlit prop keeps its own glow");
                    j++;
                }
                Check(j==pose.Count,"a draw for every tagged part and no other ("+pose.Count+" / "+j+") at "+t);
                Check(per[NativeDeepwellArt.Ram]==1&&per[NativeDeepwellArt.Klaxon]==4&&per[NativeDeepwellArt.Core]==1&&per[NativeDeepwellArt.Grate]==4,"the ram, four klaxons, the core and four grates at "+t);
                Check(per[NativeDeepwellArt.RamRod]==0||per[NativeDeepwellArt.RamRod]==2,"the ram's two rods");rods+=per[NativeDeepwellArt.RamRod];
                Check(per[NativeDeepwellArt.WhaleBody]==NativeDeepwellLooks.Whales&&per[NativeDeepwellArt.WhaleTail]==NativeDeepwellLooks.Whales&&per[NativeDeepwellArt.WhaleFinL]==NativeDeepwellLooks.Whales&&per[NativeDeepwellArt.WhaleFinR]==NativeDeepwellLooks.Whales,"the cow and her calf");
                Check(per[NativeDeepwellArt.JellyBell]==NativeDeepwellLooks.Jellies&&per[NativeDeepwellArt.JellyArms]==NativeDeepwellLooks.Jellies,"every jellyfish's bell and arms");
                Check(per[NativeDeepwellArt.Fish]==NativeDeepwellLooks.Schools*NativeDeepwellLooks.SchoolFish&&per[NativeDeepwellArt.Kelp]==NativeDeepwellLooks.KelpStalks*NativeDeepwellLooks.KelpJoints&&per[NativeDeepwellArt.Drone]==1,"every fish, every kelp joint, the drone");
            }
            Check(worst<1e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.00000")+")");
            Check(seen.All(n=>n>0)&&rods>0,"two rounds show every rig model ("+string.Join(",",slots.Where((s,i)=>seen[i]==0))+")");
            Check(klaxon>2&&lit>0,"the klaxons burn through the telegraph ("+klaxon.ToString("0.0")+")");
            Check(most>250&&most<NativeStageArtPose.Capacity-16,"a frame's rig draws fit the pose buffer (up to "+most+" of "+NativeStageArtPose.Capacity+")");
            rig.Build(map.hazards,0,NativePhase.Fight,0,17.3,17.3);NativeDeepwellArt.Pose(rig,pose);int n0=pose.Count;var copy=pose.Place.Take(n0*12).ToArray();
            rig.Build(map.hazards,0,NativePhase.Fight,0,17.3,17.3);NativeDeepwellArt.Pose(rig,pose);Check(pose.Count==n0&&pose.Place.Take(n0*12).SequenceEqual(copy),"the same moment poses the same");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=12+f/30.0;rig.Build(map.hazards,1,NativePhase.Fight,0,t,t);NativeDeepwellArt.Pose(rig,pose);}
            Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

            // ---- the layout export ----
            var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
            try
            {
                StageArtLayout.Write(root,"rig",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;
                Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block and hazard");
            }
            finally{if(File.Exists(tmp))File.Delete(tmp);}

            // ---- the wiring and its fallback ----
            string R(string p)=>File.ReadAllText(Path.Combine(root,p));var draw=R("Assets/ChipArena/Runtime/NativeDeepwellPresentation.cs");
            Check(draw.Contains("art.Prepare(map,NativeDeepwellArt.Slots,NativeDeepwellArt.Casts);")&&draw.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation loads and draws the art");
            Check(draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);if(rigBuilder!=null){Fill(rigBuilder.Parts);foreach(var b in rig)Draw(b,instancing);}}"),"the box kit and rig draw whenever the art does not");
            Check(R("Assets/ChipArena/Core/NativeDeepwellKit.cs").Contains("{int n=Parts.Count;Parts.Add(x,y,z,hx,hy,hz,q,kind,shadow,color);if(Parts.Count>n){Slot[n]=slot;Level[n]=level;"),"the rig tags its parts beside the very add that builds them");
            // the scripts' shared numbers are the Core's
            var kitpy=R("tools/stages/rig_kit.py");var seapy=R("tools/stages/rig_sea.py");
            Check(kitpy.Contains("SEABED, WATER, DOME_SPRING, DOME_APEX, WELL = -9.0, -1.95, 7.2, 12.6, 10.4")&&NativeDeepwellLooks.Seabed==-9.0&&NativeDeepwellLooks.Water==-1.95&&NativeDeepwellLooks.DomeSpring==7.2&&NativeDeepwellLooks.DomeApex==12.6&&NativeDeepwellLooks.WellRadius==10.4,"the scripts' places are the Core's");
            for(int i=0;i<NativeDeepwellKit.Smokers;i++){NativeDeepwellKit.Smoker(i,out double x,out double z,out double h);Check(seapy.Contains("("+x.ToString("0")+", "+z.ToString("0")+", "+h.ToString("0.0")+")"),"smoker "+i+" stands where the effects light it");}

            // ---- the bank, where it is on this machine ----
            string bankPath=Bank(root);
            if(bankPath==null)Console.WriteLine("Deepwell art: the bank is not on this machine (git-ignored); its own checks are skipped");
            else
            {
                var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);long bytes=new FileInfo(bankPath).Length;
                foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
                var cases=map.blocks.Where(b=>b.role!="static").ToList();
                Check(cases.Count==16&&cases.All(b=>b.id.StartsWith("rig-case-",StringComparison.Ordinal)&&sp.Blocks.TryGetValue(b.id,out var t)&&t.All(p=>p>=0)&&t.Distinct().Count()==3)&&sp.Blocks.Count==cases.Count,"every supply case has its model in three tiers, and nothing else is a block model");
                Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()==3),"every rig slot has its model in three tiers ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
                var near=sp.Static.Where(c=>int.TryParse(c.Id,out _)).ToList();var far=sp.Static.Where(c=>!int.TryParse(c.Id,out _)).ToList();
                Check(near.Count>=20&&near.All(c=>c.Props.Distinct().Count()==3),"the map's chunks, each in three tiers ("+near.Count+")");
                Check(far.Count==3&&far.All(c=>c.Props.Distinct().Count()==1),"the far sea's chunks, one tier each ("+far.Count+")");
                Check(bytes<=20500000,"the bank stays within its budget of about 20 MB ("+(bytes/1e6).ToString("0.0")+" MB)");
                // the play space: within 2 m over a floor a pilot may stand on (a top no pilot column caps), the Fine art keeps within
                // .25 m of a block (what looks like cover is cover); higher is overhead. Nothing stands in a spawn's capsule.
                var capped=new HashSet<string>(map.pilotBlocks.Where(p=>!p.bots).Select(p=>p.id.Replace("rig-top-","rig-")));
                var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2,cap:capped.Contains(b.id))).ToArray();
                int proud=0,total=0;double farthest=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;long verts=0;int inHull=0;
                foreach(var c in near)
                {
                    var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                    {
                        verts++;double floor=double.NegativeInfinity;foreach(var b in boxes)if(!b.cap&&v.X>=b.x0&&v.X<=b.x1&&v.Z>=b.z0&&v.Z<=b.z1&&b.y1<=v.Y+.05&&b.y1>floor)floor=b.y1;
                        if(double.IsNegativeInfinity(floor)||v.Y-floor>2)continue;total++;
                        double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                        if(best>.25+1e-3){proud++;var key=((int)Math.Floor(v.X))+","+(Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}farthest=Math.Max(farthest,best);
                        foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                    }
                }
                Console.WriteLine("Deepwell art: bank "+(bytes/1e6).ToString("0.00")+" MB, "+bank.Props.Count+" props, "+near.Count+" map chunks ("+verts+" Fine vertices) and "+far.Count+" far; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+farthest.ToString("0.00")+" m");
                if(proud>0)Console.WriteLine("Deepwell art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(40).Select(q=>q.Key+":"+q.Value)));
                Check(farthest<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+farthest.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
                Check(inSpawn==0&&inHull==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
            }
            Console.WriteLine("PASS deepwell art ("+count+" checks; rig draws up to "+most+" a frame)");
        }
        private static double Col(NativeStageArtPose p,int i,int c){int o=i*12+c;return Math.Sqrt(p.Place[o]*p.Place[o]+p.Place[o+4]*p.Place[o+4]+p.Place[o+8]*p.Place[o+8]);}
        private static string Bank(string root)
        {
            const string name="ChipArenaLocal/Resources/ChipArenaStageDeepwell.bytes";var dir=Environment.GetEnvironmentVariable("CHIPARENA_STAGE_BANKS");
            foreach(var p in new[]{dir==null?null:Path.Combine(dir,name),Path.Combine(root,"Assets/LocalAssets/"+name),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/"+name})
                if(p!=null&&File.Exists(p))return p;
            return null;
        }
    }
}
#endif
