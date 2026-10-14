# Hazard kit (Unity Chip Arena)

A reusable kit of map hazards and alarms, with one record schema, trigger and state model, look set and sound set.
The owner asked for it on 2026-10-14: "hazards, alarms (useful for Ward Zero) ... alarms for immersion (we need to
bring sounds in), saws, turrets, etc."

**Ownership.** Claude owns the kit's presentation: models, lights, VFX, sound, the DEV preview and alarm placement.
Astra owns all authority: hits, damage, collision, targeting, timing that decides a hit, networking and the sim
clock. Alarms are presentation only and ship now. Turrets and kit saws draw and sound in full in the DEV preview,
but they are not placed on any map. They wait for Astra to implement the authority contracts in sections 9 and 10.
The existing Factory saws and Ward Zero hazards are unchanged.

Code (all under `unity/ChipArenaConversion/Assets/ChipArena/`):

| File | What |
|---|---|
| `Core/NativeHazardKit.cs` | Schema, catalogue, palettes, default timings, validation, placement lookup, DEV gallery records |
| `Core/NativeHazardKitClock.cs` | Phase from trigger, strobe/beacon/sign/klaxon clocks, flash caps, beat lock, music reaction, turret preview timeline, saw pose and blood |
| `Core/NativeHazardKitBuild.cs` | `NativeKitScene`: the per-frame instanced parts, FX quads, practical lights and turret rounds of every piece |
| `Core/NativeHazardKitSound.cs` | `NativeKitSoundPlan` (loops, one-shots, falloff, duck, cap, trips) and `NativeKitSynth` (built-in fallback sounds) |
| `Runtime/NativeHazardKitWorld.cs` | Per-arena scene, placement loading, trigger adapter that reads replicated state, DEV gallery |
| `Runtime/NativeHazardKitPresentation.cs` | Drawing: `ArenaPresentation` partial |
| `Runtime/NativeHazardKitAudio.cs` | Sound on `NativeAudio`'s existing voices |
| `Assets/Conversion/Resources/HazardKit.json` | Presentation-only placements (Ward Zero and its Expedition instance) |
| `Tests/HazardKitChecks.cs` | Managed checks (`CoreTests --hazard-kit`) |

## 1. Catalogue

| Kind (`kind`) | Variants / looks | Mounts | Authority |
|---|---|---|---|
| `alarm-beacon` | Rotating beacon: a dome over a turning reflector, with beam cones and light on the walls. Looks `biohazard` (red), `lockdown` (amber), `evacuation` (green) | wall, ceiling, floor | none (presentation) |
| `alarm-strobe` | Strobe panel: a xenon flash behind a cage | wall, ceiling | none |
| `alarm-klaxon` | Klaxon horn: a red-painted bell, an indicator lamp, a shudder on each blast | wall, ceiling | none |
| `alarm-sign` | Lit warning sign: a backlit face with hazard-striped rails. Text from the look (BIOHAZARD, LOCKDOWN, EVACUATE) or `text` (A-Z and spaces, up to 14 characters) | wall, ceiling | none |
| `turret` | Sentry: a head on a yaw ring and yoke, with a sensor eye, twin rotary barrel packs, a laser sight, heat fins and a status light. Looks `security`, `industrial` | floor, ceiling, wall | Astra (section 9) |
| `saw` | Variants `floor-slot`, `wall`, `ceiling-drop`, `rail`, `pendulum`. Looks `industrial`, `surgical` | by variant | Astra (section 10) |

Alarm palettes are listed below. Each look's default sign text and loop key are the defaults in `NativeHazardKit.Look`.

| Look | Lamp | Sign | Loop key |
|---|---|---|---|
| biohazard | red `#FF2A22` | BIOHAZARD | `alarm.biohazard` |
| lockdown | amber `#FFA31A` | LOCKDOWN | `alarm.lockdown` |
| evacuation | green `#2DFF8E` | EVACUATE | `alarm.evacuation` |

## 2. Record schema (schema 1)

One record shape covers every kind. The JsonUtility shape takes vectors as `{x,y,z}`, and every field is optional
unless noted.

