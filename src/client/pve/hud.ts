// PvE HUD widgets (design §7.3). Pure string builders: main.ts concatenates them into drawHud's markup when the snapshot
// carries `pve`. Identity lives in class/data-* (so hud-morph keeps nodes); changing values live in style and text.
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,decodeAdd,type DropTier,type PveView} from '../../shared/pve/contract.js';
import {ECONOMY,PVE,PVE_SIREN,revivePrice} from '../../shared/pve/balance.js';
import {BOSSES} from '../../shared/pve/bosses.js';
import {RARITIES} from '../../shared/content.js';
import {icon} from '../icons.js';
import {flames} from '../siren-card.js';
import type {MoneyPopup} from './tracks.js';

export const escapeHtml=(x:unknown)=>String(x).replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
/** `$1,250` (D26). */
export const formatCash=(n:number)=>(n<0?'−':'')+'$'+Math.abs(Math.round(n)).toLocaleString('en-US');
/** `+$10` / `−$150`. */
export const formatGain=(n:number)=>(n<0?'−':'+')+'$'+Math.abs(Math.round(n)).toLocaleString('en-US');
/** m:ss, never negative. */
export const clock=(seconds:number)=>{const s=Math.max(0,Math.ceil(seconds));return `${Math.floor(s/60)}:${String(s%60).padStart(2,'0')}`;};
const TIER_ORDER:DropTier[]=['common','uncommon','rare','epic','legendary','mythic','rainbow'];
export const tierName=(t:DropTier)=>t==='rainbow'?'Rainbow':RARITIES[t].name;
export const tierColor=(t:DropTier)=>t==='rainbow'?'#ffffff':RARITIES[t].color;
const ROLE_ICON={dps:'bot',tank:'shield',medic:'heart-plus'} as const;
const pct=(a:number,b:number)=>Math.max(0,Math.min(100,b>0?a/b*100:0)).toFixed(1);

/** Top-left money counter with floating gains (`data-gain` keeps each popup's node and animation). */
export function moneyHud(pve:PveView,me:string,popups:readonly MoneyPopup[]=[]){
 const row=pve.party[me];if(!row||row.kind==='bot')return '';
 return `<div class="pve-money" role="status" aria-label="Money ${formatCash(row.money)}"><span class="pve-money-icon" aria-hidden="true">$</span><b>${Math.round(row.money).toLocaleString('en-US')}</b><div class="pve-money-gains" aria-hidden="true">${popups.map(p=>`<span class="pve-gain ${p.amount<0?'spend':''}" data-gain="${escapeHtml(p.id)}" style="--gain-delay:${p.delay.toFixed(3)}s">${formatGain(p.amount)}</span>`).join('')}</div></div>`;
}

/** Top-centre stage line before the boss: OPENING, WAVE n / 3 · k LEFT, INTERMISSION 0:08. */
export function waveHeading(pve:PveView,time:number){
 if(pve.bossView||pve.stage==='boss'||pve.stage==='ended'||pve.cinematic)return '';
 const left=pve.stageUntil!==undefined?pve.stageUntil-time:undefined;
 if(pve.stage==='opening')return `<div class="pve-heading" data-stage="opening"><small>BOSS TEST · WARD ZERO</small><strong>Opening${left!==undefined?` <span>${clock(left)}</span>`:''}</strong><em>Choose your beginning chip</em></div>`;
 if(pve.stage==='intermission')return `<div class="pve-heading" data-stage="intermission"><small>WAVE ${pve.wave} / ${pve.waves} CLEARED</small><strong>${pve.wave>=pve.waves?'The Siren is coming':'Intermission'}${left!==undefined?` <span>${clock(left)}</span>`:''}</strong><em>Spend money at the machines · F</em></div>`;
 const done=pve.waveTotal>0?Math.max(0,pve.waveTotal-pve.waveLeft):0;
 return `<div class="pve-heading" data-stage="wave"><small>WAVE ${pve.wave} / ${pve.waves}</small><strong>${pve.waveLeft} <span>left</span></strong><i class="pve-wave-progress" aria-hidden="true"><b style="width:${pct(done,pve.waveTotal)}%"></b></i></div>`;
}

