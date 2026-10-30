// Build-time only: run the pinned original client/chaos-visuals.ts + summon-visuals.ts (the killed summon's jitter and
// voxel break-up, the minionHit puff), client/ceiling-field.ts (the ceiling-field hit holograms) and client/pve/pickups.ts
// + chip-model.ts (the chip pickup's glow sprite, its canvas texture and the light beam) and record what they draw, for
// Tests/SummonCeilingPickupChecks.cs to replay through Core/NativeSummonBreak.cs, NativeCeilingField.cs and
// NativePickupFx.cs. Canvas drawing is recorded through a stand-in 2D context (node has no canvas): the exact calls the
// source makes. The ceiling fragment shader is asserted verbatim and evaluated here with the same maths.
// Usage: node tools/export_summon_ceiling_pickup.mjs <source checkout at 05950ea with node_modules>
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]);
const pinned={'src/client/chaos-visuals.ts':'bb3764b08d006d0f8e9d7b249ccadc64a7826173','src/client/summon-visuals.ts':'a63333d92f64affe309c821b4033349c493a1e9c',
 'src/client/character-rig.ts':'5a020f684a9a2a29238769f3e18a61cddf545e20','src/client/ceiling-field.ts':'e761d49294759a20c5484743a4218e424743ab21',
 'src/shared/ceiling-field.ts':'311896315b562b7131eb0902ce018090a273f49d','src/client/pve/pickups.ts':'56771b79f58dc9148770eccf4c595ec57781a575',
 'src/client/pve/chip-model.ts':'046b48a01191a6106558671989d1c9efb7d62fba','src/client/pve/hologram.ts':'64933574494776f132bbed556fe8a97338da4014',
 'src/shared/content.ts':'b0613af732ff5a8fb28374212f4631ae41a0ca43'};
for(const [file,blob] of Object.entries(pinned)){const bytes=fs.readFileSync(path.join(source,file));if(createHash('sha1').update(`blob ${bytes.length}\0`).update(bytes).digest('hex')!==blob)throw Error('Source blob mismatch: '+file);}
// A recording 2D canvas: every call and property write the source makes, per canvas.
const canvases=[];
class Recorder{constructor(log){return new Proxy({},{get:(_,k)=>(...a)=>{log.push([k,...a]);if(k==='createRadialGradient')return {addColorStop:(...s)=>log.push(['addColorStop',...s])};},set:(_,k,v)=>{log.push(['set',k,typeof v==='object'?'gradient':v]);return true;}});}}
globalThis.OffscreenCanvas=class{constructor(w,h){this.width=w;this.height=h;this.index=canvases.length;this.calls=[];canvases.push(this);}getContext(){return new Recorder(this.calls);}};
const {register}=await import(pathToFileURL(path.join(source,'node_modules/tsx/dist/esm/api/index.mjs')).href);register();
const load=p=>import(pathToFileURL(path.join(source,'src',p)).href);
const T=await import(pathToFileURL(path.join(source,'node_modules/three/build/three.module.js')).href);
const chaos=await load('client/chaos-visuals.ts'),look=await load('client/summon-visuals.ts'),ceiling=await load('client/ceiling-field.ts'),field=await load('shared/ceiling-field.ts');
const pickups=await load('client/pve/pickups.ts'),chip=await load('client/pve/chip-model.ts');
const p9=v=>Number((+v).toPrecision(9));const v3=o=>[o.x,o.y,o.z].map(p9);const rgb=c=>[c.r,c.g,c.b].map(p9);
const out={source:pinned};

