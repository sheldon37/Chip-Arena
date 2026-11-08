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

- **Standing and walking:** upright, the head turning after the pilot it's hunting; it holds very still, then moves all at once. The sword is carried as in the owner's reference walk (2026-11-06; the angle corrected 2026-11-07 from the reference clips' standing frames): it hangs from the right hand at its side, the arm nearly straight, the blade laid out to its right side (a few degrees ahead of square, never back) and down about thirty degrees, its broad side to the front and its point resting on the water, where it lays a ring every 0.6 m it travels. At rest it stands as the reference does (below, "Live walk and idle"). The walk is the pack's slow walk at about one heavy step a second, its stride matched to the ground speed.
- **The knee:** a true one-knee kneel set on the muscles (no clip has one), used for the cutscene, each threshold and the death. The fist closes on the planted sword by IK.
- **Gliding:** a dash leaves the water, feet off it, with a wake laid behind.
- **Wings:** rooted on the chest but square to the body so the pair stays a pair, opening and flaring on an underdamped spring, thrown past their mark and shivering back, each pair behind the one above; they lag turns and blow back with speed.
- **Robe, halo, ring of blades:** in phases 1 and 2 the robe is a cloth skirt (below, "The robe as cloth"): a narrow panel between the legs, open either side so the legs show whole, a panel each side to the knee and a long one behind; phase 3 keeps the strips' remnant at the waist. The halo tilts by phase; the blades turn.
- **The blade clears the body:** after the arm's pose the hand turns about the wrist until the blade (guard to point) is out of capsules round the torso, head, legs and free arm, leaving on the side it was last pushed to (Runtime `FallenBladeClear`). A planted sword is left alone. Drawn from the water, the fist goes straight from the planted grip to the carry (`FallenGrip`), never back through the clip's open hand.
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
- Sounds: the song plays; the Slash, the moves and the adds have no sound of their own yet. Their request is section 17.
- The owner's ear check of the section timings in section 6.

## 15. Playable standalone encounter (Astra, 2026-11-06)

NOCTIEL's Boss Battle entry now runs the real encounter in Still Water for 1–4 humans (no bots). See the newest DEV_LOG entry and task PR for verification/publication status. Section 14 remains the historical preview receipt. The original preview is retained.

Authority: `Runtime/NativeFallenAuthority.cs`; deterministic geometry/modifiers and replicated record: `Core/NativeFallenRules.cs`; presentation adapter: `ArenaPresentation.FallenBossViewOf` in `Runtime/NativeFallenBinding.cs`. Phase drafts are real PvE picks; lives use a held interact input. Fall of Light uses one seeded volley per cast with a minimum 2.4-second warning.

Remaining design integration: Expedition end-of-run destination choice and finale ledger; intro seen/skip votes; rendered human co-op acceptance and Claude's polish/sounds/reflections. This standalone fight does not change rolled Expedition runs or their compatibility digest.

## 16. Live motion (Claude, 2026-11-06)

Owner: "Their intro still starts while we're loading in ... a black pillar in them ... when they turn their weapon in intro, it goes through their body ... a lot of feet sliding, and it also needs better attack animations."

- **Loading:** the stage preload loads NOCTIEL's own song for `expedition-lux-cecidit` (it loaded the Siren's), the boss songs wait while the loading screen covers the view (`NativeAudio.StageCovered`), and a boss's intro hold keeps the entrance intro's loading budget instead of the fight's none. The authority's load gate already holds the clock until clients are ready (its 12 s deadline is Astra's).
- **The fight's motion** is Core `NativeFallenLiveMotion`, run after the binding (`ArenaPresentation.FallenLiveMotion`, Runtime `NativeFallenLiveBody.cs`). It plays the preview's choreography on the authority's clock: each clip's contact lands on the move's hit (Cleave's three cuts at +0, +0.3, +0.6 s; Severance's second wing at +0.6 s), short windups draw the wings in, long charges spread them, the trail shows on the strike only. Descent leaps from where it stood and lands where and when the authority puts it; Vow flies its lane on the authority's own lerp; Feather Rain and Fall of Light lift it off the water. Chasing at the authority's 9 m/s it glides half a metre up with its wings open; standing, it turns to its quarry in steps. Clips crossfade, position and facing follow springs.
- **Feet:** live, a sole the clip puts on the water stays where it came down (two-bone leg IK), stepping in a short lifted arc when the body has moved or turned past it, one foot at a time.
- **The walk (the owner's reference video, 2026-11-06):** up to about 2.3 m/s it plays the slow walk, from 0.6 m/s at the stride's own ground speed. The reference's step a second is 1.4 m/s at NOCTIEL's size. Above that it plays the brisker walk, and above 3.2 m/s it glides. **The fight never walks yet:** the authority moves it at 9 m/s or not at all, so in play it glides or stands. A walking approach is a movement rule (Astra's); the proposal is in DEV_LOG.
- **Impact on rules:** none. No hit time, hitbox, damage, cooldown, movement or wire value changes; the body only shows the authority's facts (Descent's arc leads the authority's teleport, which happens on the hit as before).
- **Checks:** `Tests/FallenBossChecks.cs` (`--fallen-boss`): every move's contact within 35 ms of its authority hit, the chase glides and lands, standing turns and every clip change crossfade, missing clips fall back.

## 17. Sounds (Claude, 2026-11-06)

Owner: "write a sound cue list for elevenlabs". The request is `docs/audio/sound-requests-noctiel.json` (`docs/audio/SOUND-REQUESTS.md`, NOCTIEL's sounds): 57 keys, 92 takes, 148.6 s of generation, about 5,944 credits at the generator's documented rate. The song stays the bed; these are sparse and monochrome like the picture (porcelain plate, glass, steel, still water, wordless voices, light). Every one-hit kill has an audible tell as long as its warning. 2D keys play flat; the rest play at the boss, an add or a pilot. **Wired** (2026-11-06): Core `NativeFallenBossCues.Cutscene` plays the cutscene's beats on the intro hold's clock, all flat. `NativeFallenBossCues.Fight` plays each move's tell and strike on the authority's times, plus the Slash, the chips, the death, the 12:00 enrage and the local pilot's Hush warning. Feather Rain's blades fall on the lanes nearest the listener, Fall of Light's columns are the authority's nearest ones, and Severance's wave sounds as it reaches the listener. Runtime `NativeFallenBossAudio.cs` adds what only a change shows: adds rising, singing, shooting, gripping and dying (not when a phase change clears them), the local pilot's lost feather, a pilot falling back in, a revive (heard by the two in it), the water opening under a pilot who stood still, hits on NOCTIEL, its glide, standing turns and footfalls. A key whose clips haven't been imported is silent.

| Key | Plays when | Space | Takes | Length (s) | Level (dB) | Priority |
| --- | --- | --- | ---: | --- | ---: | ---: |
| `boss.noctiel.step` | a footfall on the water (the feet the live motion plants) | at its place | 4 | 0.25–0.6 | -10 | 1 |
| `boss.noctiel.glide` | it takes off into a glide to chase | at its place | 2 | 1–1.6 | -8 | 1 |
| `boss.noctiel.wings.open` | its wings spread (the cutscene, a long charge, Feather Rain, Fall of Light) | at its place | 2 | 1–1.6 | -6 | 1 |
| `boss.noctiel.wings.beat` | the wings thrown out on a strike | at its place | 3 | 0.4–0.9 | -6 | 1 |
| `boss.noctiel.plate` | standing turns and small moves: its plate shifting | at its place | 3 | 0.2–0.5 | -14 | 2 |
| `boss.noctiel.hurt` | a pilot's hit lands on it | at its place | 4 | 0.2–0.6 | -6 | 1 |
| `boss.noctiel.feather` | the cutscene's one feather touches the water (0:04-0:15) | 2D | 1 | 1.2–2 | -14 | 1 |
| `boss.noctiel.ripple` | a slow ring across the whole water (the feather, each pair of wings opening) | 2D | 2 | 1.5–2.4 | -12 | 1 |
| `boss.noctiel.gauntlet` | the close-up: the gauntlet on the hilt (0:28) | 2D | 1 | 0.6–1.1 | -8 | 1 |
| `boss.noctiel.veins` | black veins crawl up the blade | 2D | 1 | 1.6–2.4 | -8 | 1 |
| `boss.noctiel.halo.crack` | the crack runs through the halo | 2D | 1 | 0.6–1.2 | -6 | 1 |
| `boss.noctiel.rise` | it rises from its knee (0:39) | 2D | 1 | 2–3 | -6 | 1 |
| `boss.noctiel.mask` | the blank mask lifts toward the lens (0:48) | 2D | 1 | 0.5–1 | -8 | 2 |
| `boss.noctiel.sword.pull` | it pulls the sword out of the water | 2D | 1 | 1.4–2.2 | -4 | 1 |
| `boss.noctiel.slash` | the Slash cuts the screen (the opening on the drop, every phase change) | 2D | 2 | 0.6–1.2 | +0 | 1 |
| `boss.noctiel.slash.part` | the two halves of the frame slide apart | 2D | 1 | 0.6–1.2 | -4 | 1 |
| `boss.noctiel.slowdown` | time slows hard as it swings at the camera | 2D | 1 | 0.7–1.2 | -6 | 2 |
| `boss.noctiel.kneel` | it drops to one knee at a threshold | at its place | 1 | 0.6–1.1 | -2 | 1 |
| `boss.noctiel.chip` | each of its three modifier chips turns over on the beat | 2D | 2 | 0.3–0.6 | -8 | 1 |
| `boss.noctiel.cleave` | each of Cleave's three cuts | at its place | 4 | 0.35–0.8 | -2 | 1 |
| `boss.noctiel.vow.draw` | Vow's tell: the water ahead parts in a line | at its place | 1 | 0.7–1.2 | -6 | 1 |
| `boss.noctiel.vow` | Vow's thrust crosses its lane in a blink | at its place | 2 | 0.5–1 | +1 | 1 |
| `boss.noctiel.severance.wind` | Severance's tell: the blade held wide | at its place | 1 | 0.6–1 | -6 | 1 |
| `boss.noctiel.severance` | the flat swing throws the crescent across the map (and the second wing) | at its place | 2 | 0.7–1.2 | +2 | 1 |
| `boss.noctiel.wave` | the crescent passing close to a pilot | at its place | 2 | 0.4–0.9 | -4 | 2 |
| `boss.noctiel.leap` | Descent: it leaps into the dark | at its place | 1 | 0.5–1 | -4 | 1 |
| `boss.noctiel.descent.mark` | Descent's tell: the white ring closing on its pilot | at its place | 1 | 0.9–1.4 | -6 | 1 |
| `boss.noctiel.land` | Descent lands on the hit | at its place | 2 | 1–1.6 | +4 | 1 |
| `boss.noctiel.rain.lines` | Feather Rain's tell: thin lines of light on the water | 2D | 1 | 0.8–1.2 | -10 | 1 |
| `boss.noctiel.rain` | a steel feather falls into its lane | at its place | 4 | 0.3–0.7 | -6 | 1 |
| `boss.noctiel.verdict.plant` | Verdict / Last Verdict: it plants the sword | at its place | 1 | 0.6–1.1 | +1 | 1 |
| `boss.noctiel.verdict.crack` | the OHK tell: the crack brightens across the whole map (2.4 s; 2.8 s for Last Verdict) | 2D | 1 | 2.2–2.7 | -2 | 1 |
| `boss.noctiel.verdict.split` | the map splits along the crack | 2D | 1 | 1.2–2 | +5 | 1 |
| `boss.noctiel.choir` | Choir: it lifts its off hand and the Cantors come | at its place | 1 | 1.4–2 | -4 | 1 |
| `boss.noctiel.cantor` | a Cantor sings its beam at a pilot | at its place | 2 | 1.4–2 | -10 | 1 |
| `boss.noctiel.cantor.death` | a Cantor is killed | at its place | 1 | 0.5–1 | -8 | 1 |
| `boss.noctiel.sweep.shake` | Wing Sweep's tell: the blade wings spread and shake | at its place | 1 | 0.6–1 | -6 | 1 |
| `boss.noctiel.sweep` | the full-circle Wing Sweep | at its place | 2 | 0.8–1.3 | +2 | 1 |
| `boss.noctiel.eclipse` | the OHK tell: the dark rolls in from the edge (3.2 s) | 2D | 1 | 3–3.5 | +0 | 1 |
| `boss.noctiel.eclipse.pool` | a pool of light opens on the water | at its place | 2 | 0.7–1.2 | -10 | 1 |
| `boss.noctiel.penance` | Penance: it pours from the blade | at its place | 1 | 1–1.6 | -6 | 1 |
| `boss.noctiel.penitent` | a Penitent rises from the water by a pilot | at its place | 2 | 0.6–1.2 | -8 | 1 |
| `boss.noctiel.penitent.latch` | a Penitent latches on | at its place | 2 | 0.2–0.6 | -8 | 2 |
| `boss.noctiel.fall.mark` | Fall of Light's tell: a bright square on the water, one bar ahead | at its place | 2 | 0.4–0.8 | -10 | 1 |
| `boss.noctiel.fall` | a column of light falls | at its place | 4 | 0.6–1.2 | +2 | 1 |
| `boss.noctiel.throne` | a Throne hangs and turns | at its place | 1 | 1.4–2 | -12 | 2 |
| `boss.noctiel.throne.fire` | a Throne shoots down at a pilot | at its place | 2 | 0.3–0.8 | -6 | 1 |
| `boss.noctiel.throne.break` | a Throne is brought down | at its place | 1 | 1–1.5 | -2 | 1 |
| `boss.noctiel.hush` | the Hush warning: the water stills and brightens under a pilot who stands still | 2D | 1 | 1–1.5 | -8 | 1 |
| `boss.noctiel.hush.open` | the water opens under a pilot who stayed still | at its place | 1 | 0.5–1 | -2 | 1 |
| `boss.noctiel.life.lost` | a pilot loses a feather (a life) | 2D | 1 | 0.6–1.1 | -6 | 1 |
| `boss.noctiel.return` | a pilot falls back in from the dark | at its place | 1 | 0.6–1.1 | -8 | 1 |
| `boss.noctiel.revive` | a teammate starts giving a feather (the revive hold) | 2D | 1 | 1.5–2 | -10 | 1 |
| `boss.noctiel.revive.done` | the feather is given and the pilot stands | 2D | 1 | 0.6–1.1 | -6 | 1 |
| `boss.noctiel.death` | NOCTIEL falls: its plate cracks and the light goes out | 2D | 1 | 2.6–3.5 | +3 | 1 |
| `boss.noctiel.shatter` | the hollow shell shatters into the water (the song's hit at 4:08) | 2D | 1 | 1.6–2.4 | +0 | 1 |
| `boss.noctiel.enrage` | The Light Goes Out (the 12:00 enrage): white, then black | 2D | 1 | 2.6–3.5 | +4 | 2 |

## 18. The intro's choreography (Claude, 2026-11-06)

The owner's four reference clips (`01` the feather, `02` kneel and rise, `03` the draw, `04` the windup and cut) are one sequence; option A keeps all four in order. Core `NativeFallenIntroMotion` keys the body on the cutscene's existing beats; the music, cues, shots, Slash and effects keep their times. Runtime `NativeFallenIntroBody.cs` applies it to the rig: the hips' travel, the back's lean and twist and breath, the head, both feet by two-bone IK, the claymore's keyed path through the existing arm IK (always in the right hand), and the free arm.

| Intro (s) | Shot | Motion |
| --- | --- | --- |
| 1.4–10.2 | Feather | A falling leaf: swings about 1.55 s apart, higher at each turn and tilting into it, smaller near the water; flat on the water on the 10.2 cue, floating until the cut |
| 11–33.5 | Far, gauntlet, blade | Kneeling, breathing slowly |
| 33.5–35.6 | Halo, long lens | The head lifts before the body moves |
| 35.2–37.6 | Long lens | The rise over the planted front foot: the hips forward 0.72 m, the back knee off the water, the back foot steps up beside it; the hand stays on the planted sword |
| 47.0–47.75 | Low three-quarter | A dip, the pull straight up (the point out of the water), the keyed fist takes the sword |
| 47.75–48.7 | | The blade wheeled up on its right side (not across the body) to a salute, point up |
| 48.7–49.2 | | Out and down to horizontal at its side |
| 49.2–49.9 | | Up the side, the wrist rolling the edge forward, to a cocked windup behind the right shoulder, above the halo; the top wings open wider |
| 49.9–50.43 | Lens from 50.1 | The held windup, weight back |
| 50.43–50.61 | | The load: the blade rolls to vertical overhead |
| 50.61–50.81 | | The downswing in 0.20 s (the reference's took about 0.34): the left foot steps into the lunge, the back pitches and twists, the free arm thrown back; the point meets the water toward the lens on the drop |
| 50.81 + 0.95 | Slash, fight | The impact held 0.25 s, then a 0.7 s release: the left foot then the right step home to the fight's pose, the fist back to the carry |

Not taken from the references: clip 03's roll of the blade across the front of the body (it would cut through NOCTIEL at 3.4 m), the pause in the low guard between 03 and 04 (no time before the drop), clip 04's two-handed end (NOCTIEL is one-handed), and the foot pops and slides. The fist stays closed on the grip (no finger animation), and the robe, wings and halo follow their own springs.

## 19. The intro from the reference clips themselves (Claude, 2026-11-06)

Owner: "Reproduce the reference videos as accurately as technically possible in Unity ... Treat the video as the authoritative motion reference ... Prefer extracting and retargeting actual skeletal motion." Section 18's keyed choreography was hand-placed keys over a pack clip and IK, an approximation of the clips; this replaces it with motion extracted from them. Branch `claude/noctiel-mocap`, checked in the Unity Editor beside the reference frames and corrected there before it went to shared.

- **How:** MediaPipe's pose landmarks on every frame of clips 02, 03 and 04 (24 a second); the claymore tracked in the picture; both solved onto NOCTIEL's own rest skeleton (each limb aimed along the reference's, the elbow's and the knee's hinge from the bend, so no limb flips or twists), the feet held on the water where the reference holds them still. The whole pipeline is `tools/motion` (README there), with `correct.py` on top for what the solve got wrong in depth; the baked file is `Resources/FallenIntroMocap.json`, sampled by Core `NativeFallenIntroMocap`, applied by Runtime `NativeFallenIntroMocapBody.cs`. Clip 01 (the feather) stays as section 18 has it.
- **On the rig:** every bone under the pelvis is set from its bind pose each frame, parents first (a bone the clips don't carry turns with the nearest one above it; the fingers keep the fist), so no turn is left over from the clip pose or a blend. The fist stays on the planted sword by the existing plant IK until the reference's pull; the left hand takes the grip in the lunge (two-handed, as clip 04 ends: section 18 left it out). The planted sword stands at one point the arm can reach from the kneel through the stand (`plantGrip` in the file), a little in front of where the solve's depth put it.
- **Timing:** the reference's own speed throughout, except the chop (top to contact 0.25 s in the clip, 0.20 s as the owner asked); the contact is on the impact (50.81, the drop). Played back to back the clips fill the cutscene: the rise begins at 35.36 (the beat is 35.20), the pull at 40.61 (its sound moves with it from 47.0), the windup's top at 50.61. After the contact the lunge plays on for 0.6 s (clip 04 to frame 148), then lets go over 0.65 s: the fight's next move can start from 51.41 (`bossGap`, Astra's), so frames after 148 are only approached. The kneel is clip 02's frame 60, held from 11 s with the keyed breath until 33.78. `NativeFallenIntroMocap.Speed` scales everything together and `Map` each stretch, without touching the frames; the contact stays on the impact.
- **The head:** the reference's own, except the mask's close-up (44.46–47.0), which still turns it to the lens, fading back by 47.6.
- **Checked (`CoreTests --fallen-mocap`, 120,180 checks):** the blade on its track, mean 0.3 degrees, worst 4, on the frames not filled by hand; no bone faster than 6.8 degrees in 1/240 s; elbows to 105, knees to 109, forearm roll to 59, wrist to 88 degrees, a foot out to 41 degrees, no backward bend; 99% of the time a sole, toe or knee within 1 mm (rest) of the water; held feet slide at most 0.04 m/s, the two stepping windows 0.43 m/s; the planted grip inside the arm's reach on every frame and the fist on it within 6 cm (rest).

What each moment is made of. The file's per-clip `corrected` track says it frame by frame and limb by limb (1 legs and hips, 2 right arm, 4 left arm, 8 blade, 16 chest); anything unflagged is the extraction, retargeted.

| Clip | Extracted, retargeted | Corrected by hand, read off the reference frames beside Unity (`tools/motion/correct.py`) | Filled |
| --- | --- | --- | --- |
| 02 kneel, rise (shown from 60) | the spine and head; the left arm's path until it reaches the knee | every frame's legs and hips (the solve had the rear thigh forward, the knee 0.38 m off the floor, the hips 0.13 m high and a 0.58 m body jump at 120–132: now a true one-knee kneel); the rear leg read off the video every few frames from 100 to 148 (its foot low, behind and turned in while the knee comes up over it, then one step landing at 148); the right arm onto the one plant; the left hand on the knee to 127, off by 142; the chest's lean, easing out 104–138 | frames 0–47 held (no body read), not shown |
| 03 pull, salute | the spine, head and left arm; the right arm's path after 84; the blade's way after 116 | the feet locked, the hips fitted to them; the right arm from the plant, joining the extracted path by 84, level with the shoulder through the wheel (the fist up to 0.13 m higher, the elbow's bend turned back in depth); the blade at 68–104 read off the video frame by frame (its angle, how foreshortened, the tip toward the lens), turned up to 8 degrees more by 116; its depth at 100–156 turned forward (the solve had it back into the wings) | the blade at 68–104 (read, not tracked) |
| 04 raise, cut | the spine, head and both arms; the blade's way before 52; the two-handed grip | the feet locked to 122; the hips' height and sideways travel from 120; the lunge's step read off the video frame by frame, 120–140 (the foot barely leaves the floor, toes down and back, and lands heel first, 0.68 m from the left foot; turned well out from 60), the left foot's pivot 124–138; the blade at 52–76 read off the video (the track jumped about 60 degrees at 64–70, where the blade leaves the picture), 4–8 degrees up through the hold 77–119; the fist's drop at 132–139 | the blade at 120–135 (motion blur), read off the video frame by frame |

The stance is one place through all three clips, so the 1 s joins at 38.28 and 45.28 move no foot: seen clean at 1× and 0.25×.

**Against the real videos (Claude, 2026-11-07).** The owner supplied clips 01–04 (kept outside git). The check before this one had only the 17 frames of `docs/motion/`; on every frame the videos showed what those hid, all fixed in `tools/motion/correct.py`:

- **The chop went round the wrong way.** It is one overhand swing of about 300 degrees in 12 frames: the tip low behind the head (124), straight back (126–127), up to the picture's left (128–131), across the chest toward the lens (132), out low to the picture's right (133–134). The fill between the two frames read earlier went up and over the picture's right, up to 116 degrees off.
- **The wheel trailed** the video by 20–30 degrees at 85–89, with the blade nearly at the lens by 89.
- **The raise jumped** about 60 degrees back and forth at 64–70 and held 8 degrees low through the long hold.
- **The rear foot lifted** about 0.2 m through the rise, and the knee came up early and 0.2 m high.
- **The fists reached the lunge's low place at 139;** the video's are there by 134–135, so the blade ran about 0.25 m (rest) too high.
- **The right arm drooped into a V** through the wheel; the video's reads level and straight from the front.
- **The lunge's foot lifted 0.25 m with the knee high;** the video's barely leaves the floor and lands heel first, its sole to the camera at 134–135.
- **The left hand left the knee late.**

A second pass over the same videos, made separately, reached the same reading of the chop and found the last three. Each was compared with the first pass on the same frames, and the closer one kept.

Measured on the video (`tools/motion/vidcheck.py`, `vidblade.py`; camera fitted per clip, 447 frames): the rear leg's bones outside the figure, median 17 to 6 px, worst 36 to 16 px (854 x 480); the blade's angle in the picture, median 3.3 to 2.7 degrees in clip 03 and 3.8 to 1.6 in clip 04. The measure can't read the blade over the body (clip 03, about 76–88), off the top of the picture (clip 04, about 60–74) or in the blur (clip 04, 126–133): those were read by eye on a metre grid (`grid.py`) and checked by overlay. Seen in the Editor beside the video, front and side, at the frames of the stretches above, the chop at a tenth of speed.

Known differences:

- Depth everywhere is inferred from one camera. The picture settles x and y; for the rear leg depth comes from the bones' lengths, and for the blade from how foreshortened it looks.
- NOCTIEL's claymore is about a third longer than the video's sword (by design), so the tip travels further than the video's. Directions match, not tip positions.
- The planted hold: the reference rests its fist on the pommel; NOCTIEL's fist grips the handle (the sword is fixed in its hand), so the wrist there is the plant IK's. The planted sword stands a little nearer the body than the video's.
- The raised hold (76–116): the fist about 0.12 m (rest) lower than the video's; the blade's angle matches.
- The lunge's foot lands about 0.08 m (rest) wider than the video's at 134–136; the video's slides out to the same place by 142. Left, so the foot doesn't slide.
- Clip 04 frame 0: the stance a little narrower than the reference's, from the one common stance.
- Clip 04 frames after 148 are only approached: the layer lets go for the fight (above). In the DEV preview the stand-in fight then turns the body over 0.2 s (51.7–51.95) while the blade crosses to the carry; the preview's timeline does that, not the intro. The live fight's own hand-back has not been seen.
- Clip 01, the feather, was not compared: it is still section 18's.
- The blade often reads black with a pale edge: the flat of it faces away from the key light. Left as it is.

### Live walk and idle (Claude, 2026-11-07, the owner)

"When Noctiel walks, I think its mixing in his idle combat pose ... Id like his idle pose to be the same as this (With both feet down)", with a
still from the reference walk. The motion bank had no walk at all (`Armed-Walk-Slow` and `Armed-Walk` were never baked: the bake takes the
players' weapon sets' clips), so it slid along in its stance; both are in the bank now (the Drive LocalAssets.zip). Travel hovering at the walk's floor dropped it into its stance mid-stride: once walking it walks on until its
travel is under `WalkStop` (0.25 m/s) and it has walked `WalkHold` (0.4 s). Its idle is no longer the unarmed stance: the slow walk held where
both feet are down (`StandAt`, the frame where the knees bend alike, found once in the bank by `BothFeetDown`), upright, the claymore trailing
low from the right hand as the carry has it.

The owner's next look (a recording of the idle, "May need more idle adjustments to noctiel; please reference"): held, the walk's frame still
read as a stride (the feet in line, the right leg trailing with its toes up, the spine twisted 17 degrees) and the blade pointed out and back,
short of the water, so from in front it looked held out. The reference clips' standing frames (and the owner's still) stand square with the
feet planted apart, the left a little ahead, the blade out to the right side with its point on the floor. Now, at rest, `FallenBossFrame.Stand`
(Core: eases in over `StandIn` 0.45 s once it stands, out over `StandOut` 0.22 s as it walks) lays a stance over the held frame in Runtime
(`FbStandNames`/`FbStandTo`, muscle targets: the soles level within 3 cm, the left foot 26 cm ahead, the feet half a metre apart, the knees
nearly straight, no twist; found by rendering the frame on the body and measuring), and the carry lays the blade out to its right side
(`FbDragWay` 0.99, 0, 0.1) with its point 2 cm over the water (`FbDragTip`). The walk keeps its own legs (Stand is 0 while walking).

### The void held through the intro, the claymore remade, the adds alive (Claude, 2026-11-08, the owner)

"For noctiel fight, only start animating the background AFTER the intro completes. Also, lets make his sword more stylized. Maybe slightly
longer, thinner and overall a better design with glow effects. Begin animating his minions as well."

- **The void:** still through the intro, the song easing in over 2.5 s after it (Runtime `FallenVoidDraw`).
- **The claymore** (Core `NativeFallenSword`, Runtime `NativeFallenSwordBody.cs`, in place of the bank's sword pieces):
  - Its blade runs to 1.9 in the sword's frame (about 3.6 m from the fist as drawn), about two thirds the bank's width, a barb at its foot.
  - A line of light in the fuller; phase 3 black with edges of light, phase 4 all light.
  - A guard of three notched primaries and coverts a side, a chain-wrapped ricasso, and a pommel eye that opens in phase 3.
  - A soft glow along it, edge glints and rising motes. The section 4 table's phase looks hold.
- **The adds** (Core `NativeFallenAdds`, Runtime `NativeFallenAddsBody.cs`, in place of the bank's rigid adds):
  - Posed every frame from the authority's facts.
  - The Cantor rises, sways and sings with its sleeves and open hood.
  - The Penitent crawls on planted hands and rears up to claw.
  - The Throne breathes its blades, charges, fires and kicks back.
  - Each has its own death; anything under the water is cut at its surface.

### The hand at rest, the intro's guard, water that rings where it's touched, a void that plays the song (Claude, 2026-11-08, the owner)

"Might need to fix hand positioning here- I attached video of animation that still needs to be fixed as well. Lets also make the ripple effect on
the stage more 'water-like' like were stepping in water, not just random ripples. Have the background (The void area) react with the music as well."

- **The hand at rest:** the carry turns the sword round its blade so the wrist bends least (the fingers as near the forearm's line as the grip
  allows); before, the fist sat cocked over the handle with the pommel across the thigh.
- **The intro's guard:** through clip 04's guard (frames 88 to 121) the blade is turned to 12 degrees under level, 20 degrees behind the
  left, as the reference holds it over the head (it hung 40 degrees under, behind the left shoulder).
- **The water:** the bass no longer runs out from the boss on the floor, the slams no longer cross it and the level's web is gone. What touches the water rings it:
  the pilots' footfalls (Runtime `FallenPilotSteps`: alternate feet, every 0.75 m), landings and dashes, NOCTIEL's feet down and up. Each ring
  is a ripple packet (steep front, a train of smaller ripples inside, the wavelength growing as it spreads) bending the reflection, with
  glints on its slopes.
- **The void:** Resources/NativeFallenVoid.shader on a sphere round the eye, drawn first: a horizon glow with the level, curtains of light
  flaring with the bass and climbing the sky, a flash and a rising band on each slam. The water mirrors it.

### Breathing, armour and robe, motes off the cloth (Claude, 2026-11-08, the owner)

"seems like they sit there like a statue a lot? no indication of breathing or anything. Give better design to the body as well; it still looks
overall simple, maybe we can give them clothes attached to the hood? like some kind of robe (Also integrate effects with magica cloth)", then
"Maybe like a armor / robe combo actually".

- **It breathes:** `NativeFallenLiveMotion.BreathAt` (4.6 s, in quicker than out, every fourth breath deeper) and `FallenBossFrame.Life` (1 at
  rest, 0.55 walking, 0.25 through a move, 0 on a knee). Runtime lays them on the muscles (`FbBreathNames`: the back, chest and shoulders with
  the breath, the weight easing foot to foot, the head drifting and tilting, the free hand flexing) and on the folded wings' roll.
- **Armour and robe:** a tabard (Core `NativeFallenTabard`, phases 1 and 2) from under the hood over the breastplate, narrower than it so the
  plate shows either side, cinched over the belt's clasp, hanging before the legs to the shin; pale border, a stripe down its middle; phase 2
  burnt shorter. The skirt's front panel is gone (the tabard hangs there). A sash knotted at the left hip, two tails to the knee. Skinned to
  the chest, waist and hips by height (the grid's `Bone0`), held to the belt and at the knot, simulated below.
- **Motes off the cloth:** each cloth's hem (`Grid.Hem`) is read back from the cloth as drawn each frame; a point sheds a mote of light more
  the faster it moves (`FbMoteRest` 0.05 a second at rest, `FbMoteBySpeed` 0.32 per metre a second), embers off phase 2's burnt hems; they
  drift up and fade over 1.6 to 2.8 s, drawn with the boss's effects (`NativeFallenBossFx.ClothMote`); none on the low tier or past 90 m.

### Walking toward its quarry, the hood as cloth, the raised blade (Claude, 2026-11-08, the owner)

"his walk seems to not be correct? ... re-evauluate animations during slash. Assuming he has a hood on? Can we also integrate magica cloth into
that as well; give it more of a fabric appearance", with two screenshots of the intro's raised blade lying through the hood.

- **The walk:** the authority walks it toward the nearest pilot (`NativeFallenApproach`) while its quarry, which it faced, may be another: it
  strode forward while sliding sideways. Walking, it faces where it walks (`WalkTurn` 0.2 s); stopped, it turns to its quarry.
- **The hood:** phases 1 to 3 wear a fabric hood in the cowl's place (Core `NativeFallenHood`, built with the skirts in Runtime
  `NativeFallenCloth.cs`): drawn up to a soft peak behind, open round the mask with a rolled trim, creased under the jaw, a short skirt of it
  over the collar. Skinned to the head (the skirt handed to the neck and chest), named `Hat.NOCTIEL hood` for the bridge's hood tuning, its
  crown and face fixed and the skirt simulated; held as drawn past 90 m or on the low tier; mirrored in the water.
- **The raised blade:** the blade's keep-out round the head (`FallenBladeClear`) is the hood's: set back to its middle, 0.15 wide, up to the
  peak; the left arm's is as wide as its pauldron.

### The robe as cloth, the legs, the sword hand (Claude, 2026-11-07, the owner)

"I think noctiel needs his arm thats holding the sword a little further from his body and also further back (Currently its almost right in
front of his stomach) ... Its hard to see his legs. Make this a lot more details, and integrate magica cloth into their clothes to simulate cloth
movement."

- **The hand:** the carry's wrist hangs from the shoulder 0.17 out and 0.05 back (was 0.07 out, 0.09 forward), the arm a touch less straight
  (0.9 of its reach), so the fist sits out past the hip and the hilt beside the thigh, not over the stomach.
- **The robe as cloth:** the seven rigid strips hung to the ankles all round, the front two right over the legs. Now phases 1 and 2 hang a
  skirt (Core `NativeFallenRobe`, Runtime `NativeFallenCloth.cs`) from under the faulds: a narrow panel between the legs to above the knee
  (the cloth seen from in front), open either side of it (nothing else within 55 degrees) so the legs show whole, a side panel each side to the knee, a long panel behind to mid-calf, slits between, pleated, the hem a little ragged; phase 2's shorter and
  burnt. It is a skinned renderer on the body's own skeleton (bind poses shared with the bake's body mesh), drawn with NOCTIEL's material and
  mirrored in the water from a bake each frame. Its paint map (the waist's two rows fixed, the rest moving) is registered by name and it is
  handed to the licensed cloth simulation through `NativeVisualCloth` (the local Magica bridge: the Skirt preset, capsules on the hips,
  thighs and calves, a plane at the water), one handle per phase's skirt so only the shown one runs; it holds as drawn past 90 m or on the
  low tier and starts over after a jump. Without the bridge it is skinned to the pelvis (the side hems leaning on their thighs) and follows
  the legs. Phase 3's remnant stays rigid; phase 4 has none.
- **The legs** (tools/gear/fallen_boss.py, the bank in the Drive zip): the cuisse in three lames, each lapping the next with a rolled edge and
  two rivets, engraved down the front; the knee a bigger domed cop ringed in trim with a fan of fins on its outside; the greave ridged,
  rolled trim at its top and over the ankle, engraved chevrons, a dark fin down the back of the calf, a flared cuff; the sabaton with three
  lames over the instep, a toe cap, a hinge disc and a heel spur; quilted rings on the undersuit's legs.
