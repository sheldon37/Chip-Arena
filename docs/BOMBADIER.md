# Bombadier: a DEV weapon

Owner request, 2026-11-04: "I'd like to add a new dev weapon and intro; Bombadier. An explosives maniac, supports 1-4 abilities. Very combo oriented (can chain one explosion and bounce players to
another one). Supports an Ultimate ability with a cutscene. Give unique animations, effects, and balance the damage 'somewhat' to give players a chance to fight against it. For the intro, make it cinematic; I want to
introduce a much higher detail of intros going forward. Very beautiful animations, very high detailed effects. Be creative, and make this like 'Oh shit, they're using the bombadier'."

Status: presentation by Claude, reviewed in Unity; the intro plays today with DEV access. The kit has no rules yet: they, the weapon row and access are Astra's (AGENTS.md role split), and the Sandbox preview waits on that row. DEV access only, like the Cyber Lord.

## The idea

Every other weapon hits where you aim. Bombadier plays pinball: you lay charges, and each blast throws its victim somewhere you have already planned for. The fun is the setup and the payoff, and the counterplay is that all of it is on the floor in plain sight.

## The kit

| Key | Ability (`id`) | What it does | Damage | Cooldown |
| --- | --- | --- | --- | --- |
| LMB | Pop Bomb (`pop-bomb`) | Lob a bomb at 17 m/s with gravity. It bounces once and goes off after .9 s, or on touching an enemy: 2.4 m blast, launches upward at 5 m/s. 1.5 a second, 6 in the drum, 1.7 s reload | 12 | |
| RMB, Shift | Block, Dodge | As any gun | | |
| Q | Blast Jump (`blast-jump`) | A charge at your own feet throws you 9 m up and along your look. No damage to you; 2.5 m blast round where you left | 8 | 8 s |
| E | Dead Man's Switch (`dead-mans-switch`) | Sets off every charge you have out, nearest you first, .12 s apart, so one explosion chases the next | as each charge | 2 s |
| 1 | Bounce Mine (`bounce-mine`) | Throw a mine up to 18 m; it sticks to floor or wall, arms in .5 s, and an enemy within 1.6 m or any blast touching it sets it off: launched straight up 8 m, no steering for .5 s. 3 out at most, 20 s life, 15 HP. 2 charges | 6 | 7 s a charge |
| 2 | Sticky Fuse (`sticky-fuse`) | A fast dart that sticks to the first enemy or surface and burns 1.2 s (a dodge roll shakes it off), then blows and **throws its victim toward your nearest live charge within 12 m** (the bounce). No charge near: straight up | 16 | 9 s |
| 3 | Cluster Lob (`cluster-lob`) | A slow heavy bomb bursts in the air over the aimed point into 6 bomblets, falling in a 5 m ring over .35 s. Each pops for 8, and launches twice as hard anyone already in the air | 8 each | 11 s |
| 4 | Firecracker Line (`firecracker-line`) | A burning fuse runs 16 m along the floor at 14 m/s; 8 small blasts go off along it behind the flame, each shoving forward along the line. A wall stops it | 5 each | 12 s |
| 5 | **Grand Finale** (`grand-finale`) | The ultimate, below | 40 | meter |

### The chain

A blast that catches someone within 1.2 s of your last launch on them is a **link**. It is what the kit is for, and it is also where the damage is held back:

- Each link does less: ×1, ×.8, ×.6, ×.45.
- At the third link the victim is **Scorched** for 2.5 s: this Bombadier cannot launch them, and their blasts do ×.5 to them. The combo ends, and the victim gets a real window to answer.
- A full chain with everything landing comes to about 45 to 60 of 100 HP. Nothing in the kit kills from full on its own.

### Giving players a chance

- **90 HP, ×.95 speed, no direct fire.** Bombs arc and take time. Up close the Bombadier is in trouble.
- **Your own blasts hit you too:** ×.25 damage and the full launch (Blast Jump aside). A panic bomb at your feet throws you around.
- **Charges can be shot.** Mines have 15 HP, and shooting one sets it off where it is, which can throw the Bombadier instead.
- **Everything is telegraphed.** Mines blink, fuses burn and spit sparks, a stuck pilot sees the fuse on themselves and can roll it off.
- **A long reload,** and the drum is small.

## Grand Finale: the ultimate

