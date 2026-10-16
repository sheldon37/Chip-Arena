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
            var session=new ExpeditionSession(run,Content,_=>{});session.Sources.Seat=_=>Local.Seat.id;
            var director=new ExpeditionDirector(session,layout,_=>Vec(Local.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight),(a,b)=>ExpeditionVisible(a,b),100,100,60);
            try
            {
                Check(run.Enter(area.id),"enter fixture instance");director.Step();BindExpeditionCombat(director,area,data);
                Check(Pve.Stage=="wave"&&Map.hazards.Length==Content.Map("siren").hazards.Length&&Pve.Content.machineBlocks.All(b=>Props.Any(p=>p.LocalFixture&&p.Definition.id==b.id)),"full Ward hazards, wave lighting state and boss-test fixtures bound");
                var ticket=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(run,"native-host",session.Sources));
                Check(ExpeditionAreaReady.Accept(session,"native-host",ticket),"matching ready ticket accepted");
                Check(!director.CompleteAreaLoad((_,__)=>false)&&run.Snapshot().loading&&!run.Snapshot().members[0].safe,"failed placement stays frozen and unsafe");
                Check(director.CompleteAreaLoad((_,points)=>{Local.Body.position=Vec(points["native-host"].Position)+Vector3.up*(NativeLocomotionRules.HalfHeight+.04f);return true;}),"placement completes");
                var entryFeet=Vec(Local.Body.position-Vector3.up*NativeLocomotionRules.HalfHeight);
                var add=SpawnPveAdd("shambler",true);Check(add!=null,"real encounter entity created");
                add.Pve.SpawnAt=add.Pve.ActiveAt=add.Pve.ExposedAt=Time-1;add.Pve.Struck=true;add.Collider.enabled=true;add.Body.isKinematic=false;
                HitPveAdd(add,Local,1000000,Vector3.zero,0,null,"projectile",0,false);
                Check(run.Snapshot().instances.Single(x=>x.id==area.id).progress==1,"confirmed combat death advances quota once");
                Check(run.Snapshot().members[0].wallet==25,"real shambler kill pays the Expedition wallet");
                ExpeditionKilled(add,Local);Check(run.Snapshot().members[0].wallet==25,"duplicate kill callback cannot pay twice");
                void Stand(V3 at){Local.Body.position=Vec(at)+Vector3.up*NativeLocomotionRules.HalfHeight;Physics.SyncTransforms();}
                int Wallet()=>run.Snapshot().members[0].wallet;
                var cache=area.interactions.First(i=>area.anchors.Any(a=>a.id==i.anchor&&a.kind=="chest"));Stand(new V3(cache.x,cache.y,cache.z));
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==25&&run.Snapshot().rewards.Length==0,"cache refuses insufficient funds without granting reward");
                Check(run.Credit("native-host","interaction-fixture",1000),"fund machine checks");
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==925,"nearby cache purchase charges exactly once");
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==925,"sold cache refuses duplicate input");
                director.Step();Check(TryExpeditionInteract(Local,out _)&&Wallet()==925,"restocking cache refuses purchase");
                var pick=run.Snapshot().rewards.Single();Check(pick.claimed&&pick.owner=="native-host"&&pick.offers.Length>0,"purchased cache offers belong to buyer");
                Check(session.Execute("native-host",new ExpeditionCommand(ExpeditionIntent.OpenChoice,pick.id)).state==ExpeditionCommandState.Pending,"open choice intent");
                Check(NativeExpeditionView.Build(run,"native-host",session.Sources).Member("native-host").choosing,"chooser presence is public");
                float heldYaw=Local.Yaw,heldPitch=Local.Pitch;
                Local.Input=new InputFrame{X=1,Z=1,Jump=true,Roll=true,Block=true,Yaw=37,Pitch=12};
                Local.MoveVelocity=Local.Knockback=Vector3.forward*10;Local.Body.linearVelocity=Vector3.forward*10;
                PveInput(Local);RouteLeetInputs();
                Check(Frozen(Local)&&Local.Input.X==0&&Local.Input.Z==0&&!Local.Input.Jump&&!Local.Input.Roll&&!Local.Input.Block,"choosing suppresses movement and actions");
                Check(Local.Yaw==heldYaw&&Local.Pitch==heldPitch&&Local.Input.Yaw==heldYaw&&Local.Input.Pitch==heldPitch,"choosing freezes camera yaw and pitch");
                Check(!TryExpeditionInteract(Local,out _),"choosing refuses machine interaction");
                Check(Local.MoveVelocity==Vector3.zero&&Local.Knockback==Vector3.zero&&Local.Body.linearVelocity==Vector3.zero,"choosing cancels momentum");
                RestoreLeetInputs();Local.Input=new InputFrame();Local.MoveVelocity=Local.Knockback=Local.Body.linearVelocity=Vector3.zero;
                var choiceGround=Local.Body.position;
                Local.Body.position=choiceGround+Vector3.up*2;Local.FrozenPosition=null;Local.GroundAttached=false;
                Local.Body.linearVelocity=Vector3.up*8;ProbePilotGround(Local);RouteLeetInputs();RestoreLeetInputs();
                Check(Local.Body.useGravity&&Local.Body.linearVelocity.y<=0,"airborne choice stops rising but permits gravity");
                float choiceTop=Local.Body.position.y;
                for(int tick=0;tick<90;tick++)
                {
                    ProbePilotGround(Local);RouteLeetInputs();RestoreLeetInputs();Physics.Simulate(1f/60);
                }
                ProbePilotGround(Local);
                Check(Local.Body.position.y<choiceTop-1&&Local.Grounded,"airborne chooser falls and lands on Ward geometry");
                Check(Math.Abs(Local.Body.position.x-choiceGround.x)<.05f&&Math.Abs(Local.Body.position.z-choiceGround.z)<.05f,"falling choice holds horizontal position");
                Local.Body.position=choiceGround+Vector3.up*2;Local.FrozenPosition=null;Local.GroundAttached=false;
                Local.Body.linearVelocity=Vector3.down*7;ProbePilotGround(Local);RouteLeetInputs();
                Check(Local.Body.linearVelocity.y<=-7,"choice preserves accumulated falling speed");RestoreLeetInputs();
                Local.FrozenUntil=Time+1;RouteLeetInputs();RestoreLeetInputs();
                Check(!Local.Body.useGravity&&Local.Body.linearVelocity==Vector3.zero,"real freeze still suspends an airborne chooser");
                Local.FrozenUntil=0;Local.Body.position=choiceGround;Local.FrozenPosition=null;Local.Body.linearVelocity=Vector3.zero;ProbePilotGround(Local);
                float chooserHp=Local.Hp;Damage(Local,null,1,Vector3.zero,0,false,stage:true);
                Check(Local.Hp<chooserHp&&run.IsChoosing("native-host"),"ordinary damage still reaches a choosing pilot");Local.Hp=chooserHp;
                int installBefore=Events.Count(e=>e.Kind==EventKind.Install);
                Check(session.Execute("native-host",new ExpeditionCommand(ExpeditionIntent.Select,pick.id,pick.offers[0])).state==ExpeditionCommandState.Pending,"choose purchased chip");
                SyncExpeditionInventory();Check(Local.Seat.chips.TryGetValue(pick.offers[0],out int held)&&held==1,"choice applied to live combat inventory");
                Check(Frozen(Local)&&Events.Count(e=>e.Kind==EventKind.Install)==installBefore+1,"selection holds through wrist plant and emits Install");
                Check(session.Execute("native-host",new ExpeditionCommand(ExpeditionIntent.Select,pick.id,pick.offers[0])).state==ExpeditionCommandState.Refused&&Events.Count(e=>e.Kind==EventKind.Install)==installBefore+1,"duplicate emits no Install");
                run.SetChoosing("native-host",false);Check(Frozen(Local),"automatic tray close cannot cut final plant short");
                Local.Input=new InputFrame{Yaw=91,Pitch=-24};RouteLeetInputs();
                Check(Local.Yaw==heldYaw&&Local.Pitch==heldPitch&&Local.Input.Yaw==heldYaw&&Local.Input.Pitch==heldPitch,"final wrist plant still freezes camera yaw and pitch");
                RestoreLeetInputs();
                for(int i=0;i<8;i++)run.Tick(.25);Check(!Frozen(Local),"plant completion releases hold");
                RouteLeetInputs();Check(Local.Input.Yaw==91&&Local.Input.Pitch==-24,"plant completion releases camera input");
                RestoreLeetInputs();Local.Body.useGravity=true;
                var chipMachine=data.machines.First(m=>m.kind=="chip");Stand(chipMachine.front.Position());
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==750&&run.Snapshot().rewards.Length==2,"Ward dispenser uses run wallet and personal reward queue");
                var med=data.machines.First(m=>m.kind=="med");Stand(med.front.Position());Local.Hp=(float)Local.Stats["hp"]*.4f;
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==675,"med station debits run wallet");
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==675,"med cooldown prevents repeat debit");
                for(int tick=0;tick<9;tick++){Match.Tick(.25);StepPveServices();}
                Check(Local.Hp>(float)Local.Stats["hp"]*.85f,"purchased heal actually ticks in Expedition");
                var hire=data.machines.First(m=>m.kind=="recruit");Stand(hire.front.Position());
                Check(TryExpeditionInteract(Local,out _)&&Wallet()==375&&Pve.Hires.Count==1,"recruit purchase queues paid hire");
                for(int tick=0;tick<4;tick++)Match.Tick(.25);StepPveServices();Check(Pve.Recruits.Count==1&&Pve.Hires.Count==0,"paid recruit actually spawns");
                Stand(new V3(0,1000,0));Check(!TryExpeditionInteract(Local,out _)&&Wallet()==375,"out of range interaction cannot spend");
                Stand(entryFeet);
                void FinishIntro()
                {
                    Check(Pve.Holding&&Pve.Cinematic=="intro","boss arrival starts the existing intro scene");
                    float before=Pve.Boss.Hp;Damage(Pve.Boss,Local,50,Vector3.zero,0,false);Check(Pve.Boss.Hp==before,"intro blocks combat damage");
                    for(int i=0;i<600&&Pve.Holding;i++)PveHoldTick(1f/60);
                    Check(!Pve.Holding&&Pve.Stage=="boss"&&Pve.Phase==1&&Math.Abs(Pve.Floor-Pve.MaxHp*.75f)<.01,"intro releases combat at the shared 75 percent gate");
                }
                void FinishReveal(){for(int i=0;i<600&&Pve.Holding;i++)PveHoldTick(1f/60);Check(!Pve.Holding,"phase reveal completes");}
                void DefeatAllPhases()
                {
                    Damage(Pve.Boss,Local,1000000,Vector3.zero,0,false,true);StepExpeditionCombat();
                    Check(Pve.Holding&&Pve.Reveal==2&&Pve.Boss.Hp>0,"75 percent gate prevents skipping Find Me");FinishReveal();
                    Check(Pve.Phase==2&&Pve.Hidden&&Pve.Possessed!=0&&Pve.Cap==72,"shared possession and hidden horde cap");
                    var possessed=Minions.Single(m=>m.Id==Pve.Possessed);possessed.Pve.SpawnAt=possessed.Pve.ActiveAt=possessed.Pve.ExposedAt=Time-1;possessed.Pve.Struck=true;possessed.Collider.enabled=true;possessed.Body.isKinematic=false;possessed.Body.position=Vec(Pve.Content.boss.spawn.Position(.85f));Physics.SyncTransforms();
                    HitPveAdd(possessed,Local,1000000,Vector3.zero,0,null,"projectile",0,false);
                    Check(Pve.Holding&&Pve.Reveal==3&&!Pve.Hidden,"killing possessed add finds Siren");FinishReveal();
                    Check(Pve.Phase==3&&Math.Abs(Siren(Pve.Boss).Scale-1.5f)<.001&&Math.Abs(Pve.Boss.Collider.height-2.55f)<.001,"shared growth phase");
                    Damage(Pve.Boss,Local,1000000,Vector3.zero,0,false,true);StepExpeditionCombat();Check(Pve.Holding&&Pve.Reveal==4,"25 percent gate reveals enrage");FinishReveal();
                    Check(Pve.Phase==4&&Pve.Floor==0&&Siren(Pve.Boss).MaxRains==2&&Siren(Pve.Boss).MaxEruptions==4,"shared enrage rules");
                    Damage(Pve.Boss,Local,1000000,Vector3.zero,0,false,true);StepExpeditionCombat();
                    Check(Pve.Stage=="intermission"&&Pve.Rate==0&&Minions.All(m=>m.Pve==null),"final phase clears adds and stops spawning");
                }
                StepExpeditionCombat();FinishIntro();Check(director.BossArrivals.Length==1&&Pve.Boss.Hp>0,"real boss registered");
                Check(Math.Abs(director.BossArrivals[0].y-area.bossPoints[0].y)<.001,"boss arrival FX anchored at authored feet");
                Damage(Pve.Boss,Local,50,Vector3.zero,0,false);float hp=Pve.Boss.Hp;StepExpeditionCombat();
                Check(hp<Pve.MaxHp&&Math.Abs(director.BossArrivals[0].hp-hp)<.001,"boss plate follows real damage");
                DefeatAllPhases();
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
                Check(director.TryBeginFinal(),"unlocked final trigger begins combat");StepExpeditionCombat();FinishIntro();
                DefeatAllPhases();Check(run.Snapshot().phase==ExpeditionPhase.Victory,"real final death reaches victory");
                Check(run.VoteContinue("native-host",true),"continue vote");session.Tick(.1);Check(run.Snapshot().loop==1&&director.Barrier.active&&!run.Snapshot().members[0].safe,"Continue starts a new unsafe load barrier");LoadCurrent();
                Check(run.VoteExtraction("native-host",true),"sanctuary extraction vote");for(int i=0;i<41;i++)session.Tick(.25);
                Check(session.Run.Snapshot().phase==ExpeditionPhase.Ended&&session.Run.Snapshot().members[0].extracted,"durable extraction ends local run");
                Debug.Log("PASS Expedition M1 native ready/failed placement/real kill/boss damage/death/return/final/Continue/extraction callbacks");
            }
            finally{Expedition=null;Reset();}
        }
        public void ValidateExpeditionCoop()
        {
            if(MapId!="expedition-city")return;
            void Check(bool ok,string why){if(!ok)throw new Exception("Expedition native co-op: "+why);}
            string json=Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text;
            var layout=NativeExpeditionLayout.Parse(json);layout.compatibility=NativeExpeditionLayout.Compatibility(json,Content);
            var fixture=new NativeMatch(Content){Cooperative=true,Teams=true,ShowIntros=false,FinalKillcams=false};
            fixture.Seats.Add(new NativeSeat{id=1,name="Host",spec="pistol",team=0});
            fixture.Seats.Add(new NativeSeat{id=2,name="Guest",spec="pistol",team=0});
            fixture.Seats.Add(new NativeSeat{id=900,name="Boss",spec="siren",team=1,bot=true,AuthorityBoss=true});
            using(var host=new NativeArena(Content,fixture,MapId){LocalId=1,ReplicaScene=root.scene})
            {
                Check(fixture.Start(),"fixture starts");host.Reset();
                var run=NativeExpedition.Create("native-coop",layout.world,"host","pistol",99,layout.instances.Select(id=>new ExpeditionInstance{id=id,target=1}).ToArray());
                Check(run.Join("guest","pistol"),"guest joins");
                var session=new ExpeditionSession(run,Content,_=>{});session.Sources.Seat=id=>id=="host"?1:id=="guest"?2:0;
                var director=new ExpeditionDirector(session,layout,id=>Vec(host.Pilots.Single(p=>p.Seat.id==session.Sources.Seat(id)).Body.position-Vector3.up*NativeLocomotionRules.HalfHeight),(a,b)=>host.ExpeditionVisible(a,b),100,100,60);
                session.ReprintEvidence=host.ExpeditionReprintEvidence;
                var area=layout.Area(layout.world);host.BindExpeditionCombat(director,area,JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));
                Check(director.BeginAreaLoad(),"start barrier");
                var ticket=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(run,"host",session.Sources));
                Check(ExpeditionAreaReady.Accept(session,"host",ticket)&&!director.CompleteAreaLoad((_,points)=>host.PlaceExpeditionParty(points)),"waits for guest");
                Check(ExpeditionAreaReady.Accept(session,"guest",ticket)&&director.CompleteAreaLoad((_,points)=>host.PlaceExpeditionParty(points)),"two native capsules placed");
                var guest=host.Pilots.Single(p=>p.Seat.id==2);
                Check((guest.Body.position-host.Local.Body.position).sqrMagnitude>1,"distinct placements");
                host.Local.Hp=0;host.RecordExpeditionDeaths();Check(!run.Snapshot().members.Single(m=>m.account=="host").alive&&run.Snapshot().members.Single(m=>m.account=="guest").alive,"host death leaves guest active");
                var machine=area.anchors.First(a=>a.kind=="machine");var interaction=area.interactions.Single(i=>i.anchor==machine.id);
                guest.Body.position=new Vector3(interaction.x,interaction.y+NativeLocomotionRules.HalfHeight,interaction.z);Physics.SyncTransforms();director.Step();
                Check(run.Credit("guest","test-reprint",100),"guest funds");
                Check(host.TryExpeditionInteract(guest,out _)&&host.expeditionChannels.ContainsKey("guest"),"guest F starts measured reprint");
                // Session.Tick caps a single delta at one second; drive the channel
                // with ordinary frames instead of asking one call to skip 3.1 seconds.
                for(int tick=0;tick<29;tick++){session.Tick(.1);host.StepExpeditionReprints();}
                Check(!run.Snapshot().members.Single(m=>m.account=="host").alive&&host.Local.Hp<=0&&host.expeditionChannels.ContainsKey("guest"),"reprint stays pending before three seconds");
                for(int tick=0;tick<2;tick++){session.Tick(.1);host.StepExpeditionReprints();}
                Check(run.Snapshot().members.Single(m=>m.account=="host").alive&&host.Local.Hp>0&&host.Local.Collider.enabled,"guest reprints host into live capsule");
                using(var replica=new NativeArena(Content,new NativeMatch(Content),MapId){LocalId=2,ReplicaScene=root.scene})
                {
                    replica.ApplyFrame(NativeFrameCodec.Clone(host.NetworkFrame()),2);
                    var view=NativeExpeditionView.Build(run,"guest",session.Sources);
                    replica.ApplyExpeditionCollision(view,area,json);
                    Check(replica.hubDoors.Count==host.hubDoors.Count&&replica.hubDoors.Select(b=>b.enabled).SequenceEqual(host.hubDoors.Select(b=>b.enabled)),"replica builds the same closed gates");
                    var wire=NativeNetCodec.Decode(NativeNetCodec.Encode(new NativeNetMessage{type="expedition-public",epoch=7,generation=host.Generation,expeditionPublic=view}));
                    Check(wire.epoch==7&&wire.generation==host.Generation&&wire.expeditionPublic.hub.Gates.All(g=>g.Blocking),"JsonUtility preserves map generation and gate solidity");
                    var gate=host.hubAuthority.Layout.Gates[0];guest.Body.position=new Vector3(gate.Interact.x,gate.Interact.y,gate.Interact.z);Physics.SyncTransforms();
                    Check(host.TryExpeditionInteract(guest,out _),"guest gate interaction");session.Tick(1);host.StepHubGates();
                    view=NativeExpeditionView.Build(run,"guest",session.Sources);replica.ApplyExpeditionCollision(view,area,json);
                    Check(!host.hubDoors[0].enabled&&!replica.hubDoors[0].enabled,"guest follows authority release marker");
                }
                Debug.Log("PASS Expedition native co-op placement, guest reprint, collider replication and wire generation");
            }
        }
        public void ValidateExpeditionM1Geometry()
        {
            if(MapId=="expedition-city")
            {
                var original=MapId;var scene=ReplicaScene;ReplicaScene=root.scene;
                try{LoadExpeditionM1Layout(Resources.Load<TextAsset>(NativeExpeditionLayout.ResourceName).text,JsonUtility.FromJson<NativePveContent>(Resources.Load<TextAsset>("PveContent").text));Debug.Log("PASS Expedition M1 complete startup layout gate");}
                finally{ExpeditionMap(original);ReplicaScene=scene;}
            }
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
