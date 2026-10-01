/**
 * Development-only body-damage lab (docs/BODY-DAMAGE-HANDOFF.md). Renders the real wound layer (body-wounds.ts), death
 * effects, impact ragdoll (ragdoll.ts, body-trauma.ts) and kneel/fall collapse (body-collapse.ts) as labelled frame strips.
 * Every wound comes from the authority contract itself (resolveBodyWound, deathMotion), so the lab shows what a client
 * receives. Not built by Vite and never imported from src. With `npm run dev` running, open
 * /tools/body-lab/index.html?scene=<name>.
 *
 *  scene=pistol    pistol punctures (head, chest, arm, thigh), then an ordinary torso death
 *        cavity    base sniper head cavity (lethal: the authority picks the kneel/fall)
 *        decap     faster sniper shot removes the head; the body kneels and falls
 *        cut       sword cuts on arm and thigh, then a lethal cut severs the arm
 *        thrust    dagger stabs (puncture and cavity), then a lethal thrust
 *        crush     shield bruise, then a lethal shield blow crushes the head (hat flies loose)
 *        limb      lethal shield blow crushes the right forearm (the held weapon flies loose)
 *        torso     ordinary torso death (pistol, plain ragdoll)
 *        kneel     grounded fast headshot: impact, knees, forward fall, ragdoll (default)
 *        airborne  the same headshot in the air: immediate ragdoll, keeps falling
 *        finisher  wounded victim, attacker's cosmetic kill effect (effect=<id>, default ragdoll-ember)
 *        low       reduced quality: coarser wounds, fewer fragments and droplets (with quality=.3)
 *        gallery   one living wound per column (every kind and trauma), rows: head-on, angled, reduced quality
 *  views=front,side,three,back,top,close   camera rows (close frames the scene's focus part)
 *  times=a,b,c   ages after death to sample (scene defaults). The first column is the living victim just before death.
 *  fps=<n>       frames simulated between samples (default 60; 20, 30, 144 for frame-rate studies)
 *  late=1        the death is first seen at the first sample age (late snapshot / late join)
 *  repeat=<n>    every snapshot is delivered n times per frame (repeated snapshots must not restart or duplicate anything)
 *  hide=<regex>  hide objects whose name matches before each render (e.g. wound-spray|gibber-blood to compare bodies)
 *  quality=<0-1> effects quality; detail=low also switches rig parts to their low-detail boxes
 *  yaw=<rad>, effect=<id>, cell=<px>, aspect=<w/h>, dist=<m>, light=arena|lab
 * The sheet title ends with the disposal check: scene objects before the victim vs after DeathEffects/BodyWounds clear.
 */
import * as T from 'three';
import {createCharacterRig,setRigDetail,type CharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter} from '../../src/client/character-animation.js';
import {arenaLighting} from '../../src/client/arena-lighting.js';
import {dressRig} from '../../src/client/cosmetics.js';
import {buildWeapon} from '../../src/client/weapon-visuals.js';
import {BodyWounds,woundAnchors} from '../../src/client/body-wounds.js';
import {DeathEffects} from '../../src/client/death-effects.js';
import {initPhysics,type PlayerView,type Snapshot,type DeathPresentation} from '../../src/shared/simulation.js';
import {resolveBodyWound,deathMotion,projectileBodyImpact,meleeBodyImpact,type BodyImpact,type BodyWound} from '../../src/shared/body-damage.js';
import type {Vec,SpecId} from '../../src/shared/config.js';
import type {Cosmetics} from '../../src/shared/cosmetics.js';

const q=new URLSearchParams(location.search);
const num=(k:string,d:number)=>q.has(k)&&Number.isFinite(Number(q.get(k)))?Number(q.get(k)):d;
const sceneName=q.get('scene')||'kneel',cell=num('cell',230),aspect=num('aspect',1),cellW=Math.round(cell*aspect),fps=Math.max(1,num('fps',60)),late=q.get('late')==='1',repeat=Math.max(1,Math.round(num('repeat',1)));
const yaw=num('yaw',0),light=q.get('light')||'arena',dist=num('dist',3.3);
const status=document.getElementById('status')!,sheet=document.getElementById('sheet') as HTMLCanvasElement;

