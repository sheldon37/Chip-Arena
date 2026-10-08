// TEST BOSS picker and the PvE lobby variant (design §7.1). Pure string builders; main.ts wraps them in its panel().
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,RECRUIT_PREFIX,pveOf} from '../../shared/pve/contract.js';
import {PVE} from '../../shared/pve/balance.js';
import {pilotCard,lobbyDensity} from '../lobby-ui.js';
import {sirenFace} from '../siren-card.js';
import {actionIcon,icon} from '../icons.js';
import {escapeHtml} from './hud.js';
import {readySummary,readyStatus,roomAdvanced,roomAdvancedOpen} from '../room-options.js';

/** Saved picker choices: additive keys in `chip-arena-preferences`. */
export interface BossPrefs{bossHostOnline:boolean;bossPartyBots:0|1|2|3;bossSkipWaves:boolean}
export function bossPrefs(raw:Partial<Record<keyof BossPrefs,unknown>>={}):BossPrefs{
 const bots=Number(raw.bossPartyBots);
 return {bossHostOnline:raw.bossHostOnline===true,bossPartyBots:(Number.isInteger(bots)&&bots>=0&&bots<=PVE.maxPartyBots?bots:0) as 0|1|2|3,
  bossSkipWaves:raw.bossSkipWaves===true};
}
const button=(action:string,text:string,primary=false,extra='')=>`<button class="btn ${primary?'primary':''}" data-action="${action}" ${extra}>${actionIcon(action)}${text}</button>`;
export interface TestBossOptions{classOptions:string;prefs:BossPrefs;busy?:boolean;error?:string;
 /** How "Host online" will host on this build: desktop app, dedicated server or the website relay. */ hosting:'desktop'|'server'|'browser'}
/** Picker body (screen 'test-boss'). */
export function testBossBody(o:TestBossOptions){
 const p=o.prefs,where=o.hosting==='desktop'?'Your desktop app hosts the run; friends join with the room code.':o.hosting==='server'?'The game server hosts the run; friends join with the room code.':'Your browser hosts the run through the website relay; keep this tab open.';
 return `<div class="pve-picker"><label class="field">BOSS</label><div class="options pve-boss-options"><button class="class-option active pve-boss-card" data-boss="siren" aria-pressed="true"><span class="pve-boss-face">${sirenFace()}</span><strong>The Siren</strong><small>Ward Zero · survive 3 waves of minions, then the Siren</small></button><div class="pve-boss-card soon" aria-hidden="true"><span class="pve-boss-face">${icon('circle-help')}</span><strong>More bosses</strong><small>Coming later</small></div></div>${o.error?`<p class="connection-error" role="alert">${escapeHtml(o.error)}</p>`:''}<div class="pve-picker-options"><label class="settings-row">Host online — friends join with a room code <input type="checkbox" id="boss-host-online" ${p.bossHostOnline?'checked':''} ${o.busy?'disabled':''}/></label><p class="fine">${p.bossHostOnline?where:'Offline: the run plays on this computer only.'}</p><label class="settings-row" for="boss-party-bots">Party bots <select id="boss-party-bots">${[0,1,2,3].map(n=>`<option value="${n}" ${p.bossPartyBots===n?'selected':''}>${n}</option>`).join('')}</select></label><p class="fine">AI pilots fill the party (up to ${PVE.maxParty}). A friend who joins replaces the newest bot.</p><label class="settings-row">Start at the Siren <input type="checkbox" id="boss-skip-waves" ${p.bossSkipWaves?'checked':''}/></label></div><label class="field">YOUR CLASS</label>${o.classOptions}<p class="fine">Intros play as normal. Earn money from minions to buy chips, heals, revives and recruits. <kbd>F</kbd> interacts · <kbd>C</kbd> opens your chips; chips you take wait there until you choose.</p><div class="actions lobby-actions">${button('start-boss',o.busy?'Starting…':'Start Boss Fight',true,o.busy?'disabled':'')}${button('home','Back')}</div></div>`;
}
/** Home-screen Boss Test entry: a glossy black pill with a bone skull, set apart on the right of the main menu. */
const SKULL=`<svg viewBox="0 0 48 48" aria-hidden="true" focusable="false"><defs><linearGradient id="boss-pill-bone" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fbf9ff"/><stop offset=".62" stop-color="#d9d3e6"/><stop offset="1" stop-color="#9d95ad"/></linearGradient></defs><path fill="url(#boss-pill-bone)" d="M24 4.5C13.2 4.5 6.5 12.1 6.5 21.4c0 5.5 2.5 9.5 6.3 11.9v4.6a3.2 3.2 0 0 0 3.2 3.2h16a3.2 3.2 0 0 0 3.2-3.2v-4.6c3.8-2.4 6.3-6.4 6.3-11.9C41.5 12.1 34.8 4.5 24 4.5Z"/><path fill="#09080d" d="M12.6 22.6c-.1-3.2 2.5-5.2 5.5-4.8 2.8.4 4.2 2.8 3.5 5.6-.6 2.6-2.8 4.4-5.4 4.1-2.3-.3-3.5-2.2-3.6-4.9Zm22.8 0c.1-3.2-2.5-5.2-5.5-4.8-2.8.4-4.2 2.8-3.5 5.6.6 2.6 2.8 4.4 5.4 4.1 2.3-.3 3.5-2.2 3.6-4.9ZM24 27.8l-2.6 4.6h5.2Z"/><circle class="boss-pill-eye" cx="17.4" cy="22.8" r="1.7"/><circle class="boss-pill-eye" cx="30.6" cy="22.8" r="1.7"/><path fill="none" stroke="#09080d" stroke-width="1.5" stroke-linecap="round" d="M16.4 35.6h15.2M20 35.6v5.3M24 35.6v5.3M28 35.6v5.3"/></svg>`;
export function bossPill(){
 return `<button class="boss-pill" data-action="test-boss" aria-label="Boss test: fight the Siren"><span class="boss-pill-skull">${SKULL}</span><span class="boss-pill-text"><strong>BOSS TEST</strong><small>PvE · The Siren</small></span><span class="key">04</span></button>`;
}
/** Party members for the lobby and results: humans and party bots (never the boss or recruits). */
export function partyPlayers(state:Snapshot):PlayerView[]{
 return state.players.filter(p=>p.id!==BOSS_PLAYER_ID&&!p.id.startsWith(RECRUIT_PREFIX));
}
export interface PveLobbyOptions{me:string;owner:string;code:string;codeLabel:string;codeReady:boolean;invite:string;local:boolean;classOptions:string;players:PlayerView[];
 /** Advanced Options fold state; defaults to the viewer's remembered choice. */ advancedOpen?:boolean}
