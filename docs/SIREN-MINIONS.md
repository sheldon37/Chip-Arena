# Siren minions: zombie Pilots (design)

Owner request (2026-10-02): the Siren gets minions. They are procedurally generated Pilots (our player model) that act
like zombies, spawn throughout the map, and are friendly to the Siren and its team. They are zombie-like: rot, black
flame burns, wounds. "After design, we can work on spawn logic and etc."

**Status: design phase, presentation only.** The look, the animation set and a review gallery are implemented on
`claude/visual-revision-siren-minions` in `src/client/siren/minions/**`. **Nothing in the game uses them yet.** There are
no spawns, AI, damage, network fields or renderer wiring. The systems contract at the end is a proposal. Spawn rules,
caps, the AI and the snapshot fields are next-phase work and need the owner and Astra (the systems owner) to agree on them.

![A horde clawing out of a Ward Zero door, the Siren among them](siren-minions-horde.jpg)

![Face options and the animation states](siren-minions-sheet.jpg)

Top row of the sheet: the face options (blank, eyes, cracked) on two seeds. Below it: emerge, walk 1.3 m/s,
run 4.5 m/s, lunge, and death (headless SwiftShader, lab lighting).

## The look

Each minion is the normal Pilot rig: `createCharacterRig`, `RIG_VERSION` 2, with the same joint, socket and mesh names.
Because of that, every pose helper, IK, LOD and ragdoll path keeps working. The seed then corrupts the rig
deterministically:

- **Rot.** The shell turns ashen grey-green. Each part gets its own slight tint.
  - Uneven decay blotches with darker brown cores and rare sickly ooze specks.
  - Decay gathers low on the body and around burns and wounds. Some parts stay almost clean.
  - Grime runs down the lower end of every part.
  - The joints show dried flesh and blood. The gloves and boots are just filthy.
- **Black-flame burns** on 1–3 parts:
  - Charred black patches with pale violet ember cracks and a sooty, scorched rim.
  - Black-flame tongues lick off them. A tongue is the Siren's ink material in a voxel cluster: dark at the root, paling
    toward the tips, flickering and swaying.
- **Wounds** on 0–4 parts (usually 1–3):
  - Real openings carved with the body-damage carver (`body-wounds.ts carve`): a cavity, a slash, or a pair of
    punctures. The openings are lined with flesh and have torn rims.
  - A blood run drips below each wound.
- **Missing parts**, on about a third of the minions:
  - A hand or a forearm, leaving a stump with flesh, a protruding bone and a burnt ragged band.
  - Or the jaw plate, leaving a row of teeth and a dark hollow.
- **Dying glow.** The Pilot's glowing armor is dead:
  - The accent plates are dark grey-violet. Most of them have one crack that leaks the Siren's pale violet light, and
    some are fully dead.
  - The glow strips break into failing segments.
  - Everything flickers, and the whole minion's glow drops out now and then, like failing power.
- **Face.** There are three options; see below.
- **Weapon scraps**, in about half of the minions that have a right hand: a broken sword, a pistol held by the barrel,
  or a length of pipe, used as a club.
- **Body language.**
  - Hunch, head tilt and a dropped shoulder.
  - Arms that reach, hang limp, or one of each.
  - A dragged leg with a limp.
  - The walk and run cadence, and how often and how hard it twitches.

Every seed looks different, but they all share one palette and one set of rules, so they read as one family. Gore is
stylized and voxel-sized, in the same tone as the existing wounds. The palette lives in one place:
`materials.ts MINION_PALETTE` and the per-category self-light and glow.

## Face options (owner choice)

Pilots are blank-faced, so the face is offered as options. Pass `createSirenMinion(seed, {face})`, or change
`DEFAULT_MINION_FACE` in `look.ts`.

| Option | Look | Notes |
| --- | --- | --- |
| `blank` | No face: the rotted Pilot head | Closest to the Pilot; reads as "dead pilot". The least readable at range. |
| `eyes` (**default**) | Faint white Siren-style eyes burned into dark sockets, slanted toward the nose, a soot tear under one or both | Reads at range, even in the dark hall, and ties the adds to the Siren without giving Pilots a real face. |
| `cracked` | The head shell is split, the crack leaks violet light, and black flame burns from the crown | The most dramatic option. It adds one tongue per minion, and the tongues are the first to be cut in low detail. |

## Variety knobs

The seed decides every knob. `MinionOptions` overrides any of them, for tests, the lab and future systems such as an
elite variant.

| Knob | Range (seed) | Override |
| --- | --- | --- |
| Rot | light / medium / heavy (quantized so layouts can be shared) | `rot` 0..1 |
| Burns | 1–3 parts (back, torso, arms, legs, head side) | `burn` 0..1 |
| Wounds | 0–4 carved openings, usually 1–3 (cavity / slash / punctures) | `wounds` |
| Missing | none, left/right hand, left/right forearm, jaw | `missing` |
| Weapon scrap | none, sword, pistol, pipe (needs a right hand) | `scrap` |
| Arms | reach, limp, mixed | `arms` |
| Hunch | .18–.5 rad | `hunch` |
| Asymmetry | head tilt ±.35, shoulder drop .04–.16, dragged leg and limp .35–1 | (seed) |
| Rhythm | walk and run cadence, twitch rate .25–.6/s, jerk .5–1 | (seed) |
| Glow phase | 4 material pools, so neighbouring minions do not flicker in step | (seed) |

