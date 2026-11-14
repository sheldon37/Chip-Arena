# Additional ROUNDS pack adaptations — batch 2

118 new chips across eight independently selectable packs; 138 including Cards+. **This is a partial implementation of the rest of Booga, not completion of the profile.** Off by default; restart required; exact enabled content is part of multiplayer compatibility. Lower-left credit names the original concept author; Chip Arena implementation, names and descriptions are new. No DLL, third-party code or art was copied.

## Source evidence and scope

Source repositories were inspected at the commits below on 2026-10-18. Declared source versions differ from the installed profile in several cases. Those mechanics are references for adaptations, not claims of exact-version parity. Config rows alone do not prove a card still exists in the installed runtime.

| Pack | New chips | Profile version | Declared source version | Inspected source |
|---|---:|---|---|---|
| vecs | 12 | 1.2.1 | 1.2.0 | https://github.com/Virepri/VECsPlugin/tree/7e39b035522f5492da452e16efc8e2f34aeb577e |
| rsc | 15 | 1.7.5 | 1.7.5 | https://github.com/RS-Mind/rscards/tree/69c032efd04026b17fb62b4e6f4490921be5bb14 |
| arcana | 13 | 1.9.6 | 1.9.5 | https://github.com/willis81808/Arcana/tree/e819563beb0dfe92af823e424b9a9130f8be5bee |
| bsc | 28 | 2.2.3 | 2.2.3 | https://github.com/tddebart/BossSlothsMods/tree/49a71f492f921928dae4804756ff021fcfd86fc2 |
| keys | 17 | 2.2.0 | 0.8.0 | https://github.com/KeysRounds/KeysCards/tree/69ddffb6ff1fe9c00d3e93e0b8081e5451a52819 |
| pce | 10 | 2.7.11 | 2.7.11 | https://github.com/pdcook/PCE/tree/60a8918e7483febf9a5ac3da508ddb60b9d9e406 |
| wwc | 13 | 1.12.1 | 1.12.01 | https://github.com/willuwontu/wills-wacky-cards/tree/3902394e6eeb968f81567dd754cd31885bfd6b56 |
| cpc | 10 | 1.4.3 | 1.4.2 | https://github.com/poppycars22/ChaosPoppycarsCards/tree/af7680b32106b5fe0e978b216c6eb93a9bcccf6f |

VECs covers its 12 saved concepts, with substantial documented substitutions. RSC has 15 adaptations; Nothing Field remains unverified, and Repentance/Repentence is one concept. Arcana leaves The Fool, Temperance and Wheel of Fortune pending because draft/score/shop mechanics are not implemented. Other seven pack toggles are explicitly marked partial. BSC card-copy/theft, dynamic airborne/charge bonuses and custom armor; Keys treasure/transform progression; PCE draft grants/control changes; WWC curse/null/pick systems; CPC classes/crafting/curses remain pending.

Other profile families (CR, BudsCards, BPP, GearUP, Root modules, Supcom2Cards, ZOMC, UnstableCards, FFC, Tesseract, SCME, Luck) are **not implemented by this batch**. Their saved names are not sufficient mechanical specifications. Some public pages provide only names; exact installed plugin files or source release links are still needed for version verification. Framework/utilities among the 129 packages are not chip packs.

## Gameplay contract

- Uses trusted bundled definitions only. External packs cannot use reserved `rounds-` IDs to impersonate built-in mechanics. External schema stays stat-only.
- IDs and pack selection order are deterministic. Unknown/duplicate selections fail closed; loading errors prevent hosting/joining, including bundled-only configurations.
- Mechanic aliases share mutual draft exclusions across all packs and vanilla counterparts. They never grant a proc until the corresponding chip is held with a positive count.
- Existing 512-chip catalog/64-external-chip limits remain. All bundled chips plus the full external allowance fit.
- Magazine/reload-focused adaptations use an internal gun tag and are not offered to ammo-less bows.
- Magazine capacity is a multiplier; thorns is a fraction of received direct damage; regeneration starts after 3 seconds without damage. Added projectile volleys retain the existing 0.75 multi-projectile damage factor as well as chip damage stats.
- Air jumps share the existing authority/prediction budget and six-extra-jump cap; no new movement replication protocol.
- Field effects follow existing pilot/prop/minion support and boss immunity rules. These adaptations do not add enemy support that the underlying native mechanic lacks.

