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

Status: on `claude/blades`. Not played by a person or between two real clients.

## The kit

| Key | Move (`id`) | What it does | Damage |
| --- | --- | --- | --- |
| LMB | Fang Flurry (`fangs-cut`) | Five quick cuts that chain (0.4 s window). Each leaves a venom stack; the last leaves two | 10, 10, 11, 12, 18 |
| LMB on a sprint | Pounce (`fangs-pounce`) | A 5 m leap; landing cuts everyone within 1.6 m and leaves two stacks | 20 |
| Q | Shadow Step (`fangs-step`) | Gone for 0.14 s, then behind the foe nearest the aim (12 m, 35 degrees of the look, in sight), facing it, and a stab with two stacks. With no foe, a 6 m blink along the look | 24 |
| E | Smoke Veil (`fangs-veil`) | Smoke, and unseen by enemies for 4 s or until you attack: the body is not drawn for them, bots and the lock-on pass you over. That attack deals twice as much and leaves three more stacks | |
| 1 | Fang Fan (`fangs-fan`) | Five venom knives across 30 degrees, 34 m/s out to 18 m, each 9 and a stack | 9 each |
| 2 | Venom Bloom (`fangs-bloom`) | Every poisoned foe within 14 m takes 6 per stack at once; the stacks are spent | 6 a stack |
| 3 | Assassinate (`fangs-assassinate`) | The foe in front within 2.4 m. From behind (its back to you within 75 degrees) or below 40% health: the takedown, both held in the pack's paired stealth kill, lethal at its blow (a boss takes 60 and is not held). Otherwise the paired frontal critical for 45 | 45 / lethal |
| RMB, F, Shift | Guard, parry, roll | As any melee weapon | |

**Venom** is the arena's poison, by stacks. A foe holds up to 6 stacks, each 1.6 a second. A new stack refreshes every stack's 4 s clock.

Every number is in Core `NativeFangs`. Cooldowns: Q 7, E 12 (from its end), 1 6, 2 10, 3 18 seconds.

## The look

- **The daggers** (Core `NativeWeaponModels.Fangs.cs`, sculpted with WeaponSculpt): twin recurved fangs, a venom channel lit in the accent,
  blackened steel with a bright edge, a serpent's guard, a wrapped grip. Thrown knives fly as their own blades (Runtime
  `NativeBladePresentation.cs`).
- **The motion** (Core `NativeBladeMotion`): the pack's assassin stance (its equipped idle), root-motion runs and walks, guard, block, tumble
  and hit reactions. The cuts are its first combo and its second's last two parts; the pounce its leaping attack; the step its dashing stab
  skill; the veil its backward evade; the fan and the bloom its throwing attack and its two-handed strike skill (on the upper body, the legs
  free); the takedown the pack's stealth kill from behind (the victim plays the matching hit clip) and the frontal critical the assassin's
  paired critical.
- **Effects** (Core `NativeBladeFx.cs`, in the pilot's weapon colour):
  - twin trails from both drawn blades;
  - venom splashing from each cut and dripping from the poisoned (more stacks, more drips);
  - the shadow step's smoke at both ends;
  - the veil's billow, and a thin smoke at the feet while it lasts (for the wielder and allies);
  - the knives' streaks;
  - the bloom: a green pulse from the wielder, and a burst sized by the stacks on each poisoned one;
  - the takedown's dark flash and, on a kill, its smoke.

## The intro: Silent Kill (`fangs-silent-kill`)

7 s, the name at 5.2 s, on the pack's clips (Core `NativeBladeIntros`).

1. Darkness and a low green mist. Two points of venom-green light, then the pilot forms out of smoke, crouched (the stealth sneak idle).
2. A shadow step: gone in a puff, back a stride ahead.
3. The twin daggers in a flurry (the assassin's fifth combo), venom flung at each cut.
4. The two-handed strike into the floor (4.25 s): the venom blooms in a great green burst.
5. The hero: the daggers held low, venom dripping, smoke curling round the legs.

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

## Not yet

- Grantable from the main menu's DEV screen only once its key is in the account server's grant list (a Site publish, Astra's).
- Not played by a person; not tried between two real clients. The paired clips' alignment (where the victim is held) needs judging in the
  Editor.
