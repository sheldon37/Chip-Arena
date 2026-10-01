import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {createCharacterRig} from '../src/client/character-rig.js';
import {SirenCutscene,CUTSCENE_TAIL,sirenify} from '../src/client/siren/index.js';
import {SIREN} from '../src/shared/siren-contract.js';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';

const P=(id:string,pos:{x:number;y:number;z:number},extra:Record<string,unknown>={})=>({id,name:id,pos,yaw:0,hp:100,...extra}) as unknown as PlayerView;
const grab={owner:SIREN.spec,victim:'v',at:10,crushAt:13.2,releaseAt:13.8};
/** The two rigs as the renderer places them mid-hold: the Siren on its player, the victim at arm's length, head high. */
function pair(){
 const rigs=new Map([[SIREN.spec,createCharacterRig('#b8b0ff')],['v',createCharacterRig('#3fc6ff')]]);sirenify(rigs.get(SIREN.spec)!);
 rigs.get(SIREN.spec)!.group.position.set(0,.85,0);rigs.get('v')!.group.position.set(0,1,1.05);return rigs;
}
function state(time:number,held:boolean,victimAlive=true,phase='fight'):Snapshot{
 return {time,phase,players:[P(SIREN.spec,{x:0,y:.85,z:0}),P('v',{x:0,y:1.4,z:.95},{hp:victimAlive?100:0}),P('other',{x:6,y:.85,z:6})],projectiles:[],events:[],options:{},
  siren:{patches:[],eruptions:[],rains:[],marks:[],grabs:held?[grab]:[]}} as unknown as Snapshot;
}

test('only the victim and the Siren player get the cutscene',()=>{
 for(const me of ['v',SIREN.spec]){const c=new SirenCutscene();assert.equal(c.check(state(10.5,true),me),true,me);assert.equal(c.role,me==='v'?'victim':'siren');}
 const c=new SirenCutscene();assert.equal(c.check(state(10.5,true),'other'),false);
});

test('the scene plays through the crush and its tail, then hands the camera back',()=>{
 const c=new SirenCutscene();assert.ok(c.check(state(11,true),'v'));
 // The victim dies at the crush and the hold leaves the snapshot: the cutscene keeps going for the tail.
 assert.ok(c.check(state(13.3,false,false),'v'));assert.ok(c.check(state(13.2+CUTSCENE_TAIL-.05,false,false),'v'));
 assert.equal(c.check(state(13.2+CUTSCENE_TAIL+.01,false,false),'v'),false);
});

test('the Siren player\'s scene ends on the release, when it can move again',()=>{
 const c=new SirenCutscene();assert.ok(c.check(state(11,true),SIREN.spec));
 assert.ok(c.check(state(13.3,false,false),SIREN.spec),'plays through the crush');assert.ok(c.check(state(13.75,false,false),SIREN.spec));
 assert.equal(c.check(state(13.81,false,false),SIREN.spec),false,'control is back at releaseAt');
});

test('an interrupted hold cuts straight back',()=>{
 const c=new SirenCutscene();assert.ok(c.check(state(11,true),SIREN.spec));
 assert.equal(c.check(state(11.5,false),SIREN.spec),false,'hold ended before the crush');
 const d=new SirenCutscene();d.check(state(11,true),'v');assert.equal(d.check(state(11.2,true,true,'result'),'v'),true);assert.equal(d.check(state(11.3,true,true,'lobby'),'v'),false);
});

test('every shot frames the pair with a finite camera, letterbox bars and wall avoidance',()=>{
 const rigs=pair();
 for(const me of ['v',SIREN.spec]){const c=new SirenCutscene(),cam=new T.PerspectiveCamera(64,16/9,.08,400);
  for(let t=10;t<13.2+CUTSCENE_TAIL;t+=.1){const held=t<13.2,s=state(t,held,held);if(!c.check(s,me))break;c.update(s,cam,rigs);
   for(const x of [cam.position.x,cam.position.y,cam.position.z,cam.quaternion.x,cam.fov])assert.ok(Number.isFinite(x),`${me} t=${t.toFixed(1)}`);
   const bars=cam.children.find(o=>o.name==='siren-cutscene-bars');assert.ok(bars,'bars attached');assert.ok(cam.position.y>-.2,'camera stays above the floor');}
 }
 // Occlusion: a wall a short way from the subject pulls the camera in front of it.
 const c=new SirenCutscene(),cam=new T.PerspectiveCamera(64,1,.08,400);c.check(state(10.3,true),'v');
 c.update(state(10.3,true),cam,rigs);const free=cam.position.clone();
 c.update(state(10.3,true),cam,rigs,()=>.8);const look=new T.Vector3();cam.getWorldDirection(look);
 assert.ok(cam.position.distanceTo(free)>.3,'the occluded camera moved');
});

test('the victim looks into the grin; the Siren player sees its grin over the victim in its grip',()=>{
 const rigs=pair();
 const head=(id:string)=>{const r=rigs.get(id)!;r.group.updateMatrixWorld(true);return r.head.getWorldPosition(new T.Vector3());};
 const facing=(me:string)=>{const c=new SirenCutscene(),cam=new T.PerspectiveCamera(64,1,.08,400),s=state(11.5,true);c.check(s,me);c.update(s,cam,rigs);const dir=new T.Vector3();cam.getWorldDirection(dir);return {cam,dir};};
 const victimView=facing('v'),toSiren=head(SIREN.spec).sub(victimView.cam.position).normalize();assert.ok(victimView.dir.dot(toSiren)>.95);
 const sirenView=facing(SIREN.spec),toOwnHead=head(SIREN.spec).sub(sirenView.cam.position).normalize();assert.ok(sirenView.dir.dot(toOwnHead)>.95);
 sirenView.cam.updateMatrixWorld(true);const victimOnScreen=head('v').project(sirenView.cam);assert.ok(Math.abs(victimOnScreen.x)<1&&Math.abs(victimOnScreen.y)<1&&victimOnScreen.z<1,'the victim is in the frame');
});

test('a Siren turning mid-hold eases the camera round instead of snapping',()=>{
 const rigs=pair();
 const c=new SirenCutscene(),cam=new T.PerspectiveCamera(64,16/9,.08,400);let t=11.5;
 c.check(state(t,true),SIREN.spec);c.update(state(t,true),cam,rigs);const before=cam.position.clone();
 rigs.get(SIREN.spec)!.group.rotation.y=Math.PI/2;t+=1/60;c.check(state(t,true),SIREN.spec);c.update(state(t,true),cam,rigs);
 assert.ok(cam.position.distanceTo(before)<.25,'one frame later the camera has barely moved');
 for(let i=0;i<120;i++){t+=1/60;c.check(state(t,true),SIREN.spec);c.update(state(t,true),cam,rigs);}
 const fresh=new SirenCutscene(),settled=new T.PerspectiveCamera(64,16/9,.08,400);fresh.check(state(t,true),SIREN.spec);fresh.update(state(t,true),settled,rigs);
 assert.ok(cam.position.distanceTo(settled.position)<.1,'two seconds later it has settled on the new facing');
});
