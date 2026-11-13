# Fangs: a DEV dagger class

The owner's request, 2026-11-10: "Two sets actually Katana and New Daggers type (Testing different abilities and animation sets) Katana will
be Public, and DEV-Only Daggers", then "Make very high quality designed katana / dev dagger classes; Both with unique chips with their
abilities that play unique animations when selected. Use the new animation pack for both classes and use really cool effects. Use really
great designs for the weapons, and give them both a very awesome intro with effects".

- DEV only: an option on the main menu's DEV screen like every DEV class (`devOnly`). Its kit answers only a pilot allowed it (a grant or
  developer access).
- Claude built the rules as well as the look; the rules are logged for Astra.
- An original design: twin curved venom daggers, a shadow assassin. The moves are the Frank Slash Pack's assassin set, its stealth kills and
  its paired criticals.

Status: on `claude/blades` (merged into shared and the test channel with this release). Not played by a person or between two real clients.

## The kit

| Key | Move (`id`) | What it does | Damage |
| --- | --- | --- | --- |
| LMB | Fang Flurry (`fangs-cut`) | Five quick cuts that chain (0.4 s window): a leaping strike, a double, a backhand, a cross and a spinning finisher. Each leaves a venom stack; the last leaves two | 11, 11, 12, 14, 20 |
| LMB on a sprint | Pounce (`fangs-pounce`) | A 5 m dash in 0.32 s, cutting everyone it passes within 1.4 m of its line (once each) and leaving two stacks; a ring of dust where it ends | 20 |
| LMB in the air, looking down | Plunging slam | The plunge every melee weapon has (owner 2026-11-11: slam attacks from the air for the Glaive, the Fangs and the Katana): looking 45 degrees or more down after a moment airborne, a click drops you at 24 m/s and the landing cuts everyone within 0.9 m, then a 0.3 s recovery. Not while a move of the kit runs. Core `NativeAirAttackSimulation` by kin (the daggers') | 1.5 times the weapon's 11, up to 60% more from higher |
| Q | Shadow Step (`fangs-step`) | Gone for 0.14 s, then behind the foe nearest the aim (12 m, 35 degrees of the look, in sight), facing it (the view turns with you), and a stab with two stacks. With no foe, a 6 m blink along the look | 24 |
| E | Smoke Veil (`fangs-veil`) | Smoke, and unseen by enemies for 4 s or until you attack: the body, the name and the health bar are not drawn for them, bots and the lock-on pass you over. That attack deals twice as much and leaves three more stacks | |
| 1 | Fang Fan (`fangs-fan`) | Five venom knives across 30 degrees, 34 m/s out to 18 m, each 9 and a stack | 9 each |
| 2 | Venom Bloom (`fangs-bloom`) | Every poisoned foe within 14 m takes 6 per stack at once; the stacks are spent | 6 a stack |
| 3 | Assassinate (`fangs-assassinate`) | The foe in front within 2.4 m. From behind (its back to you within 75 degrees) or below 40% health: the takedown, both held in the pack's paired stealth kill, lethal at its blow (a boss takes 60 and is not held). Otherwise the paired frontal critical for 45 | 45 / lethal |
| RMB, F, Shift | Guard, parry, roll | As any melee weapon | |

**Venom** is the arena's poison, by stacks. A foe holds up to 6 stacks, each 1.6 a second. A new stack refreshes every stack's 4 s clock.

Every number is in Core `NativeFangs`. Cooldowns: Q 7, E 12 (from its end), 1 6, 2 10, 3 18 seconds.

## The look

- **The kills** (Core `NativeBladeCuts`; owner 2026-11-12: "I also want all bladed weapons to support gibbing"; docs/BISECT-GIB.md "Blade
  classes"): as the daggers', a slit throat on the first four cuts and on the takedown from behind (a throat cut), an arm (a forearm when the
  cut is low) on the finisher, the pounce, the shadow step's stab, the frontal critical and a thrown knife; the slam's chop an arm. The venom
  bloom cuts nothing.
- **The daggers** (Core `NativeWeaponModels.Fangs.cs`, sculpted with WeaponSculpt): twin recurved fangs, a venom channel lit in the accent,
  blackened steel with a bright edge, a serpent's guard, a wrapped grip. Thrown knives fly as their own blades (Runtime
  `NativeBladePresentation.cs`).
