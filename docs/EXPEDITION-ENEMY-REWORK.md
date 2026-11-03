# Expedition enemies off voxels, the Backrooms' own entities and their surroundings

Owner, 2026-11-03: "Begin reworking the other expedition enemies; Instance unique ones. Since we moved away from voxels; This
includes bosses. Ensure very detailed and high quality designs, make sure you review and ensure great detail / animations before
completing. Create unique enemies for backroom levels (Poolrooms and level 0), and design the "Surrounding area" around the
instance for these as well."

Split as usual: the looks, models, animation and surroundings are Claude's; new kinds' behaviour, stats, spawns and network
are Astra's (section 4 is the proposal Astra builds from). Status lines live in docs/DEV_LOG.md.

## 1. What there is (survey, 2026-11-03)

| Instance | Kind | Built today | Notes |
|---|---|---|---|
| Central Station | `linesman` | voxel on the zombie rig (`Core/NativeStationModels`) | brute body; hi-vis vest, hard hat, cable reel; battery pack weak point |
| | `conductor` | voxel | tall shambler; uniform and cap; red lantern prop |
| | `undertow` | voxel | drowned runner, swims submerged; grab-drag |
| | `pileup` | voxel | three fused commuters on a brute body; carriage door shield; second head weak point |
| City Ironworks | `cinderling` | voxel (`Core/NativeIronworksModels`) | charred runner, ember core, packs of 3 |
| | `stoker` | voxel | heat suit, rake prop, hook pull |
| | `slagback` | voxel | brute under a slag crust that sheds |
| | `ladleman` | voxel | big brute with a molten ladle |
| | `rigger` | voxel | hangs from girders on a chain, hook drop |
| Ward Zero boss | the Siren | the old source model bank plus unit-box detail pieces | captured source clips |
| Central Station boss | THE 6:47 | a code sculpt of about 190 boxes, 90 joints | 13 m walking carriage, 6 legs, the Stationmaster |
| City Ironworks boss | THE CONVERTER | a code sculpt of boxes and cylinders | 8 m Bessemer vessel on 4 legs, the foreman |
| Level 0, Poolrooms | none of their own | the common zombies only | the exit cache stands in for a boss |

