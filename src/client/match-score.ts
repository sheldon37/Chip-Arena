import type {Snapshot,PlayerView} from '../shared/simulation.js';
const escape=(s:string)=>s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
/** Awarded sets already include the winning round during result/draft. */
export function scoreCircles(state:Snapshot,p:PlayerView){
 const total=Math.max(1,Math.min(20,state.options.wins||5)),sets=Math.max(0,Math.min(total,p.score));
 const partial=state.setWinner?0:Math.max(0,Math.min(1,p.roundWins/(state.options.roundsPerSet||2)));
 return Array.from({length:total},(_,i)=>i<sets?1:i===sets?partial:0);
}
export function matchScoreHud(state:Snapshot,localId:string){
 const alive=state.players.filter(p=>p.connected&&p.hp>0).length;
 return `<aside class="match-overview" aria-label="Match scores and surviving players"><section class="set-scoreboard"><div class="score-title">SET SCORE <small>◐ ROUND · ● SET</small></div>${state.players.map(p=>{
 const color=/^#[0-9a-f]{6}$/i.test(p.color)?p.color:'#dce7f5',fills=scoreCircles(state,p),rounds=state.setWinner?0:p.roundWins;
 return `<div class="score-pilot ${p.id===localId?'is-you':''}" style="--pilot-color:${color}"><span class="score-name" title="${escape(p.name)}">${escape(p.name)}${p.id===localId?'<small>YOU</small>':''}</span><span class="set-circles" role="img" aria-label="${escape(p.name)}: ${p.score} sets won${rounds?', '+rounds+' rounds toward the next set':''}">${fills.map(fill=>`<i class="set-circle ${fill===1?'complete':fill>0?'partial':'empty'}" style="--fill:${fill}turn" aria-hidden="true"></i>`).join('')}</span></div>`;
 }).join('')}</section><section class="alive-roster" aria-label="Players alive"><strong>${alive} / ${state.players.length} ALIVE</strong><div class="alive-grid">${state.players.map(p=>`<div class="alive-pilot ${p.hp<=0||!p.connected?'out':''}" title="${escape(p.name)} — ${!p.connected?'Disconnected':p.hp<=0?'Eliminated':Math.ceil(p.hp)+' HP'}"><span>${escape(p.name)}</span><small>${!p.connected?'OFFLINE':p.hp<=0?'OUT':Math.ceil(p.hp)+' HP'}</small></div>`).join('')}</div></section></aside>`;
}
