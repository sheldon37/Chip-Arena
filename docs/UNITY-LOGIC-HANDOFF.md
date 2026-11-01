# Unity logic handoff — PR #66

## 2026-10-09 — account/profile compatibility and service setup (PR #81)

Account PR #81 and Editor-test isolation PR #83 are merged into shared/cloud
`603ca76b69b2256d58b24abc4ac23166306e1fd4`. The owner chose the most efficient
implementation and authorized publication to Update.
Reuse the existing hosted account service and D1 records; no database migration,
new issuer, credential reset, or backend deployment is necessary.

- All existing NativeAccount members remain: `Account`, `Access` (including
  `Developer`), `Status`, `Busy`, `Token`, `RecoveryCode`, `Accepted`;
  `Login(action,user,pass,recovery,save)`, `Logout`, `Save(profile)`,
  `ReloadSavedProfile`, `Refresh`, `Configure(origin)`. Configure is functional,
  including its optional loadRemembered argument; it is not a no-op.
- **Menu change requested by owner:** removed the Account service URL field from
  NativeProfileUI. No other Settings/Account URL control exists in this head.
  Players sign in normally without entering an endpoint. Claude should keep the
  field hidden; internal Configure and existing custom endpoint preferences work.
- `NativeAccount.DefaultOrigin` points to the existing Chip Arena hosted service.
  New preferences use it. `ConversionPreferences.MigrateAccountService()` upgrades
  older blank/unset URLs once and persists `accountServiceVersion=1`, preserving
  custom URLs and every other preference. Later explicit empty URLs remain empty.
- No configured service shows exactly **Signed out / offline**, including attempted
  sign-in. No network request starts and Busy stays false. Configured-service
  connection failures remain distinguishable from offline mode.
- All NativeProfile fields remain: `name`, `class`, `color`; cosmetics:
  `lobbyPose`, `preferredSpec`, `intro`, `introText`, `bodyColor`, `hat`,
  `killEffect`, `overrideWeaponIntro`. Existing local pilots, account IDs/revisions,
  queued saves, account-scoped pending effects and Reload saved pilot are retained.
  New real JsonUtility Editor checks cover migration and full profile save/reload/
  lobby serialization, including developer/unlocks/flags.
- Signed access remains authoritative for DEV, weapons and cosmetics; flags remain
  in Account.flags and signed claim data. No username-based grants or client bypass.
  No passwords, session/token values or recovery codes were added to source, docs,
  fixtures or Connection report. Existing DPAPI remembered sessions are unchanged.
- Authorized Sites owner inspection confirmed the existing Sheldon record already
  has DEV access and a saved pilot. The supplied password matches its stored verifier
  (checked in memory; no secret logged or saved). No account reset or row change.
- Released through Update: UBA **#8**, native version **80b8edb9**, source `603ca76`.
  Both exact-head native CI jobs passed (`37974507246`), followed by real Unity
  6000.3.24f1 / Linear compile, Editor checks and full delivery validation.
  Publisher `37974643037` attempt 2 verified the public manifest and every payload
  part; receipt commit `b4462d36e4cc63625a48487b9ad6eb5a1de5184f` at 18:52:39 UTC.
- Main automation PR #82 (`f6137be`) starts a missing build pinned to the checked
  cloud commit when Unity's repository hook does not queue one. It uses the existing
  target, avoids duplicates and failed-build retries, then runs all publication
  gates. The actual API start successfully produced build #8. Future Claude/Astra
  game batches still must reach `codex/unity-cloud-current`; task-only pushes are
  not published. No combat, animation, profile schema or server API changes.
- Local checks: 44 passed / one platform skip for the game; publisher suite 65
  passed / one skip, also green on hosted Linux/Windows. Legacy Electron Checks
  still has its documented 12 unit failures and TypeScript errors in chaos.ts,
  leet.ts and standard-melee.ts; the native pipeline does not relabel them green.
- Windows sign-in remains untested: this environment's unauthenticated service
  request was blocked by Cloudflare 403/1010, with no workaround or credential
  submission. Owner administration inspection and password verification are not an
  end-to-end login. After Update, test Sheldon sign-in, DEV access, pilot load/save
  and Reload saved pilot on Windows. Existing accounts and records were untouched.


