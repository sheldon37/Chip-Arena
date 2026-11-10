# Substation 128 (`club`): the club that dances

2026-11-10 · Claude (presentation lane) · branch `claude/stage-club` from shared `e6d564dc` (`origin/claude/sleepy-ritchie-g8ruhv`)

Owner request (2026-11-10): four new stages that "set a new bar for quality", each with unique hazards, very beautiful, in
the Random selection. This one is the owner's "dance club that reacts to music (light tiles, light beams)". Working name
"Club Afterglow"; shipped name **Substation 128**: a nightclub built inside a decommissioned electrical substation on the
river, running at 128 bpm. Status: **implemented on the branch, seen in the Editor; not merged, not released.**

## At a glance

| | |
|---|---|
| Map id / look | `club` / `club-substation` |
| Play area | 52 x 40 m (bounds x ±26, z ±20, ceiling 12, killY −6), mirrored across x = 0 |
| Levels | hall 0 m, DJ stage 1.0 m, mezzanines + VIP balcony + lighting catwalk 4.2 m |
| Built for | 4v4 and FFA 8 (Random rotation: yes) |
| Blocks | 237 (20 breakable props at round start), 33 pilot columns, 2,543 nav nodes / 4,321 links (724 gated) |
| Hazards | the charged floor (9 `grid` sections), the laser wall (`maglev`), the bass drop (2 `vent`s with push) |
| Music | the owner's **Neon Substation** (`club-theme`; see "Music") |

## Layout (`docs/arenas/scripts/club_m1.py`)

Run `python3 docs/arenas/scripts/club_m1.py [--out DIR]`: it prints both level plans and its checks and exits 1 on a failure.

- **The hall** (ground, 0 m). The LED **dance floor** (x ±9, z ±7.5, 18 x 15 one-metre tiles) at the centre, under the
  lighting truss (10.4 m) and the hung **video cube** (5.2..8.2 m, solid, out of reach). Truss towers at its four corners
  (solid). Go-go podiums (1.0 m) east and west of it, booths, cocktail islands and cast-iron columns in the side lanes.
- **North: the stage.** The DJ stage (1.0 m deck, front steps and back steps behind each stack), the DJ booth (half
  cover), the 12 m **LED wall** behind it, the two **speaker stacks** (4.6 m, solid) either side. Between the floor and the
  stage the apron, crossed overhead by the **lighting catwalk** (4.2 m, both mezzanines to each other) on two columns.
  Beyond the barrier at the stage's wings, the **crowd pit** (presentation).
- **South: the bar.** The counter (1.1 m, half cover) across the hall, the back bar and a pillar under the **VIP
  balcony** (4.2 m), which two **bar stairs** climb through stairwells.
- **The sides (A west, B east).** Under each **mezzanine** (4.2 m): the **green room** (team spawns), the **cloakroom**
  and the **backstage room**, doors opening only into the cross passages (no spawn sees across the hall). The **grand
  stair** climbs beside each green room. On each mezzanine a **VIP lounge** room (an FFA spawn), sofas and planters, and
  the rail over the crowd pit. High in the side walls, five arches onto the **crowd galleries** (presentation).
- **Spawns.** Team A: four in the west green room; B the mirror. FFA: A1/B1 (green rooms), f/F (cloakrooms), g/G
  (backstage rooms), h/H (VIP lounges, upstairs). All 28 FFA pairs and all 16 A-B pairs are blind under ±1 m jitter and
  eyes at 1.2/1.6/1.9 m; every spawn is 10 m or more from any hazard volume.
