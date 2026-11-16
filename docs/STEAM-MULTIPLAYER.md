# Steam player-hosted multiplayer

Steam is an opt-in Win64 networking profile. One player's Unity process owns the lobby, match and every Expedition cohort it simulates. Valve provides Steam lobby services and peer connectivity through Steam Networking Sockets / Steam Datagram Relay (SDR). Ordinary rooms require no rented game server. The existing standalone player can continue using Cloudflare or LAN.

## What runs where

| Responsibility | Current implementation | Steam profile |
| --- | --- | --- |
| Simulation and admission | Host Unity process. `NativeNetwork.HostMessage` verifies protocol, content revision, room, seat and Expedition requirements. Clients send inputs and choices. | Same authority and checks. Incoming P2P sockets must belong to the active listener and an authenticated lobby member. The guest connects to the original lobby owner. Resuming requires the same socket Steam ID and existing seat checks. |
| Default internet room | `NativeCloudflareHost` opens a loopback TCP listener, starts the bundled cloudflared Quick Tunnel and leases a short code in the Site directory. Guests resolve the code to a validated tunnel origin and connect over WSS. The code is renewed while the host is alive. | Steam lobby ID, membership, compatibility metadata and a P2P listener on virtual port zero. No tunnel process, public IP, directory round trip or port forwarding in the Steam path. |
| Optional transports | LAN uses direct TCP and local discovery. The explicit managed relay override uses its own native/v3 WebSocket host, join and listing endpoints. The legacy Electron worker also has its own room service. | Different backends can coexist in one Steam-enabled binary. All peers in a given room use its backend. Existing standalone users can join a compatible Steam-enabled host that selects Cloudflare. |
| Discovery and invitations | The default Quick Tunnel flow uses private codes and account invitations. The optional managed relay has a public list. | Public browser, friends' accessible lobbies, overlay invite dialog, warm lobby/rich-presence callbacks and cold `+connect_lobby` launch. Private is the creation default. Friends-only and private lobbies stay out of global results. Private rooms use invitations. |
| Gameplay wire | `NativeConnection` frames and compresses messages. Bounded ordered commands, coalesced snapshots, background heartbeats, control epochs and frame ordering feed the existing host/replica logic. | `OrderedRoomStream` carries the same framed bytes in ordered, reliable Steam messages of up to 48 KiB. Large snapshots can span multiple carrier messages. The existing 32 KiB inbound guest payload limit and 2 MiB gameplay frame limit still apply. |
| Accounts and persistence | The Site account service uses its database for accounts, sessions, XP, grants, profile/cosmetic data, presence and account invites. The protected local session authenticates backend requests. Unity peers receive a short-lived signed player claim. Local preferences and the existing save paths also remain in use. | A Steam ID identifies a connected peer. It does not grant a Chip Arena account, XP, cosmetics or developer access. Account-backed modes such as Expedition still use the existing sign-in and valid claims. No account migration, database change or Steam stats write. |
| Build/update | Standalone update feed and a separate optional SteamPipe staging step. | Steam-managed installs use Steam updates. Staging a standalone executable alone still gives it the standalone networking implementation. Build the Steam profile first to include the adapter. |

Implementation entry points are `Core/NativePlayerRooms.cs`, `Runtime/NativeSteamMenu.cs`, `Runtime/SteamPlayerRooms.cs`, `Runtime/SteamRoomDirectory.cs` and `Runtime/SteamRoomSocket.cs` under `unity/ChipArenaConversion/Assets/ChipArena`. Gameplay routing stays in `NativeNetwork.cs`, `NativeExpeditionNetwork.cs`, `NativeExpeditionSplitRuntime.cs`, and `NativeStageGateNetwork.cs`. The existing Cloudflare directory and account routes are in `src/relay/worker.ts`, `directory.ts`, `accounts.ts`, `account-xp.ts` and `account-social.ts`.

## Room lifecycle

- **Create.** The local host lobby opens while the Steam request completes. The Steam lobby starts private. The listener, compatibility key, original owner, capacity and metadata are installed before the selected visibility is published. Start and invitations use the existing readiness gate. The Steam profile skips Quick Tunnel standby.
- **Find.** Public results require the same wire schema, source and content/mod revision, a free slot, and an open lobby. The friends tab requests fresh metadata for distinct lobbies occupied by Steam friends playing this App ID. Both views exclude full, private and in-progress results and support a text filter. The screen refreshes automatically and has a manual refresh. Closed rows may persist until the next response. Joining rechecks metadata and owner, followed by authoritative game admission.
- **Display.** Rows carry the room name, Steam persona of the host, connected human count and capacity, team/FFA/Deathmatch/Boss Battle/Expedition mode and selected map. Reserved disconnected seats count against capacity. The optional host country comes from Steam's IP-country API. Estimated latency comes from Steam's ping-location data when available. The connection report measures actual in-session RTT separately.
- **Play.** Steam discovery publishes a summary. The existing room-options message still carries the actual match settings, including blind pick, kill target and Deathmatch chip rules. Fresh admission stops during drafting, starting, deploying, playing or when all seats are reserved.
- **Reconnect.** A guest with retained lobby membership can open a new socket to the original host even when the lobby is locked. The game owns the resume token, 75-second automatic retry window and 90-second host reservation. A valid account is additionally required for an Expedition seat. An application restart or loss of lobby membership during a locked match is outside this recovery path.
- **Exit.** The host closes admission, clears join presence, closes sockets and leaves the lobby. Cancellation and late create/join callbacks release abandoned memberships. Steam may transfer lobby ownership after a host exits. A changed owner ends the game session. Steam host promotion and crash migration remain disabled. Existing pre-match promotion on other transports is retained.

