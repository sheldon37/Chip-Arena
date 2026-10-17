# Zombie design: the Siren's rotted pilots

Owner request (2026-10-17): "Yes fix the zombies animations (Design as well, it seems they are still on the lower quality end)
Improve these vastly." This document covers the design half: models, silhouettes, materials, shading and detail. Motion (poses,
gait, follow, secondary motion, attack curves) is a separate task on `claude/zombie-motion`. Presentation only: no hitbox,
collider, timing, damage, movement or network change, and every vertex stays inside the kind's capsule (HordeKindChecks).

![The 40-add horde in the Ward Zero corridor, before (top) and after (bottom)](zombie-design-crowd.jpg)

![Faces at close range, before (top) and after (bottom)](zombie-design-faces.jpg)

## What was wrong

The seven kinds were stacks of bevelled boxes on the 22-joint rig. A cube head carried a flat face plate with square glowing
eyes and a row of block teeth, and the torso and limbs were boxes with box armour. Up close at 1440p it read as voxel toys, not
as the pilots the Siren took. There was no ambient occlusion, every surface had the same Lambert response, and every add of a
kind and look was identical.

## What makes a stylised zombie read (the brief)

- **Silhouette first.** Each kind gets one exaggeration you can read at 30 m: the shambler's lopsided armour, the runner's long
  starved limbs and hooked claws, the brute's trapezius hump and rock fists, the spitter's glowing throat sac, the crawler's
  ribs and spine, the bloater's gut, the Hulk's mountain of muscle with a tiny sunken head.
- **The pilot underneath.** These are our pilots. Each look keeps different pieces of the pilot's white armour (the cube helmet
  cracked open, a pauldron, a sleeve, the chest plates, the back unit, shin plates and boots) and the dark suit, torn open over
  rotted skin. No two limbs match.
- **A face that holds up close.** The skull is sculpted rather than boxed: brow, sunken sockets with the glow deep in them, a
  collapsed nose, cheekbones and gaunt temples, lips torn back from long uneven teeth, a hinged U-shaped jaw.
- **Material contrast.** Skin, wet flesh, chalky bone, glossy shell, matte cloth and metal respond differently to light. Baked
  occlusion grounds the forms, grime runs toward the hands and feet, and the Siren's ink-blood gathers round the mouth and chest.
- **Crowd variety without new draws.** Each add gets its own skin tone, slightly different proportions and its own missing
  armour pieces, all through the instanced body tint, so a horde of one kind does not look copy-pasted.

## Decisions

- **Armour wraps the body.** The pilot's pieces are curved shells hugging the form: the helmet round the skull with its face
  smashed out, pectoral shells either side of a sternum ridge carrying the pilot's centre strip, a collar band, a dome over the
  deltoid, and sleeves and forearm guards wrapped round the limb. Flat slabs wider than a zombie limb read as boxes being
  carried. Flat chamfered slabs remain where the pilot has them (shins, knees, belt, back unit, the brute's driven-in shards).
- **The body is organic.** Lofted limbs with muscle masses, a ribcage loft, superellipse masses, and a skull sampled from a
  sculpted function, smooth-shaded with baked occlusion.
- **Colour carries the rot.** Grey-green skin mottled with bruised and jaundiced patches (baked per kind and look, plus a finer
  per-add pattern in the shader). Wet flesh in the wounds, chalky bone, cream teeth, and the Siren's ink in cracks, seams and
  eyes. The brute's and the Hulk's hides are darker and greener so their mass reads heavy.
- **Each kind's glow stays its own** (the shambler's violet, the runner's magenta, the spitter's acid green, and so on), from
  the existing seam colours, so gameplay reads are unchanged.

## How it is built

- `Core/NativeZombieKit.cs`: the organic kit on the existing `WeaponSculpt` path: lofted limbs with profiles, superellipse
  masses with displacement, curved armour shells with broken edges (`Shell`), broken chamfered slabs (`Armor`), tubes, strips on
  surfaces (veins, cracks, seams), claws, tatters and cables. It also holds `Finish` (occlusion bake, mottling, grime, blood,
  proportion regions) and `Variation` (the per-add tint). Every primitive is tier aware.
- `Core/NativeZombieAnatomy.cs`: the rotted pilot's palette, skull, scalp, jaw, neck, torso, arms, hands and legs.
- `Core/NativeZombieKinds.cs`: the seven kinds as choices on that anatomy (`Zb`), plus each kind's extras.
- `Resources/NativeZombieLit.shader`: a Built-in RP surface shader with the horde shader's inputs (scene tint, ink, actor and stage
  lights, struck flash, death dissolve), plus per-class wrap, specular and rim (`LightingZombie`), baked occlusion, skin mottling,
  per-add proportions, missing pieces and sway. It falls back to the existing horde shader if it is missing or unsupported.
- The zone kinds (Ironworks, Station) keep their classic bodies; only the occlusion bake reaches them. `ClassicRunner` and
  `ClassicBrute` stay for the Ironworks kinds that reuse them, so their triangle counts are unchanged.

