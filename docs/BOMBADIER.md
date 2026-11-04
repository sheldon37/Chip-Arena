# Bombadier: a DEV weapon

Owner request, 2026-11-04: "I'd like to add a new dev weapon and intro; Bombadier. An explosives maniac, supports 1-4 abilities. Very combo oriented (can chain one explosion and bounce players to
another one). Supports an Ultimate ability with a cutscene. Give unique animations, effects, and balance the damage 'somewhat' to give players a chance to fight against it. For the intro, make it cinematic; I want to
introduce a much higher detail of intros going forward. Very beautiful animations, very high detailed effects. Be creative, and make this like 'Oh shit, they're using the bombadier'."

Owner, the same day: "can we [have] them holding TNTs instead of a grenade launcher? Modify animations however needed with this in mind." So there is no launcher: a bundle of TNT in each fist, thrown by hand.

Status: presentation by Claude, reviewed in Unity; the intro plays today with DEV access. Authority, the DEV row/access, bots, snapshot/late-join/replay integration and native gates are implemented on `codex/bombadier-rules` (2026-11-04). Native Windows gameplay acceptance remains pending. DEV access only, like the Cyber Lord.

## The idea

Every other weapon hits where you aim. Bombadier plays pinball: you lay charges, and each blast throws its victim somewhere you have already planned for. The fun is the setup and the payoff, and the counterplay is that all of it is on the floor in plain sight.

## The kit

| Key | Ability (`id`) | What it does | Damage | Cooldown |
| --- | --- | --- | --- | --- |
| LMB | Pop Bomb (`pop-bomb`) | Throw a stick of TNT at 28 m/s with gravity, a hand at a time. It bounces once and goes off after .9 s, or on touching an enemy: 2.4 m blast, launches upward at 5 m/s. 1.5 a second, 6 on the belt, 1.7 s to restock from the satchel | 12 | |
| RMB, Shift | Block, Dodge | As any gun | | |
| Q | Blast Jump (`blast-jump`) | A charge at your own feet throws you 9 m up and along your look. No damage to you; 2.5 m blast round where you left | 8 | 8 s |
| E | Dead Man's Switch (`dead-mans-switch`) | Sets off every charge you have out, nearest you first, .12 s apart, so one explosion chases the next | as each charge | 2 s |
| 1 | Bounce Mine (`bounce-mine`) | Throw a mine up to 18 m; it sticks to floor or wall, arms in .5 s, and an enemy within 1.6 m or any blast touching it sets it off: launched straight up 8 m, no steering for .5 s. 3 out at most, 20 s life, 15 HP. 2 charges | 6 | 7 s a charge |
| 2 | Sticky Fuse (`sticky-fuse`) | A short stick, thrown hard, that sticks to the first enemy or surface and burns 1.2 s (a dodge roll shakes it off), then blows and **throws its victim toward your nearest live charge within 12 m** (the bounce). No charge near: straight up | 16 | 9 s |
| 3 | Cluster Lob (`cluster-lob`) | A slow heavy bundle, heaved two-handed, bursts in the air over the aimed point into 6 firecrackers, falling in a 5 m ring over .35 s. Each pops for 8, and launches twice as hard anyone already in the air | 8 each | 11 s |
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
- **A long restock,** and the belt is short.

## Grand Finale: the ultimate

