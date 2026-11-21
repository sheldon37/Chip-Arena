# Zombies emerge from the floor (the drop-in removed)

Owner (2026-11-21): "Please assign an agent to rework zombie spawn animation as well. Currently theyre using old drop in effect
or just spawning in. Can we animate them digging out of the ground? Or have them laying there, and just rising? The hit indicator
from the old drop in effect is massive and inaccurate, so I want to remove that drop in animation entirely."

Branch `claude/zombie-emerge` (Z1). Code: `Core/NativeHordeEmerge.cs` (rules, clock, pose, shapes),
`Runtime/NativeZombieEmergeDraw.cs` (the ground), `Runtime/NativeHordeZombiePresentation.cs` (the job, the style, the shake),
`Runtime/NativeHordeKindPresentation.cs` (the pose hook and the cues), `Runtime/NativePveHorde.cs` (the authority's hold).
Checks: `Tests/EmergeChecks.cs` (`--zombie-emerge`) and the Editor gate `NativeArena.ValidateHordeDrops`.

## The spawn paths (mapped on `89be9868`)

| Path | Entry | Before | Now |
|---|---|---|---|
| Horde adds through a map entry of kind `drop` (Ward Zero's `drop-obs-n` and the Hulk's, `exp-city`'s 20, Meridian's roof hatches, which allow every kind) | `drop` | The jump-down: the ink ring closing on the spot, the ink tear, the leap and fall, the touchdown slam (brute path: 10 to 5 damage within 2.4 m, 380 shove, other adds shoved; every other kind: 12 to 5, or the Hulk's 25 to 10 within 5.5 m with 900 impulse) | Emerges on the same clock; no slam |
| Horde adds through a ceiling vent (Ward Zero, the foundry) | `vent`, `surface` `ceiling` | The same jump-down (`NativeWardEntries.Drop`) | Emerges (inside: most lie and rise) |
| A door or cell with no opening (`from` = `exit`): Meridian's street spawns and hub mobs, `exp-*` none | `door`/`cell` | Popped in standing, twitched awake, walked in place; exposed at `WalkAt` (.6 s, or at once if queued), active at 1.8 s (1.2 s if queued) | Emerges in 1.8 s, the torso out at 1.0 s, queued or not |
| Doors and cells with an opening, floor and wall vents, breaches | | Walk in, climb out, break through | Unchanged |
| Boss summons (`SpawnPveAddAt`: the slag pools, the shed slagbacks, the station's spill, the Backrooms bosses) | `boss` | Appear at once, active at once (scripted by the bosses) | Unchanged (they would need a rules delay: for Astra) |
| The Rigger's hang, the Backrooms entities (Faceling, Smiler, Drowned, Lifeguard, Hound, Tiler) | | Their own entrances | Unchanged (`OwnEntrance`) |
| DEV monsters | synthetic `door` | Active at once | Unchanged (a guest cannot resolve their synthetic entry) |

## The rules (minimal; no wire change)

- **The clock is the entry's own.** A drop entry keeps `NativeHordeDrop.Duration` (SpawnAt to ActiveAt, the old warning,
  leap, fall and recovery: 2.71 s from 6.5 m for every kind but the Hulk, 4.36-4.49 s for the Hulk) and `LandAt` (ActiveAt minus
  .9 s or the Hulk's 1.5 s) is now when the torso is out. A pop-in door is `PopSpan` 1.8 s with the torso out at `PopTorso` 1.0 s
  (`NativeWardEntries.Timeline`). Guests derive ExposedAt from the same replicated `spawnAt`, `activeAt`, `first` and entry.
- **Under the floor, then rising.** `WardEntryPose` holds every entering add kinematic. An emerging add's capsule centre is a
  whole height under its standing point (collider off) until ExposedAt, then rises on `NativeHordeEmerge.Sink` (its top over the
  drawn head, keyed to the pack's crawl-to-stand) to stand at ActiveAt, when it turns physical as before.
- **Targetable once the torso is out.** Before ExposedAt it is no target and no enemy (`EmergeHeld` in `CanHit`/`Enemy`), and
  takes no damage (`HitPveAdd` already gated ExposedAt). From ExposedAt it can be shot, still unable to move or attack until
  ActiveAt. Lock-on and the melee abilities open at ExposedAt for every emerging entry.
- **No slam.** `PveDropSlam`, `WardLandingSlam`, `StepPveDrop` and the `minion-drop` event are gone, with
  `NativeHordeDropRules.Touch`/`Damage`/`DamageCentre`/`DamageRim`/`Impulse`/`AddImpulse`. The smash's shove stays.
- **Rule changes for Astra** (DEV_LOG has the list): the landing damage, knockback and add shove removed (the owner's word); a
  dropping brute is now held (no DoTs ticking, no shove) until ActiveAt like every other entering add; pop-in doors expose at 1.0 s
  (was .6 s, or 0 s queued) and a queued pop-in takes 1.8 s (was 1.2 s); lock-on and melee targeting open at ExposedAt for
  pop-in doors (was ActiveAt).

## The presentation

- **Styles** (`NativeHordeEmerge.Choose`, from facts every peer shares: the map, the entry, the bleed's theme at the spot, the
  add's seed): open ground (the city) digs out 84%; inside, under a ceiling vent and in the bleed 82% lie and rise; the brute and
  the Hulk always dig out.
- **Dig out:** through the tell (a third of the way to the torso out, .25-1 s) cracks creep out and the floor's plates heave;
  at the break the plates are thrown aside, clods and toon dust burst out as a hand claws through the floor, then the other;
  the head and shoulders push up (more clods and dust), the hands slam flat by the hole and the body hauls itself out to the
  waist and up onto a knee by the torso out; then the pack's crawl-to-stand rises it to stand at ActiveAt. Grit trickles off it.
  The hole and its earth rim stay a few seconds; the brute's and the Hulk's (bigger, heavier rubble, a shake at the break and as
  it hauls) stay 24 and 40 s as a scar, then fade.
- **Lie and rise:** a dark wet stain seeps out through the tell and the body surfaces in it lying face down (the pack's forward
  death at rest), its arms flopped by its head; it stirs (the head lifts, a hand drags), plants its hands and pushes up onto its
  knees, then the crawl-to-stand. The stain dries and fades 7 s after.
- **Honest tell:** the ground cracking a beat before is the only warning; no ring, no disc, no tear. The cracks reach .95 m
  (light), 1.45 m (brute), 2.6 m (Hulk), inside the old slam discs.
- **Sound** (existing bank keys and voices): `structure.crack` as the tell starts (the Hulk's over `structure.rumble`),
  `structure.debris` at the break and the haul (the heavy kinds' with `structure.thud`, the Hulk's with `structure.boom`), a
  `Groan` call as it rises (the Hulk's `structure.groan`).
- **Tiers:** every stream follows `NativeZombieFx.Tier` (Reduced effects and the low tier halve them; the grit and the stain's
  sheen are the full tier's); the shake follows Settings, camera shake (halved by Reduced effects).
- **Host, guests and the killcam** draw from the replicated clocks; a rewind clears the records and replays them.
- **Galleries:** the Dev Sandbox horde gallery's brute digs out and its spitter lies and rises; the Hulk gallery's Hulk digs out.
- **Removed:** the ink ring, the ink tear, the jump-down's leap and fall for entering adds, the touchdown slam's impact, shake and
  prop jolt, the Hulk's warning rumble, `NativeHordeFx.DropMark`/`LandWave`/`LandDebris`, `NativeHordeDrop.Rumble`, and Ward
  Zero's red warning shaft and grit down from a drop hatch. `NativeHordeGait.Drop` and `NativeZombieFx.Landing` stay for the
  Ironworks Rigger's chain drop.

## Animation impact

- **Hit timing, damage, cooldowns:** no attack before ActiveAt (unchanged); the slam's damage is gone.
- **Hitboxes:** none under the floor; from ExposedAt the capsule's top follows the drawn head (within .09 m over it in
  `EmergeChecks`; the capsule's radius is unchanged, a kneeling body leans up to .43 m ahead of it).
- **Movement:** none while emerging (unchanged); the brute's recovery shove is gone with its separate path.
- **Networking:** no new field; ExposedAt and the timeline are derived on guests from the existing ones; the body's replicated
  position is now under the floor and rising during the emergence (it used to hang in the air for drops).
