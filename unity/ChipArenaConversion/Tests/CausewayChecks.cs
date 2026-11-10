#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // Starfall Causeway (docs/arenas/CAUSEWAY.md; draft docs/arenas/scripts/causeway_m1.py; Core NativeCauseway*, Runtime
    // NativeCausewayPresentation, MapLooks.json `causeway-starfall`). Against the committed catalogue and looks: the record validates,
    // sits in the shipped catalogue and Random, every spawn is reachable on the nav graph and keeps 3 m from every hazard on its
    // floor; the hazards are the existing kinds, idle for the fight's first 4 s, telegraphed at least as long as today's, and the
    // solar wind's push is the only rules change (no other vent pushes); the look resolves, styled, with the advanced lighting; the
    // backdrop stays out of the play space; the kit's parts stay inside their blocks (the collider rule), the sculpted pieces fill
    // theirs, and the Shattered Arch clears every standable top; the rig and effects follow the replicated state, telegraphs are
    // never reduced, everything is deterministic and allocation free per frame; each audio cue sounds once; the runtime is wired.
    internal static class CausewayChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Causeway: "+what);count++;}
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
            var map=maps.maps.Single(m=>m.id==NativeCausewayLooks.MapId);NativeMapRules.Validate(map);
            Check(map.rotation&&NativeMapRules.ArenaSelectable(map)&&NativeMapRules.RandomEligible(content.Map(map.id),Enumerable.Range(1,8).Select(i=>new NativeSeat{id=i,team=i%2}),true),"selectable and in Random for a full room");
            Check(map.spawns.ffa.Length==8&&map.spawns.teams.Length==2&&map.spawns.teams.All(t=>t.points.Length==4),"8 FFA spawns and two team tables of four");
            Check(map.blocks.Length<=350&&map.blocks.Count(b=>b.role!="static")<=200,"block budgets ("+map.blocks.Length+")");
            Check(map.look==NativeCausewayLooks.Look&&map.name=="Starfall Causeway","name and look");
            Check(map.nav.nodes.Length<2000&&map.nav.links.Length<3500,"a lean nav graph ("+map.nav.nodes.Length+" nodes)");
            Nav(map);Hazards(map,maps);
            var set=MapLookChecks.Load(root);NativeMapLooks.Validate(set);var look=NativeMapLooks.ByLook(set,map.look);
            Check(look!=null&&look.id==map.id&&NativeMapStyle.On(look,false)&&look.advanced!=null&&look.blocks.dressed&&look.lights.Length<=8&&look.farClip>=2000,"the look: styled, dressed, advanced, eight lights, a far plane past the planet");
            Check(look.blockLooks.All(b=>b.block.StartsWith("material:",StringComparison.Ordinal)||map.blocks.Any(k=>NativeBlockLooks.Glob(b.block,k.id))),"every block look names real blocks");
            Presentation(map,look);
            Wiring(root);
            Console.WriteLine("PASS Starfall Causeway: "+count+" checks");
        }

        private static void Nav(ContentMap map)
        {
            var nodes=map.nav.nodes.ToDictionary(n=>n.id);var adj=nodes.Keys.ToDictionary(k=>k,k=>new List<string>());
            foreach(var l in map.nav.links){adj[l.a].Add(l.b);if(l.kind!="drop")adj[l.b].Add(l.a);}
            string Near(V3 p){var n=nodes.Values.Where(n=>Math.Abs(n.pos.y-p.y)<=.6f&&(n.pos-p).Length<=2).OrderBy(n=>(n.pos-p).Length).FirstOrDefault();Check(n!=null,"nav attachment "+p.x+","+p.y+","+p.z);return n.id;}
            var first=Near(map.spawns.ffa[0].pos);var seen=new HashSet<string>{first};var q=new Queue<string>();q.Enqueue(first);
            while(q.Count>0)foreach(var n in adj[q.Dequeue()])if(seen.Add(n))q.Enqueue(n);
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))Check(seen.Contains(Near(p.pos)),"nav reaches spawn "+p.pos.x+","+p.pos.z);
            // every level of the map is on the graph: walkways, span, crowns
            foreach(var y in new[]{-3.2f,0f,4.4f})Check(seen.Any(n=>Math.Abs(nodes[n].pos.y-.9f-y)<.05f),"nav reaches level "+y);
        }

        private static void Hazards(ContentMap map,UnityMapContent maps)
        {
            var hz=map.hazards;Check(hz.Count(h=>h.kind=="press")==6&&hz.Count(h=>h.kind=="maglev")==1&&hz.Count(h=>h.kind=="vent")==2&&hz.Length==9,"six falling stars, the comet and two winds");
            foreach(var h in hz)
            {
                for(int round=0;round<2;round++)for(double t=0;t<4;t+=.05)
                {
                    if(h.kind=="maglev")Check(NativeCyberCityHazards.At(h,NativePhase.Fight,round,0,t).Phase==NativeHazardPhase.Idle,"the comet idles through the arm delay");
                    else Check(NativeFactoryHazards.State(h,round,NativeFactoryHazards.Clock(NativePhase.Fight,0,t)).Phase==NativeFactoryHazards.Parked,"arm delay "+h.id);
                }
                if(h.kind=="press")Check(h.warn>=3&&h.lethal&&Math.Abs(h.b.y-h.size.y/2)<.01f,"a star's 3 s telegraph and its y-0 floor (the press kind's lethal band is absolute) "+h.id);
                if(h.kind=="vent")Check(h.warn>=.8f&&!h.lethal&&h.damage>0&&h.push>0&&Math.Abs(h.axis.Length-1)<1e-4,"the wind's telegraph, damage and push "+h.id);
                if(h.kind=="maglev")Check(h.warn>=6&&h.danger>=3&&h.first-h.warn>=4,"the comet's warning");
            }
            // the push is the only rules change: no vent anywhere else pushes
            Check(maps.maps.Where(m=>m.id!=map.id).SelectMany(m=>m.hazards??Array.Empty<MapHazard>()).All(h=>h.kind!="vent"||h.push==0),"no other map's vent pushes");
            // a star's lethal window: from the last instant of its fall through its blast, never during its telegraph
            foreach(var h in hz.Where(h=>h.kind=="press"))
            {
                double open=h.period-h.warn-h.rise-h.dwell-h.travel,impact=h.start+h.offset+open+h.warn+h.rise;
                Check(!NativeFactoryHazards.State(h,0,impact-h.rise-.1).Lethal&&NativeFactoryHazards.State(h,0,impact+.05).Lethal&&!NativeFactoryHazards.State(h,0,impact+h.dwell+h.travel+.05).Lethal,"the star's lethal window "+h.id);
                var s=NativeCausewayLooks.StarAt(h,0,impact-h.rise*.5);Check(s.Fall>.4&&s.Fall<.6&&s.Py>s.Y+1,"the star's drawn fall "+h.id);
                Check(Math.Abs(NativeCausewayLooks.StarAt(h,0,impact+.2).Since-.2)<1e-6,"the star's impact time "+h.id);
            }
            // spawns: 3 m (plan) from every live volume spanning their body on their floor
            foreach(var p in map.spawns.ffa.Concat(map.spawns.teams.SelectMany(t=>t.points)))
            {
                double fy=p.pos.y-1.1;
                foreach(var h in hz)
                {
                    double x0,x1,z0,z1,y0,y1;
                    if(h.kind=="press"){x0=h.b.x-h.size.x/2;x1=h.b.x+h.size.x/2;z0=h.b.z-h.size.z/2;z1=h.b.z+h.size.z/2;y0=h.b.y-h.size.y/2;y1=h.a.y+h.size.y/2;}
                    else if(h.kind=="vent"){x0=h.a.x-h.size.x/2;x1=h.a.x+h.size.x/2;z0=h.a.z-h.size.z/2;z1=h.a.z+h.size.z/2;y0=h.a.y;y1=h.a.y+h.size.y;}
                    else{x0=h.center.x-h.size.x/2;x1=h.center.x+h.size.x/2;z0=h.center.z-h.size.z/2;z1=h.center.z+h.size.z/2;y0=h.center.y-h.size.y/2;y1=h.center.y+h.size.y/2;}
                    if(y0-.1>fy+1.8||y1<fy)continue;double dx=Math.Max(0,Math.Max(x0-p.pos.x,p.pos.x-x1)),dz=Math.Max(0,Math.Max(z0-p.pos.z,p.pos.z-z1));
                    Check(Math.Sqrt(dx*dx+dz*dz)>=3,"spawn "+p.pos.x+","+p.pos.z+" clear of "+h.id);
                }
            }
            // the cues: each once however the frames cut the time, in time order of their hazards
            var into=new FactoryCue[64];var scratch=new FactoryCue[128];var all=new List<(int,int,int)>();var whole=new List<(int,int,int)>();
            for(double t=0;t<90;t+=1/60.0){int n=NativeCausewayLooks.Cues(hz,0,0,t,t+1/60.0,into,scratch);for(int i=0;i<n;i++)all.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            for(double t=0;t<90;t+=.5){int n=NativeCausewayLooks.Cues(hz,0,0,t,t+.5,into,scratch);for(int i=0;i<n;i++)whole.Add((into[i].Kind,into[i].Hazard,into[i].Cycle));}
            Check(all.Count==all.Distinct().Count()&&all.OrderBy(x=>x).SequenceEqual(whole.OrderBy(x=>x)),"each cue sounds once ("+all.Count+")");
            Check(all.Count(c=>c.Item1==NativeCausewayLooks.CueStarImpact)==all.Count(c=>c.Item1==NativeCausewayLooks.CueStarFall)&&all.Count(c=>c.Item1==NativeCausewayLooks.CueCometPass)>=2&&all.Count(c=>c.Item1==NativeCausewayLooks.CueWindGust)>=6,"every kind of cue sounds");
        }

        private static void Presentation(ContentMap map,MapLook look)
        {
            // the backdrop: builds (its sink throws on any box in the play space), the planet and its rings out past the void
            var sectors=NativeCausewayScenery.Build(map);Check(sectors!=null&&sectors.Length==NativeCausewayScenery.Sectors+2&&sectors.All(s=>s.Vertices>0),"the backdrop and the planet build");
            var planet=sectors[NativeCausewayScenery.Sectors];Check(planet.MinX>map.bounds.maxX+100&&planet.MaxZ<map.bounds.minZ-100,"the planet lies far out in the south-east");
            var rings=sectors[NativeCausewayScenery.Sectors+1];Check(Math.Abs(rings.MaxX-rings.MinX)>2*NativeCausewayLooks.RingIn,"the rings span past the planet");
            // the kit: attached parts inside their blocks grown by Proud; the sculpted blocks drawn by the kit fill theirs
            var kit=NativeCausewayKit.Build(map,look);Check(kit!=null&&kit.Count>1500&&kit.Count==kit.Parent.Length,"the kit builds ("+kit?.Count+" parts)");
            var outside=new List<string>();
            for(int i=0;i<kit.Count;i++)
            {
                int parent=kit.Parent[i];Check(parent>=0,"every static part belongs to a block");var b=map.blocks[parent];var p=Of(kit.Parts.Items[i]);
                if(!Within(p,Of(b),NativeCausewayKit.Proud+1e-3)&&outside.Count<12)outside.Add(b.id+" #"+i+" ["+p.X0.ToString("0.00")+".."+p.X1.ToString("0.00")+", "+p.Y0.ToString("0.00")+".."+p.Y1.ToString("0.00")+", "+p.Z0.ToString("0.00")+".."+p.Z1.ToString("0.00")+"]");
            }
            Check(outside.Count==0,"every attached part stays within its block grown by "+NativeCausewayKit.Proud+": "+string.Join("; ",outside));
            foreach(var b in map.blocks.Where(b=>NativeCausewayLooks.KitBlock(b.id)))
            {
                var parts=Enumerable.Range(0,kit.Count).Where(i=>kit.Parent[i]==Array.IndexOf(map.blocks,b)).Select(i=>Of(kit.Parts.Items[i])).ToList();var x=Of(b);
                Check(parts.Count>=8,b.id+" is sculpted");double top=parts.Max(p=>p.Y1),lo=parts.Min(p=>p.Y0);
                Check(top>=x.Y1-NativeCausewayKit.Proud&&lo<=x.Y0+.05,b.id+": the sculpt fills its block's height ("+lo.ToString("0.00")+".."+top.ToString("0.00")+")");
            }
            Check(map.blocks.Count(b=>NativeCausewayLooks.KitBlock(b.id))==18,"eighteen sculpted blocks (four wardens, two watchers, six braziers, six fallen drums)");
            // the Shattered Arch: every stone at least Clearance over every standable top under it, at any time
            var tops=map.blocks.Where(b=>!map.pilotBlocks.Any(p=>Math.Abs(p.center.x-b.pos.x)<.01&&Math.Abs(p.center.z-b.pos.z)<.01)).Select(Of).ToList();
            for(double t=0;t<200;t+=3.7)for(int k=0;k<NativeCausewayRig.ArchStones;k++)
            {
                NativeCausewayRig.ArchStone(k,t,out double x,out double y,out double z,out _);double under=tops.Where(b=>x+.9>b.X0&&x-.9<b.X1&&z+1.2>b.Z0&&z-1.2<b.Z1&&b.Y1<y).Select(b=>b.Y1).DefaultIfEmpty(-99).Max();
                Check(y-.9-under>=NativeCausewayKit.Clearance,"arch stone "+k+" clears the tops under it");
                Check(Math.Abs(x)>3.2||y>7,"arch stone "+k+" over the Breach");
            }
            // the rig and the effects: the replicated state's, deterministic, telegraphs never reduced, allocation free per frame
            var looks=NativeCausewayLooks.Resolve(look.kitLooks);var rig=new NativeCausewayRig(looks,map);var fx=new NativeCausewayFx();fx.Prepare(map);
            Check(rig.Pylons==8&&rig.Braziers==6&&rig.Spires==2,"the rig finds the pylons, braziers and spires");
            double fight=10;var star=map.hazards.First(h=>h.kind=="press");double impact=fight+NativeFactoryHazards.ArmDelay+star.start+star.offset+(star.period-star.warn-star.rise-star.dwell-star.travel)+star.warn+star.rise;
            foreach(double t in new[]{fight+1,impact-1.5,impact-.2,impact+.1,impact+1,fight+28.5,fight+30.1,fight+35.5})
            {
                FactoryPart[] Parts(){rig.Build(map.hazards,3,NativePhase.Fight,fight,t,t);return rig.Parts.Items.Take(rig.Parts.Count).ToArray();}
                Check(Parts().SequenceEqual(Parts()),"the rig is deterministic at "+t);
                int Tele(double q,bool low){fx.Build(map.hazards,3,NativePhase.Fight,fight,t,t,0,2,-6,1,0,0,0,1,0,q,low);return fx.Telegraph;}
                int full=Tele(1,false);Check(full==Tele(0,false)&&full==Tele(0,true),"telegraphs are never reduced at "+t);
                Check(fx.Quads.Count<NativeCausewayFx.Capacity,"the effects fit their buffer at "+t);
            }
            fx.Build(map.hazards,3,NativePhase.Fight,fight,impact-1.5,impact-1.5,0,2,-6,1,0,0,0,1,0,1,false);Check(fx.Telegraph>40,"a star's telegraph draws ("+fx.Telegraph+")");
            int early=0;fx.Build(map.hazards,3,NativePhase.Fight,fight,fight+2,fight+2,0,2,-6,1,0,0,0,1,0,1,false);early=fx.Telegraph;Check(early==0,"nothing telegraphs in the arm delay");
            var beams=new NativeBeamList();fx.Beams=beams;fx.Build(map.hazards,3,NativePhase.Fight,fight,impact-1.5,impact-1.5,0,2,-6,1,0,0,0,1,0,1,false);Check(beams.Count>0,"the beams take the shafts when the look's beams are on");fx.Beams=null;
            for(int warm=0;warm<3;warm++){rig.Build(map.hazards,3,NativePhase.Fight,fight,impact,impact);fx.Build(map.hazards,3,NativePhase.Fight,fight,impact,impact,0,2,-6,1,0,0,0,1,0,1,false);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<240;f++){double t=fight+f*.25;rig.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t);fx.Build(map.hazards,f&3,NativePhase.Fight,fight,t,t,f*.1,2,-6,1,0,0,0,1,0,f%2,f%3==0);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"the rig and effects allocate nothing per frame ("+(after-before)+" bytes)");
            // another map draws nothing of this
            var other=new NativeCausewayFx();other.Prepare(new ContentMap{id="polar"});other.Build(map.hazards,0,NativePhase.Fight,0,30,30,0,0,0,1,0,0,0,1,0,1,false);Check(other.Quads.Count==0,"another map draws no Causeway effects");
            Check(NativeCausewayScenery.Build(new ContentMap{id="polar"})==null&&NativeCausewayKit.Build(new ContentMap{id="polar"},look)==null,"another map builds no Causeway scenery or kit");
        }

        private static void Wiring(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/"+f));
            Check(R("Runtime/NativeSourcePresentation.cs").Contains("CausewayScenery(arena);")&&R("Runtime/NativeStagePrewarm.cs").Contains("if(PrewarmCauseway(arena))built++;"),"SourceWorld draws it and PrewarmStage builds it");
            Check(R("Runtime/NativeAudio.cs").Contains("CausewayCues(arena,menu);"),"the cues play");
            Check(R("Runtime/NativeArenaDressing.cs").Contains("if(causewayKit&&NativeCausewayLooks.KitBlock(p.Definition.id))continue;"),"the dressing leaves the sculpted blocks to the kit");
            Check(R("Runtime/NativeStageHazardsRuntime.cs").Contains("VentPush(h,s);")&&R("Runtime/NativeCausewayPresentation.cs").Contains("p.Knockback+=dir*k*(float)p.Stats[\"knockbackTaken\"]"),"the wind's push runs on the host's vent step");
            var guids=new HashSet<string>();
            foreach(var f in new[]{"Core/NativeCausewayLooks.cs","Core/NativeCausewayScenery.cs","Core/NativeCausewayKit.cs","Core/NativeCausewayFx.cs","Runtime/NativeCausewayPresentation.cs"})
            {
                Check(File.Exists(Path.Combine(root,"Assets/ChipArena/"+f+".meta")),f+" has its meta");var g=System.Text.RegularExpressions.Regex.Match(R(f+".meta"),"guid: ([0-9a-f]{32})").Groups[1].Value;Check(g.Length==32&&guids.Add(g),f+": a guid of its own");
            }
            var all=Directory.GetFiles(Path.Combine(root,"Assets"),"*.meta",SearchOption.AllDirectories).Where(f=>!f.Contains("NativeCauseway")).Select(f=>System.Text.RegularExpressions.Regex.Match(File.ReadAllText(f),"guid: ([0-9a-f]{32})").Groups[1].Value);
            Check(!all.Any(g=>guids.Contains(g)),"no other asset shares the new guids");
        }
    }
}
#endif
