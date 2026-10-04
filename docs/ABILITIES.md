# Ranged and magic abilities (Q / E)

Every ranged and magic spec has a Q and an E. Melee specs and the DEV classes (Ansatsuken, Emperor, L33T, Siren) are
unchanged. Data lives in `src/shared/gun-kit.ts` and `src/shared/magic-kit.ts`, handlers in `gun-abilities.ts` and
`magic-abilities.ts`, the framework in `abilities.ts` / `ability-system.ts`. Older clients that do not see the host's
ability flag keep the legacy controls (design §3.12).

## Guns: Q = aim down sights, E = utility

| Spec | Q (hold) | E | Cooldown | Damage / radius | Notes |
|---|---|---|---|---|---|
| Pistol | Aim (FOV 55, spread ×.25, walk ×.75) | Frag Grenade | 8 s | 66 / 4.2 m | Lands where aimed, .6 s fuse after first contact, no detonation on player contact |
| Machine gun | Aim (FOV 52, spread ×.4, walk ×.55) | Underbarrel Rocket | 9 s | 55 / 3.6 m | Explodes on contact, arms after 3 m, breaks cover |
| Rifle | Scope 3× (FOV 28, no spread, walk ×.5) | Orbital Mark | 13 s | 85 / 3 m | Dart tags a target or point; strike tracks it, locks .2 s before landing 1 s later |

E works while reloading and costs no ammo.

## Magic: primary, Q spell, E spell

| School | Primary (dmg / interval / speed / gravity / mana) | Q (cd, mana) | E (cd, mana) |
|---|---|---|---|
| Fire | 24 / .5 s / 34 / 9 / 8; 1.5 m blast, 4 burn DPS | Flame Lance (5 s, 16): piercing javelin, 30 + burn | Meteor Strike (11 s, 35): 70 in 4.5 m, burning ground |
| Water | 20 / .38 s / 42 / 10 / 6; Soak slow on direct hit | Tidal Wave (6.5 s, 18): 22, knockback + slow | Geyser Wall (11 s, 32): three geysers, 60, knock-up + slow |
| Lightning | 15 / .28 s / 55 / 20 / 5; 6 chain to nearest enemy | Blink Strike (6 s, 16): 9 m teleport, 24 along the path | Ball Lightning (12 s, 38): slow orb zaps, 40 burst in 3.5 m |
| Darkness | 34 / .66 s / 26 / 3 / 10; 20 % drain | Shadow Shackles (7 s, 18): 20, strong slow, heals | Black Hole (14 s, 40): pull, 55 collapse in 6 m |

Pre-overhaul primaries (cfdce4b): fire 22/.56/24/12, water 18/.40/31/16, lightning 12/.27/85/40, darkness 27/.70/15/5;
E was a 3-4 s close-range push/pull/nova/fireball sharing the primary cooldown.

## Balance measurement (2026-10-04, `npm run test:duel-balance`)

Hard bots, seeded `Simulation`, n = 20 duels per pairing and arena (indicative: Wilson 95 % CI about ±20 points),
`BOT_LEAD [0,0,0]`. Before = `cfdce4b`, after = `claude/abil-integration`. Win % of the magic school averaged over
its duels against pistol, machine gun and rifle.

| School | Open before | Open after | Cover before | Cover after |
|---|---|---|---|---|
| Fire | 21 % | 55 % | 27 % | 69 % |
| Water | 3 % | 61 % | 15 % | 57 % |
| Lightning | 58 % | 87 % | 55 % | 52 % |
| Darkness | 5 % | 55 % | 3 % | 60 % |

Perfect-aim primary TTK on a still target at 15 m (after; harness table 3): fire 2.08 s, water 2.00 s, lightning
1.98 s, darkness 1.90 s; pistol 2.05 s, machine gun 1.53 s, rifle 1.25 s. Before (design MAP probe): magic 2.2–3.9 s.
Perfect-aim E against a 6.8 m/s strafer, mean damage per cast: frag 36, rocket 49, Orbital Mark 58, Meteor 61,
Geyser 55, Ball Lightning 51, Black Hole 60 (gate ≥ 35; Orbital ≥ 40).

Integration retune decisions:
- **Bot lead stays off.** Any lead (.2–.8) made the lead-using side win 80–100 % with 2–4 s duels; the hook now
  applies only to magic bots if it is ever set.
- **Lightning** 15 dmg / .28 s / 55 m/s / gravity 20 (M stream had 15 / .27 / 85 / 30): bots won 95–100 % against
  every spec; damage changes barely moved that, bolt speed did. It is still the strongest school in open-arena bot
  duels (62–95 %), but 36–69 % in cover.
- **Frag** 60 → 66 to meet the E gate. **Pistol / MG bots** ADS only from 25 m: ADS bots lost 98 % of duels when
  ADS started at 12–14 m (slower walking).

Known deviations, reported rather than tuned away: water beats rifle 100 % and darkness 96 % in the open; rifle
loses most open duels against magic (0–25 %) but wins 36–65 % of them in cover; melee bots still lose ≈100 % to every ranged and
magic spec in cover, as before (bot walk-in AI; before 0–4 %, after 0 %); Pong holder 45–87 % vs the same spec
(fire 87 % above the 55–80 band, lightning 45 % below). Bot fidelity is limited; the owner's playtest decides.
