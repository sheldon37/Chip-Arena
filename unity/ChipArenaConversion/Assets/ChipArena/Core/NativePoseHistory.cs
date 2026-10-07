using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    // Only the captured/base layer belongs in this history. Recoil, IK and first-person
    // settling are applied afterwards and must never become the next frame's input.
    public sealed class NativePoseHistory
    {
        private readonly Vector3[] position=new Vector3[CapturedRig.Bones.Length];
        private readonly Quaternion[] rotation=new Quaternion[CapturedRig.Bones.Length];
        private float at=-100;private bool fresh=true;
        public void BlendBase(CapturedRig.Pose pose,float time,bool contactFrame)
        {
            float dt=time-at;bool reset=fresh||dt<0||dt>.35f;
            float weight=reset||contactFrame?1:1-(float)Math.Exp(-22*Math.Min(.1f,dt));
            for(int i=0;i<position.Length;i++)
            {
                if(weight<1){pose.Position[i]=Vector3.Lerp(position[i],pose.Position[i],weight);pose.Rotation[i]=Quaternion.Slerp(rotation[i],pose.Rotation[i],weight);}
                position[i]=pose.Position[i];rotation[i]=pose.Rotation[i];
            }
            at=time;fresh=false;
        }
    }
    public sealed class NativeVisualClock
    {
        public const double Step=1d/60;
        private double lastSimulation=double.NaN,value;
        public void Reset(double simulationTime){lastSimulation=simulationTime;value=simulationTime;}
        // Fill the gap between fixed ticks for analytic presentation only. Never advance
        // authoritative timers or rewind when a menu freezes the simulation mid-frame.
        public float Sample(double simulationTime,double sinceFixedStep,bool paused)
        {
            if(double.IsNaN(lastSimulation)||simulationTime<lastSimulation||simulationTime-lastSimulation>.35)Reset(simulationTime);
            lastSimulation=simulationTime;
            if(!paused)value=Math.Max(value,simulationTime+Math.Max(0,Math.Min(Step,sinceFixedStep)));
            return (float)value;
        }
    }
}
