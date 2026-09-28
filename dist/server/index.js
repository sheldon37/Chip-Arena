// src/shared/intros.ts
var INTRO_STYLES = {
  "dev-matrix": "DEV \xB7 L33T Throne",
  "neon-drop": "Neon Drop",
  "vector-slide": "Vector Slide",
  "signal-spin": "Signal Spin",
  "arc-salute": "Arc Salute",
  "pulse-wave": "Pulse Wave"
};

// src/shared/cosmetics.ts
var HATS = { none: "No hat", cap: "Ball cap", beanie: "Beanie", top: "Top hat", wizard: "Wizard hat", crown: "Crown", cone: "Traffic cone", antenna: "Antenna", halo: "Halo", cowboy: "Cowboy hat" };
var KILL_STYLES = { "voxel-burst": "Voxel breakup", confetti: "Confetti cannon", ascension: "Beam me up", shrink: "Pocket-sized", launch: "Team Rocket", ghost: "Ghost escape", "dev-clown": "Clown getaway \xB7 DEV ONLY", "dev-clown-love": "Clown Love \xB7 DEV ONLY", "dev-bass-van": "Bass Van \xB7 DEV ONLY" };
var developerKillEffect = (id) => id.startsWith("dev-");
var DEFAULT_COSMETICS = { intro: "neon-drop", introText: "Ready to make an impact", bodyColor: "#e5e7eb", hat: "none", killEffect: "voxel-burst" };
function cosmetics(raw, dev = false) {
  return { introText: introText(raw?.introText), intro: raw && Object.hasOwn(INTRO_STYLES, raw.intro) && (dev || raw.intro !== "dev-matrix") ? raw.intro : "neon-drop", bodyColor: typeof raw?.bodyColor === "string" && /^#[0-9a-f]{6}$/i.test(raw.bodyColor) ? raw.bodyColor : DEFAULT_COSMETICS.bodyColor, hat: raw && Object.hasOwn(HATS, raw.hat) ? raw.hat : "none", killEffect: raw && Object.hasOwn(KILL_STYLES, raw.killEffect) && (dev || !developerKillEffect(raw.killEffect)) ? raw.killEffect : "voxel-burst" };
}
function introText(value) {
  return typeof value === "string" ? Array.from(value.replace(/[\u0000-\u001f\u007f-\u009f\u202a-\u202e\u2066-\u2069]/g, " ").replace(/\s+/g, " ").trim()).slice(0, 64).join("") || DEFAULT_COSMETICS.introText : DEFAULT_COSMETICS.introText;
}

// src/shared/network-config.ts
var NETWORK = { maxPendingInputs: 180, maxInputBatch: 60, inputQueueLimit: 90, correctionDecay: 18, hardSnapDistance: 2.5, interpolationMinMs: 75, interpolationMaxMs: 350, extrapolationMaxMs: 80 };

// src/shared/relay-protocol.ts
var RELAY = { protocol: "chip-arena-https-v1", intervalMs: 50, inputHoldSeconds: 0.75, timeoutMs: 12e3, requestTimeoutMs: 4e3, maxEvents: 48, maxHostBytes: 384e3, maxGuestBytes: 24e3, ttlMs: 72e5 };
function normalizeRoomCode(value) {
  const code = value.trim().toUpperCase().replace(/[\s-]/g, "");
  return /^[A-HJ-NP-Z2-9]{8}$/.test(code) ? code : null;
}
function newRoomToken() {
  return [...crypto.getRandomValues(new Uint8Array(32))].map((n) => n.toString(16).padStart(2, "0")).join("");
}
function cleanProfile(p) {
  return { cosmetics: cosmetics(p?.cosmetics, true), authToken: typeof p?.authToken === "string" && p.authToken.length <= 1500 ? p.authToken : void 0, name: typeof p?.name === "string" ? p.name.replace(/[<>]/g, "").slice(0, 20) || "Pilot" : "Pilot", class: ["ranged", "magic", "melee"].includes(p?.class) ? p.class : "ranged", color: /^#[0-9a-f]{6}$/i.test(p?.color || "") ? p.color : "#97e4d0" };
}
var guestTypes = /* @__PURE__ */ new Set(["ready", "class", "spec", "chip", "ping", "rematch", "lobby"]);
var hostTypes = /* @__PURE__ */ new Set(["welcome", "error", "notice", "pong", "rematchVotes"]);
function validPacket(p, host) {
  if (!p || !Number.isSafeInteger(p.seq) || p.seq < 1 || !Number.isSafeInteger(p.ack) || p.ack < 0 || !Array.isArray(p.events) || p.events.length > RELAY.maxEvents) return false;
  let id = 0;
  for (const e of p.events) {
    if (!Number.isSafeInteger(e?.id) || e.id <= id || !e.message || !(host ? hostTypes : guestTypes).has(e.message.type)) return false;
    id = e.id;
    if (JSON.stringify(e.message).length > 4096) return false;
  }
  if (p.latest !== void 0) {
    if (host) {
      const s = p.latest;
      if (s?.type !== "snapshot" || !s.state || !Number.isFinite(s.state.tick) || !Array.isArray(s.state.players) || s.state.players.length > 8 || !Array.isArray(s.state.props) || s.state.props.length > 300) return false;
    } else {
      const latest = p.latest;
      const inputs = latest?.type === "inputs" ? latest.inputs : latest?.type === "input" ? [latest.input] : null;
      if (!Array.isArray(inputs) || !inputs.length || inputs.length > NETWORK.maxInputBatch) return false;
      if (latest.type === "inputs" && (typeof latest.epoch !== "string" || !/^\d+:\d+:\d+$/.test(latest.epoch))) return false;
      if (inputs.some((i, n) => !i || !["seq", "x", "z", "yaw", "pitch"].every((k) => typeof i[k] === "number" && Number.isFinite(i[k])) || !Number.isSafeInteger(i.seq) || i.seq < 0 || JSON.stringify(i).length > 2e3 || n > 0 && i.seq <= inputs[n - 1].seq)) return false;
    }
  }
  return true;
}

// src/shared/invite-url.ts
function publicInviteOrigin(value) {
  if (typeof value !== "string") return null;
  try {
    const url = new URL(value);
    return url.protocol === "https:" && !url.username && !url.password && !url.port && /^[-a-z0-9]+\.trycloudflare\.com$/.test(url.hostname) && url.pathname === "/" && !url.search && !url.hash ? url.origin : null;
  } catch {
    return null;
  }
}

// src/shared/room-directory.ts
var DIRECTORY_TTL_MS = 18e4;

// src/relay/directory.ts
var json = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "Access-Control-Allow-Origin": "*", "X-Content-Type-Options": "nosniff" } });
var hash = async (s) => [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
async function directory(request, db, now = Date.now()) {
  if (!db) return json({ error: "Room lookup is temporarily unavailable. Use an invite link." }, 503);
  const url = new URL(request.url), raw = url.pathname.slice("/api/directory/".length), code = normalizeRoomCode(raw);
  if (!code) return json({ error: "Enter the complete room code from your host." }, 400);
  if (request.method === "GET") {
    const row2 = await db.prepare("SELECT origin,expires FROM online_rooms WHERE code=? AND expires>?").bind(code, now).first();
    if (!row2) return json({ error: "Room code expired or was not found. Ask the host to keep their room open and copy its code." }, 404);
    return json({ code, origin: row2.origin, expires: row2.expires });
  }
  if (request.method !== "POST") return json({ error: "Method not allowed." }, 405);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) return json({ error: "Room registration must come from the host server." }, 403);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) return json({ error: "Invalid host session." }, 401);
  if (!request.headers.get("content-type")?.includes("application/json")) return json({ error: "Expected JSON." }, 415);
  const reader = request.body?.getReader();
  if (!reader) return json({ error: "Missing registration." }, 400);
  const decoder = new TextDecoder();
  let body2 = "", bytes = 0;
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    bytes += r.value.length;
    if (bytes > 2048) {
      await reader.cancel();
      return json({ error: "Registration too large." }, 413);
    }
    body2 += decoder.decode(r.value, { stream: true });
  }
  body2 += decoder.decode();
  let input;
  try {
    input = JSON.parse(body2);
  } catch {
    return json({ error: "Invalid JSON." }, 400);
  }
  const origin = publicInviteOrigin(input?.origin);
  if (!origin) return json({ error: "Invalid public game address." }, 400);
  const key = await hash(token), creator = await hash(request.headers.get("CF-Connecting-IP") || "local-preview");
  const row = await db.prepare("SELECT host_key,expires FROM online_rooms WHERE code=?").bind(code).first();
  if (row && row.expires > now && row.host_key !== key) return json({ error: "Room code is already in use. Create another room." }, 409);
  if (!row || row.expires <= now) {
    await db.prepare("DELETE FROM online_rooms WHERE code IN (SELECT code FROM online_rooms WHERE expires<=? LIMIT 100)").bind(now).run();
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM online_rooms WHERE expires>?").bind(creator, now).first();
    if (count.n >= 2048 || count.mine >= 12) return json({ error: "Too many active rooms. Use an invite link or close an old room." }, 429);
  }
  const expires = now + DIRECTORY_TTL_MS;
  const result2 = await db.prepare("INSERT INTO online_rooms(code,host_key,creator,origin,expires) VALUES(?,?,?,?,?) ON CONFLICT(code) DO UPDATE SET origin=excluded.origin,expires=excluded.expires,host_key=excluded.host_key,creator=excluded.creator WHERE online_rooms.host_key=excluded.host_key OR online_rooms.expires<=?").bind(code, key, creator, origin, expires, now).run();
  if (!result2.meta.changes) return json({ error: "Room code is already in use. Create another room." }, 409);
  return json({ code, expires });
}

