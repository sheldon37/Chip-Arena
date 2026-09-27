import * as T from 'three';
import {createCharacterRig,rigBox} from './character-rig.js';

/** A broad, bespectacled adult in a stained tank top and blue work trousers. */
export function createVanDriver(){
 const r=createCharacterRig('#cfaa86',0xcfaa86);r.group.name='bass-driver';r.group.scale.setScalar(1.08);
 for(const name of ['abdomen','abdomen-shell','ribcage','upper-chest','sternum','back-panel','waist-band'])r.group.getObjectByName(name)!.visible=false;
 r.leftShoulder.position.x=.4;r.rightShoulder.position.x=-.4;
 rigBox(r.spine,'tank-belly',0,.07,.02,.78,.36,.52,0xe9e5d6);
 rigBox(r.chest,'tank-top',0,.10,.005,.72,.29,.47,0xe9e5d6);
 for(const side of [-1,1])rigBox(r.chest,'tank-strap',side*.24,.27,0,.13,.13,.39,0xe9e5d6);
 rigBox(r.spine,'shirt-stain',.19,.02,.286,.19,.09,.015,0x9c9172);
 rigBox(r.chest,'shirt-stain-small',-.16,.16,.25,.10,.12,.018,0xaaa286);
 rigBox(r.head,'hair-back',0,.21,-.15,.38,.24,.09,0x453a31);
 rigBox(r.head,'nose',0,.13,.2,.08,.1,.09,0xc29a76);
 rigBox(r.head,'moustache',0,.045,.18,.18,.045,.035,0x514033);
 rigBox(r.head,'glasses-bridge',0,.2,.191,.09,.03,.04,0x161b25);
 for(const side of [-1,1]){
  rigBox(r.head,'glasses-frame',side*.106,.20,.181,.185,.135,.044,0x161b25);
  rigBox(r.head,'glasses-lens',side*.106,.20,.207,.13,.08,.014,0xaadce8);
  rigBox(r.head,'glasses-eye',side*.10,.2,.217,.022,.033,.008,0x273440);
 }
 const trousers=new T.MeshLambertMaterial({color:0x34445f});r.group.traverse(o=>{if(o instanceof T.Mesh&&/thigh|shin|kneecap|hip-shell/.test(o.name))o.material=trousers;});
 return {rig:r,dispose(){trousers.dispose();}};
}

