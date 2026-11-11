#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Polaris Station, rebuilt (docs/arenas/POLAR.md; draft docs/arenas/scripts/polar_m2.py; Core NativePolar*, Runtime
    // NativePolarPresentation, MapLooks.json `polar-aurora`). Against the committed catalogue and looks: the record validates, sits
    // in the shipped catalogue and Random, mirrors about x = 0, every spawn is reachable on the nav graph and keeps 3 m from every
    // vent and 6 m from the thin ice; the hazards are the existing kinds (four steam vents with damage, four collapse segments of
    // thin ice), idle for the fight's first 4 s; the only pilot columns cap the outer walls and the dish's pedestal (no invisible
    // walls), and the shelf's south edge is open over the sea; the look resolves, styled, with the advanced lighting and its
    // snow; the box kit's parts stay on their blocks (the collider rule) or under the sea's level and lay no coincident faces;
    // the rig's set pieces keep moving, clear every standable top and follow the replicated state; the effects' telegraphs are
    // never reduced; everything is deterministic and allocation free per frame; each audio cue sounds once; the map is off the
    // shared M1 paths; the runtime is wired.
    internal static class PolarChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Polaris: "+what);count++;}
        internal struct Box3 {public double X0,X1,Y0,Y1,Z0,Z1;}
        internal static Box3 Of(ContentBlock b)=>new Box3{X0=b.pos.x-b.size.x/2,X1=b.pos.x+b.size.x/2,Y0=b.pos.y-b.size.y/2,Y1=b.pos.y+b.size.y/2,Z0=b.pos.z-b.size.z/2,Z1=b.pos.z+b.size.z/2};
        internal static Box3 Of(in FactoryPart p)
        {
            var q=new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw);var b=new Box3{X0=double.MaxValue,X1=double.MinValue,Y0=double.MaxValue,Y1=double.MinValue,Z0=double.MaxValue,Z1=double.MinValue};
            for(int c=0;c<8;c++){var v=Vector3.Transform(new Vector3((c&1)==0?-p.Hx:p.Hx,(c&2)==0?-p.Hy:p.Hy,(c&4)==0?-p.Hz:p.Hz),q);b.X0=Math.Min(b.X0,p.X+v.X);b.X1=Math.Max(b.X1,p.X+v.X);b.Y0=Math.Min(b.Y0,p.Y+v.Y);b.Y1=Math.Max(b.Y1,p.Y+v.Y);b.Z0=Math.Min(b.Z0,p.Z+v.Z);b.Z1=Math.Max(b.Z1,p.Z+v.Z);}
            return b;
        }
        private static bool Within(Box3 a,Box3 b,double g)=>a.X0>=b.X0-g&&a.X1<=b.X1+g&&a.Y0>=b.Y0-g&&a.Y1<=b.Y1+g&&a.Z0>=b.Z0-g&&a.Z1<=b.Z1+g;
        /// <summary>The blocks a pilot can stand on: every block but those capped by a pilot column that stops everyone.</summary>
        internal static List<Box3> Standable(ContentMap map)=>map.blocks.Where(b=>!map.pilotBlocks.Any(p=>!p.bots&&Math.Abs(b.pos.x-p.center.x)<=p.size.x/2+.01&&Math.Abs(b.pos.z-p.center.z)<=p.size.z/2+.01&&p.bottom<=b.pos.y+b.size.y/2)).Select(Of).ToList();
        internal static ContentMap Load(string root,out UnityMapContent maps,out ConversionContent content)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string R(string f)=>File.ReadAllText(Path.Combine(root,f));
            var text=R("Assets/Conversion/Resources/UnityMaps.json");maps=JsonSerializer.Deserialize<UnityMapContent>(text,o);
            content=JsonSerializer.Deserialize<ConversionContent>(R("Assets/Conversion/Resources/GameContent.json"),o);NativeMapRules.Merge(content,JsonSerializer.Deserialize<UnityMapContent>(text,o),text);
            return maps.maps.Single(m=>m.id==NativePolarLooks.MapId);
        }

        public static void Run(string root)
        {
            count=0;var map=Load(root,out var maps,out var content);NativeMapRules.ValidateShippedCatalog(content);NativeMapRules.Validate(map);
            Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,8).Select(i=>new NativeSeat{id=i,team=i%2}),true),"selectable and in Random for a full room");
            Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
            Check(map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"block budgets ("+map.blocks.Length+")");
            Check(map.look==NativePolarLooks.Look&&map.name=="Polaris Station"&&map.id=="polar","id, name and look");
            Check(map.nav.nodes.Length<3600&&map.nav.links.Length<6200,"a lean nav graph ("+map.nav.nodes.Length+" nodes)");
            Check(NativeStageMusic.Theme(map.id,"x")=="polar-pulse-de-la-mort","the stage keeps its current song until the owner's `polar-theme`");
            Layout(map);Nav(map);Hazards(map);Ice(map);
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,map.look);
            Check(look!=null&&look.id==map.id&&NativeMapStyle.On(look,false)&&look.advanced!=null&&look.blocks.dressed&&look.lights.Length==8&&look.farClip>=1200,"the look: styled, dressed, advanced, eight lights, a far plane past the aurora");
            Check(look.advanced.weather.kind=="snow"&&look.advanced.beams.on&&look.advanced.gi.on&&look.sky.variant=="night"&&look.sky.stars>0,"snow falls under a starred night sky; beams and the light volume on");
            Check(look.blockLooks.All(b=>b.block.StartsWith("material:",StringComparison.Ordinal)||map.blocks.Any(k=>NativeBlockLooks.Glob(b.block,k.id))),"every block look names real blocks");
            Check(look.kitLooks.All(k=>NativePolarLooks.Kinds.Contains(k.block))&&NativePolarLooks.Kinds.Length==NativePolarLooks.Defaults.Length&&NativePolarLooks.Kinds.Length<=NativeFactoryKit.Kinds.Length&&NativePolarLooks.Kinds.Distinct().Count()==NativePolarLooks.Kinds.Length,"the kit kinds");
            foreach(var (k,_) in NativePolarLooks.Defaults)Check(NativeBlockLooks.Id(k)>=0,"a default kit look is a known key: "+k);
            Presentation(root,map,look);
            OffM1(root,map,content);
            Wiring(root);
            Console.WriteLine("PASS Polaris Station: "+count+" checks");
        }

        private static void Layout(ContentMap map)
        {
            // mirror symmetric about x = 0, ids swapping w/e and a/b
            string Mirror(string id)=>string.Join("-",id.Split('-').Select(t=>t=="w"?"e":t=="e"?"w":t=="a"?"b":t=="b"?"a":t));
            var by=map.blocks.ToDictionary(b=>b.id);
            foreach(var b in map.blocks){Check(by.TryGetValue(Mirror(b.id),out var m),"a mirror partner for "+b.id);Check(Math.Abs(m.pos.x+b.pos.x)<1e-3&&Math.Abs(m.pos.z-b.pos.z)<1e-3&&Math.Abs(m.pos.y-b.pos.y)<1e-3&&(V(m.size)-V(b.size)).Length()<1e-3,"the mirror of "+b.id);}
            for(int i=0;i<8;i+=2)Check(Math.Abs(map.spawns.ffa[i].pos.x)<1e-3||Math.Abs(map.spawns.ffa[i].pos.x+map.spawns.ffa[i+1].pos.x)<1e-3&&Math.Abs(map.spawns.ffa[i].pos.z-map.spawns.ffa[i+1].pos.z)<1e-3,"FFA spawns mirror in pairs (or stand on the axis)");
            for(int i=0;i<4;i++){var a=map.spawns.teams[0].points[i].pos;var b=map.spawns.teams[1].points[i].pos;Check(Math.Abs(a.x+b.x)<1e-3&&Math.Abs(a.z-b.z)<1e-3&&a.x<-20,"team spawn "+i+" mirrors, A in the west");}
            // the levels: the shelf (0), the terrace (2.4), the foredeck (3.2)
            Check(map.blocks.Single(b=>b.id=="polar-obs-terrace").pos.y+map.blocks.Single(b=>b.id=="polar-obs-terrace").size.y/2==2.4f&&Of(map.blocks.Single(b=>b.id=="polar-ship-hull")).Y1==3.2f,"the terrace at 2.4 m and the foredeck at 3.2 m");
            // no invisible walls: the only pilot columns cap the outer walls (5 m and more) and the pedestal under the dish's yoke
            Check(map.pilotBlocks.Length==6&&map.pilotBlocks.All(p=>!p.bots&&p.bottom>=4.9f)&&map.pilotBlocks.Count(p=>p.id.StartsWith("polar-top-ridge-",StringComparison.Ordinal))==4
                &&map.pilotBlocks.Any(p=>p.id=="polar-top-ship-bridge")&&map.pilotBlocks.Any(p=>p.id=="polar-top-obs-pedestal"&&p.bottom>=9),"six pilot columns: the ridges' caps, the wheelhouse's, the pedestal's; none lower than 4.9 m");
            // the south edge is open over the sea: no block stands south of the shelf, the bounds run 4 m past it, a fall dies fast
            double edge=map.blocks.Min(b=>b.pos.z-b.size.z/2);Check(Math.Abs(edge-NativePolarLooks.ShelfZ)<1e-3&&map.bounds.minZ<=edge-4&&!map.blocks.Any(b=>b.id.StartsWith("polar-ridge-",StringComparison.Ordinal)&&b.pos.z+b.size.z/2<-24.9),"the shelf's south edge is open, 4 m inside the bounds");
            Check(map.bounds.killY==-3&&NativePolarLooks.Sea>map.bounds.killY&&NativePolarLooks.Sea<-1,"a body dies just under the sea's surface (killY -3, the sea at -1.3)");
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(p.pos.z-edge>=4.5,"spawn "+p.pos.x+","+p.pos.z+" stands back from the shelf's edge");
        }
        private static Vector3 V(V3 v)=>new Vector3(v.x,v.y,v.z);

        private static void Nav(ContentMap map)
        {
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
            string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
            foreach(var y in new[]{0f,2.4f,3.2f})Check(seen.Any(n=>Math.Abs(nodes[n].pos.y-.9f-y)<.05f),"nav reaches level "+y);
            // both north pockets, the shelf path behind the observatory and the thin ice are on the graph
            foreach(var (x,z) in new[]{(-20f,20f),(20f,20f),(4f,-23f),(1f,5f),(-1f,-5f),(0f,10f)})Check(seen.Any(n=>Math.Abs(nodes[n].pos.x-x)<1.01f&&Math.Abs(nodes[n].pos.z-z)<1.01f),"nav reaches "+x+","+z);
            // with the thin ice gone, every spawn still reaches every other (the bridge between the ponds, the road, the foredeck)
            var gone=new HashSet<string>(map.segments.SelectMany(s=>s.nav.remove));Check(gone.Count>60,"the sheets take their links with them ("+gone.Count+")");
            var adj2=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());foreach(var l in map.nav.links){if(gone.Contains(l.id))continue;adj2[l.a].Add(l.b);if(l.kind!="drop")adj2[l.b].Add(l.a);}
            var seen2=new HashSet<string>{first};q.Enqueue(first);while(q.Count>0)foreach(var n in adj2[q.Dequeue()])if(seen2.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen2.Contains(Near(p.pos)),"with the ice gone, nav still reaches spawn "+p.pos.x+","+p.pos.z);
        }

        private static void Hazards(ContentMap map)
        {
            var hz=map.hazards;Check(hz.Length==4&&hz.All(h=>h.kind=="vent"),"four steam vents");
            foreach(var h in hz)
            {
                for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
                Check(h.warn>=1.5f&&!h.lethal&&h.damage>0&&h.tick>0&&h.push==0&&string.IsNullOrEmpty(h.pushMode)&&h.dwell>=2,"a vent scalds (damage, no push) after a 1.5 s telegraph "+h.id);
                // nothing damages before FightAt + 4 s + its quiet and its warning
                double first=NativeFactoryHazards.ArmDelay+h.start+h.offset+(h.period-h.warn-h.dwell)+h.warn;Check(first>=12,"the first burst of "+h.id+" comes "+first+" s into the fight");
                for(double t=0;t<first-.01;t+=.1)Check(!NativeFactoryHazards.State(h,0,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Lethal,"no scald before the first burst "+h.id);
                Check(NativeFactoryHazards.State(h,0,NativeFactoryHazards.Clock(NativePhase.Fight,0,first+.5)).Lethal,"the burst scalds "+h.id);
                NativePolarLooks.VentAt(h,0,first-NativeFactoryHazards.ArmDelay-h.warn*.5,out double w,out double b);Check(w>.3&&w<.7&&b==0,"the warning's level "+h.id);
                NativePolarLooks.VentAt(h,0,first-NativeFactoryHazards.ArmDelay+.6,out w,out b);Check(w==0&&b>.9,"the burst's level "+h.id);
                // it stands on the shelf, clear of every block
                Check(h.a.y==0&&!map.blocks.Any(k=>k.pos.y+k.size.y/2>.05f&&Math.Abs(k.pos.x-h.a.x)<k.size.x/2+h.size.x/2&&Math.Abs(k.pos.z-h.a.z)<k.size.z/2+h.size.z/2),"the vent stands clear on the shelf "+h.id);
            }
            Check(hz.Select(h=>h.start).Distinct().Count()==4,"the four vents burst in turn");
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))foreach(var h in hz)
            {double dx=Math.Max(0,Math.Abs(p.pos.x-h.a.x)-h.size.x/2),dz=Math.Max(0,Math.Abs(p.pos.z-h.a.z)-h.size.z/2);Check(Math.Sqrt(dx*dx+dz*dz)>=3,"spawn "+p.pos.x+","+p.pos.z+" clear of "+h.id);}
            var into=new FactoryCue[32];var scratch=new FactoryCue[64];var all=new List<(int,int,int)>();var whole=new List<(int,int,int)>();
            for(double t=0;t<90;t+=1/60.0){int n=NativePolarLooks.Cues(hz,0,0,t,t+1/60.0,into,scratch);for(int i=0;i<n;i++)all.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            for(double t=0;t<90;t+=.5){int n=NativePolarLooks.Cues(hz,0,0,t,t+.5,into,scratch);for(int i=0;i<n;i++)whole.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            Check(all.Count==all.Distinct().Count()&&all.OrderBy(x=>x).SequenceEqual(whole.OrderBy(x=>x))&&all.Count(c=>c.Item1==NativePolarLooks.CueVentBurst)>=16&&all.Count(c=>c.Item1==NativePolarLooks.CueVentHiss)==all.Count(c=>c.Item1==NativePolarLooks.CueVentBurst),"each vent cue sounds once ("+all.Count+")");
        }

        // The thin ice: four pancake segments over the channel; a blast on a seam cracks its sheet, which sinks under the sea.
        private static void Ice(ContentMap map)
        {
            NativeStructureRules.Validate(map);var segs=map.segments;Check(segs.Length==4&&segs.All(s=>s.mode=="pancake"&&s.telegraph>=1.2f&&s.rubble.Length==0&&s.maxAccel<=6.9001f),"four sheets of thin ice: pancake segments, a 1.2 s crack at least, nothing left behind");
            foreach(var s in segs)
            {
                var plates=s.blocks.Select(id=>map.blocks.Single(b=>b.id==id)).ToList();var seam=map.blocks.Single(b=>b.id==s.supports.Single().member);
                Check(plates.Count==2&&plates.All(p=>p.role=="carried"&&p.segment==s.id&&Math.Abs(p.pos.y+p.size.y/2)<1e-4)&&seam.role=="structural"&&seam.hp>0&&seam.hp<=60&&Math.Abs(seam.pos.y+seam.size.y/2)<1e-4,s.id+": two carried plates and a structural seam, flush with the shelf");
                Check(s.poses.All(p=>p.angle==0&&p.drop>=3)&&plates.All(p=>p.pos.y+p.size.y/2-s.poses[0].drop<NativePolarLooks.Sea-.5),s.id+": the plates ride level down under the sea");
                // nothing but the sea under it: no block's top stands under the sheet above killY
                var area=new Box3{X0=plates.Min(p=>Of(p).X0),X1=plates.Max(p=>Of(p).X1),Z0=plates.Min(p=>Of(p).Z0),Z1=plates.Max(p=>Of(p).Z1)};
                Check(!map.blocks.Any(b=>!s.blocks.Contains(b.id)&&b.id!=seam.id&&Of(b).X1>area.X0+.01&&Of(b).X0<area.X1-.01&&Of(b).Z1>area.Z0+.01&&Of(b).Z0<area.Z1-.01),s.id+": open water under it");
                Check(Math.Abs(area.X0+NativePolarLooks.ChannelHalf)<1e-3&&Math.Abs(area.X1-NativePolarLooks.ChannelHalf)<1e-3&&area.Z0>=NativePolarLooks.ChannelS-1e-3&&area.Z1<=NativePolarLooks.ChannelN+1e-3,s.id+": in the channel");
                Check(s.nav.remove.Length>=20&&s.nav.remove.All(id=>map.nav.links.Any(l=>l.id==id)),s.id+": its nav links go with it");
                foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(!NativeStructureRules.Swept(s,p.pos,6f)||p.pos.y>2,"spawn "+p.pos.x+","+p.pos.z+" stands 6 m from "+s.id);
                // the presentation's reading of the replicated state
                NativePolarLooks.SheetAt(s,0,0,10,out double c,out double k,out bool open,out _);Check(c==0&&k==0&&!open,"whole");
                NativePolarLooks.SheetAt(s,1,10+s.telegraph,10+s.telegraph*.5,out c,out k,out open,out _);Check(c>.4&&c<.6&&k==0&&!open,"cracking");
                NativePolarLooks.SheetAt(s,2,10,10+NativeStructureRules.Duration(s)*.5,out c,out k,out open,out _);Check(c==1&&k>.4&&k<.6&&open,"sinking");
                NativePolarLooks.SheetAt(s,3,10,40,out c,out k,out open,out double since);Check(c==1&&k==1&&open&&since==30&&Math.Abs(NativePolarLooks.Fall(s)-NativeStructureRules.Duration(s))<1e-6,"gone");
            }
            // the state machine: a broken seam cracks its sheet after the arm delay, it sinks, it is gone
            var state=new NativeStructureState(map);var dead=segs[0].supports[0].member;
            Check(state.Step(3,0,id=>id!=dead).Count==0,"nothing cracks in the arm delay");var ch=state.Step(5,0,id=>id!=dead);Check(ch.Count==1&&ch[0].state==1&&ch[0].id==segs[0].id,"the seam's sheet cracks");
            float t0=state.Get(segs[0].id).t0;Check(Math.Abs(t0-5-segs[0].telegraph)<1e-4,"it goes after its telegraph");
            Check(state.Step(t0+.01f,0,id=>id!=dead).Any(c=>c.state==2)&&state.Step(t0+NativeStructureRules.Duration(segs[0])+.01f,0,id=>id!=dead).Any(c=>c.state==3)&&segs.Skip(1).All(s=>state.Get(s.id).state==0),"it sinks and is gone; its neighbours hold");
        }

        private static void Presentation(string root,ContentMap map,MapLook look)
        {
            var sectors=NativePolarScenery.Build(map);Check(sectors!=null&&sectors.Length==NativePolarScenery.Sectors&&sectors.All(s=>s.Vertices>0),"the box backdrop builds (its sink throws on any box in the play space)");
            // the box kit: attached parts on their blocks, the sea under the shelf, nothing overhead; deterministic
            var kit=NativePolarKit.Build(map,look);var again=NativePolarKit.Build(map,look);
            Check(kit!=null&&kit.Count>500&&kit.Count==kit.Parent.Length&&kit.Count==kit.Family.Count&&kit.Count==kit.Role.Count&&kit.Count==kit.Kind.Count&&kit.Count==kit.Owner.Count,"the kit builds ("+kit?.Count+" parts)");
            Check(again.Count==kit.Count&&Enumerable.Range(0,kit.Count).All(i=>kit.Parts.Items[i].Equals(again.Parts.Items[i])),"the kit is the same every build");
            var outside=new List<string>();int below=0;
            for(int i=0;i<kit.Count;i++)
            {
                var p=Of(kit.Parts.Items[i]);
                if(kit.Role[i]==NativePolarKit.Below){below++;Check(kit.Parent[i]<0&&p.Y1<=NativePolarKit.BelowY&&p.Y1<=NativePolarLooks.Sea+1e-6,"the sea lies under the shelf");continue;}
                Check(kit.Role[i]==NativePolarKit.Attached&&kit.Parent[i]>=0&&kit.Owner[i]==map.blocks[kit.Parent[i]].id,"every other static part belongs to a block");var b=Of(map.blocks[kit.Parent[i]]);
                bool thin=Math.Min(p.X1-p.X0,p.Z1-p.Z0)<=NativePolarKit.Thin+1e-3&&Math.Max(p.X1-p.X0,p.Z1-p.Z0)<=NativePolarKit.Thin+1e-3;
                if(!Within(p,b,(thin?NativePolarKit.ThinReach:NativePolarKit.Proud)+1e-3)&&outside.Count<12)outside.Add(map.blocks[kit.Parent[i]].id+" #"+i+" "+kit.Family[i]+" ["+p.X0.ToString("0.00")+".."+p.X1.ToString("0.00")+", "+p.Y0.ToString("0.00")+".."+p.Y1.ToString("0.00")+", "+p.Z0.ToString("0.00")+".."+p.Z1.ToString("0.00")+"]");
            }
            Check(outside.Count==0,"every attached part stays within its block grown by "+NativePolarKit.Proud+" (a stovepipe within "+NativePolarKit.ThinReach+"): "+string.Join("; ",outside));
            Check(below==2&&kit.Family.Contains("vent")&&kit.Family.Contains("cabin-wall")&&kit.Family.Contains("hull")&&kit.Family.Contains("wheelhouse")&&kit.Family.Contains("spire")&&kit.Family.Contains("pedestal"),"the kit's families");
            // no z-fighting: the dressed blocks and the kit's parts lay no two same-facing faces in one plane
            var boxes=SurfaceChecks.Boxes(root,map,true);
            for(int i=0;i<kit.Count;i++)
            {
                var p=kit.Parts.Items[i];var q=Quaternion.Normalize(new Quaternion(p.Qx,p.Qy,p.Qz,p.Qw));var c=new Vector3(p.X,p.Y,p.Z);var h=new Vector3(p.Hx,p.Hy,p.Hz);var lo=new Vector3(float.MaxValue);var hi=new Vector3(float.MinValue);
                for(int k=0;k<8;k++){var v=c+Vector3.Transform(new Vector3((k&1)==0?-h.X:h.X,(k&2)==0?-h.Y:h.Y,(k&4)==0?-h.Z:h.Z),q);lo=Vector3.Min(lo,v);hi=Vector3.Max(hi,v);}
                boxes.Add(new SurfaceChecks.Box{Id="polar-kit#"+i+" "+kit.Family[i],Kit=true,C=c,H=h,Q=q,Look=p.Look,Color=p.Color,Bevel=NativeFactoryKit.Bevel(p.Hx,p.Hy,p.Hz),Min=lo,Max=hi});
            }
            var clashes=SurfaceChecks.Clashes(boxes,null,NativeSurfaceLayers.Step*.9f);
            Check(clashes.Count==0,"the blocks and the kit lay no two same-facing faces in one plane"+(clashes.Count>0?" ("+clashes.Count+"; first: "+boxes[clashes[0].A].Id+" / "+boxes[clashes[0].B].Id+" at "+clashes[0].At+")":""));

            // the set pieces move all match long
            Check(NativePolarLooks.DishLowest()>=5.0+NativePolarKit.Clearance,"the dish's rim never comes within 2 m of the windbreak's top ("+NativePolarLooks.DishLowest().ToString("0.00")+" m)");
            double elMin=9,elMax=-9;for(double t=0;t<600;t+=.5){double el=NativePolarLooks.DishElevation(t);elMin=Math.Min(elMin,el);elMax=Math.Max(elMax,el);Check(NativePolarLooks.DishAzimuth(t+.5)>NativePolarLooks.DishAzimuth(t),"the dish keeps turning");}
            Check(elMin>.55&&elMin<.6&&elMax>1.2&&elMax<1.27,"the dish tilts between 32 and 72 degrees");
            int flights=0;for(double t=0;t<460;t+=.25)
            {
                if(!NativePolarLooks.BalloonAt(t,out double x,out double y,out double z,out double scale,out _))continue;flights++;
                bool inside=x>map.bounds.minX-1&&x<map.bounds.maxX+1&&z>map.bounds.minZ-1&&z<map.bounds.maxZ+1;Check(!inside||y-NativePolarLooks.BalloonRadius*scale-NativePolarLooks.SondeDrop-.6>=8.5,"the balloon and its sonde are 8.5 m up before they cross into the map ("+x.ToString("0.0")+", "+y.ToString("0.0")+")");
            }
            Check(flights>1200,"a balloon is in the sky most of the time");
            for(double t=0;t<200;t+=1.3){NativePolarLooks.Searchlight(t,out double x,out double y,out double z);Check(z<NativePolarLooks.LampZ-10&&Math.Abs(x)<40&&y<1,"the searchlight plays over the field");}
            NativePolarLooks.AuroraRay(1,.4,10,out double ax,out double ay,out double az,out double ah,out _,out _);NativePolarLooks.AuroraRay(1,.4,14,out double bx,out double by,out double bz,out double bh,out _,out _);
            Check(Math.Sqrt(ax*ax+az*az)>150&&ay>60&&ah>40&&(Math.Abs(ax-bx)>.5||Math.Abs(ah-bh)>.5),"the aurora stands far out and high, and moves");

            // the rig: deterministic, every part in its role (overhead parts clear every standable top, lamps lie on the shelf, the
            // cups sit on their masts, the floes lie in the sea), following the replicated state
            var looks=NativePolarLooks.Resolve(look.kitLooks);var rig=new NativePolarRig(looks,map);var fx=new NativePolarFx();fx.Prepare(map);var tops=Standable(map);var all=map.blocks.Select(Of).ToList();
            int[] st={0,0,0,0,0,0,0,0};float[] t0={0,0,0,0,0,0,0,0};double fight=10;var vent=map.hazards[0];double burst=fight+NativeFactoryHazards.ArmDelay+vent.start+vent.offset+(vent.period-vent.warn-vent.dwell)+vent.warn;
            var pose=new NativeStageArtPose();int floes=0,overhead=0,lampsLit=0;
            foreach(double t in new[]{fight+1,fight+7,burst-vent.warn*.5,burst+.4,burst+3,fight+33.3,fight+47,fight+61.7,fight+90,fight+133,fight+171,fight+215})
            {
                // one sheet cracking then gone part way through the list
                st[1]=t>fight+40?3:t>fight+30?1:0;t0[1]=(float)(t>fight+40?fight+34:fight+34);
                FactoryPart[] Parts(){rig.Build(map,st,t0,3,NativePhase.Fight,fight,t);return rig.Parts.Items.Take(rig.Parts.Count).ToArray();}
                var parts=Parts();Check(parts.SequenceEqual(Parts()),"the rig is deterministic at "+t);Check(parts.Length<NativePolarRig.Capacity,"the rig fits its buffer");
                for(int i=0;i<parts.Length;i++)
                {
                    var a=Of(parts[i]);int role=rig.Role[i];
                    if(role==NativePolarRig.Overhead)
                    {
                        overhead++;bool outsideMap=a.X1<map.bounds.minX||a.X0>map.bounds.maxX||a.Z1<map.bounds.minZ||a.Z0>map.bounds.maxZ;if(outsideMap)continue;
                        double under=tops.Where(b=>a.X1>b.X0&&a.X0<b.X1&&a.Z1>b.Z0&&a.Z0<b.Z1).Select(b=>b.Y1).DefaultIfEmpty(-99).Max();
                        Check(a.Y0-under>=NativePolarKit.Clearance-1e-6,"an overhead part at "+a.X0.ToString("0.0")+".."+a.X1.ToString("0.0")+", "+a.Y0.ToString("0.00")+", "+a.Z0.ToString("0.0")+".."+a.Z1.ToString("0.0")+" clears the top under it ("+under.ToString("0.00")+") at "+t);
                    }
                    else if(role==NativePolarRig.Floor)Check(a.Y1-a.Y0<.1&&all.Any(b=>Math.Abs(b.Y1-a.Y0)<.05&&a.X0>=b.X0-.01&&a.X1<=b.X1+.01&&a.Z0>=b.Z0-.01&&a.Z1<=b.Z1+.01),"a vent lamp lies on the shelf");
                    else if(role==NativePolarRig.Attached)Check(all.Any(b=>Within(a,b,NativePolarKit.Thin+.06)),"an anemometer sits on its mast");
                    else{floes++;Check(a.Y1<=NativePolarKit.BelowY&&Math.Abs((a.X0+a.X1)/2)<NativePolarLooks.ChannelHalf,"a floe lies in the channel's water");}
                }
                NativePolarArt.Pose(rig,pose);Check(pose.Count==Enumerable.Range(0,parts.Length).Count(i=>rig.Slot[i]>=0)&&pose.Count<NativeStageArtPose.Capacity,"every tagged part is posed");
                lampsLit+=Enumerable.Range(0,parts.Length).Count(i=>rig.Slot[i]==NativePolarArt.VentLamp&&rig.Level[i]>.02);
                int Tele(double q,bool low){fx.Build(map,st,t0,3,NativePhase.Fight,fight,t,0,2,-6,1,0,0,0,1,0,q,low);return fx.Telegraph;}
                int full=Tele(1,false);Check(full==Tele(0,false)&&full==Tele(0,true),"telegraphs are never reduced at "+t);
                Check(fx.Quads.Count<NativePolarFx.Capacity&&fx.Aurora>100,"the effects fit their buffer and the aurora draws at "+t);
            }
            Check(floes==NativePolarRig.Floes*6&&overhead>500&&lampsLit>=8,"floes bob where the sheet went ("+floes+"), the dish draws, the vent's lamps light");
            for(int i=0;i<8;i++){st[i]=0;t0[i]=0;}
            fx.Build(map,st,t0,3,NativePhase.Fight,fight,burst-vent.warn*.5,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>20,"a vent's warning draws ("+fx.Telegraph+")");
            fx.Build(map,st,t0,3,NativePhase.Fight,fight,burst+.5,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>40,"a vent's burst draws ("+fx.Telegraph+")");
            fx.Build(map,st,t0,3,NativePhase.Fight,fight,fight+2,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph==0,"nothing telegraphs in the arm delay");
            st[2]=1;t0[2]=(float)(fight+50);fx.Build(map,st,t0,3,NativePhase.Fight,fight,fight+49.4,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>40,"a cracking sheet's telegraph draws ("+fx.Telegraph+")");st[2]=0;
            var beams=new NativeBeamList();fx.Beams=beams;fx.Build(map,st,t0,3,NativePhase.Fight,fight,fight+20,0,2,-6,1,0,0,0,1,0,1,false);Check(beams.Count==1,"the beams take the searchlight's shaft when the look's beams are on");fx.Beams=null;
            for(int warm=0;warm<3;warm++){rig.Build(map,st,t0,3,NativePhase.Fight,fight,burst);fx.Build(map,st,t0,3,NativePhase.Fight,fight,burst,0,2,-6,1,0,0,0,1,0,1,false);NativePolarArt.Pose(rig,pose);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++){double t=fight+f*.25;st[0]=f>120?3:f>100?1:0;t0[0]=(float)(fight+28);rig.Build(map,st,t0,f&3,NativePhase.Fight,fight,t);NativePolarArt.Pose(rig,pose);fx.Build(map,st,t0,f&3,NativePhase.Fight,fight,t,f*.1,2,-6,1,0,0,0,1,0,f%2,f%3==0);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"the rig, the poses and the effects allocate nothing per frame ("+(after-before)+" bytes)");
            int rigMax=0,fxMax=0,fxLow=0;var sw=System.Diagnostics.Stopwatch.StartNew();int frames=0;st[0]=0;
            for(double t=fight;t<fight+60;t+=.1,frames++){rig.Build(map,st,t0,1,NativePhase.Fight,fight,t);rigMax=Math.Max(rigMax,rig.Parts.Count);fx.Build(map,st,t0,1,NativePhase.Fight,fight,t,-10,2,-4,1,0,0,0,1,0,1,false);fxMax=Math.Max(fxMax,fx.Quads.Count);}
            double ms=sw.Elapsed.TotalMilliseconds/frames;
            for(double t=fight;t<fight+60;t+=.5){fx.Build(map,st,t0,1,NativePhase.Fight,fight,t,-10,2,-4,1,0,0,0,1,0,0,true);fxLow=Math.Max(fxLow,fx.Quads.Count);}
            Check(rigMax<NativePolarRig.Capacity&&fxMax<NativePolarFx.Capacity&&fxLow<fxMax,"the frame's parts and quads fit and the low tier draws fewer");
            Console.WriteLine("   polar: kit "+kit.Count+" parts; rig up to "+rigMax+" parts a frame; effects up to "+fxMax+" quads (low tier "+fxLow+"); box backdrop "+sectors.Sum(m=>m.Vertices)+" vertices, "+sectors.Sum(m=>m.Indices.Length/3)+" triangles in "+sectors.Length+" meshes; rig + effects "+ms.ToString("0.00")+" ms a frame (CoreTests, Release)");
            var other=new NativePolarFx();other.Prepare(new ContentMap{id="causeway"});other.Build(new ContentMap{id="causeway"},null,null,0,NativePhase.Fight,0,30,0,0,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no Polaris effects");
            Check(NativePolarScenery.Build(new ContentMap{id="causeway"})==null&&NativePolarKit.Build(new ContentMap{id="causeway"},look)==null,"another map builds no Polaris scenery or kit");
        }

        // The stage is off the shared M1 code paths (Core NativeNewStage*), which the other M1 stages keep.
        private static void OffM1(string root,ContentMap map,ConversionContent content)
        {
            Check(!NativeNewStageLooks.Active(map.id)&&NativeNewStageLooks.Index(map.id)<0&&!NativeNewStageLooks.Looks.Contains(map.look),"Polaris is not one of the M1 stages' maps or looks any more");
            Check(NativeNewStageScenery.Build(map)==null,"the M1 backdrop builds nothing for it");
            var fx=new NativeNewStageFx();fx.Prepare(map);fx.Build(map.hazards,0,NativePhase.Fight,0,30,30,0,6,0,1,0,0,0,1,0,1,false);Check(fx.Quads.Count==0,"the M1 effects draw nothing for it");
            var pack=JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PackDressing.json")));
            Check(!pack.GetProperty("maps").EnumerateArray().Any(m=>m.GetProperty("map").GetString()==map.id),"the Toon Harbor pack dresses nothing on it");
            Check(!map.blocks.Any(b=>b.id.Contains("-module-")||b.id.Contains("-garage-")||b.id.Contains("-drill-")||b.id.Contains("-radome")),"none of the M1 stage's blocks remain");
        }

        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("            PolarScenery(arena);\n")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmPolar(arena))built++;"),"SourceWorld draws it and PrewarmStage builds it");
            Check(R("Runtime/NativeAudio.cs").Contains("PolarCues(arena,menu);"),"the cues play");
            string draw=R("Runtime/NativePolarPresentation.cs");
            Check(draw.Contains("if(!NativePolarLooks.Active(arena.MapId))return;")&&draw.Contains("var f=arena.Structures.Get(map.segments[i].id);segState[i]=f.state;segT0[i]=f.t0;")&&draw.Contains("rigBuilder.Build(map,segState,segT0,round,match.Phase,match.FightAt,now);"),"the draw is gated on the map and follows the replicated state");
            Check(draw.Contains("art.Prepare(map,NativePolarArt.Slots,NativePolarArt.Casts);")&&draw.Contains("NativePolarArt.Pose(rigBuilder,artPose);ArtDrawn=art.Render(arena,camera,low,artPose);")&&draw.Contains("if(!ArtDrawn){foreach(var b in kit)Draw(b,instancing);")
                &&draw.Contains("if(!ArtDrawn)for(int i=0;i<sectors.Length;i++)"),"the stage art draws in place of the box kit, rig and backdrop; without the bank the boxes draw");
            Check(NativeStageArt.BankOf(NativePolarLooks.MapId)==NativePolarArt.Bank&&NativePolarArt.Bank=="ChipArenaStagePolar","the stage's own bank");
            string gen=File.ReadAllText(Path.Combine(root,"tools/generate_stage_hazards.py"));
            Check(gen.Contains("'polar':'polar_m2'")&&gen.Contains("'polar':('polar-top-ridge','polar-top-ship-bridge')")&&gen.Contains("'polar':('polar-top-obs-pedestal',)")&&gen.Contains("if name=='polar':segment_nav(m)"),"the generator reads the new draft, keeps only the edge caps and the pedestal's, and fills the sheets' nav");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativePolarLooks.cs","Core/NativePolarScenery.cs","Core/NativePolarKit.cs","Core/NativePolarFx.cs","Core/NativePolarArt.cs","Runtime/NativePolarPresentation.cs"})
            {
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");var g=System.Text.RegularExpressions.Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+": a guid of its own");
            }
            var all=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Where(f=>!f.Contains("NativePolar")).Select(f=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f),"guid: ([0-9a-f]{32})").Groups[1].Value);
            Check(!all.Any(g=>guids.Contains(g)),"no other asset shares the new guids");
        }
    }
}
#endif
