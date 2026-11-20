# DEV · Architect of Reality (the owner's dev intro)

Owner, 2026-11-20: "a new dev intro for myself. The dev intro id like to be even better, with name effects and everything.
Essentially something 'bad-ass, godlike' really show the authority of the dev." The theme they chose: **Architect of
reality**: the arena dissolves into code and light and the dev rebuilds it around them; the world obeys. The name shown is the
live account display name, with its own name effects. Then the owner chose its music, "Obey", their own track: "maybe start
around 5-6 secs. It drops at about 18 seconds (allow the intro to be slightly longer if this is normally a constraint)".

- **Id:** `dev-architect` (registered `NativeIntroEntry.Dev("architect", …)`). **Shown as:** "DEV · Architect of Reality".
  **Who:** live DEV access only (the framework's Dev kind).
- **Length:** 15.7 s. **Name beat:** 12.63 s, the song's drop. Presentation only: the match reads the registry's length.
- **Code:** Core `NativeIntroArchitect.cs` (the script and `NativeArchitectFont`, the stroke font the name is written in),
  Runtime `NativeIntroArchitectCard.cs` (the name card's `"architect"` effect), the set's scripted Blender
  `tools/stages/intro_dev_architect.py`, checks `Tests/IntroArchitectChecks.cs` (`--intro-architect`), sounds
  `docs/audio/intros/dev-architect.md` (the list) with its request rows `docs/audio/intros/I1.json` and its 17 keys in
  `Core/NativeIntroSoundKeys.cs` (the bank contract).

## The music
`Resources/Audio/music/architect-obey.wav` is the owner's track from 5.17 s to 20.92 s (intro time = song time − 5.17 s),
faded over 15.30–15.70 s, the cover art stripped. It is a WAV, not an MP3, so the drop lands on its sample: an MP3's encoder
delay would put it 25–50 ms late on the picture. It plays as the intro's bed (`Music`), and while it plays the intro is drawn
on its playback time, so the picture follows what is heard on every machine. Reduced effects keeps it. Imported compressed in
memory and preloaded, at the track's own level (no normalise).

The beats below are the song's transients as measured from the file (5 ms envelopes of the full band and of the sub under
120 Hz); each visual beat is keyed to the same number.

## The story in beats

| Time (s) | Song | Beat | What happens | Camera |
| --- | --- | --- | --- | --- |
| 0.00 | the hit at 5.17 | The strike | A column of white light slams down out of the dark onto the old arena's dais (the entrance stage every intro used, modelled: steel deck plates with the 1 m grid, the hexagonal dais in six wedges, twelve neon pylons, the dark skyline). Light cracks race out over the deck. | Inside the pylon ring, wide, settling. |
| 0.06–1.86 | the glitch stutters | The world glitches | On each stutter a band of the world is thrown sideways and snaps back, split in colour; the neon stutters. | 1.07: low across the cracking deck. |
| 1.97 | the drop-out | It lets go | The arena comes apart from the centre out: every plate, wedge, pylon and tower lifts and turns; its stone falls in on itself to its wireframe of light; the wire breaks into code glyphs that rise and fade to light. Beneath: the void and its grid. | A crane up and back over the whole unmaking. |
| 2.97 | a soft hit | A tear | A rift of light runs across the world. | (the crane) |
| 3.505 | the sub returns | The presence | Before any body: a point of light, chest-high over the centre, its flare and halo; glyphs pour into it on spirals; the old world's shards orbit it. 3.74 and 3.90 pulse it. | Orbiting the light. |
| 5.035 | the sub cuts | The draft | The body is traced in lines of light along its bones, pens running out from the chest; its outline shimmers. | Close on the forming figure. |
| 6.38–6.66 | the sub ramps to a hit | The body | It condenses from the core out (chest, then limbs, hands and feet last), light pouring into every part; at 6.66 the visor ignites and a shell of light throws the shards back. It floats, head bowed. | Low, looking up at it. |
| 8.03 | the sub cuts: the long riser | Stillness | It hangs perfectly still while pens of light draw the new world's plan in the void: the floor's seams from the centre out, eight monoliths' edges up out of the dark, their capstones, the axis to where the crown will hang. Time stops for the glyphs: they hang where they are. The arms open, slowly. | Far out: a tiny figure in the void. |
| 10.50–11.69 | the riser, its five stutters | The gathering | The arms rise and the new world comes in from the void as wireframes, light streaming behind: the floor up out of the abyss, the monoliths in from the horizon, the crown down out of the sky, a step nearer on each stutter. | Low, looking up, pushing in. |
| 11.82 | the pre-hit | The inhale | Everything holds just short of its place; the light draws in. | The visor, close. |
| 12.27, 12.50 | the dips | The hands close | The world trembles; the hands close and draw in. | (the close-up) |
| 12.62 | **the drop** | **The world obeys** | Everything snaps into place at once, solid: a floating platform of obsidian, eight monoliths with gold capstones, the crown 13 m up, far spires rising. Light floods out along every seam and climbs the monoliths. The architect lands on the dais. | The hero frame, low and wide. |
| 12.67–13.6 | the drop's sustain | The name | The live name is written into the sky above and behind, in light, a letter at a time, each traced by its own pen, flashing as it closes, its colour split and a slice of it slipping, both settling; on the card the same letters decode and lock. | A slow push and boom down. |
| 13.9–15.3 | | Authority | A light sweeps the name; the crown turns; gold embers rise from the seams. | Settling to still. |
| 15.30–15.70 | the fade | | The song fades; the picture goes to black with it. | |

## The set (`ChipArenaIntroDev.bytes`, 5.0 MB; never committed)
Every piece is modelled in its own frame by `tools/stages/intro_dev_architect.py` (the stage kit's geometry, finishes, baked
occlusion and colour) and placed, flown, dissolved and built by Core (its layout numbers are the script's). Fine as `<prop>`,
Low as `<prop>/low`; every solid that dissolves or is built has a `<prop>.wire` twin (its edges as glowing bars).
- The old arena: `old-plate` (4 m), `old-dais-wedge`, `old-pylon`, `old-skyline-0..3` (six towers each).
- The architect's: `dais` (a sixteen-rayed sigil, a keel hanging into the void), `tile-1..3` (8, 12 and 16 round it to
  7.4 m, a seam of light between each), `mono-plinth` and `mono-obelisk` (eight, 9.3 m out, 12 m tall, gold capstones, veins
  and columns of code), `crown-arc` (eight, an 11.5 m ring 13.2 m up), `spire-a..c` (far), `shard-a..d`, `grid` (the void's,
  to 80 m).
Build: `blender -b --factory-startup --python-exit-code 1 --python tools/stages/intro_dev_architect.py -- --out <dir> --bank <file>`.
Without the bank the intro draws the dark lit void (`NativeIntroVoid`) under the same effects: never the old blocks.

## Budgets (the framework's dev limits) and Reduced effects
Peak effects calls 460 High (at the presence) and 205 Low; set pieces 129 High (round the drop: the new world solid and its
wires fading) and 80 Low (Low keeps wires on the obelisks only); 8 lights. Reduced effects: no camera shake (the handheld is
dropped), every flash widened and dimmed (`Flash`), the tremble slow and small, the card's letters fade in without cycling.

## Checks
`Tests/IntroArchitectChecks.cs` (`--intro-architect`), beside the framework's `IntroV2Checks`: the registry (15.7 s, the name at
the drop), the song in the build, the beats in order, a cue on every beat and stutter (each with a key, an existing sound and a
clip), every frame finite on High and Low with and without the bank, the peaks, every streak over 1 m on its head's camera
plane, determinism under a scrub, no allocation per frame, Reduced effects (the same camera twice; no light or glow flash
faster than 3 Hz), the body's build, and the stroke font (every letter, digit and mark inside its cell; accents fold; other
scripts are left to the card; a 20-letter name fits).
