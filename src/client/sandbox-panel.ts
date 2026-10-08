// Developer Sandbox tools (owner 2026-10-04): chip search/sort, dummy cast, sandbox map, animation tests, slow motion.
// Server effects go through the sandbox debug commands (src/shared/sandbox-tools.ts); intros, motion studies and the PvE
// downed/revive views are client presentation only (the DEV fixture injector).
import type {Snapshot} from '../shared/simulation.js';
import {CHIPS,RARITIES,WEAPONS,eligibleChips,type Chip} from '../shared/content.js';
import {canUse,type AccessInput} from '../shared/access.js';
import {ALL_MAPS} from '../shared/maps.js';
import {findChips,castSpec,keyInput,respawnDelay,CAST_KEYS,SANDBOX_RESPAWN,type ChipSort,type CastKey,type SandboxView} from '../shared/sandbox-tools.js';
import type {SpecId} from '../shared/config.js';
import {fixtureBase,fixtureParty} from '../shared/pve/fixtures.js';
import type {PveView} from '../shared/pve/contract.js';
import {REGEN_SECONDS} from './pve/downed.js';

export interface SandboxHost{state():Snapshot;me():string;access:AccessInput;debug(c:object):void;fixture(v:PveView|null):void;motion(m:string):void;intro(id:string):void}
export const sandboxUi={chip:'vital',query:'',sort:'rarity' as ChipSort,all:false,scale:1,key:'ai' as CastKey};
/** The panel's chip list: chips for my weapon, or every chip a dummy could cast with my access. */
export function sandboxChips(spec:keyof typeof WEAPONS,access:AccessInput):Chip[]{
 const base=sandboxUi.all?CHIPS.filter(c=>{const s=castSpec(c,spec);return !!s&&(!WEAPONS[s].devOnly||canUse(access,'weapon:'+s));}):eligibleChips(spec,{});
 return findChips(base,sandboxUi.query,sandboxUi.sort);
}
/** The list's options. Sorted by rarity, each tier is one group under its own header; otherwise rows carry the tier name. */
export function chipOptions(list:Chip[]){
 const opt=(c:Chip,label:string)=>`<option value="${c.id}" ${c.id===sandboxUi.chip?'selected':''}>${label}</option>`;
 if(sandboxUi.sort!=='rarity'&&sandboxUi.sort!=='rarity-desc')return list.map(c=>opt(c,`${RARITIES[c.rarity].name} · ${c.name}`)).join('');
 const groups:Chip[][]=[];for(const c of list){const g=groups[groups.length-1];if(g&&g[0].rarity===c.rarity)g.push(c);else groups.push([c]);}
 return groups.map(g=>`<optgroup label="${RARITIES[g[0].rarity].name} (${g.length})">${g.map(c=>opt(c,c.name)).join('')}</optgroup>`).join('');
}
/** Dummy cast keys: 'AI fight' is the old 4 s of bot AI; the rest make the dummy press that key at you. */
const KEY_LABELS:Record<CastKey,string>={ai:'AI fight · 4 s',e:'E',q:'Q',block:'Block (RMB)',dash:'Dash (Shift)',jump:'Jump (Space)','1':'1','2':'2','3':'3','4':'4',mode:'Siren mode'};
/** The weapon the dummy will cast the chosen chip with: its current one when the chip fits, as the server picks it. */
const castWeapon=(dummySpec:SpecId)=>{const c=CHIPS.find(x=>x.id===sandboxUi.chip);return c?castSpec(c,dummySpec):undefined;};
const keyOptions=(spec:SpecId|undefined)=>CAST_KEYS.map(k=>`<option value="${k}" ${k===sandboxUi.key?'selected':''} ${k!=='ai'&&(!spec||!keyInput(spec,k))?'disabled':''}>${KEY_LABELS[k]}</option>`).join('');
const sel=(id:string,o:[string,string][],label:string,cur='')=>`<select id="${id}" aria-label="${label}">${o.map(([v,t])=>`<option value="${v}" ${v===cur?'selected':''}>${t}</option>`).join('')}</select>`;
const ANIMS:[string,string][]=[['vent','Vent fall + SPLAT!'],['down','Downed + HELP (dummy)'],['revive','Holographic revive (dummy)'],['death','Death · kill effect above'],
 ['killcam','Killcam (dummy kills you)'],['intro','Weapon intro'],['sword','Melee swings'],['greatsword','Greatsword mocap'],['hit','Hit reaction'],['stagger','Heavy hit · stagger'],
 ['necronomicon','Summons attack (zombies)'],['puppet-master','Summons attack (toys)'],['summons-die','Summon deaths'],['l44t-scare','Slenderman · teleport + static']];