/** Top-centre boss bar (replaces .match-heading): name, fill, lag, notches, locked floor, mini chip track. */
export function bossBar(state:Snapshot,pve:PveView){
 const bv=pve.bossView,boss=state.players.find(p=>p.id===BOSS_PLAYER_ID);
 if(!bv||!boss||pve.stage==='ended')return '';
 const def=BOSSES[pve.boss],max=Math.max(1,boss.maxHp),hp=Math.max(0,boss.hp),hidden=!!bv.hidden;
 const locked=bv.phase<4&&hp<=bv.floor+.5&&!hidden;
 const notches=def.thresholds.map((t,i)=>`<i class="pve-notch ${i+1<bv.phase?'passed':''}" style="left:${(t*100).toFixed(1)}%"></i>`).join('');
 const slots=def.track.map((c,i)=>i<pve.track.revealed?`<span class="pve-boss-chip on" data-slot="${i}" title="${escapeHtml(c.name)}">${icon(c.icon)}</span>`:`<span class="pve-boss-chip" data-slot="${i}">?</span>`).join('');
 return `<div class="pve-boss-bar" data-phase="${bv.phase}" data-hidden="${hidden?1:0}" data-locked="${locked?1:0}"><div class="pve-boss-name"><span class="pve-boss-title" data-glitch="${escapeHtml(def.name.toUpperCase())}">${escapeHtml(def.name.toUpperCase())}</span><small>PHASE ${bv.phase} / 4</small></div><div class="pve-boss-track" role="meter" aria-label="${escapeHtml(def.name)} health" aria-valuemin="0" aria-valuemax="${Math.round(max)}" aria-valuenow="${Math.round(hp)}"><i class="pve-boss-lag" style="width:${pct(hp,max)}%"></i><i class="pve-boss-fill" style="width:${pct(hp,max)}%"></i>${bv.phase<4?`<i class="pve-boss-floor" style="left:${pct(bv.floor,max)}%"></i>`:''}${notches}</div><div class="pve-boss-meta"><span>${hidden?'HIDDEN — FIND HIM':locked?'PHASE LOCKED':`${Math.ceil(hp).toLocaleString('en-US')} / ${Math.round(max).toLocaleString('en-US')}`}</span><span class="pve-boss-chips">${slots}</span></div></div>`;
}