type Hit={at:number;impact:BodyImpact;local:Vec;dir:Vec;damage:number;headshot?:boolean;melee?:boolean};
interface Scenario {hits:Hit[];fatal:Hit;times:number[];views:string[];effect?:string;air?:boolean;focus:string;spec?:SpecId;cosmetics?:Partial<Cosmetics>;quality?:number;
 /** Close-up: offset in the focus part's frame, and the camera offset from it in the victim's facing frame. */
 near?:Vec;eye?:Vec}
const v=(x:number,y:number,z:number):Vec=>({x,y,z});
const pistol=projectileBodyImpact({spec:'pistol',vel:v(0,0,-52),radius:.045}),rifle=(speed:number)=>projectileBodyImpact({spec:'rifle',vel:v(0,0,-speed),radius:.045});
const front=v(0,0,-1),fromLeft=v(-1,0,-.15),stab={...meleeBodyImpact('daggers',1,true)};
const SCENES:Record<string,Scenario>={
 pistol:{hits:[{at:.2,impact:pistol,local:v(.05,.66,.3),dir:front,damage:23,headshot:true},{at:.35,impact:pistol,local:v(-.08,.18,.3),dir:front,damage:23},{at:.5,impact:pistol,local:v(.36,.05,.3),dir:front,damage:23},{at:.62,impact:pistol,local:v(.12,-.42,.3),dir:front,damage:23}],
  fatal:{at:1,impact:pistol,local:v(.06,.05,.3),dir:front,damage:23},times:[0,.3,.8,2],views:['close','front','three'],focus:'spine'},
 cavity:{hits:[{at:.3,impact:rifle(150),local:v(-.1,.12,.3),dir:front,damage:58}],fatal:{at:1,impact:rifle(150),local:v(.04,.64,.3),dir:front,damage:87,headshot:true},times:[0,.08,.25,.6,1.2],views:['close','front','side'],focus:'head',near:v(0,.15,0),eye:v(.35,.1,.9)},
 decap:{hits:[],fatal:{at:1,impact:rifle(270),local:v(0,.64,.3),dir:front,damage:87,headshot:true},times:[0,.06,.15,.32,.55,.82,1.2,2],views:['close','side','three'],focus:'spine',near:v(0,.6,0),eye:v(.5,.6,.9)},
 cut:{hits:[{at:.25,impact:meleeBodyImpact('sword'),local:v(.36,.08,.1),dir:fromLeft,damage:30,melee:true},{at:.5,impact:meleeBodyImpact('sword'),local:v(.15,-.45,.15),dir:v(-1,0,-.3),damage:30,melee:true}],
  fatal:{at:1,impact:meleeBodyImpact('greatsword'),local:v(.36,.12,.05),dir:fromLeft,damage:52,melee:true},times:[0,.1,.3,.7,1.5],views:['close','front','three'],focus:'spine',near:v(.32,.38,0),eye:v(1,.2,.6)},
 thrust:{hits:[{at:.25,impact:stab,local:v(-.06,.1,.3),dir:front,damage:15,melee:true},{at:.5,impact:{...stab,speed:40},local:v(.1,-.05,.3),dir:front,damage:15,melee:true}],
  fatal:{at:1,impact:{...stab,speed:40},local:v(-.12,.22,.3),dir:front,damage:15,melee:true},times:[0,.3,.8,2],views:['close','front','three'],focus:'spine'},
 crush:{hits:[{at:.3,impact:meleeBodyImpact('shield'),local:v(-.36,.05,.2),dir:v(1,0,-.2),damage:23,melee:true}],fatal:{at:1,impact:{...meleeBodyImpact('shield'),speed:22},local:v(0,.66,.3),dir:front,damage:23,headshot:true,melee:true},
  times:[0,.05,.15,.35,.8,2],views:['close','front','side'],focus:'spine',near:v(0,.6,0),eye:v(.4,.7,.9),cosmetics:{hat:'top'}},
 limb:{hits:[],fatal:{at:1,impact:{...meleeBodyImpact('shield'),speed:20},local:v(-.36,-.18,.2),dir:v(.9,0,-.4),damage:23,melee:true},times:[0,.05,.15,.4,1],views:['close','front','three'],focus:'right-shoulder',near:v(0,-.25,0),eye:v(-.8,.2,.8),spec:'sword'},
 torso:{hits:[],fatal:{at:1,impact:pistol,local:v(-.05,.1,.3),dir:front,damage:23},times:[0,.15,.4,.8,1.5,3],views:['front','side','three'],focus:'spine'},
 kneel:{hits:[],fatal:{at:1,impact:rifle(150),local:v(.03,.64,.3),dir:front,damage:87,headshot:true},times:[0,.05,.15,.25,.32,.45,.6,.75,.82,.95,1.3,2.5],views:['side','three','front'],focus:'spine'},
 airborne:{hits:[],air:true,fatal:{at:1,impact:rifle(150),local:v(.03,.64,.3),dir:front,damage:87,headshot:true},times:[0,.15,.3,.5,.8,1.5],views:['side','three'],focus:'spine'},
 finisher:{hits:[{at:.3,impact:rifle(150),local:v(-.1,.12,.3),dir:front,damage:58},{at:.5,impact:meleeBodyImpact('sword'),local:v(.36,.08,.1),dir:fromLeft,damage:30,melee:true}],
  fatal:{at:1,impact:rifle(270),local:v(0,.64,.3),dir:front,damage:87,headshot:true},effect:'ragdoll-ember',times:[0,.2,.6,1.4],views:['front','three'],focus:'spine'},
 low:{hits:[{at:.3,impact:rifle(150),local:v(-.1,.12,.3),dir:front,damage:58},{at:.5,impact:meleeBodyImpact('sword'),local:v(.36,.08,.1),dir:fromLeft,damage:30,melee:true}],
  fatal:{at:1,impact:{...meleeBodyImpact('shield'),speed:22},local:v(0,.66,.3),dir:front,damage:23,headshot:true,melee:true},times:[0,.1,.35,1],views:['close','front'],focus:'spine',quality:.3,cosmetics:{hat:'top'}},
};
const S=SCENES[sceneName]||SCENES.kneel,quality=num('quality',S.quality??1),effectId=q.get('effect')||S.effect||'impact-ragdoll';
const times=q.get('times')?.split(',').map(Number).filter(Number.isFinite)||S.times,views=(q.get('views')||S.views.join(',')).split(',').filter(Boolean);

