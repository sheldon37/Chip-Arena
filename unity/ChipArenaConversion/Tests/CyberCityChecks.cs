using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // Cyber City presentation (docs/arenas/CYBERCITY_PROPOSAL.md, CYBERCITY-SIMULATION-CONTRACT.md; Core NativeCyberCityLooks,
    // NativeCyberCityHazards, NativeCyberCityKit, NativeCyberCityScenery, NativeCyberCityFx, NativeCyberCityAmbience; Runtime
    // NativeCyberCityPresentation, NativeCyberCityAudio). Against the frozen draft blockout (Tests/cybercity-draft.json, written by
    // docs/arenas/scripts/cybercity_m1.py --json): the look bank leaves every base key and old map alone and the shader's bank
    // ids match; the neon-rain preset validates; every draft block resolves through a Cyber City entry and its billboard
    // segments validate; the kit is deterministic, keyed by every prefix family, keeps §2.1 rule 1 below 3 m and stays in the
    // dressing budget; the hazard timing is the contract's table and a pure function; the drives, the train and the FX are pure,
    // never reduce a telegraph and allocate nothing; the backdrop keeps out of the slab and the menu orbit within its budget;
    // the runtime hooks are dormant off the map.
    internal static class CyberCityChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Cyber City: "+what);count++;}
        // The frozen draft (System.Text.Json fills the fields).
#pragma warning disable CS0649
        private sealed class Draft{public DraftMap[] maps;}
        private sealed class DraftMap{public string id;public ContentBlock[] blocks;public StructureDefinition[] segments;public MapHazard[] hazards;public MapBounds bounds;public MapSpawns spawns;public MapPilotBlock[] pilotBlocks;}
