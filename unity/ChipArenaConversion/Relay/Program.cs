using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
var builder=WebApplication.CreateBuilder(args);
// Relay query tickets are credentials: request URLs must never enter access logs.
builder.Logging.AddFilter("Microsoft.AspNetCore",LogLevel.Warning);
builder.Services.AddRateLimiter(o=>{o.RejectionStatusCode=429;o.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(c=>RateLimitPartition.GetFixedWindowLimiter(c.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
var app=builder.Build();app.UseRateLimiter();app.UseWebSockets(new WebSocketOptions{KeepAliveInterval=TimeSpan.FromSeconds(3)});var rooms=new ConcurrentDictionary<string,Room>();
app.MapGet("/health",()=>Results.Ok(new{service="chip-arena-native-relay",protocol=2}));
app.MapGet("/native/v2/rooms",(HttpContext c)=>Results.Json(new{rooms=rooms.Values.Where(r=>r.Ready&&r.Listed&&r.Source==c.Request.Query["source"].ToString()).Take(100).Select(r=>new{code=r.Code,name=r.Name,players=1+r.Pipes.Count,capacity=8})}));
app.Map("/native/v2/host",async c=>{
 if(!c.WebSockets.IsWebSocketRequest||rooms.Count>=1000||!Regex.IsMatch(c.Request.Query["source"].ToString(),"^[a-f0-9]{40}$")){c.Response.StatusCode=400;return;}
 string name=Regex.Replace(c.Request.Query["name"].ToString(),"[<>\\p{Cc}\\p{Cf}]"," ").Trim();if(name.Length>40)name=name[..40];var room=new Room{Code=Token(4).ToUpperInvariant(),Secret=Token(32),Source=c.Request.Query["source"].ToString(),Name=name,Listed=c.Request.Query["listed"]=="1"};if(!rooms.TryAdd(room.Code,room)){c.Response.StatusCode=503;return;}
 using var socket=await c.WebSockets.AcceptWebSocketAsync();room.Host=socket;using var linked=CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted,room.Stop.Token);
 try{await Text(socket,"ROOM\t"+room.Code+"\t"+room.Secret,linked.Token);room.Ready=true;byte[] data=new byte[64];while(socket.State==WebSocketState.Open){var r=await socket.ReceiveAsync(new ArraySegment<byte>(data),linked.Token);if(r.MessageType==WebSocketMessageType.Close)break;if(r.Count>0)throw new InvalidDataException("Host control only accepts close frames");}}
 catch(Exception e)when(e is WebSocketException or OperationCanceledException or IOException){}
 finally{rooms.TryRemove(room.Code,out _);room.Stop.Cancel();foreach(var pipe in room.Pipes.Values){pipe.Stop.Cancel();pipe.Ready.TrySetCanceled();}}
});
app.Map("/native/v2/join",async c=>{
 if(!c.WebSockets.IsWebSocketRequest||!rooms.TryGetValue(c.Request.Query["room"].ToString(),out var room)||room.Source!=c.Request.Query["source"]||room.Stop.IsCancellationRequested||!room.Ready){c.Response.StatusCode=404;return;}
 var pipe=new Pipe{Ticket=Token(32)};lock(room.Gate){if(room.Pipes.Count>=7){c.Response.StatusCode=409;return;}room.Pipes[pipe.Ticket]=pipe;}
 using var socket=await c.WebSockets.AcceptWebSocketAsync();pipe.Guest=socket;using var linked=CancellationTokenSource.CreateLinkedTokenSource(c.RequestAborted,room.Stop.Token,pipe.Stop.Token);
 try{await room.Send.WaitAsync(linked.Token);try{await Text(room.Host!,"JOIN\t"+pipe.Ticket,linked.Token);}finally{room.Send.Release();}var host=await pipe.Ready.Task.WaitAsync(TimeSpan.FromSeconds(10),linked.Token);var up=Copy(socket,host,linked.Token,128*1024);var down=Copy(host,socket,linked.Token,8*1024*1024);await Task.WhenAny(up,down);pipe.Stop.Cancel();try{await Task.WhenAll(up,down);}catch(Exception e)when(e is WebSocketException or OperationCanceledException or IOException){} }
 catch(Exception e)when(e is WebSocketException or OperationCanceledException or TimeoutException or IOException){}
 finally{pipe.Stop.Cancel();pipe.Done.TrySetResult();room.Pipes.TryRemove(pipe.Ticket,out _);}
});
app.Map("/native/v2/accept",async c=>{
 if(!c.WebSockets.IsWebSocketRequest||!rooms.TryGetValue(c.Request.Query["room"].ToString(),out var room)||!Fixed(room.Secret,c.Request.Query["secret"].ToString())||!room.Pipes.TryGetValue(c.Request.Query["ticket"].ToString(),out var pipe)||Interlocked.Exchange(ref pipe.Claimed,1)!=0||pipe.Stop.IsCancellationRequested){c.Response.StatusCode=403;return;}
 using var socket=await c.WebSockets.AcceptWebSocketAsync();pipe.Ready.TrySetResult(socket);try{await pipe.Done.Task.WaitAsync(c.RequestAborted);}catch(OperationCanceledException){}finally{pipe.Stop.Cancel();}
});
app.Run();
static string Token(int bytes)=>Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
static bool Fixed(string a,string b)=>a.Length==b.Length&&CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a),Encoding.UTF8.GetBytes(b));
static Task Text(WebSocket s,string message,CancellationToken token)=>s.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),WebSocketMessageType.Text,true,token);
static async Task Copy(WebSocket from,WebSocket to,CancellationToken token,int budget){byte[] bytes=new byte[16384];long interval=Environment.TickCount64;int used=0;while(!token.IsCancellationRequested){var read=await from.ReceiveAsync(new ArraySegment<byte>(bytes),token);if(read.MessageType==WebSocketMessageType.Close)return;if(read.MessageType!=WebSocketMessageType.Binary)throw new InvalidDataException("Binary transport required");long now=Environment.TickCount64;if(now-interval>=1000){used=0;interval=now;}used+=read.Count;if(used>budget)throw new InvalidDataException("Relay bandwidth budget exceeded");await to.SendAsync(new ArraySegment<byte>(bytes,0,read.Count),WebSocketMessageType.Binary,read.EndOfMessage,token);}}
sealed class Room{public required string Code,Secret,Source,Name;public bool Listed,Ready;public WebSocket? Host;public readonly object Gate=new();public readonly SemaphoreSlim Send=new(1);public readonly CancellationTokenSource Stop=new();public readonly ConcurrentDictionary<string,Pipe> Pipes=new();}
sealed class Pipe{public required string Ticket;public int Claimed;public WebSocket? Guest;public readonly CancellationTokenSource Stop=new();public readonly TaskCompletionSource<WebSocket> Ready=new(TaskCreationOptions.RunContinuationsAsynchronously);public readonly TaskCompletionSource Done=new(TaskCreationOptions.RunContinuationsAsynchronously);}
