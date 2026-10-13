# Kill-cam systems and Siren super armor

Owner request: a quick replay of how the local player died, Siren super armor during abilities, and Claude to own kill-cam UI design.

## Branch and ownership

- Systems branch: `codex/killcam-siren-armor`, from shared `claude/visual-revision` at `4dbb1513a2f8d6c782cfddb5b8ee91fad1dd9d47`.
- Read AGENTS.md and the newest DEV_LOG entry. Fetch this task branch before building on it; do not replace shared history with a source ZIP or the local verification snapshot.
- Astra owns replay capture/lifecycle and combat rules. Claude owns final kill-cam UI, camera presentation and transitions. Final UI: `src/client/killcam-hud.ts` + `src/client/killcam.css`; replay-only shoulder camera: `src/client/killcam-camera.ts` (see the Claude DEV_LOG entry). `KillcamInfo.at` (clamped fatal-hit time) feeds the timeline marker.
- Implemented on the task branch. Not merged, not published. The standing test-channel instruction applies after an approved shared-branch merge; this request does not authorize an automatic merge. No changes to update feeds, accounts or settings.

## Replay behavior and contract

- Unity final killcam (2026-10-13, `docs/FINAL-KILLCAM.md`): when a round ends on a kill, every client plays that kill in slow motion through this same presentation scene (`ReplayActive` is true). Once the final kill is decided, no normal killcam arms that round, and a normal one playing or waiting is cut over, so the last victim sees only the final one. Every other death keeps the behaviour below.
- Automatic, once per witnessed local death, in both local practice and online rooms. Records authoritative snapshots, not predicted input or video. No new network messages, server rewind, recorded files or upload traffic.
- Keeps up to five seconds at 20 Hz, at most 112 snapshots / 100,000 top-level player, prop, projectile and event entries. The active clip retains its bounded frame references while live history continues. Minimum useful prehistory is 0.2 seconds. A late-joining dead spectator never replays an unwitnessed death.
- Plays up to three seconds before the fatal hit plus 0.6 seconds afterward, at normal speed. Waits briefly for the aftermath; a one-second wall-clock deadline handles stopped packets. Missing history shortens the clip.
- Third-person view follows the recorded killer's yaw/pitch. Environmental/self deaths or absent killers fall back to the recorded victim. This is a reconstruction of the victim client's received snapshots, not a guaranteed exact recording of the killer's screen. Dropped packets can leave gaps; cosmetic physics/particles are reconstructed.
- Space, Escape or `data-action="killcam-skip"` skip; arrow keys skip and select a spectator target. Completion returns to live spectating, or the applicable results screen. Round/map/generation changes, respawn/heal, lobby return, disconnect, pause/menu exit and hiding the page cancel playback. Drafts and the next round always take priority. The match continues throughout replay.
- `Killcam.observe(snapshot, localId, nowMs)` consumes accepted live snapshots. `sample(liveSnapshot, localId, nowMs)` yields the historical presentation frame or undefined. Never assign its result to main's live `state`, reconcile the predictor from it, or change the local input identity.
- `Killcam.info` exposes `deathId`, `victimId`, `killerId`, `killerName`, `cause`, `start`, `end`, `time`, normalized `progress` and the actual `viewId`. `active`, `waiting`, `skip()` and `clear()` support presentation controls. `killcamHud(info)` is the UI seam. Names must remain HTML-escaped.
- HP, death records, ability/attack states, prop destruction and projectile membership come from the historical frame at or before playback time. Only transforms interpolate; no future live state is mixed into the clip.
- Main continues consuming networking and generating input for the real local identity. Prediction is excluded from replay rendering. Renderer/audio temporal state clears at both boundaries; the saved camera preference is restored. Replay suppresses live hitmarkers, floating numbers, result panels and cinematic camera overrides. Preserve those boundaries when adding transitions.
- Existing diagnostics expose `window.chipArena.getKillcam()` and `skipKillcam()` alongside `getState()`. They do not grant combat authority.

## Siren armor

