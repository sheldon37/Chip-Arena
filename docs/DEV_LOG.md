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



## 2026-10-06 — Codex — Unity Windows EXE icon and build handoff
- Owner requests a Windows EXE and stylized voxel CA launcher icon. Continued isolated branch
  `codex/unity-prototype` from `3739884e1d2ecbb235f7497696638ec2bfb0a9ca`; no shared-game or feed changes.
- Added original generated transparent RGBA CA icon, importer metadata and generation provenance.
  Editor preparation fills/read-checks default and standalone icon slots using Unity's supported API.
  Windows packaging now rejects missing UnityPlayer.dll/game data; the command clears stale logs,
  explains activation/support requirements and opens the player folder only after build success.
- Validation: updated Editor helper compiles with C# 9 against actual Unity 6000.3.24f1 managed APIs,
  warnings as errors. PNG decoding/alpha and source structure/package integrity checks pass.
  No gameplay, animation, hit timing, hitboxes, movement, damage or network code changed.
- Blocker: no activated Unity Editor in this authoring environment. No EXE was produced; Windows
  player compilation, icon embedding/Explorer appearance, CMD execution and gameplay remain unrun.
  Next: activate the pinned Editor via Hub on Windows, install Windows Build Support, open/import
  the project, close it, and run Build-Windows.cmd (or use the Editor's Build Windows TEST ZIP menu).
- Status: source and icon implemented on the Unity test branch; not merged, no game-feed release.

## 2026-10-06 — Codex — Isolated Unity engine-evaluation prototype
- Owner requests a separate Unity test branch and delivery. Branch `codex/unity-prototype`, shared base
  `05950eaef80ff2483a9c44d898cad98be1a76bbc` (tree `1984514e15f96473fda5d56fde20c0ccc1401a0f`).
  The source overlay was prepared in an isolated directory and is added to that exact remote base tree.
- Added self-contained `unity/ChipArenaPrototype`, pinned to Unity 6.3 LTS 6000.3.24f1. Includes one arena,
  source-grounded movement/sword/block/roll constants, simplified combat, procedural instanced voxel poses,
  energy bolts, breakable cover, decorative PhysX debris, 0–192-bot workloads, frame-time/CSV measurements,
  and a distinct 2-player trusted-LAN host-authoritative test protocol (no prediction or production integration).
- Existing gameplay, hitboxes, timing, animation, saves, launcher and network protocols are untouched.
  Prototype behavior differs deliberately; README and PROTOTYPE-SCOPE list omissions. Purchased animation
  assets and full Siren/Chip/round systems are not included. Built-in render pipeline; native pack/rig evaluation is next-stage.
- Validation: 22 real managed-core checks pass, including 192 bots/600 ticks and a real TCP loopback
  input/authority/snapshot/disconnect exchange. Core compiles with Roslyn from .NET SDK 8.0.425.
  Runtime, separate Editor assembly, and standalone-Windows conditional runtime all compile against the
  official Unity 6000.3.24f1/.NET Standard 2.1 assemblies using C# 9, warnings as errors. No Unity stubs.
  Fixed the build helper's CompressionLevel ambiguity found by the real API check.
- Unity Editor import/play, shader compilation, visual/PhysX acceptance, two actual Unity windows and a
  compiled Windows player remain NOT RUN: this environment has no activated Editor. Managed API compilation
  is not a Unity build. No FPS uplift or production-parity claim. See project VALIDATION.md.
- Added local Editor menu/Windows command to validate and build a separate TEST ZIP. Isolated branch-only CI
  checks the core and packages a private **source** artifact; it uses read-only contents and no Unity credentials.
  No existing workflow is altered. Status: implemented on test branch; not merged or published to any game feed.
  Next: owner opens the source project in Unity Hub, runs the supplied Editor checks, Play and Windows build,
  then evaluates equal-load performance and one licensed native animation pack before broader migration.

## 2026-10-06 — Codex — Clear surviving enemies at wave completion
- Branch `codex/wave-end-cleanup`, shared base `9028309412355e3cb978609d4776970c050900bf`, exact tree
  `e885503d2a52068ab0d4c4896784566da13c1ab7`. Owner requests cleanup excluding bosses and publication to Update.
  Recovered an isolated checkout from the local source after its old parent Git metadata expired; every tracked blob,
  file mode and the resulting tree match the current remote before edits. No other open task overlaps this fix.
- Every normal, timed-out or debug-skipped wave now invokes horde-only cleanup before intermission rewards/respawns.
  Active/exposed entering adds die through existing cleanup handling; pending/unexposed arrivals and the spawn budget
  are cancelled. Bosses, humans, party bots, recruits and friendly summons are outside that registry and are untouched.
  Boss-phase transitions retain their existing horde behavior. Wave bonuses, respawns, timings, hitboxes, ordinary damage,
  cooldowns, movement, animations, saves and network formats are unchanged; deaths/snapshots now reflect the cleanup.
- Checks (Linux, Node 24.19.0/npm 11.9.0): client types pass; 49 affected director/horde/flow/boss/possession/render tests
  pass. Real-Rapier coverage verifies active/exposed/queued cleanup, surviving boss body/HP and friendly summon, no delayed
  spawns during intermission, and reuse of the same entry next wave. Natural/timeout/debug clears are covered.
  Extended PvE multiplayer passes: both clients observe live adds going from 2 to 0 at wave clear, 853 matching shared
  ticks, reconnect and victory; raw snapshot p95 34,708 B. All 10 updater channel/menu-status fixtures pass.
- Full verify units: 1307 total, 1293 pass, 13 fail, one skipped: the 12 known baseline names plus the eight-client ping
  timing check under full-suite load. That check passes alone (80 identical snapshots, ping p95 6.61 ms). An incidental
  standalone party-relay rerun hit its previously observed 403-versus-200 flake; it passes in this full verify run.
  Full `npm run verify` completes with those unit failures, four known server TypeScript errors and the existing
  WebSocket rematch timeout; HTTPS integration passes (3418 matching snapshots, rematch and guest leave).
- Two complete client/relay release builds produce byte-identical manifests and payloads for candidate
  `0.2.e337f51c003d`, game build `49ca456f76647ac42ef59970`. Both actual updater payloads pass installation,
  all 103 game-file hashes (107 including launcher), launcher activation, same-version skip, settings preservation,
  offline/corrupt refusal and source-privacy checks. Schema 1: 24,708,289 B; schema 2: 20,578,756 B.
  Existing TEST-only launcher wrappers are preserved, extracted from the previous verified pack with matching hashes.
  `git diff --check` passes.
- Release: owner authorized merge/publication. PR #57 merged at `a0167bece51e21f465b1cdf1fbbc0e0bd948820d`;
  its complete tree `c6882b17ec7f04ee80f5b07ce14f6da747c70227` matches the tested checkout exactly.
  TEST channel `5ddab5b780e87eb9f8d9ab3e6d309e2f784f1125` publishes `0.2.e337f51c003d`
  (successful run `37526867343`). Public Git blob hashes of both manifests and payloads match the verified local files.
  The temporary assembly branch deleted itself; no publishing workflow/chunks remain on channel main.
- Actual network updater checks pass for both schemas from `0.2.b18d54478b46` to `0.2.e337f51c003d`: all 103 installed
  game-file hashes match, settings survive, schema-2 launcher activation succeeds, and a second update skips. The actual
  menu status reports version `0.2.e337f51c003d` and build `49ca456f76647ac42ef59970` after installation.
  Release receipt branch: `codex/wave-clear-release-receipt`. Native Windows/GPU and manual gameplay not run;
  baseline verification failures remain as detailed above. No live-feed, Site or Windows seed release.

## 2026-10-06 — Codex — Persistent impact effects, projectile procs and a much larger Siren hunt
- Branch `codex/impact-lifetimes-siren-horde`, shared base `c52a532ad2763251570fc96d43cf88833b74758c`;
  local base checked against the entire remote Git tree. Owner requests no impact cancellation, a huge Find-me horde,
  and impact effects on added projectiles; owner chose each eligible projectile over the old once-per-volley fields.
- Impact Chip fields no longer evict older fields at the per-owner/kind or global limit. Delayed bombs and L44T Nuke marks
  finish normally. Client blast/spark buffers grow while retaining existing particles; beam/impact-mark/Nuke visual limits
  no longer discard active effects. Natural expiry and round/reset cleanup remain, and particle buffers shrink on reset.
  Supplementary point-light pools and separately balanced Q/E ability-cast limits remain unchanged.
- Each primary projectile, Blade Runner, Firework fragment and L44T Copy/Paste shot can proc impact effects on players,
  cover and minions. Described exclusions (Satellite Office, Rear Guard, Four-Way Stop, Double Helix and Splitter),
  reflections, nonrecursive fragments/copies, cooldowns and explicit once-per-attack Shrapnel/Vacuum rules are retained.
  Full and simplified card descriptions explain the rule/exclusions. Weapon damage/DoTs, attack timing/hitboxes,
  cooldowns and movement are unchanged; more independent impact effects intentionally increase aggregate damage/control.
- Siren possession spawn rate and living targets increase fourfold: 72/120/168/192 adds at 12/20/28/36 per second for
  1/2/3/4 party members; relay caps are 72/112/112/112. Entry reservations increase only during possession. Rendering
  covers 192 adds + 16 corpses while keeping full-rig/shadow budgets. Other phases, hints, possessed HP and rewards retain
  their rules. Network/save formats are unchanged; larger authoritative snapshots/effect counts are intentional.
- Checks: Linux, Node 24.19.0/npm 11.9.0; client types and client/relay release build pass. 121 affected tests pass;
  final projectile/L44T/card checks pass 39/39. Full final unit run: 1306 total, 1293 pass, 12 fail, one skipped;
  failure names exactly match the untouched base. `npm run verify` retains four server TypeScript errors and WebSocket
  rematch timeout; its transient party-relay failure passes in the final full unit run. HTTPS integration passes
  (3439 matching snapshots/rematch), and PvE multiplayer passes (812 shared ticks, reconnect, victory, no disagreement).
  Chaos network check hits the old voxel-burst/impact-ragdoll cosmetics assertion identically on untouched base.
- Real-Rapier stress with 192 adds/16 corpses: 2.084 ms average sim step, 1.126 ms PvE share, 4.536 ms p99;
  snapshot p95 60,065 bytes, 208 add tuples, no dropped horde events. Original CPU and 64 KiB snapshot gates pass.
  All 10 remote-channel/menu-status updater fixtures pass. `git diff --check` passes.
- Windows/native GPU, real-browser visual/difficulty acceptance and remote human play not run. Heavy combinations can
  increase GPU/memory/simulation load; headless crowd results do not measure unlimited overlapping effects on Windows.
- Prepared TEST candidate `0.2.b18d54478b46`, game build `9df11a70d649891bf1a915a4`. Both real updater payloads pass:
  103 game files/107 with launcher, all installed hashes, launcher activation, same-version skip, offline/corrupt refusal,
  settings preservation and source-privacy checks. Schema 1 is 24,708,136 B; schema 2 is 20,575,267 B.
  Two complete release builds produced byte-identical manifests and payloads for both schemas.
- Release: owner approved merge/publication. PR #55 merged at `c146cd6a5fc724d0038915a1c91a313146d8addc`;
  its complete tree `29c193dc42d9aa1de955de06341f1de31c96e754` matches the tested checkout exactly.
  TEST channel `ecbbdd077a9db20db999a34668cba8c891ed4872` publishes the candidate above (run `37519393074`).
  Public Git blob hashes of both manifests and payloads match the verified local files. The temporary chunk-assembly
  branch deleted itself; no publishing workflow/chunks remain on channel main.
- Actual network updater checks pass for both schemas from `0.2.a06765ab0a43` to `0.2.b18d54478b46`: all 103 installed
  game files match, settings survive, schema-2 launcher activation succeeds, and a second update skips. The actual menu
  status handler advertises this build/version for both schemas. Status: merged and released to the existing TEST Update
  button. Native Windows/GPU/difficulty acceptance remains. No live-feed, Site or new Windows seed release.

## 2026-10-06 — Codex — PvP chip presentation for Siren personal picks
- Branch `codex/siren-pvp-chip-ui`, shared base `9de0365171d3b22404e2bd125f2634d971418dc5`
  (local checkout verified against its complete Git tree). Owner requests UI only and publication to the existing Update button.
- Siren's personal screen now reuses the PvP 3D pilot stage, hover look/reach, compact card tray and nameplate. A separate
  presentation snapshot feeds only DraftStage/DraftView; the arena, simulation, input, audio and networking keep the real
  fight snapshot. Personal offers/order, immediate pick command, queue, Choose later/C/Esc, number keys, visibility gates,
  damage warning and vulnerability are retained. No PvP turn order, ready gate, selection timer or post-pick hold is adopted.
- Both trays now fill the available width and distribute cards left to right instead of centering a capped-width group.
  Card widths stay readable; one offer stays centered; narrow/short screens retain horizontal scrolling. The personal tray
  reflows on resize, and an attack warning updates without moving or rebuilding the card buttons.
- Checks: Node 24.19.0/npm 11.9.0; client typecheck passes; 227 affected PvE/draft/card tests pass. Two new regressions
  exercise independent personal views through the real DraftStage without mutating fight state, and shared tray markup
  for 1/2/3/6/8/12 offers at five viewport sizes. Full unit suite: 1286 tests, 1273 pass, 12 failures matching the previous
  untouched-baseline names exactly, one skipped. No shared/server/relay/launcher code or gameplay rules changed.
- Native Windows/GPU, real-browser visual acceptance and remote-player manual play not run. Chromium's download failed
  (invalid/truncated archive); headless rig tests are not visual acceptance. Owner visual review remains.
- Release: PR #53 merged at `6e3da8e3f6f6f8e56891bab7077c505acee69b9a`; its complete tree
  `20463da03a359f3437e8d211aa00a10dac412db8` matches the tested checkout. Server compilation retains the four documented
  baseline errors; all 10 remote-channel/menu-status fixtures pass. Two release builds produced byte-identical manifests
  and payloads for `0.2.a06765ab0a43`, build `798dc25d1e90f4bc954933ac`. Real-payload checks pass for both schemas:
  103 game files (107 including launcher), all installed hashes, launcher activation, same-version skip, corruption/offline
  refusal, settings preservation and no source archive/maps. Schema 1: 24,707,221 B; schema 2: 20,576,951 B.
- Published atomically to TEST channel `b61a81fcb07f0e56ae51ef166367a61071313d09` (publishing run `37513170104`).
  Public Git blob hashes of all four feed files exactly match the verified local files. The temporary chunk-assembly
  branch deleted itself; no workflow or chunks were added to channel main. No live-feed, Site or new Windows seed release.
- After raw-GitHub cache propagation, the actual network updater passed for both schemas from `0.2.8829a9b74310`
  to this release: all 103 installed game hashes match, schema-2 launcher activation succeeds, settings survive, and a
  second run skips. The actual menu-status handler advertises this build/version for both schemas. Status: merged and
  released to the existing TEST Update button. Native visual review remains.

## 2026-10-05 — Codex — Attack recovery, cheaper swings and sprint stamina
- Branch `codex/combat-flow-stamina`, exact shared base `ab93263c704266949432f9ee32034b157921d720` (PR #50).
  Owner requests smoother attack endings/combos, lower attack stamina, sprint drain/visual, and all steps to the test Update button.
- Sword, shield and greatsword recover along a single quaternion arc between fitted contact/guard poses, avoiding late captured
  wrist regrips. Dagger recoveries limit abrupt wrist turns in chest space; the full spin stays intact. Combo transitions remember
  the final floor-corrected displayed pose. Ground clearance and the greatsword support grip are retained. Contact timing,
  hitboxes, damage, cooldowns, attack travel, first-person poses and DEV choreography are unchanged.
- Standard primary stamina: daggers 10→7, sword 18→14, shield 22→16, greatsword 26→20; Echo Strike 3→2 per extra hit.
  Specials/roll costs and tiny DEV jab costs remain unchanged. Only the three golden traces with standard melee costs change.
- Double-tap-W sprint retains 1.35x forward speed, costs 12 stamina/s, suppresses simultaneous regeneration, and falls back to
  jogging at zero. Exhaustion stays latched until W is released, preventing regen/sprint oscillation. Round/reset/respec clears it.
  Shared host/prediction helpers and `caps.move=2` synchronize stamina/exhaustion; old move-v1 hosts retain their free-sprint
  prediction. HUD adds an amber SPRINTING chevron/bar and a release-W exhaustion prompt (reduced motion respected).
- Checks: Node 24.19.0/npm 11.9.0, fresh `npm ci`, client types pass. Eight new tests cover recovery at 60/120 Hz and three
  attack rates, actual-pose combo continuity, drain/exhaustion/reset, old-host compatibility and delayed JSON snapshot replay.
  Existing captured-melee/greatsword animation tests pass; revised costs and all PvP golden traces pass (31 affected tests).
  Full `npm run verify` retains four known server TypeScript errors and WebSocket rematch timeout; HTTPS multiplayer passes.
  A movement-network run had a transient shared-snapshot mismatch; rerun passes (635 identical snapshots, 0.128 m P95 correction,
  <0.001 m instantaneous visual correction), and untouched-base comparison passes. Test-channel/status fixtures: 10/10 pass.
- Visual measurements (60 Hz, 1x attack rate): largest recovery/exit angular step sword cuts 29/32/34°→13/9/15°,
  dagger cuts 1–3 36/70/47°→25/18/22°. The dagger spin is preserved. These are rig measurements, not native visual acceptance.
  The previously recorded dagger spin→Echo Strike unwind remains; contact/anticipation is deliberately not time-filtered.
- Windows EXE/native GPU/manual gameplay not run (Linux container; Chromium download failed). No Site/live-feed publish or new seed ZIP.
  Final full unit run (`node --import tsx --test --test-concurrency=4 tests/*.test.ts`): 1271/1284 pass, 12 failures
  matching the untouched baseline names exactly, one skipped (baseline 1263/1276). The first unrestricted repeat exited early
  without a summary; the bounded run completed.
- Release: PR #51 merged at `db58d1befb5c1c38871117f7117f31f48758d996`; its complete tree matches the tested checkout
  (`a6beb516700bf7cc86faa6585aecf73b975c04f5`). Two `release:prepare` builds produced byte-identical schema-1/schema-2
  manifests and payloads for `0.2.8829a9b74310`, game build `10e08a8dd84292d1164dc939`. PR #5 payload checks pass:
  all 103 game files (107 with launcher files), actual install/launcher activation, same-version skip, corruption/offline refusal,
  settings preservation, and no editable source/maps in public payloads. Sizes: schema 1 24,706,861 B; schema 2 20,576,024 B.
- Published both manifests and payloads atomically to the existing test feed at `b7307d60b91020d609a0f26e5b9c8082318b5777`.
  The connector's 16 MiB request cap required eight verified chunks and one temporary GitHub publishing job (run 37334499364):
  assembled bytes checked against exact SHA-256/length, refused a moved feed, then pushed feed-only main and deleted its staging
  branch. No publishing workflow/chunks were left on main; no source, secrets, saves, Windows seed or live Site/feed changed.
  Public Git blob hashes match all four local feed files exactly. After raw-GitHub cache propagation, the actual network
  updater passed for both schemas from previous `0.2.00f72f2f7eb8` to this release: all 103 installed game file hashes match,
  schema-2 launcher activation succeeds, settings sentinel survives, second run skips, and the actual remote menu-status handler
  advertises this build/version. Status: merged and released to the existing TEST Update button; Windows feel review remains.

## 2026-10-05 — Claude — Melee animations (sword, daggers, shield, greatsword) from the Explosive RPG Character pack
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` `0aa70dc` (PR #49 merge, plus its log commit
  `b19733a`). Owner requests 2026-10-05: "re-do the animations (Melee) with the ones from the pack" (the owner's licensed
  Explosive "RPG Character Mecanim Animation Pack", shared by Drive link; FBX/.meta files stay outside the repository), then
  "convert the greatsword as well".
- Change (presentation only): offline converter `scripts/mocap/convert-explosive.mjs` (+ `explosive-pack.mjs`: FBX 7.3 load,
  Z-up fix, 3ds Max biped → Mixamo bone names, T-pose from the Unity avatar in the .meta) writes the generated tables
  `src/client/mocap/{sword,daggers,shield,greatsword}-clips.ts`. Guard idle, walk and run carries, block, block impact and
  stagger come from the pack. Cuts: sword R1 forehand, R2 backhand, R6 high diagonal; daggers reverse grip in both fists, R1
  / L1 / R2 thrusts and stabs, the 4th L1 under the full-body spin; shield Attack3 lunge and Attack1 rising bash (the pack's
  left-arm shield read onto the game's right hand); greatsword (2Hand-Sword) high guard, Attack5 overhead forehand, Attack8
  flat backhand, both hands on the hilt (one-handed only in the run carry). The Mixamo greatsword converter stays as a
  documented way back.
- How the captures fit the game: each cut's [start, contact start, contact end, settled] marks retime onto the
  authoritative windup / hit window / recovery (contact always inside the hit window; sweep directions match the server's);
  windup pace keys (`MocapClip.pace`) spend the short game windups where the pose moves (daggers keep 35% of the captured
  pull-back, sword cut 2 60%). Cuts stand on the procedural phrase's footwork, the captured hips lowered just enough for the
  planted feet to reach the floor; guard, block and stagger keep the gait's feet under the captured hips (legs re-solved
  onto the gait's targets). The converter keeps the greatsword's two-handed hilt (fists, pommel, blade root) off the voxel
  head, pauldrons and torso and brings the support grip within reach. At runtime the blade clearance turns and lifts blades
  off the body and floor; a final floor guard runs only on grounded frames a captured layer posed.
- Review: two adversarial rounds. Round 1 (13 findings, all fixed): skating cut feet, loop-seam elbow pops, dagger wrist
  flips, block foot slides, follow-ups entering from the guard, floor-lift flips, among others. Round 2 (14 findings, 11
  fixed): greatsword hit/parry feet popping 13-15 cm and sinking 6-7 cm (now no worse than procedural), cut feet hovering up
  to 9 cm (now ≤ 2 cm), the greatsword run blade snapping ~1 rad each stride (carries now ≤ 0.02 rad per 1/960 s on every
  weapon), the backhand's 35 cm pre-contact blade drop and a recovery snap (cuts now continuous at 3840 Hz), fists 5-14 cm
  inside the head (Attack2 replaced by Attack5; fists ≥ 3 cm clear), the support hand 9 cm off the hilt in the guard (max
  5.9, mean 0.8 cm), the roll blend ignoring its slerp, the floor guard acting on specials and juggles, and the clearance
  cost (greatsword run 1.34 ms → 0.28 ms per rig-frame; broad phase per body part, covered carry layers skipped), and no
  test of the greatsword's sweep directions. Regression tests added for each. Not fixed (low): an Echo Strike follow-up
  after the dagger spin still unwinds ~150° in ~40 ms (the procedural phrases do the same); the converter's guard-heading
  shift tilts the chest up to ~6.5° where its hip/spine lean eases engage; no test drives an Echo Strike follow-up through
  a captured set.
- Impacts: hit timing, hitboxes, damage, cooldowns, movement rules, attack travel and network behaviour unchanged (client
  presentation only; nothing in src/shared or the server reads the clips). First-person view models and the procedural
  phrases (MOCAP off) unchanged. `rarity-combat` "dagger primaries and echoes alternate ..." (a baseline failure that asserts
  the two dagger cuts are exact arm mirrors) still fails with different numbers: the captured cuts are not mirror images.
- Known limits: the reverse-grip dagger turned up out of the floor in a 60-90 ms windup rotates up to ~1 rad within a
  millisecond, and sword cut 2's windup turns ~0.07 rad within 1/15360 s (both continuous, a flick at 60 fps; the sword's was
  0.25 before); the shield's procedural hit reaction over the captured guard dips an ankle 1.4 cm (as before this work).
- Checks: full unit suite under Node 24.19.0 (the portable runtime's major; the updater test needs it): 1276 tests, 1263
  pass, 12 fail, 1 skipped, the 12 failing names identical to the `b19733a` baseline; pvp-golden traces, melee-fluidity,
  locomotion-root-sync, reaction-fluidity, block-riposte and blocking pass in it. `melee-mocap` 16/16 and `greatsword-mocap`
  8/8, new this round: planted cut feet within 20 mm of the phrase's, hit/parry reactions no worse than procedural (no
  ankle under -0.82), carries < 0.04 rad per 1/960 s, the roll blend, floor-guard gating, greatsword sweep signs, greatsword
  cuts continuous 960 vs 3840 Hz with the fists out of the head, guard support hand < 8 cm (mean < 3.5 cm). check:types
  pass; server tsc only its 4 baseline errors. Lab strips (scratch) of both greatsword cuts, guard, run and block reviewed.
- Not run: native Windows look and feel, online play with remote players, owner review of the new style.

## 2026-10-05 — Claude — Melee block riposte (blocking melee is worth it)
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` 6ad1516 (PR #48 merge). Owner request 2026-10-04:
  "Make blocking melee actually worth it, maybe longer stunned time? enough to get a hit or 2".
- Before: the blocker stayed locked in its own 0.30 s Block window and the attacker's 0.25-0.95 s stun often ended first;
  at a late block, a counter landed in 11 of 20 weapon pairings and a second hit in none.
- Change (`successfulBlock`, melee kind): when a human standard-melee blocker (Daggers, Sword, Shield, Greatsword) blocks
  and the attacker is not the Siren, the attacker's stun is `max(old stun, min(1.4, ready + 1.0 + 0.004 × blocked damage)
  + chip walk-back)` (`ready` = the blocker's attack cooldown + windup / attack rate; walk-back = the extra rebound of
  block-force chips over the base push, as distance / run speed; `BLOCK_PUNISH`, `meleeBlockPunish`). The Block window keeps
  running (guard, recoil and greatsword blockHit clip, protection from other hits) until the blocker's attack, special or
  roll actually starts (`riposteTry`; an action that cannot start keeps the window), mirrored in the predictor via the
  optional `riposteAt` view field. The blocker's first landed direct hit on that attacker ends the riposte stun
  (`settleRiposte`; an absorbed hit or another player's leaves it), and the riposte clears the attacker's stagger-escape
  immunity, so the counter's normal follow-up applies (Dagger pair stun, Shield knockdown, Sword/Greatsword stagger). The
  parried attacker's queued afterimage echoes spare the blocker (they still hit others). Bot, ranged and magic blockers and
  a Siren attacker keep the old stun (bot difficulty, boss fights and pvp-golden unchanged). Client: the parry stagger keeps
  its old pace (shaped on 0.95 s) and holds the opened guard, so a long stun never plays in slow motion.
- Measured (sim duel, block on the tick before the strike, counter walks into reach, then swings): every pairing keeps one
  free counter, two for Daggers and a Shield knockdown, up to a 0.65-0.75 s host-time reaction (0.65 s worst: Greatsword
  vs Greatsword), also with Bulwark (×1.8 block force). Repulsor (×3) loses it in 2 of 16 pairings (Sword or Shield
  attacker vs a Greatsword blocker): its ring-out trade. Stuns 1.15-1.4 s (1.8 s with Repulsor), but the counter ends them.
- Review: two adversarial rounds. Round 1 (window ended at impact): 9 confirmed findings, fixed by the v2 design (guard and
  recoil visible online, no echo or Emperor-jab holes, counters keep knockdown/stagger, only standard-melee blockers riposte,
  concrete-value tests). Round 2 (v2): stagger immunity or an absorbed counter freed the attacker early; block-force chips
  pushed the attacker out of reach; a failed attack/roll dropped the guard; echoes were deleted for everyone; the window was
  too tight for online delay. All five fixed, each covered by a test that fails without its fix. Round 3: one display-only
  finding (the counter's hit reaction kept the riposte end time), fixed with a test; no regressions found elsewhere.
- Impacts: hit timing, hitboxes, damage, knockback and cooldowns unchanged; the blocked attacker's stun length and the
  human melee blocker's ability to end its own window early change. Network: optional `riposteAt` snapshot field (old
  clients ignore it; old hosts never send it, so the predictor behaves as before); no new caps or inputs.
- Checks (Node 24.19.0, at cc0e9b6): check:types pass; server tsc only its 4 baseline errors (same lines as 6ad1516);
  block-riposte 7/7 (each review fix fails its test when disabled), blocking, reaction-fluidity 16/16, all pvp-golden traces
  identical; full unit suite 1258: 1245 pass, the 12 known failures, 1 skipped (as before); network block fails only its
  baseline "65.5 !== 77" (a projectile reflection check) and default only "Timed out: rematch", as on 6ad1516.
- Not run: native Windows feel and look of the riposte (guard hold, parry hold, counter), real online/relay latency (the
  1.0 s window is sized from the measured sim timings plus estimated delay), owner balance playtest.
- Status: merged into `claude/visual-revision` at `0aa70dc` (PR #49; PR CI: unit 1258 with only the 12 known failures,
  server build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not
  the live feed or Site): test `0.2.e07d5479254f` at channel `d67f3b6`; schema 1 `arena-5c6212e3dcbf4f0d.json.gz`
  24,647,325 B, schema 2 `arena-6f333439386ba6a2.pack` 20,539,120 B. Candidate (`--test-only --channel`, Node 24.19.0,
  tooling 35ea65d): every stage passed except the baseline-only verify (the same 12 names, 4 server tsc errors); repeat
  build identical. Raw feed served the new manifests; old-launcher and v2 installs at `0.2.6a229a16de6f` updated, then
  reported current; settings sentinel untouched; menu status `0.2.e07d5479254f`.

## 2026-10-04 — Claude — Double-tap-W sprint, Tab melee lock-on, roll-cancel of melee attacks, Escape menu board
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` d31fa25 (PR #47 merge). Owner requests 2026-10-04.
- Sprint: two W presses within 300 ms latch a held `Input.sprint` while W stays down (client `SprintTap`); shared rule
  (`movement.ts` `SPRINT`, `sprintIntent`) gives ×1.35 run speed when moving within ~53° of the aim, not while attacking,
  blocking, aiming, guarding, swinging, stunned, locked or swimming; works in the air. No stamina cost. Sent only to hosts
  with `caps.move` (and offline). Snapshot `sprinting` only when true; extra forward lean on the rig. Bots never sprint.
- Roll-cancel: a human pilot's fresh roll press cancels a live standard melee primary (sword, daggers, shield, greatsword;
  windup, sweep, recovery or Echo follow-ups) or an Emperor jab. No stamina refund; the attack cooldown is refunded to the roll
  duration only when the cancel comes before the first strike (`beforeStrike`; Echo follow-ups are marked and never refund).
  E specials, Emperor Electric, Siren moves, casts and reloads stay committed. Bots never cancel. The predictor mirrors it on
  `caps.move` hosts; the cancelled attack's pose ends (`rollCancelled`) so it never resumes after the roll.
- Lock-on (client only, `src/client/melee-lock.ts`): for a melee pilot alive in a fight, Tab locks the nearest enemy in view
  (16 m, 50° cone; players, PvE adds, enemy summons, Slenderman; never allies or the hidden boss); a tap cycles clockwise,
  holding 0.4 s releases; the lock ends when the target dies, disappears or passes 24 m, or the pilot stops being eligible.
  Yaw/pitch ease toward the target and mouse look pauses while locked; a reticle marks the target. Line of sight is required
  to lock or cycle; behind cover the lock stops steering and releases after 1 s. Otherwise Tab keeps the Builds board.
  Escape now shows the match menu with the player/party board beside it (stacked below it on windows ≤1180 px; not in the
  Dev Sandbox, whose panel sits there) (owner suggestion).
- Impacts: hit timing, hitboxes, damage and cooldowns unchanged for attacks that are not cancelled; a cancel before the strike
  deals no hit, mid-sweep loses the rest of the arc and Echo follow-ups; movement gains sprint for humans only. Network:
  optional `sprint` and `rollCancel` input flags, optional `sprinting` and `followup` snapshot fields, `HOST_CAPS.move`.
  The host cancels only for inputs carrying `rollCancel` (sent with Shift by new clients), so older clients keep today's
  behaviour on a new host. pvp-golden unchanged.
- Checks (Node 22.22.0): check:types pass; server tsc 4 baseline errors; sprint-rollcancel 15/15, melee-lock 5/5, all 6
  pvp-golden traces identical; full unit suite 1250, 13 failures = the 12 known names + the Node 22 portable-update test;
  network motion, party and HTTPS pass (block and emperor-network fail identically on d31fa25; default only "rematch").
  Two adversarial review rounds (4 areas): 15 findings fixed. Real-app (Vite dev server, headless Chromium): Tab lock turns
  the camera onto the dummy with the reticle, hold releases, death releases; sprint 6.8 → 9.18 m/s; greatsword roll-cancel
  clears the commitment (stamina 100 − 26 − 29); Escape menu + board at 1440 and stacked at 1100 px.
- Not run: native Windows feel (sprint lean, lock-on turn rate, roll-cancel blends), online/relay latency feel, bots sprinting.
- Status: merged into `claude/visual-revision` at `6ad1516` (PR #48; PR CI: unit 1250 with only the 12 known failures, server
  build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not the live
  feed or Site): test `0.2.6a229a16de6f` at channel `aac8363`; schema 1 `arena-25f71767d56ffd80.json.gz` 24,644,929 B,
  schema 2 `arena-7d91f8f13fbea198.pack` 20,536,649 B. Candidate (`--test-only --channel`, Node 24.19.0, tooling 35ea65d):
  every stage passed except the baseline-only verify; repeat build identical. Old-launcher and v2 installs at
  `0.2.57123b899145` updated, then reported current; settings sentinel untouched.

## 2026-10-04 — Claude — Siren PvE: much harder "Find me..." (more spawns), 30 s intermissions
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` 2ae916d (PR #46 merge). Owner requests 2026-10-04.
- "Find me..." (Siren phase 2): possession spawn rate ×1.8 → ×5 and alive ×1.6 → ×3, with a possession-only hard cap
  `HORDE.maxAlivePossession` 48 (waves and other phases keep 40, relay rooms 28): 18/30/42/48 adds alive at 3/5/7/9 per s
  for h=1..4 (was 10/16/22/29 at 1.08-3.24). 48 + 16 corpses fits the client's 64 impostors. Details: docs/PVE.md.
- Intermissions: 30 s each (were 12 s between waves and 8 s before the boss).
- Fixed (pre-existing, made common by the harder hunt): a party wipe during the hunt counted as "found" (Siren back at
  50%, chip-3 reveal events, possessed reward at defeat); a debug phase skip during the hunt left the add possessed.
- Impacts: PvE only. No change to hit timing, hitboxes, damage values, cooldowns, movement or PvP; pvp-golden unchanged.
  Network: up to 48 add tuples (+16 corpses) in phase 2 instead of 40; snapshot p95 50.4 KB at that worst case.
- Checks (Node 22.22.0): check:types pass; server tsc 4 baseline errors; pve-*.test.ts 203/203 (new pve-possession
  tests fail without their fixes); test:pve-perf ok at 48 adds; pve-flow 3/3; network pve passes (rawP95 33.3 KB);
  full unit suite 1231, 13 failures = the 12 known names + the Node 22 portable-update test.
  Before/after hunt matrix (72 hunts each): adds alive about 2×, downs 25 → 54, wipes 4 → 13, time to find unchanged.
- Not run: native Windows FPS at 48 adds, relay-hosted hunt, full `npm run test:pve-balance`. Owner playtest needed.
- Risks: phase 3 inherits the bigger field (median 26.5 adds when found, about 42 at h=4); kill income rises during the
  hunt, which still has no time limit (the add starts chasing at 60 s).
- Status: merged into `claude/visual-revision` at `d31fa25` (PR #47; PR CI: unit 1231 with only the 12 known failures, server
  build 4 baseline errors, websocket only "Timed out: rematch", HTTPS passes). Published to the test channel (not the live
  feed or Site): test `0.2.57123b899145` at channel `fee293c`; schema 1 `arena-8133bc1171033b69.json.gz` 24,638,975 B,
  schema 2 `arena-885f3d47d405ddc3.pack` 20,533,942 B. Candidate (`--test-only --channel`, Node 24.19.0, tooling 35ea65d):
  every stage passed except the baseline-only verify; repeat build identical. Raw feed served the new manifests; old-launcher
  and v2 installs at `0.2.fd485a10e3d0` updated from the live feed, then reported current; settings sentinel untouched;
  menu status `0.2.57123b899145`.

## 2026-10-04 — Claude — Sandbox rarity sort, keypress dummy cast, dummy respawn, DEV-only sandbox; Ward Zero PvP vents/machines
- Branch `claude/sleepy-ritchie-g8ruhv` from `claude/visual-revision` accc362 (PR #44 merge). Owner requests 2026-10-04.
- Chip list: strict tier order common → … → mythic → mishima → ansatsuken → l44t → siren (`RARITY_ORDER`; the old sort
  used catalog weight, so tiers interleaved), grouped per tier in the list; Rarity ↑ is the default sort.
- Dummy cast "Press key" (AI, E, Q, Block, Dash, Jump, 1-4, Mode): the dummy stands still, turns to the asking pilot and
  presses that key with the chip's weapon (holds where the kit charges or aims); keys the dummy's weapon has no use for are
  greyed out and refused. DEV kits are granted to the dummy as a scoped `weapon:` unlock, never the DEV role. Slenderman
  (Scare Tactics, E) stalks the pilot who asked. Repeats refresh the dummy's cooldowns (sandbox only). A Siren chip cast
  takes the sandbox to Ward Zero; any other chip restores the previous arena and stops her song.
- Dummy respawn: a killed dummy stands up on its own pad after a delay (panel "Respawn (s)", default 5, 1-30, 0 = the old
  round end), refilled and with once-per-round chips (Emergency Exit, Do Not Press, Round Two, CTRL+Z) re-armed; waits for
  animation holds and a vent-fall body landing. A downed human still resolves the round (bots beat the human, or a human
  duel) without the waiting bots.
- DEV-only sandbox: `sandboxAllowed()` = DEV account or the Vite dev server; the home link is hidden and the menu, WebMCP,
  `window.chipArena`, local debug commands, the panel and F3 are gated. Production build check: link hidden, no panel.
- Ward Zero outside PvE (PvP + sandbox): no Chip/Recruit/Heal machines, their pads or prompts (`machinesPresent` = a real
  PvE run). Vents: snapshots announce each opening `VENT_PIT.warn + lead` (1.5 s) early; the stage rattles the lid and fills
  the rim for 1 s, swings it 0.5 s ahead of the pit turning lethal, trails it on close, and keeps drawing the last opening
  while an interpolated view lags (online); positional metal cues. No add ever climbs out. Sandbox "Vent fall" forces a real
  opening (warn → swing → fall) and never hides or snaps the regular cycle.
- Impacts: no change to hit timing, hitboxes, damage, cooldowns or movement outside the sandbox. Ward Zero PvP keeps the same
  lethal vent schedule (presentation only leads it). Network: Ward Zero snapshots carry upcoming vent openings 1.5 s early;
  `snapshot.sandbox` (respawn/safe/repeat) in sandbox rooms only; revive reason `sandbox-respawn`. pvp-golden unchanged.
- Review: adversarial review workflow (4 areas): 13 distinct findings, all fixed, most with a regression test (the vent,
  round-end and fixture tests fail without their fix); verifiers confirmed the respawn fixes on the pushed code.
- Checks (Node 22.22.0): check:types pass (server tsc keeps its 4 baseline errors, identical on accc362); sandbox-tools
  16/16, ward-pvp-presentation 6/6, sandbox-access 1/1; full unit suite 1228, 13 failures = the 12 known names + the Node 22
  portable-update test (all fail on the base too); network pve and maps pass; default network only "Timed out: rematch".
  PR CI (Node 24.19.0) `ea042c6`: unit 1228, only the 12 known failures; server build only the 4 baseline errors; websocket
  only "Timed out: rematch"; HTTPS passes.
- Not run: native Windows, online sandbox room, packaging. Real-app captures (Vite dev server, headless Chromium/SwiftShader):
  tier groups, greyed keys, respawn after 3 s with the round going on, keypress Slenderman jumpscare, Ward Zero downed preview
  without machines, PvP vent warn → swing → open → fall with no add.
- Earlier publications (from the channel's PUBLISHED.md): PR #43 `492b251` -> test `0.2.6bb5c3639b53` (channel `7a393b8`;
  schema 1 `arena-38d7b91c0c5dbe17.json.gz` 26,206,825 B, schema 2 `arena-2870598814579f84.pack` 22,099,609 B); PR #44
  `accc362` -> test `0.2.cb814cb80ece` (channel `1bc3517`; schema 1 `arena-2bf02d3b1f9a2899.json.gz` 24,625,849 B, schema 2
  `arena-614b8df19c9400d9.pack` 20,530,767 B).
- Status: merged into `claude/visual-revision` at `29d184b` (PR #45); published to the test channel (not the live feed or
  Site): test `0.2.fd485a10e3d0` at channel `b2241e2`; schema 1 `arena-d23a8fd7f204c872.json.gz` 24,637,893 B, schema 2
  `arena-3d53ae883a980000.pack` 20,534,868 B. Candidate (`--test-only --channel`, Node 24.19.0, tooling 35ea65d) passed
  every stage except verify, which had only the baseline failures (12 known unit names, 4 server tsc errors, websocket
  "Timed out: rematch"); repeat build identical. Raw feed served the new manifests; an old-launcher install and a v2 install
  at `0.2.cb814cb80ece` each installed the new version from the live feed with `manual-update.mjs`, then reported current;
  settings sentinel untouched; menu status reports `0.2.fd485a10e3d0`. Native Windows look/feel review remains.

## 2026-10-04 — Claude — Developer Sandbox tools; Ward Zero as a PvP and sandbox map
- Branch `claude/sandbox-devtools` from `claude/visual-revision` 492b251. No PR opened; not merged; not released.
- Sandbox panel (`src/client/sandbox-panel.ts`): live chip search (name/id/description) + sort (default, rarity ↑/↓ by
  catalog weight, name; pure `findChips`), "Every chip" scope; Dummy cast (dummy takes the chip + a fitting weapon and
  fights as a hard bot for 4 s, repeat every N s, safe mode = humans keep 1 HP); sandbox map select; animation tests (vent
  fall + SPLAT 2/10 s, downed/HELP and holographic revive via the DEV fixture injector, death effect, killcam, weapon intro,
  melee/greatsword, hit/stagger, summons, summon deaths, Slenderman), slow motion (local only). Warm-up re-runs when drawn.
- Authority (`src/shared/sandbox-tools.ts`, sandbox debug commands only): dummy-cast, safe, map, vent-fall, down, hit,
  summons-die; DEV-only weapons need `canUse`. `respec` extracted from the spec command unchanged.
- Ward Zero (owner request): pickable PvP map (not in random rotation); outside PvE its vents run a fixed 12 s cycle as
  pits (the PvE VentPits code), vent-fall kill with normal attacker credit; PvP keeps normal respawn (no splat body); no
  adds, boss, machines or boss song (vents pause while a Siren pilot forces the map); bots use the existing vent steering.
  `VentPits.splatAt` (PvE default 10 s) and `lands`. Snapshot gains `vents` only on Ward Zero outside PvE.
- Impacts: no change to hit timing, hitboxes, damage, cooldowns or movement outside the sandbox and Ward Zero PvP.
- Checks: check:types pass; sandbox-tools 6/6; pve-* + pvp-golden 213/213 (all 7 traces identical); full unit suite
  1211, only the 13 known Node 22 failures; network pve passes; default network only "Timed out: rematch" (baseline).
  Playwright/SwiftShader screenshots of the panel, Ward Zero sandbox fall/splat, downed/revive, PvP Ward Zero match.
- Payload: `npm run build` stopped at the live guard: schema 1 26,218,140 B (+11,315 B; 3,740 B over 25 MiB, before
  this entry). Guard unchanged; the 32 MiB test channel accepts it. Not run: Windows, online sandbox room, packaging.

## 2026-10-04 — Claude — publication records: PRs #37-#41 and PR #42 on the test channel
- PRs #37-#41 merged `7b91b6c` -> test `0.2.7c06fa8f3a9f` (channel `d14910c`); schema 1 `arena-054ddd2d7339d417.json.gz`
  26,187,119 B, schema 2 `arena-96b2e5e50fd6120c.pack` 22,093,779 B.
- PR #42 merged `5bb0366` -> test `0.2.24a26b19d4ee` (channel `db0bad1`); schema 1 `arena-516d55633004c310.json.gz`
  26,187,925 B, schema 2 `arena-81b2d09164d54a85.pack` 22,093,555 B.
- Both: candidate (`--test-only --channel`, Node 24.19.0) passed every stage except baseline-only verify (12 known Node 24
  unit names, 4 server tsc errors, websocket "Timed out: rematch"); old launcher `run.mjs` and the dcc7430 seed
  `manual-update.mjs` each installed the new version from the live feed, then reported current. Live feed and Site untouched.

## 2026-10-04 — Claude — summon chips get real combat; Slenderman (Scare Tactics) rework

- Branch `claude/summons-slender` from `claude/visual-revision` 7b91b6c, merged with 5bb0366 (PR #42 freeze fix). No PR opened; not merged; not released.
- Summons in scope: zombies (Necronomicon, Graveyard Shift, Corpse Party), toy soldiers (Puppet Master "Toy Soldier"), and
  the L44T Scare Tactics Slenderman (DEV-only, unchanged access). No other chip spawns a creature.
- Summon combat (`src/shared/chaos.ts` SUMMON): windup → strike → recovery with damage on the strike frame. Zombie lunge+claw
  (windup .42 s, reach 1.75 m to start, 2.4 m checked at the strike, recover .48 s, 12 dmg); toy soldier aims and fires one
  round (windup .45 s, starts within 7 m, 9.5 m at the strike, cover blocks the ray, recover .55 s, 8 dmg; it now holds ~5 m
  instead of walking into melee). Damage through `sim.damage(kind 'minion')` credited to the summoner, blocks parry as before,
  no life steal on summon bites (as before). Targets: nearest enemy pilot (PvE boss unless hidden) or enemy summon; PvE adds via
  the horde assist, which now only chases and returns the add (the horde no longer applies its instant D38 hit).
- Health: pools unchanged from the chip texts (zombie 2× owner max HP, toy 25). New: burn/poison DoTs on chaos summons, all
  direct-hit procs (Static/Vacuum/Shrapnel) on summon hits, standard-melee specials hit summons in PvP. `minionDeath` event.
  Caps unchanged (14 zombies, 3 toys per owner, 34 total). Bots focus a hostile summon within 6 m that is clearly closer than
  their pilot target (`botFocus`).
- Slenderman (`src/shared/leet.ts`, SLENDER in leet-content): a 160 HP kinematic chaos minion (`kind 'slender'`, every damage
  path). Never moves while his target's authoritative camera (input yaw/pitch/camera mode, wall-clipped) sees his head or chest
  within 36°; staring builds sanity (.09/s + up to .5/s within 14 m); looking away decays it (.3/s). Unseen for .35 s and off
  cooldown (3.2 → 1.3 s over his 22 s life) he teleports behind the target to .72 → .5 × the gap. Reach (1.6 m) or sanity 1 →
  the existing 2.2 s jumpscare then knockout (`leet-scare`). Ends on owner death, expiry, recast, round reset or his death.
- Client: `summon-visuals.ts` (attack/flinch/corpse poses, tin-soldier look + rifle, tracer and pooled voxel break-up),
  summon health bars in `overhead-health.ts`, new Slenderman model `slender-model.ts` (suit/shirt/tie, faceless head, long
  limbs, six tentacles, sway, head tilt, teleport flicker/smear + glyph-pool static), victim-only static overlay and
  drone/heartbeat `slender-static.ts`. All meshes use the rig's Lambert palette and existing MeshBasic variants (no new programs,
  no lights); static is DOM/CSS, audio is Web Audio oscillators (no assets).
- Impacts: hit timing (summon damage now on a strike frame, was instant), hitboxes (Slenderman capsule .28 r × 2.46 m, new),
  damage (unchanged numbers; DoTs/procs now apply to summons), cooldowns (zombie ~.9 s cycle unchanged, toy 1.0 s, Slenderman
  hop cooldown new), movement (attack windups/recovery root summons; toy keeps range; Slenderman teleports only), networking
  (snapshot: summon hp whole numbers, timers to ms; leetFields gain slender/seen/sanity/hops/hopAt/from/yaw/hp; new events
  minionShot, minionDeath, leet-slender-hop). pvp-golden: only `necronomicon` regenerated (header note); the other six identical.
- Checks: `npm run check:types` pass; `tests/summons.test.ts` 16/16; chaos, leet, chaos-visuals, first-block, pve-foundation,
  chip-vfx-sweep, pvp-golden pass (known baseline failures excepted). Full suite once: 1205 tests, 15 failed = the 13 known +
  2 chip-vfx-sweep tests (synthetic snapshots without `events`), fixed and rerun green. `run-network pve` pass (victory);
  default network fails only the known "Timed out: rematch"; `run-network chaos` fails at its cosmetics assertion on the base
  too (pre-existing, before the summon section). `npm run build` ok: live payload 26,206,719 / 26,214,400 bytes.
- Not run: Windows/GPU, real multiplayer, audio listening, packaging. Lab captures via tools/visual-lab vfx.html
  (zombie, toy, summonhit, summondeath, slender, slendermodel, slenderhop); health bars do not draw in the lab (not "playing").
- Risks/next: balance of the Slenderman (sanity rates, 160 HP, hop shrink) and the toy's ranged shot need owner playtest.

## 2026-10-04 — Claude — random lag spikes (full-screen freezes, every mode, offline)

- Branch `claude/lag-spikes` from `origin/claude/visual-revision` 7b91b6c. Implemented on branch; no PR, not merged, not published.
- Method: Playwright + SwiftShader real-app runs (dev server) of a PvP bot match, the Developer Sandbox firing every gun
  and magic school, and an offline TEST BOSS run (waves, spawns, Siren phases 2-3). Recorded a rAF frame log, long
  tasks, a CDP CPU profile and a trace of every WebGL `linkProgram`. Also timed a headless local PvP match in Node with
  `--trace-gc`.
- Cause 1 (the freezes, worst when shooting): GPU programs were linked mid-game. three.js links a program the first
  time a new material variant is drawn, and deletes it when the last material using it is disposed. Per-event materials
  (projectile trails per shot, melee trails, impact glows, death fireworks, wolf-landing dust, Siren claw slashes) were
  re-linked every time the previous effect of that kind had expired. Pooled effects (chip blasts, sparks) linked on
  first use. On the boss stage the seeker's point light changed the light count, which re-keys every material.
  Fix in `src/client/shader-warmup.ts` (renderer only):
  - every linked program is pinned (cap 512);
  - the first compiled ShaderMaterial of each shader source is kept, so its stage ids stay stable;
  - on a new scene composition (map, PvE stage, round) the scene and hidden probes for per-event variants are compiled.
    When a Siren is present, this runs once more with one extra point light.
  SwiftShader evidence:
  - firing test: main-thread shader-compile time 31.2 s -> 2.5 s, links after start 64 -> 9, hitches over 950 ms 26 -> 0
    with a link (all 26 had been link frames);
  - PvP: links after start 22 -> 3;
  - PvE boss fight: links 17 -> 2 (Siren slash 13 -> 1), in-fight compile time 28.6 s -> 5.7 s.
- Cause 2 (GC pauses): the killcam history ran structuredClone on the whole snapshot at 20 Hz and kept 6 s of it, so
  the static map's props were stored again in every frame. Fix: `Killcam.record` reuses the previous frame's copy of a
  prop or event when it is equal (`sameData`); players and the rest are still copied. Headless PvP (5 hard bots, 2 min):
  major GC max 30-55 ms -> 6-9 ms, total GC about 1.7 s -> 1.0 s, sim step p999 12-14 ms -> 5.5 ms. Playback is
  unchanged (the killcam tests pass).
- No gameplay, snapshot, network or save changes. Hit timing, hitboxes, damage, cooldowns and movement are unchanged.
- Checks (Node 22.22.2): `check:types` passes. The new `tests/lag-spikes.test.ts` passes 5/5. pvp-golden passes 7/7.
  `tests/pve-*.test.ts` + killcam + lag-spikes pass 217/217. The full suite had 1189 tests and only the 13 known
  failures. `run-network.mjs pve` (TEST_PORT 8823) reached victory, deflated p95 5620 B. `npm run build` gave a live
  payload of 26,187,878 B against a limit of 26,214,400.
- Not fixed or not reproduced:
  - offline PvP still builds a full snapshot every tick on the main thread (about 1.8 MB/tick of short-lived allocation,
    mostly Rapier vector wrappers and prop views);
  - intro and cutscene light-count changes (Siren intro, bass-van cabin light) still compile once each per session;
  - auto-graphics RenderBudget resizes were not seen;
  - no audio-decode, HUD or worker catch-up hitch was found;
  - SwiftShader cannot show real GPU timings.
- Next: owner test on Windows. The first match after launch may still compile once during the intro or countdown.

## 2026-10-04 — Claude — PvE vent pits: black open vents, vent-fall deaths, timed splat

- Branch `claude/pve-vent-pits` from `origin/claude/visual-revision` 8840198, merged with 3457323 (PRs #37–#40). Not merged, not released, no PR.
- Visual: an open Ward Zero vent draws a shaft into the floor quad (per-pixel ray into a 2.1 m box: seamed walls and rungs fading to black, black bottom, faint red lip). It replaces the old black plane that the floor tiles showed through. Its opacity follows the existing lid curve (`entryOpenAmount`, moved verbatim to `src/shared/pve/vent-pits.ts` and re-exported from `client/siren/stage.ts`), so timing is unchanged. It is fully dark at the lethal open amount (0.5).
- Fall (server, `VentPits` in `pve/vent-pits.ts`, stepped from `PveSystem.stepAfterPhysics`): a connected, alive member or recruit whose capsule centre is within 0.78 m of an open vent (open amount ≥ 0.5) and not jumping dies at once with kind `vent-fall`. Credit goes through `Simulation.damage` like the `fall` kill, so a recent attacker within attributionSeconds is the source and killer. Roll i-frames, the revive shield and chip saves do not hold the floor up. God mode and hidden players are exempt. The boss and adds never fall. Closed vents are solid. PvP Ward Zero vents never open, so PvP is unchanged; pvp-golden is identical.
- Splat: at death+9.4 s the record becomes `vent-splat` (new death id, same source and kind, `landAt`). The body falls 13 m and lands at exactly death+10 s on `splatSpot()`: the hall centre, otherwise the nearest clear ring point off blocks, machines, vents, the boss (radius × scale + 2.4 m) and other bodies. It is then a normal kept body. `inVentShaft()` blocks paid revives, beacons, the HELP bubble and the F prompt until it lands. The Tab board and HUD show "In a vent · lands in Ns".
- Respawn rules: a wave/phase `respawnAll` during the shaft time revives normally on a pad and skips the splat. The solo self-revive waits for the landing and then revives at the landing spot. Holds shift `landAt`.
- Bots: `VentPits.steer` deflects party-bot and recruit moves around open or scheduled vents. Recruits can fall but have no splat (no kept body).
- Snapshot: only the optional `DeathPresentation.landAt` was added. Client effects are in `client/pve/vent-fall.ts`. Audio reuses dash, jump, arcade-heavy, water and arcade-punch. Small economy.ts gates. horde.ts and entries.ts were not touched.
- Checks (Linux, Node 22): `check:types` passes. The new `tests/pve-vent-fall.test.ts` passes 7/7. `tests/pve-*.test.ts` passes 200/200. pvp-golden passes 7/7. `run-network.mjs pve` reached victory. The full suite had 1184 tests and only the 13 known failures. `npm run build` gave a live payload of 26,187,038 B against a limit of 26,214,400 (27 KB headroom). Pve-lab images were captured under SwiftShader (kinds ventfall and splat were added).
- Not run: Windows/GPU, online multiplayer by hand, packaging, publication.
- Next: owner review of the pit look and the splat. The tight update headroom matters for the next asset change.

## 2026-10-04 — Claude — PvE: Siren adds take hits while entering
- Branch `claude/pve-adds-hittable-entering` from `origin/claude/visual-revision` `8840198`. Implemented on branch; not merged/published.
- Root cause: an entering add had no physics body (created only at `activeAt`), and `Horde.hit()/status()` returned early unless
  `phase==='active'`, so every damage path (all go through registered colliders / `chaos.minions` -> `hitAdd`) ignored it.
- Now an entering add registers its body when it comes into sight (`shared/pve/entering.ts` `entryExposedAt`): doors/cells at
  `walkAt` (leaves/teeth open, walk starts); vents at climb age push+open .8 s (grate open, head and hands over the rim) =
  `activeAt-1.15`, also for re-emerging travellers. The body has no gravity and a no-contact collision group (queries still
  hit it) and follows the drawn body every tick: the door/cell walk, or the vent climb (`VENT_CLIMB_BODY` keys now shared
  with the client pose). Pending, behind closed leaves/teeth/grate and underground adds stay unhittable.
- Hits on entering adds: damage, gibs, crawl, stagger, DoTs/slows apply; no knockback until active (stays on its path); no
  attacks (AI unchanged). Kill: corpse at the path point (vent: the climb exit, not over the pit), entry slot released,
  credit/money once. An entering add that loses a leg reports state `crawl` (entry stays >= 0); client `enteringView()` keeps
  it on its entry path and keeps the entry pose for a corpse killed on the way in. Snapshot encoding unchanged.
- Hit timing/damage/cooldowns unchanged; hitboxes: new for entering adds only; PvP untouched (pvp-golden passes).
- Checks: check:types pass; new tests/pve-adds-entering.test.ts 6/6; tests/pve-*.test.ts + combat-fx + pvp-golden 214/214
  (one pve-horde assertion updated: entering adds are in chaos.minions once in sight); `run-network.mjs pve` (TEST_PORT 8817)
  victory, deflated p95 5715 B; full suite 1162 pass / 13 fail = the 13 known Node 22 failures; build live payload
  26,170,710 B (limit 26,214,400). Not run: Windows hand-play, visual check of hitbox vs. climb in the client.
- Balance note: adds are now easier to kill at doors, cells and vents (about 1.2 s / 1.0 s / 1.15 s exposed before active).

## 2026-10-04 — Claude — melee attack stamina 40% lower (all melee)
- Branch `claude/melee-stamina` from `origin/claude/visual-revision` `b7f3dad`.
- Owner: "Lower stamina drain from melee attacks by a good portion, on ALL melee." Primary swing cost (`meleeCost`,
  souls-combat.ts) ×0.6: daggers 16→10, sword 30→18, shield 36→22, greatsword 44→26. Echo Strike follow-ups 5→3
  (`followupCost`). E specials keep their costs (30/38/45/55). DEV melee (Emperor, Ansatsuken) unchanged (already 3–4/hit).
- Gameplay change: stamina only. Hit timing, hitboxes, damage, cooldowns, movement and networking are unchanged.
- Tests: three pins updated with comments (souls-combat costs, melee-reference-update dagger spend, modifiers stamina gate).
  pvp-golden melee-dev, siren-melee, siren-ward-zero regenerated; with the old costs all seven traces matched the old hashes.
  Full Node 22 suite before pin updates: only those 6 plus the 13 known failures; pvp-golden 7/7 and the 3 updated files pass.

## 2026-10-04 — Claude — greatsword blade clears the shoulders (mocap follow-up)

- Publication (previous task): PR #36 merged 076afe5 -> test 0.2.7f4cf400e693; channel c351c41; schema1 arena-79bbf8ca845d2b9e.json.gz 26,167,432 B, schema2 arena-4bbd4d77d7a7b6cf.pack 22,081,531 B; old launcher and seed manual-update both installed then reported current; candidate verify baseline-only.
- Task: owner report that the greatsword runs through the shoulder. Branch `claude/greatsword-shoulder` from
  `origin/claude/visual-revision` 076afe5. Implemented on branch; no PR, not merged, not published.
- Cause: the run carry (shoulder rest) had its grip dragged toward the support shoulder (wide voxel shoulders), laying the
  blade through the chest and right pauldron. Converter: the support hand now lets go where reaching the grip would need
  a shift over ~.25 m (the clip table is regenerated). Runtime: `src/client/mocap/blade-clearance.ts` turns the blade
  about the grip (and lifts the grip off the shoulders/chest) until the blade slab clears the pauldrons, chest shell and
  head by 2 cm, and rolls the flat onto a nearby surface; continuous and stateless; cuts only turn the blade (no grip
  shift). Visual-lab fix: the raw `mocap=` preview import no longer breaks the lab on a clean checkout.
- Impacts: presentation only. Hit timing, hitboxes, damage, cooldowns, movement and networking unchanged.
- Checks (Node 22.22.2): `npm run check:types` pass; greatsword-mocap (new clearance test over idle/walk/run/block),
  pvp-golden and the 8 MOCAP-off melee/locomotion files: 86/86 pass. Lab strips (run/walk/idle) reviewed.
  Not run: full suite, build, Windows/GPU, multiplayer.
- Next: owner check of the run carry; the blade now rides up against the outside of the right pauldron rather than
  lying flat across its top.

## 2026-10-04 — Claude — killcam uses Call of Duty timing
- Branch `claude/killcam-cod` from `origin/claude/visual-revision` `076afe5`.
- Owner: the ~4 s aftermath "feels too long"; use Call of Duty logic. `KILLCAM` before 1.5 → 3 s (lead-in from the killer's
  view), after 4 → 1.25 s, hold .6 s unchanged: ~4.85 s in all (was ~6.1 s), so a round-ending kill fits the 5 s result
  screen. Skip, killer camera and the one-way cut to the body are unchanged. Client presentation only: no change to hit
  timing, hitboxes, damage, cooldowns, movement, respawn/result timing or networking.
- Checks: killcam unit + presentation 16/16, killcam network scenario passes, `npm run check:types` passes. Not run: full
  suite (killcam constants and their tests only), Windows hand-play.

## 2026-10-04 — Claude — publication record: PR #34 + PR #35 on the test channel
- Owner confirmed publication and granted standing permission for pushes, merges and test-channel updates (2026-10-04).
- PR #34 merged `c307925`, PR #35 merged `ab2e4c5` -> test `0.2.31513375c335` (channel commit `e49b238`).
  Schema 1 `arena-627b9d85d7fb7712.json.gz` 26,148,392 B; schema 2 `arena-e002c9381d84a9a6.pack` 22,068,484 B.
- Candidate (`candidate.py --test-only --channel`, Node 24.19.0): every stage passed except baseline-only verify (12 known
  Node 24 unit names, 4 server tsc errors, websocket "Timed out: rematch"); source-privacy and channel-e2e passed.
- Verified from the live feed: old launcher `run.mjs` 0.2.19e114ae7c87 -> new then "up to date"; dcc7430 seed
  `manual-update.mjs` test-seed -> new via schema 2 then "current". Live feed and Site untouched.

## 2026-10-04 — Claude — greatsword Mixamo mocap (client visual only)

- Task: owner-supplied Mixamo "Great Sword Pack" converted into the voxel rig's pose system for the greatsword.
  Branch `claude/greatsword-mocap`, base `origin/claude/batch-intros-fx-killcam` ec169ed (PR #34 → visual-revision).
  Implemented on branch; no PR opened, not merged, not published.
- Changes: offline converter `scripts/mocap/` (three FBXLoader in Node; Mixamo → rig joint rotations, hinge knees,
  heading, hips height, chest-space IK targets for grip/blade/edge/poles/free hand; root travel stripped; quantized,
  delta-coded) writing `src/client/mocap/greatsword-clips.ts` (8 clips, ~22 KB source, ~8 KB gzip). Runtime
  `src/client/mocap/` (decoder, Catmull-Rom sampling, C1 retime, rig pose through rig-ik, per-weapon sets). Greatsword
  third-person rig: captured guard idle, walk/run upper body over the gait (stride-synced), alternating cuts
  (slash / slash (3)), block raise + blocked-hit impact, hitstun reel under the procedural impulse. FBX/zip never
  committed; rerun steps in DEVELOPMENT.md. Other weapons, first-person view model, wolf-spin special, rolls,
  knockdowns, deaths (ragdoll) and intros keep their procedural code.
- Timing: each cut is retimed so its captured contact (blade crossing the front arc) spans swingAt..swingUntil and
  settles at the end of recovery, at any attack rate. Hit timing, hitboxes, damage, cooldowns, movement, networking,
  snapshots and src/shared are unchanged (pvp-golden passes in the full suite).
- Tests: new `tests/greatsword-mocap.test.ts` (table, contact alignment within a frame, joint limits, limb continuity,
  two-hand grip). Eight procedural melee/locomotion suites now set `MOCAP.enabled=false` so they keep guarding the
  procedural layers (still live for first person and other weapons); their greatsword choreography assertions
  (kinetic-chain lag, low-then-rise sweep, two-beat recovery, sprint carry, transition turn bounds) describe the
  replaced third-person phrases — owner review of that scoping requested.
- Checks (Linux, Node 22.22.2): `npm run check:types` pass; full unit suite 1170 tests, only the 13 known failures;
  `npm run build` pass, live payload 26,167,005 B (limit 26,214,400; +18,673 B vs 26,148,332). Visual lab strips
  (SwiftShader) before/after reviewed. Not run: Windows/GPU, multiplayer, packaging, update-channel publication.
- Risks / next: owner playtest of feel (hoist is the capture's speed, ~as fast as the strike; turning in place slides
  the captured idle stance; deep low sweep on cut 2). Mapped clips can be swapped in convert-greatsword.mjs.

## 2026-10-04 — Claude — killcam starts closer to the kill (result screen stays 5 s)
- Branch `claude/killcam-forward` from `origin/claude/visual-revision` `c307925` (PR #34 merge).
- Owner: keep the 5 s result screen, just move the killcam forward. `KILLCAM` before 2 → 1.5 s and live hold 1.2 → .6 s;
  aftermath stays 4 s. The kill now plays ~2.1 s after death (was 3.2 s), so a round-ending kill shows ~2.9 s of
  aftermath before the 5 s result ends (was ~1.8 s); other deaths show the full 4 s. Client presentation only: no change
  to hit timing, hitboxes, damage, cooldowns, movement, respawn/result timing or networking.
- Tests: killcam pins/timing updated (`tests/killcam.test.ts`, `tests/killcam-network.ts`); killcam unit + presentation
  16/16 pass, killcam network scenario passes, `npm run check:types` passes. Not run: full suite (change is confined to
  killcam constants), Windows hand-play.
- Publication record: PR #34 merged `c307925` (intros refresh, combat FX on adds, hold-to-aim, ADS zeroing, killcam
  aftermath). CI was baseline-only (4 server tsc errors, known Node 24 unit names, websocket "Timed out: rematch"; a
  first-run `sawDestruction` websocket failure did not recur on the one rerun). Not yet published to the test channel:
  publication paused for owner confirmation after an auto-mode permission block.
- Next: owner confirms publication; build the test candidate from the merge of this branch and publish both feeds.

## 2026-10-04 — Claude — weapon intros refresh, 3 s lobby return, ADS sight alignment, Emperor audio leak, AoE outlines

- Branch `claude/intros-refresh` from `origin/claude/visual-revision` `dcc7430`. No PR (orchestrator merges). Not merged,
  not published.
- Publication record (pending from the integration notes): PR #33 merged `dcc7430` -> test `0.2.1412350a765a`; channel
  `ae36a7e`; schema1 `arena-255764ffce5226cd.json.gz` 26,135,339 B (old launcher network update verified
  0.2.5036b50f0c34 -> new) and schema2 `arena-258068ee0d6f182c.pack` 22,063,628 B (seed network update verified via v2);
  candidate checks passed except the baseline verify; private GitHub Release creation blocked for that session (HTTP 403),
  seed ZIP at `releases/channel-dcc7430` for the owner to upload.
- Lobby return: the host's Return to lobby countdown is 3 s (was 5 s), `LOBBY_RETURN_SECONDS` on the authoritative sim
  (server and host rooms). It is the only automatic lobby timer; PvP match results and PvE results still wait for a click.
- Weapon intros (11 non-DEV): `weapon-intro-effects.ts` now draws only pooled ChipBlasts voxels (muzzle cones, tracers,
  casings, smoke, blade trails, a voxel crescent, honeycomb barrier, quake/fire/shock/void/kinetic blasts in miniature,
  serpent and portal voxels) instead of rings, cones, planes, tubes and line bolts. Fixed 60 Hz stepping from age 0 with a
  reseeded generator keeps it a pure function of age (scrub/late join/frame rate identical); one ChipBlasts per stage.
  Motion: magic intros hold the staff in the left hand and cast with the right (as in-game Q/E); guns get an ADS cheek
  weld and recoil into the shoulders. Hits, name card (3.2 s), 4.8 s duration, cameras and intro audio unchanged.
- ADS (owner: sights must line up with the shots): aimed gun shots launch on the ballistic arc that meets the crosshair ray
  hit point (authority, `spawnProjectile`); speed/gravity/damage/spread unchanged, hip fire unchanged; ADS bots aim straight.
  First-person view model rotates/shifts so each gun's sight line (pistol sights, MG carry handle, rifle scope axis) runs
  down the camera axis. This is a gameplay change (ADS no longer drops below the crosshair at range).
- Emperor audio leak (lightning staff and rifle): client cues mapped Blink Strike, its teleport and Orbital Mark to
  Emperor's `electric-hook` clips. Now dash/lightning cues; electric-hook plays only for Emperor events. The authority was
  already Emperor-gated (Q's legacy `electric` input is ignored for other specs).
- AoE outlines removed: field voxel rims and dome wireframes, blast/impact ground rings (now fill the disc to 92 % of the
  radius), outline telegraph motes (now inside the area), Orbital Mark shrinking ring, melee/wolf/impact rings. Areas read
  through their volume plus an edge-less ground glow that keeps the enemy/ally tint. Gameplay radii unchanged; the
  ability aim preview path is untouched.
- Animation impact: intro-only presentation; no hit timing, hitbox, damage, cooldown, movement or network change from the
  intros. ADS zeroing changes ADS projectile launch pitch only.
- Tests: new `weapon-intro-effects`, `ads-alignment`, `emperor-audio-isolation`; updated lobby-return (3 s), chip-blasts and
  chip-vfx-sweep (no outlines), ranged-magic-fluidity (sights on the crosshair), ability-presentation (blink cue),
  abilities-foundation (rifle scoped launch now zeroed). pvp-golden: ffa-foundry and teams-moonlit regenerated (gun bots ADS;
  verified identical with zeroing disabled); the other five traces unchanged.
- Checks (Linux, Node 22): `npm run check:types` pass; full `npm test` 1132 tests, failures = the 13 known Node 22 baseline
  names plus 3 caused here and then fixed (ADS rifle launch test, two golden traces) - all three pass after the update;
  pvp-golden 7/7; `npm run build` pass, live payload 26,140,097 B (< 26,214,400). Visual lab before/after strips for all 11
  intros, ADS first-person sights with a crosshair, and four area effects (SwiftShader).
- Not run: duel-balance (exceeded 10 min; ADS zeroing makes aimed pistol/MG/rifle shots, incl. 25 m+ ADS bots, more
  accurate at range - needs an owner balance look), native Windows/GPU, online multiplayer, packaging/update publication.
- Next: owner review of intro look/feel and ADS feel at range; merge, then the standing test-channel publication.


## 2026-10-04 — Claude — combat FX on extra projectiles, weapon effects on Siren adds, hold-to-aim

- Branch `claude/combat-fx-fixes` from `origin/claude/visual-revision` `dcc7430`; no PR (owner merges).
- Owner reports: impact effects missing on additional projectiles and capping out; weapon effects and
  life steal missing on Siren minions; plus "remove the aim-circle … show it if we hold the button".
- Root causes: projectile hits on adds emitted no `impact` event and applied no DoTs (adds had no status
  state at all); chip extras (Backfire/Crossfire/Helix), and Orbitals were stripped of
  burn/poison and never life-stole; Splitter never split on adds; add hit events above the 20/s per-attacker
  bucket were silently dropped; the 180-spark impact pool dropped new impacts when full; melee/thrown add
  hits and flame lance / water slows skipped adds.
- Changes: shared on-hit for adds (`Simulation.minionWeaponHit/meleeHitMinion/addProcs`): DoTs, life steal,
  Static/Vacuum/Shrapnel per direct attack, impact event per projectile, Splitter on adds. `weaponFx`
  secondary shots keep the weapon DoTs + life steal (players and adds) but no chip procs. Horde: burn/poison
  DoTs (elements.ts rules, credited to the source), slows (Tidal Wave, Shackles, Soak, Geyser Wall) and
  enemy frost/time fields slow adds; over-bucket hits coalesce (flushed on a token, after 0.25 s or on death);
  `PveAddView.st` status bits in flags bits 26-28 (tuple length unchanged); flame/bubble/droplet status rigs
  on the nearest 16 adds. Spark pool recycles oldest. Hold-to-aim: Frag, Meteor, Geyser Wall, Black Hole cast
  on key release for human pilots (tap = release cast; hold > 0.2 s shows the preview; roll/block/other
  non-ADS ability cancels); bots, rocket and Orbital Mark (a dart, no preview) keep cast-on-press; host
  advertises `caps.aimHold`, old hosts keep cast-on-press and the always-on circle; HUD "READY · HOLD TO AIM".
- PvP impact: with Helix/Crossfire/Backfire/Orbitals, secondary shots now burn/poison and life-steal (Splitter
  fragments still never reapply enchantments, per the existing pinned rule);
  aimed E abilities cast on release. pvp-golden unchanged. Hit timing, hitboxes, damage values, cooldowns and
  snapshot sizes unchanged; add movement is slower while slowed.
- Checks (Linux, Node 22.22.2): `npm run check:types` passes; full unit suite 1152 tests, failures equal the
  13 known Node 22 baseline names after fixing one regression found by it (Splitter fragments re-enchanting,
  reverted); new `tests/combat-fx.test.ts` (projectile counts x impacts/heals/coalesced add hits for pistol/MG
  multishot, rifle buckshot, water torrent; DoTs/slows on adds per path) and `tests/aim-hold.test.ts` (tap,
  hold-release, block/roll cancel at 30/60/120 Hz, latest-input and relay-batch delivery; preview gating);
  pvp-golden identical; `node tests/run-network.mjs pve` passes (victory, rejoin, old client refused). Visual
  lab `tools/visual-lab/vfx.html?fx=multishot|buckshot` (Playwright/SwiftShader): sparks on every multishot hit.
- Not run: native Windows/GPU, online multiplayer by hand, packaging; no test-channel publication from this
  task branch (shared branch publication after approval/merge).
- Risks/next: owner playtest of hold-to-aim feel (release latency ≈ one input frame) and add status visuals;
  Orbital Mark kept press-cast (existing test pins it as a dart without ground preview).
  Status: implemented on branch; not merged; not released.

## 2026-10-04 — Claude — kill-cam aftermath: keep watching ~4 s past the kill

- Branch `claude/killcam-aftermath` from `claude/visual-revision` `dcc7430`. No PR; not merged; not published.
- Owner: "move forward a bit, show about 4 seconds after kill" (clarified: keep watching ~4 s after). Window was 3 s
  before / .6 s after the fatal hit (3.6 s clip, starting ~1.2 s after death); now 2 s before / 4 s after (6 s clip),
  still starting after a 1.2 s live hold on the death (`KILLCAM.hold`). The aftermath is no longer waited for: later
  history frames stream into the clip while it plays (playback lags live by 3.2 s); a stalled feed ends the clip .5 s
  after its last frame. History 6 s / 132 frames. Camera: killer's shoulder view, cutting once to the victim's body
  if the killer is gone, dead or >24 m away during the aftermath. Skip/respawn/epoch/lobby rules unchanged.
- Files: `src/client/killcam.ts`, HUD comment, `tests/killcam.test.ts` (timing pins, aftermath + body-cut tests),
  `tests/killcam-network.ts` (replay frame past the kill). No simulation, hit timing, damage, networking or renderer change.
- Checks: `check:types` ok; killcam + presentation 16/16; `tests/killcam-network.ts` ok (3 clients, 87 snapshots,
  aftermath); full unit suite 1110 pass / 13 fail = known Node 22 failures list exactly.
- Risk / next: a round-ending kill enters `result` for 5 s (`resultSeconds`), then countdown/draft cancels the replay,
  so 1v1 and last-kill replays show only ~1.8 s of aftermath. Showing the full 4 s there needs `resultSeconds` ≈ 8
  (gameplay pacing; owner decision, not changed). No Windows/visual playtest of the new framing.

## 2026-10-04 — Claude — ranged/magic Q+E ability overhaul integration (+ PvE downed, chip throw, update headroom)

- Branch `claude/abil-integration` from foundation `claude/abil-foundation`; merges abil-guns, abil-magic, abil-chips,
  abil-presentation, abil-vfx, then `claude/pve-downed` `bf23737`, `claude/pve-chip-throw` `a130059` and
  `claude/update-headroom` `f491e72` (all clean merges). Base of the streams: `claude/visual-revision` `cfdce4b`.
  No PR opened yet; not merged; not published.
- Ability overhaul (design in the orchestrator plan; player tables and balance in `docs/ABILITIES.md`): guns get Q = ADS
  (rifle 3x scope) and E = Frag / Underbarrel Rocket / Orbital Mark; magic gets faster, flatter primaries (Soak, enemy
  chain, 20 % drain), a Q spell and a strong placed/travelling E per school; legacy push/pull/nova/fireball E removed per
  action id (L33T root, melee specials, Necronomicon kept); 16 magic chips; Rally Pong v2 for 3D; first-person staff
  fix; HUD cooldown row; chip VFX sweep; PvE life steal on every direct-hit path. Mixed builds: the host advertises an
  ability flag; older clients keep the legacy controls.
- Integration retune (`tests/duel-balance.ts`, `npm run test:duel-balance`, n=20 per pairing): magic vs guns (open)
  fire 21→55 %, water 3→61 %, darkness 5→55 %, lightning 58→87 %; BOT_LEAD stays [0,0,0] (any lead gave 80-100 % for
  the leading side) and now applies to magic bots only; lightning 15/.28 s/55 m/s/gravity 20 (was 15/.27/85/30);
  frag 60→66; pistol/MG bots ADS from 25 m.
- PvE downed (`bf23737`): downed members keep their body, HELP bubble, holographic regeneration on revive (presentation
  only; `PvePartyView.upAt`). Chip throw (`a130059`): chips land 2.0-2.7 m in front of the machines (pop .55 s, hop
  1.1 m, still inside the .6 s dispense delay). Update headroom (`f491e72`): test-channel schema 1 up to 32 MiB,
  updater v2 (schema 2), launcher self-update/restore; live profile output unchanged.
- Astra/Codex-history files touched: src/shared/simulation.ts, content.ts, config.ts, chaos.ts, arcade.ts, emperor.ts,
  standard-melee.ts, host-room.ts, relay-protocol.ts, src/server/index.ts, src/client/main.ts, renderer.ts,
  prediction.ts, snapshots.ts, audio.ts, character-animation.ts, control-hints.ts, match-hud.css and other client
  presentation files, desktop/main.cjs, desktop/update.cjs, launcher/*, scripts/package-update.mjs,
  package-desktop.py, repackage-desktop.py, docs/DEVELOPMENT.md, tests chaos/modifiers/rarity-combat/relay/
  rig-arena-sniper/first-block.
- Animation/combat impacts: hit timing, damage, cooldowns and hitboxes changed deliberately for ranged/magic (new Q/E
  windups, lockouts, fuses, blast radii, orb radii, primary numbers; magic E no longer shares the primary cooldown or
  reload). Movement: ADS walk multipliers, slows (Soak/Shackles/Geyser, floor .4, immunity), pulls, knock-ups, Blink
  teleport; a roll cancels an ability windup. Networking: Q input bit, optional snapshot fields (omitted when default),
  host capability flag in welcome; predictor mirrors ADS and field pulls. Melee, DEV classes, PvP death presentation:
  unchanged. Downed/chip-throw: presentation and chip landing position only.
- Checks (Linux, Node 22.22.2): `npm run check:types` pass; full unit suite 1122 tests, 14 failures = the 13 known
  Node 22 names + `dev-bots` "ability bots use Q" (start distance below the new 25 m ADS range; test fixed, the file
  then passed 10/10; full suite not re-run). Intentionally changed tests: pvp-golden (4 traces regenerated once with a
  header; melee-dev and siren-melee identical), gun-abilities (frag 66, ADS 25 m), magic-abilities (lightning row),
  dev-bots (Q range), abilities-foundation (kits). `node tests/run-network.mjs pve` victory; `chaos` fails only on the
  known cosmetics assertion; `tests/pve-loopback-network.ts` pass; `npm run test:pve-perf` incl. new abilities scenario
  (avg step .93 ms, p99 2.16 ms, snapshot p95 54,945 B, no ability/explosion events dropped). `npm run build` (live):
  payload 26,135,216 B (limit 26,214,400; 79,184 B headroom); test-channel profile: schema 1 26,135,216 B, schema 2
  22,064,822 B. Playwright real-app sandbox smoke (SwiftShader): fire Q Lance + E Meteor, pistol ADS + frag, no console
  errors.
- Not run: Windows/Electron, GPU, online multiplayer with people, real updater on Windows, release:prepare x2,
  verify:standard, test:network:local, test:https, test:stress, pve-balance, duel gate at n=200, full visual capture set.
- Risks: lightning still wins 62-95 % of open bot duels; water beats rifle/darkness in the open; melee bots lose
  ~100 % to ranged/magic as before; payload headroom is 79 KB under the live 25 MiB guard.
- Pending publication record from the previous integration: PR #32 merged `cfdce4b` -> test `0.2.5036b50f0c34`
  (payload `arena-164f10d7096c7469`, 26,045,324 B, channel `70fe238`). CI first run hit the flaky party-relay 403 (not
  touched by the PR; passed 5/5 locally), rerun passed; otherwise baseline-only. Updater verified.
- Next: orchestrator review, PR into `claude/visual-revision`, merge, test-channel publication; owner playtest of feel
  and balance.

## 2026-10-03 — Claude — update headroom review round 2: short writes, journalled restore, current-check

- Branch `claude/update-headroom` (base `cfdce4b`), implementation `f951fbc`; tooling PR #5
  `claude/test-update-channel` `2519377`. Not merged, not published, no GitHub Release.
- `launcher/update.mjs`: the schema-2 download and unpack loop until every byte is written and
  hash the bytes written (tested with a FileHandle that writes a few hundred bytes per call, and
  one that writes nothing). `windowsSafe` also refuses `CONIN$`, `CONOUT$`, `CLOCK$`, `COM0`,
  `LPT0` and device names followed by spaces.
- A launcher restore (`desktop/update.cjs` and `update.mjs`) writes `runtime/launcher-restoring.json`
  first and replaces files by copy-and-rename. The desktop finishes an interrupted restore before
  the next Update or Create Room, and `update.mjs` before its next start. Nothing is proven while
  the marker exists.
- Blind spot "reports current but never checks": `current` still never proves. While Update is
  unproven, a `current` result is compared with the badge's update check (`feedVersion` in
  `desktop/main.cjs`; in a TEST-ONLY seed, the channel handler). A different feed version counts as
  a failed run and restores the launcher. `installed` proves only when `release.json` has the
  reported version. Residual gap (documented): the check is offline or unavailable, or the feed
  equals the installed version.
- Packager: the live profile refuses `CHIP_UPDATE_SCHEMA2`, `CHIP_UPDATE_SCHEMA2_MAX_MIB` and
  `CHIP_UPDATE_LAUNCHER_OVERLAY` before it builds; live output is unchanged (same values).
- Seed privacy (tooling): the seed no longer contains `chip-arena-source.zip`. It is
  distributed only as an owner-only GitHub Release asset on this private repo, tag
  `test-seed-<content version>`.
- Checks: candidate `f951fbc` + tooling `2519377` on Node 24.19.0 passed setup, fixture 10/10,
  updater tests 28/28, runtime pins, build, repeat build (byte-identical feed), actual payload,
  Windows/desktop packages, seed ZIP, `source-privacy-zip`, `source-privacy-feed` and
  `channel-e2e` 33/33. Content `0.2.5036b50f0c34` (game unchanged): schema 1 26,045,324 bytes,
  identical to the published payload, and schema 2 22,028,495 bytes. Seed ZIP: 284,149,423 bytes
  (was 333,669,480), SHA-256
  `95d34fb255d5f61db3ad5e1bfbe661a470899a8195bf093afd630602c93c4742`. That ZIP was installed and
  its `start.mjs` served `/`, the JS bundle, music and `/health`; `/chip-arena-source.zip` is a 404.
  `verify` failed only on the baseline: the same 12 Node 24 unit names as round 1, server tsc
  errors, the websocket rematch timeout, and the HTTPS tick-0 flake. Node 22 full suite: 980 tests,
  13 failures, exactly the known list. A first run during the candidate build also failed
  `network-bandwidth` ping queue under load; that file passed twice on its own and the idle rerun
  was clean. Node 24 updater and desktop tests: 46/46. New tests fail on the round-1 code (7/21).
  `tsc` passes.
- Not run: native Windows (rename/locking, Electron `session.fetch` through the badge handler,
  real Create Room), real GitHub hosting, the seed release itself. Next: owner/orchestrator
  review; merge; publish the feed and the private seed release from one candidate of the merged
  commit.

## 2026-10-03 — Claude — update headroom review round 1: launcher probation in stable desktop code

- Branch `claude/update-headroom` (base `cfdce4b`), implementation `2db36fa`; tooling PR #5
  `claude/test-update-channel` `ebd998f`. Not merged, not published, no GitHub Release.
- Blocking finding confirmed and fixed: a new launcher that loaded and spoke the protocol but then
  failed (for example a broken `remote-feed.mjs` mapping, or an `update.mjs` whose `applyUpdates`
  throws) was never rolled back, and an Update could delete the backup of a `start.mjs` that had
  never run. Now `desktop/update.cjs` (seed-only, never self-updated) owns probation. A new
  launcher must work once in a later run of each kind it takes part in. For Update, that is an
  Update that installs a version, for `update.mjs`, `manual-update.mjs`, `remote-feed.mjs` and
  `local-feed.mjs`. For hosting, that is a Create Room that reaches `YOUR GAME:`
  (`desktop/main.cjs`), for `start.mjs` and `update.mjs`. Until then, any failed run of that kind
  restores the previous launcher and drops the staged copy. `update.mjs` no longer confirms
  itself. A launcher activated while another is unproven keeps the original backup.
- Non-blocking fixes: v2 refuses the hold notice (`hold:true`) instead of downgrading; a schema-1
  install drops a launcher staged by schema 2; unpack, syntax-check and install phases log progress
  (90 s desktop silence timeout); schema 2 and the packager refuse Windows device names and trailing
  dots or spaces; live `CHIP_UPDATE_SCHEMA1/2_MAX_MIB` can only lower 25 MiB; the test-channel overlay
  is checked before any file is written.
- Not covered (documented in DEVELOPMENT.md): failures after `YOUR GAME:` (online tunnel), a bug
  that appears only after the new launcher installed one update, non-desktop entry points. A
  rolled-back launcher is staged again by the next Update until a fixed version is published
  (each such Update re-downloads the payload, about 22 MB today).
- Checks: candidate `2db36fa` + tooling `ebd998f` on Node 24.19.0. Passed: setup, fixture 9/9,
  updater tests, runtime pins, build, repeat build (all four feed files byte-identical), both
  payloads, Windows/desktop packages, seed ZIP, and the new in-candidate `channel-e2e` 30/30.
  Schema 1 is 26,045,324 bytes, identical to the published `arena-164f10d7096c7469.json.gz`.
  Schema 2 is 22,026,471 bytes. The seed ZIP is 333,669,480 bytes, SHA-256 `73376dff…a876c7`.
  `verify` failed on baseline items: 12 unit names, 4 server tsc errors and the websocket rematch
  timeout. It also failed HTTPS once with a tick-0 snapshot mismatch; the game source equals
  `cfdce4b`, and `tests/https-network.ts` then passed twice on its own, so this is treated as flaky
  under load. Full `channel-e2e` with the 0e82a31 seed (what testers hold) as the old install:
  38/38. Live packager output is byte-identical to the `cfdce4b` packager on the same build.
  Node 22 full suite: 976 tests, 13 failures, exactly the known Node 22 list. Node 24 updater,
  desktop and manual-updater tests: 27/27. tsc passes.
- Not run: native Windows (rename/locking, Electron, real Create Room), real GitHub hosting, the
  seed release. Next: owner/orchestrator review; merge; publish the feed and the seed from one
  candidate of the merged commit.

## 2026-10-03 — Claude — update-size headroom: 32 MiB test guard, updater v2, launcher self-update

- Owner approved on 2026-10-03 ("do what you need") after asking why updates are limited to 25 MiB.
  Branch `claude/update-headroom` from `cfdce4b` (shared `claude/visual-revision`). Implementation
  `299a742` + `f192798`; this entry and a test-only whitelist check follow. Tooling: PR #5
  `claude/test-update-channel` `7c7ded2`, `2a17b17`, `b5ff54c`. Not merged, not published.
- A. `scripts/package-update.mjs` profiles (`scripts/update-format.mjs`). `live` (default) keeps the
  25 MiB Site guard, and its output is byte-identical to the old packager (checked). `test-channel`
  (set by `candidate.py --channel`) allows schema 1 up to the 32 MiB cap of every installed launcher,
  holds schema 1 back when it no longer fits, and always adds schema 2.
- B. Schema 2 is Brotli of `CHIPPACK` + index + raw files (no base64). `launcher/update.mjs` reads
  `latest-v2.json` only when the caller opts in. Only a 404 falls back to `latest.json`. The download
  is streamed with a 256 MiB cap, a byte count and SHA-256, then unpacked file by file under the same
  `allowedPath` rule plus a launcher whitelist. Launcher files are syntax-checked, staged and applied
  at the next start, with journal and backup. (Rollback in this first version covered only a crash,
  an `update.mjs` that cannot load and a silent entry-point death; review round 1 below corrects
  this.) The desktop timeout now counts 90 s of silence (45 min maximum).
  Settings, saves, `runtime/` and `test-channel.json` are never targets. Schema 1 is unchanged.
- Current build (`f192798`, content `0.2.5036b50f0c34`, unchanged game): schema 1 is 26,045,324 bytes,
  byte-identical to the published payload. Schema 2 is 22,025,342 bytes (-15.4%, Brotli q11). Seed
  ZIP `Chip-Arena-TEST-ONLY.zip` is 333,652,190 bytes, SHA-256 `413ea6e9…a5125`.
- Checks (Node 24.19.0 candidate, tooling `2a17b17`): verify matched the baseline only (12 unit names,
  4 server tsc errors, rematch timeout; HTTPS passed). These passed: updater tests 20/20 (including
  the new `tests/update-v2.test.ts`), fixture tests 9/9, repeat build of all four feed files
  byte-identical, `check-payload` on both feeds, Windows/desktop packaging and the seed ZIP launcher
  check. `channel-e2e.mjs` passed 27/27 against a local HTTPS feed using the real code of the
  published `cfdce4b` ZIP and the new ZIP. Old installs: schema 1 at 30.9 MiB installs, 33.9 MiB
  is refused with the install kept, and the hold notice shows "Update Available!" and installs
  `…-download-the-new-TEST-ZIP-from-GitHub-Releases`. New installs: v2 is preferred, a 63.7 MiB
  payload installs, launcher staging then activation then confirmation works, broken-entry and
  incompatible-API rollbacks work, and these are refused: traversal, hash mismatch, oversize,
  longer body, dropped connection. Same-version skip and settings/session preservation pass.
  tsc passes. Node 22: full suite has 972 tests with 13 failures, the known Node 22 set; updater-v2 tests 13/13.
- Not run: native Windows (rename/locking, Electron), real GitHub raw/Release hosting, the
  GitHub Release seed itself (procedure in tooling `docs/TEST-CHANNEL-README.md`).
- Risks / next: testers need one new seed ZIP for v2 (old installs keep schema 1 up to 32 MiB).
  `resources/app` (Electron shell, badge handler) does not self-update. Launcher rollback: see the
  review round 1 entry. Channel Git history grows about
  48 MB per publication. Next: the owner or orchestrator reviews, merges into the shared branch,
  publishes the feed and the GitHub Release seed.

## 2026-10-03 — Claude — test publications of PR #30 and PR #31

- Owner asked to merge and publish both boss-test follow-ups for the Update button.
- PR #30 (desync after Esc/pause, Esc → pause menu, taken chips wait) merged at `6fd525c`; content `0.2.85b68726b813`,
  payload `arena-71bb7881594044cd.json.gz`, channel commit `7a53f4e`.
- PR #31 (29 damage/health chips, 6 boss-only money chips, green/red stat lines, ~2.5x money, instant F revive, camera
  filter, PvE Tab board, owner stage track re-encoded to 128 kbps) merged at `e9edad4`; content `0.2.9a0466bf45fe`, payload
  `arena-cd9fa8ddbe4b964c.json.gz` (26,021,943 bytes, under the 25 MiB hosting limit), channel commit `8f6f5a3`.
- Both: merge trees identical to the tested heads; CI and candidate `verify` matched the baseline only (12 Node 24 unit
  names, 4 server tsc errors, rematch timeout; HTTPS passed); repeat builds byte-identical; actual-payload checks passed;
  no secrets; the real launcher updater upgraded an isolated install over the network and then skipped.
- Not run: native Windows Update button, GPU visuals. Live feed and Site unchanged.

## 2026-10-03 — Claude — legendary / mythic chip rework and volumetric chip blasts

- Branch `claude/pve-legendary-vfx` from `claude/pve-batch2` `9b5445c`; no PR. Owner request: rework broken/weak
  Legendary+ chips so they feel exciting, and replace flat on-impact/area visuals with real voxel explosions.
- Audit of all 26 legendary + 13 mythic chips (code read, unit probes, seat-swapped bot duels); table in the session
  scratchpad (`after/legendary-vfx/audit.md`). Broken: Trading Places never swapped grounded pilots (room check hit
  the floor); blinks (Pocket Portal, Emergency Exit) cast from floor contact stopped at 0 m; Meteor Shower meteors
  were deleted by the 3-per-owner field cap; bots aimed gravity-free shots (boomerang/Pong) with gravity.
- Reworked (ids, rarities and the 7-mythic melee pool kept): Satellite Office (homing satellite shots), Red Light
  Green Light (freeze, then relaunch at the current aim ×2 speed ×1.5 damage), Return to Sender (return leg re-hits
  ×1.4), Comet Dash (landing blast follows the roll, low gravity removed), Tumble Engine (no penalty, i-frames),
  Thunder Step (locks lightning on up to 2 enemies), Now You See Me (blink to target, ×1.5, 0.25 s untouchable),
  Emergency Exit (cheats a lethal hit, +40 HP, 1.2 s immunity), Time Out (Block freezes nearby enemies 1.2 s),
  Supernova (no Block penalty, nova Blocks recharge in 0.6 s), Trading Places (fixed, 0.5 s daze), Meteor Shower
  (5 tracked meteors, 1.5 s cd), Pocket Portal (arrival blast), Do Not Press (+30% HP, 2 s immunity); Hurricane,
  Vampire, Repulsor damage penalties softened. Tuning in `CHAOS_TUNING` (chaos.ts); card stat lines on the cards.
- VFX: new pooled `src/client/chip-blasts.ts` (two InstancedMeshes, fixed capacity, 3 zero-intensity lights):
  fireball + shock shell/ring that ends exactly on the event radius + debris + smoke + flash; shock bolts, implosion,
  void, frost, toxic, quake, nova styles; live-field emitters (storm bolts from a cloud, flames, acid, frost shards,
  accretion spirals, falling meteors, comet trail). Time/repulsion fields get a dome. Keyed to authoritative events:
  explosion events now carry `message` (chip tag) except plain weapon `blast`; fields carry `fx`. Quality/compat
  respected. Dev-only `tools/visual-lab/vfx.html` renders the real Renderer on a real Simulation.
- Tests: new `tests/legendary-rework.test.ts` (15) and `tests/chip-blasts.test.ts` (6). Intended-behaviour test
  updates: chaos.test (Satellite/Stop-Go/Exit/Panic numbers), first-block (Time Out freezes the shooter; thawed for
  the reset check), pve-economy (clear the Exit immunity before scripted kills).
- Checks (Node 22.22.2): check:types pass; full suite 954 tests, 13 fail = the known Node 22 set; pvp-golden 5/5
  unchanged (no golden trace uses a reworked chip). Not run: Windows, real-app play, network suites, perf on GPU.
- Risks: duel numbers are bot-based (bots rarely Block/roll); Thunder Step/Comet/Supernova may need owner tuning;
  Static Discharge (rare) showed no arcs in bot duels (not investigated). Status: implemented on branch, not merged.
## 2026-10-03 — Claude — menus: Boss Test skull pill, simpler online room

- Branch `claude/pve-menus` from `claude/pve-batch2` 9b5445c. Owner asks: "glossy black pill
  button with some skull design, place it on the right" and "Simplify Online Room Options
  (… see 'Ready' easier) … details under an 'Advanced Options' option".
- Home: Test Boss leaves the main list and becomes `bossPill()` (pve/menu.ts, pve.css): glossy
  black pill, inline bone-skull SVG with lavender eyes, sheen on hover, visible focus ring; on
  the right of the menu (moves under the menu at <=860px, full width on phones).
- Room lobby (PvP main.ts, PvE pveLobbyBody): big "n/m READY" meter, green READY / amber NOT
  READY badges and green ready cards, a larger Ready button with a "Waiting for n pilots" line,
  room code + copy buttons at the top of the side column. Mode + arena stay visible; sets,
  rounds, friendly fire, random map, bots/difficulty, bot drafts, host-only bot loadouts,
  draft summary, invite link and connection report move into a collapsed "Advanced Options"
  fold (summary line shows sets/rounds/bots) remembered in localStorage
  `chip-arena-room-advanced` (try/catch; room-options.ts). `matchOptions(lobby,part)` splits
  the same pieces; practice/create keep the full block. PvE party roster uses the room's
  density columns. Short viewports shrink lobby portraits so names clear the Ready bar.
- No setting semantics, element ids, host gating or network messages changed.
- Checks: check:types clean; full suite 925/938, failures = the 13 known Node 22 names;
  pvp-golden 5/5; new tests/room-options.test.ts; pve-ui pins updated for the pill (intended).
  Playwright with the local backend, host + guest, PvP and PvE online rooms at 1920x1080,
  1280x720 and 390x844, plus home at 820/390 widths. Not run: Windows desktop, packaging.
- Risks: in the headless runs a PvE online guest sometimes dropped to DISCONNECTED under heavy
  CPU load (seen on base 9b5445c too); the phone lobby still has pre-existing class-row clutter.
- Status: implemented on branch; not merged; not published.

## 2026-10-03 — Claude — PvE stage music (Restricted Zone Pulse) + batch 2 verification

- Branch `claude/pve-batch2` (from `948b5ba` = `claude/visual-revision` `6fd525c` + econ-revive, chips, camera,
  tab-board merges); no PR.
- Owner: "lets replace the music on the stage to the one attached- until Siren spawns of course :)". Added the owner's
  `public/music/restricted-zone-pulse.mp3` (byte-exact, 4,113,904 B). In a PvE run on Ward Zero the client loops it from
  the first in-game phase (specialization, intros, countdown, waves, intermissions, reveal) until `bossSong` is set at the
  Siren's intro tick, where Black Siren Protocol takes over unchanged. Lobby keeps the menu music; 35% under intro
  stings; rewinds for Retry/new runs; music volume/mute apply; load error falls back to the rotation. Not in
  `MUSIC_TRACKS`, so PvP rotation is unchanged. PvE pre-decodes the boss song so it starts at offset 0 on the intro.
  Files: `src/client/{music,audio,boss-theme,main}.ts`, new `tests/pve-stage-music.test.ts`, docs PVE/ASSET-CREDITS.
- Animation/hit timing/hitboxes/damage/cooldowns/movement/networking: unchanged (client audio only; no wire change).
- Checks (Node 22.22.2): check:types pass; full suite 933 tests, 920 pass, 13 fail = the known set exactly;
  pvp-golden 5/5; `run-network.mjs pve` victory; `pve-loopback-network.ts` pass; `test:pve-balance` 60 runs, 22/60
  victories (S7 table: 13/60), money at boss h=1 pistol 619 / rifle 317. Real app (Vite, offline TEST BOSS, SwiftShader):
  stage track playing through specialization/intro/countdown/fight; paused and rewound the tick the Siren song started
  (boss buffer started once, offset 0); Tab board, chip screen green/red stat lines, one F tap revived a party bot.
- Size: the track is 2:53 (not 8.5 min; the header claimed 64 kbps but it is ~190 kbps VBR). Re-encoded to 128 kbps CBR without
  cover art (2.77 MB); `npm run build` passes with a 26,022,078-byte (24.8 MiB) payload, under the 25 MiB hosting limit.
- Status: implemented on branch; not merged; not released.

## 2026-10-03 — Claude — PvE playtest fixes: more money, instant revive

- Branch `claude/pve-econ-revive` from `213f62b` (PR #30 head); no PR (integrator merges).
- Revive: the hold-F channel cancelled one round trip after it began because the client's
  `pveIdle()` idled all input (interact too) while the party row showed a channel. Revive is
  now instant: one F press near a downed teammate charges and revives (50% HP, shield); deny
  'money' otherwise. Channel state/UI removed; sub-frame F taps are latched in `main.ts`.
  E is the special ability (`secondary`), not interact.
- Money (`balance.ts`): income about 2.5x (rewards 25/20/80, head 10, start 150, wave clear
  100+50w, thresholds 250, boss $10 per 1%, possessed 300/100); prices about 1.2x (chip
  175+25 max 350, recruit 300, heal 75, revive 150+50 max 400). Harness, solo pistol median:
  earned at boss 1512 -> 3406, bought 2 recruits + 4 chips -> 2 + 8. Table in docs/PVE.md.
- Animation/hit timing/hitboxes/damage/cooldowns/movement/networking: unchanged (wire fields
  unchanged; `channel` is simply never set). PvP untouched; pvp-golden passes.
- Checks (Node 22.22.2): check:types pass; full suite 913 tests, 900 pass, 13 fail = the known
  set; `node tests/run-network.mjs pve` victory; balance harness h=1,2 pistol/rifle seeds 1-5
  before/after; real app (Vite, offline TEST BOSS, SwiftShader ~20 fps): one F tap revived a
  downed party bot and charged $150; holding F 3 s revived once; too poor = no revive.
- Not run: Windows build, hosted multi-browser play. Risk: owner judgement of the new numbers.
  Status: implemented on branch; not merged; not released.
## 2026-10-03 — Claude — more damage/health chips, green/red chip stat lines, PvE money chips

- Branch `claude/pve-chips` from `213f62b` (PR #30 head, `claude/pve-fix-desync-holdchip`); no PR opened.
- Owner request: more damage and health chips; chip cards show the description first, then
  positives in green and negatives in red (for example "+100% Health"); money-gain chips in PvE
  (boss fight only).
- 29 stat chips (`src/shared/power-chips.ts`, added to `CHIPS`): 15 damage (flat, per class,
  close/far range, finisher, crit/backstab, glass-cannon trade-offs) and 14 health (flat/percent
  max health, regeneration, life steal, damage reduction, tank trade-offs), common to legendary.
  None are mythic, so the rainbow melee pool test keeps its size. All run through `statsFor`.
- `src/shared/chip-stats.ts` builds per-copy stat lines from chip effects (old chips get them too),
  or uses explicit `Chip.stats`. `chipCard` renders `<ul class="chip-stats">` after the description,
  and the aria-label adds the lines with "(penalty)". Board tooltips show the same lines.
- PvE money chips (`src/shared/pve/money-chips.ts`, 6 chips from common to legendary) are not
  in `CHIPS`, so PvP never sees them. For human pilots, an ordinary (non-rainbow) PvE pick has a 30%
  chance to swap its last offer for one (`withMoneyOffer`, seeded economy stream). `Economy.grant`
  scales earnings through `moneyWithChips` (one line; refunds are unchanged). Party bots skip them.
- `tests/pve-foundation.test.ts`: the offerChips base-hash check now runs on the base catalogue
  (new chips removed temporarily), so it still proves the default path is unchanged.
  New `tests/power-chips.test.ts` covers this work. New Lucide icons were generated via `icons:generate`.
- Checks (Node 22.22.2): `npm run check:types` passes; full suite 905 pass / 13 fail, which is exactly the
  known Node 22 failure set; pvp-golden unchanged (PvP traces draft no chips). Playwright shots: draft 1600x900 and 1280x720, PvE start screen,
  money and mixed offer screens (session scratchpad, after/pve-chips).
- Risks: PvP draft pools grew, so offer odds per existing chip are lower. Balance numbers have
  not been playtested. Picks now draw extra economy RNG, which shifts seeded PvE runs.
  The draw comes after the catalogue offers.
- Status: implemented on branch; not merged; not released.
## 2026-10-03 — Claude — camera snaps ("pulls another direction randomly and instantly")

- Branch `claude/pve-camera` from `claude/pve-fix-desync-holdchip` `213f62b`; no PR (orchestrator). Not merged, not released.
- Finding: the live camera's yaw/pitch are purely client-local (only pointer-lock `mousemove` changes them in play; the
  server, prediction, holds, revives and teleports never write them; cutscene/spectator/killcam are separate cameras).
  So a sudden turn in play can only come from a mouse delta. Chromium pointer lock (Windows/Electron) is known to
  report bogus single huge `movementX/Y` on the first move after a lock is (re)acquired (Esc→Resume, chip screen,
  board, alt-tab) and as rare lone spikes; main.ts applied every delta unfiltered. The Windows trigger itself was not
  reproducible here (Linux headless). Position prediction was also checked in scripted offline boss runs (solo and
  3 party bots): no snaps, corrections ≤0.44 m eased.
- Fix (client input sanitising, all modes, no feel change in normal play): `src/client/mouse-look.ts` `MouseLookFilter`
  drops >48 px deltas on the first event / first 120 ms after a lock, and a lone >260 px event that is also >5× what
  the recent hand speed predicts (a same-direction follow-up confirms a real flick). Wired in `main.ts` mousemove and
  `pointerlockchange`. No hit timing, hitboxes, damage, cooldowns, movement or networking changes.
- Checks: `tests/mouse-look.test.ts` (new, 7 cases); real app (Playwright, synthetic locked mousemoves): fresh-lock
  300 px jump and a 900 px spike ignored, 10 px tracking and a 1620 px ramped flick applied exactly; check:types,
  pvp-golden and the full unit suite (failures = the known Node 22 set). Not run: Windows/Electron, real mouse.
- Follow-up (owner: "exiting chip selection ... seems to make us look upward"): consistent, same mechanism. Closing
  the chip screen re-locks; Chromium warps the cursor from the click (Choose later / cards sit below the centre) to the
  lock centre, read as a move up. Chromium sets `pointerLockElement` before `pointerlockchange`, so the warp could reach
  the filter before it knew of the new lock. The filter now starts a fresh lock at the first locked event after an
  unlocked spell (`track()`), settle window 250 ms. Nothing resets or changes pitch while the screen is open. Real app with
  emulated lock + warp (0,−180 px): base `213f62b` pitched up 0.32 rad per close (to the 1.1 clamp); `07f91fd` still
  missed the Esc close; now 0 on Choose later click, Esc and card pick; aiming after re-lock exact.
- Next: owner playtest on Windows; if snaps persist, consider `requestPointerLock({unadjustedMovement:true})` (raw input;
  removes OS acceleration, so a feel change to decide).
## 2026-10-03 — Claude — PvE Tab party board (no rounds/sets; kills, deaths, money)

- Branch `claude/pve-tab-board` from `claude/visual-revision` `6fd525c`; no PR (orchestrator). Not merged, not released.
- Owner feedback: the Tab board in PvE still counted rounds/sets. In a boss run (`snapshot.pve`) `PlayerBoard` now renders
  `src/client/pve/board.ts`: stage heading (wave n/3 + adds left, boss phase n/4 + health, intermission, victory/defeat),
  run clock, party totals, one scoreboard row per party member (humans and party bots; recruits nested with HP) with
  status/HP, kills (+heads), deaths, assists, revives, add and boss damage, money now and earned, weapon and chips (same
  hover inspection). Live cells swap in place so chip hover survives updates. HUD nav button reads Party in PvE.
- Authority: all stats were already live in the party rows; only the run start was missing, so the director view gains
  optional `startedAt` (PvE-only field; PvP snapshots carry no `pve`, pvp-golden unchanged). Fixture base sets it.
- PvP board path unchanged (test pins its markup hash from `6fd525c`). No combat, timing, hitbox, movement or network
  behaviour change.
- Checks: check:types pass; new tests/pve-board.test.ts 5/5; pvp-golden, pve-ui, pve-contract, pve-director, model-ui,
  pve-foundation pass; full suite 904 pass / 13 fail = the known Node 22 list exactly. Playwright (SwiftShader) real
  offline Test Boss Tab board plus fixture wave/boss states at 1920x1080 and 1280x720 inspected. Not run: Windows build,
  network suites, multiplayer playtest.
- Risk/next: at 720p with four members and many chips the board scrolls. Owner playtest of the board.

## 2026-10-03 — Claude — PvE playtest fixes: offline desync after Esc, taken chips wait as pending picks

- Branch `claude/pve-fix-desync-holdchip` from `claude/visual-revision` `58f5d7b`; no PR opened (orchestrator). Not merged, not released.
- Root cause (owner: shots "meters behind", rubberbanding, `pendingInputs` 63 at 7 ms RTT, starting at Esc): while the
  offline boss run was paused the client kept sending one idle input per tick to the paused worker; after Resume the
  host consumed them one per tick behind the live inputs, a permanent ~1 s backlog. Real app (pre-fix): 62 queued inputs
  consumed after a 4 s pause. Scripted 60 Hz run (new test, control case): pending 81–92, median lag 3–3.5 m, p95 ~8 m.
- Fix: `LocalRoom.send` drops fight inputs while paused; `main.ts` syncs the worker pause before stepping. After: pending
  ≤2, p95 predicted-vs-authority 0.000002 m (max ≤0.45 m from contact with bots/boss). PvE only: pointer lock lost while
  playing (browser Esc) opens the game menu and pauses the offline run; Resume / Esc re-lock; menu text says the boss test
  is paused. Taking a dispensed chip never opens the chip screen (pending pick, `C`/CHIPS icon); the client option is
  removed, `prefs` is a server no-op for published clients. No change to hit timing, hitboxes, damage, cooldowns,
  movement rules, snapshot format or PvP.
- Checks (Linux, Node 22.22.2): `check:types` pass; PvE tests + `pvp-golden` 163/163 (+2 new in
  `tests/pve-local-input-sync.test.ts`); full suite 912, failures equal the 13 known Node 22 names; `run-network.mjs pve`
  and `pve-loopback-network.ts` pass; Playwright/SwiftShader app check of Esc → menu → Resume (no inputs posted while paused).
- Not run: Windows desktop, real pointer-lock Esc (headless has none; whether a re-lock without a click is granted is up
  to Chromium), online PvE, packaging. Risk: a worker stall >100 ms still drops sim time (shared PvP clock, unchanged).
- Next: owner playtest on Windows (Esc → menu → Resume, long run with party bots); then merge/publish decision.

## 2026-10-03 — Claude — PvE Siren TEST BOSS merge and test-channel publication

- Owner authorized merge and test publication ("When this is complete, please merge and publish to test").
- PR #28 merged into `claude/visual-revision` at `2bec705ef5aaff6efb890ba8ae848d1175a646f3`; tree identical to the
  tested head `6410e3d`. CI `verify` (Node 24.19) matched the recorded baseline only: units 898/910 with exactly the 12
  names, the four server tsc errors, the WebSocket rematch timeout; HTTPS passed.
- Candidate build at the merge commit (`candidate.py --test-only --channel`, Node 24.19.0/npm 11.9.0): same baseline-only
  `verify`; repeat build byte-identical; actual payload passed (102 files, skip, offline/corrupt refusal, settings
  sentinel, URL restriction); packaging passed. Payload audit vs `0.2.177d472cf940`: only the client/host-worker bundles,
  CSS, `index.html`, `server.mjs` and `release.json` changed; no key-pattern matches outside the embedded Rapier WASM.
- Published content `0.2.65597ffe7165`, build `5ec47234ba96cc77d4eb2b7a`, payload `arena-1a9e8da3344ad6c7.json.gz`
  (23,244,770 bytes, SHA-256 `1a9e8da3344ad6c7a0d6203dcf29d142903ab7988e3c58f68a90b203cb99a546`), channel commit
  `de5cc4d`. Live feed and Site unchanged.
- Real network: the real launcher updater upgraded an isolated install `0.2.177d472cf940` → `0.2.65597ffe7165` on the
  first check; the second check skipped.
- Not run: native Windows Update button, GPU look/fps, real multi-machine play, desktop Host online.
- Status: merged and published to TEST. Next: owner playtest; retune `src/shared/pve/balance.ts` from feedback.

## 2026-10-03 — Claude — PvE Siren TEST BOSS: streams integrated (S7)

- Owner request 2026-10-02 (first PvE mode: Siren TEST BOSS on Ward Zero; spec and design in `docs/PVE.md`).
- Branch `claude/pve-integration` from `claude/pve-foundation` `2484995` (base `claude/visual-revision` `b6d2f58`,
  unchanged at integration time). Merged with `--no-ff`, no conflicts: horde `4210963`, director `0d97649`, support
  `bbb1354`, horde visuals `da32e28`, world visuals `54eb934`, UI `6d62613`. No PR yet; not merged into the shared branch.
- Streams (all on this branch): F foundation (contract, balance, seeded rng, Ward Zero layout, boss chip track, fixtures,
  `PveSystem`/`PveVisuals` facades, gated hooks, golden PvP trace); S1 horde (nav flow fields, vent/door/cell entries,
  swipe/lunge/smash, limbs and gibs, corpses, vent travel, possession, assists); S2 director (opening, 3 waves,
  intermissions, holds, reveal → intro → boss, floors, possession, grow, no cooldown, victory/defeat); S3 support
  (money, chip/recruit/med machines, personal picks, rainbow, revive/respawn/self-revive, recruits, party bots); S4 UI
  (TEST BOSS picker and Host online, HUD money/party/boss bar, personal chip screen with under-attack blink, Siren
  reveal, results, offline worker room, hello/rejoin, debug ops); S5 horde visuals (crowd LOD and impostors, swipe,
  crawl, limp, vent climb/dive, door walk-out, black blood and flame); S6 world visuals (vent lids, door leaves, cell
  gates, machines, pickups, holograms, revive beacons, grown-boss FX scaling).
- S7 changes: cross-stream requests (test seed-era assertions, door `leaf.z` 26.8, vent exit = climb stand point and
  `nearVent` 2.0, contract `pendingHiresOf`, renderer → horde graphics mode, `bossIntroExtra` 1.8 s, fixture revive
  target, `tests/fixtures/pve-horde-60.json`); fixes: stale add body handle after a kill (Rapier panic), flow-field
  seeds for players in the 2 m service corridors (stalled runs), PvE-only snapshot clock catch-up below 20 fps
  (combat numbers froze); new debug op `boss-damage:n`; balance retune (waves 22/28/34 at .5/.6/.7/s, shambler 6,
  runner 4, boss `damageScale` .2-.26, `hpPerExtraMember` .5); new tests `pve-flow`, `pve-integration`, balance
  harness `npm run test:pve-balance`, real-server suite `run-network.mjs pve`, relay/loopback boss extensions.
- Systems touch (Astra's files): the foundation's gated hooks in `simulation.ts`, `chaos.ts`, `siren.ts`,
  `siren-contract.ts`, `siren-motion.ts`, `bot-abilities.ts`, `standard-melee.ts`, `stage-hazards.ts`, `movement.ts`,
  `intros.ts`, `config.ts`, `content.ts`, `host-room.ts`, `server/index.ts`, client `host-worker.ts`, `browser-room.ts`,
  `renderer.ts`, `snapshots.ts`, `overhead-health.ts` (edit map in `docs/PVE.md`). S7 added one line each in
  `renderer.ts` (graphics mode) and `snapshots.ts` (PvE-gated step cap). S1-S6 did not edit `src/shared` outside `pve/`.
- Animation impacts: PvP unchanged (golden trace identical). PvE adds: add attack hit times from `PVE_TIMING`
  (swipe/lunge .34 s, smash .43 s) shared by authority and poses; add hitboxes are capsules (crawlers smaller), limb
  hits by point; add damage per `balance.ts` (retuned); the grown Siren's capsule and reach ×1.5; phase 4 removes boss
  cooldowns (no Last Laugh floor, D35; concurrency caps kept); humans are frozen while their chip screen is open (D18);
  networking adds `snapshot.pve`, `{type:'chip',pve}` commands, hello-gated ready and keyed rejoin.
- Checks (Linux, Node 22.22.2): `check:types` pass; full units 909/896, exactly the 13 baseline failure names;
  `pvp-golden` identical; `pve-flow` (deterministic victory twice, defeat, debug flow; `PVE_LONG=1` pass); network
  suites match baselines (default rematch timeout, chaos cosmetics, block 65.5≠77 known; others pass) plus new `pve`
  pass; siren/killcam/body-damage/https/pve-loopback/pve-relay pass; `test:pve-perf` ok (0.51 ms/step, 49.9 KB p95);
  server build only the 4 known errors; balance table in `docs/PVE.md`; offline real-app smoke (Playwright/SwiftShader)
  36 screenshots through victory, Retry and defeat.
- Not run: native Windows GPU look/fps, real internet multi-machine play, desktop Electron Host online, Site browser
  hosting, hardware audio, hosted two-browser smoke (the client cannot open test rooms, so no debug ops), Node 24.19.
- Risks: balance is harness-tuned (bot humans do not dodge; boss mostly wins against them); melee bots weak in crowds;
  large hordes on low-end GPUs (R1); open chip screen can cover the boss intro if never picked.
- Final independent review (fuzzed 15 seeds × up to 10 min, directed sims, real-app PvE→PvP, network suites): one
  blocking bug fixed — Retry soft-locked in weapon select when a party member had disconnected (`start()` now drops
  disconnected humans in PvE; regression test `tests/pve-retry-disconnect.test.ts`). After the fix: full units 910/897,
  exactly the 13 baseline names; types pass. Known minor: a cinematic hold cancels an in-progress revive channel
  (no charge; press F again). The Siren chip tray (smile, chip card, ??? cards) was confirmed rendering in the app.
- Status: implemented on branch. Next: PR into `claude/visual-revision`, merge and test-channel publication
  (owner-authorized 2026-10-02: "When this is complete, please merge and publish to test").

## 2026-10-02 — Claude — chest-up chip draft merge and test-channel publication

- Owner asked to merge and publish the chip-selection change for the in-game Update button.
- Merge:
  - PR #26 merged into `claude/visual-revision` at `fd2f745b8c84f7ebb72d365f63aeef9daa0cb707`.
  - The merge tree `ef1a268` is identical to the tested PR head `f79072f`.
- Build: `candidate.py --test-only --channel` at the merge commit (tooling `claude/test-update-channel` `fd13d5a`;
  Node 24.19.0, npm 11.9.0).
  - Passed: setup, fixture tests, updater tests, runtime pins, build, repeat build (byte-identical payload),
    actual payload (102 files, same-version skip, offline/corrupt refusal, settings sentinel, URL restriction),
    and Windows/desktop/isolated packaging.
  - `verify` matched the recorded baseline only:
    - units 728/740, exactly the 12 recorded names (portable-update passes on 24.19)
    - the four server tsc errors
    - the WebSocket rematch timeout
    - HTTPS integration passed
- Payload audit against `0.2.58d079f38769`:
  - Only the client JS/CSS bundles, `index.html` and `release.json` changed.
  - The minion module is not in the bundle.
  - The only key-pattern matches are inside the embedded 2 MB Rapier WASM base64, identical in the previous payload.
- Published content `0.2.177d472cf940`, build `d47c040895b0bfc483017fa2`, payload `arena-50e93185d7def3f6.json.gz`
  (22,978,928 bytes, SHA-256 `50e93185d7def3f6f07e1928cc55ad8ff3ef1a9197d70c03d87cbdd5d3f647fc`), at channel commit
  `7a9ff3d`. Live feed and Site unchanged.
- Real network: the real launcher updater in an isolated install upgraded `0.2.19e114ae7c87` → `0.2.177d472cf940`
  from the public channel on the first check, and the second check skipped (up to date).
- Not run: the native Windows Update button, GPU visuals.
- Status: merged and published to TEST. Next: owner playtest of the draft. Siren PvE TEST BOSS mode is in progress
  on a separate task branch based on this merge.

## 2026-10-02 — Claude — chest-up chip draft (ROUNDS-style) and Siren minion design

- Owner request: (a) in the chip draft, zoom up to the picker chest-up, like ROUNDS card picks. Everyone gets the
  same pose except bosses, which keep their own pose but are still framed close. (b) Begin designing Siren minions:
  procedural zombie Pilots (rot, black-flame burns, wounds), friendly to the Siren and its team; spawn logic comes
  later. Owner face decision: blank by default, some with a cracked head leaking black flame, no burned-in eyes.
- Branch `claude/visual-revision-draft-minions` from shared `claude/visual-revision` `433202a`. It merges two task
  branches:
  - `claude/visual-revision-draft-closeup` at `b4626e6`
  - `claude/visual-revision-siren-minions` at `d80388a`
  Each was implemented, adversarially reviewed and fixed in its own worktree.
- Chip draft (`draft-stage.ts`, `draft-view.ts`, `draft-screen.css`):
  - Layout: the cards sit in a tray at the top, with the picker filling the screen below. Framing adapts to window
    size, phone portrait and two card rows.
  - Pose: `draftPose` gives every non-boss Pilot the same calm pose with the weapon hidden. `isBossSpec` (currently
    `siren`) keeps the boss's own animation, framed chest-up.
  - Reach: the arm on the hovered card's side reaches along a curved wrist path. The head and chest turn to the card.
  - Grab: still keyed to the authoritative `chosenAt`, continuing from the hover reach.
  - Turn start: a push-in on each turn (none with reduced motion).
  - Pick moment: the tray height is held across the pick, so cards no longer jump. The no-WebGL fallback centres
    the tray.
  - Draft lab: dev-only `tools/draft-lab`.
  - No markup ids, network, preference or authority change.
- Siren minions (`src/client/siren/minions/**`, `docs/SIREN-MINIONS.md`, lab `kind=minions`): design only. Nothing in
  the game imports the module, so the bundle and gameplay are unaffected.
  - The seed corrupts a standard Pilot rig with rot, black-flame burns, carved wounds, missing parts, dying violet
    glow and weapon scraps.
  - Faces: blank by default; about 25% of seeds are cracked; `eyes` appears only on request.
  - Animation set: shamble, frenzied run, idle twitches, lunge/club/smash, flinch, black-flame-pool emergence, and
    burn-away death. Planted feet hold through turns; the step phase is rate-independent; death rests on the floor.
  - `prewarmSirenMinions(budgetMs)` spreads layout building over frames.
  - The doc proposes (does not implement) a systems contract: a chaos-minion kind `siren-add`, which reuses the
    existing owner/team friendliness, plus spawn rules on `SIREN_ADD_SPAWNS`. Systems files (Astra) are untouched.
- Hit timing, hitboxes, damage, cooldowns, movement rules and network behaviour are unchanged.
- Checks (Linux, Node 22.22.2) on the integration merge `27da229`:
  - `npm run check:types` passed.
  - Full `node --import tsx --test tests/*.test.ts`: 740 tests, 727 pass. The 13 failures are identical to the
    recorded Node 22 baseline (12 names plus portable-update).
  - New `tests/draft-closeup.test.ts` 11/11 (uniform pose, boss exception, framing per aspect, reach/look, 30/60/120 Hz
    agreement, pick keeps the tray still).
  - New `tests/siren-minions.test.ts` 11/11 (determinism, rig contract, budget, face pool, planted feet on turns, floor
    contact, prewarm, no Math.random).
  - Reviewers' Playwright measurements on the real app (Developer Sandbox Preview Draft) at 1920x1080, 1280x720,
    1024x768, 844x390 and 390x844, plus SwiftShader renders of the minion gallery, strips and Ward Zero horde.
- Not run: native Windows/GPU look and frame rate, a real online draft (hover is local, so others see the scan),
  the Siren as the picker in the real app (lab only).
- Risks:
  - The pilot is small with two card rows on 720p.
  - The reach reads as a raised hand beside the head.
  - Minions cost about 50 draws each and about 11k triangles; the doc suggests a cap of about 12 live, with shadows
    off at range.
  - Emergence needs solid floor at the spawn point.
- Next: owner review of the draft feel and the minion gallery. Then spawn/AI/friendliness systems with Astra on the
  doc's proposed contract.

## 2026-10-02 — Claude — Ward Zero rebuilt as a sealed, music-synced hall

- Owner request: revise Siren's stage. It should be a closed-off place (no open world), a big building for a "crazy"
  demon, with better design, still reactive to the music including hazards, and planned for adds spawning with the
  Siren and players running around to grab chips. Branch `claude/sleepy-ritchie-g8ruhv`, based on the shared
  `claude/visual-revision` `a9269ff` (plus the previous publication log).
- Shared layout (`siren-stage-layout.ts`, Claude-owned contract):
  - `SIREN_HALL`: walls reach a 16 m roof, and the roof is a collider.
  - 20 wall columns (`SIREN_COLUMNS`); the four pylons now run to the vault.
  - New data-only hooks: `SIREN_ADD_SPAWNS` (four cells and four ward doors, each with an inward yaw) and
    `SIREN_CHIP_SPOTS` (six pads on the running loop). Nothing spawns from them yet.
  - Cell cabinets moved out of the cells.
  - Partitions, cells, furniture, vents and `sirenStageHazards` are unchanged. 72 props (was 51), 18 breakable.
- Systems touch (Astra's file, coordinate here): `src/shared/siren.ts` `ground()` clamps its ray origin under the
  roof on map `siren`, so effects aimed at the vault land on the floor. No hit timing, hitbox, damage, cooldown,
  movement or network rule changed; vents keep their schedule, damage and launch.
- Client:
  - `WardZeroVisuals` was rewritten from the shared layout: an asylum great hall turned concert hall, with organ,
    face and rose window, chained great doors, galleries of patients, backwards clocks, and a vault with fan,
    chandeliers, work lights and speakers.
  - All of it is song-locked, and the vents still read `sirenStageHazards`. The beacon spin is now integrated per
    frame, so section changes no longer make the beams jump.
  - MapScenery and NightWorld draw no sky, skyline, trims or grid for theme `siren`. They apply `WARD_ZERO_AIR`
    (haze, overhead key light) and restore the old key position elsewhere.
  - The roof prop casts no shadow. `sirenStage.shell(props)` cuts the near walls and roof away for an outside camera
    (menu overview).
  - Dressing costs about 177 draws (old ward about 257) and 5 point/spot lights (was 4).
  - The lab has new stage views. `docs/SIREN.md` gained a Ward Zero section; screenshot `docs/ward-zero-hall.jpg`.
- Checks:
  - `npm run check:types` passed.
  - New `tests/siren-hall.test.ts` (2) and `tests/siren-stage-visuals.test.ts` (4) passed; the extended
    `siren-contract` passed.
  - Every `tests/siren-*.test.ts` passed, and `tests/siren-network.ts` passed (89 identical snapshots).
  - Full `npm test` on container Node 22: 705/718. The failures are the 12 recorded baseline names plus the
    portable-update test, which fails only on Node 22 and is re-checked on 24.19 in the publication run.
  - Visual: lab sheets at song 50/82/152, plus the real `ArenaRenderer` driven by a local Simulation with an
    authorised Siren and four bots. Checked: build, groove, lift telegraph/eruption, breather, climax, doors,
    galleries, vault and menu overview.
  - Headless SwiftShader only. Native Windows/GPU look, frame rate and audio sync are not checked.
- Risks:
  - Bots still have no pathing and ignore vents.
  - Adds and chip pickups need systems work (spawn and grant logic, minion vent damage) that uses the new hooks.
  - Cell add spawns sit about 2.4 m from the back wall, so multi-unit `summon` offsets need a clearance check.
- Merge and publication (owner asked to merge into the DEV-button branch; standing test-channel instruction):
  - PR #24 merged into `claude/visual-revision` at `5af891cec6b06c2e04fad34d43d3027ec6826964`, tree identical to
    the tested `75a25b9`. That merge also carries the previous chip-draft publication log and Astra's PR #23 log.
  - CI and a clean Node 24.19.0/npm 11.9.0 `npm run verify` match the recorded baseline only: units 706/718 with
    exactly the 12 names (the portable-update test passes on 24.19), the four server tsc errors, and the WebSocket
    rematch timeout. HTTPS integration passed.
  - Two `release:prepare` builds, plus a third at the merge commit, were byte-identical.
  - PR #5 `check-payload` passed (102 files, skip, offline/corrupt refusal, settings sentinel, URL restriction);
    channel/status tests 6/6.
  - Payload audit against `0.2.0a02ae6804bc`: only the client bundles, index, server and release files changed. The
    one key-pattern match is the embedded Rapier WASM base64, also present in the previous bundles.
  - Published content `0.2.58d079f38769`, build `0e75b40bf0eb4d6e09780ae6`, payload `arena-66974bad66385f57.json.gz`
    (22,975,402 bytes, SHA-256 `66974bad66385f5744f25439b8153c776b9ed27b3b278cb4268c897c9cc5b8ce`), at channel
    commit `d5f371486bc8934499fb8af598b346e10b87bb66`. Live feed and Site unchanged.
  - Real network: the isolated install upgraded `0.2.0a02ae6804bc` → `0.2.58d079f38769` from the public channel on
    the first check (102 files verified, settings preserved, same-version skip, menu status).
  - Not run: the native Windows Update button, GPU visuals and audio.
- Status: merged and published to TEST. Next: native look and frame-rate review of Ward Zero, then systems work for
  adds and chip pickups on the new hooks.

## 2026-10-02 — Claude — chip-draft merge and test-channel publication

- Owner asked to merge the chip draft and publish it for the in-game Update button. PR #22 merged
  `claude/sleepy-ritchie-g8ruhv` into `claude/visual-revision` at `a9269ff44e0a14fd245d7e6de9a1da8ffc75883b`
  (tree identical to the tested branch, after merging the shared head `2b436fe` incl. Astra's kill-effect fix).
- Published content `0.2.0a02ae6804bc`, build `d6c50207790e5a10bc39dbc9`, payload
  `arena-d03e12bc009aee7c.json.gz` (22,963,995 bytes, SHA-256
  `d03e12bc009aee7cee056be98f5bce7b59accea1e85eef2843eacc47b106e07c`) at channel commit
  `bc845295ba6d32cdd5edbc4f6b29a123eab36462`. Live feed and Site unchanged.
- Clean detached worktree, Node 24.19.0/npm 11.9.0, `npm run setup`. `npm run verify` nonzero on the recorded
  baseline only: units 697/709 with exactly the 12 names, the four server tsc errors, WebSocket rematch
  timeout; HTTPS integration passed. Two `release:prepare` builds byte-identical; PR #5 check-payload passed
  (102 files, skip, offline/corrupt refusal, settings sentinel, URL restriction); channel/status tests 6/6.
  Payload audit vs `0.2.c9064e15755f`: only the client bundle/CSS, index, server and release files changed; one
  key-pattern match is inside the embedded Rapier WASM and identical in the previous payload.
- Real network: `0.2.c9064e15755f` installed from the channel into an isolated folder, then upgraded to
  `0.2.0a02ae6804bc` on the first manifest check (102 files, settings sentinel, same-version skip, menu status).
  Native Windows Update button and GPU visuals not run. Exclusive-tier draft cinematics were checked in the
  browser with the real markup (shortened spotlight ends with each server reveal).

## 2026-10-02 — Astra — kill-effect persistence test publication

- Owner authorized merge, publication and any needed Update-button fix. PR #21
  merged at 2b436fe7c73f832aeba49679583a59fbcddc4c77. Published that exact tested
  source as 0.2.c9064e15755f (build 2febd9d796681c42028cfeba) to the existing TEST
  channel at e427c71726ea6fac98d985915b2a1d527b07571e. The later concurrent PR #22
  draft-screen merge is not included in this package; preserve it in future builds.
- All tracked source blobs matched the approved remote tree except regenerated
  build identity. Two release:prepare builds produced identical feed bytes.
  Payload arena-217607dd6f5fb52a.json.gz: 22,954,568 bytes, SHA256
  217607dd6f5fb52a7d29f813d9de7410723d71788673ce874fc3f5233eaa20d1.
  Browser upload used the owner's existing fallback authorization; downloaded
  payload hash matched before atomic manifest activation and old-payload removal.
- Actual payload check passes: 102 installed file hashes, settings preservation,
  same-version skip, offline/corruption refusal. Remote channel/status tooling 6/6.
  Real public-feed install from 0.2.95b7f0a5f044 passes all 102 file hashes,
  settings sentinel, same-version skip and menu status. Client updateStatus reports
  available for the old build and current for the new build. No button code change
  needed. Public-feed test uses curl transport to accommodate this environment's
  previously observed Node/proxy timeout; native Electron click/restart unrun.
- The previous entry records the exact code checks and unchanged baseline failures:
  12/12 focused, types/client build pass; broad verification nonzero on known unit,
  server TS and generic WS failures; HTTPS passed. No need to repeat unchanged
  broad gameplay suites for publication alone.
- Status: merged and published to TEST, live feed/backend unchanged. Select
  Impact ragdoll & gibs and Save Pilot once after updating. Protection is local
  per account/device until the account service supports that effect. Native
  Windows selection/restart and cross-device acceptance remain unverified.

## 2026-10-02 — Claude — full-screen 3D chip draft and a longer kill-cam pre-roll

- Owner feedback after the kill-cam test update: let the replay start a little later, and rework chip
  selection into a full-screen scene with the pilot. Branch `claude/sleepy-ritchie-g8ruhv`, merged with
  shared `claude/visual-revision` `7d31530` first. Not merged into the shared branch; not published.
- Kill cam: new `KILLCAM.hold` (.6 s). Playback now starts ~1.2 s after death (aftermath + hold); the
  stalled-packet deadline grows by the same amount, and the full clip still fits the 5 s result. Tests derive
  their timing from the constants, and a new test covers the hold. Lifecycle/cancellation unchanged.
- Draft: `draft-stage.ts` renders the current picker's dressed rig on a podium through the main renderer
  (PlayerIntros pattern) while the phase is `draft`. `draftScreen` (draft-presentation.ts) replaces the panel with
  a full-screen overlay: heading/turn order on top, the picker's nameplate, and the offers as a horizontal tray
  (one row up to six; two rows for 7-12; mid-width screens split six as 3+3; phones and short landscape windows
  use one sideways-scrolling row). Cards stay real `<button data-chip>` chips (compact size), so the click path,
  readyAt gate, aria labels and existing markup tests are unchanged.
- Look and grab: `draft-view.ts` maps card centres onto a plane in front of the pilot. The head/neck/chest turn
  to the hovered or focused card on your own turn; otherwise they follow the boot-in, then a slow scan. On a pick
  (driven by authoritative `draft.chosen/chosenAt`, so bot, timeout and other viewers' picks animate too), the
  others shatter, the card flies (fixed-position copy) into the projected palm while the hand on that side
  reaches (rig-ik `hand`), a glowing 3D chip lands in the palm at .48 s with a rarity flash and the install
  sound for every viewer, and the existing details spotlight with all rarity cinematics opens at .85 s (inserted
  on the sim clock, duration shortened to end with the 3.2/4.6/4.8 s server reveal). Emperor/Ansatsuken
  cinematic keyframes now follow `--selection-duration`. Arrow keys move focus, Space/Enter pick.
- Set losers draft at hp 0, so the stage poses a cleaned-up `stageView` (alive, idle), including Siren. The
  arena keeps updating underneath but is not drawn; compatibility (SVG) rendering keeps the old arena backdrop
  with a dark card backdrop. No sim, network, protocol, timing, hitbox, damage, cooldown or save change.
- Checks (Node 24.19.0/npm 11.9.0): types pass; full units 689/701 with exactly the 12 recorded baseline names;
  new `tests/draft-screen.test.ts` (7: rows, timeline inside the reveal, own/other markup + escaping, taken slot
  and spotlight gating, look/reach accuracy and bone lengths, dead-picker staging) and the kill-cam hold test
  pass; `tests/killcam-network.ts` passes (87 snapshots); Vite build passes. Independent review (3 lenses)
  found the row clipping the flying card, arrow keys swallowed for dead pickers, dead-picker posing, gaze before
  IK, per-pick hand latch, contact-sound dedupe, light-count shader hitch, reduced motion, online clock and
  row scroll reset; all fixed here.
- Rendered (headless Chromium/SwiftShader, sandbox draft preview): 1-12 offers at 1920x1080, 1366x768,
  1280x720, 960x540, 844x390 and 390x844; hover look-at; grab with either hand; spotlight hand-off; a guest
  watching a host's pick in a real WebSocket test room. `docs/chip-arena-draft-stage.jpg`.
- Not verified: native Windows/GPU frame pacing (software GL), exclusive-tier (Mishima/Ansatsuken/L44T/Siren)
  grabs, audio levels, two-PC play. Hover is local only (other viewers see the scan, as before no protocol).
  `tools/chip-lab` still renders the old panel markup.

## 2026-10-02 — Astra — preserve kill effects across legacy account sync

- Branch codex/kill-effect-save from shared 16f4bcc9f4ea71ab165d462b13e48fd5a6f1372f.
  Owner reports Impact ragdoll & gibs periodically changing to Voxel breakup,
  including the customization dropdown.
- Evidence: currently served /chip-arena-source.zip at AUTH_ORIGIN has an older
  cosmetics catalog without impact-ragdoll; its sanitizer falls back to voxel-burst
  and account savedProfile/view both use that sanitizer. This archive is evidence
  of the published site's version, not a direct production DB/worker inspection.
  Current desktop code accepts impact-ragdoll, but account loads overwrite local
  cosmetics and saves previously reported success without checking the returned choice.
- Added account-scoped pending kill-effect preference. Explicit local selections
  and save attempts survive an older server's unchanged fallback and failed requests.
  A successful acknowledgement or a different later cloud effect clears the pending
  value. Current access checks still reject revoked/unauthorized DEV effects.
  Save messages disclose when the cloud did not retain the effect; other profile
  fields retain their existing cloud behavior. No combat/network/save-server schema
  changes, credentials, production account mutations or backend deployment.
- Verified: Node 24.19.0; types and 12/12 focused sync/account tests pass; client
  Vite build passes. Full units 690/702: same 12 failure names as published baseline,
  no new unit failures. Four existing server TypeScript errors remain.
  Full verify exits nonzero on those baseline failures and the known WebSocket
  rematch timeout. HTTPS passes: five sets, four drafts, 3,693 matching snapshots,
  destruction agreement, retry, rematch and guest leave (188.4 s).
- Limits: local protection is device/account scoped, not cross-device cloud support.
  Until the account backend recognizes the new effect, select it and Save Pilot
  once after this fix; a previously lost choice cannot be reconstructed safely.
  Windows dropdown/restart acceptance not run. Implemented on task branch only;
  not merged or published. Do not publish the whole browser Site to fix this issue.

## 2026-10-02 — Astra — bot intro integration and test publication

- Owner authorized merge and in-game publication. PR #18 merged into the kill-cam
  branch at 54fa61b; PR #19 integrated into claude/visual-revision at
  cc8eb637083d881a96f53699d67452d4b602e0b5. This exact source includes Claude's
  kill-cam presentation and Siren super armor plus boss-only bot intros.
- Published content 0.2.95b7f0a5f044, build 4beea896863a860cc27c9216 to the existing
  TEST channel, commit 630b9f55c83ef7235d7d78f8502af48d22978ec7. Payload
  arena-4df9cf86d6afed03.json.gz, 22,953,915 bytes, SHA256
  4df9cf86d6afed031452b5c8b59cc1867a179719290ef02408162c325d43f2c3.
- Node 24.19.0/npm 11.9.0; real Rapier and unchanged locked dependencies.
  Source files checked against remote blob hashes (build identity regenerated).
  Two release:prepare builds produced identical feeds; actual payload installation
  verified all 102 files, settings sentinel, same-version skip and corrupt/offline
  refusal. Remote channel/status tooling 6/6 passes.
- Full verify remains nonzero: units 685/697 with the same 12 documented failures,
  four known server TypeScript errors, generic WebSocket rematch failure.
  HTTPS integration passes: five sets, four drafts, 3,365 identical snapshots,
  destruction agreement, retry, rematch and guest leave. Focused 23/23 and intro
  network 192 identical snapshots previously passed.
- Connector upload exceeded its 16 MiB request limit. Owner approved browser
  fallback; payload uploaded in GitHub UI, downloaded checksum verified, then
  manifest/log activated atomically and old payload removed. Real public feed
  installation from 0.2.1aeb4fad8c90 passed all 102 file hashes, same-version skip,
  settings preservation and menu status. Default Node network fetch hit the
  proxy timeout; the public-feed check used curl as the fetch transport with a
  60-second timeout. Shipped updater and its timeouts were not modified.
- Status: merged and published to TEST; live feed unchanged. Native Windows/GPU,
  replay audio and physical two-PC acceptance remain owner checks. Existing test
  installations use the in-game Update button; CDN propagation can take minutes.

## 2026-10-02 — Astra — boss-only bot intros

- Branch `codex/boss-only-bot-intros`, remote base `codex/killcam-siren-armor`
  `9d7c218683486b8a7c6fe31f0ec1e110595e703d` (includes Claude kill-cam UI).
- Authority intro queue now excludes ordinary bots, including non-boss DEV weapons.
  Human intros remain; authorized Siren bots retain their entrance, last in order.
  Removed slots add no wait. Empty queues go directly to countdown. Global skip and
  sandbox still bypass all intros; boss music uses the shortened authoritative queue.
- Checks: Node 24.19.0, locked dependencies installed offline, real Rapier; 23/23
  focused bot-intro, existing intro, kill-cam and Siren armor tests pass. New bot
  cases rerun 3/3 after test type-narrowing correction; `npm run check:types` passes.
  Existing two-client intro network suite passed (192 identical intro snapshots,
  both human names, combat locked; host skip setting synchronized).
- No hit timing, hitbox, damage, cooldown, movement, snapshot schema or save changes.
  Native Windows visual/audio acceptance not run. Separate add-on PR targets the
  kill-cam feature branch; implemented on branch, not merged or published.

## 2026-10-02 — Claude — owner-requested kill-cam test-channel publication

- Owner asked to publish the merged kill-cam work to the existing test Update channel.
  Source `claude/visual-revision` at `7d31530cbe996dd45744e45da2442ace9eefecdb` (PR #16 merge,
  includes Claude PR #17); clean detached worktree, Node 24.19.0 / npm 11.9.0 (official tarball,
  SHA-256 checked), `npm run setup`, real Rapier 0.19.3. Live feed and Site unchanged.
- Published content `0.2.1aeb4fad8c90`, build `786c2215912319ba449a53d3`, payload
  `arena-0a306d01b2140b24.json.gz` (22,953,525 bytes, SHA-256
  `0a306d01b2140b24f4c387104e9c9ac9cf14847e1238020ee35e8cab578d2fe4`) at channel commit
  `012d37bdf6aa504f0d8b4058b9552c7936743f3a` in `sheldon37/chip-arena-test-updates`
  (manifest + single payload swapped together; PUBLISHED.md row added).
- Gate on the exact commit: `npm run verify` nonzero on the recorded baseline only. Units
  682/694 with exactly the 12 recorded failure names (the updater test that failed under the
  container's Node 22 passes on 24.19.0); four known server tsc errors (chaos.ts, leet.ts ×2,
  standard-melee.ts); generic WebSocket rematch timeout. HTTPS integration passed.
- `release:prepare` run twice: manifest and payload byte-identical. PR #5 `check-payload.mjs`
  passed on the real payload (102 files, same-version skip, offline/corrupt refusal, settings
  sentinel, URL restriction); remote-channel/status tests 6/6 in their original layout.
- Independent read-only pre-publish audit (3 agents: provenance, secrets/safety, channel
  format) found no blockers: recomputed GAME_BUILD matches; kill-cam UI and Siren armor are
  present in the bundles; every byte changed vs `0.2.c99e20077a04` traces to `97daa88`/`4e9a65d`;
  no secrets (embedded JWKs are public verify keys, unchanged); +8,244 bytes, under the caps.
  The committed `build-info.generated.ts` is stale (the build stamps it); cosmetic.
- Real network: before publishing, the then-public `0.2.c99e20077a04` was installed through
  the real updater from the channel into an isolated folder (102 files). The public manifest
  served the new version on the first check after push; the same folder then upgraded to
  `0.2.1aeb4fad8c90` (102 files verified, settings sentinel preserved, same-version skip,
  remote menu-status handler reports the new build/version).
- Not verified: native Windows EXE/Update button, GPU visuals, replay audio, two-PC play.
  Diagnostic test build with the documented baseline failures. Existing test installations
  press Update.

## 2026-10-02 — Claude — kill-cam UI, transitions and replay camera framing

- Task: `docs/KILLCAM-HANDOFF.md` UI/camera scope. Branch `claude/sleepy-ritchie-g8ruhv`
  (service-assigned name) from Astra's feature head `97daa8861211d76f730052dd092a9234b51f426d`
  (PR #16, `codex/killcam-siren-armor`). Siren armor and replay lifecycle untouched.
- Replaced the placeholder `killcam-hud.ts` with a broadcast-style frame in new `killcam.css`:
  letterbox bars, corner `KILL CAM` tag with a clock relative to the fatal hit (−2.4s → +0.3s),
  lower-third card (killer name in their colour, team tag, weapon icon/name, readable
  cause; environmental/self deaths read "You were eliminated" + cause), a timeline with a
  fatal-hit marker and red aftermath segment, and a pill Skip button (`data-action="killcam-skip"`
  kept, works without pointer lock). Nothing covers the screen centre. Names, weapon and team
  strings are HTML-escaped; colours must match `#rgb/#rrggbb` before reaching inline style.
- Transitions: bars/card/tag animate in with a short flash covering the camera cut. In the last
  0.3 s the bars, card and controls retract while the tag stays until the cut. Return to live
  spectating mounts a one-shot flash and a green `LIVE` chip (1.6 s, fight phase only). HUD is
  redrawn immediately at both boundaries (no 100 ms lag behind the camera cut). The root keeps a
  stable class/`data-death` per clip, so the 10 Hz HUD morph never restarts entry animations.
  Reduced motion: no flash/pulse, instant states. Responsive rules for ≤850 px, ≤580 px phones
  and ≤480 px-tall landscape. No backdrop blur over the 3D view (cheap at every quality).
- Developer sandbox panel and live death ticker are hidden during replay (`#ui[data-killcam]`).
- Camera: visual review found the centred third-person pivot hid the victim behind the killer
  in melee kills. Replay-only `killcam-camera.ts` moves the pivot to the recorded pilot's right
  shoulder (.85 m, wall-checked) and .22 m up, converging on the same aim point. Live,
  spectator and first-person cameras, aiming and muzzles are unchanged.
- Small systems touch (coordinated here for Astra): `KillcamInfo.at` = recorded fatal-hit time
  clamped into [start,end], for the timeline marker. Additive; sampling/lifecycle unchanged.
  No gameplay, hit timing, hitboxes, damage, cooldowns, movement or network/snapshot changes.
- Checks (Node 22.22.0 in this container; repo pins 24.19.0): `npm run check:types` pass;
  focused `killcam`/`siren-super-armor` + new `killcam-presentation` tests 18/18; 
  `tests/killcam-network.ts` passes (3 clients, 75 identical snapshots, victim-only replay,
  authority unchanged, live packets, skip, round reset, armor damage/no launch). Vite build pass.
  Full units 677/690: the 12 recorded baseline names plus `portable update ... survives offline`,
  which fails identically with this diff stashed (environmental, Node version), so no new failures.
- Rendered verification (headless Chromium + SwiftShader, local Vite + authority, ~2 fps):
  local hard-bot 1v1 death (replay into round result), 6-pilot FFA death (completion → live
  spectating with LIVE chip), Space skip mid-replay, sandbox self death (victim fallback card),
  online WebSocket room with bots (killer view, completion, spectating), performance graphics +
  first-person preference (replay forced third-person; next round after draft restored
  first-person with no replay leftovers), high graphics. Static layouts at 1920×1080,
  1280×720, 820×1180, 390×844 and 844×390; escaping probe raised no dialog.
  Screenshots: `docs/chip-arena-killcam.jpg` (online melee replay, shoulder framing),
  `docs/chip-arena-killcam-phone.jpg` (phone layouts, killer and environmental).
- Not verified: native Windows/GPU frame pacing (SwiftShader runs ~2 fps, so animation feel was
  judged from timings, not smooth playback), replay audio, physical two-PC rooms, simultaneous
  kills, killer disconnect mid-clip, lava/fall/Siren Last Laugh-specific deaths and destruction
  rewinds. Card shows the weapon held in the recorded frame, not necessarily the killing attack.
- Status: owner-authorized integration done. PR #17 (`4e9a65d`) merged into
  `codex/killcam-siren-armor` at `9d7c218`; PR #16 merged into `claude/visual-revision` at
  `7d31530cbe996dd45744e45da2442ace9eefecdb`, tree `37424fa` identical to the tested tree.
  Published afterwards on the owner's explicit request; see the publication entry above.

## 2026-10-02 — Astra — kill-cam systems and Siren ability super armor

- Task branch `codex/killcam-siren-armor`, exact shared base
  `claude/visual-revision` at `4dbb1513a2f8d6c782cfddb5b8ee91fad1dd9d47`.
  All 549 remote source blobs verified before edits. Separate local verification
  checkout; GitHub commits preserve the actual remote parent, not synthetic history.
- Added bounded client snapshot history and automatic victim-only replay: up to
  3 s before death + .6 s aftermath, recorded killer camera with victim fallback,
  Space/Esc/button skip, then live spectating. Live authority/input identity and
  network processing continue. Historical HP, deaths, destruction and attacks
  never borrow live/future state. Round/draft/respawn/lobby/disconnect cancel safely.
- Added replay renderer/audio reset boundaries and a basic isolated HUD seam.
  Claude owns final UI/camera polish: `src/client/killcam-hud.ts` and
  `docs/KILLCAM-HANDOFF.md`. No designed UI overhaul or regenerated assets.
- Siren's eight numbered abilities resist physical interruption through recovery,
  including Marked lock and Last Laugh hold. HP/DOT/lethal damage still apply.
  Guards cover normal/DEV melee, rebound, juggling, Chip forces and stage launches.
  Primary attacks and persistent rain/fire/seeker lifetimes do not extend armor.
  Damage values, attack/hitbox timing, cooldowns and costs are unchanged; forced
  movement/stun resistance changes intentionally. No snapshot/save schema change.
- Node 24.19.0/npm 11.9.0, real Rapier 0.19.3, unchanged lockfile/dependencies.
  New focused tests 14/14; typecheck and final client Vite build pass.
  Full units 678/690 versus untouched gameplay baseline 664/676: identical 12
  failure names, no new failures. Initial fixture-only TypeScript errors were
  fixed; final typecheck passes. Full `npm run verify` was rerun and remains
  nonzero: those baseline units, four known server Rapier/NodeNext type errors,
  and the existing generic WebSocket rematch timeout. Client/relay compile pass.
- Dedicated `tests/killcam-network.ts`: 3 real WebSocket clients / 75 identical
  snapshots; victim-only replay, authority unchanged, continued live packets,
  skip/reset and shared armor damage/no launch. Existing Siren network suite passes
  with 89 identical snapshots. Final HTTPS integration passes: five sets, four drafts, 3,516 shared snapshots,
  destruction agreement, retry, rematch and guest leave (179.4 s).
- Visual test blocked: Cloud Browser rejected local Vite with
  `net::ERR_BLOCKED_BY_CLIENT`; no installed local Chromium or native Windows/GPU.
  Rendered replay/transition/audio quality and physical two-PC acceptance remain
  unverified. Node-only 8-player/262-prop diagnostic: 101 retained snapshots,
  ~18.8 MiB heap delta, ~1.24 ms/capture, ~.046 ms/sample; not a GPU benchmark.
- Status: implemented on task branch for PR/Claude handoff; NOT merged or released.
  No update-feed or Site publication. Next: Claude visual/UI review using the handoff,
  owner-approved integration into the shared branch, then the existing test channel.

## 2026-10-01 — Astra — owner-authorized body damage merge and test publication

- After the verification report below, the owner explicitly requested merge and
  publication to the existing test Update channel. PR #14 merged into
  `claude/visual-revision` at `25599c4052af5f3f50457ea7491dcb01b6216076`.
  Its tree exactly matches the verified combined head `d7af2d2`.
- Published content `0.2.c99e20077a04`, build `4dea4f9ddc1b3bb217acf8f9`,
  channel commit `8bf843b2963b0575e6a13e41c10afcfa0e7fcca8` in
  `sheldon37/chip-arena-test-updates`. Manifest and payload changed atomically;
  PUBLISHED.md records the source commit. Live feed and Site were not changed.
- Clean release directory: all 549 source blobs verified against the merge tree.
  `npm run release:prepare` passes twice with byte-identical manifest/payload.
  Payload: 22,945,281 bytes; SHA-256
  `aafb58f58c54cb5245694beb127d4d328e294cc1c87540d698948ebe0c9246ff`.
  Existing check-payload verifies all 102 installed files, same-version skip,
  settings sentinel, offline/checksum refusal and URL restrictions. PR #5's
  remote-channel/status tests pass 6/6 using the original scripts in their required
  directory layout (an initial relocated-script test failed on its own missing path).
- Network verification: installed the previous public version `0.2.2fb402472edb`
  through the real updater into an isolated folder; all 102 files, settings
  preservation, same-version skip and the remote menu-status handler passed.
  The main public endpoint initially served the cached old manifest, then returned
  the new version on the seventh propagation check (~120 s). The follow-on real
  network installation was blocked by this environment's network policy for
  raw.githubusercontent.com; the previous install stayed unchanged. New-payload
  installation is verified by the local actual-payload check above, not claimed
  as a completed public-network upgrade or native Windows button test.
- Release remains an owner-approved diagnostic test update: the 12 baseline unit
  failures, four server tsc errors, generic WebSocket rematch timeout and unrun
  real-Rapier rendered/Windows visual acceptance from the previous entry remain.
  No additional gameplay or presentation changes were made during publication.

## 2026-10-01 — Astra — real-Rapier body damage verification and PR #14 integration

- Integrated Claude `afb7561075ba71cef78ff091f2a5e7cea6b2b725` (parent systems
  `2f148ad5dfaf1495ca9018633a454783bbf4d86c`) into `codex/body-damage-systems`,
  draft PR #14. Physics fixes are commit `6148fe27b0684fbab3f50ac3ef1439225da9b2d9`.
  Shared target remains `claude/visual-revision` at `a2480ba6985bb8a60efc10f79839eeb7d3dc9a27`.
- Separate API-materialized verification checkout: all 549 tracked file bytes matched
  Claude's Git blob hashes, including media recovered from the uploaded source/test ZIP.
  Node 24.19.0, npm 11.9.0, fresh `npm ci --no-audit --no-fund`; runtime Rapier 0.19.3.
  These results use real Rapier WASM, not Claude's stand-in. Local synthetic Git history
  was not pushed; remote commits retain Claude's actual parent history and tree.
- Fixed only the physics adapter and regression tests. Impact deaths with a motion
  contract no longer receive the legacy per-part spin (measured up to 18 rad/s for a
  melee victim inheriting only 0.3 m/s). Their initial linear velocity still consumes
  the authority result once plus the existing bounded kick for a detached subtree.
  Explicit cosmetics and old snapshots without motion retain their legacy fallback.
- Real Rapier exposed a release discontinuity missed by the prior chest-only test:
  the right forearm jumped 9.2 cm (cavity) / 9.9 cm (decap) when script visibility
  switched to solver-corrected bodies. At the 50/60 s boundary, copy the exact scripted
  pose and velocities once before physical steps continue. The same boundary probe
  (10 microseconds apart) now measures <0.05 mm across connected parts in those fixtures.
  Detached heads remain physical throughout. Claude's animation curves/meshes are unchanged.
- New regression coverage observes real bodies/joints: no added spin or double force,
  equal kick across a severed arm subtree, all-part release at 20/30/60/144 Hz, identical
  late/stepped physics, immediate head flight, and head/arm subtrees freed by a spine crush.
  `body-damage-visuals.test.ts` passes 7/7. Initial requested focused run passed 61/61;
  the final full suite also passes all requested wound, visual, existing ragdoll and
  body-damage tests, including the three added regressions. Wound tests pass 11/11.
  `body-damage-network.ts`: three real WebSocket clients, four identical HostedRoom
  snapshots (living wound, fatal cavity/kneel, later death, round reset).
- Complete systems baseline `2f148ad`: 646/658 units. Claude before fixes: 661/673.
  Final fixes: 664/676. Exactly the same 12 failure names on base and final; no new
  failures. Restoring matching media made three of the previously reported 15 pass:
  original intro music assets, fourteen-map budget/reset, and menu-music playback.
  Remaining failures cover music catalog count, behavior/catalog counts, Demon Step,
  reference poses, disconnect forfeit, block knockback, dagger leading-hand pose,
  vertical-platform support, shield DoT and PCM sample rate. They were not suppressed.
- `npm run check:types` and Vite pass. Broad `npm run verify` also passes client/relay
  compilation, but server compilation retains the four known errors (chaos.ts,
  leet.ts twice, standard-melee.ts); generic WebSocket integration times out at rematch.
  HTTPS integration passes: five sets, four drafts, 3,430 identical shared snapshots,
  destruction, retry, rematch and guest leave; 174.5 s. Broad verification is not green.
- Visual gate BLOCKED: `npm run dev -- --host 127.0.0.1` started Vite and the authority,
  but Cloud Browser rejected the kneel lab URL with `net::ERR_BLOCKED_BY_CLIENT`.
  No real-Rapier rendered lab scenes/screenshots, Windows/GPU, first-person/spectating
  or two-physical-PC visual acceptance is claimed. BODY-DAMAGE-HANDOFF lists the exact
  scene queries for Claude/owner's remaining review; no presentation rewrite was made.
- No gameplay damage, hit timing, hitboxes, cooldowns, knockback values, movement rules
  or network contracts changed in this follow-up. Status: combined feature/fixes pushed
  to draft PR #14; NOT merged or published. Owner explicitly required stopping after
  integration if verification fails: baseline failures plus the blocked visual gate
  prevent step 6. Next: complete real-Rapier visual review and resolve the failing gates
  before merging/publishing; the existing test and live feeds are untouched.

## 2026-10-01 — Claude — body damage visuals: wounds, severing/crushing, kneel/fall

- Task: Astra's `docs/BODY-DAMAGE-HANDOFF.md` (draft PR #14). Branch `claude/body-damage-visuals` from the
  `codex/body-damage-systems` head `2f148ad` (shared base `a2480ba`). Presentation only; the contract is consumed
  unchanged (`resolveBodyWound`, `deathMotion`, `bodyDeathPhase` timing, `bodyPartVelocity`).
- Living wounds (new `body-wounds.ts`; rig-owned layouts in `voxel-kit.ts`): each snapshot wound is anchored once on a
  rest rig (a ray along the shot through the named part; thin plates such as the sternum carry the hole into the body
  behind; contact points just off the surface use the nearest solid face) and carved into that part's voxels.
  Puncture: narrow channel with a bloody rim. Cavity: ragged hollow with lifted petals. Cut: tilted groove. Thrust:
  slot. Bruise: dented, discoloured layer. Scorch: char and embers. Flesh lines the walls (one extra instanced mesh per
  wounded part; the flesh material emits a share of its colour so openings read red under match lighting). Wounds
  stay on the part through animation and are copied into ragdolls, knockdowns and kill effects. Dedupe by `id@at`;
  snapshot absence, heal, reset, rematch, rig rebuild and player removal restore the shared sculpt and release the
  buffers. Pooled spray only for fresh wounds (none on late join or at the first-person camera). Reduced quality
  carves coarser; low detail draws plain boxes; the SVG compatibility renderer skips wounds. Siren and sever/crush
  wounds are skipped (the death effect owns them).
- Impact ragdoll (`ragdoll.ts`, new `body-trauma.ts`, `death-effects.ts`, `gibber-vfx.ts`): `impact-ragdoll` reads
  `death.wound`/`motion`. Severed `destroyedPart`: detaches at its joint with matching voxel cross-sections (flesh, bone,
  marrow, set flush where each part's geometry ends). Crushed part: no body; bounded fragments (≤40, reduced 16), its
  hat or weapon thrown loose, ragged stumps with splinters on the parent and on freed children. Shorter spray
  profiles. `bodyPartVelocity` is unchanged; its bounded kick now reaches the whole detached subtree (inherited
  velocity once). DEV Gibber keeps its path but gets the voxel caps instead of flat discs. Wound cells never shape
  colliders; voxel burst ignores them.
- Kneel/fall (new `body-collapse.ts`): forward kinematics over the ragdoll's own part frames from authority time. Head
  jolt along the shot, legs buckle, knees on the floor at `kneesAt` (feet pivot on a visual ankle inside the shin part
  and stay flat), forward fall about the knees with some hip flexion, limp arms. Cover and the floor limit the
  scripted fall. Bodies get the pose and its velocities before every physics step and are released at the first step
  at or after `releaseAt` (≤1/60 s later); tone holds the release bend .3 s. A severed head flies from the first frame.
  `ragdoll` motion (airborne, launched) is unchanged.
- Renderer: `BodyWounds.update` after posing, before `deathEffects.update` (dead rigs are carved before the corpse
  copies them); the first-person view model gets the local player's wounds. Dev-only lab `tools/body-lab`
  (`?scene=pistol|cavity|decap|cut|thrust|crush|limb|torso|kneel|airborne|finisher|low|gallery`).
- No damage, attack timing, hitboxes, cooldowns, movement rules, knockback values or network fields changed.
- Checks: new `tests/body-wounds.test.ts` 11/11 (no physics engine). New `tests/body-damage-visuals.test.ts` 4/4 only
  against a local toy physics stand-in (not committed): unverified until run with real Rapier. Whole suite on base
  vs branch here (no npm packages): no test that passes on base fails on the branch (53→64 passing; 143→158 with the
  stand-in). Scratch typecheck of changed client files, new tests and the lab: no new errors. Lab renders reviewed for
  every scene plus the wound gallery from several angles; scripted kneel frames are pixel-identical at 20/30/60/144 fps,
  for a late first sighting and for repeated snapshots. Costs here: carve ~0.7 ms per wound, collapse setup ~1.1 ms per
  death, 0.02 ms per physics step.
- Not run: real Rapier (the new integration test; existing ragdoll tests in `weapon-entrances-gibber`,
  `interiors-ragdolls-hud`, `reaction-fluidity`; post-release motion), Vite build, verify, Windows/GPU, multiplayer,
  in-game first person and spectating. The npm registry is blocked in this sandbox.
- For Astra: (1) impact deaths still get the legacy cosmetic spin (lever×dir×15 for melee plus random) on every part;
  on the stand-in melee deaths flail. Please judge it with real Rapier (physics adapter, unchanged here). (2) Optional
  contract: a blade swing direction would let cut grooves follow the swing (today: seeded tilt plus the shot's
  vertical). (3) A `spine` crush removes the upper trunk root only; pelvis stays, head and arms fly free.
  (4) Explicit cosmetic effects keep their behaviour (no sever/crush/kneel); living wounds still ride along.
- Status: implemented on branch and pushed; not merged or published. Next: Astra runs the physics tests and reviews
  post-release motion with real Rapier, then owner-authorized integration into the shared branch.

## 2026-10-01 — Astra — body damage systems and Claude visual handoff

- Owner requested impact-driven bullet holes/cavities, blade cuts/stabs, lethal
  severing/crushing, public default ragdolls/gibs, and grounded fast-headshot
  knees-to-forward-collapse. Astra owns functions; Claude owns visuals.
- Task `codex/body-damage-systems`, shared base `a2480ba`. Added authority-owned
  `body-damage.ts` and cosmetic physics/timeline helpers in `body-ragdoll.ts`.
  Actual projectile identity/velocity/radius survive reflection; debris passes
  finite mass, closing speed and contact point. Damage mitigation affects visual
  severity before remaining-HP clipping. Melee cuts, thrusts, throws and shockwave
  crushes use distinct descriptors. Wounds are bounded to eight per life.
- Optional snapshot wound/death-motion fields carry stable IDs and chosen results;
  reset/heal clears them. Default `impact-ragdoll` is public; explicit saved effects
  and DEV/Siren overrides remain. No damage, attack timing, hitbox, cooldown or
  movement-rule changes. Cosmetic corpse velocity consumes gameplay knockback
  once. No living limb-health system or damaging corpse physics.
- Client code is only the existing-ragdoll fallback/velocity adapter. Claude still
  must implement holes, cavities, crushed fragments, live wounds and kneel/fall.
  Exact ownership, field semantics, timing and acceptance checklist are in
  `docs/BODY-DAMAGE-HANDOFF.md`.
- Checks (Node 24.19.0/npm 11.9.0): 16/16 new real-Rapier tests pass, including
  projectile collisions, reflection, mitigation, blocked/allied hits, reset,
  snapshot cloning and phase selection. Typecheck passes. New three-client real
  WebSocket test receives four identical HostedRoom snapshots: living wounds,
  fatal cavity/kneel choice, later death snapshot and reset. Focused death/default
  tests pass 22/22. Existing selected-voxel effect test now explicitly selects it.
- Full units: 643/658; fresh baseline 627/642 with exactly the same 15 failure
  names. Four media tests cannot run from this source-only API checkout; remaining
  failures reproduce on the base. An earlier incomplete-schema baseline had 25
  failures; the fresh comparison supersedes that result. No dependency changes.
  Client Vite build passes. Relay build passes after fetching its existing license
  file; server build reproduces the four documented TS errors. Broad verify's
  WebSocket suite reproduces the rematch timeout. HTTPS passes five sets/four
  drafts, 3,314 matching snapshots, retry, rematch and leave (182.6 s). Full
  `npm run verify` exits 1; recorded failures were not suppressed. After correcting
  the selected-voxel fixture, the final unit run has no new failure names.
- Windows/GPU, visual acceptance and two physical PCs are unrun. This branch does
  not implement Claude's visuals and is not a green production release candidate.
- Status: draft PR #14, initial systems handoff `fdaf42c`, visuals pending;
  not merged/published.
  Claude should branch from this task head as `claude/body-damage-visuals` and
  preserve the shared contracts. Await owner-authorized integration and actual
  verification before the standing test-channel publication.

## 2026-10-01 — Codex — combined Chip cards, Siren teams and instant bot drafts

- Owner authorized merging Claude's `claude/chip-cards` at `1d16c68` and the
  isolated team fix at `a93d9eb` into `claude/visual-revision`, adding optional
  hidden bot selections, and publishing to the existing test Update channel.
  Integration branch: `codex/chip-cards-bot-draft`; shared base `256395d`.
- Added opt-in **Skip bot chip choices** in Practice draft settings, Create Room
  and host-controlled lobby settings. Bots receive their usual first offered
  Chip through normal eligibility/award checks immediately; their selection and
  reveal screens are skipped. Human choices, timers and reveals are unchanged.
  The saved preference defaults off; room settings survive lobby changes and
  rematches. Guests cannot change it, and only literal true enables it.
- Preserved Claude's card redesign and three Siren Chips (191 total). Combined
  the narrow simulation changes with the team fix: Ward Zero overrides the map,
  not the selected game mode or team assignments. All other Claude-owned card,
  visual, contract and SIREN.md files retain their supplied contents.
- Checks (Node 24.19.0/npm 11.9.0): types pass; focused card/team/skip/settings
  regressions 19/19 pass. Three real WebSocket clients agree on 87 snapshots,
  including instant bot awards, Siren's Crown raising HP to 18,000, preserved
  teams and allied damage protection. Dedicated-server checks pass across 14
  maps/eight players, with 35 identical fight snapshots and host-only settings.
  Full units: 630/642, exactly the same 12 failure names as the isolated fix's
  619/631 baseline, including stale catalog counts. Client/relay builds pass;
  the four known server TypeScript errors and generic WebSocket rematch timeout
  remain. HTTPS passes five sets/four drafts, 3,472 identical snapshots, retry,
  rematch and leave (177.2 s). Full `npm run verify` exits 1 for those known
  failures; broad verification is not green.
- Gameplay changes are bot draft pacing, restored team rules and Claude's three
  Chip stat effects. No attack timing, hitboxes, cooldowns or movement physics
  changed. An optional boolean travels through existing settings/snapshot
  objects; new Chip IDs require clients to update together. No save migration.
- Not run: local visual preview (Chromium download failed), native Windows/GPU,
  audible sync or two physical PCs. Claude's own visual review is recorded below.
- Status: implemented and verified on integration branch; owner-authorized merge
  and test publication next. Publication results will be recorded separately.

## 2026-10-01 — Claude — Chip card redesign and Siren-exclusive Chips

- Owner request: Chip cards looked too basic; make them more appealing, keep the DEV Chips' "wow", give the Siren
  its own glitchy black-flame animated Chips, and add 2–3 boss Chips for testing. Branch `claude/chip-cards`, base
  shared `claude/visual-revision` `256395d` (includes Astra's PR #11 and the bot test publication).
- Cards (`chip-card.ts`, new `chip-cards.css`): each Chip is a processor package: metal legs, a raised heat spreader
  holding the lit die (icon), grade with rank pips, name, effect, compatibility and stack slots. Legendary is
  gold-plated with a gleam, Mythic holographic; Mishima, Ansatsuken and L44T keep their auras on their own packages
  (Mishima's current now runs around the edge instead of across the title). Old card rules were removed from
  `style.css` (72 rules; spotlight cards keep their package background). Player-board chips follow the same look.
- Siren: new rarity `siren` (`content.ts`, `leet-content.ts` weight 0) and `src/shared/siren-chips.ts`: Hollow Crown
  (+3,000 HP), Black Flame Hide (30% thorns, half knockback), Psychotic Break (damage ×1.25, speed ×1.12, incoming
  damage ×1.15). Siren-only, offered with the other DEV kits' exclusive odds (two guaranteed while any remain).
  `siren-card.ts`: black flame, the grin, scanlines and a glitch burst; picking one plays a Siren reveal on the
  default 3.2 s draft timing. `simulation.ts` gets two guard clauses (draft pick and sandbox add refuse Siren Chips
  for other specs). New dev-only `tools/chip-lab`; docs/SIREN.md has the details.
- Gameplay impact: only when the Siren drafts these Chips (HP, thorns, received knockback, damage, movement speed,
  incoming damage through existing stat paths). No hit timing, hitboxes, cooldowns, movement rules or network
  schema changed; three new Chip ids travel in existing fields, so all clients need the update together.
- Checks: new `tests/chip-cards.test.ts` 5/5 (tsx). `rarity-combat` tier-markup expectation adds Siren's 16
  particles. With a temporary local physics stub (removed, not committed), the whole suite on base and branch:
  0 tests that pass on base fail on the branch (186 vs 181 passing; +5 new). The catalog-count tests in core,
  rarity-combat and chaos already fail on base (188 ≠ 182) and now report 191; names unchanged. Scratch typecheck
  of client, simulation and the new test: no new errors (TS 6 flags the CSS import like the existing ones). Chip lab
  renders reviewed: all tiers, hover, reveal, Siren glitch, spotlights, board, 720p, narrow and reduced motion.
- Not run here: real-physics tests, `verify`, Vite build, Windows/in-game draft, multiplayer (npm registry blocked).
- Status: implemented on branch and pushed; not merged or published. Next: owner-approved merge into
  `claude/visual-revision`, then the standing test-channel publication from the merged commit (needs Node 24/npm).

## 2026-10-01 — Astra — preserve teams in Siren matches (isolated branch)

- Owner reported that a red squad versus blue Siren lost its team behavior and
  requested a separate branch while Claude may be working on Chips. Branch
  `codex/siren-team-preservation` starts at shared `256395d`; implementation
  `9844501`. No shared-branch merge or test-channel publication for this task.
- Root cause: `Simulation.syncSirenArena()` explicitly changed the selected mode
  to FFA at match start/intro. Team IDs survived, but allied protection, bot target
  filtering, team colors and shared wins stopped applying. Ward Zero now overrides
  only the map; the selected mode, teams and friendly-fire option remain intact.
- Six new regressions failed on the original code and pass after the fix. They
  cover saved/newly selected/player/bot Siren, red-squad wins, colors, ally damage,
  bot/Marked targeting, bot rebuilds, rounds, sets, rematches and lobby return.
  Updated the old intro assertion that required FFA. Expanded the three-client
  loopback test to submit team choices and verify them after selection and reset.
- Targeted tests: 39/39 pass. Three real WebSocket clients agreed on 87 snapshots;
  team assignments and allied damage protection passed alongside the existing
  Siren checks. Types pass; full units 619/631 with exactly the same 12 baseline
  failure names as PR #11, no new failures. Client/relay compilation passes;
  the same four server TypeScript errors and generic WebSocket rematch timeout
  remain. HTTPS passed five sets, four drafts, 3,679 matching snapshots, retry,
  rematch and leave (187.5 s). Full `npm run verify` exits 1 for those known
  baseline failures; broad verification is not green.
- No Chip implementation, visuals, attack timing, hitboxes, damage values,
  cooldowns, movement rules or network schemas changed. Team gameplay now follows
  the lobby choice instead of being silently disabled by Siren. Windows/GPU and
  physical two-PC playtesting remain unrun.
- Status: implemented on the isolated branch, not merged or released. Published
  content remains `0.2.c452eefcbbac`. Next: coordinate with Claude's Chip work,
  integrate the narrow simulation hunks, then publish under the standing test
  channel instruction. Do not replace the shared branch with this branch wholesale.

## 2026-10-01 — Astra — cutscene/bot test publication and standing channel instruction

- Owner asked to publish the merged changes and continue using the Update channel
  unless told otherwise. Recorded that standing instruction in AGENTS.md and
  DEVELOPMENT.md for both agents. It covers this existing test channel; merge
  authorization, live feed and Site publication remain separate.
- PR #11 merged into `claude/visual-revision` at
  `fd673130a56dac89db1df90133fda10ea0afc728`, with the exact verified task tree.
  Built that source in a clean worktree. Documentation branch:
  `codex/siren-bots-test-publication`, base `fd67313`.
- Published content `0.2.c452eefcbbac`, build `bd733d69b623c360820e25b4`, to
  `sheldon37/chip-arena-test-updates` at
  `79e939f500d2147162428ac4cdeae82c2ed78a90`. Payload: 22,904,059 bytes / 102 files;
  SHA256 `3212d9ce61a9515778588d783fbe371f591906a45f69e63f573b18c39098c5a0`.
  Successful workflow `36821019040` verified and atomically published feed/history
  and removed its temporary branch. Main contains only feed, README and history.
- Includes Claude's silent smile/Last Laugh cutscene, removed voice hooks, Siren
  15,000 base HP, DEV-only bot loadout choices, and normal/DEV bot abilities.
  No gameplay, hit timing, hitboxes, damage, cooldowns, movement or networking
  changed during publication; gameplay impacts of PR #11 are recorded below.
- Node 24.19.0/npm 11.9.0: clean npm restore and two `release:prepare` builds
  produced identical feed bytes. Actual payload checker passed all installed
  hashes, same-version skip, offline/corrupt checksum refusal, settings sentinel
  and test-feed URL restriction. Channel/status tests 6/6; auth/manual updater
  tests 7/7. Original music MP3 hash unchanged.
- Actual network updater installed previous `0.2.149269b37b5b` from immutable
  channel `3e9d5d9`, then upgraded through main to `0.2.c452eefcbbac`. All 102
  installed hashes, preserved settings, same-version skip and menu version/build
  passed. The previous manifest needed one timeout retry; the new upgrade passed.
- Reused exact-code PR #11 checks: 57/57 feature tests, full units 613/625 (same
  12 baseline failures), three-client Siren 84 matching snapshots, HTTPS 3,505
  matching snapshots. Known four server TS errors and generic WebSocket rematch
  timeout remain; broad verification is not green. Windows Electron/GPU,
  audible/visual acceptance and physical two-PC testing remain unrun.
- Status: published to the existing test Update channel. Existing test installs
  press Update. No new seed ZIP needed; live feed and Site unchanged. This
  documentation follow-up is scope/whitespace checked. Next: Windows playtest
  of the two cutscene roles, DEV bot choices/abilities and Siren HP.

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
