using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    // The Factory's presentation (docs/arenas/FACTORY_PROPOSAL.md; Core NativeFactoryHazards, NativeFactoryKit, NativeFactoryRig,
    // NativeFactoryScenery, NativeFactoryFx; Runtime NativeFactoryPresentation, NativeFactoryAudio) against the draft blockout
    // (Tests/factory-draft.json, written by docs/arenas/scripts/factory_m1.py --json; test_factory_draft.py keeps it current):
    // inert on every other map (ids, hooks, looks), the dressing resolves every draft block by its prefix and every kit kind,
    // hazard states and cues are pure functions of (definition, round, clock) with the proposal's lethal windows and telegraph
    // lamps, the kit and rigs stay inside their budgets and against surfaces, the backdrop stays outside the shell, effect
    // tiers reduce the air but never the telegraphs, and the per-frame builders allocate nothing.
    internal static class FactoryChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Factory: "+what);count++;}
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};string draft=File.ReadAllText(Path.Combine(root,"Tests/factory-draft.json"));
            var map=JsonSerializer.Deserialize<UnityMapContent>(draft,o).maps[0];var hz=JsonSerializer.Deserialize<FactoryHazardContent>(draft,o).maps[0].hazards;
            var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);NativeMapLooks.Validate(set);
            var look=NativeMapLooks.Find(set,"factory");
            Inert(root,set,look,map);Dressing(map,look);Hazards(hz,draft);Kit(map,hz,look);Scenery();Effects(hz,look);Allocation(map,hz,look);
            Console.WriteLine("PASS factory ("+count+" checks: inert on other maps and in the hooks, dressing for every draft prefix and kit kind, pure hazard states with the proposal's lethal windows, lamp telegraphs, kit/rig budgets and rule 1, backdrop outside the shell, tiers, no per-frame allocation)");
        }
        private static void Inert(string root,MapLookSet set,MapLook look,ContentMap map)
        {
            Check(look!=null&&look.look=="factory-first-shift"&&NativeBlockLooks.Dressed(look,false),"the factory look exists and dresses its blocks");
            Check(NativeMapLooks.Find(set,"brickfall")?.look=="golden-demolition"&&set.maps.Count(m=>m.id=="factory")==1,"one factory entry beside Brickfall's");
            foreach(var id in new[]{"foundry","moonlit","brickfall","siren","lavaworks","sawmill",""}){Check(!NativeFactoryScenery.Active(id)&&!NativeFactoryHazards.Active(id),id+" is not the factory");}
            Check(NativeFactoryScenery.Active("factory")&&NativeFactoryHazards.Active("factory")&&map.id=="factory","only map id factory is active");
            // the 14 existing look keys keep their ids and palette; the new key is 14
            string[] old={"plain","brick-red","brick-buff","terracotta","concrete-board","steel-green","timber","tar","asphalt","cobble","window-lit","neon","corrugated","demolition"};
            for(int i=0;i<old.Length;i++)Check(NativeBlockLooks.Id(old[i])==i,old[i]+" keeps id "+i);
            Check(NativeBlockLooks.Id("factory-grating")==NativeBlockLooks.FactoryGrating&&NativeBlockLooks.FactoryGrating==14&&NativeBlockLooks.Palette[14]==0x8C9296,"factory-grating is look 14 (#8C9296)");
            Check(NativeBlockLooks.Keys.Length<=16,"look ids fit the shader's look + 16 x flags packing");
            string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeBlockLit.shader"));
            Check(shader.Contains("#define LOOK_FACTORY_GRATING 14.0")&&shader.Contains("else if(BkIs(look,LOOK_FACTORY_GRATING))"),"the shader draws look 14");
            // the hooks: one line each, dormant unless the map id is factory, no GameObject until a factory frame
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            Check(R("NativeSourcePresentation.cs").Contains("FactoryScenery(arena);"),"SourceWorld (arena and killcam replay) calls the factory");
            Check(R("NativeBackdropPresentation.cs").Contains("FactoryBackdrop(mapId,time,camera);"),"the menu backdrop calls the factory");
            Check(R("NativeAudio.cs").Contains("FactoryCues(arena,menu);"),"the audio frame calls the factory cues");
            var p=R("NativeFactoryPresentation.cs");
            Check(p.Contains("if(!NativeFactoryScenery.Active(arena.MapId))return;if(factoryStage==null)factoryStage=gameObject.AddComponent<NativeFactoryStage>();"),"the arena hook returns before creating anything on other maps");
            Check(p.Contains("if(!NativeFactoryScenery.Active(mapId))return;if(factoryStage==null)factoryStage=gameObject.AddComponent<NativeFactoryStage>();"),"the menu hook returns before creating anything on other maps");
            Check(!p.Contains("Collider")&&!p.Contains("new GameObject")&&!p.Contains("Rigidbody"),"no colliders or objects");
            Check(p.Contains("NativeSceneryBuilder.BackdropLayer")&&p.Split("ShadowCastingMode.Off,true)").Length-1==3,"the backdrop draws on the backdrop layer with no shadow");
            Check(p.Contains("if(hz!=null&&rigBuilder!=null)")&&p.Contains("private void OnDestroy()=>Release();"),"no hazards, no rigs; resources released with the component");
            var a=R("NativeFactoryAudio.cs");Check(a.Contains("if(menu||arena==null||!NativeFactoryHazards.Active(arena.MapId))"),"audio is dormant off the factory");
            Check(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")).Contains("\"factory\""),"simulation map is integrated");
        }
        private static void Dressing(ContentMap map,MapLook look)
        {
            // every draft block is dressed by a listed entry, by its id prefix (not a material fallback)
            var expect=new Dictionary<string,string>{["factory-gallery-"]="factory-grating",["factory-catwalk-"]="factory-grating",["factory-stair-"]="factory-grating",["factory-crib-"]="factory-grating",
                ["factory-wall-"]="",["factory-floor"]="concrete-board",["factory-press-n-bolster"]="demolition",["factory-crate-"]="timber",["factory-ladle"]="steel-green",["factory-dock-a-container"]="corrugated"};
            var prefixes=new HashSet<string>();
            foreach(var b in map.blocks)
            {
                var c=NativeBlockLooks.Resolve(look.blockLooks,null,b.id,b.material,0x123456);Check(c.Listed&&!b.id.Contains("material:"),"block "+b.id+" is dressed by an entry");
                foreach(var e in expect)if(b.id.StartsWith(e.Key,StringComparison.Ordinal)&&e.Value!="")Check(NativeBlockLooks.Keys[c.Look]==e.Value,b.id+" -> "+e.Value+" (got "+NativeBlockLooks.Keys[c.Look]+")");
                var g=look.blockLooks.First(x=>NativeBlockLooks.Score(x.block,b.id,b.material)>0);Check(!g.block.StartsWith("material:",StringComparison.Ordinal),b.id+" matched by an id pattern ("+g.block+")");
                int dash=b.id.IndexOf('-',8);prefixes.Add(dash>0?b.id.Substring(0,dash):b.id);
            }
            Check(prefixes.Count>=24,"the draft carries the kit's prefix families ("+prefixes.Count+")");
            var looks=NativeFactoryKit.Resolve(look.kitLooks);
            for(int k=0;k<NativeFactoryKit.Kinds.Length;k++){var c=NativeBlockLooks.Resolve(look.kitLooks,null,NativeFactoryKit.Kinds[k],"",0);Check(c.Listed,"kit kind "+NativeFactoryKit.Kinds[k]+" has a kitLooks entry");Check((looks.Flags[k]&(NativeBlockLooks.Band|NativeBlockLooks.Tag))==0,"no kit kind carries the load-bearing band");}
            Check(look.lights.Length==8&&look.lights.All(l=>l.range>0),"8 practical lights");Check(look.shadows.cascades==4&&look.shadows.fallbackCascades==2&&look.shadows.fallbackDistance==65,"shadows 4 over 85 m, low tier 2 over 65 m");
        }
        private static FactoryHazard H(FactoryHazard[] hz,string id)=>hz.First(h=>h.id==id);
        // Seconds (1 ms steps) in [from, to) where `pred` holds.
        private static double Span(Func<double,bool> pred,double from,double to){int n=0;for(double t=from;t<to;t+=.001)if(pred(t))n++;return n*.001;}
        private static void Hazards(FactoryHazard[] hz,string draft)
        {
            NativeFactoryHazards.Validate(hz);Check(hz.Length==11,"11 hazards in the draft");
            foreach(var k in new[]{"saw-slot","saw-rail","crane","press","vent"})Check(hz.Any(h=>h.kind==k),"a "+k);
            Check(double.IsNegativeInfinity(NativeFactoryHazards.Clock(NativePhase.Countdown,10,20))&&double.IsNegativeInfinity(NativeFactoryHazards.Clock(NativePhase.Draft,10,20))&&double.IsNegativeInfinity(NativeFactoryHazards.Clock(NativePhase.Intro,10,20)),"parked outside the fight");
            Check(NativeFactoryHazards.Clock(NativePhase.Fight,10,16)==2&&NativeFactoryHazards.Clock(NativePhase.Result,10,30)==16,"clock = now - FightAt - 4 in the fight and result");
            foreach(var h in hz)
            {
                // nothing lethal before the arm time or outside the fight; pure: the same inputs give the same state
                Check(!NativeFactoryHazards.State(h,65,double.NegativeInfinity).Lethal&&NativeFactoryHazards.State(h,65,double.NegativeInfinity).Phase==NativeFactoryHazards.Parked,h.id+" parked before the arm time");
                for(double c=-2;c<h.start+h.offset;c+=.01)Check(!NativeFactoryHazards.State(h,65,c).Lethal,h.id+" harmless before its first cycle");
                for(double c=0;c<60;c+=.37){var a=NativeFactoryHazards.State(h,65,c);var b=NativeFactoryHazards.State(h,65,c);Check(a.Phase==b.Phase&&a.U==b.U&&a.Lift==b.Lift&&a.Spin==b.Spin&&a.X==b.X&&a.LoadY==b.LoadY&&a.Lethal==b.Lethal,h.id+" deterministic at "+c);
                    var p=NativeFactoryHazards.State(h,65,c+h.period);if(h.kind!=NativeFactoryHazards.Crane&&!h.alternate&&c>=h.start+h.offset)Check(p.Phase==a.Phase&&Math.Abs(p.U-a.U)<1e-9&&p.Lethal==a.Lethal,h.id+" repeats every period");}
            }
            // the proposal's lethal windows (FACTORY_PROPOSAL.md 3.8)
            var slot=H(hz,"factory-saw-slot-n");double s0=slot.start;double ls=Span(t=>NativeFactoryHazards.State(slot,65,t).Lethal,s0,s0+slot.period);Check(ls>6.4&&ls<6.7,"slitter lethal about 6.5 s of 14 ("+ls+")");
            var w=NativeFactoryHazards.State(slot,65,s0+.8);Check(w.Phase==NativeFactoryHazards.Warning&&!w.Lethal&&w.Lift==0,"slitter warning is under the floor");
            double first=s0;while(!NativeFactoryHazards.State(slot,65,first).Lethal)first+=.001;Check(first-s0>=1.6+.05,"at least 1.6 s of telegraph before the slitter is live ("+(first-s0)+")");
            var run0=NativeFactoryHazards.State(slot,65,s0+1.6+.35+.01);var run1=NativeFactoryHazards.State(slot,66,s0+1.6+.35+.01);Check(run0.Reverse!=run1.Reverse,"odd and even rounds start at opposite ends");
            var nx=NativeFactoryHazards.State(slot,66,s0+slot.period+1.6+.36);Check(nx.Reverse!=run1.Reverse,"alternate runs come back the other way");
            double speed=Math.Abs(NativeFactoryHazards.State(slot,66,s0+4).Center.x-NativeFactoryHazards.State(slot,66,s0+5).Center.x);Check(Math.Abs(speed-6)<1e-3,"the blade runs at 6 m/s");
            var rail=H(hz,"factory-saw-rail-n");double lr=Span(t=>NativeFactoryHazards.State(rail,65,t).Lethal,rail.start,rail.start+rail.period);Check(Math.Abs(lr-1.25)<.01,"rail saw lethal 1.25 s of 5 ("+lr+")");
            var press=H(hz,"factory-press-n");double lp=Span(t=>NativeFactoryHazards.State(press,65,t).Lethal,press.start,press.start+press.period);Check(lp>1.25&&lp<1.42,"press lethal about 1.33 s of 6 ("+lp+")");
            for(double t=press.start;t<press.start+press.period;t+=.01){var st=NativeFactoryHazards.State(press,65,t);if(st.Lethal)Check(st.LoadY<NativeFactoryHazards.LethalRam&&st.BoxCenter.y-st.BoxSize.y/2<.1&&st.BoxCenter.y+st.BoxSize.y/2>st.LoadY,"a live press's volume runs from the bed to above the ram's underside");}
            var crane=H(hz,"factory-crane");double lc=Span(t=>NativeFactoryHazards.State(crane,65,t).Lethal,crane.start,crane.start+crane.period);Check(lc>.14&&lc<.2,"crane lethal about 0.17 s a cycle ("+lc+")");
            double red=crane.start+crane.travel,hit=red+crane.warn+crane.rise;Check(NativeFactoryHazards.State(crane,65,red+.01).Phase==NativeFactoryHazards.Warning&&!NativeFactoryHazards.State(crane,65,hit-.2).Lethal,"3 s red telegraph, harmless until the load is low");
            Check(Math.Abs(NativeFactoryHazards.State(crane,65,red+1).ImpactAt-hit)<1e-9,"the state knows its impact time");
            var landed=NativeFactoryHazards.State(crane,65,hit+1);Check(landed.Landed&&landed.LoadY==0&&!landed.Lethal&&landed.Zone==NativeFactoryHazards.ZoneOf(crane,65,0),"landed load is cover at its zone");
            var zs=new HashSet<int>();for(int n=0;n<4;n++)zs.Add(NativeFactoryHazards.ZoneOf(crane,64,n));Check(zs.Count==4,"the crane serves every zone in four cycles");
            Check(NativeFactoryHazards.ZoneOf(crane,64,0)!=NativeFactoryHazards.ZoneOf(crane,65,0),"odd rounds start on the other side");
            for(double t=crane.start;t<crane.start+crane.period;t+=.05){var st=NativeFactoryHazards.State(crane,65,t);Check(st.HookY>st.LoadY+crane.size.y-1e-6||!st.Attached,"the hook stays above the load it carries");Check(st.LoadY>=0&&st.LoadY<=crane.carry+1e-6,"the load stays between the floor and its carry height");}
            // cues: each exactly once however the clock is cut into frames
            var buf=new FactoryCue[512];int whole=NativeFactoryHazards.Cues(hz,65,0,40,buf);var kinds=new int[17];for(int i=0;i<whole;i++)kinds[buf[i].Kind]++;
            int pieces=0;var r=new Random(7);double from=0;while(from<40){double to=Math.Min(40,from+.005+r.NextDouble()*.05);pieces+=NativeFactoryHazards.Cues(hz,65,from,to,buf);from=to;}
            Check(whole>0&&pieces==whole,"cues are heard once whatever the frame cut ("+whole+" vs "+pieces+")");
            Check(kinds[NativeFactoryHazards.CueHorn]==2*2&&kinds[NativeFactoryHazards.CueImpact]==2&&kinds[NativeFactoryHazards.CuePressSlam]>0&&kinds[NativeFactoryHazards.CueGrind]>0,"horns twice per drop, impacts, slams and grinding in 40 s");
            Check(NativeFactoryHazards.Cues(hz,65,double.NegativeInfinity,double.NegativeInfinity,buf)==0&&NativeFactoryHazards.Cues(hz,65,5,5,buf)==0,"an empty window has no cues");
            // lamps: green parked, amber strobing through the warning, red while live; zone lamps only for the served zone
            var park=NativeFactoryHazards.State(slot,65,.5);Check(NativeFactoryRig.SlotLamp(park,.5,36,.5)==NativeFactoryRig.LampSafe,"slot lamps green when parked");
            Check(NativeFactoryRig.SlotLamp(w,.5,36,0.01)==NativeFactoryRig.LampAmber&&NativeFactoryRig.SlotLamp(w,.5,36,1.0/NativeFactoryRig.WarnHz*.75)==NativeFactoryRig.LampOff,"slot lamps strobe amber through the warning");
            var live=NativeFactoryHazards.State(slot,65,s0+5);Check(NativeFactoryRig.SlotLamp(live,live.Reverse?0:1,36,3)!=NativeFactoryRig.LampSafe&&NativeFactoryRig.SlotLamp(live,live.U,36,0.01)==NativeFactoryRig.LampChase,"red with a chase at the blade while it runs");
            var cw=NativeFactoryHazards.State(crane,65,red+.5);Check(NativeFactoryRig.ZoneLamp(cw,cw.Zone,0.01)==NativeFactoryRig.LampRed&&NativeFactoryRig.ZoneLamp(cw,(cw.Zone+1)%4,0.01)==NativeFactoryRig.LampOff,"only the served zone's lamps strobe red");
            // validation rejects what cannot run
            void Reject(Action<FactoryHazard> m,string what){var bad=JsonSerializer.Deserialize<FactoryHazardContent>(draft,new JsonSerializerOptions{IncludeFields=true}).maps[0].hazards.First(x=>x.id==slot.id);m(bad);bool threw=false;try{NativeFactoryHazards.Validate(new[]{bad});}catch(InvalidOperationException){threw=true;}Check(threw,"validation rejects "+what);}
            Reject(h=>h.kind="lava","an unknown kind");Reject(h=>h.period=5,"a motion longer than its period");Reject(h=>h.radius=0,"a blade without a radius");Reject(h=>h.warn=0,"no warning");
            Reject(h=>{h.axis=new V3(0,0,2);},"a bad axle");
        }
        private static void Kit(ContentMap map,FactoryHazard[] hz,MapLook look)
        {
            var looks=NativeFactoryKit.Resolve(look.kitLooks);var a=NativeFactoryKit.Build(map.blocks,hz,looks);var b=NativeFactoryKit.Build(map.blocks,hz,looks);
            Check(a.Count==b.Count&&Enumerable.Range(0,a.Count).All(i=>a.Items[i].X==b.Items[i].X&&a.Items[i].Y==b.Items[i].Y&&a.Items[i].Z==b.Items[i].Z&&a.Items[i].Look==b.Items[i].Look&&a.Items[i].Color==b.Items[i].Color),"the kit is deterministic");
            Check(a.Count>3000&&a.Count<=NativeFactoryKit.MaxParts,"kit parts within budget ("+a.Count+")");
            int casters=0;for(int i=0;i<a.Count;i++)if(a.Items[i].Shadow)casters++;int draws=(casters+499)/500+(a.Count-casters+499)/500+5;Check(draws<=NativeBlockLooks.DrawBudget,"kit + rig draws "+draws+" within 60");
            var boxes=map.blocks.Select(x=>(x0:x.pos.x-x.size.x/2,x1:x.pos.x+x.size.x/2,y0:x.pos.y-x.size.y/2,y1:x.pos.y+x.size.y/2,z0:x.pos.z-x.size.z/2,z1:x.pos.z+x.size.z/2)).ToArray();
            int loose=0;
            for(int i=0;i<a.Count;i++)
            {
                var p=a.Items[i];Check(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&p.Hx>0&&p.Hy>0&&p.Hz>0&&Math.Abs(p.Qx*p.Qx+p.Qy*p.Qy+p.Qz*p.Qz+p.Qw*p.Qw-1)<1e-3,"kit part "+i+" is a valid box");
                Check(p.Look>=0&&p.Look<NativeBlockLooks.Keys.Length&&(p.Flags&(NativeBlockLooks.Band|NativeBlockLooks.Tag))==0,"kit part "+i+" has a known look and no band");
                // rule 1 proxy: below 3 m a part either lies on the floor (paint, plates, grates) or touches a block within 25 cm
                double r=Math.Sqrt(p.Hx*p.Hx+p.Hy*p.Hy+p.Hz*p.Hz);if(p.Y-r>3||p.Y+Math.Max(p.Hy,0)<.06)continue;
                bool near=boxes.Any(x=>p.X+r>x.x0-.25&&p.X-r<x.x1+.25&&p.Y+r>x.y0-.25&&p.Y-r<x.y1+.25&&p.Z+r>x.z0-.25&&p.Z-r<x.z1+.25&&!(x.y0<-.4));
                if(!near)loose++;
            }
            Check(loose==0,"no kit part stands free of a block below 3 m ("+loose+")");
            // the rig: capacity, the hanging load casts no shadow while it is high, telegraph lamps exist for every hazard
            var rig=new NativeFactoryRig(looks);var crane=H(hz,"factory-crane");
            for(double c=0;c<60;c+=.25){rig.Build(hz,65,c);Check(rig.Parts.Count<NativeFactoryRig.Capacity,"rig within capacity at "+c);var st=NativeFactoryHazards.State(crane,65,c);
                if(st.LoadY>2.5)for(int i=0;i<rig.Parts.Count;i++){var p=rig.Parts.Items[i];if(p.Look==looks.Look[NativeFactoryKit.Load]&&p.Color==looks.Color[NativeFactoryKit.Load])Check(!p.Shadow,"the hanging load casts no sun shadow at "+c);}}
            rig.Build(hz,65,double.NegativeInfinity);int lamps=0;for(int i=0;i<rig.Parts.Count;i++)if(rig.Parts.Items[i].Look==NativeBlockLooks.Id("neon"))lamps++;Check(lamps>=2*2*19+2+2*3+1+16,"every hazard has its lamps ("+lamps+")");
        }
        private static void Scenery()
        {
            var a=NativeFactoryScenery.Build();var b=NativeFactoryScenery.Build();Check(a.Length==NativeFactoryScenery.Sectors,"five sectors");int verts=0;
            for(int s=0;s<a.Length;s++)
            {
                var m=a[s];Check(m.Positions.SequenceEqual(b[s].Positions)&&m.Colors.SequenceEqual(b[s].Colors)&&m.Indices.SequenceEqual(b[s].Indices),"sector "+s+" deterministic");verts+=m.Vertices;
                for(int t=0;t<m.Indices.Length;t+=3)
                {
                    int i0=m.Indices[t],i1=m.Indices[t+1],i2=m.Indices[t+2];double top=Math.Max(m.Positions[i0*3+1],Math.Max(m.Positions[i1*3+1],m.Positions[i2*3+1]));if(top<=.01)continue;
                    double cx=(m.Positions[i0*3]+m.Positions[i1*3]+m.Positions[i2*3])/3,cz=(m.Positions[i0*3+2]+m.Positions[i1*3+2]+m.Positions[i2*3+2])/3;
                    Check(Math.Abs(cx)>=NativeFactoryScenery.InnerX-1e-3||Math.Abs(cz)>=NativeFactoryScenery.InnerZ-1e-3,"sector "+s+" stands outside the shell");
                    if(top>NativeFactoryScenery.OrbitCap)Check(NativeFactoryScenery.OrbitDistance(cx,cz)>=8,"tall backdrop keeps 8 m from the menu orbit");
                }
            }
            Check(verts<200000,"backdrop under 200k vertices ("+verts+")");
            for(double t=0;t<200;t+=3.1){NativeFactoryScenery.Trolley(t,out double x,out _,out double z);Check(Math.Abs(z)>NativeFactoryScenery.InnerZ+20&&Math.Abs(x)<=54.001,"the goliath trolley stays on its girder");}
        }
        private static NativeFxLight Light(){var l=new NativeFxLight{Lx=.5,Ly=.59,Lz=-.63,Sr=1.1,Sg=.9,Sb=.7};for(int i=0;i<9;i++)l.Ambient[i]=.15;return l;}
        private static void Effects(FactoryHazard[] hz,MapLook look)
        {
            var fx=new NativeFactoryFx();var l=Light();var crane=H(hz,"factory-crane");double warn=crane.start+crane.travel+1.5;
            int Halos(double q,bool low){fx.Build(hz,65,warn,40,-10,1.8,-12,1,0,0,0,1,0,l,q,low,true);return fx.Halos;}
            int full=Halos(1,false),reduced=Halos(.3,false),lowTier=Halos(.3,true);Check(full>0&&full==reduced&&full==lowTier,"telegraph halos never reduced ("+full+"/"+reduced+"/"+lowTier+")");
            fx.Build(hz,65,warn,40,-10,1.8,-12,1,0,0,0,1,0,l,1,false,true);int sparks=fx.Sparks+fx.Puffs+fx.Motes,shafts=fx.Shafts;
            fx.Build(hz,65,warn,40,-10,1.8,-12,1,0,0,0,1,0,l,.3,true,true);int lowAir=fx.Sparks+fx.Puffs+fx.Motes;Check(lowAir<sparks&&shafts>0&&fx.Shafts==0,"Reduced and the low tier thin the air and drop the shafts");
            fx.Build(hz,65,warn,40,0,32,46,1,0,0,0,1,0,l,1,false,false);Check(fx.Motes==0&&fx.Shafts==0,"behind the menu: backdrop life only");
            // the sun sheets stay inside the hall they light (outer walls x ±38, z ±30; the halls' faces of the bay walls z ±10.4), for any sun
            for(int s=0;s<12;s++){double el=(20+s*5)*Math.PI/180,br=s*30*Math.PI/180;var sun=new NativeFxLight{Lx=Math.Cos(el)*Math.Sin(br),Ly=Math.Sin(el),Lz=Math.Cos(el)*Math.Cos(br),Sr=1,Sg=1,Sb=1};
                fx.Build(hz,65,warn,40,-10,1.8,-12,1,0,0,0,1,0,sun,1,false,true);bool inside=true;for(int q=fx.ShaftStart;q<fx.ShaftStart+fx.Shafts;q++)for(int c=0;c<4;c++){double x=fx.Quads.Corners[q*12+c*3],z=fx.Quads.Corners[q*12+c*3+2];inside&=Math.Abs(x)<=38.9&&Math.Abs(z)<=30.9&&Math.Abs(z)>=9.6;}
                Check(inside,"sun shafts stay inside the halls (sun "+(20+s*5)+"/"+(s*30)+")");}
            for(double t=0;t<60;t+=.5){fx.Build(hz,65,t,t+20,-10,1.8,-12,1,0,0,0,1,0,l,1,false,true);Check(fx.Quads.Count<NativeFactoryFx.Capacity,"effects within capacity at "+t);}
            // the same clock gives the same quads (the killcam and every client see the same sparks)
            fx.Build(hz,65,17.3,40,-10,1.8,-12,1,0,0,0,1,0,l,1,false,true);var first=fx.Quads.Corners.Take(fx.Quads.Count*12).ToArray();fx.Build(hz,65,17.3,40,-10,1.8,-12,1,0,0,0,1,0,l,1,false,true);
            Check(first.SequenceEqual(fx.Quads.Corners.Take(fx.Quads.Count*12)),"effects are a pure function of the clocks and camera");
            var buf=new FactoryCue[64];int n=0,m=0;for(int k=0;k<15;k++)n+=NativeFactoryAmbience.Between(k*4,k*4+4,buf);Check(NativeFactoryAmbience.Between(0,60,buf)==0,"a window over 5 s (a stalled or rewound clock) plays nothing");for(double t=0;t<60;t+=.0167)m+=NativeFactoryAmbience.Between(t,Math.Min(60,t+.0167),buf);Check(n>10&&n==m,"ambience one-shots heard once ("+n+")");
        }
        private static void Allocation(ContentMap map,FactoryHazard[] hz,MapLook look)
        {
            var looks=NativeFactoryKit.Resolve(look.kitLooks);var rig=new NativeFactoryRig(looks);var fx=new NativeFactoryFx();var l=Light();var cues=new FactoryCue[64];
            for(int i=0;i<3;i++){rig.Build(hz,65,i);fx.Build(hz,65,i,i,0,1.8,0,1,0,0,0,1,0,l,1,false,true);}
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<240;i++){double c=i*.25;foreach(var h in hz)NativeFactoryHazards.State(h,65,c);rig.Build(hz,65,c);fx.Build(hz,65,c,c+20,-10,1.8,-12,1,0,0,0,1,0,l,1,false,true);NativeFactoryHazards.Cues(hz,65,c,c+.25,cues);NativeFactoryAmbience.Between(c,c+.25,cues);}
            long after=GC.GetAllocatedBytesForCurrentThread();Check(after-before==0,"states, rig, effects and cues allocate nothing per frame ("+(after-before)+" bytes)");
        }
    }
}
