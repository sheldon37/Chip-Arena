# Belvedere Camp: the Expedition hub's life

The owner, 2026-11-20: "the hub still seems really empty? I thought we were bringing more life to this and things." Then: "Yes, start
the camp".

This is W5 of `docs/EXPEDITION-OVERHAUL.md`, which carries R6 (a more detailed hub: survivors, interactions) and R10 (the chip and
healing machines in the hub). Everything here is presentation. Rules, collision, spawns and networking stay Astra's (A1).

**Code:** `Core/NativeCamp.cs` (the plan), `Core/NativeCampFx.cs` (light and fire), `Core/NativeCampSynth.cs` (placeholder sounds),
`Core/NativeCampMeshes.cs`, `Runtime/NativeCampDraw.cs`, `Runtime/NativeCampAudio.cs`, `Runtime/NativeCampWalk.cs`.
**Art:** `tools/stages/camp_kit.py`, `camp_parts.py`, `camp_bank.py` (the bank `ChipArenaCamp`). **Checks:** `Tests/CampChecks.cs` (`--camp`).

## The rule that shapes part 1 (plan Q19)

Until Astra's A1 builds the hub blocks, nothing stands on the plaza floor that a pilot could walk through.
- Art below the gates' track (3.4 m) lies within 0.25 m of the fence's inner face, where the fence stands. It never enters a gate's
  leaf travel or pocket (5-10.3 m either side of a gate, from the inner face to 0.5 m inboard, up to 3.4 m).
- Strings and sails hang from 3.5 m (every bulb from 4.3 m).
- Nothing stands where an A1 block will, so part 1 never moves when the blocks land.
- No survivor is drawn in a live run until A1's structures exist. (They exist since A1: see "Part 2".)

`CampChecks` holds every piece's envelope and base to these rules. With `CHIPARENA_CAMP_BANK` set, it also holds every vertex of the
bank's meshes to them, and checks that no face lies flat on a surface the hub already draws (the floor and the dress's asphalt
at y 0, the fence's inner face, its top), which would z-fight. `camp_bank.py` runs the envelope check when it builds the bank.

## Part 1: the live camp

The fixed frame is the sanctuary: 49.6 m square, fence inner faces at ±24.8, four 10 m gates. It is the same on all five Meridian
plans. The run's world seed varies only the dead bulbs, the fires' phases and the sails' colours.

| What | Where |
| --- | --- |
| Fence lamps: caged bulkheads on goosenecks over the fence top, heads at 3.7 m | 7, each side between its fires and corners |
| Sail masts: scaffold tubes clamped to the inner face to 4.8 m, a small lamp at 4.3 m | 8, at ±19.6 m along each side |
| Corner masts: lattice masts in the inner corners to 7.2 m, a warm flood at 6.7 m aimed into the camp | 4 |
| Gate masts: on the fence top at each opening's edges, a flood down the lane at 4.5 m, the strings' eyes | 8; the north gate's east mast carries the camp PA |
| Bulb strings: N-S and E-W spans along ±5.35 m (the "#" round the future mess ring), fence runs from each corner mast to its gate masts, a string across each gate | 16 strings, about 420 bulbs, one in about fourteen dead |
| Shade sails across each corner, from the corner mast (6.4 m) to its two sail masts (4.6 m) | 4, in the survivors' tarp colours |
| Half-drum fires against the inner face (0.245 m deep), coals and logs | 8, two near each corner |
| The war board, flat on the north fence at x 15.4-19.0 | The run's plan painted on plywood: streets, routes in chalk, red pins and yarn from the camp; TILESET: CITY; the three dealt instances' posters, crossed in red as the view clears them; the desk lamp |
| The MISSING board, flat on the north fence at x -16.5 to -13.0 | 24 flyers |
| Gate signs hung from each gate's string (4.0-4.62 m) | CITY HALL / BELVEDERE CAMP over the north gate; EAST, SOUTH, WEST GATE |
| The fuse board on the east fence over A1's generator (z -12.5), the cable round the fence at 3.05 m | |
| Notices | CURFEW 6:47, STAY IN THE LIGHT, BOIL ALL WATER, RADIO 64.7 FM |

**Light** (art bible 3.2: warm light means people):
- Each lamp, flood and fire lights the fence, the plaza and the tents round it through the city's lamp grid (`NativeCityLamps`,
  2026-11-21, with Meridian's street lamps: `docs/expedition/MERIDIAN-DRESS.md` "The light"), and has a halo; every lit bulb near
  the eye has a soft halo. (Before, each laid a flat pool disc on the plaza, and only the three in the practical slots lit anything.)
- The camp still takes up to three practical slots through `NativeLighting.KitLights` (two with reduced effects, one on the Low tier),
  for the pilots' light: the lights that matter most at the eye, by the hazard kit's own score, eased in and out. They are marked
  (-1), so ChipArena/StageLit takes them from the grid and never lights a surface twice.
- The fires breathe under 2.4 Hz (under 0.7 Hz with reduced effects, which also drops their embers). Lamps and bulbs are steady.
  Nothing flashes.
