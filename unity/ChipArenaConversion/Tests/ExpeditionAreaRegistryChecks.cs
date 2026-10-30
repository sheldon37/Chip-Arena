using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ChipArena.UnityConversion;
internal static class ExpeditionAreaRegistryChecks
{
    private sealed class FakeArea:IExpeditionAreaRuntime
    {
        public string AreaId;public string Area=>AreaId;public int Occupants{get;set;}
        public ExpeditionRuntimeLoad Load{get;set;}=ExpeditionRuntimeLoad.Created;
        public int Steps,Disposals;public bool Shown,Throw;public Action During;
        public void Step(long tick){Steps++;During?.Invoke();if(Throw)throw new Exception("injected area failure");}
        public void SetPresented(bool shown){Shown=shown;}
        public void Dispose(){Disposals++;Load=ExpeditionRuntimeLoad.Disposed;}
    }
    public static void Run(string root)
    {
        int checks=0;void Check(bool value,string why){if(!value)throw new Exception("Area registry: "+why);checks++;}
        void Reject(Action action,string why){bool refused=false;try{action();}catch{refused=true;}Check(refused,why);}
        var created=new List<FakeArea>();
        var registry=new ExpeditionAreaRegistry<FakeArea>("hub","ward",id=>{var a=new FakeArea{AreaId=id};created.Add(a);return a;});
        var hub=registry.Acquire("hub");hub.Occupants=1;hub.Load=ExpeditionRuntimeLoad.Ready;
        var ward=registry.Acquire("ward");ward.Occupants=3;
        Check(registry.Acquire("ward")==ward&&created.Count==2,"same Ward encounter is reused");
        Reject(()=>registry.Acquire("private-ward"),"private copies refused");
        registry.Present("hub");Check(hub.Shown&&!ward.Shown,"only local area presented");
        Reject(()=>registry.Present("ward"),"loading destination not displayed");
        double clock=0;int clocks=0;Action<double> shared=dt=>{clock+=dt;clocks++;};
        Check(registry.Step(0,shared)&&hub.Steps==1&&ward.Steps==0,"loading cohort does not stop hub");
        Check(!registry.Step(0,shared)&&clocks==1,"duplicate host tick does not double clock");
        ward.Load=ExpeditionRuntimeLoad.Ready;
        for(int i=1;i<=60;i++)registry.Step(i,shared);
        Check(hub.Steps==61&&ward.Steps==60&&clocks==61&&Math.Abs(clock-61d/60)<1e-9,"two areas tick; shared clock once");
        Check(!registry.Step(2,shared)&&clocks==61,"stale host tick ignored");
        registry.Present("ward");Check(!hub.Shown&&ward.Shown&&registry.Presented=="ward","presentation rebind hides former area");
        ward.Occupants=0;registry.Step(61,shared);Check(ward.Steps==60&&registry.Find("ward")==ward,"empty instance sleeps without discarding encounter");
        ward.Occupants=1;registry.Step(62,shared);Check(ward.Steps==61,"return resumes retained instance");
        hub.During=()=>Reject(()=>registry.Acquire("hub"),"no reentrant creation");registry.Step(63,shared);hub.During=null;
        ward.Throw=true;Reject(()=>registry.Step(64,shared),"step failure propagates");
        Check(registry.Faulted,"partial tick latches fault");int before=clocks;
        Reject(()=>registry.Step(64,shared),"partial tick cannot replay");Reject(()=>registry.Step(65,shared),"faulted simulation cannot continue");Check(clocks==before,"fault cannot double shared clock");
        registry.Dispose();registry.Dispose();Check(hub.Disposals==1&&ward.Disposals==1,"owned runtimes disposed exactly once");
        Reject(()=>registry.Acquire("hub"),"disposed registry refuses acquisition");
        // Prevent a new default-scene query from silently reintroducing cross-area aim/collision.
        var pattern=new Regex(@"\bPhysics\.(?:Linecast|Raycast\w*|SphereCast\w*|CapsuleCast\w*|BoxCast\w*|Overlap\w*|CheckSphere|CheckBox|CheckCapsule|Simulate)\s*\(");
        foreach(var path in Directory.GetFiles(Path.Combine(root,"Assets/ChipArena/Runtime"),"*.cs"))
        {
            string file=Path.GetFileName(path);if(file.Contains("Validation")||file=="ArenaGame.cs")continue;
            Check(!pattern.IsMatch(File.ReadAllText(path)),"scene query audit: "+file);
        }
        Console.WriteLine("PASS Expedition area runtime foundation: "+checks+" checks (clock/load/occupancy/presentation/fault isolation and runtime query audit)");
    }
}
