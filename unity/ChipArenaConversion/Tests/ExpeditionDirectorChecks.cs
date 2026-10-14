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
        Console.WriteLine("PASS Expedition director ("+count+" checks)");
    }
}
