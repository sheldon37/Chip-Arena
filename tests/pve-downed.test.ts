// PvE downed bodies (owner 2026-10-03; src/client/pve/downed.ts): a downed party member's body stays where it fell (also
// gibbed), a bobbing HELP bubble rides on the revive beacon for everyone but the downed player, and coming back (paid
// revive, self-revive, respawn) plays a holographic regeneration that seals the wounds, rebuilds missing parts and stands
// the player up from the body (a distant respawn pad dissolves the old body and materialises the player there).
// Real Rapier ragdolls, PvE fixture views and the real Economy for the authoritative `upAt` stamp.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {v} from '../src/shared/config.js';
import {Simulation,initPhysics,type DeathPresentation,type Player,type PlayerView,type Snapshot} from '../src/shared/simulation.js';
import {resolveBodyWound,deathMotion,projectileBodyImpact,meleeBodyImpact} from '../src/shared/body-damage.js';
import {fixtureBase,fixtureParty,type PartyRowKind} from '../src/shared/pve/fixtures.js';
import {PVE_EVENT,type PveView} from '../src/shared/pve/contract.js';
import {revivePrice} from '../src/shared/pve/balance.js';
import {PVE_LAYOUT} from '../src/shared/pve/layout.js';
import {Economy} from '../src/shared/pve/economy.js';
import {DeathEffects} from '../src/client/death-effects.js';
import {PveVisuals} from '../src/client/pve/visuals.js';
import {DOWNED,REGEN_SECONDS,bodyKept,helpShown,helpBob,helpWidth,regenAge,regenPhase,regenMode,voxelArrival,jointSubtree,downedBodies} from '../src/client/pve/downed.js';
import {SIREN_CRUSH_KEEP} from '../src/client/siren/kills.js';
import {createCharacterRig,type CharacterRig} from '../src/client/character-rig.js';
import {animateCharacter} from '../src/client/character-animation.js';
import {BodyWounds} from '../src/client/body-wounds.js';
import {fakeParts,withPveParts} from './helpers/pve-fakes.js';
await initPhysics();

const near=(a:number,b:number,eps=1e-9)=>Math.abs(a-b)<=eps;
function pilot(id:string,extra:Partial<PlayerView>={}):PlayerView{
 return {id,name:id,color:'#3fc6ff',class:'ranged',spec:'pistol',pos:{x:0,y:.86,z:0},vel:{x:0,y:0,z:0},yaw:0,pitch:0,hp:100,maxHp:100,mana:100,maxMana:100,ammo:12,maxAmmo:12,stamina:100,maxStamina:100,
  blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],
  connected:true,bot:null,ack:0,grounded:true,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],hitStunUntil:-1e9,team:'blue',...extra} as PlayerView;
}
const snap=(time:number,pve:PveView|undefined,players:PlayerView[]):Snapshot=>({time,tick:0,phase:'fight',remaining:60,round:1,setNumber:1,generation:1,setWinner:null,winner:null,result:'',players,props:[],projectiles:[],events:[],
 impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options:{map:'siren'},siren:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},...(pve?{pve}:{})}) as unknown as Snapshot;
const view=(time:number,ids:string[],kinds:PartyRowKind[]):PveView=>({...fixtureBase({time,ids}),party:fixtureParty({time,ids,kinds})});

// ---- deaths: the authority's body-damage records (docs/BODY-DAMAGE-HANDOFF.md) ----
const at0=v(0,.85,0),rifle=(speed:number)=>projectileBodyImpact({spec:'rifle',vel:v(0,0,-speed),radius:.045});
const CASES={decap:{impact:rifle(270),local:v(0,.64,.3),dir:v(0,0,-1),damage:87,headshot:true,melee:false},
 arm:{impact:meleeBodyImpact('greatsword'),local:v(.36,.12,.05),dir:v(-1,0,-.15),damage:52,headshot:false,melee:true},
 torso:{impact:projectileBodyImpact({spec:'pistol',vel:v(0,0,-52),radius:.045}),local:v(-.05,.1,.3),dir:v(0,0,-1),damage:23,headshot:false,melee:false}};
