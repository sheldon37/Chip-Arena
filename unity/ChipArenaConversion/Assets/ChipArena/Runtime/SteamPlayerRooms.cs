#if CHIP_ARENA_STEAM
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;

namespace ChipArena.UnityConversion
{
    // Matchmaking and callbacks run on Unity's main thread. Only the established socket
    // stream runs on NativeConnection's workers, including during Expedition scene loads.
    internal sealed partial class SteamPlayerRooms : ISteamPlayerRooms
    {
        private readonly List<IDisposable> registrations = new List<IDisposable>();
        private readonly HashSet<IDisposable> calls = new HashSet<IDisposable>();
        private readonly Dictionary<HSteamNetConnection,SteamRoomSocket> sockets = new Dictionary<HSteamNetConnection,SteamRoomSocket>();
        private readonly object io = new object();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private HSteamListenSocket listener;
        private HSteamNetConnection outgoing;
        private CSteamID lobby, owner, requested;
        private int generation;
        private Host host;
        private TaskCompletionSource<NativeConnection> dial;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long nextOwnerCheck;
        private string lastEvent = "initialized";
        public string PendingInvite { get; set; }
        public string EndReason { get; set; }
        public string HostName => SteamFriends.GetPersonaName();
        private long createMs = -1, joinMs = -1, browseMs = -1;
        public string Diagnostics => "Steam Networking Sockets · " + lastEvent +
            " · SDR " + SteamNetworkingUtils.GetRelayNetworkStatus(out _) +
            " · auth " + SteamNetworkingSockets.GetAuthenticationStatus(out _) +
            " · create/join/list ms " + createMs + "/" + joinMs + "/" + browseMs;

        internal static SteamPlayerRooms Open(uint expectedAppId)
        {
            if(!SteamAPI.Init()) throw Error("Steam initialization failed. Check the client, license and App ID.");
            var service = new SteamPlayerRooms();
            try
            {
                if(SteamUtils.GetAppID().m_AppId != expectedAppId || !SteamUser.BLoggedOn())
                    throw Error("Sign into Steam with the configured app running.");
                SteamNetworkingUtils.InitRelayNetworkAccess();
                service.registrations.Add(Callback<SteamNetConnectionStatusChangedCallback_t>.Create(service.ConnectionChanged));
                service.registrations.Add(Callback<GameLobbyJoinRequested_t>.Create(e => service.Invited(e.m_steamIDLobby)));
                service.registrations.Add(Callback<GameRichPresenceJoinRequested_t>.Create(e => service.ReadInvite((e.m_rgchConnect ?? "").Split(' '))));
                service.registrations.Add(Callback<LobbyChatUpdate_t>.Create(service.MembershipChanged));
                SteamApps.GetLaunchCommandLine(out var launch, 4096);
                service.ReadInvite((launch ?? "").Split(' '));
                service.ReadInvite(Environment.GetCommandLineArgs());
                return service;
            }
            catch { service.Dispose(); throw; }
        }
        private void ReadInvite(string[] args)
        {
            int index = Array.IndexOf(args, "+connect_lobby");
            if(index >= 0 && index + 1 < args.Length && PlayerRoomInfo.LobbyNumber(args[index+1], out var id))
                Invited(new CSteamID(id));
        }
        private void Invited(CSteamID id)
        { if(id.IsValid() && id.IsLobby() && id != lobby) PendingInvite = id.ToString(); }
        private static NativeOnlineRoomException Error(string message) => new NativeOnlineRoomException(message);

        public void Tick()
        {
            SteamAPI.RunCallbacks();
            if(clock.ElapsedMilliseconds < nextOwnerCheck) return;
            nextOwnerCheck = clock.ElapsedMilliseconds + 500;
            lock(io)
                foreach(var pair in sockets.Where(p => p.Value.Closed).ToArray()) sockets.Remove(pair.Key);
            if(lobby.IsValid() && owner.IsValid() && SteamUser.BLoggedOn())
            {
                var current = SteamMatchmaking.GetLobbyOwner(lobby);
                if(current != owner) EndReason = "Steam room ownership or membership changed. The host session ended.";
            }
        }

