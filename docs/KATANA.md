# Katana: a melee class

The owner's request, 2026-11-10: "Make very high quality designed katana / dev dagger classes; Both with unique chips with their abilities that
play unique animations when selected. Use the new animation pack for both classes and use really cool effects. Use really great designs for the
weapons, and give them both a very awesome intro with effects". Earlier the same day: "Katana will be Public".

- Available to all players, including guests, in the melee category.
- Claude built the rules as well as the look, as for the Glaive and the Top Dog; the rules are logged for Astra.
- An original design. The moves are the Frank Slash Pack's katana set (the owner's Asset Store pack), retimed to the rules.

Status: on `claude/blades`. Not played by a person or between two real clients.

## The kit

| Key | Move (`id`) | What it does | Damage |
| --- | --- | --- | --- |
| LMB | Flowing Cuts (`katana-cut`) | Four cuts that chain if you keep attacking (0.45 s window). The third steps 1.2 m in. The fourth cleaves: it pushes and stuns longer | 18, 18, 22, 30 |
| LMB on a sprint | Rushing Draw (`katana-rush`) | A 5 m dash in 0.16 s, cutting everyone within 1.25 m of the path and throwing them aside | 22 |
| LMB after a block or parry | Moon Reflection (`katana-counter`) | Within 1 s of a block or parry (a hit held on the guard, a timed block or an F parry), the next cut is the counter: a paired critical. The foe is held in the matching clip through it (1.25 s); the blow lands 0.55 s in | 38 |
| Q | Moonlit Draw (`katana-iai`) | The blade is sheathed and gathered (0.32 s), then a 7.5 m dash along the aim (a wall stops it). Everyone within 1.3 m of the line is marked. 0.45 s after the dash the line splits: each marked one takes the cut and staggers. Cooldown 8 s, from the split | 46 |
| E | Petal Storm (`katana-storm`) | Six cuts all round (2.9 m), one every 0.13 s; the last throws them up. You move at 45% while it lasts. Cooldown 10 s | 8 a cut |
| RMB, F, Shift | Guard, parry, roll | As any melee weapon | |

Every number is in Core `NativeKatana`.

## The look

- **The blade** (Core `NativeWeaponModels.Katana.cs`, sculpted with WeaponSculpt): a curved single-edged blade with a lit hamon in the pilot's
  accent, a habaki, a pierced tsuba, a wrapped tsuka and a kashira. Its saya hangs at the left hip (Runtime `NativeBladePresentation.cs`); the
  blade rests in it through the iai's draw and from the click to the cast's end.
- **The motion** (Core `NativeBladeMotion`): the pack's first stance, its root-motion runs, its guard walk as the walk and its run as the sprint;
  its guard, block, tumble and hit reactions. The cuts are its first combo (Combo01 parts 1-3) and Combo03's last part. The rush is stance
  three's lunging cut, the iai stance three's dash skill and the sheathe (its click lands on the split), the storm stance three's flurry and the
  counter the pack's paired katana critical. Each move plays a clip window. The window's strike (the pack blade's fastest moment, measured
  in Blender) lands on the rules' hit, at most 1.6 times the clip's pace. A cut on the move plays on the upper body over the stride.
- **Effects** (Core `NativeBladeFx.cs`, in the pilot's weapon colour):
  - a crescent trail swept from the drawn blade through every cut, brighter on the finisher;
  - at each cut: a hairline slash, a flash and sparks;
  - the iai: its draw flash; the line of light hanging where it went, trembling brighter toward the split; a hairline on each one it marked;
    the split (the line flaring and breaking into motes and petals, a great slash across each one); the click's ring at the hip;
  - the storm: its whirl of petals and wind arcs, its launch streaks;
  - the counter: a moon rising behind the pair.
- **The icon**: `spec-katana` in `tools/export_class_icons.mjs`. The ability cards use the shared icons (moon, wind, eclipse, move-right).

## The intro: Crimson Moon (`katana-crimson-moon`)

7.2 s, the name at 5.4 s. The pack's clips play on the model (Core `NativeBladeIntros`, the arena's motion on the intro body).

1. Night, a crimson moon over the pilot's shoulder, petals drifting, a low haze. The pilot stands with the blade sheathed (the unarmed idle).
2. At 2.55 s, one great iai cut (stance two's skill, slowed at its height) leaves a line of light hanging across the stage.
3. The blade is slid home. At the click (4.35 s) the line splits, the moon flares and the petals burst.
4. The hero: standing calm, the blade back in its saya at the hip, the moon to one side.

Its music is the arc-salute bed. Presentation only.

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
`PairBy`, `PairClip`, `PairAt` and `PairUntil`, and its body plays the victim's clip. The record is replicated in snapshots, late join, death
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
| Saya, cards, sound | `Runtime/NativeBladePresentation.cs` |
| The chips | `Core/NativeKatanaChips.cs`; cards and picks `Runtime/NativeBladeChipArt.cs` |
| The intro | `Core/NativeBladeIntros.cs` |
| Checks | `Tests/BladeChecks.cs` (`CoreTests --blades`) |

## Seen in the Editor (2026-11-10)

- The four cuts, the finisher's glow, the storm's ring and petals, the iai (the gather, the dash, the line hanging, the split's slash across
  the marked one for 46) and the rush on the model; the saya at the left hip from the side and behind; Crimson Moon from start to card;
  the ten chips' picks.

## Not yet

- Not played by a person; not tried between two real clients.
- The counter's paired critical on screen: its rules pass in the Editor build validation (a block opens it, the foe held, the blow, the
  release), but no capture caught a bot's swing landing on the guard inside the window.