const toWorld=(l:Vec)=>({x:l.x*Math.cos(yaw)+l.z*Math.sin(yaw),y:l.y,z:-l.x*Math.sin(yaw)+l.z*Math.cos(yaw)});
const position=S.air?v(0,1.9,0):v(0,.85,0);
function wound(h:Hit,id:string,lethal:boolean):BodyWound{const local=toWorld(h.local),point=v(position.x+local.x,position.y+local.y,position.z+local.z);
 return resolveBodyWound({id,at:h.at,position,yaw,point,direction:toWorld(h.dir),impact:h.impact,damage:h.damage,lethal,headshot:h.headshot});}
const hits=S.hits.map((h,i)=>wound(h,'w'+i,false)),fatal=wound(S.fatal,'fatal',true);
const deathVelocity=S.air?v(0,2.4,-2.2):v(0,0,-.4),motion=deathMotion({wound:fatal,grounded:!S.air,incapacitated:false,yaw,velocity:deathVelocity});
const fatalPoint=(()=>{const l=toWorld(S.fatal.local);return v(position.x+l.x,position.y+l.y,position.z+l.z);})();
const death:DeathPresentation={wound:fatal,motion,id:'death-'+sceneName,at:S.fatal.at,effect:effectId,seed:20261001,pos:position,velocity:deathVelocity,direction:toWorld(S.fatal.dir),source:'shooter',kind:'direct',yaw,
 hitPoint:fatalPoint,melee:!!S.fatal.melee,force:350,gibPart:effectId==='impact-ragdoll'?fatal.destroyedPart:undefined};
