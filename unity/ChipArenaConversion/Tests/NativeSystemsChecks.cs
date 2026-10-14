using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;
internal static class NativeSystemsChecks
{
    private static readonly JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true};
    private static void Check(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static void Near(double a,double b,string message,double tolerance=.0001){Check(Math.Abs(a-b)<=tolerance,message+": "+a+" != "+b);}
    private static T Decode<T>(JsonElement e)=>JsonSerializer.Deserialize<T>(e.GetRawText(),Options);
    public static void Run(string root,ConversionContent content)
    {
        foreach(float speed in new[]{10f,80f,200f})foreach(int part in new[]{CapturedRig.Head,CapturedRig.Spine,CapturedRig.LArm})foreach(bool lethal in new[]{false,true})
        {
            var wound=NativeBodyDamage.Resolve(NativeBodyDamage.Projectile("bow",speed,.045f),part,39,lethal);
            Check(wound.trauma=="pierce"&&(wound.kind=="puncture"||wound.kind=="cavity")&&!wound.destroyed&&NativeBodyDamage.Valid(wound),"arrows puncture/cavitate without burning or bullet severing");
        }
        Check(NativeBodyDamage.Projectile("pistol",120,.045f).kind=="bullet"&&NativeBodyDamage.Projectile("fire",40,.1f).kind=="energy","gun and magic wound kinds preserved");
        var metadataHit=new ArenaEvent{Kind=EventKind.Headshot,Projectile=42,Pierced=true,From=new V3(1,2,3)};
        var hitCopy=JsonSerializer.Deserialize<ArenaEvent>(JsonSerializer.Serialize(metadataHit,new JsonSerializerOptions{IncludeFields=true,IgnoreReadOnlyProperties=true}),Options);
        Check(hitCopy.Projectile==42&&hitCopy.Pierced&&hitCopy.From.x==1,"hit metadata JSON round trip");
        var oldHit=JsonSerializer.Deserialize<ArenaEvent>("{\"Kind\":0,\"Projectile\":7}",Options);
        Check(!oldHit.Pierced&&oldHit.Projectile==7,"old frames default to no pass-through claim");
        var push=new V3(0,5,9);var carried=NativeMeleeRules.External(ref push,1f/60,out _,true);Near(carried.z,9,"wave push is not dragged during dash");Near(carried.y,5,"wave preserves one-shot lift");Near(push.y,0,"lift consumed once");
        var dragged=NativeMeleeRules.External(ref push,1f/60,out _);Near(dragged.z,9*Math.Exp(-3.1/60),"post-wave drag");Near(dragged.y,0,"no repeated lift");
        Check(NativeIntroRules.Style(new NativeSeat{spec="siren"})==NativeIntroRules.Default,"ungranted Siren standard intro");Check(NativeIntroRules.Style(new NativeSeat{spec="siren",AuthorityBoss=true})=="dev-siren","boss intro preserved");Check(NativeIntroRules.Style(new NativeSeat{spec="rifle"})=="weapon-rifle","weapon intro preserved");Check(NativeIntroRules.Style(new NativeSeat{spec="siren",overrideIntro=true,intro="missing-style"})==NativeIntroRules.Default,"unknown override normalized");Check(NativeIntroRules.Style(new NativeSeat{spec="pistol",overrideIntro=true,intro="neon-drop"})=="weapon-pistol","retired override plays the signature");
        var pve=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),Options);
        using(var file=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/pve-parity.json"))))
        {
            foreach(var e in file.RootElement.GetProperty("cases").EnumerateArray())
            {int h=e.GetProperty("h").GetInt32(),wave=e.GetProperty("wave").GetInt32();var data=pve.waves[wave-1];var flow=e.GetProperty("flow");Near(NativePveRules.BossHp(h),e.GetProperty("hp").GetDouble(),"party HP");Near(NativePveRules.BossDamage(h),e.GetProperty("damage").GetDouble(),"party damage");Near(NativePveRules.Budget(data,h),e.GetProperty("budget").GetDouble(),"wave budget");Near(NativePveRules.Rate(data,h),flow.GetProperty("rate").GetDouble(),"spawn flow");Near(NativePveRules.Cap(data,h,e.GetProperty("relay").GetBoolean()),flow.GetProperty("maxAlive").GetDouble(),"alive cap");}

        }
        Console.WriteLine("PASS native PvE: 24 source scaling cases; native fixed-count schedules checked separately");
        using(var file=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/native-systems-parity.json"))))
        {
            var d=file.RootElement;
            foreach(var f in d.GetProperty("climbs").EnumerateArray()){var at=NativeEntryRules.Climb(f.GetProperty("time").GetSingle(),f.GetProperty("activeAt").GetSingle());Near(at.y,f.GetProperty("y").GetDouble(),"climb Y");Near(at.z,f.GetProperty("z").GetDouble(),"climb Z");}
            foreach(var f in d.GetProperty("timelines").EnumerateArray()){var t=NativeEntryRules.Timeline(f.GetProperty("kind").GetString(),f.GetProperty("first").GetBoolean());Near(t.walk,f.GetProperty("walk").GetDouble(),"entry walk");Near(t.duration,f.GetProperty("active").GetDouble(),"entry duration");}
            foreach(var f in d.GetProperty("weights").EnumerateArray())Near(NativeEntryRules.Weight(f.GetProperty("distance").GetSingle(),f.GetProperty("kind").GetString(),f.GetProperty("wave").GetBoolean(),f.GetProperty("possession").GetBoolean()),f.GetProperty("value").GetDouble(),"entry weight");
            var vent=new NativeEntryView{kind="vent",mode="spawn",openAt=1,closeAt=5};Near(NativeVentRules.Open(vent,1.35f),0,"vent push");Near(NativeVentRules.Open(vent,1.8f),1,"vent opened");Near(NativeVentRules.Open(vent,5.4f),0,"vent closed");Check(NativeVentRules.Falls(new V3(0,.9f,0),new V3(),1),"open vent falls");Check(!NativeVentRules.Falls(new V3(0,2,0),new V3(),1),"jump clears vent");
            Console.WriteLine("PASS entrances: 241 original climb samples, six timelines, 96 spawn weights and vent boundaries");
            var bones=new Dictionary<string,int>{{"head",5},{"spine",2},{"left-shoulder",7},{"right-shoulder",11},{"left-elbow",8},{"right-elbow",12},{"left-hip",14},{"right-hip",18},{"left-knee",15},{"right-knee",19}};
            foreach(var f in d.GetProperty("wounds").EnumerateArray())
            {var input=f.GetProperty("input");var expected=f.GetProperty("expected");bool head=input.GetProperty("headshot").GetBoolean();var point=Decode<V3>(input.GetProperty("point"));int part=NativeBodyDamage.Part(point,0,head);Check(part==bones[expected.GetProperty("part").GetString()],"body part");var wound=NativeBodyDamage.Resolve(Decode<NativeBodyImpact>(input.GetProperty("impact")),part,60,input.GetProperty("lethal").GetBoolean(),head);Check(wound.kind==expected.GetProperty("kind").GetString(),"wound kind");Near(wound.energy,expected.GetProperty("energy").GetDouble(),"wound energy",.002);Near(wound.severity,expected.GetProperty("severity").GetDouble(),"wound severity");Near(wound.radius,expected.GetProperty("radius").GetDouble(),"wound radius");}
            var limbs=new Dictionary<string,int>{{"torso",0},{"head",1},{"lShoulder",2},{"rShoulder",4},{"lElbow",8},{"rElbow",16},{"lHip",32},{"rHip",64},{"lKnee",128},{"rKnee",256}};
            foreach(var f in d.GetProperty("shares").EnumerateArray())
            {var state=f.GetProperty("state");var hit=f.GetProperty("hit");var rng=new NativePveRandom(f.GetProperty("seed").GetUInt32());var shares=NativeHordeDamage.Shares(50,Decode<V3>(state.GetProperty("pos")),(float)(state.GetProperty("yaw").GetDouble()*180/Math.PI),state.GetProperty("crawl").GetBoolean(),Decode<V3>(hit.GetProperty("point")),hit.GetProperty("kind").GetString(),3,false,rng.Next);var expected=f.GetProperty("expected").EnumerateArray().ToArray();Check(shares.Count==expected.Length,"blast share count");for(int i=0;i<shares.Count;i++){Check(shares[i].part==limbs[expected[i].GetProperty("part").GetString()],"blast share limb");Near(shares[i].damage,expected[i].GetProperty("damage").GetDouble(),"blast share damage");}}
            foreach(var f in d.GetProperty("random").EnumerateArray())
            {uint seed=f.GetProperty("seed").GetUInt32();Check(NativePveRandom.Hash(seed,"horde")==f.GetProperty("hash").GetUInt32(),"source seed hash");var random=new NativePveRandom(seed);foreach(var expected in f.GetProperty("values").EnumerateArray())Near(random.Next(),expected.GetDouble(),"source PRNG",0);}
            foreach(var f in d.GetProperty("music").EnumerateArray())
            {double at=pve.bossMusic.Looped(f.GetProperty("raw").GetDouble());Near(at,f.GetProperty("looped").GetDouble(),"music loop");Near(pve.bossMusic.Beat(at),f.GetProperty("beat").GetDouble(),"music beat");Check(pve.bossMusic.Section(at)==f.GetProperty("section").GetString(),"music section");}
        }
        Console.WriteLine("PASS original-engine fixtures: 72 wounds, 36 horde hit splits, 80 PRNG values and 1,001 music positions");
        var ledger=new NativeImpactLedger();Near(NativeDebrisRules.Damage(10,10),15.925,"debris energy");Near(NativeDebrisRules.Damage(100,3),0,"debris low-speed guard");Near(NativeDebrisRules.Damage(100,100),65,"debris cap");ledger.Begin();Near(ledger.Claim("a","box",0,65),65,"first impact");Near(ledger.Claim("b","box",0,65),10,"shared fragment cap");ledger.End(0);ledger.Begin();Near(ledger.Claim("a","box",.5,65),0,"resting contact cannot retrigger");ledger.End(.5);ledger.Begin();ledger.End(.6);ledger.Begin();Near(ledger.Claim("a","box",1,65),65,"separated contact returns");ledger.End(1);
        var stats=NativeContentRules.Stats(content,"pistol",new Dictionary<string,int>());stats["critical"]=.5;stats["execute"]=.4;Near(NativeProjectileRules.Damage(10,stats,1,100,10,false,0,.5f,.1f),28,"execute and critical");Near(NativeProjectileRules.Falloff(20,3),0,"outside blast");Check(NativeAccess.Verify("invalid",_=>null,1)==NativeAccess.Guest,"invalid grant denied");
        string relay=Environment.GetEnvironmentVariable("CHIP_TEST_RELAY");if(!string.IsNullOrEmpty(relay))RelayChecks.Run(relay,content.sourceCommit).GetAwaiter().GetResult();
        MainThreadIndependentTransport();LanCodeDiscovery();Transport().GetAwaiter().GetResult();Console.WriteLine("PASS transport: strict UTF-8, decompression limit, ordered loopback messages, isolated clients, disconnect and reconnect");
    }
    private static async Task Until(Func<bool> ready)
    {var start=DateTime.UtcNow;while(!ready()){if((DateTime.UtcNow-start).TotalSeconds>4)throw new TimeoutException("Loopback transport did not settle");await Task.Delay(5);}}
    private static void LanCodeDiscovery()
    {
        using(var listener=new NativeListener(0))using(var discovery=new NativeLanDiscovery("ABCDEFGH",new string('a',40),listener.Port))using(var cancel=new CancellationTokenSource(5000))
        using(var guest=NativeLanDiscovery.Connect("ABCDEFGH",new string('a',40),cancel.Token).GetAwaiter().GetResult())
        {Check(SpinWait.SpinUntil(()=>listener.Accepted.Count>0,2000),"code-only LAN join reaches host");listener.Accepted.TryDequeue(out var host);using(host){guest.Send("{\"test\":1}");Check(SpinWait.SpinUntil(()=>host.Received.Count>0,2000),"discovered connection carries framed messages");}}
        Console.WriteLine("PASS real UDP code discovery plus TCP framed transport on loopback");
    }
    private sealed class PausedUnityContext:SynchronizationContext
    {public int Posts;public override void Post(SendOrPostCallback callback,object state){Interlocked.Increment(ref Posts);}}
    private static void MainThreadIndependentTransport()
    {
        var previous=SynchronizationContext.Current;var context=new PausedUnityContext();
        SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            using(var listener=new NativeListener(0))
            {
                var task=NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None);
                Check(task.Wait(4000),"connect must not wait for Unity Update");
                using(var client=task.Result)
                {
                    var deadline=DateTime.UtcNow.AddSeconds(4);
                    while(listener.Accepted.IsEmpty&&DateTime.UtcNow<deadline)Thread.Sleep(5);
                    Check(listener.Accepted.TryDequeue(out var host),"accept while Unity context is paused");
                    using(host)
                    {
                        for(int i=0;i<100;i++){client.Send("input-"+i);Thread.Sleep(12);}
                        while(host.Received.Count<100&&DateTime.UtcNow<deadline)Thread.Sleep(5);
                        Check(!client.Closed&&!host.Closed&&host.Received.Count==100,"100 reliable inputs progress without rendering");
                        for(int i=0;i<100;i++)Check(host.TryReceive(out var text)&&text=="input-"+i,"input order under stalled renderer");
                        Check(context.Posts==0,"transport does not capture main-thread context");
                    }
                }
            }
        }
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
        Console.WriteLine("PASS transport with a paused Unity synchronization context (100 inputs, no render callbacks)");
    }
    private static async Task Transport()
    {
        string payload="{\"name\":\"Pilot 🌠\",\"data\":\""+new string('z',64000)+"\"}";Check(NativeConnection.Decode(NativeConnection.Encode(payload),100000)==payload,"compression round trip");
        bool rejected=false;try{NativeConnection.Decode(NativeConnection.Encode(payload),100);}catch(InvalidDataException){rejected=true;}Check(rejected,"compression expansion cap");rejected=false;try{NativeConnection.Decode(new byte[]{0,255,255},100);}catch(ArgumentException){rejected=true;}Check(rejected,"invalid UTF8 denied");
        using(var listener=new NativeListener(0))using(var first=await NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None))using(var second=await NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None))
        {
            await Until(()=>listener.Accepted.Count==2);Check(listener.Accepted.TryDequeue(out var a),"first accepted");Check(listener.Accepted.TryDequeue(out var b),"second accepted");
            using(a)using(b)
            {
                for(int i=0;i<20;i++)first.Send("message-"+i);second.Send("second-client");await Until(()=>a.Received.Count==20&&b.Received.Count==1);
                for(int i=0;i<20;i++){Check(a.TryReceive(out var text)&&text=="message-"+i,"ordered reliable delivery");}Check(b.TryReceive(out var other)&&other=="second-client","isolated client");
                a.Send(payload);await Until(()=>first.Received.Count==1);Check(first.TryReceive(out var received)&&received==payload,"large compressed snapshot");
                first.Dispose();await Until(()=>a.Closed);Check(!b.Closed,"disconnect isolation");
                using(var resumed=await NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None)){await Until(()=>listener.Accepted.Count==1);Check(listener.Accepted.TryDequeue(out var remote),"reconnect accepted");using(remote){resumed.Send("resume-seat");await Until(()=>remote.Received.Count==1);Check(remote.TryReceive(out var msg)&&msg=="resume-seat","reconnect delivery");}}
                using(var invalid=new TcpClient()){await invalid.ConnectAsync("127.0.0.1",listener.Port);await Until(()=>listener.Accepted.Count==1);Check(listener.Accepted.TryDequeue(out var rejectedPeer),"invalid peer accepted");using(rejectedPeer){byte[] bad={0,0,0,0,0,0,0,2};await invalid.GetStream().WriteAsync(bad,0,bad.Length);await Until(()=>rejectedPeer.Closed);Check(rejectedPeer.Error!=null,"bad magic rejected");}}
            }
        }
    }
}