// ---- A. Summons: the minionHit puff, the corpse jitter, the break-up ---------------------------------------------------
{
 const L=look.SUMMON_LOOK,A={look:{breakAt:L.death.breakAt,gone:L.death.gone,zombie:{scale:L.zombie.scale,gib:[...L.zombie.gib]},toy:{scale:L.toy.scale,gib:[...L.toy.gib]}},hits:[],frames:[]};
 const scene=new T.Scene(),cv=new chaos.ChaosVisuals(scene);let bursts=[];
 const fx={burst:(p,color,count,power)=>bursts.push({pos:v3(p),color,count,power}),line:()=>{}};
 const minion=(id,kind,x,y,z,yaw)=>({id,owner:'o',kind,pos:{x,y,z},yaw,hp:10,maxHp:20,speed:3,until:99,lastAttack:-10});
 const live=[minion('z1','zombie',1.25,1.6,2.5,.4),minion('t1','toy',-2,1.1,3,-.7)];
 const step=(time,events,minions)=>{bursts=[];cv['summons']({time,generation:0,events,minions},fx);return bursts;};
 step(10,[],live);
 // Hits: on each kind, a stale one (> .5 s old), one on a target that is not a summon.
 const hitEvents=[{id:1,time:10.1,type:'minionHit',pos:{x:1.3,y:1.7,z:2.4},target:'z1'},{id:2,time:10.1,type:'minionHit',pos:{x:-1.9,y:1.2,z:3.1},target:'t1'},
  {id:3,time:9.55,type:'minionHit',pos:{x:0,y:1,z:0},target:'z1'},{id:4,time:10.1,type:'minionHit',pos:{x:5,y:1,z:5},target:'add-7'}];
 const fired=step(10.1,hitEvents,live);for(const e of hitEvents){const b=fired.find(f=>f.pos.join()===v3(e.pos).join());A.hits.push({target:e.target,kind:e.target==='z1'?'zombie':e.target==='t1'?'toy':'none',age:p9(10.1-e.time),pos:v3(e.pos),burst:b??null});}
 // The killing hit lands with the death in one snapshot: the rig still exists for the hit.
 const kill=step(10.2,[{id:5,time:10.2,type:'minionHit',pos:{x:1.2,y:1.5,z:2.5},target:'z1'},{id:6,time:10.2,type:'minionDeath',pos:{x:1.25,y:1.6,z:2.5},target:'z1',message:'zombie'},{id:7,time:10.2,type:'minionDeath',pos:{x:-2,y:1.1,z:3},target:'t1',message:'toy'}],[]);
 A.killHit=kill[0]??null;
 const dead=cv['dead'],start={z1:{...dead.get('z1')},t1:{...dead.get('t1')}};A.dead={z1:{at:start.z1.at,pos:[start.z1.x,start.z1.y,start.z1.z].map(p9)},t1:{at:start.t1.at,pos:[start.t1.x,start.t1.y,start.t1.z].map(p9)}};
 for(const age of [0,.1,.3,.5,.54,.551,.6,.65,.7,.74,.749,.75,.76,.8,.85,.89,.899,.9,.95,1.2]){
  const b=step(10.2+age,[],[]),frame={age,bursts:b,rigs:{}};
  for(const id of ['z1','t1']){const d=dead.get(id);frame.rigs[id]=d?{present:true,visible:d.rig.group.visible,pos:v3(d.rig.group.position),broke:d.broke}:{present:false};}
  A.frames.push(frame);
 }
 out.summon=A;
}

