using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot
    {public int CyberAttack;public NativeCyberKit Cyber=new NativeCyberKit();public float CyberTrailTick,CyberWallTick,CyberLinkTick;}
    public sealed partial class NativeArena
    {
        private void BotCyber(NativePilot p,ref InputFrame input,Vector3 target,float distance,bool visible)
        {
            if(!CyberAllowed(p)||!visible)return;input.Shoot=distance<=2.2f;input.Attack=input.Shoot;
            if(CyberBusy(p))return;var enemy=Pilots.FirstOrDefault(q=>q.Hp>0&&CanHit(p,q)&&Vector3.Distance(q.Body.position,target)<.1f);
            if(distance<=NativeKernelPanicRules.Range&&p.Mana>=NativeKernelPanicRules.Mana&&Time>=p.KernelReady&&enemy!=null&&Time>=enemy.BlockEnd)input.SirenAbility=4;
            else if(p.Mana>=40&&Time>=p.Cyber.CrashReady&&Pilots.Count(q=>q.Hp>0&&CanHit(p,q)&&Vector3.Distance(q.Body.position,p.Body.position)<=4)>=2)input.SirenAbility=3;
            else if(distance<=6&&p.Mana>=15&&Time>=p.Cyber.PacketReady)input.SirenAbility=1;
            else if(distance>3&&distance<=12&&p.Mana>=20&&Time>=p.Cyber.QReady)input.AbilityQ=true;
            else if(distance<=12&&p.Hp<70&&p.Mana>=25&&Time>=p.Cyber.LinkReady)input.SirenAbility=2;
            if(input.SirenAbility>0||input.AbilityQ)input.Attack=input.Shoot=input.Roll=false;
        }
        private bool CyberAllowed(NativePilot p)=>p.Seat.spec=="cyberlord"&&p.Seat.access.DeveloperWeapon("cyberlord",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        private bool CyberBusy(NativePilot p)=>KernelBusy(p)||Incapacitated(p)||Time<p.RollEnd||Time<p.BlockEnd||Time<p.AttackEnd||Time<p.Ready;
        private bool CyberPay(NativePilot p,float cost)
        {cost*=(float)(p.Stats["resourceCost"]*p.Stats["abilityCost"]);if(p.Mana<cost)return false;p.Mana-=cost;return true;}
        private float CyberScale(NativePilot p)=>NativeAbilityRules.Scale(p.Stats,p.Mana>=.75f*p.Stats["maxMana"]);
        private void CyberBegin(NativePilot p,string kind,float windup,float duration)
        {var s=p.Cyber;p.CyberAttack=nextAttack++;s.Kind=kind;s.At=Time;s.Release=Time+windup;s.Until=Time+duration;s.Resolved=false;s.Facing=V3.Forward(p.Input.Yaw);p.Yaw=p.Input.Yaw;p.AttackAt=p.LastAttack=Time;p.AttackEnd=p.Ready=s.Until;p.MoveVelocity=Vector3.zero;Emit(EventKind.AbilityCast,p,0,p.Body.position,0,kind);}
        private void StepCyberInput(NativePilot p)
        {
            if(!CyberAllowed(p)||CyberBusy(p))return;var i=p.Input;var s=p.Cyber;float cd=(float)p.Stats["abilityCooldown"];
            if(i.AbilityQ&&Time>=s.QReady&&CyberPay(p,20))
            {
                CyberBegin(p,"data-dash",0,.2f);s.QReady=p.QReady=Time+NativeCyberRules.DashCooldown*cd;s.DashAt=Time;s.DashUntil=Time+.2f;s.TrailUntil=s.DashUntil+1.2f;
                s.DashFrom=Vec(p.Body.position);s.DashTo=Vec(KernelTravel(p.Body.position,p.Body.position+Vec(s.Facing)*7));p.CyberTrailTick=s.DashUntil+.3f;
                CyberArea(p,18,"data-dash",q=>NativeCyberRules.SegmentDistance(Vec(q),s.DashFrom,s.DashTo)<=.42f);
                Emit(EventKind.Teleport,p,0,Vec(s.DashTo),0,"data-dash",from:Vec(s.DashFrom));
            }
            else if(i.AbilityE&&Time>=s.EReady&&CyberPay(p,35))
            {CyberBegin(p,"firewall",0,.2f);s.EReady=p.EReady=Time+12*cd;s.WallAt=Time;s.WallUntil=Time+4;s.WallYaw=p.Yaw;s.WallPoint=Vec(p.Body.position-Vector3.up*.865f+Vec(s.Facing)*.8f);p.CyberWallTick=Time+.25f;}
            else if(i.SirenAbility==1&&Time>=s.PacketReady&&CyberPay(p,15))
            {CyberBegin(p,"packet-burst",.08f,.25f);s.PacketReady=Time+4*cd;}
            else if(i.SirenAbility==2&&Time>=s.LinkReady)
            {
                var target=CyberAim(p,12);if(target==null||!CyberPay(p,25))return;
                CyberBegin(p,"siphon-link",0,.2f);s.LinkReady=Time+10*cd;s.LinkAt=Time;s.LinkUntil=Time+2.5f;s.LinkTarget=target.Seat.id;p.CyberLinkTick=Time+.25f;
            }
            else if(i.SirenAbility==3&&Time>=s.CrashReady&&CyberPay(p,40))
            {CyberBegin(p,"system-crash",.5f,.8f);s.CrashReady=Time+14*cd;s.CrashAt=Time;s.CrashUntil=Time+.8f;s.CrashPoint=Vec(p.Body.position);}
            else if(i.Attack||i.Shoot)
            {if(Time>s.LastComboEnd+.6f)s.Combo=-1;s.Combo=(s.Combo+1)%3;float rate=(float)p.Stats["attackRate"];CyberBegin(p,new[]{"cyber-jab","cyber-cross","cyber-palm"}[s.Combo],(s.Combo==2?.18f:.1f)/rate,NativeCyberRules.ComboSeconds[s.Combo]/rate);s.LastComboEnd=s.Until;p.Combo=s.Combo;}
        }
        private NativePilot CyberAim(NativePilot p,float range)
        {
            Vector3 from=p.Body.position+Vector3.up*.3f;V3 end=Vec(from)+GunRules.Direction(p.Input.Yaw,p.Input.Pitch)*range;NativePilot best=null;float nearest=2;
            foreach(var q in Pilots)
                if(q.Hp>0&&CanHit(p,q)&&GunRules.SegmentActor(Vec(from),end,new Actor{Position=Vec(q.Body.position-Vector3.up*.865f)},0,out float t,out _)&&t<nearest&&MeleeVisible(from,q.Body.position,p,q.Collider)){nearest=t;best=q;}
            return best;
        }
        private float CyberHit(NativePilot p,NativePilot q,float amount,string detail,bool primary=false,float stun=0)
        {
            float actual=Damage(q,p,amount*(primary?(float)p.Stats["damage"]:CyberScale(p)),q.Body.position-p.Body.position,120,false,primary:primary,melee:primary,weaponHit:primary,damageKind:detail);
            if(actual>0){if(primary)p.Mana=Math.Min((float)p.Stats["maxMana"],p.Mana+6);if(stun>0&&q.Hp>0)StaggerMelee(q,stun);if(primary)ImpactChips(p,q,null,q.Body.position,(q.Body.position-p.Body.position).normalized,p.CyberAttack,p.CyberAttack);}
            return actual;
        }
        private void CyberArea(NativePilot p,float amount,string detail,Func<Vector3,bool> contains,bool primary=false,float stun=0,bool launch=false)
        {
            foreach(var q in Pilots.ToArray())
            {
                if(p.Hp<=0)return;if(q.Hp<=0||!CanHit(p,q)||!contains(q.Body.position)||!MeleeVisible(p.Body.position,q.Body.position,p,q.Collider))continue;
                float actual=CyberHit(p,q,amount,detail,primary,stun);if(actual>0&&launch&&q.Hp>0)LaunchMelee(q,p,3.8f);
            }
            foreach(var m in Minions.ToArray())
            {if(p.Hp<=0)return;if(m.Hp>0&&CanHit(p,m)&&contains(m.Body.position)&&MeleeVisible(p.Body.position,m.Body.position,p,m.Collider))HitMinion(m,p,amount*(primary?(float)p.Stats["damage"]:CyberScale(p)),m.Body.position-p.Body.position,120,m.Body.position,detail);}
        }
        private void StepCyberWorld()
        {
            foreach(var p in Pilots)
            {
                var s=p.Cyber;if(!CyberAllowed(p)||p.Hp<=0||!p.Seat.connected){p.Cyber=new NativeCyberKit();continue;}
                if(Incapacitated(p)||Time<p.BlockEnd||Time<p.RollEnd){s.Resolved=true;s.DashUntil=0;s.LinkUntil=0;s.Until=Time;p.CyberLinkTick=0;}
                if(!s.Resolved&&Time>=s.Release)
                {
                    s.Resolved=true;
                    if(s.Kind.StartsWith("cyber-"))CyberArea(p,NativeCyberRules.ComboDamage[s.Combo],s.Kind,q=>NativeCyberRules.Cone(Vec(q-p.Body.position),s.Facing,2.2f,1.4f),true,NativeMeleeRules.HitStun(p.Weapon,(float)p.Stats["attackRate"]),s.Combo==2);
                    else if(s.Kind=="packet-burst")CyberPackets(p);
                    else if(s.Kind=="system-crash")CyberArea(p,25,"system-crash",q=>new Vector2(q.x-s.CrashPoint.x,q.z-s.CrashPoint.z).magnitude<=4&&Math.Abs(q.y-s.CrashPoint.y)<=1.6f,stun:1);
                }
                if(s.DashUntil>0&&Time>=s.DashAt&&Time<=s.DashUntil+Rules.Dt&&!KernelBusy(p)){p.Body.position=KernelTravel(p.Body.position,Vec(NativeCyberRules.DashPosition(s,Time)));p.MoveVelocity=Vector3.zero;}
                if(Time>=p.CyberTrailTick&&p.CyberTrailTick>0&&p.CyberTrailTick<=s.TrailUntil+.0001f)
                {p.CyberTrailTick+=.3f;CyberArea(p,6,"data-dash",q=>NativeCyberRules.SegmentDistance(Vec(q),s.DashFrom,s.DashTo)<=.42f);}
                if(Time>=p.CyberWallTick&&p.CyberWallTick>0&&p.CyberWallTick<=s.WallUntil+.0001f)
                {p.CyberWallTick+=.25f;CyberArea(p,2.5f,"firewall",q=>NativeCyberRules.WallCross(Vec(q),Vec(q),s.WallPoint,s.WallYaw,.42f,out _));}
                var linked=Pilots.FirstOrDefault(q=>q.Seat.id==s.LinkTarget);
                if(p.CyberLinkTick>0&&p.CyberLinkTick<=s.LinkUntil+.0001f&&(linked==null||linked.Hp<=0||!linked.Seat.connected||!CanHit(p,linked)||Vector3.Distance(p.Body.position,linked.Body.position)>15||!MeleeVisible(p.Body.position,linked.Body.position,p,linked.Collider))){s.LinkUntil=Time;p.CyberLinkTick=0;}
                if(Time>=p.CyberLinkTick&&p.CyberLinkTick>0&&p.CyberLinkTick<=s.LinkUntil+.0001f&&linked!=null){p.CyberLinkTick+=.25f;Heal(p,CyberHit(p,linked,2,"siphon-link")*.5f);}
            }
        }
        private void CyberPackets(NativePilot p)
        {
            var start=p.Body.position+Vector3.up*.3f;int attack=nextAttack++;
            for(int i=0;i<8&&ProjectileCount<100;i++)
            {
                var dir=Vec(GunRules.Direction(p.Input.Yaw+(i-3.5f)*(50f/7),p.Input.Pitch));
                var shot=new NativeBullet{CyberPacket=true,Id=nextBullet++,Attack=attack,Owner=p.Seat.id,Original=p.Seat.id,Spec="cyberlord",Born=Time,Position=start,Velocity=dir*60,BaseVelocity=dir*60,FixedSpeed=60,Damage=5*CyberScale(p),Radius=.045f,Impulse=80,Gravity=0,Life=.2f,Travel=6,Direct=false,WeaponFx=true};Bullets.Add(shot);Emit(EventKind.Shot,p,0,start,5,"packet-burst");
            }
        }
        private bool CyberAbsorb(NativePilot owner,Vector3 from,Vector3 to,float radius)
        {
            foreach(var wall in Pilots)
            {
                var s=wall.Cyber;if(wall.Hp<=0||!wall.Seat.connected||!CyberAllowed(wall)||Time>=s.WallUntil||!CanHit(wall,owner)||!NativeCyberRules.WallCross(Vec(from),Vec(to),s.WallPoint,s.WallYaw,radius,out float fraction))continue;
                Vector3 point=Vector3.Lerp(from,to,fraction);
                var prior=SweepNative(from,point-from,radius,c=>{var q=c.GetComponent<NativePilotBody>()?.Pilot;var m=c.GetComponent<NativeMinionBody>()?.Minion;return c!=owner.Collider&&(q==null||q.Hp>0&&CanHit(owner,q)&&!Invulnerable(q))&&(m==null||m.Hp>0&&CanHit(owner,m));});
                if(prior.HasValue&&prior.Value.Distance<(point-from).magnitude-.01f)continue;
                Emit(EventKind.Absorb,wall,0,point,0,"firewall");return true;
            }
            return false;
        }
    }
}
