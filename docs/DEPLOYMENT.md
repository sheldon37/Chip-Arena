# Deployment

Chip Arena's public Site has no ChatGPT/game login. It includes its own HTTPS room relay, D1 room mailbox database, browser client, and local practice. The creator's Web Worker owns continuous Rapier simulation. The request-based server relays bounded snapshots/intentions; it does not pretend to run a persistent 60 Hz physics loop.

## Sites build

```sh
npm ci
npm run build
```

The artifact contains `dist/client` (browser assets/source ZIP), `dist/server/index.js` (Cloudflare Worker), and `dist/.openai` (hosting manifest and Drizzle migrations). `.openai/hosting.json` preserves Chip Arena's independent project identity and declares its own `DB` binding. Deploy the whole Worker artifact through Sites; do not publish only static assets when online rooms are needed. Generated migrations are applied by deployment. No Embertrail database, account, service endpoint, repository or asset is used.

The relay uses same-origin `/api/rooms/{create,join,exchange,leave}` POST requests. Room codes are eight characters. Each browser generates a separate random 256-bit session token; only hashes are stored. Guest claims are atomic; a room code cannot impersonate the host. Tokens remain in memory and are not included in invite URLs. No provider keys or secrets are required. Rooms expire after two hours, stale hosts prevent joining, and cleanup removes expired/closed mailboxes on creation. Guest seats are not transferable after leaving; create a fresh room for a replacement player.

HTTPS exchanges target 50 ms with one request in flight. Network/D1 time can reduce effective snapshot delivery below the simulation's 20 Hz output. Reliable selection events retain IDs until acknowledged; retries are idempotent. Snapshots coalesce; ordered input batches are retained until the simulation acknowledges consumption. Batches are bounded and deduplicated, and the simulation consumes one input per tick. This improves reachability through restrictive networks but adds latency compared with a direct data channel or dedicated WebSocket backend. No external TURN/signaling service or paid infrastructure was provisioned.

## Local development

`npm run dev` starts Vite plus the Node server; `/api/network`, `/api/rooms` and `/socket` proxy to it. The Node relay uses SQLite with the same migrations and HTTP handler as the deployed D1 relay. Its mailboxes are ephemeral in-memory data. The default local Create/Join flow automatically selects the dedicated WebSocket server; `npm run test:https` exercises the separate relay.

## Persistent dedicated backend

A dedicated server offers independent authority and a continuous WebSocket transport. Use a host supporting a long-running Node 24 process and WebSockets. A single process must own each room; multiple replicas require room-aware routing.

```sh
npm ci
npm run build
npm run server:build
PORT=8788 ALLOWED_ORIGINS=https://your-client.example npm start
```

This serves `dist/client`, `/api/network`, `/api/rooms`, `/health`, and `/socket`. Put it behind TLS; HTTPS clients need `wss://` for the dedicated path. Configure WebSocket upgrades and idle timeouts above 15 seconds. For a frontend hosted on Sites, set its runtime `MULTIPLAYER_URL=wss://YOUR_BACKEND/socket` after deploying the Node server. `/api/network` returns this endpoint and normal Create/Join plus invite links use it automatically. Set the server’s `ALLOWED_ORIGINS` to the Site origin. When Node serves the frontend itself, routing is automatic with no additional configuration. Advanced settings remain an optional manual override. Rebuild client/server together when protocols change.

```sh
docker build -t chip-arena .
docker run --rm -p 8788:8788 -e ALLOWED_ORIGINS=http://localhost:8788 chip-arena
```

Docker deployment was not run here; Node compilation, HTTP and WebSocket integration tests were run separately. No external dedicated backend has been provisioned. A modified browser host can cheat; this prototype has no independent anti-cheat, host migration, or refresh recovery.

## Verification boundary

Two browser clients and complete input-driven HTTP matches are tested against the supervised/local backend. Those results do not certify public deployment performance. Public internet end-to-end verification still requires two players to connect through the published backend, complete a match and rematch. See TEST-RESULTS.md for actual runs and latency measurements.

## Current deployment limitation

This Sites integration exposes D1/R2 bindings but does not expose the persistent room-coordination/Durable Object binding needed to host the shared WebSocket simulation. No unsupported Worker-global room map was substituted. The public Site remains on the HTTPS relay until a real Node backend is deployed and `MULTIPLAYER_URL` is configured. There is no paid provisioning in this update. A browser-to-Worker WebSocket alone would not solve cross-client room coordination.
