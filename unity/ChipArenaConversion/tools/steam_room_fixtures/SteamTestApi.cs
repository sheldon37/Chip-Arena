// Offline callback fixture only. check_unity_compile.py separately compiles all
// three runtime assemblies with the tagged upstream Steamworks.NET source.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Steamworks
{
    public readonly record struct CSteamID(ulong Value)
    {
        public bool IsValid() => Value > 0;
        public bool IsLobby() => Value >= 1000;
        public override string ToString() => Value.ToString();
        public static explicit operator ulong(CSteamID id) => id.Value;
        public static CSteamID Nil => default;
    }
    public readonly record struct HSteamListenSocket(int N) { public static HSteamListenSocket Invalid => default; }
    public readonly record struct HSteamNetConnection(int N) { public static HSteamNetConnection Invalid => default; }
    public readonly record struct SteamAPICall_t(int N) { public static SteamAPICall_t Invalid => default; }
    public enum EResult { k_EResultOK, k_EResultLimitExceeded }
    public enum ELobbyType { k_ELobbyTypePrivate, k_ELobbyTypeFriendsOnly, k_ELobbyTypePublic }
    public enum ELobbyDistanceFilter { k_ELobbyDistanceFilterWorldwide }
    public enum ELobbyComparison { k_ELobbyComparisonEqual }
    public enum EChatRoomEnterResponse { k_EChatRoomEnterResponseSuccess = 1 }
    [Flags] public enum EChatMemberStateChange { k_EChatMemberStateChangeLeft=2,k_EChatMemberStateChangeDisconnected=4,k_EChatMemberStateChangeKicked=8,k_EChatMemberStateChangeBanned=16 }
    public enum ESteamNetworkingConnectionState { k_ESteamNetworkingConnectionState_Connecting, k_ESteamNetworkingConnectionState_Connected, k_ESteamNetworkingConnectionState_ClosedByPeer, k_ESteamNetworkingConnectionState_ProblemDetectedLocally }
    public struct SteamNetworkingIdentity
    { private CSteamID id; public void SetSteamID(CSteamID value)=>id=value; public ulong GetSteamID64()=>(ulong)id; }
    public struct Info { public SteamNetworkingIdentity m_identityRemote; public HSteamListenSocket m_hListenSocket; public ESteamNetworkingConnectionState m_eState; public int m_eEndReason; }
    public struct SteamNetConnectionStatusChangedCallback_t { public Info m_info; public HSteamNetConnection m_hConn; }
    public struct GameLobbyJoinRequested_t { public CSteamID m_steamIDLobby; }
    public struct GameRichPresenceJoinRequested_t { public string m_rgchConnect; }
    public struct LobbyChatUpdate_t { public ulong m_ulSteamIDLobby,m_ulSteamIDUserChanged; public uint m_rgfChatMemberStateChange; }
    public struct LobbyCreated_t { public ulong m_ulSteamIDLobby; public EResult m_eResult; }
    public struct LobbyEnter_t { public ulong m_ulSteamIDLobby; public uint m_EChatRoomEnterResponse; }
    public struct LobbyMatchList_t { public uint m_nLobbiesMatching; }
    public sealed class Callback<T> : IDisposable
    {
        private static event Action<T> Handlers;
        private readonly Action<T> handler;
        private Callback(Action<T> action) { handler=action; Handlers+=action; }
        public static Callback<T> Create(Action<T> action)=>new Callback<T>(action);
        public static void Fire(T value)=>Handlers?.Invoke(value);
        public void Dispose()=>Handlers-=handler;
    }
    public sealed class CallResult<T> : IDisposable
    {
        private static readonly Dictionary<SteamAPICall_t, Action<T,bool>> Waiting=new();
        private SteamAPICall_t call;
        private Action<T,bool> handler;
        public static CallResult<T> Create(Action<T,bool> action)=>new CallResult<T>{handler=action};
        public void Set(SteamAPICall_t id) { call=id; Waiting.Add(call,handler); }
        public static void Fire(SteamAPICall_t id,T value)=>Waiting[id](value,false);
        public void Dispose()=>Waiting.Remove(call);
    }
    public static class SteamAPI
    { public static bool Init()=>true; public static void RunCallbacks(){} public static void Shutdown(){} }
    public static class SteamUser
    { public static CSteamID Me=new(1); public static bool BLoggedOn()=>true; public static CSteamID GetSteamID()=>Me; }
    public readonly record struct AppId_t(uint m_AppId);
    public readonly record struct CGameID(uint Value) { public AppId_t AppID()=>new(Value); }
    public struct FriendGameInfo_t { public CGameID m_gameID; public CSteamID m_steamIDLobby; }
    public struct LobbyDataUpdate_t { public ulong m_ulSteamIDMember,m_ulSteamIDLobby; public byte m_bSuccess; }
    public enum EFriendFlags { k_EFriendFlagImmediate }
    public static class SteamUtils
    { public static AppId_t GetAppID()=>new(7); public static string GetIPCountry()=>"US"; public static bool IsOverlayEnabled()=>true; }
    public static class SteamApps
    { public static string Launch=""; public static int GetLaunchCommandLine(out string s,int max){s=Launch;return s.Length;} }
    public static class SteamFriends
    {
        public static int GetFriendCount(EFriendFlags f)=>2;
        public static CSteamID GetFriendByIndex(int i,EFriendFlags f)=>new((ulong)(i+1));
        public static bool GetFriendGamePlayed(CSteamID friend,out FriendGameInfo_t game)
        { game=new(){m_gameID=new(7),m_steamIDLobby=new(1001)}; return true; }
        public static string Connect; public static CSteamID Overlay;
        public static string GetPersonaName()=>"Pilot";
        public static bool SetRichPresence(string k,string v){Connect=v;return true;}
        public static void ActivateGameOverlayInviteDialog(CSteamID id)=>Overlay=id;
    }
    public static class SteamMatchmaking
    {
        public static bool Open; public static int Limit,Joins; public static ELobbyType Privacy;
        public static CSteamID Owner;
        public static readonly List<CSteamID> Members=new(), Left=new(), Listed=new();
        public static readonly Dictionary<(CSteamID,string),string> Data=new();
        public static readonly Dictionary<string,string> Filters=new();
        private static int serial; public static SteamAPICall_t Last=>new(serial);
        public static SteamAPICall_t CreateLobby(ELobbyType p,int n){Privacy=p;Limit=n;return new(++serial);}
        public static SteamAPICall_t JoinLobby(CSteamID id){Joins++;return new(++serial);}
        public static void LeaveLobby(CSteamID id)=>Left.Add(id);
        public static CSteamID GetLobbyOwner(CSteamID id)=>Owner;
        public static int GetNumLobbyMembers(CSteamID id)=>Members.Count;
        public static CSteamID GetLobbyMemberByIndex(CSteamID id,int i)=>Members[i];
        public static bool SetLobbyMemberLimit(CSteamID id,int n){Limit=n;return true;}
        public static bool SetLobbyJoinable(CSteamID id,bool v){Open=v;return true;}
        public static bool SetLobbyType(CSteamID id,ELobbyType t){Privacy=t;return true;}
        public static bool SetLobbyData(CSteamID id,string k,string s){Data[(id,k)]=s;return true;}
        public static string GetLobbyData(CSteamID id,string k)=>Data.GetValueOrDefault((id,k),"");
        public static void AddRequestLobbyListStringFilter(string k,string v,ELobbyComparison c)=>Filters[k]=v;
        public static void AddRequestLobbyListFilterSlotsAvailable(int n)=>Filters["slots"]=n.ToString();
        public static void AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter d){}
        public static void AddRequestLobbyListResultCountFilter(int n){}
        public static bool RequestLobbyData(CSteamID lobby)=>true;
        public static SteamAPICall_t RequestLobbyList()=>new(++serial);
        public static CSteamID GetLobbyByIndex(int i)=>Listed[i];
    }
    public static class SteamNetworkingUtils
    {
        public static void InitRelayNetworkAccess(){}
        public static string GetRelayNetworkStatus(out int details){details=0;return "fixture";}
        public static float GetLocalPingLocation(out int p){p=1;return 1;}
        public static void ConvertPingLocationToString(ref int p,out string s,int n)=>s="test-location";
        public static bool ParsePingLocationString(string s,out int p){p=1;return s=="test-location";}
        public static int EstimatePingTimeFromLocalHost(ref int p)=>27;
    }
    public static class SteamNetworkingSockets
    {
        public static HSteamNetConnection Dial; public static int Accepted;
        public static readonly List<HSteamNetConnection> Closed=new();
        public static HSteamListenSocket CreateListenSocketP2P(int port,int n,object options)=>new(1);
        public static bool CloseListenSocket(HSteamListenSocket h)=>true;
        public static HSteamNetConnection ConnectP2P(ref SteamNetworkingIdentity id,int port,int n,object options)=>Dial=new(Dial.N+1);
        public static EResult AcceptConnection(HSteamNetConnection h){Accepted++;return EResult.k_EResultOK;}
        public static bool CloseConnection(HSteamNetConnection h,int code,string reason,bool linger){Closed.Add(h);return true;}
        public static EResult SendMessageToConnection(HSteamNetConnection h,IntPtr data,uint count,int flags,out long serial){serial=1;return EResult.k_EResultOK;}
        public static int ReceiveMessagesOnConnection(HSteamNetConnection h,IntPtr[] msgs,int count)=>0;
        public static string GetAuthenticationStatus(out int details){details=0;return "fixture";}
    }
    public struct SteamNetworkingMessage_t
    {
        public int m_cbSize; public IntPtr m_pData;
        public static SteamNetworkingMessage_t FromIntPtr(IntPtr p)=>throw new InvalidOperationException();
        public static void Release(IntPtr p)=>throw new InvalidOperationException();
    }
    public static class Constants { public const int k_cchMaxSteamNetworkingPingLocationString=1024,k_nSteamNetworkingSend_ReliableNoNagle=9; }
}
