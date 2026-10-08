import type {Chip} from './content.js';
const chip=(id:string,name:string,description:string,icon:string):Chip=>({id:'arcade-'+id,name,rarity:'ansatsuken',tags:['ansatsuken'],max:1,icon,mechanic:'arcade-'+id,effects:[],description});
export const ARCADE_CHIPS:Chip[]=[
 chip('shinku','Shinku Hadoken','Hold 1 to charge. Release a full charge to fire a huge, piercing five-hit Hadoken.','◎'),
 chip('rush','Ten-Hit Rush','Your punch–punch–high-kick chain becomes a rapid ten-hit combo with a high-kick finisher.','10'),
 chip('red','Shakunetsu','Hadokens turn crimson and inflict a much stronger burn.','♨'),
 chip('cyclone','Hurricane Tatsu','Tatsumaki spins longer, pulls nearby enemies inward and lands more kicks.','⟳'),
 chip('rising','Shin Shoryuken','Shoryuken becomes three rising hits. The final hit launches enemies and shatters nearby cover.','⇈'),
 chip('cancel','Special Cancel','After a combo hit, interrupt your punches with Tatsumaki or Shoryuken. Special moves recover faster.','»'),
 chip('focus','Focus Armor','Take greatly reduced damage while charging Hadoken. You can still be knocked back.','◇'),
 chip('perfect','Perfect Rhythm','Landing the final combo hit restores stamina and a little health.','♪'),
 chip('juggle','Air Juggle','Hits deal greatly increased damage to airborne enemies. Keep them off the ground.','↑'),
 chip('afterimage','Afterimage Assault','Each punch leaves a delayed echo that strikes the same location. Enemies can move away.','▥'),
 chip('counter','Counter Hit','Striking an enemy during their attack deals extra damage and knockback.','!'),
 chip('secondwind','Round Two','Once per round, survive a lethal hit with a sliver of health and recover all stamina.','Ⅱ'),
];
export const ARCADE={chipRevealSeconds:4.8,knockdownRecovery:.9,parryInvulnerability:.10,parryFlash:.22,exclusiveChance:.85,guaranteedExclusives:2,comboMemory:1.1,chargeSeconds:1.4,hadokenCooldown:1.1,tatsuCooldown:5,shoryukenCooldown:6} as const;
export const ARCADE_INCOMPATIBLE=new Set(['echo-strike','hurricane','block-recharge']);
