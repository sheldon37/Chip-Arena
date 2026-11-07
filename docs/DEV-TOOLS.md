# DEV tools and DEV Abuse (Unity)

Owner, 2026-10-14: "Improve sandbox tools- Better UI, more control. With this in mind, give a "DEV Abuse" Toggle in dev
settings, this will allow dev options in matches (online included) for only dev accounts."

Owner clarification, 2026-10-14: DEV Abuse is a fun system for developers to drop into matches and play around with the
community. It is surprise first, then a reveal.

This document covers the presentation and UI side (Claude). The authority is Astra's, and
`docs/DEV-ABUSE-AUTHORITY.md` is the source of truth for it: the gate, the wire, the action arguments and limits, rate
limits, deduplication, account isolation and replication. Where this document and that one differ on the authority, that
one wins.

## Who gets what

| Match | Verified, unexpired developer | Everyone else |
| --- | --- | --- |
| Developer Sandbox (offline) | The DEV panel, as the old Sandbox Tools, with no opt-in | Never |
| Offline match, online host, online guest | The DEV panel when Settings · Developer · **DEV Abuse** is on | Never |
| Any | The DEV ABUSE ON tag while the opt-in is in effect | The reveal moment and DEV tags, from `devRevealed` |

- "Verified developer" means `account.Developer`: the P256-signed claim, Developer and unexpired.
- **DEV Abuse** is off by default and saved in the preferences (`devAbuse`).
  - It is shown only to a verified developer.
  - It sets Astra's session opt-in `ConversionGame.DevAbuseEnabled`, again for a verified developer only.
  - No host ever trusts it. Every command is checked against the sender's verified seat (`NativeDevRules.DevAllowed`).
- A non-developer never sees the toggle or the panel, even with a stale saved preference.
- Being the host gives no extra rights. A verified developer guest can use every action, room-wide ones included.

## The panel

- **F3** opens or closes the panel. The pause menu's **DEV Tools** item opens it too.
- The panel opens with the cursor free; **F4** frees or recaptures the cursor.
  - While the cursor is free, the pilot holds still: it sends neutral input, as it does while the panel's search field has
    the keyboard.
  - With the cursor captured, you keep playing with the panel visible.
- The panel sits on the right of the match, at most 720 canvas units wide.
  - At 3440x1440 the fight stays in view.
  - It fits the smallest canvas, 1066x600 (interface scale 1.5 at 1080p).
- It is drawn in the cyber kit: `NativeHudChrome` panels, tags and keycaps, `NativeIcons`, the `NativeCyberTheme`
  palette.
- It allocates nothing per frame once warm.

### Layout

- **Header:**
  - mode: SANDBOX or DEV ABUSE;
  - cursor state;
  - context: OFFLINE, ONLINE · HOST or ONLINE · GUEST;
  - DEV COMMANDS ON, or OPT-IN;
  - HOST: NO DEV PROTOCOL, for a guest of an older host;
  - NO PROGRESSION THIS MATCH, once a command has run for your account.
