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
| Each mode's pick; an expedition's pick at Deploy | `Core/NativeRoomPick.cs`; `Runtime/NativeExpeditionWeaponPick.cs` |

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

## Weapon flourishes

Owner, 2026-11-18: "Give each weapon choice a selection animation for the player (Of course, everyone is on their own screen
so others wont see this) The animations should look very nice, to the give players the "Oh that looks cool!" impression."

- **When.** Choosing a card (a click, the arrow keys or a controller click) plays that weapon's flourish, 1.6-1.95 s.
  Clicking the chosen card again replays it; choosing another card stops the old one at once and starts the new one there.
  Locking in plays a short finisher (0.95 s) on the locked-in card. Against bots that screen is skipped (lock-in goes straight
  to the reveal), so the finisher plays on the pilot's own card in the reveal instead, unless the locked-in screen just played
  it. The other pilots' cards in the reveal never play anything: each pilot only sees their own.
- **Both pick screens.** Blind Pick All's grid and Blind Pick's dealt cards play them the same way. The dealt cards are hero
  cards: a hero card plays the card's whole part (the icon leaving its slot, the Katana's split, L33T's glitch) and keeps a
  grid card's measure, since its icon is far larger, while starting and ending at its own icon. Its shots, bolts and arrow
  aim at the other dealt cards. A locked dealt card plays the finisher until the next view arrives, and the locked-in screen
  or the reveal carries it on.
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

## Each mode's own pick (2026-11-19)

Owner, 2026-11-19: "Its pulling chip selection logic from play vs bots (So if people select Blind pick all or anything else,
its changing expedition pick logic.) I want Blind Pick choice with 5 options by default for expedition."

The Weapon pick (`blindPick`, `blindChoices`) is the Play vs Bots and versus rooms' setting. `Core/NativeRoomPick` decides which
pick each mode's opening asks the rules for:

| Mode | Pick | Where its setting lives |
| --- | --- | --- |
| Versus (Play vs Bots, a versus room) | Off, Blind Pick All or Blind Pick, as set | the player's preferences, as before |
| Expedition | always Blind Pick, 5 choices by default | the host's `expeditionChoices` (this session), sent to guests in the room options' own `blindPick` / `blindChoices` |
| Boss Battle | the lobby loadouts (no opening) | none |

- A rules change is stored for its own mode only (`NativeRoomPick.Store` in `SetRoomOptions`). Switching modes stores nothing,
  so Blind Pick All in Play vs Bots never reaches an expedition, and an expedition's count never reaches Play vs Bots.
- The co-op room's old Blind Pick All box wrote the Play vs Bots setting. It is gone. An expedition room has a Weapon choices row
  in its place (2 up to the ordinary pool, 5 marked default); a Boss Battle room has none.
- **Where an expedition picks:** at the Deployment Station, when the host presses Deploy, before the eject
  (`Runtime/NativeExpeditionWeaponPick.cs`, `DeployWithPick`). The eject, the cradle card and the drop then run unbroken, and
  the pods carry the picks. A run started without a station (a direct start) picks as it loads, as a versus match does. A
  run's Restart keeps the weapons.
- **How:** exactly the versus Blind Pick. The host deals each pilot's private offers from its own random stream (not per
  client), each client draws only its own view and sends `opening-choice`, the host checks and locks, a random offer at 0 s,
  then the 2 s reveal. Then the host keeps each pick on its seat (`ApplyOpeningWeapon`) and deploys.
- The screen is Blind Pick's dealt cards with the weapon flourishes, headed EXPEDITION · BLIND PICK · N CHOICES. Like the
  versus weapon pick, it has no 3D pilot (the pilot model is on the Deathmatch chip stage only).
- A co-op room's lobby shows every pilot's own look: the lobby rows are no longer swapped to the pistol and idle pose there.

## Notes

- Rotated layers (the reticles, the stamp, the floor rails, a dealt card in flight) are drawn outside every `U.BeginBand`
  clip. IMGUI maps a clip through the matrix current at draw time (see `NativeLobbyUI`'s fold chevron).
- The views carry ids from the host, so lookups here never throw (`OpeningWeapon` / `OpeningChip`).
- The comeback picker resets nothing of the arena's presentation, so the fight's corpses, blood, wounds and gibs stay. Only the
  stage restarts (`RestartDraftStage`). Every other renderer is hidden while it shows and put back as it was. The room's
  starting chip keeps the PvP draft's reset.
- Still to check in a Windows build: a host and a guest with different weapons (including a timeout), a team game, deferred
  picks, the winning killcam and a rematch.
