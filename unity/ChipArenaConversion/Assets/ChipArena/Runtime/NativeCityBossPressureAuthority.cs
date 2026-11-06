using System;
using System.Collections.Generic;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly NativeCityBossPressure cityBossPressure=new NativeCityBossPressure();
        private readonly List<NativeZoneBossHazardFrame> cityPressureHazards=new List<NativeZoneBossHazardFrame>(16);
        private readonly HashSet<int> cityPressureHit=new HashSet<int>();
        private void ResetCityBossPressure(){cityBossPressure.Reset();cityPressureHazards.Clear();cityPressureHit.Clear();}
        private Vector3 CityBossFeet(NativePilot p)
        {
            var feet=p.Body.position-Vector3.up*(p.Collider.height*.5f);
            // Resolve the pilot's own supporting floor, never the boss's floor. Retain the airborne position if no floor is near.
            if(PhysicsWorld.Raycast(feet+Vector3.up*.2f,Vector3.down,out var hit,3,WorldMask,QueryTriggerInteraction.Ignore)&&hit.normal.y>.65f)feet=hit.point;
            return feet;
        }
        private void StepCityBossPressure()
        {
            if(zoneBossActor==null||zoneBossActor.Hp<=0){ResetCityBossPressure();return;}
            foreach(var p in Pilots)
            {
                if(!ZonePilot(p)){cityBossPressure.Remove(p.Seat.id);continue;}
                var feet=cityBossPressure.Due(p.Seat.id,Time)?Vec(CityBossFeet(p)):default;
                if(cityBossPressure.Schedule(ZoneBoss.archetype,p.Seat.id,Time,feet,out var h))cityPressureHazards.Add(h);
            }
            for(int i=cityPressureHazards.Count-1;i>=0;i--)
            {
                var h=cityPressureHazards[i];var target=Pilots.Find(p=>p.Seat.id==h.targetSeat);
                if(target==null||!ZonePilot(target)||Time>h.until){cityPressureHazards.RemoveAt(i);cityPressureHit.Remove(h.id);continue;}
                if(Time<h.hitAt||!cityPressureHit.Add(h.id))continue;
                // One impact, even during a long frame, immunity or a dodge. A missed impact never follows its target.
                var feet=target.Body.position-Vector3.up*(target.Collider.height*.5f);
                if(!NativeCityBossPressure.Contact(h,target.Seat.id,Vec(feet),target.Collider.radius)||Invulnerable(target))continue;
                Damage(target,null,NativeCityBossPressure.Damage,Vector3.zero,0,false,primary:false,rollAvoidable:true,
                    causeKind:ZoneBoss.archetype,attackerMinion:zoneBossActor);
            }
        }
    }
}
