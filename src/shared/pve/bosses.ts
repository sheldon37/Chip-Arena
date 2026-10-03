// Boss definitions and the fixed boss chip track (design §6.2). These chips are NOT in CHIPS, so the PvP catalog, drafts and
// chip tests are unchanged. rarity 'siren' reuses the existing card art and aura.
import type {Chip} from '../content.js';
import type {HordeModifiers,PveBossId} from './contract.js';
import {PVE_SIREN} from './balance.js';

export type BossMechanic='spawn'|'possess'|'grow'|'nocd';
/** A boss chip: a normal Chip (effects:[]) plus the mechanic the director runs when it is revealed. */
export interface BossChip extends Chip{mechanic:BossMechanic;
 /** Reserved for future Siren empowerment chips: the director multiplies every revealed chip's empower into modifiers(). */
 empower?:Partial<HordeModifiers>}
export interface BossDefinition{id:PveBossId;
 /** Player spec used for the boss Player. */ spec:'siren';
 /** Display name (animated title, HP bar). */ name:string;
 /** HP fractions that end phases 1-3 (hard caps, D2). */ thresholds:readonly number[];
 /** Chip k (1-based) is revealed at the start of the boss stage (k=1) and at each threshold (k=2..4). */ track:readonly BossChip[];
 color:string}
const chip=(id:string,name:string,description:string,icon:string,mechanic:BossMechanic):BossChip=>
 ({id,name,description,icon,mechanic,rarity:'siren',tags:['siren-boss'],max:1,effects:[]});
/** Names and descriptions are the owner's words (punctuation trimmed). */
export const SIREN_BOSS_CHIPS:readonly BossChip[]=[
 chip('siren-boss-arrive','The Siren is here...','Spawns the Siren.','skull','spawn'),
 chip('siren-boss-possess','Find me...','Siren will "possess" one of his minions. Minion spawns are increased until he is found.','ghost','possess'),
 chip('siren-boss-beautiful','Am I beautiful?','Siren will grow larger, enhancing the range and area of effect on all attacks/abilities.','sparkles','grow'),
 chip('siren-boss-play',"Fine then... I'll play",'Siren has 100% cooldown reduction. All abilities no longer have a cooldown.','hourglass','nocd'),
];
export const SIREN_BOSS:BossDefinition={id:'siren',spec:'siren',name:'The Siren',thresholds:PVE_SIREN.thresholds,track:SIREN_BOSS_CHIPS,color:'#b7a8ff'};
export const BOSSES:Record<PveBossId,BossDefinition>={siren:SIREN_BOSS};
