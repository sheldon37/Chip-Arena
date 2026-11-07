// Build-time only: run the pinned original client/death-announcements.ts (the sky death broadcast), client/pve/vent-fall.ts
// (the Ward Zero vent-fall and vent-splat deaths) and record what they draw frame by frame, for Tests/DeathFxChecks.cs to
// replay through Core/NativeBroadcastFx.cs and Core/NativeVentDeath.cs (the PvE revive regeneration is Astra's own port). Also writes Core/NativeBroadcastEdges.Generated.cs: the portrait rig's fit and the edge lines
// (EdgesGeometry(mesh, 24)) of every visible portrait mesh per hat, local to the rig bone they move with.
// Usage: node tools/export_death_fx_fixtures.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/death-announcements.ts':'f2d3d2a021620b9c967819b635a9dd8a99045fe7','src/client/character-rig.ts':'5a020f684a9a2a29238769f3e18a61cddf545e20',
 'src/client/cosmetics.ts':'1c2586c79b5020fbc385aa0e4a32951f8ccf9cc5','src/client/auditor-halo.ts':'13fa46aac967bf126884e6402008f8d9876954cd',
 'src/client/pve/vent-fall.ts':'88eedeee9c23c74d203b43a34412822c87ba45a4','src/client/cinematic-victim.ts':'5a55b24b1cd253707cd2675ec68f8c2dfd5c1a66',
 'src/shared/pve/vent-pits.ts':'e42405839f3797da723f60cee594b5c8105d18d5'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=p=>import(pathToFileURL(path.join(source,'src',p)).href);
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const ann=await load('client/death-announcements.ts'),rigm=await load('client/character-rig.ts'),cos=await load('client/cosmetics.ts');
const p9=v=>Number((+v).toPrecision(9));const v3=o=>[o.x,o.y,o.z].map(p9);const q4=o=>[o.x,o.y,o.z,o.w].map(p9);
// Rig joints in CapturedRig order (the captured rig's 22 bones share the source rig's names).
const JOINTS=['motion-root','pelvis','spine','chest','neck','head','left-clavicle','left-shoulder','left-elbow','left-wrist','right-clavicle','right-shoulder','right-elbow','right-wrist','left-hip','left-knee','left-ankle','left-toe','right-hip','right-knee','right-ankle','right-toe'];
const RIG_KEYS=['body','pelvis','spine','chest','neck','head','leftShoulder','leftArm','leftElbow','leftHand','rightShoulder','rightArm','rightElbow','rightHand','leftLeg','leftKnee','leftFoot','leftToe','rightLeg','rightKnee','rightFoot','rightToe'];
const out={source:pinned};

