#if ARENA_TESTS
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The zombies' emergence (owner 2026-11-21: "rework zombie spawn animation ... digging out of the ground? Or have them laying
    // there, and just rising? The hit indicator from the old drop in effect is massive and inaccurate, so I want to remove that drop
    // in animation entirely"; Core/NativeHordeEmerge, docs/ZOMBIE-EMERGE.md):
    //  - no drop indicator, ink tear, leap or landing slam is left (rules, presentation, the Ward Zero hatch's shaft);
    //  - every kind's emergence on every emerging entry ends exactly at the simulation's activation (ActiveAt), its torso out at
    //    ExposedAt, the clocks the rules already replicate (no wire change), on host and guest alike;
    //  - nothing is damaged, knocked back or shoved while one emerges, and it is neither a target nor hittable under the floor;
    //  - the capsule rises with the drawn body (its top over the drawn head);
    //  - the pose and the ground's shapes are deterministic and continuous, and no frame allocates.
    internal static class EmergeChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Zombie emergence: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        private static string Body(string text,string head)
        {int i=text.IndexOf(head,StringComparison.Ordinal);if(i<0)return "";int b=text.IndexOf('{',i),depth=0;for(int k=b;k<text.Length;k++){if(text[k]=='{')depth++;else if(text[k]=='}'&&--depth==0)return text.Substring(i,k-i+1);}return "";}
        public static void Run(string root)
        {
            count=0;float worstGap=0;
            NoDropIn(root);Timing(root);NoDamage(root);worstGap=Capsule();Pose();Styles();Determinism();Allocation();Drawer(root);
            Console.WriteLine("PASS zombie emergence ("+count+" checks: no drop indicator, tear, leap or slam left; every kind's emergence ends at ActiveAt with its torso out at ExposedAt on every emerging entry, host and guest; nothing struck while it emerges, no target under the floor; the capsule's top within "+worstGap.ToString("F2")+" m over the drawn head; continuous, deterministic, no allocation)");
        }
        // ---- 1. nothing of the drop-in is left ----
        private static void NoDropIn(string root)
        {
            var pres=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");var zombie=R(root,"Assets/ChipArena/Runtime/NativeHordeZombiePresentation.cs");
            var gallery=R(root,"Assets/ChipArena/Runtime/NativeHordeHulkGallery.cs");var drop=R(root,"Assets/ChipArena/Runtime/NativePveDrop.cs");var horde=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");
            var fx=R(root,"Assets/ChipArena/Core/NativeHordeFx.cs");var clock=R(root,"Assets/ChipArena/Core/NativeHordeDrop.cs");var ward=R(root,"Assets/ChipArena/Core/NativeWardZeroFx.cs");
            foreach(var (name,text) in new[]{("kinds",pres),("zombie",zombie),("gallery",gallery)})
                foreach(var gone in new[]{"HordeDropJob(","DropMark(","LandWave(","LandDebris(","NativeHordeDrop.Rumble(","NativeZombieFx.Landing(p)","the ink tear","hordeDrops["})Check(!text.Contains(gone),name+": no `"+gone+"` (the drop-in's ring, tear and slam) is left");
            Check(!Regex.IsMatch(pres,@"entering&&\(NativeHordeDrop\.Is")&&!pres.Contains("NativeHordeGait.Drop(minionPose,seed,now-a.SpawnAt"),"no entering add plays the jump-down");
            Check(!fx.Contains("public static bool DropMark(")&&!fx.Contains("public static bool LandWave(")&&!fx.Contains("public static bool LandDebris("),"Core has no landing ring, slam wave or slam debris left to draw");
            Check(!clock.Contains("public static float Rumble("),"no drop warning rumble");
            foreach(var gone in new[]{"PveDropSlam","WardLandingSlam","StepPveDrop","\"minion-drop\""})Check(!drop.Contains(gone)&&!horde.Contains(gone),"the authority has no `"+gone+"` left");
            Check(!Body(ward,"private void EntranceFx(").Contains("s.DropEntry"),"the Ward Zero drop hatch draws no warning shaft down to a landing");
            Check(typeof(NativeHordeDropRules).GetMember("Touch").Length==0&&typeof(NativeHordeDropRules).GetMember("Damage").Length==0,"the landing slam's rules are gone");
        }
        // ---- 2. the emergence's end is the simulation's activation, on every emerging entry, for every kind ----
        private static readonly PveEntry DropE=new PveEntry{kind="drop",height=6.5f},Hatch=new PveEntry{kind="drop",height=7f},HulkDrop=new PveEntry{kind="drop",hulk=true,height=9f},
            Ceiling=new PveEntry{kind="vent",surface="ceiling",height=4f},Street=new PveEntry{kind="door",from=new PvePoint{x=3,y=0,z=4},@exit=new PvePoint{x=3,y=0,z=4}},
            Door=new PveEntry{kind="door",from=new PvePoint{x=0,z=0},@exit=new PvePoint{x=0,z=2}},Vent=new PveEntry{kind="vent",surface="floor"};
        // The rules' ExposedAt (NativePveHorde.SpawnPveAddCore and NativeNetworkFrame.ApplyMinions: the same expression).
        private static float Exposed(PveEntry e,string kind,float spawn,float walk,float active)
            =>NativeWardEntries.Drop(e)?NativeHordeDrop.LandAt(active,kind=="hulk"?NativeHordeDrop.Hulk:NativeHordeDrop.Brute):e.kind=="vent"?active-1.15f:walk;
        private static void Timing(string root)
        {
            var horde=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");var net=R(root,"Assets/ChipArena/Runtime/NativeNetworkFrame.cs");
            Check(horde.Contains("ExposedAt=NativeWardEntries.Drop(entry)?NativeHordeDrop.LandAt(times.active,profile):entry.kind==\"vent\"?times.active-1.15f:times.walk};"),"the host's ExposedAt is the expression checked here");
            Check(net.Contains("var timeline=NativeWardEntries.Timeline(m.Pve.Entry,mf.first,m.Pve.Stats.id);m.Pve.WalkAt=mf.spawnAt+timeline.walk;")&&net.Contains("m.Pve.ExposedAt=NativeWardEntries.Drop(m.Pve.Entry)?NativeHordeDrop.LandAt(mf.activeAt,profile):m.Pve.Entry.kind==\"vent\"?mf.activeAt-1.15f:m.Pve.WalkAt;"),"a guest derives the same ExposedAt from the replicated spawnAt, activeAt and entry (no new field)");
            var p=new CapturedRig.Pose();
            foreach(string kind in NativeHordeKinds.Ids)foreach(var e in new[]{DropE,Hatch,HulkDrop,Ceiling,Street})foreach(bool first in new[]{true,false})
            {
                bool emerges=NativeHordeEmerge.Emerges(e,kind);
                if(NativeHordeEmerge.OwnEntrance(kind)){Check(!emerges,kind+" keeps its own entrance");continue;}
                Check(emerges,kind+" emerges from a "+e.kind+(e.surface!=null?" ("+e.surface+")":""));
                var t=NativeWardEntries.Timeline(e,first,kind);float spawn=40,walk=spawn+t.walk,active=spawn+t.duration,exposed=Exposed(e,kind,spawn,walk,active);
                var m=NativeHordeEmerge.Of(spawn,exposed,active);
                Check(Math.Abs(m.Span-(active-spawn))<1e-5f&&Math.Abs(m.Torso-(exposed-spawn))<1e-5f,kind+": the emergence stands at ActiveAt and its torso is out at ExposedAt");
                Check(m.Break>0&&m.Break<m.Torso&&m.Torso<m.Span,kind+": the tell, the break, the torso out, risen, in that order");
                if(NativeHordeDrop.Is(e))Check(Math.Abs(t.duration-NativeHordeDrop.Duration(NativeHordeDrop.HeightOf(e,kind=="hulk"?NativeHordeDrop.Hulk:NativeHordeDrop.Brute),kind=="hulk"?NativeHordeDrop.Hulk:NativeHordeDrop.Brute))<1e-5f,kind+": a drop entry keeps its old warning-to-touchdown (pacing unchanged)");
                if(NativeHordeEmerge.PopIn(e))Check(t.walk==NativeHordeEmerge.PopTorso&&t.duration==NativeHordeEmerge.PopSpan,kind+": a door with no opening emerges in its first walk-in's 1.8 s");
                int k=NativeHordeKinds.Resolve(kind);
                foreach(var style in new[]{NativeHordeEmerge.Style.Dig,NativeHordeEmerge.Style.Rise})
                {
                    NativeHordeGait.Emerge(p,k,9,style,m.Span,m,out float lift);p.World();var risen=p.WorldPosition[CapturedRig.Head];
                    Check(lift==0&&risen.Y>.3f,kind+" "+style+": stood up by ActiveAt (head "+risen.Y+")");
                    Check(!NativeHordeGait.Emerge(p,k,9,style,0,m,out _),kind+" "+style+": nothing of it shows as the tell begins");
                }
                // The authority's capsule: no collider under the floor before the torso is out, standing at ActiveAt.
                Check(NativeHordeEmerge.Sink(m.Torso-.001f,m.Torso,m.Span,1.7f)==-1.7f&&NativeHordeEmerge.Sink(m.Span,m.Torso,m.Span,1.7f)==0,kind+": the capsule is under the floor until the torso is out and stands at ActiveAt");
            }
            // Entrances with an opening, and the kinds with their own, keep theirs (and their clocks).
            foreach(string kind in new[]{"shambler","runner","brute"}){Check(!NativeHordeEmerge.Emerges(Door,kind)&&!NativeHordeEmerge.Emerges(Vent,kind)&&!NativeHordeEmerge.Emerges(new PveEntry{kind="breach"},kind),"a door with an opening, a floor vent and a breach keep their entrances");Check(NativeWardEntries.Timeline(Door,true,kind)==NativeEntryRules.Timeline("door",true),"a door's walk-in is unchanged");}
            foreach(string kind in new[]{"faceling","smiler","tiler","rigger"})Check(NativeWardEntries.Timeline(Street,false,kind)==NativeEntryRules.Timeline("door",false),kind+": its pop-in timeline is unchanged");
            Check(!NativeHordeEmerge.Emerges(new PveEntry{kind="boss"},"runner")&&!NativeHordeEmerge.Emerges(null,"shambler"),"boss summons keep their scripted entrances");
        }
        // ---- 3. no damage while it emerges; no target under the floor ----
        private static void NoDamage(string root)
        {
            var horde=R(root,"Assets/ChipArena/Runtime/NativePveHorde.cs");var arena=R(root,"Assets/ChipArena/Runtime/NativeArena.cs");var drop=R(root,"Assets/ChipArena/Runtime/NativePveDrop.cs");
            var pose=Body(horde,"private void WardEntryPose(");Check(pose.Length>0,"the entry pose is found");
            foreach(var bad in new[]{"Damage(","Knockback","Emit(","PveSlamShove(","External","HitMinion(","LastAttack","StrikeAt"})Check(!pose.Contains(bad),"emerging touches no one (`"+bad+"`)");
            Check(pose.Contains("NativeHordeEmerge.Sink(Time-a.SpawnAt,a.ExposedAt-a.SpawnAt,a.ActiveAt-a.SpawnAt,m.Collider.height)")&&pose.Contains("m.Collider.enabled=Time>=a.ExposedAt;"),"the capsule rises on the emergence's sink, its collider from ExposedAt");
            var step=Body(horde,"private void StepPveHordeCore(");int held=step.IndexOf("if(Time<a.ActiveAt)\n                {WardEntryPose(m);continue;}",StringComparison.Ordinal);
            Check(held>0&&held<step.IndexOf("PveAddStrike(",StringComparison.Ordinal)&&held<step.IndexOf("TryVentTravel(",StringComparison.Ordinal),"every entering add is held on its clock before any AI, movement or attack");
            Check(!step.Contains("bruteDrop"),"no add skips the hold (the brute's old drop path is gone)");
            Check(arena.Contains("&&!EmergeHeld(target)&&")&&drop.Contains("private bool EmergeHeld(NativeMinion m)=>m.Pve!=null&&NativeHordeEmerge.Emerges(m.Pve.Entry,m.Pve.Stats?.id)&&Time<m.Pve.ExposedAt;"),"under the floor it is neither an enemy nor hittable (CanHit, Enemy)");
            Check(horde.Contains("if(source==null||a.Travelling||Time<a.ExposedAt||Pve?.Holding==true)return 0;"),"no damage reaches it before ExposedAt");
            var validation=R(root,"Assets/ChipArena/Runtime/NativeHordeDropValidation.cs");Check(validation.Contains("\"no slam at \"+x+\" m: hp=\"")&&validation.Contains("\"the capsule rises with the drawn body (\""),"the Editor build gate drives the real authority through an emergence");
        }
        // ---- 4. the capsule's top stays over the drawn head as it rises ----
        private static float Capsule()
        {
            var p=new CapturedRig.Pose();float worst=0;
            foreach(int kind in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Spitter,NativeHordeKinds.Hulk})
            foreach(var (torso,span) in new[]{(NativeHordeDrop.LandAt(NativeHordeDrop.Duration(6.5f)),NativeHordeDrop.Duration(6.5f)),(NativeHordeEmerge.PopTorso,NativeHordeEmerge.PopSpan),(NativeHordeDrop.LandAt(NativeHordeDrop.Duration(9,NativeHordeDrop.Hulk),NativeHordeDrop.Hulk),NativeHordeDrop.Duration(9,NativeHordeDrop.Hulk))})
            foreach(var style in new[]{NativeHordeEmerge.Style.Dig,NativeHordeEmerge.Style.Rise})
            {
                var m=NativeHordeEmerge.Of(0,torso,span);float sc=NativeHordeKinds.Scale[kind],height=kind==NativeHordeKinds.Hulk?NativeHordeKinds.HulkHeight:NativeHordeKinds.AddHeight,root=NativeHordeKinds.DrawRoot(kind,false);
                for(float a=m.Torso;a<=m.Span;a+=1/60f)
                {
                    NativeHordeGait.Emerge(p,kind,5,style,a,m,out float lift);p.World();
                    float head=root+(p.WorldPosition[CapturedRig.Head].Y+lift)*sc+.35f*sc,top=height+NativeHordeEmerge.Sink(a,m.Torso,m.Span,height);
                    Check(top>=head-.06f*sc,NativeHordeKinds.Ids[kind]+" "+style+" at "+a+": the drawn head ("+head+") is inside the capsule ("+top+")");
                    worst=Math.Max(worst,(top-head)/sc);
                }
                Check(worst<.45f,NativeHordeKinds.Ids[kind]+": the capsule's top keeps near the drawn head ("+worst+" m over)");
            }
            return worst;
        }
        // ---- 5. the pose: hidden through the tell, continuous through the break and the hand-over to the take ----
        private static void Pose()
        {
            var p=new CapturedRig.Pose();var prev=new Vector3[CapturedRig.Bones.Length];
            foreach(var style in new[]{NativeHordeEmerge.Style.Dig,NativeHordeEmerge.Style.Rise})
            foreach(var (torso,span) in new[]{(1.806f,2.706f),(1f,1.8f),(2.86f,4.36f)})
            {
                var m=NativeHordeEmerge.Of(0,torso,span);bool had=false;float worst=0;
                for(int i=0;i<=(int)(span*240);i++)
                {
                    float a=i/240f;bool shown=NativeHordeGait.Emerge(p,NativeHordeKinds.Shambler,11,style,a,m,out float lift);if(!shown){had=false;continue;}p.World();
                    for(int j=0;j<prev.Length;j++){var w=p.WorldPosition[j]+new Vector3(0,lift,0);if(had)worst=Math.Max(worst,(w-prev[j]).Length());prev[j]=w;}had=true;
                    // Under the floor until the break, and never through it: the root never rises over its standing height.
                    Check(lift<=0,"never above standing");
                }
                Check(worst<.12f,style+" "+span+" s: no joint pops more than 12 cm in a 240th of a second ("+worst+")");
            }
            // Dig: the first hand is the first thing through the floor, the head comes up after both hands.
            {
                var m=NativeHordeEmerge.Of(0,1.806f,2.706f);float hand=-1,head=-1;
                for(int i=0;i<=(int)(1.806f*240);i++){float a=i/240f;if(!NativeHordeGait.Emerge(p,NativeHordeKinds.Shambler,11,NativeHordeEmerge.Style.Dig,a,m,out _))continue;p.World();
                    if(hand<0&&Math.Max(p.WorldPosition[CapturedRig.LHand].Y,p.WorldPosition[CapturedRig.RHand].Y)>NativeHordeGait.Floor)hand=a;if(head<0&&p.WorldPosition[CapturedRig.Head].Y>NativeHordeGait.Floor)head=a;}
                Check(hand>=m.Break&&hand<m.Break+.3f&&head>hand+.25f,"a hand claws through first ("+hand+"), the head after it ("+head+")");
            }
            // Lie and rise: it lies on the floor (the head low), then gets up.
            {
                var m=NativeHordeEmerge.Of(0,1.806f,2.706f);NativeHordeGait.Emerge(p,NativeHordeKinds.Shambler,11,NativeHordeEmerge.Style.Rise,m.Break,m,out _);p.World();
                Check(p.WorldPosition[CapturedRig.Head].Y<NativeHordeGait.Floor+.35f&&p.WorldPosition[CapturedRig.Chest].Y<NativeHordeGait.Floor+.4f,"it lies there at the break");
            }
        }
        // ---- 6. the style by context ----
        private static void Styles()
        {
            int digOut=0,digIn=0,n=2000;
            for(uint s=1;s<=n;s++)
            {
                Check(NativeHordeEmerge.Choose(true,false,true,s)==NativeHordeEmerge.Style.Dig&&NativeHordeEmerge.Choose(false,true,true,s)==NativeHordeEmerge.Style.Dig,"the heavy kinds always dig out");
                if(NativeHordeEmerge.Choose(false,false,false,s)==NativeHordeEmerge.Style.Dig)digOut++;if(NativeHordeEmerge.Choose(true,false,false,s)==NativeHordeEmerge.Style.Dig)digIn++;
                Check(NativeHordeEmerge.Choose(false,true,false,s)==NativeHordeEmerge.Choose(true,false,false,s),"the bleed reads as inside");
            }
            Check(digOut>n*.75f&&digIn<n*.28f,"open ground mostly digs ("+digOut+"), inside mostly lies and rises ("+(n-digIn)+")");
            Check(NativeHordeEmerge.Outdoor(NativeExpeditionGeneratedWorld.MapId)&&NativeHordeEmerge.Outdoor("expedition-city")&&NativeHordeEmerge.Outdoor("expedition-city-walk-2")&&!NativeHordeEmerge.Outdoor("siren")&&!NativeHordeEmerge.Outdoor("expedition-ward-zero")&&!NativeHordeEmerge.Outdoor("expedition-foundry")&&!NativeHordeEmerge.Outdoor(null),"the city is open ground, the instances are inside");
            Check(NativeHordeEmerge.Heavy(NativeHordeKinds.Brute)&&NativeHordeEmerge.Heavy(NativeHordeKinds.Hulk)&&!NativeHordeEmerge.Heavy(NativeHordeKinds.Runner),"the brute and the Hulk are heavy");
            var light=NativeHordeEmerge.LookOf(NativeHordeKinds.Shambler);var brute=NativeHordeEmerge.LookOf(NativeHordeKinds.Brute);var hulk=NativeHordeEmerge.LookOf(NativeHordeKinds.Hulk);
            Check(light.Scar==0&&brute.Scar>0&&hulk.Scar>brute.Scar&&hulk.Reach>brute.Reach&&brute.Reach>light.Reach&&hulk.Chunk>brute.Chunk&&brute.Chunk>light.Chunk&&hulk.Chunks>light.Chunks,"the heavy kinds dig out bigger, heavier, and leave a scar that stays a while");
            // The tell is short and honest: a beat before the hand, never a radius marker the size of the old ring.
            foreach(var (torso,span) in new[]{(1.806f,2.706f),(1f,1.8f),(2.86f,4.36f)}){var m=NativeHordeEmerge.Of(0,torso,span);Check(m.Break>=.25f&&m.Break<=1f,"the tell lasts "+m.Break+" s");}
            Check(hulk.Reach<NativeHordeDrop.Hulk.SlamRadius&&brute.Reach<NativeHordeDrop.Brute.SlamRadius,"the broken ground is smaller than the old slam disc");
        }
        // ---- 7. determinism ----
        private static void Determinism()
        {
            var a=new CapturedRig.Pose();var b=new CapturedRig.Pose();var m=NativeHordeEmerge.Of(10,11.8f,12.7f);
            for(int i=0;i<600;i++)
            {
                float t=10+i/200f;var style=(NativeHordeEmerge.Style)(i%2);int kind=i%7;
                bool sa=NativeHordeGait.Emerge(a,kind,(uint)(i/3),style,t-10,m,out float la);NativeHordeGait.Emerge(b,(kind+3)%7,77,NativeHordeEmerge.Style.Dig,(i%9)*.3f,m,out _);bool sb=NativeHordeGait.Emerge(b,kind,(uint)(i/3),style,t-10,m,out float lb);
                Check(sa==sb&&la==lb,"the same moment twice draws the same");if(!sa)continue;
                for(int j=0;j<CapturedRig.Bones.Length;j++)Check(a.Rotation[j]==b.Rotation[j]&&a.Position[j]==b.Position[j],"joint "+j+" is the same, whatever was posed before");
                Check(NativeHordeEmerge.Plate(5,i%7,7,t-10,m,1.4f,out var o1,out float t1,out _,out _)==NativeHordeEmerge.Plate(5,i%7,7,t-10,m,1.4f,out var o2,out float t2,out _,out _)&&o1==o2&&t1==t2,"the ground's shapes are the same");
            }
            Check(NativeHordeEmerge.Choose(false,false,false,123)==NativeHordeEmerge.Choose(false,false,false,123),"the style is the seed's");
        }
        // ---- 8. no allocation per frame ----
        private static void Allocation()
        {
            var p=new CapturedRig.Pose();var m=NativeHordeEmerge.Of(0,1.806f,2.706f);var hm=NativeHordeEmerge.Of(0,2.86f,4.36f);
            void Frame(int i)
            {
                float t=(i%330)/100f;var mk=i%2==0?m:hm;var look=NativeHordeEmerge.LookOf(i%3==0?NativeHordeKinds.Hulk:NativeHordeKinds.Brute);
                NativeHordeGait.Emerge(p,i%7,(uint)i,(NativeHordeEmerge.Style)(i%2),t,mk,out _);NativeHordeEmerge.Sink(t,mk.Torso,mk.Span,1.7f);NativeHordeEmerge.Of(0,mk.Torso,mk.Span);
                for(int k=0;k<look.Plates;k++)NativeHordeEmerge.Plate(3,k,look.Plates,t,mk,look.Reach,out _,out _,out _,out _);
                for(int k=0;k<look.Chunks;k++)NativeHordeEmerge.Clod(3,k,look.Chunks,t-mk.Break,look.Hole,look.Chunk,4,out _,out _,out _,out _);
                for(int k=0;k<look.Dust;k++)NativeHordeEmerge.Dust(3,k,look.Dust,t-mk.Break,look.Hole,out _,out _,out _,out _);
                for(int k=0;k<6;k++)NativeHordeEmerge.Grit(3,k,t,mk,look.Hole,out _,out _);
                for(int k=0;k<look.Cracks;k++)for(int j=0;j<=NativeHordeEmerge.CrackSegments;j++)NativeHordeEmerge.CrackPoint(3,k,look.Cracks,j,NativeHordeEmerge.Tell(t,mk),look.Reach,out _);
                NativeHordeEmerge.Hole(t,mk,look.Hole,look.Scar,out _,out _);NativeHordeEmerge.Stain(t,mk,.95f,out _,out _,out _);NativeHordeEmerge.CrackAlpha(t,mk,look.Scar);NativeHordeEmerge.StainEdge(3,i%32);
                NativeHordeEmerge.Choose(i%2==0,false,i%5==0,(uint)i);
            }
            for(int i=0;i<300;i++)Frame(i);long before=GC.GetAllocatedBytesForCurrentThread();for(int i=300;i<1500;i++)Frame(i);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1200 frames of emergences, both styles, every kind ("+used+" bytes)");
        }
        // ---- 9. the runtime drawer: draws only, follows the tier, allocates nothing per frame ----
        private static void Drawer(string root)
        {
            var draw=R(root,"Assets/ChipArena/Runtime/NativeZombieEmergeDraw.cs");var zombie=R(root,"Assets/ChipArena/Runtime/NativeHordeZombiePresentation.cs");var kinds=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            foreach(var bad in new[]{".Hp=","Damage(","Emit(","StaggerAt=","LastAttack=","linearVelocity=","AddComponent<","Collider>().enabled","Body.position=",".Ring("})Check(!draw.Contains(bad),"the drawer never writes `"+bad+"` (and draws no ring)");
            foreach(var m in new[]{"public void Emerge(","private void EmergeDecals(","private void EmergeStain("})
            {var b=Body(draw,m);Check(b.Length>0,"the drawer has "+m);foreach(var bad in new[]{".Select(",".Where(",".Any(",".First",".ToArray(",".ToList(","=>","\"+","+\"","$\"","new List<","new Dictionary<","new Mesh","new Material","new int[","new float[","new Vector3[","new Color["})Check(!b.Contains(bad),m+" allocates nothing per frame (`"+bad+"`)");}
            foreach(var b in new[]{Body(zombie,"private void HordeEmergeJob("),Body(zombie,"private void DrawHordeEmerges("),Body(kinds,"private void HordeEmergeCues(")})
                foreach(var bad in new[]{".Select(",".Where(",".ToArray(",".ToList(","new List<","new Dictionary<","new HordeEmergeRec["})Check(b.Length>0&&!b.Contains(bad),"the job, the draw and the cues allocate no collection per frame (`"+bad+"`)");
            foreach(var stream in new[]{"N(look.Plates)","N(look.Chunks)","N(look.Dust)","N(look.Cracks)"})Check(draw.Contains(stream),"the stream "+stream+" follows the tier (Reduced effects and the low tier halve it)");
            Check(draw.Contains("if(extras)for(int i=0;i<6;i++)if(NativeHordeEmerge.Grit(")&&draw.Contains("if(extras&&wet>.05f)"),"the grit and the stain's sheen are the full tier's");
            Check(zombie.Contains("look.Shake*(b==0?1:.6f)")&&R(root,"Assets/ChipArena/Runtime/ConversionGame.cs").Contains("presentation.HordeShake(cameraView.transform,prefs.reducedEffects?.5f:1);"),"the heavy kinds' shake is the settings' (camera shake, halved by Reduced effects)");
            Check(kinds.Contains("HordeEmergeJob(m.Id,kind,spot,a.SpawnAt,marks,style,seed,sc);")&&kinds.Contains("var marks=NativeHordeEmerge.Of(a.SpawnAt,a.ExposedAt,a.ActiveAt);"),"the live adds draw from their replicated clocks (host, guests, killcam)");
            Check(zombie.Contains("for(int i=0;i<hordeEmerges.Length;i++)hordeEmerges[i].Live=false;"),"a rewind (the killcam) forgets the emergences in flight and replays them from the clock");
            var core=R(root,"Assets/ChipArena/Core/NativeHordeEmerge.cs");Check(!core.Contains("UnityEngine")&&!core.Contains("NativeArena")&&!core.Contains("new Random(")&&!core.Contains("DateTime"),"NativeHordeEmerge.cs is engine free and clock free");
        }
    }
}
#endif
