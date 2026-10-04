/**
 * Development-only visual lab. Renders the real rig, cosmetics, weapons and animation
 * code as labelled frame strips so presentation changes can be compared before/after.
 * Not part of the shipped client (Vite only builds the root index.html) and never imported from src.
 *
 * Query parameters (all times are seconds on the lab clock; frame labels show t-start):
 *  kind=motion|intro|lobby   (default motion)
 *  spec=<weapon spec>        (default pistol)
 *  mode=<animation preview>  idle|walk|run|attack|windup|release|left-strike|right-strike|reload|block|jump|
 *                            ability-q|ability-e (Q/E phrase from each cycle start; the lab marks the kit live, so
 *                            phrases preview before G/M flip `live`)|ads (aiming; first-person view uses the ADS FOV)
 *  patch=<JSON>              fields shallow-merged into the preview player every step (from patchFrom on)
 *  patchFrom=<s>             the static patch applies only while t>=patchFrom (default: always)
 *  patchAt=<JSON array>      keyed timeline [{"t":0.3,"patch":{...}},...]: each key's fields are
 *                            shallow-merged (after patch, in ascending t) on every step with t>=key.t
 *  rel=<a,b.c,...>           numeric fields (top-level or dotted paths such as hitReaction.at,
 *                            knockdown.landedAt) in patch and in every patchAt key are offset by start
 *  frames, start, span       sample times start + i*span/(frames-1) seconds
 *  warm=<s>                  warm-up simulated before start (default 1)
 *  dt=<s>                    simulation step (default 1/60); try 1/30 or 1/120 for frame-rate studies
 *  exact=1                   step exactly onto every sample time (sub-frame sampling) instead of
 *                            capturing the nearest step
 *  views=side,front,three,three-right,back,top,first   camera rows (motion/lobby). 'first' renders the
 *                            first-person view model as the renderer does (first-person-view.ts, the same
 *                            hidden head/leg/torso meshes, camera at the eye) with the third-person rig hidden
 *  style=<intro style>       intro kind; lobbyPose=<pose> lobby kind
 *  cell=<px>, dist=<m>, height=<m>, focus=<m>   cell height, camera distance/height, look-at height
 *  aspect=<w/h>              cell aspect (default 1); intros also pass it to the intro camera (e.g. 1.778)
 *  light=lab|arena|portrait  lab (default): the original lab lights, no tone mapping.
 *                            arena: match lights (arenaLighting) with the renderer's ACES tone mapping,
 *                            exposure .92, sRGB output and soft shadows. portrait: the menu-portrait lights
 *                            with ACES and exposure 1.1. For kind=intro any non-lab value only switches on
 *                            the renderer's ACES/.92/sRGB output (the intro stage keeps its own lights).
 *  root=travel               integrate the player's velocity (skipped while planted: committed or hitstun),
 *                            roll displacement (rollVelocity) and melee attack travel (attackTravel, with the
 *                            same synthesized commitment as primary-melee-motion) into the rig root, so foot
 *                            sliding shows against the grid. Vertical motion is not integrated.
 *  follow=0|1                cameras follow the rig root (default 1); 0 keeps them on the start position
 *  crosshair=1               draw the screen-centre crosshair on 'first' cells (ADS sight alignment checks)
 *  blockHit=<s,s,...>        record a 'blockHit' on the lab player at these times (block-impacts.ts)
 *  mods=0                    skip weaponModifiers/bodyModifiers (muzzle flash, spell-charge orb, chip parts),
 *                            which otherwise run every step like the renderer
 *  cosmetics=<JSON>, color=<css>   rig dressing
 *  legacy=1                  reproduce the pre-upgrade lab for old queries: no time-base fix, no modifiers
 *
 * Time-base fix (default): the preview player's hitStunUntil is forced to -1e9 before patches, so warm-up
 * and frame 0 (t<=0) never enter the hitstun flinch branch; the lobby warm-up clamps its clock at 0.
 * A patch or keyed patch that sets hitStunUntil still wins.
 */
