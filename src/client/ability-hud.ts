// Ability HUD contract (design §3.15; FOUNDATION minimal markup, PRESENTATION styles it). Pure functions of the snapshot.
import {abilityClient,kitFor,abilityActive,abilityCooldown,abilityCost,maxCharges,adsProfile,adsSettle,type HostCaps,type AdsProfile} from '../shared/abilities.js';
import {clamp,type SpecId} from '../shared/config.js';
import {statsFor,WEAPONS} from '../shared/content.js';
import type {PlayerView,Snapshot} from '../shared/simulation.js';
export interface AbilitySlotView{key:'Q'|'E';name:string;short:string;mode:'hold'|'press';state:'ready'|'cooldown'|'active'|'casting'|'nomana';
 remaining:number;max:number;fraction:number;cost?:number;affordable:boolean;charges?:{n:number;max:number};steady?:boolean}
/** Q and E cards for a non-DEV ranged/magic pilot on a capable host; [] otherwise (main.ts keeps the legacy E card). */
export function abilitySlots(me:PlayerView,time:number,caps:HostCaps|null|undefined):AbilitySlotView[]{
 if(!abilityClient(caps,me.spec))return [];const kit=kitFor(me.spec)!,stats=statsFor(me.chips,me.spec),w=WEAPONS[me.spec],magic=w.class==='magic',out:AbilitySlotView[]=[];
 const casting=(slot:'q'|'e')=>me.abilityCastAt!==undefined&&me.abilityCastAt>time&&(slot==='q'?(me.lastAbility??-10)>=me.lastSecondary:me.lastSecondary>(me.lastAbility??-10));
 const card=(key:'Q'|'E',name:string,short:string,remaining:number,max:number,cost:number,charges?:{n:number;max:number}):AbilitySlotView=>{
  cost*=stats.resourceCost;const affordable=!magic||me.mana>=cost,slot=key==='Q'?'q':'e',empty=charges?charges.n<=0:remaining>0;
  return {key,name,short,mode:'press',state:casting(slot)?'casting':!affordable?'nomana':empty&&remaining>0?'cooldown':'ready',remaining,max,fraction:max>0?Math.max(0,Math.min(1,1-remaining/max)):1,...(cost?{cost}:{}),affordable,...(charges?{charges}:{})};};
 if(kit.q.kind==='ads'&&kit.ads)out.push({key:'Q',name:kit.q.name,short:kit.q.short,mode:'hold',state:me.adsSince!==undefined?'active':'ready',remaining:0,max:0,fraction:1,affordable:true,steady:adsSettle(me,me.spec,time)>=1});
 else if(abilityActive(me.spec,'q'))out.push(card('Q',kit.q.name,kit.q.short,me.abilityCd??0,abilityCooldown(kit.q,stats),abilityCost(kit.q,stats)));
 if(me.chips.necronomicon&&magic)out.push(card('E','Raise the Dead','RAISE THE DEAD',me.secondaryCd,10,35));
 else if(abilityActive(me.spec,'e')){const max=maxCharges(stats);out.push(card('E',kit.e.name,kit.e.short,me.secondaryCd,abilityCooldown(kit.e,stats),abilityCost(kit.e,stats),max>1?{n:me.eCharges??max,max}:undefined));}
 else out.push(card('E','Special','SPECIAL',me.secondaryCd,w.secondaryCooldown,0));
 return out;
}
/** `.cooldown.ability` cards: `--p` is the recharge fraction and `data-state` the slot state. '' when there are no slots. */
export function abilitySlotsHtml(slots:AbilitySlotView[]){return slots.map(s=>{const label=s.mode==='hold'?s.state==='active'?s.steady?'STEADY':'AIMING':'HOLD':s.state==='casting'?'CASTING':s.state==='nomana'?'NO MANA':s.state==='cooldown'?s.remaining.toFixed(1)+'s':'READY';
 return `<div class="cooldown ability" data-slot="${s.key}" data-state="${s.state}" style="--p:${s.fraction.toFixed(3)}"><small>${s.key} · ${s.short}</small><strong class="${s.state==='ready'||s.state==='active'?'ready':''}">${label}${s.charges?` <b class="pips">${'◆'.repeat(s.charges.n)}${'◇'.repeat(Math.max(0,s.charges.max-s.charges.n))}</b>`:''}</strong>${s.cost?`<small>${Math.round(s.cost)} mana</small>`:''}</div>`;}).join('');}
