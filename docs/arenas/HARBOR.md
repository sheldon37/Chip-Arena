# Pier 9 (`harbor`), rebuilt

2026-11-11 · Claude · branch `claude/rework-harbor` (base `claude/sleepy-ritchie-g8ruhv` at `f81e663e`) · one of the five M1
stages rebuilt from scratch (owner: "revamp ALL the current stages out of the blocky style ... Maybe even COMPLETELY rework ...
Pier 9 ... more of a unique feel, very good designs, and nice animations"; the theme approved 2026-11-11: "Yes, start them").

Status: **implemented on the branch and seen in the shared Editor; not pushed, not merged, not released.** The stage bank
`ChipArenaStagePier` is local (git-ignored): the integrator packs it into the builder's Drive zip.

The map keeps its id (`harbor`: saves, lobbies and the Random list key on it) and its name. The M1 container yard
(docs/arenas/NEW-STAGES.md, `docs/arenas/scripts/harbor_m1.py`, the Toon Harbor kit) is history.

**Pitch.** The last night of the season on a seaside amusement pier. Painted timber, iron and glass under thousands of bulbs:
a Ferris wheel turning off the west side, a two-storey carousel whose mounts rise and fall, a band stand with its dolls keeping
the beat, striped stalls and ticket booths, strings of bulbs from turret to mast. The Comet Coaster's track runs along the
channel between the two piers and its train comes through the crossing; a drop tower stands on each pier. Fireworks over the
sea, fog on the water, a lighthouse turning in the north-east, the seafront's lit hotels in the south. Under the boards there is
only the sea.

Original design: nothing here takes a name, layout, look or element from any other game or any real pier.

## Layout (docs/arenas/scripts/harbor_m2.py)

x east, z north, y up; point symmetric (x, z) → (−x, −z); team A south (the Gate), team B north. 314 blocks (all static), 8 FFA
spawns and two team tables of 4; the pier x ±20, z ±34; bounds x ±24, z ±38 (4 m of sea past every edge), ceiling 22, killY
−5.5 (2 m under the lowest floor plus the body: a fall from the boards dies about .75 s after it leaves them, just under the
water at −4.6).

| Level | y | What |
|---|---|---|
| Under the boards | −2.6 | A stair down a pit in each midway (13 risers), a landing under the deck, the catwalk out under the centre bridge and round the caisson that carries the track: a way across under the train. Rails with gaps; piles (full cover). |
| The boardwalk | 0 | Two piers (z −34..−5 and its mirror) across **the channel** (z ±5, open sea). The coaster's boarded trestle runs along it (x −20..20, z ±1.6); the centre bridge (5 m) and two footbridges (4 m) cross it; the 3.4 m gaps beside the track can be jumped. A patch of **missing boards** on each pier (2.4 m square, no rail) opens straight down. |
| Roof terraces | 4.2 | Each end pavilion's roof: twin turrets and the PIER 9 sign (cover), rails, open drops to the midway. |

- **The Gate** (pavilion A; its twin at the pier head): a hall with a great arch and two side doors, the fortune teller's kiosk
  behind the arch (floor to roof), a screen inside each door, the stair up the west wall (21 risers); the second way up is the
  prize crates stepped 1.1, 2.3 and 3.4 m outside the east wall.
- **The midway:** the ticket booth on the centre line; three stalls a pier (a back wall, two cheeks, a counter with a 1.6 m way
  in, a roof to stand on); planters with lamp posts and prize crates (half and full cover); a cart.
- **The rides:** the carousel (south pier, west) and the band stand (north pier, east): an octagonal base a step up (.2 m), a
  drum at its heart (full cover), an upper deck overhead (the mounts and the dolls ride there, out of the play space). The
  **drop towers**: a mast on each pier (south pier east, north pier west) inside its 4.6 m square.
- **Routes:** the midways, the three bridges, the catwalk under the boards, the terraces; a jump across the channel by the track.
- **Spawns:** team A in the Gate behind the kiosk, team B in the far pavilion. FFA: both halls, both landings under the boards,
  a stall by the channel on each pier, a stall by each pavilion's yard. The draft checks every FFA and A-B pair blocked under
  ±1 m jitter at three eye heights (3D), spacing (FFA 14.4 m, A-B 62 m), 3 m from every hazard volume on the spawn's floor,
  every spawn and plate reachable on foot, no tops in the 1.3-2.2 m band, stairs (risers ≤ .2 m, ≥ 1.6 m wide), point symmetry.
- **Nav** (generator): 2784 nodes, 5160 links (354 gated by the hazards); every spawn and all three levels reachable.
- **No invisible walls.** 68 pilot columns: 12 for everyone, each under art that hangs over a top (the six upper-deck boxes of
  the two rides under the turning mounts and the dome; the four turrets under their onion domes; the two masts under their
  crowns); 56 for bots only, all rails at a lethal drop (`tools/generate_stage_hazards.py` `EDGES`/`OVERHANG`, `open_stage`).

## Hazards (existing kinds; no rules or networking change)

The press kind times from the arm time (FightAt + 4 s); the maglev from FightAt. Nothing is live in the fight's first 4 s.

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **The Comet Coaster** `harbor-coaster` | `maglev` | The crossing's lane along the channel: x −20..20, z ±1.6, 0..2.6 m | Every 34 s from FightAt + 22; 7 s warning (bells, the wig-wag lamps, the lane's lamps chasing), 3.5 s danger (the lane burns red), the pass at 30 m/s with a 13 m train (1.77 s), 2 s clearing. The direction alternates each pass and round (a shuttle coaster). | Lethal; bots wait (the maglev nav gate). |
| **The drop towers** `harbor-drop-s`, `harbor-drop-n` | `press` | A 4.6 m square round each mast: (10, −15) and (−10, 15) | Every 24 s each, the north tower 12 s after the south. 3.4 s telegraph (the siren, the beacon, the square on the boards closing in, a column of light), a .7 s fall from 18 m, .6 s on the boards, a 2.4 s climb. First telegraph at FightAt + 20.9 s. | Lethal from the last of the fall through the hold and the first half second of the climb (the press kind's band: the underside under 1.8 m). |
| **The sea** | open edges | Every edge, the channel, the missing boards, the catwalk's gaps | — | A fall dies at killY −5.5, credited to whoever hit them last. |

How they are drawn (Core `NativePierLooks`, pure functions of the replicated state): in the pass the train is the simulation's
(`TrainAt`: its five cars cover exactly the kill box); before it, it falls back down the spike it stood on, reaching the pier's
edge at the pass's first instant at speed; after it, it runs up the far spike, stands, and comes back the other way (PierChecks:
in the lane exactly through the pass, never jumping). A gondola is the press's ram (`DropAt`).

