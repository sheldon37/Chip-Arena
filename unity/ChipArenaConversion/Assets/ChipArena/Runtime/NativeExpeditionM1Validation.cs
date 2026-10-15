#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateExpeditionM1Combat()
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Expedition M1 combat: "+why);}
            var layout=NativeExpeditionLayout.Parse(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text);
            layout.compatibility=NativeExpeditionLayout.Compatibility(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text,Content);
            var area=layout.areas.Single(a=>a.map==MapId);if(area.role!="instance")return;
            var data=JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text);
            ReplicaScene=root.scene;Match.Cooperative=true;Match.Teams=true;Match.Seats[0].team=0;
            Match.Seats[1].AuthorityBoss=true;Match.Seats[1].spec="siren";Match.Seats[1].team=1;Reset();
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10,round=1,set=1,cooperative=true,teams=true});
            var run=NativeExpedition.Create("native-m1-check",layout.world,"native-host","pistol",12345,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=1}).ToArray());
            var session=new ExpeditionSession(run,Content,_=>{});
            var director=new ExpeditionDirector(session,layout,_=>Vec(Local.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight),(a,b)=>ExpeditionVisible(a,b),100,100,60);
            try
            {
                Check(run.Enter(area.id),"enter fixture instance");director.Step();BindExpeditionCombat(director,area,data);
                var ticket=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(run,"native-host",session.Sources));
                Check(ExpeditionAreaReady.Accept(session,"native-host",ticket),"matching ready ticket accepted");
                Check(!director.CompleteAreaLoad((_,__)=>false)&&run.Snapshot().loading&&!run.Snapshot().members[0].safe,"failed placement stays frozen and unsafe");
                Check(director.CompleteAreaLoad((_,points)=>{Local.Body.position=Vec(points["native-host"].Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);return true;}),"placement completes");
                var add=SpawnPveAdd("shambler",true);Check(add!=null,"real encounter entity created");
                add.Pve.SpawnAt=add.Pve.ActiveAt=add.Pve.ExposedAt=Time-1;add.Pve.Struck=true;add.Collider.enabled=true;add.Body.isKinematic=false;
                HitPveAdd(add,Local,1000000,Vector3.zero,0,null,"projectile",0,false);
                Check(run.Snapshot().instances.Single(x=>x.id==area.id).progress==1,"confirmed combat death advances quota once");
                StepExpeditionCombat();Check(director.BossArrivals.Length==1&&Pve.Boss.Hp>0,"real boss registered");
                Damage(Pve.Boss,Local,50,Vector3.zero,0,false);float hp=Pve.Boss.Hp;StepExpeditionCombat();
                Check(hp<Pve.MaxHp&&Math.Abs(director.BossArrivals[0].hp-hp)<.001,"boss plate follows real damage");
                Damage(Pve.Boss,Local,1000000,Vector3.zero,0,false,true);StepExpeditionCombat();
                Check(run.Snapshot().instances.Single(x=>x.id==area.id).cleared&&director.BossArrivals[0].hp==0,"real boss death clears instance and plate");
                void LoadCurrent()
                {
                    var next=layout.Area(run.Snapshot().area);ExpeditionMap(next.map);BindExpeditionCombat(director,next,data);
                    var ready=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(run,"native-host",session.Sources));
                    Check(ExpeditionAreaReady.Accept(session,"native-host",ready),"new area ready accepted");
                    Check(director.CompleteAreaLoad((_,points)=>{Local.Body.position=Vec(points["native-host"].Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);return true;}),"new area placement");
                    director.Step();
                }
                Check(run.VoteExit("native-host",true),"leave cleared instance");session.Tick(.1);Check(director.Barrier.active,"return has a real barrier");LoadCurrent();
                for(int i=0;i<5;i++)session.Tick(.25);ExpeditionSyncClosures();
                var trigger=layout.Area(layout.world).bossTrigger;Local.Body.position=new Vector3(trigger.x,trigger.y+NativeLocomotionRules.HalfHeight,trigger.z);
                Check(director.TryBeginFinal(),"unlocked final trigger begins combat");StepExpeditionCombat();
                Damage(Pve.Boss,Local,1000000,Vector3.zero,0,false,true);StepExpeditionCombat();Check(run.Snapshot().phase==ExpeditionPhase.Victory,"real final death reaches victory");
                Check(run.VoteContinue("native-host",true),"continue vote");session.Tick(.1);Check(run.Snapshot().loop==1&&director.Barrier.active&&!run.Snapshot().members[0].safe,"Continue starts a new unsafe load barrier");LoadCurrent();
                Check(run.VoteExtraction("native-host",true),"sanctuary extraction vote");for(int i=0;i<41;i++)session.Tick(.25);
                Check(session.Run.Snapshot().phase==ExpeditionPhase.Ended&&session.Run.Snapshot().members[0].extracted,"durable extraction ends local run");
                Debug.Log("PASS Expedition M1 native ready/failed placement/real kill/boss damage/death/return/final/Continue/extraction callbacks");
            }
            finally{Expedition=null;Reset();}
        }
        public void ValidateExpeditionM1Geometry()
        {
            var layout=NativeExpeditionLayout.Parse(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text);
            var area=layout.areas.Single(a=>a.map==MapId);
            var points=area.slots.Concat(area.doors.SelectMany(d=>d.entries.Concat(d.returns))).Concat(area.bossPoints).Concat(area.doors.Select(d=>d.approach)).ToArray();
            var origin=points[0].Position;
            foreach(var profile in new[]{(.45f,1.8f),(NativeHordeKinds.HulkRadius,NativeHordeKinds.HulkHeight)})
                foreach(var p in points)
                {
                    if(!ExpeditionSupported(p.Position,profile.Item1,profile.Item2))throw new Exception("Expedition M1 unsupported placement "+MapId+" at "+p.x+","+p.y+","+p.z);
                    if(!ExpeditionReachable(origin,p.Position,profile.Item1,profile.Item2))throw new Exception("Expedition M1 unreachable placement "+MapId+" at "+p.x+","+p.y+","+p.z+" radius "+profile.Item1);
                }
            Debug.Log("PASS Expedition M1 native placement and navigation probes: "+MapId);
        }
    }
}
#endif
