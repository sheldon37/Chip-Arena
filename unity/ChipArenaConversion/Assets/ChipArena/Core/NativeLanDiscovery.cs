using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace ChipArena.UnityConversion
{
    public sealed class NativeLanDiscovery:IDisposable
    {
        public const int DiscoveryPort=29942;
        private readonly UdpClient listener;private readonly CancellationTokenSource stop=new CancellationTokenSource();private readonly string code,source;private readonly int port;
        public static string[] Addresses()
        {try{return NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Select(a=>a.Address).Where(a=>a.AddressFamily==AddressFamily.InterNetwork&&!IPAddress.IsLoopback(a)).Select(a=>a.ToString()).Distinct().ToArray();}catch(NetworkInformationException){return Array.Empty<string>();}}
        public NativeLanDiscovery(string code,string source,int port)
        {this.code=code;this.source=source;this.port=port;listener=new UdpClient(AddressFamily.InterNetwork);try{listener.Client.SetSocketOption(SocketOptionLevel.Socket,SocketOptionName.ReuseAddress,true);listener.Client.Bind(new IPEndPoint(IPAddress.Any,DiscoveryPort));_=Task.Run(Listen);}catch{listener.Dispose();throw;}}
        private async Task Listen()
        {
            try{while(!stop.IsCancellationRequested){var datagram=await listener.ReceiveAsync().ConfigureAwait(false);if(datagram.Buffer.Length>160)continue;var request=Encoding.ASCII.GetString(datagram.Buffer).Split(' ');if(request.Length!=4||request[0]!="CAU4"||request[1]!=code||request[2]!=source||request[3].Length!=16)continue;var response=Encoding.ASCII.GetBytes("CAU4 "+code+" "+source+" "+request[3]+" "+port);await listener.SendAsync(response,response.Length,datagram.RemoteEndPoint).ConfigureAwait(false);}}
            catch(Exception e)when(e is SocketException||e is ObjectDisposedException||e is OperationCanceledException){}
        }
        public static async Task<NativeConnection> Connect(string code,string source,CancellationToken cancellation)
        {
            code=NativeRoomCodes.RawOnline(code);
            using(var socket=new UdpClient(AddressFamily.InterNetwork))using(var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                socket.EnableBroadcast=true;socket.Client.Bind(new IPEndPoint(IPAddress.Any,0));timeout.CancelAfter(3500);
                using(timeout.Token.Register(()=>socket.Dispose()))
                {
                    string nonce=Guid.NewGuid().ToString("N").Substring(0,16);var request=Encoding.ASCII.GetBytes("CAU4 "+code+" "+source+" "+nonce);
                    var receive=socket.ReceiveAsync();
                    while(!timeout.IsCancellationRequested)
                    {
                        try{await socket.SendAsync(request,request.Length,new IPEndPoint(IPAddress.Broadcast,DiscoveryPort)).ConfigureAwait(false);}catch(SocketException){/* Some interfaces prohibit broadcast; loopback/manual address still work. */}
                        // Also reaches hosts on this PC when broadcast is not looped back by the OS.
                        await socket.SendAsync(request,request.Length,new IPEndPoint(IPAddress.Loopback,DiscoveryPort)).ConfigureAwait(false);
                        var delay=Task.Delay(600,timeout.Token);
                        while(await Task.WhenAny(receive,delay).ConfigureAwait(false)==receive)
                        {
                            var reply=await receive.ConfigureAwait(false);receive=socket.ReceiveAsync();
                            if(reply.Buffer.Length>160)continue;var fields=Encoding.ASCII.GetString(reply.Buffer).Split(' ');
                            if(fields.Length==5&&fields[0]=="CAU4"&&fields[1]==code&&fields[2]==source&&fields[3]==nonce&&int.TryParse(fields[4],out int port)&&port>0&&port<=65535)
                                return await NativeConnection.Connect(reply.RemoteEndPoint.Address.ToString(),port,cancellation).ConfigureAwait(false);
                        }
                    }
                }
            }
            throw new TimeoutException("LAN discovery timed out. Use the host address if this network blocks broadcasts.");
        }
        public static string Invite(string address,int port,string code)=>"chiparena://lan?address="+Uri.EscapeDataString(address)+"&port="+port+"&code="+Uri.EscapeDataString(code);
        public static bool ParseInvite(string value,out string address,out int port,out string code)
        {
            address=code=null;port=0;if(value==null||value.Length>512||!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="chiparena"||uri.Host!="lan"||uri.UserInfo.Length>0||uri.Fragment.Length>0)return false;
            foreach(var part in uri.Query.TrimStart('?').Split('&')){var pair=part.Split('=');if(pair.Length!=2)return false;string decoded=Uri.UnescapeDataString(pair[1]);if(pair[0]=="address")address=decoded;else if(pair[0]=="port")int.TryParse(decoded,out port);else if(pair[0]=="code")code=decoded.ToUpperInvariant();else return false;}
            return IPAddress.TryParse(address,out var ip)&&ip.AddressFamily==AddressFamily.InterNetwork&&port>0&&port<=65535&&NativeRoomCodes.Valid(code);
        }
        public void Dispose(){stop.Cancel();listener.Dispose();}
    }
}
