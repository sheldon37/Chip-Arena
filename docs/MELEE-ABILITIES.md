# Melee abilities (Q / E) — design proposal

Status: proposal, owner request 2026-10-11 ("overhaul melee abilities … magic / ranged have Q and E; change this for
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
Wolf Knight somersault). The cooldown model now matches magic: Q ≈ 6–8 s, E ≈ 10–12 s, both paid in stamina (melee's
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
- Windup .12 s, then 7 m in .22 s; stops at walls and at the first enemy hit.
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
- Cooldown 7 s, 20 stamina. It's a reposition with a .1 s windup, not an escape from stun.

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
- The first enemy hit is carried and knocked back 6 m for 20 damage. If it is driven into a wall, it takes another 15
  and .6 s stun.
- The charge stops at the first hit or at a wall.
- Cooldown 8 s, 25 stamina.

**E · Aegis Ricochet** (`aegis-ricochet`, evolves `shield-throw`): the shield throw now bounces.
- It bounces to up to 2 more enemies within 7 m of the last hit, 22 damage each.
- The first target is knocked down, as today. The shield then returns.
- While the shield is out, the thrower can't block. That's the risk.
- Cooldown 9 s, 35 stamina. Up to 66 split across three targets; 22 on a single target plus the knockdown.

## Greatsword — Juggernaut (heavy, area, reach)

**Q · Earthsplitter** (`earthsplitter`): a .45 s windup with the blade dragged behind.
- The slam sends a fissure 10 m forward, 1.6 m wide, travelling at 25 m/s.
- Enemies on it take 30 damage and a .5 s knock-up.
- The cracked ground glows for 2 s and slows anyone on it by 30 % (×.7 speed).
- Cooldown 8 s, 30 stamina.
- This is the greatsword's answer to kiting: reach in a line, not a dash.

**E · Wolf Knight** (`wolf-knight`, keeps `wolf-spin`): today's somersault with ragdolling direct hits and a landing
shockwave.
- New: **super armour** during the flip. Light hits (< 25 damage) cause no hitstun, but damage is still taken.
- Cooldown 11 s (was 5 s), stamina 45 (was 55). Its damage is unchanged.

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
  (from, to), Recast, Absorb (projectile stopped by the charge), Bounce (the shield hop), Land (Skyfall/Wolf Knight
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
