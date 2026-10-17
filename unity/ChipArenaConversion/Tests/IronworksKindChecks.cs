#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The City Ironworks enemies' presentation (Core/NativeHordeZones, NativeIronworksKinds, NativeIronworksModels,
    // NativeIronworksMotion, NativeIronworksFx, NativeIronworksGallery; Runtime/NativeIronworksKindsPresentation.cs): the five
    // register after the Siren's seven and change nothing of them; the brute's seams turn cream in the Ironworks only; the
    // telegraph timings are the doc's; the view fields are the doc's names and bind in one line; the bodies, crust and props build
    // within budget and the bodies stay in their capsules; feet plant; the telegraphs read (cream, cyan, white, never orange; the
    // pool under them); nothing allocates per frame; the runtime only reads; the cue keys are section 8's.
    internal static class IronworksKindChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok){if(Environment.GetEnvironmentVariable("IRONWORKS_ALL")=="1"){Console.WriteLine("FAIL ironworks kinds: "+what);return;}throw new Exception("Ironworks kinds: "+what);}count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static readonly string[] Five={"cinderling","stoker","slagback","ladleman","rigger"};
        public static void Run(string root)
        {
            Registration(root);Unchanged(root);Seams(root);Timings(root);View(root);Models();Inside();Feet();Motion();Effects();Allocation();Runtime(root);Cues(root);
            Console.WriteLine("PASS ironworks kinds ("+count+" checks: the five register after the Siren's seven and leave them unchanged, cream brute seams in the Ironworks only, the doc's telegraph timings, the view's fields bind in one line, models within budget and their capsules, planted feet, readable telegraphs, no allocation, presentation-only runtime, section 8's cue keys)");
        }
        // ---- the five as kinds: after the seven, resolved and known, no content row, their capsules and bodies ----
        private static void Registration(string root)
        {
            Check(NativeHordeKinds.Count==7&&NativeHordeKinds.All==NativeHordeKinds.Count+NativeHordeZones.Count&&NativeHordeZones.Count==5+NativeStationKinds.KindCount,"the Siren's seven stay Count; the five follow them (All "+NativeHordeKinds.All+")");
            Check(NativeIronworksKinds.First==NativeHordeKinds.Count,"the cinderling is the first kind after the Siren's");
            for(int z=0;z<5;z++)
            {
                int k=NativeIronworksKinds.Kind(z);string id=Five[z];
                Check(NativeHordeKinds.Ids[k]==id&&NativeHordeKinds.Resolve(id)==k&&NativeHordeKinds.Known(id)&&NativeIronworksKinds.Local(k)==z&&NativeIronworksKinds.Is(k),id+" resolves to kind "+k+" is known to the authority and wire");
                Check(NativeHordeKinds.Shipped[k],id+" is a shipped zone content kind");
                Check(NativeHordeZones.Of(k)!=null&&NativeHordeZones.Of(k).Zone==NativeIronworksKinds.Map&&NativeHordeKinds.Base(k)==NativeHordeZones.Of(k).Body&&NativeHordeKinds.Index(k)==k,id+" borrows its body's tables");
                Check(NativeHordeKinds.Looks[k]>=1&&NativeHordeKinds.Looks[k]<=NativeHordeKinds.MostLooks,id+" looks");
                for(int i=10000;i<10300;i++){int l=NativeHordeKinds.Look(k,i);Check(l>=0&&l<NativeHordeKinds.Looks[k],"look in range");}
            }
            int c=NativeIronworksKinds.Kind(NativeIronworksKinds.Cinderling),st=NativeIronworksKinds.Kind(NativeIronworksKinds.Stoker),sl=NativeIronworksKinds.Kind(NativeIronworksKinds.Slagback),la=NativeIronworksKinds.Kind(NativeIronworksKinds.Ladleman),ri=NativeIronworksKinds.Kind(NativeIronworksKinds.Rigger);
            Check(NativeHordeKinds.Base(c)==NativeHordeKinds.Runner&&NativeHordeKinds.Base(st)==NativeHordeKinds.Shambler&&NativeHordeKinds.Base(sl)==NativeHordeKinds.Brute&&NativeHordeKinds.Base(la)==NativeHordeKinds.Brute&&NativeHordeKinds.Base(ri)==NativeHordeKinds.Shambler,"bodies: the runner, the shambler, the brute, the brute, the shambler (doc 6)");
            Check(NativeHordeKinds.Scale[c]==.93f&&NativeHordeKinds.Scale[st]==1.07f&&NativeHordeKinds.Scale[sl]==1&&NativeHordeKinds.Scale[la]==1.24f&&NativeHordeKinds.Scale[ri]==1,"drawn at .93, 1.07, 1, 1.24 (doc 6) and the rigger at 1 (its hard hat inside the shambler's capsule the contract gives it)");
            // The world capsules: the contract's where it names one (the ladleman's .62 x 2.1, the rigger's the shambler's), the add capsule else.
            Check(Near(NativeHordeKinds.WorldRadius(la),.62f)&&Near(NativeHordeKinds.WorldHeight(la),2.1f)&&Near(NativeHordeKinds.RootHeight(la,false),1.05f),"the ladleman's capsule is the contract's .62 x 2.1");
            Check(Near(NativeHordeKinds.WorldRadius(ri),NativeHordeKinds.AddRadius)&&Near(NativeHordeKinds.WorldHeight(ri),NativeHordeKinds.AddHeight)&&Near(NativeHordeKinds.WorldRadius(c),NativeHordeKinds.AddRadius)&&Near(NativeHordeKinds.WorldRadius(sl),NativeHordeKinds.AddRadius)&&Near(NativeHordeKinds.RootHeight(c,false),.85f),"the rigger's, the cinderling's and the slagback's are the add capsule");
            Check(Near(NativeHordeKinds.WorldRadius(st),.45f)&&Near(NativeHordeKinds.WorldHeight(st),1.82f),"the stoker's proposed capsule is the add capsule at its size (.45 x 1.82)");
            for(int z=0;z<5;z++){int k=NativeIronworksKinds.Kind(z);Check(Near(NativeHordeKinds.DrawRoot(k,false),.85f*NativeHordeKinds.Scale[k]),"the drawn root stands its rig root over the floor");}
            Check(NativeHordeKinds.Resolve("future-kind")==NativeHordeKinds.Shambler&&!NativeHordeKinds.Known("future-kind")&&NativeHordeKinds.Resolve("Cinderling")==NativeHordeKinds.Shambler,"unknown ids still draw as the shambler; ids exact");
            // None of the five is in today's content, the live manifest or the draft's allow lists (Astra's rows add them).
            var adds=JsonDocument.Parse(R(root,"Assets/Conversion/Resources/PveContent.json")).RootElement.GetProperty("adds").EnumerateArray().Select(a=>a.GetProperty("id").GetString()).ToList();
            Check(adds.All(a=>NativeHordeKinds.Resolve(a)<NativeHordeKinds.Count)&&!Five.Any(adds.Contains),"today's content has none of the five (its adds resolve to the Siren's kinds)");
            var draft=R(root,"Tests/expedition-ironworks-layout-draft.json");var allows=Regex.Matches(draft,"\"allow\": ?\\[([^\\]]*)\\]").Select(m=>m.Groups[1].Value).ToList();
            Check(allows.Count>0&&allows.All(a=>!Five.Any(f=>a.Contains("\""+f+"\""))),"the draft's allow lists still name only the Siren's kinds (the proposals sit beside them)");
            // The live exp-layout-6 manifest binds the approved five kinds to authored entries.
            var liveAllows=Regex.Matches(R(root,"Assets/Conversion/Resources/ExpeditionLayout.json"),"\"allow\": ?\\[([^\\]]*)\\]").Select(m=>m.Groups[1].Value).ToList();
            Check(liveAllows.Count>0&&Five.All(f=>liveAllows.Any(a=>a.Contains("\""+f+"\""))),"the live manifest allows all five ("+liveAllows.Count+" lists)");
        }
        private static bool Near(float a,float b,float e=1e-4f)=>Math.Abs(a-b)<=e;
        // ---- today's seven exactly as before (with the full bitwise comparison in the DEV_LOG; HordeKindChecks still run). The
        // models' counts were re-pinned on 2026-10-17 by the zombie design pass (claude/zombie-design: the seven rebuilt on the
        // organic kit, docs/ZOMBIE-DESIGN.md); the zone kinds still leave them exactly as built. ----
        private static readonly int[] BaseCounts={6590,13388,0,2918,5836,0,564,1128,0,6800,13744,0,2762,5524,0,552,1104,0,5816,11712,0,2698,5396,0,456,912,0,5404,10856,0,2284,4568,0,432,864,0,5112,10232,0,2150,4300,0,348,696,0,6450,13012,0,2770,5540,0,480,960,0,6906,13988,0,2930,5860,0,516,1032,0,5932,11992,192,2804,5608,106,564,1128,12,5354,10732,200,2740,5480,118,420,840,12,4662,9332,0,2108,4216,0,348,696,0,4884,9808,0,2072,4144,0,396,792,0,7132,14480,0,2760,5520,0,564,1128,0,6224,12464,0,2760,5520,0,432,864,0,6904,14008,194,2500,5000,118,408,816,12,6928,14008,200,2668,5336,114,408,816,12};
        private static void Unchanged(string root)
        {
            Check(NativeHordeKinds.Ids.Take(7).SequenceEqual(new[]{"shambler","runner","brute","spitter","crawler","bloater","hulk"}),"the seven ids in their order");
            Check(NativeHordeKinds.Looks.Take(7).SequenceEqual(new[]{3,2,2,2,2,2,2})&&NativeHordeKinds.Shipped.Take(7).SequenceEqual(new[]{true,true,true,true,false,false,true}),"their looks and shipped flags");
            Check(NativeHordeKinds.Seam.Take(7).SequenceEqual(new[]{0x9d84ff,0xff4fb4,0xff6a2c,0x8dff4f,0xe8dcff,0xffc23a,0xff2e4d})&&NativeHordeKinds.SeamClass.Take(7).SequenceEqual(new[]{2,2,1,2,2,1,1}),"their seams");
            Check(NativeHordeKinds.Radius.Take(7).SequenceEqual(new[]{.42f,.42f,.42f,.42f,.32f,.5f,NativeHordeKinds.HulkRadius/NativeHordeKinds.HulkScale})&&NativeHordeKinds.Height.Take(7).SequenceEqual(new[]{1.7f,1.7f,1.7f,1.7f,1.3f,1.7f,1.7f})&&NativeHordeKinds.Scale.Take(7).SequenceEqual(new[]{1,1,1,1,1,1,1.8f})&&NativeHordeKinds.Mass.Take(7).SequenceEqual(new[]{1,.78f,2.6f,.95f,.85f,1.9f,9}),"their capsules, scales and weights");
            Check(NativeHordeKinds.Default.Take(7).SequenceEqual(new[]{NativeHordeKinds.Attack.Swipe,NativeHordeKinds.Attack.Lunge,NativeHordeKinds.Attack.Smash,NativeHordeKinds.Attack.Spit,NativeHordeKinds.Attack.Pounce,NativeHordeKinds.Attack.Belly,NativeHordeKinds.Attack.Smash}),"their attacks");
            // Out-of-range indices clamp as they always did (0 below; the Hulk's tables past every kind).
            Check(NativeHordeKinds.Index(-2)==0&&NativeHordeKinds.Index(NativeHordeKinds.All)==NativeHordeKinds.Hulk&&NativeHordeKinds.Base(99)==NativeHordeKinds.Hulk&&NativeHordeKinds.Give(99)==NativeHordeKinds.Give(0),"indices out of range clamp as before");
            for(int k=0;k<7;k++){Check(NativeHordeKinds.Base(k)==k&&NativeHordeKinds.Index(k)==k,"a Siren kind is its own base");Check(NativeHordeKinds.StepCue(k)==NativeZombieSteps.Key(k,k==NativeHordeKinds.Crawler,0),"step cue of "+k);Check(NativeHordeModels.Tongues(k,0).Length>0,"the Siren's kinds keep their tongues");}
            // Every model of the seven: the base commit's triangle and vertex counts per kind, look and tier (and the jaws').
            int i=0;for(int k=0;k<7;k++)for(int l=0;l<NativeHordeKinds.Looks[k];l++)for(int t=0;t<3;t++)
            {
                var m=NativeHordeModels.Build(k,l,t);int v=m.Mesh.Sections.Sum(s=>s.Positions.Count);
                Check(m.Mesh.Triangles==BaseCounts[i]&&v==BaseCounts[i+1]&&(m.Jaw?.Triangles??0)==BaseCounts[i+2],NativeHordeModels.Id(k,l,t)+" is as pinned ("+m.Mesh.Triangles+" triangles, "+v+" vertices)");i+=3;
                Check(NativeHordeModels.Id(k,l,t)=="minion-"+NativeHordeKinds.Ids[k]+"-"+l+"-"+NativeHordeModels.TierNames[t],"model id unchanged");
            }
            Check(i==BaseCounts.Length,"every model of the seven compared");
            // The rules' own file still uses the shared tables only where it did (no Ironworks rule here).
            var sim=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");Check(!sim.Contains("Ironworks")&&!sim.Contains("NativeHordeZones"),"the authority's horde has no Ironworks code (Astra's)");
        }
        // ---- the zone tint: the brute cream (0xFFD27A) only while the Ironworks is the map ----
        private static void Seams(string root)
        {
            bool was=NativeIronworksKinds.Zone;
            try
            {
                NativeIronworksKinds.Zone=false;
                for(int k=0;k<NativeHordeKinds.All;k++)Check(NativeHordeKinds.SeamOf(k)==NativeHordeKinds.Seam[k],"outside the Ironworks every kind's seam is its own ("+k+")");
                Check(NativeHordeKinds.SeamOf(NativeHordeKinds.Brute)==0xff6a2c&&NativeHordeModels.Id(NativeHordeKinds.Brute,1,0)=="minion-brute-1-full","the brute orange and its own model elsewhere");
                foreach(var map in new[]{"expedition-ward-zero","expedition-town","expedition-city","boss-test","expedition-transit",null,"","Expedition-Ironworks","Expedition-Foundry","foundry"})Check(!NativeIronworksKinds.ZoneOf(map),"map "+(map??"null")+" is not the Ironworks");
                Check(NativeIronworksKinds.ZoneOf("expedition-ironworks")&&NativeIronworksKinds.Map=="expedition-ironworks","the Ironworks design map id (PR #254's)");
                Check(NativeIronworksKinds.ZoneOf("expedition-foundry")&&NativeIronworksKinds.LiveMap=="expedition-foundry","the live Ironworks map id (PR #255's)");
                // The live layout: the Ironworks area runs on the live id, and no other area's map reads as the Ironworks.
                var layout=R(root,"Assets/Conversion/Resources/ExpeditionLayout.json");var areas=Regex.Matches(layout,@"\{""id"": ?""(exp-[a-z0-9-]+)"", ?""role"": ?""[a-z]+"", ?""map"": ?""([a-z0-9-]+)""").Select(m=>(m.Groups[1].Value,m.Groups[2].Value)).ToList();
                Check(areas.Count>=4&&areas.Count(a=>NativeIronworksKinds.ZoneOf(a.Item2))==1&&areas.Any(a=>a.Item1=="exp-foundry"&&a.Item2==NativeIronworksKinds.LiveMap),"the live layout's only Ironworks map is exp-foundry's ("+string.Join(",",areas.Select(a=>a.Item1+"="+a.Item2))+")");
                NativeIronworksKinds.Zone=true;
                Check(NativeHordeKinds.SeamOf(NativeHordeKinds.Brute)==0xffd27a&&NativeIronworksKinds.BruteSeam==0xffd27a,"in the Ironworks the brute's seams are cream");
                for(int k=0;k<NativeHordeKinds.All;k++)if(k!=NativeHordeKinds.Brute){Check(NativeHordeKinds.SeamOf(k)==NativeHordeKinds.Seam[k],"only the brute's change ("+k+")");Check(NativeHordeModels.Id(k,0,0)=="minion-"+NativeHordeKinds.Ids[k]+"-0-full","only the brute's model changes ("+k+")");}
                for(int l=0;l<2;l++)for(int t=0;t<3;t++)Check(NativeHordeModels.Id(NativeHordeKinds.Brute,l,t)==NativeHordeModels.CreamId(l,t)&&NativeHordeModels.CreamId(l,t)=="minion-brute-"+l+"-"+NativeHordeModels.TierNames[t]+"-cream","the cream brute model id ("+l+","+t+")");
            }
            finally{NativeIronworksKinds.Zone=was;}
            // The cream brute is the brute exactly but its seams and eyes: the same geometry, only glow colours differ, and they are cream.
            var cream=WeaponPaint.Linear(0xffd27a);
            for(int l=0;l<2;l++)for(int t=0;t<3;t++)
            {
                var a=NativeHordeModels.Build(NativeHordeKinds.Brute,l,t);var b=NativeHordeModels.Build(NativeHordeKinds.Brute,l,t,NativeIronworksKinds.BruteSeam);int changed=0;bool same=a.Mesh.Sections.Count==b.Mesh.Sections.Count;
                for(int s=0;same&&s<a.Mesh.Sections.Count;s++)
                {
                    var x=a.Mesh.Sections[s];var y=b.Mesh.Sections[s];same&=x.Bone==y.Bone&&x.Positions.SequenceEqual(y.Positions)&&x.Normals.SequenceEqual(y.Normals)&&x.Indices.SequenceEqual(y.Indices);
                    for(int v=0;same&&v<x.Colors.Count;v++)
                    {
                        if(x.Colors[v]==y.Colors[v]&&x.Emission[v]==y.Emission[v])continue;changed++;
                        var e=new Vector3(y.Emission[v].X,y.Emission[v].Y,y.Emission[v].Z);float k=e.X/Math.Max(1e-6f,cream.X);same&=k>0&&Vector3.Distance(e,cream*k)<1e-3f*Math.Max(1,k)&&y.Emission[v].W==x.Emission[v].W;
                    }
                }
                Check(same&&changed>0,"the cream brute "+l+"/"+t+" differs only in its seams and eyes, and they glow cream ("+changed+" vertices)");
            }
            Check(NativeHordeModels.Build(NativeHordeKinds.Brute,0,0,-1).Mesh.Triangles==NativeHordeModels.Build(NativeHordeKinds.Brute,0,0).Mesh.Triangles,"no seam override builds the brute's own");
            // The draft's record names this tint (ExpeditionIronworksDraftChecks reads the record; the doc 3.1 and decision 14).
            var doc=Doc(root);if(doc!=null)Check(doc.Contains("cream `0xFFD27A`")&&doc.Contains("Cream in the Ironworks only (presentation)"),"the doc's decision 14");
            var rec=R(root,"Tests/expedition-ironworks-layout-draft.json");Check(Regex.IsMatch(rec,"\"tint\": ?16765562")&&16765562==NativeIronworksKinds.BruteSeam,"the draft record's tint is the presentation's");
        }
        private static string Doc(string root){string p=Path.Combine(root,"../../docs/EXPEDITION-CITY-IRONWORKS.md");if(!File.Exists(p)){Console.WriteLine("SKIP ironworks kinds doc: docs/EXPEDITION-CITY-IRONWORKS.md is not in this tree");return null;}return Regex.Replace(File.ReadAllText(p),"[ \\t]*\\n[ \\t]*"," ");}
        // ---- the telegraph timings: the doc's (section 6 and the contract's fields), derived from the replicated times ----
        private static void Timings(string root)
        {
            var doc=Doc(root);
            if(doc!=null)
            {
                foreach(var s in new[]{"Wind-up 0.38 s","a 0.45 s fuse","a 1.4 m ember burst","wind-up 0.5 s","wind-up 0.7 s","4.5 m line 0.6 m wide","2.5 m toward the Stoker over 0.35 s and roots 0.4 s","Cooldown 6 s","wind-up 0.55 s","0.8 s stagger","25% faster","wind-up 1.0 s","pours for 1.2 s","60 degrees by 4.6 m","molten pool for 3 s","Cooldown 7 s","a 2 s stun","T-2.0 s","T-1.6 s","stops 2.4 m over the spot, swaying 0.6 m either side (0.8 s period)","cream ring (1.5 m)","T-0.35 s","wind-up 0.6 s","0.5 s sweep through 300 degrees","cream dashed ring at 3.2 m and a cold inner ring at 1 m","Cooldown 5 s","climbs (2.0 s)"})
                    Check(doc.Contains(s),"the doc still says `"+s+"` (update NativeIronworksKinds to follow it)");
                foreach(var s in new[]{"pop {fuse .45, radius 1.4","pull {windup .7, range 4.5, width .6, drag 2.5, dragTime .35, root .4, cooldown 6}","shed {stagger .8, speedAfter 1.25}","armour {front 120 deg, reduction .6","pour {windup 1.0, duration 1.2, arc 60 deg, range 4.6, pool 3 s","stun 2}","hookDrop {rattle .4, lock 1.25, slide .35, hookHeight 2.4, sway .6, swayPeriod .8, radius 1.5","swing {windup .6, sweep .5, arc 300 deg, radius 3.2, inner 1.0","climb {throw .5, speed 5, reach 8","chain {hp 40, fall .2 max hp, stun 1.5}"})
                    Check(doc.Contains(s),"the contract still says `"+s+"`");
                // The contract's 10.1 rows are the proposed rows the DEV preview times with.
                foreach(var z in Enumerable.Range(0,5))
                {
                    var p=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(z));var m=Regex.Match(doc,"\\| `"+p.id+"` \\| [a-z]+ \\| ([0-9.]+) \\| ([0-9.]+) \\| ([a-z]+)[^|]*\\| ([0-9.]+) / ([0-9.]+) \\| ([0-9.]+) \\| ([0-9.]+) \\| ([0-9.]+) \\| ([0-9.]+) \\| ([0-9.]+) \\| ([0-9.]+) \\| ([0-9.]+) \\| \\.?([0-9.]+) / \\.?([0-9.]+) \\| \\.?([0-9.]+) \\|");
                    Check(m.Success,"the contract's "+p.id+" row parses");float F(int g)=>float.Parse((m.Groups[g].Value.StartsWith(".")?"0":"")+m.Groups[g].Value,System.Globalization.CultureInfo.InvariantCulture);float Fr(int g)=>float.Parse("0."+m.Groups[g].Value.TrimStart('0','.'),System.Globalization.CultureInfo.InvariantCulture);
                    Check(p.hp==F(1)&&p.damage==F(2)&&p.attack==m.Groups[3].Value&&p.walk==F(4)&&p.run==F(5)&&p.reach==F(6)&&p.startRange==F(7)&&p.hit==F(8)&&p.cycle==F(9)&&p.lunge==F(10)&&p.impulse==F(11)&&p.reward==F(12),p.id+" proposed row is the contract's");
                    Check(Near(p.limbFrac,Fr(13),1e-4f)&&Near(p.headFrac,Fr(14),1e-4f)&&Near(p.stagger,Fr(15),1e-4f),p.id+" fractions are the contract's");
                }
            }
            float Nan=float.NaN;
            // Cinderling: the strike at LastAttack + .38 (the authority's formula), the pop only on a melee kill, after the fuse.
            var cr=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(0));Check(NativeHordeKinds.Strike(cr,false,0)==.38f&&NativeHordeKinds.Strike(cr,true,0)==.34f,"the cinderling's lunge lands at .38 (the crawl rule .34)");
            Check(NativeIronworksKinds.PopAt(10,true)==10.45f&&float.IsNaN(NativeIronworksKinds.PopAt(10,false))&&NativeIronworksKinds.PopRadius==1.4f,"the pop: .45 s after a melee kill, 1.4 m; none otherwise");
            Check(NativeIronworksKinds.Strobe(-.01f)==0&&NativeIronworksKinds.Strobe(.5f)==0&&NativeIronworksKinds.Strobe(.2f)>=0,"the fuse strobes only through the fuse");
            Check(NativeIronworksFx.CoreFlare(.38f,.38f)>.99f&&NativeIronworksFx.CoreFlare(.05f,.38f)<.2f&&NativeIronworksFx.CoreFlare(Nan,.38f)==0,"the core flares white to the strike");
            // Stoker: the throw at pullAt + .7, the hook at the line's end at the throw's end, the drag .35 s, the root .4 s.
            Check(NativeIronworksKinds.Pull(10,10.69f,true,out _)==NativeIronworksKinds.PullPhase.Windup&&NativeIronworksKinds.Pull(10,10.71f,true,out _)==NativeIronworksKinds.PullPhase.Throw,"the throw at .7 s");
            Check(Near(NativeIronworksKinds.HookOut(10,10.7f+NativeIronworksKinds.HookFlight-1e-4f,true),4.5f,.01f)&&NativeIronworksKinds.HookOut(10,10.5f,true)==0,"the hook flies the 4.5 m line at the throw, never before");
            float drag=10+.7f+NativeIronworksKinds.HookFlight;Check(NativeIronworksKinds.Pull(10,drag+.01f,true,out _)==NativeIronworksKinds.PullPhase.Drag&&NativeIronworksKinds.Pull(10,drag+.36f,true,out _)==NativeIronworksKinds.PullPhase.Root&&NativeIronworksKinds.Pull(10,drag+.76f,true,out _)==NativeIronworksKinds.PullPhase.Recover,"a hit drags .35 s then roots .4 s");
            Check(Near(NativeIronworksKinds.HookOut(10,drag+.35f,true),2f,.01f),"the drag reels the hook in 2.5 m");
            Check(NativeIronworksKinds.Pull(10,drag+.1f,false,out _)==NativeIronworksKinds.PullPhase.Recover&&NativeIronworksKinds.Pull(10,drag+.5f,false,out _)==NativeIronworksKinds.PullPhase.None,"a miss reels in and is done");
            Check(NativeIronworksKinds.PullInterrupted(14,90,false)&&!NativeIronworksKinds.PullInterrupted(13,90,false)&&NativeIronworksKinds.PullInterrupted(1,90,true),"the pull's interrupt: 15% of its health or a heavy melee blow");
            var srow=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(1));Check(NativeHordeKinds.Strike(srow,false,0)==.5f,"the rake sweep lands at .5");
            // Slagback: the slam at .55 on the brute's disc, the shed's .8 s stagger.
            var gr=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(2));Check(NativeHordeKinds.Strike(gr,false,0)==.55f&&NativeHordeKinds.SmashRadiusOf(NativeIronworksKinds.Kind(2))==1.6f&&NativeHordeKinds.SmashAheadOf(NativeIronworksKinds.Kind(2))==1.2f,"the slam lands at .55 on the 1.6 m disc 1.2 m ahead");
            Check(NativeIronworksKinds.Shedding(5,5)==1&&NativeIronworksKinds.Shedding(5,5.81f)==0&&NativeIronworksKinds.Crusted(5,4.9f)&&!NativeIronworksKinds.Crusted(5,5)&&NativeIronworksKinds.Crusted(Nan,99),"the shed: .8 s, the crust gone after");
            Check(NativeIronworksKinds.Frontal(new Vector2(0,-1),0)&&!NativeIronworksKinds.Frontal(new Vector2(0,1),0)&&NativeIronworksKinds.Frontal(new Vector2(.8f,-.6f),0)&&!NativeIronworksKinds.Frontal(new Vector2(1,0),0),"the armoured front is 120 degrees");
            // Ladleman: the pour at pourAt + 1 for 1.2 s on the 60 degree, 4.6 m arc; the pool burns 3 s; the spill's 2 s stun.
            Check(NativeIronworksKinds.Pour(10,10.99f,out _)==NativeIronworksKinds.PourPhase.Raise&&NativeIronworksKinds.Pour(10,11.01f,out _)==NativeIronworksKinds.PourPhase.Pour&&NativeIronworksKinds.Pour(10,12.21f,out _)==NativeIronworksKinds.PourPhase.Lower,"the pour: 1 s raise, 1.2 s pour");
            Check(float.IsNaN(NativeIronworksKinds.Sweep(10,10.5f))&&Near(NativeIronworksKinds.Sweep(10,11.0001f),-1,.01f)&&Near(NativeIronworksKinds.Sweep(10,12.1999f),1,.01f),"the stream sweeps the arc from edge to edge");
            Check(!NativeIronworksKinds.Pool(10,10.9f,out _,out _,out _)&&NativeIronworksKinds.Pool(10,11.6f,out float laid,out _,out _)&&laid>.3f&&laid<.7f&&NativeIronworksKinds.Pool(10,15.1f,out _,out float heat,out _)&&heat>.5f&&!NativeIronworksKinds.Pool(10,10+1+1.2f+3+NativeIronworksKinds.PoolCool+.01f,out _,out _,out _),"the pool: laid as it pours, burning 3 s after, then cooled out");
            Check(NativeIronworksKinds.InArc(new Vector2(0,3),0)&&!NativeIronworksKinds.InArc(new Vector2(0,4.7f),0)&&NativeIronworksKinds.InArc(new Vector2(.5f*3*(float)Math.Sin(.5),3*(float)Math.Cos(.5)),0)&&!NativeIronworksKinds.InArc(new Vector2(3*(float)Math.Sin(.6),3*(float)Math.Cos(.6)),0),"the arc: 4.6 m, 60 degrees");
            Check(NativeIronworksKinds.Spilled(5,5.5f)>.99f&&NativeIronworksKinds.Spilled(5,7.6f)==0&&!NativeIronworksKinds.Carrying(5,5)&&NativeIronworksKinds.Carrying(Nan,9),"the spill's 2 s stun, the ladle gone");
            var lrow=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(3));Check(NativeHordeKinds.Strike(lrow,false,0)==.6f,"the ladle swing lands at .6");
            // Rigger: rattle T-2.0, lock T-1.6, slide T-.35, land T 0 (lockAt + 1.6); the ring flashes at the slide; the swing.
            float L=10;Check(NativeIronworksKinds.Drop(L,L-.41f,out _)==NativeIronworksKinds.DropPhase.None&&NativeIronworksKinds.Drop(L,L-.39f,out _)==NativeIronworksKinds.DropPhase.Rattle,"the rattle 2.0 s before the landing (.4 s before the lock)");
            Check(NativeIronworksKinds.Drop(L,L+1.24f,out _)==NativeIronworksKinds.DropPhase.Lowered&&NativeIronworksKinds.Drop(L,L+1.26f,out _)==NativeIronworksKinds.DropPhase.Slide&&NativeIronworksKinds.Drop(L,L+1.61f,out _)==NativeIronworksKinds.DropPhase.Land&&NativeIronworksKinds.Drop(L,L+2.11f,out _)==NativeIronworksKinds.DropPhase.Done,"the slide .35 s before the landing at the lock + 1.6");
            Check(Near(1.6f-NativeIronworksKinds.DropSlide,.35f)&&Near(NativeIronworksKinds.DropLand+NativeIronworksKinds.DropRattle,2f),"T-.35 and T-2.0 from the landing");
            Check(NativeIronworksKinds.Ring(L,L+.5f,out float fl)>.99f&&fl==0&&NativeIronworksKinds.Ring(L,L+1.26f,out float fl2)==1&&fl2>.9f&&NativeIronworksKinds.Ring(L,L-.1f,out _)==0,"the ring fades in with the lock and flashes at the slide");
            Check(NativeIronworksKinds.Slid(L,L+1.2f)==0&&NativeIronworksKinds.Slid(L,L+1.6f)==1&&NativeIronworksKinds.HookLowered(NativeIronworksKinds.HookFall+.5f)>.99f&&NativeIronworksKinds.HookLowered(0)==0,"the hook drops first, the Rigger slides last");
            float sway=0;for(int i=0;i<200;i++)sway=Math.Max(sway,Math.Abs(NativeIronworksKinds.Sway(1+i*.01f,7)));Check(sway<=.6f+1e-4f&&sway>.5f,"the hook sways .6 m either side");
            Check(!NativeIronworksKinds.Lamp(Nan,5,true)&&NativeIronworksKinds.Lamp(4,5,true)&&NativeIronworksKinds.Lamp(Nan,5,false),"the lamp: off while it lurks, on from the lock");
            NativeIronworksKinds.Swing(.3f,out _,out float r0,out bool sw0);NativeIronworksKinds.Swing(.6f+.25f,out _,out float r1,out bool sw1);Check(!sw0&&r0<1.2f&&sw1&&r1==3.2f,"the swing: spun close in the wind-up, 3.2 m through the sweep");
            NativeIronworksKinds.Swing(.6f,out float a0,out _,out _);NativeIronworksKinds.Swing(1.1f,out float a1,out _,out _);Check(Near(a0-a1,300*(float)Math.PI/180,1e-3f),"the sweep turns 300 degrees in .5 s");
            var rrow=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(4));Check(NativeHordeKinds.Strike(rrow,false,0)==.6f,"the swing lands at .6");
            Check(NativeIronworksKinds.Climbed(10,10.4f,10,out bool th)==0&&th&&Near(NativeIronworksKinds.Climbed(10,12.5f,10,out _),1),"the climb: a .5 s throw, then 10 m at 5 m/s (2 s)");
            float h=NativeIronworksKinds.Fallen(10,10.5f,10,out bool landed,out _);Check(Near(h,10-4.9f*.25f,1e-3f)&&!landed&&NativeIronworksKinds.Fallen(10,12,10,out bool l2,out _)==0&&l2,"a cut chain falls under gravity");
            Check(Near(NativeIronworksKinds.EntryLock(20),20-.5f-1.6f)&&Near(NativeIronworksKinds.EntryDuration,2.5f),"the hang entrance lands .5 s before ActiveAt (2.5 s from the rattle)");
        }
        // ---- the view: the doc's field names, NaN and -1 when unset, one line to bind ----
        private static void View(string root)
        {
            var names=typeof(IronworksView).GetFields().Where(f=>!f.IsStatic).Select(f=>f.Name).ToArray();
            var want=new[]{"pullTarget","pullAt","shedAt","pourAt","pourYaw","spilledAt","hang","lockAt","lockX","lockZ","climbAt","climbTo","chainCutAt"};
            Check(names.OrderBy(x=>x).SequenceEqual(want.OrderBy(x=>x)),"the view's fields are the contract's ("+string.Join(", ",names)+")");
            var doc=Doc(root);if(doc!=null)foreach(var n in new[]{"pullAt","shedAt","pourAt","pourYaw","spilledAt","lockAt","climbAt","chainCutAt"})Check(doc.Contains("`"+n+"`"),"the doc's 10.4 names `"+n+"`");
            var v=IronworksView.None;Check(v.pullTarget==-1&&float.IsNaN(v.pullAt)&&float.IsNaN(v.shedAt)&&float.IsNaN(v.pourAt)&&float.IsNaN(v.spilledAt)&&v.hang==-1&&float.IsNaN(v.lockAt)&&float.IsNaN(v.climbAt)&&v.climbTo==-1&&float.IsNaN(v.chainCutAt),"unset: NaN times, -1 indices");
            var rt=R(root,"Assets/ChipArena/Runtime/NativeIronworksKindsPresentation.cs");
            Check(Regex.Matches(rt,"internal static IronworksView IronworksViewOf\\(NativeMinion m\\)=>m\\.Pve\\?\\.Zone\\.Ironworks\\?\\?IronworksView\\.None;").Count==1,"the binding is one line (IronworksViewOf), bound to replicated zone fields");
            Check(Regex.Matches(rt,"IronworksViewOf\\(m\\)").Count>=3&&rt.Split('\n').Where(l=>!l.TrimStart().StartsWith("///",StringComparison.Ordinal)).All(l=>!l.Contains("new IronworksView{")),"every live read goes through it (no view made anywhere else)");
            // A view drives the pose: the ladleman's raised ladle, the stoker's raised rake (the hands go up), the slagback's reel.
            float Hands(int z,IronworksView view,float t)
            {
                var g=new NativeHordeGait();var p=new CapturedRig.Pose();int k=NativeIronworksKinds.Kind(z);var row=NativeHordeKinds.ProposedRow(k);float y=0;
                for(int i=0;i<=(int)(t*60);i++){g.Iron=view;var f=new HordeMotionInput{Kind=k,Id=70000+z,Seed=NativeHordeKinds.Seed(70000+z),Time=i/60f,Root=new Vector3(0,.85f,0),Health=1,AttackAge=float.NaN,Strike=NativeHordeKinds.Strike(row,false,0),Recover=NativeHordeKinds.Recover(row,false,0),Attack=NativeHordeKinds.Default[k]};g.Pose(f,p);y=(p.WorldPosition[CapturedRig.LHand].Y+p.WorldPosition[CapturedRig.RHand].Y)*.5f;}
                return y;
            }
            var none=IronworksView.None;var pour=none;pour.pourAt=1;var pull=none;pull.pullAt=1;
            Check(Hands(3,pour,1.9f)>Hands(3,none,1.9f)+.15f,"a pourAt lifts the ladle");Check(Hands(1,pull,1.55f)>Hands(1,none,1.55f)+.2f,"a pullAt raises the rake");
        }
        // ---- models: the five's bodies, the crust and the props ----
        private static readonly int[] Tell={NativeIronworksKinds.WhiteHot,NativeIronworksKinds.Cyan,NativeIronworksKinds.Fissure,NativeIronworksKinds.Cyan,NativeIronworksKinds.ColdWhite};
        private static bool Orange(Vector3 c){float m=Math.Max(c.X,Math.Max(c.Y,c.Z));if(m<1e-4f)return false;var n=c/m;return n.X>.99f&&n.Y>.2f&&n.Y<.62f&&n.Z<.25f;}
        private static void Models()
        {
            for(int z=0;z<5;z++)
            {
                int k=NativeIronworksKinds.Kind(z);
                for(int l=0;l<NativeHordeKinds.Looks[k];l++)
                {
                    var tiers=new HordeModel[3];for(int t=0;t<3;t++){tiers[t]=NativeHordeModels.Build(k,l,t);Check(tiers[t].Kind==k&&tiers[t].Triangles>0&&tiers[t].Triangles<=NativeHordeModels.Budget[t],NativeHordeModels.Id(k,l,t)+" triangles "+tiers[t].Triangles+" within "+NativeHordeModels.Budget[t]);Check(tiers[t].Jaw==null,"no hinged jaw");}
                    Check(tiers[0].Triangles>tiers[1].Triangles&&tiers[1].Triangles>tiers[2].Triangles,NativeHordeModels.Id(k,l,0)+" thins out tier by tier");
                    var full=new SortedSet<int>(tiers[0].Mesh.Sections.Where(s=>s.Triangles>0).Select(s=>s.Bone));for(int b=1;b<22;b++)Check(full.Contains(b),NativeHordeModels.Id(k,l,0)+" has a part on joint "+b);
                    for(int t=1;t<3;t++)Check(new SortedSet<int>(tiers[t].Mesh.Sections.Where(s=>s.Triangles>0).Select(s=>s.Bone)).SetEquals(full),NativeHordeModels.Id(k,l,t)+" keeps every joint");
                    foreach(var m in tiers)foreach(var s in m.Mesh.Sections)
                    {
                        Check(s.Positions.Count==s.Normals.Count&&s.Positions.Count==s.Colors.Count&&s.Positions.Count==s.Emission.Count&&s.Indices.All(i=>i>=0&&i<s.Positions.Count)&&s.Indices.Count%3==0,"section arrays agree");
                        foreach(var e in s.Emission){if(e.W>2.5f){NativeMinionFx.Decode(e.W,out int cls,out double cell);Check(cls>=0&&cls<=2&&cell>=0&&cell<=1,"glow code decodes");}else Check(e.W==0,"plain parts carry no tint code");}
                        foreach(var c in s.Colors)Check(Math.Max(c.X,Math.Max(c.Y,c.Z))<=1.0001f,"albedo at most 1");
                    }
                    Check(tiers[0].Mesh.Sections.Sum(s=>s.Emission.Count(e=>e.W>2.5f))>0,NativeHordeModels.Id(k,l,0)+" has flickering glow");
                    // Its tell: glowing parts in its non-orange colour (white-hot, cyan, white-gold, cold white), none plain orange but the slagback's dim cracks.
                    var tell=WeaponPaint.Linear(Tell[z]);tell/=Math.Max(tell.X,Math.Max(tell.Y,tell.Z));int tells=0,orange=0;
                    foreach(var s in tiers[0].Mesh.Sections)foreach(var e in s.Emission){var c=new Vector3(e.X,e.Y,e.Z);float mx=Math.Max(c.X,Math.Max(c.Y,c.Z));if(mx<.3f)continue;if(Vector3.Distance(c/mx,tell)<.08f)tells++;if(Orange(c))orange++;}
                    Check(tells>0,NativeHordeModels.Id(k,l,0)+" carries its tell ("+tells+")");Check(z==NativeIronworksKinds.Slagback||orange==0,NativeHordeModels.Id(k,l,0)+" glows no plain orange ("+orange+")");
                    Check(NativeHordeModels.Tongues(k,l).Length==0,"no Siren black-flame tongues on an Ironworks kind");
                }
            }
            for(int l=0;l<2;l++)for(int t=0;t<3;t++){var c=NativeHordeModels.IronworksCrust(l,t);Check(c.Triangles>0&&c.Triangles<=NativeHordeModels.Budget[t]/2&&c.Sections.All(s=>s.Bone>=0&&s.Bone<22),"the slagback crust "+l+"/"+t+" ("+c.Triangles+")");}
            Check(NativeHordeModels.IronworksCrust(0,0).Triangles>NativeHordeModels.IronworksCrust(0,2).Triangles,"the crust thins on the far tier");
            float brightest=0,ladle=0;
            for(int p=0;p<NativeHordeModels.PropCount;p++)for(int t=0;t<NativeHordeModels.PropTiers;t++)
            {
                var m=NativeHordeModels.IronworksProp(p,t);Check(m.Triangles>0&&m.Triangles<=2400&&m.Sections.Count==1&&m.Sections[0].Bone==0,"prop "+NativeHordeModels.PropId(p,t)+" ("+m.Triangles+")");
                Check(NativeHordeModels.PropId(p,t)=="iw-prop-"+NativeHordeModels.PropNames[p]+"-"+t,"prop id");
                float e=m.Sections[0].Emission.Max(x=>x.X+x.Y+x.Z);if(p==NativeHordeModels.PropLadle)ladle=e;else if(p!=NativeHordeModels.PropLamp)brightest=Math.Max(brightest,e);
            }
            for(int z=0;z<5;z++)foreach(var s in NativeHordeModels.Build(NativeIronworksKinds.Kind(z),0,0).Mesh.Sections)foreach(var e in s.Emission)brightest=Math.Max(brightest,e.X+e.Y+e.Z);
            Check(ladle>brightest,"the ladle's molten bowl is the brightest thing on any of them ("+ladle+" over "+brightest+")");
            Check(NativeHordeModels.IronworksProp(NativeHordeModels.PropRake,0).Bounds(0,out var lo,out var hi)&&hi.Z-lo.Z>2.8f&&hi.Z-lo.Z<3.2f,"the rake is 3 m");
        }
        // ---- every posed body inside its capsule (trunk and head; arms and legs as the Siren kinds') ----
        private static void Inside()
        {
            var pose=new CapturedRig.Pose();
            for(int z=0;z<5;z++)
            {
                int k=NativeIronworksKinds.Kind(z);var st=NativeHordeKinds.ProposedRow(k);
                for(int l=0;l<NativeHordeKinds.Looks[k];l++)
                {
                    var model=NativeHordeModels.Build(k,l,0);var crust=z==NativeIronworksKinds.Slagback?NativeHordeModels.IronworksCrust(l,0):null;
                    foreach(float world in new[]{0,st.walk,st.run})
                    {
                        float speed=world/NativeHordeKinds.Scale[k];var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Look=l,Id=21000+z*10+l,Seed=NativeHordeKinds.Seed(21000+z*10+l),Health=1,AttackAge=float.NaN};
                        float trunk=0,arm=0,leg=0,low=1e9f;
                        for(int i=0;i<200;i++)
                        {
                            float t=i/60f;f.Time=t;f.Root=new Vector3(0,NativeHordeKinds.Root[k],speed*t);f.Velocity=new Vector3(0,0,speed);f.Ground=0;g.Pose(f,pose);if(i<80||i%7!=0)continue;
                            foreach(var mesh in crust==null?new[]{model.Mesh}:new[]{model.Mesh,crust})foreach(var s in mesh.Sections)
                            {
                                int region=s.Bone<=CapturedRig.Head?0:s.Bone==CapturedRig.LClav||s.Bone==CapturedRig.RClav?1:s.Bone<=CapturedRig.RHand?2:3;var q=pose.WorldRotation[s.Bone];var o=pose.WorldPosition[s.Bone];
                                foreach(var v in s.Positions){var w=o+Vector3.Transform(new Vector3(-v.X,v.Y,v.Z),q);w.Y+=NativeHordeKinds.Root[k];float d=Outside(k,w);low=Math.Min(low,w.Y);if(region==0)trunk=Math.Max(trunk,d);else if(region==2)arm=Math.Max(arm,d);else if(region==3)leg=Math.Max(leg,d);}
                            }
                        }
                        string what=NativeHordeModels.Id(k,l,0)+" at "+world+" m/s";bool run=world>=st.run*.99f&&world>0,still=world==0;
                        Check(trunk<=(still?.12f:run?.17f:.12f),what+": trunk and head inside the capsule (worst "+trunk.ToString("0.000")+" m out)");
                        Check(arm<=.45f,what+": arms within reach (worst "+arm.ToString("0.000")+")");Check(leg<=(still?.1f:.45f),what+": legs (worst "+leg.ToString("0.000")+")");Check(low>=-.04f,what+": nothing under the floor ("+low+")");
                    }
                }
            }
        }
        private static float Outside(int k,Vector3 w){float r=NativeHordeKinds.Radius[k],h=NativeHordeKinds.Height[k];float y=Math.Max(r,Math.Min(h-r,w.Y));return Math.Max(0,new Vector3(w.X,w.Y-y,w.Z).Length()-r);}
        // ---- planted feet ----
        private static void Feet()
        {
            var pose=new CapturedRig.Pose();
            for(int z=0;z<5;z++)
            {
                int k=NativeIronworksKinds.Kind(z);var st=NativeHordeKinds.ProposedRow(k);
                foreach(float world in new[]{st.walk,st.run})foreach(float turn in new[]{0f,40f})
                {
                    float speed=world/NativeHordeKinds.Scale[k];var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Id=31000+z,Seed=NativeHordeKinds.Seed(31000+z),Health=1,AttackAge=float.NaN};var last=new Vector3[2];var air=new bool[2];float worst=0;int steps=0;var at=new Vector3(0,.85f,0);float yaw=0;
                    for(int i=0;i<420;i++){float t=i/60f;yaw+=turn/60;float r=yaw*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;at+=v/60;f.Time=t;f.Root=at;f.Velocity=v;f.Yaw=yaw;g.Pose(f,pose);
                        for(int j=0;j<2;j++){if(i>60&&!air[j]&&!g.FootAir[j])worst=Math.Max(worst,Vector3.Distance(last[j],g.FootWorld[j]));if((g.Landed&(1<<j))!=0)steps++;last[j]=g.FootWorld[j];air[j]=g.FootAir[j];}}
                    Check(worst<1e-4f,Five[z]+" at "+world+" m/s turning "+turn+": planted feet never slide (worst "+worst+")");Check(steps>=8,Five[z]+" steps ("+steps+")");
                }
            }
        }
        // ---- motion: the takes, the props in the hands, the strikes on the authority's time ----
        private static void Motion()
        {
            Check(NativeZombieMotion.Walks(NativeIronworksKinds.Kind(1)).SequenceEqual(new[]{NativeZombieClips.Need("walk-6")})&&NativeZombieMotion.Runs(NativeIronworksKinds.Kind(1)).SequenceEqual(new[]{NativeZombieClips.Need("chase-2")}),"the stoker walks walk-6, chases chase-2 (doc 6.2)");
            Check(NativeZombieMotion.Walks(NativeIronworksKinds.Kind(0)).SequenceEqual(new[]{NativeZombieClips.Need("sprint-1"),NativeZombieClips.Need("sprint-3")})&&NativeZombieMotion.Idles(NativeIronworksKinds.Kind(0)).Contains(NativeZombieClips.Need("idle-3")),"the cinderling sprints (6.1)");
            Check(NativeZombieMotion.Walks(NativeIronworksKinds.Kind(2)).SequenceEqual(new[]{NativeZombieClips.Need("walk-4")})&&NativeZombieMotion.Runs(NativeIronworksKinds.Kind(2)).SequenceEqual(new[]{NativeZombieClips.Need("chase-5")}),"the slagback walk-4, chase-5 (6.3)");
            int lk=NativeIronworksKinds.Kind(3);Check(NativeZombieMotion.Rate(NativeZombieClips.Need("walk-4"),1,lk)<NativeZombieMotion.Rate(NativeZombieClips.Need("walk-4"),1,NativeHordeKinds.Brute)&&Near(NativeHordeZones.Tempo(lk,NativeZombieMotion.Tempo),.7f),"the ladleman walks at tempo .7 (6.4)");
            for(int z=0;z<5;z++)Check(NativeZombieMotion.Captured(NativeIronworksKinds.Kind(z)),Five[z]+" walks on the capture");
            var pose=new CapturedRig.Pose();
            // Carried: the rake in both fists (each within 12 cm of the pole's line), the ladle's shank in both fists.
            foreach(int z in new[]{NativeIronworksKinds.Stoker,NativeIronworksKinds.Ladleman})
            {
                int k=NativeIronworksKinds.Kind(z);var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Id=72000+z,Seed=NativeHordeKinds.Seed(72000+z),Health=1,AttackAge=float.NaN,Root=new Vector3(0,.85f,0)};
                for(int i=0;i<90;i++){f.Time=i/60f;g.Pose(f,pose);}g.PlaceProps(f,pose);Check(g.PropCount==1,Five[z]+" carries its prop");
                var p=g.Props[0];var dir=Vector3.Transform(Vector3.UnitZ,p.Turn);
                foreach(int hand in new[]{CapturedRig.LHand,CapturedRig.RHand}){var grip=pose.WorldPosition[hand]+Vector3.Transform(new Vector3(0,-.075f,0),pose.WorldRotation[hand]);var rel=grip-p.At;float off=(rel-dir*Vector3.Dot(rel,dir)).Length();Check(off<.12f,Five[z]+"'s fist "+hand+" on the pole ("+off.ToString("0.000")+" m off)");}
            }
            // The strike lands on the authority's time for every kind (the gait's channels: the shared rule).
            for(int z=0;z<5;z++){var row=NativeHordeKinds.ProposedRow(NativeIronworksKinds.Kind(z));float s=NativeHordeKinds.Strike(row,false,0),r=NativeHordeKinds.Recover(row,false,0);NativeHordeGait.Channels(s,s,r,NativeHordeKinds.Default[NativeIronworksKinds.Kind(z)],out _,out float swing,out _,out _,out _);Check(swing==1&&s==row.hit,Five[z]+" strikes at LastAttack + "+row.hit);}
            // The Rigger off the ground: every state poses finite, the fists on the chain over its head while it hangs.
            foreach(NativeHordeGait.RiggerState stt in Enum.GetValues(typeof(NativeHordeGait.RiggerState)))for(float u=0;u<2;u+=.25f){NativeHordeGait.RiggerHang(pose,3,stt,u,u,.1f);for(int b=0;b<22;b++)Check(!float.IsNaN(pose.WorldPosition[b].X)&&!float.IsNaN(pose.WorldRotation[b].W),"rigger "+stt+" finite");}
            NativeHordeGait.RiggerHang(pose,3,NativeHordeGait.RiggerState.Hang,0,0,0);Check(pose.WorldPosition[CapturedRig.LHand].Y>.45f&&pose.WorldPosition[CapturedRig.RHand].Y>.45f&&pose.WorldPosition[CapturedRig.LFoot].Y<-.5f,"hanging: the fists over its head, the legs below");
            // The pull's hook flies the 4.5 m line from the Stoker (not 4.5 m past the rake's end) and reels back in to the rake.
            {
                int k=NativeIronworksKinds.Kind(1);var g=new NativeHordeGait();var v=IronworksView.None;v.pullAt=1;v.pullTarget=1;var f=new HordeMotionInput{Kind=k,Id=72500,Seed=NativeHordeKinds.Seed(72500),Health=1,Root=new Vector3(0,.85f,0),AttackAge=float.NaN,Ground=0};
                float far=0,reeled=0;for(int i=0;i<150;i++){f.Time=i/60f;g.Iron=v;g.Pose(f,pose);g.PlaceProps(f,pose);float d=new Vector2(g.HookTip.X,g.HookTip.Z).Length()*NativeHordeKinds.Scale[k];far=Math.Max(far,d);if(Near(f.Time,1+.7f+.14f+.35f+.2f,.01f))reeled=d;}
                Check(Near(far,NativeIronworksKinds.PullRange,.2f),"the pull's hook reaches the 4.5 m line's end ("+far.ToString("0.00")+" m)");Check(reeled<=far-1.7f&&reeled>=NativeIronworksKinds.PullRange-NativeIronworksKinds.PullDrag-.25f,"the drag reels it in 2.5 m (to the rake's end at the most; "+reeled.ToString("0.00")+" m)");
            }
            // The swing's hook rides 3.2 m out in the sweep; the spilled ladle lies on the floor.
            {
                int k=NativeIronworksKinds.Kind(4);var g=new NativeHordeGait();var row=NativeHordeKinds.ProposedRow(k);float s=NativeHordeKinds.Strike(row,false,0),r=NativeHordeKinds.Recover(row,false,0);var f=new HordeMotionInput{Kind=k,Id=73000,Seed=NativeHordeKinds.Seed(73000),Health=1,Root=new Vector3(0,.85f,0),Strike=s,Recover=r,Attack=NativeHordeKinds.Attack.Swipe};
                for(int i=0;i<60;i++){f.Time=i/60f;f.AttackAge=i>=20?(i-20)/60f:float.NaN;g.Pose(f,pose);}g.PlaceProps(f,pose);var hook=g.Props[0].At;Check(g.PropCount==1&&g.ChainCount==1&&Near(new Vector2(hook.X,hook.Z).Length()*NativeHordeKinds.Scale[k],3.2f,.05f),"the swing's hook rides the 3.2 m circle ("+new Vector2(hook.X,hook.Z).Length()*NativeHordeKinds.Scale[k]+")");
            }
            {
                int k=NativeIronworksKinds.Kind(3);var g=new NativeHordeGait();var v=IronworksView.None;v.spilledAt=1;var f=new HordeMotionInput{Kind=k,Id=74000,Seed=NativeHordeKinds.Seed(74000),Health=1,Root=new Vector3(0,.85f,0),AttackAge=float.NaN,Ground=0};
                for(int i=0;i<150;i++){f.Time=i/60f;g.Iron=v;g.Pose(f,pose);g.PlaceProps(f,pose);}Check(g.PropCount==1&&g.Props[0].Prop==NativeHordeModels.PropLadleEmpty&&g.Props[0].At.Y<-.5f,"the spilled ladle lies empty on the floor");
            }
        }
        // ---- effects: deterministic, bounded, readable ----
        private static IronFxInput Input(int z,float age,IronworksView v,float t=12)
        {
            int k=NativeIronworksKinds.Kind(z);var row=NativeHordeKinds.ProposedRow(k);
            return new IronFxInput{Kind=z,Seed=99,Time=t,Yaw=30,Scale=NativeHordeKinds.Scale[k],Speed=1,Floor=Vector3.Zero,Chest=new Vector3(0,1.3f,0),Head=new Vector3(0,1.6f,0),HandL=new Vector3(.3f,1,.2f),HandR=new Vector3(-.3f,1,.2f),Hook=new Vector3(0,1,2),Lip=new Vector3(0,1,1.2f),Target=new Vector3(0,.9f,3),HasTarget=true,
                AttackAge=age,Strike=NativeHordeKinds.Strike(row,false,0),Recover=NativeHordeKinds.Recover(row,false,0),View=v,Anchor=new Vector3(0,12,0),Landing=Vector3.Zero};
        }
        private static bool Inside(Vector2 p,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            float Side(Vector3 u,Vector3 v)=>(v.X-u.X)*(p.Y-u.Z)-(v.Z-u.Z)*(p.X-u.X);
            float s0=Side(a,b),s1=Side(b,c),s2=Side(c,d),s3=Side(d,a);return (s0>=0&&s1>=0&&s2>=0&&s3>=0)||(s0<=0&&s1<=0&&s2<=0&&s3<=0);
        }
        private static float Luma(Vector3 c)=>.2126f*c.X+.7152f*c.Y+.0722f*c.Z;
        private static void Effects()
        {
            var b=new IronFxBuffer(4096);var none=IronworksView.None;
            // Each telegraph's decals are cream, cyan or white: never plain orange (3.2).
            var pull=none;pull.pullAt=11.6f;var pour=none;pour.pourAt=11.5f;pour.pourYaw=30;var drop=none;drop.lockAt=11.5f;drop.lockX=1;drop.lockZ=2;drop.hang=3;
            var cases=new(int z,float age,IronworksView v,string what)[]{(0,.3f,none,"the cinderling's flare"),(1,.3f,none,"the rake sweep's arc"),(1,float.NaN,pull,"the pull's line"),(2,.4f,none,"the slam's ring"),(3,float.NaN,pour,"the pour's arc"),(3,.4f,none,"the swing's arc"),(4,float.NaN,drop,"the drop's ring"),(4,.3f,none,"the swing's rings")};
            float tele=1e9f;
            foreach(var c in cases)
            {
                b.Clear();NativeIronworksFx.Emit(b,Input(c.z,c.age,c.v),true);int decals=0;
                for(int i=0;i<b.Count;i++){var e=b.Items[i];if(e.Kind!=IronFxKind.Decal||!e.Additive)continue;decals++;Check(!Orange(e.Color),c.what+": no plain orange decal");if(e.Alpha>.8f)tele=Math.Min(tele,Luma(e.Color));}
                Check(c.z==0||decals>0,c.what+" draws its telegraph ("+decals+" decals)");
                Check(b.Count<=NativeIronworksFx.MaxPerAdd,c.what+" within its budget ("+b.Count+")");
            }
            Check(NativeIronworksFx.PoolMost*(.2126f+.7152f*.48f+.0722f*.12f)<tele,"the molten pool never outshines a telegraph ("+tele+")");
            {
                // The burning pool as drawn: every additive layer over its middle (off a seam between quads) summed stays under the telegraphs too.
                var hotPour=none;hotPour.pourAt=10;hotPour.pourYaw=30;b.Clear();NativeIronworksFx.Emit(b,Input(3,float.NaN,hotPour,12.7f),true);
                float pa=31.3f*(float)Math.PI/180,mid=(NativeIronworksKinds.PourInner+NativeIronworksKinds.PourRange)*.5f;var at=new Vector2((float)Math.Sin(pa),(float)Math.Cos(pa))*mid;float sum=0;int over=0;
                for(int i=0;i<b.Count;i++){var e=b.Items[i];if(e.Kind!=IronFxKind.Decal||!e.Additive)continue;if(Inside(at,e.A,e.B,e.C,e.D)){sum+=Luma(e.Color)*e.Alpha;over++;}}
                Check(over>=2&&sum<tele,"the burning pool, every layer summed ("+sum.ToString("0.00")+" over "+over+" layers), stays under the telegraphs ("+tele.ToString("0.00")+")");
            }
            // The pull's line is cyan dashes along the Stoker's facing, 4.5 m; the slam's ring cream dashes round the 1.6 m disc.
            b.Clear();NativeIronworksFx.Emit(b,Input(1,float.NaN,pull),true);var cyan=WeaponPaint.Linear(NativeIronworksKinds.Cyan);Check(Enumerable.Range(0,b.Count).Count(i=>b.Items[i].Kind==IronFxKind.Decal&&Vector3.Distance(Vector3.Normalize(b.Items[i].Color),Vector3.Normalize(cyan))<.05f)>=6,"the pull's line is cyan dashes");
            float far=0;for(int i=0;i<b.Count;i++)if(b.Items[i].Kind==IronFxKind.Decal)foreach(var p in new[]{b.Items[i].A,b.Items[i].B,b.Items[i].C,b.Items[i].D})far=Math.Max(far,new Vector2(p.X,p.Z).Length());Check(far<=.45f+4.5f+.35f,"the line ends at 4.5 m");
            b.Clear();NativeIronworksFx.Emit(b,Input(2,.4f,none),true);float yr=30*(float)Math.PI/180;var c0=new Vector2((float)Math.Sin(yr),(float)Math.Cos(yr))*1.2f;float most=0;for(int i=0;i<b.Count;i++)if(b.Items[i].Kind==IronFxKind.Decal)foreach(var p in new[]{b.Items[i].A,b.Items[i].B})most=Math.Max(most,(new Vector2(p.X,p.Z)-c0).Length());Check(most<=1.6f+.07f,"the slam ring stays on the authority's disc ("+most+")");
            // The pop: the fuse, then embers to 1.4 m; the crust's shards land on the floor; the spill; the chain snap; deterministic.
            b.Clear();NativeIronworksFx.CinderlingDeath(b,new Vector3(0,1,0),Vector3.Zero,.2f,true,5,true);Check(b.Items.Take(b.Count).Any(e=>e.Kind==IronFxKind.Glow)&&b.Items.Take(b.Count).All(e=>e.Kind==IronFxKind.Glow||e.Kind==IronFxKind.Decal),"the fuse strobes the core");
            {var rim=b.Items.Take(b.Count).Where(e=>e.Kind==IronFxKind.Decal).ToArray();float r=0;foreach(var e in rim)foreach(var p in new[]{e.A,e.B,e.C,e.D})r=Math.Max(r,new Vector2(p.X,p.Z).Length());
             Check(rim.Length>0&&Near(r,NativeIronworksKinds.PopRadius,.02f)&&rim.All(e=>!Orange(e.Color)),"the fuse rings the pop's 1.4 m in cream ("+r.ToString("0.00")+" m)");}
            b.Clear();NativeIronworksFx.CinderlingDeath(b,new Vector3(0,1,0),Vector3.Zero,.75f,true,5,true);Check(b.Items.Take(b.Count).Count(e=>e.Kind==IronFxKind.Streak)>8&&b.Items.Take(b.Count).Where(e=>e.Kind==IronFxKind.Streak).All(e=>new Vector2(e.A.X,e.A.Z).Length()<=1.4f+.01f),"the pop's embers fly to its 1.4 m");
            b.Clear();NativeIronworksFx.CrustShed(b,new Vector3(0,1.3f,0),Vector3.Zero,Vector3.UnitZ,3,7,true);Check(b.Items.Take(b.Count).Where(e=>e.Kind==IronFxKind.Chunk).All(e=>e.A.Y<.05f),"the crust's shards come to rest on the floor");
            var b2=new IronFxBuffer(4096);b.Clear();NativeIronworksFx.Emit(b,Input(3,float.NaN,pour,13),true);NativeIronworksFx.Emit(b2,Input(3,float.NaN,pour,13),true);Check(b.Count==b2.Count&&Enumerable.Range(0,b.Count).All(i=>b.Items[i].A==b2.Items[i].A&&b.Items[i].Color==b2.Items[i].Color),"the effects are a pure function of the input");
            var small=new IronFxBuffer(10);for(int i=0;i<20;i++)NativeIronworksFx.Emit(small,Input(3,float.NaN,pour,13),true);Check(small.Count==10,"a full buffer drops the rest");
        }
        // ---- no allocation per frame: five kinds walking, attacking, in their specials, their props and effects, the rigger's poses ----
        private static void Allocation()
        {
            var pose=new CapturedRig.Pose();var gaits=new NativeHordeGait[5];var reacts=new NativeHordeReact[5];var fx=new IronFxBuffer(4096);
            for(int z=0;z<5;z++){gaits[z]=new NativeHordeGait();reacts[z]=new NativeHordeReact();reacts[z].Bind(NativeIronworksKinds.Kind(z));}
            void Frame(int i)
            {
                float t=i/60f;fx.Clear();
                for(int z=0;z<5;z++)
                {
                    int k=NativeIronworksKinds.Kind(z);var st=NativeHordeKinds.ProposedRow(k);float strike=NativeHordeKinds.Strike(st,false,0),recover=NativeHordeKinds.Recover(st,false,0);
                    var v=IronworksView.None;float loop=t%6;v.pullAt=t-loop+1;v.pourAt=t-loop+1;v.shedAt=t-loop+4;if(loop>4.5f)v.spilledAt=t-loop+4.5f;v.lockAt=t-loop+.5f;v.lockX=1;v.lockZ=1;v.pullTarget=loop>2?1:-1;
                    if(i%90==10)reacts[z].Add(t,new Vector2(0,-1),StaggerWeight.Step,26,true,false,false,0);gaits[z].Iron=v;gaits[z].IronTarget=new Vector3(1,.85f,t+3);gaits[z].IronTargetKnown=true;
                    var f=new HordeMotionInput{Kind=k,Id=75000+z,Seed=NativeHordeKinds.Seed(75000+z),Time=t,Root=new Vector3(0,.85f,t*1.2f),Velocity=new Vector3(0,0,1.2f),Health=1,AttackAge=(i%120)/60f,Strike=strike,Recover=recover,Attack=NativeHordeKinds.Default[k],Side=1,HasTarget=true,Target=new Vector3(1,.85f,t*1.2f+3)};
                    gaits[z].Pose(f,pose,reacts[z]);gaits[z].PlaceProps(f,pose);
                    NativeIronworksFx.Emit(fx,new IronFxInput{Kind=z,Seed=(uint)z,Time=t,Yaw=10,Scale=1,Speed=1.2f,Chest=new Vector3(0,1.3f,t),Head=new Vector3(0,1.6f,t),Hook=new Vector3(0,1,t+2),Lip=new Vector3(0,1,t+1),Target=new Vector3(0,.9f,t+3),HasTarget=true,AttackAge=f.AttackAge,Strike=strike,Recover=recover,View=v,Anchor=new Vector3(0,12,t),Hanging=z==4&&loop<2},true);
                }
                NativeHordeGait.RiggerHang(pose,5,(NativeHordeGait.RiggerState)(i%6),t%2,t,.1f);NativeIronworksFx.CinderlingDeath(fx,Vector3.UnitY,Vector3.Zero,t%2,true,3,true);NativeIronworksFx.ChainSnap(fx,Vector3.UnitY,t%1.2f,4,true);NativeIronworksFx.DeflectSparks(fx,Vector3.UnitY,Vector3.UnitZ,t%.3f,5,true);
                NativeIronworksKinds.Pool(1,t%6,out _,out _,out _);NativeIronworksKinds.HookOut(1,t%3,true);NativeIronworksKinds.Cue(2,"slam");
            }
            for(int i=0;i<400;i++)Frame(i);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=400;i<2400;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation per frame over 2000 frames of the five walking, attacking, pulling, pouring, shedding, spilling and dropping, their props and effects ("+used+" bytes)");
        }
        // ---- the runtime: presentation only, hooked where the horde is drawn ----
        private static void Runtime(string root)
        {
            var rt=R(root,"Assets/ChipArena/Runtime/NativeIronworksKindsPresentation.cs");
            foreach(var bad in new[]{".Hp=","Damage(","StaggerAt=","RecoverAt=","StrikeAt=","LastAttack=","m.Yaw=",".radius=",".height=","Collider.enabled","Body.position=","linearVelocity=","isKinematic=","AddComponent<",".Pve.Entry="})Check(!rt.Contains(bad),"the Ironworks presentation never writes `"+bad+"`");
            var audio=rt.Substring(rt.IndexOf("public sealed partial class NativeAudio",StringComparison.Ordinal));Check(audio.Split("BankPlay(").Length-1==audio.Split("Play(").Length-1,"its cues are bank-only");
            var p=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            foreach(var hook in new[]{"IronworksZone(arena);","IronworksHit(m,s,e,","if(IronworksAdd(arena,m,rig,s,kind,now))return true;","IronworksInput(arena,s,m);","IronworksQueue(arena,rig,s,m,ground,glow,flash);","NativeIronworksKinds.Is(kind))return;","DrawIronworksFx(draw,zfx,eye,right,up,quality,now);","IronworksDeath(death,d.Kind,d.Melee,rig,fx,floorY);","IronworksGallery(arena);","ClearIronworks();","IronworksCues(arena,m,kind,strike,pos,t);","IronworksDeathCues(arena,t);","NativeHordeKinds.SeamOf(j.Kind)"})
                Check(p.Contains(hook),"the horde presentation hands the Ironworks kinds over: `"+hook+"`");
            Check(R(root,"Assets/ChipArena/Runtime/NativeHordeKindModels.cs").Contains("for(int kind=0;kind<NativeHordeKinds.All;kind++)")&&R(root,"Assets/ChipArena/Runtime/NativeHordeKindModels.cs").Contains("IronworksModels(shader);"),"the bank builds the five, their props, the crust and the cream brute");
            var z=R(root,"Assets/ChipArena/Runtime/NativeHordeZombiePresentation.cs");Check(z.Contains("NativeHordeKinds.SeamOf(p.Heavy?NativeHordeKinds.Hulk:NativeHordeKinds.Brute)")&&z.Contains("NativeHordeKinds.SeamOf(d.Kind)"),"the brute's landing cracks and impact ink follow its seam (cream in the Ironworks)");
            Check(p.Contains("\"cinderling\",\"stoker\",\"slagback\",\"ladleman\",\"rigger\"")&&p.Contains("preview<NativeHordeKinds.All"),"the DEV preview offers the five (looks only; the authority's adds keep their kinds)");
            foreach(var f in new[]{"NativeIronworksKinds.cs","NativeIronworksModels.cs","NativeIronworksMotion.cs","NativeIronworksFx.cs","NativeIronworksGallery.cs","NativeHordeZones.cs"}){var t=R(root,"Assets/ChipArena/Core/"+f);Check(!t.Contains("NativeArena")&&!t.Contains("NativePilot")&&!t.Contains("new Random(")&&!t.Contains("UnityEngine"),f+" reads no authority state and no clock-free randomness");}
        }
        // ---- the cue keys: section 8's pve.ironworks keys, all in the bank contract, all routed ----
        private static void Cues(string root)
        {
            var keys=new HashSet<string>(NativeIronworksKinds.CueKeys.Where(k=>k!=null));Check(keys.Count==34&&NativeIronworksKinds.CueCount==34,"34 enemy cue keys ("+keys.Count+")");
            foreach(var k in keys)Check(NativeSoundKeys.Known(k)&&k.StartsWith("pve.ironworks.",StringComparison.Ordinal),k+" is a bank key");
            for(int z=0;z<5;z++)for(int c=0;c<NativeIronworksKinds.Keys[z].Length;c++)Check(NativeIronworksKinds.Keys[z][c]=="pve.ironworks."+NativeIronworksKinds.Ids[z]+"."+NativeIronworksKinds.CueNames[z][c]&&NativeIronworksKinds.Cue(z,NativeIronworksKinds.CueNames[z][c])==NativeIronworksKinds.Keys[z][c],"the key table spells "+NativeIronworksKinds.Keys[z][c]);
            var doc=Doc(root);if(doc!=null){var inDoc=Regex.Matches(doc,"`(pve\\.ironworks\\.[a-z.]+)`").Select(m=>m.Groups[1].Value).Where(k=>!k.EndsWith(".",StringComparison.Ordinal)&&!k.EndsWith("*",StringComparison.Ordinal)).ToHashSet();inDoc.Remove("pve.ironworks.rigger");Check(inDoc.SetEquals(keys),"the keys are exactly section 8's ("+string.Join(", ",inDoc.Except(keys))+" / "+string.Join(", ",keys.Except(inDoc))+")");}
            Check(NativeSoundKeys.Find("pve.ironworks.rigger.rattle",out var rattle)&&rattle.Priority==NativeSoundPriority.High&&rattle.Space==NativeSoundSpace.Positional,"the rattle is positional and high priority (it must cut through the furnace bed)");
            var rt=R(root,"Assets/ChipArena/Runtime/NativeIronworksKindsPresentation.cs");
            foreach(var cue in new[]{"flare","screech","rake","hook.windup","hook.throw","hook.yank","slam.windup","slam","shed","emerge","pour.windup","pour","swing","spill","rattle","lamp","lower","slide","shriek","land","swing.windup","throw","snap"})Check(rt.Contains("\""+cue+"\""),"cue "+cue+" is routed");
            Check(rt.Contains("NativeIronworksKinds.Cue(NativeIronworksKinds.Cinderling,\"pop\")"),"the pop is routed");
        }
    }
}
#endif
