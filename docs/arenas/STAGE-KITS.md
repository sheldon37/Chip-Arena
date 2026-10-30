# Stage kits: the harbor kit bank on the Unity-era stages

2026-10-28 · Claude (presentation lane) · branch `claude/stage-kits` from `claude/pack-bank` (`55a6e5d`)

Owner (2026-10-28): "Lets do all of those", "I want to have advanced lighting and much better environment + stage
development", "Do whatever needs to be done". Toon Harbor's crates and containers replace box props on the Factory and the
Ironworks. The Expedition maps, the Factory and Cyber City are the direction; Brickfall is the baseline.

Presentation only. No collider, health, stage, hazard, spawn, network message, save or timing changes. Every look falls
back to today's look when the kit bank is missing (CI, cloud builds, the public source zips, `--ca-no-pack-banks`).

## What it does

1. **Block props.** A block the table names draws as kit bank props in place of its box: Factory cover crates (four crates on
   a pallet, a single crate on the stacked ones), drums (a drum cage), dock pallets, the two dock containers (a 20 ft
   container), the parts bins (steel crates on a pallet); Ironworks mould boxes (four crates), scrap and charge boxes (a steel
   crate, the kit's scrap still heaped on top); the town's crates and its five wild containers. The block keeps its
   collider, health, damage stages, fragments and every effect that reads its transform.
2. **Harbour yards** round Factory, City Ironworks, Central Station and the town's west edge, outside play space: quay walls
   over dark water, container terminals in stacks, ship-to-shore and quay cranes, moored freighters and tugs, lamps, fences,
   trucks and yard clutter. The lights atlas (windows, lamps, aviation lights) glows by each map's night level.
3. **A textured NativeBlockLit variant** (`BLOCK_PACK`) draws both.

## Rules (Core `NativePackDressing`)

- The table is `Assets/Conversion/Resources/PackDressing.json`: kits (`fill` one prop, or `grid` along × across, optionally on
  a pallet), per-map rules (block id, exact or `*` glob as MapLooks' blockLooks; the kit; `keep`, the last damage stage the
  props draw at), the yards and the lights atlas' `glow`.
- The pick is deterministic: the block id's hash (FNV-1a, `NativeBlockLooks.Hash`) mixed with the slot number picks each
  slot's prop and its half turn. Its longer footprint side runs along the slot's; near-square props take any quarter turn.
  Never random per frame, the same on every client and in the killcam, whatever the block order.
- `NativePackBank.Fit` places each prop in its slot: footprint centred, base on the floor, scaled uniformly, then stretched
  toward the slot by the kit's `stretch`.
- **The collider rule** (ARENA_PROPOSAL.md 2.1 rule 1): nothing proud of the collider (1 mm tolerance; the stage rule allows
  0.25 m below 3 m). No slot is left more than 0.25 m short of its box on a side or at its top, so no invisible collider
  edge stands out of the prop. Props must be **solid** groups (containers, crates, pallets, cages, cabinets, lockers,
  barrels, tanks…). Open frames, fences, scaffolding and vehicles never stand for a collider.
- **What keeps its box:**
  - no bank, no table or no entry for the map;
  - roles other than `static` and `cover`;
  - foundations;
  - collapse segment members and supports (they sag and fall with the destruction kit);
  - blocks over 13 m on a side or 6.5 m tall;
  - a missing, open or ill-fitting prop (the plan's `Kept` lists why);
  - at runtime: fragments and rubble, and a dressed block past its kept stage (S2 and the stub by default). Its damaged box
    then draws as today.
- **Kit parts** (`NativePackDressing.Covers`): a dressing kit part of a dressed block stops drawing with it when it lies inside
  the block grown 0.25 m and starts below its top. Parts resting on the top stay (the Ironworks' scrap heaps).
  - Factory: `NativePackStage.Strip` in `Pack`.
  - Ironworks: `iwPack`.
  - The town: `townPack`.

## Yards (Core `NativePackScenery`)

- Each yard has a kind, an anchor, a turn and an extent, and is built from the bank and integer hashes of its seed.
- Kinds: apron, quay, stacks (up to `height` tiers of 40 ft and paired 20 ft boxes), sts, crane, ship, tug, lights, fence,
  scatter, trucks, building.
- **Keep-out:** every piece stays outside the map blocks' footprint grown 2 m. `Build` throws otherwise, and the checks
  hold it.
- The props bake into one world-space mesh per bank material and tier (detail or not). The ground boxes bake as SourceLit
  sectors. All draw on `NativeSceneryBuilder.BackdropLayer` without shadows. The low tier leaves the detail tier out.
- Ship-to-shore cranes stand 20 m behind the quay edge: legs about 18 m either side of their origin, booms out to +79 m over
  the water, the machinery 33 m back.
- **The town:** its skyline leaves the docks' lots empty, and its west ground plate ends at the quay so the basin's water
  shows. This only happens while the docks draw (`NativeExpeditionCityScenery.Build`'s `reserved` and `westWater`);
  without the bank it is today's skyline.

| Map | Block props | Yards (real bank; triangles normal / low tier) |
|---|---|---|
| Factory | 16 blocks (52 pieces) | south quay beyond the stockyard: 3 STS, 2 quay cranes, a loaded freighter, 2 tugs, stacks, trucks, lamps, fence; 0.50 M / 0.22 M |
| City Ironworks | 5 blocks (8 pieces) | east of the hall: quay, 3 STS, freighter, tug, stacks, lamps, trucks; aprons round the hall; 0.43 M / 0.19 M |
| Central Station | none | north freight depot: stacks, quay cranes, trucks, lamps, a shed, aprons; 0.10 M / 0.09 M |
| Expedition town | 20 blocks (20 pieces) | west docks by the Ironworks gate: quay and basin, 3 STS, freighter, tug, stacks, lamps, trucks; 0.43 M / 0.18 M |

The Ironworks and the station are closed halls: their shells and roofs hide the yards from the floor, so those yards read
from outside cameras (the overview) only. Opening a door or skylight is collision work (HARBOR_PROPOSAL.md, Astra handoff).
The Factory's cranes stand over its roof and through its roof gaps. The town's west edge is walled by its buildings and the
Ironworks hall: from the street the cranes rise over the roofs; the quay, the stacks and the freighter read from the roofs.

## Runtime

- `Runtime/NativePackStage.cs`:
  - the table (JsonUtility, validated), each map's plan, one material per bank material (NativeBlockLit with `BLOCK_PACK`:
    the atlas, the colour at most 1, emission gain, the logos' cutout, glass drawn dark and opaque);
  - `NativePackDraws`: pooled instanced batches per bank mesh, no allocation once warm;
  - `NativePackYards`.
- `NativeArenaDressing`:
  - a planned block draws its pieces at its drawn pose, nudge and fall, with its dust coat, health share and seed;
  - after its kept stage it draws as its box;
  - the props join `NativePackDraws`, drawn after the boxes, casting shadows.
- `SourceWorld` calls `PackYards` (live and killcam); `RenderBackdrop` calls `PackBackdrop`. The shader warm-up draws the
  variant when the bank is present.
- **Shader** (`Resources/NativeBlockLit.shader`, `#pragma multi_compile_local __ BLOCK_PACK` in both passes):
  - albedo is the atlas texel times `_PackColor` (the instance tint); emission is the texel times `_PackEmit` and
    `_ArenaPackGlow`;
  - the same toon light (`ToonBlockLit` on the interpolated normal, the facet for the edge term), legacy light, practical
    slots, blast flashes, muzzle lights, Ward light field, ground haze and fog;
  - `PackSurface` (in SHARED): the style's albedo floor, soot from S1, the grime ramp up from the prop's base, top dust, the
    round's dust coat and the style's foot occlusion;
  - normals go through the instance's inverse transpose (cofactors);
  - the logos' cutout holds in the shadow caster;
  - outside the variant's sections nothing reads a 2D texture (`BlockLookChecks`).
- Low tier: block props draw (they replace boxes and cost about the same). The yards' detail tier is left out, and each
  loaded freighter is replaced by the empty hull (a fifth of its triangles).

## Animation, timing and network impacts

None. Hit timing, hitboxes, damage, cooldowns, movement and networking are unchanged; the props follow the blocks' own
transforms.

## Checks

- `CoreTests --stage-kits` (`Tests/StageKitChecks.cs`, also in the full run): the table (valid, Unity-era maps, selected
  solid props, every rule matching), the plans against a synthetic bank of the real props' bounds, the gap and proud rules,
  determinism and block order, variety, what keeps its box, the damage stages, the kit-part rule, the yards' keep-out and
  determinism, the town's skyline with and without the docks, and the runtime and shader wiring.
- `CA_PACK_BANK=<ChipArenaPackKit.bytes>` adds the same plans and the yards' triangle budgets against the real bank.
- `BlockLookChecks` now allows the variant's atlas.
- Offline renders: a WebGL harness in the session's scratch (`sk/harness`). It builds the scenes from the worktree's Core
  and runs NativeBlockLit's SHARED code as GLSL. There are before/after views of the Factory and City Ironworks, the
  yards, the town's docks and the station depot.

## Not yet / risks

- Not seen in the Unity Editor or a Windows build. The variant's HLSL compiles nowhere here, and the harness is a stand-in
  for Unity's forward path (one shadow map, a gradient sky, its own bloom).
- The props replace the factory kit's container and bins dressing. The rules cover only blocks whose look the kit props
  improve. Ironworks lockers, carts and racks keep the kit's storytelling parts.
- The yards add 0.1-0.5 M triangles per map on the backdrop layer (about 0.2 M on the low tier), in at most 13 draws.
- Destructible dressed crates switch from props to the damaged box at S2. This pop reads as the break, but the owner may
  prefer the props kept to S2 (`keep: 2`).
