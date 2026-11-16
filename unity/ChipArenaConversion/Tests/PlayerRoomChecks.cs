using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;

internal static class PlayerRoomChecks
{
    private sealed class Endpoint : IOrderedRoomSocket
    {
        public Endpoint Peer;
        public int Backpressure, Fragments;
        public volatile bool Closed;
        public readonly ConcurrentQueue<byte[]> Inbox=new ConcurrentQueue<byte[]>();
        public byte[] TryRead()
        {
            if(Inbox.TryDequeue(out var data))return data;
            if(Closed||Peer.Closed)throw new IOException("Carrier closed");
            return null;
        }
        public bool TryWrite(byte[] b,int offset,int count)
        {
            if(Closed||Peer.Closed)throw new IOException("Carrier closed");
            if(Backpressure>0){Backpressure--;return false;}
            if(count>OrderedRoomStream.ChunkSize)throw new Exception("Oversize carrier message");
            var copy=new byte[count];Buffer.BlockCopy(b,offset,copy,0,count);
            Peer.Inbox.Enqueue(copy);Fragments++;return true;
        }
        public void Dispose()=>Closed=true;
    }
    private static void Check(bool ok,string label){if(!ok)throw new Exception(label);}
    private static async Task<string> Read(NativeConnection conn,CancellationToken token)
    {
        while(!conn.Closed){if(conn.TryReceive(out var msg))return msg;await Task.Delay(2,token);}
        throw new Exception("Premature close: "+conn.Error);
    }
    public static async Task Run()
    {
        Check(PlayerRoomInfo.LobbyNumber("18446744073709551615",out _),"unsigned lobby ID");
        foreach(var s in new[]{"01","-1"," 42","0","18446744073709551616","https://example.com"})
            Check(!PlayerRoomInfo.LobbyNumber(s,out _),"reject malformed room ID");
        Check(PlayerRoomInfo.Compatibility("x","a")!=PlayerRoomInfo.Compatibility("x","b"),"mod isolation");
        Check(PlayerRoomInfo.Label("<b>\r\nlong name",5)=="blong","safe bounded label");
        Check(PlayerRoomInfo.ModeName(false,false,true,true)=="Team Deathmatch","mode");
        var a=new Endpoint{Backpressure=2};var b=new Endpoint();a.Peer=b;b.Peer=a;
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var host=new NativeConnection(new OrderedRoomStream(a),32768,2);
        using var guest=new NativeConnection(new OrderedRoomStream(b),NativeConnection.MaxFrame,1);
        guest.Send("{\"type\":\"hello\"}");Check(await Read(host,stop.Token)=="{\"type\":\"hello\"}","guest admission frame");
        var rand=new Random(302);
        var data=new string(Enumerable.Range(0,780000).Select(_=>(char)rand.Next(32,126)).ToArray());
        host.Send(data);host.Send("control after large frame");
        Check(await Read(guest,stop.Token)==data && a.Fragments>10,"large incompressible frame over multiple Steam-sized messages");
        Check(await Read(guest,stop.Token)=="control after large frame","reliable order across backpressure");
        host.StartHeartbeat();guest.StartHeartbeat();
        for(int i=0;i<12;i++)guest.Send("command-"+i);
        for(int i=0;i<12;i++)Check(await Read(host,stop.Token)=="command-"+i,"ordered guest controls");
        guest.Send(new string('x',40000));while(!host.Closed)await Task.Delay(5,stop.Token);
        Check(host.Error.Contains("too large"),"host keeps the 32 KiB expanded limit");
        while(!guest.Closed)await Task.Delay(5,stop.Token);
        Check(a.Closed&&b.Closed,"disposal releases both endpoints");
        Console.WriteLine("PASS player-room metadata, backpressure, large-frame fragmentation, ordering, limits and close");
    }
}
