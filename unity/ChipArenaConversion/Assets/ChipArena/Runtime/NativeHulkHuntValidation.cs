#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateHulkHunt()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Native Hulk hunt: "+why);}
            void Clock(float t)=>Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=t,fightAt=10,round=Match.Round,set=Match.Set,cooperative=true});
            var bounds=Map.bounds;var entries=Pve.Content.entries;var stageEntries=StageContent.entries;var p=Members().First();
            try
            {
                foreach(var add in Minions.ToArray())RemoveMinion(add,false);pveEntries.Clear();Clock(400);
                Map.bounds=new MapBounds{minX=-30,maxX=30,minZ=980,maxZ=1090,ceiling=20,killY=-6};
                AddProp(new ContentBlock{id="hulk-check-floor",material="foundation",role="static",pos=new V3(0,-.5f,1034),size=new V3(50,1,106),rotation=new ContentRotation()});
                Pve.Holding=false;Pve.Stage="wave";Pve.Wave=1;Pve.Cap=8;Pve.Rate=0;Pve.SpawnBudget=0;
                Pve.Content.entries=new[]{new PveEntry{e=0,id="hulk-check-entry",kind="drop",hulk=true,from=new PvePoint{z=1000},@exit=new PvePoint{z=1000}}};
                p.Seat.chips.Clear();p.Seat.spec="pistol";p.Weapon=Content.Weapon("pistol");p.Stats=NativeContentRules.Stats(Content,"pistol",p.Seat.chips);p.Stats["hp"]=1000;p.Stats["damageTaken"]=p.Stats["knockbackTaken"]=1;
                p.Seat.alive=p.Seat.connected=true;p.Down=null;p.Juggle=null;p.Kernel=null;p.Roll=null;p.ShieldUntil=p.RollEnd=p.BlockEnd=0;p.Dots.Clear();
                foreach(var q in Pilots.Where(q=>q!=p))q.Body.position=new Vector3(24,1,1080);
                var m=SpawnPveAdd("hulk",true,0);Check(m!=null,"spawn Hulk");var a=m.Pve;
                a.SpawnAt=a.WalkAt=a.ActiveAt=a.ExposedAt=Time-10;a.DamageScale=1;a.Target=p;a.ThinkAt=Time+100;
                m.Body.isKinematic=false;m.Collider.enabled=true;
                void Place(float x,float z){p.Hp=1000;p.Knockback=Vector3.zero;p.Body.position=new Vector3(x,p.Collider.height/2+.025f,z);p.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
                void ResetHulk(float now)
                {
                    ClearHulkCollisions(m);Clock(now);m.Body.position=new Vector3(0,m.Collider.height/2+.025f,1000);m.Body.linearVelocity=Vector3.zero;m.Yaw=0;
                    a.ChargeAt=a.ChargeEndAt=-100;a.ChargeTarget=a.ChargeEnd=0;a.ChargeReadyAt=a.ChargeProbeAt=a.RecoverAt=a.StaggerAt=0;a.Struck=true;a.Target=p;Physics.SyncTransforms();
                }
                ResetHulk(410);Place(0,1002.5f);float hit=ChooseHulkMove(m,p,1.1f);Check(a.Move==1&&Math.Abs(hit-.62f)<.001f,"punch choice and timing");
                m.LastAttack=Time;HulkMoveStrike(m);Check(Math.Abs(p.Hp-976)<.01f&&p.Knockback.z>11&&p.Knockback.y==2.5f,"punch volume/damage/shove");
                Place(1.5f,1002.5f);HulkMoveStrike(m);Check(p.Hp==1000,"outside punch misses");
                a.Move=2;Place(2,1001);HulkMoveStrike(m);Check(p.Hp==980&&p.Knockback.y==2,"swipe volume/damage");Place(0,998);HulkMoveStrike(m);Check(p.Hp==1000,"behind swipe misses");
                foreach(int move in new[]{1,2}){a.Move=move;Place(0,1002);p.BlockEnd=Time+1;HulkMoveStrike(m);Check(p.Hp==1000&&Math.Abs(p.Knockback.y-(move==1?1.25f:1))<.001f,"block half shove "+move);}p.BlockEnd=0;
                ResetHulk(420);Place(0,1050);Check(StepHulkHunt(m,p)&&a.Charging,"cross-map start");Place(5,1050);Clock(420.5f);StepHulkHunt(m,p);Check(m.Yaw>0&&!a.ChargeLocked,"wind tracks");
                CancelHulkWind(m,m.MaxHp*.25f);Check(!a.Charging&&Math.Abs(a.ChargeReadyAt-Time-4)<.001f,"stagger cancel cooldown");
                ResetHulk(430);Place(0,1050);StepHulkHunt(m,p);Clock(430.9f);StepHulkHunt(m,p);var heading=a.ChargeHeading;var point=a.ChargePoint;
                Place(1.5f,1050);
                for(int i=1;i<=560&&a.Charging;i++){Clock(430.9f+i/60f);StepHulkHunt(m,p);m.Body.position+=m.Body.linearVelocity/60f;Physics.SyncTransforms();}
                Check(a.ChargeEnd==3&&p.Hp==1000&&m.Body.position.z>1053,"cross-map run, sidestep missed, overshoot end");
                Check((a.ChargeHeading-heading).Length==0&&(a.ChargePoint-point).Length==0&&m.Yaw==0,"locked point and yaw");Check(Math.Abs(a.ChargeReadyAt-a.ChargeEndAt-10)<.001f,"cooldown from end");
                ResetHulk(450);Place(0,1001.2f);a.BeginCharge(449,p.Seat.id,Vec(m.Body.position),new V3(0,.9f,1050));StepHulkHunt(m,p);Check(a.ChargeEnd==1&&p.Hp==972&&p.Knockback.y==4.5f&&a.RecoverAt==Time+1,"pilot swept end");
                ResetHulk(460);Place(0,1001.2f);p.BlockEnd=Time+1;a.BeginCharge(459,p.Seat.id,Vec(m.Body.position),new V3(0,.9f,1050));StepHulkHunt(m,p);Check(a.ChargeEnd==1&&p.Hp==1000&&p.Knockback.y==2.25f,"charge block");p.BlockEnd=0;
                // Real collision adapters: each kind is passed through the same contact path as the run.
                string material=Content.materials.First(x=>!x.indestructible).id;
                var wall=AddProp(new ContentBlock{id="hulk-wall-check",material="foundation",role="static",pos=new V3(0,2,1020),size=new V3(6,4,.4f),rotation=new ContentRotation()});
                ResetHulk(470);a.ChargeHeading=new V3(0,0,1);Check(HulkBreak(m,wall,new Vector3(0,2,1019.8f))&&!wall.GetComponent<Collider>().enabled,"dynamic wall opens at contact");
                var breach=wardBreaches.Last();Check(breach.Dynamic&&breach.charged&&breach.actor==m.Id&&breach.breakAt==Time&&breach.normal.z==1,"dynamic record");
                foreach(string role in new[]{"", "masonry", "structural"})
                {var prop=AddProp(new ContentBlock{id="hulk-prop-"+role,material=material,role=role,pos=new V3(10,1,1020),size=new V3(1,2,1),hp=100,rotation=new ContentRotation()});Check(HulkBreak(m,prop,prop.transform.position)&&!Props.Contains(prop),"fractures "+role);}
                var panel=AddProp(new ContentBlock{id="hulk-panel",material="foundation",role="static",pos=new V3(10,2,1030),size=new V3(5,4,.4f),rotation=new ContentRotation()});
                var entry=new PveEntry{e=1,id="hulk-panel-entry",kind="breach",block="hulk-panel",from=new PvePoint{x=10,z=1028},@exit=new PvePoint{x=10,z=1032}};
                Pve.Content.entries=new[]{Pve.Content.entries[0],entry};StageContent.entries=Pve.Content.entries;
                Check(HulkBreakKind(panel)==2&&HulkBreak(m,panel,panel.transform.position),"interior entry panel");Check(wardBreaches.Last().actor==m.Id&&!wardBreaches.Last().Dynamic,"entry actor");
                entry.from.x=40;panel.GetComponent<Collider>().enabled=true;Check(HulkBreakKind(panel)==0,"boundary entrance protected");
                foreach(string id in new[]{"hulk-floor","hulk-shell","hulk-ceiling","hulk-stair","hulk-door-frame","hulk-machine"})
                {var prop=AddProp(new ContentBlock{id=id,material=material,pos=new V3(20,1,1040),size=new V3(1,2,1),hp=100,rotation=new ContentRotation()});Check(!HulkBreak(m,prop,prop.transform.position)&&prop.Health==100,"protected "+id);}
                // Sweep through real wall/prop/structure contacts without an end, including the .3 s slowdown.
                int caseIndex=0;
                foreach(string role in new[]{"static","","structural"})
                {
                    float now=474+caseIndex++;ResetHulk(now);Place(0,1050);
                    var obstacle=AddProp(new ContentBlock{id="charge-wall-contact-"+caseIndex,material=role=="static"?"foundation":material,role=role,pos=new V3(0,2,1001.05f),size=new V3(5,4,.2f),hp=100,rotation=new ContentRotation()});
                    a.BeginCharge(now-1,p.Seat.id,Vec(m.Body.position),Vec(p.Body.position));Physics.SyncTransforms();StepHulkHunt(m,p);
                    Check(a.Charging&&Math.Abs(m.Body.linearVelocity.z-3.6f)<.01f,"continues slowed through "+role);
                    Clock(now+.31f);StepHulkHunt(m,p);Check(a.Charging&&Math.Abs(m.Body.linearVelocity.z-6)<.01f,"speed restores "+role);
                    Check(obstacle==null||!obstacle.GetComponent<Collider>().enabled,"collision removed "+role);
                }
                ResetHulk(480);Place(0,1050);a.BeginCharge(479,p.Seat.id,Vec(m.Body.position),Vec(p.Body.position));
                var solid=AddProp(new ContentBlock{id="hulk-shell-stop",material="foundation",role="static",pos=new V3(0,2,1001),size=new V3(5,4,.2f),rotation=new ContentRotation()});Physics.SyncTransforms();StepHulkHunt(m,p);Check(a.ChargeEnd==2&&Math.Abs(a.StaggerAt-Time-1.6f)<.001f,"solid stun");solid.GetComponent<Collider>().enabled=false;
                ResetHulk(490);m.Body.position=new Vector3(0,m.Collider.height/2+.025f,1086.99f);a.BeginCharge(489,p.Seat.id,Vec(m.Body.position),new V3(0,.9f,1089));StepHulkHunt(m,p);Check(a.ChargeEnd==2,"floor gap stop");
                ResetHulk(510);Place(0,1050);a.Move=2;a.BeginCharge(509,p.Seat.id,Vec(m.Body.position),Vec(p.Body.position));StepHulkHunt(m,p);
                var frame=NativeFrameCodec.Clone(NetworkFrame());var mf=frame.minions.Single(x=>x.id==m.Id);Check(mf.move==2&&mf.chargeAt==509&&mf.chargeEndAt==-100&&mf.chargeTarget==p.Seat.id,"JsonUtility running state");
                var guest=new NativeArena(Content,new NativeMatch(Content),MapId){Replica=true,ReplicaScene=root.scene};
                try
                {
                    guest.ApplyFrame(frame,LocalId);var copy=guest.Minions.Single(x=>x.Id==m.Id);Check(copy.Pve.Move==2&&copy.Pve.ChargeTarget==p.Seat.id&&copy.Pve.ChargeAt==509&&copy.Yaw==m.Yaw,"late join");
                    EndHulkCharge(m,3,m.Body.position);var end=NativeFrameCodec.Clone(NetworkFrame());guest.ApplyFrame(end,LocalId);Check(guest.Minions.Single(x=>x.Id==m.Id).Pve.ChargeEnd==3,"end frame");
                    guest.ApplyFrame(frame,LocalId);Check(guest.Minions.Single(x=>x.Id==m.Id).Pve.ChargeEndAt==-100,"replay rewind restores running");
                }finally{guest.Dispose();}
                Debug.Log("PASS native Hulk moves, charge lock/cancel/cross-map/sidestep/ends, breakables, protection, JsonUtility/late join/replay");
            }
            finally{Pve.Content.entries=entries;StageContent.entries=stageEntries;Map.bounds=bounds;Reset();}
        }
    }
}
#endif
