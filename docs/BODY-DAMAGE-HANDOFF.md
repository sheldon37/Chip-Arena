# Body damage and death presentation — Claude handoff

Owner request, 2026-10-01: Astra implements functions/authority/physics; Claude owns animation and visuals. Bullets should puncture, make larger cavities or remove the struck part according to impact strength. Blades cut or stab; blunt attacks crush. Impact ragdoll/gibs becomes the public default. A lethal fast headshot can remove the head or leave a large cavity, followed by knees → forward fall where physically appropriate.

## Branch and ownership

- Systems task: `codex/body-damage-systems`, based on shared `claude/visual-revision` at `a2480ba6985bb8a60efc10f79839eeb7d3dc9a27`.
- Claude: fetch this task branch and branch `claude/body-damage-visuals` from its latest head. Read `AGENTS.md`, recent `docs/DEV_LOG.md`, this file, then the contracts below. Do not replace the shared branch wholesale.
- Astra owns `src/shared/body-damage.ts`, `body-ragdoll.ts`, simulation integration, damage profiles and authority tests. Request contract changes in your handoff instead of independently reimplementing the resolver.
- Claude owns new client wound/crush/collapse presentation and may extend `src/client/ragdoll.ts`, `death-effects.ts`, `gibber-vfx.ts`, rig meshes and a visual lab. Preserve the small authority-contract/velocity adapters already present in those files.
- Systems verification is in progress at the first handoff push. Consult the newest DEV_LOG entry/PR for final results. This is not a completed visual release; no merge/publication yet.

## Stable contract

- `PlayerView.wounds?: BodyWound[]`: at most eight accepted physical impacts per life, oldest evicted. No wounds for rejected hits. Reset/heal clears them. Persistent snapshot state, not transient events.
- `DeathPresentation.wound?: BodyWound`: the fatal hit only. Absent for DOT, falls or other deaths without a physical impact descriptor. Earlier wounds remain in `PlayerView.wounds`. Do not invent a new head wound from the last attacker's weapon.
- `BodyWound`: unique `id`, authority `at`, `part`, `kind`, `trauma`, local `point`, `normal`, `direction`, radius in world game units, normalized severity, stylized energy/momentum/speed, optional `destroyedPart`.
- Part names match the existing rig: head, spine, left/right shoulder/elbow/hip/knee. A destroyed root owns its descendant subtree. Avoid duplicate forearms, hands, shins, weapons, hats and floating armor after severing.
- `point` and both vectors are **victim-local upright capsule coordinates**, calculated from the authority's facing at impact. They are NOT already bone-local coordinates. Map the selected part into the current animated rig, then keep wounds attached to that part through motion, ragdoll and severing. Verified head hits are clamped into the head height band. This is a coarse capsule region model, not a mesh-accurate hitbox rewrite.
- Wound kinds: `puncture`, `cavity`, `cut`, `bruise`, `sever`, `crush`, `scorch`. Trauma kinds distinguish bullet, cut, pierce, blunt, blast and energy. `sever`/`crush` and `destroyedPart` are lethal-only. Living wounds must not disable limbs or change HP/hitboxes.
- `DeathPresentation.motion?: DeathMotion`: `ragdoll` or `kneel-fall`, `kneesAt`, `releaseAt` relative to `death.at`, facing, forward direction and resulting world velocity. Use `bodyDeathPhase(motion, state.time - death.at)` from `body-ragdoll.ts`.
- Kneel timing is currently .32 s to knees and .82 s to release into physics. Gate: lethal bullet head cavity/sever, relative speed >=110, grounded, not rolling/knocked down/juggled, resulting speed <8. Airborne, tumbling and strongly launched victims ragdoll immediately. Do not force them upright.
- Authoritative HP reaches zero and gameplay control stops immediately. The brief collapse is visual; it does not delay scores, create damageable corpses or keep the victim alive.
- Default effect ID: **`impact-ragdoll`**, public, no DEV check. Missing/invalid cosmetics use it; explicitly saved cosmetic choices (including legacy voxel-burst) remain selected. Keep special finishers and Siren death/crush overrides intact. If requested to migrate old selected cosmetics, coordinate that separately.
- Existing fallback currently renders ordinary ragdoll and selected-part severing using old assets. It does NOT yet draw punctures/cavities, crushed fragments, living wounds or kneel/fall animation. Do not mistake the fallback for finished visuals.

## Visual work

1. Build readable voxel wounds. A puncture is a small entrance opening; a cavity is localized missing volume. Use geometry/masking appropriate to the existing renderer, not a large flat dark sticker. Do not infer exits/through-shots unless the contract supplies enough information; a wound is distinct from the Piercing Chip's gameplay behavior.
2. Give cuts and piercing distinct shapes. Severed parts should detach at the chosen joint/region with matching exposed caps, attached cosmetics and directional spray. Avoid exploding an entire character for a low-energy pistol kill. Crushing should destroy/deform the struck region into bounded fragments, visibly different from a clean blade cut.
3. Animate the gated headshot death: immediate impact/wound, brief loss of balance, knees, forward collapse, continuous handoff into the articulated ragdoll. Match the incoming pose and handoff transforms; no teleport, pop, resetting to an upright mannequin or detached head reappearing. Drive all phases from authority time, including late snapshots.
4. Preserve `bodyPartVelocity` integration. Death motion velocity already includes gameplay knockback; do not add `death.force` to the whole body again. The helper applies only a bounded extra impulse to a destroyed subtree. Do not alter gameplay knockback or collision shapes to improve animation.
5. Keep wounds attached during live animation and transfer them to the dead body. Deduplicate by wound/death ID. Snapshot absence and generation changes must clear them. Handle round reset, heal, rematch, despawn, late join, low quality, first person and spectating.
6. Respect existing render budgets, five-second death lifetime, disposal and reduced-quality behavior. Pool particles; no server corpse rigid bodies or damaging cosmetic gibs. Corpses colliding with nearby snapshot cover remain cosmetic.

## Verification and handoff back

Create a repeatable visual-lab scene for pistol puncture, base sniper cavity, faster sniper decapitation, limb cut, blade thrust, shield/debris crush, ordinary torso death, grounded kneel/fall, airborne headshot, custom finisher, and reduced quality. Inspect multiple camera angles and low/high frame rates. Test late snapshots, repeated snapshots, rewind/reset and disposal. Run affected tests/types and report actual failures separately from baseline failures; no invented Windows or GPU results.

Document changed client files, before/after evidence, any contract request, actual checks and remaining issues in DEV_LOG. Push your task branch and give the owner/Astra its exact SHA. Do not change damage, attack timing, cooldowns, hitboxes, default access rules or authority-selected outcomes. Do not auto-merge or publish this unfinished pair. Once integrated and verified with owner-authorized merge, use the existing standing test Update channel workflow.

## Model limits

Energy = .5 × effective mass × relative speed² × transfer × post-mitigation damage scale. Momentum is tracked separately. Profiles and thresholds are deliberate **fictional game tuning**, not anatomical/real-world ballistics. Projectiles use captured weapon identity/radius and impact velocity, including reflections. Debris uses existing finite mass and normal closing speed. Attacks do not gain extra gameplay damage from this presentation model. No living limb-health system, mesh-accurate collision, persistent corpse damage, penetration-depth simulation, or postmortem dismemberment is introduced.
