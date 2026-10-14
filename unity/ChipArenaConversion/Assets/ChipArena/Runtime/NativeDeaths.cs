using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeDeath
    {public NativeDeath ViewCopy(){var d=(NativeDeath)MemberwiseClone();d.actor=actor?.ViewCopy();return d;}public bool hasActor,hasWound;public int id,owner,seed,lost,projectile;public float at,yaw,force,landAt;public string style,model,damageKind,hazardId;public Vector3 position,velocity,direction;public bool melee,kept,kneel;public Actor actor;public NativeWound wound;}
    public sealed partial class NativePilot
    {public readonly List<NativeWound> Wounds=new List<NativeWound>();public NativeDeath Death;public NativePilot LastAttacker;public float LastAttackerAt;public float RevivedAt=-100;}
    public sealed partial class NativeArena
    {
        public readonly List<NativeDeath> MinionDeaths=new List<NativeDeath>();
        private void Wound(NativePilot p,NativePilot source,float amount,bool lethal,bool head,bool melee,bool direct,Vector3 dir,NativeBodyImpact impact,Vector3? point)
        {
            if(impact==null&&source!=null)impact=melee?NativeBodyDamage.Melee(source.Seat.spec,(float)source.Stats["attackRate"]):direct?NativeBodyDamage.Projectile(source.Seat.spec,(float)source.Weapon.speed,.045f):null;
            if(impact==null)return;Vector3 hit=point??(p.Body.position+Vector3.up*(head?.6f:0));Vector3 local=Quaternion.Euler(0,-p.Yaw,0)*(hit-p.Body.position);var wound=NativeBodyDamage.Resolve(impact,NativeBodyDamage.Part(new V3(-local.x,local.y,local.z),0,head),amount,lethal,head);if(wound==null)return;
            wound.id=nextEvent++;wound.at=Time;if(head)local.y=Mathf.Clamp(local.y,.48f,.8f);wound.point=new V3(Mathf.Clamp(-local.x,-.5f,.5f),Mathf.Clamp(local.y,-.9f,.9f),Mathf.Clamp(local.z,-.4f,.4f));var localDirection=Quaternion.Euler(0,-p.Yaw,0)*dir.normalized;wound.direction=new V3(-localDirection.x,localDirection.y,localDirection.z);p.Wounds.Add(wound);while(p.Wounds.Count>8)p.Wounds.RemoveAt(0);
        }
        public double DeathRecordMs {get;private set;}
        private void RecordDeath(NativePilot p,NativePilot source,Vector3 direction,float force,bool melee,bool sirenCrush,string damageKind=null,bool stage=false,string hazardId=null)
        {
            long at=System.Diagnostics.Stopwatch.GetTimestamp();
            try{RecordDeathCore(p,source,direction,force,melee,sirenCrush,damageKind,stage,hazardId);}
            finally{DeathRecordMs+=(System.Diagnostics.Stopwatch.GetTimestamp()-at)*1000d/System.Diagnostics.Stopwatch.Frequency;}
        }
        private void RecordDeathCore(NativePilot p,NativePilot source,Vector3 direction,float force,bool melee,bool sirenCrush,string damageKind,bool stage,string hazardId)
        {
            if(damageKind!="collapse"&&source==null)source=StageCredit(p);
            // A hold interrupted by a bystander is not a Siren crush; only the timed crush hit sets this flag.
            string style=NativeSirenRules.DeathStyle(p.Seat.spec,sirenCrush,source?.Seat.killEffect);
            p.Pose.Wounds=p.Wounds.ToArray();p.Death=new NativeDeath{damageKind=damageKind,hazardId=hazardId,id=p.Seat.id,owner=source?.Seat.id??0,at=Time,seed=nextEvent++,yaw=sirenCrush&&source!=null?NativeSirenRules.CrushDeathYaw(source.Yaw):p.Yaw,force=force,style=style,model=p.Seat.spec=="siren"?"siren":"pilot",position=p.Body.position,velocity=p.Body.isKinematic?Vector3.zero:p.Body.linearVelocity+p.Knockback,direction=direction,melee=melee,kept=Pve!=null&&!p.Seat.AuthorityBoss,actor=p.Pose.ViewCopy(),wound=stage?null:p.Wounds.LastOrDefault()};
            var w=p.Death.wound;p.Death.kneel=w!=null&&w.part==5&&w.trauma=="bullet"&&w.speed>=110&&(w.kind=="cavity"||w.kind=="sever")&&p.Grounded&&p.Down==null&&p.Juggle==null&&p.Roll==null&&p.Death.velocity.magnitude<8;
        }
        private void RecordMinionDeath(NativeMinion m)
        {
            if(m.Hp>0||m.Kind=="slender")return;MinionDeaths.RemoveAll(d=>Time-d.at>5);while(MinionDeaths.Count>=256)MinionDeaths.RemoveAt(0);
            MinionDeaths.Add(new NativeDeath{id=m.Id,seed=nextEvent++,at=Time,position=m.Body.position,yaw=m.Yaw,velocity=m.Body.isKinematic?Vector3.zero:m.Body.linearVelocity,style="impact-ragdoll",lost=m.Pve?.Lost??0,model=m.Pve==null?"summon-"+m.Kind:"horde-"+(m.Id%6),actor=new Actor{Id=m.Id,SourceSpec="pistol",BodyColor="#e5e7eb",AccentColor="#70e1d0",Yaw=m.Yaw,RigScale=m.Kind=="toy"?.65f:1}});
        }
    }
}
