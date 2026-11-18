# Expedition overhaul: the master plan (2026-11-18, final)

**Status:** approved by the owner on 2026-11-18 with every recommended default ("Yes, use defaults"); see section 3. Release 1 (the drop) is in progress. A feasibility, completeness and owner-intent review was applied to the first draft. Section 9 lists what the review changed.

**The owner's short version:** the appendix at the end of this file.


**Inputs:**
- the owner's request (R1-R12, including the follow-up);
- the current-state map and its capture stills;
- the code at shared `442bcf69`, read-only;
- three independent concepts;
- one review of the first draft (39 findings, all accepted).

**Paths:** `Core/`, `Runtime/`, `Editor/` and `Resources/` are under `unity/ChipArenaConversion/Assets/ChipArena/`. `Tests/` and `tools/` are under `unity/ChipArenaConversion/`. Line numbers are at `442bcf69`.

**Owners:**
- **Claude** builds presentation: art, models, lighting, animation, VFX, UI and sound.
- **Astra** builds rules, generation, collision, spawns and network, from the paste-ready hand-off in section 8, also kept on its own as `docs/EXPEDITION-OVERHAUL-ASTRA.md`.
- Shared Core contract files name an owner for each field.

**At a glance:**
- **Release order follows the owner's words.** The owner named the pods "First" and asked for the tileset machine "now", so release 1 is the drop: the ATLAS board, the animated eject and pods that crash all round the camp. Night comes second.
- **Seven test-channel releases,** each playable (section 5).
- **Six owner decisions** (section 3): the city's size, the time before control, the light packs, enemies near instances, how far survivors interact, and cache rarity.

---

## 0. How this plan was chosen

Three concepts were written independently and compared on owner intent, buildability, risk and staging.

- **MERIDIAN AFTER 6:47** gives the strongest answer to the follow-up (R11, R12): a hill city in layers where the land itself bleeds into each instance (a dead-flat plain near Level 0, a flooded basin near the Poolrooms, a rail cut at the station); four bands of bleed; roofs that act as a second street; an honest map edge; a full hub plan. **It is the spine.**
- **BEACON** has the best journey and the best groundwork: latent fixes first; the pod's cabin as the loading screen; the camp's searchlight locking onto your pod; a capsule waiting at the ramp foot, so pods can be solid; rescued survivors; tier liveries; one tell per instance.
- **LIGHTS OUT** has the most inventive lore and the best shared contract: walls missing a rectangle of brick with yellow wallpaper behind; shop windows that open onto Level 0 rooms; zombies from doors, windows and manholes; one Core layout that Astra fills and Claude draws. Its write-up was incomplete, so only its lore and contract were used.

MERIDIAN's world could not be grafted onto another concept without replacing that concept's world, while BEACON's and LIGHTS OUT's strengths graft cleanly onto MERIDIAN.

**Grafted from BEACON:** the groundwork package; ExitReach and solid pods; the cabin loading card, and the seat at the bottom of the eject; the searchlight lock-on; rescued survivors; tier liveries; the war board; one tell per instance; the GeneratedFrameChecks suite; the ElevenLabs sound batch.

**Grafted from LIGHTS OUT:** the City Layout Contract; one shared band function for art and enemy tables; spawns from buildings, and manholes for today's mid-street spawns; the departures board over the DEPLOY gate; the Level 0 window rooms and missing-brick walls; the deliberate copy-pasted row; a vertical set piece for each district; a far ring of skyline; protected route lamps; the drop-ceiling street.

**Changed from MERIDIAN:**
- **The datum.** The sanctuary stays at y 0, where every hub record and the REVIVE-O-MATIC already sit. The city falls away below it.
- **The light field v2** follows the terrain instead of using 16 world slices.
- **Reach.** A pilot who stacks air-jump chips and MOON BOOTS can reach any roof (R11). So nothing's honesty depends on "out of reach": every roof is drawn as its collider, and survivors stand behind glass, mesh or a serving hatch, or under a low roof.
- **Time to control** is +3.6 s instead of +4.5 s.

---

## 1. The vision

**Meridian after 6:47.**

Every clock in Meridian stopped at 6:47, the night the Siren got out of Ward Zero, and it has been night ever since. (The game already has Meridian Biocontainment, and City Hall's clock stopped at 6:47.)

**The start.**
- An expedition starts at ATLAS, a split-flap departures board on the station wall that names the tileset you are about to drop into.
- On Deploy the floor opens. Your pilot falls through a lit tube into the seat of their pod, the harness slams down, and the pod drops out of the station's belly.
- The loading screen is the pod's cabin, with your squad's pod lamps blinking READY.

**The drop.**
- You come in burning over the city with your squad, each pod on its own bearing.
- The camp's searchlight finds you. The pods crash all round Belvedere Camp, a survivors' camp on the hilltop, never in a row, and leave smoking craters.
- You climb out among people: guards in the sentry towers by the gates, a quartermaster in the trading kiosk behind the camp's chip machine, a medic at the infirmary hatch beside its healing machine, a radio operator calling the night's news. Each time you clear an instance, someone new joins the camp.

**The city.**
- Beyond the gates is a real night city, much bigger than today's, built on a hill: sloped streets, terraces, stair-streets and rooftops.
- There is a viaduct to fight on, a rail cut to drop into, and zombies on the parapets.
- It is lit by its street lamps, windows and fires.

**The bleed.** Walk toward an instance and the city turns into it:
- near Level 0 the ground goes dead flat, the buildings start to repeat, and the rain stops under an office ceiling hung over the street;
- near the Poolrooms your footsteps start to echo;
- near Ward Zero the lamps turn red;
- near Central Station the wires hang overhead, and every clock reads 6:47;
- near the Ironworks the rain turns to ash.

The last streets before each instance are built from that instance's own materials, so players recognise exactly what they are walking into.

**Finding your way.** The stopped clock is north, the searchlight is home, and every instance lights its own patch of sky.

**Caches** are Civil Defence lockers that burst open in their rarity's colour, somewhere new every run.

**Delivery.** It ships in seven test-channel releases, each one playable. The first is the drop: the tileset board, and pods crashing all round the camp.

---

## 2. The design, request by request

### R1. ATLAS, the tileset machine

**The board** is a railway split-flap departures board, headed **ATLAS · DEPARTURES**.
- It hangs on the station's back wall, over the DEPLOY gate frame.
  - The frame reaches 5.6 m and the ceiling is 7.81 m.
  - The board is about 6.4 × 1.6 m, centred near (0, 6.6, 8.3).
- It has four columns: TILESET · ENEMIES · INSTANCES · FINAL.
  - **Row 1, lit:** CITY · MERIDIAN | THE INFECTED | 5 INSTANCES | THE SIREN.
  - **Rows 2-4, dim amber:** NOT IN SERVICE. No invented names and no dates.
- **Rows come from the registry.** The flaps draw runtime text from each tileset's display fields through one split-flap glyph atlas, built once. A new tileset's row needs its registry entry and, optionally, an emblem: no new code and no baked card art.
- If the wall band does not frame well in the DEV station preview, the fallback is a pedestal board at (-9.4, 0, 7.0), yaw 40°.

**Moments:**
- **When the station opens,** row 1 clatters into place in 1.2 s and a bell rings. This is an arrival, not a spin, so with one tileset nothing pretends to roll.
- **The console** gains a TILESET card first, so the order is TILESET, INSTANCE, MUTATOR, DEPLOY. The card has its own Shot that frames the board.
- **Later, with two or more tilesets:**
  - the host steps through the rows to pick one (Q1). A RANDOM lever that really rolls through the pool is added only if the owner wants one;
  - the chosen row lights in its accent;
  - the Instance Machine's marquee and reels re-skin to that tileset's pool;
  - the rolls clear.

**Elsewhere:** the Expeditions screen and the camp's war board both show TILESET: CITY · MERIDIAN.

**Cost:**
- about 6k triangles, so the station stays under its 130k cap;
- emissive flaps, so it takes none of the station's 8 practical lights;
- machine index 2 for sparks;
- its sounds play from a Claude presentation table, so Astra's `NativeDeploySounds` is untouched.

**Astra's part (A3):**
- a versioned tileset registry, with a mutator pool per tileset;
- a host intent, SelectTileset (RollTileset only if the owner asks for RANDOM);
- ticket schema 2, and deploymentProtocol 3;
- `worldSeed` mixes in only non-city tileset ids, so every City seed and every seed-pinned check stays as it is;
- every City-only site in the code (eleven, listed in A3) keyed by tileset, or deferred with an owner;
- a DEV-only proof tileset that differs from City in its hub enemy table, one mutator and its final-boss id, so the proof really exercises the switch. Players still see CITY alone.

**For the owner:** A3 ships a one-page "Adding a tileset" recipe. It lists everything a new tileset needs (registry row, generator and Version, look, bank families, pool, enemy table, mutator pool, final bosses, ambience, relief profile, board row and emblem), with the DEV proof tileset as the template to copy.

### R2 and R5. Modelled, not boxed

**The art route:**
- **The main route** is the house Blender kit: `tools/stages/city_*.py` on `stagekit.py`, headless Blender 5.2.
- **Owned packs** whose licence is on file ship through the Drive LocalAssets zip where they fit: Toon Harbor's street and wall lights, crates and containers (`docs/ASSET-CREDITS.md:179-183`, owner licence; the selection already exists in `tools/pack_bank/selections/kit-harbor.json`). POLY and Synty pieces ship only on the owner's word (Q15). Nothing licensed is committed.
- **The style** is "toon noir": the house anime-toon ramp, with outlines kept and tuned for night.
- **Contact shade** is modelled into the art, because nothing bakes ambient occlusion across a roll.
- **The art agents** run on Fable 5.2, per the owner's rule.

**The module bank** is built on a 2.5 m bay.
- **Ground floors** are 4.0 m: shopfronts, lobbies, depots, roller shutters.
- **Upper storeys** are 3.0 m, so buildings are 7, 10, 13, 16, 19 or 22 m tall. The 7 m height matches the terrace step in R11.
- **Detail:** cornices, parapets, sills and 15-20 cm window reveals.
- **Above 3.1 m:** balconies, fire escapes, tanks and vents.
- **Also:** corners, roof caps, stepped plinths for sloped streets, and road tiles 6 cm thick or less.
- **Roofs are honest.** A chip-stacked pilot can reach any roof (R11), so every roof is drawn as its collider: flat with parapet blocks, or pitched over ramp wedges with the art within 0.25 m. Spires, tanks and signs on reachable roofs are blocks, or rise above the map ceiling.

**Seven families:**
- Old Town;
- Threshold Business Park (Level 0);
- Bath Street (the Poolrooms);
- Meridian quarantine (Ward Zero);
- Victorian rail (Central Station);
- brick industrial (City Ironworks);
- civic (City Hall and the Crag).

Each family is its own bank file, so only the rolled districts load. The warp and threshold modules of each instance's family take that instance's own materials and fixtures: Level 0's wallpaper, carpet and troffers and the Poolrooms' tile from the Backrooms pack bank (CC0 add-on bakes, already in the zip), and Ward Zero's, the Station's and the Ironworks' from their stage banks.

**Hero buildings,** each modelled once:
- **The five halls,** each with its own door:
  - Level 0: a 6 m office door with a tiny brass knob, under a green EXIT;
  - the Poolrooms: frosted glass in a mosaic arch;
  - Ward Zero: a decon airlock with strip curtains;
  - Central Station: a rolling grille under the clock;
  - City Ironworks: a riveted blast door.
- **City Hall** and its clock tower.
- **The Cordon.**
- **Old Town hero lots:**
  - the Rialto cinema, its marquee reading NOW SHOWING · NOTHING;
  - a 24-hour diner;
  - a laundromat with its machines lit behind glass;
  - LUCKY PAWN;
  - a pharmacy cross;
  - an arcade full of Chip Arena cabinets;
  - a fire station;
  - a noodle bar.

**Gates and fences:** each of the four hub gates is unique.
- **Where they come from:**
  - north: the park's wrought-iron gate, plated in sheet steel;
  - east: shipping-container doors on a slide track;
  - south: the side panels of a cut-up city bus, with route 647 still on the blind;
  - west: a roller shutter in a scaffold frame.
- **How they move:** each fills its 10 × 3.2 × 0.4 m closure and is posed by `NativeExpeditionHubGates.PoseOf`. A shut leaf fills its closure. An open leaf clears 0.02 s before its collider releases, and rolls into its pocket, 0.21-0.39 m inside the gate's plane, right against the fence's inner face.
- **Also rig parts:** the halls' airlock leaves and the final court's portcullis.
- **Fence sections** are modelled to their 20 × 0.4 × 3.2 m boxes.
- **They come first.** The fence and the four hub gates have fixed ids and places on every roll, so they use the existing stage-art drawer and ship in M2, where players look on every run. The five hall fronts follow in M3.

**Props:**
- Until Astra's prop blocks arrive (a first pass on today's grid in M3), props go only where the rules allow: on walls, overhead, flat on the ground, or on roofs.
- The prop blocks are lamp posts, bollards, trunks, cars, buses, barriers, benches, shelters and barrels. Each stem gets a kit dresser in M3 and a modelled one in M4.

**Variety:**
- The planner picks modules by district and by a hash that mixes in `worldSeed`, so cells stop repeating between runs.
- Level 0's warp breaks the no-repeat rule on purpose.

**Order:**
- **M2:** the camp ring (fence and four hub gates), on the existing stage-art drawer.
- **M3:** the per-roll drawer, the City Layout Contract adapter and the five hall fronts.
- **M4:** the seven families on today's grid, with the prop dressers. Collision stays Astra's.
- **M5:** the layered city.
- The first hall and one street go to the owner before the rest.

**Fallback:** without the banks (CI, cloud and public builds), today's kit draws.

### R3 and R12. The bleed: the city turns into each instance

**The mechanism:**
- The blueprint's influences already exist. They are generation output, present on every peer and in the digest.
- One pure Core function, `NativeCityBleed.At(blueprint, x, z)`, returns the dominant instance and its share. It uses the same smooth math as today's `NativeDeployPresentation.Weights` (at most 0.08 change per 25 cm), so there are no seams.
  - It takes the grid's size and origin from the blueprint, not from the `GridWidth`/`GridHeight` constants that `Weights` and `CellAt` use today (`Core/NativeDeployPresentation.cs:224-241`), so it works on the 42 × 34 grid too. `Weights` becomes a thin wrapper over it.
- `NativeCityBleed.Band(share)` maps the share to a band.
- **Who owns it, and when:**
  - Claude writes the function in M2, for presentation only, with provisional thresholds tuned to today's grid.
  - Astra reviews its math when it lands, and owns the whole function, thresholds included, from A5a (M4), when it first gates spawns.
  - From then on, art and rules read the same function. Presentation may smooth its own blend on top, but never changes `At` or `Band`, so a presentation change can never move a spawn.

| Band | Share | Distance from the hall door (layered city) | What changes |
| --- | --- | --- | --- |
| Rumour | 0.10-0.25 | about 110-160 m | Sky, sound and a few signs |
| Drift | 0.25-0.45 | about 60-110 m | Materials, lamps, signage, ground paint, zombie clothes |
| Warp | 0.45-0.70 | about 30-60 m | The architecture goes wrong, the ground and light change, and the instance's own creatures start to appear (Astra) |
| Threshold | 0.70 and over | the last 30 m | The world is the instance, built from the instance's own materials |

- **The distances** hold on the layered city, after A4 retunes the influences. On today's grid the bands are shorter and the south arm is weaker (it reaches only 28-40% share 40-60 m in), so the M2 thresholds are tuned per grid and versioned with the world Version.
- **The land bleeds too** (R11): each theme has a relief profile, which is Astra's generation data.
- **The tell:** each instance has one, which players learn to read.
- **Read the world, not the signs.** The gate boards name each instance and its distance, as they do today. Out in the streets the survivors' trail points only home (SANCTUARY), so players find instances by the bleed.
- **Optional, presentation only:** the rumour band's sky and sound creep outward with run time until the instance is cleared, so a neglected instance seems to spread. Spawn bands do not creep unless Astra adopts it.
- **A cleared instance** calms its district over 8 s:
  - its lamps steady;
  - the bleed falls to half;
  - the sky and sound calm;
  - the camp cheers and the radio reports it;
  - the searchlight flashes that instance's colour;
  - Astra stops its warp and threshold spawns.

#### Level 0, the Backrooms (THE HUM): Threshold Business Park
**The tell:** the hum starts and the rain stops.
- **Relief:** a dead-flat plain on the lower town. The lots fall into a perfect grid, every block the same height.
- **Rumour:**
  - a 60 Hz hum under the rain;
  - clouds glowing fluorescent yellow;
  - THRESHOLD MUTUAL on the skyline, a tower whose every window is the same flat yellow with nothing behind it;
  - in a dark high window, a grin that fades after you have looked at it for a second. It is presentation, behind dark glass, never an enemy.
- **Drift:**
  - beige stucco offices with ribbon windows;
  - EXIT signs over doors that lead nowhere;
  - traffic lights stuck on amber;
  - fluorescent troffers on span wires instead of sodium lamps, buzzing in sync on one circuit and dying circuit by circuit;
  - shop windows that show yellow wallpapered rooms instead of shops (an interior swap in the window shader);
  - a payphone that rings when you are 15 m away and stops at 3 m;
  - zombies in yellowed office clothes.
