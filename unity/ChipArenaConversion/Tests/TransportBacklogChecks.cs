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
        int gate=runtime.IndexOf("if(!peer.Loading.Send(NetworkNow))continue;",start,StringComparison.Ordinal);
        int reports=runtime.IndexOf("type=\"expedition-party-load\"",start,StringComparison.Ordinal);
        Check(gate>start&&reports>gate,"loading cadence gates reliable reports as well as snapshots");
        RunAsync().GetAwaiter().GetResult();
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
        Console.WriteLine("PASS loopback: 240-command paced recovery without rate-limit disconnect");
        Console.WriteLine("PASS blocked writer: 100 ordered commands, atomic writes, latest snapshot, packet and byte limits");
    }
}
