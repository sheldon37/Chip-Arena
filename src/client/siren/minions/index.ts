/**
 * Siren minions façade (design phase, presentation only; see docs/SIREN-MINIONS.md). Nothing in the game imports this
 * yet: spawn rules, AI, damage and networking are next-phase systems work. The renderer side of that phase would:
 *  - createSirenMinion(view.seed) once per minion id (rebuild only if the seed changes), add rig.group to the scene,
 *  - every frame set rig.group.position to the minion's pos and rotation.y to its yaw, then animateSirenMinion(...),
 *  - setSirenMinionDetail(minion, compatibility || performance graphics) on creation and when graphics change,
 *  - disposeSirenMinion(minion) when the minion leaves the snapshot after its death effects (MINION_TIMING.deathFx).
 * Never call recolorCharacterRig or dressRig on a minion rig (they would repaint the corrupted parts).
 */
export {createSirenMinion,setSirenMinionDetail,disposeSirenMinion,sirenMinionStats,sirenMinionOf,categoryOf,type SirenMinion} from './model.js';
export {animateSirenMinion,attackKind,MINION_TIMING,type SirenMinionView,type MinionAttack} from './motion.js';
export {minionLook,minionSeed,MINION_FACES,DEFAULT_MINION_FACE,type MinionLook,type MinionOptions,type MinionFace} from './look.js';
export {corruptedLayoutCount} from './layouts.js';
export {materialCount,MINION_PALETTE} from './materials.js';