The meter fills 8 per enemy hit, 6 per chain link, and 1 a second; full at 100. Key 5 (like the Cyber Lord's fifth key).

1. **Cut-in, 0 to .6 s.** The pilot slaps a detonator box on the floor. Caster's screen: the live portrait, hazard bands, GRAND FINALE. Everyone else: a banner, and an air-raid siren starts.
2. **The plunge, .6 to 1.7 s.** The pilot crouches, rooted, over the box. Every enemy within 40 m gets a red circle (4 m radius) on the floor that follows them at 3 m/s.
3. **The lock, 2.1 s.** The pilot brings the launcher down on the plunger, the circles stop. Run.
4. **The strike, 2.6 s.** A column of blasts goes through each circle, 40 damage and a big launch, ignoring cover. Out of the circle takes nothing.
5. **The fireworks, to 3.6 s.** Fireworks over the arena; the camera comes home.

Rooted and hittable all the way through; killing the Bombadier does not stop it (it is a dead man's switch). 40 is under half of anyone's health: it sets up a chain rather than ending a fight.

The cutscene is directed for the caster only, as Kernel Panic's is for its participants. Everyone else keeps their camera and control, because they need both to get out of the circle.

## The intro: Last Warning (`dev-bombadier`)

The entrance everyone recognises and dreads. 7.6 s, the name at 6 s. No music for the first half: a fuse, ticking, then nothing, and then everything.

| Time | Beat | Camera |
| --- | --- | --- |
| 0 | Black. A lit match falls out of it onto the rope, which catches | Macro, on the floor |
| .2 | The flame runs off along the floor spitting sparks | Low, racing with the flame |
| 1.3 | The flame passes charges taped to a pylon. One red light blinks on. Another. They come on round the whole arena, ticking faster | Pulls up and back, the arena dark, red lights spreading |
| 2.6 | The flame runs to a pair of boots and dies under one of them. Silence | Low on the boots, then a slow tilt up: the bandolier, the drum launcher, the visor with red lights in it |
| 3.7 | The pilot holds up a detonator, tilts its head, and clicks it | Close on the thumb, then the visor |
| 4.2 | **One beat of nothing. Every red light goes out.** | Hold |
| 4.5 | The farthest pylon goes. The blasts come toward the camera one after another, each bigger | Pilot walking at the camera, a wall of fire rising behind it |
| 5.4 | The shockwave passes the pilot. Coat of embers, debris sailing by, it does not look back | Slow motion, low and wide |
| 6.0 | Hazard tape whips across, the pilot's name stamps onto it. Bomb tossed up and caught. The card's progress bar has been a burning fuse all along, and goes dark for the one beat of nothing | Settling to the hand-off |

## Presentation (Claude)

| Piece | File |
| --- | --- |
| The record, the names, the sandbox preview | `Core/NativeBombKit.cs` |
| The effects: charges, blasts by kind, the hand-on between links, the line, the circles, the columns, the fireworks, the Scorched smoulder; the Finale's camera | `Core/NativeBombFx.cs` |
| The launcher, the Kickdrum, sculpted in code | `Core/NativeWeaponModels.Bomb.cs` |
| The poses | `Core/NativeVisualMotion.cs` (`BombCast`) |
| Sound, including the intro's whole timeline | `Core/NativeBombSounds.cs` |
| The intro; its hazard-tape name card and fuse-burning progress bar | `Core/NativeBombadierIntro.cs`; `Runtime/NativeBombScreen.cs` (`BombCard`) |
| Models and gear, HUD cards, chain counter, cut-in, warning | `Runtime/NativeBombPresentation.cs`, `Runtime/NativeBombScreen.cs` |
| Checks | `Tests/BombadierChecks.cs` (`CoreTests --bombadier`) |

To see it: the intro now: Customize, Entrance, Last Warning, Preview Intro, with DEV access. The kit: Dev Sandbox, Preview Bombadier, with the weapon in hand (once its row exists).

## Rules contract (Astra)

`Actor.Bomb` (`NativeBombKit`, `Core/NativeBombKit.cs`), copied in snapshots, late join and the killcam like `Actor.Cyber`, checked with `Valid()`. All times on the presentation clock. The presentation adds nothing to the wire; `NativeBombPreview` in the same file writes the record the way the host would, so it is the worked example.

- **`Bombs`** every live charge: `Id`, `Kind` (`NativeBombs.Pop`, `Mine`, `Sticky`, `Lob`, `Bomblet`), `Pos`/`Vel` each frame, `At` thrown, `ArmAt` armed, `BlowAt` when it will go if known (-100 if not), `Stuck` the id of who it is stuck to (0: none), `Normal` the surface. Removed when it goes.
- **`Blasts`** every blast of the last 1.5 s: `Id`, `At`, `Pos`, `Radius`, `Kind`, `Link` the chain link it made (0: none), `FromPos` the blast that handed on to it when `Link`>0 or it was set off by another.
- **`Line`** the Firecracker Line while it burns: `At`, `From`, `To`, `Speed`. Its blasts appear in `Blasts`.
- **`Finale`**: `At`, `Lock`, `Strike`, `Until` (cut-in ends at `At`+.6), `Box` the detonator's spot on the floor, `Dir` the facing, `Circles` one per enemy with `Id`, `Pos` (current; fixed from `Lock`).
- **`Chain`** the current link count and **`ChainUntil`** when it lapses; **`Meter`** 0 to 1; **`Ready`** the absolute ready times in `NativeBombs.Slot` order (Q, E, 1, 2, 3, 4), with **`MineCharges`**.
- **`Cast`** the pilot's current action for the pose: `CastKind` (the ability id), `CastAt`, `CastRelease`, `CastUntil`.
- **`Scorched`** this Bombadier's finished victims: `Id`, `At`, `Until` (`NativeBombs.ScorchSeconds`, 2.5 s). On the kit rather than the victim, since it is this Bombadier who cannot throw them; the glow and the victim's HUD line read it.
- A victim's flight is the rules' own: launch them with `Knockback`/velocity and the usual air state.

Input: LMB `Shoot`, Q `AbilityQ`, E `AbilityE`, 1 to 5 `SirenAbility` (extend the key loop in `ConversionGame` to 5 for this spec, as for the Cyber Lord).

Needs from you: the DEV weapon row and access (`bombadier`, as the Cyber Lord), content rows, the authority, bots, the chain and Scorched rules, the numbers above (tune what plays badly and tell me), `NativeIntroRules.Duration`/`NameAt` already carry `dev-bombadier`, and native checks. Q on the keyboard: `NativeRangedRules.KeyboardQ` needs this spec beside the Cyber Lord's if the row's own Q does not qualify.