function death(id:string,at:number,kind:keyof typeof CASES,pos=at0,effect='impact-ragdoll'):DeathPresentation{
 const c=CASES[kind],point=v(pos.x+c.local.x,pos.y+c.local.y,pos.z+c.local.z);
 const wound=resolveBodyWound({id:'fatal-'+id,at,position:pos,yaw:0,point,direction:c.dir,impact:c.impact,damage:c.damage,lethal:true,headshot:c.headshot});
 const velocity=v(0,0,-.3),motion=deathMotion({wound,grounded:true,incapacitated:false,yaw:0,velocity});
 return {wound,motion,id:'death-'+id+'-'+at,at,effect,seed:4242,pos,velocity,direction:c.dir,source:'horde',kind:'direct',yaw:0,hitPoint:point,melee:c.melee,force:350,
  gibPart:effect==='siren-crush'?'head':wound.destroyedPart};
}
/** The renderer's per-frame order: rigs posed (decoratePlayer may hide them), living wounds, death effects, PvE. */
class Frame{
 scene=new T.Scene();deaths=new DeathEffects(this.scene);pve=new PveVisuals(this.scene);layer=new BodyWounds(this.scene);rigs=new Map<string,CharacterRig>();
 camera=new T.PerspectiveCamera();bursts=0;shown=new Map<string,boolean>();
 constructor(readonly quality=1,readonly compatibility=false){this.camera.position.set(0,4,12);}
 run(state:Snapshot,me='me'){
  for(const p of state.players){let r=this.rigs.get(p.id);if(!r){r=createCharacterRig(p.color);this.scene.add(r.group);this.rigs.set(p.id,r);}
   r.group.visible=p.hp>0;const pos=p.death&&p.hp<=0?p.death.pos:p.pos;r.group.position.set(pos.x,pos.y,pos.z);r.group.rotation.y=p.yaw;
   animateCharacter(r,p,p.death&&p.hp<=0?p.death.at:state.time,1/60);if(r.group.visible)r.group.visible=this.pve.decoratePlayer(r,p,state);this.shown.set(p.id,r.group.visible);}
  this.layer.update(state,this.rigs,{quality:this.quality,dt:1/60});this.deaths.update(state,this.rigs,this.quality);
  this.pve.update(state,{camera:this.camera,me,dt:1/60,quality:this.quality,compatibility:this.compatibility,replay:false,rigs:this.rigs,burst:()=>{this.bursts++;}});
 }
 named(name:string){const out:T.Object3D[]=[];this.scene.traverse(o=>{if(o.name===name)out.push(o);});return out;}
 get keeper(){return downedBodies(this.scene);}
}
const worldPos=(o:T.Object3D)=>{o.updateWorldMatrix(true,false);return o.getWorldPosition(new T.Vector3());};
/** Not drawn any more: hidden (itself or an ancestor) or scaled away. */
const sealed=(o:T.Object3D)=>{for(let a:T.Object3D|null=o;a;a=a.parent)if(!a.visible)return true;o.updateWorldMatrix(true,false);return o.getWorldScale(new T.Vector3()).x<.05;};

// ---- pure rules -----------------------------------------------------------------------------------------------------
test('rules: a connected party member keeps a body while down (also through the end outro); HELP shows to others only, never after the run',()=>{
 const t=50,v0=view(t,['me','p2','bot','gone'],['alive','dead','bot','disconnected']),dead=(id:string)=>pilot(id,{hp:0,death:death(id,t-3,'torso')});
 assert.equal(bodyKept(dead('p2'),v0),true);assert.equal(bodyKept(dead('bot'),v0),true,'party bots too');
 assert.equal(bodyKept(dead('gone'),v0),false,'disconnected');assert.equal(bodyKept(dead('stranger'),v0),false,'not in the party (a recruit, the boss)');
 assert.equal(bodyKept(pilot('p2'),v0),false,'alive');assert.equal(bodyKept(dead('p2'),undefined),false,'PvP');
 assert.equal(bodyKept(dead('p2'),{...v0,stage:'ended'}),true,'kept through the end outro');
 assert.equal(helpShown(dead('p2'),v0,'me'),true);assert.equal(helpShown(dead('p2'),v0,'p2'),false,'the downed player sees the HUD instead');
 assert.equal(helpShown(dead('bot'),v0,'me'),true);assert.equal(helpShown(pilot('p2'),v0,'me'),false);assert.equal(helpShown(dead('gone'),v0,'me'),false);
 assert.equal(helpShown(dead('p2'),{...v0,stage:'ended'},'me'),false);
 // Bob: pure in time, bounded, one period; size constant on screen between 5 and 26 m.
 for(const id of ['p2','bot'])for(let k=0;k<40;k++){const x=k*.037;assert.ok(Math.abs(helpBob(x,id))<=DOWNED.help.bob+1e-12);assert.ok(near(helpBob(x,id),helpBob(x+DOWNED.help.period,id),1e-9));}
 assert.notEqual(helpBob(1,'p2'),helpBob(1,'bot'),'members bob out of phase');
 assert.equal(helpWidth(1),helpWidth(5));assert.equal(helpWidth(40),helpWidth(26));assert.ok(near(helpWidth(10)/helpWidth(5),2));
 assert.deepEqual(jointSubtree('left-shoulder').sort(),['left-elbow','left-shoulder']);assert.deepEqual(jointSubtree('head'),['head']);assert.equal(jointSubtree('pelvis').length,11);
 assert.equal(regenMode({x:0,z:0},{x:1,z:1}),'place');assert.equal(regenMode({x:0,z:0},{x:6,z:0}),'pad');assert.equal(regenMode(undefined,{x:0,z:0}),'pad');
});

