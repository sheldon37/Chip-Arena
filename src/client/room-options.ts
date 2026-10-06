// Online room layout helpers (PvP and PvE lobbies): the ready summary, the ready status line and the remembered
// "Advanced Options" fold. Layout only: no setting, element id or network message changes here.
export const ROOM_ADVANCED_KEY='chip-arena-room-advanced';
type Store=Pick<Storage,'getItem'|'setItem'>|undefined;
const browserStore=():Store=>{try{return globalThis.localStorage;}catch{return undefined;}};

/** Whether this viewer last left "Advanced Options" open (collapsed by default; storage may be blocked). */
export function roomAdvancedOpen(store:Store=browserStore()){try{return store?.getItem(ROOM_ADVANCED_KEY)==='1';}catch{return false;}}
export function rememberRoomAdvanced(open:boolean,store:Store=browserStore()){try{store?.setItem(ROOM_ADVANCED_KEY,open?'1':'0');}catch{/* private window or blocked storage: the fold just resets */}}

const esc=(value:unknown)=>String(value).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
/** Big "2/3 READY" meter for the roster heading. */
export function readySummary(ready:number,total:number){
 const all=total>0&&ready>=total;
 return `<span class="ready-count" data-all-ready="${all?1:0}" aria-label="${ready} of ${total} ready"><strong>${ready}/${total}</strong>READY</span>`;
}
/** One line under the Ready button: who the room is waiting for. */
export function readyStatus(ready:number,total:number,noun='pilot',start='The match starts when every pilot is ready.'){
 const waiting=Math.max(0,total-ready);
 return waiting?`<strong class="ready-wait">Waiting for ${waiting} ${noun}${waiting===1?'':'s'}</strong> · ${start}`:`<strong class="ready-go">Everyone is ready.</strong> ${start}`;
}
/** Collapsible "Advanced Options" fold; `note` summarises the hidden values so the collapsed view still tells the rules. */
export function roomAdvanced(body:string,open=roomAdvancedOpen(),note=''){
 return `<details class="lobby-advanced room-advanced" data-room-advanced ${open?'open':''}><summary><span>Advanced Options</span>${note?`<small>${esc(note)}</small>`:''}</summary>${body}</details>`;
}