- The war board's desk lamp clicks on while the eye's pilot is within 4 m of the board.
- DARKNESS leaves the camp lit (R4).

**Sound** (`docs/audio/SOUND-REQUESTS.md`, "The camp's sound bed"): the two nearest fires' crackle, the generator's hum at the fuse
board, the PA's radio (64.7 FM), the murmur of people round the camp and the desk lamp's click. These are synthesized placeholders
until the owner's listening pass and batch.

**Draw:**
- The bank's props are combined once per run into six world meshes: one per side of the fence, the strings, and the sails with
  the cables. They draw with ChipArena/StageLit: Fine within 40 m, Low beyond and on the Low tier.
- The fires and halos draw as one effects batch.
- Live, and in the killcam, through `CampScenery` in `SourceWorld`. The meshes are combined behind the loading screen
  (`PrewarmCamp` in `PrewarmStage`), so the hub's first frame builds nothing.
- Without the bank (CI, cloud and public builds, `--ca-no-pack-banks`), nothing is drawn.

## Part 2: the camp on A1's blocks (live) and its DEV preview

Since Astra's A1 (`codex/belvedere-camp-a1`) builds the hub plan table's blocks, the camp below draws in every live run's hub, on
those blocks. The Developer walk shows the same camp as its DEV preview.

**Live** (`claude/camp-a1`, every build, with the camp bank):
- The structures' art (below) on their blocks. Each block's greybox gives way where the bank holds its art
  (`NativeCamp.ArtOf`, `NativeCampDraw.Hides`, read by the arena dressing on the generated hub only). Without the bank the
  greybox stays and nothing else draws. The blocks keep their collision; the art keeps within 0.25 m of their faces below 3.1 m
  (CampChecks measures 0.08 m at most on the bank).
- The survivors at their posts, the 13 from the start always, and each rescued survivor once the run's live view says its
  instance is cleared (`NativeCamp.Joined`: no new key or wire field). A survivor who joined while the pilots were away is
  cheered as they come back to the hub.
- The brazier burns in the fountain.
- The camp's machines and the REVIVE-O-MATIC are the live hub's own (Astra's A1 builds and serves them; their models,
  labels and the chip price with its mutators come from the machine presentation).
- The survivors' bodies are made behind the hub's loading screen, so none is made while the pods come down.

**The DEV preview:** Settings · Developer · Walk Meridian greybox, on any layout or deal, in Editor and development builds only.
The walk's map is the only map its switches (C, U, K, Y) answer to, and they are compiled only into those builds. It adds the
camp's two machines and the REVIVE-O-MATIC (the walk has no live ones) and the clearance overlay.

