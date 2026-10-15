using System;
namespace ChipArena.UnityConversion
{
    // Value state: replay never mutates an acknowledged snapshot or an earlier prediction.
    public struct NativeCyberStep
    {
        public bool Present,Stopped;public int Combo,LockTarget;
        public float CastAt,At,Until,Distance,Travelled;public V3 Facing;
        public static float Arc(int combo)=>combo==2?CyberKit2Spec.SpinArc:combo==4?CyberKit2Spec.PalmCone:CyberKit2Spec.ComboArc;
        public static bool Matches(NativeCyberKit kit,in NativeCyberStep step)=>step.Present&&kit?.Version==2&&kit.At==step.CastAt&&NativeCyberKit2Rules.Current(kit)==2;
        public static NativeCyberStep Begin(NativeCyberKit kit,bool grounded,int target,V3 position,Func<V3,V3,float,int,int,float> limit)
        {
            if(kit?.Version!=2||NativeCyberKit2Rules.Current(kit)!=2)return default;
            var window=new CyberCombo.Swing(kit.Combo,kit.At,kit.Release,kit.Until);
            float distance=grounded?CyberCombo.StepDistance[kit.Combo]:0;
            if(limit!=null&&distance>0)distance=MathUtil.Clamp(limit(position,kit.Facing,distance,kit.Combo,target),0,distance);
            return new NativeCyberStep{Present=true,CastAt=kit.At,At=window.StepAt,Until=window.StepUntil,Distance=distance,Facing=kit.Facing,Combo=kit.Combo,LockTarget=target,Stopped=!grounded};
        }
        public float Fraction(float time)=>MathUtil.Smooth((time-At)/Math.Max(.001f,Until-At));
        // Exact intersection of the forward segment with a one-metre centre-clearance sphere.
        public static float TargetLimit(V3 delta,V3 facing,float distance,int combo)
        {
            if(!NativeCyberRules.Cone(delta,facing,CyberKit2Spec.ComboRange,Arc(combo)))return distance;
            float squared=V3.Dot(delta,delta);if(squared<=1)return 0;
            float along=V3.Dot(delta,facing),sideSquared=squared-along*along;
            if(along<=0||sideSquared>=1)return distance;
            return Math.Min(distance,Math.Max(0,along-(float)Math.Sqrt(1-sideSquared)));
        }
        public static V3 Advance(ref NativeCyberStep step,NativeCyberKit kit,float time,bool grounded,V3 position,Func<V3,V3,float,int,int,float> limit)
        {
            if(!Matches(kit,step)) {step=default;return default;}
            if(step.Stopped)return default;
            if(!grounded||kit.CutAt>=kit.At){step.Distance=step.Travelled;step.Until=Math.Max(step.At+.001f,Math.Min(step.Until,time));step.Stopped=true;return default;}
            float wanted=Math.Max(0,step.Distance*step.Fraction(time)-step.Travelled);
            float allowed=limit==null?wanted:MathUtil.Clamp(limit(position,step.Facing,wanted,step.Combo,step.LockTarget),0,wanted);
            step.Travelled+=allowed;
            if(allowed+1e-5f<wanted){step.Distance=step.Travelled;step.Until=Math.Max(step.At+.001f,Math.Min(step.Until,time));step.Stopped=true;}
            return step.Facing*allowed;
        }
        public static NativeCyberStep Restore(NativeCyberKit kit,float at,float until,float distance,float time)
        {
            var step=Begin(kit,true,0,default,null);if(!step.Present)return step;
            step.At=at;step.Until=until;step.Distance=distance;step.Travelled=distance*step.Fraction(time);return step;
        }
    }
}
