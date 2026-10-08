using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    public struct NativePredictionState
    {public V3 Position,Velocity;public float Speed,Gravity,JumpSpeed;public bool Grounded,CanMove,CanSprint;}
    // Only locomotion is predicted. Damage, resources, cooldowns and hit outcomes remain authority-owned.
    public sealed class NativePrediction
    {
        private readonly List<InputFrame> pending=new List<InputFrame>();
        private int acknowledged=-1,lastSequence=-1;
        public NativePredictionState State{get;private set;}
        public int Pending=>pending.Count;
        public void Reset(NativePredictionState state,int sequence=-1)
        {State=state;pending.Clear();acknowledged=lastSequence=sequence;}
        public void Push(InputFrame input,Func<V3,V3,V3> constrain)
        {
            if(!input.Valid||input.Sequence<=lastSequence)return;
            lastSequence=input.Sequence;
            if(pending.Count==120)pending.RemoveAt(0);
            pending.Add(input);State=Step(State,input,constrain);
        }
        public void Reconcile(NativePredictionState authority,int sequence,Func<V3,V3,V3> constrain)
        {
            if(sequence<acknowledged)return;
            acknowledged=sequence;lastSequence=Math.Max(lastSequence,sequence);
            pending.RemoveAll(i=>i.Sequence<=sequence);State=authority;
            foreach(var input in pending)State=Step(State,input,constrain);
        }
        private static float Towards(float from,float to,float amount)=>from<to?Math.Min(from+amount,to):Math.Max(from-amount,to);
        public static NativePredictionState Step(NativePredictionState state,InputFrame raw,Func<V3,V3,V3> constrain)
        {
            var input=raw.Sanitized();float dt=Rules.Dt;
            if(!state.CanMove)return state;
            float radians=input.Yaw*(float)Math.PI/180;
            float dot=input.X*(float)Math.Sin(radians)+input.Z*(float)Math.Cos(radians);
            bool sprint=input.Sprint&&state.CanSprint&&!input.Shoot&&!input.Aim&&dot>=.6f;
            float speed=state.Speed*(sprint?Rules.SprintMultiplier:1);
            state.Velocity.x=Towards(state.Velocity.x,input.X*speed,Rules.Acceleration*dt);
            state.Velocity.z=Towards(state.Velocity.z,input.Z*speed,Rules.Acceleration*dt);
            if(input.Jump&&state.Grounded){state.Velocity.y=state.JumpSpeed;state.Grounded=false;}
            if(!state.Grounded)state.Velocity.y-=state.Gravity*dt;else state.Velocity.y=0;
            var wanted=state.Position+state.Velocity*dt;
            var next=constrain==null?wanted:constrain(state.Position,wanted);
            if(next.y>wanted.y+.00001f&&state.Velocity.y<0){state.Grounded=true;state.Velocity.y=0;}
            state.Position=next;return state;
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
