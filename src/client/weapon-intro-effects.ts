import * as T from 'three';
import {WEAPON_INTROS,type WeaponIntro} from '../shared/weapon-intros.js';
import type {CharacterRig} from './character-rig.js';
import {poseWeaponIntro} from './weapon-intro-motion.js';
import {ChipBlasts,blastTint,type Tint} from './chip-blasts.js';
/**
 * Weapon-intro effects (owner request 2026-10-04: the weapon entrances looked dated). Everything is drawn with the pooled
 * volumetric voxel family (ChipBlasts: InstancedMesh voxels, the same blast styles as the abilities) instead of flat
 * rings, cones, planes and line bolts. Determinism: the voxel pools are stepped at a fixed 60 Hz from age 0 and every
 * spawn comes from a reseeded generator, so any age (a late join, a lab strip, a scrub backwards) shows exactly what an
 * uninterrupted playback would; a backwards jump rewinds the pools and replays. Anchors (muzzle, blade tip, hands) are
 * read from the choreography sampled at each step's time, then the rig is re-posed at the present age.
 * No per-frame allocation in steady playback: scratch vectors are preallocated and the pools have fixed capacity.
 */
export const INTRO_FX_STEP=1/60;
const STEP=INTRO_FX_STEP,G=-.05,AT=G+.95;// floor height; blasts take the centre-of-mass height (floor + .95)
const fade=(age:number,at:number,seconds:number)=>Math.max(0,1-Math.abs(age-at)/seconds);
/** Small deterministic generator (reseeded on rewind). */
function rng(seed:number){let s=seed>>>0||1;return()=>{s=(Math.imul(s,1664525)+1013904223)>>>0;return s/4294967296;};}
const SHIELD:Tint={core:0xffffff,hot:0xbfe4ff,cool:0x5aa6ff},EMBER=0xffd08a,ASH=0x3a3d44,SMOKE=0x9a9ea6,BRASS=0xd9ac64,VOID_DARK=0x12081f,VOID_DEEP=0x5a2496;
/** Belt Rattle rounds: 24 Hz through the authored burst (1.93-2.33 s), matching the muzzle-flash flicker. */
const BELT=Array.from({length:10},(_,i)=>1.93+i/24);
export class WeaponIntroEffects{
 readonly root=new T.Group();readonly blasts:ChipBlasts;private ownBlasts:boolean;private materials=new Set<T.Material>();private ghosts:T.Group[]=[];private ghostSources:T.Object3D[]=[];
 private steps=0;private random=rng(0x1e7a);private posedAt=NaN;private trail=false;private offTrail=false;
 private muzzleNode?:T.Object3D;private tipNode?:T.Object3D;private offTipNode?:T.Object3D;
 // Scratch anchors (world space), refreshed by anchor(t).
 private muzzle=new T.Vector3();private tip=new T.Vector3();private offTip=new T.Vector3();private hand=new T.Vector3();private pelvis=new T.Vector3();private dir=new T.Vector3();private fwd=new T.Vector3();private right=new T.Vector3();private lastTip=new T.Vector3();private lastOffTip=new T.Vector3();private p=new T.Vector3();private p2=new T.Vector3();private q=new T.Quaternion();
 constructor(private style:WeaponIntro,r:CharacterRig,blasts?:ChipBlasts){this.root.name='weapon-entrance';const spec=WEAPON_INTROS[style].spec;
  this.ownBlasts=!blasts;this.blasts=blasts??new ChipBlasts(this.root);this.blasts.reset();
  this.muzzleNode=r.weapon.getObjectByName('muzzle');this.tipNode=r.weapon.getObjectByName('blade-tip');this.offTipNode=r.offWeapon.getObjectByName('blade-tip');
  if(spec==='daggers'){r.group.traverse(o=>this.ghostSources.push(o));for(let i=0;i<3;i++){const ghost=r.group.clone(true);ghost.name='phantom-afterimage';ghost.traverse(o=>{if(o instanceof T.Mesh){const m=new T.MeshBasicMaterial({color:WEAPON_INTROS[style].color,transparent:true,opacity:.16,depthWrite:false,toneMapped:false});this.materials.add(m);o.material=m;}});this.root.add(ghost);this.ghosts.push(ghost);}}
 }
 /** Seconds of choreography the voxel pools have been stepped through. */
 get clock(){return this.steps*STEP;}
 update(age:number,r:CharacterRig,quality:number,compat=false){age=Math.max(0,age);
  if(age<this.clock-1e-6){this.blasts.reset();this.steps=0;this.random=rng(0x1e7a);this.trail=this.offTrail=false;}
  const k=Math.max(0,Math.min(1,quality))*(compat?.5:1);this.posedAt=NaN;let posed=false;
  while((this.steps+1)*STEP<=age+1e-9){this.emit(this.steps*STEP,r,k,compat);posed||=this.posedAt===this.posedAt;this.blasts.update(STEP);this.steps++;}
  // Afterimages are the same choreography sampled a few frames in the past (a real motion trail along the path), then the
  // rig is re-posed at the present age; everything stays a pure function of age.
  if(this.ghosts.length){const live=age>.35&&age<2.7&&quality>.3;this.ghosts.forEach((ghost,i)=>{ghost.visible=live;if(!live)return;posed=true;poseWeaponIntro(r,this.style,age-(i+1)*.065);let n=0;ghost.traverse(o=>{const source=this.ghostSources[n++];o.position.copy(source.position);o.quaternion.copy(source.quaternion);o.scale.copy(source.scale);if(o instanceof T.Mesh)(o.material as T.MeshBasicMaterial).opacity=.16*(1-i*.25)*fade(age,1.5,1.2);});});}
  if(posed)poseWeaponIntro(r,this.style,age);
 }
 /** Pose the rig at `t` and read the world anchors (only for steps that need them). */
 private anchor(r:CharacterRig,t:number){if(this.posedAt===t)return;poseWeaponIntro(r,this.style,t);r.group.updateMatrixWorld(true);this.posedAt=t;
  (this.muzzleNode??r.rightHand).getWorldPosition(this.muzzle);(this.tipNode??r.rightHand).getWorldPosition(this.tip);(this.offTipNode??r.leftHand).getWorldPosition(this.offTip);r.rightHand.getWorldPosition(this.hand);r.pelvis.getWorldPosition(this.pelvis);
  this.dir.set(0,0,1).applyQuaternion(r.weapon.getWorldQuaternion(this.q));const yaw=r.group.rotation.y;this.fwd.set(Math.sin(yaw),0,Math.cos(yaw));this.right.set(-Math.cos(yaw),0,Math.sin(yaw));}
 // ---- voxel helpers (all spawn into the shared pools) ----
 /** Directional muzzle blast: a stretched cone of glow voxels along `d`. */
 private cone(p:T.Vector3,d:T.Vector3,count:number,speed:number,spread:number,life:number,size:number,c0:number,c1:number){const rnd=this.random;for(let i=0;i<count;i++){const s=speed*(.45+rnd()*.75);this.blasts.glow.spawn(p.x,p.y,p.z,d.x*s+(rnd()-.5)*spread*speed,d.y*s+(rnd()-.5)*spread*speed,d.z*s+(rnd()-.5)*spread*speed,life*(.7+rnd()*.6),size,size*.3,c0,c1,0,6,0,-1e9,2.6);}}
 /** Slow muzzle smoke: opaque grey voxels that rise, drift and grow. */
 private smoke(p:T.Vector3,count:number){const rnd=this.random;for(let i=0;i<count;i++)this.blasts.solid.spawn(p.x+(rnd()-.5)*.05,p.y+(rnd()-.5)*.05,p.z+(rnd()-.5)*.05,(rnd()-.5)*.25,.3+rnd()*.3,(rnd()-.5)*.25,.9+rnd()*.5,.03,.13+rnd()*.05,SMOKE,ASH,-.15,1.4,0,-1e9,1,.8);}
 /** A brass casing kicked out to the right; it falls, bounces on the floor and tumbles. */
 private casing(p:T.Vector3){const rnd=this.random,R=this.right;this.blasts.solid.spawn(p.x,p.y,p.z,R.x*(1.4+rnd())-this.fwd.x*.3,1.6+rnd()*.8,R.z*(1.4+rnd())-this.fwd.z*.3,1.3,.035,.03,BRASS,0x8a6a3a,9.8,.2,0,G+.02,2.2,9);}
 /** Glow voxels on the segment a->b (a blade's swept path between two steps). */
 private sweep(a:T.Vector3,b:T.Vector3,count:number,life:number,size:number,c0:number,c1:number){for(let i=0;i<count;i++){const u=(i+.5)/count;this.blasts.glow.spawn(a.x+(b.x-a.x)*u,a.y+(b.y-a.y)*u,a.z+(b.z-a.z)*u,0,0,0,life,size,size*.25,c0,c1);}}
 /** Blasts and impacts are drawn in miniature for the close intro cameras (voxel sizes x.45; the radius sets the reach). */
 private mini(on:boolean,scale=.45){this.blasts.solid.sizeScale=this.blasts.glow.sizeScale=on?scale:1;}
 /** Kicked-up floor dust in a ring around (x,z). */
 private dust(x:number,z:number,count:number,radius:number,speed:number){const rnd=this.random;for(let i=0;i<count;i++){const a=rnd()*Math.PI*2;this.blasts.solid.spawn(x+Math.cos(a)*radius,G+.04,z+Math.sin(a)*radius,Math.cos(a)*speed,.3+rnd()*.5,Math.sin(a)*speed,.7,.06,.13,0x6b6e76,0x2a2c32,1.5,2,0,G+.02,1,2);}}
 /** One fixed step of the choreography's effects, from `t` to `t+STEP`. `k` is the effects quality (0..1). */
 private emit(t:number,r:CharacterRig,k:number,compat:boolean){
  const W=WEAPON_INTROS[this.style],spec=W.spec,hit=W.hit,color=W.color,once=(at:number)=>at>=t-1e-9&&at<t+STEP-1e-9,n=(b:number)=>Math.max(1,Math.round(b*k)),rnd=this.random,glow=this.blasts.glow,solid=this.blasts.solid,live=k>.05,B=this.blasts,o={quality:k,compat};
  const chance=(rate:number)=>live&&rnd()<rate*STEP*k;
  if(spec==='pistol'||spec==='rifle'||spec==='machinegun'){
   const heavy=spec==='machinegun',rifle=spec==='rifle';
   // Pistol twirl: the steel catches the light in small voxel glints.
   if(spec==='pistol'&&t>.5&&t<1.05&&chance(40)){this.anchor(r,t);glow.spawn(this.muzzle.x,this.muzzle.y,this.muzzle.z,0,.2,0,.16,.07,.01,0xffffff,color);}
   // Longwatch: a red sight beam (a short-lived voxel line along the bore) while the shot is lined up.
   if(rifle&&t>1.2&&t<hit&&live&&this.steps%2===0){this.anchor(r,t);this.p.copy(this.muzzle).addScaledVector(this.dir,3.6);B.line(this.muzzle,this.p,0xffd2cc,0xff5047,k*.7,0,STEP*2.4);}
   // Anticipation: a breath-hold glint at the muzzle.
   if(once(hit-.25)){this.anchor(r,t);for(let i=0;i<n(6);i++){const a=i/6*Math.PI*2;glow.spawn(this.muzzle.x,this.muzzle.y,this.muzzle.z,Math.cos(a)*.5,Math.sin(a)*.5,0,.18,.06,.01,0xffffff,color);}}
   const shots=heavy?BELT.length:1;
   for(let i=0;i<shots;i++)if(once(heavy?BELT[i]:hit)){this.anchor(r,t);const last=i===shots-1,size=rifle?.15:heavy?.1:.12;
    this.cone(this.muzzle,this.dir,n(rifle?22:heavy?8:14),rifle?11:7,.35,.16,size,0xffffff,color);
    if(!heavy||i%2===0){this.p.copy(this.muzzle).addScaledVector(this.dir,rifle?12:7);B.line(this.muzzle,this.p,0xffffff,color,k,.02,rifle?.2:.12,.06);}
    if(rifle){// muzzle-brake side jets and a dust kick at the feet from the recoil
     for(const side of [-1,1]){this.p.copy(this.right).multiplyScalar(side);this.cone(this.muzzle,this.p,n(6),4,.3,.14,.08,0xffffff,color);}this.dust(this.pelvis.x,this.pelvis.z,n(10),.3,1.2);}
    if(i===0||last)this.mini(true);B.impact(this.muzzle,rifle?.5:.32,color,{quality:k*(heavy?.5:.8),compat});this.mini(false);
    this.casing(this.muzzle);if(last)this.smoke(this.muzzle,n(heavy?6:4));}
   // Barrel smoke after the shot (heavier after the belt).
   if(t>hit+.08&&t<hit+(heavy?1.6:1.1)&&chance(heavy?14:8)){this.anchor(r,t);this.smoke(this.muzzle,1);}
   return;}
  if(spec==='sword'||spec==='daggers'||spec==='greatsword'){
   // Anticipation: motes drawn onto the edge before the cut.
   if(spec!=='greatsword'&&t>hit-.5&&t<hit-.1&&chance(36)){this.anchor(r,t);const a=rnd()*Math.PI*2,b=rnd()*Math.PI-Math.PI/2,R=.45,x=Math.cos(a)*Math.cos(b),y=Math.sin(b),z=Math.sin(a)*Math.cos(b);glow.spawn(this.tip.x+x*R,this.tip.y+y*R,this.tip.z+z*R,-x*R/.25,-y*R/.25,-z*R/.25,.25,.05,.03,color,0xffffff);}
   // Blade trails: the tip's swept path between steps, a fading ribbon of glow voxels (dagger spin: both blades).
   const from=spec==='daggers'?.9:spec==='sword'?hit-.14:hit-.3,to=hit+(spec==='daggers'?.28:.3);
   if(t>from&&t<to&&live){this.anchor(r,t);if(this.trail)this.sweep(this.lastTip,this.tip,spec==='greatsword'?4:3,.2,spec==='greatsword'?.11:.075,0xffffff,color);this.lastTip.copy(this.tip);this.trail=true;
    if(spec==='daggers'){if(this.offTrail)this.sweep(this.lastOffTip,this.offTip,3,.18,.07,0xffffff,color);this.lastOffTip.copy(this.offTip);this.offTrail=true;}}else this.trail=this.offTrail=false;
   // Greatsword dragged on the walk-in: sparks where the tip scrapes the floor.
   if(spec==='greatsword'&&t<1.1&&live){this.anchor(r,t);if(this.tip.y<G+.22&&rnd()<.6*k)glow.spawn(this.tip.x,G+.04,this.tip.z,-this.fwd.x*1.5+(rnd()-.5),.8+rnd()*1.4,-this.fwd.z*1.5+(rnd()-.5),.3,.045,.015,0xffffff,EMBER,9,.5,0,G+.02,2.5);}
   if(once(hit)){this.anchor(r,t);
    if(spec==='sword'){// Flash Draw: a voxel crescent that sweeps across the cut, an edge pop and sparks.
     const yaw=Math.atan2(this.fwd.x,this.fwd.z),c=n(34);for(let i=0;i<c;i++){const u=i/Math.max(1,c-1),a=yaw+1.15-2.3*u,R=1.0+.08*Math.sin(Math.PI*u),y=1.0+.25*(u-.5),x=Math.sin(a),z=Math.cos(a);glow.spawn(this.pelvis.x+x*R,y,this.pelvis.z+z*R,x*1.6,0,z*1.6,.28,.04+.08*(1-Math.abs(u-.5)),.02,0xffffff,color,0,2,u*.08);}
     this.mini(true);B.impact(this.tip,.5,color,{quality:k,compat});this.mini(false);}
    else if(spec==='daggers'){// Twin Phantom: an X of voxel cuts in front of the chest.
     for(const s of [-1,1]){this.p.copy(this.pelvis).addScaledVector(this.fwd,.55).addScaledVector(this.right,.45*s);this.p.y+=.85;this.p2.copy(this.pelvis).addScaledVector(this.fwd,.55).addScaledVector(this.right,-.45*s);this.p2.y-=.1;B.line(this.p,this.p2,0xffffff,color,k,.03,.3,.06);}
     this.p.copy(this.pelvis).addScaledVector(this.fwd,.55);this.p.y+=.38;this.mini(true);B.impact(this.p,.6,color,{quality:k,compat});this.mini(false);}
    else{// Colossus Oath: the blade bites the floor; a quake blast, ember cracks racing outward, debris.
     this.p.set(this.tip.x,AT,this.tip.z);this.mini(true);B.blast('quake',this.p,1.6,o);this.mini(false);this.mini(true);B.impact(this.p,.9,EMBER,{quality:k,compat,ground:true,cool:0xff6a2a});this.mini(false);
     const lines=n(8);for(let l=0;l<lines;l++){const a=l/lines*Math.PI*2+rnd()*.3;let x=this.tip.x,z=this.tip.z;for(let s=0;s<9;s++){x+=Math.cos(a)*.18+(rnd()-.5)*.08;z+=Math.sin(a)*.18+(rnd()-.5)*.08;glow.spawn(x,G+.03,z,0,0,0,.95-s*.04,.08,.05,EMBER,0xff6a2a,0,0,s*.025);}}}}
   return;}
  if(spec==='shield'){
   if(once(1.0)){this.anchor(r,t);this.dust(this.pelvis.x,this.pelvis.z,n(10),.25,.9);}
   if(once(hit)){this.anchor(r,t);// Bastion Drop: the rim hits the floor (kinetic blast) and a voxel honeycomb barrier ripples out from the impact.
    this.p.copy(this.pelvis).addScaledVector(this.fwd,.4);this.p.y=AT;this.mini(true);B.blast('kinetic',this.p,1.4,{...o,tint:SHIELD});this.mini(false);
    const cx=this.pelvis.x+this.fwd.x*.55,cz=this.pelvis.z+this.fwd.z*.55,cy=1.0,cell=.2*(k<.5?1.5:1),edge=1.15,sector=Math.PI/3;
    for(let a=-8;a<=8;a++)for(let b=-8;b<=8;b++){const u=(a+b*.5)*cell,v=b*cell*.866,d=Math.hypot(u,v),ang=Math.atan2(v,u),wrap=((ang%sector)+sector)%sector,limit=edge*Math.cos(Math.PI/6)/Math.cos(wrap-Math.PI/6);if(d>limit)continue;const rim=d>limit-cell;
     glow.spawn(cx+this.right.x*u,cy+v,cz+this.right.z*u,this.fwd.x*.4,0,this.fwd.z*.4,rim?.55:.4,rim?.09:.06,.02,rim?0xffffff:SHIELD.hot,SHIELD.cool,0,0,d*.22);}}
   return;}
  // Magic: the free (right) hand casts; the staff stays in the left hand (as in the in-game Q/E casting).
  if(spec==='fire'){
   // Cinder Crown: flame voxels gather in the casting hand, then a flame lance and a fire blast in front.
   if(t>.4&&t<hit&&live){this.anchor(r,t);const charge=Math.min(1,(t-.4)/(hit-.6));if(rnd()<(.3+charge*.7)*k)solid.spawn(this.hand.x+(rnd()-.5)*.08,this.hand.y+.02,this.hand.z+(rnd()-.5)*.08,(rnd()-.5)*.3,.8+charge*1.2,(rnd()-.5)*.3,.3+charge*.15,.05+charge*.07,.02,rnd()<.35?0xffe28a:0xffb340,0xd8401c,-.4,.5,0,-1e9,1,3);if(chance(18))glow.spawn(this.hand.x,this.hand.y,this.hand.z,(rnd()-.5)*.6,1.4+rnd(),(rnd()-.5)*.6,.5,.035,.01,0xfff6d2,0xffb340);}
   if(once(hit)){this.anchor(r,t);this.p.copy(this.pelvis).addScaledVector(this.fwd,1.7);this.p.y=AT;B.line(this.hand,this.p,0xffffff,0xffc23a,k,.1,.22,.08);this.mini(true,.35);B.blast('fire',this.p,.75,o);this.mini(false);}
   if(t>hit&&t<hit+1.6&&chance(10)){this.anchor(r,t);const a=rnd()*Math.PI*2,R=.5+rnd()*.7;glow.spawn(r.group.position.x+Math.cos(a)*R,G+.05,r.group.position.z+Math.sin(a)*R,0,.9+rnd()*.6,0,.7,.045,.015,0xffe28a,0xd8401c);}
   return;}
  if(spec==='water'){
   // Tidal Serpent: two voxel serpents spiral up around the fighter, then dive into a tidal splash on the hit.
   if(t<hit+.3&&live){this.anchor(r,t);const x0=r.group.position.x,z0=r.group.position.z;for(let i=0;i<2;i++){const rise=Math.min(1,Math.max(0,(t-.1)/(hit-.1))),dive=Math.max(0,Math.min(1,(t-hit)/.3)),u=rise*rise*(3-2*rise),a=u*Math.PI*3+i*Math.PI+t*(i?-.8:.8),R=.75+u*.2+dive*.3,y=.15+u*1.7-dive*1.75,x=x0+Math.cos(a)*R,z=z0+Math.sin(a)*R;
     glow.spawn(x,y,z,0,0,0,.42,.06+.05*k,.02,i?0xc1f6ff:0xeaffff,color);if(rnd()<.15*k)solid.spawn(x,y,z,(rnd()-.5)*.4,.2,(rnd()-.5)*.4,.8,.05,.03,0xbff7ff,0x2b8bd6,9.8,0,0,G+.02);}}
   if(once(hit)){this.anchor(r,t);this.p.set(this.pelvis.x,AT,this.pelvis.z);this.mini(true);B.blast('kinetic',this.p,1.4,{...o,tint:blastTint('tidal-wave')});this.mini(false);
    for(let i=0;i<n(24);i++){const a=rnd()*Math.PI*2,R=.6+rnd()*.5;solid.spawn(this.pelvis.x+Math.cos(a)*R,G+.1,this.pelvis.z+Math.sin(a)*R,Math.cos(a)*.6,2.5+rnd()*2,Math.sin(a)*.6,1.1,.07,.04,0xeaffff,0x2b8bd6,9.8,.2,rnd()*.08,G+.02,1.6);}}
   return;}
  if(spec==='lightning'){
   const root=()=>{this.anchor(r,t);return r.group.position;};
   // Thunderstep: the landing spot crackles while the fighter drops, a voxel trail follows the fall, then a shock blast
   // (with its sky bolt) lands exactly on the hit; the staff head keeps arcing for a beat.
   if(t>.3&&t<hit&&chance(26)){const g=root(),a=rnd()*Math.PI*2,R=.4+rnd()*.6;glow.spawn(g.x+Math.cos(a)*R,G+.06,g.z+Math.sin(a)*R,-Math.cos(a)*R*2,1.5+rnd()*2,-Math.sin(a)*R*2,.22,.07,.02,0xffffff,0x8fb8ff);}
   if(t>hit-.4&&t<hit&&live){this.anchor(r,t);for(let i=0;i<2;i++)glow.spawn(this.pelvis.x+(rnd()-.5)*.3,this.pelvis.y+(rnd()-.3)*.6,this.pelvis.z+(rnd()-.5)*.3,0,1.2,0,.2,.09,.02,0xffffff,color);}
   if(once(hit)){const g=root();this.p.set(g.x,AT,g.z);this.mini(true);B.blast('shock',this.p,1.3,o);this.mini(false);}
   if(t>hit+.05&&t<hit+1.2&&live&&this.steps%5===0&&rnd()<.8*k){this.anchor(r,t);this.p.set(this.muzzle.x+(rnd()-.5)*.7,this.muzzle.y+(rnd()-.3)*.5,this.muzzle.z+(rnd()-.5)*.7);B.line(this.muzzle,this.p,0xffffff,color,k,.1,.07);}
   return;}
  if(spec==='darkness'){
   // Eventide Gate: a portal of dark voxels with a swirling violet rim opens behind the fighter, a void blast is cast
   // forward on the hit, and the gate implodes shut.
   const open=T.MathUtils.smoothstep(t,0,1),close=1-T.MathUtils.smoothstep(t,2.6,3.15),s=open*close,cx=0,cy=1.15,cz=-.6,RX=1.25*s,RY=1.5*open;
   if(s>.02&&live){for(let i=0,c=n(4);i<c;i++){const a=rnd()*Math.PI*2,d=Math.sqrt(rnd());solid.spawn(cx+Math.cos(a)*d*RX,cy+Math.sin(a)*d*RY,cz,-Math.cos(a)*d*.3,-Math.sin(a)*d*.3,0,.38,.12,.3,d<.45?VOID_DEEP:VOID_DARK,0x07030f,0,0,0,-1e9,1,.5);}
    for(let i=0,c=n(5);i<c;i++){const a=rnd()*Math.PI*2,x=Math.cos(a),y=Math.sin(a);glow.spawn(cx+x*RX,cy+y*RY,cz+.02,-y*RX*2.2-x*.4,x*RY*2.2-y*.4,0,.45,.1,.03,0xd59bff,0x7a32d8);}}
   if(once(hit)){this.anchor(r,t);this.p.copy(this.hand).addScaledVector(this.fwd,.9);B.line(this.hand,this.p,0xf3e2ff,0xbc72ff,k,.08,.2,.05);this.mini(true);B.blast('void',this.p,1.0,o);this.mini(false);}
   if(once(2.95)){this.p.set(cx,cy,cz);this.mini(true);B.blast('implosion',this.p,.9,o);this.mini(false);}
  }
 }
 dispose(){if(this.ownBlasts)this.blasts.dispose();else this.blasts.reset();this.root.removeFromParent();this.materials.forEach(m=>m.dispose());}
}
