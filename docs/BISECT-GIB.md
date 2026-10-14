# Bisect gib: split a body in two

Owner request (2026-10-12): "For the buzzsaw death on factory, can we add essentially a "split in the middle" gib? This can be useful for future things too".

The bisect gib is presentation only. No hitbox, damage, timing, network or hazard rule reads it, and no simulation file references it (the managed checks enforce this). Every client builds it from the replicated death record, so the host, guests, late joiners and the killcam see the same split.

## Pieces

| Layer | File | Role |
|---|---|---|
| Core (engine free) | `Core/NativeBisect.cs` | `Split`, `Plan`, `Request`, `Applies`, and the effect functions (`Shudder`, `Sparks`, `Gush`) |
| Core | `Core/NativeSawBisect.cs` | The Factory saws: which saw made a death, and its request |
| Core | `Core/NativeMeleeBisect.cs` | Melee blade kills: the swing's plane (from `NativeMeleeSlice`'s plan) as a request, through the body or only the part it takes |
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
- **`BisectCut.Rooted[h]`**: whether half `h` has pelvis geometry of its own. A half without it (a head, a torso cut off above the hips) keeps the pelvis only as its corpse's root; `NativeCorpse` makes that stand-in still and collisionless and hangs nothing from it, so the piece cannot stand on an invisible pelvis.
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
  - `Mask`: the ragdoll bodies the cut may cross (bit = the body's joint). Everything else stays whole on half 0, whichever side it is on. All bodies by default; a melee head or limb cut names that part's (`NativeBisect.MaskOf`). `HalfOf(bone, point)` says which half a joint ends up on (the dropped weapon, an Auditor halo and the team bands use it).
  - `Seam`, `SeamHalf`: the line the cut's blood leaves along (unit, in the plane, through `Point`), in order the way the blade passed. Zero keeps the saw's vertical seam over the body's height.
- **`NativeBisect.Request(cause, point, normal, travel, seed, part, lift, push, topple, hitch, blood, sparks)`** builds a request from a plane and a few feel numbers.
- **`NativeBisect.Applies(gore, style, kept, model)`** decides whether a death splits: gore on, a pilot body, not a kept (revivable) PvE body, and a stock ending (`impact-ragdoll`/`ragdoll`). A killer's purchased kill effect keeps its own look.
- **After the split nothing allocates.** `Shudder(r, age)` (the hold's tremble), `Sparks(r, age, quality, into)` (at most `MaxSparks`, into a caller array) and `Gush(r, k, ...)` (the blood sprays) are pure functions of the request and the age.

## Adding a cause

A new cause (a greatsword execution, a laser, a Kernel Panic finisher, a minion) only maps itself to a `BisectRequest`:

1. In Core, from replicated data only, decide that the death qualifies, then build the plane, usually with `NativeBisect.Request` and a few feel numbers. `NativeSawBisect.Find` and `Request` are the worked example: the plane is the killing blade's (normal = axle) through the pelvis.
2. In `ArenaPresentation`, add a method like `SawBisect`. It checks `NativeBisect.Applies(Gore, ...)`, then returns `SplitCorpse(arena, death, rig, -1, pelvis => <your request>)`. Add your check where `SawBisect` is called in `NativeDeathPresentation` (corpse creation). A null return falls back to the current corpse.
3. Everything after is shared: the split, both half corpses, the hold and part, the fountains, the cut blood and sparks, the floor splats, and `ReleaseBisect`.

## Melee cuts

Owner task (2026-10-14): connect the melee impact information to the slicing presentation. Until now a melee kill only detached preset ragdoll joints along the swing's plane (`NativeMeleeSlice`, 2026-10-12). Now a lethal blade blow cuts the body's own mesh along that plane, with sealed caps, detached pieces and blood along the cut. It is presentation only, from replicated data only.

- **Which kills.** A pilot killed by a sword, greatsword or daggers blow (swing, sprint attack, jump attack, E special, Q/E kit hit), gore on, a stock ending (`impact-ragdoll` / `ragdoll`), not a revivable PvE body, not a Siren.
  - Everything else keeps today's joint slice exactly: gore off, a killer's purchased kill effect, a dagger's slit throat (no part comes off), every shield kill (crush, or the Aegis edge's joint).
  - Horde zombies keep their own launch path. They only gain the air-flag fix below.
- **The plane.** It is the one `NativeMeleeSlice.Plan` already derived: the posed blade of the attack index the attacker played (`MeleeSwing.VisibleSweep`), turned by its committed yaw (locked during the hit window), through where that blade passes the victim. Jump attacks and vertical kit kills chop top-down; spins, Flash Step and Kunai cut level.
  - New: the attacker's position comes from the **Hit event's own direction** (`ArenaEvent.Normal`, the authority's unit vector from the attacker's body centre to the target's at the hit's tick). Only the attacker's distance is the frame's. The plan also records where the plane meets the body (`SlicePlan.Point`) and the weapon (`Spec`).
- **Through the body** (a sword's trunk cut, every greatsword cleave, a sword or greatsword chop): the whole body is cut by that plane. Half 0 keeps the pelvis. The part above a level or sloping cut slides off along the swing (down the slope) and topples the way the blade went. A chop's halves fall apart to the sides.
- **Only a part** (a sword's head or arm cut, a dagger's limb on a finisher or kit kill, a dagger chop's arm): only that part's bodies are cut (`Mask`: the neck with the head, an upper arm with its forearm, a forearm).
  - The cut falls where the swing's plane crosses the part, kept inside it. The swing's bevel is kept within 35° of square across a limb and 15° across the neck.
  - The part flies along the swing with the old severed joint's kick and spin. The body falls on as it was going.
- **Feel.**
  - **Hold:** the halves hold for a hit-stop of 0.045 s (daggers), 0.06 s (sword) or 0.1 s (greatsword), 0.03 s more for a chop, and shudder.
  - **Blood:** the seam gushes from entry to exit along the blade's pass, and both halves fountain from their cut faces. There are no sparks.
  - **Momentum:** a moving body's halves keep its momentum, capped at 12 m/s.
  - The weapon, an Auditor halo and team bands go with the half their joint is on; a part cut keeps them on the body.
- **No stall.** The split runs on a worker. Through-cuts take at most 8 ms and 5.6 MB; part cuts about 1 ms and 0.7 MB (Standard Full body; managed timing here, not Windows).
  - If the split is not in by the end of the hitch, the halves keep holding (`NativeCorpse.SplitReady`, at most `HoldLimit` = 1.5 s more) instead of blocking the frame.
  - Bodies first seen late (a late join, a body rebuilt after a killcam) also split in the background now, for saws too; they used to split at once on the main thread.
- **Host, guests and the replay.** Every peer builds the cut from the same replicated swing, Hit event and death record.
  - The killcam's reset clears the hit ledger, so each death's first-seen blow is kept for the round (`killBlows`). The body rebuilt after a killcam and the replay's body are cut the same way. The cache is cleared at round end and on a full reset.
  - The ledger now reads a jump attack from the swing's replicated `AirAttack`. `!Grounded` was always false at a landing attack's hit, so landing kills never chopped.
- **Measured agreement.** A guest whose frame comes 30–150 ms after the hit, with the attacker 3–15 cm further along its lunge, gets the same plane orientation. The cut position can still be up to 7.6 cm off, and in 48 of 691 random blows a different part is taken (head vs arm by the height thresholds).

### Simulation/network contract still needed (Astra)

The attacker's position at the hit's tick is not replicated. The Hit event's `From` is the hit point (`Emit` defaults it to the position), so a guest takes the attacker's distance from its snapshot, which can be a tick or two late. A plane through a sloping blade moves by that error times its slope. For an exact cut on every peer:

- **Melee Hit/Headshot events** (primary swings in `StepMelee`, the landing arc and ring, sprint rings, melee Q/E kit and E special hits) should carry:
  - **`From`:** the attacker's Rigidbody position (body centre, the point `Damage`'s normal starts from) at the hit's tick, Unity world.
  - **`Projectile`:** the swing's `Id` (or the kit cast's id), greater than 0.
- **Damage, hit detection, interruption, kill credit and the wire format are unchanged.** Both fields already exist, are serialized and are validated (`NativeNetworkFrame`). No Hit event sets `Projectile` today.
- **Check before changing it:** today only the Cyber Lord's effect records (`NativeCyberLordFx`) keep a Hit's `From`, for the Cyber Lord's own hits. Impact cues read `From` only on Impact and Pong events, and the hit ledger takes the attacker from the actor. Melee weapon hits are therefore free to carry the attacker there.
- **The presentation side is already in place.** The ledger keeps them (`MeleeBlow.Exact`/`Struck`), and `NativeMeleeSlice.Attacker` uses `From` when it agrees with the event's direction within 2°, else falls back. It is dormant until the fields are filled. The checks prove a late guest then cuts exactly as the host.

### Checks

`Tests/MeleeBisectChecks.cs`; run alone with `dotnet Tests/bin/Release/net8.0/CoreTests.dll --melee-bisect` after building the test project.

- **Request validity:** 500 random lethal blows per weapon (reach, facing, index, jump, kit). Every request lies on the body and on the swing's plane, part cuts are kept across the limb, and the request is deterministic.
- **Body splits:** representative horizontal, diagonal and vertical cuts on the Standard body (and the Sheldon Clothes and Prototype bodies for chops and heads) split sealed, whole and capped on the plane, and a part cut takes only its part.
- **Blood and allocation:** the seam's gushes run along the cut, and the hold and gushes allocate nothing per frame.
- **Agreement:** host vs a late guest, and the exact contract path.
- **Wiring and isolation:** the ledger read and the runtime wiring are checked, and no simulation or wire file references any of it.

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
- **Checks: `Tests/BisectChecks.cs`** (and `Tests/MeleeBisectChecks.cs` for melee cuts). They cover determinism, volume, silhouette, the see-through sweep, the cap area, every skin and tier with a headshot, the saw plane, the gore-off fallback, no allocation after the split, and the wiring.
