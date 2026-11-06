# Expeditions screen (Unity menu)

Owner requests:
- 2026-10-15: redesign the "Start Expedition" screen. Modern, multiplayer capable, the party at the top.
- 2026-11-06: "remove the Wave Defense option, and add boss battle option ... to test bosses". And: "revamp the start
  expedition screen UI, it shows the outdated method. The run is now randomized based on the slot machines (So black siren
  is not applicable) ... the map here will not be accurate. and no point in selecting different expeditions as its a
  all-in-one thing. Clean this up, make the UI beautiful with great effects and transitions."

Status (2026-11-06, `claude/expeditions-boss-battle`): presentation only. Checked with managed checks and a type-check
against Unity reference assemblies (see DEV_LOG). Not yet seen in the Unity Editor or on Windows.

## Where it lives

| Piece | File |
| --- | --- |
| Catalogue: the run's entry, its briefing, the boss roster and emblems. Rename things here. | `Core/NativeExpeditionCatalogue.cs` |
| Rules (modes, launch gates), layout and the sample spin's picks | `Core/NativeExpeditionsMenu.cs` |
| Drawing, input and actions | `Runtime/NativeExpeditionsMenu.cs` |
| Entry points | `NativeHomeMenu.cs` `BossScreen` (Start Expedition, home key 1); `NativeLobbyUI.cs` `LobbyScreen` (a PvE room) |
| Checks | `Tests/ExpeditionsMenuChecks.cs` (`CoreTests --expeditions-menu`, also in the full run) |

The run's catalogue entry keeps the id `black-siren` and its areas. The screen no longer shows that name, but
`Core/ExpeditionDeploymentRoom` reads the entry: its live areas are the Instance Machine's authored pool, and the id is part
of the run's compatibility digest. Renaming it changes every run's digest, so leave it.

## Layout

The screen is a full-canvas cyber layout over the panel screens' shade, with a void veil, a pool of colour behind the
browser and a slow scan band. In Boss Battle the pool eases toward the picked boss's colour. From top to bottom:

1. **Header.**
   - PVE · EXPEDITIONS or PVE · BOSS BATTLE, and the title, which rises in when the mode changes.
   - A seat chip: SOLO, HOSTING · ROOM code, or GUEST · ROOM code.
   - At the right: Customize and Settings (both return here), Report in a room, and the close hexagon when not in a room.
2. **Party strip**, PARTY n / 4. There is one card per member: the live 3D portrait, the seat number, the role (HOST with a
   crown, YOU, BOT, PILOT), the name, and the weapon and class. A room shows each member's ready pill and the READY meter.
   The newest bot has × for whoever sets the party. Open seats offer ADD BOT (Boss Battle only) and HOST ROOM, or the invite
   in a room.
