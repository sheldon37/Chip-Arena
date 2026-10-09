// Build-time only (C18): run the pinned original client/ragdoll-vfx.ts (RagdollVfx), gibber-vfx.ts (GibberVfx and its
// profiles), body-trauma.ts (woundCap, faceAlong, CrushDebris), death-effects.ts (voxel-burst) and shared/body-ragdoll.ts
// (bodyPartVelocity, which a crushed part's fragments inherit) and record what Tests/KillEffectChecks.cs compares
// (Tests/kill-effect-parity.json). Also writes Core/NativeKillBoxes.Generated.cs: the box meshes of the source rig (pilot
// body, every held weapon and every hat) in traversal order, local to their rig bone, which voxel-burst, the Soul Drift
// ghost, the stump caps and crush fragments are built from.
// Usage: node tools/export_kill_effect_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/ragdoll-vfx.ts':'5d0c30a37b5b398da153621f5024b402b834aff9','src/client/gibber-vfx.ts':'261f00b6848da45ededad4238a671e804c632bf9',
 'src/client/body-trauma.ts':'ca72905edbb822426f8b3aef1c7bc18e397faff6','src/client/death-effects.ts':'ea5832858a19444d9b1b94f28e1ed8f9c55466e2',
 'src/client/ragdoll.ts':'02d00c0893efeca829c1ffe6f32a7be4050903b0','src/client/character-rig.ts':'5a020f684a9a2a29238769f3e18a61cddf545e20',
 'src/client/weapon-visuals.ts':'913f954df486a8ebb73f46d48363b2d447a04521','src/client/cosmetics.ts':'1c2586c79b5020fbc385aa0e4a32951f8ccf9cc5',
 'src/client/body-wounds.ts':'216f47aba9f0e965d8ed8aad862c876765bd44b6','src/client/character-animation.ts':'7adee1f45fb45b1cc20245ebf0ae559e6db99870',
 'src/client/pve/downed.ts':'d55118853db5c7b34a3ae7cbaf0943a7fc196249','src/shared/arena.ts':'d476b1c8049169273d79f45b9300dff61ba5c039',
 'src/shared/ragdoll-styles.ts':'3e32b814d6da0735af5b365e680624cd97891ce5','src/shared/body-ragdoll.ts':'0b4411c9e7cf8b553861df24868cf63e20da5f03',
 'src/shared/config.ts':'54454ee3110e936ada16d429813bb55b4a07eb1f'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const load=f=>import(pathToFileURL(path.join(source,'src',f)).href);
