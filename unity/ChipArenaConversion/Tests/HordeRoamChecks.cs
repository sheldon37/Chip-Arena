#if ARENA_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
namespace ChipArena.UnityConversion
{
    // The Expedition town zombies' roaming presentation (owner 2026-10-15: "Monsters outside of the hub should walk around, every
    // now and then stand idle for moments. Only start chasing the player when they detect them (Either through vision or
    // audio)"; docs/EXPEDITION-HUB.md "Town zombie roaming and perception"): Astra's replicated HubZombieView maps to the pose
    // through one function (driven here by the real HubZombieBrain); with no record the state is inferred from the add's motion
    // (an instance horde only standing apart from hunting); the idle varies per add and is the same on every peer; the alert's
    // head snap, scream and burst run from the replicated alert time (a late joiner draws the same moment); the wander take
    // plants its feet at the host's pace; the head looks at its pilot; no allocation; presentation only.
    internal static class HordeRoamChecks
    {
        private static int count;
        private static void Check(bool ok,string what){if(!ok)throw new Exception("Horde roam: "+what);count++;}
        private static string R(string root,string path)=>File.ReadAllText(Path.Combine(root,path));
        public static void Run(string root)
        {
            Mapping();Brain();Inferred();Variety();Alert();LookAt();Wander();Allocation();Source(root);
            Console.WriteLine("PASS horde roam ("+count+" checks: Astra's HubZombieView maps one for one to the pose's states, weights, reaction ages and blends, through the real HubZombieBrain; the motion-inferred fallback with hysteresis and holds, an instance horde only idle or hunting; five idle variants per add, deterministic and out of step across a crowd; the alert's snap, scream and burst from hubAlertAt, the same for a late joiner; heads look at their pilot; the wander take plants its feet at the host's pace; no allocation; presentation only)");
        }
        private static HordeRoamWire W(HordeRoamState s,float at,float alertAt=-100)=>new HordeRoamWire{State=s,At=at,AlertAt=alertAt};
        // ---- Astra's record through the mapping, as the host's brain runs (Core/NativeHubPerception) ----
        private static void Brain()
        {
            foreach(HubZombieState h in Enum.GetValues(typeof(HubZombieState)))Check((byte)h==(byte)(HordeRoamState)(byte)h&&Enum.GetName(typeof(HordeRoamState),(byte)h)==h.ToString(),"the presentation's states are the host's, one for one ("+h+")");
            Check(NativeHordeRoam.FromHub(null).State==HordeRoamState.None&&NativeHordeRoam.FromHub(new HubZombieView{State=HubZombieState.None}).State==HordeRoamState.None,"no record: no state (infer)");
            var v=NativeHordeRoam.FromHub(new HubZombieView{State=HubZombieState.Alert,AlertAt=12.5f,LookYaw=-35});
            Check(v.State==HordeRoamState.Alert&&v.AlertAt==12.5f&&v.HasLookYaw&&v.LookYaw==-35&&float.IsNaN(v.At),"the record's alert time and look; the state's start is not replicated");
            var brain=new HubZombieBrain(4242,0,new V3(0,0,0),0);var track=new NativeHordeRoamTrack();var seen=new HashSet<HordeRoamState>();var pos=new V3(0,0,0);
            float alertAge=float.NaN,burst=float.NaN,firstChase=float.NaN,searchSeen=float.NaN;float maxWander=0,maxIdle=0,maxSearch=0;
            for(int i=0;i<60*40;i++)
            {
                float t=i/60f;
                if(i==60*12)brain.Observe(t,3,new V3(0,0,9),pos);
                if(i>60*12&&i<60*14)brain.Observe(t,3,new V3(0,0,9),pos);
                brain.Step(t,pos,i<60*16);if(brain.NeedsGoal)brain.Wander(t,new V3(3,0,4));
                var r=track.Read(true,NativeHordeRoam.FromHub(brain.View),77,t,0,false);seen.Add(r.State);
                maxWander=Math.Max(maxWander,r.Wander);maxIdle=Math.Max(maxIdle,r.Idle);maxSearch=Math.Max(maxSearch,r.Search);
                if(i==60*12+18)alertAge=r.AlertAge;
                if(r.State==HordeRoamState.Chase&&float.IsNaN(firstChase)){firstChase=t;burst=r.BurstAge;}
                if(r.State==HordeRoamState.Search&&float.IsNaN(searchSeen))searchSeen=t;
            }
            Check(seen.SetEquals(new[]{HordeRoamState.Idle,HordeRoamState.Wander,HordeRoamState.Alert,HordeRoamState.Chase,HordeRoamState.Search}),"the brain's idle, wander, alert, chase and search all reach the pose ("+string.Join(",",seen)+")");
            Check(maxWander==1&&maxIdle==1&&maxSearch==1,"each calm state reaches its whole weight");
            Check(Math.Abs(alertAge-.3f)<1e-3f,"the alert age is the host's hubAlertAt ("+alertAge+")");
            Check(firstChase-12>=NativeHordeRoam.AlertMin-1e-3f&&firstChase-12<=NativeHordeRoam.AlertMax+1f/60+1e-3f&&burst==0,"the chase breaks out .4-.8 s after the alert and bursts from its first frame ("+(firstChase-12).ToString("0.000")+" s)");
            Check(searchSeen>=15.9f,"losing the target reads as the search");
        }
        // ---- the record's states to the pose ----
        private static void Mapping()
        {
            var t=new NativeHordeRoamTrack();
            Check(!t.Read(false,W(HordeRoamState.Idle,0),1,1,0,false).On,"off: no roaming read (the gait is today's)");
            foreach(var s in new[]{HordeRoamState.Wander,HordeRoamState.Idle,HordeRoamState.Alert,HordeRoamState.Chase,HordeRoamState.Search})
            {
                t.Reset();var r=t.Read(true,W(s,8),1,10,3,false);
                Check(r.On&&!r.Inferred&&r.State==s&&Math.Abs(r.Age-2)<1e-5f,s+": the record's state and age (the speed is ignored)");
                Check(r.Wander==(s==HordeRoamState.Wander?1:0)&&r.Idle==(s==HordeRoamState.Idle?1:0)&&r.Search==(s==HordeRoamState.Search?1:0),s+": its weights");
                Check(NativeHordeRoam.Calm(s)==(r.Calm>.99f),s+": calm only while wandering, idling or searching");
            }
            // A change blends over NativeHordeRoam.Blend from the record's own start.
            t.Reset();t.Read(true,W(HordeRoamState.Wander,0),1,9.9f,0,false);var m=t.Read(true,W(HordeRoamState.Search,10),1,10.25f,0,false);
            Check(Math.Abs(m.Search-.5f)<1e-4f&&Math.Abs(m.Wander-.5f)<1e-4f,"wander to search: half way at a quarter second");
            m=t.Read(true,W(HordeRoamState.Search,10),1,10.5f,0,false);Check(m.Search==1&&m.Wander==0,"the search is whole after the blend");
            for(float u=10.6f;u<14;u+=.1f)t.Read(true,W(HordeRoamState.Search,10),1,u,0,false);m=t.Read(true,W(HordeRoamState.Wander,14),1,14.1f,0,false);Check(m.Wander>0&&m.Wander<.2f&&m.Search>.8f,"the return to the wander eases in");
            // The notice and the chase after it: ages from the alert time; the burst from the chase's start.
            t.Reset();m=t.Read(true,W(HordeRoamState.Alert,20,20),1,20.3f,0,false);Check(Math.Abs(m.AlertAge-.3f)<1e-4f&&float.IsNaN(m.BurstAge)&&m.Calm==0,"a notice: the alert age, no burst yet");
            m=t.Read(true,W(HordeRoamState.Chase,20.6f,20),1,20.7f,4,false);Check(Math.Abs(m.AlertAge-.7f)<1e-4f&&Math.Abs(m.BurstAge-.1f)<1e-4f,"the chase out of the alert bursts from its start");
            for(float u=20.8f;u<22;u+=.1f)t.Read(true,W(HordeRoamState.Chase,20.6f,20),1,u,4,false);
            m=t.Read(true,W(HordeRoamState.Chase,20.6f,20),1,22.0f,4,false);Check(float.IsNaN(m.AlertAge)&&float.IsNaN(m.BurstAge),"the alert's layer is over by NoticeSeconds");
            m=t.Read(true,W(HordeRoamState.Chase,30,20),1,30.1f,4,false);Check(float.IsNaN(m.BurstAge),"a later chase (no fresh alert) does not replay the burst");
            m=t.Read(true,W(HordeRoamState.Search,40,20),1,40.2f,0,false);Check(float.IsNaN(m.AlertAge),"a search shows no alert");
            var look=new HordeRoamWire{State=HordeRoamState.Alert,At=50,AlertAt=50,HasLookYaw=true,LookYaw=40};m=t.Read(true,look,1,50.1f,0,false);Check(m.HasLookYaw&&m.LookYaw==40,"the record's look direction reaches the pose");
            // The record's state with no start time: each peer starts it on the frame it sees the change.
            t.Reset();for(float u=60;u<60.95f;u+=.2f)t.Read(true,new HordeRoamWire{State=HordeRoamState.Wander,At=float.NaN},1,u,0,false);m=t.Read(true,new HordeRoamWire{State=HordeRoamState.Wander,At=float.NaN},1,61,0,false);
            Check(m.Wander==1&&Math.Abs(m.Age-1)<1e-4f,"an unseen start: the state's age from the first frame seen");
            m=t.Read(true,new HordeRoamWire{State=HordeRoamState.Idle,At=float.NaN},1,61.25f,0,false);Check(m.Idle==0&&m.Wander==1&&m.Age==0,"a change seen starts there");
            m=t.Read(true,new HordeRoamWire{State=HordeRoamState.Idle,At=float.NaN},1,61.5f,0,false);Check(Math.Abs(m.Idle-.5f)<1e-4f&&Math.Abs(m.Wander-.5f)<1e-4f&&Math.Abs(m.Age-.25f)<1e-4f,"and blends from that frame");
            // The mapped pose: the same frame idling, wandering and chasing draws differently; idle with a look point looks there.
            float idleApart=0,searchApart=0;
            for(float u=3;u<12.5f;u+=1.5f){var chase=Head(Sim(0,5100,HordeRoamState.Chase,0,0,null,u).Item1,out _);idleApart=Math.Max(idleApart,Angle(Head(Sim(0,5100,HordeRoamState.Idle,0,0,null,u).Item1,out _),chase));searchApart=Math.Max(searchApart,Angle(Head(Sim(0,5100,HordeRoamState.Search,0,0,null,u).Item1,out _),chase));}
            Check(idleApart>8&&searchApart>8,"idling and searching pose the head away from the chase's ("+idleApart.ToString("0")+", "+searchApart.ToString("0")+" deg)");
        }
        // ---- no record: the state from the add's ground speed ----
        private static void Inferred()
        {
            var t=new NativeHordeRoamTrack();HordeRoamPose r=default;float now=0;
            void Run(float seconds,float speed,bool engaged=false){for(int i=0;i<Math.Round(seconds*60);i++){now+=1f/60;r=t.Read(true,default,7,now,speed,engaged);}}
            Run(2,0);Check(r.Inferred&&r.State==HordeRoamState.Idle&&r.Idle==1,"standing reads as an idle");
            Run(2,.65f);Check(r.State==HordeRoamState.Wander&&r.Wander==1,"a slow shamble reads as a wander");
            Run(1f/60,1.5f);Check(r.State==HordeRoamState.Chase&&Math.Abs(r.AlertAge)<1e-4f&&Math.Abs(r.BurstAge)<1e-4f,"setting off after a calm second reads as a notice (snap, scream, burst as it sets off)");
            Run(.5f,1.5f);Check(Math.Abs(r.AlertAge-.5f)<.02f&&r.Calm<.01f,"the notice plays on into the chase");
            Run(3,3.4f);Check(r.State==HordeRoamState.Chase&&float.IsNaN(r.AlertAge),"the authority's run is a chase; the notice is over");
            Run(3,0,true);Check(r.State==HordeRoamState.Chase,"standing to strike (an attack within EngagedFor) stays a chase");
            Run(1.2f,0);Check(r.State==HordeRoamState.Chase,"a short stop holds the chase");
            Run(.5f,0);Check(r.State==HordeRoamState.Idle,"a chase that stands StopHold seconds settles to an idle");
            Run(2,1.5f);Check(r.State==HordeRoamState.Chase,"chase again");
            Run(1,.6f);Check(r.State==HordeRoamState.Wander,"a chase that slows to a shamble for ChaseHold settles to a wander");
            // Hysteresis: speeds jittering inside the bands never flicker the state.
            t.Reset();now=100;Run(1,.1f);int flips=0;var last=r.State;for(int i=0;i<600;i++){now+=1f/60;r=t.Read(true,default,7,now,i%2==0?.2f:.3f,false);if(r.State!=last)flips++;last=r.State;}
            Check(flips==0&&r.State==HordeRoamState.Idle,"jitter between .2 and .3 m/s stays an idle");
            Run(1,.6f);flips=0;last=r.State;for(int i=0;i<600;i++){now+=1f/60;r=t.Read(true,default,7,now,i%2==0?1.0f:1.1f,false);if(r.State!=last)flips++;last=r.State;}
            Check(flips==0&&r.State==HordeRoamState.Wander,"jitter between 1.0 and 1.1 m/s stays a wander");
            // A fresh add already running (today's hub zombies, out of their entrance) is a chase with no notice.
            t.Reset();r=t.Read(true,default,8,200,1.5f,false);Check(r.State==HordeRoamState.Chase&&float.IsNaN(r.AlertAge),"spawned chasing: no notice");
            // The hub's roster (ExpeditionHubRules.Kind, any ring, late in the loop): its authority walk and run read as a chase,
            // the host's wander pace (min(1.1, walk x .55)) as moving.
            for(int k=0;k<NativeHordeKinds.Count;k++)
            {
                if(!new[]{"near","mid","far"}.Any(ring=>ExpeditionHubRules.Kind(ring,NativeHordeKinds.Ids[k],1000)))continue;var st=NativeHordeKinds.ProposedRow(k);
                Check(st.walk>NativeHordeRoam.ChaseAbove&&st.run>NativeHordeRoam.ChaseAbove,NativeHordeKinds.Ids[k]+": its walk "+st.walk+" and run "+st.run+" are chases");
                Check(NativeHordeRoam.WanderSpeed(st.walk)>NativeHordeRoam.MoveAbove&&NativeHordeRoam.WanderSpeed(st.walk)<=1.1f,NativeHordeKinds.Ids[k]+": the host's wander pace moves");
            }
            // An add without the record that does not roam (an instance horde): only standing apart from hunting. Slowed by a limp
            // or a frost field it still hunts; standing alone it idles; it never reads a wander or an alert.
            t.Reset();now=400;void RunH(float seconds,float speed,bool engaged=false){for(int i=0;i<Math.Round(seconds*60);i++){now+=1f/60;r=t.Read(true,default,9,now,speed,engaged,false);}}
            RunH(1,1.5f);Check(r.State==HordeRoamState.Chase,"an instance add hunts");RunH(2,.65f);Check(r.State==HordeRoamState.Chase&&r.Calm==0,"slowed to .65 m/s (a limp, a frost field) it still hunts");
            RunH(2,0,true);Check(r.State==HordeRoamState.Chase,"standing with a pilot in reach it hunts");RunH(2,0);Check(r.State==HordeRoamState.Idle,"standing alone it idles");
            RunH(.5f,.65f);Check(r.State==HordeRoamState.Chase&&float.IsNaN(r.AlertAge)&&float.IsNaN(r.BurstAge),"setting off again: no inferred alert or scream");
            // A missed half second or a rewind starts over.
            t.Reset();now=300;Run(2,0);r=t.Read(true,default,7,now+1,1.5f,false);Check(r.State==HordeRoamState.Chase&&float.IsNaN(r.AlertAge),"a gap starts over");
        }
        // ---- the idle's variety: per add, deterministic, out of step ----
        private static void Variety()
        {
            var w=new float[NativeHordeRoam.IdleVariants];
            var visited=new Dictionary<int,HashSet<int>>();
            for(int id=10000;id<10016;id++)
            {
                uint seed=NativeHordeKinds.Seed(id);visited[id]=new HashSet<int>();
                for(float t=0;t<180;t+=.25f){NativeHordeRoam.Variants(seed,t,w,out float age);Check(Math.Abs(w.Sum()-1)<1e-5f&&w.All(x=>x>=0)&&age>=0&&age<NativeHordeRoam.VariantPeriod(seed)+1e-3f,"the variants' weights sum to one");visited[id].Add(Array.IndexOf(w,w.Max()));}
                Check(visited[id].Count==NativeHordeRoam.IdleVariants,"add "+id+" shows "+visited[id].Count+" of the five idle variants in three minutes");
                for(int n=-20;n<60;n++){Check(NativeHordeRoam.Variant(seed,n)!=NativeHordeRoam.Variant(seed,n+1),"add "+id+": never the same variant twice in a row");if(n%5==0)Check(Enumerable.Range(n,5).Select(x=>NativeHordeRoam.Variant(seed,x)).Distinct().Count()==NativeHordeRoam.IdleVariants,"add "+id+": every variant in each five slots");}
            }
            for(float t=5;t<60;t+=7)
            {
                var dominant=new HashSet<int>();var ages=new List<float>();
                for(int id=10000;id<10016;id++){NativeHordeRoam.Variants(NativeHordeKinds.Seed(id),t,w,out float age);dominant.Add(Array.IndexOf(w,w.Max()));ages.Add((float)Math.Round(age,2));}
                Check(dominant.Count>=3,"16 adds at "+t+" s idle on "+dominant.Count+" different variants");Check(ages.Distinct().Count()>=12,"the adds' idle schedules are out of step at "+t+" s");
            }
            // The same add on two peers (two gaits, the same frames) poses bit for bit the same; different adds differ.
            var a=Sim(0,10003,HordeRoamState.Idle,0,0,null,9.5f).Item2;var b=Sim(0,10003,HordeRoamState.Idle,0,0,null,9.5f).Item2;var c=Sim(0,10004,HordeRoamState.Idle,0,0,null,9.5f).Item2;
            Check(a.SequenceEqual(b),"the same add idles identically on every peer");Check(!a.SequenceEqual(c),"another add idles differently");
            // Each variant moves the body: the head through a variant's slot is not the head of another.
            uint s0=NativeHordeKinds.Seed(10005);var byVariant=new Dictionary<int,Vector3>();
            for(float t=1;t<120&&byVariant.Count<NativeHordeRoam.IdleVariants;t+=.5f){NativeHordeRoam.Variants(s0,t,w,out float age);int v=Array.IndexOf(w,w.Max());if(w[v]<.999f||byVariant.ContainsKey(v)||age<1.2f)continue;byVariant[v]=Head(Sim(0,10005,HordeRoamState.Idle,0,0,null,t).Item1,out _);}
            Check(byVariant.Count==NativeHordeRoam.IdleVariants,"an add reaches every variant ("+byVariant.Count+")");
            var heads=byVariant.Values.ToList();int apart=0;for(int i=0;i<heads.Count;i++)for(int j=i+1;j<heads.Count;j++)if(Angle(heads[i],heads[j])>3)apart++;
            Check(apart>=8,"the variants pose the head apart ("+apart+" of 10 pairs over 3 degrees)");
        }
        // ---- the notice from the replicated alert time ----
        private static void Alert()
        {
            float T=40;var target=new Vector3(3.5f,.9f,3.5f);
            // The scream's curve and the shriek.
            Check(NativeHordeRoam.Scream(NativeHordeRoam.ScreamAt-.1f)==0&&NativeHordeRoam.Scream(NativeHordeRoam.ScreamAt+.3f)>.99f&&NativeHordeRoam.Scream(NativeHordeRoam.ScreamAt+NativeHordeRoam.ScreamSeconds)==0,"the scream opens at ScreamAt, holds, and is over by ScreamAt + ScreamSeconds");
            Check(NativeHordeRoam.ShriekAt(T)==T+NativeHordeRoam.ScreamAt,"the shriek is the scream's opening");
            Check(NativeHordeRoam.SnapWeight(0)==0&&NativeHordeRoam.SnapWeight(NativeHordeRoam.Snap)>.99f&&NativeHordeRoam.SnapWeight(NativeHordeRoam.NoticeSeconds)==0,"the snap lands in Snap seconds and hands back by NoticeSeconds");
            Check(NativeHordeRoam.Burst(.12f)>.99f&&NativeHordeRoam.Burst(NativeHordeRoam.BurstSeconds)==0,"the burst throws the trunk at once and recovers");
            // The head snaps to the noticed pilot: away before the notice (idle, no target), on it by Snap + .05 s.
            var before=Head(Sim(0,10007,HordeRoamState.Idle,T,T,null,T-.02f).Item1,out var at);
            var snapped=Head(SimNotice(10007,T,target,T+NativeHordeRoam.Snap+.05f,T-3).Item1,out at);
            var to=Vector3.Normalize(new Vector3(target.X-at.X,0,target.Z-at.Z));var flat=Vector3.Normalize(new Vector3(snapped.X,0,snapped.Z));var flat0=Vector3.Normalize(new Vector3(before.X,0,before.Z));
            Check(Angle(flat,to)<14,"the head is on the pilot "+(NativeHordeRoam.Snap+.05f)+" s into the alert ("+Angle(flat,to).ToString("0.0")+" deg)");
            Check(Angle(flat0,to)>Angle(flat,to)+15,"before the alert it was not looking there");
            // A late joiner (its first frame mid-scream) draws the same moment as a peer that watched it from the idle.
            foreach(float age in new[]{.35f,.6f,.9f,1.2f})
            {
                // (The idle's blend out runs from the change a peer saw; one that joins after it draws the same pose.)
                if(age>NativeHordeRoam.Blend){var watched=SimNotice(10007,T,target,T+age,T-3).Item2;var late=SimNotice(10007,T,target,T+age,T+NativeHordeRoam.Blend).Item2;
                float diff=0;for(int i=0;i<watched.Length;i++)diff=Math.Max(diff,Math.Abs(watched[i]-late[i]));
                Check(diff<.02f,"a guest joining "+NativeHordeRoam.Blend+" s into the alert draws the same pose at "+age+" s (worst "+diff.ToString("0.0000")+" m)");}
                var t1=new NativeHordeRoamTrack();var t2=new NativeHordeRoamTrack();t1.Read(true,W(HordeRoamState.Idle,T-5),9,T-1,0,false);
                Check(t1.Read(true,W(HordeRoamState.Alert,T,T),9,T+age,0,false).AlertAge==t2.Read(true,W(HordeRoamState.Alert,T,T),9,T+age,0,false).AlertAge,"the alert age is the record's, not the peer's");
            }
            // The scream lifts the head and flings the arms: the pose mid-scream is not the alert's first frame.
            var first=SimNotice(10007,T,target,T+.02f,T-3).Item2;var mid=SimNotice(10007,T,target,T+NativeHordeRoam.ScreamAt+.35f,T-3).Item2;
            float moved=0;for(int i=0;i<first.Length;i++)moved=Math.Max(moved,Math.Abs(first[i]-mid[i]));Check(moved>.08f,"the scream moves the body ("+moved.ToString("0.00")+" m)");
        }
        // ---- the head looks at its pilot (the gait's look-at turned and pitched toward it) ----
        private static void LookAt()
        {
            foreach(int k in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Spitter})
            {
                var ahead=HeadAt(k,new Vector3(0,.9f,4));var left=HeadAt(k,new Vector3(-3,.9f,4));var right=HeadAt(k,new Vector3(3,.9f,4));var up=HeadAt(k,new Vector3(0,3.5f,2.5f));var down=HeadAt(k,new Vector3(0,-1.5f,2.5f));
                Check(left.X<ahead.X-.15f&&right.X>ahead.X+.15f,NativeHordeKinds.Ids[k]+": the head turns toward a pilot on either side");
                float P(Vector3 v)=>(float)(Math.Asin(Math.Max(-1,Math.Min(1,Vector3.Normalize(v).Y)))*180/Math.PI);
                Check(P(up)>P(ahead)+10&&P(down)<P(ahead)-8,NativeHordeKinds.Ids[k]+": and looks up at one above, down at one below ("+P(up).ToString("0")+" / "+P(ahead).ToString("0")+" / "+P(down).ToString("0")+" deg)");
            }
        }
        private static Vector3 HeadAt(int k,Vector3 target)
        {
            // (The face's direction averaged over the last second: the standing takes nod the head through tens of degrees, and the
            // secondary motion's loll and tics ride on that, so one frame is a sample of the take, not of the look.)
            var g=new NativeHordeGait();var p=new CapturedRig.Pose();var sum=Vector3.Zero;
            for(int i=0;i<240;i++)
            {
                var f=new HordeMotionInput{Kind=k,Id=70001,Seed=NativeHordeKinds.Seed(70001),Time=i/60f,Root=new Vector3(0,.85f,0),Health=1,AttackAge=float.NaN,HasTarget=true,Target=target};g.Pose(f,p);
                if(i<180)continue;p.World();var fw=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.Head]);sum+=g.ToWorld(p.WorldPosition[CapturedRig.Head]+fw,Vector3.Zero)-g.ToWorld(p.WorldPosition[CapturedRig.Head],Vector3.Zero);
            }
            return sum/60;
        }
        // ---- the wander: the slow take at the host's pace (and limping), planted feet; the hub's roster ----
        private static void Wander()
        {
            var pose=new CapturedRig.Pose();
            foreach(int k in new[]{NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Brute,NativeHordeKinds.Spitter})
            {
                Check(NativeZombieMotion.Wanders(k).Length>0&&NativeZombieMotion.Wanders(k).All(c=>NativeZombieClips.Speed(c)<.6f),NativeHordeKinds.Ids[k]+": wanders on the pack's slow walk");
                float pace=NativeHordeRoam.WanderSpeed(NativeHordeKinds.ProposedRow(k).walk);
                foreach(int clip in NativeZombieMotion.Wanders(k))foreach(float speed in new[]{pace,pace*.55f})foreach(float turn in new[]{0f,30f})
                {
                    int id=61000+k*1000;while(NativeZombieMotion.Wander(k,NativeHordeKinds.Seed(id))!=clip)id++;
                    var g=new NativeHordeGait();var tr=new NativeHordeRoamTrack();var last=new Vector3[2];var air=new bool[2];float worst=0,worstDrawn=0;int steps=0,planted=0,off=0;var at=new Vector3(0,.85f,0);float yaw=0;
                    for(int i=0;i<600;i++)
                    {
                        float t=i/60f;yaw+=turn/60;float rr=yaw*(float)Math.PI/180;var v=new Vector3((float)Math.Sin(rr),0,(float)Math.Cos(rr))*speed;at+=v/60;
                        var f=new HordeMotionInput{Kind=k,Id=id,Seed=NativeHordeKinds.Seed(id),Health=1,AttackAge=float.NaN,Time=t,Root=at,Velocity=v,Yaw=yaw,Ground=0,Roam=tr.Read(true,W(HordeRoamState.Wander,-5),id,t,speed,false)};g.Pose(f,pose);
                        if(i==300)Check(g.WanderClip==clip&&g.WanderTakeShare==1,NativeHordeKinds.Ids[k]+": wandering, the stride is the wander take's");
                        for(int j=0;j<2;j++)
                        {
                            var drawn=g.ToWorld(pose.WorldPosition[j==0?CapturedRig.LFoot:CapturedRig.RFoot],at);
                            if(i>90&&!air[j]&&!g.FootAir[j]){worst=Math.Max(worst,Vector3.Distance(last[j],g.FootWorld[j]));float e=new Vector2(drawn.X-g.FootWorld[j].X,drawn.Z-g.FootWorld[j].Z).Length();worstDrawn=Math.Max(worstDrawn,e);if(e>.012f)off++;planted++;}
                            if((g.Landed&(1<<j))!=0)steps++;last[j]=g.FootWorld[j];air[j]=g.FootAir[j];
                        }
                    }
                    string what=NativeHordeKinds.Ids[k]+" wandering on "+NativeZombieClips.Key(clip)+" at "+speed.ToString("0.00")+" m/s turning "+turn;
                    Check(worst<1e-4f,what+": planted feet never slide");Check(steps>=5&&planted>steps,what+": it steps ("+steps+")");
                    // (The brute's heavy stride below its 1.3 m/s walk lags a planted ankle a few frames on every take: its gait's own.)
                    if(k==NativeHordeKinds.Brute)Check(worstDrawn<.14f&&off*100<planted*2,what+": the drawn ankle stays near its spot (worst "+worstDrawn.ToString("0.000")+" m, "+off+" of "+planted+" planted frames over 1.2 cm)");
                    else Check(worstDrawn<.015f,what+": the drawn ankle of a planted foot is on its spot ("+worstDrawn.ToString("0.0000")+" m)");
                }
            }
            // Off the town (Roam off) the stride is the kind's own walk.
            var g0=new NativeHordeGait();var p0=new CapturedRig.Pose();for(int i=0;i<60;i++)g0.Pose(new HordeMotionInput{Kind=0,Id=61001,Seed=NativeHordeKinds.Seed(61001),Health=1,AttackAge=float.NaN,Time=i/60f,Root=new Vector3(0,.85f,i/60f),Velocity=new Vector3(0,0,1)},p0);
            Check(g0.WanderTakeShare==0,"off the town the walk is the kind's own");
        }
        // ---- no allocation: 16 town zombies through every state ----
        private static void Allocation()
        {
            var gaits=new NativeHordeGait[16];var tracks=new NativeHordeRoamTrack[16];var p=new CapturedRig.Pose();var w=new float[NativeHordeRoam.IdleVariants];
            for(int i=0;i<16;i++){gaits[i]=new NativeHordeGait();tracks[i]=new NativeHordeRoamTrack();}
            void Frame(int n)
            {
                float t=n/60f;
                for(int i=0;i<16;i++)
                {
                    int id=10100+i;var s=(HordeRoamState)(1+(i+n/240)%5);float speed=s==HordeRoamState.Chase?3.4f:s==HordeRoamState.Wander||s==HordeRoamState.Search?.65f:0;
                    var wire=i<8?new HordeRoamWire{State=s,At=i%2==0?float.NaN:(n/240)*4f,AlertAt=(n/240)*4f-.6f,HasLookYaw=i%2==0,LookYaw=30}:default;
                    var r=tracks[i].Read(true,wire,id,t,speed,false);
                    var f=new HordeMotionInput{Kind=i%4,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Root=new Vector3(i*2,.85f,t*speed),Velocity=new Vector3(0,0,speed),Health=1,AttackAge=float.NaN,HasTarget=!NativeHordeRoam.Calm(r.State),Target=new Vector3(i*2+3,.9f,t*speed+4),Roam=r};
                    gaits[i].Pose(f,p);NativeHordeRoam.Variants(f.Seed,t,w,out _);NativeHordeRoam.MoanAt(f.Seed,t);
                }
            }
            for(int n=0;n<300;n++)Frame(n);long before=GC.GetAllocatedBytesForCurrentThread();for(int n=300;n<1500;n++)Frame(n);long used=GC.GetAllocatedBytesForCurrentThread()-before;
            Check(used==0,"no allocation over 1200 frames of 16 roaming adds in every state ("+used+" bytes)");
        }
        // ---- presentation only, the town only, one read of the record ----
        private static void Source(string root)
        {
            foreach(var f in new[]{"NativeHordeRoam.cs","NativeHordeRoamMotion.cs"}){var t=R(root,"Assets/ChipArena/Core/"+f);Check(!t.Contains("NativeArena")&&!t.Contains("NativePilot")&&!t.Contains("UnityEngine")&&!t.Contains("new Random("),f+" reads no authority state, no engine and no clock-free randomness");}
            var rp=R(root,"Assets/ChipArena/Runtime/NativeHordeRoamPresentation.cs");
            Check(rp.Contains("public static HordeRoamWire Read(NativeMinion m)=>NativeHordeRoam.FromHub(m?.Pve?.Hub);"),"the host's record (NativePveAdd.Hub) is read in one place, through the one mapping");
            Check(rp.Contains("m.Pve.Stats.id!=\"hulk\"")&&rp.Contains("public static bool Roams(NativeMinion m)=>false;"),"the Hulk keeps its own; adds without the record infer standing and hunting only");
            Check(rp.Contains("if(wire.State==HordeRoamState.None)return false;"),"the moans and the shriek are the roaming population's (every other add calls as it hunts, NativeZombieAudio)");
            var all=Directory.EnumerateFiles(Path.Combine(root,"Assets/ChipArena"),"*.cs",SearchOption.AllDirectories).ToDictionary(f=>Path.GetRelativePath(Path.Combine(root,"Assets/ChipArena"),f).Replace('\\','/'),File.ReadAllText);
            Check(all.Where(x=>x.Key!="Core/NativeHordeRoam.cs").All(x=>!x.Value.Contains("new HordeRoamWire"))&&Regex.Matches(all["Core/NativeHordeRoam.cs"],"new HordeRoamWire").Count==1,"nothing but the mapping builds the record's read");
            Check(Regex.Matches(rp,"NativeHordeRoamRecord\\.Read\\(").Count==2,"the presentation and the voices read it through the one function");
            var hubReaders=all.Where(x=>Regex.IsMatch(x.Value,"\\.Pve\\??\\.Hub\\b")).Select(x=>x.Key).OrderBy(x=>x).ToList();
            // (The authority's own files read it too: the hub authority, its validation and the frame.)
            Check(hubReaders.Where(x=>Regex.IsMatch(x,"Presentation|Audio|Fx|Hud|Draw|Motion|Core/")).All(x=>x=="Runtime/NativeHordeRoamPresentation.cs")&&hubReaders.Contains("Runtime/NativeHordeRoamPresentation.cs"),"only the mapping reads NativePveAdd.Hub in the presentation ("+string.Join(", ",hubReaders)+")");
            foreach(var bad in new[]{".Hp=","Damage(","Emit(","StaggerAt=","RecoverAt=","StrikeAt=","LastAttack=","Body.position=","linearVelocity=","Hub=","AddComponent<"})Check(!rp.Contains(bad),"the roaming presentation never writes `"+bad+"`");
            Check(!rp.Contains("Play(")&&rp.Contains("ZombieCall(m.Id,NativeZombieCall.Shriek,")&&rp.Contains("ZombieCall(m.Id,NativeZombieCall.Moan,"),"the voices go through the zombie voice (bank, stand-in, built-in take), never a direct play");
            var kp=R(root,"Assets/ChipArena/Runtime/NativeHordeKindPresentation.cs");
            Check(kp.Contains("var roam=HordeRoam(arena,m,s,now,entering,ref target);")&&kp.Contains("RoarAge=roar,Move=move};f.Roam=roam;")&&kp.Contains("HordeRoamCue(arena,m,kind,pos,t);"),"hooked where the adds are posed and cued");
            var keys=new HashSet<string>(NativeSoundKeys.All.Select(d=>d.Key));Check(keys.Contains("pve.zombie.moan")&&keys.Contains("pve.zombie.shriek")&&keys.Contains("pve.add.death")&&keys.Contains("siren.rush"),"the voice keys and their stand-ins are bank keys");
        }

        // ---- helpers: a standing add posed through `seconds` with a state from the record ----
        private static (CapturedRig.Pose,float[],NativeHordeGait) Sim(int kind,int id,HordeRoamState state,float at,float alertAt,Vector3? target,float until)
        {
            var g=new NativeHordeGait();var p=new CapturedRig.Pose();var tr=new NativeHordeRoamTrack();var flat=new float[22*3];
            for(float t=Math.Max(0,until-3);t<=until+1e-4f;t+=1f/60)
            {
                var r=tr.Read(true,new HordeRoamWire{State=state,At=at,AlertAt=alertAt},id,t,0,false);
                g.Pose(new HordeMotionInput{Kind=kind,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Root=new Vector3(0,.85f,0),Health=1,AttackAge=float.NaN,HasTarget=target.HasValue&&!NativeHordeRoam.Calm(r.State),Target=target??default,Roam=r},p);
            }
            p.World();for(int i=0;i<22;i++){var w=g.ToWorld(p.WorldPosition[i],new Vector3(0,.85f,0));flat[i*3]=w.X;flat[i*3+1]=w.Y;flat[i*3+2]=w.Z;}
            return (p,flat,g);
        }
        // Idle until T, the host's alert at T toward the pilot at `target` (its look yaw); posed from `start` (a late joiner's first
        // frame) to `until`.
        private static (CapturedRig.Pose,float[]) SimNotice(int id,float T,Vector3 target,float until,float start)
        {
            var g=new NativeHordeGait();var p=new CapturedRig.Pose();var tr=new NativeHordeRoamTrack();var flat=new float[22*3];
            for(float t=start;t<=until+1e-4f;t+=1f/60)
            {
                var wire=t<T?new HordeRoamWire{State=HordeRoamState.Idle,At=float.NaN}:new HordeRoamWire{State=HordeRoamState.Alert,At=float.NaN,AlertAt=T,HasLookYaw=true,LookYaw=(float)(Math.Atan2(target.X,target.Z)*180/Math.PI)};
                var r=tr.Read(true,wire,id,t,0,false);
                g.Pose(new HordeMotionInput{Kind=0,Id=id,Seed=NativeHordeKinds.Seed(id),Time=t,Root=new Vector3(0,.85f,0),Health=1,AttackAge=float.NaN,HasTarget=!NativeHordeRoam.Calm(r.State),Target=target,Roam=r},p);
            }
            p.World();for(int i=0;i<22;i++){var w=g.ToWorld(p.WorldPosition[i],new Vector3(0,.85f,0));flat[i*3]=w.X;flat[i*3+1]=w.Y;flat[i*3+2]=w.Z;}
            return (p,flat);
        }
        // The head's world facing (and its world position) for a pose at the origin facing +z.
        private static Vector3 Head(CapturedRig.Pose p,out Vector3 at)
        {
            p.World();var g=new NativeHordeGait();g.Pose(new HordeMotionInput{Kind=0,Id=1,Seed=1,Root=new Vector3(0,.85f,0),Health=1,AttackAge=float.NaN},new CapturedRig.Pose());
            var fw=Vector3.Transform(Vector3.UnitZ,p.WorldRotation[CapturedRig.Head]);at=g.ToWorld(p.WorldPosition[CapturedRig.Head],new Vector3(0,.85f,0));return Vector3.Normalize(g.ToWorld(p.WorldPosition[CapturedRig.Head]+fw,Vector3.Zero)-g.ToWorld(p.WorldPosition[CapturedRig.Head],Vector3.Zero));
        }
        private static float Angle(Vector3 a,Vector3 b)=>(float)(Math.Acos(Math.Max(-1,Math.Min(1,Vector3.Dot(Vector3.Normalize(a),Vector3.Normalize(b)))))*180/Math.PI);
    }
}
#endif
