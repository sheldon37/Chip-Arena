using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using NVector=System.Numerics.Vector3;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {
        public int CyberChainNext,CyberStormMask,CyberUplinkResult;public bool CyberCutDone=true,CyberMeteorDone=true,CyberHoleDone=true;
        public float CyberHoleTick,CyberOrbitalTick;public Vector3 CyberYankFrom;
        public readonly bool[] CyberCloneDone={true,true,true};
        public readonly List<int> CyberCutAdds=new List<int>();
    }
    public sealed partial class NativeArena
    {
        private struct CyberTarget
        {
            public NativePilot Pilot;public NativeMinion Add;
            public int Id=>Pilot?.Seat.id??Add.Id;public Vector3 Point=>Pilot?.Body.position??Add.Body.position;
            public Collider Collider=>Pilot!=null?(Collider)Pilot.Collider:Add.Collider;
            public bool Alive=>(Pilot?.Hp??Add?.Hp??0)>0;
        }
        private IEnumerable<CyberTarget> CyberTargets(NativePilot p)
        {foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q))yield return new CyberTarget{Pilot=q};foreach(var q in Minions)if(q.Hp>0&&CanHit(p,q))yield return new CyberTarget{Add=q};}
        private float CyberTargetHit(NativePilot p,CyberTarget q,float amount,string detail)
        {return q.Pilot!=null?CyberHit(p,q.Pilot,amount,detail):HitMinion(q.Add,p,amount*CyberScale(p),q.Point-p.Body.position,0,q.Point,detail);}
        private void CyberSlow(CyberTarget q,float amount,float until)
        {if(q.Pilot!=null){q.Pilot.SlowFactor=Math.Min(q.Pilot.SlowFactor,1-amount);q.Pilot.SlowEnd=Math.Max(q.Pilot.SlowEnd,until);}else{q.Add.SlowFactor=Math.Min(q.Add.SlowFactor,1-amount);q.Add.SlowUntil=Math.Max(q.Add.SlowUntil,until);if(q.Add.Pve!=null){q.Add.Pve.SlowFactor=q.Add.SlowFactor;q.Add.Pve.SlowUntil=q.Add.SlowUntil;}}}
        private void Cyber2Arc(NativePilot p,int hitId,Vector3 from)
        {
            if(!NativeCyberKit2Rules.Overclock(p.Cyber,Time))return;
            var q=CyberTargets(p).Where(x=>x.Id!=hitId&&Vector3.Distance(x.Point,from)<=CyberKit2Spec.ArcRange&&MeleeVisible(from,x.Point,p,x.Collider)).OrderBy(x=>(x.Point-from).sqrMagnitude).FirstOrDefault();
            if(!q.Alive)return;var point=q.Point;CyberTargetHit(p,q,CyberKit2Spec.ArcDamage,"overclock");Emit(EventKind.AbilityHit,p,q.Id,point,CyberKit2Spec.ArcDamage,"overclock",from:from);
        }
        private static NVector CyberN(V3 v)=>new NVector(v.x,v.y,v.z);
        private static Vector3 CyberU(NVector v)=>new Vector3(v.X,v.Y,v.Z);
        private Vector3 CyberFloor(NativePilot p,float range,Vector3? origin=null)
        {
            var start=origin??p.Body.position;var at=start+Vector3.ClampMagnitude(Vec(p.Input.AbilityTarget)-start,range);
            var hit=SweepNative(p.Body.position,at-p.Body.position,.1f,c=>c.GetComponent<NativePilotBody>()==null&&c.GetComponent<NativeMinionBody>()==null);
            if(hit.HasValue)at=hit.Value.Point;
            if(root.scene.GetPhysicsScene().Raycast(at+Vector3.up*.3f,Vector3.down,out var floor,100,WorldMask,QueryTriggerInteraction.Ignore)&&floor.normal.y>.5f)return floor.point;
            return p.Body.position-Vector3.up*.865f;
        }
        private bool Cyber2MotionPending(NativePilot p)
        {
            var s=p.Cyber;if(s.Version!=2||s.Kit2==null||s.CutAt>=s.At)return false;
            return s.Kind=="meteor-byte"&&!p.CyberMeteorDone||s.Kind=="glitch-cut"&&Time<=s.Until+Rules.Dt||s.Kind=="uplink"&&s.Kit2.UplinkArriveAt>0&&Time<=s.Kit2.UplinkArriveAt+Rules.Dt;
        }
        private void Cyber2Input(NativePilot p)
        {
            var s=p.Cyber;if(s.Version!=2){p.CyberCutDone=p.CyberMeteorDone=p.CyberHoleDone=true;p.CyberChainNext=0;p.CyberStormMask=0;for(int n=0;n<3;n++)p.CyberCloneDone[n]=true;}NativeCyberKit2Rules.Ensure(s);var i=p.Input;
            if(i.CancelAbilities){NativeCyberRules.Interrupt(s,Time);return;}
            if(s.Kind=="uplink"&&Time>=s.Kit2.UplinkLatchAt&&s.Kit2.UplinkLatchAt>0&&(i.Jump||i.Attack||i.Shoot)){NativeCyberRules.Interrupt(s,Time);p.Ready=p.AttackEnd=Time;}
            if(!s.Ranged.Ranged&&i.SirenAbility==1&&Time<s.Kit2.ForkUntil){s.Kit2.ForkUntil=Time;return;}if(i.SirenAbility==5){CyberSwitch(p);return;}if(CyberBusy(p))return;
            int slot=NativeCyberKit2Rules.Slot(s,i);s.Kit2R.Ready[4]=p.KernelReady;
            if(!NativeCyberKit2Rules.Begin(s,slot,Time,ref p.Mana,(float)p.Stats["attackRate"],(float)(p.Stats["resourceCost"]*p.Stats["abilityCost"]),(float)p.Stats["abilityCooldown"],(float)p.Stats["resourceCost"]))return;
            s.Facing=V3.Forward(i.Yaw);p.CyberAttack=nextAttack++;p.Yaw=i.Yaw;p.AttackAt=p.LastAttack=Time;p.AttackEnd=p.Ready=s.Until;
            if(NativeCyberKit2Rules.Move(s,Time)==0)p.MoveVelocity=Vector3.zero;
            p.CyberStep=slot==2?NativeCyberStep.Begin(s,p.Grounded&&!NativeHazards.InLava(MapId,Vec(p.Body.position)),i.LockTarget,Vec(p.Body.position),(pos,dir,d,n,target)=>CyberStepLimit(p,pos,dir,d,n,target)):default;
            var m=s.Kit2;var r=s.Kit2R;var from=p.Body.position;
            if(slot==0){m.UplinkFrom=Vec(from);m.UplinkAnchor=Vec(from+Vector3.up*CyberKit2Melee.EmitterHeight+Vec(GunRules.Direction(i.Yaw,i.Pitch))*CyberKit2Spec.UplinkRange);m.UplinkOutcome=m.UplinkTarget=0;m.UplinkLatchAt=m.UplinkArriveAt=-100;}
            if(slot==4){m.CutFrom=Vec(from);m.CutTo=Vec(KernelTravel(from,from+Vec(s.Facing)*CyberKit2Spec.CutDistance));m.MarkedCount=0;m.DetonateAt=s.Until+CyberKit2Spec.MarkDelay;p.CyberCutAdds.Clear();p.CyberCutDone=false;}
            if(slot==5){m.MeteorFrom=Vec(from);m.MeteorTarget=Vec(CyberFloor(p,CyberKit2Spec.MeteorRange,Vec(m.MeteorFrom)));m.LandAt=s.Until;p.CyberMeteorDone=false;}
            if(slot==8){r.ChainCount=0;r.ChainFrom=Vec(from+Vector3.up*.3f);p.CyberChainNext=0;}
            if(slot==9){r.OrbFrom=Vec(from+Vector3.up*.3f);r.OrbPoint=r.OrbFrom;r.OrbLand=r.Implode=-100;p.CyberHoleDone=true;}
            if(slot==10){r.StormCentre=Vec(CyberFloor(p,CyberKit2Spec.StormRange));r.StormSeed=random.Next();p.CyberStormMask=0;}
            if(slot==11){r.OrbitalCentre=r.OrbitalPoint=r.OrbitalAim=Vec(CyberFloor(p,CyberKit2Spec.OrbitalRange));r.OrbitalPointAt=Time;p.CyberOrbitalTick=s.Release+CyberKit2Spec.OrbitalTick;p.KernelReady=r.Ready[4];}
            p.QReady=m.Ready[0];p.EReady=m.Ready[1];Emit(EventKind.AbilityCast,p,0,from,0,s.Kind);
        }
        private void Cyber2Release(NativePilot p,int slot)
        {
            var s=p.Cyber;var m=s.Kit2;var r=s.Kit2R;var from=p.Body.position;
            if(slot==0)
            {
                from+=Vector3.up*CyberKit2Melee.EmitterHeight;var end=from+Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*CyberKit2Spec.UplinkRange;
                var hit=SweepNative(from,end-from,.08f,c=>c!=p.Collider);m.UplinkAnchor=Vec(hit?.Point??end);var target=hit?.Collider.GetComponent<NativePilotBody>()?.Pilot;var add=hit?.Collider.GetComponent<NativeMinionBody>()?.Minion;
                p.CyberUplinkResult=!hit.HasValue?CyberKit2Melee.Miss:target!=null&&!CanHit(p,target)||add!=null&&!CanHit(p,add)?CyberKit2Melee.Miss:add!=null||target!=null&&!target.Seat.AuthorityBoss&&target.Seat.spec!="siren"?CyberKit2Melee.Yank:CyberKit2Melee.Zip;
                m.UplinkTarget=target?.Seat.id??add?.Id??0;p.CyberYankFrom=target?.Body.position??add?.Body.position??Vector3.zero;
                m.UplinkLatchAt=CyberKit2Melee.LatchAt(s.Release,CyberN(m.UplinkFrom),CyberN(m.UplinkAnchor));
                m.UplinkArriveAt=p.CyberUplinkResult==CyberKit2Melee.Zip?CyberKit2Melee.ZipArrive(m.UplinkLatchAt,CyberN(m.UplinkFrom),CyberN(m.UplinkAnchor)):p.CyberUplinkResult==CyberKit2Melee.Miss?s.Release+CyberKit2Melee.MissSeconds:m.UplinkLatchAt+CyberKit2Spec.YankSeconds;
                s.Until=m.Until[0]=Math.Max(s.Until,m.UplinkArriveAt);p.Ready=p.AttackEnd=s.Until;
            }
            if(slot==1){m.OverclockAt=Time;m.OverclockUntil=Time+CyberKit2Spec.OverclockSeconds;Emit(EventKind.AbilityHit,p,0,from,0,"overclock");}
            if(slot==2)
            {
                int step=s.Combo;CyberArea(p,CyberKit2Spec.ComboDamage[step],s.Kind,q=>NativeCyberRules.Cone(Vec(q-from),s.Facing,CyberKit2Spec.ComboRange,step==2?CyberKit2Spec.SpinArc:step==4?CyberKit2Spec.PalmCone:CyberKit2Spec.ComboArc),true,NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"]));
                foreach(var q in Pilots.Where(q=>q.Hp>0&&CanHit(p,q)&&NativeCyberRules.Cone(Vec(q.Body.position-from),s.Facing,CyberKit2Spec.ComboRange,step==2?CyberKit2Spec.SpinArc:CyberKit2Spec.ComboArc)&&MeleeVisible(from,q.Body.position,p,q.Collider)))
                {
                    if(step==3&&LaunchMelee(q,p,CyberKit2Spec.RiseLaunch))q.Down.JuggleUntil=Time+CyberKit2Spec.RiseJuggle;
                    if(step==4&&!SirenArmor(q))q.Knockback+=Vec(s.Facing)*CyberKit2Spec.PalmShove;

                }
                if(Time<m.ForkUntil)for(int n=0;n<3;n++){var point=Vec(m.ClonePos[n])+Vector3.up*.865f;var q=CyberNearest(p,point,CyberKit2Spec.CloneReach);if(!q.Alive)continue;m.CloneStrikeAt[n]=Time;m.CloneTarget[n]=q.Id;CyberTargetHit(p,q,CyberKit2Spec.ComboDamage[step]*CyberKit2Spec.CloneShare*(float)p.Stats["damage"]/Math.Max(.001f,CyberScale(p)),"fork-strike");}
            }
            if(slot==3){m.ForkAt=Time;m.ForkUntil=Time+CyberKit2Spec.ForkSeconds;for(int n=0;n<3;n++){m.ClonePos[n]=Vec(CyberU(CyberKit2Melee.Formation(n,CyberN(Vec(from-Vector3.up*.865f)),p.Yaw)));m.CloneBurstAt[n]=-100;p.CyberCloneDone[n]=false;}}
            if(slot==7)Cyber2Plasma(p);
            if(slot==8)
            {
                var aim=Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch));var target=CyberTargets(p).Where(q=>Vector3.Distance(q.Point,from)<=CyberKit2Spec.ChainRange&&Vector3.Angle(aim,q.Point-from)<=CyberKit2Spec.ChainCone&&MeleeVisible(from,q.Point,p,q.Collider)).OrderBy(q=>(q.Point-from).sqrMagnitude).FirstOrDefault();
                r.ChainFrom=Vec(from+Vector3.up*.3f);
                while(target.Alive&&r.ChainCount<6){int n=r.ChainCount++;r.ChainIds[n]=target.Id;r.ChainPoints[n]=Vec(target.Point);var at=target.Point;target=CyberTargets(p).Where(q=>!r.ChainIds.Take(r.ChainCount).Contains(q.Id)&&Vector3.Distance(q.Point,at)<=CyberKit2Spec.HopRange&&MeleeVisible(at,q.Point,p,q.Collider)).OrderBy(q=>(q.Point-at).sqrMagnitude).FirstOrDefault();}
            }
            if(slot==9)
            {
                from+=Vector3.up*.3f;var end=from+Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*CyberKit2Spec.OrbRange;var hit=SweepNative(from,end-from,.18f,c=>c!=p.Collider);var point=hit?.Point??end;
                if(hit.HasValue&&hit.Value.Normal.y>.5f)point+=Vector3.up*CyberKit2Ranged.HoleLift;
                r.OrbFrom=Vec(from);r.OrbPoint=Vec(point);r.OrbLand=Time+Vector3.Distance(from,point)/CyberKit2Spec.OrbSpeed;r.Implode=r.OrbLand+CyberKit2Spec.HoleSeconds;p.CyberHoleTick=r.OrbLand+CyberKit2Spec.HoleTick;p.CyberHoleDone=false;
            }
        }
        private CyberTarget CyberNearest(NativePilot p,Vector3 at,float range)=>CyberTargets(p).Where(q=>Vector3.Distance(q.Point,at)<=range&&MeleeVisible(at,q.Point,p,q.Collider)).OrderBy(q=>(q.Point-at).sqrMagnitude).FirstOrDefault();
        private void Cyber2Plasma(NativePilot p)
        {
            var r=p.Cyber.Kit2R;bool overflow=CyberKit2Spec.Overflow(r.PlasmaShot),split=NativeCyberKit2Rules.Overclock(p.Cyber,Time);int hand=CyberKit2Spec.PlasmaHand(r.PlasmaShot);
            for(int n=0;n<(split?3:1)&&ProjectileCount<100;n++)
            {
                var dir=Vec(GunRules.Direction(p.Input.Yaw+(n==1?-CyberKit2Spec.SplitAngle:n==2?CyberKit2Spec.SplitAngle:0),p.Input.Pitch));var from=p.Body.position+Vector3.up*.3f;var start=from+Vector3.Cross(Vector3.up,dir).normalized*(hand==0?.18f:hand==1?-.18f:0);
                float radius=overflow?CyberKit2Spec.OverflowRadius:CyberKit2Spec.PlasmaRadius;if(SweepNative(from,start-from,radius,c=>c!=p.Collider).HasValue)start=from;
                string detail=overflow?"plasma-overflow":"plasma-byte";float share=n==0?1:CyberKit2Spec.SplitShare;
                var b=new NativeBullet{CyberPacket=true,Detail=detail,Draw=hand,Id=nextBullet++,Attack=p.CyberAttack,Owner=p.Seat.id,Original=p.Seat.id,Spec="cyberlord",Born=Time,Position=start,Velocity=dir*CyberKit2Spec.PlasmaSpeed,BaseVelocity=dir*CyberKit2Spec.PlasmaSpeed,FixedSpeed=CyberKit2Spec.PlasmaSpeed,Damage=(overflow?CyberKit2Spec.OverflowDamage:CyberKit2Spec.PlasmaDamage)*(float)p.Stats["damage"]*share,Radius=radius,Life=CyberKit2Spec.PlasmaRange/CyberKit2Spec.PlasmaSpeed,Travel=CyberKit2Spec.PlasmaRange,Direct=false,WeaponFx=true};Bullets.Add(b);Emit(EventKind.Shot,p,0,start,b.Damage,detail);
            }
        }
        private void Cyber2World(NativePilot p)
        {
            var s=p.Cyber;var m=s.Kit2;var r=s.Kit2R;int slot=NativeCyberKit2Rules.Current(s);
            if(Incapacitated(p)||Time<p.BlockEnd||Time<p.RollEnd||KernelBusy(p))NativeCyberRules.Interrupt(s,Time);
            if(!s.Resolved&&Time>=s.Release){s.Resolved=true;Cyber2Release(p,slot);}
            bool live=s.CutAt<s.At;
            if(NativeCyberKit2Rules.Overclock(s,Time))
            {
                r.Ready[4]=p.KernelReady;NativeCyberKit2Rules.Recover(s,Time,Rules.Dt);p.KernelReady=r.Ready[4];m.Ready[6]=p.KernelReady;p.QReady=m.Ready[0];p.EReady=m.Ready[1];
            }
            if(slot==0&&live&&m.UplinkLatchAt>0&&Time>=m.UplinkLatchAt&&Time<=m.UplinkArriveAt+Rules.Dt)
            {
                if(m.UplinkOutcome==0){m.UplinkOutcome=p.CyberUplinkResult;m.UplinkFrom=Vec(p.Body.position);if(m.UplinkOutcome==1){m.UplinkArriveAt=CyberKit2Melee.ZipArrive(m.UplinkLatchAt,CyberN(m.UplinkFrom),CyberN(m.UplinkAnchor));s.Until=m.Until[0]=m.UplinkArriveAt;p.Ready=p.AttackEnd=s.Until;}if(m.UplinkOutcome!=3)Emit(EventKind.AbilityHit,p,m.UplinkTarget,Vec(m.UplinkAnchor),0,m.UplinkOutcome==2?"uplink-yank":"uplink-zip",from:Vec(m.UplinkFrom));}
                if(m.UplinkOutcome==1){p.Body.position=KernelTravel(p.Body.position,CyberU(CyberKit2Melee.ZipBody(CyberN(m.UplinkFrom),CyberN(m.UplinkAnchor),m.UplinkLatchAt,m.UplinkArriveAt,Time)));p.MoveVelocity=(Vec(m.UplinkAnchor)-Vec(m.UplinkFrom)).normalized*CyberKit2Spec.ZipSpeed;p.Body.linearVelocity=Time<m.UplinkArriveAt?Vector3.zero:p.MoveVelocity;}
                if(m.UplinkOutcome==2){var q=CyberTargets(p).FirstOrDefault(x=>x.Id==m.UplinkTarget);if(q.Alive&&(q.Pilot==null||!SirenArmor(q.Pilot))){var to=KernelTravel(q.Point,CyberU(CyberKit2Melee.YankBody(CyberN(Vec(p.CyberYankFrom)),CyberN(m.UplinkFrom),CyberN(s.Facing),m.UplinkLatchAt,Time)));if(q.Pilot!=null){q.Pilot.Body.position=to;StunMelee(q.Pilot,Math.Max(0,m.UplinkLatchAt+CyberKit2Spec.YankStun-Time));}else{q.Add.Body.position=to;q.Add.NextAttack=Math.Max(q.Add.NextAttack,m.UplinkLatchAt+CyberKit2Spec.YankStun);if(q.Add.Pve!=null){q.Add.Pve.StaggerAt=Math.Max(q.Add.Pve.StaggerAt,m.UplinkLatchAt+CyberKit2Spec.YankStun);q.Add.Pve.Struck=true;}}}}
            }
            if(slot==4&&live&&Time>=s.Release&&Time<=s.Until+Rules.Dt)
            {
                var old=p.Body.position;var next=KernelTravel(old,CyberU(CyberKit2Melee.CutBody(CyberN(m.CutFrom),CyberN(m.CutTo),s.Release,s.Until,Time)));p.Body.position=next;p.MoveVelocity=Vector3.zero;p.Body.linearVelocity=Vector3.zero;
                foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&m.MarkedCount<8&&!m.Marked.Take(m.MarkedCount).Contains(q.Seat.id)&&NativeCyberRules.SegmentDistance(Vec(q.Body.position),Vec(old),Vec(next))<=CyberKit2Spec.CutWidth){m.Marked[m.MarkedCount++]=q.Seat.id;Emit(EventKind.AbilityHit,p,q.Seat.id,q.Body.position,0,"glitch-mark");}
                foreach(var q in Minions)if(q.Hp>0&&CanHit(p,q)&&!p.CyberCutAdds.Contains(q.Id)&&NativeCyberRules.SegmentDistance(Vec(q.Body.position),Vec(old),Vec(next))<=CyberKit2Spec.CutWidth){p.CyberCutAdds.Add(q.Id);Emit(EventKind.AbilityHit,p,q.Id,q.Body.position,0,"glitch-mark");}
            }
            if(!p.CyberCutDone&&Time>=m.DetonateAt){p.CyberCutDone=true;foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&m.Marked.Take(m.MarkedCount).Contains(q.Seat.id))CyberHit(p,q,CyberKit2Spec.CutDamage,"glitch-detonate");foreach(var q in Minions)if(q.Hp>0&&p.CyberCutAdds.Contains(q.Id))HitMinion(q,p,CyberKit2Spec.CutDamage*CyberScale(p),Vector3.zero,0,q.Body.position,"glitch-detonate");}
            if(slot==5&&live&&!p.CyberMeteorDone)
            {
                if(Time<CyberKit2Melee.DiveAt(s.At))m.MeteorTarget=Vec(CyberFloor(p,CyberKit2Spec.MeteorRange,Vec(m.MeteorFrom)));
                p.Body.position=KernelTravel(p.Body.position,CyberU(CyberKit2Melee.MeteorBody(CyberN(m.MeteorFrom),CyberN(m.MeteorTarget),s.At,Time)));p.MoveVelocity=Vector3.zero;p.Body.linearVelocity=Vector3.zero;
                if(Time>=m.LandAt){p.CyberMeteorDone=true;Cyber2Area(p,Vec(m.MeteorTarget),CyberKit2Spec.MeteorRadius,CyberKit2Spec.MeteorDamage,"meteor-impact");foreach(var q in Pilots)if(q.Hp>0&&CanHit(p,q)&&Vector3.Distance(q.Body.position,Vec(m.MeteorTarget)+Vector3.up*.865f)<=CyberKit2Spec.MeteorRadius&&LaunchMelee(q,p,CyberKit2Spec.MeteorLaunch))q.Down.JuggleUntil=Time+CyberKit2Spec.MeteorJuggle;}
            }
            for(int n=0;n<3;n++)if(!p.CyberCloneDone[n])
            {
                if(Time<m.ForkUntil){m.ClonePos[n]=Vec(CyberU(CyberKit2Melee.Formation(n,CyberN(Vec(p.Body.position-Vector3.up*.865f)),p.Yaw)));m.CloneYaw[n]=p.Yaw;}
                else if(m.CloneBurstAt[n]<0){var q=CyberNearest(p,Vec(m.ClonePos[n])+Vector3.up*.865f,CyberKit2Spec.DetonateRange);m.CloneBurst[n]=!q.Alive?m.ClonePos[n]:Vec(q.Point-Vector3.up*.865f);m.CloneBurstAt[n]=Time+CyberKit2Spec.DetonateDash;}
                else if(Time>=m.CloneBurstAt[n]){p.CyberCloneDone[n]=true;Cyber2Area(p,Vec(m.CloneBurst[n]),CyberKit2Spec.DetonateRadius,CyberKit2Spec.DetonateDamage,"fork-burst");}
            }
            while(p.CyberChainNext<r.ChainCount&&Time>=r.Release[1]+p.CyberChainNext*CyberKit2Spec.HopDelay)
            {
                int n=p.CyberChainNext++;var q=CyberTargets(p).FirstOrDefault(x=>x.Id==r.ChainIds[n]);if(!q.Alive)continue;r.ChainPoints[n]=Vec(q.Point);CyberTargetHit(p,q,CyberKit2Spec.ChainHopDamage(n),"chain-hop");CyberSlow(q,CyberKit2Spec.ChainSlow,Time+CyberKit2Spec.ChainSlowSeconds);
            }
            if(!p.CyberHoleDone&&Time>=r.OrbLand)
            {
                var at=Vec(r.OrbPoint);if(Time<r.Implode)foreach(var q in CyberTargets(p))if((q.Pilot==null||!SirenArmor(q.Pilot))&&MeleeVisible(at,q.Point,p,q.Collider)){var d=at-q.Point;float pull=CyberKit2Spec.Pull(d.magnitude);if(pull>0){if(q.Pilot!=null)q.Pilot.Body.position=KernelTravel(q.Point,q.Point+d.normalized*Math.Min(d.magnitude,pull*Rules.Dt));else q.Add.Body.position=KernelTravel(q.Point,q.Point+d.normalized*Math.Min(d.magnitude,pull*Rules.Dt));}}
                while(p.CyberHoleTick<=Time&&p.CyberHoleTick<=r.Implode){Cyber2Area(p,at,CyberKit2Spec.PullRadius,CyberKit2Spec.TickDamage,"singularity-tick",centre:true);p.CyberHoleTick+=CyberKit2Spec.HoleTick;}
                if(Time>=r.Implode){p.CyberHoleDone=true;Cyber2Area(p,at,CyberKit2Spec.ImplodeRadius,CyberKit2Spec.ImplodeDamage,"singularity-implode",CyberKit2Spec.ImplodeShove,true);}
            }
            if(r.At[3]>-90&&r.CutAt[3]<r.At[3])for(int n=0;n<CyberKit2Spec.Drops;n++){CyberKit2Spec.StormDrop(r.StormSeed,n,out float t,out float x,out float z);if((p.CyberStormMask&(1<<n))==0&&Time>=r.Release[3]+t){p.CyberStormMask|=1<<n;Cyber2Area(p,Vec(r.StormCentre)+new Vector3(x,0,z),CyberKit2Spec.DropRadius,CyberKit2Spec.DropDamage,"storm-drop");}}
            if(r.At[4]>-90&&r.CutAt[4]<r.At[4]&&Time>=r.Release[4]&&Time<=r.Until[4]+Rules.Dt)
            {
                r.OrbitalAim=Vec(CyberFloor(p,CyberKit2Spec.OrbitalRange));float x=r.OrbitalPoint.x,z=r.OrbitalPoint.z;CyberKit2Spec.OrbitalStep(ref x,ref z,r.OrbitalAim.x,r.OrbitalAim.z,Rules.Dt);r.OrbitalPoint=new V3(x,r.OrbitalAim.y,z);r.OrbitalPointAt=Time;
                while(p.CyberOrbitalTick<=Time&&p.CyberOrbitalTick<=r.Until[4]){Cyber2Area(p,Vec(r.OrbitalPoint),CyberKit2Spec.OrbitalRadius,CyberKit2Spec.OrbitalDps*CyberKit2Spec.OrbitalTick,"orbital-tick");p.CyberOrbitalTick+=CyberKit2Spec.OrbitalTick;}
                foreach(var q in CyberTargets(p))if(Vector3.Distance(q.Point,Vec(r.OrbitalPoint)+Vector3.up*.865f)<=CyberKit2Spec.OrbitalRadius)CyberSlow(q,CyberKit2Spec.OrbitalSlow,Time+2*Rules.Dt);
            }
        }
        private void Cyber2Area(NativePilot p,Vector3 at,float radius,float damage,string detail,float shove=0,bool centre=false)
        {var c=centre?at:at+Vector3.up*.865f;CyberRangedArea(p,at,damage,detail,q=>Vector3.Distance(q,c)<=radius,shove);Emit(EventKind.Impact,p,0,at,damage,detail);}
        private void Cyber2Splash(NativePilot p,NativeBullet b,Vector3 at)
        {
            bool big=b.Detail=="plasma-overflow";float baseDamage=big?CyberKit2Spec.OverflowDamage:CyberKit2Spec.PlasmaDamage;
            float amount=(big?CyberKit2Spec.OverflowSplash:CyberKit2Spec.PlasmaSplash)*b.Damage/Math.Max(.001f,baseDamage*CyberScale(p));
            Cyber2Area(p,at,big?CyberKit2Spec.OverflowSplashRadius:CyberKit2Spec.PlasmaSplashRadius,amount,big?"plasma-overflow-hit":"plasma-hit",centre:true);
        }
    }
}
