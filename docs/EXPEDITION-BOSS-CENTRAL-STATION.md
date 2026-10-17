# Expedition boss: Central Station — THE 6:47 (design)

Claude, 2026-10-17. Branch `claude/boss-central-station`, based on shared `claude/sleepy-ritchie-g8ruhv` at `dfb8aac` and
fast-forwarded to `ce85919` (Astra's PRs #263-#265: zone music, the station's four enemies shipped, the station hazards
live through `Core/NativeZoneHazards.cs`, `Core/NativeZoneCombat.cs` and `Runtime/NativeExpeditionZoneHazards.cs`). It was then merged
with shared `f3d8eb4`, which adds the boss motion library and Astra's verified zone gameplay release.

**Status: design approved by the owner on 2026-10-17 (every recommended option in section 11) and merged into shared. Not live.** It adds one inert Core file (the concept model and its key
poses), its managed checks and this doc. No runtime file, live resource, rule, AI, director, wire or network field
changes. Today the station still fights the placeholder Siren (`SpawnExpeditionBoss`, archetype `siren`). Every number
here is a proposal for Astra, who owns rules, AI, numbers, networking and authority (section 10).

Owner (2026-10-17, verbatim): "let us begin on the bosses for these two areas~ I want these animations to be a lot more
fluid than what we have now" and "A high quality design matching the areas too". The City Ironworks boss is a separate
task. The shared fluid-motion library (`docs/BOSS-MOTION.md`, `Core/NativeBossMotion*.cs`) was a third task. It merged
into shared at `f3d8eb4`, and this branch merges that. Section 7 maps every motion onto its types. The concept model does
not call it: the model stays inert, and the binding comes later.

| What | Where |
| --- | --- |
| This design | `docs/EXPEDITION-BOSS-CENTRAL-STATION.md` |
| The concept model: skeleton, geometry (3 tiers), 17 key poses, hit volumes, the proposed move table, the motion numbers (inert) | `unity/ChipArenaConversion/Assets/ChipArena/Core/NativeStationBossModel.cs` |
| Managed checks (`CoreTests --station-boss`; in the full run) | `unity/ChipArenaConversion/Tests/StationBossModelChecks.cs` |
| Concept renders (not in the repo) | section 12 |

## Contents

1. The concept
2. The body
3. The arena
4. Phases
5. Attacks
6. Moments: adds, staggers and breaks, the arrival, the death
7. Animation direction
8. Presentation: name, effects, audio, music
9. The concept model
10. Astra contract (proposal) and the prompt for Astra
11. Owner decisions
12. Renders, checks and what is not done
13. Presentation build

## 1. The concept

**THE 6:47: the evacuation special finally arrives.**

The station's story is a train that never came. The board has said `6.47 EVAC SPECIAL 2 DELAYED` since the night the
city fell. The concourse is the evacuation point: cots under the arcades, a field hospital, luggage on trolleys. The
evacuees waited for the 6:47, and the horde came out of the trains instead. The train standing at platform 2 reads NOT IN
SERVICE, so it is not the 6:47. The 6:47 never reached the station. It stopped somewhere in the west tunnel with
everyone aboard.

The boss is that train. When the quota is done, the PA says "This is the last call for the six forty-seven", the board
flips, and the 6:47 rolls out of the west tunnel on the express line, braking hard, every window lit amber and packed
with faces. It stops at the crossing, unfolds six legs made of its passengers' arms, climbs out of the pit and smashes
through the platform barrier into the concourse it never reached. The Stationmaster stands waist-deep in its cab roof,
grown into it, in his frock coat and top hat, holding up the pocket watch that stopped at 6:47 and ringing the last-call
bell. Station PA horns grafted to the cab announce its moves with the station's own chime.

It combines the three recorded ideas, each with a clear job:
- **The Express** gives the body: a locomotive horror out of the west tunnel. It is made specific as *the 6:47*, the
  train the board has been waiting for, and grounded in what is already built: the express line, the tunnel mouth, the
  pits and the barrow crossing.
- **The Stationmaster** gives the face: a humanoid focal point whose gestures are the telegraphs (he points the watch, he
  raises it to the vault, he rings the bell). He is also the critical weak point, and his top hat makes the silhouette
  readable from the far side of the concourse.
- **The Voice of the Station** gives the sound: every big move is announced with the station's four-note PA chime, and
  the strike lands on the beat after the fourth note. It is a telegraph language unique to this boss and learnable by
  ear.

Why it is the strongest: it answers the station's open question (where is the 6:47?) and it pays off what the players
have read on the board for the whole run. It makes the evacuation point the arena, and it turns the station's machines
(the express, the third rail, the levers, the glass vault) into its moves and its punishments. It is the logical top of
the station's enemy family: the Pileup is commuters fused with one carriage door, and the 6:47 is a whole train fused
with its passengers. It also reads instantly at night, because a dark shape with a row of lit windows and one blazing
headlamp means "train" at any distance.

Alternates, one line each:
- **The Voice of the Station:** a PA Siren echo screaming through every speaker. Atmospheric, but it has no body to
  fight, and it repeats Ward Zero's Siren.
- **The Express itself:** a nameless fused locomotive. Spectacular, but generic, and it competes with the scheduled
  express hazard instead of using it.
- **The Stationmaster alone:** a giant official with the timetable. Characterful, but human-scale against a 22 m
  cathedral, so it under-uses the arena.

## 2. The body

**Silhouette and scale.** It reads as a train on six arched legs with a top-hatted figure on top. Against a pilot (1.75 m):