3. **The browser follows the mode.** Switching mode slides the new body in under a cyan line that sweeps across the browser
   (Reduced effects: no slide or sweep).
   - **START EXPEDITION: THE RUN**, tagged ONE RUN · NEW EVERY TIME. One card, no carousel and no map:
     - **Key art: the Deployment Station's two machines in miniature, spinning a sample** every 8.4 s, labelled SAMPLE SPIN.
       - The INSTANCE MACHINE's three reels (EAST, WEST, SOUTH) carry the station's own destinations, colours and
         emblems (`NativeDeployPresentation`, `NativeDeployArt.EmblemMarkup`). They move with the station's reel motion
         (`NativeDeployPresentation.Reel`): spin-up, cruise, staggered stops with overshoot and settle, and a flash in the
         destination's colour as each lands. The lever drops as the roll starts.
       - Then the MUTATOR MACHINE (OPTIONAL, dashed) flips its three cards through the catalogue and lands, each card bordered
         in its polarity (BOON, BANE, CHAOS).
       - The picks are made up (`NativeExpeditionsMenu.SpinPicks`, seeded by the spin's number). Nothing is read or decided:
         the station rolls the real run. Reduced effects shows one landed sample, still.
     - **Details:** PVE · CO-OP RUN and ALL-IN-ONE, the title EXPEDITION, the tag line, the party, length and NEW CITY EVERY
       RUN, then HOW A RUN GOES: 01 Deployment Station, 02 The City, 03 Three Areas, 04 The Last Fight. Step 01 lights while
       the sample spins.
   - **BOSS BATTLE**, with the counter, ‹ › and the Q/E keys:
     - **The card:** the picked boss's art and its details. When the pick changes, the old card slides out and the new one
       arrives (Reduced effects: a cross-fade).
       - **Art:** the boss's emblem on a stage: an aura in its colour, a receding floor, a fine ring with ticks turning one
         way and a dotted arc turning the other, rising motes and a scan line. On arrival a ring bursts outward and the emblem
         settles from a little larger. The intro card's line (IT HAS HEARD YOU, THE WATER IS ALWAYS WARM...) sits at the
         foot between two rules. A stamp at the top left shows FIGHT NOW (pulsing), IN RUNS or IN DEVELOPMENT.
       - **Details:** the eyebrow and subtitle, the name (glowing, with a glitch as it arrives), the blurb, the area, phases and
         move count, and SIGNATURE MOVES. The moves come from each boss's own table (`NativeStationBoss.Moves`,
         `NativeIronworksBoss.Actions`, `NativeHumBoss.Moves`, `NativeDeepEndBoss.Moves`, `NativeFallenBoss.Moves`), so the
         card cannot drift from the fight.
     - **The rail:** every boss as a tile with its emblem, name and state. The pick is lit in its accent with brackets and a
       bar. Hover lifts a tile; a click picks it.
     - **Emblems** are markup in the catalogue (white, with near-black cut-outs, tinted by the draw), checked by rasterising
       each: THE 6:47's carriage nose under a clock stopped at 6:47, Vessel C on its trunnions pouring, THE HUM's ceiling
       panel over a long pale face, THE DEEP END's drain, NOCTIEL's wings about a falling sword. The Siren uses the lobby's
       face.
4. **MODE** (TAB switches). Two tiles: **START EXPEDITION** and **BOSS BATTLE**. The picked tile has a slow sheen.
   - Under START EXPEDITION: IN THE INSTANCE MACHINE, the destinations, each with the boss at its end. The ones the sample
     spin just landed on light up.
   - Under BOSS BATTLE: THE FIGHT, what this boss is today and how to reach it, then the honest reason when it cannot start.
   - The LOADOUT row shows the weapon and class, and the party bots (NO BOTS IN A RUN for the run). EDIT or L opens the
     drawer.
