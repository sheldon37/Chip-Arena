using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ExpeditionHubAuthorityChecks
{
    public static void Run(string root)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Hub authority: "+why);}
        string json=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json"));
        var layout=ExpeditionHubLayout.Parse(json);var area=NativeExpeditionLayout.Parse(json).Area(layout.Area);
        foreach(var gate in layout.Gates)
        {
            int index=Array.IndexOf(layout.Gates,gate);var a=new ExpeditionHubAuthority(layout,0);
            Check(a.Blocking(index,0),"gate initially shut");
            Check(a.Interact(gate.Inside.Position,0,_=>false),"inside opens");
            Check(a.Blocking(index,.49)&&!a.Blocking(index,.5),"opening collider threshold");
            a.Step(1,_=>false);a.Step(3.49,_=>false);Check(a.View().Gates[index].Open,"2.5 seconds of clear sensor required");
            a.Step(3.5,_=>false);Check(!a.View().Gates[index].Open&&!a.Blocking(index,4.14)&&a.Blocking(index,4.15),"auto close and solid threshold");
            a.Step(4.2,v=>v==gate.Closure);Check(a.View().Gates[index].Open,"closure overlap reopens");
            a.Interact(gate.Outside.Position,5,v=>v==gate.Closure);Check(a.View().Gates[index].Open,"manual close refuses occupied closure");
            a.Interact(gate.Outside.Position,6,_=>false);Check(!a.View().Gates[index].Open,"outside closes");
            a.Interact(gate.Outside.Position,6.1,_=>false);Check(a.View().Gates[index].Open,"reverse closing from outside");
            a.Step(7,v=>v==gate.Sensor);a.Step(8,_=>false);a.Step(10,v=>v==gate.Sensor);a.Step(11,_=>false);a.Step(13.49,_=>false);
            Check(a.View().Gates[index].Open,"sensor occupancy restarts timeout");
            var view=a.View();view.Gates[index].Open=false;Check(a.View().Gates[index].Open,"view detached from authority");
            Check(ExpeditionHubRules.Crosses(area.sanctuary,gate.Inside.Position,gate.Outside.Position),"ward in to out");
            Check(ExpeditionHubRules.Crosses(area.sanctuary,gate.Outside.Position,gate.Inside.Position),"ward out to in");
            Check(ExpeditionHubRules.Intersects(gate.Closure,gate.Inside.Position,gate.Outside.Position,.5f),"horde nav crossing refused even at open gate");
        }
        Check(ExpeditionHubRules.Crosses(area.sanctuary,new V3(-30,100,0),new V3(30,100,0)),"fast/overhead shot crossing entire sanctuary");
        Check(!ExpeditionHubRules.Crosses(area.sanctuary,new V3(30,0,20),new V3(40,0,20)),"outside shot unchanged");
        Check(ExpeditionHubRules.Crosses(area.sanctuary,new V3(12.3f,0,-3),new V3(12.3f,0,3),.4f),"projectile radius at boundary");
        var input=new InputFrame{Attack=true,Shoot=true,AbilityQ=true,AbilityE=true,AbilityEHeld=true,AbilityEReleased=true,Hadoken=true,HadokenHeld=true,Uppercut=true,SirenAbility=4,Interact=true,Jump=true,Roll=true,Sprint=true,X=1};
        var held=ExpeditionHubRules.Holster(input);Check(!held.Attack&&!held.Shoot&&!held.AbilityQ&&!held.AbilityE&&!held.AbilityEHeld&&!held.Hadoken&&held.SirenAbility==0,"all weapon intents removed");
        Check(held.Interact&&held.Jump&&held.Roll&&held.Sprint&&held.X==1,"movement and interactions retained");
        for(int n=0;n<=8;n++)Check(ExpeditionHubRules.Cap(n)==(n==0?0:Math.Min(16,2+2*n)),"living cap "+n);
        foreach(var row in new[]{("near",10f),("mid",15f),("far",20f)})Check(Math.Abs(25*ExpeditionHubRules.Reward(row.Item1)-row.Item2)<.001,"ring payout "+row.Item1);
        foreach(var ring in new[]{"near","mid","far"})Check(!ExpeditionHubRules.Kind(ring,"hulk",99999),"no hub Hulks");
        Check(!ExpeditionHubRules.Kind("near","spitter",999)&&!ExpeditionHubRules.Kind("far","brute",359)&&ExpeditionHubRules.Kind("far","brute",360),"ring roster and brute timer");
        var authority=new ExpeditionHubAuthority(layout,0);
        var run=NativeExpedition.Create("hub-wire",layout.Area,"host","pistol",12,new[]{new ExpeditionInstance{id="exp-ward-zero",target=1}});
        var batch=ExpeditionViewBatch.Build(run,new[]{"host"},new ExpeditionViewSources{Hub=authority.View});
        var options=new JsonSerializerOptions{IncludeFields=true};var decoded=JsonSerializer.Deserialize<ExpeditionView>(JsonSerializer.Serialize(batch.Public,options),options);
        Check(decoded.hub.Count==4&&decoded.hub.Ward&&decoded.hub.Holster&&decoded.mine==null,"public hub roundtrip without private data");
        var receiver=new ExpeditionViewReceiver("host");receiver.Public(decoded);receiver.Private(batch.Private[0]);Check(receiver.View.hub.Count==4,"guest receives all gate states");
        authority.Interact(layout.Gates[0].Inside.Position,1,_=>false);var changed=ExpeditionViewBatch.Build(run,new[]{"host"},new ExpeditionViewSources{Hub=authority.View});
        receiver.Public(changed.Public);receiver.Private(changed.Private[0]);Check(receiver.View.hub.Gates[0].Open,"source-only gate change accepted at unchanged run revision");
        Console.WriteLine("PASS Expedition hub authority: "+checks+" checks (gate safety/timing, ward segments, holster intents, cap/rings, guest wire)");
    }
}
