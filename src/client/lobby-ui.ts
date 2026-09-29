import type {AccessInput} from '../shared/access.js';
import type {ClassId} from '../shared/config.js';
import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS,CLASS_INFO} from '../shared/content.js';
import {LOBBY_POSES,lobbyPose,BOT_WEAPONS,preferredWeapons,preferredWeapon,type BotWeapon} from '../shared/lobby-presentation.js';
import {classIcon,icon} from './icons.js';
const esc=(value:unknown)=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
export function pilotCard(p:PlayerView,tag:string,index=0,ready?:boolean){
 const color=/^#[\da-f]{6}$/i.test(p.color)?p.color:'#ff6b35',pose=lobbyPose(p.cosmetics?.lobbyPose),styleColor="#"+WEAPONS[p.spec].color.toString(16).padStart(6,"0");
 return `<article class="pilot-card ${ready?'is-ready':''}" data-style="${p.spec}" style="--pilot-accent:${color};--style-color:${styleColor}"><div class="pilot-card-top"><span class="pilot-number">${String(index+1).padStart(2,'0')}</span><span>${esc(tag)}</span>${ready===undefined?'':`<span class="pilot-ready">${icon(ready?'check':'clock-3')}${ready?'Ready':'Not ready'}</span>`}</div><div class="portrait-stage" data-pose="${pose}"><canvas data-pilot-preview="${esc(p.id)}" aria-label="${esc(p.name)} — ${esc(LOBBY_POSES[pose])}"></canvas><div class="sleep-marks" aria-hidden="true"><span>z</span><span>Z</span><span>Z</span></div></div><div class="pilot-card-name"><strong>${esc(p.name)}</strong><span class="pilot-class">${classIcon(p.class)}<span>${esc(CLASS_INFO[p.class].name)}</span><i class="pilot-class-fx" aria-hidden="true"></i></span></div></article>`;
}
export function botLoadouts(count:number,choices:BotWeapon[]){
 return `<section class="bot-loadouts"><div class="section-title"><h2>Bot loadouts</h2><span>${count} ${count===1?'opponent':'opponents'}</span></div>${Array.from({length:count},(_,i)=>{const selected=choices[i]||'random';return `<label class="bot-loadout" for="bot-weapon-${i}"><span class="bot-slot">${String(i+1).padStart(2,'0')}</span><span>Bot ${i+1}</span><select id="bot-weapon-${i}" data-bot-weapon="${i}"><option value="random" ${selected==='random'?'selected':''}>Random weapon</option>${Object.entries(CLASS_INFO).map(([cls,info])=>`<optgroup label="${esc(info.name)}">${BOT_WEAPONS.filter(w=>w.class===cls).map(w=>`<option value="${w.id}" ${selected===w.id?'selected':''}>${esc(w.name)}</option>`).join('')}</optgroup>`).join('')}</select></label>`;}).join('')}<p class="fine">Random chooses a weapon when the match is created.</p></section>`;
}

export function lobbyDensity(count:number){return count<=2?'large':count<=4?'medium':'compact';}
export function preferredWeaponOptions(cls:ClassId,value:unknown,access:AccessInput=false){
 const selected=preferredWeapon(value,cls,access);
 return preferredWeapons(access).filter(w=>w.class===cls).map(w=>`<option value="${w.id}" ${w.id===selected?'selected':''}>${esc(w.name)}</option>`).join('');
}
