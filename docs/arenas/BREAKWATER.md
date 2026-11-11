# Breakwater (`breakwater`), rebuilt

2026-11-11 · Claude · branch `claude/rework-breakwater` (base `claude/sleepy-ritchie-g8ruhv` at `f81e663e`) · one of the five M1
stages rebuilt from scratch (owner, 2026-11-11: "I want to revamp ALL the current stages out of the blocky style ... Maybe even
COMPLETELY rework the following stages: Polaris Station, Deepwell, Skyframe, Pier 9, and Breakwater. These all look kinda thrown
together and all factory like. Maybe give them all more of a unique feel, very good designs, and nice animations"; the theme
approved the same day: "Yes, start them").

Status: **implemented on the branch; not pushed, not merged, not released.** The stage bank is local (git-ignored).

**Pitch.** A storm-battered granite breakwater at blue hour, the lighthouse at its head. The lamp turns and its two beams sweep
the stone, the sea and the pilots; swells run in from the north and break on the wave wall in bursts of spray; a bell buoy rocks
out in the dark, lanterns swing on their brackets, gulls wheel over the lantern; across the harbour the town's windows are lit.
Both long edges are open: the sea kills. It is still the small duel: 44 x 17.9 m of mole, three lanes at three heights.

Original design: nothing here takes a name, layout, look or element from any other game. The M1 stage it replaces (a flat pier
head dressed with the Toon Harbor containers) is gone but for its id, its name and its role.

## Layout (docs/arenas/scripts/breakwater_m2.py)

