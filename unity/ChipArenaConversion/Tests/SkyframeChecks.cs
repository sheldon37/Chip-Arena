#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Skyframe, rebuilt 2026-11-11 (docs/arenas/SKYFRAME.md; draft docs/arenas/scripts/skyframe_m2.py; Core NativeSkyframe*, Runtime
    // NativeSkyframePresentation, MapLooks.json `skyframe-golden-hour`). Against the committed catalogue and looks: the record
    // validates, sits in the shipped catalogue and Random, every spawn is reachable on the nav graph and keeps 3 m from every hazard
    // on its floor; the hazards are the existing kinds (the crane's beam drop, the crosswind), idle for the fight's first 4 s and
    // telegraphed at least as long as before; the edges are open (no pilot column but the trees' and, for bots, the balustrades at
    // lethal drops); the look resolves, styled, with the advanced lighting, its key the stage's sun; the backdrop stays out of the play
    // space; the kit's parts stay inside their blocks (the collider rule) and the frame overhead clears every floor; the rig and
    // effects follow the replicated state, the carried bundle swings round the frame, telegraphs are never reduced, everything is
    // deterministic and allocation free per frame; each audio cue sounds once; the runtime is wired and the stage has left the M1
    // batch's shared presentation and the harbour kit.
    internal static class SkyframeChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Skyframe: "+what);count++;}
        private struct Box3 {public double X0,X1,Y0,Y1,Z0,Z1;}
        private static Box3 Of(ContentBlock b)=>new Box3{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2};
        private static Box3 Of(in FactoryPart p)
        {
            var q=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var b=new Box3{X0=double.MaxValue,X1=double.MinValue,Y0=double.MaxValue,Y1=double.MinValue,Z0=double.MaxValue,Z1=double.MinValue};
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?-p.Hx:p.Hx,(c&2)==0?-p.Hy:p.Hy,(c&4)==0?-p.Hz:p.Hz),q);b.X0=Math.Min(b.X0,p.X+v.X);b.X1=Math.Max(b.X1,p.X+v.X);b.Y0=Math.Min(b.Y0,p.Y+v.Y);b.Y1=Math.Max(b.Y1,p.Y+v.Y);b.Z0=Math.Min(b.Z0,p.Z+v.Z);b.Z1=Math.Max(b.Z1,p.Z+v.Z);}
            return b;
        }
        private static bool Within(Box3 a,Box3 b,double g)=>a.X0>=b.X0-g&&a.X1<=b.X1+g&&a.Y0>=b.Y0-g&&a.Y1<=b.Y1+g&&a.Z0>=b.Z0-g&&a.Z1<=b.Z1+g;
        private static bool Floor(ContentBlock b)=>b.id=="skyframe-garden"||b.id.StartsWith("skyframe-gallery-")||b.id.StartsWith("skyframe-bridge-")||b.id.StartsWith("skyframe-terrace-")||b.id.StartsWith("skyframe-landing-")||b.id.StartsWith("skyframe-stair-")||b.id.EndsWith("-roof");

        public static void Run(string root)
        {
            count=0;var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var text=R("Assets/Conversion/Resources/UnityMaps.json");var maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);
            var content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);NativeMapRules.Merge(content,JsonSerializer.Deserialize<UnityMapContent>(text,o),text);
            NativeMapRules.ValidateShippedCatalog(content);
            var map=maps.maps.Single(m=>m.id==NativeSkyframeLooks.MapId);NativeMapRules.Validate(map);
            Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,8).Select(i=>new NativeSeat{id=i,team=i%2}),true),"selectable and in Random for a full room");
            Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
            Check(map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"block budgets ("+map.blocks.Length+")");
            Check(map.look==NativeSkyframeLooks.Look&&map.name=="Skyframe"&&map.blocks.All(b=>b.id.StartsWith("skyframe-",StringComparison.Ordinal)),"name, look and id stems");
            Check(map.nav.nodes.Length<2600&&map.nav.links.Length<4200,"a lean nav graph ("+map.nav.nodes.Length+" nodes)");
            // point symmetric: every block has its twin across the centre
            foreach(var b in map.blocks)Check(map.blocks.Any(c=>Math.Abs(c.pos.x+b.pos.x)<1e-3&&Math.Abs(c.pos.z+b.pos.z)<1e-3&&Math.Abs(c.pos.y-b.pos.y)<1e-3&&Math.Abs(c.size.x-b.size.x)<1e-3&&Math.Abs(c.size.y-b.size.y)<1e-3&&Math.Abs(c.size.z-b.size.z)<1e-3),"block "+b.id+" has its twin across the centre");
            Nav(map);Hazards(map);Edges(map);
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,map.look);
            Check(look!=null&&look.id==map.id&&NativeMapStyle.On(look,false)&&look.advanced!=null&&look.blocks.dressed&&look.lights.Length<=8&&look.farClip>=2000,"the look: styled, dressed, advanced, at most eight lights, a far plane past the cloud banks");
            Check(look.blockLooks.All(b=>b.block.StartsWith("material:",StringComparison.Ordinal)||map.blocks.Any(k=>NativeBlockLooks.Glob(b.block,k.id))),"every block look names real blocks");
            Check(Math.Abs(look.key.elevation-NativeSkyframeLooks.SunElevation)<1e-6&&Math.Abs(look.key.bearing-NativeSkyframeLooks.SunBearing)<1e-6&&look.key.elevation<25,"the look's key is the stage's low sun");
            NativeSkyframeLooks.Sun(out double sx,out double sy,out double sz);Check(Math.Abs(sx*sx+sy*sy+sz*sz-1)<1e-9&&sy>0&&Math.Abs(sx)<Math.Abs(sz),"the sun stands low, off the teams' axis (neither base looks into it)");
            Presentation(map,look);
            Retired(root,maps,map);
            Wiring(root);
            Console.WriteLine("PASS Skyframe: "+count+" checks");
        }

        private static void Nav(ContentMap map)
        {
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
            string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
            // every level of the map is on the graph: the cloud terraces, the garden, the gallery and the roofs
            foreach(var y in new[]{-3.2f,0f,3.6f})Check(seen.Any(n=>Math.Abs(nodes[n].pos.y-.9f-y)<.05f),"nav reaches level "+y);
            Check(seen.Any(n=>nodes[n].pos.y>4&&nodes[n].pos.x<-15&&nodes[n].pos.z<-8)&&seen.Any(n=>nodes[n].pos.y>4&&Math.Abs(nodes[n].pos.x)<9&&Math.Abs(nodes[n].pos.z)<9),"both the roofs and the gallery are reached");
        }

        private static void Hazards(ContentMap map)
        {
            var hz=map.hazards;Check(hz.Count(h=>h.kind=="crane")==1&&hz.Count(h=>h.kind=="vent")==2&&hz.Length==3,"the crane and two crosswinds");
            var crane=hz.Single(h=>h.kind=="crane");
            foreach(var h in hz)for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
            Check(crane.zones.Length==4&&crane.warn>=3&&crane.lethal&&crane.zones.All(z=>crane.carry>z.center.y+z.size.y)&&Math.Abs(crane.a.y-NativeSkyframeLooks.JibY)<1e-6,"the crane: four zones, a 3 s telegraph, the load carried clear, its jib at the hazard's height");
            Check(crane.zones.Count(z=>Math.Abs(z.center.y)<.01f)==2&&crane.zones.Count(z=>Math.Abs(z.center.y-3.6f)<.01f)==2,"two zones on the lawns and two on the pavilion roofs");
            // the zones ring the tower, each standing on a floor with nothing over it; a hop never crosses the frame
            foreach(var z in crane.zones)
            {
                Check(map.blocks.Any(b=>Floor(b)&&Math.Abs(Of(b).Y1-z.center.y)<.01&&z.center.x-z.size.x/2>=Of(b).X0&&z.center.x+z.size.x/2<=Of(b).X1&&z.center.z-z.size.z/2>=Of(b).Z0&&z.center.z+z.size.z/2<=Of(b).Z1),"a zone lies on one floor");
                Check(!map.blocks.Any(b=>Of(b).Y1>z.center.y+.05&&Of(b).X1>z.center.x-z.size.x/2-.4&&Of(b).X0<z.center.x+z.size.x/2+.4&&Of(b).Z1>z.center.z-z.size.z/2-.4&&Of(b).Z0<z.center.z+z.size.z/2+.4),"a zone's shaft is open");
            }
            double F=NativeSkyframeLooks.FrameHalf+.3;int exact=0,travelling=0;
            for(int round=0;round<2;round++)for(double clock=-2;clock<170;clock+=.05)
            {
                var m=NativeSkyframeLooks.CraneAt(crane,round,clock);var st=m.State;
                if(st.Phase==NativeFactoryHazards.Travelling)travelling++;else if(st.Cycle>=0){Check(m.Exact&&Math.Abs(m.X-st.X)<1e-6&&Math.Abs(m.Z-st.Z)<1e-6,"off its travel the drawn hook is the simulation's");exact++;}
                Check(m.Reach>=NativeSkyframeLooks.MinReach-1e-9&&m.Reach<NativeSkyframeLooks.JibLength-1,"the trolley stays on the jib");
                // the carried bundle (along x, as its collider lies) never enters the frame's plan above the gallery
                if(m.LoadY>5)Check(Math.Abs(m.X)-crane.size.x/2>F||Math.Abs(m.Z)-crane.size.z/2>F,"the bundle swings round the frame at "+clock.ToString("0.00")+" ("+m.X.ToString("0.0")+", "+m.Z.ToString("0.0")+")");
                if(st.Landed)Check(Math.Abs(m.LoadY-m.FloorY)<1e-6,"landed on its zone's floor");
            }
            Check(exact>0&&travelling>0,"the crane travels and works");
            {
                double impact=crane.start+crane.offset+crane.travel+crane.warn+crane.rise;
                Check(!NativeFactoryHazards.State(crane,0,impact-crane.rise-.1).Lethal&&NativeFactoryHazards.State(crane,0,impact-.02).Lethal&&NativeFactoryHazards.State(crane,0,impact+.02).Lethal&&!NativeFactoryHazards.State(crane,0,impact+2).Lethal,"the drop's lethal window: the fall's end and the impact, never the telegraph or the landed bundle");
                Check(Math.Abs(NativeSkyframeLooks.CraneAt(crane,0,impact+.3).Since-.3)<1e-6&&NativeSkyframeLooks.CraneAt(crane,0,impact-1).Warn>.5,"the crane's moment: its telegraph and the time since the impact");
            }
            foreach(var h in hz.Where(h=>h.kind=="vent"))
            {
                Check(h.warn>=.8f&&!h.lethal&&h.damage>0&&h.push>0&&h.pushMode==NativeStageHazards.PushWind&&Math.Abs(h.axis.Length-1)<1e-4,"the wind's telegraph, damage and push "+h.id);
                Check(Math.Sign(h.axis.z)==Math.Sign(h.a.z)&&Math.Abs(h.axis.x)<1e-6&&Math.Abs(h.a.y-NativeSkyframeLooks.TerraceY)<1e-4,"the wind blows off its cloud terrace "+h.id);
                // the gap it blows toward: no balustrade across the middle of the section's outer edge
                double edge=h.a.z+Math.Sign(h.axis.z)*h.size.z/2;
                Check(!map.blocks.Any(b=>b.id.StartsWith("skyframe-rail-")&&Math.Abs(b.pos.z-edge)<.3&&Math.Abs(b.pos.x-h.a.x)<b.size.x/2+1),"the outer balustrade is open before the gust "+h.id);
                NativeSkyframeRig.SockAt(h,out double sxk,out double syk,out double szk);var deck=map.blocks.Single(b=>b.id.StartsWith("skyframe-terrace-")&&Math.Sign(b.pos.z)==Math.Sign(h.a.z));
                Check(Math.Abs(szk)>Math.Abs(deck.pos.z)+deck.size.z/2+.1&&Math.Abs(szk)<Math.Abs(deck.pos.z)+deck.size.z/2+.5&&sxk>Of(deck).X0&&sxk<Of(deck).X1&&Math.Abs(sxk-h.a.x)>h.size.x/2&&syk>h.a.y+3,"its windsock stands on a pole just off the terrace's outer edge, beside the gust's section");
            }
            // spawns: 3 m (plan) from every live volume spanning their body on their floor
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))
            {
                double fy=p.pos.y-1.1;
                void Clear(double x0,double x1,double z0,double z1,double y0,double y1,string id)
                {
                    if(y0-.1>fy+1.8||y1<fy)return;double dx=Math.Max(0,Math.Max(x0-p.pos.x,p.pos.x-x1)),dz=Math.Max(0,Math.Max(z0-p.pos.z,p.pos.z-z1));
                    Check(Math.Sqrt(dx*dx+dz*dz)>=3,"spawn "+p.pos.x+","+p.pos.z+" clear of "+id);
                }
                foreach(var h in hz)
                {
                    if(h.kind=="vent")Clear(h.a.x-h.size.x/2,h.a.x+h.size.x/2,h.a.z-h.size.z/2,h.a.z+h.size.z/2,h.a.y,h.a.y+h.size.y,h.id);
                    else foreach(var z in h.zones)Clear(z.center.x-z.size.x/2,z.center.x+z.size.x/2,z.center.z-z.size.z/2,z.center.z+z.size.z/2,z.center.y,h.carry+h.size.y,h.id);
                }
            }
            // the cues: each once however the frames cut the time
            var into=new FactoryCue[64];var scratch=new FactoryCue[128];var all=new List<(int,int,int)>();var whole=new List<(int,int,int)>();
            for(double t=0;t<90;t+=1/60.0){int n=NativeSkyframeLooks.Cues(hz,0,0,t,t+1/60.0,into,scratch);for(int i=0;i<n;i++)all.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            for(double t=0;t<90;t+=.5){int n=NativeSkyframeLooks.Cues(hz,0,0,t,t+.5,into,scratch);for(int i=0;i<n;i++)whole.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            Check(all.OrderBy(x=>x).SequenceEqual(whole.OrderBy(x=>x))&&all.Count>20,"each cue sounds once ("+all.Count+")");
            Check(all.Count(c=>c.Item1==NativeSkyframeLooks.CueCraneImpact)>=4&&all.Count(c=>c.Item1==NativeSkyframeLooks.CueCraneHorn)==2*all.Count(c=>c.Item1==NativeSkyframeLooks.CueCraneRelease)&&all.Count(c=>c.Item1==NativeSkyframeLooks.CueWindGust)>=6&&all.Count(c=>c.Item1==NativeSkyframeLooks.CueWindWarn)>=6,"every kind of cue sounds");
        }

        // The owner's rule (twice): no invisible walls. The only pilot columns left for players stand over the trees (their crowns
        // hang over the trunks' tops); every other is a balustrade's at a lethal drop and stops bots alone. Falls kill fast.
        private static void Edges(ContentMap map)
        {
            var forAll=map.pilotBlocks.Where(p=>!p.bots).ToArray();
            Check(forAll.Length==8&&forAll.All(p=>p.id.StartsWith("skyframe-tree-",StringComparison.Ordinal)),"only the eight trees keep a column for players ("+string.Join(",",forAll.Select(p=>p.id))+")");
            Check(map.pilotBlocks.Where(p=>p.bots).All(p=>p.id.StartsWith("skyframe-rail-",StringComparison.Ordinal))&&map.pilotBlocks.Count(p=>p.bots)>=20,"every other column is a balustrade's at a lethal drop, for bots only");
            double lowest=map.blocks.Where(Floor).Min(b=>Of(b).Y1);
            Check(Math.Abs(lowest-NativeSkyframeLooks.TerraceY)<1e-4&&map.bounds.killY<=lowest+.9-2&&map.bounds.killY>=lowest-3.5,"falls kill fast: killY "+map.bounds.killY+" a body under the lowest floor "+lowest);
            double x0=map.blocks.Min(b=>Of(b).X0),x1=map.blocks.Max(b=>Of(b).X1),z0=map.blocks.Min(b=>Of(b).Z0),z1=map.blocks.Max(b=>Of(b).Z1);
            Check(map.bounds.minX<=x0-4&&map.bounds.maxX>=x1+4&&map.bounds.minZ<=z0-4&&map.bounds.maxZ>=z1+4,"the bounds run 4 m past every open edge");
            // the balustrades are low enough to vault and every edge has its gaps
            Check(map.blocks.Where(b=>b.id.StartsWith("skyframe-rail-")).All(b=>Math.Abs(b.size.y-1.1f)<1e-4),"every balustrade is 1.1 m");
        }

        private static void Presentation(ContentMap map,MapLook look)
        {
            var sectors=NativeSkyframeScenery.Build(map);Check(sectors!=null&&sectors.Length==NativeSkyframeScenery.Sectors&&sectors.All(s=>s.Vertices>0),"the backdrop builds (its sink throws on any box in the play space)");
            var kit=NativeSkyframeKit.Build(map,look);Check(kit!=null&&kit.Count>900&&kit.Count==kit.Parent.Length,"the kit builds ("+kit?.Count+" parts)");
            var outside=new List<string>();int overhead=0;var floors=map.blocks.Where(Floor).Select(Of).ToArray();
            for(int i=0;i<kit.Count;i++)
            {
                int parent=kit.Parent[i];var p=Of(kit.Parts.Items[i]);
                if(parent<0)
                {
                    // overhead: inside the frame's plan and Clearance over every floor under it
                    overhead++;double f=NativeSkyframeLooks.FrameHalf;Check(p.X0>=-f-1e-3&&p.X1<=f+1e-3&&p.Z0>=-f-1e-3&&p.Z1<=f+1e-3,"the frame keeps to its plan (part "+i+")");
                    double under=floors.Where(b=>p.X1>b.X0&&p.X0<b.X1&&p.Z1>b.Z0&&p.Z0<b.Z1&&b.Y1<=p.Y0+.01).Select(b=>b.Y1).DefaultIfEmpty(-99).Max();
                    Check(p.Y0-under>=NativeSkyframeKit.Clearance,"the frame clears every floor under it (part "+i+": "+p.Y0.ToString("0.00")+" over "+under.ToString("0.00")+")");continue;
                }
                var b=map.blocks[parent];
                if(!Within(p,Of(b),NativeSkyframeKit.Proud+1e-3)&&outside.Count<12)outside.Add(b.id+" #"+i+" ["+p.X0.ToString("0.00")+".."+p.X1.ToString("0.00")+", "+p.Y0.ToString("0.00")+".."+p.Y1.ToString("0.00")+", "+p.Z0.ToString("0.00")+".."+p.Z1.ToString("0.00")+"]");
            }
            Check(outside.Count==0,"every attached part stays within its block grown by "+NativeSkyframeKit.Proud+": "+string.Join("; ",outside));
            Check(overhead>60,"the unfinished frame stands over the gallery ("+overhead+" parts)");
            for(int c=0;c<8;c++)Check(NativeSkyframeKit.FrameRise(c)>=3&&NativeSkyframeKit.FrameRise(c)<=NativeSkyframeLooks.Storeys,"a column's rise");
            Check(NativeSkyframeLooks.FrameY+(NativeSkyframeLooks.Storeys-1)*NativeSkyframeLooks.Storey+1.2<NativeSkyframeLooks.JibY-8,"the jib swings clear over the frame");
            // the rig and the effects: the replicated state's, deterministic, telegraphs never reduced, allocation free per frame
            var looks=NativeSkyframeLooks.Resolve(look.kitLooks);var rig=new NativeSkyframeRig(looks,map);var fx=new NativeSkyframeFx();fx.Prepare(map);
            Check(rig.Trees==8&&rig.Socks==2,"the rig finds the trees and the windsocks");
            var crane=map.hazards.Single(h=>h.kind=="crane");double fight=10,arm=fight+NativeFactoryHazards.ArmDelay,warnAt=arm+crane.start+crane.travel+1.5,impact=arm+crane.start+crane.travel+crane.warn+crane.rise;
            var wind=map.hazards.First(h=>h.kind=="vent");double gustAt=arm+wind.start+wind.offset+wind.period-wind.dwell+.6,windWarn=gustAt-.6-wind.warn*.5;
            foreach(double t in new[]{fight+1,fight+7,arm+crane.start+2,warnAt,impact-.3,impact+.1,impact+1,impact+5,impact+7,windWarn,gustAt,gustAt+2})
            {
                FactoryPart[] Parts(){rig.Build(map.hazards,3,NativePhase.Fight,fight,t,t);return rig.Parts.Items.Take(rig.Parts.Count).ToArray();}
                Check(Parts().SequenceEqual(Parts()),"the rig is deterministic at "+t);
                int Tele(double q,bool low){fx.Build(map.hazards,3,NativePhase.Fight,fight,t,t,0,2,-6,1,0,0,0,1,0,q,low);return fx.Telegraph;}
                int full=Tele(1,false);Check(full==Tele(0,false)&&full==Tele(0,true),"telegraphs are never reduced at "+t);
                Check(fx.Quads.Count<NativeSkyframeFx.Capacity,"the effects fit their buffer at "+t);
            }
            fx.Build(map.hazards,3,NativePhase.Fight,fight,warnAt,warnAt,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>15,"the drop's telegraph draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,gustAt,gustAt,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>60,"the gust draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,windWarn,windWarn,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>30,"the wind's telegraph draws ("+fx.Telegraph+")");
            fx.Build(map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph==0,"nothing telegraphs in the arm delay");
            var beams=new NativeBeamList();fx.Beams=beams;fx.Build(map.hazards,3,NativePhase.Fight,fight,warnAt,warnAt,0,2,-6,1,0,0,0,1,0,1,false);Check(beams.Count>0,"the beams take the warning shaft when the look's beams are on");fx.Beams=null;
            // the rig's set pieces: all there at every moment, moving all match, out of the play space
            var b0=map.bounds;double[] last=null;int moved=0,frames=0;
            for(double t=fight;t<fight+90;t+=.25,frames++)
            {
                rig.Build(map.hazards,0,NativePhase.Fight,fight,t,t);var per=new int[NativeSkyframeArt.Slots.Length];var at=new double[NativeSkyframeArt.Slots.Length];
                for(int i=0;i<rig.Parts.Count;i++)
                {
                    int s=rig.Slot[i];if(s<0)continue;per[s]++;ref var p=ref rig.Parts.Items[i];at[s]+=p.X*1.3+p.Y*.7+p.Z+p.Qx*5+p.Qy*3+p.Qz*7;
                    if(s==NativeSkyframeArt.Rotor||s>=NativeSkyframeArt.Airship&&s<NativeSkyframeArt.Bird||s>=NativeSkyframeArt.Cloud){var e=Of(p);Check(e.X0>b0.maxX||e.X1<b0.minX||e.Z0>b0.maxZ||e.Z1<b0.minZ,NativeSkyframeArt.Slots[s]+" stays outside the bounds at "+t);}
                    if(s>=NativeSkyframeArt.Bird&&s<=NativeSkyframeArt.WingR)Check(p.Y>16&&p.Y<NativeSkyframeLooks.JibY-1.5&&Math.Max(Math.Abs(p.X),Math.Abs(p.Z)/.82)>NativeSkyframeLooks.FrameHalf+6,"a bird wheels round the frame, under the jib");
                    if(s>=NativeSkyframeArt.Crown&&s<NativeSkyframeArt.Crown+NativeSkyframeArt.Crowns){var cq=NativeSkyframeArt.Turn(p);cq.Rotate(0,1,0,out _,out double cup,out _);Check(cup>.99,"a crown only leans");}
                }
                Check(per[NativeSkyframeArt.CraneTop]==1&&per[NativeSkyframeArt.Trolley]==1&&per[NativeSkyframeArt.Hook]==1&&per[NativeSkyframeArt.Load]==1&&per[NativeSkyframeArt.Cable]==4,"the crane's parts");
                Check(per[NativeSkyframeArt.Crown]+per[NativeSkyframeArt.Crown+1]+per[NativeSkyframeArt.Crown+2]==8&&per[NativeSkyframeArt.Windsock]==2&&per[NativeSkyframeArt.Rotor]==NativeSkyframeLooks.Turbines,"crowns, windsocks, rotors");
                Check(per[NativeSkyframeArt.Airship]+per[NativeSkyframeArt.Airship+1]==NativeSkyframeLooks.Airships&&per[NativeSkyframeArt.Bird]==NativeSkyframeLooks.Birds&&per[NativeSkyframeArt.WingL]==NativeSkyframeLooks.Birds&&per[NativeSkyframeArt.WingR]==NativeSkyframeLooks.Birds,"airships and birds");
                int clouds=0;for(int k=0;k<NativeSkyframeLooks.CloudShapes;k++)clouds+=per[NativeSkyframeArt.Cloud+k];Check(clouds==NativeSkyframeLooks.Clouds,"the cloud islands");
                if(last!=null)foreach(int s in new[]{NativeSkyframeArt.CraneTop,NativeSkyframeArt.Crown,NativeSkyframeArt.Rotor,NativeSkyframeArt.Airship,NativeSkyframeArt.Bird,NativeSkyframeArt.Cloud})if(Math.Abs(at[s]-last[s])>1e-7)moved++;
                last=at;
            }
            Check(moved>frames*5,"the set pieces move all match long ("+moved+" of "+frames*6+" steps)");
            // the bundle is the simulation's: where its collider stands when it lands
            rig.Build(map.hazards,0,NativePhase.Fight,fight,impact+1,impact+1);
            {int li=Enumerable.Range(0,rig.Parts.Count).Single(i=>rig.Slot[i]==NativeSkyframeArt.Load);var p=rig.Parts.Items[li];var z=crane.zones[0];
                Check(Math.Abs(p.X-z.center.x)<1e-4&&Math.Abs(p.Z-z.center.z)<1e-4&&Math.Abs(p.Y-(z.center.y+crane.size.y/2))<1e-4&&Math.Abs(p.Hx-crane.size.x/2)<1e-5&&Math.Abs(p.Hz-crane.size.z/2)<1e-5,"the landed bundle lies on its zone, the collider's size");}
            // the windsock fills before the gust
            Check(NativeSkyframeRig.SockLift(0,0,double.PositiveInfinity)==0&&NativeSkyframeRig.SockLift(.8,0,double.PositiveInfinity)>.99&&NativeSkyframeRig.SockLift(0,1,double.PositiveInfinity)==1,"the windsock hangs limp, fills through the telegraph and stands in the gust");
            for(int warm=0;warm<3;warm++){rig.Build(map.hazards,3,NativePhase.Fight,fight,impact,impact);fx.Build(map.hazards,3,NativePhase.Fight,fight,impact,impact,0,2,-6,1,0,0,0,1,0,1,false);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++){double t=fight+f*.25;rig.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t);fx.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t,f*.1,2,-6,1,0,0,0,1,0,f%2,f%3==0);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"the rig and effects allocate nothing per frame ("+(after-before)+" bytes)");
            int rigMax=0,fxMax=0,fxLow=0;var sw=System.Diagnostics.Stopwatch.StartNew();int n=0;
            for(double t=fight;t<fight+60;t+=.1,n++){rig.Build(map.hazards,1,NativePhase.Fight,fight,t,t);rigMax=Math.Max(rigMax,rig.Parts.Count);fx.Build(map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,1,false);fxMax=Math.Max(fxMax,fx.Quads.Count);}
            double ms=sw.Elapsed.TotalMilliseconds/n;
            for(double t=fight;t<fight+60;t+=.5){fx.Build(map.hazards,1,NativePhase.Fight,fight,t,t,-10,2,-4,1,0,0,0,1,0,0,true);fxLow=Math.Max(fxLow,fx.Quads.Count);}
            int verts=sectors.Sum(m=>m.Vertices),tris=sectors.Sum(m=>m.Indices.Length/3);
            Check(rigMax<NativeSkyframeRig.Capacity-40&&fxMax<NativeSkyframeFx.Capacity&&fxLow<fxMax,"the frame's parts and quads fit and the low tier draws fewer ("+rigMax+", "+fxMax+", "+fxLow+")");
            Console.WriteLine("   skyframe: kit "+kit.Count+" parts ("+overhead+" overhead); rig up to "+rigMax+" parts a frame; effects up to "+fxMax+" quads (low tier "+fxLow+"); backdrop "+verts+" vertices, "+tris+" triangles in "+sectors.Length+" meshes; rig + effects "+ms.ToString("0.00")+" ms a frame (CoreTests, Release)");
            var other=new NativeSkyframeFx();other.Prepare(new ContentMap{id="polar"});other.Build(map.hazards,0,NativePhase.Fight,0,30,30,0,0,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no Skyframe effects");
            Check(NativeSkyframeScenery.Build(new ContentMap{id="polar"})==null&&NativeSkyframeKit.Build(new ContentMap{id="polar"},look)==null,"another map builds no Skyframe scenery or kit");
        }

        // The stage has left the 2026-10-31 batch's shared presentation and the harbour kit; the other M1 stages keep their numbers.
        private static void Retired(string root,UnityMapContent maps,ContentMap map)
        {
            Check(!NativeNewStageLooks.Active(map.id)&&NativeNewStageLooks.Index(map.id)<0&&!NativeNewStageLooks.Maps.Contains(map.id),"Skyframe is no longer one of the M1 batch's shared stages");
            Check(NativeNewStageScenery.Build(map)==null&&NativeNewStageKit.Build(map,null,null)==null,"the M1 scenery and kit build nothing for it");
            foreach(var (id,n) in new[]{("breakwater",0),("harbor",1),("rig",3),("polar",4)})if(NativeNewStageLooks.Maps.Contains(id))Check(NativeNewStageLooks.Index(id)==n,"M1 stage "+id+" keeps its number "+n);
            Check(NativeNewStageLooks.Maps.Length==NativeNewStageLooks.Looks.Length,"the M1 stages and their looks stay paired");
            Check(!File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PackDressing.json")).Contains("\"skyframe\""),"the harbour kit no longer dresses Skyframe");
            Check(File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py")).Contains("'skyframe':'m2'")&&File.Exists(Path.Combine(root,"../../docs/arenas/scripts/skyframe_m2.py"))&&File.Exists(Path.Combine(root,"../../docs/arenas/scripts/skyframe_m1.py")),"the generator reads the M2 draft; the M1 draft stays as history");
        }

        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("SkyframeScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmSkyframe(arena))built++;"),"SourceWorld draws it and PrewarmStage builds it");
            Check(R("Runtime/NativeAudio.cs").Contains("SkyframeCues(arena,menu);"),"the cues play");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeSkyframeLooks.cs","Core/NativeSkyframeScenery.cs","Core/NativeSkyframeKit.cs","Core/NativeSkyframeFx.cs","Core/NativeSkyframeArt.cs","Runtime/NativeSkyframePresentation.cs"})
            {
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");var g=System.Text.RegularExpressions.Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+": a guid of its own");
            }
            var all=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Where(f=>!f.Contains("NativeSkyframe")).Select(f=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f),"guid: ([0-9a-f]{32})").Groups[1].Value);
            Check(!all.Any(g=>guids.Contains(g)),"no other asset shares the new guids");
        }
    }
}
#endif