function player(t:number):PlayerView{
 const dead=t>=death.at;const wounds=[...hits,...(dead?[fatal]:[])].filter(w=>w.at<=t);
 return {id:'victim',name:'victim',color:'#3fc6ff',class:'ranged',spec:S.spec||'pistol',pos:position,vel:v(0,0,0),yaw,pitch:0,hp:dead?0:100,maxHp:100,mana:100,maxMana:100,ammo:12,maxAmmo:12,stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],connected:true,bot:null,ack:0,grounded:!S.air,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],hitStunUntil:-1e9,
  cosmetics:{bodyColor:'#e5e7eb',hat:'none',killEffect:'impact-ragdoll',...S.cosmetics} as Cosmetics,wounds:wounds.length?wounds:undefined,death:dead?death:undefined} as PlayerView;
}
const snapshot=(t:number):Snapshot=>({generation:1,setNumber:1,round:1,time:t,tick:Math.round(t*60),phase:'fight',remaining:60,setWinner:null,winner:null,result:'',players:[player(t)],props:[],projectiles:[],events:[],impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options:{map:'foundry'}} as unknown as Snapshot);

function label(ctx:CanvasRenderingContext2D,text:string,x:number,y:number){ctx.fillStyle='rgba(0,0,0,.6)';ctx.fillRect(x,y,Math.min(cellW-4,ctx.measureText(text).width+10),16);ctx.fillStyle='#ece8f4';ctx.fillText(text,x+5,y+12);}
function camera(cam:T.PerspectiveCamera,view:string,target:T.Vector3){
 const e=S.eye||v(.45,.25,1.25),o:Record<string,number[]>={front:[0,.9,dist],side:[dist,.9,0],three:[dist*.72,1.2,dist*.72],back:[0,.9,-dist],top:[0,dist+1.2,.01],close:[e.x,e.y,e.z]};
 const c=o[view]??o.front,off=new T.Vector3(c[0],c[1],c[2]).applyAxisAngle(new T.Vector3(0,1,0),yaw);cam.position.copy(target).add(off);cam.lookAt(target);
}