## Adaptation mapping

| Pack | Source concept | Chip Arena chip | Implemented behavior / differences |
|---|---|---|---|
| vecs | Anti-Gravity Wells | Repulsor Well | Direct impacts leave a 3.5 m repulsion field for 3 s, pushing hostile pilots and movable props. |
| vecs | Gravity Wells | Attractor Well | Direct impacts create a 5 m gravity field for 3.5 s: 10 DPS and inward pull. Boss pilots resist the pull. |
| vecs | Bigger Magazine | Double Reservoir | Double magazine capacity; reloads take twice as long. |
| vecs | Blood Magic | Bloodguard Circuit | Blocks recharge 40% faster; take 15% more damage. Replaces health-paid emergency blocking. |
| vecs | Capture | Position Exchange | Direct hits swap positions with a hostile pilot when both destinations are clear. 4 s cooldown; no block priming. |
| vecs | Consume | Absorption Buffer | Successful blocks restore 12 health, capped at maximum health. Healing is fixed instead of scaling with the caught bullet. |
| vecs | Extra Magazine | Reserve Loader | Reloads take 25% less time; magazine capacity is reduced by 15%. Replaces alternating fast and slow magazines. |
| vecs | Inner Peace | Settled Aim | +40% damage; movement speed reduced by 15%. Always active: replaces the stationary magazine charge. |
| vecs | Mag of Holding | Deep Reservoir | +50% magazine capacity; reloads take 35% longer. Capacity stays fixed instead of shrinking after empty reloads. |
| vecs | Panic! | Emergency Vent | Once per life below 30% health: heal 30%, gain 2 s protection, reset block/dodge, and emit a 6 m repulsion field for 4 s. Replaces low-health fire-rate scaling. |
| vecs | Teleporter | Impact Transit | Direct hits on props blink you toward impact, up to 12 m, respecting walls; arrival deals a 3 m blast. 1.2 s cooldown. No block priming. |
| vecs | Tennis | Return Amplifier | Reflected shots deal 50% more damage and travel 25% faster. Replaces saving blocked damage for the next shot. |
| rsc | Bounce Absorption | Bounce Converter | +75% damage and +40% projectile speed; removes two bounces, down to zero. |
| rsc | Changeup | Changeup Drive | +35% attack rate; projectiles travel 30% slower. Gravity remains weapon-dependent. |
| rsc | Harming Field | Guard Discharge | Successful blocks emit a 3 m, 18-damage pulse. Blocks recharge 15% slower. |
| rsc | Hitscan | Velocity Rail | Projectiles travel 3x faster with no gravity; attack rate reduced by 25%. Still simulated projectiles, not instant hits. |
| rsc | Mortar | Guided Payload | +25% damage and +0.25 homing strength; projectiles ignore gravity. Reloads take 20% longer. Tracks targets instead of cursor redirection. |
| rsc | Open Chamber | Open Feed | Reloads take 20% less time; +20% attack rate and 15% slower projectiles. |
| rsc | Rebound | Rebound Node | +1 projectile bounce. |
| rsc | Reckless Attack | Reckless Driver | +60% attack rate; reloads take 15% less time; damage reduced by 20%. |
| rsc | Reflection Accelerant | Ricochet Booster | +35% damage, +25% projectile speed and +2 bounces. Speed boost applies at launch instead of each bounce. |
| rsc | Regenerator | Repair Stream | Regenerate 4 health per second after avoiding damage for 3 seconds. |
| rsc | Repentance | Restoration Frame | +50% maximum health; lifesteal reduced by half. |
| rsc | Slug | Dense Slug | +60% damage; projectiles travel 25% slower. |
| rsc | Spare Shotgun | Scatter Reserve | +3 projectiles per volley with a 16-degree fan; damage stat reduced by 45%. Replaces weapon swapping. |
| rsc | Split | Split Feed | +25% magazine capacity; reloads take 20% less time; damage reduced by 20%. |
| rsc | Twin Scythe | Twin Escort | Two orbiting satellites add homing shots at 40% damage. Replaces contact scythes; satellites do not destroy enemy bullets. |
| arcana | The Chariot | Charge Discharge | Successful blocks emit a 3 m pulse: 22 damage and 500 impulse. Replaces the directional charge. |
| arcana | Death | Reclamation Core | Once per life, survive a lethal hit or escape below 25% health: restore up to 40 HP, blink back and gain 1.2 s protection. Replaces extra lives and revival growth. |
| arcana | The Devil | Soulfire Driver | +30% damage, +50% outgoing impulse; direct hits apply 3 burn DPS for 3 s. Replaces the original unholy projectile effect. |
| arcana | The Empress | Thornwake | Dodging leaves a 3 m damaging field for 3 s at 15 DPS. Uses fire damage instead of movement-spawned slowing thorns. |
| arcana | The Hanged Man | Temporal Sacrifice | Direct impacts leave a 4 m time field for 4 s: hostile shots at 20% speed and pilots at 60%. Take 15% more damage. No self-kill or permanent slow. |
| arcana | The Hermit | Hermit Frame | +35% damage and +20% maximum health; movement speed reduced by 15%. Always active instead of stacking while stationary. |
| arcana | The Hierophant | Binding Lattice | Direct impacts create a 3.5 m frost field for 3 s: 4 DPS; hostile pilots move at 45% speed. Replaces chain restraints. |
| arcana | Justice | Retribution Frame | +25% maximum health; return 15% of direct damage received to the attacker. Replaces maximum-health-scaled retaliation. |
| arcana | The Lovers | Pacifier Guard | +0.2 s manual block window; successful blocks restore 8 health. Replaces passive chance to erase nearby bullets. |
| arcana | The Magician | Manifest Array | Two satellites add homing shots to projectile volleys at 40% damage. Replaces block-triggered missiles. |
| arcana | The Moon | Lunar Interdict | Successful blocks freeze visible non-boss pilots within 5 m for 1.2 s and leave a 3 s time field. Replaces the lunar beam. |
| arcana | The Sun | Radiant Fan | +2 projectiles in a 12-degree fan and +1 bounce; damage stat reduced by 25%. Replaces radiant beams. |
| arcana | The Tower | Storm Relay | Direct impacts create a 4 m storm field for 3 s dealing 18 DPS. Replaces chain lightning. |
| bsc | Double jump | Sloth Jump Drive | +1 extra air jump and +20% maximum health. |
| bsc | Elite sneakers | Sprint Soles | +25% movement speed and +10% jump launch speed. |
| bsc | Bolt action | Bolt Driver | +90% damage; attack rate reduced by 45%; reloads take 20% longer. |
| bsc | Boing | Boing Coil | +1 bounce and +15% damage. Bounces use solid geometry; no screen-edge reflection. |
| bsc | Compacted shot | Compacted Volley | +2 projectiles, damage stat reduced by 15%; reloads take 15% longer. |
| bsc | Drum magazine | Gravity Drum | +50% magazine capacity; +30% gravity and 15% less damage. Gravity penalty is fixed instead of scaling with ammo. |
| bsc | Foldable stock | Folding Receiver | +20% damage, +25% magazine capacity; projectiles 25% slower and reloads 20% longer. |
| bsc | Futuristic stock | Chrome Receiver | +35% health, +25% damage, +25% magazine capacity; reloads take 25% longer. |
| bsc | Get over here | Grapple Driver | Direct hits pull hostile non-boss pilots toward you. No effect on their teleport position. |
| bsc | Attract | Vacuum Driver | Direct impacts pull visible pilots and movable props within 5 m using 700 impulse. One pulse per attack. |
| bsc | Repel | Repel Driver | +50% outgoing impulse; reloads take 10% longer. |
| bsc | Higher caliber | High Caliber Core | +30% damage, +50% outgoing impulse; movement speed reduced by 10%. |
| bsc | Le monk | Heavy Monk Frame | +40% maximum health; movement speed reduced by 20%. |
| bsc | Long fall boots | Longfall Springs | +25% jump launch speed, +15% gravity, 15% less maximum health and 20% less debris damage. No special wall-impact immunity. |
| bsc | Morning coffee | Caffeine Drive | +35% movement speed; maximum health reduced by 20%. Bonus is constant instead of draining while moving. |
| bsc | Omega | Final Cartridge | The last round deals 50% extra damage; reloads take 20% longer. |
| bsc | Overclocked flywheels | Flywheel Drive | +50% projectile speed; reloads take 10% longer. Bonus is constant instead of growing as the magazine empties. |
| bsc | Rolling thunder | Thunder Delivery | Direct impacts call five staggered blasts dealing 26 damage each. 1.5 s cooldown; reloads take 25% longer. |
| bsc | Sawblade bullets | Sawtooth Rounds | +40% damage and +2 bounces; projectile speed halved and magazine capacity reduced by 20%. Uses solid geometry rather than screen edges. |
| bsc | Sluggish rounds | Slow Ricochet | +15% damage and +4 bounces; projectile speed reduced to 40% and gravity increased by 20%. No screen-edge bounce. |
| bsc | Spread the love | Wide Embrace | +4 projectiles in a 24-degree fan; damage stat reduced by 45%. |
| bsc | Sneeze | Sneeze Array | +6 projectiles in a 35-degree fan, +50% magazine capacity; damage reduced by 70% and reloads take 60% longer. Projectile collision rules unchanged. |
| bsc | Thorns | Thorn Plating | Return 20% of direct damage received to the attacker. |
| bsc | Whale | Whale Frame | Double maximum health; movement speed reduced by 35% and attack rate by 25%. Capsule size stays unchanged. |
| bsc | Wooden stock | Timber Receiver | +15% movement speed and +25% magazine capacity; reloads take 10% longer. |
| bsc | Yeetus | Launch Amplifier | Double outgoing impulse. |
| bsc | Yin | Yin Receiver | +30% damage and +25% magazine capacity; reloads take 20% less time and attack rate is reduced by 20%. |
| bsc | Yin Yang | Balance Receiver | +15% damage, +20% health, +25% magazine capacity and +1 projectile. Blocks and reloads take 35% longer; replaces extra blocks. |
| keys | Angelic | Angelic Thrusters | +1 air jump, +10% movement speed and damage; projectile gravity reduced by 35%. |
| keys | Bottomless Pockets | Pocket Reservoir | +40% damage and +40% magazine capacity; movement speed reduced by 5%. |
| keys | Boxing Glove | Impact Glove | +25% damage and +50% impulse; projectile speed reduced by 20%. |
| keys | Crimson Potion | Crimson Guard | Successful blocks restore 15 health. Blocks recharge 25% slower. Does not cleanse damage-over-time effects. |
| keys | Giants Gauntlet | Giant Repair Frame | +50% maximum health; regenerate 4 HP/s after avoiding damage for 3 s. Regeneration is fixed instead of percentage-based. |
| keys | Grovetenders Shield | Grove Guard | +30% maximum health and +0.15 s block window; blocks recharge 15% slower. Replaces two extra automatic blocks. |
| keys | Iron Carapace | Iron Shell | +40% maximum health; blocks recharge 20% faster. Standalone chip; no transformation or extra block. |
| keys | Iron Heart | Iron Heart Drive | +25% health, +15% speed, +2 air jumps and 3 HP/s regeneration after 3 s without damage. Standalone chip. |
| keys | Iron Pincers | Iron Pincer Array | +35% damage, +20% attack rate, +30% projectile speed and +25% magazine capacity. Standalone chip. |
| keys | Mending Reload | Mending Loader | Completed reloads restore 10 health, capped at maximum health. Replaces percentage-based healing. |
| keys | Mirror | Mirror Frame | Blocks recharge twice as fast; maximum health reduced by 30%. |
| keys | Moonwalker | Moonwalker Drive | Player and projectile gravity reduced by 65%; +20% jump power and damage; movement and projectile speed reduced by 20%. |
| keys | New Sneakers | Fresh Soles | +20% movement speed and attack rate. |
| keys | Nimble | Nimble Drive | +15% movement speed and attack rate; reloads take 15% less time. |
| keys | Raid Boss | Raid Frame | Double maximum health; gravity reduced by 35%, movement by 35%, attack rate by 20%. No capsule enlargement. |
| keys | The Eggs Blessing | Hatchling Blessing | +10% maximum health and damage. Standalone chip; no egg timer. |
| keys | Zephyrs Blessing | Zephyr Thrusters | +3 air jumps, +20% movement speed and 30% less gravity. Attack rate is unchanged. |
| pce | Ant | Ant Drive | +70% damage and +20% movement speed; maximum health reduced by 40% and jump power by 15%. No size change or stomping. |
| pce | Moon Shoes | Moon Soles | Player gravity reduced by 65%. Map bounds remain unchanged. |
| pce | Jetpack | Burst Jetpack | +4 air jumps; movement speed reduced by 15%. Separate jump presses replace continuous thrust and fuel. |
| pce | Old Jetpack | Sputter Thrusters | +2 air jumps; jump launch speed reduced by 15%. Separate jump presses replace continuous thrust. |
| pce | Super Jump | Super Jump Coil | +40% jump launch speed; jump is immediate instead of charged by crouching. |
| pce | Straight Shot | Straight Driver | Projectile gravity removed. |
| pce | Tractor Beam | Tractor Hook | Direct hits pull hostile non-boss pilots toward you. |
| pce | Fragmentation | Fragment Driver | Direct impacts emit a 1.8 m blast dealing 10 damage with outward impulse. One blast per attack; replaces fragment projectiles. |
| pce | Mulligan | Mulligan Reserve | Once per life, survive a lethal hit or escape below 25% health: restore up to 40 HP, blink back and gain 1.2 s protection. |
| pce | Super Ball | Accelerant Ball | +3 bounces and +40% projectile speed; damage reduced by 20%. Speed applies at launch instead of growing on each bounce. |
| wwc | Ammo Cache | Ammo Cache Bank | +75% magazine capacity; reloads take 10% less time; movement speed reduced by 20%. |
| wwc | Boomerang | Return Driver | Shots return and can refund one round when caught. +35% damage; attack rate halved. No projectile gravity. |
| wwc | Bounce | Bounce Loop | +1 projectile bounce on solid geometry. Screen-edge wrapping is not used. |
| wwc | Boots Of Leaping | Leaping Soles | +35% jump launch speed. Weapon spread remains unchanged. |
| wwc | Running Shoes | Runner Soles | +35% movement speed; multi-projectile volleys fan 6 degrees wider. |
| wwc | Aggressive Venting | Vent Accelerator | +60% attack rate; reloads take 15% longer. Does not emit a heat aura. |
| wwc | Gatling Gun | Gatling Feed | +80% magazine capacity; damage reduced by 35%, attack rate by 20%; +18 degrees spread. Reloads take 25% longer. |
| wwc | Shotgun | Double Scatter | +3 projectiles in a 20-degree fan, +40% magazine capacity and +30% projectile speed. Damage reduced by 40%; attack rate halved. |
| wwc | Vampirism | Vampiric Drive | +15% lifesteal, +30% damage, +15% movement and +10% jump power; gravity reduced by 30%. |
| wwc | Wild Aim | Wild Driver | +30% damage and +1 projectile in a 35-degree fan. Replaces randomly scattered single shots. |
| wwc | Strong Body | Strong Frame | +20% health and movement speed. Status-effect duration is unchanged. |
| wwc | Dimensional Shuffle | Dimensional Exchange | Direct hits swap positions with hostile pilots when both destinations are clear. 4 s cooldown; replaces block-triggered global shuffle. |
| wwc | Banishment | Shadow Interdict | Successful blocks freeze visible non-boss pilots within 5 m for 1.2 s and leave a 3 s time field. Replaces silence and shadow banishment. |
| cpc | Healing block | Healing Guard | Successful blocks restore 20 health; blocks recharge 10% faster. |
| cpc | Ammo Chest | Ammo Vault | Double magazine capacity; reloads take 50% longer. |
| cpc | Balloon Bullets | Balloon Payload | +60% projectile radius; damage reduced by 25%. |
| cpc | Duplicator | Duplicate Array | +1 projectile per volley and +8 degrees spread; damage reduced by 25%. Adds one instead of doubling existing projectile count. |
| cpc | Double Duplicator | Quad Array | +3 projectiles per volley and +16 degrees spread; damage halved. Adds three instead of quadrupling existing projectile count. |
| cpc | Plant-like bullets | Growing Ricochet | +3 bounces and +25% damage. Damage applies at launch instead of increasing on each bounce. |
| cpc | Infectious Bullets | Infection Payload | Direct hits apply 5 poison DPS for 4 s. Refreshes on hit; does not spread to other targets. |
| cpc | Nano Machines | Nano Repair | +15% maximum health; regenerate 5 HP/s after 3 s without damage. Rate is fixed instead of scaling with missing health. |
| cpc | Red Velvet Cake | Velvet Repair | +30% health, +10% lifesteal and 3 HP/s regeneration after 3 s without damage. |
| cpc | Sentry Gun | Sentry Array | Two satellites add homing shots to volleys at 40% damage. Replaces the stationary block-placed sentry. |
