// PvE variant of the Tab player board (owner feedback 2026-10-03: no rounds, sets or score-to-win in a boss run). Pure string
// builders; PlayerBoard (../player-board.ts) owns the DOM, the chip tooltip and the PvP board. The static part (pilots,
// weapons, installed chips) re-renders only when its key changes, so hovering a chip keeps working; the run line and each
// member's live cells (state, HP, kills, deaths, assists, revives, damage, money) are swapped in place every HUD tick.
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,type PvePartyView,type PveView} from '../../shared/pve/contract.js';
import {BOSSES} from '../../shared/pve/bosses.js';
import {CHIPS,CLASS_INFO,WEAPONS} from '../../shared/content.js';
import {rarityStyle} from '../chip-card.js';
import {chipIcon,icon,specIcon} from '../icons.js';
import {escapeHtml,formatCash} from './hud.js';

const n=(x:number)=>Math.round(x).toLocaleString('en-US');
const pct=(a:number,b:number)=>Math.max(0,Math.min(100,b>0?a/b*100:0)).toFixed(1);
const color=(p:PlayerView|undefined)=>/^#[\da-f]{6}$/i.test(p?.color||'')?p!.color:'#8a8f98';
/** Elapsed m:ss (floor), or h:mm:ss past an hour. */
export const runClock=(seconds:number)=>{const s=Math.max(0,Math.floor(seconds)),h=Math.floor(s/3600),m=Math.floor(s/60)%60,r=String(s%60).padStart(2,'0');return h?`${h}:${String(m).padStart(2,'0')}:${r}`:`${m}:${r}`;};
/** Run time from fight start, frozen at the end of the run; undefined before the fight starts. */
export function runSeconds(state:Snapshot,pve:PveView){return pve.startedAt===undefined?undefined:Math.max(0,(pve.endedAt??state.time)-pve.startedAt);}
const ROLE={dps:['bot','DPS'],tank:['shield','TANK'],medic:['heart-plus','MEDIC']} as const;
export const PVE_BOARD_COLUMNS=['Status','Kills','Deaths','Assists','Revives','Add dmg','Boss dmg','Money','Earned'] as const;

/** Party members (humans and party bots) present in the snapshot, in party (slot) order. */
export function boardMembers(state:Snapshot,pve:PveView){return Object.keys(pve.party).map(id=>({id,row:pve.party[id],p:state.players.find(x=>x.id===id)}));}

/** Stage title and detail line for the board heading. */
export function runHeading(state:Snapshot,pve:PveView){
 const boss=BOSSES[pve.boss],bv=pve.bossView,time=runSeconds(state,pve),bossPlayer=state.players.find(p=>p.id===BOSS_PLAYER_ID);
 let title:string,detail:string;
 if(pve.stage==='ended'||pve.outcome){title=pve.outcome==='victory'?'Victory':'Defeat';detail=pve.outcome==='victory'?`${boss.name} is silenced`:bv?`${boss.name} won in phase ${bv.phase} / 4`:`The party fell on wave ${pve.wave} / ${pve.waves}`;}
 else if(bv||pve.stage==='boss'){const phase=bv?.phase??1;title=`${boss.name} · Phase ${phase} / 4`;detail=bv?.hidden?'Hidden — find him':bossPlayer&&bossPlayer.maxHp>0?`Boss health ${Math.max(0,Math.round(bossPlayer.hp/bossPlayer.maxHp*100))}%`:'Boss fight';}
 else if(pve.stage==='intermission'){title=pve.wave>=pve.waves?`${boss.name} is coming`:'Intermission';detail=`Wave ${pve.wave} / ${pve.waves} cleared`;}
 else if(pve.stage==='wave'&&pve.wave>0){title=`Wave ${pve.wave} / ${pve.waves}`;detail=`${n(pve.waveLeft)} ${pve.waveLeft===1?'add':'adds'} left`;}
 else {title='Opening';detail='Choose your beginning chip';}
 return {title,detail,time};
}

