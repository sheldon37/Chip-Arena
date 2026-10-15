# Expeditions screen (Unity menu)

Owner request (2026-10-15): redesign the "Start Expedition" screen. Expeditions replace the single boss
fight for testing. The screen should be modern, multiplayer capable, have "Play Wave Defense" for single
waves, show the party at the top and have a "Select Expedition" browser. Placeholders are fine for now.

Status: implemented on `claude/expeditions-screen`. It is presentation only, checked with managed checks and
offline renders. It has not been seen in Unity or on Windows.

## Where it lives

| Piece | File |
| --- | --- |
| Catalogue: names, lines, placeholders. Rename things here. | `Core/NativeExpeditionCatalogue.cs` |
| Rules (modes, wave mapping, launch gates) and layout | `Core/NativeExpeditionsMenu.cs` |
| Drawing, input and actions | `Runtime/NativeExpeditionsMenu.cs` |
| Entry points | `NativeHomeMenu.cs` `BossScreen` (Start Expedition, home key 1); `NativeLobbyUI.cs` `LobbyScreen` (a PvE room) |
| Checks | `Tests/ExpeditionsMenuChecks.cs` (`CoreTests --expeditions-menu`, also in the full run) |

The catalogue holds display text only. The ids the game uses stay the layout's: world `exp-city`, area
`exp-ward-zero`, and maps `expedition-city` and `expedition-ward-zero`. A check fails if a live area's
layout id or map does not exist.

## Layout

The screen is a full-canvas cyber layout over the panel screens' shade, with a void veil, a violet pool and
a slow scan band. From top to bottom:

1. **Header.** PVE · EXPEDITIONS, the title, and a seat chip: SOLO, HOSTING · ROOM code, or GUEST · ROOM
   code. At the right: Customize and Settings (both return here), Report in a room, and the close hexagon
   when not in a room.
2. **Party strip**, PARTY n / 4. There is one card per member: the live 3D portrait (`LobbyPortrait`; the
   emblem until the portrait is ready), the seat number, the role (HOST with a crown, YOU, BOT, PILOT), the
   name, and the weapon and class. A room shows each member's ready pill and the READY meter. The newest
   bot has × for whoever sets the party.
   - Open seats: the first one offers ADD BOT, plus HOST ROOM when not in a room, or the invite (Copy
     Invite or Copy Code) in a room.
3. **SELECT EXPEDITION.** A carousel with the counter, ‹ › and the Q/E keys. The neighbours show as spines
   with their titles running up them. A selected card shows:
   - **Key art:** the stage scan of the previewed area (`NativeStageSchematic`), with a sweep, and the
     Siren's face above it for her expedition. Hovering a route row previews that area; an area with no map
     shows NO SCAN YET.
   - **Details:** the kicker and threat tags, the title, the tag line, and the hub, party and length.
   - **The route:** HUB, then 01–03, then FINAL. Each row is LIVE or IN DEVELOPMENT.
   - **Placeholders:** a skyline silhouette with SIGNAL LOST, COMING SOON and UNCHARTED rows.
   - When the pick changes, the card turns: the old card narrows and slides out under the spines as the new
     one widens in. With Reduced effects it is a cross-fade instead.
   - Dots under the card show each expedition.
4. **MODE** (TAB switches). Two tiles:
   - **START EXPEDITION**, the full run.
   - **PLAY WAVE DEFENSE**. Selecting it shows the START AT row: WAVE 1, WAVE 2, WAVE 3, SIREN. Keys 1–4
     pick one.
   - Under the tiles, the full run instead shows its notes and the honest reason it cannot start.
   - The LOADOUT row shows the weapon and class, the party bots, and mirror or own loadouts. EDIT or L
     opens the drawer.
5. **Action bar.**
   - Not in a room: a SOLO | HOST ROOM | JOIN ROOM segment.
   - In a room: the room code with how it is reached, the LAN line, CODE, INVITE (LAN host) and LEAVE ROOM.
   - Then a status line: an error, the launch's reason, or who the room is waiting for.
   - Then the launch button: label, detail line and ENTER. A guest gets READY (or CANCEL READY) and SEND
     LOADOUT instead.
6. **LOADOUT drawer.** A sheet from the right, in canvas units, so the GUILayout forms work. It holds party
   bots (0–3), "Party bots use my weapon", and the class and weapon pickers (`ClassRow` / `WeaponCards`).
   A click outside, L, controller X or B, or Escape closes it.

Fit: the layout is laid out in a design space at least 1240×720 and scales the canvas down below that. That
only happens at a large interface scale on a small window: 1280×720 at 1.5 is 0.83. At 900-unit canvases it
is full size. At ultrawide the content is capped at 1760 units and centred. Short canvases use a compact
card, mode panel and bar.

Controls:
- Q/E, A/D or ←/→: turn the carousel.
- Tab, W/S or ↑/↓: switch the mode.
- 1–4: pick the wave start.
- L: open the loadout.
- Enter or Space: launch or Ready.
- Escape: closes the drawer, else goes back (not in a room).
- Controller (read once a frame): LB/RB turn, Y switches mode, X opens the loadout, A launches, B closes or
  goes back. The d-pad is not mapped; the old input manager has no axis for it here.

## What works now

Every action is a call the old screens already made:

| Old | Now |
| --- | --- |
| Start Boss Fight | PLAY WAVE DEFENSE at Wave 1 or the Siren: `prefs.pve=true;sandboxWanted=false;RequestStage(StageLoadKind.Boss,StartMatch)` |
| Start at the Siren | the SIREN wave tile (`ApplyRule(RoomOpt.SkipWaves,…)`) |
| DEV Expedition M1 | START EXPEDITION, solo: `RequestStage(StageLoadKind.Expedition,StartExpeditionM1)`. Still developer-only and offline; `StartExpeditionM1` keeps its own gate. |
| Host Co-op Room | HOST ROOM (`prefs.pve=true;Go(MenuScreen.Create)`). The room created there opens on this screen. |
| Join Room | JOIN ROOM (`Go(MenuScreen.Join)`). A PvE room opens on this screen for the guest. |
| Party bots, mirror weapons, class and weapon | party strip add/remove (`ApplyRule(RoomOpt.Bots,…)`) and the drawer |
| Room lobby: Start, Ready / Cancel Ready, Send Loadout, Copy Code, Copy Invite Link, LAN line, Leave Room, Customize, Settings, Connection report | the action bar and the header |

Transitions title this screen, and a PvE room, EXPEDITIONS / PVE.

## What waits on Astra

- **The full run in a room.** START EXPEDITION is disabled in any room: "Co-op expeditions: waiting on host
  support."
- **Waves 2 and 3** are shown and locked (NEEDS START-WAVE SUPPORT). A room carries only `skipWaves`.
- **The host's pick and mode** do not reach guests. Guests see the default expedition in Wave Defense,
  labelled THE ROOM'S RUN. That is what a room can play today. The wave start does replicate, through
  `skipWaves`.
- No room field or network message was added. A check pins `NativeRoomOptions`' fields.

The contract for these is in DEV_LOG, under "Expeditions screen".
