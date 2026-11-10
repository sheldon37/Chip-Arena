# Glaive: a melee class

The owner's request, 2026-11-09: "Begin creating a new melee class; Glaive (Reference warframe glaives) It's throwable, slices like a sword,
etc. Lets make the combos flow nicely and the animation very nice; with great quality effects."

Defaults chosen for this first version (say if any should change):

- Available to all players, including guests, in the melee category (owner request, 2026-11-09).
- Claude built the rules as well as the look, as for the Top Dog; the rules are logged for Astra.
- An original design built on the mechanic (a ring blade you cut with, throw to ricochet and come back, and burst). No other game's names,
  weapons or art.

Status: on `claude/glaive`. Not played by a person or between two real clients.

## The kit

| Key | Move (`id`) | What it does | Damage |
| --- | --- | --- | --- |
| LMB | Ring Cuts (`glaive-cut`) | Four cuts that chain if you keep attacking (0.45 s window): a flat forehand from the right to the left, a rising backhand from the left to the right, a **whirl** (the whole body turns once, the ring at arm's length: everyone within 2.7 m all round), then a backhand **flick** that throws the ring out on a boomerang's loop (5.5 m ahead, out on the right and back on the left) and catches it again, cutting everyone within 0.8 m of it once each way. Cuts stagger; the whirl pushes out | 16, 16, 22, 18 |
| LMB on a sprint | Skimming Cut (`glaive-skim`) | A lunge 4.5 m along the facing in 0.2 s, the ring swept low across: everyone within 1.2 m of the path cut and thrown aside | 20 |
| RMB, F, Shift | Guard, parry, roll | As any melee weapon | |
| Q | Ricochet Throw (`glaive-throw`) | The ring flies along the aim at 30 m/s (24 m at most). The first enemy it reaches is cut and it **ricochets** to the nearest other enemy in sight within 9 m, up to 3 times (each 85% as hard). Then it comes back to the hand at 34 m/s through anything, cutting whoever it passes once. A wall turns it back with a spark. Q's cooldown (6 s) starts at the catch | 26 (12 on the way back) |
| Q or LMB while it is out | Burst (`glaive-burst`) | The ring bursts where it is: everyone within 3.4 m hurt and thrown out (7 m/s, 4 up). It re-forms in the hand 0.45 s later | 30 |
| E | Halo (`glaive-halo`) | The ring leaves the hand and circles you 1.7 m out, 2.2 turns a second for 4 s, cutting each enemy it passes (each at most every 0.35 s). LMB bursts it. Cooldown 10 s | 10 a pass |

While the ring is out the hand is empty: the left click bursts it instead of cutting. Every number is in Core `NativeGlaive`.

## The look

- **The ring** (Core `NativeWeaponModels.Glaive.cs`), redesigned at the owner's word (2026-11-09: "make the glaive a better design"): the
  tri-crescent. A heavy stepped ring of blade round the fist (a dark hub, a raised steel band, a bevelled edge lit all round in the
  pilot's accent, a lit inner rim, inlaid arcs on both faces), three broad crescent fangs sweeping off its rim with the spin, each with a
  lit cutting edge and all pitched the same way out of the plane like a turbine's blades (so even edge on it reads as a slanted fan,
  not a line), small notched teeth between them, and a wrapped grip across its near side with lit studs. Thrown, it is drawn as the
  same model, flat, tipped into its flight and spinning fast (Runtime `NativeGlaivePresentation.cs`).
- **The moves are keyed by hand** (Core `NativeGlaiveMotion`), the way the Top Dog's mallet is (Core `NativeDogMallet`, the same solver on
  the model): one-handed, the left arm the stance's own, the ring's edge turned along its own travel so it leads every cut. Reworked at
  the owner's word (2026-11-09: "improve the animations significantly") to use the whole body:
  - the cuts run at chest and shoulder height: each coils deep (the chest wound about 70 degrees round, the knees sunk, a held beat),
    whips through at full reach and follows round until the ring wraps the other side, ending where the next begins;
  - the backhand rises from low on the left onto the toes, the ring ending above the right shoulder;
  - the whirl is a leap: a deep crouch with the ring swung out wide, the legs tucked under a full turn in the air, landed in a crouch;
  - the flick winds across to the left hip and whips out, the open hand held for the catch, which yanks the arm back;
  - the skim is a low dive with the ring swept across low; the throw a frisbee backhand wound right across the chest; the halo flung
    up from overhead on a stretch; the burst a punch at the ring.
  Under it all: the one-handed sword's stance, stride, jumps, guard and reactions. The cut's crescent trail reaches the fangs' tips.