**What it draws:**
- A1's structures, on their blocks' exact boxes:
  - the mess ring (canopy and posts);
  - the fountain with its brazier burning (its fire and light join the camp's);
  - four benches, the fourth with a folded blanket, a crate and a lantern until its survivor joins;
  - the kiosk with its hatches, shelves and the awning over the chip machine;
  - the infirmary;
  - the lookout with its glazed cabin and searchlight;
  - four sentry towers;
  - the radio shack;
  - the supply cabin;
  - the generator;
  - eight sandbag walls.
- The camp's two machines (their real models).
- The survivors at their posts: 13 from the start, and up to five rescued joiners, never replicated. The nearest six within 30 m
  are the pilot model, dressed for their roles and living on pack clips (see Survivors below). The rest are the bank's rigid posed
  figures, seeded per run.
- Glass you can see through where a survivor stands behind it.

**Keys:**
- C: the preview on or off.
- U: how many rescued survivors have joined (0-5). A survivor joining brings a cheer or a wave from the standing civilians.
- Y: that cheer, to review it without a rescue.
- K: the clearance overlay, as filled boxes (never ring outlines): A1's blocks, the gates' leaf travel and track, the 4 m lanes,
  the ring's walkway, and the landing field's pods for the walk's deal (hull, skid apron, and the capsule at ExitReach 2.4 m).

A banner says DEV PREVIEW · BELVEDERE CAMP (W5 PLAN) · NOT GAMEPLAY.

### Survivors

Owner (2026-11-20): "For the people in the hub, We can use our player model. Of course, dress the NPC's appropriately".

**Roles:** Core `NativeCampSurvivors` gives every post a role. Each role has a clean outfit and a loop of pack clips.

**Outfits** go through the zombies' dressing (Runtime `NativeZombieModelPresentation.cs`), made clean:
- the pack's pieces;
- the garment bank's apron (the cook, the Quartermaster, the welder), lab coat (Doc, with her lanyard), coveralls (Sparks) and
  scrubs (the nurse);
- parkas with beanies or caps for the lookouts and sentries;
- a Locker skin tone and human eyes;
- no wear, rot or blood.

**Clips** come from the camp's own motion bank, `Resources/ChipArenaCampMotion`. It is local and never committed. The local tool's
intromotion request bakes it (the hold script is `scratch/camp/hold/bake-motion2.sh`). It holds:
- the Crafting pack's chair clips (Idle, Talk, Drink, Eat) for the benches;
- its work clips (Hammer-Table, Use, Chisel) at the kiosk and the infirmary;
- its kneel clips for Sparks at the REVIVE-O-MATIC;
- its idles, cheers and wave;
- the RPG pack's unarmed watch, alert and injured idles for the lookouts, the sentries and the rescued office worker.

**How the clips play:**
- They play as keyed poses (Core `NativeIntroPack`, as the intros' pack clips do), eased .45 s between beats.
- Each survivor's loop starts at its own phase.
- A seated clip's hips go down onto the bench (the bench's .47 m seat against the chair's .55 m), and its legs are re-solved to
  keep its feet where the clip has them.
- Props are drawn from the camp bank: a mug, a bowl, a hammer, and the radio operator's headset. Each sits in its fist (the
  binding's weapon frames) or on its head.

**Budget:** at most six survivors draw on the model, the nearest within 30 m, kept until 33 m (Runtime
`NativeCampSurvivorsDraw.cs`: the live hub and the walk). The rest stay the bank's figures, which draw one by one so that a
survivor on the model leaves its figure out. Without the camp's motion bank every survivor is its figure.

**Checks:** `CampChecks` holds:
- a role for every post;
- clean looks from the banks' own pieces, garments and gear;
- whole loops;
- the six-nearest pick and its slack.

With `CHIPARENA_CAMP_MOTION` set, it also holds every role on the bank's clips:
- whole poses;
- the feet by the floor;
- a seated clip's feet held while its hips go down;
- no allocation per sample.

`--camp-motion-probe BANK` prints each clip's hips, knees, feet and hands on the model.

## The hub plan table (the A1 contract)

`NativeCamp.Table`: the camp-* blocks Astra builds in A1, in the hand-off of 2026-11-20. `CampChecks` holds the table to every
clearance:
- inside the fence;
- clear of the gates' interact and sensor volumes, their leaf travel, pockets and track, and the 4 m lanes;
- the mess ring's 2.5 m walkway;
- the fence-vault rule;
- the benches' headroom;
- every row inside one of A0's landing-field reservations (`NativeExpeditionHubPlan`), so the spots never move.

Over 2,000 seeds, the pods' hulls and skid aprons clear every block by 1 m or more. The capsule at ExitReach 2.4 m clears them by
1.73 m or more.

**Changed by the owner (2026-11-21, "Move to 1 m"):** the awning posts stand at (14.85, 12.15) and (12.15, 14.85), 1.35 m out
along the chip machine's sides, 1.05 m clear of its body (the preview's (14.6, 12.4) and (12.4, 14.6) left 0.694 m). The kiosk's
art reads the posts from the plan, so the awning spans them wherever the table stands them.

**Changed while the art was modelled:** camp-kiosk is 3.2 m tall, not 2.6 m. The awning over the chip machine hangs from its top edge
above 3.1 m, and nothing stands proud of the kiosk below.

## With A1 (Claude, the same release: `claude/camp-a1`)

- Done: the structures' art draws on A1's blocks, each block's greybox hidden only where its art draws.
- Done: the survivors live in the camp, the visual body near and the rigid figures beyond 30 m, at most six skinned
  (CampChecks: who has joined, the art's reach, the awning's metre).
- Done by Astra in A1: `NativeMachineRules.Present` for the generated hub and the chip label's mutators. The kiosk and the
  infirmary dress the machines.
- Done: the bleed's wall finder skips every `camp-*` block (`Core/NativeCityBleedDress.cs`).
- Done: the climb-out (below).
- Next, after the release: the survivors' interactions (plan R6), driven by existing replicated events.

### The climb-out (ExitReach 2.4 m)

A1 moved each pod's record (the capsule where the pilot stands at GO) to the ramp's foot, 2.4 m ahead of the pod centre. The drop
(Core `NativePodDrop`) now has the pilot climb out to it:
1. braced in the jump seat until the bolts blow the door;
2. the harness up, standing up in the cabin (at most 0.45 s);
3. a walk out over the sill, down the ramp's tread and off its foot onto the capsule, easing in and out, by 0.05 s before GO.

The stand may end up to 0.9 s later than the old stand-in-place, so on the 5 s opening every pod's pilot reaches the capsule
just before GO. The first two pods' pilots walk (at most 2.2 and 2.6 m/s), the third and fourth jog (3.1 and 3.8 m/s; the
checks hold them under 3.2 and 4.6). The walk moves the
drawn body, so the model's own locomotion plays its stride. Nothing here is a rule: the capsule never moves, and the clock,
boarding and collision are Astra's. `PodDropChecks` holds the path (forward only, inside the doorway, on the floor, the ramp or
the crater) and its pace.

## Building the bank

Build it from the plan the CoreTests write, then run Blender headless:

```
dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --camp-dump PLAN.json
blender --background --factory-startup --python unity/ChipArenaConversion/tools/stages/camp_bank.py -- --plan PLAN.json --out OUT [--no-ao] [--preview DIR]
```

The bank goes to `OUT/ChipArenaLocal/Resources/ChipArenaCamp.bytes`. It is never committed: the integrator carries it in the Drive
zip.
