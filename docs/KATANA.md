# Katana: a melee class

The owner's request, 2026-11-10: "Make very high quality designed katana / dev dagger classes; Both with unique chips with their abilities that
play unique animations when selected. Use the new animation pack for both classes and use really cool effects. Use really great designs for the
weapons, and give them both a very awesome intro with effects". Earlier the same day: "Katana will be Public".

- Available to all players, including guests, in the melee category.
- Claude built the rules as well as the look, as for the Glaive and the Top Dog; the rules are logged for Astra.
- An original design. The moves are the Frank Slash Pack's katana set (the owner's Asset Store pack), retimed to the rules.

Status: on `claude/blades` (merged into shared and the test channel with this release). Not played by a person or between two real clients.

## The kit

| Key | Move (`id`) | What it does | Damage |
| --- | --- | --- | --- |
| LMB | Flowing Cuts (`katana-cut`) | Four cuts that chain if you keep attacking (0.45 s window). The third steps 1.2 m in. The fourth cleaves: it pushes and stuns longer | 18, 18, 22, 30 |
| LMB on a sprint | Rushing Draw (`katana-rush`) | A 5 m dash in 0.16 s, cutting everyone within 1.25 m of the path and throwing them aside | 22 |
| LMB after a parry | Moon Reflection (`katana-counter`) | Within 1 s of a timed parry (a blow landing inside the F parry's window; a blow held on the raised guard opens nothing, owner 2026-11-11), the next cut is the counter: a paired critical; once the parry has landed the click counts already inside the parry's window (the riposte, the HUD's "RIPOSTE · LMB counter", the window then ends as a standard riposte's). The foe is held in the matching clip through it (1.25 s); the blow lands 0.55 s in and throws the foe 1.23 m back as its clip stumbles (walls stop it) | 38 |
| Q | Moonlit Draw (`katana-iai`) | The blade is sheathed and gathered (0.32 s), then a 7.5 m dash along the aim (a wall stops it). Everyone within 1.3 m of the line is marked. 0.45 s after the dash the line splits: each marked one takes the cut and staggers. Cooldown 8 s, from the split | 46 |
| E | Petal Storm (`katana-storm`) | Six cuts all round (2.9 m), one every 0.13 s; the last throws them up. You move at 45% while it lasts. Cooldown 10 s | 8 a cut |
| LMB in the air, looking down | Plunging slam | The plunge every melee weapon has (owner 2026-11-11: slam attacks from the air for the Glaive, the Fangs and the Katana): looking 45 degrees or more down after a moment airborne, a click drops you at 24 m/s and the landing cuts everyone within 1 m, then a 0.45 s recovery. Not while a move of the kit runs. Core `NativeAirAttackSimulation` by kin (the sword's) | 1.5 times the weapon's 18, up to 60% more from higher |
| RMB, F, Shift | Guard, parry, roll | As any melee weapon; a timed parry dazes its striker as long as a standard melee parry does (the riposte's stun, until the Katana's first blow; owner 2026-11-12) | |

Every number is in Core `NativeKatana`.

## The look

- **The kills** (Core `NativeBladeCuts`; owner 2026-11-12: "I also want all bladed weapons to support gibbing"; docs/BISECT-GIB.md "Blade
  classes"): the first two cuts take the head or an arm (the first, a level forehand at the chest, beheads; the second, a rising backhand that
  strikes low, takes a forearm); the third and fourth cuts, the rush, the iai (along its line), the storm, the counter and the Crescent Wave cut
  through the body; the plunging slam chops it top-down. The planes are the pack's own blade at each strike. The DEV Katana cuts the same ways on
  its own clips (surveyed at 200 fps by the DEV classes agent).
- **The blade** (Core `NativeWeaponModels.Katana.cs`, sculpted with WeaponSculpt): a curved single-edged blade with a lit hamon in the pilot's
  accent, a habaki, a pierced tsuba, a wrapped tsuka and a kashira. The blade rests in its saya from the sheathing's click to the iai's end.
- **The saya** (Core `NativeKatanaSaya`, drawn by Runtime `NativeBladePresentation.cs`; owner 2026-11-11: "sheathe is still not on body"):
  where the Frank Slash Pack's katana set wears it, on the model's pelvis bone (the pack's `Sword_Case` bone rides its pelvis): the hilt at the
  front of the belly on the left, the saya down and out along the left thigh, keeping about 5 cm off it through the idles and runs (it swings
  back along the hip in the runs as the pelvis turns). The pack's clips move it with the left hand and the model's left hand carries it as
  they do: the stance-three moves (the iai, the rush, the storm until its flurry) hold it pulled forward for the draw, the sheathing takes it
  up for the blade. The sheathing also turns the blade over in the right hand (about 165 degrees, early in the clip) so it goes in edge
  up; the blade sits in the saya at the iai's draw (the clip's 0.6 s) and the sheathing's click (1.867 s), so it leaves and enters without a
  jump. Measured on the pack's bones (Blender, on its FBX) and fitted on the model's own hands at those moments. The voxel body (no model)
  keeps its upright hip frame.
- **The motion** (Core `NativeBladeMotion`): the pack's first stance, its root-motion runs, its guard walk as the walk and its run as the sprint;
  its guard, block, tumble and hit reactions. The cuts are its first combo (Combo01 parts 1-3) and Combo03's last part. The rush is stance
  three's lunging cut, the iai stance three's dash skill and the sheathe (its click lands on the split), the storm stance three's flurry and the
  counter the pack's paired katana critical. Each move plays a clip window. The window's strike (the pack blade's fastest moment, measured
  in Blender) lands on the rules' hit, at most 1.6 times the clip's pace. A cut on the move plays on the upper body over the stride.
- **A parried attacker** (any weapon's, Core `NativeVisualMotion.Choose`; owner 2026-11-11: "Parry with katana make enemy walk backwards in
  place"): it recoils at its own pace (a Katana or Fangs attacker on its set's block, a jolt back with the blade up: their face-on hit
  threw the head back past the shoulders; any other on its hit from that side), then stays dazed on its feet (its set's stun) until the
  parry's shove has slid it out, 0.35 s past the stun. Before, the recoil was stretched over the stun, and as the shove slid it on past the stun, the stride's backward
  walk played while the body hardly moved. The counter's foe is held where the hold puts it and now travels back with its clip after the
  blow (Core `NativeBladeMotion.VictimTravel`, the victim clip's own 1.23 m): held still, it stepped back on the spot.
- **Effects** (Core `NativeBladeFx.cs`, in the pilot's weapon colour):
  - the weapon trail (owner 2026-11-11: "Give both weapon weapon trail vfx"): the blade sampled through every move, its sweep a smooth
    crescent narrowing to its tail (a sheet in the weapon colour, a white-hot edge, a faint halo, sparks shed off the fastest of it),
    brighter on the finisher; its strength follows the blade's speed in the hand; the rush, the iai's dash and the slam trail at a set
    strength. (Until then the trail scene found no blade for the kits: no trail drew.)
  - at each cut: a hairline slash, a flash and sparks;
  - the iai: its draw flash; the line of light hanging where it went, trembling brighter toward the split; a hairline on each one it marked;
    the split (the line flaring and breaking into motes and petals, a great slash across each one); the click's ring at the hip;
  - the storm: its whirl of petals and wind arcs, its launch streaks;
  - the counter: a moon rising behind the pair.
- **The icon**: `spec-katana` in `tools/export_class_icons.mjs`. The ability cards use the shared icons (moon, wind, eclipse, move-right).

## The intro: Crimson Moon (`katana-crimson-moon`)

7.2 s, the name at 5.4 s. The pack's clips play on the model (Core `NativeBladeIntros`, the arena's motion on the intro body).

1. Night, a crimson moon over the pilot's shoulder (a crisp disc with its halo, dark cloud drifting across it), petals tumbling down, a dark
   mist low over the floor. The pilot stands with the blade sheathed, the left hand resting on the saya (the pack's sheathed idle).
2. At 1.25 s the pilot sinks into the iai stance, the left hand pulling the saya forward (the pack's stance-three draw, the class's own
   Moonlit Draw); the blade leaves the saya at 1.75 s, and from 2.2 s it is drawn out slowly, filmed low on the pilot's left, the saya's
   side (owner 2026-11-11: "They don't pull sword out until animation is done essentially": the clip's draw-out takes a tenth of a second
   before the cut, and the wide shot stood ahead and to the left, where the iai stance turns the body's back, so the blade seemed to come
   out only as the cut ended; it now plays 3.5 times slower, in view). At 2.55 s its one great cut
   (slowed at its height), its sweep a crimson trail, a glint along the edge, leaves a line of light hanging across the stage; the petals it
   crossed hang cut in two on it.
3. From 3.3 s the whole sheathing: the blade turned over in the hand, the saya taken up by the left hand, the blade slid home. At the click
   (4.35 s) a flash at the saya's mouth and a shockwave over the floor: the line flares and shatters into falling shards, the cut petals
   fall apart, the moon flares.
4. The hero: standing calm, the blade back in its saya at the hip, the moon to one side.

Its music is the arc-salute bed. Presentation only.

Its look (owner 2026-11-11: "Floating parts, needs much improved vfx"): set pieces where a shape must read crisp (the moon, the petals, the
line and its shards), lit smoke for the mist, no glow rings or balls; the trail and the glint from the blade where the model holds it
(Runtime `NativeBladePresentation.BladeIntroHeld`). The model holds the blade as the match does (owner 2026-11-11: "Katana intro is broken,
they dont hold it"): the entrance's keyed stance holds no sword the way the game's pose does, so the hold the intro body took on its first frame
hung the blade from the bare wrist along the forearm; the blade classes' entrances take the match's hold (Core `NativeBladeGrip`, measured on
the arena's body) and the palm grip the match draws with.

## The chips

Ten chips of its own (tier `katana`, "KATANA SPECIAL", crimson `#ff3d6e`, a lacquer cartridge with the knot crest, the tsuka's wrap; the crescent is the Glaive's alone). They are offered only
to the Katana and are likely in its drafts (two guaranteed, 85%). Rules: Runtime `NativeKatanaAuthority.cs`; numbers and copy: Core
`NativeKatanaChips`.

| Chip (`id`) | Copies | What it does |
| --- | --- | --- |
| Honed Edge (`katana-honed-edge`) | 2 | +10% Katana damage a copy; every cut bleeds 3 a second for 2 s |
| Crescent Wave (`katana-crescent-wave`) | 1 | The fourth cut sends a crescent of light 8 m ahead, cutting everyone it passes for 16 |
| Thousand Petals (`katana-thousand-petals`) | 1 | The storm cuts 4 more times, and every cut draws enemies 1 m in |
| Twin Moons (`katana-twin-moons`) | 1 | The iai's line splits twice: a second split 0.3 s later for 60% |
| Severing Line (`katana-severing-line`) | 1 | The split kills anyone it leaves below 20% health; a kill takes 4 s off Q |
| Flash Step (`katana-flash-step`) | 1 | The rush dashes 60% farther, and you are untouchable while it dashes |
| Still Water (`katana-still-water`) | 1 | After 1.2 s without attacking, the next cut deals +50% |
| Mirror Moon (`katana-mirror-moon`) | 1 | The counter's window lasts 2 s; the counter deals +50% and heals 15 |
| Falling Blossom (`katana-falling-blossom`) | 1 | The storm's launch is higher; your next cut on a launched enemy within 2 s deals +60% |
| Sheathed Fury (`katana-sheathed-fury`) | 1 | Q's cooldown is 30% shorter and its draw quicker (0.2 s) |

The card and the pick are in Runtime `NativeBladeChipArt` (each pick its own animation over the INSTALLED card, its sounds in `Picks`).

## Sound

Synthesized in C# (Core `NativeToonSynth`, ids `blade-*`): the swish of a cut, the slice, the draw's ring out of the saya, the sheathe and its
click, the split, the dash, a gust, the ringing edge, a chime, the whetstone, the crescent wave, a drop and a clash.

## The record

`Actor.Blade` (Core `NativeBladeKit`, shared with the Fangs): the move, the combo, the ready times, a move's travel, the iai's line and its
split, the counter's window, a paired move's victim and clip, the chips held and the last 1.5 s of hits. A held victim's actor carries
`PairBy`, `PairClip`, `PairAt` and `PairUntil`, and its body plays the victim's clip whatever its class (owner 2026-11-13: "Give one to
everyone": a set that does not list the clip borrows it from the motion bank when the host first names it, Core `NativeBladeMotion.Lent`,
Runtime `VisualMotionLend`; the Cyber Lord, with no set, plays it on `NativeVisualMotion.Held`). The record is replicated in snapshots, late join, death
records and the killcam, and checked with `Valid()`.

## Files

| Piece | File |
| --- | --- |
| Names, numbers | `Core/NativeKatana.cs` |
| The shared record, helpers | `Core/NativeBlades.cs` |
| The rules (host) | `Runtime/NativeKatanaAuthority.cs`, `Runtime/NativeBladeAuthority.cs` (shared), wired in `NativeArena.cs` |
| Rules validation (build checks) | `Runtime/NativeBladeValidation.cs` |
| The moves on the pack's clips | `Core/NativeBladeMotion.cs`, the set in `Core/NativeVisualMotion.cs` |
| The model | `Core/NativeWeaponModels.Katana.cs` (registered in `.Blades.cs`) |
| Effects | `Core/NativeBladeFx.cs` |
| Saya, cards, sound | `Runtime/NativeBladePresentation.cs`; where the pack wears and moves the saya `Core/NativeKatanaSaya.cs` |
| The chips | `Core/NativeKatanaChips.cs`; cards and picks `Runtime/NativeBladeChipArt.cs` |
| The intro | `Core/NativeBladeIntros.cs` |
| Checks | `Tests/BladeChecks.cs` (`CoreTests --blades`) |

## Seen in the Editor (2026-11-11, the owner's third notes)

- Crimson Moon's draw from the pilot's left: the hilt brought forward, the blade drawn slowly out of the saya with its crimson trail and a
  glint at the mouth, then the cut uncoiling toward the camera.
- A parried sword bot and a parried Katana bot: the recoil (the Katana's on its block, the head upright), the daze while the shove slides
  it, then its stance; never the walk.
- The counter on a sword bot's blow on the F parry, then the click: the foe held 1.6 m away until the blow, then thrown back with its clip
  to 2.82 m.
- The counter on the Hot Dog skin (the owner's pilot): its sausage folds at mid-height through the counter (its top kinks over), the
  spine curled about 50 degrees past the stance; the skin's own fix is with its session (DEV_LOG).

## Seen in the Editor (2026-11-10)

- The four cuts, the finisher's glow, the storm's ring and petals, the iai (the gather, the dash, the line hanging, the split's slash across
  the marked one for 46) and the rush on the model; the saya at the left hip from the side and behind; Crimson Moon from start to card;
  the ten chips' picks.

## Not yet

- Not played by a person; not tried between two real clients.
- The counter in a real exchange between people (captures stage the blow on the parry: the tool's `parryhit`, a local option).