test('regeneration timeline: pure in authoritative time, identical at 30/60/120 Hz; rig hidden until the rise; done after REGEN_SECONDS',()=>{
 const v0=view(10,['me','p2'],['alive','alive']);v0.party.p2.upAt=10;
 assert.equal(regenAge(v0,'p2',8.9),undefined,'before (a rewind)');assert.equal(regenAge(v0,'p2',10),0);assert.ok(near(regenAge(v0,'p2',10.5)!,.5));
 assert.equal(regenAge(v0,'p2',10+REGEN_SECONDS),undefined,'after');assert.equal(regenAge(v0,'me',10.5),undefined,'never came back');
 assert.equal(regenAge(v0,'p2',9.9),0,'an interpolated frame just before upAt clamps to the start');
 assert.equal(regenAge(v0,'p2',10-.36),0,'the presented clock trails the newest snapshot by up to 350 ms (plus catch-up): still the start');
 const sample=(hz:number)=>{const out=new Map<string,string>();for(let i=0;i*1/hz<=REGEN_SECONDS+.1;i++){const t=i/hz;if(Math.abs(t*60-Math.round(t*60))<1e-9&&Math.round(t*60)%2===0)out.set(t.toFixed(4),JSON.stringify(regenPhase(t)));}return out;};
 const a=sample(30),b=sample(60),c=sample(120);for(const [k,x] of a){assert.equal(b.get(k),x,k);assert.equal(c.get(k),x,k);}
 let last=-1;for(let t=0;t<=REGEN_SECONDS+.05;t+=1/120){const ph=regenPhase(t);assert.ok(ph.scan>=last-1e-12);last=ph.scan;assert.equal(ph.rise,t>=DOWNED.scan);assert.ok(ph.glow>=0&&ph.glow<=1);}
 assert.equal(regenPhase(0).glow,0);assert.equal(regenPhase(DOWNED.scan*.8).scan,1);assert.equal(regenPhase(DOWNED.scan-1e-6).rise,false);assert.equal(regenPhase(DOWNED.scan).rise,true);
 assert.equal(regenPhase(REGEN_SECONDS).done,true);assert.equal(regenPhase(REGEN_SECONDS).glow,0);
 assert.ok(voxelArrival(0)<voxelArrival(1)&&voxelArrival(1)<DOWNED.scan,'every voxel lands before the rise');
});

// ---- the authoritative stamp ----------------------------------------------------------------------------------------
function econWorld(humans:string[],o:{partyBots?:0|1|2|3}={}){
 let econ!:Economy;const {factory}=fakeParts({economy:h=>(econ=new Economy(h))});
 const s=withPveParts(factory,()=>new Simulation({pve:{boss:'siren',seed:7,...o}}));
 for(const id of humans){s.addPlayer(id,id,'ranged');s.pve!.command(id,{action:'hello',v:1},false);}
 s.phase='fight';s.fightStart=s.time;s.pve!.onFightStart();for(const p of s.players.values())if(econ.choosing(p))s.pve!.command(p.id,{action:'close'},false);
 return {s,econ};
}
const tick=(s:Simulation,n=1,interact=new Set<string>())=>{for(let i=0;i<n;i++){for(const p of s.players.values())if(!p.bot){p.input={...p.input,interact:interact.has(p.id),seq:p.input.seq+1};p.inputAt=s.time;}s.step();}};
const kill=(s:Simulation,p:Player)=>{s.damage(p,1e6,null,'environment');assert.ok(p.hp<=0&&p.death);};
const upAt=(s:Simulation,id:string)=>(s.snapshot() as Snapshot).pve!.party[id].upAt;

