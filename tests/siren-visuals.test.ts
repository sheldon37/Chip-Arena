import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {createCharacterRig} from '../src/client/character-rig.js';
import {sirenify,isSirenRig,animateSiren,sirenFirstPerson,SirenEffects,SirenIntroStage,SirenStageVisuals,SIREN_KILL_EFFECTS,markBeeps} from '../src/client/siren/index.js';
import {SIREN,SIREN_TIMING,SIREN_SLOTS,type SirenMoveId,type SirenMoveState,type SirenWorldView} from '../src/shared/siren-contract.js';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';

const player=(id:string,extra:Record<string,unknown>={}):PlayerView=>({id,name:id,color:'#b8b0ff',class:'melee',spec:'sword',pos:{x:0,y:.85,z:0},vel:{x:0,y:0,z:0},yaw:0,pitch:0,hp:100,maxHp:100,mana:0,maxMana:0,ammo:0,maxAmmo:0,stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],connected:true,bot:null,ack:0,grounded:true,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],...extra}) as unknown as PlayerView;
const snapshot=(time:number,players:PlayerView[],world?:SirenWorldView,projectiles:unknown[]=[]):Snapshot=>({time,tick:0,phase:'fight',remaining:60,round:1,setNumber:1,setWinner:null,winner:null,result:'',players,props:[],projectiles,events:[],impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options:{},siren:world,bossSong:{track:'black-siren-protocol',startedAt:0}}) as unknown as Snapshot;
function move(id:SirenMoveId):SirenMoveState{
 const t=SIREN_TIMING;switch(id){
  case 'swipe':case 'rend':return {id,seq:1,at:0,hitAt:t[id].windup,until:t[id].windup+t[id].active+t[id].recover,side:1};
  case 'rush':return {id,seq:1,at:0,hitAt:t.rush.windup,endAt:t.rush.windup+t.rush.dash,until:t.rush.windup+t.rush.dash+t.rush.recover,dir:{x:0,y:0,z:1}};
  case 'laugh':{const end=t.laugh.windup+t.laugh.lunge;return {id,seq:1,at:0,hitAt:t.laugh.windup,endAt:end,until:end+t.laugh.release,target:'v',caught:'v',caughtAt:end,crushAt:end+t.laugh.crush};}
  case 'mark':return {id,seq:1,at:0,endAt:t.mark.voice,hitAt:t.mark.voice+t.mark.lock,until:t.mark.voice+t.mark.lock+t.mark.recover,target:'v'};
  case 'eruption':case 'rain':return {id,seq:1,at:0,hitAt:t[id].windup,until:t[id].windup+t[id].recover,aim:{x:0,y:0,z:8}};
  default:{const m=t[id] as {windup:number;recover:number};return {id,seq:1,at:0,hitAt:m.windup,until:m.windup+m.recover,side:1};}
 }
}
const finite=(o:T.Object3D)=>{let ok=true;o.traverse(n=>{for(const v of [n.position.x,n.position.y,n.position.z,n.rotation.x,n.rotation.y,n.rotation.z,n.scale.x,n.scale.y,n.scale.z])if(!Number.isFinite(v))ok=false;});return ok;};

test('markBeeps accelerate inside the lock window',()=>{
 const b=markBeeps(1.2,2.4);assert.ok(b.length>=6);assert.equal(b[0],1.2);assert.ok(b.every(t=>t>=1.2&&t<2.4));
 for(let i=2;i<b.length;i++)assert.ok(b[i]-b[i-1]<=b[i-1]-b[i-2]+1e-9&&b[i]-b[i-1]>=.07-1e-9);
});

test('sirenify re-skins the rig once and hides weapons and cosmetics',()=>{
 const rig=createCharacterRig('#ff6b35',undefined,true);sirenify(rig);sirenify(rig);assert.ok(isSirenRig(rig));
 for(const s of [rig.weapon,rig.offWeapon,rig.sockets.head,rig.sockets.back])assert.equal(s.visible,false);
 const names:string[]=[];rig.group.traverse(o=>names.push(o.name));
 assert.equal(names.filter(n=>n==='siren-eye').length,2);assert.equal(names.filter(n=>n==='siren-claws').length,2);assert.equal(names.filter(n=>n==='siren-apparition').length,1,'built once');
});

test('every move animates to finite poses for its whole timeline',()=>{
 const all=[...Object.values(SIREN_SLOTS).flatMap(s=>[s.primary,...s.abilities])] as SirenMoveId[];
 for(const id of all){const rig=createCharacterRig('#b8b0ff',undefined,true);sirenify(rig);const m=move(id);
  for(let t=-.2;t<=m.until+.3;t+=.05){const p=player(SIREN.spec,{siren:{mode:['hellfire','damnation','eruption','rain','mark'].includes(id)?'ranged':'melee',modeAt:-5,move:m}});animateSiren(rig,p,snapshot(t,[p]),t,1/60,0);}
  assert.ok(finite(rig.group),`${id} produced a non-finite transform`);}
});

