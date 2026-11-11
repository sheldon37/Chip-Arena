# The Factory: stage art

2026-11-11 · Claude (stage art pass on the older stages) · branch `claude/artpass-factory` from `claude/sleepy-ritchie-g8ruhv`
(`db641c9c`). Status: **implemented on the branch and seen in the shared Editor; not merged, not released.** The bank is local
(git-ignored); the integrator packs it into the builder's Drive zip.

Owner (2026-11-11): "I want to revamp ALL the current stages out of the blocky style. Making them a LOT more detailed and styled ...
Ensure theres no Z-Fighting, remove invisible barriers etc."

The Factory keeps its layout, colliders, nav, hazards, timings, kill shapes and spawns exactly (docs/arenas/FACTORY_PROPOSAL.md,
FACTORY-SIMULATION-CONTRACT.md). What changes is what draws: with the stage bank `ChipArenaStageFactory` the modelled art draws in
place of the dressed blocks, the kit's 5,504 boxes and the rig's boxes; without it every box draws as before. The recipe is
docs/arenas/STAGE-ART.md; this page is what the Factory adds.

## What is modelled

- **The building** (`tools/stages/factory_art.py`): the slab with the two slitters' trenches cut through it; the shell's concrete
  plinth in board-marked lifts with its kerb and ledge, corrugated cladding with real ribs, the clerestory lights behind steel
  glazing bars, the roller shutters' curved slats, the mains as pipes with turned flanges; the bay walls; the eighteen runway
  columns as H-sections with stiffeners, bolted base plates and gusseted haunches; the runways as plate girders with stiffeners
  and a rail; the hall roofs' ribbed sheets, trusses and purlins, the skylight lanterns' frames, turbine vents, ten high-bay
  sodium lamps and the docks' floods; grating decks, stairs and rails (round tube); the pulpits' braced legs, ribbed parapets,
  roofs and beacons; the presses' cast heads, cylinders and light curtains, the die beds; every hazard's painted fittings.
- **The plant** (`factory_plant.py`): lathes (pedestals, bed, ways, carriage with apron, tool post and handwheels, tailstock,
  headstock), machining centres (enclosure, sliding door and window, pendant, louvres, chip chute), switch cabinets, the steam
  hammers (leaning cast frames, crosshead, anvil and die, tup in its guides, the cylinder and steam chest); bar racks loaded with
  round stock on every arm, strapped coils on timber cradles, billet stacks (every billet a little out of line), I-beam bundles,
  crossed ingot courses, plate stacks, open ingot moulds; tool cribs (mesh cage, gate), shelving, blockwork screens with
  extinguishers, parts stillages; the site offices (ribbed cabin, lit windows, door, roof unit), the 20 ft containers (corrugated
  sides, doors, lock rods), the forklifts (chassis, counterweight, overhead guard, seat and wheel, mast, carriage and forks); the
  ladle (a shell with riveted hoops, trunnions and a pouring lip, a slag crust broken over the glowing melt) in a cradle frame
  on its transfer car.
- **The rigs** (`factory_rig.py`, posed by Core `NativeFactoryArt`): the saw blades (32 raked teeth, a ground body, expansion
  slots, arbor flanges), the rail saws' carriages (trolley and rollers, drop arm, a striped guard over the blade, motor), the
  press rams (cast platen, die plate, striped lip) and their guide rods, the crane (end trucks and wheels, two box girders with
  stiffeners and striped ends, the walkway and its rail, a capacity plate, the festoon, the glazed cab with its work lamp; the
  trolley with its hoist drum, motor and gear case; ropes; the hook block with its sheave and swivel hook; the strapped bundle of
  three I-beams; the slings), and the six lathe chucks, which turn (the only new motion: presentation only, on the field clock).
- **The breakable blocks** (block props): two crate sizes, the pallet stacks, the drum pallets.
- Everything else the kit carries is rebuilt from its own parts (`factory_kit.kit_part`).

