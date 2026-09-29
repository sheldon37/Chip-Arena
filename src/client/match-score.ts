import {TEAMS,sameTeam} from '../shared/teams.js';
import type {Snapshot,PlayerView} from '../shared/simulation.js';
const escape=(s:string)=>s.replace(/[&<>"']/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]!));
/** Awarded sets already include the winning round during result/draft. */
export function scoreCircles(state:Snapshot,p:PlayerView){
 const total=Math.max(1,Math.min(20,state.options.wins||5)),sets=Math.max(0,Math.min(total,p.score));
 const partial=state.setWinner?0:Math.max(0,Math.min(1,p.roundWins/(state.options.roundsPerSet||2)));
 return Array.from({length:total},(_,i)=>i<sets?1:i===sets?partial:0);
}
export function matchScoreHud(state:Snapshot,localId:string){
 const teamMode=state.options.gameMode==='teams',scorePlayers=teamMode?state.players.filter((p,i,all)=>all.findIndex(q=>q.team===p.team)===i):state.players;
 const alive=state.players.filter(p=>p.connected&&p.hp>0).length;
 return `<aside class="match-overview" style="--score-columns:${Math.min(10,Math.max(1,state.options.wins||5))}" aria-label="Match scores and surviving players"><section class="set-scoreboard"><div class="score-title">SET SCORE <small>${state.options.roundsPerSet===2?'◐ ROUND':'1/'+(state.options.roundsPerSet||2)+' PER ROUND'} · ● SET</small></div>${scorePlayers.map(p=>{
 const name=teamMode?TEAMS[p.team!].name+' team':p.name,you=p.id===localId||sameTeam(p,state.players.find(q=>q.id===localId),state.options.gameMode);
 const color=/^#[0-9a-f]{6}$/i.test(p.color)?p.color:'#dce7f5',fills=scoreCircles(state,p),rounds=state.setWinner?0:p.roundWins;
 return `<div class="score-pilot ${you?'is-you':''}" style="--pilot-color:${color}"><span class="score-name" title="${escape(name)}">${escape(name)}${you?'<small>YOU</small>':''}</span><span class="set-circles" role="img" aria-label="${escape(name)}: ${p.score} sets won${rounds?', '+rounds+' rounds toward the next set':''}">${fills.map(fill=>`<i class="set-circle ${fill===1?'complete':fill>0?'partial':'empty'}" style="--fill:${fill}turn" aria-hidden="true"></i>`).join('')}</span></div>`;
 }).join('')}</section><section class="alive-roster" aria-label="Players alive"><strong>${alive} / ${state.players.length} ALIVE</strong><div class="alive-grid">${state.players.map(p=>`<div style="--team-color:${teamMode?TEAMS[p.team!].color:p.color}" class="alive-pilot ${p.hp<=0||!p.connected?'out':''}" title="${escape(p.name)} — ${!p.connected?'Disconnected':p.hp<=0?'Eliminated':'Alive'}"><span>${escape(p.name)}${teamMode?'<em>'+TEAMS[p.team!].name+'</em>':''}</span><small>${!p.connected?'OFFLINE':p.hp<=0?'OUT':''}</small></div>`).join('')}</div></section></aside>`;
}
