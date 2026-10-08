// Victory and defeat results (design §7.7, §6.8): one pilot card with a live portrait per party member, chips, damage and
// the rest of the run's stats; Retry and Return to Menu in the panel's bottom-centre sticky actions.
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,pveOf,type PvePartyView} from '../../shared/pve/contract.js';
import {RARITIES} from '../../shared/content.js';
import {cardChip} from '../../shared/pve/money-chips.js';
import {pilotCard} from '../lobby-ui.js';
import {chipIcon,icon,actionIcon} from '../icons.js';
import {rarityStyle} from '../chip-card.js';
import {escapeHtml,formatCash} from './hud.js';

const button=(action:string,text:string,primary=false,extra='')=>`<button class="btn ${primary?'primary':''}" data-action="${action}" ${extra}>${actionIcon(action)}${text}</button>`;
const n=(x:number)=>Math.round(x).toLocaleString('en-US');
/** Party members (humans and party bots) as PlayerViews, in party order; used for the portraits too. */
export function resultPlayers(state:Snapshot):PlayerView[]{
 const pve=pveOf(state);if(!pve)return [];
 return Object.keys(pve.party).map(id=>state.players.find(p=>p.id===id)).filter((p):p is PlayerView=>!!p);
}
const total=(r:PvePartyView)=>r.addDamage+r.bossDamage;
function chipBadges(p:PlayerView){
 const chips=Object.entries(p.chips||{}).filter(([,k])=>k>0).map(([id,k])=>({c:cardChip(id),k})).filter(x=>!!x.c);
 if(!chips.length)return '<span class="pve-result-nochips">No chips installed</span>';
 return chips.map(({c,k})=>`<span class="pve-result-chip rarity-${c!.rarity}" style="${rarityStyle(c!)}" title="${escapeHtml(c!.name)} · ${RARITIES[c!.rarity].name}">${chipIcon(c!.id)}<strong>${escapeHtml(c!.name)}</strong>${k>1?`<b>×${k}</b>`:''}</span>`).join('');
}
/** The results panel body plus its title (pure). `votes` is the latest rematch vote count (Retry). */
export function pveResults(state:Snapshot,me:string,opts:{votes?:number;peerLost?:boolean}={}){
 const pve=pveOf(state);if(!pve)return {title:'Run ended',eyebrow:'BOSS TEST',body:''};
 const victory=pve.outcome==='victory',players=resultPlayers(state),boss=state.players.find(p=>p.id===BOSS_PLAYER_ID);
 const mvp=players.reduce<PlayerView|undefined>((best,p)=>!best||total(pve.party[p.id])>total(pve.party[best.id])?p:best,undefined);
 const humans=state.players.filter(p=>p.connected&&!p.bot).length,votes=opts.votes??0;
 const cards=players.map((p,i)=>{const r=pve.party[p.id],bot=r.kind==='bot',crown=mvp?.id===p.id&&total(r)>0;
  const stats:[string,string][]=[['Damage',n(total(r))],['Boss damage',n(r.bossDamage)],['Kills',`${n(r.kills)}${r.heads?` <small>(${n(r.heads)} heads)</small>`:''}`],['Assists',n(r.assists)],['Revives',n(r.revives)],['Deaths',n(r.deaths)],...(bot?[]:[['Earned',formatCash(r.earned)]] as [string,string][])];
  return `<div class="pve-result" data-member="${escapeHtml(p.id)}" data-mvp="${crown?1:0}">${crown?`<span class="pve-mvp">${icon('sparkles')}MVP</span>`:''}${pilotCard(p,bot?'BOT':p.id===me?'YOU':'PILOT',i)}<div class="pve-result-chips">${chipBadges(p)}</div><dl class="pve-result-stats">${stats.map(([k,v])=>`<div><dt>${k}</dt><dd>${v}</dd></div>`).join('')}</dl></div>`;}).join('');
 const bossLeft=!victory&&boss&&boss.maxHp>0?`<p class="pve-result-boss">The Siren kept <strong>${Math.max(0,Math.round(boss.hp/boss.maxHp*100))}%</strong> health (${n(Math.max(0,boss.hp))} / ${n(boss.maxHp)}).</p>`:!victory&&pve.wave>0&&!pve.bossView?`<p class="pve-result-boss">The party fell on wave ${pve.wave} of ${pve.waves}.</p>`:'';
 const retry=opts.peerLost?'':button('retry','Retry',true);
 const waiting=votes>0&&votes<humans?`<span class="fine pve-retry-votes">Waiting for ${humans-votes} more…</span>`:'';
 const body=`<div class="pve-results" data-outcome="${victory?'victory':'defeat'}"><p class="pve-result-line">${victory?'The Siren is silenced':'The Siren smiles'}</p>${bossLeft}<div class="lobby-roster pve-result-grid" data-count="${players.length}">${cards}</div></div><div class="actions pve-result-actions">${retry}${button('leave','Return to Menu',true)}${waiting}</div>`;
 return {title:victory?'VICTORY':'DEFEAT',eyebrow:'BOSS TEST · THE SIREN',body};
}
