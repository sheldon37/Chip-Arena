/** Body-damage presentation (docs/BODY-DAMAGE-HANDOFF.md) without a physics engine: the living wound layer, the
 * trauma pieces (caps, crush fragments) and the kneel/fall collapse. Wounds come from the authority contract itself. */
import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {v,type Vec} from '../src/shared/config.js';
import {resolveBodyWound,deathMotion,projectileBodyImpact,meleeBodyImpact,MAX_BODY_WOUNDS,type BodyImpact,type BodyWound} from '../src/shared/body-damage.js';
import type {PlayerView,Snapshot} from '../src/shared/simulation.js';
import {createCharacterRig,setRigDetail} from '../src/client/character-rig.js';
import {currentLayout,baseLayout,copyVoxelPart,layoutBoxes,type VoxelLayout} from '../src/client/voxel-kit.js';
import {BodyWounds,woundAnchors,carvePart,specFor,restMeshes,woundKey,WOUND_GEOMETRY} from '../src/client/body-wounds.js';
import {woundCap,faceAlong,boneSign,CrushDebris} from '../src/client/body-trauma.js';
import {KneelFall,partBoxes,COLLAPSE,type CollapsePart} from '../src/client/body-collapse.js';
import {GibberVfx,SEVER_PROFILE} from '../src/client/gibber-vfx.js';

const pistol=projectileBodyImpact({spec:'pistol',vel:v(0,0,-52),radius:.045}),rifle=(speed:number)=>projectileBodyImpact({spec:'rifle',vel:v(0,0,-speed),radius:.045});
const at=v(0,.85,0);
function hit(id:string,impact:BodyImpact,local:Vec,dir:Vec,damage:number,o:{lethal?:boolean;headshot?:boolean;at?:number;yaw?:number;position?:Vec}={}):BodyWound{
 const p=o.position||at,yaw=o.yaw||0,w=(x:Vec)=>v(x.x*Math.cos(yaw)+x.z*Math.sin(yaw),x.y,-x.x*Math.sin(yaw)+x.z*Math.cos(yaw)),l=w(local);
 return resolveBodyWound({id,at:o.at??1,position:p,yaw,point:v(p.x+l.x,p.y+l.y,p.z+l.z),direction:w(dir),impact,damage,lethal:!!o.lethal,headshot:o.headshot});
}
const WOUNDS={
 puncture:()=>hit('p',pistol,v(-.04,.16,.3),v(0,0,-1),23),
 cavity:()=>hit('c',rifle(150),v(-.04,.16,.3),v(0,0,-1),58),
 head:()=>hit('h',rifle(150),v(.04,.64,.3),v(0,0,-1),87,{headshot:true}),
 cut:()=>hit('k',meleeBodyImpact('greatsword'),v(-.04,.16,.3),v(-1,-.3,-.25),52),
 arm:()=>hit('a',meleeBodyImpact('sword'),v(.36,.08,.12),v(-1,0,-.15),30),
 stab:()=>hit('s',{...meleeBodyImpact('daggers',1,true),speed:40},v(-.04,.16,.3),v(0,0,-1),15),
 bruise:()=>hit('b',meleeBodyImpact('shield'),v(.12,-.42,.3),v(0,0,-1),23),
 scorch:()=>hit('f',projectileBodyImpact({spec:'fire',vel:v(0,0,24),radius:.14}),v(0,.1,-.3),v(0,0,1),22),
};
function view(id:string,wounds:BodyWound[]|undefined,extra:Partial<PlayerView>={}):PlayerView{
 return {id,name:id,color:'#3fc6ff',class:'ranged',spec:'pistol',pos:at,vel:v(0,0,0),yaw:0,pitch:0,hp:100,maxHp:100,mana:0,maxMana:0,ammo:0,maxAmmo:0,stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],connected:true,bot:null,ack:0,grounded:true,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],wounds,...extra} as PlayerView;
}
const snap=(time:number,players:PlayerView[],generation=1)=>({generation,setNumber:1,round:1,time,players,props:[]} as unknown as Snapshot);
const finite=(m:T.Object3D)=>{let ok=true;m.updateMatrixWorld(true);m.traverse(o=>{if(!o.matrixWorld.elements.every(Number.isFinite))ok=false;});return ok;};
const carved=(group:T.Object3D)=>{const out:T.InstancedMesh[]=[];group.traverse(o=>{if(o instanceof T.InstancedMesh&&currentLayout(o)?.custom&&!o.userData.bodyWound)out.push(o);});return out;};

