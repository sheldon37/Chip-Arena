// TEST BOSS picker and the PvE lobby variant (design §7.1). Pure string builders; main.ts wraps them in its panel().
import type {PlayerView,Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,RECRUIT_PREFIX,pveOf} from '../../shared/pve/contract.js';
import {PVE} from '../../shared/pve/balance.js';
import {pilotCard,lobbyDensity} from '../lobby-ui.js';
import {sirenFace} from '../siren-card.js';
import {actionIcon,icon} from '../icons.js';
import {escapeHtml} from './hud.js';

/** Saved picker choices: additive keys in `chip-arena-preferences`. */
export interface BossPrefs{bossHostOnline:boolean;bossPartyBots:0|1|2|3;bossSkipWaves:boolean;bossAutoOpenChips:boolean}
export function bossPrefs(raw:Partial<Record<keyof BossPrefs,unknown>>={}):BossPrefs{
 const bots=Number(raw.bossPartyBots);
 return {bossHostOnline:raw.bossHostOnline===true,bossPartyBots:(Number.isInteger(bots)&&bots>=0&&bots<=PVE.maxPartyBots?bots:0) as 0|1|2|3,
  bossSkipWaves:raw.bossSkipWaves===true,bossAutoOpenChips:raw.bossAutoOpenChips!==false};
}
const button=(action:string,text:string,primary=false,extra='')=>`<button class="btn ${primary?'primary':''}" data-action="${action}" ${extra}>${actionIcon(action)}${text}</button>`;
export interface TestBossOptions{classOptions:string;prefs:BossPrefs;busy?:boolean;error?:string;
 /** How "Host online" will host on this build: desktop app, dedicated server or the website relay. */ hosting:'desktop'|'server'|'browser'}
/** Picker body (screen 'test-boss'). */
export function testBossBody(o:TestBossOptions){
 const p=o.prefs,where=o.hosting==='desktop'?'Your desktop app hosts the run; friends join with the room code.':o.hosting==='server'?'The game server hosts the run; friends join with the room code.':'Your browser hosts the run through the website relay; keep this tab open.';
 return `<div class="pve-picker"><label class="field">BOSS</label><div class="options pve-boss-options"><button class="class-option active pve-boss-card" data-boss="siren" aria-pressed="true"><span class="pve-boss-face">${sirenFace()}</span><strong>The Siren</strong><small>Ward Zero · survive 3 waves of minions, then the Siren</small></button><div class="pve-boss-card soon" aria-hidden="true"><span class="pve-boss-face">${icon('circle-help')}</span><strong>More bosses</strong><small>Coming later</small></div></div>${o.error?`<p class="connection-error" role="alert">${escapeHtml(o.error)}</p>`:''}<div class="pve-picker-options"><label class="settings-row">Host online — friends join with a room code <input type="checkbox" id="boss-host-online" ${p.bossHostOnline?'checked':''} ${o.busy?'disabled':''}/></label><p class="fine">${p.bossHostOnline?where:'Offline: the run plays on this computer only.'}</p><label class="settings-row" for="boss-party-bots">Party bots <select id="boss-party-bots">${[0,1,2,3].map(n=>`<option value="${n}" ${p.bossPartyBots===n?'selected':''}>${n}</option>`).join('')}</select></label><p class="fine">AI pilots fill the party (up to ${PVE.maxParty}). A friend who joins replaces the newest bot.</p><label class="settings-row">Start at the Siren <input type="checkbox" id="boss-skip-waves" ${p.bossSkipWaves?'checked':''}/></label><label class="settings-row">Open chip screen when I take a chip <input type="checkbox" id="boss-auto-open" ${p.bossAutoOpenChips?'checked':''}/></label></div><label class="field">YOUR CLASS</label>${o.classOptions}<p class="fine">Intros play as normal. Earn money from minions to buy chips, heals, revives and recruits. <kbd>F</kbd> interacts · <kbd>C</kbd> opens your chips.</p><div class="actions lobby-actions">${button('start-boss',o.busy?'Starting…':'Start Boss Fight',true,o.busy?'disabled':'')}${button('home','Back')}</div></div>`;
}
/** Party members for the lobby and results: humans and party bots (never the boss or recruits). */
export function partyPlayers(state:Snapshot):PlayerView[]{
 return state.players.filter(p=>p.id!==BOSS_PLAYER_ID&&!p.id.startsWith(RECRUIT_PREFIX));
}
export interface PveLobbyOptions{me:string;owner:string;code:string;codeLabel:string;codeReady:boolean;invite:string;local:boolean;classOptions:string;players:PlayerView[]}
/** Lobby body for a PvE room: roster (≤4), invite, class and Ready; no mode, sets, rounds, map or bot rows. */
export function pveLobbyBody(state:Snapshot,o:PveLobbyOptions){
 const pve=pveOf(state),helloed=new Set(pve?.helloed??[]),me=state.players.find(p=>p.id===o.me),players=o.players;
 const roster=players.map((p,i)=>{const bot=p.id.startsWith(PARTY_BOT_PREFIX)||!!p.bot,stale=!bot&&!!pve&&!helloed.has(p.id);
  return `<div class="lobby-pilot-slot" data-stale="${stale?1:0}">${pilotCard(p,bot?'BOT':p.id===o.me?'YOU':p.id===o.owner?'ROOM OWNER':'PLAYER',i,bot?true:p.ready)}${stale?'<p class="pve-needs-update">Needs the latest update</p>':''}</div>`;}).join('');
 const invite=o.local?'':`<div class="room-invite"><div><small>${escapeHtml(o.codeLabel)}</small><strong>${escapeHtml(o.code)}</strong></div><div class="actions">${button('copy-code','Copy Code',true,o.codeReady?'':'disabled')}${button('copy-invite','Copy Invite Link')}</div></div><details class="lobby-advanced"><summary>Invite link & connection</summary><label class="field" for="invite-link">INVITE LINK</label><input id="invite-link" class="input invite-url" readonly value="${escapeHtml(o.invite)}"/>${button('network-report','Connection report')}</details>`;
 return `<div class="lobby-room pve-lobby"><div class="lobby-roster-column"><div class="section-title"><h2>Party</h2><span>${players.length} / ${PVE.maxParty} · ${players.filter(p=>p.ready||p.bot).length} ready</span></div><div class="lobby-roster" data-density="${lobbyDensity(players.length)}" data-count="${players.length}">${roster}</div>${invite}<label class="field">YOUR CLASS</label>${o.classOptions}<div class="actions lobby-actions">${button('ready',me?.ready?'Cancel Ready':'Ready',!me?.ready)}${button('leave','Leave Room')}<span class="fine">The run starts when every pilot is ready.</span></div></div><aside class="lobby-room-settings pve-lobby-rules"><div class="eyebrow">BOSS TEST</div><h2>The Siren</h2><span class="pve-boss-face small">${sirenFace()}</span><p class="fine">Ward Zero · 3 waves of minions, then the Siren and his four chips.${state.options.pve?.skipWaves?' Starts at the Siren.':''}</p><p class="fine">${state.options.skipIntros?'Intros off':'Intros on'} · personal chip screens · money from kills.</p></aside></div>`;
}