## Animation states

The poses come from the fields listed below, the seed and the time. Each minion keeps one small state, its gait phase:
the cadence follows speed, so the phase has to accumulate. That state is deterministic, clamps dt to .05 s, and restarts
when time runs backward or jumps by more than .35 s (kill-cam rewinds, late snapshots). The animation draws no random
numbers anywhere. Layers blend inside a numeric pose buffer, so states hand over without pops.

| State | Needs | What it does |
| --- | --- | --- |
| Idle | `yaw`, `vel` ≈ 0 | Weight drift, irregular breathing, head lolling, Siren-like head and shoulder twitches and spasms at hashed times. |
| Shamble | `vel` < 2 m/s (`hp`/`maxHp` below 40% sags it lower) | Hunched, lurching walk. One foot drags low, toe down, and swings out. The torso dips on the bad leg and pitches forward on each good step. Arms swing loosely or reach. |
| Frenzied run | `vel` > 3 m/s (cross-fade 2–3) | Crouched sprint with the head up, high knees, and wild alternating arm flails (reaching minions claw forward). |
| Lunge (grab) | `attackAt`, kind `lunge` | Claws raised, lunge, two-hand grab at chest height, bite, recover. Hands use IK. |
| Club | `attackAt`, kind `club` (default with a scrap) | Overhead wind-up with a torso twist, a diagonal swing through, and the free arm out for balance. |
| Smash | `attackAt`, kind `smash` | Two-handed hammer fist from overhead. |
| Flinch | `hitAt`, optional `hitYaw` | A snap away from the hit that rings out over .42 s, layered on any state. |
| Emergence | `spawnAt` | A black-flame pool spreads, hands burst out of the floor, the head and shoulders rise, the hands slam the floor and haul the body up, a knee comes out, and it jerks awake into its walk (1.95 s). |
| Death | `deathAt` | Starts from the exact pose at death. It recoils, the knees buckle, and it topples forward. It chars part by part and burns away feet first, eyes last, into black flame and ash (2.3 s). Effects end at 3.1 s, leaving a scorch. |

- **Feet are ground-anchored.** Both legs use the shared IK (`rig-ik foot`). A planted foot moves backward in the rig
  exactly as fast as the body travels, and the cadence rises with speed so a stance never asks the short legs to reach
  too far.
- **Measured with the shared probe** (`tests/helpers/motion-probe.ts`) at .8, 1.3 and 4.5 m/s and 30/60/120 Hz:
  - The good foot slips under 2 cm while planted.
  - The median planted-foot speed is 0.
  - The dragged foot scuffs by design.
- **Attack hit times.** The claws or club arrive at `MINION_TIMING.hit`: lunge .34 s, club .40 s, smash .43 s after
  `attackAt`. The full cycle is .9 s, the chaos zombie's cooldown.

## Performance

These are Node CPU measurements in this container, not the owner's GPU. They come from `prewarm`, `createSirenMinion`,
`animateSirenMinion` and `sirenMinionStats`.

**Meshes and triangles per minion:**

| | Value |
| --- | --- |
| Meshes | ≤ 53 (target ≤ 55); about 50 drawn when idle |
| Base | The Pilot rig's 48 meshes, minus hidden missing parts |
| Additions | Up to 3 tongues (the third is hidden in low detail), 1 weapon scrap, 1 effect mesh (drawn only while emerging or dying) |
| Instances (voxel boxes) | Average 930 (max about 1,160), against the Pilot's 218 |
| Triangles | About 11,200 per minion (max 13,900); a Pilot is 2,600 |
| Rot, burns, cracks, eyes, wound flesh | Instance colours in the parts' own layouts, so they cost no draw calls |

**Sharing:** every minion shares the geometry, layouts and materials. That is at most 48 materials: 6 categories × 4
flicker pools × alive/charred. There are about 520 cached layouts, around 1 MB.

**Creation cost:**
- About 15 ms per minion while the layout cache is cold, and about 1.6 ms once it is warm.
- `prewarmSirenMinions()` warms the cache in about 0.5 s. Call it during the Siren intro, or split it across frames.

**Animation cost for 20 minions:**

| State | Per minion | Per frame |
| --- | --- | --- |
| Alive (walk, run, attack mix) | 15 µs | 0.30 ms |
| Emerging | 28 µs | 0.57 ms |
| Dying | 25 µs | 0.51 ms |

**Low detail** (SVG compatibility, performance graphics; `setSirenMinionDetail`): every part becomes its tinted plain
box, the third tongue is hidden, and the effects drop to half the cubes when quality < .5.

**Budget risk: draw calls.** 20 minions are about 1,000 draws, plus a shadow pass. Recommendations for the next phase:
- Cap alive adds at about 12.
- Turn `castShadow` off beyond about 15 m.
- If larger hordes are wanted, add a crowd renderer that draws all minions' boxes per material in one instanced draw.
  The shared layouts make that possible.

