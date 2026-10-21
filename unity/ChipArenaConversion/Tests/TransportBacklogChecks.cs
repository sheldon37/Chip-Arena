using System;
using System.IO;
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
    public static void Run()=>RunAsync().GetAwaiter().GetResult();
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
        Console.WriteLine("PASS loopback: 240-command paced recovery without rate-limit disconnect");
        Console.WriteLine("PASS blocked writer: 100 ordered commands, atomic writes, latest snapshot, packet and byte limits");
    }
}