export const sandboxSearch=()=>`<label class="field" for="sandbox-search">CHIP SEARCH</label><div class="row"><input id="sandbox-search" class="input" type="search" placeholder="Name, id or text" value="${sandboxUi.query.replace(/"/g,'&quot;')}"/>${sel('sandbox-sort',[['rarity','Rarity ↑'],['rarity-desc','Rarity ↓'],['name','Name'],['default','Catalog order']],'Sort',sandboxUi.sort)}</div>`;
export function sandboxPanel(map:string,dummySpec:SpecId='fire',view?:SandboxView){
 return `<label class="settings-row" style="font-size:12px">Every chip (dummy cast)<input id="sandbox-all" type="checkbox" ${sandboxUi.all?'checked':''}/></label>
<label class="field">DUMMY CAST · CHIP ABOVE ON YOU</label><div class="row"><div><label class="fine" for="cast-every">Repeat every (s, 0 = once)</label><input id="cast-every" class="input" type="number" value="0" min="0" max="60"/></div><label class="settings-row" style="font-size:12px">Safe mode<input id="cast-safe" type="checkbox" checked/></label></div>
<div class="row"><div><label class="fine" for="cast-key" title="The dummy turns to you and presses this key">Press key</label><select id="cast-key">${keyOptions(castWeapon(dummySpec))}</select></div><div><label class="fine" for="sandbox-respawn" title="Seconds until a killed dummy stands up again; 0 = the round ends as before">Respawn (s)</label><input id="sandbox-respawn" class="input" type="number" value="${view?.respawn??SANDBOX_RESPAWN.default}" min="0" max="${SANDBOX_RESPAWN.max}" step="0.5"/></div></div>
<div class="actions"><button class="btn primary" data-sb="cast">Dummy cast</button><button class="btn" data-sb="stop">Stop</button></div>
<label class="field" for="sandbox-map">SANDBOX MAP</label><select id="sandbox-map">${ALL_MAPS.map(m=>`<option value="${m.id}" ${m.id===map?'selected':''}>${m.name}</option>`).join('')}</select>
<label class="field">ANIMATION TESTS</label><div class="row">${sel('anim-target',[['opponent','Dummy'],['self','Me']],'Target')}${sel('anim-delay',[['2','Splat 2s'],['10','Splat 10s']],'Splat delay')}${sel('anim-scale',[['1','1×'],['.5','0.5×'],['.25','0.25×']],'Time scale',String(sandboxUi.scale).replace(/^0/,''))}</div>
<div class="anim-list">${ANIMS.map(([k,t])=>`<div class="settings-row" style="font-size:12px">${t}<button class="btn" data-sb="${k}">Play</button></div>`).join('')}</div>`;
}
const val=(id:string)=>(document.getElementById(id) as HTMLInputElement|null)?.value??'';
const on=(id:string)=>!!(document.getElementById(id) as HTMLInputElement|null)?.checked;
/** Follow-up steps run on the sim clock (slow motion and slow frames keep the sequence intact). */
const queue:{at:number;f:()=>void}[]=[];
export function sandboxTick(t:number){for(const q of queue.filter(q=>t>=q.at)){queue.splice(queue.indexOf(q),1);q.f();}}
/** The dummy as a downed PvE party member (kept body, HELP beacon) and, with upAt, its regeneration. */
function downView(t:number,me:string,bot:string,upAt?:number):PveView{const ids=[me,bot],v=fixtureBase({time:t,ids});v.party=fixtureParty({time:t,ids,kinds:['alive','dead']});if(upAt!==undefined)v.party[bot].upAt=upAt;return v;}
export function playAnim(k:string,h:SandboxHost){
 const s=h.state(),target=val('anim-target'),safe=on('cast-safe'),bot=s.players.find(p=>p.id!==h.me()&&p.bot)?.id??'';
 queue.length=0;const later=(sec:number,f:()=>void)=>queue.push({at:s.time+sec,f});
 if(k==='vent')h.debug({action:'vent-fall',target,delay:Number(val('anim-delay'))});
 else if(k==='down'||k==='revive'){h.debug({action:'down',target:'opponent',hold:k==='down'?8:10});h.fixture(downView(s.time,h.me(),bot));
  if(k==='revive')later(2.5,()=>{h.debug({action:'heal'});const t=h.state().time;h.fixture(downView(t,h.me(),bot,t+.05));});
  later(k==='down'?8:3.3+REGEN_SECONDS,()=>h.fixture(null));}
 else if(k==='death')h.debug({action:'down',target:'opponent',hold:4});
 else if(k==='killcam')h.debug({action:'down',target:'self',by:'dummy'});
 else if(k==='intro')h.intro(target==='self'?h.me():bot);
 else if(k==='sword'||k==='greatsword'){if(target==='self'){h.debug({action:'spec',id:k});h.motion('attack');}else h.debug({action:'dummy-cast',spec:k,safe});}
 else if(k==='hit'||k==='stagger')h.debug({action:'hit',target,heavy:k==='stagger'});
 else if(k==='summons-die')h.debug({action:k});
 else h.debug({action:'dummy-cast',id:k,safe,...(k==='l44t-scare'?{key:'e'}:{})});
}
/** Delegated listeners on the panel root (once). Typing in the search box never reaches the game hotkeys. */
export function bindSandbox(root:HTMLElement,h:SandboxHost,redraw:()=>void){
 const mySpec=()=>(h.state().players.find(p=>p.id===h.me())?.spec??'pistol') as SpecId,dummySpec=()=>(h.state().players.find(p=>p.bot&&p.id!==h.me())?.spec??'fire') as SpecId;
 /** Keys the chosen chip's weapon has no use for are greyed out (and the choice falls back to AI fight). */
 const keys=()=>{const box=document.getElementById('cast-key') as HTMLSelectElement|null;if(!box)return;const spec=castWeapon(dummySpec());if(sandboxUi.key!=='ai'&&(!spec||!keyInput(spec,sandboxUi.key)))sandboxUi.key='ai';box.innerHTML=keyOptions(spec);};
 const refill=()=>{const list=sandboxChips(mySpec(),h.access),box=document.getElementById('sandbox-chip') as HTMLSelectElement|null;
  if(list.length&&!list.some(c=>c.id===sandboxUi.chip))sandboxUi.chip=list[0].id;if(box&&list.length){box.innerHTML=chipOptions(list);box.dispatchEvent(new Event('change',{bubbles:true}));}keys();};
 root.addEventListener('input',e=>{const t=e.target as HTMLInputElement;if(t.id==='sandbox-search'){sandboxUi.query=t.value;refill();}});
 root.addEventListener('keydown',e=>{const t=e.target as HTMLElement;if(t.id==='sandbox-search'&&(e.key==='Enter'||e.key==='ArrowDown')){e.preventDefault();document.getElementById('sandbox-chip')?.focus();}});
 root.addEventListener('change',e=>{const t=e.target as HTMLInputElement;
  if(t.id==='sandbox-sort'){sandboxUi.sort=t.value as ChipSort;refill();}else if(t.id==='sandbox-all'){sandboxUi.all=t.checked;refill();}
  else if(t.id==='sandbox-map'){h.debug({action:'map',id:t.value});setTimeout(redraw,50);}else if(t.id==='anim-scale')sandboxUi.scale=Number(t.value)||1;
  else if(t.id==='cast-key')sandboxUi.key=t.value as CastKey;else if(t.id==='sandbox-chip'){if(t.value)sandboxUi.chip=t.value;keys();}
  else if(t.id==='sandbox-respawn'&&t.value!==''){const n=respawnDelay(t.value);t.value=String(n);h.debug({action:'respawn',seconds:n});}});
 root.addEventListener('click',e=>{const k=(e.target as HTMLElement).closest<HTMLElement>('[data-sb]')?.dataset.sb;if(!k)return;
  if(k==='cast'||k==='stop')h.debug({action:'dummy-cast',id:sandboxUi.chip,stop:k==='stop',every:Number(val('cast-every')),safe:on('cast-safe'),key:sandboxUi.key});else playAnim(k,h);});
}
