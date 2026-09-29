import type {PlayerView} from '../shared/simulation.js';
import {WEAPONS,CLASS_INFO} from '../shared/content.js';
import {LOBBY_POSES,lobbyPose,BOT_WEAPONS,type BotWeapon} from '../shared/lobby-presentation.js';
import {specIcon,icon} from './icons.js';
const esc=(value:unknown)=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
export function pilotCard(p:PlayerView,tag:string,index=0,ready?:boolean){
 const color=/^#[\da-f]{6}$/i.test(p.color)?p.color:'#ff6b35',pose=lobbyPose(p.cosmetics?.lobbyPose);
 return `<article class="pilot-card ${ready?'is-ready':''}" style="--pilot-accent:${color}"><div class="pilot-card-top"><span class="pilot-number">${String(index+1).padStart(2,'0')}</span><span>${esc(tag)}</span>${ready===undefined?'':`<span class="pilot-ready">${icon(ready?'check':'clock-3')}${ready?'Ready':'Not ready'}</span>`}</div><div class="portrait-stage" data-pose="${pose}"><canvas data-pilot-preview="${esc(p.id)}" aria-label="${esc(p.name)} — ${esc(LOBBY_POSES[pose])}"></canvas><div class="sleep-marks" aria-hidden="true"><span>z</span><span>Z</span><span>Z</span></div></div><div class="pilot-card-name"><strong>${esc(p.name)}</strong><span>${specIcon(p.spec)}${esc(WEAPONS[p.spec].name)}</span></div><div class="pilot-card-bottom"><span>${esc(CLASS_INFO[p.class].name)}</span><span>${esc(LOBBY_POSES[pose])}</span></div></article>`;
}
export function botLoadouts(count:number,choices:BotWeapon[]){
 return `<section class="bot-loadouts"><div class="section-title"><h2>Bot loadouts</h2><span>${count} ${count===1?'opponent':'opponents'}</span></div>${Array.from({length:count},(_,i)=>{const selected=choices[i]||'random';return `<label class="bot-loadout" for="bot-weapon-${i}"><span class="bot-slot">${String(i+1).padStart(2,'0')}</span><span>Bot ${i+1}</span><select id="bot-weapon-${i}" data-bot-weapon="${i}"><option value="random" ${selected==='random'?'selected':''}>Random weapon</option>${Object.entries(CLASS_INFO).map(([cls,info])=>`<optgroup label="${esc(info.name)}">${BOT_WEAPONS.filter(w=>w.class===cls).map(w=>`<option value="${w.id}" ${selected===w.id?'selected':''}>${esc(w.name)}</option>`).join('')}</optgroup>`).join('')}</select></label>`;}).join('')}<p class="fine">Random chooses a weapon when the match is created.</p></section>`;
}