const alive=(p:PlayerView|undefined)=>!!p&&p.hp>0;
/** Bottom-left party panel: one row per member (humans and party bots), recruits nested under their hirer. */
export function partyHud(state:Snapshot,pve:PveView,me:string){
 const ids=Object.keys(pve.party);if(!ids.length)return '';
 const respawnHint=pve.stage==='boss'?'Respawns next phase':pve.stage==='ended'?'':'Respawns at wave clear';
 const rows=ids.map(id=>{
  const row=pve.party[id],p=state.players.find(x=>x.id===id),bot=row.kind==='bot',dead=!alive(p),name=p?.name??id;
  const color=/^#[\da-f]{6}$/i.test(p?.color||'')?p!.color:'#8a8f98',choosing=!!row.open&&!dead;
  let status='';
  if(!row.connected)status=`<span class="pve-member-status">${icon('x')}OFFLINE</span>`;
  else if(dead){
   const since=row.downAt!==undefined?clock(state.time-row.downAt):'';
   const self=row.selfReviveAt!==undefined&&row.selfReviveAt>state.time;
   status=`<span class="pve-member-status">${icon('skull')}DOWN${!bot&&!self?` · Revive ${formatCash(row.reviveCost||revivePrice(row.revived))}`:''}${since?` · ${since}`:''}</span><span class="pve-member-hint">${self?`SELF-REVIVE ${clock(row.selfReviveAt!-state.time)}`:respawnHint}</span>`;
  }else if(row.channel)status=`<span class="pve-member-status">${icon('heart-plus')}REVIVING ${escapeHtml(state.players.find(x=>x.id===row.channel!.target)?.name??'')}</span>`;
  else if(choosing)status=`<span class="pve-member-status choosing">${icon('cpu')}CHOOSING A CHIP</span>`;
  const recruits=pve.recruits.filter(r=>r.owner===id).map(r=>{const rp=state.players.find(x=>x.id===r.id);
   return `<div class="pve-recruit" data-recruit="${escapeHtml(r.id)}" data-role="${r.role}" data-down="${alive(rp)?0:1}">${icon(ROLE_ICON[r.role])}<span>${r.role==='dps'?'DPS':r.role==='tank'?'TANK':'MEDIC'}</span><i class="pve-hp mini"><b style="width:${rp?pct(rp.hp,rp.maxHp):'0'}%"></b></i></div>`;}).join('');
  return `<div class="pve-member" data-member="${escapeHtml(id)}" data-down="${dead?1:0}" data-me="${id===me?1:0}" data-bot="${bot?1:0}" data-choosing="${choosing?1:0}" style="--member-color:${color}"><div class="pve-member-line"><i class="pve-member-dot" aria-hidden="true"></i><strong>${escapeHtml(name)}</strong>${bot?'<em class="pve-tag">BOT</em>':id===me?'<em class="pve-tag you">YOU</em>':''}${choosing?`<span class="pve-member-chip" aria-label="Choosing a chip">${icon('cpu')}</span>`:''}<span class="pve-member-hpnum">${dead?'':Math.ceil(p!.hp)}</span></div><i class="pve-hp"><b style="width:${dead||!p?'0':pct(p.hp,p.maxHp)}%"></b></i>${status}${recruits}</div>`;
 }).join('');
 return `<section class="pve-party" aria-label="Party"><div class="pve-party-title">${icon('users')}PARTY</div>${rows}</section>`;
}

/** Bottom-right reopen icon for queued picks (Choose later). Pulses for a few seconds after a new pick arrives. */
export function chipLaterIcon(pve:PveView,me:string,time:number,open=false){
 const row=pve.party[me];if(!row||!row.picks.length||open||row.open)return '';
 const best=row.picks.reduce<DropTier>((b,p)=>TIER_ORDER.indexOf(p.tier)>TIER_ORDER.indexOf(b)?p.tier:b,'common');
 const newest=Math.max(...row.picks.map(p=>p.at)),fresh=time-newest<6&&time>=newest;
 return `<button class="pve-chip-pending ${fresh?'fresh':''}" data-action="pve-chips" data-tier="${best}" style="--tier:${tierColor(best)}" aria-label="Open chip selection, ${row.picks.length} waiting">${icon('cpu')}<span>CHIPS</span><b>${row.picks.length}</b><kbd>C</kbd></button>`;
}

export interface PveBannerSpec{key:string;title:string;sub?:string;tone:'wave'|'clear'|'boss'|'deny'|'respawn'|'victory'|'defeat'|'found'|'phase';at:number;duration:number}
const DENY:Record<string,string>={money:'NOT ENOUGH MONEY',pickups:'TAKE YOUR CHIPS FIRST',full:'ALREADY AT FULL HEALTH',cooldown:'STATION COOLING DOWN',
 seats:'NO SEATS FOR RECRUITS',limit:'RECRUIT LIMIT REACHED','offers-refreshed':'OFFERS REFRESHED'};
/** The most recent banner that is still showing: stage starts, wave clears, the boss spawn, possession, outros, and my
 *  own deny / respawn events. Pure in (state, me). */