Ward Zero has no unique kinds (it inherits the Siren's horde).

## 2. The approach

- **Humanoid kinds** (all nine zone kinds, the Backrooms' humanoids, the bosses' riders and the Siren) go on the player's model,
  the zombies' proven route (docs/ZOMBIE-MODELS.md):
  - a variant table per kind: wardrobe pieces, colours, body and face shapes, bone build, skin and shader wear;
  - the zombie motion stack (the ZOMBIE PRO mocap, the gait, the strike timing) with each kind's procedural overlay
    (`NativeStationMotion`, `NativeIronworksMotion`) carried onto the model;
  - the voxel body stays as the Reduced and Impostor tiers and without the bake, as for the zombies.
- **Their signature gear** is modelled in Blender, run headless from scripts (the same Blender 5.2 the Backrooms add-on uses):
  - detailed hard-surface props: hard hats, battery packs, cable reels, the conductor's lantern and ticket punch, carriage
    doors, the stoker's rake and hood, the ladle, the rigger's hook, chain and harness, slag crust plates;
  - bevelled edges, real thickness, inset panels, rivets, straps and buckles, at game budgets (a prop 300 to 3,000 triangles);
  - exported as FBX into a local bank (docs/PACK-BANK.md), riding the bones the voxel props ride today;
  - lit by the model's toon shader, so they sit with the bodies.
- **Shader looks per kind** on the zombie look (`NativeVisualBodyFx.cginc`): charred skin with ember cracks (cinderling),
  waterlogged pallor and wet sheen (undertow, the Poolrooms' drowned), slag crust, soot, hi-vis reflective tape.
- **Non-humanoid creatures** come from the owner's licensed creature packs where one fits a design (the Infinity PBR packs'
  rigs and takes, the PROTOFACTOR creatures); they are restyled to the toon look, never shown in their fantasy dress.
- **The bosses' machines** (the 6:47's carriage and legs, the Converter's vessel, yoke and legs) are rebuilt in Blender as
  detailed hard-surface parts on today's joints, so their rigs, motion library, colliders and rules stay as they are.
- **Review before done**, every kind (the owner's bar):
  - a turnaround sheet (front, three-quarter, side, back) and a face or detail close-up, on the PC;
  - an animation strip of each move at its authority timing: idle, walk, run, every attack's windup, hit and recovery, the hit
    reaction, the death;
  - a check that every hit volume, weak point and prop still sits where the rules put it;
  - side by side with the voxel version, then the owner's check in the Windows build.

## 3. Order of work

1. The Backrooms' surroundings (section 5): presentation only, no dependency.
2. The Backrooms' entities: this spec to Astra now; their looks and motion built against DEV previews while the rules land.
3. Central Station's four kinds on the model.
4. City Ironworks' five kinds on the model.
5. The bosses: the Siren on the model; THE 6:47 and THE CONVERTER rebuilt part by part.

## 4. The Backrooms' own entities (rule proposals for Astra)

Numbers are proposals in the house terms (the zombie kinds' table in docs/ZOMBIE-MODELS.md section 1). Astra owns the final
values, the AI and the network.

### Level 0 (`exp-backrooms`, and the PvP map as bots only if Astra wants)

**Faceling (common).** An office worker with no face: smooth skin from brow to chin, beige shirt and slacks yellowed by the
damp, a lanyard. It stands facing a wall, swaying, until it notices a pilot; then its head snaps round and it runs.
- Dormant facing a wall until a pilot comes within 10 m with sight, or gunfire lands within 14 m; 0.45 s turn (the tell),
  then hunts.
- Speed 1.4 m/s walk, 4.4 run; claw reach 1.8 m, hit at 0.32 s of a 0.9 s cycle, about a runner's damage; health as a
  shambler.
- Model: the player's model, male and female variants, the face shapes flattened and a blank face, office wardrobe.

**Hound (pack hunter).** A gaunt, hairless humanoid that runs on all fours, its arms too long, its spine arched, its jaw
hanging. Packs of 2 or 3.
- Gallop 6.0 m/s; a howl on alert (0.8 s) that sets every hound within 25 m hunting.
- Pounce from 3 to 6 m: 0.35 s crouch, a 0.5 s leap; on a hit the pilot is knocked down 0.9 s and bitten three times
  0.3 s apart; a miss leaves it sprawled 0.8 s.
- Health 0.8 of a runner's; capped at 6 alive.
- Model: the player's model, gaunt build, arms 1.25 long, a hinged jaw; a quadruped gallop (a new procedural gait over the
  crawl takes).

**Smiler (elite ambusher).** A tall darkness, 2.2 m, almost invisible, with a wide glowing grin and two pinprick eyes. It lives
where the lights have died.
- It stands and moves only in dark cells: the layout's `Dead` and `Flicker` circuits (Core `BackroomsLayout`), plus any cell
  whose fixture a pilot has shot out if Astra adds that.
- Blink: every 4 to 6 s, teleports to a dark cell within 12 m that no pilot is looking at; the fixtures round it stutter for
  0.5 s first (the tell).
- Strike: grin lunge, 2.6 m reach, 0.6 s windup, about two brute hits of damage, and a 1.5 s dread on the pilot (the
  presentation darkens and narrows the screen; no rule effect beyond the damage unless Astra wants one).
- Lit: in a lit cell it takes 1.5× damage and retreats toward the dark.
- Health 2.5 of a brute's; one alive at a time; enters once the quota is half done.
- Model: the player's model under a matte void shader (light-eating, edges fraying into smoke), a teeth and gums mesh with its
  own glow, the eyes; smoke trailing as it moves.

### Poolrooms (`exp-poolrooms`)

**Drowned (common).** A bloated bather in a striped vintage swimsuit and rubber cap, waterlogged and grey-white, goggles
fogged. Slow on the deck, fast in the water.
- Deck 1.0 m/s; wading it is not slowed (1.6 m/s); in a deep pool it swims submerged at 3.2 m/s, showing only a wake and
  bubbles, and surfaces with a lunge (2.2 m, 0.4 s) at a pilot within 1.5 m of the water.
- Idle face down, floating; health 1.2 of a shambler's.
- Model: the player's model, heavy build, pallor, wrinkled waterlogged skin, swim cap and goggles, a striped one-piece.

**Tiler (ambusher swarm).** A patch of white wall or floor tile that peels away and scuttles: a crab-like body of tile plates
and grout on six thin jointed legs.
- Dormant flat in a wall or the deck (the presentation shows it as tiles slightly proud of their neighbours, a hairline of
  wet grout round them); a pilot within 4 m wakes it: 0.6 s peel (the tell), then it scuttles.
- 5.5 m/s; a leaping bite, 2.5 m, light damage; 3 to 5 burst from one patch.
- Death: a burst of tile shards (cosmetic unless Astra wants a small splash of damage).
- Health 0.6 of a runner's.
- Model: the Infinity PBR spider rig and its takes, re-skinned as tile plates (white glazed tile, teal grout, chipped edges).

**Lifeguard (elite).** A gaunt 2.6 m figure in a faded red lifeguard uniform, whistle on a lanyard, a long rescue hook pole.
It watches from the lifeguard chairs.
- Whistle (1.0 s): every Drowned within 20 m surfaces and hunts for 6 s.
- Rescue hook: thrown from up to 7 m, 0.6 s windup; drags the pilot 4 m toward it, toward the deep water if it stands by
  a pool.
- Pole sweep: 2.6 m reach, 0.5 s windup, about a brute hit.
- Health 3 of a brute's; one at a time; enters once the quota is 40% done.
- Model: the player's model, tall and thin (1.4×), a red uniform, the whistle, the hook pole (Blender), sun-bleached hair.

### Spawns (proposal)

- Level 0: Facelings take the shamblers' share; Hounds in 1 of 3 waves; the Smiler as above; runners and brutes stay
  (fewer).
- Poolrooms: Drowned take the shamblers' share; Tilers from tiled walls near the player's route; the Lifeguard as above;
  runners and spitters stay (fewer).
- Each needs an intro wave like the zone kinds' (`NativeZoneCombat`), caps, and the generator's spawn anchors (wall
  patches for Tilers, dark cells for the Smiler, deep pools for the Drowned).

## 5. The surroundings (the generated world's districts)

**Built 2026-11-03** (`Core/NativeExpeditionCityKit.Backrooms.cs`, wired in `NativeExpeditionCityKit.Generated`; the join zones in
`Core/NativeExpeditionJoin.cs`). Seen in Unity on generated seed 123, which rolls both: the office park round Level 0's east
entrance, the baths round the Poolrooms' west one. `--generated-districts` covers them (placement rules, budgets, the halls).
Still to do: props in open ground wait for the blockout to leave room for them (anything below 3.1 m would block a route).

The generated outdoor map blends each building's look toward the instance it is near (Core `NativeExpeditionCityKit.Generated`;
the themes the authority rolls: `industrial`, `rail`, `quarantine`, and now `backrooms` and `poolrooms`, which today fall back
to the neutral town).

**Level 0's district, "the office park" (theme `backrooms`).**
- Low beige stucco offices and drop-ceiling interiors behind long ribbon windows, lit the same sick fluorescent yellow from
  inside, every pane the same; empty carpeted lobbies behind tinted glass doors.
- Mismatched EXIT signs, a few lit wrong; dead shrubs in concrete planters; faded parking-bay lines; sodium lamps.
- The entrance frame: an office lobby whose revolving door has been torn out, the wallpaper and the carpet running out past
  the threshold onto the pavement; "LEVEL 0" lettering; the accent a fluorescent yellow; a hum.
- The hall behind it: a long windowless office block, its roof plant humming, every window lit yellow.

**The Poolrooms' district, "the municipal baths" (theme `poolrooms`).**
- White tile and glass-block facades with teal bands, barrel-vaulted roofs, "BATHS" and "SWIMMING" lettering, pool-blue
  light spilling from frosted windows.
- Drained outdoor pools with chrome ladders, lifeguard chairs, a slide; tiled plazas with puddles; chlorine haze.
- The entrance frame: a tiled arch with turnstiles; "THE POOLROOMS"; the accent pool turquoise.
- The hall behind it: the baths' hall, its vaulted glass roof lit from within.

Both get their join-zone name, threat line and accent (Core `NativeExpeditionJoinZones`), street lamps in their light and their
ground marks, under the town's placement rules (nothing stands out more than the wall band below 3.1 m, nothing in a keep-out).
