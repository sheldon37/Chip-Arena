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
                foreach(var shut in hubAuthority.Layout.Gates)
                {
                    int index=Array.IndexOf(hubAuthority.Layout.Gates,shut);var outward=Quaternion.Euler(0,shut.Yaw,0)*Vector3.forward;
                    Local.Body.position=new Vector3(shut.Closure.x,.9f,shut.Closure.z)+outward*(shut.Closure.sz/2+Local.Collider.radius+.01f);Physics.SyncTransforms();
                    Check(!HubOccupied(shut.Closure),"pressing a capsule against shut gate is not actual overlap");StepHubGates();
                    Check(!hubAuthority.View().Gates[index].Open&&hubDoors[index].enabled&&hubTops[index].enabled,"contact cannot open gate or disable its colliders");
                    Local.Body.position=new Vector3(shut.Closure.x,.9f,shut.Closure.z);Physics.SyncTransforms();Check(HubOccupied(shut.Closure),"capsule inside opening is an actual overlap");StepHubGates();
                    Check(!hubAuthority.View().Gates[index].Open&&hubDoors[index].enabled,"even penetration cannot auto-open an already solid gate");
                    Local.Body.position=new Vector3(shut.Closure.x,shut.Closure.y+shut.Closure.sy/2+2,shut.Closure.z);Physics.SyncTransforms();Check(!HubOccupied(shut.Closure),"capsule above closure does not occupy it");
                }
                Local.Body.position=new Vector3(0,.9f,0);Physics.SyncTransforms();
                Check(!Physics.GetIgnoreLayerCollision(28,11)&&Physics.GetIgnoreLayerCollision(28,ActorLayer),"permanent ward collides only with horde layer");
                Local.Input=new InputFrame{Attack=true,AbilityQ=true,Interact=false};HubInput(Local);Check(Local.Input.Attack,"entry grace before holster");
                Match.Tick(.24);HubInput(Local);Check(Local.Input.Attack,"not holstered at .24 seconds");
                Match.Tick(.02);HubInput(Local);Check(!Local.Input.Attack&&!Local.Input.AbilityQ,"holstered after .25 seconds");
                Check(HubCanTarget(Local,Local)&&!HubCanTarget(Local,null),"old aggro has grace; new inside target refused");
                for(int t=0;t<5;t++)Match.Tick(.25);Check(!HubCanTarget(Local,Local),"aggro expires after 1.5 seconds");
                var gate=hubAuthority.Layout.Gates[0];Local.Body.position=Vec(gate.Outside.Position)+Vector3.up*.9f;Local.Input=new InputFrame{Attack=true};HubInput(Local);
                Check(Local.Input.Attack&&!hubEntered.ContainsKey(Local.Seat.id),"weapon restored immediately outside");
                Check(HubInteract(Local)&&hubAuthority.View().Gates[0].Open,"outside interaction opens");
                session.Tick(.2);StepHubGates();Check(hubDoors[0].enabled,"opening remains solid before release");
                HubInteract(Local);Check(!hubAuthority.View().Gates[0].Open&&hubDoors[0].enabled,"early opening reversal stays solid");
                HubInteract(Local);session.Tick(.6);StepHubGates();Check(!hubDoors[0].enabled,"gate collider releases");
                HubInteract(Local);session.Tick(.2);StepHubGates();Check(!hubDoors[0].enabled,"closing remains released before block");
                HubInteract(Local);Check(hubAuthority.View().Gates[0].Open&&!hubDoors[0].enabled,"early closing reversal stays released");
                Local.Body.position=new Vector3(gate.Closure.x,.9f,gate.Closure.z);Physics.SyncTransforms();HubInteract(Local);Check(hubAuthority.View().Gates[0].Open&&!hubDoors[0].enabled,"cannot close on capsule");
                Check(hubBlockedLinks.Count>0,"horde links through gates indexed");
                Local.Body.position=new Vector3(0,.9f,0);hubSpawnAt=Time;StepHubPopulation();
                int count=hubMobs.Count;Check(count==1&&Pve.Cap==4,"solo hub spawns one, cap four");
                StepHubPopulation();Check(hubMobs.Count==count,"no refill before three seconds");
                for(int i=0;i<6;i++){for(int t=0;t<12;t++)Match.Tick(.25);StepHubPopulation();}
                Check(hubMobs.Count<=4&&hubMobs.Count>0&&Minions.All(m=>m.Pve?.Stats.id!="hulk"),"bounded hub population without Hulks");
                var mob=Minions.First(m=>hubMobs.ContainsKey(m.Id));mob.Pve.ExposedAt=0;mob.Pve.Travelling=false;
                ValidateHubPerception(mob);
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
        private void ValidateHubPerception(NativeMinion mob)
        {
            void Check(bool ok,string why){if(!ok)throw new Exception("Hub perception native: "+why);}
            var info=hubMobs[mob.Id];var originalBrain=info.Brain;var originalPosition=mob.Body.position;float originalYaw=mob.Yaw;
            var pilotPosition=Local.Body.position;bool kinematic=mob.Body.isKinematic,collider=mob.Collider.enabled;
            var blocker=new GameObject("Hub perception LOS fixture");blocker.layer=PropLayer;blocker.transform.SetParent(root.transform);
            blocker.transform.position=new Vector3(1000,1.5f,1005);var box=blocker.AddComponent<BoxCollider>();box.size=new Vector3(8,3,1);box.enabled=false;
            var enemies=new System.Collections.Generic.List<NativePilot>{Local};
            try
            {
                mob.Body.isKinematic=false;mob.Collider.enabled=true;mob.Body.position=new Vector3(1000,.9f,1000);mob.Yaw=0;
                Local.Body.position=new Vector3(1000,.9f,1010);Physics.SyncTransforms();
                Check(HubSees(mob,Local),"clear forward vision");
                box.enabled=true;Physics.SyncTransforms();Check(!HubSees(mob,Local),"scene wall blocks forward vision");
                box.enabled=false;Local.Body.position=new Vector3(1000,.9f,990);Physics.SyncTransforms();Check(!HubSees(mob,Local),"rear pilot outside close radius not seen");
                Local.Body.position=new Vector3(1000,.9f,998);Physics.SyncTransforms();Check(HubSees(mob,Local),"close rear pilot noticed");
                Local.Body.position=new Vector3(1000,.9f,990);info.Brain=new HubZombieBrain(mob.Id,Time,Vec(mob.Body.position),0);info.Sound=hubSoundId;info.SenseAt=Time;
                HubNoise(Local,Local.Body.position,HubNoiseKind.Fire);
                Check(StepHubMind(mob,enemies,out _)&&info.Brain.View.State==HubZombieState.Alert&&mob.Pve.Struck,"hearing starts turn reaction without attack");
                float alerted=info.Brain.View.AlertAt;Check(Math.Abs(info.Brain.View.LookYaw)==180,"reaction looks toward heard position");
                for(int i=0;i<4;i++)Match.Tick(.25);info.SenseAt=Time;StepHubMind(mob,enemies,out _);
                Check(info.Brain.View.State==HubZombieState.Chase&&info.Brain.View.AlertAt==alerted,"reaction resolves once into pursuit");
                // The record must survive Unity's actual serializer and guest reconstruction, including late join mid-reaction.
                mob.Pve.Hub.State=HubZombieState.Alert;var source=NetworkFrame();var wire=NativeFrameCodec.Clone(source);var record=wire.minions.Single(x=>x.id==mob.Id);
                Check(record.hubState==HubZombieState.Alert&&record.hubAlertAt==alerted&&record.hubLookYaw==mob.Pve.Hub.LookYaw,"JsonUtility carries shared state time and look");
                var guest=new NativeArena(Content,new NativeMatch(Content),MapId){Replica=true,ReplicaScene=root.scene};
                try
                {
                    guest.ApplyFrame(wire,LocalId);var copy=guest.Minions.Single(x=>x.Id==mob.Id).Pve.Hub;
                    Check(copy!=null&&copy.State==HubZombieState.Alert&&copy.AlertAt==alerted&&copy.LookYaw==record.hubLookYaw,"late join restores reaction");
                    record.hubState=HubZombieState.None;guest.ApplyFrame(wire,LocalId);Check(guest.Minions.Single(x=>x.Id==mob.Id).Pve.Hub==null,"absent hub state clears on guest");
                }
                finally{guest.Dispose();}
                int noiseBefore=hubSounds.Count;Local.Body.position=new Vector3(0,.9f,0);
                foreach(HubNoiseKind kind in Enum.GetValues(typeof(HubNoiseKind)))HubNoise(Local,Local.Body.position,kind);
                Check(hubSounds.Count==noiseBefore,"sanctuary cannot emit any player noise");
                var gate=hubAuthority.Layout.Gates[0];var outside=Vec(gate.Outside.Position)+Vector3.up*.9f;mob.Body.position=outside;Local.Body.position=Vec(gate.Inside.Position)+Vector3.up*.9f;mob.Yaw=90;
                Check(HubCross(outside,Local.Body.position)&&!HubSees(mob,Local)&&!HubPerception.Hears(Vec(outside),Vec(Local.Body.position),HubNoiseKind.Explosion,HubCross(outside,Local.Body.position)),"gate boundary blocks sight and noise");
                Check(info.Points.Length>0&&info.Points.All(n=>info.Zone.Volume.Contains(new V3(n.x,info.Zone.Volume.y,n.z))&&!HubInside(n)),"roaming destinations are authored nav points in the zone outside sanctuary");
                Debug.Log("PASS hub zombie native sight/LOS/hearing/reaction/fence/JsonUtility/guest state checks");
            }
            finally
            {
                box.enabled=false;CapturedAnimation.Release(blocker);mob.Body.position=originalPosition;mob.Yaw=originalYaw;mob.Body.linearVelocity=Vector3.zero;mob.Body.isKinematic=kinematic;mob.Collider.enabled=collider;
                Local.Body.position=pilotPosition;info.Brain=originalBrain;mob.Pve.Hub=originalBrain.View;mob.Pve.Target=null;info.SenseAt=Time;hubSounds.Clear();hubFeet.Clear();Physics.SyncTransforms();
            }
        }
    }
}
