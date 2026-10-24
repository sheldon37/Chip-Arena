using System;
using ChipArena.UnityConversion;
internal static class DownAttackChecks
{
    static void Check(bool b,string m){if(!b)throw new Exception("down attack: "+m);}
    public static void Run(ConversionContent content)
    {
        foreach(var spec in new[]{"sword","daggers","shield","greatsword"})
        {
            var w=content.Weapon(spec);var stats=NativeContentRules.Stats(content,spec,new System.Collections.Generic.Dictionary<string,int>());
            var s=NativeMeleeFeel.Begin(w,stats,1,1,0);NativeAirAttackSimulation.Begin(s,-45,new V3(0,10,0));NativeAirAttackSimulation.Carry(s,new V3(10,0,10));
            Check(s.AirKind=="down"&&Math.Abs(s.PlungeAt-1.08f)<.0001,"threshold and windup");Check(s.Carry.x==1.5f&&NativeAirAttackSimulation.Vertical(s,1.1f)==-24,"motion");
            Check(s.DirectContact("pilot:2")&&!s.DirectContact("pilot:2"),"one direct hit");NativeAirAttackSimulation.Land(s,2);
            Check(!s.Contact("pilot:2",2,new V3(),.42f,true),"direct shares landing ledger");
            float radius=NativeAirAttackSimulation.DownRadius(spec);Check(NativeAirAttackSimulation.Contact(s,2,new V3(radius,0,0),0),"ring boundary");Check(!NativeAirAttackSimulation.Contact(s,2,new V3(radius+.001f,0,0),0),"no larger ring");
            Check(Math.Abs(s.Until-2-NativeAirAttackSimulation.DownRecovery(spec))<.0001,"recovery");Check(NativeAirAttackSimulation.ComboUntil(s)==s.ImpactAt,"no dagger combo");
            var predicted=new NativePredictionState{Position=new V3(0,10,0),Velocity=new V3(10,0,0),Time=1,AirborneAt=0,Stamina=100,CanMove=true,Gravity=23,Speed=6.8f,MeleeWeapon=w,MeleeStats=stats,Combo=-1};
            predicted=NativePrediction.Step(predicted,new InputFrame{Attack=true,Pitch=-45},(a,b)=>b);
            Check(predicted.Swing?.AirKind=="down"&&predicted.AirAttackUsed,"guest commits same down kind");Check(predicted.Velocity.y<0,"windup preserves gravity, no hang");
            for(int tick=0;tick<6;tick++)predicted=NativePrediction.Step(predicted,new InputFrame{Pitch=-45},(a,b)=>b);
            Check(predicted.Velocity.y==-24&&predicted.Swing.AirKind=="down","guest plunge speed");
            var f=NativeMeleeFeel.Begin(w,stats,1,1,0);NativeAirAttackSimulation.Begin(f,-44.99f,new V3());Check(f.AirKind==""&&f.PlungeAt==1.15f,"forward attack unchanged");
        }
        Check(NativeAirAttackSimulation.HeightScale(2)==1&&NativeAirAttackSimulation.HeightScale(8)==1.6f&&NativeAirAttackSimulation.HeightScale(100)==1.6f,"height cap");
        Console.WriteLine("PASS downward plunge: four weapons, threshold, motion, direct/ring ledger, exact radii, recovery and capped fall scaling");
    }
}
