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
        private Vector3 predictionCorrection;
        public float ReplicaRenderTime=>(float)replicaClock.Time;
        private static void SetReplicaPosition(Rigidbody body,Vector3 position)
        {body.interpolation=RigidbodyInterpolation.None;body.transform.position=position;body.position=position;}
        private NativePredictionState PredictionState(NativePilotFrame frame)
        {
            var p=Local;bool unlocked=p!=null&&frame.hp>0&&p.Pose.GrabOwner==0&&Match.Phase==NativePhase.Fight&&frame.stunEnd<=Time&&frame.rollEnd<=Time&&frame.down==null&&string.IsNullOrEmpty(frame.fighter?.Kind)&&p.Pose.AttackUntil<=Time;
            return new NativePredictionState{Position=Vec(frame.position),Velocity=new V3(frame.moveVelocity.x,frame.velocity.y,frame.moveVelocity.z),
                Speed=p==null?6.8f:(float)(6.8*p.Stats["speed"])*(p.Input.Aim&&p.Weapon.@class=="ranged"?(float)Content.Ads(p.Seat.spec).move:1),
                Gravity=p==null?23:(float)(23*p.Stats["playerGravity"]),JumpSpeed=p==null?8.5f:(float)(8.5*p.Stats["jumpPower"]),
                Grounded=p?.Pose.Grounded??false,CanMove=unlocked,CanSprint=unlocked&&frame.stamina>0&&frame.reloadEnd<=Time&&frame.blockEnd<=Time};
        }
        private V3 ConstrainPrediction(V3 old,V3 desired)
        {
            desired=NativeMapRules.ExcludeTops(Map,desired);
            return Vec(NativeGroundMotor.Constrain(root.scene.GetPhysicsScene(),Vec(old),Vec(desired)));
        }
        private bool PredictionGrounded(V3 position)=>NativeGroundMotor.Probe(root.scene.GetPhysicsScene(),Vec(position)).Found;
        private void BufferReplica(NativeGameFrame frame,bool reset)
        {
            if(reset){replicaHistory.Clear();replicaClock.Reset(frame.time);predictionCorrection=Vector3.zero;}
            replicaHistory.Add(frame);while(replicaHistory.Count>32)replicaHistory.RemoveAt(0);replicaClock.Observe(frame.time);
            var local=frame.pilots.FirstOrDefault(p=>p.id==LocalId);
            if(local==null)return;
            var state=PredictionState(local);var before=Local.Body.position;
            if(reset)predictor.Reset(state,local.inputAck);else predictor.Reconcile(state,local.inputAck,ConstrainPrediction,PredictionGrounded);
            var error=before-Vec(predictor.State.Position);predictionCorrection=reset||error.magnitude>3?Vector3.zero:error;
            SetReplicaPosition(Local.Body,Vec(predictor.State.Position)+predictionCorrection);
        }
        public void PredictReplica(InputFrame input)
        {
            if(!Replica||replicaFrame==null||Local==null)return;
            Local.Input=input;Local.Yaw=input.Yaw;Local.Pitch=input.Pitch;
            predictor.Push(input,ConstrainPrediction,PredictionGrounded);SetReplicaPosition(Local.Body,Vec(predictor.State.Position)+predictionCorrection);
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
