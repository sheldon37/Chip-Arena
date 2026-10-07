using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
namespace ChipArena.UnityConversion
{
    [Serializable] public sealed class NativeNetMessage
    {
        public int version=2,id,epoch,generation,pick,team;public string type,source,room,resume,token,chip,error;public bool ready;
        public NativeProfile profile;public InputFrame input;public NativeGameFrame frame;public NativeSeatFrame[] seats;
    }
    internal sealed class NativePeer
    {public NativeConnection Connection;public NativeSeat Seat;public string Resume;public bool Ready;public DateTime Arrived=DateTime.UtcNow,Disconnected;}
    public sealed partial class ConversionGame
    {
        private NativeListener listener;private NativeConnection server;private readonly List<NativePeer> peers=new List<NativePeer>();
        private Task<NativeConnection> connecting;private CancellationTokenSource connectStop;private string hostAddress="127.0.0.1",portText="29941",roomCode="",joinCode="",resumeToken="",networkStatus="";private int networkId=1,networkEpoch,inputSequence;private float nextSnapshot,nextPing;private bool networkReady;private NativeSeatFrame[] lobbySeats=Array.Empty<NativeSeatFrame>();
        private bool Online=>Hosting||server!=null;private bool Hosting=>listener!=null||relayHost!=null;
        private static string RandomToken(int bytes){var data=new byte[bytes];using(var random=RandomNumberGenerator.Create())random.GetBytes(data);return BitConverter.ToString(data).Replace("-","").ToLowerInvariant();}
        private NativeProfile Profile()=>new NativeProfile{name=prefs.name,@class=content.Weapon(prefs.weapon).@class,color=prefs.accentColor,cosmetics=new NativeCosmetics{bodyColor=prefs.bodyColor,hat=prefs.hat,killEffect=prefs.killEffect,preferredSpec=prefs.weapon,intro=prefs.intro,introText=prefs.introText,overrideWeaponIntro=prefs.overrideIntro}};
        private void HostRoom()
        {
            Time.timeScale=1;EndPreview();
            try{if(!int.TryParse(portText,out int port)||port<1||port>65535)throw new ArgumentException("Choose a port between 1 and 65535.");CloseNetwork();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();listener=new NativeListener(port);roomCode=RandomToken(4).ToUpperInvariant();networkStatus="Hosting on port "+port+" · Room "+roomCode;menu=true;}
            catch(Exception e)when(e is System.Net.Sockets.SocketException||e is ArgumentException){networkStatus=e.Message;}
        }
        private void JoinRoom()
        {
            Time.timeScale=1;EndPreview();
            if(connecting!=null)return;if(!int.TryParse(portText,out int port)||port<1||port>65535){networkStatus="Invalid port.";return;}
            server?.Dispose();server=null;networkId=0;connectStop?.Cancel();connectStop=new CancellationTokenSource();try{connecting=internetRooms?NativeRelayHost.Join(prefs.relayOrigin,joinCode.Trim().ToUpperInvariant(),content.sourceCommit,connectStop.Token):NativeConnection.Connect(hostAddress.Trim(),port,connectStop.Token);}catch(ArgumentException e){networkStatus=e.Message;return;}networkStatus="Connecting…";
        }
        private void CloseNetwork()
        {connectStop?.Cancel();if(connecting!=null){var abandoned=connecting;_=abandoned.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});}connecting=null;if(relayConnecting!=null){var abandoned=relayConnecting;_=abandoned.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});}relayConnecting=null;relayHost?.Dispose();relayHost=null;listener?.Dispose();listener=null;server?.Dispose();server=null;foreach(var peer in peers)peer.Connection?.Dispose();peers.Clear();lobbySeats=Array.Empty<NativeSeatFrame>();networkReady=false;networkId=1;resumeToken="";}
        private void SendServer(NativeNetMessage message){message.epoch=networkEpoch;message.generation=arena?.Generation??0;server?.Send(JsonUtility.ToJson(message));}
        private void NetworkTick()
        {
            RelayTick();
            if(connecting?.IsCompleted==true)
            {
                if(connecting.IsFaulted||connecting.IsCanceled)networkStatus="Could not connect. Check the host address, port and firewall.";
                else{server=connecting.Result;SendServer(new NativeNetMessage{type="hello",source=content.sourceCommit,room=joinCode.Trim().ToUpperInvariant(),resume=resumeToken,token=account?.Token,profile=Profile()});networkStatus="Joining conversion room…";}connecting=null;
            }
            if(Hosting)
            {
                var accepted=listener?.Accepted??relayHost.Accepted;while(accepted.TryDequeue(out var c)){if(peers.Count(p=>p.Connection!=null&&!p.Connection.Closed)>=15)c.Dispose();else peers.Add(new NativePeer{Connection=c});}
                foreach(var peer in peers.ToArray())
                {
                    var connection=peer.Connection;if(connection==null)continue;
                    if(connection.Closed||(DateTime.UtcNow-connection.LastReceived).TotalSeconds>12||peer.Seat==null&&(DateTime.UtcNow-peer.Arrived).TotalSeconds>5)
                    {connection.Dispose();peer.Connection=null;peer.Disconnected=DateTime.UtcNow;if(peer.Seat!=null){peer.Seat.connected=false;arena?.AcceptInput(peer.Seat.id,new InputFrame{Sequence=int.MaxValue});LobbyBroadcast();}else peers.Remove(peer);continue;}
                    int count=0;while(count++<120&&connection.TryReceive(out string json))try{var m=JsonUtility.FromJson<NativeNetMessage>(json);if(m==null||m.version!=2)throw new ArgumentException();HostMessage(peer,m);}catch(Exception e)when(e is ArgumentException||e is InvalidOperationException){connection.Dispose();networkStatus="A peer sent an invalid message.";break;}
                }
                foreach(var peer in peers.Where(p=>p.Connection==null&&(DateTime.UtcNow-p.Disconnected).TotalSeconds>90&&arena==null).ToArray())peers.Remove(peer);
                if(arena!=null&&Time.unscaledTime>=nextSnapshot){nextSnapshot=Time.unscaledTime+.05f;string json=JsonUtility.ToJson(new NativeNetMessage{type="frame",epoch=networkEpoch,frame=arena.NetworkFrame()});foreach(var peer in peers)if(peer.Seat!=null&&peer.Connection?.Closed==false)peer.Connection.Send(json,true);}
            }
            if(server!=null)
            {
                int count=0;while(count++<64&&server.TryReceive(out string json))try{var m=JsonUtility.FromJson<NativeNetMessage>(json);ClientMessage(m);}catch(Exception e)when(e is ArgumentException||e is InvalidOperationException){server.Dispose();networkStatus="Invalid or incompatible host state.";break;}
                if(server.Closed||(DateTime.UtcNow-server.LastReceived).TotalSeconds>12){networkStatus="Disconnected. Rejoin this host within 90 seconds to reclaim your seat.";server.Dispose();server=null;menu=true;pending=new InputFrame();}
            }
            if(Online&&Time.unscaledTime>=nextPing){nextPing=Time.unscaledTime+1;var ping=JsonUtility.ToJson(new NativeNetMessage{type="ping"});server?.Send(ping);foreach(var p in peers)if(p.Connection?.Closed==false)p.Connection.Send(ping);}
            if(arena?.Replica==true)arena.InterpolateReplica(Time.unscaledDeltaTime);
        }
        private void HostMessage(NativePeer peer,NativeNetMessage message)
        {
            var c=peer.Connection;if(message.type=="ping")return;
            if(peer.Seat==null)
            {
                if(message.type!="hello"||message.source!=content.sourceCommit||message.room!=roomCode){c.Send(JsonUtility.ToJson(new NativeNetMessage{type="error",error="Room code or conversion version does not match."}));return;}
                NativePeer previous=!string.IsNullOrEmpty(message.resume)?peers.Find(p=>p!=peer&&p.Resume==message.resume&&p.Connection==null&&(DateTime.UtcNow-p.Disconnected).TotalSeconds<=90):null;
                if(previous!=null){peer.Seat=previous.Seat;peer.Resume=previous.Resume;peer.Ready=previous.Ready;peer.Seat.connected=true;peers.Remove(previous);arena?.ClearRemoteInput(peer.Seat.id);}
                else
                {
                    if(arena!=null||peers.Count(p=>p.Seat!=null)>=(prefs.pve?dedicated?4:3:dedicated?8:7)){c.Send(JsonUtility.ToJson(new NativeNetMessage{type="error",error="The room is full or the match has started."}));return;}
                    int id=dedicated?1:2;while(peers.Any(p=>p.Seat?.id==id))id++;peer.Seat=SanitizedSeat(id,message.profile,message.token);peer.Resume=RandomToken(32);
                }
                c.Send(JsonUtility.ToJson(new NativeNetMessage{type="welcome",id=peer.Seat.id,resume=peer.Resume,epoch=networkEpoch}));LobbyBroadcast();return;
            }
            var seat=peer.Seat;
            if(message.type=="auth-refresh"){var access=NativeAccess.Verify(message.token,s=>JsonUtility.FromJson<NativePlayerClaim>(s),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());if(!string.IsNullOrEmpty(access.AccountId)&&access.AccountId==seat.access.AccountId)seat.access=access;return;}
            if(message.type=="ready"&&arena==null){peer.Ready=message.ready;LobbyBroadcast();return;}
            if(message.type=="profile"&&arena==null){var clean=SanitizedSeat(seat.id,message.profile,message.token);peer.Seat=clean;peer.Ready=false;LobbyBroadcast();return;}
            if(message.type=="team"&&arena==null){seat.team=Math.Max(0,Math.Min(7,message.team));LobbyBroadcast();return;}
            if(arena==null||message.epoch!=networkEpoch||message.generation!=arena.Generation)return;
            if(message.type=="input"){arena.AcceptInput(seat.id,message.input);return;}var pilot=arena.Pilots.Find(p=>p.Seat.id==seat.id);if(pilot==null)return;
            if(message.type=="chip"&&message.chip?.Length<=64)match.Choose(seat.id,message.chip);
            else if(message.type=="pve-pick"&&message.chip?.Length<=64)arena.ChoosePve(pilot,message.pick,message.chip);
            else if(message.type=="pve-open")arena.OpenPvePick(pilot);
            else if(message.type=="pve-close")arena.ClosePvePick(pilot);
            else if(message.type=="rematch"){peer.Ready=true;LobbyBroadcast();}
        }
        private NativeSeat SanitizedSeat(int id,NativeProfile profile,string token)
        {
            var access=NativeAccess.Verify(token,s=>JsonUtility.FromJson<NativePlayerClaim>(s),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());var c=profile?.cosmetics??new NativeCosmetics();string spec=c.preferredSpec;
            if(!content.weapons.Any(w=>w.id==spec&&(!w.devOnly||access.Allows("weapon:"+spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))))spec="pistol";
            var seat=new NativeSeat{id=id,name=NativeCosmeticRules.Name(profile?.name),spec=spec,team=id-1,access=access,bodyColor=NativeCosmeticRules.Color(c.bodyColor,"#e5e7eb"),accentColor=NativeCosmeticRules.Color(profile?.color,"#70e1d0"),hat=NativeCosmeticRules.Hat(c.hat,access),killEffect=NativeCosmeticRules.Kill(c.killEffect,access),intro=NativeCosmeticRules.Intro(c.intro,access),introText=NativeCosmeticRules.IntroText(c.introText),overrideIntro=c.overrideWeaponIntro};return seat;
        }
        private void LobbyBroadcast()
        {
            var list=new List<NativeSeatFrame>();if(!dedicated)list.Add(new NativeSeatFrame{id=1,name=prefs.name,spec=prefs.weapon,team=0,connected=true,ready=true});
            list.AddRange(peers.Where(p=>p.Seat!=null).Select(p=>new NativeSeatFrame{id=p.Seat.id,name=p.Seat.name,spec=p.Seat.spec,team=p.Seat.team,connected=p.Seat.connected,ready=p.Ready}));lobbySeats=list.ToArray();string json=JsonUtility.ToJson(new NativeNetMessage{type="lobby",seats=lobbySeats,epoch=networkEpoch});foreach(var p in peers)if(p.Seat!=null&&p.Connection?.Closed==false)p.Connection.Send(json);
        }
        private void ClientMessage(NativeNetMessage message)
        {
            if(message==null||message.version!=2)throw new ArgumentException();if(message.type=="ping")return;
            if(message.type=="error"){networkStatus=message.error?.Substring(0,Math.Min(message.error.Length,160))??"Room rejected the request.";return;}
            if(message.type=="welcome"){if(message.id<1||message.id>8||message.resume?.Length!=64)throw new ArgumentException();networkId=message.id;resumeToken=message.resume;networkEpoch=message.epoch;networkStatus="Connected · waiting for the host";return;}
            if(message.type=="lobby"){if(message.seats==null||message.seats.Length>8||message.seats.Any(s=>s==null||s.name==null||s.name.Length>40||!content.weapons.Any(w=>w.id==s.spec)))throw new ArgumentException();lobbySeats=message.seats;if(message.epoch!=networkEpoch){arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();menu=true;networkEpoch=message.epoch;}return;}
            if(message.type!="frame"||networkId<1)return;var f=message.frame;if(f==null||!f.Valid(content)||!f.seats.Any(s=>s.id==networkId))throw new ArgumentException();
            if(arena==null||!arena.Replica||message.epoch!=networkEpoch||arena.MapId!=f.map)
            {arena?.Dispose();match=new NativeMatch(content);arena=new NativeArena(content,match,f.map){Replica=true,LocalId=networkId};networkEpoch=message.epoch;presentation?.ResetPresentation();prefs.map=f.map;NativeLighting.Apply(content.Map(f.map),cameraView);prefs.pve=f.cooperative;menu=false;visualClock.Reset(f.time);}
            arena.ApplyFrame(f,networkId);generation=arena.Generation;if(inputSequence==0){yaw=arena.Local.Yaw;pitch=12;}networkStatus="Connected";
        }
        private void NetworkPanel()
        {
            GUILayout.Space(16);GUILayout.Label("Multiplayer · Unity conversion",heading);GUILayout.Label(networkStatus,small);RelayPanel();
            if(!Online){GUILayout.BeginHorizontal();GUILayout.Label("Host address",small);hostAddress=GUILayout.TextField(hostAddress,253);GUILayout.Label("Port",small);portText=GUILayout.TextField(portText,5,GUILayout.Width(75));GUILayout.EndHorizontal();GUILayout.BeginHorizontal();GUILayout.Label("Room code",small);joinCode=GUILayout.TextField(joinCode,8);GUILayout.EndHorizontal();GUILayout.BeginHorizontal();if(!internetRooms&&GUILayout.Button("Host LAN room",button))HostRoom();GUI.enabled=connecting==null;if(GUILayout.Button("Join host",button))JoinRoom();GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.Label("Guests need the same conversion build, the host’s address and room code.",small);}
            else
            {
                if(Hosting)GUILayout.Label("Room "+roomCode+(listener!=null?" · port "+listener.Port:" · internet relay"),label);
                foreach(var s in lobbySeats)GUILayout.Label(s.name+" · "+s.spec+" · team "+(s.team+1)+" · "+(!s.connected?"disconnected":s.ready?"ready":"choosing"),small);
                if(!Hosting&&arena==null){if(GUILayout.Button(networkReady?"Not ready":"Ready",button)){networkReady=!networkReady;SendServer(new NativeNetMessage{type="ready",ready=networkReady});}if(GUILayout.Button("Send my loadout",button))SendServer(new NativeNetMessage{type="profile",profile=Profile(),token=account?.Token});}
                if(Hosting&&arena!=null&&GUILayout.Button("Return room to lobby",button)){ClearReplay();arena.Dispose();arena=null;match=null;presentation?.ResetPresentation();networkEpoch++;foreach(var p in peers)p.Ready=false;LobbyBroadcast();}
                if(GUILayout.Button("Leave room",button)){ClearReplay();CloseNetwork();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();}
            }
        }
        private void ChooseChip(int seat,string chip){if(server!=null)SendServer(new NativeNetMessage{type="chip",chip=chip});else match.Choose(seat,chip);nativeAudio.Play("install",null,.6f);}
        private void ChoosePveChip(int pick,string chip){if(server!=null)SendServer(new NativeNetMessage{type="pve-pick",pick=pick,chip=chip});else arena.ChoosePve(arena.Local,pick,chip);nativeAudio.Play("install",null,.6f);}
        private void PveOpen(bool open){if(server!=null)SendServer(new NativeNetMessage{type=open?"pve-open":"pve-close"});else if(open)arena.OpenPvePick(arena.Local);else arena.ClosePvePick(arena.Local);}
    }
}
