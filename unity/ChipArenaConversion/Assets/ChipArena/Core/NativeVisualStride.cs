using System;

namespace ChipArena.UnityConversion
{
    // The visual body's stride (owner 2026-10-23: "Some of these animations really dont fit the new body", "I thought we have a
    // running animation pack already? Cant we utilize that?"; docs/BOZO-INTEGRATION.md, animation revamp). While a pilot runs or
    // walks on the ground, the visual body's legs and hip height come from the animation pack's own humanoid locomotion clips,
    // retargeted by Unity onto the body (Runtime NativeVisualStridePresentation.cs); its upper body, holds and aim stay the
    // game's. This picks the clips' weights and their playback rate from the body's travel. Presentation only: the game's gait,
    // footfalls, movement and every rule are unchanged.
    //
    // Clips, by index: 0-7 the runs and 8-15 the walks, each by direction clockwise from forward (forward, forward-right,
    // right, back-right, back, back-left, left, forward-left), and 16 the sprint. Directions and velocities are in the body's
    // own frame, Unity's: +x right, +z forward.
    public static class NativeVisualStride
    {
        public const int Directions=8, Walks=8, Sprint=16, Clips=17;
        public static readonly string[] Names={
            "Unarmed-Run-Forward","Unarmed-Run-Forward-Right","Unarmed-Run-Right","Unarmed-Run-Backward-Right","Unarmed-Run-Backward","Unarmed-Run-Backward-Left","Unarmed-Run-Left","Unarmed-Run-Forward-Left",
            "Unarmed-Strafe-Forward","Unarmed-Strafe-Forward-Right","Unarmed-Strafe-Right","Unarmed-Strafe-Backward-Right","Unarmed-Strafe-Backward","Unarmed-Strafe-Backward-Left","Unarmed-Strafe-Left","Unarmed-Strafe-Forward-Left",
            "Unarmed-Sprint"};
        // Travel speeds (m/s): the walks give way to the runs between WalkTop and RunFrom; the sprint takes over between
        // SprintFrom and SprintTop going forward (the game's run is 6.8, its sprint 1.35 times that). The legs take the clips
        // from Moving over Ramp m/s, and the rate stays within RateMin..RateMax of the clips' own.
        public const float WalkTop=2.6f, RunFrom=4.0f, SprintFrom=7.4f, SprintTop=8.6f, SprintCone=.6f, Moving=.35f, Ramp=.6f, RateMin=.6f, RateMax=2.0f;

        // The legs' share of the clips for a body travelling at `speed` (0 below Moving, 1 from Moving+Ramp, eased).
        public static float LegWeight(float speed)
        {
            float t=Math.Max(0,Math.Min(1,(speed-Moving)/Ramp));return t*t*(3-2*t);
        }

        // Fills `weights` (Clips long) for the body's local travel (vx, vz in m/s) and returns the playback rate that makes
        // the blended clips travel at that speed. `speedX`/`speedZ` are each clip's own travel on the body (m/s, Clips long;
        // AnimationClip.averageSpeed times the avatar's human scale). Allocation free.
        public static float Weights(float vx,float vz,float[] speedX,float[] speedZ,float[] weights)
        {
            Array.Clear(weights,0,Clips);
            float speed=(float)Math.Sqrt(vx*vx+vz*vz);
            if(speed<1e-4f){weights[Walks]=1;return 1;}
            // Direction: the two neighbouring sectors, by angle.
            float angle=(float)Math.Atan2(vx,vz);if(angle<0)angle+=2*(float)Math.PI;
            float sector=angle/(2*(float)Math.PI)*Directions;int a=(int)Math.Floor(sector)%Directions,b=(a+1)%Directions;float f=sector-(float)Math.Floor(sector);
            // Gait: walk to run by speed; the sprint forward only, past the run.
            float run=Smooth((speed-WalkTop)/(RunFrom-WalkTop));
            float ahead=vz/speed,sprint=ahead>SprintCone?Smooth((speed-SprintFrom)/(SprintTop-SprintFrom))*Smooth((ahead-SprintCone)/(1-SprintCone)):0;
            float keep=1-sprint;
            weights[a]+=(1-f)*run*keep;weights[b]+=f*run*keep;
            weights[Walks+a]+=(1-f)*(1-run)*keep;weights[Walks+b]+=f*(1-run)*keep;
            weights[Sprint]=sprint;
            // The rate: the actual speed over the blended clips' travel along it.
            float bx=0,bz=0;for(int i=0;i<Clips;i++){bx+=weights[i]*speedX[i];bz+=weights[i]*speedZ[i];}
            float along=(bx*vx+bz*vz)/speed;if(!(along>.05f))return 1;
            return Math.Max(RateMin,Math.Min(RateMax,speed/along));
        }
        private static float Smooth(float t){t=Math.Max(0,Math.Min(1,t));return t*t*(3-2*t);}

        // The blended cycle's length (s) for the weights: the clips' lengths, weighted.
        public static float Cycle(float[] weights,float[] lengths)
        {
            float sum=0,w=0;for(int i=0;i<Clips;i++){sum+=weights[i]*lengths[i];w+=weights[i];}return w>1e-6f?sum/w:1;
        }
    }
}