test('wounds anchor on the struck part, at its face, travelling into it',()=>{
 for(const [name,make] of Object.entries(WOUNDS)){const w=make(),anchors=woundAnchors(w);assert.ok(anchors.length>=1,name);
  for(const a of anchors){const roots=w.part==='spine'?['spine','pelvis']:[w.part];assert.ok(roots.includes(a.root),`${name} ${a.root}`);assert.ok(restMeshes(a.root).some(m=>m.name===a.mesh),name);
   assert.ok(Math.abs(Math.abs(a.hit.getComponent(a.axis))-a.size.getComponent(a.axis)/2)<1e-9,`${name} on the face`);for(let k=0;k<3;k++)assert.ok(Math.abs(a.hit.getComponent(k))<=a.size.getComponent(k)/2+1e-9,name);
   assert.ok(Math.abs(a.dir.length()-1)<1e-9&&-a.sign*a.dir.getComponent(a.axis)>=.49,`${name} goes in`);}}
 // A shot through the sternum plate carries on into the ribcage behind it.
 assert.deepEqual(woundAnchors(hit('x',pistol,v(0,.15,.3),v(0,0,-1),23)).map(a=>a.mesh),['sternum','ribcage']);
});

test('carving stays inside the part, finite, and grows with the wound',()=>{
 const rig=createCharacterRig('#3fc6ff',undefined,true),cells:Record<string,number>={},opening:Record<string,number>={};
 for(const [name,make] of Object.entries(WOUNDS)){const w=make(),a=woundAnchors(w)[0],mesh=rig.group.getObjectByName(a.root)!.getObjectByName(a.mesh) as T.InstancedMesh;
  const out=carvePart('t',baseLayout(mesh)!,a.size,[specFor(w,a,false,[.8,.8,.8])]);cells[name]=out.cellCount;
  // Opening size: the area the flesh lining spans across the struck face (metres squared).
  const lo=[Infinity,Infinity,Infinity],hi=[-Infinity,-Infinity,-Infinity];for(const {box} of layoutBoxes(out.cells))for(let k=0;k<3;k++){lo[k]=Math.min(lo[k],box[k]*a.size.getComponent(k));hi[k]=Math.max(hi[k],box[k]*a.size.getComponent(k));}
  opening[name]=[0,1,2].filter(k=>k!==a.axis).reduce((area,k)=>area*Math.max(0,hi[k]-lo[k]),1);
  for(const layout of [out.shell,out.cells,out.shell.plain!,out.cells.plain!])for(const {box,color} of layoutBoxes(layout)){assert.ok([...box,...color].every(Number.isFinite),name);
   // In metres: cells may stand proud by the cavity rim lift, or overhang the back of a plate thinner than one cell.
   for(let k=0;k<3;k++){const size=a.size.getComponent(k);assert.ok((Math.abs(box[k])+Math.abs(box[k+3])/2-.5)*size<=.035,`${name} inside`);}}
  assert.ok(out.cellCount>0||w.kind==='scorch',name);}
 assert.ok(opening.cavity>opening.puncture*2,`cavity ${opening.cavity} > puncture ${opening.puncture}`);assert.ok(opening.head>opening.puncture*2,'head cavity');
 assert.ok(opening.cut>opening.stab,'a slash opens more of the face than a thrust');
});

