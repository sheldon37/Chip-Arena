using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

// The Hollow Sanctum (docs/arenas/SANCTUM.md): the map record, its navigation and hazards, its look, kit, glass, rigs, effects,
// lights and cues, and the runtime wiring. `dotnet run --project Tests/CoreTests.csproj -c Release -- --sanctum` (also in the full run).
internal static class SanctumChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Sanctum: "+why);}
        var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));
        var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);
        var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);NativeMapRules.Merge(content,maps,text);
        Check(maps.maps.Count(m=>m.id=="sanctum")==1,"the sanctum exists exactly once");var map=content.Map("sanctum");
        NativeMapRules.Validate(map);NativeMapRules.ValidateShippedCatalog(content);
        Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(map),"in the PvP list and Random");
        Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
        Check(map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"the block budget ("+map.blocks.Length+" blocks)");
        Check(map.look==NativeSanctumLooks.Look&&map.music=="moonlit-wilderness","the look and the placeholder song");
        // navigation: every spawn reached from FFA 0, and all three levels (crypt -3.6, the floor, the galleries 4.0)
        var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
        foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
        string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
        var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
        while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
        foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
        foreach(var level in new[]{-3.6f,0f,4f})Check(seen.Any(id=>Math.Abs(nodes[id].pos.y-.9f-level)<.05f),"nav reaches the level at "+level);
        Check(map.nav.nodes.Length<4000,"a lean nav ("+map.nav.nodes.Length+" nodes)");
        // hazards: the existing kinds only, idle for the first 4 s of both rounds, telegraphs at least as long as today's
        var kinds=new[]{"crane","saw-rail","burner","shutter"};Check(map.hazards.Length==6&&map.hazards.All(h=>kinds.Contains(h.kind)),"six hazards of existing kinds");
        foreach(var h in map.hazards)
        {
            for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)
            {
                if(NativeStageHazards.Ward(h))Check(NativeWardZeroHazards.At(h,NativePhase.Fight,round,0,t).Phase==NativeHazardPhase.Idle,"arm delay "+h.id);
                else Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
            }
            Check(h.kind=="crane"?h.warn>=3:h.kind=="saw-rail"?h.warn>=1.6:h.kind=="burner"?h.warn>=1.5:h.warn>=3,"telegraph length "+h.id);
            Check(h.kind=="burner"||h.kind=="shutter"?!h.lethal:h.lethal,"lethality "+h.id);
            foreach(var sp in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))
            {
                float feet=sp.pos.y-1.1f;
                IEnumerable<(V3 c,V3 s)> Vols(){if(h.kind=="crane")foreach(var z in h.zones)yield return (new V3(z.center.x,z.center.y+z.size.y/2,z.center.z),z.size);else if(h.kind=="saw-rail")yield return ((h.a+h.b)*.5f,new V3(Math.Abs(h.a.x-h.b.x)+h.size.x,h.size.y,Math.Abs(h.a.z-h.b.z)+h.size.z));else yield return (h.center,h.size);}
                foreach(var (c,s) in Vols()){if(feet<c.y-s.y/2-2||feet>c.y+s.y/2+1)continue;double dx=Math.Max(0,Math.Abs(sp.pos.x-c.x)-s.x/2),dz=Math.Max(0,Math.Abs(sp.pos.z-c.z)-s.z/2);Check(Math.Sqrt(dx*dx+dz*dz)>=3,"spawn "+sp.pos.x+","+sp.pos.z+" keeps 3 m from "+h.id);}
            }
        }
        var crane=map.hazards.Single(h=>h.kind=="crane");Check(crane.zones.Length==4&&crane.zones.All(z=>z.center.y==0&&crane.carry>z.center.y+z.size.y),"the four chandeliers' zones on the floor, hung over their lethal height");
        Check(map.hazards.Where(h=>h.kind=="burner").All(h=>h.damage==8&&h.tick==.25f&&h.live==2.5f),"holy fire: 8 per 0.25 s tick for 2.5 s, as Deepwell's flares");
        Check(map.hazards.Where(h=>h.kind=="shutter").All(h=>h.push>0&&h.dwell==8&&h.first==14),"the crypt gates: one toll for both, 8 s shut");
        // the look
        var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);NativeMapLooks.Validate(set);
        var look=NativeMapLooks.Find(set,"sanctum");Check(look!=null&&look.look==NativeSanctumLooks.Look&&look.toon.on&&look.blocks.dressed&&look.advanced.gi.on&&look.advanced.scatter.on&&look.advanced.beams.on&&look.lights.Length==8,"the look: styled, dressed, the light volume, scattering and beams, eight practicals");
        Check(look.sky.variant=="night"&&Math.Abs(look.key.elevation-NativeSanctumLooks.MoonElevation)<1e-3&&Math.Abs(look.key.bearing-NativeSanctumLooks.MoonBearing)<1e-3,"the moon key agrees with the shafts and the glass");
        Check(NativeSanctumLooks.Kinds.All(k=>look.kitLooks.Any(e=>e.block==k)),"every kit kind is listed in the look");
        foreach(var e in look.blockLooks)Check(map.blocks.Any(b=>NativeBlockLooks.Score(e.block,b.id,b.material)>0),"block look "+e.block+" decides a real block");
        var looks=NativeSanctumLooks.Resolve(look.kitLooks);
        // the kit: attached detail within 0.25 m of a block below 3 m (thin props, candles and sticks, within 0.9 m), overhead structure 2 m over every standable top under it
        var kit=NativeSanctumKit.Build(map,looks);Check(kit.Parts.Count>3000&&kit.Parts.Count<=NativeFactoryKit.MaxParts,"the kit's size ("+kit.Parts.Count+" parts)");
        Check(kit.Candles.Count/5>=300,"hundreds of candles ("+kit.Candles.Count/5+")");
        var tops=map.nav.nodes.Select(n=>n.pos).ToArray();
        bool NearBlock(in FactoryPart p,float grow){foreach(var b in map.blocks){if(Math.Abs(p.X-b.pos.x)<=b.size.x/2+grow+Ext(p,0)&&Math.Abs(p.Y-b.pos.y)<=b.size.y/2+grow+Ext(p,1)&&Math.Abs(p.Z-b.pos.z)<=b.size.z/2+grow+Ext(p,2))return true;}return false;}
        for(int i=0;i<kit.Parts.Count;i++)
        {
            var p=kit.Parts.Items[i];Check(float.IsFinite(p.X+p.Y+p.Z+p.Hx+p.Hy+p.Hz+p.Qx+p.Qy+p.Qz+p.Qw),"finite part "+i);
            double low=p.Y-Ext(p,1);
            if(kit.Role[i]==NativeSanctumKit.Attached){bool thin=Math.Min(Ext(p,0),Ext(p,2))<.12f;if(low<3)Check(NearBlock(p,thin?.9f:.25f),"attached part "+i+" at "+p.X+","+p.Y+","+p.Z+" lies on a block (thin props within 0.9 m, anything that could read as cover within 0.25 m)");}
            else foreach(var t in tops)if(Math.Abs(t.x-p.X)<=Ext(p,0)+.42&&Math.Abs(t.z-p.Z)<=Ext(p,2)+.42&&t.y-.9<low)Check(low-(t.y-.9)>=2,"overhead part "+i+" at "+p.X+","+p.Y+","+p.Z+" stands 2 m over the top at "+t.x+","+(t.y-.9)+","+t.z);
        }
        var kit2=NativeSanctumKit.Build(map,looks);Check(kit2.Parts.Count==kit.Parts.Count&&Enumerable.Range(0,kit.Parts.Count).All(i=>kit.Parts.Items[i].Equals(kit2.Parts.Items[i])),"the kit is the same every build");
        Check(NativeSanctumKit.Build(content.Map("rig"),looks).Parts.Count==0,"another map gets no kit");
        // the windows: their openings are open (no block in the glass's plane), sills out of reach
        foreach(var w in NativeSanctumLooks.Windows)
        {
            bool zn=w.Nz!=0;double gx=w.X-w.Nx*NativeSanctumScenery.Plane,gz=w.Z-w.Nz*NativeSanctumScenery.Plane;
            for(int a=-2;a<=2;a++)for(int b=-2;b<=2;b++){double u=a*(w.Half-.3)/2,y=w.Y+b*(w.HalfY-.3)/2;if(w.Rose&&u*u+(y-w.Y)*(y-w.Y)>(w.Half-.3)*(w.Half-.3))continue;double x=gx+(zn?u:0),z=gz+(zn?0:u);
                Check(!map.blocks.Any(k=>Math.Abs(x-k.pos.x)<k.size.x/2&&Math.Abs(y-k.pos.y)<k.size.y/2&&Math.Abs(z-k.pos.z)<k.size.z/2),"window at "+w.X+","+w.Y+","+w.Z+" is open at "+x+","+y+","+z);}
            Check(w.Y-w.HalfY>=7.99,"window sill out of reach");
        }
        Check(NativeSanctumLooks.Windows.Count(w=>NativeSanctumLooks.MoonShare(w)>0)>=10,"the moon finds at least ten windows");
        // the glass: built, every pane 8 m or more over the floor, HDR on the moonlit side
        var glass=NativeSanctumScenery.Build();Check(glass.Length==NativeSanctumScenery.Sectors&&glass.Sum(g=>g.Boxes)>4000,"the glass ("+glass.Sum(g=>g.Boxes)+" panes)");
        foreach(var g in glass)Check(g.MinY>=7.9f,"glass stays in its windows");
        Check(glass.Any(g=>Enumerable.Range(0,g.Emission.Length/4).Any(i=>g.Emission[i*4]+g.Emission[i*4+1]+g.Emission[i*4+2]>1.5f)),"the moonlit glass glows past the bloom's threshold");
        // the rigs follow the replicated state
        var rig=new NativeSanctumRig(looks);var hz=map.hazards;int ci=Array.FindIndex(hz,h=>h.kind=="crane"),ri=Array.FindIndex(hz,h=>h.kind=="saw-rail");
        for(double t=0;t<70;t+=.37)for(int round=0;round<2;round++)
        {
            rig.Build(hz,round,NativePhase.Fight,0,t,t);double clock=NativeFactoryHazards.Clock(NativePhase.Fight,0,t);
            var st=NativeFactoryHazards.State(hz[ci],round,clock);
            for(int k=0;k<4;k++)
            {
                double y=rig.Ring[k*5+1]-.12;
                if(st.Zone==k&&(st.Phase==NativeFactoryHazards.Falling||st.Phase==NativeFactoryHazards.Hoisting))Check(Math.Abs(y-st.LoadY)<1e-6,"the served chandelier is the load");
                else if(st.Zone==k&&(st.Phase==NativeFactoryHazards.Landed||st.Phase==NativeFactoryHazards.Regrip))Check(Math.Abs(y-hz[ci].zones[k].center.y)<1e-6,"the fallen chandelier lies on its zone");
                else if(st.Zone!=k||st.Phase==NativeFactoryHazards.Parked||st.Phase==NativeFactoryHazards.Travelling)Check(Math.Abs(y-crane.carry)<1e-6,"a resting chandelier hangs at the carry height");
            }
            var cs=NativeFactoryHazards.State(hz[ri],round,clock);Check(Math.Abs(rig.CenserZ-cs.Center.z)<1e-5&&Math.Abs(rig.CenserX-cs.Center.x)<1e-5,"the censer is at the simulation's point");
            Check(Math.Abs(rig.CenserY-cs.Center.y)<.75,"the censer's arc stays inside its live volume's height");
            var gs=NativeWardZeroHazards.At(hz.First(h=>h.kind=="shutter"),NativePhase.Fight,round,0,t);Check(Math.Abs(rig.Gate-NativeWardZeroHazards.Closure(gs))<1e-9,"the gates close by the shutters' closure");
        }
        // the hazards' polish (2026-11-11): the drawn motion is one unbroken take. Stepping two rounds at 5 ms, no chandelier's
        // frame, chain end or thrown piece and nothing of the censer (its turn, its lid, its rope and tether) jumps between two
        // steps by more than its fastest honest motion (the fall's 16 m/s, the whip's lash); the censer's centre never leaves
        // the simulation's point and the low rim of a falling chandelier rides the simulation's load height
        {
            double atBody=0,atRope=0,worstBody=0,worstTurn=0,worstChain=0,worstLoose=0,worstCenser=0,worstRope=0,worstLid=0,rim=0;var was=new double[4*3];var wasQ=new FactoryQ[4];var wasEnd=new double[4*3];var wasLoose=new double[rig.Loose.Length];var wasScrap=new double[rig.Scrap.Length];
            var wasCenser=FactoryQ.Identity;var wasRope=new double[NativeSanctumRig.RopePoints*3+NativeSanctumRig.TetherPoints*3];double wasLid=0;bool begun=false;int tipped=0,whipped=0,thrown=0,cocked=0,slack=0;
            for(double t=3;t<64;t+=.005)
            {
                rig.Build(hz,0,NativePhase.Fight,0,t,t);double clock=NativeFactoryHazards.Clock(NativePhase.Fight,0,t);var st=NativeFactoryHazards.State(hz[ci],0,clock);
                for(int k=0;k<4;k++)
                {
                    int e=(k*NativeSanctumRig.ChainPoints+rig.ChainCount[k]-1)*3;var bq=rig.BodyQ[k];
                    if(begun)
                    {
                        {double d=Dist(rig.Body,k*3,was,k*3);if(d>worstBody){worstBody=d;atBody=t;}}worstTurn=Math.Max(worstTurn,1-Math.Abs(bq.X*wasQ[k].X+bq.Y*wasQ[k].Y+bq.Z*wasQ[k].Z+bq.W*wasQ[k].W));
                        // (the chain's end is let go at the snap and taken up at the hoist: its line is unbroken between)
                        bool cut=st.Zone==k&&(st.Phase==NativeFactoryHazards.Falling&&rig.Snap[k]<.006||st.Phase==NativeFactoryHazards.Hoisting&&rig.Hoist[k]<.006);if(!cut)worstChain=Math.Max(worstChain,Dist(rig.ChainPts,e,wasEnd,k*3));
                        for(int i=0;i<NativeSanctumRig.ChandelierCandles;i++){int lo=(k*NativeSanctumRig.ChandelierCandles+i)*8;if(rig.Loose[lo+7]>0&&wasLoose[lo+7]>0)worstLoose=Math.Max(worstLoose,Dist(rig.Loose,lo,wasLoose,lo));}
                        for(int i=0;i<NativeSanctumRig.DebrisCount;i++){int so=(k*NativeSanctumRig.DebrisCount+i)*8;if(rig.Scrap[so+7]>0&&wasScrap[so+7]>0)worstLoose=Math.Max(worstLoose,Dist(rig.Scrap,so,wasScrap,so));}
                    }
                    Array.Copy(rig.Body,k*3,was,k*3,3);wasQ[k]=bq;Array.Copy(rig.ChainPts,e,wasEnd,k*3,3);
                    if(st.Zone==k&&st.Phase==NativeFactoryHazards.Falling)
                    {
                        // the lowest point of the hoop (radius Hoop, tipped) against the load's underside
                        bq.Rotate(0,1,0,out _,out double up,out _);double low=rig.Body[k*3+1]-NativeSanctumRig.Hoop*Math.Sqrt(Math.Max(0,1-up*up));if(st.LoadY<hz[ci].zones[k].center.y+4)rim=Math.Max(rim,Math.Abs(low-st.LoadY));if(up<.97)tipped++;
                    }
                    if(rig.Snap[k]>.1&&rig.Snap[k]<.4&&Math.Abs(rig.ChainPts[e]-hz[ci].zones[k].center.x)>.4)whipped++;
                    if(Enumerable.Range(0,NativeSanctumRig.ChandelierCandles).Any(i=>rig.Loose[(k*NativeSanctumRig.ChandelierCandles+i)*8+7]>0))thrown++;
                }
                Array.Copy(rig.Loose,wasLoose,wasLoose.Length);Array.Copy(rig.Scrap,wasScrap,wasScrap.Length);
                var cq=rig.CenserQ;
                if(begun)
                {
                    worstCenser=Math.Max(worstCenser,1-Math.Abs(cq.X*wasCenser.X+cq.Y*wasCenser.Y+cq.Z*wasCenser.Z+cq.W*wasCenser.W));worstLid=Math.Max(worstLid,Math.Abs(rig.LidHop-wasLid));
                    for(int i=0;i<NativeSanctumRig.RopePoints;i++){double d=Dist(rig.RopePts,i*3,wasRope,i*3);if(d>worstRope){worstRope=d;atRope=t;}}
                    for(int i=0;i<NativeSanctumRig.TetherPoints;i++){double d=Dist(rig.TetherPts,i*3,wasRope,(NativeSanctumRig.RopePoints+i)*3);if(d>worstRope){worstRope=d;atRope=t;}}
                }
                wasCenser=cq;wasLid=rig.LidHop;Array.Copy(rig.RopePts,wasRope,NativeSanctumRig.RopePoints*3);Array.Copy(rig.TetherPts,0,wasRope,NativeSanctumRig.RopePoints*3,NativeSanctumRig.TetherPoints*3);
                var cs=NativeFactoryHazards.State(hz[ri],0,clock);cq.Rotate(0,1,0,out _,out double cy,out double cz);
                if(cs.Phase==NativeFactoryHazards.Warning&&cz>.6)cocked++;if(!rig.CenserHeld)slack++;
                // the rope's foot is the censer's crown ring, the tether's its foot ring, their heads the yoke and the cleat
                cq.Rotate(0,NativeSanctumRig.CenserRing,0,out double rx,out double ry,out double rz);Check(Math.Abs(rig.RopePts[0]-rig.CenserX-rx)+Math.Abs(rig.RopePts[1]-rig.CenserY-ry)+Math.Abs(rig.RopePts[2]-rig.CenserZ-rz)<1e-9,"the rope takes the censer by its crown ring");
                int tl=(NativeSanctumRig.TetherPoints-1)*3;Check(Math.Abs(rig.TetherPts[tl])+Math.Abs(rig.TetherPts[tl+1]-NativeSanctumRig.CleatY)+Math.Abs(rig.TetherPts[tl+2]-NativeSanctumRig.CleatZ)<1e-9,"the tether runs to its cleat");
                begun=true;
            }
            Console.WriteLine("Sanctum motion: worst step in 5 ms: chandelier "+worstBody.ToString("0.000")+" m, its turn "+worstTurn.ToString("0.00000")+", chain end "+worstChain.ToString("0.000")+" m, thrown pieces "+worstLoose.ToString("0.000")+" m; censer turn "+worstCenser.ToString("0.00000")+", lid "+worstLid.ToString("0.0000")+" m, rope and tether "+worstRope.ToString("0.000")+" m; rim off the load "+rim.ToString("0.000")+" m (at "+atBody.ToString("0.000")+" s, "+atRope.ToString("0.000")+" s)");
            Check(worstBody<.11&&worstTurn<2e-4,"a chandelier's frame never jumps (worst step "+worstBody.ToString("0.000")+" m, turn "+worstTurn.ToString("0.00000")+")");
            Check(worstChain<.2&&worstLoose<.2,"the chain's end and the thrown candles and debris never jump (worst "+worstChain.ToString("0.000")+", "+worstLoose.ToString("0.000")+" m)");
            Check(worstCenser<2e-4&&worstLid<.02&&worstRope<.12,"the censer's turn, its lid, its rope and its tether never jump (worst "+worstCenser.ToString("0.00000")+", "+worstLid.ToString("0.0000")+" m, "+worstRope.ToString("0.000")+" m)");
            Check(rim<.12,"a falling chandelier's low rim rides the simulation's load height ("+rim.ToString("0.000")+" m off at most)");
            Check(tipped>0&&whipped>0&&thrown>0&&cocked>0&&slack>0,"a fall tips, the snapped chain lashes, candles are thrown; the censer is cocked back through its warning and its tether pays out in the swing");
        }
        // the lights on the floor (owner 2026-11-11: depth fighting with the lights): every pool of moonlight lies on the top of a
        // static block .6 m or more wide (never a pew, a coping or a pier's side), lifted over the runner and the inlays
        {
            var probe=new NativeSanctumFx();probe.Prepare(map,kit);probe.Build(hz,rig,0,NativePhase.Fight,0,30,30,-20,1.7,-3,1,0,0,0,1,0,1,false);
            NativeSanctumLooks.MoonRay(out double mx,out double my,out double mz);int floors=0,walls=0;
            foreach(var w in NativeSanctumLooks.Windows){if(NativeSanctumLooks.MoonShare(w)<.05)continue;for(double v=-.8;v<=.8;v+=.4){double py=w.Y+v*w.HalfY;if(NativeSanctumFx.Top(map.blocks,w.X+w.Nx*.45,py,w.Z+w.Nz*.45,mx,my,mz,90,out double sy)){floors++;Check(map.blocks.Any(b=>b.role=="static"&&Math.Abs(b.pos.y+b.size.y/2-sy)<1e-6&&Math.Min(b.size.x,b.size.z)>=.6),"a pool's floor is a static block's top");}else walls++;}}
            Check(floors>20&&probe.Pools>60,"the moon's pools lie on floors ("+probe.Pools+" pools; of "+(floors+walls)+" probed rays "+floors+" meet a floor)");
            Check(NativeSanctumFx.GroundLift>=.035&&NativeSanctumFx.GroundLift<=.06,"the floor's lights lie over the runner and the inlays (25 mm), under a boot");
        }
        rig.Build(hz,0,NativePhase.Countdown,0,1,1);Check(Enumerable.Range(0,4).All(k=>rig.Ring[k*5+4]==0&&Math.Abs(rig.Ring[k*5+1]-.12-crane.carry)<1e-6)&&rig.Gate==0,"parked before the fight");
        // the effects: telegraphs never reduced; the lights; determinism; no allocation per frame
        var fx=new NativeSanctumFx();fx.Prepare(map,kit);
        foreach(var h in hz)
        {
            double warnAt=h.kind=="crane"?4+h.start+h.travel+h.warn*.5:h.kind=="saw-rail"?4+h.start+h.warn*.5:h.first+h.offset+h.warn*.5;
            rig.Build(hz,1,NativePhase.Fight,0,warnAt,warnAt);fx.Build(hz,rig,1,NativePhase.Fight,0,warnAt,warnAt,0,5,-20,1,0,0,0,1,0,0,true);int lowT=fx.Telegraph;
            fx.Build(hz,rig,1,NativePhase.Fight,0,warnAt,warnAt,0,5,-20,1,0,0,0,1,0,1,false);Check(lowT>0&&lowT==fx.Telegraph,h.id+": its warning draws the same at the low tier and full quality ("+lowT+")");
        }
        fx.Build(hz,rig,0,NativePhase.Fight,0,30,30,-20,1.7,-3,1,0,0,0,1,0,1,false);Check(fx.Flames>300&&fx.Shafts>0,"candle flames ("+fx.Flames+") and the moon's shafts ("+fx.Shafts+")");
        Check(fx.Quads.Count<NativeSanctumFx.Capacity,"the effects fit ("+fx.Quads.Count+" quads)");
        Check(Enumerable.Range(0,8).All(i=>float.IsFinite(fx.LightPos[i*4]+fx.LightPos[i*4+1]+fx.LightPos[i*4+2]+fx.LightRgb[i*3])&&fx.LightPos[i*4+3]>0),"eight finite practical lights");
        var a1=fx.Quads.Corners.Take(fx.Quads.Count*12).ToArray();fx.Build(hz,rig,0,NativePhase.Fight,0,30,30,-20,1.7,-3,1,0,0,0,1,0,1,false);Check(a1.SequenceEqual(fx.Quads.Corners.Take(fx.Quads.Count*12)),"the effects are deterministic");
        long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<60;i++){double t=10+i*.53;rig.Build(hz,i&1,NativePhase.Fight,0,t,t);fx.Build(hz,rig,i&1,NativePhase.Fight,0,t,t,-20,1.7,-3,1,0,0,0,1,0,1,false);}
        Check(GC.GetAllocatedBytesForCurrentThread()-before==0,"the rig and the effects allocate nothing per frame");
        // the chandelier's light falls with it
        {
            var st=NativeFactoryHazards.State(crane,0,0);double fallAt=4+crane.start+crane.travel+crane.warn+crane.rise*.6;rig.Build(hz,0,NativePhase.Fight,0,fallAt,fallAt);var z0=crane.zones[NativeFactoryHazards.ZoneOf(crane,0,0)];
            fx.Build(hz,rig,0,NativePhase.Fight,0,fallAt,fallAt,z0.center.x,2,z0.center.z+6,1,0,0,0,1,0,1,false);
            Check(Enumerable.Range(0,8).Any(i=>Math.Abs(fx.LightPos[i*4]-z0.center.x)<.5&&fx.LightPos[i*4+1]<crane.carry-.5),"a falling chandelier carries its light down");
        }
        // the cues: in order, once each, nothing before the arm delay
        var cues=new FactoryCue[512];int nc=NativeSanctumCues.Between(hz,0,NativePhase.Fight,0,0,4,cues);Check(nc==0,"no cue in the first 4 s");
        nc=NativeSanctumCues.Between(hz,0,NativePhase.Fight,0,0,60,cues);var kindsHeard=new HashSet<int>(cues.Take(nc).Select(c=>c.Kind));Check(nc>0&&Enumerable.Range(0,15).All(kindsHeard.Contains),"every cue kind sounds in a minute ("+nc+")");
        int split=0;for(double t=0;t<60;t+=.1)split+=NativeSanctumCues.Between(hz,0,NativePhase.Fight,0,t,t+.1,cues);Check(split==NativeSanctumCues.Between(hz,0,NativePhase.Fight,0,0,60.0000001,cues)||Math.Abs(split-nc)<=1,"each cue lies in one window");
        // the runtime wiring and the new files' metas
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
        Check(R("Runtime/NativeSourcePresentation.cs").Contains("SanctumScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmSanctum(arena))built++;"),"SourceWorld and PrewarmStage call the sanctum");
        Check(R("Runtime/NativeAudio.cs").Contains("SanctumCues(arena,menu);")&&R("Runtime/NativeAdvancedLightingPresentation.cs").Contains("SanctumCandidates(arena,volumeCandidates)"),"the audio and the light volume call the sanctum");
        Check(R("Runtime/NativeSanctumPresentation.cs").Contains("NativeLighting.PickedLights(fx.LightPos,fx.LightRgb)")&&R("Runtime/NativeSanctumPresentation.cs").Contains("if(!NativeSanctumLooks.Active(arena.MapId))return;"),"the sanctum draws and lights only its own map");
        var guids=new HashSet<string>();
        foreach(var f in new[]{"Core/NativeSanctumLooks.cs","Core/NativeSanctumKit.cs","Core/NativeSanctumScenery.cs","Core/NativeSanctumRig.cs","Core/NativeSanctumFx.cs","Core/NativeSanctumCues.cs","Runtime/NativeSanctumPresentation.cs","Runtime/NativeSanctumAudio.cs"})
        {var meta=R(f+".meta");var g=meta.Split('\n').First(l=>l.StartsWith("guid:")).Substring(5).Trim();Check(g.Length==32&&guids.Add(g),f+" has its own meta");}
        Check(!NativeSanctumLooks.Active("rig")&&NativeSanctumLooks.Active("sanctum"),"keyed on the sanctum alone");
        Console.WriteLine("PASS sanctum: "+count+" checks (map, nav, hazards, look, kit "+kit.Parts.Count+" parts and "+kit.Candles.Count/5+" candles, glass "+glass.Sum(g=>g.Boxes)+" panes, rigs, effects, lights, cues, wiring)");
    }
    private static double Dist(double[] a,int i,double[] b,int j)=>Math.Sqrt((a[i]-b[j])*(a[i]-b[j])+(a[i+1]-b[j+1])*(a[i+1]-b[j+1])+(a[i+2]-b[j+2])*(a[i+2]-b[j+2]));
    // a part's half extent along a world axis (its rotated box's)
    private static float Ext(in FactoryPart p,int axis)
    {
        var q=new FactoryQ{X=p.Qx,Y=p.Qy,Z=p.Qz,W=p.Qw};double e=0;
        foreach(var (lx,ly,lz) in new[]{(p.Hx,0f,0f),(0f,p.Hy,0f),(0f,0f,p.Hz)}){q.Rotate(lx,ly,lz,out double x,out double y,out double z);e+=Math.Abs(axis==0?x:axis==1?y:z);}
        return (float)e;
    }
}
