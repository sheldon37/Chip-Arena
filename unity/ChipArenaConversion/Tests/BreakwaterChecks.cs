#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Breakwater, rebuilt (docs/arenas/BREAKWATER.md; draft docs/arenas/scripts/breakwater_m2.py; Core NativeBreakwater*, Runtime
    // NativeBreakwaterPresentation, MapLooks.json `breakwater-blue-hour`). Against the committed catalogue and looks: the record
    // validates, sits in the shipped catalogue and in Random for a duel only; three levels on the nav graph, every spawn reachable
    // and 3 m from the wash on its floor; both long edges open onto the sea with the kill plane under its surface; the only pilot
    // columns for everyone stand on the end buildings, the turret and the Stone; the wash is an existing vent with the wind push,
    // idle for the fight's first 4 s and telegraphed for 3 s; the look resolves, styled, with the advanced lighting; the box
    // backdrop stays out of the play space; the box kit's parts keep to their blocks (the collider rule) or stand overhead, with
    // no two coplanar faces laid on each other; the rig and effects follow the clock and the replicated state, telegraphs are
    // never reduced, everything is deterministic and allocation free per frame; the lamp's beams and the lanterns fill the eight
    // light slots; each audio cue sounds once; the runtime is wired and the map has left the M1 stages' shared presentation.
    internal static class BreakwaterChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Breakwater: "+what);count++;}
        private struct Box3 {public double X0,X1,Y0,Y1,Z0,Z1;}
        private static Box3 Of(ContentBlock b)=>new Box3{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2};
        private static Box3 Of(in FactoryPart p)
        {
            var q=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var b=new Box3{X0=double.MaxValue,X1=double.MinValue,Y0=double.MaxValue,Y1=double.MinValue,Z0=double.MaxValue,Z1=double.MinValue};
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?-p.Hx:p.Hx,(c&2)==0?-p.Hy:p.Hy,(c&4)==0?-p.Hz:p.Hz),q);b.X0=Math.Min(b.X0,p.X+v.X);b.X1=Math.Max(b.X1,p.X+v.X);b.Y0=Math.Min(b.Y0,p.Y+v.Y);b.Y1=Math.Max(b.Y1,p.Y+v.Y);b.Z0=Math.Min(b.Z0,p.Z+v.Z);b.Z1=Math.Max(b.Z1,p.Z+v.Z);}
            return b;
        }
        private static bool Within(Box3 a,Box3 b,double g)=>a.X0>=b.X0-g&&a.X1<=b.X1+g&&a.Y0>=b.Y0-g&&a.Y1<=b.Y1+g&&a.Z0>=b.Z0-g&&a.Z1<=b.Z1+g;

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var text=R("Assets/Conversion/Resources/UnityMaps.json");var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);NativeMapRules.Merge(content,JsonSerializer.Deserialize<UnityMapContent>(text,o),text);
            NativeMapRules.ValidateShippedCatalog(content);
            var map=maps.maps.Single(m=>m.id==NativeBreakwaterLooks.MapId);NativeMapRules.Validate(map);
            // the small duel: in Random for 1v1 and 2v2, never for a bigger room (NativeTrainingRules.RoomFits)
            Check(map.rotation&&NativeMapRules.ArenaSelectable(map),"selectable, in the rotation");
            Check(NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,2).Select(i=>new NativeSeat{id=i,team=i%2}),true)&&NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,4).Select(i=>new NativeSeat{id=i,team=i%2}),true),"in Random for a duel and for 2v2");
            Check(!NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,8).Select(i=>new NativeSeat{id=i,team=i%2}),true)&&!NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,5).Select(i=>new NativeSeat{id=i}),false),"never in Random for a big room");
            Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
            Check(map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"block budgets ("+map.blocks.Length+")");
            Check(map.look==NativeBreakwaterLooks.Look&&map.name=="Breakwater","name and look");
            Check(map.nav.nodes.Length<1200&&map.nav.links.Length<2000,"a lean nav graph ("+map.nav.nodes.Length+" nodes)");
            var bd=map.bounds;Check(bd.maxX-bd.minX<=46&&bd.maxZ-bd.minZ<=27,"a small map ("+(bd.maxX-bd.minX)+" x "+(bd.maxZ-bd.minZ)+" m)");
            Layout(map);Nav(map);Hazards(map,maps);
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,map.look);
            Check(look!=null&&look.id==map.id&&NativeMapStyle.On(look,false)&&look.advanced!=null&&look.blocks.dressed&&look.lights.Length<=8&&look.farClip>=1200,"the look: styled, dressed, advanced, eight lights, a far plane past the town");
            Check(look.advanced.weather.kind=="rain"&&look.advanced.beams.on,"a storm's rain, and the beams on for the lamp");
            Check(look.blockLooks.All(b=>b.block.StartsWith("material:",StringComparison.Ordinal)||map.blocks.Any(k=>NativeBlockLooks.Glob(b.block,k.id))),"every block look names real blocks");
            Check((look.kitLooks??Array.Empty<MapLookBlock>()).All(e=>NativeBreakwaterLooks.Kinds.Contains(e.block)),"every kit look is a kit kind");
            Check(NativeBreakwaterLooks.Kinds.Length==NativeBreakwaterLooks.Defaults.Length&&NativeBreakwaterLooks.Kinds.Distinct().Count()==NativeBreakwaterLooks.Kinds.Length&&NativeBreakwaterLooks.Kinds.Length<=NativeFactoryKit.Kinds.Length,"the kit kinds");
            Presentation(map,look);
            Wiring(root);
            Console.WriteLine("PASS Breakwater: "+count+" checks");
        }

        private static void Layout(ContentMap map)
        {
            var bd=map.bounds;var boxes=map.blocks.Select(Of).ToArray();
            // mirror symmetric: every block has its twin across x = 0
            foreach(var b in map.blocks)Check(map.blocks.Any(c=>Math.Abs(c.pos.x+b.pos.x)<1e-3&&Math.Abs(c.pos.z-b.pos.z)<1e-3&&Math.Abs(c.pos.y-b.pos.y)<1e-3&&Math.Abs(c.size.x-b.size.x)<1e-3&&Math.Abs(c.size.y-b.size.y)<1e-3&&Math.Abs(c.size.z-b.size.z)<1e-3&&c.role==b.role),"block "+b.id+" has its twin across the centre line");
            for(int i=0;i<4;i++){var a=map.spawns.teams[0].points[i].pos;Check(map.spawns.teams[1].points.Any(p=>Math.Abs(p.pos.x+a.x)<1e-3&&Math.Abs(p.pos.z-a.z)<1e-3&&Math.Abs(p.pos.y-a.y)<1e-3),"team spawn "+i+" mirrored");}
            // open edges: the sea runs 4 m past the mole north and south; the ends are shut by the end buildings and their wings
            double north=boxes.Max(b=>b.Z1),south=boxes.Min(b=>b.Z0);
            Check(Math.Abs(north-NativeBreakwaterLooks.WallZ)<1e-3&&Math.Abs(south-NativeBreakwaterLooks.QuayZ)<1e-3&&bd.maxZ>=north+4-1e-3&&bd.minZ<=south-4+1e-3,"the bounds run 4 m past both open edges");
            Check(boxes.Min(b=>b.X0)>=bd.minX-1e-3&&boxes.Max(b=>b.X1)<=bd.maxX+1e-3,"the ends stand inside the bounds");
            // the kill plane: under the sea's surface (a fall goes under before it kills), 2 m and the body under the lowest floor
            double low=NativeBreakwaterLooks.QuayY;
            Check(bd.killY<NativeBreakwaterLooks.SeaY-.9&&low+.9-2>bd.killY&&bd.killY>=low-3.2,"killY "+bd.killY+" under the sea ("+NativeBreakwaterLooks.SeaY+"), a fast fall from the quay ("+low+")");
            Check(NativeBreakwaterLooks.SeaY<low-.5,"the sea lies under the quay");
            // no invisible walls: a column for everyone only on the end buildings (the map's ends), the turret and the Stone (the art
            // stands on them); the storm parapet's, at the lethal drop, stop bots only
            foreach(var p in map.pilotBlocks)
            {
                bool edge=p.id.StartsWith("breakwater-end-",StringComparison.Ordinal),art=p.id=="breakwater-turret-top"||p.id=="breakwater-stone-top";
                Check(p.bots?p.id.StartsWith("breakwater-parapet-",StringComparison.Ordinal):edge||art,"pilot column "+p.id+(p.bots?" (bots)":"")+" has its reason");
            }
            Check(map.pilotBlocks.Count(p=>!p.bots)==8&&map.pilotBlocks.Count(p=>p.bots)==4,"eight columns for everyone (six on the end buildings, the turret, the Stone), four for bots ("+map.pilotBlocks.Length+")");
            // cover never tops out in the band a jump half clears (1.3..2.2 m over its floor), stairs excepted
            foreach(var b in map.blocks.Where(b=>!b.id.Contains("-stair-")&&b.size.y<6))
            {
                var x=Of(b);double floor=boxes.Where(f=>f.Y1<=x.Y0+.05&&f.X0<=b.pos.x&&f.X1>=b.pos.x&&f.Z0<=b.pos.z&&f.Z1>=b.pos.z).Select(f=>f.Y1).DefaultIfEmpty(double.NaN).Max();
                if(double.IsNaN(floor)||Math.Abs(floor-x.Y0)>.05)continue;double h=x.Y1-floor;Check(!(h>1.3&&h<2.2),b.id+" tops out "+h.ToString("0.0")+" m over its floor");
            }
        }

        private static void Nav(ContentMap map)
        {
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
            string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
            // every level is on the graph: the quay, the deck, the wave wall's walk
            foreach(var y in new[]{-2.4f,0f,2.4f})Check(seen.Count(n=>Math.Abs(nodes[n].pos.y-.9f-y)<.05f)>=20,"nav reaches level "+y);
            // (the rest are the tops of cover a pilot jumps onto: the granite blocks, the boats, the crates)
            Check(seen.Count>=nodes.Count*.9,"the graph is one piece but for the tops of cover ("+seen.Count+" of "+nodes.Count+")");
            // the FFA spawns stand on all three levels
            Check(map.spawns.ffa.Select(s=>Math.Round(s.pos.y-1.1,1)).Distinct().Count()==3,"FFA spawns on three levels");
        }

        private static void Hazards(ContentMap map,UnityMapContent maps)
        {
            var hz=map.hazards;Check(hz.Length==2&&hz.All(h=>h.kind=="vent"),"two washes (vents), nothing else");
            foreach(var h in hz)
            {
                for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
                Check(h.warn>=3&&!h.lethal&&h.damage>0&&h.push>0&&h.pushMode==NativeStageHazards.PushWind&&Math.Abs(h.axis.z+1)<1e-4&&Math.Abs(h.axis.x)<1e-4,"the wash: a 3 s telegraph, damage and a push south "+h.id);
                Check(Math.Abs(h.a.z+h.size.z/2-NativeBreakwaterLooks.WallZ)<1e-3&&Math.Abs(h.a.z-h.size.z/2-NativeBreakwaterLooks.QuayZ)<1e-3&&h.a.y<=NativeBreakwaterLooks.QuayY+1e-3&&h.a.y+h.size.y>=NativeBreakwaterLooks.WalkY+1.8,"the wash runs the mole's whole width and all three floors "+h.id);
                // its phases as the presentation reads them: the telegraph, the run across, the time since
                double quiet=h.period-h.warn-h.dwell,c0=h.start+h.offset;
                NativeBreakwaterLooks.WashAt(h,0,c0+quiet+h.warn*.5,out double warn,out double wash,out double level,out _);Check(Math.Abs(warn-.5)<.02&&wash<0&&level==0,"mid telegraph "+h.id);
                NativeBreakwaterLooks.WashAt(h,0,c0+quiet+h.warn+h.dwell*.5,out warn,out wash,out level,out _);Check(warn==0&&Math.Abs(wash-.5)<.02&&level>0,"mid wash "+h.id);
                NativeBreakwaterLooks.WashAt(h,0,c0+h.period+1,out warn,out wash,out _,out double after);Check(warn==0&&wash<0&&Math.Abs(after-1)<1e-6,"a second after the wash "+h.id);
                NativeBreakwaterLooks.WashAt(h,0,c0+1,out warn,out wash,out _,out after);Check(warn==0&&wash<0&&double.IsInfinity(after),"before the first wash "+h.id);
            }
            Check(Math.Abs(hz[0].a.x+hz[1].a.x)<1e-3&&Math.Abs(Math.Abs(hz[0].offset-hz[1].offset)-hz[0].period/2)<1e-3,"the two washes mirror each other and alternate");
            // the comber reaches the wall as the telegraph ends; the front crosses the whole mole within the wash
            NativeBreakwaterLooks.Comber(0.01,-1,out double z0,out double h0);NativeBreakwaterLooks.Comber(1,-1,out double z1,out double h1);
            Check(z0>NativeBreakwaterLooks.WallZ+20&&z1<NativeBreakwaterLooks.WallZ+1&&h1>h0+3,"the comber gathers far out and stands at the wall");
            Check(NativeBreakwaterLooks.WashFront(0)==NativeBreakwaterLooks.WallZ&&NativeBreakwaterLooks.WashFront(.85)<NativeBreakwaterLooks.QuayZ,"the wash's front crosses the mole");
            // spawns: 3 m (plan) from the wash on their floor
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))foreach(var h in hz)
            {
                double dx=Math.Max(0,Math.Max(h.a.x-h.size.x/2-p.pos.x,p.pos.x-(h.a.x+h.size.x/2)));Check(dx>=3,"spawn "+p.pos.x+","+p.pos.z+" clear of "+h.id);
            }
            // the cues: each once however the frames cut the time
            var into=new FactoryCue[64];var scratch=new FactoryCue[128];var all=new List<(int,int,int)>();var whole=new List<(int,int,int)>();
            for(double t=0;t<90;t+=1/60.0){int n=NativeBreakwaterLooks.Cues(hz,0,0,t,t+1/60.0,into,scratch);for(int i=0;i<n;i++)all.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            for(double t=0;t<90;t+=.5){int n=NativeBreakwaterLooks.Cues(hz,0,0,t,t+.5,into,scratch);for(int i=0;i<n;i++)whole.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            Check(all.Count==all.Distinct().Count()&&all.OrderBy(x=>x).SequenceEqual(whole.OrderBy(x=>x)),"each cue sounds once ("+all.Count+")");
            Check(all.Count(c=>c.Item1==NativeBreakwaterLooks.CueWashWarn)>=6&&all.Count(c=>c.Item1==NativeBreakwaterLooks.CueWashWarn)-all.Count(c=>c.Item1==NativeBreakwaterLooks.CueWashBreak)<=1&&all.Count(c=>c.Item1==NativeBreakwaterLooks.CueBreak)>=30&&all.Count(c=>c.Item1==NativeBreakwaterLooks.CueBuoyBell)>=15,"every kind of cue sounds");
            // a swell's break cue falls on the moment its spray starts
            for(int i=0;i<NativeBreakwaterLooks.Swells;i++){double at=(3-(double)i/NativeBreakwaterLooks.Swells)*NativeBreakwaterLooks.SwellPeriod;Check(NativeBreakwaterLooks.SinceBreak(i,at+.25,out double bx,out double half)<.2501&&NativeBreakwaterLooks.SinceBreak(i,at-.01,out _,out _)>1&&Math.Abs(bx)<=20&&half>=8,"swell "+i+" breaks on its cue");
                var before=NativeBreakwaterLooks.SwellAt(i,at-.5);Check(before.U>.9&&Math.Abs(before.X-bx)<1e-9&&before.Z<NativeBreakwaterLooks.WallZ+3,"swell "+i+" reaches the wall where it breaks");}
        }

        private static void Presentation(ContentMap map,MapLook look)
        {
            var boxes=map.blocks.Select(Of).ToArray();var bd=map.bounds;
            // the box backdrop: builds (its sink throws on any box in the play space above the quay's floor)
            var sectors=NativeBreakwaterScenery.Build(map);Check(sectors!=null&&sectors.Length==NativeBreakwaterScenery.Sectors&&sectors.All(s=>s.Vertices>0),"the box backdrop builds");
            Check(sectors[NativeBreakwaterScenery.Landmark].MinX>bd.maxX+1&&sectors[NativeBreakwaterScenery.Landmark].MaxY>NativeBreakwaterLooks.LanternY,"the lighthouse stands past the east end, its lantern over the mole");
            Check(NativeBreakwaterLooks.LightX-5.2>bd.maxX+1,"the tower's foot lies outside the bounds");
            // the box kit: attached parts inside their blocks grown by Proud; overhead parts on a block with a column for everyone, or
            // hung Clearance over the floor
            var kit=NativeBreakwaterKit.Build(map,look);Check(kit!=null&&kit.Count>350&&kit.Count==kit.Parent.Length&&kit.Count==kit.Overhead.Length,"the kit builds ("+kit?.Count+" parts)");
            var again=NativeBreakwaterKit.Build(map,look);Check(again.Count==kit.Count&&Enumerable.Range(0,kit.Count).All(i=>kit.Parts.Items[i].Equals(again.Parts.Items[i])),"the kit is deterministic");
            var outside=new List<string>();int overhead=0;
            for(int i=0;i<kit.Count;i++)
            {
                int parent=kit.Parent[i];Check(parent>=0,"every kit part belongs to a block");var b=map.blocks[parent];var p=Of(kit.Parts.Items[i]);var x=Of(b);
                Check(b.role=="static","the kit dresses static blocks only ("+b.id+")");
                if(kit.Overhead[i])
                {
                    overhead++;bool columned=map.pilotBlocks.Any(c=>!c.bots&&c.id==b.id+"-top");
                    if(columned){Check(p.Y0>=x.Y1-1e-3&&p.X0>=x.X0-.3&&p.X1<=x.X1+.3&&p.Z0>=x.Z0-.3&&p.Z1<=x.Z1+.3,b.id+": what stands on it stays over it");continue;}
                    double floor=boxes.Where(f=>f.Y1<=p.Y0+1e-3&&f.X0<p.X1&&f.X1>p.X0&&f.Z0<p.Z1&&f.Z1>p.Z0&&!(f.X0==x.X0&&f.Z0==x.Z0&&f.Y1==x.Y1)).Select(f=>f.Y1).DefaultIfEmpty(-99).Max();
                    Check(p.Y0-floor>=NativeBreakwaterKit.Clearance,b.id+": an overhead part hangs "+(p.Y0-floor).ToString("0.00")+" m over the floor");continue;
                }
                if(!Within(p,x,NativeBreakwaterKit.Proud+1e-3)&&outside.Count<12)outside.Add(b.id+" #"+i+" ["+p.X0.ToString("0.00")+".."+p.X1.ToString("0.00")+", "+p.Y0.ToString("0.00")+".."+p.Y1.ToString("0.00")+", "+p.Z0.ToString("0.00")+".."+p.Z1.ToString("0.00")+"]");
            }
            Check(outside.Count==0,"every attached part stays within its block grown by "+NativeBreakwaterKit.Proud+": "+string.Join("; ",outside));
            Check(overhead>=40,"the turret's belfry and roof, the obelisk, the roofs and the brackets stand overhead ("+overhead+")");
            Coplanar(map,kit);
            // the rig: the lamp posts, each lantern hung from its bracket over the floor, the bell in the belfry, everything that
            // moves outside the play space or over it
            var looks=NativeBreakwaterLooks.Resolve(look.kitLooks);var rig=new NativeBreakwaterRig(looks,map);var fx=new NativeBreakwaterFx();fx.Prepare(map);
            Check(rig.Posts==8&&rig.Lantern.Length==24&&fx.FixedCount==5,"eight lamp posts; four room lamps and the Stone's");
            double fight=10;var h0=map.hazards[0];double quiet=h0.period-h0.warn-h0.dwell,warnAt=fight+NativeFactoryHazards.ArmDelay+h0.start+h0.offset+quiet,burstAt=warnAt+h0.warn;
            var seen=new int[NativeBreakwaterArt.Slots.Length];
            foreach(double t in new[]{fight+1,fight+7.3,warnAt+.5,warnAt+2.9,burstAt+.1,burstAt+1.1,burstAt+2.1,burstAt+h0.dwell+.5,burstAt+11.2,burstAt+12.5,fight+60.3})
            {
                FactoryPart[] Parts(){rig.Build(map.hazards,3,NativePhase.Fight,fight,t,t);return rig.Parts.Items.Take(rig.Parts.Count).ToArray();}
                var parts=Parts();Check(parts.SequenceEqual(Parts()),"the rig is deterministic at "+t);Check(rig.Parts.Count<NativeBreakwaterRig.Capacity-40,"the rig fits its buffer at "+t);
                for(int i=0;i<rig.Parts.Count;i++)
                {
                    int slot=rig.Slot[i];if(slot>=0)seen[slot]++;var p=Of(parts[i]);bool water=slot==NativeBreakwaterArt.Wash||slot==NativeBreakwaterArt.WashFall||slot==NativeBreakwaterArt.Comber;
                    bool inside=p.X1>bd.minX&&p.X0<bd.maxX&&p.Z1>bd.minZ&&p.Z0<bd.maxZ;if(!inside||water)continue;
                    // inside the bounds: under the quay's floor (the swells), or overhead (the lanterns 2 m over their floors, the bell
                    // and flags over columned blocks)
                    if(p.Y1<=NativeBreakwaterLooks.QuayY-.05)continue;
                    double floor=boxes.Where(f=>f.Y1<=p.Y0+.05&&f.X0<p.X1&&f.X1>p.X0&&f.Z0<p.Z1&&f.Z1>p.Z0).Select(f=>f.Y1).DefaultIfEmpty(-99).Max();
                    Check(p.Y0-floor>=1.95||floor>5,"a rig part at "+parts[i].X.ToString("0.0")+", "+parts[i].Y.ToString("0.0")+", "+parts[i].Z.ToString("0.0")+" (slot "+slot+") hangs "+(p.Y0-floor).ToString("0.00")+" m over the floor at "+t);
                }
                for(int i=0;i<rig.Posts;i++)
                {
                    NativeBreakwaterRig.Bracket(rig.PostX(i),rig.PostTop(i),rig.PostZ(i),out double px,out double py,out double pz);double dx=rig.Lantern[i*3]-px,dy=rig.Lantern[i*3+1]-py,dz=rig.Lantern[i*3+2]-pz;
                    Check(Math.Abs(Math.Sqrt(dx*dx+dy*dy+dz*dz)-NativeBreakwaterRig.LanternDrop)<1e-6&&dy<0,"lantern "+i+" hangs from its bracket at "+t);
                }
                int Tele(double q,bool low){fx.Build(rig,map.hazards,3,NativePhase.Fight,fight,t,t,0,2,-6,1,0,0,0,1,0,q,low);return fx.Telegraph;}
                int full=Tele(1,false);Check(full==Tele(0,false)&&full==Tele(0,true),"telegraphs are never reduced at "+t);
                Check(fx.Quads.Count<NativeBreakwaterFx.Capacity-200,"the effects fit their buffer at "+t+" ("+fx.Quads.Count+")");
                // the lights: slot 0 the lamp as two beams at the optic's heading, the others lanterns where they hang
                Check(Math.Abs(fx.LightBeam[0]-(8+NativeBreakwaterLooks.BeamHeading(t)))<1e-4&&fx.LightBeam[0]>=8&&fx.LightBeam[0]<14.3&&Math.Abs(fx.LightPos[1]-NativeBreakwaterLooks.LanternY)<1e-4&&fx.LightRgb[0]>1,"slot 0 is the lamp, its beams turning with the optic at "+t);
                Check(Math.Abs(rig.Heading-NativeBreakwaterLooks.BeamHeading(t))<1e-12,"the optic turns with the lamp");
                int lit=0;for(int s=1;s<NativeMapLooks.MaxLights;s++){if(fx.LightRgb[s*3]<=0)continue;lit++;Check(fx.LightBeam[s]==0&&(Enumerable.Range(0,rig.Posts).Any(i=>Math.Abs(rig.Lantern[i*3]-fx.LightPos[s*4])<1e-4&&Math.Abs(rig.Lantern[i*3+1]-fx.LightPos[s*4+1])<1e-4&&Math.Abs(rig.Lantern[i*3+2]-fx.LightPos[s*4+2])<1e-4)||Enumerable.Range(0,fx.FixedCount).Any(i=>Math.Abs(fx.Fixed[i*5]-fx.LightPos[s*4])<1e-4&&Math.Abs(fx.Fixed[i*5+1]-fx.LightPos[s*4+1])<1e-4&&Math.Abs(fx.Fixed[i*5+2]-fx.LightPos[s*4+2])<1e-4)),"slot "+s+" is a lantern or a fixed lamp at "+t);}
                Check(lit>=5,"the lanterns near the eye are lit ("+lit+")");
            }
            Check(seen.All(n=>n>0),"the moments show every rig slot ("+string.Join(",",NativeBreakwaterArt.Slots.Where((s,i)=>seen[i]==0))+")");
            // the telegraph and the wash draw; nothing telegraphs in the arm delay or between washes
            fx.Build(rig,map.hazards,3,NativePhase.Fight,fight,warnAt+1.5,warnAt+1.5,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>40,"the wash's telegraph draws ("+fx.Telegraph+")");
            fx.Build(rig,map.hazards,3,NativePhase.Fight,fight,burstAt+1,burstAt+1,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>60,"the wash draws ("+fx.Telegraph+")");
            fx.Build(rig,map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph==0&&fx.Atmosphere>40&&fx.Lights>20,"nothing telegraphs in the arm delay; the sea and the lights draw");
            var beams=new NativeBeamList();fx.Beams=beams;fx.Build(rig,map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(beams.Count==2,"the beam list takes the lamp's two beams when the look's beams are on");fx.Beams=null;
            // the beam crosses the arena: over a turn of the lamp the deck's middle is lit twice
            int peaks=0;double last=0,prev=0;for(double t=0;t<2*Math.PI/NativeBreakwaterLooks.BeamTurn;t+=.02){double v=NativeBreakwaterLooks.BeamOn(t,0,0);if(prev>last&&prev>v&&prev>.9)peaks++;last=prev;prev=v;}
            Check(peaks==2,"both beams cross the deck's middle each turn ("+peaks+")");
            // gulls keep over the ceiling; the buoy and the boats ride outside the bounds
            for(double t=0;t<300;t+=1.7)for(int i=0;i<NativeBreakwaterLooks.Gulls;i++){NativeBreakwaterLooks.GullAt(i,t,out double x,out double y,out double z,out _,out _,out double flap);Check(y>bd.ceiling&&Math.Abs(flap)<.6,"gull "+i+" wheels over the ceiling");}
            Check(NativeBreakwaterLooks.BuoyZ>bd.maxZ+8&&NativeBreakwaterLooks.BoatZ<bd.minZ-3&&NativeBreakwaterLooks.DinghyZ<bd.minZ-1.5,"the buoy and the boats lie outside the bounds");
            // allocation free per frame
            for(int warm=0;warm<3;warm++){rig.Build(map.hazards,3,NativePhase.Fight,fight,burstAt+1,burstAt+1);fx.Build(rig,map.hazards,3,NativePhase.Fight,fight,burstAt+1,burstAt+1,0,2,-6,1,0,0,0,1,0,1,false);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++){double t=fight+f*.25;rig.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t);fx.Build(rig,map.hazards,f&3,NativePhase.Fight,fight,t,t,f*.1,2,-6,1,0,0,0,1,0,f%2,f%3==0);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"the rig and effects allocate nothing per frame ("+(after-before)+" bytes)");
            // the numbers (printed, not pinned)
            int rigMax=0,fxMax=0,fxLow=0;var sw=System.Diagnostics.Stopwatch.StartNew();int frames=0;
            for(double t=fight;t<fight+60;t+=.1,frames++){rig.Build(map.hazards,1,NativePhase.Fight,fight,t,t);rigMax=Math.Max(rigMax,rig.Parts.Count);fx.Build(rig,map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,1,false);fxMax=Math.Max(fxMax,fx.Quads.Count);}
            double ms=sw.Elapsed.TotalMilliseconds/frames;
            for(double t=fight;t<fight+60;t+=.5){rig.Build(map.hazards,1,NativePhase.Fight,fight,t,t);fx.Build(rig,map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,0,true);fxLow=Math.Max(fxLow,fx.Quads.Count);}
            Check(fxLow<fxMax,"the low tier draws fewer quads");
            Console.WriteLine("   breakwater: kit "+kit.Count+" parts ("+overhead+" overhead); rig up to "+rigMax+" parts a frame; effects up to "+fxMax+" quads (low tier "+fxLow+"); box backdrop "+sectors.Sum(m=>m.Vertices)+" vertices in "+sectors.Length+" meshes; rig + effects "+ms.ToString("0.00")+" ms a frame (CoreTests, Release)");
            // another map draws nothing of this
            var other=new NativeBreakwaterFx();other.Prepare(new ContentMap{id="polar"});other.Build(rig,map.hazards,0,NativePhase.Fight,0,30,30,0,0,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no Breakwater effects");
            Check(NativeBreakwaterScenery.Build(new ContentMap{id="polar"})==null&&NativeBreakwaterKit.Build(new ContentMap{id="polar"},look)==null,"another map builds no Breakwater scenery or kit");
        }

        // The box kit lays no two coplanar faces on each other: wherever two parts' faces (or a part's and a block's) turn the same
        // way in one plane (within 2 mm) and overlap by more than a hand's breadth, they are the same colour's joints crossing
        // (the deck's seams) or the check fails (a strip under 2 cm wide, where a plate is let into its block, is not counted). Axis-aligned parts only (the turned ones are bars in the air).
        private static void Coplanar(ContentMap map,NativeBreakwaterKit kit)
        {
            var faces=new List<(int axis,int sign,double d,double a0,double a1,double b0,double b1,int owner,int color)>();
            void Add(Box3 x,int owner,int color)
            {
                faces.Add((0,1,x.X1,x.Y0,x.Y1,x.Z0,x.Z1,owner,color));faces.Add((0,-1,x.X0,x.Y0,x.Y1,x.Z0,x.Z1,owner,color));faces.Add((1,1,x.Y1,x.X0,x.X1,x.Z0,x.Z1,owner,color));
                faces.Add((2,1,x.Z1,x.X0,x.X1,x.Y0,x.Y1,owner,color));faces.Add((2,-1,x.Z0,x.X0,x.X1,x.Y0,x.Y1,owner,color));
            }
            // (a part's face counts only where it stands flush with or proud of its own block on that side: its back, sunk in the
            // block, is never seen)
            for(int i=0;i<kit.Count;i++)
            {
                ref var p=ref kit.Parts.Items[i];if(Math.Abs(p.Qw)<.9999f)continue;int n=faces.Count;Add(Of(p),i,p.Color);var x=Of(map.blocks[kit.Parent[i]]);
                faces.RemoveAll(f=>f.owner==i&&(f.axis==0?(f.sign>0?f.d<x.X1-1e-6:f.d>x.X0+1e-6):f.axis==1?f.d<x.Y1-1e-6:(f.sign>0?f.d<x.Z1-1e-6:f.d>x.Z0+1e-6)));
            }
            int blocks=0;foreach(var b in map.blocks.Where(b=>b.role=="static")){Add(Of(b),-1-blocks,-1);blocks++;}
            double worst=0;string where="";int pairs=0;var list2=new List<string>();
            foreach(var g in faces.GroupBy(f=>(f.axis,f.sign,Math.Round(f.d/.002))))
            {
                var list=g.ToList();
                for(int i=0;i<list.Count;i++)for(int j=i+1;j<list.Count;j++)
                {
                    var a=list[i];var b=list[j];if(a.owner==b.owner||a.owner<0&&b.owner<0)continue;if(Math.Abs(a.d-b.d)>.002)continue;
                    double oa=Math.Min(a.a1,b.a1)-Math.Max(a.a0,b.a0),ob=Math.Min(a.b1,b.b1)-Math.Max(a.b0,b.b0);if(oa<=.021||ob<=.021)continue;
                    if(a.color==b.color&&a.owner>=0&&b.owner>=0)continue;double area=oa*ob;pairs++;if(list2.Count<12)list2.Add((a.owner>=0?map.blocks[kit.Parent[a.owner]].id:"block")+"/"+(b.owner>=0?map.blocks[kit.Parent[b.owner]].id:"block")+" axis "+a.axis+" d "+a.d.ToString("0.00")+" "+area.ToString("0.000"));if(area>worst){worst=area;where="axis "+a.axis+" at "+a.d.ToString("0.000")+", "+Math.Max(a.a0,b.a0).ToString("0.00")+", "+Math.Max(a.b0,b.b0).ToString("0.00");}
                }
            }
            Check(worst<=.01,"the box kit lays no coplanar faces on each other ("+pairs+" pairs, the largest "+worst.ToString("0.000")+" m2 at "+where+"): "+string.Join("; ",list2));
        }

        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("BreakwaterScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmBreakwater(arena))built++;"),"SourceWorld draws it and PrewarmStage builds it");
            Check(R("Runtime/NativeAudio.cs").Contains("BreakwaterCues(arena,menu);"),"the cues play");
            var draw=R("Runtime/NativeBreakwaterPresentation.cs");
            Check(draw.Contains("NativeLighting.PickedLights(fx.LightPos,fx.LightRgb,fx.LightBeam);NativeLighting.ActorLights(true);"),"the lamp's beams and the lanterns light the stage and the pilots");
            Check(draw.Contains("rigBuilder.Build(map.hazards,round,match.Phase,match.FightAt,now,now);")&&draw.Contains("breakwater.Render(arena,FieldClock(arena),WorldEye,EffectsQuality,LowGraphics,UsesInstancing);"),"everything moves on the drawn arena's field clock (the killcam shows the same sea)");
            // the wash is the existing wind push, run by the host's vent step (no rules change)
            Check(R("Runtime/NativeStageHazardsRuntime.cs").Contains("VentPush(h,s);")&&R("Runtime/NativeCausewayPresentation.cs").Contains("if(h.pushMode!=NativeStageHazards.PushWind||s.Phase!=NativeFactoryHazards.Bursting||!(h.push>0))return;"),"the wash's push is the host's existing wind push");
            // the map has left the M1 stages' shared presentation
            Check(!NativeNewStageLooks.Active(NativeBreakwaterLooks.MapId)&&!NativeNewStageLooks.Maps.Contains(NativeBreakwaterLooks.MapId)&&NativeNewStageLooks.Index(NativeBreakwaterLooks.MapId)<0,"the M1 presentation no longer draws Breakwater");
            Check(NativeNewStageLooks.Index("harbor")==1&&NativeNewStageLooks.Index("skyframe")==2&&NativeNewStageLooks.Index("rig")==3&&NativeNewStageLooks.Index("polar")==4&&NativeNewStageLooks.Maps.Length==NativeNewStageLooks.Looks.Length,"the other M1 stages keep their stage numbers");
            Check(!File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PackDressing.json")).Contains("\"breakwater\""),"the Toon Harbor kit no longer dresses Breakwater");
            string gen=File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py"));
            Check(gen.Contains("EDGES['breakwater']=('breakwater-end-',)")&&gen.Contains("OVERHANG['breakwater']=('breakwater-turret','breakwater-stone')")&&gen.Contains("{name}_m2.py"),"the generator opens the stage and reads the M2 draft");
            Check(File.Exists(Path.Combine(root,"../../docs/arenas/scripts/breakwater_m2.py"))&&File.Exists(Path.Combine(root,"../../docs/arenas/scripts/breakwater_m1.py")),"the M2 draft, the M1 kept as history");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeBreakwaterLooks.cs","Core/NativeBreakwaterScenery.cs","Core/NativeBreakwaterKit.cs","Core/NativeBreakwaterFx.cs","Core/NativeBreakwaterArt.cs","Runtime/NativeBreakwaterPresentation.cs"})
            {
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");var g=System.Text.RegularExpressions.Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+": a guid of its own");
            }
            var all=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Where(f=>!f.Contains("NativeBreakwater")).Select(f=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f),"guid: ([0-9a-f]{32})").Groups[1].Value);
            Check(!all.Any(g=>guids.Contains(g)),"no other asset shares the new guids");
        }
    }
}
#endif
