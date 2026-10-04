/**
 * Development-only PvE world lab (stream S6). Renders the real Ward Zero dressing (src/client/siren/stage.ts) and the PvE
 * world view (src/client/pve/world-view.ts through the PveVisuals facade) as labelled frame sheets, driven by seeded
 * fixtures and synthetic PveViews that follow src/shared/pve/contract.ts. Not built by Vite and never imported from src.
 * With the dev server running, open /tools/pve-lab/index.html?<query>.
 *
 *  kind=overview   the 8 machines in Ward Zero (views=overview,m0..m7,chips,pods,meds)
 *  kind=dispense   a chip purchase at machine m (default 0) from t=0: flap, pop arc, rest. tier=<drop tier>
 *  kind=recruit    a recruit purchase at pod m (default 4) from t=0; the recruit (role=dps|tank|medic) exists at .8 s
 *  kind=pickups    one pickup of every drop tier, owned alternately by me and a friend
 *  kind=entries    entry e (vent 0-7, cell 8-11, door 12-15) opening from t=0 with a stand-in minion; mode=spawn|dive
 *  kind=holo       remote pilots choosing a chip: calm, and under attack (blink on and off)
 *  kind=beacon     downed pilots: waiting (F · REVIVE $), self-revive countdown, HELP bubbles
 *  kind=downed     a downed pilot's kept body (died at -6 s) and its regeneration on revive at t=0 (src/client/pve/downed.ts):
 *                  wound=decap|arm|torso|crush (Siren Last Laugh), back=place|pad (pad: back up 3.6 m away), views=downed,downed-wide
 *  kind=boss       the Siren at scale 1 and grown to 1.5 next to a pilot (growth over times); cutscene=1: Last Laugh frame
 *  frames, start, span | times=a,b,c   sample times; cell=<px>, aspect=<w/h>, quality=<0-1>, dt=<s>, warm=<s>
 */
import * as T from 'three';
import {createCharacterRig,type CharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter} from '../../src/client/character-animation.js';
import {arenaLighting} from '../../src/client/arena-lighting.js';
import {MATERIALS,type Vec} from '../../src/shared/config.js';
import type {PlayerView,Snapshot,GameEvent} from '../../src/shared/simulation.js';
import {sirenify,animateSiren,SirenEffects,SirenStageVisuals,SirenCutscene} from '../../src/client/siren/index.js';
import {WARD_ZERO_AIR} from '../../src/client/siren/stage.js';
import {SIREN,SIREN_TIMING,sirenPoint,type SirenWorldView,type SirenPlayerState} from '../../src/shared/siren-contract.js';
import {sirenStageBlocks,SIREN_STAGE_THEME} from '../../src/shared/siren-stage-layout.js';
import {createSirenMinion,animateSirenMinion,minionSeed,type SirenMinion} from '../../src/client/siren/minions/index.js';
import {PveVisuals} from '../../src/client/pve/visuals.js';
import {fixtureBase,fixtureParty} from '../../src/shared/pve/fixtures.js';
import {PVE_ENTRIES,PVE_MACHINES,STAND_Y} from '../../src/shared/pve/layout.js';
import {PVE_TIMING,BOSS_PLAYER_ID,type DropTier,type PveView,type RecruitRole,encodeAdd} from '../../src/shared/pve/contract.js';
import {PRICES,PVE_SIREN} from '../../src/shared/pve/balance.js';
import {fixtureAdd} from '../../src/shared/pve/fixtures.js';
import {DeathEffects} from '../../src/client/death-effects.js';
import {BodyWounds} from '../../src/client/body-wounds.js';
import {resolveBodyWound,deathMotion,projectileBodyImpact,meleeBodyImpact} from '../../src/shared/body-damage.js';
import {initPhysics,type DeathPresentation} from '../../src/shared/simulation.js';

const q=new URLSearchParams(location.search);
const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d;
const kind=q.get('kind')||'overview';
const sampleList=q.get('times')?.split(',').map(Number).filter(Number.isFinite);
const frames=sampleList?.length||Math.max(1,num('frames',kind==='overview'?1:6)),cell=num('cell',300),aspect=num('aspect',1.4),cellW=Math.round(cell*aspect);
const quality=num('quality',1),dt=num('dt',1/60),warm=num('warm',.5);
const status=document.getElementById('status')!;const sheet=document.getElementById('sheet') as HTMLCanvasElement;
const ME='me',FRIEND='p2';

