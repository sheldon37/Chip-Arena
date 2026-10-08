# Chip Arena — design intent, not an implementation checklist

This records owner preferences from prior development requests. It does not
certify what exists in the current build and does not authorize implementing
unfinished requests during migration. Inspect code and test before assigning
working/partial/missing status. Current explicit owner instructions take priority.

## Identity
A 3D voxel arena game inspired by round-based modifier drafting. Players select
computer Chips rather than cards. Ranged, Magic, and Melee are the requested core
class categories with weapon/element specialties. Preserve existing multiplayer
and bot support and the current match flow.

## Chips and arena
Favor distinctive, interacting mechanics and chaotic combinations over a pool
dominated by small stat bonuses. Prior examples include impact-created black
holes and a Necronomicon-style zombie-summoning modifier. Preserve actual
implemented scaling; do not invent formulas from these examples.
Destructible objects and physics-based damage are part of the intended experience.
Chip selection should communicate whose turn it is; higher rarities should have
recognizably stronger presentation without making normal play unreadable.

## Combat and animation
Attacks should communicate weight through anticipation, planted feet, footwork,
hip/torso rotation, impact, follow-through, and recovery. Heavy greatsword attacks
must not reduce to a lightweight sideways arm swing. Blocking should visibly use
the equipped melee weapon. Hit reactions should transition into the reaction,
not instantly snap to a still pose. Preserve combat timing and network behavior
unless the requested task explicitly changes them.

## Presentation and multiplayer requests
Owner requests have included weapon intros and an override option, customization,
distinct team identification including intros, dead-player spectating with player
cycling, and return-to-lobby with a countdown. These are requirements/preferences
to reconcile against source, not a claim that every item is complete.
Keep developer-only functionality access-controlled. Never copy credentials from
old prompts into documentation, source, release artifacts, or client-side checks.

## Migration boundary
Preserve the actual game. Do not import Embertrail systems, redesign its rules,
change engines, optimize unrelated mechanics, or regenerate art during migration.
