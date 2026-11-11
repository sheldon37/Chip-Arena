using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// Ward Zero's stage art (docs/arenas/STAGE-ART.md, docs/arenas/WARDZERO-ART.md): the bank's name for both map records, the kit's
// families, kept parts and movers (every part is kept, a mover or scenery; the boxes themselves unchanged), the posed blocks
// (every wall a Hulk may break out, every breach panel, the Expedition's exit frame), the art's poses against the kit's own Move at
// every moment (each mover's parts stand where the boxes would, a slat hidden exactly when its box is, a broken-out block and
// what rides on it left undrawn, a knock's shudder carried), no invisible walls (no pilot columns in either record; the stairs
// and ramps solid under their wedges), the runtime's wiring and its bank-absent fallback; and, where the bank is on this machine
// (it is git-ignored), the bank itself against the map and its budget.
// `dotnet run --project Tests/CoreTests.csproj -c Release -- --wardzero-art` (also in the full run).
internal static class WardZeroArtChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Ward Zero art: "+why);}
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);
        var siren=content.Map("siren");var exp=content.Map("expedition-ward-zero");var entries=WardZeroArtLayout.Entries(root);

        // ---- the registry: one bank for both records ----
        Check(NativeStageArt.BankOf("siren")=="ChipArenaStageWardZero"&&NativeStageArt.BankOf("expedition-ward-zero")=="ChipArenaStageWardZero"&&NativeWardZeroArt.Bank=="ChipArenaStageWardZero","both Ward Zero records name the bank");
        Check(NativeWardZeroLooks.Active(siren)&&NativeWardZeroLooks.Active(exp),"both records carry the quarantine look");

        // ---- the kit: families, kept parts, movers; the same for both records ----
        var kit=NativeWardZeroKit.Build(siren.blocks,siren.hazards,entries);var kitE=NativeWardZeroKit.Build(exp.blocks,exp.hazards,entries);
        Check(kit.Parts.Length==kitE.Parts.Length&&kit.Movers.Length==kitE.Movers.Length&&kit.MoverSlots==kitE.MoverSlots,"the two records' kits have the same parts and movers");
        for(int i=0;i<kit.Parts.Length;i++){var a=kit.Parts[i];var b=kitE.Parts[i];Check(siren.blocks[a.Parent].id==exp.blocks[b.Parent].id&&a.X==b.X&&a.Y==b.Y&&a.Z==b.Z&&a.Hx==b.Hx&&a.Hy==b.Hy&&a.Hz==b.Hz&&a.Drive==b.Drive&&a.Color==b.Color&&kit.Family[i]==kitE.Family[i],"part "+i+" is the same in both records");}
        Check(kit.Family.Length==kit.Parts.Length&&kit.Family.All(f=>!string.IsNullOrEmpty(f))&&kit.Keeps.Length==kit.Parts.Length&&kit.MoverOf.Length==kit.Parts.Length,"every part has its family, its kept flag and its mover");
        int kept=0,moving=0,scenery=0;
        for(int i=0;i<kit.Parts.Length;i++)
        {
            var p=kit.Parts[i];bool mover=kit.MoverOf[i]>=0;Check(!(kit.Keeps[i]&&mover),"part "+i+" is not both kept and a mover");
            Check(mover==(p.Drive==NativeWardZeroKit.DriveLeaf||p.Drive==NativeWardZeroKit.DriveSlat),"the moving parts are the movers ("+kit.Family[i]+")");
            if(kit.Keeps[i])kept++;else if(mover)moving++;else{scenery++;Check(p.Drive==NativeWardZeroKit.DriveSteady||kit.Family[i]=="shard","scenery is steady, or a shard's tooth ("+kit.Family[i]+")");Check(p.Mx==0&&p.My==0&&p.Mz==0,"scenery does not move");}
        }
        Check(kept>2500&&moving==236&&scenery>3000,"the kit splits into lit boxes, movers and scenery ("+kept+", "+moving+", "+scenery+")");
        Check(kit.Movers.Length==108&&kit.MoverSlots==17&&kit.Movers.All(m=>m.Slot>=0&&m.Slot<kit.MoverSlots)&&Enumerable.Range(0,kit.MoverSlots).All(s=>kit.Movers.Any(m=>m.Slot==s)),"108 movers on 17 models ("+kit.Movers.Length+", "+kit.MoverSlots+")");
        Check(kit.Movers.Count(m=>m.Drive==NativeWardZeroKit.DriveSlat)==48&&kit.Movers.Where(m=>m.Drive==NativeWardZeroKit.DriveSlat).Select(m=>m.Slot).Distinct().Count()==4,"48 shutter slats on four models");
        // the kit's boxes are what they were: the families and movers add nothing and move nothing
        var plain=NativeWardZeroKit.Build(siren.blocks,siren.hazards,entries,false);Check(plain.Parts.Length==kit.Parts.Length&&plain.Glows.Length==kit.Glows.Length,"the kit's part and glow counts are unchanged");

        // ---- the posed blocks ----
        var art=NativeWardZeroArt.Build(kit,siren.blocks,entries);var artE=NativeWardZeroArt.Build(kitE,exp.blocks,entries);
        foreach(var b in siren.blocks)Check(!NativeHulkBreach.Eligible(siren,b)||NativeWardZeroArt.Posed(b),"a wall a Hulk may break out is posed ("+b.id+")");
        int eligible=siren.blocks.Count(b=>NativeHulkBreach.Eligible(siren,b));
        foreach(var e in entries.Where(e=>e.kind=="breach")){int at=Array.FindIndex(art.Blocks,bi=>siren.blocks[bi].id==e.block);Check(at>=0&&entries[art.BlockEntry[at]].id==e.id,"breach panel "+e.block+" is posed with its entry");}
        Check(art.Blocks.Length==128&&artE.Blocks.Length==131&&art.Slots.Length==art.MoverSlots+128&&artE.Slots.Length==art.MoverSlots+131&&art.Slots.Distinct().Count()==art.Slots.Length&&artE.Slots.Distinct().Count()==artE.Slots.Length,"128 posed blocks, 131 with the Expedition's exit frame ("+art.Blocks.Length+", "+artE.Blocks.Length+")");
        Check(art.Slots.SequenceEqual(artE.Slots.Where(s=>!s.Contains("expedition-ward-zero-exit-")))&&artE.Slots.Count(s=>s.StartsWith("b/expedition-ward-zero-exit-"))==3,"the records share their slots; the exit frame is the Expedition's");
        Check(siren.blocks.Where(NativeWardZeroArt.Posed).All(b=>b.role=="static"&&(b.rotation==null||Math.Abs(b.rotation.w)>.9999)),"posed blocks are upright static blocks");
        Check(art.BlockEntry.Count(e=>e>=0)==8&&art.MoverPart.All(p=>p>=0),"eight panels; every mover has its part");
        Check(art.Slots.Length+0<=NativeStageArtPose.Capacity&&kit.Movers.Length+art.Blocks.Length<=NativeStageArtPose.Capacity,"the poses fit the buffer");

        // ---- the poses against the kit's own Move, at every moment ----
        var pose=new NativeStageArtPose();var states=new NativeHazardState[siren.hazards.Length];var open=new float[entries.Length];var breached=new bool[entries.Length];var blockOut=new bool[siren.blocks.Length];var nudge=new float[siren.blocks.Length*3];
        var members=Enumerable.Range(0,kit.Movers.Length).Select(g=>Enumerable.Range(0,kit.Parts.Length).Where(i=>kit.MoverOf[i]==g).ToArray()).ToArray();
        var mood=NativeWardZeroMood.Preset("calm");int seen=0,hiddenSeen=0,shownSeen=0,maxDraws=0;double worst=0;
        void Moment(double t,int round)
        {
            for(int i=0;i<states.Length;i++)states[i]=NativeWardZeroHazards.At(siren.hazards[i],NativePhase.Fight,round,0,t);
            for(int e=0;e<open.Length;e++)open[e]=(float)Math.Max(0,Math.Min(1,Math.Sin(t*.37+e*.9)*.8+.3));
            art.Pose(kit,siren.blocks,states,open,mood,breached,blockOut,nudge,pose);seen++;maxDraws=Math.Max(maxDraws,pose.Count);
            // the movers come first, in order, less the hidden ones
            int d=0;
            for(int g=0;g<kit.Movers.Length;g++)
            {
                var m=kit.Movers[g];var b=siren.blocks[m.Parent];int n=m.Parent*3;bool gone=m.Entry>=0&&breached[m.Entry]||blockOut[m.Parent];
                float f=NativeWardZeroKit.Move(kit.Parts[art.MoverPart[g]],states,open,mood,out bool hidden);if(hidden)hiddenSeen++;if(hidden||gone)continue;
                Check(d<pose.Count&&pose.Slot[d]==m.Slot&&Same(pose,d,b.pos.x+m.X+m.Mx*f+nudge[n],b.pos.y+m.Y+m.My*f+nudge[n+1],b.pos.z+m.Z+m.Mz*f+nudge[n+2]),"mover "+g+" is drawn in order, at its place");shownSeen++;
                foreach(int i in members[g])
                {
                    // the box's centre: the model's point (the part's offset in its group) through the pose
                    var p=kit.Parts[i];float fp=NativeWardZeroKit.Move(p,states,open,mood,out bool hp);Check(!hp,"a drawn mover's parts are all out");At(pose,d,p.X-m.X,p.Y-m.Y,p.Z-m.Z,out double x,out double y,out double z);
                    double ex=b.pos.x+p.X+p.Mx*fp+nudge[n],ey=b.pos.y+p.Y+p.My*fp+nudge[n+1],ez=b.pos.z+p.Z+p.Mz*fp+nudge[n+2];
                    worst=Math.Max(worst,Math.Sqrt((x-ex)*(x-ex)+(y-ey)*(y-ey)+(z-ez)*(z-ez)));
                }
                d++;
            }
            // then the posed blocks that stand
            for(int i=0;i<art.Blocks.Length;i++)
            {
                int bi=art.Blocks[i];var b=siren.blocks[bi];int e=art.BlockEntry[i];if(blockOut[bi]||e>=0&&breached[e])continue;
                Check(d<pose.Count&&pose.Slot[d]==art.MoverSlots+i&&Same(pose,d,b.pos.x+nudge[bi*3],b.pos.y+nudge[bi*3+1],b.pos.z+nudge[bi*3+2]),"posed block "+b.id+" stands where its block does");d++;
            }
            Check(d==pose.Count,"nothing else is drawn ("+d+" of "+pose.Count+")");
        }
        for(int round=0;round<2;round++)for(double t=0;t<95;t+=.05)Moment(t,round);
        Check(worst<1e-4,"every mover's parts stand where the kit's boxes would (off by "+worst.ToString("0.00000")+" m at most)");
        Check(hiddenSeen>1000&&shownSeen>100000,"slats seen hidden in their lintels and out ("+hiddenSeen+", "+shownSeen+")");
        Check(maxDraws>kit.Movers.Length-48+art.Blocks.Length&&maxDraws<=kit.Movers.Length+art.Blocks.Length&&maxDraws<=NativeStageArtPose.Capacity,"with shutters down their slats are drawn too ("+maxDraws+" draws at most)");
        // a breach, a Hulk's wall, a knock
        var panel=entries.First(e=>e.kind=="breach");int pe=Array.IndexOf(entries,panel);int wall=Array.FindIndex(siren.blocks,b=>NativeHulkBreach.Eligible(siren,b)&&kit.Parts.Any(p=>siren.blocks[p.Parent]==b));
        breached[pe]=true;blockOut[wall]=true;int knocked=art.Blocks.First(bi=>bi!=wall&&art.BlockEntry[Array.IndexOf(art.Blocks,bi)]<0);nudge[knocked*3]=.03f;nudge[knocked*3+2]=-.02f;
        for(double t=0;t<30;t+=.25)Moment(t,0);
        Check(Enumerable.Range(0,pose.Count).All(i=>pose.Slot[i]!=art.MoverSlots+Array.FindIndex(art.Blocks,bi=>siren.blocks[bi].id==panel.block)&&pose.Slot[i]!=art.MoverSlots+Array.IndexOf(art.Blocks,wall)),"an open breach's panel and a broken-out wall are not drawn");
        Array.Clear(breached,0,breached.Length);Array.Clear(blockOut,0,blockOut.Length);Array.Clear(nudge,0,nudge.Length);
        // deterministic and allocation free
        Moment(12.35,1);var first=new float[pose.Count*12];Array.Copy(pose.Place,first,first.Length);int firstCount=pose.Count;Moment(40,0);Moment(12.35,1);
        Check(pose.Count==firstCount&&first.SequenceEqual(pose.Place.Take(first.Length)),"the poses are a pure function of the moment");
        for(int i=0;i<states.Length;i++)states[i]=NativeWardZeroHazards.At(siren.hazards[i],NativePhase.Fight,0,0,20);
        art.Pose(kit,siren.blocks,states,open,mood,breached,blockOut,nudge,pose);long before=GC.GetAllocatedBytesForCurrentThread();for(int k=0;k<200;k++)art.Pose(kit,siren.blocks,states,open,mood,breached,blockOut,nudge,pose);
        Check(GC.GetAllocatedBytesForCurrentThread()==before,"posing allocates nothing");

        // ---- no invisible walls ----
        using(var doc=JsonDocument.Parse(text))foreach(var m in doc.RootElement.GetProperty("maps").EnumerateArray())
        {
            string id=m.GetProperty("id").GetString();if(id!="siren"&&id!="expedition-ward-zero")continue;
            Check(m.GetProperty("pilotBlocks").GetArrayLength()==0,id+" has no pilot columns (nothing invisible keeps a pilot off a top or behind a rail)");
            // a stair's or a ramp's wedge collider is solid down to its foot's floor: the treads reach the floor and the ramp's
            // skirts close its sides, so the art under them is solid too
            foreach(var r in m.GetProperty("ramps").EnumerateArray())
            {
                string rid=r.GetProperty("id").GetString();double bottom=r.GetProperty("bottom").GetDouble();var map=id=="siren"?siren:exp;
                if(r.GetProperty("kind").GetString()=="stair")foreach(var bid in r.GetProperty("blocks").EnumerateArray()){var b=map.blocks.Single(x=>x.id==bid.GetString());Check(Math.Abs(b.pos.y-b.size.y/2-bottom)<.01,"tread "+b.id+" stands on the wedge's floor (no hollow under a solid wedge)");}
                else Check(map.blocks.Count(b=>b.id.StartsWith(rid+"-skirt-",StringComparison.Ordinal))>=16,"ramp "+rid+" is closed by its skirts");
            }
        }

        // ---- the layout export ----
        string tmp=Path.Combine(Path.GetTempPath(),"wardzero-art-layout-"+Guid.NewGuid().ToString("N")+".json");
        try
        {
            StageArtLayout.Write(root,"siren",tmp);using var doc=JsonDocument.Parse(File.ReadAllText(tmp));var r=doc.RootElement;
            Check(r.GetProperty("parts").GetArrayLength()==kit.Parts.Length&&r.GetProperty("movers").GetArrayLength()==kit.Movers.Length&&r.GetProperty("posed").GetArrayLength()==art.Blocks.Length&&r.GetProperty("entries").GetArrayLength()==entries.Length&&r.GetProperty("routes").GetArrayLength()==kit.Routes.Length,"the layout carries the kit, the movers, the posed blocks, the entries and the routes");
            Check(r.GetProperty("parts").EnumerateArray().Count(p=>p.GetProperty("kept").GetBoolean())==kept&&r.GetProperty("ward").GetProperty("moverSlots").GetInt32()==kit.MoverSlots,"the layout's kept parts and mover slots");
        }
        finally{if(File.Exists(tmp))File.Delete(tmp);}

        // ---- the wiring and the bank-absent fallback (source) ----
        string wz=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeWardZeroPresentation.cs")),core=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core/NativeStageArt.cs"));
        Check(core.Contains("(NativeWardZeroLooks.MapId,NativeWardZeroArt.Bank),(\"expedition-ward-zero\",NativeWardZeroArt.Bank)"),"the registry's line");
        Check(wz.Contains("wzArt=NativeWardZeroArt.Build(wzKit,blocks,wzEntries);")&&wz.Contains("wzArtDraw.Prepare(map,wzArt.Slots,WardArtCasts);"),"the art is prepared with the map's kit (behind the loading screen: PrewarmStage builds the kit)");
        Check(wz.Contains("if(wzArtDraw!=null&&wzArt!=null&&wzArtDraw.Ready){wzArt.Pose(wzKit,map.blocks,wzStates,wzOpen,wzMood,wzBreached,wzBlockOut,wzArtNudge,wzArtPose);WardArtDrawn=wzArtDraw.Render(arena,eye,LowGraphics,wzArtPose);}"),"the art is posed from the frame's hazard states, open amounts, mood, breaches and shudders, and drawn");
        Check(wz.Contains("DrawWardKit(arena,now,eye,WardArtDrawn);")&&wz.Contains("if(art&&!wzKit.Keeps[i])continue;"),"under the art only the kit's kept boxes draw; without it every box draws as before");
        Check(wz.Contains("wzArtNudge[i*3]=by.x;wzArtNudge[i*3+1]=by.y;wzArtNudge[i*3+2]=by.z;")&&wz.Contains("Array.Clear(wzArtNudge,0,wzArtNudge.Length);"),"a knock's shudder reaches the art and clears with the arena");
        string back=wz.Substring(wz.IndexOf("private bool WardZeroBackdrop(",StringComparison.Ordinal));back=back.Substring(0,back.IndexOf("private static bool Cutaway(",StringComparison.Ordinal));
        Check(!back.Contains("wzArt")&&!back.Contains("Keeps"),"the menu backdrop keeps the box kit");

        // ---- the bank, where it is on this machine ----
        string bankPath=Bank(root);
        if(bankPath==null)Console.WriteLine("Ward Zero art: the bank is not on this machine (git-ignored); its own checks are skipped");
        else
        {
            var bytes=File.ReadAllBytes(bankPath);var bank=NativePackBank.Read(bytes);var sp=NativeStageArt.Read(bank);
            Check(bytes.Length<=21*1048576,"the bank keeps to its budget, about 20 MB ("+(bytes.Length/1048576.0).ToString("0.0")+" MB)");
            foreach(var b in exp.blocks)Check(sp.Covers.Contains(b.id),"the art replaces block "+b.id);
            var breakable=siren.blocks.Where(b=>b.role!="static").ToArray();
            Check(breakable.Length==12&&breakable.All(b=>sp.Blocks.ContainsKey(b.id)&&sp.Blocks[b.id].All(p=>p>=0)&&sp.Blocks[b.id].Distinct().Count()>=2),"every gurney and cart has its model in its tiers");
            Check(artE.Slots.All(s=>sp.Rig.ContainsKey(s)&&sp.Rig[s].All(p=>p>=0)&&sp.Rig[s].Distinct().Count()>=2),"every rig slot has its model in its tiers ("+string.Join(",",artE.Slots.Where(s=>!sp.Rig.ContainsKey(s)).Take(6))+")");
            Check(sp.Static.Count>=20&&sp.Static.All(c=>c.Props.Distinct().Count()>=2),"the static chunks, each in its tiers");
            foreach(var b in breakable)
            {
                var prop=bank.Props[sp.Blocks[b.id][0]];double top=double.MinValue,bottom=double.MaxValue,out_=0;
                foreach(var part in prop.Parts)foreach(var v in bank.Meshes[part.Mesh].Positions){top=Math.Max(top,v.Y);bottom=Math.Min(bottom,v.Y);out_=Math.Max(out_,Math.Max(Math.Abs(v.X)-b.size.x/2,Math.Max(Math.Abs(v.Y)-b.size.y/2,Math.Abs(v.Z)-b.size.z/2)));}
                Check(out_<=.25&&top>=b.size.y/2-.12&&bottom<=-b.size.y/2+.12,"the model of "+b.id+" fills its block (out "+out_.ToString("0.00")+", top "+top.ToString("0.00")+", bottom "+bottom.ToString("0.00")+")");
            }
            // the play space: below 2 m over its floor (the ground's, the upper floor's) the Fine art keeps to the blocks (within
            // .25 m; a thin prop within .9 m); higher is overhead; nothing stands in a spawn's capsule. The posed blocks and the
            // movers count at rest.
            var boxes=exp.blocks.Select(b=>(x0:b.pos.x-b.size.x/2,x1:b.pos.x+b.size.x/2,y0:b.pos.y-b.size.y/2,y1:b.pos.y+b.size.y/2,z0:b.pos.z-b.size.z/2,z1:b.pos.z+b.size.z/2)).ToArray();
            int proud=0,total=0;double far=0;var where=new Dictionary<string,int>();var spawns=siren.spawns.ffa.Concat(siren.spawns.teams.SelectMany(t=>t.points)).Select(s=>s.pos).ToArray();int inSpawn=0;
            void Vertex(double x,double y,double z)
            {
                if(x<siren.bounds.minX||x>siren.bounds.maxX||z<siren.bounds.minZ||z>siren.bounds.maxZ)return;
                double floor=y<5.5?0:6;if(y-floor>2||y<floor-.05)return;total++;
                double best=double.MaxValue;foreach(var b in boxes){double dx=Math.Max(0,Math.Max(b.x0-x,x-b.x1)),dy=Math.Max(0,Math.Max(b.y0-y,y-b.y1)),dz=Math.Max(0,Math.Max(b.z0-z,z-b.z1));best=Math.Min(best,Math.Sqrt(dx*dx+dy*dy+dz*dz));if(best==0)break;}
                if(best>.25){proud++;var key=((int)Math.Floor(x))+","+((int)Math.Floor(y*2)/2.0)+","+((int)Math.Floor(z));where[key]=where.TryGetValue(key,out var w0)?w0+1:1;}far=Math.Max(far,best);
                foreach(var s in spawns){double dx=x-s.x,dz=z-s.z,dy=y-(s.y-1.1);if(dx*dx+dz*dz<.35*.35&&dy>.05&&dy<1.8)inSpawn++;}
            }
            foreach(var c in sp.Static)foreach(var part in bank.Props[c.Props[0]].Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)Vertex(v.X,v.Y,v.Z);
            for(int i=0;i<artE.Blocks.Length;i++){var b=exp.blocks[artE.Blocks[i]];foreach(var part in bank.Props[sp.Rig[artE.Slots[artE.MoverSlots+i]][0]].Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)Vertex(b.pos.x+v.X,b.pos.y+v.Y,b.pos.z+v.Z);}
            foreach(var m in kit.Movers){var b=siren.blocks[m.Parent];foreach(var part in bank.Props[sp.Rig["m"+m.Slot][0]].Parts)foreach(var v in bank.Meshes[part.Mesh].Positions)Vertex(b.pos.x+m.X+v.X,b.pos.y+m.Y+v.Y,b.pos.z+m.Z+v.Z);}
            int tris(int tier)=>sp.Static.Sum(c=>bank.Props[c.Props[tier]].Parts.Sum(p=>bank.Meshes[p.Mesh].Submeshes.Sum(s=>s.Indices.Length/3)));
            int rigTris(int tier)=>sp.Rig.Values.Sum(r=>bank.Props[r[tier]].Parts.Sum(p=>bank.Meshes[p.Mesh].Submeshes.Sum(s=>s.Indices.Length/3)));
            Console.WriteLine("Ward Zero art: bank "+(bytes.Length/1048576.0).ToString("0.00")+" MB, "+bank.Props.Count+" props; static triangles "+tris(0)+" / "+tris(1)+" / "+tris(2)+"; rig "+rigTris(0)+" / "+rigTris(1)+" / "+rigTris(2)+"; "+total+" play-space vertices, "+proud+" over .25 m from a block, the farthest "+far.ToString("0.00")+" m");
            if(proud>0)Console.WriteLine("Ward Zero art: proud at "+string.Join(" ",where.OrderByDescending(q=>q.Value).Take(30).Select(q=>q.Key+":"+q.Value)));
            Check(far<=.9&&proud<=total/100,"the art keeps to the blocks in the play space (farthest "+far.ToString("0.00")+" m, "+proud+" of "+total+" over .25 m)");
            Check(inSpawn==0,"nothing stands in a spawn ("+inSpawn+" vertices)");
        }
        Console.WriteLine("PASS ward zero art ("+count+" checks; "+seen+" moments posed; "+kept+" kept boxes, "+moving+" moving parts on "+kit.Movers.Length+" movers, "+scenery+" parts modelled as scenery; "+eligible+" Hulk walls among "+art.Blocks.Length+" posed blocks; no pilot columns)");
    }
    private static bool Same(NativeStageArtPose p,int i,double x,double y,double z){int o=i*12;return Math.Abs(p.Place[o+3]-x)<1e-4&&Math.Abs(p.Place[o+7]-y)<1e-4&&Math.Abs(p.Place[o+11]-z)<1e-4;}
    private static void At(NativeStageArtPose p,int i,double lx,double ly,double lz,out double x,out double y,out double z)
    {int o=i*12;var m=p.Place;x=m[o]*lx+m[o+1]*ly+m[o+2]*lz+m[o+3];y=m[o+4]*lx+m[o+5]*ly+m[o+6]*lz+m[o+7];z=m[o+8]*lx+m[o+9]*ly+m[o+10]*lz+m[o+11];}
    private static string Bank(string root)
    {
        foreach(var p in new[]{Path.Combine(root,"Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageWardZero.bytes"),"F:/Chip Arena Development/Chip-Arena-Unity/unity/ChipArenaConversion/Assets/LocalAssets/ChipArenaLocal/Resources/ChipArenaStageWardZero.bytes"})
            if(File.Exists(p))return p;
        return null;
    }
}