### Shading data (per vertex)

| Channel | Meaning |
| --- | --- |
| `uv0.x` | surface class + 0.02 + 0.96 × occlusion. Classes: 0 plain (old bank), 1 skin, 2 flesh, 3 shell, 4 cloth, 5 bone, 6 matte, 7 wire/metal |
| `uv0.y` | variant (0 always, 1-4 optional piece group) + 0.02 + 0.96 × sway weight |
| `color.a` | proportion region: 1 none, 0.75 head, 0.5 torso width, 0.25 limb girth |

The 0.02/0.96 offset keeps the fraction away from the integer, so interpolation never crosses a class boundary.

### Per add (instanced `_BodyTint`, white = the model as built)

- `r = 1 + tone` (±0.45): skin lighter and violet, or darker and greener.
- `g = 1 + seed` (0.002-0.998): mottling offset and proportions: head ±5 %, torso width ±5 %, limb girth ±7 %, scaled about
  each joint's origin so the joint itself never moves. The capsule check uses the widest case.
- `b = 1 + mask/16`: four bits. A set bit collapses that variant's vertices to the joint origin. Variant groups: 1 the left arm
  and leg armour and the visor shard, 2 the chest plates and the brute's shards, 3 the back unit, 4 the right arm and leg armour.
  Mask 15 (everything gone) is never used.

The tint comes from `NativeHordeModels.Variation(id)`, set in `HordeQueue`, `HordeQueueJaw`, `HordePartsDraw` and the death body.
Nothing is allocated per frame.

### Budgets (triangles per model; the old budgets stand)

Budgets per tier: Full (nearest 24) 7,200, Reduced (to 40 m) 3,000, Impostor 600. Measured on every kind and look:

| Kind | Full before | Full after | Reduced before | Reduced after | Impostor before | Impostor after |
| --- | --- | --- | --- | --- | --- | --- |
| Shambler | 3,778-4,362 | 6,045-6,846 | 2,578-2,814 | 2,357-2,721 | 564-576 | 422-432 |
| Runner | 3,936-4,060 | 5,936-6,305 | 2,404-2,560 | 2,291-2,517 | 552 | 342-416 |
| Brute | 3,292-3,328 | 6,625-6,861 | 2,492-2,560 | 2,905-2,907 | 468-516 | 508-516 |
| Spitter | 3,766-4,046 | 5,912-6,616 | 2,566-2,794 | 2,262-2,559 | 564-600 | 420 |
| Crawler | 3,232-3,248 | 6,070-6,222 | 2,024-2,028 | 2,348-2,407 | 432-444 | 342-346 |
| Bloater | 3,984-4,232 | 5,980-6,820 | 2,604-2,764 | 2,442-2,827 | 528-564 | 440-452 |
| Hulk | 3,786-3,900 | 6,789-6,883 | 1,910-2,012 | 2,825-2,869 | 528-540 | 460 |

All tiers: Full 5,912-6,883 (mean 6,435; before 3,232-4,362), Reduced 2,262-2,907 (mean 2,587; before 1,910-2,814),
Impostor 342-516 (mean 427; before 432-600). The impostor got cheaper: it is now a block skull and slabs only.

Draw calls do not change: each kind keeps the same sections and materials, with one instanced draw per model part. The 40-add
crowd (24 full and 16 reduced, plus 5 hinged jaws) is 402 draws and 212 shadow draws, before and after. Its triangles go from
136,128 to 196,259 (+44 %).

## Dangling parts (for the motion agent)

These are attached to existing joint sections; none is a new joint. Their sway is in the vertex shader (`uv0.y` sway weight,
0 at the root to 1 at the tip; amplitude 1.8 cm x, 2.4 cm z; phase from the add's seed). A motion-side spring could replace it
later; the weights are already in the mesh.

- Tatters: pelvis rags (Pelvis), collar rags (Chest), sleeve stubs on bare arms (LArm/RArm, LClav/RClav), the shambler's hood
  shawl rags (Chest), the scalp flap (Head), the big kinds' waist rags (Spine), shin tatters (LKnee/RKnee).
- Cables: under the chest plates (Chest), out of the back unit (Chest), under the pauldrons (LClav/RClav).
- The spitter's drool (Head, from the upper teeth so opening the jaw never drops it) and the shambler's visor shard (Head).

## Notes for motion

- Joints are not renamed or reordered; the jaw is the existing hinged jaw model on the Head joint.
- Bowed heads hide the faces in a front view. A slight head lift on the idle and walk would show the new faces more often.
- The variation scales are about joint origins, so poses and planted feet are unaffected.

## Not verified here

No Unity Editor or Windows run. The renders come from an offline WebGL harness that runs the same Core mesh code and a port of
the shader's shared block. It is not Unity. The surface shader compiles through glslang's HLSL front end, not Unity's compiler.