| Field | Meaning |
|---|---|
| `id` | Required; unique on the map |
| `kind` | Required; a kind from the catalogue |
| `variant` | Saws: `floor-slot`, `wall`, `ceiling-drop`, `rail`, `pendulum` |
| `look` | Alarm look, turret look or saw look (see catalogue) |
| `mount` | `floor`, `wall`, `ceiling` |
| `anchor` | Optional block id: `pos`/`rot` are then in that block's frame |
| `pos`, `rot` | Placement. `rot` is degrees (pitch, yaw, roll). Local +z faces out of the wall; +y is up |
| `size` | `size.x` scales the piece (0 means 1; turrets default to 1.4) |
| `a`, `b` | Saw path ends (`ceiling-drop`: housing to lowest point), in the record's frame |
| `range` | Turret reach (m); an alarm's light reach |
| `arc` | Degrees: a turret's sweep, or a pendulum's swing |
| `rate` | A klaxon's blast period (s, default 1.6), or a turret's rounds per second |
| `radius`, `speed`, `length` | Saw blade radius (m), travel speed (m/s), pendulum arm (m) |
| `gain` | Sound trim (0..4) |
| `music` | `free` (default) or `beat` (section 7) |
| `react` | The music dips and swells when this alarm trips within earshot (section 7) |
| `text` | A sign's words |
| `trigger` | `mode`, `events[]`, `hazard`, `first`, `period`, `offset`, `warn`, `active`, `cooldown`, `radius` (section 4) |
| `sounds` | `loop`, `pulse`, `start`, `stop` key overrides; `gain`, `inner`, `outer` falloff radii (m) |

Validation is `NativeHazardKit.Validate`. It requires unique ids, known kinds, looks, mounts, variants, modes and
events, and finite geometry. Turrets: range ≤ 60 m, arc ≤ 360°, rate ≤ 30/s. Saws: radius ≤ 2.5 m, speed ≤ 20 m/s.
A schedule's period must hold warn + active + cooldown, and an event trigger must name events or a hazard. A map
may place at most 96 pieces.

```json
{"id":"ward-klaxon-0","kind":"alarm-klaxon","look":"biohazard","mount":"wall",
 "pos":{"x":-16.0,"y":10.6,"z":-8.6},"rot":{"x":0,"y":90,"z":0},"music":"beat","react":true,
 "trigger":{"mode":"event","events":["wave-start","breach","boss-reveal","round-start"],"active":3.4}}
```

## 3. Placement

- **Presentation-only pieces (now).** `Resources/HazardKit.json` is a `KitSet` (`schema` 1, `placements[]`). Each
  placement names a `map` id or a map `look`, and every map with that look gets its records. The file is read and
  validated once. A broken file turns the kit off with a warning and the game runs on. No authority reads this file.
- **Authority pieces (after Astra).** Turrets and kit saws that hurt anyone belong in the map's
  `UnityMaps.json hazards[]`, so the authority loads, validates and replicates them. The proposal is a kit
  `MapHazard` kind (`turret`, or `saw` with a variant) carrying the same record fields. The presentation then draws
  it from the replicated state (sections 9 and 10). Until then, a turret or saw in `HazardKit.json` would only be
  scenery. None is placed.

## 4. Triggers

`trigger.mode`:

- `always`: active for good.
- `schedule`: a cycle that starts at `first` + `offset`, every `period` s.
- `event` (the default): fires on the named events. `radius` > 0 keeps only events within that many metres of the
  piece, and `hazard` keeps only events of that stage hazard.
- `manual`: an adapter or the DEV preview fires the piece.

`trigger.hazard` also makes the piece **follow** that stage hazard's own phase. Its warn is the piece's arming, its
danger and active are the piece's active, and its clear is the piece's cooldown. The stronger state wins.

Events are read once a frame from replicated state, by `NativeHazardKitWorld.Triggers`. Each event carries its own
time, so the ledger is idempotent: host, guests, late joiners and the killcam agree.

