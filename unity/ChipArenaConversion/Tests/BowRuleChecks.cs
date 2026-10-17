using System;
using ChipArena.UnityConversion;
internal static class BowRuleChecks
{
    static void Check(bool b,string m){if(!b)throw new Exception("Bow: "+m);}
    public static void Run()
    {
        Check(NativeBowRules.Fraction(.249f,1)==0&&NativeBowRules.Fraction(.8f,1)==1&&NativeBowRules.Fraction(.4f,2)==1,"tap and rate-scaled full draw");
        Check(NativeBowRules.Damage(0)==18&&NativeBowRules.Damage(1)==62&&NativeBowRules.Speed(1)==110&&NativeBowRules.Gravity(1)==14&&NativeBowRules.Impulse(1)==300,"ballistics endpoints");
        Check(Math.Abs(NativeBowRules.Spread(3.3f,1)-.0015f)<.00001&&Math.Abs(NativeBowRules.Spread(4.8f,1)-.01f)<.00001,"steady hold then fatigue");
        for(int i=0;i<12;i++)Check(NativeBowRules.RainOffset(i).Length<=3.5f,"rain stays in exact circle");
        var s=new NativePredictionState{Bow=true,BowDrawAt=-1,CanMove=true,CanSprint=true,Speed=10,Grounded=true,Stamina=100};
        for(int i=0;i<30;i++)s=NativePrediction.Step(s,new InputFrame{Shoot=true,Z=1},(a,b)=>b,p=>true);
        Check(s.BowDrawAt>=0&&Math.Abs(s.Velocity.z-7)<.001,"drawing prediction slows movement");
        s=NativePrediction.Step(s,new InputFrame{},(a,b)=>b,p=>true);Check(s.BowDrawAt<0&&Math.Abs(s.Ready-s.Time-.35f)<.001,"release nock");
        s=NativePrediction.Step(s,new InputFrame{Shoot=true},(a,b)=>b,p=>true);Check(s.BowDrawAt<0,"cannot draw during nock");
        s.Ready=0;s=NativePrediction.Step(s,new InputFrame{Shoot=true},(a,b)=>b,p=>true);s=NativePrediction.Step(s,new InputFrame{Block=true,Shoot=true},(a,b)=>b,p=>true);Check(s.BowDrawAt<0,"RMB cancels");
        Console.WriteLine("PASS Bow rules and prediction: draw scaling, ballistics, fatigue, rain bounds, slow, nock and cancel");
    }
}
