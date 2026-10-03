// The Siren's chip reveal for every player (design §7.5, §6.2): the grin, then the fixed 4-card track with the current chip
// booting and "???" for the chips not reached yet. Timed by negative animation delays from the authoritative cinematic, so
// late joiners and rewinds land at the right moment. Written into its own root only when the window changes.
import {pveOf,type PveView} from '../../shared/pve/contract.js';
import {BOSSES} from '../../shared/pve/bosses.js';
import type {Snapshot} from '../../shared/simulation.js';
import {chipCard} from '../chip-card.js';
import {sirenCinematic} from '../siren-card.js';
import {escapeHtml} from './hud.js';

export type RevealWindow='smile'|'tray'|'fade';
/** Which part of the reveal plays at `time` (null when no reveal cinematic is active). */
export function revealWindow(pve:PveView|undefined,time:number):RevealWindow|null{
 const c=pve?.cinematic;if(!c||c.kind!=='reveal'||time<c.at||time>=c.until)return null;
 return time<(c.smileUntil??c.at)?'smile':time<c.until-.4?'tray':'fade';
}
/** Overlay markup for the reveal, or '' (pure in view and time). */
export function sirenRevealHtml(pve:PveView|undefined,time:number){
 const w=revealWindow(pve,time);if(!w||!pve?.cinematic)return '';
 const c=pve.cinematic,age=time-c.at,smileUntil=c.smileUntil??c.at,chip=c.chip??Math.max(1,pve.track.revealed),track=BOSSES[pve.boss].track,current=track[chip-1];
 const smile=`<div class="siren-selection pve-smile" style="--selection-duration:${(smileUntil-c.at).toFixed(2)}s;--selection-delay:${(-age).toFixed(3)}s">${sirenCinematic()}</div>`;
 if(w==='smile')return `<div class="pve-reveal" data-window="smile" data-reveal-chip="${chip}" role="img" aria-label="The Siren smiles">${smile}</div>`;
 const revealed=Math.max(pve.track.revealed,chip),since=time-smileUntil,found=chip===3&&pve.bossView?.foundAt!==undefined&&pve.bossView.foundAt>=c.at-.05;
 const cards=track.map((card,i)=>i<revealed?`<div class="pve-reveal-slot ${i===chip-1?'pve-boot':'done'}" data-slot="${i}">${chipCard(card,0,{disabled:true,index:i===chip-1?0:undefined,elapsed:i===chip-1?Math.round(since*1000)/1000:0,attr:'data-boss-chip'})}</div>`
  :`<div class="pve-reveal-slot locked" data-slot="${i}"><div class="pve-locked-card" aria-label="Locked Siren chip"><span>???</span><small>${i+1} / ${track.length}</small></div></div>`).join('');
 return `<div class="pve-reveal" data-window="${w}" data-reveal-chip="${chip}" style="--reveal-age:${(-since).toFixed(3)}s;--fade-delay:${(c.until-.4-time).toFixed(3)}s" role="dialog" aria-label="The Siren chooses ${escapeHtml(current?.name??'')}"><div class="pve-reveal-backdrop">${sirenCinematic()}</div><header class="pve-reveal-head"><span class="eyebrow">${found?'FOUND HIM · ':''}THE SIREN CHOOSES · CHIP ${chip} / ${track.length}</span></header>${current?`<div class="pve-reveal-spotlight"><strong data-glitch="${escapeHtml(current.name)}">${escapeHtml(current.name)}</strong><p>${escapeHtml(current.description)}</p></div>`:''}<div class="pve-reveal-tray">${cards}</div></div>`;
}
/** Root manager: rewrites only when the cinematic or its window changes. */
export class PveRevealOverlay{
 private key='';
 constructor(private root:HTMLElement){root.className='pve-reveal-root';}
 get active(){return this.key!=='';}
 update(state:Snapshot|undefined,visible=true){
  const pve=state?pveOf(state):undefined,time=state?.time??0,w=visible?revealWindow(pve,time):null;
  const key=w?`${pve!.cinematic!.at}:${w}:${pve!.track.revealed}`:'';if(key===this.key)return;this.key=key;
  this.root.innerHTML=w?sirenRevealHtml(pve,time):'';
 }
}