## The look (`harbor-pier-lights`)

- **Light:** a cool moon in the east (bearing 72°, elevation 36°), a warm amber rim from the wheel's side, a violet trilight;
  linear fog 45-1100 m in deep navy; the anime style on (saturation 1.22, contrast 1.12, blue shadows, warm lights); the
  advanced lighting (light volume, scatter, beams; no weather); eight practical lights (both gates, the carousel in pink, the
  band stand in cyan, both booths, both crossings).
- **The set pieces that move all match long** (Core `NativePierRig`, on the render clock): the Ferris wheel (a turn in 80 s,
  sixteen cabins swaying on their pivots, its rim's and spokes' lamps chasing), the carousel's turntable and its ten mounts
  rising and falling (horses and sea horses, two rows), the band stand's eight dolls keeping the beat and the star turning over
  its dome, four boats rocking at their moorings; on the hazard clock: the train on its spikes, the gondolas, the beacons, the
  crossing's wig-wags and the lane's lamps. The marquee bulbs of the whole pier chase in three groups.
- **Effects** (Core `NativePierFx`): fireworks from the barge in the east (a shell every 2.3 s: peonies, willows, rings, the
  flash and its light on the water), fog banks on the sea and wisps in the channel, the pier's lights broken on the swell,
  the lighthouse's beams, the wheel as a wheel of light, lamp halos; the telegraphs (never reduced): the lane's amber and red
  glow, the wig-wags' glare, the train's headlamp and sparks, the drop square closing in, its column of light, the slam's
  flash, dust ring and splinters.
- **Backdrop** (Core `NativePierScenery`, baked SourceLit): near, the wheel's A-frames and landing stage, the trestles and the two
  spikes (to 35 m), the neck south to the shore, the pier head's landing; the fireworks barge, the lighthouse on its rock, bell
  buoys; far, the sea, the beach and promenade, the seafront's hotels with lit windows, the hills, the headland, ships.
- **Box kit** (Core `NativePierKit`, drawn only without the stage bank): 5433 NativeBlockLit parts; attached parts within .25 m
  of their blocks, thin lamp posts on their planters, free parts 2 m over every standable top or under it (PierChecks).
