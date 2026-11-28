using System;
using System.Collections.Generic;
using ChipArena.UnityConversion;

internal static class NativeMeleeFeelChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Melee feel: "+why);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    public static void Run(ConversionContent data)
    {
        NativePredictionState State(string spec)=>new NativePredictionState{MeleeWeapon=data.Weapon(spec),MeleeStats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>()),CanMove=true,CanSprint=true,Grounded=true,Stamina=100,Combo=-1,BufferUntil=-1,Speed=6.8f,Gravity=23,JumpSpeed=8.5f};
        foreach(string spec in new[]{"sword","daggers","shield","greatsword"})
        {
            foreach(float rate in new[]{.5f,1,2})
            {
                var state=State(spec);state.MeleeStats["attackRate"]=rate;
                var s=NativeMeleeFeel.Begin(state.MeleeWeapon,state.MeleeStats,2,1,0);
                Check(Near(s.ChainAt-2,NativeMeleeFeel.ChainSeconds(spec)/rate)&&s.ChainAt>=s.HitUntil&&s.ChainAt<s.Until,"exact chain/rate "+spec);
                Check(Near(s.Distance,NativeMeleeFeel.DefaultStep(spec)),"default step "+spec);
            }
            Check(Near(NativeMeleeFeel.Turn(0,180,spec,Rules.Dt),NativeMeleeFeel.TurnRate(spec)*Rules.Dt*(float)Math.PI/180),"turn limit "+spec);
            Check(Near(NativeMeleeFeel.Distance(spec,false,20),NativeMeleeFeel.StepCap(spec,false))&&Near(NativeMeleeFeel.Distance(spec,true,20),NativeMeleeFeel.StepCap(spec,true))&&NativeMeleeFeel.Distance(spec,false,.5f)==0,"lunge caps/overlap "+spec);
            var a=State(spec);a.Time=1;NativeMeleePrediction.Advance(ref a,new InputFrame{Attack=true},null);var first=a.Swing;float paid=a.Stamina;
            a.Time=first.HitAt-.001f;NativeMeleePrediction.Advance(ref a,new InputFrame{Attack=true},null);Check(a.BufferUntil<0,"reject windup buffer "+spec);
            a.Time=first.ChainAt;NativeMeleePrediction.Advance(ref a,new InputFrame{Shoot=true},null);Check(a.Swing.At==first.At,"held input must not create chain "+spec);
            a.Time=first.HitUntil-.001f;NativeMeleePrediction.Advance(ref a,new InputFrame{Attack=true},null);Check(Near(a.BufferUntil,a.Time+.4f),"edge expiry "+spec);
            a.Time=first.ChainAt;NativeMeleePrediction.Advance(ref a,new InputFrame(),null);Check(a.Swing.At==a.Time&&Near(a.Stamina,paid-NativeMeleeFeel.Cost(a.MeleeWeapon,a.MeleeStats)),"released-edge paid chain "+spec);
            a.Time=a.Swing.Until+NativeMeleeFeel.ComboPause+.001f;NativeMeleePrediction.Advance(ref a,new InputFrame{Attack=true},null);Check(a.Combo==0&&a.Swing.Index==1,"combo pause resets opener "+spec);
        }
        var great=State("greatsword");great.Time=1;NativeMeleePrediction.Advance(ref great,new InputFrame{Attack=true},null);var old=great.Swing;
        great.Time=old.HitAt;NativeMeleePrediction.Advance(ref great,new InputFrame{Attack=true},null);great.Time=old.ChainAt;NativeMeleePrediction.Advance(ref great,new InputFrame(),null);
        Check(great.Swing.At==old.At,"early greatsword press expires before chain");
        great.Stamina=0;NativeMeleePrediction.Advance(ref great,new InputFrame{Attack=true},null);Check(great.Swing.At==old.At&&great.Stamina==0,"unfunded chain preserves old recovery");
        var turning=State("sword");turning.Time=1;NativeMeleePrediction.Advance(ref turning,new InputFrame{Attack=true},null);var before=turning.Swing;
        turning.Time=1.05f;NativeMeleePrediction.Advance(ref turning,new InputFrame{Yaw=90},null);Check(before.Yaw==0&&turning.Swing.Yaw>0,"replay state is not aliased");
        float committed=turning.Swing.Yaw;turning.Time=turning.Swing.HitAt;NativeMeleePrediction.Advance(ref turning,new InputFrame{Yaw=180},null);Check(turning.Swing.Yaw==committed,"active yaw locks");
        turning.Time=turning.Swing.ChainAt;NativeMeleePrediction.Advance(ref turning,new InputFrame{Attack=true,Yaw=180},null);Check(turning.Swing.Yaw==committed,"chain starts at previous heading before turning");
        Check(NativeMeleeFeel.Turn(359*(float)Math.PI/180,1,"sword",Rules.Dt)>359*(float)Math.PI/180,"shortest turn across wrap");
        var input=new InputFrame{Sequence=1,Attack=true,Shoot=true,LockTarget=2,Z=1};var held=input.HeldOnly();Check(!held.Attack&&held.Shoot&&held.LockTarget==2,"one-shot wire edge and held lock intent");
        Check(!new InputFrame{LockTarget=-1}.Valid,"invalid lock identity rejected");
        var initial=State("sword");var predictor=new NativePrediction();predictor.Reset(initial,0);predictor.Push(input,null);
        Check(predictor.State.Swing!=null&&predictor.State.Position.z==0&&predictor.State.Velocity.z==0,"guest walking stops on first press, before RTT");
        float stamina=predictor.State.Stamina;predictor.Push(input,null);Check(predictor.Pending==1&&predictor.State.Stamina==stamina,"duplicate sequence not replayed");
        for(int i=2;i<=12;i++)predictor.Push(new InputFrame{Sequence=i,Z=1},null);
        var predicted=predictor.State;predictor.Reconcile(initial,0,null);
        Check(Near(predicted.Position.z,predictor.State.Position.z)&&Near(predicted.Swing.At,predictor.State.Swing.At)&&Near(predicted.Stamina,predictor.State.Stamina),"reconciliation repeats same swing/step/cost once");
        var denied=initial;denied.Stamina=0;predictor.Reconcile(denied,0,null);Check(predictor.State.Swing==null&&predictor.State.Stamina==0,"authority rejection removes predicted attack");
        var locked=State("sword");locked.Time=1;NativeMeleePrediction.Advance(ref locked,new InputFrame{Attack=true,LockTarget=2,Sprint=true,Z=1},id=>new V3(0,0,5));
        Check(locked.Swing.OpenerLunge&&locked.Swing.SprintAttack&&Near(locked.Swing.Distance,2.2f),"predicted opener/sprint flags and lock step");
        var airborne=State("sword");airborne.Grounded=false;airborne.Time=1;NativeMeleePrediction.Advance(ref airborne,new InputFrame{Attack=true,Sprint=true,Z=1},null);Check(!airborne.Swing.SprintAttack,"airborne input is not a sprint attack");
        Console.WriteLine("PASS melee feel: exact rates/chains/steps, edge buffer/expiry/funding, combo reset, bounded turning/active lock, guest stop/replay/rejection and lunge flags");
    }
}
