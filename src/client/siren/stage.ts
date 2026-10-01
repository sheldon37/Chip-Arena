import * as T from 'three';
import type {Snapshot} from '../../shared/simulation.js';
import {bossSongOf} from '../../shared/siren-contract.js';
import {BLACK_SIREN_PROTOCOL as TRACK,barAt,loopedSongTime,sectionAt,type BossSectionId} from '../../shared/boss-music.js';
import {SIREN_VENTS,sirenStageHazards} from '../../shared/siren-stage-layout.js';
import {FlameField} from './flames.js';
import {PALETTE,glowMaterial,haloMaterial,decalMaterial,tickSirenClock} from './materials.js';

const clamp=(x:number,a=0,b=1)=>Math.min(b,Math.max(a,x));
const sm=(x:number)=>{x=clamp(x);return x*x*(3-2*x);};
const mix=(a:number,b:number,t:number)=>a+(b-a)*t;
const unit=new T.BoxGeometry(1,1,1),dummy=new T.Object3D(),col=new T.Color();

/** How each section of the song dresses the ward. Values blend over about a bar when the section changes. */
interface Mood {tubes:number;strobe:number;erratic:number;beacons:number;spin:number;grins:number;eyes:number;circle:number;red:number;night:number}
const MOODS:Record<BossSectionId,Mood>={
 build:   {tubes:.45,strobe:0, erratic:.8,beacons:.15,spin:.5, grins:.1,eyes:0, circle:.25,red:0,  night:.6},
 groove:  {tubes:.9, strobe:0, erratic:.1,beacons:.45,spin:.9, grins:.35,eyes:0, circle:.45,red:.1, night:.6},
 lift:    {tubes:.95,strobe:.25,erratic:.1,beacons:1,  spin:3.2,grins:.8,eyes:0, circle:.75,red:.35,night:.55},
 breather:{tubes:.18,strobe:0, erratic:1, beacons:0,  spin:.3, grins:.2,eyes:1, circle:.3,red:0,  night:.45},
 climax:  {tubes:1,  strobe:1, erratic:0, beacons:1,  spin:5.5,grins:1, eyes:1, circle:1,  red:.6, night:.5},
 outro:   {tubes:.5, strobe:0, erratic:.5,beacons:.2, spin:.6, grins:.3,eyes:.4,circle:.3,red:.1, night:.6},
};
const KEYS=Object.keys(MOODS.build) as (keyof Mood)[];

/** A large painted grin (the Siren's mark), as flat triangles in the XY plane facing +z. */
function grinGeometry(w:number,h:number,teeth=13){
 const up=(x:number)=>h*.55+h*.9*(x/w)**2,lo=(x:number)=>-h*.45+h*1.5*(x/w)**2,pos:number[]=[],step=2*w/teeth;
 for(let i=0;i<teeth;i++){const a=-w+i*step,b=a+step,m=(a+b)/2;pos.push(a,up(a),0,m,lo(m)+(up(m)-lo(m))*.18,0,b,up(b),0);}
 for(let i=0;i<teeth-1;i++){const a=-w+(i+.5)*step,b=a+step,m=(a+b)/2;pos.push(a,lo(a),0,b,lo(b),0,m,up(m)-(up(m)-lo(m))*.18,0);}
 const g=new T.BufferGeometry();g.setAttribute('position',new T.Float32BufferAttribute(pos,3));g.setAttribute('uv',new T.Float32BufferAttribute(new Array(pos.length/3*2).fill(0),2));g.computeVertexNormals();return g;
}

/**
 * Ward Zero: a condemned containment ward in lockdown. Ceiling tube lights, red siren beacons on every wall, painted
 * grins and tally marks, a containment circle on the floor, drain vents that spit black fire, and a pair of giant
 * eyes that open on the back wall when the song drops out. Everything is driven by song time (the boss track's
 * detected beats and sections), so it pulses with the music and is identical for every viewer.
 */
