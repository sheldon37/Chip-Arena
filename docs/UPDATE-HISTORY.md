# Chip Arena — game update history

Start here for the game's past updates; use [DEV_LOG.md](DEV_LOG.md) for current
agent handoffs and [PROJECT_STATE.md](PROJECT_STATE.md) for migration/release status.
This is a retrospective index of the **68 retained source snapshots**, ending at
`18231698c7c79a7a604e567b933eb2d0155d7a3e`, plus the subsequent GitHub work.
It was reconstructed from Git diffs and checked-in feature/test notes on 2026-09-29.

## How to interpret this history

- The commit links are implementation evidence and preserve the actual changes.
  The summaries describe those diffs and their accompanying notes; they are not
  new verification or a guarantee that every old feature still works today.
- Most original messages were simply “Update Site source.” This index supplies
  readable topics without rewriting Git history. Separate corrective snapshots
  remain separate rows.
- A source snapshot, a Site publication number, a desktop launcher version and an
  update-content hash are different identifiers. This document does not invent
  release numbers for snapshots. v55–v60 below are labels from existing note files.
- The migration report associates the final original snapshot with published Site
  version 64. Earlier individual deployment outcomes are not independently audited
  here. The GitHub migration/candidate PRs remain unmerged and unreleased.
- Dates below are commit timestamps converted to America/New_York. Older notes
  sometimes use their original session/date convention.
- Later changes supersede older rules. For example v59 mapped Emperor E to roll;
  the last original snapshot restores E wavedash while Shift remains roll.
  Historical Chip counts and balance numbers are not current-balance promises.

## Useful feature records

| Topic | Records to read |
| --- | --- |
| Core physics, projectiles, timed blocks, networking and early verification | [TEST-RESULTS.md](TEST-RESULTS.md), [TESTING.md](TESTING.md) |
| Articulated rig, expanded arena and sniper | [RIG-ARENA-SNIPER.md](RIG-ARENA-SNIPER.md) |
| Historic Chip catalogue and formulas | [CHIP-CATALOG.md](CHIP-CATALOG.md); verify current rules in src/shared/content.ts and related content modules |
| Clown Love / first-block correction | [CLOWN-LOVE.md](CLOWN-LOVE.md), [FIRST-BLOCK-FIX.md](FIRST-BLOCK-FIX.md) |
| DEV L33T H4X | [L33T-H4X.md](L33T-H4X.md), [L33T-VALIDATION.md](L33T-VALIDATION.md) |
| Accounts, Ansatsuken, parry and Raging Demon | [ACCOUNTS-ANSATSUKEN.md](ACCOUNTS-ANSATSUKEN.md) |
| Emperor, maps, music, score circles and desktop transport | [TEST-RESULTS.md](TEST-RESULTS.md), [DESKTOP.txt](DESKTOP.txt) |
| Standard melee / death announcements / entrances / Gibber | [v55](update-v55.md), [v56](update-v56.md), [v57](update-v57.md) |
| Block stun / stamina / rolls / melee reactions | [v58](update-v58.md), [v59](update-v59.md), [v60](update-v60.md) |
| Current design preferences and source map | [DESIGN_INTENT.md](DESIGN_INTENT.md), [DEVELOPMENT.md](DEVELOPMENT.md) |

## Original game-development snapshots

