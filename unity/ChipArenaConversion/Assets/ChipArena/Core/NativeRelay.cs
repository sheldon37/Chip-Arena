using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ChipArena.UnityConversion
{
    // Binary WebSocket byte stream carries the exact versioned LAN frames through a conversion-only relay.
    public sealed class NativeWebSocketStream:Stream
    {
        private readonly ClientWebSocket socket;public NativeWebSocketStream(ClientWebSocket socket){this.socket=socket;}
        public static Uri Endpoint(string origin,string path)
        {if(!Uri.TryCreate(origin,UriKind.Absolute,out var uri)||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath!="/"||uri.Scheme!="wss"&&!(uri.Scheme=="ws"&&uri.IsLoopback))throw new ArgumentException("Use a wss:// relay origin (ws:// is allowed on localhost).");return new Uri(uri,path);}
        public static async Task<NativeWebSocketStream> Connect(Uri uri,CancellationToken token)
        {var socket=new ClientWebSocket();socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(3);try{using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(token)){timeout.CancelAfter(10000);await socket.ConnectAsync(uri,timeout.Token);}return new NativeWebSocketStream(socket);}catch{socket.Dispose();throw;}}
        public override async Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken token)
        {while(true){var read=await socket.ReceiveAsync(new ArraySegment<byte>(buffer,offset,count),token);if(read.MessageType==WebSocketMessageType.Close)return 0;if(read.MessageType!=WebSocketMessageType.Binary)throw new InvalidDataException("Expected relay binary data");if(read.Count>0)return read.Count;}}
        public override async Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken token)=>await socket.SendAsync(new ArraySegment<byte>(buffer,offset,count),WebSocketMessageType.Binary,true,token);
        protected override void Dispose(bool disposing){if(disposing)socket.Dispose();base.Dispose(disposing);}public override bool CanRead=>true;public override bool CanWrite=>true;public override bool CanSeek=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}public override void Flush(){}public override int Read(byte[] b,int o,int n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int n)=>throw new NotSupportedException();public override long Seek(long a,SeekOrigin b)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
    }
    public sealed class NativeRelayHost:INativeOnlineHost
    {
        private readonly ClientWebSocket control;private readonly CancellationTokenSource stop;private readonly string origin,secret;private readonly Task loop,heartbeat;private int accepting,disposed;
        public readonly ConcurrentQueue<NativeConnection> Accepted=new ConcurrentQueue<NativeConnection>();public string Room{get;}public string Error{get;private set;}public bool Closed=>stop.IsCancellationRequested;
        ConcurrentQueue<NativeConnection> INativeOnlineHost.Accepted=>Accepted;
        private NativeRelayHost(ClientWebSocket socket,CancellationTokenSource cancellation,string origin,string room,string secret){control=socket;stop=cancellation;this.origin=origin;Room=room;this.secret=secret;loop=Task.Run(Listen);heartbeat=Task.Run(Heartbeat);}
        public static async Task<NativeRelayHost> Host(string origin,string source,string name,bool listed,CancellationToken cancellation)
        {
            var socket=new ClientWebSocket();var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellation);socket.Options.KeepAliveInterval=TimeSpan.FromSeconds(3);try{using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){timeout.CancelAfter(10000);await socket.ConnectAsync(NativeWebSocketStream.Endpoint(origin,"native/v3/host?source="+Uri.EscapeDataString(source)+"&name="+Uri.EscapeDataString(name)+"&listed="+(listed?"1":"0")+"&codeLength=6"),timeout.Token);string[] ack=(await Control(socket,timeout.Token)).Split('\t');if(ack.Length!=3||ack[0]!="ROOM"||!NativeRoomCodes.Valid(ack[1])||ack[2].Length!=64)throw new InvalidDataException("Invalid relay room response");return new NativeRelayHost(socket,stop,origin,ack[1],ack[2]);}}catch{socket.Dispose();stop.Dispose();throw;}
        }
        public static async Task<NativeConnection> Join(string origin,string room,string source,CancellationToken token)
        {var stream=await NativeWebSocketStream.Connect(NativeWebSocketStream.Endpoint(origin,"native/v3/join?room="+Uri.EscapeDataString(NativeRoomCodes.RawOnline(room))+"&source="+Uri.EscapeDataString(source)),token);return new NativeConnection(stream);}
        private static async Task<string> Control(ClientWebSocket socket,CancellationToken token)
        {var bytes=new byte[512];int total=0;while(true){var read=await socket.ReceiveAsync(new ArraySegment<byte>(bytes,total,bytes.Length-total),token);if(read.MessageType!=WebSocketMessageType.Text)throw new IOException("Relay control disconnected");total+=read.Count;if(read.EndOfMessage)break;if(total==bytes.Length)throw new InvalidDataException("Control message too long");}return new UTF8Encoding(false,true).GetString(bytes,0,total);}
        private async Task Heartbeat()
        {
            try{var ping=Encoding.UTF8.GetBytes("PING");while(!Closed){await Task.Delay(3000,stop.Token).ConfigureAwait(false);await control.SendAsync(new ArraySegment<byte>(ping),WebSocketMessageType.Text,true,stop.Token).ConfigureAwait(false);}}
            catch(Exception){if(!Closed)Error="Relay heartbeat failed.";Dispose();}
        }
        private async Task Listen()
        {
            try
            {
                while(!Closed)
                {
                    string message;using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){timeout.CancelAfter(12000);message=await Control(control,timeout.Token).ConfigureAwait(false);}
                    if(message=="PONG")continue;var fields=message.Split('\t');if(fields.Length!=2||fields[0]!="JOIN"||fields[1].Length!=64)throw new InvalidDataException("Invalid relay invitation");
                    if(Interlocked.Increment(ref accepting)>15){Interlocked.Decrement(ref accepting);continue;}_=Accept(fields[1]);
                }
            }
            catch(Exception){if(!Closed)Error="Relay control disconnected, timed out or sent an invalid message.";Dispose();}
        }
        private async Task Accept(string ticket)
        {try{var uri=NativeWebSocketStream.Endpoint(origin,"native/v3/accept?room="+Room+"&secret="+secret+"&ticket="+ticket);var stream=await NativeWebSocketStream.Connect(uri,stop.Token);if(Closed){stream.Dispose();return;}Accepted.Enqueue(new NativeConnection(stream,32768));}catch(Exception e)when(e is InvalidDataException||e is IOException||e is WebSocketException||e is OperationCanceledException||e is ObjectDisposedException){if(!Closed)Error="A relay connection failed.";}finally{Interlocked.Decrement(ref accepting);}}
        public void Dispose(){if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Cancel();control.Dispose();while(Accepted.TryDequeue(out var c))c.Dispose();}
    }
}
