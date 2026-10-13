using System;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class CyberRangedSimulationChecks
{
    static int checks;
    static void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Cyber ranged simulation: "+why);}
    static void Near(float a,float b,string why)=>Check(Math.Abs(a-b)<.001f,why+": "+a+" != "+b);
    static NativeCyberKit Ranged()=>new NativeCyberKit{Ranged=new NativeCyberRangedState{Ranged=true}};
    public static void Run()
    {
        checks=0;Check(new InputFrame{SirenAbility=5}.Valid&&!new InputFrame{SirenAbility=6}.Valid,"key 5 wire bounds");Check(new InputFrame{SirenAbility=5}.HeldOnly().SirenAbility==0,"switch is a tap, never held/queued");
        var s=new NativeCyberKit();float mana=93;
        Check(!NativeCyberRangedRules.Switch(s,10,true)&&!s.Ranged.Ranged,"external lock refuses switch");
        foreach(string locked in new[]{"cast","dash","link"})
        {s=new NativeCyberKit();if(locked=="cast")s.Until=11;if(locked=="dash")s.DashUntil=11;if(locked=="link")s.LinkUntil=11;Check(!NativeCyberRangedRules.Switch(s,10,false),locked+" refuses switch");Check(!s.Ranged.Ranged&&s.Ranged.ModeReady==-100,"refusal spends no clock");}
        s=new NativeCyberKit{PacketReady=20,QReady=17};Check(NativeCyberRangedRules.Switch(s,10,false),"switch starts");Near(s.Until,10.3f,"switch lock");Near(s.Ranged.ModeReady,11,"switch cooldown");Near(mana,93,"switch shares unchanged mana");
        Check(!NativeCyberRangedRules.Begin(s,0,10.2f,ref mana,1,1,1),"switch blocks attack");Check(!NativeCyberRangedRules.Switch(s,10.99f,false),"cooldown refusal");Check(NativeCyberRangedRules.Switch(s,11,false)&&!s.Ranged.Ranged&&s.PacketReady==20&&s.QReady==17,"melee switch preserves clocks");
        for(int slot=0;slot<5;slot++)
        {
            s=Ranged();mana=100;float rate=slot==0?2:1,cost=slot==4?.8f:.5f;
            Check(NativeCyberRangedRules.Begin(s,slot,20,ref mana,2,cost,.75f),"begin "+slot);
            Near(mana,100-CyberRangedSpec.Costs[slot]*cost,"cost "+slot);Near(s.Release,20+CyberRangedSpec.Windup[slot]/rate,"windup "+slot);
            Near(s.Ranged.Ready[slot],20+CyberRangedSpec.Cooldowns[slot]/rate*(slot==0||slot==4?1:.75f),"cooldown "+slot);
            Check(s.Kind==CyberRangedSpec.Ids[slot]&&s.Valid(),"detail and validity "+slot);
            var predicted=Ranged();float predictedMana=100;NativeCyberRules.Predict(predicted,new InputFrame{SirenAbility=(byte)slot,Shoot=slot==0},20,new V3(),ref predictedMana,2,.5f,.75f,(a,b)=>b,.8f);
            Near(predictedMana,mana,"prediction mana "+slot);Near(predicted.Release,s.Release,"prediction windup "+slot);Near(predicted.Ranged.Ready[slot],s.Ranged.Ready[slot],"prediction clock "+slot);
            float spent=mana;Check(!NativeCyberRangedRules.Begin(s,slot,20.01f,ref mana,2,cost,.75f)&&mana==spent,"no duplicate spend "+slot);
            float cut=20+CyberRangedSpec.Windup[slot]/rate*.5f;NativeCyberRangedRules.Interrupt(s,cut);Check(s.Resolved&&s.Until==cut&&s.Release==cut&&s.Ranged.Until[slot]==cut&&s.Ranged.Release[slot]==cut&&s.Valid(),"pre-release cut "+slot);
            Check(!NativeCyberRangedRules.Switch(s,cut,true),"cut while incapacitated refuses switch");
        }
        s=Ranged();mana=0;Check(!NativeCyberRangedRules.Begin(s,1,10,ref mana,1,1,1)&&s.Kind==""&&s.Ranged.Ready[1]==-100,"insufficient mana no cast or clock");
        Check(NativeCyberRangedRules.Begin(s,0,10,ref mana,1,1,1)&&s.Ranged.BoltHand==1,"free bolt first hand");Check(NativeCyberRangedRules.Begin(s,0,10.27f,ref mana,1,1,1)&&s.Ranged.BoltHand==0,"alternate hand");
        s=Ranged();s.Ranged.Ready[4]=30;mana=100;Check(!NativeCyberRangedRules.Begin(s,4,29,ref mana,1,1,1)&&mana==100,"shared ultimate refuses before clock");Check(NativeCyberRangedRules.Begin(s,4,30,ref mana,1,1,1),"shared ultimate ready");
        NativeCyberRangedRules.Interrupt(s,31.1f);Near(s.Ranged.Release[4],31,"post-release interrupt retains release");Near(s.Ranged.Until[4],31.1f,"post-release cuts recovery");
        {NativeCyberKit fallback;
        fallback=Ranged();fallback.EReady=20;mana=0;NativeCyberRules.Predict(fallback,new InputFrame{AbilityE=true,Shoot=true},10,new V3(),ref mana,1,1,1,(a,b)=>b);Check(fallback.Kind=="bit-bolt"&&mana==0,"unavailable Q/E falls back to ranged bolt, never melee combo");
        fallback=Ranged();mana=100;NativeCyberRules.Predict(fallback,new InputFrame{AbilityQ=true,Shoot=true},10,new V3(),ref mana,1,1,1,(a,b)=>b);Check(fallback.Kind=="data-dash"&&mana==80&&fallback.Ranged.Ranged,"Q unchanged in ranged mode");
        }
        Near(NativeCyberRangedRules.RootDamage(100,100),45,"root healthy");Near(NativeCyberRangedRules.RootDamage(50,100),60,"root missing health");Near(NativeCyberRangedRules.RootDamage(1,1000),75,"root cap");
        Near(CyberRangedSpec.FieldFlight(0),.35f,"minimum flight");Near(CyberRangedSpec.FieldFlight(22),1.1f,"maximum flight");Near(CyberRangedSpec.FieldDamage*CyberRangedSpec.FieldSeconds/CyberRangedSpec.FieldTick,56,"eight field ticks");
        s.Ranged.FieldPoint=new V3(4,7,9);var actor=new Actor{Cyber=s};var copied=actor.ViewCopy();s.Ranged.Ready[4]=900;s.Ranged.FieldPoint.x=77;Check(copied.Cyber.Ranged.Ready[4]==50&&copied.Cyber.Ranged.FieldPoint.x==4,"actor nested record deep copy");
        var view=new CyberLordView();copied.SourceSpec="cyberlord";Check(CyberLordFeed.Read(copied,view)&&view.Ranged&&view.FieldPoint.Y==7&&view.RRelease[4]==31,"feed reads authoritative nested record");
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var wire=JsonSerializer.Deserialize<NativeCyberKit>(JsonSerializer.Serialize(copied.Cyber,options),options);Check(wire.Valid()&&wire.Ranged.FieldPoint.y==7&&wire.Ranged.Release[4]==31,"managed JSON record roundtrip");
        var bad=wire.ViewCopy();bad.Ranged.Ready=null;Check(!bad.Valid(),"null array rejected");bad=wire.ViewCopy();bad.Ranged.At=new float[6];Check(!bad.Valid(),"oversize array rejected");bad=wire.ViewCopy();bad.Ranged.FieldUntil=float.NaN;Check(!bad.Valid(),"NaN rejected");bad=wire.ViewCopy();bad.Ranged.RootTo.x=10000;Check(!bad.Valid(),"vector bound rejected");bad=wire.ViewCopy();bad.Ranged.Until[4]=29;Check(!bad.Valid(),"reversed cast rejected");bad=wire.ViewCopy();bad.Ranged.BoltHand=2;Check(!bad.Valid(),"hand bound rejected");
        Check(NativeCyberRangedRules.BotSlot(2,.4f,1,false,false)==3,"bot emergency swap");Check(NativeCyberRangedRules.BotSlot(15,1,.5f,false,true)==4,"bot root");Check(NativeCyberRangedRules.BotSlot(15,1,.5f,true,true)==2,"bot avoids blocked root");Check(NativeCyberRangedRules.BotSlot(15,1,1,false,true,i=>i==1)==1,"bot falls back through cooldowns");Check(NativeCyberRangedRules.BotSlot(30,1,1,false,true)==1,"bot lance");Check(NativeCyberRangedRules.BotSlot(10,1,1,false,false)==-1,"bot occlusion");
        var state=new NativePredictionState{CanMove=true,CanSprint=true,Grounded=true,Cyber=new NativeCyberKit(),CyberMana=100,CyberRate=1,CyberCost=1,CyberUltimateCost=1,CyberCooldown=1,Speed=6.8f,Time=10};
        state=NativePrediction.Step(state,new InputFrame{SirenAbility=5,Z=1},(a,b)=>b);Check(state.Cyber.Ranged.Ranged&&state.Position.z>0,"prediction switch permits movement");float before=state.CyberMana;state=NativePrediction.Step(state,new InputFrame{SirenAbility=1,Z=1},(a,b)=>b);Check(state.CyberMana==before&&state.Cyber.Kind=="mode-switch","prediction switch refuses casts");
        Console.WriteLine("PASS Cyber ranged simulation: "+checks+" switch, cast, interrupt, prediction, bounds, managed JSON and bot checks");
    }
}