// src/shared/config.ts
var CONFIG = {
  maxPlayers: 8,
  simulationHz: 60,
  snapshotHz: 20,
  wins: 5,
  roundsPerSet: 2,
  countdown: 3,
  resultSeconds: 5,
  selectionSeconds: 18,
  disconnectSeconds: 12,
  maxRoundSeconds: 100,
  speed: 6.8,
  acceleration: 35,
  jumpSpeed: 8.5,
  gravity: -23,
  dashSpeed: 15,
  dashDuration: 0.18,
  dashCooldown: 2.8,
  externalDrag: 3.1,
  maxExternalSpeed: 70,
  playerMass: 65,
  playerRadius: 0.42,
  playerHalfHeight: 0.43,
  minimumImpactSpeed: 3.6,
  safeEnergy: 45,
  damageScale: 0.035,
  maximumDamagePerImpact: 65,
  impactPairCooldown: 0.4,
  fragmentWindow: 0.35,
  fragmentDamageCap: 75,
  attributionSeconds: 7,
  manaRegen: 12,
  staminaRegen: 24,
  staminaRegenDelay: 0.45,
  shieldDrain: 33,
  shieldImpactCost: 0.9,
  blockDuration: 0.3,
  blockCooldown: 4,
  blockMaximumDuration: 0.65,
  blockMinimumCooldown: 1.5,
  blockCooldownChip: 0.85,
  blockWindowChip: 0.08,
  blockReflectDamageChip: 1.15,
  blockReflectSpeedChip: 1.1,
  blockPulseRadius: 3,
  blockPulseDamageChip: 6,
  blockPulseImpulseChip: 360,
  maxReflections: 3,
  maxReflectedDamage: 100,
  maxReflectedSpeed: 120,
  volleyDamage: 0.75,
  volleyFanDegrees: 7,
  followupDamage: 0.45,
  followupCost: 5,
  followupGap: 0.08,
  projectileLifetime: 3,
  projectileMaxTravel: 160,
  splitLifetime: 1.2,
  splitMaxTravel: 18,
  homingRange: 16,
  homingConeDegrees: 50,
  maxBodies: 150,
  maxProjectiles: 100,
  maxEvents: 80,
  maxInputRate: 100,
  staleInputSeconds: 0.3,
  maxRooms: 24
};

