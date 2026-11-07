using System;
using System.Collections;
using System.IO;
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
        private INativeOnlineHost relayHost;private Task<INativeOnlineHost> relayConnecting;private bool internetRooms=true,listedRoom;private NativeRoomListing[] relayRooms=Array.Empty<NativeRoomListing>();
        private CancellationTokenSource listStop;private bool listingRooms;
        private string relayOverride="";private bool UseManagedRelay=>relayOverride.Length>0;
        private static string OnlineError(Exception error,string fallback)=>error?.GetBaseException() is NativeOnlineRoomException known?known.Message:fallback;
        private Task<string> ResolveOnlineOrigin(CancellationToken token){token.ThrowIfCancellationRequested();return Task.FromResult(relayOverride);}
        private async Task<INativeOnlineHost> CreateOnlineRoom(CancellationToken token)
        {
            if(UseManagedRelay){string origin=await ResolveOnlineOrigin(token);return await NativeRelayHost.Host(origin,content.sourceCommit,prefs.name+"’s room",listedRoom,token);}
            string executable=Path.GetFullPath(Path.Combine(Application.dataPath,"..",NativeTunnelBinary.RelativePath));
            return await NativeCloudflareHost.Start(executable,s=>JsonUtility.FromJson<NativeDirectoryEntry>(s),token);
        }
        private async Task<NativeConnection> JoinOnlineRoom(string code,CancellationToken token)
        {if(UseManagedRelay){string origin=await ResolveOnlineOrigin(token);return await NativeRelayHost.Join(origin,code,content.sourceCommit,token);}return await NativeCloudflareHost.Join(code,s=>JsonUtility.FromJson<NativeDirectoryEntry>(s),token);}
        private void HostRelay()
        {
            if(!ModsReady)return;
            if(relayConnecting!=null||connecting!=null)return;
            try{Time.timeScale=1;EndPreview();CloseNetwork();ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();if(!dedicated)Save();connectStop=new CancellationTokenSource();relayConnecting=CreateOnlineRoom(connectStop.Token);networkStatus="Creating online room…";}
            catch(ArgumentException e){networkStatus=e.Message;}
        }
        private IEnumerator ListRelayRooms()
        {
            if(listingRooms)yield break;listingRooms=true;try{listStop?.Cancel();listStop=new CancellationTokenSource();var token=listStop.Token;relayRooms=Array.Empty<NativeRoomListing>();networkStatus="Finding online rooms…";
            var resolve=ResolveOnlineOrigin(token);while(!resolve.IsCompleted){if(token.IsCancellationRequested)yield break;yield return null;}
            if(token.IsCancellationRequested)yield break;if(resolve.IsFaulted||resolve.IsCanceled){networkStatus="Room list could not be loaded. Please try again.";yield break;}
            var http=new UriBuilder(NativeWebSocketStream.Endpoint(resolve.Result,"native/v3/rooms?source="+content.sourceCommit)){Scheme="https"};
            using(var request=UnityWebRequest.Get(http.Uri.AbsoluteUri)){request.redirectLimit=0;request.timeout=8;yield return request.SendWebRequest();if(token.IsCancellationRequested)yield break;if(request.result!=UnityWebRequest.Result.Success||request.downloadedBytes>32768){networkStatus="Room list could not be loaded. Please try again.";yield break;}try{var data=JsonUtility.FromJson<NativeRoomListings>(request.downloadHandler.text);relayRooms=(data?.rooms??Array.Empty<NativeRoomListing>()).Where(r=>r!=null&&r.name?.Length<=40&&NativeRoomCodes.Valid(r.code)&&r.players>=1&&r.players<=8).Take(100).ToArray();networkStatus=relayRooms.Length+" room(s) found.";}catch(ArgumentException){networkStatus="Room list could not be loaded. Please try again.";}}
            }finally{listingRooms=false;}
        }
        private void RelayTick()
        {
            if(relayConnecting?.IsCompleted==true){if(relayConnecting.IsCanceled||relayConnecting.IsFaulted)networkStatus=OnlineError(relayConnecting.Exception,"Could not start the online room. Check your internet connection and try again.");else{relayHost=relayConnecting.Result;roomCode=relayHost.Room;networkStatus="Room ready · "+RoomCode;LobbyBroadcast();}relayConnecting=null;}
            if(relayHost is NativeCloudflareHost tunnel&&!tunnel.CodeAvailable)networkStatus="Room code refresh is delayed. Connected players can keep playing.";
            if(relayHost?.Closed==true){networkStatus="Online connection lost. The match has stopped.";ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();networkEpoch++;relayHost.Dispose();relayHost=null;foreach(var p in peers){p.Connection?.Dispose();p.Connection=null;if(p.Seat!=null)p.Seat.connected=false;}menu=true;}
        }
    }
}
