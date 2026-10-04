import type {KillcamInfo} from './killcam.js';
import {specIcon} from './icons.js';
const escape=(value:string)=>value.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
/** Only a valid #rgb/#rrggbb colour is interpolated into the inline style. */
const safeColor=(value:string|undefined,fallback:string)=>value&&/^#[0-9a-f]{3}([0-9a-f]{3})?$/i.test(value)?value:fallback;

/** Optional presentation context read from the historical replay frame (never live authority). */
export interface KillcamContext {killerColor?:string;weapon?:string;spec?:string;team?:{name:string;color:string};}

const CAUSES:Record<string,string>={
 direct:'Direct hit',secondary:'Special attack',burn:'Burning',poison:'Poison',fall:'Fell out of the arena',overtime:'Overtime',
 debris:'Falling debris',environment:'Environment',lava:'Lava',spikes:'Spikes',saw:'Saw blade',vent:'Vent blast',tree:'Crushed by a tree',
 thorns:'Thorns',reflected:'Reflected shot',blunt:'Blunt force',pierce:'Piercing hit',blast:'Explosion',nuke:'Nuke',bolt:'Bolt',
 jab:'Jab',combo:'Combo',parry:'Parry counter',slam:'Slam','dagger-throw':'Thrown dagger','shield-throw':'Thrown shield',
 'sword-launch':'Sword launch','wolf-spin':'Wolf spin',hellfire:'Hellfire',energy:'Energy blast',zombie:'Zombie',push:'Knocked away',pull:'Pulled in',
 'grappling-shot':'Grappling shot',nova:'Nova',
};
/** Readable death cause; unknown kinds degrade to their own words rather than a raw id. */
export function killcamCause(kind:string){
 if(CAUSES[kind])return CAUSES[kind];
 const words=kind.replace(/[-_]+/g,' ').replace(/([a-z])([A-Z])/g,'$1 $2').trim().toLowerCase();
 return words?words[0].toUpperCase()+words.slice(1):'Eliminated';
}
/** Seconds relative to the fatal hit: −1.5 counts down to the kill, then up to +4.0 through the aftermath. */
export function killcamClock(info:Readonly<KillcamInfo>){
 const d=info.time-info.at;return d<-.05?`−${Math.abs(d).toFixed(1)}s`:d>.05?`+${d.toFixed(1)}s`:'0.0s';
}
/** The last moments before the clip ends: the letterbox and cards play their exit animation. */
export const KILLCAM_EXIT=.3;

/**
 * Broadcast-style replay overlay. Everything sits on the frame edges (letterbox, corner tag, lower third, timeline) so the
 * centre of the screen, where the fatal hit happens, stays clear. The root keeps a stable class/data-death per clip so the
 * 10 Hz HUD morph never restarts its entry animation; only the progress transform and clock text change between redraws.
 */
export function killcamHud(info:Readonly<KillcamInfo>,context:KillcamContext={}){
 const span=Math.max(.001,info.end-info.start),mark=Math.min(1,Math.max(0,(info.at-info.start)/span));
 const closing=info.end-info.time<=KILLCAM_EXIT?' out':'',after=info.time>=info.at;
 const killer=info.killerName?escape(info.killerName):'',self=!info.killerId;
 const color=safeColor(context.killerColor,self?'#ff535f':'#ff6b35');
 const weapon=!self&&context.weapon?`${context.spec?specIcon(context.spec):''}<span>${escape(context.weapon)}</span><i aria-hidden="true">·</i>`:'';
 const team=!self&&context.team?`<em style="--kc-team:${safeColor(context.team.color,'#a6a7ad')}">${escape(context.team.name)}</em>`:'';
 const watching=info.viewId===info.killerId&&!self?'Killer’s view':'Your final moments';
 return `<div class="killcam" data-killcam="playing" data-death="${escape(info.deathId)}" style="--kc-accent:${color};--kc-mark:${(mark*100).toFixed(2)}%">`
  +`<i class="kc-letterbox kc-top${closing}" aria-hidden="true"></i><i class="kc-letterbox kc-bottom${closing}" aria-hidden="true"></i>`
  +`<i class="kc-vignette${closing}" aria-hidden="true"></i><i class="kc-flash" aria-hidden="true"></i>`
  +`<div class="kc-tag${closing}" role="status" aria-label="Kill cam replay"><b class="kc-rec" aria-hidden="true"></b><strong>KILL CAM</strong><span class="kc-clock">${killcamClock(info)}</span></div>`
  +`<div class="kc-card${closing}"><small>${self?'You were eliminated':'Eliminated by'}</small>`
  +`<strong>${self?killcamCause(info.cause):killer||'Unknown pilot'}</strong>${team}`
  +`<span class="kc-cause">${weapon}<span>${self?'No killer · replaying your view':killcamCause(info.cause)}</span></span></div>`
  +`<div class="kc-timeline${closing}"><span class="kc-view">${watching}</span>`
  +`<div class="kc-track${after?' after':''}" role="progressbar" aria-label="Replay progress" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${Math.round(info.progress*100)}">`
  +`<i class="kc-fill" style="--kc-rest:${((1-info.progress)*100).toFixed(2)}%"></i><i class="kc-mark" title="Fatal hit"></i></div></div>`
  +`<button class="kc-skip${closing}" data-action="killcam-skip" type="button">Skip replay <kbd>Space</kbd></button>`
  +`</div>`;
}

/** One-shot flash and LIVE chip mounted when the replay hands back to live spectating (skip or completion). */
export function killcamReturnHud(deathId:string){
 return `<div class="killcam-return" data-return="${escape(deathId)}" aria-hidden="true"><i></i><b>LIVE</b></div>`;
}