/** One living wound per column, seen head-on and from 40 degrees, then carved at reduced quality. */
const GALLERY:[string,Hit][]=[
 ['pistol puncture',{at:0,impact:pistol,local:v(-.04,.16,.3),dir:front,damage:23}],
 ['rifle cavity',{at:0,impact:rifle(150),local:v(-.04,.16,.3),dir:front,damage:58}],
 ['head cavity',{at:0,impact:rifle(150),local:v(.04,.64,.3),dir:front,damage:87,headshot:true}],
 ['head puncture',{at:0,impact:pistol,local:v(.06,.62,.3),dir:front,damage:23,headshot:true}],
 ['sword cut',{at:0,impact:meleeBodyImpact('sword'),local:v(-.04,.16,.3),dir:v(-1,0,-.25),damage:30,melee:true}],
 ['greatsword cut',{at:0,impact:meleeBodyImpact('greatsword'),local:v(-.04,.16,.3),dir:v(-1,-.3,-.25),damage:52,melee:true}],
 ['arm cut',{at:0,impact:meleeBodyImpact('sword'),local:v(.36,.08,.12),dir:fromLeft,damage:30,melee:true}],
 ['dagger stab',{at:0,impact:stab,local:v(-.04,.16,.3),dir:front,damage:15,melee:true}],
 ['deep stab',{at:0,impact:{...stab,speed:40},local:v(-.04,.16,.3),dir:front,damage:15,melee:true}],
 ['shield bruise',{at:0,impact:meleeBodyImpact('shield'),local:v(-.04,.16,.3),dir:front,damage:23,melee:true}],
 ['fire scorch',{at:0,impact:projectileBodyImpact({spec:'fire',vel:v(0,0,-24),radius:.14}),local:v(-.04,.16,.3),dir:front,damage:22}],
 ['thigh puncture',{at:0,impact:pistol,local:v(.12,-.42,.3),dir:front,damage:23}],
];
async function gallery(gl:T.WebGLRenderer,ctx:CanvasRenderingContext2D){
 const scene=new T.Scene();arenaLighting(scene,false);const ground=new T.Mesh(new T.PlaneGeometry(200,60),new T.MeshLambertMaterial({color:0x474c5a}));ground.rotation.x=-Math.PI/2;scene.add(ground);
 const cam=new T.PerspectiveCamera(30,cellW/cell,.02,100),rows=['head-on','angled','reduced'];sheet.width=GALLERY.length*cellW;sheet.height=rows.length*cell+22;ctx.fillStyle='#0c0b10';ctx.fillRect(0,0,sheet.width,sheet.height);
 const full=new BodyWounds(scene),reduced=new BodyWounds(scene);const cells:string[]=[];
 for(let r=0;r<rows.length;r++)for(let i=0;i<GALLERY.length;i++){const [name,h]=GALLERY[i],at=v(i*3,.85,r===2?6:0),w=resolveBodyWound({id:'g'+i,at:0,position:at,yaw:0,point:v(at.x+h.local.x,at.y+h.local.y,at.z+h.local.z),direction:h.dir,impact:h.impact,damage:h.damage,lethal:false,headshot:h.headshot});
  if(r!==1){const rig=createCharacterRig('#3fc6ff','#e5e7eb',true);rig.group.position.set(at.x,at.y,at.z);scene.add(rig.group);const p={...player(-1),id:'g'+r+'-'+i,pos:at,wounds:[w],hp:100,death:undefined} as PlayerView;animateCharacter(rig,p,2,1/60,0);
   (r===2?reduced:full).update({...snapshot(2),players:[p]} as Snapshot,new Map([[p.id,rig]]),{quality:r===2?.3:1,dt:1/60});
   // BodyWounds keeps every rig it was handed in one frame; feed each instance all of its rigs again below.
   (rig.group.userData as {lab:unknown}).lab={p};if(r===0)cells.push(`${name}: ${w.part} ${w.kind} r${w.radius.toFixed(3)} s${w.severity.toFixed(2)}`);}}
 // Re-reconcile all gallery rigs per instance together (an instance drops rigs missing from a frame).
 for(const [inst,row] of [[full,0],[reduced,2]] as const){const players:PlayerView[]=[],rigs=new Map<string,{group:T.Group}>();scene.traverse(o=>{const lab=(o.userData as {lab?:{p:PlayerView}}).lab;if(lab&&lab.p.id.startsWith('g'+row+'-')){players.push(lab.p);rigs.set(lab.p.id,{group:o as T.Group});}});inst.update({...snapshot(2),players} as Snapshot,rigs,{quality:row===2?.3:1,dt:1/60});}
 for(let r=0;r<rows.length;r++)for(let i=0;i<GALLERY.length;i++){const [name,h]=GALLERY[i],at=v(i*3,.85,r===2?6:0),w=resolveBodyWound({id:'g'+i,at:0,position:at,yaw:0,point:v(at.x+h.local.x,at.y+h.local.y,at.z+h.local.z),direction:h.dir,impact:h.impact,damage:h.damage,lethal:false,headshot:h.headshot});
  // Look straight at the face the wound was carved into (anchor), at the carved spot.
  const anchor=woundAnchors(w)[0],normal=anchor?new T.Vector3().setComponent(anchor.axis,anchor.sign):new T.Vector3(0,0,1),point=new T.Vector3(at.x+w.point.x,at.y+w.point.y,at.z+w.point.z);
  if(anchor){const mesh=scene.children.find(o=>(o.userData as {lab?:{p:PlayerView}}).lab?.p.id===(r===2?'g2-':'g0-')+i)?.getObjectByName(anchor.root)?.getObjectByName(anchor.mesh);if(mesh){mesh.updateWorldMatrix(true,false);point.copy(anchor.hit).divide(anchor.size).applyMatrix4(mesh.matrixWorld);normal.transformDirection(mesh.matrixWorld);}}
  const eye=normal.clone().multiplyScalar(.85);if(r===1)eye.applyAxisAngle(new T.Vector3(0,1,0),.7).add(new T.Vector3(0,.25,0));cam.position.copy(point).add(eye);cam.lookAt(point);
  gl.render(scene,cam);ctx.drawImage(gl.domElement,i*cellW,22+r*cell);label(ctx,`${rows[r]} · ${name}`,i*cellW+2,24+r*cell);}
 ctx.fillStyle='#c9b8ff';ctx.fillText('body-lab · gallery · '+cells.join(' | '),6,15);
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}

