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

## Weapon flourishes

Owner, 2026-11-18: "Give each weapon choice a selection animation for the player (Of course, everyone is on their own screen
so others wont see this) The animations should look very nice, to the give players the "Oh that looks cool!" impression."

- **When.** Choosing a card (a click, the arrow keys or a controller click) plays that weapon's flourish, 1.6-1.95 s.
  Clicking the chosen card again replays it; choosing another card stops the old one at once and starts the new one there.
  Locking in plays a short finisher (0.95 s) on the locked-in card. Against bots that screen is skipped (lock-in goes straight
  to the reveal), so the finisher plays on the pilot's own card in the reveal instead, unless the locked-in screen just played
  it. The other pilots' cards in the reveal never play anything: each pilot only sees their own.
- **How.** `Runtime/NativeWeaponFlourish.cs` and `NativeWeaponFlourish.Moves.cs` (`NativeWeaponCard` is partial). `Draw` and
  `Hero` queue the flourish of the chosen card (`Look.Selected`, `SelectAge`) or the locked one (`Locked`, `LockAge`; `Finish`
  for the reveal's own card, `Quiet` for the others). `NativeWeaponCard.Flourishes()` draws them once per frame after every
  card, over the whole screen. The card's own part (its shake, squash, cracks, split, the icon leaving its slot) plays only
  while `Flourishes()` drew that flourish, so a screen that never calls it shows nothing.
- **Look.** Hardlight copies of the weapon's own icon art (the raster the card already drew, so nothing rasterises during a
  flourish), with light, trails, sparks and particles. Every particle is a closed-form function of the flourish's age: no
  per-frame state or allocation, and a restart is clean. Targets come from the cards drawn that frame (the shots land on other
  cards, the lightning chains through them, the arrow hits the hero card's frame).
- **Sound.** A few short, quiet cues per move (`Core/NativeWeaponFlourishPlan`: an effects bank key with today's clip as its
  fallback, or a Top Dog toon sound), played through NativeAudio's screen cue path, each once.
- **Reduced effects.** A gentle glow and ring on the icon instead; nothing moves or flashes.
- **Timing.** Nothing waits on a flourish: the pick, the countdown, the lock and the reveal run exactly as before.

| Weapon | Selection flourish |
| --- | --- |
| Pistol | Spins out of the icon, three shots round the screen (the last after turning round), bullet holes on other cards, brass, a gunslinger's twirl home. |
| Rifle | Rises and aims at the farthest card; the screen darkens round a scope that swings onto it and locks; one shot across the grid, a smoke trail, the target cracks; the bolt cycles. |
| Machine Gun | A sweeping spray of tracers with a fountain of brass bouncing on the card, a hot barrel that smokes. |
| Fire | The card catches fire; a fireball gathers over it and arcs across the grid into a burst of embers and smoke. |
| Water | A globe wells out of the icon, wobbles, bursts into a crown of droplets that splash round the card; the card jiggles. |
| Lightning | The card crackles, then a bolt forks from card to card down the grid, each struck card flashing. |
| Darkness | A singularity opens on the icon (its disc turning, motes spiralling in), collapses, and throws out a dark ring. |
| One-Handed Sword | Spins out; a cross cut, a backhand and an overhead across the card, each leaving a mark and sparks; sheathed home. |
| Daggers | The crossed pair twirls round the card like a pinwheel, splits, both blades thunk into the card's top corners and quiver, then are pulled home. |
| Shield | Wound back, then charges and bashes into a wall of hardlight hexes; rebounds and plants with a hex dome. |
| Greatsword | Hauled up, turned point down, slammed into the card: an impact flash, the card cracks and squashes, chunks and dust fly; wrenched out, the cracks heal. |
| Bow | Turned on the hero card and drawn, loosed: the arrow arcs across and thunks into the hero card's frame, quivers and breaks into light. |
| Katana | Laid across the card in stillness and falling petals; one iai cut and the card parts along it for a frozen beat, closes, and the blade slides home with a click; petals burst. |
| Glaive | The ring blade spins up and is thrown round the grid on a wide boomerang loop with a ribbon of light, then caught back in the card. |
| Fangs | Two venom blades rake an X across the card; the cuts glow and drip venom down it, bubbling. |
| DEV Greatsword | Raised in a glitch and plunged under the card: Earthsplitter's fissure races across the whole screen, the floor erupting; it glitches home. |
| DEV Katana | The screen dims under a rising moon; a flurry of cuts, a final cross, then a spiral storm of petals and DEV hexes; sheathed. |
| Bombadier | The TNT hops out with its fuse fizzing, swells and flashes with each tick, then BOOM: flash, rings, fire, smoke and confetti over the grid. |
| Top Dog | BOING out of the card, two hops and a backflip along the cards, back up with the Big Mallet: BONK! The card squashes flat and springs back, dizzy stars, a squiggle of mustard. |
| Siren | The card darkens and its eyes burn, three claws rake it, and violet hellfire climbs it. |
| Emperor Style | The crown rises and lightning strikes it, a left and a right electric punch rock the card, an electric wind hook spirals up round it. |
| Ansatsuken | The fist draws back and gathers a ball of blue energy, thrusts it across the screen in a rippling wave, then rises in a spinning uppercut. |
| L33T H4x | The card is hacked (its slices tear sideways), binary pours round it, three bit bolts launch, pause on their marks and strike; ACCESS GRANTED types out. |
| Cyber Lord | Hardlight hexes ripple over the card, the fist data-dashes out and back in afterimages, a firewall of hexes rises and bursts into packets. |

Each finisher is a light pillar, a flash and two rings on the locked card, with the weapon's signature in small (casings for
the guns, a slash for the blades, embers, droplets, arcs, a hex dome, an arrow up the beam, petals, confetti, dizzy stars,
claws, a bolt, a ki ring, binary, packets).

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
