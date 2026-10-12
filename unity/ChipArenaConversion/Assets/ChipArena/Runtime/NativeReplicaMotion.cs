using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class NativeArena
    {
        private readonly List<NativeGameFrame> replicaHistory=new List<NativeGameFrame>();
        private readonly NativeSnapshotClock replicaClock=new NativeSnapshotClock();
        private readonly NativePrediction predictor=new NativePrediction();
        private float predictionAirJumpAt=-100;
        private Vector3 predictionCorrection;private float predictionPoseAt=-100,predictionPoseOffset;
        public float ReplicaRenderTime=>(float)replicaClock.Time;
        private static void SetReplicaPosition(Rigidbody body,Vector3 position)
        {body.interpolation=RigidbodyInterpolation.None;body.transform.position=position;body.position=position;}
        private NativePredictionState PredictionState(NativePilotFrame frame)
        {
            var p=Local;bool unlocked=p!=null&&!(p.Pose.Kernel?.Busy(Time)??false)&&frame.hp>0&&p.Pose.GrabOwner==0&&Match.Phase==NativePhase.Fight&&frame.stunEnd<=Time&&frame.frozenUntil<=Time&&frame.scareUntil<=Time&&frame.rollEnd<=Time&&frame.down==null&&string.IsNullOrEmpty(frame.fighter?.Kind)&&(p.Pose.AttackUntil<=Time||p.Pose.CommitUntil>Time||NativeWhirlwindRules.Busy(p.Pose.MeleeKit,Time));
            var a=p?.Pose;NativeMeleeSwing swing=null;
            if(p!=null&&a.CommitUntil>Time&&NativeMeleeRules.Standard(p.Seat.spec))
            {
                swing=NativeMeleeFeel.Begin(p.Weapon,p.Stats,a.CommitAt,a.Combo+1,a.CommitYaw*Mathf.Deg2Rad);
                if(a.SprintAttack)NativeSprintAttack.Apply(swing,a.CastRate);
                swing.HitAt=a.CommitAt+(a.Followup?.08f/a.CastRate:a.VisualWindup);swing.HitUntil=swing.HitAt+a.VisualActive;swing.Until=a.CommitUntil;
                swing.StepAt=a.StepAt;swing.StepUntil=a.StepUntil;swing.Distance=a.StepDistance;swing.ChainAt=a.ChainAt;
                swing.AirKind=a.AirKind;swing.AirStartY=a.AirStartY;swing.FallHeight=a.FallHeight;swing.Air=a.AirAttack;swing.PlungeAt=a.PlungeAt;swing.ImpactAt=a.ImpactAt;swing.Carry=new V3(frame.moveVelocity.x,0,frame.moveVelocity.z);
                swing.Followup=a.Followup;swing.SprintAttack=a.SprintAttack;swing.OpenerLunge=a.OpenerLunge;swing.Remaining=a.SwingRemaining;
            }
            bool melee=p!=null&&NativeMeleeRules.Standard(p.Seat.spec)&&frame.reloadEnd<=Time&&frame.blockEnd<=Time;
            return new NativePredictionState{Bow=p?.Seat.spec=="bow",BowDrawAt=a?.BowDrawAt??-1,AirborneAt=a?.AirborneAt??-1,AirAttackUsed=a?.AirAttackUsed??false,JumpSpec=p?.Seat.spec,AirJumps=a?.AirJumps??0,AirJumpAt=a?.AirJumpAt??-100,Swimming=NativeHazards.InLava(MapId,Vec(frame.position)),Time=Time,RollUntil=frame.rollEnd,Ready=frame.ready,Stamina=frame.stamina,Combo=a?.Combo??-1,ComboUntil=a?.ComboUntil??0,BufferUntil=a?.MeleeBufferUntil??-1,
                OverdriveAt=frame.overdriveAt,OverdriveUntil=frame.overdriveUntil,MeleeKit=a?.MeleeKit?.ViewCopy(),MeleeWeapon=melee?p.Weapon:null,MeleeStats=melee?p.Stats:null,Swing=swing,Position=Vec(frame.position),Velocity=new V3(frame.moveVelocity.x,frame.velocity.y,frame.moveVelocity.z),
                Speed=p==null?6.8f:(float)(6.8*p.Stats["speed"])*(p.Input.Aim&&NativeRangedRules.Scope(p.Seat.spec)?(float)Content.Ads(p.Seat.spec).move:1),
                Gravity=p==null?23:(float)(23*p.Stats["playerGravity"]),JumpSpeed=p==null?8.5f:(float)(8.5*p.Stats["jumpPower"]),
                Sprinting=p?.Pose.Sprinting??false,Grounded=p?.Pose.Grounded??false,CanMove=unlocked,CanSprint=unlocked&&frame.stamina>0&&frame.reloadEnd<=Time&&frame.blockEnd<=Time};
        }
        private V3 ConstrainPrediction(V3 old,V3 desired)
        {
            desired=NativeMapRules.ExcludeTops(Map,desired);
            return Vec(NativeGroundMotor.Constrain(root.scene.GetPhysicsScene(),Vec(old),Vec(desired)));
        }
        private bool PredictionGrounded(V3 position)=>NativeGroundMotor.Probe(root.scene.GetPhysicsScene(),Vec(position)).Found;
        private void BufferReplica(NativeGameFrame frame,bool reset)
        {
            if(reset){predictionAirJumpAt=-100;predictionPoseAt=-100;replicaHistory.Clear();replicaClock.Reset(frame.time);predictionCorrection=Vector3.zero;}
            replicaHistory.Add(frame);while(replicaHistory.Count>32)replicaHistory.RemoveAt(0);replicaClock.Observe(frame.time);
            var local=frame.pilots.FirstOrDefault(p=>p.id==LocalId);
            if(local==null)return;
            var state=PredictionState(local);var before=Local.Body.position;
            if(reset)predictor.Reset(state,local.inputAck);else predictor.Reconcile(state,local.inputAck,ConstrainPrediction,PredictionGrounded,id=>MeleeLockPoint(Local,id));
            var error=before-Vec(predictor.State.Position);predictionCorrection=reset||error.magnitude>3?Vector3.zero:error;
            SetReplicaPosition(Local.Body,Vec(predictor.State.Position)+predictionCorrection);
        }
        public void PredictReplica(InputFrame input)
        {
            if(!Replica||replicaFrame==null||Local==null)return;
            Local.Input=input;Local.Yaw=Local.Pose.CommitUntil>Time?Local.Pose.CommitYaw:Local.MeleeKit.RecoverUntil>Time&&!NativeWhirlwindRules.Active(Local.MeleeKit,predictor.State.Time)?Local.MeleeKit.Yaw*Mathf.Rad2Deg:input.Yaw;Local.Pitch=input.Pitch;
            predictor.Push(input,ConstrainPrediction,PredictionGrounded,id=>MeleeLockPoint(Local,id));
            if(predictor.State.Swing!=null)Local.Yaw=predictor.State.Swing.Yaw*Mathf.Rad2Deg;SetReplicaPosition(Local.Body,Vec(predictor.State.Position)+predictionCorrection);
        }
        public void PresentedJump(NativePilot p,out bool grounded,out float airJumpAt)
        {
            grounded=p.Pose.Grounded;airJumpAt=p.Pose.AirJumps>0?p.Pose.AirJumpAt:-100;
            if(!Replica||p!=Local)return;
            var state=predictor.State;grounded=state.Grounded;
            // One event per airtime: an authority acknowledgement may correct its timestamp, but must not play it twice.
            if(state.AirJumps==0)predictionAirJumpAt=-100;
            else if(predictionAirJumpAt<=-90)predictionAirJumpAt=state.AirJumpAt+ReplicaRenderTime-state.Time;
            airJumpAt=predictionAirJumpAt;
        }
        private void PredictedJumpPose(Actor a)
        {PresentedJump(Local,out a.Grounded,out a.AirJumpAt);a.AirJumps=predictor.State.AirJumps;}
        private void PredictedMeleePose(Actor a)
        {
            var state=predictor.State;var s=state.Swing;if(state.MeleeWeapon==null||!state.CanMove||NativeWhirlwindRules.Busy(state.MeleeKit,state.Time))return;
            if(s==null){a.CommitUntil=a.AttackUntil=Math.Min(a.AttackUntil,state.Time);a.SwingAt=a.SwingUntil=-100;return;}
            // Translate only the transient local pose onto the interpolated presentation clock.
            // Recorded authority timestamps and remote actors are never rewritten.
            if(predictionPoseAt!=s.At){predictionPoseAt=s.At;predictionPoseOffset=ReplicaRenderTime-state.Time;}
            float offset=predictionPoseOffset;
            a.Sprinting=false;a.Combo=state.Combo;a.AttackAt=a.LastAttack=a.CommitAt=s.At+offset;a.AttackUntil=a.CommitUntil=s.Until+offset;a.CommitYaw=s.Yaw*Mathf.Rad2Deg;
            a.SwingAt=s.HitAt+offset;a.SwingUntil=s.HitUntil+offset;a.StepAt=s.StepAt+offset;a.StepUntil=s.StepUntil+offset;a.StepDistance=s.Distance;
            a.AirKind=s.AirKind;a.AirStartY=s.AirStartY;a.FallHeight=s.FallHeight;a.AirAttack=s.Air;a.PlungeAt=s.PlungeAt+offset;a.ImpactAt=s.ImpactAt<0?-1:s.ImpactAt+offset;a.AirAttackUsed=state.AirAttackUsed;a.AirborneAt=state.AirborneAt;
            a.ChainAt=s.ChainAt+offset;a.Followup=s.Followup;a.SprintAttack=s.SprintAttack;a.OpenerLunge=s.OpenerLunge;
            a.VisualWindup=s.HitAt-s.At;a.VisualActive=s.HitUntil-s.HitAt;a.VisualDuration=s.Until-s.At;
        }
        public void InterpolateReplica(float delta)
        {
            if(replicaHistory.Count==0)return;
            double time=replicaClock.Advance(delta);while(replicaHistory.Count>2&&replicaHistory[1].time<=time)replicaHistory.RemoveAt(0);
            var a=replicaHistory[0];var b=replicaHistory.Count>1?replicaHistory[1]:a;
            if(Map.unity)RestoreStructures(a.segments,(float)time,a.props);
            float blend=Mathf.Clamp01((float)((time-a.time)/Math.Max(.00001,b.time-a.time)));
            foreach(var p in Pilots)
            {
                if(p.Seat.id==LocalId){predictionCorrection*=Mathf.Exp(-15*delta);SetReplicaPosition(p.Body,Vec(predictor.State.Position)+predictionCorrection);continue;}
                var x=a.pilots.FirstOrDefault(f=>f.id==p.Seat.id);var y=b.pilots.FirstOrDefault(f=>f.id==p.Seat.id);
                if(x==null||y==null)continue;
                bool continuous=(x.hp>0)==(y.hp>0)&&Vector3.Distance(x.position,y.position)<5;
                SetReplicaPosition(p.Body,continuous?Vector3.Lerp(x.position,y.position,blend):y.position);
                p.Yaw=Mathf.LerpAngle(x.yaw,y.yaw,blend);p.Pitch=Mathf.Lerp(x.pitch,y.pitch,blend);
            }
            foreach(var m in Minions)
            {
                var x=a.minions.FirstOrDefault(f=>f.id==m.Id);var y=b.minions.FirstOrDefault(f=>f.id==m.Id);
                if(x!=null&&y!=null){SetReplicaPosition(m.Body,Vector3.Distance(x.position,y.position)<5?Vector3.Lerp(x.position,y.position,blend):y.position);m.Yaw=Mathf.LerpAngle(x.yaw,y.yaw,blend);}
            }
            Physics.SyncTransforms();
        }
    }
}