const {RagdollVfx}=await load('client/ragdoll-vfx.ts'),{GibberVfx,GIBBER_PROFILE,SEVER_PROFILE,CRUSH_PROFILE}=await load('client/gibber-vfx.ts');
const {woundCap,faceAlong,CrushDebris,CAP_MESH,boneSign}=await load('client/body-trauma.ts'),{KILL_EFFECTS}=await load('client/death-effects.ts');
const {createCharacterRig}=await load('client/character-rig.ts'),{buildWeapon}=await load('client/weapon-visuals.ts'),{dressRig}=await load('client/cosmetics.ts');
const {bodyPartVelocity,BODY_RAGDOLL}=await load('shared/body-ragdoll.ts'),{ANIMATION}=await load('client/character-animation.ts'),{DOWNED}=await load('client/pve/downed.ts'),{ARENA}=await load('shared/arena.ts'),{RAGDOLL_STYLES}=await load('shared/ragdoll-styles.ts');
// A decomposed rotation is compared with its sign fixed (w >= 0).
const canon=q=>q.w<0?new T.Quaternion(-q.x,-q.y,-q.z,-q.w):q;
const r6=v=>Math.round(v*1e6)/1e6,v3=v=>[r6(v.x),r6(v.y),r6(v.z)],q4=q=>[r6(q.x),r6(q.y),r6(q.z),r6(q.w)];
// A list of equal-length number rows: count, index-weighted column sums and the first and last rows in full.
const digest=rows=>{const n=rows.length,w=n?rows[0].length:0,sums=Array(w).fill(0);rows.forEach((row,i)=>row.forEach((x,k)=>sums[k]+=(i+1)*x));return {n,sums:sums.map(r6),first:n?rows[0].map(r6):null,last:n?rows[n-1].map(r6):null};};
const BONES=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const ROOTS=['pelvis','spine','head','left-shoulder','left-elbow','right-shoulder','right-elbow','left-hip','left-knee','right-hip','right-knee'];
const SOCKETS={'weapon-socket':1,'offhand-weapon-socket':1,'head-cosmetic':2,'chest-cosmetic':2,'back-socket':2};
const MAGIC=['fire','water','lightning','darkness','leet'],DUAL=['daggers','emperor','ansatsuken'];
const SPECS=['pistol','rifle','machinegun','fire','water','lightning','darkness','sword','daggers','shield','greatsword','emperor','ansatsuken','leet'];
const HATS=['cap','beanie','top','wizard','crown','cone','antenna','halo','cowboy','auditor-halo'];
// The rig's material colours by name (character-rig.ts BODY/ACCENT/EDGE/FLEX_PARTS), checked against marker colours below.
const BODY_PARTS=/^(hip-shell|abdomen-shell|ribcage|upper-chest|collar|blank-head|crown)$|-(upper-arm|forearm|thumb|thigh|calf|shin)$/,ACCENT_PARTS=/^(waist-band|sternum)$|-(shoulder-cap|cuff|kneecap)$/,EDGE_PARTS=/^(back-panel|jaw)$|-(palm|fingers|foot)$/,FLEX_PARTS=/^(abdomen|neck-joint)$|-(elbow-joint|hip-joint|knee-joint|heel)$/;
const MARK_ACCENT='#2d6cdf',MARK_BODY='#c8553d',hex=c=>c.getHex();
const boneOf=(rig,o)=>{for(let q=o.parent;q;q=q.parent){const k=BONES.findIndex(b=>rig[b]===q);if(k>=0)return k;}return -1;};
function record(rig,o,tagged){
 const anchor=boneOf(rig,o),local=new T.Matrix4();for(let p=o;p&&p!==rig[BONES[anchor]];p=p.parent)local.premultiply(p.matrix);
 const pos=new T.Vector3(),q=new T.Quaternion(),s=new T.Vector3();local.decompose(pos,q,s);
 const back=new T.Matrix4().compose(pos,q,s);for(let k=0;k<16;k++)if(Math.abs(back.elements[k]-local.elements[k])>1e-6)throw Error('Box local is not a TRS: '+o.name);
 let rootBone=-1,zone=0;for(let p=o.parent;p;p=p.parent){if(rootBone<0&&ROOTS.includes(p.name))rootBone=BONES.findIndex(b=>rig[b]===p);if(SOCKETS[p.name]&&!zone)zone=SOCKETS[p.name];}
 let hidden=false;for(let p=o;p;p=p.parent)if(!p.visible)hidden=true;
 const mat=o.material,color=hex(mat.color),glow=mat.emissive?mat.emissive.clone().multiplyScalar(mat.emissiveIntensity??1):new T.Color(0);
 let tag=0;if(tagged){tag=BODY_PARTS.test(o.name)?1:ACCENT_PARTS.test(o.name)?2:EDGE_PARTS.test(o.name)?3:FLEX_PARTS.test(o.name)?4:0;}
 else tag=color===new T.Color(MARK_ACCENT).getHex()?2:color===new T.Color(MARK_BODY).getHex()?1:0;
 if(tag===1&&color!==new T.Color(MARK_BODY).getHex()||tag===2&&color!==new T.Color(MARK_ACCENT).getHex())throw Error('Unexpected rig colour: '+o.name);
 return {name:o.name,bone:anchor,root:rootBone,zone,detail:!!o.userData.detail,hidden,tag,color,glow:[r6(glow.r),r6(glow.g),r6(glow.b)],p:v3(pos),q:q4(q),s:v3(s)};
}
const isBox=o=>o.isMesh&&o.geometry.type==='BoxGeometry'&&o.parent?.name!=='enchantment'&&!o.userData.bodyWound;
// A visible mesh that is not a box (a hat's cylinders and cones, a caster's crystal): the Soul Drift ghost then draws the model.
const round=root=>{let found=false;root.traverse(o=>{if(!o.isMesh||o.geometry.type==='BoxGeometry')return;let shown=true;for(let p=o;p;p=p.parent)if(!p.visible)shown=false;if(shown)found=true;});return found;};
// Body boxes in rig.group traversal order, with the slot where each socket's subtree is visited.
const bodyRig=createCharacterRig(MARK_ACCENT,MARK_BODY),body=[],slots={};bodyRig.group.updateMatrixWorld(true);
bodyRig.group.traverse(o=>{if(SOCKETS[o.name]!==undefined&&slots[o.name]===undefined)slots[o.name]=body.length;if(isBox(o))body.push(record(bodyRig,o,true));});
const edgeHex=hex(bodyRig.group.getObjectByName('jaw').material.color),flexHex=hex(bodyRig.group.getObjectByName('abdomen').material.color);
// The game rig's hand sockets keep their offsets (renderer.ts equip); casters' weapons move to the left hand (magic-motion.ts).
function weaponRig(spec,accent=MARK_ACCENT,bodyColor=MARK_BODY){const rig=createCharacterRig(accent,bodyColor);buildWeapon(rig.weapon,spec);
 if(MAGIC.includes(spec))rig.sockets.leftHand.add(rig.weapon);if(DUAL.includes(spec))buildWeapon(rig.offWeapon,spec,true);rig.group.updateMatrixWorld(true);return rig;}
