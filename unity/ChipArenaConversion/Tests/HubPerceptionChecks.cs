using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class HubPerceptionChecks
{
    public static void Run(string root)
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Hub perception: "+why);}
        var origin=new V3();
        Check(HubPerception.Sees(origin,0,new V3(0,0,18),true,false),"18m sight inclusive");
        Check(!HubPerception.Sees(origin,0,new V3(0,0,18.01f),true,false),"sight range bounded");
        Check(HubPerception.Sees(origin,0,V3.Forward(55)*10,true,false)&&!HubPerception.Sees(origin,0,V3.Forward(55.1f)*10,true,false),"110 degree cone boundary");
        Check(HubPerception.Sees(origin,0,new V3(0,0,-3),true,false)&&!HubPerception.Sees(origin,0,new V3(0,0,-3.01f),true,false),"close omnidirectional notice");
        Check(!HubPerception.Sees(origin,0,new V3(0,0,2),false,false),"close notice cannot see through walls");
        Check(!HubPerception.Sees(origin,0,new V3(0,0,2),true,true),"fence blocks sight including close notice");
        var radii=new[]{5,10,12,14,28,36,22,16};
        foreach(HubNoiseKind k in Enum.GetValues(typeof(HubNoiseKind)))
        {
            float r=HubPerception.Radius(k);Check(r==radii[(int)k],"authored noise radius "+k);
            Check(HubPerception.Hears(origin,new V3(r,0,0),k,false)&&!HubPerception.Hears(origin,new V3(r+.01f,0,0),k,false),"noise range "+k);
            Check(!HubPerception.Hears(origin,new V3(1,0,0),k,true),"fence blocks noise "+k);
        }
        var sanctuary=new[]{new ExpeditionVolume{x=0,y=2,z=0,sx=10,sy=4,sz=10}};
        Check(ExpeditionHubRules.Crosses(sanctuary,new V3(-6,1,0),new V3(6,1,0)),"outside-to-outside sound across sanctuary blocked");
        Check(!ExpeditionHubRules.Crosses(sanctuary,new V3(-6,1,6),new V3(6,1,6)),"sound on same exterior side allowed");
        for(int seed=1;seed<=200;seed++)
        {
            var b=new HubZombieBrain(seed,0,origin,0);b.Step(1.99f,origin,false);Check(b.View.State==HubZombieState.Idle,"minimum idle");
            b.Step(6,origin,false);Check(b.NeedsGoal&&b.View.State==HubZombieState.Wander,"maximum idle");
            b.Wander(6,new V3(10,0,0));b.Step(7,origin,false);Check(b.View.State==HubZombieState.Wander,"wander travels");
            b.Step(8,new V3(10,0,0),false);Check(b.View.State==HubZombieState.Idle,"arrival idles");
            b.Observe(10,2,new V3(0,0,10),origin);Check(b.View.State==HubZombieState.Alert&&b.View.AlertAt==10,"notice starts reaction");
            b.Step(10.39f,origin,true);Check(b.View.State==HubZombieState.Alert,"minimum alert");
            b.Observe(10.5f,2,new V3(0,0,11),origin);Check(b.View.AlertAt==10,"repeated perception cannot restart reaction");
            b.Step(10.81f,origin,true);Check(b.View.State==HubZombieState.Chase,"maximum alert");
            Check(b.Goal.z==11,"last observed position remembered");
            b.Step(13.49f,origin,true);Check(b.View.State==HubZombieState.Chase,"three second memory grace");
            b.Step(13.51f,origin,true);Check(b.View.State==HubZombieState.Search&&b.Target==0&&b.Goal.z==11,"lost sight searches last position without live target");
            b.Step(14,new V3(0,0,11),false);Check(b.SearchInPlace,"arrival searches in place");
            b.Step(17.99f,new V3(0,0,11),false);Check(b.View.State==HubZombieState.Search,"four second search");
            b.Step(18.01f,new V3(0,0,11),false);Check(b.NeedsGoal,"search returns to wandering");
            b.Observe(20,3,new V3(8,0,0),origin);Check(b.View.State==HubZombieState.Alert&&b.View.AlertAt==20&&b.View.LookYaw==90,"reacquisition reacts and looks toward source");
            b.Step(20.1f,origin,false);Check(b.View.State==HubZombieState.Search,"death disconnect or sanctuary expiry loses target");
            b.Step(30.11f,origin,false);Check(b.NeedsGoal,"unreachable memory position times out");
            Check(HubZombieView.Valid(b.View),"finite public view");
        }
        var a=new HubZombieBrain(88,0,origin,0);var same=new HubZombieBrain(88,0,origin,0);
        for(int i=0;i<100;i++){Check(a.Pick(17)==same.Pick(17),"per-zombie deterministic random stream");}
        var options=new JsonSerializerOptions{IncludeFields=true};
        foreach(HubZombieState state in new[]{HubZombieState.Wander,HubZombieState.Idle,HubZombieState.Alert,HubZombieState.Chase,HubZombieState.Search})
        {
            var v=new HubZombieView{State=state,AlertAt=12.5f,LookYaw=-120};var copy=JsonSerializer.Deserialize<HubZombieView>(JsonSerializer.Serialize(v,options),options);
            Check(copy.State==state&&copy.AlertAt==12.5f&&copy.LookYaw==-120&&HubZombieView.Valid(copy),"state wire roundtrip "+state);
            var isolated=v.Copy();v.LookYaw=40;Check(isolated.LookYaw==-120,"immutable captured view");
        }
        Check(HubZombieView.Valid(null),"old/non-hub frames omit optional state");
        Check(!HubZombieView.Valid(new HubZombieView{State=(HubZombieState)255})&&!HubZombieView.Valid(new HubZombieView{State=HubZombieState.Alert,AlertAt=float.NaN})&&!HubZombieView.Valid(new HubZombieView{State=HubZombieState.Chase,LookYaw=float.PositiveInfinity}),"malformed wire rejected");
        Check(HubZombieView.Valid(HubZombieState.None,0,0),"older scalar frames default to no hub state");
        Check(!HubZombieView.Valid((HubZombieState)6,0,0)&&!HubZombieView.Valid(HubZombieState.Alert,float.NaN,0)&&!HubZombieView.Valid(HubZombieState.Chase,1,float.PositiveInfinity),"scalar wire validation");
        var map=JsonSerializer.Deserialize<UnityMapContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/UnityMaps.json")),options).maps.Single(m=>m.id=="expedition-city");
        string manifest=File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/ExpeditionLayout.json"));
        var layout=ExpeditionHubLayout.Parse(manifest);var area=NativeExpeditionLayout.Parse(manifest).Area(layout.Area);
        foreach(var zone in layout.Zones)
        {
            var points=map.nav.nodes.Where(n=>zone.Volume.Contains(new V3(n.pos.x,zone.Volume.y,n.pos.z))&&!ExpeditionHubRules.Inside(area.sanctuary,n.pos)).ToArray();
            Check(points.Length>1,"authored roaming destinations exist in "+zone.Id);
            Check(points.Any(a=>points.Any(b=>HubPerception.FlatDistance(a.pos,b.pos)>=3&&HubPerception.FlatDistance(a.pos,b.pos)<=14&&!ExpeditionHubRules.Crosses(area.sanctuary,a.pos,b.pos,.42f))),"local wander leg fits zone and fence "+zone.Id);
        }
        string R(string f)=>File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime",f));
        var wire=R("NativeNetworkFrame.cs");Check(wire.Contains("hubState=m.Pve?.Hub?.State??HubZombieState.None")&&wire.Contains("Hub=mf.hubState==HubZombieState.None?null:new HubZombieView")&&wire.Contains("!HubZombieView.Valid(m.hubState,m.hubAlertAt,m.hubLookYaw)"),"capture validate and guest apply");
        var horde=R("NativePveHorde.cs");Check(horde.Contains("if(roaming){if(StepHubMind(m,enemies,out target))continue;}")&&horde.Contains("else if(Time>=a.ThinkAt"),"hub mind bypasses omniscient ordinary target selection");
        var runtime=R("NativeHubPerceptionRuntime.cs");Check(runtime.Contains("HubInside(p.Body.position)||HubInside(at)||HubCross(p.Body.position,at)")&&runtime.Contains("HubCross(m.Body.position,n.Position)"),"noise source and listener both enforce fence");
        Check(R("NativeArena.cs").Contains("HubEventNoise(kind,source,position);")&&R("NativeExpeditionHubAuthority.cs").Contains("HubNoise(p,p.Body.position,HubNoiseKind.Gate)"),"real combat and gate noise hooked");
        Check(runtime.Contains("p.Grounded&&!f.Grounded")&&runtime.Contains("p.Pose.Sprinting")&&runtime.Contains("p.LastAttack>f.AttackAt"),"actual movement landing and combat sampling");
        Console.WriteLine("PASS hub zombie perception: "+checks+" checks (cone/LOS/fence, eight noise radii, 200 seeded idle/reaction/memory/search traces, wire and runtime wiring)");
    }
}