- **Effects** (Core `NativeGlaiveFx.cs`, in the pilot's weapon colour): a crescent of light along the ring's outer edge through every fast
  part of a cut, with a spark at the edge; the thrown ring's spinning glow, its wake back along its path and the motes it sheds; a slash
  line, a flash and sparks at every cut; a ping ring and sparks at a throw's cut and each ricochet; the burst (a white flash, a shockwave
  over the floor and a second ring in the tint, shards flung out, sparks and dust); light drawn into the hand as the ring re-forms; a flash
  at the catch.
- **The class icon** is a ring blade with its three hooks and the grip across its middle, two motion arcs trailing it (`spec-glaive` in
  `tools/export_class_icons.mjs`, with a small drawing). The ability cards use the shared icons (swords, rotate, orbit, sparkles).

## The intro: Crescent Return (`glaive-crescent`)

Owner, 2026-11-09: "create a very well animated intro". The Glaive's own entrance (Core `NativeGlaiveIntro`, open to every player:
the class entrance a Glaive pilot plays unless they pick another, and pickable in the Locker). 7 s, the name at 5.2 s:

- **The ricochets (0.35 s):** the stage is dark. The thrown ring streaks in from the left, a spinning light with a wake, and ricochets off
  two pylons (0.8 s, 1.25 s) in a ping and a shower of sparks, each pylon lighting as it is struck.
- **The catch (1.7 s, slow motion):** on its way back the pilot drops in from above, reaching up, and catches it high in the air in a
  flash and a ring of light; turns a whirl as it falls (legs tucked) and lands in a deep crouch (2.15 s) in dust and a floor ring.
- **The orbit (2.65 s):** rising onto the toes it flings the ring up; it comes down to circle the pilot twice, 1.7 m out, carving a
  circle of light into the floor while the stage powers up; the free hand follows it round.
- **The cut (4.05 s):** caught back with a recoil, one big forehand: a crescent of light hangs where it went across, the floor circle
  flashes up into a wall of motes, and the crescent breaks into motes drifting up.
- **The hero (4.65 s):** standing tall, the ring held upright beside the face, its edge lit, motes rising round it; the name at 5.2 s.

The body is keyed in the script; while the ring is in the hand its arm and the chest are the Glaive's own keyed solver (Core
`NativeGlaiveMotion.Intro`, put on the model by Runtime `NativeDogMalletPresentation.GlaiveIntro`), and while it is out the runtime
draws the held model where `NativeGlaiveIntro.Ring` says, spinning (Runtime `NativeGlaivePresentation.GlaiveIntroRing`). Its music is
the 150 bpm signal-spin bed. Presentation only.

## The chips

The owner's request, 2026-11-09: "create uniquely designed chips to benefit the glaive with effects on selection".

