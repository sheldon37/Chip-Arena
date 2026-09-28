import type {Chip,Rarity} from './content.js';
const chip=(id:string,name:string,rarity:Rarity,description:string,icon:string):Chip=>({id:'arcade-'+id,name,rarity,tags:['ansatsuken'],max:1,icon,mechanic:'arcade-'+id,effects:[],description});
export const ARCADE_CHIPS:Chip[]=[
 chip('shinku','Shinku Hadoken','legendary','Hold 1 to charge. Release a full charge to fire a huge, piercing five-hit Hadoken.','◎'),
 chip('rush','Ten-Hit Rush','legendary','Your three-hit punch and knee chain becomes a rapid ten-hit combo with a flying-knee finisher.','10'),
 chip('red','Shakunetsu','epic','Hadokens turn crimson and inflict a much stronger burn.','♨'),
 chip('cyclone','Hurricane Tatsu','epic','Tatsumaki spins longer, pulls nearby enemies inward and lands more kicks.','⟳'),
 chip('rising','Shin Shoryuken','mythic','Shoryuken becomes three rising hits. The final hit launches enemies and shatters nearby cover.','⇈'),
 chip('cancel','Special Cancel','rare','After a combo hit, interrupt your punches with Tatsumaki or Shoryuken. Special moves recover faster.','»'),
 chip('focus','Focus Armor','epic','Take greatly reduced damage while charging Hadoken. You can still be knocked back.','◇'),
 chip('perfect','Perfect Rhythm','rare','Landing the final combo hit restores stamina and a little health.','♪'),
 chip('juggle','Air Juggle','epic','Hits deal greatly increased damage to airborne enemies. Keep them off the ground.','↑'),
 chip('afterimage','Afterimage Assault','legendary','Each punch leaves a delayed echo that strikes the same location. Enemies can move away.','▥'),
 chip('counter','Counter Hit','rare','Striking an enemy during their attack deals extra damage and knockback.','!'),
 chip('secondwind','Round Two','mythic','Once per round, survive a lethal hit with a sliver of health and recover all stamina.','Ⅱ'),
];
export const ARCADE={parryInvulnerability:.10,parryFlash:.22,exclusiveChance:.85,guaranteedExclusives:2,comboMemory:1.1,chargeSeconds:1.4,hadokenCooldown:1.1,tatsuCooldown:5,shoryukenCooldown:6} as const;
export const ARCADE_INCOMPATIBLE=new Set(['echo-strike','hurricane','block-recharge']);