## Windows setup

1. Enroll as a Steamworks partner and obtain the base game's App ID or its separate Playtest App ID. For a Steam-installed test, configure a Windows depot and launch option on the same app, grant the test accounts access to that app/package, and give the build account the appropriate SteamPipe permissions. Use the Playtest App ID for a Playtest depot. Signed-in testers need the same app and compatible builds. Ordinary Steam lobby/P2P sessions require no publisher Web API key or dedicated server login token.
2. Use a separate checkout of this development branch for Steam packaging. Start from `unity/ChipArenaConversion` before opening the project in Unity. With the real numeric ID supplied in your build environment, this PowerShell command sets the local profile:

   ```powershell
   python tools/configure_steam_multiplayer.py --app-id $env:CHIP_ARENA_STEAM_APP_ID --default cloudflare
   ```

   Use `--default steam` for a Steam-first test or release profile. The helper adds the pinned upstream Unity package `com.rlabrecque.steamworks.net` at tag `2025.165.0`, preserves other dependencies and compiler flags, sets `CHIP_ARENA_STEAM`, and writes the local `steam_appid.txt` and player `Assets/StreamingAssets/ChipArenaSteam.json`. The runtime verifies the effective App ID. Keep generated profile changes in this checkout. Both public App IDs and privacy defaults are configurable. Credentials belong in Steam's client/build tooling.
3. Open the configured project in Unity **6000.3.24f1** and allow Package Manager to resolve the Git dependency. Install Windows Build Support and supply the project's usual local content packs. Use the existing Win64 build process from this checkout. To run outside Steam, start the desktop Steam client under the same OS user and use the matching developer `steam_appid.txt` in the working directory. In Create or Join select **Online → Steam**. `--ca-steam` selects Steam at launch. `--ca-cloudflare` selects the existing internet backend. Failed Steam initialization leaves a reason on the online setup screens and the Cloudflare/LAN controls available.
4. Stage the locally verified player with the existing SteamPipe tool. See [Steam Playtest packaging](STEAM-PLAYTEST.md). The networking-aware stager checks the player App ID against the supplied depot app and checks for the Win64 Steam API plugin. It removes development `steam_appid.txt` from the staged depot. Configure the executable given by the staging tool. Launch through Steam for overlay and invitation tests. Keep this Steam-specific build off the standalone update feed.
5. For future single-sign-on, have the client request an audience-specific Web API ticket and have the **backend** validate it with Valve for the expected app and identity. Link the verified Steam ID to a signed-in existing Chip Arena account with an explicit conflict/recovery policy. Store a suitably permissioned publisher key only in backend secrets. This is separate from authenticated Steam lobby membership and P2P socket identity.

Steamworks.NET exposes Valve's current Steam interfaces to Unity without adding a second replication framework. The Steam-free build has no wrapper package, SDK symbols or native plugin requirement.

