using System;
using System.Numerics;

namespace ChipArena.UnityConversion
{
    /// <summary>
    /// The zombie animation comparison (owner 2026-11-04: "two zombies side by side ... Identical selections, speeds, turn inputs"): the
    /// same authority path through the same presented root (Core/NativeHordeFlow) into two gaits, the left with every stage off (the take as
    /// converted, at its own pace, its legs its own), the right as the game draws it, each of its three stages (NativeHordeGait.StagePlant,
    /// StagePosture, StageRetime) on a switch. The take, the speed, the turn rate and the route are the DEV panel's. Engine-free: the checks
    /// drive it as the panel does. A DEV tool: nothing in the game reads it, and it reads only the takes and the kinds' tables, so a
    /// later pack's cycles compare on it the same way.
    /// </summary>
    public sealed class NativeZombieCompare
    {
        public enum RouteKind{Straight,Circle,Turn45,Turn90,Turn180,Zigzag,Stand90,Stand180}
        public static readonly string[] RouteNames={"Straight","Circle","45 turns","90 turns","180 turns","Zigzag","Stand 90","Stand 180"};
        /// <summary>The kinds it compares (the ones that walk on the capture).</summary>
        public static readonly int[] Kinds={NativeHordeKinds.Shambler,NativeHordeKinds.Runner,NativeHordeKinds.Spitter,NativeHordeKinds.Brute,NativeHordeKinds.Hulk};
        public static readonly string[] KindNames={"Shambler","Runner","Spitter","Brute","Hulk"};
        /// <summary>How long a turning route runs on between its turns (s), and a zigzag between its sides.</summary>
        public const float TurnEvery=3,ZigzagEvery=.33f,ZigzagAngle=60;
        public const int Id=995000;
        public int Kind=NativeHordeKinds.Shambler;
        /// <summary>The take both walk on (NativeZombieClips index; -1: the kind's own, by its seed).</summary>
        public int Take=-1;
        public float Speed=1.5f,TurnRate=90;
        public RouteKind Route=RouteKind.Straight;
        /// <summary>The right gait's stages; the left's are off unless the panel turns one on.</summary>
        public bool Plant=true,Posture=true,Retime=true,LeftPlant,LeftPosture,LeftRetime;
        /// <summary>The legs the strides are planned for (the model's ratio on the model, 1 on the voxel body).</summary>
        public float Legs=1;
        public readonly NativeHordeGait Left=new NativeHordeGait(),Right=new NativeHordeGait();
        public readonly HordeFlow Flow=new HordeFlow();
        public HordeMotionInput Input;
        /// <summary>The authority's body and heading (rig metres on an open floor, Unity yaw) and the clock.</summary>
        public Vector3 Body;public float Heading,Time;
        private float next;private int sign=1;
        public void Reset()
        {
            Time=0;Heading=0;next=TurnEvery;sign=1;Body=new Vector3(0,NativeHordeKinds.Root[NativeHordeKinds.Index(Kind)],0);
            Flow.Reset();Left.Reset();Right.Reset();Input=default;
        }
        /// <summary>The rig's speed for a world speed (the Hulk's rig is a small copy of it).</summary>
        public float RigSpeed=>Speed/NativeHordeKinds.Scale[NativeHordeKinds.Index(Kind)];
        /// <summary>Steps the route by `dt` (0: holds, the frame step and the pause) and poses both gaits.</summary>
        public void Step(float dt,CapturedRig.Pose left,CapturedRig.Pose right)
        {
            if(Input.Id==0)Reset();
            if(dt>0)
            {
                Time+=dt;float speed=RigSpeed;
                switch(Route)
                {
                    case RouteKind.Circle:Heading+=TurnRate*dt;break;
                    case RouteKind.Zigzag:if(Time>=next){next+=ZigzagEvery;sign=-sign;Heading=sign*ZigzagAngle;}break;
                    case RouteKind.Turn45:case RouteKind.Turn90:if(Time>=next){next+=TurnEvery;Heading+=sign*(Route==RouteKind.Turn45?45:90);sign=-sign;}break;
                    case RouteKind.Turn180:if(Time>=next){next+=TurnEvery;Heading+=180;}break;
                    case RouteKind.Stand90:case RouteKind.Stand180:speed=0;if(Time>=next){next+=TurnEvery;Heading+=Route==RouteKind.Stand90?sign*90:180;sign=-sign;}break;
                }
                float r=Heading*BossMath.Deg;var v=new Vector3((float)Math.Sin(r),0,(float)Math.Cos(r))*speed;Body+=v*dt;
                Flow.Configure(Kind);Flow.Host(Time,Time,Body,v,true,Heading,dt,false);
                Input=new HordeMotionInput{Kind=Kind,Id=Id,Seed=NativeHordeKinds.Seed(Id),Health=1,AttackAge=float.NaN,Flow=true,Tier=NativeHordeModels.Full,Time=Time,Root=Flow.Position,Velocity=Flow.Velocity,Yaw=Flow.Yaw,YawRate=Flow.YawRate,Bank=Flow.Bank,Pitch=Flow.Pitch,Ground=Flow.Position.Y-NativeHordeKinds.Root[NativeHordeKinds.Index(Kind)]};
            }
            if(Input.Id==0)return;
            Stages(Left,LeftPlant,LeftPosture,LeftRetime);Stages(Right,Plant,Posture,Retime);
            Left.Pose(Input,left);Right.Pose(Input,right);
        }
        private void Stages(NativeHordeGait g,bool plant,bool posture,bool retime){g.Legs=Legs;g.ForceTake=Take;g.StagePlant=plant;g.StagePosture=posture;g.StageRetime=retime;}
        /// <summary>The right gait's playback over the take's own pace, its stride (m a cycle) over the capture's on these legs, its steps a second and the capture's.</summary>
        public void Readout(out float rate,out float stretch,out float steps,out float ownSteps)
        {
            int c=Right.RunNow>.5f?Right.RunClip:Right.WalkClip;float d=c<0?1:Math.Max(.1f,NativeZombieClips.Duration(c));
            rate=Right.CycleRate*d;steps=2*Right.CycleRate;ownSteps=2/d;
            float own=c<0?1:Math.Max(.01f,NativeZombieMotion.OwnStrideOf(c)*Math.Max(1,Legs));stretch=Right.CycleRate>.01f?RigSpeed/Right.CycleRate/own:0;
        }
    }
}
