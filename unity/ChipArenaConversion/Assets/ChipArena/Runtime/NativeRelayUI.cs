using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeRoomListing {public string code,name;public int players,capacity;}
    [Serializable] public sealed class NativeRoomListings {public NativeRoomListing[] rooms;}
    public sealed partial class ConversionGame
    {
        private NativeRelayHost relayHost;private Task<NativeRelayHost> relayConnecting;private bool internetRooms,listedRoom;private NativeRoomListing[] relayRooms=Array.Empty<NativeRoomListing>();
        private void HostRelay()
        {
            try{NativeWebSocketStream.Endpoint(prefs.relayOrigin,"native/v3/host");CloseNetwork();ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();if(!dedicated)Save();connectStop=new CancellationTokenSource();relayConnecting=NativeRelayHost.Host(prefs.relayOrigin,content.sourceCommit,prefs.name+"’s room",listedRoom,connectStop.Token);networkStatus="Creating internet room…";}
            catch(ArgumentException e){networkStatus=e.Message;}
        }
        private IEnumerator ListRelayRooms()
        {
            Uri uri;try{uri=NativeWebSocketStream.Endpoint(prefs.relayOrigin,"native/v3/rooms?source="+content.sourceCommit);}catch(ArgumentException e){networkStatus=e.Message;yield break;}var http=new UriBuilder(uri){Scheme=uri.Scheme=="wss"?"https":"http"};
            using(var request=UnityWebRequest.Get(http.Uri.AbsoluteUri)){request.redirectLimit=0;request.timeout=8;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success||request.downloadedBytes>32768){networkStatus="Room list could not be loaded.";yield break;}try{var data=JsonUtility.FromJson<NativeRoomListings>(request.downloadHandler.text);relayRooms=(data?.rooms??Array.Empty<NativeRoomListing>()).Where(r=>r!=null&&r.name?.Length<=40&&r.code?.Length==8&&r.players>=1&&r.players<=8).Take(100).ToArray();networkStatus=relayRooms.Length+" room(s) found.";}catch(ArgumentException){networkStatus="Invalid room list.";}}
        }
        private void RelayTick()
        {
            if(relayConnecting?.IsCompleted==true){if(relayConnecting.IsCanceled||relayConnecting.IsFaulted)networkStatus="Relay connection failed. Check the relay URL.";else{relayHost=relayConnecting.Result;roomCode=relayHost.Room;networkStatus="Hosting internet room "+RoomCode;LobbyBroadcast();}relayConnecting=null;}
            if(relayHost?.Closed==true){networkStatus="Relay disconnected. The match has stopped.";ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();networkEpoch++;relayHost.Dispose();relayHost=null;foreach(var p in peers){p.Connection?.Dispose();p.Connection=null;if(p.Seat!=null)p.Seat.connected=false;}menu=true;}
        }
    }
}
