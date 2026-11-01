#if UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        // Inactive components avoid Awake (account/prefs/presentation); real loopback transports
        // exercise the same handlers used by the lobby, including the native JsonUtility codec.
        public static void ValidateLobbyPromotion(ConversionContent data)
        {
            var a=new GameObject("Lobby authority fixture");a.SetActive(false);
            var b=new GameObject("Lobby guest fixture");b.SetActive(false);
            var host=a.AddComponent<ConversionGame>();var guest=b.AddComponent<ConversionGame>();
            void Check(bool ok,string why){if(!ok)throw new Exception("Lobby transfer: "+why);}
            NativeNetMessage Read(NativeConnection connection)
            {
                var end=DateTime.UtcNow.AddSeconds(5);
                while(DateTime.UtcNow<end){if(connection.TryReceive(out var json))return NativeNetCodec.Decode(json);Thread.Sleep(5);}
                throw new Exception("Lobby transfer message timed out");
            }
            NativeConnection Connect(ConversionGame target,out NativePeer peer)
            {
                var connection=NativeConnection.Connect("127.0.0.1",target.listener.Port,CancellationToken.None).GetAwaiter().GetResult();
                var end=DateTime.UtcNow.AddSeconds(5);NativeConnection accepted=null;
                while(!target.listener.Accepted.TryDequeue(out accepted)&&DateTime.UtcNow<end)Thread.Sleep(5);
                if(accepted==null)throw new Exception("Lobby fixture accept timeout");peer=new NativePeer{Connection=accepted};target.peers.Add(peer);return connection;
            }
            try
            {
                host.content=guest.content=data;host.prefs=new ConversionPreferences{pve=true,expedition=true,bots=0};guest.prefs=new ConversionPreferences();
                host.listener=new NativeListener(0);host.roomCode=NativeRoomCodes.Create();guest.internetRooms=false;guest.joinCode=host.roomCode;
                // Reclaim the same seat repeatedly, even before the authority observes a disconnect.
                for(int attempt=0;attempt<3;attempt++)
                {
                    guest.server=Connect(host,out var peer);
                    host.HostMessage(peer,new NativeNetMessage{type="hello",source=data.sourceCommit,contentRevision=data.contentRevision,room=host.roomCode,
                        lobbyProtocol=1,expeditionProtocol=NativeExpeditionParty.Protocol,resume=guest.resumeToken,profile=guest.Profile()});
                    guest.ClientMessage(Read(guest.server));Check(guest.networkId==2&&host.peers.Count==1,"rejoin retains one seat, attempt "+attempt);
                    string token=guest.resumeToken;guest.CloseNetwork();Check(guest.resumeToken==token&&guest.resumeRoom==guest.ResumeRoomKey,"menu return retains room-scoped resume token");
                }
                guest.server=Connect(host,out var active);
                host.HostMessage(active,new NativeNetMessage{type="hello",source=data.sourceCommit,contentRevision=data.contentRevision,room=host.roomCode,lobbyProtocol=1,
                    expeditionProtocol=NativeExpeditionParty.Protocol,resume=guest.resumeToken,profile=guest.Profile()});guest.ClientMessage(Read(guest.server));
                host.FlushLobby();guest.ClientMessage(Read(guest.server));
                Check(host.CanPromote(2),"updated connected guest eligible");active.LobbyProtocol=0;Check(!host.CanPromote(2),"old guest cannot transfer");active.LobbyProtocol=1;
                host.PromoteHost(2);Check(host.PromotionBusy&&!host.GuestsReady,"start blocked during preparation");guest.ClientMessage(Read(guest.server));
                Check(!guest.Hosting&&host.Hosting,"old lobby remains authority while preparing");host.HostMessage(active,Read(active.Connection));
                guest.PromotionClientMessage(new NativeNetMessage{type="host-commit",transfer="wrong"});Check(!guest.Hosting,"wrong transfer cannot commit");
                guest.ClientMessage(Read(guest.server));host.HostMessage(active,Read(active.Connection));
                Check(guest.Hosting&&guest.server==null&&guest.prefs.expedition&&guest.prefs.bots==0,"target becomes host with room options");
                Check(host.promotionMoveAt>0&&host.promotionCode==guest.roomCode,"old host schedules join only after new host acknowledges");
                Check(!guest.CanPromote(1),"host cannot promote itself");
            }
            finally{host.CloseNetwork();guest.CloseNetwork();UnityEngine.Object.DestroyImmediate(a);UnityEngine.Object.DestroyImmediate(b);}
        }
    }
}
#endif