export class WardZeroVisuals {
 readonly group=new T.Group();private active=false;private saved=new Map<T.Light,number>();private mood:Mood={...MOODS.build};private last=0;
 private tubes!:T.InstancedMesh;private tubeLevel:number[]=[];private tubeLights:T.PointLight[]=[];
 private beacons:{head:T.Group;beam:T.Mesh;lamp:T.ShaderMaterial;spot?:T.SpotLight;phase:number}[]=[];
 private grins:{mesh:T.Mesh;mat:T.ShaderMaterial;big:boolean}[]=[];private eyes:{mesh:T.Mesh;mat:T.ShaderMaterial}[]=[];private eyeHalos:T.Sprite[]=[];
 private circle:{ring:T.ShaderMaterial;ticks:T.InstancedMesh;tickMat:T.ShaderMaterial;runes:T.Group};
 private vents:{id:string;grate:T.Group;glow:T.ShaderMaterial;acc:number;burst:boolean}[]=[];
 private scanned=-1e9;private fire=new FlameField(700);private redWash=new T.HemisphereLight(0xff2a3a,0x000000,0);private fill=new T.HemisphereLight(0x8d86a6,0x1a1720,0);private ceiling!:T.Group;
 constructor(private scene:T.Scene){
  this.group.name='ward-zero';this.group.visible=false;scene.add(this.group);this.group.add(this.fire.mesh,this.redWash,this.fill);
  this.buildShell();this.buildCladding();this.buildTubes();this.buildBeacons();this.buildGraffiti();this.circle=this.buildCircle();this.buildVents();
 }

