import type {PlayerView} from '../shared/simulation.js';
/** Compact sprint feedback beside the player's resources, in both camera modes. */
export function sprintStatus(p:Pick<PlayerView,'hp'|'sprinting'|'sprintExhausted'>,active:boolean){
 if(!active||p.hp<=0)return '';
 if(p.sprinting)return '<span class="sprint-status"><i aria-hidden="true">»</i> SPRINTING</span>';
 if(p.sprintExhausted)return '<span class="sprint-status exhausted">SPRINT DEPLETED · RELEASE W</span>';
 return '';
}
