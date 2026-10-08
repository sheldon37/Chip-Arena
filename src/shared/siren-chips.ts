import type {Chip} from './content.js';

/**
 * Siren-exclusive Chips (owner request 2026-10-01: boss Chips for testing). Offered only to the Siren, with the same
 * exclusive odds as the other DEV kits. They use stats the Siren's combat already honours: maximum health, damage,
 * movement speed, incoming damage, received knockback and thorns (weapon hits on the Siren burn the attacker).
 */
export const SIREN_CHIPS:Chip[]=[
 {id:'siren-crown',name:'Hollow Crown',rarity:'siren',tags:['siren'],max:1,icon:'◎',
  description:'+3,000 maximum health. It takes far longer to put down.',effects:[{stat:'hp',op:'add',value:3000}]},
 {id:'siren-hide',name:'Black Flame Hide',rarity:'siren',tags:['siren'],max:1,icon:'♨',
  description:'Weapon hits on you burn the attacker for 30% of the damage they deal. Knockback on you is halved.',
  effects:[{stat:'thorns',op:'add',value:.3},{stat:'knockbackTaken',op:'mul',value:.5}]},
 {id:'siren-break',name:'Psychotic Break',rarity:'siren',tags:['siren'],max:1,icon:'≋',
  description:'Every attack deals 25% more damage and you move 12% faster, but you take 15% more damage.',
  effects:[{stat:'damage',op:'mul',value:1.25},{stat:'speed',op:'mul',value:1.12},{stat:'damageTaken',op:'mul',value:1.15}]},
];
/** Same shape as the other exclusive kits: the first two offers are Siren Chips while any remain, later slots usually. */
export const SIREN_CHIP_ODDS={exclusiveChance:.85,guaranteedExclusives:2} as const;