test('every return stamps PvePartyView.upAt at the revive tick: paid revive, solo self-revive and the free respawn; deaths keep it',()=>{
 {const {s,econ}=econWorld(['h1','h2']);try{const h1=s.players.get('h1')!,h2=s.players.get('h2')!;h2.body.setTranslation(v(0,.9,-8),true);tick(s);kill(s,h2);
   assert.equal(upAt(s,'h2'),undefined);econ.grant('h1',revivePrice(0),'t');const d=h2.death!.pos;h1.body.setTranslation(v(d.x+1,.9,d.z),true);h1.body.setLinvel(v(),true);tick(s);
   tick(s,1,new Set(['h1']));assert.ok(h2.hp>0);const up=upAt(s,'h2')!;assert.ok(near(up,s.time,1/60+1e-9)&&up<=s.time,'stamped on the revive tick');
   assert.ok(s.events.some(e=>e.type===PVE_EVENT.revive&&e.target==='h2'&&Math.abs(e.time-up)<1e-9),'same time as the pve-revive event');
   tick(s,100);kill(s,h2);assert.equal(upAt(s,'h2'),up,'a later death keeps the last return (the client compares it with death.at)');
  }finally{s.dispose();}}
 {const {s}=econWorld(['h1']);try{const h1=s.players.get('h1')!;kill(s,h1);const died=s.time;tick(s,60*6);assert.ok(h1.hp>0,'self-revive');
   const up=upAt(s,'h1')!;assert.ok(up>died&&s.events.some(e=>e.type===PVE_EVENT.revive&&e.message==='self'&&Math.abs(e.time-up)<1e-9));}finally{s.dispose();}}
 {const {s,econ}=econWorld(['h1','h2']);try{const h2=s.players.get('h2')!;const pad=PVE_LAYOUT.pads[0];h2.body.setTranslation(v(pad.x,.9,pad.z),true);tick(s);kill(s,h2);
   econ.respawnAll('wave');assert.ok(h2.hp>0);assert.equal(upAt(s,'h2'),s.time,'respawn');}finally{s.dispose();}}
});

// ---- kept bodies ------------------------------------------------------------------------------------------------------
test('a downed member\'s gibbed ragdoll stays as it fell for the whole downed time (no fade, severed head kept); PvP still fades at 5 s',()=>{
 const f=new Frame(),d=death('p2',10,'decap'),down=pilot('p2',{hp:0,death:d,pos:d.pos});assert.equal(d.wound?.kind,'sever');
 const frames=[10,10.3,11,12,13.9,16,30,90];let head:T.Vector3|undefined;
 for(const t of frames){const v0=view(t,['me','p2'],['alive','dead']);f.run(snap(t,v0,[pilot('me',{pos:{x:4,y:.86,z:4}}),down]));
  const roots=f.named('ragdoll');assert.equal(roots.length,1,`one kept body at ${t}`);const root=roots[0];assert.equal(root.userData.gibPart,'head');
  for(const part of root.children)assert.ok(near(part.scale.x,1,1e-9),'never faded or shrunk');
  if(t>=12)assert.ok(worldPos(root.children.find(c=>c.name==='pelvis')!).y<.45,'lying on the floor');
  const h=root.children.find(c=>c.name==='head')!;assert.ok(h.visible);if(t>=16){const p=worldPos(h);if(head)assert.ok(p.distanceTo(head)<1e-6,'settled and frozen');head=p;}
  assert.ok(f.named('gibber-wound').length>=1,'cut caps stay');}
 // The same death in PvP: the stock five-second lifetime.
 const g=new Frame();for(const t of [10,10.5,12,14.9,15.1])g.run(snap(t,undefined,[pilot('me'),pilot('p2',{hp:0,death:d,pos:d.pos})]));assert.equal(g.named('ragdoll').length,0,'PvP body gone after 5 s');
});

