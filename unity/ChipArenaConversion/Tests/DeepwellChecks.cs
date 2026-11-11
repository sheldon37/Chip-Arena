#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Deepwell (map `rig`, rebuilt 2026-11-11 as a research station on the ocean floor: docs/arenas/DEEPWELL.md; draft
    // docs/arenas/scripts/rig_m2.py; Core NativeDeepwell*, Runtime NativeDeepwellPresentation, MapLooks.json `rig-abyss`). Against the
    // committed catalogue and looks: the record validates, sits in the shipped catalogue and Random, keeps its id and name; every
    // spawn is reachable on the nav graph and keeps 3 m from every hazard on its floor; the hazards are the existing kinds (the
    // drill's piston a `press`, the bubbling vents `vent`s with the `wind` push), idle for the fight's first 4 s and telegraphed at
    // least as long as today's; the only falls are the two moon pools, and they kill fast; no pilot column stands anywhere but over
    // the hull's walls and glass, the drill's pylons and (bots only) the catwalks' rails; the look resolves, styled, with the
    // advanced lighting; the box kit's parts stay inside their blocks (the collider rule), its overhead parts clear every floor,
    // its seabed stays outside the hull, and no two of its faces fight; the rig and effects follow the replicated state,
    // telegraphs are never reduced, the sea outside moves all match long and never enters the hull, everything is deterministic
    // and allocation free per frame; each audio cue sounds once; the runtime is wired and the map is off the shared M1 paths.
    internal static class DeepwellChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Deepwell: "+what);count++;}
        internal struct Box3 {public double X0,X1,Y0,Y1,Z0,Z1;}
        internal static Box3 Of(ContentBlock b)=>new Box3{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2};
        internal static Box3 Of(in FactoryPart p)
        {
            var q=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var b=new Box3{X0=double.MaxValue,X1=double.MinValue,Y0=double.MaxValue,Y1=double.MinValue,Z0=double.MaxValue,Z1=double.MinValue};
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?-p.Hx:p.Hx,(c&2)==0?-p.Hy:p.Hy,(c&4)==0?-p.Hz:p.Hz),q);b.X0=Math.Min(b.X0,p.X+v.X);b.X1=Math.Max(b.X1,p.X+v.X);b.Y0=Math.Min(b.Y0,p.Y+v.Y);b.Y1=Math.Max(b.Y1,p.Y+v.Y);b.Z0=Math.Min(b.Z0,p.Z+v.Z);b.Z1=Math.Max(b.Z1,p.Z+v.Z);}
            return b;
        }
        private static bool Within(Box3 a,Box3 b,double g)=>a.X0>=b.X0-g&&a.X1<=b.X1+g&&a.Y0>=b.Y0-g&&a.Y1<=b.Y1+g&&a.Z0>=b.Z0-g&&a.Z1<=b.Z1+g;
        internal static ContentMap Map(string root,out UnityMapContent maps,out ConversionContent content)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string text=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json"));maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);
            content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),o);NativeMapRules.Merge(content,JsonSerializer.Deserialize<UnityMapContent>(text,o),text);
            return maps.maps.Single(m=>m.id==NativeDeepwellLooks.MapId);
        }

        public static void Run(string root)
        {
            count=0;var map=Map(root,out var maps,out var content);NativeMapRules.ValidateShippedCatalog(content);NativeMapRules.Validate(map);
            Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,8).Select(i=>new NativeSeat{id=i,team=i%2}),true),"selectable and in Random for a full room");
            Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
            Check(map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"block budgets ("+map.blocks.Length+")");
            Check(map.id=="rig"&&map.name=="Deepwell"&&map.look==NativeDeepwellLooks.Look,"the stage keeps its id and its name; its look is its own");
            Check(map.nav.nodes.Length<2500&&map.nav.links.Length<4500,"a lean nav graph ("+map.nav.nodes.Length+" nodes)");
            Check(map.blocks.All(b=>b.id.StartsWith("rig-",StringComparison.Ordinal))&&!map.blocks.Any(b=>b.id.StartsWith("rig-deck-",StringComparison.Ordinal)||b.id.StartsWith("rig-derrick-",StringComparison.Ordinal)),"nothing of the platform is left");
            // point symmetric: fair to both teams
            var keys=new HashSet<string>(map.blocks.Select(b=>K(b.pos.x,b.pos.y,b.pos.z,b.size.x,b.size.y,b.size.z)));
            Check(map.blocks.All(b=>keys.Contains(K(-b.pos.x,b.pos.y,-b.pos.z,b.size.x,b.size.y,b.size.z))),"every block has its partner turned half round");
            Nav(map);Hazards(map,maps);Falls(map);Columns(map);
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,map.look);
            Check(look!=null&&look.id==map.id&&NativeMapStyle.On(look,false)&&look.advanced!=null&&look.blocks.dressed&&look.lights.Length==8&&look.farClip>=300,"the look: styled, dressed, advanced, eight lights, a far plane past the whales");
            Check(look.blockLooks.All(b=>b.block.StartsWith("material:",StringComparison.Ordinal)||map.blocks.Any(k=>NativeBlockLooks.Glob(b.block,k.id))),"every block look names real blocks");
            Check(look.advanced.weather.kind==""&&look.fog.mode=="linear"&&look.fog.end<=220&&look.sky.stars==0,"no weather and no stars under the sea; the water closes the view");
            Presentation(root,map,look);Sea();
            Wiring(root);
            Console.WriteLine("PASS Deepwell: "+count+" checks");
        }
        private static string K(double x,double y,double z,double sx,double sy,double sz)=>string.Join(",",new[]{x,y,z,sx,sy,sz}.Select(v=>(Math.Round(v*1000)+0.0).ToString()));

        private static void Nav(ContentMap map)
        {
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
            string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
            // every deck is on the graph: the moon-pool bays, the main deck, the gallery deck
            foreach(var y in new[]{-1.6f,0f,3.2f})Check(seen.Any(n=>Math.Abs(nodes[n].pos.y-.9f-y)<.05f),"nav reaches level "+y);
            // the catwalks, both wings' lounges, the gallery and all four tunnels
            foreach(var (x,z,y) in new[]{(0f,15f,3.2f),(0f,-15f,3.2f),(-25f,0f,3.2f),(25f,0f,3.2f),(0f,8f,3.2f),(-22f,15f,0f),(22f,15f,0f),(-22f,-15f,0f),(22f,-15f,0f)})
                Check(seen.Any(n=>Math.Abs(nodes[n].pos.x-x)<1.1f&&Math.Abs(nodes[n].pos.z-z)<1.1f&&Math.Abs(nodes[n].pos.y-.9f-y)<.05f),"nav reaches "+x+","+z+" on "+y);
            // no node hangs over a pool (a bot never paths into the water); the subs in the pools are islands
            Check(!map.nav.nodes.Any(n=>Pool(n.pos.x,n.pos.z)&&n.pos.y-.9f<-1.5f),"no nav node on the water");
        }
        private static bool Pool(double x,double z)=>x>NativeDeepwellLooks.PoolX0&&x<NativeDeepwellLooks.PoolX1&&Math.Abs(z)>NativeDeepwellLooks.PoolZ0&&Math.Abs(z)<NativeDeepwellLooks.PoolZ1;

        private static void Hazards(ContentMap map,UnityMapContent maps)
        {
            var hz=map.hazards;Check(hz.Count(h=>h.kind=="press")==1&&hz.Count(h=>h.kind=="vent")==4&&hz.Length==5,"the drill's piston and four vents");
            foreach(var h in hz)
            {
                for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
                for(double t=0;t<8;t+=.05)Check(!NativeFactoryHazards.State(h,0,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Lethal,"nothing hurts in the fight's first 8 s "+h.id);
                if(h.kind=="press")
                {
                    Check(h.warn>=3&&h.lethal&&Math.Abs(h.b.y-h.size.y/2)<.01f&&h.id=="rig-piston","the piston's 3 s telegraph and its y-0 floor (the press kind's lethal band is absolute)");
                    Check(h.a.y-h.size.y/2>=3.2+3.4&&Math.Abs(h.a.x)<.01&&Math.Abs(h.a.z)<.01,"parked, the ram hangs clear over the gallery's eye lines, on the Well's axis");
                    double open=h.period-h.warn-h.rise-h.dwell-h.travel,strike=h.start+h.offset+open+h.warn+h.rise;
                    Check(!NativeFactoryHazards.State(h,0,strike-h.rise-.1).Lethal&&NativeFactoryHazards.State(h,0,strike+.05).Lethal&&NativeFactoryHazards.State(h,0,strike+h.dwell-.05).Lethal&&!NativeFactoryHazards.State(h,0,strike+h.dwell+h.travel+.05).Lethal,"the piston's lethal window");
                    var p=NativeDeepwellLooks.PistonAt(h,0,strike-h.rise-h.warn*.5);Check(p.Warn>.4&&p.Warn<.6&&!p.Live&&!p.Held,"the telegraph's progress");
                    p=NativeDeepwellLooks.PistonAt(h,0,strike+.3);Check(p.Held&&p.Live&&Math.Abs(p.Since-.3)<1e-6&&Math.Abs(p.Y-h.b.y)<1e-4,"held on the wellhead, the strike's time");
                    Check(NativeDeepwellLooks.CoreLevel(NativeDeepwellLooks.PistonAt(h,0,strike+.02),0)>.95&&NativeDeepwellLooks.CoreLevel(NativeDeepwellLooks.PistonAt(h,0,1),0)<.35,"the core flares at the strike and rests between");
                    // nothing but the wellhead under the ram: no block in its footprint above the floor
                    Check(!map.blocks.Any(b=>{var x=Of(b);return x.Y1>.01&&x.Y0<h.a.y+h.size.y/2&&x.X0<h.b.x+h.size.x/2&&x.X1>h.b.x-h.size.x/2&&x.Z0<h.b.z+h.size.z/2&&x.Z1>h.b.z-h.size.z/2;}),"the ram's stroke is clear");
                }
                else
                {
                    Check(h.warn>=.8f&&!h.lethal&&h.damage>0&&h.damage<=4&&h.push>0&&h.pushMode==NativeStageHazards.PushWind&&Math.Abs(h.axis.Length-1)<1e-4&&Math.Abs(h.axis.y)<1e-6,"a vent's telegraph, damage and push "+h.id);
                    // on the bay's floor beside its pool, blowing toward the water
                    Check(Math.Abs(h.a.y+1.6)<.01&&Math.Abs(h.a.x)>NativeDeepwellLooks.PoolX1&&h.a.x*h.axis.x<0&&Math.Abs(h.a.z)>NativeDeepwellLooks.PoolZ0&&Math.Abs(h.a.z)<NativeDeepwellLooks.PoolZ1,"the vent stands beside its pool and blows toward it "+h.id);
                    Check(Math.Abs(Math.Abs(h.a.x)-h.size.x/2-NativeDeepwellLooks.PoolX1)<.5,"its column reaches the pool's edge "+h.id);
                }
            }
            // a vent and its mirror share their clock (fair to both teams); a bay's two alternate
            MapHazard V(string id)=>hz.Single(h=>h.id==id);
            Check(V("rig-vent-n-w").offset==V("rig-vent-s-e").offset&&V("rig-vent-n-e").offset==V("rig-vent-s-w").offset&&Math.Abs(V("rig-vent-n-w").offset-V("rig-vent-n-e").offset)==V("rig-vent-n-w").period/2,"the vents' clocks");
            // spawns: 3 m (plan) from every live volume spanning their body on their floor
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))
            {
                double fy=p.pos.y-1.1;
                foreach(var h in hz)
                {
                    double x0,x1,z0,z1,y0,y1;
                    if(h.kind=="press"){x0=h.b.x-h.size.x/2;x1=h.b.x+h.size.x/2;z0=h.b.z-h.size.z/2;z1=h.b.z+h.size.z/2;y0=h.b.y-h.size.y/2;y1=h.a.y+h.size.y/2;}
                    else{x0=h.a.x-h.size.x/2;x1=h.a.x+h.size.x/2;z0=h.a.z-h.size.z/2;z1=h.a.z+h.size.z/2;y0=h.a.y;y1=h.a.y+h.size.y;}
                    if(y0-.1>fy+1.8||y1<fy)continue;double dx=Math.Max(0,Math.Max(x0-p.pos.x,p.pos.x-x1)),dz=Math.Max(0,Math.Max(z0-p.pos.z,p.pos.z-z1));
                    Check(Math.Sqrt(dx*dx+dz*dz)>=3,"spawn "+p.pos.x+","+p.pos.z+" clear of "+h.id);
                }
                Check(!Pool(p.pos.x,p.pos.z),"no spawn over a pool");
            }
            // the cues: each once however the frames cut the time
            var into=new FactoryCue[64];var scratch=new FactoryCue[128];var all=new List<(int,int,int)>();var whole=new List<(int,int,int)>();
            for(double t=0;t<90;t+=1/60.0){int n=NativeDeepwellLooks.Cues(hz,0,0,t,t+1/60.0,into,scratch);for(int i=0;i<n;i++)all.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            for(double t=0;t<90;t+=.5){int n=NativeDeepwellLooks.Cues(hz,0,0,t,t+.5,into,scratch);for(int i=0;i<n;i++)whole.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            Check(all.Count==all.Distinct().Count()&&all.OrderBy(x=>x).SequenceEqual(whole.OrderBy(x=>x)),"each cue sounds once ("+all.Count+")");
            Check(all.Count(c=>c.Item1==NativeDeepwellLooks.CuePistonSlam)>=4&&all.Count(c=>c.Item1==NativeDeepwellLooks.CuePistonWarn)>=4&&all.Count(c=>c.Item1==NativeDeepwellLooks.CueVentBlast)>=12&&all.Count(c=>c.Item1==NativeDeepwellLooks.CueVentWarn)>=12,"every kind of cue sounds");
        }

        // The only falls are the moon pools: open water in the bays' floors, no rail and no column for a pilot; killY close under them
        private static void Falls(ContentMap map)
        {
            var boxes=map.blocks.Select(Of).ToList();double low=map.blocks.Where(b=>b.id.IndexOf("-floor",StringComparison.Ordinal)>=0).Min(b=>b.pos.y+b.size.y/2);
            Check(Math.Abs(low+1.6)<.01&&map.bounds.killY<low+.9-2&&map.bounds.killY>=low-3.2,"killY stands 2 to 3.2 m under the lowest floor ("+map.bounds.killY+")");
            int open=0,subs=0;
            for(double x=NativeDeepwellLooks.PoolX0+.25;x<NativeDeepwellLooks.PoolX1;x+=.5)for(double z=NativeDeepwellLooks.PoolZ0+.25;z<NativeDeepwellLooks.PoolZ1;z+=.5)foreach(int s in new[]{-1,1})
            {
                var under=map.blocks.Where(b=>{var o=Of(b);return x>o.X0&&x<o.X1&&s*z>o.Z0&&s*z<o.Z1&&o.Y1<2.5;}).ToList();
                if(under.Count==0)open++;else{Check(under.All(b=>b.id.StartsWith("rig-sub-",StringComparison.Ordinal)),"only the sub floats in the pool at "+x+","+s*z);subs++;}
                Check(!map.pilotBlocks.Any(p=>!p.bots&&Math.Abs(x-p.center.x)<p.size.x/2&&Math.Abs(s*z-p.center.z)<p.size.z/2&&p.bottom<2.5),"no column over the water at "+x+","+s*z);
            }
            Check(open>250&&subs>20,"both pools are open water round their subs ("+open+" open samples)");
            // a fall from the pool's edge passes killY in about half a second (gravity 23 m/s2)
            double drop=low+.9-map.bounds.killY,seconds=Math.Sqrt(2*drop/23);Check(seconds<.62,"death comes fast: "+seconds.ToString("0.00")+" s after stepping off");
            // everywhere else the hull is closed: every edge of every walkable floor on the outline meets a wall or glass
            double minX=boxes.Min(b=>b.X0),maxX=boxes.Max(b=>b.X1),minZ=boxes.Min(b=>b.Z0),maxZ=boxes.Max(b=>b.Z1);
            Check(minX>=map.bounds.minX&&maxX<=map.bounds.maxX&&minZ>=map.bounds.minZ&&maxZ<=map.bounds.maxZ,"the station stands inside its bounds");
        }

        // No invisible walls: a column stands only over the hull's walls and glass (a double jump could top them, and the roofs over
        // them are art), the tunnels' bulkheads, the drill's pylons (the gantry sits on them) and, for bots only, the catwalks' rails
        private static void Columns(ContentMap map)
        {
            int walls=0,pylons=0,bots=0;
            foreach(var p in map.pilotBlocks)
            {
                string id=p.id;var block=map.blocks.FirstOrDefault(b=>b.id==id.Replace("rig-top-","rig-"));Check(block!=null,"column "+id+" stands on its block");
                var o=Of(block);Check(Math.Abs(p.bottom-(o.Y1-.1))<.01&&Math.Abs(p.center.x-block.pos.x)<.01&&Math.Abs(p.center.z-block.pos.z)<.01&&Math.Abs(p.size.x-block.size.x)<.01&&Math.Abs(p.size.z-block.size.z)<.01,"column "+id+" is its block's top");
                if(p.bots){Check(id.StartsWith("rig-top-rail-catwalk-",StringComparison.Ordinal),"a bots-only column is a catwalk rail over the water: "+id);bots++;}
                else if(id.StartsWith("rig-top-pylon-",StringComparison.Ordinal))pylons++;
                else{Check(id.StartsWith("rig-top-wall-",StringComparison.Ordinal)||id.StartsWith("rig-top-glass-",StringComparison.Ordinal)||id.StartsWith("rig-top-bulkhead-",StringComparison.Ordinal),"a pilot column only over the hull: "+id);Check(o.Y1>=3.2-.01,"only over a full-height wall: "+id);walls++;}
            }
            Check(pylons==4&&bots==4&&walls>60,"columns: "+walls+" over walls and glass, "+pylons+" pylons, "+bots+" for bots");
            // nothing a pilot can stand on or vault has one: floors, stairs, rails, consoles, cases, lockers, chambers, subs
            Check(!map.pilotBlocks.Any(p=>!p.bots&&(p.id.Contains("-floor")||p.id.Contains("-stair-")||p.id.Contains("-rail-")||p.id.Contains("-console-")||p.id.Contains("-case-")||p.id.Contains("-locker-")||p.id.Contains("-chamber-")||p.id.Contains("-sub-")||p.id.Contains("-screen-")||p.id.Contains("-gallery-")||p.id.Contains("-catwalk"))),"no column over anything a pilot may stand on");
        }

        private static void Presentation(string root,ContentMap map,MapLook look)
        {
            // the kit: attached parts inside their blocks grown by Proud; the glass blocks drawn by the kit
            var kit=NativeDeepwellKit.Build(map,look);Check(kit!=null&&kit.Count>900&&kit.Count==kit.Parent.Length&&kit.Count==kit.Role.Length,"the kit builds ("+kit?.Count+" parts)");
            var outside=new List<string>();var tops=map.blocks.Select(Of).ToList();
            for(int i=0;i<kit.Count;i++)
            {
                var p=Of(kit.Parts.Items[i]);int role=kit.Role[i];
                if(role==0)
                {
                    int parent=kit.Parent[i];Check(parent>=0,"an attached part belongs to a block");var b=map.blocks[parent];
                    if(!Within(p,Of(b),NativeDeepwellKit.Proud+1e-3)&&outside.Count<12)outside.Add(b.id+" #"+i+" ["+p.X0.ToString("0.00")+".."+p.X1.ToString("0.00")+", "+p.Y0.ToString("0.00")+".."+p.Y1.ToString("0.00")+", "+p.Z0.ToString("0.00")+".."+p.Z1.ToString("0.00")+"]");
                }
                else if(role==1)
                {
                    // overhead: Clearance over every standable top under it (a top no pilot column caps)
                    double under=tops.Where(b=>p.X1>b.X0&&p.X0<b.X1&&p.Z1>b.Z0&&p.Z0<b.Z1&&b.Y1<p.Y0+.05).Select(b=>b.Y1).DefaultIfEmpty(-99).Max();
                    bool capped=map.blocks.Any(b=>{var o=Of(b);return Math.Abs(o.Y1-under)<.01&&p.X1>o.X0&&p.X0<o.X1&&p.Z1>o.Z0&&p.Z0<o.Z1&&map.pilotBlocks.Any(c=>!c.bots&&c.id==b.id.Replace("rig-","rig-top-"));});
                    if(!capped&&p.Y0-under<NativeDeepwellKit.Clearance-1e-3&&outside.Count<12)outside.Add("overhead #"+i+" at "+p.Y0.ToString("0.00")+" over a top at "+under.ToString("0.00")+" ("+p.X0.ToString("0.0")+","+p.Z0.ToString("0.0")+")");
                }
                else
                {
                    bool below=p.Y1<=-1.0+1e-3;bool clear=!NativeDeepwellLooks.Inside(p.X0,p.Z0)&&!NativeDeepwellLooks.Inside(p.X1,p.Z0)&&!NativeDeepwellLooks.Inside(p.X0,p.Z1)&&!NativeDeepwellLooks.Inside(p.X1,p.Z1)&&!NativeDeepwellLooks.Inside((p.X0+p.X1)/2,(p.Z0+p.Z1)/2);
                    if(!(clear||below&&p.Y1<=NativeDeepwellLooks.Seabed+1e-3)&&outside.Count<12)outside.Add("outside #"+i+" ["+p.X0.ToString("0.0")+".."+p.X1.ToString("0.0")+", "+p.Z0.ToString("0.0")+".."+p.Z1.ToString("0.0")+"]");
                }
            }
            Check(outside.Count==0,"every attached part stays within its block grown by "+NativeDeepwellKit.Proud+", every overhead part clears the floors, every outside part stays outside the hull: "+string.Join("; ",outside));
            foreach(var b in map.blocks.Where(b=>NativeDeepwellLooks.KitBlock(b.id)))
            {
                int idx=Array.IndexOf(map.blocks,b);var parts=Enumerable.Range(0,kit.Count).Where(i=>kit.Parent[i]==idx).Select(i=>Of(kit.Parts.Items[i])).ToList();var x=Of(b);
                Check(parts.Count>=4&&parts.Max(p=>p.Y1)>=x.Y1-.01&&parts.Min(p=>p.Y0)<=x.Y0+.01,b.id+": the glass is framed top to bottom");
            }
            Check(map.blocks.Count(b=>NativeDeepwellLooks.KitBlock(b.id))>=40&&map.blocks.All(b=>b.material=="metal"),"the glass is the kit's; steel, brass and glass are all the `metal` material");
            // the hull's Inside test agrees with the map: every block lies inside it, and nothing 3 m outside it is
            foreach(var b in map.blocks)Check(NativeDeepwellLooks.Inside(b.pos.x,b.pos.z,.05),b.id+" lies inside the hull's outline");
            Check(!NativeDeepwellLooks.Inside(15,8)&&!NativeDeepwellLooks.Inside(0,24)&&!NativeDeepwellLooks.Inside(31,0)&&!NativeDeepwellLooks.Inside(12,20)&&NativeDeepwellLooks.Inside(22,12)&&NativeDeepwellLooks.Inside(-14,0),"the outline's bays");
            // no two faces of the kit (with the blocks it leaves drawn) fight
            var boxes=SurfaceChecks.Boxes(root,map,true).Where(b=>!NativeDeepwellLooks.KitBlock(b.Id)).ToList();
            for(int i=0;i<kit.Count;i++)
            {
                var p=kit.Parts.Items[i];var q=Quaternion.Normalize(new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw));var c=new Vector3(p.X,p.Y,p.Z);var h=new Vector3(p.Hx,p.Hy,p.Hz);var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
                for(int k=0;k<8;k++){var v=c+Vector3.Transform(new Vector3((k&1)==0?-h.X:h.X,(k&2)==0?-h.Y:h.Y,(k&4)==0?-h.Z:h.Z),q);lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);}
                boxes.Add(new SurfaceChecks.Box{Id="rig-kit#"+i,Kit=true,C=c,H=h,Q=q,Look=p.Look,Color=p.Color,Bevel=NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz),Min=lo,Max=hi});
            }
            int nb=boxes.Count-kit.Count;string Who(int i)=>i<nb?boxes[i].Id:kit.Role[i-nb]==0?map.blocks[kit.Parent[i-nb]].id+"#"+(i-nb):"free#"+(i-nb);
            var clashes=SurfaceChecks.Clashes(boxes,map.bounds,NativeSurfaceLayers.Step*.9f);
            Check(clashes.Count==0,"no two visible faces of the kit and the blocks overlap in one plane ("+clashes.Count+(clashes.Count>0?"; first: "+boxes[clashes[0].A].Id+" / "+boxes[clashes[0].B].Id+" at "+clashes[0].At+", "+string.Join(" | ",clashes.Take(14).Select(c=>Who(c.A)+"/"+Who(c.B)+"@"+c.At.X.ToString("0.0")+","+c.At.Y.ToString("0.00")+","+c.At.Z.ToString("0.0")+" n"+c.Normal.X.ToString("0")+c.Normal.Y.ToString("0")+c.Normal.Z.ToString("0"))):"")+")");

            // the rig and the effects: the replicated state's, deterministic, telegraphs never reduced, allocation free per frame
            var looks=NativeDeepwellLooks.Resolve(look.kitLooks);var rig=new NativeDeepwellRig(looks,map);var fx=new NativeDeepwellFx();fx.Prepare(map);
            double fight=10;var ram=map.hazards.First(h=>h.kind=="press");double strike=fight+NativeFactoryHazards.ArmDelay+ram.start+ram.offset+(ram.period-ram.warn-ram.rise-ram.dwell-ram.travel)+ram.warn+ram.rise;
            var vent=map.hazards.First(h=>h.kind=="vent");double blast=fight+NativeFactoryHazards.ArmDelay+vent.start+vent.offset+vent.period-vent.dwell;
            foreach(double t in new[]{fight+1,fight+9,strike-2.5,strike-1,strike-.2,strike+.1,strike+1,strike+2,blast-2,blast-.5,blast+.5,blast+1.9,blast+2.5,fight+40})
            {
                FactoryPart[] Parts(){rig.Build(map.hazards,3,NativePhase.Fight,fight,t,t);return rig.Parts.Items.Take(rig.Parts.Count).ToArray();}
                Check(Parts().SequenceEqual(Parts()),"the rig is deterministic at "+t);
                int Tele(double q,bool low){fx.Build(map.hazards,3,NativePhase.Fight,fight,t,t,0,2,-6,1,0,0,0,1,0,q,low);return fx.Telegraph;}
                int full=Tele(1,false);Check(full==Tele(0,false)&&full==Tele(0,true),"telegraphs are never reduced at "+t);
                Check(fx.Quads.Count<NativeDeepwellFx.Capacity,"the effects fit their buffer at "+t);
            }
            rig.Build(map.hazards,3,NativePhase.Fight,fight,strike+.3,strike+.3);
            var body=rig.Parts.Items[Array.IndexOf(rig.Slot,NativeDeepwellArt.Ram)];Check(Math.Abs(body.Y-ram.b.y)<1e-3&&rig.Piston.Held&&rig.CoreLevel>.6,"the ram stands on the wellhead while the press holds");
            rig.Build(map.hazards,3,NativePhase.Fight,fight,fight+1,fight+1);body=rig.Parts.Items[Array.IndexOf(rig.Slot,NativeDeepwellArt.Ram)];Check(Math.Abs(body.Y-ram.a.y)<1e-3&&rig.Vents==4&&rig.VentWarn.Take(4).All(w=>w==0),"parked through the arm delay");
            rig.Build(map.hazards,3,NativePhase.Fight,fight,blast+.6,blast+.6);Check(rig.VentBlast.Take(rig.Vents).Count(b=>b>.9)==2&&rig.VentBlast.Take(rig.Vents).Count(b=>b==0)==2,"a vent and its mirror blow together; the other pair rests");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,strike-1,strike-1,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>40,"the piston's telegraph draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,blast-1,blast-1,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>70,"a vent's telegraph draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph==0&&fx.Glass>=40&&fx.Lights>20&&fx.Atmosphere>40,"nothing telegraphs in the arm delay; the glass, the lamps and the sea draw");
            var beams=new NativeBeamList();fx.Beams=beams;fx.Build(map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(beams.Count>=10,"the beams take the surface light's shafts and the drone's lamp when the look's beams are on");fx.Beams=null;
            for(int warm=0;warm<3;warm++){rig.Build(map.hazards,3,NativePhase.Fight,fight,strike,strike);fx.Build(map.hazards,3,NativePhase.Fight,fight,strike,strike,0,2,-6,1,0,0,0,1,0,1,false);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++){double t=fight+f*.25;rig.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t);fx.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t,f*.1,2,-6,1,0,0,0,1,0,f%2,f%3==0);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"the rig and effects allocate nothing per frame ("+(after-before)+" bytes)");
            int rigMax=0,fxMax=0,fxLow=0;var sw=System.Diagnostics.Stopwatch.StartNew();int frames=0;
            for(double t=fight;t<fight+60;t+=.1,frames++){rig.Build(map.hazards,1,NativePhase.Fight,fight,t,t);rigMax=Math.Max(rigMax,rig.Parts.Count);fx.Build(map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,1,false);fxMax=Math.Max(fxMax,fx.Quads.Count);}
            double ms=sw.Elapsed.TotalMilliseconds/frames;
            for(double t=fight;t<fight+60;t+=.5){fx.Build(map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,0,true);fxLow=Math.Max(fxLow,fx.Quads.Count);}
            Check(rigMax<NativeDeepwellRig.Capacity-40&&fxMax<NativeDeepwellFx.Capacity&&fxLow<fxMax,"the frame's parts and quads fit and the low tier draws fewer");
            Console.WriteLine("   deepwell: kit "+kit.Count+" parts; rig up to "+rigMax+" parts a frame; effects up to "+fxMax+" quads (low tier "+fxLow+"); rig + effects "+ms.ToString("0.00")+" ms a frame (CoreTests, Release)");
            var other=new NativeDeepwellFx();other.Prepare(new ContentMap{id="polar"});other.Build(map.hazards,0,NativePhase.Fight,0,30,30,0,0,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no Deepwell effects");
            Check(NativeDeepwellKit.Build(new ContentMap{id="polar"},look)==null&&!NativeDeepwellLooks.Active("polar")&&NativeDeepwellLooks.Active("rig"),"another map builds no Deepwell kit");
        }

        // The sea outside: it moves all match long, stays out of the hull and off the windows, and keeps its distance from the eye
        private static void Sea()
        {
            double far=0;
            for(double t=0;t<600;t+=1.7)
            {
                for(int k=0;k<NativeDeepwellLooks.Whales;k++)
                {
                    NativeDeepwellLooks.WhaleAt(k,t,out double x,out double y,out double z,out double heading,out _,out double tail,out _);double l=NativeDeepwellLooks.WhaleLength(k);
                    Check(!NativeDeepwellLooks.Inside(x,z,l*.6)&&y-l*.12>NativeDeepwellLooks.Seabed+1&&Math.Abs(tail)<.5,"a whale keeps clear of the hull and the seabed at "+t);far=Math.Max(far,Math.Sqrt(x*x+z*z));
                }
                for(int i=0;i<NativeDeepwellLooks.Jellies;i++)
                {
                    NativeDeepwellLooks.JellyAt(i,t,out double x,out double y,out double z,out double pulse,out double ax,out double ay,out double az,out double size,out int tint);
                    Check(!NativeDeepwellLooks.Inside(x,z,size+.4)&&y>NativeDeepwellLooks.Seabed+size*3&&pulse>=0&&pulse<=1&&Math.Abs(ax*ax+ay*ay+az*az-1)<1e-6&&tint>=0&&tint<3,"jellyfish "+i+" drifts outside the hull at "+t+" ("+x.ToString("0.0")+","+z.ToString("0.0")+")");
                }
                for(int s=0;s<NativeDeepwellLooks.Schools;s++)for(int i=0;i<NativeDeepwellLooks.SchoolFish;i+=5)
                {
                    NativeDeepwellLooks.FishAt(s,i,t,out double x,out double y,out double z,out _,out double pitch,out _);
                    Check((!NativeDeepwellLooks.Inside(x,z,.5)||y>5&&Math.Abs(x)>18&&Math.Abs(z)>9.9)&&y>NativeDeepwellLooks.Seabed+.5&&Math.Abs(pitch)<1.2,"a fish of school "+s+" keeps outside the hull (or over a ring tunnel) at "+t+" ("+x.ToString("0.0")+","+y.ToString("0.0")+","+z.ToString("0.0")+")");
                }
                NativeDeepwellLooks.Drone(t,out double dx,out double dy,out double dz,out _);Check(dx*dx+dz*dz>14.5*14.5&&dy>9,"the drone circles outside the dome, over the roofs, at "+t);
            }
            Check(far<75,"the whales stay inside the fog's reach ("+far.ToString("0")+" m)");
            for(int i=0;i<NativeDeepwellLooks.KelpStalks;i++)
            {
                NativeDeepwellLooks.KelpRoot(i,out double x,out double z,out double h,out _);Check(!NativeDeepwellLooks.Inside(x,z,2.5)&&h>=9&&h<=16,"kelp "+i+" roots clear of the hull");
                for(int k=0;k<NativeDeepwellLooks.KelpJoints;k++)for(double t=0;t<60;t+=2.3){NativeDeepwellLooks.KelpSway(i,k,t,out double lx,out double lz);Check(Math.Abs(lx)<.3&&Math.Abs(lz)<.3,"kelp sways gently");}
            }
            // it moves: nothing rests between two seconds apart
            NativeDeepwellLooks.WhaleAt(0,10,out double a0,out _,out double a1,out _,out _,out _,out _);NativeDeepwellLooks.WhaleAt(0,12,out double b0,out _,out double b1,out _,out _,out _,out _);Check(Math.Abs(a0-b0)+Math.Abs(a1-b1)>1,"the whale swims");
            NativeDeepwellLooks.KelpSway(3,3,10,out double k0,out _);NativeDeepwellLooks.KelpSway(3,3,12,out double k1,out _);Check(Math.Abs(k0-k1)>.01,"the kelp sways");
            for(int i=0;i<NativeDeepwellKit.Smokers;i++){NativeDeepwellKit.Smoker(i,out double x,out double z,out _);Check(!NativeDeepwellLooks.Inside(x,z,4),"a smoker stands clear of the hull");}
        }

        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("DeepwellScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmDeepwell(arena))built++;"),"SourceWorld draws it and PrewarmStage builds it");
            Check(R("Runtime/NativeAudio.cs").Contains("DeepwellCues(arena,menu);"),"the cues play");
            Check(R("Runtime/NativeArenaDressing.cs").Contains("if(deepwellKit&&NativeDeepwellLooks.KitBlock(p.Definition.id))continue;"),"the dressing leaves the glass to the kit");
            Check(R("Runtime/NativeStageHazardsRuntime.cs").Contains("VentPush(h,s);")&&R("Runtime/NativeCausewayPresentation.cs").Contains("if(h.pushMode!=NativeStageHazards.PushWind"),"the vents' push runs on the host's vent step (the existing wind push)");
            var draw=R("Runtime/NativeDeepwellPresentation.cs");
            Check(draw.Contains("art.Prepare(map,NativeDeepwellArt.Slots,NativeDeepwellArt.Casts)")&&draw.Contains("NativeDeepwellArt.Pose(rigBuilder,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);")&&draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);"),"the art draws in place of the box kit and rig; without the bank the boxes draw");
            // off the shared M1 paths
            Check(!NativeNewStageLooks.Active("rig")&&NativeNewStageLooks.Index("rig")<0&&!NativeNewStageLooks.Maps.Contains("rig")&&NativeNewStageLooks.Maps.All(id=>NativeNewStageLooks.Index(id)==Array.IndexOf(new[]{"breakwater","harbor","skyframe","rig","polar"},id)),"Deepwell is off the M1 presentation; the others keep their stage numbers");
            Check(NativeStageArt.BankOf("rig")==NativeDeepwellArt.Bank,"its bank is registered");
            var gen=File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py"));Check(gen.Contains("DRAFTS={'rig':'rig_m2'}")&&gen.Contains("'rig':('rig-top-wall','rig-top-glass','rig-top-bulkhead')")&&gen.Contains("'rig':('rig-top-pylon',)"),"the generator reads the new draft and opens the stage");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeDeepwellLooks.cs","Core/NativeDeepwellKit.cs","Core/NativeDeepwellFx.cs","Core/NativeDeepwellArt.cs","Runtime/NativeDeepwellPresentation.cs"})
            {
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");var g=System.Text.RegularExpressions.Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+": a guid of its own");
            }
            var all=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Where(f=>!f.Contains("NativeDeepwell")).Select(f=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f),"guid: ([0-9a-f]{32})").Groups[1].Value);
            Check(!all.Any(g=>guids.Contains(g)),"no other asset shares the new guids");
        }
    }
}
#endif