/** Ground preview while the live point E is ready or casting (not Necronomicon's Raise the Dead). */
export function previewOn(me:PlayerView,time:number,caps:HostCaps|null|undefined){const e=abilitySlots(me,time,caps).find(s=>s.key==='E');return !!e&&!me.chips.necronomicon&&abilityActive(me.spec,'e')&&(e.state==='ready'||e.state==='casting');}
/** Q-held aim (§4.3): the spec's ADS profile on a capable host; without caps only today's rifle scope (FOV 28, ×.36). */
export interface AimState{scoped:boolean;profile:AdsProfile|null;sens:number}
export function aimState(caps:HostCaps|null|undefined,spec:SpecId|undefined,held:boolean):AimState{
 const on=abilityClient(caps,spec),profile=held&&on?adsProfile(spec!)??null:null,scoped=!!profile||held&&!on&&spec==='rifle';return {scoped,profile,sens:profile?profile.sens:scoped?.36:1};}
/** Rifle scope overlay (legacy text without caps); pistol/MG ADS use the tightening crosshair instead. */
export const scopeHtml=(a:AimState)=>a.scoped&&a.profile?.overlay!=='sight'?`<div class="scope-overlay" aria-label="Rifle scope active"><i></i><span>${a.profile?'3× SCOPE · RELEASE Q':'3× · RIFLE<br>RELEASE Q TO UNSCOPE'}</span></div>`:'';
const burst={start:-9,last:-9};
/** Crosshair classes: `.ads` with --settle (0..1 after the settle) and --bloom (MG bloom toward the hip spread). */
export function crosshairHtml(me:PlayerView,time:number,a:AimState){const p=a.profile;if(p?.overlay!=='sight')return '<div class="crosshair "></div>';
 const shot=me.lastShot??-9,w=WEAPONS[me.spec],hip=w.spread,ads=hip*p.spread;if(shot-burst.last>.25)burst.start=shot;burst.last=shot;
 const n=Math.round((shot-Math.max(burst.start,me.adsSince??shot))*(me.attackRate||1)/w.interval)+1,bloom=p.bloom&&me.adsSince!==undefined&&time-shot<=.25&&hip>ads?clamp(p.bloom*n/(hip-ads),0,1):0;
 const k=adsSettle(me,me.spec,time);return `<div class="ads-vignette" style="opacity:${(k*.12).toFixed(3)}"></div><div class="crosshair ads" style="--settle:${k.toFixed(2)};--bloom:${bloom.toFixed(2)}"></div>`;}
const markOn=(me:PlayerView,s:Snapshot)=>s.fields?.find(f=>f.fx==='orbital-mark'&&f.attach===me.id&&f.until>s.time);
/** SLOWED / SHACKLED / MARKED badges beside the DoT tags. */
export function abilityTags(me:PlayerView,s:Snapshot){const w=me.slowed,m=markOn(me,s),sh=w?.kind==='shackles';
 return (w&&w.until>s.time?`<span class="status-${sh?'shackles':'slow'}">${sh?'SHACKLED':'SLOWED'} ${(w.until-s.time).toFixed(1)}s</span>`:'')+(m?`<span class="status-marked">MARKED ${(m.until-s.time).toFixed(1)}s</span>`:'');}
/** First-person screen tints for slows, and the red edge pulse while marked (stronger from the lock). */
export function abilityScreen(me:PlayerView,s:Snapshot,first:boolean){const w=me.slowed,m=markOn(me,s);
 return (first&&w&&w.until>s.time?`<div class="dot-screen ${w.kind==='shackles'?'shackles':'slow'}" aria-hidden="true"></div>`:'')+(m?`<div class="dot-screen marked${s.time>=(m.lockAt??m.until)-.05?' lock':''}" aria-hidden="true"></div>`:'');}
