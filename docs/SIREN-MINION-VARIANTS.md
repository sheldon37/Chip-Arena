# Siren minion kinds: presentation and rules contract (owner 2026-10-12)

Owner request: "While we're at this, please improve animations of siren minions and design. Give them hitting from
melee too. Add some variants (Like brutes, spitters, runners, etc.)"

Split: Claude built the presentation on `claude/siren-minions` (models, animation, hit reactions, telegraphs, effects,
audio routing, DEV preview). Astra owns the rules: simulation, AI, stats, spawn mix, networking and rule tests. Nothing
below is implemented in the simulation yet. The numbers are starting proposals; Astra may tune them and should record
any change here so the presentation follows (Core/NativeHordeKinds.cs holds the presentation's copy, and
Tests/HordeKindChecks.cs fails when the authority's attack formula or capsules change without it).

Update (2026-10-12, zombies): the kinds now draw as zombies built from the pilot body on the owner's zombie mocap,
entrances included, and the brute gets a knock-back smash look and a jump-down entrance from map data. That presentation,
its jump-down contract and the brute's knock-back, landing-slam, drop-entry and hit-volume proposals are in
docs/SIREN-ZOMBIES.md (with its own prompt for Astra). Everything below still holds.

## 1. What already works without new rules

The network already carries each add's kind (`NativeMinionFrame.addKind` = `PveAddStats.id`). The presentation resolves
it to a kind, so the shipped `shambler`, `runner` and `brute` already draw as themselves. Any new id that the content
names (`spitter`, `crawler`, `bloater`) draws as that kind with no presentation change. An unknown id draws as the
shambler.

Timed from replicated data only, on host, guests and killcam alike:

- Attacks: wind-up from `lastAttack`, the blow arriving exactly at `lastAttack + hit` (the content's `hit`, or .34 s when
  crawling or armless, as `StepPveHorde`), recovery by `max(cycle, hit + .05)`.
- The brute's slam disc (1.6 m, 1.2 m ahead) is drawn from the first frame of the wind-up and closes in to its rim at the
  strike, then the shockwave.
- Hit reactions from the replicated Hit/Headshot events and their `Normal`: directional recoil, a shove scaled by the kind's
  weight, stagger-back steps, a hit-stop on melee blows, the struck flash, and ink that lands and stays.
- A hit of a quarter of the add's health in the wind-up cancels the shown blow, as `StaggerAt` cancels the real one
  (inferred; see `staggerAt` below).
- Melee kills slice the add along the killing swing. The authority's severed limbs fly off as the model's own parts.

Until the new kinds exist, the owner can preview them: Settings · Developer · "Siren minion preview (looks only)" draws
every horde add as one kind, or as a mix. The Dev Sandbox pause menu · "Preview Siren Minions" opens a gallery with every
kind looping its whole set. Both use the adds' real rules and are presentation only.

## 2. Kinds, stats and timings

Today's rows (unchanged, from PveContent.json) and the proposed new rows. `hit` is the wind-up (the time from
`lastAttack` to the damage), active is the instant the authority resolves the hit, recovery is `cycle - hit`, and the
cooldown between attack starts is `cycle`.

| id | hp | damage | walk | run | reach | startRange | hit (wind-up) | cycle | recovery | lunge | impulse | reward | limbFrac | headFrac | stagger | smashRadius |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `shambler` | 60 | 6 | 1.5 | 3.4 | 1.75 | 1.45 | .34 | 1.05 | .71 | .45 | 90 | 25 | .35 | .4 | .45 | - |
| `runner` | 38 | 4 | 2.6 | 4.6 | 2 | 2.2 | .34 | .85 | .51 | .9 | 60 | 20 | .35 | .4 | .45 | - |
| `brute` | 240 | 15 | 1.3 | 2.4 | 2.1 | 1.7 | .43 | 1.7 | 1.27 | .3 | 260 | 80 | .45 | .5 | .25 | 1.6 |
| `spitter` | 45 | 7 | 1.6 | 2.9 | 11 | 9 | .55 | 2.6 | 2.05 | 0 | 40 | 30 | .35 | .4 | .45 | 1.3 (puddle) |
| `crawler` | 34 | 5 | 2.2 | 3.9 | 1.6 | 2.4 | .3 | 1.1 | .8 | 1.6 (pounce) | 70 | 20 | .3 | .35 | .5 | - |
| `bloater` | 150 | 10 | 1.1 | 1.8 | 1.6 | 1.4 | .62 | 2.2 | 1.58 | .2 | 180 | 45 | .4 | .45 | .3 | 2.6 (death burst) |

Attack names (`PveAddStats.attack`) that the presentation maps: `swipe`, `lunge`, `smash`, `spit`, `pounce`, `belly`
(also `burst`). Keep the existing crawl and armless overrides (.34 s hit, reach 1.3 / 1.2, lunge .15 / .45). A crawling
add of any kind is presented with the lunge.

