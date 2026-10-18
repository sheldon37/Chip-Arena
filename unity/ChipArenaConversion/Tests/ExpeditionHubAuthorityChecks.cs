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
            a.Step(4.2,v=>v==gate.Closure);Check(!a.View().Gates[index].Open&&a.Blocking(index,4.2),"already solid closing cannot be reopened by occupancy");
            a.Interact(gate.Outside.Position,4.3,_=>false);Check(a.View().Gates[index].Open&&!a.Blocking(index,4.81),"explicit interaction still opens after contact");
            a.Interact(gate.Outside.Position,5,v=>v==gate.Closure);Check(a.View().Gates[index].Open,"manual close refuses occupied closure");
            a.Interact(gate.Outside.Position,6,_=>false);Check(!a.View().Gates[index].Open,"outside closes");
            a.Interact(gate.Outside.Position,6.1,_=>false);Check(a.View().Gates[index].Open,"reverse closing from outside");
            a.Step(7,v=>v==gate.Sensor);a.Step(8,_=>false);a.Step(10,v=>v==gate.Sensor);a.Step(11,_=>false);a.Step(13.49,_=>false);
            Check(a.View().Gates[index].Open,"sensor occupancy restarts timeout");
            var reversal=new ExpeditionHubAuthority(layout,0);
            reversal.Step(10,v=>v==gate.Closure);Check(!reversal.View().Gates[index].Open&&reversal.Blocking(index,10),"shut gate ignores overlap even after long idle");
            reversal.Interact(gate.Inside.Position,11,_=>false);Check(reversal.Blocking(index,11.2),"opening starts solid");
            reversal.Interact(gate.Inside.Position,11.2,_=>false);Check(reversal.Blocking(index,11.2)&&reversal.Blocking(index,11.6),"early opening reversed to closing stays solid");
            reversal.Step(11.61,v=>v==gate.Closure);Check(!reversal.View().Gates[index].Open&&reversal.Blocking(index,11.61),"solid early reversal cannot safety-open");
            reversal.Interact(gate.Inside.Position,12,_=>false);Check(!reversal.Blocking(index,12.51),"full opening releases");
            reversal.Interact(gate.Inside.Position,13,_=>false);Check(!reversal.Blocking(index,13.2),"closing starts released");
            reversal.Interact(gate.Inside.Position,13.2,_=>false);Check(!reversal.Blocking(index,13.2)&&!reversal.Blocking(index,13.4),"early closing reversed to opening stays released");
            reversal.Interact(gate.Inside.Position,14,_=>false);reversal.Step(14.64,v=>v==gate.Closure);
            Check(reversal.View().Gates[index].Open&&!reversal.Blocking(index,14.64),"overlap before solid marker reverses safely without an invisible wall");
            reversal.Interact(gate.Inside.Position,15,_=>false);reversal.Step(15.65,v=>v==gate.Closure);
            Check(reversal.View().Gates[index].Open&&!reversal.Blocking(index,15.65),"threshold tick overlap checked before enabling collider");
            reversal.Interact(gate.Inside.Position,16,_=>false);reversal.Step(16.66,_=>false);reversal.Step(16.7,v=>v==gate.Closure);
            Check(!reversal.View().Gates[index].Open&&reversal.Blocking(index,16.7),"overlap after collider is solid cannot release it");
            var view=a.View();view.Gates[index].Open=false;Check(a.View().Gates[index].Open,"view detached from authority");
            Check(ExpeditionHubRules.Crosses(area.sanctuary,gate.Inside.Position,gate.Outside.Position),"ward in to out");
            Check(ExpeditionHubRules.Crosses(area.sanctuary,gate.Outside.Position,gate.Inside.Position),"ward out to in");
            Check(ExpeditionHubRules.Intersects(gate.Closure,gate.Inside.Position,gate.Outside.Position,.5f),"horde nav crossing refused even at open gate");
        }
        Check(ExpeditionHubRules.Crosses(area.sanctuary,new V3(-30,100,0),new V3(30,100,0)),"fast/overhead shot crossing entire sanctuary");
        Check(!ExpeditionHubRules.Crosses(area.sanctuary,new V3(30,0,20),new V3(40,0,20)),"outside shot unchanged");
        Check(ExpeditionHubRules.Crosses(area.sanctuary,new V3(12.3f,0,-3),new V3(12.3f,0,3),.4f),"projectile radius at boundary");
        var box=new ExpeditionVolume{x=0,y=1.5f,z=0,sx=4,sy=3,sz=.4f};
        Check(!ExpeditionHubRules.CapsuleOverlaps(box,new V3(0,.9f,.63f),1.8f,.42f),"contact-offset capsule is not an overlap");
        Check(!ExpeditionHubRules.CapsuleOverlaps(box,new V3(0,.9f,.62f),1.8f,.42f),"tangent capsule is not an overlap");
        Check(ExpeditionHubRules.CapsuleOverlaps(box,new V3(0,.9f,.6f),1.8f,.42f),"actual penetration is occupied");
        Check(!ExpeditionHubRules.CapsuleOverlaps(box,new V3(2.3f,.9f,.5f),1.8f,.42f),"box corner uses round capsule distance");
        Check(ExpeditionHubRules.CapsuleOverlaps(box,new V3(2.28f,.9f,.48f),1.8f,.42f),"actual corner overlap");
        Check(!ExpeditionHubRules.CapsuleOverlaps(box,new V3(0,4,0),1.8f,.42f),"capsule fully above closure is not occupied");
        Check(ExpeditionHubRules.CapsuleOverlaps(box,new V3(0,3.7f,0),1.8f,.42f),"rounded bottom cap overlaps closure");
        box.yaw=90;Check(!ExpeditionHubRules.CapsuleOverlaps(box,new V3(.63f,.9f,0),1.8f,.42f)&&ExpeditionHubRules.CapsuleOverlaps(box,new V3(.6f,.9f,0),1.8f,.42f),"rotated gate capsule overlap");
        string runtime=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionHubAuthority.cs"));
        Check(runtime.Contains("ExpeditionHubRules.CapsuleOverlaps(v,Vec(center),height,radius)")&&!runtime.Contains("r+.15f")&&runtime.Contains("bool solid=hubAuthority.Blocking(i,now);"),"runtime uses capsule geometry and authority collision state without occupancy override");
        Check(runtime.Contains("p.Body.position+p.Body.rotation*Vector3.Scale(capsule.center,scale)")&&!runtime.Contains("TransformPoint(capsule.center)"),"gate occupancy reads the physics pose, not the interpolated render transform");
        var input=new InputFrame{Attack=true,Shoot=true,AbilityQ=true,AbilityE=true,AbilityEHeld=true,AbilityEReleased=true,Hadoken=true,HadokenHeld=true,Uppercut=true,SirenAbility=4,Interact=true,Jump=true,Roll=true,Sprint=true,X=1};
        var held=ExpeditionHubRules.Holster(input);Check(!held.Attack&&!held.Shoot&&!held.AbilityQ&&!held.AbilityE&&!held.AbilityEHeld&&!held.Hadoken&&held.SirenAbility==0,"all weapon intents removed");
        Check(held.Interact&&held.Jump&&held.Roll&&held.Sprint&&held.X==1,"movement and interactions retained");
        for(int n=1;n<=8;n++)Check(ExpeditionHubRules.Cap(n,1)==Math.Min(96,16+12*n),"connected lobby cap "+n);
        Check(ExpeditionHubRules.Cap(8,0)==0&&ExpeditionHubRules.Cap(0,0)==0,"city empty means no hub enemies regardless of lobby");
        Check(ExpeditionHubRules.Cap(1,1)==28&&ExpeditionHubRules.Cap(2,1)==40&&ExpeditionHubRules.Cap(4,1)==64&&ExpeditionHubRules.Cap(8,1)==96,"hub population scales well above the previous eight-to-thirty-two limit");
        Check(ExpeditionHubRules.Cap(8,8)==ExpeditionHubRules.Cap(8,1)&&ExpeditionHubRules.Cap(40,1)==96,"lobby is the population scale, not only the number of residents in the city");
        Check(Math.Abs(ExpeditionHubRules.SpawnInterval-.75)<1e-6&&ExpeditionHubRules.SpawnBurst==2,"spawn rate ramps the larger city without unbounded single-tick bursts");
        Check(runtime.Contains("Run.PartySnapshot().members.Count(m=>m.connected&&!m.extracted)")&&runtime.Contains("ExpeditionHubRules.Cap(lobby,hubLiving.Count)"),"split-area city population uses the whole lobby");
        Check(runtime.Contains("Math.Min(8,excess.Length-Pve.Cap)")&&runtime.Contains("SpawnBurst"),"population trims and refill are tick-bounded");
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