- **Left tab rail:**
  - **Pilot**:
    - your pilot card;
    - the state pills: GOD, INF AMMO, NO CD, REVEALED, SHRUNK/GROWN;
    - restore, down, hit, clear enemy summons, teleport to aim and the reveal;
    - god, infinite ammo and no cooldowns, as on/off pairs showing the replicated state;
    - your weapon;
    - a link to your chips.
  - **Interact**:
    - a target picker covering every pilot and bot, with DEV DUMMY labels;
    - parameters: launch speed, freeze seconds, dummies around, debris-rain material, swap weapon;
    - launch, freeze, bring, send to aim, swap weapon, debris rain, shrink, grow, hazard at target, dummies around,
      debris at target, restore target and down target.
  - **Monsters** (see "Monsters" below; proposed commands):
    - a kind card per content add, with HP, speed and the hulk and spitter caps;
    - a count stepper (1–8), place (near, aim, drop, entries) and behaviour (hunt, frenzy, idle);
    - the Interact target picker (who they hunt, where near and drop place them);
    - Spawn monsters, Clear DEV monsters and the live DEV MONSTERS n/24 count;
    - in the sandbox, the Ward Zero entry picker (moved from Arena).
  - **Dummies**:
    - the sandbox dummy's weapon, input and cast;
    - restore, down, hit and clear summons for the dummy;
    - respawn and repeat-cast seconds;
    - bot difficulty;
    - spawn (input, team) and clearing the DEV dummies.
  - **Chips**:
    - target (self or dummy), search, sort (rarity, name, installed) and filter (all, favourites, installed, recent);
    - a grid of cards with rarity colour and icon, installed count / max, − + MAX CLR, a favourite star (saved) and CAST;
    - Cast every chip.
  - **Arena**:
    - restore all, Arena Reset, Test Block, End round;
    - map and Load map;
    - the debris launcher;
    - the destruction effects preview.
  - **Hazards**:
    - the hazard boxes overlay;
    - each authored hazard with Trigger;
    - the Siren vent dives;
    - the vent splat delay.
  - **Previews**:
    - Preview Draft, Rarities, Knockout and My Death;
    - the galleries: Siren minions, Cyber double jump, saw split, revive, chip selecting;
    - Preview Hazard Kit (every alarm, turret and saw cycling its states) and Lock Hazard Kit to music
      (`docs/HAZARD-KIT.md` 12);
    - Preview DEV Reveal;
    - Zombie gore preview (five zombies looping a hit, a head shot, a gib, a slice and a death; `docs/ZOMBIE-GORE.md`).
  - **Camera**:
    - the fly camera and its speed, and snap to pilot;
    - the six read-only overlays;
    - the event log and its filters;
    - the FPS / network graph;
    - hide HUD.
  - **Simulation**:
    - simulation speed: the sandbox's own speed, or a time-scale command;
    - the room's authoritative speed;
    - safe mode;
    - read-only state: phase, set, round, time, pilots, projectiles, minions, step, frame, round trip.
- **Footer:**
  - the hovered button's reason, or the last action's verdict;
  - the hotkey legend.

### States

Every button resolves through Core `NativeDevActions.Resolve`.

| State | Badge | Meaning |
| --- | --- | --- |
| Live | none | Runs: the sandbox's own call, a DEV command, or a view tool. |
| Needs opt-in | OPT-IN | A DEV command while DEV Abuse is off. |
| Not now | ROUND | A DEV command outside an active round. The authority refuses those. |
| Sandbox only | SBX | A sandbox control or local preview outside the Developer Sandbox. |
| Needs host | HOST | A proposed command the match's authority does not offer yet ("HOST NEEDS MONSTER COMMANDS"). |

- Clicking a disabled button only says why. Nothing reports success on its own:
  - **done** means the authority replied `done`;
  - **refused** shows the authority's reason;
  - **accepted** waits;
  - a reply missing after ten seconds shows as **outcome unknown**, never as undone or done.
- Effects are drawn from the replicated state, never optimistically.

### Hotkeys

| Key | Action |
| --- | --- |
| F3 | Panel |
| F4 | Free or capture the cursor (panel open) |
| F5 | Restore me |
| F6 | Down dummy |
| F7 | Make the dummy cast |
| F1 | Event log |
| F2 | FPS / network graph |
| F10 | Fly camera |
| F11 | Hide HUD (screenshot mode) |
| Home | Snap the fly camera to the pilot |

- **Fly camera controls:** WASD move, E or Space up, Q down, Shift fast, Ctrl slow, the mouse wheel sets the speed.
- **No clash:**
  - none of these keys is bound anywhere else in the Unity code;
  - J is the Expedition screen, F8 the prototype lab's capture, F9 compatibility rendering;
  - F3 matches the Electron client's DEV panel key.
- The hotkeys work with the panel closed, wherever the panel would be available. They resolve like the buttons.

## What runs where

- **Developer Sandbox:** every old Sandbox Tools call runs unchanged through `NativeSandbox.cs` `DevRun`. Each case is
  the old list's `NativeArena` call with the same arguments and limits; `DevToolsChecks` pins them.
  - The panel also offers existing sandbox calls the old list did not:
    - your weapon (`SandboxConfigure(weapon:)`, which resets the arena);
    - map and bot difficulty;
    - Cast every chip and a dummy cast with one chip;
    - the vent splat delay.
  - DEV commands with no sandbox call (god, teleport, interact actions) need the DEV Abuse opt-in in the sandbox too.
