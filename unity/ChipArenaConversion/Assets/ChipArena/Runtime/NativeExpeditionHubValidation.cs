using System;
using System.Linq;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        public void ValidateExpeditionHubAuthority()
        {
            if(MapId!="expedition-city")return;
            void Check(bool ok,string reason){if(!ok)throw new Exception("Expedition hub native: "+reason);}
            var layout=NativeExpeditionLayout.Parse(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text);
            var area=layout.Area(layout.world);var data=JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text);
            ReplicaScene=root.scene;Match.Cooperative=true;Match.Teams=true;Match.Seats[0].team=0;
            Match.Seats[1].AuthorityBoss=true;Match.Seats[1].spec="siren";Match.Seats[1].team=1;Reset();
            Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10,round=1,set=1,cooperative=true,teams=true});
            var run=NativeExpedition.Create("native-hub-check",layout.world,"native-host","pistol",12345,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=1}).ToArray());
            var session=new ExpeditionSession(run,Content,_=>{});session.Sources.Seat=_=>Local.Seat.id;
            var director=new ExpeditionDirector(session,layout,_=>Vec(Local.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight),(a,b)=>ExpeditionVisible(a,b),100,100,60);
            try
            {
                BindExpeditionCombat(director,area,data);Local.Body.position=new Vector3(0,.9f,0);Physics.SyncTransforms();StepHubGates();
                Check(HubOn&&hubDoors.Count==4&&hubTops.Count==4&&hubDoors.All(c=>c.enabled),"all four gates bind solid");
                Check(!Physics.GetIgnoreLayerCollision(28,11)&&Physics.GetIgnoreLayerCollision(28,ActorLayer),"permanent ward collides only with horde layer");
                Local.Input=new InputFrame{Attack=true,AbilityQ=true,Interact=false};HubInput(Local);Check(Local.Input.Attack,"entry grace before holster");
                Match.Tick(.24);HubInput(Local);Check(Local.Input.Attack,"not holstered at .24 seconds");
                Match.Tick(.02);HubInput(Local);Check(!Local.Input.Attack&&!Local.Input.AbilityQ,"holstered after .25 seconds");
                Check(HubCanTarget(Local,Local)&&!HubCanTarget(Local,null),"old aggro has grace; new inside target refused");
                for(int t=0;t<5;t++)Match.Tick(.25);Check(!HubCanTarget(Local,Local),"aggro expires after 1.5 seconds");
                var gate=hubAuthority.Layout.Gates[0];Local.Body.position=Vec(gate.Outside.Position)+Vector3.up*.9f;Local.Input=new InputFrame{Attack=true};HubInput(Local);
                Check(Local.Input.Attack&&!hubEntered.ContainsKey(Local.Seat.id),"weapon restored immediately outside");
                Check(HubInteract(Local)&&hubAuthority.View().Gates[0].Open,"outside interaction opens");
                session.Tick(.6);StepHubGates();Check(!hubDoors[0].enabled,"gate collider releases");
                Local.Body.position=new Vector3(gate.Closure.x,.9f,gate.Closure.z);Physics.SyncTransforms();HubInteract(Local);Check(hubAuthority.View().Gates[0].Open&&!hubDoors[0].enabled,"cannot close on capsule");
                Check(hubBlockedLinks.Count>0,"horde links through gates indexed");
                Local.Body.position=new Vector3(0,.9f,0);hubSpawnAt=Time;StepHubPopulation();
                int count=hubMobs.Count;Check(count==1&&Pve.Cap==4,"solo hub spawns one, cap four");
                StepHubPopulation();Check(hubMobs.Count==count,"no refill before three seconds");
                for(int i=0;i<6;i++){for(int t=0;t<12;t++)Match.Tick(.25);StepHubPopulation();}
                Check(hubMobs.Count<=4&&hubMobs.Count>0&&Minions.All(m=>m.Pve?.Stats.id!="hulk"),"bounded hub population without Hulks");
                var mob=Minions.First(m=>hubMobs.ContainsKey(m.Id));mob.Pve.ExposedAt=0;mob.Pve.Travelling=false;
                float hp=mob.Hp;Check(HitMinion(mob,Local,5,Vector3.zero,0)==0&&mob.Hp==hp,"inside damage cannot earn or hit outside");
                Local.Body.position=mob.Body.position+Vector3.right*2;
                Check(HitMinion(mob,Local,5,Vector3.zero,0,kind:"field")>0&&mob.Pve.HubCredit,"outside credited damage allowed");
                Local.Body.position=new Vector3(0,.9f,0);
                Check(HitMinion(mob,Local,5,Vector3.zero,0,kind:"field",from:mob.Body.position)>0,"outside-origin DoT survives shooter retreat");
                mob.Body.position=Local.Body.position;hubMaintainAt=Time;StepHubPopulation();Check(!Minions.Contains(mob),"horde intruder silently removed");
                Debug.Log("PASS Expedition hub native gate colliders/obstruction/holster/aggro/spawn cadence/cap/damage/DoT checks");
            }
            finally{Expedition=null;Reset();}
        }
    }
}