| Date (Eastern) | Source snapshot | Change focus |
| --- | --- | --- |
| 2026-09-26 | [d399eb9](https://github.com/sheldon37/Chip-Arena/commit/d399eb964594f931c23d80384b043217d7ef36a2) | Initial voxel arena prototype: classes, physics/destructible props, bots, match authority and network tests. |
| 2026-09-26 | [0c033f8](https://github.com/sheldon37/Chip-Arena/commit/0c033f8cc1fa6584e9f15d89a5c1b56a95591faf) | Expanded Chip/modifier rules and inspection/UI support. |
| 2026-09-26 | [ed05b65](https://github.com/sheldon37/Chip-Arena/commit/ed05b65c498eb546cb92c5a120f466639e75edbd) | Ballistic projectile drop and associated modifier/combat adjustments. |
| 2026-09-26 | [2a51e1c](https://github.com/sheldon37/Chip-Arena/commit/2a51e1c510ea639d7005b2e0ec43dd196707c245) | First-person presentation, audio, projectile trails, fire and poison effects. |
| 2026-09-26 | [c28a561](https://github.com/sheldon37/Chip-Arena/commit/c28a56175fb138d13c4df2365af811196d048369) | Set flow, visible status effects and player/build inspection board. |
| 2026-09-26 | [bf6ba57](https://github.com/sheldon37/Chip-Arena/commit/bf6ba57d45d4bf750adea6e3c7027783e56ba63f) | Timed Block and browser-hosted rooms; shared room authority. |
| 2026-09-26 | [fb33201](https://github.com/sheldon37/Chip-Arena/commit/fb3320168aed966d25eeab6111179e4af1090192) | HTTPS relay room transport and local relay test infrastructure. |
| 2026-09-26 | [2b2882a](https://github.com/sheldon37/Chip-Arena/commit/2b2882a76db4fee2c409a59de4d02ad048a43ca0) | HTTPS integration test transport and verification follow-up. |
| 2026-09-26 | [928b563](https://github.com/sheldon37/Chip-Arena/commit/928b5637be0ee8a539227d6d8aada8de7476999e) | Relay/host-room and client prediction follow-up. |
| 2026-09-26 | [5bed720](https://github.com/sheldon37/Chip-Arena/commit/5bed72075b9f83f89285bb4dc99d79c5c1a1eb3e) | Connection routing, movement prediction and remote snapshot interpolation. |
| 2026-09-27 | [bfa4f00](https://github.com/sheldon37/Chip-Arena/commit/bfa4f00c78cca9a1628bcabf1e7c0e1c2dd8a887) | Portable Windows launcher and character/death animation update. |
| 2026-09-27 | [3e9200f](https://github.com/sheldon37/Chip-Arena/commit/3e9200f60a0392cd1d34571a669300ac958587cd) | Articulated character rig, weapon/Chip visuals, expanded arena and sniper rifle rework. |
| 2026-09-27 | [74ea688](https://github.com/sheldon37/Chip-Arena/commit/74ea688f089aeb691b9230efa14e554fc235d2ff) | Expanded Chip catalogue, rarity presentation and combat/draft adjustments. |
| 2026-09-27 | [419e248](https://github.com/sheldon37/Chip-Arena/commit/419e2483f23968dc7cd7d893bcd3c67df004162a) | Chaotic Chip mechanics, cosmetics, music, kill cinematics, developer authentication and content updater. |
| 2026-09-27 | [1469888](https://github.com/sheldon37/Chip-Arena/commit/146988829a80dfc098d26455f20da85ff9bf9341) | Clown Love kill-effect animation, clown rig, timing and sound. |
| 2026-09-27 | [794c734](https://github.com/sheldon37/Chip-Arena/commit/794c734bce028e3c92a211801846d38f39e0f101) | First-block proc and feedback corrections. |
| 2026-09-27 | [36536b6](https://github.com/sheldon37/Chip-Arena/commit/36536b69eafc5809f33bed4a4c08c9357274c629) | Bass Van kill-effect rig, animation and sound. |
| 2026-09-27 | [60ade64](https://github.com/sheldon37/Chip-Arena/commit/60ade6456e7a107f4b0095f227d4a08db675031b) | Bass Van timing and presentation refinement. |
| 2026-09-27 | [1d5848e](https://github.com/sheldon37/Chip-Arena/commit/1d5848ecba308f880fa76486c70c3bad63d693e1) | Night lighting, projectile glow, block tint and knockback/movement adjustments. |
| 2026-09-27 | [f6ec317](https://github.com/sheldon37/Chip-Arena/commit/f6ec3178cba1ed67e1a0c592a92c0e304c6fa008) | Player introductions with shared timing, visuals and synthesized audio. |
| 2026-09-27 | [29f75dc](https://github.com/sheldon37/Chip-Arena/commit/29f75dcc990b1b8906f19316c0c061d7f8baa71d) | Intro choreography/audio and cinematic/armor presentation refinement. |
| 2026-09-27 | [075b84f](https://github.com/sheldon37/Chip-Arena/commit/075b84f67d3a8cd8abbb7d0ce32e45c5bb772668) | Audio follow-up and cinematic verification changes. |
| 2026-09-27 | [6eb51a6](https://github.com/sheldon37/Chip-Arena/commit/6eb51a66637400710729de1a9672ecf884ff20ea) | Electron desktop shell, HTTPS party rooms and render/burst-budget changes. |
| 2026-09-27 | [63a042b](https://github.com/sheldon37/Chip-Arena/commit/63a042be13eb5b5f52e7f4e642851835e17100ae) | Desktop connection diagnostics and launcher follow-up. |
| 2026-09-27 | [10c73a0](https://github.com/sheldon37/Chip-Arena/commit/10c73a0c8756a921b7028948d8c8283d700ec21b) | Local-host invite handling and desktop/server startup improvements. |
| 2026-09-27 | [6039d49](https://github.com/sheldon37/Chip-Arena/commit/6039d4973b300150360793c7374b944d625072b7) | Packaging/generated-output cleanup; no gameplay-source changes in this snapshot. |
| 2026-09-27 | [8b65eda](https://github.com/sheldon37/Chip-Arena/commit/8b65eda28ab03c27453453544d034d0926225a2e) | Connection metrics, server socket output and network-bandwidth checks. |
| 2026-09-27 | [301be8f](https://github.com/sheldon37/Chip-Arena/commit/301be8f57cc104a473f41ed70be684c7f3c1e745) | Room directory/join flow, configurable draft choices and shared draft presentation. |
| 2026-09-27 | [3c3c2d8](https://github.com/sheldon37/Chip-Arena/commit/3c3c2d82f0252ee12560bf2a2b2017b2d28a5ebf) | Developer L33T H4X style, exclusive Chips, Matrix effects and throne intro. |
| 2026-09-27 | [64e2c10](https://github.com/sheldon37/Chip-Arena/commit/64e2c1092d33fa6402b82d489154290275a0f11a) | L33T/modifier test follow-up; no gameplay-source changes in this snapshot. |
| 2026-09-28 | [cabe74c](https://github.com/sheldon37/Chip-Arena/commit/cabe74c77a453ab5c3ea09410b806779af8f473f) | Accounts, profile/access rules and developer Ansatsuken combat/intro system. |
| 2026-09-28 | [71c25fa](https://github.com/sheldon37/Chip-Arena/commit/71c25facb4581f84f4e2bfc5ce4b0a2d53cd5eba) | Encrypted desktop account sessions, parry, hitstun and arcade audio follow-up. |
| 2026-09-28 | [abe819d](https://github.com/sheldon37/Chip-Arena/commit/abe819d13da27650148dfc61bcc7a8e72c231111) | Ansatsuken reference poses, exclusive rarity/presentation, grantable halo and Raging Demon effect. |
| 2026-09-28 | [a47eeba](https://github.com/sheldon37/Chip-Arena/commit/a47eeba959658bbfaf8231b55c5dbb5eece4c9a7) | Developer Emperor Style, exclusive Chips, electric attacks, intros and ragdolls. |
| 2026-09-28 | [07df3cf](https://github.com/sheldon37/Chip-Arena/commit/07df3cf8035ec22be891c8d9f803383b732f5bd3) | Demon Step/Raging Demon presentation, Emperor revisions and in-game desktop manual updater. |
| 2026-09-28 | [594083a](https://github.com/sheldon37/Chip-Arena/commit/594083ae1b1ca6db8ab12483899429c59243cc4f) | Direct bundled desktop startup, native LAN transport, maps/bots/scenery and map music. |
| 2026-09-28 | [dc53c15](https://github.com/sheldon37/Chip-Arena/commit/dc53c155a54d71138b8f75df23658e5f35b25515) | Test-results documentation refresh; no gameplay-source changes. |
| 2026-09-28 | [89f096c](https://github.com/sheldon37/Chip-Arena/commit/89f096cc2d32f73886fe128a1801b05b0cf62216) | Asset-credit documentation refresh; no gameplay-source changes. |
| 2026-09-28 | [a0f414b](https://github.com/sheldon37/Chip-Arena/commit/a0f414bbcb3d32dd0701d30c08aa353632b9924e) | Password/account adjustments, rotating arenas and set-score circles. |
| 2026-09-28 | [ba00ef7](https://github.com/sheldon37/Chip-Arena/commit/ba00ef7018540602bb90188a28ab25b3c0c1d6d5) | Set progression and score-display follow-up. |
| 2026-09-28 | [5e806a2](https://github.com/sheldon37/Chip-Arena/commit/5e806a2fa874e4699fed377608f54609d80c469a) | Audio adjustment. |
| 2026-09-28 | [e060e5a](https://github.com/sheldon37/Chip-Arena/commit/e060e5a01983078415f090c85a499feb922efbd4) | Host/client match and set-flow follow-up. |
| 2026-09-28 | [8d2b17b](https://github.com/sheldon37/Chip-Arena/commit/8d2b17bb6ef392336213969562ea01e2b25f7d42) | Audio/music/menu controls refinement. |
| 2026-09-28 | [3273752](https://github.com/sheldon37/Chip-Arena/commit/32737525726637f16b31e61204926f5ae611c670) | Match-score display and Emperor Chip tuning. |
| 2026-09-28 | [903542b](https://github.com/sheldon37/Chip-Arena/commit/903542bfc7a972b8387bf6fdb4e848c24ed55f0e) | Menu-music controls and regression checks. |
| 2026-09-28 | [218133a](https://github.com/sheldon37/Chip-Arena/commit/218133a71cee6b5839033777b375ab7c64864758) | Menu-music/UI follow-up. |
| 2026-09-28 | [e8d1974](https://github.com/sheldon37/Chip-Arena/commit/e8d19746c181a37779d3692cf656191ccbf18115) | Grounded recovery, combat/movement locking and Emperor/Ansatsuken follow-up. |
| 2026-09-28 | [1789e24](https://github.com/sheldon37/Chip-Arena/commit/1789e249f8efa2f72d47eceebb77648245fa5c96) | Vertical arenas and stage hazards with bot/network checks. |
| 2026-09-28 | [2b6b0e7](https://github.com/sheldon37/Chip-Arena/commit/2b6b0e7f1da0bd3d83fbbbdeac48610757187b26) | Night-world scenery and renderer refinement. |
| 2026-09-28 | [8d9ca40](https://github.com/sheldon37/Chip-Arena/commit/8d9ca40a4f583e188f1555481b8806fb9236e322) | Hazard interaction, movement, map and audiovisual follow-up. |
| 2026-09-28 | [24c0abe](https://github.com/sheldon37/Chip-Arena/commit/24c0abe00bbc615f9f66a44cac56848796431ce0) | Presentation overhaul: pose curves, melee/fighter motion, icons and ceiling-field visuals/rules. |
| 2026-09-28 | [142b885](https://github.com/sheldon37/Chip-Arena/commit/142b885927360b31c4be6d0589a637d70f08d819) | Fighter motion-quality refinement and rig regression coverage. |
| 2026-09-28 | [8b3fcc9](https://github.com/sheldon37/Chip-Arena/commit/8b3fcc9db5d31122e1b459d4816448f266794d93) | Lobby pose/portrait/UI presentation and fighter-animation refinement. |
| 2026-09-29 | [b177e07](https://github.com/sheldon37/Chip-Arena/commit/b177e07c13531f8a24379ad9703a2f847a48b9af) | Lobby/fighter/melee presentation follow-up. |
| 2026-09-29 | [d6df92d](https://github.com/sheldon37/Chip-Arena/commit/d6df92d2e38da7e467fe3eba9c4920a96279719b) | Lobby/cosmetic presentation follow-up. |
| 2026-09-29 | [97cd13a](https://github.com/sheldon37/Chip-Arena/commit/97cd13a529d08d677448f7d147169877b702e3ca) | Emperor prediction and Chip-behavior follow-up. |
| 2026-09-29 | [571b1c7](https://github.com/sheldon37/Chip-Arena/commit/571b1c74c712e0b27a0a923f226ad0b2f86ef63f) | Teams, combat numbers, score/UI changes and startup update-status check. |
| 2026-09-29 | [1f35258](https://github.com/sheldon37/Chip-Arena/commit/1f35258f53533f437bafb641a3f59212b9d274e3) | Interiors, ragdoll styles, overhead health, HUD and Hadoken visuals. |
| 2026-09-29 | [fefc7f9](https://github.com/sheldon37/Chip-Arena/commit/fefc7f9a6697ecd5b77457ec1272e8dee999b97a) | v55 notes: standard melee attacks/specials, death announcements and hit feedback. |
| 2026-09-29 | [c5da056](https://github.com/sheldon37/Chip-Arena/commit/c5da056140b3c1f0e31f3b0d857cd80ea65ac977) | v56 notes: full-body primary melee motion and sky portraits. |
| 2026-09-29 | [e09691b](https://github.com/sheldon37/Chip-Arena/commit/e09691bec97bb47537db92ccf6add7f7aaf13666) | v57 notes: weapon signature entrances, shield knockdown, greatsword direct-hit ragdoll and DEV Gibber. |
| 2026-09-29 | [addce60](https://github.com/sheldon37/Chip-Arena/commit/addce60b12d00e6d805acb4ba4badc8ae21793a1) | v58 notes: strength-dependent melee block stun and world-space death broadcasts. |
| 2026-09-29 | [3aff6e7](https://github.com/sheldon37/Chip-Arena/commit/3aff6e724dbaaca130285c4ccee12c74ff58ccf6) | v59 notes: committed melee, shared stamina, dodge rolls and roll Chips. |
| 2026-09-29 | [29f967e](https://github.com/sheldon37/Chip-Arena/commit/29f967ebd044231e16887407b2161e34df2c3744) | v60 notes: melee reference motion, directional/body-region reactions and dagger tuning. |
| 2026-09-29 | [08ec219](https://github.com/sheldon37/Chip-Arena/commit/08ec2194099de0474cec9f4bd9455c92bcfbecf8) | Further primary melee/full-body motion and distinct weapon blocking poses. |
| 2026-09-29 | [ebc1058](https://github.com/sheldon37/Chip-Arena/commit/ebc1058ded12861d9ea497b09998eaf93c170ac0) | Hit-reaction blending/motion-quality refinement. |
| 2026-09-29 | [c541c16](https://github.com/sheldon37/Chip-Arena/commit/c541c16331dfbb7fdd69fb05bf540b51ab0cc244) | Return-to-lobby countdown, intro override, team identities/intros and dead-player spectating. |
| 2026-09-29 | [1823169](https://github.com/sheldon37/Chip-Arena/commit/18231698c7c79a7a604e567b933eb2d0155d7a3e) | Restore Emperor E wavedash while retaining Shift roll; reduce team-label obstruction. |

## GitHub migration and release preparation

These entries describe branch work, not additional player releases. PR status
was checked on 2026-09-29; consult GitHub for later merge/release changes.

| Work | Evidence | Status at this handoff |
| --- | --- | --- |
| Preserve original history, shared instructions, pinned checks and development log | [PR #1](https://github.com/sheldon37/Chip-Arena/pull/1), docs/MIGRATION-REPORT.md | Draft, unmerged; original live services preserved |
| Fix server TypeScript/Rapier type resolution | [PR #2](https://github.com/sheldon37/Chip-Arena/pull/2) | Open, unmerged; excluded from Candidate 001 |
| Exact-commit builder and isolated updater fixture | [PR #3](https://github.com/sheldon37/Chip-Arena/pull/3), [CANDIDATE-001.md](CANDIDATE-001.md) | Draft, unpublished; baseline failures remain |
| Owner Windows validation | [CANDIDATE-001.md](CANDIDATE-001.md) | Owner reports update/restart, persistent settings, reopen, bots and current-version check passed; no complete multiplayer/account certification |
| Mixed-feed update-label fix and this history | [DEV_LOG.md](DEV_LOG.md) | Test-fixture changes on the release branch; no production publication |
| Complete visual revision: fluid animation, Souls-like melee, detailed voxel model, intros, UI | Branch `claude/visual-revision`, [VISUAL-REVISION-PLAN.md](VISUAL-REVISION-PLAN.md), [DEV_LOG.md](DEV_LOG.md) | Implemented on branch, PR open against migration/shared-development; not merged, not released |

## Visual revision (2026-09-30, Claude) — implemented on branch, not released

Game update source: branch `claude/visual-revision` (base `migration/shared-development`
dfb90c8); see DEV_LOG for the exact head commit. Presentation-only: hit timing, hitboxes,
damage, cooldowns, movement rules, networking, saves and access control are unchanged.

- Locomotion: planted feet (no skating), speed-matched cadence (~6.5 steps/s at a run,
  tunable `GAIT`), hip-led turns with turn steps, strafe/backpedal gaits, jump push-off and
  landing spring, class stances, breathing idle, blended dodge rolls.
- Reactions: continuous knockdown (fall, land, roll, rise), layered hit reactions that keep
  the weapon, hinge-limited ragdolls (comedic fling kept), rewindable kill cinematics.
- Melee: Dark Souls-like weight from the owner's reference GIFs (shield slam, sword combo
  with leaping overhead finisher, greatsword follow-through, low split-stance daggers),
  stable IK, guards and running carry, living block with recoil, swept trails, visual
  hit-stop and impact effects. DEV classes keep their fighting-game style.
- Ranged/magic: shouldered rifle/MG, isosceles pistol, reload choreography, spring recoil,
  casting phrases, continuous spell orb, improved first-person view model.
- Model: same blank-faced design rebuilt as detailed voxel armor in data-driven slots (for
  future options), hue-true brighter glow, voxel weapon detail; mesh count unchanged.
- Intros: hit-timed choreography, planted footwork, weighted acting per weapon, 4-shot
  cameras, living hero holds, staging. Durations/hit marks unchanged.
- UI: token/button pass, CSS-timed intro name card, HUD updates in place, overflow fixes.
- Tests (Linux, Node 22.22.2): full suite 523 tests, only the 13 pre-existing failures;
  typecheck passes. Not tested: native Windows, real GPU performance, online multiplayer.

## 2026-09-30 — DEV melee combo-ready parity and Demon Step invulnerability

- Source work: `codex/dev-melee-combo`, based on `claude/visual-revision` at
  `32422fbf0c96b13cf1007f892e5668748e84a1e7`; see DEV_LOG.md for the integration handoff.
- Emperor Style and Ansatsuken settle directly into the next combo-ready stance,
  share normal melee's 2.4 s hold, and release it on dodge/parry/hit/special or timeout.
- Ansatsuken Demon Step gains 0.20 s of invulnerability in the middle of its 0.30 s
  dash. Startup and recovery remain vulnerable; existing DoTs and forced arena damage
  remain effective. Damage timing and protection use the shared match authority.
- Owner-approved merge/publication on 2026-10-01: PR #6 merged into
  `claude/visual-revision` at `5ecc20f`; test content `0.2.298dbb325341`
  published in channel commit `2112c4a`. Existing testers press Update in-game.
  Full unit results: 545/557, the same 12 failures as Claude's base; no new failures.
  Native Windows visual/gameplay testing remains unperformed.

## 2026-10-01 — individual DEV primary combo steps

- Ansatsuken and Emperor no longer auto-complete their primary strings from one
  activation. Each activation commits one hit; stop after any hit or continue
  within 2.4 s. Holding follows standard melee input behavior. Stamina is per hit;
  full-string costs and per-hit damage are retained, including exclusive chips.
- Animation and authoritative snapshots select the individual fist/kick. Demon
  Step i-frames retained. Tests and publication status are in the latest DEV_LOG.
- PR #7 merged at `66451cd`; test content `0.2.f3f24aed59e8` published in channel
  commit `873e23f`. Existing installations use the in-game Update button.

## 2026-10-02 — chest-up chip draft and Siren minion design

- Chip draft: the picker is shown large and chest-up under the cards, ROUNDS-style.
  - Every Pilot uses the same calm pose; bosses (the Siren) keep their own pose and are still framed close.
  - The arm reaches toward the hovered card, then grabs and draws in the picked chip.
  - The cards no longer jump at the pick.
  - Presentation only.
- Siren minions: design phase. Procedural zombie Pilots (rot, black-flame burns, wounds, blank or cracked-head faces)
  with a full animation set and a design doc (docs/SIREN-MINIONS.md). They are not in matches yet; spawn logic, AI
  and friendliness are next.
- Source: `claude/visual-revision-draft-minions` (integration `27da229`). Tests: full suite only the 13 known
  Node 22 failures; typecheck passes. See DEV_LOG for merge and test-channel publication status. Native Windows
  review remains.

## Continuing with Claude or Astra

Read CLAUDE.md (which imports AGENTS.md), PROJECT_STATE, recent DEV_LOG entries and
this index. Inspect the actual affected source and relevant historical notes rather
than assuming an old screenshot or test count describes the current build.
The owner confirmed `claude/visual-revision` as the shared game/test-development
branch on 2026-10-01. Migration work remains on migration/shared-development until
PR #1 merges. Release tooling is on release/candidate-workflow (PR #3) and
claude/test-update-channel (PR #5); unmerged tooling is not automatically present
in another checkout. Fetch and read those refs explicitly when needed.

For each future player update, append a short entry with the exact source commit,
what changed, actual tests, known regressions and separately verified release status.
Use DEV_LOG for implementation handoffs. Do not poll PRs or start automatic AI
review/fix loops. Never include credentials, player saves or private account data.
