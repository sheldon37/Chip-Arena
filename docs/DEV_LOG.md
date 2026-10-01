# Chip Arena — development log

Read recent entries and relevant open PRs before starting work. Keep entries concise,
newest first; use Git diffs and tests as implementation evidence. Entries on task
branches are not automatically visible in other checkouts. Check GitHub for current
merge/release status; an entry records status at the time it was written.

For every completed task or unfinished handoff, record:
- Date, agent, task, branch, base commit, and PR or implementation commit when known.
- Changes and rationale; relevant files/systems and compatibility impacts.
- Actual checks with passed/failed/not-run outcomes and environment limitations.
- Remaining issues, overlapping work/dependencies, and the exact next step.
- Status: implemented on branch / merged / released (release version only if verified).
For animation/combat work explicitly state whether hit timing, hitboxes, damage,
cooldowns, movement rules, or network behavior changed. Never invent a commit,
PR, test result, release, or earlier log history. Git history identifies the commit
containing an entry; no self-referential commit hash is required.


## 2026-10-01 — Astra — cutscene integration, DEV bots, abilities and Siren HP

- Owner requested merging `claude/siren-cutscene` `452bbe6` into the shared branch,
  removal of dropped voice hooks, DEV bot loadouts, 15,000 Siren HP, and bot weapon
  abilities. Task branch `codex/siren-cutscene-dev-bots` starts at shared `dd8bb6e`
  and includes Claude's exact `452bbe610403000d02bb9352e2ab62e3ed12637d` commit/tree.
  Claude's visual, contract, lab and cutscene-test files remain unchanged.
- Removed laugh/You file loading, playback and event hooks from SirenAudio and
  its caller, plus the obsolete intro-laugh test. Music and lock-on beeps remain;
  shared event names still drive visuals. Claude's camera/HUD wiring is retained.
- Bot selectors in local practice and the host's room lobby show ??? only with
  verified DEV access. Authority permission comes from the actual configuring
  player, not request fields, item grants or a DEV guest. Bots receive only the
  selected weapon grant, never a DEV role/account identity. Duplicate Sirens fall
  back to ordinary random weapons; humans and bots share the existing slot limit.
- Active bots now choose normal E abilities, Emperor electric/wavedash, Ansatsuken
  Hadoken/uppercut/Tatsumaki, L33T Root Command and Siren's mode switch/all eight
  abilities. Basic attacks release during recovery so specials can get the next
  ready frame. Close-range casters approach into ability range. All decisions use
  the normal input/combat path, respecting visibility, resources and cooldowns;
  dummy bots remain inactive. Random loadouts continue to use ordinary weapons.
- Siren base HP is 15,000 for players/bots, before normal HP Chip modifiers.
  Saved loadouts, specialization, rounds/rematches and sandbox stat rebuilds agree.
  Animation impact: Claude's change is presentation-only. This follow-up changes
  HP, bot choices and bot approach movement; attack damage, hit timing, hitboxes,
  cooldown values, movement physics and snapshot/input schemas remain unchanged.
- Implementation `8979723`, PR #11. Final checks (Node 24.19.0/npm 11.9.0): types pass; units
  613/625, with exactly the same 12 failure names as the previous verified source.
  All 57 Siren/cutscene/DEV-bot tests pass. Client and HTTPS relay build; existing
  four server TypeScript errors remain. Three real WebSocket clients agreed on 84
  snapshots, including DEV bot grants, guest refusal, duplicate Siren refusal,
  15,000 HP, melee, mode, Marked and music continuity. Full `npm run verify` also
  reproduced the known generic WebSocket rematch timeout. HTTPS passed five sets,
  four drafts, 3,505 matching snapshots, retry, rematch and leave (177.6 s).
  Overall verification remains nonzero only for the documented baseline failures.
- Status: implemented and verified on task branch / PR #11 for the owner-requested merge.
  No new test-channel or live publication. Published content remains
  `0.2.149269b37b5b`. Native Windows/GPU, visual/audio acceptance and physical two-PC
  testing remain unrun. Next: owner-requested shared-branch merge and Windows playtest;
  publication of these follow-up changes remains separate from the prior test release.

## 2026-10-01 — Claude — Siren: silent smile and Last Laugh cutscene

