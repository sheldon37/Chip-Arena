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
  - **The Central Station's four no longer use those tiers (2026-11-04):** few of them live at once (`NativeZoneCombat.Cap`: at most seven), so they stay on the model at any distance and rank first for its bodies; a frame without a body draws nothing rather than a voxel; each of their variants has a body made ahead on the map. The voxel body is left to a client without the bake and to Settings · Player model Classic.
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
   **Rules built** by Astra (#317, section 6). **Looks and first moves built 2026-11-03** (`claude/backrooms-entities`;
   docs/ZOMBIE-MODELS.md 4h): the Faceling, Drowned and Lifeguard live on the model; the Smiler its own smoke apparition (owner
   2026-11-03; 4i); the Hound a starved beast on all fours with its own doglike head (4j); the Tiler a crab of tiles, its
   body made in code (4k).
3. Central Station's four kinds on the model. **Built 2026-11-03** and live (`claude/station-models`; docs/ZOMBIE-MODELS.md 4g:
   the gear modelled in Blender, the tape, the fused commuter; reviewed in turnarounds, close-ups and attack strips on the PC).
4. City Ironworks' five kinds on the model.
5. The bosses: the Siren on the model; THE 6:47 and THE CONVERTER rebuilt part by part. **THE 6:47 built 2026-11-04** (docs/EXPEDITION-BOSS-CENTRAL-STATION.md section 14).

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

## 6. Authority contract (Astra, 2026-11-02)

Implemented on `codex/backrooms-entity-rules`, based on shared `dac9016`. Section 4 remains the design proposal;
this section records the implemented tuning. Dedicated models/motion, tells, sounds and screen effects remain Claude's.
Only Expedition uses these rosters; the PvP maps retain their existing behavior.

| ID | Base HP | Base damage | Movement (m/s) | Alive cap | Intro |
|---|---:|---:|---|---:|---|
| `faceling` | 60 | 4 | 1.4 / 4.4 | 12 | Opening |
| `hound` | 30 | 3 × 3 pin bites | 6 gallop; 10 pounce | 6 | 15% quota |
| `smiler` | 600 | 30 | 2, dark cells only | 1 | 50% quota |
| `drowned` | 72 | 6 | Deck 1; wading 1.6; deep 3.2 | 12 | Opening |
| `tiler` | 23 | 3 | 5.5 | 8 | 15% quota |
| `lifeguard` | 720 | 15 sweep / 3 hook | 1.4 / 2.2 | 1 | 40% quota |

Existing wave, party/elapsed-time and mutator scaling still applies. Hulkamania falls back to brutes in both areas because their authored entrances do not admit Hulks. The area cap is 16 before mutators. Hounds and Tilers
request packs of three through the existing entrance queue; caps, occupied landings and remaining quota can reduce a pack.
Hounds appear in waves 2 and 4; Tilers from wave 2; elites from wave 3. Runners/brutes remain in Level 0, runners/spitters
in Poolrooms. A temporarily unavailable intro anchor does not stop ordinary quota spawns; the intro retries.

- Facelings face an authored solid wall. Sight within 10 m, damage, or recent gunfire/impact/explosion within 14 m wakes them;
  turn tell .45 s, claw hit .32 s / .9 s cycle. Noise can carry through walls.
- Hounds wake on sight within 10 m or damage, howl .8 s to wake hounds within 25 m. Pounce at 3–6 m with sight: crouch .35 s,
  travel .5 s along the locked heading with body sweep/support checks, contact at .85 s; miss recovery .8 s, cooldown 5 s.
  A successful hit pins movement for .9 s, bites at contact / +.3 / +.6 (9 base total); weapons remain usable. Victim damage
  40, teammate damage 25, heavy impulse or CC breaks the pin. Death, lost visibility, range, disconnect and area departure release it.
- Smilers spawn in walkable Dead/Flicker cells. Both travel and blink paths must stay dark, unblocked and supported; no jumping
  across lit corridors. Blink attempts every 4–6 s: destination at most 12 m away, unoccupied and outside every living pilot's
  view, .5 s warning, conditions rechecked on completion. Lunge reach 2.6, windup .6, cooldown 2.5; dread lasts 1.5 s and is
  presentation only. External placement in light immobilizes it and makes incoming damage 1.5×. Shot-out fixtures are not added.