export function bannerFor(state:Snapshot,pve:PveView,me:string):PveBannerSpec|null{
 const t=state.time,list:PveBannerSpec[]=[],c=pve.cinematic;
 if(c?.kind==='outro')list.push({key:'outro-'+c.at,title:c.outcome==='defeat'?'PARTY WIPED':'THE SIREN IS SILENCED',sub:c.outcome==='defeat'?'The Siren smiles':'Victory',tone:c.outcome==='defeat'?'defeat':'victory',at:c.at,duration:c.until-c.at+.5});
 if(!c){
  if(pve.stage==='wave')list.push({key:'wave-'+pve.wave+'-'+pve.stageAt,title:`WAVE ${pve.wave}`,sub:pve.wave>=pve.waves?'Final wave before the Siren':`${pve.waveTotal} minions incoming`,tone:'wave',at:pve.stageAt,duration:2.6});
  if(pve.stage==='intermission'&&pve.wave>0)list.push({key:'clear-'+pve.wave+'-'+pve.stageAt,title:'WAVE CLEARED',sub:formatGain(ECONOMY.waveClearBase+ECONOMY.waveClearPerWave*pve.wave)+' · fallen pilots respawn',tone:'clear',at:pve.stageAt,duration:3});
  if(pve.stage==='boss'&&pve.bossView?.phase===1)list.push({key:'spawned-'+pve.stageAt,title:'Siren has Spawned',tone:'boss',at:pve.stageAt,duration:3.2});
  const b=pve.bossView;
  if(b?.possessedAt!==undefined)list.push({key:'possess-'+b.possessedAt,title:'FIND ME...',sub:'The Siren hides inside a minion',tone:'phase',at:b.possessedAt,duration:3.2});
  if(b?.possessedAt!==undefined&&b.hidden&&t-b.possessedAt>=PVE_SIREN.possession.hints.strong){const k=Math.floor((t-b.possessedAt-PVE_SIREN.possession.hints.strong)/PVE_SIREN.possession.hints.pingEvery),at=b.possessedAt+PVE_SIREN.possession.hints.strong+k*PVE_SIREN.possession.hints.pingEvery;
   list.push({key:'whisper-'+at,title:'Find me...',tone:'phase',at,duration:PVE_SIREN.possession.hints.pingFor});}
  if(b?.foundAt!==undefined)list.push({key:'found-'+b.foundAt,title:'FOUND HIM',tone:'found',at:b.foundAt,duration:2});
 }
 for(const e of state.events){
  if(e.type==='pve-deny'&&e.target===me&&e.message)list.push({key:'deny-'+e.id,title:DENY[e.message]??e.message.toUpperCase(),tone:'deny',at:e.time,duration:1.4});
  else if(e.type==='pve-respawn'&&e.target===me)list.push({key:'respawn-'+e.id,title:'BACK IN THE FIGHT',sub:'Free respawn at 50% health',tone:'respawn',at:e.time,duration:2.2});
  else if(e.type==='pve-revive'&&e.target===me)list.push({key:'revive-'+e.id,title:'REVIVED',sub:e.message==='self'?'Self-revive used':undefined,tone:'respawn',at:e.time,duration:2});
 }
 const live=list.filter(b=>t>=b.at-.05&&t<b.at+b.duration);
 return live.sort((a,b)=>b.at-a.at)[0]??null;
}
/** Centre banner at 24% height; `delay` is the memoised negative animation delay for b.key. */
export function pveBanner(b:PveBannerSpec|null,delay=0){
 if(!b)return '';
 return `<div class="pve-banner" data-banner="${escapeHtml(b.key)}" data-tone="${b.tone}" style="--banner-delay:${delay.toFixed(3)}s;--banner-duration:${b.duration.toFixed(2)}s" role="status"><strong${b.tone==='boss'||b.tone==='phase'?` data-glitch="${escapeHtml(b.title)}"`:''}>${escapeHtml(b.title)}</strong>${b.sub?`<span>${escapeHtml(b.sub)}</span>`:''}</div>`;
}