[Steamworks.NET installation](https://steamworks.github.io/installation/) · [Pinned wrapper](https://github.com/rlabrecque/Steamworks.NET/releases/tag/2025.165.0) · [Valve matchmaking and invitations](https://partner.steamgames.com/doc/features/multiplayer/matchmaking) · [Steam Networking Sockets](https://partner.steamgames.com/doc/api/ISteamNetworkingSockets) · [Steam Datagram Relay](https://partner.steamgames.com/doc/features/multiplayer/steamdatagramrelay) · [Networking estimates](https://partner.steamgames.com/doc/api/ISteamNetworkingUtils) · [Account tickets](https://partner.steamgames.com/doc/features/auth)

## Reliability audit

| Symptom | Transport work and remaining measurement |
| --- | --- |
| Slow room creation | Cloudflare process startup, edge registration and code resolution have their own latency. Steam uses an asynchronous lobby call and prewarms SDR. Record click-to-local-lobby and click-to-joinable separately on cold and warm starts. |
| Delayed invites or joins | Steam supplies push callbacks and launch arguments. Lobby search/entry and P2P establishment still depend on Steam connectivity. API requests have a 20-second deadline, outgoing sockets 15 seconds. Invalid, incompatible and abandoned requests close cleanly. |
| Lag with larger parties | Route RTT, congestion and reliable backlog can contribute. The host also simulates combat, navigation and Expedition actors, encodes frames, and fans them out. The normal network tick targets 20 snapshots/sec. The initial adapter uses reliable ordered delivery. Under loss, a missing fragment can delay later snapshots. Record RTT plus host frame/simulation timings before deciding on unreliable sequenced snapshots or separate lanes. |
| Frozen guests on instance entry | Cohort tickets, generated-layout identity, local replica loading, ready acknowledgments, load timeouts and bounded input credits belong to the game. The same barriers and background connection heartbeats run on either transport. Steam callbacks still need the Unity main thread for new connections. |
| Lost host | Lobby ownership election lacks live host-only simulation, per-seat private state, RNG, pending rewards and per-cohort readiness. Transparent crash migration requires authenticated checkpoints and replay/idempotence for outstanding commands/rewards. Existing voluntary handoff code is insufficient for arbitrary host loss. |

Settings → Connection report and Ctrl+Shift+F9 include the existing frame/load/disconnect context. A Steam-enabled session also reports SDR and authentication availability, the last socket event, and last create/join/list latency in milliseconds. A value of -1 means there is no completed sample. None of the offline checks measure an internet route.

## Migration checklist

| State | Work and evidence |
| --- | --- |
| Implemented | Opt-in build profile and backend selection, host/guest Steam socket admission, overlay and Join Game routes, compatible public and friends discovery, private rooms, filtering, lifecycle cleanup and diagnostics. |
| Offline verification | Run the focused core command below for Cloudflare directory, transport backlog, reconnect timing, Expedition transitions and Deathmatch. Run the separate adapter fixture for privacy, invitations, owner checking, listing, cancellation and redial with simulated SDK callbacks. Run both semantic compiles with the pinned Unity references. |
| Partner setup outstanding | Configure the real app/depot/package, import the pinned package in Unity, produce a signed-in Windows test build and install it on separate PCs. |
| Live acceptance outstanding | Complete the matrix below. Compare Steam and Cloudflare captures from the same build and hardware before changing defaults. |
| Future decisions | Release default, optional persistent account linking and cross-store matchmaking, snapshot delivery policy under loss, and host migration. Keep the existing backend for cross-platform accounts and persistence. |

From `unity/ChipArenaConversion`:

```text
dotnet run -c Release --project Tests/CoreTests.csproj -- --player-rooms
dotnet run -c Release --project tools/steam_room_fixtures/SteamRoomFixtures.csproj
python -m unittest discover -s Tests -p test_steam_networking_setup.py
python -m unittest discover -s Tests -p test_native_updater.py
python tools/check_unity_compile.py --unity-root PATH_TO_UNITY_REFERENCES --output OUTPUT_STANDARD
python tools/check_unity_compile.py --unity-root PATH_TO_UNITY_REFERENCES --output OUTPUT_STEAM --steamworks-package PATH_TO_TAGGED_COM_RLABRECQUE_STEAMWORKS_NET
```

## Live acceptance matrix

| Clients | Action | Pass condition |
| --- | --- | --- |
| Host + friend + nonfriend | Create each visibility, edit settings, refresh/filter both tabs, fill/vacate seats, leave and test a stale row. | Only eligible rooms appear. Metadata updates. Private requires an invitation. Full, incompatible, in-progress and closed rooms cannot admit a fresh pilot. |
| Two Steam accounts, separate PCs | Join via browser, overlay invitation while running, invitation from desktop with game closed, and Steam Friends → Join Game. Test overlay disabled, wrong App ID and incompatible mod sets. | All successful paths reach the same original host and seat exactly once. Failures are visible and leave no stale room. |
| Two then four/eight humans | Play FFA/team Deathmatch and normal PvP. Exercise Blind Pick, kill target, chip choices, host-only controls and combat under latency/loss. | Options, inputs, damage, kills, pickups, score, match end and rematch agree. Compare host frame time, RTT, dropped snapshot symptoms and disconnects with Cloudflare on equivalent hardware. |
| Two to four signed-in Expedition accounts | Start Deployment Station, take a subset through a join circle, delay one guest, transition instances, fight a boss, then disconnect and reconnect a guest. | Correct cohorts move, waiting pilots stay, all required ready acknowledgments arrive, seats and private choices match, rewards apply once. |
| Host + guest in combat | Interrupt only the guest socket, then restore with original lobby membership. Repeat with the host quitting, force-killed, and Steam disconnected. | Recover the same seat inside the window when the host remains. Host loss ends the session; nobody independently simulates as a replacement. Record outcomes for Steam membership loss and long outages. |
| Standalone host + guest | Run Cloudflare private-code join and account invite, then LAN host/join. Repeat using the compatible Steam-enabled build with Cloudflare selected and verify saved profiles/XP. | Existing room codes, accounts, invitations and gameplay work. Standalone updater and Steam-managed installations keep their own update paths. |