## 3. Behaviours (proposed)

- **Brute: charge and slam.** It keeps today's smash (1.6 m disc 1.2 m ahead at `hit` .43; its knock-back and the
  jump-down's landing slam: docs/SIREN-ZOMBIES.md section 4). New charge when its target is
  6-12 m away with a clear line, with an 8 s cooldown:
  - .6 s wind-up, then a straight charge at 7 m/s for up to 1.6 s on the line fixed at the wind-up's end.
  - The first pilot it meets takes 18 damage and impulse 600, and the charge ends.
  - A wall or a large prop ends it and staggers the brute for 1.2 s.
  - Otherwise it ends into a smash when the target is within `startRange`.
- **Spitter: keep distance and lob.**
  - It holds 6-10 m from its target, strafing. Within 4 m it backs off at walk speed.
  - It spits when its target is within `startRange` 9 m with a line of sight, after the .55 s wind-up, every 2.6 s.
  - The glob is a host projectile on a gravity arc: about 9 m/s across, flight `max(.35, d/9 + .12)` s, apex
    `.9 + .08·d` m over the chord.
  - On landing it does 7 damage within .6 m and leaves an acid puddle (radius 1.3 m, 4 s). The puddle ticks 4 dps as a
    field (`Hit` Detail `field`, so no stagger).
  - At most 4 spitters alive.
- **Runner: flank and lunge.**
  - Between 5 and 9 m from its target it curves onto an offset approach, alternating ±60° around the target, so runners
    arrive from the sides.
  - It keeps today's lunge (2.2 m start, .9 m carried in the .34 s wind-up).
  - After a lunge it may sidestep 1.5-2 m before closing again (optional).
- **Crawler: low pounce.** It moves on all fours.
  - From 2.4 m it pounces: a 1.6 m leap carried over the .3 s wind-up, hitting at the end.
  - It may keep today's crawl rules.
