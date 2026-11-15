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
        private async Task<INativeOnlineHost> CreateOnlineRoom(CancellationToken token,bool standby=false)
        {
            if(UseManagedRelay){string origin=await ResolveOnlineOrigin(token);return await NativeRelayHost.Host(origin,content.sourceCommit,prefs.name+"’s room",listedRoom,token);}
            string executable=Path.GetFullPath(Path.Combine(Application.dataPath,"..",NativeTunnelBinary.RelativePath));
            return await NativeCloudflareHost.Start(executable,s=>JsonUtility.FromJson<NativeDirectoryEntry>(s),token,standby:standby);
        }
        private async Task<NativeConnection> JoinOnlineRoom(string code,CancellationToken token)
        {if(UseManagedRelay){string origin=await ResolveOnlineOrigin(token);return await NativeRelayHost.Join(origin,code,content.sourceCommit,token);}return await NativeCloudflareHost.Join(code,s=>JsonUtility.FromJson<NativeDirectoryEntry>(s),token);}
        private Task<INativeOnlineHost> standbyRoom;
        private CancellationTokenSource standbyStop;
        private bool OpeningOnlineLobby=>!dedicated&&relayConnecting!=null;
        private void UpdateStandbyRoom()
        {
            // Only prepare the built-in private transport while the pilot is setting up an online room.
            if(dedicated||!menu||MenuTarget!=MenuScreen.Create||!internetRooms||UseManagedRelay||
                Hosting||server!=null||connecting!=null||relayConnecting!=null||modsError.Length!=0)
            {DiscardStandbyRoom();return;}
            if(standbyRoom!=null)return;
            standbyStop=new CancellationTokenSource();
            standbyRoom=CreateOnlineRoom(standbyStop.Token,true);
        }
        private void DiscardStandbyRoom()
        {
            var task=standbyRoom;if(task==null)return;
            var stop=standbyStop;standbyRoom=null;standbyStop=null;stop.Cancel();
            _=task.ContinueWith(done=>{
                try{if(done.Status==TaskStatus.RanToCompletion)done.Result.Dispose();else if(done.IsFaulted)_=done.Exception;}
                finally{stop.Dispose();}
            },TaskScheduler.Default);
        }
        private async Task<INativeOnlineHost> ActivateStandby(Task<INativeOnlineHost> pending,CancellationToken token)
        {
            try
            {
                var host=(NativeCloudflareHost)await pending;
                if(!token.IsCancellationRequested&&!host.Closed&&host.CodeAvailable){host.AdmitGuests();return host;}
                host.Dispose();
            }
            catch(Exception) when(pending.IsFaulted&&!token.IsCancellationRequested)
            {
                // Setup may have failed while the form was open. A real Create click gets a fresh attempt.
            }
            token.ThrowIfCancellationRequested();
            return await CreateOnlineRoom(token);
        }
        private void HostRelay()
        {
            if(!ModsReady)return;
            if(relayConnecting!=null||connecting!=null)return;
            try
            {
                Time.timeScale=1;EndPreview();
                var pending=UseManagedRelay?null:standbyRoom;var preparedStop=pending!=null?standbyStop:null;
                if(pending!=null){standbyRoom=null;standbyStop=null;}
                CloseNetwork();ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();if(!dedicated)Save();
                roomCode="";connectStop=preparedStop??new CancellationTokenSource();
                relayConnecting=pending==null?CreateOnlineRoom(connectStop.Token):ActivateStandby(pending,connectStop.Token);
                networkStatus="Connecting room to the internet…";LobbyBroadcast();
            }
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
            UpdateStandbyRoom();
            if(relayConnecting?.IsCompleted==true)
            {
                if(relayConnecting.IsCanceled||relayConnecting.IsFaulted)
                {
                    networkStatus=OnlineError(relayConnecting.Exception,"Could not start the online room. Check your internet connection and try again.");
                    if(!dedicated){roomGuest=false;afterMatch=MenuScreen.Create;menuFromMatch=true;}
                }
                else{relayHost=relayConnecting.Result;roomCode=relayHost.Room;networkStatus="Room ready · "+RoomCode;LobbyBroadcast();}
                relayConnecting=null;
            }
            if(relayHost is NativeCloudflareHost tunnel&&!tunnel.CodeAvailable)networkStatus="Room code refresh is delayed. Connected players can keep playing.";
            if(relayHost?.Closed==true){networkStatus="Online connection lost. The match has stopped.";ClearReplay();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();networkEpoch++;relayHost.Dispose();relayHost=null;foreach(var p in peers){p.Connection?.Dispose();p.Connection=null;if(p.Seat!=null)p.Seat.connected=false;}menu=true;}
        }
    }
}
