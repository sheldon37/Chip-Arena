/**
 * Development-only Siren lab. Renders the real Siren visuals (src/client/siren) as labelled frame strips, driven by
 * synthetic snapshots that follow the shared contract (src/shared/siren-contract.ts). Not built by Vite and never
 * imported from src. With `npm run dev` running, open /tools/siren-lab/index.html?<query>.
 *
 *  kind=rig (default)  the Siren through a move. move=idle|walk|swap|hit|knockdown|<SirenMoveId>; mode=melee|ranged;
 *                      whiff=1 makes Last Laugh miss
 *  kind=intro          the dev-siren intro scene (use aspect=1.778)
 *  kind=stage          Ward Zero at song time t (views: overview,eye,play,back,corner,cells,beacon)
 *  kind=fp             the first-person view model (mode=melee|ranged)
 *  stage=1, song=<s>   (rig) put the Siren inside Ward Zero, at song time <s> for lab t=0
 *  move=death          the Siren's own death effect; whiff=1 makes Last Laugh miss
 *  hide=<regex>        hide objects whose name matches (debugging); probe=x,y,z logs small meshes containing a point
 *  frames, start, span   sample times start + i*span/(frames-1) (seconds on the move/song clock)
 *  warm=<s>              simulated before the first sample (default .6), dt=<s> (default 1/60)
 *  views=front,side,three,back,top,wide,behind   camera rows; dist, height, focus (look-at height), ahead (look-at z)
 *  cell=<px>, aspect=<w/h>, light=arena|ward|lab, quality=<0-1>
 */
import * as T from 'three';
import {createCharacterRig,type CharacterRig} from '../../src/client/character-rig.js';
import {animateCharacter} from '../../src/client/character-animation.js';
import {arenaLighting} from '../../src/client/arena-lighting.js';
import {MATERIALS,type Vec} from '../../src/shared/config.js';
import {VIEW} from '../../src/shared/view.js';
import {hitReaction} from '../../src/shared/hit-reaction.js';
import type {PlayerView,Snapshot,GameEvent} from '../../src/shared/simulation.js';
import {sirenify,animateSiren,sirenFirstPerson,SirenEffects,SirenIntroStage,SirenStageVisuals,SIREN_KILL_EFFECTS} from '../../src/client/siren/index.js';
import {poseFirstPersonView} from '../../src/client/first-person-view.js';
import {markFirstPersonRig} from '../../src/client/pose-flags.js';
import {SIREN,SIREN_TIMING,SIREN_SPAWN,sirenPoint,type SirenMoveId,type SirenMoveState,type SirenPlayerState,type SirenWorldView,type SirenShot,type SirenMode} from '../../src/shared/siren-contract.js';
import {sirenStageBlocks,SIREN_STAGE_THEME} from '../../src/shared/siren-stage-layout.js';
import {SIREN_INTRO} from '../../src/shared/siren-contract.js';

const q=new URLSearchParams(location.search);
const num=(k:string,d:number)=>q.has(k)?Number(q.get(k)):d;
const kind=q.get('kind')||'rig',move=q.get('move')||'idle';
const frames=Math.max(1,num('frames',8)),cell=num('cell',240),aspect=num('aspect',1),cellW=Math.round(cell*aspect);
const dist=num('dist',4.2),camHeight=num('height',1.1),focus=num('focus',.9),ahead=num('ahead',0);
const light=q.get('light')||'arena',quality=num('quality',1),dt=num('dt',1/60),warm=num('warm',.6);
const status=document.getElementById('status')!;const sheet=document.getElementById('sheet') as HTMLCanvasElement;

type Shot=Snapshot['projectiles'][number]&{siren?:SirenShot};
interface Frame {players:PlayerView[];projectiles:Shot[];world:SirenWorldView;events:GameEvent[];deaths:{victim:string;at:number;effect:string}[];bossSong?:{track:'black-siren-protocol';startedAt:number}}

