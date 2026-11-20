# DEV · Architect of Reality (the owner's dev intro)

Owner, 2026-11-20: "a new dev intro for myself. The dev intro id like to be even better, with name effects and everything.
Essentially something 'bad-ass, godlike' really show the authority of the dev." Theme chosen: **Architect of reality**: the arena
dissolves into code and light, and the dev rebuilds it around them; the world obeys. The name shown is the live account display
name, with its own name effects. Presentation only (no rule, wire, save or timing change beyond the intro's own length).

- **Id:** `dev-architect`. **Shown as:** "DEV · Architect of Reality". **Gate:** a developer account, as the Sheldon Intro.
- **Length:** 9.5 s. **Name beat:** 7.5 s.
- **Code:** Core `NativeDevArchitectIntro.cs` (the script: pose, camera, light, set, effects, name strokes, cues), Core
  `NativeArchitectGlyphs.cs` (the stroke font the name is written in), the set bank script `tools/stages/intro_dev_architect.py`
  (scripted Blender into the local bank `ChipArenaIntroDev.bytes`, never committed).

## The story in beats

The music beats the owner was given (0-3 s dissolve, glitch and drone; 3-6.5 s rebuild, rising; 6.5-7.5 s stillness, a near-silent
sub-drop; 7.5-9.5 s the name, the big hit and a choir chord) are the intro's own.

| Time (s) | Beat | What happens | Camera |
| --- | --- | --- | --- |
| 0.00 | The arena | A stone and steel arena in the dark: a round floor of slabs in three rings, eight pylons, light in their seams. A low drone. | Low and wide at the floor's edge, creeping in. |
| 0.40 | First tear | Reality glitches: bands of the world slip sideways and snap back; the lights stutter. | (held) |
| 0.90-2.40 | Coming apart | The arena comes apart from the outside in: each slab and pylon segment lifts off, its stone falls away to its wireframe (light along every edge), the wire breaks into code glyphs, and the glyphs into light. Underneath: the void, a blueprint grid running to the horizon. | Crane up and back to a high wide frame: the whole arena unmade. |
| 1.10 | The presence | Before any body: one point of white light at the centre, chest-high. The glyph streams bend toward it and wind round it. | (the crane) |
| 2.40-3.00 | The body | The point opens into the body's outline in light lines; the body condenses from the core outwards, glyphs pouring into each limb. Floating 0.35 m over the grid, head bowed. | Close and low on the chest, pushing in. |
| 3.00 | Ignition | The visor ignites. | (cut on it) |
| 3.40 | "The floor" | The right hand sweeps out and presses down. Light lines race out over the grid drawing the floor's seams; the slabs fly up out of the void and lock into place in a wave from the feet outward, each lock a flash on its seams. | Low behind the shoulder, the floor racing away. |
| 4.20 | "Rise" | Both hands lift, palms up. Light lines climb out of the floor drawing eight monoliths three times the pylons' height; their stone segments fly in from the dark on curved paths and lock bottom to top, round the ring. | Floor level, looking up: the tiny figure, the towering stone. |
| 5.20 | "The crown" | The right arm rises overhead, the hand opens. A pen of light draws a vast ring high above; its arcs fall in from the sky and lock; the far architecture (spires on the horizon) lights in a wave. | Top-down through the ring, craning down. |
| 5.90-6.40 | The descent | The arms fall to the sides; the architect sinks the last 0.35 m. The centre disc locks under the feet as they touch (6.40): every seam flares. | Craning down to a wide frontal frame. |
| 6.50-7.50 | Stillness | Everything stops. The glyph motes hang frozen in the air; the seams breathe slowly; near silence. The head rises to look into the lens. | Locked off, symmetrical; a slow push from 7.0. |
| 7.50 | The name | Two fingers rise. The frozen glyphs rush to a line above and behind the architect and the name is written there in light: each letter traced stroke by stroke by its own pen, flashing as it closes, its colour split and sliced, the distortion settling. The monoliths and the ring flare with it; a wave of light runs out across the floor. | Low and a little wide, the name above the head. |
| 7.50-9.50 | Authority | The name settles into a steady, heavy light; glyph embers rise off it; the ring turns once and stops. | A slow push and boom down to the hero frame. |

## The set (`ChipArenaIntroDev.bytes`)
A modelled kit, every piece in its own frame; the script places them (one layout, in Core). Original work, scripted in Blender
5.2.3 (`tools/stages/intro_dev_architect.py`), finished with the stage kit's baked occlusion and colour. Without the bank the
intro draws a dark lit void: the grid, the light lines, the glyphs and the name, never the old blocks.

## Checks
`Tests/` managed checks (`--intro-dev`): determinism, the length and name beat, no per-frame allocation, the budgets at peak, Reduced
effects (no flash above 3 Hz, no camera shake), the streak rule (no streak over 1 m left off its head's camera plane), the cues on
their beats, and every glyph of the stroke font closed and inside its cell.