#pragma warning restore CS0649
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};
            var draft=JsonSerializer.Deserialize<Draft>(File.ReadAllText(Path.Combine(root,"Tests/cybercity-draft.json")),o).maps.Single();
            var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);
            Looks(root,set,draft);Hazards(draft);Kit(draft);Train(draft);Fx(draft);Backdrop();Hooks(root);
            Console.WriteLine("PASS cyber city ("+count+" checks: look bank beside the base keys (old maps and Brickfall untouched, shader ids match), neon-rain preset, every draft block dressed by a Cyber City entry, billboard segments valid; kit deterministic per prefix family, rule 2.1.1 below 3 m, dressing budget; contract timing table, purity, round parity; drives, train envelope, FX purity, tiers (telegraphs never reduced), no allocations; backdrop outside the slab and the menu orbit within 10 draws; runtime and audio hooks dormant off the map)");
        }
        private static void Looks(string root,MapLookSet set,DraftMap draft)
        {
            NativeMapLooks.Validate(set);var c=NativeMapLooks.Find(set,"cybercity");
            Check(c!=null&&c.look=="neon-rain"&&NativeMapLooks.ByLook(set,"neon-rain")==c&&NativeBlockLooks.Dressed(c,false),"the neon-rain preset is cybercity's, dressed");
            Check(c.lights.Length==8&&c.sky.variant=="night"&&c.fog.mode=="linear"&&c.shadows.cascades==4&&c.shadows.fallbackCascades==2&&c.ambience=="cybercity","8 practical lights, night sky, linear fog, 4/2 cascades, the ambience id");
            Check(NativeCyberCityLooks.Active("cybercity")&&!NativeCyberCityLooks.Active("brickfall")&&!NativeCyberCityLooks.Active(null)&&!NativeCyberCityLooks.Active("cybercity2"),"Active only for the map id cybercity");
            // The bank: ids beyond every base key, slot keys known, palettes set; the base keys and Resolve untouched for base looks.
            for(int i=0;i<NativeCyberCityLooks.Slots;i++){var k=NativeCyberCityLooks.Keys[i];if(k==null){Check(NativeCyberCityLooks.Palette[i]==-1,"unused slot "+i);continue;}
                Check(NativeBlockLooks.Id(k)==NativeCyberCityLooks.Offset+i&&NativeCyberCityLooks.Is(NativeBlockLooks.Id(k))&&Array.IndexOf(NativeBlockLooks.Keys,k)<0&&NativeCyberCityLooks.Palette[i]>=0,"bank key "+k);
                Check(i<16&&i!=6,"slot "+k+" fits the shader's 16 ids and avoids the timber id");}
            Check(NativeCyberCityLooks.Offset>=NativeBlockLooks.Keys.Length&&NativeCyberCityLooks.Bank>NativeBlockLooks.SpinZ&&(NativeCyberCityLooks.Bank&(NativeBlockLooks.SpinZ*2-1))==0,"bank ids and flag clear of the base keys and flags");
            for(int i=0;i<NativeBlockLooks.Keys.Length;i++)Check(NativeBlockLooks.Id(NativeBlockLooks.Keys[i])==i,"base key "+NativeBlockLooks.Keys[i]+" keeps its id");
            Check(NativeBlockLooks.Id("cyber-marble")==-1&&NativeBlockLooks.Id(null)==-1,"unknown keys stay unknown");
            var bf=NativeMapLooks.Find(set,"brickfall");int bfChecked=0;
            foreach(var e in bf.blockLooks){var ch=NativeBlockLooks.Resolve(bf.blockLooks,null,e.block.Replace("*","x"),"brick",0);Check((ch.Flags&NativeCyberCityLooks.Bank)==0&&ch.Look<NativeBlockLooks.Keys.Length,"brickfall's entries never take the bank flag ("+e.block+")");bfChecked++;}
            foreach(var e in set.preview){var ch=NativeBlockLooks.Resolve(null,set.preview,e.block.Replace("*","x"),"stone",0x123456);Check((ch.Flags&NativeCyberCityLooks.Bank)==0,"the dev preview never takes the bank flag");}
            Check(bfChecked>40,"brickfall entries checked ("+bfChecked+")");
            // The shader: the flag and the slot ids, the bank only under the flag, the dust layer kept off it.
            string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeBlockLit.shader"));
            Check(shader.Contains("#define FLAG_CYBER "+NativeCyberCityLooks.Bank+"u"),"FLAG_CYBER = Bank");
            foreach(var (n,v) in new[]{("TILE",NativeCyberCityLooks.Tile),("PANEL",NativeCyberCityLooks.Panel),("METAL",NativeCyberCityLooks.Metal),("GRATE",NativeCyberCityLooks.Grate),("ROAD",NativeCyberCityLooks.Road),("GLASS",NativeCyberCityLooks.Glass),("HOLO",NativeCyberCityLooks.Holo),("TUBE",NativeCyberCityLooks.Tube)})
            {var m=Regex.Match(shader,@"#define CY_"+n+@" ([0-9.]+)\r?\n");Check(m.Success&&float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)==v,"CY_"+n+" = "+v);}
            int shared0=shader.IndexOf("// SHARED BEGIN",StringComparison.Ordinal),shared1=shader.IndexOf("// SHARED END",StringComparison.Ordinal),fn=shader.IndexOf("void BkCyber(",StringComparison.Ordinal);
            Check(fn>shared0&&fn<shared1,"BkCyber is in the SHARED section the offline harness runs");
            Check(shader.Contains("bool cyber=(fl&FLAG_CYBER)!=0u;\n            if(cyber)BkCyber(")&&shader.Contains("if(nO.y>0.7&&!cyber)"),"the bank runs only under its flag; the dust and soot layer skips it");
            // Every draft block resolves through a Cyber City id entry (not a material fallback); kit-less families are listed.
            int listed=0;var looks=new HashSet<string>();
            foreach(var b in draft.blocks)
            {
                var best=c.blockLooks.Where(e=>!e.block.StartsWith("material:",StringComparison.Ordinal)).Select(e=>NativeBlockLooks.Score(e.block,b.id,b.material)).DefaultIfEmpty(-1).Max();
                Check(best>=1,"draft block "+b.id+" resolves through a Cyber City id entry");var ch=NativeBlockLooks.Resolve(c.blockLooks,null,b.id,b.material,0);listed++;looks.Add(NativeCyberCityLooks.KeyOf(ch.Look,ch.Flags));
                Check(ch.Listed&&NativeCyberCityLooks.KeyOf(ch.Look,ch.Flags)!=null,"draft block "+b.id+" has a known look");
                if(b.role=="structural")Check((ch.Flags&NativeBlockLooks.Band)!=0&&(ch.Flags&NativeBlockLooks.Tag)!=0,"structural member "+b.id+" carries the 2.3 band and DANGER tag");
                else Check((ch.Flags&NativeBlockLooks.Band)==0,"only members carry the band ("+b.id+")");
            }
            foreach(var s in draft.segments)foreach(var r in s.rubble)Check(c.blockLooks.Any(e=>NativeBlockLooks.Score(e.block,r.id,r.material)>=1),"rubble "+r.id+" resolves");
            Check(listed==draft.blocks.Length&&listed>600&&looks.Contains("cyber-glass")&&looks.Contains("cyber-road")&&looks.Contains("cyber-holo")&&looks.Contains("cyber-grate")&&looks.Contains("concrete-board"),"all "+listed+" draft blocks dressed ("+looks.Count+" looks)");
            // The draft's billboard segments are valid structure content (Core NativeStructureRules.Validate).
            var map=new ContentMap{id=draft.id,blocks=draft.blocks,segments=draft.segments};NativeStructureRules.Validate(map);
            Check(draft.segments.Length==2&&draft.segments.All(s=>s.mode=="tear-hinge"&&s.supports.Length==2&&s.failBelow==2&&s.telegraph>=NativeStructureRules.MinTelegraph&&s.crush.damage==45&&s.crush.cap==60),"two tear-hinge billboards, either strut drops one, 1 s telegraph, crush 45 capped at 60");
        }
        private static NativeHazardState At(MapHazard h,int round,double t)=>NativeCyberCityHazards.Evaluate(h,round,10,t);
        private static void Hazards(DraftMap draft)
        {
            var d=NativeCyberCityHazards.Draft();Check(d.Length==draft.hazards.Length,"Draft() lists the blockout's hazards");
            for(int i=0;i<d.Length;i++)
            {
                var a=d[i];var b=draft.hazards[i];
                Check(a.id==b.id&&a.kind==b.kind&&a.first==b.first&&a.period==b.period&&a.offset==b.offset&&a.warn==b.warn&&a.danger==b.danger&&a.speed==b.speed&&a.length==b.length&&a.charge==b.charge&&a.live==b.live&&a.tick==b.tick&&a.damage==b.damage&&a.lethal==b.lethal,"Draft() "+a.id+" equals the blockout's definition");
                Check(Math.Abs(a.center.x-b.center.x)<1e-4&&Math.Abs(a.center.y-b.center.y)<1e-4&&Math.Abs(a.size.x-b.size.x)<1e-4&&Math.Abs(a.size.y-b.size.y)<1e-4&&Math.Abs(a.size.z-b.size.z)<1e-4,"Draft() "+a.id+" volume");
            }
            var mag=d[0];var gw=d[1];var ge=d[2];
            // The contract's table (round 1, fight at 10 s): pass 0 E->W, head at the east portal at 28 s; then every 32 s, alternating.
            (double t,NativeHazardPhase p)[] table={(10,NativeHazardPhase.Idle),(21.99,NativeHazardPhase.Idle),(22.01,NativeHazardPhase.Warn),(24.99,NativeHazardPhase.Warn),(25.01,NativeHazardPhase.Danger),(27.99,NativeHazardPhase.Danger),(28.01,NativeHazardPhase.Active),(30.76,NativeHazardPhase.Active),(30.78,NativeHazardPhase.Clear),(32.76,NativeHazardPhase.Clear),(32.8,NativeHazardPhase.Idle),(54.01,NativeHazardPhase.Warn),(60.5,NativeHazardPhase.Active)};
            foreach(var (t,p) in table)Check(At(mag,1,t).Phase==p,"maglev at "+t+" s is "+p+" (got "+At(mag,1,t).Phase+")");
            var s0=At(mag,1,28.5);Check(s0.Cycle==0&&s0.Direction==-1&&Math.Abs(s0.Head-(40-44*.5))<1e-6&&Math.Abs(s0.Tail-(s0.Head+42))<1e-6,"pass 0 runs east to west: head "+s0.Head);
            var s1=At(mag,1,60.5);Check(s1.Cycle==1&&s1.Direction==1&&Math.Abs(s1.Head-(-40+44*.5))<1e-6,"pass 1 runs west to east");
            Check(At(mag,2,28.5).Direction==1&&At(mag,3,28.5).Direction==-1,"the first pass alternates with the round's parity");
            Check(Math.Abs(At(mag,1,23.5).Level-.5f)<1e-5&&At(mag,1,26).Level==1&&Math.Abs(At(mag,1,31.7727).Level-.5f)<2e-3,"warning builds, danger and pass full, clear fades");
            Check(Math.Abs(At(mag,1,25).Lead-3)<1e-9&&At(mag,1,26.5).Telegraph&&!At(mag,1,29).Telegraph,"lead time and telegraph flag");
            Check(NativeCyberCityHazards.TrainBox(mag,At(mag,1,29.5),out V3 tc,out V3 ts)&&Math.Abs(ts.x-42)<1e-3&&ts.y==mag.size.y&&ts.z==mag.size.z&&!NativeCyberCityHazards.TrainBox(mag,At(mag,1,20),out _,out _),"the train's box inside the portals, nothing when idle");
            Check(NativeCyberCityHazards.TrainBox(mag,At(mag,1,28.2),out tc,out ts)&&Math.Abs(tc.x+ts.x/2-40)<1e-3,"the box is clipped at the portal while the train enters");
            // Grids: idle 8.5 s, charge 2.5, live 2, discharge 1; west first on odd rounds; even rounds shift by `offset` (west +7, east -7), so the east leads.
            (double t,NativeHazardPhase p)[] gt={(20,NativeHazardPhase.Idle),(22.6,NativeHazardPhase.Warn),(25.1,NativeHazardPhase.Active),(26.9,NativeHazardPhase.Active),(27.1,NativeHazardPhase.Clear),(28.1,NativeHazardPhase.Idle),(36.6,NativeHazardPhase.Warn)};
            foreach(var (t,p) in gt)Check(At(gw,1,t).Phase==p,"grid-w at "+t+" s is "+p);
            Check(At(ge,1,32.5).Phase==NativeHazardPhase.Active&&At(gw,1,32.5).Phase==NativeHazardPhase.Idle&&At(gw,2,25.1).Phase==NativeHazardPhase.Idle&&At(gw,2,32.1).Phase==NativeHazardPhase.Active&&At(ge,2,25.1).Phase==NativeHazardPhase.Active&&At(ge,2,32.1).Phase!=NativeHazardPhase.Active,"the east coupling runs 7 s after the west; on even rounds the east leads by 7 s");
            Check(At(gw,1,9).Phase==NativeHazardPhase.Idle&&At(mag,1,9).Phase==NativeHazardPhase.Idle&&NativeCyberCityHazards.Evaluate(null,1,10,30).Phase==NativeHazardPhase.Idle&&NativeCyberCityHazards.Evaluate(new MapHazard{kind="laser"},1,10,30).Phase==NativeHazardPhase.Idle,"idle before the fight, without a definition and for unknown kinds");
            Check(NativeCyberCityHazards.At(mag,NativePhase.Fight,1,10,29.5).Phase==NativeHazardPhase.Active&&NativeCyberCityHazards.At(mag,NativePhase.Result,1,10,29.5).Phase==NativeHazardPhase.Active&&NativeCyberCityHazards.At(mag,NativePhase.Countdown,2,10,29.5).Phase==NativeHazardPhase.Idle&&NativeCyberCityHazards.At(gw,NativePhase.Draft,1,10,25.1).Phase==NativeHazardPhase.Idle&&NativeCyberCityHazards.At(gw,NativePhase.Intro,1,10,25.1).Phase==NativeHazardPhase.Idle,"hazards run in Fight and Result only (FightAt is stale in the countdown)");
            Check(Math.Abs(At(mag,1,15).Until-7)<1e-9&&Math.Abs(At(mag,1,33).Until-21)<1e-9&&Math.Abs(At(gw,1,12).Until-10.5)<1e-9&&Math.Abs(At(gw,1,20).Until-2.5)<1e-9,"Idle's Until counts down to the next warning, before the first one too");
            Check(At(mag,1,15).Clear(6)&&!At(mag,1,17).Clear(6)&&!At(mag,1,23).Clear(0)&&At(mag,1,33).Clear(6)&&At(gw,1,20).Clear(1.5)&&!At(gw,1,21.5).Clear(1.5)&&!At(gw,1,25.1).Clear(0),"the nav gate: Idle with the margin left");
            // Pure: the same inputs give the same state (the killcam replays on the same clock).
            bool same=true;for(double t=0;t<140;t+=.37)foreach(var h in d){var x=At(h,1,t);var y=At(h,1,t);if(x.Phase!=y.Phase||x.Cycle!=y.Cycle||!(x.Head==y.Head||double.IsNaN(x.Head)&&double.IsNaN(y.Head))||x.Level!=y.Level)same=false;}
            Check(same,"hazard states are a pure function of (round, FightAt, time)");
            // Cues: one per phase start, the maglev's warning heard map-wide.
            Check(NativeCyberCityAmbience.Cue(mag,At(mag,1,22.1),out string clip,out _,out _,out bool pos)=="maglev.warn"&&clip=="countdown"&&!pos&&NativeCyberCityAmbience.Cue(mag,At(mag,1,22.6),out _,out _,out _,out _)==null,"the warning chime fires in its first .25 s, heard everywhere");
            Check(NativeCyberCityAmbience.Cue(mag,At(mag,1,25.1),out clip,out _,out _,out _)=="maglev.horn"&&NativeCyberCityAmbience.Cue(mag,At(mag,1,28.1),out clip,out _,out _,out pos)=="maglev.pass"&&pos&&NativeCyberCityAmbience.Cue(gw,At(gw,1,25.05),out clip,out _,out _,out _)=="grid.live"&&clip=="lightning","horn, rush and the coupling's crack");
        }
        private static void Kit(DraftMap draft)
        {
            var a=NativeCyberCityKit.Build(draft.blocks,draft.hazards);var b=NativeCyberCityKit.Build(draft.blocks,draft.hazards);
            Check(a.Parts.Length==b.Parts.Length&&a.Parts.Zip(b.Parts,(x,y)=>x.Parent==y.Parent&&x.X==y.X&&x.Y==y.Y&&x.Z==y.Z&&x.Hx==y.Hx&&x.Look==y.Look&&x.Color==y.Color&&x.Drive==y.Drive).All(v=>v),"the kit is deterministic ("+a.Parts.Length+" parts)");
            Check(a.Maglev==0&&a.Grids[0]==1&&a.Grids[1]==2,"the kit finds the maglev and both couplings");
            Check(a.Parts.All(p=>p.Parent>=0&&p.Parent<draft.blocks.Length&&p.Hx>0&&p.Hy>0&&p.Hz>0&&NativeCyberCityLooks.KeyOf(p.Look,p.Flags)!=null&&p.Gain>0),"parts have parents, sizes, known looks");
            // Every prefix family that should be dressed is (by stem), and the drives sit on the right blocks.
            var stems=a.Parts.Select(p=>NativeCyberCityKit.Stem(draft.blocks[p.Parent].id)).ToArray();
            foreach(var fam in new[]{"frontage-","portal-","trench-wall-","guideway","trench-floor","ground-n","ground-s","plaza-deck","grid-w-deck","grid-e-deck","grid-w-pylon-","grid-e-pylon-","station-deck","station-col-","station-lift","station-lift-head","station-stair-","nova-spire","vega-spire","nova-s","vega-s","nova-roof-unit-","lantern-post-","signal-post-","lantern-bar","signal-bar","lantern-booth","lantern-box-3","lantern-stall-","neon-van-","rain-van-","neon-kiosk","lamp-n-","lamp-s-","billboard-n-strut-","billboard-n-frame-","rank-canopy","dock-canopy","rank-cab-","prom-n-planter-","prom-s-shelter-","pit-nw-stair-","pit-nw-rail-"})
                Check(stems.Any(s=>s!=null&&s.StartsWith(fam,StringComparison.Ordinal)),"the "+fam+" family is dressed");
            Check(a.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveCoil)==80&&a.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveSafe)==16&&a.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveGridBar)>=8&&a.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveGridDeck)==2&&a.Parts.Count(p=>p.Drive==NativeCyberCityKit.DriveGridLamp)==8,"80 coils, 16 SAFE lights (6 alcoves and 2 stair exits a wall), coupling rails, decks and 8 pylon lamps");
            Check(a.Parts.Where(p=>p.Drive==NativeCyberCityKit.DriveEdge).All(p=>stems[Array.IndexOf(a.Parts,p)].StartsWith("trench-wall-",StringComparison.Ordinal)||stems[Array.IndexOf(a.Parts,p)]=="plaza-deck"),"edge strips only on the Cut's walls and the plaza bridge");
            // §2.1 rule 1: below 3 m a part stays within .25 m of its block, unless it is floor paint (under 6 cm, at or below the
            // block's top) or thin (a post, a lamp: at most .35 m across).
            int bad=0;string first=null;
            for(int i=0;i<a.Parts.Length;i++)
            {
                var p=a.Parts[i];var bl=draft.blocks[p.Parent];float wy=bl.pos.y+p.Y;if(wy-p.Hy>=3)continue;
                float ox=Math.Max(0,Math.Abs(p.X)+p.Hx-bl.size.x/2),oy=Math.Max(0,Math.Abs(p.Y)+p.Hy-bl.size.y/2),oz=Math.Max(0,Math.Abs(p.Z)+p.Hz-bl.size.z/2);
                bool paint=p.Hy*2<=.06f&&wy-p.Hy<=bl.pos.y+bl.size.y/2+.07f,thin=Math.Max(p.Hx,p.Hz)*2<=.35f;
                if(Math.Max(ox,Math.Max(oy,oz))>.25f+1e-4f&&!paint&&!thin){bad++;first??=bl.id+" part "+i;}
            }
            Check(bad==0,"rule 2.1.1: nothing below 3 m stands out more than .25 m ("+bad+", first "+first+")");
            // Budget: the dressing draws (500 a draw) and glows.
            int draws=(a.Parts.Count(p=>p.Shadow)+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk+(a.Parts.Count(p=>!p.Shadow)+a.Train.Length+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk;
            Check(draws<=NativeBlockLooks.DrawBudget/6&&a.Parts.Count(p=>p.Shadow)<200,"dressing in "+draws+" draws (budget 60), few shadow casters ("+a.Parts.Count(p=>p.Shadow)+")");
            Check(a.Glows.Length>40&&a.Glows.All(g=>g.Size>0&&g.R>=0&&g.G>=0&&g.B>=0),"glow sources for reflections and haze ("+a.Glows.Length+")");
            // A different map's blocks get no kit.
            Check(NativeCyberCityKit.Build(new[]{new ContentBlock{id="hotel-pier-nw",pos=new V3(0,1,0),size=new V3(1,2,1)}},null).Parts.Length==0&&NativeCyberCityKit.Stem("hotel-x")==null&&NativeCyberCityKit.Stem("rubble-cybercity-billboard-n-0")=="rubble-billboard-n-0","other maps' ids get nothing");
            // Glyphs: every letter the signs use has strokes, all inside the 5 x 7 cell.
            foreach(char ch in "NOVAEGHLIXCDUPSTKBMW24ROY")Check(NativeCyberCityKit.Glyph(ch).Count>0&&NativeCyberCityKit.Glyph(ch).All(r=>r[0]>=0&&r[0]+r[2]<=5&&r[1]>=0&&r[1]+r[3]<=7),"glyph "+ch);
            // Drives: pure, steady parts unchanged, the signal shows one lamp per phase.
            var mag=draft.hazards[0];var states=new NativeHazardState[draft.hazards.Length];
            void Frame(double t){for(int i=0;i<states.Length;i++)states[i]=NativeCyberCityHazards.Evaluate(draft.hazards[i],1,10,t);}
            bool pure=true;foreach(double t in new[]{20,23.5,26.2,28.6,31.5,25.6})
            {
                Frame(t);foreach(var p in a.Parts){NativeCyberCityKit.Drive(p,states,t,out float r1,out float g1,out float b1,out float l1);NativeCyberCityKit.Drive(p,states,t,out float r2,out float g2,out float b2,out float l2);if(r1!=r2||g1!=g2||b1!=b2||l1!=l2||float.IsNaN(r1)||l1<0)pure=false;}
            }
            Check(pure,"drives are pure and finite");
            float Edge(double t){Frame(t);var p=a.Parts.First(x=>x.Drive==NativeCyberCityKit.DriveEdge);NativeCyberCityKit.Drive(p,states,t,out float r,out float g,out float bb,out float l);return r/(g+.001f);}
            Check(Edge(20)<1&&Edge(23.5)>1.5&&Edge(26.13)>8,"the Cut's edge strips: cyan at rest, amber warning, red danger");
            Frame(26.5);var coils=a.Parts.Where(x=>x.Drive==NativeCyberCityKit.DriveCoil).ToArray();float lit=0;foreach(var p in coils){NativeCyberCityKit.Drive(p,states,26.5,out _,out _,out _,out float l);lit=Math.Max(lit,l);}
            Frame(20);float rest=0;foreach(var p in coils){NativeCyberCityKit.Drive(p,states,20,out _,out _,out _,out float l);rest=Math.Max(rest,l);}
            Check(lit>1.2f&&rest<.3f,"coils race in the danger ("+lit+") and breathe at rest ("+rest+")");
            Frame(25.6);var deck=a.Parts.First(x=>x.Drive==NativeCyberCityKit.DriveGridDeck&&draft.blocks[x.Parent].pos.x<0);NativeCyberCityKit.Drive(deck,states,25.6,out _,out _,out _,out float live);Frame(20);NativeCyberCityKit.Drive(deck,states,20,out _,out _,out _,out float idle);
            Check(live>1.3f&&idle==1,"a live coupling's grating glows (window above 1), idle it does not");
        }
        private static void Train(DraftMap draft)
        {
            var k=NativeCyberCityKit.Build(draft.blocks,draft.hazards);var mag=draft.hazards[0];float top=mag.center.y+mag.size.y/2,floor=mag.center.y-mag.size.y/2;
            Check(k.Train.Length>30&&k.Train.All(p=>p.Parent==-2),"the train rig ("+k.Train.Length+" parts)");
            float maxY=k.Train.Max(p=>NativeCyberCityKit.RailTop+p.Y+p.Hy),minY=k.Train.Min(p=>NativeCyberCityKit.RailTop+p.Y-p.Hy),maxZ=k.Train.Max(p=>Math.Abs(p.Z)+p.Hz),len=-k.Train.Min(p=>p.X-p.Hx),nose=k.Train.Max(p=>p.X+p.Hx);
            Check(maxY<=top+1e-4f&&minY>=floor-1e-4f&&maxZ<=mag.size.z/2+1e-4f,"the train stays inside the kill envelope (top "+maxY+" <= "+top+", half width "+maxZ+")");
            Check(Math.Abs(len-mag.length)<.6f&&Math.Abs(nose)<.05f,"the train is the definition's length ("+len+") with its nose at the head");
            NativeCyberCityKit.TrainPlace(k.Train[0],10,-1,0,out float x,out _,out float z);Check(Math.Abs(x-(10-k.Train[0].X))<1e-4&&Math.Abs(z+k.Train[0].Z)<1e-4,"westbound trains are turned about y");
        }
        private static NativeFxLight Light(){var l=new NativeFxLight{Lx=-.4,Ly=.8,Lz=.4,Sr=.3,Sg=.33,Sb=.5};for(int i=0;i<9;i++)l.Ambient[i]=.05;return l;}
        private static void Fx(DraftMap draft)
        {
            var kit=NativeCyberCityKit.Build(draft.blocks,draft.hazards);var back=NativeCyberCityScenery.Build();var scene=NativeCyberFxScene.Build(draft.blocks,draft.hazards,kit,back);
            Check(scene.Maglev==0&&scene.Grids[0]==1&&scene.Grids[1]==2&&scene.BeaconX.Length==16&&scene.VentX.Length>=6&&scene.Screens.Length>5&&scene.AviationX.Length>10,"the FX scene: hazards, 16 alcove and exit beacons, vents, backdrop screens and beacons");
            Check(scene.Top(0,-1)>6&&scene.Top(-30,-2)<=-3.99f&&Math.Abs(scene.Top(-20,26))<1e-4&&scene.Top(16,15.5)>23&&scene.Top(60,60)==0,"roof map: covered under the station, the open Cut's floor, the street, the spire, beyond the slab");
            var fx=new NativeCyberCityFx();var fx2=new NativeCyberCityFx();var light=Light();var states=new NativeHazardState[draft.hazards.Length];var screens=new NativeCyberScreen[2];
            void Frame(NativeCyberCityFx f,double t,double q,bool low,bool atm,double ex=6.6,double ey=1.7,double ez=-2.3)
            {for(int i=0;i<states.Length;i++)states[i]=NativeCyberCityHazards.Evaluate(draft.hazards[i],1,10,t);f.Build(scene,t,ex,ey,ez,0,0,-1,0,1,0,light,q,low,atm,states,screens,0);}
            Frame(fx,28.6,1,false,true);Frame(fx2,28.6,1,false,true);
            Check(fx.Quads.Count==fx2.Quads.Count&&fx.Quads.Corners.Take(fx.Quads.Count*12).SequenceEqual(fx2.Quads.Corners.Take(fx.Quads.Count*12))&&fx.Quads.Colors.Take(fx.Quads.Count*4).SequenceEqual(fx2.Quads.Colors.Take(fx.Quads.Count*4)),"the FX are a pure function of the frame ("+fx.Quads.Count+" quads)");
            Check(fx.Quads.Count<NativeCyberCityFx.Capacity*.8&&fx.Rain>200&&fx.Telegraph>20&&fx.Cars>40,"a busy frame fits: rain "+fx.Rain+", telegraph "+fx.Telegraph+", cars "+fx.Cars);
            bool finite=true;for(int i=0;i<fx.Quads.Count*12;i++)if(float.IsNaN(fx.Quads.Corners[i])||float.IsInfinity(fx.Quads.Corners[i]))finite=false;for(int i=0;i<fx.Quads.Count;i++){float al=fx.Quads.Colors[i*4+3];if(!(al>0&&al<=1)||fx.Quads.Layers[i]>1||fx.Quads.Modes[i]>1)finite=false;}
            Check(finite,"quads are finite with a layer, a mode and an opacity");
            // Tiers: the atmosphere scales; the telegraphs never do (2.1 rule 4).
            foreach(double t in new[]{23.5,26.5,25.6,28.6})
            {
                Frame(fx,t,1,false,true);int full=fx.Telegraph,rain=fx.Rain;Frame(fx,t,.3,false,true);int reduced=fx.Telegraph,rr=fx.Rain;Frame(fx,t,1,true,true);int low=fx.Telegraph,rl=fx.Rain;
                Check(full==reduced&&full==low&&full>0&&rr<rain&&rl<rain,"at "+t+" s the telegraph keeps "+full+" quads on every tier; rain "+rain+" / "+rr+" / "+rl);
            }
            Frame(fx,28.6,1,false,false);Check(fx.Rain+fx.Telegraph+fx.Steam+fx.Reflections+fx.Pools==0&&fx.Cars>0,"behind the menu only the backdrop life is built");
            Frame(fx,20,1,false,true,-30,-2.35,0);int trench=fx.Telegraph;Check(trench>0,"the Cut's walls take their wash at rest");
            // Under the station deck no rain falls; over the open street it does.
            Frame(fx,40,1,false,true,0,1.7,0);int dry=fx.Rain;Frame(fx,40,1,false,true,-20,1.7,16);Check(fx.Rain>dry,"less rain under the concourse ("+dry+") than in the open market ("+fx.Rain+")");
            // Billboard screens: stressed glitch, falling noise, a 3 s dying flicker, then dark.
            screens[0]=new NativeCyberScreen{X=-3,Y=12.3f,Z=28.6f,Ux=7,Vy=2.7f,Nz=-1,Seed=7,State=1,T0=31,Live=true};
            int Screen(int state,double t){screens[0].State=state;for(int i=0;i<states.Length;i++)states[i]=NativeCyberCityHazards.Evaluate(draft.hazards[i],1,10,20);fx.Build(scene,t,-3,1.7,19,1,0,0,0,1,0,light,1,false,true,states,screens,1);return fx.Telegraph;}
            Check(Screen(1,30.5)>Screen(0,30.5)&&Screen(3,32)>Screen(0,32)&&Screen(3,34.5)==Screen(0,34.5),"a billboard's screen glitches under stress and dies 3 s after landing");
            // No allocations once warm (the frame builders, the drives and the hazard clock).
            Frame(fx,28.6,1,false,true);long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<20;i++){Frame(fx,20+i*.7,i%3==0?.3:1,i%5==0,true);foreach(var p in kit.Parts)NativeCyberCityKit.Drive(p,states,20+i,out _,out _,out _,out _);}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocations per frame ("+used+" bytes over 20 frames)");
        }
        private static void Backdrop()
        {
            var a=NativeCyberCityScenery.Build();var b=NativeCyberCityScenery.Build();
            Check(a.Length==b.Length&&a.Zip(b,(x,y)=>x.X==y.X&&x.Y==y.Y&&x.Z==y.Z&&x.Hx==y.Hx&&x.Hy==y.Hy&&x.Look==y.Look&&x.Color==y.Color).All(v=>v),"the backdrop is deterministic ("+a.Length+" boxes)");
            Check(a.Length<=10*NativeBlockLooks.Chunk,"the backdrop fits 10 instanced draws ("+(a.Length+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk+")");
            int enter=0,orbit=0;string firstEnter=null,firstOrbit=null;
            foreach(var x in a)
            {
                double ax=Math.Abs(x.X)-x.Hx,az=Math.Abs(x.Z)-x.Hz;bool ground=x.Y+x.Hy<=1e-3&&x.Y-x.Hy>=-.6;bool tunnel=Math.Abs(x.Z)+x.Hz<=4.3&&x.Y+x.Hy<=0.01&&Math.Abs(x.X)-x.Hx>=NativeCyberCityScenery.SlabX-1e-3;
                if(ax<NativeCyberCityScenery.InnerX-1e-3&&az<NativeCyberCityScenery.InnerZ-1e-3&&!(ground&&(ax>=NativeCyberCityScenery.SlabX-1e-3||az>=NativeCyberCityScenery.SlabZ-1e-3))&&!tunnel){enter++;firstEnter??=x.X+","+x.Y+","+x.Z;}
                if(x.Y+x.Hy>NativeCyberCityScenery.OrbitCap&&Footprint(x)<NativeCyberCityScenery.OrbitClear-2){orbit++;firstOrbit??=x.X+","+x.Y+","+x.Z+" h "+x.Hx+","+x.Hy+","+x.Hz;}
            }
            Check(enter==0,"nothing of the backdrop enters the slab (only ground plates and the tunnels meet its edge; "+enter+", first "+firstEnter+")");
            Check(orbit==0,"the menu orbit flies clear of everything over "+NativeCyberCityScenery.OrbitCap+" m ("+orbit+", first "+firstOrbit+")");
            Check(a.Count(x=>x.Kind==2)>5&&a.Count(x=>x.Kind==3)>10&&a.Max(x=>x.Y+x.Hy)>300,"screens, beacons and megatowers");
            bool cars=true;for(int l=0;l<NativeCyberCityScenery.Lanes;l++)for(int i=0;i<NativeCyberCityScenery.LaneCars[l];i++)for(double t=0;t<60;t+=7.3){NativeCyberCityScenery.Car(l,i,t,out double x,out double y,out double z,out _,out _);if(y<30||Math.Abs(x)<44&&Math.Abs(z)<36)cars=false;}
            Check(cars,"the flying traffic stays above 30 m and outside the slab");
        }
        // The footprint's nearest approach to the menu orbit, sampled 9 x 9 over the box (yaw ignored: backdrop boxes are square-on).
        private static double Footprint(NativeCyberBox b){double best=double.MaxValue;for(int i=0;i<=8;i++)for(int j=0;j<=8;j++)best=Math.Min(best,NativeCyberCityScenery.OrbitDistance(b.X-b.Hx+b.Hx*i/4,b.Z-b.Hz+b.Hz*j/4));return best;}
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            Check(R("NativeSourcePresentation.cs").Contains("CyberCityScenery(arena);")&&R("NativeBackdropPresentation.cs").Contains("CyberCityBackdrop(mapId,time,camera);")&&R("ArenaPresentation.cs").Contains("DisposeCyberCity();"),"SourceWorld, the menu backdrop and Dispose reach the Cyber City presentation");
            var p=R("NativeCyberCityPresentation.cs");
            Check(p.Contains("if(!NativeCyberCityLooks.Active(arena.MapId)||!EnsureCyberCity())return;")&&p.Contains("if(!NativeCyberCityLooks.Active(mapId)||!EnsureCyberCity())return;"),"dormant unless the map is cybercity");
            Check(!p.Contains("Collider")&&!p.Contains("new GameObject")&&!p.Contains("AddComponent")&&!p.Contains("Rigidbody"),"no colliders, objects or bodies");
            Check(p.Contains("NativeCyberCityHazards.At(metroHazards[i],match.Phase,round,match.FightAt,now)")&&p.Contains("NativeStructureFx.RoundIndex(match.Set,match.Round)")&&p.Contains("PresentationClock(arena)"),"hazard rigs read the pure Core state on the presentation clock with the round index and match phase");
            Check(p.Contains("const int layer=NativeSceneryBuilder.BackdropLayer;")&&p.Contains("MetroDraw(bt,layer,ShadowCastingMode.Off)"),"the backdrop draws on the backdrop layer and casts no shadows");
            var audio=R("NativeCyberCityAudio.cs");Check(audio.Contains("NativeCyberCityLooks.Active(arena.MapId)")&&R("NativeAudio.cs").Contains("CyberCityCues(arena,menu);"),"the sound hook is dormant off the map");
            foreach(var f in new[]{"NativeCyberCityLooks.cs","NativeCyberCityHazards.cs","NativeCyberCityKit.cs","NativeCyberCityScenery.cs","NativeCyberCityFx.cs","NativeCyberCityAmbience.cs"})Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f)).Contains("UnityEngine"),f+" is engine-free Core");
        }
    }
}