test('HELP bubble rides on each downed member\'s beacon for the others, bobbing; hidden for the downed player and for the living',()=>{
 const f=new Frame(),t=40,ids=['me','p2','p3','party-bot-1'],v0=view(t,ids,['dead','dead','alive','bot']);
 const players=[pilot('me',{hp:0,death:death('me',30,'torso')}),pilot('p2',{hp:0,death:death('p2',30,'arm',v(3,.85,0))}),pilot('p3'),pilot('party-bot-1',{hp:0,death:death('party-bot-1',30,'torso',v(-3,.85,0))})];
 const ys:number[]=[];
 for(let k=0;k<8;k++){f.run(snap(t+k*.15,v0,players));const b=f.pve.world.beacons!;assert.deepEqual([...b.states.keys()].sort(),['me','p2','party-bot-1']);assert.deepEqual([...b.help].sort(),['p2','party-bot-1']);
  const sprite=f.named('pve-help').find(s=>s.visible&&Math.abs(worldPos(s).x-3)<.01)!;assert.ok(sprite,'over p2');ys.push(worldPos(sprite).y);
  assert.equal((sprite as T.Sprite).material.depthTest,false,'readable behind adds');}
 assert.ok(Math.max(...ys)-Math.min(...ys)>.05,'bobs');assert.ok(Math.min(...ys)>2.5,'above the revive label');
 assert.equal(f.named('pve-help').filter(s=>s.visible).length,2);
 // Four bodies kept at once (me, p2, the bot; late frames build one per frame).
 for(let k=0;k<3;k++)f.run(snap(t+2+k/60,v0,players));assert.equal(f.named('ragdoll').length,3);
});

test('revive in place: the body is handed over, wounds seal, the missing head is rebuilt in voxels, the rig stays hidden and then stands up from the body; nothing is left after',()=>{
 const f=new Frame(),d=death('p2',10,'decap'),me=pilot('me',{pos:{x:4,y:.86,z:4}});
 for(const t of [10,10.5,12,15,20]){f.run(snap(t,view(t,['me','p2'],['alive','dead']),[me,pilot('p2',{hp:0,death:d,pos:d.pos})]));}
 const root=f.named('ragdoll')[0],pelvis=worldPos(root.children.find(c=>c.name==='pelvis')!),caps=f.named('gibber-wound');assert.ok(caps.length>=1);
 // Back up at t=20 on the death spot (standAt), 50% HP, no death, no wounds.
 const up=20,alive=pilot('p2',{hp:50,pos:{x:d.pos.x,y:.86,z:d.pos.z}}),at=(age:number)=>{const v1=view(up+age,['me','p2'],['alive','alive']);v1.party.p2.upAt=up;return snap(up+age,v1,[me,alive]);};
 // The newest snapshot (hp, upAt) arrives while the presented clock still trails upAt: the regeneration starts at age 0.
 f.run(at(-.3));assert.deepEqual(f.keeper.stats().waiting,['p2'],'adopted before the presented clock reaches upAt');assert.equal(f.shown.get('p2'),false);
 assert.equal(f.named('ragdoll').length,1,'the body still lies there');
 f.run(at(0));f.run(at(DOWNED.settle));const r=f.keeper.regen('p2')!;assert.ok(r,'regenerating');assert.equal(r.mode,'place');assert.equal(r.corpse,true);assert.ok(r.voxels>20&&r.voxels<=DOWNED.voxels.high,'head voxels '+r.voxels);
 assert.equal(f.shown.get('p2'),false,'live rig hidden while the body regenerates');assert.equal(f.named('ragdoll').length,1,'the same body, now owned by the keeper');
 assert.equal(f.named('pve-regen-holo').length,1,'one instanced draw on the body');
 for(const age of [.1,.2,.3,.4,.5,.6,.7])f.run(at(age));
 assert.ok(caps.every(sealed),'cut caps sealed by the scan');assert.equal(f.shown.get('p2'),false);
 const holo=f.named('pve-regen-holo')[0] as T.InstancedMesh;assert.ok(holo.count>r.voxels*.9,'voxels assembled: '+holo.count);
 // The rise: the live rig shows in the body's pose (its pelvis where the body's lies) and blends up to standing.
 f.run(at(DOWNED.scan+.001));assert.equal(f.shown.get('p2'),true);assert.equal(f.keeper.regen('p2')!.handed,true);
 const rig=f.rigs.get('p2')!;assert.ok(worldPos(rig.pelvis).distanceTo(pelvis)<.02,'stands up from the body: '+worldPos(rig.pelvis).distanceTo(pelvis).toFixed(3));
 assert.equal(f.named('ragdoll').length,0,'the body becomes the player');
 const heights:number[]=[];for(let age=DOWNED.scan+1/60;age<REGEN_SECONDS+.2;age+=1/60){f.run(at(age));heights.push(worldPos(rig.pelvis).y);}
 assert.ok(heights[0]<pelvis.y+.15,'starts low');assert.ok(heights.at(-1)!>.5,'ends standing: '+heights.at(-1)!.toFixed(2));
 for(let i=1;i<heights.length;i++)assert.ok(heights[i]>=heights[i-1]-.01,'rises monotonically');
 assert.equal(f.keeper.regen('p2'),undefined,'finished');assert.deepEqual(f.keeper.stats(),{regens:[],waiting:[],draws:0});
 assert.equal(f.named('pve-regen-holo').length,0);assert.equal(f.layer.stats(rig.group).wounds,0,'no wounds on the live rig');assert.ok(f.bursts>0,'sparkles');
});

