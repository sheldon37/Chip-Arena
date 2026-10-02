/**
 * Siren minion gallery for the visual lab (dev only; loaded by lab.ts for kind=minions). Renders the real minion
 * generator and animation code (src/client/siren/minions).
 *
 *  kind=minions&view=grid    a grid of seeds standing idle (default)
 *    seeds=1,2,3,...         seeds (default 12 seeds); cols=<n> per row (default 6)
 *    t=<s>                   lab time of the still (default 2.4); views=front,three,back,side (camera rows per seed)
 *  kind=minions&view=strip   one seed through its animation states, one row per state
 *    seed=<n>                (default 7); states=idle,walk,run,lunge,club,smash,flinch,emerge,death
 *    frames=<n>              columns per row (default 8); span=<s> overrides each state's span
 *    cam=side|three|front|back camera for the strip (default three)
 *    faces=1                 every seed once per face option (blank, eyes, cracked)
 *    pilot=1                 a normal Pilot in the first cell (scale and value reference)
 *  yaw=<rad>                 turn the minions and cameras together (yaw=3.14 faces the arena moon light)
 *  kind=minions&view=ward    a horde coming out of a Ward Zero spawn (SIREN_ADD_SPAWNS) with the stage dressing
 *    spawn=<id>              spawn point (default door-ne); t=<s> lab time; song=<s> song time at t=0 (default 140)
 *    siren=1                 the Siren itself idles among them; aspect=1.778 for a wide shot
 *  Shared: face=blank|eyes|cracked, rot=<0..1>, burn=<0..1>, wounds=<n>, missing=<option>, scrap=<option>, arms=<style>,
 *          cell=<px> (default 260), aspect=<w/h>, dist=<m>, height=<m>, focus=<m>, light=arena|lab (default arena),
 *          low=1 (low detail), quality=<0..1> (effects), bg=<css hex>
 * Frames sample authority time; walk/run integrate the velocity into the root over a checker floor so foot sliding shows.
 */
import * as T from 'three';
import {arenaLighting} from '../../src/client/arena-lighting.js';
import {createCharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter} from '../../src/client/character-animation.js';
import {MATERIALS} from '../../src/shared/config.js';
import {Simulation,initPhysics,type Snapshot} from '../../src/shared/simulation.js';
import {sirenify,animateSiren,SirenStageVisuals} from '../../src/client/siren/index.js';
import {WARD_ZERO_AIR} from '../../src/client/siren/stage.js';
import {sirenStageBlocks,SIREN_STAGE_THEME,SIREN_ADD_SPAWNS} from '../../src/shared/siren-stage-layout.js';
import {createSirenMinion,animateSirenMinion,setSirenMinionDetail,MINION_TIMING,MINION_FACES,type SirenMinionView,type MinionOptions,type SirenMinion} from '../../src/client/siren/minions/index.js';

type State={name:string;span:number;view:(t:number)=>SirenMinionView;speed:number};
const STATES:Record<string,State>={
 idle:{name:'idle',span:2.4,speed:0,view:()=>({yaw:0,vel:{x:0,z:0}})},
 walk:{name:'walk 1.3 m/s',span:1.6,speed:1.3,view:()=>({yaw:0,vel:{x:0,z:1.3}})},
 shamble:{name:'walk .8 m/s',span:1.6,speed:.8,view:()=>({yaw:0,vel:{x:0,z:.8}})},
 run:{name:'run 4.5 m/s',span:.9,speed:4.5,view:()=>({yaw:0,vel:{x:0,z:4.5}})},
 lunge:{name:'lunge',span:.95,speed:0,view:()=>({yaw:0,vel:{x:0,z:0},attackAt:0,attack:'lunge'})},
 club:{name:'club',span:.95,speed:0,view:()=>({yaw:0,vel:{x:0,z:0},attackAt:0,attack:'club'})},
 smash:{name:'smash',span:.95,speed:0,view:()=>({yaw:0,vel:{x:0,z:0},attackAt:0,attack:'smash'})},
 flinch:{name:'flinch',span:.5,speed:0,view:()=>({yaw:0,vel:{x:0,z:0},hitAt:0})},
 emerge:{name:'emerge',span:MINION_TIMING.emerge+.15,speed:0,view:()=>({yaw:0,vel:{x:0,z:0},spawnAt:0})},
 death:{name:'death',span:MINION_TIMING.deathFx-.2,speed:0,view:()=>({yaw:0,vel:{x:0,z:0},deathAt:0})},
};

