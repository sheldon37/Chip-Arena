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
| Checks | `Tests/GlaiveChecks.cs` (`CoreTests --glaive`) |

## Not yet

- No intro, no chips of its own, no controller-specific mapping beyond Q and E.
- Not played by a person; not tried between two real clients.