export function createBassVan(quality:number){
 const van=new T.Group();van.name='bass-van-vehicle';
 const box=(name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,color:T.ColorRepresentation)=>rigBox(van,name,x,y,z,sx,sy,sz,color);
 const white=0xf0f1e9,trim=0x303d4c;
 const glass=new T.MeshLambertMaterial({color:0x9bd7e2,transparent:true,opacity:.22,depthWrite:false,side:T.DoubleSide});
 const pane=(parent:T.Object3D,name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number)=>{const m=rigBox(parent,name,x,y,z,sx,sy,sz,0x9bd7e2);m.material=glass;return m;};
 box('chassis',0,-.59,0,3.85,.20,1.6,trim);
 box('cargo-floor',-.4,-.42,0,2.8,.16,1.5,0x455267);
 box('roof',-.04,1.19,0,3.78,.14,1.66,white);
 box('rear-wall',-1.86,.32,0,.16,1.64,1.6,white);
 box('far-wall',-.42,.34,-.76,2.95,1.55,.10,white);
 box('side-sill',-.36,-.29,.77,2.9,.36,.12,white);
 box('rear-pillar',-1.68,.6,.75,.18,1.04,.14,white);
 box('cab-pillar',.85,.53,.75,.15,1.18,.14,white);
 box('hood',1.49,-.05,0,.93,.49,1.57,white);
 box('rear-side-panel',-1.32,.34,.77,.69,1.55,.12,white);
 box('sliding-door-track',-.44,1.115,.855,2.7,.045,.05,trim);
 const cargoDoor=new T.Group();cargoDoor.name='bass-cargo-door';cargoDoor.position.set(-.08,0,.82);van.add(cargoDoor);
 rigBox(cargoDoor,'cargo-door-panel',0,-.035,0,1.8,.79,.10,white);
 rigBox(cargoDoor,'cargo-door-top',0,1.055,0,1.8,.16,.10,white);
 for(const side of [-1,1])rigBox(cargoDoor,'cargo-window-frame',side*.79,.665,0,.22,.63,.10,white);
 rigBox(cargoDoor,'cargo-door-solid-panel',0,.665,0,1.36,.62,.10,white);
 rigBox(cargoDoor,'cargo-door-handle',.66,.21,.08,.19,.05,.055,trim);
 const cabDoor=new T.Group();cabDoor.name='bass-cab-door';cabDoor.position.set(1.68,0,.78);van.add(cabDoor);
 rigBox(cabDoor,'cab-door-panel',-.365,-.09,0,.73,.66,.10,white);
 rigBox(cabDoor,'cab-door-top',-.365,1.04,0,.73,.10,.10,white);
 for(const x of [-.69,-.04])rigBox(cabDoor,'cab-window-frame',x,.62,0,.08,.76,.10,white);
 pane(cabDoor,'cab-door-window',-.365,.62,0,.57,.74,.025);
 rigBox(cabDoor,'cab-door-handle',-.59,.14,.075,.16,.045,.045,trim);
 box('far-cab-door',1.29,-.09,-.76,.73,.66,.10,white);
 pane(van,'far-cab-window',1.29,.62,-.76,.57,.74,.025);
 pane(van,'windshield',1.70,.675,0,.025,.75,1.36);
 box('windscreen-top',1.62,1.10,0,.12,.13,1.57,white);
 for(const side of [-1,1]){
  box('windshield-pillar',1.68,.71,side*.72,.11,.85,.10,white);
  box('mirror',1.48,.46,side*.95,.25,.18,.15,trim);
  box('headlight',1.975,-.015,side*.56,.025,.19,.27,0xfff4a5);
  box('tail-light',-1.952,-.19,side*.59,.024,.24,.19,0xff5449);
 }
 box('front-bumper',1.97,-.43,0,.16,.16,1.7,trim);
 box('rear-bumper',-1.98,-.43,0,.14,.16,1.7,trim);
 box('grille',1.979,-.22,0,.025,.18,.68,0x1a2432);
 box('passenger-bench',-.45,-.19,-.15,1.35,.22,.55,0x536781);
 box('bench-back',-.45,.12,-.47,1.35,.60,.13,0x536781);
 box('driver-seat',1.16,-.20,.13,.47,.18,.52,0x536781);
 box('driver-seat-back',.93,.12,.13,.12,.62,.52,0x536781);
 // Windowed doors enclose the van; the driver stays in the front cab.
 const wheelGeo=new T.CylinderGeometry(.33,.33,.18,quality<.5?10:16),rimGeo=new T.CylinderGeometry(.16,.16,.195,12),speakerGeo=new T.CylinderGeometry(.30,.35,.055,quality<.5?12:20),capGeo=new T.CylinderGeometry(.11,.11,.06,12),ringGeo=new T.TorusGeometry(.35,.025,5,quality<.5?12:24),steeringGeo=new T.TorusGeometry(.18,.025,5,12);
 const rubber=new T.MeshLambertMaterial({color:0x141923}),silver=new T.MeshLambertMaterial({color:0xb6c5d4}),coneMat=new T.MeshLambertMaterial({color:0x2c3547}),cyan=new T.MeshBasicMaterial({color:0x6af3e8}),pink=new T.MeshBasicMaterial({color:0xf386ff});
 const wheels:T.Group[]=[],cones:T.Mesh[]=[],rings:T.Mesh[]=[];
 const steering=new T.Mesh(steeringGeo,rubber);steering.rotation.y=Math.PI/2;steering.position.set(1.64,.32,.12);van.add(steering);
 for(const x of [-1.18,1.22])for(const side of [-1,1]){const wheel=new T.Group();wheel.position.set(x,-.52,side*.80);van.add(wheel);for(const [geo,mat]of [[wheelGeo,rubber],[rimGeo,silver]] as const){const m=new T.Mesh(geo,mat);m.rotation.x=Math.PI/2;wheel.add(m);}wheels.push(wheel);}
 for(const [i,x]of [-.93,.05].entries()){
  box('roof-speaker-cabinet',x,1.65,.06,.87,.82,.55,0x202a3b);
  const cone=new T.Mesh(speakerGeo,coneMat);cone.name='bass-speaker-cone';cone.rotation.x=Math.PI/2;cone.position.set(x,1.66,.369);van.add(cone);cones.push(cone);
  const cap=new T.Mesh(capGeo,silver);cap.position.y=.039;cone.add(cap);
  const ring=new T.Mesh(ringGeo,i?pink:cyan);ring.position.set(x,1.66,.375);van.add(ring);rings.push(ring);
 }
 box('boombox-handle',-.44,2.17,.04,1.1,.08,.12,trim);
 for(const x of [-.95,.07])box('handle-support',x,2.07,.04,.08,.18,.12,trim);
 const notes:T.Group[]=[];
 for(let i=0;i<(quality<.5?2:4);i++){
  const note=new T.Group();note.name='bass-music-note';const color=i%2?0xf386ff:0x6af3e8;
  rigBox(note,'note-head',-.055,0,0,.18,.12,.045,color);rigBox(note,'note-stem',.016,.16,0,.055,.35,.045,color);rigBox(note,'note-flag',.11,.30,0,.19,.065,.045,color);van.add(note);notes.push(note);
 }
 return {van,cargoDoor,cabDoor,wheels,cones,rings,notes,dispose(){for(const g of [wheelGeo,rimGeo,speakerGeo,capGeo,ringGeo,steeringGeo])g.dispose();for(const m of [rubber,silver,coneMat,cyan,pink,glass])m.dispose();}};
}
