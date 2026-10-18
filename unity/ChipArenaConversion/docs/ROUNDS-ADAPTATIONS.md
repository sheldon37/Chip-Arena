# Cards+ adaptations — pack 1

Owner selected adaptations on 2026-10-18. This release covers the 20 Cards+ concepts, not the full Booga profile. Enable Settings > Mods > Bundled adaptations > Cards+ adaptations, then restart. Off by default. All player-hosted peers must select the same pack and external files. Dedicated servers remain vanilla.

Original concepts: willis81808 (Erick Willis), CardsPlus 1.8.12 in the supplied profile. Reference: https://thunderstore.io/c/rounds/p/willis81808/CardsPlus/ and https://github.com/willis81808/CardsPlus . Repository license observed 2026-10-18: MIT; full notice ships as Resources/CardsPlus-LICENSE.txt and is copied into the local Mods folder. No original DLLs, artwork or implementation code imported.

These are substantial adaptations with original Chip Arena names/descriptions. The byline credits the source concept author, not authorship of this implementation. No claim of original-author endorsement.

| Source card | Chip Arena adaptation | Implemented behavior |
|---|---|---|
| Hare | Hare Drive | +20% movement speed; reloads take 20% less time. |
| Turtle | Turtle Guard | Blocks recharge 30% faster; movement speed is reduced by 10%. |
| Terminator | Breach Volley | +2 projectiles per volley; each deals 25% less damage. Projectile weapons only. |
| Slow Poke | Heavy Payload | +40% damage; 10% less movement speed and 20% slower projectiles. |
| Phantom | Phase Reserve | A successful block grants 0.6 s damage protection. No wall phasing. |
| Low Gravity | Lunar Frame | 35% less gravity and 10% greater jump launch speed. |
| Quick Reflexes | Reflex Buffer | +0.15 s manual block window; blocks recharge 20% slower. Does not auto-block. |
| Snake Attack | Drone Nest | Direct impacts summon a combat toy for 6 s, up to 3 toys per owner. |
| Excalibur | Escort Array | Two orbiting satellites add homing shots to your volleys at 40% damage. No recursive chip procs. |
| Hot Potato | Ember Footprint | Dodging leaves a 3 m fire field for 3 s, dealing 15 damage per second. |
| Smoke Grenade | Suppression Cloud | Direct impacts leave a 4 m time field for 4 s. Enemy shots move at 20% speed; pilots at 60%. No visual smoke. |
| Adware | Packet Theft | Direct hits steal one loaded round from gun users, or drain 8 mana/stamina from other pilots. |
| Short Circuit | Arc Fault | Every fourth direct hit adds an 8-damage, 3 m area pulse using the existing static-hit rules. |
| Hamper | Cryo Snare | Direct impacts create a 3.5 m frost field for 3 s: hostile pilots move at 45% speed; field deals 4 DPS. |
| Burnout | Finish Protocol | Direct hits deal 40% more damage to targets already below 30% health. |
| Contagion | Toxic Firmware | Direct hits apply 4 poison DPS for 4 s. Refreshes on hit; does not spread between targets. |
| Reboot Optics | Disruption Pulse | A successful block freezes nearby non-boss pilots for 1.2 s within 5 m and creates a 3 s time field. Cover blocks the freeze. |
| RAM Upgrade | Reserve Bank | +25 maximum mana and +20 maximum stamina. |
| Overclock | Fast Cache | +25% mana and stamina regeneration. |
| Cyber Psychosis | Redline Kernel | +25% damage, +15% attack rate and +10% movement speed; take 20% more damage. |

## Deliberate differences

- No ROUNDS quickhack menu, RAM casting loop or class prerequisite chain. The eight cyber-themed cards are independent draftable chips; mana/stamina and existing hit/block mechanics replace the original active quickhacks.
- Phase Reserve protects against ordinary damage after a successful block; it does not disable colliders or let projectiles pass through walls. Forced damage retains existing rules.
- Reflex Buffer is manual blocking. Disruption Pulse uses the existing block freeze instead of blinding human players. Suppression Cloud uses a time field instead of hiding vision.
- Drone Nest summons existing toy minions; Escort Array uses existing satellite projectiles, not snakes or sword assets. Ember Footprint triggers on dodge, not every movement step. Toxic Firmware does not spread poison between enemies.
- Existing effects retain their PvE limits: resource theft affects pilots; frost/time fields slow pilots (time also slows hostile projectiles), not zombie locomotion; direct-hit poison, execution, static pulses, fire fields, satellites and summons retain existing PvE handlers. Boss freeze immunity is preserved.
- Aliased mechanics exclude their existing vanilla counterparts in both pick directions. Existing proc, collision, friendly-fire, shared summon/projectile limits and no-recursion rules apply. No new unbounded spawn loop.

## Attribution and compatibility

ContentChip now carries optional author/sourcePack/sourceCard. External schema-1 packs may set author on the pack or per chip; chip author wins. Credit accepts at most 48 characters, no controls or angle brackets. Existing packs need no edits. Bottom-left credit has its own footer row; full card description includes the source card when supplied.

Bundled pack is trusted compiled content, outside the 64 external-chip budget. Schema 1 still rejects scripted mechanics. Fixed aliases are only for this catalog; files cannot claim arbitrary built-in mechanics. Every bundled definition and pack version is hashed into contentRevision. Native data export is unchanged.

## Verification and remaining work

Managed definition/eligibility/alias/stats/serialization tests and Unity semantic compilation are run for this change. A Windows gate exercises real Unity attribution serialization and authority block/fire/field/summon/satellite hooks. Windows execution, rendered byline placement, balance and human multiplayer require the builder/playtest; compilation alone is not gameplay acceptance.

Remaining Booga packs are not implemented by this release. Continue per-pack license and exact-mechanic review before adapting WWC, CPC, Cosmic Rounds, PCE, GearUP, Root, Unstable Cards and the rest. Claude owns any new bespoke visuals; this pass uses existing effect presentation and adds only the requested functional byline.
