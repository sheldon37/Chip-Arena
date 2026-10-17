#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The Central Station's four enemies' presentation (Core/NativeStationKinds, NativeStationModels, NativeStationMotion,
    // NativeStationFx; Runtime/NativeStationKindsPresentation.cs; docs/EXPEDITION-CENTRAL-STATION.md sections 7, 8 and 10):
    // the four ids registered after the Siren's seven and the City Ironworks' five (Core/NativeHordeZones), drawn but unknown to
    // the authority until shipped; today's seven kinds exactly as at the shared
    // base (Tests/station-enemies-baseline.txt, written by Tests/StationBaseline.cs before these kinds existed); the doc's
    // capsules, rows and telegraph timings; the special strikes landing on those times; the view's field names as section 8
    // names them; the models, props and takes; deterministic effects; no allocation per frame; the runtime presentation only.
    internal static class StationEnemyChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Station enemies: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static readonly int[] Kinds={NativeStationKinds.Kind(NativeStationKinds.Linesman),NativeStationKinds.Kind(NativeStationKinds.Conductor),NativeStationKinds.Kind(NativeStationKinds.Undertow),NativeStationKinds.Kind(NativeStationKinds.Pileup)};
        public static void Run(string root)
        {
            Registration(root);Unchanged(root);string doc=Doc(root);if(doc!=null){Contract(doc);Timings(doc);Fields(doc,root);Audio(doc);}else{Timings(null);Fields(null,root);}
            Strikes();Models();Takes(root);Effects();Allocation();Runtime(root);
            Console.WriteLine("PASS station enemies ("+count+" checks: the four registered after the Siren's seven and the Ironworks' five, all sixteen side by side, the seven exactly as at the base, the live maps' kinds known as before, section 8's capsules and rows, section 7's telegraphs on their times, the view's section 8 field names, models, props and takes, deterministic effects, the cue keys, no allocation, presentation-only runtime)");
        }
        private static string Doc(string root){string path=Path.Combine(root,"../../docs/EXPEDITION-CENTRAL-STATION.md");if(!File.Exists(path)){Console.WriteLine("SKIP station enemies doc: docs/EXPEDITION-CENTRAL-STATION.md is not in this tree");return null;}return File.ReadAllText(path);}

        // ---- the four registered as zone kinds after the Ironworks' (Core/NativeHordeZones) ----
        private static void Registration(string root)
        {
            Check(NativeHordeKinds.Count==7&&NativeHordeKinds.All==NativeHordeKinds.Count+NativeHordeZones.Count&&NativeHordeZones.Count==NativeIronworksKinds.KindCount+NativeStationKinds.KindCount,"the Siren's seven stay Count; the zones' kinds follow them (All "+NativeHordeKinds.All+")");
            // Both zones registered side by side (Core/NativeHordeZones): the City Ironworks' five first, then the station's four.
            Check(NativeIronworksKinds.First==NativeHordeKinds.Count&&NativeStationKinds.First==NativeHordeZones.First(NativeStationKinds.Kinds)&&NativeStationKinds.First==NativeIronworksKinds.First+NativeIronworksKinds.KindCount&&NativeStationKinds.First+NativeStationKinds.KindCount==NativeHordeKinds.All,"the station's four follow the Ironworks' five");
            for(int k=0;k<NativeHordeKinds.All;k++)
            {
                bool iron=NativeIronworksKinds.Is(k),station=NativeStationKinds.Is(k);var z=NativeHordeZones.Of(k);
                Check(k<NativeHordeKinds.Count?z==null&&!iron&&!station:z!=null&&iron!=station&&z.Id==NativeHordeKinds.Ids[k]&&NativeHordeKinds.Resolve(z.Id)==k&&NativeHordeKinds.Known(z.Id)&&NativeHordeKinds.Shipped[k],"kind "+k+" ("+NativeHordeKinds.Ids[k]+") belongs to exactly one zone or to the Siren");
                Check(k>=NativeHordeKinds.Count||NativeHordeKinds.Base(k)==k&&NativeHordeKinds.Index(k)==k,"a Siren kind is its own base");
            }
            foreach(var len in new[]{NativeHordeKinds.Ids.Length,NativeHordeKinds.Names.Length,NativeHordeKinds.Shipped.Length,NativeHordeKinds.Looks.Length,NativeHordeKinds.Radius.Length,NativeHordeKinds.Height.Length,NativeHordeKinds.Axis.Length,NativeHordeKinds.Root.Length,NativeHordeKinds.CenterAhead.Length,NativeHordeKinds.Scale.Length,NativeHordeKinds.Mass.Length,NativeHordeKinds.Default.Length,NativeHordeKinds.Seam.Length,NativeHordeKinds.SeamClass.Length})
                Check(len==NativeHordeKinds.All,"every kind table holds all "+NativeHordeKinds.All+" kinds ("+len+")");
            for(int k=0;k<NativeHordeKinds.All;k++){NativeZombieMotion.Walks(k);NativeZombieMotion.Runs(k);NativeZombieMotion.Idles(k);var m=NativeHordeModels.Build(k,0,NativeHordeModels.Impostor);Check(m.Mesh.Triangles>0&&NativeHordeModels.Id(k,0,0)=="minion-"+NativeHordeKinds.Ids[k]+"-0-full","kind "+k+" builds, walks and is named in the bank");}
            string[] ids={"linesman","conductor","undertow","pileup"},names={"Linesman","Conductor","Undertow","Pileup"};int[] body={NativeHordeKinds.Brute,NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute};
            for(int i=0;i<4;i++)
            {
                int k=Kinds[i];Check(NativeHordeKinds.Ids[k]==ids[i]&&NativeHordeKinds.Names[k]==names[i]&&NativeHordeKinds.Resolve(ids[i])==k&&NativeHordeKinds.Known(ids[i]),ids[i]+" resolves to kind "+k+" is known to the authority and wire");
                Check(NativeHordeZones.Of(k)!=null&&NativeStationKinds.Is(k)&&NativeHordeKinds.Base(k)==body[i],ids[i]+" borrows its body tables from "+NativeHordeKinds.Ids[body[i]]);
                Check(NativeHordeKinds.Shipped[k],ids[i]+" ships with its bound zone content row");
                Check(NativeHordeKinds.Looks[k]>=1&&NativeHordeKinds.Looks[k]<=NativeHordeKinds.MostLooks,ids[i]+" looks within the bank's");
                Check(NativeHordeKinds.Root[k]==NativeHordeKinds.AddHeight*.5f&&Math.Abs(NativeHordeKinds.Height[k]-NativeHordeKinds.AddHeight)<1e-5f&&NativeHordeKinds.Axis[k]==1&&NativeHordeKinds.CenterAhead[k]==0,ids[i]+" drawn on the rig's own 1.7 m (its scale makes the capsule)");
                Check(Math.Abs(NativeHordeKinds.RootHeight(k,false)-NativeHordeKinds.WorldHeight(k)*.5f)<1e-5f,ids[i]+": the floor found from the authority's capsule centre");
            }
            for(int k=0;k<NativeHordeKinds.All;k++)for(int j=0;j<k;j++)Check(NativeHordeKinds.Ids[k]!=NativeHordeKinds.Ids[j],"kind ids are unique");
            // Nothing in today's content names them; every content id resolves to the kind it did.
            using var pve=JsonDocument.Parse(R(root,"Assets/Conversion/Resources/PveContent.json"));var contentIds=pve.RootElement.GetProperty("adds").EnumerateArray().Select(a=>a.GetProperty("id").GetString()).ToList();
            foreach(var id in ids)Check(!contentIds.Contains(id),"base Siren content has no "+id+" row; the zone binding supplies it");
            foreach(var id in contentIds)Check(NativeHordeKinds.Resolve(id)<NativeHordeKinds.Count,"content kind "+id+" still resolves to one of the seven");
            Check(NativeHordeKinds.Resolve("future-kind")==NativeHordeKinds.Shambler&&NativeHordeKinds.MixCount==NativeHordeKinds.Hulk,"unknown ids still draw as the shambler; the DEV mix still hashes over the first six");
            // The live maps (the live Expedition layout, which runs the Central Station and City Ironworks areas; its activation
            // snapshots; every Expedition draft fixture; the Unity maps): each kind an allow list names is known and resolves as at the
            // base, and each proposed station enemy is registered for zone content. Known is the authority's and the wire's
            // test (NativeNetworkFrame's minion validation, NativeDevMonsterAuthority's DEV spawns, NativeExpeditionLayout's allow
            // lists), so they accept and refuse exactly what they did; the split-area runtime carries no kind of its own.
            var seven=NativeHordeKinds.Ids.Take(NativeHordeKinds.Count).ToArray();int files=0,allowed=0,proposed=0;
            foreach(var file in new[]{"Assets/Conversion/Resources/ExpeditionLayout.json","Assets/Conversion/Resources/UnityMaps.json"}.Concat(Directory.GetFiles(Path.Combine(root,"Tests"),"expedition-*.json").OrderBy(f=>f,StringComparer.Ordinal).Select(f=>"Tests/"+Path.GetFileName(f))))
            {
                using var doc=JsonDocument.Parse(R(root,file));var allow=new List<string>();var station=new List<string>();Collect(doc.RootElement,null,allow,station);files++;
                foreach(var id in allow){int b=Array.IndexOf(seven,id);Check(NativeHordeKinds.Known(id)&&(b<0||NativeHordeKinds.Resolve(id)==b),file+": allowed kind "+id+" is known and resolves as at the base");allowed++;}
                foreach(var id in station){Check(ids.Contains(id)&&NativeHordeKinds.Known(id),file+": proposed "+id+" is registered for zone content");proposed++;}
            }
            Check(files>=10&&allowed>0&&proposed>0,"the live layout and fixtures were read ("+files+" files, "+allowed+" allowed, "+proposed+" proposed)");
            for(int k=0;k<NativeHordeKinds.Count;k++)Check(NativeHordeKinds.Known(NativeHordeKinds.Ids[k])&&NativeHordeKinds.Resolve(NativeHordeKinds.Ids[k])==k,NativeHordeKinds.Ids[k]+" is known and resolves to itself as at the base");
            foreach(var id in new[]{null,"","future-kind","Linesman","pileup "})Check(!NativeHordeKinds.Known(id),"Known still refuses "+(id??"null"));
        }
        private static void Collect(JsonElement e,string key,List<string> allow,List<string> station)
        {
            if(e.ValueKind==JsonValueKind.Object){foreach(var p in e.EnumerateObject())Collect(p.Value,p.Name,allow,station);}
            else if(e.ValueKind==JsonValueKind.Array){foreach(var v in e.EnumerateArray())if(v.ValueKind==JsonValueKind.String){if(key=="allow")allow.Add(v.GetString());else if(key=="station")station.Add(v.GetString());}else Collect(v,key,allow,station);}
        }
        // ---- today's seven exactly as at the base (every table, model, take and pose) ----
        private static void Unchanged(string root)
        {
            var want=File.ReadAllLines(Path.Combine(root,"Tests/station-enemies-baseline.txt")).Where(l=>!l.StartsWith("#")&&l.Length>0).Select(l=>{int s=l.IndexOf(' ');return new KeyValuePair<string,string>(l.Substring(0,s),l.Substring(s+1));}).ToList();
            var have=StationBaseline.Measure();Check(want.Count==have.Count&&want.Count>150,"the baseline lists every measure ("+have.Count+")");
            for(int i=0;i<want.Count;i++)
            {
                Check(want[i].Key==have[i].Key,"measure "+i+" is "+want[i].Key);var a=want[i].Value.Split(' ');var b=have[i].Value.Split(' ');Check(a.Length==b.Length,want[i].Key+" has its values");
                for(int j=0;j<a.Length;j++)
                {
                    double x=0,y=0;bool num=double.TryParse(a[j],NumberStyles.Float,CultureInfo.InvariantCulture,out x)&&double.TryParse(b[j],NumberStyles.Float,CultureInfo.InvariantCulture,out y);
                    // (Exact on this runtime; a platform's last-bit libm differences are allowed.)
                    Check(num?Math.Abs(x-y)<=1e-6*Math.Max(1,Math.Abs(x)):a[j]==b[j],want[i].Key+" unchanged ("+a[j]+" -> "+b[j]+")");
                }
            }
        }
        // ---- section 8's contract: capsules, weights, rows, seam hues ----
        private static void Contract(string doc)
        {
            string sec8=Between(doc,"## 8. Astra contract","### AI states");
            foreach(var id in new[]{"linesman","conductor","undertow","pileup"})Check(sec8.Contains("`"+id+"`"),"section 8 names "+id);
            // capsule (radius, height) | 0.48, 1.75 | 0.40, 1.9 | 0.42, 1.7 (...) | 0.75, 2.1 |
            var caps=Row(sec8,"capsule (radius, height)");float[][] want={new[]{.48f,1.75f},new[]{.40f,1.9f},new[]{.42f,1.7f},new[]{.75f,2.1f}};
            for(int i=0;i<4;i++){var nums=Nums(caps[i]);Check(Math.Abs(nums[0]-want[i][0])<1e-4f&&Math.Abs(nums[1]-want[i][1])<1e-4f,"the doc's capsule "+caps[i]);int k=Kinds[i];
                Check(Math.Abs(NativeHordeKinds.WorldRadius(k)-nums[0])<1e-4f&&Math.Abs(NativeHordeKinds.WorldHeight(k)-nums[1])<1e-4f,NativeHordeKinds.Ids[k]+" is drawn in section 8's capsule ("+NativeHordeKinds.WorldRadius(k)+", "+NativeHordeKinds.WorldHeight(k)+")");}
            var mass=Row(sec8,"mass (presentation)");for(int i=0;i<4;i++)Check(Math.Abs(Nums(mass[i])[0]-NativeHordeKinds.Mass[Kinds[i]])<1e-4f,NativeHordeKinds.Ids[Kinds[i]]+" weighs section 8's "+mass[i]);
            var hp=Row(sec8,"hp");var walk=Row(sec8,"walk / run (m/s)");var stag=Row(sec8,"stagger");var frac=Row(sec8,"limb / head fractions");var reward=Row(sec8,"reward");var melee=Row(sec8,"melee (damage, reach, hit, cycle)");
            for(int i=0;i<4;i++)
            {
                var p=NativeHordeKinds.ProposedRow(Kinds[i]);string id=p.id;Check(p.id==NativeHordeKinds.Ids[Kinds[i]],id+" proposed row");
                Check(Nums(hp[i])[0]==p.hp,id+" hp "+hp[i]);var wr=Nums(walk[i]);Check(wr[0]==p.walk&&wr[1]==p.run,id+" walk / run "+walk[i]);Check(Nums(stag[i])[0]==p.stagger,id+" stagger "+stag[i]);
                var lh=Nums(frac[i]);Check(lh[0]==p.limbFrac&&lh[1]==p.headFrac,id+" limb / head "+frac[i]);Check(Nums(reward[i])[0]==p.reward,id+" reward "+reward[i]);
                var m=Nums(melee[i]);
                if(id=="undertow")Check(m[0]==p.reach&&m[1]==p.hit&&m[2]==p.damage,id+" grab reach, hit and hold damage "+melee[i]);else Check(m[0]==p.damage&&m[1]==p.reach&&m[2]==p.hit&&m[3]==p.cycle,id+" melee "+melee[i]);
            }
            // Section 7's identity hues (none repeats a Siren kind's seam).
            string sec7=Between(doc,"## 7. The station's enemies","### 7.1");
            foreach(var (k,hex) in new[]{(NativeStationKinds.Kind(NativeStationKinds.Linesman),"#6FD8FF"),(NativeStationKinds.Kind(NativeStationKinds.Conductor),"#E8F0FF"),(NativeStationKinds.Kind(NativeStationKinds.Undertow),"#2EF0C0")})
            {Check(sec7.Contains(hex)&&NativeHordeKinds.Seam[k]==int.Parse(hex.Substring(1),NumberStyles.HexNumber),NativeHordeKinds.Ids[k]+" seam "+hex);for(int j=0;j<NativeHordeKinds.Count;j++)Check(NativeHordeKinds.Seam[j]!=NativeHordeKinds.Seam[k],"no seam repeats a Siren kind's");}
            Check(sec7.Contains("Pileup | none; its lit carriage window is the read"),"the Pileup has no seam hue: its window is the read");
            Check(doc.Contains("orange `#FF6A10`")&&NativeStationKinds.HiVis==0xFF6A10,"the Linesman's hi-vis orange");
        }
        // ---- section 7's telegraphs and specials, and the presentation's own curves on them ----
        private static void Timings(string doc)
        {
            if(doc!=null)
            {
                // (The doc's phrase, the number in it, the presentation's constant.)
                foreach(var (text,number,value) in new[]{("a 1.1 s wind-up",1.1f,NativeStationKinds.ArcWindup),("within 9 m",9f,NativeStationKinds.ArcRange),("within 4 m of each other",4f,NativeStationKinds.ArcChain),("40% slow for 0.35 s",.35f,NativeStationKinds.ArcJolt),
                    ("Cooldown 6 s",6f,NativeStationKinds.ArcCooldown),("(3 grounded)",3f,NativeStationKinds.ArcGroundedCooldown),("Hits from his rear 120°",120f,NativeStationKinds.BackCone),("40% then a hold",40f,40f),
                    ("a 0.8 s wind-up",.8f,NativeStationKinds.WhistleWindup),("within 14 m",14f,NativeStationKinds.WhistleRadius),("for 6 s",6f,NativeStationKinds.AboardSeconds),("Cooldown 12 s",12f,NativeStationKinds.WhistleCooldown),("every 20 s",20f,NativeStationKinds.LastCallCooldown),("(1.2 s\n    telegraph)",1.2f,NativeStationKinds.LastCallTelegraph),
                    ("0.6 s of bubbles",.6f,NativeStationKinds.GrabTelegraph),("The lunge reaches 2.6 m",2.6f,NativeStationKinds.GrabReach),("dragged at 2.7 m/s",2.7f,NativeStationKinds.DragSpeed),("lasts\n    up to 1.5 s",1.5f,NativeStationKinds.HoldSeconds),("it lasts up to 1.0 s",1f,NativeStationKinds.HoldSolo),("Cooldown 5 s after a release",5f,NativeStationKinds.GrabCooldown),
                    ("the root sunk 0.75 m",.75f,NativeStationKinds.Submerged),("1.3x speed",1.3f,NativeStationKinds.SurfaceRate),("the root lowers 1 m",1f,NativeStationKinds.DeathSink),("over 1.2 s)",1.2f,NativeStationKinds.DeathSinkSeconds),
                    ("a 0.7 s wind-up (it leans back",.7f,NativeStationKinds.ShoveWindup),("Then a 2.4 m",2.4f,NativeStationKinds.ShoveReach),("2.4 m, 70° cone",70f,NativeStationKinds.ShoveCone),("a\n    6 m/s knockback",6f,NativeStationKinds.ShoveKnock),("Cooldown 4 s",4f,NativeStationKinds.ShoveCooldown),("the front 70° (the door)",70f,NativeStationKinds.FrontCone)})
                {
                    Check(doc.Contains(text),"the doc says \""+text.Replace("\n    "," ")+"\"");Check(Math.Abs(number-value)<1e-5f,"the presentation's "+value+" is the doc's \""+text.Replace("\n    "," ")+"\"");
                }
                Check(doc.Contains("arc 18 + 2 chains x 10, 9 m, wind-up 1.1, cd 6 (3 grounded)")&&doc.Contains("whistle 14 m aura, wind-up 0.8, cd 12; last call cd 20")&&doc.Contains("grab drag 2.7 m/s, up to 1.5 s")&&doc.Contains("shove 2.4 m 70° cone, 20 damage, 6 m/s, wind-up 0.7, cd 4"),"section 8's special row as the presentation times it");
                Check(NativeStationKinds.ArcTargets==3&&doc.Contains("chains to up to 2 more pilots"),"the bolt strikes the primary and up to 2 chained pilots");
            }
            Check(NativeStationKinds.SpecialStrike(NativeStationKinds.Special.Arc)==NativeStationKinds.ArcWindup&&NativeStationKinds.SpecialStrike(NativeStationKinds.Special.Whistle)==NativeStationKinds.WhistleWindup&&NativeStationKinds.SpecialStrike(NativeStationKinds.Special.Grab)==NativeStationKinds.GrabTelegraph&&NativeStationKinds.SpecialStrike(NativeStationKinds.Special.Shove)==NativeStationKinds.ShoveWindup&&float.IsNaN(NativeStationKinds.SpecialStrike(NativeStationKinds.Special.None)),"each special strikes at its wind-up");
            Check(NativeHordeKinds.ProposedRow(NativeStationKinds.Kind(NativeStationKinds.Undertow)).hit==NativeStationKinds.GrabTelegraph,"the Undertow's grab (its content attack) lands at the telegraph's end");
            foreach(var s in new[]{NativeStationKinds.Special.Arc,NativeStationKinds.Special.Whistle,NativeStationKinds.Special.Grab,NativeStationKinds.Special.Shove})Check(NativeStationKinds.SpecialEnd(s)>NativeStationKinds.SpecialStrike(s),s+" plays out after its strike");
            // The arc: held telegraph to the strike, the whip starting exactly then, back to the gait by its end.
            float W=NativeStationKinds.ArcWindup;
            NativeStationMotion.ArcCurve(W-1e-3f,out float wind,out float whip,out float w);Check(whip==0&&wind>.99f&&w==1,"the arc's arm is held raised to the strike");
            NativeStationMotion.ArcCurve(W+.001f,out _,out whip,out _);Check(whip>0&&whip<.05f,"the whip starts at LastAttack + 1.1 s");
            NativeStationMotion.ArcCurve(W+NativeStationKinds.ArcWhip,out _,out whip,out _);Check(whip>.999f,"the whip completes .25 s after the strike");
            NativeStationMotion.ArcCurve(NativeStationKinds.SpecialEnd(NativeStationKinds.Special.Arc),out _,out _,out w);Check(w==0,"the arc hands back to the gait");
            NativeStationMotion.ArcCurve(float.NaN,out wind,out whip,out w);Check(wind+whip+w==0,"no arc without a replicated LastAttack");
            // The whistle: the inhale held to the blast at .8 s.
            NativeStationMotion.WhistleCurve(NativeStationKinds.WhistleWindup-1e-3f,out float inhale,out float blast,out w);Check(inhale>.99f&&blast==0&&w==1,"the inhale holds to the blast");
            NativeStationMotion.WhistleCurve(NativeStationKinds.WhistleWindup+.05f,out _,out blast,out _);Check(blast>.99f,"the blast at LastAttack + .8 s");
            Check(NativeStationMotion.LanternFlare(NativeStationKinds.WhistleWindup+.05f,float.NaN)>NativeStationMotion.LanternFlare(.1f,float.NaN)&&NativeStationMotion.LanternFlare(float.NaN,float.NaN)==1,"the lantern flares in the inhale, most at the blast");
            // The last call: raised within its 1.2 s, rung.
            NativeStationMotion.LastCallCurve(.6f,out float raise,out _,out _);NativeStationMotion.LastCallCurve(NativeStationKinds.LastCallTelegraph+.46f,out float after,out _,out _);Check(raise>.99f&&after==0,"the lantern raised through the last call's 1.2 s, lowered after");
            // The shove: the push take's impact exactly at the strike, its brace held before.
            NativeStationMotion.ShoveCurve(NativeStationKinds.ShoveWindup,out _,out float thrust,out _,out float ct);Check(Math.Abs(ct-NativeZombieClips.Clip(NativeZombieClips.Need("shove-1")).Marks[2])<1e-5f&&thrust>.99f,"the shove's push lands at LastAttack + .7 s");
            NativeStationMotion.ShoveCurve(.3f,out float brace,out thrust,out _,out _);Check(brace>.9f&&thrust==0,"the brace holds before the push");
            // The Undertow's surfacing on the crawl-to-stand at 1.3 times.
            Check(Math.Abs(NativeStationMotion.SurfaceSeconds-NativeZombieClips.Duration(NativeZombieMotion.CrawlStand)/1.3f)<1e-5f,"surfacing plays the crawl-to-stand at 1.3 times");
            Check(NativeStationKinds.SpecialOf(NativeStationKinds.Kind(NativeStationKinds.Linesman),"arc")==NativeStationKinds.Special.Arc&&NativeStationKinds.SpecialOf(NativeStationKinds.Kind(NativeStationKinds.Conductor),"whistle")==NativeStationKinds.Special.Whistle&&NativeStationKinds.SpecialOf(NativeStationKinds.Kind(NativeStationKinds.Undertow),"grab")==NativeStationKinds.Special.Grab&&NativeStationKinds.SpecialOf(NativeStationKinds.Kind(NativeStationKinds.Pileup),"shove")==NativeStationKinds.Special.Shove,"section 8's attack names start the specials");
            Check(NativeStationKinds.SpecialOf(NativeStationKinds.Kind(NativeStationKinds.Linesman),null)==NativeStationKinds.Special.None&&NativeStationKinds.SpecialOf(NativeStationKinds.Kind(NativeStationKinds.Linesman),"whistle")==NativeStationKinds.Special.None&&NativeStationKinds.SpecialOf(NativeHordeKinds.Brute,"arc")==NativeStationKinds.Special.None,"anything else is the content's melee");
        }
        // ---- the view: section 8's names, so Astra's binding is one line ----
        private static void Fields(string doc,string root)
        {
            var t=typeof(StationView);
            var want=new (string name,Type type)[]{("attack",typeof(string)),("arcTargets",typeof(int[])),("arcAt",typeof(float)),("grounded",typeof(bool)),("lanternBroken",typeof(bool)),("lastCallAt",typeof(float)),("aboardUntil",typeof(float)),
                ("submerged",typeof(bool)),("surfaceAt",typeof(float)),("grabTarget",typeof(int)),("grabAt",typeof(float)),("releaseAt",typeof(float)),("splitAt",typeof(float)),("childIds",typeof(int[]))};
            foreach(var (name,type) in want){var f=t.GetField(name,BindingFlags.Public|BindingFlags.Instance);Check(f!=null&&f.FieldType==type,"StationView."+name+" ("+type.Name+")");}
            Check(t.GetFields(BindingFlags.Public|BindingFlags.Instance).Length==want.Length,"the view holds section 8's fields and nothing else");
            if(doc!=null){string sec8=Between(doc,"### Networking: what the presentation needs","### What PR #254");foreach(var n in new[]{"arcTargets","arcAt","grounded","aboardUntil","lanternBroken","lastCallAt","submerged","surfaceAt","grabTarget","grabAt","releaseAt","splitAt"})Check(sec8.Contains("`"+n),"section 8 names `"+n+"`");
                Check(sec8.Contains("child ids")&&sec8.Contains("`arc`, `whistle`, `grab`, `shove`"),"section 8 names the split's child ids and the four attack names");}
            var v=StationView.None;Check(float.IsNaN(v.arcAt)&&float.IsNaN(v.lastCallAt)&&float.IsNaN(v.surfaceAt)&&float.IsNaN(v.grabAt)&&float.IsNaN(v.releaseAt)&&float.IsNaN(v.splitAt)&&float.IsNaN(v.aboardUntil)&&v.grabTarget==-1&&v.attack==null&&!v.grounded&&!v.submerged&&!v.lanternBroken&&!v.Aboard(0)&&!v.Holding(0),"StationView.None holds no event");
            v.grabTarget=3;v.grabAt=1;v.releaseAt=2.5f;Check(v.Holding(1.2f)&&!v.Holding(2.5f)&&!v.Holding(.9f),"a hold from grabAt to releaseAt");v.aboardUntil=7;Check(v.Aboard(1.1f)&&!v.Aboard(7)&&!v.Aboard(.9f),"the buff for its 6 s");
            var p=R(root,"Assets/ChipArena/Runtime/NativeStationKindsPresentation.cs");Check(Regex.Matches(p,@"^\s*internal static StationView StationViewOf\(NativeMinion m\)=>m\.Pve\?\.Zone\.Station\?\?StationView\.None;",RegexOptions.Multiline).Count==1&&Regex.Matches(p,@"StationViewOf\(m\)").Count>=4,"the binding is one line (StationViewOf, bound to replicated zone fields), and every live read goes through it");
        }
        // ---- section 10's enemy cues ----
        private static void Audio(string doc)
        {
            string sec=Between(doc,"### Enemy cues (P)","## 11.");var docKeys=Regex.Matches(sec,@"`(pve\.station\.[a-z]+\.[a-z]+)`").Select(m=>m.Groups[1].Value).ToList();
            Check(docKeys.Count==22&&docKeys.OrderBy(x=>x).SequenceEqual(NativeStationKinds.CueKeys.OrderBy(x=>x)),"the cue keys are section 10's 22");
            for(int i=0;i<NativeStationKinds.CueKeys.Length;i++)
            {
                var key=NativeStationKinds.CueKeys[i];Check(NativeSoundKeys.Find(key,out var def)&&def.Space==NativeSoundSpace.Positional&&def.Cap>=NativeStationKinds.CueCaps[i]-1e-4f,key+" is in the bank contract, positional, its cap "+NativeStationKinds.CueCaps[i]);
                var brief=Regex.Match(sec,"`"+Regex.Escape(key)+@"` \| [^|]*?([0-9.]+) s\.");if(brief.Success)Check(def.Cap>=float.Parse(brief.Groups[1].Value,CultureInfo.InvariantCulture)-1e-4f,key+" holds its brief's "+brief.Groups[1].Value+" s");
                var stand=NativeStationKinds.StandIn(key);if(stand!=null)Check(NativeSoundKeys.Known(stand)&&!stand.StartsWith(NativeStationKinds.Prefix,StringComparison.Ordinal),key+" borrows an existing key ("+stand+")");
            }
            foreach(int k in Kinds){var d=NativeStationKinds.DeathCue(k);Check(d!=null&&NativeStationKinds.CueKeys.Contains(d),NativeHordeKinds.Ids[k]+" has a death cue");}
        }
        // ---- the melee strikes: the gait's captured blow lands its impact at LastAttack + the row's hit ----
        private static void Strikes()
        {
            var pose=new CapturedRig.Pose();
            foreach(int k in Kinds)
            {
                var st=NativeHordeKinds.ProposedRow(k);var attack=NativeHordeKinds.AttackOf(k,st.attack,false);float H=NativeHordeKinds.Strike(st,false,0),Rc=NativeHordeKinds.Recover(st,false,0);
                Check(attack==NativeHordeKinds.Default[k],NativeHordeKinds.Ids[k]+"'s content attack "+st.attack+" is its kind's ("+attack+")");
                var g=new NativeHordeGait();var f=new HordeMotionInput{Kind=k,Id=70000+k,Seed=NativeHordeKinds.Seed(70000+k),Root=new Vector3(0,.85f,0),Health=1,Strike=H,Recover=Rc,Attack=attack,Side=1};
                for(int i=0;i<=60;i++){f.Time=i/60f;f.AttackAge=i<10?float.NaN:(i-10)/60f;g.Pose(f,pose);}
                f.Time=2;f.AttackAge=H;g.Pose(f,pose);Check(g.AttackClip>=0&&Math.Abs(g.AttackClipTime-NativeZombieClips.Clip(g.AttackClip).Marks[2])<1e-5f,NativeHordeKinds.Ids[k]+": the blow's impact exactly at LastAttack + "+H);
            }
        }
        // ---- models and props ----
        private static void Models()
        {
            foreach(int k in Kinds)for(int l=0;l<NativeHordeKinds.Looks[k];l++){var m=NativeHordeModels.Build(k,l,0);Check(m.Kind==k&&m.Mesh.Spec=="minion-"+k,NativeHordeKinds.Ids[k]+" builds its own model");var t=NativeHordeModels.Tongues(k,l);Check(t.Length==0,NativeHordeKinds.Ids[k]+" grows no black flame (the Siren's minions' own; NativeHordeModels.Tongues for every zone kind)");}
            // The identity lights are in the models: the headlamp and battery, the cold eyes, the aqua eyes, the window (the prop).
            bool Glows(WeaponMesh mesh,int hex,float least){var c=WeaponPaint.Linear(hex);foreach(var s in mesh.Sections)foreach(var e in s.Emission){var v=new Vector3(e.X,e.Y,e.Z);if(v.Length()<least)continue;var n=Vector3.Normalize(v);if(Vector3.Dot(n,Vector3.Normalize(c))>.98f)return true;}return false;}
            Check(Glows(NativeHordeModels.Build(NativeStationKinds.Kind(NativeStationKinds.Linesman),0,0).Mesh,0xEAF4FF,3)&&Glows(NativeHordeModels.Build(NativeStationKinds.Kind(NativeStationKinds.Linesman),0,0).Mesh,NativeStationKinds.LinesmanBlue,1),"the Linesman's headlamp and battery glow");
            Check(Glows(NativeHordeModels.Build(NativeStationKinds.Kind(NativeStationKinds.Undertow),0,0).Mesh,NativeStationKinds.UndertowAqua,1),"the Undertow's aqua eyes");
            foreach(NativeHordeModels.StationProp prop in Enum.GetValues(typeof(NativeHordeModels.StationProp)))
            {
                int[] budget={1200,800,240};int last=int.MaxValue;
                for(int tier=0;tier<NativeHordeModels.Tiers;tier++){var m=NativeHordeModels.BuildStationProp(prop,tier);Check(m.Triangles>0&&m.Triangles<=budget[tier]&&m.Triangles<=last,prop+" tier "+tier+" "+m.Triangles+" triangles");last=m.Triangles;
                    Check(m.Sections.All(s=>s.Bone==0)&&NativeHordeModels.StationPropId(prop,tier).StartsWith("station-"),prop+" is one piece");}
            }
            Check(Glows(NativeHordeModels.BuildStationProp(NativeHordeModels.StationProp.Lantern,0),NativeStationKinds.LanternRed,3)&&!Glows(NativeHordeModels.BuildStationProp(NativeHordeModels.StationProp.BrokenLantern,0),NativeStationKinds.LanternRed,1),"the lantern's red light, out once broken");
            Check(Glows(NativeHordeModels.BuildStationProp(NativeHordeModels.StationProp.Door,0),NativeStationKinds.WindowTube,2),"the door's lit window");
            // The door carried inside the Pileup's capsule (its shove reaches past it, as the shove does).
            var pose=new CapturedRig.Pose();var g=new NativeHordeGait();var mo=new NativeStationMotion();var o=new StationMotionOutput();int pk=NativeStationKinds.Kind(NativeStationKinds.Pileup);
            var f=new HordeMotionInput{Kind=pk,Id=71000,Seed=NativeHordeKinds.Seed(71000),Root=new Vector3(0,.85f,0),Health=1,AttackAge=float.NaN};float worst=0;
            for(int i=0;i<120;i++){f.Time=i/60f;g.Pose(f,pose);var s=new StationMotionInput{Kind=pk,Id=71000,Seed=f.Seed,Time=f.Time,Dt=1/60f,SpecialAge=float.NaN,MeleeAge=float.NaN,LastCallAge=float.NaN,SurfaceAge=float.NaN,HoldAge=float.NaN,ReleaseAge=float.NaN};mo.Pose(s,pose,ref o);
                var door=NativeHordeModels.BuildStationProp(NativeHordeModels.StationProp.Door,0);foreach(var sec in door.Sections)foreach(var v in sec.Positions){var w=o.PropAt+Vector3.Transform(v,o.PropTurn);worst=Math.Max(worst,new Vector2(w.X,w.Z).Length()-NativeHordeKinds.Radius[pk]);Check(w.Y>=NativeHordeGait.Floor-.01f,"the door never under the floor");}}
            Check(worst<.12f,"the carried door at the front of the Pileup's capsule ("+worst.ToString("0.000")+" m over)");
        }
        // ---- the takes: the station's exports, decoded, its kinds' own sets ----
        private static void Takes(string root)
        {
            using var prov=JsonDocument.Parse(R(root,"tools/pack-zombie-provenance.json"));var src=prov.RootElement.GetProperty("clips").EnumerateArray().ToDictionary(e=>e.GetProperty("clip").GetString(),e=>e.GetProperty("source").GetString());
            foreach(var (key,file) in new[]{("writhe-1","Zombie_OnFire_1_SHORT"),("death-shock","Zombie_OnFire_2A_SHORT"),("idle-1","Zombie_Idle_1_v2"),("reach-1","Zombie_Reach_1_Loop"),("crawl-1","Zombie_Crawl_1_Loop"),("crawl-2","Zombie_Crawl_2_Loop"),("shove-1","Zombie_Atk_Arms_4_SHORT_Loop")})
            {int c=NativeZombieClips.Find(key);Check(c>=0&&NativeZombieClips.Duration(c)>0&&src["zombie/"+key]==file+"_IPC",key+" exported from the pack's "+file);}
            var clip=NativeZombieClips.Clip(NativeZombieClips.Need("death-shock"));Check(clip.Marks[1]>1&&clip.Marks[1]<clip.Marks[2],"the electrocuted collapse's head reaches the floor late in its take");
            string[][] sets={new[]{"walk-6","chase-2","idle-3"},new[]{"walk-4","chase-1","idle-1"},new[]{"chase-2","chase-5","shamble-2"},new[]{"walk-4","walk-4","idle-3"}};
            for(int i=0;i<4;i++){int k=Kinds[i];Check(NativeZombieMotion.Captured(k)&&NativeZombieClips.Key(NativeZombieMotion.Walks(k)[0])==sets[i][0]&&NativeZombieClips.Key(NativeZombieMotion.Runs(k)[0])==sets[i][1]&&NativeZombieClips.Key(NativeZombieMotion.Idles(k)[0])==sets[i][2],NativeHordeKinds.Ids[k]+" moves on its own takes");}
            Check(NativeHordeZones.Tempo(NativeStationKinds.Kind(NativeStationKinds.Pileup),NativeZombieMotion.Tempo)<NativeHordeZones.Tempo(NativeStationKinds.Kind(NativeStationKinds.Linesman),NativeZombieMotion.Tempo)&&NativeHordeZones.Tempo(NativeStationKinds.Kind(NativeStationKinds.Pileup),NativeZombieMotion.Tempo)==.7f,"the Pileup's heavy shuffle (walk-4 at .7)");
            // Deaths: each falls to the floor and stays there; the Undertow sinks its metre.
            var pose=new CapturedRig.Pose();foreach(int k in Kinds){Check(NativeStationMotion.Death(pose,k,5,1.6f,out float sink),NativeHordeKinds.Ids[k]+" has its death");Check(pose.Lowest()>NativeHordeGait.Floor-.01f&&pose.Lowest()<NativeHordeGait.Floor+.01f,NativeHordeKinds.Ids[k]+" lies on the floor");
                if(NativeStationKinds.Local(k)==NativeStationKinds.Undertow)Check(sink>.9f&&sink<=1,"the Undertow sinks 1 m");else Check(sink==0,NativeHordeKinds.Ids[k]+" does not sink");
                pose.World();float head=pose.WorldPosition[CapturedRig.Head].Y;NativeStationMotion.Death(pose,k,5,.05f,out _);pose.World();Check(pose.WorldPosition[CapturedRig.Head].Y>head+.3f,NativeHordeKinds.Ids[k]+" falls from standing");}
            Check(!NativeStationMotion.Death(pose,NativeHordeKinds.Brute,5,1,out _),"the Siren's kinds keep their own deaths");
            NativeStationMotion.DoorFall(1,out var at,out var turn);var up=Vector3.Transform(Vector3.UnitY,turn);Check(Math.Abs(up.Y)<.05f&&at.Y<NativeHordeGait.Floor+.06f,"the Pileup's door lies flat on the floor");
            NativeStationMotion.LanternFall(1,new Vector3(-.3f,-.2f,.2f),out at,out _);Check(at.Y<NativeHordeGait.Floor+.45f,"the lantern falls to the floor");
        }
        // ---- effects: deterministic, bounded ----
        private static void Effects()
        {
            var a=new Vector3[NativeStationFx.BoltPoints];var b=new Vector3[NativeStationFx.BoltPoints];
            int n=NativeStationFx.Bolt(7,1,.5f,new Vector3(0,1,0),new Vector3(0,1,6),.11f,a);NativeStationFx.Bolt(7,1,.5f,new Vector3(0,1,0),new Vector3(0,1,6),.11f,b);
            Check(n==NativeStationFx.BoltPoints&&a.SequenceEqual(b)&&a[0]==new Vector3(0,1,0)&&a[n-1]==new Vector3(0,1,6),"the bolt is seeded and runs end to end");
            Check(a.Skip(1).Take(n-2).All(p=>new Vector2(p.X,p.Y-1).Length()<=.11f*(float)Math.Sqrt(6)*1.4143f+1e-4f),"its jag bounded by its amplitude (each axis)");
            NativeStationFx.Bolt(7,1,.5f+1f/NativeStationFx.BoltRate,new Vector3(0,1,0),new Vector3(0,1,6),.11f,b);Check(!a.SequenceEqual(b),"it re-jags every frame of its rate");
            Check(NativeStationFx.BoltLife(0)>.9f&&NativeStationFx.BoltLife(NativeStationKinds.BoltSeconds)==0&&NativeStationFx.BoltLife(-.01f)==0,"the bolt flashes at its strike and is gone by its seconds");
            Check(NativeStationFx.Jolt(.1f)>0&&NativeStationFx.Jolt(NativeStationKinds.ArcJolt+.2f)==0,"the jolt for the slow's .35 s");
            Check(NativeStationFx.WhistleRing(.35f,out float r,out float al)&&Math.Abs(r-NativeStationKinds.WhistleRadius)<1e-4f&&al>0&&!NativeStationFx.WhistleRing(.61f,out _,out _),"the whistle's ring reaches the aura's 14 m");
            Check(NativeStationFx.BellRing(.3f,0,out _,out _)&&!NativeStationFx.BellRing(.1f,0,out _,out _)&&!NativeStationFx.BellRing(2,20,out _,out _),"the bell rings during the last call");
            for(int i=0;i<30;i++){if(NativeStationFx.Converge(3,i,1f,new Vector3(0,0,0),new Vector3(0,0,2.6f),out var p,out _))Check(Math.Abs(p.Z-2.6f)<.05f,"the bubbles arrive at the target's feet");}
            for(int i=0;i<16;i++)if(NativeStationFx.ShoveDust(5,i,.3f,out var p,out _,out _)){float ang=(float)(Math.Atan2(p.X,p.Z)*180/Math.PI);Check(Math.Abs(ang)<=NativeStationKinds.ShoveCone*.5f+1e-3f&&new Vector2(p.X,p.Z).Length()<=NativeStationKinds.ShoveReach+1e-3f,"the shove's dust inside its 2.4 m, 70 degree cone");}
            for(int i=0;i<28;i++)if(NativeStationFx.SplitChunk(9,i,1.2f,out var p,out _,out _))Check(p.Y>=0,"the split's pieces rest on the floor");
            Check(NativeStationFx.WindowFlicker(4,1,.05f)!=NativeStationFx.WindowFlicker(4,1,float.NaN)||NativeStationFx.WindowFlicker(4,1,.1f)!=NativeStationFx.WindowFlicker(4,1,float.NaN),"a hit stutters the window");
            Check(NativeStationFx.AboardFlash(0)==1&&NativeStationFx.AboardFlash(.5f)==0&&NativeStationFx.AboardTrail(3)>.99f&&NativeStationFx.AboardTrail(NativeStationKinds.AboardSeconds+.1f)==0,"the buff's flash and trail");
            // The headlamp's beam stops at the floor (and lays its pool there), never runs past its length, and fades end-on.
            Check(Math.Abs(NativeStationFx.BeamReach(1.8f,.6f,out bool lands)-3)<1e-5f&&lands&&NativeStationFx.BeamReach(1.8f,.1f,out lands)==NativeStationFx.BeamLength&&!lands&&NativeStationFx.BeamReach(1.8f,-.3f,out lands)==NativeStationFx.BeamLength&&!lands,"the beam lands on the floor within its length");
            Check(NativeStationFx.BeamAlpha(0,0)>0&&NativeStationFx.BeamAlpha(1,0)>NativeStationFx.BeamAlpha(0,0)&&NativeStationFx.BeamAlpha(1,.99f)<.02f&&NativeStationFx.BeamAlpha(1,-1)==0,"the beam brightens with the charge and fades when seen end-on");
            for(float c=0;c<=1;c+=.25f)for(float f=0;f<=1;f+=.25f){NativeStationFx.Lamp(c,f,out float ls,out float lg);NativeStationFx.Pack(f>.5f,c,f,out float ps,out float pg);NativeStationFx.Eyes(f>.5f,c,f,out float es,out float eg);
                Check(ls>0&&ls<1.2f&&lg>0&&ps>0&&ps<1.2f&&pg>0&&es>0&&es<1&&eg>0,"the identity halos stay inside a metre or so at every charge and distance");}
        }
        // ---- no allocation per frame: every state of every kind, the effects ----
        private static void Allocation()
        {
            var pose=new CapturedRig.Pose();var gaits=new NativeHordeGait[4];var motions=new NativeStationMotion[4];var outs=new StationMotionOutput[4];var bolt=new Vector3[NativeStationFx.BoltPoints];
            for(int i=0;i<4;i++){gaits[i]=new NativeHordeGait();motions[i]=new NativeStationMotion();}
            void Frame(int fr)
            {
                float t=fr/60f;
                for(int i=0;i<4;i++)
                {
                    int k=Kinds[i];var st=NativeHordeKinds.ProposedRow(k);float strike=NativeHordeKinds.Strike(st,false,0),recover=NativeHordeKinds.Recover(st,false,0);int phase=fr/240%4;
                    var f=new HordeMotionInput{Kind=k,Id=72000+k,Seed=NativeHordeKinds.Seed(72000+k),Time=t,Root=new Vector3(0,.85f,t*.9f),Velocity=new Vector3(0,0,.9f),Health=1,AttackAge=phase==1?(fr%120)/60f:float.NaN,Strike=strike,Recover=recover,Attack=NativeHordeKinds.Default[k],Side=1,HasTarget=true,Target=new Vector3(1,.85f,t+3)};
                    gaits[i].Pose(f,pose);
                    var sp=phase==2?(i==0?NativeStationKinds.Special.Arc:i==1?NativeStationKinds.Special.Whistle:i==2?NativeStationKinds.Special.Grab:NativeStationKinds.Special.Shove):NativeStationKinds.Special.None;
                    var s=new StationMotionInput{Kind=k,Id=72000+k,Seed=f.Seed,Time=t,Dt=1/60f,Special=sp,SpecialAge=sp!=NativeStationKinds.Special.None?(fr%120)/60f:float.NaN,MeleeAge=f.AttackAge,MeleeStrike=strike,Melee=f.Attack,Speed=gaits[i].Speed,
                        Grounded=phase==3,Submerged=phase==3&&i==2,LastCallAge=phase==3&&i==1?(fr%100)/60f:float.NaN,SurfaceAge=phase==0&&i==2?(fr%180)/60f:float.NaN,HoldAge=phase==1&&i==2?(fr%90)/60f:float.NaN,ReleaseAge=phase==3&&i==2?(fr%50)/60f:float.NaN,
                        HasAim=true,Aim=new Vector3(0,.4f,4),HasHold=true,Hold=new Vector3(0,-.75f,1.2f)};
                    motions[i].Pose(s,pose,ref outs[i]);NativeStationMotion.Death(pose,k,9,(fr%140)/60f,out _);
                }
                NativeStationFx.Bolt(5,fr&3,t,Vector3.Zero,new Vector3(0,1,5),.1f,bolt);NativeStationFx.Fork(5,1,0,t,bolt,bolt.Length,out _,out _);NativeStationFx.Spark(3,fr%20,t%.3f,4,Vector3.UnitY,.6f,out _,out _,out _);
                NativeStationFx.Ripple(fr%3,t,.7f,1.3f,out _,out _);NativeStationFx.Converge(4,fr%30,t%1,Vector3.Zero,Vector3.UnitZ,out _,out _);NativeStationFx.Splash(4,fr%40,t%.8f,out _,out _,out _);NativeStationFx.Drip(4,1,t,out _,out _);
                NativeStationFx.ShoveDust(6,fr%16,t%1,out _,out _,out _);NativeStationFx.SplitChunk(6,fr%28,t%1.4f,out _,out _,out _);NativeStationFx.Shard(6,fr%14,t%.9f,out _,out _,out _);
                NativeStationMotion.DoorFall(t%1,out _,out _);NativeStationMotion.LanternFall(t%1,Vector3.Zero,out _,out _);NativeStationMotion.ShoveCurve(t%1.5f,out _,out _,out _,out _);
            }
            for(int i=0;i<1000;i++)Frame(i);
            long before=GC.GetAllocatedBytesForCurrentThread();for(int i=1000;i<3000;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation per frame over 2000 frames of the four walking, striking, in every special and state, dying ("+used+" bytes)");
        }
        // ---- the runtime: presentation only, hooked where the horde is drawn ----
        private static void Runtime(string root)
        {
            var p=R(root,"Assets/ChipArena/Runtime/NativeStationKindsPresentation.cs");
            foreach(var bad in new[]{".Hp=","Damage(","Emit(","StaggerAt=","RecoverAt=","StrikeAt=","LastAttack=","Yaw=m.","m.Yaw=",".radius=",".height=","Collider.enabled","Body.position=","linearVelocity=","isKinematic=","AddComponent<","Pve.Station"})Check(!p.Contains(bad),"the station presentation never writes `"+bad+"`");
            var audio=p.Substring(p.IndexOf("public sealed partial class NativeAudio",StringComparison.Ordinal));audio=audio.Substring(0,audio.IndexOf("public sealed partial class NativeSourceModels",StringComparison.Ordinal));
            Check(audio.Split("Play(").Length==1&&audio.Contains("CreatureCue(key,NativeStationKinds.StandIn(key)"),"the station cues are bank clips or existing stand-ins (CreatureCue), never synthesized or other sounds");
            var k=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            foreach(var hook in new[]{"StationAttack(m,kind,now,ref age);","StationInput(ref f,kind);","StationPosed(arena,m,s,kind,now,ground,f,ref pos);","StationGlow(m,kind,now,ref glow);","StationQueue(rig,m,kind,s.Tier,a.Lost,glow,flash,now);",
                "float stationSink=StationDeathPose(d.Kind,d.Seed,fx);","floorY+NativeHordeKinds.DrawRoot(d.Kind,false)-stationSink","StationDeathFx(death,d.Kind,d.Seed,fx,rig,floorY);","DrawStationFx(draw,arena,now,eye,right,up,low);","StationGallery(me,now,galleryAt,across,yawToMe,ground);",
                "ClearStationGallery();","ClearStationKinds();","StationKindsEnd(now);","StationCues(m,kind,pos,t);","StationEvents(arena,t);","StationCuesClear();","&&!StationSpecialNow(m,kind)&&"})Check(k.Contains(hook),"the horde presentation calls `"+hook+"`");
            Check(R(root,"Assets/ChipArena/Runtime/NativeHordeKindModels.cs").Contains("StationPropModels(shader);"),"the props are built with the bank");
            foreach(var f in new[]{"NativeStationKinds.cs","NativeStationModels.cs","NativeStationMotion.cs","NativeStationFx.cs"}){var t=R(root,"Assets/ChipArena/Core/"+f);Check(!t.Contains("NativeArena")&&!t.Contains("NativePilot")&&!t.Contains("new Random(")&&!t.Contains("UnityEngine"),f+" reads no authority state and no clock-free randomness");}
        }

        // ---- the doc's tables ----
        private static string Between(string doc,string a,string b){int i=doc.IndexOf(a,StringComparison.Ordinal);Check(i>=0,"the doc has \""+a+"\"");int j=doc.IndexOf(b,i+a.Length,StringComparison.Ordinal);Check(j>i,"the doc has \""+b+"\" after it");return doc.Substring(i,j-i);}
        // A row of section 8's table: its four cells (Linesman, Conductor, Undertow, Pileup).
        private static string[] Row(string sec,string label){var line=sec.Split('\n').FirstOrDefault(l=>l.StartsWith("| "+label+" |",StringComparison.Ordinal));Check(line!=null,"section 8 has the row "+label);var cells=line.Split('|').Select(c=>c.Trim()).Where(c=>c.Length>0).ToArray();Check(cells.Length==5,label+" has four cells");return cells.Skip(1).ToArray();}
        private static float[] Nums(string cell)=>Regex.Matches(cell,@"[0-9]+(\.[0-9]+)?").Select(m=>float.Parse(m.Value,CultureInfo.InvariantCulture)).ToArray();
    }
}
#endif