2026-10-08, Codex/Astra. Branch `codex/unity-parity-logic-current`, PR [#66](https://github.com/sheldon37/Chip-Arena/pull/66) into `claude/sleepy-ritchie-g8ruhv` (#62). Requested baseline `b828acb`; earlier logic checkpoints `718cd4f`, `d80d974`, `c9888fc`. PR #65 had conflicts after Claude advanced; replacement PR #66 starts directly at `4207641` and carries the identical reviewed tree in `9921c78`, without a merge or force push. Claude presentation changes through `4207641` are preserved. Implemented on the branch only: nothing merged or released.

Read the status prefix on each A1–A31 entry in `UNITY-PARITY-GAPS.md`. Original findings remain below those prefixes as evidence. Do not infer acceptance from “0 pending” catalogue entries.

## Logic delivered

- A1/A5: optional nested-object presence survives actual JsonUtility; compact control envelopes; replay snapshots detach mutable actors and isolate their physics scene; failure cleanup restores live visibility.
- A2–A4: transport runs independently of Unity's render synchronization context; replica Transforms move, inputs predict/reconcile and remote actors interpolate. Events cap at 512 with 32 transient notifications per simulation tick. Frames encode once for all guests, cap raw JSON at 1 MiB / encoded payload at 256 KiB, and trim old cosmetic events/corpses/effects/shots before skipping an oversized frame. Authority gameplay does not depend on presentation drops. Epoch/generation guards, lobby coalescing/rate limits, relay heartbeat/retry/closure, invalid-peer isolation, participant-only frames and reconnect transfer cover the listed stall paths. Disconnected bodies stop colliding; a 12-second disconnect grace permits seat recovery, then an absent opposing side forfeits rather than farming rounds. A reclaimed seat after timeout remains dead until a new round; its 90-second lease does not undo a completed forfeit.
- A6: managed P-256 public-signature verification, catch-all token failure and per-peer host exception isolation. No private signing keys or credentials added. Mono execution still needs the Unity run.
- A8/A9/A11/A14: real bot/cosmetic/options lobby data; ready includes selected loadout; eight team IDs and team-change API; all-ready auto-start; LAN IPv4/code discovery and LAN invite parsing; source bot reaction/error/ranges/ability conditions and difficulty levels; random map/set rotation, skip-intro and bot options; source defaults. Ordinary bot intros are excluded and a human Siren is ordered last; sandbox skips intros.
- A10/A15/A16: head volume, held melee, independent Q/E timing, authority-derived targets, block/roll/DoT rules, ability prop contacts, Siren parry rules, persistent field force, overtime and fresh PvP/PvE seeds. Enemy acquisition excludes allies even when friendly fire permits damage.
- A17/A19: lobby pose and pending account-scoped effect choices preserved; empty account profiles no longer replace local pilots; queued profile saves, persistent recovery code, reload API and atomic local preferences with corrupt-file recovery. Rematch/retry count only connected humans; a three-second lobby return is cancellable; guests can leave PvE results; host-loss state is exposed.
- A20/A23: corpse-position paid/self revives retain resources; self-revive waits for vent landing; group respawns avoid occupied pads; possession selects a seeded distant active add, restores full scaled HP and cancels windup; hunt persists beyond 60 s while possessed; medic group pulses/caps; source add impulse, LOS, chase and Emperor-minion contacts; per-pilot expiring denials; cleanup records exposed add deaths. Navigation reuses flow/queue arrays and spreads grid queries across ticks. Boss fall rescue preserves HP and pushes pilots away from spawn. Collision-induced horizontal velocity carries with knockbackTaken; no invented 19 m pilot ceiling; debris damage only in Fight; collapse events emitted.
- A21/A24/A27–A30: richer event stream, measured metrics/report data, authoritative PvE pick source and top-tier bot ties; ability projectile ceiling; listed Chip/summon/armor/Return Address/ledger fixes; shared Late Bloomer motion state follows the pinned source's object-spread behavior (including that inherited quirk); a player-lifetime named mutex and saved fullscreen API.
- A22: signed local sandbox APIs for map/weapon/difficulty/reset/block, previews, debris launch, selected/every-Chip dummy casts, splat delay and time scale. Claude owns menu wiring and visual test presentation.

## Contracts for Claude

| Need | Available logic |
|---|---|
| Lobby cards | `ConversionGame.LobbySeats`: `bot`, `difficulty`, `team`, `accentColor`, `bodyColor`, `hat`, kit/name/ready/connected. The roster already reads these; do not append invented duplicate bot cards. |
| Guest Ready / team | `GuestReady(bool)` sends both loadout and readiness; `ChangeTeam(int)` supports 0–7. |
| Room rules | `RoomOptions` / `SetRoomOptions(NativeRoomOptions)`: per-bot `weapon/team/difficulty`, global difficulty, map=`random`, randomizeMap, skipPlayerIntros, 1–10 rounds, teams/friendlyFire, bot/draft/PvE options. Use these methods when changing options. |
| LAN/code/invite | `LanAddresses`, `RoomCode`, `InviteUrl`, `JoinInvite(string)`. Blank host-address field selects code discovery. `InviteUrl` is named differently from the existing `RoomInvite` drawing method. Internet CA- directory codes remain gated. |
| Saved pilot | `ReloadSavedPilot()`. Both Customize and Account save paths use `SavePilot()`. Preserve `prefs.lobbyPose` even before a native pose picker exists. |
| Match results | `VoteRematch()`, `RematchVotes`, `RematchRequired`, `HostLost`, `RequestLobbyReturn()`, `CancelLobbyReturn()`, `LobbyReturnSeconds`, `ReturnMainMenu()`. Existing result button actions/counts are wired with minimal layout changes. Guest retry waiting/cancel-countdown presentation can use these fields. |
| PvE picks/notices | `NativePvePick.Source` and frame `source`: `start`, `machine`, `wave`. Existing pick label reads it. `arena.PveNotice(seatId)` is private to that pilot and expires; existing denial banner reads it. |
| Connection pill/report | `ConnectionMetrics` gives RTT/latest/p95, snapshot gap/latest/p95, total decoded KiB, largest decoded frame KiB, samples and connected state. `ConnectionReportJson()` serializes the local client report; `PeerMetrics` supplies host-side peer reports. No account tokens in report data. |
| Desktop settings | `Fullscreen` / `SetFullscreen(bool)`. |
| Sandbox | `NativeArena.SandboxConfigure`, `SandboxReset`, `SandboxTestBlock`, `SandboxPreview`, `SandboxLaunchDebris`, `SandboxCastChip`, `SandboxCastEveryChip`, `SandboxSplatDelay`; `ConversionGame.SetSandboxTimeScale`. `SandboxPreviewRarity` exposes the requested preview tier. Keep the signed/offline guards. |
| Specialization | No timed phase added: owner decision 5 is still open. Menu selection remains the current behavior. |

## Event contract

Existing numeric EventKind values are preserved; new values append. Wire protocol is **5**. Use current native clients/authority together; relay route names remain `/native/v3` because that endpoint transports opaque native bytes. Relay health advertises protocol 5 and host control now uses PING/PONG.

New emitted kinds: `Explosion`, `BlockPulse`, `Vacuum`, `Arc`, `Freeze`, `Teleport`, `Critical`, `Catch`, `Split`, `Ricochet`, `Pong`, `CeilingHit`, `HazardHit`, `BurnTick`, `PoisonTick`, `Collapse`, `Leet`, `Emperor`, `Arcade`, `Siren`, `PveBuy`, `PveGib`, `PvePhase`, `PveRevive`, `PveMoney`, `Install`, `Field`, `Summon`, `Heal`.

All carry Id/Time/Source/Target/Position/Amount. `Radius` is the gameplay radius when applicable; `From` is the beam/teleport origin (otherwise Position); `Detail` is the effect/ability/action/source identifier when applicable. Explosion Detail is `chip` or `ability`; field Detail carries its specific Chip ID. Collapse Detail is the prop ID; Siren Detail is the move ID; PvE phase Detail is the cinematic; install Detail is the Chip ID. Amount retains event-specific meaning (damage, healed HP, price, reward, or limb bit). A source 0 means no pilot source; horde strikes no longer falsely credit the boss. Burn/poison ticks have their own kind and no duplicate Hit event, allowing camera shake to exclude them. Heal is emitted only for actual positive healing. Presentation consumers should explicitly handle new kinds instead of assuming every unknown event is an ordinary hit.

## Verification and remaining gates

Actual: managed CoreTests and real UDP/TCP; check_relay.py with real WebSocket; 24 Python tests; 51 UpdaterTests. Runtime API compilation uses real Unity 6000.3.24f1 DLLs with .NET 8 Roslyn/C# 9, not Unity's Mono runtime. The cloud SDK's MSBuild process discovery is unsupported, so checks used directly compiled assemblies; GitHub CI builds the normal projects. Native CI run 37811222667 on c9888fc passed both native-source and native-updater-windows, including shipping launcher compilation and real Windows process fixtures. Local CoreTests report 100 PASS lines; source packaging validates 670 files.

`NativeNetworkBuildChecks` invokes real JsonUtility and isolated PhysX regressions, including `ValidateParityCombat` and `ValidateFollowupParity`; **they have not run here**. The Windows launcher fixture is not a Unity player build: no rendered test, live sign-in, WAN test, headless Unity or real native update is claimed. `cmd.exe /c Build-Windows.cmd` was attempted and exited 127 because cmd.exe is unavailable; no Unity Editor/license is present. Run the exact PR head with Build-Windows.cmd and retain build.log and the `UNITY_TEST_BUILD_READY` line, then test two-client host/join/ready/reconnect, four-player Ward Zero, deaths/replay, end votes, account grants, duplicate launch/fullscreen, update startup/rollback and four resolutions.

Open decisions: A7 endpoints/hosting (1–2), A8 internet invites (1–2), A13 native feed/release (4), A18 specialization (5), A24 Siren intro duration (7), A25 dedicated topology (9), A26 migration/cutover (3), and A22 visual-test scope (8). A12 remains the environment/acceptance gate. No live feed, Site, shared branch or signing secrets were changed. Owner retains merges and publication.