// src/relay/party.ts
var protocol = "chip-arena-party-v2";
var fail = (error, status = 400) => Object.assign(Error(error), { status });
var json2 = (body2, status = 200) => Response.json({ ...body2, protocol }, { status, headers: { "Cache-Control": "no-store" } });
var hash2 = async (s) => [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
async function party(request, db, now = Date.now()) {
  if (request.method !== "POST") throw fail("Method not allowed.", 405);
  const url = new URL(request.url);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) throw fail("Use this server to connect.", 403);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) throw fail("Invalid session.", 401);
  const reader = request.body?.getReader();
  if (!reader) throw fail("Missing request.");
  let bytes = 0, text = "";
  const decoder = new TextDecoder();
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    bytes += r.value.length;
    if (bytes > RELAY.maxHostBytes) {
      await reader.cancel();
      throw fail("Message too large.", 413);
    }
    text += decoder.decode(r.value, { stream: true });
  }
  text += decoder.decode();
  let input;
  try {
    input = JSON.parse(text);
  } catch {
    throw fail("Invalid JSON.");
  }
  const code = typeof input.code === "string" ? normalizeRoomCode(input.code) : null;
  if (!code || input.protocol !== protocol) throw fail("Reload the game and use a current room code.");
  const key = await hash2(token), action = url.pathname.split("/").at(-1);
  let room = await db.prepare("SELECT * FROM party_rooms WHERE code=?").bind(code).first();
  if (action === "create") {
    if (room) {
      if (room.host_key === key && !room.closed && room.expires > now) return json2({ code });
      throw fail("Code already in use.", 409);
    }
    const creator = await hash2(request.headers.get("CF-Connecting-IP") || "local");
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM party_rooms WHERE expires>? AND closed=0 AND host_seen>?").bind(creator, now, now - RELAY.timeoutMs).first();
    if (count.n >= 512 || count.mine >= 12) throw fail("Too many active rooms.", 429);
    await db.batch([db.prepare("DELETE FROM party_seats WHERE code IN (SELECT code FROM party_rooms WHERE expires<? OR closed=1)").bind(now), db.prepare("DELETE FROM party_rooms WHERE expires<? OR closed=1").bind(now), db.prepare("INSERT INTO party_rooms(code,host_key,creator,expires,host_seen) VALUES(?,?,?,?,?)").bind(code, key, creator, now + RELAY.ttlMs, now)]);
    return json2({ code, id: "host" }, 201);
  }
  if (!room || room.closed || room.expires <= now) throw fail("Room closed or not found.", 404);
  const host = room.host_key === key;
  if (!host && now - room.host_seen > RELAY.timeoutMs) throw fail("The host is offline.", 410);
  let seat = host ? null : await db.prepare("SELECT * FROM party_seats WHERE code=? AND seat_key=?").bind(code, key).first();
  if (action === "join") {
    if (host) throw fail("The host already has a seat.", 403);
    if (seat && !seat.has_left) return json2({ code, id: seat.id });
    if (seat?.has_left) throw fail("This session ended. Join again from the menu.", 410);
    const id = "p" + newRoomToken().slice(0, 16);
    const r = await db.prepare(`INSERT INTO party_seats(id,code,seat_key,profile,seen) SELECT ?,?,?,?,? WHERE EXISTS(SELECT 1 FROM party_rooms WHERE code=? AND phase='lobby' AND closed=0) AND (SELECT COUNT(*) FROM party_seats WHERE code=? AND has_left=0)<?`).bind(id, code, key, JSON.stringify(cleanProfile(input.profile)), now, code, code, CONFIG.maxPlayers - 1).run();
    if (!r.meta.changes) throw fail("Room is full or the match has started.", 409);
    return json2({ code, id });
  }
  if (!host && (!seat || seat.has_left)) throw fail("Seat is not authorized.", 403);
  if (action === "leave") {
    await db.prepare(host ? "UPDATE party_rooms SET closed=1 WHERE code=?" : "UPDATE party_seats SET has_left=1 WHERE id=?").bind(host ? code : seat.id).run();
    return json2({ ok: true });
  }
  if (action !== "exchange") throw fail("Unknown operation.", 404);
  if (host) {
    if (!Number.isSafeInteger(input.seq) || input.seq < 1 || !Array.isArray(input.peers) || input.peers.length > 7) throw fail("Invalid host packet.");
    if (input.snapshot && !validPacket({ seq: 1, ack: 0, events: [], latest: input.snapshot }, true)) throw fail("Invalid snapshot.");
    for (const peer of input.peers) if (typeof peer.id !== "string" || !validPacket(peer.packet, true) || peer.packet.latest) throw fail("Invalid peer packet.");
    const statements = [db.prepare("UPDATE party_rooms SET host_seen=?,host_seq=?,snapshot=COALESCE(?,snapshot),phase=COALESCE(?,phase) WHERE code=? AND host_key=? AND host_seq<=?").bind(now, input.seq, input.snapshot ? JSON.stringify(input.snapshot) : null, input.snapshot?.state?.phase || null, code, key, input.seq)];
    for (const peer of input.peers) statements.push(db.prepare("UPDATE party_seats SET host_packet=? WHERE id=? AND code=? AND EXISTS(SELECT 1 FROM party_rooms WHERE code=? AND host_seq=?)").bind(JSON.stringify(peer.packet), peer.id, code, code, input.seq));
    statements.push(db.prepare("UPDATE party_seats SET has_left=1 WHERE code=? AND seen<?").bind(code, now - RELAY.timeoutMs));
    statements.push(db.prepare("SELECT * FROM party_seats WHERE code=?").bind(code));
    const rows = (await db.batch(statements)).at(-1).results;
    return json2({ serverNow: now, peers: rows.map((p) => ({ id: p.id, profile: JSON.parse(p.profile), left: !!p.has_left, seen: p.seen, packet: p.guest_packet ? JSON.parse(p.guest_packet) : null })) });
  }
  if (bytes > RELAY.maxGuestBytes || !validPacket(input.packet, false)) throw fail("Invalid guest packet.");
  const result2 = await db.batch([db.prepare("UPDATE party_seats SET guest_packet=?,guest_seq=?,seen=? WHERE id=? AND guest_seq<=?").bind(JSON.stringify(input.packet), input.packet.seq, now, seat.id, input.packet.seq), db.prepare("SELECT host_packet FROM party_seats WHERE id=?").bind(seat.id), db.prepare("SELECT snapshot,host_seen,closed FROM party_rooms WHERE code=?").bind(code)]);
  room = result2[2].results[0];
  if (room.closed) throw fail("Host closed the room.", 410);
  return json2({ serverNow: now, packet: result2[1].results[0].host_packet ? JSON.parse(result2[1].results[0].host_packet) : null, snapshot: room.snapshot ? JSON.parse(room.snapshot) : null });
}