test('living wounds attach to the struck part, follow it, transfer to copies, and dedupe',()=>{
 const scene=new T.Scene(),rig=createCharacterRig('#3fc6ff',undefined,true),rigs=new Map([['a',rig]]),layer=new BodyWounds(scene);scene.add(rig.group);
 const w=[WOUNDS.head(),WOUNDS.cavity(),WOUNDS.arm()],p=view('a',w);layer.update(snap(5,[p]),rigs,{quality:1,dt:1/60});
 assert.equal(layer.stats(rig.group).wounds,3);const parts=carved(rig.group);assert.ok(parts.length>=3);
 const head=rig.group.getObjectByName('head')!.getObjectByName('blank-head') as T.InstancedMesh;assert.ok(currentLayout(head)?.custom);
 const cells=head.getObjectByName('wound-cells') as T.InstancedMesh;assert.ok(cells&&cells.parent===head&&cells.geometry===WOUND_GEOMETRY&&cells.count>0,'flesh rides on the part');
 assert.equal(currentLayout(copyVoxelPart(head)),currentLayout(head),'ragdoll/knockdown copies keep the carved sculpt');
 const layouts=parts.map(m=>currentLayout(m));for(let n=0;n<3;n++)layer.update(snap(5+n,[view('a',w.map(x=>({...x,point:{...x.point}})))]),rigs,{quality:1,dt:1/60});
 assert.deepEqual(carved(rig.group).map(m=>currentLayout(m)),layouts,'repeated snapshots never rebuild');
 // Moving the part moves the wound with it.
 const before=cells.getWorldPosition(new T.Vector3());rig.head.rotation.x=.6;rig.group.updateMatrixWorld(true);assert.ok(cells.getWorldPosition(new T.Vector3()).distanceTo(before)>.01);
 assert.ok(finite(rig.group));
 // A rematch restarts ids: same id at a different authority time is a different wound.
 const replay={...w[0],at:w[0].at+30};assert.notEqual(woundKey(replay),woundKey(w[0]));
 layer.update(snap(40,[view('a',[replay])],2),rigs,{quality:1,dt:1/60});assert.equal(layer.stats(rig.group).wounds,1);
 // Heal / reset: the snapshot has no wounds any more.
 let released=0;for(const m of carved(rig.group))m.addEventListener('dispose',()=>released++);
 layer.update(snap(41,[view('a',undefined)],2),rigs,{quality:1,dt:1/60});
 assert.equal(carved(rig.group).length,0);assert.equal(currentLayout(head),baseLayout(head));assert.equal(head.getObjectByName('wound-cells'),undefined);assert.ok(released>=1,'GPU buffers released');
 layer.dispose();assert.equal(scene.getObjectByName('wound-spray'),undefined);
});

test('low quality carves coarser; low detail and see-through draw the plain variants',()=>{
 const scene=new T.Scene(),w=[WOUNDS.cavity()],counts:number[]=[];
 for(const quality of [1,.3]){const rig=createCharacterRig('#3fc6ff',undefined,true),layer=new BodyWounds(scene);layer.update(snap(5,[view('a',w)]),new Map([['a',rig]]),{quality,dt:1/60});counts.push(layer.stats(rig.group).cells);
  if(quality<1){setRigDetail(rig,true);const [part]=carved(rig.group),layout=currentLayout(part)!;assert.equal(part.instanceMatrix,(layout.plain as VoxelLayout).matrix,'low detail draws the plain carve');
   const cells=part.getObjectByName('wound-cells') as T.InstancedMesh;assert.equal(cells.instanceMatrix,currentLayout(cells)!.plain!.matrix);
   cells.material=new T.MeshBasicMaterial({transparent:true});setRigDetail(rig,false);assert.equal(cells.count,0,'see-through copies draw no flesh cells');}
  layer.dispose();}
 assert.ok(counts[1]<counts[0],`reduced ${counts[1]} < full ${counts[0]}`);
});

test('Siren, destroyed parts and the compatibility renderer get no carved wounds; first person gets its own',()=>{
 const scene=new T.Scene(),layer=new BodyWounds(scene),rig=createCharacterRig('#3fc6ff',undefined,true),fp=createCharacterRig('#3fc6ff',undefined,true),rigs=new Map([['a',rig]]);
 const sever=hit('s',rifle(270),v(0,.64,.3),v(0,0,-1),87,{headshot:true,lethal:true});assert.equal(sever.destroyedPart,'head');
 layer.update(snap(5,[view('a',[sever])]),rigs,{quality:1,dt:1/60});assert.equal(carved(rig.group).length,0,'severed/crushed parts belong to the death effect');
 layer.update(snap(5,[view('a',[WOUNDS.cavity()],{spec:'siren' as never})]),rigs,{quality:1,dt:1/60});assert.equal(carved(rig.group).length,0,'Siren keeps her own look');
 layer.update(snap(5,[view('a',[WOUNDS.cavity()])]),rigs,{quality:1,dt:1/60,enabled:false});assert.equal(carved(rig.group).length,0,'compatibility renderer');
 layer.update(snap(5,[view('a',[WOUNDS.arm()])]),rigs,{quality:1,dt:1/60,firstPerson:{group:fp.group,playerId:'a'},hideSpray:'a'});
 assert.ok(carved(rig.group).length>=1&&carved(fp.group).length>=1,'view-model arm shows the cut');
 assert.equal(layer.spray.mesh.count,0,'no spray at the first-person camera');
 // The rig leaves (player gone or rebuilt): restored, and the layer forgets it.
 layer.update(snap(6,[]),new Map(),{quality:1,dt:1/60});assert.equal(carved(rig.group).length+carved(fp.group).length,0);layer.dispose();
});