        // Steam can complete a create/join after the menu has cancelled it. Keep the
        // callback until completion so that a late success always releases that membership.
        private async Task<T> Result<T>(SteamAPICall_t handle, CancellationToken token, Action<T> late = null)
        {
            if(handle == SteamAPICall_t.Invalid) throw Error("Steam could not start the request.");
            var result = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            CallResult<T> cb = null;
            bool abandoned = false;
            cb = CallResult<T>.Create((value, failed) => {
                calls.Remove(cb); cb.Dispose();
                if(abandoned || token.IsCancellationRequested)
                { if(!failed) late?.Invoke(value); result.TrySetCanceled(); }
                else if(failed) result.TrySetException(Error("Steam request failed. Try again."));
                else result.TrySetResult(value);
            });
            calls.Add(cb); cb.Set(handle);
            using(var stop = CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token))
            {
                try
                {
                    if(await Task.WhenAny(result.Task, Task.Delay(20000, stop.Token)) == result.Task)
                        return await result.Task;
                    abandoned = true;
                    if(result.Task.Status == TaskStatus.RanToCompletion) late?.Invoke(result.Task.Result);
                    stop.Token.ThrowIfCancellationRequested();
                    throw Error("Steam request timed out.");
                }
                finally { stop.Cancel(); }
            }
        }
        private void ReleaseLate(ulong id,int operation)
        {
            var late = new CSteamID(id);
            if(!lifetime.IsCancellationRequested && late.IsValid() && late != lobby && (operation == generation || late != requested)) SteamMatchmaking.LeaveLobby(late);
        }
        private sealed class Host : INativeOnlineHost
        {
            private readonly SteamPlayerRooms service;
            public Host(SteamPlayerRooms service,string room) { this.service=service; Room=room; }
            public string Room { get; }
            public bool Closed { get; internal set; }
            public ConcurrentQueue<NativeConnection> Accepted { get; } = new ConcurrentQueue<NativeConnection>();
            public void Dispose() { if(!Closed) service.Leave(); }
        }