- **The motion** (Core `NativeBladeMotion`): the pack's assassin stance (its equipped idle), root-motion runs and walks, guard, block, tumble
  and hit reactions. The cuts are the pack's Dual (twin swords) set's chain (the owner's pick 2026-11-13, in place of the Rogue's): its first
  combo's flat forehand, springing double and bent-over lunging cut, its sixth combo's long forehand lunge, then its first combo's low
  spinning sweep as the finisher (`Frank_RPG_Dual_Combo01_1`, `01_2`, `01_3`, `06_1`, `01_4`; strikes 0.43, 0.26, 0.24, 0.30, 0.24 s, at up
  to 1.6 times the clips' pace; the set ships as root-motion takes only, played in place). The cuts that lunge (the third's step in, the finisher) take the whole body even on the move, their legs carrying the
  lunge; the others play on the upper body over the stride while moving. The dash is the Rogue (dual blade) set's fifth
  chain's deep lunge (owner 2026-11-11: "utilize the "Rogue" from the frank slash animation set"), at up to twice its pace. The slam is the
  set's air attack, held to the landing. The step its dashing stab
  skill; the veil its backward evade; the fan its two-handed outward sweep (Attack06: the knives flung from both hands; its spinning
  Attack04 lay the body flat on the floor) and the bloom its two-handed strike skill (both on the upper body while moving, the legs free);
  the takedown the pack's stealth kill from behind, its throat cut (`Frank_Stealth_Kill_Behind_01`, the owner's pick 2026-11-13: the foe's
  head held with a fang at its throat, the fang drawn across on the blow at 1.3 s of the clip; the victim plays the matching
  `Hit_Behind_01` on the same window, so both bodies stay together) and the frontal critical the assassin's paired critical. A foe of
  any class plays its side (owner 2026-11-13: "Give one to everyone"; before, only the Katana's, DEV Katana's and Fangs' bodies did):
  docs/KATANA.md "The record".
- **Effects** (Core `NativeBladeFx.cs`, in the pilot's weapon colour):
  - weapon trails from both blades (owner 2026-11-11: "Give both weapon weapon trail vfx"): each blade sampled through every move, its
    sweep a smooth crescent (a spline through the samples) narrowing to its tail: a sheet in the weapon colour, a white-hot edge, a faint
    halo, venom shed off the fastest of it. Their strength follows the blade's speed in the hand, so a wind-up leaves little; a dash and a
    slam trail at a set strength. Hidden from enemies while veiled;
  - venom splashing from each cut and dripping from the poisoned (more stacks, more drips);
  - the shadow step's smoke at both ends;
  - the veil's billow, and a thin smoke at the feet while it lasts (for the wielder and allies);
  - the knives' streaks;
  - the bloom: a green pulse from the wielder, and a burst sized by the stacks on each poisoned one;
  - the takedown's dark flash and, on a kill, its smoke.

## The intro: Silent Kill (`fangs-silent-kill`)

7 s, the name at 5.2 s, on the pack's clips (Core `NativeBladeIntros`).

1. Darkness and a low dark mist (lit smoke, the venom light under it). Two points of venom-green light, then the pilot forms out of
   gathering smoke, green sparks drawn in with it, crouched (the stealth sneak idle).
2. A shadow step: gone in a burst of smoke, a streak of green along its way, back a stride ahead in another burst.
3. The twin daggers in a flurry (the assassin's fifth combo): green trails from both blades, venom flung off the fastest of them.
4. The two-handed strike into the floor (4.25 s): a hot flash at the blades, a shockwave of green spikes over the floor, venom splashing
   out, a billow of smoke.
5. The hero: the daggers held low, venom beads swelling and dripping from both tips, smoke curling round the legs.

Its look (owner 2026-11-11: "Floating parts, needs much improved vfx"): the off-hand fang held in the model's own left hand (it hung where
the hidden rig's hand was); the trails, drips and flung venom taken from the blades where the model holds them (the runtime hands them to
the script each frame, Runtime `NativeBladePresentation.BladeIntroHeld`); smoke where soft glow balls hung before; no glow rings.

Its music is the pulse-wave bed.

## The chips

Ten chips of its own (tier `fangs`, "FANGS SPECIAL", venom green `#7dff3a`, a black chrome cartridge with the shards crest). They are offered
only to the Fangs (two guaranteed, 85%). Rules: Runtime `NativeFangsAuthority.cs`; numbers and copy: Core `NativeFangsChips`.

| Chip (`id`) | Copies | What it does |
| --- | --- | --- |
| Neurotoxin (`fangs-neurotoxin`) | 2 | Venom deals +30% a copy; each stack slows its victim by 4% (24% at most) |
| Hydra (`fangs-hydra`) | 1 | The fan throws 4 more knives (9), each passing through its first enemy |
| Long Shadow (`fangs-long-shadow`) | 1 | The step reaches 18 m and holds two charges |
| Silent Veil (`fangs-silent-veil`) | 1 | The veil lasts 2 s longer; your first attack from it does not end it |
| Chain Bloom (`fangs-chain-bloom`) | 1 | A bloom's kill leaps the victim's venom to every enemy within 5 m |
| Bloodletting (`fangs-bloodletting`) | 1 | Your venom heals you for 40% of its damage |
| Predator (`fangs-predator`) | 1 | +25% damage to enemies below half health, +12% move speed |
| Death Mark (`fangs-death-mark`) | 1 | Assassinate takes down enemies below 60%, and a takedown kill resets it |
| Smokescreen (`fangs-smokescreen`) | 1 | The veil leaves a cloud 4.5 m across for 5 s; enemies inside deal 30% less damage |
| Twin Venom (`fangs-twin-venom`) | 1 | Venom stacks to 10, and the flurry's last cut leaves 3 |

The cards and picks are in Runtime `NativeBladeChipArt`, shared with the Katana's.

## Sound

Synthesized (Core `NativeToonSynth`, ids `fang-*`): a quick swish, the stab, a venom drip, the hiss, a thrown knife, the vanish, the smoke, the
bloom, a heartbeat and a low toll.

## Files

Shared with the Katana (see docs/KATANA.md "Files"): `Core/NativeFangs.cs`, `Core/NativeFangsChips.cs`, `Runtime/NativeFangsAuthority.cs`, and
the shared record, motion, effects, presentation, chip art, intros, validation and checks.

## Seen in the Editor (2026-11-10)

- The five cuts, the shadow step (its smoke, the landing behind the foe, the stab), the fan (five knives flung from both hands), the bloom
  (its pulse and a burst on the poisoned), the veil (the backward roll into its smoke), the takedown from behind (the pack's paired stealth
  kill, the victim held in the pilot's arms and down at the blow); Silent Kill from start to card; the ten chips' picks.

## Not yet

- Grantable from the main menu's DEV screen only once its key is in the account server's grant list (a Site publish, Astra's).
- Not played by a person; not tried between two real clients. The frontal critical (Assassinate from the front) not captured; it is the
  same paired move on the assassin's critical clips.