test('fresh wounds spray from the pool; late joins and old wounds do not; eight wounds stay bounded',()=>{
 const scene=new T.Scene(),layer=new BodyWounds(scene),rig=createCharacterRig('#3fc6ff',undefined,true),rigs=new Map([['a',rig]]);
 const w=WOUNDS.cavity();layer.update(snap(w.at+.05,[view('a',[w])]),rigs,{quality:1,dt:1/60});const count=layer.spray.mesh.count;assert.ok(count>0&&count<=layer.spray.capacity);
 for(let n=0;n<240;n++)layer.update(snap(w.at+.05+n/60,[view('a',[w])]),rigs,{quality:1,dt:1/60});assert.equal(layer.spray.mesh.count,0,'droplets land and fade');
 const late=new BodyWounds(scene),rig2=createCharacterRig('#3fc6ff',undefined,true);late.update(snap(w.at+2,[view('b',[w])]),new Map([['b',rig2]]),{quality:1,dt:1/60});assert.equal(late.spray.mesh.count,0);
 const many=Array.from({length:MAX_BODY_WOUNDS},(_,i)=>hit('m'+i,i%2?rifle(150):meleeBodyImpact('greatsword'),v(-.15+.04*i,.1+.03*(i%3),.3),v(-.2,0,-1),i%2?58:52,{at:1+i/10}));
 const rig3=createCharacterRig('#3fc6ff',undefined,true);late.update(snap(9,[view('c',many)]),new Map([['c',rig3]]),{quality:1,dt:1/60});
 const s=late.stats(rig3.group);assert.equal(s.wounds,MAX_BODY_WOUNDS);let instances=0;rig3.group.traverse(o=>{if(o instanceof T.InstancedMesh&&(currentLayout(o)?.custom))instances+=o.count;});
 assert.ok(instances<4000,`bounded instances ${instances}`);assert.ok(finite(rig3.group));layer.dispose();late.dispose();
});

test('caps: clean cuts are flush flesh and bone, crushed stumps are ragged; caps sit at the end of each part',()=>{
 const clean=woundCap({width:.15,depth:.15,kind:'clean',seed:7,shell:new T.Color(0x354853)}),ragged=woundCap({width:.15,depth:.15,kind:'ragged',seed:7,shell:new T.Color(0x354853)});
 for(const cap of [clean,ragged]){assert.equal(cap.name,'gibber-wound');assert.ok(cap.userData.bodyWound&&cap.count>10);const m=new T.Matrix4();for(let i=0;i<cap.count;i++){cap.getMatrixAt(i,m);assert.ok(m.elements.every(Number.isFinite));}}
 const top=(cap:T.InstancedMesh)=>{let y=0;const m=new T.Matrix4(),p=new T.Vector3(),s=new T.Vector3();for(let i=0;i<cap.count;i++){cap.getMatrixAt(i,m);m.decompose(p,new T.Quaternion(),s);y=Math.max(y,p.y+s.y/2);}return y;};
 assert.ok(top(ragged)>top(clean)+.01,'bone splinters and torn shell stand out of a crushed stump');clean.dispose();ragged.dispose();
 // The neck: the body's geometry ends just above the head joint, the head's just below (its jaw overlaps the neck).
 const rig=createCharacterRig('#3fc6ff',undefined,true);rig.group.updateMatrixWorld(true);const joint=rig.head.getWorldPosition(new T.Vector3()),bone=new T.Vector3(0,boneSign('head'),0);
 const inverses=(root:string,names:string[])=>names.map(n=>rig.group.getObjectByName(root)!.getObjectByName(n)!.matrixWorld.clone().invert());
 const stump=faceAlong(joint,bone,inverses('spine',['neck-joint','upper-chest','ribcage']),'parent'),piece=faceAlong(joint,bone,inverses('head',['blank-head','jaw']),'child');
 assert.ok(stump>=0&&stump<.02,`stump ${stump}`);assert.ok(piece<0&&piece>-.06,`piece ${piece}`);
});

