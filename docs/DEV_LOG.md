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

