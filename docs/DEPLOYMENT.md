# Deployment

The hosted Site is public and does not require a ChatGPT login. The game itself has no authentication or account dependency. It publishes the static browser build and local practice only. Its request-based Worker runtime is not used to pretend to run a continuous physics server. No paid infrastructure was provisioned.

## Persistent backend

Use a service that supports a long-running Node.js process, WebSockets, and the memory/CPU required by Rapier. A single process must own each room. Do not run multiple replicas behind random load balancing without sticky routing or room-aware dispatch.

```sh
npm ci
npm run build
npm run server:build
PORT=8788 ALLOWED_ORIGINS=https://your-client.example npm start
```

The process serves `dist` over HTTP, `/health` for readiness, and `/socket` for WebSockets. Put it behind a TLS reverse proxy. HTTPS clients must use `wss://`. Configure proxy WebSocket upgrades and long idle timeouts (greater than the 15-second heartbeat interval). Limit incoming HTTP/WebSocket traffic at the deployment edge as appropriate. Rooms are ephemeral and vanish when the process restarts.

Alternatively build the included Dockerfile. Docker deployment was not executed in this environment; the Node production build and process are tested separately.

```sh
docker build -t chip-arena .
docker run --rm -p 8788:8788 \
  -e ALLOWED_ORIGINS=http://localhost:8788 \
  chip-arena
```

Set the browser's Settings → Multiplayer Server URL, or build with `VITE_SERVER_URL=wss://your-backend.example/socket`. Both users need access to the client and that backend; neither the client nor this server requires a ChatGPT login. Rebuild and deploy the client and server from the same source revision after protocol/content changes. Creating or sharing a room code does not publish a server.

## Static client

```sh
npm ci
npm run build
```

Publish `dist`. The source includes a separate Sites identity in `.openai/hosting.json`; it is unrelated to any other game. Use the existing identity for subsequent Site updates. All dependencies are bundled into the browser output, including the Rapier compatibility WASM; there are no remote art, font, or script CDN requirements.

## Before calling a deployment online multiplayer

Connect two independent browsers through the **deployed** backend, ready both, choose different specializations, destroy a wall or launch a prop, compare authoritative damage and the score, finish the match, and rematch. Local/supervised test results do not establish public-deployment reachability. This project currently makes no claim of publicly deployed multiplayer.

## Browser-hosted rooms

The static client now defaults to experimental PeerJS rooms: creator = authoritative host in a Web Worker, guest = input client. Normal players use room codes/invite links and enter no address. Hosting on HTTPS enables normal clipboard/WebRTC browser behavior. The build includes the worker and bundled PeerJS code; public PeerServer Cloud handles signaling only. No TURN credentials or relay backend have been provisioned. Direct connectivity is network-dependent and the preview did not complete the peer data channel.

Keep the dedicated Node server option for independent server authority. Settings → Advanced connection settings enables it; `VITE_SERVER_URL` remains its optional build-time default. Browser rooms are a trusted-host prototype path, with no host migration, account persistence or anti-cheat claim. A future relay integration should fetch short-lived ICE credentials from a controlled backend; never bundle a TURN provider’s master API key.