| Event | Source |
|---|---|
| `round-start` | `Match.FightAt` in Fight/Result |
| `wave-start` | A change of `Pve.Wave` while `Pve.Stage == "wave"` (stamped when the client sees it) |
| `boss-phase` | A change of `Pve.Phase` during the boss stage (stamped when seen) |
| `boss-reveal`, `victory`, `defeat` | `Pve.Holding` with `Pve.Cinematic`; start = now − (HoldDuration − HoldLeft) |
| `breach` | `arena.WardBreaches[]` at `breakAt`, placed at the entry's centre (static) or `point` (dynamic Hulk breach) |
| `lockdown` | A Ward shutter's Warn start (`NativeWardZeroHazards.At`), at the shutter's centre, with its id |
| `door-seal` | `NativeHazardKitWorld.Event(arena, "door-seal", at, where, id)`: for the Expedition closure "sealing" state once Expedition binds an arena (not bound yet) |
| `dev-trigger` | `arena.DevHazardPulses` (DEV panel · Hazards · Trigger) |

Gap: `wave-start` and `boss-phase` are stamped with the local clock when the change is seen. That is fine for
presentation, but a replicated wave start time would make them exact for late joiners (Astra, low priority).

## 5. States

`NativeKitState`: `Idle`, `Arming` (the warning), `Active`, `Cooldown`, `Disabled` (powered off or hacked) and
`Broken` (destroyed). Default timings (s) are below. Each can be overridden by `trigger.warn`, `trigger.active` and
`trigger.cooldown`.

| Kind | Arming | Active | Cooldown |
|---|---|---|---|
| Klaxon | 0.35 | 3.4 | 0.8 |
| Sign | 0.8 | 10 | 2.5 |
| Turret | 0.8 | 3 | 1.2 |
| Saw | 1.2 | 4 | 1.6 |
| Beacon, strobe | 0.6 | 8 | 2.5 |

Each state looks like this:

- **Beacons** spin up through arming, turn while active (1.1 turns/s Full, 0.6 Reduced, 0.3 Calm) and coast down
  through cooldown.
- **Strobes** flash at half rate while arming.
- **Klaxons** blast every `rate` s.
- **Signs** blink once a second while arming (steady in Reduced and Calm) and are lit while active.
- **Saws** hide in their slot or housing while spinning up, come out and run while active, and go back in and spin
  down through cooldown.

All clocks are pure functions of (record, clock, fire time, beat, flash mode) and allocate nothing.

## 6. Alarms, light and photosensitivity

- **Light on the walls.** Each lit piece offers a practical light. A beacon's light swings with its beam. A strobe's
  light flashes with it. A klaxon's light glows red on each blast. Kit lights are merged into the arena's 8
  practical-light slots after the map's own pick (`NativeLighting.KitLights`). At most 3 are merged on high quality,
  2 on medium and 1 on low. They fill free slots or replace the least useful ones, de-duplicated per piece and eased
  in and out. When the kit sends none, the old upload runs unchanged.
- **Settings · Effects · Alarm lights**: `Full` / `Reduced` / `Calm`. **Reduced effects** forces at least Reduced.
  - **Full**: crisp 60 ms flashes, at most **3 per second** (free-running 2.5 per second). This is the WCAG 2.3.1
    general flash threshold.
  - **Reduced**: soft cosine pulses at most **2 per second**, between 25% and 80%.
  - **Calm**: no flashes; a slow 0.3 Hz glow, and beacons turn slowly at 60% light.
  - Beat-locked lamps flash on every k-th beat, with the smallest k that stays under the cap.
- **Ward Zero and its Expedition instance** get 30 placements by the `quarantine` look:
  - 4 amber **lockdown beacons**, one over each shutter door on the corridor side. Each follows its shutter
    (`siren-shutter-n/s/e/w`) and also fires on lockdown, door-seal and dev-trigger. They stay active 9 s and are
    beat-locked. The north one makes the music react.
  - 4 **LOCKDOWN signs** over the same doors on the atrium side.
  - 8 red **biohazard strobes**, one over each breach panel. They fire on `breach` within 7 m, or dev-trigger.
  - 4 **BIOHAZARD signs** beside the cells and lane breaches.
  - 4 **klaxons** high on the atrium walls. They fire on wave start, breach, boss reveal and round start, are
    beat-locked, and klaxon 0 reacts.
  - 4 green **evacuation beacons** under the atrium roof and 2 **EVACUATE signs**. They fire on boss reveal and
    defeat and stay active 16 s.

