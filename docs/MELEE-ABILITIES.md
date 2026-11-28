# Melee abilities (Q / E) — design and simulation contract

Status: simulation implemented on `codex/melee-abilities-20261012` (base `19c00c7`), pending integration, native playtest and Claude presentation. See [MELEE-SIMULATION-HANDOFF.md](MELEE-SIMULATION-HANDOFF.md). Original owner request 2026-10-11 ("overhaul melee abilities … magic / ranged have Q and E; change this for
melee … creative direction, fun but balanced; can change abilities for melee as needed, not DEV ones"). Unity only.
DEV classes (Siren, Emperor, Ansatsuken, L33T) are out of scope.

Status check 2026-10-12: not started (no content, branch or PR). [MELEE-FEEL.md](MELEE-FEEL.md) evaluates melee
fluidity and gap closing, lists this kit as the largest gap-closing fix (G1) and records the presentation contract.

Ownership (AGENTS.md role split):
- **Astra:** rules, timing, hit tests, damage, cooldowns/costs, recasts, replication, bots, input and tests.
- **Claude:** animation (Explosive RPG pack clips), VFX, the new block effect, sound, HUD/ability cards.

The contract between the two is the replicated special state plus the fx tags below.

## Why

Today melee has LMB combos, a timed RMB block and one stamina special on E; Q does nothing. docs/ABILITIES.md records
the result: melee bots lose about 100 % of duels to every ranged and magic spec in cover. They walk in and get poked.

So every melee **Q is a gap closer or a way through fire**, and every **E is the weapon's signature move**. The four
current specials become the base for the Es, keeping what already works (launch juggle, dagger throw, shield throw, the
Whirlwind spin; replaces the Wolf Knight somersault). The cooldown model now matches magic: Q ≈ 6–8 s, E ≈ 10–12 s, both paid in stamina (melee's
resource). The basic combo keeps its current costs.

## Shared rules

- **Blocking.** Every ability hit can be blocked by the existing timed block (RMB) and dodged by a roll. A blocked
  ability hit block-stuns the attacker as a normal blocked melee hit does (NativeMeleeRules.BlockStun).
- **Readability.** No ability is an instant, untelegraphed kill. Each has a visible windup of at least .15 s, or a
  visible mark, before damage.
- **Commitment.** Abilities can't be cast while stunned, knocked down, rolling or blocking. Q and E cancel each other
  only where stated. A roll cancels an ability only during its windup, matching the "roll cancels melee attacks" rule.
- **Stat chips.** Existing chips (damage, reach, attackRate, impulse, backstab, meleeArc, projectileSpeed) apply as
  they do to the current specials. Cooldown-reduction chips, if any, apply to Q and E.
- **Damage budget.** HP 100. A Q deals ≤ 30. An E deals ≤ 60 total including follow-ups, which sits between Frag (66)
  and Ball Lightning (40).
- **Spectators and killcam** read everything from replicated state. Nothing here is client-only.

## Sword — Duelist (all-rounder, juggle combos)

**Q · Flash Step** (`flash-step`): a dash-thrust along the aim.
- Windup .15 s, then 7 m in .22 s; stops at walls and at the first enemy hit.
- That enemy takes 26 damage and .35 s hitstun.
- On a hit, half the cooldown is refunded.
- Cooldown 6 s, 18 stamina.
- No invulnerability: projectiles still hit you mid-dash.

**E · Rising Dragon** (`rising-dragon`, evolves `sword-launch`): today's rising slash.
- Launch: 1.12× weapon damage ≈ 34, aerial juggle.
- **Recast E within .9 s:** *Skyfall*. Leap after the launched target and slam them down for 22, a ground-bounce
  knockdown, and a 2 m landing shockwave dealing 10 to others.
- Without a recast, the cooldown drops to 7 s.
- Cooldown 10 s, 35 stamina. Total ≈ 56 on a full hit.

## Daggers — Shadow (assassin, mobility, marks)

**Q · Shadow Step** (`shadow-step`): blink 6 m in the movement direction (forward if standing).
- An afterimage decoy stays behind for 1 s, and bots target it.
- The next dagger hit within 1.5 s deals +40 % (stacks with `backstab`).
- No damage of its own.
- Cooldown 7 s, 20 stamina. It's a reposition with a .15 s windup, not an escape from stun.

**E · Kunai Mark** (`kunai-mark`, evolves `dagger-throw`): the spinning dagger throw, same gravity and range.
- On a hit: 18 damage plus a **Mark** for 4 s. The mark is a red sigil over the target that every player can see.
- **Recast E while the mark is up:** teleport behind the marked target with a .2 s shimmer the target can see (time to
  block or roll), then strike for 28 and .4 s stun.
- A miss sets the cooldown to 4 s.
- Cooldown 11 s, 30 stamina. Total 46.

## Shield — Guardian (control, protection)

**Q · Bulwark Charge** (`bulwark-charge`): a shield-first bull rush.
- 8 m in .45 s with a .15 s windup.
- While charging, frontal projectiles hit the shield and are absorbed: up to 3, after which the charge ends.
- The first enemy hit is carried and knocked back 6 m for 20 damage. If it is driven into a wall, it takes another 10
  and .6 s stun.
- The charge stops at the first hit or at a wall.
- Cooldown 8 s, 25 stamina.

**E · Aegis Ricochet** (`aegis-ricochet`, evolves `shield-throw`): the shield throw now bounces.
- It bounces to up to 2 more enemies within 7 m of the last hit, 20 damage each.
- The first target is knocked down, as today. The shield then returns.
- While the shield is out, the thrower can't block. That's the risk.
- Cooldown 9 s, 35 stamina. Up to 60 split across three targets; 20 on a single target plus the knockdown.

## Greatsword — Juggernaut (heavy, area, reach)

**Q · Earthsplitter** (`earthsplitter`): a .45 s windup with the blade dragged behind.
- The slam sends a fissure 10 m forward, 1.6 m wide, travelling at 25 m/s.
- Enemies on it take 30 damage and a .5 s knock-up.
- The cracked ground glows for 2 s and slows anyone on it by 30 % (×.7 speed).
- Cooldown 8 s, 30 stamina.
- This is the greatsword's answer to kiting: reach in a line, not a dash.

**E · Whirlwind** (`whirlwind`, owner replacement 2026-10-12): steerable greatsword spin.
- Fixed .3 s windup, 3.0 s active spin, then .5 s recovery. Attack-rate chips do not add pulses or shorten these phases.
- Steer at ×.8 ordinary run speed during active (5.44 m/s before movement modifiers); no sprint, jump, block or primary attack. Windup/recovery are rooted. A roll cancels only windup.
- Seven 8.5-base-damage radial pulses at active ages 0, .45, .90, 1.35, 1.80, 2.25 and 2.70 s: **59.5 maximum base damage per target**. Base radius 2.6 m, normal reach/damage modifiers, visibility checks and team rules.
- Only pulse 1 can apply the light .12 s stagger, respecting normal boss/recovery immunity. Each pulse checks block and roll again; a successful normal melee block punishes/cancels the caster. Wolf Knight's launch, landing slam and super armour are removed.
- Recast E after .5 s of active to stop immediately, then take the full .5 s recovery. No extra stamina or cooldown refund. Cost 45, cooldown 11 s from commitment; normal cost/cooldown modifiers remain.
- Bots prioritize it with an enemy within 3 m, steer while active, and only end early when the target has moved beyond 4 m.
- Replication: `MeleeKit.Kind="whirlwind"`, `Phase=windup/active/recovery`, `Release` spin start, `Until` spin end (shortens on recast), `RecoverUntil` recovery end, `Tick` emitted pulse count (0–7, including misses). `Yaw` follows steering. Targetless recast uses `RecastKind="whirlwind"`, `RecastUntil=Until`, `Target=0`, only after the minimum spin.
- `AbilityCast`, one `AbilityHit` per successful pulse/target, and targetless early-stop `Recast` carry `Detail="whirlwind"`. No artificial landing event. `SpecialKind="whirlwind"`; `SpecialUntil`/`AttackUntil` include recovery. Claude owns matching spin poses/VFX/sounds/HUD; old Wolf Knight presentation must not be reused as a flip.

## Greatsword E: Whirlwind (owner 2026-10-12)

Owner: "Change the E animation on greatsword; completely. Lets change it into something like ... a whirlwind attack?
Spinning around for a short period while able to move. (Of course, its a great sword so make sure to add weight realism)",
"(Think like Garens spinning attack)". The rule is Astra's (above, `Core/NativeWhirlwindRules`, PR #132). This section
is the presentation's contract with it (Claude, branch `claude/whirlwind`).

**Rule as built vs. the first proposal.** Claude's draft put the first pulse .35 s into the spin, after a spin-up. Astra's
rule pulses at `Release + k × .45` for k = 0..6 (seven pulses, 59.5 base), so the first pulse lands at `Release`. The
presentation follows the rule: the spin-up moved into the windup's uncoil, so the blade already crosses the front at full
speed when the spin starts. The draft's `RecoverUntil` and pulse counter shipped as `RecoverUntil` and `Tick`.

**What the presentation reads (all replicated).**
- `MeleeKit.Kind/Phase/At/Release/Until/RecoverUntil`.
  - The cast owns the pose from `At` to `RecoverUntil`.
  - Recovery starts at `Until`, the spin end; an early recast moves it up.
- Pulse k's moment is `Release + k × Interval`, from the clock. `Tick` agrees with it, but the pose doesn't need it.
- `RecastKind="whirlwind"` drives the HUD's END EARLY state.
- `SpecialKind/SpecialUntil` gate the rig routes, as for every kit E.
- Events:
  - `AbilityHit` (one per pulse per target) draws the hit flash and slash and plays the impact.
  - `AbilityCast` draws the cast ring.
  - The targetless `Recast` draws and plays nothing; the stop shows the early end.

**Motion (`Core/MeleeKitMotion`).**
- **Windup (.3 s).** The heavy blade drops from the high guard and drags low behind the right hip, its tip on the floor.
  - The hips load right while the torso coils.
  - The knees bend and the weight drops, then the hips fire first.
  - The hands come round with the blade trailing. Their rate eases up from rest to the full spin by `Release`, peaking about 1.06× the full rate in the uncoil.
- **Active.**
  - One turn per pulse: the blade tip crosses the facing at every pulse.
  - The upper body and level blade spin about the spine, over the gait's legs.
  - The hips drive each turn, and the chest leads the trailing blade by .3 rad.
  - The torso leans back and away from the blade (counterbalance) and the head leads into the turn.
  - The hips sit 15 cm low, with a 2 cm dip each turn and a 4.5 cm mass shift against the blade.
  - The blade dips low (sparks) on the far side of each turn.
- **Legs.** They stay the gait's, with planted, world-anchored feet:
  - the stance widens 8 cm;
  - standing feet mark time once per turn per foot (`MeleeGait.Widen/Shuffle`, both off by default);
  - moving, the legs run the gait from the actual velocity under the spinning upper body.
- **Recovery (.5 s).** The turn decelerates evenly from the full rate.
  - When its momentum can reach the facing, it stops facing the front. The full spin always does: a 70° last partial turn over .17 s.
  - An early recast stops within .15–.25 s, then turns back the short way if needed.
  - The weight drops, the hips and mass lurch on, and the blade's tip goes down to the floor.
  - Then the pilot returns to the guard.
- **First person.** The camera never turns.
  - The braced hands ride a low orbit.
  - Each turn the blade comes in from the right, lies out ahead across the lower view as it crosses the front, and leaves to the left with an arc streak.
  - For the rest of the turn it points down behind the camera.

**Effects (`Core/NativeMeleeKitFx`), audio, HUD.**
- **Windup.** A 2.6 m telegraph ring with inward ticks, and floor sparks from the dragged tip.
- **Spin:**
  - an accent arc ribbon behind the blade, brightest and widest toward the tip, left behind by the pilot's travel;
  - a faint ring at the hit radius, brightening at each pass;
  - wind and dust swirling round the feet;
  - sparks where the tip passes low.
- **Hits.** The existing hit flash plus a slash across each struck target.
- **Stop.** A dust puff, plus sparks where the tip meets the floor.
- **Quality.** Reduced effects and the low tier thin the effects (no smoke on low).
- **Audio** (existing bank keys only):
  - `melee.greatsword.spin` on the release;
  - `melee.greatsword.swing` at each later pulse;
  - `impact.ground` + `melee.greatsword.hit` at the stop.
  - Wanted from the owner's bank: a looping spin whoosh and a heavy scrape-stop.
- **HUD.** The E card reads SPINNING, then END EARLY (amber, its dial draining to the spin end) once the recast opens. The locker icon is `rotate-cw`.

**Notes for Astra (no simulation change requested).**
- The presentation assumes the fixed phases above (no attack-rate scaling), as the rule states.
- Guests: the spin pose is a pure function of the replicated kit, so a guest's predicted motion only moves the legs (gait velocity).
- The kit's Wolf Knight presentation was removed: no ability can replicate `Kind="wolf-knight"` and `SuperArmour` is never raised. The legacy `wolf-spin` special phrase stays, because the Unity warm-up check and `NativeSpecialChecks` still use it.

## The new block effect (Claude, presentation only)

- **Guard:** while the block window is open, an energy guard arc in the weapon's colour appears in front of the weapon
  (a curved, shimmering ward, not a floor disc). The shield draws a larger hex ward.
- **Clash:** on a successful block, a bright spark spray along the weapon edge and a vertical shock ring facing the
  attacker. Also a short white flash on the guard, a short camera hitch for the blocker (a presentation-only freeze of
  about 50 ms) and a metal clang from the pack sounds. The attacker's recoil already plays.
- **Deflect:** a reflected projectile gets a bright deflection streak and a ping.
- **Cooldown:** the existing block-cooldown tint stays.
- **Implemented (presentation, branch `claude/fx-block`):** Core `NativeBlockFx`, Runtime `NativeBlockFxPresentation`,
  `Resources/NativeBlockFx.shader`. Until the Block Detail below exists, a Block event is a clash when its Target (the
  attacker) is melee-class and within its reach + 1.25 m, or has no seat (a PvE add); otherwise a deflect. A Detail of
  `clash` or `deflect` overrides that guess and `guard` draws no hit. Known misread: a blocked summon strike (its Target is
  the summoner).

## Contract for presentation (what Claude needs from the simulation)

- Each new ability is an entry in GameContent.json `abilities`:
  - `spec` is sword, daggers, shield or greatsword; `slot` is q or e.
  - The ids are as above; `fx` equals the id.
  - Cooldown, cost, windup, range, damage, radius and duration as in this document.
  - A `bot` block.
- The HUD and ability cards read these entries.
- **Replicated per pilot:** the active special or ability kind, its phase times (start, release, end), the yaw, and the
  recast window end.
  - Kunai: the mark target id and expiry.
  - Shield ricochet: the shield's position and target chain.
  - Fissure: its start point, direction, speed and life.
  - Afterimage: its position and expiry.
  - Super armour flag.
  - Charge: the absorbed-projectile count.
- **Events,** with the fx tag in Detail: AbilityCast, AbilityHit (target, point, normal), Mark (on/off), Teleport
  (from, to), Recast, Absorb (projectile stopped by the charge), Bounce (the shield hop), Land (Skyfall
  landing), FissureStart.
- Block already emits EventKind.Block. Please add a Detail of `guard` (window opened), `clash` (melee blocked) or
  `deflect` (projectile reflected), plus the attacker id and contact point.
- Melee **Q input:** ConversionGame currently sends AbilityQ only for magic, Emperor and Siren. Melee needs it too.
  Melee E becomes the ability system's E with recast support; the old `secondary` path is retired.

## Acceptance

1. Each new ability works for host, guests, a late joiner and the killcam, with bots using them sensibly.
2. Melee bots win a reasonable share of duels in cover against ranged and magic. A target band of 25–55 % is an
   improvement, not a guarantee; the owner's playtest decides.
3. No ability lets a target be chain-stunned without a gap. The existing stagger immunity
   (NativeMeleeRules.StaggerImmuneThrough) applies to ability stuns.
4. Old maps, DEV classes and ranged/magic kits are unchanged.
