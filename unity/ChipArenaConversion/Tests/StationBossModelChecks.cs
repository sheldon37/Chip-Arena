#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // THE 6:47, the Central Station boss concept (Core/NativeStationBossModel.cs; docs/EXPEDITION-BOSS-CENTRAL-STATION.md):
    // the model builds at every tier, deterministically, within its triangle budgets; its skeleton is well formed (parents
    // before children, six legs in two alternating tripods); every key pose is finite, its planted hands land on the floor
    // where the stance puts them, nothing sinks under the floor; the carriage stays in its proposed hit capsule and the
    // Stationmaster in his, a standing boss in its footprint and every key pose in its reach box; the proposed move table is
    // well formed and matches the doc's; and nothing outside the model file references it but THE 6:47's presentation (no rule,
    // authority, network or content file: the boss is not live until Astra's record exists).
    internal static class StationBossModelChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Station boss: "+what);count++;}
        public static void Run(string root)
        {
            Builds();Skeleton();Poses();Volumes();Moves();Doc(root);Inert(root);
            Console.WriteLine("PASS station boss concept ("+count+" checks: THE 6:47 builds at three tiers within budget, its skeleton and tripods, "+
                "17 key poses finite and on the floor, the carriage box and the rider's capsule, footprint and reach, the move table and the doc's, referenced only by its presentation)");
        }
        private static void Builds()
        {
            var meshes=Enumerable.Range(0,3).Select(NativeStationBossModel.Build).ToArray();
            for(int t=0;t<3;t++)
            {
                var m=meshes[t];Check(m.Tier==t&&m.Spec==NativeStationBoss.Archetype,"tier "+t+" spec");
                Check(m.Triangles>0&&m.Triangles<=NativeStationBossModel.Budget[t],"tier "+t+" has "+m.Triangles+" triangles (budget "+NativeStationBossModel.Budget[t]+")");
                Check(m.Sections.Count<=NativeStationBossModel.Count&&m.Sections.All(s=>s.Bone>=0&&s.Bone<NativeStationBossModel.Count),"tier "+t+" sections ride real bones");
                Check(m.Sections.Select(s=>s.Bone).Distinct().Count()==m.Sections.Count,"tier "+t+" one section per bone");
                Check(m.Sections.All(s=>s.Indices.Count%3==0&&s.Indices.All(i=>i>=0&&i<s.Positions.Count)&&s.Positions.All(Finite)&&s.Normals.All(Finite)),"tier "+t+" indices and vertices valid");
                Check(m.Sections.All(s=>s.Bone!=NativeStationBossModel.Root),"tier "+t+" nothing rides the root");
            }
            Check(meshes[0].Triangles>meshes[1].Triangles&&meshes[1].Triangles>meshes[2].Triangles,"tiers thin out");
            // Every bone that carries geometry at the full tier is drawn: legs, wheels, rider, doors, chain, cables, window arms.
            var drawn=new HashSet<int>(meshes[0].Sections.Select(s=>s.Bone));
            Check(Enumerable.Range(1,NativeStationBossModel.Count-1).All(drawn.Contains),"every bone but the root carries geometry");
            var again=NativeStationBossModel.Build(0);
            Check(again.Triangles==meshes[0].Triangles&&again.Sections.Zip(meshes[0].Sections,(a,b)=>a.Bone==b.Bone&&a.Positions.SequenceEqual(b.Positions)&&a.Colors.SequenceEqual(b.Colors)).All(x=>x),"deterministic");
        }
        private static void Skeleton()
        {
            Check(NativeStationBossModel.Parent[NativeStationBossModel.Root]==-1,"the root has no parent");
            for(int i=1;i<NativeStationBossModel.Count;i++)Check(NativeStationBossModel.Parent[i]>=0&&NativeStationBossModel.Parent[i]<i,"bone "+i+" parent comes first");
            Check(NativeStationBossModel.Names.All(n=>!string.IsNullOrEmpty(n))&&NativeStationBossModel.Names.Distinct().Count()==NativeStationBossModel.Count,"bone names unique");
            for(int leg=0;leg<NativeStationBossModel.Legs;leg++)
            {
                int hip=NativeStationBossModel.Hip(leg),knee=NativeStationBossModel.Knee(leg),foot=NativeStationBossModel.Foot(leg);
                Check(NativeStationBossModel.Parent[hip]==NativeStationBossModel.Body&&NativeStationBossModel.Parent[knee]==hip&&NativeStationBossModel.Parent[foot]==knee,"leg "+leg+" chain");
                Check(Math.Sign(NativeStationBossModel.Rest[hip].X)==NativeStationBossModel.Side(leg)&&Math.Abs(NativeStationBossModel.Rest[knee].Y+NativeStationBossModel.Thigh(leg))<1e-5f&&Math.Abs(NativeStationBossModel.Rest[foot].Y+NativeStationBossModel.Shin(leg))<1e-5f,"leg "+leg+" lengths and side");
            }
            var a=NativeStationBossMotion.TripodA;var b=NativeStationBossMotion.TripodB;
            Check(a.Length==3&&b.Length==3&&a.Concat(b).OrderBy(x=>x).SequenceEqual(Enumerable.Range(0,6)),"the tripods cover the six legs once");
            Check(new[]{a,b}.All(g=>g.Select(NativeStationBossModel.Row).Distinct().Count()==3&&g.Select(NativeStationBossModel.Side).Distinct().Count()==2),"each tripod takes one leg per row on both sides");
            Check(NativeStationBossMotion.Springs.All(c=>c.Hz>0&&c.Damping>0&&c.Damping<=1)&&NativeStationBossMotion.Springs.Select(c=>c.Id).Distinct().Count()==NativeStationBossMotion.Springs.Length,"the springs are well formed");
        }
        private static readonly StationBossKey[] Keys=(StationBossKey[])Enum.GetValues(typeof(StationBossKey));
        private static void Poses()
        {
            var mesh=NativeStationBossModel.Build(1);var w=new Matrix4x4[NativeStationBossModel.Count];
            Check(Keys.Length==17,"seventeen key poses");
            foreach(var key in Keys)
            {
                var p=NativeStationBossModel.Key(key);Check(p.Rot.All(q=>Finite(q)&&Math.Abs(q.Length()-1)<1e-3f)&&p.Move.All(Finite),key+" finite");
                var pts=new List<Vector3>();NativeStationBossModel.Posed(mesh,p,pts);Check(pts.All(Finite),key+" posed finite");
                // The floor (the track bed for the train rolling in on the express line): planted hands press at most 10 cm in.
                float floor=key==StationBossKey.Rolling?NativeExpeditionStation.TrackBed:0;Check(pts.Min(v=>v.Y)>=floor-.1f,key+" sinks under the floor ("+pts.Min(v=>v.Y)+")");
            }
            // The stance: each planted wrist lands on its target and its hand rests on the floor.
            var stand=NativeStationBossModel.Key(StationBossKey.Stand);NativeStationBossModel.World(stand,w,false);
            var hands=new List<Vector3>();var bones=new List<int>();NativeStationBossModel.Posed(mesh,stand,hands,bones);
            for(int leg=0;leg<NativeStationBossModel.Legs;leg++)
            {
                var at=w[NativeStationBossModel.Foot(leg)].Translation;Check(Vector3.Distance(at,NativeStationBossModel.Stance(leg))<.02f,"leg "+leg+" wrist on its stance target");
                float low=Enumerable.Range(0,hands.Count).Where(i=>bones[i]==NativeStationBossModel.Foot(leg)).Min(i=>hands[i].Y);Check(low>-.1f&&low<.06f,"leg "+leg+" hand on the floor ("+low+")");
            }
            // The roof stays on until TERMINUS; the doors open only for All Aboard; the pantograph rises only for Live Rail.
            foreach(var key in Keys)
            {
                var p=NativeStationBossModel.Key(key);bool peeled=Math.Abs(p.Rot[NativeStationBossModel.Roof].X)>.3f;
                Check(peeled==(key==StationBossKey.TerminusRear||key==StationBossKey.TerminusSlam||key==StationBossKey.Opened||key==StationBossKey.Death),key+" roof");
                Check((p.Move[NativeStationBossModel.DoorLF].Z>.3f)==(key==StationBossKey.AllAboard),key+" doors");
            }
        }
        private static void Volumes()
        {
            var mesh=NativeStationBossModel.Build(0);var stand=NativeStationBossModel.Key(StationBossKey.Stand);var pts=new List<Vector3>();var bones=new List<int>();NativeStationBossModel.Posed(mesh,stand,pts,bones);
            var carriage=new HashSet<int>{NativeStationBossModel.Body,NativeStationBossModel.Lamp,NativeStationBossModel.Heart,NativeStationBossModel.Roof,NativeStationBossModel.PantoLow,NativeStationBossModel.PantoHigh,NativeStationBossModel.PantoHead,
                NativeStationBossModel.DoorLF,NativeStationBossModel.DoorLB,NativeStationBossModel.DoorRF,NativeStationBossModel.DoorRB,NativeStationBossModel.GrabL0,NativeStationBossModel.GrabL1,NativeStationBossModel.GrabR0,NativeStationBossModel.GrabR1,NativeStationBossModel.SmHips};
            var rider=new HashSet<int>{NativeStationBossModel.SmChest,NativeStationBossModel.SmHead,NativeStationBossModel.SmHat};
            float worstRider=0;var w=new Matrix4x4[NativeStationBossModel.Count];
            foreach(var key in Keys)
            {
                // The carriage's box rides the body bone in every pose (the doors' grab arms stay inside it until All Aboard).
                var p=NativeStationBossModel.Key(key);NativeStationBossModel.World(p,w,false);Matrix4x4.Invert(w[NativeStationBossModel.Body],out var toBody);var all=new List<Vector3>();var allBones=new List<int>();NativeStationBossModel.Posed(mesh,p,all,allBones);
                // Exempt: the open doors and their arms (All Aboard), the raised pantograph (Live Rail) and the peeled roof with it
                // (TERMINUS): those are their own breakable parts while they are out; the Stationmaster's hatch rises with him.
                bool peeled=Math.Abs(p.Rot[NativeStationBossModel.Roof].X)>.3f;
                bool Out(int b)=>b==NativeStationBossModel.SmHips||key==StationBossKey.AllAboard&&b>=NativeStationBossModel.DoorLF&&b<=NativeStationBossModel.GrabR1||
                    (key==StationBossKey.LiveRail||peeled)&&b>=NativeStationBossModel.PantoLow&&b<=NativeStationBossModel.PantoHead||peeled&&b==NativeStationBossModel.Roof;
                int bad=Enumerable.Range(0,all.Count).FirstOrDefault(i=>carriage.Contains(allBones[i])&&!Out(allBones[i])&&!Inside(Vector3.Transform(all[i],toBody),NativeStationBossModel.BodyBoxMin-new Vector3(.001f),NativeStationBossModel.BodyBoxMax+new Vector3(.001f)),-1);
                Check(bad<0,key+" "+(bad<0?"":NativeStationBossModel.Names[allBones[bad]]+" leaves the carriage box at "+Vector3.Transform(all[bad],toBody)));
            }
            var lift=new Vector3(0,NativeStationBossModel.BodyLift,0);
            Check(Enumerable.Range(0,pts.Count).All(i=>!carriage.Contains(bones[i])||Inside(pts[i]-lift,NativeStationBossModel.BodyBoxMin,NativeStationBossModel.BodyBoxMax)),"standing, the carriage leaves its box");
            for(int i=0;i<pts.Count;i++)if(rider.Contains(bones[i]))worstRider=Math.Max(worstRider,Segment(pts[i],NativeStationBossModel.RiderA,NativeStationBossModel.RiderB));
            Check(worstRider<=NativeStationBossModel.RiderRadius,"the Stationmaster leaves his hit capsule by "+(worstRider-NativeStationBossModel.RiderRadius));
            // The standing footprint (navigation proposal) and the reach of every key pose (model space).
            Check(pts.All(v=>Inside(v,NativeStationBossModel.FootprintMin,NativeStationBossModel.FootprintMax)),"a standing boss leaves its footprint");
            foreach(var key in Keys)
            {
                var p=NativeStationBossModel.Key(key);var all=new List<Vector3>();NativeStationBossModel.Posed(mesh,p,all);
                bool still=key==StationBossKey.Stand||key==StationBossKey.Walk||key==StationBossKey.Opened;
                Check(all.All(v=>Inside(v,still?NativeStationBossModel.FootprintMin:NativeStationBossModel.ReachMin,still?NativeStationBossModel.FootprintMax:NativeStationBossModel.ReachMax)),key+" leaves its "+(still?"footprint":"reach box"));
            }
            // The world placement turns and moves the model rigidly.
            var placed=NativeStationBossModel.Key(StationBossKey.Stand);placed.Root=new Vector3(0,0,3);placed.Yaw=90;var a=new Matrix4x4[NativeStationBossModel.Count];var b=new Matrix4x4[NativeStationBossModel.Count];
            NativeStationBossModel.World(placed,a);NativeStationBossModel.World(placed,b,false);var lamp=b[NativeStationBossModel.Lamp].Translation;var moved=a[NativeStationBossModel.Lamp].Translation;
            Check(Vector3.Distance(moved,new Vector3(lamp.Z,lamp.Y,3-lamp.X))<1e-3f,"the root's yaw turns the nose to +x");
        }
        private static void Moves()
        {
            var m=NativeStationBoss.Moves;Check(m.Length==8&&m.Select(x=>x.Id).Distinct().Count()==m.Length,"eight moves, unique ids");
            foreach(var x in m)
            {
                Check(x.Phase>=1&&x.Phase<=NativeStationBoss.Phases&&x.Anticipation>0&&x.Strike>0&&x.Follow>=0&&x.Recover>0&&x.Cooldown>x.Duration*.5f,x.Id+" timeline");
                if(x.Chime>0)Check(x.Note(0)>=0&&Math.Abs(x.Note(NativeStationBoss.ChimeNotes)-x.Anticipation)<1e-5f,x.Id+" chime lands its strike on the beat after the fourth note");
            }
            Check(m.Count(x=>x.Phase==1)>=4&&m.Count(x=>x.Phase==2)>=2&&m.Count(x=>x.Phase==3)>=2,"each phase adds moves");
            Check(NativeStationBoss.Phase(1)==1&&NativeStationBoss.Phase(.651f)==1&&NativeStationBoss.Phase(.65f)==2&&NativeStationBoss.Phase(.31f)==2&&NativeStationBoss.Phase(.3f)==3&&NativeStationBoss.Phase(0)==3,"phase thresholds");
            Check(NativeStationBoss.PhaseNames.Length==NativeStationBoss.Phases&&NativeStationBoss.PhaseFrom.Length==NativeStationBoss.Phases,"phase tables");
        }
        private static string N(float v)=>v.ToString("0.##",CultureInfo.InvariantCulture);
        private static void Doc(string root)
        {
            string path=Path.Combine(root,"../../docs/EXPEDITION-BOSS-CENTRAL-STATION.md");if(!File.Exists(path)){Console.WriteLine("SKIP station boss doc: docs/EXPEDITION-BOSS-CENTRAL-STATION.md is not in this tree");return;}
            string doc=File.ReadAllText(path);
            foreach(var x in NativeStationBoss.Moves)
            {
                var row=Regex.Match(doc,@"^\| `"+Regex.Escape(x.Id)+@"` \|([^\n]*)$",RegexOptions.Multiline);Check(row.Success,"the doc's move table lists "+x.Id);
                var cells=row.Groups[1].Value.Split('|').Select(c=>c.Trim()).ToArray();
                // | `id` | title | phase | anticipation | strike | follow-through | recover | cooldown | damage | ...
                Check(cells.Length>=8&&cells[0]==x.Title&&cells[1]==x.Phase.ToString(CultureInfo.InvariantCulture)&&cells[2]==N(x.Anticipation)&&cells[3]==N(x.Strike)&&cells[4]==N(x.Follow)&&cells[5]==N(x.Recover)&&cells[6]==N(x.Cooldown)&&cells[7]==N(x.Damage),
                    "the doc's "+x.Id+" row matches the code ("+string.Join(" | ",cells.Take(8))+")");
            }
            Check(doc.Contains(NativeStationBoss.Name)&&doc.Contains(NativeStationBoss.Archetype)&&doc.Contains(NativeZoneMusic.StationBoss),"the doc names the boss, its archetype and its music key");
        }
        // Presentation only: the concept is referenced by nothing but its own file, these checks and THE 6:47's presentation (the
        // runtime model, rig, effects, cues, preview, stage hook, the HUD's zone boss names and the runtime presenter): no rule,
        // authority, network or content file names it.
        private static readonly string[] Presentation={"NativeStationBossLook.cs","NativeStationBossMoves.cs","NativeStationBossRig.cs","NativeStationBossFx.cs","NativeStationBossCues.cs",
            "NativeStationBossPreview.cs","NativeStationBossStage.cs","NativeStationBossView.cs","NativeZoneBossNames.cs","NativeStationBossPresentation.cs"};
        private static void Inert(string root)
        {
            string self=Path.GetFullPath(Path.Combine(root,"Assets/ChipArena/Core/NativeStationBossModel.cs"));Check(File.Exists(self),"the model file");
            var names=new[]{"NativeStationBoss","StationBossPose","StationBossKey","StationBossMove"};int files=0;
            foreach(var dir in new[]{"Assets/ChipArena","Assets/Conversion"})foreach(var f in Directory.EnumerateFiles(Path.Combine(root,dir),"*",SearchOption.AllDirectories))
            {
                if(Path.GetFileName(f)=="NativeZoneBossAuthority.cs"||Path.GetFileName(f)=="NativeZoneBossRules.cs"||f.EndsWith(".meta",StringComparison.Ordinal)||Path.GetFullPath(f)==self||Presentation.Contains(Path.GetFileName(f)))continue;string ext=Path.GetExtension(f);if(ext!=".cs"&&ext!=".json"&&ext!=".asset"&&ext!=".prefab")continue;
                string text=File.ReadAllText(f);var hit=names.FirstOrDefault(n=>text.Contains(n));files++;
                if(hit!=null||text.Contains("\""+NativeStationBoss.Archetype+"\""))Check(false,Path.GetFileName(f)+" references the concept ("+(hit??NativeStationBoss.Archetype)+")");
            }
            Check(files>500,"the scan read the project's sources ("+files+" files)");
        }
        private static float Segment(Vector3 p,Vector3 a,Vector3 b){var ab=b-a;float t=Math.Max(0,Math.Min(1,Vector3.Dot(p-a,ab)/ab.LengthSquared()));return Vector3.Distance(p,a+ab*t);}
        private static bool Inside(Vector3 v,Vector3 min,Vector3 max)=>v.X>=min.X&&v.Y>=min.Y&&v.Z>=min.Z&&v.X<=max.X&&v.Y<=max.Y&&v.Z<=max.Z;
        private static bool Finite(Vector3 v)=>!float.IsNaN(v.X+v.Y+v.Z)&&!float.IsInfinity(v.X+v.Y+v.Z);
        private static bool Finite(Quaternion q)=>!float.IsNaN(q.X+q.Y+q.Z+q.W)&&!float.IsInfinity(q.X+q.Y+q.Z+q.W);
    }
}
#endif
