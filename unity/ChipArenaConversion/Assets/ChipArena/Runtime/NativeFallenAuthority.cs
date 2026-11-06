using System;
using System.Linq;
using UnityEngine;
using N2=System.Numerics.Vector2;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public bool FallenPaused=>ZoneBoss?.fallen!=null&&(ZoneBoss.fallen.phaseChangeAt>=0||ZoneBoss.fallen.draftAt>=0||ZoneBoss.deathAt>=0);
        private static N2 Flat(Vector3 v)=>new N2(v.x,v.z);
        private void ConfigureFallenBattle()
        {
            var z=ZoneBoss;z.fallen=new NativeFallenRecord{seed=NativeBossBattle.Seed,started=Time,phaseStarted=Time,hush=Members().Select(p=>new NativeFallenSeat{id=p.Seat.id,x=p.Body.position.x,z=p.Body.position.z,hushAt=Time}).ToArray()};
            z.action="arrive";z.duration=NativeFallenPilots.CutsceneEnd;z.hitAt=z.actionAt+z.duration;Pve.HoldDuration=Pve.HoldLeft=z.duration;Pve.Rate=0;Pve.SpawnBudget=0;bossGap=Time+1;
        }
        private void FallenDied(NativePilot p)
        {
            var s=ZoneBoss?.fallen?.hush.FirstOrDefault(x=>x.id==p.Seat.id);if(s==null)return;
            s.lives=Math.Max(0,s.lives-1);s.respawnAt=s.lives>0?Time+3:-100;s.reviver=-1;s.reviveAt=-100;
        }
        private Vector3 FallenRespawn(NativePilot p)
        {
            var boss=zoneBossActor.Body.position;Vector3 best=new Vector3(0,.9f,-45);float distance=-1;
            for(int i=0;i<16;i++){var d=NativeFallenRules.Dir(i*22.5f+p.Seat.id*7);var q=new Vector3(d.X*55,.9f,d.Y*55);float score=(q-boss).sqrMagnitude;if(score>distance){best=q;distance=score;}}
            return best;
        }
        private void FallenLives()
        {
            var f=ZoneBoss.fallen;
            foreach(var s in f.hush)
            {
                var p=Pilots.Find(p=>p.Seat.id==s.id);if(p==null||!p.Seat.connected)continue;
                if(p.Hp<=0&&s.lives>0&&s.respawnAt>=0&&Time>=s.respawnAt){RevivePve(p,FallenRespawn(p),true);s.respawnAt=-100;s.hushAt=Time;s.x=p.Body.position.x;s.z=p.Body.position.z;}
                if(p.Hp>0||s.lives>0){s.reviver=-1;s.reviveAt=-100;continue;}
                var giver=Members().Where(q=>q.Hp>0&&q.Input.InteractHeld&&Vector3.Distance(q.Body.position,p.Body.position)<=3&&f.hush.Any(h=>h.id==q.Seat.id&&h.lives>1)&&!f.hush.Any(h=>h!=s&&h.reviver==q.Seat.id)).OrderBy(q=>q.Seat.id).FirstOrDefault();
                if(giver==null){s.reviver=-1;s.reviveAt=-100;continue;}
                if(s.reviver!=giver.Seat.id||giver.LastDamage>=s.reviveAt){s.reviver=giver.Seat.id;s.reviveAt=Time;}
                if(Time-s.reviveAt>=4+(NativeFallenRules.Chip(f,"unforgiven")?2:0))
                {f.hush.First(h=>h.id==giver.Seat.id).lives--;s.lives=1;RevivePve(p,null,true);s.reviver=-1;s.reviveAt=-100;s.hushAt=Time;}
            }
        }
        private float HitFallenBoss(NativeMinion m,NativePilot source,float damage,Vector3? point,string kind)
        {
            if(m.Hp<=0||Pve.Holding||FallenPaused)return 0;
            float floor=ZoneBoss.phase<4?m.MaxHp*NativeFallenBoss.PhaseFrom[ZoneBoss.phase]:0;
            float actual=Math.Min(Math.Max(0,m.Hp-floor),Math.Max(0,damage));m.Hp-=actual;m.LastHitAt=Time;Pve.Boss.Hp=m.Hp;PveDamaged(Pve.Boss,source,actual);Emit(EventKind.Hit,source,m.Id,point??m.Body.position,actual,kind);
            if(ZoneBoss.phase<4&&m.Hp<=floor+.001f)ZoneBoss.fallen.phaseChangeAt=NativeFallenRules.NextBar(Time,ZoneBoss.fallen.phaseStarted);
            return actual;
        }
        private void FallenHurt(NativePilot p,float damage,bool lethal=false,int strike=0,bool jump=false)
        {
            if(p.Hp<=0||!p.Seat.connected||Time<p.ShieldUntil||FallenPaused||!BossOnce(p.Seat.id,strike))return;
            if(jump&&p.Body.position.y-p.Collider.height*.5f>.65f)return;
            if(!lethal&&Time<p.BlockEnd)return;
            Damage(p,null,lethal?p.Hp:damage*(float)(Pve.Boss.Stats["damage"]/NativePveRules.BossDamage(1)),Vector3.zero,0,false,forced:lethal,primary:false,rollAvoidable:!lethal,stageLethal:lethal,causeKind:NativeFallenBoss.Archetype,attackerMinion:zoneBossActor);
        }
        private void FallenPhase()
        {
            var z=ZoneBoss;var f=z.fallen;
            if(f.phaseChangeAt>=0&&Time>=f.phaseChangeAt)
            {
                z.phase++;Pve.Phase=z.phase;z.phaseAt=Time;f.phaseChangeAt=-100;f.draftAt=Time;f.draftGranted=false;f.resumeAt=Time+NativeFallenRules.DraftSeconds;f.chipOffer=NativeFallenRules.Chips(f.seed,z.phase);z.action=null;z.actionAt=z.hitAt=-100;z.duration=0;
                foreach(var m in Minions.Where(m=>m.Kind.StartsWith("fallen-",StringComparison.Ordinal)).ToArray())RemoveMinion(m,false);
                Bullets.Clear();foreach(var p in Members()){p.Dots.Clear();p.Knockback=Vector3.zero;}
            }
            if(f.draftAt<0)return;
            if(!f.draftGranted&&Time>=f.draftAt+NativeFallenRules.RevealSeconds){f.draftGranted=true;foreach(var p in Members().Where(p=>p.Hp>0&&p.Seat.connected))GrantPvePick(p,"rare",true,"boss");}
            if(Time<f.resumeAt)return;
            foreach(var row in Pve.Rows.Values)if(row.Open?.Offers?.Count>0)ChoosePve(row.Pilot,row.Open.Id,row.Open.Offers[0]);
            f.draftAt=f.resumeAt=-100;f.phaseStarted=Time;bossGap=Time+1;bossReady.Clear();bossHits.Clear();foreach(var s in f.hush){s.hushAt=Time;var p=Pilots.Find(p=>p.Seat.id==s.id);if(p!=null){s.x=p.Body.position.x;s.z=p.Body.position.z;}}
        }
        private void FallenStart(string id,NativePilot target)
        {
            var z=ZoneBoss;var f=z.fallen;var m=NativeFallenBoss.Move(id);var at=zoneBossActor.Body.position;var goal=target.Body.position;goal.y=0;
            NativeZoneBossRules.Start(z,id,Time,target.Seat.id,goal.x,goal.z,Mathf.Atan2(goal.x-at.x,goal.z-at.z)*Mathf.Rad2Deg);
            z.laneX0=at.x;z.laneZ0=at.z;z.laneX1=goal.x;z.laneZ1=goal.z;z.hitAt=Time+(id=="thrones"?1.2f:NativeFallenRules.Wind(m,f));z.duration=z.hitAt-Time+(id=="severance"?5.5f:id=="fall"?3.2f:id=="thrones"?1.2f:m.Strike+m.Follow+m.Recover);
            if(id=="vow"){var d=(goal-at).normalized;var end=at+d*Math.Min(35,Vector3.Distance(goal,at));z.laneX1=end.x;z.laneZ1=end.z;}
            f.patternSeed=(int)(NativePveRandom.Hash((uint)f.seed,z.actionId)%100000);f.pools=id=="eclipse"?Members().Where(p=>p.Hp>0).Select(p=>{var q=NativeFallenRules.Pool(Flat(p.Body.position),f.patternSeed+p.Seat.id*29);return new V3(q.X,0,q.Y);}).ToArray():Array.Empty<V3>();f.verdicts=id=="last"?new[]{z.actionYaw,z.actionYaw+60,z.actionYaw-60}:id=="verdict"?new[]{z.actionYaw}:Array.Empty<float>();
            bossReady[id]=Time+(id=="thrones"?30:m.Cooldown)*(NativeFallenRules.Chip(f,"no-mercy")&&(id=="last"||id=="verdict")?.8f:1);if(m.Kills)f.lastOhk=z.hitAt;bossGap=Time+z.duration+.6f;bossHits.Clear();bossActionFired=false;
        }
        private void FallenAttack()
        {
            var z=ZoneBoss;var f=z.fallen;var actor=zoneBossActor;float t=Time-z.hitAt;if(z.action==null||Time>z.actionAt+z.duration)return;
            Vector3 from=new Vector3(z.laneX0,0,z.laneZ0),to=new Vector3(z.laneX1,0,z.laneZ1);
            if(z.action=="vow"&&t>=0)actor.Body.position=Vector3.Lerp(from,to,Mathf.Clamp01(t/.5f));
            if(z.action=="descent"&&t>=0)actor.Body.position=to;
            actor.Yaw=z.actionYaw;actor.Body.rotation=Quaternion.Euler(0,actor.Yaw,0);
            if(t<0)return;
            if(!bossActionFired){bossActionFired=true;if(z.action=="choir"||z.action=="penance"||z.action=="thrones")FallenSummon(z.action);}
            foreach(var p in Members().Where(p=>p.Hp>0&&p.Seat.connected))
            {
                N2 q=Flat(p.Body.position),a=Flat(from),b=Flat(to),d=q-a;float r=d.Length();float reach=NativeFallenRules.Chip(f,"long-reach")?1.2f:1,heavy=NativeFallenRules.Chip(f,"heavy-hand")?1.15f:1;
                switch(z.action)
                {
                    case "cleave":if(t<.8f&&r<=8*reach&&(r<.01f||N2.Dot(N2.Normalize(d),NativeFallenRules.Dir(z.actionYaw))>=.25f))FallenHurt(p,26*heavy,strike:(int)(t/.3f));break;
                    case "vow":if(t<.5f&&N2.Distance(q,Flat(actor.Body.position))<=3)FallenHurt(p,34);break;
                    case "descent":if(t<.2f&&N2.Distance(q,b)<=9)FallenHurt(p,38);break;
                    case "sweep":if(t<.5f&&r<=28*reach)FallenHurt(p,34*heavy,jump:true);break;
                    case "severance":for(int i=0;i<(NativeFallenRules.Chip(f,"second-wing")?2:1);i++){float age=t-i*.6f;if(age>=0&&Math.Abs(r-(34*age+3))<=1.2f&&N2.Dot(N2.Normalize(d),NativeFallenRules.Dir(z.actionYaw))>=.5f)FallenHurt(p,30,strike:i,jump:true);}break;
                    case "verdict":case "last":if(t<.2f&&f.verdicts.Any(y=>NativeFallenRules.Line(q,new N2(z.targetX,z.targetZ),y)<=1.5f))FallenHurt(p,0,true);break;
                    case "eclipse":if(t<.2f&&!NativeFallenRules.SafePool(q,f.pools))FallenHurt(p,0,true);break;
                    case "fall":if(t<.2f&&NativeFallenRules.InColumn(q,f.patternSeed))FallenHurt(p,0,true);break;
                    case "rain":if(t<2.4f)for(int lane=0;lane<8;lane++){float angle=f.patternSeed*37+lane*45+(lane%2)*19;var dd=NativeFallenRules.Dir(angle);var c=a*.5f+new N2(dd.Y,-dd.X)*(lane-3.5f)*7;if(NativeFallenRules.Line(q,c,angle)<=1.25f)FallenHurt(p,18,strike:lane);}break;
                }
            }
        }
        private void StepFallenBattle()
        {
            var z=ZoneBoss;var f=z.fallen;var m=zoneBossActor;Pve.Rate=0;Pve.SpawnBudget=0;Pve.Boss.Hp=m.Hp;Pve.Boss.Body.position=m.Body.position+Vector3.up;
            if(m.Hp<=0){if(z.deathAt<0){z.deathAt=Time;z.action=null;z.actionAt=z.hitAt=-100;z.duration=0;foreach(var c in m.Body.GetComponentsInChildren<Collider>())c.enabled=false;foreach(var add in Minions.Where(a=>a.Kind.StartsWith("fallen-",StringComparison.Ordinal)).ToArray())RemoveMinion(add,false);Bullets.Clear();}if(Time-z.deathAt>=NativeFallenRules.DeathSeconds){Pve.Outcome="victory";Pve.Stage="ended";Match.FinishCooperative(true);}return;}
            foreach(var d in m.Dots.Values.ToArray())while(m.Hp>0&&d.Next<=Time&&d.Next<=d.Until){HitMinion(m,Pilots.Find(p=>p.Seat.id==d.Source),(float)d.Dps*.25f,Vector3.zero,0,kind:"field");d.Next+=.25;}
            FallenLives();if(!Members().Any(p=>p.Seat.connected&&f.hush.Any(s=>s.id==p.Seat.id&&s.lives>0))){BeginPveHold("defeat");return;}
            FallenPhase();if(FallenPaused)return;
            if(Time-f.started>=720){bossHits.Clear();foreach(var p in Members()){p.ShieldUntil=0;FallenHurt(p,0,true);var s=f.hush.First(x=>x.id==p.Seat.id);s.lives=0;s.respawnAt=-100;}BeginPveHold("defeat");return;}
            foreach(var s in f.hush)
            {
                var p=Pilots.Find(p=>p.Seat.id==s.id);if(p==null||p.Hp<=0||!p.Seat.connected)continue;var q=Flat(p.Body.position);float radius=q.Length();
                if(radius>80||p.Body.position.y<-.5f||p.Body.position.y>30){var dir=radius>0?q/radius:new N2(0,-1);p.Body.position=new Vector3(dir.X*72,.9f,dir.Y*72);p.Body.linearVelocity=Vector3.zero;Damage(p,null,15,Vector3.zero,0,false,primary:false,causeKind:NativeFallenBoss.Archetype);}
                else if(radius>74){var v=p.Body.linearVelocity;p.Body.linearVelocity=new Vector3(v.x*.94f,v.y,v.z*.94f);}
                if(N2.Distance(q,new N2(s.x,s.z))>=3){s.x=q.X;s.z=q.Y;s.hushAt=Time;}
                if(Time-s.hushAt>=NativeFallenRules.HushSeconds(f,z.phase,radius)){Damage(p,null,40,Vector3.zero,0,false,primary:false,causeKind:NativeFallenBoss.Archetype);s.hushAt=Time;}
            }
            FallenAttack();FallenAdds();
            if(z.action==null||Time>z.actionAt+z.duration){var near=Members().Where(p=>p.Hp>0&&p.Seat.connected).OrderBy(p=>(p.Body.position-m.Body.position).sqrMagnitude).FirstOrDefault();if(near!=null){var toward=near.Body.position-m.Body.position;toward.y=0;if(toward.magnitude>5)m.Body.position+=toward.normalized*(9f/60);}}
            if(Time<bossGap)return;
            var target=Members().Where(p=>p.Hp>0&&p.Seat.connected).OrderByDescending(p=>(p.Body.position-m.Body.position).sqrMagnitude).FirstOrDefault();if(target==null)return;
            var choices=NativeFallenBoss.Moves.Where(a=>a.Phase<=z.phase&&BossAvailable(a.Id)&&(!a.Kills||Time-f.lastOhk>=14)).ToArray();
            string id=z.phase==4&&BossAvailable("thrones")?"thrones":choices.Length>0?choices[(int)(NativePveRandom.Hash((uint)f.seed,z.actionId)%choices.Length)].Id:"cleave";
            if(id=="cleave"||id=="sweep"){var near=Members().Where(p=>p.Hp>0).OrderBy(p=>(p.Body.position-m.Body.position).sqrMagnitude).First();target=near;}
            FallenStart(id,target);
        }
        private void FallenSummon(string action)
        {
            string kind=action=="choir"?"cantor":action=="penance"?"penitent":"throne";int per=kind=="cantor"?2:kind=="penitent"?3:1,count=kind=="throne"?(PartySize+1)/2:per*PartySize;if(NativeFallenRules.Chip(ZoneBoss.fallen,"full-choir"))count++;
            var targets=Members().Where(p=>p.Hp>0).ToArray();if(targets.Length==0)return;
            for(int i=0;i<count&&Minions.Count(m=>m.Kind.StartsWith("fallen-",StringComparison.Ordinal))<16;i++)
            {
                var p=targets[i%targets.Length];var d=NativeFallenRules.Dir(i*137+ZoneBoss.actionId*29);var q=Flat(p.Body.position)+d*11;if(q.Length()>70)q=N2.Normalize(q)*70;
                var go=new GameObject("fallen-"+kind);go.layer=11;go.transform.SetParent(root.transform);go.transform.position=new Vector3(q.X,kind=="throne"?6:1,q.Y);var body=go.AddComponent<Rigidbody>();body.isKinematic=true;var col=go.AddComponent<CapsuleCollider>();col.height=kind=="cantor"?3:1.5f;col.radius=.65f;
                var add=new NativeMinion{Id=nextMinion++,Kind="fallen-"+kind,Owner=Pve.Boss,Body=body,Collider=col,Hp=kind=="throne"?180:45,MaxHp=kind=="throne"?180:45,Until=Time+45,NextAttack=Time+2};go.AddComponent<NativeMinionBody>().Minion=add;Minions.Add(add);
            }
        }
        private void FallenAdds()
        {
            foreach(var m in Minions.Where(a=>a.Kind.StartsWith("fallen-",StringComparison.Ordinal)).ToArray())
            {
                foreach(var dot in m.Dots.Values.ToArray())while(m.Hp>0&&dot.Next<=Time&&dot.Next<=dot.Until){HitMinion(m,Pilots.Find(p=>p.Seat.id==dot.Source),(float)dot.Dps*.25f,Vector3.zero,0,kind:"field");dot.Next+=.25;}
                if(Time>=m.Until||m.Hp<=0){RemoveMinion(m,false);continue;}var target=Members().Where(p=>p.Hp>0&&p.Seat.connected).OrderBy(p=>(p.Body.position-m.Body.position).sqrMagnitude).FirstOrDefault();if(target==null)continue;
                var d=target.Body.position-m.Body.position;d.y=0;m.Yaw=Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg;
                if(m.Kind=="fallen-penitent"){m.Body.position+=d.normalized*Math.Min(d.magnitude,7f/60);if(d.magnitude<2){target.Body.linearVelocity*=.96f;if(target.Roll!=null){RemoveMinion(m,false);continue;}}}
                if(Time>=m.NextAttack&&(m.Kind!="fallen-penitent"||d.magnitude<2))
                {Damage(target,null,(m.Kind=="fallen-throne"?18:m.Kind=="fallen-cantor"?8+12*Math.Min(1,(Time-(m.Until-45))/10):6)*(float)(Pve.Boss.Stats["damage"]/NativePveRules.BossDamage(1)),Vector3.zero,0,false,primary:false,attackerMinion:m,causeKind:m.Kind);m.LastAttack=Time;m.NextAttack=Time+1.2f;}
            }
        }
    }
}