const weapons=SPECS.map(spec=>{const rig=weaponRig(spec),main=[],off=[],rounded=round(rig.weapon)||round(rig.offWeapon);rig.weapon.traverse(o=>{if(isBox(o))main.push(record(rig,o,false));});rig.offWeapon.traverse(o=>{if(isBox(o))off.push(record(rig,o,false));});return {spec,magic:MAGIC.includes(spec),rounded,main,off};});
const hats=HATS.map(hat=>{const rig=createCharacterRig(MARK_ACCENT,MARK_BODY);dressRig(rig,{bodyColor:MARK_BODY,hat,killEffect:'impact-ragdoll'});rig.group.updateMatrixWorld(true);const list=[];rig.sockets.head.traverse(o=>{if(isBox(o))list.push(record(rig,o,false));});return {hat,rounded:round(rig.sockets.head),list};});
const socketLocal=name=>{{const o=bodyRig.group.getObjectByName(name);if(Math.abs(o.quaternion.w-1)>1e-9)throw Error('Rotated socket '+name);}const o=bodyRig.group.getObjectByName(name),bone=boneOf(bodyRig,o),m=new T.Matrix4();for(let p=o;p&&p!==bodyRig[BONES[bone]];p=p.parent)m.premultiply(p.matrix);const pos=new T.Vector3(),q=new T.Quaternion(),s=new T.Vector3();m.decompose(pos,q,s);return {bone,p:v3(pos)};};
const capMesh=Object.fromEntries(Object.entries(CAP_MESH).map(([joint,mesh])=>[BONES.findIndex(b=>bodyRig[b].name===joint),mesh]));

