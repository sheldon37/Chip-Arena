# The PvP room and Create Room

Owner, 2026-11-10: "Improve UI for create room screen and pvp lobby. Lets give players the options to change their weapon abilities in
lobby as well. So yes, overhaul the UI for pvp and create a room. Make them much better quality and a very clean / beautiful design."

Presentation only: every choice goes through the rules and messages the screens always used. No new network message, no rules change.

## The room (Runtime `NativeLobbyUI.cs`, `NativeLobbyRoomUI.cs`)

The room fills the canvas, in the cyber theme (navy glass, cyan accents):

- **Header:** PRIVATE ROOM · Match lobby; the room code chip (the code, how the room is reached, Copy Code and, for a LAN host, Copy
  Invite Link) beside Customize and Settings.
- **Pilots:** a heading with the count, free-for-all or teams, and the READY meter. Then a grid of pilot cards:
  - up to four pilots fill one row of four; five to eight fill two rows;
  - each card has the seat number and what the seat is (YOU, HOST with its gem, BOT, PILOT), the seat's portrait, the name, the weapon
    and its class, both abilities (the technique's colour when one is swapped in), and the ready band (READY, NOT READY or OFFLINE);
  - in a teams room each card carries its team chip, which opens the team list (the viewer's own card, every card for the host);
  - the host may make a guest host, and may take away the room's last bot (its ×);
  - open slots fill the row; the first holds Add a bot for the host. When none shows, Add a bot is beside the READY meter.
- **Your loadout:** the weapon (it opens the weapon picker: class and weapon cards) and the Q and E slots: each shows what is in it, its
  cost, and whether it is the default or a technique; a slot with more than one option opens its picker.
- **The picker sheet:** slides in over the room's right side under a scrim. The ability picker lists every option for the slot (the
  default and the weapon's live techniques: `NativeLocker.SlotOptions` / `AddTechniques`, as the Locker offers them), each with its
  icon, name, cost, what it does and its tag; the equipped one is ticked, and a technique on the other slot says that picking it moves
  it. A pick takes at once and flashes. A click outside, Escape, Backspace or the pad's B closes the sheet; while it is open nothing
  under it takes the pointer or the pad's focus.
- **Match setup:** under the loadout, in its own scroll: the host's Training Room switch, the mode and the rules (the essential rows,
  then Advanced Options), read-only for guests.
- **The bar:** Leave Room; the room's status (who the room waits for; "Sending your loadout…"); Start Match for the host (under the
  loading screen) or Ready / Cancel Ready for a guest.

### How a loadout change reaches the room (`LbSync`)

A third of a second after the last change of weapon or ability:

- the host's own seat goes out with its next lobby broadcast (`LobbyBroadcast`);
- a guest sends its profile (`"profile"`); a guest who is ready sends `"ready"` with the profile instead, which keeps it ready (the
  host re-sanitizes the seat from the profile either way: `SanitizedSeat`, `NativeTechniqueLoadout.Normalize`);
- an ability pick is saved to the pilot as the Locker's Save does (`SavePilot`).

There is no Send Loadout button any more. Nothing changes during a match (the host takes profiles only while `arena==null`).

## Create Room (Runtime `NativeCreateRoomUI.cs`)

Two columns:

- **The setup**, in sections:
  - the mode as three cards (Versus, Boss Test, Expedition);
  - for a versus room, the arena as a grid of tiles: Random first, then every arena in the menu's order, each with its colour and its
    line; picking one also turns the menu backdrop to it;
  - the format: free-for-all or teams as one switch, then the sets, rounds, randomize, bots and their difficulty;
  - the draft and intros;
  - the connection (Online or Local network as a switch, the port for a LAN room).
- **The side:** the pilot as the room will show it (the lobby's card), a live summary of the room (mode, arena, format, bots, how
  friends join), and Create Room.

The rule rows (`NativeRoomOptionsUI.cs`) are in the cyber theme too, so Play vs Bots and the Expeditions screen match.

## Checks

- CoreTests `--transitions` (the room's Start under the loading screen), `--expeditions-menu`, `--techniques`, and the full suite. The
  source pins kept: `if(pve){ExpeditionsScreen(age);return;}`, `("palette","Customize",MenuScreen.Customize)`,
  `float w=ConnectionPillWidth(text);`, `RequestStage(prefs.pve?StageLoadKind.Boss:StageLoadKind.Online,StartMatch)`; no orange.
- The Editor's build checks (the techniques round trip through the profile and frames).

## Not seen / not done

- A guest's view and its automatic sync with a real second client.
- Controller-only use of the sheet (B closes it; focus inside it follows the pad's usual navigation).
- The Locker's own ability page is unchanged (it still saves with Save Pilot).
