# Native conversion rooms

The Unity client can host a direct TCP LAN room or connect through this separate .NET 8 WebSocket relay. No account session, developer password or production endpoint is bundled. Native rooms use protocol 6 and require the same pinned game source; they cannot join Electron rooms.

Online rooms now use **Create Room → Copy Code → Join Room**. New codes have six
characters (for example `7KMR4P`), with no prefix and no I, O, 0 or 1. Lowercase,
spaces and hyphens are accepted when pasting; old eight-character / `CA-` codes
remain accepted. New LAN rooms also use six characters. The optional Local network
mode and LAN invite links still work without the online service.

The ordinary player UI contains no relay address. Create, join and browse obtain
the shared endpoint from the existing Update channel's `native/online.json`.
An absent, invalid or unreachable configuration reports unavailable; it never
silently creates a LAN-only room or presents a nonfunctional online code.
Only dedicated `--ca-relay` remains an explicit operator override.

The host's Unity process still owns all gameplay; the relay only forwards binary
frames. Private rooms are omitted from discovery unless their host explicitly
opts into the public list. Host control secrets / one-use guest tickets remain
256-bit values, independently of the short human-readable code. Code allocation
retries collisions atomically; existing rooms are never replaced. Existing rate
limits, bounded traffic, version checks and disconnect cleanup remain active.
The relay returns eight-character codes to old clients that omit `codeLength=6`.

**Deployment is required before online play works.** See [DEPLOYMENT.md](DEPLOYMENT.md)
for the Docker service settings, live two-client smoke check and shared endpoint
publication. Do not publish a placeholder endpoint or treat a local relay test as
proof of internet connectivity.

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

Add `--ca-pve` for Ward Zero, `--ca-bots 2` for bots, or `--ca-relay wss://YOUR-RELAY-HOST --ca-public` to list a relayed server. The server logs its room code. Players choose Ready; a match starts when the configured minimum is ready. Rejoin leases last 90 seconds. A fully empty server returns to its lobby after that lease expires. Dedicated mode is compiled but requires native Unity execution to validate.

A player-hosted room currently ends its network session if its authority closes. This relay does not manufacture or migrate a Unity physics authority. Use an unattended server for a room that should outlive an individual player's client.

`python3 tools/check_relay.py` builds the relay and managed checks, starts a temporary loopback service, and exercises real discovery, private-room behavior, two-client frame exchange and cleanup. It does not access a deployed service.

PR #65 adds a host-control PING/PONG heartbeat and bounded room-control handling. Use matching current client/relay builds; older relay source may close the host control stream. No public relay has been deployed by this change.