- **Audio** (existing clips, `pier.*` bank keys the owner's bank may fill): `pier.drop.warn`, `.slam`, `.lift`;
  `pier.coaster.warn`, `.danger`, `.pass`.
- **Low tier:** the beams fall back to billboards; atmosphere counts drop to a third; telegraphs are unchanged.

## Stage art

With the stage bank `ChipArenaStagePier` the pier is drawn from models made in Blender (`tools/stages/harbor_art.py`,
`harbor_props.py`, `harbor_kit.py`; the recipe is docs/arenas/STAGE-ART.md) in place of every block's box, the kit's and the
rig's boxes and the backdrop's near sector. The gameplay geometry is untouched; without the bank everything draws as boxes.

**Modelled:** the boards in planks with gaps over a dark bed (three shades); the boarded crossing with its steel rails; the
halls in cream boards with red pilasters, gold capitals and cornices, navy plinths and round-headed lit windows; the great
arches' spandrels and key lamps; the kiosks' glazed bays; the terraces' striped turrets with onion domes, ribs and finials; the
PIER 9 signs in bulbs; the stalls (shelves of plush bears, skittles and boxes, striped cheeks and counters, awnings with
scalloped valances); the chamfered ticket booths with brass grilles; the rides' octagonal bases, the carousel's mirrored drum
and the band stand's organ pipes, the upper decks' fascias and soffit lamps, columns, the carousel's sixteen-leaf canopy and
the band stand's ribbed dome and lantern; the masts in riveted red and white bands with guide rails and lamps, the drop
squares painted on the boards; planters with shrubs and cast-iron lamp posts with globes; crates with stencilled stars; the
sweet carts on spoked wheels; the crossing signals with crossbucks and bells; the rails as boarded panels between capped posts;
stairs; the caisson and round piles with weed at the water line; the strings of bulbs; the pile bents under the boards; the
track on past both edges. The near backdrop: the wheel's frame and landing stage, the trestles and spikes, the neck, the
pier head's landing. The rig (23 slots): the wheel and its three bulb groups, four cabins, the turntable with its poles, the
horse and the sea horse, the star, the doll, the boat, the car and the end car, the wig-wag, the lane lamp, the gondola ring,
the beacon, the three marquee groups.

**How it moves.** `NativePierRig` tags each box part a model stands for with the art's slot (beside the very add that builds
it); `NativePierArt.Pose` places each model at its tagged part's centre and turn, and lights the chasing groups and the lamps
by the frame's levels. No rules, timing, collider or network change.

**Budget.** The Mid tier's props share the Fine meshes (`--own-mid` builds its own); the Low tier is its own. The bulbs' corners
are shaded round so each is six vertices.