- Owner request after testing the merged Siren: no voice lines or laughter (the intro just smiles), and the Last
  Laugh grab becomes a cutscene for the player being killed and for the Siren's player. Branch `claude/siren-cutscene`,
  base `claude/visual-revision` `dd8bb6e` (Astra's PR #8 merge plus #9/#10 records; reviewed, no overlap problems).
- Smile: intro bar 4 and the Last Laugh windup no longer shake or laugh; the grin draws open slowly, the head creeps
  over, the eyes narrow, and the windup soot rings slow to one per 0.7 s. Same timings and cues.
- Cutscene: new `src/client/siren/cutscene.ts`, wired in `renderer.ts`, `main.ts` and `style.css`. Letterboxed shots
  from the catch: lift, close-up (victim: the grin from below; Siren: its grin from above, past the victim), grip
  two-shot, crush from high on the front right with a jolt. The victim's continues 1.75 s past the crush as their death
  camera; the Siren player's ends at `releaseAt`, when its movement unlocks; a hold broken before the crush cuts back.
  HUD and combat numbers fade, overhead health and the first-person view model hide, and mouse look is ignored while it
  runs (the Siren is locked in the move; this stops the held victim swinging under a directed camera). Other players
  are unaffected. The camera eases after the Siren's facing.
- Impact: camera, HUD and presentation only. No hit timing, hitboxes, damage, cooldowns, movement rules, network
  schema or snapshot fields changed. Astra's optional `siren-laugh`/`siren-you` clip hooks in `siren-audio.ts` stay
  silent without files; per the owner they should not be added (removing the hooks is a systems-file call).
- Checks: tsx `tests/siren-cutscene.test.ts` (7 new), `siren-visuals`, `siren-contract` 25/25 pass. Scratch typecheck
  of the changed files against three r180: same error set as base, none in changed files. Lab renders of both roles
  inspected shot by shot (`times=` lab parameter added). Not run here: full units, `verify`, Vite build, Astra's
  rapier-based Siren tests, in-game Windows and multiplayer checks (npm registry unreachable; rapier unavailable).
- Status: implemented on branch and pushed; not merged or published. Next: owner/Astra review and merge into
  `claude/visual-revision`, then an in-game check as both the victim and the Siren (optionally a test-channel update).

## 2026-10-01 — Astra — Siren test Update publication

- Owner requested the in-game Update button delivery for testing. Built the exact
  shared source `abb0bbb5294e0299520be2e265ad32dd4c595752` in a separate worktree;
  source/assets/tests match the verified Siren integration `1a8a5d0`. Publication
  documentation branch: `codex/siren-test-publication`, base `abb0bbb`.
- Published content `0.2.149269b37b5b`, build `6b9d6a07736b51c965dd7c77`, to
  `sheldon37/chip-arena-test-updates` at
  `3e9d5d92efaa585ab63a62961abfcf822deea728`. Payload: 22,896,921 bytes / 102 files;
  SHA256 `70ab73d6b2f07ae804d605f38b33f3c94e79d68c0eb976f5f512981fd8b02aa3`.
  Workflow run `36814727228` verified hashes and atomically published feed/history;
  its temporary branch was removed. Main contains only feed, README and history.
- Node 24.19.0/npm 11.9.0: clean restore; two `release:prepare` builds produced
  identical feed bytes. PR #5 actual payload checker passed installation, all file
  hashes, same-version skip, offline/corrupt checksum refusal and settings sentinel.
  Channel/status tests 6/6; auth/manual updater tests 7/7. Original MP3 hash verified.
- Actual network updater installed previous `0.2.f3f24aed59e8` from immutable
  channel `873e23f`, then upgraded from main to Siren. All 102 installed hashes,
  preserved settings sentinel, same-version skip and menu version/build passed.
  The old seed manifest needed two timeout retries; the Siren upgrade succeeded.
- Reused exact-code integration verification: Siren 42/42, full units 598/610
  (same 12 baseline failures), three-client Siren 80 matching snapshots and HTTPS
  3,509 matching snapshots. Existing four server TS errors and generic WebSocket
  rematch timeout remain; overall broad verification is not green. This is the
  owner's diagnostic test build, not a claim of native acceptance.
- No gameplay, hit timing, hitboxes, damage, cooldowns, movement or networking
  changed during publication. Windows Electron/GPU, audible sync, visual quality
  and two physical PCs remain untested; optional laugh/You clips remain absent.
  No new seed ZIP was needed. Live feed and Site unchanged.
- Status: published to the existing test channel. Next: existing test installations
  press Update, select Siren under DEV-only ???, and perform SIREN-SYSTEMS Windows
  acceptance. This documentation follow-up is whitespace/scope checked only.

## 2026-10-01 — Astra — owner-authorized Siren merge

- Owner asked to merge after assessment. PR #8 (`codex/siren-boss`, verified head
  `1a8a5d0`) merged into `claude/visual-revision` as `9f010f5aae7dda46c7c1942e85764fdc3c78fd8e`.
  Readback confirms the merge tree exactly matches the previously tested tree;
  no gameplay, assets or networking changed during merge.
