import type {Chip} from './content.js';
const chip=(id:string,name:string,description:string,icon:string):Chip=>({id:'l44t-'+id,name:'L44T '+name,rarity:'l44t',tags:['leet'],max:1,icon,mechanic:'l44t-'+id,effects:[],description});
export const LEET_CHIPS:Chip[]=[
 chip('brain','Brain Steal','Replaces Block with a possession orb. Hit an enemy to control their movement and attacks for 5 seconds. Your own body stays vulnerable.','◎'),
 chip('scare','Scare Tactics','E summons a haunted tree. Slenderman stalks the nearest enemy: he only moves when unseen, teleporting closer behind them. Staring at him fills their screen with static. If he reaches them or they stare too long, a fullscreen jumpscare ends in an instant knockout. He has 160 HP.','♜'),
 chip('nuke','Nuke','Projectile impacts mark the area with green sparks, then unleash a massive explosion that hurls nearby cover away.','☢'),
 chip('yeet','Y33t','Hits launch enemies and cover with a thousand times the knockback. Goodbye, arena.','⇈'),
 chip('time','Admin Pause','E freezes every enemy and their projectiles for 4 seconds. You keep moving.','Ⅱ'),
 chip('undo','CTRL + Z','Once per round, a lethal hit rewinds you to an earlier safe position and completely restores your health.','↶'),
 chip('copy','Copy / Paste','Hits print three delayed attack orbs above the impact. They fire at the original target position like a matrix firing squad.','▥'),
 chip('uprising','Object Uprising','Projectile impacts commandeer nearby loose cover and launch it at a visible enemy. The map works for you now.','⌗'),
];
/** Relative tier weights before exhausted/incompatible tiers are removed. */
export const LEET_WEIGHTS={mishima:0,ansatsuken:0,siren:0,common:0,uncommon:0,rare:4,epic:11,legendary:30,mythic:30,l44t:25};
/** Scare Tactics' Slenderman (2026-10-04 rework, owner: "operate EXACTLY like Slenderman"). He never moves while his target
 *  looks at him (fov half-angle in degrees, sight in m); staring builds sanity (stare + close × nearness within closeRange, per s)
 *  and it decays when the target looks away. Unobserved for `unseen` s and off his hop cooldown (hop[0] → hop[1] s as the 22 s
 *  life runs out) he teleports behind the target, `shrink` × the gap (0.72 → 0.5 with aggression). reach or sanity 1: the
 *  jumpscare knockout. Counterplay: look away (static fades), keep distance, destroy him (hp, every damage path) or the owner. */
export const SLENDER={life:22,emerge:.9,hp:160,center:1.23,half:.95,radius:.28,fov:36,sight:46,stare:.09,close:.5,closeRange:14,decay:.3,
 unseen:.35,hopFirst:2.6,hop:[3.2,1.3],shrink:[.72,.5],behind:.85,reach:1.6} as const;
export const LEET={lockRange:28,lockPause:.42,shotLife:7,brainSeconds:5,brainCooldown:6,scareSeconds:2.2,nukeDelay:1.4,nukeRadius:11,nukeDamage:160,nukeImpulse:42000,freezeSeconds:4,secondaryCooldown:12,chipRevealSeconds:4.6} as const;

/** Flight/secondary replacements that the matrix staff deliberately owns. */
export const LEET_INCOMPATIBLE=new Set(['homing','boomerang','zigzag','orbitals','corkscrew','elevator-shot','stop-go','accelerating-shot','necronomicon']);
