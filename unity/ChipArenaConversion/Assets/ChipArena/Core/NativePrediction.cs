using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public struct NativePredictionState
    {
        public V3 Position,Velocity;public float Speed,Gravity,JumpSpeed;public bool Grounded,CanMove,CanSprint,Sprinting;
        public float Time,Ready,Stamina,BufferUntil,ComboUntil,RollUntil;public int Combo;
        public float AirborneAt;public bool AirAttackUsed;public string JumpSpec;public int AirJumps;public float AirJumpAt;public bool Swimming;
        public ContentWeapon MeleeWeapon;public ChipStats MeleeStats;public NativeMeleeSwing Swing;
        public NativeMeleeKit MeleeKit;public float OverdriveAt,OverdriveUntil;
    }
    // Locomotion and local swing commitment are predicted; hits, damage and real resources stay authority-owned.
    public sealed class NativePrediction
    {
        private readonly List<InputFrame> pending=new List<InputFrame>();
        private int acknowledged=-1,lastSequence=-1;
        public NativePredictionState State{get;private set;}
        public int Pending=>pending.Count;
        public void Reset(NativePredictionState state,int sequence=-1)
        {State=state;pending.Clear();acknowledged=lastSequence=sequence;}
        public void Push(InputFrame input,Func<V3,V3,V3> constrain,Func<V3,bool> grounded=null,Func<int,V3?> lockTarget=null)
        {
            if(!input.Valid||input.Sequence<=lastSequence)return;
            lastSequence=input.Sequence;
            if(pending.Count==120)pending.RemoveAt(0);
            pending.Add(input);State=Step(State,input,constrain,grounded,lockTarget);
        }
        public void Reconcile(NativePredictionState authority,int sequence,Func<V3,V3,V3> constrain,Func<V3,bool> grounded=null,Func<int,V3?> lockTarget=null)
        {
            if(sequence<acknowledged)return;
            acknowledged=sequence;lastSequence=Math.Max(lastSequence,sequence);
            pending.RemoveAll(i=>i.Sequence<=sequence);State=authority;
            foreach(var input in pending)State=Step(State,input,constrain,grounded,lockTarget);
        }
        private static float Towards(float from,float to,float amount)=>from<to?Math.Min(from+amount,to):Math.Max(from-amount,to);
        public static NativePredictionState Step(NativePredictionState state,InputFrame raw,Func<V3,V3,V3> constrain,Func<V3,bool> grounded=null,Func<int,V3?> lockTarget=null)
        {
            var input=raw.Sanitized();float dt=Rules.Dt;
            state.Time+=dt;if(state.Grounded){state.AirJumps=0;state.AirborneAt=-1;state.AirAttackUsed=false;}else if(state.AirborneAt<0)state.AirborneAt=state.Time;
            if(state.MeleeWeapon!=null&&input.Attack&&state.Time<state.RollUntil&&state.Time>=state.RollUntil-.2f)state.BufferUntil=state.Time+NativeMeleeFeel.BufferSeconds;
            if(!state.CanMove)return state;
            bool kitBusy=NativeWhirlwindRules.Busy(state.MeleeKit,state.Time),whirling=NativeWhirlwindRules.Active(state.MeleeKit,state.Time);
            bool swinging=!kitBusy&&NativeMeleePrediction.Advance(ref state,input,lockTarget),rooted=swinging||kitBusy&&!whirling;
            float radians=input.Yaw*(float)Math.PI/180;
            float dot=input.X*(float)Math.Sin(radians)+input.Z*(float)Math.Cos(radians);
            bool sprint=!kitBusy&&input.Sprint&&state.CanSprint&&!input.Shoot&&!input.Aim&&dot>=.6f;
            state.Sprinting=sprint&&!swinging;
            float speed=state.Speed*(sprint?Rules.SprintMultiplier:1)*(whirling?NativeWhirlwindRules.MoveScale:1)*(NativeRangedRules.Overdrive(state.Time,state.OverdriveAt,state.OverdriveUntil)?NativeRangedRules.OverdriveMove:1);
            // A landing attack carries its committed horizontal velocity (x .6) through the hang and plunge.
            bool carried=swinging&&NativeAirAttackSimulation.Falling(state.Swing);
            state.Velocity.x=carried?state.Swing.Carry.x:rooted?0:Towards(state.Velocity.x,input.X*speed,Rules.Acceleration*dt);
            state.Velocity.z=carried?state.Swing.Carry.z:rooted?0:Towards(state.Velocity.z,input.Z*speed,Rules.Acceleration*dt);
            var jump=NativeJumpSimulation.Try(state.JumpSpec,input.Jump,state.Grounded,state.Swimming,kitBusy||swinging||state.Time<state.RollUntil,
                state.Velocity.y,state.JumpSpeed,ref state.AirJumps,out float launch);
            if(jump!=NativeJumpKind.None){state.Velocity.y=launch;state.Grounded=false;if(jump==NativeJumpKind.Air)state.AirJumpAt=state.Time;}
            if(!state.Grounded)state.Velocity.y-=state.Gravity*dt;else state.Velocity.y=0;
            if(NativeAirAttackSimulation.Falling(state.Swing)&&(!NativeAirAttackSimulation.Down(state.Swing)||state.Time>=state.Swing.PlungeAt))state.Velocity.y=NativeAirAttackSimulation.Vertical(state.Swing,state.Time);
            var step=swinging&&(state.Grounded||NativeSprintAttack.Hop(state.Swing))?NativeMeleeRules.Step(state.Swing,state.Time,dt):new V3();
            var wanted=state.Position+(state.Velocity+step)*dt;
            var next=constrain==null?wanted:constrain(state.Position,wanted);
            if(next.y>wanted.y+.00001f&&state.Velocity.y<0){state.Grounded=true;state.Velocity.y=0;}
            if(grounded!=null){state.Grounded=state.Velocity.y<=0&&grounded(next);if(state.Grounded)state.Velocity.y=0;}
            if(state.Grounded){state.AirJumps=0;if(NativeAirAttackSimulation.Falling(state.Swing)){state.Swing.FallHeight=Math.Max(0,state.Swing.AirStartY-next.y);NativeAirAttackSimulation.Land(state.Swing,state.Time);state.Ready=state.Swing.Until;state.ComboUntil=NativeAirAttackSimulation.ComboUntil(state.Swing);}}state.Position=next;return state;
        }
    }
    // A bounded clock on the host snapshot timeline, rather than frame-rate-dependent lerp-to-latest.
    public sealed class NativeSnapshotClock
    {
        public double Time{get;private set;}public double Latest{get;private set;}
        public void Reset(double time){Latest=time;Time=time-.1;}
        public void Observe(double time){Latest=Math.Max(Latest,time);if(Latest-Time>.5)Time=Latest-.1;}
        public double Advance(float delta){Time=Math.Min(Latest,Time+Math.Max(0,Math.Min(.1,delta)));return Time;}
    }
}