- Reused the completed checks from the prior integration entry: 42/42 Siren tests,
  three-client Siren 80 matching snapshots, HTTPS 3,509 matching snapshots;
  full units 598/610 with the same 12 baseline failures. Client/relay builds pass;
  existing server TS errors and WebSocket rematch timeout remain. GitHub scope
  passed; its full CI rerun was still running at merge. No green CI claim.
- This follow-up on `codex/siren-merge-status` updates documentation only;
  whitespace checked. No additional code tests required for the identical tree.
- Status: merged, not published. Windows/GPU/visual/audio acceptance remains
  unrun. No update-channel/live-feed publication was requested or performed.
  Next: owner playtest/release decision; optional laugh/You clips still pending.

## 2026-10-01 — Astra — integrate Claude's Siren visuals into PR #8

- Continuing `codex/siren-boss` / PR #8 from systems `514d784`; imported Claude's
  `8062eb2` and `2e986f4` visual commits. Shared target remains `claude/visual-revision`
  at `cf5fa1c`. Both DEV_LOG entries preserved; Claude-owned visual/contract/lab
  files and tests match `2e986f4` exactly.
- Completed the current `docs/SIREN.md` handoff: exclude world/first-person/intro
  Siren armor halos, apply effects and Ward Zero lighting after the current camera,
  skip the generic intro camera and preserve viewport aspect, call first-person
  restoration for ordinary specs. Fixed first-person rig churn: the world-rig
  marker does not exist on Claude's view model, so rebuild only on spec transition.
- Lock beeps now use Claude's crosshair pulse schedule, with 80 ms audio lookahead,
  deduplication, stale-cue rejection and interrupted-lock cancellation. Optional
  intro laugh moves from reveal (3.9184 s) to smile (6.0082 s). Voice assets still
  await the owner; the original song is already present.
- Gameplay impact: Black Rain height 9 -> 8 m above the landing point and downward
  speed 11.25 -> 10 m/s, keeping the 0.8 s fall/marker window. Damage, cooldowns,
  input/network schema, other move timings and collision radii are unchanged.
- Checks: types pass; all 42 Siren tests pass, including four new integration
  regressions (120-frame view-model reuse/spec restoration, intro aspect/glow,
  exact shared beep timing/cancellation, and smile-cue laugh). Full units 598/610;
  exactly the same 12 failure names as the prior branch/clean shared base. Client
  and relay builds pass; the same four server TS errors remain. General WebSocket
  suite reproduces the known rematch timeout. HTTPS passed five sets,
  four drafts, 3,509 matching snapshots, retry/rematch/leave (177.4 s).
- Three real Siren WebSocket clients agree on 80 snapshots, including access and
  duplicate refusal, melee, mode, Marked and music continuity. Overall verify is
  still red due to baseline failures. No Windows/GPU/in-game visual pass, audible
  sync assessment, physical two-PC check or packaging/updater test performed.
- Status: combined on task branch / PR #8; not merged into the shared branch or
  published. Next: owner Windows visual/audio acceptance, optional voices, then
  owner-authorized merge/publication. Handoff: `docs/SIREN-SYSTEMS.md`.

## 2026-10-01 — Astra — Siren systems and DEV selection category

- Branch `codex/siren-boss`, requested base `cf5fa1c5548e66d1364c98f9ea053202f918ec50`
  on `claude/visual-revision`. Imported Claude's contract `84b692f` before writing
  snapshot/audio/façade integration. Claude-owned files and contract tests unchanged.
- Added owner-requested fourth category `???` (verified DEV only), retaining the
  original weapon resource classes, old loadouts and exact grants. Added Siren grants,
  authority-enforced one-connected-Siren rule in all selection paths, TAKEN UI,
  Q/F/V mode binding, 1–4 abilities, HUD and DEV beat overlay. Siren forces FFA/Ward
  Zero, last intro, and continuous authoritative boss music; public rotation stays
  at 14 arenas. Original owner MP3 included and credited Xtrr - musiccreator.
- All ten move mechanics, seeded rain, capped fire patches, telegraphed launches,
  dodgeable grab/head crush, destructible-piercing/foundation-stopped homing shot,
  death/disconnect/reset cleanup, shared movement prediction, façade routing for
  world/first-person/lobby/intro/effects/deaths, optional voice hooks and lock beeps.
  Siren capsule locomotion stays standard with committed roots/rush/lunge; hover
  belongs to Claude. No replacement/placeholder visuals authored by Astra.