// ---- A. The sky death broadcast ------------------------------------------------------------------------------------
{
 const HATS=['none','cap','beanie','top','wizard','crown','cone','antenna','halo','cowboy','auditor-halo'];
 const player=(id,name,hat,at,killer)=>({id,name,color:'#70e1d0',hp:0,team:0,pos:{x:0,y:.95,z:0},cosmetics:{hat},death:{id:'d-'+id,at,announcement:{victim:name,killer}}});
 const snap=(time,players)=>({time,phase:'fight',players});
 const A={layout:[0,1,2,3].map(s=>{const f=ann.skyBroadcastFrame(s);return {slot:s,pos:v3(f.position),scale:f.scale};}),frames:[],stages:[],hats:[]};
 // One death: every frame of its timeline.
 {const scene=new T.Scene(),d=new ann.DeathAnnouncements(scene),pl=[player('p1','Victim','none',10,'Killer')];
  for(const age of [0,.01,.3,.6,.9,1.04,1.05,1.06,1.1,1.2,1.31,1.37,1.5,2,2.5,3.24,3.26,4,4.79,4.81,5.1,5.15,5.5,5.59]){
   d.update(snap(10+age,pl),new T.PerspectiveCamera(),true);const st=scene.children.find(o=>o.name==='death-firework-d-p1');
   if(!st){A.frames.push({age,shown:false});continue;}
   const [rocket,trail,stars,holo]=st.children,[group,lines,frame]=holo.children,tp=trail.geometry.getAttribute('position'),sp=stars.geometry.getAttribute('position');
   let edge=null;group.traverse(o=>{if(!edge&&o instanceof T.LineSegments)edge=o;});
   A.frames.push({age,shown:st.visible,rocket:{visible:rocket.visible,pos:v3(rocket.position)},trail:{visible:trail.visible,a:[tp.getX(0),tp.getY(0),tp.getZ(0)].map(p9),b:[tp.getX(1),tp.getY(1),tp.getZ(1)].map(p9),opacity:p9(trail.material.opacity)},
    stars:{visible:stars.visible,opacity:p9(stars.material.opacity),size:p9(stars.material.size),points:Array.from({length:72},(_,i)=>[sp.getX(i),sp.getY(i),sp.getZ(i)].map(p9))},
    holo:{visible:holo.visible,scale:v3(holo.scale),material:p9(group.children[0]?((()=>{let m=null;group.traverse(o=>{if(!m&&o instanceof T.Mesh)m=o.material;});return m.opacity;})()):0),
     head:[p9(group.getObjectByName('head').rotation.x),p9(group.getObjectByName('head').rotation.y)],linesY:p9(lines.position.y),lines:p9(lines.material.opacity),frame:p9(frame.material.opacity),edges:p9(edge.material.opacity)},
    text:d.label?d.label.textContent:null});
  }
  // The static line sets.
  const st=scene.children.find(o=>o.name==='death-firework-d-p1');if(st){const holo=st.children[3],[,lines,frame]=holo.children,lp=lines.geometry.getAttribute('position'),fp=frame.geometry.getAttribute('position');
   A.scan=Array.from({length:lp.count/2},(_,i)=>[lp.getX(i*2),lp.getY(i*2),lp.getZ(i*2),lp.getX(i*2+1),lp.getY(i*2+1),lp.getZ(i*2+1)].map(p9));
   A.frame=Array.from({length:fp.count/2},(_,i)=>[fp.getX(i*2),fp.getY(i*2),fp.getZ(i*2),fp.getX(i*2+1),fp.getY(i*2+1),fp.getZ(i*2+1)].map(p9));
   A.rocket={radius:st.children[0].geometry.parameters.radius,width:st.children[0].geometry.parameters.widthSegments,height:st.children[0].geometry.parameters.heightSegments,color:st.children[0].material.color.getHex(),
    line:st.children[1].material.color.getHex(),star:st.children[2].material.color.getHex(),holo:(()=>{let m=null;holo.children[0].traverse(o=>{if(!m&&o instanceof T.Mesh)m=o.material;});return m.color.getHex();})()};}}
 // Four deaths at once: launches spaced 1.5 s, slots 0,1,2,0; only the newest three show.
 {const scene=new T.Scene(),d=new ann.DeathAnnouncements(scene),pl=[player('a','A','none',20,null),player('b','B','none',20,'A'),player('c','C','none',20,'B'),player('e','E','none',20,'C')];
  for(const t of [20,21.6,23.2,24.6,25.4,26.1]){d.update(snap(t,pl),new T.PerspectiveCamera(),true);A.stages.push({time:t,items:d.track.items.map(a=>({id:a.id,at:p9(a.at),slot:a.slot,text:a.text})),
   shown:scene.children.filter(o=>o.name.startsWith('death-firework')&&o.visible).map(o=>({id:o.name.slice(15),pos:v3(o.position)}))});}}
 // Per hat: the portrait rig's fit, and the edges of every visible mesh, local to the rig bone.
 for(const hat of HATS){
  const scene=new T.Scene(),d=new ann.DeathAnnouncements(scene);d.update(snap(31.1,[player('h','H',hat,30,null)]),new T.PerspectiveCamera(),true);
  const st=scene.children.find(o=>o.name==='death-firework-d-h'),group=st.children[3].children[0];
  // Measure in the rig's own frame: rest pose, unit group transform.
  const rig={group};group.position.set(0,0,0);group.scale.setScalar(1);group.getObjectByName('head').rotation.set(0,0,0);group.updateMatrixWorld(true);
  const fit=st.children[3].children[0];const body=[],hatSegs=[];
  const visible=o=>{for(let a=o;a&&a!==group;a=a.parent)if(!a.visible)return false;return true;};
  group.traverse(o=>{if(!(o instanceof T.LineSegments)||!o.parent||!(o.parent instanceof T.Mesh)||!visible(o))return;
   let j=o.parent,cosmetic=false;while(j&&!JOINTS.includes(j.name)){if(j.name==='head-cosmetic')cosmetic=true;j=j.parent;}const bone=JOINTS.indexOf(j.name);
   const inv=j.matrixWorld.clone().invert(),m=inv.multiply(o.matrixWorld),p=o.geometry.getAttribute('position');
   for(let i=0;i<p.count;i+=2){const a=new T.Vector3(p.getX(i),p.getY(i),p.getZ(i)).applyMatrix4(m),b=new T.Vector3(p.getX(i+1),p.getY(i+1),p.getZ(i+1)).applyMatrix4(m);
    (cosmetic?hatSegs:body).push([bone,...v3(a),...v3(b)]);}});
  A.hats.push({hat,body,segments:hatSegs});
 }
 // The fit, measured before the reset above in a second pass.
 for(const h of A.hats){const scene=new T.Scene(),d=new ann.DeathAnnouncements(scene);d.update(snap(31.1,[player('h','H',h.hat,30,null)]),new T.PerspectiveCamera(),true);
  const g=scene.children.find(o=>o.name==='death-firework-d-h').children[3].children[0];h.fit=p9(g.scale.x);h.offset=v3(g.position);}
 out.broadcast=A;
 // Core/NativeBroadcastEdges.Generated.cs
 const f6=v=>{const s=(+v).toFixed(6).replace(/0+$/,'').replace(/\.$/,'');return (s==='-0'?'0':s)+'f';};
 const segs=list=>list.map(s=>`${s[0]},${s.slice(1).map(f6).join(',')}`).join(',');
 const body=A.hats[0].body;for(const h of A.hats)if(JSON.stringify(h.body)!==JSON.stringify(body))throw Error('Portrait body edges differ by hat: '+h.hat);
 const cs=`// Generated by tools/export_death_fx_fixtures.mjs from the pinned source (05950eaef80ff2483a9c44d898cad98be1a76bbc):
// client/death-announcements.ts DeathAnnouncements.create (createCharacterRig + dressRig, legs, arms and the hip/abdomen parts
// hidden). Per hat: the portrait rig group's fit scale and position (hologram units), and the EdgesGeometry(mesh, 24) lines of
// every visible mesh as (bone, x0, y0, z0, x1, y1, z1) local to the CapturedRig bone the mesh moves with, source (three.js)
// axes; Body is shared by every hat, Hats holds the head-cosmetic meshes. Do not edit.
namespace ChipArena.UnityConversion
{
    public static partial class NativeBroadcastEdges
    {
        public static readonly string[] Hats={${A.hats.map(h=>JSON.stringify(h.hat)).join(',')}};
        public static readonly float[] Fits={${A.hats.map(h=>f6(h.fit)).join(',')}};
        public static readonly float[] Offsets={${A.hats.map(h=>h.offset.map(f6).join(',')).join(',')}};
        public static readonly float[] Body={${segs(body)}};
        public static readonly float[][] HatSegments={
${A.hats.map(h=>`            new float[]{${segs(h.segments)}}`).join(',\n')}};
    }
}
`;
 fs.writeFileSync(path.join(root,'Assets/ChipArena/Core/NativeBroadcastEdges.Generated.cs'),cs);
}

