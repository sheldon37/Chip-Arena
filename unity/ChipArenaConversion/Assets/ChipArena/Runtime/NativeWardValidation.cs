#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateWardSimulation()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Ward native simulation: "+why);}
            // Keep the same round used to calculate alternating hazard schedules.
            void Clock(double now)=>Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=now,fightAt=10,round=Match.Round,set=Match.Set});
            ReplicaScene=root.scene;Reset();var p=Local;var connected=Pilots.ToDictionary(q=>q.Seat.id,q=>q.Seat.connected);
            try
            {
                foreach(var other in Pilots.Where(q=>q!=p)){other.Seat.connected=false;other.Collider.enabled=false;}
                void Place(Vector3 at)
                {
                    p.Hp=1000;p.Stats["hp"]=1000;p.Stats["damageTaken"]=.5;p.Seat.connected=p.Seat.alive=true;
                    p.Collider.enabled=true;p.Body.isKinematic=false;p.Body.position=at;p.Body.linearVelocity=p.Knockback=Vector3.zero;
                    p.ShieldUntil=0;p.Death=null;p.Kernel=null;p.Roll=null;p.RollEnd=0;Physics.SyncTransforms();
                }
                foreach(var h in Map.hazards.Where(h=>h.kind=="burner"||h.kind=="gas"))
                {
                    double live=10+h.first+(StageRound%2==0?h.offset:0)+h.warn;
                    var area=h.kind=="burner"?h.zones[0]:new MapVolume{center=h.center,size=h.size};
                    Place(Vec(new V3(area.center.x,area.center.y-area.size.y/2+.9f,area.center.z)));
                    for(int i=0;i<(int)(h.live*60);i++){Clock(live+i/60d);StepAuthoredHazards();}
                    float expected=1000-h.damage*h.live/h.tick*.5f;
                    Check(Math.Abs(p.Hp-expected)<.02,h.id+" mitigated tick count: expected HP "+expected+", actual "+p.Hp+", stage round "+StageRound);
                    float hp=p.Hp;Clock(live+h.live+.2);StepAuthoredHazards();Check(p.Hp==hp,h.id+" clear harmless");
                    if(h.kind=="burner"){Place(Vec(h.center));Clock(live);StepAuthoredHazards();Check(p.Hp==1000,h.id+" pocket harmless");}
                }
                var shutter=Map.hazards.First(h=>h.kind=="shutter");double closes=10+shutter.first+(StageRound%2==0?shutter.offset:0)+shutter.warn;
                Place(Vec(new V3(shutter.center.x,shutter.center.y-shutter.size.y/2+.9f,shutter.center.z)));
                Clock(closes);PoseStageLoads(Match.Time,true);
                Check(!stageLoads[shutter.id].enabled,"occupied shutter interlocks");
                for(int i=1;i<90;i++){Clock(closes+i/60d);PoseStageLoads(Match.Time,true);Physics.SyncTransforms();}
                Check(p.Hp==1000,"shutter does no damage");
                Check(!NativeStageHazards.Touch(Vec(p.Body.position),shutter.center,shutter.size)&&stageLoads[shutter.id].enabled,"shutter pushes clear before solid");
                var entry=StageContent.entries.First(e=>e.kind=="breach");Clock(100);BeginWardBreach(entry,Time+NativeBreach.Knock+NativeBreach.Through);
                var record=wardBreaches.Single();var panel=StaticBlocks.Single(b=>b.Definition.id==entry.block).GetComponent<Collider>();
                Place(Vec(entry.@exit.Position(.9f)));Clock(record.breakAt);PoseWardBreaches();Check(!panel.enabled,"panel breaks");
                Place(Vec(entry.center));Clock(record.restoreAt);float previous=record.restoreAt;PoseWardBreaches();Check(record.restoreAt>previous&&!panel.enabled,"occupied restore postponed");
                Place(Vec(entry.@exit.Position(.9f)));Clock(NativeBreach.SolidAt(record.restoreAt));PoseWardBreaches();Check(panel.enabled,"panel solid after rebuild");
                var frame=NativeFrameCodec.Clone(NetworkFrame());
                frame.pve=new NativePveFrame{breaches=wardBreaches.Select(b=>b.ViewCopy()).ToArray(),rows=Array.Empty<NativePveRowFrame>(),pickups=Array.Empty<NativePickupFrame>()};
                // NetworkFrame uses wardBreaches outside PvE. Moving the fixture to PvE
                // must clear that container: real frames never carry both forms together.
                frame=NativeFrameCodec.Clone(frame);Check(!frame.Valid(Content),"duplicate Sandbox/PvE breach containers rejected");
                frame.wardBreaches=null;
                frame=NativeFrameCodec.Clone(frame);Check(frame.Valid(Content)&&frame.pve.breaches[0].restoreAt==record.restoreAt,"real JsonUtility breach frame");
                frame.pve.breaches[0].restoreAt=float.NaN;Check(!frame.Valid(Content),"malformed breach rejected");
                foreach(string kind in new[]{"burner","gas"})
                {frame=NativeFrameCodec.Clone(NetworkFrame());frame.pilots[0].death=new NativeDeath{damageKind=kind};frame=NativeFrameCodec.Clone(frame);Check(frame.Valid(Content),"wire death "+kind);}
                ValidateWardHulkCrowd();
                // Real runtime obstruction, collider and JsonUtility gate (also exercises the Sandbox frame).
                Clock(200);wardSandboxState=new NativePveState{Content=StageContent,Boss=p,Cap=8,SpawnRandom=new NativePveRandom(12345)};
                var hulk=SpawnPveAdd("hulk",true,entry.e);Check(hulk!=null,"dynamic Hulk fixture spawn");
                var wall=StaticBlocks.Single(b=>b.Definition.id=="siren-atrium-wall-e-2");var wallCollider=wall.GetComponent<Collider>();
                hulk.Pve.SpawnAt=hulk.Pve.ActiveAt=0;hulk.Body.position=new Vector3(14.8f,NativeHordeKinds.HulkHeight/2,-2.5f);
                var goal=new Vector3(18,NativeHordeKinds.HulkHeight/2,-2.5f);Place(new Vector3(20,.9f,0));
                for(int tick=0;tick<=240;tick++){Clock(200+tick/60d);StepHulkBreach(hulk,goal);PoseWardBreaches();}
                var dynamicRecord=wardBreaches.Single(b=>b.Dynamic&&b.block==wall.Definition.id);
                Check(dynamicRecord.breakAt<=204&&!wallCollider.enabled,"Hulk narrow edge breaks within four seconds");
                frame=NativeFrameCodec.Clone(NetworkFrame());Check(frame.Valid(Content),"dynamic real JsonUtility frame valid");
                var records=frame.pve?.breaches??frame.wardBreaches;var remote=records.Single(b=>b.Dynamic);
                Check(remote.actor==hulk.Id&&(remote.point-dynamicRecord.point).Length<.001f&&(remote.normal-dynamicRecord.normal).Length<.001f&&(remote.holeSize-dynamicRecord.holeSize).Length<.001f,"dynamic guest payload round trip");
                bool wasReplica=Replica;Replica=true;
                try
                {
                    ApplyWardBreaches(records);Check(!wallCollider.enabled,"guest opens collider");
                    Clock(remote.breakAt-.01f);ApplyWardBreaches(records);Check(wallCollider.enabled,"replay rewind restores collider");
                    Clock(remote.breakAt);ApplyWardBreaches(records);Check(!wallCollider.enabled,"replay break opens collider");
                    ApplyWardBreaches(Array.Empty<NativeBreachRecord>());Check(wallCollider.enabled,"absent frame restores collider");
                }
                finally{Replica=wasReplica;}
                ApplyWardBreaches(records);dynamicRecord=wardBreaches.Single(b=>b.Dynamic);
                Clock(dynamicRecord.restoreAt);Place(Vec(wall.Definition.pos));PoseWardBreaches();
                Check(dynamicRecord.restoreAt>remote.restoreAt&&!wallCollider.enabled,"pilot holds dynamic opening");
                Place(new Vector3(20,.9f,0));hulk.Body.position=Vec(wall.Definition.pos);Clock(NativeBreach.SolidAt(dynamicRecord.restoreAt));PoseWardBreaches();
                Check(!wallCollider.enabled,"minion holds dynamic opening at solidity");
                hulk.Body.position=new Vector3(20,1.53f,3);Clock(NativeBreach.SolidAt(dynamicRecord.restoreAt));PoseWardBreaches();Check(wallCollider.enabled,"dynamic collider rebuilt");
                Debug.Log("PASS Ward native hazards, entry/dynamic Hulk breaches, occupied rebuild, guest frames and replay rewind");
            }
            finally{foreach(var q in Pilots)if(connected.TryGetValue(q.Seat.id,out bool value))q.Seat.connected=value;Reset();}
        }
        private void ValidateWardHulkCrowd()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Ward Hulk/crowd: "+why);}
            void Clock(double now)=>Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=now,fightAt=10,round=Match.Round,set=Match.Set});
            var savedPve=Pve;var savedNav=Map.nav;var content=StageContent;var savedHides=content.hideSpots;
            NativePropBody platform=null;NativeMinion hulk=null;var p=Local;var smallWave=new System.Collections.Generic.List<NativeMinion>();
            try
            {
                platform=AddProp(new ContentBlock{id="hulk-check-floor",material="foundation",role="static",pos=new V3(1000,-.5f,1000),size=new V3(16,1,16),rotation=new ContentRotation()});
                Map.nav=new MapNav{nodes=new[]{
                    new MapNavNode{id="start",pos=new V3(1000,.9f,1000)},new MapNavNode{id="mid",pos=new V3(1000,.9f,1002)},
                    new MapNavNode{id="chase",pos=new V3(1000,.9f,1004)},new MapNavNode{id="hide",pos=new V3(1004,.9f,1000)}},
                    links=new[]{new MapNavLink{id="a",a="start",b="mid"},new MapNavLink{id="b",a="mid",b="chase"},new MapNavLink{id="c",a="start",b="hide"}}};
                navMap=null;navRoutes.Clear();crowdRoutes.Clear();
                content.hideSpots=new[]{new PvePoint{x=1004,z=1000}};
                Clock(300);p.Body.position=new Vector3(1000,.875f,1004);p.Body.linearVelocity=Vector3.zero;p.Body.isKinematic=true;
                Pve=new NativePveState{Content=content,Boss=p,Started=true,Stage="wave",Wave=1,Cap=8,SpawnRandom=new NativePveRandom(12345)};
                hulk=SpawnPveAdd("hulk",true,content.entries.First(e=>e.hulk&&e.kind=="drop").e);
                Check(hulk!=null,"authored Hulk spawns");hulk.Pve.ActiveAt=hulk.Pve.ExposedAt=Time-1;hulk.Pve.Target=p;hulk.Pve.ThinkAt=Time+10;
                hulk.Body.position=new Vector3(1000,NativeHordeKinds.HulkHeight/2+.025f,1000);Physics.SyncTransforms();
                for(int i=0;i<30;i++){Clock(300+i/60d);StepPveHorde();root.scene.GetPhysicsScene().Simulate(1f/60);}
                Check(hulk.Body.position.z>1000.25f,"full-size Hulk advances on supported route");
                Check(Math.Abs(hulk.Collider.height-NativeHordeKinds.HulkHeight)<.001f&&Math.Abs(hulk.Collider.radius-NativeHordeKinds.HulkRadius)<.001f,"Hulk collider preserved");
                // Exercise the real horde caller with eight adds: this used to select eight unbounded A* searches.
                for(int i=0;i<7;i++)
                {
                    var mob=SpawnPveAdd("shambler",true);
                    Check(mob!=null,"small wave spawn");smallWave.Add(mob);mob.Pve.ActiveAt=mob.Pve.ExposedAt=Time-1;
                    mob.Pve.Target=p;mob.Pve.ThinkAt=Time+10;mob.Pve.RecoverAt=Time-1;mob.Body.position=new Vector3(1000,.875f,1000);
                }
                navRoutes.Clear();crowdRoutes.Clear();Physics.SyncTransforms();Clock(300.5);int smallBefore=NavigationSearches,smallLinks=NavigationLinkChecks;
                StepPveHorde();Check(NavigationSearches-smallBefore==1,"eight-add wave shares one budgeted goal");
                Check(NavigationLinkChecks-smallLinks<=512+8,"small wave cannot perform map-wide searches per add");
                foreach(var mob in smallWave)RemoveMinion(mob,false);smallWave.Clear();
                navRoutes.Clear();crowdRoutes.Clear();Clock(300.6);int crowdBefore=NavigationSearches;
                for(int i=0;i<192;i++)
                {
                    var move=StructureNavMove(-1000-i,new Vector3(1000,.875f,1000),p.Body.position,Vector3.forward,p.Seat.id);
                    Check(move.z>.9f,"crowd follows shared destination");
                }
                Check(NavigationSearches-crowdBefore==1,"192 mobs share one native route-tree build");
                navRoutes.Clear();crowdRoutes.Clear();hulk.Pve.Possessed=true;hulk.Pve.PossessedAt=301;hulk.Pve.HideThinkAt=0;
                int before=NavigationSearches;
                for(int i=0;i<12;i++){Clock(301+i/60d);StepPveHorde();root.scene.GetPhysicsScene().Simulate(1f/60);}
                Check(NavigationSearches-before==1,"possessed hide route is cached, not chase/hide rebuilt every tick");
                Map.nav=savedNav;navMap=null;PrepareNavigation();
                // Compare local attachment against the old exhaustive predicate with real collision/hazards.
                foreach(var point in Map.spawns.ffa.Select(s=>s.pos).Concat(content.entries.Select(e=>e.@exit.Position(.9f))))
                {
                    var actual=NearestNavNode(Vec(point));navOrigin=Vec(point);var expected=navSearch.Nearest(point,navUsable);
                    if(expected!=null&&(expected.pos-point).Length>8)expected=null;
                    Check(ReferenceEquals(actual,expected),"bounded attachment preserves authored spawn/entry access");
                }
                bool completed=false;var goal=Vec(Map.nav.nodes[0].pos);
                for(int tick=0;tick<120&&!completed;tick++)
                {
                    Clock(302+tick/60d);int linksBefore=NavigationLinkChecks;
                    var flow=AdvanceCrowdRoute(p.Seat.id,goal);
                    int first=NavigationLinkChecks;
                    for(int i=0;i<192;i++)AdvanceCrowdRoute(p.Seat.id,goal);
                    Check(NavigationLinkChecks==first,"192 callers do not multiply the per-tick flow budget");
                    Check(first-linksBefore<=512,"native flow physics-query slice stays bounded");
                    completed=flow.ready&&!flow.pending;
                }
                Check(completed,"budgeted Ward flow completes");
                Debug.Log("PASS Ward full-size Hulk PhysX movement, 192 shared crowd routes, bounded live flow slices and possessed route cache");
            }
            finally
            {
                foreach(var mob in smallWave)if(Minions.Contains(mob))RemoveMinion(mob,false);
                if(hulk!=null&&Minions.Contains(hulk))RemoveMinion(hulk,false);
                if(platform!=null)RemoveStructureProp(platform);
                content.hideSpots=savedHides;Map.nav=savedNav;Pve=savedPve;navMap=null;navRoutes.Clear();crowdRoutes.Clear();
                p.Body.isKinematic=false;Physics.SyncTransforms();
            }
        }

    }
}
#endif