- Combat/network impact is intentional for Siren: shared contact/recovery times,
  damage/ranges/cooldowns in `siren-content.ts`, optional input/player/world/shot/
  song snapshot fields. Marked defaults to 85 damage, Last Laugh instant head crush
  at catch +3.2 s. Generic chip procs/attack-rate timing do not all apply to this
  custom boss kit; exact tuning/limits and manual checks in `SIREN-SYSTEMS.md`.
  Existing weapon mechanics retained; DEV resource lookups now use weapon class.
- Checks on Node 24.19.0/npm 11.9.0: types pass; focused Siren/rig 37/37; final unit
  587/599 with exactly the same 12 failures as clean base (555/567). Client/relay
  build passes, four known server TS errors remain. Full verify attempted: generic
  WebSocket rematch timeout remains; HTTPS passed five sets/four drafts/3,497 agreed
  snapshots, retry/rematch/leave (177.6 s). Fixed initial integration failures then
  reran type/unit/build stages. Overall verify remains red. Existing tests updated
  only for authorized DEV fixtures/new intro count and Siren's separate hover,
  camera and music paths; no contract tests weakened or changed.
- Three real Siren WebSocket clients passed access/duplicate refusal, shared melee,
  mode, Marked seeker and song continuity with 80 identical snapshots. Native Windows,
  finished visual quality, real-hardware/audio sync and physical two-PC tests unrun.
- Next: owner/Claude integrate `claude/visual-revision-siren`, provide optional
  laugh/You voice files, perform Windows acceptance, then owner decides merge/release.
  Current façade is Claude's minimal contract implementation, not finished visuals.
  Status: implemented/tested on task branch; not merged or published. Updater, public
  test channel, live Site/feed, accounts database and installation untouched.

## 2026-10-01 — Claude — Siren DEV boss visuals (first pass)

- Branch `claude/visual-revision-siren` on top of the contract (`84b692f`). New files only under
  `src/client/siren/**` and `tools/siren-lab/**`, plus the owned contract files; no existing file changed except this log.
- Apparition (black-flame body, rim, flame tongues, leg wisps, hover, blank/angry eyes, jagged grin, claws and palm
  orbs), the full floating stance and all ten moves keyed to `SirenMoveState` times, effects for every move and world
  state, the Last Laugh hold/char/head crush, the Siren's own death, first-person claws, the dev-siren intro scene and
  Ward Zero's dressing with beat/section-driven lights, beacons, painted grins, giant eyes and vent visuals.
- `SIREN_STAGE_THEME.floor` lightened to `0x2a2731` for readability; `SIREN_VENTS` exported (additive).
- Checks: `tests/siren-contract.test.ts` + `tests/siren-visuals.test.ts` 18/18 (tsx; every move animates to finite
  poses, effects create/retire pieces, intro/stage/kill timelines, first-person restore). New files typecheck against
  three r180 types. Lab renders inspected for idle, all moves, intro, stage, death and first person. Not run here: the
  repo's full suite, `verify`, Vite build and an in-game check (npm registry unreachable; the systems are on Astra's
  branch). No hit timing, hitbox, damage, cooldown, movement or network behavior is defined here.
- Status: implemented on branch, not merged. Next: Astra wires the façade (docs/SIREN.md), then an in-game pass.

## 2026-10-01 — Claude — Siren DEV boss: shared contract for the Astra/Claude split

- Owner: new playable DEV boss "Siren" (one per match, free-for-all for now, adds later) with a beat-synced intro and
  stage. Astra builds systems on `codex/siren-boss`; Claude builds visuals on `claude/visual-revision-siren`. Both start
  from `claude/siren-contract`, based on `claude/visual-revision` at `cf5fa1c`. See `docs/SIREN.md`.
- Contract (new files only, no existing file changed): `src/shared/siren-contract.ts` (ids, slots, move timing table,
  snapshot/event shapes, spawn points, intro cues, accessors), `src/shared/boss-music.ts` (464 detected beats, bars,
  sections, intro window, loop; drifting tempo), `src/shared/siren-stage-layout.ts` (Ward Zero collision layout and
  beat-synced vents), `src/client/siren/index.ts` (visual façade with placeholder implementations), tests.
- Checks: `tests/siren-contract.test.ts` 11/11 passed (tsx). New files typecheck against local three r180 types. The
  npm registry was unreachable from this session, so the full suite, `verify:fast` and the Vite build were not run.
- Combat/network impact: none yet (no existing code calls the contract). Status: implemented on branch, not merged.

## 2026-10-01 — Codex — individual DEV primary combo steps

- Owner follow-up: allow one punch, two punches, or the full chain rather than
  automatically firing the whole string. Branch `codex/dev-click-combos`, based
  on shared `claude/visual-revision` at `6e32a60` after the previous publication.
