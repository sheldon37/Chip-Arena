# Combat, melee, and death announcements

- Green overhead and local health bars.
- Background death fireworks reveal a cyan hologram wearing the eliminated player's cosmetics, with launch, burst, and notification sounds. Simultaneous deaths queue. Text credits the last other player who dealt positive damage during that life; unassisted deaths say “made an oopsie.” Self-damage does not erase another player's credit. Names are captured by the authority.
- Standard sword: three-cut animation phrase and E rising slash with a brief aerial follow-up window.
- Daggers: alternating cross-body cuts and E single thrown dagger. Direct throws inherit melee damage modifiers, burn, poison, lifesteal, and direct-hit chip effects.
- Greatsword: two-handed overhead and diagonal cuts. E launches a somersault and damages nearby visible targets on actual landing, with dust, debris, and an expanding shockwave.
- Shield: primary slam causes knockback and hitstun. E hits once, knocks down, then returns. Return flight is harmless. The shield stays absent from the hand until caught, including during block cancellation.
- Existing grounded protection, one legs-up pickup, roll-away recovery, friendly-fire rules, and developer fighting styles are preserved.
- Uploaded `hitmarker_2.mp3` replaces the hit confirmation sound. The decoded WAV is mono, normalized to 0.7 peak, and preserves the supplied recording's timbre. Audio generation preserves it.
- White four-stroke hitmarker; red for authoritative projectile headshots with 1.5x direct damage. Burn and poison indicators show application and subsequent ticks. Splash, ordinary melee and shield throws do not earn headshot bonuses. No confirmation for props, self-damage, blocked hits, or teammates.

Validation: TypeScript and targeted Rapier combat, recovery, networking movement, developer-class, feedback, and presentation checks. Offline Three.js pose and hologram renders were inspected. No browser playtest was available in this environment. Two unrelated pre-existing tests assume a maximum of nine control labels (rifle now has ten) and 22050 Hz for every sound (electric-hook-hit is 44100 Hz).
