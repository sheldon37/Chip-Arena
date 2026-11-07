using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class WhirlwindChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Whirlwind: "+why);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    private static NativeMeleeKit Kit(NativeMeleeAction a)=>new NativeMeleeKit{Kind=a.Fx,Phase=a.Phase,At=a.At,Release=a.Release,Until=a.Until,RecoverUntil=a.RecoverUntil,Tick=a.Tick};
    public static void Run(ConversionContent data)
    {
        var d=data.Ability("greatsword","e");
        Check(d.id=="whirlwind"&&d.fx==d.id&&d.cost==45&&d.cooldown==11&&d.damage==8.5&&d.radius==2.6&&d.duration==3&&d.tick==.45&&d.bot.min==0&&d.bot.max==3,"content replacement, costs, tick damage/radius and bot range");
        Check(!data.abilities.Any(x=>x.id=="wolf-knight"),"no live Wolf Knight ability");
        foreach(float rate in new[]{.15f,.45f,1,1.5f,3,8})
        {
            var stats=NativeContentRules.Stats(data,"greatsword",new Dictionary<string,int>());stats["attackRate"]=rate;
            Check(Near(NativeMeleeKitRules.Windup(d,stats),.3f),"fixed telegraph at every attack rate");
        }
        foreach(float dt in new[]{1f/30,1f/60,1f/120})
        {
            var a=NativeWhirlwindRules.Begin(10,0);int pulses=0;
            Check(Near(a.Release,10.3f)&&Near(a.Until,13.3f)&&Near(a.RecoverUntil,13.8f),"phase times");
            for(int i=0;i<Math.Ceiling(4/dt);i++)
            {
                float now=10+i*dt;NativeWhirlwindRules.Advance(a,now);
                while(NativeWhirlwindRules.NextTick(a,now))pulses++;
                Check(!NativeWhirlwindRules.NextTick(a,now),"same-frame re-entry cannot repeat a tick");
                if(now<a.Release)Check(a.Tick==0&&a.Phase=="windup","no early tick");
                if(now>=a.Until)Check(a.Phase=="recovery"&&!NativeWhirlwindRules.Active(a,now),"spin ends before recovery");
            }
            Check(pulses==7&&Near(pulses*NativeWhirlwindRules.Damage,59.5f),"seven pulses, 59.5 base damage at each tick rate");
        }
        var recast=NativeWhirlwindRules.Begin(1,0);
        Check(!NativeWhirlwindRules.Recast(recast,recast.Release+.499f),"early press refused");
        NativeWhirlwindRules.Advance(recast,recast.Release+.5f);while(NativeWhirlwindRules.NextTick(recast,recast.Release+.5f)){}
        float stop=recast.Release+.5f;Check(NativeWhirlwindRules.Recast(recast,stop)&&Near(recast.Until,stop)&&Near(recast.RecoverUntil,stop+.5f)&&recast.Phase=="recovery"&&recast.Tick==2,"recast shortens spin, keeps ticks and adds full recovery");
        Check(!NativeWhirlwindRules.Recast(recast,stop+.1f)&&!NativeWhirlwindRules.NextTick(recast,stop+.5f),"cannot repeat recast or hit in recovery");
        foreach(var delta in new[]{new V3(0,0,2.6f),new V3(2.6f,0,0),new V3(0,0,-2.6f),new V3(-2.6f,0,0)})Check(NativeWhirlwindRules.Contact(delta),"360-degree edge");
        Check(!NativeWhirlwindRules.Contact(new V3(0,0,2.601f))&&!NativeWhirlwindRules.Contact(new V3(0,1.801f,0))&&NativeWhirlwindRules.Contact(new V3(0,0,3),1.5f),"range, vertical bound and reach chip");
        Console.WriteLine("PASS Whirlwind content/timeline: replacement, fixed phases, seven ticks / 59.5 damage, no repeat pulse, 0.5 s recast and radial boundaries");

        var kit=Kit(recast);var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};
        var actor=new Actor{MeleeKit=kit};var copy=actor.ViewCopy();kit.Tick=7;kit.Until=99;
        var wire=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(copy,options),options).MeleeKit;
        Check(wire.Valid()&&wire.Tick==2&&Near(wire.Until,stop)&&Near(wire.RecoverUntil,stop+.5f),"killcam and JSON retain early end / recovery / counter");
        wire.RecoverUntil=float.NaN;Check(!wire.Valid(),"nonfinite recovery rejected");wire.RecoverUntil=stop-.01f;Check(!wire.Valid(),"reversed recovery rejected");wire.RecoverUntil=stop+.5f;
        wire.Tick=8;Check(!wire.Valid(),"excess ticks rejected");wire.Tick=-1;Check(!wire.Valid(),"negative ticks rejected");
        Console.WriteLine("PASS Whirlwind replication: deep copies, early end, recovery end, pulse count and malformed-state rejection");

        var stats2=NativeContentRules.Stats(data,"greatsword",new Dictionary<string,int>());var action=NativeWhirlwindRules.Begin(0,0);
        var state=new NativePredictionState{Time=0,CanMove=true,CanSprint=true,Grounded=true,Speed=6.8f,Gravity=23,JumpSpeed=8.5f,JumpSpec="greatsword",Stamina=55,Ready=action.RecoverUntil,MeleeKit=Kit(action),MeleeWeapon=data.Weapon("greatsword"),MeleeStats=stats2};
        for(int i=0;i<225;i++)
        {
            var input=new InputFrame{X=i<100?1:-1,Yaw=90,Sprint=true,Jump=true,Attack=true,Shoot=true};
            state=NativePrediction.Step(state,input,null,_=>true);
            Check(!state.Sprinting&&state.Swing==null&&state.Stamina==55&&state.Grounded&&state.AirJumps==0,"no sprint, primary, jump or repeated resource payment");
            bool active=NativeWhirlwindRules.Active(state.MeleeKit,state.Time);
            if(!active)Check(state.Velocity.x==0,"rooted windup/recovery");
            else Check(Math.Abs(state.Velocity.x)<=6.8f*.8f+.001f,"80 percent speed cap");
            if(i==90)Check(Near(state.Velocity.x,5.44f),"full controlled speed");
            if(i==140)Check(Near(state.Velocity.x,-5.44f),"steers opposite direction");
        }
        var predictor=new NativePrediction();state.Time=1;state.MeleeKit=Kit(action);state.Position=new V3();state.Velocity=new V3();predictor.Reset(state,0);
        for(int i=1;i<=12;i++)predictor.Push(new InputFrame{Sequence=i,X=1},null,_=>true);
        var before=predictor.State.Position;var ack=state;
        for(int i=1;i<=6;i++)ack=NativePrediction.Step(ack,new InputFrame{Sequence=i,X=1},null,_=>true);
        predictor.Reconcile(ack,6,null,_=>true);Check((predictor.State.Position-before).Length<.0001f&&predictor.Pending==6,"guest ack replay preserves spin movement");
        ack.Time=1.2f;ack.MeleeKit=Kit(action);ack.MeleeKit.Until=1.2f;ack.MeleeKit.RecoverUntil=1.7f;ack.Ready=1.7f;
        predictor.Reconcile(ack,12,null,_=>true);predictor.Push(new InputFrame{Sequence=13,X=1,Jump=true},null,_=>true);
        Check(predictor.State.Velocity.x==0&&predictor.State.Grounded,"early-end snapshot stops guest movement");
        Console.WriteLine("PASS Whirlwind guest motion: steering at 5.44 m/s base, rooted windup/recovery, jump/sprint/primary locks, acknowledgement replay and early-end correction");
    }
}