// src/shared/dev-auth.ts
var base64url = (b) => btoa(String.fromCharCode(...new Uint8Array(b))).replace(/=/g, "").replace(/\+/g, "-").replace(/\//g, "_");

// src/relay/auth.ts
var result = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" } });
var from64 = (s) => Uint8Array.from(atob(s), (c) => c.charCodeAt(0));
async function login(request, env, now = Date.now()) {
  if (request.method !== "POST") return result({ error: "Use the login form." }, 405);
  const origin = request.headers.get("origin");
  if (origin && origin !== new URL(request.url).origin) return result({ error: "Use this game\u2019s login form." }, 403);
  if (!env.DEV_ACCOUNT_VERIFIER || !env.DEV_SIGNING_KEY) return result({ error: "Developer login is not configured." }, 503);
  const text = await request.text();
  if (text.length > 2048) return result({ error: "Login request too large." }, 413);
  let input;
  try {
    input = JSON.parse(text);
  } catch {
    return result({ error: "Invalid login request." }, 400);
  }
  if (typeof input.username !== "string" || typeof input.password !== "string" || input.username.length > 64 || input.password.length > 128) return result({ error: "Enter your username and password." }, 400);
  const ip = request.headers.get("CF-Connecting-IP") || "local";
  const digest = base64url(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(ip)));
  for (const [id, limit, window] of [[digest, 6, 6e4], ["account", 45, 3e5]]) {
    const key2 = id + ":" + Math.floor(now / window);
    await env.DB.prepare("INSERT INTO dev_login_rates(id,hits,expires) VALUES(?,1,?) ON CONFLICT(id) DO UPDATE SET hits=hits+1").bind(key2, now + window * 2).run();
    const count = await env.DB.prepare("SELECT hits FROM dev_login_rates WHERE id=?").bind(key2).first();
    if (count.hits > limit) return result({ error: "Too many attempts. Try again in a few minutes." }, 429);
  }
  await env.DB.prepare("DELETE FROM dev_login_rates WHERE id IN (SELECT id FROM dev_login_rates WHERE expires<? LIMIT 50)").bind(now).run();
  const account = JSON.parse(env.DEV_ACCOUNT_VERIFIER);
  const material = await crypto.subtle.importKey("raw", new TextEncoder().encode(input.password), "PBKDF2", false, ["deriveBits"]);
  const actual = new Uint8Array(await crypto.subtle.deriveBits({ name: "PBKDF2", hash: "SHA-256", salt: from64(account.salt), iterations: account.iterations }, material, 256)), expected = from64(account.hash);
  let difference = actual.length ^ expected.length;
  for (let n = 0; n < actual.length; n++) difference |= actual[n] ^ (expected[n] || 0);
  if (difference || input.username !== account.username) return result({ error: "Username or password is incorrect." }, 401);
  const payload = base64url(new TextEncoder().encode(JSON.stringify({ user: "Sheldon", role: "dev", iat: now, exp: now + 8 * 36e5, nonce: crypto.randomUUID() }))), key = await crypto.subtle.importKey("jwk", JSON.parse(env.DEV_SIGNING_KEY), { name: "ECDSA", namedCurve: "P-256" }, false, ["sign"]);
  const signature = await crypto.subtle.sign({ name: "ECDSA", hash: "SHA-256" }, key, new TextEncoder().encode(payload));
  return result({ token: payload + "." + base64url(signature), user: "Sheldon", expires: now + 8 * 36e5 });
}

