# CITY IRONWORKS BOSS: THE CONVERTER (design, concept model and Astra contract)

Claude, 2026-10-17. Branch `claude/boss-city-ironworks`, based on shared `claude/sleepy-ritchie-g8ruhv` (`dfb8aac`,
fast-forwarded to `02234ec`: Astra's PR #263 music, #264 and #265 zone gameplay, and #267).
**Status: approved by the owner on 2026-10-17 (every recommended option in section 11) and merged into shared. Not live.** The concept model `Core/NativeIronworksBossModel.cs` is
inert: nothing in the live runtime references it (a managed check holds that). No rule, AI, network field, resource,
catalogue state or live data changes. All numbers here are proposals. Astra owns rules, AI, numbers, networking and authority.

Owner (2026-10-17): "let us begin on the bosses for these two areas~ I want these animations to be a lot more fluid than
what we have now", and "A high quality design matching the areas too". This is the City Ironworks boss. The Central Station
boss is a separate parallel design. The shared fluid-motion library (`claude/boss-motion`: springs, replicated-root
smoothing, an N-leg foot-planting gait with IK, spring and verlet chains, an action timeline with anticipation, strike,
follow-through and recover, and additive layers) is a separate task. Section 8 describes this boss in its terms but does not
depend on its code.

Roles: Claude owns the look, model, animation, VFX, HUD presentation and the audio cue list. Astra owns states, AI, the action
scheduler, hit volumes, damage, health, phases, adds, hazard authority and replication.

## 0. At a glance

| | |
| --- | --- |
| Name | **THE CONVERTER**: on the HUD plate, under `BOSS ARRIVAL · CITY IRONWORKS`. The vessel itself is stencilled **VESSEL C**. Proposed archetype id `ironworks-converter` |
| Concept | Vessel C, the hall's missing third Bessemer converter. It walks on a four-legged trunnion yoke, and its dead foreman works the tilt levers from the pulpit on its back. It pours, it blows, it grabs with ingot tongs, and it sheds its slag crust |
| Scale | Standing: 6.7 m to the mouth and 8.0 m to the foreman's hard hat, about 4.5 pilots tall. The front tongs stand 8.6 m apart, the rear knees span 10.3 m, and it is 8.8 m long. The knee hubs ride at 4.4–4.9 m. Body collision proposal: a 2.7 m radius capsule, 7 m tall (section 10) |
| Arena | The Converter Hall (x -50..-24, z -12..12, 18 m roof). It rises out of the pouring pit at boss point 0 (-35, 0, 0), facing east (yaw 90), as the converter shutter opens |
| Phases | 3, at 100%, 70% and 35% health: **Blow** (crusted), **Tilt** (crust shed; the hall's own vessels wake), **Last Pour** (the pulpit torn away, the shell cracked white, the pit overflowing) |
| Actions | 8 attacks (pour, blow, stomp, grab, slam, deslag, call, charge) and 7 scripted moments (arrive, shed, break, choke, kneel, crash, die): section 6 |
| Live systems | Built on shared `02234ec`: the zone hazard clock (`NativeZoneHazardFrame`, `StepIronworksHazards`), the five live kinds and their boss mix and caps, and the live converter shutter (exp-layout-6). Section 10 names exactly what the boss adds |
| Weak points | The tuyère grate under the vessel (it glows white when it blows), the foreman (cyan visor, best hit from the side or back), the mouth's white metal when it tips toward you, four breakable crust groups, and the knees' rams (they fill the stagger meter) |
| Adds | All five Ironworks kinds, each tied to an action: Cinderlings fall out of the blow's rain, Slagbacks rise from shed crust and deslag lumps, a Ladleman comes down on the converter crane when it calls the vessels, Riggers drop from the hall's runway, and Stokers come in at the north and south doors |
| Concept model | `Core/NativeIronworksBossModel.cs`: 34 joints, three tiers (39,744 / 24,648 / 7,924 triangles with the chain props; budgets 40k / 26k / 9k), 20 key poses with two-bone leg IK, and chain props. Checks: `Tests/IronworksBossModelChecks.cs` (`CoreTests --ironworks-boss` and the full suite) |
| Music | The owner's "Hydraulic Hell", `music/city-ironworks-boss` (142.8 s; routed by `Core/NativeZoneMusic.cs`). The arrival lands on its intro's hydraulic hit, furnace flare and drop. Exact times come from `docs/ZONE-BOSS-MUSIC.md` (`claude/zone-boss-music-sync`) |

## 1. Concept

The doc's story is a sealed foundry that kept pouring, a hall of Bessemer converters "waiting tipped, as if for a pour that
never came" (`EXPEDITION-CITY-IRONWORKS.md` section 1). The kit stencils its two vessels **VESSEL A** and **VESSEL B**. The
boss is **VESSEL C**: the third converter of the hall, gone missing from its trunnions. The dead crew have stood it on a
walking yoke built from crane parts, and it keeps doing the one job it knows.

It is unmistakably this ironworks:

- **Molten metal is its weapon and its life.** The mouth's white metal is the brightest thing in the hall. Every attack
  spends that metal: it tilts to **pour**, it **blows** (the Bessemer blow, a roaring column of sparks to the roof) and it
  **deslags** out of the back.
- **Converters.** It is one. The tilt is real converter mechanics: a bull gear on the right trunnion, a pinion and motor, and
  the blast pipe down the left side into the wind box and its tuyère grate. In phase 2 it calls the hall's own two vessels to
  pour. This is the `-converter-tilt` hazard the Ironworks doc reserved as "boss-driven only".
- **Slag.** Glassy black slag crust is its breakable armour. When the crust sheds, the pieces stand up as Slagbacks.
- **Chains and cranes.** Its legs are box girders with hydraulic rams and striped hubs, like the hall's crane. Its front feet
  are **ingot tongs**: closed, they are its hooves; open, they grab. Chains hang under its yoke, and the hall's converter crane
  hook swings over the fight.
- **The dead still work the shift.** The foreman in his silver heat suit and cyan visor (the Stoker's family) hauls the
  levers on his pulpit. Each lever pull is a readable telegraph. When the Converter dies, the shift whistle on his pulpit
  blows and the foundry stops.

It also merges the three seed ideas: the converter colossus is the body, the foreman rides it (the crane-rider idea), and it
rises from the pouring pit wearing slag (the slag giant idea).

**Alternates considered (one line each):**

- *The Foreman on the crane*: a heat-suited foreman rides the converter crane's hook and drops ladles. Strong flavour, but
  it is pilot-sized and stuck on a rail, so there is no colossus and little fluid body motion. Kept as the Converter's rider.
- *Slag Giant*: a slag golem rises from the pouring pit. Readable, but it is a generic lava golem with no machinery and no
  converter. Kept as the arrival and the crust.
- *The Ladle Queen*: a giant Ladleman whose ladle is a converter on a shank. It duplicates the Ladleman elite.
- *The Hall Itself*: both converters and the crane as a stationary set-piece boss. Spectacular, but it has no body to read,
  chase or stagger, and nothing to animate fluidly.

## 2. The body

Model space: metres, +y up, +z the boss's forward. The root is on the ground under the trunnions.

| Part | Look | Reads as |
| --- | --- | --- |
| **The vessel** | A banded Bessemer converter, 3.9 m across the belly (4.4 m at the trunnion ring) and 5.6 m tall. Black iron, lit plate edges, rivet rows, five hoop bands, a trunnion ring, the eccentric nose tipped forward, and refractory brick at the lip. The mouth holds white metal (WhiteHot), with a lip glow and an orange tongue of metal slopping down the front of the nose (orange, so the white is the mouth's alone) | the body and the head. **The mouth is the gun**: when it turns toward you, you are the target |
| **Slag crust** | Four groups of big overlapping glassy plates (front, left, right, back): black slag, a glassy sheen, a dim red seam glowing under each plate's edge, and drips on the lowest row | breakable armour, the zone's slag |
| **The VESSEL C plate** | A dark nameplate on the front of the lower shell with a chipped cream stencil (the hall's own VESSEL A and B lettering) | story |
| **Bull gear and drive** | A machine-green ring gear on the right trunnion (40 teeth, 6 spokes) with a pinion and motor slung under the right girder | the tilt is mechanical: the gear turns when it pours |
| **Blast pipe and tuyères** | A pipe from the left trunnion down the shell into the wind box. The tuyère grate underneath glows white when it blows | **weak point** (white, never orange) |
| **The yoke** | Two riveted side girders with worn safety-yellow flanges and black stripes, a steam main, the trunnion bearings, two **cold-white work lamps** on the front ends (its eyes), hazard stripes, and the rear arch | crane machinery; the eyes find you |
| **The pulpit and foreman** | A grated platform with a yellow railing, a heat shield, the tilt levers (red knobs), the blast valve wheel and a gauge box with three cyan dials. The foreman wears an aluminised suit, a square hood with a **cyan visor**, a white foreman's hard hat and a respirator lamp. The shift whistle is on its pipe | the crew; a cyan **weak point**; the levers are a telegraph |
| **Legs** | Four crab legs. Each has a slewing coxa with a gear ring and a yellow cap, a box-girder femur with a ram on top and hoses, a knee hub with yellow-and-black striped caps, and a long tibia with lightening holes, a shin guard, a ram and heat-scarred lower plates | weight, the crane family |
| **Feet** | Front: **ingot tongs**, two curved jaws with teeth and hot tips. Closed they are hooves; open they grab. Rear: crane-truck **bogies** with two flanged wheels and buffers | grab, stomp |
| **Chains** | Four chains under the girders (two hooks, two shackles), verlet in the runtime | secondary motion |

**Palette** (the Ironworks enemies' and kit's): black iron 0x24211E with lit edges 0x4F4B46, worn safety yellow 0xC9A23A,
stripe black, machine green 0x2F4038, glassy slag 0x131010, refractory 0x5A3A2A and a silver suit 0xB4B1A9. The glows follow
readability rule 3.2: white-hot metal (`NativeIronworksKinds.WhiteHot`), white tuyères and phase 3 seams, a cyan visor and
dials, cold-white lamps, and dim red slag seams (the environment's warm colour, never a tell). The checks hold the tells
non-orange.

**Scale against a pilot:** a front tong stands 1.3 m, about a pilot's chest. The knee hubs ride at 4.4–4.9 m, the mouth at
6.7 m and the foreman's visor at 7.7 m. Across the 26 m hall it reads as one silhouette: the bulb of the vessel between four
arched legs, two white eyes, and a white mouth.

**Weak points and breakables** (proposals; numbers in section 10):

- Crust groups (4): each blocks most damage from its side until broken. Breaking a group exposes the shell, which shows red
  heat at the bands. At 70% health whatever crust is left sheds (the phase change).
- The tuyère grate: a critical hit. It is open to fire during the blow's inhale and column, and while kneeling or rearing.
  Enough damage during the inhale **chokes** the blow: a stagger.
- The foreman: a critical hit from the side or back. In phase 3 the torn pulpit leaves him hanging behind on its chains, an
  easier target.
- The mouth: a critical hit while it tips toward you (the pour's wind-up and strike).
- The knee rams: they fill the stagger meter. A full meter makes it **kneel**.

## 3. The concept model (`Core/NativeIronworksBossModel.cs`)

Presentation code in the enemies' part style. It uses `NativeWeaponSculpt` chamfered boxes, lathes, lofts, tubes and tori,
one section per joint, the zone palette, and three detail tiers. It is engine-free and deterministic, and it is inert: no live
file names it.

- **`NativeIronworksBoss`**: the proposed archetype, plate name, stencil, phase thresholds and the **action table** (section
  6; the doc's table is checked against it).
- **`IronworksBossPose`**: the body (height, pitch, roll, shift), the vessel's tilt, four foot targets and foot pitches, tong
  opening, lever and lean, the torn pulpit, glow levels (heat, blow, seams, cooling), crust bits, a shedding plate's flight,
  and chain swing.
- **Joints (34)**: root, yoke, vessel (with three glow groups: the metal, the tuyères and the seams), four crust groups,
  pulpit, lever, foreman, foreman arms, four legs of four joints each (coxa, femur, tibia, foot), and four tong jaws.
- **`Solve`**: joint matrices for a pose. Each leg is a coxa turned toward its foot about the yoke's up axis, and a femur and
  tibia solved by the law of cosines with the knee bent up and out. It allocates nothing.
- **`Key`**: 20 key poses: idle, walk, pour-windup, pour, blow-inhale, blow, stomp-lift, stomp, grab-reach, grab-hold, rear,
  slam, deslag, charge-windup, charge, kneel, arrive, shed, runaway, dead.
- **`ChainPose`**: link and end matrices, a stand-in for the runtime's verlet chain. **`GlowOf`** and **`Visible`** give each
  joint's emission scale and visibility for a pose.
- **Budgets:** 40,000 / 26,000 / 9,000 triangles for the full, reduced and low tiers. The boss is one actor; the horde's
  budget is 7,200 per add with up to 24 at full detail. The low tier keeps every joint.

**Checks** (`Tests/IronworksBossModelChecks.cs`, in the full CoreTests suite and `--ironworks-boss`):

- Build: each tier builds within its budget, deterministically, with every joint drawn.
- Joint table: parents precede children; the legs and jaws chain correctly.
- Key poses: every key pose solves to finite matrices inside the reach box (x ±6, z -6.2..7.6, y -3.4..9.4). In the standing
  keys nothing but the chains goes below the floor, and every foot reaches its target within 1 cm. In the stance the knees
  ride above and outside the hips, and left and right mirror.
- Bounds: the idle body stays in its drawn bounds. The vessel stays within 3.3 m of the trunnions' vertical at any tilt, and
  inside the 2.7 m capsule standing. The deslag's back roll clears the rear arch at every 5°.
- Tells: the cyan visor, the cold-white lamps, the white tuyères and the white seams; the mouth's metal outshines the crust.
- Glow groups and visibility: by phase and cooling.
- Allocation: posing, chains and glow allocate nothing.
- Doc agreement: the action table and the cue table match the code.
- Unreferenced: nothing under `Assets/` but the model names it or its archetype.

## 4. The arena: the Converter Hall as a set piece

The hall today (live `expedition-foundry`): x -50..-24 and z -12..12, with an 18 m roof truss. Vessels A and B stand on their
trunnions at (-45, ±7), mouths tipped 14° toward the pit and glowing. The **pouring pit** (-38..-32, -3..3) holds crusted slag
glowing through cracks. The converter crane bridge is at 13.3 m, with its hook over the pit at 9 to 10.4 m. Three doors
connect it: north and south at x -32.5 (5 m) and the **converter shutter** `exp-foundry:gate:converter` in the east wall at
z ±4, 5 m tall. The shutter is live in exp-layout-6 and opens with the boss encounter. The kit draws its frame;
NativeIronworksShutter draws the gate.

**What the boss does to the hall:**

- **The pit is its bed.** It rises out of it, and in phase 3 the pit overflows in cycles.
- **The crane hook** hangs over it. The blow's column throws sparks off the hook, the slam shakes the runway and swings the
  hook (verlet), and dust sifts down.
- **The hall's vessels** answer its call. In phase 2 one tilts and pours down a runner lane (z ±7, from x -42 to the pit)
  every call. In phase 3 both do. These lanes split the hall into a north, a middle and a south third for 4 s at a time.
- **The shutter.** It opens as the Converter arrives, so the party, arriving by a loop, sees the direct way open behind the
  boss. The Converter cannot pass the 8 m by 5 m opening, so the casting floor's west end is the pilots' retreat. In phase 3
  its charges end at the east wall beside the opening; a crash there staggers it.
- **Its own leavings**: pour pools (crusting over 4 s), slag patches from the deslag, shed crust as rubble, and the charge's
  molten trail.

**Phase by phase:**

| Phase | The hall |
| --- | --- |
| Arrival | The sodium lamps dip, the pit splits white, the shutter rises, the Converter climbs out, the lamps (eyes) click on, and the first blow throws sparks off the crane hook |
| 1 · Blow | As today: Vessels A and B glowing and tipped at rest, the runners r1 to r3 as the boss encounter runs them, and the ladle crane parked (owner decision 10). Its own pools and the blow's splashes come and go |
| 2 · Tilt | The crust lies in the hall as rubble, and two pieces stand up as Slagbacks. The hall's vessels wake: their mouths flare white, and one at a time they tilt and pour down the runner lanes when called |
| 3 · Last Pour | The pulpit hangs torn, the shell is cracked white, and sparks fall from the seams. The pit overflows every 14 s (spread 8 s, crust 6 s). Both vessels pour on a call, and the sodium lamps die |
| Death | The last pour runs into the pit. The metal cools white, orange, red, then black. Steam rises, the lamps flicker out, the shift whistle blows, and the hall's vessels exhale and dim |

**Readability (preserving the readability pass):** the hall keeps its light field work lights and the **two cool mercury
slots**. Boss lights replace only the farthest warm practical slots (the renders' rule). Telegraphs stay cream (areas), cyan
(lines and the grab), white (body flares) and amber (the hall vessels' hazard lanes only, the hazard kit's colour). Molten
floors stay under the decals' brightness (rule 3.2.4). The blow's column is bright but brief (2 s); the rain's circles are
cream with a hard edge. Heat haze stays on the mouth, the column and the pools.

## 5. Phases

| Phase | Health | Escalation | Moves | Adds |
| --- | --- | --- | --- | --- |
| 1 · **Blow** | 100–70% | Crusted. It walks, turns and keeps 4–7 m from its target. A slow rhythm: one action, then a breath | pour, blow, stomp, grab | the blow's Cinderling clots (2), Stokers at the doors (cap 2), a light trickle |
| 2 · **Tilt** | 70–35% | Shed: faster recovery (-20%), and the hall's vessels join | + slam, deslag, call | Slagbacks from shed crust (2) and deslag lumps (1 each, cap 2), one Ladleman lowered on the converter crane after the first call, a Rigger off the hall's runway (cap 1) |
| 3 · **Last Pour** | 35–0% | The pulpit is torn and the shell cracked. It turns 30% faster and adds the charge. The blow rains twice as dense, the pit overflows, and both vessels pour on a call | + charge; the blow becomes the last blow | clots (4 per blow, cap 6), Riggers (cap 2), the boss mix trickle (maxAlive 8) |

The boss encounter's own mix (Ironworks doc section 7: cinderling .45, stoker .15, slagback .20, rigger .10, shambler .10)
stays the base trickle under the scripted adds.

## 6. Actions

Times in seconds from the action's authority start time (`actionAt`). **Windup** is the telegraph, which is harmless.
**Strike** is the harmful window. **Follow** is the follow-through (still moving; leftovers such as pools may still burn).
**Recover** is the punish window. **Cooldown** is the time before the same action may start again. **From** is the first phase
that uses it. This table is `NativeIronworksBoss.Actions` (the managed check holds them equal).

| Id | From | Windup | Strike | Follow | Recover | Cooldown | Code |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `pour` | 1 | 1.2 | 1.6 | 0.5 | 1.0 | 7 | 1 |
| `blow` | 1 | 1.5 | 3.8 | 0.4 | 1.5 | 16 | 2 |
| `stomp` | 1 | 0.7 | 0.15 | 0.35 | 0.6 | 3 | 3 |
| `grab` | 1 | 0.9 | 0.25 | 1.6 | 0.9 | 10 | 4 |
| `slam` | 2 | 1.3 | 0.6 | 0.6 | 1.6 | 12 | 5 |
| `deslag` | 2 | 1.4 | 1.2 | 0.9 | 1.2 | 14 | 6 |
| `call` | 2 | 1.0 | 0 | 0.8 | 0.6 | 25 | 7 |
| `charge` | 3 | 1.0 | 1.2 | 0.6 | 1.0 | 9 | 8 |
| `arrive` | 1 | 5.5 | 0 | 0 | 0 | 0 | 20 |
| `shed` | 2 | 0.6 | 0 | 1.8 | 0.8 | 0 | 21 |
| `break` | 3 | 0.8 | 0 | 2.0 | 0.8 | 0 | 22 |
| `choke` | 1 | 0.3 | 0 | 0 | 2.5 | 0 | 23 |
| `kneel` | 1 | 0.4 | 0 | 0 | 3.0 | 0 | 24 |
| `crash` | 3 | 0.2 | 0 | 0 | 2.0 | 0 | 25 |
| `die` | 1 | 7.0 | 0 | 0 | 0 | 0 | 26 |

Every attack telegraphs for at least 0.7 s and leaves at least 0.6 s to punish (checked).

### 6.1 Pour (tilt pour)
- **Telegraph (1.2 s):**
  - Visual: the foreman hauls the lever back; the vessel first rocks *back* 14° (anticipation) while the metal bubbles white
    at the lip; the yoke rises and the front tongs step out wider. A **cream wedge** fades in on the floor: 52° wide, from
    1.2 to 8 m ahead, aimed at the locked target.
  - Audio: `boss.ironworks.foreman.lever` at 0, `boss.ironworks.gear.tilt` (the bull gear's ratchet) from 0.1 s, and
    `boss.ironworks.pour.windup` (bubbling, creaking) building to 1.2 s.
- **Strike (1.6 s):**
  - The vessel swings to 80° (overshooting to 86°) and a stream of white metal falls in a ballistic arc. It sweeps ±15°
    across the wedge, following the target with a lag.
  - Contact with the stream: 22. The pool left in the wedge's far half burns 8/s for 4 s while crusting.
  - Audio: `pour.loop` and `pour.splash`.
- **Counterplay:** leave the wedge sideways, or go behind it (its back is open). The mouth is a critical hit while it faces
  you.
- **Recovery (1.0 s):** the vessel rocks upright, overshoots to -6°, sways twice and drips.

### 6.2 Blow (the Bessemer blow; signature)
- **Telegraph (1.5 s):**
  - Visual: the yoke crouches, the vessel rights itself, the **tuyère grate flares white** and inhales, steam jets out of the
    trunnion seals, the foreman spins the valve wheel, and the chains lift in the updraft. **Cream circles** (1.0 m, 14 in
    phase 1, 28 in phase 3) fade in where droplets will fall, 4 to 10.5 m out.
  - Audio: `boss.ironworks.blow.inhale` (a rising whine and hiss).
- **Strike (3.8 s):**
  - Column (2.0 s): the yoke extends up 0.55 m and a roaring column of sparks and flame reaches the roof, throwing sparks
    off the crane hook. Audio: `blow.roar`.
  - Rain (1.8 s): the droplets land on their circles in a staggered order. Each deals 16 and leaves a 1 m splash for 1 s.
    Audio: `blow.rain`, `blow.land`.
  - Clots: 2 of the circles (4 in phase 3) are bigger. Their clots stand up as Cinderlings 1.2 s after landing.
- **Counterplay:**
  - Stand in the gaps between the circles.
  - Or punish: 4% of maximum health to the tuyère grate during the inhale **chokes** the blow (choke: 2.5 s, no rain). It
    needs fire from under the yoke's sides or behind.
- **Recovery (1.5 s):** it slumps and vents steam from the tuyères. The grate stays exposed, cooling.

### 6.3 Stomp
- **Telegraph (0.7 s):**
  - Visual: the body rolls its weight off one leg; that leg lifts in an arc to 2.5 m and the tongs aim. A **cream ring**
    (1.9 m) marks the foot's landing.
  - Audio: `boss.ironworks.stomp.lift` (hydraulic hiss, chain).
- **Strike (0.15 s):**
  - The leg slams down: 26 inside the ring, plus a 0.6 s knockdown.
  - Visual: dust, cracks, sparks. The vessel lurches 15° forward and slops metal over the lip.
  - Audio: `stomp.impact`.
- **Counterplay:** it is used on pilots under or beside its legs. Step out of the ring.
- **Recovery (0.6 s):** the leg stays planted; the knee ram is exposed.

### 6.4 Grab (the ingot tongs)
- **Telegraph (0.9 s):**
  - Visual: a front leg rears back, then reaches. The tongs open to 44° and the body leans toward the target. A **cyan
    dashed line** runs from the tongs to the target (lines are cyan) and a cyan ring marks the target.
  - Audio: `boss.ironworks.grab.reach`.
- **Strike (0.25 s):** the jaws snap shut on anyone in the 1.2 m target ring: 10 (`grab.snap`).
- **Follow (1.6 s):**
  - The pilot is lifted to 2.8 m and shaken twice: 6/s (`grab.squeeze`).
  - Then thrown 6 m to the side, onto an open pool if there is one: 14 on landing (`grab.throw`).
  - Allies free the pilot by dealing 200 to the tong (the jaw hinge glows when hit).
- **Counterplay:** sidestep the line during the reach; one target only.
- **Recovery (0.9 s):** the leg comes back down; the tong is a critical hit.

### 6.5 Slam (rear and slam; phase 2+)
- **Telegraph (1.3 s):**
  - Visual: both front legs climb, the body rears 24°, metal sloshes to the back lip, the chains swing back and the foreman
    braces. It **hangs** 0.2 s at the top. A **cream ring** previews the shockwave at 9 m.
  - Audio: `boss.ironworks.rear.creak` (girders groaning).
  - The grate is open to fire while it rears.
- **Strike (0.6 s):**
  - The front tongs slam down and the body drops 0.4 m below its stance (the suspension compresses).
  - A 0.6 m tall **shockwave ring** expands from 0 to 9 m: 24, which a jump clears. Audio: `slam.impact`, `slam.wave`.
- **Follow (0.6 s):**
  - The vessel lurches forward 34° and a wave of metal splashes up to 6 m ahead: 14 and a 3 s pool.
  - The vessel swings back past upright. The crane hook swings.
- **Counterplay:** jump the ring, or stay beyond 9 m. Never stand in front.
- **Recovery (1.6 s).**

### 6.6 Deslag (phase 2+)
- **Telegraph (1.4 s):**
  - Visual: the foreman cranks the lever and the vessel rolls back on its trunnions (the gear turning), mouth to the rear,
    the body pitching forward to counterbalance. Three **cream circles** (1.4 m) appear 7 to 9 m behind it.
  - Audio: `boss.ironworks.deslag.roll`.
- **Strike (1.2 s):**
  - Three slag lumps are lobbed out of the mouth at 0.4 s intervals, on smoke trails (`deslag.lob`). Each lands 0.9 s
    later: 20 (`deslag.land`).
  - Each leaves a slag patch burning 8/s (the live burner rate) for 6 s. In phase 2 one patch per deslag may stand up as a
    Slagback.
- **Counterplay:** it punishes camping its back. Watch the mouth roll and leave the circles.
- **Recovery (1.2 s):** the vessel rolls upright and overshoots forward 12°.

### 6.7 Call (phase 2+)
- **Telegraph (1.0 s):** the Converter raises its front 10° and tips its mouth toward Vessel A or B, then bellows. A heat
  haze shock rolls out. Audio: `boss.ironworks.call.bellow`.
- **Strike:** the call itself is harmless. It starts the hall vessel's **converter tilt hazard**: the live
  `NativeIronworksHazards` / `StepIronworksHazards` path, driven by the boss (section 10.2), and presented in the hazard
  kit's amber.
  - Warn 2.5 s: an amber beacon on the vessel, an amber dashed lane edge on the floor, and the vessel tipping.
  - Live 4 s: the vessel's stream falls to the floor at x -41, and a runner lane flows along z ±7 to the pit. The lane
    burns at the live burner rate (2 every 0.25 s).
  - Phase 2 calls one vessel; phase 3 calls both. The first call in phase 2 also lowers the Ladleman on the converter
    crane's hook at (-29.5, 0, 0).
- **Counterplay:** the lanes split the hall into thirds. Stay out of the lanes during the 4 s, and cross before or after.

### 6.8 Charge (phase 3)
- **Telegraph (1.0 s):**
  - Visual: the body drops low (2.95 m), the rear bogies dig back throwing sparks, and the vessel tips forward like a
    lowered head. A **cyan dashed path** (the line rule) with two edge lines shows the 14 m run.
  - Audio: `boss.ironworks.charge.windup`.
- **Strike (1.2 s):**
  - A transverse gallop (legs in sequence, 0.3 s cycles) along the path: 34 and 6 m knockback.
  - The vessel slops a molten trail behind it (8/s, 3 s). Audio: `charge.gallop`.
- **Follow (0.6 s):** a skid. The tongs dig in and the rear rises (`charge.skid`).
  - If the path ended at a wall or a hall vessel, it **crashes** instead (crash: 2.0 s stunned). The hall vessel rocks.
    Audio: `charge.crash`.
- **Counterplay:** sidestep the path, or bait it into a wall for the crash.
- **Recovery (1.0 s).**

### 6.9 Scripted moments
- **Arrive (5.5 s):** section 7.1.
- **Shed (phase 1 to 2, 3.2 s):** it shudders and the cracks in the crust brighten. The crust bursts off in four groups, 0.1 s
  apart: plates arc out and tumble, glassy shards, a cream flash. The body drops 0.3 m (relief) and the shell shows red heat
  at the bands. Then the bellow, and both hall vessels jolt awake. Two pieces of rubble stand up as Slagbacks 6 s later.
  Audio: `crust.crack`, `shed.burst`, `call.bellow`.
- **Break (phase 2 to 3, 3.6 s):** it staggers sideways. The rear arch cracks, the pulpit tears and swings down behind on its
  chains, and the foreman dangles from his belt chain. White seams split the shell (seam glow 0 to 1 in 0.8 s), the pit
  overflows and the sodium lamps die. Audio: `break.tear`, `break.split`, `pit.overflow`.
- **Choke (2.5 s):** a cough of black smoke out of the mouth, the rain cancelled, a slump. The grate is open to fire.
  Audio: `blow.choke`.
- **Kneel (3.0 s):** the stagger meter fills (section 10). Both front legs buckle, steam bursts from the knee rams, and the
  mouth dips (a small pool in front). The grate and the foreman are open to fire. Audio: `knee.buckle`.
- **Crash (2.0 s):** a charge into a wall or a hall vessel. A big shake, the vessel clanging, sparks.
- **Die (7.0 s):** section 7.4.

## 7. Moments

### 7.1 Arrival and intro (fits the existing flow)

The director records the arrival (`bossArrivals[]`: id, area, archetype, `spawned`, position at boss point 0, hp/maxHp) as
the instance's boss encounter starts. The converter shutter's closure turns `opening` over the existing 1 s transition. The
HUD banner `BOSS ARRIVAL · CITY IRONWORKS / THE CONVERTER` plays for `IntroSeconds` (3.4 s) from `spawned`, and a late joiner
past `LateJoin` sees the plate alone. The boss song starts on the shared boss clock: the owner's **"Hydraulic Hell"**
(`music/city-ironworks-boss`, 142.8 s).

**Music sync.** The owner asked to "sync their intro with the stage". The arrival is choreographed to the song's intro.
- The exact times come from the track's beat map in `docs/ZONE-BOSS-MUSIC.md` (branch `claude/zone-boss-music-sync`; tempo,
  sections, intro accents, the drop, the loop). This design does not analyse the song.
- The music agent owns the stage's beat reactions (furnace glow, press pistons, steam, sparks), `NativeAudio` and
  `NativeZoneMusic`. This design leaves them alone.
- The arrival wants three of the intro's beats:

| Song beat (from `ZONE-BOSS-MUSIC.md`) | The Converter | Cue |
| --- | --- | --- |
| song start (T = `spawned`) | The hall's sodium lamps dip, the pit's crust glows from below, the shutter starts to rise | `boss.ironworks.arrive.rumble` |
| **first hydraulic hit** | The crust splits in a cross, white light through the cracks, steam rings at the rim; the front tongs punch up and grip the rim (the feet plant on the hit) | `arrive.crack`, `step.tong` ×2 |
| the following intro accents | The vessel heaves out mouth first, tipped toward the shutter, and slag sheets off; the rear legs climb out on the next accent and it stands, the vessel rocking and settling | `arrive.emerge`, `step.bogie` ×2, `slosh` |
| **furnace flare** (the accent the stage's furnaces flare on) | The lamps (eyes) click on and the foreman's visor lights; the mouth flares white with the furnaces | `arrive.lamps` |
| **the drop** | The first blow: a roaring column to the roof, sparks off the crane hook. It turns on the nearest pilot and the fight begins | `arrive.blow` |

- Without a beat map, the fallback timings are 0.6, 1.2, 2.0, 3.2, 3.9 and 4.3 s, with the fight at 5.5 s.
- Proposal (owner decision 10): the intro hold ends **on the drop**. That replaces the Siren's 9.07 s, which is matched to
  her song.
Under the floor the body is hidden by the opaque slab. The rising reads as coming out of the molten pit because the pit glows
white and the legs stay folded inside the pit's 6 m square until the body is out.

### 7.2 Adds: the five enemies

| Kind | How it enters | Tie-in |
| --- | --- | --- |
| **Cinderling** | Clots from the blow's rain stand up 1.2 s after landing (2 per blow; 4 in phase 3; cap 6 alive) | furnace hands born of its sparks |
| **Stoker** | The live boss mix (.15), coming in through the north and south doors. Proposal: a Stoker that reaches the boss rakes the tuyères for 2 s, and the next blow comes 30% sooner. Its hook pull can drag a pilot into a pour wedge | they stoke the converter |
| **Slagback** | Two from the shed crust's rubble 6 s after the shed; one per deslag from a slag patch (phase 2+); the converter hall's slag trenches (entries 29, 30) as today | its own slag stands up |
| **Ladleman** | Lowered on the converter crane's hook at the crane bay, entry 22 (-29.5, 0, 0), after the first call in phase 2 (cap 1). He dips his ladle in the boss's pour pools to refill | the ladle serves the converter |
| **Rigger** | Off the converter hall's runway hang point, entry 28 `vent-conv-rail` (-27.8, 0, -6), with the usual rattle (the live boss cap is 2) | the crane rail |

### 7.3 Stagger and break moments

- **Crust group break** (×4): a crunch, glassy shards, a cream flash and an additive flinch. The shell's red heat shows there.
- **Choke**: shoot the grate during the inhale.
- **Kneel**: the stagger meter.
- **Crash**: bait the charge into a wall.
- **Shed** and **break**: the phase changes.

### 7.4 Death: the shift ends
- 0 s: the front left leg buckles, then the right (1.2 s). The body drops onto the pit's rim with a bounce and the rear legs
  splay.
- 1.5 s: the vessel tips forward (62°) and pours its last metal into the pit: a thinning stream, then drips.
- 2 s: the heat drains from the bottom up over 4 s: the shell's glow goes white, orange, red, then black. The metal at the
  mouth skins over with crust and the sheen turns matte. Steam columns rise.
- The lamps flicker twice and die, and the foreman slumps over his lever.
- 5.5 s: the music stops, and the **shift whistle** on the pulpit blows alone (a steam plume). The hall's vessels exhale and
  dim.
- 7 s: the encounter's defeat. The cooled hulk remains as dressing until the area unloads.

Audio: `death.buckle`, `death.pour`, `death.cool`, `death.whistle`.

## 8. Animation direction (fluid)

The current bosses and horde pose bones from takes or hard keys. This boss is built to be **procedural and continuous**:
- Nothing pops between states.
- Every action is a timeline from a replicated start time.
- Weight is carried by springs and planted feet, not keyframes.
- Secondary motion is everywhere: the vessel on its trunnions, the metal in the vessel, the chains, the foreman, the hook.

The library terms below are the shared motion library's (`claude/boss-motion`).

**Locomotion and turning (always continuous):**
- **Replicated root smoothing.** The authority replicates the body root's position and yaw (the existing boss body).
  - The drawn root follows on a critically damped spring (ω about 6 rad/s for position, 5 rad/s for yaw), with a 50°/s yaw
    rate cap (35°/s in phases 1 and 2, 65°/s in phase 3).
  - Corrections over 2 m blend over 0.4 s.
  - The body leads, and the feet follow by planting. Feet never slide.
- **Gait (N-leg foot planting, N = 4).**
  - A diagonal trot at walk: FL with RR, then FR with RL.
  - A leg steps when its planted foot is more than 0.9 m from its stance target under the moving body, or 25° behind on a
    turn.
  - Swing: 0.55 s at walk (0.32 s in the charge) with a 0.7 m lift (1.0 m charging). The arc eases out and **accelerates into
    the plant** over its last 20%, so each step lands with a thunk.
  - IK per leg: the coxa turns toward the foot, and the femur and tibia solve two-bone with the knee bent up and out. This is
    the model's `Solve`.
- **Body.**
  - Height: the planted feet's average plus the stance height, minus 0.12 m per leg in swing. The weight dips onto the
    planted pair.
  - Pitch and roll: from the planted feet's plane, through a spring (ω 5, ζ 0.6).
  - The body runs slightly ahead of the feet, so the legs trail and catch up (no treadmill).
- **Turning in place:** the feet re-plant round the pivot in pairs while the yoke's yaw spring leads. The vessel lags on its
  trunnions, so a turn makes it swing.

**Springs:**
- **The vessel on its trunnions:** a pendulum spring (ω 3.2 rad/s, ζ 0.25) on the tilt. It is driven by the body's
  acceleration and the action's tilt target, so every stop, turn and slam makes the vessel swing and settle.
- **The metal (slosh):** a 2D spring of the metal's surface normal against the vessel's axis (ω 4, ζ 0.15). Past 12° it spills
  (drips, a slop over the lip). The mouth's glow flares with the slosh.
- **The foreman:** a lean spring on his pulpit, so he is thrown about by slams and stops. His arms follow the lever (the
  model's lever-to-arms link).
- **Lamps:** their beams wobble on a small spring.

**Chains (verlet):**
- The four yoke chains (12 to 16 links each) rest on the floor when low.
- The hall's converter crane hook reacts to the slam's shake and the blow's updraft.
- In phase 3 the torn pulpit hangs on two chains, and the foreman dangles from his belt chain.

**The action timeline:**
- Each action is a set of curves over anticipation, strike, follow-through and recover (the section 6 table), for:
  - the body (height, pitch, roll, shift);
  - the vessel's tilt target;
  - each leg (foot targets relative to the body or the world);
  - the tongs;
  - the lever and lean;
  - the glow levels.
- Actions blend in over 0.15–0.25 s from the gait. Planted feet stay planted unless the action moves them.
- They hand back to the gait at recover, and the springs carry the overshoot through.
- Key poses (the model's `Key`) are the curves' extremes. The table below says where each sits.

**Additive layers:**
- **Breathing:** the idle burn. The heat pulses on a 2.4 s period, the yoke rises 3 cm, and the vessel moves ±1.5°.
- **Hit flinch:** an impulse into the body and vessel springs, scaled by damage, from the hit's side.
- **Damage states:**
  - A broken crust group shows red heat.
  - A damaged knee **limps**: its step is 30% shorter and the body rolls 3° toward it.
- **Phase 3 tremor:** 2–3 Hz noise of ±1° on the vessel and yoke, and sparks falling from the seams.

| Action | Weight and anticipation | Strike and overshoot | Follow-through and secondary | Keys |
| --- | --- | --- | --- | --- |
| Walk | The body leads by a quarter stride; the weight rolls onto the planted pair | Each plant accelerates in (a thunk, a dust puff) | The vessel swings 3–5° behind the body; the chains trail; the metal sloshes on stops | idle, walk |
| Pour | It rocks back 14° and rises 7 cm; the tongs step wide; the lever is hauled | It swings to 80° in 0.45 s and overshoots to 86°; the body dips 0.35 m forward | It drools off the lip; it rocks to -6° and sways twice; the gear turns back | pour-windup, pour |
| Blow | It crouches 0.3 m; the grate flares; steam at the seals | It extends 0.55 m in 0.25 s (overshoot); the vessel recoils 8°; a 6 Hz shudder through the column | It slumps 0.15 m; the tuyères vent; the chains settle after the updraft | blow-inhale, blow |
| Stomp | The roll off the leg (6°); the leg arcs up to 2.5 m | It accelerates down; the body rolls past centre; the vessel lurches 15° | The metal slops over the lip; the leg holds planted | stomp-lift, stomp |
| Grab | The leg draws back before the reach; the body leans 5° toward the target | The jaws snap; the body jolts back | Two shakes; the fling's swing carries the leg out; the body counter-rotates | grab-reach, grab-hold |
| Slam | Two-step climb, a 24° rear, the metal to the back lip, a 0.2 s hang | It drops 0.4 m below stance; pitch -9° | The vessel lurches 34°, swings back past upright and decays; the hook swings | rear, slam |
| Deslag | The vessel rolls back (the gear turns); the body pitches forward 4° | Three recoil twitches | It rolls upright and overshoots 12° forward; slag drips | deslag |
| Charge | It crouches low; the bogies dig in; the vessel tips 24° forward | A gallop with ±4° pitch; the vessel spills a trail | The skid: the tongs dig, the rear rises; the vessel swings 30° forward | charge-windup, charge |
| Kneel | The front legs buckle in sequence (0.15 s apart) | The body drops onto the bent legs; the vessel dips 20° | Steam from the knees; the foreman thrown forward | kneel |
| Arrive | The legs stay folded in the pit; the mouth comes first | The body rises in two heaves (front, then rear) | The vessel rocks and settles; slag sheets off | arrive |
| Shed and break | A shudder; the crack glow ramps | The plates fly on arcs and tumble (`Fling`) | The body drops in relief; the bellow; in the break the pulpit swings on its chains | shed, runaway |
| Die | The legs buckle in sequence | It drops onto the rim with a bounce | The last pour; the cooling from the bottom up; the foreman slumps | dead |

**Hit timing, hitboxes and networking impact:** none today; this is a concept. When built, every harmful window is the
authority's (the table in section 6), and the presentation only draws it from `actionAt`. Drawn reach can lead the hit volume
by at most 0.1 s and 0.3 m (the tongs and the stream), as the Cyber Lord's melee step does.

## 9. Presentation

**HUD plate:** `THE CONVERTER` with the existing eyebrow `BOSS ARRIVAL · CITY IRONWORKS`. This needs a name-table entry,
because the archetype id alone would read `IRONWORKS CONVERTER`. Proposed later (Claude): two phase notches on the plate's bar
at 70% and 35%, and a small crust strip of four segments under the bar.

**VFX list (a later `Core/NativeIronworksBossFx`, the kit's and the enemies' roles):**
- **Always:** the mouth glow and heat haze, smoke from the mouth, drips and slosh spills, the lamps' halos and soft beams,
  the visor, and steam at the seals.
- **Pour:** the stream (`NativeMolten` stream pieces), splash sparks, the pool (crusting flow pieces) and the cream wedge.
- **Blow:**
  - inhale: the tuyère flare and steam jets;
  - column: stacked additive fire sprites, rising ember streaks, sparks off the crane hook and a smoke ring under the roof;
  - rain: falling droplet streaks, cream circles and splash pools;
  - clots.
- **Stomp:** the cream ring, dust, cracks and sparks.
- **Grab:** the cyan line, a glint at the open jaws, clamp sparks and the thrown pilot's trail.
- **Slam:** the preview ring, the white shock ring (0.6 m tall), a dust wall, the metal wave and a pool.
- **Deslag:** slag lumps (props) on smoke trails, cream circles and slag patches (`NativeMolten` slag).
- **Call:** a heat haze shock. The hall vessel's stream and runner lane are the hazard's own (amber edges).
- **Charge:** the cyan path, sparks off the feet, the molten trail and the skid's sparks.
- **Crust:** glassy shards (`HotDebris`, `NativeStructureFx`) and a cream flash. Phase 3: the seams' white glow and falling
  sparks; the pit overflow (`NativeMolten` flow).
- **Kneel and choke:** knee steam and a black smoke cough.
- **Death:** steam columns, the cooling ramp and the whistle's steam.

**Audio cue keys (for the owner to generate; nothing generated here).** Mix the warnings (`blow.inhale`, `rear.creak`,
`grab.reach`, `charge.windup`, `deslag.roll`) above the furnace bed, as the Rigger's rattle is.

| Key | Count | Brief |
| --- | --- | --- |
| `boss.ironworks.arrive.rumble` | 1 | a deep subterranean rumble under a steel hall, molten slag stirring |
| `boss.ironworks.arrive.crack` | 2 | a thick crust of slag cracking open, glassy shatter, a gush of heat |
| `boss.ironworks.arrive.emerge` | 1 | a huge iron vessel heaving out of molten slag, metal groaning, slag pouring off |
| `boss.ironworks.arrive.lamps` | 1 | two heavy industrial work lamps clicking on with an electrical thunk |
| `boss.ironworks.arrive.blow` | 1 | a Bessemer converter's blast roaring to life, a jet engine of fire and sparks |
| `boss.ironworks.idle.burn` | 1 | (loop) a converter full of molten iron humming and bubbling, a low furnace breath |
| `boss.ironworks.step.tong` | 4 | giant iron tongs stamping on concrete, a heavy clank and grind |
| `boss.ironworks.step.bogie` | 4 | a crane truck's steel wheels thudding down on concrete, flange screech |
| `boss.ironworks.gear.tilt` | 3 | a giant bull gear ratcheting round, slow heavy teeth |
| `boss.ironworks.slosh` | 3 | molten metal sloshing inside a huge iron vessel |
| `boss.ironworks.foreman.lever` | 3 | a heavy iron lever hauled through its quadrant, a clunk |
| `boss.ironworks.foreman.valve` | 2 | a rusty valve wheel spun fast, a hiss of air |
| `boss.ironworks.foreman.groan` | 3 | a dead foreman's muffled groan through a heat hood's respirator |
| `boss.ironworks.pour.windup` | 2 | molten iron bubbling up to a vessel's lip, a creak of trunnions |
| `boss.ironworks.pour.loop` | 1 | (loop) a heavy stream of molten iron pouring onto concrete, roaring hiss |
| `boss.ironworks.pour.splash` | 3 | molten metal splashing and spattering, sparks crackling |
| `boss.ironworks.blow.inhale` | 2 | a huge air blast drawing in, rising whine and hiss through nozzles |
| `boss.ironworks.blow.roar` | 2 | a converter's blow at full blast, a roaring column of fire and sparks |
| `boss.ironworks.blow.rain` | 1 | (loop) a rain of molten droplets hissing as it falls |
| `boss.ironworks.blow.land` | 4 | a molten droplet hitting concrete, a sizzling spatter |
| `boss.ironworks.blow.choke` | 2 | a giant blast choking, coughing black smoke, a sputter |
| `boss.ironworks.stomp.lift` | 2 | a huge hydraulic leg lifting, a hiss and chain |
| `boss.ironworks.stomp.impact` | 4 | a colossal iron foot slamming concrete, a boom and crack |
| `boss.ironworks.grab.reach` | 2 | giant iron tongs opening, a grinding hinge |
| `boss.ironworks.grab.snap` | 3 | iron tongs snapping shut, a heavy clang |
| `boss.ironworks.grab.squeeze` | 2 | iron jaws grinding tight, a strained creak |
| `boss.ironworks.grab.throw` | 2 | a heavy swing and release, a whoosh |
| `boss.ironworks.rear.creak` | 2 | steel girders groaning as a huge machine rears up |
| `boss.ironworks.slam.impact` | 3 | a gigantic machine slamming down, a ground-shaking boom |
| `boss.ironworks.slam.wave` | 2 | a shockwave rolling across a steel floor, a rumble and rattle |
| `boss.ironworks.deslag.roll` | 2 | a converter rolling back on its trunnions, gear teeth grinding |
| `boss.ironworks.deslag.lob` | 3 | a lump of slag hurled out, a wet glassy whoosh |
| `boss.ironworks.deslag.land` | 3 | slag landing on concrete, a glassy crunch and crackle |
| `boss.ironworks.call.bellow` | 2 | a converter's mouth roaring, a vast industrial bellow, a foghorn of fire |
| `boss.ironworks.charge.windup` | 2 | huge wheels and tongs digging into concrete, sparks and a growl of motors |
| `boss.ironworks.charge.gallop` | 1 | (loop) a giant four-legged machine galloping on concrete |
| `boss.ironworks.charge.skid` | 2 | iron tongs skidding on concrete, a long screech |
| `boss.ironworks.charge.crash` | 2 | a huge iron machine crashing into a brick wall, a boom and debris |
| `boss.ironworks.crust.crack` | 3 | slag crust cracking under heat, glassy ticks |
| `boss.ironworks.crust.break` | 3 | a slab of slag armour shattering off iron |
| `boss.ironworks.shed.burst` | 1 | a whole crust of slag bursting off a vessel, glass and rock raining |
| `boss.ironworks.break.tear` | 1 | a steel platform tearing off its mounts, a shriek of metal and chains |
| `boss.ironworks.break.split` | 1 | an iron vessel cracking open, a deep crack and a rush of heat |
| `boss.ironworks.pit.overflow` | 1 | molten slag welling up and spilling over a pit's rim |
| `boss.ironworks.knee.buckle` | 2 | a huge hydraulic leg buckling, a burst of steam |
| `boss.ironworks.hurt.clang` | 4 | a heavy hit ringing off a giant iron vessel |
| `boss.ironworks.death.buckle` | 1 | a colossal machine collapsing, legs giving way, iron groaning |
| `boss.ironworks.death.pour` | 1 | the last of the molten iron pouring out and thinning to drips |
| `boss.ironworks.death.cool` | 1 | (loop) cooling iron ticking and hissing in steam |
| `boss.ironworks.death.whistle` | 1 | an old factory's end-of-shift steam whistle, long and mournful, echoing in a hall |

**Music:** the owner's "Hydraulic Hell", `music/city-ironworks-boss` (142.8 s).
- Routing: `Runtime/NativeAudio.cs` through `Core/NativeZoneMusic.cs` (`claude/zone-music`). It plays automatically from the
  arrival (`BossSongTime >= 0`) and loops on the shared boss clock.
- The beat map, the intro sync with the stage and the stage's beat reactions are the music agent's
  (`claude/zone-boss-music-sync`, `docs/ZONE-BOSS-MUSIC.md`). This design only consumes them, for presentation and never for
  rule timing:
  - the arrival on the intro's hydraulic hit, its furnace flare and the drop (section 7.1);
  - the shed's and the break's bursts snapped to the next downbeat within 0.25 s;
  - the idle burn's heat pulse (section 8, breathing) locked to the beat while nothing else drives it;
  - the music dipped 4 dB under the blow's roar and the call's bellow;
  - the music stopped at the death's 5.5 s, before the shift whistle.
- If the map marks sections: the phase 2 and phase 3 transitions on section boundaries, as the Siren's song does.

## 10. Astra contract (proposal, on the live zone systems)

Shared `02234ec` (Astra's PR #264, #265 and #267) is the base. The contract reuses what is live and names exactly what the
boss adds.

### 10.1 What is already live (reused as is)

- **The hazard clock.** `NativeZoneHazardFrame` (`Core/NativeZoneHazards.cs`) rides each network frame in
  `NativePveFrame.zone`.
  - Its fields: `running`, `boss`, `wave`, `clock`, `start`, `bossAt` and the station's fields.
  - `.Ironworks` hands `IronworksHazardInput` to the presentation. `BindZonePresentation` sets `NativeIronworksLive.Authority`
    and `Bound`.
  - `StepIronworksHazards` (`Runtime/NativeExpeditionZoneHazards.cs`) damages pilots and adds inside every live hazard volume:
    crush 35, cut 22, burners 2 every 0.25 s. Cinderlings are immune to fire. A per-actor contact ledger (`ZoneTouch`) makes
    the rate independent of the frame rate.
  - When the boss encounter starts, the frame sets `boss` and `bossAt` and restarts `clock`. The ladle crane parks
    (`BossOff`). Every `BossOn` hazard runs. `-converter-tilt` is `BossDriven`, so it never runs yet.
- **The five kinds.**
  - `NativeZoneCombat.Encounter(map, wave, boss)` gives the boss encounter's mix: cinderling .45, stoker .15, slagback .20,
    rigger .10, shambler .10.
  - `Cap(kind, wave, boss)` gives **rigger 2** and **ladleman 0** in the boss. `ZoneKindRoom` enforces the caps.
  - Each add's state rides `NativeZoneEnemyFrame`, and `IronworksViewOf` is bound to `m.Pve.Zone.Ironworks`.
- **Converter hall entries** (live exp-layout-6):

| Entry | Id | Kind | Where | Allows |
| --- | --- | --- | --- | --- |
| 0 | `breach-conv-w` | breach | (-47.2, 0, 0) | hulk, ladleman |
| 22 | `drop-conv-crane` | drop, 11 m | (-29.5, 0, 0) | hulk, ladleman |
| 28 | `vent-conv-rail` | the Rigger's hang point, 10 m | (-27.8, 0, -6) | — |
| 29, 30 | `vent-conv-trench-n` / `-s` | slag trenches | (-38.8, 0, ±10) | slagback |

- **The boss slot.**
  - `NativeExpeditionM1Arena.SpawnExpeditionBoss` places `Pve.Boss` (a pilot body) at boss point 0 (-35, 0, 0), yaw 90. It
    records the arrival with archetype `siren` and starts the `intro` hold (9.07 s).
  - `UpdateBossHealth` refreshes `bossArrivals[].hp`.
  - `StepPveBossPhases` steps the Siren's four phases through `reveal` holds at `Floor`.
- **The converter shutter** `exp-foundry:gate:converter` (`opens: "boss"`) is in the live exp-layout-6. It opens with the
  boss encounter over the 1 s transition.

### 10.2 What the boss adds on top (exact)

1. **The archetype** `ironworks-converter`.
   - `SpawnExpeditionBoss` uses it for the `exp-foundry` area, with its own `RecordBossArrival` known-archetype predicate.
     Today that predicate checks weapon ids.
   - Its body: a vertical capsule, radius 2.7 and 7 m tall, centred 0.6 m behind the trunnions, plus four non-blocking leg
     hurt volumes (radius 0.45). It replaces the pilot capsule.
   - Movement: walk 1.6 m/s; turn 35°/s in phases 1 and 2, 65°/s in phase 3; it keeps 4–7 m from its target.
   - It never leaves the hall: the shutter's 8 m × 5 m opening is smaller than it. A charge path is clamped at the hall's
     walls.
   - Only boss point 0 is used. Points 1 and 2 lie inside its footprint.
2. **Its phases, without the Siren's reveal holds.**
   - `Pve.Phase` 1..3 with floors at 70% and 35%. The `shed` (3.2 s) and `break` (3.6 s) actions play in the fight; there is
     no cinematic hold.
   - The intro hold ends on the song's drop (`docs/ZONE-BOSS-MUSIC.md`), or 5.5 s without a beat map.
3. **A boss record**, `NativeZoneBossFrame` in `NativePveFrame` as `zoneBoss`. It follows `NativeZoneEnemyFrame`'s wire rule:
   finite floats, -100 for "none" (`NativeZoneCombat.Wire` and `View`), times on the arena clock (`Time`, as `bossAt`), plus
   `Valid()` and `Copy()`.

| Field | Type | Meaning |
| --- | --- | --- |
| `action` | string | the action id (section 6), or null |
| `actionAt` | float | its start (-100 none) |
| `actionYaw` | float | the facing locked at its start (pour, slam, deslag, charge) |
| `targetX`, `targetZ` | float | the locked target point: the pour's aim, the stomp's foot, the grab's target, the charge's end |
| `targetSeat` | int | the grabbed pilot's seat (-1 none) |
| `variant` | int | the stomp's leg (0..3: FL, FR, RL, RR) or the call's vessels (bit 0 north, bit 1 south) |
| `phase` | int | 1..3 |
| `phaseAt` | float | when the current phase began; the shed and break play from it |
| `parts` | int | bits 0–3 the crust groups intact (front, left, right, back), bit 4 the pulpit attached |
| `partAt` | float[5] | each part's break time; the fling and the tear play from it |
| `stagger` | float | 0..1, the stagger meter (optional, for a HUD hint) |
| `deathAt` | float | the death's start (-100 alive) |

   The presentation reads it through a Core `IronworksBossView` (times NaN when unset), filled in one line as
   `IronworksViewOf` is.
   - The root position and yaw stay the boss body's. Health stays `bossArrivals[]`.
   - Everything else is derived from `actionAt`, the targets and the section 6 timings: the rain's circles (seeded by the
     arrival id), the deslag's landing points (from `actionYaw` and three fixed offsets) and pool expiries.
4. **Hazard frame fields** on `NativeZoneHazardFrame`, all floats on its own `clock` (the hazard clock restarts at the boss):
   - `converterAt` (-100 none): when the last `call` started;
   - `converterMask` (int: bit 0 Vessel A north, bit 1 Vessel B south);
   - `overflowAt` (-100 none): phase 3's start.
   `.Ironworks` passes them on as `IronworksHazardInput.ConverterAt`, `ConverterMask` and `OverflowAt`.
5. **Hazard definitions** in `NativeIronworksHazards`, derived from those inputs. Then `StepIronworksHazards` damages them as
   burners, unchanged.
   - `-converter-tilt` (north, existing) and a new `-converter-tilt-s` (south), from `ConverterAt` and the mask: warn 2.5,
     live 4, clear 2.
     - Proposed lane volumes: centre (-39.3, 0.3, ±7), size 5.4 × 0.6 × 2.4. That is the pour's landing and the runner lane to
       the pit. The record's 4 × 4 box covered only the landing.
   - A new `-pit-overflow`, kind slag, `BossDriven`, from `OverflowAt`: period 14, live (spread) 8, cool (crusting) 6.
     Volume: centre (-35, 0.3, 0), size 7.2 × 0.6 × 7.2.
   - The ladle crane stays parked (owner decision 10).
   - The converter crane's hook is presentation only (it swings on the slam and the blow).
6. **Scripted adds** on top of the `Encounter` trickle, through the existing spawn path and caps:

| Kind | Spawn | Rule | Cap |
| --- | --- | --- | --- |
| Cinderling | the blow's clot circles | 2 per blow (4 in phase 3), rising 1.2 s after landing. Needs a spawn at a world point, not an area entry: proposed `SpawnPveAddAt(kind, position)`, still bounded by `ZoneKindRoom` | 40 |
| Slagback | the shed's rubble; slag patches; trenches 29, 30 | 2 at the rubble 6 s after the shed; 1 per deslag at a slag patch (same point spawn); the trenches stay in the mix | 40 |
| Ladleman | entry 22 (crane-bay drop) | once, after the first call in phase 2. Needs `Cap("ladleman", …, boss)` to return 1 for this scripted spawn only; the mix keeps 0 | 1 |
| Rigger | entry 28 (`vent-conv-rail`) | unchanged | 2 (the live boss rule) |
| Stoker | the mix (.15) | unchanged. Optional rule: a Stoker within 2 m of the boss rakes the tuyères for 2 s, and the next blow comes 30% sooner | — |
| Shambler | the mix | unchanged | — |

   The design's own caps on the clots are 6 alive; ask for a separate counter if the generic 40 is too high.
7. **Damage application.**
   - The boss's hits use the existing `Damage()` path, as `ZoneHit` does for adds.
   - Its leftovers (pour pool, blow splashes, slag patches, charge trail) use `ZonePilot` and the `ZoneTouch` ledger at the
     burner rate (2 every 0.25 s), under hazard ids from 200.
   - Hits on the boss read the part under the hit point, as the Rigger's chain does: crust groups, tuyère grate, foreman,
     mouth, tongs, knee rams.

### 10.3 Health and damage numbers (base for one pilot, scaled by `ExpeditionScaling`)

- Boss health: `NativePveRules.BossHp(members)` (6,000 solo, +50% per extra pilot). Phases at 70% and 35%; damage past a
  threshold carries over.
- Crust groups:
  - each has 5% of maximum health of its own;
  - hits on an intact group's side deal 40% to the boss and 100% to the group;
  - a broken side takes 115%;
  - any crust left breaks at 70%.
- Critical multipliers: the tuyère grate ×2.0 (exposed in the blow's inhale and column, kneel and rear), the foreman ×1.5,
  the mouth ×1.5 (while it faces the shooter in the pour), the knee rams ×1.25.
- Choke: 4% of maximum health to the grate inside the blow's inhale.
- Stagger meter: 8% of maximum health of critical or knee damage within 10 s gives a kneel, then a 12 s immunity.
- Hits:

| Source | Damage |
| --- | --- |
| Pour stream | 22 |
| Blow droplet | 16 (1.0 m) |
| Stomp | 26 and a 0.6 s knockdown (1.9 m) |
| Grab | 10, then 6/s held, then 14 thrown (6 m). Release on 200 to the tong |
| Slam shockwave | 24 (0.6 m tall ring; a jump clears it) |
| Slam splash | 14 |
| Deslag lump | 20 (1.4 m) |
| Charge | 34 and 6 m knockback |

- Burns: the pour pool (4 s), blow splashes (1 s), slag patches (6 s), the charge trail (3 s), the hall vessels' lanes (4 s)
  and the pit overflow (8 s) all burn at the live burner rate (8/s).
- None of this is lethal on contact from full health; all of it can be dodged.

### 10.4 Action scheduler (proposal)

- One action at a time, within the section 6 phases and cooldowns.
- Choice by range:
  - stomp under 3 m, when a pilot is by a foot;
  - grab at 3–7 m, at a single target;
  - pour at 3–9 m, toward the most pilots in the wedge;
  - slam when two or more pilots are within 6 m;
  - deslag when a pilot has stood behind it for 3 s;
  - blow every 16 s regardless;
  - call every 25 s in phase 2+;
  - charge in phase 3 at a pilot 8–16 m away along a clear line.
- A 0.8 s minimum gap between actions (0.5 s in phase 3).

### 10.5 Prompt for Astra (ready to send)

**Intro hold, settled 2026-10-17:** keep the intro hold at 9.0678 s. The owner's boss theme is merged with an intro edit whose drop lands exactly at the hold's end (`docs/ZONE-BOSS-MUSIC.md`), so the arrival already ends on the drop with no hold change. This supersedes the hold items below. If the hold length ever changes, Claude regenerates the beat map (`tools/zone_boss_beatmap.py`).

> Astra: the City Ironworks boss design is on `claude/boss-city-ironworks` (`docs/EXPEDITION-BOSS-CITY-IRONWORKS.md`,
> section 10). The owner approved it on 2026-10-17 (every recommendation) and it is merged into shared; not live yet. It builds on your live zone systems (PR #264, #265, #267). Claude owns the look,
> model, animation, VFX and audio. Please accept, amend or refuse:
> (1) Archetype `ironworks-converter` for the exp-foundry boss encounter in `SpawnExpeditionBoss`, replacing `siren` there.
> Give it its own known-archetype predicate, boss point 0, yaw 90. Collision: a 2.7 m × 7 m capsule 0.6 m behind the
> trunnions, and four non-blocking leg hurt volumes. Walk 1.6 m/s; turn 35°/s, or 65°/s in phase 3. It stays in the hall.
> (2) Three phases at 70% and 35%, played as the `shed` and `break` actions with no reveal hold. The intro hold ends on the
> drop of "Hydraulic Hell" (beat map in `docs/ZONE-BOSS-MUSIC.md`), or 5.5 s without one.
> (3) A scheduler for the eight attacks and seven moments, with the exact timings in section 6. `NativeIronworksBoss.Actions`
> in Core mirrors the table.
> (4) `NativeZoneBossFrame` in `NativePveFrame` as `zoneBoss`: `action`, `actionAt`, `actionYaw`, `targetX`/`targetZ`,
> `targetSeat`, `variant`, `phase`/`phaseAt`, `parts`, `partAt[5]`, `stagger`, `deathAt`. Use `NativeZoneEnemyFrame`'s wire
> rule (finite, -100 none, arena `Time`). Start times and targets only.
> (5) `NativeZoneHazardFrame.converterAt` / `converterMask` / `overflowAt` (hazard-clock seconds), passed through
> `.Ironworks` as `IronworksHazardInput.ConverterAt` / `ConverterMask` / `OverflowAt`. In `NativeIronworksHazards`, drive
> `-converter-tilt` and a new `-converter-tilt-s` from them (warn 2.5 / live 4 / clear 2; lane volumes (-39.3, 0.3, ±7),
> 5.4 × 0.6 × 2.4). Add `-pit-overflow` (phase 3; period 14, live 8, cool 6; (-35, 0.3, 0), 7.2 × 0.6 × 7.2).
> `StepIronworksHazards` then burns them unchanged.
> (6) Scripted adds through the existing spawn path: a `SpawnPveAddAt(kind, position)` for the blow's Cinderling clots and the
> shed/deslag Slagbacks; one Ladleman from entry 22 after the first call (`Cap` returning 1 for that scripted spawn only); the
> Rigger on entry 28 with the live boss cap of 2; Stokers and shamblers from the live boss mix.
> (7) Part damage, crits, choke, the stagger meter and the damage numbers in 10.3. Hits go through `Damage()`; leftovers use
> the `ZoneTouch` ledger at the burner rate under hazard ids from 200.
> The hit timing in the doc is the authority's; the presentation only draws it from `actionAt`. Tell me the final field names
> and any numbers you change, and I will bind the presentation (the model on the motion library, VFX, HUD plate name and cue
> routing) after your rules land.

## 11. Owner decisions (recommended picks in bold)

Owner, 2026-10-17 (verbatim): "Go with your suggestions with the convertor too." Every row takes its recommended pick. Item 10 is met by the music's intro edit with the hold kept at 9.0678 s (see section 10.5).

1. **Concept: THE CONVERTER (Vessel C, the walking converter with its dead foreman).** Alternates: the Foreman on the crane,
   the Slag Giant (section 1).
2. **Plate name: THE CONVERTER**, with VESSEL C stencilled on the body. Alternates: VESSEL C, THE HEAD MELTER.
3. **Size as drawn**: 8 m tall and 8.6 m between the front feet, filling the hall's middle while leaving about 7 m lanes north
   and south.
   Alternate: 85% scale, at the cost of the colossus read.
4. **Grab-and-throw allowed**: up to 1.6 s of lost control, with allies able to free the pilot. Alternate: a tong snap with
   no hold.
5. **The hall's vessels join in phase 2** (the reserved converter-tilt hazard), with the ladle crane still parked (decision
   10).
6. **The phase 3 pit overflow** shrinks the middle every 14 s, crusting back for 6 s. Alternate: no overflow.
7. **It stays in the hall.** The shutter opening is the pilots' retreat, not a lane it can follow into.
8. **No new mocap.** The boss is procedural (springs, IK, timelines). Later the foreman can move onto the captured pilot rig
   so his lever pull is skeletal.
9. **Beat-map `music/city-ironworks-boss`** once it lands, for presentation sync only (section 9).
10. **The intro hold ends on the drop** of "Hydraulic Hell" (from the music agent's beat map), so the arrival lands on the
    song (5.5 s without a map). Alternate: keep the Siren's 9.07 s.

## 12. Renders (session scratchpad, not committed)

Offline WebGL renders through the game's own shaders (NativeBlockLit, NativeMolten, NativeHeatHaze), the live
`expedition-foundry` map, the real Ironworks kit, light field, lights and effects, the real pilot body, and the concept model
posed by its key poses. The shutter shots use the design manifest's gate, which is now the live exp-layout-6's. The boss's effects are the harness's sketch of the
VFX list. In `/tmp/claude-0/-home-user/4cd8d659-77bb-56f7-9323-e1639fb4be49/scratchpad/renders-boss-ironworks/`:

- turnaround with a pilot for scale: `turn-front.png`, `turn-front34.png`, `turn-side.png`, `turn-back34.png`, `turn-back.png`
- arrival: `arrive-shutter.png` (through the opening shutter, from the casting floor) and `arrive-hall.png`
- attacks:
  - `attack-pour-windup.png`, `attack-pour.png`
  - `attack-blow-inhale.png`, `attack-blow.png`
  - `attack-stomp-lift.png`, `attack-stomp.png`
  - `attack-grab-reach.png`, `attack-grab-hold.png`
  - `attack-rear.png`, `attack-slam.png`
  - `attack-deslag.png`
  - `attack-charge-windup.png`, `attack-charge.png`
- stagger: `stagger-kneel.png`
- phase changes: `phase2-shed.png`, `phase2-call.png`, `phase3-runaway.png`
- death: `death-cooled.png`

## 13. Files and status

| File | What |
| --- | --- |
| `docs/EXPEDITION-BOSS-CITY-IRONWORKS.md` | this design |
| `unity/ChipArenaConversion/Assets/ChipArena/Core/NativeIronworksBossModel.cs` (+ `.meta`) | the concept model and the action table (inert) |
| `unity/ChipArenaConversion/Tests/IronworksBossModelChecks.cs` | its managed checks (registered in `Tests/Program.cs`: the full suite and `--ironworks-boss`) |

Not done here:
- The runtime draw, motion, VFX, HUD name and cue routing: Claude, after Astra's contract.
- Rules, AI, collision, replication and hazards: Astra.
- The audio clips: the owner.
- The beat map.

Nothing has run in Unity, on Windows or with two clients.
