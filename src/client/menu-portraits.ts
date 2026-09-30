import * as T from 'three';
import {SVGRenderer} from 'three/examples/jsm/renderers/SVGRenderer.js';
import {createCharacterRig,recolorCharacterRig,setRigDetail,type CharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {disposeAuditorHalo} from './auditor-halo.js';
import {buildWeapon} from './weapon-visuals.js';
import {animateLobbyPose,lobbyChair} from './lobby-poses.js';
import {lobbyPose,type LobbyPose} from '../shared/lobby-presentation.js';
import type {PlayerView} from '../shared/simulation.js';

/** Frames the pilot from the podium to the top of the head or hat (top, world y; default: a bare head), with a
 * higher view for the reclining pose. Narrow cards keep the full character width. */
export function framePortrait(camera:T.OrthographicCamera,pose:LobbyPose,aspect:number,top=1.72){
 const sleep=pose==='sleep',high=Math.min(2.7,Math.max(1.72,top))+.1,low=-.2,centre=sleep?.35:(high+low)/2,half=Math.max(sleep?1.12:(high-low)/2*1.16,.95/aspect+1e-6);
 camera.left=-half*aspect;camera.right=half*aspect;camera.top=half;camera.bottom=-half;
 camera.position.set(0,sleep?4.8:centre+.94,sleep?3.2:5.2);
 camera.lookAt(0,centre,0);camera.updateProjectionMatrix();
}
const hatBox=new T.Box3();
/** World top of the head cosmetic (hat), measured once per outfit. */
function hatTop(r:CharacterRig){const key=r.group.userData.cosmeticKey;if(r.group.userData.hatTopKey!==key){r.group.updateMatrixWorld(true);hatBox.setFromObject(r.sockets.head);r.group.userData.hatTopKey=key;r.group.userData.hatTop=hatBox.isEmpty()?0:hatBox.max.y;}return r.group.userData.hatTop as number;}

type Actor={scene:T.Scene;rig:CharacterRig;chair:T.Group;decor:T.Group;spec:string};
/** One shared render context serves every lobby card, including an eight-player room. */
export class MenuPortraits {
 private renderer?:T.WebGLRenderer|SVGRenderer;
 private actors=new Map<string,Actor>();
 private slots:{canvas:HTMLCanvasElement;context:CanvasRenderingContext2D|null;player:PlayerView}[]=[];
 private camera=new T.OrthographicCamera(-1.3,1.3,1.3,-1.3,.1,30);
 private time=0;
 private initialize(){
  if(this.renderer)return;
  try{const renderer=new T.WebGLRenderer({alpha:true,antialias:true,preserveDrawingBuffer:true,powerPreference:'low-power'});renderer.setSize(384,480);renderer.setPixelRatio(1);renderer.setClearColor(0,0);renderer.outputColorSpace=T.SRGBColorSpace;renderer.toneMapping=T.ACESFilmicToneMapping;renderer.toneMappingExposure=1.1;this.renderer=renderer;}
  catch{const renderer=new SVGRenderer();renderer.setPrecision(3);renderer.setClearColor(new T.Color(0),0);this.renderer=renderer;}
 }
 bind(root:Element,players:PlayerView[]){
  const byId=new Map(players.map(p=>[p.id,p]));this.slots=[];
  root.querySelectorAll<HTMLCanvasElement>('canvas[data-pilot-preview]').forEach(canvas=>{const player=byId.get(canvas.dataset.pilotPreview!);if(player)this.slots.push({canvas,context:canvas.getContext('2d'),player});});
  const ids=new Set(this.slots.map(s=>s.player.id));for(const [id,actor] of this.actors)if(!ids.has(id)){this.release(actor);this.actors.delete(id);}
  if(this.slots.length)this.initialize();
 }
 private actor(p:PlayerView){
  let a=this.actors.get(p.id);if(!a){
   const scene=new T.Scene(),rig=createCharacterRig(p.color,p.cosmetics?.bodyColor),chair=lobbyChair(),decor=new T.Group();
   rig.group.position.y=.88;scene.add(rig.group);decor.add(chair);if(!(this.renderer instanceof T.WebGLRenderer))setRigDetail(rig,true);
   const podium=new T.Mesh(new T.CylinderGeometry(.96,1.02,.075,32),new T.MeshLambertMaterial({color:0x333440}));podium.position.y=-.065;decor.add(podium);scene.add(decor);
   // A softer fill and a more lateral key separate the box faces; a stronger cool rim lifts the silhouette off the dark card.
   scene.add(new T.HemisphereLight(0xe3eeff,0x474054,1.7));const key=new T.DirectionalLight(0xffedda,2.6);key.position.set(3.5,4,2.5);scene.add(key);const rim=new T.DirectionalLight(0x849fe3,2);rim.position.set(-3,2,-2);scene.add(rim);
   a={scene,rig,chair,decor,spec:''};this.actors.set(p.id,a);
  }
  recolorCharacterRig(a.rig,p.color,p.cosmetics?.bodyColor||'#e5e7eb');dressRig(a.rig,p.cosmetics);
  if(a.spec!==p.spec){a.spec=p.spec;a.rig.sockets.rightHand.add(a.rig.weapon);a.rig.sockets.leftHand.add(a.rig.offWeapon);buildWeapon(a.rig.weapon,p.spec);a.rig.offWeapon.clear();if(['daggers','emperor','ansatsuken'].includes(p.spec))buildWeapon(a.rig.offWeapon,p.spec,true);}
  return a;
 }
 render(dt:number,players?:PlayerView[]){
  if(!this.renderer||!this.slots.length||document.hidden)return;this.time+=Math.min(dt,.05);
  const updated=players?new Map(players.map(p=>[p.id,p])):undefined;
  const reduced=matchMedia('(prefers-reduced-motion: reduce)').matches;
  for(const slot of this.slots){
   const {canvas,context}=slot;if(!canvas.isConnected)continue;
   const rect=canvas.getBoundingClientRect();if(rect.width<2||rect.height<2||rect.bottom<0||rect.top>innerHeight)continue;
   const p=updated?.get(slot.player.id)||slot.player,pose=lobbyPose(p.cosmetics?.lobbyPose),a=this.actor(p);slot.player=p;
   canvas.parentElement!.dataset.pose=pose;a.chair.visible=pose==='chair';
   animateLobbyPose(a.rig,p,reduced?0:this.time,.033,pose);
   framePortrait(this.camera,pose,rect.width/rect.height,hatTop(a.rig));
   if(this.renderer instanceof T.WebGLRenderer){
    if(!context)continue;
    const width=Math.max(1,Math.round(rect.width*Math.min(devicePixelRatio,1.5))),height=Math.max(1,Math.round(rect.height*Math.min(devicePixelRatio,1.5)));
    if(canvas.width!==width||canvas.height!==height){canvas.width=width;canvas.height=height;}
    this.renderer.render(a.scene,this.camera);context.clearRect(0,0,width,height);context.drawImage(this.renderer.domElement,0,0,width,height);
   }else{
    this.renderer.setSize(rect.width,rect.height);this.renderer.render(a.scene,this.camera);
    const parent=canvas.parentElement!;parent.querySelector('.portrait-svg')?.remove();const svg=this.renderer.domElement.cloneNode(true) as SVGElement;svg.classList.add('portrait-svg');svg.style.backgroundColor='transparent';svg.setAttribute('aria-hidden','true');parent.append(svg);canvas.style.visibility='hidden';
   }
  }
 }
 private release(a:Actor){
  disposeAuditorHalo(a.rig);a.rig.sockets.head.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry')o.geometry.dispose();});
  const geometry=new Set<T.BufferGeometry>(),materials=new Set<T.Material>();a.decor.traverse(o=>{if(o instanceof T.Mesh){geometry.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);}});geometry.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());a.scene.clear();
 }
 dispose(){for(const a of this.actors.values())this.release(a);this.actors.clear();this.slots=[];if(this.renderer instanceof T.WebGLRenderer)this.renderer.dispose();this.renderer=undefined;}
}
