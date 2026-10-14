using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private NativePveState wardSandboxState;
        public bool SandboxWardEntry(int entry)
        {
            if(!SandboxAllowed||Map.look!="quarantine")return false;
            var e=StageContent.entries.FirstOrDefault(e=>e.e==entry);if(e==null)return false;
            if(Pve==null&&wardSandboxState==null)wardSandboxState=new NativePveState{Content=StageContent,Boss=Pilots.FirstOrDefault(p=>p!=Local)??Local,Stage="wave",Wave=1,Cap=8,SpawnRandom=new NativePveRandom(12345)};
            return SpawnPveAdd(e.kind=="breach"||e.hulk?"hulk":e.kind=="drop"?"brute":"shambler",true,entry)!=null;
        }
        private void StepWardSandbox()
        {
            if(!SandboxAllowed||Pve!=null)return;
            foreach(var m in Minions)
            {
                if(m.Pve==null||m.DevMonster||m.Hp<=0)continue;
                if(Time<m.Pve.ActiveAt){WardEntryPose(m);continue;}
                if(m.Body.isKinematic){m.Body.isKinematic=false;m.Collider.enabled=true;m.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
                if(m.Pve.Stats.id!="hulk"||Local==null||Local.Hp<=0)continue;
                var at=m.Body.position;var target=Local.Body.position;var delta=target-at;delta.y=0;
                var dir=WardHulkDirection(m,target,delta.normalized);
                var move=delta.magnitude>m.Pve.Stats.startRange?dir*m.Pve.Stats.run:Vector3.zero;
                if(StepHulkBreach(m,target)||!SupportedStageMove(WardNavPosition(m),move))move=Vector3.zero;
                if(move.sqrMagnitude>.001f)m.Yaw=Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg;
                m.Body.linearVelocity=new Vector3(move.x,m.Body.linearVelocity.y,move.z);
            }
        }
        private readonly List<NativeBreachRecord> wardBreaches=new List<NativeBreachRecord>(64);
        public IEnumerable<NativeBreachRecord> WardBreaches=>wardBreaches;
        private int hulkWallCount=-1;
        private readonly List<(NativePropBody wall,Collider collider)> hulkWalls=new List<(NativePropBody,Collider)>();
        private void NameWardPanels()
        {
            hulkWallCount=StaticBlocks.Count;hulkWalls.Clear();if(Map.look!="quarantine")return;
            foreach(var wall in StaticBlocks)if(NativeHulkBreach.Eligible(Map,wall.Definition))hulkWalls.Add((wall,wall.GetComponent<Collider>()));
            foreach(var e in StageContent.entries.Where(e=>e.kind=="breach"))
            {var prop=StaticBlocks.FirstOrDefault(p=>p.Definition.id==e.block);if(prop!=null)prop.gameObject.name=NativeBreach.ObjectName(e.id);}
        }
        private bool BreachAvailable(PveEntry e)=>!wardBreaches.Any(b=>b.entry==e.e&&Time<NativeBreach.SolidAt(b.restoreAt));
        private void BeginWardBreach(PveEntry e,float active)
        {
            wardBreaches.RemoveAll(b=>b.entry==e.e);
            float broken=NativeBreach.BreakAt(active);
            wardBreaches.Add(new NativeBreachRecord{entry=e.e,breakAt=broken,restoreAt=NativeBreach.DefaultRestoreAt(broken)});
        }
        private int wardBreachRevision;
        private NativePropBody WardBlock(string id)
        {foreach(var p in StaticBlocks)if(p.Definition.id==id)return p;return null;}
        private ContentBlock BreachBlock(NativeBreachRecord b)
        {
            if(b.Dynamic)return WardBlock(b.block)?.Definition;
            foreach(var e in StageContent.entries)if(e.e==b.entry&&e.kind=="breach")return WardBlock(e.block)?.Definition;
            return null;
        }
        private bool BreachOccupied(ContentBlock wall)
        {
            foreach(var p in Pilots)if(p.Hp>0&&p.Seat.connected&&NativeStageHazards.Touch(Vec(p.Body.position),wall.pos,wall.size,p.Collider.radius,p.Collider.height/2))return true;
            foreach(var m in Minions)if(m.Hp>0&&NativeStageHazards.Touch(Vec(m.Body.position),wall.pos,wall.size,m.Collider.radius,m.Collider.height/2))return true;
            return false;
        }
        private void PoseWardBreaches()
        {
            if(Map.look!="quarantine")return;
            foreach(var b in wardBreaches)
            {
                var wall=BreachBlock(b);if(wall==null)continue;var collider=WardBlock(wall.id).GetComponent<Collider>();
                // Only open colliders extend: a long-expired intact record must not reopen on approach.
                if(!Replica&&!collider.enabled)NativeHulkBreach.HoldOpen(b,Time,BreachOccupied(wall));
                bool solid=NativeHulkBreach.Solid(b,Time);
                if(collider.enabled!=solid){collider.enabled=solid;wardBreachRevision++;}
            }
        }
        private void ApplyWardBreaches(NativeBreachRecord[] records)
        {
            // Replay can jump backwards, or omit an expired record: restore old collision before applying.
            foreach(var b in wardBreaches){var wall=BreachBlock(b);if(wall!=null)WardBlock(wall.id).GetComponent<Collider>().enabled=true;}
            wardBreaches.Clear();if(records!=null)foreach(var b in records)wardBreaches.Add(b.ViewCopy());
            PoseWardBreaches();
        }
        private Vector3 WardNavPosition(NativeMinion m)=>Vec(NativeNavSearch.BodyAnchor(Vec(m.Body.position),m.Collider.height/2));
        private Vector3 WardHulkDirection(NativeMinion m,Vector3 target,Vector3 fallback)
        {
            // Nav nodes are pilot-centre positions. A 3.06 m Hulk stands .63 m higher.
            var breach=m.Pve.WallBreach;
            if(breach!=null&&Time>=breach.breakAt&&Time<breach.breakAt+3f)return Vec(breach.normal);
            if(m.Pve.HasBreachShortcut)return m.Pve.BreachShortcut;
            var navAt=WardNavPosition(m);
            return StructureNavMove(m.Id,navAt,target,fallback);
        }
        private bool FindHulkShortcut(NativeMinion m,Vector3 target,out Vector3 direction)
        {
            direction=Vector3.zero;var at=m.Body.position;var navAt=WardNavPosition(m);var delta=target-navAt;delta.y=0;float direct=delta.magnitude;
            float routeLength=float.PositiveInfinity;
            if(navRoutes.TryGetValue(m.Id,out var route)&&route.path.Count>0)
            {routeLength=0;var last=navAt;for(int i=route.step;i<route.path.Count;i++){var point=Vec(navNodes[route.path[i]].pos);routeLength+=Vector3.Distance(last,point);last=point;}routeLength+=Vector3.Distance(last,target);}
            if(!NativeHulkBreach.Shorter(routeLength,direct,navAt.y-target.y))return false;
            var dir=delta.normalized;float pole=m.Collider.height*.5f-m.Collider.radius;
            var scene=root.scene.GetPhysicsScene();
            if(!scene.CapsuleCast(at+Vector3.up*pole,at-Vector3.up*pole,m.Collider.radius-.03f,dir,out var hit,Math.Min(6,direct),WorldMask,QueryTriggerInteraction.Ignore))return false;
            var wall=hit.collider.GetComponent<NativePropBody>();if(wall==null||!NativeHulkBreach.Eligible(Map,wall.Definition))return false;
            var exit=Vec(NativeHulkBreach.Exit(wall.Definition,Vec(at),Vec(at+dir*direct),m.Collider.radius));
            var beyond=target+Vector3.up*(m.Collider.height/2-NativeLocomotionRules.HalfHeight)-exit;float length=beyond.magnitude;
            if(Vector3.Dot(exit-at,dir)>=direct||length<.1f||UnsafeStructurePath(navAt,exit-Vector3.up*(m.Collider.height/2-NativeLocomotionRules.HalfHeight)))return false;
            if(scene.CapsuleCast(exit+Vector3.up*pole,exit-Vector3.up*pole,m.Collider.radius-.03f,beyond.normalized,out _,length,WorldMask,QueryTriggerInteraction.Ignore))return false;
            // Require a supported same-floor landing on the far side; never shortcut a shaft or ledge.
            if(!scene.Raycast(exit,Vector3.down,out var floor,m.Collider.height/2+.2f,WorldMask,QueryTriggerInteraction.Ignore)||Math.Abs(floor.point.y-(at.y-m.Collider.height/2))>.25f)return false;
            direction=dir;return true;
        }
        private bool StepHulkBreach(NativeMinion m,Vector3 target)
        {
            var a=m.Pve;
            if(hulkWallCount!=StaticBlocks.Count)NameWardPanels();
            if(Replica||!NativeMapRules.WardInstance(Map.id)||a.Stats.id!="hulk"||a.Crawl||Time<a.ActiveAt)return false;
            if(a.WallBreach!=null&&Time<a.WallBreach.breakAt)
            {m.Yaw=Mathf.Atan2(a.WallBreach.normal.x,a.WallBreach.normal.z)*Mathf.Rad2Deg;return true;}
            // Traverse the opened block even when the authored graph had no link through solid wall.
            // Normal floor/hazard support and PhysX still constrain this short local steering leg.
            if(a.WallBreach!=null&&Time<a.WallBreach.breakAt+3f)return false;
            a.WallBreach=null;
            if(Time<a.StaggerAt||Time<a.RecoverAt||!a.Struck){a.WallProgress.tracking=false;return false;}
            var at=m.Body.position;var waypoint=target;
            // Shortcut work is bounded to two Hulks at 4 Hz; the normal stuck probe remains cheap.
            if(Time>=a.BreachThinkAt)
            {a.BreachThinkAt=Time+.25f;a.HasBreachShortcut=FindHulkShortcut(m,target,out a.BreachShortcut);}
            if(a.HasBreachShortcut)waypoint=at+a.BreachShortcut*6;
            if(!a.HasBreachShortcut&&navRoutes.TryGetValue(m.Id,out var route)&&route.path.Count>0&&route.step<route.path.Count)
                waypoint=Vec(navNodes[route.path[route.step]].pos);
            // The destination remains useful when the point-sized graph has no route through the blockage.
            var delta=waypoint-at;delta.y=0;if(delta.sqrMagnitude<.04f){waypoint=target;delta=target-at;delta.y=0;}
            NativePropBody chosen=null;Vector3 point=Vector3.zero;float nearest=float.MaxValue;
            foreach(var candidate in hulkWalls)
            {
                var prop=candidate.wall;if(prop==null||candidate.collider==null||!candidate.collider.enabled)continue;
                if(!NativeHulkBreach.Blocks(prop.Definition,Vec(at),Vec(waypoint),m.Collider.radius,m.Collider.height,out float distance,out var hit)||distance>=nearest)continue;
                chosen=prop;nearest=distance;point=Vec(hit);
            }
            if(chosen==null){a.WallProgress.Step(Vec(at),Time,false);return false;}
            if(!a.HasBreachShortcut&&!a.WallProgress.Step(Vec(at),Time,true))return false;
            foreach(var b in wardBreaches)if(b.Dynamic&&b.block==chosen.Definition.id&&Time<NativeBreach.SolidAt(b.restoreAt))
            {a.WallBreach=b;return Time<b.breakAt;}
            // Bounded record count. Expired records are reusable; active entry breaches are never evicted.
            for(int i=wardBreaches.Count-1;i>=0;i--)if(wardBreaches[i].Dynamic&&Time>=NativeBreach.SolidAt(wardBreaches[i].restoreAt))wardBreaches.RemoveAt(i);
            int reserved=0;foreach(var e in StageContent.entries)if(e.kind=="breach")reserved++;
            if(wardBreaches.Count>=64-reserved)return false;
            a.WallBreach=NativeHulkBreach.Begin(chosen.Definition,m.Id,Vec(point),Vec(delta),Time);
            wardBreaches.Add(a.WallBreach);a.HasBreachShortcut=false;a.WallProgress.tracking=false;
            m.Yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;return true;
        }
    }
}
