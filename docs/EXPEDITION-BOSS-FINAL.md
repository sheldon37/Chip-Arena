# The final boss: LUX CECIDIT (design and Astra contract)

Claude, 2026-11-05. Owner's brief: "Lets begin designing a final boss (One possible option) - Players will be given 5 lives each, with the option to revive eachother provided they run out of lives. Im thinking a fallen angel boss~ There will be a cutscene (Since this boss will be essentially an extreme version). The stage will be pitch black; the floor reacts to steps like players are stepping through water. The stage also reacts to music; Really huge space to run around (monochrome - evil design). The boss is Wielding a giant beautifully designed claymore in one hand corrupted looking. Several across the map attacks, some one hit kill abilities and unique adds they summon. 4 phases- each phase is shown through a very big well designed slash in the screen (camera effect) Which will go to a chip selection screen showing their next phase modifiers. Design can change through the phases as well. We will utilize this song for both their intro and boss music."

**Status: design. Presentation is Claude's; everything marked as a rule is a proposal Astra owns and can change.** Nothing here is built yet; section 14 records what is.

## 1. The owner's decisions

| Question | Decision |
| --- | --- |
| Where it sits | One of several possible final bosses at the end of an Expedition run |
| Revives | At zero lives a teammate holds revive at the body. It costs the reviver one life and gives one back. Everyone at zero loses the run |
| The chip screen | The boss reveals its next modifiers, then players draft one chip each to answer them |
| Size | A fast duelist, about 3.5 m tall, the whole fight |
| Model | Built in Blender on the model's animated skeleton |
| Colour | World and boss pure black and white. Pilots keep their colour |

## 2. The fight in one paragraph

An Expedition ends on black water with nothing else in it. Far off, something white kneels with a sword planted in the water. **NOCTIEL** is the last of a choir whose light went out, and the whole void is where it fell. It stands up, draws, and comes at the party as a 3.5 m duelist: fast, formal, a claymore longer than it is tall held in one hand. Four times it is hurt enough to change. Each time it cuts the screen itself in two, shows the party what it will do next as chips of its own, and the fight resumes on the song's next drop, with less of the angel left.

## 3. Lives and revives (rule, Astra)

- Each pilot enters with **5 lives**, shown as five white feathers on the pilot's plate.
- A death costs a feather. The pilot falls back in from the dark after 3 s, landing with a ripple in a clear spot at least 25 m from the boss, with 1.5 s of protection.
- At zero the body stays on the water. A teammate holds revive within 3 m for 4 s, and the reviver's feather goes to the fallen pilot. You can't give your last feather.
- Everyone at zero loses the run. A lone pilot has the five lives and no revive.
- A revive is interrupted by taking a hit. The boss's stillness rule (section 11) still applies to the reviver, so it can't be done safely forever.

## 4. The body

Frame: the pack skeleton every pilot uses, scaled about 1.9x (about 3.5 m tall) and driven by the pack's clips retimed to these moves, with extra procedural bones for the wings. Authored in Blender (`tools/gear/fallen_boss.py`) into its own local bank `ChipArenaPackBossFallen`, in the Drive zip, never in git.

**Everything is black, white and grey.** Polished porcelain plate over black cloth; the only "colour" is white light from inside.

| Phase | Name | Body | Wings | Halo | Sword |
| --- | --- | --- | --- | --- | --- |
| 1 | GRACE | Whole white plate, long black robe split at the front, face a smooth porcelain mask with no features | Six, feathered, folded close like a cloak | A thin, whole ring behind the head, one crack | Clean pale steel. Black veins only at the hilt |
| 2 | WRATH | Hairline cracks in the plate with light behind them, robe burning away upward into ash | The lower pair sheds into floating blades | Cracked through, tilting | Veins halfway up the blade |
| 3 | RUIN | Chest and left arm plate gone, showing black glass with white light moving inside. The mask has split | Bare bone and chain; feathers gone, the top pair are huge blades | Three shards orbiting | Blade black to the tip, a white edge. Drips upward |
| 4 | LUX CECIDIT | Hollow: a shell of plate round a figure of white light. The mask is gone | A ring of black blades turning behind it, no wings left | Gone. A hole of light where it was | A shard of pure white light in the shape of the blade, with the black steel flaking off it |

**The claymore** is 3.4 m, longer than the wielder is tall, always in the right hand and never two-handed. The crossguard is a pair of broken wings folding forward. The ricasso is wrapped in chain. The fuller carries engraved words, LUX CECIDIT, that fill with black as the veins climb. The pommel is a closed eye that opens in phase 3.

## 5. The arena: the Still Water (rule for its size, presentation for its look)

