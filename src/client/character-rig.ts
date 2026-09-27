import * as T from 'three';

/** Stable joint/socket names are the customization contract. Geometry is replaceable. */
export const RIG_VERSION = 2;
export const RIG_LENGTHS = { upperArm:.27, forearm:.25, thigh:.25, shin:.25 } as const;
const cube=new T.BoxGeometry(1,1,1),palette=new Map<string,T.MeshLambertMaterial>();
function mat(color:T.ColorRepresentation,glow=0){const key=String(color)+":"+glow;if(!palette.has(key))palette.set(key,new T.MeshLambertMaterial({color,emissive:glow?new T.Color(color).lerp(new T.Color(0xc3e5ff),.18):0,emissiveIntensity:glow}));return palette.get(key)!;}
export function rigBox(parent:T.Object3D,name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,color:T.ColorRepresentation){const m=new T.Mesh(cube,mat(color));m.name=name;m.position.set(x,y,z);m.scale.set(sx,sy,sz);m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
function joint(parent:T.Object3D,name:string,x=0,y=0,z=0){const g=new T.Group();g.name=name;g.position.set(x,y,z);parent.add(g);return g;}
export function createCharacterRig(color:string,bodyColor:T.ColorRepresentation=0xdce5e8,glow=false){
 const group=new T.Group();group.name='blank-mannequin-v2';group.userData.rigVersion=RIG_VERSION;group.userData.armorGlow=glow;
 const body=joint(group,'motion-root'),pelvis=joint(body,'pelvis',0,-.24),spine=joint(pelvis,'spine',0,.105),chest=joint(spine,'chest',0,.19),neck=joint(chest,'neck',0,.32),head=joint(neck,'head',0,.10);
 const shell=bodyColor,edge=0xb5c4ca,flex=0x354853;
 rigBox(pelvis,'hip-shell',0,0,0,.43,.22,.28,shell);rigBox(pelvis,'waist-band',0,.08,0,.44,.055,.29,color);
 rigBox(spine,'abdomen',0,.075,0,.35,.18,.25,flex);rigBox(spine,'abdomen-shell',0,.07,.04,.36,.12,.23,shell);
 rigBox(chest,'ribcage',0,.10,0,.53,.30,.30,shell);rigBox(chest,'upper-chest',0,.235,0,.47,.075,.28,shell);rigBox(chest,'sternum',0,.13,.158,.075,.22,.022,color);
 rigBox(chest,'back-panel',0,.1,-.157,.30,.21,.025,edge);rigBox(neck,'neck-joint',0,.035,0,.14,.12,.14,flex);
 // Completely blank face: no visor, eyes, mouth, ears or permanent helmet.
 rigBox(head,'blank-head',0,.165,0,.34,.34,.32,shell);rigBox(head,'crown',0,.34,0,.30,.04,.28,shell);rigBox(head,'jaw',0,-.015,.01,.28,.04,.28,edge);
 const arm=(side:'left'|'right',sign:number)=>{const shoulder=joint(chest,side+'-clavicle',sign*.275,.18),upper=joint(shoulder,side+'-shoulder',sign*.07),elbow=joint(upper,side+'-elbow',0,-RIG_LENGTHS.upperArm),hand=joint(elbow,side+'-wrist',0,-RIG_LENGTHS.forearm);
  rigBox(shoulder,side+'-shoulder-cap',sign*.065,-.025,0,.20,.14,.22,color);rigBox(upper,side+'-upper-arm',0,-.15,0,.155,.23,.17,shell);rigBox(elbow,side+'-elbow-joint',0,0,0,.14,.10,.145,flex);rigBox(elbow,side+'-forearm',0,-.13,0,.17,.20,.18,shell);rigBox(hand,side+'-cuff',0,.015,0,.175,.055,.19,color);rigBox(hand,side+'-palm',0,-.055,.005,.155,.13,.16,edge);rigBox(hand,side+'-thumb',-sign*.078,-.04,.035,.055,.08,.085,shell);
  return {shoulder,upper,elbow,hand,socket:joint(hand,side+'-hand-socket',0,-.065,.055)};};
 const left=arm('left',1),right=arm('right',-1);
 const leg=(side:'left'|'right',sign:number)=>{const hip=joint(pelvis,side+'-hip',sign*.13,-.055),knee=joint(hip,side+'-knee',0,-RIG_LENGTHS.thigh),ankle=joint(knee,side+'-ankle',0,-RIG_LENGTHS.shin),toe=joint(ankle,side+'-toe',0,-.02,.09);
  rigBox(hip,side+'-thigh',0,-.13,0,.195,.23,.22,shell);rigBox(knee,side+'-knee-joint',0,0,0,.17,.09,.18,flex);rigBox(knee,side+'-kneecap',0,-.01,.105,.16,.12,.075,color);rigBox(knee,side+'-shin',0,-.14,0,.175,.22,.20,shell);rigBox(ankle,side+'-heel',0,-.025,-.025,.195,.09,.22,flex);rigBox(toe,side+'-foot',0,-.01,.045,.21,.08,.22,edge);return {hip,knee,ankle,toe};};
 const ll=leg('left',1),rl=leg('right',-1),weapon=joint(right.socket,'weapon-socket'),offWeapon=joint(left.socket,'offhand-weapon-socket');
 const sockets={head:joint(head,'head-cosmetic'),chest:joint(chest,'chest-cosmetic'),back:joint(chest,'back-socket',0,.12,-.2),rightHand:right.socket,leftHand:left.socket,weapon,offWeapon};
 if(glow){
  rigBox(chest,'armor-back-light',0,.12,-.174,.24,.032,.015,color);
  for(const knee of [ll.knee,rl.knee])rigBox(knee,'armor-shin-light',0,-.15,.108,.035,.15,.018,color);
  lightArmor(group,color,bodyColor);
 }
 return {group,body,pelvis,spine,chest,neck,head,leftShoulder:left.shoulder,rightShoulder:right.shoulder,leftArm:left.upper,rightArm:right.upper,leftElbow:left.elbow,rightElbow:right.elbow,leftHand:left.hand,rightHand:right.hand,leftLeg:ll.hip,rightLeg:rl.hip,leftKnee:ll.knee,rightKnee:rl.knee,leftFoot:ll.ankle,rightFoot:rl.ankle,leftToe:ll.toe,rightToe:rl.toe,weapon,offWeapon,sockets,stride:0,motion:0,aimWeight:0,groundWeight:1,land:0,wasGrounded:true};
}
export type CharacterRig=ReturnType<typeof createCharacterRig>;
const down=new T.Vector3(0,-1,0);
/** Analytic two-link arm IK. Targets come from weapon sockets, never per-item frame offsets. */
export function supportHand(r:CharacterRig,target:T.Object3D,weight:number){
 if(weight<=.001)return;r.group.updateMatrixWorld(true);
 const parent=r.leftArm.parent!,start=r.leftArm.position.clone(),end=parent.worldToLocal(target.getWorldPosition(new T.Vector3()));
 const delta=end.clone().sub(start),d=T.MathUtils.clamp(delta.length(),.045,RIG_LENGTHS.upperArm+RIG_LENGTHS.forearm-.002),dir=delta.normalize();
 end.copy(start).addScaledVector(dir,d);
 const a=RIG_LENGTHS.upperArm,b=RIG_LENGTHS.forearm,along=(a*a-b*b+d*d)/(2*d),height=Math.sqrt(Math.max(0,a*a-along*along));
 const pole=new T.Vector3(1,-.8,-.25);pole.addScaledVector(dir,-pole.dot(dir)).normalize();const elbow=start.clone().addScaledVector(dir,along).addScaledVector(pole,height);
 const upper=new T.Quaternion().setFromUnitVectors(down,elbow.clone().sub(start).normalize());const lower=new T.Quaternion().setFromUnitVectors(down,end.clone().sub(elbow).normalize().applyQuaternion(upper.clone().invert()));
 r.leftArm.quaternion.slerp(upper,weight);r.leftElbow.quaternion.slerp(lower,weight);r.group.updateMatrixWorld(true);
 const wrist=target.getWorldQuaternion(new T.Quaternion()).premultiply(r.leftHand.parent!.getWorldQuaternion(new T.Quaternion()).invert());r.leftHand.quaternion.slerp(wrist,weight);
}

/** Drive the main grip in chest space, solve both arms, then preserve the desired weapon orientation. */
export function braceWeapon(r:CharacterRig,target:T.Vector3,rotation:T.Euler,weight:number){
 if(weight<.001)return;r.group.updateMatrixWorld(true);
 const parent=r.rightArm.parent!,start=r.rightArm.position.clone(),end=parent.worldToLocal(r.chest.localToWorld(target.clone())),delta=end.clone().sub(start),d=T.MathUtils.clamp(delta.length(),.045,RIG_LENGTHS.upperArm+RIG_LENGTHS.forearm-.002),dir=delta.normalize();end.copy(start).addScaledVector(dir,d);
 const a=RIG_LENGTHS.upperArm,b=RIG_LENGTHS.forearm,along=(a*a-b*b+d*d)/(2*d),height=Math.sqrt(Math.max(0,a*a-along*along)),pole=new T.Vector3(-1,-.7,-.2);pole.addScaledVector(dir,-pole.dot(dir)).normalize();const elbow=start.clone().addScaledVector(dir,along).addScaledVector(pole,height),upper=new T.Quaternion().setFromUnitVectors(down,elbow.clone().sub(start).normalize()),lower=new T.Quaternion().setFromUnitVectors(down,end.clone().sub(elbow).normalize().applyQuaternion(upper.clone().invert()));
 r.rightArm.quaternion.slerp(upper,weight);r.rightElbow.quaternion.slerp(lower,weight);r.group.updateMatrixWorld(true);
 const orientation=r.chest.getWorldQuaternion(new T.Quaternion()).multiply(new T.Quaternion().setFromEuler(rotation)),wrist=r.rightHand.parent!.getWorldQuaternion(new T.Quaternion()).invert().multiply(orientation);r.rightHand.quaternion.slerp(wrist,weight);
 r.weapon.position.set(0,.065,-.055);r.weapon.rotation.set(0,0,0);r.group.updateMatrixWorld(true);const grip=r.weapon.getObjectByName('support-grip');if(grip)supportHand(r,grip,weight);
}

export function recolorCharacterRig(r:CharacterRig,accent:string,body:string){const key=accent+body;if(r.group.userData.colorKey===key)return;r.group.userData.colorKey=key;r.group.traverse(o=>{if(!(o instanceof T.Mesh))return;if(/^(hip-shell|abdomen-shell|ribcage|upper-chest|blank-head|crown)$/.test(o.name)||/-(upper-arm|forearm|thumb|thigh|shin)$/.test(o.name))o.material=mat(body,r.group.userData.armorGlow?.14:0);else if(/^(waist-band|sternum)$/.test(o.name)||/-(shoulder-cap|cuff|kneecap)$/.test(o.name))o.material=mat(accent,r.group.userData.armorGlow?.95:0);else if(o.name.startsWith('armor-'))o.material=mat(accent,.95);});}

function lightArmor(group:T.Group,accent:string,body:T.ColorRepresentation){group.traverse(o=>{if(!(o instanceof T.Mesh))return;if(/^(hip-shell|abdomen-shell|ribcage|upper-chest)$/.test(o.name)||/-(upper-arm|forearm|thigh|shin)$/.test(o.name))o.material=mat(body,.14);else if(/^(waist-band|sternum|armor-)/.test(o.name)||/-(shoulder-cap|cuff|kneecap)$/.test(o.name))o.material=mat(accent,.95);});}