        public async Task<INativeOnlineHost> Create(PlayerRoomInfo room,string compatibility,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int operation = generation;
            long started = clock.ElapsedMilliseconds;
            // The lobby only becomes visible after the listener and metadata are ready.
            var created = await Result<LobbyCreated_t>(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePrivate,room.Capacity),
                token, e => ReleaseLate(e.m_ulSteamIDLobby,operation));
            if(created.m_eResult != EResult.k_EResultOK) throw Error("Steam lobby creation failed: " + created.m_eResult);
            if(operation != generation || token.IsCancellationRequested) { ReleaseLate(created.m_ulSteamIDLobby,operation); throw new OperationCanceledException(); }
            lobby = new CSteamID(created.m_ulSteamIDLobby); owner = SteamUser.GetSteamID();
            try
            {
                listener = SteamNetworkingSockets.CreateListenSocketP2P(0,0,null);
                if(listener == HSteamListenSocket.Invalid) throw Error("Could not open the Steam P2P listener.");
                host = new Host(this,lobby.ToString());
                if(!Set("compat",compatibility) || !Set("owner",owner.ToString()) || !PublishFields(room) ||
                    !SteamMatchmaking.SetLobbyType(lobby,Visibility(room.Privacy))) throw Error("Steam could not publish the room.");
                createMs = clock.ElapsedMilliseconds - started; lastEvent = "lobby ready"; return host;
            }
            catch { Leave(); throw; }
        }

        public async Task<NativeConnection> Join(string id,string compatibility,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if(!PlayerRoomInfo.LobbyNumber(id,out ulong number)) throw Error("Invalid Steam lobby ID.");
            var target = new CSteamID(number);
            if(!target.IsValid() || !target.IsLobby()) throw Error("Invalid Steam lobby ID.");
            bool resume = target == lobby && owner.IsValid() && host == null;
            if(!resume) Leave();
            int operation = generation;
            long started = clock.ElapsedMilliseconds;
            try
            {
                if(!resume)
                {
                    requested = target;
                    var enter = await Result<LobbyEnter_t>(SteamMatchmaking.JoinLobby(target),token,e => ReleaseLate(e.m_ulSteamIDLobby,operation));
                    if(enter.m_EChatRoomEnterResponse != (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                        throw Error("Steam lobby entry refused: " + (EChatRoomEnterResponse)enter.m_EChatRoomEnterResponse);
                    if(operation != generation || token.IsCancellationRequested)
                    { ReleaseLate(enter.m_ulSteamIDLobby,operation); throw new OperationCanceledException(); }
                    lobby = new CSteamID(enter.m_ulSteamIDLobby); requested = CSteamID.Nil;
                    owner = SteamMatchmaking.GetLobbyOwner(lobby);
                    if(lobby != target || !owner.IsValid() || owner == SteamUser.GetSteamID() ||
                        Read(lobby,"owner") != owner.ToString() || Read(lobby,"compat") != compatibility ||
                        Read(lobby,"open") != "1") throw Error("Steam room closed, full, or incompatible.");
                }
                if(SteamMatchmaking.GetLobbyOwner(lobby) != owner) throw Error("The original Steam host is no longer available.");
                var identity = new SteamNetworkingIdentity(); identity.SetSteamID(owner);
                dial = new TaskCompletionSource<NativeConnection>(TaskCreationOptions.RunContinuationsAsynchronously);
                var result = dial.Task;
                outgoing = SteamNetworkingSockets.ConnectP2P(ref identity,0,0,null);
                if(outgoing == HSteamNetConnection.Invalid) throw Error("Could not start the Steam connection.");
                using(var stop = CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token))
                {
                    try
                    {
                        if(await Task.WhenAny(result,Task.Delay(15000,stop.Token)) != result)
                        { stop.Token.ThrowIfCancellationRequested(); throw Error("Steam host connection timed out."); }
                        var connection = await result;
                        joinMs = clock.ElapsedMilliseconds - started; lastEvent = resume ? "socket reconnected" : "socket connected";
                        return connection;
                    }
                    finally { stop.Cancel(); }
                }
            }
            catch
            {
                if(operation == generation)
                {
                    if(!resume) Leave();
                    else CancelDial();
                }
                throw;
            }
        }

        private void MembershipChanged(LobbyChatUpdate_t e)
        {
            const EChatMemberStateChange departed = EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                EChatMemberStateChange.k_EChatMemberStateChangeDisconnected | EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                EChatMemberStateChange.k_EChatMemberStateChangeBanned;
            if(!lobby.IsValid() || e.m_ulSteamIDLobby != (ulong)lobby || (e.m_rgfChatMemberStateChange & (uint)departed) == 0) return;
            if(e.m_ulSteamIDUserChanged == (ulong)owner) EndReason = "The Steam host left the room.";
            else if(e.m_ulSteamIDUserChanged == (ulong)SteamUser.GetSteamID()) EndReason = "Steam lobby membership ended.";
            else if(host != null)
                foreach(var peer in sockets.Values.Where(p => p.Peer == e.m_ulSteamIDUserChanged).ToArray()) CloseHandle(peer.Handle);
        }
        private bool Member(ulong id)
        {
            if(id == 0 || id == (ulong)owner) return false;
            int count = SteamMatchmaking.GetNumLobbyMembers(lobby);
            for(int i=0; i<count; i++) if((ulong)SteamMatchmaking.GetLobbyMemberByIndex(lobby,i) == id) return true;
            return false;
        }
        private void ConnectionChanged(SteamNetConnectionStatusChangedCallback_t e)
        {
            var handle = e.m_hConn; var info = e.m_info;
            bool inbound = listener != HSteamListenSocket.Invalid && info.m_hListenSocket == listener && host != null;
            bool outbound = handle == outgoing && dial != null;
            var state = info.m_eState;
            if(state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting && inbound)
            {
                if(sockets.Count < 16 && Member(info.m_identityRemote.GetSteamID64()) &&
                    SteamNetworkingSockets.AcceptConnection(handle) == EResult.k_EResultOK)
                    sockets[handle] = new SteamRoomSocket(handle,info.m_identityRemote.GetSteamID64(),io);
                else SteamNetworkingSockets.CloseConnection(handle,0,"Room admission refused",false);
                return;
            }
            if(state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected)
            {
                ulong peer = info.m_identityRemote.GetSteamID64();
                if(inbound && Member(peer) && sockets.TryGetValue(handle,out var accepted))
                    host.Accepted.Enqueue(new NativeConnection(new OrderedRoomStream(accepted),32768,peer));
                else if(outbound && peer == (ulong)owner && SteamMatchmaking.GetLobbyOwner(lobby) == owner)
                {
                    var socket = new SteamRoomSocket(handle,peer,io); sockets.Add(handle,socket);
                    dial.TrySetResult(new NativeConnection(new OrderedRoomStream(socket),NativeConnection.MaxFrame,peer));
                    dial = null; outgoing = HSteamNetConnection.Invalid;
                }
                else
                {
                    if(outbound) { dial.TrySetException(Error("Steam host identity changed.")); dial = null; outgoing = HSteamNetConnection.Invalid; }
                    CloseHandle(handle);
                }
            }
            else if(state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer ||
                state == ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ProblemDetectedLocally)
            {
                lastEvent = state + " (" + info.m_eEndReason + ")";
                if(outbound) { dial.TrySetException(Error("Steam connection ended: " + lastEvent)); dial = null; outgoing = HSteamNetConnection.Invalid; }
                CloseHandle(handle);
            }
        }
        private void CloseHandle(HSteamNetConnection handle)
        {
            if(sockets.TryGetValue(handle,out var socket)) { socket.Dispose(); sockets.Remove(handle); }
            else SteamNetworkingSockets.CloseConnection(handle,0,"Room closed",false);
        }
        private void CancelDial()
        {
            if(outgoing != HSteamNetConnection.Invalid) CloseHandle(outgoing);
            outgoing = HSteamNetConnection.Invalid; dial?.TrySetCanceled(); dial = null;
        }
        public void Leave()
        {
            generation++; requested = CSteamID.Nil;
            if(host != null) SteamFriends.SetRichPresence("connect",null);
            if(lobby.IsValid() && SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID())
            { SteamMatchmaking.SetLobbyJoinable(lobby,false); SteamMatchmaking.SetLobbyType(lobby,ELobbyType.k_ELobbyTypePrivate); Set("open","0"); }
            if(host != null) { host.Closed = true; while(host.Accepted.TryDequeue(out var c)) c.Dispose(); host = null; }
            CancelDial();
            foreach(var pair in sockets) pair.Value.Dispose();
            sockets.Clear();
            if(listener != HSteamListenSocket.Invalid) SteamNetworkingSockets.CloseListenSocket(listener);
            listener = HSteamListenSocket.Invalid;
            if(lobby.IsValid()) SteamMatchmaking.LeaveLobby(lobby);
            lobby = owner = CSteamID.Nil; ResetPublication();
        }
        public void Dispose()
        {
            if(lifetime.IsCancellationRequested) return;
            lifetime.Cancel(); Leave();
            foreach(var call in calls) call.Dispose();
            foreach(var cb in registrations) cb.Dispose();
            calls.Clear(); registrations.Clear(); SteamAPI.Shutdown();
        }
    }
}
#endif
