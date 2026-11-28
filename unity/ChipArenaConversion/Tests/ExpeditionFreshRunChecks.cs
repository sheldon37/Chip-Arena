using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionFreshRunChecks
    {
        public static void Run(string root)
        {
            int count=0;void Check(bool ok,string why){if(!ok)throw new Exception("Fresh run: "+why);count++;}
            var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),new JsonSerializerOptions{IncludeFields=true});
            NativeExpedition New(string id)=>NativeExpedition.Create(id,"hub","host","pistol",1,new[]{new ExpeditionInstance{id="ward",target=1}});
            var run=New("fresh-one");run.Join("guest","bow");run.Enter("ward");run.Progress("quota",1);run.StartBosses(new[]{"boss"});run.DefeatBoss("boss");run.VoteExit("host",true);run.VoteExit("guest",true);run.Tick(.01);run.SetSafe("host",true);run.SetSafe("guest",true);
            var session=new ExpeditionSession(run,content);Check(session.FreshRun&&session.Sources.FreshRun,"default is nonpersistent");
            session.Checkpoint();Check(session.Sources.Saved==null,"checkpoint cannot fabricate save status");
            Check(session.Extract("guest").state==ExpeditionCommandState.Pending,"extraction commits without file writer");
            var view=NativeExpeditionView.Build(session.Run,"guest",session.Sources);
            Check(view.freshRun&&view.mine.extracted&&!view.mine.extractionSaved&&view.savedRevision==-1,"honest fresh-run extraction projection");
            Check(ExpeditionIntentTracker.Reflected(new ExpeditionCommand(ExpeditionIntent.Extract),view),"extraction confirmed without durable-save flag");
            Check(NativeExpeditionPresentation.Extraction(view).title=="EXTRACTED","no waiting-for-save message");
            Check(!session.Run.Reconnect("guest")&&!session.Run.Join("guest","bow"),"extracted member cannot rejoin same live run");
            Check(session.Run.Disconnect("host")&&session.Run.Reconnect("host"),"live reconnect retained");
            var next=new ExpeditionSession(New("fresh-two"),content);Check(next.Run.Snapshot().members.Single().wallet==0&&next.Run.Snapshot().instances.Single().progress==0,"new run has no prior progression");
            var legacy=new ExpeditionSession(run,content,_=>{});legacy.Checkpoint();bool rejected=false;
            try{new ExpeditionSession(run,content,null,legacy.Sources.Saved);}catch(ArgumentException){rejected=true;}
            Check(rejected,"fresh mode refuses saved checkpoint");
            string live=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionNetwork.cs"));
            Check(!live.Contains("NativeExpeditionPersistence.Open")&&live.Contains("new ExpeditionSession(run,content)"),"live launch has no disk store");
            Console.WriteLine("PASS Expedition fresh runs: "+count+" checks");
        }
    }
}