- Drowned use deep-pool anchors. A hittable .9 m capsule submerges them (the floor is only 1.3 m below the deck); surfacing
  restores 1.7 m. Water speed bypasses ordinary zombie wading drag. Lunge at 2.2 m: .4 s tell/contact, short swept advance
  during the last .15 s, 1.4 s cooldown. The existing water steps/nav provide their route onto deck.
- Tilers start at solid tiled walls, wake at 4 m with sight or on damage, peel for .6 s, then use a 2.5 m leaping bite
  (.4 s hit, 1.2 s cycle). Tile shards do no damage. Ground capsule .65 m × .3 m radius.
- Lifeguards spawn on deck beside deep water. The 2.6 m visual uses a 2.2 m capsule (stooped posture) to clear the one-cell arches; Claude should preserve this clearance. Whistle has 1 s windup, 12 s cooldown, surfaces nearby Drowned within 20 m
  for 6 s and gives them the target. Hook has .6 s windup, 7 m range, 7 s cooldown; pulls up to 4 m over .5 s toward nearby
  deep water (or the Lifeguard if no clear pool point), with a swept pilot body, no wall crossing and no overlapping hold.
  Pole sweep: .5 s windup, 2.6 m / 120° arc, 15 base damage, 2 s cooldown. CC cancels specials and releases holds.

Collision capsules (height/radius, metres): Faceling 1.7/.4; Hound .85/.35; Smiler 2.2/.4; Drowned 1.7/.42 (submerged .9/.42); Tiler .65/.3; Lifeguard 2.2/.42. Base rewards are 20 for the four common/pack kinds and 100 for the two elites, before existing reward modifiers.

**Claude/network hooks:** append-only horde IDs follow the Station's four, via `NativeBackroomsKinds.Kinds`; indices of
existing kinds stay fixed. The existing per-minion `zone` record now carries `dormant`, `wakeAt`, `actionTarget`, `blinkAt`,
`blinkX/Y/Z`, `dreadTarget`, `dreadUntil`; finite sentinel -100 for inactive times. `attack` adds `wake/howl/pounce/blink/lunge/hook/sweep`.
Reuse `lastAttack`, `grabTarget/grabAt/releaseAt` for pin, `pullTarget/pullAt/lockX/lockZ` for hook (`pullAt` is the windup start),
`lastCallAt` for whistle, `submerged/surfaceAt/aboardUntil` for Drowned. Smiler light stutter consumes `blinkAt` and the destination;
dread is local only when `dreadTarget` matches the pilot. Replica, late join and corpse frames copy this record.
Map identity versions were bumped so older manifests cannot join these rules. No new event enum or player save fields.

**Pending motion acceptance:** the borrowed biped Hound fallback slips a planted foot by 9.2 cm at the new gallop speed, and the generic Faceling mesh has a 2.6 cm shoulder gap.
The six new kinds are excluded from the existing captured-biped locomotion/shoulder quality certification until Claude supplies
their dedicated motion. Registration, finite fallback models and other existing enemy animation checks remain active.
This is an explicit unfinished presentation dependency, not a claim of animation acceptance.

### Level 0 encounter placement correction (2026-11-03, Astra)

Owner reports only encountering one zombie despite the native population check passing. That gate proved population, not proximity: Level 0's first 64 wall cells concentrated dormant Facelings along the north edge, while forced Expedition spawns gave every anchor the same distance weight. Sleeping enemies could fill the cap far from the explored route.

Level 0 now samples up to 160 wall anchors and 64 dark anchors across the complete maze (plus at most 24 existing roam anchors; total below the 256-entry wire bound). No geometry or enemy stats changed. Spawn candidates must be reachable from a living party member, at least 8 m away, and prefer unseen positions. If all eligible positions are visible, the minimum rises to 16 m. Selection is restricted to within two cells (6.4 m) of the closest candidate's walking-route distance, through the generated doors and corridors. Split parties use distance from the nearest member.

Every two seconds, at most two untouched, unseen, still-dormant enemies older than 20 seconds and farther than 48 walking metres from all players are removed alive. This gives no deaths, rewards or quota credit; ordinary spawn limits replenish the freed slots. Damaged, awakened, possessed and DEV enemies are retained. Poolrooms placement is unchanged. Level 0 identity is `backrooms-level0-4-entities2`.

Managed gates cover quadrant distribution, nav-distance equivalence, split-party routes, wall detours, cleanup exclusions, all anchors' collision routes and the objective ledger. The native builder additionally checks cleanup without quota/death credit and nearer replenishment; native execution and human encounter pacing are not claimed from offline tests.
