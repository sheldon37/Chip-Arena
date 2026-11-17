# Blind Pick and Deathmatch screens

Owner, 2026-11-17: "Do the UI work; We can use the same kind of screens we use for chip selection. (May have to design
unique looks for weapon selection) Kind of like chips, but animated and styled with very high quality."

This is Astra's Claude handoff from 2026-11-16 (DEV_LOG, "Blind Pick and Deathmatch"). It is presentation only. The opening
draft (`Core/NativeOpeningDraft`), its messages, the 20-second steps, the two-second reveal, the respawn timing and the
comeback chip install are unchanged.

## Where

| Screen | Code |
| --- | --- |
| Weapon cards (grid, hero, back, LOCKED IN stamp) | `Runtime/NativeWeaponCard.cs` |
| Weapon pick, starting chip, locked-in wait, reveal | `Runtime/NativeOpeningDraftUI.cs`, from `OpeningGui` down |
| Respawn countdown, comeback prompt and picker, score panel | `Runtime/NativeDeathmatchRuntime.cs`, from `DeathmatchPickGui` down |

## The screens

- **Weapon pick.** Every weapon the pilot may use is grouped by class (Ranged, Magic, Melee, then DEV) and drawn as an
  animated card in that weapon's colour.
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
- **Starting chip.** The chip draft's own tray: the same boot, holo highlight, 1–9 keys, arrows and Enter. The locked weapon
  shows as a tag.
- **Locked in.** The pilot's own hero card, stamped LOCKED IN. The starting chip they picked sits beside it, and a row of
  pips shows who is still choosing.
- **Reveal (2 s).** Every pilot's card, bots included, flips from a CLASSIFIED back to its face in turn. Each card has a
  nameplate in the pilot's lobby colour. The host's rows are `name · weapon`.
- **Respawn.** A red edge, ELIMINATED, the seconds in a draining ring, and a bar. The ring spans `NativeDeathmatch`'s 3 s.
  While the picker is open, a compact pill replaces it.
- **Comeback chip.** A prompt with the C keycap that flares magenta when an offer arrives. The picker uses the chip tray over
  a shade that keeps the arena in view, with Choose later.
- **Score panel.** FIRST TO n, and for each side: rank, colour, name, kills and a bar toward the target, with the leader
  marked. A row flashes and its count pops when that side scores.

Reduced effects stops the motion, flicker, sheen and pulses.

## Notes

- Rotated layers (the reticles, the stamp, the floor rails) are drawn outside every `U.BeginBand` clip. IMGUI maps a clip
  through the matrix current at draw time (see `NativeLobbyUI`'s fold chevron).
- The views carry ids from the host, so lookups here never throw (`OpeningWeapon` / `OpeningChip`).
- Not yet seen in a Unity player. Check in the Editor or a Windows build:
  - the layout at 16:9, 21:9 and 4:3, and at 1280x720;
  - a host and a guest with different weapons, including a timeout;
  - a team game;
  - deferred picks;
  - the winning killcam and a rematch.
