// src/shared/relay-protocol.ts
var RELAY = { protocol: "chip-arena-https-v1", intervalMs: 50, inputHoldSeconds: 0.75, timeoutMs: 12e3, requestTimeoutMs: 4e3, maxEvents: 48, maxHostBytes: 384e3, maxGuestBytes: 24e3, ttlMs: 72e5 };
function normalizeRoomCode(value) {
  const code = value.trim().toUpperCase().replace(/[\s-]/g, "");
  return /^[A-HJ-NP-Z2-9]{8}$/.test(code) ? code : null;
}
function cleanProfile(p) {
  return { name: typeof p?.name === "string" ? p.name.replace(/[<>]/g, "").slice(0, 20) || "Pilot" : "Pilot", class: ["ranged", "magic", "melee"].includes(p?.class) ? p.class : "ranged", color: /^#[0-9a-f]{6}$/i.test(p?.color || "") ? p.color : "#97e4d0" };
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
      if (s?.type !== "snapshot" || !s.state || !Number.isFinite(s.state.tick) || !Array.isArray(s.state.players) || s.state.players.length > 2 || !Array.isArray(s.state.props) || s.state.props.length > 300) return false;
    } else {
      const i = p.latest?.input;
      if (p.latest?.type !== "input" || !i || !["seq", "x", "z", "yaw", "pitch"].every((k) => typeof i[k] === "number" && Number.isFinite(i[k])) || !Number.isSafeInteger(i.seq) || i.seq < 0 || JSON.stringify(i).length > 2e3) return false;
    }
  }
  return true;
}