The meter fills 8 per enemy hit, 6 per chain link, and 1 a second; full at 100. Key 5 (like the Cyber Lord's fifth key).

1. **Cut-in, 0 to .6 s.** The pilot stoops and sets a TNT crate with a plunger on the floor, both fists empty. Caster's screen: the live portrait, hazard bands, GRAND FINALE. Everyone else: a banner, and an air-raid siren starts.
2. **The plunge, .6 to 1.7 s.** The pilot crouches, rooted, both hands on the T-handle. Every enemy within 40 m gets a red circle (4 m radius) on the floor that follows them at 3 m/s.
3. **The lock, 2.1 s.** The pilot pulls the handle up and drives it home with both hands, the circles stop. Run.
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
| 2.6 | The flame runs to a pair of boots and dies under one of them. Silence | Low on the boots, then a slow tilt up: the belt of sticks, the bundle of TNT hanging from the right fist, the visor with red lights in it |
| 3.7 | The pilot holds up a detonator, tilts its head, and clicks it | Close on the thumb, then the visor |
| 4.2 | **One beat of nothing. Every red light goes out.** | Hold |
| 4.5 | The farthest pylon goes. The blasts come toward the camera one after another, each bigger. The fuse on the bundle in the pilot's fist is lit now | Pilot walking at the camera, a wall of fire rising behind it |
| 5.4 | The shockwave passes the pilot. Coat of embers, debris sailing by, it does not look back | Slow motion, low and wide |
| 6.0 | Hazard tape whips across, the pilot's name stamps onto it. The bundle comes up to the shoulder, spitting; a stick is tossed twice end over end and caught. The card's progress bar has been a burning fuse all along, and goes dark for the one beat of nothing | Settling to the hand-off |

## The chips

Owner: "a set of 10 bombadier chips? Make them highly likely to show in their chip selection pool; Give them a unique design and an effect when selecting them." A signature set, like the Emperor's or the Siren's: their own tier (`bombadier`, "LIVE ORDNANCE"), their own tag, and the signature sets' odds (`exclusiveOdds`: two of every pick guaranteed, the rest at 85%, until the set runs out). All in `Core/NativeBombChips.cs`.

| Chip | `id` | What it does | Max | Needs a rule? | When it goes in |
| --- | --- | --- | --- | --- | --- |
| Short Fuse | `bomb-short-fuse` | Pop Bombs go off .25 s sooner and fly 15% faster | 2 | Yes | A fuse burns round the whole card in under half a second, and the card goes up |
| Black Powder | `bomb-black-powder` | +15% blast damage | 3 | No: `damage` | Powder pours down the card and heaps under it, one spark runs along it, a black mushroom |
| Bandolier | `bomb-bandolier` | +2 sticks on the belt, 20% faster restock | 3 | No: `ammoCapacity`, `reloadSpeed` | Seven sticks slam in under the card one by one, then go off in order |
| Bouncing Betty | `bomb-bouncing-betty` | +1 mine out and +1 charge; mines throw 25% higher | 2 | Yes | A mine clanks onto the card, blinks three times, the card ducks, it springs and bursts |
| Daisy Chain | `bomb-daisy-chain` | Chain window +.4 s; links x1, x.9, x.75, x.6 | 1 | Yes | Blasts go round the card, each lighting the next along a burning line |
| Scorched Earth | `bomb-scorched-earth` | Blasts leave 2 s of floor fire, 6 a second | 1 | Yes | Fire climbs out of the floor and chars the bottom of the card |
| Nitro Boots | `bomb-nitro-boots` | Blast Jump 30% faster recharge, 20% farther, drops a firecracker | 2 | Yes | The card lights two jets, gets a foot off the ground, runs out, lands in its own smoke |
| Cluster Fun | `bomb-cluster-fun` | Cluster Lob bursts into 9, recharges 20% faster | 1 | Yes | The big one sails over the card, opens, nine firecrackers rain down in a stutter |
| Blast Shield | `bomb-blast-shield` | Your own blasts no longer hurt you (they still throw you) | 1 | Yes | The blast hits a dome of light round the card and goes round it |
| Encore | `bomb-encore` | Finale meter 25% faster; 8 more blasts with its fireworks, 10 each | 1 | Yes | Fireworks and confetti all round the card, its name in lights |

The look: a scorched body, torn hazard tape along the foot, a fuse burning round the card's edge, three sticks for a crest, its own emblem in the core and large behind the text, and a small live scene of its own (`Runtime/NativeBombChipArt.cs`, `Face`). Taking one plays its effect over the "INSTALLED" card with its own line (`Reveal`, `Shake`, `NativeBombChips.Confirm`), and a pop and flourish at the fist or chest as it seats, which everyone can see and which also covers the Expedition and PvE picks (`NativeTechniqueFx.BombChipInstall`). Reduced effects keep the card and one quiet glow.

To see them today: Dev Sandbox, with the Bombadier in hand, Preview Rarities, Bombadier. The rows are not in the content until the rules exist, because a row with a mechanic the authority lacks would fail the build's "every chip implemented" check, and an offered chip must do what it says.

## Presentation (Claude)

| Piece | File |
| --- | --- |
| The record, the names, the sandbox preview | `Core/NativeBombKit.cs` |
| The effects: charges, blasts by kind, the hand-on between links, the line, the circles, the columns, the fireworks, the Scorched smoulder; the Finale's camera | `Core/NativeBombFx.cs` |
| The TNT in the fists, sculpted in code; what is thrown, the belt and the charges, as pieces from one table | `Core/NativeWeaponModels.Bomb.cs`; `NativeTnt` in `Core/NativeBombKit.cs` |
| What each fist shows moment to moment, which hand throws, a stick's tumble, where the crate goes | `NativeBombs.HeldShare`, `ThrowsLeft`, `ChargeTurn`, `BoxSpot` in `Core/NativeBombKit.cs` |
| The poses: its own set (the bare-handed stance, the fists up by the chest), thrown with the arms only over the legs (`BombUpper`), whole-body for the heave, the stoop and the Finale (`BombCast`) | `Core/NativeVisualMotion.cs` |
| First person: a bundle low in each corner, thrown from there | `NativeBombView` in `Core/NativeBombFx.cs`, `NativeViewBase.Bomb` |
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
- **`Finale`**: `At`, `Lock`, `Strike`, `Until` (cut-in ends at `At`+.6), `Box` the crate's spot on the floor (`NativeBombs.BoxSpot(feet,dir)`, so it is under the hands), `Dir` the facing, `Circles` one per enemy with `Id`, `Pos` (current; fixed from `Lock`).
- **`Chain`** the current link count and **`ChainUntil`** when it lapses; **`Meter`** 0 to 1; **`Ready`** the absolute ready times in `NativeBombs.Slot` order (Q, E, 1, 2, 3, 4), with **`MineCharges`**.
- **`Cast`** the pilot's current action for the pose: `CastKind` (the ability id), `CastAt`, `CastRelease` (it leaves the hand; give a throw at least .15 s to wind up), `CastUntil` (about .4 s past the release, so the next stick is back in the fist). `CastAt` also picks the throwing hand (`NativeBombs.ThrowsLeft`), and each `Bombs` entry's `At` should be the release: the presentation brings it from that fist over its first .1 s.
- **`Scorched`** this Bombadier's finished victims: `Id`, `At`, `Until` (`NativeBombs.ScorchSeconds`, 2.5 s). On the kit rather than the victim, since it is this Bombadier who cannot throw them; the glow and the victim's HUD line read it.
- A victim's flight is the rules' own: launch them with `Knockback`/velocity and the usual air state.

The hold is keyed on `SourceSpec`, not the weapon row: whatever `WeaponId` the row uses, the fists close on the TNT, nothing is shouldered, first person has no sights (RMB is block anyway), and `NativeWeaponDressing.Gun` keeps the muzzle flash off. A restock shows from `ReloadAt` and `ReloadUntil`.

**The chips** (see "The chips" above): the tier, odds, tag, looks and sounds are in. Yours: implement the eight mechanics in `NativeBombChips.Mechanics` in the authority (the mechanic id is the chip id; `Has(p,id)` as other chips), add them to `NativeArena.SupportedMechanics`, call `NativeBombChips.Apply(content)` from the content load beside `NativeMovementContent.Apply`, gate `bomb-` with `Dev(p,NativeBombs.Spec)` beside the other prefixes in `ChoosePve`, and consider an `incompatibilities` row for the projectile chips that do nothing to bombs. Black Powder and Bandolier need only `damage`, `ammoCapacity` and `reloadSpeed`, which your authority already reads. Tune the numbers if they play badly.

Input: LMB `Shoot`, Q `AbilityQ`, E `AbilityE`, 1 to 5 `SirenAbility` (extend the key loop in `ConversionGame` to 5 for this spec, as for the Cyber Lord).

Needs from you: the DEV weapon row and access (`bombadier`, as the Cyber Lord), content rows, the authority, bots, the chain and Scorched rules, the numbers above (tune what plays badly and tell me), `NativeIntroRules.Duration`/`NameAt` already carry `dev-bombadier`, and native checks. Q on the keyboard: `NativeRangedRules.KeyboardQ` needs this spec beside the Cyber Lord's if the row's own Q does not qualify.

## Authority decisions (Astra, 2026-11-04)

- Damage and cooldown proposals are retained. Pop cadence is 2/3 s; ordinary throws release at +.16 s, the cluster at +.28 s, with .4 s recovery after release. Bomb `At` is the release; Finale uses `BoxSpot`.
- A first hit is link 0, then links 1/2/3 use .8/.6/.45. Four Sticky-strength hits total 45.6 HP before modifiers. The third link starts Scorched immediately, adds no launch, and clears this kit's mine steering lock. Scorched hits deal half damage and cannot relaunch. Histories are per owner/victim.
- **Pilot safety budget:** at most min(60 HP, 60% of maximum HP) actual damage from that owner's chain, including follow-ups during Scorched. The cap is applied after damage modifiers. It resets after Scorched expires (or after a 1.2 s gap without an active Scorched window). PvE minions retain normal lethal damage; their launch immunity window still applies. This prevents a damage-chip/overlapping-bomb burst from defeating the promised pilot counterplay.
- Overlapping Finale circles hit a target only once, follow at 3 m/s until lock, ignore cover at strike, and retain ordinary immunity/sanctuary rules. Up to 16 nearest enemies receive circles (the record's bound). Death does not cancel it, and PvP resolution waits for its pending strike. Caster alone is rooted; the existing presentation owns the camera.
- Jump/mine launch speeds use gravity 23 m/s² to reach their proposed 9/8 m rise. Sticky handoffs use 12 m/s horizontally toward the nearest live charge within 12 m. The line shoves at 7 m/s. Cluster children follow the preview's 3.6 m ring with 1.9 m blasts, staggered across .35 s. Ordinary explosions respect cover; the Finale is the exception.
- Mines have 15 shot HP, arm .5 s after sticking, expire 20 s after release, recharge serially at 7 s, and are capped at three (a fourth replaces the oldest). Any armed mine touched by a visible blast schedules a .12 s handoff. The switch orders charges by distance. All shots use nearest-hit arbitration with world, pilot and technique obstacles.
- The kit is deep-copied, validated in snapshots and death records, and uses explicit null-presence flags for Unity's real frame codec. `NativeBombs.Source` reads the rendered snapshot except while the sandbox preview owns it. Native gates exercise the real codec, cast timing, self damage, chains, charge destruction, dodge removal, the Finale and death persistence.
- Parting Line separately requires `Phase=="hop"` for movement. Descending floor contact does not terminate its horizontal retreat; a wall still can. Regression gates cover a complete hop and early/completed movement without the anchor-throw fallback.