## 7. Music tie-in

- **`music: "beat"`** locks a piece to the playing track's beat map. Today that is Black Siren Protocol's shared map,
  `NativeBossTrack`, the same map the Siren intro and the song vents read, while `arena.BossSongTime` runs:
  - klaxons blast every 2 beats;
  - strobes flash on beats, under the cap;
  - beacons put the beam forward on the beat, one turn every 2 beats (Full) or every bar.

  With no beat-mapped track playing, the piece free-runs. Plain bpm grids are supported (`NativeKitBeat.Grid`) for
  tracks that only have a tempo.
- **Klaxon fit.** A beat-locked blast plays the bank variant that rings within 2 beats. It is the longest clip that
  fits, sped up by at most 6%. If none fits, it plays at its natural rate (`FitVariant`). The licensed bank has a
  third variant cleanly time-stretched (pitch kept) to exactly 2 beats of Black Siren Protocol (143.6 BPM, 0.836 s).
  The synthesized fallback is sped up by at most 6%.
- **Trip reaction (`react`).** When a reacting alarm trips within earshot, the music dips to 55% in 60 ms, holds for
  0.35 s, swells to 110% and settles by 2.4 s. Reduced and Calm dip only to 80% and swell to 103%. The reaction
  multiplies the music volume: Settings · Music and the mute stay in charge. Overlapping trips take the deepest dip
  and do not stack swells.
- **DEV preview** "Lock Hazard Kit to music" plays Black Siren Protocol at 55% of the music volume, from the moment it
  opens, so the owner can hear the beacon and klaxons locked to it.
- **What the music system needs for more:**
  1. Beat maps for the other tracks. Ward Zero's Containment Sirens (restricted-zone-pulse) has none, and its own
     embedded sirens would clash with a free klaxon. Beat-locked pieces free-run on it today.
  2. Section markers or stems, to switch to an intense section on a trip. None exist; the kit does not invent them.
  3. Music on its own AudioSource or GameObject, so a trip can low-pass only the music. All voices share one
     GameObject today, so the reaction is a volume dip and swell, not a filter.

## 8. Audio

There are 19 new bank keys in `NativeSoundKeys`. Every key has a deterministic built-in synthesized fallback
(`NativeKitSynth`, 22.05 kHz), so CI, cloud builds and public source zips sound complete without LocalAssets.

| Key | Space | Cap (s) | Priority | Use |
|---|---|---|---|---|
| `alarm.biohazard`, `alarm.lockdown`, `alarm.evacuation` | loop | 3 / 3.2 / 4.5 | normal | The look's alarm loop (beacons; strobes when `sounds.loop` names one) |
| `alarm.klaxon` | 3D | 1.2 | normal | One blast (3 variants, one fitted to the beat) |
| `alarm.trip` | 3D | 1.5 | normal | The trip sweep and yelp |
| `alarm.beacon` | loop | 3.2 | low | The beacon motor (pitch follows its speed) |
| `turret.servo`, `turret.scan` | 3D / loop | 0.9 / 2.4 | low | Traverse and scanning hum |
| `turret.lock` | 3D | 0.35 | high | Lock beeps, accelerating from 0.22 s to 0.06 s apart |
| `turret.spinup`, `turret.fire`, `turret.overheat`, `turret.break`, `turret.spark` | 3D | 1.2 / 0.35 / 2.5 / 1.8 / 0.6 | normal (spark low) | Spin-up, rounds, vent, shutdown, damaged sparks |
| `saw.whine` | loop | 2.4 | normal | The running blade (rate and level follow its speed) |
| `saw.spinup`, `saw.spindown`, `saw.scrape`, `saw.hit` | 3D | 1.6 / 1.8 / 1 / 0.8 | normal (scrape low) | Spin-up, spin-down, slot scrape, wet chop on a hit |

Mix rules:

- **Falloff** is full volume inside `inner` and a smooth cosine roll to silence at `outer`. The defaults are 5/42 m
  (alarms), 7/60 m (klaxons) and 2.5/22 m (machines).