async function main(){
 await initPhysics();
 if(sceneName==='gallery'){const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cellW,cell);gl.outputColorSpace=T.SRGBColorSpace;gl.toneMapping=T.ACESFilmicToneMapping;gl.toneMappingExposure=.92;const ctx=sheet.getContext('2d')!;ctx.font='11px ui-monospace,monospace';return gallery(gl,ctx);}
 const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cellW,cell);gl.shadowMap.enabled=true;gl.shadowMap.type=T.PCFSoftShadowMap;
 if(light==='arena'){gl.outputColorSpace=T.SRGBColorSpace;gl.toneMapping=T.ACESFilmicToneMapping;gl.toneMappingExposure=.92;}
 const columns=1+times.length;sheet.width=columns*cellW;sheet.height=views.length*cell+22;const ctx=sheet.getContext('2d')!;ctx.font='11px ui-monospace,monospace';ctx.fillStyle='#0c0b10';ctx.fillRect(0,0,sheet.width,sheet.height);
 const scene=new T.Scene();if(light==='arena')arenaLighting(scene,false);else{scene.background=new T.Color(0x2a2f38);scene.add(new T.HemisphereLight(0xdfe8ff,0x2c2733,1.2));const k=new T.DirectionalLight(0xffffff,1.6);k.position.set(3,6,4);scene.add(k);}
 const ground=new T.Mesh(new T.PlaneGeometry(60,60),new T.MeshLambertMaterial({color:0x474c5a}));ground.rotation.x=-Math.PI/2;ground.receiveShadow=true;scene.add(ground);const grid=new T.GridHelper(30,60,0x3d4250,0x333743);grid.position.y=.002;scene.add(grid);
 const cam=new T.PerspectiveCamera(36,cellW/cell,.03,200);const baseline=scene.children.length;
 const rig:CharacterRig=createCharacterRig('#3fc6ff','#e5e7eb',true);dressRig(rig,{bodyColor:'#e5e7eb',hat:'none',killEffect:'impact-ragdoll',...S.cosmetics} as Cosmetics);buildWeapon(rig.weapon,S.spec||'pistol');if(q.get('detail')==='low')setRigDetail(rig,true);
 rig.group.traverse(o=>{if(o instanceof T.Mesh){o.castShadow=true;o.receiveShadow=true;}});scene.add(rig.group);const rigs=new Map([['victim',rig]]);
 const wounds=new BodyWounds(scene),deaths=new DeathEffects(scene);let clock=0;
 const frame=(t:number,h:number)=>{const snap=snapshot(t),p=snap.players[0];rig.group.position.set(p.pos.x,p.pos.y,p.pos.z);rig.group.rotation.y=yaw;rig.group.visible=p.hp>0;
  animateCharacter(rig,p,p.death&&p.hp<=0?p.death.at:t,h,0);for(let n=0;n<repeat;n++){wounds.update(snap,rigs,{quality,dt:n?0:h});deaths.update(snap,rigs,quality);}clock=t;};
 // The close-up follows the scene's wound (a point in its part's frame); full views follow the body (pelvis).
 const partOf=(name:string,dead:boolean)=>dead?(scene.getObjectByName('ragdoll')||scene.getObjectByName('gibber-ragdoll'))?.getObjectByName(name):rig.group.getObjectByName(name);
 const focus=(name:string,offset:Vec|undefined,dead:boolean)=>{const part=partOf(name,dead);if(!part)return rig.group.getWorldPosition(new T.Vector3()).setY(.6);part.updateWorldMatrix(true,false);return new T.Vector3(offset?.x||0,offset?.y||0,offset?.z||0).applyMatrix4(part.matrixWorld);};
 const hide=q.get('hide')?new RegExp(q.get('hide')!):undefined;
 const render=(column:number,text:string,dead:boolean)=>{if(hide)scene.traverse(o=>{if(hide.test(o.name))o.visible=false;});for(let r=0;r<views.length;r++){const close=views[r]==='close',target=close?focus(S.focus,S.near,dead):focus('pelvis',v(0,.25,0),dead);camera(cam,views[r],target);gl.render(scene,cam);ctx.drawImage(gl.domElement,column*cellW,22+r*cell);label(ctx,`${views[r]} ${text}`,column*cellW+2,24+r*cell);}};
 // Living victim, with every wound before the fatal hit (simulated from the first hit so fresh-wound spray has settled).
 const h=1/fps;for(let t=0;t<death.at-.02;t+=h)frame(t,h);frame(death.at-.02,1e-4);render(0,'alive',false);
 const check:string[]=[];
 for(let i=0;i<times.length;i++){const at=death.at+times[i];
  if(!(late&&i===0)){for(let t=Math.max(clock+h,death.at);t<at-1e-9;t+=h)frame(t,h);}frame(at,1e-4);
  let bad=0;scene.updateMatrixWorld(true);scene.traverse(o=>{if(!o.matrixWorld.elements.every(Number.isFinite))bad++;});
  check.push(`${times[i]}s:${deaths.count}${bad?' NaN'+bad:''}`);render(i+1,`${times[i].toFixed(2)}s · ${deaths.count}`,true);}
 deaths.clear();wounds.clear();scene.remove(rig.group);const leaks=scene.children.length-baseline-1;
 const stats=wounds.stats(rig.group);
 ctx.fillStyle='#c9b8ff';ctx.fillText(`body-lab · ${sceneName} · ${effectId} · fatal ${fatal.part} ${fatal.kind}/${fatal.trauma} sev ${fatal.severity.toFixed(2)} · motion ${motion.kind} · q${quality} fps${fps}${late?' late':''}${repeat>1?' x'+repeat:''} · budget ${check.join(' ')} · dispose ${leaks===0?'ok':'LEAK '+leaks} ${stats.parts?'wounds left':''}`,6,15);
 console.warn('body-lab',JSON.stringify({scene:sceneName,fatal:{part:fatal.part,kind:fatal.kind,trauma:fatal.trauma,severity:fatal.severity,destroyed:fatal.destroyedPart},hits:hits.map(w=>w.part+':'+w.kind),motion:motion.kind,check,leaks}));
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
main().catch(e=>{status.textContent='error: '+(e?.stack||e);(window as unknown as {labError:string}).labError=String(e?.stack||e);});
