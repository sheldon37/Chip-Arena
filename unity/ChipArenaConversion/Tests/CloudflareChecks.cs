using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;
using ChipArena.UnityConversion.Editor;

internal static class CloudflareChecks
{
    private static void Check(bool value,string why){if(!value)throw new InvalidOperationException(why);}
    private static NativeDirectoryEntry Decode(string json)=>JsonSerializer.Deserialize<NativeDirectoryEntry>(json,new JsonSerializerOptions{IncludeFields=true});
    private static async Task Until(Func<bool> predicate)
    {var end=DateTime.UtcNow.AddSeconds(10);while(!predicate()){if(DateTime.UtcNow>end)throw new TimeoutException("Cloudflare transport did not settle");await Task.Delay(10);}}
    private static async Task<T> LiveStep<T>(string name,Func<Task<T>> action)
    {Console.WriteLine("Starting "+name);var result=await action();Console.WriteLine("PASS "+name);return result;}
    public static async Task Run()
    {
        Check(NativeRoomDirectory.Key("7km-r4p")=="UN7KMR4P","short codes map into the existing directory contract");
        Check(NativeRoomDirectory.Key("CAABCD")=="UNCAABCD","CA prefix remains part of a six-character code");
        foreach(string bad in new[]{"http://room.trycloudflare.com","https://room.trycloudflare.com.evil.test","https://evil.test/room.trycloudflare.com","https://user@room.trycloudflare.com","https://room.trycloudflare.com:1234","https://room.trycloudflare.com/path","https://room.trycloudflare.com?x=1","https://room.trycloudflare.com#x","https://127.0.0.1","https://trycloudflare.com","https://nested.room.trycloudflare.com"})
        {bool refused=false;try{NativeRoomDirectory.TunnelOrigin(bad);}catch(NativeOnlineRoomException){refused=true;}Check(refused,"untrusted directory origin rejected: "+bad);}
        var handler=new DirectoryHandler();using(var http=new HttpClient(handler))
        {
            string secret=new string('a',64),origin="https://fixture-room.trycloudflare.com";
            Check(await NativeRoomDirectory.Register(http,"7KMR4P",origin,secret,Decode,CancellationToken.None),"directory lease acquired");
            Check(handler.Authorization=="Bearer "+secret&&handler.Url.EndsWith("/UN7KMR4P")&&handler.Body=="{\"origin\":\""+origin+"\"}","registration matches the existing directory API");
            Check(await NativeRoomDirectory.Lookup(http,"7kmr4p",Decode,CancellationToken.None)==origin,"guest resolves the six-character code");
            Check(handler.Authorization==null&&handler.NoCache,"guest lookup sends no host credentials and bypasses stale cache");
            handler.Status=HttpStatusCode.Conflict;Check(!await NativeRoomDirectory.Register(http,"7KMR4P",origin,secret,Decode,CancellationToken.None),"collision is refused rather than overwriting another host");
            foreach(var status in new[]{HttpStatusCode.NotFound,HttpStatusCode.Redirect,HttpStatusCode.TooManyRequests,HttpStatusCode.ServiceUnavailable})
            {handler.Status=status;await Refused(()=>NativeRoomDirectory.Lookup(http,"7KMR4P",Decode,CancellationToken.None),"missing, redirected or unavailable room does not connect");}
            handler.Status=HttpStatusCode.OK;
            foreach(var body in new[]{"{}","not json",new string('a',4097),"{\"code\":\"UNOTHERX\",\"origin\":\""+origin+"\",\"expires\":1}","{\"code\":\"UN7KMR4P\",\"origin\":\"https://evil.test\",\"expires\":1}","{\"code\":\"UN7KMR4P\",\"origin\":\""+origin+"\",\"expires\":0}"})
            {handler.Reply=body;await Refused(()=>NativeRoomDirectory.Lookup(http,"7KMR4P",Decode,CancellationToken.None),"invalid or oversized directory response rejected");}
            using(var stop=new CancellationTokenSource())
            {
                handler.Hold=true;var pending=NativeRoomDirectory.Lookup(http,"7KMR4P",Decode,stop.Token);await handler.Entered.Task;stop.Cancel();
                bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"leaving cancels an in-flight lookup");
            }
        }
        string file=Path.GetTempFileName();try{File.WriteAllText(file,"untrusted executable");bool refused=false;try{NativeTunnelBinary.Verify(file);}catch(NativeOnlineRoomException){refused=true;}Check(refused,"modified connector cannot execute");}finally{File.Delete(file);}
        Console.WriteLine("PASS Cloudflare directory: short-code namespace, lease collision, strict origins, response bounds, cancellation and connector integrity");
    }
    private static async Task Refused(Func<Task<string>> action,string reason)
    {bool refused=false;try{await action();}catch(NativeOnlineRoomException){refused=true;}Check(refused,reason);}
    private sealed class DirectoryHandler:HttpMessageHandler
    {
        public string Authorization,Url,Body,Reply;public bool NoCache,Hold;public HttpStatusCode Status=HttpStatusCode.OK;
        public readonly TaskCompletionSource<bool> Entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Authorization=request.Headers.Authorization?.ToString();Url=request.RequestUri.AbsoluteUri;
            Body=request.Content==null?null:await request.Content.ReadAsStringAsync();NoCache=request.Headers.CacheControl?.NoCache==true;
            if(Hold){Entered.TrySetResult(true);await Task.Delay(Timeout.Infinite,token);}
            return new HttpResponseMessage(Status){Content=new StringContent(Reply??"{\"code\":\"UN7KMR4P\",\"origin\":\"https://fixture-room.trycloudflare.com\",\"expires\":2000000000000}")};
        }
    }
    public static async Task Live(string project)
    {
        if(Environment.OSVersion.Platform!=PlatformID.Win32NT)throw new InvalidOperationException("Run the shipping connector check on Windows.");
        string player=Path.Combine(Path.GetTempPath(),"Chip Arena tunnel fixture "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(player);bool passed=false;
        try
        {
            NativeTunnelBuild.Stage(project,player);string binary=Path.Combine(player,NativeTunnelBinary.RelativePath);NativeTunnelBinary.Verify(binary);
            Console.WriteLine("PASS Windows player stages the hash-verified pinned connector and license");
            using(var stop=new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            using(var host=await LiveStep("public host registration",()=>NativeCloudflareHost.Start(binary,Decode,stop.Token)))
            using(var first=await LiveStep("first guest code lookup and connection",()=>NativeCloudflareHost.Join(host.Room.ToLowerInvariant(),Decode,stop.Token)))
            using(var second=await LiveStep("second guest code lookup and connection",()=>NativeCloudflareHost.Join(host.Room,Decode,stop.Token)))
            {
                Check(host.Room.Length==6&&host.CodeAvailable,"host publishes a short code after tunnel registration");
                await Until(()=>host.Accepted.Count==2);Check(host.Accepted.TryDequeue(out var a),"first guest arrives at the host PC");Check(host.Accepted.TryDequeue(out var b),"second guest arrives at the host PC");
                using(a)using(b)
                {
                    first.Send("first-pilot");second.Send("second-pilot");await Until(()=>a.Received.Count==1&&b.Received.Count==1);
                    a.TryReceive(out var name);b.TryReceive(out var other);Check(new[]{name,other}.OrderBy(x=>x).SequenceEqual(new[]{"first-pilot","second-pilot"}),"separate guest streams");
                    var sender=name=="first-pilot"?a:b;
                    for(int i=0;i<20;i++)first.Send("ordered-"+i);await Until(()=>sender.Received.Count==20);
                    for(int i=0;i<20;i++)Check(sender.TryReceive(out var message)&&message=="ordered-"+i,"ordered native frames through public Cloudflare");
                    // Low-compressibility payload exercises multiple WebSocket frames, not just a tiny deflate block.
                    var random=new Random(781);string payload=new string(Enumerable.Range(0,130000).Select(_=>(char)random.Next(32,126)).ToArray());
                    sender.Send(payload);await Until(()=>first.Received.Count==1);Check(first.TryReceive(out var got)&&got==payload,"130 KB snapshot reaches the guest intact");
                    using(var http=NativeRoomDirectory.Client())Check(!await NativeRoomDirectory.Register(http,host.Room,host.Origin,new string('b',64),Decode,stop.Token),"live directory protects the owner's lease");
                    first.Dispose();await Until(()=>sender.Closed);Check(!second.Closed,"one guest leaving does not close another");
                    host.Dispose();await Until(()=>second.Closed);Check(host.Closed,"leaving stops the connector and closes remote streams");
                }
            }
            Console.WriteLine("PASS public Cloudflare: real six-character lookup, two native clients, collision refusal, ordered traffic, 130 KB snapshot and host/guest cleanup");passed=true;
        }
        finally
        {
            // A stopped Windows child can briefly retain its executable while the OS finishes closing it.
            Exception cleanup=null;
            for(int i=0;i<40;i++)
            {
                try{Directory.Delete(player,true);cleanup=null;break;}
                catch(IOException e){cleanup=e;}catch(UnauthorizedAccessException e){cleanup=e;}
                await Task.Delay(100);
            }
            if(cleanup!=null)
            {
                if(passed)throw new IOException("The stopped connector still locks its Windows package.",cleanup);
                Console.Error.WriteLine("Connector fixture cleanup was delayed: "+cleanup.GetType().Name); // Preserve the original connection error.
            }
        }
    }
}