- A disc about **160 m across**: real room to run, and far bigger than any other Expedition room. Nothing stands on it, so there's no door to get stuck in, no second floor and no cover.
- **The floor** is black mirror water, ankle deep to the eye and solid to the feet. Every footstep, roll, landing, hit and boss step sends a ripple ring that catches a thin white light, then fades. Each pilot leaves a trail, and only pilots' feet and the boss light the floor.
- **The dark** is genuinely black: no sky, no horizon. Light comes from the boss, its sword and the ripples, plus a faint rim on each pilot so you can always see your team.
- **Music:** the floor carries faint concentric rings round the boss that pulse on the song's beats, brighter on bass. On a slam a wide ring runs out across the whole disc. When the bass drops out, the rings go dark and only footsteps remain.
- **The edge** is a soft wall of dark water: you wade slower past 74 m and are pushed back at 80 m.
- **Falling feathers** drift through the dark, more each phase. They catch the ripple light and land with tiny rings.

## 6. The song

`Resources/Audio/music/lux-cecidit.mp3`, the owner's, 4:23, about 152 BPM. Measured, not heard, so the owner confirms these by ear:

| Time | Audio | Use |
| --- | --- | --- |
| 0:04-0:55 | Quiet start, build, bass out 0:39-0:48 | The cutscene, 51 s |
| 0:55-1:22 | The full band | Phase 1 loop |
| 1:22-2:01 | Heavy bass | Phase 2 loop |
| 2:01-2:25 | Breakdown, bass gone from 2:15 | Under each chip screen |
| 2:25-2:45 | Bass slams back | Phase 3 loop |
| 2:45-3:45 | The loudest part | Phase 4 loop |
| 3:45-4:23 | Bass out, a big hit at 4:08, fade from 4:14 | The death scene |

A phase loops its section on bar lines. A phase change lands on the next bar, plays the breakdown under the chip screen, and the next phase's drop lands as the screen closes. `tools/zone_boss_beatmap.py` measures the grid and accents; attacks can land on the song's accents as THE HUM's do.

## 7. The cutscene "The light fell" (0:04-0:55, skippable by vote once a party has seen it)

| Song | Shot |
| --- | --- |
| 0:04-0:15 | Pure black. One white feather falls through the dark, lit by nothing. It touches water and one ripple runs out to the edge of the frame |
| 0:15-0:28 | Low over the water, moving slowly. The party's own ripples behind us. Far away, one small white shape kneeling, a line standing up beside it |
| 0:28-0:39 | The bass enters. Close: a white gauntlet on the hilt. Black veins crawl up the blade. The crack runs through the halo |
| 0:39-0:48 | The bass drops out, nearly silent. It rises. Each pair of wings opens in turn, each throwing a slow ring across the water. Long lens, its whole height against black |
| 0:48-0:55 | The bass returns. The blank mask lifts toward us. It pulls the sword out of the water one-handed, water running upward off the blade. It raises it |
| 0:54.8 | The drop: it cuts down at the lens. **The Slash** (section 10) splits the frame. The card: NOCTIEL, "THE LIGHT THAT FELL". The two halves slide away onto the party's view and phase 1 begins |

## 8. The moves (rule; every number is a proposal)

Damage is before party scaling. **OHK** moves take a whole life whatever your health, and each has a telegraph of at least 2 s that any pilot can walk out of. Rolls and blocks don't save you from an OHK; moving does.

| Move | Phase | What happens | Tell | Numbers |
| --- | --- | --- | --- | --- |
| **Cleave** | 1 | A three-cut string, each with a step | The blade drawn back along the arm | 8 m arc, 26 each |
| **Vow** | 1 | A straight thrust, crossing the distance in a blink | It sets the point at you and the water ahead parts in a line | 35 m lane, 3 m wide, 34 |
| **Severance** | 1 | A flat swing throws a crescent of black and white that crosses the entire map | Blade held wide, the ring at its feet flattens | 2.5 m tall, crosses 160 m at 34 m/s. Jump or roll. 30 |
| **Descent** | 1 | It leaps into the dark and lands on the farthest pilot | A white ring closing on them | 1.4 s, 9 m radius, 38 |
| **Hush** | All | The stillness rule, section 11 | The water stills round you and goes bright | 6 s still, 4 s in phase 4 |
| **Feather Rain** | 2 | The wings shed blades that fall in crossing lanes over the whole map | Thin lines of light on the water, then the feathers | 8 lanes, each 2.5 m wide, 18 |
| **Verdict** | 2 | **OHK.** It plants the sword. A crack of white draws across the entire map through one pilot, then the map splits along it | The crack, brightening for 2.4 s | 3 m wide, 160 m long |
| **Choir** | 2 | Summons Cantors | It lifts its off hand | Section 9 |
| **Wing Sweep** | 3 | Its bare top wings sweep a full circle | They spread and shake | 28 m radius, 3 m tall. Jump. 34 |
| **Eclipse** | 3 | **OHK.** All light goes out. Pools of light open on the water, and outside them you die | The dark rolls in from the edge for 3.2 s | Always one pool within 14 m of each pilot, 6 m radius |
| **Penance** | 3 | Summons Penitents | It pours from the blade | Section 9 |
| **Fall of Light** | 4 | **OHK.** Columns of white fall in a pattern on the song's beat, leaving a route | Bright squares on the water, one bar ahead | Each column 8 m, 1 bar warning |
| **Last Verdict** | 4 | **OHK.** Three cracks at once, a star through the whole map | As Verdict, 2.8 s | 3 m wide |
| **The Light Goes Out** | Enrage | At 12:00 the water goes white, then black | The song's hit at 4:08 | Kills everyone |