Every model fills its block (a block is cover: a rack is loaded on every arm, a hammer's throat holds its anvil and tup), and
below 2 m over a floor nothing stands off a block by more than .25 m.

## What stays boxes, and why

- **The telegraph lamps** (slot lamps, the rail saws' wall lamps, the presses' head lamps and beacons, the crane's beacon, the zone
  corner lamps): each takes its own colour each frame, so they stay the rig's lit boxes (`NativeFactoryRig.Kept`), exactly as
  before. **A bloodied blade's smears** stay too (they dry by the clock).
- **The skylight lanterns' glazing** (24 panes): lit boxes that cast no sun shadow, so the sun still falls through the slots in
  stripes; the bank models their frames (`kitKept` in the presentation).
- Fragments and a destroyed crate's stub (the dressing's, as on every map); the effects' quads; the backdrop outside the shell
  (Core `NativeFactoryScenery`, also the main menu's) and the harbour yards (the pack bank's).
- The pack bank's crates, drums, pallets, containers and bins (docs/arenas/STAGE-KITS.md) give way to the stage art's own while
  the art draws (one material and light for the whole hall); without the stage bank they draw as before.

## No z-fighting

Each tier's static scene goes through `stagekit.lift_coplanar` with the tier's whole lift as its reach (6, 12, 20 mm): the kit's
floor paint lies 3 to 12 mm over the slab (joints, washes, lines, numbers), closer than the depth buffer parts at the Mid and Low
tiers' range, so every overlapping pair of faces turned the same way ends a whole lift apart. The presses' doubled jamb stripes
(the bay wall's guard and the head's) are drawn once.

## No invisible walls

`tools/generate_stage_hazards.py` now routes the Factory through `open_stage`. Of its eleven pilot columns, six stay:

| Column | Why |
|---|---|
| `factory-top-wall-n/s/e/w` (13.9 m) | EDGES: the shell's caps keep pilots inside the map |
| `factory-top-runway-n/s` (13.1 m) | OVERHANG: the crane's end trucks ride the runway girders' tops (a rig with no collider would pass through a pilot standing there) |

Five go: `factory-top-roof-n/s` (the hall roofs, 11.7 m), `factory-top-pulpit-a/b` (the pulpit roofs, 8.7 m) and
`factory-top-ladle` (3.9 m). A pilot may stand on whatever they reach; none is at a lethal drop, so there is no bots-only column.
The nav is unchanged (it is computed with the old columns, so no nodes appear on the opened tops: bots have no way up to them).
On the hall roofs the lanterns' glazing and the turbine vents have no collider (a build that reaches the roof through a
skylight slot passes through the glass).

## Files

- Core: `NativeFactoryArt.cs` (slots, `Pose`); `NativeFactoryRig.cs` (the shared numbers, `Kept`); `NativeFactoryKit.cs` (the
  writer's optional kind and owner log for the layout export); `NativeStageArt.cs` (the bank's line).
- Runtime: `NativeFactoryPresentation.cs`, `NativeStagePrewarm.cs` (prepare, pose, render; `kitKept`).
- Tests: `FactoryArtChecks.cs` (`--factory-art`, also in the full run), `FactoryArtLayout.cs`; `StageArtLayout.cs` and
  `Program.cs` take a line each; `StageArtChecks.cs` pins a map without art (`training` in place of `factory`).
