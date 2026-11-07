# DEV Abuse authority — owner direction, 2026-10-14

Astra implementation on `codex/dev-abuse-authority`, based on shared `09096aff`.
This is the logic/network contract for Claude's `claude/dev-tools` UI. That branch
and `docs/DEV-TOOLS.md` were not on the remote at implementation time. Incorporate
this contract there when the UI lands; do not infer UI completion from this work.

## Gate and UI entry points

`ConversionGame.DevAbuseEnabled` is a local, session-only opt-in (default false).
Claude's DEV Abuse settings toggle sets it. `SendDevCommand(action, target, args)`
returns a transaction ID. Target `0` means the sender. `DevLastReply` and
`DevReplyReceived` expose immediate results; rendering must use authoritative
state rather than optimistically changing the pilot. The offline host path
reverifies the local account token. Guests send the command to the host.

`NativeDevRules.DevAllowed(seat, utcMilliseconds)` requires the host's
`NativeAccess.Verify` result to be Developer and unexpired. Host, guest and
room owner have no special precedence: a verified developer guest can act on
any pilot and use every room action. The host checks the grant on every request,
including retries, and again before executing. A temporary weapon scope cannot
grant DEV authority. The existing offline `SandboxAllowed` gate is unchanged.

Commands require an active Fight phase and a connected target. Dead pilots can
be restored or configured; movement/down require a living target. Unsupported
content, an unavailable target, exhausted object budgets and stale revisions
produce a refusal with a reason.

## Wire and reliability

Hosts advertise `devProtocol:2` in `welcome`. A guest gets an immediate local
refusal for an older host without that capability. Missing legacy scale fields
normalize to 1; other new state fields default false. Relay protocol stays 7.

`dev-command` envelope: `version`, `epoch`, `generation`, `devCommand` containing
`{transaction, action, target, args}`. `target` is a seat ID; `args` is a positional
string array, at most four entries of at most 64 characters. Numbers use invariant
culture and must be finite. No account, role or authorization is accepted in the
payload. Identity comes exclusively from the authenticated peer seat.

`dev-reply`: `version`, `epoch`, `devReply:{transaction,state,reason}`. Current
actions are synchronous and reply `done` or `refused`; the UI also accepts
`accepted` for a future deferred action. Reasons are always present on refusals.
There are at most 32 pending client requests. A ten-second timeout explicitly
reports an unknown outcome, not that an action was undone.

A match-owned router allows eight burst commands and refills four per unscaled
second, per account (legacy signed grants use the seat). It remembers up to 8192
transactions without eviction, refuses conflicting reuse, and returns the same
terminal result on retries without reapplying effects. A retry of a completed
arena reset can receive its cached reply after generation advances. Stale new
commands cannot execute. Failed partial actions are also remembered. The gate
is rechecked before looking up cached results. Transport's existing receive and
queue limits remain in place. `DEV_ACTION` lines in the host's local Unity log
record sender seat, transaction, action and target; no token or chat is logged.

## Action arguments

| Action | Positional args | Behavior / limits |
| --- | --- | --- |
| restore / down | none | Restore at a clear spawn / knock out the target. |
| god / infinite-ammo / no-cooldowns | `on` or `off` | Match pilot flags; cooldown bypass retains action windups and resource costs. |
| chip-add / chip-remove | chip ID, count | 1–100, catalog eligibility/stack limits; cannot remove more than held. |
| weapon / swap-weapon | weapon ID | Catalog weapon; clears match chips, refreshes resources. DEV weapons use a separate expiring match-only weapon scope, never change seat identity. |
| teleport / send | x, y, z | Finite point inside arena bounds; clears committed actions and velocity. |
| launch | force | Upward velocity, 1–100 m/s; releases freeze. |
| freeze | seconds | 0–30 simulation seconds. |
| bring | none | Two metres in front of the developer's position. Developer must be alive. |
| shrink / grow | none | 0.5× / 2× visual rig size. Hit volumes and reach do **not** scale. |
| spawn-dummy | weapon ID, input, team | Input: ai, idle, primary, q, e, block, dash, jump, 1–5; team 0–7. |
| spawn-dummies-around | count | 1–7 pistol dummies, three-metre ring, hostile team relative to target. |
| clear-dummies | none | Clears only DEV-created dummies. Their inactive slots can be reused. |
| time-scale | scale | 0.1–2, authoritative match speed replicated to guests; resets outside match. |
| arena-reset | none | Rebuilds arena and preserves PvE configuration, reveal and progression exclusion. |
| round-end | none | Ends round without awarding a winner; cooperative mode ends the match. |
| hazard-trigger | hazard ID | Immediate one-shot pulse of the authored hazard's damage/volume at its anchor. Normal hazard schedules remain intact. |
| hazard-at | none | Nearest authored hazard's pulse translated to the target. Refuses maps without authored hazards. |
| debris | mass, speed, material, impact | 1–500 kg, 1–100 m/s, destructible catalog material, face/edge/corner. Launches toward target. |
| debris-rain | material | Six 8 kg pieces above target, downward 10 m/s. |
| reveal | none | Reveals the sender only, irreversible for that match. |

Eight pilot slots include dummies and the reserved PvE boss; spawning is atomic
and refuses if the count will not fit. Dummies do not count as PvE party members.
Debris uses existing fragment limits and dynamic collision/replication.

