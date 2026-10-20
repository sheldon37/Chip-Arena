# Central Station: the in-game visuals

Claude, 2026-10-16/17. Branch `claude/station-visuals`, based on shared `claude/sleepy-ritchie-g8ruhv` at `8a5de43` (both zone
designs and Astra's PR #254), then merged with shared `4164d5c` (Astra's live activation, PRs #255-#257), `6404332`
(#258-#260 and the Ironworks enemies; the station's map is unchanged there), `612f0da` (the City Ironworks visuals) and
`0b54ebe` (the station's enemies).
The design is `docs/EXPEDITION-CENTRAL-STATION.md` (sections 3-6 and 11 drive this work).

**Status: on the branch; it goes live when merged into shared.** The station is live since Astra's activation (map
`expedition-transit` in `Resources/UnityMaps.json`, area `exp-transit` in `Resources/ExpeditionLayout.json`, exp-layout-5).
This work draws on that live map and area. Every station hook is gated on the map id (`NativeExpeditionStation.Active`), and
the kit builds nothing from any other map's blocks, so other maps draw and sound as before. The set dressing, lights,
atmosphere and the departure board's idle rows are live. **The hazards' presentation is off in normal play**: they have no
authority yet, so normal play draws a calm station (`NativeStationLive.Calm`: no express, the third rail dark, the flood
still, the cabinets and the steam main idle) until Astra's replicated fields fill `NativeStationLive.View` and set
`NativeStationLive.Authority`. Only the DEV preview shows them. The one live data change is the station's own look entry in
`MapLooks.json` (section 4). No geometry, collision, spawn, machine, manifest, navigation, catalogue, rule, AI, authority,
director or network file changed.

| What | Where |
| --- | --- |
| The gate, the hazard timings, the board's text, the view, the state, the DEV preview, the cue timeline | `Core/NativeExpeditionStation.cs` |
| The dressing kit (parts, light sources, shafts, water, the express rig, the drives) | `Core/NativeExpeditionStationKit.cs`, `.Concourse.cs`, `.Shed.cs` |
| The practical-light picker | `Core/NativeExpeditionStationLights.cs` |
| The atmosphere and the hazards' telegraphs | `Core/NativeExpeditionStationFx.cs` |
| The runtime drawing (one hook line in `SourceWorld`; one prewarm line in `NativeStagePrewarm`, so the kit builds under the loading screen) | `Runtime/NativeExpeditionStationPresentation.cs`, `Runtime/NativeStationLifetime.cs` |
| The sounds on the doc's bank keys (one hook line in `NativeAudio`) | `Runtime/NativeExpeditionStationAudio.cs` |
| The DEV preview (Camera tab) | one action in `Core/NativeDevTools.cs`, one case and one toggle in `Runtime/NativeDevPanel.cs` |
| The look | the `expedition-transit` entry of `Resources/MapLooks.json` (this branch replaces the imported design look; section 4) |
| The managed checks | `Tests/ExpeditionStationVisualChecks.cs` (`CoreTests --station-visuals`; in the full run) |

## 1. What is built

**The dressing kit** (`NativeExpeditionStationKit.Build(blocks, area)`) lays parts on the live map's blocks by id stem, as the
town's kit does, and keeps the live area's records and machines clear. It is the doc's ranked gap 1.

- **The concourse.**
  - The broken glass vault: eleven arched ribs (12 chords each) springing over the screen and the clerestory, with the
    roof beams as ties, king posts and diagonals, purlins, and glazing. About a fifth of the panes are gone, with shards
    left hanging, and the panes over the Hulk drops are out. The moon falls through eight of the gaps as soft shafts, and a
    puddle lies where each lands.
  - The glazed screen to the shed: mullions, transoms, dark panes on both faces, some gone black.
  - The north clerestory with tall arched windows and the great 6:47 clock; brick lunettes at the vault's ends.
  - The CENTRAL sign in the facade's teal on a green fascia under the screen (a smaller one faces the platforms), with the
    6:47 clock hanging under it.
  - The departure board over the booth (10.4 x 4.4 x 4.4 m). North and south: DEPARTURES and five rows in columns (time,
    destination, platform, status), using the doc's final text. Under them, the ticker in three pages (SERVICE SUSPENDED /
    FOLLOW STAFF INSTRUCTIONS / DO NOT CROSS THE LINE). East and west: the 6:47 clock over PLATFORM 2 / EVAC SPECIAL /
    DOORS OPEN. Rows 0 and 4 and the title carry every live variant; one shows at a time, driven by the state.
  - The booth (glazed, lit, teal i signs), the burnt cafe (sooted front, scorched CAFE, embers, a smouldering fire), the
    newsagent (the fridge still lit, papers), a toppled vending machine.
  - The balconies: a cream fascia with a teal line, cast-iron balusters and handrails, globe lamps (one in three lit).
  - The field hospital under the west arcade (cots, a red-cross banner, a work lamp), luggage under the east.
  - The platform gates' posts and signs in the line colours (WAY OUT behind); terrazzo bands, a brass rosette, papers,
    glass grit round the fallen girder; bench backs and ends; luggage on the trolleys.
- **The ticket hall.** Troffers (a third dead, some stuttering) under a beamed ceiling; the green dado band and skirting on
  the cream tile; the tiled north wall with the lit WAY OUT and EVAC CANCELLED; the ticket windows (one lit) under TICKETS;
  the machines' blue screens (two dead); the turnstiles' red locked lamps, glass paddles and ticket slots; sandbags and
  police tape; the checkpoint's cots, crates and radio; the roundel in the floor; banded columns; the clock over the gate
  line; the route map.
- **The upper floor.** Office windows (one lit) and name plates, the PA console's red ON lamp, papers, lockers.
- **The shed.**
  - Trusses and skylight glazing. Missing panes let the moon through as shafts.
  - Sodium pendants over every platform (about half lit, a few flickering), each lit one with a light cone and a warm pool.
  - The platforms' white coping and yellow line, and red danger strips along every pit wall, some broken and some dim.
  - Hanging signs in the line colours (P1 red, P2 blue, P3 amber, P4 green), benches, the shuttered kiosk, and timetable
    screens on the footbridge piers.
- **The tracks.**
  - Running rails with polished heads, and sleepers (the low tier has one strip per track).
  - The live rail at each pit's centre on white insulators, under a yellow guard board, in the five sections of the design's
    hazard volumes. Each insulator glints blue while its section is live.
  - Hazard paint at the barrow crossings, puddles on the ballast, and cables on the pit walls.
- **The tunnel mouths.**
  - The dark beyond each mouth, a brick arch ring, and NO ENTRY over every mouth.
  - A two-aspect signal beside each mouth.
  - On the express line, four amber beacons and STAND BACK at P1's ends.
- **The stalled train.**
  - Cars 1 and 3: the red band, windows (a third lit and flickering), NOT IN SERVICE, roof pods and the cab ends.
  - Eight open doors, which are the train-door spawns.
  - Car 2, walked through: lit windows, moquette seats with backs, three ceiling tubes (one dead, one stuttering), ads,
    grab rails, a floor runner and the gangway doors.
- **The flood.** The derailed wagon with a tarp, rust and floating litter; the water's surface, which glows blue while it
  conducts.
- **The footbridges.** Cross girders under the decks, fascias, lattice on the shed side, handrails, and globe lamps.
- **The corridor and the pump room.**
  - Three pipes along the outer wall; the steam main is the lowest, burst at the hazard.
  - A cable tray, red strips every 6 m (stuttering), and wall boxes.
  - The pump room's water, the pumps' flanges, and the generator's amber lamp, panel and exhaust.
- **The signal box.**
  - Red-lit small-paned windows and CENTRAL SIGNAL BOX on the roof fascia.
  - Sixteen levers in the station's colours, each in its home and thrown poses.
  - The illuminated track diagram: the express route goes white through the warning; the live sections are red.
  - A red pendant lamp.
- **The entrances, from the area's spawns.** A staff doorway in its frame for each door spawn on a wall (a torn leaf, a
  STAFF ONLY plate), a drain grate on each floor vent, an aqua glow in the water at each sump, and a stone surround with
  timber braces on each bricked arch (a breach panel).
