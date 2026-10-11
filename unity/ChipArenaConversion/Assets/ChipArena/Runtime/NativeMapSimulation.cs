using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private MapBounds mapBounds;public MapBounds Bounds=>mapBounds??(mapBounds=NativeMapRules.Bounds(Content.Map(MapId)));
        public NativeStructure Structures{get;private set;}
        private NativeArenaNavigation arenaNavigation;private int navigationTick=-1;
        private readonly Dictionary<string,NativePropBody> structuralProps=new Dictionary<string,NativePropBody>(StringComparer.Ordinal);
        private readonly HashSet<string> structurePushes=new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string,int> mapHazardHits=new Dictionary<string,int>(StringComparer.Ordinal);
        private int StructureTick=>(int)Math.Round(Match.Time*60);
        private bool MapCeiling(V3 position,V3 delta,float radius,out float fraction,out V3 center)=>NativeSpecialRules.Ceiling(position,delta,radius,out fraction,out center,Bounds);
        private bool ShotOutside(Vector3 p)=>p.y<Bounds.cleanupY||Structures!=null&&(!Bounds.Contains(Vec(p))||p.y>Bounds.ceiling+1);
        private Vector3 SpawnPosition(NativeSeat seat,int index)
        {int teamIndex=Match.Seats.Take(index).Count(s=>s.team==seat.team);return Vec(NativeMapRules.Spawn(Content,Content.Map(MapId),index,seat.team,teamIndex,Match.Teams));}
        private void ResetStructures(ContentMap map)
        {
            mapBounds=NativeMapRules.Bounds(map);Structures=NativeMapRules.Enabled(map)?new NativeStructure(Content,map,unchecked((uint)Generation)):null;
            structuralProps.Clear();structurePushes.Clear();mapHazardHits.Clear();arenaNavigation=null;navigationTick=-1;
            if(Structures==null)return;
            foreach(var p in Props)if(!string.IsNullOrEmpty(p.Definition.group))structuralProps.Add(p.Definition.id,p);
            arenaNavigation=new NativeArenaNavigation(Content.Map(MapId));SyncStructures(StructureTick);
        }
        private bool DamageStructure(NativePropBody p,float amount,Vector3 impulse,Vector3 point,NativePilot source,string cause,Vector3? normal)
        {
            if(Structures==null||string.IsNullOrEmpty(p.Definition.group))return false;
            Structures.Damage(p.Definition.id,amount,Vec(point),Vec(normal??(impulse.sqrMagnitude>.0001f?-impulse.normalized:Vector3.up)),Vec(impulse),cause,source?.Seat.id??0,StructureTick);
            var state=Structures.Find(p.Definition.id);p.Health=state.health;if(state.state==StructureState.Destroyed)p.gameObject.SetActive(false);return true;
        }
        // Static objects carry permanent collision. Falling pieces are analytical and never crush a capsule.
        public void SyncStructures(int atTick)
        {
            if(Structures==null)return;bool changed=false;
            foreach(var b in Structures.Blocks)
            {
                if(!structuralProps.TryGetValue(b.id,out var p)||p==null)continue;
                bool active=b.state!=StructureState.Destroyed;if(p.gameObject.activeSelf!=active){p.gameObject.SetActive(active);changed=true;}p.Health=b.health;
                var position=Vec(NativeStructure.Position(b,atTick));var size=Vec(b.size);if(p.transform.position!=position){p.transform.position=position;changed=true;}if(p.transform.localScale!=size){p.transform.localScale=size;changed=true;}
                var collider=p.GetComponent<Collider>();bool solid=b.state==StructureState.Intact||b.state==StructureState.Telegraph||b.state==StructureState.Settled;if(collider.enabled!=solid){collider.enabled=solid;changed=true;}
                p.GetComponent<Renderer>().enabled=b.state!=StructureState.Destroyed;
            }
            if(changed)Physics.SyncTransforms();
        }
        private bool LandingOccupied(V3 position,V3 size)
        {
            bool Overlap(Vector3 p)=>Math.Abs(p.x-position.x)<size.x/2+.48f&&Math.Abs(p.z-position.z)<size.z/2+.48f&&Math.Abs(p.y-position.y)<size.y/2+.9f;
            return Pilots.Any(p=>p.Hp>0&&p.Seat.connected&&Overlap(p.Body.position))||Minions.Any(m=>m.Hp>0&&Overlap(m.Body.position));
        }
        private void StepStructures()
        {
            if(Structures==null||Replica)return;
            int tick=StructureTick;Structures.Step(tick,LandingOccupied);SyncStructures(tick);if(Match.Phase!=NativePhase.Fight)return;
            foreach(var group in Structures.Blocks.Where(b=>b.state==StructureState.Falling).GroupBy(b=>b.group))
            foreach(var p in Pilots.Where(p=>p.Hp>0&&p.Seat.connected))
            {
                var at=p.Body.position;if(NativeMapRules.Protected(Content,Content.Map(MapId),Vec(at)))continue;
                var near=group.FirstOrDefault(b=>tick>=b.endTick-8&&Math.Abs(at.x-b.rubblePosition.x)<b.rubbleSize.x/2+.8f&&Math.Abs(at.z-b.rubblePosition.z)<b.rubbleSize.z/2+.8f&&Math.Abs(at.y-b.rubblePosition.y)<b.rubbleSize.y/2+1.2f);
                if(near==null||!structurePushes.Add(group.Key+":"+near.seed+":"+p.Seat.id))continue;
                Vector3 delta=at-Vec(near.rubblePosition);delta.y=0;if(delta.sqrMagnitude<.01f)delta=Vector3.right*(p.Seat.id%2==0?1:-1);p.Knockback+=delta.normalized*1.5f;
            }
            foreach(var h in Content.Map(MapId).gameplay.hazards??Array.Empty<MapHazard>())
            {
                if(NativeStructure.HazardPhase(h,tick)!=2)continue;
                foreach(var p in Pilots.Where(p=>p.Hp>0&&p.Seat.connected))
                {
                    var q=p.Body.position;string key=h.id+":"+p.Seat.id;
                    if(Math.Abs(q.x-h.position.x)>h.size.x/2+.42f||Math.Abs(q.y-h.position.y)>h.size.y/2+.85f||Math.Abs(q.z-h.position.z)>h.size.z/2+.42f||NativeMapRules.Protected(Content,Content.Map(MapId),Vec(q))||mapHazardHits.TryGetValue(key,out int next)&&tick<next)continue;
                    mapHazardHits[key]=tick+(int)Math.Ceiling(h.interval*60);Damage(p,null,h.damage*h.interval,Vector3.zero,0,false,direct:false,primary:false,stage:true,damageKind:h.kind??"hazard");
                }
            }
        }
        private void ClampMapPilots()
        {
            if(Structures==null||Replica)return;
            foreach(var p in Pilots.Where(p=>p.Hp>0&&!p.Body.isKinematic))
            {var old=p.Body.position;var next=Vec(Bounds.Clamp(Vec(old),.43f));if(next==old)continue;p.Body.position=next;var v=p.Body.linearVelocity;if(next.x!=old.x){v.x=0;p.Knockback.x=0;p.MoveVelocity.x=0;}if(next.z!=old.z){v.z=0;p.Knockback.z=0;p.MoveVelocity.z=0;}p.Body.linearVelocity=v;}
        }
        private bool EscapeCollapse(Vector3 position,out Vector3 direction)
        {
            direction=Vector3.zero;
            foreach(var group in Structures.Blocks.Where(b=>b.state==StructureState.Telegraph||b.state==StructureState.Falling).GroupBy(b=>b.group))
            {
                float minX=group.Min(b=>b.position.x-b.size.x/2)-.7f,maxX=group.Max(b=>b.position.x+b.size.x/2)+.7f;
                float minZ=group.Min(b=>b.position.z-b.size.z/2)-.7f,maxZ=group.Max(b=>b.position.z+b.size.z/2)+.7f;
                if(position.x<minX||position.x>maxX||position.z<minZ||position.z>maxZ)continue;
                var exits=new[]{new Vector3(minX-.1f,position.y,position.z),new Vector3(maxX+.1f,position.y,position.z),new Vector3(position.x,position.y,minZ-.1f),new Vector3(position.x,position.y,maxZ+.1f)};
                foreach(var exit in exits.OrderBy(e=>(e-position).sqrMagnitude))if(Bounds.Contains(Vec(exit),.5f)&&!SceneLinecast(position,exit,WorldMask)){direction=(exit-position).normalized;return true;}
                // Collision may fence the nearest edge; let the rebuilt route choose another way out.
            }
            return false;
        }
        private Vector3 NavigateArena(NativePilot p,Vector3 target,out bool jump)
        {
            jump=false;if(arenaNavigation==null)return Vector3.zero;
            if(navigationTick!=StructureTick){navigationTick=StructureTick;arenaNavigation.Update(Structures);}
            var result=arenaNavigation.Direction(Vec(p.Body.position),Vec(target),out jump);
            return Vec(result);
        }
    }
}
