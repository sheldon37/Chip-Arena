#if UNITY_EDITOR || ARENA_VALIDATE
using System;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        // Actual native motor, capsule, PhysX and JsonUtility; invoked in the isolated locomotion build scene.
        public void ValidateAirJumps()
        {
            var p=Local;var oldAccess=p.Seat.access;var grant=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").GetSetMethod(true).Invoke(grant,new object[]{true});typeof(NativeAccess).GetProperty("Expires").GetSetMethod(true).Invoke(grant,new object[]{double.MaxValue});var physics=root.scene.GetPhysicsScene();var hold=Match.HoldResolution;
            var floor=new GameObject("Air jump validation floor");floor.layer=PropLayer;floor.transform.SetParent(root.transform);
            floor.transform.position=new Vector3(1000,19.5f,1000);floor.AddComponent<BoxCollider>().size=new Vector3(30,1,30);
            void Check(bool ok,string why){if(!ok)throw new Exception("Native air jump: "+why);}
            void Setup(string spec,bool airborne=false)
            {
                p.Seat.access=grant;p.Cyber=new NativeCyberKit();p.Kernel=null;p.KernelReady=0;p.Mana=100;p.Seat.connected=true;p.Seat.spec=spec;p.Weapon=Content.Weapon(spec);p.Stats=NativeContentRules.Stats(Content,spec,p.Seat.chips);
                p.AirJumps=0;p.AirJumpAt=-100;p.AirborneAt=-1;p.AirAttackUsed=false;p.Grounded=p.GroundAttached=false;p.Swing=null;p.Special=null;p.Down=null;p.Juggle=null;p.Roll=null;
                p.RollEnd=p.StunEnd=p.AttackEnd=p.Ready=p.BlockEnd=p.FrozenUntil=p.ScareUntil=p.ReloadEnd=0;
                p.Body.position=new Vector3(1000,airborne?25:20+NativeLocomotionRules.HalfHeight+.02f,1000);
                p.Body.linearVelocity=p.MoveVelocity=p.Knockback=Vector3.zero;p.Body.isKinematic=false;p.Body.useGravity=true;
                p.Input=default;p.Stamina=p.Hp=100;p.Seat.alive=true;Physics.SyncTransforms();
            }
            void Tick(bool jump=false,bool attack=false)
            {
                p.Input=new InputFrame{Jump=jump,Attack=attack,Shoot=attack};ProbePilotGround(p);StepPilot(p);BeforePhysics();physics.Simulate(Rules.Dt);AfterPhysics();Match.Tick(1d/60);
            }
            try
            {
                Match.HoldResolution=()=>true;Match.ApplyNetwork(new NativeMatchFrame{phase=NativePhase.Fight,time=10,fightAt=10});Physics.gravity=Vector3.down*Rules.Gravity;
                foreach(string spec in new[]{"sword","daggers","shield","greatsword","pistol","fire","bow","cyberlord"})
                {
                    Setup(spec);for(int i=0;i<20;i++)Tick();Check(p.Grounded,"fixture supports "+spec);
                    Tick(true);Check(!p.Grounded&&p.Body.linearVelocity.y>7&&p.AirJumps==0,"ground jump "+spec);
                    for(int i=0;i<18;i++)Tick();float before=p.Body.linearVelocity.y;Tick(true);
                    Check(NativeJumpSimulation.CanAirJump(spec)?p.AirJumps==1&&p.Body.linearVelocity.y>7:p.AirJumps==0&&p.Body.linearVelocity.y<before,"eligible air jump "+spec);
                    float at=p.AirJumpAt;before=p.Body.linearVelocity.y;Tick(true);
                    Check(p.AirJumpAt==at&&p.Body.linearVelocity.y<before,"third press refused "+spec);
                    for(int i=0;i<180&&!p.Grounded;i++)Tick();Check(p.Grounded&&p.AirJumps==0,"landing resets "+spec);
                }
                foreach(string airSpec in new[]{"daggers","bow","cyberlord"})
                {
                    Setup(airSpec,true);p.Stats["jumpPower"]=1.5;Tick(true);
                    Check(p.AirJumps==1&&Math.Abs(p.Body.linearVelocity.y-(11.475f-Rules.Gravity*Rules.Dt))<.02f,"walk-off air jump scales once and replaces vertical velocity");
                    var frame=NativeFrameCodec.Clone(NetworkFrame());var actor=frame.snapshot.Actors.First(a=>a.Id==p.Seat.id);
                    Check(frame.Valid(Content)&&actor.AirJumps==1&&actor.AirJumpAt==p.AirJumpAt&&!actor.Grounded,"real JSON retains count/time/ground contact for guests, late join and replay");
                    var prediction=PredictionState(frame.pilots.First(a=>a.id==p.Seat.id));
                    Check(prediction.AirJumps==1&&prediction.AirJumpAt==actor.AirJumpAt&&NativePrediction.Step(prediction,new InputFrame{Jump=true},null).AirJumpAt==actor.AirJumpAt,"snapshot seeds guest air budget and refuses another jump");
                    Replica=true;predictor.Reset(prediction,0);PresentedJump(p,out bool grounded,out float presentedAt);
                    Check(!grounded&&presentedAt>-90,"local predicted presentation signal");
                    prediction.AirJumpAt+=.025f;predictor.Reconcile(prediction,1,null);PresentedJump(p,out _,out float acknowledgedAt);
                    Check(acknowledgedAt==presentedAt,"corrected acknowledgement cannot replay the flip cue");
                    prediction.Grounded=true;prediction.AirJumps=0;predictor.Reconcile(prediction,2,null);PresentedJump(p,out grounded,out presentedAt);
                    Check(grounded&&presentedAt==-100,"landing releases the local cue latch");Replica=false;
                    actor.AirJumps=2;Check(!frame.Valid(Content),"invalid air count rejected");actor.AirJumps=1;
                    actor.AirJumpAt=(float)frame.time+1;Check(!frame.Valid(Content),"future air timestamp rejected");
                }
                foreach(string lockedSpec in new[]{"daggers","bow","cyberlord"})
                foreach(bool attack in new[]{false,true})
                {
                    Setup(lockedSpec,true);if(attack)p.AttackEnd=Time+1;else p.StunEnd=Time+1;Tick(true);
                    Check(p.AirJumps==0&&p.AirJumpAt==-100&&p.Body.linearVelocity.y<0,(attack?"attack":"stun")+" blocks air jump");
                }
                // Airborne, a press is the landing attack once its .15 s airtime gate opens (NativeAirAttackSimulation): that commitment
                // takes priority over a same-tick air jump; an earlier press commits nothing and the air jump goes first.
                Setup("daggers",true);p.AirborneAt=Time-1;Tick(true,true);Check(p.AttackEnd>Time&&p.AirJumps==0&&p.Swing!=null&&p.Swing.Air,"new attack commitment takes priority over same-tick jump");
                Setup("daggers",true);Tick(true,true);Check(p.Swing==null&&p.AirJumps==1,"an air press before the airtime gate leaves the air jump");
                foreach(string kind in new[]{"cyber-jab","firewall","packet-burst","siphon-link","system-crash","data-dash"})
                {
                    Setup("cyberlord",true);CyberBegin(p,kind,0,1);Tick(true);
                    Check(p.AirJumps==0&&p.AirJumpAt==-100,"Cyber cast refuses air jump: "+kind);
                    var f=NetworkFrame();var predicted=PredictionState(f.pilots.First(x=>x.id==p.Seat.id));
                    Check(NativePrediction.Step(predicted,new InputFrame{Jump=true},(old,wanted)=>wanted).AirJumps==0,"guest refuses Cyber cast: "+kind);
                }
                Setup("cyberlord",true);Tick(true,true);Check(p.Cyber.Kind=="cyber-jab"&&p.AirJumps==0,"same-tick Cyber primary wins over air jump");
                Setup("cyberlord",true);Check(StartKernelPanic(p),"DEV fixture starts Kernel Panic");Tick(true);
                Check(p.AirJumps==0,"Kernel Panic refuses host air jump");
                var kernelFrame=NetworkFrame();var kernelPrediction=PredictionState(kernelFrame.pilots.First(x=>x.id==p.Seat.id));
                Check(!kernelPrediction.CanMove&&NativePrediction.Step(kernelPrediction,new InputFrame{Jump=true},(old,wanted)=>wanted).AirJumps==0,"Kernel Panic refuses guest air jump");
                ValidateAirJumpInput();
                Debug.Log("PASS native air jump: eight weapon routes, ground/air/third press/landing, walk-off, jumpPower, attack/stun locks, coalesced input and real JSON state validation");
            }
            finally{if(KernelBusy(p))EndKernel(p,3);p.Seat.access=oldAccess;Replica=false;Match.HoldResolution=hold;CapturedAnimation.Release(floor);}
        }

        private void ValidateAirJumpInput()
        {
            // The authority rejects network input for its own seat. Exercise the guest queue with a distinct ID.
            int guestId=LocalId==1?2:1;
            void Check(bool ok,string why){if(!ok)throw new Exception("Native air jump: "+why);}
            ClearRemoteInput(LocalId);ClearRemoteInput(guestId);
            try
            {
                AcceptInput(LocalId,new InputFrame{Sequence=1,Jump=true});
                Check(!RemoteInput(LocalId).Jump,"network input cannot control the host seat");
                AcceptInput(guestId,new InputFrame{Sequence=1,Jump=true});AcceptInput(guestId,new InputFrame{Sequence=2,Z=1});
                var first=RemoteInput(guestId);var held=RemoteInput(guestId);
                Check(first.Jump&&!held.Jump&&first.Sequence==2&&held.Z==1,"coalesced guest air edge consumed once while movement is retained");
                AcceptInput(guestId,new InputFrame{Sequence=2,Jump=true});
                Check(!RemoteInput(guestId).Jump,"duplicate packet cannot replay the air edge");
                AcceptInput(guestId,new InputFrame{Sequence=3,Jump=true});
                Check(RemoteInput(guestId).Jump&&!RemoteInput(guestId).Jump,"next guest air edge consumed once");
            }
            finally{ClearRemoteInput(LocalId);ClearRemoteInput(guestId);}
        }
    }
}
#endif