// ---- Generated C# table -------------------------------------------------------------------------------------------
const f=v=>{const s=String(r6(v));return (s.includes('.')||s.includes('e')?s:s+'.0')+'f';};
const box=b=>`new KillBox("${b.name}",${b.bone},${b.root},${b.zone},${b.detail},${b.hidden},${b.tag},0x${b.color.toString(16).padStart(6,'0')},${b.glow.map(f).join(',')},${[...b.p,...b.q,...b.s].map(f).join(',')})`;
const list=a=>a.length?`new KillBox[]{\n  ${a.map(box).join(',\n  ')}}`:'new KillBox[0]';
const cs=`// Generated by tools/export_kill_effect_fixtures.mjs from the pinned source (05950eaef80ff2483a9c44d898cad98be1a76bbc):
// client/character-rig.ts createCharacterRig, weapon-visuals.ts buildWeapon (on the game rig's hand sockets,
// casters' weapons in the left hand as magic-motion.ts holds them) and cosmetics.ts dressRig. Every BoxGeometry mesh (not the
// enchantment or wound cells) in rig traversal order: name, rig bone it moves with, ragdoll part root bone (-1 none), socket
// zone (0 body, 1 weapon, 2 cosmetic), detail, hidden (it or an ancestor invisible), colour tag (0 fixed, 1 body, 2 accent, 3 body edge, 4 body flex), fixed
// colour, emissive (linear), then position, quaternion and scale local to the bone in source (three.js) axes. Do not edit.
namespace ChipArena.UnityConversion
{
    public static partial class NativeKillBoxes
    {
        public static readonly KillBox[] Body=${list(body)};
        /// <summary>Where rig traversal visits the head cosmetic socket, the off-hand socket and the weapon socket.</summary>
        public const int HatSlot=${slots['head-cosmetic']},OffhandSlot=${slots['offhand-weapon-socket']},WeaponSlot=${slots['weapon-socket']};
        /// <summary>Body edge and flex colours of the marker body colour ${MARK_BODY} (checks only).</summary>
        public const int MarkerBody=0x${new T.Color(MARK_BODY).getHex().toString(16)},MarkerEdge=0x${edgeHex.toString(16)},MarkerFlex=0x${flexHex.toString(16)};
        public static readonly string[] Specs={${SPECS.map(s=>'"'+s+'"').join(',')}};
        public static readonly bool[] Magic={${weapons.map(w=>w.magic).join(',')}};
        /// <summary>The weapon (or its off-hand twin) has visible meshes that are not boxes.</summary>
        public static readonly bool[] WeaponRound={${weapons.map(w=>w.rounded).join(',')}};
        public static readonly KillBox[][] Weapons={\n ${weapons.map(w=>list(w.main)).join(',\n ')}};
        public static readonly KillBox[][] Offhands={\n ${weapons.map(w=>list(w.off)).join(',\n ')}};
        public static readonly string[] Hats={${HATS.map(s=>'"'+s+'"').join(',')}};
        public static readonly KillBox[][] HatBoxes={\n ${hats.map(h=>list(h.list)).join(',\n ')}};
        /// <summary>The hat has visible meshes that are not boxes (cylinders, cones, spheres, rings).</summary>
        public static readonly bool[] HatRound={${hats.map(h=>h.rounded).join(',')}};
        /// <summary>body-trauma.ts CAP_MESH: the box whose cross-section a cut through each ragdoll joint (rig bone) exposes.</summary>
        public static readonly int[] CapJoints={${Object.keys(capMesh).join(',')}};public static readonly string[] CapMeshes={${Object.values(capMesh).map(s=>'"'+s+'"').join(',')}};
        /// <summary>The head cosmetic socket's position on the head bone (a loose hat flies from there).</summary>
        public static readonly float[] HatSocket={${socketLocal('head-cosmetic').p.map(f).join(',')}};
        /// <summary>The right and left hand sockets' positions on their wrist bones (a held weapon's frame; the corpse's dropped
        /// weapon body starts at the wrist).</summary>
        public static readonly float[] RightHandSocket={${socketLocal('right-hand-socket').p.map(f).join(',')}},LeftHandSocket={${socketLocal('left-hand-socket').p.map(f).join(',')}};
    }
}
`;
fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativeKillBoxes.Generated.cs'),cs);

// ---- Fixtures --------------------------------------------------------------------------------------------------------
const out={source:pinned,constants:{deathSeconds:ANIMATION.deathSeconds,deathVoxels:ANIMATION.deathVoxels,reducedDeathVoxels:ANIMATION.reducedDeathVoxels,maxDeathVoxels:ANIMATION.maxDeathVoxels,keepAge:DOWNED.keepAge,keptBudget:DOWNED.budget,arenaWidth:ARENA.width,arenaDepth:ARENA.depth,styles:Object.keys(RAGDOLL_STYLES)},
 markers:{accent:MARK_ACCENT,body:MARK_BODY,edge:edgeHex,flex:flexHex}};