## Proposed systems contract (NOT implemented; owner + Astra)

**Snapshot.** Reuse the chaos minion pipeline. It already has `MinionView`, `summon`, `removeMinion`, `hitMinion`, chase
AI, `enemy()`/`canHit()` friendliness and the cap of 34. Add a kind with optional fields:

```ts
// src/shared/chaos.ts (systems owner)
interface MinionView { id; owner; kind:'zombie'|'toy'|'siren-add'; pos; yaw; hp; maxHp; speed; until; lastAttack;
  seed?:number;      // uint32 look seed (or derive minionSeed(id))
  vel?:Vec;          // feet and gait (needed for siren-add)
  spawnAt?:number;   // emergence start; the add is inert for MINION_TIMING.emerge
  attack?:'lunge'|'club'|'smash';  // lastAttack is attackAt
  hitAt?:number; hitYaw?:number;   // last damage taken, for the flinch
  deathAt?:number;   // keep the add in the snapshot (no collider) for MINION_TIMING.deathFx after death
}
```

A separate `Snapshot.sirenAdds` would also work, but it would duplicate the minion systems. Keeping a dead add in the
snapshot for 3.1 s keeps the death rewindable for the kill-cam.

**Friendliness.** `owner` is the Siren player's id.
- The existing rules then give the requested behaviour:
  - `enemy(owner, q)` picks targets.
  - `hitMinion` ignores the owner and allies.
  - `canHit` honours teams and the friendly-fire option.
- The result: adds never target or hurt the Siren or the Siren's team, and the Siren's attacks should pass through its own adds.
- If the Siren dies or disconnects, its adds burn away: `deathAt` is set, which plays the death presentation.

**Spawns.**
- In Ward Zero: `SIREN_ADD_SPAWNS` (4 isolation cells and 4 ward doors, each with an inward yaw). The emergence pool
  fits a cell or a doorway.
- Multi-add waves need a clearance check: the cells are about 2.4 m from the back wall (DEV_LOG Ward Zero risk).
- Elsewhere ("throughout the map"): a shared `sirenAddSpawnPoints(map)` helper. It would raycast floor points on a grid,
  away from props, vents and players (for example ≥ 4 m), preferring points out of the target's view.

**Suggested tuning to start from** (owner decides):
- At most about 12 alive (performance).
- Waves of 2–4, cued by the boss song's sections.
- 60–120 HP each.
- Shamble at 1.1 m/s and run at 4.2 m/s (frenzy when close or below half HP).
- Melee range 1.6 m, 8–12 damage, .9 s cooldown.
- Inert while emerging.

**Client wiring (systems/integrator):**
- In `ChaosVisuals`, skip `kind==='siren-add'`. A small `SirenAdds` view then builds `createSirenMinion(seed)` per id.
- Each frame, set `group.position = pos` and `rotation.y = yaw`, then call `animateSirenMinion(minion, view, state.time)`.
- Call `setSirenMinionDetail` for compatibility or performance graphics.
- Dispose each minion after `deathFx`.
- Never `recolorCharacterRig` or `dressRig` a minion.
- Re-export the minion façade from `src/client/siren/index.ts`. That file is the Siren visual owner's façade and was not
  edited in this design pass.

**Network cost:** about 70 bytes of JSON per add per snapshot, so 12 adds come to about 1 KB per snapshot.

**Open questions for the owner:**
- The face option (default `eyes`).
- Wave pacing and caps.
- Whether adds appear outside Ward Zero.
- Whether players can block or roll their attacks like a player's melee.
- Whether the Siren's chips buff them.

## Review

Run `npm run dev` and open `/tools/visual-lab/index.html` with one of these queries (the parameters are listed in the
`tools/visual-lab/minions.ts` header):

- `?kind=minions` shows a grid of 12 seeds idling. Add `&pilot=1` for a Pilot reference cell, `&faces=1` for every face
  option, or `&low=1` for low detail.
- `?kind=minions&view=strip&seed=7&states=idle,walk,run,lunge,club,smash,flinch,emerge,death&cam=side` shows one row per
  animation state.
- `?kind=minions&view=ward&siren=1&aspect=1.778&song=50&camFwd=13.5` shows the horde in Ward Zero at a ward door.

A Developer Sandbox preview button was **not** added: it needs edits to `main.ts` and the renderer outside this design
pass.

## Checks (this branch)

- `tests/siren-minions.test.ts` (7 tests), all passing:
  - determinism and variety;
  - the rig contract and budget;
  - Lambert/Basic materials only;
  - low-detail fallback;
  - finite poses in every state;
  - emergence below the floor and burn-away;
  - no pops at death, emergence, attacks or flinch;
  - planted feet at 30/60/120 Hz;
  - rewind;
  - no `Math.random`;
  - the glow-flag encoding.
- `npm run check:types`, every `tests/siren-*.test.ts`, the voxel-model tests and the full suite. Results are in the
  branch handoff.
- Headless SwiftShader renders only. Native Windows/GPU look and frame rate were not checked.