- **Warp:**
  - buildings copy-pasted side by side, down to the same cracked pane;
  - doors on the second floor with no stair;
  - a window bricked up from outside;
  - rectangles of missing brick with yellow wallpaper behind, like a texture that failed to load;
  - wallpaper and skirting board on outside walls, in the Backrooms bank's own wallpaper;
  - damp carpet creeping over the pavement (flat paint, in the bank's carpet);
  - **the signature:** an office ceiling grid hung 6 m over a crossroads, half its tiles gone and its troffers buzzing. The rain stops under it and the street is lit like an office. It is overhead art above 3.1 m, like the camp's tarps.
- **Threshold:** the asphalt is carpet; the facades are windowless wallpapered walls; the hall's 6 m office door.
- **Sound:** the hum swells, rain and sirens fall away, and footsteps go soft.
- **Creatures (Astra):**
  - Hound packs in the warp band, from M4 on today's grid;
  - later, Facelings on wall anchors and a real Smiler in a dark pocket.
- **Vertical piece:** none, on purpose. Its piece is a street roofed by a drop ceiling.

#### The Poolrooms (THE DEEP END): Bath Street and the Shallows
**The tell:** your footsteps echo.
- **Relief:** the Basin, a sunken plaza half a step down. Its streets hold a sheet of still water a few centimetres deep. It is drawn only, with no rule.
- **Rumour:**
  - echoing drips and a pool filter's drone;
  - a teal glow and steam over the roofs;
  - a tiled water tower glowing aqua in the mist.
- **Drift:**
  - white tile up to 3.2 m under a teal band;
  - glass-block panels lit pool blue;
  - puddles that double every lamp;
  - NO RUNNING and NO DIVING plates;
  - storm drains glowing teal;
  - zombies in swimwear.
- **Warp:**
  - the pavement turns to the Poolrooms' own white tile, and the kerbs to rounded pool coping;
  - caustic light ripples over every facade;
  - lane ropes and pennant lines overhead;
  - pool lamps set low in the walls instead of street lamps;
  - a lifeguard chair on a balcony;
  - inflatable rings drifting in the flood;
  - tiled doorways with no rooms behind them.
- **Threshold:**
  - the rain stops, and the light becomes soft with no visible source. It is the one bright, calm place in a dark city, and that is the scare;
  - chrome-railed pool steps lead down into the flooded court, under the glass vault of MERIDIAN PUBLIC BATHS.
- **Sound:** the reverb lengthens with the share, water laps at your feet, and something splashes out of sight.
- **Creatures:**
  - Tilers, if Astra can anchor them on today's building faces in M4; otherwise on the layered city's wall features (M6);
  - the Drowned only where Astra's flood depth exists (M6 onward);
  - the Lifeguard stays home.
- **Vertical piece:** an aqueduct.

#### Ward Zero (THE SIREN): the Meridian Approach, the Cordon Line, the Decon Gate
**The tell:** the lamps turn red.
- **Relief:** a walled campus on a ridge spur (the upper town), at the top of a straight evacuation boulevard.
- **Rumour:**
  - searchlights crossing the clouds;
  - far off, a woman's voice holding one note, low-passed;
  - EVACUATION ROUTE signs pointing away from it, back toward the camp;
  - the Meridian tower, with a QUARANTINE banner and a slow red beacon.
- **Drift:**
  - the evacuation, abandoned: cars with their doors open, suitcases, STAY INDOORS posters;
  - white cladding and teal bands, from Ward Zero's own bank;
  - PA horns: REMAIN INDOORS;
  - zombies in gowns and scrubs.
- **Warp:**
  - the military cordon: sandbags and jersey barriers (Astra's blocks), decon tents overhead, checkpoint booths;
  - buildings wrapped in white quarantine sheeting that breathes in the wind, moving only above 3.1 m;
  - violet containment growth climbing walls and lamps;
  - every lamp red and turning.
- **Threshold:** the floodlit Decon Gate, under plastic decon tunnels (frames above 3.1 m, sheets hanging down to 3.1 m), with mist from the decon arches.
- **Sound:** her song comes clear, then klaxons and a PA voice.
- **Creatures:** the Siren's own kinds, so the change is in the mix: a spitter-heavy street mix from M4; spitters on the roofs and a Hulk bursting out of a quarantine panel (the breach entry) on the layered city (M6).
- **Vertical piece:** a skybridge between two hospital blocks.

#### Central Station (THE 6:47): Station Road, the Viaduct Arches, the Cut
**The tell:** the wires hang overhead, and every clock reads 6:47.
- **Relief:**
  - the Cut, a full step below the lower town, where the tracks run into the station;
  - a brick viaduct whose deck sits level with the upper town, striding over the streets.
- **Rumour:**
  - blue arcs on the clouds;
  - the 6:47's two-tone horn, far off;
  - PA chimes;
  - a stalled train on the viaduct with its windows lit.
- **Drift:**
  - tram rails converging in the road (flat paint);
  - roundels and cream tile;
  - catenary wires overhead, arcing blue every 8 s or so;
  - departure boards reading 6:47 EVACUATION SPECIAL · DELAYED;
  - red and green signal lamps as street lights;
  - luggage along the walls;
  - zombies in suits and trench coats.
- **Warp:**
  - the viaduct's arches bricked into lit shops, with a service stair up to the deck;
  - a derailed carriage hanging off the viaduct;
  - live-rail sparks;
  - flood water in the bottom of the Cut, and a commuter train frozen there with its cabin lights on;
  - footbridges over the Cut.
- **Threshold:** an iron-and-glass portico, the glass vault, and the great clock at 6:47.
- **Sound:** the PA, and the horn of a train that never comes.
- **Creatures:**
  - Conductors, whose whistle rallies the horde, and Pileups, from M4 if Astra confirms they work off their home map;
  - the Undertow only in the flooded Cut, and the Linesman only on its rails (later).
- **Vertical piece:** the viaduct.

#### City Ironworks (THE CONVERTER): the Ash Hollow, Furnace Row, the Slag Terrace
**The tell:** the rain turns to ash.
- **Relief:** down into the Ash Hollow, then a haul ramp up to the Slag Terrace (upper town).
- **Rumour:**
  - clouds lit orange from below;
  - three stacks burning at the top;
  - a drop hammer every 4 s, heard, with no camera kick unless camera shake is on;
  - ash in the rain.
- **Drift:**
  - brick depots, roller doors and rust;
  - sodium high-bays and caged orange work lamps;
  - soot and embers on the wind;
  - the shift whistle every 90 s;
  - zombies in coveralls and hi-vis.
- **Warp:**
  - pipe bridges on trestles over the street, with catwalks;
  - gantry cranes;
  - rail spurs with slag cars;
  - dark slag crust in the gutters, with faint orange cracks. It stays cool, so it never reads as a hazard it isn't. Molten metal shows only behind walls and grates that are blocks;
  - heat shimmer and sparks raining from a conveyor.
- **Threshold:** the furnace house, white-hot inside, with a gantry crane over the court and THE CONVERTER's vessel seen through a broken wall.
- **Creatures:**
  - Cinderlings, Stokers and Slagbacks, from M4;
  - later, Riggers on the pipe bridges' hangs.
- **Vertical piece:** conveyor gantries.

#### The final court: City Hall on the Crag, keyed by the final boss
- **The Siren:** every clock on the climb reads 6:47, the light turns violet, her song comes from the tower, and ink runs down the Hundred Steps.
- **NOCTIEL,** once a tileset's pool holds him: black feathers falling like snow, a broken halo of light in the clouds over the tower, and stained glass lit white-gold.
- **Future tilesets** reuse the machinery: the bleed is the tileset crossed with the instance's theme.

### R4. A darker city, lit by its lamps

**Art direction: toon noir.**
- Deep blue-violet shadows, warm sodium pools, each district's colour in its sky, and silhouettes against underlit clouds.
- Warm light means people. Cold or sick light means an instance.
- **The readability rule:** an enemy is always either lit (lamp light on actors) or silhouetted (rim and sky), never lost in murk.

**The look:** `expedition-night`, the generated map's own look.
- It is resolved by map id in the presentation.
- The generator's look string stays `expedition-city`, so the host's look check (`M1Arena:23-27`) still passes. The authored town keeps its dusk.
- **The owner approves it on a look board first** (Q16): four to six DEV-preview captures of today's street, hub and a hall front with the values below; Ward Zero's darkened interior as the darkness reference (the 2026-10-15 precedent); and two quick Blender renders from the stage kit (a Level 0 warp street, and four pods crashed round the fountain). This plan was written from a read-only checkout with no Editor run, so the board comes first after approval, before any look code merges.

| Setting | Value |
| --- | --- |
| Moon key | 0x9DB4E8 ×0.32, high at 38°; the only shadow caster, cascades to 60 m |
| Toon ambient (sky / equator / ground) | 0x1C2A4A ×0.22 / 0x1A1C30 ×0.14 / 0x0E0C14 ×0.08, about a tenth of today's |
| Rim | teal ×0.35, kept so silhouettes read |
| Fog | 0x0F1526, linear 10-150 m, tinted by the camera's district shares; the far layer below has its own curve |
| Height fog (layered city) | Pools in the low tiers; the Knoll stands above it |
| Sky | Navy, with a low cloud deck lit from below by each district's glow and swept by searchlights |
| Rain | 0.35; ash near the Ironworks; steam near the Poolrooms; none under the Level 0 ceiling or at the Poolrooms threshold |
| Lightning | Every 40-60 s; 0.15 s of skyline silhouette |

**The lamps carry the map.** The owner asked for "a lot more use of the street lights", so the light sources per street go up, not down.
- **Fixtures:** every street keeps at least today's fixture count (the kit puts two wall lamps on every face of 9 m or more, `Core/NativeExpeditionCityKit.Generated.cs:201`) and gains lamp posts (from M3), span wires at crossings, floods, neon, lit windows and fires.
- **Pools:** what changes is that each working lamp now throws a real pool through the light field. Working pools come every 10-14 m along a street, staggered across it, so a street reads as a chain of pools with darker gaps where lamps are dead.
- **Posts:** until Astra's thin post colliders land (A1, M3), lamps go on wall arms, span wires and flush mounts. After that, cobra-head posts stand on Astra's `lamp-` prop blocks.
- **Other sources:**
  - goosenecks;
  - floods at the camp, the courts, the hall doors and the Cordon;
  - neon;
  - roof fires, and barrel fires along the survivors' trail (against walls, within 0.25 m, until Astra's `barrel-` blocks exist).
- **Failure rates:** in the neutral town, 20% of lamps are dead and 12% flicker.
- **Protected route circuits:** lamps along `Hub.Routes` are warmer and none are dead. Side streets lose a third of theirs.
- **Lit windows:** 12% in the neutral town, 25% round the camp, and every window at Level 0's drift (the tell).

**The owned light packs** (Q15):
- **Toon Harbor's street and wall lights** (owner licence on file) go through the pack bank in the Drive zip and draw through our own NativeBlockLit (BLOCK_PACK), as the 2026-10-28 stage kits do. They are wall heads above 3.1 m, within 0.25 m of the wall, from M2, and post heads on Astra's lamp posts from M3. Our modelled fixtures, in the repo, are the public fallback.
- **Volumetric Light Beam** (named by the owner on 2026-10-28) draws High's hero beams (the searchlight, the camp floods, the threshold floods) through the private add-on. It needs three small public hooks, all Claude's: the map's light list in MapContext, a switch that stands our own beams down, and area visibility.
- **POLY's Subway and Construction lamps and Synty's light posts** ship only if the owner confirms their licences cover a shipped game. Until then they are references.
- Without the zip or the add-on, our own fixtures and `NativeVolumetricBeams` draw.

**Who lights what:**
- **The city light field** is `NativeWardZeroField` made parametric.
  - v1 (M2): 8 slices from 0 to 16 m, whole map at 1 m cells on High and 2 m on Balanced.
  - 4 circuits: 0 steady sodium; 1 flicker; 2 the district circuit, tinted per cell from the influences by a small 2D tint map; 3 alarm.
  - It bakes once per roll on worker threads, behind the loading card. Ward Zero and the Ironworks stay bit-identical.
  - The bank modules draw with NativeStageLit, which already includes `NativeWardLight.cginc` (`Resources/NativeStageLit.shader:41`), so the field's new parameters reach StageLit with their own pin (W1, W8).
  - v2 (M5) follows the terrain (W10).
- **The 8 practical slots** carry what moves: fires, burning pods, the searchlight, beacons, muzzle flashes and the brazier.
- **The light volume** (Balanced and High) carries the bounce. It is keyed per run (`worldSeed` and the generator Version), with candidates picked by importance and spread (W0). W10 rebuilds it for the layered city.
- **Characters:** `ActorLights(true)` in the city, so pilots and zombies take lamp light.
- **Wet streets:** each working lamp within about 40 m of the camera lays a reflection streak on the wet ground toward the eye. The streaks are camera-facing FX quads drawn from the lamp list, inside the 2,800-quad budget; the field supplies only their brightness. There is no new shader term.
- **Low tier:** no field and 3 slots. Flat lamp-pool decals, emissive parts and the rim carry the read. Fog ends at 100 m.
- **DARKNESS** becomes the grid failing.
  - Street lamps and windows go out.
  - Fires, beacons, the camp and a soft lantern on your own pilot stay lit.
  - The payout is Astra's and stays unchanged.
- **Hero beams:** VLB on High where the owner confirms it; otherwise our `NativeVolumetricBeams`. ShaftMax rises from 24 to 40, and the beam list from 128 to 256, only after measuring.

**The far layer** (for wayfinding):
- The kit draws a part only out to the fog end plus 25 m (`Core/NativeExpeditionCityKit.cs:79-84`): 175 m on High and 125 m on Low. The camera's far clip is 420 m. With 150-190 m routes, the landmarks by each hall stand 150-210 m from the camp, fully fogged and past the draw range.
- So landmarks, the searchlight beams, the district sky glows and the WMRD mast draw in a far layer that skips the kit's range and the linear fog. They use their own curve: their lit parts fog to at most about 65%, so lit windows and beacons read at 150-300 m; their unlit mass fogs into the sky but keeps its rim against the lit cloud deck.
- The far skyline ring, out to 900 m, is drawn in the sky pass, so the far clip stays 420 m.
- **LandmarkReadChecks** (Core) compute each landmark's fogged emissive colour against the sky at its distance from the camp, and require a contrast floor, at High and at Low.

### R5. Unique models: see R2

### R6. Belvedere Camp, the hub

Laid out in one pass, with the pods and machines, as one Core table: `NativeExpeditionHubPlan` (W5, and A1 in the hand-off).
- **Claude** proposes it and checks it in the DEV preview.
- **Astra** validates it, owns it from then on, and builds the blocks, slots and machine records from it.

**The place.** The sanctuary stays a 49.6 m square at the origin, with its fence and four gates unchanged in size and timing. It sits on the Knoll: Belvedere Park on the hilltop, with the city falling away below.

**Two rules shape every row:**
- **Keep clear of the gates.** Each gate's leaves roll into pockets 5.1-10.08 m either side of the gate centre, 0.21-0.39 m inside the gate's plane (the fence's centre line at ±25), right against the fence's inner face at ±24.8, under a top track at 3.3 m that runs out to 10.2 m (`Core/NativeExpeditionHub.cs:178-180`, `:300-302`). So nothing stands 5-10.3 m along a gate and within 0.5 m of the fence, up to 3.4 m high, besides the gate volumes and the lanes.
- **Survivors are never "out of reach" by height.** A chip-stacked pilot can reach any roof here. So every survivor stands behind glass, mesh or a serving hatch smaller than a pilot, or sits under a solid roof with less than a pilot's height (1.7 m) of headroom over the figure. Nobody can stand on a survivor's head.

**At the centre, the mess ring:** a square stone fountain with a brazier burning in its basin, under a corrugated ring roof at 2.6 m that is open over the fire. Three survivors sit on benches round the fire; a fourth bench waits for a rescued survivor.

**The landing field:** four pods land on spots drawn fresh every run, 11-15 m from the fountain, at least 12 m apart, doors roughly toward the fire (R7).

**The corners:**
- **NW:** the REVIVE-O-MATIC, unchanged; the mechanic, Sparks, works inside it.
- **NE:** the CHIP machine, facing the fountain, with the trading kiosk right behind it under an awning. The Quartermaster works from the kiosk's mesh hatch.
- **SW:** the MED machine, facing the fountain, with the infirmary cabin right behind it. Doc works from the cabin's hatch.
- **SE:** the lookout tower. A 9 m base carries a glazed cabin, with two lookouts behind the glass (one paces), and the searchlight on its roof. The searchlight is home from anywhere in the city.

**By the fence,** each piece just beyond a gate's leaf pocket:
- **Four sentry towers,** one by each gate: a base to 3.0 m and a mesh sentry cabin to 5.2 m, with a guard inside.
- **The radio shack** on the north fence, glazed, with the radio operator inside. Beside it, flat on the fence, **THE WAR BOARD:**
  - the rolled city painted on plywood, from the station's Preview;
  - TILESET: CITY;
  - one poster per instance, which gets a red X when cleared;
  - the party's names, and where each pilot is;
  - a desk lamp that clicks on within 4 m.
- **The MISSING board,** flat on the north fence west of the gate.
- **The supply cabin** on the south fence.
- **The generator** on the east fence, with cables to the camp along the fence.
- **Sandbags** beside each gate's lane, outside its sensor.

**Overhead, from 3.5 m:** tarps and bulb strings, from the fence posts to the lookout.

**Outside the fence:** the WMRD 64.7 radio mast, with red aircraft lights, stands beyond the map edge to the north-west, as skyline art in the far layer. On the layered city it stands beyond the Cordon.

**Survivors,** about 20 in all, seeded from the run, posed on the area clock and never replicated.
- **In the camp (13):** three at the fire, the Quartermaster, Doc, the radio operator, two lookouts, four sentries and Sparks.
- **Rescued (up to 5):** one joins per cleared instance, dressed in the camp's own look (a lantern, an armband, a blanket), not in the clothes of that district's zombies:
  - a nurse, at the infirmary window;
  - a ticket clerk, at the radio shack's second window;
  - a welder, in the trading kiosk;
  - a dazed office worker, wrapped in a blanket behind the supply cabin's window;
  - a pool attendant, on the fourth bench at the fire.
  - The names avoid the enemy kinds `conductor` (`Core/NativeStationKinds.cs:30`) and `lifeguard` (`Core/NativeBackroomsKinds.cs:20`).
- **Beyond the fence:** four figures in lit upper windows round the Knoll (a family at a table, a candle, a curtain drawn when you look up, a cat), and figures crossing lit windows now and then.
- **Bodies:**
  - near: the visual body in clean civilian clothes from the wardrobe and the garment bank, with lobby poses and bank idles;
  - at most 6 skinned within 30 m; rigid figures in the Greys' style beyond;
  - a voxel fallback.
- **The readability rule:** survivors carry lanterns and appear only inside the fence or behind lit glass, so anything zombie-shaped in the open outside the fence is a real enemy. There are no rooftop lookouts.

**Interactions.** The survivors take part in the hub's real interactions. Everything is presentation, driven by replicated events, with no new key:
- the Quartermaster works the CHIP machine's crank from the hatch as it dispenses, and slaps the counter;
- Doc reads the MED machine's monitor from the infirmary hatch and counts off the pulses, in text with a radio blip;
- Sparks cranks the REVIVE-O-MATIC through a reprint;
- each sentry works its gate's winch wheel as the leaves roll;
- the radio operator calls each clear on the camp radio (64.7 FM);
- the lookouts swing the searchlight onto each pod;
- heads turn toward a pilot within 8 m;
- **look-at lines:** face a survivor within 4 m for a second and they say a line, with a radio blip; about 60 lines keyed to the run (a clear, a death, a revive, the final gate, a mutator; under DARKNESS: "Keep your lights close");
- they wave at the landing pods, duck at impact, then cheer;
- on a clear, a cheer and a new string of lights;
- hats off for a death;
- a salute at extraction;
- a flinch at shots beyond the fence.

Trading or quests would be a separate Astra feature, later, if the owner wants them (Q18).

### R7. Pods crash in, spread around the camp

**The landing field** (Astra, A0, in the first release):
- Each run draws four landing spots from `worldSeed`, for seats 0-3:
  - 11-15 m from the fountain;
  - each pod centre at least 4.2 m off every gate axis, so the 4 m gate lanes stay clear with 1 m to spare;
  - at least 12 m apart;
  - each pod's yaw toward the fountain, ±30°.
- Every spot clears the camp's rows with its pod hull, a 2.5 m skid apron behind it and its capsule (W5's clearances). A draw that fails a clearance is re-drawn deterministically from the seed. Seats 4-7 take further draws.
- So four crashes never make the same figure twice. The presentation also varies each pod's skid direction, lean and crater shape.
- **In M1** the pilot's slot is the pod centre (ExitReach 0) and pods are not solid, as today.
- **From M3,** each pilot's capsule (its slot) waits at its ramp foot, ExitReach 2.4 m in front of the pod centre, and pods are solid while they stand. The spots themselves do not move.
- **PvP** on this map keeps today's spawn rows as its own table, and pod colliders apply only to expedition pod runs.

**The opening (5.0 s, proposed; 3 s today):**

| Time | What happens |
| --- | --- |
| -5.0 s | **Sky shot.** The loading card lifts into a view 70 m over the Knoll, looking 35° down toward the clock: the whole city under its cloud deck, each district's sky glowing. Four burning streaks appear on their own bearings, about 200 m out and 140 m up, drawn past the fog like meteors. |
| -4.6 s | **Lock-on.** The lookout's searchlight leaves its sweep and locks onto my pod. The sentries point up and the survivors look up. |
| -4.1 s | **The ride.** A cut to the chase behind my pod. In first person, one braced second in the red-lit cabin, the city rushing up in the porthole. |
| -3.7 s | **The cloud punch.** A vapour ring and a boom, staggered by distance. |
| -3.2 s | **Retros.** They flare. For two of the four pods (seeded) they sputter and fail. |
| -2.8 + 0.25·order s | **Impact.** |

**Each impact:**
- the pod comes in hot at 45-60° and touches down up to 1.5 m short, from behind its door (±40°), inside its skid apron;
- it ploughs in and comes to rest exactly on its pod centre;
- Slam chunks and marks;
- a shockwave ring and a dust wall out to 6 m;
- sparks, and a flash through the blast pool, which survives DARKNESS;
- `NativeStructureShake`, scaled by distance;
- the fence lamps flicker;
- the chase camera stops 10 m up, lets the pod punch in below it, then arcs down behind my pilot.

**After each impact:**
- **+0.6 s:** explosive bolts fire, and the door blows down into its ramp, venting steam.
- **+0.7 to +2.0 s:** my pilot braces, unbuckles and stands.
  - **M1:** they stand in the open pod, on the capsule, by GO.
  - **M3:** they step out and walk down the ramp onto the waiting capsule. The fourth pilot steps off just before GO.
- **GO:** control hands over with no snap. Today the contact flag stays "airborne" through every countdown, so the Land take and `move.land` fire at GO (`docs/SPAWN-IN.md:15-19`). A0 fixes that with SPAWN-IN option 1: the spawns are grounded and the ground probe runs during the countdown. If A0 slips, Claude suppresses the GO Land take and `move.land` on pod runs instead.

**What lasts:**
- the crater (5 m, flat, 6 cm at most), the scorch and a 3-5 m furrow stay for the run;
- the pod settles 0.2 m in, leaning 4° or less;
- the hull cools from cherry red to black over 20 s and ticks as it cools;
- the smoke thins over 40 s;
- all of it is derived from the pod records and the clock, so a late joiner and the killcam see the same craters and cooling hulls.

**The remodelled pod:**
- It keeps bones 0-5, r ≤ 1.17 m, height ≤ 3.0 m, and the Locker's paint, trim and light keys.
- It gains ablative tiles that glow then char, fins, and a porthole in the door.

**Comfort settings:**
- **Camera shake off:** the same story in steady directed shots.
- **Reduced effects:** a still, wide shot from the plaza, with no cabin moment and softer flashes.

**Fallback,** if the owner keeps 3 s: impacts at -1.75 + 0.12·order, the door at +0.35, and the pilot standing in the pod (M1) or stepping onto the capsule (M3) by GO.

**Going home by pod:**
- F at the door or anywhere on the ramp (A1 measures boarding from the door, not from the record, so the ramp side counts).
- The drawn pilot walks up into the seat before the door shuts at 0.5 s.
- The retros light at 0.9 s, and the pod lifts away, leaving an empty, smoking crater.
- LaunchGone (3.4 s) stays Astra's rule.

### R8. The drop-in: animated from the station to the street

**The eject (TravelAt goes from 4.0 to 5.6 s):**

| Time | What happens |
| --- | --- |
| 0-0.6 s | The machines dim, the room turns red, a klaxon sounds. |
| 0.4 s | Each pilot drops into a ready crouch: the Jump_Start gather. |
| 0.9-1.25 s | The hatches iris open. |
| from 1.35 + 0.09·pad s | Each pilot falls through: a startled arm throw into the Fall loop at 9.8 m/s². Pilots only ever move down. |
| 1.7 s | The tube shot: my camera looks straight down my lit drop tube, with ring lights streaking up every 0.12 s and my pilot falling below. |
| 2.7 s | The seat: my pilot lands in the pod's seat (the Chair pose with a Land compression). The harness bar slams down in 0.15 s, the cabin goes red, and the hatch seals with a bass thump. |
| 3.4-4.8 s | The release: the clamps fire, and my pod drops out of the station's belly over the rolled city at night. The city is drawn from the blueprint, like Preview: dark blocks, lit roads, three districts glowing. |
| 4.8-5.6 s | A white rush, then the fade. |

The release shot is the first to cut if pacing drags. A 4 s variant keeps the same beats without the release shot.

**Bodies:**
- The poses are game-rig tracks (`NativePackJumps`, `NativeLobbyPoses`), so the visual, voxel and Classic bodies all play them.
- One bespoke brace clip, baked on the owner's PC, only if the existing takes don't read.

**The cradle (loading):** the loading screen is the inside of my pod.
- a porthole with cloud streaks and lightning;
- the run's map in a night palette, each district breathing, each pod's streak drawn to its crater;
- an altimeter falling from 12,000 to 600 m with load progress;
- a lamp per party member: READY, LOADING 64% or LOST;
- a warning lamp per active mutator;
- the station's last line: PODS AWAY · GOOD HUNTING.

On pod runs every peer holds on the card until the hub's Countdown starts, so an early guest never sees pilots standing on bare slots.

**The exit:** see R7. The capsule never moves during the countdown.

### R9. Civil Defence caches

**The locker:** a MERIDIAN CIVIL DEFENCE locker, stencilled CD-647.
- About 0.90 × 0.60 × 0.55 m.
- A lid on a hinge bone, a latch, and a lock lamp in the tier colour.
- An LED price strip on the tinted-light bone, and an inner glow on the see-through bone.
- Sculpted in code like the machines, so it ships in the repo.
- **It arrives with its collider.** The steel locker replaces the hologram only in M3, with A2's solid block and seeded wall spots. Today's caches stand mid-street, 2 m from a spawn exit (`Core/NativeExpeditionGeneratedWorld.cs:110-115`), so a solid-looking locker there would be walked through and spawned through. Until M3 the cache keeps its hologram body, which reads as a projection.

**M2, on the hologram:** the new text, the F prompt and the loot moment, STANDARD only.

**Liveries,** once tiers exist (M3):
- STANDARD: olive steel, with hazard-orange corners and a white stencil;
- RARE: cobalt, with chrome corners;
- EPIC: violet, with glowing seams;
- LEGENDARY: black and gold.

**While it can be bought:**
- its lock lamp blinks in the tier colour;
- a thin column of motes rises in that colour; both read from 40 m;
- it goes dim when you are short of money.

**The loot moment,** from replicated data only (a generation change with the cooldown still ahead):
- 0.1 s: the latches pop;
- 0.25 s: the lid kicks open (on the hologram: it flares open), with cold vapour and a column of tier-coloured light that everyone sees, naming no buyer;
- only the buyer sees their three chips rise as cartridges and fan out at chest height, then dock on their pilot as pending. The CHIPS · C badge lights, and the C screen shows the same three. No chip is chosen at the cache: the buyer still picks with C (`Core/NativeExpeditionRewards.cs:42-49`);
- 2.2 s: the lid shuts, the lamp turns red, and an LED ring counts down RESTOCKS IN 0:42;
- at restock, a click; if the new stock rolls a different tier, its livery sweeps across;
- a late joiner sees an opened cache already open, from a first-seen clock;
- Reduced effects stays static.

**Text:**
- RARE CACHE (in the tier colour) · $175 · 3 CHIPS · KEEP 1 · F · OPEN CACHE;
- or $75 SHORT, in red;
- or RESTOCKS IN 0:42.
- No raw ids. Every string goes through ExpeditionLabels, with no allocation per frame. The J panel is fixed.

**Where (Astra, A2, M3):**
- **When it moves:** a new seeded spot every run and every reroll, not every restock, so players can plan.
- **Where it can stand:**
  - in a facade niche or an alley mouth, back to a wall, facing the street;
  - at least 40 m from the next cache;
  - at least 8 m from any spawn exit, door, door approach, join circle or the final court.
- **How many:** 4 on today's grid (one per district, one neutral); 5 on the layered city, one of them on a roof plaza reached by a stair-street or ramp: loot for climbing.
- **Instances:** 2-3 designed candidates per cache in Ward Zero, Central Station and the Ironworks.
- **Collision:** solid.

**Tiers** (Q26, needs the owner):

| Tier | Price | Odds per generation |
| --- | --- | --- |
| STANDARD | $100 | 60% |
| RARE | $175 | 28% |
| EPIC | $275 | 10% |
| LEGENDARY | $400 | 2% |

WINDFALL halves the price; BROKE doubles it. Offers come from the matching offerWeights row. Until tiers are approved: the clearer text, with STANDARD only.

The same loot moment plays for chip-machine purchases.

### R10. A chip machine and a healing machine in the camp

**Where:**
- CHIP at NE (13.5, 0, 13.5), yaw 3.927 rad;
- MED at SW (-13.5, 0, -13.5), yaw 0.785 rad.

Both face the fountain and mirror the REVIVE-O-MATIC in the corners.

**Rules (Astra, A1):**
- **Key:** F, like every PveMachine. The REVIVE-O-MATIC keeps E.
- **Prices** match the instances (Q29, a default):
  - the chip runs on the run-wide ladder ($175 to $350, mutators applied);
  - the med costs $75, with its 12 s recharge and 4 pulses of 12.5% max HP.
- **One of each.** No recruit.

**Look:**
- The machines keep their sculpted cabinets inside their collider envelope.
- They are dressed by the camp: the trading kiosk and its awning behind the chip machine, the infirmary cabin behind the med machine, cables to the generator, a lamp, hand-painted signs.
- The Quartermaster and Doc work them (R6).

**Labels:**
- CHIP MACHINE · $175, with the mutators applied;
- MED STATION · $75 · READY, or 0:12 while it recharges.

**Fixes that also help instances now** (M2):
- the chip label now includes the mutators (`NativeCombatHud.cs:221`);
- guests use the host's reach test, from the feet with line of sight (`:140`).

### R11. Meridian, a hill city in layers

**Scale** (N2, the owner's choice after a greybox walk):

| Option | Size | Area | Routes, gate to hall | On foot |
| --- | --- | --- | --- | --- |
| **A (recommended)** | 420 × 340 m: 42 × 34 cells of 10 m | about 3× today | 150-190 m | about 19-25 s |
| B | about 600 × 480 m | about 6× today | 250-300 m | about 30-40 s |

- **On foot** means running at 6.8 m/s and sprinting at 9.2 m/s while stamina lasts: 100 stamina at 12 per second gives about 8 s of sprint (`Core/ArenaModel.cs:9-10`).
- **Today** the routes are 70-110 m.
- **The anchor:** the owner pictures a Risk of Rain stage. Timing a run across it tells which option fits.
- **What B costs:** about twice A's nav nodes and generation time, crowd flow local to the pilots, about 40% more bank and landmark art, and longer M5 work.
- **Both are greyboxed for the walk** (A4 takes the size as a parameter), before any layered art.
- The rest of this section uses option A's numbers: bounds x ±210, z ±170, ceiling 60 m, killY -25.

**Ground tiers** (TerraceStep 7 m; the sanctuary is the datum):

| Tier | y | What is there |
| --- | --- | --- |
| The Crag | +14 | City Hall and the final court, north of the Knoll |
| The Knoll | 0 | The sanctuary and Belvedere Park |
| The upper town | -7 | Terraces round the Knoll; Ward Zero's ridge spur; the Slag Terrace; the viaduct deck |
| The lower town | -14 | The main streets; Level 0's flat plain; the Ash Hollow |
| The Basin | -17.5 | The Poolrooms' flooded plaza, half a step down |
| The Cut | -21 | The station's tracks, a full step below the lower town |

**Hills that read as hills.** Most tier changes are sloped streets, not walls:
- **sloped streets:** long MapRamp 'ramp' wedges at 1:8 to 1:6, so a 7 m rise takes 42-56 m. MapRamp already takes a ramp of any run under 45° (`Core/NativeMapRamps.cs:32-34`). Buildings along them stand on stepped plinths, with a basement storey showing downhill;
- **stair-streets:** where the slope is steep: MapRamp 'stair', 26.6°, at least 3 m wide, a landing every 3.5 m of rise;
- **haul ramps:** 1:4 or gentler, at least 6 m wide;
- **grassy banks** round the Knoll;
- **retaining walls** only where a terrace meets a cliff;
- **the Hundred Steps:** from the north gate up to the Crag, a 14 m rise in 100 risers. Zombies drop from its balustrades.

**Who can follow you where.** The generated nav is one graph sized for a Hulk (radius 0.954 m, height 3.06 m; `Core/NativeExpeditionGeneratedWorld.cs:173-187`):
- **Horde connectors are Hulk-sized:** sloped streets, stair-streets, haul ramps, the car park's ramps and decks, and passages under bridges and the viaduct are at least 3 m wide with at least 3.2 m of headroom. The car park's decks stand 3.5 m apart.
- **Pilot routes are not on the horde's graph:** fire escapes, ladders, rubble ramps (a slumped facade), narrow footbridges and gaps only chips can clear. The horde answers them with roof hatches, perchers and spitters, not by pathing.
- Astra may later add per-link size flags, or a second graph for ordinary zombies, with its own node and CPU budget.

**Roofs are a second street.**
- In a hill city, the roofs of a two-storey row (7 m) sit level with the next terrace's street, so you step onto them as plazas.
- Walk-ups of 7-10 m are reached by fire escapes and rubble ramps.
- **Roof gaps** (a plain jump peaks at 1.57 m and lasts 0.74 s; `Core/ArenaModel.cs:9`):
  - up to about 4 m: a running jump (it spans about 5 m on the level);
  - about 4-6 m: a sprint jump (about 6.8 m on the level);
  - wider: bridged, or a chip route (air jumps).
- **Parapets** are real blocks, at least 1.0 m tall.
- **Every roof is honest.** Six air jumps with Skybound reach about 10.8 m, about 21.6 m with MOON BOOTS, and jump-power and gravity chips go further (`Core/NativeJumpSimulation.cs:7-9`, `Core/NativeMovementContent.cs:12-14`, `Core/NativeExpeditionMutators.cs:27`, `Core/ConversionContent.cs:110`). So the plan assumes a stacked pilot can reach any roof:
  - **walkable roofs:** flat, with parapet blocks;
  - **pitched roofs:** ramp wedges, with the art within 0.25 m;
  - spires, tanks and signs on reachable roofs are blocks, or rise above the map ceiling;
  - there is no "crown" roof kind, and no pilot column over a building.
- **Pilot columns** stand only on the map edge: the Cordon's top, and the tops of the halls and the final court, which stand on the Cordon line.
- **No roof is a safe spot.** Every walkable roof without a horde connector carries a roof-hatch spawn entry, with one-way drop links to the street.
- This retires today's 40 m columns over every building, which the owner's no-invisible-walls rule forbids.

**Bridges and vertical pieces:**
- the viaduct deck, about 200 m long in the station district;
- footbridges over the Cut;
- sky-bridges between Old Town roofs;
- the Ironworks pipe catwalks;
- a three-deck car park with ramps in the neutral town;
- each district's vertical piece (see R3/R12).

**The Cordon** is the honest edge.
- A 14 m concrete blast wall on the bounds, with watchtowers every 60 m sweeping searchlights.
- A pilot column stands only above its top.
- Beyond it is skyline art only: dark suburbs, a jammed motorway, a harbour with cranes, bridges and hills, out to 900 m in the sky pass.

**Signature fights:**
- the Steps: hold the top of a stair-street;
- the Viaduct: fight on the deck while the horde moves underneath;
- the Cut: zombies drop in from both rims;
- the Roof Plazas: run the roofs over a street horde, with spitters on the next roof;
- the Haul Ramp: Slagbacks and brutes come up it in a column;
- the Hundred Steps, to the final court.

There is no fall damage, so dropping off a terrace is an escape.

**Zombies from the buildings,** not the middle of the road:
- spawn entries on shopfront doors, ground-floor windows, manholes and roof hatches;
- drop entries on parapets;
- breach panels.
- The art keeps their openings clear.
- Until blockout-4 moves the spawns, today's mid-street spawn exits are dressed as manholes whose lids pop and steam. The manholes are drawn by the presentation outside the kit, as the craters are, so the kit's spawn-exit keep-out (`Tests/GeneratedDistrictChecks.cs:97`) still holds.

**Enemies on the roofs** (M6):
- **Perchers:** spitters and runners crouched on roof edges, silhouetted against the lit clouds.
- **Leapers:** they crouch on the edge through the existing drop warning, then leap and slam the street (`NativeHordeDrop`, height from the entry).
- **On the move:** the horde follows you up sloped streets, stair-streets and ramps, and comes down by one-way drops. It meets you on pilot-only roofs through their hatches.
- **Heavies** breach district walls, and Slagbacks come up the haul ramp.

**Wayfinding.** There is no minimap; the world is the map.
- the stopped clock on City Hall's tower (north);
- the searchlight and the WMRD mast (home);
- each district's sky glow on its bearing;
- landmarks standing as lit silhouettes above the fog, in the far layer (R4);
- the protected route lamps;
- the bleed itself (R3/R12);
- the survivors' sprayed SANCTUARY arrows with walked metres (SANCTUARY 120 M), and flares at the turns, all pointing home;
- lit route boards on the gate frames, naming each instance and its distance;
- each district's sound;
- cleared districts relit.

### R12. See R3 and R12, above

---

## 3. Decisions: every open question with a default

"Owner" marks the six that need the owner's word. Every other row is a default the plan proceeds on unless the owner objects.

| # | Req | Decision | Owner |
| --- | --- | --- | --- |
| Q1 | R1 | The host picks on ATLAS. A RANDOM lever is added only if the owner wants one, decided when a second tileset is near. With one tileset the board lands on CITY when the station opens (an arrival, not a spin). | No |
| Q2 | R1 | At the Deployment Station, over the DEPLOY gate, as the console's first card; mirrored on the Expeditions screen and the camp's war board. The camp is generated from the roll, so it can only display the tileset. | No |
| Q3 | R1 | City alone. Rows 2-4 read NOT IN SERVICE, with no names or dates. | No |
| Q4 | R1 | All five instances stay in City. A future Backrooms tileset may list Level 0 and the Poolrooms too. | No |
| Q5 | R1 | Keep `black-siren` as the expedition id; the tileset becomes a new field, `city`. City's `worldSeed` is unchanged, so no digest or seed-pinned check churns. | No |
| Q6 | R1 | Yes, a final-boss pool per tileset: City has the Siren now and NOCTIEL once his rules are bound. The final court's bleed follows the boss drawn. | No |
| Q7 | R2/R5 | In order: the camp ring (M2) and the hall fronts (M3), then the street families on today's grid with collision unchanged, through the layout adapter (M4), then Astra's blockout-4 massing (M5). The modules carry over. | No |
| Q8 | R2/R5 | The house Blender kit in toon noir, with the outlines kept and tuned for night, is the main route. Owned packs with a licence on file (Toon Harbor) ship through the Drive zip where they fit. Other packs ship only on the owner's word (Q15). | No |
| Q9 | R2/R5 | Astra gives every free-standing prop a collider, thin posts included (0.3 m blocks for lamps, bollards and trunks). A first prop pass on today's grid ships in A1 (M3). Until then lamps go only on wall arms, span wires and flush mounts. | No |
| Q10 | R2/R5 | The budgets in section 6. The 480 MB visual-body cap stays. | No |
| Q11 | R2 | Freeze the authored town as the fixture for the legacy Expedition M1 mode, DEV and the validations, with its dusk look and no new art. Retire it later at the owner's word. | No |
| Q12 | R5 | Keep Toon Harbor. Build its kit bank into the Drive zip for the city's lamps, and for crates and containers where they suit the city. The modelled fixtures are the public fallback. | No (follows Q15) |
| Q13 | R3/R12 | District clothes from the drift band (presentation). Real instance kinds from about 60 m out (the warp band, a 45% share or more) and at the threshold (70% or more): at most 25% and 40% of spawns there, at most 6 alive per district, plain kinds first, none once that instance is cleared. On today's grid from M4. | **Yes** |
| Q14 | R3 | Calm and relight over 8 s (R3). Fewer zombies there would be a later rules option. | No |
| Q15 | R4 | Ship Toon Harbor's street and wall lights (licence on file) through the Drive zip, and VLB for High's hero beams through the private add-on (named by the owner on 2026-10-28). POLY's Subway and Construction lamps and Synty's light posts ship only if the owner confirms their licences cover a shipped game. Our own modelled fixtures and beams are the public fallback. | **Yes** |
| Q16 | R4 | A moonlit, overcast night after rain, with the values in R4, and a separate look for the generated city. The owner approves it on a look board before any look code merges, and on the A/B capture before M2 ships. | No (review gates) |
| Q17 | R4 | DARKNESS becomes the grid failing (R4). The payout is unchanged. | No |
| Q18 | R6 | About 20 survivors, standing or seated, behind glass, mesh or hatches, or under low roofs, plus one rescued per clear. They react to and take part in the real interactions, and say a line when you look at them. Trading or quests would be a later Astra feature. | **Yes** |
| Q19 | R6 | Everything on the plaza floor has collision, built by Astra in the one hub pass. Other dressing goes on the fence, flat, or overhead from 3.1 m. | No |
| Q20 | R7 | A seeded landing field: four spots per run, 11-15 m out, at least 12 m apart, doors toward the fountain ±30°, all inside the fence. Never a row. | No |
| Q21 | R7 | Yes, from M3. Runtime colliders come from the pod records while a pod stands. The pilot spawns at the ramp foot (ExitReach 2.4 m), never inside the pod. | No |
| Q22 | R7/R8 | Eject 5.6 s and opening 5.0 s: 3.6 s more before control. Each keeps a fallback that fits today's 4 s and 3 s. | **Yes** |
| Q23 | R7 | A lasting crash (R7). The retros flare only at the end, so it reads as a hard landing you walk away from. | No |
| Q24 | R8 | All three beats: the fall into the pod seat, the braced ride, and the climb-out. No open-sky skydive, because the pods are the fiction. | No |
| Q25 | R9 | A new seeded spot each run and each reroll, not each restock. The camp's city first, then 2-3 candidates per cache for Ward Zero, the Station and the Ironworks (exp-layout-8). | No |
| Q26 | R9 | Real tiers, with the prices and odds in R9 (Astra's rules). Until approved, the clearer text with STANDARD only. | **Yes** |
| Q27 | R9 | Solid (a 0.92 × 0.62 × 0.58 m block), and the steel locker appears only with its block (M3). The burst is public and names no buyer; the chips stay private. | No |
| Q28 | R9 | Caches stay in the streets, at least 8 m from any spawn exit, door or join circle and 40 m from each other, with one on a roof once roofs exist. | No |
| Q29 | R10 | Match the instances (R10). Hub chip purchases count on the same run-wide ladder. No recruit. | No |
| Q30 | R10 | The corners, mirroring the REVIVE-O-MATIC: chip NE, med SW. F for both. | No |
| Q31 | all | Yes: one hub plan table, `NativeExpeditionHubPlan`, for the landing field, machines, structures, posts and survivors. | No |
| Q32 | all | Astra does the collision and spawn work, from written contracts (the standing rule). Claude supplies the world brief, the hub plan and the art keep-outs. | No |
| N1 | R11 | Name the city Meridian; every clock stopped at 6:47. It already fits the lore, and it is display text that can change at any time. | No |
| N2 | R11 | Option A: 420 × 340 m with 150-190 m routes, about 19-25 s on foot. Option B: about 600 × 480 m with 250-300 m routes, about 30-40 s. Both greyboxed for the owner's walk before any layered art. | **Yes** |
| N3 | R11/R12 | Each theme shapes the land: the flat plain, the Basin, the Cut, the ridge, the Hollow and the Slag Terrace. Most tier changes are sloped streets. This is Astra's generation data. | No |
| N4 | R11 | The sanctuary on the Knoll, at the datum y 0, with the city below and the Crag above. | No |
| N5 | sound | About 45 sounds in one ElevenLabs batch on the owner's account, as for the Backrooms bosses, after an owner listening pass on the placeholders. Synthesized placeholders until then. | No (owner action) |
| X1 | R6 | The searchlight on the lookout tower is home. Once the final gate opens, it swings north and holds on City Hall. | No |
| X2 | R7 | From M3 the pilot spawns at the ramp foot, 2.4 m in front of the pod centre, which keeps Hulk clearance. Every slot user keeps a standing point outside the solid pod. | No |
| X3 | R8 | Voiced survivor lines: not now. Text lines with a radio blip; voice can follow later with no rule change. | No |
| X4 | all | The release order follows the owner's words: the pods ("First") and the tileset board ("now") ship in M1; night comes in M2. | No |
| X5 | R11 | Reach: the plan assumes a chip-stacked pilot can reach any roof or camp structure, so no honesty depends on PilotReach. Only if roof camping proves a problem in playtests may Astra propose a city-map cap on air jumps or MOON BOOTS to the owner. | No |
| X6 | R11 | Horde connectors are Hulk-sized; fire escapes, ladders, rubble ramps and chip gaps are pilot routes, answered by roof hatches, perchers and spitters. | No |

**Needs your word, most important first** (the recommended answer first):
1. **N2, the city's size:** option A, about 3× today's area with routes of about 19-25 s on foot; or option B, about 6× with about 30-40 s, which costs about twice the nav and generation time and more art. Picture a Risk of Rain stage and how long it takes to cross. You walk a greybox of each before any layered art.
2. **Q22, time to control:** an eject of 5.6 s and an opening of 5 s, 3.6 s more than today; or keep 4 s and 3 s, with the same beats compressed.
3. **Q15, the light packs:** ship the Toon Harbor lights and VLB to players through the Drive zip and the private add-on; add the POLY and Synty lamps only if you confirm their licences cover a shipped game.
4. **Q13, enemies near instances:** instance creatures from about 60 m out, under the caps above; or from about 110 m out (the drift band); or district clothes only.
5. **Q18, survivors:** reactions, help with the real interactions, and look-at lines, all cosmetic; or also trading or quests later, as a separate Astra feature.
6. **Q26, cache tiers:** STANDARD, RARE, EPIC and LEGENDARY at $100, $175, $275 and $400, with odds of 60/28/10/2%; or clearer text only, at one price.

**Owner actions, not decisions:**
- Review the look board before the night look is built, and the A/B capture before M2 ships (Q16).
- Run the ElevenLabs batch on your account when the placeholders have had a listening pass (N5).
- Forward the hand-off in section 8 to Astra, whole. It ends with an END marker.

**Owner decisions this overhaul supersedes** (proceeding on these unless the owner objects; logged in DEV_LOG with the first package):
- **2026-10-20,** "keep the voxel style and the dusk look" (`docs/DEV_LOG.md:8447`): superseded by R2 and R4.
- **2026-10-16,** "caches keep their existing look": superseded by R9.
- **2026-11-06,** one all-in-one expedition with "no point in selecting different expeditions" (`docs/EXPEDITIONS-MENU.md:5-12`): revised. There is still one expedition, `black-siren`, and the tileset is chosen inside it at the station (R1).

---

## 4. Work packages

Each package lists its owner, its scope for each side, what it depends on, the exact contract, its acceptance criteria, and the captures that prove it. The milestone each part ships in is in brackets.

**How the work is done:**
- **Branches:** each package is its own task branch from the shared Unity head (`origin/claude/sleepy-ritchie-g8ruhv` until the owner names another), with a PR into shared.
- **Before merging:** the full CoreTests, `python tools/check_unity_compile.py` and `git diff --check`.
- **Generation changes** also run the builder gates.
- **DEV_LOG:** each package logs its animation impact.
- **Shared contract files and who owns their fields:**
  - `NativeExpeditionHubPlan`: created in A0 with the landing field and ExitReach 0; Claude proposes the camp rows; Astra owns it all after A1.
  - `NativeCityBleed`: Claude writes it in M2 for presentation; Astra owns it all, thresholds included, from A5a.
  - `NativeCityLayout`: Claude's adapter shapes it in M3; Astra fills it in A4; Claude draws it.
  - `NativeTilesets`: Claude creates it in M1 with City's display fields; Astra adds the rules fields in A3.

### W0. Budgets and latent fixes (both; first) [M1]
**Claude:**
- **GeneratedFrameChecks** in CoreTests:
  - draws, triangles, instances per draw, shadow casters, FX quads, live lights and low-tier parts;
  - from 6 fixed eyes over 20 seeds, at High and Low;
  - record today's numbers, then pin them with 15% headroom.
- **A BoZo frame-time capture** with 1 and 4 pilots and 88 hub zombies, at High, Balanced and Low, using A0's DEV load harness. A 4-client Windows run with test accounts on the owner's PC confirms the numbers at M3 and M7.
- **The light volume:**
  - its key gains the run identity, `worldSeed` and the generator Version (`Runtime/NativeAdvancedLighting.cs:84`);
  - drop the doubled look lights, and pick candidates by importance and spread, so the east and south bake (`Core/NativeLightVolume.cs`).
- **Ground fog** skips `gen-floor` and sizes its grid from the map bounds (`Core/NativeExpeditionCityFx.cs:14-15,34,188,290`).
- **Practicals:** the generated map stops inheriting the authored town's 8 practicals and gets a list of its own, so no brazier fire sits among the pods.
- **Kit variety:** the kit hash mixes in `worldSeed` (`Core/NativeExpeditionCityKit.cs:143`).
- **Pod facing:** the pod presentation reads the record's yaw in degrees (`Runtime/NativePodPresentation.cs:84,120`).
- **Lobby:** pod-run pilots are hidden during Lobby.
- **The slot keep-out** (`Tests/GeneratedDistrictChecks.cs:99`) re-pinned for the landing field, with A0.

**Astra (A0):**
- `NativePodRun.InReach` reads the yaw in degrees (`Core/NativePodRun.cs:34`), with tests at 0, 90 and 225 in PodRunChecks.
- **The landing field** (R7): slots for seats 0-3 drawn per run, pods non-solid, ExitReach 0. PvP keeps today's spawn rows as its own table. One world Version bump.
- **The opening:** `ExpeditionRules.PodOpening` replaces the literal `Time + 3` at `Core/NativeMatch.cs:84`: 5.0 s for generated hubs if Q22 approves, otherwise 3.0. Other modes keep 3 s.
- **The GO snap:** SPAWN-IN option 1 (`docs/SPAWN-IN.md:94-98`): ground the spawns, and run `ProbePilotGround` before the non-fight early return (`Runtime/NativeArena.cs:117,124`), so pilots are grounded through every countdown. It removes the countdown "float" in every mode.
- **A native sim benchmark** on today's map, with 4 pilots and 88 zombies: nav ms, crowd-flow ms, perception and physics per tick. Rerun on every blockout change.
- **A DEV load harness** (Editor and DEV builds only, never in a release): a DEV lobby-count override for the hub cap (`Core/NativeExpeditionHubRules.cs:55`), and up to three stand-in members that BoZo walks along scripted routes. Parties are 1-4 signed-in accounts with no bots (`Core/NativeExpeditionParty.cs:8,15`), so a 4-pilot, 88-zombie frame cannot be measured on one PC otherwise.

**Depends on:** nothing.

**Contract:**
- Anchor, slot and pod-record yaws are in degrees everywhere. PveMachine yaw stays in radians.
- One budget table in `docs/EXPEDITION-DEPLOYMENT.md`, naming the machine, the tier and the settings. Whoever changes a number updates its row.

**Acceptance:**
- Baselines are recorded on the shared head before any overhaul code merges. If a baseline is already over its target (section 6), Claude reports it to the owner first.
- The frame check runs in the full CoreTests.
- Claude's W0 items change nothing visible beyond the fixes. A0's landing field ships only together with W4's crash.

**Captures:** the light volume before and after (the east and south now lit); the frame-time table.

### W1. Night falls (Claude) [M2]
**Claude:**
- **The look board first** (Q16, R4). No look code merges before the owner approves it.
- **The look:** the `expedition-night` look entry in `Res/MapLooks.json`, written by `docs/arenas/scripts/expedition_city.py --maplooks`.
  - `NativeLighting.Look` (`Runtime/NativeLighting.cs:230`) checks a presentation table, map id to look, before `map.look`. Its only row: `expedition-generated-city` to `expedition-night`.
  - The values are in R4.
- **Lighting:**
  - `ActorLights(true)` in the city;
  - the lamps, failure rates and protected route circuits in R4, with at least today's fixture count on every street;
  - lamp pools;
  - wet-street streaks as camera-facing FX quads from the lamp list.
- **The owned light packs** (Q15):
  - Toon Harbor's street and wall lights through the kit bank (`ChipArenaPackKit`) in the Drive zip, as wall heads above 3.1 m within 0.25 m of the wall, drawn through NativeBlockLit (BLOCK_PACK);
  - the VLB bridge in PrivateAddons for High's hero beams, with the three public hooks (R4);
  - our modelled fixtures and `NativeVolumetricBeams` as the public fallback.
  - ShaftMax from 24 to 40 and the beam list from 128 to 256, once measured within M2's allocation.
- **The city light field v1:**
  - `Core/NativeWardZeroField.cs` with its size, slices, colours and clip height as parameters;
  - 1 m cells on High, 2 m on Balanced, none on Low;
  - the district circuit tinted by a 2D map;
  - baked behind the loading screen;
  - Ward Zero and the Ironworks bit-identical;
  - the same parameters reach NativeStageLit for the bank modules.
- **Atmosphere:**
  - the district sky glows (horizon glows in NativeNightSky);
  - the fog and ambient tinted by the camera's Weights;
  - DARKNESS as the grid failing (`Core/NativeMutatorLook.cs:21`).
- **The far layer** (R4), with LandmarkReadChecks for what stands on today's grid (the hall beacons, the skyline). W10 adds the full landmark set.

**Astra:** nothing. Perception reads no light, and the DARKNESS payout is unchanged.

**Depends on:** W0, and the owner's look board.

**Contract:**
- Nothing new on the wire: map looks are in no compatibility hash.
- A DEV_LOG note to Astra: darker streets lower ranged readability, and no rule reads light.

**Re-pinned on purpose:**
- the look-script receipt (`tools/generate_expedition.py:130-147`);
- `tools/asset-lock.json:665-668`;
- `tools/package_source.py:47-52`;
- AdvancedLightingChecks:40-52;
- BlockLookChecks:61-68, for the field parameters;
- StageArtChecks:105 (the NativeStageLit shader), for the field parameters;
- ExpeditionDressingChecks:146-147,169-172;
- HordeKindChecks:337 and IronworksVisualChecks:241, which must stay bit-identical.

**Acceptance:**
- At street level the gaps between pools are dark and the pools read.
- **More light, not less:** at six fixed eyes, the lit light sources per 100 m of street are at least today's count, and the light reaching the street is several times today's.
- Pilots and zombies are lit under lamps and silhouetted elsewhere.
- On every tier, a shambler at 25 m and a spitter at 40 m read, both in a lit street and in a gap where a lamp is dead.
- The Low tier stays readable, and DARKNESS stays playable.
- A build without the zip or the add-on draws our own fixtures and beams.
- Frame time stays within M2's allocation (section 6), and `zfight.py` is clean.

**Captures:**
- an A/B strip, today against night, from six fixed eyes (the hub, the three forecourts, a mid street, 60 m up), at High and Low;
- the same street with the Toon Harbor heads and with the fallback fixtures;
- a DARKNESS strip;
- the readability strip;
- a 4-pilot frame-time capture.

The owner reviews the A/B before M2 ships.

### W2. ATLAS, the departures board (both) [Claude M1; Astra A3, M3 or M4]
**Claude, now:**
- the board: its model, a Built entry and a Footprint in `Core/NativeDeployModels.cs`, and its own Shot and Framing;
- `NativeTilesets` (Core), created with City's display fields;
- the flaps draw runtime text from those display fields through one split-flap glyph atlas, built once in `Editor/NativeDeployCardBuild.cs`;
- sparks for machine 2;
- the TILESET console card (`Runtime/NativeDeployConsoleUI.cs`);
- the Expeditions screen line;
- the arrival clatter and bell, from a Claude presentation sound table.

**Claude, later:** the pick animation (and RANDOM, if the owner wants it), reels re-skinned from the chosen pool, and the display half of the "Adding a tileset" recipe.

**Astra (A3):**
- the registry's rules fields, including a mutator pool;
- SelectTileset, which is host-only and clears the rolls (RollTileset only if the owner asks);
- ticket schema 2, and the revision rule at every site (`NativeExpeditionDeployment.cs:154`, `ExpeditionRunBundle.cs:47-49`, `SameDeploymentTicket` and the receiver);
- compatibility hashing every live tileset's pool;
- deploymentProtocol 3, coordinated with `codex/steam-player-hosted`;
- `worldSeed` mixes in only non-city tileset ids;
- the bundle picks the pool, generator, layout, enemy tables and mutator pool by tileset;
- the eleven City-only sites keyed or deferred (A3's table);
- the DEV proof tileset, and the rules half of the recipe.

**Depends on:** nothing, for Claude's half. Astra's half rides M3's compatibility break if ready, otherwise M4's.

**Contract:**
- **The registry,** `NativeTilesets` (Core):
  - fields: `{id, state: live or dev; display (Claude): name, place, accent, emblem, board row; rules (Astra): generator and its Version, look, bank families, pool [{instance, theme}], hub enemy table, mutator pool, finalBosses, ambience, relief profiles}`;
  - City is `id=city` and stays `live`.
- **State and ticket:**
  - `ExpeditionDeploymentState.tileset` is a string; null reads as city.
  - `ticket.tileset` (schema 2).
  - The revision stays monotonic across a tileset change.
  - `worldSeed` mixes in the tileset id only when it is not `city`, so City's seeds are unchanged.
- **Presentation:** DeployInput exposes the tileset and its pool. The presentation only shows the state and never decides it.
- **Unchanged:** the expedition id stays `black-siren`. `NativeDeploySounds` is unchanged.

**Acceptance:**
- The station stays under 130k triangles, with eight practicals.
- DeployModelChecks (:21, :30-34, :42-44, :51) and DeployPresentationChecks pass.
- DeploySparkChecks and DeploySoundChecks are extended to machine 2. ExpeditionsMenuChecks:36,49 are re-pinned.
- A check that ATLAS's instance list equals the Instance Machine's pool, and that the board's rows come from the registry.
- **Astra:**
  - host and guests see the same flaps;
  - an old client is refused cleanly;
  - ExpeditionDeploymentChecks are re-pinned to the new revision rule;
  - City's `worldSeed` for a given seed and roll count is unchanged (a pinned check);
  - the DEV proof tileset changes the hub enemy table, the mutator pool and the final-boss id (a pinned check);
  - RunDeploymentAcceptance on the builder.

**Captures:**
- the station Overview with the board, with 1 and 4 pilots;
- a 12-frame strip of the arrival;
- later, a host and guest pair on the DEV proof tileset showing the same flaps.

### W3. The eject and the cradle (Claude) [M1]
**Claude:**
- **The eject:** `TravelAt` goes from 4.0 to 5.6 s (`Core/NativeDeployPresentation.cs:199`), with the beats in R8.
  - New Shot values, keyed by my pad.
  - The tube, seat and belly models are drawn only after Deploy, and counted in DeployPerformanceChecks.
  - The deploy cast takes its poses from `NativePackJumps` and `NativeLobbyPoses`, so the voxel and Classic bodies play the same tracks.
  - A 4 s variant.
- **The cradle card:** it re-skins `NativeStageLoadingHud`, and `PartyLoadingRoster`'s data, for pod runs.
- **The hold:** on pod runs every peer holds the card until Countdown.
- **The ride:** the in-pod braced beat, in first person for a second.
- **Sound:** tube and cabin sound, from a presentation table.

**Astra:** nothing. A DEV_LOG note: runs start 1.6 s later.

**Depends on:** W0, for the Lobby hide.

**Contract:**
- `TravelAt` is Claude's Core constant, and the host already waits on it before RequestStage (`Runtime/NativeExpeditionDeploymentRoom.cs:44-50,74`).
- On pod runs the reveal waits for the Countdown phase, not for the area barrier.
- No wire change.

**Acceptance:**
- No rigid poses anywhere in the drop.
- Pilots move only down (DeployPresentationChecks:130-132), and the fade ends at TravelAt (:176, re-pinned).
- ExpeditionLoadingHandoffChecks:78, DeployPerformanceChecks and the SpawnInChecks:366-375 whitelist pass.
- DEV_LOG records no change to hit timing, hitboxes, damage, cooldowns, movement or networking.

**Captures:**
- eject strips, solo and with 4 pilots, at 0.1 s steps;
- the cradle card with four lamps, showing READY, LOADING and LOST;
- a host and guest run in which both hold on the card until the countdown.

### W4. Pods crash in (both) [M1, then M3]
**Claude, M1:**
- **The pod:** the remodel in `Core/NativePodModels.cs`.
- **The drop** (`Core/NativePodDrop.cs`):
  - a per-pod approach, hashed from seat, order, x, z and end, arriving from behind the pod's door (±40°);
  - the streaks, the cloud punch and the boom, and the retros, two of them failing;
  - the impact at -2.8 + 0.25·order (or the 3 s fallback);
  - the skid, at most 1.5 m and inside the spot's skid apron, to rest on the pod centre; the lean (4° or less) and sink (0.2 m), inside the collider envelope;
  - Slam, shake, shockwave, dust wall, sparks and the blast-pool flash.
- **What lasts:** the crater, furrow and scorch, drawn by the pod presentation with a shape varied per pod; the hull cooling and the smoke; all from the records and the clock.
- **The exit:**
  - the bolts and the blown door;
  - the drawn pilot strapped in on the pod's full offset and lean (today the body is raised only in y, `Runtime/NativeSpawnInPresentation.cs:106-110`);
  - the climb-out, ending standing in the open pod, on the capsule.
- **The camera:**
  - a Countdown branch in `Runtime/NativeCinematicCamera.cs`, with its own ride and shake, honouring Camera shake and Reduced effects;
  - the draw and camera limits raised above 2× Altitude for the approach.
- **Pod audio.**
- **PodDropChecks** rewritten for a crash.
- **The GO fallback:** if A0's grounding slips, suppress the GO Land take and `move.land` on pod runs.

**Claude, M3:** the climb-out continues down the ramp onto the capsule at the ramp foot; the F prompt shows at the door and on the ramp.

**Astra:**
- **A0 (M1):** the landing field, the yaw fix, `PodOpening` and the grounding fix.
- **A1 (M3):**
  - ExitReach 2.4 m: slots move to the ramp foot; the spots do not move;
  - pod colliders on host and guests, built from the pod records while a pod stands: an octagon of two crossed 1.96 × 2.85 × 1.96 m boxes, centred on record − forward(yaw°) × ExitReach, removed at launch, expedition pod runs only;
  - boarding measured from the door, so the ramp side counts.
- **Optional:** crash noise for the horde (HubNoise).

**Depends on:** W0 and A0 for M1. A1 for M3. Q22 for the 5 s.

**Contract:**
- **The pod record is unchanged:** seat, order, name, colours, look, x/y/z/yaw, end, launchAt, departed. The visuals derive from it.
- **ExitReach** is a Core constant in `NativeExpeditionHubPlan`: 0 until A1 ships, then 2.4 m. The drawn pod centre is record − forward(yaw°) × ExitReach.
- **The opening:** presentation reads `Appear = -PodOpening` on pod runs (`NativePodDrop.Appear` is today `-NativeSpawnIn.Countdown`).
- **Unchanged and Astra's:** LaunchGone, the launch length and Keys/Normalize.
- **The capsule** never moves during the countdown.
- **Save:** the pod look keys (paint, trim, light, account `cosmetics.pod`) are unchanged.

**Acceptance:**
- Four pods arrive from four directions, at least 12 m apart, and their craters persist.
- Over 200 seeds, the four landing spots never repeat the same figure, and never stand in a row.
- No ramp or skid apron blocks a gate, a machine or another pod.
- The third-person ride never clips the fence.
- **No Land take or `move.land` at GO,** on host and guest.
- PodDropChecks are re-pinned (:33, :34, :36, :40, :48, :54, :67, :68, :77, :133, :144-159, and the model limits).
- **Astra:**
  - PodRunChecks:21,23 and `Editor/NativeGeneratedWorldBuildChecks.cs:120` re-pinned for the opening (A0);
  - PodRunChecks pass at yaws of 0, 90 and 225 (A0), and board from the door and the ramp (A1).
- **The open review from 2026-11-02:** the ride, the cabin moment, boarding and launch, checked in a Windows host and guest run (M3).

**Captures:**
- four-pod strips from a fixed eye 46 m out, from the chase camera and in first person;
- Reduced effects and Camera shake off strips;
- a 0.1 s strip across GO, on host and guest;
- the craters and hulls at +20 s and +40 s, including a late joiner's view and the killcam's;
- a top-down view of the landing sites over three runs.

### W5. Belvedere Camp: the hub plan, survivors and machines (both) [M3; the machine fixes in M2]
**Claude:**
- **The plan:** the hub plan table below, checked in the DEV preview before Astra writes anything.
- **Camp art:**
  - the mess ring: fountain, brazier, canopy and benches;
  - the trading kiosk and its awning, the infirmary cabin, the supply cabin, the radio shack and the generator;
  - the lookout cabin and searchlight, and the four sentry towers;
  - bulb strings and tarps from 3.5 m;
  - the war board and the MISSING board, flat on the fence;
  - the WMRD mast, beyond the map edge in the far layer.
- **Survivors:**
  - about 20, each fitted to its structure or post;
  - the voxel fallback;
  - the interactions in R6, look-at lines included;
  - the rescued joiners on their reserved spots.
- **The machines:**
  - a `NativeInstanceMachineDress` entry for the generated map;
  - a machine draw flag that guests share (`GeneratedRun != null`);
  - the labels;
  - `area.machines`, the pods (r 1.4) and the hub blocks added to the kit keep-outs (`NativeExpeditionCityKit.cs:300-325`);
  - the sanctuary floor rule widened for the listed block ids only (`GeneratedDistrictChecks.cs:90-95,110-115`).
- **M2, ahead of the camp:** the chip label with mutators (`NativeCombatHud.cs:221`) and the guests' reach test (`:140`), which help instances now.
- **The camp's sound bed.**

**Astra (A1):**
- **Blocks:** the hub blocks from the plan, as generated-world blocks.
- **Machines:**
  - two PveMachine records, and the host's build gate (`M1Arena:110`) and serve gate (`:188`) lifted for the hub;
  - the hub prices (Q29);
  - revive clearance (ReviveMachineChecks:195,201).
- **The digest** hashes the machines, with a world Version bump.
- **Validation:** Validate, ValidateGeometry and PlaceExpeditionParty, with the clearances below.
- **The prop pass** on today's grid (A1).
- **Tests:** a hub machine suite modelled on InstanceMachineChecks; a native hub purchase in NativeExpeditionM1Validation; activation parity extended to the hub.

**Depends on:** A0's landing field. Q29 (a default).

**Contract,** the hub plan table (x east, z north, y up, in metres; fence inner faces at ±24.8; blocks are axis-aligned):

| Id | Where (x, z) | Size (x × y × z) | Notes |
| --- | --- | --- | --- |
| hub-fountain | (0, 0) | 3.6 × 0.9 × 3.6 | A square stone basin; the brazier fire in it is effects |
| hub-canopy-n, -s | (0, ±3.4), y 2.6-2.8 | 9.2 × 0.2 × 2.4 | The mess ring's roof, open over the fire |
| hub-canopy-e, -w | (±3.4, 0), y 2.6-2.8 | 2.4 × 0.2 × 4.4 | |
| hub-canopy-post-0..3 | (±4.4, ±4.4) | 0.3 × 2.6 × 0.3 | |
| hub-post-0..3 | (0, 3.4), (3.4, 0), (0, -3.4), (-3.4, 0) | 0.9 × 1.4 × 0.7, long axis round the ring | Benches with seated survivors facing the fire. 1.2 m headroom under the canopy, so no one stands on them. hub-post-3 waits for the rescued pool attendant |
| Landing field | Four spots per run (R7) | — | Pod hull, a 2.5 m skid apron behind it, and the capsule (ExitReach 0 in M1, 2.4 m from M3) |
| REVIVE-O-MATIC | (-13.5, 13.5), yaw 135° | Unchanged | Keeps E; Sparks works inside its solids |
| exp-city:hub-machine:chip | (13.5, 13.5), yaw 3.927 rad | Collider 1.3 × 2.2 × 0.9, 0.25 m behind | m 0; front 0.7, dispense 1.3, chipDrop 2.2 |
| hub-kiosk | (16.4, 16.4) | 2.6 × 2.6 × 2.6 | The trading kiosk, its mesh hatches on the south and west faces at the corner nearest the machine: the Quartermaster, and later the welder |
| hub-awning-post-0, -1 | (14.6, 12.4), (12.4, 14.6) | 0.3 × 3.4 × 0.3 | Carry the awning (art above 3.1 m) from the kiosk over the machine |
| exp-city:hub-machine:med | (-13.5, -13.5), yaw 0.785 rad | As the chip machine | m 1 |
| hub-infirmary | (-16.9, -16.9) | 3.0 × 2.6 × 3.0 | A hard-sided field cabin, its hatches on the north and east faces at the corner nearest the machine: Doc, and later the nurse at its window |
| hub-lookout | (13.5, -13.5) | Base 3 × 9 × 3; cabin 3.6 × 2.6 × 3.6 on top (y 9-11.6) | Two lookouts behind glass, one pacing |
| hub-searchlight | On the lookout roof, y 11.6-12.8 | 1.2 × 1.2 × 1.2 | A block, because a stacked pilot can reach the roof |
| hub-sentry-n, -e, -s, -w | (-11.5, 23.8), (23.8, 11.5), (11.5, -23.8), (-23.8, -11.5) | 2.0 × 5.2 × 2.0 | A base to 3.0 m and a mesh sentry cabin to 5.2 m, a guard inside; just beyond each gate's leaf pocket |
| hub-radio | (13.0, 23.9) | 4.0 × 3.3 × 1.8 | The radio shack, glazed: the operator, and later the ticket clerk. The war board is flat on the fence at x 15.4-19.0 |
| hub-supply | (-12.5, -23.8) | 3.0 × 3.3 × 2.0 | The supply cabin: later the office worker behind its window |
| hub-generator | (24.3, -12.5) | 1.0 × 1.4 × 2.0 | Cables to the camp run flat along the fence |
| hub-sandbag-* | Two per gate, beside the lane, outside the 12 × 12 sensor; for example (±7.5, 20.5) | 2.4 × 1.0 × 0.6, long axis along the fence | |
| Boards | The MISSING board flat on the north fence at x -13.0 to -16.5 | Flat | Outside every leaf pocket |

**Clearances** (Astra's Validate checks them; the distances are edge to edge):
- every pod hull, skid apron and capsule at least 1 m clear of every hub block, the machines' solids and their use, dispense and chip-drop points, and the REVIVE-O-MATIC's solids and mat;
- every block outside the gate volumes (10 × 10 interact, 12 × 12 sensor);
- **every block and fence-mounted board outside each gate's leaf travel and pockets, with the top track:** 5-10.3 m along each gate, from the fence's inner face (±24.8) to 0.5 m inboard, 0-3.4 m high;
- 4 m lanes clear from each gate inward to r 9;
- a walkway at least 2.5 m wide round the mess ring;
- **the fence vault:** within 3 m of the fence, every block is either 1.6 m tall or less (a plain jump from it peaks under the 3.2 m fence) or at least as tall as the fence (reaching its top already takes two air jumps). So no hub block lowers what it takes to vault the fence. Whether the fence itself gets the closed gates' no-vault rule stays Astra's call;
- **survivor headroom:** no post top with a figure on it has 1.7 m or more of headroom (the pilot capsule's height, `Core/NativeLocomotionRules.cs:8`);
- Hulk clearance on the lanes and round the camp, so the graph stays connected from every slot to every gate.

The DEV preview confirms every clearance at the field's worst draws before Astra writes the table.

**IDs and the rest of the contract:**
- Machine ids are as above, distinct from the legacy `exp-city:machine:0`.
- The F order is unchanged.
- Survivors are never replicated.
- **The release coupling:** Claude's draw flag and labels ship in the same release as Astra's gate lifts. Otherwise guests see, and bump into, machines the host never serves.

**Acceptance:**
- Nothing on the plaza floor can be walked through.
- Host and guests draw, label and serve the same machines.
- Survivors are identical on every peer, for a late joiner and on the killcam.
- No pod, skid apron, ramp, machine use point, gate volume or leaf pocket overlaps another.
- **A SurvivorChecks suite:**
  - deterministic per seed;
  - every figure behind glass, mesh or a hatch, or seated under a roof with less than 1.7 m of headroom;
  - at most 6 skinned bodies;
  - no allocation per frame;
  - part and caster budgets.
- MachinePlacementChecks:140-143 and MachineModelChecks:33-35 (Claude); MachinePlacementChecks:146 (Astra).
- A 4-pilot run with 88 zombies within M3's frame allocation.

**Captures:**
- night captures of the camp at eye level and from 46 m;
- each survivor in their structure;
- a cleared-instance cheer strip;
- the Quartermaster, Doc, Sparks and a sentry working their interactions;
- the machines' labels under WINDFALL and BROKE;
- a host and guest purchase pair whose prompts match;
- the camp as a late joiner sees it.

### W6. Civil Defence caches (both) [M2, then M3]
**Claude, M2 (on today's hologram):**
- the text through ExpeditionLabels, the F prompt and the J panel fix;
- the loot moment (R9): the hologram flares open, and the buyer's three cartridges dock as pending;
- STANDARD only;
- the same loot moment for chip-machine purchases.

**Claude, M3:**
- **The locker:** sculpted in code with the weapon kit (about 2.5k triangles).
  - It sits inside the 2.2 m kit keep-out and the 0.63 m chest floor limit.
  - It turns by the anchor's yaw, in degrees.
  - It replaces the hologram only where A2's solid block stands.
- **District decals,** which never change the silhouette.
- **Liveries** and the livery sweep, once tiers exist.

**Astra (A2):**
- **Spots:**
  - a seeded spot pool in place of `n%7` (`GeneratedWorld.cs:110-115`), re-picked every run and every reroll;
  - the counts and rules in R9;
  - the interaction at anchor + forward × 0.9 m, range 2.0.
- **Collision:** a solid block of 0.92 × 0.62 × 0.58 m, centred on the anchor and turned by its yaw.
- **Instance candidate sets** (exp-layout-8):
  - in the design scripts (`expedition_ironworks.py` CACHES at :468-473, `expedition_central_station.py`, `expedition_layout.py`) and the fixture `Tests/expedition-live-layout.json`;
  - offers and anchors paired per area.
- **Tiers,** if Q26 approves.
- **The mythic-pick check:** whether boss EPIC OR BETTER picks leave out mythic chips (`NativeExpeditionIntegration.cs:96`, `NativeExpeditionPick.cs:46`).

**Depends on:** nothing for M2. M3 needs A2, and Q26 for tiers.

**Contract:**
- `ExpeditionChestOffer.tier`, once approved: `standard`, `rare`, `epic` or `legendary`; missing reads as standard.
  - It is carried through validation, the view, JSON, the host handoff and the legacy savepoint.
  - It is rolled at SetWorld and at each restock, from the run's economy RNG on the host.
- The anchor's yaw, in degrees, is the chest's front.
- The open moment stays "the generation changed with the cooldown still ahead".
- The buyer and the offers stay private (`NativeExpeditionView.Leaks`).
- The chest block is in the digest.

**Acceptance:**
- No raw ids anywhere.
- The steel locker never appears without its block.
- Caches move between runs and rerolls (M3).
- The burst is public and the contents private (the Leaks tests).
- Standing at the model always means being in range.
- ExpeditionPresentationChecks re-pinned:
  - :436-459 the texts (M2);
  - :450-451 privacy;
  - :526-528 the chest floor (M3);
  - :531-533 reduced effects;
  - :613-640 the source scans.
- Also re-pinned: ExpeditionIntegrationChecks:141-149 and GeneratedDistrictChecks:100.
- **Astra re-pins:**
  - `Runtime/NativeExpeditionM1Validation.cs:39-45`;
  - ExpeditionDirectorChecks:40-41, ExpeditionIntegrationChecks:48 and ExpeditionMutatorChecks:56;
  - ExpeditionLayoutChecks:130,238, `test_expedition_layout_draft.py:129`, ExpeditionCityChecks:190,272 and ExpeditionIronworksDraftChecks:132;
  - `NativeExpeditionActivationBuildChecks.cs:164`.

**Captures:**
- the four liveries;
- the open beat at 0.1, 0.4, 1.0 and 2.2 s, from the buyer's view and a watcher's;
- three runs' cache positions plotted on a top-down view;
- a cache opened before a late joiner arrived, as the joiner sees it.

### W7. The bleed and the way (Claude) [M2 onward]
**Claude:**
- **`NativeCityBleed`** (M2): `At` and `Band`, with the grid's size and origin from the blueprint and provisional thresholds per grid; `Weights` becomes a thin wrapper. Astra reviews it, and owns it from A5a.
- **The bands:** rumour, drift, warp and threshold for all five instances and the final court (R3/R12), as module swaps, ground decals and set pieces. Warp and threshold use each instance's own materials.
- **Outfits by spawn,** in `ZombieModelOf`:
  - the entry, then the exit, then `NativeCityBleed.At`, then the theme (quarantine to ward, rail to station, industrial to foundry, liminal to office, baths to pool), then `NativeZombieLooks.Of`;
  - cached per id; only at drift share or more.
- **Atmosphere:** the sky, fog, ambient and weather blended by the camera's share.
- **Dressing:**
  - dead lamps in Astra's dark pockets, and flood planes, rails and hangs (once A4 exists);
  - manholes for today's mid-street spawn exits, drawn by the presentation outside the kit, so the kit's spawn-exit keep-out (`GeneratedDistrictChecks.cs:97`) stands unchanged.
- **Wayfinding:**
  - the survivors' trail along `Hub.Routes` Via points: SANCTUARY arrows with walked metres and flares at the turns, all pointing home;
  - lit gate boards from HubHeaders, naming each instance and its distance;
  - cleared districts relit from `ExpeditionView.instances[].cleared`;
  - themed join-circle surrounds outside the 3.3 m keep-out.
- **Optional:** the rumour band's sky and sound creep outward with run time until the instance is cleared.
- **The final court,** keyed by its boss (M7).
- **The soundscape:**
  - district beds crossfaded by the share: the hum, water, the song and klaxons, the PA and horn, the drop hammer and whistle;
  - carpet, tile and water footsteps.

**Astra:** nothing in this package. `NativeCityBleed` is theirs from A5a.

**Depends on:**
- Outfits, atmosphere, lamps, sound and the trail run on today's grid (M2).
- The modelled set pieces need W8 (M4).
- The terrain pieces need W9 and W10 (M5).
- The threshold creatures' dressing needs W11b (M6).

**Contract:**
- It reads only:
  - the influences, through `NativeCityBleed`;
  - the minion frame's spawn entry (`NativeNetworkFrame.cs:15,187`);
  - `Hub.Routes`;
  - the cleared flags;
  - W9's layout records.
- It writes nothing, and there is no wire change.
- District visuals read only the displayed area's blueprint (single view).

**Acceptance:**
- In a playtest, a player dropped at a random street point names the nearest instance within 10 s, from the world alone, with no instance name in the street.
- No seams at cell borders.
- Wardrobes are stable per id across peers, late joiners and the killcam.
- The body pools stay within ZombieMakesPerFrame and the spares.
- A BleedChecks suite: deterministic per seed; every set piece inside the placement rules; budgets.
- GeneratedDistrictChecks, for the blend and for half the buildings within 30 m in theme.

**Captures:**
- a walk from the camp to each hall, at 25 m steps, from rumour to threshold;
- outfits at three distances, including a late joiner's and the killcam's view;
- a cleared relight, before and after;
- the trail at each turn.

### W8. Modelled Meridian on today's grid (Claude) [M2, M3, M4]
**Claude:**
- **W8a, the camp ring (M2):** the fence sections and the four hub gates as rig parts, on the existing stage-art drawer, because their ids and places never change. The gates' leaves are posed by `PoseOf`.
- **W8b, the drawer and the halls (M3):**
  - **the drawer:** a runtime drawer on the prewarm hook (`Runtime/NativeStagePrewarm.cs:24`). It combines instanced modules into 32 m chunks at load, in 3 tiers, avoiding rigs or raising the 512-pose cap;
  - **the planner,** `NativeExpeditionCityArt`: modules picked by `NativeCityBleed` and a seeded hash; kit boxes hidden by rule;
  - **the layout adapter,** `NativeCityLayout.FromBlockout3`, which expresses today's blocks as the W9 layout (a lot per cell, terrace 0, today's pilot columns as edge columns). It is presentation only;
  - **the five hall fronts,** the hall airlocks and the final court's portcullis as rig parts;
  - kit dressers for A1's prop blocks, with Toon Harbor post heads on the lamp posts.
- **W8c, the streets (M4):** the seven families (R2) through the adapter, the modelled prop dressers, the final-court walls and the skyline.
- **The bank** throughout: `tools/stages/city_*.py` on stagekit, reusing `rooftop_city.py`, `cybercity_city.py`, `cybercity_street.py` and `wardzero_art.py`; one bank file per family.
- **The fallback:** without the bank, today's kit draws.

**Astra:** nothing; collision is unchanged. The prop blocks are A1's.

**Depends on:** W1, for the night materials.

**Contract:**
- None new. It reads the blocks, the blueprint, the keep-outs and the records already on every peer.
- generatedCompatibility is unchanged, and checked.

**Acceptance:**
- No box silhouettes on High or Mid (M4).
- Bank checks over 500 rolls:
  - art never more than 0.25 m short of a collider;
  - nothing standing out below 3.1 m, except flat paint of 6 cm or less;
  - keep-outs, and determinism;
  - triangles and draws per view.
- `zfight.py` at every tier.
- GeneratedFrameChecks pass, and StageArtChecks pin NativeStageLit's field parameters.
- The banks total 25 MB or less, with no file over 24 MiB.
- Public builds draw today's kit.
- The art agents run on Fable 5.2.

**Captures:**
- the four hub gates, open and shut (M2);
- each hall's front (M3);
- one street per family (M4);
- High, Mid and Low;
- the first hall and one street to the owner before the rest.

### W9. The layered city: blockout-4 and the City Layout Contract (both, Astra-led) [greybox from M1; release M5]
**Astra,** in `Core/NativeExpeditionGeneratedWorld.cs` and `NativeExpeditionDeployment.cs`:
- **Size:** the generator takes the size as a parameter, so both N2 options can be greyboxed; the release uses the owner's choice (option A: 420 × 340 m, bounds ±210 × ±170, ceiling 60, killY -25).
- **Ground:** the tiers, the relief profiles per theme, and sloped streets (R11).
- **Lots:**
  - 1×1 to 3×2 cells, with 0 or 2.5 m setbacks: alleys of 5 m, streets of 10 m, avenues of 15-20 m;
  - heights of 4 + 3n m, and towers of 25-60 m;
  - roofs walkable (flat, with parapet blocks) or pitched (ramp wedges); no crown kind.
- **Connectors and structures:**
  - sloped streets, stairs, ramps and the Hundred Steps as MapRamp records;
  - horde connectors Hulk-sized (at least 3 m wide, 3.2 m headroom); fire escapes, ladders and rubble ramps as pilot routes off the horde's graph;
  - bridges, the viaduct, the Cut and the car park (decks 3.5 m apart) as blocks;
  - the Cordon and its towers.
- **Pilot columns** only on the map edge: the Cordon's top, and the halls and final court on the Cordon line.
- **Validation:** Supported, Clear and Validate generalised to y, so support comes from any block top or ramp surface, through a spatial index (cell buckets). Today both loop over every block (`:152-158`, `:183-186`), which at this size would be about 10^8 box tests per layer on every peer.
- **Navigation,** across layers:
  - 2 m nodes on walkable surfaces, with layer ids;
  - walk, stair, ramp and one-way drop links;
  - roof islands for pilot-only roofs, with their hatches and drop links;
  - a validation-only pilot graph, for reachability through fire escapes and jumpable gaps;
  - crowd flow may be local to the pilots, which is Astra's call.
- **Spawn entries:**
  - door, vent, drop and breach entries on facades, windows, manholes, roof hatches and parapets;
  - a roof hatch on every walkable roof without a horde connector;
  - at least 20 m from door approaches.
- **Routes and influences:**
  - rings retuned by walked distance;
  - `Hub.Routes` Via points with y;
  - the influences retuned to the curve in R3.
- **New records:** features, landmark slots and prop blocks.
- **The camp:** the sanctuary and the hub plan unchanged at the origin.
- **The digest and Version:** `deployment-world-blockout-4`.
- **The native gates.**
- **Budgets:** blocks, nodes per layer and links counted on the greybox and written into the hand-off as the new bounds (section 6), and generation timed on the slowest supported guest machine.
- **First, a DEV-only greybox switch** at both sizes, for the owner's walk.

**Claude:**
- the world brief: this section and R11, plus the landmark footprints and the art keep-outs;
- a DEV top-down roll viewer at the new size;
- the holo war-table sized from the map (`NativeDeployStage.cs:61`, 200 × 152 px today);
- the skyline check sized from the map (`GeneratedDistrictChecks.cs:131`);
- the kit fallback dressing the new block roles, built by 32 m chunk, so the greybox reads.

**Depends on:** A0. N2 after the greybox walk. It starts during M1 as the long pole.

**Contract,** `NativeCityLayout` (Core, pure). It is versioned with the world Version, attached to the generated world, and every field is hashed into the digest.

```
CityLayout {
  version; cellMetres = 10; terraceStep = 7; minX, minZ, cellsX, cellsZ
  cells[]     { x, z, tier, use, lot, influences[4] }
              // use: street | plaza | lot | prefab | hub | court | cut | void
  lots[]      { id, x0, z0, x1, z1, tier, height, roof, setback[n,e,s,w], district, hero, seed }
              // roof: walkable | pitched
  links[]     { id, kind, horde, a{x,y,z}, b{x,y,z}, width, headroom, blocks[] }
              // kind: slope | stair | ramp | bridge | drop | fire-escape | ladder | rubble
              // horde: true when the link is on the horde's graph (Hulk-sized)
              // slopes, stairs and ramps are also MapRamp records; drops are also navHints.drops
  prefabs[]   { id, kind, x0, z0, x1, z1, turn, tier }
              // carpark | viaduct | gantry | skybridge | aqueduct | ceiling | cordon-tower
  landmarks[] { id, district, kind, x, y, z, yaw, sx, sz, height }
  features[]  { id, kind, volume | segment, depth }
              // dark | flood | rail | hang | wall | cut
  props[]     { block, stem }
              // lamp- | bollard- | tree- | car- | bus- | bench- | bin- | barrier- | shelter- | crate- | barrel-
  openings[]  { block, face, u0, u1, y0, y1, entry }
              // reserved spawn openings the art keeps clear
}
```

**Other parts of the contract:**
- **Spawns** stay `PveEntry` records in `area.spawns`:
  - their kind is from the existing set (door, vent, drop, breach);
  - `surface` (or a new hashed field) names what an entry emerges from: shopfront, window, manhole, hatch, parapet or breach-panel; vents keep ceiling, floor and wall;
  - `level` is the nav layer, and `height` is the drop height above its landing.
- **Block id prefixes by role:** terrace-, retain-, slope-, lot-, stair-, ramp-, bridge-, viaduct-, cut-, cordon-, crag-, landmark-, prop-, roof-.
- **New digest content:** MapRamp records, link kinds and horde flags, node layers, pilot columns, and every CityLayout record and spawn field.
- **PilotReach:** Astra publishes the reach envelope (rise and distance) for the worst live stack, for traversal design only. Nothing's honesty depends on it.
- **The art rule:** walkable roofs are drawn flat, with parapets that are blocks; pitched roofs are drawn on their ramps.

**Acceptance:**
- A 2,000-seed sweep passes Validate and ValidateGeometry.
- The native gates pass: 8 layouts, Hulk routes over every horde connector, and the 1,800-tick fixture.
- Every walkable surface, roof plaza and bridge is reachable by a pilot without chips (the pilot graph).
- Every walkable roof without a horde connector has a hatch.
- Routes run 150-190 m (option A).
- No street spawn falls within 20 m of a door approach.
- Blocks, nodes and links stay within the bounds measured on the greybox; generation takes 1.5 s or less on a worker on the slowest supported guest machine; sim cost stays within M5's allocation.
- ExpeditionDeploymentChecks:52-61 (the entrance share) and :150 (the bounds) are re-pinned to the measured numbers.

**Captures:**
- the greybox at both sizes: a top-down view of 8 seeds, a walk strip along each route, and a Hulk route trace;
- the sweep report, with the block, node and link counts.

### W10. The layered bank, the landmarks, the light field v2 and the light volume (Claude) [M5]
**Claude:**
- **Modules:**
  - terraces and retaining walls, sloped streets with stepped plinths, stair-streets with handrails on walls or blocks, haul ramps;
  - roof plazas with parapets, pitched roofs on their ramps, fire escapes, ladders, rubble ramps;
  - viaduct arches with bricked shops, footbridges, sky-bridges, pipe bridges, the car park;
  - the Cordon, with its watchtowers and searchlights.
- **Landmarks,** in the far layer:
  - City Hall and its 62 m clock tower at 6:47;
  - THRESHOLD MUTUAL;
  - MERIDIAN PUBLIC BATHS;
  - the Biocontainment slab and its beacon;
  - the station façade and clock;
  - the Ironworks stacks and furnace house;
  - the WMRD 64.7 mast, beyond the Cordon.
- **The far ring,** in the sky pass.
- **The relief bleed's dressing:** flood planes, the Cut, the flat plain, the ridge and the Hollow.
- **The light field v2,** two stacked ranges per cell:
  - the ground range, 0-12 m above the cell's lowest walkable surface, 4 slices;
  - the roof range, 0-6 m above its highest walkable roof, 4 slices;
  - so roof plazas 19-22 m up are lit as well as the streets.
- **The light volume,** rebuilt for the layered city:
  - its floor at the lowest walkable top, not the -8 m clamp (`Core/NativeLightVolume.cs:87`);
  - cells of 2 m or less where pilots walk. One box at this size would widen cells past 3.5 m under the 262,144-cell cap (`:51`);
  - tiled (each tile with its own 64 lights) or camera-local, whichever holds the budget row.
- **Height fog** in the low tiers.
- **LOD and chunk budgets.**

**Astra:** nothing beyond W9's records.

**Depends on:** W8 (the drawer) and W9.

**Contract:** W9's records.

**Acceptance:**
- Every block role is modelled at 3 tiers over 500 rolls, holding the 0.25 m and 3.1 m rules.
- LandmarkReadChecks pass for every landmark from the camp, at High and at Low.
- Frame time is within M5's allocation.
- The banks total 40 MB or less, with no file over 24 MiB.
- The field is 10 MB or less on High; the volume 12 MB or less on High and 4 MB on Balanced.

**Captures:**
- each landmark from the camp, at High and at Low;
- a roof run strip;
- the viaduct and the Cut;
- the bounce in the lower town and the Cut, before and after the volume's rebuild;
- High and Low.

### W11. District enemies and the rooftop horde (both) [W11a M4; W11b M6-M7]
**W11a, plain district kinds on today's grid (A5a, M4):**
- **Astra:**
  - `ExpeditionHubRules.Kind` (`Core/NativeExpeditionHubRules.cs:65`) gains the district and band at the spawn exit, through `NativeCityBleed`, whose thresholds Astra now owns;
  - **plain kinds per district,** each only where it works off its home map:
    - Level 0: Hounds, in packs of 3;
    - the Poolrooms: Tilers, if they can anchor on today's building faces; otherwise on the layered city's wall features (W11b);
    - Ward Zero: a spitter-heavy street mix;
    - Central Station: Conductors and Pileups;
    - City Ironworks: Cinderlings, Stokers and Slagbacks;
  - **caps (Q13):** at most 6 instance kinds alive per district, at most 25% of spawns in warp and 40% at threshold, none once that instance is cleared;
  - **network:** content rows bound on host and guests (`M1Arena:108-109`, `NativeNetworkFrame.cs:156-161`); caps on the city map (`Runtime/NativeExpeditionZoneEnemies.cs:12`); the Zone frame sent on the city map (`NativeNetworkFrame.cs:131`), which is a protocol bump;
  - rewards by theme, and a difficulty and CPU review with the owner. If the CPU review fails, the fallback is district clothes only.
- **Claude:**
  - the instance kinds' looks loaded on the city map when their district is rolled;
  - their outdoor readability at night;
  - their sounds;
  - a DEV_LOG note: no change to hit timing or hitboxes.

**W11b, the rooftop horde and feature-gated kinds (A5b, M6-M7):**
- **Astra:**
  - roof perches (a percher is Idle on a roof entry), roof-hatch entries, parapet drops through `NativeHordeDrop` with height from the entry, and breaches (Ward Zero's quarantine panels);
  - Slagbacks and brutes up the haul ramp;
  - **feature-gated kinds** (M6 onward, M7 for the last): the Smiler in dark features, the Drowned in floods, the Undertow in the flooded Cut, the Linesman on rails, the Rigger on hangs, the Faceling on walls.
- **Claude:**
  - perchers crouched on the parapet, in place of the ink tear;
  - perch idles;
  - breach panels styled per district;
  - the threshold creatures' dressing.

**Depends on:** W11a needs W7's `NativeCityBleed` and Q13. W11b needs W9.

**Contract:**
- Kind ids are unchanged and append-only (`Core/NativeHordeKinds.cs:26-44`).
- HubZombieBrain view states are unchanged: a percher is Idle on a roof entry.
- The minion frame is unchanged. The Zone frame's fields are unchanged; only where it is sent changes.
- The district kinds a run can meet follow from its blueprint.

**Acceptance:**
- No instance kind spawns below the warp threshold, or from a cleared instance.
- Hounds come in packs near Level 0 (M4).
- The horde reaches roof players by horde connectors and hatches, and leaves the roofs by drops (M6).
- CPU and frame size stay within budget.
- HordeKindChecks, the zone combat checks, ExpeditionHubAuthorityChecks:65-77 (re-pinned) and the activation fixture pass.

**Captures:**
- Hounds near Level 0 (M4);
- a percher's crouch, leap and slam (M6);
- a breach;
- a roof chase up a fire escape, met by a hatch.

### W12. The sound of the night (Claude, with an owner action) [with each package]
**Claude:**
- **Placeholders,** synthesized in the style of `NativeStructureSound.Land`, then about 45 ElevenLabs clips (N5) after an owner listening pass.
- **The eject:** klaxon, hatch, drop whoosh, tube rush, seat thunk, harness clamp, release.
- **The pods:** re-entry roar, boom, retros and their sputter, three impact layers, debris, bolts, door, steam, ticking.
- **The camp:** generator hum, radio bed (64.7 FM), cheer, gate winch, searchlight motor, the radio blip for look-at lines.
- **The lockers:** latch, lid kick, vapour, chip shimmer, restock chime.
- **The board:** flap clatter and bell.
- **The district beds and tells:**
  - the hum and the payphone;
  - drips and the pool's reverb;
  - the PA and the song;
  - the horn and the catenary arcs;
  - the drop hammer and the shift whistle.
- **The mix:** it ducks the camp behind a closed gate.

**Astra:** nothing; board sounds are in Claude's table.

**Depends on:** it rides with each package it gives sound to.

**Contract:**
- Presentation sound keys only.
- No gameplay event, rule or wire field.

**Acceptance:**
- DeploySoundChecks are extended.
- Every key resolves, with a fallback.
- **Loudness:** each cue's integrated loudness sits within ±2 LU of a named reference cue of its class in the existing mix (the REVIVE-O-MATIC's reprint cue for machines, `move.land` for impacts, the Ward Zero bed for beds), in a loudness table.
- The clips fit the Drive zip's audio budget (15 MB).

**Captures:**
- a recording of each bed, tell, and pod or eject cue (BoZo audio capture);
- the loudness table;
- the owner's listening pass on the placeholders, before the ElevenLabs batch.

---

## 5. Milestones: staged test-channel releases, in order

The order follows the owner's words: the pods came "First" and the tileset machine "now", so they lead. Night follows, because it is the broadest change Claude can make alone.

**For every milestone:**
- **Checks:** the full CoreTests, `check_unity_compile.py` and `git diff --check` before merging into shared.
- **The builder:** swap the Drive zip just before the builder push, and re-read `Latest-Build-Status.txt` first.
- **Owner review:** BoZo captures for the owner.
- **Windows:** a host and guest run whenever networking or the hub changed.
- **DEV_LOG,** with the animation impacts.
- **Compatibility:** every world Version or protocol bump refuses older test clients. Each is noted in the DEV_LOG and the release receipt.
- **Publication** to the test channel from the verified shared commit, per the standing instruction.

**M1, THE DROP** (joint; one world Version bump, for the landing field)
- **Astra, A0:** the yaw fix; the landing field (ExitReach 0, pods not solid); PvP keeps its spawn rows; `PodOpening` (5 s if Q22 approves); the grounding fix for GO; the sim benchmark; the DEV load harness.
- **Claude:**
  - W0;
  - W2: ATLAS showing CITY, the TILESET card and the Expeditions screen line;
  - W3: the eject at 5.6 s if Q22 approves, otherwise the 4 s variant, plus the cradle card and the hold;
  - W4, first part: the crash on the landing field, the climb-out to standing in the open pod, and the craters;
  - their placeholder sounds.
- **Playable:** the whole start of a run reworked: the tileset board on the station wall, an animated fall into the pod, a loading screen in the pod's cabin, and four pods crashing all round the camp.
- **If A0 is late,** M1 waits, or ships the board and the eject alone. A crash never ships in today's row.
- **Meanwhile:** Astra starts the A4 greybox; Claude makes the look board.

**M2, NIGHT FALLS** (Claude only; no rules, digest or protocol change)
- **Contents:**
  - W1: the night look, the lamps, the Toon Harbor wall heads, VLB on High, the light field v1, actor lights and the far layer's first pass;
  - W7's first pass on today's grid:
    - `NativeCityBleed`;
    - outfits by district;
    - district lamps (Level 0's troffers, Ward Zero's red lamps, the station's signal lamps, the Ironworks' caged lamps, the Poolrooms' low pool lights);
    - ground paint;
    - sky glows, fog and ambient by share;
    - the trail home and the protected route lamps;
    - manhole dressing;
    - cleared relight;
  - W8a: the modelled fence and four hub gates;
  - W6, first part: the cache text, the F prompt, the J panel and the loot moment, on the hologram, STANDARD only;
  - the machine label and reach fixes, which help instances now;
  - W12's placeholder beds.
- **Playable:** a lamp-lit night city with districts you can tell apart by light, sound and clothes, a modelled camp fence and gates, and readable caches that burst open.
- **Gates:** the look board before the look code merges; the owner's A/B review before M2 ships.

**M3, BELVEDERE CAMP** (joint; one world Version bump, plus deploymentProtocol 3 if A3 rides along)
- **Astra:**
  - A1: ExitReach, pod colliders and boarding from the door; the hub blocks and survivor structures; the camp's machines, their gates and prices; the prop pass on today's grid;
  - A2: seeded cache spots, the solid chest, exp-layout-8, and tiers if approved;
  - A3, if ready.
- **Claude:**
  - W4, second part: the climb-out down the ramp to the capsule;
  - W5: the camp, the survivors and the machine presentation, in the same release as the gate lifts;
  - W6, second part: the steel locker on A2's blocks, and the tier liveries;
  - W8b: the per-roll drawer, the layout adapter, the five hall fronts, the airlocks and the portcullis, and kit dressers for the new prop blocks, with Toon Harbor post heads on the lamp posts;
  - the board's pick animation, if A3.
- **Playable:** the living camp; solid pods you climb out of; the camp's chip and healing machines; steel lockers that move every run, with rarity colours; lamp posts, cars and benches you can bump into; the five instance halls modelled.
- **Also:** the 2026-11-02 boarding review and the 4-client frame check, in a Windows host and guest run.

**M4, MODELLED MERIDIAN** (joint; an expedition protocol bump for the Zone frame on the city map, plus A3's if it missed M3)
- **Claude:**
  - W8c: the seven families on today's grid, the modelled prop dressers, the final-court walls and the skyline;
  - W7's modelled set pieces that fit today's grid:
    - Level 0: copy-paste lots, window rooms, missing-brick walls, the ceiling grid;
    - the Poolrooms: tile, caustics, lane ropes;
    - Ward Zero: sheeting, violet growth;
    - Central Station: catenary, boards, 6:47 clocks;
    - City Ironworks: pipe bridges, embers;
  - W11a's looks, readability and sounds.
- **Astra:** A5a, the plain district kinds on today's grid; A3, if it missed M3.
- **Playable:** the box look gone from the streets on High and Mid, the bleed with its architecture, and instance creatures near their instances: Backrooms Hounds near Level 0.
- **Gates:** the owner approves the first hall (in M3) and one street before the rest; a difficulty and CPU review of the district kinds.

**M5, THE LAYERED CITY** (joint; Version `deployment-world-blockout-4`)
- **First:** the DEV greybox walk, at both sizes (N2).
- **Then:**
  - A4 / W9;
  - W10;
  - caches on roofs (A2 extended);
  - W7's relief dressing.
- **Playable:** the hill city at the owner's chosen size, with its sloped streets, terraces, roofs, stairs, bridges, the viaduct, the Cut and the Hundred Steps.

**M6, THE ROOFTOP HORDE** (joint; any protocol bump batched here)
- **Contents:**
  - A5b / W11b: rooftop perchers and leapers, roof hatches, breaches, Slagbacks up the haul ramp, and the first feature-gated kinds;
  - W7's threshold set pieces on the layered city;
  - W12's final ElevenLabs clips.
- **Playable:** roofs that are never safe, zombies leaping from parapets, walls that burst open, and the full sound of the night.

**M7, THE LAST CLIMB** (polish)
- **Contents:**
  - the final court keyed by its boss;
  - the remaining feature-gated kinds, as Astra finishes them;
  - a Low-tier pass;
  - a 4-player Windows run, host and guests;
  - budget sign-off;
  - docs: `docs/EXPEDITION-DEPLOYMENT.md`, the DEV_LOG, PROJECT_STATE if its status changed, and the "Adding a tileset" recipe checked against the code.
- **Playable:** the whole expedition, end to end, at night.

---

## 6. Budgets, and the gates to re-pin on purpose

### Budgets
These are measured first in W0, with A0's load harness. Every package carries its row in the budget table. Frame times are on the owner's PC, with 4 pilots and 88 zombies.

**Frame time: one absolute target per tier,** met at M7:

| Tier | p95 | p99 |
| --- | --- | --- |
| High | 12 ms or less | 20 ms or less |
| Balanced | 10 ms or less | 17 ms or less |
| Low | 8 ms or less | 14 ms or less |

No frame runs over 50 ms after the reveal, except area loads.

**Allocations.** W0 measures the baseline per tier. The headroom (the target minus the baseline) is shared out by milestone, and each release must stay inside its running total:

| M1 | M2 | M3 | M4 | M5 | M6 | M7 |
| --- | --- | --- | --- | --- | --- | --- |
| 5% | 20% | 15% | 20% | 25% | 10% | 5% (reserve) |

If a baseline is already over its target, Claude reports it to the owner before any overhaul code merges.

| Item | Target |
| --- | --- |
| Draws per view | 1,500 or fewer at High; 800 at Low |
| Triangles per view | 2.0 M or fewer at High; 0.8 M at Low |
| Shadow casters | 1,200 or fewer (the moon only, cascades to 60 m) |
| FX quads | 2,800 or fewer, the wet-street streaks included |
| Instances per draw | 500 or fewer |
| Live pixel lights | 9 at High, 3 at Low |
| Kit build (fallback), today's grid | 25,000 parts; 11,500 low-tier parts; 1,200 casters; 3,000 backdrop parts (unchanged) |
| Kit build (fallback), blockout-4 | Built by 32 m chunk. Within the draw range of any eye: 25,000 parts, 11,500 low-tier parts and 1,200 casters. 90,000 parts built in all. GeneratedDistrictChecks:47 re-pinned to the per-view count |
| Blocks on the map | Today 371-392, bound 500. After A1's camp and prop pass: about 600, bound 700. Blockout-4: measured on the greybox (about 2,000-3,000 expected), and the bound set from it |
| Nav | Today under 12,000 nodes. Blockout-4: nodes per layer and links measured on the greybox (about 20,000-30,000 nodes expected), and the bounds set from them. Supported and Clear through a spatial index |
| Station | 130k triangles or fewer, with 8 practicals |
| World generation | 1.5 s or less on a worker thread, on the slowest supported guest machine |
| Light bake | 3 s or less, behind the loading card |
| Light field | v1 about 3 MB (1 m cells); v2 10 MB or less on High (1 m) and 2.5 MB on Balanced (2 m); none on Low |
| Light volume | Today about 2 MB. Layered city: 12 MB or less on High and 4 MB on Balanced, with cells of 2 m or less where pilots walk; none on Low |
| Nav and crowd | Within the A0 baseline + 25% per tick at 4 pilots and 88 zombies |
| Sim wire | An area snapshot of 1 MiB JSON or less, and packets of 256 KiB or less (unchanged); frame caps of 256 minions, 500 props, 4,096 effects, 32 machine uses, machine index 0-7 (unchanged) |
| City banks | 40 MB or less in total (25 MB or less in M4); 24 MiB or less per file; only the rolled families load |
| Audio | About 45 clips, 15 MB or less |
| Drive zip | 512 MiB or less, with at least 25 MB spare, the Toon Harbor kit bank counted; under 5,000 files; the 480 MB visual-body cap unchanged |
| Survivors | At most 6 skinned within 30 m; no allocation per frame |

### Gates and tests to re-pin on purpose

| Check | What changes | Package | Owner |
| --- | --- | --- | --- |
| New: GeneratedFrameChecks, LandmarkReadChecks, SurvivorChecks, BleedChecks, city bank checks, HubPlan checks, LayoutContract checks | Added | W0, W1/W10, W5, W7, W8, A1, A4 | Claude; Astra for the last two |
| AdvancedLightingChecks:40-52; BlockLookChecks:61-68; StageArtChecks:105; ExpeditionDressingChecks:146-147,169-172; the look receipt (`generate_expedition.py:130-147`, `asset-lock.json:665-668`, `package_source.py:47-52`) | The new look and the field parameters | W1 | Claude |
| HordeKindChecks:337; IronworksVisualChecks:241 | Must stay bit-identical | W1 | Claude |
| DeployModelChecks:21,30-34,42-44,51; DeploySparkChecks; DeploySoundChecks; ExpeditionsMenuChecks:36,49 | Machine 2 | W2 | Claude |
| ExpeditionDeploymentChecks (revision, ticket JSON, receiver); a pin that City's `worldSeed` is unchanged; a pin on the DEV proof tileset | Schema 2 | A3 | Astra |
| DeployPresentationChecks:130-132 (kept), :176 (TravelAt 5.6); ExpeditionLoadingHandoffChecks:78; DeployPerformanceChecks; SpawnInChecks:366-375 | The eject and the cradle | W3 | Claude |
| PodDropChecks (:33, :34, :36, :40, :48, :54, :67, :68, :77, :133, :144-159; model limits) | A crash, not a landing | W4 | Claude |
| PodRunChecks:21,23 and the yaw cases; `NativeGeneratedWorldBuildChecks.cs:120` and :136-141; ReviveMachineChecks:195,201 against the landing field | The opening, the field, non-zero yaws | A0 | Astra |
| GeneratedDistrictChecks:99 | The landing field's slot keep-out | W0 | Claude |
| PodRunChecks, boarding from the door and the ramp | ExitReach | A1 | Astra |
| GeneratedDistrictChecks:90-95,110-115 (sanctuary floor, listed blocks only); :100; :131 (skyline from the map) | The camp, caches and size | W5, W6, W9 | Claude |
| GeneratedDistrictChecks:97 | Unchanged: the manholes are drawn outside the kit | W7 | — |
| GeneratedDistrictChecks:47 | Per view, at blockout-4 | W9 | Claude |
| MachinePlacementChecks:140-143; MachineModelChecks:33-35; ExpeditionPickChecks:63 | Camp machines (look) | W5 | Claude |
| MachinePlacementChecks:146; ReviveMachineChecks:169-173,181-201; `NativeExpeditionActivationBuildChecks.cs:171-172,194` | Camp machines (rules) | A1 | Astra |
| ExpeditionPresentationChecks:436-459,450-451,526-528,531-533,613-640; ExpeditionIntegrationChecks:141-149 | Caches (look) | W6 | Claude |
| `NativeExpeditionM1Validation.cs:39-45,97-104`; ExpeditionDirectorChecks:40-41; ExpeditionIntegrationChecks:48; ExpeditionMutatorChecks:56; the layout checks; `ActivationBuildChecks.cs:164` | Caches (rules) | A2 | Astra |
| ExpeditionDeploymentChecks:150 | The bound to 700 (A1); the greybox's numbers (A4) | A1, A4 | Astra |
| ExpeditionDeploymentChecks:52-61; the builder's 8 layouts, Hulk routes (:136-141) and 1,800-tick fixture | Blockout-4 | A4 | Astra |
| ExpeditionHubAuthorityChecks:65-77; HordeKindChecks; zone combat checks; HubPerceptionChecks | District kinds | A5a, A5b | Astra |
| ExpeditionCityChecks:43-48 and the authored town's pins | Unchanged: the town is frozen | — | — |

---

## 7. Risks and mitigations

| # | Risk | Mitigation |
| --- | --- | --- |
| 1 | **The critical path.** Blockout-4 is a large rewrite: generator, multi-level nav, entries, validation, gates. | M1-M4 ship value on today's grid. The greybox walk confirms the size before any layered art. The City Layout Contract adapter lets the bank work on both grids. |
| 2 | **Performance.** The generated map's frame time has never been measured, and a 3× world adds load: 88 zombies, tens of thousands of nav nodes, a light bake, more draws. | W0 comes first, with A0's harness. Absolute targets per tier, shared out by milestone. A spatial index for generation. Draws limited by the fog, modules chunked and instanced, banks split per district. The field uses 2 m cells on Balanced and none on Low. Nav may be local. |
| 3 | **Honesty in layers.** Zombies stuck on stairs, Hulks on narrow flights, art short of its collider, fake cover. | Horde connectors are Hulk-sized, and pilot-only routes stay off the horde's graph. Parapets are blocks. Every roof is drawn as its collider. Bank checks over 500 rolls, and `zfight.py` at every tier. |
| 4 | **Reach.** A pilot with six air jumps, Skybound and MOON BOOTS rises about 21 m, and jump-power and gravity chips go further. | Nothing's honesty depends on reach: every roof and camp structure is honest, survivors stand behind glass or under low roofs, and every pilot-only roof has a hatch. Only if roof camping proves a problem may Astra propose a city-map cap to the owner. |
| 5 | **Readability in the dark.** Ranged play, the Low tier and DARKNESS all suffer. | The lit-or-silhouetted rule, actor lights, the rim, and lamp pools on every tier. The 25 m and 40 m readability pins. The look board, and the owner's A/B before M2. |
| 6 | **Determinism.** Survivors, warp pieces, pod approaches, craters, wardrobes or lamps that disagree between peers. | Only records and seeded hashes, never per-frame randomness. Determinism checks in each suite, and late-joiner and killcam captures in W4-W7. |
| 7 | **Zip and memory.** | Banks per family, 40 MB and 24 MiB per file. The Toon Harbor kit bank counted. Audio 15 MB or less. The cap rises only with the owner's word. |
| 8 | **Compatibility breaks.** M1, M3, M4, M5 and M6 refuse older test clients and re-pin seed-pinned checks. | Batch each milestone's bumps. City's `worldSeed` is unchanged by the tileset field. Coordinate `NativeNetwork.cs` with `codex/steam-player-hosted`, and `NativeExpeditionM1.cs` with `codex/thunderstore-content`. Note each break in the release notes. |
| 9 | **Difficulty and CPU.** Instance kinds outdoors and spitters on the roofs both raise them. | The caps in Q13, plain kinds first, none in cleared districts, perchers silhouetted, and owner reviews at M4 and M6. |
| 10 | **Pacing.** 3.6 s more before control on every run. | The release shot is the first to cut. 4 s and 3 s fallbacks are kept. |
| 11 | **Comfort.** The directed drop and the cabin moment. | Camera shake off gives steady shots; Reduced effects gives a still, wide shot with no cabin. |
| 12 | **Licences.** Toon Harbor's licence is on file and the owner named VLB; the POLY and Synty licences are unconfirmed for a shipped game. | Nothing licensed goes into the repo. Packs ship only through the Drive zip or the private add-on, POLY and Synty only on the owner's word. Public builds draw our own fixtures and beams. |
| 13 | **The shared kit and look** with the authored town. | The town is frozen as a fixture, and the night look is resolved by map id. |
| 14 | **Survivors that read as zombies,** or that clip their posts. | The camp's own look, warm light and lanterns, only inside the fence or behind lit glass, roles renamed away from enemy kinds, and no rooftop lookouts. Each post sized to its pose, with checks. |
| 15 | **Shader fragility.** The 8-slot arrays once crashed the Windows shader compiler, and the field adds parameters to two shaders. | Builder test passes before merge, the BlockLookChecks and StageArtChecks pins re-pinned deliberately, keyword variants kept to a minimum, and the wet-street streaks drawn as FX quads instead of a new shader term. |
| 16 | **Double work,** from building the bank on today's grid and then again for blockout-4. | Modules, halls and landmarks carry over; only placement changes, through the same layout contract. |
| 17 | **Coordination.** A0, A1, A2 and A4 all touch the generated world's records. | Astra owns the generator file. One hub plan table, frozen at A1. DEV_LOG entries before editing shared records. One writer per file. |
| 18 | **A joint first release.** M1 needs A0, so Astra's timing gates the owner's first request. | A0 is small. If it is late, M1 waits or ships the board and the eject alone; a crash never ships in today's row. |

---

## 8. Hand-off to Astra (paste-ready)

```
===== BEGIN HAND-OFF TO ASTRA =====
```

# Expedition overhaul: rules, generation, collision and network (hand-off to Astra, 2026-11-18)

From Claude, passed on by the owner, who divides the work: Claude builds the presentation, and you build the packages below. This hand-off stands on its own. It ends with an END marker; if the marker is missing, ask the owner to forward the saved plan file again. Claude will commit the full plan as `docs/EXPEDITION-OVERHAUL.md` with the first package.

Paths: `Core/`, `Runtime/`, `Editor/` and `Resources/` are under `unity/ChipArenaConversion/Assets/ChipArena/`; `Tests/` and `tools/` are under `unity/ChipArenaConversion/`. Line numbers are at shared `442bcf69`.

## What the owner asked (R1-R12)
- **R1:** a tileset machine. The tileset decides enemy types and instances. Only City exists now, but the machine must be ready.
- **R2 and R5:** move City off the box style, with uniquely modelled buildings, props and gates.
- **R3 and R12:** streets shaped by their instance, buildings and nearby enemies included. Near Level 0, for example, Backrooms-style buildings and some Backrooms enemies.
- **R4:** a darker map lit by street lamps.
- **R6:** a detailed hub, with survivors and interactions.
- **R7:** pods that crash into the hub, spread apart. The owner named this "First".
- **R8:** an animated drop-in.
- **R9:** metal-chest caches, with a loot effect, better cost and rarity text, and new spots every run.
- **R10:** a chip machine and a healing machine in the hub.
- **R11:** a much bigger expedition area on the scale of Risk of Rain levels, with elevation, taller buildings and zombies on the roofs.

## Ground rules
**Base and branches:**
- Fetch first. The base is the shared Unity head, `origin/claude/sleepy-ritchie-g8ruhv`, unless the owner names another.
- One task branch per package, with a PR into shared.

**Checks:**
- Before merging: the full CoreTests, `python tools/check_unity_compile.py` and `git diff --check`.
- Generation changes also run the builder gates: 8 layouts, Hulk routes and the 1,800-tick fixture.

**Files:**
- **Yours:** the generator and rules files. Claude does not edit:
  - `Core/NativeExpeditionGeneratedWorld.cs`;
  - `NativeExpeditionDeployment.cs`;
  - `ExpeditionDeploymentRoom.cs`;
  - `ExpeditionRunBundle.cs`;
  - `NativeExpeditionHubRules.cs`;
  - `NativeMatch.cs`;
  - `NativePodRun.cs`;
  - `Runtime/NativeArena.cs`'s step and probe;
  - the integration, director and rewards files;
  - `NativeNetworkFrame.cs` and `NativeNetwork.cs`;
  - `NativeExpeditionM1Arena.cs`.
- **Claude's:** the presentation files, including:
  - `NativePodDrop.cs` (except LaunchGone and Keys/Normalize);
  - `NativeDeployPresentation.cs`;
  - the city kit;
  - lighting;
  - `NativeZombieLooks.cs`;
  - the machine models.
- **Shared contract files,** and who owns their fields:
  - `NativeExpeditionHubPlan`: you create it in A0 with the landing field and ExitReach 0; Claude proposes the camp rows; you own it all after A1.
  - `NativeCityBleed`: Claude writes it in M2 for presentation; please review its math when it lands; you own it all, thresholds included, from A5a, when it first gates spawns.
  - `NativeCityLayout`: Claude's adapter shapes it in M3; you fill it in A4; Claude draws it.
  - `NativeTilesets`: Claude creates it in M1 with City's display fields; you add the rules fields in A3.

**Coordination:**
- deploymentProtocol and `Runtime/NativeNetwork.cs` with `codex/steam-player-hosted`.
- `Runtime/NativeExpeditionM1.cs` with `codex/thunderstore-content`.
- Log in `docs/DEV_LOG.md` before editing shared records.

**Reach, for everything below.** Six air jumps with Skybound reach about 10.8 m, about 21.6 m with MOON BOOTS, and jump-power and gravity chips go further (`Core/NativeJumpSimulation.cs:7-9`, `Core/NativeMovementContent.cs:12-14`, `Core/NativeExpeditionMutators.cs:27`, `Core/ConversionContent.cs:110`). So the plan assumes a stacked pilot can reach any roof or camp structure, and no rule or check may rely on "out of reach" by height.

**Do not change:**
- **Records and wire:**
  - the pod record's fields;
  - the minion frame;
  - HubZombieBrain view states.
- **Ids:**
  - kind ids, which are append-only;
  - the `black-siren` expedition id.
- **Behaviour:**
  - the F order (except lifting the machine gate for the hub);
  - the REVIVE-O-MATIC's spot and its E key;
  - the privacy of cache buyers and offers.
- **Geometry and timing:**
  - the door contract (closures, interact, approach, join, returns; interiors stay authored);
  - the sanctuary square and its gates' sizes and timings.
- **Saves:** there are no run saves, and the pod look keys are unchanged.

## Owner answers that gate your packages
These are the plan's defaults until the owner says otherwise.

| Question | Default | Gates |
| --- | --- | --- |
| Q22, clocks | A 5.0 s hub opening for generated runs (3.0 if the owner says no); PvP keeps 3 s | A0's `PodOpening` |
| Q29, camp machine prices | Match the instances; no recruit | A1's machines |
| Q26, cache tiers | STANDARD, RARE, EPIC and LEGENDARY: $100, $175, $275, $400; 60/28/10/2% | A2's tiers |
| Q1, the tileset choice | The host picks (SelectTileset). RollTileset only if the owner asks for a RANDOM lever | A3 |
| Q13, district enemies | Instance kinds only at warp (a 45% share or more, about 60 m out on the layered city) and threshold (70% or more), under the caps in A5a | A5a |
| N2, scale | Option A: 420 × 340 m with 150-190 m routes. Option B: about 600 × 480 m with 250-300 m routes. Both greyboxed for the owner's walk | A4's release |
| Q18, survivors | Cosmetic only (Claude). Trading or quests would be a later feature of yours, only if the owner asks | Nothing now |

## Order
1. **A0,** now. It ships in M1, with Claude's crash.
2. **A4's greybox,** in parallel, at both sizes. It is the long pole; start it during M1.
3. **A1 and A2** in one release (M3), with one world Version bump. A3 joins them if ready, with its protocol bump batched in.
4. **A5a** (M4), with its protocol bump. A3 lands here if it missed M3.
5. **A4's release** (M5).
6. **A5b** (M6), and its last feature-gated kinds (M7).

## A0. The landing field and groundwork (M1)
**Do:**
- **The yaw fix:** `NativePodRun.InReach` reads the record's yaw in degrees (`Core/NativePodRun.cs:34`), with PodRunChecks at yaws of 0, 90 and 225.
- **The landing field:** the area's slots (`Core/NativeExpeditionGeneratedWorld.cs:99`) become per-run landing spots:
  - seats 0-3 draw four spots from `worldSeed`, 11-15 m from the fountain at the origin;
  - each pod centre at least 4.2 m off every gate axis, so the 4 m lanes stay clear with 1 m to spare;
  - at least 12 m apart;
  - each pod's yaw (degrees) toward the origin, ±30°, seeded;
  - each spot's pod hull (r 1.17 m), its 2.5 m skid apron behind the pod (away from the door) and its capsule clear of everything in A1's hub plan table, by the clearances there, including the rows that A1 has not built yet, so the field does not move when the camp lands;
  - a draw that fails a clearance is re-drawn deterministically from the seed;
  - seats 4-7 take further draws;
  - ExitReach is 0 for now: the slot is the pod centre, and pods are not solid.
- **The other slot users**: PvP free-for-all and team spawns (`GeneratedWorld.cs:119-120`) keep today's row points as their own table, so PvP on this map is unchanged. Respawn pads, `CompleteAreaLoad`, field revive, reprint, the loop reset, `ForRecipient` (slots[0]), the final-gate rig (faces slots[0]) and the nav origin (slots[0]) move with the slots. Tell Claude if you choose otherwise.
- **The opening:** `ExpeditionRules.PodOpening`, replacing the literal `Time + 3` at `Core/NativeMatch.cs:84`. PhaseEnd = FightAt = Time + PodOpening. 5.0 s for generated hubs if Q22 approves, otherwise 3.0. Other modes keep 3 s.
- **The GO snap:** SPAWN-IN option 1 (`docs/SPAWN-IN.md:94-98`): ground the spawn centres (floor + `HalfHeight + Skin`), and run `ProbePilotGround` before the non-fight early return in `NativeArena.Step` (`Runtime/NativeArena.cs:117,124`), so `Actor.Grounded` is true through every countdown. It removes the countdown "float" in every mode.
- **A native sim benchmark** on today's map with 4 pilots and 88 zombies: nav ms, crowd-flow ms, perception and physics per tick. Record it in the budget table in `docs/EXPEDITION-DEPLOYMENT.md`, and rerun it on every blockout change.
- **A DEV load harness** (Editor and DEV builds only, never in a release): a DEV lobby-count override for the hub cap (`Core/NativeExpeditionHubRules.cs:55`), and up to three stand-in members that BoZo walks along scripted routes, so a 4-pilot, 88-zombie frame can be measured on one PC. Parties are 1-4 signed-in accounts with no bots (`Core/NativeExpeditionParty.cs:8,15`).
- **Create `NativeExpeditionHubPlan`** (shared Core) with the field's rules and `ExitReach = 0`.

**Contract:**
- Anchor, slot and pod-record yaws are in degrees; PveMachine yaw stays in radians.
- One world Version bump (your string), because the slots are in the digest.
- The pod record is unchanged.

**Checks:**
- **Re-pin:** PodRunChecks:21,23 (the opening) and the yaw cases; `Editor/NativeGeneratedWorldBuildChecks.cs:120` (phaseEnd and fightAt) and :136-141 (Hulk routes from slots[0]); ReviveMachineChecks:195,201 (clearance against the new spots).
- **Add:** a landing-field check: over 2,000 seeds every draw passes Validate and ValidateGeometry, holds the spacing and the clearances, and never stands four pods in a row.

**Claude delivers:** GeneratedFrameChecks and the frame-time captures in the same table; the presentation's own degree fix; the slot keep-out re-pin (`Tests/GeneratedDistrictChecks.cs:99`); and, in the same release, the full crash on these spots (if your grounding fix slips, Claude suppresses the GO Land take on pod runs instead).

**Done when:**
- four pods land at least 12 m apart, in a different figure every run;
- there is no Land take or `move.land` at GO, on host and guest;
- the benchmark and the harness work.

## A1. The camp pass: ExitReach, solid pods, hub blocks, camp machines, the prop pass (M3)
**ExitReach and solid pods:**
- `ExitReach = 2.4` m. slots[i] = podCentre(spot) + forward(yaw) × ExitReach, facing the origin. The spots do not move; the slots move to the ramp foot.
- **Pod colliders:** runtime colliders on host and guests, built from the pod records while a pod stands, removed at launch (LaunchGone), on expedition pod runs only.
  - Shape: an octagon of two crossed 1.96 × 2.85 × 1.96 m boxes, centred on record − forward(yaw°) × ExitReach.
  - They are not generator blocks, so empty spots never hold invisible walls.
- **Boarding:** InReach measured from the door point (record − forward × 1.1 m, which is pod centre + forward × 1.3 m), within 1.5 m, on the ramp side (forward offset from the door point ≥ -0.2 m). A pilot at the door, on the ramp or at its foot can board. Pin it in PodRunChecks at yaws of 0, 90 and 225.

**Hub blocks,** from `NativeExpeditionHubPlan` (x east, z north, y up, in metres; fence inner faces at ±24.8; blocks axis-aligned). Claude delivers the final table, checked in the DEV preview, before you start. The proposal:

| Id | Where (x, z) | Size (x × y × z) | Notes |
| --- | --- | --- | --- |
| hub-fountain | (0, 0) | 3.6 × 0.9 × 3.6 | Square basin |
| hub-canopy-n, -s | (0, ±3.4), y 2.6-2.8 | 9.2 × 0.2 × 2.4 | Ring roof, open over the fire |
| hub-canopy-e, -w | (±3.4, 0), y 2.6-2.8 | 2.4 × 0.2 × 4.4 | |
| hub-canopy-post-0..3 | (±4.4, ±4.4) | 0.3 × 2.6 × 0.3 | |
| hub-post-0..3 | (0, 3.4), (3.4, 0), (0, -3.4), (-3.4, 0) | 0.9 × 1.4 × 0.7, long axis round the ring | Benches with seated survivors; 1.2 m headroom under the canopy |
| REVIVE-O-MATIC | (-13.5, 13.5), yaw 135° | Unchanged | Keeps E |
| hub-kiosk | (16.4, 16.4) | 2.6 × 2.6 × 2.6 | Behind the chip machine |
| hub-awning-post-0, -1 | (14.6, 12.4), (12.4, 14.6) | 0.3 × 3.4 × 0.3 | Flank the chip machine |
| hub-infirmary | (-16.9, -16.9) | 3.0 × 2.6 × 3.0 | Behind the med machine |
| hub-lookout | (13.5, -13.5) | Base 3 × 9 × 3; cabin 3.6 × 2.6 × 3.6 on top (y 9-11.6) | |
| hub-searchlight | On the lookout roof, y 11.6-12.8 | 1.2 × 1.2 × 1.2 | |
| hub-sentry-n, -e, -s, -w | (-11.5, 23.8), (23.8, 11.5), (11.5, -23.8), (-23.8, -11.5) | 2.0 × 5.2 × 2.0 | Just beyond each gate's leaf pocket |
| hub-radio | (13.0, 23.9) | 4.0 × 3.3 × 1.8 | Flush with the north fence |
| hub-supply | (-12.5, -23.8) | 3.0 × 3.3 × 2.0 | Flush with the south fence |
| hub-generator | (24.3, -12.5) | 1.0 × 1.4 × 2.0 | Flush with the east fence |
| hub-sandbag-* | Two per gate, beside the lane, outside the 12 × 12 sensor; for example (±7.5, 20.5) | 2.4 × 1.0 × 0.6, long axis along the fence | |

The war board (x 15.4-19.0) and the MISSING board (x -13.0 to -16.5) are Claude's flat art on the north fence, outside every leaf pocket.

**Clearances** (Validate; edge to edge):
- every pod hull, skid apron and capsule at least 1 m clear of every hub block, the machines' solids and their use, dispense and chip-drop points, and the REVIVE-O-MATIC's solids and mat;
- every block outside the gate interact (10 × 10) and sensor (12 × 12) volumes;
- **every block outside each gate's leaf travel and pockets, with the top track:** 5-10.3 m along each gate, from the fence's inner face (±24.8) to 0.5 m inboard, 0-3.4 m high. The metal leaves roll 5.08 m into pockets 0.21-0.39 m inside the gate's plane (±25), against the fence's inner face, under a track at 3.3 m out to 10.2 m (`Core/NativeExpeditionHub.cs:178-180,300-302`);
- 4 m lanes clear from each gate inward to r 9;
- a walkway at least 2.5 m wide round the mess ring;
- **the fence vault:** within 3 m of the fence, every block is either 1.6 m tall or less, or at least 3.2 m tall, so no hub block lowers what it takes to vault the fence (today two air jumps). Whether the fence itself gets the closed gates' no-vault rule stays your call;
- **survivor headroom:** no post top with a figure on it has 1.7 m or more of headroom (the pilot capsule, `Core/NativeLocomotionRules.cs:8`);
- Hulk clearance on the lanes and round the camp, so the graph stays connected from every slot to every gate.

**Machines:** two PveMachine records in the generated hub area.
- **m 0:** `exp-city:hub-machine:chip`, kind chip, at (13.5, 0, 13.5), yaw 3.927 rad.
- **m 1:** `exp-city:hub-machine:med`, kind med, at (-13.5, 0, -13.5), yaw 0.785 rad.
- **Both:** front 0.7, dispense 1.3, chipDrop 2.2.
- **Gates:** lift the host's build gate (`Runtime/NativeExpeditionM1Arena.cs:110`) and serve gate (`:188`) for the hub.
- **Prices:**
  - chip: CachePrice(mutators, min(350, 175 + 25·bought)), with `bought` counted run-wide, so hub purchases climb the same ladder;
  - med: $75, refused at full HP, 12 s recharge, 4 pulses of 12.5% max HP, 0.5 s apart;
  - no recruit in the hub.
- **Revive clearance** (ReviveMachineChecks:195,201) against the machines, blocks and pods.
- **Transfers:** decide how heal and row state survive transfers and host migration.

**The prop pass on today's grid:**
- static collision blocks with stems `lamp-`, `bollard-`, `car-`, `bus-`, `barrier-`, `bench-`, `shelter-`, `barrel-`: 0.3 m lamp posts about every 20 m along the streets, and a few props per district;
- clear of spawn exits (6 × 6 m), routes, and the door, join and airlock volumes; Hulk clearance and nav kept;
- the block bound at `Tests/ExpeditionDeploymentChecks.cs:150` raised from 500 to 700 by measurement (about 600 expected with the camp).

**Optional:** crash noise the horde hears (HubNoise ignores sanctuary positions today).

**Contract:**
- **Version and digest:** a world Version bump (your string). The digest additionally hashes the area's machines (id, kind, x, y, z, yaw, front, dispense, chipDrop); the new blocks and slots are already hashed as blocks and slots.
- **Records and ids:**
  - the pod record is unchanged;
  - machine ids are distinct from the legacy `exp-city:machine:0`;
  - survivor structures and posts are blocks; their figures are Claude's and never replicated.
- **Release coupling:** ships in the same release as Claude's machine draw flag, labels and guests' reach test. Otherwise guests see machines the host never serves.

**Checks:**
- **Re-pin:**
  - PodRunChecks (boarding from the door and the ramp);
  - `Editor/NativeGeneratedWorldBuildChecks.cs:136-141` (Hulk routes);
  - MachinePlacementChecks:146 (the exact text at `M1Arena:110,:190`);
  - ReviveMachineChecks:169-173,181-201;
  - `NativeExpeditionActivationBuildChecks.cs:171-172` (guest machine parity) and :194 (each area's wire under 1 MiB);
  - ExpeditionDeploymentChecks:150 (the bound).
- **Add:**
  - a hub machine suite modelled on InstanceMachineChecks;
  - a native hub purchase in `NativeExpeditionM1Validation`;
  - activation parity for the hub;
  - HubPlan checks: every row valid and clear, the leaf pockets and the fence-vault rule included.

**Claude delivers:**
- the hub plan table first;
- then, in the same release:
  - the camp art and survivors;
  - the climb-out to the capsule at the ramp foot;
  - the machine dressing, draw flag and labels (the reach test shipped in M2);
  - kit dressers for the prop blocks, with Toon Harbor post heads on the lamp posts;
  - the kit keep-outs;
  - the sanctuary rule widened for exactly these ids.

**Done when:**
- nothing on the plaza can be walked through;
- no ramp, skid apron, machine use point, gate volume or leaf pocket overlaps another;
- host and guests serve the same machines;
- a pilot can board from the door and the ramp;
- a Windows host and guest run passes, including the 2026-11-02 boarding review.

## A2. Caches: seeded spots, a solid chest, instance candidates, tiers (M3)
**Spots:**
- A seeded spot pool from worldSeed, re-picked every run and every reroll (not every restock), replacing `n%7` (`Core/NativeExpeditionGeneratedWorld.cs:110-115`).
- **Where each can stand:**
  - in a facade niche or an alley mouth, back to a wall or kerb;
  - facing the street: the anchor's yaw in degrees is the chest's front;
  - the interaction at anchor + forward × 0.9 m, range 2.0;
  - at least 40 m apart;
  - at least 8 m from spawn exits, doors, door approaches, join circles and the final court;
  - Hulk-supported and reachable.
- **How many:**
  - 4 on today's grid: one per district and one neutral;
  - 5 on blockout-4, including one on a roof plaza reached by a stair-street or ramp (a horde connector).

**Collision:** a solid block of 0.92 × 0.62 × 0.58 m, centred on the anchor and turned by its yaw. It is hashed. Claude's steel locker appears only where this block stands.

**Instance candidates** (exp-layout-8):
- 2-3 designed candidates per cache, for Ward Zero, Central Station and the Ironworks;
- they go in the design scripts (`docs/arenas/scripts/expedition_ironworks.py` CACHES at :468-473, `expedition_central_station.py`, `expedition_layout.py`) and the fixture `Tests/expedition-live-layout.json`;
- regenerated with `tools/generate_expedition.py`;
- one picked per run by seed;
- offers and anchors paired per area (`Core/ExpeditionAreaProjection.cs:23-27`).

**Tiers,** if the owner approves Q26:
- `ExpeditionChestOffer.tier`: standard, rare, epic or legendary; missing reads as standard.
- It is carried through validation, the view, JSON, the host handoff and the legacy savepoint.
- It is rolled at SetWorld and at each restock (60/28/10/2%), from the run's economy RNG on the host.
- Prices: $100, $175, $275, $400. WINDFALL ×0.5 and BROKE ×2.
- Offers come from the matching offerWeights row.

**Also check:** whether boss "EPIC OR BETTER" picks leave out mythic chips (`NativeExpeditionIntegration.cs:96`, `NativeExpeditionPick.cs:46`).

**Contract:**
- The open moment stays "the generation changed with the cooldown still ahead".
- The buyer and the offers stay private (`NativeExpeditionView.Leaks`).
- It ships in A1's Version bump.

**Checks to re-pin:**
- `Runtime/NativeExpeditionM1Validation.cs:39-45`;
- ExpeditionDirectorChecks:40-41, ExpeditionIntegrationChecks:48 and ExpeditionMutatorChecks:56;
- ExpeditionLayoutChecks:130,238, `Tests/test_expedition_layout_draft.py:129`, ExpeditionCityChecks:190,272 and ExpeditionIronworksDraftChecks:132;
- `NativeExpeditionActivationBuildChecks.cs:164` (Level 0 and the Poolrooms keep an exit cache);
- the builder's Hulk routes.

**Claude delivers:**
- in M2, on today's hologram: the text, F prompt, J panel fix and loot moment, STANDARD only;
- in M3: the Civil Defence locker, sculpted in code, on your blocks, with the tier liveries and the livery sweep.

## A3. The tileset registry and its choice (M3, or M4)
**The registry:** `NativeTilesets` (shared Core). You own the rules fields:
- generator and its Version;
- look id;
- bank families;
- pool [{instance, theme}];
- hub enemy table;
- mutator pool (which mutators a tileset can roll);
- finalBosses;
- ambience;
- relief profiles;
- state (live or dev).

Claude owns the display fields (name, place, accent, emblem, board row) and creates the file in M1 with City's. City is `id=city`.

**State and intents:**
- `ExpeditionDeploymentState.tileset`, a string; null reads as city.
- SelectTileset(id): host-only; it clears the instance and mutator rolls. Add RollTileset only if the owner asks for a RANDOM lever.
- The revision stays monotonic across a tileset change. Update the revision rule at:
  - `NativeExpeditionDeployment.cs:154`;
  - `ExpeditionRunBundle.cs:47-49`;
  - `SameDeploymentTicket` (`Runtime/NativeExpeditionDeploymentRoom.cs:22-25`);
  - the receiver (`ExpeditionDeploymentRoom.cs:112-120`).
- **`worldSeed`** (`Core/NativeExpeditionDeployment.cs:101`) mixes in the tileset id only when it is not `city`, so every City seed, the builder's 8 layouts, the seed-pinned suites (GeneratedDistrictChecks and ReviveMachineChecks roll at revision 0) and the captured seeds stay as they are.

**Ticket and compatibility:**
- Ticket schema 2, with `tileset`.
- Compatibility and GeneratorVersion hash every live tileset's pool.
- deploymentProtocol goes from 2 to 3, batched into M3 (or M4) and coordinated with `codex/steam-player-hosted`.

**The bundle:**
- it picks the pool, generator, hub layout, base horde content, hub enemy table and mutator pool by tileset;
- its cache is cleared when the tileset changes;
- DeployInput exposes the tileset and its pool (Catalog is private today).

**The City-only sites:** each keyed by tileset in A3, or deferred with an owner.

| # | Site | Where | Disposition |
| --- | --- | --- | --- |
| 1 | Pool themes, and the appended Backrooms and Poolrooms | `Core/ExpeditionDeploymentRoom.cs:95-96` | A3: from the tileset's pool |
| 2 | The reel list | `Runtime/NativeDeploymentLive.cs:9-11` | A3: from DeployInput's pool (your glue) |
| 3 | The spawn allow list | `Core/NativeExpeditionGeneratedWorld.cs:107` | A3: from the tileset's hub enemy table |
| 4 | The hub kind table | `Core/NativeExpeditionHubRules.cs:65` | A3: keyed by tileset; A5a adds district and band |
| 5 | Base horde content, host and guests | `Runtime/NativeExpeditionM1.cs:36`, `Runtime/NativeNetworkFrame.cs:156` | A3: the tileset's content rows |
| 6 | Map registrations | `Core/NativeUnityMaps.cs:53`, `KnownDestinations`, `NativeExpeditionJoinZones.Known` | Deferred to the first real tileset (you): its map ids register there |
| 7 | The kit's looks | `NativeExpeditionCityKit.LookOf/Active` | Claude: keyed by the tileset's bank families and look |
| 8 | Map id and look | `Core/NativeExpeditionGeneratedWorld.cs:24,117` | A3: from the registry's generator and look |
| 9 | The revive machine's spot, by map id | `Core/NativeReviveMachine.cs:306-311` | Deferred: a new tileset's hub plan names its spot (Claude proposes, you validate) |
| 10 | The final boss's seat and ids | `Runtime/NativeExpeditionSplitRuntime.cs:45`, `Runtime/NativeExpeditionM1Arena.cs:260-264` | A3: from the tileset's finalBosses |
| 11 | Zombie-only mutators (HULKAMANIA, SPRINTERS, BLOATED) | `Core/NativeExpeditionMutators.cs` | A3: from the tileset's mutator pool |

**Proof:** a DEV-only proof tileset that differs from City in its hub enemy table, one mutator (left out of its pool) and its final-boss id (a DEV alias). A check pins that the bundle takes all three from the registry. Players see CITY alone.

**The recipe:** a one-page "Adding a tileset" doc, with Claude: everything a new tileset needs (registry row, generator and Version, look, bank families, pool, hub enemy table, mutator pool, final bosses, ambience, relief profile, board row and emblem), with the proof tileset as the template to copy.

**Checks:**
- ExpeditionDeploymentChecks (revision, ticket JSON, receiver);
- a pin that City's `worldSeed` is unchanged for a given seed and roll count;
- the proof tileset's pin;
- the builder's RunDeploymentAcceptance;
- an old client refused cleanly;
- host and guests see the same flaps.

**Claude delivers:**
- the ATLAS board (live from M1, its rows drawn from the registry's display fields, so a new row needs no new code);
- the pick animation (and RANDOM, if asked), and the reels re-skinned from the chosen pool;
- board sounds from Claude's own table, so `NativeDeploySounds` needs no change.

## A4. Blockout-4: the layered city and the City Layout Contract (greybox from M1; release M5)
**First:** a DEV-only greybox switch, solo, with Claude's kit fallback, at both N2 sizes, so the owner can walk them before any layered art. The generator takes the size as a parameter.

**Size** (option A; option B scales it): 420 × 340 m: 42 × 34 cells of 10 m; bounds ±210 × ±170; ceiling 60; killY -25.

**Tiers** (TerraceStep 7 m):

| Tier | y | What is there |
| --- | --- | --- |
| The Crag | +14 | City Hall and the final court, north |
| The Knoll | 0 | The sanctuary, unchanged at the origin, and Belvedere Park |
| The upper town | -7 | Ward Zero's ridge spur, the Slag Terrace, the viaduct deck |
| The lower town | -14 | The main streets, Level 0's flat plain, the Ash Hollow |
| The Basin | -17.5 | The Poolrooms' plaza |
| The Cut | -21 | The station's tracks |

**Relief profiles by theme** (generation data):
- Level 0: dead flat, with uniform lot heights in a perfect grid;
- the Poolrooms: the Basin;
- Ward Zero: a walled ridge spur at the top of a straight boulevard;
- Central Station: the Cut, plus the viaduct;
- City Ironworks: the Hollow, plus a haul ramp up to the Slag Terrace.

**Lots:**
- 1×1 to 3×2 cells, with 0 or 2.5 m setbacks: alleys of 5 m, streets of 10 m, avenues of 15-20 m;
- heights of 4 + 3n m (7-22 m), and towers of 25-60 m;
- along sloped streets, buildings stand on stepped plinths.

**Roofs:**
- walkable: flat, with parapet blocks at least 1.0 m tall;
- pitched: ramp wedges, which Claude's art follows within 0.25 m;
- spires, tanks and signs on reachable roofs are blocks, or rise above the ceiling;
- no crown kind, and no pilot column over a building: retire today's 40 m columns (`GeneratedWorld.cs:122`).

**Pilot columns** only on the map edge: the Cordon's top, and the tops of the halls and the final court, which stand on the Cordon line.

**Connectors:**
- **Horde connectors,** on the horde's graph, Hulk-sized (at least 3 m wide, 3.2 m headroom):
  - sloped streets: MapRamp 'ramp', 1:8 to 1:6, for most tier changes;
  - stair-streets: MapRamp 'stair', 26.6°, a landing every 3.5 m of rise;
  - haul ramps: MapRamp 'ramp', 1:4 or gentler, at least 6 m wide;
  - the Hundred Steps: the north gate to the Crag, a 14 m rise;
  - the car park's ramps and decks (decks 3.5 m apart), and passages under bridges and the viaduct.
- **Pilot routes,** off the horde's graph: fire escapes, ladders, rubble ramps, narrow footbridges, and roof gaps (about 4 m for a running jump, 6 m for a sprint jump, wider only for chips or bridges).
- Per-link size flags, or a second graph for ordinary zombies, are your option later, with their own node and CPU budget.

**Structures,** as blocks:
- the viaduct deck (about 200 m);
- footbridges over the Cut;
- sky-bridges;
- pipe catwalks;
- a three-deck car park;
- the Cordon: a 14 m wall on the bounds, with watchtower blocks every 60 m and a pilot column only above its top.

**Props** as thin and solid blocks, with stems lamp-, bollard-, tree-, car-, bus-, bench-, bin-, barrier-, shelter-, crate- and barrel-:
- clear of the keep-outs: spawn exits 6 × 6 m, routes, doors, joins, airlocks;
- per district: ambulances near Ward Zero, a derailed tram near the Station, slag carts near the Ironworks, filing cabinets near Level 0, loungers near the Poolrooms;
- lamp posts about every 20 m along streets.

**Support and navigation:**
- Supported, Clear and Validate generalised to y: support comes from any block top or ramp surface.
- **A spatial index** (cell buckets) for Supported and Clear. Today both loop over every block for every candidate (`GeneratedWorld.cs:152-158,183-186`); at this size that is about 10^8 box tests per layer, on every peer, because guests rebuild the world from the ticket.
- Multi-level nav: 2 m nodes on walkable surfaces with layer ids, and walk, stair, ramp and one-way drop links.
- **Roof islands:** pilot-only roofs get their own nav, their hatches and one-way drop links to the street.
- **A validation-only pilot graph,** for reachability through fire escapes and jumpable gaps.
- Raise the node bound by measurement. Crowd flow may be local to the pilots.

**Spawn entries,** existing kinds only (door, vent, drop, breach):
- placed on shopfront doors, ground-floor windows, manholes, roof hatches, parapets (drop entries, with `height` above the landing) and breach panels;
- **a roof hatch on every walkable roof without a horde connector,** so no roof is a safe spot;
- `surface` (or a new hashed field) names which; `level` is the nav layer;
- at least 20 m from door approaches;
- each opening reserved in `openings[]`.

**Routes and influences:**
- Rings retuned by walked distance.
- `Hub.Routes` Via points with y. Routes run 150-190 m from the gates to the halls (option A).
- Influences retuned: share at least 0.70 within 30 m of a hall door, about 0.45 at 60 m, about 0.25 at 110 m, under 0.10 at 160 m. The sanctuary stays 100% neutral.

**Records:** features (dark, flood, rail, hang, wall, cut), landmark slots, and the layout below.

**The contract,** `NativeCityLayout` (Core, pure):
- It is versioned with the world Version, attached to the generated world, and every field is hashed.
- Claude draws exactly what it says, as the Backrooms layout works today.
- Claude's `FromBlockout3` adapter (M3) expresses today's world in it until you fill it natively.

```
CityLayout {
  version; cellMetres = 10; terraceStep = 7; minX, minZ, cellsX, cellsZ
  cells[]     { x, z, tier, use(street|plaza|lot|prefab|hub|court|cut|void), lot, influences[4] }
  lots[]      { id, x0, z0, x1, z1, tier, height, roof(walkable|pitched), setback[n,e,s,w], district, hero, seed }
  links[]     { id, kind(slope|stair|ramp|bridge|drop|fire-escape|ladder|rubble), horde, a{x,y,z}, b{x,y,z}, width, headroom, blocks[] }
  prefabs[]   { id, kind(carpark|viaduct|gantry|skybridge|aqueduct|ceiling|cordon-tower), x0, z0, x1, z1, turn, tier }
  landmarks[] { id, district, kind, x, y, z, yaw, sx, sz, height }
  features[]  { id, kind(dark|flood|rail|hang|wall|cut), volume | segment, depth }
  props[]     { block, stem }
  openings[]  { block, face, u0, u1, y0, y1, entry }
}
```

- `horde` is true when the link is on the horde's graph. Slopes, stairs and ramps are also MapRamp records; drops are also navHints.drops.

**Block id prefixes by role:** terrace-, retain-, slope-, lot-, stair-, ramp-, bridge-, viaduct-, cut-, cordon-, crag-, landmark-, prop-, roof-.

**Version and digest:**
- Version `deployment-world-blockout-4`.
- The digest adds MapRamp records, link kinds and horde flags, node layers, pilot columns, every CityLayout record and the new spawn fields. Today it hashes none of these.

**`NativeCityBleed`:** A4 retunes the influences it reads; its thresholds stay versioned with the world Version. You own the whole function from A5a.

**PilotReach:** publish the reach envelope (rise and distance) for the worst live stack, for traversal design only. No rule or check relies on it for honesty.

**Budgets:**
- count blocks, nodes per layer and links on the greybox, and write them into this hand-off's budget rows as the new bounds (about 2,000-3,000 blocks and 20,000-30,000 nodes are expected for option A);
- generation 1.5 s or less on a worker, measured on the slowest supported guest machine;
- the sim benchmark within the A0 baseline + 25%;
- the area wire under 1 MiB.

**Checks:**
- **The sweep:** 2,000 seeds through Validate and ValidateGeometry.
- **The native gates:** 8 layouts, Hulk routes over every horde connector, and the 1,800-tick fixture.
- **Reachability:** every walkable surface, roof plaza and bridge is reachable by a pilot without chips (the pilot graph); every walkable roof without a horde connector has a hatch.
- **Re-pin:** ExpeditionDeploymentChecks:52-61 (the entrance share) and :150 (the measured bounds); the station holo map and the skyline bounds (Claude's, sized from the map).

**Claude delivers:**
- **Before you start:**
  - the world brief: this package, plus landmark footprints;
  - the art keep-outs: reserved openings kept clear, parapets as blocks, the 0.25 m and 3.1 m rules;
  - a DEV top-down roll viewer at the new size;
  - the kit fallback dressing the new block roles, by 32 m chunk.
- **After:**
  - the layered bank;
  - the landmarks, in a far layer that reads through the fog;
  - the light field v2 and the rebuilt light volume;
  - the relief bleed's dressing.

## A5a. District kinds on today's grid (M4)
**Kinds by district:**
- `ExpeditionHubRules.Kind(ring, kind, elapsed)` (`Core/NativeExpeditionHubRules.cs:65`) gains the district and band at the spawn exit, through `NativeCityBleed`, which you now own. Set today's grid's thresholds so instance kinds start about 20-60 m from each hall door (today's street spawns start about 20 m out).
- Instance kinds are allowed only at Warp or above.

**Plain kinds,** each only where it works off its home map (they bind only on their own maps today, `NativeZoneCombat.Bind :29-35`):
- Level 0: Hounds, in packs of 3;
- the Poolrooms: Tilers, if they can anchor on today's building faces; otherwise on blockout-4's wall features, in A5b;
- Ward Zero: a spitter-heavy street mix;
- Central Station: Conductors and Pileups;
- City Ironworks: Cinderlings, Stokers and Slagbacks.

**Caps:**
- at most 25% of spawns in warp, and 40% at threshold;
- at most 6 instance-kind minions alive per district;
- none from a cleared instance (`ExpeditionView.instances[].cleared`).

**Network:**
- content rows bound on host and guests (`M1Arena:108-109`, `NativeNetworkFrame.cs:156-161`);
- caps on the city map (`Runtime/NativeExpeditionZoneEnemies.cs:12`);
- the Zone frame sent on the city map (`NativeNetworkFrame.cs:131`), with a protocol bump batched into M4.

**Also:** rewards by theme, and a difficulty and CPU review with the owner. If the CPU review fails, the fallback is district clothes only (Claude's, already live from M2).

**Contract:**
- Kind ids are unchanged and append-only (`Core/NativeHordeKinds.cs:26-44`).
- The Zone frame's fields are unchanged; only where it is sent changes.
- The minion frame and HubZombieBrain view states are unchanged.

**Checks:**
- HordeKindChecks;
- the zone combat checks;
- HubPerceptionChecks;
- ExpeditionHubAuthorityChecks:65-77, re-pinned;
- the native activation fixture (1,800 ticks, four areas);
- frame sizes within `NativeFrameBudget`;
- a measured four-pilot run, with A0's harness.

**Claude delivers:**
- the instance kinds' looks loaded on the city map when their district is rolled;
- their sounds and their night readability;
- a DEV_LOG note: no change to hit timing or hitboxes.

## A5b. The rooftop horde and feature-gated kinds (M6-M7)
**Rooftops** (on blockout-4):
- roof perches: a percher is Idle on a roof entry;
- roof-hatch door entries;
- parapet drops through `NativeHordeDrop`, with height from the entry, plus a lighter profile for runners if you add one;
- breaches, including Ward Zero's quarantine panels;
- Slagbacks and brutes up the Ironworks haul ramp.

**Feature-gated kinds** (M6 onward; the last in M7):
- the Smiler, in dark features;
- the Drowned, in flood features;
- the Undertow, in the flooded Cut;
- the Linesman, on rails;
- the Rigger, on hangs;
- the Faceling, on walls.

**Contract and checks:** as A5a, plus the horde reaching roof players by horde connectors and hatches, and leaving the roofs by drops.

**Claude delivers:**
- perchers crouched on the parapet, instead of the ink tear;
- perch idles;
- breach panels styled per district;
- the threshold creatures' dressing.

## Shared constants

| Constant | Value | Owner |
| --- | --- | --- |
| ExitReach | 0 until A1, then 2.4 m | Shared, in `NativeExpeditionHubPlan`; yours after A1 |
| The landing field | Four spots per run, 11-15 m out, 4.2 m off the gate axes, 12 m apart, yaw ±30° | Yours (A0) |
| ExpeditionRules.PodOpening | 5.0 s, generated hubs only (3.0 if the owner says no) | Yours (A0) |
| TravelAt | 5.6 s (4.0 today); the host already follows it | Claude |
| Band thresholds | Layered city: 0.10, 0.25, 0.45, 0.70; today's grid: set in A5a; versioned with the world Version | Claude's provisional values in M2; yours from A5a |
| TerraceStep | 7 m | Yours (from Claude's brief) |
| PilotReach | An envelope for the worst live stack, for traversal design only | Yours |
| Look `expedition-night` | Resolved by map id | Claude |

## Release coupling
- **M1:** A0, together with Claude's ATLAS board, eject, cradle card and full crash on the landing field. One world Version bump.
- **M2:** nothing of yours. Claude's night look, light packs, first bleed pass, camp fence and gates, cache text and the machine label and reach fixes ship alone.
- **M3:** A1, A2 and possibly A3, together with Claude's camp, survivors, machine presentation, climb-out to the ramp foot, steel lockers, liveries, hall fronts and prop dressers. One world Version bump, and deploymentProtocol 3 if A3 rides along.
- **M4:** A5a (and A3 if it missed M3), together with Claude's modelled streets, set pieces and the district kinds' looks. The expedition protocol bump for the Zone frame.
- **M5:** A4, together with Claude's layered bank, landmarks, field v2 and light volume.
- **M6:** A5b, together with Claude's perchers, breaches and threshold set pieces. Any protocol bump batched here.
- **M7:** A5b's last feature-gated kinds, together with Claude's final court and the Low-tier pass; a 4-player Windows run.
- **Every release:** log the compatibility break (older test clients are refused) in the DEV_LOG and in the release receipt.

```
===== END HAND-OFF TO ASTRA =====
```

---

## 9. What the review changed

All 39 findings were accepted; none was judged wrong. One fix, the look board, needs an Editor run, so it is scheduled as the first step after approval. The main changes:

- **Release order** (owner intent, completeness): the drop and the ATLAS board lead (M1), as the owner's "First" and "now" asked. The landing field moved into A0, so no crash ever ships in today's 2 m row. Night moved to M2.
- **Reach** (feasibility, high): air jumps, Skybound and MOON BOOTS reach any roof. The crown roof kind, the out-of-reach survivor spots and the rooftop lookouts are gone; every roof is drawn as its collider, survivors stand behind glass, mesh or hatches or under low roofs, and pilot-only roofs get hatches.
- **Light packs** (owner intent, high): Toon Harbor's licence is on file, so its lights ship through the Drive zip; VLB has a package (the bridge and three hooks); Q15 asks the sharp question about POLY and Synty.
- **Gate pockets:** the hub plan was redrawn round the leaves' travel, pockets and top track, which are now a clearance.
- **The horde's reach:** horde connectors are Hulk-sized; fire escapes and ladders are pilot routes answered by hatches; the car park's decks are 3.5 m apart.
- **Wayfinding:** a far layer for landmarks, beams and glows, with its own fog curve, the far ring in the sky pass, and LandmarkReadChecks.
- **Budgets:** absolute frame targets per tier with milestone allocations; A0's DEV load harness for 4-pilot numbers; block, nav, kit-fallback and light-volume rows; a spatial index for generation; route times corrected to 19-25 s.
- **Caches:** the steel locker waits for its block (M3); M2 ships the text and loot moment on the hologram; cartridges dock as pending, because the chip is chosen later with C.
- **Enemies near instances:** plain district kinds on today's grid in M4 (A5a), not M6; rooftops and feature-gated kinds in A5b.
- **Props and street lights:** a prop pass on today's grid in A1 (M3); the lamp count rises against today's.
- **Survivors:** they work the real interactions, say look-at lines, wear the camp's look, and avoid enemy names; Q18 goes to the owner.
- **Scale and hills:** N2 offers two sizes with their costs and an anchor; most tier changes are sloped streets.
- **The bleed:** warp and threshold use each instance's own materials; the street trail points home only; an optional creep with run time.
- **R1 readiness:** the eleven City-only sites have dispositions, the registry gains a mutator pool, the proof tileset really differs, board rows come from the registry, and A3 ships an "Adding a tileset" recipe. City's `worldSeed` is unchanged.
- **Smaller fixes:** boarding measured from the door; PvP keeps its own spawn rows; `NativeCityBleed` written by Claude in M2 with the grid from the blueprint and owned by Astra from A5a; SPAWN-IN option 1 made required for a GO with no snap; light field v2 as two stacked ranges; wet-street streaks as FX quads; the light volume rebuilt for the layered city; manholes drawn outside the kit; a generator block; the mast beyond the map edge; barrel fires against walls; all five rescued survivors placed; W12 captures and a loudness table; late-joiner and killcam captures; the superseded owner decisions listed; the hand-off's END marker and full release coupling.
- **Not done in this step:** the look board. This plan was written from a read-only checkout with no Editor run, so the board is the first thing after approval, before any look code merges.

---

## Appendix: the owner's summary (as approved)

### Expedition overhaul: the plan in brief (2026-11-18)

## The idea
The city becomes Meridian, a hill city where every clock stopped at 6:47, the night the Siren got out. It has been night ever since. You pick the tileset on a railway departures board at the station. Your pilot falls through a tube into their pod's seat, and the pods crash down all round a survivors' camp on the hilltop. The city below is lit by its street lamps, windows and fires. Walk toward an instance and the streets slowly turn into it. Near Level 0 the ground goes flat, the buildings start to repeat, and an office ceiling hangs over the street. Near the Poolrooms your footsteps echo. Near Ward Zero the lamps turn red. You learn where you are from the world itself.

## What each release gives you on the test channel
There are seven releases, and each one is playable. The first is the drop, because you asked for the pods first and the tileset machine now.

1. **The drop.** The tileset board on the station wall, showing City. Your pilot falls into the pod's seat instead of sinking stiffly. The loading screen is the inside of your pod. The pods crash all round the camp, never in a row, and leave smoking craters.
2. **Night falls.** A dark city lit by its lamps, the Toon Harbor lamps included. Each instance's district stands out by its light, sound and zombie clothes. The camp's fence and gates are properly modelled. Caches get clear price text and burst open when bought. Before the night look is built, you approve it on a board of sample shots.
3. **The camp.** Survivors behind counters and windows who work the machines, open the gates, call out your clears and say a line when you look at them. A chip machine and a healing machine in the camp. Steel lockers in new places every run, with rarity colours. Solid pods that you climb out of. Lamp posts, cars and benches that you bump into. The five instance halls are modelled.
4. **Modelled Meridian.** The box look is gone from the streets. Each instance's streets get their strange buildings, and its creatures start to appear nearby: Backrooms Hounds near Level 0, and the station's and the Ironworks' enemies near theirs.
5. **The layered city.** A much bigger hill city, with sloped streets, terraces, stairs, roofs you can run across, a viaduct and a rail cut. You walk a plain grey version first, to settle the size.
6. **The rooftop horde.** Zombies crouched on the roofs that leap down, hatches on the roofs, walls that burst open, and the finished sound set.
7. **The last climb.** The final boss's approach dressed for its boss, the last creatures, a pass for low-end PCs, and a full four-player test.

## Who builds what
- **Claude** builds what you see and hear: the board, the drop and the crash, the night look and the lamps, the modelled city, the camp and its survivors, the lockers, how the machines look, and the sound.
- **Astra** builds the rules underneath: where the pods land and what is solid, the camp's solid pieces, the camp machines and their prices, where the lockers go and their rarity, the tileset choice, the bigger city's ground, paths and spawns, and the instance creatures out in the streets. Astra's part is written up as a hand-off for you to pass on.

## Decisions I need from you
My recommendation comes first in each.

1. **How big the city is.** About three times today's area, with about 20-25 seconds on foot from the camp to an instance. Or about six times, with about 30-40 seconds, which costs more speed and more art. Think of a Risk of Rain stage you have in mind, and how long it takes to cross. You walk a grey version of each before any art is made.
2. **Time before you can play.** About 3.5 seconds longer than today, for the fall into the pod and the crash. Or keep today's timing, with the same moments squeezed in.
3. **Light packs.** Ship the Toon Harbor lamps (their licence is on file) and the Volumetric Light Beam pack (you named it on 28 October) to players, through your private asset zip. Add the POLY and Synty lamps only if you confirm their licences cover a released game.
4. **Creatures near instances.** Instance creatures start about 60 m from an instance's door. At most a quarter of the street zombies there are instance creatures, rising to 40% at the door, with at most six at once per district, and none after you clear that instance. Or start them further out, at about 110 m. Or give the zombies the district's clothes only.
5. **Survivors.** They react and help: they work the machines, open the gates, call out your clears and say a line when you look at them. Or also add trading or quests later, which is a bigger rules job for Astra.
6. **Locker rarity.** Four rarities, at $100, $175, $275 and $400, found 60%, 28%, 10% and 2% of the time. Or keep one price and only make the text clearer.

Also from you, when the time comes: approve the night-look board, run one sound batch on your ElevenLabs account as for the Backrooms bosses, and pass Astra's hand-off on whole.