A phase keeps every move before it, and its OHKs are never closer than 14 s apart.

**Health:** phases start at 100%, 70%, 40% and 15%. Proposal: `NativePveRules.BossHp` x2.5.

## 9. The adds (rule; models are Claude's)

- **Cantors** (phase 2): hollow white robes hanging in the air with no one inside, mouths open. They sing a thin beam at the nearest pilot that strengthens the longer it holds. Low health. The answer is to kill them. Two per pilot.
- **Penitents** (phase 3): pale kneeling husks that rise from the water round each pilot, never from a fixed place. They crawl fast and latch on, slowing you until shaken off. Low health. Three per pilot.
- **Thrones** (phase 4): a ring of black blades round one white eye, hanging high. Turns slowly and shoots down at whoever it sees, and takes real damage to bring down. One per two pilots.

Adds stay in the same monochrome and ripple the water under them.

## 10. The Slash and the chip screen

**The Slash** is the fight's signature. It is a screen-space effect on every client, about 1.2 s:

1. The boss drops to one knee at the threshold, invulnerable, and the music cuts to the next bar line.
2. It rises and swings at the camera. Time slows hard.
3. A white line opens across the screen on a diagonal, drawn from the point of the swing and thickening from nothing.
4. The two halves of the frame slide along the cut, ink spraying from the line in black and white.
5. White flash on the bar line, then black. The chip screen rises out of the black.

**The chip screen** uses the existing chip draft. First the boss's three modifiers turn over as black chips on the left, one at a time on the beat, under the phase's name. Then each pilot drafts one chip from the normal offer to answer them. When the timer ends, the two halves close, and the drop hits. The next phase's design is on screen.

**Boss chips** (rule, Astra; drawn by seed):

| Chip | Effect |
| --- | --- |
| Second Wing | One more Severance wave |
| No Shade | The Hush takes 1.5 s less |
| Full Choir | One more add each summon |
| Unforgiven | A revive takes 2 s longer |
| Haste | Windups 12% shorter |
| Long Reach | Cleave and Wing Sweep 20% wider |
| Heavy Hand | Melee hits 15% harder |
| Black Water | Ripples fade faster and the floor gets darker |

## 11. No safe place (rule, Astra)

The same bar as `docs/EXPEDITION-BOSS-BACKROOMS.md` section 1.

| The cheese | The answer |
| --- | --- |
| A door the boss can't fit through, a wall, a second floor | None exist. The whole arena is one open disc with no collision on it |
| The boss too big or stuck | It's 3.5 m on an open floor. It moves on an authority path and teleports with Descent, never by navigation |
| Standing still | **Hush:** still within 3 m for 6 s and the water opens under you, 40 damage |
| Kiting at range | Severance, Verdict and Feather Rain cross the whole map, and Descent picks the farthest pilot |
| Hugging it | Cleave, and a 5 m stamp when it's crowded for 3 s |
| Running to the edge | Slower past 74 m, pushed back at 80 m, and Hush is 3 s there |
| Jumping to dodge everything | Airborne doesn't dodge Verdict, Eclipse or the Fall of Light |
| Hit and hide | Nowhere to hide, plus the 12:00 enrage |
| Out of the map | Volume check each second: returned to the water with a hit |

## 12. Animation

The pack's sword clips carry it, retimed to the move table: a long, slow windup to just before the clip's hit, the hit itself almost at once, then a slow settle (Core `NativeFallenBossPreview.ClipTime`). Two-handed clips keep its left arm free: the claymore is never held in both hands. On top of the clips:

