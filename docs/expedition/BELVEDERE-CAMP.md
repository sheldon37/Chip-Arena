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
- No survivor is drawn in a live run until A1's structures exist.

`CampChecks` holds every piece's envelope and base to these rules. With `CHIPARENA_CAMP_BANK` set, it also holds every vertex of the
bank's meshes to them. `camp_bank.py` runs the same check when it builds the bank.

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
| Half-drum fires against the inner face (0.245 m deep), coals, logs, their soot | 8, two near each corner |
| The war board, flat on the north fence at x 15.4-19.0 | The run's plan painted on plywood: streets, routes in chalk, red pins and yarn from the camp; TILESET: CITY; the three dealt instances' posters, crossed in red as the view clears them; the desk lamp |
| The MISSING board, flat on the north fence at x -16.5 to -13.0 | 24 flyers |
| Gate signs hung from each gate's string (4.0-4.62 m) | CITY HALL / BELVEDERE CAMP over the north gate; EAST, SOUTH, WEST GATE |
| The fuse board on the east fence over A1's generator (z -12.5), the cable round the fence at 3.05 m | |
| Notices | CURFEW 6:47, STAY IN THE LIGHT, BOIL ALL WATER, RADIO 64.7 FM |

**Light** (art bible 3.2: warm light means people):
- Each lamp, flood and fire lays a warm pool on the plaza and a halo, and every lit bulb near the eye has a soft halo.
- The camp takes up to three practical slots through `NativeLighting.KitLights`: two with reduced effects, one on the Low tier. They
  go to the lights that matter most at the eye, chosen by the hazard kit's own score and eased in and out.
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

## Part 2: the DEV preview

The whole W5 camp from the hub plan table, for the owner's and the integrator's review before Astra's blocks exist.

**Where:** Settings · Developer · Walk Meridian greybox, on any layout or deal, in Editor and development builds only. It never draws
in a live run: the walk's map is the only map it answers to, and its switches are compiled only into those builds.

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
- The survivors at their posts: 13 from the start, and up to five rescued joiners. They are rigid posed figures, seeded per run, and
  never replicated.
- Glass you can see through where a survivor stands behind it.

**Keys:**
- C: the preview on or off.
- U: how many rescued survivors have joined (0-5).
- K: the clearance overlay, as filled boxes (never ring outlines): A1's blocks, the gates' leaf travel and track, the 4 m lanes,
  the ring's walkway, and the landing field's pods for the walk's deal (hull, skid apron, and the capsule at ExitReach 2.4 m).

A banner says DEV PREVIEW · BELVEDERE CAMP (W5 PLAN) · NOT GAMEPLAY.

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

**Changed while the art was modelled:** camp-kiosk is 3.2 m tall, not 2.6 m. The awning over the chip machine hangs from its top edge
above 3.1 m, and nothing stands proud of the kiosk below.

## When A1 lands (Claude, the same release)

- The structures' art draws on A1's blocks, each block's greybox hidden only where its art draws.
- The survivors move to the live camp: the visual body near, these rigid figures beyond 30 m, at most six skinned, with
  SurvivorChecks.
- The machines' presentation: `NativeMachineRules.Present` for the generated hub, the chip label's mutators, and the kiosk and
  infirmary as their dressing.
- The bleed's wall finder skips `camp-*` (`Core/NativeCityBleedDress.cs`).
- The survivors' interactions follow (plan R6).

## Building the bank

Build it from the plan the CoreTests write, then run Blender headless:

```
dotnet run --project unity/ChipArenaConversion/Tests/CoreTests.csproj -c Release -- --camp-dump PLAN.json
blender --background --factory-startup --python unity/ChipArenaConversion/tools/stages/camp_bank.py -- --plan PLAN.json --out OUT [--no-ao] [--preview DIR]
```

The bank goes to `OUT/ChipArenaLocal/Resources/ChipArenaCamp.bytes`. It is never committed: the integrator carries it in the Drive
zip.
