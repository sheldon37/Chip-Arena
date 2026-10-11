# Chip Arena: new arenas proposal (Arena 1: Brickfall)

2026-10-11 · Claude (presentation lane) · for the owner (decides) and Astra (implements collision, structural sim, spawns, bots, multiplayer)
Base read: `40009f1` on `claude/sleepy-ritchie-g8ruhv` (= `codex/unity-cloud-current`), Unity port `unity/ChipArenaConversion`.
Status: **concept approved by the owner on 2026-10-11 (Brickfall, with Claude's recommendations for every other decision; see "Owner decisions" below). Implementation has not started; nothing has been run in Unity, on Windows or in multiplayer.** Layout numbers come from a 2D draft script (Appendix A).

## Owner decisions (2026-10-11)
The owner chose **Brickfall** and asked for Claude's recommendation on everything else (§8.2):

| # | Decision | Ruling |
|---|---|---|
| 1 | Concept | **Brickfall** (`brickfall`) |
| 2 | Self-crush and team crush | A pilot **can** be hurt by a collapse they caused. Teammates follow the room's friendly-fire setting. |
| 3 | The Wrecker | **Deferred.** Not in M1; revisit after the G2 collapse playtest. |
| 4 | Round reset | Rebuild the map every round. |
| 5 | Random pool | `rotation:false`: selectable only (dev/experimental) until G3. |
| 6 | Tone mapping and bloom | **No game-wide change now.** Brickfall is lit for today's Linear, no-tone-mapping pipeline and existing glow; a tone-mapping pass, if any, is a separate later decision for the whole game. |
| 7 | Kill credit with no structural damager | The last pilot who damaged the victim within 6 s; otherwise no credit. |
| 8 | Roster lighting plan; Ward Zero PvP music | §2.5 accepted. No new Ward Zero PvP track for now. |
| 9 | Old music | Keep the old tracks after the old maps are removed. |
| — | Udio licence | Still the owner's to confirm for each track at delivery (§8.1). |

## At a glance

- **Recommendation: Brickfall (map id `brickfall`)**: a 1920s brick block in the middle of being demolished, at golden hour. Blow the yellow-banded load-bearing piers and the buildings come down in stages: hotel corners pancake into rubble ramps, skybridges tear off, tenement facades peel into the lanes, and kiln chimneys topple along painted fell lines.
- It keeps the current chunky box-built style. The quality jump comes from instanced bevelled blocks, a procedural block shader, a lighting rig for each map, set dressing, a backdrop and a full destruction-VFX package. It needs no textures.
- **It is shaped to be low risk for the first playtest.** It fits the existing 64×56 floor slab and the y 24 projectile ceiling. Two engine limits still need work: the hard-coded clamps (±31/±27, bots ±28/±24) don't match the new frontage, so all five move to map bounds, and jump chips can reach roofs and frontage tops, so those tops can't be stood on (both §3.2). The hotel has 3 storeys, M1 has 10 collapse segments, and every fall is authored and deterministic: it is posed from (segment, start time) on every client, so it costs no PhysX bodies and needs no 20 Hz prop snapping.
- **Every collapse is fair.** Members can't be damaged for the first 4 s of each fight. Every fall has at least 0.8 s of telegraph. A pilot takes at most 60 damage from one collapse, or 60% of their max HP if that is lower, counted after every damage multiplier. Spawns stay at least 3 m from any fall, fall-sweep or rubble volume. The ground stays connected in all 1,024 collapse states (Astra still has to test this).
- **Astra goes first:** a Unity-native map file with a content revision checked at join, per-map spawns, map-bounds clamps, a split of `static`-tagged blocks out of the prop list, the grey-box with a minimal bot nav graph, and then the segment system. Claude works in parallel on lighting, interim looks and the telegraph layer, then the art pass.
- **Owner decisions are needed before work starts** (§8): which concept, whether a pilot or their teammates can be crushed by a collapse they caused, whether to add the Wrecker, whether Brickfall goes into Random, tone mapping, and Udio licence terms.
- The two alternatives, **Skyframe** (the safest build) and **Colossus Drydock** (the most memorable), are in §4.

---

## 1. Why the current stages feel the same

| Finding | Number | Source |
|---|---|---|
| Every rotation map embeds the **same 184-block four-corner tower set**. Only the wall material changes. | 61–94% of each map's blocks; only 11–116 blocks are unique to each map | `GameContent.json` (`vertical-<map>-*` ids, from `src/shared/vertical-arena.ts`) |
| Same footprint, same heights, same spawns | Every map uses the 64×56 floor, has a top at 14.4, and the **same 8 global spawns** | `Runtime/NativeArena.cs:85` (verified) |
| The middle is empty | The central 40×40 m is **79–98% open floor** (lavaworks reads 67%, but only because of its floor slabs) | 1 m raster, research report |
| Few, flat materials | **4 materials in the whole game**, one colour each (wood `#C78342`, stone `#8A929E`, metal `#426B79`, foundation = map floor colour) | `GameContent.json` |
| Same block kit | The exact top-5 block sizes match on 10 of 14 maps; the other 4 share 4 of the 5. "Interior" huts reuse the tower panel, slat and post sizes. | research report |
| One lighting rig | All outdoor maps share the same ambient, moon direction and intensity. Only a few colours change, through map-id branches. | `Runtime/NativeLighting.cs:66-71` (verified) |
| Plain rendering | Every block is a `CreatePrimitive(Cube)` with the 28-line flat Lambert `NativeLit` shader. Each map has 195–816 separate shadow-casting renderers, with instancing off. | `NativeDestruction.cs:32-37`, `Resources/NativeLit.shader` |
| Shallow destruction | **7 blocks in the whole game** have `support[]`. Supports don't cascade, because a fallen support stays in `Props` with Health > 0. Every break makes 4 fixed chunks. There are no damage stages and no dust. | `NativeDestruction.cs:41-52` (verified) |
| Collapses feel like nothing | Break and Collapse never shake the camera: they carry `Target=0`, and the shake rule needs `target==local`. A collapse plays the generic explosion sound once per block. | `Core/NativeEventFeedback.cs:56-58` (verified), `:207,241` |
| Few hazards | Only sawmill, spikevault, lavaworks and Ward Zero (vents) have hazards. They are switches on map id. | `Core/NativeHazards.cs:10-21`, `NativeVents.cs:27` |
| Menu preview | The menu backdrop draws the baked scenery but **none of the arena blocks** | `NativeBackdropPresentation.cs:8-20` |

Ward Zero is the exception: a bespoke baked hall of 116,417 vertices in 4 batches (`tools/models-provenance.json`), with 35 moving parts. 20 follow PvE entries (8 vents, 8 door leaves, 4 cells) and 15 follow the song or mood (the fan, 6 beams, 4 clock hands, 3 chandeliers, the main doors; `NativeWardPresentation.cs:36-43`). It stays.

---

## 2. Quality bar: "current style, massively higher quality"

### 2.1 Rules for every new map
1. **What looks like cover is cover.** Below 3 m, decoration never extends more than 0.25 m beyond collision. Anything that hides a pilot but lets shots through must be an open lattice (at most 20% occlusion).
2. **Detail comes from more boxes and procedural shading, not textures.** Everything stays flat-coloured and box-built.
3. **One identity per map:** its own palette, light, sky, landmark and backdrop. Golden hour belongs to Brickfall only (see §2.5).
4. **Destruction is readable before it happens** (colour code in §2.3). **Gameplay-critical telegraphs are never reduced** by reduced-effects or `graphics=performance`.
5. **Decor never goes into sim data.** Every prop is sent in every 20 Hz frame, and `NativeFrameBudget` never trims props (`NativeFrameBudget.cs:8-23`).

### 2.2 Arena kit: what exists today and what Claude builds

| Layer | Today | Claude builds | Budget |
|---|---|---|---|
| Block geometry | Plain cubes, one renderer each | **ArenaDressing**: draws `arena.Props` as instanced bevelled meshes keyed by (size, look). 4–6 cm chamfer, baked edge highlight (+18%), corner darkening (−22%), chipped variants for damage stages. Hides the authority renderers. Instanced draws ignore today's hide paths, which only toggle Renderer components (`NativePreview.cs:22`, `NativeReplay.cs:57`, `NativeDraftPresentation.cs:16`) or deactivate the root (`NativeDraftPresentation.cs:38`), so the dressing checks the preview, replay and draft states itself and draws the replay arena, not the live one, during the killcam. | ≤60 instanced draws |
| Surfaces | `NativeLit` flat Lambert; trim strips only on the +z face | **NativeBlockLit** shader. World-space procedural looks: brick bond, board-formed concrete, rivets, planks, corrugation, tar. Also edge wear, a grime ramp over the lower 1.8 m, dust and soot on top faces, an emissive window channel, a crack overlay driven by health, a dust-coat grid, height haze, and an 8-light practical block with its own arrays (§2.4). | Per-instance parameters, no textures |
| Lighting | One global night rig | **Per-map presets in data** (`MapLooks.json`): key, rim, trilight, fog, shadow cascades, sky variant, 8 practical lights. The map-id branches in `NativeLighting` move into data; old maps keep identical values. | 0 new Unity lights |
| Sky | `NativeNightSky` exposes only `_Horizon`, `_Zenith` and `_MoonSize`; moon direction, halo, star and cloud colours are hard-coded (`NativeNightSky.shader:2,13`) | New properties for sun or moon direction, disc, halo, star amount and cloud tint, defaulting to today's values; presets dusk, day, overcast, storm | 0 extra draws |
| Backdrop | Electron-baked `map-<id>` meshes pinned to the old commit; no new ones can be added | Deterministic C# backdrop for each map, outside the bounds plus 2 m, instanced, no colliders, layer 0. Also drawn in the menu and the killcam (`SourceWorld` and `RenderBackdrop`). | ≤10 draws |
| Set dressing | Trim strips on wood and metal | Articulation kits (sills, lintels, cornices, quoins, arches, signage, fire escapes, wires), each attached to a block id. They ride with the block when it falls and disappear when it breaks. Dressing between the camera and the pilot fades out (§5.5). | ≤60 draws; small trims cast no shadows |
| Atmosphere | Moonlit snow, chimney smoke, canal water | Per-map motes, haze, sun shafts, ambient life | ≤15 draws; scaled by EffectsQuality |
| Destruction VFX | 18 sparks on Break, 6 on Collapse. No dust, no stages, no shake. | Damage stages S0–S3, material break bursts, stress telegraph, fall streamers, landing dust wall, rubble with fresh-break fade, dust coat, decal ring buffer (256), shake based on distance to the event | ≤120 live dust billboards; existing voxel pools (900 solid + 700 glow) |
| Contact shadows and decals | Siren scorch; short-lived combat-glow impact marks on props (`NativeCombatGlow.cs:143`) | Contact-AO blobs at wall bases, rebuilt on each rubble swap; scorch, chip and crack decals | 1–2 draws |
| Menus | No arena blocks in the backdrop; the room options show each map's tag line (`ConversionGame.cs:341`), but there is no map card | The backdrop shows the full arena; an arena card around the tag line; an optional title card at countdown | n/a |
| Audio | No ambience; collapse sounds like an explosion | Ambience beds plus positioned emitters; destruction cue set; one music track per map | ≤6 rubble voices |

### 2.3 Destruction colour code (shared by every map)
| What you see | Meaning |
|---|---|
| Grey concrete, buff stone, unmarked frontage | **Never falls** |
| Timber, crates, sandbags, plain brick panels | **Breaks to anything** (cover; panels open holes) |
| Yellow `#F4B321` / black `#1C1A18` chevron band at 1.0–1.6 m plus a red `#D6332A` DANGER tag | **Load-bearing. Explosives only.** Bullets and melee give a dull "tonk" and a ricochet spark, which teaches the rule. |
| Hazard-striped strip painted on the ground, yellow arrow on the base | **Fell line**: where it lands |
| Chunky yellow hinge pin | Which way a span swings |
| Amber lamp on a band | Locked (the first 4 s of each fight); it goes dark once armed and strobes red at the last damage stage |

### 2.4 Performance budget (for each new map)
| Item | Target |
|---|---|
| Frame | 144 fps target (`ConversionGame.cs:40`); **not yet measured on the owner's PC** |
| Draws | Arena ≤60, dressing ≤60, backdrop ≤10, atmosphere/VFX ≤15, decals ≤2, plus shadow passes |
| Lights | 0 new Unity lights. Practical lights go through a shader light block. Today it is in `NativeSourceLit` and `NativeSourceTransparent` (`:21`), gated by `_StageLit`, and reads the global `_StageLightPos`/`_StageLightColor[8]` arrays, which entrance intros rewrite (`NativeLighting.cs:17-25,59`) and `EndIntro` never clears (`:26`). **`NativeBlockLit` gets its own arrays**, or the arena could pick up leftover entrance lights. Combat-glow spill, blast flashes, seeker and halo lights are Unity point lights (`NativeCombatGlow.cs:76`, `NativeBlastPresentation.cs:50`, `NativeDevEffects.cs:230`, `NativeAuditorHaloPresentation.cs:133`) that light today's cubes, but point lights did not light instanced draws (`NativeLighting.cs:34-36`), so blast flashes also feed the arena block or explosions stop lighting walls. The 9 pixel-light slots drop to 3 on `performance` and low-end `auto` (`NativeSettings.cs:53`). |
| Shadows | Today the runtime sets 2 cascades over 65 m (`NativeLighting.cs:82-83`); there is no `QualitySettings.asset`. Brickfall asks for 4 over 75 m, which adds shadow passes that count in the draw budget; 2 over 65 m is the fallback. Off on `graphics=performance` (`NativeSettings.cs:53`). |
| Sim | Replicated props at round start ≤200 (after the split of `static`-tagged blocks); ≤80 live fragments; ≤360 bodies; ≤500 props per frame |
| Reduced effects | Atmosphere and debris at half or less; telegraphs never reduced |

### 2.5 Roster lighting plan
Golden hour goes to Brickfall only, because all four concepts chose it independently. Later maps: Skyframe under overcast or night floodlights, Drydock at blue hour or in a storm with sodium lamps, Junction at harsh noon, Ward Zero stays a sealed interior.

---

## 3. Recommended Arena 1: BRICKFALL

### 3.0 Why this concept, and what changed
- **Judges:** the gameplay/sim judge ranked Skyframe first and Brickfall a close second; the identity/production judge ranked Brickfall first. Brickfall has the **best combined rank** and is the most literal match for the owner's "collapsing buildings if they're exploded".
- **What the first judge asked for is built in:** the 64×56 fallback footprint (no ceiling refactor; the clamps still move to map bounds, §3.2), a hotel cut to 3 storeys (a weaker roof perch, no ceiling clipping), a trimmed M1 (10 segments; the tank, tenement-floor pancake and Wrecker are deferred), and authored falls only.

| Idea taken from another concept | From | Used for |
|---|---|---|
| Destruction colour code, "tonk" feedback, grit columns at the corners of the crush box, riders ride pancaking slabs down | Skyframe | §2.3, telegraphs |
| Telegraph decal generated from the **same Core definition** as the crush volume; amber/red lamp states; the dying-landmark beat (MERCER sign flickers out) | Drydock | Telegraphs, look |
| Telegraphs never reduced; spawn LOS checked with ±1 m jitter at 3 eye heights; build check "no intact prop inside a crush volume"; last-hit credit window | Junction | Fairness, checks |
| Pancake mode, weighted supports with cascade, authored rubble swap with pilot ejection, rubble walkability rules | Brickfall original | Core system |

### 3.1 Pitch
Golden hour, about 18:40, in a condemned brick district. The **Hotel Mercer** (3 storeys, with a blue neon MERCER blade sign) stands in the middle of a square ringed by streets. Two walk-up tenements, **Ashby House** and **Bray House**, are linked to it by steel skybridges at 7.2 m. Two **kiln chimneys** stand in notches in the frontage. Each round starts intact and ends rewritten: lanes become cover-to-cover, hotel corners become ramps into the first floor, chimneys lay a cover line across the rifle lanes, and the surviving stair core becomes a ruin spire.

**The rule to learn: yellow bands hold it up. Blow the band and it comes down.**

### 3.2 Footprint
- **Outer edge:** 64×56 m, the existing floor slab (x ±32, z ±28). An indestructible frontage 2 m deep and 14 m tall, with tops that can't be stood on, closes all four sides, with 4 shop alcoves (2×1.5 m) on each long side.
- **Play space:** 60×52 m (x ±30, z ±26). The hard-coded ±31/±27 clamps (`NativeAbilities.cs:131`, `NativeChips.cs:82` blink and dash target, `NativeLeet.cs:96`, `ConversionGame.cs:158` aim preview) land 1 m inside the frontage, and `GroundPoint` raycasts down from the clamped point (`NativeAbilities.cs:132`), so zones can land on top of or inside it. The bot clamp of ±28/±24 (`NativeBots.cs:36,38`) excludes the 2.2 m clear strip left in the lane after a facade peels (z 23.8..26), so bots would lose the lane route. **All five clamps therefore come from map bounds minus the 0.42 m capsule radius** (§6.1 step 2). All spawns also sit within today's bot clamp.
- **Area:** about 2,500 m² of open ground plus about 1,100 m² of upper floors and roofs. That is about 3,600 m² walkable, against about 4,700 m² today. It is denser on purpose.
- **Heights:**
  - hotel roof 10.8 m (parapet 11.8, roof stair house 13.4);
  - tenement roofs 7.2 m (parapet 8.2);
  - shop roofs 3.6 m;
  - frontage 14 m;
  - chimneys 16 m.
- **Limits:** the y 24 ceiling is unchanged, but it stops projectiles only (`NativeProjectiles.cs:76`, `NativeAbilities.cs:213`, `NativeMeleeSpecials.cs:185`), not pilots. The kill plane at −6 is unchanged, and there are no pits.
- **Jump reach.** The eye sits 1.37 m above the feet (capsule centre 0.85 m plus `EyeHeight` 0.52, `Core/NativeAbilityRules.cs:27`). Apex = (8.5 × jumpPower)² / (2 × 23 × playerGravity) (`NativeArena.cs:60,157,159`). jumpPower is capped at 2.5 (`ConversionContent.cs:89`), playerGravity has no cap, and no chip excludes another. Vertical gain only; horizontal gaps are not modelled.

| Build | Apex | Can reach |
|---|---|---|
| Base | 1.57 m | the 1.0 m hotel parapet (standing eye 13.2 m) |
| 1× spring (×1.18) | 2.2 m | the 13.4 m roof stair house from the parapet |
| rubber (×2) | 6.3 m | the hotel roof from the 7.2 m bridge deck, bypassing the core; 14 m frontage tops from 8.2 m parapets; 16 m chimney tops from the frontage |
| rubber + 2× spring or more | 9.8 m (2.5 cap) | as above, with margin |
| moonboots (gravity ×0.3, jump ×0.8) + rubber | 13.4 m | the hotel parapet straight from the street |
| moonboots + rubber + 2× / 3× spring | 26.0 / 32.7 m (cap at 3) | above the y 24 projectile ceiling |

- **Rule:** frontage tops, chimney tops and the roof stair house can't be stood on. Astra adds a pilot-only blocking volume or push-back trigger above them. A pitched cap alone is not enough, because `Grounded` is a plain sphere-cast with no slope test (`NativeArena.cs:104`). With this rule the highest standing eye is 13.2 m (on the hotel parapet) for every build; mid-jump it is 13.2 m plus the apex (14.8 m base, 19.5 m rubber, 26.6 m moonboots + rubber).

### 3.3 Blockout (top-down)
Generated by `docs/arenas/scripts/brickfall_m1.py` (run `python3 docs/arenas/scripts/brickfall_m1.py`). 1 char = 1 m of x and 2 m of z; north (+z) is up; edges are rounded to the grid. The map is point-symmetric: (x, z) → (−x, −z).

```
         x=-32                          x=0                           x=+31
 +26..+28 #####aa#########aa###############aa######KKK######aa############
 +24..+26 ##OO.................................OOOkKKK....oo....///ddddd##
 +22..+24 ##.......,,,,,,,,,,,,,,,,..ooo.......OOOkkkkoo........///ddddd##
 +20..+22 ##.....PT,,,,,,TPT,,,,,,TPT.../////////.kkkk.....7.........5..##
 +18..+20 ##..f..T..................T...SSSSSSSS/.kkkk...ttttttt.....oo.##
 +16..+18 ##.....T....CCCCCCCCCCC...T...SSSSSSSSoookkk...ttttttt.....oo.##
 +14..+16 ##.ooo.T....CCCCCCCCCCC...T.h.SSSSSSSS..kkkk...oo....6........##
 +12..+14 ##.....PTT.TCCCCCCCCCCC.TPT.............kkkk..Moo...MMMMoo.8MM##
 +10..+12 ##......sss.............==..............kkkk..................##
  +8..+10 ##......sss.....g.......==....OOOO........OOO.......sss..sss..##
  +6.. +8 ##..sss......sss.......PP=HHHHHHH.HH...PP.OOO.......sss.OOOOOO##
  +4.. +6 ##..sss................PP....CCCC.C....PP.......sss.....OOOOOO##
  +2.. +4 ##......................H....CCCC.C.....H...............OOOOOO##
  +0.. +2 ##......OOOO........ooo.HHHH.CCCC.C.HHHHHooo........OOOO......##
  -2.. +0 ##......OOOO........ooo.HHHH.CCCC.C.HHHHHooo........OOOO......##
  -4.. -2 ##OOOOOO.....sss........H....CCCC.C.....H.....................##
  -6.. -4 ##OOOOOO.....sss.......PP...HCCCC.CHHH.PP.......sss......sss..##
  -8.. -6 ##OOOOOO.sss.......OOO.PP.............=PP.......sss...........##
 -10.. -8 ##..sss..sss.......OOOkk......OOOO....==........G....sss......##
 -12..-10 ##MM..MMMMMM...MMM..kkkk..............TT.TTTTTTTTTTTT.TTT.....##
 -14..-12 ##..4..oo.......oo..kkkk..............P..CCCCCCCCCCC....PT....##
 -16..-14 ##........2.........kkkk..SSSSSSSS....T..CCCCCCCCCCC.....Tooo.##
 -18..-16 ##.oo.....ttttttt...kkkkooSSSSSSSS..H.T..CCCCCCCCCCC.....T....##
 -20..-18 ##........ttttttt...kkkk..SSSSSSSS....T..................T.F..##
 -22..-20 ##...1..............kkkk..////////....PT,,,,,,TPT,,,,,,TPT....##
 -24..-22 ##ddddd///....3o..ookkkkOOO........oo..,,,,,,,,,,,,,,,,.....OO##
 -26..-24 ##ddddd///....oo....kKKKOOO.................................OO##
 -28..-26 ############aa#######KKK#####aa###############aa#########aa#####
```
**Legend** (heights in metres above the street at y 0):

*Perimeter*
- `#` frontage: indestructible, 14 m; tops can't be stood on.
- `a` shop alcove: 2×1.5 m, walkable.

*Buildings*
- `H` Hotel Mercer walls (x −8..8, z −6..6). Floors at 3.6 and 7.2, roof at 10.8, parapet 1.0.
- `C` stair cores: indestructible.
- `T` tenement walls. Ashby is x −25..−6, z 12..21; Bray is the mirror. Floor 1 at 3.6, roof at 7.2, parapet 1.0.
- `P` yellow-banded load-bearing piers.
- `S` corner shops (Kessel's N, Dunmore's S): closed, roof perch at 3.6 with a 0.8 m parapet.
- `/` ramps of 25° or less, plus the loading-dock ramps.
- `=` skybridge deck at 7.2 m (overhead).
- `M` market-hall south wall: 4 m, indestructible, with two arches.

*Collapse zones*
- `K` chimney: 2.4×2.4 m, 16 m tall; top can't be stood on.
- `k` fell line: a 3 m painted strip, kept free of props.
- `,` facade-peel berm volume: kept free of props.

*Cover*
- `O` tall hard cover: tram shelter 2.6, monuments 2.4, market kiosks 3.2, storerooms 3.5, news kiosk 2.8, phone box 2.5.
- `o` low cover, destructible: pallets and sandbags 1.1, barricades 1.2.
- `s` wooden market stalls: 2.4×1.2×1.1.
- `d` loading dock: 1.2.
- `t` box truck: 6.5×2.5×3.0.

*Spawns*
- `1-4` Team A (Brick Yard, SW); `5-8` Team B (Coal Yard, NE).
- `f g h` / `F G H` extra FFA spawns.

**Hotel ground floor (the lobby)**
- Openings:
  - N face: door x −7..−5.4, the Mercer Arcade (x 0.2..2.2), a shopfront x 3.5..7.
  - S face: a shopfront x −7..−3.5, the Arcade, a door x 5.4..7.
  - W face: one door at z 1..3. E face: one door at z −3..−1.
- Wing seam partitions at z 0 (x ±4..±8) leave a 1.6 m door beside the core.
- **Rule: apart from the Mercer Arcade, a deliberate 2.0 m north–south lane through the core, no straight line passes through two ground-floor openings on opposite faces.** The draft script samples 729 N→S and 81 W→E lines at 1.6 m eye height: the only open ones are the 81 arcade-to-arcade lines. The doors sit far enough west and east that no line runs door → seam door → shopfront.

**Hotel core**
- The core is 4.8 m wide (x −2.4..2.4): a 2.6 m west bay (x −2.4..0.2) and a 2.0 m east bay (x 0.2..2.2) with a 0.2 m outer wall (x 2.2..2.4). A scissor stair uses the west bay up to 3.6 and both bays above. Landings alternate: north at 3.6, south at 7.2, north at the 10.8 roof (the only walking route onto the roof).
- At ground level the east bay is the Mercer Arcade, a north–south passage 2.0 m wide with 3.36 m headroom.

**Tenement doors**
- South face: x −22.5..−20.5 and −9..−7.5.
- West and east ends: z 13..15.
- Shop windows between the piers on the lane side.

### 3.4 Sections
Generated by `docs/arenas/scripts/elev_m1.py`. 1 column = 1 m, 1 row = 1.2 m, `@` = a 1.7 m pilot.
```
SECTION A-A  x = -7, looking west (S left, N right). Round start, everything intact.
 15.6 kkk
 14.4 ##k                                                   ##    14.0 frontage tops (drawn in the 14.4 row); S stack beyond the cut, 16 m
 13.2 ##                                                    ##
 12.0 ##                    |   @      |                    ##
 10.8 ##                    ============                    ##    10.8 hotel roof (+1.0 parapet)
  9.6 ##                    |          |                    ##
  8.4 ##                    |       @  |+++@++|  @    |     ##
  7.2 ##                    ============######=========     ##    7.2  hotel F2 / skybridge / Ashby roof
  6.0 ##                    |          |      |       |     ##
  4.8 ##                    |  @       |      |       |     ##
  3.6 ##                    ======:=====      =========     ##    3.6  hotel F1 / Ashby F1
  2.4 ##                    P     :    P      P       P     ##
  1.2 ##      @          @  P     :    P      P       P  @  ##
  0.0 ______________________P_____:____P______P_______P_______    0.0  street
        ^             ^     ^           ^     ^        ^    ^
      z: -26 frontage | Bray Steps -12 | S St -6 | HOTEL (SW|NW) +6 | N St +12 | ASHBY +21 | lane +26

SECTION B-B  z = +3, looking north. AFTER "hotel-nw" pancakes: rubble ramp r (<=22 deg) leads from West St to door D at 3.6.
 12.0 ##                                 |  @ |                     ##
 10.8 ##                            CCCCC======                     ##   10.8 NE roof
  9.6 ##                            CCCCC|    |                     ##
  8.4 ##                            CCCCC|    |                     ##
  7.2 ##                            CCCCC======                     ##   7.2
  6.0 ##- - - - - - - -             CCCCC|    |       - - - - - - - ##   6.0  market canopies (open lattice, visual only)
  4.8 ##                           @CCCCC| @  |                     ##
  3.6 ##                        @ /DCCCCC======               OOOOOO##   3.6  core N landing door D / NE floor 1
  2.4 ##                       /rrrrCCCCC|    P               OOOOOO##   2.4
  1.2 ##                  @  rrrrrrrCCCCC|    P           @   OOOOOO##
  0.0 ##____________________rrrrrrrrCCCCC|____P_______________OOOOOO##   0.0  street
      x: -30 | West Market | -14 W St -8 | NW rubble | core | NE quadrant +8 | E St +14 | East Market | +30
```
**Windows:** 1.6×2.2 m (sill at 0.8 m). Any window, roof or bridge is a free drop-down, because the jump apex is 1.57 m and there is no fall damage.

### 3.5 Zones (mirror pairs listed together)
| Callout | What it is | Plays as |
|---|---|---|
| **The Mercer**: Lobby, Floors 1–2, Roof, Core | The 16×12 hotel in 4 quadrants (each 5.6×6 m). The only walking route onto the roof is the core's north landing (jump chips: §3.2). A stair house (4.8×3×2.6 m) on the roof splits roof sightlines. | Lobby: close-quarters. Floors: mid-range window lines onto all four streets. Roof: the power perch, which shrinks as corners fall. |
| **Mercer Square**: N/S/E/W St | 6 m ring streets. Tram shelters on N and S St, barricades on W and E St, monuments at the NE and SW corners, skybridges overhead at the NW and SE corners. | Mid range; ground for magic and area damage |
| **Ashby House / Bray House** | 2-storey tenements, 19×9 m. Ground-floor shop, a floor-1 gallery over the lane, roof at 7.2. The skybridge lands on the east bay of the roof. | Close-quarters shop; firing line from the gallery onto the lane |
| **Ashby Lane / Bray Lane** | 5 m streets along the frontage. A news kiosk, a phone box, sandbags. The berm zone is kept empty. | Outer rifle lane (about 35 m to the kiosk) until the facade peels |
| **Brick Yard (A) / Coal Yard (B)** | Spawn yards, 16×14 m: loading dock, box truck, pallets, fire barrel | Three exits each: lane, market arches, Steps |
| **West / East Market** | 16×24 m halls. Brick south wall with two arches, open-lattice canopy at 6 m, 6 stalls, a kiosk, a storeroom. | Melee and ambush space linking the yard and the square |
| **Ashby Close / Bray Close** | 5 m alleys between a lane and a market | Flank route |
| **Ashby Cut / Bray Cut** | 4 m alleys between a tenement and a shop | Flank route; FFA spawn |
| **Kessel's / Dunmore's** | Closed corner shops; roof perch at 3.6 reached by a ramp from the lane | Mid-tier perch over the Steps and the lane mouth |
| **Mercer Steps / Bray Steps** | Open apron between the square and the lane, containing a fell line | Rotation space; it becomes a cover line after the chimney falls |
| **North / South Stack** | Kiln chimneys in frontage notches with painted fell lines | Demolition target |
| **Ashby / Bray Bridge** | Skybridges at 7.2 m: 2 m wide, rails to 8.3 | Risky high crossing |

### 3.6 Flow and sightlines
- **Routes between the yards (Brick Yard to Coal Yard):**
  - **Lanes:** Bray Lane → Bray Close → East Market. Exposed, long range.
  - **Market ring:** arches → market → W St / N St → Mercer Steps. Covered.
  - **Centre:** Bray Steps → S St → Lobby or Arcade → N St → Mercer Steps. About 75 m: about 11 s at walking speed (6.8 m/s), about 8 s sprinting.
  - **High route:** tenement core → roof → bridge → hotel floor 2 → core → hotel roof.
- **Lanes:** about 35 m from the close mouth to the news kiosk. The kiosk and the Coal Yard dock break the line from a lane into the opposite yard. After the chimney falls, its upright drum splits the stretch near the yard.
- **Hotel roof (standing eye 12.2 m on the deck, 13.2 m on the parapet):** sees the square, the markets through their lattice canopies, the Steps and parts of the yards. It **cannot see the lanes behind the tenements**: from the parapet, a line over the tenement's lane-side parapet reaches chest height at z ≈ 42, outside the map; even a moonboots + rubber jump (eye 26.6 m) lands at z ≈ 27. Counters to the roof:
  - one walking access, through the core (jump-chip builds also come up from the bridge decks, and moonboots + rubber from the street);
  - a 1 m parapet;
  - the stair house splits the roof;
  - every corner that falls takes away a quarter of the roof.
- **W St and E St:** north–south lines of up to about 38 m along the outer edge. The corner monuments break the inner part.
- **Spawns:** all pairs are blocked (§3.7).
- **Fit by kit:**
  - **Ranged:** lanes, hotel floors, roofs, shop perches.
  - **Melee:** lobby, arcade, tenement shops, markets, cuts, alcoves and every rubble mound. Melee kits can also drop structures with ChipBlast chips (dash-blast, shrapnel, bombs).
  - **Magic:** the 6 m ring streets, choke points at the core doors, area damage on stacked floors.
- **Round arc:**
  - 0–20 s: roofs and bridges are contested (members are locked for the first 4 s).
  - 20–50 s: the first collapses, usually a bridge, a hotel corner or a facade.
  - Late round: a core spire stands over rubble, and fights move to mid and close range.
  - Target: **3–6 collapses per round in an 8-pilot lobby** (base stats).
  - Overtime drains 18 HP/s from every pilot, ignoring damage multipliers, after 100 s of fighting (`NativeArena.cs:112`, `Core/NativeCombatRules.cs:23`, `maxRoundSeconds` 100), so a base-HP pilot is dead by about 106 s; high-HP builds last longer. The Wrecker (fight + 50 s) and the sun drop fit inside the first 100 s.

### 3.7 Spawns (a per-map table replaces the global table; today spawns are chosen by seat index only, `NativeArena.cs:85`, `NativeSandbox.cs:19`)
- **Yaw** is in degrees from +z toward +x and faces the origin, which is today's behaviour (`NativeArena.cs:88`).
- **Height:** y = 1.1, on the street.

| Team A, Brick Yard | Position (x, z) | Yaw | | Team B, Coal Yard | Position (x, z) | Yaw |
|---|---|---|---|---|---|---|
| A1 | (−27, −21) | 52 | | B1 | (27, 21) | 232 |
| A2 | (−21.5, −14.5) | 56 | | B2 | (21.5, 14.5) | 236 |
| A3 | (−17.5, −22) | 39 | | B3 | (17.5, 22) | 219 |
| A4 | (−27.5, −13.5) | 64 | | B4 | (27.5, 13.5) | 244 |

**Team rule.** Team ids run 0–7 (`Core/NativeRoomOptions.cs:13,15`, `NativeMatch.cs:95`) and teams can be uneven. The A/B tables are used only when exactly two teams are present and neither has more than 4 pilots (4v4, 4v3, 3v1 and so on); the lower team id takes side A. Any other split (5v3, 7v1, three or more teams) falls back to the FFA table in seat order, as today.

FFA seat order (spawn `min(seat, 7)`, as today):

| Seat | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
|---|---|---|---|---|---|---|---|---|
| Spawn | g (−16, 9.5) West Market | G (16, −9.5) | A1 | B1 | h (−4, 16) Ashby Cut | H (4, −16) | f (−27.5, 19) Ashby Close | F (27.5, −19) |

**Spacing by lobby size**

| Pilots | Closest pair |
|---|---|
| 2 | 37.2 m (the only pair, separated by the hotel core) |
| 3–4 | 32.4 m |
| 5–8 | 13.6 m (g–h, with Ashby's solid wall between them) |

**Checks run by the draft script (2D only, not Unity)**
- **28/28 FFA pairs and 16/16 team pairs are blocked** with ±1 m jitter at both ends and eye heights of 1.2, 1.6 and 1.9 m. The blockers modelled are every element 2 m or taller, including the hotel shell and tenement walls with their openings.
- The closest spawn to any crush, fell, berm or fall-sweep volume is **3.2 m** (f, to the Ashby facade's peel sweep). Sweeps cover the space a structure passes through while it falls: hotel panels 2.8 m beyond each face (§3.9) and tenement facade rows over the whole lane (z 21..26).
- Every spawn is at least 1.7 m from any block.
- Every spawn is inside the bot clamp.

**Rules for the build check** (Astra)
1. Floor within a 4 m raycast down, as today.
2. No spawn under collapsible structure, and none within 3 m of any crush, fell, berm, rubble or fall-sweep volume.
3. No spawn-to-spawn sightline, checked with jitter.
4. Members locked for 4 s after FightAt.
5. PvP has no mid-round respawn. For sandbox respawns, skip any point within 8 m of a live enemy or inside a stressed or falling volume.

### 3.8 Hazards
The city is the hazard. Every collapse is player-caused and telegraphed. **Nothing kills instantly, and there is no timed hazard in M1.**

| Hazard | Behaviour | Damage |
|---|---|---|
| Collapse crush | Each segment's crush volume (§3.9) | 45 when the falling mass passes head height, plus a 6 m/s outward push. **At most min(60, 60% of the victim's max HP) per pilot per segment event, from all sources combined** (crush plus that segment's debris), applied to the final amount after `damageTaken` and every other multiplier. Max HP can be as low as 15–45 (glass ×0.45, apex ×0.8, duelist −10, floor 15 at `ConversionContent.cs:88`), and damageTaken reaches ×1.59 (berserker ×1.15², annihilator ×1.2), applied after the amount is set (`NativeArena.cs:185`). |
| Riders | **Pancake mode only.** There is no platform-carry code: `StepPilot` overwrites horizontal velocity with the pilot's own movement every tick (`NativeArena.cs:152-154,164,168`) and the pilot collider has zero friction (`:77`), so hinge, peel and topple falls carry no one sideways. A rider stays on a dropping slab only while it accelerates slower than the pilot's own gravity, 23 m/s² × playerGravity (`:60,159`), which is 6.9 m/s² with moonboots. Slabs that carry riders keep `maxAccel` ≤ 6.9 m/s². | 15 on landing; no crush |
| Fell lines | Painted 3 m strip with a demolition notch and yellow arrow. Timing: base breaks → 1.0 s lean (0–8°) with a groan and mortar puffs → 0.6 s fall. | 55 plus a 7 m/s sideways push. With 1.6 s of warning, leaving the strip takes about 0.38 s plus reaction time: 1.5 m + 0.42 m from the strip centre, accelerating at 35 m/s² to 6.8 m/s (`NativeArena.cs:146,152`). |
| Loose fragments | Today's debris formula, capped at 65 per impact and 75 per group per 0.35 s, where a group is one broken block (`Core/NativeDebrisRules.cs:9,17`). Tighter while a collapse is active; the per-segment cap needs a new group key (the segment id). | 25 per hit, 40 per group per 0.35 s |
| G2: water tank (on the hotel roof stair house) | Four timber legs, 40 HP each, any damage. Losing two adjacent legs tips it to one of 4 stencilled landing spots. | 40 within 1.6 m of the landing spot |
| G3 (optional map flag): the Wrecker | At fight + 50 s, cranes outside the walls demolish a symmetric pair chosen by round parity. 5 s telegraph (horns, beacons). Pure function of round number and FightAt. | 40 plus knockback in the beaconed volume |

### 3.9 Destruction plan
**Principle: falls look physical, landings are authored.**
- A segment that loses support plays a scripted fall computed from (segment, t0). Host, guests and killcam produce identical motion.
- Under the landing dust, the falling blocks are removed and an authored rubble set becomes static collision.
- Physical fragments come only from masonry breaks, and they are budgeted.

**Block roles and damage filters** (new optional fields; old maps are unchanged)

| Role | Examples | Damage |
|---|---|---|
| `structural` | Hotel piers, tenement front piers, bridge bearings, chimney base rings | **Explosion sources only**, ×1. Bullets, melee and damage over time do 0 and give a "tonk". The demolition stat applies, **capped at ×1.5**: today it multiplies all prop damage with no cap (`NativeAbilities.cs:163`, `NativeChips.cs:38`, `NativeProjectiles.cs:133`, `NativeMelee.cs:76`), demolition (+0.6, max 8) and wrecking (×2.5, max 2) reach 36×, and one wrecking chip makes an armed rocket hit for 275. |
| `masonry` | Facade panels between windows (brick, 120 HP) | Bullets ×0.25, melee ×0.5, explosives ×1.5. Breaks into 4 fragments as today and opens a hole. |
| `carried` | Floor and roof plates, parapets, bridge decks, chimney drums | Immune; moves only with its segment |
| `cover` | Stalls, pallets, sandbags | As today (wood 34, etc.) |
| `static` | Cores, frontage, shops, kiosks, market walls, docks, truck, tenement floors in M1 | Indestructible; built once, not replicated each frame, not counted toward the cap |
| `rubble` | Authored rubble sets | Inactive until its segment lands, then static |

"Explosion sources" means `AbilityBlast` (frag, rocket, orbital, meteor, geyser, singularity, ball lightning, blink-strike) and `ChipBlast` (fire primary, block-pulse, dash-blast, shrapnel, wormhole, bombs, implosion, leet-block, leet-nuke, corpse-party). Static and trampoline call `ChipBlast` with props off (`NativeArena.cs:194`, `NativeSummons.cs:51`, `NativeChips.cs:103`) and never touch structure.

**Fire primary is the main sustained pier-breaker.** Each direct hit is a 1.5 m `ChipBlast` at the shot's damage × 0.38 (about 9), every 0.5 s with no cooldown (`NativeProjectiles.cs:98,138`): about 16–20 shots, 8–10 s, per 140 HP pier. Astra tunes it at G2 (keep it as magic's demolition role, or scale primary-weapon blasts on structural members).

**Member health** (assumes blast distance is measured to the closest point on the box, and demolition ×1; with today's centre distance each collapse costs about 40% more hits, and at the ×1.5 demolition cap a third fewer)

| Member | HP | Count | Typical cost |
|---|---|---|---|
| Hotel corner pier | 140 | 4 | Armed rocket (55 damage, ×2 vs props when armed = 110; `NativeAbilities.cs:275`, `NativeAbilityMinions.cs:38`) plus a grenade; or 2 meteors; or 2 orbitals. Solo with the machinegun: two armed rockets one 9 s cooldown apart (its bullets do 0). |
| Tenement front pier | 100 | 3 per tenement | One armed rocket; 2 piers peel the facade |
| Bridge bearing | 60 | 2 | One big explosive |
| Chimney base ring | 180 | 2 | 2–3 big explosives |

**M1 segments (10)**
A segment falls when the total weight of its intact supports drops below `failBelow`. A segment that is stressed, falling or already rubble counts as a lost support.

| Segment | Supports → falls when | Mode and timing | Rubble | What changes in play |
|---|---|---|---|---|
| `hotel-nw` / `-ne` / `-sw` / `-se` | Its corner pier (weight 1). Falls below 1. | **Pancake.** 1.2 s telegraph. Roof plate releases at t0, floor 2 at +0.25 s, floor 1 at +0.45 s. Outer facade panels peel outward 20–50°, one 3.6 m storey panel at a time (hinged at each floor line), so the sweep reaches 2.8 m beyond the face; the full 11.8 m wall never peels as one piece (it would reach about 9 m). | A mound over the 5.6×6 m footprint plus a 1.5 m apron into the street: 0.6 m at the outer corner, rising to 3.4 m at the core and seam (≤22°). Two slab shards lean on the core. | A new route up to floor 1 (through the core's north landing door for NW/NE, or the neighbouring floor edge for SW/SE). A diagonal line opens across the corner. The roof loses a quarter. |
| `bridge-ashby` / `-bray` | Bearing (1) plus its hotel quadrant segment (1). Falls below 2, so either loss drops it. | **Tear-hinge.** 0.8 s telegraph (sparks, rivets pop). Swings 0.7 s about the surviving end, tears off at +0.3 s, drops in 0.4 s. | A truss wreck lying diagonally across N St (or S St): 1.3 m high, 1.8 m wide | The high crossing is gone; vaultable cover in the street |
| `facade-ashby` / `-bray` | Piers F1–F3 (1 each). Falls below 2, so any two. | **Peel.** 0.9 s telegraph. Rows detach from the top every 0.12 s, rotate out 20–60°, and fall for 1.0 s. | A berm along the lane (x −23..−7, z 21..23.8), 0.6–1.6 m high, with two saddles 1.8 m wide and no higher than 0.5 m, and a 2.2 m clear strip along the frontage | The lane becomes cover-to-cover; the gallery becomes an open "dollhouse" facing the lane |
| `stack-n` / `-s` | Base ring (1). Falls below 1. | **Topple** along the fell line: 1.0 s lean, 0.6 s fall | A 2.0 m stump; an upright 2.4 m drum mid-lane (z ±23.5); four flat 1.3 m drums at z ±20, ±17, ±14, ±11 with 1.4 m gaps | The lane line is cut in two; a staggered cover line runs from the lane into the Steps |
| *G2:* `floors-ashby` / `-bray` | R1, R2 and the facade segment (1 each). Falls below 2. | Pancake | A 2.8 m ridge with two ramps; the core stands as a 7.2 m ruin spire | Opens a large sightline; its bridge falls with it |

**Damage stages** (from replicated health against the content `hp`, which needs no protocol change)

| Stage | Health | Look |
|---|---|---|
| S0 | 100–67% | Clean band with chevrons |
| S1 | 66–34% | Crack overlay, chipped corners, a dust trickle every 0.8 s |
| S2 | 33–1% | Heavy spalling, 6 rust-coloured rebar stubs, a waisted middle, constant grit, red lamp strobe, sparks on steel |
| S3 | Gone | A stub and a rebar tangle |

**Telegraph** (`StructureStress`: 0.8–1.2 s before any falling mass reaches head height). None of these are reduced by effects settings.
- A red crush-footprint decal fades in, generated from the same Core volume the sim uses.
- Grit columns fall at the four corners of the crush box.
- Dust streams from the slab joints.
- Windows strobe at 8 Hz, then go black.
- Creaks and a groan.
- A visual-only sag of at most 1° or 8 cm.

**Fairness rules**
1. Members are locked for the first 4 s after FightAt.
2. At least 0.8 s of telegraph before any fall.
3. At most min(60, 60% of max HP) damage per pilot per segment event, after multipliers.
4. Pilots overlapping new rubble are moved to the nearest walkable point within 3 m.
5. Rubble on walkways is 1.2 m or lower (bots already hop that, since their obstacle ray triggers a jump) or a ramp of 25° or less. It never blocks a core door, a stair foot or the frontage strip, and always leaves at least two openings of 1.6 m or wider.
6. The ground graph stays connected in all 2^10 segment states.
7. Build check: no intact prop inside any fell, berm or crush volume at round start.
8. **Credit:** a crush kill goes to the pilot who dealt the most structural damage to the triggering members in the last 10 s (ties go to the last hit). Kill-feed tag COLLAPSE. **Instigators are not immune** (owner decision, §8). Today `Damage()` returns 0 when the source is the target, or a teammate with friendly fire off (`Core/NativeCombatRules.cs:7`, `NativeArena.cs:182`), which also makes teammates immune to each other's debris. Self-crush with credit to the instigator needs a new damage path, and team crush needs its own decision.
9. The whole map is rebuilt every round (today's behaviour, kept on purpose).

**Caps**
- At most 3 segments falling at once; further triggers queue 0.3 s apart.
- At most 80 live fragments. Fragments freeze to kinematic after 2.5 s at rest and crumble after 20 s; when over the cap, the oldest crumble in a dust puff.
- A falling segment's blocks are removed when its rubble swaps in, so the frame gets smaller as the round goes on.

### 3.10 Visual identity
**Palette**

| Group | Colours |
|---|---|
| Brick | red `#9C4A30`, dark course `#7E3A26`, mortar `#CDBB9E`; hotel buff `#B8774E`; terracotta trim and base `#E3C9A0` |
| Concrete | `#8F8A80`, dark `#5F5A53`; fresh break: concrete `#C9C2B4`, brick `#D2BFA3` |
| Steel and timber | bridge "elevated green" `#3E6B5A`, rust `#7A3E22`, timber `#B4834F` |
| Ground and roofs | asphalt `#3A3836`, cobbles `#6B625A`, tar roof `#2B2A2E`, lane paint `#E8D9A8` |
| Demolition marks | yellow `#F4B321`, black `#1C1A18`, danger red `#D6332A` |
| Windows | glass `#6E8F96`, lit `#FFB45E`, dark `#1F2A30` |
| Neon | MERCER blue `#4FA8FF` (map accent), coral shop neon `#FF5A4E`; kept high and small |
| Atmosphere | dust `#B8A68F`, soot `#4A403A` |

- **Pilot readability:** keeping teal out of the environment protects only the default skin (`#E5E7EB` / `#70E1D0`). Bots use `#70e1d0`, `#ff776d`, `#bca7ff`, `#eac558`, `#75aaff`, `#f09fc7` and `#a3d876` (`Core/NativeRoomOptions.cs:27`). Three clash with this palette (`#eac558` with demolition yellow, `#ff776d` with the coral neon and danger red, `#75aaff` with MERCER blue), and players can pick any colour. So readability comes from value, not hue: the lower 1.8 m of every wall is 22% darker (a grime ramp); yellow, red, coral and blue stay small and above 3 m except on the bands; G3 checks all 7 bot colours plus the default against brick, concrete and dust, with a `#eac558` pilot beside a band as the worst case.
- **Looks** are presentation keys: `brick-red`, `brick-buff`, `terracotta`, `concrete-board`, `steel-green`, `timber`, `tar`, `asphalt`, `cobble`, `window-lit`, `neon`. **Glass is presentation-only:** windows are open holes in collision, and the glass shatters when the panel next to it breaks, when an explosion lands within 6 m, or when its segment is stressed.
- **Architecture:** 1920s walk-ups built from boxes:
  - windows set 0.15 m back, with sills and lintels sticking out 0.12 m;
  - stepped three-box cornices;
  - terracotta string courses at each floor line;
  - quoins at the hotel corners;
  - shopfront arches built from stepped voussoir boxes;
  - fire escapes and awnings on the frontage only, above 4 m;
  - boarded windows and spray-painted demolition marks.
- **Landmarks:**
  - The Hotel Mercer, with a 6 m blue MERCER blade sign on its NE corner. The sign rides the NE quadrant down and **flickers out over 3 s** after it lands.
  - Two banded kiln chimneys with white demolition notches.
  - Two 45 m tower cranes outside the walls, with red aviation lights.
- **Lighting rig "golden-demolition":**
  - Sun: `#FFC27A` at 1.20, elevation 30°, **bearing 322°** (NW; the light travels toward 142°). That is perpendicular to the team axis, so at spawn Team A (facing about 52°) and Team B (about 232°) each have it 90° to one side. A sun at 292° would sit behind Team A but 60° to Team B's right. Soft shadows, strength .85.
  - Rim: `#7FA8FF` at .36, opposite the sun, no shadows.
  - Trilight ambient: sky `#8DA6D6`×.50, equator `#C7906E`×.34, ground `#3B2A22`×.42.
  - Shadows: 4 cascades over 75 m (today 2 over 65 m; the extra passes count in §2.4).
  - Optional: the sun drops from 30° to 26° over the round, as a pure function of match time.
- **Fog and sky:**
  - Linear fog `#CF9A74` from 38 to 175 m, plus 15% ground haze from 0 to 2.5 m in the shader.
  - Dusk sky (needs the new sky properties, §2.2): zenith `#2E4470`, horizon `#F29A5E`, sun glow `#FFC38A`, sun disc `#FFE6B8` (size .05, halo .25), no stars, warm clouds at 0.3× speed.
- **Practical lights** (8, through `NativeBlockLit`'s own light arrays):
  - 4 sodium lamps `#FF9A3C` on the lane frontages: north at (−18, 4.6, 25.6) and (0, 4.6, 25.6), south at (18, 4.6, −25.6) and (0, 4.6, −25.6);
  - the MERCER sign `#4FA8FF` at (8.6, 7.5, 6.6), with a 0.15 s stutter about every 11 s;
  - 2 yard fire barrels `#FF7A2E`, flickering at 6–9 Hz;
  - 1 crane floodlight `#FFF1D6` from outside the walls, washing the hotel's north face.
  - About 35% of windows are lit; they strobe when their segment is stressed and go black when it falls.
- **Backdrop** (C#, deterministic; also drawn in the menu):
  - a second ring of condemned blocks, some half-demolished, with exposed slab edges, rebar and wallpaper;
  - a brick rail viaduct behind the south frontage, with a train every 45 s;
  - a skyline with a warm rim;
  - the two cranes;
  - an invented "MERCER PLAZA — COMING SOON" billboard;
  - bird flocks and two distant smoke columns.
- **Atmosphere VFX** (scaled by EffectsQuality; off or halved on `performance`):
  - 6 sun shafts;
  - 200 dust motes near the camera;
  - 30 paper scraps;
  - 14 pigeons, which scatter on any explosion or stress event within 18 m and return after 25 s;
  - flapping tarps;
  - collapse dust drifting east at 0.7 m/s;
  - a **dust coat** that greys nearby surfaces as the round goes on.
- **Destruction look:**
  - masonry break: a burst of brick-half voxels plus a mortar puff;
  - falls: debris streamers and wires snapping;
  - **landing dust wall:** 24–40 billboards expanding from 4 to 14 m radius in 1.6 s, up to 8 m tall; opacity .5 for 1.5 s, .2 by 3 s, gone by 7 s; thinned within 3 m of the camera; **at most 120 live billboards map-wide**;
  - a ground shock skirt and 40–120 debris voxels;
  - rubble faces that start fresh and fade to dusty over 10 s, with smoke wisps for 15 s;
  - camera shake by distance (within 25 m, scaled by mass), only when `prefs.cameraShake` is on (it defaults to off, `ConversionGame.cs:10`).

### 3.11 Audio
- **Ambience:**
  - Bed 1: distant city, 60–90 s loop.
  - Bed 2: gusting wind through broken windows and tarps, 30–60 s.
  - Each bed is an OGG under 1 MiB on a looping source (the Slender-drone pattern, `NativeSlenderPresentation.cs:15`).
  - Positioned spots: tarps, pigeons, crane whine, the viaduct train (synced to the visual), a far siren about every 70 s, fire-barrel crackle, neon buzz, and a **stress layer** of creaks whose rate rises with the worst member stage.
- **Destruction** (at most 6 rubble voices):
  - pier hit: thud plus crack;
  - stage changes: crack and gravel at S1, rebar ping at S2;
  - stress: a groan rising in level;
  - collapse: snap, a 40–60 Hz sub boom, a 2–3 s rumble, glass layers, then a 3–4 s debris tail; low-passed with distance;
  - landing thuds by material;
  - chimney: a long creak, a whoosh, then four thuds;
  - **one structure-level cue per segment**, replacing today's one explosion sound per block.
- **Udio prompt:**
  > "Instrumental big beat breakbeat, 128 BPM, 4/4, gritty and kinetic. Chopped funk drum breaks with a fat compressed snare, distorted analog bassline, punchy brass stabs, wah-wah guitar riffs, metallic industrial percussion (hammered steel, chain rattles, construction clanks), filtered siren swells, light vinyl crackle. Mood: sunset over a city block being demolished: confident, dangerous, high energy. No vocals. Consistent energy throughout, mixed loud and clean, seamless loop: the ending flows straight back into the beginning, no fade-in, no fade-out, no silence."
- **Structure** (76 bars, about 2:22):
  - A: 16 bars, groove;
  - B: 16 bars, brass hook;
  - break: 8 bars, tension bed;
  - A′ and B′: 16 bars each;
  - turnaround: 4 bars back into A.
- **Id:** `brickfall-demolition-row`. Delivery spec in §8.

---

## 4. Alternatives (short form)

| | **Skyframe** (gameplay judge's #1) | **Colossus Drydock** (most memorable) |
|---|---|---|
| Pitch | A half-built steel high-rise between two brick party walls. Three designed levels (0 / 3.6 / 7.2 m) meet at a tower-crane collar. "Bridges become ramps, perches become rubble": 10 kinematic hinge and drop groups. | A shipbreaker's graving dock with a 19 m box-built colossus held up by two struts. Blow a strut pin and it topples (4 s telegraph) onto that side's Pump House. Its body becomes a ridge, a tunnel and a spine route. |
| Size | 56×48 m, 172 boxes; fits the bot clamp exactly | 62×52 m, 261 blocks; fits today's clamps |
| Best at | The lowest risk and budget; the most learnable rule; spans turn into walkable 16.7–21° ramps; ground connected in all 1,024 states; all 16 opposing spawn pairs checked in 3D | One trailer moment visible from every spawn; the exact telegraph (the decal is the sim volume); a sunken basin gives melee its own route |
| Weak at | Steel frames and cores are indestructible, so there are fewer true "buildings coming down". Gas racks break to 2 rifle shots. Camera clipping in the 3.3 m lobbies. | Buildings collapse through host-only physics. The core crush (150) kills outright. Melee is shut out of the pin decision. The colossus is a large art job (about 700 parts), and it is the same event every round. |
| If picked | Light it overcast or under night floodlights. Racks get 120 HP or become explosion-only, armed at fight + 8 s. Add pancake segments to the annexes and the outer bays of the frames. | Light it at blue hour or in a storm. Build it on Brickfall's segment system (topple mode) instead of physics. It is better as Arena 2 or 3. |
| Effort (designer estimate) | Astra 13–16 days, Claude 14–18 days | Large; 3 phases |

**Redline Junction** (a desert rail cut with a clock-driven train) is parked. Today's bots would jump its 1.0 m parapets into the cut, because their obstacle ray starts about 0.45 m above the feet and any hit triggers a jump (`NativeBots.cs:33`, verified). It can come back later as a hazard map with scripted collapses.

---

## 5. Structural destruction contract

### 5.1 Files and owners
| File | Owner | Content |
|---|---|---|
| `Assets/Conversion/Resources/UnityMaps.json` (new) | **Astra** | Sim content for Unity-authored maps: blocks, roles, segments, spawns, bounds, hazards, nav. Has its own `schema` and `contentRevision`. **Never generated by `export_source.mjs`.** |
| `Core/ConversionContent.cs` | Astra | Optional schema fields, loader merge, validation |
| `Core/NativeStructureRules.cs` (new) | Astra writes; Claude reads | Segment state machine, pure pose functions, crush volumes, seed rule. Presentation draws telegraphs from these same definitions. |
| `Runtime/NativeDestruction.cs`, `NativeArena.cs` | Astra | Damage filters, segments, rubble swap, events, static split |
| `Runtime/NativeNetwork*.cs` | Astra | `contentRevision` in the host hello check (`NativeNetwork.cs:82`) and frame check (`NativeNetworkFrame.cs:36`), which refuse old clients at join; a `segments` array; protocol bump. The relay and LAN stay unchanged: the relay is a separately hosted service that requires `source` to be exactly 40 hex characters and filters rooms by it (`Relay/Program.cs:14,16,24`), so changing it needs a redeploy and owner authorization, and LAN datagrams have a fixed 4-field format of at most 160 bytes (`Core/NativeLanDiscovery.cs:21`). `Core/NativeTransport.cs` and `Runtime/NativeRelayUI.cs` are untouched. |
| `Runtime/NativeBots.cs`, `NativePveHorde.cs` | Astra | Minimal nav graph (step 3), links switched by segment state (step 5); cover search and PvE rebake that still see `static` blocks (§5.3) |
| Clamps: `NativeAbilities.cs:131`, `NativeChips.cs:82`, `NativeLeet.cs:96`, `ConversionGame.cs:158`, `NativeBots.cs:36,38` | Astra | All five from one Core map-bounds function, minus the capsule radius; old maps keep today's values |
| `Core/ArenaModel.cs` (`EventKind`) | Astra | New event kinds |
| `Assets/Conversion/Resources/MapLooks.json` (new) | **Claude** | Light rig, sky, fog, practical lights, look for each block id or prefix, dressing, backdrop, atmosphere, ambience ids. **Not part of the network frame.** |
| `Runtime/NativeLighting.cs`, `Core/NativeEventFeedback.cs` (shake rule, sound routing) | Claude | Presets; distance-based shake; routing for the new events |
| New: `NativeArenaDressing`, `NativeDressingKits`, `NativeBlockLit.shader`, `NativeStructureFx` plus its presentation file (not named `NativeCollapse*`, which is the corpse solver), Brickfall scenery, ambience | Claude | Presentation |
| `Editor/NativeBuildChecks.cs`, `Core/NativeParityChecks.cs`, `Tests/*` | Astra (Claude adds presentation checks) | Gates |

### 5.2 Per-map data (sketch)
```json
{ "schema": 1,
  "materials": [ { "id": "brick", "color": 10242608, "mass": 26, "durability": 120, "friction": 0.85, "indestructible": false,
                   "density": 1.9, "bulletScale": 0.25, "meleeScale": 0.5, "blastScale": 1.5, "debrisScale": 1 } ],
  "maps": [{ "id": "brickfall", "name": "Brickfall", "tag": "Condemned brick district at golden hour. Blow the piers, rewrite the streets.",
    "theme": "brickfall", "music": "brickfall-demolition-row", "ambience": "brickfall", "look": "golden-demolition", "rotation": false,
    "bounds": { "minX": -30, "maxX": 30, "minZ": -26, "maxZ": 26, "ceiling": 24, "killY": -6 },
    "spawns": { "ffa": [ { "pos": {"x": -16, "y": 1.1, "z": 9.5}, "yaw": 121 } ],
                "teams": [ { "side": 0, "points": [] }, { "side": 1, "points": [] } ] },
    "blocks": [ { "id": "hotel-pier-nw", "material": "brick", "pos": {"x": -8, "y": 1.8, "z": 6}, "size": {"x": 1.2, "y": 3.6, "z": 1.2},
                  "rotation": {"x": 0, "y": 0, "z": 0, "w": 1}, "role": "structural", "hp": 140, "nonBlast": 0, "stages": [0.66, 0.33] } ],
    "segments": [ { "id": "hotel-nw", "building": "mercer", "mode": "pancake", "blocks": [],
                    "supports": [ { "member": "hotel-pier-nw", "segment": "", "w": 1 } ], "failBelow": 1,
                    "telegraph": 1.2, "stagger": [0, 0.25, 0.45], "maxAccel": 6.9,
                    "crush": { "center": {"x": 0, "y": 0, "z": 0}, "size": {"x": 0, "y": 0, "z": 0}, "damage": 45, "push": 6, "cap": 60, "capHpFraction": 0.6 },
                    "rubble": [], "nav": { "remove": [], "add": [] } } ],
    "hazards": [],
    "nav": { "nodes": [ { "id": "n1", "pos": {"x": 0, "y": 0, "z": 0}, "layer": 0 } ], "links": [ { "a": "n1", "b": "n2", "kind": "walk", "requires": [] } ] } }] }
```
- **Format:** content is parsed with `JsonUtility` (`ConversionGame.cs:32`). Vectors and rotations are `{x,y,z}` / `{x,y,z,w}` objects as in `GameContent.json`; materials are an array with an `id` (no dictionaries); colours are ints (`ConversionContent.cs:26`; `#9C4A30` = 10242608); there are no nested arrays; missing fields read as 0, null or false, so `mass` and `friction` must be written out. `contentRevision` is a hash of the file computed at load, not a field in it. `maxAccel` is in m/s².
- **Every new field is optional.** The defaults reproduce today's `ContentBlock {id, material, pos, size, rotation, support[], dynamic, mass}` exactly (`ConversionContent.cs:21-22`). The old `support[]` "all supports gone" rule (`NativeDestruction.cs:51-52`) stays for the old maps.
- **Supports** list members (blocks) or segments (`{ "segment": "facade-ashby", "w": 1 }`). That is how cascades are written.
- **Cascade:** recompute on every member death or state change, transitively, in segment-id order, within a per-tick budget. A segment that is stressed, falling or rubble counts as lost.
- **States:** 0 intact → 1 stressed (telegraph) → 2 falling (pose from t0) → 3 rubble (static).
- **Mass:** derived from volume × density, for fragments.
- **Seed:** presentation randomness uses `hash(mapId, segmentId, roundIndex)`, so it is identical on every client and in the killcam and never needs to be sent.

### 5.3 Replicated state (small)
- **Frame:** `segments: [{id, state, t0}]`, a few bytes. Blocks of a falling segment are posed locally from (segment, t0), with no 20 Hz transforms. Guest movement prediction casts against the same computed pose.
- **Already sent:** member and masonry health in `NativePropFrame.health`. Damage stage = health / content `hp`.
- **Killcam:** `NativeReplay` records `NetworkFrame`, so the segments travel with it.
- **Static split:** only blocks tagged `static` leave the per-frame prop list and the 360 cap; not every indestructible block, because `foundation` is indestructible on all 15 maps. What assumes every block is a prop changes with it:
  - guests delete any local prop missing from the frame and snap every prop to the frame transform (`NativeNetworkFrame.cs:108`), and the killcam uses the same path (`NativeReplay.cs:59`), so static and locally posed segment blocks are exempt there;
  - `Editor/NativeBuildChecks.cs:158` requires `Props.Count == map.blocks.Length` for every map, and `:152-156` checks floors only under the global spawns;
  - bot cover search iterates the prop list (`NativeBots.cs:32`), so static kiosks, shelters, the truck and the storerooms must stay visible to it;
  - the PvE nav rebake polls `Props.Count` (`NativePveHorde.cs:151`).
- **Fragments:** fragment definitions carry their `parent` id, so fragments keep their parent's look.

### 5.4 Events: what exists today and what is added
| Event | Today | Brickfall contract |
|---|---|---|
| `Break` | Detail = material, SurfaceId = block id (`NativeDestruction.cs:43`) | Unchanged |
| `Collapse` | Detail = block id, once per block (`:52`); plays the explosion sound | Kept for old maps only |
| `Explosion` | Detail = effect, Radius, Amount (`NativeChips.cs:33`, `NativeAbilities.cs:165`) | Unchanged; the source of structural damage |
| **`StructureStress`** | n/a | Detail = segment id, Position = centroid, Radius = extent, Amount = telegraph seconds, SurfaceId = first lost member |
| **`StructureFall`** | n/a | Detail = segment id, Position, Radius, Normal = fall direction, Amount = mass in tonnes, SurfaceId = dominant material |
| **`StructureLand`** (settle and rubble swap) | n/a | Detail = segment id, Position, Radius, Amount = energy 0..1 |
| `RubbleImpact` (G2, optional) | n/a | Position, Detail = material, Amount = energy; at most 12 per second |
| Crush hit | n/a | Hit with damageKind `collapse`, Detail = segment id, through a new damage path that can hit the instigator (§3.9 rule 8) |
| Hazard phases | Pure functions of match time (`NativeHazards.cs`) | The Wrecker (G3) and the arm delay are pure functions of round, FightAt and time. Presentation calls the same Core function, so nothing new is sent. |

**Camera shake** is decided on the presentation side from the event's Position and distance. Astra can keep emitting `Target=0`.

### 5.5 Engine limits Brickfall runs into
- **Gibs:** kill-effect voxels land on a flat floor at y 0.05 everywhere inside ±32/±28 (`Core/NativeKillEffectRules.cs:84,411`), so kills on the 3.6, 7.2 and 10.8 m floors drop gibs through the building. Claude gives the kill-effect floor a per-map height query.
- **Camera:** decoration above 3 m has no collider, and the 4.4 m third-person camera sphere-casts only against colliders (`ConversionGame.cs:200`), so it passes through cornices, fire escapes and awnings. Claude fades dressing between the camera and the pilot; no colliders are added.
- **Round length:** overtime (§3.6) ends most rounds soon after 100 s of fighting.
- **Pilot height:** the y 24 ceiling doesn't stop pilots, and jump chips stack (§3.2).

---

## 6. Work split for Arena 1

### 6.1 Order and handoffs
| Step | Astra (sim) | Claude (presentation) | Handoff |
|---|---|---|---|
| 0 | Review this contract; agree it in PR notes | Commit this proposal plus the draft layout and LOS scripts under `docs/arenas/` on a `claude/` branch | **H0:** contract agreed (DEV_LOG and PR notes) |
| 1 | `UnityMaps.json` loader and validation; `contentRevision` in the hello and frame checks (relay and LAN unchanged); protocol 5 → 6; exempt Unity maps from the parity counts, the `map-<id>` model requirement and the `maps[0]` heavy suite; build the Random pool from the `rotation` flag instead of `id != "siren"` (`NativeLobbyLogic.cs:14`, `NativeArena.cs:71`) | Lighting presets in `MapLooks.json` and the dusk sky (old maps stay identical; fix the forge-by-id sky so lavaworks matches Electron) | none |
| 2 | Static split for `static`-tagged blocks, with its side effects (§5.3); per-map spawns (FFA, two-team tables with the §3.7 fallback, sandbox respawn too); all five clamps from map bounds minus the capsule radius | Interim look colours on the grey-box; the menu backdrop draws arena blocks; arena card and tag | none |
| 3 | A deterministic Brickfall grey-box generator built from §3.3 (static blocks, cover, members, masonry; segments inert; blocking above unstandable tops); a minimal bot nav graph for the intact layout (walk, ramp and stair links, so bots climb the scissor stair and path through the hotel) | Apply the looks to the grey-box | **H1:** test build → **G1** |
| 4 | Damage classes; closest-point blast and one LOS rule for `AbilityBlast` and `ChipBlast`, for role-tagged blocks only (today `AbilityBlast` damages props with no LOS check and reach = radius + 0.35 × box diagonal, `NativeAbilities.cs:159-163`, while `ChipBlast` checks LOS, `NativeChips.cs:37-38`; old maps keep that); the segment system (cascade, arm delay, poses, crush and its damage path, caps, credit, rubble swap, ejection); replication; events | Telegraph layer generated from the Core segment definitions; first pass of the destruction VFX and audio | **H2:** events frozen → **G2** |
| 5 | Full nav graph (drop and jump links of 1.2 m or less; links switched by segment state; avoid stressed and falling volumes; rubble counts as cover); the PvE rebake hook keyed to segment state, not `Props.Count` | ArenaDressing and `NativeBlockLit`; dressing kit, camera fade, per-map gib floor, backdrop, atmosphere, ambience, music | **H3:** art build → **G3** |
| 6 | Tests (§7); tuning; frame-budget hardening | Performance capture, renders, reduced-effects pass, DEV_LOG | none |

- **Animation impact:** none on hit timing, hitboxes or cooldowns. New damage sources: collapse crush and fell lines. New movement case: riding a pancaking slab down (vertical only, ≤ 6.9 m/s²).
- **Test builds:** they go to the existing test Update channel under the standing instruction, but **only after `contentRevision` ships**, so old clients are refused at join rather than failing mid-match.

### 6.2 Removing the 14 old maps later (after G3; a separate Astra PR)
These items break if the maps are simply deleted.

*Startup and lobby code (Astra)*
- `ConversionGame.cs:10,38`: the saved-map fallback is `"moonlit"`, and `content.Map` then throws in `Awake`. That is a fatal startup error for players with a saved map.
- `Core/NativeRoomOptions.cs:8,12,20`: the default map, `Normalize` (which throws when no non-siren map exists) and the Random preview.
- `NativeLobbyLogic.cs:14`: `uint % 0` when no map is in the pool. `NativeArena.cs:71`: randomize-each-set.
- `NativeDedicated.cs:14`: defaults to `foundry`. `NativePreview.cs:13`: uses `foundry`.

*Checks (Astra)*
- `NativeParityChecks.cs:18` (15 maps, 3451 blocks).
- `NativeBuildChecks.cs:113,159`.
- `test_cinematics.py:165-169`, `ParityFollowupChecks.cs:11`, `NativeNetworkBuildChecks.cs:15,119`, `EventFeedbackChecks.cs:108,112`.

*Pipeline (Astra)*
- Re-running `export_source.mjs` restores all 14 maps. Use a Unity-side allowlist instead of editing `GameContent.json`.

*Claude*
- Lighting and scenery branches keyed by map id (`NativeLighting.cs:66-71`, `NativeSceneryMotion.cs:21`).
- The rotation music bag (`NativeAudio.cs:16`).
- The "N ARENAS" count (`NativeHomeMenu.cs:177`).
- The menu backdrop default.

**Owner:** decide whether the old music tracks stay. Deleting files breaks the 83-clip audio gate.

### 6.3 Ready-to-paste prompt for Astra
> **Astra:** the owner picked a first replacement arena, **Brickfall** (map id `brickfall`). Spec: `docs/arenas/ARENA_PROPOSAL.md`, with the draft layout and LOS scripts in `docs/arenas/scripts/`. The owner's decisions are at the top of that file (self-crush yes, team crush follows friendly fire, Wrecker deferred, `rotation:false`, rebuild every round, kill credit to the last damager within 6 s). It is a condemned brick district whose buildings collapse when explosives break their yellow-banded piers. Claude owns all presentation: lighting, looks, dressing, destruction VFX, audio, menus. You own collision, the structural sim, spawns, bots, networking and tests. Base your work on the newest Unity integration head (currently `claude/sleepy-ritchie-g8ruhv` = `codex/unity-cloud-current`, `40009f1`; check it first) on a `codex/` task branch. **Keep all 14 old maps.**
> **(A) Grey-box:**
> 1. Add a `UnityMaps.json` loader (its own schema, parsed with JsonUtility in the §5.2 formats, never touched by `export_source.mjs`; `contentRevision` is a hash computed at load). Check the revision in the host hello (`NativeNetwork.cs:82`) and frame validation (`NativeNetworkFrame.cs:36`), and bump the protocol, so mismatched builds are refused at join. Leave the relay and LAN discovery unchanged; the relay is a separately hosted service and changing it needs a redeploy and owner authorization.
> 2. Exempt Unity maps from the parity counts, the `map-<id>` model requirement and the `maps[0]` suite.
> 3. Build the Random pool from the `rotation` flag. Brickfall ships with `rotation:false`.
> 4. Split blocks tagged `static` (not every indestructible block: foundation is indestructible on all 15 maps) out of `Props`, the frame and the 360 cap, and fix what assumes every block is a prop (§5.3): guest and killcam prop deletion and snapping, the `Props.Count == blocks.Length` build check, bot cover search, the PvE rebake poll.
> 5. Add per-map spawns (FFA 8, plus two 4-point team tables used only for exactly two teams of at most 4 each, otherwise FFA by seat; §3.7) and use them for sandbox respawn too.
> 6. Derive all five clamps (`NativeAbilities.cs:131`, `NativeChips.cs:82`, `NativeLeet.cs:96`, `ConversionGame.cs:158`, `NativeBots.cs:36,38`) from map bounds minus the capsule radius; old maps keep today's values.
> 7. Generate the Brickfall grey-box from §3.3 (64×56), with pilot-only blocking above the frontage, chimney and stair-house tops (§3.2), and a minimal nav graph for the intact layout so bots climb the scissor stair and path through the hotel.
> **(B) Collapse:**
> 1. Add block roles and damage filters (structural members take damage only from explosion sources, with demolition capped at ×1.5; static and trampoline never touch props). For role-tagged blocks only, measure blast distance to the closest point on the box and use one LOS rule for `AbilityBlast` and `ChipBlast`; old maps keep today's blast behaviour.
> 2. Add the segment system per §3.9 and §5: 10 segments, weighted supports with `failBelow`, a transitive cascade where falling counts as lost, a 4 s arm delay, a telegraph, pancake/tear-hinge/peel/topple poses computed from (segment, t0) on every client (riders carried in pancake mode only, riding slabs at ≤ 6.9 m/s²), crush 45 through a new damage path that can hit the instigator (capped at min(60, 60% of max HP) per pilot per segment event, all sources combined, after multipliers; debris grouped by segment id), a rubble swap with ejection, at most 3 concurrent falls, at most 80 fragments that freeze at 2.5 s and crumble at 20 s, and credit by structural damage in the last 10 s.
> 3. Replicate `segments [{id, state, t0}]` and emit `StructureStress`, `StructureFall` and `StructureLand`.
> **(C) Bots and tests:**
> 1. Bots: extend the A.7 nav graph with links switched by segment state, avoiding stressed and falling volumes.
> 2. Tests: cascade determinism; ground connectivity across all 2^10 segment states; spawn safety (including fall sweeps) and jittered LOS; the hotel opening-LOS rule; the per-build jump table against the unstandable tops; no intact prop inside a crush volume; frame size at the round-start peak and with every segment down; host, guest and killcam pose parity; old-client refusal.
> Do not change visuals or shaders. Record actual results, unrun checks and the Windows multiplayer limits in DEV_LOG and the PR. The tank, tenement floors and the Wrecker come later.

---

## 7. Milestones and what "playtested" means

| Gate | Build contains | Owner checks |
|---|---|---|
| **G0: design sign-off** | This document | Concept chosen; §8 decisions answered |
| **G1: grey-box** | Steps 1–3: static blocks, cover, masonry, spawns, map-bounds clamps, minimal bot nav graph, preset lighting, interim looks | With 2, 4 and 8 pilots plus bots: first contact in 3–7 s; no spawn exposure; every route gets used; no single perch dominates (hotel roof, lanes); bots move through ground and stairs and are never stuck for more than 5 s; layout and size feel right; frame time on the owner's PC |
| **G2: collapse** | Step 4 plus the telegraph layer, first-pass VFX and audio | Each segment can be dropped by each explosive kit in the expected time. Players read the telegraph and get out. **No full-HP pilot dies to a single segment** (cap after multipliers, relative to max HP). Credit is correct. Rubble is walkable and no one gets stuck. Ground stays connected. Dust doesn't hide fights for long. The guest view is smooth (no snapping) and the killcam matches. Body count and frame size at the peak are measured. FPS during the worst collapse is measured. Fire-primary pier time is measured. 3–6 collapses per round (base stats). A melee-heavy lobby still gets a story. |
| **G3: art pass** | Step 5: dressing, shader, backdrop, atmosphere, audio, music | 144 fps target on the owner's PC; draw counts within budget; reduced-effects and `performance` pass; pilot readability against brick; the owner's call that it is a "great design". Owner Windows multiplayer playtest. |
| **Then** | | Design Arenas 2–3 (Skyframe and Drydock, re-lit, both on the segment system), then remove the old maps per §6.2 |

**Not verified anywhere in this document:** Unity renders, Windows runs, multiplayer, real frame sizes, bot behaviour, performance, and whether the capsule climbs a 22° rubble ramp. A passing build is not proof of any of these.

---

## 8. Owner asks

### 8.1 Music delivery spec (every Udio track)
| Item | Spec |
|---|---|
| Master | WAV, 48 kHz (44.1 also fine), 24-bit, stereo. Kept outside the repo (a Drive link). |
| Loudness | **−14 LUFS integrated (±1)**, true peak **−1.0 dBTP or lower**, loudness range about 8 LU or less, all tracks within ±1 LU of each other. This matches your Ward Zero tracks (−13.3 and −12.5 LUFS). Today's map tracks sit at −16 to −20 LUFS, and there is no runtime normalisation. |
| Loop | **A seamless whole-file loop** cut on a bar line: no fade, no silence at either end, reverb tail folded into the start. That works with today's code. Tracks resume across rounds rather than restarting. |
| Length and tempo | 2–4 minutes; state the BPM, the time signature and whether the tempo drifts |
| Naming | Lowercase kebab-case ASCII id, which is also the file name (e.g. `brickfall-demolition-row`), plus a display title |
| What we commit | OGG at about q6 under `Resources/Audio/music/<id>.ogg`, an asset-lock receipt, an ASSET-CREDITS entry, and a credits file shipped with the game. The audio gate is updated to count `.ogg` (it currently expects exactly 83 `.mp3`/`.wav` files, `NativeBuildChecks.cs:127`). |
| Licence | Artist credit, original file name, creation date, and **your confirmation that your Udio plan allows use in a distributed game** (unverified by us) |

### 8.2 Decisions
1. **Concept:** Brickfall (recommended), Skyframe or Drydock.
2. **Self-crush and team crush:** can a pilot be hurt by a collapse they caused (recommended: yes; today an attacker is immune to their own debris for 7 s)? Can their teammates (recommended: follow the friendly-fire setting; today teammates are immune to each other's debris when friendly fire is off)?
3. **The Wrecker** (a forced, telegraphed, symmetric demolition at fight + 50 s, behind a map flag): add it in G3, or skip it?
4. **Round reset:** keep rebuilding the map every round (recommended).
5. **Random pool:** is Brickfall selectable only until G3 (recommended), or in Random right away?
6. **Tone mapping and bloom** (open decision 11): decide before the art pass, because it changes how every light rig is tuned.
7. **Kills with no structural damager** (Wrecker, or a collapse whose trigger has expired): credit the last pilot who hit the victim within about 6 s, or nobody?
8. **Roster lighting plan** (§2.5), and whether Ward Zero PvP gets its own music track.
9. **Old music:** does it stay after the old maps are removed?

---

## Appendix A: draft files
- `docs/arenas/scripts/brickfall_m1.py`: the blockout painter, spawn LOS check (jitter × 3 eye heights), spacing and volume-clearance checks (including fall sweeps), and the hotel opening-LOS check. It prints its report to stdout.
- `docs/arenas/scripts/elev_m1.py`: sections A-A and B-B (stdout).
- The original concept drafts (Brickfall at 72×60, Skyframe, Colossus Drydock, Redline Junction) stayed in Claude's session scratchpad and are not in the repo; §4 summarises the two alternatives.

**Verified by reading code at `40009f1`:**
- the global spawn table (`NativeArena.cs:85`);
- the bot obstacle ray and its ±28/±24 clamp (`NativeBots.cs:33-36`);
- the shake rule requires `target==local` (`NativeEventFeedback.cs:56-58`);
- the single-level supports and the 360 cap (`NativeDestruction.cs:41-52`);
- the parity pin of 15 maps and 3451 blocks (`NativeParityChecks.cs:18`);
- the map-model and audio gates (`NativeBuildChecks.cs:113,127`);
- the y 24 ceiling, which stops projectiles only (`NativeSpecialRules.cs:42-46`);
- the moonlit fallbacks and Random selection (`ConversionGame.cs:38`, `NativeRoomOptions.cs:8,12,20`, `NativeLobbyLogic.cs:14`);
- frame validation (`NativeNetworkFrame.cs:36`);
- the lighting branches keyed by map id (`NativeLighting.cs:66-71`);
- 7 support links and 4 materials in `GameContent.json`;
- `NativeLit` is 28 lines;
- the 144 fps target;
- the clamp sites, jump and gravity stats and chip stacks, blast and debris rules, damage multipliers, static-split side effects, team ids, relay and LAN limits, light arrays, sky properties, shadow settings, kill-effect floor, overtime and camera cast cited in §3.2–§6.

**Estimates, not measurements:** frame bytes, area figures, member costs (these assume closest-point blast), and the effort figures.