- **Checks** (the draft's): spawn sight lines, spacing, hazard clearance, bounds, reachability on a 0.25 m raster from A1
  (every level), each of the 7 flights walkable on its own, no tops in the 1.3-2.2 m band, mirror symmetry of blocks, pilot
  columns, spawns and hazards, the hazard rhythm (below), the laser's jump window, budgets. Longest clear sight lines:
  55 m in the hall, 58 m on the upper level (fails at 60; the Factory ships 75 m lines).

## Hazards (existing kinds only, on a 128 bpm bar grid)

One beat is 0.46875 s, a bar 1.875 s. Every hazard counts bars from the **arm time** (FightAt + 4 s, the Factory clock's
arm delay) on the **match clock**, so host, guests and the killcam agree; no hazard ever reads the music. The phrase is 16
bars (30 s):

| Bar | Event |
|---|---|
| 0 (= 16) | **The drop**: both speaker stacks burst (from the second phrase) |
| 3, 11 | the charged floor's **X** goes live (corners and centre, 5 sections) |
| 7, 15 | the charged floor's **+** goes live (the four edge sections) |
| 8 | **the laser wall** passes |

- **The charged floor** (`club-live-{w,c,e}-{s,c,n}`, kind `grid`, 6 x 5 m sections): charge 6 beats (2.8125 s; the
  shipped minimum is 2.5 s), live one bar (1.875 s), discharge 1 s, every 8 bars; 10 HP per 0.25 s tick (80 HP for a full
  window); never lethal. The X and the + never live together, so a safe square is always one step away; the X's first
  live window comes at bar 11, the +'s at bar 7. **Telegraph:** the section's tiles turn amber and fill toward red under
  scrolling hazard stripes, a red frame round the section, sparks crawling off its edge; live, it is red-hot with white
  veins and arcs; then embers and smoke. The rest of the floor dims while any section warns.
- **The laser wall** (`club-laser`, kind `maglev`, lethal like every maglev): a sheet of three red beams 0.15 to 0.65 m
  over the floor sweeps the whole dance floor along x at 8 m/s (2.3 s), its direction alternating each pass; warned 4 bars
  (7.5 s; the shipped maglev warns 6), danger the last 2 (3.75 s), every 16 bars from bar 8. **Jump it**: a jump keeps the
  feet over 0.65 m for 0.57 s and the wall passes a pilot in 0.14 s (a 0.42 s window), or leave the floor. **Telegraph:**
  the chime, then the alarm; a red line and the start column's tiles glow where it will enter, chevrons point its way;
  the emitter posts rise from their slots; the moving heads all swing red onto its line and chase it; the LED wall shows
  arrows and a red meter. It never passes while a section is live. **Kill:** a level cut through the shins where its beams
  run (NativeBisect's shared split, gore on and stock endings only, as the saws'), the body lifting off its legs and
  thrown along the sweep; the train's smash gib never plays on the club (`NativeSmashGib.Plan`).
- **The bass drop** (`club-drop-w`, `club-drop-e`, kind `vent`, push 7 m/s): the air column in front of each stack (2.8 x
  4 m, 2.6 m tall; hatched on the floor); warned 2 bars (3.75 s; the shipped vent warns 0.8), the burst 0.75 s on the
  phrase's downbeat: 3 ticks of 6 HP, each shoving 7 m/s south (toward the floor). **Telegraph:** the riser; the cones
  pump harder each beat; air rings off the cones on every beat, faster toward the drop; the zone's frame glows amber; the LED
  wall counts down the last four beats; the floor rises from its edges. **Impact:** the boom, a white flash wave rolling
  out from the stage over the floor, shockwave rings, dust, CO2 jets and confetti from the truss (presentation; Reduced
  effects and the Reduced/Calm light-flash settings soften the flash).
- Nothing harms in the fight's first 4 s (checked for every hazard and round). Nav links over the floor and through the
  drop columns are gated by their hazards (724 links).

### For Astra (the one rules change)

`NativeStageHazards.VentShove` and `NativeStageHazardsRuntime.StepAuthoredHazards`: a vent whose authored `push` is above 0
now shoves along its `axis` (made horizontal and unit) by `push` m/s on each damage tick, through `Damage`'s knockback
(`impulse = push x ticks x 65`, `direction = axis`). Before this, `push` was read only by the DEV pulse. Every vent on
every other map has `push` 0, so nothing else changes (ClubStageChecks pins it). The host applies it; guests see the
replicated position, as for every knockback. Checked natively by `NativeArena.ValidateClubRuntime` (the Editor build gate).
Astra may want a lift component or a cap; the club's number is 7 m/s per tick (21 m/s over a full burst), to tune by
playtest.

## Music reactivity (presentation only)

- `Runtime/NativeClubAudio.cs` takes NativeAudio's own **music voice**'s spectrum (`AudioSource.GetSpectrumData`, 512
  bins, Blackman-Harris) each frame the club is on screen and the music plays audibly; only the song is sampled.
- `Core/NativeClubPulse.cs` turns it into kick (onsets in 40-115 Hz over a running average), bass, mids, hats and level,
  each against its own slowly falling peak (any master volume fills the range). With no song heard (none installed, muted,
  the music at 0, silence) it eases over 0.8 s to a **steady 128 bpm clock** on the bar grid.
- The pulse drives only the party: the floor patterns' brightness, the moving heads' breathing, the LED wall's spectrum,
  the cube's chase. The patterns step on the bar grid (match clock), so they never drift from the telegraphs.
- **Photosensitivity** (the hazard kit's rule, `docs/HAZARD-KIT.md` 6): Full pulses on each kick (2.1 a second at 128 bpm,
  under 3); Reduced (the light-flash setting or Reduced effects) soft pulses at most twice a second; Calm a slow 0.3 Hz
  swell. Telegraphs never change with it.

## The look

- **MapLooks `club-substation`:** a cool violet key from high above with soft shadows (the truss casts its grid on the
  floor), a magenta rim from the stage, deep violet ambient, linear fog, the night sky (stars, the moon in the south) seen
  through the great window and the clerestory; five steady practicals (stage wash, bar, two lounges, floor glow) and three
  slots left for the show; the anime style with a strong grade (saturation 1.22, contrast 1.12, violet shade); the
  advanced lighting: the light volume, thick violet haze (density 0.035, the lamps glowing in it at 1.4), volumetric beams
  (gain 1.3) and a **polished floor** (the drizzle weather at amount 0: only its wet sheen, so the lamps streak across the
  terrazzo; no rain falls).
- **Kit** (`Core/NativeClubKit.cs`, ~2,000 NativeBlockLit parts, packed once): the lighting truss (box truss, lacing), the
  towers' lattice, cabinet seams, grilles and corners, the stage's LED lip and matting, the DJ's decks and mixer, the bar's
  brass rail, magenta underglow and back-bar bottles, LED fascias on every deck edge, rail caps and LED lines, the stairs'
  LED nosings, the walls' crane rail, the neon sign (a bolt and "128") over the VIP, the window frame, the arches' trim,
  the columns' plinths and capitals, the podiums' lit rims, the dance floor's LED border, the laser slots and the drop
  zones' hatching. Attached parts stay within 0.25 m of their block; overhead parts 2 m over any standable top.
- **Rig** (`Core/NativeClubRig.cs`, up to 1,126 parts a frame): the 270 LED tiles, the LED wall's 162 panels and the
  cube's 64, the pumping cones, 16 moving heads aimed by the show, the laser posts, two turning mirror balls, the DJ, 64
  crowd silhouettes (32 on Low) bobbing and throwing their hands up at the drop.
- **Show** (`Core/NativeClubShow.cs`): six floor patterns (checker pulse, ripples, sweeping bands, the equaliser, sparkle
  rain, the spiral; every 8 bars, crossfaded), the heads' four figures, the wall's spectrum and countdown, the fans, the
  three lent lights (NativeLighting.KitLights: 3 on High, 2 on Balanced, 1 on Low). Actor lighting is on each frame, so the
  pilots take the slots (the show's colours on the bodies; ZombieLightChecks lists the look). Amber and red are the
  telegraphs' only: the party never plays them (checked per tile). Telegraph reds stay at 1 to 1.6 (the display shoulder
  whitens a brighter red), the bloom carries them.
- **Effects** (`Core/NativeClubFx.cs`, up to ~620 quads): the heads' volumetric beams, gobo spots and flares, green and
  cyan laser fans off the stacks, the mirror balls' pin spots and specks round the hall, the stage lamps' beams up the LED
  wall, the telegraphs above, CO2 jets and confetti at the drop, uplight washes on the brick, haze, phone lights in the
  crowd.
- **Backdrop** (`Core/NativeClubScenery.cs`, three SourceLit sectors): the brick to the eaves, riveted roof trusses, the
  clerestory, the old gantry crane parked over the stage, pendant lamps; the crowd galleries beyond the arches; the sunken
  crowd pit; through the great window the quay, the river with the city's light laid on it, a lit cable-stayed bridge, the
  far bank's cooling tower turned billboard, the skyline.
- **Low tier:** no advanced lighting (no beams: billboards; no sheen), fewer facets and crowd, the same floor and every
  telegraph. **Reduced effects:** fewer atmosphere quads, softer flashes; telegraphs unchanged.

## Files

- Data: `docs/arenas/scripts/club_m1.py`; `tools/generate_stage_hazards.py` (one list entry);
  `Assets/Conversion/Resources/UnityMaps.json` and `tools/asset-lock.json` (regenerated: the club's record; the generator
  also writes `rotation: true` into the five M1 records, which the runtime already forces); `MapLooks.json` (one entry).
- Core: `NativeClubLooks`, `NativeClubPulse`, `NativeClubShow`, `NativeClubKit`, `NativeClubRig`, `NativeClubFx`,
  `NativeClubScenery`; `NativeStageHazards.VentShove`; `NativeUnityMaps` (the catalogue and rotation pins).
- Runtime: `NativeClubPresentation` (the draw, `ClubScenery`/`PrewarmClub`, `ClubLaserBisect`), `NativeClubAudio` (the spectrum and the
  hazard cues, `club.*` bank keys), `NativeClubValidation` (the Editor gate); one line each in `NativeSourcePresentation`,
  `NativeStagePrewarm`, `NativeAudio`, `NativeBisectPresentation` (the laser's cut first); the shove in
  `NativeStageHazardsRuntime`; the club's maglev skipped in `Core/NativeSmashGib.Plan`.
- Editor: `NativeBuildChecks` (one line: `ValidateClubRuntime`).
- Tests: `Tests/ClubStageChecks.cs` (`--club-stage`, also in the full run); the count and list pins in
  `BrickfallChecks`, `TransitionLoadingChecks`, `SurfaceChecks`, `MapStyleChecks`, `AdvancedLightingChecks`,
  `ZombieLightChecks` (the look lights its bodies).

## Music

The owner's song **Neon Substation** (Suno, 2026-11-10; 227.6 s) plays under the key `club-theme`
(`NativeStageMusic.Theme`). The record's `music` keeps the placeholder `cybercity-neon-riot` as the fallback. The song runs at
about 132 BPM (measured), not 128: the lights follow its spectrum as they follow any song, while the hazards keep the 128 BPM
bar grid on the match clock, so its drops do not land on the hazards' bars.

## Stage art (2026-11-10): modelled meshes, real materials, and the Greys

Owner (2026-11-10): "Are the new maps voxel? They look voxel lol ... Please improve the quality of the maps- Like better material
design; Better modeling. The DJ and people on metro map; Lets make those into Aliens (The design of Greys) ... Yes lets move
away from box built ... Make use of unity engine." Branch `claude/art-club` (from `claude/stage-art`); the recipe and the
club's own notes are in docs/arenas/STAGE-ART.md (9). Status: **implemented on the branch, seen in the Editor; not merged, not
released.** Presentation only: the map record, the colliders, nav, the hazards and their timings, the spawns and every rules
check are untouched.

With the stage bank `ChipArenaStageClub` (local, git-ignored: it travels in the builder's Drive zip) the club draws as modelled
art lit by the stage shader (ChipArena/StageLit) in place of every dressed block, the kit's ~2,300 boxes, the rig's modelled
parts and the backdrop's near sector. Without the bank every box draws exactly as before.

- **The shell.** Brick (courses and mortar in the shader, grime baked from the floors) with five arches a side onto the
  galleries (dark voussoir rings, stone keys, sills and imposts), the old crane rail as a riveted I-beam on stone corbels,
  copper bus bars on porcelain insulators and enamel warning plates over the mezzanines, the great south window's riveted
  I-section mullions and glazing bars, the neon sign as round tubes on a riveted steel panel (the bolt a real zigzag), the LED
  wall's cabinet frame. Above the play space: brick to the eaves, ten riveted Pratt trusses, purlins, the roof and its
  night-blue ridge lantern, enamel pendant lamps, strings of suspension insulators and their cables, the parked gantry crane.
- **The hall.** A polished concrete floor; the LED dance floor's aluminium frame between the rig's lit tiles; the lighting
  truss and the towers as aluminium tube truss (the towers with a black scrim sock: they are cover); the video cube's frame and
  mullions; cast-iron columns (plinth, moulded base, swelling shaft, riveted collars, capital); steel decks on joists with
  board tops; smoked-glass rails with chrome posts; ribbed stage barricades; treads with chrome nosings.
- **The stage end.** The stage with its panelled front and footlights; the speaker stacks (four rounded cabinets, baffles
  with wells and bezels for the rig's cones, the horn cabinet's flared mouths, handles); the DJ console with two decks (platters,
  tone arms), the mixer and a laptop.
- **The bar and the lounge.** A reeded walnut bar with pilasters under a black top, the brass foot rail and taps; the back bar's
  carcass, smoked mirror and glowing lathe-turned bottles; the brick pillar; velvet sofas (cushions, arms, brass buttons),
  padded booth partitions, pleated velvet drapes on the rooms' hall walls; go-go podiums; planters with succulents; lightbox
  posters in the rooms the pilots start in.
- **Breakables** (their own models while they stand to damage stage 2, soot growing with damage; then the dressing's stub):
  cable-drum tables with a lamp, slatted pine crates, stacks of steel kegs, road cases with aluminium edges and ball corners.
- **The rig** (Core NativeSubstationArt poses it from NativeClubRig): the woofer cones in their wells (pumping, thrown out at
  the drop, the ring round each dust cap in the build's and the burst's light), the sixteen moving heads (a yoke turned to the
  pan, a ribbed head along the aim, the lens in the show's colour), the laser wall's emitter posts rising from their slots and
  riding the pass, the two faceted mirror balls turning.
- **The Greys.** The DJ and the 64 crowd figures (32 on Low) are Grey aliens (an original design; STAGE-ART.md 9): tall and
  thin, a large cranium, big black almond eyes, nose and mouth slits, long arms and fingers. The crowd bobs, sways and throws its
  arms up exactly as the box rig's did (the same numbers); four looks and the rig's heights. The DJ nods on the beat with a hand at
  the headphones, the other on the mixer or thrown up through the build and the drop. The phone lights are unchanged.
- **Still the rig's boxes:** the LED tiles, the LED wall's panels and the cube's (flat lit panels, a colour each per frame).
  **Still the backdrop's:** the river, the bridge, the cooling tower and the far bank.
- **Low tier:** the bank's Low tier (the same art, about a quarter of the triangles; the reflection probe at 64 px).

Files: Core `NativeSubstationArt.cs` (new), `NativeClubRig.cs` (its outputs for the poses), `NativeClubKit.cs` (Family, Owner,
Kind per part), `NativeStageArt.cs` (the bank's line, the pose buffer 512); Runtime `NativeClubPresentation.cs` (the art's
Prepare and Render, the boxes behind `if(!ArtDrawn)`); Tests `ClubArtChecks.cs` (`--club-art`), `ClubArtLayout.cs`,
`StageArtLayout.cs` and `Program.cs` (a line each); tools/stages `club_art.py`, `club_hall.py`, `club_props.py`, `club_greys.py`,
`club_kit.py`.

## Not done / not seen

- Not playtested by people; bots were only watched in captures. The vent shove's strength is a first guess.
- Audio cues reuse existing clips (countdown, spike-rise, dash, electric-hook, lightning, explosion) under `club.*` bank
  keys the owner's bank can fill; no crowd or club ambience bed exists yet (`club-crowd` is a stub).
- No kit bank (PackDressing) entry: the stage is complete without it.
