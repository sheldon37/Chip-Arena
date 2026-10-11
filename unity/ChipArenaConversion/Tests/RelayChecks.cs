using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using ChipArena.UnityConversion;
internal static class RelayChecks
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static async Task Until(Func<bool> predicate){var start=DateTime.UtcNow;while(!predicate()){if((DateTime.UtcNow-start).TotalSeconds>5)throw new TimeoutException("Relay did not settle");await Task.Delay(10);}}
    public static async Task Run(string origin,string source)
    {
        using(var health=new HttpClient()){var healthUri=new UriBuilder(origin){Scheme=origin.StartsWith("wss:")?"https":"http",Path="health"};using(var json=JsonDocument.Parse(await health.GetStringAsync(healthUri.Uri)))Check(json.RootElement.GetProperty("service").GetString()=="chip-arena-native-relay"&&json.RootElement.GetProperty("protocol").GetInt32()==NativeConnection.Protocol&&json.RootElement.GetProperty("roomCodeLength").GetInt32()==6,"correct public relay and short-code capability");}
        bool rejected=false;try{NativeWebSocketStream.Endpoint("ws://example.org","native/v3/join");}catch(ArgumentException){rejected=true;}Check(rejected,"insecure remote relay URL rejected");
        using(var host=await NativeRelayHost.Host(origin,source,"Fixture room",true,CancellationToken.None))
        using(var first=await NativeRelayHost.Join(origin,host.Room.ToLowerInvariant(),source,CancellationToken.None))
        using(var second=await NativeRelayHost.Join(origin,host.Room,source,CancellationToken.None))
        {
            Check(host.Room.Length==6&&NativeRoomCodes.Valid(host.Room),"public relay negotiates six-character room codes");
            await Until(()=>host.Accepted.Count==2);Check(host.Accepted.TryDequeue(out var a),"first relay accepted");Check(host.Accepted.TryDequeue(out var b),"second relay accepted");
            using(a)using(b){first.Send("first-pilot");second.Send("second-pilot");await Until(()=>a.Received.Count==1&&b.Received.Count==1);a.TryReceive(out var text);b.TryReceive(out var other);Check(new[]{text,other}.OrderBy(s=>s).SequenceEqual(new[]{"first-pilot","second-pilot"}),"separate relay pipes");
                var sender=text=="first-pilot"?a:b;for(int i=0;i<20;i++)first.Send("ordered-"+i);await Until(()=>sender.Received.Count==20);for(int i=0;i<20;i++)Check(sender.TryReceive(out var message)&&message=="ordered-"+i,"ordered WebSocket byte-stream frames");
                string snapshot="{\"state\":\""+new string('v',130000)+"\"}";sender.Send(snapshot);await Until(()=>first.Received.Count==1);Check(first.TryReceive(out var got)&&got==snapshot,"large relay snapshot");
                using(var http=new HttpClient()){var uri=new UriBuilder(origin){Scheme=origin.StartsWith("wss:")?"https":"http",Path="native/v3/rooms",Query="source="+source};using(var list=JsonDocument.Parse(await http.GetStringAsync(uri.Uri))){Check(list.RootElement.GetProperty("rooms").EnumerateArray().Any(r=>r.GetProperty("code").GetString()==host.Room&&r.GetProperty("players").GetInt32()==3),"room discovery tracks guests");}}
                rejected=false;try{using(var wrong=await NativeRelayHost.Join(origin,host.Room,new string('0',40),CancellationToken.None)){}}catch(WebSocketException){rejected=true;}Check(rejected,"relay rejects different source version");
                first.Dispose();await Until(()=>sender.Closed);Check(!second.Closed,"relay disconnect isolation");host.Dispose();await Until(()=>second.Closed);
            }
        }
        using(var hidden=await NativeRelayHost.Host(origin,source,"Private room",false,CancellationToken.None))using(var http=new HttpClient()){var uri=new UriBuilder(origin){Scheme=origin.StartsWith("wss:")?"https":"http",Path="native/v3/rooms",Query="source="+source};using(var list=JsonDocument.Parse(await http.GetStringAsync(uri.Uri)))Check(!list.RootElement.GetProperty("rooms").EnumerateArray().Any(r=>r.GetProperty("code").GetString()==hidden.Room),"private room is not listed");}
        Console.WriteLine("PASS real WebSocket relay: discovery, private rooms, version guard, two isolated clients, ordered frames, 130 KB snapshot and disconnect cleanup");
    }
}
