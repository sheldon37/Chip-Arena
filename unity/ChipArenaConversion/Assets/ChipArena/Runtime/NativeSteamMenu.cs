using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace ChipArena.UnityConversion
{
    public sealed partial class ConversionGame
    {
        private ISteamPlayerRooms steamRooms;
        private bool steamSelected;
        private bool SteamOnline => internetRooms && steamSelected && steamRooms != null;
        private string steamError = null, steamRoomName = "", steamFilter = "", steamCompatibility, steamRevision;
        private bool steamShareRegion;
        private RoomPrivacy steamPrivacy = RoomPrivacy.Private;
        private PlayerRoomInfo[] steamResults = Array.Empty<PlayerRoomInfo>();
        private float nextSteamPublish, nextSteamBrowse;
        private bool steamFriendsOnly;
        private string SteamCompatibility
        {
            get
            {
                if(steamCompatibility == null || steamRevision != content.contentRevision)
                {
                    steamRevision = content.contentRevision;
                    steamCompatibility = PlayerRoomInfo.Compatibility(content.sourceCommit, steamRevision);
                }
                return steamCompatibility;
            }
        }

        [Serializable] private sealed class SteamBuildSettings { public uint appId = 0; public bool defaultSteam = false; }
        private void InitializeSteamRooms()
        {
#if CHIP_ARENA_STEAM
            try
            {
                var path = Path.Combine(Application.streamingAssetsPath, "ChipArenaSteam.json");
                var settings = File.Exists(path) ? JsonUtility.FromJson<SteamBuildSettings>(File.ReadAllText(path)) : null;
                if(settings == null || settings.appId == 0) throw new IOException("Set the Steam build App ID.");
                steamRooms = SteamPlayerRooms.Open(settings.appId);
                string[] args = Environment.GetCommandLineArgs();
                steamSelected = !UseManagedRelay && (settings.defaultSteam || args.Contains("--ca-steam")) && !args.Contains("--ca-cloudflare");
            }
            catch(Exception e) { steamError = "Steam unavailable · " + e.Message; Debug.LogWarning(steamError); }
#endif
        }

        private void RoomBackendPicker()
        {
            if(!internetRooms) return;
            if(connecting != null || relayConnecting != null || server != null) return;
            if(steamRooms == null) { if(steamError != null) Fine(steamError); return; }
            int choice = GUILayout.Toolbar(steamSelected ? 1 : 0, new[]{UseManagedRelay ? "Managed relay" : "Cloudflare", "Steam"});
            if((choice == 1) == steamSelected) return;
            listStop?.Cancel(); DiscardStandbyRoom();
            steamSelected = choice == 1; joinCode = ""; networkStatus = "";
            steamResults = Array.Empty<PlayerRoomInfo>(); nextSteamBrowse = 0;
        }
        private void SteamCreateFields()
        {
            Field("Room name"); steamRoomName = GUILayout.TextField(steamRoomName, 40);
            steamShareRegion = NativeControllerPointer.Toggle(steamShareRegion, "Show host country in browser");
            Field("Visibility");
            steamPrivacy = (RoomPrivacy)GUILayout.Toolbar((int)steamPrivacy, new[]{"Private", "Friends only", "Public"});
        }
        private bool SteamGameLocked => arena != null || expeditionSession != null || deploymentLocked ||
            deploymentRoom?.Deployed == true || openingRun != null || stageLoad.Requested || stageLoad.BlocksInput;
        private PlayerRoomInfo SteamDescription()
        {
            int capacity = prefs.pve ? 4 : 8;
            int seats = 1 + peers.Count(p => p.Seat != null); // reserves disconnected seats for the resume window
            int connected = 1 + peers.Count(p => p.Seat != null && p.Connection?.Closed == false);
            string host = PlayerRoomInfo.Label(steamRooms.HostName);
            string name = PlayerRoomInfo.Label(steamRoomName);
            return new PlayerRoomInfo {
                Name = name.Length > 0 ? name : PlayerRoomInfo.Label(host + "’s room"),
                Region = steamShareRegion ? steamRooms.Region : "", Host = host, Players = connected, Capacity = capacity, Privacy = steamPrivacy,
                Mode = PlayerRoomInfo.ModeName(prefs.pve, prefs.expedition, prefs.teams, prefs.deathmatch),
                Map = arena?.MapId ?? (prefs.pve ? (prefs.expedition ? "Deployment Station" : (NativeBossBattle.Map(bossBattlePick) ?? "siren")) : prefs.map),
                State = SteamGameLocked ? PlayerRoomState.InProgress : seats >= capacity ? PlayerRoomState.Full : PlayerRoomState.Lobby
            };
        }
        private void SteamPlatformTick()
        {
            if(steamRooms == null) return;
            steamRooms.Tick();
            if(steamRooms.EndReason != null)
            {
                var reason = steamRooms.EndReason; steamRooms.EndReason = null;
                LeaveRoom(); networkStatus = reason; Notice(reason); return;
            }
            if(SteamOnline && relayHost != null && Time.unscaledTime >= nextSteamPublish)
            { nextSteamPublish = Time.unscaledTime + .25f; steamRooms.Publish(SteamDescription()); }
            if(SteamOnline && menu && MenuTarget == MenuScreen.Join && !Hosting && server == null && connecting == null &&
                !reconnect.Active && !listingRooms && Time.unscaledTime >= nextSteamBrowse)
                StartCoroutine(FindSteamRooms(true));
            if(steamRooms.PendingInvite != null && CanAcceptInvite && !reconnect.Active && menu)
            {
                var id = steamRooms.PendingInvite; steamRooms.PendingInvite = null;
                Go(MenuScreen.Join); internetRooms = true; steamSelected = true; joinCode = id; JoinRoom();
            }
        }
        private void SteamBrowser()
        {
            bool friends = GUILayout.Toolbar(steamFriendsOnly ? 1 : 0, new[]{"Public rooms", "Friends’ rooms"}) == 1;
            if(friends != steamFriendsOnly)
            {
                steamFriendsOnly = friends; listStop?.Cancel(); steamResults = Array.Empty<PlayerRoomInfo>(); nextSteamBrowse = 0;
            }
            Field("Filter by room, host, mode, map or region");
            steamFilter = GUILayout.TextField(steamFilter, 64);
            bool enabled = GUI.enabled;
            GUI.enabled = enabled && !listingRooms && connecting == null && server == null;
            if(NativeControllerPointer.Button(listingRooms ? "Refreshing…" : "Refresh rooms", button)) StartCoroutine(FindSteamRooms(false));
            GUI.enabled = enabled && connecting == null && server == null;
            foreach(var room in steamResults)
            {
                if(!room.Matches(steamFilter)) continue;
                string quality = room.EstimatedPing < 0 ? "" : " · ~" + room.EstimatedPing + " ms";
                string region = string.IsNullOrEmpty(room.Region) ? "" : " · " + room.Region;
                if(NativeControllerPointer.Button(room.Name + " · Lobby · " + room.Players + "/" + room.Capacity +
                    "\n" + room.Host + " · " + room.Mode + " · " + room.Map + region + quality, button))
                { joinCode = room.Id; JoinRoom(); }
            }
            GUI.enabled = enabled;
        }
        private IEnumerator FindSteamRooms(bool automatic)
        {
            if(listingRooms) yield break;
            listingRooms = true;
            try
            {
                listStop?.Cancel(); listStop = new CancellationTokenSource();
                var token = listStop.Token;
                nextSteamBrowse = Time.unscaledTime + 10;
                if(!automatic) networkStatus = "Finding Steam rooms…";
                var task = steamRooms.Browse(SteamCompatibility, token, steamFriendsOnly);
                while(!task.IsCompleted && !token.IsCancellationRequested) yield return null;
                if(token.IsCancellationRequested) yield break;
                if(task.IsFaulted || task.IsCanceled) { steamResults = Array.Empty<PlayerRoomInfo>(); networkStatus = OnlineError(task.Exception, "Could not refresh Steam rooms."); yield break; }
                steamResults = task.Result;
                nextSteamBrowse = Time.unscaledTime + 10;
                if(!automatic || networkStatus.Length == 0) networkStatus = steamResults.Length + " joinable Steam room(s).";
            }
            finally { listingRooms = false; }
        }
        private void SteamInvite()
        {
            if(!steamRooms.Invite()) Notice("Steam invitations are unavailable. Check the overlay and room availability.");
        }
    }
}