// src/shared/connection.ts
function socketEndpoint(value, origin) {
  if (typeof value !== "string" || !value) return null;
  try {
    const base = new URL(origin), url = new URL(value, origin);
    if (url.username || url.password) return null;
    if (value.startsWith("/")) url.protocol = base.protocol === "https:" ? "wss:" : "ws:";
    if (!["ws:", "wss:"].includes(url.protocol) || base.protocol === "https:" && url.protocol !== "wss:") return null;
    return url.toString();
  } catch {
    return null;
  }
}

// src/relay/worker.ts
var json3 = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" } });
var fail2 = (message, status = 400) => Object.assign(new Error(message), { status });
async function hash3(s) {
  return [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
}
async function body(request) {
  if (!request.headers.get("content-type")?.includes("application/json")) throw fail2("Expected JSON.", 415);
  const reader = request.body?.getReader();
  if (!reader) throw fail2("Missing request.");
  let size = 0;
  const chunks = [];
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    size += r.value.length;
    if (size > RELAY.maxHostBytes) {
      await reader.cancel();
      throw fail2("Room message is too large.", 413);
    }
    chunks.push(r.value);
  }
  const bytes = new Uint8Array(size);
  let at = 0;
  for (const c of chunks) {
    bytes.set(c, at);
    at += c.length;
  }
  try {
    return JSON.parse(new TextDecoder().decode(bytes));
  } catch {
    throw fail2("Invalid JSON.");
  }
}
async function exchange(db, input, code, key, host, now) {
  const packet = input.packet;
  if (!validPacket(packet, host)) throw fail2("Invalid room packet.");
  if (!host && JSON.stringify(input).length > RELAY.maxGuestBytes) throw fail2("Guest message is too large.", 413);
  const side = host ? "host" : "guest", other = host ? "guest" : "host";
  const result2 = await db.batch([
    db.prepare(`UPDATE relay_rooms SET ${side}_packet=?,${side}_seq=?,${side}_ack=?,${side}_seen=? WHERE code=? AND ${side}_key=? AND closed=0 AND expires>? AND ${side}_seq<=? AND (${side}_seen<=? OR ${side}_seq=?) ${host ? "" : "AND guest_left=0 AND host_seen>?"} `).bind(JSON.stringify(packet), packet.seq, packet.ack, now, code, key, now, packet.seq, now - 10, packet.seq, ...host ? [] : [now - RELAY.timeoutMs]),
    db.prepare(`SELECT * FROM relay_rooms WHERE code=? AND ${side}_key=?`).bind(code, key)
  ]);
  const row = result2[1].results[0];
  if (!row) throw fail2("Room closed or this seat is not authorized.", 403);
  if (row.closed || row.expires <= now) throw fail2("The host closed this room.", 410);
  if (!host && (row.guest_left || now - row.host_seen > RELAY.timeoutMs)) throw fail2("The host disconnected or this guest session has ended.", 410);
  if (packet.seq > row[side + "_seq"]) throw fail2("Room messages are arriving too quickly.", 429);
  return json3({ protocol: RELAY.protocol, packet: row[other + "_packet"] ? JSON.parse(row[other + "_packet"]) : null, ack: row[other + "_ack"], peerSeen: row[other + "_seen"], serverNow: now, guest: host && row.guest_profile ? JSON.parse(row.guest_profile) : null, guestLeft: row.guest_left === 1 });
}
async function relay(request, env, now = Date.now()) {
  const url = new URL(request.url);
  if (request.method !== "POST") throw fail2("Method not allowed.", 405);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) throw fail2("Use the Chip Arena website to connect.", 403);
  if (!env.DB) throw fail2("Room service is unavailable. Local practice still works.", 503);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) throw fail2("Invalid room session.", 401);
  const input = await body(request), code = typeof input?.code === "string" ? normalizeRoomCode(input.code) : null;
  if (!code || input.protocol !== RELAY.protocol) throw fail2("Reload the game and use a current 8-character room code.", 400);
  const db = env.DB, key = await hash3(token), action = url.pathname.split("/").at(-1);
  if (action === "exchange" && (input.role === "host" || input.role === "guest")) return exchange(db, input, code, key, input.role === "host", now);
  let row = await db.prepare("SELECT * FROM relay_rooms WHERE code=?").bind(code).first();
  if (action === "create") {
    if (row) {
      if (row.host_key === key && !row.closed && row.expires > now) return json3({ code, protocol: RELAY.protocol });
      throw fail2("Room code is taken. Create another room.", 409);
    }
    await db.prepare("DELETE FROM relay_rooms WHERE code IN (SELECT code FROM relay_rooms WHERE expires<? OR closed=1 LIMIT 100)").bind(now).run();
    const creator = await hash3(request.headers.get("CF-Connecting-IP") || "local-preview");
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM relay_rooms WHERE expires>? AND closed=0 AND host_seen>?").bind(creator, now, now - RELAY.timeoutMs).first();
    if (count.n >= 512 || count.mine >= 12) throw fail2("Too many active rooms. Close an old room and retry.", 429);
    await db.prepare("INSERT INTO relay_rooms(code,host_key,creator,expires,host_seen) VALUES(?,?,?,?,?)").bind(code, key, creator, now + RELAY.ttlMs, now).run();
    return json3({ code, protocol: RELAY.protocol }, 201);
  }
  if (!row || row.closed || row.expires <= now) throw fail2("Room closed or not found. Ask the host for a fresh invite.", 404);
  if (action === "join") {
    if (now - row.host_seen > RELAY.timeoutMs) throw fail2("The host is offline. Ask them to create a new room.", 410);
    if (row.host_key === key) throw fail2("The host cannot join its own guest seat.", 403);
    const result2 = await db.prepare("UPDATE relay_rooms SET guest_key=?,guest_profile=?,guest_seen=? WHERE code=? AND closed=0 AND guest_left=0 AND (guest_key IS NULL OR guest_key=?)").bind(key, JSON.stringify(cleanProfile(input.profile)), now, code, key).run();
    if (!result2.meta.changes) throw fail2("Room is full. Ask the host for a new room.", 409);
    return json3({ code, protocol: RELAY.protocol });
  }
  const host = row.host_key === key;
  if (!host && row.guest_key !== key) throw fail2("This room session is not authorized.", 403);
  if (action === "leave") {
    await db.prepare(host ? "UPDATE relay_rooms SET closed=1 WHERE code=?" : "UPDATE relay_rooms SET guest_left=1 WHERE code=?").bind(code).run();
    return json3({ ok: true });
  }
  if (action !== "exchange") throw fail2("Unknown room operation.", 404);
  return exchange(db, input, code, key, host, now);
}
var worker_default = { async fetch(request, env) {
  if (new URL(request.url).pathname.startsWith("/api/directory/")) {
    try {
      return await directory(request, env.DB);
    } catch {
      return Response.json({ error: "Room lookup is temporarily unavailable. Use an invite link." }, { status: 503, headers: { "Cache-Control": "no-store", "Access-Control-Allow-Origin": "*" } });
    }
  }
  if (new URL(request.url).pathname === "/api/auth/login") {
    try {
      return await login(request, env);
    } catch {
      return json3({ error: "Login temporarily unavailable." }, 503);
    }
  }
  if (new URL(request.url).pathname === "/api/network") {
    const endpoint = socketEndpoint(env.MULTIPLAYER_URL, new URL(request.url).origin);
    return json3({ transport: endpoint ? "websocket" : "https-relay", endpoint });
  }
  if (new URL(request.url).pathname.startsWith("/api/rooms/")) {
    try {
      return new URL(request.url).pathname.startsWith("/api/rooms/v2/") ? await party(request, env.DB) : await relay(request, env);
    } catch (e) {
      if (!e.status) console.error("Chip Arena relay failed", e.message);
      return json3({ error: e.status ? e.message : "Room service interrupted. Retrying\u2026" }, e.status || 503);
    }
  }
  return env.ASSETS ? env.ASSETS.fetch(request) : new Response("Not found", { status: 404 });
} };
export {
  worker_default as default,
  relay
};