function player(id:string,color:string,pos:Vec,yaw:number,extra:Partial<PlayerView>={}):PlayerView{
 return {id,name:id,color,class:'melee',spec:'sword',pos,vel:{x:0,y:0,z:0},yaw,pitch:0,hp:100,maxHp:100,mana:100,maxMana:100,ammo:0,maxAmmo:0,stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],connected:true,bot:null,ack:0,grounded:true,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],hitStunUntil:-1e9,team:'blue',...extra} as PlayerView;
}
interface Frame{players:PlayerView[];view:PveView;world?:SirenWorldView;events?:GameEvent[];adds?:{n:number;pos:Vec;yaw:number;vel:{x:number;z:number};spawnAt?:number}[]}
const emptyWorld=():SirenWorldView=>({patches:[],eruptions:[],rains:[],marks:[],grabs:[]});
const stand=(x:number,z:number,s=1):Vec=>({x,y:STAND_Y*s+(s-1)*0,z});
const fwd=(yaw:number)=>({x:Math.sin(yaw),z:Math.cos(yaw)});

// ---- scenarios (pure in t) ---------------------------------------------------------------------------------------
/** kind=downed: the authority's death record for the chosen wound (docs/BODY-DAMAGE-HANDOFF.md), facing +x, at -6 s. */
let downed:DeathPresentation|undefined;
function downedDeath():DeathPresentation{
 if(downed)return downed;const yaw=Math.PI/2,pos=stand(0,0),wound=q.get('wound')||'decap',at=-6;
 const world=(x:number,y:number,z:number)=>({x:pos.x+x*Math.cos(yaw)+z*Math.sin(yaw),y:pos.y+y,z:pos.z-x*Math.sin(yaw)+z*Math.cos(yaw)}),dirOf=(x:number,y:number,z:number)=>({x:x*Math.cos(yaw)+z*Math.sin(yaw),y,z:-x*Math.sin(yaw)+z*Math.cos(yaw)});
 const c=wound==='arm'?{impact:meleeBodyImpact('greatsword'),local:[.36,.12,.05],dir:[-1,0,-.15],damage:52,headshot:false,melee:true}
  :wound==='torso'?{impact:projectileBodyImpact({spec:'pistol',vel:{x:0,y:0,z:-52},radius:.045}),local:[-.05,.1,.3],dir:[0,0,-1],damage:23,headshot:false,melee:false}
  :{impact:projectileBodyImpact({spec:'rifle',vel:{x:0,y:0,z:-270},radius:.045}),local:[0,.64,.3],dir:[0,0,-1],damage:87,headshot:true,melee:false};
 const point=world(c.local[0],c.local[1],c.local[2]),direction=dirOf(c.dir[0],c.dir[1],c.dir[2]),velocity=dirOf(0,0,-.3);
 const fatal=resolveBodyWound({id:'lab-fatal',at,position:pos,yaw,point,direction,impact:c.impact,damage:c.damage,lethal:true,headshot:c.headshot});
 const crush=wound==='crush',motion=deathMotion({wound:fatal,grounded:true,incapacitated:false,yaw,velocity});
 return downed={wound:fatal,motion,id:'lab-death',at,effect:crush?'siren-crush':'impact-ragdoll',seed:4242,pos,velocity,direction,source:null,kind:crush?'siren-crush':'direct',yaw,hitPoint:point,melee:c.melee,force:350,gibPart:crush?'head':fatal.destroyedPart};
}
const M=num('m',kind==='recruit'?4:0),machine=PVE_MACHINES[M],tier=(q.get('tier')||'legendary') as DropTier,role=(q.get('role')||'dps') as RecruitRole;
const E=num('e',12),entry=PVE_ENTRIES[E],entryMode=(q.get('mode')||'spawn') as 'spawn'|'dive';
function base(t:number,ids=[ME,FRIEND]):PveView{return {...fixtureBase({time:t,ids}),party:fixtureParty({time:t,ids,kinds:ids.map(()=> 'alive')})};}
function scenario(t:number):Frame{
 const v=base(t);
 if(kind==='overview'){
  const players=[player(ME,'#3fc6ff',stand(0,8),Math.PI)];
  v.pickups=[{id:'pk1',owner:ME,tier:'epic',pos:{x:PVE_MACHINES[0].dispense.x,y:.9,z:PVE_MACHINES[0].dispense.z},born:t-3,m:0},{id:'pk2',owner:FRIEND,tier:'rainbow',pos:{x:PVE_MACHINES[3].dispense.x,y:.9,z:PVE_MACHINES[3].dispense.z},born:t-3,m:3}];
  return {players,view:v};}
 if(kind==='dispense'){
  const players=[player(ME,'#3fc6ff',stand(machine.front.x+fwd(machine.yaw).x*.4+fwd(machine.yaw+Math.PI/2).x*1.1,machine.front.z+fwd(machine.yaw).z*.4+fwd(machine.yaw+Math.PI/2).z*1.1),machine.yaw+Math.PI)];
  if(t>=0)v.machines=[{m:M,at:0,by:ME,result:'chip'}];
  if(t>=0)v.pickups=[{id:'pk1',owner:ME,tier,pos:{x:machine.dispense.x,y:.9,z:machine.dispense.z},born:PRICES.dispenseDelay,m:M}];
  return {players,view:v};}
 if(kind==='recruit'){
  const players=[player(ME,'#3fc6ff',stand(machine.front.x+fwd(machine.yaw+Math.PI/2).x*1.6+fwd(machine.yaw).x*.8,machine.front.z+fwd(machine.yaw+Math.PI/2).z*1.6+fwd(machine.yaw).z*.8),machine.yaw+Math.PI*.8)];
  if(t>=0)v.machines=[{m:M,at:0,by:ME,result:'recruit'}];
  const at=PRICES.recruitDelay;
  if(t>=at){v.recruits=[{id:'recruit-1',owner:ME,role,at,m:M}];players.push(player('recruit-1','#7fd0ff',stand(machine.dispense.x,machine.dispense.z),machine.yaw,{bot:'normal' as never,spec:role==='medic'?'pistol' as never:role==='tank'?'shield' as never:'rifle' as never,class:role==='dps'?'ranged':'melee',weaponAway:role==='medic'}));}
  return {players,view:v};}
 if(kind==='pickups'){
  const tiers:DropTier[]=['common','uncommon','rare','epic','legendary','mythic','rainbow'];
  v.pickups=tiers.map((tier,i)=>({id:'pk'+i,owner:i%2?FRIEND:ME,tier,pos:{x:-4.5+i*1.5,y:.9,z:2},born:-5,m:0}));
  return {players:[],view:v};}
 if(kind==='entries'){
  const en=entry,mode=entryMode,timing=en.kind==='door'?PVE_TIMING.door:en.kind==='cell'?PVE_TIMING.cell:undefined;
  const close=en.kind==='vent'?(mode==='dive'?PVE_TIMING.vent.dive:PVE_TIMING.vent.climb)+.8:timing!.open+timing!.walk+.8;
  if(t>=0)v.entries=[{e:E,openAt:0,closeAt:close,mode}];
  const adds:Frame['adds']=[];
  if(en.kind==='vent'){if(mode==='spawn')adds.push({n:1,pos:{x:en.exit.x,y:.86,z:en.exit.z},yaw:en.yaw,vel:{x:0,z:0},spawnAt:0});}
  else{const k=Math.max(0,Math.min(1,(t-timing!.open)/timing!.walk)),x=en.from.x+(en.exit.x-en.from.x)*k,z=en.from.z+(en.exit.z-en.from.z)*k,moving=t>timing!.open&&k<1;
   adds.push({n:1,pos:{x,y:.86,z},yaw:en.yaw,vel:moving?{x:(en.exit.x-en.from.x)/timing!.walk,z:(en.exit.z-en.from.z)/timing!.walk}:{x:0,z:0}});}
  v.adds=adds.map(a=>encodeAdd(fixtureAdd({n:a.n,x:a.pos.x,z:a.pos.z,yaw:a.yaw,vel:a.vel,state:'entering',entry:E,spawnAt:a.spawnAt}),v.slots));
  return {players:[],view:v,adds};}
 if(kind==='holo'){
  const ids=[ME,'calm','hit-a','hit-b'];const w=base(t,ids);w.party=fixtureParty({time:t,ids,kinds:['alive','choosing','attacked','attacked']});
  // hit-b is shown a quarter blink later (blink off): its hurt is offset so the phase differs.
  w.party['hit-b'].hurtAt=t-.3-1/12;
  const players=[player('calm','#3fc6ff',stand(-2.2,0),0),player('hit-a','#ffb03f',stand(0,0),0,{team:'blue'}),player('hit-b','#ff5fb0',stand(2.2,0),0),player(ME,'#cccccc',stand(0,30),0)];
  return {players,view:w};}
 if(kind==='beacon'){
  const ids=[ME,'down','reviving','self','medic'];const w=base(t,ids);w.party=fixtureParty({time:t,ids,kinds:['alive','dead','dead','self-reviving','alive']});
  w.party.self.downAt=t-2;w.party.self.selfReviveAt=t+3;
  // Complete death records (kept bodies are real ragdolls, pve/downed.ts); a fixed death time so the bodies are settled.
  const dead=(id:string,x:number)=>player(id,'#3fc6ff',stand(x,0),0,{hp:0,death:{id:'d-'+id,at:-6,effect:'impact-ragdoll',seed:7+x,pos:stand(x,0),velocity:{x:0,y:0,z:-.6},direction:{x:0,y:0,z:-1},source:null,kind:'environment',yaw:0}});
  const players=[dead('down',-3),dead('reviving',0),dead('self',3),player('medic','#7dffb0',stand(-.9,1),Math.PI*.8),player(ME,'#cccccc',stand(0,30),0)];
  return {players,view:w};}
 if(kind==='downed'){
  const ids=[ME,'down'],w=base(t,ids),up=t>=0,dead=downedDeath(),back=q.get('back')==='pad'?stand(3.6,-1.2):stand(dead.pos.x,dead.pos.z);
  w.party=fixtureParty({time:t,ids,kinds:['alive',up?'alive':'dead']});if(up)w.party.down.upAt=0;else w.party.down.downAt=dead.at;
  const players=[player(ME,'#cccccc',stand(0,30),0),player('down','#ff8a3f',up?back:dead.pos,up?2.2:dead.yaw,up?{hp:50}:{hp:0,death:dead})];
  return {players,view:w};}
 if(kind==='ventfall'||kind==='splat'){
  // Vent pits (src/shared/pve/vent-pits.ts): ventfall = 'down' walks over open vent 0 and falls in at t=0; splat = the
  // body lands at the hall centre (record at 0, impact at VENT_PIT.drop) and is revived in place at up=<s> (default none).
  const ids=[ME,'down'],w=base(t,ids),fall=kind==='ventfall',V=PVE_ENTRIES[0].from,at=fall?stand(V.x,V.z):stand(0,0),up=num('up',99),dead=fall?t>=0:t<up;
  w.party=fixtureParty({time:t,ids,kinds:['alive',dead?'dead':'alive']});if(dead)w.party.down.downAt=fall?0:-9.4;else if(!fall)w.party.down.upAt=up;
  if(fall)w.entries=[{e:0,openAt:-3,closeAt:30,mode:'dive'}];
  const death={id:fall?'d-fall':'d-splat',at:0,effect:fall?'vent-fall':'vent-splat',seed:7,pos:at,velocity:{x:0,y:-1,z:0},direction:{x:0,y:-1,z:0},source:null,kind:'vent-fall',yaw:.4,landAt:fall?10:.6} as DeathPresentation;
  const pos=fall&&t<0?stand(V.x+2.2*(-t),V.z):at;
  const players=[player(ME,'#cccccc',stand(0,30),0),player('down','#ff8a3f',pos,fall?-Math.PI/2:.4,dead?{hp:0,death}:{hp:50,...(fall?{vel:{x:-2.2,y:0,z:0}}:{})})];
  return {players,view:w};}
 if(kind==='possess'){
  // "Find me...": the boss dissolves at t=0 and its black flame streaks to add 5.
  const w=base(t),addPos={x:6,y:.86,z:-3};w.bossView={id:BOSS_PLAYER_ID,phase:2,floor:1000,hidden:t>=0,possessed:5,possessedAt:0};
  w.adds=[encodeAdd(fixtureAdd({n:5,x:addPos.x,z:addPos.z,yaw:-1}),w.slots)];
  const sp=player(BOSS_PLAYER_ID,'#b7a8ff',{x:0,y:STAND_Y,z:0},.6,{spec:SIREN.spec as never,team:'purple',siren:{mode:'melee',modeAt:-10} as never});
  const events=t>=0?[{id:1,time:0,type:'pve-possess',pos:{x:0,y:STAND_Y,z:0},target:'add-5'} as GameEvent]:[];
  return {players:[sp],view:w,events,adds:[{n:5,pos:addPos,yaw:-1,vel:{x:0,z:0}}]};}
 // boss
 const grown=Math.max(0,t),boost={scale:PVE_SIREN.beautiful.scale,reach:PVE_SIREN.beautiful.reach,cooldown:1,at:0};
 const siren=(id:string,x:number,b?:typeof boost):PlayerView=>{const s:SirenPlayerState={mode:'melee',modeAt:-10,...(b?{boost:b}:{})};const scale=b?b.scale:1;
  return player(id,'#b7a8ff',{x,y:STAND_Y+(scale-1)*.85,z:0},0,{spec:SIREN.spec as never,class:'melee',team:'purple',siren:s,maxHp:4000,hp:3000});};
 const w=base(t);w.bossView={id:BOSS_PLAYER_ID,phase:3,floor:1000};
 if(q.get('cutscene')){
  // Last Laugh by the grown boss: the catch at .55 s after the lunge, held, crushed.
  const L=SIREN_TIMING.laugh,hitAt=L.windup,endAt=hitAt+L.lunge,crushAt=endAt+L.crush,s=1.5;
  const b=siren(BOSS_PLAYER_ID,0,boost);b.pos={x:0,y:STAND_Y+.425,z:0};(b.siren as SirenPlayerState).move={id:'laugh',seq:1,at:0,hitAt,endAt,until:endAt+L.release,target:'victim',caught:'victim',caughtAt:endAt,crushAt};
  const world=emptyWorld();if(t>=endAt&&t<endAt+L.release)world.grabs.push({owner:BOSS_PLAYER_ID,victim:'victim',at:endAt,crushAt,releaseAt:endAt+L.release});
  const victim=player('victim','#3fc6ff',t>=endAt?sirenPoint(b.pos,0,{x:-.15*s,y:.55*s,z:.75*s}):{x:0,y:STAND_Y,z:2.6},Math.PI);
  return {players:[b,victim],view:w,world};}
 void grown;
 return {players:[siren('siren-1',-1.6),siren(BOSS_PLAYER_ID,1.4,boost),player('pilot','#3fc6ff',stand(-3.6,0),0)],view:w};
}

