using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
// Substation 128 (map `club`, docs/arenas/CLUB.md): the map's rules and random selection, navigation, the hazards on the
// 128 bpm bar grid (telegraph lengths, the arm delay, no overlaps, the damage), the vent shove, the look, the kit's collider
// rule, the backdrop's keep-out, the show's telegraph ownership and colour rule, the music pulse and its flash caps, no
// allocation per frame, and the runtime wiring. `CoreTests --club-stage` (also in the full run).
internal static class ClubStageChecks
{
    private static int count;
    private static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Club stage: "+why);}
    public static void Run(string root)
    {
        count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string p)=>File.ReadAllText(Path.Combine(root,p));
        var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);
        var text=R("Assets/Conversion/Resources/UnityMaps.json");var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);
        NativeMapRules.Merge(content,maps,text);
        Check(maps.maps.Count(m=>m.id=="club")==1,"the club exists once");var map=maps.maps.Single(m=>m.id=="club");
        NativeMapRules.Validate(map);Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(map),"selectable and in Random");
        Check(map.look==NativeClubLooks.LookId&&map.name=="Substation 128"&&map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"look, name, block and prop budgets");
        Nav(map);Hazards(map);Shove(root,map,R);
        var set=JsonSerializer.Deserialize<MapLookSet>(R("Assets/Conversion/Resources/MapLooks.json"),o);Look(set,map);
        var look=NativeMapStyle.Styled(NativeMapLooks.ByLook(set,map.look),false);Kit(map,look);Scenery(map);Show(map,look);Pulse();Wiring(root,R);
        Console.WriteLine("PASS Club stage (Substation 128): "+count+" checks (rules, nav, the bar grid's hazards, the shove, the look, the kit's collider rule, the backdrop's keep-out, the show's telegraphs, the pulse, no allocation, the wiring)");
    }

    // ---- navigation: every spawn joins FFA spawn 0's graph on its own floor
    private static void Nav(ContentMap map)
    {
        var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
        foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
        string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
        var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
        while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
        foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
        // the upper level, the stage and the catwalk are on the graph
        foreach(var p in new[]{new V3(-21,5.1f,-15),new V3(0,5.1f,9.9f),new V3(0,1.9f,17.5f),new V3(-12,5.1f,-17)})Check(seen.Contains(Near(p)),"nav reaches "+p.x+","+p.y+","+p.z);
        Check(map.nav.links.Count(l=>l.hazards.Length>0)>0&&map.nav.links.Where(l=>l.hazards.Length>0).All(l=>l.hazards.All(id=>map.hazards.Any(h=>h.id==id))),"links over the floor are gated by its hazards");
    }

    // ---- the hazards on the bar grid
    private static void Hazards(ContentMap map)
    {
        double beat=NativeClubLooks.Beat,bar=NativeClubLooks.Bar,arm=NativeClubLooks.ArmDelay;
        Check(Math.Abs(beat-.46875)<1e-12&&Math.Abs(bar-1.875)<1e-12&&Math.Abs(NativeClubLooks.Phrase-30)<1e-9,"128 bpm: a beat .46875 s, a bar 1.875 s, a phrase 30 s");
        var grids=map.hazards.Where(h=>h.kind=="grid").ToArray();var laser=map.hazards.Single(h=>h.kind=="maglev");var drops=map.hazards.Where(h=>h.kind=="vent").ToArray();
        Check(grids.Length==9&&drops.Length==2&&map.hazards.Length==12,"nine floor sections, a laser wall, two drop vents");
        bool OnBar(double t){double b=(t-arm)/bar;return Math.Abs(b-Math.Round(b))<1e-6;}
        foreach(var g in grids)
        {
            Check(g.charge>=2.5&&g.live>0&&g.charge+g.live+1<=g.period&&!g.lethal&&g.damage>0,"grid timing "+g.id);
            Check(OnBar(g.first+g.period-g.live-1)&&Math.Abs(g.charge-6*beat)<1e-6&&Math.Abs(g.live-bar)<1e-6&&Math.Abs(g.period-8*bar)<1e-6,"grid "+g.id+": six beats' charge, live one bar, on the bar grid");
            int s=NativeClubShow.SectionAt(g.center.x,g.center.z);Check(s>=0&&Math.Abs(g.size.x-6)<1e-4&&Math.Abs(g.size.z-5)<1e-4,"grid "+g.id+" fills a 6 x 5 m section");
        }
        Check(grids.Select(g=>NativeClubShow.SectionAt(g.center.x,g.center.z)).Distinct().Count()==9,"the nine sections tile the floor");
        Check(laser.lethal&&laser.warn>=6&&laser.danger>=3&&OnBar(laser.first)&&Math.Abs(laser.period-16*bar)<1e-6&&laser.first-laser.warn>=arm,"the laser wall: warned 4 bars, danger 2, arrives on a bar each phrase");
        Check(Math.Abs(laser.size.x-18)<1e-4&&Math.Abs(laser.size.z-15)<1e-4&&laser.center.y+laser.size.y/2<=.66f,"the laser wall sweeps the dance floor at 0.65 m (jumpable)");
        foreach(var d in drops)
        {
            double burst=arm+d.start+d.offset+d.period-d.dwell;
            Check(d.warn>=.8&&!d.lethal&&d.push>0&&d.damage>0&&OnBar(burst)&&Math.Abs((burst-arm)/bar%16)<1e-6&&Math.Abs(d.period-16*bar)<1e-6&&Math.Abs(d.warn-2*bar)<1e-6,"the drop "+d.id+": warned 2 bars, its burst lands on the phrase's downbeat");
        }
        // the first 4 s are idle; the two patterns never live together; the laser never passes over a live section
        for(int round=0;round<3;round++)for(double t=0;t<240;t+=.05)
        {
            int liveX=0,livePlus=0;
            foreach(var g in grids){var st=NativeCyberCityHazards.At(g,NativePhase.Fight,round,0,t);if(t<4)Check(st.Phase==NativeHazardPhase.Idle,"arm delay "+g.id);if(st.Phase==NativeHazardPhase.Active){bool plus=g.id.EndsWith("-c-n")||g.id.EndsWith("-c-s")||g.id.EndsWith("-w-c")||g.id.EndsWith("-e-c");if(plus)livePlus++;else liveX++;}}
            Check(liveX==0||livePlus==0,"X and + never live together at "+t);Check(liveX==0||liveX==5,"the X lives whole");Check(livePlus==0||livePlus==4,"the + lives whole");
            var ls=NativeCyberCityHazards.At(laser,NativePhase.Fight,round,0,t);if(t<4)Check(ls.Phase==NativeHazardPhase.Idle,"arm delay laser");Check(!(ls.Phase==NativeHazardPhase.Active&&liveX+livePlus>0),"the laser passes over a quiet floor at "+t);
            foreach(var d in drops){var ds=NativeFactoryHazards.State(d,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t));if(t<4)Check(ds.Phase==NativeFactoryHazards.Parked,"arm delay "+d.id);}
        }
        var full=grids[0];Check(NativeStageHazards.DamageTicks(full,1,0,full.first+full.period-full.live-1-.001,full.first+full.period-1-.001)*full.damage==80,"a full live window is 80 HP");
        var dw=drops[0];double b0=arm+dw.start+dw.offset+dw.period-dw.dwell;Check(NativeStageHazards.DamageTicks(dw,1,0,b0-.001,b0+dw.dwell-.001)==3,"the drop ticks three times");
        Check(NativeDevHazardLook.Plan("grid",new V3(),NativeDevHazardLook.Reach(full),new NativeImpactCommand[NativeImpactCues.MaxCommands])>0&&NativeDevHazardLook.Plan("maglev",new V3(),NativeDevHazardLook.Reach(laser),new NativeImpactCommand[NativeImpactCues.MaxCommands])>0&&NativeDevHazardLook.Plan("vent",new V3(),NativeDevHazardLook.Reach(dw),new NativeImpactCommand[NativeImpactCues.MaxCommands])>0,"every club hazard kind has its DEV pulse");
    }

    // ---- the vent shove (the one rules change: docs/arenas/CLUB.md "For Astra")
    private static void Shove(string root,ContentMap map,Func<string,string> R)
    {
        var drop=map.hazards.Single(h=>h.id=="club-drop-w");float k=NativeStageHazards.VentShove(drop,out var dir);
        Check(Math.Abs(k-drop.push)<1e-6&&Math.Abs(dir.z+1)<1e-6&&dir.x==0&&dir.y==0,"the drop shoves south along its axis");
        Check(NativeStageHazards.VentShove(new MapHazard{kind="vent",push=5,axis=new V3(0,1,0)},out _)==0&&NativeStageHazards.VentShove(new MapHazard{kind="grid",push=5,axis=new V3(1,0,0)},out _)==0&&NativeStageHazards.VentShove(new MapHazard{kind="vent",push=float.NaN,axis=new V3(1,0,0)},out _)==0,"no shove without a horizontal axis, off a vent, or with a bad push");
        var o=new JsonSerializerOptions{IncludeFields=true};var all=JsonSerializer.Deserialize<UnityMapContent>(R("Assets/Conversion/Resources/UnityMaps.json"),o);
        Check(all.maps.Where(m=>m.id!="club").SelectMany(m=>m.hazards??Array.Empty<MapHazard>()).Where(h=>h.kind=="vent").All(h=>NativeStageHazards.VentShove(h,out _)==0),"every other map's vents keep push 0 (unchanged)");
        var rt=R("Assets/ChipArena/Runtime/NativeStageHazardsRuntime.cs");
        Check(rt.Contains("float shove=NativeStageHazards.VentShove(h,out var away);if(shove>0){velocity=Vec(away)*shove;impulse=shove*ticks*65;}")&&rt.Contains("velocity.normalized,impulse,false,forced:lethal"),"the authority applies the shove per damage tick through Damage's knockback");
    }

    // ---- the look
    private static void Look(MapLookSet set,ContentMap map)
    {
        NativeMapLooks.Validate(set);var l=NativeMapLooks.ByLook(set,map.look);Check(l!=null&&l.id=="club","the look exists");
        Check(NativeMapStyle.On(l,false)&&l.blocks.dressed&&l.sky.variant=="night","styled, dressed, a night sky through the windows");
        var a=l.advanced;Check(a.gi.on&&a.scatter.on&&a.beams.on&&a.weather.kind=="drizzle"&&a.weather.amount==0&&a.weather.wet>0&&a.weather.lightning==0,"advanced: the volume, thick haze, beams and a polished floor (the drizzle's sheen, no fall)");
        Check(l.lights.Length<=5,"five steady practicals: three slots stay free for the show's lights");
        foreach(var b in map.blocks){var c=NativeBlockLooks.Resolve(l.blockLooks,null,b.id,b.material,0xffffff);Check(c.Listed,"every block has a look: "+b.id);}
        var cube=NativeBlockLooks.Resolve(l.blockLooks,null,"club-cube","metal",0);Check((cube.Flags&NativeCyberCityLooks.Bank)!=0&&cube.Look==NativeCyberCityLooks.Holo,"the cube is a holo screen");
        var looks=NativeClubLooks.Resolve(l.kitLooks);
        for(int k=0;k<NativeClubLooks.Kinds.Length;k++){bool bank=(looks.Flags[k]&NativeCyberCityLooks.Bank)!=0;Check(bank?NativeCyberCityLooks.Keys[looks.Look[k]]!=null:looks.Look[k]>=0&&looks.Look[k]<NativeBlockLooks.Keys.Length,"kit kind "+NativeClubLooks.Kinds[k]+" resolves");}
        Check((looks.Flags[NativeClubLooks.Led]&NativeCyberCityLooks.Bank)!=0&&looks.Look[NativeClubLooks.Led]==NativeCyberCityLooks.Tube&&looks.Look[NativeClubLooks.Screen]==NativeCyberCityLooks.Holo&&looks.Look[NativeClubLooks.Hazard]==NativeBlockLooks.Demolition,"LEDs are tubes, screens are holo, the drop zones are hazard stripes");
    }

    // ---- the kit: attached parts inside their block grown by 0.25 m, overhead parts 2 m over every standable top
    private static void Kit(ContentMap map,MapLook look)
    {
        var a=NativeClubKit.Build(map,look);var b=NativeClubKit.Build(map,look);Check(a!=null&&a.Count>800&&a.Count<=NativeFactoryKit.MaxParts,"the kit builds ("+a?.Count+" parts)");
        Check(a.Count==b.Count&&Enumerable.Range(0,a.Count).All(i=>a.Parts.Items[i].Equals(b.Parts.Items[i])),"the kit is the same every build");
        Check(NativeClubKit.Build(map.id=="club"?new ContentMap{id="factory",blocks=map.blocks}:map,look)==null,"no kit on another map");
        var tops=new List<(double x0,double x1,double z0,double z1,double y)>();
        foreach(var k in map.blocks)
        {
            double top=k.pos.y+k.size.y/2,x0=k.pos.x-k.size.x/2,x1=k.pos.x+k.size.x/2,z0=k.pos.z-k.size.z/2,z1=k.pos.z+k.size.z/2;
            bool capped=map.pilotBlocks.Any(p=>p.bottom<=top+.01f&&Math.Abs(p.center.x-k.pos.x)<.01&&Math.Abs(p.center.z-k.pos.z)<.01);
            if(!capped)tops.Add((x0,x1,z0,z1,top));
        }
        int attached=0,overhead=0;
        for(int i=0;i<a.Count;i++)
        {
            var p=a.Parts.Items[i];Extent(p,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1);
            if(a.Overhead[i]){overhead++;foreach(var t in tops)if(x1>t.x0&&x0<t.x1&&z1>t.z0&&z0<t.z1&&t.y<y0+.01)Check(y0-t.y>=NativeClubKit.Clearance-1e-3,"overhead part "+i+" stands "+(y0-t.y).ToString("0.00")+" m over a top");continue;}
            attached++;var k=map.blocks[a.Parent[i]];float g=NativeClubKit.Proud+.001f;
            Check(x0>=k.pos.x-k.size.x/2-g&&x1<=k.pos.x+k.size.x/2+g&&y0>=k.pos.y-k.size.y/2-g&&y1<=k.pos.y+k.size.y/2+g&&z0>=k.pos.z-k.size.z/2-g&&z1<=k.pos.z+k.size.z/2+g,"part "+i+" stays inside "+k.id+" grown by 0.25 m");
        }
        Check(attached>500&&overhead>300,"attached ("+attached+") and overhead ("+overhead+") parts");
    }
    private static void Extent(in FactoryPart p,out double x0,out double x1,out double y0,out double y1,out double z0,out double z1)
    {
        var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};double ex=0,ey=0,ez=0;
        foreach(var (ax,ay,az,h) in new[]{(1.0,0.0,0.0,(double)p.Hx),(0.0,1.0,0.0,(double)p.Hy),(0.0,0.0,1.0,(double)p.Hz)}){q.Rotate(ax*h,ay*h,az*h,out double rx,out double ry,out double rz);ex+=Math.Abs(rx);ey+=Math.Abs(ry);ez+=Math.Abs(rz);}
        x0=p.X-ex;x1=p.X+ex;y0=p.Y-ey;y1=p.Y+ey;z0=p.Z-ez;z1=p.Z+ez;
    }

    // ---- the backdrop: builds (its keep-out throws on any box in the play space), the same every time
    private static void Scenery(ContentMap map)
    {
        var a=NativeClubScenery.Build(map);var b=NativeClubScenery.Build(map);Check(a!=null&&a.Length==NativeClubScenery.Sectors&&a.All(m=>m.Boxes>0),"the backdrop's three sectors build");
        Check(Enumerable.Range(0,a.Length).All(i=>a[i].Positions.SequenceEqual(b[i].Positions)&&a[i].Colors.SequenceEqual(b[i].Colors)),"the backdrop is the same every build");
        var s=new NativeClubScenery.Sink{MinX=-27,MaxX=27,MinZ=-21,MaxZ=21,Under=-.4,Ceiling=12};bool threw=false;try{s.Box(0,6,0,1,1,1,0xffffff);}catch(InvalidOperationException){threw=true;}
        Check(threw&&!NativeClubScenery.Enters(s,-1,1,12,14,-1,1)&&!NativeClubScenery.Enters(s,-1,1,-2,-.4,-1,1)&&!NativeClubScenery.Enters(s,27.05,30,0,8,-1,1),"the keep-out: refused in the play space, allowed over the ceiling, under the floor and past the margin");
        Check(NativeClubScenery.Build(new ContentMap{id="factory"})==null,"no backdrop on another map");
    }

    // ---- the show: telegraph ownership, the colour rule, never reduced, the rig and the effects, no allocation
    private static void Show(ContentMap map,MapLook look)
    {
        var show=new NativeClubShow();show.Prepare(map);var pulse=new NativeClubPulse();var rig=new NativeClubRig(NativeClubLooks.Resolve(look.kitLooks));var fx=new NativeClubFx();
        var grids=map.hazards.Where(h=>h.kind=="grid").ToArray();var laser=map.hazards.Single(h=>h.kind=="maglev");
        for(int s=0;s<NativeClubShow.Sections;s++)Check(show.GridHazard[s]!=null,"section "+s+" has its hazard");Check(show.LaserHazard!=null&&show.DropHazardW!=null&&show.DropHazardE!=null,"the laser and the drops are found");
        int maxRig=0,maxFx=0;
        for(double t=0;t<64;t+=.1)foreach(var flash in new[]{NativeKitFlash.Full,NativeKitFlash.Reduced,NativeKitFlash.Calm})
        {
            pulse.Clock(NativeClubLooks.Beats(NativePhase.Fight,0,t,t),flash);show.Build(1,NativePhase.Fight,0,t,t,pulse,flash);
            for(int i=0;i<NativeClubLooks.Tiles;i++)
            {
                float r=show.Floor[i*3],g=show.Floor[i*3+1],b=show.Floor[i*3+2];Check(!float.IsNaN(r+g+b)&&r>=0&&g>=0&&b>=0,"tile colours finite");
                int s=NativeClubShow.SectionAt(NativeClubShow.TileX(i),NativeClubShow.TileZ(i));var st=show.Grid[s];
                if(st.Phase==NativeHazardPhase.Warn)Check(show.Owner[i]==NativeClubShow.Charging,"a charging section owns its tiles at "+t.ToString("0.0"));
                if(st.Phase==NativeHazardPhase.Active)Check(show.Owner[i]==NativeClubShow.Live&&r>2,"a live section owns its tiles, red-hot, at "+t.ToString("0.0"));
                if(show.Owner[i]==NativeClubShow.Party&&r>.05)Check(b>=.25*r,"the party never plays a telegraph colour (tile "+i+" at "+t.ToString("0.0")+")");
                if(show.Owner[i]==NativeClubShow.Charging)Check(b<.25*r&&r>.3,"a charging tile is amber to red");
            }
            if(show.Laser.Phase==NativeHazardPhase.Danger){int col=0;for(int i=0;i<NativeClubLooks.Tiles;i++)if(Math.Abs(NativeClubShow.TileX(i)-(laser.center.x-show.Laser.Direction*(laser.size.x/2-.5)))<.01&&(show.Owner[i]==NativeClubShow.LaserLine||show.Owner[i]==NativeClubShow.Charging||show.Owner[i]==NativeClubShow.Live||show.Owner[i]==NativeClubShow.Discharge))col++;Check(col==NativeClubLooks.TilesZ,"the laser's start column is red through its danger (a section telegraphing over it keeps its own)");}
            foreach(bool low in new[]{false,true})
            {
                rig.Build(show,t,low,1);Check(rig.Parts.Count<NativeClubRig.Capacity&&rig.Tiles==NativeClubLooks.Tiles&&rig.Panels==NativeClubShow.WallW*NativeClubShow.WallH+NativeClubShow.CubePanels,"the rig draws every tile and panel (low "+low+")");maxRig=Math.Max(maxRig,rig.Parts.Count);
                fx.Build(show,t,0,1.6,-12,1,0,0,0,1,0,low?.34:1,low);int tele=fx.Telegraph;fx.Build(show,t,0,1.6,-12,1,0,0,0,1,0,.3,true);Check(fx.Telegraph==tele,"telegraph effects are never reduced at "+t.ToString("0.0"));maxFx=Math.Max(maxFx,fx.Quads.Count);Check(fx.Quads.Count<NativeClubFx.Capacity,"the effects fit");
            }
        }
        // the drop: the countdown, the blast's flash and confetti, the most loaded moment
        var drop=show.DropHazardW;double burst=NativeClubLooks.ArmDelay+drop.start+drop.offset+drop.period-drop.dwell;
        show.Build(1,NativePhase.Fight,0,burst-.1,burst-.1,pulse,NativeKitFlash.Full);Check(show.Rise>.9&&show.Blast==0,"the drop rises to its burst");
        show.Build(1,NativePhase.Fight,0,burst+drop.dwell+.2,burst+drop.dwell+.2,pulse,NativeKitFlash.Full);Check(show.Blast>0&&show.BlastAge<1,"the blast follows the burst");
        fx.Build(show,burst+1,0,1.6,-12,1,0,0,0,1,0,1,false);Check(fx.Quads.Count<NativeClubFx.Capacity&&fx.Show>200,"confetti and jets at the drop ("+fx.Show+")");
        // behind the fight (the countdown, the menu) the floor plays on the render clock and nothing telegraphs
        show.Build(1,NativePhase.Countdown,0,50,50,pulse,NativeKitFlash.Full);Check(!show.Warned&&show.Owner.All(x=>x==NativeClubShow.Party),"no telegraph outside the fight");
        // no allocation per frame (pulse, show, rig, effects)
        var spectrum=new float[NativeClubPulse.Bins];for(int i=0;i<spectrum.Length;i++)spectrum[i]=.01f/(1+i);
        for(int i=0;i<30;i++){pulse.Feed(spectrum,spectrum.Length,48000,1/60.0,i*.1,NativeKitFlash.Full);show.Build(1,NativePhase.Fight,0,20+i*.1,20+i*.1,pulse,NativeKitFlash.Full);rig.Build(show,20+i*.1,false,1);fx.Build(show,20+i*.1,0,1.6,-12,1,0,0,0,1,0,1,false);}
        long before=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<600;i++){double t=i*.1;pulse.Feed(spectrum,spectrum.Length,48000,1/60.0,t,NativeKitFlash.Full);show.Build(1,NativePhase.Fight,0,t,t,pulse,NativeKitFlash.Full);rig.Build(show,t,false,1);fx.Build(show,t,0,1.6,-12,1,0,0,0,1,0,1,false);}
        Check(GC.GetAllocatedBytesForCurrentThread()==before,"no allocation per frame");
        Console.WriteLine("   club: rig at most "+maxRig+" parts, effects at most "+maxFx+" quads");
    }

    // ---- the pulse: the clock, the music, the flash caps
    private static void Pulse()
    {
        var p=new NativeClubPulse();p.Feed(null,0,0,1/60.0,3.0,NativeKitFlash.Full);Check(p.Live==0&&p.Kick>.9,"no music: the clock (a kick on the beat)");
        var a=new NativeClubPulse();var b=new NativeClubPulse();a.Clock(17.3,NativeKitFlash.Full);b.Clock(17.3,NativeKitFlash.Full);Check(a.Kick==b.Kick&&a.Bass==b.Bass,"the clock is deterministic");
        // a synthetic 128 bpm song: a kick in the low bins on every beat, a pad above
        var spectrum=new float[NativeClubPulse.Bins];int kicks=0,full=0,reduced=0;double dt=1/60.0;var music=new NativeClubPulse();var soft=new NativeClubPulse();float lastFull=0,lastSoft=0;bool upFull=false,upSoft=false;
        for(int f=0;f<60*20;f++)
        {
            double t=f*dt,since=t%NativeClubLooks.Beat;for(int i=0;i<spectrum.Length;i++)spectrum[i]=(float)(i<=2?.2*Math.Exp(-since*18):i<60?.004:.001);
            music.Feed(spectrum,spectrum.Length,48000,dt,t/NativeClubLooks.Beat,NativeKitFlash.Full);soft.Feed(spectrum,spectrum.Length,48000,dt,t/NativeClubLooks.Beat,NativeKitFlash.Reduced);
            if(music.Kick>.6&&!upFull){full++;upFull=true;}else if(music.Kick<.4)upFull=false;if(soft.Kick>.6&&!upSoft){reduced++;upSoft=true;}else if(soft.Kick<.4)upSoft=false;lastFull=music.Kick;lastSoft=soft.Kick;
        }
        kicks=music.Kicks;Check(music.Live>.99,"the music drives it");
        Check(Math.Abs(kicks-20/NativeClubLooks.Beat)<=3,"a kick heard on every beat ("+kicks+" in 20 s)");
        Check(full<=3*20,"Full: at most 3 pulses a second ("+full+")");Check(reduced<=2*20,"Reduced: at most 2 pulses a second ("+reduced+")");
        var calm=new NativeClubPulse();float lo=1,hi=0;for(int f=0;f<600;f++){calm.Feed(spectrum,spectrum.Length,48000,dt,f*dt/NativeClubLooks.Beat,NativeKitFlash.Calm);if(f>120){lo=Math.Min(lo,calm.Kick);hi=Math.Max(hi,calm.Kick);}}
        Check(hi-lo<=.35f,"Calm: no pulse, a slow swell ("+lo+".."+hi+")");
        // silence hands back to the clock
        for(int f=0;f<120;f++)music.Feed(new float[0],0,48000,dt,f*dt,NativeKitFlash.Full);Check(music.Live<.25,"silence hands back to the clock");
        _=lastFull;_=lastSoft;
    }

    // ---- the runtime wiring
    private static void Wiring(string root,Func<string,string> R)
    {
        Check(R("Assets/ChipArena/Runtime/NativeSourcePresentation.cs").Contains("ClubScenery(arena);")&&R("Assets/ChipArena/Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmClub(arena))built++;"),"SourceWorld draws it, PrewarmStage builds it");
        Check(R("Assets/ChipArena/Runtime/NativeAudio.cs").Contains("ClubFrame(arena,menu);")&&R("Assets/ChipArena/Runtime/NativeClubAudio.cs").Contains("music.GetSpectrumData(NativeClubMusic.Spectrum,0,FFTWindow.BlackmanHarris)"),"the music voice's spectrum is sampled");
        var pres=R("Assets/ChipArena/Runtime/NativeClubPresentation.cs");Check(pres.Contains("NativeLighting.KitLights(")&&pres.Contains("NativeClubMusic.Fresh")&&pres.Contains("club.Flash=KitFlash"),"the show lends its lights, hears the music, honours the light-flash setting");
        Check(R("Assets/ChipArena/Editor/NativeBuildChecks.cs").Contains("if(map.id==\"club\")arena.ValidateClubRuntime();"),"the Editor gate runs the club's native checks");
        foreach(var f in new[]{"Runtime/NativeStageHazardsRuntime.cs","Core/NativeStageHazards.cs","Core/NativeCyberCityHazards.cs","Core/NativeFactoryHazards.cs"})Check(!Regex.IsMatch(R("Assets/ChipArena/"+f),"NativeClub(Pulse|Music|Show)"),f+": the rules never read the music or the show");
        var guids=new HashSet<string>();
        foreach(var f in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena"),"NativeClub*.cs",SearchOption.AllDirectories))
        {var meta=f+".meta";Check(File.Exists(meta),Path.GetFileName(f)+" has its meta");var g=Regex.Match(File.ReadAllText(meta),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),Path.GetFileName(f)+" has its own guid");}
        Check(guids.Count==10,"ten club source files ("+guids.Count+")");
    }
}