- Ansatsuken primary now commits one step per activation (right punch, left punch,
  high kick); Emperor commits left then right. Release stops after the committed
  hit; further taps or holding attack continue, matching standard melee's existing
  input/cooldown behavior. Next-step memory lasts 2.4 s after recovery. Blocking,
  stun, specials, Demon Step, roll, death and round reset clear a partial chain.
- Costs split across steps: normal Ansatsuken 4 each / 12 total; Emperor 3 each /
  6 total. Ten-Hit Rush remains ten requested steps (2.2 each / 22); Pressure String
  three (2 each / 6). Existing per-step damage, hitboxes, finisher rewards and hit
  effects retained. Nonfinal recoveries use the original .25/.17 s cadence divided
  by attack rate; final recoveries retain their original durations. Rush uses a
  .069 s windup at base rate so an individual hit fits its .115 s cadence.
- Animation renders only the selected fist/kick and finishes its recoil before
  that hit's recovery ends. Optional `singleHit` snapshot flag distinguishes this
  from older full-string samples. Movement remains unlocked; successful Demon
  Step/roll now cancels the partial chain. Demon Step i-frame timing is unchanged.
  Saves, input fields, account access and special-move damage/cooldowns unchanged.
- Checks: types passed; 64 focused combat/animation/i-frame checks passed; full
  suite 555/567 with the same 12 baseline failure names. Three existing tests were
  updated for the deliberately changed per-activation counts/costs. Ten new tests
  cover taps, hold/release, timeout/block/dodge resets, resource refusal, added chip
  steps and correct snapshot fist selection; tap tests fail on the prior source.
  Eight real WebSocket clients agreed on 200 snapshots, including stopping after
  one/two hits, special moves, Demon Step, parry and knockdown/recovery.
- Final recoil-window animation checks: 32/32 passed. PR #7 merged into the shared
  branch at `66451cdf8240b02c836741cfd1c0853ba9d7c5a1` (implementation `f5b28ec`).
  Clean exact-commit release builds produced byte-identical payloads twice;
  updater validation passed all 101 file hashes, settings preservation, same-version
  skip and corrupt/offline refusal. Published test content `0.2.f3f24aed59e8`,
  build `775161394d78cfecd5769154`, 18,267,588-byte payload, channel commit
  `873e23fddca58ea05a57332ee4ca301a5dff9ae0`. The verified temporary split-upload
  workflow succeeded and removed its branch. Existing testers press Update.
- Real network installation verified `0.2.298dbb325341` -> `0.2.f3f24aed59e8`:
  all 101 installed hashes matched, settings sentinel survived, second Update
  reported current, and menu badge returned the new build/version. The historical
  seed manifest needed one retry after the unchanged updater's 5 s timeout in
  this Linux/proxy environment; the new-version upgrade itself passed directly.
- Status: merged and published to the existing test channel; live Site/feed
  untouched. Native Windows/GPU playtest remains unrun; known baseline failures
  remain unresolved. Next: owner checks tap/hold/release feel on both DEV weapons.

## 2026-10-01 — Codex — merge and publish DEV combos / Demon Step to the shared test channel

- Owner explicitly requested merge + publish and the same in-game Update workflow
  Claude uses. PR #6 (`codex/dev-melee-combo`, implementation `66111c2`) merged into
  `claude/visual-revision` at `5ecc20f7aaba065a6c8071b983bfe393f00e14af`.
  Merge tree matches the already-tested implementation exactly. PRs #1–#5 remain
  separate; no unrelated fixes merged. Shared source/channel choice is now recorded
  in AGENTS and DEVELOPMENT for both agents.
- Published test content `0.2.298dbb325341`, game build `9499e3a5ba09df675dcc401e`,
  channel commit `2112c4a45ebae1b97afbcb631e03d00deb13eb62` in
  `sheldon37/chip-arena-test-updates`. Existing channel installations press Update;
  no new seed ZIP or launcher change is needed. Live Site/feed untouched.
- Build/checks: clean worktree at the merge SHA, npm ci, `release:prepare` twice
  with byte-identical feeds (18,266,547 bytes). PR #5's actual updater checker
  verified all 101 files, current-version skip, settings preservation, offline and
  corrupt-payload rejection. Remote-channel/status tests 6/6 and auth/manual
  updater tests 7/7 passed. Full source verification remains as recorded below:
  545/557 unit tests, identical 12 baseline failures, four baseline server build
  errors and the existing WebSocket rematch timeout. No gate was marked green.
- Publication transport: the connector's 16 MiB request limit could not carry the
  18 MB payload. Uploaded two parts to a temporary channel branch; one-shot GitHub
  workflow verified the final SHA256 and every file hash, then atomically pushed
  only the feed + PUBLISHED row onto the unchanged channel head. Workflow succeeded
  and deleted its staging branch; no scheduled or persistent publishing workflow.
