using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class DevAbuseChecks
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("DEV Abuse: "+why);}
        NativeAccess Grant(bool developer=true,double expires=1000){var a=new NativeAccess();typeof(NativeAccess).GetProperty("Developer").SetValue(a,developer);typeof(NativeAccess).GetProperty("Expires").SetValue(a,expires);typeof(NativeAccess).GetProperty("AccountId").SetValue(a,"dev-account");return a;}
        var seat=new NativeSeat{id=2,access=Grant()};
        Check(NativeDevRules.DevAllowed(seat,999),"guest dev allowed");Check(!NativeDevRules.DevAllowed(seat,1000),"exact expiry refused");
        Check(!NativeDevRules.DevAllowed(new NativeSeat{access=Grant(false)},0),"non-dev refused");Check(!NativeDevRules.DevAllowed(new NativeSeat{access=NativeAccess.ScopedWeapon(seat.access,"cyberlord",1)},1),"weapon scope cannot command");
        var forged=NativeAccess.Verify("e30."+new string('A',86),_=>new NativePlayerClaim{kind="chip-arena-player",accountId="fake",user="Fake",developer=true,iat=0,exp=1000},1);
        Check(!NativeDevRules.DevAllowed(new NativeSeat{access=forged},1),"forged claim refused");
        var options=new JsonSerializerOptions{IncludeFields=true};int runs=0;var router=new NativeDevCommands();
        NativeDevCommand C(string tx,string action="restore",params string[] args)=>new NativeDevCommand{transaction=tx,action=action,target=1,args=args};
        NativeDevReply Run(NativeDevCommand cmd,double clock=0,int generation=1,int current=1)=>router.Run(seat,cmd,1,generation,1,current,1,clock,()=>{runs++;return null;});
        var command=C("once");var decoded=JsonSerializer.Deserialize<NativeDevCommand>(JsonSerializer.Serialize(command,options),options);
        Check(Run(decoded).state=="done"&&runs==1,"decoded guest command executes");Check(Run(decoded).state=="done"&&runs==1,"retry idempotent");
        Check(Run(C("once","down")).state=="refused"&&runs==1,"conflicting retry refused");
        Check(Run(decoded,0,1,2).state=="done"&&runs==1,"reset reply replay precedes generation guard");
        Check(Run(C("stale"),0,1,2).state=="refused"&&runs==1,"new stale command refused");
        for(int i=0;i<6;i++)Check(Run(C("burst"+i)).state=="done","burst allowance");
        Check(Run(C("limited")).state=="refused","rate limit");Check(Run(C("refilled"),.25).state=="done","unscaled refill");Check(Run(C("limited"),1).state=="refused","rate refusal is terminal on retry");
        seat.access=Grant(expires:1);Check(Run(decoded).state=="refused","expiry rechecked before replay");seat.access=Grant();
        Check(NativeDevRules.Validate(C("x","time-scale","NaN"))!=null,"NaN rejected");Check(NativeDevRules.Validate(C("x","time-scale","2.1"))!=null,"range rejected");Check(NativeDevRules.Validate(C("x","send","1","2","Infinity"))!=null,"infinite point rejected");
        Check(NativeDevRules.Validate(C("x","restore",new string('x',65)))!=null,"oversized argument");Check(NativeDevRules.Validate(C("x","restore","1","2","3","4","5"))!=null,"bounded list");
        var arguments=new Dictionary<string,string[]>{["god"]=new[]{"on"},["infinite-ammo"]=new[]{"off"},["no-cooldowns"]=new[]{"on"},["chip-add"]=new[]{"chip", "2"},["chip-remove"]=new[]{"chip","1"},["weapon"]=new[]{"pistol"},["teleport"]=new[]{"1","2","3"},["send"]=new[]{"1","2","3"},["spawn-dummy"]=new[]{"pistol","ai","1"},["launch"]=new[]{"30"},["freeze"]=new[]{"2"},["swap-weapon"]=new[]{"rifle"},["debris-rain"]=new[]{"wood"},["spawn-dummies-around"]=new[]{"3"},["time-scale"]=new[]{"0.1"},["hazard-trigger"]=new[]{"press"},["debris"]=new[]{"8","15","wood","face"}};
        double t=1;foreach(var action in NativeDevRules.Actions){var cmd=C("action-"+action,action,arguments.TryGetValue(action,out var a)?a:Array.Empty<string>());Check(NativeDevRules.Validate(cmd)==null,"schema "+action);Check(Run(cmd,t++).state=="done","dispatch "+action);}
        var failure=new NativeDevCommands();var fail=C("failure");int attempts=0;
        NativeDevReply Fail()=>failure.Run(seat,fail,1,1,1,1,1,0,()=>{attempts++;throw new Exception();});
        Check(Fail().state=="refused"&&Fail().state=="refused"&&attempts==1,"failed partial command never reruns");
        var match=new NativeMatch(null);var victim=new NativeSeat{id=1};match.Seats.Add(victim);match.Seats.Add(seat);seat.devCommandsRan=true;
        Check(!match.CanEarnProgression(2)&&match.CanEarnProgression(1),"only actor progression blocked");
        Check(!victim.devRevealed&&!victim.devCommandsRan&&victim.chips.Count==0&&victim.spec=="pistol","victim untouched by routing");
        Console.WriteLine("PASS DEV Abuse authority ("+checks+" checks)");
    }
}