 // ---- construction ---------------------------------------------------------------------------------------------
 private box(w:number,h:number,d:number,x:number,y:number,z:number,m:T.Material,parent:T.Object3D=this.group){const b=new T.Mesh(unit,m);b.scale.set(w,h,d);b.position.set(x,y,z);parent.add(b);return b;}
 /** Ceiling grid, wall trim, padded cells, tally marks, warning stripes. */
 private buildShell(){
  const ceiling=new T.Group();ceiling.name='ward-ceiling';this.group.add(ceiling);this.ceiling=ceiling;
  const beam=new T.MeshLambertMaterial({color:0x121016}),pad=new T.MeshLambertMaterial({color:0x2c2833}),trim=new T.MeshLambertMaterial({color:0x26222d}),stripe=new T.MeshLambertMaterial({color:0x6b1520});
  for(let x=-28;x<=28;x+=8)this.box(.45,.6,56,x,9.2,0,beam,ceiling);for(let z=-24;z<=24;z+=12)this.box(64,.4,.35,0,9.0,z,beam,ceiling);
  // A dark skirting band and a pale scuffed dado line around the walls.
  for(const s of [-1,1]){this.box(.12,.5,56,s*31.94,.25,0,trim);this.box(64,.5,.12,0,.25,s*27.94,trim);this.box(.06,.06,56,s*31.96,1.3,0,pad);this.box(64,.06,.06,0,1.3,s*27.96,pad);}
  // Quilted padding on the inside of every cell.
  const quilts=new T.InstancedMesh(unit,pad,4*7*5);let n=0;
  for(const s of [-1,1])for(const z of [-11,11])for(let i=0;i<7;i++)for(let j=0;j<5;j++){dummy.position.set(s*29.36,.45+j*.66,z-2.4+i*.8);dummy.scale.set(.06,.6,.74);dummy.rotation.set(0,0,0);dummy.updateMatrix();quilts.setMatrixAt(n++,dummy.matrix);}
  quilts.count=n;quilts.instanceMatrix.needsUpdate=true;quilts.name='ward-quilts';this.group.add(quilts);
  // Tally marks scratched into the walls in clusters of five, as if counted by someone locked in here for a long time.
  const scratch=new T.MeshLambertMaterial({color:0x7a7389}),marks=new T.InstancedMesh(unit,scratch,900);let k=0;
  const tally=(x:number,y:number,z:number,ry:number,groups:number)=>{for(let g=0;g<groups;g++)for(let i=0;i<5;i++){if(k>=900)return;const a=g*.42+i*.06;
   dummy.position.set(x+Math.cos(ry)*a,y+(g%3)*.48+(i===4?0:0),z-Math.sin(ry)*a);dummy.rotation.set(0,ry,i===4?1.15:(Math.random()-.5)*.12);dummy.scale.set(.02,i===4?.42:.3,.012);dummy.updateMatrix();marks.setMatrixAt(k++,dummy.matrix);}};
  tally(-26,1.6,-27.92,0,14);tally(14,1.8,-27.92,0,10);tally(-8,1.5,27.92,Math.PI,12);tally(31.92,1.7,-20,-Math.PI/2,9);tally(-31.92,1.6,18,Math.PI/2,11);
  for(const s of [-1,1])for(const z of [-11,11])tally(s*29.32,1.2,z-2.2,s>0?-Math.PI/2:Math.PI/2,6);
  marks.count=k;marks.instanceMatrix.needsUpdate=true;marks.name='ward-tally';this.group.add(marks);
  // Hazard stripes on the pylons at floor level.
  for(const x of [-6,6])for(const z of [-6,6])for(let i=0;i<4;i++){const a=i*Math.PI/2;this.box(i%2?1.56:.08,.18,i%2?.08:1.56,x+Math.sin(a)*.78,.35,z+Math.cos(a)*.78,stripe);}
 }
 /** Concrete cladding over the indestructible blocks: walls get tiled panels, pylons and partitions get trim bands. */
 private buildCladding(){
  const concrete=new T.MeshLambertMaterial({color:0x3b3842}),seam=new T.MeshLambertMaterial({color:0x24212a}),band=new T.MeshLambertMaterial({color:0x4a4653});
  for(const s of [-1,1]){this.box(.05,4.6,56,s*31.97,2.8,0,concrete);this.box(64,4.6,.05,0,2.8,s*27.97,concrete);}
  const seams=new T.InstancedMesh(unit,seam,400);let n=0;const add=(x:number,y:number,z:number,sx:number,sy:number,sz:number)=>{if(n>=400)return;dummy.position.set(x,y,z);dummy.rotation.set(0,0,0);dummy.scale.set(sx,sy,sz);dummy.updateMatrix();seams.setMatrixAt(n++,dummy.matrix);};
  for(const s of [-1,1]){for(let z=-26;z<=26;z+=2)add(s*31.94,2.8,z,.02,4.6,.04);for(let y=1.6;y<=4.8;y+=1.6)add(s*31.94,y,0,.02,.04,56);for(let x=-30;x<=30;x+=2)add(x,2.8,s*27.94,.04,4.6,.02);for(let y=1.6;y<=4.8;y+=1.6)add(0,y,s*27.94,64,.04,.02);}
  seams.count=n;seams.instanceMatrix.needsUpdate=true;seams.name='ward-seams';this.group.add(seams);
  for(const x of [-6,6])for(const z of [-6,6]){this.box(1.56,.12,1.56,x,1.2,z,band);this.box(1.56,.12,1.56,x,3.6,z,band);this.box(1.56,.3,1.56,x,4.95,z,band);}
  for(const [x,z,w,d] of [[14,0,1,6],[-14,0,1,6],[0,12.5,7,1],[0,-12.5,7,1],[13,12,5,1],[-13,12,5,1],[13,-12,5,1],[-13,-12,5,1],[15,10,1,4],[-15,10,1,4],[15,-10,1,4],[-15,-10,1,4]])this.box(w+.04,.14,d+.04,x,3.0,z,band);
 }
 /** Rows of fluorescent tubes under the ceiling grid; two lights carry their glow into the room. */
 private buildTubes(){
  const rows=[-18,-6,6,18],per=6;this.tubes=new T.InstancedMesh(unit,new T.MeshBasicMaterial({color:0xffffff,toneMapped:false}),rows.length*per);this.tubes.instanceColor=new T.InstancedBufferAttribute(new Float32Array(rows.length*per*3),3);
  let i=0;for(const x of rows)for(let j=0;j<per;j++){dummy.position.set(x,7.6,-20+j*8);dummy.rotation.set(0,0,0);dummy.scale.set(.16,.1,2.4);dummy.updateMatrix();this.tubes.setMatrixAt(i,dummy.matrix);this.tubes.setColorAt(i,col.setScalar(1));this.tubeLevel.push(1);i++;
   this.box(.04,1.3,.04,x,8.3,-20+j*8-1,new T.MeshLambertMaterial({color:0x111111}));}
  this.tubes.name='ward-tubes';this.group.add(this.tubes);
  for(const x of [-11,11]){const l=new T.PointLight(0xdfe6ff,0,44,1.25);l.position.set(x,7,0);this.group.add(l);this.tubeLights.push(l);}
 }
 /** A red rotating siren beacon on each wall; the two side ones also sweep real light across the room. */
 private buildBeacons(){
  const spots:[number,number,number,number][]=[[-31.6,4.4,0,Math.PI/2],[31.6,4.4,0,-Math.PI/2],[0,4.4,-27.6,0],[0,4.4,27.6,Math.PI]];
  const housing=new T.MeshLambertMaterial({color:0x1b1a1f}),beamGeo=new T.ConeGeometry(1.6,9,24,1,true);beamGeo.translate(0,-4.5,0);beamGeo.rotateX(-Math.PI/2);
  spots.forEach(([x,y,z,ry],i)=>{const base=new T.Group();base.position.set(x,y,z);base.rotation.y=ry;this.group.add(base);
   this.box(.6,.25,.5,0,-.2,0,housing,base);const lamp=glowMaterial(PALETTE.warn,{intensity:.4});const dome=new T.Mesh(new T.SphereGeometry(.24,16,10,0,Math.PI*2,0,Math.PI/2),lamp);base.add(dome);
   const head=new T.Group();base.add(head);const beamMat=glowMaterial(PALETTE.warn,{transparent:true,opacity:.0,additive:true,side:T.DoubleSide});const beam=new T.Mesh(beamGeo,beamMat);beam.position.y=.08;head.add(beam);
   const b:WardZeroVisuals['beacons'][number]={head,beam,lamp,phase:i*1.7};
   if(i<2){const spot=new T.SpotLight(0xff2436,0,46,.32,.5,1.2);spot.position.set(0,.1,0);const target=new T.Object3D();target.position.set(0,-.3,4);head.add(spot,target);spot.target=target;b.spot=spot;}
   this.beacons.push(b);});
 }
 /** Grins painted on the walls, a giant one with a pair of giant eyes on the back wall, and "YOU" scrawled above the cells. */
 private buildGraffiti(){
  const paint=(w:number,h:number,x:number,y:number,z:number,ry:number,big=false)=>{const mat=glowMaterial(PALETTE.teeth,{intensity:.15});const m=new T.Mesh(grinGeometry(w,h,big?17:11),mat);m.position.set(x,y,z);m.rotation.y=ry;this.group.add(m);this.grins.push({mesh:m,mat,big});return m;};
  paint(5.5,.75,0,3.1,-27.88,0,true);paint(3.4,.5,0,3.0,27.88,Math.PI,true);
  paint(1.3,.2,-31.88,2.6,-6,Math.PI/2);paint(1.3,.2,31.88,2.6,6,-Math.PI/2);paint(1.1,.18,-31.88,2.2,22,Math.PI/2);paint(1.1,.18,31.88,2.2,-22,-Math.PI/2);
  for(const s of [-1,1])for(const z of [-11,11])paint(.8,.13,s*24.6,2.9,z,s>0?-Math.PI/2:Math.PI/2);
  // Giant eyes over the back-wall grin: closed slits most of the song, wide open and staring in the breakdown.
  const eye=new T.CircleGeometry(1,40);
  for(const s of [-1,1]){const mat=glowMaterial(0xffffff,{intensity:1});const m=new T.Mesh(eye,mat);m.position.set(s*2.3,4.35,-27.86);m.scale.set(1,.02,1);m.rotation.z=s*.12;this.group.add(m);this.eyes.push({mesh:m,mat});
   const halo=new T.Sprite(haloMaterial(0xf6f2ff,0));halo.position.set(s*2.3,4.35,-27.4);halo.scale.setScalar(4.5);this.group.add(halo);this.eyeHalos.push(halo);}
  // YOU, scrawled in strokes above the north cells.
  const ink=new T.MeshLambertMaterial({color:0x8d8699});const stroke=(x0:number,y0:number,x1:number,y1:number,z:number)=>{const L=Math.hypot(x1-x0,y1-y0),m=this.box(.12,L,.03,(x0+x1)/2,(y0+y1)/2,z,ink);m.rotation.z=Math.atan2(-(x1-x0),y1-y0);};
  const z=27.9,X=6;stroke(X+1.6,4.3,X+1.2,3.7,z);stroke(X+.8,4.3,X+1.2,3.7,z);stroke(X+1.2,3.7,X+1.2,3.1,z);
  stroke(X+.4,4.2,X+.4,3.2,z);stroke(X-.4,4.2,X-.4,3.2,z);stroke(X+.4,4.2,X-.4,4.2,z);stroke(X+.4,3.2,X-.4,3.2,z);
  stroke(X-.9,4.3,X-.9,3.3,z);stroke(X-1.7,4.3,X-1.7,3.3,z);stroke(X-.9,3.3,X-1.7,3.2,z);
 }
 /** The containment circle in the middle of the floor: rings, ticks and rune marks. */
 private buildCircle(){
  const ring=glowMaterial(0xe8e2ff,{intensity:.2,side:T.DoubleSide});for(const [a,b] of [[4.3,4.45],[5.2,5.32],[2.2,2.28]]){const m=new T.Mesh(new T.RingGeometry(a,b,96),ring);m.rotation.x=-Math.PI/2;m.position.y=.02;this.group.add(m);}
  const tickMat=glowMaterial(0xe8e2ff,{intensity:.2});const ticks=new T.InstancedMesh(unit,tickMat,48);
  for(let i=0;i<48;i++){const a=i/48*Math.PI*2,long=i%4===0;dummy.position.set(Math.sin(a)*4.85,.021,Math.cos(a)*4.85);dummy.rotation.set(0,a,0);dummy.scale.set(.05,.004,long?.7:.35);dummy.updateMatrix();ticks.setMatrixAt(i,dummy.matrix);}
  ticks.instanceMatrix.needsUpdate=true;ticks.name='ward-ticks';this.group.add(ticks);
  const runes=new T.Group();for(let i=0;i<12;i++){const a=i/12*Math.PI*2;for(let j=0;j<3;j++){const r=this.box(.05,.004,.28-(j%2)*.1,Math.sin(a)*3.3+(j-1)*.12,.022,Math.cos(a)*3.3,ring,runes);r.rotation.y=a+(j-1)*.6;}}
  this.group.add(runes);
  const soot=new T.Mesh(new T.CircleGeometry(6.2,48),decalMaterial(0x000000,.35));soot.rotation.x=-Math.PI/2;soot.position.y=.012;this.group.add(soot);
  return {ring,ticks,tickMat,runes};
 }
 /** Floor grates over the vents: a red rim that fills as the vent arms, black fire when it erupts. */
 private buildVents(){
  const iron=new T.MeshLambertMaterial({color:0x17151b});
  for(const v of SIREN_VENTS){const g=new T.Group();g.position.set(v.x,.015,v.z);this.group.add(g);
   for(let i=0;i<6;i++)this.box(2.2,.03,.1,0,0,-1+i*.4,iron,g);this.box(.1,.03,2.3,-1.12,0,0,iron,g);this.box(.1,.03,2.3,1.12,0,0,iron,g);this.box(2.34,.03,.1,0,0,1.12,iron,g);
   const glow=glowMaterial(PALETTE.warn,{transparent:true,opacity:0,side:T.DoubleSide});const rim=new T.Mesh(new T.RingGeometry(1.15,1.4,4,1),glow);rim.rotation.set(-Math.PI/2,0,Math.PI/4);rim.position.y=.01;g.add(rim);
   const pit=new T.Mesh(new T.PlaneGeometry(2.1,2.1),new T.MeshBasicMaterial({color:0x000000}));pit.rotation.x=-Math.PI/2;pit.position.y=-.005;g.add(pit);
   this.vents.push({id:v.id,grate:g,glow,acc:0,burst:false});}
 }

