using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;
internal static class TransportBacklogChecks
{
    private sealed class PausedStream:Stream
    {
        public readonly TaskCompletionSource<bool> Gate=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<bool> Started=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly ConcurrentQueue<byte[]> Writes=new ConcurrentQueue<byte[]>();
        public override async Task<int> ReadAsync(byte[] b,int o,int n,CancellationToken t){await Task.Delay(Timeout.Infinite,t);return 0;}
        public override async Task WriteAsync(byte[] b,int o,int n,CancellationToken t){Started.TrySetResult(true);await Gate.Task.WaitAsync(t);var copy=new byte[n];Buffer.BlockCopy(b,o,copy,0,n);Writes.Enqueue(copy);}
        public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush(){}public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long p,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    private static void Check(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(string root)
    {
        string runtime=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionSplitRuntime.cs"));
        int start=runtime.IndexOf("private void SplitNetworkStep()",StringComparison.Ordinal);
        int gate=runtime.IndexOf("if(!peer.Loading.Send(NetworkNow,NativeExpeditionLoadWindow.SnapshotSeconds))continue;",start,StringComparison.Ordinal);
        int reports=runtime.IndexOf("type=\"expedition-party-load\"",start,StringComparison.Ordinal);
        Check(gate>start&&reports<0&&runtime.Contains("peer.Connection.SendEncoded(packet,true)"),"four-Hz loading progress uses replaceable snapshots, never a reliable report backlog");
        string network=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeNetwork.cs"));
        string game=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/ConversionGame.cs"));
        string progress=File.ReadAllText(Path.Combine(root,"Assets/ChipArena/Runtime/NativeExpeditionNetwork.cs"));
        Check(network.Contains("c.StartHeartbeat()")&&network.Contains("server.StartHeartbeat()")&&!network.Contains("nextPing"),"both welcomed endpoints enable transport keepalives");
        Check(game.Contains("!inputSendWindow.TrySend()")&&network.Contains("arena.ApplyFrame(f,networkId);inputSendWindow.AuthorityFrame()"),"input credit requires an applied authority frame");
        Check(progress.Contains("NativeExpeditionLoadWindow.ProgressDue(")&&progress.Contains("expeditionNextReady=Time.unscaledTime+(float)NativeExpeditionLoadWindow.RetrySeconds"),"guest load progress and ready retries use bounded cadence");
        RunAsync().GetAwaiter().GetResult();
    }
    private static async Task HostStall(bool bounded)
    {
        using(var listener=new NativeListener(0))using(var guest=await NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None))
        {
            var end=DateTime.UtcNow.AddSeconds(5);while(listener.Accepted.IsEmpty&&DateTime.UtcNow<end)await Task.Delay(5);
            Check(listener.Accepted.TryDequeue(out var host),"host stall connected");using(host)
            {
                var window=new NativeInputSendWindow();Check(!window.TrySend(),"no input credit before first authority frame");window.AuthorityFrame();
                if(bounded){guest.StartHeartbeat();host.StartHeartbeat();guest.StartHeartbeat();host.StartHeartbeat();}
                int inputs=0,reports=0,readies=0;float previous=-1;double last=-1000,nextReady=0;
                // Every guest frame during a 60-second host load. No application receives
                // on either side; only the real socket reader/writer/keepalive tasks run.
                for(int tick=0;tick<(bounded?3600:160);tick++)
                {
                    double now=tick/60d;
                    if(!bounded||window.TrySend()){guest.Send("input-"+inputs++);if(!bounded)await Task.Delay(17);}
                    if(!bounded)continue;
                    float value=Math.Min(1,tick/1800f);
                    if(NativeExpeditionLoadWindow.ProgressDue(value,previous,now-last))
                    {guest.Send("progress-"+reports++);previous=value;last=now;}
                    if(now>=nextReady){guest.Send("ready-"+readies++);nextReady=now+NativeExpeditionLoadWindow.RetrySeconds;}
                }
                if(!bounded)
                {
                    end=DateTime.UtcNow.AddSeconds(3);while(!host.Closed&&DateTime.UtcNow<end)await Task.Delay(5);
                    Check(host.Closed&&host.Error=="Peer exceeded reliable receive budget","60 Hz inputs reproduce a disconnected host receiver in under three seconds");
                    Console.WriteLine("PASS reproduction: 60 Hz inputs overflow an undrained host in under three seconds");return;
                }
                guest.Send("host-load-end");
                end=DateTime.UtcNow.AddSeconds(13);
                while(DateTime.UtcNow<end)
                {
                    // Exercise concurrent metric readers, as the DEV diagnostics do.
                    guest.Metrics.Report(true);host.Metrics.Report(true);await Task.Delay(20);
                }
                Check(!guest.Closed&&!host.Closed,"host loading stays connected beyond the runtime's 12-second timeout");
                Check((DateTime.UtcNow-host.LastReceived).TotalSeconds<3&&(DateTime.UtcNow-guest.LastReceived).TotalSeconds<3,"both stalled game loops still receive keepalives");
                Check(guest.Metrics.Report(true).samples>=10&&host.Metrics.Report(true).samples>=10,"ping replies progress without application receive calls");
                Check(inputs==32&&readies==12&&reports<=56&&host.Received.Count==inputs+reports+readies+1&&guest.Received.IsEmpty,"bounded host traffic, no heartbeat backlog or lost reliable commands");
                int drained=0,ordered=0;while(host.TryReceive(out var text)){if(text.StartsWith("input-",StringComparison.Ordinal))Check(text=="input-"+ordered++,"all sent gameplay input retains order");drained++;}
                Check(drained==inputs+reports+readies+1&&!window.TrySend(),"heartbeat alone cannot replenish input credit");
                host.Send("{\"version\":7,\"epoch\":1,\"type\":\"frame\",\"sequence\":2}",true);
                end=DateTime.UtcNow.AddSeconds(3);string frame=null;while(frame==null&&DateTime.UtcNow<end){guest.TryReceive(out frame);await Task.Delay(5);}
                Check(frame?.Contains("\"sequence\":2")==true,"fresh authority frame delivered on the original connection");window.AuthorityFrame();
                Check(window.TrySend(),"inputs resume after authority recovers");guest.Send("after-load");
                end=DateTime.UtcNow.AddSeconds(3);while(!host.Received.Contains("after-load")&&DateTime.UtcNow<end)await Task.Delay(5);
                Check(host.TryReceive(out var after)&&after=="after-load"&&!host.Closed,"same seat transport resumes without reconnect");
                window.Reset();Check(!window.TrySend(),"disconnect clears unused input credit");
                guest.Dispose();end=DateTime.UtcNow.AddSeconds(3);while(!host.Closed&&DateTime.UtcNow<end)await Task.Delay(5);
                Check(host.Closed,"actual socket loss still closes a heartbeat connection");
            }
        }
        Console.WriteLine("PASS host stall: 60-second input/load traffic, 13 real seconds without game ticks, bounded queues, background heartbeats and same-connection recovery");
    }
    private static async Task ExpeditionLoadingQueue(bool throttle)
    {
        // Simulate an entire 60-second load without the guest consuming any packets.
        // Accelerate the application clock, not the transport: the real framed TCP reader,
        // reliable budget and writer pacing all run unchanged.
        using(var listener=new NativeListener(0))using(var guest=await NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None))
        {
            var end=DateTime.UtcNow.AddSeconds(5);while(listener.Accepted.IsEmpty&&DateTime.UtcNow<end)await Task.Delay(5);
            Check(listener.Accepted.TryDequeue(out var host),"loading loopback connected");using(host)
            {
                var loading=new NativeExpeditionLoadWindow();loading.Begin("instance:1",0);int reports=0;
                for(int tick=0;tick<240;tick++)
                {
                    double now=tick*.25;
                    if(tick%4==0)host.Send("heartbeat-"+tick);
                    if(throttle&&!loading.Send(now))continue;
                    if(!throttle)await Task.Delay(12);
                    // Two chunks cover the largest supported load-report roster (eight pilots).
                    host.Send("party-load-a-"+tick);host.Send("party-load-b-"+tick);reports+=2;
                    host.Send("{\"version\":7,\"epoch\":1,\"type\":\"expedition-state\",\"sequence\":"+tick+"}",true);
                }
                host.Send("load-end-marker");
                end=DateTime.UtcNow.AddSeconds(5);while(!guest.Received.Contains("load-end-marker")&&!guest.Closed&&DateTime.UtcNow<end)await Task.Delay(5);
                if(!throttle)
                {
                    Check(guest.Closed&&guest.Error=="Peer exceeded reliable receive budget","old 4 Hz load-report cadence reproduces disconnect");
                    Console.WriteLine("PASS reproduction: unthrottled loading reports disconnect stalled guest");return;
                }
                Check(!guest.Closed&&!host.Closed&&guest.Received.Contains("load-end-marker"),"full load window survives without a Unity receive tick");
                int drained=0;while(guest.TryReceive(out _))drained++;
                Check(reports==24&&drained>=85&&drained<=86,"bounded reports, all heartbeats/control and at most one state retained");
                // Ready receivers return to normal live report traffic with their queue draining.
                host.Send("ready-control");end=DateTime.UtcNow.AddSeconds(3);bool ready=false;
                while(!ready&&DateTime.UtcNow<end){while(guest.TryReceive(out var packet))ready|=packet=="ready-control";await Task.Delay(5);}
                Check(ready&&!guest.Closed,"same connection continues after instance loading");
            }
        }
        Console.WriteLine("PASS Expedition load: 60-second stalled reader, eight-pilot reports, heartbeats and same-connection recovery");
    }
    private static async Task RunAsync()
    {
        using(var stream=new PausedStream())using(var c=new NativeConnection(stream))
        {
            c.Send("command-0");await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            for(int i=1;i<=100;i++)c.Send("command-"+i);
            c.Send("snapshot-old",true);c.Send("snapshot-new",true);
            Check(!c.Closed,"100 queued commands survive a blocked writer");stream.Gate.SetResult(true);
            var end=DateTime.UtcNow.AddSeconds(5);while(stream.Writes.Count<102&&DateTime.UtcNow<end)await Task.Delay(10);
            Check(stream.Writes.Count==102&&!c.Closed,"backlog fully drained");
            for(int i=0;i<102;i++)
            {
                Check(stream.Writes.TryDequeue(out var wire),"packet exists");
                int size=wire[4]<<24|wire[5]<<16|wire[6]<<8|wire[7];Check(size==wire.Length-8&&wire[0]==67&&wire[3]==NativeConnection.Protocol,"one complete framed packet per write");
                var payload=new byte[size];Buffer.BlockCopy(wire,8,payload,0,size);
                Check(NativeConnection.Decode(payload,NativeConnection.MaxFrame)==(i==101?"snapshot-new":"command-"+i),"ordered commands and newest snapshot");
            }
        }
        using(var stream=new PausedStream())using(var c=new NativeConnection(stream))
        {
            c.Send("blocked");await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            for(int i=0;i<=NativeConnection.MaxReliablePackets;i++)c.Send("command");
            Check(c.Closed&&c.Error.Contains("backlog"),"sustained overload still bounded");
        }
        using(var stream=new PausedStream())using(var c=new NativeConnection(stream))
        {
            c.Send("blocked");await stream.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
            var packet=new byte[NativeConnection.MaxFrame+1];
            for(int i=0;i<4;i++)c.SendEncoded(packet);
            Check(c.Closed,"byte budget rejects oversized backlog before packet budget");
        }
        using(var listener=new NativeListener(0))using(var sender=await NativeConnection.Connect("127.0.0.1",listener.Port,CancellationToken.None))
        {
            var deadline=DateTime.UtcNow.AddSeconds(5);while(listener.Accepted.IsEmpty&&DateTime.UtcNow<deadline)await Task.Delay(5);
            Check(listener.Accepted.TryDequeue(out var receiver),"loopback connected");using(receiver)
            {
                int sent=0,received=0;deadline=DateTime.UtcNow.AddSeconds(8);
                while(received<240&&DateTime.UtcNow<deadline)
                {
                    while(sent<240&&sent-received<100)sender.Send("burst-"+sent++);
                    while(receiver.TryReceive(out var text)){Check(text=="burst-"+received,"loopback FIFO");received++;}
                    Check(!sender.Closed&&!receiver.Closed,"recovery does not trip receive flood guard");await Task.Delay(5);
                }
                Check(received==240,"240 queued commands recovered across rate windows");
            }
        }
        await ExpeditionLoadingQueue(false);
        await ExpeditionLoadingQueue(true);
        Check(NativeExpeditionLoadWindow.ProgressDue(1,.99f,.25)&&!NativeExpeditionLoadWindow.ProgressDue(1,1,.25)&&!NativeExpeditionLoadWindow.ProgressDue(1,1,4.99)&&NativeExpeditionLoadWindow.ProgressDue(1,1,5),"100% progress sends promptly once, then waits for retry cadence");
        await HostStall(false);
        await HostStall(true);
        Console.WriteLine("PASS loopback: 240-command paced recovery without rate-limit disconnect");
        Console.WriteLine("PASS blocked writer: 100 ordered commands, atomic writes, latest snapshot, packet and byte limits");
    }
}