// ---- B. Ceiling-field hit holograms --------------------------------------------------------------------------------------
{
 const glsl='void main(){vec2 p=(uvField-.5)*7.;float d=length(p),radius=.2+age*4.5;float ring=exp(-pow((d-radius)/.09,2.));vec2 cell=abs(fract(p*2.)-.5);float grid=1.-smoothstep(.015,.06,min(cell.x,cell.y));float reveal=(1.-smoothstep(radius-.1,radius+.3,d))*exp(-d*.65);float fade=pow(max(0.,1.-age/1.05),1.5);float flash=exp(-age*18.)*exp(-d*3.);float scan=.65+.35*sin(p.x*16.+age*19.);gl_FragColor=vec4(tint,(grid*reveal*.36*scan+ring*.85+flash)*fade);}';
 const vert='varying vec2 uvField;void main(){uvField=uv;gl_Position=projectionMatrix*modelViewMatrix*vec4(position,1.);}';
 const scene=new T.Scene(),c=new ceiling.CeilingFieldVisuals(scene),B={glsl,frames:[],alpha:[]};
 const hit=(id,time,x,z,color)=>({id,time,type:'ceiling-hit',pos:{x,y:field.CEILING_FIELD.height,z},...(color===undefined?{}:{color})});
 // A run: hits arrive over time (one without a colour, the absorb colour, a non-ceiling event), then a burst of 26 at once.
 const script=[[0,[hit(1,0,2,-3,0x75ddff)]],[.3,[hit(2,.3,-4,1),{id:3,time:.3,type:'impact',pos:{x:0,y:1,z:0}}]],[.5,[hit(4,.5,0,0,0xb5a3ff)]],[.9,[]],[1.04,[]],[1.06,[]],[1.4,[]],[1.6,[]],
  [2,Array.from({length:26},(_,i)=>hit(10+i,2,i*.5-6,i%3-1,i%2?0x75ddff:0xb5a3ff))],[2.2,[]],[2.25,[hit(40,2.25,7,7)]],[1.95,[]],[3.2,[]],[3.1,[]]];
 for(const [time,events] of script){
  c.update(time);for(const e of events)c.event(e,time);
  const meshes=c.group.children.map(m=>{m.updateMatrixWorld(true);return {pos:v3(m.position),age:p9(m.material.uniforms.age.value),tint:rgb(m.material.uniforms.tint.value)};});
  B.frames.push({time,events:events.map(e=>({id:e.id,type:e.type,pos:v3(e.pos),color:e.color??null})),meshes});
 }
 // One plane in detail: material, geometry, the uv -> world mapping of its corners, the shaders.
 const one=new ceiling.CeilingFieldVisuals(new T.Scene());one.event(hit(1,5,3,-2,0x75ddff),5);const m=one.group.children[0];m.updateMatrixWorld(true);
 if(!m.material.fragmentShader.includes(glsl))throw Error('Ceiling field fragment shader changed');if(m.material.vertexShader!==vert)throw Error('Ceiling field vertex shader changed');
 const pos=m.geometry.getAttribute('position'),uv=m.geometry.getAttribute('uv'),w=new T.Vector3();
 B.plane={width:m.geometry.parameters.width,height:m.geometry.parameters.height,rotation:v3(m.rotation),pos:v3(m.position),event:[3,field.CEILING_FIELD.height,-2],transparent:m.material.transparent,depthWrite:m.material.depthWrite,doubleSide:m.material.side===T.DoubleSide,additive:m.material.blending===T.AdditiveBlending,
  fog:!!m.material.fog,corners:Array.from({length:pos.count},(_,i)=>{w.fromBufferAttribute(pos,i).applyMatrix4(m.matrixWorld);return [uv.getX(i),uv.getY(i),...v3(w)].map(p9);})};
 B.field={height:field.CEILING_FIELD.height,point:v3(field.ceilingContact({x:1,y:20,z:-1},{x:.5,y:6,z:.2},.12).point)};
 // The fragment maths of the asserted GLSL, evaluated at plane points p = (uv - .5) * 7 (JS Math.pow squares a negative base).
 const fract=x=>x-Math.floor(x),smooth=(a,b,x)=>{const t=Math.min(1,Math.max(0,(x-a)/(b-a)));return t*t*(3-2*t);};
 const alpha=(px,py,age)=>{const d=Math.hypot(px,py),radius=.2+age*4.5,ring=Math.exp(-Math.pow((d-radius)/.09,2)),grid=1-smooth(.015,.06,Math.min(Math.abs(fract(px*2)-.5),Math.abs(fract(py*2)-.5))),
  reveal=(1-smooth(radius-.1,radius+.3,d))*Math.exp(-d*.65),fade=Math.pow(Math.max(0,1-age/1.05),1.5),flash=Math.exp(-age*18)*Math.exp(-d*3),scan=.65+.35*Math.sin(px*16+age*19);return (grid*reveal*.36*scan+ring*.85+flash)*fade;};
 for(const age of [0,.02,.1,.25,.4,.6,.8,1,1.04,1.05])for(const [px,py] of [[0,0],[.05,0],[.2,0],[.31,.12],[-.5,.5],[1,-1.2],[1.3,.25],[-2.2,1.1],[3.1,-.4],[0,-3.4],[2.45,2.45],[.75,.75],[1.02,0],[-1.6,-2.9]])B.alpha.push([px,py,age,p9(alpha(px,py,age))]);
 out.ceiling=B;
}

