# Slam and break debris: real pieces, and the voxel style kept

Owner, 2026-11-13: "We still seem to use voxel effects on slam attacks- Cubes show from collision; Can we improve this with
higher quality poly or realistic effects?" and "Breaking boxes also seem to still break into cubes still, I want more
realistic physic destruction now; (We can keep the information however in case I want to make a 'voxel' map.)"

Presentation only. No rule, timing, damage, hit volume, cooldown, movement or network field is read back or changed: what
breaks, when, its loose fragments' bodies and colliders, and every hit are exactly as they were. No piece has a collider,
body, light or sound, and nothing is replicated: each peer (host, guests, spectators, the killcam) draws the same pieces from
the events and snapshot it already presents, through the destruction kit's pure-in-time motion.

## Where the cubes came from, and what is drawn now

Everything goes through the destruction kit (Core `NativeStructureFx`, drawn by Runtime `NativeStructureFxPresentation`).

| Source | Before | Now (the realistic style) |
| --- | --- | --- |
| A plunging stab's impact (`NativePlungeFx.Structures`: every class's plunge) | a concrete cube burst from 3.5 m, pale cubes on the greatsword's pull-out | chunks of the floor it hit (its material and tone), small stones, low dust; a dust ring from weight 0.6; sparks and a flash on a metal floor |
| The jump attack's landing (`NativeAirAttackFxPresentation`) | no solid debris | the same, by the weapon and the impact's strength |
| Earthsplitter's fissure, Ground Slam's landing, Sunder's smash, kit landings (Skyfall), the Bulwark's wall slam (`Runtime/NativeSlamDebrisPresentation.cs`) | no solid debris | a heavy burst where the blade lands, then a light one about every 1.5 m as the fissure's front passes (each chipping and cracking the floor); the techniques and landings at the point of impact; the wall slam throws chunks of the wall back along its normal |
| A prop break (crates, boxes, bins, walls) | boxes: brick halves, splinters, plates, shards, grit | by material: a crate to boards as long as its own edges and splinters (weathered outside, pale breaks); a metal box to bent panels and torn scraps with sparks; concrete to a few big lumps, many small ones and stones; masonry to snapped bricks; glass to shards. They lean away from the pilot who struck it |
| A broken prop's loose fragments (the match's `chunk-N` props) | boxes | the material's broken piece inside each fragment's own box (a rough chunk, a clump of four torn boards, two bent panels, three shards); the box only casts its shadow |
| Collapse rubble and a collapse telegraph's grit | cubes | rough chunks, boards or panels by material, small stones |
| The Slow status marker (the cyan bits round a slowed pilot's legs, which Earthsplitter's slow ground puts on its caster) | ten cubes | ten round beads, same places, sizes, colour and timing |

**Weight** (Core `NativeSlamDebris`, 0..1): greatswords 1, the shield 0.8, the sword, katanas and glaive 0.55, the daggers and
Fangs 0.25. A plunge raises it by the fall (2-8 m), a jump attack scales it by the impact. It sets how many chunks (3 to 14),
how big (0.10 to 0.34 m), how far, and the dust ring (from 0.6).

**Pieces** (Core `NativeDebrisShapes`): low-poly flat-shaded meshes made in code, six variants each (a rough chunk, a stone, a
snapped brick, a board with torn ends, a splinter, a bent sheet-metal panel, a glass shard), at most 48 triangles. They are lit
per facet in the kit's baked light (and the anime style's ramp), drawn halfway to grey and held under white so a warm key
light cannot turn a board neon. No pack asset is used; the Drive zip is unchanged.

**Motion** (`NativeStructureFx.Tumble`): ballistic, up to four bounces by material (metal rings longest, glass dies at once), a
slide that dies away, the tumble slowing at each bounce, then the piece lies flat on its own heading, never sunk into the floor,
on a soft shadow, and sinks away at the end of its 3 to 4.2 s.

**Cost:** the kit's pooled records and fixed arrays (40 burst slots; the 1,024-piece and 120-puff caps unchanged), no allocation
per frame, no GameObject. Reduced effects and the low graphics tier halve the pieces and the dust.

## The voxel style: one switch for slams and breaks

`NativeDebrisLook` (Core `NativeDebrisShapes.cs`) picks the style per map; the default is realistic on every map.

- **A map turns it on** in `unity/ChipArenaConversion/Assets/Conversion/Resources/MapLooks.json`: add `"debris":"voxel"` to the
  map's look entry (`MapLook.debris`; anything else, or nothing, is the realistic style). The anime style's styled copy of a look
  keeps the key (`NativeMapStyle.Styled`).
- From code, `NativeDebrisLook.VoxelMaps.Add("<map id>")` does the same; `NativeDebrisLook.Preview` forces a style everywhere
  (a tool hook; null follows the map).
- In the voxel style every break, loose fragment, collapse rubble and plunge rubble is the cube debris it was, the Slow marker is
  its cubes, and the new slam callers throw the same cube burst. No shipped map sets it.

## Left as it was

- **The Top Dog's mallet slams:** cartoon rings, cracks, dust and stars by design (Core `NativeTopDogFx`); no cubes.
- **Gore** (`docs/GORE-FLESH.md`): the gibbing's flesh is its own system; this debris never draws flesh or blood.
- **The Voxel Burst kill effect** and **Classic (voxel) bodies:** voxel by choice.
- **Hit sparks** (`NativeBurstPool`): already drawn as streaks, not cubes.
- **A prop's colour** is its content material's (as the cubes were): a stage-art model whose paint differs from its material
  (the Cyber City alley bin shows dark green; its `metal` is blue-grey 0x426b79) breaks in the material's colour.

Checks: `DebrisChecks` (`--debris`). Captures and results: the 2026-11-13 entry in `docs/DEV_LOG.md`.
