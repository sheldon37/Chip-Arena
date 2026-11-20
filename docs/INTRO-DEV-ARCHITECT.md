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
| 5.035 | the sub cuts | The draft | A body of light: the dressed pilot's own silhouette in cyan, armour and helmet and all, its edges bright and a soft glow inside it (the framework's entrance ghost at no lag, no scanlines); pens of light trace its edges (each limb's two silhouette edges as the lens sees them) out from the chest; a scan sweeps up it and back down; the core of light in its chest swells. | The whole figure, a slow push. |
| 6.38–6.66 | the sub ramps to a hit | The body | It fills solid from the core out (chest, then limbs, hands and feet last), light pouring into every part; at 6.66 a white flash in the chest throws rays of light out all round and the shards back, and the visor ignites. It floats, head bowed, rim lit cyan and ice from behind, the visor glowing, an aura round it (glyph-light circling, a soft glow behind, motes rising). | Low, the figure rising over the lens. |
| 8.03 | the sub cuts: the long riser | Stillness | It hangs perfectly still (a hover, never a stand: the left knee drawn up and forward, the right leg long, the feet pointed) while pens of light draw the new world's plan in the void: the floor's seams from the centre out, eight monoliths' edges up out of the dark, their capstones, the axis to where the crown will hang. Time stops for the glyphs: they hang where they are. The arms open, slowly. | Far out: a tiny figure in the void. |
| 10.50–11.69 | the riser, its five stutters | The gathering | The arms rise and the new world comes in from the void as wireframes, light streaming behind: the floor up out of the abyss, the monoliths in from the horizon, the crown down out of the sky, a step nearer on each stutter. | Low, looking up, pushing in. |
| 11.82 | the pre-hit | The inhale | Everything holds just short of its place; the light draws in; the gaze comes level and the visor, drawn dim, kindles. | Low and frontal on the visor, close, outside the raised arms. |
| 12.27, 12.50 | the dips | The hands close | The world trembles; the hands close and come down beside the helmet, into the frame; the visor brightens on each. | (the close-up) |
| 12.62 | **the drop** | **The world obeys** | Everything snaps into place at once, solid: a floating platform of black glass (sixteen wedges round a sixteen-sided dais, its seams lit cyan at the dais to gold at the rim), eight monoliths with gold capstones, the crown's eight gold spires leaning in overhead, far spires rising. Light floods out along every seam, erupts up every monolith and on into the sky as beams, runs up the spires; the sky wells up deep blue; dust shakes off the world; sparks. The hands fling open into a V, the palms turned in and up to the sky, and the architect sinks to hover over the dais (it never lands), the visor white. | The hero frame from just over the floor, looking up: a slow push and orbit to the front. |
| 12.72–13.3 | the drop's sustain | The name | The live name is forged in the sky above the architect: for each letter code glyphs fly up out of the floor's seams to its strokes and its pen writes it white-hot (an ember glow, a gold body, a white-gold core, all bold); as the name completes it pulses and settles to a slow molten shimmer, light rays fanned behind it. The card is its small caption ("THE ARCHITECT" over the live name), its letters decoding with the sky's pens. | (the push) |
| 13.0–15.3 | | Authority | The arms lower from the V out through the sides into an open pose and hold it, both palms up to the ceiling (they come up as the arms reach the sides and stay there, steady), light pouring from the hands into the dais and running out along the seams; the visor blazing white-gold; gold embers rise from the seams; the head bows a little to look down the lens. A light sweeps the name; the crown turns. | Settling to still: the architect hovering half the frame tall between the two monoliths behind it, the sky's name above, the card low in the black glass under its feet. |
| 15.30–15.70 | the fade | | The song fades; the picture goes to black with it. | |

## The set (`ChipArenaIntroDev.bytes`, 4.8 MB; never committed)
Every piece is modelled in its own frame by `tools/stages/intro_dev_architect.py` (the stage kit's geometry, finishes, baked
occlusion and colour) and placed, flown, dissolved and built by Core (its layout numbers are the script's). Fine as `<prop>`,
Low as `<prop>/low`; every solid that dissolves or is built has a `<prop>.wire` twin (its edges as glowing bars).
- The old arena: `old-plate` (4 m), `old-dais-wedge`, `old-pylon`, `old-skyline-0..3` (six towers each).
- The architect's (no ring shapes anywhere, owner): `dais` (sixteen-sided, its corners on the floor's seams, a gold star and
  sixteen rays of light that the seams continue: one sunburst; a keel hanging into the void), `slab-00/01/10/11` (the floor:
  sixteen wedges to 7.4 m, each cut once across at an apothem alternating 3.5 / 4.6 m round the floor so no seam runs round it;
  each slab lights its left seam, graded cyan at the dais to gold at the rim), `mono-plinth` and `mono-obelisk` (eight, 9.3 m
  out, 12 m tall, gold capstones, veins and columns of code), `crown-spire` (eight gold blades rooted 10.2 m out and 14 m up
  over the monoliths, leaning in toward a point 23 m over the dais: rays, not a ring), `spire-a..c` (far), `shard-a..d`,
  `grid` (the void's, to 80 m).
- The black glass: obsidian near-black and smooth, its baked occlusion pressed to 6% (`OCCLUDED`). The stage shader occludes the
  probe's reflection by it but not the lights' highlights, so wherever the match is staged the arena's reflection probe cannot
  wash the floor grey (the first version read as pale stone on Brickfall); its gloss is the intro's own lights.
Build: `blender -b --factory-startup --python-exit-code 1 --python tools/stages/intro_dev_architect.py -- --out <dir> --bank <file>`.
Without the bank the intro draws the dark lit void (`NativeIntroVoid`) under the same effects: never the old blocks.

## Budgets (the framework's dev limits) and Reduced effects
Peak effects calls 550 High and 213 Low (at 13.4 s: the name forged, the floor's flood and pulses, the beams, the embers);
set pieces 121 High (round the drop: the new world solid and its wires fading) and 76 Low (Low keeps wires on the obelisks
only); 8 lights. The name's layers thin with its length (three on High, two on Low; fewer for very long names). Reduced effects: no camera shake (the handheld is
dropped), every flash widened and dimmed (`Flash`), the tremble slow and small, the card's letters fade in without cycling.

## Checks
`Tests/IntroArchitectChecks.cs` (`--intro-architect`), beside the framework's `IntroV2Checks`: the registry (15.7 s, the name at
the drop), the song in the build, the beats in order, a cue on every beat and stutter (each with a key, an existing sound and a
clip), every frame finite on High and Low with and without the bank, the peaks, every streak over 1 m on its head's camera
plane, determinism under a scrub, no allocation per frame, Reduced effects (the same camera twice; no light or glow flash
faster than 3 Hz), the body's build, the hover from the drop to the end, the palms up from the arms' reaching the sides to the
end (within 8 degrees of straight up as the arms lower and 3 from the open pose on, turning under 60 degrees a second, the
fingers out level), the head's frame and the visor (the drawn glass ahead of the head; the camera's framing of it up the keyed
head's axis and ahead), the inhale's close-up (low and frontal on the visor, the lens clear of the head, chest,
shoulders, elbows and hands), the hero frame (low and looking up, the architect between .36 and .62 of the frame tall, the
sky's name above the head and in frame), the card as a small caption, no ring-shaped prop in the set, and the stroke font
(every letter, digit and mark inside its cell; accents fold; other scripts are left to the card; a 20-letter name fits).

## The model and the keyed rig
The architect is keyed on the rig and the drawn model follows it (the keyed pose drawn faithfully on the model). The effects
read the drawn body through the model-aware anchors (docs/INTROS-V2.md 2a): the visor's light is the drawn glass, the hands'
light and columns the drawn palms, the draft's lines the drawn joints. The camera stays keyed: it frames the drawn visor
`EyeUp` (.18 m) up the keyed head's axis and `VisorFront` (.13 m) ahead (`FramedVisor`), as measured on the first version's frames.
The palms-up hands are keyed: each upper arm turned about its own line (where it points unchanged) and the wrist bent back, so the
palm (the hand frame's +z, as Core `NativeDraftPlant.PalmIn` has it) faces straight up with the fingers out along the forearm;
the angles were solved on the keyed rig at the keys' times and a check holds them. The drop's V is turned the same way (60 degrees
about each arm's line: the palms in and up), which keeps the upper arms inside the framework's limits (`--intro-limits`: none).
