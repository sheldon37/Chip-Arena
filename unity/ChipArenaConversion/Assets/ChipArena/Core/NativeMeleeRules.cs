using System;
using System.Collections.Generic;

namespace ChipArena.UnityConversion
{
    // Primary-only contracts from souls-combat.ts / simulation.ts / blocking.ts at 05950ea.
    // Angles here are native radians (+x/right); source fixtures reflect x and yaw. Input/camera yaw remains degrees.
    public sealed class NativeMeleeSwing
    {
        public string Spec;public int Index,Id,Remaining;public bool Followup,SprintAttack,OpenerLunge,Air;public float PlungeAt,ImpactAt=-1;public float ChainAt;
        // A landing attack's carried horizontal velocity (native m/s) through its hang and plunge (NativeAirAttackSimulation.Carry).
        public V3 Carry;
        // Prediction never resolves contacts; copy before mutating a replay state.
        public NativeMeleeSwing PredictionCopy()=>(NativeMeleeSwing)MemberwiseClone();public float DamageScale=1;public float At,HitAt,HitUntil,Until,Yaw,StepAt,StepUntil,Distance,Range,Arc;
        private readonly HashSet<string> hit=new HashSet<string>();
        public bool Contact(string id,float time,V3 delta,float extra,bool visible,bool grounded=false)
        {
            id+=":"+NativeSprintAttack.Pulse(this,time);
            if(!visible||hit.Contains(id)||!NativeMeleeRules.Contact(this,time,delta,extra)&&!NativeSprintAttack.Ring(this,time,delta,extra,grounded))return false;
            hit.Add(id);return true;
        }
    }
    public static class NativeMeleeRules
    {
        public static bool Standard(string spec)=>spec=="sword"||spec=="daggers"||spec=="shield"||spec=="greatsword";
        public static float Recovery(string spec)=>spec=="greatsword"?.5f:spec=="daggers"?.15f:.3f;
        public static NativeMeleeSwing Begin(ContentWeapon w,ChipStats stats,float at,int index,float yaw)
        {
            float rate=(float)stats["attackRate"],windup=(float)w.windup/rate,active=.16f/rate;
            return new NativeMeleeSwing{Spec=w.id,Index=index,At=at,Yaw=yaw,HitAt=at+windup,HitUntil=at+windup+active,
                Until=at+Math.Max((float)w.interval/rate,windup+active+Recovery(w.id)/rate),StepAt=at+windup*.55f,StepUntil=at+windup+active*.8f,
                Distance=w.id=="daggers"?.85f:w.id=="sword"?.65f:w.id=="shield"?.55f:.45f,Range=(float)(w.range*stats["reach"]),Arc=(float)((w.id=="sword"&&index%3==0?.8:w.arc)*stats["meleeArc"])};
        }
        public static bool Reverse(NativeMeleeSwing s)=>s.Spec=="shield"||s.Spec=="daggers"&&(s.Index%2==0||s.Index%4==3)||s.Spec=="greatsword"&&s.Index%2==0||s.Spec=="sword"&&s.Index%3==2;
        public static float Angle(NativeMeleeSwing s,float time)
        {
            float phase=MathUtil.Clamp((time-s.HitAt)/(s.HitUntil-s.HitAt),0,1);bool reverse=Reverse(s);
            if(s.SprintAttack&&s.Spec=="daggers"){int pulse=NativeSprintAttack.Pulse(s,time);phase=phase*2-pulse;reverse=pulse==1;}
            return s.Yaw-(phase-.5f)*Math.Min((float)Math.PI*2,s.Arc)*(reverse?-1:1);
        }
        public static bool Contact(NativeMeleeSwing s,float time,V3 delta,float extra)
        {
            if(s.Air)return NativeAirAttackSimulation.Contact(s,time,delta,extra);
            if(time<s.HitAt||time>s.HitUntil)return false;
            float distance=delta.Length;if(Math.Abs(delta.y)>1.6f+extra||distance>s.Range+extra)return false;
            float angle=Angle(s,time);var direction=new V3((float)Math.Sin(angle),0,(float)Math.Cos(angle));
            return V3.Dot(new V3(delta.x,0,delta.z).Normalized,direction)>=Math.Cos(.24+Math.Asin(Math.Min(.95,extra/Math.Max(.1,distance))));
        }
        public static V3 Step(NativeMeleeSwing s,float time,float dt)
        {
            if(s==null||dt<=0)return new V3();float span=Math.Max(.01f,s.StepUntil-s.StepAt);
            float speed=s.Distance*(MathUtil.Smooth((time+dt-s.StepAt)/span)-MathUtil.Smooth((time-s.StepAt)/span))/dt;
            return new V3((float)Math.Sin(s.Yaw)*speed,0,(float)Math.Cos(s.Yaw)*speed);
        }
        public static bool BeforeStrike(NativeMeleeSwing s,float time)=>s!=null&&time<s.HitAt-.5f/60;
        // Light direct-hit stun; fast attacks have slightly shorter recovery, and immunity prevents infinite combos.
        public static float HitStun(ContentWeapon w,float rate)
        {
            float baseline=w.id=="daggers"?.12f:w.id=="sword"?.18f:w.id=="shield"?.20f:w.id=="greatsword"?.28f:.18f;
            return Math.Max(.09f,Math.Min(.32f,baseline/Math.Max(.75f,rate)));
        }
        public static float FighterHitStun(bool emperor,bool heavy,float rate)
        {
            float baseline=heavy?.22f:emperor?.12f:.10f;
            return Math.Max(.08f,Math.Min(.26f,baseline/Math.Max(.75f,rate)));
        }
        public static bool StaggerReady(float now,float stunUntil,float immuneUntil)=>now>=stunUntil&&now>=immuneUntil;
        public static float StaggerImmuneThrough(float stunUntil)=>stunUntil+.5f;
        public static float BlockDuration(ChipStats s)=>(float)Math.Max(.1,Math.Min(.65,.3+s["blockWindow"]));
        public static float BlockCooldown(ChipStats s)=>(float)Math.Max(1.5,4*s["blockCooldown"]);
        public static float BlockStun(float damage,float impulse)=>MathUtil.Clamp(.18f+Math.Max(0,damage)*.008f+Math.Max(0,impulse)*.00025f,.25f,.95f);
        public static float BlockPunish(float damage,float ready,float travel)=>Math.Min(1.4f,Math.Max(0,ready)+1+Math.Max(0,damage)*.004f)+Math.Max(0,travel);
        public static float Rebound(float damage,float force)=>Math.Min(800,(160+Math.Max(0,damage)*4)*force);
        public static float ReboundTravel(float damage,float force,float attackerDamage,float taken,float speed)
        {
            float baseline=Math.Min(800,160+4*Math.Max(0,damage)/Math.Max(1,attackerDamage));
            return Math.Min(1,Math.Max(0,force*taken-baseline)/65/3.1f/(6.8f*Math.Max(.25f,speed)));
        }
        // Continuous external velocity plus a one-shot vertical kick, as in shared/movement.ts.
        public static V3 External(ref V3 remaining,float dt)=>External(ref remaining,dt,out _);
        public static V3 External(ref V3 remaining,float dt,out float steeringSpeed,bool preserveHorizontal=false)
        {
            float lift=MathUtil.Clamp(remaining.y,-24,24),decay=preserveHorizontal?1:(float)Math.Exp(-3.1f*dt);
            remaining=new V3(remaining.x*decay,0,remaining.z*decay);float speed=remaining.Length;steeringSpeed=speed;
            if(speed>70)remaining=remaining*(70/speed);return remaining+new V3(0,lift,0);
        }
        public static V3 CounterSteer(V3 move,V3 external,float steeringSpeed=-1)
        {
            float speed=steeringSpeed>=0?steeringSpeed:new V3(external.x,0,external.z).Length;if(speed<=.01f)return move;
            var away=new V3(external.x,0,external.z).Normalized;float against=Math.Min(0,V3.Dot(move,away));
            return move+away*(-against*(1-1/(1+(speed/4)*(speed/4))));
        }
    }
}