import * as T from 'three';
import {Simulation,initPhysics,type PlayerView,type Snapshot} from '../../src/shared/simulation.js';
import {v,forward} from '../../src/shared/config.js';
import {VIEW,cameraPosition} from '../../src/shared/view.js';
import {attackCommitment,attackTravel,committed,rollVelocity,standardWeapon} from '../../src/shared/souls-combat.js';
import {createCharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter,animationPreviewPlayer,attackPose} from '../../src/client/character-animation.js';
import {dressRig} from '../../src/client/cosmetics.js';
import {buildWeapon,weaponModifiers,bodyModifiers} from '../../src/client/weapon-visuals.js';
import {PlayerIntros} from '../../src/client/player-intros.js';
import {introDuration} from '../../src/shared/intros.js';
import {animateLobbyPose,lobbyPlayer} from '../../src/client/lobby-poses.js';
import {poseFirstPersonView} from '../../src/client/first-person-view.js';
import {markFirstPersonRig} from '../../src/client/pose-flags.js';
import {recordBlockHit,clearBlockImpacts} from '../../src/client/block-impacts.js';
import {arenaLighting} from '../../src/client/arena-lighting.js';
import {sampleClip,CHANNEL_COUNT,type MocapClip} from '../../src/client/mocap/mocap-clip.js';
import {applyMocapPose} from '../../src/client/mocap/mocap-pose.js';
import {MOCAP} from '../../src/client/mocap/mocap-layer.js';
import {kitFor,adsProfile} from '../../src/shared/abilities.js';

const q=new URLSearchParams(location.search);
// mocapOff=1: the procedural layers only (before/after strips of the mocap sets, src/client/mocap).
if(q.get('mocapOff')==='1')MOCAP.enabled=false;
// mocap=<clip>: pose the raw retargeted clip (clip time = lab time, no ground pass) from the uncommitted preview table that
// `node scripts/mocap/convert-greatsword.mjs <pack> --preview tools/visual-lab/mocap-preview.generated.ts` writes.
const rawClip:MocapClip|undefined=q.has('mocap')?(await import(/* @vite-ignore */ './mocap-preview.generated.ts')).PREVIEW_CLIPS[q.get('mocap')!]:undefined,rawRow=new Float32Array(CHANNEL_COUNT);
const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d;
const legacy=q.get('legacy')==='1';
const kind=q.get('kind')||'motion',spec=q.get('spec')||'pistol',mode=q.get('mode')||'idle';
if(mode.startsWith('ability')){const k=kitFor(spec as never);if(k)k.q.live=k.e.live=true;}
const frames=Math.max(1,num('frames',8)),cell=num('cell',220),dist=num('dist',4.3),camHeight=num('height',1.0);
const aspect=Math.max(.2,num('aspect',1)),cellW=Math.round(cell*aspect);
const views=(q.get('views')||'side,front,three').split(',').filter(Boolean);
const patch=q.has('patch')?JSON.parse(q.get('patch')!):{};
const patchAt:{t:number;patch:Record<string,unknown>}[]=q.has('patchAt')?JSON.parse(q.get('patchAt')!):[];
const patchFrom=num('patchFrom',-Infinity);
const rel=(q.get('rel')||'').split(',').filter(Boolean);
const light=q.get('light')||'lab',travel=q.get('root')==='travel',follow=q.get('follow')!=='0',exact=q.get('exact')==='1';
const blockHits=(q.get('blockHit')||'').split(',').filter(Boolean).map(Number);
const mods=!legacy&&q.get('mods')!=='0';
const status=document.getElementById('status')!;
const sheet=document.getElementById('sheet') as HTMLCanvasElement;

