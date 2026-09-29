/**
 * Development-only visual lab. Renders the real rig, cosmetics, weapons and animation
 * code as labelled frame strips so presentation changes can be compared before/after.
 * Not part of the shipped client (Vite only builds the root index.html).
 *
 * Query parameters:
 *  kind=motion|intro|lobby   (default motion)
 *  spec=<weapon spec>        (default pistol)
 *  mode=<animation preview>  idle|walk|run|attack|windup|release|left-strike|right-strike|reload|block|jump
 *  patch=<JSON>              fields merged into the preview player every step
 *  rel=<a,b>                 patch fields offset by the sequence start time (e.g. lastAttack)
 *  frames, start, span       sample times start + i*span/(frames-1) seconds
 *  views=side,front,three,back,top   camera rows (motion/lobby)
 *  style=<intro style>       intro kind; lobbyPose=<pose> lobby kind
 *  cell=<px>, dist=<m>, height=<m>
 */
import * as T from 'three';
import {Simulation,initPhysics,type PlayerView,type Snapshot} from '../../src/shared/simulation.js';
import {v} from '../../src/shared/config.js';
import {createCharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter,animationPreviewPlayer} from '../../src/client/character-animation.js';
import {dressRig} from '../../src/client/cosmetics.js';
import {buildWeapon} from '../../src/client/weapon-visuals.js';
import {PlayerIntros} from '../../src/client/player-intros.js';
import {introDuration} from '../../src/shared/intros.js';
import {animateLobbyPose,lobbyPlayer} from '../../src/client/lobby-poses.js';

const q=new URLSearchParams(location.search);
const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d;
const kind=q.get('kind')||'motion',spec=q.get('spec')||'pistol',mode=q.get('mode')||'idle';
const frames=Math.max(1,num('frames',8)),cell=num('cell',220),dist=num('dist',4.3),camHeight=num('height',1.0);
const views=(q.get('views')||'side,front,three').split(',').filter(Boolean);
const patch=q.has('patch')?JSON.parse(q.get('patch')!):{};
const rel=(q.get('rel')||'').split(',').filter(Boolean);
const status=document.getElementById('status')!;
const sheet=document.getElementById('sheet') as HTMLCanvasElement;

function stage(){
 const scene=new T.Scene();scene.background=new T.Color(0x1a2029);
 scene.add(new T.HemisphereLight(0xcfe3ff,0x2a2530,1.1));
 const key=new T.DirectionalLight(0xffffff,1.6);key.position.set(3,6,4);scene.add(key);
 const floor=new T.Mesh(new T.PlaneGeometry(40,40),new T.MeshLambertMaterial({color:0x3a4450}));floor.rotation.x=-Math.PI/2;scene.add(floor);
 const grid=new T.GridHelper(20,40,0x5d6b78,0x4a5561);grid.position.y=.002;scene.add(grid);
 return scene;
}
function aim(camera:T.PerspectiveCamera,view:string,target:T.Vector3){
 const o={side:[dist,camHeight,0],front:[0,camHeight,dist],three:[dist*.72,camHeight+.35,dist*.72],back:[0,camHeight,-dist],top:[0,dist+1.2,.01]}[view]??[dist,camHeight,0];
 camera.position.set(target.x+o[0],o[1],target.z+o[2]);camera.lookAt(target);
}
function label(ctx:CanvasRenderingContext2D,text:string,x:number,y:number){ctx.fillStyle='rgba(0,0,0,.55)';ctx.fillRect(x,y,Math.min(cell,ctx.measureText(text).width+10),16);ctx.fillStyle='#e8eef2';ctx.fillText(text,x+5,y+12);}

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
 const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cell,cell);gl.shadowMap.enabled=true;
 const rows=kind==='intro'?1:views.length,cols=frames;
 sheet.width=cols*cell;sheet.height=rows*cell+22;const ctx=sheet.getContext('2d')!;ctx.font='11px ui-monospace,monospace';ctx.fillStyle='#101318';ctx.fillRect(0,0,sheet.width,sheet.height);
 const title=kind==='intro'?`intro ${q.get('style')} · ${spec}`:`${kind} · ${spec} · ${kind==='lobby'?q.get('lobbyPose')||'weapon':mode}${q.has('patch')?' · '+q.get('patch'):''}`;
 ctx.fillStyle='#ffb27a';ctx.fillText(title,6,15);
 const camera=new T.PerspectiveCamera(38,1,.05,200);
 if(kind==='intro'){
  const style=q.get('style')||'neon-drop',duration=introDuration(style),start=num('start',0),span=num('span',duration);
  base.cosmetics={...base.cosmetics,intro:style as never,overrideWeaponIntro:true};base.developer=true as never;
  const intros=new PlayerIntros();
  for(let i=0;i<cols;i++){const t=start+(cols===1?0:span*i/(cols-1));
   const s:Snapshot={...snap,players:[{...base}],phase:'intro',intro:{started:0,players:[id]},time:t} as Snapshot;
   intros.update(s,1,1,false);gl.render(intros.scene,intros.camera);ctx.drawImage(gl.domElement,i*cell,22);label(ctx,`${t.toFixed(2)}s`,i*cell+2,24);}
  status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;return;
 }
 const scene=stage();
 const rig=createCharacterRig(base.color,base.cosmetics?.bodyColor,true);dressRig(rig,base.cosmetics);buildWeapon(rig.weapon,spec);
 if(['daggers','ansatsuken','emperor'].includes(spec)){rig.offWeapon.visible=true;buildWeapon(rig.offWeapon,spec,true);}else rig.offWeapon.visible=false;
 rig.group.traverse(o=>{if(o instanceof T.Mesh){o.castShadow=true;o.receiveShadow=true;}});scene.add(rig.group);
 const start=num('start',0),span=num('span',mode==='walk'||mode==='run'?.8:1.2),warm=num('warm',1),dt=1/60;
 const samples=Array.from({length:cols},(_,i)=>start+(cols===1?0:span*i/(cols-1)));
 const shot=(i:number,t:number)=>{const target=new T.Vector3(rig.group.position.x,num('focus',.85),rig.group.position.z);
  views.forEach((view,row)=>{aim(camera,view,target);gl.render(scene,camera);ctx.drawImage(gl.domElement,i*cell,22+row*cell);if(row===0)label(ctx,`${(t-start).toFixed(2)}s`,i*cell+2,24);if(i===0)label(ctx,view,2,22+row*cell+cell-18);});};
 let next=0;
 for(let t=start-warm;next<samples.length;t+=dt){
  let p:PlayerView;
  if(kind==='lobby'){p=lobbyPlayer({...base,cosmetics:{...base.cosmetics,lobbyPose:(q.get('lobbyPose')||'weapon') as never}});}
  else p=animationPreviewPlayer({...base},mode,t);
  const extra:Record<string,unknown>={...patch};for(const k of rel)if(typeof extra[k]==='number')extra[k]=(extra[k] as number)+start;
  p={...p,...extra} as PlayerView;
  rig.group.position.set(p.pos.x,p.pos.y,p.pos.z);rig.group.rotation.y=p.yaw;
  if(kind==='lobby')animateLobbyPose(rig,p,t,dt);else animateCharacter(rig,p,t,dt,p.pitch);
  while(next<samples.length&&t+dt/2>=samples[next]){shot(next,samples[next]);next++;}
 }
 status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;
}
main().catch(e=>{status.textContent='ERROR '+(e as Error).message;console.error(e);(window as unknown as {labError:string}).labError=String((e as Error).stack||e);});
