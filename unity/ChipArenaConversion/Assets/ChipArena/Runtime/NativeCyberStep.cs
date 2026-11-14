using System;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class NativePilot {public NativeCyberStep CyberStep;}
    public sealed partial class NativeArena
    {
        private float CyberStepLimit(NativePilot p,V3 position,V3 facing,float distance,int combo,int lockTarget)
        {
            if(distance<=0)return 0;
            // Lock first; every other hittable body is still a clearance constraint.
            foreach(var q in CyberTargets(p))if(q.Id==lockTarget&&MeleeVisible(Vec(position),q.Point,p,q.Collider))
                distance=NativeCyberStep.TargetLimit(Vec(q.Point)-position,facing,distance,combo);
            foreach(var q in CyberTargets(p))if(q.Id!=lockTarget&&MeleeVisible(Vec(position),q.Point,p,q.Collider))
                distance=NativeCyberStep.TargetLimit(Vec(q.Point)-position,facing,distance,combo);
            return NativeGroundMotor.ForwardStepDistance(root.scene.GetPhysicsScene(),Vec(position),Vec(facing),distance);
        }
        private float PredictCyberStepLimit(V3 position,V3 facing,float distance,int combo,int target)=>CyberStepLimit(Local,position,facing,distance,combo,target);
        private void CyberStepMotion(NativePilot p)
        {
            if(!p.CyberStep.Present||p.CyberStep.Stopped||p.CyberStep.Travelled>=p.CyberStep.Distance)return;
            bool allowed=p.Grounded&&!Incapacitated(p)&&Time>=p.RollEnd&&Time>=p.BlockEnd&&!KernelBusy(p);
            var delta=NativeCyberStep.Advance(ref p.CyberStep,p.Cyber,Time,allowed,Vec(p.Body.position),(pos,dir,d,n,target)=>CyberStepLimit(p,pos,dir,d,n,target));
            if(delta.Length>0){p.Body.position+=Vec(delta);Physics.SyncTransforms();ProbePilotGround(p);}
        }
        private void CyberStepSnapshot(NativePilot p,Actor a)
        {
            var s=p.CyberStep;if(!NativeCyberStep.Matches(p.Cyber,s))return;
            a.StepAt=s.At;a.StepUntil=s.Until;a.StepDistance=s.Distance;
        }
    }
}