- **The express** (`kit.Express`, ranked gap 4): three stainless cars (21.5 m each) in the train's own frame. Each car has a
  lit window band (it strobes as the train passes), the red line, a skirt and underframe, bogies and gangways. The nose is
  tapered, with the yellow warning panel, a windscreen, two headlights and EXPRESS; the tail has its lights. It is posed by
  the state's nose and drawn only while it is in the station.

**The view and the state** (`NativeExpeditionStation.cs`). `NativeStationView` holds the section 8 fields:
- the express (`TrainAt`, `TrainLine`);
- the third rail (`RailLive` per section, `RailChangedAt`), and `FloodLive`;
- the levers (`Levers`, `LeversAt`, `LeversUntil`);
- `LastCallAt`, `Boss`/`BossAt`, the falling `Glass` events;
- the two cabinets' phases and the steam main's phase.

`NativeStationState.Evaluate(view, t)` derives what is drawn (a pure function):
- the express: idle, a 4 s warning (the headlight shows at once and grows), the 2.2 s pass (the nose's x), a 2.4 s wake,
  or halted under the levers;
- the signals: red at idle, green through the warning and pass, dark under the levers. The design doc disagrees with
  itself here: its hazard table says the T1 signals go red 4 s ahead, while its platform section and hero shot (and the
  look's tunnel-green practical) say green when the express is due. This follows the latter; the switch is the `Signal`
  line in `Evaluate` (the beacons, STAND BACK, the headlight and APPROACHING carry the danger either way);
- the danger lamps and beacons: on through the warning and the pass;
- the live sections (all cut by the levers), the flood's glow, and the levers' pose;
- the board: APPROACHING blinking through the warning and pass, SUSPENDED under the levers, LAST CALL blinking for 6 s after
  a last call, ALL SERVICES CANCELLED (and every status CANCELLED) once the boss arrives.

`NativeStationLive.View` is where Astra's replicated fields go; `NativeStationLive.Current` is what normal play draws (that
view once `Authority` is set, else the calm view). The presentation never writes either; it publishes the view it drew
(`Shown`) for the sounds.

**Lighting.** The look entry keeps the design's night: a cold moon key through the glass against warm sodium practicals,
with haze below 2.6 m. It raises the key and ambient a little for readability, names the station's kit in `dressing` and
`atmosphere`, moves the board's practical under the larger board, and dresses the screen as iron and glass, not the city's
curtain wall.

The picker (`NativeExpeditionStationLights`) gives the eight practical slots (four on the low tier) to the 76 candidates,
eased as the town's picker does:
- the look's eight;
- the kit's lamps, globes, troffers, board, CENTRAL, fire, train windows, signals, beacons, box, strips, generator,
  cabinets and flood;
- the express's moving headlight (a forward beam) and its window band's strobe, which always win a slot while they shine.

The beacons turn as rotating beams.

The same slots light the bodies (zombies, pilots, the station's kinds): the presentation turns actor lighting on each frame
(`NativeLighting.ActorLights`), as Ward Zero and the Ironworks do, and `ZombieLit`/`SourceLit` add the slots wrapped. Unity's
own lights cannot carry a body here: the key is shadowed under the roofs and the glazing and the ambient is about .001, so
before 2026-10-20 the zombies drew near-black beside lit floors. `Tests/ZombieLightChecks.cs` (`--zombie-light`) emulates it.

**Atmosphere** (`NativeExpeditionStationFx`; ranked gaps 2, 3 and 5). This adds two draws.
- **Moonlight and lamps.**
  - Moon shafts: chains of overlapping radial sprites, soft across and along their length; the design asked for a
    light-shaft quad.
  - Dust motes drifting in the shafts, and a soft pool where each shaft lands.
  - Sodium cones and warm pools under the lit pendants.
  - Halos round the lights; the signals, beacons and cabinets are drawn as telegraphs.
- **Water.**
  - On the flood and on every puddle, each light's reflection is streaked toward the eye: dashes that shimmer on the
    flood, still on a puddle. This stands in for the water look's sheen.
  - Puddles darken the floor softly.
  - Ripples open across the flood, and the sumps breathe aqua.
  - Low mist lies over the water, and drips fall from the open roof and ring where they land.
- **Fire and steam.** The cafe's flames, embers and smoke; the steam main's wisps.

**Hazard presentation** (DEV preview only until the authority exists; telegraphs first; the quality setting and the low tier
never thin them):
- **The express.**
  - The warning: the headlight grows in the west mouth with a flare and a beam reaching down the pit. Glints race along the
    rails toward the platforms (the rails singing). The signals turn green, the beacons sweep amber, STAND BACK's border and
    the danger lamps flash, the diagram's route goes white, and the board blinks APPROACHING.
  - The pass: the train goes nose to tail in 2.2 s, its window band strobing and the headlight beam on the platforms. Sparks
    spray off the wheels, and a dust wall boils up behind the tail and settles.
- **The third rail.** Blue glints on its insulators and crackles of sparks while a section is live. A cut section goes dark.
- **The flood.** While it conducts, the water lights blue and arcs crawl across it.
- **The spark cabinets.** The fizz: the door gaps flicker and spit sparks. The arc: a jagged 2 m bolt out to the platform,
  a burst and a flash.
- **The steam main.** The hiss is a few jets; the scald is a plume billowing out of the wall.
- **Falling glass.** Through the 1.2 s telegraph, a shadow grows on the floor and a glint drops from the vault. Then the
  shards burst out, slide to rest and glint.

**Sounds** (`NativeExpeditionStationAudio`, on the doc's section 10 keys).
- The express: `station.train.warn` (the bell), `.approach` (the rails singing), `.pass`, `.recede`.
- The PA, in 2D: `station.pa.chime`, `.standback`, `.power`, `.restored`, `.doors`, `.lastcall`.
- The levers and the rail: `station.signal.lever`, `station.rail.cut`.
- The glass: `station.glass.creak`, `.fall`.
- The hazards: `station.spark.fizz`, `.arc`, `station.steam.burst`.
- The board: `station.board.flip`.

Until the owner records them, each plays today's nearest stand-in clip, and the PA's voice lines stay silent. The loops
(`station.rail.hum`, the ambiences) are not wired yet.

**The DEV preview** (DEV panel · Camera · "Station hazards preview") fills a synthetic view on a 96 s cycle:
- the express warns and passes three times;
- a last call at 14 s;
- the flood conducts from 36 to 50 s;
- the levers cut everything from 60 to 80 s;
- the boss state and falling glass from 88 s;
- the cabinets spark and the steam main bursts on their own rhythms.

It is presentation only: no damage, collider, input or network.

## 2. Placement rules (checked, recomputed from the records)

- **Hugging.** On each level, below 3.1 m, nothing stands out more than 0.25 m from a block, except flat paint (6 cm or
  less) and parts that stand over a solid block (a lamp on a railing).
- **Clutter.** A free-standing part on a walkable top below 2.2 m is at most 0.65 m tall, unless it stands against a wall or
  furniture, or on furniture. Players are never shown an obstacle that has no collider. (The sodium lamps hang from the
  trusses; there are no posts on the platforms.)
- **Records.** Nothing meets:
  - a spawn's exit or path, a vent, or a drop column;
  - an entry slot, the approach, an interaction or boss point, or the exit door's volumes.

  Track parts no taller than a sleeper, the flood's surface and flat paint may run under a spawn's path, because the
  zombies walk the rails out of the tunnels. A spawn's own doorway, grate or carriage door is its own.

## 3. Performance (the doc's section 11)

Kit parts per room, full tier, on the live map with the live area (its entrances add dressing; its records and machines keep
parts off); the light candidates are those whose reach can take a slot:

| Room | Parts | Budget | Casters | Candidates | Shafts and cones |
| --- | --- | --- | --- | --- | --- |
| Ticket hall | 428 | 900 | 0 | 10 | 0 |
| Concourse | 3,575 | 3,800 | 192 | 6 + the look's | 6 |
| Shed | 2,063 | 3,400 | 45 | 35 | 27 |
| Corridor | 75 | 700 | 0 | 7 | 0 |
| Signal box | 180 | 350 | 0 | 1 | 0 |
| Upper floor | 422 | 600 | 0 | 9 | 2 |
| **Total** | **6,743** (low tier 4,625) | design 7,850 | 237 | 76 with the look's 8 | |

- **The board.** About 2,230 of the concourse's parts are the board's glyphs (1,024 static, 754 in the live variants, 434
  in the ticker). The design's estimate of 2,600 for the concourse left the text out, so the concourse's budget takes part
  of the shed's share. The total stays under the design's 7,850.
- **Draws.**
  - The kit takes 14 instanced draws of 500 (1 casting), the express 1 while it passes, and the atmosphere 2: 17-18,
    within 22 (the design's 18 is met at rest).
  - Each draw is one instanced call per cascade for the casters.
  - The checks report the exact count.
- **Lights.** At cameras in every room, the picker lights eight slots, and four on the low tier.
- **Atmosphere quads.** 231-711 at rest at a camera in each room, and 484-979 with every hazard on at once, within the
  2,600 capacity. The low tier draws about a third at rest.
- **The low tier and compatibility rendering.**
  - Detail is out: balusters, lattice, glazing bars, sleepers (one strip a track), the ticker, NO ENTRY on the T2 and T3
    mouths, small text, rust, cables.
  - Four light slots.
  - Moon shafts halved and shortened; no motes, mist, reflections or lamp cones.
  - The telegraphs are untouched.
- **Allocation.** None per frame: the state, the drive over every part, the picker, the atmosphere and the cues are
  checked over 240 frames.

## 4. Live: what this branch changes and what Astra must supply

**The activation snapshot against the design draft** (`Tests/expedition-central-station-activation.json`, the live map, against
`Tests/expedition-central-station-draft.json`, which this work was first built on):
- **Geometry:** identical (458 blocks and 18 stairs, every id, place, size and material). The kit needed no change for it.
- **Machines:** new in the live area (chip-0 on the concourse's west side, med-0 on the island platform by the T1 edge,
  recruit-0 on the concourse's east side, chip-1 upstairs). The kit now keeps every part off a machine, the spot its pilot
  stands on, its dispenser and its chip drop; only floor paint may run under them. Nothing else had to move.
- **The look:** the activation imported the design's look. This branch replaces that one entry (below).
- **The map's tag:** live reads "The last train left at 6:47."; the design (and the board, whose 6:47 EVAC SPECIAL is the
  train stalled on platform 2 with its doors open) says "The 6:47 never left." Left as is (it is Astra's map data); worth
  aligning.

**The look entry** (`Resources/MapLooks.json`, `expedition-transit`; nothing else in the file changed; its receipt in
`tools/asset-lock.json` updated as `tools/generate_expedition.py` writes it, and `generate_expedition.py --check` passes).
Against the imported design look:
- the moon key 0.42 to 0.5, the ambient sky and equator scales 0.075/0.06 to 0.085/0.065 (readability);
- the departures practical down under the larger board (y 8 to 6, range 18 to 16), the signal box's into the box (36, 8.6,
  -54.6), the cafe fire onto the hearth (-17, 0.9, 11.4);
- the screen block as dark iron (`steel-green`) instead of the city's curtain-wall glass (its glazing, mullions and broken
  panes are kit);
- a new exact entry for the signal box's lever frame (`expedition-transit-signal-levers`) as dark painted iron
  (`cyber-metal`): the rusted `steel-green` read as camouflage on its large faces;
- `dressing` and `atmosphere` name the station's kit (fields nothing reads yet).

**What Astra must supply** (doc section 8). Fill `NativeStationLive.View` every frame on every peer, from the replicated
fields, in arena seconds (the clock `arena.Time` runs on), and set `NativeStationLive.Authority = true` once they exist:
- the express: `TrainAt`, `TrainLine`;
- the third rail: `RailLive`, using bits in the order `rail-t1-w`, `rail-t1-e`, `rail-t2`, `rail-t3-w`, `rail-t3-e`
  (`NativeExpeditionStation.RailIds`), and `RailChangedAt`;
- `FloodLive`;
- the levers: `Levers`, `LeversAt`, `LeversUntil`;
- `LastCallAt` (a Conductor's last call);
- `Boss`, `BossAt` (the board's ALL SERVICES CANCELLED; the placeholder Siren boss does not set it today);
- the falling `Glass` events (where and when each lands);
- the cabinets' and the steam main's `NativeHazardState` (the Ward grid's and gas's timing).

The presentation never writes any of it back. Until `Authority` is set, normal play draws `NativeStationLive.Calm`.

## 5. Checks and renders

**`Tests/ExpeditionStationVisualChecks.cs`** checks:
- **Other maps untouched.** The gate is the map id alone. The kit builds nothing for any other map (UnityMaps and
  GameContent). The runtime, audio and DEV hooks are gated, and the presentation never writes the live view.
- **No hazard in normal play.** Until the authority's fields exist, normal play draws the calm view; over a minute it draws
  no express, live rail, flood, cabinet, steam burst or glass: no telegraph quad, no spark, arc or dust, no beacon or
  express light, and no hazard cue.
- **The live look.** It validates, gives every station block a listed look, and names the kit.
- **The kit from the live map and area** (its records and its machines):
  - it is deterministic and does not depend on the blocks' order;
  - every part is valid;
  - the set pieces are present (the vault, every board variant on its faces, the ticker's pages, the signals, beacons, rail
    sections, levers in both poses, cabinets, flood, train doors, the express).
- **The placement rules** in section 2.
- **The budgets:** room parts, draws, casters, candidates, slots at cameras in every room, quads with every hazard on, and
  the low tier thinning the atmosphere but not the telegraphs.
- **The state:**
  - idle, warning, pass and wake;
  - the levers halting the express and cutting the rails and the flood, and power back after 60 s;
  - LAST CALL blinking, then clearing; the boss;
  - one variant per row and the ticker one page at a time;
  - the express drawn only while it passes;
  - the signals' aspects and the cut rail's glints;
  - the cue timeline, the preview's cycle;
  - no allocation per frame.

Renders are offline, through the real `NativeBlockLit` and `NativeNightSky` shaders, the live map, area and look entry. They
use the real Core kit, picker and atmosphere, the machines' Core models and today's horde and pilot rigs, through
`scratchpad/svh/dump`, and are not in the repository; the list is in the DEV_LOG entry. The beauty shots are the calm view
normal play draws; the hazard shots are the DEV preview's (or the authority's, later) states.

**Not done, and what still falls short:**
- **The water look.** It is not a shader: the flood is a dark emissive plate with reflection streaks, ripples and mist, so
  there is no true sheen or normal ripple (ranked gap 3).
- **The light-shaft quad and the wet coat.** Neither is a shader mode (ranked gaps 2 and 7); shafts are sprite chains and
  wet floors are darkening.
- **No station light field.** The many lamps share eight slots, plus pools and halos.
- **Not wired.** The loop sounds (the rail's hum, the ambiences) and the PA's voice (the owner records it).
- **Seen in the renders.** The live flood reads as a flat bright blue sheet (readable, not beautiful); falling glass reads
  only up close; the board half hides CENTRAL from the ticket hall; the island platforms are plain, and the train's exterior
  reads tan under sodium. Against the design concept, the vault's arches read weaker.
- **Not run.** The Unity Editor, a Windows player, and play: never seen in the engine.