test('crush fragments are bounded, deterministic for any snapshot timing, land and fade, and dispose',()=>{
 const rig=createCharacterRig('#3fc6ff',undefined,true);rig.group.position.set(0,.85,0);rig.group.updateMatrixWorld(true);const sources:{matrix:T.Matrix4;color:T.Color}[]=[];
 rig.head.traverse(o=>{if(o instanceof T.Mesh)sources.push({matrix:o.matrixWorld.clone(),color:new T.Color(0xdce5e8)});});
 const make=(limit:number)=>new CrushDebris({sources,loose:[{object:new T.Group()}],origin:rig.head.getWorldPosition(new T.Vector3()),direction:new T.Vector3(0,0,-1),inherited:new T.Vector3(0,0,-2),kick:2,severity:1,seed:42,limit,floor:0});
 for(const limit of [0,1,5,40]){const d=make(limit);assert.ok(d.count<=limit,`${d.count}<=${limit}`);d.dispose();}
 const a=make(40),b=make(40),m1=new T.Matrix4(),m2=new T.Matrix4();for(let n=0;n<=120;n++)a.update(n/60);b.update(2);
 const mesh=(d:CrushDebris)=>d.root.getObjectByName('crush-fragments') as T.InstancedMesh;for(let i=0;i<mesh(a).count;i++){mesh(a).getMatrixAt(i,m1);mesh(b).getMatrixAt(i,m2);assert.ok(m1.elements.every((x,k)=>Math.abs(x-m2.elements[k])<1e-9),'late == stepped');
  const p=new T.Vector3().setFromMatrixPosition(m1);assert.ok(p.y>-1e-6&&p.y<.2,`landed ${p.y}`);}
 a.update(5);mesh(a).getMatrixAt(0,m1);assert.ok(new T.Vector3().setFromMatrixScale(m1).length()<1e-6,'faded at the end of the death');
 const scene=new T.Scene();scene.add(a.root);a.dispose();assert.equal(scene.children.length,0);b.dispose();
});

/** Ragdoll-style part list from a posed rig (what ragdoll.ts builds), for the collapse without a physics world. */
function partsOf(rig:ReturnType<typeof createCharacterRig>,free:string[]=[]):CollapsePart[]{
 const names=['pelvis','spine','head','left-shoulder','left-elbow','right-shoulder','right-elbow','left-hip','left-knee','right-hip','right-knee'],hinges=new Set(['left-elbow','right-elbow','left-knee','right-knee','head']);
 rig.group.updateMatrixWorld(true);const roots=names.map(n=>rig.group.getObjectByName(n)!),set=new Set(roots),frames=new Map<T.Object3D,T.Quaternion>(),groups=new Map<string,T.Group>(),ankles=new Map<string,{pivot:T.Vector3;frame:T.Quaternion}>();
 for(const root of roots){const pos=root.getWorldPosition(new T.Vector3()),q=root.getWorldQuaternion(new T.Quaternion());let pp=root.parent;while(pp&&!set.has(pp))pp=pp.parent;const pf=pp&&frames.get(pp);let rot=q;
  if(hinges.has(root.name)&&pf){const rel=pf.clone().invert().multiply(q);rot=pf.clone().multiply(new T.Quaternion().setFromAxisAngle(new T.Vector3(1,0,0),2*Math.atan2(rel.x,rel.w)));}frames.set(root,rot);
  const g=new T.Group();g.position.copy(pos);g.quaternion.copy(rot);const inv=new T.Matrix4().compose(pos,rot,new T.Vector3(1,1,1)).invert();
  const walk=(o:T.Object3D,ankle:boolean)=>{if(o!==root&&set.has(o)||/socket|cosmetic/.test(o.name))return;const a=ankle||o.name.endsWith('-ankle');if(o instanceof T.Mesh&&o.visible){const c=new T.Mesh(o.geometry);inv.clone().multiply(o.matrixWorld).decompose(c.position,c.quaternion,c.scale);if(a)c.userData.ankle=true;g.add(c);}for(const ch of o.children)walk(ch,a);};walk(root,false);groups.set(root.name,g);
  const ankle=root.name.endsWith('-knee')&&rig.group.getObjectByName(root.name.replace('-knee','-ankle'));if(ankle)ankles.set(root.name,{pivot:ankle.getWorldPosition(new T.Vector3()).applyMatrix4(inv),frame:rot.clone().invert().multiply(ankle.getWorldQuaternion(new T.Quaternion()))});}
 return roots.map(root=>{let pp=root.parent;while(pp&&!set.has(pp))pp=pp.parent;const g=groups.get(root.name)!,pg=pp&&groups.get(pp.name),isFree=free.includes(root.name);
  return {name:root.name,parent:isFree?undefined:pp?.name,position:g.position.clone(),quaternion:g.quaternion.clone(),anchor:pg&&!isFree?g.position.clone().sub(pg.position).applyQuaternion(pg.quaternion.clone().invert()):undefined,free:isFree,boxes:partBoxes(g),ankle:ankles.get(root.name)};});
}
const LIMITS:Record<string,[number,number]>={'left-elbow':[-2.6,.05],'right-elbow':[-2.6,.05],'left-knee':[-.05,2.6],'right-knee':[-.05,2.6],head:[-.55,.65]};
function lowest(part:CollapsePart,pose:{position:T.Vector3;quaternion:T.Quaternion;ankle?:T.Quaternion}){let low=Infinity;const c=new T.Vector3();
 for(const b of part.boxes)for(let i=0;i<8;i++){c.set(i&1?b.half.x:-b.half.x,i&2?b.half.y:-b.half.y,i&4?b.half.z:-b.half.z).applyQuaternion(b.quaternion).add(b.center);if(b.ankle&&pose.ankle&&part.ankle)c.sub(part.ankle.pivot).applyQuaternion(pose.ankle).add(part.ankle.pivot);c.applyQuaternion(pose.quaternion).add(pose.position);low=Math.min(low,c.y);}return low;}