/** Spectator text while I am down (§7.3). */
export function downLine(pve:PveView,me:string,time:number){
 const row=pve.party[me];if(!row)return '';
 if(row.selfReviveAt!==undefined&&row.selfReviveAt>time)return `Self-revive in ${clock(row.selfReviveAt-time)}`;
 const alive=Object.keys(pve.party).length>1;
 return `DOWN — ${alive?'a teammate can revive you · ':''}${pve.stage==='boss'?'you respawn at the next phase':'you respawn when the wave is cleared'}`;
}

/** Strong possession hint (§D13): a screen-edge arrow toward the possessed add for pingFor seconds every pingEvery,
 *  after hints.strong seconds. `yaw` is the camera yaw (forward = (sin yaw, cos yaw), as config.forward). */
export function possessionArrow(state:Snapshot,pve:PveView,me:string,yaw:number){
 const b=pve.bossView,h=PVE_SIREN.possession.hints;if(!b?.hidden||b.possessed===undefined||b.possessedAt===undefined||pve.cinematic)return '';
 const age=state.time-b.possessedAt;if(age<h.strong||(age-h.strong)%h.pingEvery>=h.pingFor)return '';
 const t=pve.adds.find(a=>a[0]===b.possessed),self=state.players.find(p=>p.id===me);if(!t||!self)return '';
 const a=decodeAdd(t,pve.slots),dx=a.pos.x-self.pos.x,dz=a.pos.z-self.pos.z,fx=Math.sin(yaw),fz=Math.cos(yaw),rx=-Math.cos(yaw),rz=Math.sin(yaw);
 const angle=Math.atan2(dx*rx+dz*rz,dx*fx+dz*fz),deg=(angle*180/Math.PI).toFixed(1),k=Math.floor((age-h.strong)/h.pingEvery);
 return `<div class="pve-possess-arrow" data-ping="${k}" style="--arrow-angle:${deg}deg" aria-hidden="true"><i></i><span>FIND ME...</span></div>`;
}

/** Text helpers shared with the chip screen and results. */
export const sourceLabel=(source:string,tier:DropTier)=>tier==='rainbow'?'RAINBOW · 8 MAX-RARITY CHOICES':source==='start'?'BEGINNING CHIP':source==='wave'?'WAVE REWARD · '+tierName(tier).toUpperCase():'DISPENSED · '+tierName(tier).toUpperCase();
export const PARTY_LIMIT=PVE.maxParty;

const TITLE_FRONT=[[14,1.1],[46,1.5],[78,1.2],[112,1.8],[146,1.3],[180,1.95],[214,1.4],[248,1.85],[282,1.25],[316,1.7],[350,1.2],[384,1.5]],
 TITLE_BACK=[[0,.9],[30,1.05],[62,.85],[96,1.2],[130,.95],[164,1.25],[198,1],[232,1.3],[266,.95],[300,1.2],[334,.9],[368,1.1],[400,.95]];
/** The animated boss name for the mid-fight intro (§6.3): per-letter reveal from --name-delay, the glitch split and black
 *  flames behind it. Goes inside `.intro-name`; no inline opacity or transform steps (model-ui pins the intro markup). */
export function bossIntroName(name:string){
 const title=name.toUpperCase(),letters=[...title].map((ch,i)=>`<i style="--i:${i}"${ch===' '?' class="gap"':''}>${ch===' '?'&nbsp;':escapeHtml(ch)}</i>`).join('');
 return `<small>BOSS · WARD ZERO</small><div class="siren-title-flames" aria-hidden="true">${flames(400,120,TITLE_FRONT,TITLE_BACK,'.9 .92')}</div><strong class="siren-title" data-glitch="${escapeHtml(title)}" aria-label="${escapeHtml(title)}">${letters}</strong><span class="siren-title-line">It was waiting behind the great doors</span>`;
}
