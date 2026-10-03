// PvE client session (design §7.8, §7.9, D33): the hello handshake, the per-room rejoin key, the WebSocket rejoin state
// machine, DEV fixture injection and debug op strings. No DOM; storage, randomness and time are injected for tests.
import type {GameEvent,PlayerView,Snapshot} from '../../shared/simulation.js';
import {BOSS_PLAYER_ID,PARTY_BOT_PREFIX,PVE_CONTRACT,type PveCommand,type PveView} from '../../shared/pve/contract.js';
import {PVE_FIXTURES,fixtureBossPlayer,type PveFixtureName} from '../../shared/pve/fixtures.js';

export interface KeyStore{getItem(k:string):string|null;setItem(k:string,v:string):void}
const randomHex=(bytes=16)=>{const a=new Uint8Array(bytes);crypto.getRandomValues(a);return [...a].map(b=>b.toString(16).padStart(2,'0')).join('');};
export const KEY_PREFIX='chip-arena-pve-key:';
/** The rejoin key for a room code: 32 hex chars, created once and kept in sessionStorage under the code. */
export function rejoinKey(code:string,store:KeyStore|undefined,random:()=>string=randomHex){
 const name=KEY_PREFIX+code;let key=store?.getItem(name)??null;
 if(!key||!/^[0-9a-f]{32,64}$/.test(key)){key=random();try{store?.setItem(name,key);}catch{}}
 return key;
}
/** Exactly one hello per room entry: reset on every welcome, sent the first time the snapshot carries options.pve. */
export class PveHello{
 private room='';private sent=false;
 /** A new room entry (welcome). */ enter(code:string){this.room=code;this.sent=false;}
 leave(){this.room='';this.sent=false;}
 get entered(){return this.room!=='';}
 /** The hello to send for this snapshot, or null. */
 update(state:Pick<Snapshot,'options'>,store?:KeyStore,random?:()=>string):PveCommand|null{
  if(this.sent||!this.room||!state.options.pve)return null;this.sent=true;
  return {action:'hello',v:PVE_CONTRACT,key:rejoinKey(this.room,store,random)};
 }
}
/** Distinct nonce per command (PartyRoom de-duplicates identical pending events). */
export class PveNonce{private n=0;wrap(cmd:PveCommand):{type:'chip';pve:PveCommand}{return {type:'chip',pve:{...cmd,n:++this.n}};}}

export type RejoinPhase='idle'|'waiting'|'connecting'|'failed'|'done';
export const REJOIN={delay:2000,attempts:3} as const;
/** Automatic rejoin for WebSocket-hosted PvE fights only (relay rooms cannot rejoin, D33): up to 3 attempts, 2 s apart. */
export class RejoinMachine{
 phase:RejoinPhase='idle';attempts=0;nextAt=0;error='';code='';
 reset(){this.phase='idle';this.attempts=0;this.nextAt=0;this.error='';this.code='';}
 /** The socket closed. Returns true when a rejoin starts. */
 lost(now:number,ctx:{websocket:boolean;pve:boolean;phase:string;left:boolean;code:string}){
  if(this.phase==='waiting'||this.phase==='connecting')return true;
  if(!ctx.websocket||!ctx.pve||ctx.phase!=='fight'||ctx.left||!ctx.code){this.phase='failed';this.error=ctx.websocket?'':'relay';return false;}
  this.code=ctx.code;this.attempts=0;this.phase='waiting';this.nextAt=now+REJOIN.delay;return true;
 }
 /** True when an attempt should open a socket now. */
 due(now:number){if(this.phase!=='waiting'||now<this.nextAt)return false;this.phase='connecting';this.attempts++;return true;}
 /** The attempt failed (socket error, close or a refusal). */
 failed(now:number,error=''){if(this.phase!=='connecting'&&this.phase!=='waiting')return;this.error=error;
  if(this.attempts>=REJOIN.attempts){this.phase='failed';return;}this.phase='waiting';this.nextAt=now+REJOIN.delay;}
 succeeded(){this.phase='done';this.error='';}
 get active(){return this.phase==='waiting'||this.phase==='connecting';}
}

/** Debug op strings for `{action:'debug',op}` (D29). */
export const DEBUG_OPS={skipWave:()=>'skip-wave',skipToBoss:()=>'skip-to-boss',phase:(n:number)=>`boss-phase:${Math.max(1,Math.min(4,Math.floor(n)))}`,
 money:(n:number)=>`money:${Math.round(n)}`,god:(on:boolean)=>`god:${on?'on':'off'}`,killAdds:()=>'kill-adds',spawn:(kind:string)=>`spawn:${kind}`} as const;