- **DEV commands:** these go through Astra's `SendDevCommand(action, target, args)` (target 0 is the sender), using
  `docs/DEV-ABUSE-AUTHORITY.md`'s argument list.
  - The panel clamps to `NativeDevRules.Validate`'s limits:

    | Command | Limit |
    | --- | --- |
    | debris | 1–500 kg, 1–100 m/s |
    | freeze | 0–30 s |
    | launch | 1–100 m/s |
    | spawn-dummies-around | 1–7 |
    | time-scale | 0.1–2 |
    | chip count | 1–100 |
    | dummy input | ai, idle, primary, q, e, block, dash, jump, 1–5 |
    | dummy team | 0–7 |

  - `send` and `teleport` take the aim point as x y z, clamped to the validator's bounds.
  - `bring`, `shrink`, `grow`, `hazard-at`, `reveal`, `round-end` and `arena-reset` take no arguments.
- **View tools:** these run wherever the panel is, online included. They are read-only and change nothing in the match.
  - **Pilot hit volumes:** each pilot's capsule collider, plus the head box `NativeCombatRules.Headshot` tests (+.64 m,
    .17 m half extents, yaw-aligned; dim while down).
  - **Melee hit volumes:** the live swing's `NativeMeleeRules` sector. That is the current angle, reach plus a pilot's
    .45 m, the cone, the sweep across the contact window and the ±1.6 m band. Swings exist on the authority only, so a
    guest sees none. Air attacks and the sprint ring are not drawn.
  - **Hazard boxes:** the authored hazards' boxes, zones and rails.
  - **Nav graph:** links within 45 m and nodes within 30 m.
  - **Expedition anchors:** drawn when a session is bound.
  - **Shot paths:** each projectile's next .6 s, stepped on a copy with `NativeSpecialRules.Ballistic`.
  - The overlays draw at most 900 lines a frame.
  - **Fly camera:** it moves only the match camera, after the pilot camera. The pilot holds still while it flies.
  - **Event log:** the replicated event stream, with filters by category and "only mine".
  - **FPS / network graph:** frame time, simulation step and round trip.
  - **Hide HUD:** only in the live fight view.

## Replicated presentation (from Astra's fields)

- `NativeSeat.devRevealed`: the DEV tag on the nameplate and on kill-feed lines, and the reveal moment. It comes from this
  field only, never from access.
  - The moment plays for everyone when the field turns true. A pilot already revealed when first seen gets the tag only.
  - **The moment:**
    - a scan flash, then a magenta holographic banner opening from the centre;
    - "A DEVELOPER IS HERE" typed in;
    - the developer's name rising with a glitch, and a DEV badge;
    - the line "MATCH-ONLY FUN · YOUR PROGRESS IS SAFE";
    - a burst of data shards;
    - a pulsing ring and chevron over the pilot;
    - the `ui.go` cue.
  - Reduced effects drop the flash, the glitch and the shards. Nothing licensed is used.
  - The sandbox's Preview DEV Reveal plays it locally on your pilot.
- `devScale`: Astra multiplies it into `Actor.RigScale`. The nameplate, chip-select and DEV tags rise with it. The
  first-person eye and every hit volume keep their size, as the authority does.
- `NativePilotFrame.devGod`, `devInfiniteAmmo`, `devNoCooldowns`: pills on the panel and under the DEV ABUSE ON tag.
- `NativeGameFrame.devTimeScale`: a DEV TIME ×n tag for everyone while it is not 1, and the room speed in Simulation.
- `NativeGameFrame.devHazards`: each forced hazard pulse plays once through the shared effects renderer.
  - It uses the hazard cues: saw sparks and spokes, the fire burst, impact rings, electric lines, steam, gas and dust,
    keyed by the hazard's kind and sized from its volume (Core `NativeDevHazardLook`).
  - A short magenta DEV ring marks the pulse for everyone.
  - The scheduled hazard presentation is not retimed.
