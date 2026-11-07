using System;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ReviveMachineRuleChecks
{
    public static void Run()
    {
        int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Revive machine rules: "+why);n++;}
        NativeExpedition Run(string[] mods=null){var r=NativeExpedition.Create("revive","hub","payer","sword",17,new[]{new ExpeditionInstance{id="ward",target=2}},mods);r.Join("dead","bow");r.Defeat("dead");return r;}
        NativeReviveSpit Spit()=>new NativeReviveSpit{Id=1,Seat=2,Account="dead",At=10,Land=new V3(2,0,3)};
        var run=Run();Check(!run.BeginMachineRevive("payer",Spit()),"insufficient funds refused");run.Credit("payer","fund",500);
        Check(run.BeginMachineRevive("payer",Spit()),"payment accepted");Check(run.Snapshot().members[0].wallet==250&&!run.Snapshot().members[1].alive,"charged once, target remains dead");
        Check(!run.BeginMachineRevive("payer",Spit())&&run.Snapshot().members[0].wallet==250,"busy duplicate never charged");
        Check(!run.StepMachineRevive(12.999f)&&!run.Snapshot().members[1].alive,"not alive before spit");
        Check(run.StepMachineRevive(13)&&run.Snapshot().members[1].alive,"alive at three seconds");
        Check(run.MachineHeld(2,15.629f)&&!run.MachineHeld(2,15.631f)&&!run.MachineHeld(1,14),"only target held until recovery ends");
        Check(!run.StepMachineRevive(14)&&run.MachineRevive!=null,"completion idempotent and show retained");
        var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var view=NativeExpeditionView.Build(run,"payer");
        var wire=JsonSerializer.Deserialize<ExpeditionView>(JsonSerializer.Serialize(view,options),options);
        Check(wire.machineRevive.completed&&wire.machineRevive.spit.Land.z==3&&wire.machineRevive.spit.At==10,"late guest receives complete timed record");
        var copy=run.Snapshot();copy.machineRevive.spit.Land=new V3(9,9,9);Check(run.MachineRevive.spit.Land.z==3,"snapshot does not alias authority record");
        run.StepMachineRevive(16.6f);Check(run.MachineRevive==null,"record expires at end");
        run=Run(new[]{"second-wind"});Check(run.BeginMachineRevive("payer",Spit())&&run.Snapshot().members[0].wallet==0,"Second Wind truly free at zero wallet");
        run=Run(new[]{"permadeath"});run.Credit("payer","fund",500);Check(!run.BeginMachineRevive("payer",Spit()),"permadeath refuses");
        run=Run();run.Credit("payer","fund",500);run.BeginMachineRevive("payer",Spit());run.Disconnect("dead");run.StepMachineRevive(12);Check(run.MachineRevive==null&&run.Snapshot().members[0].wallet==500,"disconnect before spit refunds exactly once");run.StepMachineRevive(14);Check(run.Snapshot().members[0].wallet==500,"refund idempotent");
        run=Run();run.Credit("payer","fund",500);run.BeginMachineRevive("payer",Spit());run.Defeat("payer");Check(run.StepMachineRevive(13),"paid revive survives payer death");
        run=Run();run.Credit("payer","fund",500);run.EnableAreas();run.MoveMembers("hub","ward",new[]{"dead"});run.ReprintPlacement=(_,__)=>false;
        Check(!run.BeginMachineRevive("payer",Spit())&&run.Snapshot().members[0].wallet==500,"failed cross-area placement does not charge");
        run.ReprintPlacement=(_,target)=>run.MoveMembers("ward","hub",new[]{target});Check(run.BeginMachineRevive("payer",Spit())&&run.Snapshot().members.Single(m=>m.account=="dead").alive==false,"remote corpse moves before timed revival");
        run.RecordMachineDeath("dead",new NativeReviveDead{Cause="Recorded",Body=new V3(4,0,5),Located=true,DiedAt=2,Place="Ward"});
        view=NativeExpeditionView.Build(run,"payer");Check(view.Member("dead").death.Cause=="Recorded"&&view.Member("dead").death.Body.z==5,"death details projected");
        Console.WriteLine("PASS revive machine authority: "+n+" checks");
    }
}