export async function minionLab(q:URLSearchParams,sheet:HTMLCanvasElement,status:HTMLElement){
 const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d,view=q.get('view')||'grid',cell=num('cell',260),aspect=num('aspect',view==='strip'?.8:.8),cellW=Math.round(cell*aspect);
 const dist=num('dist',view==='strip'?3.6:3.1),camH=num('height',1.05),focus=num('focus',.9),light=q.get('light')||'arena',low=q.get('low')==='1',quality=num('quality',1);
 const opts:MinionOptions={};if(q.has('face'))opts.face=q.get('face') as MinionOptions['face'];for(const k of ['rot','burn','wounds','hunch'] as const)if(q.has(k))opts[k]=Number(q.get(k));
 if(q.has('missing'))opts.missing=q.get('missing') as MinionOptions['missing'];if(q.has('scrap'))opts.scrap=q.get('scrap') as MinionOptions['scrap'];if(q.has('arms'))opts.arms=q.get('arms') as MinionOptions['arms'];
 const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cellW,cell);gl.shadowMap.enabled=true;gl.shadowMap.type=T.PCFSoftShadowMap;
 if(light!=='lab'){gl.outputColorSpace=T.SRGBColorSpace;gl.toneMapping=T.ACESFilmicToneMapping;gl.toneMappingExposure=.92;}
 const scene=new T.Scene();scene.background=new T.Color(q.get('bg')||'#1a2029');
 if(light==='lab'){scene.add(new T.HemisphereLight(0xcfe3ff,0x2a2530,1.1));const key=new T.DirectionalLight(0xffffff,1.6);key.position.set(3,6,4);scene.add(key);}else arenaLighting(scene,false);
 const c=document.createElement('canvas');c.width=c.height=64;const g=c.getContext('2d')!;g.fillStyle='#3a4450';g.fillRect(0,0,64,64);g.fillStyle='#2f3741';g.fillRect(0,0,32,32);g.fillRect(32,32,32,32);
 const map=new T.CanvasTexture(c);map.wrapS=map.wrapT=T.RepeatWrapping;map.repeat.set(40,40);map.magFilter=T.NearestFilter;map.colorSpace=T.SRGBColorSpace;
 const floor=new T.Mesh(new T.PlaneGeometry(40,40),new T.MeshLambertMaterial({color:0xffffff,map}));floor.rotation.x=-Math.PI/2;floor.receiveShadow=true;scene.add(floor);
 const camera=new T.PerspectiveCamera(36,cellW/cell,.05,200);
 const ctx=sheet.getContext('2d')!;const label=(text:string,x:number,y:number)=>{ctx.font='11px ui-monospace,monospace';ctx.fillStyle='rgba(0,0,0,.55)';ctx.fillRect(x,y,Math.min(cellW,ctx.measureText(text).width+10),16);ctx.fillStyle='#e8eef2';ctx.fillText(text,x+5,y+12);};
 const yaw=num('yaw',0),cy=Math.cos(yaw),sy=Math.sin(yaw);
 const aim=(cam:string,x:number,z:number)=>{const o:Record<string,[number,number,number]>={side:[dist,camH,0],front:[0,camH,dist],three:[dist*.72,camH+.2,dist*.72],'three-left':[-dist*.72,camH+.2,dist*.72],back:[0,camH,-dist]};const p=o[cam]||o.three;camera.position.set(x+p[0]*cy+p[2]*sy,p[1],z-p[0]*sy+p[2]*cy);camera.lookAt(x,focus,z);};
 const place=(m:SirenMinion,x:number,z:number)=>{m.rig.group.position.set(x,-m.floor,z);m.rig.group.rotation.y=yaw;floor.position.set(Math.round(x),0,Math.round(z));};
 const build=(seed:number,face=opts.face)=>{const m=createSirenMinion(seed,{...opts,face});if(low)setSirenMinionDetail(m,true);m.rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.name!=='siren-minion-fx'&&o.name!=='siren-minion-tongue')o.castShadow=true;});scene.add(m.rig.group);return m;};
 if(view==='ward'){await ward(q,scene,floor,gl,camera,build,quality);ctx.canvas.width=cellW;ctx.canvas.height=cell;ctx.drawImage(gl.domElement,0,0);status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;return;}
 if(view==='strip'){
  const seed=num('seed',7),states=(q.get('states')||'idle,walk,run,lunge,club,emerge,death').split(',').filter(s=>STATES[s]),frames=num('frames',8),cam=q.get('cam')||'three';
  sheet.width=frames*cellW+90;sheet.height=states.length*cell+22;ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
  const m=build(seed);ctx.fillStyle='#ffb27a';ctx.font='11px ui-monospace,monospace';ctx.fillText(`siren minion seed ${seed} · ${m.look.face} · ${m.look.arms} · scrap ${m.look.scrap} · missing ${m.look.missing} · rot ${m.look.rot} · cam ${cam}${low?' · low':''}`,6,15);
  states.forEach((key,row)=>{const st=STATES[key],span=num('span',st.span),y=22+row*cell;
   for(let i=0;i<frames;i++){const t=span*i/Math.max(1,frames-1),x=st.speed*t*sy,z=st.speed*t*cy,v=st.view(t);place(m,x,z);animateSirenMinion(m,{...v,yaw,vel:{x:v.vel.z*sy,z:v.vel.z*cy}},t,quality);m.rig.group.updateMatrixWorld(true);aim(cam,x,z);gl.render(scene,camera);ctx.drawImage(gl.domElement,90+i*cellW,y);label(`${t.toFixed(2)}s`,90+i*cellW+2,y+2);}
   ctx.fillStyle='#ffb27a';ctx.fillText(st.name,4,y+cell/2);});
 }else{
  const seeds=(q.get('seeds')||'1,2,3,4,5,6,7,8,9,10,11,12').split(',').map(Number),cols=num('cols',6),views=(q.get('views')||'three').split(','),t=num('t',2.4),rows=Math.ceil((seeds.length*(q.get('faces')==='1'?MINION_FACES.length:1)+(q.get('pilot')==='1'?1:0))/cols)*views.length;
  sheet.width=cols*cellW;sheet.height=rows*cell+22;ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
  ctx.fillStyle='#ffb27a';ctx.font='11px ui-monospace,monospace';ctx.fillText(`siren minions · idle t=${t} · ${views.join(',')}${opts.face?' · face '+opts.face:''}${low?' · low':''}`,6,15);
  // pilot=1: a normal Pilot in the first cell for scale and value reference.
  if(q.get('pilot')==='1'){const r=createCharacterRig('#ff6b35',undefined,true);r.group.position.set(0,.865,0);r.group.rotation.y=yaw;r.group.traverse(o=>{if(o instanceof T.Mesh)o.castShadow=true;});scene.add(r.group);r.group.updateMatrixWorld(true);
   views.forEach((v,k)=>{aim(v,0,0);gl.render(scene,camera);const y=22+k*cell;ctx.drawImage(gl.domElement,0,y);if(k===0)label('pilot (reference)',2,y+2);});scene.remove(r.group);}
  const offset=q.get('pilot')==='1'?1:0;
  // faces=1: each seed once per face option, side by side.
  const entries=q.get('faces')==='1'?seeds.flatMap(seed=>MINION_FACES.map(face=>({seed,face}))):seeds.map(seed=>({seed,face:opts.face}));
  entries.forEach(({seed,face},j)=>{const i=j+offset,m=build(seed,face),col=i%cols,block=Math.floor(i/cols);place(m,0,0);animateSirenMinion(m,{yaw,vel:{x:0,z:0}},t,quality);m.rig.group.updateMatrixWorld(true);
   views.forEach((v,k)=>{aim(v,0,0);gl.render(scene,camera);const y=22+(block*views.length+k)*cell;ctx.drawImage(gl.domElement,col*cellW,y);if(k===0)label(`#${seed}${q.get('faces')==='1'?' '+m.look.face:''} ${m.look.arms}${m.look.scrap!=='none'?' '+m.look.scrap:''}${m.look.missing!=='none'?' -'+m.look.missing:''}`,col*cellW+2,y+2);});
   scene.remove(m.rig.group);});
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}

