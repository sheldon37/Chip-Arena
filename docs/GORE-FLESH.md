# Gore flesh: the cut faces, wounds, lumps and bone

Owner, 2026-11-13: "Also gibbing- we do use new blood, but the gibbing seems to still use voxel flesh and wounds? Can we
essentially match the quality with our blood? Using higher quality bone structure and flesh."

Presentation only. No rule, timing, damage, hit volume or network state is read back or changed: which kills cut, where the
plane lies, what comes off and when are exactly as they were (`docs/BISECT-GIB.md`). Only how the flesh and bone are drawn
changed.

## What was voxel, and what it is now

| Piece | Where it is made | Before | Now |
| --- | --- | --- | --- |
| A split body's cut face (blade kills, the Factory saws) | Core `NativeBisect.CapOf`, Runtime `NativeVisualCutPresentation.cs` `MakePlane` | 1.2 cm cells of flat colour: a rim, then three reds, random blood cells | a cross-section per pixel: the skin's (or the sleeve's) own colour at the edge with a line of blood under it, a band of fat in lobules, then a few large muscles in fine bundles with pale sheaths between them, darker to the middle, blood lying in it, wet |
| A bone in that face | the model's skeleton (`NativeModelSkeleton`), its sections in `MakePlane` | 1.2 mm cells: ivory, tan, red | cortex, spongy bone and marrow (a long bone), a honeycomb (spongy bone), the two tables round the brain (the skull), each from its own section, wet |
| A severed joint's opening (a part cut or crushed off, a smash) | `NativeVisualCutPresentation.cs` `MakePieces` | a fan of three flat colours to the middle | the same tissue, by its distance in from the opening's edge |
| An open wound on a living pilot or a corpse | `NativeVisualWoundPresentation.cs` `VisualHolePit` | a tube of three flat reds | the layers down its wall (a bloody lip, fat, muscle), darker the deeper it goes; the skeleton still shows through |
| The skin round an open wound | `Resources/NativeVisualBodyFx.cginc` `WoundSoak`, the body's toon | nothing: a clean-edged hole | the skin and clothes about the hole soaked in the blood's red, wholly at the lip, thinning over a wound's width and further below it where it runs, ragged at its edge |
| A gib's fragments (crush, smash, burst) | Core `NativeKillDebris`, Runtime `NativeKillModels.cs` `KillChunk` | three noisy spheres in one flat colour, bone bits the same sphere in ivory | five torn lumps of wet tissue (a clump, a strip, a slab, two lobes, a shred) and three shards of bone (a shaft's end with its knuckle, a splinter, a piece of rib) with jagged broken ends showing marrow; scraps of the suit keep their colour, wet with blood in their hollows. The blood pack's lumps still take every fifth |
| A stump's cap on a joint sever (voxel corpses and the kill wound) | Core `NativeKillCaps.Cap`, Runtime `KillCap` | soft spheres in the cells' flat colours | the same lumps as wet tissue |
| A bone's stub where a body has no skeleton | `NativeVisualBodyPresentation.cs` `GoreCylinder` | a plain cylinder | a broken end: a flared ring of uneven teeth round the marrow |

All of it is one shader, `Resources/NativeFlesh.shader`, lit exactly as the blood pack is (`NativeBloodLight.cginc`: the
ambient, the key light with its shadow, the arena's practical lights, blast flashes and muzzle lights, a wet highlight from
the key and the strongest practical light) and in the blood's reds (`NativeBloodSplash.Tint` 0x7d0a12 for the blood lying
in the flesh). Nothing is textured: the tissue is noise in the body's own space (the death pose), so it stays put on its
piece as the ragdoll moves, and no pack asset is used or needed. The Drive zip is unchanged.

## What is left as it was

- **Classic (voxel) bodies' cut caps and carved wounds** (`NativeBisect.Split`, `NativeWoundCarve`): the voxel body is the
  voxel look by choice (Settings, Player model). Their stump caps and fragments do take the new lumps.
- **The horde's gore** (`NativeHordeGorePresentation.cs`): zombie wounds as lit voxels and flesh bits as blobs, the zombies'
  own system.
- **A bruise's shell chips and a burn's embers** (`NativeWoundPresentation.cs`, `WoundCube`): chips of the suit's plating
  and sparks, not flesh.
- **The Voxel Burst kill effect's cubes** (`KillCube`): a purchased kill effect's own look.
- **A body the cut cannot read** (no readable mesh): its mesh-copy caps (`VisualCapMaterial`) are the body's own material.

## How a cap carries it

`BisectLook.Flesh` names a cap's kind (`Tissue`, `LongBone`, `Spongy`, `Skull`, `Sausage`, `Pit`). With it, `NativeBisect`'s
cap keeps every cell its own piece and gives each vertex its exact distance in from the section's edge (at most
`NativeBisect.FleshReach`, 6 cm) and the nearest outline's own paint. The region filled is the same as before (the same
outlines, the same exact trapezoids at the edge), so the caps stay sealed. The cells' colours are still written, for the
Low tier.

The runtime puts these on the cap's mesh: `texcoord1` (depth, kind), `texcoord2` (the shell's paint, in the project's colour space), `texcoord3` (the point
in the death pose). The shader draws the layers from the depth, with their borders wobbled by noise.

## Tiers, skins and fallbacks

- **Low graphics tier:** the caps, pits and fragments are as before (the toon's coloured cells, the plain lumps). The cap's
  mesh is the same; only the material differs.
- **Full and Reduced:** the flesh shader. A cap is made once on the cut's worker (a trunk's, with its bones' faces on their
  fine cells, is a few thousand triangles; a limb's about a thousand); lumps and shards are nine meshes made once and drawn
  through the source-model instanced batches. Nothing is allocated per frame. The shader is in the stage warm-up's list
  (`StagePreloadPlan.InstancedShaders`), so the first kill does not compile it.
- **A build without the shader** (or a GPU that cannot run it) keeps the cells.
- **The Hot Dog** is sausage through (`docs/HOT-DOG-SKIN.md`): its cut is casing, a darker ring, then pale meat with flecks
  of fat and pepper (`Sausage`), and it shows no bone (it used to get the plain bone stubs).
- **Classic (voxel) bodies** keep their voxel caps and carved wounds: they are the voxel look by choice
  (Settings, Player model). Their stump caps and fragments do take the new lumps.
- **Kill looks:** Soul Drift still swaps every renderer of the corpse to the ghost; Frostbreak leaves the caps flesh, as it
  did. A corpse's dissolve takes the caps with the body.
- **Zombies** (the horde's own gore, `NativeHordeGorePresentation.cs`) are not changed here.

## Checks

`--visual-rig` (`Tests/VisualRigChecks.cs`, "ModelCut"): the cap's depth is the exact distance from the nearest edge (a
rectangle and a notched section), the shell's paint per edge, the same region as the cells' cap, the bones' kinds, the
fragments' bone flags, and the wiring (the shader in the blood's light, the Low tier on the cells, the Hot Dog a sausage
with no bones). `--melee-bisect` is unchanged and passes.