// ---- C. PvE chip pickups: glow, its canvas texture, beam, tier colours --------------------------------------------------
{
 const group=new T.Group(),view=new pickups.PickupsView(group),C={canvases:[],samples:[],beam:null,tiers:[],beamTiers:[...chip.BEAM_TIERS],pickup:{...pickups.PICKUP}};
 const k=(id,tier,owner,born)=>({id,m:0,pos:{x:1.5,y:pickups.PICKUP.restY,z:-2},born,tier,owner});
 for(const [t,tier,owner,born] of [[5,'epic','me',1],[5.3,'epic','other',1],[7.9,'common','me',1],[2.2,'legendary','me',2],[1.8,'mythic','me',2],[6.25,'rainbow','other',1],[9.4,'rainbow','me',1],[3.1,'rare','other',1],[4.6,'mythic','other',4.2]]){
  view.update({pickups:[k('k',tier,owner,born)]},t,'me');const p=view.get('k');p.group.updateMatrixWorld(true);
  const gm=p.glow.material,bm=p.beam.material,wb=new T.Vector3();p.beam.getWorldPosition(wb);
  C.samples.push({t,tier,mine:owner==='me',born,group:v3(p.group.position),visible:p.group.visible,glow:{scale:v3(p.glow.scale),pos:v3(p.glow.position),opacity:p9(gm.opacity),color:rgb(gm.color),canvas:gm.map.image?gm.map.image.index:-1},
   beam:{visible:p.beam.visible,opacity:p9(bm.opacity),color:rgb(bm.color),world:v3(wb)}});
  view.clear();
 }
 const any=new pickups.PickupsView(new T.Group());any.update({pickups:[k('b','epic','me',0)]},3,'me');const p=any.get('b'),g=p.beam.geometry,gp=g.getAttribute('position');
 C.beam={parameters:{...g.parameters},positions:Array.from({length:gp.count},(_,i)=>[gp.getX(i),gp.getY(i),gp.getZ(i)].map(p9)),index:Array.from(g.index.array),side:p.beam.material.side===T.DoubleSide,additive:p.beam.material.blending===T.AdditiveBlending,
  depthWrite:p.beam.material.depthWrite,transparent:p.beam.material.transparent,toneMapped:p.beam.material.toneMapped,fog:p.beam.material.fog};
 C.glowMaterial={additive:p.glow.material.blending===T.AdditiveBlending,depthWrite:p.glow.material.depthWrite,transparent:p.glow.material.transparent,toneMapped:p.glow.material.toneMapped,fog:p.glow.material.fog,center:[p.glow.center.x,p.glow.center.y],rotation:p.glow.material.rotation};
 for(const c of canvases)C.canvases.push({index:c.index,width:c.width,height:c.height,calls:c.calls});
 for(const tier of ['common','uncommon','rare','epic','legendary','mythic','rainbow'])for(const t of [0,1.3,2.9,4.7])C.tiers.push({tier,t,linear:rgb(chip.tierColor(tier,t))});
 out.pickup=C;
}

fs.writeFileSync(path.join(root,'Tests/summon-ceiling-pickup-parity.json'),JSON.stringify(out)+'\n');
console.log('Exported',out.summon.frames.length,'summon frames,',out.ceiling.frames.length,'ceiling frames and',out.ceiling.alpha.length,'shader samples,',out.pickup.samples.length,'pickup samples,',out.pickup.canvases.length,'glow canvases');
