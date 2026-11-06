# Expeditions screen (Unity menu)

Owner requests:
- 2026-10-15: redesign the "Start Expedition" screen. Modern, multiplayer capable, the party at the top.
- 2026-11-06: "remove the Wave Defense option, and add boss battle option ... to test bosses". And: "revamp the start
  expedition screen UI, it shows the outdated method. The run is now randomized based on the slot machines (So black siren
  is not applicable) ... the map here will not be accurate. and no point in selecting different expeditions as its a
  all-in-one thing. Clean this up, make the UI beautiful with great effects and transitions."
- 2026-11-06, second pass: "we don't roll the machine until we start the expedition (Currently its like we roll twice
  now). Maybe design something to fill that space or make player lobby portraits bigger? Still; make the screen very
  appealing."

Status (2026-11-06, second pass on `claude/expedition-squad`): presentation only. Checked with managed checks and a
type-check against Unity reference assemblies (see DEV_LOG). Not yet seen in the Unity Editor or on Windows.

## Where it lives

| Piece | File |
| --- | --- |
| Catalogue: the run's entry, its briefing, the boss roster, emblems and who may fill seats. Rename things here. | `Core/NativeExpeditionCatalogue.cs` |
| Rules (modes, launch gates) and layout (both modes' regions, the squad card, the briefing's fit) | `Core/NativeExpeditionsMenu.cs` |
| Drawing, input and actions | `Runtime/NativeExpeditionsMenu.cs` |
| Direct zone-boss starts (Astra) | `Core/NativeBossBattle.cs`, `Runtime/NativeBossBattle.cs` |
| Entry points | `NativeHomeMenu.cs` `BossScreen` (Start Expedition, home key 1); `NativeLobbyUI.cs` `LobbyScreen` (a PvE room) |
| Checks | `Tests/ExpeditionsMenuChecks.cs` (`CoreTests --expeditions-menu`, also in the full run) |

The run's catalogue entry keeps the id `black-siren` and its areas. The screen no longer shows that name, but
`Core/ExpeditionDeploymentRoom` reads the entry: its live areas are the Instance Machine's authored pool, and the id is part
of the run's compatibility digest. Renaming it changes every run's digest, so leave it.

## Layout

The screen is a full-canvas cyber layout over the panel screens' shade, with a void veil, a pool of colour behind the
content and a slow scan band. In Boss Battle the pool eases toward the picked boss's colour. Each mode lays the screen out
its own way. Switching mode brings the new layout in, section by section, under a cyan line that sweeps across the
content, toward the right for Boss Battle and the left for the run (Reduced effects: no sweep or entrance).

1. **Header** (both modes).
   - PVE · EXPEDITIONS or PVE · BOSS BATTLE, and the title, which rises in when the mode changes.
   - A seat chip: SOLO, HOSTING · ROOM code, or GUEST · ROOM code.
   - At the right: Customize and Settings (both return here), Report in a room, and the close hexagon when not in a room.
2. **START EXPEDITION: THE SQUAD and THE RUN.** Nothing on this screen rolls or samples a run; the machines roll at the
   Deployment Station when you deploy.
   - **THE SQUAD** takes the party strip's and the browser's place, from the party's top to the bar: THE SQUAD n / 4,
     "1–4 SIGNED-IN PILOTS · NO BOTS IN A RUN", the ready meter in a room, then a tall card per seat. At 1080p a card is
     217×584 and its portrait 215×475, four times the strip's.
     - **A pilot:** its live 3D portrait standing in a shaft of its own light, the accent pooled at its feet with a ring,
       motes rising. The seat number and role (HOST with a crown, YOU, PILOT) sit at the top. At the foot the plate rises out
       of the dark: the name, the weapon and class with the class line, then the ready pill (a room) or LOADOUT (offline).
       Your card is bracketed and lifts on hover; a click opens the loadout.
     - **An open seat:** an empty pad, a ring pulsing outward on the floor under a faint shaft. The first offers COPY INVITE
       or COPY CODE in a room, or HOST ROOM offline.
     - The cards rise in one after another. A portrait keeps one size while it shows (the renderer keeps one texture per
       pilot), so the squad fades in rather than growing out of the strip.
   - **The side panel** takes the mode panel's place at the squad's full height: MODE and its tiles, then THE RUN, tagged
     ROLLED WHEN YOU DEPLOY. Below that: PVE · CO-OP RUN, the title EXPEDITION, the tag line, the party, length and NEW
     CITY EVERY RUN, and HOW A RUN GOES: 01 Deployment Station, 02 The City, 03 Three Areas, 04 The Last Fight. A light runs
     down the steps' spine, resting at each step. A short canvas uses the tight briefing (`BriefFor`). The LOADOUT row
     reads NO BOTS IN A RUN.