// src/relay/worker.ts
var json = (body2, status = 200) => Response.json(body2, { status, headers: { "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" } });
var fail = (message, status = 400) => Object.assign(new Error(message), { status });
async function hash(s) {
  return [...new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(s)))].map((n) => n.toString(16).padStart(2, "0")).join("");
}
async function body(request) {
  if (!request.headers.get("content-type")?.includes("application/json")) throw fail("Expected JSON.", 415);
  const reader = request.body?.getReader();
  if (!reader) throw fail("Missing request.");
  let size = 0;
  const chunks = [];
  while (true) {
    const r = await reader.read();
    if (r.done) break;
    size += r.value.length;
    if (size > RELAY.maxHostBytes) {
      await reader.cancel();
      throw fail("Room message is too large.", 413);
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
    throw fail("Invalid JSON.");
  }
}
async function exchange(db, input, code, key, host, now) {
  const packet = input.packet;
  if (!validPacket(packet, host)) throw fail("Invalid room packet.");
  if (!host && JSON.stringify(input).length > RELAY.maxGuestBytes) throw fail("Guest message is too large.", 413);
  const side = host ? "host" : "guest", other = host ? "guest" : "host";
  const result = await db.batch([
    db.prepare(`UPDATE relay_rooms SET ${side}_packet=?,${side}_seq=?,${side}_ack=?,${side}_seen=? WHERE code=? AND ${side}_key=? AND closed=0 AND expires>? AND ${side}_seq<=? AND (${side}_seen<=? OR ${side}_seq=?) ${host ? "" : "AND guest_left=0 AND host_seen>?"} `).bind(JSON.stringify(packet), packet.seq, packet.ack, now, code, key, now, packet.seq, now - 10, packet.seq, ...host ? [] : [now - RELAY.timeoutMs]),
    db.prepare(`SELECT * FROM relay_rooms WHERE code=? AND ${side}_key=?`).bind(code, key)
  ]);
  const row = result[1].results[0];
  if (!row) throw fail("Room closed or this seat is not authorized.", 403);
  if (row.closed || row.expires <= now) throw fail("The host closed this room.", 410);
  if (!host && (row.guest_left || now - row.host_seen > RELAY.timeoutMs)) throw fail("The host disconnected or this guest session has ended.", 410);
  if (packet.seq > row[side + "_seq"]) throw fail("Room messages are arriving too quickly.", 429);
  return json({ protocol: RELAY.protocol, packet: row[other + "_packet"] ? JSON.parse(row[other + "_packet"]) : null, ack: row[other + "_ack"], peerSeen: row[other + "_seen"], serverNow: now, guest: host && row.guest_profile ? JSON.parse(row.guest_profile) : null, guestLeft: row.guest_left === 1 });
}
async function relay(request, env, now = Date.now()) {
  const url = new URL(request.url);
  if (request.method !== "POST") throw fail("Method not allowed.", 405);
  if (request.headers.get("origin") && request.headers.get("origin") !== url.origin) throw fail("Use the Chip Arena website to connect.", 403);
  if (!env.DB) throw fail("Room service is unavailable. Local practice still works.", 503);
  const token = request.headers.get("authorization")?.replace(/^Bearer /, "");
  if (!/^[a-f0-9]{64}$/.test(token || "")) throw fail("Invalid room session.", 401);
  const input = await body(request), code = typeof input?.code === "string" ? normalizeRoomCode(input.code) : null;
  if (!code || input.protocol !== RELAY.protocol) throw fail("Reload the game and use a current 8-character room code.", 400);
  const db = env.DB, key = await hash(token), action = url.pathname.split("/").at(-1);
  if (action === "exchange" && (input.role === "host" || input.role === "guest")) return exchange(db, input, code, key, input.role === "host", now);
  let row = await db.prepare("SELECT * FROM relay_rooms WHERE code=?").bind(code).first();
  if (action === "create") {
    if (row) {
      if (row.host_key === key && !row.closed && row.expires > now) return json({ code, protocol: RELAY.protocol });
      throw fail("Room code is taken. Create another room.", 409);
    }
    await db.prepare("DELETE FROM relay_rooms WHERE code IN (SELECT code FROM relay_rooms WHERE expires<? OR closed=1 LIMIT 100)").bind(now).run();
    const creator = await hash(request.headers.get("CF-Connecting-IP") || "local-preview");
    const count = await db.prepare("SELECT COUNT(*) AS n, SUM(CASE WHEN creator=? THEN 1 ELSE 0 END) AS mine FROM relay_rooms WHERE expires>? AND closed=0 AND host_seen>?").bind(creator, now, now - RELAY.timeoutMs).first();
    if (count.n >= 512 || count.mine >= 12) throw fail("Too many active rooms. Close an old room and retry.", 429);
    await db.prepare("INSERT INTO relay_rooms(code,host_key,creator,expires,host_seen) VALUES(?,?,?,?,?)").bind(code, key, creator, now + RELAY.ttlMs, now).run();
    return json({ code, protocol: RELAY.protocol }, 201);
  }
  if (!row || row.closed || row.expires <= now) throw fail("Room closed or not found. Ask the host for a fresh invite.", 404);
  if (action === "join") {
    if (now - row.host_seen > RELAY.timeoutMs) throw fail("The host is offline. Ask them to create a new room.", 410);
    if (row.host_key === key) throw fail("The host cannot join its own guest seat.", 403);
    const result = await db.prepare("UPDATE relay_rooms SET guest_key=?,guest_profile=?,guest_seen=? WHERE code=? AND closed=0 AND guest_left=0 AND (guest_key IS NULL OR guest_key=?)").bind(key, JSON.stringify(cleanProfile(input.profile)), now, code, key).run();
    if (!result.meta.changes) throw fail("Room is full. Ask the host for a new room.", 409);
    return json({ code, protocol: RELAY.protocol });
  }
  const host = row.host_key === key;
  if (!host && row.guest_key !== key) throw fail("This room session is not authorized.", 403);
  if (action === "leave") {
    await db.prepare(host ? "UPDATE relay_rooms SET closed=1 WHERE code=?" : "UPDATE relay_rooms SET guest_left=1 WHERE code=?").bind(code).run();
    return json({ ok: true });
  }
  if (action !== "exchange") throw fail("Unknown room operation.", 404);
  return exchange(db, input, code, key, host, now);
}
var worker_default = { async fetch(request, env) {
  if (new URL(request.url).pathname.startsWith("/api/rooms/")) {
    try {
      return await relay(request, env);
    } catch (e) {
      if (!e.status) console.error("Chip Arena relay failed", e.message);
      return json({ error: e.status ? e.message : "Room service interrupted. Retrying\u2026" }, e.status || 503);
    }
  }
  return env.ASSETS ? env.ASSETS.fetch(request) : new Response("Not found", { status: 404 });
} };
export {
  worker_default as default,
  relay
};