- `devDummy`: DEV DUMMY in the target picker. The Dummies tab acts on a DEV dummy first.
- `devCommandsRan` / `NativeMatch.CanEarnProgression`: NO PROGRESSION THIS MATCH on your own HUD and panel once a
  command ran for your account.

## Monsters (proposed, Astra to confirm)

Owner, 2026-10-14: "Allow options to spawn monsters in the dev menu". The panel, the tag and the rift are Claude's and
are on the branch. The commands, caps, placement, behaviour and the replicated flag are Astra's. Until Astra confirms
them in `docs/DEV-ABUSE-AUTHORITY.md`, this section is a proposal, and that document wins wherever the two differ.

**Proposed contract**

- `spawn-monster`, target seat (0 = the sender), args `[kind, count, place, behaviour]`:
  - `kind`: a content add id (`PveContent.json` adds: shambler, runner, brute, spitter, hulk). The panel lists them from
    the content, at most 64 characters.
  - `count`: 1–8. The host clamps to its caps: 24 DEV monsters a room, 2 hulks, `NativeSpitterRules.Cap` spitters (4).
    The `done` reply's `reason` says how many spawned (the panel shows "done: reason").
  - `place`:
    - `near`: a ring 4–7 m round the target;
    - `aim`: where the sender's crosshair hits, up to 40 m;
    - `drop`: about 6 m above the target;
    - `entries`: the map's vent, door, drop and breach entrances nearest the target; refused on a map without them.
  - `behaviour`: `hunt` (only the target), `frenzy` (the nearest living pilot) or `idle` (stands until hit, then hunts its
    attacker).
- `clear-monsters`: no args. It clears the room's DEV monsters only.
- The host advertises `devProtocol 2`. Like every DEV command: Fight phase only, match-only, no XP, drops or credit,
  `devCommandsRan` set, a `DEV_ACTION` log line.
- Replicated: `NativeMinionFrame.devMonster` (a DEV monster), for the tag, the rift and the count.
- The sandbox runs the same command locally, on the same path.

**Panel behaviour**

- Spawn and Clear resolve like every DEV command, plus one state: **HOST**, "HOST NEEDS MONSTER COMMANDS", until the
  authority offers them.
  - Online: the host's advertised protocol is 2 or more.
  - Offline and in the sandbox: the local `NativeDevRules.Actions` lists `spawn-monster`.
- Argument shapes come from Core `NativeDevMonsters.ArgsOk` until `NativeDevRules.Validate` knows the actions; after
  that, Validate decides.
- The target is the Interact picker's (your own seat sends 0). `DevArgs` sends the selected kind, the clamped count, and
  the place and behaviour ids.
- The Ward Zero entry picker (`arena.SandboxWardEntry`, sandbox route `ward-spawn`) shows only in the sandbox:
  - a filter (ALL, VENT, DOOR, DROP, BREACH, CELL), previous and next, and "Nearest to me";
  - each entry's kind, level and what `SandboxWardEntry` spawns there (a breach or hulk entry: HULK; a drop: BRUTE;
    otherwise SHAMBLER).

**Presentation (every client)**

- Both effects read one adapter, `NativeDevMonsterAdapter.DevMonsterOf(minion)` in `Runtime/NativeDevMonsters.cs`. It
  returns false until Astra's flag exists, so nothing shows today. **Binding is one line:** replace its body with the
  minion's replicated flag (for example `m!=null&&m.DevMonster`).
- **DEV MONSTER tag:** a magenta tag over each living DEV monster in view, within 40 m, hidden behind walls. It breathes
  unless Reduced effects is on.
- **Summon-in rift:** plays once, the first time a DEV monster is seen, on the shared effects renderer
  (`NativeBlastPresentation.DevMonsterRift`, Core `NativeDevMonsterLook.Rift`). It is a magenta ground ring, three tall
  beams (magenta, cyan, violet), glitch streaks up the column and two spark bursts. With Reduced effects it keeps the
  ring, one beam and a smaller burst.
  - An entry spawn keeps its own vent, door, drop or breach animation instead.
  - A monster already older than .6 s when first seen (a late join) gets no rift.
- No hit timing, hitbox, damage, cooldown, movement or networking change: presentation only.