| Measure | Metres | Times a pilot |
| --- | --- | --- |
| Carriage (shell, buffers to torn rear) | 9.9 long, 2.9 wide, 2.7 tall | |
| Underframe height standing | 1.55 | |
| Roof | 4.3 | 2.5x |
| Stationmaster's hat top | 6.7 | 3.8x |
| Knees (mid and rear legs; the front arms' elbows) | 3.5 (2.6) | 2x |
| Footprint standing (hands and all) | 10.8 wide, 13.1 long | |
| Terminus rear-up (front hands) | 9.9 | 5.7x |

That is readable from across the concourse and from the balconies (y 6), where the Stationmaster is at eye level. It
stays inside the concourse's 24 m depth with room to turn.

**Materials and palette** (the station's own, `NativeExpeditionStationKit`):

| Part | Colour | Why |
| --- | --- | --- |
| Livery | station green `#2C463C` with a cream band `#B6AA8C` and brass lining `#C8A040` | the station's ironwork and tile; an older train than the stainless express, so it reads as *the special* |
| Nose | the trains' yellow warning panel `#D2A41C`, black chevrons, an iron plough | the station's own train language |
| Windows | sodium/board amber `#FFB45A`, passengers' silhouettes, hands pressed on the glass | the departure board's colour: the train the board was waiting for |
| Headlamp | one cold white eye `#F2F6FF` | moonlight against the amber |
| Limbs | drowned grey-green flesh `#6B7366`, the evacuees' coats (navy, brown, grey, army khaki, maroon), amber veins | the horde that came out of the trains |
| Stationmaster | black frock coat, gold braid and epaulettes, maroon waistcoat, white gloves, top hat with a gold band, amber eyes | the station's last official |
| Pantograph and cables | iron, copper, blue insulators and sparks (Linesman blue `#6FD8FF`) | the third rail's power |
| The Heart | hot amber-white `#FFD27A` | the one warm thing in the carriage: the weak point |

**Lit parts:** the window band and the doors' windows (amber), the destination blind (`6.47 EVAC` in amber), the cab-side
windows, the headlamp and two marker lamps, the Stationmaster's eyes and his watch face, the stopped wristwatches on
every leg (amber dots), the amber veins, the Heart (through the torn rear end, and through the roof once it opens), and
the pantograph's insulators (blue).

**Weak points** (proposed multipliers, section 10):
- **The Stationmaster** (rider capsule): 2.0x; 2.5x in TERMINUS, when he has risen out of the hatch.
- **The Heart** (the torn rear end, and the open roof in TERMINUS): 1.5x; 2.0x while the boss is staggered.
- **The nose and plough:** armoured, 0.6x.
- **The legs:** 0.8x. Limb hits never stagger.

**Breakable parts:**
- **The headlamp:** 4% of max health, from the front only. Once it is broken, Departure's lock telegraph slows from 1.2 s to
  1.6 s and the charge can no longer re-aim. The beam goes dark, so the boss is visibly half-blind.
- **The pantograph:** 5% of max health, while raised or from above (best from the balconies). It snaps and falls, and Live
  Rail is gone for the fight.
- **The side shell:** broken by the express (Mind the Gap, section 6). That flank shows the passengers and takes 1.3x.
- **The doors' arms:** 120 damage releases a held pilot. This is not permanent.
- **The roof** peels back at the TERMINUS phase change. It is scripted, not shot off.

## 3. The arena

The grand concourse is the set piece (owner decision 1 of the station design). It is 60 x 24 m of open floor between
the balcony posts, with the booth under the departure board at the centre, kiosks, benches, the girder, the platform
barrier with its three gates to P1, the T1 express pit behind it, the glass vault overhead, and balconies at y 6 on both
sides.

Each part of the concourse has a job in the fight:
- **The departure board** is the boss's clock. It reads `ALL SERVICES CANCELLED` (live today) and row 0 adds
  `6.47 EVAC SPECIAL 1 ARRIVING` through the arrival. The boss tears it down at the TERMINUS phase change, and it ends
  the fight reading `DEPARTED`.
- **The platform barrier and the express lane.** In LAST CALL the barrier holds. At PLATFORM ALTERATION the boss smashes
  the centre two segments (x -18..18), and the concourse joins P1 and the T1 pit. The express then becomes part of the
  fight: it is lethal to pilots in the pit and can hit the boss (Mind the Gap).
- **The tunnel mouth.** The 6:47 arrives out of the west mouth on T1. In PLATFORM ALTERATION the Linesmen walk out of it.
- **The glass vault.** Platform Alteration brings panes down on the pilots. More panes go at each phase change: the moon
  shafts widen and the puddles on the floor grow (the live puddles of Live Rail).
- **The flood.** It is far west in T3, so the boss does not use it. Its role in this arena goes to the puddles under the
  broken panes, which Live Rail electrifies.
- **The signal box levers.** These are live today and work during the boss. A pilot who runs the 100 m to the box and
  throws them cuts the power for 60 s: Live Rail fails (the pantograph only sparks) and the express halts. This is a
  real co-op choice: one pilot leaves the fight to make it safer.
- **The booth, the kiosks, the balcony posts and stairs, and the fallen board (TERMINUS)** are crash walls for Departure,
  where the boss staggers.
- **The balconies** are the vantage for the Stationmaster and the pantograph. A Conductor rings the last call from them
  in LAST CALL.

**Boss points.** The 6:47 is 13 m long. With its root at today's point 0 (0, 0, 3), its nose sits 0.4 m from the booth.
Proposal: move point 0 to (0, 0, 1.5) yaw 0, where the arrival ends under the board. Move points 1 and 2 to (-12, 0, 5)
yaw 20 and (12, 0, 5) yaw 340. Those two are where the phase changes play. Its lane graph is in section 10.

## 4. Phases

| Phase | Health | Arena | Moves | Adds | Escalation |
| --- | --- | --- | --- | --- | --- |
| **LAST CALL** | 100-65% | the concourse as built; barrier intact; the express runs its normal schedule behind the barrier | Buffer Swipe, All Aboard, Departure, Platform Alteration | the light horde from the concourse doors; one Conductor at a time on a balcony | learn the chime, the lane, the doors and the glass |
| *phase change* | at 65% | "Platform alteration": the boss turns to the barrier, rears, and smashes the centre two segments; P1 and the pit join the arena | | Next Stop once (4 adds) | the arena opens to the express |
| **PLATFORM ALTERATION** | 65-30% | barrier down; the boss calls the express every ~40 s; Mind the Gap; ambient glass every 9 s | + Shunt, Live Rail; Departure lanes can cross P1 | + Linesmen out of the T1 tunnel mouths, an Undertow in the T1 pit | the machine turns on the players, and the players can turn it on the boss |
| *phase change* | at 30% | "Terminus": the boss tears the departure board off its chains (it crashes across the booth), the roof peels open, the Stationmaster rises ringing the bell, every clock in the station strikes, more panes fall | | Next Stop once | the boss opens up: weaker, faster, angrier |
| **TERMINUS** | 30-0% | board down (new cover and a crash wall); the roof open; ambient glass every 6 s | + Terminus, Next Stop; Departure becomes a double charge; turns 95°/s | + a Pileup out of the boss's doors (once) | the Heart and the Stationmaster exposed; the big slam |

Each phase change is a 3.5 s busy window (proposed invulnerable, like today's `reveal` holds but without freezing the
simulation). The arena change lands at its end.

## 5. Attacks

The action timeline is anticipation (the telegraph), strike, follow-through and recover, then the cooldown. The table
is `NativeStationBoss.Moves`, and `StationBossModelChecks` holds it to this doc. Damage is in today's add units before
party scaling.

| Id | Move | From phase | Anticipation (s) | Strike (s) | Follow-through (s) | Recover (s) | Cooldown (s) | Damage | Shape |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `buffer` | Buffer Swipe | 1 | 0.55 | 0.18 | 0.3 | 0.45 | 3 | 20 | front 120°, 4.4 m, 4 m/s knockback |
| `doors` | All Aboard | 1 | 0.8 | 0.25 | 1.5 | 0.6 | 9 | 6 | a 3.2 x 2.6 m box off the flank door; hold up to 1.5 s (1.0 s solo) at 8 dps |
| `departure` | Departure | 1 | 1.2 | 1.4 | 0.7 | 0.8 | 11 | 34 | a 3.2 m lane, 22 m at 16 m/s, 9 m/s knockback |
| `alteration` | Platform Alteration | 1 | 0.6 | 1.2 | 0.2 | 0.6 | 14 | 18 | one falling pane per pilot (up to 4), r 1.4 (the live glass values) |
| `shunt` | Shunt | 2 | 0.7 | 0.2 | 0.35 | 0.5 | 6 | 24 | 5 m, 70° cone, 10 m/s knockback |
| `live-rail` | Live Rail | 2 | 1.2 | 4 | 0.3 | 0.8 | 18 | 8 | per second in the live puddles (r 2.2), 0.35 s jolt |
| `terminus` | Terminus | 3 | 1.4 | 0.15 | 0.85 | 1 | 16 | 40 | slam within 3 m; ring to 10 m at 12 m/s, 0.6 m lip (18 to anyone who does not jump it) |
| `passengers` | Next Stop | 3 | 1 | 2 | 0 | 0.5 | 25 | 0 | 4 adds from the doors (once in TERMINUS: a Pileup) |

### Buffer Swipe (`buffer`)
- **Telegraph, 0.55 s.** The near front arm draws back across the body, which yaws 10° away and drops 0.15 m. The shoulder
  wheel spins backward. The buffers' rims flash amber at 0.25 s. Audio: `boss.station.swipe-windup` (an iron creak and
  many voices inhaling) at 0.
- **Strike, 0.18 s.** The arm sweeps 120° across the front at hand height 1.0-2.4 m. Audio: `boss.station.swipe`.
- **Follow-through and recover, 0.75 s.** The body over-rotates 6° and springs back, the window arms swing, and the hand
  replants. This is the punish window on the swing side.
- **Counter:** step out of 4.4 m, or stay behind the front arms. It is the boss's answer to pilots hugging its nose.

### All Aboard (`doors`)
- **Telegraph, 0.8 s.** The door's amber windows brighten and the leaves shudder open 5 cm, so blades of light lie on the
  floor. An amber rectangle (3.2 x 2.6 m) marks the reach. Audio: a garbled PA "mind the doors" (`boss.station.doors-warn`)
  and a pneumatic hiss at 0.
- **Strike, 0.25 s.** The doors slam open and a mass of arms shoots out. A pilot in the rectangle is caught (6 damage).
- **Hold, up to 1.5 s (1.0 s with one pilot in the instance; the Undertow's owner rule).** The pilot is dragged to the
  doorway at 2.6 m/s and takes 8 dps.
  - **Release:** 120 damage to the arms from teammates, 40 from the victim, any stagger of the boss, or the limit.
- **Recover, 0.6 s.** The arms withdraw and the doors close.
- **Counter:** read the door glow and step out; shoot the arms. The flanks are never safe for long, and the Heart is
  behind them.

### Departure (`departure`): the signature
- **Telegraph, 1.2 s.**
  - **The chime:** the PA chime plays four notes 0.3 s apart (0, 0.3, 0.6, 0.9 s) and the charge launches on the fifth
    beat (1.2 s).
  - **The crouch:** the boss crouches (the body drops 0.55 m, the nose dips 6°) and the front hands dig in and spark.
  - **The lock:** the headlamp flares and locks on the target, and the Stationmaster points the watch.
  - **The lane** lights on the floor: two amber rails 3.2 m apart, with chevrons that pulse toward the target on each note.
  - **Audio:** the wheels spin up (`boss.station.wheels`) and steam bursts from the underframe.
  - **The lane locks on the fourth note (0.9 s).** A sidestep after it is safe.
- **Strike, 1.4 s.** It charges 22 m down the lane at 16 m/s in a bounding gallop. The chain and cables stream behind
  it.
- **Follow-through, 0.7 s.** The front hands plant and skid, the body yaws 14° and rolls 9°, and the tail swings out.
  Sparks and skid scorch follow.
- **Recover, 0.8 s.** It turns back on the gait (section 7).
- **Crash:** if the lane meets the booth, a kiosk, the balcony posts or stairs, the fallen board or a wall, the boss
  crashes. That is a 2.2 s stagger, and the Heart takes 2x.
- **TERMINUS:** a double charge. The skid becomes a 0.6 s turn and a second, two-note chime.
- **Counter:** step out of the lane after the fourth note, bait it into the booth or the kiosks, or break the headlamp.

### Platform Alteration (`alteration`)
- **Telegraph, 0.6 s.** The Stationmaster raises the watch to the vault and the boss blows its horn (`boss.station.horn`,
  a deep two-tone). The PA horns glow amber.
- **Strike, 1.2 s.** The live falling-glass event: an amber ring and a growing shadow under each pilot (up to 4), and
  the glass creaks (`station.glass.creak`, already in the bank). The shards land at 1.8 s.
- **Follow-through and recover, 0.8 s.** The boss stands with its arm up for 1.8 s in all. This is a punish window.
- **Counter:** jog out of the ring (1.2 s is enough) and do not stand together. It answers balcony campers and
  long-range kits.

### Shunt (`shunt`)
- **Telegraph, 0.7 s.** The nose lowers, the buffers glow, and the front hands come up to chest height, palms forward.
  Audio: `boss.station.brace` (an iron scrape).
- **Strike, 0.2 s.** A 5 m, 70° cone with 10 m/s knockback. With the barrier down, it can throw pilots off P1 into the
  T1 pit, worst when the express is due.
- **Follow-through and recover, 0.85 s.** The body over-travels 0.5 m and settles.
- **Counter:** sidestep the cone, and never stand between the boss and the pit during a warning.

### Live Rail (`live-rail`)
This move needs the pantograph unbroken and the power on.
- **Telegraph, 1.2 s.** The pantograph springs up (overshoot and settle), and blue arcs climb from its head to the
  vault's iron ribs. The front hands kneel on the floor. The puddles under the broken panes start to glow and crackle.
  Audio: `boss.station.panto` and a rising hum.
- **Strike, 4 s.** Six puddles (r 2.2) are live: 8 dps and a 0.35 s jolt. Arcs leap from the boss's hands to the nearest
  puddles.
- **Recover, 1.1 s.** The pantograph folds.
- **Counter:**
  - stand on dry floor;
  - break the pantograph;
  - throw the signal-box levers (Live Rail fails for 60 s);
  - kill any Linesman before he grounds himself in a puddle.

### Terminus (`terminus`)
- **Telegraph, 1.4 s.**
  - **The chime:** four notes 0.35 s apart (0, 0.35, 0.7, 1.05 s), and the slam lands on the fifth beat (1.4 s).
  - **The rear-up:** the boss rears onto its rear and mid legs (the body rises 1.85 m and pitches up 28°) and lifts its
    front hands to 9.9 m.
  - **The Stationmaster** raises the bell and the watch.
  - **The clocks:** every clock in the station ticks with the notes, and dust falls from the vault.
- **Strike, 0.15 s.** The slam deals 40 within 3 m of the hands. A ring of lifted floor tiles rolls out at 12 m/s to
  10 m (0.83 s). Its 0.6 m lip deals 18 and a 0.5 s stumble to anyone who does not jump it.
- **Follow-through, 0.85 s.** The body bounces (a heave spring overshoot) and the roof flap and the Stationmaster whip
  forward.
- **Recover, 1.0 s.** The longest punish window. The Heart is open through the roof.
- **Counter:** be more than 10 m away or jump the ring, then punish the recover.

### Next Stop (`passengers`)
- **Telegraph, 1.0 s.** Every door bangs and amber light floods out. A crowd moans through the metal
  (`boss.station.spill`) and the PA says a garbled "next stop".
- **Strike, 2 s.** Four adds step out of the doors in turn. Once in TERMINUS, a Pileup comes out carrying one of the
  boss's own door leaves.
- **Counter:** kill the adds at the doors and fight away from the flanks.

**Choosing moves.** The proposed AI keeps 6-9 m from its target. It swipes or shunts at the nose, uses All Aboard at the
flanks, Departure at range, and Alteration against height or range. It moves on its lane graph (section 10). No two big
telegraphs overlap: one chimed move at a time, and never Departure during an express warning unless the lane runs away
from the pit.

## 6. Moments

### Adds: the station's four, in their roles

The live boss mix today is `NativeZoneCombat.StationBoss`: shambler 0.5, runner 0.3, Linesman 0.1, Conductor 0.1. The
caps are Conductor 2, Linesman 2, Undertow 2, Pileup 1. Proposal: a mix per phase.

| Phase | Mix | Where they come from | Their role against the boss |
| --- | --- | --- | --- |
| LAST CALL | shambler 0.6, runner 0.3, Conductor 0.1 (cap 1) | the concourse doors and grates; the Conductor from a balcony door | the Conductor rings the last call from the balcony and buffs the horde: climb up and kill him |
| PLATFORM ALTERATION | shambler 0.45, runner 0.25, Linesman 0.15, Conductor 0.1, Undertow 0.05 (cap 1) | + the T1 tunnel mouths (Linesmen walk in along the rails), the T1 pit by the crossing (the Undertow) | a Linesman grounded in a live puddle is the Live Rail combo; the Undertow grabs toward the pit when the express is due |
| TERMINUS | shambler 0.4, runner 0.3, Linesman 0.1, Conductor 0.1, Undertow 0.05, Pileup 0.05 (cap 1) | + the boss's own doors (Next Stop) | the Pileup steps out of the boss carrying one of its doors |

Horde cap during the boss: 8, plus 2 per extra pilot. The phase changes clear nothing. Next Stop plays once at each phase
change.

### Staggers and breaks

| Moment | Trigger | Result |
| --- | --- | --- |
| **Crash** | Departure meets a wall, the booth, a kiosk, the balcony posts or stairs, or the fallen board | 2.2 s stagger: the nose crumples, one front arm buckles, steam vents, the Stationmaster slumps; the Heart takes 2x |
| **Mind the Gap** (PLATFORM ALTERATION on) | the carriage box overlaps the express's lethal band (\|z + 12.5\| < 2.5) during a pass | the express hits the boss: 8% of max health, a 3 s stagger, thrown 4 m along the line in a storm of sparks; the struck flank's shell breaks (1.3x for the rest of the fight). The AI avoids the pit during warnings; players bait Departure lanes that cross the edge |
| **Headlamp shot out** | 4% of max health to the lamp, from the front | a 0.8 s flinch; the beam dies; Departure is slower and cannot re-aim |
| **Pantograph snapped** | 5% of max health to the raised pantograph or from above | a 0.8 s flinch; it falls in sparks; no Live Rail |
| **Door arms beaten** | 120 damage to the out-thrust arms | the held pilot drops; the doors slam on the arms |
| **Phase changes** | 65% and 30% | 3.5 s scripted; the arena change |

### The arrival (the existing boss flow)

The flow is unchanged. When the quota is done the authority records the arrival (`bossArrivals[]`, archetype
`station-647`, at boss point 0) and starts the intro hold (`BeginPveHold("intro")`, 9.07 s, simulation frozen). The zone
frame's `boss`/`bossAt` are set (`StepZoneHazards`, live), the PA plays the last call at `bossAt` (`CueLastCall`, live), and
the board reads ALL SERVICES CANCELLED (live). Because the simulation is frozen, the whole arrival is presentation timed
from `spawned`, and it ends exactly on the authority's pose. A late joiner (more than 3 s, `LateJoin`) sees the boss
already in place and the HUD plate.

**Synced to the boss theme.** The owner's theme is "Station Chime of Doom" (`music/central-station-boss`). It starts with
the intro (`BossSongTime` 0 at the hold's start). The choreography hangs on the song's own landmarks, not on fixed
seconds. Their exact times are the beat map's, in `docs/ZONE-BOSS-MUSIC.md` (`claude/zone-boss-music-sync`). This doc
does not analyse the song. It names the beats it wants:
- **chime hit:** the song's opening chime accent;
- **light cut:** an intro accent where the stage's lights cut;
- **intro accents:** the hits between them;
- **the drop.**

The light cut and the board are the music agent's stage reactions; this design only asks what happens at them. The
seconds in the table are the fallback when no beat map is installed.

| Beat (fallback s from `spawned`) | What happens |
| --- | --- |
| **Chime hit** (0.0) | The PA chime and "This is the last call for the six forty-seven." The board flips; row 0 reads `6.47 EVAC SPECIAL 1 ARRIVING`, blinking. |
| **Light cut** (1.6) | The concourse and shed lights cut. In the dark, T1's west signal turns green and the 6:47's headlight blooms in the west mouth, the only light in the station. A deep two-tone horn sounds that is not the express's (`boss.station.horn`). |
| **Intro accents** (3.0-6.8) | Each accent is one beat of the approach. It rolls out of the tunnel on its wheels, legs folded (the Rolling pose), windows blazing, and brake sparks burst on the accents (`boss.station.brakes`). It stops at the crossing. The legs unfold on the next accent (`boss.station.unfold`). It heaves out of the pit onto P1 and turns 90° to face the concourse in one continuous turn on its gait. Between accents the motion is continuous; only the impacts snap to the beat. |
| **The drop** (6.8) | It bursts through the centre barrier gate (the Arrival pose): it rears, the front arms come over the barrier and the headlamp sweeps the party. The gate posts and PLATFORM signs fly (`boss.station.barrier`). The lights slam back on, and the banner shows `BOSS ARRIVAL · CENTRAL STATION` / `THE 6:47` (proposed per-archetype delay; today's banner runs 0-3.4 s). |
| After the drop (to the hold's end) | It plants at boss point 0. The Stationmaster rises from the hatch and raises the watch. |
| Hold ends (9.07 today) | The fight starts. |

**One coordination point for Astra and the music doc.** The intro hold is 9.0678 s today, the Siren song's lead. For
this track the hold should end on, or just after, the drop: set it from the beat map's drop time for
`music/central-station-boss`, so the burst-through and the start of the fight land together. Everything before the drop
is presentation from `spawned` and stretches to fit; only the final plant must match the authority's pose.

The barrier gate is decoration in this arrival: the centre gate is open already. The two barrier segments only fall at
the PLATFORM ALTERATION phase change.

### The death (fits the existing 4 s victory hold)

| From `diedAt` (s) | What happens |
| --- | --- |
| 0-1.2 | The legs buckle in sequence, rear to front, 0.15 s apart: each hand's plant spring gives and the knees fold. The body drops onto its wheels with a heave overshoot, and the chain and cables whip. |
| 1.2-2.5 | The headlamp flickers and dies. Steam vents. The windows go dark one by one from the rear as the passengers sink, and the window arms go limp. |
| 2.5-3.5 | The Stationmaster slumps forward. His top hat falls and rolls across the floor. The pocket watch drops and its hands move to 6:48. |
| 3.5-4.0 | Every clock in the station ticks to 6:48. The board flickers: `6.47 EVAC SPECIAL DEPARTED`. PA: "The six forty-seven has departed. This station is now closed." |

## 7. Animation direction

The owner's main ask is *much more fluid*. Every motion here is built from the shared library (`docs/BOSS-MOTION.md`).
Nothing is a hard cut between canned poses. The numbers are `NativeStationBossMotion`. The rig follows the library's
"build a boss's own rig" pattern (`BossCrawlerRig` is the nearest reference: six legs on a tripod gait, a dragging
cable). Its parts:
- a `BossGaitDef` for the six legs;
- one `BossActionDef` per move with key tables;
- `BossChain`s for the chain and cables;
- `BossSecondOrder` parts for everything that lags;
- the layers on top;
- the pose written by joint index into a `BossPose` for `NativeBossPoseApply`.

The model's joints already use the library's convention: local -Y down a limb.

**Replicated root: `BossRootFollow`.** The authority sends the root (position and yaw) and each move's record, never
per-frame poses.
- **The follow:** `Observe` per snapshot, then `StepAt(arena.ReplicaRenderTime)` on a guest. The host feeds each fixed
  step and presents a step behind on the visual clock.
- **Proposed settings:** `PositionHalfLife` about 0.12 s; `MaxTurnRate` 70°/s (95°/s in TERMINUS) and `MaxTurnAccel`
  about 180°/s², through its `BossTurn`; a heavy lean (`MaxBank` 4°, `MaxPitch` 3°, `LeanFrequency` 1.2 Hz,
  `LeanDamping` 0.5). The `root-*` channels in `NativeStationBossMotion` are these targets in Hz and ζ.
- **Turns:** the body leads with the shoulders: the front hips turn first and the rear follow 0.12 s later, through the
  gait's `Twist`.
- **The arrival and late joins:** a `BossInertializer` hands the arrival's presentation pose to the live follow when the
  hold ends. A late joiner or a teleport never pops.

**Gait: `BossGait` with six `BossLegDef`s in two alternating tripods** (A = left front, right mid, left rear, phase 0;
B = the other three, phase 0.5).
- **Leg definitions:** `Hip` and `Home` come from the model's `Rest` and `Stance`. `Upper` and `Lower` come from `Thigh`
  and `Shin`. `Pole` is up and out (out and back for the front arms). `BodyHeight` is 1.55.
- **Ground:** the map floor through `IBossGround`; the track bed in the pit.
- **Walk:** 2.6 m/s, stride 1.9 m, duty 0.62, step height 0.45 m.
- **The swing arc:** each foot is planted by IK on the floor ahead. The swing lifts the hand, rolls the wrist and slaps it
  down with a 0.08 s squash (fingers splay).
- **The planted hand stays locked** until its tripod lifts.
- **Turning in place:** a foot re-steps when its hip-to-hand yaw error passes 25° or its reach passes 0.9 of the leg.
  Turning never pauses the gait.
- **Body response:** the body heaves on a spring (1.6 Hz, ζ 0.55) from the plant impacts. It leans into acceleration and
  turns (1.2 Hz, ζ 0.5), and it rolls 2-3° toward the swinging tripod.
- **The charge** switches to a bound: front pair, mid pair, rear pair, duty 0.45, step height 0.75 m. The switch blends
  over two steps. The wheels at the hips turn by distance (the follow's `Speed` over the 0.52 m radius).
- **The crouch, rear-up and slam** are body moves through the gait's `ExtraOffset`/`ExtraRotation`, so the planted hands
  stay planted. `BossIk.TwoBone` with a little `soft` keeps the arms from popping straight.

**Secondary motion:**

| Part | Library part | Behaviour |
| --- | --- | --- |
| The Stationmaster | `BossSecondOrder3` on the chest (2.2 Hz, ζ 0.35) and `BossLookAt` on the head | lags the body and overshoots: thrown forward on the brake, back on the launch |
| His hat | `BossSecondOrder3` (3 Hz, ζ 0.3) | tilts with his head; held on with the left hand during the charge |
| Window arms (4) | `BossSecondOrder3` each (1.4 Hz, ζ 0.25) | dangle and swing loosely |
| The coupling chain (4 links) | `BossChain` (Count 4, Length 0.34, MinStretch 0.95) | gravity 9.8, damping 0.02, 3 iterations; trails behind the charge, swings on the brake |
| The torn cables (2 x 3) | `BossChain` (Count 3, Length 0.62) | as the chain, and they spark at their ends |
| The pantograph | `BossSecondOrder` on the raise (5 Hz, ζ 0.3) | rises with a snap and a settle |
| The grab arms | `BossSecondOrder3` (4 Hz, ζ 0.45) | shoot out and wobble |
| The headlamp's aim | `BossLookAt` (half-life about 0.1 s) | tracks the target smoothly; locks hard at the fourth note |
| Steam | particles | at the underframe on every crouch and brake |

**Additive layers:**
- `BossBreath` on the Stationmaster's chest (0.25 Hz);
- the Heart's pulse (`BossBreath` at 0.75 Hz in LAST CALL, 1.0 Hz in PLATFORM ALTERATION, 1.4 Hz in TERMINUS);
- `BossFlinch` on hits (the body and the struck limb; never pauses the gait);
- `BossStagger` for the crash and the express hit (the body thrown along the push, the gait stepping after it);
- the passengers' windows flicker on hits.

**Actions: one `BossActionDef` per move**, driven by `BossActionPlayer.Play(def, actionId, actionAt, hitAt, duration)`
from the replicated record (section 10).
- **The phases** are the move table's: `Anticipation` (`InOut`), `Strike` into `Contact` (`In`, so the hand accelerates
  into the hit), `FollowThrough` (`OutBack`) and `Recover` (`Settle`).
- **Timing:** `BossTimeline` stretches the phases before contact to the authority's `hitAt`, so what is telegraphed is
  what hits.
- **The chime notes** are presentation events at fixed times inside the anticipation.

**Per action** (weight, anticipation, overshoot, follow-through):

| Action | Weight and anticipation | Overshoot and follow-through | Secondary |
| --- | --- | --- | --- |
| Buffer Swipe | heavy: the body counter-yaws 10° and sinks 0.15 m before the arm goes | the arm carries 20° past centre; the body over-rotates 6° and springs back | window arms swing out; the Stationmaster ducks |
| All Aboard | the leaves shudder (2 Hz) through the 0.8 s | the arms overshoot 0.3 m and wobble back while holding | the body rolls 9° toward the door; the chain swings |
| Departure | a deep crouch, the hands digging in (sparks), the nose dipping on each chime note in rhythm | launch: tail-heavy, then the bound; brake: hands skid, yaw 14°, roll 9°, tail out, then a heave bounce | everything streams back on the charge and whips forward on the brake; the hat flies up and is caught |
| Platform Alteration | the Stationmaster's arm rises over 0.45 s with an ease-in; the body rears 8° | the horn blast shakes the whole carriage (0.2 s, 3 Hz) | the window arms jolt |
| Shunt | brace: body back 0.3 m, palms up | over-travel 0.5 m, settle | |
| Live Rail | the front hands come down in two beats (left, right); the pantograph springs up | arcs jitter on a 12 Hz noise; the body trembles | the Stationmaster's arms spread and shake |
| Terminus | the rear-up over 1.4 s, slowing at the top (hang time on the fourth note) | the slam lands fast (0.15 s); the body bounces twice (heave spring); the roof flap and the Stationmaster whip forward | dust from the vault; the chain slams down |
| Crash stagger | | the nose crumples down 11°, roll 11°, one front arm buckles (its plant released), then a slow push back up through 2.2 s | steam; the Heart flares |
| Phase changes | each is authored on the timeline from `phaseAt`: the barrier smash (a Shunt into the barrier) and the board tear (a rear-up, the front hands hooking the board's chains) | the roof peel springs open and overshoots | |
| Death | legs give rear to front | heave bounce on the wheels | the hat rolls (spring) |

**Hit timing and networking impact.** Strikes land at the record's `hitAt` (`actionAt` plus the move's anticipation). The
presentation only stretches the follow-through, never the anticipation, so what you see telegraphed is what hits. The
gait and every spring and chain are presentation only. Hitboxes follow the authority's root and move state, not the
drawn limbs, except the breakable parts, whose positions come from the same model the authority would use (section 10).

## 8. Presentation

**HUD plate.** `THE 6:47`. Today's flow names an archetype through `NativeExpeditionPresentation.Name`, which would print
`STATION 647`, so it needs one name entry (`station-647` -> `THE 6:47`). That is Claude's, at wiring. The banner eyebrow
stays `BOSS ARRIVAL · CENTRAL STATION`. Proposed: a small phase tag on the plate (LAST CALL, PLATFORM ALTERATION,
TERMINUS), which needs the phase field.

**Effects:**
- headlamp beam and glare (and its death);
- window glow and flicker;
- the Heart's pulse;
- the PA horns' amber rings on each announcement;
- the Departure lane (amber rails and chevrons) and brake sparks with skid scorch;
- steam vents and dust;
- the doors' amber spill and the reach rectangle;
- the glass rings and shards (the live station effect, plus rings);
- the pantograph arcs to the vault and the live puddles (blue);
- the Terminus ring with its lifted-tile lip;
- the board crash (dust, sparks, the board's text going on flickering on the floor) and the barrier debris;
- the roof peel (sparks, the interior glow);
- the express impact;
- the headlamp shattering and the pantograph snapping;
- stagger steam and the Heart flaring;
- the death: steam, windows dying, the clocks moving to 6:48, the board reading DEPARTED.

**Audio cue keys** for the owner to generate. Space: P = positional, F = 2D. The station's existing keys are reused where
they fit: `station.pa.chime`, `station.pa.lastcall`, `station.glass.creak`, `station.glass.fall`, `station.train.*`.

| Key | Space | Brief |
| --- | --- | --- |
| `boss.station.horn` | P | A deep, detuned two-tone train horn, longer and lower than the express's, 1.5 s. |
| `boss.station.chime` | F | The four-note station chime, slowed and bent, each note a separate hit (four files or one with the timing). |
| `boss.station.announce` | F | The station announcer's voice breaking up: "the train now departing does not stop", garbled, 1.2 s. |
| `boss.station.doors-warn` | F | A garbled "mind the doors" and a pneumatic hiss, 0.8 s. |
| `boss.station.doors` | P | Carriage doors slamming open hard, 0.4 s. |
| `boss.station.grab` | P | Many hands grabbing, wet and frantic, 0.6 s. |
| `boss.station.unfold` | P | Six huge limbs unfolding from under a carriage: joints cracking, iron and flesh, 1 s. |
| `boss.station.wheels` | P | Steel wheels spinning up on rails, a rising whine, 1.2 s. |
| `boss.station.charge` | P | A train-sized body charging: rumble, roar, rails singing, 1.4 s. |
| `boss.station.brakes` | P | Brakes screaming, a long skid on steel, 1.5 s. |
| `boss.station.impact` | P | A train smashing into a wall: crunch, glass, metal groan, 1.2 s. |
| `boss.station.step` | P | A huge hand slapping down on stone with an iron clank under it (6 variants). |
| `boss.station.swipe-windup` | P | An iron creak and many voices inhaling, 0.5 s. |
| `boss.station.swipe` | P | A heavy limb sweeping air, 0.3 s. |
| `boss.station.brace` | P | Iron scraping on stone, 0.7 s. |
| `boss.station.panto` | P | A pantograph rising: a spring clack and a rising electrical buzz, 1.2 s. |
| `boss.station.arc` | P | A power arc leaping to iron girders, a crack and sizzle, 0.8 s (loopable variant). |
| `boss.station.rear` | P | A massive body rearing: metal stretching, the carriage groaning, 1.4 s. |
| `boss.station.slam` | P | A train-weight slam into stone: boom, cracking tiles, 1 s. |
| `boss.station.shockwave` | P | Floor tiles lifting and rolling outward, 0.9 s. |
| `boss.station.bell` | P | The Stationmaster's brass handbell, urgent, 1 s. |
| `boss.station.tick` | F | A huge clock tick, slowed (one hit; played on the chime notes in Terminus). |
| `boss.station.spill` | P | A crowd moaning through metal, doors banging, 1.5 s. |
| `boss.station.voice` | P | The passengers' low moan, loopable 3 s (idle bed). |
| `boss.station.hurt` | P | A metal groan with many voices crying out, 0.6 s. |
| `boss.station.lamp-break` | P | A big headlamp shattering, 0.6 s. |
| `boss.station.panto-break` | P | A pantograph snapping off in sparks, 1 s. |
| `boss.station.express-hit` | P | An express train slamming into a carriage at speed, 2 s. |
| `boss.station.barrier` | P | Steel barriers torn and thrown, 1.2 s. |
| `boss.station.board` | P | The departure board tearing off its chains and crashing, split-flaps rattling, 2.5 s. |
| `boss.station.roof` | P | A carriage roof peeling open, tearing steel, 1.5 s. |
| `boss.station.death` | P | A long brake release, steam, settling metal, the moan fading, 3.5 s. |
| `boss.station.clocks` | F | Every clock in the station ticking once together and chiming 6:48, 2 s. |
| `boss.station.departed` | F | PA: "The six forty-seven has departed. This station is now closed." |

**Music.** The boss track is the fixed key `music/central-station-boss` (`NativeZoneMusic.StationBoss`). It plays
automatically from the arrival (`BossSongTime >= 0`), looping on the shared boss clock, with the Siren's song as the
fallback until the owner's MP3 is installed. There is one track. The phases change the presentation, not the music. Beat
timings, proposed:
- **Gameplay never syncs to the music.** Every telegraph is authority-timed (the chime is the boss's own audio, played at
  the move's notes), so a missing or replaced track cannot change a hit.
- **Presentation may sync** to the beat map being built for this track (`docs/ZONE-BOSS-MUSIC.md`, the owner's
  "Station Chime of Doom", 87.2 s: tempo, sections, intro accents, drop, loop). Its uses here:
  - the arrival keys on the chime hit, the light cut, the intro accents and the drop (section 6);
  - the Heart's pulse, the window flicker and the PA horns' idle glow ride the beat;
  - the phase changes start their visual sting on the next bar line (at most one bar later, presentation only);
  - the death's last tick to 6:48 lands on a downbeat.

  Without a beat map these run on their own clocks (section 7). The stage's own beat reactions (lights, board) are the
  music agent's.

## 9. The concept model

`Core/NativeStationBossModel.cs` is real Core presentation code built with the same procedural kit as the station's
enemies (`WeaponSculpt`: chamfered boxes, lathes, tubes, tori; flat-shaded, with amber emissive accents). It is inert:
nothing outside the file references it, and the checks hold that.

**The skeleton** has 68 bones:
- the root and the carriage body;
- the parts: the headlamp, the Heart and the roof panel;
- the pantograph: three bones;
- the Stationmaster: ten bones (hips, chest, head, hat, and two arms of three each);
- six legs, each a wheel, a hip, a knee and a foot (the hand);
- four door leaves and two grab-arm chains (two bones each);
- the coupling chain (four links) and two torn cables (three links each);
- four window arms (two bones each).

Every bone carries geometry. A pose is a local rotation and an extra translation per bone, plus the root's placement.

| Tier | Triangles | Budget |
| --- | --- | --- |
| Full | 19,824 | 20,500 |
| Reduced | 9,984 | 10,500 |
| Low | 4,602 | 4,800 |

For scale: the Standard pilot is 7,782 triangles and an elite add is 3,500-5,100. This is the one boss on screen.

**17 key poses:**
- Stand and Walk (mid-stride, tripod A lifted);
- Rolling (legs folded, on the rails) and Arrival (rearing through the gate);
- the three beats of Departure: crouch, charge and brake;
- Swipe, All Aboard, Alteration, Live Rail and Shunt;
- the two beats of Terminus: rear and slam;
- Opened (the TERMINUS idle: roof peeled, the Stationmaster risen), Stagger and Death (with the hat on the floor).

The planted hands use a small two-bone IK solver in the file (authoring only). The library's gait does this live.

**Hit volumes (proposal):**
- **The carriage:** a box riding the body bone, in the body's frame: x ±1.62, y -0.45..3.85, z -4.9..5.75. Plough, roof
  arms and hatch lid are inside it.
- **The Stationmaster:** a capsule from (0, 4.6, 2.35) to (0, 6.75, 2.4), radius 0.8.
- **Out of the box while extended:** the open doors' arms, the raised pantograph and the peeled roof. They are separate
  breakable parts.
- **The standing footprint:** x ±5.7, z -6.8..7.6 (model space).
- **Every key pose's reach:** x ±6.2, y up to 10.4, z -9.8..9.2.

## 10. Astra contract (proposal)

Base: the systems live on shared `ce85919`. Everything below is named against them. Astra owns every value.

**What exists and is reused as is:**
- **The boss flow:** `RecordBossArrival` (`bossArrivals[]`: id, area, archetype, spawned, x/y/z, hp/maxHp) and
  `BeginPveHold("intro")` (9.0678 s).
- **Boss health and its scaling:** `NativePveRules.BossHp`.
- **The HUD plate's health:** `UpdateBossHealth`.
- **The zone frame** (`NativeZoneHazardFrame`, replicated as `pve.zone`):
  - `boss` and `bossAt`;
  - the express: `trainAt`, the 4 s warning, the lethal band `|z + 12.5| < 2.5`, the push to 3.1;
  - the levers: `leverAt` and `leverUntil` (60 s, the express halted);
  - the falling glass: `glassX`, `glassZ` and `glassAt` (up to 8 panes, 1.2 s telegraph, 18 damage, r 1.4);
  - `floodGrounded`.
- **The station's four enemies** (`NativeZoneEnemyFrame`, `StationViewOf`), the boss add mix
  `NativeZoneCombat.StationBoss` and its caps.

**What the boss needs on top** (exactly):

1. **A boss body that is not a pilot.** Today's boss is the Siren pilot seat (`Pve.Boss`). The 6:47 is 13 m long with a
   rider and breakable parts, so a pilot capsule cannot carry it. Recommended: a boss actor on the minion path (kind
   `station-647`, the add frame's replication), with:
   - the carriage box and the rider capsule from section 9;
   - part volumes: the lamp (a sphere, r 0.45 at the nose), the Heart (a sphere, r 1.0 at the rear), the raised
     pantograph (a box), the open doors' arms (a box);
   - legs as 12 limb capsules, at 0.8x and with no stagger.

   It moves on a lane graph, not the navmesh:
   - **Concourse:** (-20, 3.5), (-10, 3), (0, 1.5), (10, 3), (20, 3.5), (-12, 5), (12, 5).
   - **P1, from PLATFORM ALTERATION:** (-10, -6.5), (0, -6.5), (10, -6.5).

   It turns in place up to 70°/s (95°/s in TERMINUS) and walks at 2.6 m/s. The archetype in `bossArrivals[]` becomes
   `station-647`.

2. **One replicated boss record** beside the zone frame (`pve.zoneBoss`, finite wire floats with the -100 sentinel as
   the zone frame uses):

   | Field | Meaning |
   | --- | --- |
   | `phase` (1..3), `phaseAt` | the phase and the start of its 3.5 s change |
   | `action` (one of the 8 ids, or null), `actionId` (one per move), `actionAt`, `hitAt`, `duration`, `cancelAt` | the move as `BossActionPlayer.Play` takes it (`docs/BOSS-MOTION.md`): its start, the authority's hit (start plus the anticipation), its length, and a cut (a stagger or a phase change cancels a move) |
   | `actionTarget` (seat id) | the locked target (Departure, All Aboard, Alteration) |
   | `laneX0`, `laneZ0`, `laneX1`, `laneZ1` | Departure's lane (locked at the fourth note); X0/Z0 is also the centre of Shunt and Terminus |
   | `holdTarget`, `holdSide` (-1/+1), `holdUntil` | All Aboard's hold; `holdUntil` moves forward on an early release, set from the pilot count when it lands (1.5 s, or 1.0 s solo) |
   | `staggerAt`, `staggerSeconds`, `staggerPushX`, `staggerPushZ`, `staggerCause` (`crash`, `express`, `part`, `phase`) | staggers, as `BossStagger.Start` takes them |
   | `lampBrokenAt`, `pantoBrokenAt`, `expressHitAt`, `sideBroken` (bit 0 left, bit 1 right) | the breaks |
   | `liveAt`, `liveUntil` | Live Rail; the six puddles are fixed points in this doc and the presentation's table |
   | `barrierDownAt`, `boardDownAt`, `roofOpenAt` | the arena changes |
   | `spillAt`, `spillIds` (up to 6) | Next Stop and the adds it made |
   | `diedAt` | the death timeline |

   The root (x, y, z, yaw) and the health come from the actor's own frame.

3. **The arena hooks through the zone frame and colliders:**
   - **Glass:** Alteration fills `glassX/Z/At` (Time + 1.2 per target). The flat 4 s rain of the live boss stage becomes
     off in LAST CALL, every 9 s in PLATFORM ALTERATION and every 6 s in TERMINUS.
   - **The express:** in PLATFORM ALTERATION and TERMINUS the boss sets `trainAt` about 40 s apart, with its horn 6 s
     before the existing 4 s warning. The lethal band, the push and the lever halt are unchanged.
   - **Mind the Gap:** the express pass tests the boss's carriage box against the lethal band.
   - **The levers:** `leverAt`/`leverUntil` (live) make Live Rail fail while they hold.
   - **Colliders:** the `barrier-1` and `barrier-2` colliders turn off at `barrierDownAt`. A new board collider (10.4 x
     4.4 x 2.2, tilted 30° over the booth) turns on at `boardDownAt`.

4. **The adds per phase** (section 6), through `NativeZoneCombat.Encounter` with the boss phase instead of one boss mix.
   Next Stop spawns at the boss's door positions (body-local x ±1.47, z -0.35, turned to world).

5. **The intro hold** for this archetype ends on the drop of `music/central-station-boss` (the beat map in
   `docs/ZONE-BOSS-MUSIC.md`) instead of the Siren song's 9.0678 s lead (section 6).

6. **Boss points** for `exp-transit` (a layout data change, Claude's, when wiring): point 0 (0, 0, 1.5) yaw 0, point 1
   (-12, 0, 5) yaw 20, point 2 (12, 0, 5) yaw 340.

**Proposed numbers:**

| Item | Proposal |
| --- | --- |
| Health | `NativePveRules.BossHp` as today (6,000 x party scaling) |
| Phase thresholds | 65% and 30% |
| Damage multipliers | Stationmaster 2.0x (2.5x in TERMINUS), Heart 1.5x (2.0x staggered), nose 0.6x, legs 0.8x, struck flank 1.3x |
| Breakables | lamp 4% of max health (front only), pantograph 5%, door arms 120 per hold |
| Staggers | crash 2.2 s, express 3 s (8% damage), part break 0.8 s, phase change 3.5 s |
| Moves | the section 5 table |

**Animation impacts** (for the log): the strikes land on the record's `hitAt` (`actionAt` plus the anticipation). Nothing changes the existing kinds' or
hazards' timing, hitboxes, damage, cooldowns, movement or networking. The boss's own hit volumes follow its root and move
state.

**Prompt for Astra** (ready to send):

> Astra, the Central Station boss design is on `claude/boss-central-station` (docs/EXPEDITION-BOSS-CENTRAL-STATION.md;
> the inert concept model is Core/NativeStationBossModel.cs; base ce85919). It is THE 6:47, a 13 m six-legged carriage
> with the Stationmaster in its roof, fought in the concourse in three phases. It is built on your live systems, and
> nothing in it is wired.
>
> The owner approved the design on 2026-10-17, with every recommended option in section 11. Please implement the rules side:
> 1. A non-pilot boss actor `station-647` on the minion path:
>    - the carriage box and the rider capsule from section 9, plus the part volumes and limb capsules from section 10;
>    - it moves on the lane graph in section 10;
>    - `RecordBossArrival` uses archetype `station-647` at boss point 0.
> 2. A replicated `pve.zoneBoss` record with the fields in section 10 item 2 (finite wire floats, the -100 sentinel):
>    phase/phaseAt, action/actionId/actionAt/hitAt/duration/cancelAt/actionTarget (the shape BossActionPlayer takes), the lane, hold, stagger, the breaks, Live Rail, the arena changes, the spill
>    and diedAt.
> 3. The eight moves in the section 5 table: start times, target choice, the hit windows at hitAt (actionAt + anticipation), the
>    All Aboard hold (1.5 s, 1.0 s solo, released by 120 team damage, 40 self, or any stagger), and the AI rules at the
>    end of section 5.
> 4. The arena hooks through NativeZoneHazardFrame:
>    - Alteration fills glassX/Z/At, and the ambient glass rhythm changes per phase (off, 9 s, 6 s);
>    - the boss calls the express (trainAt ~40 s apart in phases 2-3, its horn 6 s ahead);
>    - Mind the Gap tests the carriage box against the lethal band;
>    - the levers make Live Rail fail;
>    - the barrier colliders turn off and a board collider turns on at their times.
> 5. The adds per phase via Encounter, and Next Stop spawning at the boss's doors.
> 6. The intro hold for `station-647` ends on the boss theme's drop (the beat map in docs/ZONE-BOSS-MUSIC.md),
>    not on the Siren's 9.0678 s lead.
>
> Every number is a proposal, yours to balance. Tell me the final field names and I will bind the presentation (model,
> gait, timeline, effects, HUD name THE 6:47, the arrival and death sequences, cues). Not in this ask: no change to the
> existing kinds, hazards or Ward Zero's Siren.

## 11. Owner decisions

Owner, 2026-10-17 (verbatim): "Go with your suggestions". Every row takes its recommended option, in bold below.

| # | Topic | Options | Recommended |
| --- | --- | --- | --- |
| 1 | Concept | THE 6:47 (the Express + the Stationmaster + the Voice) / the Voice alone / the Stationmaster alone | **THE 6:47** |
| 2 | Name on the plate | `THE 6:47` / `THE LAST DEPARTURE` / `THE STATIONMASTER` | **`THE 6:47`** |
| 3 | Size | 13 m long, the rider's hat at 6.7 m (as modelled) / smaller (about 9 m) for easier navigation | **as modelled**: it is the concourse's scale |
| 4 | Arena changes (they change collision) | barrier down in phase 2 and the board falling in phase 3 / presentation-only rubble | **both, with colliders** |
| 5 | The express in the fight | the boss calls it every ~40 s in phases 2-3, plus Mind the Gap / the normal 70-110 s schedule | **boss-called, with Mind the Gap** |
| 6 | The levers during the fight | allowed (one pilot runs 100 m to cut Live Rail and halt the express for 60 s) / locked during the boss | **allowed**: they already work |
| 7 | All Aboard's hold | the Undertow's rule (1.5 s, 1.0 s solo) / shorter | **the Undertow's rule** |
| 8 | Falling glass | driven by the boss (Alteration, then 9 s and 6 s ambient) / the live flat 4 s rain | **boss-driven** |
| 9 | The ending | the station's clocks start again at 6:48 and the board reads DEPARTED / a plain collapse | **the clocks and DEPARTED** |
| 10 | Boss points | move point 0 to (0, 0, 1.5) and points 1-2 to z 5 / keep (0, 0, 3) and z 6.5 | **move them**: the nose clears the booth |

## 12. Renders, checks and what is not done

**Renders.** These are offline WebGL renders, not in the repo, of the concept model in the live station (`expedition-transit`, its Core
kit, light picker and atmosphere; NativeBlockLit and NativeNightSky through the station harness). They are in
`renders-boss-station/` in the session scratchpad:
- the turnaround with a pilot for scale: `turn-front.png`, `turn-side.png`, `turn-back.png`, `turn-top.png`;
- the arrival: `arrival-hero.png` (bursting through the centre gate under CENTRAL, the headlamp on the party);
- one key frame per signature attack: `attack-departure.png` (the lane and the chime crouch), `attack-all-aboard.png`,
  `attack-alteration.png` (rings under four pilots, shards falling), `attack-live-rail.png` (arcs to the vault, blue
  puddles, the express warning on the board), `attack-terminus.png` (the slam and the jumpable ring);
- the phase change: `phase-terminus.png` (the departure board torn down across the booth, the roof open, the
  Stationmaster risen with the bell).

The renders place the concept's effects as quads (beams, lanes, rings, arcs, sparks) and keep the station's own glass,
board and express states. They are concept frames and do not show runtime timing.

**Checks.** `StationBossModelChecks` (`CoreTests --station-boss`, and in the full run) covers:
- the three tiers build, deterministic and within budget, and every bone carries geometry;
- the skeleton and the tripods;
- all 17 poses are finite and on the floor (the track bed for Rolling), and the planted hands land on their stance;
- the roof, doors and pantograph change only in their poses;
- the carriage box in every pose, the rider capsule, the footprint and the reach;
- the move table is well formed and matches this doc, and the chime lands the strike;
- the doc names the boss, the archetype and the music key;
- inert: no file under `Assets/ChipArena` or `Assets/Conversion` but the model references it.

**Not done:**
- everything at runtime: the boss actor, rules, AI, the record, the colliders and the add mix are Astra's;
- the presentation binding: the runtime model, gait, timeline, effects, cues and HUD name, after Astra's fields and the
  motion library;
- audio (the keys above are for the owner);
- the arrival and death sequences beyond their key poses;
- Unity Editor, Windows, and solo or multiplayer play: not run.

## 13. Presentation build

Claude, 2026-10-17, branch `claude/boss-station-presentation` (from shared `535b42c`). **On branch, not merged, not live.**
Presentation only: nothing here changes a rule, hitbox, collider, timing, damage, cooldown, movement or network field. In
normal play nothing changes: the boss's view is `None` until Astra binds it, so the placeholder Siren fights on and the
station draws, lights and sounds exactly as before. A DEV preview plays the whole fight without the authority.

**What is built** (all under `unity/ChipArenaConversion/Assets/ChipArena/`):

| Part | File | What it does |
| --- | --- | --- |
| The view | `Core/NativeStationBossView.cs` | `StationBossView` mirrors section 10's record (phase and phaseAt; action, actionId, actionAt, hitAt, duration, cancelAt, actionTarget; the lane; the hold; the stagger; the breaks; Live Rail; the arena changes; the spill; diedAt) plus the actor's root, yaw and health and the arrival's `spawned`. It has a `None`, and the wire's -100 sentinel converts with `Time`. The file also holds the arena's fixed places (Live Rail's six puddles, the barrier, the board, T1, the doors) and the floor the gait walks on (the pit's bed). |
| The model | `Core/NativeStationBossLook.cs` | The concept's skeleton and three tiers, plus 20 presentation joints: the PA horns on sprung stalks; the coat tails (3 links each); each side window, the cab's glass and the markers as their own lit joints (so they die one by one and flicker on hits); the torn flanks (drawn once the express breaks a side). A grime pass in the vertex colours adds no triangles; the lamp's lens and the joint collars are full-detail only. Tiers are 22,164 / 10,496 / 4,850 triangles against budgets of 23,000 / 11,200 / 5,000, and every added part is inside the carriage's box. |
| The moves | `Core/NativeStationBossMoves.cs` | One `BossActionDef` per move, with key poses from the concept's numbers. Each phrase's phases sum to the move table, and its first contact is at the anticipation (the record's `hitAt`). The file also has the two phase changes (the barrier smash, the board torn down), the stagger, the part flinch, and the arrival's and the death's poses. Buffer Swipe and All Aboard mirror to their target's side. |
| The rig | `Core/NativeStationBossRig.cs` | Root: `BossRootFollow` (a heavy turn and lean; during the arrival, a path from the west tunnel). Gait: `BossGait` (six legs in alternating tripods that morph into a front-mid-rear bound on the charge; planted hands locked; hinge-stable IK). Actions: three `BossActionPlayer`s (moves, phase changes, staggers). Chains: `BossChain`s for the coupling chain, the cables, the horn stalks, the window arms and the coat tails over the roof. Second-order dynamics: the Stationmaster's chest (thrown by the body's acceleration), his hat, the pantograph, the grab arms, the body's heave. `BossLookAt` for his head and the headlamp (locked on Departure's lane at the fourth note). Layers: breath, the Heart's pulse, the chime's nods, `BossFlinch`, `BossStagger`, `BossInertializer`. The death (legs giving rear to front, the hat falling and rolling) and the snapped pantograph use loose pieces. |
| The effects | `Core/NativeStationBossFx.cs` | Quads for every section 8 effect, each joint's light level, and up to three practical lights. Telegraphs follow the game's danger language: amber warns (`0xFFB020`, the hazard kit's arming colour), red strikes (`0xFF2A1A`), and the station's electricity is the Linesman's blue. Telegraphs are never thinned. |
| The stage hook | `Core/NativeStationBossStage.cs` | The arrival's beats in one place (`NativeStationBossBeats`) and what the station should show (`NativeStationBossStage.Shown`), described below. |
| The cues | `Core/NativeStationBossCues.cs` | The 34 `boss.station.*` keys of section 8 at their moments, queued for the audio. |
| The DEV preview | `Core/NativeStationBossPreview.cs` | A scripted record on a 96 s loop: the arrival, a walk and turn, all eight moves, the PLATFORM ALTERATION change, the crash stagger, Mind the Gap, the headlamp and pantograph breaks, the TERMINUS change, the death. |
| HUD | `Core/NativeZoneBossNames.cs` | The plate and banner name `station-647` THE 6:47 (through `NativeExpeditionStates.BossName`), and its banner waits for the drop. Every other archetype is unchanged (the Siren still reads SIREN). |
| The presenter | `Runtime/NativeStationBossPresentation.cs` | The binding point. The draw: one mesh per joint and tier with the game's NativeSourceLit material (emission, the hit flash, the station's practical lights through the actor-light path), the tier chosen by distance. The effects batch, the lights merged into the station's slots, the stage hook and the cue queue. The DEV panel toggle (Camera · "Station boss preview") and the preview's banner and plate. The audio drain uses NativeAudio's bank path with no stand-in, so cues are silent until the owner's clips exist. |

**The binding point for Astra.** It is one line in `Runtime/NativeStationBossPresentation.cs`:
`internal static StationBossView StationBossViewOf(NativeArena arena)=>StationBossView.None;`. Binding replaces it with a read
of the replicated `pve.zoneBoss` record and the boss actor's frame into a `StationBossView` (times on the arena clock; -100
becomes NaN through `StationBossView.Time`). The presenter follows the root through `BossRootFollow`; on a guest, the root can
instead be fed as snapshots (`rig.Follow.Observe` and `StepAt(arena.ReplicaRenderTime)`).

The presentation assumes three things of the rules:
- for the phase changes, the authority faces the boss at the barrier (PLATFORM ALTERATION) and at the board (TERMINUS);
- `barrierDownAt` and `boardDownAt` are the smash's and the tear's contact (the presentation lands them there);
- `roofOpenAt` is the peel.

The managed check `StationBossPresentationChecks` pins the `None` line; update it when binding.

**The hook API for the station's presentation** (`Core/NativeStationBossStage.cs`). The boss's presenter writes
`NativeStationBossStage.Shown` (a `StationBossStage`) and `ShownClock` every frame it draws, and the station's board, clocks
and lights read it. Until they do, nothing in the station changes (the renders apply it the way the station would).
- `Board`: `BoardOwn`; `BoardArriving` (row 0 reads `6.47 EVAC SPECIAL 1 ARRIVING`, blinking while `BoardBlink`); or
  `BoardDeparted` (row 0 reads `6.47 EVAC SPECIAL DEPARTED`). `Row0` gives the text and `BoardFlicker` the flip's stutter.
- `ClockHour`:`ClockMinute` for every station clock (6:47; 6:48 from 3.5 s after the death); `ClockTick` pulses on each strike.
- `Lights` is the house and practical lights' level (about .06 through the arrival's cut), `LightSurge` the slam back on the
  drop, and `WestSignalGreen` turns T1's west signal green.
- `BarrierFall` and `BoardFall` (0..1, the board's with its bounce) pose the barrier's centre segments and the board (their
  colliders are the authority's); `Shake` and `Dust` are for impacts.

**The arrival's beats.** `NativeStationBossBeats.Current` holds the doc's fallback (chime hit 0, light cut 1.6, accents 3.0,
3.95, 4.9 and 5.85, drop 6.8, hold 9.07) until the boss theme's beat map binds it through `NativeStationBossBeats.Use`. The
music work on `claude/zone-boss-music-sync` had not merged when this was built. The choreography keys on the beats:
- the headlight blooms in the west mouth on the light cut;
- brake sparks fly on the first two accents, and it stops at the crossing on the second;
- the legs unfold on the third (rear first), then it climbs and turns onto P1 until the drop;
- it bursts through the gate on the drop;
- it walks to the spawn pose and the Stationmaster rises with the watch by the hold's end, where the live follow takes over
  through the inertializer.

**Meeting the music work.** `claude/zone-boss-music-sync` (pushed, not merged when this was written) already makes the
station's lights and board perform to the boss song in game (`NativeExpeditionStationMusic`): the lights cut at the song's
bass cut and flare on its drop, and the board reads LAST CALL, then ALL SERVICES CANCELLED. Once both merge:
- the station should take the light level from the music stage and use this hook only for what the song does not cover:
  row 0's ARRIVING and DEPARTED, the clocks, the barrier and board poses, the shake and the dust. Applying `Lights` as well
  would cut the lights twice;
- the beats should come from the song. On the boss clock (the hold's start), its map has chimes at 1.33 to 3.69 s (a slam
  with the one at 3.01 s), the bass cut at 4.86 s, a slam at 6.21 s, chimes from 6.88 s (slams at 7.38 and 8.73 s) and the
  drop at 9.07 s, on the hold's end. The design's drop is 2.3 s before the hold's end. With the song's drop, the burst
  through the gate would straddle the handover to the live follow, so choosing which hits drive the four accents and the
  drop needs a look at the arrival again. `Use` refuses beats out of order.

**Shared-file lines** (each one call or entry):
- `Runtime/NativeSourcePresentation.cs`: `StationBossScenery(arena)` after the station's scenery;
- `Runtime/NativeExpeditionStationAudio.cs`: `StationBossCues(arena,menu)`;
- `Core/NativeDevTools.cs`: the `preview-station-boss` action;
- `Runtime/NativeDevPanel.cs`: its case, panel and HUD calls;
- `Core/NativeExpeditionStates.cs`: the zone boss name and the banner delay;
- `Core/NativeBossMotionGait.cs`: two opt-in fields, both off by default (`BossGait.Free` for legs a rig owns, and
  `BossGaitDef.TurnRadius` so turning on the spot keeps stepping). The reference rigs are unchanged.

**Checks** (`CoreTests --station-boss-presentation`, also in the full run):
- normal play's `None` pin: no draw, light, cue or station change, and the Siren's name and banner unchanged;
- the DEV preview's determinism;
- contact on the record's hit within one frame at 60 and 144 fps, also with the hit moved 15%;
- planted-hand slip under 1 cm (walking, turning on the spot, the 16 m/s bound);
- no pops across 12 transitions (the per-frame change halves when the frame rate doubles);
- 0 bytes per frame;
- the tier budgets and volumes;
- the HUD's name routing and banner delay;
- the cue keys against section 8's table.

**Not done:**
- the binding (Astra's record);
- the station presentation reading the hook (the board's new rows, the clocks' hands, the lights' cut, the barrier and board
  poses);
- the boss theme's beat map binding the beats;
- TERMINUS's double charge as its own turn and second chime (it plays as one stretched Departure);
- the owner's audio clips;
- the pocket watch's hands stay at 6:47 (the station's clocks get 6:48 through the hook);
- the DEV preview's path crosses the concourse benches (the feet pass through them; only the DEV preview walks there), and
  the express that knocks it in Mind the Gap is the station's hazard, so the preview shows the hit without the train;
- the Unity Editor, Windows and play: not run.