- **Bloater: belly slam and death burst.**
  - The belly slam hits a 1.6 m disc 0.6 m ahead at `hit` .62.
  - On death it bursts .55 s later (the presentation's `BurstAt`): 25 damage with linear falloff over 2.6 m to pilots
    (other adds optional), impulse 400, and the burst event.
  - At most 2 bloaters alive.
- **Caps:** brutes 3, bloaters 2 and spitters 4 alive at once (all modes), inside the existing `maxAlive`.

## 4. Spawn mix (proposed `waves[].mix` and `bossMix[].mix`)

| stage | shambler | runner | brute | spitter | crawler | bloater |
|---|---|---|---|---|---|---|
| wave 1 | .85 | - | - | - | .15 | - |
| wave 2 | .55 | .25 | - | .1 | .1 | - |
| wave 3 | .4 | .22 | .12 | .12 | .08 | .06 |
| boss phase 1 | .5 | .25 | .1 | .1 | .05 | - |
| boss phase 2 (Find me) | .45 | .2 | .05 | .1 | .15 | .05 |
| boss phase 3 | .4 | .25 | .15 | .12 | - | .08 |
| boss phase 4 | .35 | .25 | .15 | .12 | .05 | .08 |

Find me (phase 2, Siren hidden): possession should keep choosing from the upright kinds (shambler, runner, spitter,
brute), never a crawler or a bloater. A crawler's grin is at knee height, and a bloater could burst the hidden Siren's
host. The possessed add keeps its kind's look plus the existing hint (seam, halo, grin, trail).

## 5. Hit volumes

| kind | collider | notes |
|---|---|---|
| `shambler`, `runner`, `brute`, `spitter` | CapsuleCollider radius .42, height 1.7 (today's) | unchanged; models are sculpted inside it |
| crawl (any kind that lost a leg) | radius .32, height .88 (today's) | unchanged |
| `crawler` | CapsuleCollider direction Z, radius .32, height 1.3, centre (0, 0, .25); body root .36 above the floor | the presentation's crawler fills this capsule |
| `bloater` | radius .5, height 1.7 | wider belly |

Tests/HordeKindChecks.cs reads today's capsules out of NativePveHorde.cs and holds every posed body to its kind's volume.
The trunk and head stay inside; the shoulders sit at the edge; reaching claws and striding legs go no farther than a
pilot's. If the collider changes, change `NativeHordeKinds.Radius / Height / Axis / Root / CenterAhead` with it.

## 6. Events and replicated fields the presentation listens to

The presentation matches on the event's `Detail` (any `EventKind`). A new `EventKind.Minion` appended at the end of the
enum is suggested, so the generic Arc/Field/Summon routes (sounds, impact cues) never pick these up. `Source` is the
boss seat, `Target` the add's id.

| Detail | when | fields | presentation |
|---|---|---|---|
| `minion-spit` | the glob leaves the mouth (at `lastAttack + hit`) | `From` launch point, `Position` landing point, `Amount` flight time (s), `Radius` puddle radius | the glob arc, its splash and the acid puddle; replaces the DEV preview's prediction for that add |
| `minion-acid` | each puddle damage tick (optional) | `Position`, `Radius`, `Amount` seconds left | puddle refresh (the puddle already lasts 4 s from landing) |
| `minion-slam` | the brute's strike resolves | `Position` disc centre, `Radius` | confirms the shockwave (today it plays from `lastAttack`; a cancelled smash then never shows one) |
| `minion-charge` | the brute's charge starts and ends | `Position` start, `From` direction or end point, `Amount` duration | lowered-shoulder run and dust trail (to add when the rule lands) |
| `minion-pounce` | the crawler's leap starts | `Position` landing point | leap streak (the crawler's attack already plays from `lastAttack`) |
| `minion-burst` | the bloater bursts | `Position`, `Radius`, `Amount` damage | the burst already plays .55 s after death; the event lets it follow a changed delay |

Small replicated fields that would make the presentation exact (all optional; today it infers them):

- `NativeMinionFrame.staggerAt` (float, match time): the add's `StaggerAt`. The presentation cancels a blow and reels
  the add on it, instead of inferring the stagger from a hit of a quarter of its health.
- `NativeMinionFrame.side` (int, 1 right / -1 left): `NativePveAdd.Side`, the arm that swings (today hashed from
  `lastAttack`).
- `NativeMinionFrame.chargeAt` and `swellAt` (float): the brute's charge and the bloater's swell, if they get them.
- `NativeDeath.addKind` (string) on minion deaths. Today the presentation remembers the kind from the living add. A guest
  or killcam that never saw the add alive draws its death as a shambler.

## 7. Presentation keys (for reference)

- **Models:** `minion-<kind>-<look>-<full|reduced|impostor>`, built in code (Core/NativeHordeModels.cs; the zombie
  builders in Core/NativeZombieModels.cs).
  - Looks: shambler 3, the rest 2, chosen by the add's id.
  - The nearest 24 adds (16 on the low graphics tier) draw full, the rest within 40 m reduced, impostors beyond.
- **Audio:** existing effects-bank keys only, silent without the owner's LocalAssets bank.
  - Swipe `siren.claw`.
  - Lunge and pounce `siren.rush`.
  - Smash: `structure.groan` in the wind-up, then `siren.slam` + `impact.ground`.
  - Spit `magic.darkness.shot`.
  - Belly: `field.acid` in the wind-up, then `blast.toxic`.
  - Brute and bloater steps `move.step.stone`.
- **Seam colours:** shambler violet, runner pink, brute ember, spitter acid green, crawler pale violet, bloater amber.

## Prompt for Astra

```
Task: Siren minion kinds, simulation side (owner 2026-10-12: "Add some variants (Like brutes, spitters, runners,
etc.)"). Claude's presentation is on claude/siren-minions (docs/SIREN-MINION-VARIANTS.md); it already draws every kind
from NativeMinionFrame.addKind and times attacks from lastAttack + the content's hit. Base: the current shared Unity
head.

1. Content (Assets/Conversion/Resources/PveContent.json + tools/export_pve.mjs if it is generated): add `spitter`,
   `crawler`, `bloater` rows with the numbers in section 2 (attack names spit / pounce / belly), and the wave and
   boss-phase mixes in section 4. Keep the shambler/runner/brute rows unchanged unless you tune them; record any change
   in section 2 and in Core/NativeHordeKinds.Proposed.
2. NativePveHorde: spawn colliders per kind (section 5: the crawler's Z capsule .32 x 1.3, centre (0,0,.25), root .36;
   the bloater's .5 x 1.7); caps per kind (section 3); behaviours: brute charge + slam, spitter keep-distance + lob
   (a host projectile on the stated arc, puddle field 1.3 m / 4 s / 4 dps, 7 direct within .6 m), runner flank,
   crawler pounce, bloater belly slam and the death burst .55 s after death. Possession in Find me only from upright
   kinds.
3. Events (section 6): emit minion-spit (From, Position, Amount = flight, Radius), minion-slam, minion-burst and, with
   the new rules, minion-charge / minion-pounce, preferably on a new EventKind.Minion appended to the enum so no generic
   route hears them. Replicate NativeMinionFrame.staggerAt and side, and NativeDeath.addKind for minion deaths (wire
   compatible additions; validate as the other minion fields).
4. Bots and guests: recruits and party bots target the new kinds like the others; guest replicas need nothing beyond
   the frame fields.
5. Tests: rule tests for each behaviour, the caps, the mixes and the colliders. Keep Tests/HordeKindChecks.cs passing:
   if you change the attack formula (StepPveHorde hit/recover/lunge), the smash disc or the capsules, update
   Core/NativeHordeKinds and this doc in the same change.
Do not change presentation files beyond NativeHordeKinds' mirrored numbers; hand visual follow-ups (the brute charge's
pose and dust, spit events replacing the preview) back to Claude in DEV_LOG.
```
