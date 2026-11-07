using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class RangedAbilityChecks
{
    private static void Check(bool ok,string why){if(!ok)throw new Exception("Ranged Q: "+why);}
    private static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    public static void Run(ConversionContent data)
    {
        var fan=data.Ability("pistol","q");var drive=data.Ability("machinegun","q");
        Check(fan.id=="fan-hammer"&&fan.kind=="burst"&&fan.live&&fan.cooldown==7&&fan.cost==0&&fan.tick==.075,"Fan entry");
        Check(drive.id=="overdrive"&&drive.kind=="buff"&&drive.live&&drive.cooldown==10&&drive.cost==0&&drive.duration==4&&drive.lockout==.8,"Overdrive entry");
        Check(data.Ads("pistol")==null&&data.Ads("machinegun")==null&&data.Ads("rifle").overlay=="scope"&&data.Ability("rifle","q").kind=="ads","only rifle retains ADS");
        Check(!NativeBotRules.Ads("pistol",30)&&!NativeBotRules.Ads("machinegun",30)&&NativeBotRules.Ads("rifle",30)&&NativeBotRules.Ability("fan-hammer").max>0&&NativeBotRules.Ability("overdrive").max>0,"bot activation ranges and rifle-only scope");
        foreach(int ammo in new[]{0,1,2,3,4,7,14})foreach(float dt in new[]{1f/30,1f/60,1f/120})
        {
            var burst=NativeRangedRules.Fan(10,ammo);int remaining=ammo,events=0;
            for(int frame=0;frame<60;frame++)
            {
                float time=10+frame*dt;
                while(burst.Due(time))
                {
                    Check(remaining>0,"never creates ammo");float scheduled=burst.At+burst.Shots*.075f;
                    Check(time+.00001f>=scheduled&&time-scheduled<dt+.00002f,"anchored schedule fires within one tick");
                    if(events==0)Check(frame==0,"first round immediate");
                    remaining--;events++;burst.Shots++;
                }
                Check(!burst.Due(time),"same-tick reentry cannot double fire");
            }
            Check(events==Math.Min(4,ammo)&&remaining==ammo-events,"only the originally available four rounds");
        }
        Check(Near(NativeRangedRules.Spread(.008,true),.0176f)&&Near(23*NativeRangedRules.FanDamage,18.4f),"Fan cone and damage multiply normal shots");
        Console.WriteLine("PASS ranged Q entries, rifle-only scope, bots, immediate 1-4 round bursts, anchored 75 ms timing and shot scales");

        float at=10,until=14,vent=14.8f;
        Check(!NativeRangedRules.Overdrive(9.999f,at,until)&&NativeRangedRules.Overdrive(at,at,until)&&NativeRangedRules.Overdrive(13.999f,at,until)&&!NativeRangedRules.Overdrive(until,at,until),"half-open four second buff");
        Check(!NativeRangedRules.Vent(13.999f,until,vent)&&NativeRangedRules.Vent(until,until,vent)&&NativeRangedRules.Vent(14.799f,until,vent)&&!NativeRangedRules.Vent(vent,until,vent),"exact vent boundaries");
        foreach(double rate in new[]{.15,1,2.5,5})Check(Near(NativeRangedRules.Interval(.085,rate,true)*1.6f,NativeRangedRules.Interval(.085,rate,false)),"rate composes with attack-rate chips");
        Check(NativeRangedRules.Valid(0,0,0)&&!NativeRangedRules.Overdrive(0,0,0)&&!NativeRangedRules.Vent(0,0,0),"absent old fields inactive");
        Check(!NativeRangedRules.Valid(float.NaN,until,vent)&&!NativeRangedRules.Valid(at,float.PositiveInfinity,vent)&&!NativeRangedRules.Valid(at,until,float.NegativeInfinity)&&!NativeRangedRules.Valid(at,at-1,vent)&&!NativeRangedRules.Valid(at,until,until-1),"malformed clocks rejected");
        var source=new Actor{OverdriveAt=at,OverdriveUntil=until,VentUntil=vent};var copy=source.ViewCopy();source.OverdriveUntil=99;
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var wire=JsonSerializer.Deserialize<Actor>(JsonSerializer.Serialize(copy,options),options);
        Check(wire.OverdriveAt==at&&wire.OverdriveUntil==until&&wire.VentUntil==vent,"recorded and late-join clocks retained");
        Console.WriteLine("PASS Overdrive four-second buff, 0.8 s vent, 1.6 rate with chips, backward-compatible clocks and snapshot copies/JSON");

        var initial=new NativePredictionState{CanMove=true,CanSprint=true,Grounded=true,Time=10,Speed=6.8f,Gravity=23,JumpSpeed=8.5f,OverdriveAt=at,OverdriveUntil=until};
        var state=initial;
        for(int i=0;i<45;i++)state=NativePrediction.Step(state,new InputFrame{X=1},null,_=>true);
        Check(Near(state.Velocity.x,5.44f),"guest applies active movement penalty");
        state.Time=14;for(int i=0;i<10;i++)state=NativePrediction.Step(state,new InputFrame{X=1},null,_=>true);
        Check(Near(state.Velocity.x,6.8f),"vent restores normal movement");
        var prediction=new NativePrediction();prediction.Reset(initial,0);
        for(int i=1;i<=18;i++)prediction.Push(new InputFrame{Sequence=i,X=1},null,_=>true);
        var before=prediction.State.Position;var ack=initial;
        for(int i=1;i<=9;i++)ack=NativePrediction.Step(ack,new InputFrame{Sequence=i,X=1},null,_=>true);
        prediction.Reconcile(ack,9,null,_=>true);
        Check((prediction.State.Position-before).Length<.0001f&&prediction.Pending==9,"ack replay agrees with host clock");
        Console.WriteLine("PASS Overdrive guest movement at 80 percent, expiry without lingering slowdown and acknowledgement replay");
    }
}