test('mode swap retracts the claws and lights the palm orbs',()=>{
 const rig=createCharacterRig('#b8b0ff',undefined,true);sirenify(rig);const claws:T.Object3D[]=[],orbs:T.Object3D[]=[];rig.group.traverse(o=>{if(o.name==='siren-claws')claws.push(o);if(o.name==='siren-orb')orbs.push(o);});
 const at=(t:number,mode:'melee'|'ranged')=>{const p=player(SIREN.spec,{siren:{mode,modeAt:0}});animateSiren(rig,p,snapshot(t,[p]),t,1/60,0);};
 at(2,'melee');assert.ok(claws.every(c=>c.visible&&c.scale.y>.9));assert.ok(orbs.every(o=>!o.visible));
 at(2,'ranged');assert.ok(claws.every(c=>c.scale.y<.5));assert.ok(orbs.every(o=>o.visible));
});

test('effects create and retire their world pieces with the snapshot',()=>{
 const scene=new T.Scene(),fx=new SirenEffects(scene),siren=player(SIREN.spec,{siren:{mode:'ranged',modeAt:-5}}),victim=player('v',{pos:{x:0,y:.85,z:3}});
 const rigs=new Map([[siren.id,createCharacterRig('#b8b0ff')],[victim.id,createCharacterRig('#3fc6ff')]]);sirenify(rigs.get(siren.id)!);
 const world:SirenWorldView={patches:[{id:'p',owner:siren.id,kind:'hellfire',pos:{x:2,y:0,z:2},radius:1.1,at:0,until:10}],eruptions:[{id:'e',owner:siren.id,pos:{x:0,y:0,z:8},radius:2.2,at:0,burstAt:1,until:1.5}],rains:[{id:'r',owner:siren.id,center:{x:0,y:0,z:9},radius:7,at:0,until:10}],marks:[{id:'m',owner:siren.id,target:'v',at:0,lockAt:.1,throwAt:1,shot:'s'}],grabs:[{owner:siren.id,victim:'v',at:0,crushAt:3.2,releaseAt:3.8}]};
 const shot={id:'s',owner:siren.id,spec:'sword',pos:{x:0,y:1,z:2},vel:{x:0,y:0,z:3},radius:.45,reflections:0,elements:[],siren:{kind:'seeker',born:1,target:'v'}};
 assert.ok(fx.owns(shot));assert.ok(!fx.owns({id:'x'}));
 for(let t=0;t<2;t+=1/30)fx.update(snapshot(t,[siren,victim],world,[shot]),t,rigs,1);
 for(const type of ['siren-swipe','siren-rend','siren-slam','siren-explode','siren-eruption','siren-drop','siren-crush','siren-hit','siren-seeker-out'])fx.event({id:1,time:2,type,pos:{x:0,y:1,z:2},source:siren.id,amount:1,radius:2} as never,2,1);
 const before=scene.children[0].children.length;assert.ok(before>4,'pieces were created');
 for(let t=2;t<4;t+=1/30)fx.update(snapshot(t,[siren,victim],{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},[]),t,rigs,1);
 assert.ok(scene.children[0].children.length<before,'pieces were retired');fx.clear();
});

test('intro, stage and kill effects run their whole timelines',()=>{
 const scene=new T.Scene(),camera=new T.PerspectiveCamera(40,16/9,.1,100),intro=new SirenIntroStage(scene),rig=createCharacterRig('#b8b0ff');
 const light=new T.HemisphereLight(0xffffff,0x000000,.6);scene.add(light);
 for(let t=0;t<7.3;t+=.1)intro.update(t,rig,camera,1);assert.equal(light.intensity,0,'the intro owns the lights');intro.setActive(false);assert.equal(light.intensity,.6,'and gives them back');
 const stage=new SirenStageVisuals(scene);stage.setActive(true);for(let t=0;t<200;t+=.5)stage.update(snapshot(t,[]),t,1,camera);stage.setActive(false);
 for(const [name,factory] of Object.entries(SIREN_KILL_EFFECTS)){const original=createCharacterRig('#3fc6ff');if(name===SIREN.deaths.dissipate)sirenify(original);
  const p=player('v',{hp:0});const fx=factory(scene,original.group,p,{id:'d',at:0,effect:name,seed:3,pos:{x:0,y:1.3,z:0},velocity:{x:0,y:0,z:0},direction:{x:0,y:0,z:1},source:SIREN.spec,kind:'melee',yaw:0,gibPart:'head'} as never,1,200);
  for(let a=0;a<5;a+=1/30)fx.update(a);fx.dispose();}
});

test('first person restores the normal arms for other specs',()=>{
 const view=createCharacterRig('#70e1d0',undefined,true),arm=view.rightArm.children.find(o=>o instanceof T.Mesh) as T.Mesh,before=arm.material;
 sirenFirstPerson(view,player(SIREN.spec,{spec:SIREN.spec,siren:{mode:'melee',modeAt:-5}}),1,1/60);assert.notEqual(arm.material,before);
 sirenFirstPerson(view,player('me'),1.1,1/60);assert.equal(arm.material,before);
});
