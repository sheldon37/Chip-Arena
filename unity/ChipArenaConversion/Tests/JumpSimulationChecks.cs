using System;
using System.Collections.Generic;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class JumpSimulationChecks
{
    public static void Run(ConversionContent content)
    {
        void Check(bool ok,string why){if(!ok)throw new Exception("Jump simulation: "+why);}
        bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
        foreach(var weapon in content.weapons)
        {
            int used=0;
            var jump=NativeJumpSimulation.Try(weapon.id,true,false,false,false,-15,8.5f,ref used,out float v);
            Check(jump==(weapon.id=="daggers"?NativeJumpKind.Air:NativeJumpKind.None),weapon.id+" air permission");
            if(weapon.id=="daggers")Check(used==1&&Near(v,7.65f),"air impulse replaces falling speed");
            Check(NativeJumpSimulation.Try(weapon.id,true,false,false,false,v,8.5f,ref used,out _)==NativeJumpKind.None,"third jump refused "+weapon.id);
            Check(NativeJumpSimulation.Try(weapon.id,true,true,false,false,-1,17,ref used,out v)==NativeJumpKind.Ground&&used==0&&v==17,"landing reset and jumpPower scaling "+weapon.id);
        }
        foreach(bool ground in new[]{false,true})
        {
            int used=ground?1:0;
            Check(NativeJumpSimulation.Try("daggers",true,ground,false,true,-2,8.5f,ref used,out _)==NativeJumpKind.None&&used==0,"locks refuse without spending air jump; landing still resets");
        }
        int air=0;
        Check(NativeJumpSimulation.Try("daggers",false,false,false,false,-8,8.5f,ref air,out _)==NativeJumpKind.None&&air==0,"held/no edge does not jump");
        Check(NativeJumpSimulation.Try("daggers",true,false,true,false,3,8.5f,ref air,out _)==NativeJumpKind.None&&air==0,"swimming cannot use an air jump");
        Check(NativeJumpSimulation.Try("daggers",true,false,true,false,1,17,ref air,out float stroke)==NativeJumpKind.Swim&&Near(stroke,20)&&air==0,"existing scaled swim stroke remains separate");

        var initial=new NativePredictionState{JumpSpec="daggers",AirJumpAt=-100,CanMove=true,Grounded=true,Gravity=23,JumpSpeed=8.5f,Speed=6.8f};
        V3 Floor(V3 old,V3 wanted)=>new V3(wanted.x,Math.Max(0,wanted.y),wanted.z);
        bool Ground(V3 at)=>at.y<=.001f;
        var world=new ArenaWorld();world.Covers.Clear();var actor=world.Find(1);actor.SourceSpec="daggers";actor.VisualSet=1;
        var predicted=initial;int jumps=0;float lastAt=-100;
        for(int i=1;i<=150;i++)
        {
            var input=new InputFrame{Sequence=i,Jump=i==1||i==20||i==21||i==115||i==125};
            world.Step(input,default);predicted=NativePrediction.Step(predicted,input,Floor,Ground);
            Check(Near(actor.Position.y,predicted.Position.y)&&Near(actor.Velocity.y,predicted.Velocity.y)&&actor.AirJumps==predicted.AirJumps,"lab/prediction trajectory tick "+i);
            if(predicted.AirJumpAt!=lastAt){jumps++;lastAt=predicted.AirJumpAt;}
            if(i==21)Check(actor.AirJumps==1&&Near(actor.AirJumpAt,20*Rules.Dt),"third edge does not refresh the event");
        }
        Check(jumps==2,"air jump available again after actual landing");
        var predictor=new NativePrediction();predictor.Reset(initial,0);var authority=initial;
        for(int i=1;i<=35;i++)
        {
            var input=new InputFrame{Sequence=i,Jump=i==1||i==20};predictor.Push(input,Floor,Ground);
            if(i<=12)authority=NativePrediction.Step(authority,input,Floor,Ground);
        }
        var before=predictor.State;predictor.Reconcile(authority,12,Floor,Ground);
        Check(Near(before.Position.y,predictor.State.Position.y)&&before.AirJumps==predictor.State.AirJumps&&Near(before.AirJumpAt,predictor.State.AirJumpAt),"acknowledgement replays pending air edge exactly once");
        int pending=predictor.Pending;predictor.Push(new InputFrame{Sequence=20,Jump=true},Floor,Ground);
        Check(predictor.Pending==pending&&Near(before.Position.y,predictor.State.Position.y),"duplicate input cannot buy another jump");
        var locked=initial;locked.Grounded=false;locked.CanMove=false;
        Check(NativePrediction.Step(locked,new InputFrame{Jump=true},Floor,Ground).AirJumps==0,"guest action locks reject air jump");
        locked.CanMove=true;locked.RollUntil=1;
        Check(NativePrediction.Step(locked,new InputFrame{Jump=true},Floor,Ground).AirJumps==0,"guest roll rejects air jump");
        locked.RollUntil=0;locked.MeleeWeapon=content.Weapon("daggers");locked.MeleeStats=NativeContentRules.Stats(content,"daggers",new Dictionary<string,int>());locked.Stamina=100;
        var attackJump=NativePrediction.Step(locked,new InputFrame{Jump=true,Attack=true},Floor,Ground);
        Check(attackJump.Swing!=null&&attackJump.AirJumps==0,"same-tick attack commitment blocks guest air jump");
        var json=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var copy=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(new Actor{AirJumps=1,AirJumpAt=3.5f,Grounded=false},json),json);
        Check(copy.AirJumps==1&&copy.AirJumpAt==3.5f&&!copy.Grounded,"replicated jump state round trip");
        var track=new NativeJumpTrack{Explicit=true};track.Observe(1,true,0,false);track.AirJumpSignal(-100);
        for(int i=1;i<=30;i++){float t=1+i/60f;track.Observe(t,false,i/60f,false);track.AirJumpSignal(i>=15?1.25f:-100);}
        Check(track.AirJumps==1&&Near(track.AirJumpAt,1.25f),"explicit signal needs no velocity inference and does not repeat");
        Console.WriteLine("PASS dagger air jump: shared lab/prediction trajectories, landing reset, locks, swim separation, impulse scaling, edge replay and replicated signal");

        foreach(var cut in new[]{("sword",1,1),("sword",2,-1),("sword",3,1),("daggers",1,1),("daggers",2,-1),("shield",1,1),("shield",2,1),("greatsword",1,1),("greatsword",2,-1)})
        {
            var stats=NativeContentRules.Stats(content,cut.Item1,new Dictionary<string,int>());
            var swing=NativeMeleeRules.Begin(content.Weapon(cut.Item1),stats,2,cut.Item2,0);
            float theta=Math.Min((float)Math.PI*2,swing.Arc)*.5f*cut.Item3;
            float radius=swing.Range*.75f;var first=new V3((float)Math.Sin(theta)*radius,0,(float)Math.Cos(theta)*radius);var last=new V3(-first.x,0,first.z);
            Check(swing.Contact("first",swing.HitAt,first,.01f,true)&&!swing.Contact("last",swing.HitAt,last,.01f,true),cut+": native blade enters on the correct side");
            Check(swing.Contact("last",swing.HitUntil,last,.01f,true)&&!swing.Contact("first",swing.HitUntil,first,.01f,true),cut+": opposite side then one-hit ledger");
        }
        Console.WriteLine("PASS native melee sweep order: all nine combo cuts enter/leave on the captured blade side with per-target contact ledgers");
    }
}
