import {Chip} from '../shared/content.js';
import {DRAFT_REVEAL_SECONDS} from '../shared/draft-settings.js';
import {chipCard,rarityStyle} from './chip-card.js';
const escape=(s:string)=>s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
const cuts=['polygon(0 0,52% 0,37% 43%,0 32%)','polygon(52% 0,100% 0,100% 38%,37% 43%)','polygon(0 32%,37% 43%,48% 71%,0 78%)','polygon(37% 43%,100% 38%,100% 68%,48% 71%)','polygon(0 78%,48% 71%,61% 100%,0 100%)','polygon(48% 71%,100% 68%,100% 100%,61% 100%)'];
export function brokenChip(c:Chip,copies:number,index:number,elapsed:number){
 const card=chipCard(c,copies,{disabled:true});
 return `<div class="chip-shatter" aria-hidden="true" inert style="${rarityStyle(c)};--break-delay:${index*.035-elapsed}s"><div class="shatter-size">${card}</div>${cuts.map((cut,n)=>`<div class="chip-shard" style="clip-path:${cut};--dx:${(n%2?1:-1)*(45+index*5)}px;--dy:${(n-2.5)*44}px;--spin:${(n%2?1:-1)*(15+n*7)}deg">${card}</div>`).join('')}</div>`;
}
export function selectedChip(c:Chip,copies:number,picker:string,elapsed:number){
 return `<div class="selection-spotlight" role="status" aria-live="polite" style="${rarityStyle(c)};--selection-delay:${-elapsed}s;--selection-duration:${DRAFT_REVEAL_SECONDS}s"><div class="selection-focus"><div class="selection-owner">${escape(picker)} <span>INSTALLED</span></div>${chipCard(c,Math.max(0,copies-1),{disabled:true,chosen:true})}<div class="selection-confirm">UPGRADE SECURED <span>×${copies}</span></div></div></div>`;
}
