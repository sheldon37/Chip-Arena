# Native conversion rooms

The Unity client can host a direct TCP LAN room or connect through this separate .NET 8 WebSocket relay. No account session, developer password or production endpoint is bundled. Native rooms use protocol 6 and require the same pinned game source; they cannot join Electron rooms.

Online rooms now use **Create Room → Copy Code → Join Room**. New codes have six
characters (for example `7KMR4P`), with no prefix and no I, O, 0 or 1. Lowercase,
spaces and hyphens are accepted when pasting. Old eight-character / `CA-` codes
remain supported for LAN and the optional managed relay. New LAN rooms also use six characters. The optional Local network
mode and LAN invite links still work without the online service.

Ordinary Online rooms automatically start the bundled, hash-verified Cloudflare
connector on the host PC. Guests resolve the six-character code through the
existing room directory and connect over a secure WebSocket to the host's
loopback-only native game listener. No router configuration, subscription,
account sign-in for Cloudflare, manual address, Render service, or shared relay
configuration is needed. Gameplay remains in the host's Unity process.

The directory uses `UN` plus the six-character code internally, retaining its
existing eight-character contract and independent 256-bit lease credentials.
Registration retries collisions; a live lease cannot be overwritten. The host
refreshes its lease while open; it expires within three minutes after leaving.
Directory outages do not interrupt already-connected players. These rooms are
private: share the code; they are not placed in the optional managed-relay list.
Native protocol/content checks still refuse Electron or incompatible games.

The connector uses an isolated configuration and only a loopback listener. It
is killed when hosting is cancelled or the room closes. A Windows job object
also kills it if the game crashes. Quick Tunnels have no uptime guarantee and
remain a playtesting/hobby connection option. A tunnel disconnect ends the
room; no host migration or automatic authority replacement is claimed.

Windows packaging downloads pinned cloudflared 2026.9.3 from its official
release, checks its exact size/SHA-256, and includes it plus its Apache license
in `app/runtime/`. Players receive it through the normal Update payload; they
do not download or install a connector themselves. Source ZIPs contain no
connector binary. A missing or altered player binary cannot be executed.

An optional operator-managed relay is still available with the explicit
`--ca-relay wss://YOUR-RELAY-HOST` argument on **both host and guest** launches.
Only that opt-in path exposes relay public-room discovery. Old saved relay URLs
are ignored for ordinary players. See [DEPLOYMENT.md](DEPLOYMENT.md) for this
advanced option; it is not required for Cloudflare Online rooms.

Local relay development: `dotnet run --project Relay/ChipArena.Relay.csproj --urls
http://127.0.0.1:8080`. `PORT` (when supplied by a hosting platform) overrides the
bind address with `http://0.0.0.0:<PORT>`. Keep a self-managed upstream private and
use a TLS/WebSocket reverse proxy. Query tickets are credentials: disable full
URL/query access logs. Hosted deployments need one instance (room state is in
memory); a restart ends current rooms.

A Windows Unity build can also run an unattended authority:

```powershell
.\ChipArena-Native-Conversion-TEST.exe -batchmode -nographics --ca-server --ca-port 29941 --ca-map foundry --ca-min-players 2
```

Add `--ca-online` for an automatic Cloudflare room, `--ca-pve` for Ward Zero, `--ca-bots 2` for bots, or `--ca-relay wss://YOUR-RELAY-HOST --ca-public` to list a relayed server. The server logs its room code. Players choose Ready; a match starts when the configured minimum is ready. Rejoin leases last 90 seconds. A fully empty server returns to its lobby after that lease expires. Dedicated mode is compiled but requires native Unity execution to validate.

A player-hosted room currently ends its network session if its authority closes. This relay does not manufacture or migrate a Unity physics authority. Use an unattended server for a room that should outlive an individual player's client.

`python3 tools/check_relay.py` builds the relay and managed checks, starts a temporary loopback service, and exercises real discovery, private-room behavior, two-client frame exchange and cleanup. It does not access a deployed service.

PR #65 adds a host-control PING/PONG heartbeat and bounded room-control handling. Use matching current client/relay builds; older relay source may close the host control stream. No public relay has been deployed by this change.

Internet acceptance: on Windows, run `dotnet run --project Tests/CoreTests.csproj
-c Release -- --cloudflare-smoke`. This stages the exact shipping connector,
creates a temporary public tunnel and directory lease, then verifies two guests,
ordered native frames, a 130 KB snapshot, collision refusal and cleanup. The
`Native Cloudflare internet check` workflow runs it when networking files change.
It is separate from native Unity/Windows gameplay acceptance.
