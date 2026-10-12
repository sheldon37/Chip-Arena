using System;
namespace ChipArena.UnityConversion
{
    public static class NativeAirAttackSimulation
    {
        public const float MinimumAir=.15f,Hang=.15f,PlungeSpeed=-14;
        public static float Recovery(string spec)=>spec=="daggers"?.25f:spec=="sword"?.35f:spec=="shield"?.4f:.55f;
        public static float Radius(string spec)=>spec=="daggers"?1.4f:spec=="sword"?1.6f:spec=="shield"?1.8f:2.4f;
        // Owner 2026-10-12: the attack keeps .6 of the horizontal speed it was committed with until the impact.
        public const float CarryScale=.6f;
        // Lava swimming is not an airtime: it keeps its ordinary swings (NativeHazards.InLava / NativePredictionState.Swimming).
        public static bool Airborne(bool grounded,bool swimming)=>!grounded&&!swimming;
        public static void Carry(NativeMeleeSwing s,V3 velocity){s.Carry=new V3(velocity.x*CarryScale,0,velocity.z*CarryScale);}
        // The combo after the impact: only the daggers carry theirs on; the other weapons start again from cut A.
        public static float ComboUntil(NativeMeleeSwing s)=>s.Spec=="daggers"?s.Until+NativeMeleeFeel.ComboPause:s.ImpactAt;
        public static bool Starts(bool grounded,float airborneAt,bool used,float now,InputFrame input)=>!grounded&&airborneAt>=0&&!used&&now-airborneAt>=MinimumAir&&input.Attack;
        public static void Apply(NativeMeleeSwing s)
        {s.Air=true;s.SprintAttack=s.OpenerLunge=false;s.PlungeAt=s.At+Hang;s.ImpactAt=-1;s.HitAt=s.PlungeAt;s.HitUntil=s.Until=s.ChainAt=s.At+10;s.Distance=0;s.StepAt=s.StepUntil=s.At;s.DamageScale=s.Spec=="greatsword"?1.5f:1.3f;}
        public static bool Falling(NativeMeleeSwing s)=>s?.Air==true&&s.ImpactAt<0;
        public static float Vertical(NativeMeleeSwing s,float now)=>now<s.PlungeAt?0:PlungeSpeed;
        public static void Land(NativeMeleeSwing s,float now)
        {if(!Falling(s))return;s.PlungeAt=Math.Min(s.PlungeAt,now);s.ImpactAt=s.HitAt=now;s.HitUntil=now+Rules.Dt*.5f;s.Until=s.ChainAt=now+Recovery(s.Spec);}
        public static bool Contact(NativeMeleeSwing s,float time,V3 delta,float extra)
        {
            if(s.ImpactAt<0||time<s.ImpactAt||time>s.HitUntil||Math.Abs(delta.y)>.8f+extra)return false;
            float d=new V3(delta.x,0,delta.z).Length;
            return d<=Radius(s.Spec)+extra||d<=s.Range+extra&&V3.Dot(new V3(delta.x,0,delta.z).Normalized,V3.Forward(s.Yaw*180/(float)Math.PI))>=Math.Cos(Math.Min(Math.PI,s.Arc)*.5);
        }
    }
}
