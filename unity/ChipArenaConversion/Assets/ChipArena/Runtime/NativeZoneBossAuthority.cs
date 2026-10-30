using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public NativeZoneBossFrame ZoneBoss {get;private set;}
        public StationBossView StationZoneBossView()
        {
            var z=ZoneBoss;if(z?.archetype!=NativeZoneBossRules.Station)return StationBossView.None;
            foreach(var m in Minions)if(m.Id==z.actor){var p=m.Body.position;return NativeZoneBossViews.Station(z,p.x,p.y,p.z,m.Yaw,m.Hp,m.MaxHp,BossSongAt);}
            return StationBossView.None;
        }
        public IronworksBossView IronworksZoneBossView()
        {
            var z=ZoneBoss;if(z?.archetype!=NativeZoneBossRules.Ironworks)return IronworksBossView.None;
            foreach(var m in Minions)if(m.Id==z.actor){var p=m.Body.position;return NativeZoneBossViews.Ironworks(z,p.x,p.y,p.z,m.Yaw,m.Hp,m.MaxHp,BossSongAt);}
            return IronworksBossView.None;
        }
        private NativeMinion zoneBossActor;
        private readonly Dictionary<string,float> bossReady=new Dictionary<string,float>();
        private readonly HashSet<long> bossHits=new HashSet<long>();
        private readonly List<(Vector3 at,float hit,float end,float radius,float damage,int id,string spawn,string kind)> bossPools=new List<(Vector3,float,float,float,float,int,string,string)>();
        private readonly float[] bossPartDamage=new float[5];
        private float bossGap,bossHoldDamage,bossSelfDamage,bossChokeDamage,bossStaggerDamage,bossStaggerWindow,bossStaggerImmune,bossBehindAt=-100,bossShedAdds=-100;
        private bool bossLadle,bossPileup,bossActionFired;private int bossPoolId=300;
        private readonly NativeStationRoute stationRoute=new NativeStationRoute();
        private float stationRouteAt;
        private readonly Collider[] stationOverlap=new Collider[1];
        private BoxCollider bossBoard;private bool bossBarriers;
        private bool IsZoneBoss(NativeMinion m)=>m!=null&&NativeZoneBossRules.Known(m.Kind);
        private void ResetZoneBoss()
        {
            ZoneBoss=null;zoneBossActor=null;bossBoard=null;bossBarriers=false;stationRoute.Clear();stationRouteAt=0;bossReady.Clear();bossHits.Clear();bossPools.Clear();Array.Clear(bossPartDamage,0,5);
            bossGap=bossHoldDamage=bossSelfDamage=bossChokeDamage=bossStaggerDamage=bossStaggerWindow=bossStaggerImmune=0;bossBehindAt=bossShedAdds=-100;bossLadle=bossPileup=false;bossPoolId=300;
        }
        private void SpawnZoneBoss(string kind,Vector3 at,float yaw)
        {
            var obj=new GameObject(kind);obj.layer=11;obj.transform.SetParent(root.transform);obj.transform.position=at;obj.transform.rotation=Quaternion.Euler(0,yaw,0);
            var body=obj.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            // The minion capsule is a query handle; compound child colliders carry the actual body and weak points.
            var capsule=obj.AddComponent<CapsuleCollider>();capsule.radius=.01f;capsule.height=.02f;capsule.enabled=false;
            var m=new NativeMinion{Id=nextMinion++,Kind=kind,Owner=Pve.Boss,Body=body,Collider=capsule,Hp=Pve.MaxHp,MaxHp=Pve.MaxHp,Yaw=yaw,Until=float.PositiveInfinity};
            obj.AddComponent<NativeMinionBody>().Minion=m;Minions.Add(m);zoneBossActor=m;
            ZoneBoss=new NativeZoneBossFrame{actor=m.Id,archetype=kind,phaseAt=Time,parts=kind==NativeZoneBossRules.Ironworks?31:0};
            ConfigureZoneBossBody(m);NativeZoneBossRules.Start(ZoneBoss,"arrive",Time,-1,at.x,at.z,yaw);
            // The reserved Siren seat remains an attribution/intro anchor only, with no pilot collision or AI.
            Pve.Cap=8+2*Math.Max(0,Pilots.Count(p=>!p.Seat.AuthorityBoss&&p.Seat.connected)-1);Pve.Boss.Collider.enabled=false;Pve.Boss.Body.isKinematic=true;Pve.Boss.Seat.connected=false;Pve.Floor=0;
        }
        private void ConfigureZoneBossBody(NativeMinion m)
        {
            if(m.Body.transform.Find("Boss hull")!=null)return;
            void Part(string name,Vector3 center,Vector3 size,bool box=true)
            {
                var go=new GameObject(name);go.layer=11;go.transform.SetParent(m.Body.transform,false);go.transform.localPosition=center;go.AddComponent<NativeMinionBody>().Minion=m;
                if(box){var c=go.AddComponent<BoxCollider>();c.size=size;}else{var c=go.AddComponent<CapsuleCollider>();c.radius=size.x;c.height=size.y;}
            }
            if(m.Kind==NativeZoneBossRules.Station)
            {
                Part("Boss hull",new Vector3(0,2.9f,0),new Vector3(2.9f,2.7f,9.9f));Part("Stationmaster",new Vector3(0,5.35f,3),new Vector3(.55f,2.2f,0),false);
                Part("Headlamp",new Vector3(0,3.2f,5),new Vector3(.45f,.9f,0),false);Part("Heart",new Vector3(0,2.8f,-5),new Vector3(1,2,0),false);
                Part("Pantograph",new Vector3(0,4.8f,-1),new Vector3(1.8f,1.6f,1.8f));
                for(int i=0;i<6;i++){float side=i%2==0?-1:1,z=3-i/2*3;Part("Leg "+i,new Vector3(side*3.3f,1.65f,z),new Vector3(.45f,3.3f,0),false);Part("Leg upper "+i,new Vector3(side*2,2.7f,z),new Vector3(.5f,2,0),false);}
            }
            else
            {
                Part("Boss hull",new Vector3(0,3.5f,-.6f),new Vector3(2.7f,7,0),false);
                Part("Foreman",new Vector3(0,7.5f,-.6f),new Vector3(.55f,1.6f,0),false);
                for(int i=0;i<4;i++)Part("Tong "+i,new Vector3(i%2==0?-3.7f:3.7f,1.6f,i<2?2.8f:-2.8f),new Vector3(.45f,3.2f,0),false);
            }
            if(m.Kind==NativeZoneBossRules.Ironworks)foreach(var c in m.Body.GetComponentsInChildren<Collider>())if(c.name.StartsWith("Tong ",StringComparison.Ordinal))foreach(var p in Pilots)Physics.IgnoreCollision(c,p.Collider);
            m.Collider.center=new Vector3(0,m.Kind==NativeZoneBossRules.Station?2.9f:3.5f,0);m.Collider.direction=m.Kind==NativeZoneBossRules.Station?2:1;m.Collider.radius=m.Kind==NativeZoneBossRules.Station?1.3f:2.65f;m.Collider.height=m.Kind==NativeZoneBossRules.Station?9.7f:6.9f;m.Collider.enabled=true;
        }
        private bool BossOnce(int seat,int strike=0)=>bossHits.Add(((long)strike<<32)|(uint)seat);
        private void BossHurt(NativePilot p,float amount,Vector3 center,float knock=0,float stun=0,int strike=0,bool jump=false)
        {
            if(!ZonePilot(p)||Invulnerable(p)||jump&&p.Body.position.y-p.Collider.height*.5f>center.y+.6f||!BossOnce(p.Seat.id,strike))return;
            if(SceneLinecast(center+Vector3.up,p.Body.position,WorldMask))return;
            if(Time<p.BlockEnd){BlockProc(p,Pve.Boss,p.Body.position,"clash");return;}
            float actual=Damage(p,null,amount,p.Body.position-center,0,false,primary:false,rollAvoidable:true);
            if(actual>0){p.Knockback+=(p.Body.position-center).normalized*knock;p.StunEnd=Math.Max(p.StunEnd,Time+stun);}
        }
        private Vector3 BossLocal(Vector3 world)=>Quaternion.Euler(0,-zoneBossActor.Yaw,0)*(world-zoneBossActor.Body.position);
        private Vector3 BossWorld(float x,float z)=>zoneBossActor.Body.position+Quaternion.Euler(0,ZoneBoss.actionYaw,0)*new Vector3(x,0,z);
        private void BossRelease(bool thrown=false)
        {
            var z=ZoneBoss;if(z==null||z.holdTarget<0)return;var p=Pilots.Find(p=>p.Seat.id==z.holdTarget);
            z.holdUntil=Time;z.holdTarget=-1;bossHoldDamage=bossSelfDamage=0;
            if(thrown&&p!=null&&ZonePilot(p))BossHurt(p,14,zoneBossActor.Body.position,6,0,900);
        }
        private void BossStagger(string cause,float seconds)
        {
            var z=ZoneBoss;z.cancelAt=Time;z.staggerAt=Time;z.staggerSeconds=seconds;z.staggerCause=cause;z.liveUntil=Time;bossGap=Time+seconds;BossRelease();
        }
        private void BossStart(string id,NativePilot target)
        {
            var z=ZoneBoss;var m=zoneBossActor;NativeZoneBossRules.Start(z,id,Time,target?.Seat.id??-1,target?.Body.position.x??m.Body.position.x,target?.Body.position.z??m.Body.position.z,m.Yaw);
            z.laneX0=m.Body.position.x;z.laneZ0=m.Body.position.z;var aim=new Vector3(z.targetX-m.Body.position.x,0,z.targetZ-m.Body.position.z).normalized;
            z.actionYaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;
            z.laneX1=z.laneX0+aim.x*(z.archetype==NativeZoneBossRules.Station?22:14);z.laneZ1=z.laneZ0+aim.z*(z.archetype==NativeZoneBossRules.Station?22:14);
            if(id=="alteration"&&ZoneHazards!=null){var targets=Pilots.Where(ZonePilot).Take(4).ToArray();ZoneHazards.glassX=targets.Select(p=>p.Body.position.x).ToArray();ZoneHazards.glassZ=targets.Select(p=>p.Body.position.z).ToArray();ZoneHazards.glassAt=targets.Select(p=>z.hitAt).ToArray();}
            if(id=="doors")z.holdSide=BossLocal(target.Body.position).x<0?-1:1;
            z.variant=id=="call"?(z.phase==3?3:((z.actionId&1)==0?1:2)):id=="stomp"?NativeZoneBossRules.StompLeg(BossLocal(target.Body.position).x,BossLocal(target.Body.position).z):0;
            NativeZoneBossRules.Action(z.archetype,id,out var a);bossReady[id]=Time+a.Cooldown;bossGap=Time+z.duration+(z.phase==3?.5f:.8f);bossHits.Clear();bossActionFired=false;bossChokeDamage=0;
        }
        private bool BossAvailable(string id)=>!bossReady.TryGetValue(id,out float at)||Time>=at;
        private void StepZoneBoss()
        {
            var z=ZoneBoss;var m=zoneBossActor;if(z==null||m==null||Pve.Holding)return;
            Pve.Boss.Hp=m.Hp;Pve.Boss.Body.position=m.Body.position+Vector3.up*.9f;
            if(m.Hp<=0){if(z.deathAt<0){z.deathAt=Time;BossRelease();z.cancelAt=Time;bossPools.Clear();z.hazards=Array.Empty<NativeZoneBossHazardFrame>();foreach(var c in m.Body.GetComponentsInChildren<Collider>())c.enabled=false;}return;}
            foreach(var pair in m.Dots.ToArray()){var d=pair.Value;while(m.Hp>0&&d.Next<=Time&&d.Next<=d.Until){HitZoneBoss(m,Pilots.Find(p=>p.Seat.id==d.Source),(float)d.Dps*.25f,null,"field");d.Next+=.25;}if(Time>d.Until)m.Dots.Remove(pair.Key);}
            int phase=NativeZoneBossRules.Phase(z.archetype,m.Hp,m.MaxHp);
            if(phase>z.phase)
            {
                z.phase++;z.phaseAt=Time;Pve.Phase=z.phase;BossRelease();BossStagger("phase",z.archetype==NativeZoneBossRules.Station?3.5f:z.phase==2?3.2f:3.6f);
                NativeZoneBossRules.Start(z,z.archetype==NativeZoneBossRules.Station?"phase":z.phase==2?"shed":"break",Time,-1,m.Body.position.x,m.Body.position.z,m.Yaw);
                bossGap=Time+z.duration;bossActionFired=true;stationRoute.Clear();stationRouteAt=0;
                if(z.archetype==NativeZoneBossRules.Station){if(z.phase==2){z.barrierDownAt=Time;if(ZoneHazards!=null)ZoneHazards.trainAt=Time+40;}else{z.boardDownAt=z.roofOpenAt=Time;}}
                else if(z.phase==2){for(int i=0;i<4;i++)if((z.parts&(1<<i))!=0){z.partAt[i]=Time;z.parts&=~(1<<i);}bossShedAdds=Time+6;}
                else{z.parts&=15;z.partAt[4]=Time;if(ZoneHazards!=null)ZoneHazards.overflowAt=ZoneHazards.clock;}
            }
            SyncZoneBossArena();StepBossPools();
            if(bossShedAdds>=0&&Time>=bossShedAdds){bossShedAdds=-100;SpawnPveAddAt("slagback",BossWorld(-4,-2));SpawnPveAddAt("slagback",BossWorld(4,-2));}
            if(z.holdTarget>=0)
            {
                var held=Pilots.Find(p=>p.Seat.id==z.holdTarget);
                if(held==null||!ZonePilot(held))BossRelease();else if(Time>=z.holdUntil)BossRelease(z.archetype==NativeZoneBossRules.Ironworks);else
                {ZoneDrag(held,BossWorld(z.holdSide*2.1f,-.35f)-held.Body.position,2.6f);if(ZoneTouch(held.Seat.id,220,.25f))Damage(held,null,z.archetype==NativeZoneBossRules.Station?2:1.5f,Vector3.zero,0,false,primary:false);}
            }
            if(z.action=="departure"&&z.cancelAt<0&&z.lampBrokenAt<0&&Time<z.actionAt+(z.variant==1?.4f:.9f))
            {var locked=Pilots.Find(p=>p.Seat.id==z.targetSeat);if(locked!=null&&ZonePilot(locked)){var aim=locked.Body.position-m.Body.position;aim.y=0;aim.Normalize();z.targetX=locked.Body.position.x;z.targetZ=locked.Body.position.z;z.actionYaw=Mathf.Atan2(aim.x,aim.z)*Mathf.Rad2Deg;z.laneX1=z.laneX0+aim.x*22;z.laneZ1=z.laneZ0+aim.z*22;}}
            if(z.cancelAt<0&&z.action!=null&&Time>=z.hitAt&&Time<=z.actionAt+z.duration)StepBossAction();
            PublishBossPools();
            if(Time<bossGap)return;
            var target=Pilots.Where(ZonePilot).OrderBy(p=>(p.Body.position-m.Body.position).sqrMagnitude).FirstOrDefault();if(target==null)return;
            Vector3 local=BossLocal(target.Body.position);float distance=new Vector2(local.x,local.z).magnitude;bool station=z.archetype==NativeZoneBossRules.Station;
            string action=null;
            if(station)
            {
                if(z.phase==3&&BossAvailable("passengers"))action="passengers";
                else if(z.phase==3&&distance<10&&BossAvailable("terminus"))action="terminus";
                else if(z.phase>=2&&z.pantoBrokenAt<0&&Time>= (ZoneHazards?.leverUntil??-100)&&BossAvailable("live-rail"))action="live-rail";
                else if(Math.Abs(local.x)<4&&local.z>0&&distance<6&&BossAvailable(z.phase>=2?"shunt":"buffer"))action=z.phase>=2?"shunt":"buffer";
                else if(Math.Abs(local.x)<5&&Math.Abs(local.z)<3&&BossAvailable("doors"))action="doors";
                else if(distance>9&&BossAvailable("departure")&&(ZoneHazards==null||Time<ZoneHazards.trainAt-10||Time>ZoneHazards.trainAt+5))action="departure";
                else if(BossAvailable("alteration"))action="alteration";
            }
            else
            {
                if(z.phase>=2&&BossAvailable("call"))action="call";
                else if(BossAvailable("blow"))action="blow";
                else if(z.phase==3&&distance>=8&&distance<=16&&BossAvailable("charge"))action="charge";
                else if(z.phase>=2&&Pilots.Count(p=>ZonePilot(p)&&(p.Body.position-m.Body.position).sqrMagnitude<36)>=2&&BossAvailable("slam"))action="slam";
                else if(distance<4&&BossAvailable("stomp"))action="stomp";
                else if(distance<=7&&BossAvailable("grab"))action="grab";
                else if(local.z<0){if(bossBehindAt<0)bossBehindAt=Time;if(z.phase>=2&&Time-bossBehindAt>=3&&BossAvailable("deslag"))action="deslag";}
                else{bossBehindAt=-100;if(distance<=9&&BossAvailable("pour"))action="pour";}
            }
            if(action!=null){BossStart(action,target);return;}BossWalk(target,station);
        }
        private void BossWalk(NativePilot target,bool station)
        {
            var m=zoneBossActor;Vector3 at=m.Body.position,delta=target.Body.position-at;delta.y=0;
            if(station){StationBossWalk(target);return;}
            if(delta.magnitude<4)m.Yaw=Mathf.MoveTowardsAngle(m.Yaw,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,(ZoneBoss.phase==3?65:35)/60f);
            m.Body.rotation=Quaternion.Euler(0,m.Yaw,0);if(delta.magnitude<4)return;
            if(!station)
            {
                var goal=NativeConverterRoute.Goal(new System.Numerics.Vector3(at.x,at.y,at.z),new System.Numerics.Vector3(target.Body.position.x,target.Body.position.y,target.Body.position.z));
                delta=new Vector3(goal.X,goal.Y,goal.Z)-at;
                m.Yaw=Mathf.MoveTowardsAngle(m.Yaw,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,(ZoneBoss.phase==3?65:35)/60f);
                m.Body.rotation=Quaternion.Euler(0,m.Yaw,0);
            }
            Vector3 next=at+delta.normalized*Math.Min(delta.magnitude,(station?2.6f:1.6f)/60);
            if(station){next.x=Mathf.Clamp(next.x,-20,20);next.z=Mathf.Clamp(next.z,ZoneBoss.phase>=2?-6.5f:1.5f,5);}else if(!NativeConverterRoute.Contains(next.x,next.z))return;
            if(!BossPathBlocked(at,next,station))m.Body.position=next;
        }
        private bool StationPoseClear(Vector3 at,float yaw)
        {
            return PhysicsWorld.OverlapBox(at+Vector3.up*2.9f,new Vector3(1.5f,1.4f,5.05f),stationOverlap,
                Quaternion.Euler(0,yaw,0),WorldMask,QueryTriggerInteraction.Ignore)==0;
        }
        private bool StationTurnClear(Vector3 at,float from,float to)
        {
            float angle=Mathf.DeltaAngle(from,to);int steps=Math.Max(1,Mathf.CeilToInt(Math.Abs(angle)/2));
            for(int i=1;i<=steps;i++)if(!StationPoseClear(at,from+angle*i/steps))return false;
            return true;
        }
        private void StationBossWalk(NativePilot target)
        {
            var m=zoneBossActor;var at=m.Body.position;var delta=target.Body.position-at;delta.y=0;
            float yaw=Mathf.MoveTowardsAngle(m.Yaw,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,(ZoneBoss.phase==3?95:70)/60f);
            if(StationTurnClear(at,m.Yaw,yaw))
            {
                float old=m.Yaw;m.Yaw=yaw;
                // Keep a safe strafe route when turning would obstruct its next segment.
                if(stationRoute.Next(new System.Numerics.Vector3(at.x,at.y,at.z),out var nextGoal)&&BossPathBlocked(at,new Vector3(nextGoal.X,nextGoal.Y,nextGoal.Z),true))m.Yaw=old;
                m.Body.rotation=Quaternion.Euler(0,m.Yaw,0);
            }
            if(delta.magnitude<6){stationRoute.Clear();stationRouteAt=0;return;}
            if(Time>=stationRouteAt)
            {
                stationRouteAt=Time+1;
                stationRoute.Plan(new System.Numerics.Vector3(at.x,at.y,at.z),new System.Numerics.Vector3(target.Body.position.x,at.y,target.Body.position.z),ZoneBoss.phase,
                    (a,b)=>!BossPathBlocked(new Vector3(a.X,a.Y,a.Z),new Vector3(b.X,b.Y,b.Z),true));
            }
            if(!stationRoute.Next(new System.Numerics.Vector3(at.x,at.y,at.z),out var waypoint))return;
            delta=new Vector3(waypoint.X,waypoint.Y,waypoint.Z)-at;
            var next=at+delta.normalized*Math.Min(delta.magnitude,2.6f/60);
            if(BossPathBlocked(at,next,true)){stationRoute.Clear();stationRouteAt=Time+.1f;return;}
            m.Body.position=next;
        }
        private bool BossPathBlocked(Vector3 from,Vector3 to,bool station)
        {
            if(station&&!StationPoseClear(to,zoneBossActor.Yaw))return true;
            var delta=to-from;if(delta.sqrMagnitude<.000001f)return false;
            var half=station?new Vector3(1.5f,1.4f,5.05f):new Vector3(4.2f,4.1f,3.3f);
            return PhysicsWorld.BoxCast(from+Vector3.up*(station?2.9f:4.2f),half,delta.normalized,out _,Quaternion.Euler(0,zoneBossActor.Yaw,0),delta.magnitude,WorldMask,QueryTriggerInteraction.Ignore);
        }
        private void StepBossAction()
        {
            var z=ZoneBoss;var m=zoneBossActor;var at=m.Body.position;float t=Time-z.hitAt;bool first=!bossActionFired;bossActionFired=true;
            NativeZoneBossRules.Action(z.archetype,z.action,out var action);
            Vector3 center=new Vector3(z.laneX0,at.y,z.laneZ0),target=new Vector3(z.targetX,at.y,z.targetZ);
            if(first)
            {
                if(z.action=="live-rail"){z.liveAt=Time;z.liveUntil=Time+4;}
                if(z.action=="call"&&ZoneHazards!=null)
                {ZoneHazards.converterAt=ZoneHazards.clock;ZoneHazards.converterMask=z.variant;if(!bossLadle){bossLadle=true;bossScriptedLadle=true;try{SpawnPveAdd("ladleman",true,22);}finally{bossScriptedLadle=false;}}}
                if(z.action=="passengers")
                {
                    var ids=new List<int>();for(int i=0;i<4;i++){string kind=!bossPileup&&i==0?"pileup":i%2==0?"conductor":"runner";var add=SpawnPveAddAt(kind,BossWorld(i%2==0?-2:2,-.35f));if(add!=null){ids.Add(add.Id);if(kind=="pileup")bossPileup=true;}}
                    z.spillAt=Time;z.spillIds=ids.ToArray();
                }
                if(z.action=="blow")for(int i=0;i<(z.phase==3?28:14);i++)
                {NativeZoneBossRules.Droplet(z.actionId,i,z.phase,out float x,out float zz,out float delay);bossPools.Add((at+new Vector3(x,0,zz),z.hitAt+delay,z.hitAt+delay+1,1,16,bossPoolId++,i<(z.phase==3?4:2)?"cinderling":null,"blow"));}
                if(z.action=="deslag")for(int i=0;i<3;i++){float hit=z.hitAt+.9f+i*.4f;bossPools.Add((BossWorld((i-1)*2.5f,-8),hit,hit+6,1.4f,20,bossPoolId++,i==0?"slagback":null,"deslag"));}
                if(z.action=="pour")bossPools.Add((BossWorld(0,6),z.hitAt+1.6f,z.hitAt+5.6f,3,0,bossPoolId++,null,"pour"));
            }
            if(z.action=="departure"||z.action=="charge")
            {
                int chargeLeg=z.action=="departure"?NativeZoneBossRules.DepartureLeg(z.phase,t,action.Strike):t<=action.Strike?0:-1;if(chargeLeg<0)return;
                bool second=chargeLeg==1;
                float chargeTime=second?t-2:t;if(chargeTime>action.Strike)return;
                var end=new Vector3(z.laneX1,at.y,z.laneZ1);
                if(second){var swap=center;center=end;end=swap;}
                var next=Vector3.Lerp(center,end,Mathf.Clamp01(chargeTime/action.Strike));
                bool station=z.archetype==NativeZoneBossRules.Station;
                bool outside=station?Math.Abs(next.x)>24||next.z>6||next.z<(z.phase>=2?-7:0):!NativeConverterRoute.Contains(next.x,next.z);
                float chargeYaw=z.actionYaw+(second?180:0);
                if(station&&!StationTurnClear(at,m.Yaw,chargeYaw)){BossStagger("crash",2.2f);stationRouteAt=0;return;}
                float previousYaw=m.Yaw;m.Yaw=chargeYaw;
                if(outside||BossPathBlocked(at,next,station)){m.Yaw=previousYaw;BossStagger("crash",2.2f);stationRouteAt=0;return;}
                stationRoute.Clear();stationRouteAt=0;
                m.Body.position=next;m.Yaw=z.actionYaw+(second?180:0);m.Body.rotation=Quaternion.Euler(0,m.Yaw,0);
                foreach(var p in Pilots)if(ZonePilot(p)){var d=p.Body.position-next;d.y=0;if(d.magnitude<(station?2.1f:3.2f))BossHurt(p,34,next,station?9:6,strike:second?1:0);}
                if(!station&&ZoneTouch(m.Id,221,.2f))bossPools.Add((next,Time,Time+3,1.8f,0,bossPoolId++,null,"charge"));return;
            }
            if(z.action=="terminus"||z.action=="slam")
            {
                bool station=z.action=="terminus";float radius=Math.Min(station?10:9,t*(station?12:15));
                foreach(var p in Pilots)if(ZonePilot(p))
                {float d=Vector2.Distance(new Vector2(p.Body.position.x,p.Body.position.z),new Vector2(center.x,center.z));
                    if(station&&t<.15f&&d<3)BossHurt(p,40,center,0,0,1);
                    if(t<(station?10/12f:.6f)&&Math.Abs(d-radius)<.65f)BossHurt(p,station?18:24,center,0,.5f,2,true);
                    if(!station&&t>=.6f&&t<1.2f){var local=Quaternion.Euler(0,-z.actionYaw,0)*(p.Body.position-center);if(local.z>0&&local.z<6&&Math.Abs(local.x)<3)BossHurt(p,14,center,0,0,3);}
                }return;
            }
            if(z.action=="live-rail"&&Time<z.liveUntil&&Time>=(ZoneHazards?.leverUntil??-100)&&z.pantoBrokenAt<0)
            {foreach(var p in Pilots)if(ZonePilot(p)&&p.Body.position.y<2)for(int i=0;i<6;i++){var puddle=new Vector3(NativeStationBossArena.Puddles[i*3],0,NativeStationBossArena.Puddles[i*3+1]);if((new Vector2(p.Body.position.x-puddle.x,p.Body.position.z-puddle.z)).magnitude<2.2f)ZoneHazardPilot(p,230+i,2,.25f,"grid",stun:.35f);}return;}
            if(t>action.Strike)return;
            foreach(var p in Pilots)if(ZonePilot(p))
            {
                var d=Quaternion.Euler(0,-z.actionYaw,0)*(p.Body.position-center);float distance=new Vector2(d.x,d.z).magnitude;
                if(Math.Abs(p.Body.position.y-at.y)>3)continue;
                if(z.action=="buffer"||z.action=="shunt")
                {float reach=z.action=="buffer"?4.4f:5,arc=z.action=="buffer"?60:35;if(distance<=reach&&Math.Abs(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg)<=arc)BossHurt(p,z.action=="buffer"?20:24,center,z.action=="buffer"?4:10);}
                if(z.action=="pour"&&distance>=1.2f&&distance<=8&&Math.Abs(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg)<=26)BossHurt(p,22,center);
                if(z.action=="stomp"&&Vector2.Distance(new Vector2(p.Body.position.x,p.Body.position.z),new Vector2(target.x,target.z))<=1.9f)BossHurt(p,26,target,0,.6f);
                if(z.action=="doors"||z.action=="grab")
                {
                    var local=BossLocal(p.Body.position);bool inside=z.action=="doors"?Math.Abs(local.x-z.holdSide*3)<1.6f&&Math.Abs(local.z+.35f)<1.3f:Vector2.Distance(new Vector2(p.Body.position.x,p.Body.position.z),new Vector2(target.x,target.z))<=1.2f;
                    if(inside&&z.holdTarget<0&&bossHits.Count==0){float before=p.Hp;BossHurt(p,z.action=="doors"?6:10,center);if(p.Hp<before&&!Invulnerable(p)&&Time>=p.BlockEnd){z.holdTarget=p.Seat.id;z.holdUntil=Time+(z.action=="grab"?1.6f:Pilots.Count(ZonePilot)==1?1:1.5f);}}
                }
            }
        }
        private void PublishBossPools()
        {
            ZoneBoss.hazards=bossPools.Where(b=>Time<=b.end).Take(64).Select(b=>new NativeZoneBossHazardFrame{id=b.id,kind=b.kind,x=b.at.x,y=b.at.y,z=b.at.z,radius=b.radius,hitAt=b.hit,until=b.end}).ToArray();
        }
        private void StepBossPools()
        {
            for(int i=bossPools.Count-1;i>=0;i--)
            {
                var b=bossPools[i];if(Time<b.hit)continue;
                foreach(var p in Pilots)if(ZonePilot(p)&&Math.Abs(p.Body.position.y-b.at.y)<2&&Vector2.Distance(new Vector2(p.Body.position.x,p.Body.position.z),new Vector2(b.at.x,b.at.z))<=b.radius)
                {if(Time<b.hit+.1f&&b.damage>0)ZoneHazardPilot(p,b.id,b.damage,.2f,"hazard");if(Time<b.end)ZoneHazardPilot(p,b.id+100000,2,.25f,"burner");}
                if(b.spawn!=null&&Time>=b.hit+1.2f){if(b.spawn!="cinderling"||Minions.Count(m=>m.Hp>0&&m.Pve?.Stats.id=="cinderling")<6)SpawnPveAddAt(b.spawn,b.at);b.spawn=null;bossPools[i]=b;}
                if(Time>b.end&&b.spawn==null)bossPools.RemoveAt(i);
            }
        }
        private bool bossScriptedLadle;
        private NativeMinion SpawnPveAddAt(string kind,Vector3 position)
        {
            // Keep normal entry validation/caps, then commit a grounded world spawn in this area's PhysicsScene.
            if(!PhysicsWorld.Raycast(position+Vector3.up*3,Vector3.down,out var floor,6,WorldMask,QueryTriggerInteraction.Ignore)||floor.normal.y<.65f)return null;
            var m=SpawnPveAdd(kind,true);if(m==null)return null;var a=m.Pve;a.BossSpawn=true;
            m.Body.position=floor.point+Vector3.up*(m.Collider.height/2+.04f);a.ActiveAt=a.SpawnAt=a.ExposedAt=a.WalkAt=Time;a.Landed=a.DropLanded=true;a.First=false;a.Struck=true;a.Entry=new PveEntry{e=a.Entry.e,id=a.Entry.id,kind="boss",from=new PvePoint{x=m.Body.position.x,y=floor.point.y,z=m.Body.position.z},@exit=new PvePoint{x=m.Body.position.x,y=floor.point.y,z=m.Body.position.z}};
            m.Collider.enabled=true;m.Body.isKinematic=false;return m;
        }
        private float HitZoneBoss(NativeMinion m,NativePilot source,float damage,Vector3? point,string kind)
        {
            if(ZoneBoss==null||m.Hp<=0||Pve.Holding)return 0;var z=ZoneBoss;var local=BossLocal(point??m.Body.position);float multiplier=1;bool crit=false;
            if(z.archetype==NativeZoneBossRules.Station)
            {
                if(local.y>4.4f&&local.z>1){multiplier=z.phase==3?2.5f:2;crit=true;}
                else if(local.z< -4){multiplier=Time<z.staggerAt+z.staggerSeconds?2:1.5f;crit=true;}
                else if(local.y<1.8f)multiplier=.8f;else if(local.z>3)multiplier=.6f;else if(z.sideBroken!=0)multiplier=1.3f;
                bool front=source!=null&&BossLocal(source.Body.position).z>4,above=source!=null&&BossLocal(source.Body.position).y>4.3f;
                int part=front&&local.z>4&&Math.Abs(local.x)<.65f?0:local.y>4.3f&&local.z<1&&local.z> -2&&(above||z.action=="live-rail"&&Time<z.actionAt+z.duration)?1:-1;
                if(part>=0&&point.HasValue){bossPartDamage[part]+=damage;if(bossPartDamage[part]>=m.MaxHp*(part==0?.04f:.05f)&&(part==0?z.lampBrokenAt:z.pantoBrokenAt)<0){if(part==0)z.lampBrokenAt=Time;else z.pantoBrokenAt=Time;BossStagger("part",.8f);}}
            }
            else
            {
                bool grate=local.y>1&&local.y<3&&local.z< -1.5f&&(z.action=="blow"||z.action=="slam"||Time<z.staggerAt+z.staggerSeconds);
                if(grate){multiplier=2;crit=true;if(z.action=="blow"&&Time<z.hitAt){bossChokeDamage+=damage;if(bossChokeDamage>=m.MaxHp*.04f)BossStagger("choke",2.5f);}}
                else if(local.y>6){multiplier=1.5f;crit=true;}else if(Math.Abs(local.x)>3){multiplier=1.25f;crit=true;}
                else if(z.action=="pour"&&local.z>1.5f&&local.y>4){multiplier=1.5f;crit=true;}
                else
                {int part=Math.Abs(local.z)>Math.Abs(local.x)?local.z>0?0:3:local.x<0?1:2;bool intact=(z.parts&(1<<part))!=0;multiplier=intact?.4f:1.15f;if(intact){bossPartDamage[part]+=damage;if(bossPartDamage[part]>=m.MaxHp*.05f){z.parts&=~(1<<part);z.partAt[part]=Time;}}}
                if(crit&&Time>=bossStaggerImmune){if(Time-bossStaggerWindow>10){bossStaggerWindow=Time;bossStaggerDamage=0;}bossStaggerDamage+=damage*multiplier;z.stagger=Math.Min(1,bossStaggerDamage/(m.MaxHp*.08f));if(z.stagger>=1){BossStagger("kneel",3);bossStaggerImmune=Time+12;bossStaggerDamage=z.stagger=0;}}
            }
            if(z.holdTarget>=0&&point.HasValue&&Math.Abs(local.x)>1.4f)
            {if(source?.Seat.id==z.holdTarget)bossSelfDamage+=damage;else bossHoldDamage+=damage;if(bossHoldDamage>=(z.archetype==NativeZoneBossRules.Station?120:200)||z.archetype==NativeZoneBossRules.Station&&bossSelfDamage>=40)BossRelease();}
            float actual=Math.Min(m.Hp,Math.Max(0,damage*multiplier));m.Hp-=actual;m.LastHitAt=Time;Pve.Boss.Hp=m.Hp;PveDamaged(Pve.Boss,source,actual);Emit(EventKind.Hit,source,m.Id,point??m.Body.position,actual,kind);return actual;
        }
        private void SyncZoneBossArena()
        {
            var z=ZoneBoss;if(z?.archetype!=NativeZoneBossRules.Station)return;
            if(z.barrierDownAt>=0&&!bossBarriers)
            {
                bossBarriers=true;
                foreach(var prop in AllBlocks.Where(p=>p.Definition.id.EndsWith("barrier-1",StringComparison.Ordinal)||p.Definition.id.EndsWith("barrier-2",StringComparison.Ordinal)))
                {
                    NativeStationBossStage.BarrierPose(prop.transform.position.x<0?-1:1,1,out var pivot,out var rot,out var move);
                    var q=new Quaternion(rot.X,rot.Y,rot.Z,rot.W);var origin=new Vector3(pivot.X,pivot.Y,pivot.Z);var offset=new Vector3(move.X,move.Y,move.Z);
                    foreach(var c in prop.GetComponentsInChildren<Collider>())
                    {
                        if(c is BoxCollider box){var go=new GameObject("Station fallen barrier collision");go.layer=PropLayer;go.transform.SetParent(root.transform);go.transform.position=origin+q*(box.transform.TransformPoint(box.center)-origin)+offset;go.transform.rotation=q*box.transform.rotation;go.AddComponent<BoxCollider>().size=Vector3.Scale(box.size,box.transform.lossyScale);}
                        c.enabled=false;
                    }
                }
            }
            if(z.boardDownAt>=0&&bossBoard==null)
            {
                NativeStationBossStage.BoardPose(1,out var rot,out var move);var pivot=NativeStationBossStage.BoardPivot;
                var q=new Quaternion(rot.X,rot.Y,rot.Z,rot.W);var origin=new Vector3(pivot.X,pivot.Y,pivot.Z);
                var go=new GameObject("Station fallen departure board");go.layer=PropLayer;go.transform.SetParent(root.transform);
                go.transform.position=origin+q*(new Vector3(NativeStationBossArena.BoardX,NativeStationBossArena.BoardY,NativeStationBossArena.BoardZ)-origin)+new Vector3(move.X,move.Y,move.Z);go.transform.rotation=q;bossBoard=go.AddComponent<BoxCollider>();bossBoard.size=new Vector3(10.4f,4.4f,2.2f);
            }
            if(Replica||zoneBossActor==null||ZoneHazards==null||z.phase<2||Time<ZoneHazards.leverUntil)return;
            if(NativeZoneHazardFrame.TrainSweep(ZoneHazards.trainAt,Time-1d/60,Time,out double left,out double right))
            {var p=zoneBossActor.Body.position;float extent=Math.Abs(Mathf.Cos(zoneBossActor.Yaw*Mathf.Deg2Rad))*4.95f+Math.Abs(Mathf.Sin(zoneBossActor.Yaw*Mathf.Deg2Rad))*1.45f;
                if(Math.Abs(p.z+12.5f)<extent+2.5f&&p.x+5>=left&&p.x-5<=right&&z.expressHitAt<ZoneHazards.trainAt)
                {z.expressHitAt=Time;z.sideBroken|=p.z> -12.5f?1:2;zoneBossActor.Hp=Math.Max(0,zoneBossActor.Hp-zoneBossActor.MaxHp*.08f);BossStagger("express",3);}}
        }
    }
}