/** Lobby body for a PvE room: roster (≤4), invite, class and Ready; no mode, sets, rounds, map or bot rows. */
export function pveLobbyBody(state:Snapshot,o:PveLobbyOptions){
 const pve=pveOf(state),helloed=new Set(pve?.helloed??[]),me=state.players.find(p=>p.id===o.me),players=o.players;
 const roster=players.map((p,i)=>{const bot=p.id.startsWith(PARTY_BOT_PREFIX)||!!p.bot,stale=!bot&&!!pve&&!helloed.has(p.id);
  return `<div class="lobby-pilot-slot" data-stale="${stale?1:0}">${pilotCard(p,bot?'BOT':p.id===o.me?'YOU':p.id===o.owner?'ROOM OWNER':'PLAYER',i,bot?true:p.ready)}${stale?'<p class="pve-needs-update">Needs the latest update</p>':''}</div>`;}).join('');
 const invite=o.local?'':`<div class="room-invite"><div><small>${escapeHtml(o.codeLabel)}</small><strong>${escapeHtml(o.code)}</strong></div><div class="actions">${button('copy-code','Copy Code',true,o.codeReady?'':'disabled')}${button('copy-invite','Copy Invite Link')}</div></div>`;
 const advanced=o.local?'':roomAdvanced(`<label class="field" for="invite-link">INVITE LINK</label><input id="invite-link" class="input invite-url" readonly value="${escapeHtml(o.invite)}"/>${button('network-report','Connection report')}`,o.advancedOpen??roomAdvancedOpen(),'Invite link · connection report');
 const ready=players.filter(p=>p.ready||p.bot).length;
 return `<div class="lobby-room pve-lobby"><div class="lobby-roster-column"><div class="section-title"><h2>Party</h2><span>${players.length} / ${PVE.maxParty} joined</span>${readySummary(ready,players.length)}</div><div class="lobby-roster" data-density="${lobbyDensity(players.length)}" data-count="${players.length}">${roster}</div><label class="field">YOUR CLASS</label>${o.classOptions}<div class="actions lobby-actions">${button('ready',me?.ready?'Cancel Ready':'Ready',!me?.ready)}${button('leave','Leave Room')}<span class="fine">${readyStatus(ready,players.length,'pilot','The run starts when every pilot is ready.')}</span></div></div><aside class="lobby-room-settings pve-lobby-rules">${invite}<div class="eyebrow">BOSS TEST</div><h2>The Siren</h2><span class="pve-boss-face small">${sirenFace()}</span><p class="fine">Ward Zero · 3 waves of minions, then the Siren and his four chips.${state.options.pve?.skipWaves?' Starts at the Siren.':''}</p><p class="fine">${state.options.skipIntros?'Intros off':'Intros on'} · personal chip screens · money from kills.</p>${advanced}</aside></div>`;
}