test('respawn on a pad: the old body dissolves where it lay, the player materialises from voxels at the pad',()=>{
 const f=new Frame(),d=death('p2',10,'arm'),me=pilot('me',{pos:{x:4,y:.86,z:4}});
 for(const t of [10,12,16])f.run(snap(t,view(t,['me','p2'],['alive','dead']),[me,pilot('p2',{hp:0,death:d,pos:d.pos})]));
 const up=16,pad={x:8,y:.86,z:-6},alive=pilot('p2',{hp:50,pos:pad}),at=(age:number)=>{const v1=view(up+age,['me','p2'],['alive','alive']);v1.party.p2.upAt=up;return snap(up+age,v1,[me,alive]);};
 f.run(at(0));f.run(at(DOWNED.settle));const r=f.keeper.regen('p2')!;assert.equal(r.mode,'pad');assert.ok(r.voxels>100,'whole-body voxels: '+r.voxels);assert.equal(f.shown.get('p2'),false);
 const pieces=f.named('ragdoll')[0].children.filter(c=>c.name in {pelvis:1,spine:1});
 f.run(at(.4));assert.ok(pieces.every(p=>p.scale.x<.8),'dissolving');f.run(at(DOWNED.dissolve+.01));assert.equal(f.named('ragdoll').length,0,'old body gone');
 f.run(at(.79));assert.equal(f.shown.get('p2'),false);f.run(at(DOWNED.scan));assert.equal(f.shown.get('p2'),true);
 const rig=f.rigs.get('p2')!;assert.ok(Math.hypot(worldPos(rig.pelvis).x-pad.x,worldPos(rig.pelvis).z-pad.z)<.3,'standing on the pad');
 f.run(at(REGEN_SECONDS+.01));assert.deepEqual(f.keeper.stats(),{regens:[],waiting:[],draws:0});
});

test('a rewind (kill cam) or a late join rebuilds the kept body, one fast-forward per frame; leaving PvE releases it with the stock ending',()=>{
 const f=new Frame(),ids=['me','p2','p3','party-bot-1'],kinds:PartyRowKind[]=['alive','dead','dead','bot'],xs=[0,3,-3];
 const downs=['p2','p3','party-bot-1'].map((id,i)=>pilot(id,{hp:0,death:death(id,5,'torso',v(xs[i],.85,2))}));
 const me=pilot('me',{pos:{x:0,y:.86,z:6}}),live=(t:number)=>snap(t,view(t,ids,kinds),[me,...downs]);
 f.run(live(40));assert.equal(f.named('ragdoll').length,1,'late: one per frame');f.run(live(40.02));f.run(live(40.04));assert.equal(f.named('ragdoll').length,3);
 // Kill cam: frames from before the deaths (everybody alive), then the death itself, then live again.
 const before=(t:number)=>snap(t,view(t,ids,['alive','alive','alive','bot']),[me,...downs.map(p=>pilot(p.id,{pos:p.death!.pos}))]);
 f.run(before(4));assert.equal(f.named('ragdoll').length,0,'rewound: no bodies before the deaths');
 for(const t of [5,5.2,5.4]){const s=live(t);f.run(s);}assert.equal(f.named('ragdoll').length,3,'replayed from the death');
 f.run(live(41));f.run(live(41.02));assert.equal(f.named('ragdoll').length,3,'live again');
 // Leaving PvE (no view): the hold ends with the stock fade, then the bodies go.
 for(const t of [42,42.5,43.2])f.run(snap(t,undefined,[me,...downs]));assert.equal(f.named('ragdoll').length,0);
});