/** Live heading: stage, run clock and party totals. */
export function boardRun(state:Snapshot,pve:PveView){
 const h=runHeading(state,pve),rows=boardMembers(state,pve).map(m=>m.row),sum=(f:(r:PvePartyView)=>number)=>rows.reduce((a,r)=>a+f(r),0);
 const totals:[string,string,string][]=[['crosshair','Kills',n(sum(r=>r.kills))],['skull','Deaths',n(sum(r=>r.deaths))],['swords','Damage',n(sum(r=>r.addDamage+r.bossDamage))],['sparkles','Earned',formatCash(sum(r=>r.kind==='bot'?0:r.earned))]];
 return `<div class="pveb-run" data-stage="${pve.stage}"${pve.outcome?` data-outcome="${pve.outcome}"`:''}><div class="pveb-run-main"><h2>${escapeHtml(h.title)}</h2><p><span>${escapeHtml(h.detail)}</span>${h.time!==undefined?`<span class="pveb-clock">${icon('clock-3')}Run <b>${runClock(h.time)}</b></span>`:''}</p></div><dl class="pveb-totals">${totals.map(([i,k,v])=>`<div><dt>${icon(i)}${k}</dt><dd>${v}</dd></div>`).join('')}</dl></div>`;
}

function status(state:Snapshot,pve:PveView,id:string,row:PvePartyView,p:PlayerView|undefined){
 const down=!p||p.hp<=0;let label:string,kind:string;
 if(!row.connected){label=`${icon('x')}Offline`;kind='offline';}
 else if(down){kind='down';const self=row.selfReviveAt!==undefined&&row.selfReviveAt>state.time;label=`${icon('skull')}Down${row.downAt!==undefined?` <small>${runClock(state.time-row.downAt)}</small>`:''}${self?` <small>· self-revive</small>`:''}`;}
 else if(row.channel){kind='reviving';label=`${icon('heart-plus')}Reviving <small>${escapeHtml(state.players.find(x=>x.id===row.channel!.target)?.name??'')}</small>`;}
 else if(row.open){kind='choosing';label=`${icon('cpu')}Choosing chip`;}
 else {kind='alive';label=`${icon('heart-pulse')}Alive`;}
 const hp=p&&!down?`<span class="pveb-hpnum">${Math.ceil(p.hp)}<small> / ${Math.round(p.maxHp)}</small></span>`:'';
 const recruits=pve.recruits.filter(r=>r.owner===id).map(r=>{const rp=state.players.find(x=>x.id===r.id),[ic,name]=ROLE[r.role],down=!!rp&&rp.hp<=0;
  return `<span class="pveb-recruit" data-role="${r.role}" data-down="${down?1:0}" title="Recruit ${name}${rp?down?' · down':` · ${Math.ceil(rp.hp)} HP`:''}">${icon(ic)}${name}<i><b style="width:${rp?pct(rp.hp,rp.maxHp):'100'}%"></b></i></span>`;}).join('');
 return `<div class="pveb-status" data-state="${kind}"><div class="pveb-state"><span>${label}</span>${hp}</div><i class="pveb-hp"><b style="width:${down||!p?'0':pct(p.hp,p.maxHp)}%"></b></i>${recruits?`<div class="pveb-recruits">${recruits}</div>`:''}</div>`;
}

/** One member's live cells (the status cell plus the stat columns in PVE_BOARD_COLUMNS order). */
export function boardLive(state:Snapshot,pve:PveView,id:string){
 const row=pve.party[id],p=state.players.find(x=>x.id===id);if(!row)return '';const bot=row.kind==='bot';
 const cell=(k:string,v:string,extra='')=>`<div class="pveb-stat" data-stat="${k}"${extra}><span>${k}</span><b>${v}</b></div>`;
 return status(state,pve,id,row,p)+cell('Kills',`${n(row.kills)}${row.heads?`<small>${n(row.heads)} ${row.heads===1?'head':'heads'}</small>`:''}`)+cell('Deaths',n(row.deaths))+cell('Assists',n(row.assists))+
  cell('Revives',n(row.revives))+cell('Add dmg',n(row.addDamage))+cell('Boss dmg',n(row.bossDamage))+
  cell('Money',bot?'—':formatCash(row.money),' data-money="1"')+cell('Earned',bot?'—':formatCash(row.earned));
}

