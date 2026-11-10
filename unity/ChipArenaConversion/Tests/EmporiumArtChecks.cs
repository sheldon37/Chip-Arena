using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Top Dog Emporium's stage art (docs/arenas/EMPORIUM.md "Stage art", docs/arenas/STAGE-ART.md): its art poses against its box rig
// at every moment of two rounds, the boxes the rig keeps under the art (the telegraph lamps), the layout export, the runtime's
// wiring and its bank-absent fallback; and, where the bank is on this machine (it is git-ignored), the bank itself against the map.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --emporium-art` (also in the full run).
internal static class EmporiumArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Emporium art: "+why);}
        Check(NativeStageArt.BankOf("emporium")=="ChipArenaStageEmporium"&&NativeStageArt.BankOf("sanctum")=="ChipArenaStageSanctum","the Emporium's bank is named beside the Sanctum's");
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);var map=content.Map("emporium");
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);var look=NativeMapLooks.Find(set,"emporium");
        var looks=NativeEmporiumLooks.Resolve(look.kitLooks);var rig=new NativeEmporiumRig(looks);var pose=new NativeStageArtPose();

        // ---- the slots ----
        var slots=NativeEmporiumArt.Slots;
        Check(slots.Length==37&&slots.Distinct().Count()==37&&slots[NativeEmporiumArt.Wiener]=="wiener"&&slots[NativeEmporiumArt.Bubble]=="bubble"&&slots[NativeEmporiumArt.SauceHead+1]=="sauce-head-m"&&slots[NativeEmporiumArt.SignBulbs+2]=="sign-bulbs-2","the rig slots");
        var burners=map.hazards.Where(h=>h.kind==NativeWardZeroHazards.Burner).ToArray();
        Check(burners.Length==NativeEmporiumArt.Griddles&&burners.All(h=>h.zones.Length==2)&&burners.SelectMany(h=>new[]{"griddle-"+h.id+"-0","griddle-"+h.id+"-1"}).SequenceEqual(slots.Skip(NativeEmporiumArt.Griddle)),"a griddle slot per zone, in the hazards' order");
        Check(NativeEmporiumArt.Casts(NativeEmporiumArt.Wiener)&&NativeEmporiumArt.Casts(NativeEmporiumArt.Bun)&&NativeEmporiumArt.Casts(NativeEmporiumArt.Sign)&&!NativeEmporiumArt.Casts(NativeEmporiumArt.Bubble)&&!NativeEmporiumArt.Casts(NativeEmporiumArt.SignBulbs+1)&&!NativeEmporiumArt.Casts(NativeEmporiumArt.Griddle+3),"the big parts cast shadows, the small glowing ones do not");
        NativeEmporiumArt.Pose("factory",map.hazards,map.blocks,0,NativePhase.Fight,0,20,20,pose);Check(pose.Count==0,"nothing is posed on another map");

        // ---- the poses agree with the box rig at every moment ----
        double worst=0;string worstAt="";int seen=0,wieners=0,west=0,east=0,squashed=0,stretched=0,columns=0,blinks=0,hot=0,open=0,rammed=0;
        void Near(string what,double t,double d){if(d>worst){worst=d;worstAt=what+" at "+t.ToString("0.00");}}
        double L=NativeEmporiumLooks.SignLength/2,t8=Math.Tan(Math.PI/8)*1.04,t16=Math.Tan(Math.PI/16)*1.04;
        for(int round=0;round<2;round++)for(double t=0;t<70;t+=.05)
        {
            rig.Build(map.id,map.hazards,map.blocks,round,NativePhase.Fight,0,t,t);NativeEmporiumArt.Pose(map.id,map.hazards,map.blocks,round,NativePhase.Fight,0,t,t,pose);seen++;
            int lids=0,eyes=0,bulbs=0,griddles=0;
            for(int i=0;i<pose.Count;i++)
            {
                int slot=pose.Slot[i];int p=i*12;double sy=Math.Sqrt(pose.Place[p+1]*pose.Place[p+1]+pose.Place[p+5]*pose.Place[p+5]+pose.Place[p+9]*pose.Place[p+9]),sx=Math.Sqrt(pose.Place[p]*pose.Place[p]+pose.Place[p+4]*pose.Place[p+4]+pose.Place[p+8]*pose.Place[p+8]);
                double x,y,z;
                switch(slot)
                {
                    case NativeEmporiumArt.Wiener:At(pose,i,0,0,0,out x,out y,out z);Near("the wiener",t,Nearest(rig,x,y,z,.9,.9*t16,1.185));wieners++;break;
                    case NativeEmporiumArt.WienerFace:
                        // an eye's white on the leading side
                        At(pose,i,.9*.78+.04,.9*.55,.42,out x,out y,out z);Near("the wiener's eye",t,Nearest(rig,x,y,z,.28,.28*t8,.05));if(pose.Place[p]>0)east++;else west++;break;
                    case NativeEmporiumArt.WienerPupil:At(pose,i,0,0,0,out x,out y,out z);Near("a pupil",t,Nearest(rig,x,y,z,.03,.08,.08));break;
                    case NativeEmporiumArt.Flap:At(pose,i,0,-1.05,0,out x,out y,out z);Near("a flap",t,Nearest(rig,x,y,z,.03,1.05,1.65));if(Math.Abs(pose.Place[p+1])>.5)open++;break;
                    case NativeEmporiumArt.Bun:
                        At(pose,i,0,.05,0,out x,out y,out z);Near("a bun",t,Nearest(rig,x,y,z,Math.Min(1.55,1.5*sx),.05*sy,Math.Min(1.55,1.5*sx)));if(sy<.9)squashed++;if(sy>1.1)stretched++;break;
                    case NativeEmporiumArt.Ram:At(pose,i,0,.5,0,out x,out y,out z);Near("a ram",t,Nearest(rig,x,y,z,.22,sy/2,.22));if(sy<3)rammed++;break;
                    case NativeEmporiumArt.Bell:At(pose,i,0,0,0,out x,out y,out z);Near("a bell",t,Nearest(rig,x,y,z,.32,.28,.32));break;
                    case NativeEmporiumArt.Popcorn:At(pose,i,0,0,0,out x,out y,out z);Near("a popcorn sausage",t,Nearest(rig,x,y,z,.075,.075,.24));break;
                    case NativeEmporiumArt.GeyserCap:case NativeEmporiumArt.GeyserCap+1:At(pose,i,0,.5,0,out x,out y,out z);Near("a geyser's cap",t,Nearest(rig,x,y+.0,z,.44,sy/2,.44));break;
                    case NativeEmporiumArt.PumpRod:At(pose,i,0,.5,0,out x,out y,out z);Near("a pump's rod",t,Nearest(rig,x,y,z,.12,sy/2,.12));break;
                    case NativeEmporiumArt.PumpHead:case NativeEmporiumArt.PumpHead+1:At(pose,i,0,.11,0,out x,out y,out z);Near("a pump's head",t,Nearest(rig,x,y,z,.42,.11,.42));break;
                    case NativeEmporiumArt.Sauce:case NativeEmporiumArt.Sauce+1:
                        columns++;Near("a sauce column's height",t,rig.ColumnTop.Min(c=>Math.Abs(c-sy)));break;
                    case NativeEmporiumArt.SauceHead:case NativeEmporiumArt.SauceHead+1:At(pose,i,0,.1,0,out x,out y,out z);Near("a sauce column's head",t,Nearest(rig,x,y,z,double.NaN,double.NaN,.24));break;
                    case NativeEmporiumArt.Sign:
                        At(pose,i,0,0,0,out x,out y,out z);Near("the sign",t,Nearest(rig,x,y,z,.9,.9*t16,L-.5));
                        At(pose,i,L-.32,0,0,out x,out y,out z);Near("the sign's heading",t,Nearest(rig,x,y,z,.76,.76*t8,.3));break;
                    case NativeEmporiumArt.SignBulbs:case NativeEmporiumArt.SignBulbs+1:case NativeEmporiumArt.SignBulbs+2:
                    {
                        int g=slot-NativeEmporiumArt.SignBulbs;double u=g*2*Math.PI/36;At(pose,i,Math.Cos(u)*(L+1.4),Math.Sin(u)*2.9,0,out x,out y,out z);Near("a marquee bulb",t,Nearest(rig,x,y,z,.17,.17,.17));
                        bool on=((g+(int)Math.Floor(t*6))%3)==0;if(on)bulbs++;Check(on==(pose.Tint[i*4]>.9),"the marquee's chase: group "+g+" at "+t);break;
                    }
                    case NativeEmporiumArt.BunHanger:x=pose.Place[p+3];y=pose.Place[p+7];z=pose.Place[p+11];Near("a conveyor hanger",t,Nearest(rig,x,y-.385,z,.03,.335,.03));break;
                    case NativeEmporiumArt.GrillSausageCounter:At(pose,i,0,0,0,out x,out y,out z);Near("a counter's grill sausage",t,Nearest(rig,x,y,z,.055,.055,.31));break;
                    case NativeEmporiumArt.GrillSausagePrep:At(pose,i,0,0,0,out x,out y,out z);Near("a prep table's grill sausage",t,Nearest(rig,x,y,z,.055,.055,.235));break;
                    case NativeEmporiumArt.BigDogArm:At(pose,i,0,Math.Sqrt(.36+.0025),0,out x,out y,out z);Near("the Big Dog's glove",t,Nearest(rig,x,y,z,.17,.17*t8,.1));break;
                    case NativeEmporiumArt.BigDogPupils:eyes++;break;
                    case NativeEmporiumArt.BigDogLids:lids++;break;
                    case NativeEmporiumArt.JukeTube:At(pose,i,0,.5,0,out x,out y,out z);Near("a jukebox tube",t,Nearest(rig,x,y,z,.06,.95,.03));break;
                    case NativeEmporiumArt.Bubble:At(pose,i,0,0,0,out x,out y,out z);Near("a bubble",t,Nearest(rig,x,y,z,.035,.035,.0375));break;
                    default:
                        if(slot>=NativeEmporiumArt.Griddle){griddles++;if(pose.Tint[i*4]>2)hot++;Check(pose.Place[p+3]==0&&pose.Place[p+7]==0&&pose.Place[p+11]==0,"a griddle's bed stays in the world's frame");}
                        break;
                }
            }
            Check(lids+eyes==1&&(lids==1)==NativeEmporiumRig.Blink(t),"the Big Dog blinks with the rig at "+t);if(lids==1)blinks++;
            Check(bulbs==1&&griddles==8,"one bulb group lit, eight griddle beds at "+t);
            // every replaced box has a model: the art never draws fewer wieners, buns or columns than the rig does
            int rigWieners=rig.WienerX.Count(v=>!double.IsNaN(v)),poseWieners=0,poseColumns=0;for(int i=0;i<pose.Count;i++){if(pose.Slot[i]==NativeEmporiumArt.Wiener)poseWieners++;if(pose.Slot[i]==NativeEmporiumArt.Sauce||pose.Slot[i]==NativeEmporiumArt.Sauce+1)poseColumns++;}
            Check(rigWieners==poseWieners&&rig.ColumnTop.Count(v=>v>=.1)==poseColumns,"as many wieners and sauce columns as the rig draws at "+t);
        }
        Check(worst<2e-3,"every posed model stands where the box rig's own part does (worst "+worst.ToString("0.0000")+" m: "+worstAt+")");
        Check(wieners>50&&west>20&&east>20&&squashed>0&&stretched>0&&columns>50&&blinks>0&&hot>50&&open>10&&rammed>10,"the poses cover the wiener both ways, the buns' squash and stretch, the slam, the geysers, the hot griddles, the flaps, a blink ("+wieners+","+west+","+east+","+squashed+","+stretched+","+columns+","+blinks+","+hot+","+open+","+rammed+")");

        // ---- the boxes the rig keeps under the art: lamps and drop shadows only ----
        int kept=0,replaced=0;var keptRoles=new HashSet<byte>();
        foreach(double t in new[]{9.0,14.5,16.0,20.0,31.0}){rig.Build(map.id,map.hazards,map.blocks,0,NativePhase.Fight,0,t,t);for(int i=0;i<rig.Parts.Count;i++){if(rig.Kept[i]){kept++;keptRoles.Add(rig.Role[i]);Check(rig.Parts.Items[i].Hy<=.26f&&!rig.Parts.Items[i].Shadow,"a kept box is a lamp or a drop shadow");}else replaced++;}}
        Check(kept>500&&replaced>3000&&keptRoles.IsSubsetOf(new[]{NativeEmporiumRig.Hazard,NativeEmporiumRig.Floor}),"the telegraph lamps and the drop shadows stay boxes ("+kept+" kept, "+replaced+" replaced over five moments)");

        // ---- a frame's draws: inside the buffer, deterministic, allocation free ----
        NativeEmporiumArt.Pose(map.id,map.hazards,map.blocks,0,NativePhase.Fight,0,15.5,15.5,pose);int n=pose.Count;var copy=pose.Place.Take(n*12).ToArray();
        Check(n>120&&n<NativeStageArtPose.Capacity-20,"a frame's rig draws ("+n+")");
        NativeEmporiumArt.Pose(map.id,map.hazards,map.blocks,0,NativePhase.Fight,0,15.5,15.5,pose);Check(pose.Count==n&&pose.Place.Take(n*12).SequenceEqual(copy),"the same moment poses the same");
        int most=0;for(double t=0;t<60;t+=.25){NativeEmporiumArt.Pose(map.id,map.hazards,map.blocks,0,NativePhase.Fight,0,t,t,pose);most=Math.Max(most,pose.Count);}Check(most<NativeStageArtPose.Capacity-20,"the busiest frame fits the buffer ("+most+")");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<300;f++){double t=10+f/30.0;NativeEmporiumArt.Pose(map.id,map.hazards,map.blocks,1,NativePhase.Fight,0,t,t,pose);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"posing is allocation free per frame");

        // ---- the layout export ----
        var tmp=Path.Combine(Path.GetTempPath(),"stage-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"emporium",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var j=doc.RootElement;var kit=NativeEmporiumKit.Build(map,look);
            Check(j.GetProperty("blocks").GetArrayLength()==map.blocks.Length&&j.GetProperty("parts").GetArrayLength()==kit.Parts.Count&&j.GetProperty("hazards").GetArrayLength()==map.hazards.Length,"the layout carries every block, hazard and kit part");
            Check(kit.Family.Count==kit.Parts.Count&&kit.Owner.Count==kit.Parts.Count&&kit.Kind.Count==kit.Parts.Count&&kit.Family.All(f=>!string.IsNullOrEmpty(f)),"every kit part has its family, owner and kind");
            var families=new HashSet<string>(kit.Family);
            Check(new[]{"floor","wall","wall:neon","wall:poster","wall:icon","oven","roof","pendant","booth","counter","tower","plinth","bridge","stair","bridge-step","conveyor","door-sign:neon"}.All(families.Contains),"the families the models are sculpted by");
            Check(Enumerable.Range(0,kit.Parts.Count).All(i=>(kit.Owner[i]==null)==kit.Overhead[i]&&(kit.Owner[i]==null||map.blocks.Any(b=>b.id==kit.Owner[i]))),"a part's owner is the block it dresses; overhead parts have none");
            Check(j.GetProperty("rig").GetProperty("lanes").GetArrayLength()==2&&Math.Abs(j.GetProperty("rig").GetProperty("mascotBase").GetDouble()-NativeEmporiumRig.MascotBase)<1e-9&&Math.Abs(j.GetProperty("rig").GetProperty("bunHeight").GetDouble()-.94)<1e-9,"the rig's shared numbers");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the runtime's wiring and its fallback ----
        string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var pres=R("Assets/ChipArena/Runtime/NativeEmporiumPresentation.cs");
        Check(pres.Contains("art.Prepare(map,NativeEmporiumArt.Slots,NativeEmporiumArt.Casts);")&&pres.Contains("ArtDrawn=art.Render(arena,camera,low,artPose);"),"the presentation prepares and renders the art");
        Check(pres.Contains("if(!ArtDrawn)foreach(var b in kit)Draw(b,instancing);")&&pres.Contains("Fill(rigBuilder.Parts,ArtDrawn?rigBuilder.Kept:null);")&&pres.Contains("if(kept!=null&&!kept[i])continue;"),"the box kit and the whole box rig draw whenever the art does not; under the art only the kept boxes");
        var shader=R("Assets/ChipArena/Resources/NativeStageLit.shader");Check(shader.Contains("#define DETAIL_TILE 16")&&shader.Contains("else if(detail==DETAIL_TILE)")&&R("tools/stages/stagekit.py").Contains("DETAIL_TILE = 16"),"the glazed tile surface, in the shader and the stage kit");
        foreach(var f in new[]{"Assets/ChipArena/Core/NativeEmporiumArt.cs"})Check(File.Exists(Path.Combine(root,f+".meta")),f+" has its meta");
        foreach(var f in new[]{"tools/stages/emporium_art.py","tools/stages/emporium_fit.py","tools/stages/emporium_props.py","tools/stages/emporium_kit.py"})Check(File.Exists(Path.Combine(root,f)),f+" is in the tools");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Emporium art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bank=NativePackBank.Read(File.ReadAllBytes(bankPath));var sp=NativeStageArt.Read(bank);double mb=new FileInfo(bankPath).Length/1048576.0;
            Check(mb<=21,"the bank keeps to its budget ("+mb.ToString("0.0")+" MB)");
            foreach(var b in map.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var breakable=map.blocks.Where(b=>b.role!="static").ToArray();
            Check(breakable.Length==12&&breakable.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)&&sp.Blocks[b.id].Distinct().Count()==3),"every breakable crate and barrel has its model in three tiers");
            Check(slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)),"every rig slot has its model in every tier ("+string.Join(",",slots.Where(s=>!sp.Rig.ContainsKey(s)))+")");
            Check(sp.Static.Count>=20&&sp.Static.All(c=>c.Props.Distinct().Count()==3),"the static chunks, each in three tiers");
            // the play space: below 2 m over its floor (the hall's, the balconies') the Fine art keeps to the blocks (within .25 m,
            // the stage rule; a thin prop such as a stool within .9 m); higher is overhead; nothing stands in a spawn's capsule
            var boxes=map.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            var decks=map.blocks.Where(b=>b.id=="emporium-balcony-n"||b.id=="emporium-balcony-s").ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            foreach(var c in sp.Static)
            {
                var prop=bank.Props[c.Props[0]];foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)
                {
                    double floor=0;foreach(var d in decks)if(v.Y>=d.pos.y+d.size.y/2-.05&&Math.Abs(v.X-d.pos.x)<=d.size.x/2+.3&&Math.Abs(v.Z-d.pos.z)<=d.size.z/2+.3)floor=d.pos.y+d.size.y/2;
                    if(v.Y-floor>2||v.Y<floor-.05)continue;total++;
                    double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-v.X,v.X-b.x1)),dy=Math.Max(0,Math.Max(b.y0-v.Y,v.Y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-v.Z,v.Z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                    if(best>.25){proud++;var key=((int)Math.Floor(v.X))+","+((int)Math.Floor(v.Y*2)/2.0)+","+((int)Math.Floor(v.Z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                    foreach(var s in spawns){double dx=v.X-s.x,dz=v.Z-s.z,dy=v.Y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
                }
            }
            Console.WriteLine("Emporium art: bank "+mb.ToString("0.00")+" MB, "+bank.Props.Count+" props; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>total/200)Console.WriteLine("Emporium art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(30).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS Emporium art ("+count+" checks)");
    }
    // The point (lx, ly, lz) of draw i's frame in the world.
    private static void At(NativeStageArtPose p,int i,double lx,double ly,double lz,out double x,out double y,out double z)
    {int o=i*12;var m=p.Place;x=m[o]*lx+m[o+1]*ly+m[o+2]*lz+m[o+3];y=m[o+4]*lx+m[o+5]*ly+m[o+6]*lz+m[o+7];z=m[o+8]*lx+m[o+9]*ly+m[o+10]*lz+m[o+11];}
    // The distance from (x, y, z) to the nearest box rig part of these half extents (NaN: any), or infinity.
    private static double Nearest(NativeEmporiumRig rig,double x,double y,double z,double hx,double hy,double hz)
    {
        double best=double.MaxValue;
        for(int i=0;i<rig.Parts.Count;i++)
        {
            ref var p=ref rig.Parts.Items[i];if(!double.IsNaN(hx)&&Math.Abs(p.Hx-hx)>1.5e-3||!double.IsNaN(hy)&&Math.Abs(p.Hy-hy)>1.5e-3||!double.IsNaN(hz)&&Math.Abs(p.Hz-hz)>1.5e-3)continue;
            double d=Math.Sqrt((p.X-x)*(p.X-x)+(p.Y-y)*(p.Y-y)+(p.Z-z)*(p.Z-z));if(d<best)best=d;
        }
        return best;
    }
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageEmporium.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageEmporium.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
}