// ---- B. Ward Zero vent-fall and vent-splat --------------------------------------------------------------------------
{
 const vent=await load('client/pve/vent-fall.ts'),pits=await load('shared/pve/vent-pits.ts');
 const B={fx:{...vent.VENT_FX},pit:{drop:pits.VENT_PIT.drop,splatAt:pits.VENT_PIT.splatAt},fall:[],splat:[]};
 // The dead rig's last pose, carried into vent-fall's victim over .1 s.
 const original=rigm.createCharacterRig('#70e1d0');original.leftArm.rotation.set(.5,0,.3);original.rightArm.rotation.set(-.6,.1,-.2);original.spine.rotation.x=.15;original.head.rotation.y=.4;original.body.position.set(0,.03,0);original.group.updateMatrixWorld(true);
 B.carried={joints:RIG_KEYS.map(k=>q4(original[k].quaternion)),body:v3(original.body.position)};
 const pilot={id:'v',color:'#70e1d0',spec:'sword',cosmetics:{hat:'cap'},pos:{x:1.5,y:.95,z:-2}};
 const death=(effect,seed)=>({id:'d',at:0,effect,seed,pos:{x:1.5,y:.95,z:-2},velocity:{x:0,y:-1,z:0},direction:{x:0,y:-1,z:0},yaw:.6,kind:effect,source:null});
 const joints=group=>JOINTS.map(n=>q4(group.getObjectByName(n).quaternion));
 {const scene=new T.Scene(),f=vent.ventFall(scene,original.group,pilot,death('vent-fall',4242),1,96),rootObj=scene.children.find(o=>o.name==='vent-fall'),victim=rootObj.children[0];
  for(const age of [0,.02,.05,.08,.1,.15,.3,.5,.62,.7,1]){f.update(age);B.fall.push({age,pos:v3(rootObj.position),visible:rootObj.visible,yaw:p9(rootObj.rotation.y),group:[victim.rotation.x,victim.rotation.y,victim.rotation.z].map(p9),groupPos:v3(victim.position),joints:joints(victim),body:v3(victim.getObjectByName('motion-root').position)});}
  f.dispose();}
 for(const [seed,quality] of [[4242,1],[0,.3],[987654321,1]]){
  const scene=new T.Scene(),f=vent.ventSplat(scene,original.group,pilot,death('vent-splat',seed),quality,96),rootObj=f.root,victim=f.rig.group,dust=f.extras.slice(0,-1),pop=f.extras[f.extras.length-1];
  const frames=[];
  const rec=(age,keep)=>{if(keep)f.keep(age);else f.update(age);frames.push({age,keep,pos:v3(rootObj.position),scale:v3(rootObj.scale),yaw:p9(rootObj.rotation.y),group:[victim.rotation.x,victim.rotation.y,victim.rotation.z].map(p9),groupPos:v3(victim.position),joints:joints(victim),
   dust:dust.map(m=>({visible:m.visible,pos:v3(m.position),quat:q4(m.quaternion),scale:p9(m.scale.x),chip:m.material.color.getHex()!==0x8d8597,opacity:p9(m.material.opacity)})),
   pop:{visible:pop.visible,pos:v3(pop.position),scale:[p9(pop.scale.x),p9(pop.scale.y)],opacity:p9(pop.material.opacity),depthTest:pop.material.depthTest,renderOrder:pop.renderOrder}});};
  for(const age of [0,.2,.45,.59,.6,.61,.65,.72,.8,.95,1.1,1.3,1.49,1.6,1.84,1.85,1.9,2.4])rec(age,false);for(const age of [1.2,2.5,9])rec(age,true);
  B.splat.push({seed,quality,count:dust.length,keepAge:f.keepAge,frames});f.dispose();
 }
 out.vent=B;
}

fs.writeFileSync(path.join(root,'Tests/death-fx-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',out.broadcast.frames.length,'broadcast frames,',out.broadcast.hats.length,'portrait hats,',out.vent.fall.length,'vent-fall and',out.vent.splat.length*out.vent.splat[0].frames.length,'vent-splat frames');