function player(id:string,color:string,pos:Vec,yaw:number,extra:Partial<PlayerView>={}):PlayerView{
 return {id,name:id,color,class:'melee',spec:'sword',pos,vel:{x:0,y:0,z:0},yaw,pitch:0,hp:100,maxHp:100,mana:100,maxMana:100,ammo:0,maxAmmo:0,stamina:100,maxStamina:100,blockTime:0,blockCd:0,blockCooldown:0,blockDuration:0,lastBlock:-10,guarding:false,dashCd:0,secondaryCd:0,cooldown:0,reloadTime:0,score:0,roundWins:0,chips:{},ready:true,selected:true,offers:[],connected:true,bot:null,ack:0,grounded:true,lastAttack:-10,lastSecondary:-10,reach:1,windup:0,statuses:[],hitStunUntil:-1e9,...extra} as PlayerView;
}

/** The move timeline exactly as the systems write it (absolute times), started at t=0. */
function moveState(id:SirenMoveId,seq=1,side:1|-1=1):SirenMoveState{
 const T0=0;
 switch(id){
  case 'swipe':case 'rend':{const m=SIREN_TIMING[id];const hitAt=T0+m.windup;return {id,seq,at:T0,hitAt,until:hitAt+m.active+m.recover,side};}
  case 'slam':return {id,seq,at:T0,hitAt:SIREN_TIMING.slam.windup,until:SIREN_TIMING.slam.windup+SIREN_TIMING.slam.recover};
  case 'rush':{const m=SIREN_TIMING.rush;return {id,seq,at:T0,hitAt:m.windup,endAt:m.windup+m.dash,until:m.windup+m.dash+m.recover,dir:{x:0,y:0,z:1}};}
  case 'laugh':{const m=SIREN_TIMING.laugh,hitAt=m.windup,endAt=hitAt+m.lunge;if(q.get('whiff')==='1')return {id,seq,at:T0,hitAt,endAt,until:endAt+m.whiff,target:'victim'};
   return {id,seq,at:T0,hitAt,endAt,until:endAt+m.release,target:'victim',caught:'victim',caughtAt:endAt,crushAt:endAt+m.crush};}
  case 'hellfire':{const m=SIREN_TIMING.hellfire;return {id,seq,at:T0,hitAt:m.windup,until:m.windup+m.recover,side};}
  case 'damnation':{const m=SIREN_TIMING.damnation;return {id,seq,at:T0,hitAt:m.windup,until:m.windup+m.recover};}
  case 'eruption':{const m=SIREN_TIMING.eruption;return {id,seq,at:T0,hitAt:m.windup,until:m.windup+m.recover,aim:{x:0,y:0,z:8}};}
  case 'rain':{const m=SIREN_TIMING.rain;return {id,seq,at:T0,hitAt:m.windup,until:m.windup+m.recover,aim:{x:0,y:0,z:9}};}
  case 'mark':{const m=SIREN_TIMING.mark;return {id,seq,at:T0,endAt:m.voice,hitAt:m.voice+m.lock,until:m.voice+m.lock+m.recover,target:'target'};}
 }
}
const MELEE:SirenMoveId[]=['swipe','rend','slam','rush','laugh'];
const rng=(seed:number)=>()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/4294967296;};

