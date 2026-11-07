using System;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class NightMeleeChecks
{
    static void Check(bool v,string why){if(!v)throw new Exception("Night melee: "+why);}
    public static void Run(ConversionContent data)
    {
        foreach(string spec in new[]{"daggers","sword","shield","greatsword"})
        {
            var stats=NativeContentRules.Stats(data,spec,new Dictionary<string,int>());var w=data.Weapon(spec);
            var state=new NativePredictionState{JumpSpec=spec,Time=1,AirborneAt=.8f,Position=new V3(0,3,0),Velocity=new V3(0,3,0),CanMove=true,MeleeWeapon=w,MeleeStats=stats,Gravity=23,Stamina=100,Combo=-1,BufferUntil=-1};
            var start=state;var predictor=new NativePrediction();predictor.Reset(start,0);
            V3 Floor(V3 from,V3 to)=>new V3(to.x,Math.Max(0,to.y),to.z);
            for(int n=1;n<=75;n++)predictor.Push(new InputFrame{Sequence=n,Attack=n==1},Floor,p=>p.y<=0);
            state=predictor.State;float spent=100-state.Stamina;
            Check(Math.Abs(spent-NativeMeleeFeel.Cost(w,stats)*1.25f)<.001f,"single stamina debit "+spec);
            Check(state.Grounded&&state.Position.y==0&&state.Swing==null,"plunge lands and recovers "+spec);
            var before=state;predictor.Reconcile(start,0,Floor,p=>p.y<=0);Check((before.Position-predictor.State.Position).Length<.001f&&before.Stamina==predictor.State.Stamina,"replay neither repays nor relaunches");
            var s=NativeMeleeFeel.Begin(w,stats,1,1,0);NativeAirAttackSimulation.Apply(s);
            Check(!s.Contact("a",1.2f,new V3(0,0,1),0,true),"no timed airborne hit");
            Check(NativeAirAttackSimulation.Vertical(s,1.1f)==0&&NativeAirAttackSimulation.Vertical(s,1.2f)==-14,"hang then plunge");
            NativeAirAttackSimulation.Land(s,1.5f);float radius=NativeAirAttackSimulation.Radius(spec);
            Check(Math.Abs(s.Until-1.5f-NativeAirAttackSimulation.Recovery(spec))<.001f,"landing recovery");
            Check(!s.Contact("rear",1.5f,new V3(0,0,-radius),0,false)&&s.Contact("rear",1.5f,new V3(0,0,-radius),0,true)&&!s.Contact("rear",1.5f,new V3(0,0,-radius),0,true),"LOS and shared impact ledger");
            Check(!s.Contact("far",1.5f,new V3(0,0,-radius-.01f),0,true),"ring radius");
            Check(s.DamageScale==(spec=="greatsword"?1.5f:1.3f),"damage scale");
        }
        var edge=new InputFrame{Attack=true};Check(!NativeAirAttackSimulation.Starts(false,1,false,1.149f,edge)&&NativeAirAttackSimulation.Starts(false,1,false,1.151f,edge)&&!NativeAirAttackSimulation.Starts(false,1,true,2,edge)&&!NativeAirAttackSimulation.Starts(true,1,false,2,edge)&&!NativeAirAttackSimulation.Starts(false,1,false,2,new InputFrame{Shoot=true}),"airtime/once/ground/edge gates");
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var actor=new Actor{AirAttack=true,PlungeAt=2,ImpactAt=3,MainWeaponAway=true,AirborneAt=1,AirAttackUsed=true,MeleeKit=new NativeMeleeKit{RecoverAt=3,RecoverUntil=4,ShieldUntil=5}};
        var copy=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(actor,options),options);Check(copy.AirAttack&&copy.PlungeAt==2&&copy.ImpactAt==3&&copy.MainWeaponAway&&copy.AirAttackUsed&&copy.MeleeKit.RecoverAt==3&&copy.MeleeKit.ShieldUntil==5,"wire clocks and hand flag");
        var hit=new ArenaEvent{Kind=EventKind.AbilityHit,Detail="bulwark-charge",WallSlam=true};Check(JsonSerializer.Deserialize<ArenaEvent>(JsonSerializer.Serialize(hit,options),options).WallSlam,"wall impact flag retains canonical fx");
        Console.WriteLine("PASS night melee: four landing attacks, hang/plunge/recovery, single cost, prediction replay, input gates, LOS/radius/dedup and replicated clocks/flags");
    }
}