// 1. RagdollVfx counts over budgets and qualities.
const styles=Object.keys(RAGDOLL_STYLES);out.swarmCounts=[];
for(const style of styles)for(const quality of [1,.3])for(const budget of [269,85,20,7,4,3,2,1,0]){const v=new RagdollVfx(style,1234,budget,quality);out.swarmCounts.push({style,quality,budget,count:v.count,total:v.points.geometry.getAttribute('position').count,rings:v.rings.length,core:!!v.core});}
// 2. RagdollVfx frames: a moving centre and eleven moving parts (part meshes carry a Lambert child to record the recolour).
const centerAt=a=>new T.Vector3(.4*Math.sin(a),.35+.1*Math.cos(a*2),-.3+.2*a);
const partAt=(a,k)=>centerAt(a).add(new T.Vector3(Math.cos(k*1.3+a)*.3,(k%4)*.15-.1,Math.sin(k*1.7-a*.5)*.25));
const ages=[0,.05,.3,.61,.9,1.1,1.15,1.6,1.7,1.75,1.85,2.0,2.4,2.45,2.5,3.1,4.0,4.4,4.9];
out.swarm=[];
for(const style of styles)for(const [quality,seed,budget] of [[1,48213,269],[.3,48213,269],[1,7,85]]){
 const v=new RagdollVfx(style,seed,budget,quality),parts=[];for(let k=0;k<11;k++){const mesh=new T.Group(),m=new T.Mesh(new T.BoxGeometry(1,1,1),new T.MeshLambertMaterial({color:0xe5e7eb,transparent:style==='ragdoll-soul'}));mesh.add(m);parts.push({mesh,body:{}});}
 const attr=v.points.geometry.getAttribute('position'),tint=v.points.geometry.getAttribute('color'),colors=[];for(let i=0;i<attr.count;i++)colors.push([r6(tint.getX(i)),r6(tint.getY(i)),r6(tint.getZ(i))]);
 const frames=[];
 for(const age of ages){
  const center=centerAt(age);parts.forEach((p,k)=>{p.mesh.position.copy(partAt(age,k));p.mesh.scale.setScalar(1);});v.update(age,center,parts);
  const pts=[];for(let i=0;i<attr.count;i++)pts.push([attr.getX(i),attr.getY(i),attr.getZ(i)]);
  const mat=parts[0].mesh.children[0].material;
  frames.push({age,points:digest(pts),opacity:r6(v.points.material.opacity),collapse:r6(parts[0].mesh.scale.x),
   body:{color:mat.color.getHex(),opacity:r6(mat.opacity),emissive:mat.emissive.getHex()},
   core:v.core?{p:v3(v.core.position),s:r6(v.core.scale.x)}:null,
   rings:v.rings.map(ring=>({p:v3(ring.position),s:r6(ring.scale.x),r:[r6(ring.rotation.x),r6(ring.rotation.y),r6(ring.rotation.z)],opacity:r6(ring.material.opacity),visible:ring.visible}))});
 }
 out.swarm.push({style,quality,seed,budget,size:v.points.material.size,colors,ringColors:v.rings.map(r=>r.material.color.getHex()),ringTubes:v.rings.map(r=>r.geometry.parameters.tube),
  coreColor:v.core?v.core.material.color.getHex():null,frames});
}
// 3. GibberVfx: the three profiles over qualities and budgets, emitting from a moving point (fixed 1/60 steps).
const pointAt=t=>new T.Vector3(.3*Math.sin(t*2.1),1.1-.6*Math.min(1,t)+.05*Math.sin(t*5),-.2+.15*t),normalAt=t=>new T.Vector3(Math.sin(t),1,.4*Math.cos(t)).normalize();
out.spray=[];const profiles={gibber:GIBBER_PROFILE,sever:SEVER_PROFILE,crush:CRUSH_PROFILE};
const sprayAges=[0,1/60,.1,.25,.5,.56,.9,1.2,1.25,1.8,2.5,3.6,3.7,4.2,4.4,4.8];
const m=new T.Matrix4(),pp=new T.Vector3(),qq=new T.Quaternion(),ss=new T.Vector3(),cc=new T.Color();
for(const [name,quality,budget,seed,floor] of [['gibber',1,267,9001,0],['gibber',.3,267,9001,0],['gibber',1,83,4242,0],['gibber',1,12,77,0],['sever',1,267,31337,.12],['sever',.3,267,31337,.12],['crush',1,268,555,.1],['crush',.3,268,555,.1]]){
 const profile=name==='gibber'?GIBBER_PROFILE:{...profiles[name],floor},g=new GibberVfx(seed,budget,quality,profile);let elapsed=0;const frames=[];
 const colors=[];for(let i=0;i<g.spray.count;i++){g.spray.getColorAt(i,cc);colors.push(cc.getHex());}
 for(const age of sprayAges){while(elapsed+1/60<=age+1e-6){g.step(elapsed,pointAt(elapsed),normalAt(elapsed));elapsed+=1/60;}g.update(age);
  const drops=g.drops.map(d=>[d.pos.x,d.pos.y,d.pos.z,d.vel.x,d.vel.y,d.vel.z,d.life,d.size]),shown=[];
  g.drops.forEach((d,i)=>{if(d.life<=0)return;g.spray.getMatrixAt(i,m);m.decompose(pp,qq,ss);const q=canon(qq);shown.push([i,pp.x,pp.y,pp.z,q.x,q.y,q.z,q.w,ss.x,ss.y,ss.z]);});
  frames.push({age,drops:digest(drops),shown:digest(shown),pools:g.pools.map(p=>({visible:p.visible,p:v3(p.position),s:[r6(p.scale.x),r6(p.scale.y)],rz:r6(p.rotation.z)})),poolOpacity:g.pools.length?r6(g.pools[0].material.opacity):null});}
 out.spray.push({profile:name,quality,budget,seed,floor,count:g.count,drops:g.drops.length,pools:g.pools.length,colors,material:hex(g.spray.material.color),glow:profile.glow??null,frames});
}
// 4. woundCap cells.
out.caps=[];
for(const [width,depth,kind,seed,shell,bone,low] of [[.138,.138,'clean',123456789,0x354853,'centre',false],[.138,.138,'ragged',987654321,0x354853,'centre',false],[.3312,.253,'clean',42,0xe5e7eb,'back',false],[.3312,.253,'ragged',4242,0xc8553d,'back',true],[.161,.1702,'clean',2654435761,0xc8553d,'centre',true],[.147,.156,'ragged',3,0x70e1d0,'none',false],[.04,.2,'clean',5,0x808080,'centre',false]]){
 const mesh=woundCap({width,depth,kind,seed,shell:new T.Color(shell),bone,low}),cells=[];
 for(let i=0;i<mesh.count;i++){mesh.getMatrixAt(i,m);m.decompose(pp,qq,ss);mesh.getColorAt(i,cc);cells.push({p:v3(pp),q:q4(canon(qq)),s:v3(ss),c:[r6(cc.r),r6(cc.g),r6(cc.b)]});}
 out.caps.push({width,depth,kind,seed,shell,bone,low,cells});
}
// Inputs a check rebuilds from (bone matrices, debris sources) keep full precision.
// 5. A posed rig: bone world matrices, box world matrices (checks the table) and faceAlong at each cap joint and side.
function posed(spec,hat,accent,bodyColor,x=1.5){const rig=weaponRig(spec,accent,bodyColor);rig.group.position.set(x,.1,-2);rig.group.rotation.y=.7;rig.spine.rotation.x=.25;rig.leftArm.rotation.z=.6;rig.leftElbow.rotation.x=-.9;rig.rightArm.rotation.x=-.8;rig.rightElbow.rotation.x=-.5;rig.head.rotation.y=.35;rig.leftLeg.rotation.x=.4;rig.rightKnee.rotation.x=.7;
 if(hat)dressRig(rig,{bodyColor,hat,killEffect:'impact-ragdoll'});rig.group.updateMatrixWorld(true);return rig;}