/** Synthetic authoritative state at time t for the chosen move. Deterministic: a pure function of t. */
function scenario(t:number):Frame{
 const out:Frame={players:[],projectiles:[],world:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},events:[],deaths:[]};
 const base={x:0,y:.85,z:0};let pos={...base},vel={x:0,y:0,z:0};const yaw=0;
 const isMove=['swipe','rend','slam','rush','laugh','hellfire','damnation','eruption','rain','mark'].includes(move);
 const mode:SirenMode=(q.get('mode') as SirenMode)||(isMove&&!MELEE.includes(move as SirenMoveId)?'ranged':'melee');
 const s:SirenPlayerState={mode,modeAt:move==='swap'?0:-10};
 const ev=(type:string,at:number,data:Partial<GameEvent>)=>{if(t>=at)out.events.push({id:out.events.length+1,time:at,type,pos:pos,source:SIREN.spec,...data} as GameEvent);};
 if(move==='walk'){vel={x:0,y:0,z:5.2};pos={x:0,y:.85,z:5.2*t};}
 if(isMove){
  // Repeating primaries alternate sides like the real input does.
  const id=move as SirenMoveId,period=id==='swipe'?.4:id==='hellfire'?.42:Infinity,n=period<Infinity?Math.max(0,Math.floor(t/period)):0;
  const m=moveState(id,n+1,n%2?-1:1);if(period<Infinity){const shift=n*period;m.at+=shift;m.hitAt+=shift;m.until+=shift;}
  if(t<=m.until+.6)s.move=m;
  const hand=(side:1|-1)=>sirenPoint(base,yaw,side===1?SIREN_SPAWN.rightHand:SIREN_SPAWN.leftHand);
  if(id==='swipe'||id==='rend'){for(let k=0;k<=n;k++){const at=SIREN_TIMING[id].windup+k*(period<Infinity?period:0);ev('siren-'+id,at,{pos:sirenPoint(base,yaw,{x:0,y:.2,z:1.1}),amount:(k%2?-1:1),radius:id==='rend'?3:2.3});}}
  if(id==='slam')ev('siren-slam',m.hitAt,{pos:sirenPoint({...base,y:0},yaw,{x:0,y:0,z:1.2}),radius:3.6});
  if(id==='rush'){const a=m.hitAt,b=m.endAt!,d=6,u=Math.min(1,Math.max(0,(t-a)/(b-a)));pos={x:0,y:.85,z:d*u};if(t>a&&t<b)vel={x:0,y:0,z:d/(b-a)};
   ev('siren-rush',a,{pos:{...base},from:{x:0,y:.85,z:d}});
   for(let k=0;k<8;k++){const at=a+(b-a)*k/8;if(t>=at)out.world.patches.push({id:'trail-'+k,owner:SIREN.spec,kind:'trail',pos:{x:0,y:0,z:d*k/8+.4},radius:.75,at,until:at+3});}}
  if(id==='laugh'){ev('siren-laugh',0,{});ev('siren-lunge',m.hitAt,{target:'victim'});const lunge=Math.min(1,Math.max(0,(t-m.hitAt)/(m.endAt!-m.hitAt)));pos={x:0,y:.85,z:2.2*lunge};
   const vpos={x:0,y:.85,z:3.4};if(m.caught){ev('siren-catch',m.caughtAt!,{target:'victim',pos:vpos});ev('siren-crush',m.crushAt!,{target:'victim',pos:{...vpos,y:vpos.y+.7}});
    if(t>=m.caughtAt!&&t<m.until)out.world.grabs.push({owner:SIREN.spec,victim:'victim',at:m.caughtAt!,crushAt:m.crushAt!,releaseAt:m.until});
    const held=t>=m.caughtAt!;const v=player('victim','#3fc6ff',held?sirenPoint(pos,yaw,{x:-.15,y:.55,z:.75}):vpos,Math.PI,{});if(t>=m.crushAt!){v.hp=0;out.deaths.push({victim:'victim',at:m.crushAt!,effect:SIREN.deaths.crush});}out.players.push(v);}
   else{ev('siren-whiff',m.endAt!,{});out.players.push(player('victim','#3fc6ff',{x:2.2,y:.85,z:3.6},Math.PI,{}));}}
  if(id==='hellfire'){for(let k=0;k<=n;k++){const at=SIREN_TIMING.hellfire.windup+k*period,side:1|-1=k%2?-1:1,from=hand(side),age=t-at;if(age<0)continue;ev('siren-cast',at,{pos:from,message:'hellfire'});
    const vz=24,vy=1.2,g=4,land=(vy+Math.sqrt(vy*vy+2*g*from.y))/g,hitAge=Math.min(land,(14-from.z)/vz);
    if(age<hitAge)out.projectiles.push({id:'hf-'+k,owner:SIREN.spec,spec:'sword' as never,pos:{x:from.x,y:from.y+vy*age-g*age*age/2,z:from.z+vz*age},vel:{x:0,y:vy-g*age,z:vz},radius:.22,reflections:0,elements:[],siren:{kind:'hellfire',born:at}});
    else{const p={x:from.x,y:0,z:from.z+vz*hitAge};out.world.patches.push({id:'hp-'+k,owner:SIREN.spec,kind:'hellfire',pos:p,radius:1.1,at:at+hitAge,until:at+hitAge+10});ev('siren-hit',at+hitAge,{pos:{...p,y:.3},message:'hellfire'});}}}
  if(id==='damnation'){const at=m.hitAt,from=sirenPoint(base,yaw,SIREN_SPAWN.overhead),age=t-at,vy=4,vz=15,g=9,land=(vy+Math.sqrt(vy*vy+2*g*from.y))/g;
   if(age>=0){ev('siren-cast',at,{pos:from,message:'damnation'});if(age<land)out.projectiles.push({id:'dm',owner:SIREN.spec,spec:'sword' as never,pos:{x:from.x,y:from.y+vy*age-g*age*age/2,z:from.z+vz*age},vel:{x:0,y:vy-g*age,z:vz},radius:.5,reflections:0,elements:[],siren:{kind:'damnation',born:at}});
    else{const p={x:from.x,y:0,z:from.z+vz*land};ev('siren-explode',at+land,{pos:p,radius:3.5});out.world.patches.push({id:'scorch',owner:SIREN.spec,kind:'scorch',pos:p,radius:2.2,at:at+land,until:at+land+4});}}}
  if(id==='eruption'){const at=m.hitAt,burst=at+SIREN_TIMING.eruption.warning;if(t>=at)out.world.eruptions.push({id:'er',owner:SIREN.spec,pos:m.aim!,radius:2.2,at,burstAt:burst,until:burst+SIREN_TIMING.eruption.burst});ev('siren-cast',at,{message:'eruption'});ev('siren-eruption',burst,{pos:m.aim!,radius:2.2});}
  if(id==='rain'){const at=m.hitAt,until=at+SIREN_TIMING.rain.duration,c=m.aim!;if(t>=at&&t<until+1)out.world.rains.push({id:'rn',owner:SIREN.spec,center:c,radius:7,at,until});ev('siren-cast',at,{message:'rain'});
   const r=rng(7);for(let k=0;k<60;k++){const spawn=at+k/6,ang=r()*Math.PI*2,rad=Math.sqrt(r())*7,land={x:c.x+Math.cos(ang)*rad,y:0,z:c.z+Math.sin(ang)*rad},fall=.62,age=t-spawn;if(spawn>=until)break;
    if(age>=0&&age<fall)out.projectiles.push({id:'drop-'+k,owner:SIREN.spec,spec:'sword' as never,pos:{x:land.x,y:8*(1-age/fall),z:land.z},vel:{x:0,y:-13,z:0},radius:.16,reflections:0,elements:[],siren:{kind:'drop',born:spawn,land,landAt:spawn+fall}});
    if(age>=fall)ev('siren-drop',spawn+fall,{pos:land});}}
  if(id==='mark'){const target={x:1.5,y:.85,z:13};out.players.push(player('target','#ffd04a',target,Math.PI,{}));ev('siren-mark',0,{target:'target'});ev('siren-lock',m.endAt!,{pos:target,target:'target'});
   const shotAge=t-m.hitAt,from=sirenPoint(base,yaw,SIREN_SPAWN.seeker);if(t<m.hitAt+20)out.world.marks.push({id:'mk',owner:SIREN.spec,target:'target',at:0,lockAt:m.endAt!,throwAt:m.hitAt,shot:shotAge>=0?'seeker':undefined});
   if(shotAge>=0){const d={x:target.x-from.x,y:target.y+.3-from.y,z:target.z-from.z},L=Math.hypot(d.x,d.y,d.z),u=Math.min(1,3.2*shotAge/L);out.projectiles.push({id:'seeker',owner:SIREN.spec,spec:'sword' as never,pos:{x:from.x+d.x*u,y:from.y+d.y*u,z:from.z+d.z*u},vel:{x:d.x/L*3.2,y:d.y/L*3.2,z:d.z/L*3.2},radius:.45,reflections:0,elements:[],siren:{kind:'seeker',born:m.hitAt,target:'target'}});}}
 }
 const extra:Partial<PlayerView>={};
 if(move==='hit'){extra.hitReaction=hitReaction({x:0,y:.85,z:0},0,{x:0,y:0,z:-1},{x:.1,y:1.25,z:.2},.2,24);extra.hitStunUntil=.2+.35;}
 if(move==='knockdown')extra.knockdown={at:.1,until:2.2} as never;
 const siren=player(SIREN.spec,'#b8b0ff',pos,yaw,{vel,...extra});(siren as unknown as {siren:SirenPlayerState}).siren=s;
 if(move==='death'&&t>=.2){siren.hp=0;out.deaths.push({victim:SIREN.spec,at:.2,effect:SIREN.deaths.dissipate});}
 out.players.unshift(siren);
 // Stable ids: sorted by time, newly reached events always append, as in a real snapshot.
 out.events.sort((a,b)=>a.time-b.time);out.events.forEach((e,i)=>{e.id=i+1;});
 return out;
}