/** A horde shot in Ward Zero: the hall, its air and dressing, minions emerging at a spawn, shambling, running and
 * lunging toward the camera, optionally with the Siren among them. */
async function ward(q:URLSearchParams,scene:T.Scene,floor:T.Mesh,gl:T.WebGLRenderer,camera:T.PerspectiveCamera,build:(seed:number,face?:MinionOptions['face'])=>SirenMinion,quality:number){
 const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d,t=num('t',3),song=num('song',140),spawn=SIREN_ADD_SPAWNS.find(s=>s.id===(q.get('spawn')||'door-ne'))||SIREN_ADD_SPAWNS[0];
 floor.visible=false;scene.background=new T.Color(WARD_ZERO_AIR.background);scene.fog=new T.Fog(WARD_ZERO_AIR.fog,WARD_ZERO_AIR.near,WARD_ZERO_AIR.far);scene.getObjectByName('night-moon')?.position.set(...WARD_ZERO_AIR.key);
 const props=new Map<string,T.Object3D>();for(const b of sirenStageBlocks()){const m=new T.Mesh(new T.BoxGeometry(b.size.x,b.size.y,b.size.z),new T.MeshLambertMaterial({color:b.material==='foundation'?SIREN_STAGE_THEME.floor:MATERIALS[b.material].color}));m.position.set(b.pos.x,b.pos.y,b.pos.z);m.castShadow=!b.id.startsWith('siren-roof-');m.receiveShadow=true;scene.add(m);props.set(b.id,m);}
 const visuals=new SirenStageVisuals(scene);visuals.setActive(true);
 const f={x:Math.sin(spawn.yaw),z:Math.cos(spawn.yaw)},r={x:f.z,z:-f.x},at=(fw:number,lat:number)=>({x:spawn.x+f.x*fw+r.x*lat,z:spawn.z+f.z*fw+r.z*lat});
 // [seed, forward, lateral, speed, state offset]: emerging at the threshold, a shambling front, a runner, a lunge.
 const horde:[number,number,number,number,string][]=[[21,1.2,-1.1,0,'emerge:.85'],[22,1.0,1.3,0,'emerge:1.35'],[23,3.4,-2.2,1.0,''],[24,4.2,.6,1.2,''],[25,5.6,2.4,.9,''],[26,6.4,-.8,4.2,''],[27,9.4,.9,0,'lunge:.33'],[28,7.4,-2.8,1.1,'']];
 for(const [seed,fw,lat,speed,state] of horde){const m=build(seed),p=at(fw,lat),[kind,ofs]=state.split(':'),view={yaw:spawn.yaw,vel:{x:f.x*speed,z:f.z*speed},spawnAt:kind==='emerge'?t-Number(ofs):undefined,attackAt:kind==='lunge'?t-Number(ofs):undefined,attack:kind==='lunge'?'lunge' as const:undefined};
  m.rig.group.position.set(p.x,-m.floor,p.z);m.rig.group.rotation.y=spawn.yaw;for(let k=-1.2;k<=0;k+=1/30)animateSirenMinion(m,{...view},t+k,quality);}
 if(q.get('siren')==='1'){
  await initPhysics();const sim=new Simulation({sandbox:true});sim.addPlayer('siren','Siren','ranged',null,'#b8b0ff',{});const pl=sim.players.get('siren')!;sim.resetRound();sim.phase='fight';for(let i=0;i<30;i++)sim.step(1/60);
  const base=sim.snapshot().players.find(p=>p.id==='siren')!,rig=createCharacterRig('#b8b0ff',undefined,true);sirenify(rig);scene.add(rig.group);void pl;
  const sp=at(5.6,-3.4),pv={...base,spec:'siren' as never,pos:{x:sp.x,y:base.pos.y,z:sp.z},yaw:spawn.yaw+.5,vel:{x:0,y:0,z:0},hitStunUntil:-1e9};rig.group.position.set(sp.x,base.pos.y,sp.z);rig.group.rotation.y=pv.yaw;
  for(let k=-1;k<=0;k+=1/30){const snap={...sim.snapshot(),time:t+k} as Snapshot;animateCharacter(rig,pv,t+k,1/30,0);animateSiren(rig,pv,snap,t+k,1/30,0);}sim.dispose();
 }
 const snap=(time:number)=>({time,tick:0,phase:'fight',players:[],props:[],projectiles:[],events:[],options:{map:'siren'},siren:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},bossSong:{track:'black-siren-protocol',startedAt:-song}} as unknown as Snapshot);
 const eye=at(num('camFwd',12.5),num('camLat',1.2)),look=at(3,0);camera.position.set(eye.x,num('height',1.75),eye.z);camera.lookAt(look.x,num('focus',1.0),look.z);camera.updateMatrixWorld();
 for(let k=t-1;k<t;k+=1/30)visuals.update(snap(k),k,quality,camera);visuals.update(snap(t),t,quality,camera);visuals.shell(props);
 gl.render(scene,camera);
}
