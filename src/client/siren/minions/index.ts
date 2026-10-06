/**
 * Siren minions façade (design phase, presentation only; see docs/SIREN-MINIONS.md). Nothing in the game imports this
 * yet: spawn rules, AI, damage and networking are next-phase systems work. The renderer side of that phase would:
 *  - prewarmSirenMinions(budgetMs) once per frame during the Siren intro until it returns true (caches every shared
 *    corrupted layout a seed can need, a few ms per frame),
 *  - createSirenMinion(view.seed) once per minion id (rebuild only if the seed changes), add rig.group to the scene,
 *  - every frame set rig.group.position to the minion's pos and rotation.y to its yaw, then animateSirenMinion(...)
 *    with the authoritative time (state.time, the same clock animateSiren gets: both share the Siren shader clock),
 *  - setSirenMinionDetail(minion, true) only on the SVG compatibility path (plain boxes lose the rot, burns and wounds;
 *    'performance' graphics keep full detail: the draw count is the same),
 *  - disposeSirenMinion(minion) when the minion leaves the snapshot after its death effects (MINION_TIMING.deathFx).
 * Never call recolorCharacterRig, dressRig, retagVoxelPart(mesh) resets, body-wounds carving/collapse or death-effects
 * voxel bursts on a minion rig: they would repaint the corrupted parts or revert them to the clean Pilot sculpt.
 */
export {createSirenMinion,prewarmSirenMinions,setSirenMinionDetail,disposeSirenMinion,sirenMinionStats,sirenMinionOf,categoryOf,type SirenMinion} from './model.js';
export {animateSirenMinion,attackKind,swipeArm,armless,stumpLeg,entryWalkStart,entryDuration,MINION_TIMING,VENT_RIM_Z,type SirenMinionView,type MinionAttack,type MinionEntry,type MinionEntryKind} from './motion.js';
export {applyLostLimbs,stumpsOf,activeCuts,LIMB_CUTS,LOSS_STUMPS} from './limbs.js';
export {minionLook,minionSeed,seededFace,MINION_FACES,DEFAULT_MINION_FACE,CRACKED_FACE_SHARE,type MinionLook,type MinionOptions,type MinionFace} from './look.js';
export {corruptedLayoutCount} from './layouts.js';
export {materialCount,MINION_PALETTE} from './materials.js';
