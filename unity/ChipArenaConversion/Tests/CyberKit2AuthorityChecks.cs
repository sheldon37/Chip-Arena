using System;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class CyberKit2AuthorityChecks
{
    public static void Run()
    {
        int checks=0;void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Cyber v2 rules: "+why);}
        NativeCyberKit Kit(bool ranged=false){var s=new NativeCyberKit();NativeCyberKit2Rules.Ensure(s);s.Ranged.Ranged=ranged;return s;}
        {
            // Model Unity's inline null expansion; System.Text.Json alone preserves nulls.
            var legacy=new NativeCyberKit();legacy.CapturePresence();
            legacy.Kit2=new CyberKit2MeleeRecord();legacy.Kit2R=new CyberKit2RangedRecord();
            Check(!legacy.Valid(),"expanded absent v2 records reproduce legacy rejection");
            legacy.RestorePresence();Check(legacy.Valid()&&legacy.Kit2==null&&legacy.Kit2R==null,"absent v2 records restored");
            var v2=Kit();v2.Kit2R.Ready[0]=27;v2.CapturePresence();v2.RestorePresence();v2.RestorePresence();
            Check(v2.Valid()&&v2.Kit2R.Ready[0]==27,"present v2 survives repeated restoration");
            v2.Kit2R.Ready[0]=float.NaN;v2.RestorePresence();Check(!v2.Valid(),"present malformed v2 remains rejected");
            var missing=Kit();missing.Kit2R=null;missing.CapturePresence();missing.Kit2R=new CyberKit2RangedRecord();missing.RestorePresence();
            Check(!missing.Valid(),"missing required v2 record remains rejected");
        }
        for(int slot=0;slot<12;slot++)
        {
            if(slot==6)continue;var s=Kit(slot>=7);float mana=100;
            Check(NativeCyberKit2Rules.Begin(s,slot,10,ref mana,1,1,1,1),"begin "+slot);
            Check(Math.Abs(mana-(100-CyberKit2Spec.Costs[slot]))<.001f,"cost "+slot);
            Check(s.Valid(),"valid at cast start "+slot);Check(NativeCyberKit2Rules.Move(s,10)==CyberKit2Spec.MoveScale[slot],"move "+slot);
            float left=mana;Check(!NativeCyberKit2Rules.Begin(s,slot,10,ref mana,1,1,1,1)&&mana==left,"no double commitment "+slot);
            NativeCyberRules.Interrupt(s,10.01f);Check(s.Resolved&&s.Until==10.01f&&s.Valid(),"cut before release "+slot);
            var copy=s.ViewCopy();if(slot>=7)copy.Kit2R.Ready[slot-7]=999;else copy.Kit2.Ready[slot]=999;
            Check((slot>=7?s.Kit2R.Ready[slot-7]:s.Kit2.Ready[slot])!=999,"deep copy "+slot);
            var options=new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true};var decoded=JsonSerializer.Deserialize<NativeCyberKit>(JsonSerializer.Serialize(s,options),options);Check(decoded.Valid(),"wire round trip "+slot);
        }
        {
            var s=Kit();float mana=100;float t=10;
            for(int i=0;i<7;i++){Check(NativeCyberKit2Rules.Begin(s,2,t,ref mana,1,1,1,1)&&s.Combo==i%5,"five hit combo");t=s.Until+.01f;}
            t+=1;Check(NativeCyberKit2Rules.Begin(s,2,t,ref mana,1,1,1,1)&&s.Combo==0,"combo reset");
        }
        {
            var s=Kit(true);float mana=100;float t=10;
            for(int i=0;i<6;i++){Check(NativeCyberKit2Rules.Begin(s,7,t,ref mana,1,1,1,1)&&s.Kit2R.PlasmaShot==i,"plasma counter");Check(CyberKit2Spec.PlasmaHand(i)==(i%3==2?2:i%2),"alternating hands");t=s.Until+.001f;}
            s.Kit2.OverclockAt=0;s.Kit2.OverclockUntil=30;Check(NativeCyberKit2Rules.Begin(s,7,t,ref mana,1,1,1,1)&&Math.Abs(s.Until-t-.3f/1.6f)<.001f,"overclock fire interval");
            s.Kit2R.Ready=null;Check(!s.Valid(),"reject malformed slots");
        }
        {
            var state=new NativePredictionState{Cyber=Kit(true),CyberMana=100,CyberRate=1,CyberCost=1,CyberCooldown=1,CyberUltimateCost=1,Time=10,CanMove=true,CanSprint=true,Speed=6.8f,Grounded=true,JumpSpec="cyberlord",JumpSpeed=8.5f};
            var moved=NativePrediction.Step(state,new InputFrame{Shoot=true,Z=1},(a,b)=>b);
            Check(moved.Position.z>0&&moved.Cyber.Version==2,"caster predicts movement while throwing");
        }
        {
            var gate=new NativeStageBarrier();gate.Begin(2,"siren",4,new[]{1,2,3},10);
            Check(!gate.Report(2,1,"siren",4)&&!gate.Report(2,2,"foundry",4)&&!gate.Report(2,2,"siren",3),"stage stale reports refused");
            Check(!gate.Report(4,2,"siren",4),"late arrival excluded");
            Check(gate.Report(1,2,"siren",4)&&gate.Report(1,2,"siren",4)&&gate.Ready==1,"stage report idempotent");
            gate.Step(11,id=>id!=3);Check(gate.Waiting&&gate.Required==2,"disconnect removes required seat");
            gate.Report(2,2,"siren",4);gate.Step(12,id=>true);Check(!gate.Waiting,"all ready releases clock");
            gate.Begin(3,"siren",5,new[]{1,2},20);gate.Step(31.99f,id=>true);Check(gate.Waiting,"gate waits before timeout");gate.Step(32,id=>true);Check(!gate.Waiting,"12 second fallback");gate.Step(33,id=>true);Check(!gate.Waiting,"released gate never reopens");
        }
        Console.WriteLine("PASS Cyber v2 authority rules: "+checks+" checks");
    }
}
