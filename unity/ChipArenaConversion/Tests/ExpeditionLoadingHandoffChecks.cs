using System;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace ChipArena.UnityConversion
{
    internal static class ExpeditionLoadingHandoffChecks
    {
        private sealed class Placement:IExpeditionCohortPlacement
        {public int Applied;public bool Validate()=>true;public void Apply(){Applied++;}public void Rollback(){}}
        public static void Run(string root)
        {
            int n=0;void Check(bool ok,string why){if(!ok)throw new Exception("Expedition loading handoff: "+why);n++;}
            var codec=new JsonSerializerOptions{IncludeFields=true};
            T Wire<T>(T value)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,codec),codec);
            foreach(int party in new[]{2,3,4})foreach(bool reduced in new[]{false,true})
            {
                var accounts=Enumerable.Range(1,party).Select(i=>"pilot"+i).ToArray();
                var run=NativeExpedition.Create("loading-run","hub",accounts[0],"pistol",17,new[]{new ExpeditionInstance{id="ward",target=12}});
                foreach(var who in accounts.Skip(1))Check(run.Join(who,"pistol"),"join guest");run.EnableAreas();
                var cohorts=new ExpeditionCohorts("loading-run",1,new string('a',64),"hub","ward");
                foreach(var who in accounts){Check(cohorts.Add(who,"hub"),"register pilot");cohorts.Acknowledge(who,cohorts.Member(who).Ticket);}
                var old=cohorts.Member(accounts[1]).Ticket;var placement=new Placement();
                Check(cohorts.Begin("join:1","hub","ward",accounts,NativeExpeditionLoadWindow.Seconds,(_,__,___)=>placement),"whole party reserves destination");
                ExpeditionView Project(string area,string who)
                {
                    var view=NativeExpeditionView.Build(run.ForArea(area),who);
                    view.mine=NativeExpeditionView.Build(run.ForArea(cohorts.Member(who).Area),who).mine;
                    view.members=NativeExpeditionView.Build(run,null).members;return view;
                }
                foreach(var who in accounts)
                {
                    var projection=ExpeditionAreaProjection.Build(who,cohorts,Project,null);
                    var publicFrame=Wire(projection.Public);var personal=Wire(projection.Private);var ticket=publicFrame.areaIdentity;
                    var receiver=new ExpeditionViewReceiver(who);
                    Check(receiver.BindArea(ticket)&&!receiver.Public(publicFrame)&&receiver.Private(personal),"destination public/private pair arrives before transfer commit");
                    Check(receiver.View.loading&&receiver.View.area=="ward"&&receiver.View.mine.account==who,"each recipient sees its own destination load");
                    var inbox=new ExpeditionLoadInbox<string>();var loading=new NativeStageLoading();
                    Check(inbox.Offer(ticket,1,"destination-1"),"queue guest construction");loading.Request(StageLoadKind.Area,reduced);
                    var signal=new StageLoadSignals{Stage=true,Ready=true,Waiting=true,WaitBudget=NativeStageLoading.ExpeditionWaitTimeout};
                    // Elapsed time without an actual GUI repaint must never start heavy work.
                    for(int frame=0;frame<120;frame++)loading.Step(1/60f,signal,0);
                    Check(!loading.CanBuild&&inbox.Pending,"world construction waits for the loading details to paint");
                    Check(inbox.Offer(ticket,3,"destination-3")&&!inbox.Offer(ticket,2,"stale")&&!inbox.Offer(old,1000,"old-area"),"coalesce latest frame and reject old area/sequence");
                    loading.ContentPresented();Check(loading.CanBuild&&loading.TakeAction(),"paint releases construction exactly once");
                    Check(inbox.Take()=="destination-3"&&!inbox.Pending&&inbox.Take()==null&&!loading.TakeAction(),"only newest destination builds once");
                    Check(!inbox.Applied(ticket),"received UI ticket is not proof that the destination was built");
                    inbox.MarkApplied(ticket);Check(inbox.Applied(ticket)&&!inbox.Applied(old),"only the constructed ticket may acknowledge readiness");
                    inbox.Reset();Check(!inbox.Applied(ticket),"reconnect clears constructed ticket");
                    var ready=ExpeditionAreaReady.Capture(receiver.View);
                    Check(ready!=null&&ready.target=="ward"&&cohorts.Acknowledge(who,ticket),"loaded recipient acknowledges its destination ticket");
                    if(who!=accounts.Last())Check(!cohorts.Commit("join:1")&&placement.Applied==0,"host readiness cannot move the party before guests load");
                }
                // A slow guest gets the same loading window as the transport.
                cohorts.Advance(45);Check(cohorts.Commit("join:1")&&placement.Applied==1,"party commits once after all guest loads, including a 45-second load");
                Check(run.MoveMembers("hub","ward",accounts)&&run.ForArea("ward").Snapshot().members.Length==party,"all members arrive together");
                var cancelled=new ExpeditionLoadInbox<string>();var current=cohorts.Member(accounts[1]).Ticket;
                Check(cancelled.Offer(current,1,"abandoned"),"pending scene exists before disconnect");cancelled.Reset();Check(cancelled.Take()==null,"disconnect discards deferred scene");
            }
            var cadence=new NativeExpeditionLoadWindow();cadence.Begin("load",0);
            Check(cadence.Send(0,NativeExpeditionLoadWindow.SnapshotSeconds),"first loading snapshot immediate");
            Check(!cadence.Send(.1,NativeExpeditionLoadWindow.SnapshotSeconds)&&cadence.Send(.25,NativeExpeditionLoadWindow.SnapshotSeconds),"loading snapshots refresh at four Hz");
            var recovered=new ExpeditionLoadInbox<string>();
            var recoveryTicket=new ExpeditionCohortTicket{run="recover",epoch=1,area="ward",layout=new string('a',64),serial=1};
            recovered.Offer(recoveryTicket,1,"first");var interrupted=new NativeStageLoading();interrupted.Request(StageLoadKind.Area,false);interrupted.Cancel();
            Check(recovered.Pending&&!recovered.Applied(recoveryTicket)&&!interrupted.ActionPending,"cancelled UI leaves destination pending for rescheduling");
            recovered.Offer(recoveryTicket,2,"newest");interrupted.Request(StageLoadKind.Area,false);
            for(int i=0;i<60;i++){interrupted.Step(1/60f,new StageLoadSignals{Waiting=true},0);interrupted.ContentPresented();}
            Check(interrupted.CanBuild&&interrupted.TakeAction()&&recovered.Take()=="newest","rescheduled load constructs newest snapshot");
            recovered.MarkApplied(recoveryTicket);Check(recovered.Applied(recoveryTicket),"recovered destination can acknowledge");
            // Guard the actual network/GUI handoff, since the managed runner cannot create Unity scenes.
            string runtime=Path.Combine(root,"Assets/ChipArena/Runtime");
            var network=File.ReadAllText(Path.Combine(runtime,"NativeNetwork.cs"));
            Check(network.Contains("expeditionLoadInbox.Offer(frame.areaIdentity,frame.sequence,message)")&&network.Contains("StageLoadKind.Area,ApplyPendingExpeditionFrame)"),"guest network dispatch uses the deferred load path");
            var stages=File.ReadAllText(Path.Combine(runtime,"NativeStageLoadingUI.cs"));
            Check(stages.Contains("stageLoad.CanBuild&&stageLoad.TakeAction()")&&stages.Contains("stageLoad.ContentPresented()"),"actual scene action waits for repaint");
            var deployment=File.ReadAllText(Path.Combine(runtime,"NativeExpeditionDeploymentRoom.cs"));
            Check(deployment.Contains("deploymentTravelAt=Time.unscaledTime+NativeDeployPresentation.TravelAt")&&deployment.Contains("Time.unscaledTime>=deploymentTravelAt")&&deployment.Contains("deploymentTravelAt=-1"),"host travel waits for the existing drop timeline and can be cancelled");
            Check(File.ReadAllText(Path.Combine(runtime,"NativeExpeditionAreaTransfers.cs")).Contains("accounts,NativeExpeditionLoadWindow.Seconds,"),"cohort timeout agrees with transport loading window");
            Console.WriteLine("PASS Expedition loading handoff: "+n+" checks (2/3/4 players, repaint gate, latest snapshot, all-ready barrier, stale tickets and cancellation)");
        }
    }
}
