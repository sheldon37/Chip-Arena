// Impact cue parity fixtures (presentation only). Runs the pinned original modules themselves, never the native port:
// client/event-vfx.ts EventVfx.event (its BurstPool.spawn, beam and ChipBlasts impact/line/paddle calls recorded),
// client/chaos-visuals.ts summons' minionShot (its renderer summonFx recorded) and client/standard-melee-effects.ts (the
// hit flash sparks and the slam dust and stones read back from its scene graph at fixed ages). Each event scenario also
// states the Unity event that stands for the source events (kind, Detail, From, the colour the presentation resolves,
// whether the target is a pilot), so Tests/ImpactCueChecks.cs can run Core/NativeImpactCues on it and compare.
// Owner cuts in the Unity presentation (2026-10-11 effects overhaul, review 2026-10-11), not applied here: the melee hit
// flash sparks are drawn as HDR streaks along each spark box's long axis and the slam dust points as lit, eroding smoke
// puffs (Runtime/NativeMeleeFlash.cs through NativeVfxDraw), instead of the source's thin boxes and hard-edged squares. The
// place, length, count, colour and fade of every spark and dust point are the source's, which these fixtures pin.
// Usage: node tools/export_impact_cues.mjs <pinned source> <temporary output>  (link a node_modules containing three
// r180 into the temporary output). Writes Tests/impact-cue-parity.json.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath,pathToFileURL} from 'node:url';import {stripTypeScriptTypes} from 'node:module';import {createHash} from 'node:crypto';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..'),source=path.resolve(process.argv[2]),temp=path.resolve(process.argv[3]);
const inventory=JSON.parse(fs.readFileSync(path.join(root,'tools/conversion-inventory.json')));
fs.mkdirSync(temp,{recursive:true});fs.writeFileSync(path.join(temp,'package.json'),'{"type":"module"}');
for(const f of inventory.files.filter(f=>f.path.startsWith('src/')&&f.path.endsWith('.ts'))){const input=path.join(source,f.path);if(!fs.existsSync(input))continue;const b=fs.readFileSync(input);if(createHash('sha1').update(`blob ${b.length}\0`).update(b).digest('hex')!==f.gitBlob)throw Error('Source mismatch '+f.path);const out=path.join(temp,f.path.replace(/\.ts$/,'.js'));fs.mkdirSync(path.dirname(out),{recursive:true});let code=b.toString().replace(/import\s*\{([^}]+)\}\s*from\s*(['"])([^'"]+)\2/g,(whole,names,quote,module)=>{if(!module.startsWith('.'))return whole;let target=path.resolve(path.dirname(input),module.replace(/\.js$/,'.ts'));if(!fs.existsSync(target))return whole;let text=fs.readFileSync(target,'utf8'),kept=names.split(',').filter(n=>!new RegExp('(?:export\\s+)?(?:interface|type)\\s+'+n.trim().split(/\s+as\s+/)[0]+'\\b').test(text));return kept.length?'import {'+kept.join(',')+'} from '+quote+module+quote:'import '+quote+module+quote;});fs.writeFileSync(out,stripTypeScriptTypes(code,{mode:'transform'}));}
const load=async p=>import(pathToFileURL(path.join(temp,'src',p+'.js')));
const T=await import(pathToFileURL(path.join(temp,'node_modules/three/build/three.module.js')));
const {EventVfx}=await load('client/event-vfx'),{ChaosVisuals}=await load('client/chaos-visuals'),{StandardMeleeEffects}=await load('client/standard-melee-effects');
const {MATERIALS}=await load('shared/config'),{WEAPONS}=await load('shared/content');
const P=(x,y,z)=>({x,y,z}),A=v=>[v.x,v.y,v.z],r6=x=>Math.round(x*1e6)/1e6;

// ---- EventVfx.event: every call it makes for one source event, as NativeImpactCommand fields ----
const record=[];
const vfxFor=trails=>{const v=new EventVfx(new T.Scene(),()=>({quality:1,compat:false,trails}));
 v.bursts.spawn=(pos,color,count,power)=>record.push({op:'Burst',a:A(pos),color,count,power});
 v.beam=(a,b,color)=>record.push({op:'Beam',a:A(a),b:A(b),color});
 v.chipBlasts.impact=(pos,radius,color,o)=>record.push({op:'Impact',a:A(pos),radius,color,ground:!!o.ground});
 v.chipBlasts.line=(a,b,c0,c1,q,jitter=.25,life=.14,sweep=0)=>record.push({op:'Line',a:A(a),b:A(b),color:c0,color1:c1,jitter,life,sweep});
 v.chipBlasts.paddle=(at,dir,color)=>record.push({op:'Paddle',a:A(at),b:A(dir),color});
 return v;};
const vfx={on:vfxFor(true),off:vfxFor(false)};
const at=P(1.5,1.2,-2),from=P(-1,1.6,2.5),foe=P(4,1,3);
const state={time:5,players:[{id:'me',pos:P(0,1,0),spec:'pistol'},{id:'foe',pos:foe,spec:'pistol'}],events:[]};
// Unity side: kind, Detail, From (null: Emit's default, the position), Color (the colour the presentation resolves where
// Unity's event has none), Amount, Radius, TargetPilot, Trails, HasTarget/Target (a rally's target pilot body).
const U=(kind,o={})=>({kind,detail:o.detail??null,from:o.from?A(o.from):null,color:o.color??0,amount:o.amount??0,radius:o.radius??0,targetPilot:!!o.targetPilot,trails:o.trails??true,target:o.target?A(o.target):null});
const S=(type,o={})=>({id:1,time:5,type,pos:at,source:'foe',...o});
const hitColor=kind=>kind==='debris'?0xffbe71:kind==='burn'?0xff822f:kind==='poison'?0x9de34d:0xffffff;// simulation.ts:241
const cases=[
 // simulation.ts:241 a pilot `hit` (above .05) carries its kind's colour; :473 debris on a pilot also emits debrisHit.
 ['hit direct',[S('hit',{message:'direct',target:'me',amount:5,color:hitColor('direct')})],U('Hit',{detail:'direct',amount:5,targetPilot:true})],
 ['headshot',[S('hit',{message:'direct',target:'me',amount:9,headshot:true,color:hitColor('direct')})],U('Headshot',{detail:'direct',amount:9,targetPilot:true})],
 ['hit secondary',[S('hit',{message:'secondary',target:'me',amount:3,color:hitColor('secondary')})],U('Hit',{detail:'secondary',amount:3,targetPilot:true})],
 ['hit melee',[S('hit',{message:'melee',target:'foe',amount:22,color:hitColor('melee')})],U('Hit',{detail:'melee',amount:22,targetPilot:true})],
 ['hit stage saw',[S('hit',{message:'stage-saw',target:'me',amount:22,color:hitColor('stage-saw')})],U('Hit',{detail:'stage-saw',amount:22,targetPilot:true})],
 ['hit at .05 is no hit',[],U('Hit',{detail:'direct',amount:.05,targetPilot:true})],
 ['burn tick',[S('hit',{message:'burn',target:'me',amount:2,color:hitColor('burn')})],U('BurnTick',{detail:'burn',amount:2,targetPilot:true})],
 ['poison tick',[S('hit',{message:'poison',target:'me',amount:2,color:hitColor('poison')})],U('PoisonTick',{detail:'poison',amount:2,targetPilot:true})],
 ['debris hit',[S('hit',{message:'debris',target:'me',amount:6,color:hitColor('debris')}),S('debrisHit',{target:'me',amount:6})],U('Hit',{detail:'debris',amount:6,targetPilot:true})],
 ['debris graze',[S('debrisHit',{target:'me',amount:.03})],U('Hit',{detail:'debris',amount:.03,targetPilot:true})],
 ['hit on an add (minionHit)',[S('minionHit',{target:'add-1',amount:5})],U('Hit',{detail:'direct',amount:5})],
 ['blockHit',[S('blockHit',{source:'me',target:'foe',message:'projectile',color:0x91f7eb})],U('Block')],
 ...['wood','stone','metal','foundation'].map(m=>['break '+m,[S('break',{material:m,radius:2,color:MATERIALS[m].color})],U('Break',{detail:m,color:MATERIALS[m].color})]),
 ['break by the Siren seeker',[S('break',{source:'siren',material:'stone'})],U('Break',{detail:'stone'})],
 ['collapse',[S('collapse',{radius:3})],U('Collapse',{detail:'tower-1'})],
 ['impact with from',[S('impact',{from,color:WEAPONS.rifle.color})],U('Impact',{detail:null,from,color:WEAPONS.rifle.color})],
 ['impact with from, trails off',[S('impact',{from,color:WEAPONS.rifle.color})],U('Impact',{from,color:WEAPONS.rifle.color,trails:false}),'off'],
 ['impact without from',[S('impact',{color:WEAPONS.pistol.color})],U('Impact',{color:WEAPONS.pistol.color})],
 ['impact without colour',[S('impact',{})],U('Impact')],
 ['critical',[S('critical',{color:0xffd66c})],U('Critical',{amount:10})],
 ['catch',[S('catch',{})],U('Catch')],
 ['split',[S('split',{color:0x96ffb7})],U('Split',{amount:8})],
 ['ricochet',[S('ricochet',{color:0xffcc49})],U('Ricochet',{amount:8})],
 ['pong bounce',[S('ricochet',{mods:{pong:1},color:0xff6475})],U('Pong',{amount:8})],
 ['pong rally',[S('ricochet',{mods:{pong:1},color:0xff6475,message:'rally',from,target:'foe'})],U('Pong',{detail:'rally',from,target:foe,amount:8})],
 ['pong rally, target gone',[S('ricochet',{mods:{pong:1},color:0xff6475,message:'rally',from,target:'nobody'})],U('Pong',{detail:'rally',from,amount:8})],
 ['summon',[S('summon',{message:'zombie'})],U('Summon',{detail:'zombie'})],
 ['stage saw',[S('stage-saw-hit',{target:'me',amount:22,color:0xffbb55,message:'SAW'})],U('HazardHit',{detail:'saw',amount:22,targetPilot:true})],
 ['stage lava',[S('stage-lava-hit',{target:'me',amount:8,color:0xff6933,message:'LAVA'})],U('HazardHit',{detail:'lava',amount:8,targetPilot:true})],
 ['stage spikes hit',[S('stage-spikes-hit',{target:'me',amount:25,color:0xffbb55,message:'SPIKES'})],U('HazardHit',{detail:'spikes',amount:25,targetPilot:true})],
 ['leet-freeze',[S('leet-freeze',{color:0x55ff88,radius:28})],U('Leet',{detail:'leet-freeze',radius:28})],
 ['leet-nuke',[S('leet-nuke',{color:0x55ff88,radius:11})],U('Explosion',{detail:'leet-nuke',radius:11})],
 ['leet-lock',[S('leet-lock',{from,color:0x55ff88})],U('Arc',{detail:'leet-lock',from,radius:.25})],
 ['leet bolt impact (no source event)',[],U('Leet',{detail:'bolt',amount:24})],
 ['ball lightning arc',[S('arc',{from,color:WEAPONS.lightning.color})],U('Arc',{detail:'ball-lightning',from,radius:.15})],
 ['arc without from',[S('arc',{})],U('Arc')],
 ['static arc (a blast)',[S('arc',{radius:3})],U('Arc',{radius:3})],
 ['explosion',[S('explosion',{message:'frag',radius:3})],U('Explosion',{detail:'frag',radius:3})],
 ['heal',[S('heal',{target:'me',amount:5})],U('Heal',{amount:5,targetPilot:true})],
];
const events=[];
for(const [name,list,unity,trails] of cases){record.length=0;for(const e of list)vfx[trails??'on'].event(e,state,'me');events.push({name,position:A(at),unity,calls:record.map(c=>({...c}))});}

// ---- Effect records with no Unity event (NativeImpactCues.Record) and derived events ----
const records=[];
const recordCase=(name,element,radius,hop,list,run)=>{record.length=0;run?run():list.forEach(e=>vfx.on.event(e,state,'me'));records.push({name,element,radius,hop,position:A(at),from:A(from),calls:record.map(c=>({...c}))});};
recordCase('leet-control (no from)',2,1,false,[S('leet-control',{target:'me',color:0x55ff88})]);
recordCase('leet-slender-hop',2,1,true,[S('leet-slender-hop',{from,target:'me',amount:1,color:0x55ff88})]);
recordCase('blink (teleport blast only)',3,1,false,[]);
recordCase('ball lightning record (its Arc event draws)',3,.15,false,[]);
recordCase('leet-lock record (its Arc event draws)',2,.25,false,[]);
{// chaos-visuals.ts summons(): a toy's minionShot with renderer.ts summonFx (burst = BurstPool.spawn, line = chipBlasts.line(..,.02,.09)).
 const cv=new ChaosVisuals(new T.Scene()),fx={burst:(p,c,n,pw)=>record.push({op:'Burst',a:A(p),color:c,count:n,power:pw}),line:(a,b,c0,c1)=>record.push({op:'Line',a:A(a),b:A(b),color:c0,color1:c1,jitter:.02,life:.09,sweep:0})};
 recordCase('toy shot (minionShot)',0,.1,false,null,()=>cv.summons({time:5,generation:0,events:[{id:7,time:5,type:'minionShot',pos:at,from,source:'me',target:'toy-1'}],minions:[]},fx));}
recordCase('stage-spikes-rise',-1,0,false,[S('stage-spikes-rise',{})]);
recordCase('rocket dud impact',-2,.3,false,[S('impact',{color:0x9a9a9a,message:'rocket-dud'})]);

// ---- standard-melee-effects.ts: flash sparks and slam dust/stones read back from the scene graph ----
const flashes=[];const flashAges=[0,.03,.07,.12,.2,.27];
for(const spec of ['greatsword','shield','sword','daggers','pistol'])for(const quality of [1,.3]){
 const scene=new T.Scene(),m=new StandardMeleeEffects(scene),t0=12.345,players=[{id:'a',spec},{id:'b',spec:'pistol'}];
 m.update({time:t0,projectiles:[],players},quality);m.event({id:3,time:t0,type:'hit',pos:at,source:'a',target:'b',amount:20,message:'melee'},quality);
 const frames=[];for(const age of flashAges){m.update({time:t0+age,projectiles:[],players},quality);const g=scene.children.find(o=>o.name==='melee-hit-flash');if(!g){frames.push({age,sparks:[]});continue;}
  const sparks=g.children.filter(o=>o.geometry?.type==='BoxGeometry').map(o=>({p:A(o.position).map(r6),q:[o.quaternion.x,o.quaternion.y,o.quaternion.z,o.quaternion.w].map(r6),sz:r6(o.scale.z),alpha:r6(o.material.opacity),color:o.material.color.getHex()}));
  frames.push({age,root:A(g.position),sparks});}
 const burnScene=new T.Scene(),burn=new StandardMeleeEffects(burnScene);burn.update({time:t0,projectiles:[],players},quality);burn.event({id:4,time:t0,type:'hit',pos:at,source:'a',target:'b',amount:3,message:'burn'},quality);
 flashes.push({spec,quality,at:t0,frames,burnFlashes:burnScene.children.filter(o=>o.name==='melee-hit-flash').length});}
const slams=[];const slamAges=[0,.1,.25,.5,.8,1.05];
for(const quality of [1,.3]){
 const scene=new T.Scene(),m=new StandardMeleeEffects(scene),t0=20;m.update({time:t0,projectiles:[],players:[]},quality);
 m.event({id:5,time:t0,type:'melee-slam',pos:at,source:'a',radius:3.7,color:0xe8d4b2},quality);
 const frames=[];for(const age of slamAges){m.update({time:t0+age,projectiles:[],players:[]},quality);const g=scene.children.find(o=>o.name==='wolf-landing-impact');
  const dust=g.children.find(o=>o.isPoints),stones=g.children.filter(o=>o.isMesh&&o.geometry.type==='IcosahedronGeometry'),pos=dust.geometry.getAttribute('position');const points=[];
  for(let i=0;i<pos.count;i++){const v=new T.Vector3(pos.getX(i),pos.getY(i),pos.getZ(i)).multiplyScalar(dust.scale.x).add(dust.position);points.push(A(v).map(r6));}
  frames.push({age,root:A(g.position),dustColor:dust.material.color.getHex(),dustSize:dust.material.size,dustOpacity:r6(dust.material.opacity),points,
   stones:stones.map(o=>({p:A(o.position).map(r6),q:[o.quaternion.x,o.quaternion.y,o.quaternion.z,o.quaternion.w].map(r6),s:r6(o.scale.x),color:o.material.color.getHex(),radius:o.geometry.parameters.radius,detail:o.geometry.parameters.detail}))});}
 slams.push({quality,frames});}
fs.writeFileSync(path.join(root,'Tests/impact-cue-parity.json'),JSON.stringify({source:'05950ea',lightning:WEAPONS.lightning.color,materials:Object.fromEntries(Object.entries(MATERIALS).map(([k,v])=>[k,v.color])),events,records,flashes,slams}));
console.log('event scenarios',events.length,'records',records.length,'flash runs',flashes.length,'slam runs',slams.length);
