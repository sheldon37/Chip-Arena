import {KILL_STYLES,canUseKillEffect,type Cosmetics} from '../shared/cosmetics.js';
import type {AccessInput} from '../shared/access.js';

type Effect=Cosmetics['killEffect'];
type Store=Pick<Storage,'getItem'|'setItem'|'removeItem'>;
const key=(accountId:string)=>'chip-arena-kill-effect-pending:'+accountId;
const allowed=(effect:unknown,access:AccessInput):effect is Effect=>typeof effect==='string'&&Object.hasOwn(KILL_STYLES,effect)&&canUseKillEffect(access,effect);

/** Retain a supported local choice until the account service acknowledges it.
 * The server's last value distinguishes an old fallback from a later cloud edit.
 * Account IDs isolate devices' local preferences; this never grants item access.
 */
export function rememberKillEffect(store:Store,accountId:string,selected:Effect,remote:Effect,access:AccessInput){
 const pending=allowed(selected,access)&&selected!==remote;
 if(pending)store.setItem(key(accountId),JSON.stringify({selected,remote}));
 else store.removeItem(key(accountId));
 return pending;
}

export function restoreKillEffect(store:Store,accountId:string,remote:Effect,access:AccessInput):Effect{
 let saved:any;try{saved=JSON.parse(store.getItem(key(accountId))||'null');}catch{store.removeItem(key(accountId));return remote;}
 if(!saved)return remote;
 if(!allowed(saved.selected,access)||typeof saved.remote!=='string'||remote!==saved.remote||remote===saved.selected){
  store.removeItem(key(accountId));return remote;
 }
 return saved.selected;
}
