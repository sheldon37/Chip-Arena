# Bisect gib: split a body in two

Owner request (2026-10-12): "For the buzzsaw death on factory, can we add essentially a "split in the middle" gib? This can be useful for future things too".

The bisect gib is presentation only. No hitbox, damage, timing, network or hazard rule reads it, and no simulation file references it (the managed checks enforce this). Every client builds it from the replicated death record, so the host, guests, late joiners and the killcam see the same split.

## Pieces

| Layer | File | Role |
|---|---|---|
| Core (engine free) | `Core/NativeBisect.cs` | `Split`, `Plan`, `Request`, `Applies`, and the effect functions (`Shudder`, `Sparks`, `Gush`) |
| Core | `Core/NativeSawBisect.cs` | The Factory saws: which saw made a death, and its request |
| Core | `NativeBodyModels.CutLook(skin)` | The cap look per body skin |
| Runtime | `Runtime/NativeBisectModels.cs` | Gathers the meshes a body draws, runs the split (on a worker), and uploads the half models |
| Runtime | `Runtime/NativeBisectCorpse.cs` | Two `NativeCorpse` halves in one physics scene: a hold, then they part |
| Runtime | `Runtime/NativeBisectPresentation.cs` | The saw death, its blood and sparks, the `Gore` switch and the Dev Sandbox preview |

## Core API

- **`BisectSource{Bone, Group, Mesh, Fill, Cap}`** is one mesh the body draws. `Mesh` is in its own joint's draw frame, exactly as uploaded. `Group` indexes the `looks` array (0 is the body, 1 the hat). `Fill` is a closed, never-drawn stand-in whose section caps an open mesh: a wound cut's skin is open at its hole, so its unwounded part fills the cap. `Cap=false` means the mesh is cut but never capped (glass, inset lines).
- **`NativeBisect.Split(sources, draw, plane, looks, tier, seed)`** returns a `BisectCut`.
  - `draw[bone]` is each joint's mesh-to-world matrix (System.Numerics row vectors).
  - A mesh wholly on one side goes to that half untouched (`Whole`); a crossed mesh is clipped exactly.
  - Each ragdoll body (`NativeBisect.Bodies`) and look group gets one sealed cap per half. The cap is the union of its closed meshes' sections, coloured by depth from the edge (`BisectLook`: the shell's paint, then the lining band, then flesh layers).
  - The split is deterministic for the same inputs and seed. Cell sizes are `Cells[tier]`.
  - It only reads its inputs, so it can run on a worker thread.
- **`NativeBisect.Plan(sources, draw, plane)`** is the cheap part: which bodies each half keeps and their bounds, without pieces. The corpses are built from it at once, before the split finishes.
- **`BisectLook`** sets the cap paint. `NativeBodyModels.CutLook(skin)` gives each skin's look: Standard is the darkened shell, then the undersuit with accent conduit glints, then flesh; Sheldon Clothes uses the coat lining; the Prototype is shell, then flesh. `BisectLook.Hat` caps in the hat's own paint.
- **`BisectRequest`** is a cut and how the halves leave it:
  - `Cause`, `Point`, `Normal`; half 1 lies along `Normal`.
  - `Travel`: the cutter's velocity.
  - `Away0/1`, `Spin0/1`: each half's parting velocity and tumble.
  - `Hitch`: seconds the halves hold on the cutter. `Blood`: the blood scale.
  - `Sparks`: a metal cutter.
  - `Hub`, `Axle`, `Rate`, `Radius`: a spinning blade (blood flung off its rim, sparks along its motion). Leave `Radius` at 0 for none.
  - `Seed`.
- **`NativeBisect.Request(cause, point, normal, travel, seed, part, lift, push, topple, hitch, blood, sparks)`** builds a request from a plane and a few feel numbers.
- **`NativeBisect.Applies(gore, style, kept, model)`** decides whether a death splits: gore on, a pilot body, not a kept (revivable) PvE body, and a stock ending (`impact-ragdoll`/`ragdoll`). A killer's purchased kill effect keeps its own look.
- **After the split nothing allocates.** `Shudder(r, age)` (the hold's tremble), `Sparks(r, age, quality, into)` (at most `MaxSparks`, into a caller array) and `Gush(r, k, ...)` (the blood sprays) are pure functions of the request and the age.

## Adding a cause

A new cause (a greatsword execution, a laser, a Kernel Panic finisher, a minion) only maps itself to a `BisectRequest`:

1. In Core, from replicated data only, decide that the death qualifies, then build the plane, usually with `NativeBisect.Request` and a few feel numbers. `NativeSawBisect.Find` and `Request` are the worked example: the plane is the killing blade's (normal = axle) through the pelvis.
2. In `ArenaPresentation`, add a method like `SawBisect`. It checks `NativeBisect.Applies(Gore, ...)`, then returns `SplitCorpse(arena, death, rig, -1, pelvis => <your request>)`. Add your check where `SawBisect` is called in `NativeDeathPresentation` (corpse creation). A null return falls back to the current corpse.
3. Everything after is shared: the split, both half corpses, the hold and part, the fountains, the cut blood and sparks, the floor splats, and `ReleaseBisect`.

## Factory saws

- The authority kills with the first lethal hazard touching the body, in `Map.hazards` order. `NativeSawBisect.Find` repeats that from the death record: the record's time (and one frame earlier), the round and `FightAt`. If the first touching hazard is not a saw (the crane, a press, a lethal vent), the death is not split. If nothing touches (the record's float time missed the tick), it takes the nearest lethal saw within 0.75 m.
- **Slot saw** (axle z): the halves part to the sides and topple outward. **Rail saw** (axle x): the cut is front and back.
- The halves hold on the blade for 0.17 s (slot) or 0.13 s (rail). They shudder, with sparks and blood out of the seam, then part through the existing ragdoll path.
- Each half's trunk fountains. The blood along the cut and flung off the rim lands as the round's splats. The blade keeps a drying blood smear (`NativeFactoryRig.Build(..., blood)`). The weapon drops whole, with the half its hand is on.

## Switches and preview

- **`ArenaPresentation.Gore`** (default true). False keeps every death as it was: no split, no cut blood, no blade blood. There is no player setting for it yet; this is the field one would drive.
- **Effects settings.** Reduced effects and the low graphics tier thin the sparks and blood; the split itself stays.
- **`SplitInBackground`** is false on WebGL, where the split runs at once. A body first seen after its hold (a late join, a corpse rebuilt after the killcam) also splits at once.
- **Dev Sandbox: "Preview Saw Split".** It cycles every skin through the slot and rail cuts on a stand-in 3.2 m ahead, every 3.4 s.
- **Checks: `Tests/BisectChecks.cs`.** They cover determinism, volume, silhouette, the see-through sweep, the cap area, every skin and tier with a headshot, the saw plane, the gore-off fallback, no allocation after the split, and the wiring.