Rebuild (from `unity/ChipArenaConversion`; about 30 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout harbor LAYOUT.json
blender --background --factory-startup --python tools/stages/harbor_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b] [--preview-tier N]` renders the baked stage and the rig's props; `--tiers`, `--no-ao`,
`--static-only`, `--stats`.)

**The bank** (2026-11-11): 17,018,596 bytes (budget: about 20 MB), 566 props. Static: 60 map chunks; Fine 200,594 triangles
(211,457 vertices), Mid the Fine meshes again, Low 33,116 (47,111); the near backdrop in 3 named chunks of one tier, 15,520
(16,874). Rig: 23 props; 36,095 / 31,281 / 17,709 triangles by tier; up to 132 rig draws a frame (the pose buffer holds 512).

**No z-fighting.** Every tier's static scene goes through `stagekit.lift_coplanar` (8 passes, 5.5 mm tolerance at the Fine
tier), the backdrop's chunks too; mouldings are capped in convex strips. Measured with `zfight.py`: 0.04 m2 (Fine), 0.04 (Mid),
0.09 (Low) of coplanar overlap.

**Checks.** `--pier` (93,713 checks): the record, Random, the columns, nav, the hazards' timings and lethal windows, the train
against the simulation, spawns, cues, the look, the backdrop, the kit's collider rule, the set pieces' places, determinism,
no allocation per frame, the wiring, off the M1 paths. `--pier-art` (928,249): the slots, every pose against its tagged part
at every .05 s of two rounds, the chases, the layout, the fallback's wiring and, with the bank: every block covered, every rig
slot in three tiers, the chunks' tiers, the budget, the play-space rule (123,771 vertices within 2 m over a standable floor:
164 over .25 m from a block, the lamp posts on their planters, the farthest .50 m), the spawns clear.

## Music

The stage keeps its current track as the placeholder: the map record's `music` is `factory-anvil-loop` and
`NativeStageMusic.Theme` plays the owner's earlier song `harbor-container-terminal` (Container Terminal, written for the old
yard). The key the owner's new song will take is **`harbor-theme`**; its Suno prompt is in
docs/arenas/STAGE-MUSIC-PROMPTS.md ("Pier 9 — Last Night of the Season").

**In (2026-11-11):** the owner's **Last Night of the Season** (196.000 s) plays as `harbor-theme`; Container Terminal
stays packaged and the record's `music` stays the fallback.

## Files

- Draft: `docs/arenas/scripts/harbor_m2.py` (layout, hazards, checks, plan PNG). Generator: `tools/generate_stage_hazards.py`
  (`DRAFTS`, `EDGES`, `OVERHANG`); `UnityMaps.json` and `tools/asset-lock.json` regenerated (only the `harbor` record changed).
- Core: `NativePierLooks.cs` (ids, kit kinds, places, the train, the gondolas, cues), `NativePierKit.cs` (kit and rig),
  `NativePierScenery.cs`, `NativePierFx.cs`, `NativePierArt.cs`. Runtime: `NativePierPresentation.cs` (draw component,
  SourceWorld and prewarm hooks, audio). Art: `tools/stages/harbor_art.py`, `harbor_props.py`, `harbor_kit.py`.
- Shared files (small edits): `NativeSourcePresentation.SourceWorld`, `NativeStagePrewarm.PrewarmStage`, `NativeAudio.Render`
  and `NativeStageArt.Banks` (one line each); `NativeNewStageLooks` (Pier 9 out of `Maps` and `Looks`; `Index` keeps the other
  four stages' numbers through a private `Order`); `MapLooks.json` (the `harbor` entry replaced) and its pin in
  `asset-lock.json`; `PackDressing.json` (the `harbor` entry and the kit only it used, `drum-cages-2x1`, removed).
- Tests: `PierChecks.cs` (`--pier`), `PierArtChecks.cs` (`--pier-art`), both in the full run; `Program.cs`, `StageArtLayout.cs`
  (one line each); the M1 pins: `NewStageLookChecks` (Pier 9's column, weather, dressing, rig and gantry checks out; Skyframe's
  crane stands in for the gantry's telegraph check), `StageKitChecks` (the dressing count), `AdvancedLightingChecks` (the look's
  name), `TransitionLoadingChecks` (the SourceWorld hook).
- Left in place, unreachable for `harbor`: the M1 kit's, scenery's, rig's and effects' Pier 9 branches in `NativeNewStage*.cs`.

## Seen in the shared Editor (2026-11-11)

A match, one idle bot, god mode, the game's camera placed by `maincam`: before (the M1 yard, six views), the box kit without
the bank (twelve views: the fallback), and the art (thirteen views: the midway, the carousel, the Ferris wheel, the channel
and a mast, the band stand, under the boards, the terrace, a stall, the spike and fireworks, the Gate and its sign, from above
with the train in the lane, a rail and the shore); the south drop tower through its telegraph (amber, then red, the column
of light, the gondola on the boards); the coaster's warning and pass from a stall's roof (the wig-wags, the headlamp and
sparks); the Low graphics tier (four views). The Editor's build checks (`PrototypeBuild.Validate`) pass on `88c170d4`,
including "native map harbor: 8 spawn floors, capsule settling".

Performance (shared Editor, High, 10 s, one idle bot; other agents shared the machine): the M1 yard up its main axis 1320
frames, mean 7.58 ms, p95 8.68 ms; the rebuilt pier with the art up the midway from the Gate 1246 and 1247 frames, mean 8.03
and 8.03 ms, p95 9.29 and 9.20 ms; the Low graphics tier 1376 frames, mean 7.27 ms, p95 9.46 ms. **The art is about .45 ms
over the M1 stage at High, not at or under it** (the views differ: the new one looks down the whole pier at the wheel, both
rides and the strings of bulbs).

## Not done / falls short

- The frame time at High is a little over the M1 stage's (above). The first lever is the Fine tier's reach (it draws to 38 m
  because the Mid tier shares its meshes).
- The coaster's pass was caught only from a stall's roof with the roof in the way (the train is seen in the lane from above);
  the gondola's fall itself (.7 s) was not caught in a frame, its telegraph and its hold were.
- Under the boards is dark (bulkhead lamps glow but all eight practical lights are on the deck).
- The far backdrop (the sea's facets, the seafront, the lighthouse, the barge) is still box-built; the sea reads as tiles from
  straight above. The mounts are simple (a horse and a sea horse); the stalls' prizes are small shapes.
- The box kit's bulbs do not chase (the art's do); the lamps cast no shadows (as on every stage).
- No bank-absent run of the final commit in the Editor (the box-kit capture is the first commit's); played only by an idle
  bot: no human playtest, no network guest, no Windows build, no killcam on the art.
- The owner's new song is not in: the old Container Terminal plays.