## Replicated presentation data

- `NativeSeat.devRevealed` → `NativeSeatFrame.devRevealed`: false initially;
  only reveal sets it. Draw the moment/tag from this field, never from access.
- `devScale` on seat/frame, multiplied into existing `Actor.RigScale`:
  ordinary third-person rendering and replay consume it automatically.
- `NativePilotFrame.devGod`, `devInfiniteAmmo`, `devNoCooldowns`: UI status.
- `NativeGameFrame.devTimeScale`: authoritative time scale.
- `NativeGameFrame.devHazards`: bounded, two-second `NativeDevHazardPulse[]`
  with authored hazard ID, translated world position and simulation timestamp.
  Claude should draw the forced hazard pulse from these records; the ordinary
  scheduled hazard presentation is not retimed by this command.
- `devDummy` identifies created dummies. No stealth join is implemented.

## Account isolation and lifetime

Effects only mutate arena bodies, match seats and temporary objects. They never
call account/profile save, purchases or reward reduction. Original weapons are
restored when the arena is disposed; identity grants are never replaced by a
troll effect. Flags/resources on pilot objects end on arena reconstruction;
weapon/chip/scale/reveal changes are confined to the current match. DEV time
scale ends on leaving the match. Reconnect preserves host-owned match state.

Before the first mutation, the invoking account's seats are marked
`devCommandsRan`. This survives arena resets and disconnects, and is replicated.
`NativeMatch.CanEarnProgression(seatId)` excludes those seats. Other pilots retain
eligibility. Current PvP/PvE has no persistent XP/receipt writer; any future writer
must use this gate. Expedition's separate dormant extraction session is not a
match receipt writer and has no active arena binding. This implementation does
not invent account rewards or modify its save transaction.

Future stealth joining is explicitly out of scope. If later built, terms must
state developers may join sessions; hidden presence must never capture/log room
chat or voice, and reveal must end hidden presence.

## Verification

Managed `--dev-abuse`: 95 checks for verified/non-dev/expired/forged grants,
scoped weapon non-authority, all action schemas/dispatch, bounded values,
rate/dedup/stale reset semantics, failure replay and progression isolation.

`NativeArena.ValidateDevAbuseAuthority` is attached to the real Unity native
build gate. It exercises the actual actions in local and encoded-guest paths,
god damage, ammo/cooldowns, chip/weapon changes, dummies, debris, hazards, round
and arena resets, account/grant isolation, JsonUtility and guest snapshot apply.
This gate requires Unity; adding it is not evidence that it has run.

Unity compilation, PhysX, Windows rendered gameplay, live host/guest networking,
UI integration, reveal/hazard visuals and compiled test-channel publication
remain acceptance steps. No native/Windows pass is claimed from managed tests.

## DEV monsters — protocol 2

`spawn-monster` uses target seat (0 = sender), with exactly four arguments:
`[kind, count, place, behaviour]`. Kinds must exist in PveContent and be one of
shambler, runner, brute, spitter or hulk. Count is 1–8; actual placement is capped
by 24 live DEV monsters, 2 live hulks and NativeSpitterRules.Cap live spitters.
Hulk/spitter caps include ordinary monsters. Successful replies report the actual
count in `reason`; retries replay it without another spawn. No valid placement
refuses. `clear-monsters` removes only DEV monsters and their acid/poison.

Placement uses host-observed positions and view input. `near` checks a 4–7 m
ring, `aim` requires a world hit within 40 m, and `drop` checks headroom about
6 m above supported ground near the target. `entries` uses compatible authored
Ward entrances on Siren, placing at the supported exit; it does not destroy a
breach wall or run the normal wave entrance sequence. Other maps currently have
no authored monster entry adapter and refuse `entries`. This is a contract
clarification for Claude's summon-in effect, which owns the visual arrival.

`hunt` attacks only the selected pilot; `frenzy` chooses the nearest living pilot;
`idle` waits for damage and then hunts its first attacker. The bounded direct
chase uses world obstruction, ground and stage hazard checks on every map. It
stops at obstacles; it does not promise a full navmesh path around them.
Every pilot can damage DEV monsters, including the developer who spawned them.
They do not enter Siren's wave completion, possession or reward paths.

`NativeMinionFrame.devMonster` replicates on all maps. Off-PvE spit projectiles
and pools use optional `NativeGameFrame.devAcid`; PvE retains `pve.acid`.
Damage/death records carry `damageKind = "dev-monster"` and kill-feed text marks
DEV MONSTER. No spawning-player credit is assigned; the existing ten-second
last-damager rule still applies. Match exit clears monsters and lingering attacks.
No XP, drops, economy or progression receipt is issued for killing them.
The same command path works in the local Developer Sandbox without requiring
its separate DEV Abuse toggle; host verification remains mandatory.

The native authority fixture now covers spawn replies, damage eligibility,
idle wake, frame encoding/guest apply, clear isolation and phase cleanup.
These Unity/PhysX checks are added, not run in this container.


Claude's Monsters presentation at shared `debed457` is integrated on this draft.
`NativeDevMonsterAdapter.DevMonsterOf` now reads `m.DevMonster`; authority lives
in `NativeDevMonsterAuthority.cs` to preserve Claude's presentation file.
