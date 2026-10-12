using System;
using System.Collections.Generic;
using ChipArena.UnityConversion;

internal static class SprintAttackChecks
{
    static void Check(bool value,string why){if(!value)throw new Exception("Sprint attack: "+why);}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    public static void Run(ConversionContent data)
    {
        string[] specs={"sword","daggers","shield","greatsword"};
        float[] wind={.16f,.12f,.12f,.48f},active={.18f,.24f,.16f,.16f},recovery={.45f,.25f,.5f,.7f},distance={2.2f,2.6f,2,2.4f},scale={1.2f,.7f,.9f,1.4f};
        NativePredictionState State(string spec)=>new NativePredictionState{Time=1,MeleeWeapon=data.Weapon(spec),MeleeStats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>()),CanMove=true,CanSprint=true,Grounded=true,Stamina=100,Combo=-1,BufferUntil=-1,Gravity=23,Speed=6.8f};
        for(int i=0;i<specs.Length;i++)foreach(float rate in new[]{.5f,1,2})
        {
            var p=State(specs[i]);p.MeleeStats["attackRate"]=rate;var input=new InputFrame{Attack=true,Sprint=true,Z=1,LockTarget=2};
            NativeMeleePrediction.Advance(ref p,input,_=>new V3(0,0,1));var s=p.Swing;
            Check(s.SprintAttack&&s.Index==1&&!p.Sprinting,"opener/stop "+specs[i]);
            Check(Near(s.HitAt-s.At,wind[i]/rate)&&Near(s.HitUntil-s.HitAt,active[i]/rate)&&Near(s.Until-s.HitUntil,recovery[i]/rate),"timing "+specs[i]);
            Check(Near(s.Distance,distance[i])&&Near(s.DamageScale,scale[i]),"step/damage "+specs[i]);
            Check(Near(100-p.Stamina,NativeMeleeFeel.Cost(p.MeleeWeapon,p.MeleeStats)*1.3f),"cost "+specs[i]);
            float walked=0;for(float t=s.At;t<s.StepUntil+.02f;t+=Rules.Dt)walked+=NativeMeleeRules.Step(s,t,Rules.Dt).z*Rules.Dt;
            Check(Near(walked,distance[i]),"integrated step "+specs[i]);
            Check(NativeMeleeRules.BeforeStrike(s,s.At)&&!NativeMeleeRules.BeforeStrike(s,s.HitAt),"windup roll recovery "+specs[i]);
            if(i==3)Check(!p.Grounded&&Near(p.Velocity.y,23*wind[i]/rate*.5f),"physical hop");
            var unfunded=State(specs[i]);unfunded.Stamina=NativeMeleeFeel.Cost(unfunded.MeleeWeapon,unfunded.MeleeStats)*1.29f;
            NativeMeleePrediction.Advance(ref unfunded,input,null);Check(unfunded.Swing==null,"sprint cost checked before commitment");
            var air=State(specs[i]);air.Grounded=false;NativeMeleePrediction.Advance(ref air,input,null);Check(!air.Swing.SprintAttack,"ground only");
            var held=State(specs[i]);NativeMeleePrediction.Advance(ref held,new InputFrame{Shoot=true,Sprint=true,Z=1},null);Check(!held.Swing.SprintAttack,"edge only");
            var combo=State(specs[i]);combo.Combo=0;combo.ComboUntil=3;NativeMeleePrediction.Advance(ref combo,input,null);Check(!combo.Swing.SprintAttack,"first swing only");
        }
        var d=State("daggers");NativeMeleePrediction.Advance(ref d,new InputFrame{Attack=true,Sprint=true,Z=1},null);var twin=d.Swing;
        float t1=twin.HitAt+.06f,t2=t1+.12f;var front=new V3(0,0,1);
        Check(twin.Contact("victim",t1,front,.3f,true)&&!twin.Contact("victim",t1+.001f,front,.3f,true)&&twin.Contact("victim",t2,front,.3f,true)&&!twin.Contact("victim",t2+.001f,front,.3f,true),"two and only two contacts 120ms apart");
        d.Time=twin.ChainAt;NativeMeleePrediction.Advance(ref d,new InputFrame{Attack=true,Sprint=true,Z=1},null);Check(!d.Swing.SprintAttack&&d.Swing.Index==2&&d.Swing.At<twin.Until,"dagger normal chain");
        var g=State("greatsword");NativeMeleePrediction.Advance(ref g,new InputFrame{Attack=true,Sprint=true,Z=1},null);var cleave=g.Swing;var rear=new V3(0,0,-1.5f);
        Check(!cleave.Contact("rear",cleave.HitAt,rear,0,true,false)&&!cleave.Contact("rear",cleave.HitAt,rear,0,false,true)&&cleave.Contact("rear",cleave.HitAt,rear,0,true,true),"ring requires ground and sight");
        Check(!cleave.Contact("rear",cleave.HitAt+.02f,rear,0,true,true)&&!NativeSprintAttack.Ring(cleave,cleave.HitAt,new V3(0,0,-1.61f),0,true),"ring radius/dedup");
        Check(cleave.Contact("front",cleave.HitAt+.08f,front,.3f,true,true)&&!cleave.Contact("front",cleave.HitAt+.09f,front,.3f,true,true),"blade and ring share ledger");
        var shield=State("shield");NativeMeleePrediction.Advance(ref shield,new InputFrame{Attack=true,Sprint=true,Z=1},null);float v=NativeSprintAttack.Impulse(shield.Swing,0)/65,travel=0;
        for(int i=0;i<600;i++){v*=(float)Math.Exp(-3.1f*Rules.Dt);travel+=v*Rules.Dt;}Check(Near(travel,2.5f),"shield unopposed knockback distance");
        var start=State("greatsword");var predictor=new NativePrediction();predictor.Reset(start,0);
        for(int i=1;i<=45;i++)predictor.Push(new InputFrame{Sequence=i,Attack=i==1,Sprint=true,Z=1},null);
        var before=predictor.State;predictor.Reconcile(start,0,null);Check(Near(before.Position.z,predictor.State.Position.z)&&Near(before.Position.y,predictor.State.Position.y)&&Near(before.Stamina,predictor.State.Stamina),"hop reconciliation does not repay or relaunch");
        Console.WriteLine("PASS sprint attacks: four profiles/rates/costs, edge/ground/opener gates, step travel, twin contacts/chain, ring visibility/dedup, shield distance and hop replay");
    }
}