// ---- rendering ---------------------------------------------------------------------------------------------------
function snapshotAt(t:number,f:Frame):Snapshot{
 return {time:t,tick:Math.round(t*60),phase:'fight',remaining:60,round:1,setNumber:1,setWinner:null,winner:null,result:'',players:f.players,props:[],projectiles:[],events:f.events??[],impact:null,
  metrics:{bodies:0,projectiles:0,tickMs:0},options:{map:'siren',gameMode:'teams'},siren:f.world??emptyWorld(),bossSong:{track:'black-siren-protocol',startedAt:-140},pve:f.view} as unknown as Snapshot;
}
function label(ctx:CanvasRenderingContext2D,text:string,x:number,y:number){ctx.fillStyle='rgba(0,0,0,.6)';ctx.fillRect(x,y,Math.min(cellW,ctx.measureText(text).width+10),16);ctx.fillStyle='#ece8f4';ctx.fillText(text,x+5,y+12);}
const machineCam=(m:number,dist=4.2,side=.55,h=1.9):[number[],number[]]=>{const k=PVE_MACHINES[m],f=fwd(k.yaw),r=fwd(k.yaw+Math.PI/2);return [[k.x+f.x*dist+r.x*dist*side,h,k.z+f.z*dist+r.z*dist*side],[k.x+f.x*.6,1.3,k.z+f.z*.6]];};
function cameraFor(view:string):[number[],number[]]{
 if(view==='overview')return [[0,46,30],[0,0,2]];
 if(view==='high')return [[0,30,-2],[0,0,-1]];
 if(/^m\d$/.test(view))return machineCam(Number(view[1]));
 if(view==="machine")return machineCam(M,num("dist",4.2),num("side",-.6),num("height",1.9));
 if(view==='front')return machineCam(M,3.2,0,1.5);
 if(view==='pickups')return [[0,2.4,10.5],[0,1,2]];
 if(view==='entry'||view==='entry-side'){const en=entry,f=fwd(en.yaw),r=fwd(en.yaw+Math.PI/2),side=view==='entry-side'?1:.35,d=en.kind==='vent'?5:7.5;
  return [[en.exit.x+f.x*d+r.x*d*side,en.kind==='vent'?3.2:2.4,en.exit.z+f.z*d+r.z*d*side],[en.from.x+(en.exit.x-en.from.x)*.4,en.kind==='vent'?.3:1.5,en.from.z+(en.exit.z-en.from.z)*.4]];}
 if(view==='vent'){const en=entry,f=fwd(en.yaw);return [[en.from.x+f.x*3.2+1.2,3.4,en.from.z+f.z*3.2+.8],[en.from.x,-.4,en.from.z]];}
 if(view==='splat')return [[3.2,2.4,4.6],[0,.4,0]];
 if(view==='splat-wide')return [[0,5,17],[0,6,0]];
 if(view==='holo')return [[0,2.1,5.2],[0,1.6,0]];
 if(view==='beacon')return [[0,3.2,7.5],[0,.9,0]];
 if(view==='downed')return [[2.2,1.7,3.4],[1,.35,-.2]];
 if(view==='downed-wide')return [[1.8,3.1,7.2],[1.6,.8,-.4]];
 if(view==='boss')return [[0,1.9,7.8],[0,1.4,0]];
 if(view==='possess')return [[3,4.5,9],[3,1,-1.5]];
 if(view==='boss-side')return [[6.5,2.2,4.5],[0,1.4,0]];
 return [[0,46,30],[0,0,2]];
}
async function main(){
 await initPhysics();
 const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cellW,cell);gl.shadowMap.enabled=true;gl.shadowMap.type=T.PCFSoftShadowMap;
 gl.outputColorSpace=T.SRGBColorSpace;gl.toneMapping=T.ACESFilmicToneMapping;gl.toneMappingExposure=.92;
 const defaults:Record<string,string>={overview:'overview',dispense:'machine',recruit:'machine',pickups:'pickups',entries:'entry',holo:'holo',beacon:'beacon',boss:'boss',possess:'possess',downed:'downed,downed-wide',ventfall:'vent',splat:'splat-wide,splat'};
 const views=(q.get('views')||defaults[kind]).split(',').filter(Boolean),cut=!!q.get('cutscene'),rows=cut?1:views.length;
 const start=num('start',kind==='overview'?100:-.2),span=num('span',kind==='entries'?3:kind==='recruit'?1.8:1.2),times=sampleList?.length?sampleList:Array.from({length:frames},(_,i)=>start+(frames===1?0:span*i/(frames-1)));
 sheet.width=times.length*cellW;sheet.height=rows*cell+22;const ctx=sheet.getContext('2d')!;ctx.font='11px ui-monospace,monospace';ctx.fillStyle='#0c0b10';ctx.fillRect(0,0,sheet.width,sheet.height);
 ctx.fillStyle='#c9b8ff';ctx.fillText(`pve-lab · ${kind} · ${location.search.replace(/^\?/,'')}`,6,15);
 const draw=(r:number,c:number,text:string)=>{ctx.drawImage(gl.domElement,c*cellW,22+r*cell);label(ctx,text,c*cellW+2,24+r*cell);};

 const scene=new T.Scene();arenaLighting(scene,false);
 scene.background=new T.Color(WARD_ZERO_AIR.background);scene.fog=new T.Fog(WARD_ZERO_AIR.fog,WARD_ZERO_AIR.near,WARD_ZERO_AIR.far);scene.getObjectByName('night-moon')?.position.set(...WARD_ZERO_AIR.key);
 const props=new Map<string,T.Object3D>();
 for(const b of sirenStageBlocks()){const m=new T.Mesh(new T.BoxGeometry(b.size.x,b.size.y,b.size.z),new T.MeshLambertMaterial({color:b.material==='foundation'?SIREN_STAGE_THEME.floor:MATERIALS[b.material].color}));m.position.set(b.pos.x,b.pos.y,b.pos.z);m.castShadow=!b.id.startsWith('siren-roof-');m.receiveShadow=true;scene.add(m);props.set(b.id,m);}
 const stage=new SirenStageVisuals(scene);stage.setActive(true);
 const camera=new T.PerspectiveCamera(num('fov',42),cellW/cell,.05,300);scene.add(camera);
 const pve=new PveVisuals(scene),effects=new SirenEffects(scene),director=cut?new SirenCutscene():undefined,deaths=new DeathEffects(scene),wounds=new BodyWounds(scene);
 const rigs=new Map<string,CharacterRig>(),minions=new Map<number,SirenMinion>();
 const rigFor=(p:PlayerView)=>{let r=rigs.get(p.id);if(!r){r=createCharacterRig(p.color,undefined,true);if(p.spec===SIREN.spec)sirenify(r);r.group.traverse(o=>{if(o instanceof T.Mesh){o.castShadow=true;o.receiveShadow=true;}});scene.add(r.group);rigs.set(p.id,r);}return r;};
 let lastEvent=0;
 const step=(t:number,h:number)=>{
  const f=scenario(t),snap=snapshotAt(t,f),live=new Set(f.players.map(p=>p.id));
  for(const [id,r] of rigs)if(!live.has(id))r.group.visible=false;
  for(const p of f.players){const r=rigFor(p),dead=p.hp<=0&&p.death,at=dead?p.death!.pos:p.pos;r.group.visible=p.hp>0;r.group.position.set(at.x,at.y,at.z);r.group.rotation.y=p.yaw;r.weapon.visible=!p.weaponAway;
   animateCharacter(r,p,dead?p.death!.at:t,h,0);if(p.spec===SIREN.spec)animateSiren(r,p,snap,t,h,0);r.group.visible&&=pve.decoratePlayer(r,p,snap);}
  for(const a of f.adds??[]){let m=minions.get(a.n);if(!m){m=createSirenMinion(minionSeed('add-'+a.n));scene.add(m.rig.group);minions.set(a.n,m);}
   m.rig.group.position.set(a.pos.x,a.pos.y,a.pos.z);m.rig.group.rotation.y=a.yaw;animateSirenMinion(m,{yaw:a.yaw,vel:a.vel,spawnAt:a.spawnAt},t,quality);}
  for(const e of f.events??[])if(e.id>lastEvent){lastEvent=e.id;effects.event(e,t,quality);pve.event(e,snap);}
  effects.update(snap,t,rigs,quality,camera);stage.update(snap,t,quality,camera);wounds.update(snap,rigs,{quality,dt:h});deaths.update(snap,rigs,quality);
  pve.update(snap,{camera,me:ME,dt:h,quality,compatibility:false,replay:false,rigs,burst:()=>{}});
  return {f,snap};
 };
 let prev=times[0]-warm;
 for(let i=0;i<times.length;i++){
  for(let t=prev;t<times[i]-1e-9;t+=dt)step(t,dt);prev=times[i];const {snap}=step(times[i],1e-4);
  if(director){director.check(snap,q.get('cutscene')==='victim'?'victim':BOSS_PLAYER_ID);director.update(snap,camera,rigs);effects.update(snap,times[i],rigs,quality,camera);stage.update(snap,times[i],quality,camera);stage.shell(props);gl.render(scene,camera);draw(0,i,`cutscene ${times[i].toFixed(2)}s${director.active?'':' (off)'}`);continue;}
  for(let r=0;r<views.length;r++){const [p,l]=cameraFor(views[r]);camera.position.set(p[0],p[1],p[2]);camera.lookAt(l[0],l[1],l[2]);
   stage.update(snap,times[i],quality,camera);stage.shell(props);pve.update(snap,{camera,me:ME,dt:1e-4,quality,compatibility:false,replay:false,rigs,burst:()=>{}});
   gl.render(scene,camera);draw(r,i,`${views[r]} ${times[i].toFixed(2)}s`);}
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
main().catch(e=>{status.textContent='error: '+(e?.stack||e);(window as unknown as {labError:string}).labError=String(e?.stack||e);});