/** dt shifts the fixture's build time (e.g. +1.5 shows a reveal 0.5 s in); events are appended to every state (money popups). */
export interface FixtureInjectOptions{dt?:number;events?:Partial<GameEvent>[];
 /** Rebuild the view relative to every state's time (a frozen moment, for screenshots on slow renderers). */ freeze?:boolean}
const NAMES=['Ana','Kestrel','Party Bot','Juno'],COLORS=['#ff6b35','#6fd3ff','#9be36b','#ffcf5a'];
/** DEV fixture injection (§7.8): replaces `pve` on every received or sampled state. Party ids are the real local player
 *  first, then synthetic members (added as PlayerViews beside the local player so names and HP exist); boss fixtures add a
 *  boss PlayerView. Results fixtures also present phase 'match'. Built once per set() so cinematics play forward. */
export class PveFixtureInjector{
 name:string|null=null;private view:PveView|null=null;private extra:PlayerView[]=[];private phase:Snapshot['phase']|undefined;private intro:Snapshot['intro'];private events:GameEvent[]=[];private serial=0;private injected=new Set<string>();private frozen:{name:PveFixtureName;dt:number;ids:string[]}|null=null;
 get active(){return !!this.view;}
 set(name:PveFixtureName|PveView|null,state:Snapshot,me:string,o:FixtureInjectOptions={}){
  if(!name){this.frozen=null;this.name=null;this.view=null;this.extra=[];this.phase=undefined;this.intro=undefined;this.events=[];return null;}
  const players=state.players.filter(p=>!this.injected.has(p.id)),self=players.find(p=>p.id===me)??players[0];
  const ids=[me,'pve-fixture-2',PARTY_BOT_PREFIX+'9','pve-fixture-3'],time=state.time+(o.dt??0);
  this.events=(o.events??[]).map((e,i)=>({id:1e9+(++this.serial)*16+i,time:state.time,pos:self?.pos??{x:0,y:0,z:0},type:'pve-cash',target:me,...e}) as GameEvent);
  const view=typeof name==='string'?PVE_FIXTURES[name]({time,ids,seed:7}):name;
  this.frozen=o.freeze&&typeof name==='string'?{name,dt:o.dt??0,ids}:null;
  this.name=typeof name==='string'?name:'custom';this.view=view;this.phase=typeof name==='string'&&name.startsWith('results')?'match':undefined;
  this.extra=[];if(!self)return view;
  Object.keys(view.party).forEach((id,i)=>{if(players.some(p=>p.id===id))return;const row=view.party[id],dead=row.downAt!==undefined;
   const a=[2.2,-2.2,2.7,-2.7][i%4];this.extra.push({...self,id,name:NAMES[i%4],color:COLORS[i%4],bot:row.kind==='bot'?'normal':null,connected:row.connected,
    pos:{x:self.pos.x+Math.sin(self.yaw+a)*4.5,y:self.pos.y,z:self.pos.z+Math.cos(self.yaw+a)*4.5},hp:dead?0:Math.round(self.maxHp*[1,.62,.84,.35][i%4]),
    chips:{vital:1,ricochet:i%2+1,piercing:1},death:undefined,spec:(['pistol','sword','rifle','fire'] as const)[i%4],class:(['ranged','melee','ranged','magic'] as const)[i%4]});});
  const intro=view.cinematic?.kind==='boss-intro';
  if((view.bossView||intro)&&!players.some(p=>p.id===BOSS_PLAYER_ID))this.extra.push(fixtureBossPlayer(self,view.bossView?.phase??1,{time,ids}));
  for(const p of this.extra)this.injected.add(p.id);
  this.intro=intro?{started:view.cinematic!.at,players:[BOSS_PLAYER_ID],boss:true}:undefined;
  return view;
 }
 apply<T extends Snapshot>(state:T):T{
  if(!this.view)return state;
  if(this.frozen){const f=this.frozen,time=state.time+f.dt;this.view=PVE_FIXTURES[f.name]({time,ids:f.ids,seed:7});if(this.intro&&this.view.cinematic)this.intro={...this.intro,started:this.view.cinematic.at};}
  const players=[...state.players.filter(p=>!this.injected.has(p.id)).map(p=>{const row=this.view!.party[p.id];return row&&row.downAt!==undefined&&p.hp>0&&this.name!=='custom'?{...p,hp:0}:p;}),...this.extra];
  return {...state,players,events:this.events.length?[...state.events,...this.events]:state.events,options:{...state.options,pve:state.options.pve??{boss:'siren',seed:0}},pve:this.view,...(this.phase?{phase:this.phase}:{}),...(this.intro?{intro:this.intro}:{})};
 }
}
