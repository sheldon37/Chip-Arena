# Optional operator-managed relay

Normal Online rooms now use automatic Cloudflare tunnels on each host computer.
They do not require this service or any Render subscription. This document is
only for operators deliberately selecting `--ca-relay` on both host and guest.
Each room's creator remains the Unity simulation authority.

## Deploy

Render (or another Docker web-service host):

- Repository: `sheldon37/Chip-Arena`; choose the reviewed networking commit.
- Root directory / Docker build context: `unity/ChipArenaConversion`.
- Dockerfile relative to that directory: `Relay/Dockerfile`.
- One instance; HTTPS and WebSocket upgrades enabled; health path `/health`.
- Disable automatic deploys for every game-source commit: relay restarts end rooms.
- The application honors the host's `PORT`; Docker otherwise listens on 8080.
- No account-service keys, game accounts, database, licensed media or Unity
  installation is required. The Docker context allowlist includes only relay
  source, the project file and the shared room-code implementation.
- Set request logging to exclude query strings. Host accept-route tickets must
  not enter access logs. Keep the upstream reachable only through the proxy.

Select the hosting plan explicitly. For responsive joining, use an instance that
does not sleep. A sleeping free instance needs a separate cold-start policy and
can exceed the game's bounded connection timeouts; do not advertise it as
equivalent to an always-on service. No paid service is created by committing this
Dockerfile.

## Verify before configuring players

1. `https://<actual-service-host>/health` must return service
   `chip-arena-native-relay`, `protocol: 6` and `roomCodeLength: 6`.
2. From this Unity project directory, run:

   ```sh
   dotnet run --project Tests/CoreTests.csproj -c Release -- --relay-smoke wss://<actual-service-host>
   ```

   This creates temporary rooms and exercises two real WebSocket clients, short
   codes, case normalization, discovery, private-room hiding, incompatible-source
   refusal, ordered frames, a 130 KB snapshot and host/guest disconnect cleanup.
   It does not claim Unity physics, rendering or two-machine gameplay acceptance.
3. Record the deployed source commit, actual URL, plan/region and test result.

## Select the optional endpoint

After the live smoke passes, launch the participating players with the same
explicit `--ca-relay wss://<actual-service-host>` argument. Do not publish
`native/online.json`: the abandoned shared-service bootstrap is not used by the
Cloudflare client. No paid service is needed for the default Online flow.

Build the exact integrated Unity commit with the owner's existing
`Build-and-Publish.cmd`. Its Unity/native checks must pass before the Windows
player is put on the Update button. An updated source branch alone is not a
published game update.