- Tools: `tools/stages/factory_art.py`, `factory_plant.py`, `factory_rig.py`, `factory_kit.py`; `tools/generate_stage_hazards.py`
  (the Factory's `EDGES`/`OVERHANG`, its nav kept), and the regenerated `UnityMaps.json` and `tools/asset-lock.json` (only the
  Factory's `pilotBlocks` differ).

Rebuild: `dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout factory LAYOUT.json`, then
`blender --background --factory-startup --python tools/stages/factory_art.py -- --layout LAYOUT.json --out <Assets>/LocalAssets`
(about 20 s; `--preview DIR [--views a,b]`, `--tiers`, `--no-ao`, `--static-only`, `--stats`).

## Numbers, checks and what was seen

**The bank** (2026-11-11): 21,221,467 bytes, 530 props. Static: 36 chunks (20 m cells, two bands); Fine 165,734 triangles
(241,279 vertices), Mid 80,944 (124,275), Low 33,207 (52,342). Rig: 11 meshes for the 13 slots (the high hook and load share the
low ones'); 7,939 / 4,813 / 1,700 triangles. Breakables: 4 meshes for 10 blocks; 5,844 / 2,682 / 160. About 20 s to build.

**Z-fighting** (`zfight.py BANK UnityMaps.json factory`): no block left drawn as a box beside the art; faces of different parts in
one plane and overlapping: Fine 0.17 m2 (eps 4 mm), Mid 0.10 m2 (5 mm), Low 0.01 m2 (8 mm). Before the lift's reach was widened
the Fine tier held 34.8 m2 (the floor's paint). What is left on the Fine tier is twenty slivers of about 0.01 m2 each, a
centimetre over the slab where a floor joint runs under a wall or a plate.

**Frame time** (shared Editor, High tier, the bay from the west dock, 10 s, `perf=`; the box kit is the base commit `db641c9c`
run back to back with the art): the box kit mean 8.21, 8.44 and 8.13 ms, p95 9.46, 9.93 and 9.21 ms; the art 8.18 and 7.86 ms,
p95 9.36 and 9.10 ms with the first bank (96 chunks of 12 m), and 7.94 ms, p95 9.06 ms with this one (36 chunks). At or a little
under the boxes. (Two earlier art runs, 10.16 and 11.75 ms, were taken while other sessions' bakes loaded the machine: their
p95 of 15 and 22 ms and 230 ms worst frames are the load's, and the back-to-back runs replace them.)

**Checks** (2026-11-11): `--factory-art` PASS (106,438 checks with the bank: the poses against the box rig at every 0.05 s of two
rounds, worst under 2 mm; the kept boxes; the layout; the six pilot columns; the wiring; the bank's covers, tiers, play-space
rule and spawns); `--stage-art`, `--surfaces`, `--transitions`, `--stage-kits`, `--map-style`, `--emporium-art`, `--club-art`
PASS; the full CoreTests suite PASS (exit 0, twice); the Unity compile check PASS (Assembly-CSharp, Assembly-CSharp-Editor); the
Editor's build checks: @@VALIDATE@@.

**Seen in the shared Editor** (a match, one idle bot, god mode, the game's camera placed by `maincam`): thirteen views before
(the box kit) and after (the art) at High and on the Low graphics tier (which draws the bank's Low tier: coarse stripes, six-sided
bar stock, no small parts); the hazards on the art: a rail saw swinging out along its track under its guard, a press slammed
with its lamps red and lifted again, the crane travelling with its bundle and beacon; the slitters' lamps along their trenches.

**Not done / not seen**
- The backdrop outside the shell (the blast furnace, stacks, sheds, the sign) is still Core `NativeFactoryScenery`'s box-built
  sectors: they are the main menu's too, where no bank draws, and inside the hall they show only over the walls.
- The crane's drop onto a zone, a slitter's blade up close and a bloodied blade were not caught on camera on the art (their poses
  and the kept smears are checked against the box rig); the killcam on the art, a bank-absent run in the Editor with this
  branch's code (the fallback is checked by source), multiplayer guests, a native Windows build and the main menu: not run.
- Fine tier z-fight measure 0.17 m2, over the 0.1 m2 bar by the slivers above.
- On the hall roofs the lanterns' glazing and the vents have no collider (reached only by stacked jump chips through a skylight).
- Under the toon grade the plastered blockwork screens shade in soft patches; the forklifts' cabs are open (their blocks are solid
  cover).
- The bank is 21.2 MB, a little over the 20 MB the budget names.