out.rigs=[];
for(const [spec,hat,accent,bodyColor] of [['pistol','crown','#ff3366','#33aa77'],['fire',null,'#70e1d0','#e5e7eb'],['daggers','cap','#ffd166','#26547c']]){
 const rig=posed(spec,hat,accent,bodyColor),bones=BONES.map(b=>[...rig[b].matrixWorld.elements]),boxes=[];
 rig.group.traverse(o=>{if(isBox(o))boxes.push({name:o.name,m:o.matrixWorld.elements.map(r6),c:hex(o.material.color)});});
 const faces=[];
 for(const name of Object.keys(CAP_MESH)){const joint=rig.group.getObjectByName(name);if(!joint)continue;const jp=joint.getWorldPosition(new T.Vector3()),jq=joint.getWorldQuaternion(new T.Quaternion()),bone=new T.Vector3(0,boneSign(name),0).applyQuaternion(jq);
  let parent=joint.parent;while(parent&&!ROOTS.includes(parent.name))parent=parent.parent;
  for(const [side,owner] of [['parent',parent],['child',joint]]){if(!owner)continue;const inverses=[];const walk=n=>{if(n!==owner&&ROOTS.includes(n.name))return;if(SOCKETS[n.name])return;if(n.isMesh&&!n.userData.bodyWound&&!n.userData.detail)inverses.push(n.matrixWorld.clone().invert());for(const c of n.children)walk(c);};walk(owner);
   faces.push({joint:BONES.findIndex(b=>rig[b]===joint),side,owner:BONES.findIndex(b=>rig[b]===owner),count:inverses.length,s:r6(faceAlong(jp,bone,inverses,side))});}}
 out.rigs.push({spec,hat,accent,body:bodyColor,bones,boxes,faces});
}
// 6. voxel-burst from those rigs (the effect factory exactly as DeathEffects calls it).
out.burst=[];const burstAges=[0,.05,.2,.4,.7,1,1.5,2.5,4.4,4.8];
for(const [k,quality,budget,death] of [[0,1,280,{seed:20481,pos:{x:1.5,y:1.05,z:-2},velocity:{x:3,y:1,z:-9},direction:{x:.2,y:.1,z:-1}}],[0,.3,280,{seed:20481,pos:{x:1.5,y:1.05,z:-2},velocity:{x:3,y:1,z:-9},direction:{x:.2,y:.1,z:-1}}],[1,1,280,{seed:7,pos:{x:32.4,y:1.05,z:-2},velocity:{x:0,y:0,z:0},direction:{x:0,y:0,z:0}}],[2,1,60,{seed:99,pos:{x:1.5,y:1.05,z:-2},velocity:{x:-1,y:4,z:2},direction:{x:-1,y:0,z:0}}]]){
 const rigCase=out.rigs[k],rig=posed(rigCase.spec,rigCase.hat,rigCase.accent,rigCase.body,death.pos.x),scene=new T.Scene();
 const effect=KILL_EFFECTS['voxel-burst'](scene,rig.group,{id:'p',color:rigCase.accent,cosmetics:{bodyColor:rigCase.body},yaw:0},{id:'p',at:0,effect:'voxel-burst',source:null,kind:'shot',yaw:0,...death},quality,budget);
 const frames=[];for(const age of burstAges){effect.update(age);frames.push({age,chunks:digest(scene.children.map(c=>[c.position.x,c.position.y,c.position.z,c.rotation.x,c.rotation.y,c.rotation.z,c.scale.x]))});}
 out.burst.push({rig:k,quality,budget,death,bones:BONES.map(b=>[...rig[b].matrixWorld.elements]),count:effect.count,colors:scene.children.map(c=>hex(c.material.color)),frames});effect.dispose();
}
// 7. CrushDebris: the head's meshes of the first posed rig plus a loose object.
{const rig=posed('pistol','crown','#ff3366','#33aa77'),head=rig.head,sources=[];const walk=n=>{if(n!==head&&ROOTS.includes(n.name))return;if(SOCKETS[n.name])return;if(n.isMesh&&!n.userData.bodyWound)sources.push({matrix:n.matrixWorld.clone(),color:n.material.color.clone()});for(const c of n.children)walk(c);};walk(head);
 const loose=new T.Group();loose.position.set(1.4,1.9,-1.9);loose.quaternion.setFromEuler(new T.Euler(.2,.5,-.1));
 for(const [limit,severity] of [[40,1],[16,.4],[3,.8]]){const d=new CrushDebris({sources,loose:[{object:loose}],origin:head.getWorldPosition(new T.Vector3()),direction:new T.Vector3(.3,0,-1),inherited:new T.Vector3(1,.5,-2),kick:2,severity,seed:3141,limit,floor:.2});
  const frames=[];for(const age of [0,.1,.3,.6,1,2,4.5]){d.update(age);const chunks=[];for(let i=0;i<d.mesh.count;i++){d.mesh.getMatrixAt(i,m);m.decompose(pp,qq,ss);const q=canon(qq);chunks.push([pp.x,pp.y,pp.z,q.x,q.y,q.z,q.w,ss.x]);}frames.push({age,chunks:digest(chunks),loose:{p:v3(loose.position),q:q4(loose.quaternion),s:r6(loose.scale.x)}});}
  const colors=[];for(let i=0;i<d.mesh.count;i++){d.mesh.getColorAt(i,cc);colors.push([cc.r,cc.g,cc.b]);}
  out.debris=out.debris||[];out.debris.push({limit,severity,count:d.count,sources:sources.map(s=>({m:[...s.matrix.elements],c:hex(s.color)})),loose:{p:[1.4,1.9,-1.9],q:q4(new T.Quaternion().setFromEuler(new T.Euler(.2,.5,-.1)))},origin:head.getWorldPosition(new T.Vector3()).toArray(),colors:digest(colors),frames});
  loose.position.set(1.4,1.9,-1.9);loose.quaternion.setFromEuler(new T.Euler(.2,.5,-.1));loose.scale.setScalar(1);}}
