# Unity conversion — what still differs from the Electron game

Written 2026-10-08 by Claude against `claude/sleepy-ritchie-g8ruhv` @ `eb8f7f7` (PR #62),
for the owner's question "is there anything else we need to revamp to replicate
Electron's Chip Arena?". Source compared: the pinned original at `05950ea`.

**Method.** Read-only code audit in five areas: screens, gameplay, networking,
presentation and existing trackers. Each P1 claim was then re-checked by a reviewer
trying to refute it. Nothing was run in Unity; see *Not verified* at the end.
Items are split by the owner's role split in AGENTS.md. Mixed gaps appear twice,
once per owner, with cross-references (for example C1 needs A8).

**Current logic status (Codex/Astra, 2026-10-08):** PR [#66](https://github.com/sheldon37/Chip-Arena/pull/66), branch `codex/unity-parity-logic-current`, implements the fixes marked below; earlier code checkpoints `718cd4f`, `d80d974`, `c9888fc`. Replacement PR #66 carries that reviewed tree directly onto the current target in `9921c78`, avoiding the conflicts on superseded PR #65 without a merge or force push. Claude's presentation changes through `4207641` are preserved. These are **branch implementations, not Unity/Windows acceptance**. The original findings and line references are retained as audit evidence; they describe the pre-fix tree. The previous claim that all critical/high findings remain open is superseded by these statuses and the consolidated issue #60 comment.

Managed/core, real loopback UDP/TCP/WebSocket, Python and updater checks pass. Real JsonUtility and PhysX regression cases are wired into `NativeBuildChecks` but cannot run here. `cmd.exe /c Build-Windows.cmd` exits 127 (`cmd.exe: command not found`); there is no activated Unity Editor. No `UNITY_TEST_BUILD_READY` result exists for this branch. See [UNITY-LOGIC-HANDOFF.md](UNITY-LOGIC-HANDOFF.md) for tests, UI contracts, remaining gates and release boundaries.

Legend:
- `P1` means a player would notice a missing feature or a broken flow, `P2` a clear difference, `P3` polish.
- `S`/`M`/`L` is effort.
- `#N` is a finding in `docs/reviews/2026-10-08-unity-conversion-review.md` (mirrored in issue #60).
- Unity paths are relative to `unity/ChipArenaConversion/Assets/ChipArena`. Source paths are relative to `src/` (and `desktop/`) at `05950ea`.

**Not gaps.**
- The 90 s seat resume and the relay room list are Unity additions; keep them unless the owner says otherwise.
- Host migration, mid-match spectator join and the dev-only Run Input Test never shipped in Electron.

## Astra (logic and programming)

### Blockers

- **A1 — FIXED IN CODE, PR #66** (optional object presence; Unity acceptance pending). Original finding: P1/M partial: every guest is dropped on the first match frame. JsonUtility writes null nested fields (prop definition, death, fighter, down, pve) as default objects, which fail Valid(), and the client then disposes the connection. Runtime/NativeNetwork.cs:60,64,115; Runtime/NativeNetworkFrame.cs:40,61. Tracked #1/#3. Same root as A5.
- **A2 — FIXED IN CODE, PR #66** (worker transport; Unity acceptance pending). Original finding: P1/M partial: message rate is tied to frame rate. ReadLoop/WriteLoop resume on Unity's main thread (no ConfigureAwait or worker), while a guest sends 60 reliable inputs/s into a 32-message queue, so a guest below about 120 fps hits 'Reliable queue overflow'. Core/NativeTransport.cs:24-25,44; Runtime/ConversionGame.cs:138. Tracked #2/#16.
- **A3 — FIXED IN CODE, PR #66** (replica transforms, prediction and interpolation; Unity acceptance pending). Original finding: P1/L partial: the guest world is frozen and there is no prediction. The replica never steps physics, so everything that reads Transforms (camera at ConversionGame.cs:151, health bars, lock-on, summons) stays at spawn. Pilots lerp toward 20 Hz frames and minions snap. There is no client predictor or buffered interpolation (source client/prediction.ts Predictor, client/snapshots.ts SnapshotBuffer). Runtime/NativeNetworkFrame.cs:85,99,126-127. Tracked #11/#12/#17/#81.
- **A4 — FIXED IN CODE, PR #66** (frame/event limits and listed stall paths; Unity acceptance pending). Original finding: P1 (depends on load)/M partial: match frames have no size limit. About 190 KB of JSON goes out every 50 ms and is deflated separately per peer. Horde and field hits are not capped (one field over 10 adds is about 600 events per frame), so Ward Zero co-op can exceed the 8192-event limit in Valid() or the 2 MB Encode limit, and either one disconnects guests. Stall/ghost-match paths are also still open: a stale frame after Return to Lobby, the authority running on after a relay drop, and an uncaught InvalidDataException. Runtime/NativeArena.cs:96,259; Runtime/NativePveHorde.cs:97; Runtime/NativeChips.cs:121; source shared/pve/horde.ts:28 HORDE_EVENTS_PER_TICK=8. Tracked #14/#69/#85, #66/#67/#70/#124/#126-#130.
- **A5 — FIXED IN CODE, PR #66** (replay nulls and isolated physics cleanup; Unity acceptance pending). Original finding: P1/S partial: the death replay throws on every frame in any non-PvE match, offline Play vs Bots included. The JsonUtility round trip creates a default pve, so ConfigurePve throws. The killcam never shows, live renderers stay disabled, and a collidable arena copy is left in the live physics scene. Runtime/NativeReplay.cs:23,51; Runtime/NativeNetworkFrame.cs:80; Runtime/NativePve.cs:23. Tracked #10/#15/#71/#86.
- **A6 — FIXED IN CODE, PR #66** (managed P-256 and exception boundaries; Unity acceptance pending). Original finding: P1/M partial: sign-in and every DEV or granted unlock fail. Tokens are verified with ECDsa P-256 under Mono (Core/NativeAccess.cs:27, catch list :31; Editor/PrototypeBuild.cs:99 forces Mono), so the DEV Menu, Sandbox, Emperor/Ansatsuken/Leet/Siren and granted cosmetics never unlock, and the host strips them from guests. The host loop catches only ArgumentException/InvalidOperationException (Runtime/NativeNetwork.cs:57,86,100), so a crafted token can stall a host. Fix: a managed P-256 verifier, a catch-all in Verify and a per-peer catch. Tracked #4/#9.
- **A7 — OPEN / GATED** (owner decisions 1–2: endpoints and hosting). Original finding: P1/L missing: no online services work out of the box. accountOrigin and relayOrigin default to empty (Runtime/ConversionGame.cs:10), so players must type URLs (Runtime/NativeProfileUI.cs:71; Runtime/NativeNetwork.cs:126,142). No public native relay is deployed (Relay/README.md). The relay limiter is keyed on the socket peer IP with no forwarded headers and no per-creator room cap (Relay/Program.cs:11,16; source relay worker.ts allows 12 rooms per CF-Connecting-IP). Electron has both built in: desktop/accounts.cjs:4 AUTH_ORIGIN, and desktop/main.cjs hostRoom(online) with cloudflared and the Site directory. Needs an owner hosting decision. Tracked only as a CONVERSION-STATUS boundary note; the relay limits are #5/#13/#65.
- **A8 — PARTIAL / HANDOFF, PR #66** (LAN addresses, code-only join and LAN invite implemented; internet directory/invites await decisions 1–2). Original finding: P1/S-M different: joining and hosting need a typed IP and port. The LAN host is told guests need 'this computer's address' but is never shown it, and there is no code-only join, CA- code lookup through a directory, or invite URL. Showing the LAN IPv4 and accepting a code-only join are S; directory codes and invite links are M and depend on the hosting decision. Runtime/NativeNetwork.cs:125,131-147,153-154 (the only address lookup is in the unused lab ArenaGame.cs:61); source desktop/join.cjs resolveJoin, client/main.ts:141,185-186, shared/invite-url.ts. New.
- **A9 — FIXED IN CODE, PR #66** (lobby data, readiness, teams and auto-start; Unity acceptance pending). Original finding: P1/M partial: broken lobby flows. (1) Online teams: a guest's team is seat id-1, so a second guest becomes a third team, and the host handles 'team' messages but no client ever sends one (Runtime/NativeNetwork.cs:89,102). (2) A guest's weapon change reaches the host only through Send Loadout; Ready sends only the flag (Runtime/NativeHomeMenu.cs:420-422; Runtime/ConversionGame.cs:247), so a guest who readies without sending plays the old weapon. (3) LobbyBroadcast omits bots and room rules (Runtime/NativeNetwork.cs:106-107). (4) The match does not start on its own when everyone is ready (source shared/simulation.ts:153 setReady). Tracked #39/#101; the rest is new.
- **A10 — FIXED IN CODE, PR #66** (combat rules; Unity acceptance pending). Original finding: P1/S-M different: combat rules differ in every match.
   - A 0.25 s stun on every direct gun, magic or ability hit (Runtime/NativeArena.cs:183).
   - Block negates all ability damage (:176).
   - Q/E presses during a reload or a slow fire interval are dropped once the 0.3 s buffer expires (Runtime/NativeAbilities.cs:42,52).
   - Roll i-frames stop only direct hits (:175).
   - The headshot zone starts 0.4 m above body centre and includes the shoulders (Runtime/NativeProjectiles.cs:122 vs shared/headshots.ts).
   - Melee needs a fresh click for every swing (Runtime/ConversionGame.cs:91 vs held attack in client/main.ts:338).
   - Also different: Black Hole pull, the parry cooldown, Siren riposte, ability shots against props, and the trusted AbilityTarget.
Tracked #6/#7/#8/#40/#44/#47/#48/#49/#51/#54/#55/#58.
- **A11 — FIXED IN CODE, PR #66** (difficulty, reaction/aim and kit bot logic; Unity acceptance pending). Original finding: P1/L different: the bot AI is a crude rewrite. Bots aim at the target's exact centre with no reaction delay, jitter or lead, use one 12 m standoff, cast on frame-modulo ticks, and never roll or use kit moves (Runtime/NativeArena.cs:238-243). There are no dummy/easy/normal/hard levels (grep -i difficulty finds nothing). The Siren boss casts the first ready slot in fixed order and does not scale with party size (Runtime/NativePveHorde.cs:130). Source: shared/simulation.ts:131,433-444, shared/bot-abilities.ts, shared/pve/balance.ts:69. Tracked #46/#121; the boss difficulty part is new.
- **A12 — PARTIAL: WINDOWS BUILD/PACKAGING PASS; PLAYTEST OPEN** (owner's `build(4).log`, Unity 6000.3.24f1, 86 PASS lines, 15 maps / 120 spawn floors, both packaging markers and exit 0; reported source `5900583` plus PR #67 v3 / `9903a66`). Candidate identity/hash validation passes (version `83e6a566`, revision `639271006350008324`); rendered gameplay and the host/join, sign-in, rounds/sets/draft/rematch and resolution matrix remain. Cloud build #2 also confirms `8fd2f78` (shared `2604b2a` plus PR #68) compiles and passes 86 Unity checks; build #3 also passes but final cloud delivery remains blocked by Python project-root lookup (repair on PR #68). The log receipt and exact next steps are in DEV_LOG. The following original finding is retained as pre-fix audit evidence, including its historical claims about build status. Original finding: P1/L acceptance gate: the head this list was written against (eb8f7f7, which includes the new menu) has not been imported, built or played in Unity. The owner reported a Unity build on 2026-10-08, but which commit it came from, and whether launcher packaging passed, is not recorded. The earlier recorded failure (CS1504 at launcher packaging) has a fix in the tree (Editor/NativeLauncherCompiler.cs response file, VALIDATION.md:258-267) that no build has confirmed yet. UNITY_TEST_BUILD_READY has never been recorded, and CI does not build Unity. Next: build this head with Build-Windows.cmd and record the result; the owner then runs the Windows playtest matrix (host+join, sign-in, rounds, sets, draft, rematch, four resolutions). Evidence: VALIDATION.md:3,15 and 'Remaining acceptance'; CONVERSION-STATUS 'Acceptance status'. Tracked #114.
- **A13 — DELIVERY WORKING: OWNER REPORTS SUCCESSFUL UPDATE; EXTENDED ACCEPTANCE OPEN** (owner approved the validated candidate; version `83e6a566`, revision `639271006350008324`, channel commit `c5251fb`). Public manifest and all 12 parts were downloaded and the reassembled archive passed full checksum/CRC/inventory/identity checks. At 20:21 EDT the owner reported the older installation successfully updated through this feed. Installed identity, settings retention, repeat-update and rollback are not independently verified; cloud builds #2/#3 pass the Unity export; build #3 confirms the project-relative script setting but final delivery fails Python project-root lookup. PR #68 repairs path handling; another real cloud run must verify delivery (see UNITY-CLOUD-BUILD-HANDOFF.md). PRs #63/#64/#67 are not merged by this publication. The following original finding is retained as pre-fix audit evidence, including its historical 404 claim. Original finding: P2 as a feature but required before replacing Electron, M missing: native update delivery. The feed .../native/windows-x64/latest.json returns 404 (Core/NativeUpdateRules.cs:54). The publish and cloud-build PRs #63/#64 are unmerged and have never run. No Unity-to-Unity update has been tested. Electron's Update Game cannot hand testers the Unity build, so the starter ZIP must be installed by hand. Tracked in docs/UNITY-NATIVE-UPDATE-HANDOFF.md and DEV_LOG 2026-10-08.

### Parity

- **A14 — FIXED IN CODE, PR #66** (room setup options/defaults and bot seats; Unity acceptance pending). Original finding: P2/M missing (re-rated from P1): match setup logic.
   - No random arena or 'Randomize map after each set' (shared/simulation.ts:123,484; NativeArena.MapId is readonly per match).
   - No Skip player intros: ShowIntros=true is hard-coded and every bot intro is queued (Runtime/ConversionGame.cs:59).
   - Rounds are capped at 5 instead of 1-10 (:235).
   - Defaults differ: 3 sets, 2 rounds, skip bot drafts on, foundry, versus the source's 5 sets, skip bot drafts off, moonlit.
   - Bots have no per-bot weapon or team, and only red/blue teams exist instead of 8.
   - Mixed bots never get a melee weapon, and all bots are generic 'Pilot N' with one look (Runtime/ConversionGame.cs:63 vs simulation.ts:133-134).
Tracked #36/#39/#42/#64/#108.
- **A15 — FIXED IN CODE, PR #66** (overtime and fresh match/run seeds; Unity acceptance pending). Original finding: P2/S missing: rule gaps. There is no 100 s overtime (18 HP/s; source shared/simulation.ts:522), so stalemates never end; maxRoundSeconds is exported but unused. Every match and rematch deals the same Chip offers, because the draft seed is 12345 (Core/NativeMatch.cs:39) and no caller passes one. Tracked #36/#41/#50.
- **A16 — FIXED IN CODE, PR #66** (targeting separated from damage permission; Unity acceptance pending). Original finding: P2/S different: with Teams and Friendly fire on, bots and homing shots target teammates. Unity has a single Enemy() predicate that counts allies as enemies when friendly fire is on (Runtime/NativeArena.cs:171, used by Bot() at :240 and by homing in NativeProjectiles.cs:72). The source keeps targeting (enemy) and damage (canHit) separate (shared/simulation.ts:147-148). New.
- **A17 — FIXED IN CODE, PR #66** (pilot/profile preservation and reload hook; Unity acceptance pending). Original finding: P2/S partial: saving a pilot from Unity damages the shared account. Profile() omits lobbyPose, so a save writes 'weapon' over the account's chosen pose (Runtime/NativeNetwork.cs:25; NativeAccount.cs:11). Sign-in replaces the local pilot even when the account has no saved profile (hasProfile, #18). There is no pending kill-effect sync and no manual reload (NativeAccount.cs:51). #18 tracked; the rest is new.
- **A18 — OPEN / GATED** (owner decision 5: specialization phase versus menu selection). Original finding: P2/M missing: the specialization phase. In the source, Start opens a timed 'specialization' phase where each pilot picks a weapon, the Siren shows '· TAKEN', then 'Selection locked' (shared/simulation.ts:154). Unity takes the weapon from the menu and goes straight to intros; grep -i speciali over Assets/ChipArena finds nothing. New. Needs an owner decision.
- **A19 — FIXED IN CODE, PR #66** (votes, cancellable return and guest exit; Unity acceptance pending). Original finding: P2/M partial: end-of-match flow.
   - Rematch has no vote count and no host-lost 'Return to Main Menu' variant (Runtime/NativeMatchOverlays.cs:137-151).
   - Return to Lobby is immediate, with no 3 s 'Returning to lobby in N…' and Cancel (source shared/simulation.ts:87,194; client/main.ts:363).
   - Online guests cannot press Return to Menu on the PvE results (NativeMatchOverlays.cs:176 'Hosting||!Online').
   - There is no PvE retry vote ('Waiting for N more…').
Tracked #101 and DEV_LOG 2026-10-08; the guest button is new.
- **A20 — FIXED IN CODE, PR #66** (Ward Zero rules and navigation allocation budget; Unity acceptance pending). Original finding: P2/M different: Ward Zero rules.
   - Every revive, paid or self, teleports the pilot to the nearest pad instead of where they fell (Runtime/NativePve.cs:69,78-82,127).
   - 'Find me' possession auto-ends after 60 s and picks the oldest add.
   - The Siren has line of sight through adds.
   - Medic heals one target, and knockback is doubled.
   - A DEV Siren can be picked in co-op.
   - Clearing a wave removes adds without deaths (:136).
   - Self-revive works inside a vent shaft.
   - A boss below y=-6 is clamped instead of pushed off.
Tracked #72-#80, #131-#141; only #72 was re-checked at HEAD.
- **A21 — FIXED IN CODE, PR #66** (event model and gameplay emission; Unity acceptance pending). Original finding: P2/M missing: the event model is too small. Core/ArenaModel.cs:89 EventKind has only 10 kinds, so explosions, blockPulse, vacuum, arc, freeze, teleport, critical/catch/split/ricochet/Pong, ceiling-hit, hazard hits, burn/poison ticks, Leet/Emperor events and pve buy/gib/phase never reach presentation or audio. The source handles about 60 event types (client/event-vfx.ts, client/audio.ts). This must come before the blast VFX and the missing sounds. #61 (sound) tracked; the visual need is new. Unblocks C6 and C8.
- **A22 — PARTIAL / HANDOFF, PR #66** (sandbox logic/control/preview hooks implemented; Claude owns their UI and visual tests; decision 8 still applies to visual scope). Original finding: P2/L partial: Developer Sandbox logic. Unity's SandboxBody (Runtime/NativeSandbox.cs:46-56) is missing:
   - a weapon or map switch during the session
   - opponent difficulty
   - Arena Reset and Test Block
   - draft, rarity, knockout and my-death previews
   - the debris launcher (mass, speed, material, impact type)
   - a dummy that casts the selected Chip, or every Chip
   - splat delay and time scale
Source: client/main.ts:248 drawSandbox; sandbox-panel.ts:34-38; shared/simulation.ts:528-536. New; #88/#134 and #20/#143 are tracked.
- **A23 — FIXED IN CODE, PR #66** (collision carry, player ceiling and collapse events; Unity acceptance pending). Original finding: P2/M different: physics. Overwriting velocity every tick erases debris and body pushes (Runtime/NativeArena.cs:164). Pilots hit an invisible ceiling at 19 m, which the source does not have (:165). Debris deals damage outside the fight phase (NativeDestruction AfterPhysics). Support collapse emits no 'collapse' event. Tracked #52/#111/#112; the collapse event is new.
- **A24 — PARTIAL / HANDOFF, PR #66** (PvE bot rarity ties and authoritative pick source fixed; PvP first-offer choice already matched source; Siren intro duration awaits decision 7). Original finding: P2/S different: match-flow timing. The PvP Siren intro lasts 9.07 s instead of the source's 7.27 s (Core/NativeIntroRules.cs:12 vs siren-contract.ts:165). The bot draft tie-break differs, and the PvE pick label is inferred on the client. Listed under 'For Astra' in the DEV_LOG 2026-10-08 Claude entry.
- **A25 — OPEN / GATED** (owner decision 9: dedicated room ownership/topology). Original finding: P2/M different: room control. A Unity dedicated server is one room fixed by --ca-map/--ca-pve/--ca-bots (Runtime/NativeDedicated.cs). There are no owner 'settings', 'lobby' or 'cancel-lobby' messages, ownership does not pass to another player on disconnect, and there is no multi-room support or /health endpoint (source src/server/index.ts:58,64). New. Needs an owner decision.
- **A26 — OPEN / GATED** (owner decision 3: Electron migration/cutover). Original finding: P2/M missing: no path for existing Electron players. Electron preferences ('chip-arena-preferences', client/main.ts:88-91) and the signed-in session (account-session.dat) are not imported into native-preferences-v1.json or conversion-account.bin. Unity rooms use protocol 3 with 'CAU' framing (Core/NativeTransport.cs), so they cannot join Electron rooms. Tracked VALIDATION.md:170-173 and a CONVERSION-STATUS boundary note.

### Polish

- **A27 — FIXED IN CODE, PR #66** (measured RTT/gaps/bytes and JSON report; Unity acceptance pending). Original finding: P3/M missing: connection diagnostics. ping is only a keepalive, with no pong or RTT. There are no connection metrics (RTT p95, snapshot gap, decoded KiB) and no JSON connection report data (source client/connection-metrics.ts; client/main.ts:183; server pong). New.
- **A28 — FIXED IN CODE, PR #66** (ability ceiling; standard/throw ceilings already existed; Unity acceptance pending). Original finding: P3/S partial: the 24 m projectile ceiling that bounces or absorbs shots applies only to Pong shots (Runtime/NativeProjectiles.cs:83). The source applies it to all shots (shared/ceiling-field.ts; simulation.ts:397-402; ability-system.ts:221; standard-melee.ts:108), so ricochet builds and lofted shots behave differently. New.
- **A29 — FIXED IN CODE, PR #66** (listed Chip handler and ledger gaps; Unity acceptance pending). Original finding: P3/M partial: several Chip effects are partial even though 0 of 236 show as pending (that check covers only mechanic ids and stats, NativeArena.cs:57). Affected: coin-toss, swap-shop, sticky-bomb against summons, vampire-dash, trampoline, storm-recall, L44T yeet on cover, Late Bloomer+Splitter, Return Address, once-per-round ledger eviction, and ammo lost at the projectile cap (Runtime/NativeChips.cs:58-102). Tracked #53/#102-#106.
- **A30 — FIXED IN CODE, PR #66** (player process lock and fullscreen hook; Unity acceptance pending). Original finding: P3/S missing: desktop shell. There is no single-instance lock, so a second launch shares the preferences file and port 29941. There is no fullscreen setting beyond Alt+Enter (Editor/PrototypeBuild.cs:45 vs desktop/main.cjs:36,47). New.
- **A31 — FIXED IN CODE, PR #66** (trackers and issue handoff; Unity acceptance pending). Original finding: P3/S different: the trackers overstate status. CONVERSION-STATUS line 4 names codex/unity-conversion, and its Multiplayer and Accounts rows claim working bounded frames and signed unlocks, which #1-#4 contradict. VALIDATION still says 'New multiplayer is not implemented yet'. Issue #60 has no comments saying which findings are fixed. Tracked #118.

## Claude (visuals)

**Progress on `claude/sleepy-ritchie-g8ruhv` (updated 2026-10-08).** Implemented on the branch (not merged to a
release, not verified in Unity or on Windows; checks are the Unity API compile, the Python and managed checks and
offline menu renders):
- C2 room lobby: pilot cards, ready meter and bar, room column in the source order with selects and Advanced
  Options, Siren face, source panel shell (`ea8981b`, `5c75ed4`, `77e320d`).
- C4 class row and weapon cards in Play vs Bots, Test Boss, the lobby and Customize (`81939f9`).
- C5 PvE results member cards and the match-end panel (`87f093d`, `6ece053`).
- C15/C21 account screen, toasts, M mute key, Test Sound, display names, colour presets (`469db9a`, `c67273f`).
- C7 combat numbers for every pilot (`2517303`); C22 source camera shake (`b75d3c4`).
- C20 lobby connection pill (`4207641`); the "· N MS" reading and the Connection report wait on A27.
- C24 ANIMATIONS.md corrected (`3da391d`); the note on issue #60 is not posted yet.
- Kit: deferred kit drawing in scrolled bodies (`662c9d3`), translucent borders as rings (`77e320d`).

In progress: C6 blasts (`claude/wip-c6`, under review); C7 status/team visuals and first-person sway; C17 gore and
beacons. C9 telegraphs and C10 projectiles follow C6 (they share its voxel pools); C11 and C12 have specs. C1 and C3
wait on PR #65 (room options, invite URL, LAN addresses).


### Blockers

- **C1** P1/S different (visual half of the join flow): the host lobby should show the LAN address and room code, the lobby needs a Copy Invite Link button and invite field, and the Join field should accept 'CA-ABCD-2345 or room invite link' (source client/main.ts:155-156), once A8 exposes them. Unity currently offers only Copy Code (Runtime/NativeNetwork.cs:153-154). New.

### Parity

- **C2** P2/M partial: the room lobby is drawn as text rows (Runtime/NativeNetwork.cs:149-166). The source has a pilot card per pilot with a BOT/YOU/ROOM OWNER tag and a Ready pill, a '2/3 READY' meter with 'Waiting for N pilots', an empty 'Waiting for a pilot' card, team pickers, read-only rules for guests, an Advanced Options fold, and in PvE a party roster with the Siren card (client/lobby-ui.ts:8, team-ui.ts:5, room-options.ts, pve/menu.ts pveLobbyBody). Depends on A9. Team picker tracked as #39; the rest is new.
- **C3** P2/M missing: Play vs Bots and Create options UI. Missing: a BOT DIFFICULTY selector, a Random arena choice, per-bot weapon ('Random weapon') and team selects with Start disabled when everyone is on one team, a Skip player intros toggle, and rounds 1-10. Also missing at P3: the map description line, because ContentMap.tag is exported but MapGrid never reads it (Runtime/ConversionGame.cs:227-254). Source: client/main.ts:121,136,146; lobby-ui.ts:12; team-ui.ts:6. Needs A11 and A14 first.
- **C4** P2/M missing: class and specialization presentation. Missing: the YOUR CLASS cards (Ranged/Magic/Melee/DEV with taglines, client/main.ts:119), and the 'Choose your specialization' screen with weapon chip cards showing a description, the 'aim higher' hint and DMG · /SEC · cost · range (main.ts:157). Unity's WeaponGrid is a grid of names only (Runtime/ConversionGame.cs:249). New. Needs A18 (or owner decision 5).
- **C5** P2/M partial: PvE results content. Missing: a portrait card per member, installed chip badges with rarity and ×count, total damage with '(n heads)', and 'The Siren kept X% health' or 'The party fell on wave N of M' (source client/pve/results.ts pveResults). Unity's PveResultsPanel shows a weapon and chip-count line plus six stats (Runtime/NativeMatchOverlays.cs:154-177). New.
- **C6** P2/L partial (re-rated from P1): event-driven combat VFX. On `claude/wip-c6` (not merged), the source's volumetric blasts (client/chip-blasts.ts: 11 styles sized to the gameplay radius, impacts, field emitters, pooled lights) run in Core/NativeBlastSim.cs. They match the source voxel for voxel (Tests/BlastChecks.cs). Runtime/NativeBlastPresentation.cs draws them on host and guests from the existing ability/chip effects, zones, chip fields, ability shots and surface impacts, replacing the two flat 0.22 s halos. The Siren's own attack records are excluded and keep the halos (see C25). Still open: a blast's style and tint are exact only when a zone, field, shot or teleport identifies it; other chip blasts use the owner's school. Every other event still uses the same 12- or 24-cube burst (Runtime/ArenaPresentation.cs:180). Both need A21. The owner asked on 2026-10-03 that explosions read as real explosions.
- **C7** P2/L partial: feedback visuals that are still open.
   - Muzzle flash: ported with C11 (Runtime/NativeWeaponPresentation.cs). Slide recoil: the source never moves the pistol slide, because its voxel part has no name and ranged-motion.ts:222 finds nothing. The whole gun kicks with the hand, which RangedPose already ports. GunPresentation's slide kick has no source counterpart.
   - Burn, poison, slow, shackle and Orbital Mark rigs and team bands are ported on `claude/wip-c7-status` (Runtime/NativeStatusPresentation.cs; parity in Tests/status-parity.json). Guests see only the bands: DoTs, slows and the mark's target/lock are host-only (Astra).
   - Combat numbers show only your own hits.
   - First person has no sway, bob or recoil. Implemented on `claude/wip-c7-fp-sway` (the first-person-view.ts view layer); not playtested.
Tracked #30/#62/#63/#122/#94 (also #29, #23/#24).
- **C8** P2/M partial: audio.
   - Stereo panning is reversed (#31).
   - Many events are silent (#61; needs the event-model work).
   - No master limiter, pitch variation, music ducking, PvE intro dip or clown laugh (Runtime/NativeAudio.cs:63-73 vs client/audio.ts and music.ts). The silent events need A21.
Tracked #31/#60/#61/#97/#142; the mix items are new.
- **C9** P2/M different: field telegraphs are outlined dashed rings in kind colours with no ally/enemy tint (Runtime/NativeWorldEffects.cs:13-19; NativeAbilityPresentation.cs:30-57). The source uses soft ground glows (ally 0x55dcff, enemy 0xff5a46), domes and voxel cores (client/chaos-visuals.ts; owner 2026-10-04: 'areas are no longer outlined'). New.
- **C10** P2/M partial: projectile and ability-shot models are generic. Ranged shots are thin boxes, magic shots are spinning cubes, and every ability shot is a cube (Runtime/ArenaPresentation.cs:142-146; NativeAbilityPresentation.cs:10-15). The source has per-spec meshes, including a frag grenade, a finned rocket, a tidal wave, a bowling ball and element orbs (client/renderer.ts abilityGeometry). New.
- **C11** P2/M partial: weapon presentation. On `claude/wip-c11` (not merged), Core/NativeWeaponDressing.cs ports the swept melee ribbons (client/melee-trails.ts), the chip weapon modules, the reach stretch and the fire and poison blade enchants, also on thrown daggers and shields (client/weapon-visuals.ts weaponModifiers/enchantWeapon). They match the source (Tests/WeaponPresentationChecks.cs). Runtime/NativeWeaponPresentation.cs draws them on host and guests, in third and first person. Still open: only the first punch of an Emperor or Ansatsuken action sweeps, because the actor has no per-hit timestamp (Astra). The Siren's empty weapon socket stays bare. #98 covers only reach.
- **C12** P2/L missing: DEV fighter and Siren effects. Missing: Emperor electric ribbons, Leet glyph trails and teleport columns, the hadouken dome, Demon Step afterimages, Siren claw slashes and black flames, and the Siren's first-person claw view model (client/emperor-effects.ts, matrix-effects.ts, hadouken-visual.ts, demon-afterimages.ts, siren/fx.ts, siren/flames.ts, siren/first-person.ts). Unity uses the generic magic cube and Ring/Beam primitives (ArenaPresentation.cs:145; NativeWorldEffects.cs:21-34; NativeViewPresentation.cs:51-84). New; #35 covers the Leet cast animation.
- **C13** P2/L missing: Developer Sandbox visual tools. Missing: the animation inspector (idle, walk, run, attack, wind-up, release, dagger strikes, reload, block, airborne), the 14 animation tests (holographic revive, killcam, weapon intro, greatsword mocap, stagger, summon attacks and deaths, Slenderman, …), the colliders/supports view and the kill-effect preview (client/sandbox-panel.ts:34). New.
- **C14** P2/L missing: live 3D pilot portraits on the menu, lobby and results cards, the lobby idle poses (weapon, chair, sleep, crossed) with an IDLE ANIMATION picker in Customize, and the animated Auditor halo (client/menu-portraits.ts:26, lobby-poses.ts, auditor-halo.ts, main.ts:131,165). Unity has a 2D emblem card (Runtime/NativeHomeMenu.cs:456-484; documented substitution) and a static halo (NativeSourcePresentation.cs:18). New.
- **C15** P2/S partial: the Account screen has no 'Reload saved pilot' button, no list of unlocked items (GRANTABLE_CONTENT names), no account flags and no Copy button for the recovery code (source client/main.ts:151, shared/access.ts:2-8). Unity: Runtime/NativeProfileUI.cs:65-70; NativeHomeMenu.cs:442-444. New. Reload needs A17.
- **C16** P2/M missing: private cinematic cameras. There is no Raging Demon victim screen (client/demon-cinematic.ts) and no Siren 'Last Laugh' grab cutscene (client/siren/cutscene.ts). ConversionGame.UpdateCamera (:149-164) only switches first person, third person and ADS. The in-world scenes that spectators see are baked and match. New.
- **C17** P2/M partial: Ward Zero gore and beacons. On `claude/wip-c17` (not merged), a severed horde limb is thrown as the source's tumbling chunk with an ink spray, and hits on adds splash ink (Core/NativeGoreRules.cs from client/pve/gore.ts; Runtime/NativeGorePresentation.cs). Downed party members get the revive column with rising motes, the self-revive progress ring, the 'F · REVIVE' / '$price' or 'SELF-REVIVE 0:04' label and the HELP bubble (Core/NativeBeaconRules.cs from client/pve/beacons.ts and downed.ts; Runtime/NativeBeaconPresentation.cs, NativeBeaconHud.cs). Tests/GoreBeaconChecks.cs compares them with the source. Still open: the black-flame puffs and stump drips (the siren/flames.ts FlameField, with C12). Hit events carry no hit direction yet, so chip-field, zone and burn/poison ticks still splash ink (at most every .25 s per add) and a bullet sever on an add in a field can be aimed from the field owner (Astra: fill ArenaEvent.Normal in HitPveAdd; DEV_LOG C17). A guest's revive label always reads $150 until the revived count is replicated (Astra; see DEV_LOG 2026-10-08 C17).
- **C18** P2/M partial: ragdoll kill effects keep the source forces but approximate the visuals. Unity draws generic cube clouds instead of point swarms, rings, body recolouring and the singularity core. voxel-burst is 60 generic cubes, the gibber has 55 drops instead of 150, and the stump cap is a 3x3 grid of cubes (Runtime/NativeDeathPresentation.cs:60-113; NativeWoundPresentation.cs:29-31 vs client/ragdoll-vfx.ts, death-effects.ts, gibber-vfx.ts). New.
- **C19** P2/M different: there is no ACES filmic tone mapping (exposure 0.92). The built-in pipeline has no post-processing stack, and NativeSourceLit.shader is a plain Lambert shader (vs client/renderer.ts:75). How large the difference is needs a side-by-side render. New.
- **C25** P2/M missing: Siren attack impacts. The source sends siren-slam, siren-eruption, siren-drop, siren-explode, siren-seeker-hit and the other siren-* events, and client/siren/fx.ts draws them as black fire bursts, shock rings and ground cracks. Unity records them as untagged darkness effects (Runtime/NativeSiren.cs SirenArea and StepSirenShot, NativeSirenStage.cs vents). Since the C6 review fixes (`claude/wip-c6-fixes`), Runtime/NativeBlastPresentation.cs finds them by position (`NativeBlastFrame.AtSiren`) and keeps the old flat halos, instead of drawing chip blasts the source never draws. To do: port siren/fx.ts. That needs the owner and the siren-* type on the effect record (A21). C12 covers the claw slashes and flames. New.

### Polish

- **C20** P3/S missing: the Connection report dialog with Copy (offered in Settings, the lobby and the pause menu) and the top-bar connection light with room code and '· N MS' (source client/main.ts:116,156,363). The pause menu shows only a local diagnostic line (Runtime/NativeHomeMenu.cs:489-516). New. Needs A27.
- **C21** P3/S different: menu details.
   - No toast notices (client/main.ts:117 notice(), about 40 messages).
   - No Test Sound button and no M mute key.
   - The Siren mode key is shown to everyone instead of only with Siren access.
   - The intro preview lacks the name card and 'Back to customization', because IntroOverlay requires previewArena==null (Runtime/ConversionGame.cs:196).
   - Hats, kill effects, intros and DEV toggles show raw ids (Runtime/NativeProfileUI.cs:75; NativeAdmin.cs:31,42).
   - Colours are #rrggbb text fields instead of colour pickers (NativeProfileUI.cs:42).
Mostly new; #90 is adjacent.
- **C22** P3/S different: camera shake fires only on Hit/Headshot against the local pilot and uses a sinusoid curve (Runtime/ConversionGame.cs:112). The source also shakes on impact, break, debris, parry, block, reflect and collapse, with random jitter (client/event-vfx.ts). New.
- **C23** P3/L different: CSS effects are approximated in IMGUI (backdrop and filter blur, clip-path shards, CSS 3D card rotations). The SVG icons are already rasterised. Tracked in CONVERSION-STATUS 'Native adapters and boundaries'; owner to accept or reject.
- **C24** P3/S different: ANIMATIONS.md 'Fidelity limits' still says blade clearance and the wrist limiter are not ported, which commit 1c0f257 superseded. Record the visual fixes #21/#22/#28/#32/#43/#144 (and #25, if confirmed) on issue #60.

## Owner decisions

1. Default endpoints: whether to bundle the Site account origin (https://chip-arena-prototype.sheldon37.chatgpt.site, as Electron does in desktop/accounts.cjs and dev-auth.ts) and the room directory into the Unity client. CONVERSION-STATUS currently says no production endpoints are bundled, so every player must type the URL.
2. Internet hosting model: either authorise deploying the native .NET relay (infrastructure; fix the forwarded-header limiter and add per-creator room caps first, #5/#13/#65), or port Electron's zero-setup model (bundled cloudflared tunnel plus Site directory CA- codes and invite links) to Unity. Both are owner-gated infrastructure.
3. Cutover from Electron: Unity protocol 5 cannot join Electron rooms. Decide whether testers switch all at once or run both games in parallel, and whether to import Electron preferences and the session or accept that testers re-enter their pilot and sign in again. The Electron session is encrypted with safeStorage, so migrating it may not be practical.
4. Update delivery: approve merging PRs #63/#64 and the first native test-channel publication after an accepted Windows build. Decide how Electron testers get the Unity starter ZIP (a manual install, or an Electron update that points to it).
5. Specialization step: restore the source's timed in-match 'Choose your specialization' phase with the Siren TAKEN rule, or accept choosing the weapon in the menu.
6. Accept or reject the documented approximations:
   - the 2D pilot card instead of live 3D portraits
   - IMGUI instead of CSS blur, clip-path shards and 3D card turns
   - ragdoll and voxel particles that are not pixel-identical
   - PhysX contact feel instead of Rapier
   - the captured shield guard pose (avoids a 16 cm hip pop)
   - one contact per lava channel (a deliberate fix of a duplicate contact in the source)
7. Canonical Siren PvP intro length: the current 9.07 s, or the source's 7.27 s.
8. Developer Sandbox scope: port all 14 animation tests, the previews and the debris launcher, or name a subset.
9. Dedicated servers: whether owner-controlled, multi-room dedicated authority (Electron's server) is required, or one room per process is acceptable.
10. Keep the Unity-only additions as intended behaviour: the 90 s seat resume on reconnect and the relay room list.
11. Tone mapping: adopt ACES to match Electron (this changes the look of every scene). Best decided after a side-by-side render.
12. Sequencing under the role split: Astra's event-model extension has to land before Claude can add the blast VFX, and before the missing sounds (#61) can be added. Confirm priority relative to the networking blockers.

## Original audit verification limits (pre-PR #66)

- Nothing was run. There was no Unity import, compile, render, play, build or update on eb8f7f7, and whether this head compiles is unknown. The last recorded build, on the updater branch, failed at launcher packaging (CS1504).
- The first-frame disconnect and the killcam throw rest on documented Unity JsonUtility behaviour (null custom-class fields serialise as default instances). The managed tests use System.Text.Json, which hides the problem.
- The Mono ECDsa failure mode (NotImplementedException vs PlatformNotSupportedException) is inferred from Mono's stubs; the review marks #9 PLAUSIBLE. Sign-in fails either way. Only the host stall depends on which exception is thrown.
- The transport ceiling of about fps/2 relies on UnitySynchronizationContext and Mono socket behaviour; the review reproduced it with a probe, but not in Unity. The frozen guest camera is inferred from Rigidbody/Transform sync semantics. The frame size (about 190 KB) and the event counts are estimates from code.
- These defects are currently hidden behind the first-frame disconnect, so their real-world severity shows only after that fix: frame-rate-bound transport, frozen guest view and frame size.
- Every 'ported/at parity' visual claim is code-level only and has not been compared on screen: 20 entrances, 11 scripted kills, hats, wound carving, killcam framing, hazards, and the menus and HUD at 1280x720, 1080p, 1440p and 3440x1440. Neither is any rendered difference, including tone mapping, particles and IMGUI approximations.
- Feel needs a playtest: PhysX vs Rapier contacts, ramps, stairs and moving props, first-person/ADS/scope stability, the audio mix, the stereo direction (#31 is from code), and combat feel after the rule fixes.
- Whether the Site account API accepts requests from the native client in practice is untested. It sends CORS * with no origin gate and the /api/accounts/ path matches.
- Partly checked:
  - Of the Ward Zero sub-findings, only #72 was re-checked at HEAD.
  - #25 is presumed fixed by the captured-melee port.
  - The Chip-effect gaps were not checked exhaustively beyond those listed; '0 of 236 pending' checks only names and stats.
- No host+join test with a second machine, no internet relay room, no headless --ca-server run and no live account flow have been executed. The native feed is 404, so the updater path cannot be exercised yet.
