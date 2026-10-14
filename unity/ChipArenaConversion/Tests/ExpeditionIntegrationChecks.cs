using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class ExpeditionIntegrationChecks
{
    private static int count;
    private static readonly JsonSerializerOptions Codec=new JsonSerializerOptions{IncludeFields=true};
    private static T Copy<T>(T x)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x,Codec),Codec);
    private static void Check(bool yes,string why){count++;if(!yes)throw new Exception("Expedition integration: "+why);}
    private static NativeExpedition Ready()
    {
        var run=NativeExpedition.Create("integration","city","host","pistol",42,new[]{new ExpeditionInstance{id="ward",target=1}});
        run.Join("guest","pistol");run.Enter("ward");run.Progress("quota",1);run.StartBosses(new[]{"boss"});run.DefeatBoss("boss");
        run.VoteExit("host",true);run.VoteExit("guest",true);run.Tick(.01);run.SetSafe("host",true);run.SetSafe("guest",true);return run;
    }
    public static void Run(string root)
    {
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),Codec);
        var run=Ready();run.Credit("host","money",100);run.Credit("guest","money",50);
        var session=new ExpeditionSession(run,data,_=>{});session.Checkpoint();
        var batch=ExpeditionViewBatch.Build(run,new[]{"host","guest"},session.Sources);
        Check(batch.Public.mine==null&&batch.Public.viewer==null,"public frame contains no personal state");
        var receiver=new ExpeditionViewReceiver("guest");
        Check(!receiver.Private(Copy(batch.Private[0])),"wrong owner rejected");
        Check(!receiver.Private(Copy(batch.Private[1]))&&receiver.Public(Copy(batch.Public)),"private-before-public pairs");
        Check(receiver.View.mine.wallet==50&&NativeExpeditionView.Leaks(receiver.View,run.Snapshot()).Count==0,"private wallet isolated");
        run.Tick(.1);var newer=ExpeditionViewBatch.Build(run,new[]{"guest"},session.Sources);
        Check(!receiver.Public(Copy(newer.Public)),"public alone does not expose mixed versions");
        Check(!receiver.Private(Copy(batch.Private[1])),"old private cannot join new public");
        Check(receiver.Private(Copy(newer.Private[0])),"new pair publishes");
        Check(!receiver.Public(Copy(batch.Public))&&!receiver.Private(Copy(batch.Private[1])),"old complete pair rejected");
        var next=run.Snapshot();next.epoch++;next.revision=0;var migrated=ExpeditionViewBatch.Build(new NativeExpedition(next),new[]{"guest"},null);
        receiver.Public(Copy(migrated.Public));Check(receiver.Private(Copy(migrated.Private[0]))&&receiver.View.epoch==next.epoch,"new epoch wins despite lower revision");
        Check(!receiver.Public(Copy(newer.Public)),"old epoch rejected");
        session.SetWorld(new[]{new ExpeditionWorldAnchor{id="machine",area="city",kind="machine",x=10,y=1,z=12},new ExpeditionWorldAnchor{id="chest",area="city",kind="chest",x=3}},new[]{new ExpeditionChestOffer{id="chest",generation="g1",price=30,cooldown=4}});
        string drop=run.Snapshot().rewards[0].id;Check(run.PlaceReward(drop,1,2,3),"place reward at authority spawn");
        var restored=new NativeExpedition(Copy(run.Snapshot()));Check(restored.Snapshot().anchors.Any(a=>a.id==drop&&a.y==2),"reward position persists");
        var world=NativeExpeditionView.Build(run,"guest");Check(world.chests[0].price==30&&world.anchors.Length==3,"world anchors and price project publicly");
        world.anchors[0].x=99;Check(run.Snapshot().anchors[0].x==10,"world projection is detached");
        var router=new ExpeditionCommandRouter(session);string reward=run.Snapshot().rewards.First(r=>r.owner=="guest").id;
        var req=new ExpeditionCommandRequest{run="integration",epoch=1,transaction="claim1",intent=ExpeditionIntent.Claim,target=reward};
        var stale=Copy(req);stale.epoch=2;Check(router.Execute("guest",stale).state==ExpeditionCommandState.Refused,"wrong epoch command rejected");
        Check(router.Execute("stranger",req).state==ExpeditionCommandState.Refused,"unbound sender refused");
        Check(router.Execute("host",req).state==ExpeditionCommandState.Refused,"cannot claim other owner");
        Check(router.Execute("guest",req).state==ExpeditionCommandState.Pending,"guest claim accepted");
        Check(router.Execute("guest",req).state==ExpeditionCommandState.Pending,"retransmission returns original result");
        req.intent=ExpeditionIntent.Reprint;Check(router.Execute("guest",req).state==ExpeditionCommandState.Refused,"transaction reuse with changed payload refused");
        var claimed=run.Snapshot().rewards.First(r=>r.id==reward);
        Check(claimed.offers.Length>0&&claimed.offers.All(id=>new[]{"epic","legendary","unique"}.Contains(data.chips.First(c=>c.id==id).rarity)),"boss offers are catalog Epic+");
        string chip=claimed.offers[0];Check(session.Execute("guest",new ExpeditionCommand(ExpeditionIntent.Select,reward,chip)).state==ExpeditionCommandState.Pending,"select catalog chip");
        Check(run.Snapshot().rewards.First(r=>r.id==reward).selected==chip,"chosen id persists");
        Check(NativeExpeditionView.Build(run,"guest").Reward(reward).selected==chip,"private history carries selected chip");
        ExpeditionCommandRequest sent=null;var view=NativeExpeditionView.Build(run,"guest");
        var guest=new ExpeditionGuestCommands("guest",()=>view,r=>sent=r);var tracker=new ExpeditionIntentTracker();var command=new ExpeditionCommand(ExpeditionIntent.Reprint,"host");
        tracker.Submit(guest,command,view,0);guest.Reply(new ExpeditionCommandReply{transaction=sent.transaction,state=ExpeditionCommandState.Refused,reason="range"});
        Check(tracker.State(command,view,.01).state==ExpeditionCommandState.Refused,"guest refusal reaches tracker immediately");
        tracker.Submit(guest,command,view,1);guest.Reply(new ExpeditionCommandReply{transaction=sent.transaction,state=ExpeditionCommandState.Confirmed});
        // Use downed target so state cannot itself confirm the reprint.
        run.Defeat("host");view=NativeExpeditionView.Build(run,"guest");
        Check(tracker.State(command,view,1.1).state==ExpeditionCommandState.Pending,"ACK cannot claim confirmation");
        run=Ready();bool fail=true;ExpeditionSavepoint written=null;
        session=new ExpeditionSession(run,data,s=>{if(fail)throw new IOException("disk full");written=s.Copy();});
        long rev=run.Snapshot().revision;
        Check(session.Extract("guest").state==ExpeditionCommandState.Failed&&session.Run.Snapshot().revision==rev&&!run.Snapshot().members[1].extracted,"failed save leaves authority unchanged");
        fail=false;Check(session.Extract("guest").state==ExpeditionCommandState.Pending&&written.extracted.Contains("guest"),"save succeeds before extraction publish");
        Check(NativeExpeditionView.Build(session.Run,"guest",session.Sources).mine.extractionSaved,"durable source confirms extraction");
        bool rejected=false;try{new ExpeditionSession(Ready(),data,_=>{},written);}catch(ArgumentException){rejected=true;}
        Check(rejected,"saved tombstones cannot be overwritten by a fresh authority");
        run=Ready();run.Credit("host","cash",100);run.Defeat("guest");session=new ExpeditionSession(run,data,_=>{});
        session.ReprintEvidence=(_,__)=>new ExpeditionReprintEvidence{cost=20,machine=true,remote=true,uninterrupted=true,distance=5,range=3,channelSeconds=3,requiredSeconds=2};
        Check(session.Execute("host",new ExpeditionCommand(ExpeditionIntent.Reprint,"guest")).state==ExpeditionCommandState.Refused&&run.Snapshot().members[0].wallet==100,"reprint rejects range before debit");
        session.ReprintEvidence=(_,__)=>new ExpeditionReprintEvidence{cost=20,machine=true,remote=true,uninterrupted=true,distance=2,range=3,channelSeconds=3,requiredSeconds=2};
        Check(session.Execute("host",new ExpeditionCommand(ExpeditionIntent.Reprint,"guest")).state==ExpeditionCommandState.Pending&&run.Snapshot().members[0].wallet==80&&run.Snapshot().members[1].alive,"validated reprint debits and revives");
        run=Ready();run.BeginFinal();run.Defeat("guest");Check(run.FinishFinal(3),"final rewards issue");
        var final=run.Snapshot().dice.Last();Check(final.each==3&&final.eligible.SequenceEqual(new[]{"host"})&&run.Snapshot().rewards.Count(r=>r.id.StartsWith(final.boss+":"))==3,"final frozen survivors and reward conservation");
        Check(!run.FinishFinal(3),"duplicate final death cannot reissue");
        Check(run.VoteContinue("host",true)&&!run.ContinueReady,"one pilot cannot force loop");
        Check(run.VoteContinue("guest",true)&&run.AdvanceWhenReady()&&run.Snapshot().continueVotes.Length==0,"all connected pilots can choose continue, votes reset");
        var evidence=new ExpeditionReprintEvidence{cost=10,machine=true,uninterrupted=true,range=3,distance=4,channelSeconds=3,requiredSeconds=2};
        Check(!evidence.Valid,"out of range reprint denied");evidence.distance=2;Check(evidence.Valid,"valid machine channel");evidence.uninterrupted=false;Check(!evidence.Valid,"interrupted channel denied");
        var metrics=new NativeConnectionMetrics();int a=metrics.Ping(0);metrics.Pong(a,.1);int b=metrics.Ping(1);metrics.Pong(b,1.15);metrics.Ping(2);metrics.Ping(11);
        var report=metrics.Report(true);Check(Math.Abs(report.loss-1d/3)<.001&&Math.Abs(report.jitterMs-50)<.001,"measured loss and successive RTT jitter");
        Console.WriteLine("PASS Expedition integration ("+count+" assertions)");
    }
}
