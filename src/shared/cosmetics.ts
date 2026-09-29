import {RAGDOLL_STYLES} from './ragdoll-styles.js';
import {AccessInput,canUse,accessOf} from './access.js';
import {INTRO_STYLES,IntroStyle} from './intros.js';
import {lobbyPose,type LobbyPose} from './lobby-presentation.js';
import {WEAPONS} from './content.js';
import type {SpecId} from './config.js';
export const HATS={'auditor-halo':'Auditor Halo · DEV ONLY',none:'No hat',cap:'Ball cap',beanie:'Beanie',top:'Top hat',wizard:'Wizard hat',crown:'Crown',cone:'Traffic cone',antenna:'Antenna',halo:'Halo',cowboy:'Cowboy hat'} as const;
export const KILL_STYLES={'dev-gibber':'Gibber · DEV ONLY',ragdoll:'Ragdoll',...RAGDOLL_STYLES,'dev-raging-demon':'Raging Demon · DEV ONLY','voxel-burst':'Voxel breakup',confetti:'Confetti cannon',ascension:'Beam me up',shrink:'Pocket-sized',launch:'Team Rocket',ghost:'Ghost escape','dev-clown':'Clown getaway · DEV ONLY','dev-clown-love':'Clown Love · DEV ONLY','dev-bass-van':'Bass Van · DEV ONLY'} as const;
export const developerKillEffect=(id:string)=>id.startsWith('dev-');
export const canUseKillEffect=(a:AccessInput|undefined,id:string)=>id==='dev-gibber'?accessOf(a).developer:!developerKillEffect(id)||canUse(a,'effect:'+id);
export interface Cosmetics {lobbyPose?:LobbyPose;preferredSpec?:SpecId;intro?:IntroStyle;introText?:string;bodyColor:string;hat:keyof typeof HATS;killEffect:keyof typeof KILL_STYLES;}
export const DEFAULT_COSMETICS:Cosmetics={lobbyPose:'weapon',intro:'neon-drop',introText:'Ready to make an impact',bodyColor:'#e5e7eb',hat:'none',killEffect:'voxel-burst'};
export function cosmetics(raw:any,dev:AccessInput=false):Cosmetics{return {lobbyPose:lobbyPose(raw?.lobbyPose),preferredSpec:typeof raw?.preferredSpec==='string'&&Object.hasOwn(WEAPONS,raw.preferredSpec)&&(!WEAPONS[raw.preferredSpec as SpecId].devOnly||canUse(dev,'weapon:'+raw.preferredSpec))?raw.preferredSpec:undefined,introText:introText(raw?.introText),intro:raw&&Object.hasOwn(INTRO_STYLES,raw.intro)&&(!raw.intro.startsWith('dev-')||canUse(dev,'intro:'+raw.intro))?raw.intro:'neon-drop',bodyColor:typeof raw?.bodyColor==='string'&&/^#[0-9a-f]{6}$/i.test(raw.bodyColor)?raw.bodyColor:DEFAULT_COSMETICS.bodyColor,hat:raw&&Object.hasOwn(HATS,raw.hat)&&(raw.hat!=='auditor-halo'||canUse(dev,'hat:auditor-halo'))?raw.hat:'none',killEffect:raw&&Object.hasOwn(KILL_STYLES,raw.killEffect)&&canUseKillEffect(dev,raw.killEffect)?raw.killEffect:'voxel-burst'};}

export function introText(value:unknown){return typeof value==='string'?Array.from(value.replace(/[\u0000-\u001f\u007f-\u009f\u202a-\u202e\u2066-\u2069]/g,' ').replace(/\s+/g,' ').trim()).slice(0,64).join('')||DEFAULT_COSMETICS.introText!:DEFAULT_COSMETICS.introText!;}