test('kneel/fall: jolt, knees on the floor, forward fall, all from authority time, inside joint limits, no pops',()=>{
 for(const variant of ['cavity','sever'] as const){
  const yaw=.7,rig=createCharacterRig('#3fc6ff',undefined,true);rig.group.position.set(2,1,3);rig.group.rotation.y=yaw;const parts=partsOf(rig,variant==='sever'?['head']:[]),ground=1-.85;
  const w=hit('k',rifle(variant==='sever'?270:150),v(0,.64,.3),v(0,0,-1),87,{headshot:true,lethal:true,yaw,position:v(2,1,3)}),m=deathMotion({wound:w,grounded:true,incapacitated:false,yaw,velocity:v(.5,0,-.3)});
  assert.equal(m.kind,'kneel-fall');assert.ok(KneelFall.supports(parts));const k=new KneelFall(parts,m,{ground,shot:w.direction,severity:w.severity});
  assert.ok(Math.abs(k.release*60-Math.round(k.release*60))<1e-9&&k.release>=m.releaseAt&&k.release<m.releaseAt+1/60+1e-9,'release on a physics step');
  assert.equal(k.driven.includes('head'),variant==='cavity');
  const byName=new Map(parts.map(p=>[p.name,p])),legs=['left-knee','right-knee'],start=Math.min(...legs.map(n=>lowest(byName.get(n)!,k.pose(0).get(n)!)));
  // t=0 is exactly the death pose.
  for(const [name,pose] of k.pose(0)){const p=byName.get(name)!;assert.ok(pose.position.distanceTo(p.position)<1e-6&&Math.abs(pose.quaternion.angleTo(p.quaternion))<1e-6,`${variant} ${name} starts at the death pose`);}
  let prev=new Map([...k.pose(0)].map(([n,s])=>[n,s.position.clone()]));
  for(let t=1/240;t<=k.release+1e-9;t+=1/240){const pose=k.pose(t);
   for(const [name,s] of pose){assert.ok(s.position.distanceTo(prev.get(name)!)<.03,`${variant} ${name} continuous at ${t.toFixed(3)}`);const [lo,hi]=LIMITS[name]||[-9,9];
    if(LIMITS[name]){const h=k.hinge(t,name);assert.ok(h>=lo-1e-6&&h<=hi+1e-6,`${variant} ${name} hinge ${h.toFixed(3)} at ${t.toFixed(3)}`);}
    if(!legs.includes(name)&&!/hip$/.test(name))assert.ok(lowest(byName.get(name)!,s)>=ground+.02,`${variant} ${name} above the floor at ${t.toFixed(3)}`);}
   for(const n of legs)assert.ok(lowest(byName.get(n)!,pose.get(n)!)>=Math.min(start,ground)-1e-6,`${variant} ${n} never sinks at ${t.toFixed(3)}`);
   prev=new Map([...pose].map(([n,s])=>[n,s.position.clone()]));}
  // On the knees at kneesAt: the kneecaps/shins rest on the floor and the pelvis is lower than standing.
  const kneel=k.pose(m.kneesAt);for(const n of legs){const low=lowest(byName.get(n)!,kneel.get(n)!);assert.ok(low>=ground&&low<=ground+.02,`${variant} ${n} on the floor ${low.toFixed(3)}`);}
  assert.ok(kneel.get('pelvis')!.position.y<byName.get('pelvis')!.position.y-.08,'knees bent, body lower');
  // Forward: the chest ends up ahead of where it started, along the facing.
  const forward=new T.Vector3(Math.sin(yaw),0,Math.cos(yaw)),chest=(t:number)=>k.pose(t).get('spine')!.position.clone();assert.ok(chest(k.release).sub(chest(0)).dot(forward)>.3,`${variant} falls forward`);
  if(variant==='cavity'){const nod=k.hinge(.05,'head');assert.ok(nod<-.15,`a shot from the front snaps the head back (${nod.toFixed(2)})`);}
  // Pure function of time: any order of samples gives the same pose; velocities match the pose.
  const late=k.pose(.61).get('spine')!.position.clone();k.pose(.1);k.pose(.8);assert.ok(k.pose(.61).get('spine')!.position.distanceTo(late)<1e-12);
  const lin=new T.Vector3(),ang=new T.Vector3();k.velocity(.6,'spine',lin,ang);const fd=k.pose(.6+1/480).get('spine')!.position.clone().sub(k.pose(.6-1/480).get('spine')!.position).multiplyScalar(240);assert.ok(lin.distanceTo(fd)<1e-6);
  let visited=0;k.each(.5,(name,pose,l,a)=>{visited++;assert.ok([pose.position.x,pose.position.y,pose.position.z,l.x,l.y,l.z,a.x,a.y,a.z].every(Number.isFinite),name);});assert.equal(visited,k.driven.length);
 }
});