5. **Action bar.**
   - Not in a room: a SOLO | HOST ROOM | JOIN ROOM segment.
   - In a room: the room code, the LAN line, CODE, INVITE (LAN host) and LEAVE ROOM.
   - Then a status line: an error, the launch's reason, or who the room is waiting for.
   - Then the launch button: START EXPEDITION, or FIGHT THE SIREN (FIGHT and the boss's name), with its detail line and
     ENTER. A guest gets READY (or CANCEL READY) and SEND LOADOUT instead.
6. **LOADOUT drawer.** As before: party bots (0–3), "Party bots use my weapon", and the class and weapon pickers.

Fit: the layout is laid out in a design space at least 1240×720 and scales the canvas down below that. At ultrawide the
content is capped at 1760 units and centred. Short canvases use a compact card, machine, rail, mode panel and bar. The
checks hold every part inside its region from 1280×720 at 1.5 to 3440×1440 and 4K.

Controls:
- Tab, W/S or ↑/↓: switch the mode.
- Q/E, A/D or ←/→: turn the boss roster. 1–6: pick a boss (switching to Boss Battle).
- L: open the loadout.
- Enter or Space: launch or Ready.
- Escape: closes the drawer, else goes back (not in a room).
- Controller (read once a frame): LB/RB turn the roster, Y switches mode, X opens the loadout, A launches, B closes or
  goes back.

## What each boss can do from here (`NativeExpeditionCatalogue.BossFight`)

| Boss | State | What the launch does |
| --- | --- | --- |
| THE SIREN | Direct (FIGHT NOW) | The boss-test match started at her: `ApplyRule(SkipWaves,1)`, `prefs.pve=true`, `RequestStage(StageLoadKind.Boss,StartMatch)`. No waves. Solo with bots, or co-op through a room. |
| THE 6:47 | InRun | Disabled. "Fought inside a run for now: roll CENTRAL STATION at the Instance Machine. A direct start is next." |
| THE CONVERTER | InRun | Disabled, as above (CITY IRONWORKS). |
| THE HUM | InRun | Disabled, as above (LEVEL 0). |
| THE DEEP END | InRun | Disabled, as above (POOLROOMS). |
| NOCTIEL | Preview | Disabled. Presentation only behind DEV panel · Camera · Final boss preview until its rules are bound. |

## Boss Battle direct starts (handoff to Astra)

The four zone bosses' rules are live, but they only spawn inside a run: `NativeExpeditionM1Arena.SpawnExpeditionBoss`, after
the area's quota (or the Backrooms exit), in an expedition session's area. Starting one from this screen needs a
simulation entry, which is Astra's side of the role split. The screen is ready for it:

- **What is wanted:** pick a zone boss, press FIGHT, and the party (solo with bots, or a co-op room) lands in that boss's
  arena with the boss arriving. No hub, no quota, no deployment roll. Victory or a wipe returns to this screen, and
  Restart fights it again.
- **Presentation hook:** `Runtime/NativeExpeditionsMenu.cs` `ExpStartBoss(K.Boss)` switches on the boss id. Add a case for
  the boss that calls the new start, then set that boss's `Fight=BossFight.Direct` in `Core/NativeExpeditionCatalogue.cs`.
  The card, the rail, the launch label, the gates and the checks follow from that. The checks require every Direct boss to
  have its own case.
- **Room:** no room field carries the picked boss today, so a guest's screen shows the room's boss as the Siren. A direct
  zone-boss start in a room needs the pick in `NativeRoomOptions` (for example the catalogue id) and the guest view to read
  it (`if(seat==X.Seat.Guest){...expBoss=...}` in `ExpeditionsScreen`).
- **Ids:** archetypes `station-647`, `ironworks-converter`, `level0-hum`, `poolrooms-deep-end`; destinations
  `exp-transit`, `exp-foundry`, `exp-backrooms`, `exp-poolrooms` (the catalogue's `Boss.Destination`).

## What works now

Every action is a call the old screens already made:

| Old | Now |
| --- | --- |
| Start Boss Fight / Start at the Siren | BOSS BATTLE · THE SIREN · FIGHT THE SIREN |
| Wave Defense at Wave 1 | Removed from this screen (owner 2026-11-06). The match code is unchanged; Boss Battle always starts it at the Siren. |
| Expedition | START EXPEDITION: `BeginDeploymentStation(K.Run.Id)`, then the station's Deploy uses `RequestStage(StageLoadKind.Expedition,StartExpeditionM1)`. One to four signed-in humans; the host waits for ready guests. |
| Host Co-op Room / Join Room | HOST ROOM (`prefs.pve=true;Go(MenuScreen.Create)`), JOIN ROOM (`Go(MenuScreen.Join)`) |
| Party bots, mirror weapons, class and weapon | party strip add/remove (`ApplyRule(RoomOpt.Bots,…)`) and the drawer |
| Room lobby: Start, Ready / Cancel Ready, Send Loadout, Copy Code, Copy Invite Link, LAN line, Leave Room, Customize, Settings, Connection report | the action bar and the header |

A mode is the room's Versus option: 2 for the run, 1 for the co-op boss match, which Boss Battle always starts at the boss
(`skipWaves`). Transitions title this screen, and a PvE room, EXPEDITIONS / PVE.

## Multiplayer integration (Astra, 2026-10-15)

`NativeRoomOptions.expedition` carries the host's mode. Create Room offers Versus, Boss Test and Expedition. Guests display
the host's mode and ready up. Expedition has no lobby bots and requires distinct signed-in accounts plus a compatible
client; runtime validates these again at launch. See EXPEDITION-HUB.md, "Expedition co-op hosting," for the load barrier,
reconnect policy and unrun Windows checks.
