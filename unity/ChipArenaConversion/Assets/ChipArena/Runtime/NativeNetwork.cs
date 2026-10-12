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
        public int version=NativeConnection.Protocol,id,epoch,generation,pick,team;public string type,source,contentRevision,room,resume,token,chip,error;public bool ready;
        [NonSerialized] public NativeRoomOptions options;public NativeProfile profile;public InputFrame input;[NonSerialized] public NativeGameFrame frame;public NativeSeatFrame[] seats;
    }
    internal sealed class NativePeer
    {public NativeConnection Connection;public NativeSeat Seat;public string Resume;public bool Ready;public readonly NativeMessageBudget Changes=new NativeMessageBudget();public DateTime Arrived=DateTime.UtcNow,Disconnected;}
    public sealed partial class ConversionGame
    {
        private NativeLanDiscovery lanDiscovery;private NativeListener listener;private NativeConnection server;private readonly List<NativePeer> peers=new List<NativePeer>();
        private Task<NativeConnection> connecting;private CancellationTokenSource connectStop;private string hostAddress="",portText="29941",roomCode="",joinCode="",resumeToken="",networkStatus="";private int networkId=1,networkEpoch,inputSequence;private float nextSnapshot,nextPing;private bool networkReady;private NativeSeatFrame[] lobbySeats=Array.Empty<NativeSeatFrame>();
        private readonly NativeFrameOrder frameOrder=new NativeFrameOrder();private bool lobbyDirty;private float nextLobby;
        public NativeConnectionReport ConnectionMetrics=>server?.Metrics.Report(!server.Closed)??new NativeConnectionReport();
        public string ConnectionReportJson()=>JsonUtility.ToJson(ConnectionMetrics,true);
        public NativeConnectionReport[] PeerMetrics=>peers.Where(p=>p.Seat!=null&&p.Connection!=null).Select(p=>{var report=p.Connection.Metrics.Report(!p.Connection.Closed);report.seatId=p.Seat.id;report.name=p.Seat.name;return report;}).ToArray();
        public string[] LanAddresses=>NativeLanDiscovery.Addresses();public string RoomCode=>relayHost!=null?NativeRoomCodes.Online(roomCode):roomCode;public string InviteUrl=>relayHost!=null?NativeRoomCodes.Invite(roomCode):listener==null?null:NativeLanDiscovery.Invite(LanAddresses.FirstOrDefault()??"127.0.0.1",listener.Port,roomCode);
        public bool JoinInvite(string invite){if(NativeRoomCodes.ParseInvite(invite,out var onlineCode)){internetRooms=true;joinCode=onlineCode;JoinRoom();return true;}if(!NativeLanDiscovery.ParseInvite(invite,out var address,out int port,out var code))return false;internetRooms=false;hostAddress=address;portText=port.ToString();joinCode=code;JoinRoom();return true;}
        private bool Online=>Hosting||server!=null;private bool Hosting=>listener!=null||relayHost!=null;
        // In a room: hosting, or a guest the host has welcomed (JoinRoom resets networkId to 0; welcome assigns 1-8).
        private bool InRoom=>Hosting||server!=null&&networkId>=1;private string joinError="";
        private static string RandomToken(int bytes){var data=new byte[bytes];using(var random=RandomNumberGenerator.Create())random.GetBytes(data);return BitConverter.ToString(data).Replace("-","").ToLowerInvariant();}
        private NativeProfile Profile()=>new NativeProfile{name=prefs.name,@class=content.Weapon(prefs.weapon).@class,color=prefs.accentColor,cosmetics=new NativeCosmetics{lobbyPose=prefs.lobbyPose,bodyColor=prefs.bodyColor,hat=prefs.hat,killEffect=prefs.killEffect,preferredSpec=prefs.weapon,intro=prefs.intro,introText=prefs.introText,overrideWeaponIntro=prefs.overrideIntro}};
        private void HostRoom()
        {
            Time.timeScale=1;EndPreview();
            try{if(!int.TryParse(portText,out int port)||port<1||port>65535)throw new ArgumentException("Choose a port between 1 and 65535.");CloseNetwork();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();listener=new NativeListener(port);roomCode=NativeRoomCodes.Create();try{lanDiscovery=new NativeLanDiscovery(roomCode,content.sourceCommit,port);}catch(System.Net.Sockets.SocketException){networkStatus="LAN discovery unavailable; join by address.";}LobbyBroadcast();networkStatus="Hosting · "+string.Join(", ",LanAddresses.Select(a=>a+":"+port))+" · Room "+roomCode;menu=true;}
            catch(Exception e)when(e is System.Net.Sockets.SocketException||e is ArgumentException){networkStatus=e.Message;}
        }
        private void JoinRoom()
        {
            Time.timeScale=1;EndPreview();
            if(connecting!=null||relayConnecting!=null)return;if((joinCode??"").Trim().StartsWith("CA-",StringComparison.OrdinalIgnoreCase)&&NativeRoomCodes.TryNormalize(joinCode.Trim().Substring(3),out var legacyCode)&&legacyCode.Length==8)internetRooms=true;try{joinCode=NativeRoomCodes.RawOnline(joinCode);}catch(ArgumentException e){networkStatus=e.Message;return;}int port=0;if(!internetRooms&&(!int.TryParse(portText,out port)||port<1||port>65535)){networkStatus="Invalid port.";return;}joinError="";
            server?.Dispose();server=null;receivedOptions=null;lobbySeats=Array.Empty<NativeSeatFrame>();prefs.pve=false;networkReady=false;networkId=0;frameOrder.Reset();connectStop?.Cancel();connectStop=new CancellationTokenSource();try{connecting=internetRooms?JoinOnlineRoom(joinCode,connectStop.Token):string.IsNullOrWhiteSpace(hostAddress)?NativeLanDiscovery.Connect(joinCode.Trim().ToUpperInvariant(),content.sourceCommit,connectStop.Token):NativeConnection.Connect(hostAddress.Trim(),port,connectStop.Token);}catch(ArgumentException e){networkStatus=e.Message;return;}networkStatus="Connecting…";
        }
        private void CloseNetwork()
        {listStop?.Cancel();relayRooms=Array.Empty<NativeRoomListing>();if(receivedOptions!=null)prefs.pve=false;receivedOptions=null;lanDiscovery?.Dispose();lanDiscovery=null;connectStop?.Cancel();if(connecting!=null){var abandoned=connecting;_=abandoned.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});}connecting=null;if(relayConnecting!=null){var abandoned=relayConnecting;_=abandoned.ContinueWith(t=>{if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});}relayConnecting=null;relayHost?.Dispose();relayHost=null;listener?.Dispose();listener=null;server?.Dispose();server=null;foreach(var peer in peers)peer.Connection?.Dispose();peers.Clear();lobbySeats=Array.Empty<NativeSeatFrame>();networkReady=false;networkId=1;resumeToken="";}
        private void SendServer(NativeNetMessage message){message.epoch=networkEpoch;message.generation=arena?.Generation??0;server?.Send(NativeNetCodec.Encode(message));}
        private void NetworkTick()
        {
            RelayTick();LobbyTick();EndFlowTick();
            if(connecting?.IsCompleted==true)
            {
                if(connecting.IsFaulted||connecting.IsCanceled)networkStatus=internetRooms?OnlineError(connecting.Exception,"Could not join. Check the code, keep the host’s room open, and make sure both games are updated."):"Could not find this local room. Check that both players are on the same network.";
                else{server=connecting.Result;SendServer(new NativeNetMessage{type="hello",source=content.sourceCommit,contentRevision=content.contentRevision,room=joinCode.Trim().ToUpperInvariant(),resume=resumeToken,token=account?.Token,profile=Profile()});networkStatus="Joining room…";}connecting=null;
            }
            if(Hosting)
            {
                var accepted=listener?.Accepted??relayHost.Accepted;while(accepted.TryDequeue(out var c)){if(peers.Count(p=>p.Connection!=null&&!p.Connection.Closed)>=15)c.Dispose();else peers.Add(new NativePeer{Connection=c});}
                foreach(var peer in peers.ToArray())
                {
                    var connection=peer.Connection;if(connection==null)continue;
                    if(connection.Closed||(DateTime.UtcNow-connection.LastReceived).TotalSeconds>12||peer.Seat==null&&(DateTime.UtcNow-peer.Arrived).TotalSeconds>5)
                    {connection.Dispose();peer.Connection=null;peer.Disconnected=DateTime.UtcNow;if(peer.Seat!=null){peer.Seat.connected=false;arena?.SetPilotConnected(peer.Seat.id,false);LobbyBroadcast();}else peers.Remove(peer);continue;}
                    int count=0;while(count++<120&&connection.TryReceive(out string json))try{var m=NativeNetCodec.Decode(json);if(m==null||m.version!=NativeConnection.Protocol)throw new ArgumentException();HostMessage(peer,m);}catch(Exception){connection.Dispose();networkStatus="A peer sent an invalid message.";break;}
                }
                foreach(var peer in peers.Where(p=>p.Connection==null&&(DateTime.UtcNow-p.Disconnected).TotalSeconds>90&&arena==null).ToArray())peers.Remove(peer);
                if(arena!=null&&Time.unscaledTime>=nextSnapshot){nextSnapshot=Time.unscaledTime+.05f;try{var frame=arena.NetworkFrame();frame.rematchVotes=endFlow.Voters;frame.rematchRequired=endFlow.Required;frame.lobbyReturnSeconds=LobbyReturnSeconds;var packet=NativeFrameBudget.Encode(frame,networkEpoch);if(packet!=null){foreach(var peer in peers)if(peer.Seat!=null&&match.Seat(peer.Seat.id)!=null&&peer.Connection?.Closed==false)peer.Connection.SendEncoded(packet,true);}else networkStatus="Snapshot exceeded its presentation budget.";}catch(Exception){networkStatus="Could not encode the current match snapshot.";}}
            }
            if(lobbyDirty&&Hosting&&Time.unscaledTime>=nextLobby){lobbyDirty=false;nextLobby=Time.unscaledTime+.2f;FlushLobby();}
            if(server!=null)
            {
                int count=0;while(count++<64&&server.TryReceive(out string json))try{var m=NativeNetCodec.Decode(json);ClientMessage(m);}catch(Exception){server.Dispose();networkStatus="Invalid or incompatible host state.";break;}
                if(server.Closed||(DateTime.UtcNow-server.LastReceived).TotalSeconds>12){networkStatus=joinError.Length>0?joinError:"Disconnected. Rejoin this host within 90 seconds to reclaim your seat.";joinError="";server.Dispose();server=null;menu=true;pending=new InputFrame();}
            }
            if(Online&&Time.unscaledTime>=nextPing){nextPing=Time.unscaledTime+1;void Probe(NativeConnection c){if(c==null||c.Closed)return;c.Send(NativeNetCodec.Encode(new NativeNetMessage{type="ping",id=c.Metrics.Ping(NativeConnectionMetrics.Now)}));}Probe(server);foreach(var p in peers)Probe(p.Connection);}
            if(arena?.Replica==true)arena.InterpolateReplica(Time.unscaledDeltaTime);
        }
        private void HostMessage(NativePeer peer,NativeNetMessage message)
        {
            var c=peer.Connection;if(message.type=="ping"){c.Send(NativeNetCodec.Encode(new NativeNetMessage{type="pong",id=message.id}));return;}if(message.type=="pong"){c.Metrics.Pong(message.id,NativeConnectionMetrics.Now);return;}
            if(peer.Seat==null)
            {
                if(message.type!="hello"||!NativeMapRules.Compatible(message.version,message.source,message.contentRevision,content)||message.room!=roomCode){c.Reject(NativeNetCodec.Encode(new NativeNetMessage{type="error",error="Room code or conversion version does not match."}));return;}
                NativePeer previous=!string.IsNullOrEmpty(message.resume)?peers.Find(p=>p!=peer&&p.Resume==message.resume&&(p.Connection!=null||(DateTime.UtcNow-p.Disconnected).TotalSeconds<=90)):null;
                if(previous!=null&&arena!=null&&match.Seat(previous.Seat.id)==null){c.Send(NativeNetCodec.Encode(new NativeNetMessage{type="error",error="This match started without your seat. Join after the host returns to the lobby."}));return;}
                if(previous!=null){previous.Connection?.Dispose();peer.Seat=previous.Seat;peer.Resume=previous.Resume;peer.Ready=previous.Ready;peer.Seat.connected=true;peers.Remove(previous);arena?.SetPilotConnected(peer.Seat.id,true);}
                else
                {
                    if(arena!=null||peers.Count(p=>p.Seat!=null)>=(prefs.pve?dedicated?4:3:dedicated?8:7)){c.Send(NativeNetCodec.Encode(new NativeNetMessage{type="error",error="The room is full or the match has started."}));return;}
                    int id=dedicated?1:2;while(peers.Any(p=>p.Seat?.id==id))id++;peer.Seat=SanitizedSeat(id,message.profile,message.token);peer.Resume=RandomToken(32);
                }
                c.Send(NativeNetCodec.Encode(new NativeNetMessage{type="welcome",source=content.sourceCommit,contentRevision=content.contentRevision,id=peer.Seat.id,resume=peer.Resume,epoch=networkEpoch}));LobbyBroadcast();return;
            }
            var seat=peer.Seat;
            if((message.type=="ready"||message.type=="profile"||message.type=="team"||message.type=="auth-refresh"||message.type=="rematch")&&!peer.Changes.Allow(Time.unscaledTime))return;
            if(message.type=="auth-refresh"){var access=NativeAccess.Verify(message.token,s=>JsonUtility.FromJson<NativePlayerClaim>(s),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());if(!string.IsNullOrEmpty(access.AccountId)&&access.AccountId==seat.access.AccountId)seat.access=access;return;}
            if(message.type=="ready"&&arena==null){var clean=SanitizedSeat(seat.id,message.profile,message.token);clean.team=seat.team;peer.Seat=clean;peer.Ready=message.ready;LobbyBroadcast();return;}
            if(message.type=="profile"&&arena==null){var clean=SanitizedSeat(seat.id,message.profile,message.token);clean.team=seat.team;peer.Seat=clean;peer.Ready=false;LobbyBroadcast();return;}
            if(message.type=="team"&&arena==null){seat.team=prefs.pve?0:Math.Max(0,Math.Min(7,message.team));peer.Ready=false;LobbyBroadcast();return;}
            if(arena==null||message.epoch!=networkEpoch||message.generation!=arena.Generation)return;
            if(message.type=="input"){arena.AcceptInput(seat.id,message.input);return;}var pilot=arena.Pilots.Find(p=>p.Seat.id==seat.id);if(pilot==null)return;
            if(message.type=="chip"&&message.chip?.Length<=64)match.Choose(seat.id,message.chip);
            else if(message.type=="pve-pick"&&message.chip?.Length<=64)arena.ChoosePve(pilot,message.pick,message.chip);
            else if(message.type=="pve-open")arena.OpenPvePick(pilot);
            else if(message.type=="pve-close")arena.ClosePvePick(pilot);
            else if(message.type=="rematch"&&match.Phase==NativePhase.Match){endFlow.Reconcile(match.Seats.Where(s=>!s.bot&&s.connected).Select(s=>s.id));endFlow.Vote(seat.id);}
        }
        private NativeSeat SanitizedSeat(int id,NativeProfile profile,string token)
        {
            var access=NativeAccess.Verify(token,s=>JsonUtility.FromJson<NativePlayerClaim>(s),DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());var c=profile?.cosmetics??new NativeCosmetics();string spec=c.preferredSpec;
            if(prefs.pve&&spec=="siren")spec="pistol";if(!content.weapons.Any(w=>w.id==spec&&(!w.devOnly||access.Allows("weapon:"+spec,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))))spec="pistol";
            var seat=new NativeSeat{id=id,name=NativeCosmeticRules.Name(profile?.name),spec=spec,team=NativeLobbyRules.DefaultTeam(id,prefs.pve),access=access,bodyColor=NativeCosmeticRules.Color(c.bodyColor,"#e5e7eb"),accentColor=NativeCosmeticRules.Color(profile?.color,"#70e1d0"),hat=NativeCosmeticRules.Hat(c.hat,access),lobbyPose=NativeLobbyPoses.Normalize(c.lobbyPose),killEffect=NativeCosmeticRules.Kill(c.killEffect,access),intro=NativeCosmeticRules.Intro(c.intro,access),introText=NativeCosmeticRules.IntroText(c.introText),overrideIntro=NativeCosmeticRules.OverrideIntro(c.intro,c.overrideWeaponIntro,access)};return seat;
        }
        private void LobbyBroadcast(){lobbyDirty=true;}
        private void FlushLobby()
        {
            var list=new List<NativeSeatFrame>();if(!dedicated){var local=SanitizedSeat(1,Profile(),account?.Token);local.team=prefs.pve?0:prefs.team;list.Add(LobbySeat(local,true));}
            list.AddRange(peers.Where(p=>p.Seat!=null&&p.Seat.connected).Select(p=>LobbySeat(p.Seat,p.Ready)));
            if(arena==null)list.AddRange(LobbyBots(list.Count(s=>s.connected)).Select(s=>LobbySeat(s,true)));
            lobbySeats=list.ToArray();string json=NativeNetCodec.Encode(new NativeNetMessage{type="lobby",seats=lobbySeats,options=CaptureRoomOptions(),epoch=networkEpoch});
            foreach(var p in peers)if(p.Seat!=null&&p.Connection?.Closed==false){if(arena==null)p.Connection.ClearSnapshot();p.Connection.Send(json);}

        }
        private void ClientMessage(NativeNetMessage message)
        {
            if(message==null||message.version!=NativeConnection.Protocol)throw new ArgumentException();if(message.type=="ping"){server.Send(NativeNetCodec.Encode(new NativeNetMessage{type="pong",id=message.id}));return;}if(message.type=="pong"){server.Metrics.Pong(message.id,NativeConnectionMetrics.Now);return;}
            if(message.type=="error"){networkStatus=message.error?.Substring(0,Math.Min(message.error.Length,160))??"Room rejected the request.";if(networkId<1){joinError=networkStatus;server.Dispose();}return;}
            if(message.type=="welcome"){if(!NativeMapRules.Compatible(message.version,message.source,message.contentRevision,content)||message.id<1||message.id>8||message.resume?.Length!=64)throw new ArgumentException();networkId=message.id;resumeToken=message.resume;frameOrder.Reset();frameOrder.Control(message.epoch);networkEpoch=message.epoch;networkStatus="Connected · waiting for the host";return;}
            if(message.type=="lobby"){if(!frameOrder.Control(message.epoch))return;if(message.seats==null||message.seats.Length>8||message.seats.Any(s=>s==null||s.name==null||s.name.Length>40||!content.weapons.Any(w=>w.id==s.spec)))throw new ArgumentException();lobbySeats=message.seats;receivedOptions=message.options;if(receivedOptions!=null){receivedOptions.Normalize(content);prefs.pve=receivedOptions.cooperative;}networkReady=lobbySeats.FirstOrDefault(s=>s.id==networkId)?.ready??false;if(message.epoch!=networkEpoch){arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();menu=true;networkEpoch=message.epoch;}return;}
            if(message.type!="frame"||networkId<1||message.epoch<frameOrder.Epoch)return;var f=message.frame;if(f==null||!f.Valid(content)||!f.seats.Any(s=>s.id==networkId))throw new ArgumentException();
            if(!frameOrder.Frame(message.epoch,f.generation,f.time))return;
            if(arena==null||!arena.Replica||message.epoch!=networkEpoch||arena.MapId!=f.map)
            {arena?.Dispose();match=new NativeMatch(content);arena=new NativeArena(content,match,f.map){Replica=true,LocalId=networkId};networkEpoch=message.epoch;presentation?.ResetPresentation();prefs.map=f.map;NativeLighting.Apply(content.Map(f.map),cameraView);prefs.pve=f.cooperative;menu=false;visualClock.Reset(f.time);}
            server.Metrics.Snapshot(NativeConnectionMetrics.Now);guestVotes=f.rematchVotes?.Length??0;guestRequired=f.rematchRequired;guestReturnUntil=f.lobbyReturnSeconds>0?Time.unscaledTime+f.lobbyReturnSeconds:-1;arena.ApplyFrame(f,networkId);generation=arena.Generation;if(inputSequence==0){yaw=arena.Local.Yaw;pitch=12;}networkStatus="Connected";
        }
        // Create Room (main.ts:154 create screen): Unity's mode (Versus / Boss Test), YOUR CLASS, matchOptions() in its box,
        // the "Online room" check (here the conversion relay) with the port or relay URL it needs, then draftOptions,
        // introOptions and All players draft (NativeRoomOptionsUI.cs). A Boss Test room sets its party in the lobby; the
        // lobby can still change the match rules.
        private void CreateBody()
        {
            Fine("This computer hosts the match. Share your room code so friends can join from any network.");
            BeginRules(false,578);var o=rules;
            RuleSelect(7478,RoomOpt.Versus,"Mode",VersusLabels,o.cooperative?1:0);ClassRow("Your class",ClassCardStyle.Stacked);
            if(!o.cooperative)MatchOptions(RulesPart.All,false,false);
            RuleSelect(7482,RoomOpt.Online,"Connection",ConnectionLabels,internetRooms?0:1);RuleFine(7484,internetRooms?"Friends on any network can join with your room code.":"Only players on the same local network can join.",0,4);
            if(!internetRooms){Field("Port");portText=GUILayout.TextField(portText,5,GUILayout.Width(120));Fine("The lobby shows your LAN address and invite link once the room is open. Friends on the same LAN can join with just the code.");}
            else if(UseManagedRelay){listedRoom=GUILayout.Toggle(listedRoom,"Show in public room list");}
            if(!o.cooperative)DraftOptions();IntroOptions();
            if(!o.cooperative)RuleCheck(7486,RoomOpt.BothDraft,"All players draft after each set",o.bothDraft);else RuleFine(7488,"Party bots and where the run starts are set in the lobby.",0,6);
            RuleFine(7494,"Keep your game open while friends are in your room.",8,6);
        }
        // Join Room (main.ts:154 join screen): the ROOM CODE field, which takes 'CA-ABCD-2345 or room invite link' (JoinScreen
        // hands a pasted link to JoinInvite), Unity's connection with the host address and port (blank: find the code on
        // this LAN) or the relay and its room list, and YOUR CLASS.
        private void JoinBody()
        {
            BeginRules(false,578);
            RuleSelect(7490,RoomOpt.Connection,"Connection",ConnectionLabels,internetRooms?0:1);
            RuleField(7496,"ROOM CODE");JoinCodeField();
            if(!internetRooms)
            {
                Field("Host address");GUILayout.BeginHorizontal();hostAddress=GUILayout.TextField(hostAddress,253);GUILayout.Label("Port",small,GUILayout.Width(44));portText=GUILayout.TextField(portText,5,GUILayout.Width(90));GUILayout.EndHorizontal();
                Fine("Leave the address blank to find this code on the same LAN. If discovery is blocked, enter the host’s address. An invite link fills both.");
            }
            else if(UseManagedRelay)
            {
                GUI.enabled=relayConnecting==null&&connecting==null&&!listingRooms;
                if(GUILayout.Button(listingRooms?"Finding rooms…":"Browse public rooms",button))StartCoroutine(ListRelayRooms());
                foreach(var room in relayRooms)if(GUILayout.Button(room.name+" · "+room.players+" / "+room.capacity+" · "+room.code,button)){joinCode=room.code;JoinRoom();}GUI.enabled=true;
            }
            else Fine("Enter the six-character code from your host. They need to keep their room open while you play.");
            ClassRow("Your class",ClassCardStyle.Stacked);
        }
        // Leave Room: close the connection or room and drop any running match (the source's Leave Room / Return to Main Menu).
        private void LeaveRoom(){ClearReplay();CloseNetwork();arena?.Dispose();arena=null;match=null;presentation?.ResetPresentation();menu=true;Go(MenuScreen.Home);}
        private void ChooseChip(int seat,string chip){if(server!=null)SendServer(new NativeNetMessage{type="chip",chip=chip});else match.Choose(seat,chip);}
        private void ChoosePveChip(int pick,string chip){if(server!=null)SendServer(new NativeNetMessage{type="pve-pick",pick=pick,chip=chip});else arena.ChoosePve(arena.Local,pick,chip);}
        private void PveOpen(bool open){if(server!=null)SendServer(new NativeNetMessage{type=open?"pve-open":"pve-close"});else if(open)arena.OpenPvePick(arena.Local);else arena.ClosePvePick(arena.Local);}
    }
}