3. **BOSS BATTLE: the party strip, the roster and the fight.**
   - **Party strip**, PARTY n / 4. There is one card per member: the live 3D portrait, the seat number, the role, the name,
     and the weapon and class. A room shows each member's ready pill and the READY meter. Bots are for the Siren only
     (`NativeExpeditionCatalogue.BotsAllowed`): her open seats offer ADD BOT and the newest bot has ×. A zone boss's strip
     reads "1–4 PILOTS · NO BOTS FOR THIS BOSS" and its open seat offers only the invite or HOST ROOM.
   - **The roster**, with the counter, ‹ › and the Q/E keys:
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
   - **MODE** (TAB switches): the two tiles, then THE FIGHT: what this boss is and who can fight it (the Siren with bots;
     the zone bosses 1–4 pilots, no hub, quota or roll; NOCTIEL in development). The LOADOUT row reads NO BOTS · 1–4 PILOTS
     for a zone boss.
4. **MODE tiles** (both modes): **START EXPEDITION** (1–4 PLAYERS) and **BOSS BATTLE** (SOLO OR CO-OP). The picked tile has
   a slow sheen.
5. **Action bar.**
   - Not in a room: a SOLO | HOST ROOM | JOIN ROOM segment.
   - In a room: the room code, the LAN line, CODE, INVITE (LAN host) and LEAVE ROOM.
   - Then a status line: an error, the launch's reason, or who the room is waiting for.
   - Then the launch button: START EXPEDITION, or FIGHT and the boss's name, with its detail line (BOTS FILL SEATS for the
     Siren, 1–4 PILOTS for a zone boss) and ENTER. A guest gets READY (or CANCEL READY) and SEND LOADOUT instead.
6. **LOADOUT drawer.** Party bots (0–3) and "Party bots use my weapon" for the Siren; for a zone boss or the run, a line
   saying they take no bots. Then the class and weapon pickers.

Fit: the layout is laid out in a design space at least 1240×720 and scales the canvas down below that. At ultrawide the
content is capped at 1760 units and centred. Short canvases use a compact card, squad plate, briefing, rail, mode panel and
bar. The checks hold every part inside its region from 1280×720 at 1.5 to 3440×1440 and 4K, and every squad portrait at
least twice the strip's.

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
| THE SIREN | Direct (FIGHT NOW) | The boss-test match started at her: `SelectBossBattle("siren")`, `ApplyRule(SkipWaves,1)`, `prefs.pve=true`, `RequestStage(StageLoadKind.Boss,StartMatch)`. No waves. Solo with bots, or co-op through a room. |
| THE 6:47 | Direct (FIGHT NOW) | Astra's `StartBossBattle("station")`: its arena, its arrival, phases and song. 1–4 pilots, no bots. |
| THE CONVERTER | Direct (FIGHT NOW) | `StartBossBattle("converter")`, as above. |
| THE HUM | Direct (FIGHT NOW) | `StartBossBattle("hum")`, as above. |
| THE DEEP END | Direct (FIGHT NOW) | `StartBossBattle("deepend")`, as above. |
| NOCTIEL | Preview | Disabled. Presentation only behind DEV panel · Camera · Final boss preview until its rules are bound. |

## Boss Battle direct starts (handoff to Astra)

**Implemented 2026-11-06 (Astra):** `station`, `converter`, `hum`, `deepend` are Direct. The runtime creates the selected instance arena without a run/station, uses run-start party scaling and normal arrival/phase authority, and returns to this screen on victory/wipe. Offline Restart repeats the selected fight. These four support 1–4 humans and **no party bots**; Siren retains bots. `NativeRoomOptions.boss` carries the host pick with missing/empty = `siren`; guests already read it. Claude: update bot chips/controls and general copy (done 2026-11-06, `claude/expedition-squad`). See DEV_LOG/task PR for verification and delivery status. The original handoff below is historical.


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