function stage(){
 const scene=new T.Scene();scene.background=new T.Color(0x1a2029);
 if(light==='arena')arenaLighting(scene,false);
 else if(light==='portrait'){scene.add(new T.HemisphereLight(0xe3eeff,0x474054,2.5));const key=new T.DirectionalLight(0xffedda,3);key.position.set(3,5,4);scene.add(key);const rim=new T.DirectionalLight(0x849fe3,1.4);rim.position.set(-3,2,-2);scene.add(rim);}
 else{
  scene.add(new T.HemisphereLight(0xcfe3ff,0x2a2530,1.1));
  const key=new T.DirectionalLight(0xffffff,1.6);key.position.set(3,6,4);scene.add(key);
 }
 const floor=new T.Mesh(new T.PlaneGeometry(40,40),new T.MeshLambertMaterial({color:0x3a4450}));floor.rotation.x=-Math.PI/2;scene.add(floor);
 // Root travel: a .5 m checker floor (visible at grazing angles, unlike 1px grid lines) and grid are re-centred
 // under the rig in whole 1 m steps, so both stay world-fixed; a uniform grid colour hides the moving centre line.
 if(travel){const c=document.createElement('canvas');c.width=c.height=64;const g=c.getContext('2d')!;g.fillStyle='#3a4450';g.fillRect(0,0,64,64);g.fillStyle='#2b333d';g.fillRect(0,0,32,32);g.fillRect(32,32,32,32);
  const map=new T.CanvasTexture(c);map.wrapS=map.wrapT=T.RepeatWrapping;map.repeat.set(40,40);map.magFilter=T.NearestFilter;map.anisotropy=8;map.colorSpace=T.SRGBColorSpace;(floor.material as T.MeshLambertMaterial).color.set(0xffffff);(floor.material as T.MeshLambertMaterial).map=map;}
 const grid=new T.GridHelper(20,40,travel?0x4a5561:0x5d6b78,0x4a5561);grid.position.y=.002;scene.add(grid);
 return {scene,floor,grid};
}
function aim(camera:T.PerspectiveCamera,view:string,target:T.Vector3){
 const o={side:[dist,camHeight,0],front:[0,camHeight,dist],three:[dist*.72,camHeight+.35,dist*.72],'three-right':[-dist*.72,camHeight+.35,dist*.72],back:[0,camHeight,-dist],top:[0,dist+1.2,.01]}[view]??[dist,camHeight,0];
 camera.position.set(target.x+o[0],o[1],target.z+o[2]);camera.lookAt(target);
}
function label(ctx:CanvasRenderingContext2D,text:string,x:number,y:number){ctx.fillStyle='rgba(0,0,0,.55)';ctx.fillRect(x,y,Math.min(cellW,ctx.measureText(text).width+10),16);ctx.fillStyle='#e8eef2';ctx.fillText(text,x+5,y+12);}
/** Deep-copies a patch and offsets the numeric fields at the rel paths (top-level or dotted) by start. */
function withRel(source:Record<string,unknown>,start:number){
 const out=JSON.parse(JSON.stringify(source)) as Record<string,unknown>;
 for(const path of rel){
  const keys=path.split('.');let node:Record<string,unknown>|undefined=out;
  for(let i=0;i<keys.length-1&&node;i++){const child:unknown=node[keys[i]];node=child&&typeof child==='object'?child as Record<string,unknown>:undefined;}
  const last=keys[keys.length-1];if(node&&typeof node[last]==='number')node[last]=(node[last] as number)+start;
 }
 return out;
}

