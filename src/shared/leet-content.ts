import type {Chip} from './content.js';
const chip=(id:string,name:string,description:string,icon:string):Chip=>({id:'l44t-'+id,name:'L44T '+name,rarity:'l44t',tags:['leet'],max:1,icon,mechanic:'l44t-'+id,effects:[],description});
export const LEET_CHIPS:Chip[]=[
 chip('brain','Brain Steal','Replaces Block with a possession orb. Hit an enemy to control their movement and attacks for 5 seconds. Your own body stays vulnerable.','◎'),
 chip('scare','Scare Tactics','E summons a haunted tree. Slenderman emerges and stalks an enemy. If he reaches them, a fullscreen jumpscare ends in an instant knockout.','♜'),
 chip('nuke','Nuke','Projectile impacts mark the area with green sparks, then unleash a massive explosion that hurls nearby cover away.','☢'),
 chip('yeet','Y33t','Hits launch enemies and cover with a thousand times the knockback. Goodbye, arena.','⇈'),
 chip('time','Admin Pause','E freezes every enemy and their projectiles for 4 seconds. You keep moving.','Ⅱ'),
 chip('undo','CTRL + Z','Once per round, a lethal hit rewinds you to an earlier safe position and completely restores your health.','↶'),
 chip('copy','Copy / Paste','Hits print three delayed attack orbs above the impact. They fire at the original target position like a matrix firing squad.','▥'),
 chip('uprising','Object Uprising','Projectile impacts commandeer nearby loose cover and launch it at a visible enemy. The map works for you now.','⌗'),
];
/** Relative tier weights before exhausted/incompatible tiers are removed. */
export const LEET_WEIGHTS={mishima:0,ansatsuken:0,siren:0,common:0,uncommon:0,rare:4,epic:11,legendary:30,mythic:30,l44t:25};
export const LEET={lockRange:28,lockPause:.42,shotLife:7,brainSeconds:5,brainCooldown:6,scareSeconds:2.2,nukeDelay:1.4,nukeRadius:11,nukeDamage:160,nukeImpulse:42000,freezeSeconds:4,secondaryCooldown:12,chipRevealSeconds:4.6} as const;

/** Flight/secondary replacements that the matrix staff deliberately owns. */
export const LEET_INCOMPATIBLE=new Set(['homing','boomerang','zigzag','orbitals','corkscrew','elevator-shot','stop-go','accelerating-shot','necronomicon']);