**Contract changes for Astra to confirm**

1. `SendDevCommand`'s guest check refuses unless `devHostProtocol==1`. It should be `>=1`, or every guest of a
   protocol-2 host is refused. The panel already reads an older host as `devHostProtocol<1`.
2. `SendDevCommand` needs `DevAbuseEnabled` in the sandbox too. "The sandbox runs the same command locally" needs either
   that opt-in or a sandbox exemption for these two commands.
3. Add `spawn-monster` and `clear-monsters` to `NativeDevRules.Actions` and `Validate`: kind at most 64 characters,
   count 1–8, the place and behaviour ids above.
4. `aim` cannot carry x y z in the four args. Use the sender's replicated input `AimTarget` (the pilot's crosshair, not
   the fly camera). It is still set while the panel holds the pilot still.
5. Replicas build `NativeMinion.Pve` from `addKind` only on a PvE stage or the Siren map (`NativeNetworkFrame.cs`). DEV
   monsters on other maps need that add state on replicas, for the zombie presentation and the adapter's entry and
   spawn-age reads.
6. Put the replicated flag on `NativeMinion`, set from `NativeMinionFrame.devMonster`, and bind the adapter (point 1 of
   "Presentation").
7. Put the spawned count in the `done` reply's `reason`, for example "3 spawned (cap 24)".

## Guardrails (owner direction 2026-10-14, defaults)

- Every DEV action is match-only and ends with the match.
- Players' account progression, purchases and rewards are never reduced by DEV actions.
- The developer's own seat earns no XP or progression in a match where DEV commands ran (`devCommandsRan`,
  `NativeMatch.CanEarnProgression`).
- The host logs DEV actions locally (`DEV_ACTION` lines in the host's Unity log).
- No DEV tag shows until the developer reveals; the reveal is for that match and cannot be undone.
- Shrink and grow are visual. Hit volumes and reach do not scale.

**Open for the owner to confirm:**

- the reveal's wording ("A DEVELOPER IS HERE", "MATCH-ONLY FUN · YOUR PROGRESS IS SAFE");
- the reveal's sound (it reuses `ui.go`);
- whether non-developers should see the DEV TIME tag.

Stealth joining stays out of scope, as `DEV-ABUSE-AUTHORITY.md` states.

## Differences from the first brief

- The first brief's names `devActive` and `dev-command {transaction, action, target, args}` became Astra's
  `devRevealed` and the `SendDevCommand` API.
- Room-wide actions are not limited to the host.
- `send` takes x y z, and `bring` takes no arguments.
- The panel follows the authority throughout.

## Verification

- Managed checks: `DevToolsChecks` covers:
  - the toggle, the availability matrix and every sandbox route;
  - the command states and the validator's limits;
  - the link's replies and its timeout;
  - the replicated-state reads and the hazard pulse looks;
  - read-only overlays and the fly camera;
  - the allocation-free frame;
  - the palette, hooks, hotkeys and metas;
  - the proposed monster commands: argument shapes and clamps, HOST NEEDS MONSTER COMMANDS until the authority offers
    them, kinds from the content, the adapter off by default, the rift and the seen tracker, and the ward entries.
- The Unity runtime and editor compile was checked against the real assemblies.
- Offline renders of every tab (Monsters included), the reveal and the HUD tools were made in the scratch IMGUI harness. They are not
  committed.
- **Not run:** the Unity Editor, Windows, PhysX, two clients over a real network, the native build gate
  (`ValidateDevAbuseAuthority` is Astra's), and in-game look and feel.


### Astra authority binding (draft PR #211)

The implementation branch now provides protocol 2, the two monster commands,
actual-count replies and the replicated DevMonster flag. The adapter is bound.
`entries` currently places at compatible authored Ward exits on Siren and uses
the summon rift; it does not run wave entrance traversal or breach walls. Maps
without authored entrances refuse that mode. `aim` traces the host-observed view
ray against world geometry up to 40 m instead of accepting a client point.
Other modes use supported ground/headroom checks. See DEV-ABUSE-AUTHORITY.md.
This binding is on the implementation draft, not a claim of a native playtest.
