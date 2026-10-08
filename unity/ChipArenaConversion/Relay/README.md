# Native conversion rooms

The Unity client can host a direct TCP LAN room or connect through this separate .NET 8 WebSocket relay. No account session, developer password or production endpoint is bundled. Native rooms use protocol 5 and require the same pinned game source; they cannot join Electron rooms.

Run `dotnet run --project Relay/ChipArena.Relay.csproj --urls http://127.0.0.1:8080` for local development. In the Unity menu, enable **Internet room via conversion relay**, enter `ws://127.0.0.1:8080`, and host or join. Private rooms are omitted from discovery; listing a room is an explicit host option.

For an internet deployment, place the relay behind an HTTPS/WebSocket reverse proxy and enter its `wss://` origin. Keep the upstream bound to loopback or a private interface. Disable URL/query logging at the proxy: connection tickets in the accept route are credentials. The relay enforces room capacity, one-use tickets, per-address connection limits, bounded binary bandwidth and cleanup when the host leaves. Room names and occupancy are visible only for listed rooms. It forwards game frames; the Unity host still verifies player grants and owns gameplay. Provisioning a public endpoint is a separate deployment step, not performed by this conversion.

A Windows Unity build can also run an unattended authority:

```powershell
.\ChipArena-Native-Conversion-TEST.exe -batchmode -nographics --ca-server --ca-port 29941 --ca-map foundry --ca-min-players 2
```

Add `--ca-pve` for Ward Zero, `--ca-bots 2` for bots, or `--ca-relay wss://YOUR-RELAY-HOST --ca-public` to list a relayed server. The server logs its room code. Players choose Ready; a match starts when the configured minimum is ready. Rejoin leases last 90 seconds. A fully empty server returns to its lobby after that lease expires. Dedicated mode is compiled but requires native Unity execution to validate.

A player-hosted room currently ends its network session if its authority closes. This relay does not manufacture or migrate a Unity physics authority. Use an unattended server for a room that should outlive an individual player's client.

`python3 tools/check_relay.py` builds the relay and managed checks, starts a temporary loopback service, and exercises real discovery, private-room behavior, two-client frame exchange and cleanup. It does not access a deployed service.

PR #65 adds a host-control PING/PONG heartbeat and bounded room-control handling. Use matching current client/relay builds; older relay source may close the host control stream. No public relay has been deployed by this change.
