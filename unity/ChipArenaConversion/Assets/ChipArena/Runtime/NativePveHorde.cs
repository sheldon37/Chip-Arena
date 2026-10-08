using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ChipArena.UnityConversion
{
    public sealed class NativePveAdd
    {
        public PveAddStats Stats;public PveEntry Entry;public int Lost,Side=1;public float ActiveAt,ExposedAt,SpawnAt,ThinkAt,RecoverAt,StaggerAt,DamageScale,HpScale,PossessedAt,SlowUntil,SlowFactor=1,LastDamage=-100,VentReady,WalkAt,DiveAt,VentUntil;public bool Struck=true,Possessed,Crawl,Limp,First=true,Travelling;public Vector3 External;public NativePilot Target;public NativeMinion KillerSummon;public NativePilot LastHit;
        public readonly Dictionary<int,float> Dealt=new Dictionary<int,float>();public readonly Dictionary<int,float> Parts=new Dictionary<int,float>();
    }
    public sealed partial class NativeArena
    {
        private readonly Dictionary<int,float> pveEntries=new Dictionary<int,float>();private NativePveNavigation pveNavigation;
        private NativeMinion SpawnPveAdd(string kind,bool force=false)
        {
            var v=Pve;var members=Members().Where(p=>p.Hp>0).ToArray();var candidates=v.Content.entries.Select(e=>new{entry=e,weight=EntryQueue(e.e)>=3||e.kind=="vent"&&VentHazard(e.e)?0:NativeEntryRules.Weight(force?float.PositiveInfinity:members.Select(p=>new Vector2(p.Body.position.x-e.@exit.x,p.Body.position.z-e.@exit.z).magnitude).DefaultIfEmpty(float.PositiveInfinity).Min(),e.kind,v.Stage=="wave",v.Hidden)}).ToArray();float total=candidates.Sum(e=>e.weight);if(total<=0)return null;
            double roll=v.SpawnRandom.Next()*total;var entry=candidates.Last(e=>e.weight>0).entry;foreach(var choice in candidates){roll-=choice.weight;if(choice.weight>0&&roll<0){entry=choice.entry;break;}}
            var times=PlanEntry(entry);var stats=v.Content.adds.First(x=>x.id==kind);int wave=v.Stage=="boss"?3:Math.Max(1,v.Wave);float hpScale=1+.12f*(wave-1),damageScale=1+.06f*(wave-1);
            var obj=new GameObject("Siren "+kind);obj.layer=11;obj.transform.SetParent(root.transform);obj.transform.position=Vec(entry.from.Position(entry.kind=="vent"?-1:.9f));var c=obj.AddComponent<CapsuleCollider>();c.radius=.42f;c.height=1.7f;c.enabled=false;c.sharedMaterial=surfaces["pilot"];var body=obj.AddComponent<Rigidbody>();body.mass=40;body.constraints=RigidbodyConstraints.FreezeRotation;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            var a=new NativePveAdd{Stats=stats,Entry=entry,HpScale=hpScale,DamageScale=damageScale,SpawnAt=times.spawn,WalkAt=times.walk,First=times.first,ActiveAt=times.active,ExposedAt=entry.kind=="vent"?times.active-1.15f:times.walk};
            var m=new NativeMinion{Id=nextMinion++,Owner=v.Boss,Kind="siren-add",Body=body,Collider=c,Hp=stats.hp*hpScale,MaxHp=stats.hp*hpScale,Until=float.PositiveInfinity,Yaw=entry.yaw*Mathf.Rad2Deg,Pve=a};obj.AddComponent<NativeMinionBody>().Minion=m;Minions.Add(m);return m;
        }
        private void StepPveHorde()
        {
            var v=Pve;const float dt=1f/60;pveNavigation=pveNavigation??new NativePveNavigation(this);
            int active=Minions.Count(m=>m.Pve!=null),entering=Minions.Count(m=>m.Pve!=null&&Time<m.Pve.ActiveAt);
            if(v.Rate>0&&Time>=v.SpawnAt&&active<v.Cap&&entering<(v.Hidden?64:16)&&(v.SpawnBudget<0||v.Spawned<v.SpawnBudget))
            {int count=Math.Min(8,1+(int)((Time-v.SpawnAt)*v.Rate));for(int n=0;n<count&&active<v.Cap;n++){var mix=v.Stage=="boss"?v.Content.bossMix[v.Phase-1].mix:v.Content.waves[v.Wave-1].mix;var created=SpawnPveAdd(NativePveRules.Weighted(mix,v.SpawnRandom.Next));if(created==null)break;v.Spawned++;active++;v.SpawnAt+=1/v.Rate;if(v.SpawnBudget>=0&&v.Spawned>=v.SpawnBudget)break;}}else if(active>=v.Cap||entering>=(v.Hidden?64:16))v.SpawnAt=Time;
            var enemies=Pilots.Where(p=>p.Hp>0&&p.Seat.connected&&!p.Seat.AuthorityBoss).ToArray();var adds=Minions.Where(m=>m.Pve!=null).ToArray();
            foreach(var m in adds)
            {
                if(m.Hp<=0)continue;var a=m.Pve;
                if(a.Travelling){if(Time>=a.VentUntil)Reemerge(m);continue;}
                if(Time<a.ActiveAt)
                {Vector3 to=Vec(a.Entry.@exit.Position(.9f));if(a.Entry.kind=="vent")m.Body.position=to+Quaternion.Euler(0,m.Yaw,0)*Vec(NativeEntryRules.Climb(Time,a.ActiveAt));else m.Body.position=Vector3.Lerp(Vec(a.Entry.from.Position(.9f)),to,Mathf.Clamp01((Time-a.WalkAt)/Math.Max(.1f,a.ActiveAt-a.WalkAt)));m.Collider.enabled=Time>=a.ExposedAt;continue;}
                if(m.Body.isKinematic){m.Body.isKinematic=false;m.Collider.enabled=true;m.Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;}
                if(m.Body.position.y<-6){if(a.Possessed)FoundPveBoss(new Vector3(0,0,24.2f));RemoveMinion(m,false);continue;}
                foreach(var pair in m.Dots.ToArray()){var dot=pair.Value;while(m.Hp>0&&dot.Next<=Time+1e-8&&dot.Next<=dot.Until+1e-8){HitMinion(m,Pilots.Find(p=>p.Seat.id==dot.Source),(float)(dot.Dps*.25),Vector3.zero,0);dot.Next+=.25;}if(Time>dot.Until)m.Dots.Remove(pair.Key);}if(m.Hp<=0)continue;
                Vector3 at=m.Body.position;var target=a.Target;
                if(Time>=a.ThinkAt||target==null||target.Hp<=0||!target.Seat.connected)
                {
                    a.ThinkAt=Time+.75f;target=enemies.Where(p=>!a.Possessed||!p.Seat.bot).OrderBy(p=>pveNavigation.Distance(at,p.Body.position,p.Seat.id)+3*Math.Max(0,adds.Count(q=>q.Pve.Target==p)-6)-(a.Target==p?2:0)-(v.Recruits.Any(r=>r.Pilot==p&&r.Role=="tank")&&Vector3.Distance(p.Body.position,at)<8?6:0)).FirstOrDefault();a.Target=target;if(target!=null&&TryVentTravel(m,target))continue;
                }
                if(!a.Struck&&Time>=m.StrikeAt){a.Struck=true;PveAddStrike(m);if(m.Hp<=0)continue;}
                if(target==null){m.Body.linearVelocity=new Vector3(0,m.Body.linearVelocity.y,0);continue;}Vector3 d=target.Body.position-at;float distance=new Vector2(d.x,d.z).magnitude;
                bool left=(a.Lost&(2|8))!=0,right=(a.Lost&(4|16))!=0,armless=left&&right;float reach=a.Crawl?1.3f:armless?1.2f:a.Stats.reach,start=a.Crawl?1:armless?.95f:a.Stats.startRange,hit=a.Crawl||armless?.34f:a.Stats.hit;
                if(a.Struck&&!a.Possessed&&Time>=a.RecoverAt&&Time>=a.StaggerAt&&distance<=start&&Math.Abs(d.y)<1.6f)
                {m.LastAttack=Time;m.StrikeAt=Time+hit;a.Struck=false;a.RecoverAt=Time+Math.Max(a.Stats.cycle,hit+.05f);a.Side=-a.Side;if(a.Side==1&&right)a.Side=-1;else if(a.Side==-1&&left)a.Side=1;m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;}
                Vector3 move=Vector3.zero;
                if(Time<a.StaggerAt){}
                else if(!a.Struck){m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;if(distance>.8f)move=Vec(V3.Forward(m.Yaw))*((a.Crawl?.15f:armless?.45f:a.Stats.lunge)/Math.Max(.05f,hit));}
                else if(Time>=a.RecoverAt)
                {
                    float speed=(distance<=7||m.Hp<m.MaxHp*.5f?a.Stats.run:a.Stats.walk)*(a.Crawl?.35f:a.Limp?.55f:1);Vector3 dir=pveNavigation.Direction(at,target.Body.position,target.Seat.id);
                    if(a.Possessed){float age=Time-a.PossessedAt;if(age<60){if(age%6>=5.2f||distance>=8&&distance<=14)dir=Vector3.zero;else if(distance<8)dir=-dir;}speed= a.Stats.run;}
                    move=distance>1.1f||a.Possessed?dir*speed:Vector3.zero;
                }
                if(move.sqrMagnitude>.001f){float factor=Time<a.SlowUntil?a.SlowFactor:1;foreach(var f in ChipFields)if(Enemy(f.Owner,m.Owner)&&Vector3.Distance(f.Position,at)<f.Radius)factor=Math.Min(factor,f.Kind=="frost"?.45f:f.Kind=="time"?.6f:1);move*=factor;int n=0;foreach(var other in adds){if(other==m||Time<other.Pve.ActiveAt||n>=6)continue;Vector3 diff=at-other.Body.position;diff.y=0;float dist=diff.magnitude;if(dist<.9f){n++;move+=dist<.0001f?Vector3.right*(m.Id<other.Id?-1.8f:1.8f):diff/dist*(.9f-dist)*2;}}m.Yaw=Mathf.Atan2(move.x,move.z)*Mathf.Rad2Deg;}
                a.External*=Mathf.Exp(-6*dt);move+=Vector3.ClampMagnitude(a.External,6);m.Body.linearVelocity=new Vector3(move.x,m.Body.linearVelocity.y,move.z);
            }
        }
        private bool TryVentTravel(NativeMinion m,NativePilot target)
        {
            var a=m.Pve;if(a.Crawl||!a.Struck||Time<a.VentReady||Time-a.LastDamage<2||a.Possessed&&Time-a.PossessedAt<10)return false;
            int e=Array.FindIndex(StageVents,v=>new Vector2(v.x-m.Body.position.x,v.z-m.Body.position.z).magnitude<=2);if(e<0||pveNavigation.Distance(m.Body.position,target.Body.position,target.Seat.id)<=28||Minions.Count(n=>n.Pve?.Travelling==true)>=2||VentHazard(e,.9f)||Pve.SpawnRandom.Next()>=.25)return false;
            a.Entry=Pve.Content.entries.First(x=>x.e==e);a.Travelling=true;a.DiveAt=Time;a.VentUntil=Time+2.4f;a.VentReady=Time+20;a.External=Vector3.zero;m.Body.linearVelocity=Vector3.zero;m.Body.isKinematic=true;m.Collider.enabled=false;m.Body.position=StageVents[e]+Vector3.up*.9f;m.Yaw=Mathf.Atan2(-m.Body.position.x,-m.Body.position.z)*Mathf.Rad2Deg;DiveEntry(a.Entry);return true;
        }
        private void Reemerge(NativeMinion m)
        {
            var a=m.Pve;var entry=Pve.Content.entries.Where(e=>e.kind=="vent"&&EntryQueue(e.e)<3&&!VentHazard(e.e)).OrderBy(e=>a.Target==null?e.e:pveNavigation.Distance(Vec(e.@exit.Position(.9f)),a.Target.Body.position,a.Target.Seat.id)).FirstOrDefault();if(entry==null)return;
            var times=PlanEntry(entry);a.Entry=entry;a.Travelling=false;a.First=times.first;a.SpawnAt=times.spawn;a.WalkAt=times.walk;a.ActiveAt=times.active;a.ExposedAt=times.active-1.15f;m.Yaw=entry.yaw*Mathf.Rad2Deg;
        }
        private void PveAddStrike(NativeMinion m)
        {
            var a=m.Pve;Vector3 at=m.Body.position,dir=Vec(V3.Forward(m.Yaw));bool left=(a.Lost&(2|8))!=0,right=(a.Lost&(4|16))!=0,armless=left&&right;float damage=a.Stats.damage*a.DamageScale*(a.Crawl?.6f:armless?.35f:left!=right?.65f:1),reach=a.Crawl?1.3f:armless?1.2f:a.Stats.reach,impulse=a.Stats.impulse*(a.Crawl||armless?.5f:1);
            foreach(var q in Pilots)
            {
                if(q.Hp<=0||!q.Seat.connected||q.Seat.AuthorityBoss)continue;Vector3 d=q.Body.position-at;float dist=new Vector2(d.x,d.z).magnitude;
                if(a.Stats.attack=="smash"&&!a.Crawl){Vector3 center=at+dir*1.2f;if(new Vector2(q.Body.position.x-center.x,q.Body.position.z-center.z).magnitude>1.6f)continue;}
                else if(q!=a.Target||dist>reach||dist>.001f&&Vector3.Dot(dir,new Vector3(d.x,0,d.z).normalized)<(a.Stats.attack=="lunge"?.5f:.35f))continue;
                if(Math.Abs(d.y)>1.8f||!SummonVisible(m,at+Vector3.up*.3f,q.Body.position,q.Collider)||Invulnerable(q))continue;
                if(Time<q.BlockEnd){BlockProc(q,m.Owner,q.Body.position);a.StaggerAt=Time+.6f;a.External=-d.normalized*3;}else Damage(q,m.Owner,damage,new Vector3(d.x,0,d.z).normalized,impulse,false,primary:false,melee:true);
            }
        }
        private float HitPveAdd(NativeMinion m,NativePilot source,float amount,Vector3 dir,float impulse,Vector3? point,string kind,float radius,bool head)
        {
            var a=m.Pve;if(source==null||a.Travelling||Time<a.ExposedAt||Pve.Holding)return 0;float actual=0;
            foreach(var share in NativeHordeDamage.Shares(amount,new V3(-m.Body.position.x,m.Body.position.y,m.Body.position.z),-m.Yaw,a.Crawl,point.HasValue?(V3?)new V3(-point.Value.x,point.Value.y,point.Value.z):null,kind,radius>0?radius:3,head,Pve.SpawnRandom.Next))
            {
                int limb=(a.Lost&share.part)!=0?0:share.part;float applied=share.damage*(limb==1?1.5f:limb==0?1:.9f),taken=Math.Max(0,Math.Min(m.Hp,applied));m.Hp-=taken;actual+=taken;
                if(limb>0){a.Parts.TryGetValue(limb,out float pool);pool+=taken;a.Parts[limb]=pool;if(pool>=m.MaxHp*(limb==1?a.Stats.headFrac:a.Stats.limbFrac)){a.Lost|=NativeHordeDamage.Descendants(limb);if(limb==1)m.Hp=0;bool l=(a.Lost&(32|128))!=0,r=(a.Lost&(64|256))!=0;a.Crawl=(a.Lost&(32|64))!=0||l&&r;a.Limp=!a.Crawl&&(l||r);if(a.Crawl){m.Collider.height=.88f;m.Collider.radius=.32f;}Fx(point??m.Body.position,.35f,1);}}
            }
            a.LastHit=source;a.LastDamage=Time;a.Dealt.TryGetValue(source.Seat.id,out float dealt);a.Dealt[source.Seat.id]=dealt+actual;
            if(amount>=m.MaxHp*.25f||impulse>=400){a.StaggerAt=Time+a.Stats.stagger;a.Struck=true;a.RecoverAt=Time;}a.External+=new Vector3(dir.x,0,dir.z)*Math.Min(6,impulse/40);
            Emit(EventKind.Hit,source,m.Id,point??m.Body.position,actual);
            if(m.Hp<=0){Vector3 at=m.Body.position;PveAddKilled(m,source);RemoveMinion(m,false);if(a.Possessed)FoundPveBoss(at);}return actual;
        }
        private void PveAddKilled(NativeMinion m,NativePilot source)
        {
            var a=m.Pve;var recruit=Pve.Recruits.Find(r=>r.Pilot==source);NativePilot paid=recruit?.Owner??source;bool head=(a.Lost&1)!=0;float reward=a.Stats.reward;
            if(recruit!=null)GrantPveMoney(paid,(reward+(head?10:0))*.5f,"recruit");else{GrantPveMoney(paid,reward,"kill");if(head)GrantPveMoney(paid,10,"head");}
            if(Pve.Rows.TryGetValue(paid.Seat.id,out var row))row.Kills++;
            foreach(var hit in a.Dealt)if(hit.Key!=source.Seat.id&&hit.Key!=paid.Seat.id&&hit.Value>=m.MaxHp*.2f){var p=Pilots.Find(x=>x.Seat.id==hit.Key);GrantPveMoney(p,reward*.4f,"assist");if(Pve.Rows.TryGetValue(hit.Key,out var assist))assist.Assists++;}
            if(a.Possessed)foreach(var p in Members())GrantPveMoney(p,p==paid?300:100,"possessed");Emit(EventKind.Death,source,m.Id,m.Body.position,0);
        }
        private void StepPveRecruits()
        {
            foreach(var hire in Pve.Hires.Where(h=>Time>=h.at).ToArray())
            {
                Pve.Hires.Remove(hire);var row=Pve.Rows[hire.owner.Seat.id];if(Pilots.Count>=8||Pve.Recruits.Count>=3){row.Money+=300;row.Spent-=300;continue;}double roll=Pve.EconomyRandom.Next();string role=roll<.4?"dps":roll<.7?"tank":"medic",spec=role=="tank"?"shield":role=="medic"?"water":Pve.EconomyRandom.Next()<.5?"rifle":"sword";var seat=new NativeSeat{id=nextRecruit++,name=role=="dps"?"Recruit":role=="tank"?"Guardian":"Medic",spec=spec,team=0,bot=true};Match.Seats.Add(seat);var p=SpawnPilot(seat,0);p.Stats["hp"]=role=="tank"?250:role=="medic"?80:100;if(role=="tank")p.Stats["damageTaken"]*=.7;p.Hp=(float)p.Stats["hp"];var machine=Pve.Content.machines[hire.machine];p.Body.position=Vec(machine.dispense.Position(.9f));Pve.Recruits.Add(new NativePveRecruit{Pilot=p,Owner=hire.owner,Role=role,At=Time});row.Hired++;
            }
            foreach(var recruit in Pve.Recruits.ToArray())
            {
                var p=recruit.Pilot;if(p.Hp<=0){if(recruit.RemoveAt==0)recruit.RemoveAt=Time+2.5f;if(Time>=recruit.RemoveAt){Pve.Recruits.Remove(recruit);Pilots.Remove(p);Match.Seats.Remove(p.Seat);CapturedAnimation.Release(p.Body.gameObject);}continue;}
                if(recruit.Role!="medic"||Time<recruit.HealAt)continue;recruit.HealAt=Time+.5f;var target=recruit.Owner.Hp>0?recruit.Owner:Members().Where(q=>q.Hp>0).OrderBy(q=>q.Hp/q.Stats["hp"]).FirstOrDefault();if(target==null)continue;
                var endangered=Members().Where(q=>q.Hp>0&&q!=target&&q.Hp/q.Stats["hp"]<.35).OrderBy(q=>q.Hp/q.Stats["hp"]).FirstOrDefault();if(endangered!=null&&target.Hp/target.Stats["hp"]>.7)target=endangered;
                float distance=Vector3.Distance(p.Body.position,target.Body.position);if(distance<=9&&MeleeVisible(p.Body.position,target.Body.position,p,target.Collider))Heal(target,Math.Min(3*(distance<=4.5f?1:.5f)*(Time-target.LastDamage<1?.6f:1),(float)target.Stats["hp"]*.04f*.5f));
            }
        }
        private InputFrame PveBot(NativePilot p,InputFrame input)
        {
            if(Pve==null)return input;if(p.Seat.AuthorityBoss)return SirenBot(p,input);var recruit=Pve.Recruits.Find(r=>r.Pilot==p);var anchor=recruit?.Owner.Hp>0?recruit.Owner:Members().Where(q=>!q.Seat.bot&&q.Hp>0).OrderBy(q=>(q.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();
            var minion=Minions.Where(m=>m.Pve!=null&&m.Hp>0&&Time>=m.Pve.ExposedAt&&(anchor==null||Vector3.Distance(m.Body.position,anchor.Body.position)<=18)&&MeleeVisible(p.Body.position+Vector3.up*.5f,m.Body.position,p,m.Collider)).OrderBy(m=>m.Pve.Target==anchor?0:1).ThenBy(m=>(m.Body.position-p.Body.position).sqrMagnitude).FirstOrDefault();Vector3? target=minion?.Body.position??(!Pve.Hidden&&Pve.Boss.Hp>0?(Vector3?)Pve.Boss.Body.position:null);
            if(recruit?.Role=="medic")target=null;Vector3 move=Vector3.zero;
            if(target.HasValue){Vector3 d=target.Value-p.Body.position;float distance=new Vector2(d.x,d.z).magnitude;input.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;input.Pitch=Mathf.Atan2(d.y,distance)*Mathf.Rad2Deg;input.AimTarget=input.AbilityTarget=Vec(target.Value);bool melee=p.Weapon.@class=="melee";move=melee?(distance>p.Weapon.range*.65?d.normalized:Vector3.zero):distance>10?d.normalized:distance<6?-d.normalized*.8f:Vector3.zero;input.Shoot=!melee||distance<p.Weapon.range*1.25;input.Reload=p.Ammo==0;input.Block=minion!=null&&minion.Pve.Target==p&&!minion.Pve.Struck&&distance<=2;if(input.Block)input.Shoot=false;input.AbilityE=Time>=p.EReady&&((int)(Time*60)+p.Seat.id)%60==0;input.AbilityQ=p.Weapon.@class=="magic"&&Time>=p.QReady&&((int)(Time*60)+p.Seat.id)%90==0;}
            if(anchor!=null&&Vector3.Distance(anchor.Body.position,p.Body.position)>(target.HasValue?12:5))move=pveNavigation?.Direction(p.Body.position,anchor.Body.position,anchor.Seat.id)??(anchor.Body.position-p.Body.position).normalized;input.X=move.x;input.Z=move.z;return input;
        }
        private InputFrame SirenBot(NativePilot p,InputFrame input)
        {
            if(Pve.Hidden)return new InputFrame{Yaw=p.Yaw};var q=SirenTarget(p,60);if(q==null)return input;Vector3 d=q.Body.position-p.Body.position;float distance=new Vector2(d.x,d.z).magnitude;var s=Siren(p);input.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;input.Pitch=Mathf.Atan2(d.y,distance)*Mathf.Rad2Deg;input.AbilityTarget=Vec(q.Body.position-Vector3.up*.85f);input.AimTarget=Vec(q.Body.position);Vector3 move=distance>(s.Ranged?12:2)?pveNavigation.Direction(p.Body.position,q.Body.position,q.Seat.id):Vector3.zero;input.X=move.x;input.Z=move.z;input.Shoot=distance<(s.Ranged?28:2.6f);
            if(s.Move==null&&Time>=p.Ready){if(distance>9&&!s.Ranged||distance<4&&s.Ranged)input.AbilityQ=true;else{var names=s.Ranged?NativeSirenRules.Ranged:NativeSirenRules.Melee;for(int i=0;i<4;i++)if((!s.Ready.TryGetValue(names[i],out float t)||Time>=t)&&(s.Ranged||distance<(i==2||i==3?7:3.5f))){input.SirenAbility=(byte)(i+1);input.Shoot=false;break;}}}return input;
        }
    }
    internal sealed class NativePveNavigation
    {
        private readonly NativeArena arena;private readonly bool[] blocked=new bool[63*55];private readonly Dictionary<int,(int target,int version,int[] steps)> flows=new Dictionary<int,(int,int,int[])>();private int version,propCount=-1;private float poll;private Collider[] overlaps=new Collider[32];
        public NativePveNavigation(NativeArena arena){this.arena=arena;}
        private static int Cell(Vector3 p)=>Mathf.Clamp(Mathf.RoundToInt(p.x)+31,0,62)+Mathf.Clamp(Mathf.RoundToInt(p.z)+27,0,54)*63;
        private static Vector3 Center(int i)=>new Vector3(i%63-31,.9f,i/63-27);
        private void Grid()
        {
            if(arena.Time<poll&&propCount>=0)return;poll=arena.Time+.5f;if(propCount==arena.Props.Count)return;propCount=arena.Props.Count;version++;flows.Clear();var scene=arena.Root.scene.GetPhysicsScene();
            for(int i=0;i<blocked.Length;i++){Vector3 at=Center(i);int n;while((n=scene.OverlapCapsule(at-Vector3.up*.2f,at+Vector3.up*.5f,.43f,overlaps,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))==overlaps.Length)Array.Resize(ref overlaps,overlaps.Length*2);blocked[i]=false;for(int k=0;k<n;k++)if(overlaps[k].bounds.max.y>.18f){blocked[i]=true;break;}}
        }
        private int[] Flow(Vector3 target,int id)
        {
            Grid();int cell=Cell(target);if(flows.TryGetValue(id,out var cached)&&cached.target==cell&&cached.version==version)return cached.steps;int[] steps=Enumerable.Repeat(int.MaxValue,blocked.Length).ToArray();var queue=new Queue<int>();steps[cell]=0;queue.Enqueue(cell);
            while(queue.Count>0){int at=queue.Dequeue(),x=at%63,z=at/63;foreach(int to in new[]{x>0?at-1:-1,x<62?at+1:-1,z>0?at-63:-1,z<54?at+63:-1})if(to>=0&&!blocked[to]&&steps[to]>steps[at]+1){steps[to]=steps[at]+1;queue.Enqueue(to);}}
            flows[id]=(cell,version,steps);return steps;
        }
        public float Distance(Vector3 from,Vector3 to,int id){var f=Flow(to,id);return f[Cell(from)]==int.MaxValue?1000+Vector3.Distance(from,to):f[Cell(from)];}
        public Vector3 Direction(Vector3 from,Vector3 to,int id)
        {
            var delta=to-from;delta.y=0;if(!arena.Root.scene.GetPhysicsScene().SphereCast(from+Vector3.up*.15f,.42f,delta.normalized,out _,delta.magnitude,NativeArena.WorldMask,QueryTriggerInteraction.Ignore))return delta.normalized;
            var f=Flow(to,id);int cell=Cell(from),best=cell,score=f[cell];for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){int tx=cell%63+x,tz=cell/63+z;if(tx<0||tx>62||tz<0||tz>54)continue;int next=tx+tz*63;if(x!=0&&z!=0&&(blocked[cell+x]||blocked[cell+z*63]))continue;if(!blocked[next]&&f[next]<score){best=next;score=f[next];}}
            Vector3 dir=Center(best)-from;dir.y=0;return dir.normalized;
        }
    }
}
