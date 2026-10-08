using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed class NativeSirenState
    {public bool Ranged;public float ModeAt=-100,Scale=1,Reach=1,Cooldown=1;public int Side=1,MaxRains=int.MaxValue,MaxEruptions=int.MaxValue;public readonly Dictionary<string,float> Ready=new Dictionary<string,float>();public NativeSirenMove Move;}
    public sealed class NativeSirenMove
    {public SirenRule Rule;public int Id,Side;public float At,HitAt,EndAt,Until,TrailAt,CaughtAt,CrushAt;public NativePilot Target,Caught;public Vector3 Direction,Aim;public bool Fired;public readonly HashSet<int> Hit=new HashSet<int>();}
    public sealed class NativeSirenField
    {public NativePilot Owner;public int Id;public string Kind;public Vector3 Position;public float Radius,At,Until,Next;public bool Fired;}
    public sealed partial class NativePilot {public NativeSirenState Siren;public NativePilot HeldBy;}
    public sealed partial class NativeArena
    {
        public readonly List<NativeSirenField> SirenFields=new List<NativeSirenField>();private float sirenFieldAt;
        private NativeSirenState Siren(NativePilot p)=>p.Siren??(p.Siren=new NativeSirenState());
        private bool SirenArmor(NativePilot p)=>Dev(p,"siren")&&p.Hp>0&&p.Siren?.Move!=null&&p.Siren.Move.Rule.Id!="swipe"&&p.Siren.Move.Rule.Id!="hellfire"&&Time<p.Siren.Move.Until;
        private void CancelSiren(NativePilot p)
        {var m=p.Siren?.Move;if(m?.Caught!=null){m.Caught.HeldBy=null;m.Caught.Body.useGravity=true;m.Caught.Knockback=m.Caught.MoveVelocity=m.Caught.Body.linearVelocity=Vector3.zero;}if(p.Siren!=null)p.Siren.Move=null;if(p.HeldBy!=null){var owner=p.HeldBy;p.HeldBy=null;CancelSiren(owner);}p.Body.useGravity=true;}
        private NativePilot SirenTarget(NativePilot p,float range,bool aimed=false)=>Pilots.Where(q=>q.Hp>0&&q.Seat.connected&&Enemy(p,q)&&Vector3.Distance(q.Body.position,p.Body.position+Vector3.up*.45f)<=range&&(!aimed||Vector3.Dot((q.Body.position-p.Body.position-Vector3.up*.45f).normalized,Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)))>.7f)&&MeleeVisible(p.Body.position+Vector3.up*.45f,q.Body.position,p,q.Collider)).OrderBy(q=>(q.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();
        private bool StartSiren(NativePilot p,string id)
        {
            var state=Siren(p);var r=NativeSirenRules.Rule(id);if(!Dev(p,"siren")||r==null||!FighterReady(p)||state.Move!=null||p.HeldBy!=null||Time<p.Ready||state.Ready.TryGetValue(id,out float ready)&&Time<ready)return false;
            if(!(id==(state.Ranged?"hellfire":"swipe")||(state.Ranged?NativeSirenRules.Ranged:NativeSirenRules.Melee).Contains(id)))return false;
            if(id=="rain"&&SirenFields.Count(f=>f.Owner==p&&f.Kind=="rain")>=state.MaxRains||id=="eruption"&&SirenFields.Count(f=>f.Owner==p&&f.Kind=="eruption")>=state.MaxEruptions)return false;
            if(id=="mark"&&Bullets.Any(b=>b.Owner==p.Seat.id&&b.SirenKind=="seeker"))return false;var target=id=="mark"?SirenTarget(p,40,true):null;if(id=="mark"&&target==null)return false;
            var m=new NativeSirenMove{Id=nextAttack++,Rule=r,At=Time,HitAt=Time+r.Windup,EndAt=Time+r.Windup+r.Travel,Until=Time+NativeSirenRules.Duration(r),Target=target,Direction=Vec(V3.Forward(p.Input.Yaw)),Side=state.Side};state.Side=-state.Side;
            Vector3 point=p.Input.AbilityTarget.Length>.01f?Vec(p.Input.AbilityTarget):p.Body.position+Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch))*28;m.Aim=GroundPoint(p.Body.position+Vector3.ClampMagnitude(point-p.Body.position,28));
            state.Move=m;state.Ready[id]=Time+(r.Cooldown>0?r.Cooldown:NativeSirenRules.Duration(r))*state.Cooldown;p.AttackAt=p.LastAttack=Time;p.AttackEnd=p.Ready=m.Until;p.Combo++;return true;
        }
        private float SirenHit(NativePilot p,NativePilot q,float damage,bool melee=false,Vector3? direction=null)
        {
            if(q.Hp<=0||!q.Seat.connected||!Enemy(p,q)||Invulnerable(q)||NativeSpecialRules.Protected(q.Down,Time))return 0;Vector3 dir=direction??(q.Body.position-p.Body.position).normalized;
            if(Time<q.BlockEnd){BlockProc(q,p,q.Body.position);if(melee)SuccessfulMeleeBlock(q,p,damage,dir,100);return 0;}
            return Damage(q,p,damage*(float)p.Stats["damage"],dir,0,false,direct:false,primary:false,melee:melee);
        }
        private void SirenLaunch(NativePilot p,NativePilot q,float speed)
        {if(q.Hp<=0||SirenArmor(q))return;StunMelee(q,.45f);q.Down=null;q.Juggle=new NativeJuggle{At=Time,Until=Time+2,LastHit=Time,Hits=1,Owner=p};q.Knockback.y=speed-q.Body.linearVelocity.y;q.Grounded=false;}
        private void SirenArea(NativePilot p,Vector3 at,float radius,float damage,float lift=0,bool falloff=false)
        {foreach(var q in Pilots){Vector3 d=q.Body.position-at;float dist=new Vector2(d.x,d.z).magnitude;if(dist>radius+.35f||Math.Abs(d.y)>4*Siren(p).Scale||!MeleeVisible(at+Vector3.up*.12f,q.Body.position,p,q.Collider))continue;if(SirenHit(p,q,damage*(falloff?Math.Max(.15f,1-dist/(radius+.4f)):1))>0&&lift>0)SirenLaunch(p,q,lift);}Fx(at,radius,4);}
        private void SirenMelee(NativePilot p,NativeSirenMove m,float range,float damage,float arc)
        {
            Vector3 at=p.Body.position,dir=Vec(V3.Forward(p.Input.Yaw));foreach(var q in Pilots)
            {Vector3 d=q.Body.position-at;if(m.Hit.Contains(q.Seat.id)||new Vector2(d.x,d.z).magnitude>range+.35f||Math.Abs(d.y)>1.9f*Siren(p).Scale||Vector3.Dot(new Vector3(d.x,0,d.z).normalized,dir)<Mathf.Cos(arc/2)||!MeleeVisible(at,q.Body.position,p,q.Collider))continue;m.Hit.Add(q.Seat.id);if(SirenHit(p,q,damage,true)>0&&m.Rule.Id=="rend"&&q.Hp>0&&!SirenArmor(q)){StunMelee(q,.1f);q.Down=new NativeRecovery{At=Time,Until=Time+1.2f,Source=p.Seat.id};q.Knockback=dir*3+Vector3.up*2.5f;}if(p.Hp<=0||Incapacitated(p))break;}
        }
        private void SirenPatch(NativePilot p,Vector3 at,bool trail=false)
        {string kind=trail?"trail":"hellfire";var own=SirenFields.Where(f=>f.Owner==p&&f.Kind==kind).ToArray();if(own.Length>=(trail?24:12))SirenFields.Remove(own[0]);SirenFields.Add(new NativeSirenField{Id=nextEvent++,Owner=p,Kind=kind,Position=GroundPoint(at),Radius=(trail?.85f:1.1f)*Siren(p).Reach,At=Time,Until=Time+(trail?3:10)});}
        private void SirenShot(NativePilot p,string kind,NativePilot target=null,Vector3? position=null)
        {
            if(ProjectileCount>=100)return;var state=Siren(p);float speed=kind=="drop"?10:kind=="seeker"?3:kind=="damnation"?13:20;Vector3 offset=kind=="damnation"?new Vector3(0,1.42f,.12f):kind=="seeker"?new Vector3(-.3f,.7f,.7f):new Vector3(state.Move?.Side==-1?.36f:-.36f,.62f,.52f);Vector3 pos=position??(p.Body.position+Quaternion.Euler(0,p.Input.Yaw,0)*offset*state.Scale);
            Bullets.Add(new NativeBullet{Id=nextBullet++,Attack=nextAttack++,Owner=p.Seat.id,Original=p.Seat.id,Spec="siren",SirenKind=kind,SirenTarget=target?.Seat.id??0,Born=Time,Position=pos,Velocity=(kind=="drop"?Vector3.down:Vec(GunRules.Direction(p.Input.Yaw,p.Input.Pitch)))*speed,Damage=kind=="drop"?9:kind=="seeker"?85:kind=="damnation"?30:10,Gravity=kind=="damnation"?8:0,Life=kind=="seeker"?float.PositiveInfinity:5,Travel=200,Radius=(kind=="drop"?.12f:kind=="seeker"?.32f:kind=="damnation"?.42f:.18f)*state.Reach,Direct=false});
        }
        private void StepSirenPilot(NativePilot p)
        {
            if(!Dev(p,"siren"))return;var s=Siren(p);if(!FighterReady(p)||p.HeldBy!=null){CancelSiren(p);return;}
            if(p.Input.AbilityQ&&s.Move==null&&Time>=p.Ready){s.Ranged=!s.Ranged;s.ModeAt=Time;p.Ready=Time+.35f;}
            if(p.Input.SirenAbility>0&&p.Input.SirenAbility<=4)StartSiren(p,(s.Ranged?NativeSirenRules.Ranged:NativeSirenRules.Melee)[p.Input.SirenAbility-1]);
            if(p.Input.Shoot)StartSiren(p,s.Ranged?"hellfire":"swipe");var m=s.Move;if(m==null)return;var r=m.Rule;
            if(!m.Fired&&Time>=m.HitAt)
            {
                m.Fired=true;
                switch(r.Id)
                {
                    case "slam":SirenArea(p,GroundPoint(p.Body.position),3.6f*s.Reach,16,9);break;
                    case "laugh":m.Target=SirenTarget(p,7);Vector3 d=m.Target!=null?m.Target.Body.position-p.Body.position:Vec(V3.Forward(p.Input.Yaw));d.y=0;m.Direction=d.normalized;break;
                    case "hellfire":case "damnation":SirenShot(p,r.Id);break;
                    case "eruption":SirenFields.Add(new NativeSirenField{Id=nextEvent++,Owner=p,Kind="eruption",Position=m.Aim,Radius=2.2f*s.Reach,At=Time,Next=Time+1,Until=Time+1.5f});break;
                    case "rain":SirenFields.Add(new NativeSirenField{Id=nextEvent++,Owner=p,Kind="rain",Position=m.Aim,Radius=7*s.Reach,At=Time,Next=Time,Until=Time+10});break;
                    case "mark":if(m.Target?.Hp>0)SirenShot(p,"seeker",m.Target);else CancelSiren(p);break;
                }
            }
            if((r.Id=="swipe"||r.Id=="rend")&&Time>=m.HitAt&&Time<m.HitAt+r.Active)SirenMelee(p,m,r.Range*s.Reach,r.Damage,r.Arc);
            if(r.Id=="rush"&&Time>=m.HitAt&&Time<m.EndAt){SirenMelee(p,m,1.1f*s.Reach,14,Mathf.PI*2);if(Time>=m.TrailAt){SirenPatch(p,p.Body.position,true);m.TrailAt=Time+.07f;}}
            if(r.Id=="laugh"&&m.Caught==null&&Time>=m.HitAt&&Time<m.EndAt)
            {var q=m.Target;if(q?.Hp>0&&q.Seat.connected&&!Invulnerable(q)&&!NativeSpecialRules.Protected(q.Down,Time)&&q.HeldBy==null&&Vector3.Distance(q.Body.position,p.Body.position)<=1.05f*s.Reach&&MeleeVisible(p.Body.position,q.Body.position,p,q.Collider)){StunMelee(q,.1f);q.Roll=null;q.RollEnd=Time;q.HeldBy=p;m.Caught=q;m.CaughtAt=Time;m.CrushAt=Time+3.2f;m.Until=Time+3.8f;p.AttackEnd=p.Ready=m.Until;}}
            if(Time>=m.Until)CancelSiren(p);
        }
        private Vector3 SirenMotion(NativePilot p)
        {var m=p.Siren?.Move;if(m==null||Time>=m.Until||Time<m.HitAt||Time>=m.EndAt||m.Rule.Id!="rush"&&!(m.Rule.Id=="laugh"&&m.Caught==null))return Vector3.zero;return m.Direction*(m.Rule.Id=="rush"?6*Siren(p).Reach/.44f:7/.35f)*Math.Min(1,(m.EndAt-Time)*60);}
        private bool PinSirenVictim(NativePilot q)
        {var p=q.HeldBy;var m=p?.Siren?.Move;if(p==null||m==null||p.Hp<=0||q.Hp<=0){if(p!=null)CancelSiren(p);return false;}float lift=Mathf.Clamp01((Time-m.CaughtAt)/.5f);Vector3 point=p.Body.position+Quaternion.Euler(0,p.Input.Yaw,0)*new Vector3(0,(.2f+lift*.75f)*Siren(p).Scale,Siren(p).Scale);q.Body.position=point;q.Body.linearVelocity=q.Knockback=q.MoveVelocity=Vector3.zero;q.Body.useGravity=false;return true;}
        private void StepSirenWorld()
        {
            foreach(var p in Pilots){var m=p.Siren?.Move;if(m?.Caught==null)continue;var q=m.Caught;if(p.Hp<=0||q.Hp<=0||!q.Seat.connected){CancelSiren(p);continue;}if(Time>=m.CrushAt){if(Pve!=null)Damage(q,p,(float)q.Stats["hp"]*.6f*NativePveRules.BossDamage(PartySize),Vector3.zero,0,false,direct:false,primary:false);else Damage(q,p,q.Hp+1,Vector3.zero,0,false,forced:true,primary:false);CancelSiren(p);}else PinSirenVictim(q);}
            foreach(var f in SirenFields.ToArray())
            {
                if(f.Owner.Hp<=0||!f.Owner.Seat.connected||Time>=f.Until){SirenFields.Remove(f);continue;}
                if(f.Kind=="eruption"&&!f.Fired&&Time>=f.Next){f.Fired=true;SirenArea(f.Owner,f.Position,f.Radius,28,10);}
                if(f.Kind=="rain")while(Time>=f.Next&&f.Next<f.Until){f.Next+=.22f;float a=(float)random.NextDouble()*Mathf.PI*2,r=Mathf.Sqrt((float)random.NextDouble())*f.Radius;Vector3 land=GroundPoint(f.Position+new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r));SirenShot(f.Owner,"drop",position:land+Vector3.up*8);}
            }
            if(Time<sirenFieldAt)return;sirenFieldAt=Time+.25f;var touched=new HashSet<string>();
            foreach(var f in SirenFields)if(f.Kind=="trail"||f.Kind=="hellfire")foreach(var q in Pilots){Vector3 d=q.Body.position-f.Position;string key=f.Owner.Seat.id+":"+q.Seat.id;if(touched.Contains(key)||new Vector2(d.x,d.z).magnitude>f.Radius+.3f||Math.Abs(d.y)>.95f||!MeleeVisible(f.Position+Vector3.up*.15f,q.Body.position,f.Owner,q.Collider))continue;touched.Add(key);SirenHit(f.Owner,q,1.25f);}
        }
        private bool StepSirenShot(NativeBullet b)
        {
            if(b.SirenKind==null)return false;var p=Pilots.Find(x=>x.Seat.id==b.Owner);if(p==null||p.Hp<=0||!p.Seat.connected||!Dev(p,"siren")){Bullets.Remove(b);return true;}
            bool seeker=b.SirenKind=="seeker";if(seeker){var q=Pilots.Find(x=>x.Seat.id==b.SirenTarget);if(q==null||q.Hp<=0||!q.Seat.connected){Bullets.Remove(b);return true;}b.Velocity=(q.Body.position+Vector3.up*.2f-b.Position).normalized*3;}else{b.Life-=1f/60;if(b.Life<=0){Bullets.Remove(b);return true;}}
            Vector3 delta=b.Velocity/60+Vector3.down*b.Gravity/(2*3600);b.Velocity.y-=b.Gravity/60;float left=delta.magnitude;Vector3 dir=delta.normalized;
            for(int i=0;i<512&&left>.000001f;i++)
            {
                var hit=SweepNative(b.Position,dir*left,b.Radius,c=>{var q=c.GetComponent<NativePilotBody>()?.Pilot;return q!=null?q.Hp>0&&q.Seat.connected&&Enemy(p,q)&&!Invulnerable(q)&&!NativeSpecialRules.Protected(q.Down,Time)&&(!seeker||q.Seat.id==b.SirenTarget):c.GetComponent<NativePropBody>()!=null;});
                if(!hit.HasValue){b.Position+=dir*left;break;}var h=hit.Value;b.Position=h.Point;var target=h.Collider.GetComponent<NativePilotBody>()?.Pilot;var prop=h.Collider.GetComponent<NativePropBody>();
                if(seeker&&prop!=null&&!prop.Indestructible){DamageProp(prop,prop.Health+1,Vector3.zero,h.Point);left-=h.Distance;continue;}
                if(seeker){if(target!=null)SirenHit(p,target,85);Fx(h.Point,1,4);}
                else if(b.SirenKind=="damnation"){SirenArea(p,h.Point,3.5f*Siren(p).Reach,30,0,true);foreach(var cover in Props.ToArray())if(!cover.Indestructible&&Vector3.Distance(cover.transform.position,h.Point)<3.5f*Siren(p).Reach&&MeleeVisible(h.Point,cover.transform.position,p,cover.GetComponent<Collider>()))DamageProp(cover,30*(float)p.Stats["demolition"],dir*150,h.Point);}
                else{if(target!=null)SirenHit(p,target,b.Damage);if(prop!=null)DamageProp(prop,b.Damage*(float)p.Stats["demolition"],dir*80,h.Point);if(b.SirenKind=="hellfire")SirenPatch(p,h.Point);else Fx(h.Point,.3f,4);}
                Bullets.Remove(b);break;
            }
            return true;
        }
    }
}
