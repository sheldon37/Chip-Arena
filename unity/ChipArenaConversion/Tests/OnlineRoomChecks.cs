using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;
internal static class OnlineRoomChecks
{
    private static void Check(bool pass,string why){if(!pass)throw new InvalidOperationException(why);}
    public static void Run()
    {
        foreach(var input in new[]{"7KMR4P","7kmr4p"," 7km r4p ","7KM-R4P","CA-7KMR4P"})Check(NativeRoomCodes.RawOnline(input)=="7KMR4P","short room codes accept lowercase, spacing and separators");
        Check(NativeRoomCodes.RawOnline("ca-abcd-2345")=="ABCD2345"&&NativeRoomCodes.Online("ABCDEFGH")=="ABCDEFGH","legacy codes stay pasteable without an added prefix");
        Check(NativeRoomCodes.RawOnline("CA-ABCD")=="CAABCD","short code beginning CA is not mistaken for a legacy prefix");
        foreach(var invalid in new[]{null,"","ABC12","123456","ABC0EF","ABCDEF7","ABCDEF!","AБCDEF",new string('A',65)})Check(!NativeRoomCodes.TryNormalize(invalid,out _),"invalid code rejected before connecting");
        for(int i=0;i<1024;i++){string code=NativeRoomCodes.Create();Check(code.Length==6&&NativeRoomCodes.Valid(code)&&code.All(c=>!"01IO".Contains(c)),"six unambiguous cryptographically sampled characters");}
        var occupied=new HashSet<string>{"ABC234"};var candidates=new Queue<string>(new[]{"ABC234","ABC234","DEF567"});
        Check(NativeRoomCodes.TryReserve(occupied.Add,()=>candidates.Dequeue())&&occupied.SetEquals(new[]{"ABC234","DEF567"}),"collisions retry and never overwrite an existing room");
        int attempts=0;Check(!NativeRoomCodes.TryReserve(_=>false,()=>{attempts++;return "ABC234";})&&attempts==32,"collision retries are bounded");
        string invite=NativeRoomCodes.Invite("7kmr4p");Check(NativeRoomCodes.ParseInvite(invite,out var parsed)&&parsed=="7KMR4P","online invite round trips without exposing a relay address");
        foreach(var bad in new[]{"https://example.org/?code=7KMR4P","chiparena://room?code=7KMR4P&origin=evil","chiparena://user@room?code=7KMR4P","chiparena://room/path?code=7KMR4P","chiparena://room?code=7KMR4P#evil"})Check(!NativeRoomCodes.ParseInvite(bad,out _),"invite cannot select an arbitrary endpoint");
        Check(NativeLanDiscovery.ParseInvite(NativeLanDiscovery.Invite("192.168.1.4",29941,"7KMR4P"),out _,out _,out var local)&&local=="7KMR4P","short LAN invite accepted");
        Service().GetAwaiter().GetResult();
        Console.WriteLine("PASS simple online rooms: 6-character codes, legacy paste, collision retries, LAN/online invites, secure service discovery and cancellation");
    }
    private static NativeOnlineConfiguration Decode(string json)=>JsonSerializer.Deserialize<NativeOnlineConfiguration>(json,new JsonSerializerOptions{IncludeFields=true});
    private static async Task Service()
    {
        var handler=new ResponseHandler();using(var http=new HttpClient(handler))
        {
            handler.Body="{\"schema\":1,\"protocol\":6,\"relayOrigin\":\"wss://relay.example.org\"}";
            Check(await NativeOnlineService.Resolve(http,Decode,CancellationToken.None)=="wss://relay.example.org"&&handler.Url==NativeOnlineService.ConfigurationUrl,"resolve the shared service without player configuration");
            foreach(var origin in new[]{"ws://example.org","ws://127.0.0.1:8080","wss://localhost","wss://user:pass@example.org","wss://example.org/path","wss://example.org?secret=x","https://example.org"})
            {bool refused=false;try{NativeOnlineService.Validate(new NativeOnlineConfiguration{schema=1,protocol=6,relayOrigin=origin});}catch(Exception e)when(e is ArgumentException||e is InvalidOperationException){refused=true;}Check(refused,"reject unsafe discovered origin");}
            foreach(var body in new[]{"{}","{\"schema\":2,\"protocol\":6,\"relayOrigin\":\"wss://example.org\"}","{\"schema\":1,\"protocol\":4,\"relayOrigin\":\"wss://example.org\"}",new string(' ',4097)})
            {handler.Body=body;bool refused=false;try{await NativeOnlineService.Resolve(http,Decode,CancellationToken.None);}catch(InvalidOperationException){refused=true;}Check(refused,"missing, incompatible or oversized configuration fails closed");}
            handler.Status=HttpStatusCode.NotFound;bool unavailable=false;try{await NativeOnlineService.Resolve(http,Decode,CancellationToken.None);}catch(InvalidOperationException e){unavailable=e.Message==NativeOnlineService.Unavailable;}Check(unavailable,"undeployed service gives an honest unavailable state");
            using(var stop=new CancellationTokenSource()){stop.Cancel();bool cancelled=false;try{await NativeOnlineService.Resolve(http,Decode,stop.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"leaving a screen cancels service discovery");}
            handler.Delay=true;using(var stop=new CancellationTokenSource()){var pending=NativeOnlineService.Resolve(http,Decode,stop.Token);await handler.Entered.Task;stop.Cancel();bool cancelled=false;try{await pending;}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancellation interrupts an in-flight discovery request");}
        }
    }
    private sealed class ResponseHandler:HttpMessageHandler
    {
        public string Body,Url;public bool Delay;public readonly TaskCompletionSource<bool> Entered=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);public HttpStatusCode Status=HttpStatusCode.OK;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {token.ThrowIfCancellationRequested();Url=request.RequestUri.AbsoluteUri;if(Delay){Entered.TrySetResult(true);await Task.Delay(Timeout.Infinite,token);}return new HttpResponseMessage(Status){Content=new StringContent(Body??"")};}
    }
}