x east, z north, y up; mirror symmetric x → −x; team A west (the harbour gate), team B east (the lighthouse's hall). 152 blocks
(16 breakable), 8 FFA spawns and two team tables of 4; bounds x ±22, z −13.5..12.4 (4 m of sea past both long edges), ceiling 16,
killY −5 (was −6 on a flat map: the sea's surface lies at −3.4, a metre under the quay, so a pilot goes under before the kill;
about half a second from the quay's edge, three quarters from the deck's). `NativeTrainingRules.RoomFits` still keeps it to four
pilots (1v1, 2v2 or a four-pilot FFA); it is in the rotation and in Random for those rooms only.

| Level | y | What |
|---|---|---|
| The harbour quay | −2.4 | Along the south side (z −9.5..−5), in two halves joined by a 1.5 m ledge round the bastion. Open to the harbour: a fall. A flight up to the deck from a landing at each end. |
| The deck | 0 | The mole's top, 10 m wide. The **Mariners' Stone** at its centre (full cover, the duel's pivot), quarried granite blocks (a full stack and a half course each side: the lee of the wash), an upturned boat (half), bollards, lamp posts, lobster pots and barrels (breakable). The **bastion** bulges out over the quay at the centre. Its south edge drops to the quay. |
| The wave wall | 2.4 | Along the north side (z 5..8.4): the walk on top, the storm parapet on its seaward edge (1.1 m, breached where the wash comes over: a fall into the sea). The **watch turret** parts it at the centre; the **pulpit**, the raised landing in front of the turret, joins the two inner flights and is the power position over the deck. A flight at each end too. |

- **Routes:** the deck (fast, the Stone to dance round), the walk (high, exposed to the sea and the wash, a fall on one side), the
  quay (low, hidden from the deck by its own edge, a fall on one side, no way up but the end flights). Six flights (12 risers of
  0.2 m each, 1.6 m wide).
- **The ends:** each a hall of two rooms (the team spawns), 2 m doors set off the spawns' lines, a roof at 5 m, and wings across
  the walk and the quay. Nobody leaves the map's ends: these are the only walls with a pilot column for everyone.
- **Spawns:** team A in the gate's two rooms, team B in the hall's. FFA: the four rooms, both ends of the quay, both ends of the
  walk. The draft checks every FFA and A–B pair blocked under ±1 m jitter at three eye heights (3D), 3 m from the wash on each
  spawn's floor, every spawn and plate reachable on foot, no cover in the 1.3–2.2 m band, the stairs.
- **Nav** (generator): 556 nodes, 794 links (380 gated by the wash); all three levels and every spawn on one graph (the rest are
  the tops of cover).
- **Pilot columns** (the owner: no invisible walls): 12 in the record. For everyone, 8: the six on the end buildings (each hall's
  roof and its two wings: they keep pilots inside the map, the mole runs on past both ends as scenery), the turret's and the
  Stone's (the belfry and roof, and the obelisk, stand on those blocks). For bots only, 4: the storm parapet's lengths, at the
  lethal drop. Nothing else: every other top can be stood on by whoever reaches it.

## Hazard (an existing kind; timings from the draft)

| Hazard | Kind | Where | Timing | Effect |
|---|---|---|---|---|
| **The wash** `breakwater-wash-{w,e}` | `vent` + push (the Causeway's "wind" mode) | A 6 m wide band across the whole mole on each half, x 7..13 and its mirror, from the quay's floor to 2.2 m over the walk: where the parapet is breached | Period 22 s each, 11 s apart (they alternate); 3 s telegraph, 2.2 s wash. First telegraph at FightAt + 20.8 s (west), + 31.8 s (east). Nothing is live in the fight's first 4 s (the arm delay) | 1 HP per 0.25 s tick (about 9 HP a wash) and a push of 13 m/s² south: off the walk onto the deck, off the deck onto the quay, off the quay into the harbour. Walking into it holds you; so does the lee of the granite blocks. |

No rules change: the push is `NativeArena.VentPush` (`pushMode` "wind"), which the Causeway added on 2026-11-10 and Astra's vent
step already runs for any map. The only pin touched is CausewayChecks' "no other map's vent is a wind", which now excepts
Breakwater. For Astra to confirm or tune: the strength (13 m/s², a little over the Causeway's 12, because here the first fall is
only onto the next floor), and that bots treat the band as gated through the telegraph and the wash (the vent carries damage, so
the nav gate applies).

## The look (`breakwater-blue-hour`)

- **Light:** the sun four degrees under the western horizon: a cool lavender key from the afterglow (bearing 262°, elevation 16°),
  a warm rim from the east (the lamp's side), a deep blue trilight; linear fog 40–1100 m; the dusk sky with moving storm cloud;
  the anime style on (saturation 1.18, contrast 1.1, blue shadows, warm lights); the advanced lighting (light volume, scatter,
  beams) with **rain** (the M1 stage had drizzle); far plane 1500 m.
- **The practical lights are picked each frame** (`NativeLighting.PickedLights`, as the Sanctum does): slot 0 is the lighthouse's
  lamp as a **two-beam light** turning with the optic (NativeWardLight.cginc's WardBeam), so its beams really sweep the stone,
  the sea, the art and the pilots, each crossing the deck about every six seconds; the other seven are the lights nearest the
  eye: the eight lanterns, each where it hangs this frame on its swinging bracket, the lamp on the back wall of each hall's two
  rooms and the Mariners' Stone's lamp (a light eases out as the next one out takes its slot).
- **Animated set pieces (all match long, from the field clock, so every client and the killcam show the same sea):** the optic
  turning in the open lantern and its two volumetric beams; five swells running in from 56 m out, steepening as they shoal and
  breaking on the wave wall one every 2.6 s, each break throwing spray up the wall and over the walk (lit as the beam passes);
  eight lanterns swinging in the wind (wildly while a wash breaks over their part of the mole); the turret's storm bell (it
  tolls through a wash's telegraph and rings down after); the bell buoy riding the swell north-west with its red flash; a
  fishing boat and a dinghy riding at their moorings in the harbour; nine gulls (three round the lantern, six in wide ovals over
  the mole, flapping in bursts and gliding); the gate's flag and the turret's pennant streaming south; foam at the wall's foot,
  glints toward the afterglow, spindrift across the mole, the town's lights and ships on the horizon.
- **The wash's look (never reduced by the quality or the low tier):** through the telegraph the band's edges burn cold on all
  three floors, chevrons run south (the way it will carry you), light pillars stand at its corners, the bell tolls, the lanterns
  begin to swing, and **the comber** gathers 26 m out and runs in on the breach, reaching the wall as the warning ends; then
  the water comes over: a sheet running across the walk, down the wall, across the deck, down onto the quay and off it, with its
  foaming front, streaks, falls and mist; after it the wet sheen and the drips.
- **Audio** (existing clips with `breakwater.*` bank keys the owner's bank may fill): `breakwater.wash.warn`,
  `breakwater.wash.break`, `breakwater.break` (each swell, where it breaks), `breakwater.buoy` (the bell, every 5.3 s).
- **Low tier:** the beams fall back to billboards; atmosphere counts drop to a third; telegraphs are unchanged; the art draws its
  Low tier.

## Stage art

With the stage bank `ChipArenaStageBreakwater` (git-ignored, from the builder's Drive zip) nothing of the stage is a box:
`NativeStageArtDraw` draws the mole modelled in Blender in place of every block's box, the box kit, the box rig and the box
backdrop. Without the bank everything draws from the box kit, rig and backdrop below. The recipe is docs/arenas/STAGE-ART.md.

**Modelled** (`tools/stages/breakwater_*.py`, on stagekit):
- *The mole:* granite setts with a runner of larger slabs and puddles that hold the lamps' light; coping stones laid one by one
  along every edge (a few knocked out); the wave wall's **recurved sea face** from its toe under the sea to the lip under the
  walk, weed below the tide line, armour stones heaped at its foot; its deck face with a plinth, buttress piers, blind arches
  and mooring rings; the storm parapet under a saddleback coping, its breached ends broken in toothed courses with rubble; the
  quay with timber fender piles, iron straps, ladders and rings; the bastion with a brass compass rose let into its setts.
- *The pulpit and the watch turret:* three blind arches and the harbour board's bronze; the turret's quoins, string courses, lit
  slits and boarded door, the corbel table, the open belfry (four piers, round heads, the bell's beam), the bell-cast slate cap,
  its finial and vane; gulls sitting out the weather on the roofs (only where nobody can stand).
- *The Mariners' Stone:* a stepped plinth, a quoined die with bronze plaques (a ship's wheel in relief), an anchor gone to rust
  leaning on it, a cornice; over it the obelisk and its lamp.
- *The halls:* the harbour gate in granite ashlar under a slate roof with a lit round window in its gable, a chimney and the
  flagstaff; the lighthouse's hall lime-washed with a red band, granite quoins and a crenellated parapet; both with stone door
  surrounds and keystones, storm lamps over the doors, boarded ceilings on beams, benches and lamps inside; the gates in their
  back walls; the wings' barred sally ports and store doors.
- *Furniture:* quarried granite in rough-hewn courses with drill marks, skids and a chain; the upturned boats on trestles;
  lathed cast-iron bollards with mooring ropes over the edge and coils beside them, lamp posts with swan-neck brackets; stairs
  with bullnosed treads; lifebuoys on the wall and the halls, a net hung to dry on the gate.
- *Breakables* (block models): lobster pots (wicker hoops and netting), barrels (staves and hoops), fish crates (slats, the
  catch on ice).
- *The rig* (15 slots, `Core/NativeBreakwaterArt.cs`): the optic (bull's-eye lenses in a brass cage), a lantern, the bell, a
  swell, the comber, the wash and its fall, the buoy and its lamp, the fishing boat, the dinghy, a gull's body and two wings, a
  flag's length.
- *Far* (named chunks, one tier): the sea in faceted swell out to the horizon; the lighthouse on its rock (a tapering lime-washed
  tower with two red bands, lit windows, a corbelled gallery and iron rail, an open lantern under a copper dome); the mole
  running west to the shore with a lamp every 17 m, the far pier head and its green light, the town climbing its hill, a
  church, moored boats, the headlands.

**How it moves.** `NativeBreakwaterRig` tags each box part it builds with the art's slot (`Slot`, `Level`); `NativeBreakwaterArt.Pose`
places each tagged part's model at that part's own centre and turn (the water's models are unit boxes scaled by the part's half
extents), so the art moves exactly as the box rig does.

**Materials** (`ChipArena/StageLit`, unchanged): flagstone joints on floors and walls (the calm fine surface), plain dressed trim,
wet smoothness on the setts and the quay so the lamp's beam and the lanterns catch, metals at half metal or less, baked dirt,
tide-line weed (the bake's moss, by a zone function) and eased occlusion (an open-air stage), emission above 1 on lamps and
windows for the bloom.

Rebuild (from `unity/ChipArenaConversion`; about 12 s):
```
dotnet run --project Tests/CoreTests.csproj -c Release -- --stage-layout breakwater LAYOUT.json
blender --background --factory-startup --python tools/stages/breakwater_art.py -- --layout LAYOUT.json --out Assets/LocalAssets
```
(`--preview DIR [--views a,b] [--preview-tier N]` renders the baked stage and the rig's props in Blender; `--tiers`, `--no-ao`,
`--no-far`, `--static-only`, `--stats` for quick looks and the coplanar stacks.)

## The box kit (without the bank)

`Core/NativeBreakwaterKit.cs`: 594 NativeBlockLit parts on the static blocks (copings, courses, buttresses, nosings, door frames
and lamps, the turret's belfry and stepped slate cap, the obelisk, the gate's stepped roof and flagstaff, the hall's merlons,
the posts' brackets), every attached part inside its block grown by 0.25 m, the 58 overhead ones on columned blocks or 2 m over
the floor, and no two coplanar faces laid on each other (checked). `NativeBreakwaterRig`: up to 84 parts a frame.
`Core/NativeBreakwaterScenery.cs`: the box backdrop (the faceted sea, armour stones, the lighthouse, the mole to the shore, the
town). The block looks in `MapLooks.json` give the blocks their stone.

## Files

- Draft: `docs/arenas/scripts/breakwater_m2.py` (layout, hazard, checks, plan PNG); `breakwater_m1.py` kept as history.
- Core: `NativeBreakwaterLooks.cs` (ids, kit kinds, places, the clock's motion, the wash's poses, audio cues),
  `NativeBreakwaterKit.cs` (kit and rig), `NativeBreakwaterFx.cs` (effects and the light slots), `NativeBreakwaterScenery.cs`,
  `NativeBreakwaterArt.cs`. Runtime: `NativeBreakwaterPresentation.cs` (draw component, SourceWorld and prewarm hooks, audio).
- Art: `tools/stages/breakwater_art.py`, `breakwater_kit.py`, `breakwater_props.py`, `breakwater_far.py`.
- Checks: `Tests/BreakwaterChecks.cs` (`--breakwater`), `Tests/BreakwaterArtChecks.cs` (`--breakwater-art`); both in the full run.
- Shared files touched (one small edit each): see "Shared edits" below.

## Shared edits (for the integrator)

- `Core/NativeNewStageLooks.cs`: `breakwater` removed from `Maps` and its look from `Looks`; `Index` now returns a fixed stage
  number from a private `Stages` list (0 Breakwater's .. 4 Polaris), so the other four M1 stages keep the numbers their kit,
  rig, scenery and effects switch on whichever ids leave `Maps`. Breakwater's code in the NativeNewStage* files is now unused
  (left in place: the others' agents are editing those files).
- `tools/generate_stage_hazards.py`: `draft()` reads `<name>_m2.py` when it exists (else `_m1.py`); `EDGES['breakwater']`,
  `OVERHANG['breakwater']` (so the map goes through `open_stage`).
- `MapLooks.json`: Breakwater's own line rewritten. `PackDressing.json`: Breakwater's Toon Harbor entry removed.
- `UnityMaps.json`, `tools/asset-lock.json`: regenerated (only Breakwater's record differs).
- `Core/NativeStageArt.cs` (`Banks`), `Runtime/NativeSourcePresentation.cs`, `Runtime/NativeStagePrewarm.cs`,
  `Runtime/NativeAudio.cs`: one line each.
- Tests: `Program.cs` (two flags, two lines in the full run), `StageArtLayout.cs` (one line), `NewStageLookChecks.cs`
  (Breakwater's rows dropped; the sink probe uses Pier 9), `NewStagesChecks.cs` (Breakwater out of the M1 list),
  `StageKitChecks.cs` (its expected count dropped), `CausewayChecks.cs` (the wind pin excepts Breakwater),
  `TransitionLoadingChecks.cs` (the prewarm entry).

## Music

The owner's **Last Light Duel** (`breakwater-arena-duel`) stays the stage's track (NativeStageMusic.Theme; the map record's
`music` keeps the generator's placeholder as the fallback). If the owner wants a new song for the rebuilt stage it takes that
place under the key `breakwater-theme`; a Suno prompt is in docs/arenas/STAGE-MUSIC-PROMPTS.md ("Breakwater — Storm Light").

## The bank (2026-11-11)

`ChipArenaStageBreakwater.bytes`: 6,178,491 bytes (budget: about 20 MB), 302 props. Static: 18 map chunks of 22 m in three tiers
(the mole is seen end to end, so fewer, larger draws) and 3 far chunks (the sea, the lighthouse, the shore) in one; Fine 57,937
triangles (52,676 vertices), Mid 28,668 (30,951), Low with the far chunks 23,905 (27,735). Rig: 15 props; 6,902 / 3,950 /
1,618 triangles by tier; up to 58 rig draws a frame. Breakables: 3 meshes for 16 blocks; 1,628 / 1,118 / 238.

Z-fighting (`zfight.py BANK UnityMaps.json breakwater --tier N`): 0.03 m² in 8 pairs at Fine, 0.02 m² in 8 at Mid, 0 at Low
(the bar is 0.1 m²). `stagekit.lift_coplanar` runs on every tier's static scene and on the far chunks; the deepest stack is two
layers (the box kit lays none: BreakwaterChecks).

## Checks

- `--breakwater` (Tests/BreakwaterChecks.cs, 4304 checks): the record validates, in the catalogue, in Random for a duel and 2v2
  only; mirror symmetry; the open edges, the bounds 4 m past them and the kill plane under the sea; every pilot column has its
  reason (8 for everyone, 4 for bots); no cover in the 1.3–2.2 m band; the nav graph reaches every spawn and all three levels;
  the wash (an existing vent with the wind push, parked through the arm delay, a 3 s telegraph, the whole mole's width, mirrored
  and alternating, 3 m from every spawn); the comber and the front; each cue once however the frames cut the time, a swell's
  break cue on the moment its spray starts; the look (styled, advanced, rain, beams); the box backdrop outside the play space;
  the box kit's collider rule, its overhead parts and no coplanar faces; the rig (deterministic, inside its buffer, every part
  in the bounds under the quay's floor or 2 m overhead, the lanterns hung from their brackets); telegraphs never reduced; the
  light slots (slot 0 the lamp as two beams at the optic's heading, the rest lanterns and fixed lamps where they are); both
  beams cross the deck each turn; gulls over the ceiling; allocation free per frame; the wiring; the map out of the M1
  presentation and the other four keeping their stage numbers.
- `--breakwater-art` (Tests/BreakwaterArtChecks.cs): the slots; at every 0.05 s of two rounds every draw is its tagged part's
  (place, turn, size, light); the counts (a lantern a post, nine gulls and their wings, two flags, a comber for every wash
  gathering or running, water on the mole only while one runs); deterministic, allocation free; the layout export; the wiring
  and the bank-absent fallback; and, where the bank is on the machine: every block covered, every breakable and rig slot in
  three tiers, the chunks, the budget, the probe over the deck clear of every block, the play-space rule (within 2 m over a
  floor the art keeps within 0.25 m of a block: 0 of 26,780 vertices over, the farthest 0.24 m; what stands on a columned
  block is no cover) and the spawns.
- The full CoreTests suite: 1002 PASS lines, exit 0 (twice; the second on `055be42c`, after which only the art scripts, the
  look's rain amount, one threshold in BreakwaterArtChecks and this doc changed; `--breakwater` and `--breakwater-art` rerun
  after them). Unity compile (`check_unity_compile.py`): Assembly-CSharp and Assembly-CSharp-Editor pass.
- The Editor's build checks (`PrototypeBuild.Validate`, wtvalidate.sh): ok on `3e343600`, including "native map breakwater:
  isolated Edit-mode physics, 8 spawn floors, capsule settling".

## Seen in the Editor (2026-11-11)

Captures in the shared Editor (a match, one bot, the game's own camera placed by `maincam`): the M1 stage before (`db641c9c`)
and the rebuilt one, the same four views (wide, the deck from each end, the lighthouse); eight views of the rebuilt stage (wide,
the Stone with the pulpit and the turret, the walk with the bell in its belfry, the quay and the fishing boat, the sea side with
a swell and spray on the wall, the lamp and its beams, the deck from each end); the wash at every half second from arena time
26 to 32 (the telegraph's edges, chevrons and pillars, the edges going white as it breaks, the water across the deck and down
the wall); the Low graphics tier (wide and deck).

Frame time (High tier, the deck from the west gate, 10 s, one bot, runs taken back to back, other agents sharing the machine):
the M1 stage 7.59 ms mean / 9.07 p95 and 9.23 / 11.58; the rebuilt stage 7.60 / 8.92 and 7.68 / 8.85. Level with the M1
stage's better run, under its worse. (An earlier run, with 26 chunks of 12 m and heavier rain, took 9.14 / 11.74 against the
M1's 7.59 / 9.23: the chunks went to 22 m and the rain from 0.3 to 0.22.) Low graphics tier: 7.08 / 9.64.

## Not done / not seen

- The wash's water was seen with its first model (a chequer of foam and water); the model was then changed to streaks of foam
  along its run and seen only in Blender. The quarried blocks' last form (broad hewn planes) was seen in the wash captures;
  the eight-view sheet shows an earlier, rounder one, and a sea still in two tones (now one smooth tone, seen in the wide and
  wash captures).
- The comber running in on the breach was not caught in a frame (the wash camera looks along the deck); the east wash, the
  lanterns' wild swing and the bell's toll in motion likewise (checked by state in BreakwaterChecks).
- The box kit, box rig and box backdrop (the bank-absent fallback) were not run in the Editor: checked in CoreTests only.
- Played only by a bot: no human playtest of the wash's push (13 m/s²), no network (guest) test, no killcam on the art, no
  native Windows build.
- No shadows from the practical lights (as STAGE-ART.md says for every stage); the halls' rooms are lit by their one lamp each.
- Breakwater's unused code in the M1 files (Core NativeNewStageKit/Rig/Scenery/Fx) is left in place.
- Hazard sounds are the existing clips; no ambience loop (`breakwater-storm` is a key only).
