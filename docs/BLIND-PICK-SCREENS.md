# Blind Pick All, Blind Pick and Deathmatch screens

Owner, 2026-11-17: "Do the UI work; We can use the same kind of screens we use for chip selection. (May have to design
unique looks for weapon selection) Kind of like chips, but animated and styled with very high quality."

Owner, 2026-11-18: "For the deathmatch mode, For the chip selection; We can bring the player model in for the chip selection
like we do for the normal pvp mode. As matter of fact, we can use that exact screen. Also, change current Blind Pick Name to
"Blind Pick All", and make a new blind pick where we get a customizable amount of choices (Default 3) of weapon/class to
pick (we only pick 1). This will be similar to a chip screen."

This began as Astra's Claude handoff from 2026-11-16 (DEV_LOG, "Blind Pick and Deathmatch"). It is presentation only. The
opening draft (`Core/NativeOpeningDraft`), its messages, the 20-second steps, the two-second reveal, the respawn timing and
the comeback chip install are unchanged.

Players read the original mode (every allowed weapon, in a grid) as **Blind Pick All**. Its ids, fields and messages stay
`blindPick`. **Blind Pick** (N choices) is the new mode. Its rules are Astra's (PR #411, `Core/NativeOpeningDraft`):
- the room's `blindChoices`, where 0 means Blind Pick All, normalised by `OfferCount` (default 3);
- the host's private deal per seat (`WeaponOffers`, sent only in that seat's view by `CardsFor`);
- the pick checked against the deal;
- a random offer locked when the 20 s run out;
- the dealt protocol's version gate (`DealtProtocol`, `ProtocolRefusal`).

## Where

| Screen | Code |
| --- | --- |
| Weapon cards (grid, hero, back, LOCKED IN stamp) | `Runtime/NativeWeaponCard.cs` |
| Blind Pick All's weapon pick, locked-in wait, reveal | `Runtime/NativeOpeningDraftUI.cs`, from `OpeningGui` down |
| Blind Pick's dealt weapons (N choices) | `Runtime/NativeBlindChoiceUI.cs`; layout, deal and hook in `Core/NativeBlindChoices.cs` |
| Starting chip and comeback chip, on the PvP draft's stage | `Runtime/NativeDeathmatchPickStage.cs`; follow-through rule `Core/NativePickFollow.cs` |
| Respawn countdown, comeback prompt, score panel | `Runtime/NativeDeathmatchRuntime.cs`, from `DeathmatchPickGui` down |
| Room options and lobby lines | `Runtime/NativeRoomOptionsUI.cs` (`BlindPickRows`), `NativeLobbyUI.cs`, `NativeLobbyRoomUI.cs` |

## The screens

- **Blind Pick All: weapon pick.** Every weapon the pilot may use is grouped by class (Ranged, Magic, Melee, then DEV) and
  drawn as an animated card in that weapon's colour.
  - Each card shows its class line, its icon on a turning reticle, and its name.
  - The cards boot in behind a scan wipe.
  - The highlighted card lifts, scans and flickers, using the chip tray's highlight timing (`NativeHoloScan`). The other
    cards recede.
  - The current choice wears the tray's brackets.
  - A hero card on the right previews the highlighted weapon, or shows the choice when nothing is highlighted. It has:
    - a holo emitter with a beam, a floor ring, reticles and a pulse;
    - the name, with the HUD's event glitch when the weapon changes;
    - the role line;
    - relative Power, Rate and Reach bars;
    - the exact stat line from the loadout strip;
    - the description.
  - Lock in is a bar in the chosen weapon's colour.
  - Top right shows the locked-in pips and count, and a countdown ring that turns red in the last five seconds.
  - Controls: a click chooses (unconfirmed, as before). Lock in or Enter confirms. The arrow keys move through the grid.
    Controller pointer clicks work through `U.SilentButton`.
