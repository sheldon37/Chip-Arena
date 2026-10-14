using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class ExpeditionDirectorChecks
{
    public static void Run(string root)
    {
        int count=0;void Check(bool ok,string why){count++;if(!ok)throw new Exception("Expedition director: "+why);}
        var slots=Enumerable.Range(0,8).Select(i=>new{x=i*2,y=0,z=0,yaw=0}).ToArray();
        var volume=new{x=0,y=1,z=0,sx=30,sy=4,sz=20,yaw=0};var point=new{x=0,y=0,z=0,yaw=0};
        object Area(bool world)=>new{id=world?"city":"ward",role=world?"world":"instance",map=world?"city-map":"ward-map",
            anchors=world?new[]{new{id="city-door",kind="door",building="ward",x=0,y=0,z=0,yaw=0},new{id="cache",kind="chest",building="",x=2,y=0,z=0,yaw=0},new{id="machine",kind="machine",building="",x=3,y=0,z=0,yaw=0}}:new[]{new{id="ward-door",kind="door",building="ward",x=0,y=0,z=0,yaw=0}},
            doors=new[]{new{anchor=world?"city-door":"ward-door",interact=volume,closure=volume,approach=point,entries=world?Array.Empty<object>():slots.Cast<object>().ToArray(),returns=world?slots.Cast<object>().ToArray():Array.Empty<object>()}},
            interactions=world?new[]{new{anchor="cache",x=2,y=0,z=0,range=2.5},new{anchor="machine",x=3,y=0,z=0,range=2.5}}:Array.Empty<object>(),
            sanctuary=new{volumes=world?new[]{volume}:Array.Empty<object>(),slots=world?slots.Cast<object>().ToArray():Array.Empty<object>()},
            gates=world?new[]{new{id="final-gate",opens="final",closure=volume}}:Array.Empty<object>(),
            boss=new{points=new[]{point},trigger=volume},spawns=new[]{new{e=0,id="entry",kind="door",from=point,exit=point,yaw=0,level=0,clear=2,allow=new[]{"shambler"}}}};
        string json=JsonSerializer.Serialize(new{schema=1,revision="layout-test",world="city",instances=new[]{"ward"},areas=new[]{Area(true),Area(false)}});
        var layout=NativeExpeditionLayout.Parse(json);Check(layout.areas.Length==2&&layout.Area("city").anchors[0].area=="city","area propagated to public anchors");
        void Refuse(string text,string why){bool failed=false;try{NativeExpeditionLayout.Parse(text);}catch(FormatException){failed=true;}Check(failed,why);}
        Refuse(json.Replace("\"schema\":1","\"schema\":2"),"unknown schema");
        Refuse(json.Replace("\"x\":0,",""),"missing position cannot become origin");
        Refuse(json.Replace("\"building\":\"ward\"","\"building\":\"absent\""),"unpaired building");
        Refuse(json.Replace("\"range\":2.5","\"range\":-1"),"negative reach");
        Check(new ExpeditionVolume{x=2,y=1,z=3,sx=2,sy=2,sz=6,yaw=90}.Contains(new V3(4,1,3)),"rotated volume");
        var codec=new JsonSerializerOptions{IncludeFields=true};var content=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),codec);
        var run=NativeExpedition.Create("director-test","city","host","pistol",17,new[]{new ExpeditionInstance{id="ward",target=1}});int writes=0;bool fail=false;
        var session=new ExpeditionSession(run,content,_=>{if(fail)throw new IOException("test disk failure");writes++;});V3 at=new V3(2,0,0);bool los=true;
        var director=new ExpeditionDirector(session,layout,_=>at,(_,__)=>los,10,20,30);
        Check(director.CanPurchase("host","cache"),"host range evidence");los=false;Check(!director.CanPurchase("host","cache"),"world occlusion refuses");los=true;
        director.Step();Check(writes==1,"initial checkpoint");session.Tick(.25);director.Step();Check(writes==1,"clock-only revision does not rewrite save");
        session.Run.Credit("host","cash",100);Check(session.Purchase("host","cache").state==ExpeditionCommandState.Pending,"purchase through session");director.Step();var offer=run.Snapshot().chests[0];Check(!offer.sold&&offer.cooldown>run.Elapsed&&offer.generation!="0:cache:0","restock with fresh generation and persisted deadline");
        Check(session.Purchase("host","cache").state==ExpeditionCommandState.Refused,"restock cooldown enforced");
        Check(director.MachineEvidence("host","machine",2,true)?.Valid==false&&director.MachineEvidence("host","machine",3,true)?.Valid==true,"measured reprint channel");
        Check(director.MachineEvidence("host","machine",3,false)?.Valid==false,"interruption refuses reprint");
        fail=true;Check(!director.Checkpoint()&&director.SaveFailure!=null&&writes==1,"failed checkpoint keeps written savepoint");fail=false;
        run.Enter("ward");Check(director.MonsterDefeated("spawn-1",new V3(1,0,1),1,true),"death progresses encounter and positions drop");
        Check(!director.MonsterDefeated("spawn-1",new V3(1,0,1),1,true),"duplicate death does not issue rewards twice");
        Check(run.Snapshot().rewards.Length==2,"cache and monster rewards only");
        run.StartBosses(new[]{"TEST-boss"});Check(director.BossDefeated("TEST-boss",new V3(1,0,1)),"boss rewards positioned");
        Refuse(json.Replace("\"clear\":2","\"clear\":0"),"zero spawn clearance");
        Refuse(json.Replace("\"level\":0","\"level\":0.5"),"fractional spawn level");
        Refuse(json.Replace("\"final-gate\"","\"cache\""),"gate collides with public anchor ID");
        Refuse(json.Replace("\"opens\":\"final\"","\"opens\":\"other\""),"unsupported gate condition");
        Refuse(json.Replace("[\"shambler\"]","[\"shambler\",\"shambler\"]"),"duplicate spawn allow");
        Check(!NativeMapRules.ArenaSelectable(new ContentMap{id="expedition-city",rotation=true})&&NativeMapRules.ArenaSelectable(new ContentMap{id="siren"}),"expedition filtered even when rotation true");
        var geometry=new ConversionContent{maps=new[]{"city-map","ward-map"}.Select(id=>new ContentMap{id=id,bounds=new MapBounds{minX=-50,maxX=50,minZ=-50,maxZ=50,killY=-8,ceiling=40}}).ToArray()};
        int probes=0;NativeExpeditionLayout.ValidateGeometry(layout,geometry,null,(_,p,r,h)=>{probes++;return true;},(_,a,b,r,h)=>true);Check(probes>30,"geometry validates slots, anchors and heavy clearance");
        bool rejected=false;try{NativeExpeditionLayout.ValidateGeometry(layout,geometry,null,(_,p,r,h)=>true,(_,a,b,r,h)=>false);}catch(FormatException){rejected=true;}Check(rejected,"disconnected navigation refuses");
        rejected=false;try{NativeExpeditionLayout.ValidateGeometry(layout,geometry,null,(_,p,r,h)=>h<NativeHordeKinds.HulkHeight,(_,a,b,r,h)=>true);}catch(FormatException){rejected=true;}Check(rejected,"Hulk clearance refuses");
        layout.Area("city").gates[0].closure.x=49;rejected=false;try{NativeExpeditionLayout.ValidateGeometry(layout,geometry,null,(_,p,r,h)=>true,(_,a,b,r,h)=>true);}catch(FormatException){rejected=true;}Check(rejected,"volume extents refuse out-of-bounds");layout=NativeExpeditionLayout.Parse(json);
        var r2=NativeExpedition.Create("barrier-test","city","host","pistol",19,new[]{new ExpeditionInstance{id="ward",target=1}});r2.Join("guest","pistol");
        var s2=new ExpeditionSession(r2,content,_=>{});var d2=new ExpeditionDirector(s2,layout,_=>new V3(0,0,0),(_,__)=>true,10,20,30);string fingerprint=new string('a',64);d2.BindLayoutIdentity(fingerprint);
        s2.Sources.Seat=who=>who=="host"?1:who=="guest"?7:0;
        var initial=NativeExpeditionView.Build(r2,"guest",s2.Sources);
        Check(initial.areaBarrier.layout==fingerprint&&!initial.areaBarrier.active&&initial.areaBarrier.generation==0,"layout published before first area load");
        Check(initial.Member("host").seat==1&&initial.Member("guest").seat==7,"all members carry their authority seat");
        layout.compatibility=fingerprint;
        var startup=new ExpeditionDirector(new ExpeditionSession(NativeExpedition.Create("startup","city","host","pistol",1,new[]{new ExpeditionInstance{id="ward",target=1}}),content,_=>{}),layout,_=>new V3(),(_,__)=>true,10,20,30);
        Check(startup.Barrier.layout==fingerprint&&!startup.Barrier.active,"constructor publishes validated layout without explicit binding or load");
        Check(!d2.EnterDoor("stranger","city-door"),"nonmember cannot enter");
        Check(d2.EnterDoor("host","city-door")&&r2.Snapshot().loading,"entry freezes run behind barrier");var b2=d2.Barrier;
        Check(b2.target=="ward"&&b2.generation==1&&b2.required==2&&b2.ready==0,"target generation and counts");
        var ticket=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(r2,"guest",s2.Sources));
        Check(ticket!=null&&ticket.run=="barrier-test"&&ticket.epoch==1&&ticket.target=="ward"&&ticket.generation==1&&ticket.layout==fingerprint,"capture detached load ticket");
        Check(ExpeditionAreaReady.Capture(initial)==null,"inactive startup cannot acknowledge a load");
        ExpeditionAreaReadyRequest TicketCopy()=>JsonSerializer.Deserialize<ExpeditionAreaReadyRequest>(JsonSerializer.Serialize(ticket,codec),codec);
        var staleTicket=TicketCopy();staleTicket.run="previous-run";Check(!ExpeditionAreaReady.Accept(s2,"guest",staleTicket),"different run ACK refused");
        staleTicket=TicketCopy();staleTicket.epoch++;Check(!ExpeditionAreaReady.Accept(s2,"guest",staleTicket),"different authority epoch ACK refused");
        staleTicket=TicketCopy();staleTicket.generation++;Check(!ExpeditionAreaReady.Accept(s2,"guest",staleTicket),"different load generation ACK refused");
        staleTicket=TicketCopy();staleTicket.target="city";Check(!ExpeditionAreaReady.Accept(s2,"guest",staleTicket),"different area ACK refused");
        staleTicket=TicketCopy();staleTicket.layout=new string('b',64);Check(!ExpeditionAreaReady.Accept(s2,"guest",staleTicket),"different content fingerprint ACK refused");
        Check(!ExpeditionAreaReady.Accept(s2,"stranger",ticket)&&!ExpeditionAreaReady.Accept(s2,"guest",null)&&d2.Barrier.ready==0,"unknown sender and null ACK leave readiness unchanged");
        Check(ExpeditionAreaReady.Matches(ticket,NativeExpeditionView.Build(r2,"guest",s2.Sources)),"captured ticket matches current paired view");
        Check(!d2.Ready("guest","ward",0,fingerprint)&&!d2.Ready("guest","city",1,fingerprint)&&!d2.Ready("guest","ward",1,new string('b',64)),"stale target generation and fingerprint refused");
        Check(d2.Ready("host","ward",1,fingerprint)&&d2.Ready("host","ward",1,fingerprint)&&d2.Barrier.ready==1,"duplicate ACK does not inflate count");
        Check(!d2.CompleteAreaLoad((_,__)=>true),"missing peer freezes transfer");double clock=r2.Elapsed;s2.Tick(.2);Check(r2.Elapsed==clock,"loading pauses difficulty clock");
        Check(ExpeditionAreaReady.Accept(s2,"guest",TicketCopy())&&ExpeditionAreaReady.Accept(s2,"guest",TicketCopy())&&d2.Barrier.ready==2,"serialized peer ticket accepted idempotently");
        Check(!d2.CompleteAreaLoad((_,__)=>false)&&r2.Snapshot().loading,"failed placement remains frozen");
        Check(d2.CompleteAreaLoad((area,assigned)=>area=="ward"&&assigned.Count==2&&assigned["host"].x!=assigned["guest"].x)&&!r2.Snapshot().loading,"party placed in distinct authored slots");
        Check(!ExpeditionAreaReady.Accept(s2,"guest",ticket)&&!ExpeditionAreaReady.Matches(ticket,NativeExpeditionView.Build(r2,"guest",s2.Sources)),"completed load rejects stale completion");
        Check(d2.Closures.Single(c=>c.id=="ward-door").state=="sealing","instance door sealing");s2.Tick(1);Check(d2.Closures.Single(c=>c.id=="ward-door").state=="sealed","one second seal clock");
        r2.Progress("kill",1);r2.StartBosses(new[]{"TEST-boss"});Check(d2.RecordBossArrival("TEST-boss","test",a=>a=="test",new V3(3,4,5),80,100)&&!d2.RecordBossArrival("TEST-boss","test",a=>true,new V3(3,4,5),80,100),"boss arrival once per authority entity");
        var view=NativeExpeditionView.Build(r2,"host",s2.Sources);Check(view.bossArrivals.Length==1&&view.closures.Length==3&&!view.areaBarrier.active,"public state supplied");
        var boss=view.bossArrivals.Single();
        Check(boss.x==3&&boss.y==4&&boss.z==5&&boss.hp==80&&boss.maxHp==100,"arrival has actual spawn position and health");
        Check(!d2.UpdateBossHealth("city","TEST-boss",50,100)&&!d2.UpdateBossHealth("ward","absent",50,100),"health update scopes boss and area");
        Check(!d2.UpdateBossHealth("ward","TEST-boss",double.NaN,100)&&!d2.UpdateBossHealth("ward","TEST-boss",101,100)&&!d2.UpdateBossHealth("ward","TEST-boss",1,0)&&!d2.UpdateBossHealth("ward","TEST-boss",-1,100),"invalid health refused");
        long revision=r2.Snapshot().revision;
        Check(d2.UpdateBossHealth("ward","TEST-boss",25,120),"damage and max health changes recorded");
        Check(r2.Snapshot().revision==revision&&boss.hp==80,"health is detached source state, not a rules revision");
        ExpeditionView Receive()
        {
            var batch=ExpeditionViewBatch.Build(r2,new[]{"guest"},s2.Sources);
            var receiver=new ExpeditionViewReceiver("guest");
            receiver.Public(JsonSerializer.Deserialize<ExpeditionView>(JsonSerializer.Serialize(batch.Public,codec),codec));
            receiver.Private(JsonSerializer.Deserialize<ExpeditionPrivateFrame>(JsonSerializer.Serialize(batch.Private.Single(),codec),codec));
            return receiver.View;
        }
        var remote=Receive();
        Check(remote.Member("host").seat==1&&remote.Member("guest").seat==7&&remote.areaBarrier.layout==fingerprint,"late guest receives seats and fingerprint through public/private wire");
        Check(remote.bossArrivals.Single().hp==25&&remote.bossArrivals.Single().maxHp==120&&remote.bossArrivals.Single().x==3&&remote.bossArrivals.Single().spawned==boss.spawned,"late guest receives latest health and original spawn");
        remote.bossArrivals[0].hp=999;Check(d2.BossArrivals[0].hp==25,"boss projection detached");
        s2.Sources.Seat=who=>who=="guest"?4:0;
        Check(Receive().Member("guest").seat==4&&Receive().Member("host").seat==0,"seat rebind and unavailable pilot refresh without a rules revision");
        view.closures[0].state="tampered";Check(d2.Closures[0].state!="tampered","projection detached from authority");
        Check(d2.BossDefeated("TEST-boss",new V3(0,0,0)),"complete instance");Check(Receive().bossArrivals.Single().hp==0&&d2.BossArrivals.Single().maxHp==120&&!d2.UpdateBossHealth("ward","TEST-boss",10,120),"death preserves max health and cannot be revived by stale updates");d2.Step();Check(d2.Closures.Single(c=>c.id=="final-gate").state=="opening"&&d2.Closures.Single(c=>c.id=="final-gate").Blocking,"final gate starts opening with collision");s2.Tick(1);Check(!d2.Closures.Single(c=>c.id=="final-gate").Blocking,"gate clears collision at one second");
        r2.VoteExit("host",true);r2.VoteExit("guest",true);s2.Tick(.1);Check(d2.Barrier.active&&d2.Barrier.target=="city"&&d2.Barrier.generation==2,"return starts new generation");
        Check(!d2.Ready("guest","ward",1,fingerprint),"late ACK rejected across return");r2.Disconnect("guest");d2.Step();Check(d2.Barrier.required==1,"disconnected peer removed from barrier");
        d2.Ready("host","city",2,fingerprint);Check(d2.CompleteAreaLoad((area,assigned)=>area=="city"&&assigned.Count==1),"return uses town door slots");d2.Step();Check(NativeExpeditionView.Build(r2,"host",s2.Sources).Member("host").safe,"authority sanctuary membership");
        Check(d2.TryBeginFinal(),"final encounter begins after gate and return");
        Check(!d2.RecordBossArrival("final","test",_=>true,new V3(float.NaN,0,0),100,100)&&!d2.RecordBossArrival("final","test",_=>true,new V3(),100,double.PositiveInfinity),"nonfinite spawn and health refused");
        Check(d2.RecordBossArrival("final","test",_=>true,new V3(-3,0,9),100,100),"final boss supplies spawn and health");
        Check(d2.FinalDefeated(new V3(-3,0,9))&&d2.BossArrivals.Single(a=>a.area=="city").hp==0,"final death publishes zero health");
        // Victory and the next town loop share an area: they must still reset and place the party.
        Check(r2.VoteContinue("host",true),"host consents to next loop");s2.Tick(0);
        Check(r2.Snapshot().loop==1&&r2.Snapshot().loading&&d2.Barrier.active&&d2.Barrier.generation==3&&d2.Barrier.target=="city"&&d2.Barrier.ready==0,"same-area next loop starts fresh barrier before projection");
        Check(d2.BossArrivals.Length==0&&d2.Closures.Single(c=>c.id=="final-gate").Blocking,"new loop clears boss arrivals and closes gate immediately");
        Check(!ExpeditionAreaReady.Accept(s2,"host",ticket)&&!d2.CompleteAreaLoad((_,__)=>true),"old-loop ACK cannot release new loop");
        var loopTicket=ExpeditionAreaReady.Capture(NativeExpeditionView.Build(r2,"host",s2.Sources));
        Check(!ExpeditionAreaReady.Accept(s2,"guest",loopTicket),"disconnected peer cannot acknowledge");
        Check(ExpeditionAreaReady.Accept(s2,"host",loopTicket)&&d2.CompleteAreaLoad((area,assigned)=>area=="city"&&assigned.Count==1&&assigned["host"].x==layout.Area("city").slots[0].x&&assigned["host"].z==layout.Area("city").slots[0].z),"new loop places living party at sanctuary slots");
        Console.WriteLine("PASS Expedition director ("+count+" checks)");
    }
}
