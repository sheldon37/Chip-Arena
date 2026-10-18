using System;
using System.Linq;
using ChipArena.UnityConversion;
internal static class ExpeditionCoopChecks
{
    public static void Run(ExpeditionLayout layout,ConversionContent content)
    {
        int n=0;void Check(bool ok,string why){n++;if(!ok)throw new Exception("Expedition co-op: "+why);}
        var load=new NativeExpeditionLoadWindow();load.Begin("startup",100);
        Check(load.Active(113)&&load.Active(159.9)&&!load.Active(160),"slow startup survives normal timeout but has fixed deadline");
        load.Begin("startup",150);Check(!load.Active(160),"duplicate notices cannot extend load deadline");
        load.Begin("hub:1",161);int packets=0;for(double t=161;t<221;t+=.05)if(load.Send(t))packets+=3;
        Check(packets<=36&&packets+60<128,"bootstrap retries plus one-second pings fit receive queue during sixty-second load");
        load.Complete();Check(!load.Active(162),"ready restores normal timeout");load.Begin("hub:1",170);Check(!load.Active(170),"old ticket cannot reopen completed grace");
        load.Begin("ward:2",180);Check(load.Active(193),"new area gets its own loading deadline");load.Reset();Check(!load.Active(193),"leave clears loading state");
        var ids=new[]{"host","guest-a","guest-b","guest-c"};
        Check(NativeExpeditionParty.Validate(ids,true,true)==null,"four authenticated ready players");
        Check(NativeExpeditionParty.Validate(ids.Concat(new[]{"fifth"}),true,true)!=null,"capacity");
        Check(NativeExpeditionParty.Validate(new[]{"host","host"},true,true)!=null,"duplicate identity");
        Check(NativeExpeditionParty.Validate(ids,false,true)!=null&&NativeExpeditionParty.Validate(ids,true,false)!=null,"authentication and ready gates");
        Check(NativeExpeditionParty.Validate(ids,true,true,false)!=null,"old client cannot strand the area barrier");
        var options=new NativeRoomOptions{expedition=true,cooperative=false,bots=3};options.Normalize(content);
        Check(options.cooperative&&options.bots==0,"Expedition forces cooperative humans");
        options.expedition=false;options.bots=3;options.Normalize(content);Check(options.bots==3,"wave bots preserved");
        var run=NativeExpedition.Create("coop-check","city",ids[0],"pistol",17,new[]{new ExpeditionInstance{id="ward",target=1}});
        foreach(string id in ids.Skip(1))Check(run.Join(id,"pistol"),"join "+id);
        var session=new ExpeditionSession(run,content,_=>{});
        var director=new ExpeditionDirector(session,layout,_=>new V3(0,0,0),(_,__)=>true,10,20,30);
        director.BindLayoutIdentity(new string('a',64));Check(director.BeginAreaLoad(),"initial load");
        ExpeditionAreaReadyRequest Ticket()=>ExpeditionAreaReady.Capture(NativeExpeditionView.Build(run,ids[0],session.Sources));
        var ticket=Ticket();
        Check(!ExpeditionAreaReady.Accept(session,"stranger",ticket),"non-member readiness refused");
        foreach(string id in ids.Take(3))Check(ExpeditionAreaReady.Accept(session,id,ticket),"ready "+id);
        Check(!director.CompleteAreaLoad((_,__)=>true),"cannot place before last guest loads");
        Check(run.Disconnect(ids[3])&&run.Reconnect(ids[3]),"authenticated reconnect during load");
        Check(!run.Reconnect("stranger")&&!run.Join("stranger","pistol"),"loading reconnect cannot add new identities");
        Check(ExpeditionAreaReady.Accept(session,ids[3],ticket),"reconnected pilot readiness");
        Check(director.CompleteAreaLoad((_,points)=>points.Count==4&&points.Values.Select(p=>p.x).Distinct().Count()==4),"all four receive distinct slots atomically");
        Check(director.EnterDoor(ids[1],"city-door"),"guest can initiate party transfer");
        Check(!ExpeditionAreaReady.Accept(session,ids[1],ticket),"previous area ticket rejected");
        ticket=Ticket();foreach(string id in ids)Check(ExpeditionAreaReady.Accept(session,id,ticket),"instance ready "+id);
        Check(director.CompleteAreaLoad((_,points)=>points.Count==4),"party enters instance");
        Check(run.Defeat(ids[0]),"host down");
        Check(run.Progress("guest-kill",1)&&run.StartBosses(new[]{"boss"})&&run.DefeatBoss("boss"),"surviving guests can finish encounter");
        Check(run.Snapshot().members.Count(m=>m.alive)==3,"host death preserves living guests");
        var batch=ExpeditionViewBatch.Build(run,ids,session.Sources);
        Check(batch.Public.mine==null&&batch.Public.viewer==null,"public frame has no private player data");
        Check(batch.Private.Length==4&&batch.Private.All(f=>f.mine.account==f.viewer),"one private projection per account");
        foreach(string id in ids.Skip(1))Check(run.VoteExit(id,true),"living guest exit vote");
        session.Tick(11);director.Step();Check(run.Snapshot().area=="city"&&director.Barrier.active,"party returns to hub after guest votes");
        ticket=Ticket();foreach(string id in ids)Check(ExpeditionAreaReady.Accept(session,id,ticket),"return ready "+id);
        Check(director.CompleteAreaLoad((_,points)=>points.Count==3&&!points.ContainsKey(ids[0])),"downed host stays down across transfer");
        Console.WriteLine("PASS Expedition co-op party and lifecycle: "+n+" checks");
    }
}
