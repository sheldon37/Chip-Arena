using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// The Factory's stage art (docs/arenas/FACTORY-ART.md, docs/arenas/STAGE-ART.md): its art poses against its box rig
// at every moment of two rounds, the boxes the rig keeps under the art (the telegraph lamps, a blade's blood), the layout export,
// the pilot columns the art pass leaves (the shell's caps and the runways), the runtime's wiring and its bank-absent fallback; and,
// where the bank is on this machine (it is git-ignored), the bank itself against the map.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --factory-art` (also in the full run).
internal static class FactoryArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Factory art: "+why);}
        Check(NativeStageArt.BankOf("factory")=="ChipArenaStageFactory"&&NativeFactoryArt.Bank=="ChipArenaStageFactory"&&NativeStageArt.BankOf("sanctum")=="ChipArenaStageSanctum","the Factory's bank is named beside the others");
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("factory");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,"factory");
        var looks=NativeFactoryKit.Resolve(look.kitLooks);var rig=new NativeFactoryRig(looks);var pose=new NativeStageArtPose();FactoryHazard[] hz=map.hazards;

        // ---- the slots ----
        var slots=NativeFactoryArt.Slots;
        Check(slots.Length==13&&slots.Distinct().Count()==13&&slots[NativeFactoryArt.Blade]=="blade"&&slots[NativeFactoryArt.RailSaw]=="rail-saw"&&slots[NativeFactoryArt.PressRod]=="press-rod"&&slots[NativeFactoryArt.Bridge]=="crane-bridge"
            &&slots[NativeFactoryArt.HookHigh]=="crane-hook-high"&&slots[NativeFactoryArt.LoadHigh]=="crane-load-high"&&slots[NativeFactoryArt.Sling]=="crane-sling"&&slots[NativeFactoryArt.Chuck]=="chuck","the rig slots");
        Check(NativeFactoryArt.Casts(NativeFactoryArt.Blade)&&NativeFactoryArt.Casts(NativeFactoryArt.Bridge)&&NativeFactoryArt.Casts(NativeFactoryArt.Load)&&NativeFactoryArt.Casts(NativeFactoryArt.Hook)&&!NativeFactoryArt.Casts(NativeFactoryArt.LoadHigh)
            &&!NativeFactoryArt.Casts(NativeFactoryArt.HookHigh)&&!NativeFactoryArt.Casts(NativeFactoryArt.Ropes)&&!NativeFactoryArt.Casts(NativeFactoryArt.Sling),"the rig casts shadows as the box rig does: not the ropes and slings, nor the hook and load while they hang high");
        var heads=map.blocks.Where(NativeFactoryArt.LatheHead).ToArray();
        Check(heads.Length==6&&heads.All(h=>{var bed=map.blocks.Single(b=>b.id==h.id.Substring(0,h.id.Length-5));return Math.Sign(bed.pos.x-h.pos.x)==NativeFactoryArt.ChuckSide(h);}),"six lathes, each chuck on its bed's side of the headstock");

        // ---- the poses agree with the box rig at every moment ----
        double worst=0;string worstAt="";int slotBlades=0,railBlades=0,slammed=0,high=0,low=0,taut=0,slack=0,fallen=0,highHook=0,lowHook=0;
        void Near(string what,double t,double d){if(d>worst){worst=d;worstAt=what+" at "+t.ToString("0.00");}}
        var crane=hz.Single(h=>h.kind==NativeFactoryHazards.Crane);var press=hz.First(h=>h.kind==NativeFactoryHazards.Press);double bw=crane.size.x/3;
        for(int round=0;round<2;round++)for(double t=-1;t<150;t+=.05)
        {
            rig.Build(hz,round,t);NativeFactoryArt.Pose(hz,map.blocks,round,t,t,pose);int blades=0,rods=0,slings=0,chucks=0,one=0;
            for(int i=0;i<pose.Count;i++)
            {
                int slot=pose.Slot[i];int p=i*12;double sy=Math.Sqrt(pose.Place[p+1]*pose.Place[p+1]+pose.Place[p+5]*pose.Place[p+5]+pose.Place[p+9]*pose.Place[p+9]),sx=Math.Sqrt(pose.Place[p]*pose.Place[p]+pose.Place[p+4]*pose.Place[p+4]+pose.Place[p+8]*pose.Place[p+8]);
                double x,y,z;
                switch(slot)
                {
                    case NativeFactoryArt.Blade:
                    {
                        // the disc at the hazard's radius where the rig's plates are, turned as one of them is (they stand 22.5 degrees apart)
                        blades++;At(pose,i,0,0,0,out x,out y,out z);double half=sx/Math.Sqrt(2);Near("a blade",t,Nearest(rig,x,y,z,half,half,.025));Check(Math.Abs(sx-sy)<1e-4&&(Math.Abs(sx-.8)<1e-4||Math.Abs(sx-.85)<1e-4),"a blade's radius is its hazard's");
                        At(pose,i,1,0,0,out double ax,out double ay,out double az);double turn=double.MaxValue;
                        for(int k=0;k<rig.Parts.Count;k++){ref var q=ref rig.Parts.Items[k];if(Math.Abs(q.Hx-half)>1e-3||Math.Abs(q.Hz-.025)>1e-3||Math.Abs(q.X-x)+Math.Abs(q.Y-y)+Math.Abs(q.Z-z)>1e-3)continue;
                            new FactoryQ{X=q.Qx,Y=q.Qy,Z=q.Qz,W=q.Qw}.Rotate(sx,0,0,out double rx,out double ry,out double rz);turn=Math.Min(turn,Math.Sqrt((x+rx-ax)*(x+rx-ax)+(y+ry-ay)*(y+ry-ay)+(z+rz-az)*(z+rz-az)));}
                        Near("a blade's turn",t,turn);if(Math.Abs(sx-.8)<1e-4)slotBlades++;else railBlades++;break;
                    }
                    case NativeFactoryArt.RailSaw:At(pose,i,0,.85+.45-.275,0,out x,out y,out z);Near("a rail saw's carriage",t,Nearest(rig,x,y,z,.16,.125,.28));break;
                    case NativeFactoryArt.Ram:At(pose,i,0,0,0,out x,out y,out z);Near("a ram",t,Nearest(rig,x,y,z,press.size.x/2,press.size.y/2,press.size.z/2));if(y<1)slammed++;break;
                    case NativeFactoryArt.PressRod:rods++;At(pose,i,0,.5,0,out x,out y,out z);Near("a guide rod",t,Nearest(rig,x,y,z,.05,sy/2,.05));break;
                    case NativeFactoryArt.Bridge:one++;At(pose,i,.95,1.16,0,out x,out y,out z);Near("the bridge's girder",t,Nearest(rig,x,y,z,.23,.6,10.9));break;
                    case NativeFactoryArt.Trolley:one++;At(pose,i,0,.21,0,out x,out y,out z);Near("the trolley",t,Nearest(rig,x,y,z,1.3,.21,1.1));break;
                    case NativeFactoryArt.Ropes:At(pose,i,.18,.5,.12,out x,out y,out z);Near("a hoist rope",t,Nearest(rig,x,y,z,.018,sy/2,.018));break;
                    case NativeFactoryArt.Hook:case NativeFactoryArt.HookHigh:
                        one++;At(pose,i,0,.75,0,out x,out y,out z);Near("the hook block",t,Nearest(rig,x,y,z,.28,.4,.18,slot==NativeFactoryArt.Hook));if(slot==NativeFactoryArt.Hook)lowHook++;else highHook++;break;
                    case NativeFactoryArt.Load:case NativeFactoryArt.LoadHigh:
                        one++;At(pose,i,0,.04,0,out x,out y,out z);Near("the bundle",t,Nearest(rig,x,y,z,bw*.48,.04,crane.size.z/2,slot==NativeFactoryArt.Load));if(slot==NativeFactoryArt.Load)low++;else high++;if(y<.2)fallen++;break;
                    case NativeFactoryArt.Sling:
                        slings++;At(pose,i,0,.5,0,out x,out y,out z);
                        if(Math.Abs(sy-NativeFactoryArt.SlackSling)<1e-9&&Math.Abs(pose.Place[p+5]-1)<1e-6&&slingsSlack(pose)){Near("a slack sling",t,Nearest(rig,x,y,z,.02,.5,.02));slack++;}else{Near("a sling",t,Nearest(rig,x,y,z,.02,.02,sy/2));taut++;}break;
                    case NativeFactoryArt.Chuck:
                    {
                        var h=heads[chucks++];At(pose,i,0,0,0,out x,out y,out z);Check(Math.Abs(x-(h.pos.x+NativeFactoryArt.ChuckSide(h)*h.size.x/2))<1e-4&&Math.Abs(y-h.pos.y)<1e-4&&Math.Abs(z-h.pos.z)<1e-4,"a chuck sits on its headstock's face");
                        At(pose,i,1,0,0,out double cx,out _,out _);Check(Math.Abs(cx-x-NativeFactoryArt.ChuckSide(h))<1e-4,"a chuck's axis points along its bed");break;
                    }
                }
            }
            int rigBlades=0;for(int k=0;k<rig.Parts.Count;k++)if(Math.Abs(rig.Parts.Items[k].Hz-.045f)<1e-4&&Math.Abs(rig.Parts.Items[k].Hx-.17f)<1e-4)rigBlades++;
            Check(blades==rigBlades/2&&rods==8&&one==4&&chucks==6&&(slings==4||slings==2),"as many blades as the rig draws, eight rods, the crane whole, six chucks at "+t.ToString("0.00")+" ("+blades+","+rigBlades+","+rods+","+one+","+slings+")");
        }
        Check(worst<2e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.0000")+" m: "+worstAt+")");
        Check(slotBlades>200&&railBlades>2000&&slammed>50&&high>500&&low>100&&fallen>50&&taut>1000&&slack>100&&highHook>500&&lowHook>50,"the poses cover the slitters up, the rail saws, the slam, the crane carrying, dropping and regripping ("+slotBlades+","+railBlades+","+slammed+","+high+","+low+","+fallen+","+taut+","+slack+","+highHook+","+lowHook+")");
        NativeFactoryArt.Pose(null,null,0,10,10,pose);Check(pose.Count==0,"no hazards and no blocks pose nothing");

        // ---- the boxes the rig keeps under the art: the lamps, and a bloodied blade's smears ----
        int kept=0,replaced=0,smears=0;var blood=new double[hz.Length];for(int i=0;i<blood.Length;i++)blood[i]=5;
        foreach(double t in new[]{-1.0,9.0,14.5,22.0,31.0,47.5,63.0})
        {
            rig.Build(hz,0,t,blood);
            for(int i=0;i<rig.Parts.Count;i++)
            {
                ref var p=ref rig.Parts.Items[i];bool lamp=p.Look==looks.Look[NativeFactoryKit.Lamp]&&p.Flags==looks.Flags[NativeFactoryKit.Lamp]&&!p.Shadow&&p.Hy<=.11f,smear=p.Look==looks.Look[NativeFactoryKit.Red]&&p.Hz<.035f&&p.Hy<.03f&&!p.Shadow;
                if(rig.Kept[i]){kept++;if(smear&&!lamp)smears++;Check(lamp||smear,"a kept box is a lamp or a smear of blood (part "+i+" at "+t+")");}else{replaced++;Check(!lamp,"every lamp stays a box under the art (part "+i+" at "+t+")");}
            }
            for(int i=rig.Parts.Count;i<NativeFactoryRig.Capacity;i++)Check(!rig.Kept[i],"nothing is kept past the frame's parts");
        }
        Check(kept>500&&replaced>300&&smears>20,"the telegraph lamps and the blood stay boxes ("+kept+" kept, "+smears+" smears, "+replaced+" replaced over seven moments)");

        // ---- a frame's draws: inside the buffer, deterministic, allocation free ----
        NativeFactoryArt.Pose(hz,map.blocks,0,15.5,15.5,pose);int n=pose.Count;var copy=pose.Place.Take(n*12).ToArray();
        Check(n>=26&&n<60,"a frame's rig draws ("+n+")");
        NativeFactoryArt.Pose(hz,map.blocks,0,15.5,15.5,pose);Check(pose.Count==n&&pose.Place.Take(n*12).SequenceEqual(copy),"the same moment poses the same");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=10+f/30.0;NativeFactoryArt.Pose(hz,map.blocks,1,t,t,pose);rig.Build(hz,1,t,blood);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing and the rig are allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"factory",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;var kit=NativeFactoryKit.Build(map.blocks,hz,looks,true,true);var plain=NativeFactoryKit.Build(map.blocks,hz,looks);
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("parts").GetArrayLength()==kit.Count&&j.GetProperty("hazards").GetArrayLength()==hz.Length,"the layout carries every block, hazard and kit part");
            Check(kit.Count==plain.Count&&plain.KindLog==null&&kit.KindLog.Count==kit.Count&&kit.OwnerLog.Count==kit.Count&&Enumerable.Range(0,kit.Count).All(i=>kit.Items[i].X==plain.Items[i].X&&kit.Items[i].Y==plain.Items[i].Y&&kit.Items[i].Hx==plain.Items[i].Hx&&kit.Items[i].Look==looks.Look[kit.KindLog[i]]),
                "the logged kit is the kit, part for part, each with its kind; nothing is logged in play");
            Check(kit.OwnerLog.All(w=>w!=null&&(w=="roofs"||w.StartsWith("flight:factory-stair-")||w.StartsWith("hazard:")&&hz.Any(h=>"hazard:"+h.id==w)||map.blocks.Any(b=>b.id==w))),"every kit part names the block, flight or hazard it dresses");
            var r=j.GetProperty("rig");Check(Math.Abs(r.GetProperty("railUp").GetDouble()-.14)<1e-9&&Math.Abs(r.GetProperty("trolleyUp").GetDouble()-1.76)<1e-9&&r.GetProperty("chucks").GetArrayLength()==6&&j.GetProperty("spawns").GetArrayLength()==16&&j.GetProperty("lights").GetArrayLength()==8,"the rig's shared numbers, the spawns and the lights");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the pilot columns: the shell's caps and the runways only (no invisible wall over a top a pilot can reach) ----
        Check(map.pilotBlocks.Select(p=>p.id).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(new[]{"factory-top-runway-n","factory-top-runway-s","factory-top-wall-e","factory-top-wall-n","factory-top-wall-s","factory-top-wall-w"})&&map.pilotBlocks.All(p=>!p.bots),
            "six pilot columns: the four wall caps and the two runways ("+string.Join(",",map.pilotBlocks.Select(p=>p.id))+")");
        foreach(var (id,open) in new[]{("factory-ladle",true),("factory-pulpit-a-roof",true),("factory-pulpit-b-roof",true),("factory-roof-n-2",true),("factory-roof-s-3",true),("factory-wall-n-clad",false),("factory-wall-e-clad",false),("factory-runway-n",false),("factory-runway-s",false)})
        {
            var b=map.blocks.Single(x=>x.id==id);var on=new V3(b.pos.x,b.pos.y+b.size.y/2+.9f,b.pos.z);
            Check((NativeMapRules.ExcludeTops(map,on,false)-on).Length>.001f!=open,id+(open?": a pilot stands on it":": its pilot column holds it"));
        }
        var gen=File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py"));
        Check(gen.Contains("EDGES['factory']=('factory-top-wall',);OVERHANG['factory']=('factory-top-runway',)"),"the generator keeps the Factory's edges and the runways under the crane");

        // ---- the runtime's wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var pres=R("Assets/ChipArena/Runtime/NativeFactoryPresentation.cs");
        Check(pres.Contains("art.Prepare(map,NativeFactoryArt.Slots,NativeFactoryArt.Casts);")&&pres.Contains("NativeFactoryArt.Pose(hz,map.blocks,round,clock,now,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);")&&R("Assets/ChipArena/Runtime/NativeStagePrewarm.cs").Contains("PrepareArt(map);"),
            "the presentation prepares the art (its prewarm too), poses and renders it");
        Check(pres.Contains("foreach(var b in ArtDrawn?kitKept:kit)Draw(b,instancing);")&&pres.Contains("Fill(rigBuilder.Parts,ArtDrawn?rigBuilder.Kept:null);")&&pres.Contains("if(kept!=null&&!kept[i])continue;")&&pres.Contains("if(!Pane(ref p,g))continue;"),
            "the box kit and the whole box rig draw whenever the art does not; under the art only the lanterns' glazing and the kept boxes");
        foreach(var f in new[]{"Assets/ChipArena/Core/NativeFactoryArt.cs"})Check(File.Exists(Path.Combine(root,f+".meta")),f+" has its meta");
        foreach(var f in new[]{"tools/stages/factory_art.py","tools/stages/factory_plant.py","tools/stages/factory_rig.py","tools/stages/factory_kit.py"})Check(File.Exists(Path.Combine(root,f)),f+" is in the tools");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Factory art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);double mb=new FileInfo(bankPath).Length/1048576.0;
            Check(mb<=21,"the bank keeps to its budget ("+mb.ToString("0.0")+" MB)");
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var breakable=map.blocks.Where(b=>b.role!="static").ToArray();
            Check(breakable.Length==10&&breakable.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)&&sp.Blocks[b.id].Distinct().Count()==3),"every breakable crate, pallet stack and drum pallet has its model in three tiers");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)),"every rig slot has its model in every tier ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            Check(sp.Static.Count>=30&&sp.Static.All(c=>c.Props.Distinct().Count()==3),"the static chunks, each in three tiers");
            // the play space: below 2 m over its floor (the slab, the galleries, catwalks, landings and pulpit decks) the Fine art stands on
            // a block or keeps within .25 m of one, so nothing stands where no collider is; nothing stands in a spawn's capsule
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            var decks=map.blocks.Where(b=>b.id.StartsWith("factory-gallery-",StringComparison.Ordinal)||b.id.StartsWith("factory-catwalk-",StringComparison.Ordinal)||b.id.EndsWith("-landing",StringComparison.Ordinal)||b.id.EndsWith("-deck",StringComparison.Ordinal)).ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            foreach(var c in sp.Static)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    if(Math.Abs(v.X)>=38||Math.Abs(v.Z)>=30)continue;
                    double floor=0;foreach(var d in decks)if(v.Y>=d.pos.y+d.size.y/2-.05&&Math.Abs(v.X-d.pos.x)<=d.size.x/2+.3&&Math.Abs(v.Z-d.pos.z)<=d.size.z/2+.3)floor=Math.Max(floor,d.pos.y+d.size.y/2);
                    if(v.Y-floor>2||v.Y<floor-.05)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,b.y0-v.Y),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25){proud++;var key=((int)Math.Floor(v.X))+","+((int)Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Factory art: bank "+mb.ToString("0.00")+" MB, "+bank.Props.Count+" props; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Factory art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(30).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.26&&proud<=total/1000,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS Factory art ("+count+" checks)");
    }
    // The slings hang slack this frame: just two are posed (four run taut to the bundle's corners).
    private static bool slingsSlack(NativeStageArtPose p){int n=0;for(int i=0;i<p.Count;i++)if(p.Slot[i]==NativeFactoryArt.Sling)n++;return n==2;}
    // The point (lx, ly, lz) of draw i's frame in the world.
    private static void At(NativeStageArtPose p,int i,double lx,double ly,double lz,out double x,out double y,out double z)
    {int o=i*12;var m=p.Place;x=m[o]*lx+m[o+1]*ly+m[o+2]*lz+m[o+3];y=m[o+4]*lx+m[o+5]*ly+m[o+6]*lz+m[o+7];z=m[o+8]*lx+m[o+9]*ly+m[o+10]*lz+m[o+11];}
    // The distance from (x, y, z) to the nearest box rig part of these half extents (and this shadow casting, if asked), or infinity.
    private static double Nearest(NativeFactoryRig rig,double x,double y,double z,double hx,double hy,double hz,bool? shadow=null)
    {
        double best=double.MaxValue;
        for(int i=0;i<rig.Parts.Count;i++)
        {
            ref var p=ref rig.Parts.Items[i];if(Math.Abs(p.Hx-hx)>1.5e-3||Math.Abs(p.Hy-hy)>1.5e-3||Math.Abs(p.Hz-hz)>1.5e-3||shadow.HasValue&&p.Shadow!=shadow.Value)continue;
            double d=Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y)+(p.Z-z)*(p.Z-z));if(d<best)best=d;
        }
        return best;
    }
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageFactory.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageFactory.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
}
