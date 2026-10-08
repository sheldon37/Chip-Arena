using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public string PveNotice(int id)=>Pve!=null&&Pve.Rows.TryGetValue(id,out var row)&&Time<row.NoticeUntil?row.Notice:null;
        private float nextMedicPulse;
        private void MedicPulse()
        {
            if(Time<nextMedicPulse)return;nextMedicPulse=Time+.5f;
            foreach(var p in Pilots.Where(p=>p!=Pve.Boss&&p.Hp>0))
            {int medics=Pve.Recruits.Count(r=>r.Role=="medic"&&r.Pilot.Hp>0&&Vector3.Distance(r.Pilot.Body.position,p.Body.position)<=9&&MeleeVisible(r.Pilot.Body.position,p.Body.position,r.Pilot,p.Collider));Heal(p,NativePveParity.MedicHeal(medics,(float)p.Stats["hp"],Time-p.LastDamage<1));}
        }
        private void RespawnPveParty()
        {
            var occupied=Members().Where(p=>p.Hp>0).Select(p=>p.Body.position).ToList();
            foreach(var p in Members().Where(p=>p.Hp<=0&&p.Seat.connected))
            {var point=Pve.Content.pads.Select(pad=>Vec(pad.Position(.9f))).OrderBy(at=>occupied.Any(q=>Vector3.Distance(q,at)<2)).ThenBy(at=>Minions.Any(m=>m.Pve!=null&&m.Hp>0&&Vector3.Distance(m.Body.position,at)<4)).ThenBy(at=>(at-p.Body.position).sqrMagnitude).First();RevivePve(p,point);occupied.Add(point);}
        }
        private void PlacePveBoss(Vector3 at)
        {
            Pve.Boss.Body.position=at;Pve.Boss.Body.linearVelocity=Pve.Boss.Knockback=Pve.Boss.MoveVelocity=Vector3.zero;
            foreach(var p in Members().Where(p=>p.Hp>0)){Vector3 d=p.Body.position-at;d.y=0;if(d.magnitude>=4)continue;if(d.sqrMagnitude<.001f)d=Vector3.forward;p.Body.position=at+d.normalized*4;p.Body.linearVelocity=p.Knockback=p.MoveVelocity=Vector3.zero;}
        }
        private void KillPveHorde()
        {foreach(var m in Minions.Where(m=>m.Pve!=null).ToArray()){if(Time>=m.Pve.ExposedAt){m.Hp=0;Emit(EventKind.Death,null,m.Id,m.Body.position,0);}RemoveMinion(m,false);}}
    }
}