- **Standing:** upright, the sword held low and forward by arm IK, its point near the water, the head turning after the pilot it's hunting. It holds very still, then moves all at once.
- **The knee:** a true one-knee kneel set on the muscles (no clip has one), used for the cutscene, each threshold and the death. The fist closes on the planted sword by IK.
- **Gliding:** a dash leaves the water, feet off it, with a wake laid behind.
- **Wings:** rooted on the chest but square to the body so the pair stays a pair, opening and flaring on an underdamped spring, thrown past their mark and shivering back, each pair behind the one above; they lag turns and blow back with speed.
- **Robe, halo, ring of blades:** the strips hang by their own weight and swing with its speed; the halo tilts by phase; the blades turn.
- **The sword** trails a thin bright edge on every fast swing and drags a wake when its point meets the water.

Impact on rules: none until bound. All hit timing comes from the move table, and each preview move lands on a song beat.

## 13. Astra's contract (proposal)

1. **Flow.** A final area `expedition-lux-cecidit` as one option at the end of a run, with the Still Water arena (Core `NativeFallenArena`, a flat disc, the edge rules in section 5).
2. **Archetype** `final-noctiel` on the zone boss path: `NativeFallenBoss.Moves`, phases at 70/40/15%, its name and views bound to `FallenBossViewOf`.
3. **Lives and revives,** section 3, as a replicated per-seat count.
4. **Record fields** beside `NativeZoneBossFrame`: `phaseChangeAt`, `chipOffer[]`, the crack lines (`verdicts`), Eclipse's pools, Fall of Light's pattern seed, and a `hush[]` per-seat meter.
5. **The chip screen:** boss modifiers from a seeded table, then the existing draft, held while the phase changes.
6. **Song clock:** phase changes wait for the next bar, on `NativeZoneBossSong`.
7. **Acceptance:** the safe spot sweep, one of each OHK being survivable by walking, and revive costs.

## 14. Built

**A DEV preview, on the test channel when its build's receipt below says so: nothing in play.** DEV panel · Camera · **Final boss preview**, on any map. It plays a scripted fight on the whole song, on a black stage of its own far below the level (2 km down), and restores the level's look when it stops. No rule reads it.

| Part | Where |
| --- | --- |
| The model, four phases, the adds | `tools/gear/fallen_boss.py`, into the local bank `ChipArenaPackBossFallen` (Drive zip) |
| Names, moves, phases, parts and chains, the song's sections | Core `NativeFallenBoss.cs` |
| The script, the camera, the Slash's numbers | Core `NativeFallenBossPreview.cs` |
| Every effect, the water's rings and waves | Core `NativeFallenBossFx.cs` |
| The song's measure the floor reacts to | `tools/fallen_song_map.py` into `Resources/FallenBossMusic.json` |
| Body, pose, wings, robe | Runtime `NativeFallenBossBody.cs` |
| Stage, stand-in pilots, trail, chip screen | Runtime `NativeFallenBossPresentation.cs` |
| The cut across the screen | Runtime `NativeFallenSlashImage.cs`, `Resources/NativeFallenSlash.shader` |
| Black water, effects, feathers, plate | `Resources/NativeFallenWater.shader`, `NativeFallenFx`, `NativeFallenMotes`, `NativeFallenBody` |
| Checks | `Tests/FallenBossChecks.cs` (`--fallen-boss`) |

**Not built yet:**

- Astra's rules (section 13). The preview's pilots are stand-ins, its damage is nothing, and the chip screen's draft is a placeholder for the real draft.
- Binding the presentation to Astra's record: the script is replaced by the record in one place, as THE HUM's `HumBossViewOf` does.
- Real pilots' reflections in the water: the stand-ins have them through a mirrored second body, which is too dear for every real pilot without a look at the cost.
- Sounds: the song plays; the Slash, the moves and the adds have no sound of their own.
- The owner's ear check of the section timings in section 6.

## 15. Playable standalone encounter (Astra, 2026-11-06)

NOCTIEL's Boss Battle entry now runs the real encounter in Still Water for 1–4 humans (no bots). See the newest DEV_LOG entry and task PR for verification/publication status. Section 14 remains the historical preview receipt. The original preview is retained.

Authority: `Runtime/NativeFallenAuthority.cs`; deterministic geometry/modifiers and replicated record: `Core/NativeFallenRules.cs`; presentation adapter: `ArenaPresentation.FallenBossViewOf` in `Runtime/NativeFallenBinding.cs`. Phase drafts are real PvE picks; lives use a held interact input. Fall of Light uses one seeded volley per cast with a minimum 2.4-second warning.

Remaining design integration: Expedition end-of-run destination choice and finale ledger; intro seen/skip votes; rendered human co-op acceptance and Claude's polish/sounds/reflections. This standalone fight does not change rolled Expedition runs or their compatibility digest.
