using System;
using System.Collections.Concurrent;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace ChipArena.UnityConversion
{
    // Conversion-only wire version. Never joins an Electron room or evaluates remote code.
    public sealed class NativeConnection:IDisposable
    {
        public const byte Protocol=7;
        public const int MaxFrame=2*1024*1024;
        // Two seconds of 60 Hz input plus control traffic; retain every command in order.
        public const int MaxReliablePackets=128,MaxReliableBytes=8*1024*1024;
        private static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
        private readonly TcpClient client;private readonly Stream stream;private readonly CancellationTokenSource stop=new CancellationTokenSource();
        private readonly ConcurrentQueue<byte[]> reliable=new ConcurrentQueue<byte[]>();private readonly SemaphoreSlim ready=new SemaphoreSlim(0,1);private byte[] latest;private string receivedState;
        // Only host-to-guest state is replaceable; commands and replies remain ordered.
        private static readonly Regex StateEnvelope=new Regex(@"^\{""version"":7,""epoch"":-?[0-9]+,""type"":""(?:frame|expedition-state)"",",RegexOptions.CultureInvariant);
        public readonly NativeConnectionMetrics Metrics=new NativeConnectionMetrics();public readonly ConcurrentQueue<string> Received=new ConcurrentQueue<string>();public string Error{get;private set;}public bool Closed=>stop.IsCancellationRequested;public DateTime LastReceived{get;private set;}=DateTime.UtcNow;
        private readonly int receiveLimit;private int reliableCount,receivedCount,reliableBytes;private readonly Task reader,writer;
        public NativeConnection(TcpClient socket,int inboundLimit=MaxFrame)
        {client=socket;client.NoDelay=true;receiveLimit=inboundLimit;stream=client.GetStream();reader=Task.Run(ReadLoop);writer=Task.Run(WriteLoop);}
        public NativeConnection(Stream transport,int inboundLimit=MaxFrame){stream=transport;receiveLimit=inboundLimit;reader=Task.Run(ReadLoop);writer=Task.Run(WriteLoop);}
        public static async Task<NativeConnection> Connect(string host,int port,CancellationToken cancellation)
        {
            var socket=new TcpClient();try{var connect=socket.ConnectAsync(host,port);var timeout=Task.Delay(8000,cancellation);if(await Task.WhenAny(connect,timeout).ConfigureAwait(false)!=connect)throw new TimeoutException("Connection timed out");await connect.ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return new NativeConnection(socket);}catch{socket.Dispose();throw;}
        }
        public static byte[] Encode(string json)
        {
            byte[] plain=Utf8.GetBytes(json);if(plain.Length<2||plain.Length>MaxFrame)throw new InvalidDataException("Invalid frame size");byte[] data;
            using(var output=new MemoryStream()){output.WriteByte(1);using(var zip=new DeflateStream(output,CompressionLevel.Fastest,true))zip.Write(plain,0,plain.Length);data=output.ToArray();}
            if(data.Length>=plain.Length+1){data=new byte[plain.Length+1];Buffer.BlockCopy(plain,0,data,1,plain.Length);}return data;
        }
        public static string Decode(byte[] data,int maximum)
        {
            if(data.Length<2||data.Length>MaxFrame+1||data[0]>1)throw new InvalidDataException("Invalid frame");
            if(data[0]==0){if(data.Length-1>maximum)throw new InvalidDataException("Frame too large");return Utf8.GetString(data,1,data.Length-1);}
            using(var input=new MemoryStream(data,1,data.Length-1))using(var zip=new DeflateStream(input,CompressionMode.Decompress))using(var result=new MemoryStream())
            {byte[] chunk=new byte[8192];int count;while((count=zip.Read(chunk,0,chunk.Length))>0){if(result.Length+count>maximum)throw new InvalidDataException("Expanded frame too large");result.Write(chunk,0,count);}return Utf8.GetString(result.ToArray());}
        }
        public void Send(string json,bool snapshot=false)
        {if(!Closed)SendEncoded(Encode(json),snapshot);}
        public void Reject(string json){Send(json);_=Task.Run(async()=>{await Task.Delay(100).ConfigureAwait(false);Dispose();});}
        public void ClearSnapshot()=>Interlocked.Exchange(ref latest,null);
        public void SendEncoded(byte[] packet,bool snapshot=false)
        {if(Closed)return;if(packet==null||packet.Length<2||packet.Length>MaxFrame+1)throw new InvalidDataException("Invalid encoded packet");if(snapshot)Interlocked.Exchange(ref latest,packet);else{int count=Interlocked.Increment(ref reliableCount);int bytes=Interlocked.Add(ref reliableBytes,packet.Length);if(count>MaxReliablePackets||bytes>MaxReliableBytes){Interlocked.Decrement(ref reliableCount);Interlocked.Add(ref reliableBytes,-packet.Length);Fail("Reliable queue overflow (send backlog exceeded bounded budget)");return;}reliable.Enqueue(packet);}if(ready.CurrentCount==0)try{ready.Release();}catch(SemaphoreFullException){}catch(ObjectDisposedException){}}
        public bool TryReceive(out string json){if(Received.TryDequeue(out json)){Interlocked.Decrement(ref receivedCount);return true;}json=Interlocked.Exchange(ref receivedState,null);return json!=null;}
        private async Task Exact(byte[] buffer,int count)
        {int at=0;while(at<count){int n=await stream.ReadAsync(buffer,at,count-at,stop.Token).ConfigureAwait(false);if(n==0)throw new EndOfStreamException();at+=n;}}
        private async Task ReadLoop()
        {
            try{var header=new byte[8];DateTime interval=DateTime.UtcNow;int rate=0;while(!Closed){await Exact(header,8).ConfigureAwait(false);if(header[0]!=67||header[1]!=65||header[2]!=85||header[3]!=Protocol)throw new InvalidDataException("Incompatible conversion protocol");int size=header[4]<<24|header[5]<<16|header[6]<<8|header[7];if(size<2||size>Math.Min(MaxFrame+1,receiveLimit+1024))throw new InvalidDataException("Invalid packet length");var data=new byte[size];await Exact(data,size).ConfigureAwait(false);var now=DateTime.UtcNow;if((now-interval).TotalSeconds>=1){interval=now;rate=0;}if(++rate>180)throw new InvalidDataException("Peer exceeded message rate");string message=Decode(data,receiveLimit);Metrics.Received(Utf8.GetByteCount(message));
                if(receiveLimit==MaxFrame&&StateEnvelope.IsMatch(message))Interlocked.Exchange(ref receivedState,message);
                else{if(Interlocked.Increment(ref receivedCount)>128)throw new InvalidDataException("Peer exceeded reliable receive budget");Received.Enqueue(message);}LastReceived=now;}}
            catch(Exception e)when(e is WebSocketException||e is InvalidDataException||e is IOException||e is SocketException||e is OperationCanceledException||e is ObjectDisposedException||e is ArgumentException){if(!Closed)Fail(e is EndOfStreamException?"Peer disconnected":e.Message);}
        }
        private async Task WriteLoop()
        {
            try
            {
                var clock=System.Diagnostics.Stopwatch.StartNew();long windowStart=0;int writes=0;
                while(!Closed)
                {
                    await ready.WaitAsync(stop.Token).ConfigureAwait(false);
                    do
                    {
                        byte[] data;
                        if(reliable.TryDequeue(out data)){Interlocked.Decrement(ref reliableCount);Interlocked.Add(ref reliableBytes,-data.Length);}
                        else data=Interlocked.Exchange(ref latest,null);
                        if(data==null)break;
                        // Drain a recovered backlog below the peer's 180 packets/second guard.
                        // Small bursts avoid adding a timer delay to normal 60 Hz input on Windows.
                        if(writes==16)
                        {
                            long delay=125-(clock.ElapsedMilliseconds-windowStart);
                            if(delay>0)await Task.Delay((int)delay,stop.Token).ConfigureAwait(false);
                            windowStart=clock.ElapsedMilliseconds;writes=0;
                        }
                        int n=data.Length;var wire=System.Buffers.ArrayPool<byte>.Shared.Rent(n+8);
                        try
                        {
                            wire[0]=67;wire[1]=65;wire[2]=85;wire[3]=Protocol;
                            wire[4]=(byte)(n>>24);wire[5]=(byte)(n>>16);wire[6]=(byte)(n>>8);wire[7]=(byte)n;
                            Buffer.BlockCopy(data,0,wire,8,n);
                            // One WebSocket message per framed packet, rather than separate header/body messages.
                            await stream.WriteAsync(wire,0,n+8,stop.Token).ConfigureAwait(false);
                        }
                        finally{System.Buffers.ArrayPool<byte>.Shared.Return(wire);}
                        writes++;
                    }while(!Closed);
                }
            }
            catch(Exception e)when(e is WebSocketException||e is InvalidDataException||e is IOException||e is SocketException||e is OperationCanceledException||e is ObjectDisposedException){if(!Closed)Fail(e.Message);}
        }
        private void Fail(string error){Error=error;Dispose();}
        public void Dispose(){if(Closed)return;stop.Cancel();stream.Dispose();client?.Close();}
    }
    public sealed class NativeListener:IDisposable
    {
        private readonly TcpListener listener;private readonly CancellationTokenSource stop=new CancellationTokenSource();private readonly Task loop;
        public readonly ConcurrentQueue<NativeConnection> Accepted=new ConcurrentQueue<NativeConnection>();public int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
        public NativeListener(int port,bool loopbackOnly=false){listener=new TcpListener(loopbackOnly?IPAddress.Loopback:IPAddress.Any,port);listener.Start(16);loop=Task.Run(Accept);}
        private async Task Accept(){try{while(!stop.IsCancellationRequested){var socket=await listener.AcceptTcpClientAsync().ConfigureAwait(false);if(Accepted.Count>=16){socket.Close();continue;}Accepted.Enqueue(new NativeConnection(socket,32768));}}catch(ObjectDisposedException){}catch(SocketException){}}
        public void Dispose(){stop.Cancel();listener.Stop();while(Accepted.TryDequeue(out var c))c.Dispose();}
    }
}
