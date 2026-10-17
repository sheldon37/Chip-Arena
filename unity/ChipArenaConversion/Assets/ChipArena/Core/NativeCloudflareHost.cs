using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ChipArena.UnityConversion
{
    public interface INativeOnlineHost:IDisposable
    {
        string Room{get;} bool Closed{get;} ConcurrentQueue<NativeConnection> Accepted{get;}
    }
    public sealed class NativeCloudflareHost:INativeOnlineHost
    {
        private readonly NativeListener listener;private readonly CancellationTokenSource stop;
        private readonly HttpClient http;private readonly Func<string,NativeDirectoryEntry> decode;
        private readonly string secret;private NativeTunnelProcess tunnel;private int disposed;
        private long directoryUntil;public string Room{get;private set;}
        public string Origin=>tunnel?.Origin;
        public bool Closed=>stop.IsCancellationRequested||tunnel?.Closed==true;
        public bool CodeAvailable=>DateTime.UtcNow.Ticks<Interlocked.Read(ref directoryUntil);
        public ConcurrentQueue<NativeConnection> Accepted=>listener.Accepted;
        private NativeCloudflareHost(Func<string,NativeDirectoryEntry> decode,CancellationToken token)
        {
            this.decode=decode;stop=CancellationTokenSource.CreateLinkedTokenSource(token);http=NativeRoomDirectory.Client();
            listener=new NativeListener(0,true);byte[] bytes=new byte[32];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
            secret=BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant();
        }
        public static async Task<NativeCloudflareHost> Start(string executable,Func<string,NativeDirectoryEntry> decode,CancellationToken token,bool linuxTest=false)
        {
            var host=new NativeCloudflareHost(decode,token);
            try
            {
                host.tunnel=await NativeTunnelProcess.Start(executable,host.listener.Port,host.stop.Token,linuxTest).ConfigureAwait(false);
                for(int attempt=0;attempt<8;attempt++)
                {
                    string code=NativeRoomCodes.Create();
                    if(!await NativeRoomDirectory.Register(host.http,code,host.Origin,host.secret,decode,host.stop.Token).ConfigureAwait(false))continue;
                    host.Room=code;Interlocked.Exchange(ref host.directoryUntil,DateTime.UtcNow.AddSeconds(150).Ticks);
                    host.stop.Token.ThrowIfCancellationRequested();_=host.Renew();return host;
                }
                throw new NativeOnlineRoomException("Could not reserve a room code. Please try creating the room again.");
            }
            catch{host.Dispose();throw;}
        }
        private async Task Renew()
        {
            try
            {
                while(!Closed)
                {
                    await Task.Delay(CodeAvailable?45000:10000,stop.Token).ConfigureAwait(false);
                    try
                    {
                        if(await NativeRoomDirectory.Register(http,Room,Origin,secret,decode,stop.Token).ConfigureAwait(false))
                            Interlocked.Exchange(ref directoryUntil,DateTime.UtcNow.AddSeconds(150).Ticks);
                        else Interlocked.Exchange(ref directoryUntil,0);
                    }
                    catch(NativeOnlineRoomException){/* A directory outage must not interrupt connected players. */}
                }
            }
            catch(OperationCanceledException){}catch(ObjectDisposedException){}
        }
        public static async Task<NativeConnection> Join(string code,Func<string,NativeDirectoryEntry> decode,CancellationToken token)
        {
            using(var http=NativeRoomDirectory.Client())
            {
                string origin=await NativeRoomDirectory.Lookup(http,code,decode,token).ConfigureAwait(false);
                return await Connect(origin,token).ConfigureAwait(false);
            }
        }
        public static async Task<NativeConnection> Connect(string origin,CancellationToken token)
        {
            var endpoint=new Uri(NativeRoomDirectory.TunnelOrigin(origin).Replace("https://","wss://")+"/");
            Exception last=null;
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                // Registration can precede DNS/edge routing. Three fast failures used to give up in two seconds.
                deadline.CancelAfter(40000);
                for(int attempt=0;attempt<20;attempt++)
                {
                    token.ThrowIfCancellationRequested();
                    try{return new NativeConnection(await NativeWebSocketStream.Connect(endpoint,deadline.Token).ConfigureAwait(false));}
                    catch(Exception e)when(e is WebSocketException||e is OperationCanceledException)
                    {
                        last=e;token.ThrowIfCancellationRequested();if(deadline.IsCancellationRequested||attempt==19)break;
                        try{await Task.Delay(2000,deadline.Token).ConfigureAwait(false);}
                        catch(OperationCanceledException){token.ThrowIfCancellationRequested();break;}
                    }
                }
            }
            throw new NativeOnlineRoomException("Could not reach the host. Ask them to keep their room open and try again.",last);
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref disposed,1)!=0)return;stop.Cancel();tunnel?.Dispose();listener.Dispose();http.Dispose();
            // Directory leases expire after three minutes. No endpoint is repointed after the host leaves.
        }
    }
}
