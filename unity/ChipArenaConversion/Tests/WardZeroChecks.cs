using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The revamped Ward Zero's presentation (docs/arenas/WARDZERO_PROPOSAL.md, WARDZERO-SIMULATION-CONTRACT.md; Core
    // NativeWardZeroLooks, NativeWardZeroHazards, NativeWardZeroMood, NativeWardZeroKit, NativeWardZeroLights, NativeWardZeroFx;
    // Runtime NativeWardZeroPresentation). Against the frozen draft (Tests/wardzero-draft.json, written by
    // docs/arenas/scripts/wardzero_m1.py --json): today's Ward Zero keeps its look and stays off the revamp (the gate is the
    // map's look, which today's map does not carry); the bank slots sit beside Cyber City's and the shader's ids match; the
    // quarantine preset validates and dresses every draft block; the hazard timing is the generator's table and a pure
    // function; the mood follows the game state; the kit is deterministic, dresses every family, reads its entrances from the
    // entries (none in code), keeps rule 2.1.1 below 3 m and its budget; the lights and the effects are pure, never reduce a
    // telegraph and allocate nothing; the runtime hooks are dormant off the revamp.
    internal static class WardZeroChecks
    {
        private static int count;private static readonly List<string> facts=new List<string>();
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Ward Zero: "+what);count++;}
#pragma warning disable CS0649
        private sealed class Draft{public DraftMap[] maps;public DraftPve pve;}
        private sealed class DraftMap{public string id,name,look,ambience;public ContentBlock[] blocks;public MapHazard[] hazards;public MapBounds bounds;public MapSpawns spawns;public DraftRamp[] ramps;}
        private sealed class DraftRamp{public string id,kind;public V3 low,high;public float width,bottom;public string[] blocks;}
        private sealed class DraftPve{public NativeWardEntry[] entries;}
#pragma warning restore CS0649
        public static void Run(string root)
        {
            var o=new JsonSerializerOptions{IncludeFields=true};
            var draft=JsonSerializer.Deserialize<Draft>(File.ReadAllText(Path.Combine(root,"Tests/wardzero-draft.json")),o);var map=draft.maps.Single();var entries=draft.pve.entries;
            var set=JsonSerializer.Deserialize<MapLookSet>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/MapLooks.json")),o);
            Looks(root,set,map);Hazards(map);Mood();Kit(map,entries);Lights(map,entries);Fx(map,entries);Hooks(root);
            Console.WriteLine("PASS ward zero ("+count+" checks: today's Ward Zero keeps its look and the revamp gate stays shut on it; bank slots beside Cyber City's, shader ids match; quarantine preset, every draft block dressed; hazard timing table, purity, round parity, Fight/Result only; mood states, corruption, easing; kit deterministic per family, entrances from the entries only (breach panels, Hulk hatch), rule 2.1.1 below 3 m, budget, low tier; practical lights and FX pure, telegraphs never reduced, no allocations; runtime hooks dormant; "+string.Join(", ",facts)+")");
        }
        private static void Looks(string root,MapLookSet set,DraftMap draft)
        {
            NativeMapLooks.Validate(set);var q=NativeMapLooks.ByLook(set,NativeWardZeroLooks.Look);
            Check(q!=null&&q.id=="siren-quarantine"&&NativeMapLooks.Find(set,"siren")?.look=="ward-zero","the quarantine preset is its own entry; today's Ward Zero keeps ward-zero");
            Check(NativeBlockLooks.Dressed(q,false)&&q.lights.Length==8&&q.key.shadows=="none"&&q.fog.mode=="linear"&&q.ambience=="wardzero"&&q.blocks.haze>0,"dressed, 8 practical lights, no key shadows indoors, linear fog, its ambience and haze");
            // The gate: the map id and the look together; today's Ward Zero (GameContent.json) has no look.
            var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
            var today=content.Map("siren");Check(string.IsNullOrEmpty(today.look)&&!NativeWardZeroLooks.Active(today),"today's Ward Zero has no look, so the revamp stays off");
            Check(NativeWardZeroLooks.Active(new ContentMap{id="siren",look="quarantine"})&&!NativeWardZeroLooks.Active(new ContentMap{id="cybercity",look="quarantine"})&&!NativeWardZeroLooks.Active(new ContentMap{id="siren",look="ward-zero"})&&!NativeWardZeroLooks.Active(null),"Active needs the id siren and the look quarantine");
            Check(draft.id=="siren"&&draft.name=="Ward Zero"&&draft.look==NativeWardZeroLooks.Look,"the draft replaces Ward Zero under its id and name and carries the look");
            Check(!File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")).Contains("\"quarantine\""),"the draft is not in UnityMaps.json (no rotation change)");
            // The bank: Ward Zero's slots are the ones Cyber City leaves free; ids past every base key.
            for(int i=0;i<16;i++)
            {
                var k=NativeWardZeroLooks.Keys[i];if(k==null){Check(NativeWardZeroLooks.Palette[i]==-1,"unused ward slot "+i);continue;}
                Check(NativeCyberCityLooks.Keys[i]==null&&NativeBlockLooks.Id(k)==NativeCyberCityLooks.Offset+i&&NativeWardZeroLooks.Is(NativeBlockLooks.Id(k))&&Array.IndexOf(NativeBlockLooks.Keys,k)<0&&NativeWardZeroLooks.Palette[i]>=0,"ward key "+k+" in a free bank slot");
                Check(i!=6,"ward slot "+k+" avoids the timber id");
            }
            for(int i=0;i<NativeCyberCityLooks.Slots;i++){var k=NativeCyberCityLooks.Keys[i];if(k!=null)Check(NativeBlockLooks.Id(k)==NativeCyberCityLooks.Offset+i,"Cyber City key "+k+" keeps its id");}
            for(int i=0;i<NativeBlockLooks.Keys.Length;i++)Check(NativeBlockLooks.Id(NativeBlockLooks.Keys[i])==i,"base key "+NativeBlockLooks.Keys[i]+" keeps its id");
            var cy=NativeMapLooks.Find(set,"cybercity");foreach(var e in cy.blockLooks){var ch=NativeBlockLooks.Resolve(cy.blockLooks,null,e.block.Replace("*","x"),"stone",0);Check(NativeWardZeroLooks.Keys[Math.Max(0,Math.Min(15,ch.Look))]==null||(ch.Flags&NativeCyberCityLooks.Bank)==0,"Cyber City's entries never land on a ward slot ("+e.block+")");}
            string shader=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Resources/NativeBlockLit.shader"));
            foreach(var (n,v) in new[]{("TILE",NativeWardZeroLooks.Tile),("BURN",NativeWardZeroLooks.Burn),("PANEL",NativeWardZeroLooks.Panel),("GLASS",NativeWardZeroLooks.Glass),("LIGHT",NativeWardZeroLooks.Light),("INK",NativeWardZeroLooks.Ink)})
            {var m=Regex.Match(shader,@"#define WZ_"+n+@" ([0-9.]+)\r?\n");Check(m.Success&&float.Parse(m.Groups[1].Value,System.Globalization.CultureInfo.InvariantCulture)==v,"WZ_"+n+" = "+v);}
            int shared0=shader.IndexOf("// SHARED BEGIN",StringComparison.Ordinal),shared1=shader.IndexOf("// SHARED END",StringComparison.Ordinal),wz=shader.IndexOf("WZ_TILE)",StringComparison.Ordinal);
            Check(wz>shared0&&wz<shared1,"the ward looks are in the SHARED section the offline harness runs");
            // Every draft block resolves through a Ward Zero id entry (not a material fallback) to a known look.
            var looks=new HashSet<string>();
            foreach(var b in draft.blocks)
            {
                var best=q.blockLooks.Where(e=>!e.block.StartsWith("material:",StringComparison.Ordinal)).Select(e=>NativeBlockLooks.Score(e.block,b.id,b.material)).DefaultIfEmpty(-1).Max();
                Check(best>=1,"draft block "+b.id+" resolves through a Ward Zero id entry");var ch=NativeBlockLooks.Resolve(q.blockLooks,null,b.id,b.material,0);var key=NativeWardZeroLooks.KeyOf(ch.Look,ch.Flags);
                Check(ch.Listed&&key!=null,"draft block "+b.id+" has a known look");looks.Add(key);
            }
            Check(draft.blocks.Length>500&&looks.Contains("ward-tile")&&looks.Contains("ward-panel")&&looks.Contains("ward-glass")&&looks.Contains("cyber-grate")&&looks.Contains("cyber-metal"),"all "+draft.blocks.Length+" draft blocks dressed ("+looks.Count+" looks)");
        }
        private static NativeHazardState At(MapHazard h,int round,double t)=>NativeWardZeroHazards.Evaluate(h,round,10,t);
        private static MapHazard H(DraftMap d,string id)=>d.hazards.Single(h=>h.id==id);
        private static void Hazards(DraftMap draft)
        {
            var d=NativeWardZeroHazards.Draft();Check(d.Length==draft.hazards.Length,"Draft() lists the blockout's hazards");
            for(int i=0;i<d.Length;i++)
            {
                var a=d[i];var b=draft.hazards[i];
                Check(a.id==b.id&&a.kind==b.kind&&a.first==b.first&&a.period==b.period&&a.offset==b.offset&&a.warn==b.warn&&a.charge==b.charge&&a.live==b.live&&a.dwell==b.dwell&&a.travel==b.travel&&a.rise==b.rise&&a.push==b.push&&a.tick==b.tick&&a.damage==b.damage&&a.lethal==b.lethal,"Draft() "+a.id+" equals the blockout's definition");
                Check(Math.Abs(a.center.x-b.center.x)<1e-4&&Math.Abs(a.center.y-b.center.y)<1e-4&&Math.Abs(a.center.z-b.center.z)<1e-4&&Math.Abs(a.size.x-b.size.x)<1e-4&&Math.Abs(a.size.y-b.size.y)<1e-4&&Math.Abs(a.size.z-b.size.z)<1e-4&&(a.zones?.Length??0)==(b.zones?.Length??0),"Draft() "+a.id+" volume and zones");
                for(int z=0;z<(a.zones?.Length??0);z++)Check(Math.Abs(a.zones[z].center.z-b.zones[z].center.z)<1e-4&&Math.Abs(a.zones[z].size.x-b.zones[z].size.x)<1e-4,"Draft() "+a.id+" zone "+z);
            }
            Check(d.Count(h=>h.kind==NativeWardZeroHazards.Burner)==4&&d.Count(h=>h.kind==NativeWardZeroHazards.Gas)==2&&d.Count(h=>h.kind==NativeWardZeroHazards.Grid)==2&&d.Count(h=>h.kind==NativeWardZeroHazards.Shutter)==4,"four burners, two decon locks, two bridges, four shutters");
            // The generator's timing table (round 1, fight at 10 s); the managed clock must print the same.
            void Table(string id,params (double t,NativeHazardPhase p)[] rows){var h=H(draft,id);foreach(var (t,p) in rows){var s=At(h,1,t);Check(s.Phase==p,id+" at "+t+" s is "+p+" (got "+s.Phase+")");}}
            const NativeHazardPhase I=NativeHazardPhase.Idle,W=NativeHazardPhase.Warn,D=NativeHazardPhase.Danger,A=NativeHazardPhase.Active,C=NativeHazardPhase.Clear;
            Table("siren-burner-e-a",(10.1,I),(15.9,I),(16.1,W),(17.4,W),(17.6,A),(19.9,A),(20.1,C),(20.9,C),(21.1,I),(26.1,W),(27.6,A),(30.1,C));
            Table("siren-burner-e-b",(20.9,I),(21.1,W),(22.6,A),(25.1,C),(26.1,I),(31.1,W),(32.6,A));
            Table("siren-gas-nw",(24.9,I),(25.1,W),(27.6,A),(31.4,A),(31.6,C),(34.4,C),(34.6,I),(49.1,W),(51.6,A));
            Table("siren-grid-e",(28.4,I),(28.6,W),(31.1,A),(32.9,A),(33.1,C),(34.1,I),(44.6,W),(47.1,A));
            Table("siren-shutter-n",(29.9,I),(30.1,W),(33.1,D),(34.1,D),(34.3,A),(43.1,A),(43.3,C),(44.6,C),(44.8,I),(66.1,W),(69.1,D));
            Table("siren-shutter-e",(47.9,I),(48.1,W),(51.1,D),(52.3,A),(61.3,C),(62.8,I),(84.1,W),(87.1,D));
            // Round parity: on even rounds each hazard moves by its offset (the burner pairs and the shutter axes swap).
            Check(At(H(draft,"siren-burner-e-a"),2,16.1).Phase==I&&At(H(draft,"siren-burner-e-a"),2,21.1).Phase==W&&At(H(draft,"siren-burner-e-b"),2,16.1).Phase==W,"even rounds shift the burners by their offsets");
            Check(At(H(draft,"siren-shutter-n"),2,30.1).Phase==I&&At(H(draft,"siren-shutter-n"),2,48.1).Phase==W&&At(H(draft,"siren-shutter-e"),2,30.1).Phase==W,"even rounds swap which shutter axis closes first");
            // Level: the warning builds, the shutter's closure rises through the travel and falls through the rise.
            var sn=H(draft,"siren-shutter-n");Check(Math.Abs(NativeWardZeroHazards.Closure(At(sn,1,33.6))-.5f)<.06f&&NativeWardZeroHazards.Closure(At(sn,1,40))==1&&NativeWardZeroHazards.Closure(At(sn,1,25))==0&&NativeWardZeroHazards.Closure(At(sn,1,44.7))<.05f,"shutter closure: halfway mid-travel, shut through the dwell, open at rest");
            var be=H(draft,"siren-burner-e-a");Check(NativeWardZeroHazards.Tick(be,At(be,1,17.6))==0&&NativeWardZeroHazards.Tick(be,At(be,1,18.3))==3&&NativeWardZeroHazards.Tick(be,At(be,1,16.5))==-1,"burn ticks every .25 s while active, none outside");
            Check(Math.Abs(NativeWardZeroHazards.Lead(At(be,1,12))-4)<1e-9&&Math.Abs(NativeWardZeroHazards.Lead(At(be,1,21.5))-4.5)<1e-9&&At(be,1,16.5).Telegraph,"Idle's lead counts down to the next warning (before the first too); the warning telegraphs");
            Check(NativeWardZeroHazards.At(be,NativePhase.Fight,1,10,17.6).Phase==A&&NativeWardZeroHazards.At(be,NativePhase.Result,1,10,17.6).Phase==A&&NativeWardZeroHazards.At(be,NativePhase.Countdown,1,10,17.6).Phase==I&&NativeWardZeroHazards.At(be,NativePhase.Draft,1,10,17.6).Phase==I,"hazards run in Fight and Result only");
            Check(At(be,1,9).Phase==I&&NativeWardZeroHazards.Evaluate(null,1,10,30).Phase==I&&NativeWardZeroHazards.Evaluate(new MapHazard{kind="laser"},1,10,30).Phase==I,"idle before the fight, without a definition and for unknown kinds");
            bool same=true;for(double t=0;t<160;t+=.37)foreach(var h in d){var x=At(h,1,t);var y=At(h,1,t);if(x.Phase!=y.Phase||x.Cycle!=y.Cycle||x.Level!=y.Level||x.Since!=y.Since)same=false;}
            Check(same,"hazard states are a pure function of (round, FightAt, time)");
            // Fairness the generator checks too: the two bridges are never live together; at most two of four portals shut.
            int both=0,shut=0;var ge=H(draft,"siren-grid-e");var gw=H(draft,"siren-grid-w");var portals=d.Where(h=>h.kind==NativeWardZeroHazards.Shutter).ToArray();
            for(int r=1;r<=2;r++)for(double t=10;t<250;t+=.1){if(At(ge,r,t).Phase==A&&At(gw,r,t).Phase==A)both++;shut=Math.Max(shut,portals.Count(p=>NativeWardZeroHazards.Closure(At(p,r,t))>0));}
            Check(both==0&&shut<=2,"bridges never live together ("+both+"), at most "+shut+" of 4 portals shut");
        }
        private static void Mood()
        {
            NativeWardMood T(NativeWardMoodInput i)=>NativeWardZeroMood.Target(i);
            var pvp=T(new NativeWardMoodInput{Phase=NativePhase.Fight,FightAge=10});Check(pvp.Calm==1&&pvp.Fluor==1&&pvp.Strobe==0,"PvP rests calm, full clinical white");
            var wave=T(new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="wave",Wave=2});Check(wave.Horde==1&&wave.Fluor<1&&wave.Flicker>pvp.Flicker,"a wave is the horde state: dimmer, more failing tubes");
            var boss=T(new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="boss",BossPhase=2});Check(boss.Boss==1&&boss.Ink>=.5f,"the Siren's fight is the boss state, the ink at half by phase 2");
            var p3=T(new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="boss",BossPhase=3});var hid=T(new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="boss",BossPhase=2,Hidden=true});
            var lost=T(new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="boss",Holding=true,Cinematic="defeat"});var won=T(new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="ended",Holding=true,Cinematic="victory"});
            Check(p3.Emergency==1&&hid.Emergency==1&&lost.Emergency==1&&won.Calm==1&&p3.Strobe==1&&p3.Red>.5f,"phases 3-4, the possession hunt and a defeat are the emergency; a victory is calm");
            Check(hid.Ink>boss.Ink&&NativeWardZeroMood.Corruption(new NativeWardMoodInput{Pve=true,Stage="wave",Wave=3})>NativeWardZeroMood.Corruption(new NativeWardMoodInput{Pve=true,Stage="wave",Wave=1}),"the ink spreads with the waves and more while she hides");
            float c0=NativeWardZeroMood.Corruption(new NativeWardMoodInput{Phase=NativePhase.Fight,FightAge=0}),c1=NativeWardZeroMood.Corruption(new NativeWardMoodInput{Phase=NativePhase.Fight,FightAge=150}),c2=NativeWardZeroMood.Corruption(new NativeWardMoodInput{Phase=NativePhase.Fight,FightAge=600});
            Check(Math.Abs(c0-.12f)<1e-5&&Math.Abs(c1-.5f)<1e-5&&c2==c1,"in PvP the ink creeps from .12 to half over 150 s");
            var alarm=T(new NativeWardMoodInput{Phase=NativePhase.Fight,FightAge=10,Alarm=1});Check(Math.Abs(alarm.Emergency-.55f)<1e-5&&alarm.Calm>.4f,"a hazard alarm takes over a share, never the whole room");
            foreach(var m in new[]{pvp,wave,boss,p3,hid,lost,won,alarm})Check(Math.Abs(m.Calm+m.Horde+m.Boss+m.Emergency-1)<1e-5,"state weights sum to 1");
            var song=new NativeWardMoodInput{Phase=NativePhase.Fight,Pve=true,Stage="boss",BossPhase=2,Song=true,Section="climax",BeatInBar=1,BeatPhase=0};var on=T(song);song.BeatPhase=.9;var off=T(song);
            Check(on.Beat>.9f&&on.Down>.9f&&off.Beat<.01f&&on.Groove==1,"the climax pumps the lights on the beat and the downbeat");
            song.Section="breather";song.BeatPhase=0;Check(T(song).Beat<.2f,"a breather calms the pulse");
            var e=pvp;for(int i=0;i<300;i++)NativeWardZeroMood.Ease(ref e,p3,1f/60);Check(Math.Abs(e.Emergency-p3.Emergency)<.02f&&Math.Abs(e.Fluor-p3.Fluor)<.02f,"easing reaches the target in a few seconds");
            var e2=pvp;NativeWardZeroMood.Ease(ref e2,p3,1f/60);Check(e2.Emergency>0&&e2.Emergency<.1f,"and never jumps in one frame");
            var calm=NativeWardZeroMood.Preset("calm");var emer=NativeWardZeroMood.Preset("emergency");Check(calm.Calm>.99f&&NativeWardZeroMood.Preset("horde").Horde>.99f&&NativeWardZeroMood.Preset("boss").Boss>.99f&&emer.Emergency>.99f,"the four named presets");
        }
        private static void Kit(DraftMap draft,NativeWardEntry[] entries)
        {
            var a=NativeWardZeroKit.Build(draft.blocks,draft.hazards,entries);var b=NativeWardZeroKit.Build(draft.blocks,draft.hazards,entries);
            Check(a.Parts.Length==b.Parts.Length&&a.Parts.Zip(b.Parts,(x,y)=>x.Parent==y.Parent&&x.X==y.X&&x.Y==y.Y&&x.Z==y.Z&&x.Hx==y.Hx&&x.Hy==y.Hy&&x.Look==y.Look&&x.Color==y.Color&&x.Drive==y.Drive&&x.Entry==y.Entry&&x.Detail==y.Detail).All(v=>v)&&a.Glows.Length==b.Glows.Length,"the kit is deterministic ("+a.Parts.Length+" parts, "+a.Glows.Length+" glows)");
            Check(a.Parts.All(p=>p.Parent>=0&&p.Parent<draft.blocks.Length&&p.Hx>0&&p.Hy>0&&p.Hz>0&&NativeWardZeroLooks.KeyOf(p.Look,p.Flags)!=null&&p.Gain>0),"parts have parents, sizes, known looks");
            Check(a.Grids[0]>=0&&a.Grids[1]>=0&&draft.hazards[a.Grids[0]].center.x<0&&draft.hazards[a.Grids[1]].center.x>0&&a.Burners.Length==4&&a.Gases.Length==2&&a.Shutters.Length==4,"the kit finds both bridges, four burners, two locks, four shutters");
            Check(a.Routes.Length==8&&a.Routes.Count(r=>r.Stair)==6&&a.Routes.All(r=>Math.Abs(r.TopY-r.FootY-6)<.05f&&r.Width>=2.5f),"eight routes between the floors found from the blocks (six stairs, two ramps), 6 m each");
            foreach(var r in draft.ramps){var k=a.Routes.FirstOrDefault(x=>x.Id==r.id||x.Id==r.id+"-deck");Check(k.Id!=null&&Math.Abs(k.FootX-r.low.x)<.6f&&Math.Abs(k.FootZ-r.low.z)<.6f&&Math.Abs(k.TopX-r.high.x)<.6f&&Math.Abs(k.TopZ-r.high.z)<.6f,"the kit's route "+r.id+" matches the draft's collision ramp");}
            var stems=a.Parts.Select(p=>NativeWardZeroKit.Stem(draft.blocks[p.Parent].id)).ToArray();
            foreach(var fam in new[]{"slab","vault","deck-n","deck-s","gallery-n","gallery-e","shell-n","shell-e","atrium-wall-","atrium-upper-e","hall-n-wall","concourse-n-wall","wing-wall-ne","wing-tall-ne","cells-n-row","obs-n-row","cells-n-station","stair-atrium-e-","stair-ne-","ramp-e-deck","ramp-e-skirt-","bridge-e-deck","gantry-e-deck","gallery-n-rail","lab-e-tank-","lab-e-bench-","lane-e-furnace","lane-e-wall-","decon-nw-","morgue-nw-","intake-ne-","control-ne-","breach-cells-n","breach-gantry-e","cradle-shard-"})
                Check(stems.Any(s=>s!=null&&s.StartsWith(fam,StringComparison.Ordinal)),"the "+fam+" family is dressed");
            // Entrances come from the entries alone: without them no part or glow is tagged; every entry dresses something.
            var none=NativeWardZeroKit.Build(draft.blocks,draft.hazards,null);
            Check(none.Parts.All(p=>p.Entry<0)&&none.Glows.All(g=>g.Entry<0)&&none.Drops.Length==0&&none.Breaches.Length==0,"without entries nothing is an entrance (no coordinates in code)");
            for(int i=0;i<entries.Length;i++)Check(a.Parts.Any(p=>p.Entry==i),"entry "+entries[i].id+" ("+entries[i].kind+"/"+entries[i].Surface+") is dressed");
            Check(a.Breaches.Length==8&&a.Breaches.All(e=>entries[e].kind=="breach")&&a.Drops.Length==3,"eight breach panels and three drop hatches");
            foreach(int e in a.Breaches)
            {
                var en=entries[e];int blk=Array.FindIndex(draft.blocks,x=>x.id==en.block);var mine=a.Parts.Where(p=>p.Entry==e).ToArray();
                Check(blk>=0&&mine.All(p=>p.Parent==blk)&&mine.Length>=10&&mine.Any(p=>p.Drive==NativeWardZeroKit.DriveBreach),"breach "+en.id+": its panel block, framed, braced and lamped");
                var pb=draft.blocks[blk];Check(Math.Abs(pb.pos.x-en.center.x)<1e-3&&Math.Abs(pb.pos.z-en.center.z)<1e-3&&Math.Abs(pb.size.y-en.size.y)<1e-3&&pb.size.y>=3.4f&&Math.Max(pb.size.x,pb.size.z)>=3.2f,"breach "+en.id+" panel is the entry's box and fits the Hulk");
                // The dressing sits on the panel's play face (the side the normal points to).
                float face=Math.Abs(en.normal.x)>.5f?en.normal.x*pb.size.x/2:en.normal.z*pb.size.z/2;Check(mine.All(p=>(Math.Abs(en.normal.x)>.5f?p.X*Math.Sign(en.normal.x):p.Z*Math.Sign(en.normal.z))>=Math.Abs(face)-1e-3f),"breach "+en.id+" dressing faces the play space");
            }
            int cradle=Array.FindIndex(entries,x=>x.kind=="drop"&&x.hulk);Check(cradle>=0&&entries[cradle].clear>=4.5f&&entries[cradle].height>=12,"the Cradle drop takes the Hulk: 4.5 m clear landing from the roof");
            var hatch=a.Parts.Where(p=>p.Entry==cradle).ToArray();float span=hatch.Max(p=>p.X+p.Hx)-hatch.Min(p=>p.X-p.Hx);Check(hatch.Length>4&&span>=5.2f,"the Spine hatch opens 5.2 m or more ("+span+")");
            // A moved entry moves its rig (the kit reads the data, not a table of places).
            var moved=entries.Select(x=>x).ToArray();int vent=Array.FindIndex(entries,x=>x.kind=="vent"&&x.Surface=="floor"&&x.level==0);var src=entries[vent];
            var copy=new NativeWardEntry{e=src.e,level=src.level,song=src.song,id=src.id,kind=src.kind,surface=src.surface,yaw=src.yaw,pit=src.pit,from=new NativeWardPoint{x=src.from.x+2,y=src.from.y,z=src.from.z},exit=new NativeWardPoint{x=src.exit.x+2,y=src.exit.y,z=src.exit.z}};moved[vent]=copy;var m=NativeWardZeroKit.Build(draft.blocks,draft.hazards,moved);
            float Cx(NativeWardZeroKit k)=>k.Parts.Where(p=>p.Entry==vent).Average(p=>draft.blocks[p.Parent].pos.x+p.X);Check(Math.Abs(Cx(m)-Cx(a)-2)<.05f,"moving a vent's data moves its grate");
            // Section 2.1 rule 1: below 3 m a part stays within .25 m of its block, unless it is floor paint or thin.
            int bad=0;string first=null;
            for(int i=0;i<a.Parts.Length;i++)
            {
                var p=a.Parts[i];var bl=draft.blocks[p.Parent];if(!(bl.rotation==null||bl.rotation.w>.9999f))continue;float wy=bl.pos.y+p.Y;if(wy-p.Hy>=3)continue;
                float ox=Math.Max(0,Math.Abs(p.X)+p.Hx-bl.size.x/2),oy=Math.Max(0,Math.Abs(p.Y)+p.Hy-bl.size.y/2),oz=Math.Max(0,Math.Abs(p.Z)+p.Hz-bl.size.z/2);
                bool paint=p.Hy*2<=.06f&&wy-p.Hy<=bl.pos.y+bl.size.y/2+.07f,thin=Math.Max(p.Hx,p.Hz)*2<=.35f||Math.Min(p.Hx,p.Hz)*2<=.08f&&p.Hy*2<=.08f;
                // A shutter slat is the shutter hazard's own body (its volume collides while shut), checked against that volume below.
                if(p.Drive==NativeWardZeroKit.DriveSlat)continue;
                if(Math.Max(ox,Math.Max(oy,oz))>.25f+1e-4f&&!paint&&!thin){bad++;first??=bl.id+" part "+i+" ("+ox+","+oy+","+oz+")";}
            }
            Check(bad==0,"rule 2.1.1: nothing below 3 m stands out more than .25 m ("+bad+", first "+first+")");
            // The slats close inside their shutter's volume (the portal), never outside it.
            int outside=0;foreach(var p in a.Parts.Where(x=>x.Drive==NativeWardZeroKit.DriveSlat)){var h=draft.hazards[p.Hazard];var bl=draft.blocks[p.Parent];float x=bl.pos.x+p.X,y=bl.pos.y+p.Y,z=bl.pos.z+p.Z;
                if(h.kind!=NativeWardZeroHazards.Shutter||Math.Abs(x-h.center.x)>h.size.x/2+.3f||Math.Abs(z-h.center.z)>h.size.z/2+.3f||y-p.Hy<h.center.y-h.size.y/2-.05f||y+p.Hy>h.center.y+h.size.y/2+1.2f)outside++;}
            Check(outside==0,"shutter slats close inside their portal's hazard volume ("+outside+" outside)");
            int draws=(a.Parts.Length+NativeBlockLooks.Chunk-1)/NativeBlockLooks.Chunk,low=a.Parts.Count(p=>!p.Detail);
            Check(draws<=NativeBlockLooks.DrawBudget/3&&a.Parts.Length<9000,"dressing in "+draws+" draws (budget "+NativeBlockLooks.DrawBudget+")");
            Check(low<a.Parts.Length*.9&&low>a.Parts.Length*.4,"the low tier leaves the detail out ("+low+" of "+a.Parts.Length+")");
            facts.Add(a.Parts.Length+" kit parts in "+draws+" draws ("+low+" on the low tier), "+a.Glows.Length+" glows");
            Check(a.Parts.Where(p=>p.Drive==NativeWardZeroKit.DriveRoute||p.Drive==NativeWardZeroKit.DriveRouteDown||p.Drive==NativeWardZeroKit.DriveGridBar||p.Drive==NativeWardZeroKit.DriveGridLamp||p.Drive==NativeWardZeroKit.DriveShutterLamp||p.Drive==NativeWardZeroKit.DriveBurner||p.Drive==NativeWardZeroKit.DriveGas||p.Drive==NativeWardZeroKit.DriveEntryLamp||p.Drive==NativeWardZeroKit.DriveSlat).All(p=>!p.Detail),"route guides, hazard rigs and entrance lamps stay on the low tier");
            Check(a.Parts.Count(p=>p.Drive==NativeWardZeroKit.DriveSlat)==48&&a.Parts.Count(p=>p.Drive==NativeWardZeroKit.DriveRoute)>=48&&a.Parts.Count(p=>p.Drive==NativeWardZeroKit.DriveRouteDown)>0&&a.Parts.Count(p=>p.Drive==NativeWardZeroKit.DriveBurner)>=16,"48 shutter slats, nosings on every stair, down marks, burner grates");
            Check(a.Glows.Length>100&&a.Glows.All(g=>g.Size>0&&g.R>=0&&g.G>=0&&g.B>=0)&&a.Glows.Count(g=>g.Kind==NativeWardZeroKit.GlowStrobe)>=8&&a.Glows.Any(g=>g.Kind==NativeWardZeroKit.GlowSpine)&&a.Glows.Count(g=>g.Kind==NativeWardZeroKit.GlowFluor&&g.Floor>3)>20,"glows on both floors: troffers, beacons, the Spine");
            Check(NativeWardZeroKit.Build(new[]{new ContentBlock{id="hotel-pier-nw",pos=new V3(0,1,0),size=new V3(1,2,1)}},null,null).Parts.Length==0&&NativeWardZeroKit.Stem("cybercity-x")==null,"other maps' ids get nothing");
            // Drives and moves: pure and finite; doors follow the open amount, slats the closure, ink the corruption.
            var states=new NativeHazardState[draft.hazards.Length];var open=new float[entries.Length];
            void Frame(double t,float o){for(int i=0;i<states.Length;i++)states[i]=NativeWardZeroHazards.Evaluate(draft.hazards[i],1,10,t);for(int i=0;i<open.Length;i++)open[i]=o;}
            bool pure=true;foreach(var (t,mood) in new[]{(12.0,"calm"),(17.6,"horde"),(31.1,"boss"),(34.5,"emergency"),(48.5,"calm")})
            {
                Frame(t,.5f);var mm=NativeWardZeroMood.Preset(mood);
                foreach(var p in a.Parts){NativeWardZeroKit.Drive(p,states,open,mm,t,out float r1,out float g1,out float b1,out float l1);NativeWardZeroKit.Drive(p,states,open,mm,t,out float r2,out float g2,out float b2,out float l2);float f1=NativeWardZeroKit.Move(p,states,open,mm,out bool h1),f2=NativeWardZeroKit.Move(p,states,open,mm,out bool h2);
                    if(r1!=r2||g1!=g2||b1!=b2||l1!=l2||f1!=f2||h1!=h2||float.IsNaN(r1)||l1<0||f1<0||f1>1)pure=false;}
            }
            Check(pure,"drives and moves are pure and finite");
            var leaf=a.Parts.First(p=>p.Drive==NativeWardZeroKit.DriveLeaf);var calm=NativeWardZeroMood.Preset("calm");Frame(12,0);float shut=NativeWardZeroKit.Move(leaf,states,open,calm,out _);Frame(12,1);
            Check(shut==0&&NativeWardZeroKit.Move(leaf,states,open,calm,out _)==1&&(leaf.Mx!=0||leaf.My!=0||leaf.Mz!=0),"an entrance leaf moves with its entry's open amount");
            var slat=a.Parts.Where(p=>p.Drive==NativeWardZeroKit.DriveSlat&&draft.hazards[p.Hazard].id=="siren-shutter-n").OrderBy(p=>p.Y).First();Frame(25,0);NativeWardZeroKit.Move(slat,states,open,calm,out bool up);Frame(40,0);NativeWardZeroKit.Move(slat,states,open,calm,out bool down);
            Check(up&&!down,"the lowest shutter slat is up in its lintel at rest and down while shut");
            var ink=a.Parts.Where(p=>p.Drive==NativeWardZeroKit.DriveInk).ToArray();int Shown(NativeWardMood mm)=>ink.Count(p=>{NativeWardZeroKit.Move(p,states,open,mm,out bool h);return !h;});
            var lo=calm;lo.Ink=.1f;var hi=calm;hi.Ink=.9f;Check(ink.Length>100&&Shown(lo)<Shown(hi)&&Shown(hi)>ink.Length*.8,"the ink spreads with the corruption ("+Shown(lo)+" -> "+Shown(hi)+" of "+ink.Length+")");
            foreach(char ch in "MORGUECLLSOBINTAKFDNRWHZ0")Check(NativeCyberCityKit.Glyph(ch).Count>0,"glyph "+ch);
        }
        private static void Lights(DraftMap draft,NativeWardEntry[] entries)
        {
            var kit=NativeWardZeroKit.Build(draft.blocks,draft.hazards,entries);var a=NativeWardZeroLights.Build(kit);var b=NativeWardZeroLights.Build(kit);
            facts.Add(a.Candidates.Length+" light candidates");
            Check(a.Candidates.Length>30&&a.Candidates.Length==b.Candidates.Length&&a.Candidates.Zip(b.Candidates,(x,y)=>x.X==y.X&&x.Y==y.Y&&x.Z==y.Z&&x.Kind==y.Kind&&x.Range==y.Range).All(v=>v),"candidates are deterministic ("+a.Candidates.Length+")");
            Check(a.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowFluor&&c.Y<6)&&a.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowFluor&&c.Y>7)&&a.Candidates.Any(c=>c.Kind==NativeWardZeroKit.GlowStrobe&&c.Alarm>0),"light on both floors and the hazard alarms");
            var states=new NativeHazardState[draft.hazards.Length];for(int i=0;i<states.Length;i++)states[i]=NativeWardZeroHazards.Evaluate(draft.hazards[i],1,10,40);var calm=NativeWardZeroMood.Preset("calm");var emer=NativeWardZeroMood.Preset("emergency");
            a.Settle(0,1.7,10,calm,states,40);b.Settle(0,1.7,10,calm,states,40);
            Check(a.Pos.SequenceEqual(b.Pos)&&a.Rgb.SequenceEqual(b.Rgb)&&a.Slot.All(s=>s>=0),"the pick is a pure function of camera, mood, states and clock; all eight slots in use");
            Check(Enumerable.Range(0,8).All(s=>{var c=a.Candidates[a.Slot[s]];return Math.Abs(c.X)<30&&Math.Abs(c.Z-10)<30;}),"the slots go to lights near the camera");
            double Sum(NativeWardZeroLights l,int k){double s=0;for(int i=0;i<8;i++)s+=l.Rgb[i*3+k];return s;}
            a.Settle(0,1.7,10,emer,states,40.1);double redE=Sum(a,0)/(Sum(a,1)+1e-6);a.Settle(0,1.7,10,calm,states,40.1);double redC=Sum(a,0)/(Sum(a,1)+1e-6);Check(redE>redC,"the emergency reads redder ("+redE.ToString("F2")+" vs "+redC.ToString("F2")+")");
            a.Settle(0,7.7,18,calm,states,40);int upper=Enumerable.Range(0,8).Count(s=>a.Candidates[a.Slot[s]].Y>7);Check(upper>=4,"on the upper floor most slots are upper-floor lights ("+upper+")");
            // Easing: a moving camera changes slots without popping (no slot changes weight by more than the rate a frame).
            a.Settle(0,1.7,10,calm,states,40);var w=(float[])a.Weight.Clone();a.Update(20,1.7,-10,calm,states,40.016,1f/60);bool smooth=true;for(int i=0;i<8;i++)if(Math.Abs(a.Weight[i]-w[i])>4f/60+1e-4f&&w[i]>0&&a.Weight[i]>0)smooth=false;Check(smooth,"slots ease in and out");
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<60;i++)a.Update(Math.Sin(i*.1)*30,1.7,Math.Cos(i*.1)*20,i%2==0?calm:emer,states,40+i/60.0,1f/60);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"the light picker allocates nothing ("+used+" bytes over 60 frames)");
        }
        private static NativeFxLight Light(){var l=new NativeFxLight{Lx=-.2,Ly=.95,Lz=.2,Sr=.05,Sg=.05,Sb=.06};for(int i=0;i<9;i++)l.Ambient[i]=.04;return l;}
        private static void Fx(DraftMap draft,NativeWardEntry[] entries)
        {
            var kit=NativeWardZeroKit.Build(draft.blocks,draft.hazards,entries);var scene=NativeWardFxScene.Build(draft.blocks,draft.hazards,kit,entries);
            Check(scene.VentX.Length==10&&scene.VentSong.Count(s=>s>=0)==8&&scene.DoorEntry.Length==28&&scene.DropEntry.Length==3&&scene.Routes.Length==8&&scene.SteamX.Length==16,"the FX scene: 10 floor vents (8 song), 28 doors, cells and grilles, 3 drops, 8 routes, steam leaks");
            var fx=new NativeWardZeroFx();var fx2=new NativeWardZeroFx();var light=Light();var states=new NativeHazardState[draft.hazards.Length];var open=new float[entries.Length];var song=new bool[2];var warn=new float[2];
            void Frame(NativeWardZeroFx f,double t,double q,bool low,string mood,double ex,double ey,double ez,float o=0)
            {for(int i=0;i<states.Length;i++)states[i]=NativeWardZeroHazards.Evaluate(draft.hazards[i],1,10,t);for(int i=0;i<open.Length;i++)open[i]=o;f.Build(scene,t,ex,ey,ez,1,0,0,0,1,0,light,q,low,NativeWardZeroMood.Preset(mood),states,open,song,warn);}
            Frame(fx,17.6,1,false,"horde",24,1.7,0);Frame(fx2,17.6,1,false,"horde",24,1.7,0);
            Check(fx.Quads.Count==fx2.Quads.Count&&fx.Quads.Corners.Take(fx.Quads.Count*12).SequenceEqual(fx2.Quads.Corners.Take(fx.Quads.Count*12))&&fx.Quads.Colors.Take(fx.Quads.Count*4).SequenceEqual(fx2.Quads.Colors.Take(fx.Quads.Count*4)),"the FX are a pure function of the frame ("+fx.Quads.Count+" quads)");
            facts.Add("a busy frame "+fx.Quads.Count+" of "+NativeWardZeroFx.Capacity+" quads");
            Check(fx.Quads.Count<NativeWardZeroFx.Capacity*.8&&fx.Telegraph>20&&fx.Haze>0&&fx.Halos>0,"a busy frame fits: telegraph "+fx.Telegraph+", haze "+fx.Haze+", halos "+fx.Halos+" ("+fx.Quads.Count+" of "+NativeWardZeroFx.Capacity+")");
            bool finite=true;for(int i=0;i<fx.Quads.Count*12;i++)if(float.IsNaN(fx.Quads.Corners[i])||float.IsInfinity(fx.Quads.Corners[i]))finite=false;for(int i=0;i<fx.Quads.Count;i++){float al=fx.Quads.Colors[i*4+3];if(!(al>0&&al<=1)||fx.Quads.Layers[i]>1||fx.Quads.Modes[i]>1)finite=false;}
            Check(finite,"quads are finite with a layer, a mode and an opacity");
            // Tiers: light and life scale; telegraphs, entrances and route guides never do (section 2.1 rule 4).
            foreach(var (t,x,y,z) in new[]{(16.5,30.0,1.7,0.0),(17.6,30.0,1.7,0.0),(28.9,6.0,7.7,0.0),(31.2,6.0,7.7,4.0),(26.0,-26.0,7.7,20.0),(28.0,-26.0,7.7,20.0),(31.0,0.0,1.7,10.0),(40.0,0.0,1.7,10.0)})
            {
                Frame(fx,t,1,false,"horde",x,y,z,.6f);int full=fx.Telegraph,ent=fx.Entrances,guide=fx.Guide,life=fx.Haze+fx.Motes+fx.Halos+fx.Pools;
                Frame(fx,t,.3,false,"horde",x,y,z,.6f);int rt=fx.Telegraph,re=fx.Entrances,rg=fx.Guide,rl=fx.Haze+fx.Motes+fx.Halos+fx.Pools;
                Frame(fx,t,1,true,"horde",x,y,z,.6f);int lt=fx.Telegraph,le=fx.Entrances,lg=fx.Guide,ll=fx.Haze+fx.Motes+fx.Halos+fx.Pools;
                Check(full==rt&&full==lt&&ent==re&&ent==le&&guide==rg&&guide==lg&&full>0&&rl<life&&ll<life,"at "+t+" s the telegraph keeps "+full+" quads, the entrances "+ent+", the guides "+guide+" on every tier; light and life "+life+" / "+rl+" / "+ll);
            }
            Frame(fx,12,1,false,"calm",0,1.7,10,0);int shut=fx.Entrances;Frame(fx,12,1,false,"calm",0,1.7,10,.8f);Check(fx.Entrances>shut,"opening entrances show ("+shut+" -> "+fx.Entrances+")");
            song[0]=true;warn[1]=.6f;Frame(fx,12,1,false,"boss",0,1.7,10,0);int sung=fx.Telegraph;song[0]=false;warn[1]=0;Frame(fx,12,1,false,"boss",0,1.7,10,0);Check(sung>fx.Telegraph,"the song vents erupt and warn");
            Frame(fx,12,1,false,"calm",0,1.7,10);int clean=fx.Ink;Frame(fx,12,1,false,"emergency",0,1.7,10);Check(fx.Ink>=clean,"the ink's breath grows with the corruption");
            Frame(fx,17.6,1,false,"horde",24,1.7,0);long before=GC.GetAllocatedBytesForCurrentThread();
            var mood=NativeWardZeroMood.Preset("boss");
            for(int i=0;i<20;i++){Frame(fx,15+i*.9,i%3==0?.3:1,i%5==0,i%2==0?"horde":"emergency",Math.Sin(i)*30,i%2==0?1.7:7.7,Math.Cos(i)*20,i%4*.3f);foreach(var p in kit.Parts){NativeWardZeroKit.Drive(p,states,open,mood,15+i,out _,out _,out _,out _);NativeWardZeroKit.Move(p,states,open,mood,out _);}}
            long used=GC.GetAllocatedBytesForCurrentThread()-before;Check(used==0,"no allocations per frame ("+used+" bytes over 20 frames)");
        }
        private static void Hooks(string root)
        {
            string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
            var src=R("NativeSourcePresentation.cs");var back=R("NativeBackdropPresentation.cs");
            Check(src.Contains("bool revamp=arena.MapId==NativeWardZeroLooks.MapId&&NativeWardZeroLooks.Active(arena.Map);")&&src.Contains("if(!revamp)sourceModels.Queue(\"map-\"+arena.MapId,Matrix4x4.identity);")&&src.Contains("if(arena.MapId==\"siren\"&&!revamp)WardPresentation(arena);")&&src.Contains("if(revamp)WardZeroScenery(arena);"),"SourceWorld keeps today's hall unless the map is the revamp");
            Check(back.Contains("if(!revamp)sourceModels.Queue(\"map-\"+mapId,Matrix4x4.identity);")&&back.Contains("if(revamp)WardZeroBackdrop(mapId,time,camera);")&&back.Contains("else if(mapId==\"siren\")")&&R("ArenaPresentation.cs").Contains("DisposeWardZero();"),"the menu backdrop and Dispose reach the revamp, today's hall otherwise");
            var p=R("NativeWardZeroPresentation.cs");
            Check(p.Contains("var map=arena.Map;if(!NativeWardZeroLooks.Active(map)||!EnsureWardZero())return;")&&p.Contains("!NativeWardZeroLooks.Active(map)||!EnsureWardZero())return false;"),"dormant unless the map is the revamp");
            Check(!p.Contains("Collider")&&!p.Contains("new GameObject")&&!p.Contains("AddComponent")&&!p.Contains("Rigidbody"),"no colliders, objects or bodies");
            Check(p.Contains("NativeWardZeroHazards.At(wzHazards[i],match.Phase,round,match.FightAt,now)")&&p.Contains("NativeStructureFx.RoundIndex(match.Set,match.Round)")&&p.Contains("PresentationClock(arena)"),"hazard rigs read the pure Core state on the presentation clock");
            Check(p.Contains("NativeLighting.PickedLights(wzLights.Pos,wzLights.Rgb)")&&R("NativeLighting.cs").Contains("currentMap=map;arenaPicked=false;"),"the picked lights take the practical slots only while the revamp draws; a new look resets them");
            Check(R("NativeLighting.cs").Contains("public static MapLook Look(ContentMap map)=>(string.IsNullOrEmpty(map.look)?null:NativeMapLooks.ByLook(LookSet(),map.look))??NativeMapLooks.Resolve("),"a map's look field picks its preset (today's maps carry none or their own)");
            foreach(var f in new[]{"NativeWardZeroLooks.cs","NativeWardZeroHazards.cs","NativeWardZeroMood.cs","NativeWardZeroKit.cs","NativeWardZeroLights.cs","NativeWardZeroFx.cs"})Check(!File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Core",f)).Contains("UnityEngine"),f+" is engine-free Core");
            // Today's hall is untouched: its presentation and motion data are the same files, read the same way.
            Check(R("NativeWardPresentation.cs").Contains("Vector3 at=NativeArena.StageVents[e];")&&File.Exists(Path.Combine(root,"Assets/Conversion/Resources/WardMotion.json")),"today's Ward Zero presentation is unchanged");
        }
    }
}