 // ---- per frame --------------------------------------------------------------------------------------------------
 setActive(on:boolean){
  if(on===this.active)return;this.active=on;this.group.visible=on;
  if(!on){for(const [l,i] of this.saved)l.intensity=i;this.saved.clear();this.fire.clear();this.scanned=-1e9;}
 }
 private own(o:T.Object3D){for(let x:T.Object3D|null=o;x;x=x.parent)if(x===this.group)return true;return false;}

 update(state:Snapshot,time:number,quality:number,_camera?:T.Camera){
  if(!this.active)return;tickSirenClock(time);const dt=clamp(time-this.last,0,.05);this.last=time;
  const song=bossSongOf(state),raw=song?state.time-song.startedAt:-1,t=song?loopedSongTime(TRACK,Math.max(0,raw)):0;
  const pos=song&&raw>=0?barAt(TRACK,t):{bar:0,beat:1,phase:time%1,index:-1},section:BossSectionId=song&&raw>=0?sectionAt(TRACK,t):'build';
  // Ease toward the section's mood (about a bar).
  const target=MOODS[section],k=1-Math.exp(-dt*2.2);for(const key of KEYS)this.mood[key]+=(target[key]-this.mood[key])*k;const m=this.mood;
  const beatPulse=(1-pos.phase)**3,down=pos.beat===1?beatPulse:0,q=quality<.5?.5:1;

  // The arena's own night lights are mostly shut off in here: the ward is lit by its own tubes and beacons.
  // (Lights are re-collected about once a second rather than walking the scene every frame.)
  if(time-this.scanned>1||time<this.scanned){this.scanned=time;this.scene.traverse(o=>{if(o instanceof T.Light&&!this.own(o)&&o.name!=='armor-light'&&!this.saved.has(o))this.saved.set(o,o.intensity);});}
  for(const [l,base] of this.saved)l.intensity=base*m.night;

  // Tubes: steady, pulsing with the beat, strobing in the climax, failing at random in the quiet parts.
  let sum=0;for(let i=0;i<this.tubeLevel.length;i++){const seed=i*12.9898,fail=m.erratic*(Math.sin(time*(3+i%5)+seed)>.55-.4*m.erratic?1:0)*(Math.sin(time*23+seed*3)>0?1:.2);
   const strobe=m.strobe>0?mix(1,pos.phase<.45?1:.08,m.strobe):1,level=clamp(m.tubes*(1-.9*fail)*strobe*(.82+.18*beatPulse));this.tubeLevel[i]=level;sum+=level;
   this.tubes.setColorAt(i,col.setRGB(level*.95,level*.97,level));}
  if(this.tubes.instanceColor)this.tubes.instanceColor.needsUpdate=true;const avg=sum/this.tubeLevel.length;for(const l of this.tubeLights)l.intensity=avg*190;

  // Beacons: spin with the section, flash on downbeats.
  for(const b of this.beacons){b.head.rotation.y=time*m.spin+b.phase;const bright=m.beacons*(.6+.4*down);b.lamp.uniforms.uIntensity.value=.25+1.4*bright;(b.beam.material as T.ShaderMaterial).uniforms.uOpacity.value=.16*bright*q;if(b.spot)b.spot.intensity=bright*260;}
  this.redWash.intensity=m.red*(.15+.25*down);this.fill.intensity=.25+.35*m.tubes;
  // The ceiling grid would block the spectator and overview cameras: draw it only from below.
  if(_camera)this.ceiling.visible=_camera.position.y<8.6;

  // Painted grins glow on the beat; the big ones swell on downbeats.
  for(const g of this.grins){g.mat.uniforms.uIntensity.value=.08+m.grins*(g.big?.55+.45*down:.35+.4*beatPulse);}
  // Giant eyes open in the breakdown and climax, with a slow blink every two bars.
  const blink=pos.bar%2===0&&pos.beat===4&&pos.phase>.6?0:1;for(const [i,e] of this.eyes.entries()){const open=Math.max(.02,m.eyes*blink);e.mesh.scale.set(1.1,1.5*open,1);e.mat.uniforms.uIntensity.value=.4+.8*m.eyes;(this.eyeHalos[i].material as T.SpriteMaterial).opacity=.35*m.eyes*blink;}
  // Containment circle: a ring of light that throbs with the music.
  this.circle.ring.uniforms.uIntensity.value=.08+m.circle*(.3+.7*down);this.circle.tickMat.uniforms.uIntensity.value=.05+m.circle*(.2+.5*beatPulse);this.circle.runes.rotation.y=time*.05*(1+m.spin*.3);

  // Vents: read the same schedule the simulation damages with.
  const hazards=song&&raw>=0?sirenStageHazards(raw):[];
  for(const v of this.vents){const h=hazards.find(x=>x.id===v.id);const warn=h?.warning??0,on=!!h?.active;
   v.glow.uniforms.uOpacity.value=on?.95:warn*.9;v.glow.uniforms.uColor.value.setHex(on?0xffffff:PALETTE.warn);
   if(warn>0&&Math.random()<dt*q*20*warn)this.fire.emit(v.grate.position.x+(Math.random()-.5)*2,.05,v.grate.position.z+(Math.random()-.5)*2,0,.5+warn,0,.4,.06,'black',1.5,2);
   if(on){if(!v.burst){v.burst=true;this.fire.burst(v.grate.position.x,.1,v.grate.position.z,Math.round(28*q),2,.6,.14,'black',4,1);}
    v.acc+=dt*q*110;while(v.acc>=1){v.acc-=1;const a=Math.random()*Math.PI*2,r=Math.random()*1;this.fire.emit(v.grate.position.x+Math.cos(a)*r,.05,v.grate.position.z+Math.sin(a)*r,(Math.random()-.5)*.5,6+Math.random()*4,(Math.random()-.5)*.5,.38+Math.random()*.2,.12+Math.random()*.1,Math.random()<.06?'ember':Math.random()<.2?'smoke':'black',-3,1.4);}}
   else v.burst=false;}
  this.fire.update(dt);
 }
}
