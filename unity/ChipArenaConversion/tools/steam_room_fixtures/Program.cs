using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ChipArena.UnityConversion;
using Steamworks;

internal static class Program
{
    private static readonly CSteamID Lobby = new(1001);
    private static readonly CancellationToken Token = CancellationToken.None;
    private static int checks;
    private static void Check(bool value,string name)
    { if(!value) throw new Exception(name); checks++; }

    // Steam callbacks and Unity Task continuations execute on a single thread.
    private sealed class MainThread : SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> work = new();
        public override void Post(SendOrPostCallback f,object state) => work.Enqueue(()=>f(state));
        public void Until(Func<bool> done)
        {
            var watch = Stopwatch.StartNew();
            while(!done())
            {
                if(watch.ElapsedMilliseconds>4000) throw new TimeoutException("Fixture callback did not settle");
                if(work.TryDequeue(out var next)) next(); else Thread.Sleep(1);
            }
        }
        public T Get<T>(Task<T> task) { Until(()=>task.IsCompleted); return task.GetAwaiter().GetResult(); }
    }
    private static void Socket(HSteamNetConnection handle,bool incoming,ESteamNetworkingConnectionState state,ulong peer)
    {
        var remote=new SteamNetworkingIdentity(); remote.SetSteamID(new CSteamID(peer));
        Callback<SteamNetConnectionStatusChangedCallback_t>.Fire(new SteamNetConnectionStatusChangedCallback_t {
            m_hConn=handle,m_info=new Info {m_identityRemote=remote,m_eState=state,m_hListenSocket=incoming?new(1):default}
        });
    }
    private static void Main()
    {
        var thread=new MainThread(); SynchronizationContext.SetSynchronizationContext(thread);
        SteamApps.Launch="+connect_lobby 1009";
        using var rooms=SteamPlayerRooms.Open(SteamUtils.GetAppID().m_AppId);
        Check(rooms.PendingInvite=="1009","cold launch invite");
        Callback<GameLobbyJoinRequested_t>.Fire(new(){m_steamIDLobby=new(1008)});
        Check(rooms.PendingInvite=="1008","warm overlay invite");
        Callback<GameRichPresenceJoinRequested_t>.Fire(new(){m_rgchConnect="+connect_lobby 1007"});
        Check(rooms.PendingInvite=="1007","Steam friends Join Game");
        Callback<GameRichPresenceJoinRequested_t>.Fire(new(){m_rgchConnect="+connect_lobby invalid"});
        Check(rooms.PendingInvite=="1007","malformed launch ignored");

        SteamMatchmaking.Owner=SteamUser.Me; SteamMatchmaking.Members.AddRange(new[]{SteamUser.Me,new CSteamID(2)});
        var room=new PlayerRoomInfo {Name="Lobby", Host="Host", Players=1, Capacity=8,
            Mode="Team Deathmatch", Map="brickfall", Privacy=RoomPrivacy.Public};
        foreach(var privacy in new[]{RoomPrivacy.Private,RoomPrivacy.Friends,RoomPrivacy.Public})
        {
            room.Privacy=privacy;
            var create=rooms.Create(room,"build",Token);
            Check(SteamMatchmaking.Privacy==ELobbyType.k_ELobbyTypePrivate,"no early listing");
            CallResult<LobbyCreated_t>.Fire(SteamMatchmaking.Last,new(){m_ulSteamIDLobby=(ulong)Lobby});
            var host=thread.Get(create);
            Check(host.Room==Lobby.ToString()&&SteamMatchmaking.Open,"ready room");
            Check((int)SteamMatchmaking.Privacy==(int)privacy,"visibility");
            Check((SteamFriends.Connect!=null)==(privacy!=RoomPrivacy.Private),"friend join respects privacy");
            Check(rooms.Invite()&&SteamFriends.Overlay==Lobby,"invite existing room");
            var friendList=rooms.Browse("build",Token,true);
            Callback<LobbyDataUpdate_t>.Fire(new(){m_ulSteamIDLobby=(ulong)Lobby,m_ulSteamIDMember=(ulong)Lobby,m_bSuccess=1});
            Check(thread.Get(friendList).Length==(privacy==RoomPrivacy.Private?0:1),"fresh friends list deduplicates and excludes private rooms");
            if(privacy==RoomPrivacy.Public)
            {
                // Include a stale result returned by a simulated directory; the actual reader must discard it.
                SteamMatchmaking.Listed.AddRange(new[]{Lobby,new CSteamID(1002)});
                var browse=rooms.Browse("build",Token);
                CallResult<LobbyMatchList_t>.Fire(SteamMatchmaking.Last,new(){m_nLobbiesMatching=2});
                var rows=thread.Get(browse);
                Check(rows.Length==1 && rows[0].Matches("deathmatch") && rows[0].Matches("Brick") && !rows[0].Matches("expedition"),"browser rows and filters");
                Check(rows[0].EstimatedPing==27 && rows[0].Capacity==8 && rows[0].Players==1,"browser counts and estimate");
                Check(SteamMatchmaking.Filters["ca_open"]=="1"&&SteamMatchmaking.Filters["slots"]=="1"&&SteamMatchmaking.Filters["ca_compat"]=="build"&&SteamMatchmaking.Filters["ca_visibility"]=="Public","discovery query");
                foreach(var state in new[]{PlayerRoomState.InProgress,PlayerRoomState.Full,PlayerRoomState.Lobby})
                {
                    room.State=state; rooms.Publish(room);
                    Check(SteamMatchmaking.Open==(state==PlayerRoomState.Lobby) && (SteamFriends.Connect!=null)==SteamMatchmaking.Open,"status, presence and joinability");
                }
                Socket(new(51),true,ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting,99);
                Check(SteamNetworkingSockets.Closed.Contains(new(51))&&host.Accepted.IsEmpty,"non-member refused");
                Socket(new(52),true,ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connecting,2);
                Socket(new(52),true,ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected,2);
                Check(host.Accepted.TryDequeue(out var admitted)&&admitted.RemoteSteamId==2,"authenticated inbound socket");
                Callback<LobbyChatUpdate_t>.Fire(new(){m_ulSteamIDLobby=(ulong)Lobby,m_ulSteamIDUserChanged=2,
                    m_rgfChatMemberStateChange=(uint)EChatMemberStateChange.k_EChatMemberStateChangeKicked});
                thread.Until(()=>admitted.Closed);
                Check(SteamNetworkingSockets.Closed.Contains(new(52)),"departing member loses the game socket");
                admitted.Dispose();
            }
            host.Dispose();
            Check(host.Closed&&!SteamMatchmaking.Open&&SteamFriends.Connect==null&&SteamMatchmaking.Left.Last()==Lobby,"leave hides listing and clears presence");
        }

        using(var stop=new CancellationTokenSource())
        {
            var late=rooms.Create(room,"build",stop.Token); var handle=SteamMatchmaking.Last;
            stop.Cancel(); thread.Until(()=>late.IsCompleted);
            Check(late.IsCanceled,"cancel before creation completes");
            CallResult<LobbyCreated_t>.Fire(handle,new(){m_ulSteamIDLobby=1111});
            Check(SteamMatchmaking.Left.Last()==new CSteamID(1111),"late created lobby released");
        }
        SteamUser.Me=new(2); SteamMatchmaking.Owner=new(1);
        SteamMatchmaking.Data[(Lobby,"ca_open")]="1";
        var join=rooms.Join(Lobby.ToString(),"build",Token);
        CallResult<LobbyEnter_t>.Fire(SteamMatchmaking.Last,new(){m_ulSteamIDLobby=(ulong)Lobby,m_EChatRoomEnterResponse=1});
        thread.Until(()=>SteamNetworkingSockets.Dial.N!=0);
        Socket(SteamNetworkingSockets.Dial,false,ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected,1);
        var guest=thread.Get(join);
        Check(guest.RemoteSteamId==1,"guest connected to owner");
        var previous=SteamNetworkingSockets.Dial;
        Socket(previous,false,ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_ClosedByPeer,1);
        thread.Until(()=>guest.Closed);
        SteamMatchmaking.Data[(Lobby,"ca_open")]="0";
        int joins=SteamMatchmaking.Joins;
        var resume=rooms.Join(Lobby.ToString(),"build",Token);
        Check(joins==SteamMatchmaking.Joins&&previous!=SteamNetworkingSockets.Dial,"redial original host in a locked match without re-entering");
        Socket(SteamNetworkingSockets.Dial,false,ESteamNetworkingConnectionState.k_ESteamNetworkingConnectionState_Connected,1);
        var recovered=thread.Get(resume);
        SteamMatchmaking.Owner=SteamUser.Me; rooms.Tick();
        Check(rooms.EndReason!=null,"automatic lobby ownership transfer ends match");
        rooms.Leave(); thread.Until(()=>recovered.Closed);
        rooms.EndReason=null; SteamMatchmaking.Owner=new(1);
        var bad=rooms.Join(Lobby.ToString(),"other-build",Token);
        CallResult<LobbyEnter_t>.Fire(SteamMatchmaking.Last,new(){m_ulSteamIDLobby=(ulong)Lobby,m_EChatRoomEnterResponse=1});
        thread.Until(()=>bad.IsCompleted);
        Check(bad.IsFaulted&&bad.Exception.GetBaseException() is NativeOnlineRoomException,"incompatible invite rejected");
        using(var stop=new CancellationTokenSource())
        {
            int before=SteamMatchmaking.Left.Count;
            var waiting=rooms.Join(Lobby.ToString(),"build",stop.Token); var handle=SteamMatchmaking.Last;
            stop.Cancel();
            CallResult<LobbyEnter_t>.Fire(handle,new(){m_ulSteamIDLobby=(ulong)Lobby,m_EChatRoomEnterResponse=1});
            thread.Until(()=>waiting.IsCompleted);
            Check(waiting.IsCanceled&&SteamMatchmaking.Left.Count==before+1,"cancelled successful join leaves exactly once");
        }
        Console.WriteLine("PASS "+checks+" Steam adapter checks with simulated SDK callbacks");
    }
}