async function main(){
 await initPhysics();
 const sim=new Simulation({sandbox:true});
 const id='lab';sim.addPlayer(id,'Lab','ranged',null,q.get('color')||'#ff6b35',{});
 const player=sim.players.get(id)!;player.spec=spec as never;sim.resetRound();sim.phase='fight';
 for(const prop of [...sim.props.values()])sim.removeProp(prop.id);
 sim.addProp({id:'floor',pos:v(0,-.5,0),size:v(64,1,56),material:'foundation'});
 player.body.setTranslation(v(0,1.2,0),true);
 for(let i=0;i<90;i++)sim.step(1/60);
 const snap=sim.snapshot();const base=snap.players.find(p=>p.id===id)!;base.yaw=0;
 if(q.has('cosmetics'))base.cosmetics={...base.cosmetics,...JSON.parse(q.get('cosmetics')!)};
 const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cellW,cell);gl.shadowMap.enabled=true;
 if(light!=='lab'){gl.outputColorSpace=T.SRGBColorSpace;gl.toneMapping=T.ACESFilmicToneMapping;gl.toneMappingExposure=light==='portrait'&&kind!=='intro'?1.1:.92;gl.shadowMap.type=T.PCFSoftShadowMap;}
 const rows=kind==='intro'?1:views.length,cols=frames;
 sheet.width=cols*cellW;sheet.height=rows*cell+22;const ctx=sheet.getContext('2d')!;ctx.font='11px ui-monospace,monospace';ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
 const flags=(travel?' · root=travel':'')+(blockHits.length?' · blockHit '+blockHits.join(','):'')+(q.has('patchAt')?' · patchAt '+q.get('patchAt'):'')+(q.has('light')?' · light='+light:'')+(q.has('dt')?' · dt='+q.get('dt'):'');
 const title=kind==='intro'?`intro ${q.get('style')} · ${spec}${flags}`:`${kind} · ${spec} · ${kind==='lobby'?q.get('lobbyPose')||'weapon':mode}${q.has('patch')?' · '+q.get('patch'):''}${flags}`;
 ctx.fillStyle='#ffb27a';ctx.fillText(title,6,15);
 const camera=new T.PerspectiveCamera(38,cellW/cell,.05,200);
 if(kind==='intro'){
  const style=q.get('style')||'neon-drop',duration=introDuration(style),start=num('start',0),span=num('span',duration);
  base.cosmetics={...base.cosmetics,intro:style as never,overrideWeaponIntro:true} as PlayerView['cosmetics'];base.developer=true as never;
  const intros=new PlayerIntros();
  for(let i=0;i<cols;i++){const t=start+(cols===1?0:span*i/(cols-1));
   const s:Snapshot={...snap,players:[{...base}],phase:'intro',intro:{started:0,players:[id]},time:t} as Snapshot;
   intros.update(s,cellW/cell,1,false);gl.render(intros.scene,intros.camera);ctx.drawImage(gl.domElement,i*cellW,22);label(ctx,`${t.toFixed(2)}s`,i*cellW+2,24);}
  status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;return;
 }
 const {scene,floor,grid}=stage();
 const rig=createCharacterRig(base.color,base.cosmetics?.bodyColor,true);dressRig(rig,base.cosmetics);buildWeapon(rig.weapon,spec);
 const paired=['daggers','ansatsuken','emperor'].includes(spec);
 if(paired){rig.offWeapon.visible=true;buildWeapon(rig.offWeapon,spec,true);}else rig.offWeapon.visible=false;
 rig.group.traverse(o=>{if(o instanceof T.Mesh){o.castShadow=true;o.receiveShadow=true;}});scene.add(rig.group);
 // First-person view model, built and hidden exactly like ArenaRenderer.firstRig (renderer constructor).
 let fp:ReturnType<typeof createCharacterRig>|undefined,viewRoot:T.Group|undefined,fpCamera:T.PerspectiveCamera|undefined;
 if(views.includes('first')){
  fp=createCharacterRig(base.color,base.cosmetics?.bodyColor,true);markFirstPersonRig(fp);buildWeapon(fp.weapon,spec);fp.offWeapon.visible=paired;if(paired)buildWeapon(fp.offWeapon,spec,true);
  fp.head.visible=false;fp.leftLeg.visible=false;fp.rightLeg.visible=false;
  for(const part of [fp.pelvis,fp.spine,fp.chest,fp.neck])for(const child of part.children)if(child instanceof T.Mesh)child.visible=false;
  fpCamera=new T.PerspectiveCamera(VIEW.firstFov,cellW/cell,.08,400);viewRoot=new T.Group();fpCamera.add(viewRoot);viewRoot.add(fp.group);scene.add(fpCamera);
 }
 const start=num('start',0),span=num('span',mode==='walk'||mode==='run'?.8:1.2),warm=num('warm',1),dt=num('dt',1/60);
 const samples=Array.from({length:cols},(_,i)=>start+(cols===1?0:span*i/(cols-1)));
 const root=new T.Vector3(base.pos.x,base.pos.y,base.pos.z),home=root.clone();
 const shot=(i:number,t:number)=>{const origin=follow?rig.group.position:home,target=new T.Vector3(origin.x,num('focus',.85),origin.z);
  views.forEach((view,row)=>{
   let cam=camera;
   if(fp&&viewRoot&&fpCamera){const first=view==='first';rig.group.visible=!first;viewRoot.visible=first;if(first)cam=fpCamera;}
   if(cam===camera)aim(camera,view,target);
   gl.render(scene,cam);ctx.drawImage(gl.domElement,i*cellW,22+row*cell);if(view==='first'&&q.get('crosshair')==='1'){const cx=i*cellW+cellW/2,cy=22+row*cell+cell/2;ctx.strokeStyle='#ff2a5a';ctx.lineWidth=1;ctx.beginPath();ctx.moveTo(cx-9,cy);ctx.lineTo(cx-3,cy);ctx.moveTo(cx+3,cy);ctx.lineTo(cx+9,cy);ctx.moveTo(cx,cy-9);ctx.lineTo(cx,cy-3);ctx.moveTo(cx,cy+3);ctx.lineTo(cx,cy+9);ctx.stroke();}if(row===0)label(ctx,`${(t-start).toFixed(2)}s`,i*cellW+2,24);if(i===0)label(ctx,view,2,22+row*cell+cell-18);});
  rig.group.visible=true;};
 const staticPatch=withRel(patch,start),keys=patchAt.map(k=>({t:Number(k.t),patch:withRel(k.patch||{},start)})).sort((a,b)=>a.t-b.t);
 if(blockHits.length)clearBlockImpacts();
 const fired=new Set<number>();let prev:number|undefined;
 const step=(t:number,h:number)=>{
  let p:PlayerView;
  if(kind==='lobby'){p=lobbyPlayer({...base,cosmetics:{...base.cosmetics,lobbyPose:(q.get('lobbyPose')||'weapon') as never} as PlayerView['cosmetics']});}
  else p=animationPreviewPlayer({...base},mode,t);
  if(!legacy&&kind!=='lobby')p={...p,hitStunUntil:-1e9};
  let extra:Record<string,unknown>=t>=patchFrom?{...staticPatch}:{};for(const k of keys)if(t>=k.t)extra={...extra,...k.patch};
  p={...p,...extra} as PlayerView;
  if(travel&&prev!==undefined&&kind!=='lobby'){
   const gap=t-prev,roll=p.roll;
   if(roll&&prev<roll.until&&t>roll.at){const u=rollVelocity(roll,prev,gap);root.x+=u.x*gap;root.z+=u.z*gap;}
   else if(!(committed(p,t)||(p.hitStunUntil||0)>t)){root.x+=p.vel.x*gap;root.z+=p.vel.z*gap;}
   if(standardWeapon(p.spec)){const a=attackPose(p,t),c=p.attackCommitment||attackCommitment(p.spec,p.lastAttack,a.windup,a.window,p.attackRate||1,a.windup+a.window),d=attackTravel(c,t)-attackTravel(c,prev),f=forward(p.attackCommitment?c.yaw:p.yaw);root.x+=f.x*d;root.z+=f.z*d;}
  }
  for(const at of blockHits)if(!fired.has(at)&&t>=at){fired.add(at);const f=forward(p.yaw);recordBlockHit({type:'blockHit',time:at,pos:{x:root.x+f.x*.45,y:root.y+.25,z:root.z+f.z*.45},source:p.id,target:'lab-attacker',message:'melee'},t,{x:root.x,y:root.y,z:root.z});}
  rig.group.position.set(travel?root.x:p.pos.x,p.pos.y,travel?root.z:p.pos.z);rig.group.rotation.y=p.yaw;
  if(travel){grid.position.x=floor.position.x=Math.round(root.x);grid.position.z=floor.position.z=Math.round(root.z);}
  if(kind==='lobby')animateLobbyPose(rig,p,legacy?t:Math.max(0,t),h);else animateCharacter(rig,p,t,h,p.pitch);
  if(rawClip){const c=rawClip;sampleClip(c,Math.max(0,t),rawRow);rig.body.position.set(0,-.025,0);rig.body.rotation.set(0,0,0);applyMocapPose(rig,rawRow,{weight:1});}
  if(mods&&kind!=='lobby'){weaponModifiers(rig.weapon,p,t);bodyModifiers(rig,p,t);if(p.spec==='daggers')weaponModifiers(rig.offWeapon,p,t);}
  if(fp&&viewRoot&&fpCamera){
   poseFirstPersonView(fp,viewRoot,p,t,h,false);
   if(mods){weaponModifiers(fp.weapon,p,t);bodyModifiers(fp,p,t);if(p.spec==='daggers')weaponModifiers(fp.offWeapon,p,t);}
   const eye=cameraPosition({x:rig.group.position.x,y:rig.group.position.y,z:rig.group.position.z},p.yaw,p.pitch,'first'),look=forward(p.yaw,p.pitch);
   fpCamera.fov=p.adsSince!==undefined?adsProfile(p.spec)?.fov??VIEW.firstFov:VIEW.firstFov;fpCamera.updateProjectionMatrix();
   fpCamera.position.set(eye.x,eye.y,eye.z);fpCamera.lookAt(eye.x+look.x*30,eye.y+look.y*30,eye.z+look.z*30);
  }
  prev=t;
 };
 let next=0;
 if(exact){
  for(let t=start-warm;next<samples.length;t=Math.min(t+dt,samples[next])){
   step(t,prev===undefined?dt:t-prev);
   while(next<samples.length&&Math.abs(samples[next]-t)<1e-9){shot(next,samples[next]);next++;}
  }
 }else for(let t=start-warm;next<samples.length;t+=dt){
  step(t,dt);
  while(next<samples.length&&t+dt/2>=samples[next]){shot(next,samples[next]);next++;}
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
// kind=minions: the Siren minion gallery (tools/visual-lab/minions.ts; parameters in its header).
(kind==='minions'?import('./minions.js').then(m=>m.minionLab(q,sheet,status)):main()).catch(e=>{status.textContent='ERROR '+(e as Error).message;console.error(e);(window as unknown as {labError:string}).labError=String((e as Error).stack||e);});
