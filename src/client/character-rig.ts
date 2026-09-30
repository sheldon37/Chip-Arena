import * as T from 'three';

/** Stable joint/socket names are the customization contract. Geometry is replaceable. */
export const RIG_VERSION = 2;
export const RIG_LENGTHS = { upperArm:.27, forearm:.25, thigh:.25, shin:.25 } as const;
const cube=new T.BoxGeometry(1,1,1),palette=new Map<string,T.MeshLambertMaterial>();
function mat(color:T.ColorRepresentation,glow=0,tint=.18){const key=String(color)+":"+glow+(tint===.18?"":":"+tint);if(!palette.has(key))palette.set(key,new T.MeshLambertMaterial({color,emissive:glow?new T.Color(color).lerp(new T.Color(0xc3e5ff),tint):0,emissiveIntensity:glow}));return palette.get(key)!;}
export function rigBox(parent:T.Object3D,name:string,x:number,y:number,z:number,sx:number,sy:number,sz:number,color:T.ColorRepresentation){const m=new T.Mesh(cube,mat(color));m.name=name;m.position.set(x,y,z);m.scale.set(sx,sy,sz);m.castShadow=m.receiveShadow=true;parent.add(m);return m;}
/** One part registry for both colour paths (glow creation and recolor), so a new part can never keep its creation colour. */
const BODY_PARTS=/^(hip-shell|abdomen-shell|ribcage|upper-chest|collar|blank-head|crown)$|-(upper-arm|forearm|thumb|thigh|calf|shin)$/,ACCENT_PARTS=/^(waist-band|sternum)$|-(shoulder-cap|cuff|kneecap)$/,EDGE_PARTS=/^(back-panel|jaw)$|-(palm|fingers|foot)$/,FLEX_PARTS=/^(abdomen|neck-joint)$|-(elbow-joint|hip-joint|knee-joint|heel)$/;
/** Arena glow: the whole shell (head included) is softly self-lit, accents keep their hue under ACES, and only the thin armor strips burn bright. */
const GLOW={body:.24,accent:1.1,accentTint:.05,strip:2};
/** Gloves, boots and joints follow the body colour (joints always darker than the shell), staying close to the classic grey/slate on the default body. */
const EDGE=new T.Color(0xb5c4ca),FLEX=new T.Color(0x354853),edgeOf=(body:T.ColorRepresentation)=>new T.Color(body).multiplyScalar(.6).lerp(EDGE,.1).getHex(),flexOf=(body:T.ColorRepresentation)=>{const shell=new T.Color(body),c=shell.clone().multiplyScalar(.08).lerp(FLEX,.6),lum=(x:T.Color)=>.2126*x.r+.7152*x.g+.0722*x.b;return (lum(c)<lum(shell)*.6?c:shell.multiplyScalar(.45)).getHex();};
/** Small boxes that only refine the silhouette: no shadow, hidden in low detail (SVG compatibility, 'performance'). */
const detail=(m:T.Mesh)=>{m.castShadow=false;m.userData.detail=true;return m;};
type Cover={mesh:T.Mesh;joint:T.Object3D;y:number;z:number};
const covers=new WeakMap<T.Object3D,Cover[]>();
function joint(parent:T.Object3D,name:string,x=0,y=0,z=0){const g=new T.Group();g.name=name;g.position.set(x,y,z);parent.add(g);return g;}
export function createCharacterRig(color:string,bodyColor:T.ColorRepresentation=0xdce5e8,glow=false){
 const group=new T.Group();group.name='blank-mannequin-v2';group.userData.rigVersion=RIG_VERSION;group.userData.armorGlow=glow;
 const body=joint(group,'motion-root'),pelvis=joint(body,'pelvis',0,-.24),spine=joint(pelvis,'spine',0,.105),chest=joint(spine,'chest',0,.19),neck=joint(chest,'neck',0,.32),head=joint(neck,'head',0,.10);
 const shell=bodyColor,edge=edgeOf(bodyColor),flex=flexOf(bodyColor),joints:Cover[]=[],cover=(mesh:T.Mesh,joint:T.Object3D)=>{joints.push({mesh,joint,y:mesh.position.y,z:mesh.position.z});return mesh;};
 rigBox(pelvis,'hip-shell',0,0,0,.43,.22,.28,shell);rigBox(pelvis,'waist-band',0,.08,0,.44,.055,.29,color);
 // The abdomen shell wraps the back flush with the hips and ribcage, so no dark band shows from the chase camera.
 rigBox(spine,'abdomen',0,.075,0,.35,.18,.25,flex);rigBox(spine,'abdomen-shell',0,.07,0,.36,.13,.275,shell);
 rigBox(chest,'ribcage',0,.10,0,.53,.30,.30,shell);rigBox(chest,'upper-chest',0,.235,0,.47,.075,.28,shell);detail(rigBox(chest,'collar',0,.29,-.01,.28,.05,.22,shell));rigBox(chest,'sternum',0,.13,.158,.075,.22,.022,color);
 rigBox(chest,'back-panel',0,.1,-.157,.30,.21,.025,edge);rigBox(neck,'neck-joint',0,.025,0,.15,.15,.15,flex);
 // Completely blank face: no visor, eyes, mouth, ears or permanent helmet.
 rigBox(head,'blank-head',0,.165,0,.34,.34,.32,shell);rigBox(head,'crown',0,.34,0,.30,.04,.28,shell);rigBox(head,'jaw',0,-.015,.01,.28,.04,.28,edge);
 const arm=(side:'left'|'right',sign:number)=>{const shoulder=joint(chest,side+'-clavicle',sign*.275,.18),upper=joint(shoulder,side+'-shoulder',sign*.07),elbow=joint(upper,side+'-elbow',0,-RIG_LENGTHS.upperArm),hand=joint(elbow,side+'-wrist',0,-RIG_LENGTHS.forearm);
  // A pauldron cap over the shoulder (no separate deltoid: one that turns with the arm only ever showed as a wedge through the raised cap); the arm tapers from shoulder to wrist and ends in a fist around the grip line.
  rigBox(shoulder,side+'-shoulder-cap',sign*.065,-.01,0,.19,.12,.21,color);rigBox(upper,side+'-upper-arm',0,-.15,0,.175,.23,.185,shell);cover(rigBox(elbow,side+'-elbow-joint',0,0,0,.14,.10,.145,flex),elbow);rigBox(elbow,side+'-forearm',0,-.13,0,.16,.20,.17,shell);rigBox(hand,side+'-cuff',0,.015,0,.175,.055,.19,color);rigBox(hand,side+'-palm',0,-.045,.005,.145,.10,.15,edge);detail(rigBox(hand,side+'-fingers',0,-.085,.07,.13,.075,.10,edge));rigBox(hand,side+'-thumb',-sign*.078,-.04,.035,.055,.08,.085,shell);
  return {shoulder,upper,elbow,hand,socket:joint(hand,side+'-hand-socket',0,-.065,.055)};};
 const left=arm('left',1),right=arm('right',-1);
 const leg=(side:'left'|'right',sign:number)=>{const hip=joint(pelvis,side+'-hip',sign*.13,-.055),knee=joint(hip,side+'-knee',0,-RIG_LENGTHS.thigh),ankle=joint(knee,side+'-ankle',0,-RIG_LENGTHS.shin),toe=joint(ankle,side+'-toe',0,-.02,.09);
  detail(rigBox(hip,side+'-hip-joint',0,-.02,0,.16,.12,.17,flex));rigBox(hip,side+'-thigh',0,-.13,0,.195,.23,.22,shell);cover(rigBox(knee,side+'-knee-joint',0,0,0,.17,.09,.18,flex),knee);cover(rigBox(knee,side+'-kneecap',0,-.01,.105,.16,.12,.075,color),knee);rigBox(knee,side+'-shin',0,-.14,0,.175,.22,.20,shell);detail(rigBox(knee,side+'-calf',0,-.095,-.10,.15,.13,.06,shell));rigBox(ankle,side+'-heel',0,-.025,-.025,.195,.09,.22,flex);rigBox(toe,side+'-foot',0,-.01,.045,.21,.08,.22,edge);return {hip,knee,ankle,toe};};
 const ll=leg('left',1),rl=leg('right',-1);covers.set(group,joints);const weapon=joint(right.socket,'weapon-socket'),offWeapon=joint(left.socket,'offhand-weapon-socket');
 const sockets={head:joint(head,'head-cosmetic'),chest:joint(chest,'chest-cosmetic'),back:joint(chest,'back-socket',0,.12,-.2),rightHand:right.socket,leftHand:left.socket,weapon,offWeapon};
 if(glow){
  rigBox(chest,'armor-back-light',0,.12,-.174,.24,.032,.015,color);
  for(const knee of [ll.knee,rl.knee])rigBox(knee,'armor-shin-light',0,-.15,.108,.035,.15,.018,color);
  lightArmor(group,color,bodyColor);
 }
 return {group,body,pelvis,spine,chest,neck,head,leftShoulder:left.shoulder,rightShoulder:right.shoulder,leftArm:left.upper,rightArm:right.upper,leftElbow:left.elbow,rightElbow:right.elbow,leftHand:left.hand,rightHand:right.hand,leftLeg:ll.hip,rightLeg:rl.hip,leftKnee:ll.knee,rightKnee:rl.knee,leftFoot:ll.ankle,rightFoot:rl.ankle,leftToe:ll.toe,rightToe:rl.toe,weapon,offWeapon,sockets,stride:0,motion:0,aimWeight:0,groundWeight:1,land:0,wasGrounded:true};
}
export type CharacterRig=ReturnType<typeof createCharacterRig>;
export {supportHand,braceWeapon} from './rig-ik.js';
/** Joint-cover hook, called after every full pose (animateCharacter, animateLobbyPose, PlayerIntros): the knee and elbow
 * covers turn half the hinge angle about the pivot, so they bisect the bend instead of opening a wedge. Joints are untouched. */
