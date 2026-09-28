import {RainWarrior} from './ansatsuken-intro.js';
import {MatrixThrone} from './leet-intro.js';
import * as T from 'three';
import {CharacterRig,createCharacterRig} from './character-rig.js';
import {dressRig} from './cosmetics.js';
import {introFrame} from '../shared/intros.js';
import type {Snapshot} from '../shared/simulation.js';
import {ArmorGlow} from './armor-glow.js';
import {poseIntro,introCamera,INTRO_HIT,ease} from './intro-choreography.js';
export {poseIntro} from './intro-choreography.js';

/** A self-contained stage with absolute-time choreography and four authored shots per entrance. */
export class PlayerIntros {
 readonly scene=new T.Scene();readonly rain=new RainWarrior(this.scene);readonly throne=new MatrixThrone(this.scene);readonly camera=new T.PerspectiveCamera(43,1,.1,80);readonly armor=new ArmorGlow(this.scene);
 private rig:CharacterRig|undefined;private key='';private rings:T.Mesh[]=[];private sparks:T.Mesh[]=[];private streaks:T.Mesh[]=[];private arcs:T.Mesh[]=[];private columns:T.Mesh[]=[];private pulses:T.Mesh[]=[];private core:T.Mesh;private shell:T.Mesh;private accent=new T.MeshBasicMaterial({color:0x75eeff,toneMapped:false});private rim:T.PointLight;
 constructor(){
  this.scene.background=new T.Color(0x040611);this.scene.fog=new T.Fog(0x040611,9,26);this.scene.add(new T.HemisphereLight(0x9fbcdf,0x14101d,.6));const key=new T.DirectionalLight(0xd9e6ff,1.35);key.position.set(-3,5,4);this.scene.add(key);this.rim=new T.PointLight(0x56eaff,7,9);this.rim.position.set(1,2,-2);this.scene.add(this.rim);const fill=new T.PointLight(0xf369db,4,8);fill.position.set(-3,1.3,1);this.scene.add(fill);
  const floor=new T.Mesh(new T.PlaneGeometry(80,80),new T.MeshLambertMaterial({color:0x080d19}));floor.rotation.x=-Math.PI/2;floor.position.y=-.065;this.scene.add(floor);
  const grid=new T.GridHelper(36,36);grid.material.dispose();grid.material=new T.LineBasicMaterial({color:0x24536b,transparent:true,opacity:.35});grid.position.y=-.06;this.scene.add(grid);
  const platform=new T.Mesh(new T.CylinderGeometry(1.18,1.28,.08,48),new T.MeshLambertMaterial({color:0x12182a}));platform.position.y=-.04;this.scene.add(platform);
  for(let i=0;i<3;i++){const ring=new T.Mesh(new T.RingGeometry(1.23+i*.40,1.25+i*.40,64),new T.MeshBasicMaterial({color:i===1?0xd562ed:0x5bdfff,transparent:true,opacity:.7-i*.18,side:T.DoubleSide,toneMapped:false}));ring.rotation.x=-Math.PI/2;ring.position.y=-.01;this.scene.add(ring);this.rings.push(ring);}
  for(let i=0;i<2;i++){const ring=new T.Mesh(new T.RingGeometry(.9,.94,64),new T.MeshBasicMaterial({color:i?0xf68eff:0x97f7ff,transparent:true,opacity:0,side:T.DoubleSide,depthWrite:false,toneMapped:false}));ring.rotation.x=-Math.PI/2;ring.position.y=.012+i*.003;this.scene.add(ring);this.pulses.push(ring);}
  const cube=new T.BoxGeometry(1,1,1);for(let i=0;i<28;i++){const spark=new T.Mesh(cube,this.accent);this.scene.add(spark);this.sparks.push(spark);}
  for(let i=0;i<10;i++){const streak=new T.Mesh(cube,new T.MeshBasicMaterial({color:i%2?0xdf6de9:0x5df0ff,transparent:true,opacity:.65,toneMapped:false,depthWrite:false}));this.scene.add(streak);this.streaks.push(streak);}
  for(let i=0;i<12;i++){const column=new T.Mesh(cube,new T.MeshBasicMaterial({color:0x71efff,transparent:true,opacity:.4,toneMapped:false,depthWrite:false}));this.scene.add(column);this.columns.push(column);}
  for(let i=0;i<3;i++){const arc=new T.Mesh(new T.TorusGeometry(.73+i*.13,.016,5,48,Math.PI*1.35),new T.MeshBasicMaterial({color:i%2?0xff81e8:0x88edff,transparent:true,opacity:.8,toneMapped:false,depthWrite:false}));this.scene.add(arc);this.arcs.push(arc);}
  this.core=new T.Mesh(new T.IcosahedronGeometry(.13,2),new T.MeshBasicMaterial({color:0xd8fcff,toneMapped:false,transparent:true,opacity:.95}));this.shell=new T.Mesh(new T.IcosahedronGeometry(.24,1),new T.MeshBasicMaterial({color:0x83eaff,wireframe:true,toneMapped:false,transparent:true,opacity:.6}));this.scene.add(this.core,this.shell);
  // Sparse synth backdrop, deliberately outside the subject's silhouette.
  for(const side of [-1,1])for(let n=0;n<4;n++){const bar=new T.Mesh(cube,new T.MeshBasicMaterial({color:side<0?0x276979:0x743465,toneMapped:false}));bar.position.set(side*(3.4+n*.75),.8+n*.15,-3.2-n*.5);bar.scale.set(.035,1.6+n*.3,.035);this.scene.add(bar);}
 }
 update(state:Snapshot,aspect:number,quality=1,compatibility=false){
  const frame=introFrame(state);if(!frame){this.clear();return false;}const {player:p,style,age}=frame,key=p.id+JSON.stringify(p.cosmetics)+p.color;
  if(this.key!==key){this.clear();this.key=key;this.rig=createCharacterRig(p.color,p.cosmetics?.bodyColor,true);dressRig(this.rig,p.cosmetics);this.scene.add(this.rig.group);this.accent.color.set(p.color).lerp(new T.Color(0x89eaff),.48);this.rim.color.copy(this.accent.color);if(style==='dev-matrix')this.rim.color.set(0x55ff88);}
  this.rain.group.visible=style==='dev-ansatsuken';this.throne.group.visible=style==='dev-matrix';poseIntro(this.rig!,style,age);if(style==='dev-matrix')this.throne.update(age,this.rig!);if(style==='dev-ansatsuken')this.rain.update(age,this.rig!,quality);introCamera(this.camera,style,age,this.rig!.group.position,aspect);this.armor.update([{rig:this.rig!,player:p}],state.time,quality,compatibility);
  const hit=INTRO_HIT[style],burst=Math.max(0,age-hit),fade=Math.max(0,1-burst/1.2),active=age>=hit&&fade>0;
  this.pulses.forEach((ring,i)=>{const elapsed=burst-i*.09;ring.visible=active&&elapsed>=0;ring.rotation.set(style==='pulse-wave'?0:-Math.PI/2,0,0);ring.position.set(0,style==='pulse-wave'?1.13:.015+i*.003,style==='pulse-wave'?.3:0);ring.scale.setScalar(.12+elapsed*(style==='pulse-wave'?3.5:2.7));(ring.material as T.MeshBasicMaterial).opacity=fade*.9;});
  this.sparks.forEach((spark,i)=>{spark.visible=active&&(quality>=.5||i%2===0);const a=i*2.39996,d=.2+burst*(1.3+i%5*.23);spark.position.set(Math.cos(a)*d,.06+Math.max(0,burst*(1.3+i%3*.25)-burst*burst*1.7),Math.sin(a)*d);spark.scale.set(.035,.05+.08*fade,.035).multiplyScalar(fade);spark.rotation.set(burst*3,a,burst*4);});
  this.streaks.forEach((streak,i)=>{streak.visible=style==='vector-slide'&&age>.1&&age<1.6;streak.position.set(this.rig!.group.position.x-.4-i*.16,.035+(i%2)*.02,(i%5-2)*.17);streak.scale.set(.5+ease(age/.8)*1.1,.016,.025);(streak.material as T.MeshBasicMaterial).opacity=.6*(1-ease((age-1.08)/.52));});
  this.columns.forEach((column,i)=>{column.visible=style==='neon-drop'&&age<1.25;const a=i*Math.PI/6;column.position.set(Math.cos(a)*.75,this.rig!.group.position.y+1+(i%3)*.3,Math.sin(a)*.75);column.scale.set(.02,.6+(i%3)*.5,.02);(column.material as T.MeshBasicMaterial).opacity=.5*(1-ease((age-.88)/.37));});
  this.arcs.forEach((arc,i)=>{arc.visible=(style==='signal-spin'&&age>.25&&age<2.4)||(style==='arc-salute'&&age>1.25&&age<2.65);arc.position.set(0,style==='arc-salute'?1.38:.9,-.12);arc.rotation.set(style==='arc-salute'?0:Math.PI/2+i*.4,i*.4,age*(i%2?-3:3)+i*2);arc.scale.setScalar(style==='arc-salute'?.55:1);(arc.material as T.MeshBasicMaterial).opacity=style==='arc-salute'?.5*(1-ease((age-2.2)/.45)):.75*(1-ease((age-1.5)/.9));});
  this.core.visible=this.shell.visible=style==='pulse-wave'&&age<1.7;const charge=ease(age/1.45),pop=ease((age-1.55)/.15);for(const orb of [this.core,this.shell]){orb.position.copy(this.rig!.group.position).add(new T.Vector3(0,.15,.47));orb.scale.setScalar((.35+charge*.85)*(1+pop*3));orb.rotation.set(age*2,age*4,age);(orb.material as T.MeshBasicMaterial).opacity=(1-pop)*.85;}
  this.rings.forEach((r,i)=>{r.visible=style!=='dev-ansatsuken';r.rotation.z=age*(i%2?-.18:.14);});return true;
 }
 clear(){this.armor.clear();if(this.rig){this.rig.group.removeFromParent();this.rig.sockets.head.traverse(o=>{if(o instanceof T.Mesh&&o.geometry.type!=='BoxGeometry')o.geometry.dispose();});}this.rig=undefined;this.key='';}
 dispose(){this.clear();this.armor.dispose();const geometries=new Set<T.BufferGeometry>(),materials=new Set<T.Material>();this.scene.traverse(o=>{if(o instanceof T.Mesh||o instanceof T.LineSegments){geometries.add(o.geometry);for(const m of Array.isArray(o.material)?o.material:[o.material])materials.add(m);}});geometries.forEach(g=>g.dispose());materials.forEach(m=>m.dispose());this.scene.clear();}
}