function snapshotAt(t:number,f:Frame):Snapshot{
 return {time:t,tick:Math.round(t*60),phase:'fight',remaining:60,round:1,setNumber:1,setWinner:null,winner:null,result:'',players:f.players,props:[],projectiles:f.projectiles,events:f.events,impact:null,metrics:{bodies:0,projectiles:0,tickMs:0},options:{map:'foundry'},siren:f.world,bossSong:f.bossSong} as unknown as Snapshot;
}

function lights(scene:T.Scene){
 if(light==='arena')arenaLighting(scene,false);
 else if(light==='ward'){scene.background=new T.Color(SIREN_STAGE_THEME.sky);scene.add(new T.HemisphereLight(0x8f86a8,0x0c0a10,.35));const k=new T.DirectionalLight(0xc9c1e6,.6);k.position.set(-6,12,-4);scene.add(k);}
 else{scene.background=new T.Color(0x2a2f38);scene.add(new T.HemisphereLight(0xdfe8ff,0x2c2733,1.2));const k=new T.DirectionalLight(0xffffff,1.6);k.position.set(3,6,4);scene.add(k);}
}
function floor(scene:T.Scene,color=0x262833){
 const f=new T.Mesh(new T.PlaneGeometry(80,80),new T.MeshLambertMaterial({color}));f.rotation.x=-Math.PI/2;f.receiveShadow=true;scene.add(f);
 const g=new T.GridHelper(40,40,0x3b3f4c,0x30333d);g.position.y=.003;scene.add(g);
}
function label(ctx:CanvasRenderingContext2D,text:string,x:number,y:number){ctx.fillStyle='rgba(0,0,0,.6)';ctx.fillRect(x,y,Math.min(cellW,ctx.measureText(text).width+10),16);ctx.fillStyle='#ece8f4';ctx.fillText(text,x+5,y+12);}