test('the Siren crush keeps its headless victim lying (not burned away) and regenerates the head on revive',()=>{
 const f=new Frame(),d=death('p2',10,'torso',at0,'siren-crush'),me=pilot('me',{pos:{x:4,y:.86,z:4}});
 for(const t of [10,11,12.5,14,30]){f.run(snap(t,view(t,['me','p2'],['alive','dead']),[me,pilot('p2',{hp:0,death:d,pos:d.pos})]));
  const root=f.named('siren-crush');assert.equal(root.length,1,'kept at '+t);if(t-10>=SIREN_CRUSH_KEEP){const victim=root[0].children[0];assert.ok(near(victim.scale.x,1,1e-9),'not burned away');}}
 const up=30,alive=pilot('p2',{hp:50,pos:{x:d.pos.x,y:.86,z:d.pos.z}}),at=(age:number)=>{const v1=view(up+age,['me','p2'],['alive','alive']);v1.party.p2.upAt=up;return snap(up+age,v1,[me,alive]);};
 f.run(at(0));f.run(at(DOWNED.settle));const r=f.keeper.regen('p2')!;assert.equal(r.mode,'place');assert.ok(r.voxels>20,'head voxels');
 for(let k=0;k<=14;k++)f.run(at(k/10));f.run(at(REGEN_SECONDS+.01));assert.equal(f.named('siren-crush').length,0);assert.deepEqual(f.keeper.stats(),{regens:[],waiting:[],draws:0});
});

test('budgets: four members regenerate at once within two draws each and the voxel caps; kept bodies never starve other deaths; low quality and the SVG path',()=>{
 const ids=['me','p2','p3','p4','party-bot-1'],xs=[-4,-1.3,1.3,4],downs=ids.slice(1).map((id,i)=>pilot(id,{hp:0,death:death(id,5,i%2?'arm':'decap',v(xs[i],.85,0))}));
 const recruit=pilot('recruit-1',{hp:0,death:death('recruit-1',29.9,'torso',v(0,.85,4))}),me=pilot('me',{pos:{x:0,y:.86,z:8}});
 for(const [quality,compatibility] of [[1,false],[.3,false],[1,true]] as const){
  const f=new Frame(quality,compatibility),kinds:PartyRowKind[]=['alive','dead','dead','dead','bot'];
  for(let k=0;k<5;k++)f.run(snap(30+k/60,view(30+k/60,ids,kinds),[me,...downs,recruit]));
  assert.equal(f.named('ragdoll').length,5,'four kept bodies plus the recruit\'s ordinary death');
  assert.equal(f.named('pve-help').filter(s=>s.visible).length,compatibility?0:4,'HELP bubbles (none as sprite rectangles on the SVG path)');
  // Everybody back up in place at t=40.
  const at=(age:number)=>{const v1=view(40+age,ids,['alive','alive','alive','alive','bot']);for(const id of ids.slice(1))v1.party[id].upAt=40;
   return snap(40+age,v1,[me,...downs.map(p=>pilot(p.id,{hp:50,pos:{x:p.death!.pos.x,y:.86,z:p.death!.pos.z}}))]);};
  f.run(at(0));f.run(at(DOWNED.settle));const stats=f.keeper.stats();assert.equal(stats.regens.length,4);
  const holo=f.named('pve-regen-holo') as T.InstancedMesh[];
  if(compatibility){assert.equal(holo.length,0,'no instanced hologram on the SVG path');assert.equal(stats.draws,0);}
  else{assert.ok(holo.length<=8&&stats.draws<=8,'at most two draws per regeneration');const cap=quality<.5?DOWNED.voxels.low:DOWNED.voxels.high;
   for(const id of ids.slice(1)){const r=f.keeper.regen(id)!;assert.ok(r.voxels>0&&r.voxels<=cap,`${id} voxels ${r.voxels} ≤ ${cap}`);}
   for(const h of holo)assert.equal((h.material as T.Material).depthWrite,false);}
  for(let k=1;k<=14;k++)f.run(at(k/10));f.run(at(REGEN_SECONDS+.02));
  assert.deepEqual(f.keeper.stats(),{regens:[],waiting:[],draws:0});assert.equal(f.named('pve-regen-holo').length,0);
  for(const id of ids.slice(1))assert.equal(f.shown.get(id),true,'everybody stood up');
 }
});
