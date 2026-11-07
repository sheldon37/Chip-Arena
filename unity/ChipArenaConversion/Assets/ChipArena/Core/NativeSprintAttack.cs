using System;

namespace ChipArena.UnityConversion
{
    // One authoritative profile for host commitment, guest prediction and replicated swing timing.
    public static class NativeSprintAttack
    {
        public static bool Starts(bool chained,int index,bool grounded,InputFrame input,bool sprinting)=>
            !chained&&index==1&&grounded&&input.Attack&&NativeMeleeFeel.SprintAttack(input,sprinting);
        public static bool Hop(NativeMeleeSwing s)=>s!=null&&s.SprintAttack&&s.Spec=="greatsword";
        public static float HopSpeed(NativeMeleeSwing s,float gravity)=>gravity*(s.HitAt-s.At)*.5f;
        public static float Cost(ContentWeapon w,ChipStats stats,bool sprint)=>NativeMeleeFeel.Cost(w,stats)*(sprint?1.3f:1);
        public static void Apply(NativeMeleeSwing s,float rate)
        {
            s.SprintAttack=true;s.OpenerLunge=true;
            float wind=s.Spec=="sword"?.16f:s.Spec=="greatsword"?.48f:.12f;
            float active=s.Spec=="sword"?.18f:s.Spec=="daggers"?.24f:.16f;
            float recovery=s.Spec=="sword"?.45f:s.Spec=="shield"?.5f:s.Spec=="greatsword"?.7f:.25f;
            s.HitAt=s.At+wind/rate;s.HitUntil=s.HitAt+active/rate;s.Until=s.HitUntil+recovery/rate;
            s.StepAt=s.At;s.StepUntil=s.HitAt+(s.Spec=="greatsword"?0:active*.5f/rate);
            s.Distance=s.Spec=="sword"?2.2f:s.Spec=="daggers"?2.6f:s.Spec=="shield"?2:2.4f;
            s.DamageScale=s.Spec=="sword"?1.2f:s.Spec=="daggers"?.7f:s.Spec=="shield"?.9f:1.4f;
            s.ChainAt=s.Spec=="daggers"?s.HitUntil+.15f/rate:s.Until;
        }
        public static int Pulse(NativeMeleeSwing s,float time)=>s.SprintAttack&&s.Spec=="daggers"&&time>=s.HitAt+(s.HitUntil-s.HitAt)*.5f?1:0;
        public static bool Ring(NativeMeleeSwing s,float time,V3 delta,float extra,bool grounded)=>
            Hop(s)&&grounded&&time>=s.HitAt&&time<=s.HitUntil&&Math.Abs(delta.y)<=.8f+extra&&new V3(delta.x,0,delta.z).Length<=1.6f+extra;
        // Existing knockback loses exp(-3.1*dt) per tick before integration; choose an unopposed 2.5 m travel.
        public static float Impulse(NativeMeleeSwing s,float normal)=>s.SprintAttack&&s.Spec=="shield"?65*2.5f*((float)Math.Exp(3.1f*Rules.Dt)-1)/Rules.Dt:normal*s.DamageScale;
    }
}