test('kneel/fall: cover in front shortens the scripted fall; missing legs or an airborne victim do not kneel',()=>{
 const rig=createCharacterRig('#3fc6ff',undefined,true);rig.group.position.set(0,.85,0);const parts=partsOf(rig);
 const w=hit('k',rifle(150),v(0,.64,.3),v(0,0,-1),87,{headshot:true,lethal:true}),m=deathMotion({wound:w,grounded:true,incapacitated:false,yaw:0,velocity:v(0,0,0)});
 const open=new KneelFall(parts,m,{ground:0}),wall=new KneelFall(parts,m,{ground:0,props:[{pos:{x:0,y:1,z:.9},size:{x:3,y:2,z:.3}}]});
 assert.ok(open.fallAngle>.9&&open.fallAngle<=COLLAPSE.fallAngle,`open ${open.fallAngle}`);assert.ok(wall.fallAngle<open.fallAngle-.3,`wall ${wall.fallAngle}`);
 assert.equal(KneelFall.supports(parts.filter(p=>p.name!=='left-knee')),false);assert.equal(KneelFall.supports(partsOf(rig,['right-hip'])),false);
 assert.equal(deathMotion({wound:w,grounded:false,incapacitated:false,yaw:0,velocity:v(0,2,0)}).kind,'ragdoll','airborne victims keep falling');
});

test('spray profiles: the DEV Gibber fountain is unchanged; the impact profiles are smaller and bounded',()=>{
 const gibber=new GibberVfx(9,220,1),impact=new GibberVfx(9,220,1,SEVER_PROFILE),low=new GibberVfx(9,220,.3,SEVER_PROFILE),tiny=new GibberVfx(9,5,1,SEVER_PROFILE);
 assert.equal(gibber.count,160);assert.equal(impact.count,80);assert.equal(low.count,40);assert.ok(tiny.count<=5);
 for(const vfx of [gibber,impact]){for(let n=0;n<120;n++)vfx.step(n/60,new T.Vector3(0,1.5,0),new T.Vector3(0,1,0));vfx.update(2);vfx.dispose();}low.dispose();tiny.dispose();
});