- All eight **numbered abilities** gain super armor from their accepted start through recovery (`move.at <= time < move.until`). Includes Marked's lock and Last Laugh's extended hold. Primary Claw/Hellfire, mode switching and lingering rain/fire/seeker effects do not extend armor.
- Full HP damage, elemental DOT, wounds and lethal outcomes still apply. Armor rejects new hit reactions, melee/block-rebound stun, knockdown, juggle, impulse/pull/launch effects, relevant Chip forces and Ward Zero vent launch. Lethal damage still cancels the ability/releases a held victim.
- Existing locomotion, collision/gravity, prior momentum and scripted possession/fear/teleports are not invulnerability mechanics. Armor does not grant damage reduction, new i-frames or immunity to those scripted controls.
- `sirenSuperArmor(playerView, authorityTime)` in `src/shared/siren-motion.ts` is the pure presentation helper. `SirenSystem.armored(player)` additionally enforces authoritative fight/access checks. No new snapshot fields are necessary. If adding an armor visual, consume this helper instead of inventing another timer.
- No damage values, hit timing, hitboxes, cooldowns, resource costs, access grants or account/save schema changed. The intentional gameplay change is interruption/forced-movement resistance during those ability windows.

## Verification and visual acceptance

- Node 24.19.0/npm 11.9.0, real Rapier 0.19.3. All 549 base files were verified against the remote Git blob hashes before changes; installed dependencies reuse the preceding real-Rapier verification environment and the unchanged lockfile.
- `node --import tsx --test tests/killcam.test.ts tests/siren-super-armor.test.ts`: 14/14 pass, including nested snapshot isolation, bounded history, death dedupe, historical topology, missing killers, packet stalls, reset/skip behavior, all ability windows, damage/DOT, melee/DEV launches, blocked attacks, Chip forces, vents and lethal grab release.
- `node --import tsx tests/killcam-network.ts`: three real loopback WebSocket consumers, 75 identical snapshots; victim-only replay, continued live packets, unmodified authority, armor damage/no launch, skip and round reset.
- Existing `tests/siren-network.ts`: three clients, 89 matching snapshots; team preservation, DEV access/bot loadouts, primary/mode/Marked, instant bot Chip awards and song continuity.
- Typecheck and client Vite build pass. Full unit suite: 678/690, compared with 664/676 on the unchanged shared gameplay base; exactly the same 12 failure names. `npm run verify` still fails on those known tests, four existing Rapier/NodeNext server type errors and generic WebSocket rematch timeout. See DEV_LOG for HTTPS results.
- One Node diagnostic with eight players / 262 props retained 101 frames (~18.8 MiB heap delta), averaging 1.24 ms/capture and 0.046 ms/interpolated sample. This is not a browser/GPU or worst-case performance benchmark.
- Rendered smoke test blocked: Cloud Browser returned `net::ERR_BLOCKED_BY_CLIENT` for local Vite. No installed local Chromium, native Windows/GPU or physical two-PC acceptance was available. Do not claim UI, audio or camera quality was visually verified.

Claude / Windows review:

1. Replace the placeholder UI; keep skip available without pointer lock, clearly label replay and avoid covering the fatal hit. Review camera transitions, historical projectile visibility and audio timing at high and performance graphics settings.
2. Die to ranged, melee, DOT, falling and Siren Last Laugh; watch from first-person and third-person preferences. Check that wounds, destruction, corpse effects and trails rewind, then return to the current world without leftovers. Exact cosmetic particles need not match.
3. Try FFA/Teams with surviving players, simultaneous kills, killer disconnect/death, an immediate result, automatic bot drafts, round reset, lobby return and a backgrounded window. A replay must never delay selections/respawn or control another player.
4. Exercise every Siren ability against shield/sword launches, Ansatsuken, EWGF, Chip pulls and vents. Damage must apply, abilities should finish, idle/primary Siren should still react, and killing Siren during Last Laugh must release its victim.
5. Rerun the focused checks/types and document visual acceptance. Merge/publication remain separate; retain the recorded baseline failures when preparing any diagnostic test update.