function chipButtons(p:PlayerView){
 const chips=CHIPS.filter(c=>(p.chips[c.id]||0)>0);
 return chips.length?chips.map(c=>`<button class="board-chip rarity-${c.rarity}" style="${rarityStyle(c)}" data-board-chip="${c.id}" data-player="${escapeHtml(p.id)}" aria-label="${escapeHtml(c.name)}, ${p.chips[c.id]} stacks, ${escapeHtml(p.name)}" aria-describedby="board-chip-tooltip"><span class="board-chip-symbol">${chipIcon(c.id)}</span><strong>${c.name}</strong><b>×${p.chips[c.id]}</b></button>`).join(''):'<p class="board-empty pveb-nochips">No chips installed yet. Pick chips from drops and the chip machines.</p>';
}

/** Re-render key for the static markup (identity, weapon, chips; recruits only by id). */
export function boardKey(state:Snapshot,pve:PveView,me:string){
 return 'pve:'+JSON.stringify([me,...boardMembers(state,pve).map(({id,row,p})=>[id,row.kind,p?.name,p?.spec,p?.class,p?.color,p?.chips])]);
}

/** Whole PvE board panel (static part; live slots are filled by boardRun/boardLive and refreshed in place). */
export function pveBoardHtml(state:Snapshot,pve:PveView,me:string){
 const members=boardMembers(state,pve).map(({id,row,p},i)=>{
  const bot=row.kind==='bot',name=p?.name??id,w=p?WEAPONS[p.spec]:undefined,copies=p?Object.values(p.chips).reduce((a,v)=>a+v,0):0,types=p?CHIPS.filter(c=>(p.chips[c.id]||0)>0).length:0;
  const tag=bot?'<small class="bot">BOT</small>':id===me?'<small>YOU</small>':'';
  return `<article class="pveb-member ${id===me?'own':''}" data-member="${escapeHtml(id)}" data-bot="${bot?1:0}" style="--member-color:${color(p)};--member-i:${i}"><div class="pveb-pilot"><span class="pilot-head" style="--armor:${color(p)}"></span><div class="pveb-name"><h3>${escapeHtml(name)} ${tag}</h3>${p&&w?`<button class="pveb-weapon" data-board-chip="spec" data-player="${escapeHtml(id)}" aria-describedby="board-chip-tooltip" aria-label="${escapeHtml(w.name)} specialization (${CLASS_INFO[p.class].name}), ${escapeHtml(name)}" title="${CLASS_INFO[p.class].name}">${specIcon(p.spec)}<span>${escapeHtml(w.name)}</span></button>`:''}</div></div><div class="pveb-live" data-board-live="${escapeHtml(id)}">${boardLive(state,pve,id)}</div><div class="pveb-chips"><div class="pveb-chips-title">CHIPS <span>${copies} ${copies===1?'copy':'copies'} · ${types} ${types===1?'type':'types'}</span></div><div class="board-chip-grid pveb-chip-grid">${p?chipButtons(p):''}</div></div></article>`;}).join('');
 const head=`<div class="pveb-columns" aria-hidden="true"><span>Pilot</span>${PVE_BOARD_COLUMNS.map(c=>`<span>${c}</span>`).join('')}</div>`;
 return `<section class="board-panel pveb-panel" role="region" aria-label="Party board"><div class="board-heading pveb-heading"><div class="pveb-heading-text"><div class="eyebrow">HOLD TAB · PARTY BOARD · ${escapeHtml(BOSSES[pve.boss].name.toUpperCase())} BOSS TEST</div><div data-board-run>${boardRun(state,pve)}</div></div><button class="close" data-board-close aria-label="Close party board">×</button></div>${head}<div class="pveb-members">${members}</div><p class="board-hint">Hover a Chip or weapon for its effects and stacked values. Release Tab to return. The run continues while inspecting.</p></section>`;
}
