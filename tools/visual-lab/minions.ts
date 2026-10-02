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
 *    pilot=1                 a normal Pilot in the first cell (scale and value reference)
 *  Shared: face=blank|eyes|cracked, rot=<0..1>, burn=<0..1>, wounds=<n>, missing=<option>, scrap=<option>, arms=<style>,
 *          cell=<px> (default 260), aspect=<w/h>, dist=<m>, height=<m>, focus=<m>, light=arena|lab (default arena),
 *          low=1 (low detail), quality=<0..1> (effects), bg=<css hex>
 * Frames sample authority time; walk/run integrate the velocity into the root over a checker floor so foot sliding shows.
 */
import * as T from 'three';
import {arenaLighting} from '../../src/client/arena-lighting.js';
import {createCharacterRig} from '../../src/client/character-rig.js';
import {createSirenMinion,animateSirenMinion,setSirenMinionDetail,MINION_TIMING,type SirenMinionView,type MinionOptions,type SirenMinion} from '../../src/client/siren/minions/index.js';

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
 const aim=(cam:string,x:number,z:number)=>{const o:Record<string,[number,number,number]>={side:[dist,camH,0],front:[0,camH,dist],three:[dist*.72,camH+.2,dist*.72],'three-left':[-dist*.72,camH+.2,dist*.72],back:[0,camH,-dist]};const p=o[cam]||o.three;camera.position.set(x+p[0],p[1],z+p[2]);camera.lookAt(x,focus,z);};
 const place=(m:SirenMinion,x:number,z:number)=>{m.rig.group.position.set(x,-m.floor,z);m.rig.group.rotation.y=0;floor.position.set(Math.round(x),0,Math.round(z));};
 const build=(seed:number)=>{const m=createSirenMinion(seed,opts);if(low)setSirenMinionDetail(m,true);m.rig.group.traverse(o=>{if(o instanceof T.Mesh&&o.name!=='siren-minion-fx'&&o.name!=='siren-minion-tongue')o.castShadow=true;});scene.add(m.rig.group);return m;};
 if(view==='strip'){
  const seed=num('seed',7),states=(q.get('states')||'idle,walk,run,lunge,club,emerge,death').split(',').filter(s=>STATES[s]),frames=num('frames',8),cam=q.get('cam')||'three';
  sheet.width=frames*cellW+90;sheet.height=states.length*cell+22;ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
  const m=build(seed);ctx.fillStyle='#ffb27a';ctx.font='11px ui-monospace,monospace';ctx.fillText(`siren minion seed ${seed} · ${m.look.face} · ${m.look.arms} · scrap ${m.look.scrap} · missing ${m.look.missing} · rot ${m.look.rot} · cam ${cam}${low?' · low':''}`,6,15);
  states.forEach((key,row)=>{const st=STATES[key],span=num('span',st.span),y=22+row*cell;
   for(let i=0;i<frames;i++){const t=span*i/Math.max(1,frames-1),z=st.speed*t;place(m,0,z);animateSirenMinion(m,st.view(t),t,quality);m.rig.group.updateMatrixWorld(true);aim(cam,0,z);gl.render(scene,camera);ctx.drawImage(gl.domElement,90+i*cellW,y);label(`${t.toFixed(2)}s`,90+i*cellW+2,y+2);}
   ctx.fillStyle='#ffb27a';ctx.fillText(st.name,4,y+cell/2);});
 }else{
  const seeds=(q.get('seeds')||'1,2,3,4,5,6,7,8,9,10,11,12').split(',').map(Number),cols=num('cols',6),views=(q.get('views')||'three').split(','),t=num('t',2.4),rows=Math.ceil((seeds.length+(q.get('pilot')==='1'?1:0))/cols)*views.length;
  sheet.width=cols*cellW;sheet.height=rows*cell+22;ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
  ctx.fillStyle='#ffb27a';ctx.font='11px ui-monospace,monospace';ctx.fillText(`siren minions · idle t=${t} · ${views.join(',')}${opts.face?' · face '+opts.face:''}${low?' · low':''}`,6,15);
  // pilot=1: a normal Pilot in the first cell for scale and value reference.
  if(q.get('pilot')==='1'){const r=createCharacterRig('#ff6b35',undefined,true);r.group.position.set(0,.865,0);r.group.traverse(o=>{if(o instanceof T.Mesh)o.castShadow=true;});scene.add(r.group);r.group.updateMatrixWorld(true);
   views.forEach((v,k)=>{aim(v,0,0);gl.render(scene,camera);const y=22+k*cell;ctx.drawImage(gl.domElement,0,y);if(k===0)label('pilot (reference)',2,y+2);});scene.remove(r.group);}
  const offset=q.get('pilot')==='1'?1:0;
  seeds.forEach((seed,j)=>{const i=j+offset,m=build(seed),col=i%cols,block=Math.floor(i/cols);place(m,0,0);animateSirenMinion(m,{yaw:0,vel:{x:0,z:0}},t,quality);m.rig.group.updateMatrixWorld(true);
   views.forEach((v,k)=>{aim(v,0,0);gl.render(scene,camera);const y=22+(block*views.length+k)*cell;ctx.drawImage(gl.domElement,col*cellW,y);if(k===0)label(`#${seed} ${m.look.arms}${m.look.scrap!=='none'?' '+m.look.scrap:''}${m.look.missing!=='none'?' -'+m.look.missing:''}`,col*cellW+2,y+2);});
   scene.remove(m.rig.group);});
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
