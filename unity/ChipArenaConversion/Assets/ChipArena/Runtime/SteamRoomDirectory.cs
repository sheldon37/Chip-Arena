#if CHIP_ARENA_STEAM
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Steamworks;

namespace ChipArena.UnityConversion
{
    internal sealed partial class SteamPlayerRooms
    {
        private readonly Dictionary<string,string> published = new Dictionary<string,string>();
        private int publishedCapacity;
        private bool? joinable;
        private string presence;
        private long nextLocation;
        private static string Read(CSteamID id,string key) => SteamMatchmaking.GetLobbyData(id,"ca_" + key);
        private bool Set(string key,string value)
        {
            value = value ?? "";
            if(published.TryGetValue(key,out var previous) && previous == value) return true;
            if(!SteamMatchmaking.SetLobbyData(lobby,"ca_" + key,value)) return false;
            published[key] = value; return true;
        }
        private static ELobbyType Visibility(RoomPrivacy privacy) => privacy == RoomPrivacy.Public ? ELobbyType.k_ELobbyTypePublic :
            privacy == RoomPrivacy.Friends ? ELobbyType.k_ELobbyTypeFriendsOnly : ELobbyType.k_ELobbyTypePrivate;
        private void ResetPublication() { published.Clear(); publishedCapacity = 0; joinable = null; presence = null; nextLocation = 0; }
        private bool SetOpen(bool value)
        {
            if(joinable == value) return true;
            if(!SteamMatchmaking.SetLobbyJoinable(lobby,value)) return false;
            joinable = value; return true;
        }
        private bool PublishFields(PlayerRoomInfo room)
        {
            if(!room.Joinable && !SetOpen(false)) return false;
            if(publishedCapacity != room.Capacity)
            {
                if(!SteamMatchmaking.SetLobbyMemberLimit(lobby,room.Capacity)) return false;
                publishedCapacity = room.Capacity;
            }
            if(!Set("visibility",room.Privacy.ToString()) || !Set("name",room.Name) || !Set("host",room.Host) ||
               !Set("mode",room.Mode) || !Set("map",room.Map) || !Set("players",room.Players.ToString(CultureInfo.InvariantCulture)) ||
               !Set("capacity",room.Capacity.ToString(CultureInfo.InvariantCulture)) || !Set("status",room.State.ToString()) ||
               !Set("region",room.Region) || !Set("open",room.Joinable ? "1" : "0")) return false;
            if(clock.ElapsedMilliseconds >= nextLocation)
            {
                nextLocation = clock.ElapsedMilliseconds + 30000;
                string location = "";
                float age = SteamNetworkingUtils.GetLocalPingLocation(out var local);
                if(age >= 0 && age <= 300) SteamNetworkingUtils.ConvertPingLocationToString(ref local,out location,Constants.k_cchMaxSteamNetworkingPingLocationString);
                Set("location",location);
            }
            if(!SetOpen(room.Joinable)) return false;
            string connect = room.Joinable && room.Privacy != RoomPrivacy.Private ? "+connect_lobby " + lobby : null;
            if(connect != presence && SteamFriends.SetRichPresence("connect",connect)) presence = connect;
            return true;
        }
        public void Publish(PlayerRoomInfo room)
        {
            if(host == null) return;
            if(!PublishFields(room))
            { SetOpen(false); lastEvent = "metadata update delayed"; }
        }
        public bool Invite()
        {
            if(!lobby.IsValid() || joinable != true || !SteamUtils.IsOverlayEnabled()) return false;
            SteamFriends.ActivateGameOverlayInviteDialog(lobby); return true;
        }
        private async Task<List<CSteamID>> FriendsLobbies(CancellationToken token)
        {
            var found = new List<CSteamID>();
            var pending = new HashSet<ulong>();
            var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using(var callback = Callback<LobbyDataUpdate_t>.Create(e => {
                if(e.m_ulSteamIDMember != e.m_ulSteamIDLobby || !pending.Remove(e.m_ulSteamIDLobby)) return;
                if(e.m_bSuccess != 0) found.Add(new CSteamID(e.m_ulSteamIDLobby));
                if(pending.Count == 0) finished.TrySetResult(true);
            }))
            using(var stop = CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token))
            {
                var flags = EFriendFlags.k_EFriendFlagImmediate;
                int count = SteamFriends.GetFriendCount(flags);
                for(int i=0; i<count && pending.Count<100; i++)
                {
                    var friend = SteamFriends.GetFriendByIndex(i,flags);
                    if(!SteamFriends.GetFriendGamePlayed(friend,out var game) || game.m_gameID.AppID() != SteamUtils.GetAppID() ||
                        !game.m_steamIDLobby.IsLobby() || !game.m_steamIDLobby.IsValid() || !pending.Add((ulong)game.m_steamIDLobby)) continue;
                    if(!SteamMatchmaking.RequestLobbyData(game.m_steamIDLobby)) pending.Remove((ulong)game.m_steamIDLobby);
                }
                if(pending.Count > 0) await Task.WhenAny(finished.Task,Task.Delay(4000,stop.Token));
                stop.Token.ThrowIfCancellationRequested(); stop.Cancel();
            }
            return found;
        }
        public async Task<PlayerRoomInfo[]> Browse(string compatibility,CancellationToken token,bool friendsOnly = false)
        {
            token.ThrowIfCancellationRequested();
            long started = clock.ElapsedMilliseconds;
            List<CSteamID> ids;
            if(friendsOnly) ids = await FriendsLobbies(token);
            else
            {
                void Filter(string key,string value) => SteamMatchmaking.AddRequestLobbyListStringFilter("ca_"+key,value,ELobbyComparison.k_ELobbyComparisonEqual);
                Filter("compat",compatibility); Filter("visibility","Public"); Filter("open","1");
                SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
                SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
                SteamMatchmaking.AddRequestLobbyListResultCountFilter(100);
                var list = await Result<LobbyMatchList_t>(SteamMatchmaking.RequestLobbyList(),token);
                ids = new List<CSteamID>();
                for(int i=0; i<Math.Min(100u,list.m_nLobbiesMatching); i++) ids.Add(SteamMatchmaking.GetLobbyByIndex(i));
            }
            token.ThrowIfCancellationRequested();
            var rooms = new List<PlayerRoomInfo>();
            foreach(var id in ids)
            {
                string privacy = Read(id,"visibility");
                if(!id.IsValid() || !id.IsLobby() || Read(id,"compat") != compatibility || Read(id,"open") != "1" ||
                    (privacy != "Public" && (!friendsOnly || privacy != "Friends")) || Read(id,"status") != "Lobby" ||
                    !int.TryParse(Read(id,"players"),out int players) || !int.TryParse(Read(id,"capacity"),out int capacity)) continue;
                var room = new PlayerRoomInfo {
                    Id = id.ToString(), Name = PlayerRoomInfo.Label(Read(id,"name")), Host = PlayerRoomInfo.Label(Read(id,"host")),
                    Mode = PlayerRoomInfo.Label(Read(id,"mode")), Map = PlayerRoomInfo.Label(Read(id,"map")),
                    Region = PlayerRoomInfo.Label(Read(id,"region"),12), Players = players, Capacity = capacity,
                    Privacy = privacy == "Friends" ? RoomPrivacy.Friends : RoomPrivacy.Public
                };
                if(!room.Joinable || room.Name.Length == 0) continue;
                var raw = Read(id,"location");
                if(raw.Length > 0 && raw.Length < Constants.k_cchMaxSteamNetworkingPingLocationString &&
                    SteamNetworkingUtils.ParsePingLocationString(raw,out var location))
                    room.EstimatedPing = SteamNetworkingUtils.EstimatePingTimeFromLocalHost(ref location);
                rooms.Add(room);
            }
            browseMs = clock.ElapsedMilliseconds - started;
            return rooms.ToArray();
        }
        public string Region => SteamUtils.GetIPCountry();
    }
}
#endif
