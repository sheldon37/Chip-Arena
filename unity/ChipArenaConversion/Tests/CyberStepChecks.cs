using System;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class CyberStepChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool value,string why){checks++;if(!value)throw new Exception("Cyber step: "+why);}
        void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.0002f,why+": "+a+" != "+b);
        NativeCyberKit Kit(int combo,bool overclock=false)
        {
            var s=new NativeCyberKit{Combo=combo-1,LastComboEnd=10};NativeCyberKit2Rules.Ensure(s);
            if(overclock){s.Kit2.OverclockAt=9;s.Kit2.OverclockUntil=20;}
            float mana=100;Check(NativeCyberKit2Rules.Begin(s,2,10,ref mana,1,1,1,1),"begin");s.Facing=V3.Forward(37);return s;
        }
        var expected=new[]{.4f,.25f,.3f,.2f,.45f};
        for(int combo=0;combo<5;combo++)foreach(bool fast in new[]{false,true})
        {
            var kit=Kit(combo,fast);float release=kit.Release,until=kit.Until;var step=NativeCyberStep.Begin(kit,true,0,default,null);
            Near(step.Distance,expected[combo],"distance "+combo);float rate=fast?1.6f:1;
            Near(kit.Release-10,CyberKit2Spec.ComboWindups[combo]/rate,"unchanged hit time");
            Near(kit.Until-10,CyberKit2Spec.ComboSeconds[combo]/rate,"unchanged duration");
            var baseline=NativeCyberStep.Begin(Kit(combo),true,0,default,null);
            Near((step.At-10)*rate,baseline.At-10,"Overclock step start");Near((step.Until-10)*rate,baseline.Until-10,"Overclock step end");
            foreach(int hz in new[]{60,144,240})
            {
                var moving=step;V3 pos=default;float prev=0;
                for(int i=0;i<=hz;i++){float time=10+i/(float)hz;var delta=NativeCyberStep.Advance(ref moving,kit,time,true,pos,null);pos+=delta;Check(moving.Travelled>=prev&&moving.Travelled<=expected[combo]+.0001f,"bounded monotone");prev=moving.Travelled;}
                Near(pos.Length,expected[combo],"complete travel");Near(V3.Dot(pos,kit.Facing),expected[combo],"locked facing");
            }
            Near(kit.Release,release,"hit unchanged after movement");Near(kit.Until,until,"end unchanged after movement");
            var air=NativeCyberStep.Begin(kit,false,0,default,null);Near(NativeCyberStep.Advance(ref air,kit,11,true,default,null).Length,0,"air start cannot step after landing");
            if(combo==0)Check(step.Distance*step.Fraction(release)>.22f&&step.Distance*step.Fraction(release)<.24f,"jab impact travel");
            if(combo==4)Near(step.Distance*step.Fraction(release),.45f,"palm impact travel");
        }
        var forward=new V3(0,0,1);
        Near(NativeCyberStep.TargetLimit(new V3(0,0,1.2f),forward,.4f,0),.2f,"one metre clearance");
        Near(NativeCyberStep.TargetLimit(new V3(0,0,.7f),forward,.4f,0),0,"already close");
        Near(NativeCyberStep.TargetLimit(new V3(0,0,-1.2f),forward,.4f,0),.4f,"behind outside cone");
        Near(NativeCyberStep.TargetLimit(new V3(.6f,0,1),forward,.4f,0),.2f,"off-axis clearance sphere");
        Near(NativeCyberStep.TargetLimit(new V3(0,0,3),forward,.4f,0),.4f,"outside reach");
        {
            var kit=Kit(0);var step=NativeCyberStep.Begin(kit,true,0,default,null);V3 pos=default;
            pos+=NativeCyberStep.Advance(ref step,kit,10.06f,true,pos,null);
            pos+=NativeCyberStep.Advance(ref step,kit,10.08f,true,pos,(p,f,d,c,t)=>Math.Min(d,.01f));
            Near(step.Distance,pos.Length,"dynamic obstruction records actual travel");
            Near(NativeCyberStep.Advance(ref step,kit,11,true,pos,null).Length,0,"blocked step never resumes");
            var restored=NativeCyberStep.Restore(kit,step.At,step.Until,step.Distance,10.08f);
            Near(NativeCyberStep.Advance(ref restored,kit,10.1f,true,pos,null).Length,0,"shortened snapshot never adds residual travel");
        }
        foreach(bool interrupt in new[]{false,true})
        {
            var kit=Kit(2);var step=NativeCyberStep.Begin(kit,true,0,default,null);V3 pos=NativeCyberStep.Advance(ref step,kit,10.06f,true,default,null);
            if(interrupt)NativeCyberRules.Interrupt(kit,10.07f);
            Near(NativeCyberStep.Advance(ref step,kit,10.08f,interrupt,pos,null).Length,0,"cut or airborne halts step");Near(step.Distance,pos.Length,"cut distance");
        }
        {
            var kit=Kit(0);var state=new NativePredictionState{Time=10-Rules.Dt,CanMove=true,Grounded=true,Cyber=kit,CyberRate=1,CyberMana=100,Ready=11,Speed=6.8f};
            var host=NativeCyberStep.Begin(kit,true,0,default,(p,f,d,c,t)=>Math.Min(d,.25f));V3 pos=default;
            for(int n=0;n<12;n++)
            {
                state=NativePrediction.Step(state,new InputFrame{Sequence=n,X=1,Yaw=170},(a,b)=>b,p=>true,null,(p,f,d,c,t)=>Math.Min(d,.25f));
                pos+=NativeCyberStep.Advance(ref host,kit,state.Time,true,pos,null);Near((state.Position-pos).Length,0,"owner equals host without steering");
            }
            var at=NativeCyberStep.Restore(kit,state.CyberStep.At,state.CyberStep.Until,state.CyberStep.Distance,state.Time);Near(at.Travelled,state.CyberStep.Travelled,"snapshot restoration");
            var actor=new Actor{Cyber=kit,StepAt=host.At,StepUntil=host.Until,StepDistance=host.Distance};var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
            var copy=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(actor,options),options);Near(copy.StepDistance,host.Distance,"existing wire field roundtrip");
        }
        {
            var kit=new NativeCyberKit();NativeCyberKit2Rules.Ensure(kit);
            var initial=new NativePredictionState{Time=5,CanMove=true,Grounded=true,Cyber=kit,CyberRate=1,CyberMana=100,Speed=6.8f};
            var replay=new NativePrediction();replay.Reset(initial);NativePredictionState ack=default;
            for(int n=0;n<10;n++){replay.Push(new InputFrame{Sequence=n,Shoot=n==0,Yaw=n==0?0:90,X=1},(a,b)=>b,p=>true);if(n==3)ack=replay.State;}
            var expectedState=replay.State;replay.Reconcile(ack,3,(a,b)=>b,p=>true);
            Near((expectedState.Position-replay.State.Position).Length,0,"pending input replay");Near(expectedState.CyberStep.Travelled,replay.State.CyberStep.Travelled,"replay travel not doubled");
            Check(ack.CyberStep.Travelled<replay.State.CyberStep.Travelled,"snapshot value state isolated");
        }
        Console.WriteLine("PASS Cyber melee step: "+checks+" checks (five windows/rates, clearance, cuts, replay, wire)");
    }
}