- **Blind Pick: N dealt weapons.** Like the chip screen:
  - The pilot's N weapons are dealt one by one from a deck under the screen. Each card flies face down to its place, then
    flips face up with a flash.
  - Each face is the weapon's hero card (stats, role, description). When many are dealt, it is the compact grid card.
  - The cards sit in rows like the chip tray, a short row centred.
  - They use the grid's highlight, choice and Lock in. A click, 1-9 or the arrows choose; Lock in or Enter confirms.
  - The choice wears YOUR CHOICE. Each card has its number key under it.
  - Nothing is marked until the pilot chooses. The rules lock a random offer when time runs out, so the timer reads RANDOM PICK
    AT 0, and an unconfirmed choice is not kept.
  - `Look.SelectAge` starts from 0 on each choice and `LockAge` on the lock, as in the grid. `NativeWeaponCard.Flourishes()`
    runs after the cards.
  - The screen reads only `NativeBlindChoiceView`, built from the host's private Weapon-step view: my offers, the locked-in
    count and the time.
  - Room options: Weapon pick (Off, Blind Pick All, Blind Pick) and, for Blind Pick, Weapon choices (2 up to the pool).
- **Starting chip and comeback chip.** Both use the PvP chip draft's own screen with the pilot, as the Siren personal pick
  does:
  - the draft stage with the local pilot on its model, the cyber corridor, and the gaze and reach toward the highlighted card;
  - the draft's shades, header, tray low in front of the pilot, nameplate, and the 1-9, arrows and Enter keys;
  - a pick plays the draft's follow-through: the others shatter, the card flies to the palm and the pilot plants it.
    - Any key or click ends the follow-through, and so do an attack and the step's end.
    - The pilot's respawn ends it at once, so the stage never holds a respawn.
  - The starting chip's top right shows the opening's locked-in count and the 20 s countdown in the draft's pills. The
    nameplate carries the locked weapon.
  - The comeback chip's top right shows YOU ARE VULNERABLE WHILE CHOOSING when alive, and any queued picks. The respawn pill
    shows top centre while the pilot is down. It also has Choose later (C) and the attack alarm.
  - In the fight the arena is hidden while the picker is open and keeps simulating.
  - The picker that opens on a rewarded death stands in front of that death's killcam, as the tray did. A running killcam ends;
    one not yet started waits until the picker closes.
- **Locked in.** The pilot's own hero card, stamped LOCKED IN. The starting chip they picked sits beside it, and a row of
  pips shows who is still choosing.
- **Reveal (2 s).** Every pilot's card, bots included, flips from a CLASSIFIED back to its face in turn. Each card has a
  nameplate in the pilot's lobby colour. The host's rows are `name · weapon`. The headers name the session's mode: BLIND PICK
  ALL, BLIND PICK, or DEATHMATCH for a starting chip alone.
- **Respawn.** A red edge, ELIMINATED, the seconds in a draining ring, and a bar. The ring spans `NativeDeathmatch`'s 3 s.
  While the picker is open, a compact pill replaces it.
- **Comeback prompt.** The C keycap flares magenta when an offer arrives. It sits above the vitals panel.
- **Score panel.** FIRST TO n, and for each side: rank, colour, name, kills and a bar toward the target, with the leader
  marked. A row flashes and its count pops when that side scores.

Reduced effects stops the motion, flicker, sheen and pulses, and deals the cards in place.

## Notes

- Rotated layers (the reticles, the stamp, the floor rails, a dealt card in flight) are drawn outside every `U.BeginBand`
  clip. IMGUI maps a clip through the matrix current at draw time (see `NativeLobbyUI`'s fold chevron).
- The views carry ids from the host, so lookups here never throw (`OpeningWeapon` / `OpeningChip`).
- Opening the comeback picker resets the arena's transient presentation (corpses, blood, wounds), as the Siren pick's screen
  does.
- Still to check in a Windows build: a host and a guest with different weapons (including a timeout), a team game, deferred
  picks, the winning killcam and a rematch.