export function settleJointCovers(r:CharacterRig){const list=covers.get(r.group);if(!list)return;for(const c of list){const a=-.5*c.joint.rotation.x,cos=Math.cos(a),sin=Math.sin(a);c.mesh.rotation.x=a;c.mesh.position.y=c.y*cos-c.z*sin;c.mesh.position.z=c.y*sin+c.z*cos;}}
/** Level-of-detail hook: low=true hides optional detail meshes (SVG compatibility or 'performance' graphics). It only
 * restores meshes it hid itself, so parts another layer hides (the first-person torso) stay hidden. */
export function setRigDetail(r:CharacterRig,low:boolean){r.group.traverse(o=>{if(!(o instanceof T.Mesh)||!o.userData.detail)return;if(low){if(o.visible){o.visible=false;o.userData.lodHidden=true;}}else if(o.userData.lodHidden){o.visible=true;o.userData.lodHidden=false;}});}

export function recolorCharacterRig(r:CharacterRig,accent:string,body:string){const key=accent+body;if(r.group.userData.colorKey===key)return;r.group.userData.colorKey=key;paintRig(r.group,accent,body,!!r.group.userData.armorGlow);}

function lightArmor(group:T.Group,accent:string,body:T.ColorRepresentation){paintRig(group,accent,body,true);}
function paintRig(group:T.Group,accent:T.ColorRepresentation,body:T.ColorRepresentation,glow:boolean){const edge=edgeOf(body),flex=flexOf(body);group.traverse(o=>{if(!(o instanceof T.Mesh))return;const n=o.name;
 if(BODY_PARTS.test(n))o.material=mat(body,glow?GLOW.body:0);else if(ACCENT_PARTS.test(n))o.material=glow?mat(accent,GLOW.accent,GLOW.accentTint):mat(accent);else if(n.startsWith('armor-'))o.material=mat(accent,GLOW.strip,GLOW.accentTint);else if(EDGE_PARTS.test(n))o.material=mat(edge);else if(FLEX_PARTS.test(n))o.material=mat(flex);});}
