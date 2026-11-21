using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using ChipArena.UnityConversion;

internal static class PodRunChecks
{
    private static int count;
    private static readonly JsonSerializerOptions Codec=new JsonSerializerOptions{IncludeFields=true};
    private static void Check(bool ok,string why){count++;if(!ok)throw new Exception("Pod rules: "+why);}
    private static T Copy<T>(T x)=>JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(x,Codec),Codec);
    private static NativePodRecord Record(int id=1)=>new NativePodRecord{seat=id,order=id-1,name="Pilot",bodyColor="#123456",accentColor="#abcdef",look="paint:aabbcc",x=(id-1)*2};
    private static NativeMatch Match(ConversionContent data)=>new NativeMatch(data){Cooperative=true,HoldResolution=()=>true};
    public static void Run(string root)
    {
        var data=JsonSerializer.Deserialize<ConversionContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/GameContent.json")),Codec);
        var duel=new NativeMatch(data);
        duel.Seats.Add(new NativeSeat{id=1,spec="pistol"});duel.Seats.Add(new NativeSeat{id=2,spec="pistol"});
        Check(duel.Start()&&duel.PhaseEnd-duel.Time==3,"PvP opening stays three seconds");
        var m=Match(data);var source=new[]{Record(),Record(2)};var pods=new NativePodRun(source);source[0].x=999;
        Check(!pods.Started&&pods.Snapshot().Length==0,"loading has no active drop");
        Check(pods.Start(m)&&m.Phase==NativePhase.Countdown&&m.PhaseEnd==5&&m.FightAt==5,"first ready hub starts five second countdown");
        var guest=Match(data);guest.ApplyNetwork(Copy(new NativeMatchFrame{phase=m.Phase,time=m.Time,phaseEnd=m.PhaseEnd,fightAt=m.FightAt,cooperative=true}));
        Check(guest.Phase==NativePhase.Countdown&&guest.PhaseEnd-guest.Time==5&&guest.FightAt==guest.PhaseEnd,"replicated clock drives guest drop");
        Check(!pods.Board(1,Vector3.Zero,4.99,true),"boarding blocked before fight");
        for(int i=0;i<20;i++)m.Tick(.25);
        Check(m.Phase==NativePhase.Fight&&m.FightAt==5,"controls unlock after countdown");
        Check(!pods.Start(m)&&pods.Snapshot()[0].end==5,"area return or reconnect cannot start another countdown");
        Check(!pods.Board(2,Vector3.Zero,5,true)&&!pods.Board(77,Vector3.Zero,5,true),"cannot use another pilot's pod or unknown seat");
        Check(!pods.Board(1,new Vector3(0,0,-1),5,true)&&!pods.Board(1,new Vector3(0,2,0),5,true)&&!pods.Board(1,new Vector3(0,0,1.51f),5,true),"door side, height and radius enforced");
        Check(!pods.Board(1,Vector3.Zero,5,false)&&!pods.Board(1,new Vector3(float.NaN,0,0),5,true),"authority eligibility and finite position required");
        Check(pods.Board(1,new Vector3(0,0,1.5f),5,true)&&pods.Departing(1),"own door accepts interaction at boundary");
        var wire=Copy(pods.Snapshot());Check(NativePodRun.Valid(wire,5)&&wire[0].launchAt==5&&wire[0].look=="paint:aabbcc"&&wire[0].x==0,"late join preserves fixed anchors, look and launch timestamp");
        wire[0].x=999;Check(pods.Snapshot()[0].x==0,"snapshots cannot mutate authority");
        int extracted=0;pods.Tick(8.39,_=>true,_=>{extracted++;return true;});Check(extracted==0,"pilot remains until launch gone");
        pods.Tick(8.4,_=>true,_=>{extracted++;return true;});pods.Tick(10,_=>true,_=>{extracted++;return true;});
        Check(extracted==1&&pods.Snapshot()[0].departed&&!pods.Departing(1)&&NativePodRun.Valid(Copy(pods.Snapshot()),10),"removal exactly once; late joins retain departure tombstone");
        Check(pods.Board(2,new Vector3(2,0,0),10,true),"second pilot boards independently");
        pods.Tick(10.2,_=>false,_=>throw new Exception("ineligible extraction"));Check(!pods.Departing(2)&&pods.Snapshot()[1].launchAt==-1,"lost eligibility cancels launch");
        pods.Board(2,new Vector3(2,0,0),11,true);pods.Tick(15,_=>true,_=>false);Check(!pods.Departing(2)&&!pods.Snapshot()[1].departed,"failed extraction restores parked pod");
        pods.Board(2,new Vector3(2,0,0),16,true);pods.CancelDepartures();Check(!pods.Departing(2)&&!pods.Start(m)&&pods.Snapshot()[0].departed&&pods.Snapshot()[1].end==5,"repeat loop cancels in-flight departure without replaying landing or resurrecting extracted pilots");
        wire=pods.Snapshot();wire[1].seat=1;Check(!NativePodRun.Valid(wire,20),"duplicate seat wire rejected");
        wire=pods.Snapshot();wire[1].launchAt=30;Check(!NativePodRun.Valid(wire,20),"future launch rejected");
        Check(NativePodRun.Look("light:ABCDEF,paint:112233,unknown:fff,trim:010203,paint:ZZZZZZ")=="paint:112233,trim:010203,light:abcdef"&&NativePodRun.Look(new string('x',257))=="","look normalized and bounded");
        foreach(int yaw in new[]{0,90,225})
        {
            var door=Record();door.yaw=yaw;double angle=yaw*Math.PI/180;
            var forward=new Vector3((float)Math.Sin(angle),0,(float)Math.Cos(angle));
            Check(NativePodRun.InReach(door,forward)&&!NativePodRun.InReach(door,-forward),"door yaw in degrees "+yaw);
        }
        Economy(data);
        Console.WriteLine("PASS "+count+" pod rules checks");
    }
    private static void Economy(ConversionContent data)
    {
        var run=NativeExpedition.Create("pod-rule","city","host","pistol",42,new[]{new ExpeditionInstance{id="ward",target=1}});
        run.Join("guest","pistol");Check(!run.CanExtract("guest"),"uncleared run cannot extract");
        run.EnableAreas();run.MoveMembers("city","ward",new[]{"guest"});var ward=run.ForArea("ward");ward.Progress("quota",1);ward.StartBosses(new[]{"boss"});ward.DefeatBoss("boss");run.MoveMembers("ward","city",new[]{"guest"});run.SetSafe("host",true);run.SetSafe("guest",true);
        run.Credit("guest","pod-cash",50);var session=new ExpeditionSession(run,data);
        Check(!run.Extract("host"),"host cannot abandon connected guest");
        var pods=new NativePodRun(new[]{Record(2)});var m=Match(data);pods.Start(m);pods.Board(2,new Vector3(2,0,0),5,run.CanExtract("guest"));
        var before=JsonSerializer.Serialize(run.Snapshot(),Codec);pods.Tick(8,_=>run.CanExtract("guest"),_=>session.Extract("guest").state==ExpeditionCommandState.Pending);
        Check(before==JsonSerializer.Serialize(run.Snapshot(),Codec),"boarding delay does not commit economy or ledger");
        pods.Tick(8.4,_=>run.CanExtract("guest"),_=>session.Extract("guest").state==ExpeditionCommandState.Pending);
        Check(run.PartySnapshot().members.Single(x=>x.account=="guest").extracted&&pods.Snapshot()[0].departed,"pod delegates extraction to existing split session");
        var after=JsonSerializer.Serialize(run.Snapshot(),Codec);pods.Tick(20,_=>true,_=>session.Extract("guest").state==ExpeditionCommandState.Pending);
        Check(after==JsonSerializer.Serialize(run.Snapshot(),Codec),"completion cannot duplicate XP or wallet ledger");
        Check(session.Extract("host").state==ExpeditionCommandState.Pending,"host may leave once guest has extracted");
    }
}
