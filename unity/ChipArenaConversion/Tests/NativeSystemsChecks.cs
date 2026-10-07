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
        var pve=JsonSerializer.Deserialize<NativePveContent>(File.ReadAllText(Path.Combine(root,"Assets/Conversion/Resources/PveContent.json")),Options);
        using(var file=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/pve-parity.json"))))
        {
            foreach(var e in file.RootElement.GetProperty("cases").EnumerateArray())
            {int h=e.GetProperty("h").GetInt32(),wave=e.GetProperty("wave").GetInt32();var data=pve.waves[wave-1];var flow=e.GetProperty("flow");Near(NativePveRules.BossHp(h),e.GetProperty("hp").GetDouble(),"party HP");Near(NativePveRules.BossDamage(h),e.GetProperty("damage").GetDouble(),"party damage");Near(NativePveRules.Budget(data,h),e.GetProperty("budget").GetDouble(),"wave budget");Near(NativePveRules.Rate(data,h),flow.GetProperty("rate").GetDouble(),"spawn flow");Near(NativePveRules.Cap(data,h,e.GetProperty("relay").GetBoolean()),flow.GetProperty("maxAlive").GetDouble(),"alive cap");}
            foreach(var e in file.RootElement.GetProperty("schedules").EnumerateArray())
            {var schedule=new NativeWaveSchedule(0,e.GetProperty("skip").GetBoolean());double resolved=e.GetProperty("resolvedAt").GetDouble();var events=new List<PveWaveEvent>();for(double t=0;t<550;t+=.25){var change=schedule.Step(t,t%33<30?0:5,resolved<0?(double?)null:resolved);if(change!=null)events.Add(change);}var expected=e.GetProperty("events").EnumerateArray().ToArray();Check(events.Count==expected.Length,"schedule count");for(int i=0;i<events.Count;i++){Check(events[i].kind==expected[i].GetProperty("kind").GetString(),"wave event");Near(events[i].at,expected[i].GetProperty("at").GetDouble(),"wave time");}}
        }
        Console.WriteLine("PASS native PvE: 24 source scaling cases and six complete wave schedules");
        using(var file=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Tests/native-systems-parity.json"))))
        {
            var d=file.RootElement;
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
        Transport().GetAwaiter().GetResult();Console.WriteLine("PASS transport: strict UTF-8, decompression limit, ordered loopback messages, isolated clients, disconnect and reconnect");
    }
    private static async Task Until(Func<bool> ready)
    {var start=DateTime.UtcNow;while(!ready()){if((DateTime.UtcNow-start).TotalSeconds>4)throw new TimeoutException("Loopback transport did not settle");await Task.Delay(5);}}
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
