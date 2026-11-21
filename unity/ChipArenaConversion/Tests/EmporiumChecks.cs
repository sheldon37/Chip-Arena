using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// Top Dog Emporium (map `emporium`, docs/arenas/EMPORIUM.md): the catalogue record (rules, Random, nav to every spawn), the hazards'
// fairness (idle through the arm delay, telegraphs at least today's), the vent push rule (NativeStageHazards.VentLaunch: only vents
// with push, only bursting, only a pilot not already rising), the look (MapLooks entry, kit kinds), the kit's collider rule and the
// roof's clearance, the backdrop's keep-out, the rigs following the replicated state (the wiener at the sweep, the bun at the ram,
// the column with the burst), determinism and no allocation per frame for the rig, the effects and the sound plan, telegraphs never
// reduced, the sound plan's cues once per cycle, and the runtime wiring. Every other map is untouched.
internal static class EmporiumChecks
{
    static int count;
    static void Check(bool ok,string what){count++;if(!ok)throw new Exception("Emporium: "+what);}
    public static void Run(string root)
    {
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);
        Check(maps.maps.Count(m=>m.id=="emporium")==1,"the map exists once");var map=maps.maps.Single(m=>m.id=="emporium");
        NativeMapRules.Validate(map);Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&content.maps.Any(m=>m.id=="emporium"&&m.rotation),"selectable and in Random");
        Check(map.look==NativeEmporiumLooks.Look&&map.name=="Top Dog Emporium"&&map.blocks.Length<=350&&map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"record shape ("+map.blocks.Length+" blocks)");
        int replicated=map.blocks.Count(b=>b.role!="static");Check(replicated<=200,"replicated props at round start ("+replicated+")");
        // nav: every spawn reachable from FFA spawn 0, attachments on the same floor within 2 m
        var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
        foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
        string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment at "+p.x+","+p.y+","+p.z);return n.id;}
        var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
        foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav to spawn "+p.pos.x+","+p.pos.z);
        Check(nodes.Values.Any(n=>Math.Abs(n.pos.y-4.5f)<.05f)&&nodes.Values.Any(n=>Math.Abs(n.pos.y-3.3f)<.05f),"nav on the balconies and the bun bridges");
        Check(map.nav.links.Count(l=>l.hazards!=null&&l.hazards.Length>0)>100,"links across the hazards are gated");

        // ---- hazards: kinds, idle through the arm delay, telegraphs
        var hz=map.hazards;Check(hz.Count(h=>h.kind=="maglev")==2&&hz.Count(h=>h.kind=="press")==2&&hz.Count(h=>h.kind=="burner")==4&&hz.Count(h=>h.kind=="vent")==4,"twelve hazards of four existing kinds");
        foreach(var h in hz)
        {
            for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)
            {
                if(h.kind=="maglev")Check(NativeCyberCityHazards.At(h,NativePhase.Fight,round,0,t).Phase==NativeHazardPhase.Idle,"arm delay "+h.id);
                else if(NativeStageHazards.Ward(h))Check(NativeWardZeroHazards.At(h,NativePhase.Fight,round,0,t).Phase==NativeHazardPhase.Idle,"arm delay "+h.id);
                else Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
            }
            if(h.kind=="maglev")Check(h.warn>=6&&h.danger>=3&&h.lethal,"wiener telegraph "+h.id);
            if(h.kind=="press")Check(h.warn>=.8&&h.lethal,"bun press telegraph "+h.id);
            if(h.kind=="burner")Check(h.warn>=1.5&&!h.lethal&&h.damage*Math.Round(h.live/h.tick)<=64,"griddle telegraph and burst "+h.id);
            if(h.kind=="vent")Check(h.warn>=.8&&h.push>0&&h.damage>0&&h.damage<=4&&!h.lethal,"geyser "+h.id);
        }
        var laneN=hz.Single(h=>h.id=="emporium-lane-n");var laneS=hz.Single(h=>h.id=="emporium-lane-s");
        Check(Math.Abs(Math.Abs(laneN.first-laneS.first)-laneN.period/2)<1e-6,"the lanes run half a period apart");
        foreach(var lane in new[]{laneN,laneS})for(double t=lane.first-lane.warn;t<lane.first+lane.period*3;t+=.01){var s=NativeCyberCityHazards.At(lane,NativePhase.Fight,1,0,t);
            if(s.Phase!=NativeHazardPhase.Active)continue;double c=NativeEmporiumRig.Centre(lane,s);Check(NativeStageHazards.TrainSweep(lane,1,0,t-.001,t,out var bc,out var bs)&&Math.Abs(c-bc.x)<NativeEmporiumRig.Radius(lane)+.2,"the drawn wiener is the sweep at "+t.ToString("0.00"));}

        // ---- the vent push rule (only push vents, only bursting, only a pilot not already rising; every shipped vent before has push 0)
        var gey=hz.First(h=>h.kind=="vent");
        double burst=gey.start+gey.offset+gey.period-gey.dwell+.3;var bs0=NativeFactoryHazards.State(gey,0,burst);Check(bs0.Phase==NativeFactoryHazards.Bursting,"geyser bursting at its window");
        var body=new V3(gey.a.x,gey.a.y+.9f,gey.a.z);
        Check(NativeStageHazards.VentLaunch(gey,bs0,body,0),"a burst launches a pilot standing in it");
        Check(!NativeStageHazards.VentLaunch(gey,bs0,body,gey.push),"a pilot already rising is not launched again");
        Check(!NativeStageHazards.VentLaunch(gey,bs0,new V3(gey.a.x+3,body.y,gey.a.z),0),"outside the column nothing");
        Check(!NativeStageHazards.VentLaunch(gey,NativeFactoryHazards.State(gey,0,burst-gey.dwell-.1),body,0),"no launch while it pumps");
        Check(Math.Pow(gey.push,2)/46>=3.6+.4,"the launch clears the balcony (apex "+(Math.Pow(gey.push,2)/46).ToString("0.0")+" m)");
        foreach(var m in maps.maps.Where(m=>m.id!="emporium"))foreach(var h in (m.hazards??Array.Empty<MapHazard>()).Where(h=>h.kind=="vent"))
        {Check(h.pushMode!=NativeStageHazards.PushLaunch,"no other map's vent launches: "+h.id);var st=NativeFactoryHazards.State(h,0,h.start+h.offset+h.period-h.dwell+.2);Check(!NativeStageHazards.VentLaunch(h,st,new V3(h.a.x,h.a.y+.9f,h.a.z),0),"no launch on "+h.id);}

        // ---- the bun press pancakes (the Factory's smash), nothing else here does
        var pw=hz.First(h=>h.kind=="press");double open=pw.period-pw.warn-pw.rise-pw.dwell-pw.travel,slamClock=pw.start+pw.offset+open+pw.warn+pw.rise*.8;
        var smash=NativeSmashGib.Plan(true,"impact-ragdoll",false,"pilot","hazard","emporium",hz,pw.id,0,0,4+slamClock,new System.Numerics.Vector3(pw.a.x,.9f,pw.a.z),System.Numerics.Vector3.Zero,0,7);
        Check(smash!=null&&smash.Cause==SmashCause.Press,"a bun press death pancakes");
        Check(NativeSmashGib.Plan(true,"impact-ragdoll",false,"pilot","hazard","emporium",hz,gey.id,0,0,4+burst,new System.Numerics.Vector3(gey.a.x,.9f,gey.a.z),System.Numerics.Vector3.Zero,0,7)==null,"a geyser death is no smash");
        // ---- the look
        var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=set.maps.Single(l=>l.id=="emporium");
        Check(look.look==NativeEmporiumLooks.Look&&look.toon!=null&&look.toon.on&&look.advanced!=null&&look.advanced.beams.on&&look.blocks.dressed&&(look.lights?.Length??0)<=8,"the golden-hour look: styled, dressed, advanced");
        Check(look.kitLooks.All(k=>NativeEmporiumLooks.Kinds.Contains(k.block))&&NativeEmporiumLooks.Kinds.Length<=NativeFactoryKit.Kinds.Length,"kit looks name kit kinds");
        var kinds=NativeEmporiumLooks.Resolve(look.kitLooks);Check((kinds.Flags[NativeEmporiumLooks.Tube]&NativeCyberCityLooks.Bank)!=0&&kinds.Look[NativeEmporiumLooks.Tube]==NativeCyberCityLooks.Tube,"neon resolves to the tube look");
        var fallback=NativeEmporiumLooks.Resolve(null);Check(fallback.Look[NativeEmporiumLooks.Griddle]==NativeWardZeroLooks.Burn&&(fallback.Flags[NativeEmporiumLooks.Griddle]&NativeCyberCityLooks.Bank)!=0,"defaults keep the bank flag (the griddle burner)");
        foreach(var b in map.blocks)Check(NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0xFFFFFF).Listed,"block look for "+b.id);

        // ---- the kit: attached parts inside their block grown by Proud below 3 m; overhead parts clear of standable tops; the roof high
        var kit=NativeEmporiumKit.Build(map,look);Check(kit!=null&&kit.Count>1000&&kit.Count<NativeFactoryKit.MaxParts,"kit size ("+kit.Count+")");
        var stems=Enumerable.Range(0,kit.Count).GroupBy(i=>kit.Parent[i]<0?"(overhead)":Regex.Replace(map.blocks[kit.Parent[i]].id,@"-(n|s|e|w|a|b|\d+)(?=-|$)","")).Select(g=>(g.Key,g.Count())).OrderByDescending(x=>x.Item2).Take(10);
        Console.WriteLine("   kit parts by stem: "+string.Join(", ",stems.Select(x=>x.Item1.Replace("emporium-","")+" "+x.Item2)));
        var unstandable=map.pilotBlocks;
        bool Standable(ContentBlock b)=>!unstandable.Any(p=>!p.bots&&Math.Abs(p.center.x-b.pos.x)<.01&&Math.Abs(p.center.z-b.pos.z)<.01&&Math.Abs(p.bottom-(b.pos.y+b.size.y/2-.1f))<.02);
        int over=0;
        for(int i=0;i<kit.Count;i++)
        {
            var p=kit.Parts.Items[i];Extent(p,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
            if(kit.Parent[i]>=0)
            {
                var b=map.blocks[kit.Parent[i]];double g=NativeEmporiumKit.Proud+.01;
                bool inside=x0>=b.pos.x-b.size.x/2-g&&x1<=b.pos.x+b.size.x/2+g&&z0>=b.pos.z-b.size.z/2-g&&z1<=b.pos.z+b.size.z/2+g&&y0>=b.pos.y-b.size.y/2-g;
                Check(inside||y0>=3.0,"kit part "+i+" stays on "+b.id);
            }
            else
            {
                over++;double top=0;foreach(var b in map.blocks){if(b.pos.x+b.size.x/2<x0||b.pos.x-b.size.x/2>x1||b.pos.z+b.size.z/2<z0||b.pos.z-b.size.z/2>z1)continue;double t=b.pos.y+b.size.y/2;if(t<=y0+.01&&Standable(b))top=Math.Max(top,t);}
                Check(y0>=top+NativeEmporiumKit.Clearance-1e-6,"overhead part "+i+" clears the tops under it ("+y0.ToString("0.00")+" over "+top.ToString("0.00")+")");
            }
        }
        Check(over>150,"the roof, pendants and conveyor rails ("+over+")");
        var k2=NativeEmporiumKit.Build(map,look);Check(k2.Count==kit.Count&&Enumerable.Range(0,kit.Count).All(i=>kit.Parts.Items[i].Equals(k2.Parts.Items[i])),"the kit is the same every build");

        // ---- the backdrop: built inside the keep-out (the builder throws), three sectors
        var sectors=NativeEmporiumScenery.Build(map);Check(sectors.Length==3&&sectors.All(s=>s.Boxes>20),"the backdrop's sectors ("+string.Join(",",sectors.Select(s=>s.Boxes))+")");
        Check(NativeEmporiumScenery.Build(maps.maps.First(m=>m.id=="factory"))==null&&NativeEmporiumKit.Build(maps.maps.First(m=>m.id=="factory"),look)==null,"other maps build nothing");

        // ---- the rig: the replicated state only, deterministic, allocation free, never past its capacity
        var rig=new NativeEmporiumRig(kinds);double press=0;
        for(double t=0;t<80;t+=.37)
        {
            rig.Build(map.id,hz,map.blocks,1,NativePhase.Fight,0,t,t);Check(rig.Parts.Count<NativeEmporiumRig.Capacity-50,"rig capacity at "+t);
            for(int i=0;i<hz.Length;i++)
            {
                var h=hz[i];
                if(h.kind=="maglev"){var s=NativeCyberCityHazards.At(h,NativePhase.Fight,1,0,t);double c=NativeEmporiumRig.Centre(h,s);bool shown=!double.IsNaN(rig.WienerX[i]);
                    if(s.Phase==NativeHazardPhase.Active&&Math.Abs(c)<h.size.x/2)Check(shown&&Math.Abs(rig.WienerX[i]-c)<1e-9,"the wiener at the sweep");if(shown&&(s.Phase==NativeHazardPhase.Warn||s.Phase==NativeHazardPhase.Danger)){double near=h.center.x-s.Direction*h.size.x/2;Check((rig.WienerX[i]+s.Direction*NativeEmporiumRig.Radius(h)-near)*s.Direction<=.01,"the wiener waits inside the oven until the pass");}
                    if(shown)Check(2*NativeEmporiumRig.Radius(h)+NativeEmporiumRig.Bob(c)<=h.size.y+1e-6&&NativeEmporiumRig.Long(h)+NativeEmporiumRig.Radius(h)*.4<=h.size.z/2,"the drawn wiener stays inside the sweep's box");}
                if(h.kind=="press"){var st=NativeFactoryHazards.State(h,1,NativeFactoryHazards.Clock(NativePhase.Fight,0,t));Check(Math.Abs(rig.BunBottom[i]-st.LoadY)<1e-9,"the bun's underside is the ram's");press++;}
                if(h.kind=="vent"){var st=NativeFactoryHazards.State(h,1,NativeFactoryHazards.Clock(NativePhase.Fight,0,t));Check((rig.ColumnTop[i]>0)==(st.Phase==NativeFactoryHazards.Bursting&&st.U*5.2>=.1),"the sauce column with the burst");}
            }
        }
        Check(press>0,"presses sampled");
        rig.Build(map.id,hz,map.blocks,1,NativePhase.Intro,0,10,10);Check(hz.Select((h,i)=>i).All(i=>double.IsNaN(rig.WienerX[i])&&rig.ColumnTop[i]==0),"parked outside the fight");
        var r2=new NativeEmporiumRig(kinds);rig.Build(map.id,hz,map.blocks,0,NativePhase.Fight,3,31.7,31.7);r2.Build(map.id,hz,map.blocks,0,NativePhase.Fight,3,31.7,31.7);
        Check(rig.Parts.Count==r2.Parts.Count&&Enumerable.Range(0,rig.Parts.Count).All(i=>rig.Parts.Items[i].Equals(r2.Parts.Items[i])&&rig.Role[i]==r2.Role[i]),"the rig is deterministic");
        var r3=new NativeEmporiumRig(kinds);r3.Build(map.id,hz,map.blocks,0,NativePhase.Fight,8,36.7,31.7);Check(r3.Parts.Count==rig.Parts.Count&&Enumerable.Range(0,rig.Parts.Count).All(i=>rig.Parts.Items[i].Equals(r3.Parts.Items[i])),"the same at any FightAt");
        r2.Build("factory",hz,map.blocks,0,NativePhase.Fight,0,20,20);Check(r2.Parts.Count==0,"nothing on another map");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<240;f++)rig.Build(map.id,hz,map.blocks,1,NativePhase.Fight,0,f/60.0+12,f/60.0+12);
        Check(GC.GetAllocatedBytesForCurrentThread()-before<1024,"the rig allocates nothing per frame ("+(GC.GetAllocatedBytesForCurrentThread()-before)+" B)");

        // ---- the effects: telegraphs never reduced; capacity; allocation free; nothing on another map
        var fx=new NativeEmporiumFx();fx.Prepare(map);
        foreach(double t in new[]{9.0,11.5,12.5,15.4,16.0,18.3,22.6,27.0,33.5,45.0})
        {
            fx.Build(hz,1,NativePhase.Fight,0,t,t,0,6,-20,1,0,0,0,1,0,1,false);int full=fx.Telegraph,all=fx.Quads.Count;
            fx.Build(hz,1,NativePhase.Fight,0,t,t,0,6,-20,1,0,0,0,1,0,0,true);Check(fx.Telegraph==full,"telegraphs never reduced at "+t);Check(fx.Quads.Count<=all,"the low tier draws less at "+t);
            Check(all<NativeEmporiumFx.Capacity-100,"effects capacity at "+t+" ("+all+")");
        }
        before=GC.GetAllocatedBytesForCurrentThread();for(int f=0;f<240;f++)fx.Build(hz,1,NativePhase.Fight,0,f/60.0+12,f/60.0+12,0,6,-20,1,0,0,0,1,0,1,false);
        Check(GC.GetAllocatedBytesForCurrentThread()-before<1024,"the effects allocate nothing per frame");
        var other=new NativeEmporiumFx();other.Prepare(maps.maps.First(m=>m.id=="factory"));other.Build(hz,1,NativePhase.Fight,0,20,20,0,6,-20,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"no effects on another map");

        // ---- the sound plan: every cue once whatever the windows; the samples
        var cues=new EmporiumCue[256];var all1=new List<(int,int,int)>();var all2=new List<(int,int,int)>();
        int n1=NativeEmporiumSound.Cues(hz,1,0,0,60,cues);for(int i=0;i<n1;i++)all1.Add((cues[i].Kind,cues[i].Hazard,cues[i].Cycle));
        for(double t=0;t<60;t+=1/60.0){int n=NativeEmporiumSound.Cues(hz,1,0,t,t+1/60.0,cues);for(int i=0;i<n;i++)all2.Add((cues[i].Kind,cues[i].Hazard,cues[i].Cycle));}
        Check(n1>40&&all1.OrderBy(x=>x).SequenceEqual(all2.OrderBy(x=>x)),"each cue once across any windows ("+n1+")");
        Check(Enumerable.Range(0,NativeEmporiumSound.Keys.Length).All(k=>all1.Any(c=>c.Item1==k)),"every cue kind plays in a minute");
        for(int k=0;k<NativeEmporiumSound.Keys.Length;k++){var s=NativeEmporiumSound.Samples(k);Check(s!=null&&s.Length>1000&&s.All(v=>v>=-1&&v<=1)&&s.Any(v=>Math.Abs(v)>.05),"samples "+NativeEmporiumSound.Keys[k]);}
        Check(NativeEmporiumSound.Cues(hz,1,0,double.NegativeInfinity,3.9,cues)==0,"nothing before the hazards arm");

        // ---- wiring and files
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
        Check(R("Runtime/NativeSourcePresentation.cs").Contains("EmporiumScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmEmporium(arena))built++;"),"SourceWorld and PrewarmStage hooks");
        Check(R("Runtime/NativeAudio.cs").Contains("EmporiumCues(arena,menu);"),"the audio hook");
        Check(R("Runtime/NativeStageHazardsRuntime.cs").Contains("NativeStageHazards.VentLaunch(h,s,Vec(p.Body.position),p.Body.linearVelocity.y))p.Knockback.y=Math.Max(p.Knockback.y,h.push);"),"the host applies the vent launch");
        var pres=R("Runtime/NativeEmporiumPresentation.cs");Check(pres.Contains("NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz)")&&pres.Contains("NativeSceneryBuilder.BackdropLayer")&&pres.Contains("ShadowCastingMode.Off"),"the draw path");
        var guids=new HashSet<string>();
        foreach(var f in new[]{"Core/NativeEmporiumLooks.cs","Core/NativeEmporiumRig.cs","Core/NativeEmporiumKit.cs","Core/NativeEmporiumFx.cs","Core/NativeEmporiumScenery.cs","Core/NativeEmporiumSound.cs","Runtime/NativeEmporiumPresentation.cs","Runtime/NativeEmporiumAudio.cs"})
        {var m=Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})");Check(m.Success&&guids.Add(m.Groups[1].Value),"meta for "+f);}
        Check(File.Exists(Path.Combine(root,"../../docs/arenas/EMPORIUM.md")),"the stage's doc");
        Console.WriteLine("PASS Top Dog Emporium: "+count+" checks ("+map.blocks.Length+" blocks, "+kit.Count+" kit parts, "+sectors.Sum(s=>s.Boxes)+" backdrop boxes, "+map.nav.nodes.Length+" nav nodes)");
    }
    private static void Extent(FactoryPart p,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)
    {
        var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};double ex=0,ey=0,ez=0;
        foreach(var (ax,ay,az,h) in new[]{(1.0,0.0,0.0,(double)p.Hx),(0.0,1.0,0.0,(double)p.Hy),(0.0,0.0,1.0,(double)p.Hz)}){q.Rotate(ax*h,ay*h,az*h,out double rx,out double ry,out double rz);ex+=Math.Abs(rx);ey+=Math.Abs(ry);ez+=Math.Abs(rz);}
        x0=p.X-ex;x1=p.X+ex;y0=p.Y-ey;y1=p.Y+ey;z0=p.Z-ez;z1=p.Z+ez;
    }
}
