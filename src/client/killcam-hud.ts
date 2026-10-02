import type {KillcamInfo} from './killcam.js';
const escape=(value:string)=>value.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));

/** Functional placeholder using existing styles. Claude owns the final visual treatment. */
export function killcamHud(info:Readonly<KillcamInfo>){
 return `<div class="spectator-label killcam-label" data-killcam="playing"><strong>KILL CAM</strong><span>${info.killerName?'Killed by '+escape(info.killerName):'Death replay'}</span><progress aria-label="Replay progress" max="1" value="${info.progress.toFixed(2)}"></progress><div class="actions"><button class="btn primary" data-action="killcam-skip">Skip replay</button></div><small>Space / Esc to skip · Match continues</small></div>`;
}
