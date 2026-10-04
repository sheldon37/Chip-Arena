// Developer Sandbox tools (owner 2026-10-04): chip search/sort, dummy cast, sandbox map, animation tests, slow motion.
// Server effects go through the sandbox debug commands (src/shared/sandbox-tools.ts); intros, motion studies and the PvE
// downed/revive views are client presentation only (the DEV fixture injector).
import type {Snapshot} from '../shared/simulation.js';
import {CHIPS,RARITIES,WEAPONS,eligibleChips,type Chip} from '../shared/content.js';
import {canUse,type AccessInput} from '../shared/access.js';
import {ALL_MAPS} from '../shared/maps.js';
import {findChips,castSpec,type ChipSort} from '../shared/sandbox-tools.js';
import {fixtureBase,fixtureParty} from '../shared/pve/fixtures.js';
import type {PveView} from '../shared/pve/contract.js';
import {REGEN_SECONDS} from './pve/downed.js';

export interface SandboxHost{state():Snapshot;me():string;access:AccessInput;debug(c:object):void;fixture(v:PveView|null):void;motion(m:string):void;intro(id:string):void}
export const sandboxUi={chip:'vital',query:'',sort:'default' as ChipSort,all:false,scale:1};
/** The panel's chip list: chips for my weapon, or every chip a dummy could cast with my access. */
export function sandboxChips(spec:keyof typeof WEAPONS,access:AccessInput):Chip[]{
 const base=sandboxUi.all?CHIPS.filter(c=>{const s=castSpec(c,spec);return !!s&&(!WEAPONS[s].devOnly||canUse(access,'weapon:'+s));}):eligibleChips(spec,{});
 return findChips(base,sandboxUi.query,sandboxUi.sort);
}
export const chipOptions=(list:Chip[])=>list.map(c=>`<option value="${c.id}" ${c.id===sandboxUi.chip?'selected':''}>${RARITIES[c.rarity].name} · ${c.name}</option>`).join('');
const sel=(id:string,o:[string,string][],label:string,cur='')=>`<select id="${id}" aria-label="${label}">${o.map(([v,t])=>`<option value="${v}" ${v===cur?'selected':''}>${t}</option>`).join('')}</select>`;
const ANIMS:[string,string][]=[['vent','Vent fall + SPLAT!'],['down','Downed + HELP (dummy)'],['revive','Holographic revive (dummy)'],['death','Death · kill effect above'],
 ['killcam','Killcam (dummy kills you)'],['intro','Weapon intro'],['sword','Melee swings'],['greatsword','Greatsword mocap'],['hit','Hit reaction'],['stagger','Heavy hit · stagger'],
 ['necronomicon','Summons attack (zombies)'],['puppet-master','Summons attack (toys)'],['summons-die','Summon deaths'],['l44t-scare','Slenderman · teleport + static']];
export const sandboxSearch=()=>`<label class="field" for="sandbox-search">CHIP SEARCH</label><div class="row"><input id="sandbox-search" class="input" type="search" placeholder="Name, id or text" value="${sandboxUi.query.replace(/"/g,'&quot;')}"/>${sel('sandbox-sort',[['default','Default order'],['rarity','Rarity ↑'],['rarity-desc','Rarity ↓'],['name','Name']],'Sort',sandboxUi.sort)}</div>`;
export function sandboxPanel(map:string){
 return `<label class="settings-row" style="font-size:12px">Every chip (dummy cast)<input id="sandbox-all" type="checkbox" ${sandboxUi.all?'checked':''}/></label>
<label class="field">DUMMY CAST · CHIP ABOVE ON YOU</label><div class="row"><div><label class="fine" for="cast-every">Repeat every (s, 0 = once)</label><input id="cast-every" class="input" type="number" value="0" min="0" max="60"/></div><label class="settings-row" style="font-size:12px">Safe mode<input id="cast-safe" type="checkbox" checked/></label></div>
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
 else h.debug({action:'dummy-cast',id:k,safe});
}
/** Delegated listeners on the panel root (once). Typing in the search box never reaches the game hotkeys. */
export function bindSandbox(root:HTMLElement,h:SandboxHost,redraw:()=>void){
 const refill=()=>{const me=h.state().players.find(p=>p.id===h.me()),list=sandboxChips(me?.spec??'pistol',h.access),box=document.getElementById('sandbox-chip') as HTMLSelectElement|null;
  if(list.length&&!list.some(c=>c.id===sandboxUi.chip))sandboxUi.chip=list[0].id;if(box&&list.length){box.innerHTML=chipOptions(list);box.dispatchEvent(new Event('change',{bubbles:true}));}};
 root.addEventListener('input',e=>{const t=e.target as HTMLInputElement;if(t.id==='sandbox-search'){sandboxUi.query=t.value;refill();}});
 root.addEventListener('keydown',e=>{const t=e.target as HTMLElement;if(t.id==='sandbox-search'&&(e.key==='Enter'||e.key==='ArrowDown')){e.preventDefault();document.getElementById('sandbox-chip')?.focus();}});
 root.addEventListener('change',e=>{const t=e.target as HTMLInputElement;
  if(t.id==='sandbox-sort'){sandboxUi.sort=t.value as ChipSort;refill();}else if(t.id==='sandbox-all'){sandboxUi.all=t.checked;refill();}
  else if(t.id==='sandbox-map'){h.debug({action:'map',id:t.value});setTimeout(redraw,50);}else if(t.id==='anim-scale')sandboxUi.scale=Number(t.value)||1;});
 root.addEventListener('click',e=>{const k=(e.target as HTMLElement).closest<HTMLElement>('[data-sb]')?.dataset.sb;if(!k)return;
  if(k==='cast'||k==='stop')h.debug({action:'dummy-cast',id:sandboxUi.chip,stop:k==='stop',every:Number(val('cast-every')),safe:on('cast-safe')});else playAnim(k,h);});
}