// 8. bodyPartVelocity for a crushed part (CrushDebris inherits it): speed caps and the momentum kick per part mass.
out.ragdoll={maxSpeed:BODY_RAGDOLL.maxSpeed,maxDetachedKick:BODY_RAGDOLL.maxDetachedKick,detached:[]};
for(const [velocity,part,momentum,direction] of [[{x:1.5,y:.5,z:0},'head',45,{x:1,y:.1,z:0}],[{x:-3,y:2,z:9},'spine',300,{x:0,y:0,z:-2}],[{x:30,y:-4,z:12},'left-elbow',4,{x:.2,y:.9,z:-.1}],
 [{x:0,y:0,z:0},'right-knee',90,{x:0,y:0,z:0}],[{x:2,y:1,z:-1},'left-shoulder',45,{x:-1,y:0,z:1}],[{x:-20,y:10,z:15},'right-hip',500,{x:3,y:-1,z:2}],[{x:0,y:0,z:0},'head',1e6,{x:0,y:1,z:0}]]){
 const v=bodyPartVelocity({kind:'ragdoll',kneesAt:0,releaseAt:0,facing:0,direction:{x:0,y:0,z:1},velocity},{id:'w',at:0,part,kind:'crush',trauma:'blunt',destroyedPart:part,severity:1,radius:.1,momentum,energy:0,speed:0,point:{x:0,y:0,z:0},direction},part,direction);
 out.ragdoll.detached.push({velocity:[velocity.x,velocity.y,velocity.z],part:BONES.findIndex(b=>bodyRig[b].name===part),momentum,direction:[direction.x,direction.y,direction.z],out:[v.x,v.y,v.z]});}
fs.writeFileSync(path.join(root,'Tests/kill-effect-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',body.length,'body boxes,',weapons.reduce((n,w)=>n+w.main.length+w.off.length,0),'weapon boxes,',hats.reduce((n,h)=>n+h.list.length,0),'hat boxes;',out.swarm.length,'swarm runs,',out.spray.length,'spray runs,',out.caps.length,'caps,',out.burst.length,'bursts,',out.debris.length,'debris runs');
