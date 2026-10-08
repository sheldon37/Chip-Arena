# Committed melee, shared stamina and dodge rolls

Standard melee keeps the supplied reference phrases: dagger turns and low cuts, the sword's three-cut sequence, the rising two-handed greatsword chop and the hip-driven shield slam. Running stops through windup, contact and recovery. Authored, collision-tested steps still advance the player. No running, jumping, roll or block cancel bypasses that commitment. Hit interruptions cancel the action.

| Weapon | Load | Basic stamina | Special stamina | Hit stun | Base roll distance | Roll duration | Roll stamina |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Daggers | 2 | 24 | 30 | 0.196 s | 5.84 m | 0.425 s | 22 |
| Sword | 5 | 30 | 38 | 0.250 s | 5.30 m | 0.463 s | 24 |
| Shield | 8 | 36 | 45 | 0.304 s | 4.76 m | 0.500 s | 26 |
| Greatsword | 12 | 44 | 55 | 0.376 s | 4.04 m | 0.550 s | 29 |

Load is a gameplay unit. Basic attack costs are 20 + 2 × load. Special costs are 125% of basic costs, rounded up. Existing resource and echo-strike modifiers still apply. Standard hits cannot refresh active stun and grant 0.75 seconds of stagger immunity after recovery; damage still applies. Shield knockdowns, the one grounded pickup, bounded aerial follow-ups and greatsword direct-hit ragdolls remain.

All classes display stamina. Shift starts a grounded, direction-locked dodge roll with a fresh press. Running does not add to its distance. Base invincibility starts 75 ms into the roll and lasts 180 ms. Both startup and recovery can be punished. DoTs already ticking, overtime and falls remain active. Rolls consume stamina, pause its regeneration through recovery and keep collision with cover. Existing dash-triggered Chips activate on rolls.

Ansatsuken retains its existing Demon Step and developer attack behavior. Emperor's Shift and E use the shared dodge roll; its attacks, electric steps, Storm Footwork and defensive Chip effects remain. Other standard magic/ranged weapons also roll according to their load.

New Chips: Long Roll, Quick Tuck, Efficient Roll, Ghost Timing, Tumble Engine and Reserve Cell. Marathon Cell is now available to every class. Roll-specific Chips do not appear in Ansatsuken drafts. Invincibility upgrades are capped at 60% of the roll and always leave exposed startup and recovery.

The HUD shows stamina, roll cost, distance and cooldown. The Builds weapon tooltip exposes load and actual roll values. Movement rules, action timestamps and stamina authorization are shared with client prediction and serialized snapshots. The desktop update bundle and hosted browser build use the same gameplay source.

Validation: 144 focused combat, block, knockdown, prediction, network-motion, developer-style and animation checks passed before the final visual clearance adjustment. Offline articulated-rig review checked the tumble, weapon carry and planted melee movement. Browser playtesting was unavailable. The final 22 targeted combat/animation checks passed after the clearance correction. Production build and desktop packaging run during publishing.
