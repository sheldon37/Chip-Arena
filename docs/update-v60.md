# Melee motion and impact update

- Shield: tucked preload, planted lead leg, rear-foot push and an outward slam. Existing knockback, hitstun and knockdown remain.
- Sword: cross cut, backhand and a raised-knee overhead finish with a low recovery. Lead-foot placement accounts for the attack's actual authored root travel.
- Greatsword: changed from overhead chops to the new reference's low rotating sweeps and rising recovery. Both attack directions use the matching wide gameplay sweep.
- Daggers: alternating lunges, a low third cut and a complete fourth-cut turn; separate lead-foot plants and rear-foot recovery.
- Blocked melee opens the attacker's guard, throws the weapon hand upward, then settles through the authoritative stun time.
- Hit reactions use the contact position and incoming direction. Head, torso, arms and legs react differently; off-center hits rotate the body. Camera aim stays responsive.

## Dagger tuning

| Setting | Before | Now |
| --- | --- | --- |
| Primary stamina | 24 | 16 |
| Throw stamina | 30 | 30 |
| Primary hitstun at base speed | 0.196 s | 0.593 s |
| Throw speed | 35 m/s | 30 m/s |
| Throw gravity | 3 m/s² | 18 m/s² |
| Maximum projectile travel | 48 m | 32 m |

Primary dagger stun scales with the attacker's swing cadence and includes the next swing's contact window. Later hits do not refresh it. The existing 0.75-second stagger protection after stun is preserved. This gives two swings time to connect while retaining an escape window.

Thrown daggers tumble six revolutions per second around their center and follow the shared ballistic integration used by other projectiles. Projectile speed/gravity modifiers still apply. Range falloff comes from the falling trajectory and finite travel: at base speed, a horizontal throw drops 1 m after 10 m of travel. Aim higher to reach farther; no additional damage falloff is applied.

Validation covers actual two-hit connections at four attack speeds, stagger protection, parry timing, hit-location reactions, snapshot timing, ballistic motion and spin, root/foot alignment, all combo floor clearances, and the existing combat/prediction/roll suites. Reference contact sheets and offline rig renders were inspected; live multiplayer browser playtesting was not available in this session.