- Network verification passed after the raw GitHub cache refreshed: installed
  previous content `0.2.19e114ae7c87`, updated through the real network updater to
  `0.2.298dbb325341`, verified all 101 installed hashes and settings sentinel,
  then confirmed current-version skip and the menu badge's build/version.
  Linux network test used its environment proxy; the historical seed manifest
  required one retry after the updater's existing 5 s timeout. Updater code and
  timeouts were unchanged. Native Windows validation remains separate.
- Combat effects are those detailed below: combo animation flow plus Demon Step
  protection only; no further timing, damage, hitbox, cooldown, movement, save or
  protocol changes. Windows EXE/GPU and two-PC DEV playtesting remain unrun.
- Status: merged and released to the existing TEST channel. Next: owner playtests
  both DEV combo chains/exits and Demon Step startup/middle/recovery in-game.

## 2026-09-30 — Astra — DEV melee combo holds and Demon Step i-frames

- Owner: apply the standard melee combo-ready system to DEV melee; follow-up adds
  invulnerability frames to Ansatsuken's Demon Step. Branch `codex/dev-melee-combo`,
  based on Claude's requested visual work at `32422fbf0c96b13cf1007f892e5668748e84a1e7`
  (`claude/visual-revision`, PR #4). PRs #1–#5 remain unmerged at inspection.
- Emperor and Ansatsuken now recover directly into their next chamber rather than
  returning to guard before raising it. Share standard melee's 2.4 s visual hold
  and 0.8 s fade. Remember dodge/parry/hit exits even when the fighter layer is
  skipped or the exit field clears; discard stale state on death/rewind/rig reuse.
  Next-string entry and planted footwork retained. L33T is magic and unaffected.
- Demon Step: the existing 0.30 s dash protects from +0.05 s inclusive to +0.25 s
  exclusive (0.20 s / 12 frames at 60 Hz). Requires authorized Ansatsuken, a live
  player and an active dash. Canceled/expired dashes lose protection. Applies to
  authoritative damage, direct hits, normal projectiles and thrown daggers/shields;
  avoids hit procs, new status application, knockback and confirmed stuns/launches.
  Existing burn/poison, overtime, falls and forced L33T scare damage follow the
  same exceptions as dodge rolls. Updated the weapon's description.
- Combat/network impact: attack contacts, hitboxes, damage values, combo counters,
  attack cooldowns, dash cost/speed/distance/cooldown, saves and protocol unchanged.
  The intentional gameplay change is Demon Step damage avoidance in that window.
  Reuses `arcade.demonStepAt` and `dashTime`; no new snapshot or input fields.
- Checks (Linux, Node 24.19.0 / npm 11.9.0): types passed; 15 new tests passed;
  existing roll/combo/DEV footwork checks 13/13 passed. Full final unit suite
  545/557 passed, with exactly the same 12 failure names as a clean checkout of
  Claude's base (530/542). The live end-pose regression fails on the unchanged base.
  Client/relay compilation passed; server compilation retains the four baseline
  errors covered by PR #2. Full `npm run verify` hit the known WebSocket rematch
  timeout; HTTP/SQLite integration passed five sets, four drafts, snapshot agreement,
  lost-response retry, rematch and guest leave (179.5 s). Overall verify remains red.
- Not verified: Windows EXE/GPU appearance or two-machine DEV combat. Visual-lab
  browser launch was blocked by the missing Playwright Chromium executable.
  No packaging, merge, test-channel publish or live-feed change in this task.
- Next: review/merge this branch into `claude/visual-revision`, playtest both LMB
  strings into another string/dodge/parry and Demon Step's startup/middle/recovery,
  then build/publish a test-channel version when requested. Keep Claude's existing
  log entries. Status: implemented/tested on task branch; not merged or released.

## 2026-10-01 — Claude — Rain Warrior and Emperor's Oath intros (channel update)

- Owner: smoother DEV intros, fix their feet, Emperor's Oath camera in front of the player.
  626cfc5: new intro-footwork.ts (world-planted steps, pivots, lift arcs) used by both
  intros. Emperor's Oath: slip 0.044 -> 0 m, lowest ankle -0.847 -> -0.795, chest square
  to the camera (dot .78 -> 1.00); hips-led oath, rear-foot step, breathing hold; still 3
  shots, framing within limits; right arm path unchanged. Rain Warrior: feet no longer
  twisted off the knees, blended two-step pivot turn, ends in the bladed karate stance,
  breathing opening, new hadoken beat at the 4.6 s hit (orb may need polish). Durations,
  nameAt and access unchanged.
- Tests: new tests/dev-intro-fluidity.test.ts (6; 4 fail on old code); full suite only the
  13 known failures. Known: pre-existing synth backdrop bars also show on the rain stage.
- Published: content 0.2.19e114ae7c87 (channel 924651e), network update verified.

## 2026-09-30 — Claude — DEV fighting styles: ground-anchored feet (channel update)

- Owner: emperor (Tekken) / ansatsuken (Street Fighter) feet looked wrong while running
  and attacking; touch up moves. 2776860: the ground pass now covers both styles; fighter
  moves layer per-foot edits on the gait (layerLeg/gaitFoot) instead of fixed leg angles;
  bladed stances in stances.ts; kick chamber no longer flips the knee; combo-ready stance
  now works in live play. Slip e.g. backpedal 6.0 m -> 0.003 m; ankles never below the
  floor; knee flips gone. Guard bounce, hip-driven strikes, donkey kick, Shoryuken and
  Demon Step touched up. Access control and gameplay unchanged.
- Tests: new tests/devstyle-fluidity.test.ts (fails on old code); full suite only the 13
  known failures; no existing test edited.
- Published: content 0.2.87477529ed9b (channel e19eea3), verified network update; full
  channel ZIP (SHA256 168f0a2c...07a3) sent to the owner in parts for a second PC.

## 2026-09-30 — Claude — souls roll, fluid greatsword, combo-ready holds (channel update)

- Owner feedback: rolls levitated; greatsword looked robotic; after a hit, hold the pose
  for the next swing (combo), exit by rolling or timing out to idle; same for DEV styles.
- 587d99d roll: floor-solved shoulder roll (tuck was 13-65 cm above the floor, now within
  ~2 cm in 6 directions), dip, 360-degree tumble, crouch exit. 9ab51ce greatsword: smooth
  splines (old curves stopped at every reversal), hips-first chain (chest +71 ms), weight
  shift, heel pivot; combo-ready holds for sword/daggers/shield/greatsword (next cut starts
  from the hold; roll/hit/block/special release over .22 s; 2.4 s timeout). 35569fa
  emperor/ansatsuken chambered fighting stance between strings (L33T unchanged).
- Tests: new tests/roll-greatsword-fluidity.test.ts (8); full suite only the 13 known
  failures; no existing test edited. Known: kneeling roll exit can graze ~2.5 cm, diagonal-
  back exit bobs ~7 cm, greatsword reverse cut still raises high in windup.
- Published to the test channel: content 0.2.da174b55f7c3 from 35569fa (channel 5842c18);
  verified updating 0.2.4d3dcebad34a -> 0.2.da174b55f7c3 over the network, then current.

## 2026-09-30 — Claude — online test update channel (owner-requested)

- Owner asked for a working Update button so testers stop downloading ZIPs; approved a
  PUBLIC test-only channel repo: https://github.com/sheldon37/chip-arena-test-updates
  (feed at https://raw.githubusercontent.com/sheldon37/chip-arena-test-updates/main/feed/).
  Live feed and Site untouched.
- Tooling: branch `claude/test-update-channel` (base release/candidate-workflow 7ff6462),
  PR #5, head fd13d5a. `candidate.py`/`make-test-package.py --channel <url>` builds a
  TEST-ONLY ZIP whose Update button and menu badge read the channel through the unchanged
  production updater (schema/SHA256/32 MB checks); no live fallback. Without --channel the
  fixture is unchanged. Tests: scripts/release/test-remote-channel.test.mjs (6/6 with the
  existing status test); real-network install from the channel verified (installed, then
  current).
- Published: content 0.2.4d3dcebad34a from `claude/visual-revision` 0e82a31 (channel repo
  7757039, logged in its PUBLISHED.md). Channel seed ZIP (SHA256 205883b4...905b) built from
  0e82a31; packaging step re-run manually after fixing a packager bug (fd13d5a).
- Cross-development (Claude or Astra): to ship a new test version, build a candidate from
  the exact commit with `--test-only --channel <feed url>` using the PR #5 tooling, then
  replace the channel repo's `feed/` files (one payload only), add a PUBLISHED.md row,
  push, and log it here. Testers press Update; raw GitHub may cache ~5 min.
- Status: test channel live for testers; not a player release.

## 2026-09-30 — Claude — Dark Souls sprint carries, intro label, test build 2

- Owner feedback on test build 1: melee blades faced backwards while running; wanted a
  Dark Souls sprint and a two-handed greatsword low carry (owner reference image), and the
  intro weapon pill removed so the weapon name fades in with the pilot name.
- Changes (7c735d3, f688db0, a3b40e8): forward-down sprint carries for sword, daggers,
  shield (right-hand forearm ride; still meets the arm-swing test) and a two-handed
  greatsword carry with sprint lean; walk uses the same carry. Intro weapon name is plain
  text animated with the name. Tests: owner-approved greatsword exemption in the
  rig-arena-sniper arm-swing clause (replaced by a grip/blade-position assertion); two
  new-in-revision tests that pinned the rejected tip-back carry now pin forward-down
  (verified failing on the old carry). Full suite: only the 13 known failures.
- Test build 2 from a3b40e8 (same tooling/flags as build 1): SHA256 7ab1728b...9c9a,
  content 0.2.4d3dcebad34a; verify failed only on known items. Not published.

## 2026-09-30 — Claude — visual revision TEST-ONLY Windows build

- Owner requested a Windows test build of `claude/visual-revision` at 2557a12. Built with
  PR #3 tooling (release/candidate-workflow 7ff6462) `candidate.py --test-only`, Node
  24.19.0 / npm 11.9.0 (Python 3.11): Chip-Arena-TEST-ONLY.zip, SHA256 0f4df606...71c5,
  content 0.2.754d8ed53b1b, build 8d4cb82a5c68b3f79c2e3b0b. Status BLOCKED/publishable:false.
- Passed: setup, fixture tests, updater tests, runtime pins, two builds with identical
  payload, actual payload install, Windows/desktop/isolated packaging. `verify` failed only
  on known items: 12 known unit failures (portable-update passes on Node 24), the 4 server
  TS errors (PR #2), WebSocket rematch timeout.
- Delivered to the owner as 10 split parts; not published, no live feed change. Native
  Windows run pending owner test.

## 2026-09-30 — Claude — complete visual revision

- Task: owner-requested complete visual revision (animations, intros, model, UI),
  presentation-only. Branch `claude/visual-revision`, base `migration/shared-development`
  `dfb90c8`; PR to migration/shared-development (see GitHub). Plan, owner direction,
  invariants and ownership: docs/VISUAL-REVISION-PLAN.md. Player-facing summary:
  docs/UPDATE-HISTORY.md (copied from PR #3's version 7ff6462 plus a new entry — on
  merge conflict keep both sides' rows/sections).
- Changes: dev-only visual lab (tools/visual-lab, not in the build); no-behaviour module
  split; ground-anchored locomotion; continuous reactions/knockdown/ragdolls; Souls-like
  melee from owner reference GIFs (DEV classes untouched); shouldered guns, reloads,
  casting, first-person view model; detailed voxel model in armor slots with hue-true
  glow; reworked intros; UI polish and in-place HUD updates. Unchanged: hit timing,
  hitboxes, damage, cooldowns, movement rules, network/snapshot formats, saves, access
  control; src/shared, server and relay untouched; no pre-existing test edited.
- Checks (Linux, Node 22.22.2): full `node --import tsx --test tests/*.test.ts` 523 tests,
  only the 13 pre-existing failures; `npm run check:types` passes; independent reviews
  (invariants, animation quality, real-app smoke via Playwright/SwiftShader) passed.
  `npm run build:check` still stops on the 4 known server TS errors (fixed by PR #2).
- Not run: native Windows/GPU, real-hardware performance, online multiplayer, packaging.
- Known open: jump airborne pose barely visible in lab strips; dagger idle guard less low
  than the reference; warm accents pale under lobby/portrait lighting; intro close-ups
  blow the chest glow to white (pre-existing); crossed-arms lobby fist clips (pre-existing);
  Developer Sandbox buttons overflow at 1280x720 (pre-existing); intro scenes render
  slower under software GL, so check frame rate on the owner's PC. Owner-gated stale tests
  (fighter high kick, rarity-combat dagger mirror) still fail as before.
- Next: owner playtest on Windows and review of feel (cadence, melee, glow); then merge
  decisions. Status: implemented on branch; not merged; not released.

## 2026-09-29 — Astra — shared development log

- Branch: `migration/shared-development`; base before this documentation task:
  `dcdcd930565d05ace379e263206e13366b3e4e69`; PR #1.
- Added this log and a shared AGENTS.md handoff requirement. CLAUDE.md already
  imports AGENTS.md, so both agents use the same policy.
- Checks: read current remote AGENTS.md and PR metadata; documentation-only change.
  No gameplay/build/Windows tests run for this task. Existing baseline failures
  remain documented in MIGRATION-REPORT.md; this entry does not revalidate them.
- Runtime, game mechanics, assets and update feed unchanged. No player release.
- Status: implemented on migration branch; PR #1 still draft/unmerged at writing.
- Next: each agent synchronizes this branch before making its own task branch;
  after migration merges, start new tasks from updated main. Review relevant open
  PRs to avoid overlapping work. Resolve baseline failures before release work.