- **Duck.** Alarm loops give way to shots, hits and blasts within 25 m of the listener, down to 40%.
- **Cap.** Alarm loops are capped together, so two alarms are not twice as loud.
- **Voices.** At most 3 kit loops hold voices, and all at low priority (never one of the 4 reserved voices).
  One-shots use the bank's voice pool.
- The effects volume and the mute apply to everything.

The licensed clips are 46 entries from Universal Sound FX (Imphenzia), trimmed, capped, loudness-normalised and
encoded to Ogg. They ship in the LocalAssets zip and never in this repository; pack and file names are listed in
`docs/ASSET-CREDITS.md`. The effects bank budget rises from 12 MB to 13 MB (the merged bank is 12.22 MB).

## 9. Turret: presentation and Astra contract

**Presentation (done).** The turret draws as follows:

- a scan sweep with a cyan-white laser sight;
- **acquire**: tracking, with an amber laser and eye;
- **lock**: the fair warning. The laser goes red, beeps accelerate and the barrels spin up over 0.8 s before any
  round;
- **fire**: bursts with muzzle flash, tracers and impacts, reusing `NativeWeaponFxDraw` shapes and the machine-gun
  muzzle look. Rounds travel at 90 m/s;
- heat fins on top that glow as the barrels heat;
- **overheat**: the head droops, the laser goes off, an orange status light and steam from the fin vents;
- **damaged** (health < 0.5): a jittering servo, sparks and smoke;
- **disabled**: the head slumps, with a dim blue eye and status light;
- **destroyed**: slumped and unpowered, with a smoke column and rare sparks.

Ceiling mounts hang inverted; wall mounts sit on a bracket. In the DEV preview, the 16 s timeline in
`NativeTurretPreview` drives it.

**Contract (Astra; proposed).** The authority owns everything that decides a hit. Paste-ready:

> Turret hazard (kit kind `turret` in `UnityMaps.json hazards[]`): fields as in docs/HAZARD-KIT.md 2 (`pos`, `rot`,
> `mount`, `range` default 18 m, `arc` default 110 degrees about the mount's forward, `rate` rounds/s default 10).
> The authority:
> - targets the nearest living, visible, hostile pilot or monster in range and arc, with LOS from the muzzle and the
>   team rule of the mode (PvE: players are hostile; PvP: everyone, or the owning team if one is set);
> - must hold the target for `lock` = 0.8 s (state Acquire then Lock) before the first round;
> - fires bursts with spread (proposal 1.5 degrees), damage per round (proposal 6) and heat per round. It overheats
>   at heat 1 (no fire for 2 s) and cools when idle;
> - has HP (proposal 150) and can be hacked or switched off (Disabled) if the mode allows.
>
> It replicates per turret, about 10-20 Hz: state (Offline, Scan, Acquire, Lock, Fire, Cool, Overheat, Disabled,
> Destroyed) and the time it began, yaw/pitch (degrees, in the turret's frame), target seat (-1 none), last-shot time
> and burst count, heat 0..1, health 0..1, and the last round's hit point (or none). Hits go through the normal
> hit/damage events (kind `hazard`, source the turret id) so kill credit, the killcam and the hit sounds work.
> The presentation reads exactly `NativeTurretView`.

## 10. Saws: presentation and Astra contract

**Presentation (done).** Each variant has its own housing:

- a floor slot with hazard paint and end lamps;
- a wall slot;
- a ceiling housing with a telescoping arm;
- a rail with its carriage;
- a pendulum pivot and arm.

Lamps show the state: green idle, amber blinking while arming, red while active, amber during cooldown. While
running, the blade has:

- a spin-up and spin-down;
- a motion-blur disc and a steel rim glint, so it reads in the dark and edge-on;
- sparks fanning from the contact point with a flickering practical light;
- scrape marks along a slot, and a hot streak behind the blade.

Blood darkens the blade after hits and fades to a stain; it is presentation, fed by hit events. Audio is the whine
loop, spin-up, spin-down, scrape, and the wet chop on each hit. Pose is `NativeKitSaw.Pose` (pure): floor-slot and
wall run back and forth along a→b, ceiling-drop drops a→b, rail rides a→b, and pendulum swings `arc` on `length`.

**Contract (Astra; proposed).** Generalise the Factory saw authority (`NativeFactoryHazards` `saw-slot` and
`saw-rail`, unchanged today) to the kit variants. Paste-ready:

> Kit saw hazard (kind `saw` + `variant` in `hazards[]`): the authority computes the live blade volume from the same
> pure pose (`NativeKitSaw.Pose` on the record, phase and match clock; or its own equivalent, which the presentation
> then matches). The blade is live only while Active and exposure > 0.3. The authority applies the lethal or damage
> rule (record `damage`/`lethal`, as Factory saws) and knockback, and emits hit events (kind `hazard`, source the saw
> id). Trigger: schedule or event (wave start, breach, DEV trigger) on the replicated clock, as section 4.
> Replicate: fire time (or schedule), so every client derives the same pose; and saw hit events with time and
> position, which the presentation turns into blood and the chop.

## 11. Existing hazards and migration

| Existing | Where | Kit equivalent | Migration (Astra decides when) |
|---|---|---|---|
| Factory `saw-slot` (2), `saw-rail` (2) | `NativeFactoryHazards` and the Factory presentation | `saw` floor-slot / rail | Keep. Optionally express as kit records with `trigger.mode = "schedule"` (period, warn, rise, travel) once the kit saw authority exists. Until then the Factory rig and its `hazard.saw` sound are unchanged |
| Factory press, crane, vents | Factory | not in the kit | None |
| Ward Zero shutters (4) | `NativeWardZeroHazards` (shutter) | Lockdown alarms follow them via `trigger.hazard` | Done (presentation only; shutter authority unchanged) |
| Ward Zero burners, gas, grids | `NativeStageHazards` (Ward kinds) | Could get kit alarms the same way | Optional placements |
| Cyber City maglev and grid | City kinds | none | None |

The kit adds no authority and changes no existing hazard: the managed checks pin that the stage hazards and their
records are untouched.

## 12. DEV preview

DEV panel · Previews (offline Developer Sandbox only):

- **Preview Hazard Kit** lays the 16 gallery pieces in a row on test bays, ahead of where you stand: the lockdown
  wall beacon, the strobe, the klaxon, three signs, the evacuation ceiling beacon, three turrets (floor, ceiling,
  wall) and the five saws. Each fires in turn on a 12 s cycle (0.45 s stagger), so every state shows and sounds.
  Saws take a scripted hit 1.4 s into each run (blood and the chop; nobody is hurt). Turrets loop their 16 s
  timeline.
- **Lock Hazard Kit to music** plays Black Siren Protocol and locks the beat pieces to it (section 7).

Hazard pulses from DEV panel · Hazards · Trigger also fire any placed piece listening for `dev-trigger`.

## 13. Checks

`CoreTests --hazard-kit` (in the full run too) covers:

- the placement file's schema and validation;
- Ward Zero and its instance's 30 alarms and their sources;
- deterministic timelines;
- flash caps: at most 3 Hz Full, 2 Hz Reduced, none in Calm, both free-running and beat-locked;
- beat-locked blasts and turns, the klaxon fit and the trip reaction;
- the 19 keys, their contract entries and fallbacks;
- the light budget;
- the turret and saw timelines, and the saw hit chop;
- the gallery;
- no per-frame allocation;
- existing stage hazards untouched.

`DevToolsChecks` covers the two gallery routes, and `EffectsAudioChecks` / `test_effects_audio.py` cover the key
count and the 13 MB budget.

## 14. Follow-ups

- **Photosensitivity outside the kit (left unchanged):**
  - Factory hazard lamps strobe at 6, 8 and 10 Hz (`NativeFactoryRig.WarnHz/ZoneHz/ChaseHz`);
  - Ward Zero shutter lamps blink at 5 Hz in Danger (`NativeWardZeroKit`);
  - both exceed 3 Hz. They could honour Settings · Alarm lights the same way (Claude, needs owner OK since they are
    existing looks).
- Expedition door sealing: call `NativeHazardKitWorld.Event(arena, "door-seal", ...)` from the closure view once
  Expedition binds an arena.
- Turret and kit saw authority (sections 9 and 10), then placements.
- Music: beat maps, sections or stems, and a separate music source (section 7).