function rigCamera(camera:T.PerspectiveCamera,view:string,target:T.Vector3){
 const o:Record<string,number[]>={front:[0,camHeight,dist],side:[dist,camHeight,0],three:[dist*.72,camHeight+.35,dist*.72],'three-left':[-dist*.72,camHeight+.35,dist*.72],back:[0,camHeight,-dist],top:[0,dist+1.5,.01],wide:[dist*1.6,camHeight+3.5,-dist*.8],behind:[1.2,camHeight+.6,-dist*.9],high:[dist*.5,dist*1.4,dist*.5]};
 const c=o[view]??o.front;camera.position.set(target.x+c[0],c[1],target.z+c[2]);camera.lookAt(target.x,focus,target.z+(view==='wide'||view==='behind'||view==='high'?ahead:0));
}

async function main(){
 const gl=new T.WebGLRenderer({antialias:true,preserveDrawingBuffer:true});gl.setPixelRatio(1);gl.setSize(cellW,cell);gl.shadowMap.enabled=true;gl.shadowMap.type=T.PCFSoftShadowMap;
 if(light!=='lab'){gl.outputColorSpace=T.SRGBColorSpace;gl.toneMapping=T.ACESFilmicToneMapping;gl.toneMappingExposure=.92;}
 const views=(q.get('views')||(kind==='stage'?'overview,eye':'front,side,three')).split(',').filter(Boolean),rows=kind==='intro'||kind==='fp'?1:views.length;
 sheet.width=frames*cellW;sheet.height=rows*cell+22;const ctx=sheet.getContext('2d')!;ctx.font='11px ui-monospace,monospace';ctx.fillStyle='#0c0b10';ctx.fillRect(0,0,sheet.width,sheet.height);
 ctx.fillStyle='#c9b8ff';ctx.fillText(`siren-lab · ${kind} · ${kind==='rig'?move:''} ${location.search.replace(/^\?/,'')}`,6,15);
 const camera=new T.PerspectiveCamera(kind==='intro'?43:38,cellW/cell,.05,300);
 const start=num('start',0),span=num('span',kind==='intro'?SIREN_INTRO.duration:1.2),times=Array.from({length:frames},(_,i)=>start+(frames===1?0:span*i/(frames-1)));
 const draw=(r:number,c:number,text:string)=>{ctx.drawImage(gl.domElement,c*cellW,22+r*cell);label(ctx,text,c*cellW+2,24+r*cell);};

 if(kind==='fp'){
  // First person, built like the renderer's view model: a rig on a root under the camera, torso/head/legs hidden.
  const scene=new T.Scene();lights(scene);floor(scene);const root=new T.Group();camera.add(root);scene.add(camera);const rig=createCharacterRig('#b8b0ff',undefined,true);markFirstPersonRig(rig);root.add(rig.group);
  rig.head.visible=false;rig.leftLeg.visible=false;rig.rightLeg.visible=false;for(const part of [rig.pelvis,rig.spine,rig.chest,rig.neck])for(const c of part.children)if(c instanceof T.Mesh)c.visible=false;
  camera.fov=VIEW.firstFov;camera.updateProjectionMatrix();camera.position.set(0,1.6,0);camera.lookAt(0,1.4,6);let prev=times[0]-warm;
  for(let i=0;i<times.length;i++){for(let t=prev;t<=times[i];t+=dt){const f=scenario(t),me={...f.players[0],spec:'sword' as never};poseFirstPersonView(rig,root,me,t,dt,false);sirenFirstPerson(rig,{...f.players[0],spec:SIREN.spec as never},t,dt);}prev=times[i]+dt;
   gl.render(scene,camera);draw(0,i,`fp ${times[i].toFixed(2)}s`);}
  done();return;
 }
 if(kind==='intro'){
  const scene=new T.Scene();scene.background=new T.Color(0x040611);scene.fog=new T.Fog(0x040611,9,26);scene.add(new T.HemisphereLight(0x9fbcdf,0x14101d,.6));const key=new T.DirectionalLight(0xd9e6ff,1.35);key.position.set(-3,5,4);scene.add(key);
  const stage=new SirenIntroStage(scene),rig=createCharacterRig('#b8b0ff',undefined,true);scene.add(rig.group);
  for(let i=0;i<times.length;i++){stage.update(times[i],rig,camera,quality);gl.render(scene,camera);draw(0,i,`${times[i].toFixed(2)}s`);}
  done();return;
 }

 const scene=new T.Scene();lights(scene);
 if(kind==='stage'){
  scene.background=new T.Color(SIREN_STAGE_THEME.sky);scene.fog=new T.Fog(new T.Color(SIREN_STAGE_THEME.sky).lerp(new T.Color(0x405677),.42),65,210);
  for(const b of sirenStageBlocks()){const m=new T.Mesh(new T.BoxGeometry(b.size.x,b.size.y,b.size.z),new T.MeshLambertMaterial({color:b.material==='foundation'?SIREN_STAGE_THEME.floor:MATERIALS[b.material].color}));m.position.set(b.pos.x,b.pos.y,b.pos.z);m.castShadow=m.receiveShadow=true;scene.add(m);}
  const visuals=new SirenStageVisuals(scene);visuals.setActive(true);
  const cams:Record<string,[number[],number[]]>={overview:[[0,46,30],[0,0,2]],eye:[[0,4.2,-24],[0,1.2,0]],play:[[-3,3.2,-18],[2,1.4,2]],back:[[0,2.6,8],[0,3.2,-28]],corner:[[27,9,24],[0,0,0]],cells:[[-14,2.6,-2],[-29,1.8,-11]],beacon:[[-24,3.5,-20],[-10,3,-6]]};
  let prev=start-warm;
  for(let i=0;i<times.length;i++){for(let t=prev;t<times[i];t+=dt){visuals.update(snapshotAt(t,{players:[],projectiles:[],world:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},events:[],deaths:[],bossSong:{track:'black-siren-protocol',startedAt:0}}),t,quality,camera);}prev=times[i];
   const snap=snapshotAt(times[i],{players:[],projectiles:[],world:{patches:[],eruptions:[],rains:[],marks:[],grabs:[]},events:[],deaths:[],bossSong:{track:'black-siren-protocol',startedAt:0}});
   for(let r=0;r<views.length;r++){const [p,l]=cams[views[r]]??cams.overview;camera.position.set(p[0],p[1],p[2]);camera.lookAt(l[0],l[1],l[2]);visuals.update(snap,times[i],quality,camera);gl.render(scene,camera);draw(r,i,`${views[r]} · song ${times[i].toFixed(2)}s`);}}
  done();return;
 }

 // stage=1 puts the rig in Ward Zero; song=<s> is the song time at lab t=0.
 const inWard=q.get('stage')==='1',songAt=num('song',140);let ward:SirenStageVisuals|undefined;
 if(inWard){scene.background=new T.Color(SIREN_STAGE_THEME.sky);for(const b of sirenStageBlocks()){const m=new T.Mesh(new T.BoxGeometry(b.size.x,b.size.y,b.size.z),new T.MeshLambertMaterial({color:b.material==='foundation'?SIREN_STAGE_THEME.floor:MATERIALS[b.material].color}));m.position.set(b.pos.x,b.pos.y,b.pos.z);m.castShadow=m.receiveShadow=true;scene.add(m);}ward=new SirenStageVisuals(scene);ward.setActive(true);}
 else floor(scene);
 const rigs=new Map<string,CharacterRig>(),effects=new SirenEffects(scene),kills=new Map<string,{update(age:number):void}>();
 const rigFor=(p:PlayerView)=>{let r=rigs.get(p.id);if(!r){r=createCharacterRig(p.color,undefined,true);if(p.id===SIREN.spec)sirenify(r);r.group.traverse(o=>{if(o instanceof T.Mesh){o.castShadow=true;o.receiveShadow=true;}});scene.add(r.group);rigs.set(p.id,r);}return r;};
 let lastEvent=0;
 const step=(t:number,h:number)=>{
  const f=scenario(t),snap=snapshotAt(t,f);
  for(const p of f.players){const r=rigFor(p);r.group.position.set(p.pos.x,p.pos.y,p.pos.z);r.group.rotation.y=p.yaw;
   const dead=f.deaths.find(d=>d.victim===p.id);
   if(dead){r.group.visible=false;let k=kills.get(p.id);if(!k){const factory=SIREN_KILL_EFFECTS[dead.effect as keyof typeof SIREN_KILL_EFFECTS];k=factory(scene,r.group,p,{id:'d-'+p.id,at:dead.at,effect:dead.effect,seed:7,pos:p.pos,velocity:{x:0,y:0,z:0},direction:{x:0,y:0,z:1},source:SIREN.spec,kind:'melee',yaw:p.yaw,gibPart:'head'} as never,quality,400,snap);kills.set(p.id,k);}k.update(t-dead.at);continue;}
   animateCharacter(r,p,t,h,0);if(p.id===SIREN.spec)animateSiren(r,{...p,spec:SIREN.spec as never},snap,t,h,0);}
  for(const e of f.events)if(e.id>lastEvent){lastEvent=e.id;effects.event(e,t,quality);}
  effects.update(snap,t,rigs,quality,camera);
  if(ward){(snap as unknown as {bossSong:unknown}).bossSong={track:'black-siren-protocol',startedAt:-songAt};ward.update({...snap,time:t} as Snapshot,t,quality,camera);}
  return f;
 };
 let prev=times[0]-warm;
 for(let i=0;i<times.length;i++){
  for(let t=prev;t<times[i]-1e-9;t+=dt)step(t,dt);prev=times[i];const f=step(times[i],1e-4);
  const target=new T.Vector3(f.players[0].pos.x,0,f.players[0].pos.z);
  if(q.get('hide')){const re=new RegExp(q.get('hide')!);scene.traverse(o=>{if(re.test(o.name))o.visible=false;});}
  for(let r=0;r<views.length;r++){rigCamera(camera,views[r],target);effects.update(snapshotAt(times[i],f),times[i],rigs,quality,camera);gl.render(scene,camera);draw(r,i,`${views[r]} ${times[i].toFixed(2)}s`);}
 }
 if(q.get('probe')){const pt=new T.Vector3(...q.get('probe')!.split(',').map(Number));scene.updateMatrixWorld(true);scene.traverse(o=>{if(!(o instanceof T.Mesh||o instanceof T.Sprite)||!o.visible)return;const b=new T.Box3().setFromObject(o);b.expandByScalar(.05);if(b.containsPoint(pt)&&b.getSize(new T.Vector3()).length()<8)console.warn('probe hit',o.type,o.name,o.parent?.name,(o as T.Mesh).material&&((o as T.Mesh).material as T.Material).type,JSON.stringify(b.min),JSON.stringify(b.max));});}
 done();
}
function done(){status.textContent='done';(window as unknown as {labDone:boolean}).labDone=true;}
main().catch(e=>{status.textContent='error: '+(e?.stack||e);(window as unknown as {labError:string}).labError=String(e?.stack||e);});