Ten chips of its own, a signature set like the Top Dog's: their own tier (`glaive`, "GLAIVE SPECIAL", cyan `#4fe8ff`, a tri-crescent
crest on the 3D chip), offered only to the Glaive and likely in its drafts (two guaranteed, 85%: the content's `exclusiveOdds`); the
ordinary projectiles' chips are not offered to it (its ring is its own). Rules, numbers and copy: Core `NativeGlaiveChips`; the rules
Runtime `NativeGlaiveAuthority.cs`, host-side (Claude builds this class's rules at the owner's word; logged for Astra).

| Chip (`id`) | Copies | What it does |
| --- | --- | --- |
| Fourth Bounce (`glaive-fourth-bounce`) | 1 | Ricochet Throw ricochets 2 more times (5 in all) and looks 4 m farther (13 m) for the next target; each ricochet keeps 90% of the hit instead of 85% |
| Keen Edge (`glaive-keen-edge`) | 2 | +10% Glaive damage a copy (cuts, skim, flick, throw, return, burst, halo, shards); every hit but the burst makes them bleed 3 a second for 2 s, refreshed by the next (the arena's damage over time, `bleed`) |
| Twin Orbit (`glaive-twin-orbit`) | 1 | Halo sends a second ring of light circling the other way, starting opposite, 1.2 m farther out (2.9 m), cutting on its own clock (10 a pass, each at most every 0.35 s) |
| Shatterpoint (`glaive-shatterpoint`) | 1 | The Burst is 40% wider (4.76 m) and throws out 6 shards, evenly spread, that fly 5 m at 25 m/s (a wall stops one), each cutting whoever it passes once for 8 |
| Magnet Return (`glaive-magnet-return`) | 1 | Each enemy the returning ring cuts is dragged 3 m toward you (never closer than 1.2 m; not the Siren's armour or a boss) |
| Quick Catch (`glaive-quick-catch`) | 1 | Catching the thrown ring, or the halo's when it comes back, takes 2.5 s off Q's cooldown and gives +25% move speed for 1.5 s (the combo's flick does not count) |
| Whirlwind (`glaive-whirlwind`) | 1 | The combo's whirl drags everyone within 3.5 m 1.5 m in as it winds up, then reaches 3.5 m (from 2.7) for +20% damage and holds them in instead of pushing them out |
| Long Arc (`glaive-long-arc`) | 1 | The flick's boomerang loop goes 60% farther (8.8 m) and 40% wider, in the same time; its cuts deal +25% |
| Skimming Stone (`glaive-skimming-stone`) | 1 | The skim lunges 50% farther (6.75 m) and lays a slick of light along its path that lasts 2 s after it: enemies on it (1.1 m either side) are 40% slower while they stay |
| Crescent Guard (`glaive-crescent-guard`) | 1 | While the ring is out of your hand (thrown, coming back, circling or flicked) you take 25% less damage; the halo's ring (and Twin Orbit's) cuts down the enemy shots it passes: bullets, ability shots, acid |

Every number is in `NativeGlaiveChips`. The record carries what the look needs (`Keen`, `Twin`, `Shatter`, `LongArc`, `Guard`,
`QuickUntil`, the slick's `SlickAt`, `SlickFrom`, `SlickTo`; `Bounces` up to 5) and four new hits (`shard`, `pull`, `guard`, `bleed`).
In the world (Core `NativeGlaiveFx.cs`, in the pilot's weapon colour): Twin Orbit's ring of light with its own glow and wake, the
shards spinning out of the burst on the same ways the rules fly them, the slick shimmering and rippling on the floor, Quick Catch's
rush of speed lines, Crescent Guard's three faint crescents turning round you while the ring is out and a flash and crescent where a
shot is cut down, light streaming toward you from whoever is dragged, Keen Edge's dark red drips, a brighter crescent trail with each
copy of Keen Edge. Two new synthesized sounds: a glassy chime (`ring-chime`) and a stone's skip off water (`ring-skip`).

**The card** (Runtime `NativeGlaiveChipArt.Face`): faint orbits round the emblem, tipped like the halo seen from the side, a crescent
sweeping along the top, a glint circling the card's edge like the halo with its trail, each chip's emblem large behind the text and its
own small loop.

**The pick** (`NativeGlaiveChipArt.Reveal` and `Pose`, over the INSTALLED card; its cues in `NativeGlaiveChips.Picks`): each its own,
the card knocked, turned and pulsed in time:

- Fourth Bounce: the ring flies in and ricochets round the card's four corners, a ping, a ring of light, sparks and the count (×1 to ×4)
  at each, then comes home into the emblem in a flash.
- Keen Edge: the edge's glint sweeps along the diagonal, a hair-thin cut splits the face, light spills out of it, and it seals end to end
  in a run of shimmer.
- Twin Orbit: two rings come round the card from its foot in opposite directions, each laying a circle of light; they meet at the top in
  a flash.
- Shatterpoint: the emblem glows in the middle, cracks spread out of it, it bursts in a flash and a shockwave and its shards spin away.
- Magnet Return: field lines curl in from the edges and motes are drawn faster and faster to the spinning ring; a ring of light closes on
  it and it snaps them in.
- Quick Catch: the ring spins up on the emblem, flies off on a boomerang loop past the card's edge and back into the emblem with a flash;
  speed lines whip off the card.
- Whirlwind: a vortex of light, arms and motes spiralling in tighter and faster, collapses into the ring's whirl, which cuts all round.
- Long Arc: the ring takes the long way round, tracing a long boomerang loop across and beyond the card with a trail of light that fades
  from its start once the loop closes.
- Skimming Stone: water across the card's foot; the ring skims in flat and skips three times, ripples and splashes at each touch, a slick
  of light glowing behind it.
- Crescent Guard: a great crescent sweeps once round the card, a dome of hexes lights up cell by cell from the foot, and a red shot
  strikes it and is cut down in sparks.

Reduced effects keeps one soft glow. The draft holds 5.2 s on one of these (`NativeGlaiveChips.RevealSeconds`).

Checks: `Tests/GlaiveChipChecks.cs` (`CoreTests --glaive-chips`); each rule against the real arena in `NativeGlaiveValidation.cs` (the
Editor's build checks).

## Sound

Eight cues synthesized in C# (Core `NativeToonSynth`, ids `ring-*`): a thin blade's swish for each cut, a slice on each hit, a spun throw, a
ricochet's ping, the halo's hum while it circles, the burst, the catch and the flick. As for the Top Dog's, the effects bank is asked for
`topdog.ring-*` first, so recorded versions can replace them.

## The record (`Actor.Glaive`, Core `NativeGlaiveKit`)

Replicated in snapshots, late join, death records and the killcam, checked with `Valid()`: the move (`CastKind`, `CastAt`, `CastRelease`,
`CastUntil`, `CastStep`), the combo, Q's and E's ready times, the ring (`Disc`: in the hand, out, coming back, circling, burst, flicked;
since when; its sampled flight; the flick's and the halo's start), its ricochets, `Reformed`, and the last 1.5 s of hits for the effects
and the sound. Guests mirror it onto their own pilot (`p.Glaive`) for the cards.

## Files

| Piece | File |
| --- | --- |
| Names, numbers, the disc's paths, the record | `Core/NativeGlaive.cs` |
| The rules (host) | `Runtime/NativeGlaiveAuthority.cs`, wired in `NativeArena.cs` |
| Rules validation (build checks) | `Runtime/NativeGlaiveValidation.cs` |
| The keyed moves | `Core/NativeGlaiveMotion.cs` (on the model: `Runtime/NativeDogMalletPresentation.cs`) |
| The model | `Core/NativeWeaponModels.Glaive.cs` |
| Effects | `Core/NativeGlaiveFx.cs` |
| The thrown ring, cards, sound | `Runtime/NativeGlaivePresentation.cs` |
| Sound | `Core/NativeToonSynth.cs` (`ring-*`) |
| The chips: names, numbers, emblems, shared paths, pick cues | `Core/NativeGlaiveChips.cs` (their rules in `Runtime/NativeGlaiveAuthority.cs`) |
| The chips' cards and picks | `Runtime/NativeGlaiveChipArt.cs` |
| Checks | `Tests/GlaiveChecks.cs` (`CoreTests --glaive`), `Tests/GlaiveChipChecks.cs` (`CoreTests --glaive-chips`) |

## Not yet

- No controller-specific mapping beyond Q and E.
- The chips' rules are checked in the Editor's build checks (`NativeGlaiveValidation.cs`); their picks and world effects have not been
  seen in the Editor or a build yet. Keen Edge's bleed ticks show as the arena's poison ticks.
- Not played by a person; not tried between two real clients.
