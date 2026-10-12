// The game's own class and weapon icons: original filled artwork authored here as geometry (not derived from any icon
// library), exported as SVG-subset markup for NativeIcons (Core/NativeIconRaster.cs, art mode) to
// Assets/Conversion/Resources/ClassIcons.json. One ink colour (white, tinted by the call site) on a 24-unit view box;
// a second tone is the same ink at reduced opacity; data-knockout="1" shapes erase what is drawn before them, cutting a
// clean gap where one part overlaps another. Icons with fine detail carry a simplified `small` drawing used at 22 px and
// below. Data only; deterministic. Usage: node tools/export_class_icons.mjs [--check | --preview <dir>]: --check only
// verifies the committed JSON is current; --preview also writes plain SVGs with masks standing in for the knockouts, for
// checking the art in a browser.
import fs from 'node:fs';import path from 'node:path';import {fileURLToPath} from 'node:url';
const project=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');

// ---- geometry: paths of M L Q C A Z with affine transforms (rotation keeps circular arcs; a mirror flips sweep) ----
const R=v=>{const r=Math.round(v*100)/100;return Object.is(r,-0)?0:r;};
class P {
  constructor(c=[]){this.c=c;}
  M(x,y){this.c.push(['M',[x,y]]);return this;} L(x,y){this.c.push(['L',[x,y]]);return this;}
  Q(cx,cy,x,y){this.c.push(['Q',[cx,cy],[x,y]]);return this;} C(ax,ay,bx,by,x,y){this.c.push(['C',[ax,ay],[bx,by],[x,y]]);return this;}
  A(r,large,sweep,x,y){this.c.push(['A',r,large,sweep,[x,y]]);return this;} Z(){this.c.push(['Z']);return this;}
  poly(pts){pts.forEach((p,i)=>i?this.L(p[0],p[1]):this.M(p[0],p[1]));return this.Z();}
  add(...others){for(const o of others)this.c.push(...o.c);return this;}
  map(f,flip=false,k=1){return new P(this.c.map(c=>c[0]==='A'?['A',c[1]*k,c[2],flip?1-c[3]:c[3],f(c[4])]:[c[0],...c.slice(1).map(f)]));}
  rot(deg,cx=12,cy=12){const a=deg*Math.PI/180,co=Math.cos(a),si=Math.sin(a);return this.map(([x,y])=>[cx+(x-cx)*co-(y-cy)*si,cy+(x-cx)*si+(y-cy)*co]);}
  shift(dx,dy){return this.map(([x,y])=>[x+dx,y+dy]);}
  scale(s,cx=12,cy=12){return this.map(([x,y])=>[cx+(x-cx)*s,cy+(y-cy)*s],false,s);}
  flipX(cx=12){return this.map(([x,y])=>[2*cx-x,y],true);}
  d(){return this.c.map(c=>c[0]==='Z'?'Z':c[0]==='A'?`A${R(c[1])} ${R(c[1])} 0 ${c[2]} ${c[3]} ${R(c[4][0])} ${R(c[4][1])}`:c[0]+c.slice(1).map(p=>R(p[0])+' '+R(p[1])).join(' ')).join('');}
}
const poly=pts=>new P().poly(pts);
const circ=(cx,cy,r)=>new P().M(cx-r,cy).A(r,1,1,cx+r,cy).A(r,1,1,cx-r,cy).Z();
const at=(cx,cy,r,deg)=>[cx+r*Math.cos(deg*Math.PI/180),cy+r*Math.sin(deg*Math.PI/180)];
// Inner circle wound the other way, so a ring needs no even-odd rule.
const circR=(cx,cy,r)=>new P().M(cx-r,cy).A(r,1,0,cx+r,cy).A(r,1,0,cx-r,cy).Z();
const ring=(cx,cy,r0,r1)=>circ(cx,cy,r1).add(circR(cx,cy,r0));
const star=(cx,cy,r0,r1,n=4,start=-90)=>poly(Array.from({length:n*2},(_,i)=>at(cx,cy,i%2?r0:r1,start+i*180/n)));
const crect=(x,y,w,h,c=0)=>c?poly([[x+c,y],[x+w-c,y],[x+w,y+c],[x+w,y+h-c],[x+w-c,y+h],[x+c,y+h],[x,y+h-c],[x,y+c]]):poly([[x,y],[x+w,y],[x+w,y+h],[x,y+h]]);
// A tapered stroke from a to b (widths wa, wb), as a quad.
const taper=(a,b,wa,wb)=>{const dx=b[0]-a[0],dy=b[1]-a[1],l=Math.hypot(dx,dy),nx=-dy/l,ny=dx/l;return poly([[a[0]+nx*wa/2,a[1]+ny*wa/2],[b[0]+nx*wb/2,b[1]+ny*wb/2],[b[0]-nx*wb/2,b[1]-ny*wb/2],[a[0]-nx*wa/2,a[1]-ny*wa/2]]);};

// ---- layers: {p, evenodd, opacity (the second tone), line (an open path drawn as a stroke this wide), knock (first
// erase the shape grown by this gap), cut (erase only: a negative-space cut, grown by knock if given)} ----
const GAP=1.3,TONE=.42;
const L=(p,o={})=>({p:Array.isArray(p)?p.reduce((a,b)=>a.add(b),new P()):p,...o});
const line=pts=>{const p=new P();pts.forEach((q,i)=>i?p.L(q[0],q[1]):p.M(q[0],q[1]));return p;};
function markup(layers)
{
  let s='';
  for(const l of layers)
  {
    const d=l.p.d(),rule=l.evenodd?' fill-rule="evenodd"':'',grow=l.knock||0;
    // Strokes are round-joined and round-capped, as NativeIconRaster always draws them (spelt out for other SVG renderers).
    const round=' stroke-linejoin="round" stroke-linecap="round"';
    const erase=l.line?`<path d="${d}" fill="none" stroke="#fff" stroke-width="${R(l.line+2*grow)}"${round} data-knockout="1"/>`
      :`<path d="${d}"${rule} fill="#fff"${grow?` stroke="#fff" stroke-width="${R(2*grow)}"${round}`:''} data-knockout="1"/>`;
    if(l.cut||grow)s+=erase;if(l.cut)continue;
    s+=`<path d="${d}"${rule} ${l.line?`fill="none" stroke="#fff" stroke-width="${R(l.line)}"${round}`:'fill="#fff"'}${l.opacity?` opacity="${l.opacity}"`:''}/>`;
  }
  return s;
}

// ---- the icons ----
const icons={};
// Sampled ellipse band (centre, radii, thickness t, local angles a0..a1, tilt), for orbits that pass in front of and
// behind what they circle.
const ellipseBand=(cx,cy,rx,ry,t,a0,a1,tilt=0,n=40)=>{const o=[],i=[];for(let k=0;k<=n;k++){const a=(a0+(a1-a0)*k/n)*Math.PI/180;o.push([cx+(rx+t/2)*Math.cos(a),cy+(ry+t/2)*Math.sin(a)]);i.push([cx+(rx-t/2)*Math.cos(a),cy+(ry-t/2)*Math.sin(a)]);}return poly([...o,...i.reverse()]).rot(tilt,cx,cy);};

// RANGED: a round driving into a scope reticle, its trail streaking out behind it.
icons['class-ranged']=()=>{
  const reticle=[ring(13.8,12,5.6,7.6),crect(12.7,1.2,2.2,3.6),crect(21,10.9,2.4,2.2),crect(12.7,19.2,2.2,3.6)];
  const round=poly([[5.6,10.1],[12.2,10.1],[14.4,10.8],[16.4,12],[14.4,13.2],[12.2,13.9],[5.6,13.9]]);
  const trail=[taper([4.6,12],[.4,12],2,.5),taper([5.2,8.7],[1.6,8.7],1.2,.3),taper([5.2,15.3],[1.6,15.3],1.2,.3)];
  return [L(reticle),L(round,{knock:GAP}),L(crect(7.3,9,.8,6),{cut:true}),L(trail,{opacity:TONE})];
};
// MELEE: two longswords crossed, a clash spark flaring between their points.
const blade=(w=3,len=23)=>{const t=12-len/2,g=t+len*.62,h=w/2;
  return [poly([[12,t],[12+h,t+4],[12+h,g],[12-h,g],[12-h,t+4]]),poly([[7.6,g],[16.4,g],[15.6,g+1.9],[8.4,g+1.9]]),crect(11.05,g+1.9,1.9,len*.38-4.6),poly([[12,12+len/2-2.8],[13.4,12+len/2-1.4],[12,12+len/2],[10.6,12+len/2-1.4]])];};
icons['class-melee']=()=>{
  const a=blade(2.8).map(p=>p.rot(45)),b=blade(2.8).map(p=>p.rot(-45));
  return [L(a),L(b,{knock:1.1}),L(star(12,4.6,.95,3.3),{knock:.6})];
};
// MAGIC: a faceted focus crystal held in a staff's claw, a glyph orbit passing behind and in front of it.
icons['class-magic']=()=>{
  const crystal=poly([[12,.8],[16,7.2],[12,15.4],[8,7.2]]),facet=poly([[12,2.8],[12.55,7.2],[12,12.6],[11.45,7.2]]);
  const claw=[poly([[12,18.6],[8.6,15.8],[8.2,12.4],[9.8,14.4],[12,16.2]]),poly([[12,18.6],[15.4,15.8],[15.8,12.4],[14.2,14.4],[12,16.2]])];
  const staff=poly([[10.95,17.6],[13.05,17.6],[13.05,22.6],[12,23.6],[10.95,22.6]]);
  const back=ellipseBand(12,8.4,9.8,2.6,1.3,180,360,-12),front=ellipseBand(12,8.4,9.8,2.6,1.3,0,180,-12);
  const glyphs=[star(19.8,1.9,.5,1.8),star(3.4,13.8,.45,1.4)];
  return [L(back),L(claw),L(staff),L(crystal,{knock:1.1}),L(facet,{cut:true}),L(front,{knock:1}),L(glyphs,{knock:.8})];
};
// DEV: a die with its pins inside a hex frame.
const hexP=(r,cx=12,cy=12)=>poly(Array.from({length:6},(_,i)=>at(cx,cy,r,-90+i*60)));
icons['class-dev']=()=>{
  const frame=hexP(11.4).add(hexP(9.2));
  const die=crect(7.7,7.7,8.6,8.6,1.6),core=crect(10.3,10.3,3.4,3.4,.5);
  const pins=[];for(const o of [-2.1,2.1]){pins.push(crect(12+o-.65,5.2,1.3,1.8),crect(12+o-.65,17,1.3,1.8),crect(5.2,12+o-.65,1.8,1.3),crect(17,12+o-.65,1.8,1.3));}
  return [L(frame,{evenodd:true}),L(pins),L(die.add(core),{evenodd:true})];
};

// PISTOL: a compact sidearm, muzzle right.
icons['spec-pistol']=()=>{
  const slide=poly([[3.4,5.6],[21,5.6],[22,6.6],[22,10.2],[3.4,10.2],[2.6,9.4],[2.6,6.4]]);
  const frame=poly([[8.4,10.2],[20.6,10.2],[20.6,12.2],[8.4,12.2]]);
  const guard=poly([[9.6,12.2],[15.2,12.2],[14.2,16.4],[9.6,16.4]]).add(poly([[11.6,12.2],[13.4,12.2],[12.8,14.8],[11.6,14.8]]));
  const grip=poly([[3.4,10.2],[10.2,10.2],[9.4,13.4],[7.6,20.8],[2.6,20.8],[2,19.8],[3.6,13.2]]);
  return [L(slide),L(frame),L(guard,{evenodd:true}),L(grip),L([0,1,2].map(i=>crect(4.3+i*1.45,5.6,.75,2.9)),{cut:true}),L(line([[8.4,10.2],[20.6,10.2]]),{cut:true,line:.5})];
};
// RIFLE: a long scoped rifle on the diagonal, muzzle top right.
icons['spec-rifle']=()=>{
  const body=[poly([[-15,1.2],[-14.2,-1],[-6.5,-1.8],[-4.5,-1.8],[-4.5,2],[-7.5,2.5],[-10.5,4.4],[-15,4.6]]),crect(-4.8,-2,10.2,3.8,.6),poly([[5.2,-1.25],[15.3,-1.25],[15.3,.75],[5.2,.95]]),crect(14.6,-1.9,2.3,3.2,.4),poly([[-1.6,1.7],[1.4,1.7],[1.7,5.4],[-.4,5.4]])];
  const scope=[crect(-3.6,-5.8,8.4,2.5,.6),poly([[4.4,-6.4],[7.3,-6.8],[7.3,-2.6],[4.4,-3.3]]),poly([[-3.4,-6.4],[-5.7,-6.7],[-5.7,-2.7],[-3.4,-3.1]]),crect(-.9,-3.5,.9,1.7),crect(2.1,-3.5,.9,1.7)];
  const place=p=>p.scale(.8,0,0).rot(-45,0,0).shift(12.2,11.8);
  return [L(body.map(place)),L(scope.map(place),{knock:.85})];
};
// MACHINE GUN: a belt-fed gun with a cooled barrel jacket, its ammo belt swinging down from the feed.
icons['spec-machinegun']=()=>{
  const receiver=poly([[4.2,5],[13.6,5],[14.4,5.8],[14.4,10.8],[4.2,10.8]]),stock=poly([[.6,5.8],[4.4,6.2],[4.4,10.2],[1.4,11],[.6,10.6]]);
  const jacket=crect(14.2,6,6.6,3.8,.5).add(...[0,1,2].map(i=>crect(15,7.1,1.1,1.6).shift(i*1.95,0)));
  const barrel=crect(20.6,7,3,1.8),handle=poly([[6.8,5],[7.8,2.8],[12.6,2.8],[13.4,5],[11.9,5],[11.5,4.1],[8.8,4.1],[8.3,5]]),grip=poly([[10.6,10.8],[13.4,10.8],[12.8,14.4],[10.6,14.4]]);
  // The belt hangs from the feed under the receiver: rounds stacked side by side, noses forward, swinging back as it falls.
  const round=poly([[-2.4,-.8],[.9,-.8],[2.4,0],[.9,.8],[-2.4,.8]]);
  const belt=[[7.6,12.6,4],[7.1,14.6,12],[6.2,16.5,22],[4.9,18.2,32],[3.3,19.6,42]].map(([x,y,a])=>round.shift(x,y).rot(a,x,y));
  return [L(stock),L(receiver),L(jacket,{evenodd:true}),L(barrel),L(handle),L(grip),L(belt,{knock:.5})];
};
// BOW: a recurve at full draw on the diagonal, the string pulled back to the nock and the arrow flying up and right.
const arrowShape=(x0,x1,y=12,w=1.1,head=2.6,fl=true)=>{const a=[crect(x0,y-w/2,x1-x0-head+.4,w),poly([[x1-head,y-1.9],[x1,y],[x1-head,y+1.9],[x1-head+.7,y]])];const f=fl?[poly([[x0,y-.4],[x0+3.2,y-.4],[x0+2.2,y-2.3],[x0-.6,y-2.3]]),poly([[x0,y+.4],[x0+3.2,y+.4],[x0+2.2,y+2.3],[x0-.6,y+2.3]])]:[];return {shaft:a,fletch:f};};
icons['spec-bow']=()=>{
  const limbs=ellipseBand(7.6,12,8.6,9.8,2.6,-78,78);
  const tips=[taper([9.6,2.4],[11.9,1.1],1.6,.5),taper([9.6,21.6],[11.9,22.9],1.6,.5)];
  const riser=crect(14.9,8.6,3.4,6.8,.8),grip=crect(15.9,10.2,1.4,3.6,.3);
  const string=line([[9.8,2.6],[3.6,12],[9.8,21.4]]);const ar=arrowShape(2.4,22.6);
  const place=p=>p.rot(-38,12,12);
  return [L([limbs,...tips,riser].map(place)),L(place(grip),{cut:true}),L(place(string),{line:.75,opacity:TONE}),L(ar.shaft.map(place),{knock:1}),L(ar.fletch.map(place),{opacity:TONE})];
};
// VOLLEY (the bow's Q): five arrows fanned out of one nock, flying up and right.
icons['ability-volley']=()=>{
  const out=[];for(let i=0;i<5;i++){const a=arrowShape(2.6,21.4,12,.95,2.4,false);out.push(...a.shaft.map(p=>p.rot(-12-i*16,3,20.5).shift(0,8.5)));}
  return [L(out.slice(0,6)),L(out.slice(6),{knock:.7}),L(circ(3.4,20.4,1.6),{opacity:TONE})];
};
// RAIN OF ARROWS (the bow's E): arrows falling steeply onto a marked circle.
icons['ability-rain']=()=>{
  const ringP=ellipseBand(12,19.6,9.4,3,1.4,0,360,0,56);const falls=[];
  for(const [x,y,len] of [[6.4,2.2,10.6],[12,.8,13],[17.6,2.6,10.2]]){const a=arrowShape(0,len,0,1,2.4,false);falls.push(...a.shaft.map(p=>p.rot(90,0,0).shift(x,y)));}
  return [L(ringP,{opacity:TONE}),L(falls,{knock:.8}),L([star(6.4,15.6,.45,1.4),star(17.6,15.2,.45,1.4)])];
};
// SWORD: a longsword with a winged crossguard, on the diagonal.
icons['spec-sword']=()=>{
  const s=[poly([[12,-1.4],[13.55,1.3],[13.55,14.6],[10.45,14.6],[10.45,1.3]]),poly([[6.4,14.4],[17.6,14.4],[16.6,16.5],[7.4,16.5]]),crect(11.05,16.5,1.9,4.2),poly([[12,20.2],[13.7,21.9],[12,23.6],[10.3,21.9]])];
  const place=p=>p.rot(45,12,11.2).shift(0,.6);
  return [L(s.map(place)),L(place(poly([[11.55,3.2],[12.45,3.2],[12.45,13],[11.55,13]])),{cut:true})];
};
// DAGGERS: two reverse-grip daggers crossed, blades down.
const dagger=()=>[poly([[10.3,10.6],[13.7,10.6],[13.9,18],[12,23.4],[10.1,18]]),poly([[8.2,9.2],[15.8,9.2],[14.9,10.8],[9.1,10.8]]),crect(11,4.4,2,4.8,.3),poly([[12,1],[13.6,2.6],[13,4.4],[11,4.4],[10.4,2.6]])];
icons['spec-daggers']=()=>{
  const a=dagger().map(p=>p.rot(34,12,12.6)),b=dagger().map(p=>p.flipX().rot(-34,12,12.6));
  const cuts=[...[6.2,7.6].map(y=>crect(10.6,y,2.8,.45).rot(34,12,12.6)),...[6.2,7.6].map(y=>crect(10.6,y,2.8,.45).rot(-34,12,12.6))];
  return [L(a),L(b,{knock:1.1}),L(cuts,{cut:true})];
};
// SHIELD: a heater shield, bevelled face, a sword crossed behind it.
icons['spec-shield']=()=>{
  const sw=[poly([[12,-2.4],[13.6,.6],[13.6,15.8],[10.4,15.8],[10.4,.6]]),poly([[6.2,15.6],[17.8,15.6],[16.8,17.9],[7.2,17.9]]),crect(10.95,17.9,2.1,3.3),poly([[12,20.6],[13.8,22.4],[12,24.2],[10.2,22.4]])].map(p=>p.rot(45,12,12));
  const sh=p=>p.scale(.84,12,3).shift(1.3,.6);
  const shield=sh(new P().M(5,4.6).L(12,3).L(19,4.6).L(19,11).C(19,15.8,16,19,12,21.4).C(8,19,5,15.8,5,11).Z());
  const bevel=sh(new P().M(12,5.6).L(16.6,6.6).L(16.6,10.9).C(16.6,14.3,14.8,16.8,12,18.6).Z());
  return [L(sw),L(shield,{knock:1.1}),L(bevel,{cut:true})];
};
// GREATSWORD: a zweihander, point up: a broad blade, a ricasso with parrying lugs, a long two-hand grip.
icons['spec-greatsword']=()=>{
  const bladeP=poly([[12,.2],[15.6,4.8],[15.6,12.4],[14.4,13.4],[9.6,13.4],[8.4,12.4],[8.4,4.8]]),fuller=crect(11.35,5.6,1.3,6.6);
  const ricasso=crect(10.4,13.4,3.2,2.2),lugs=[poly([[10.4,13.6],[7.6,12.6],[8.2,14.4],[10.4,15]]),poly([[13.6,13.6],[16.4,12.6],[15.8,14.4],[13.6,15]])];
  const guard=poly([[3.2,15.4],[20.8,15.4],[20,18.2],[18.6,17.4],[5.4,17.4],[4,18.2]]),grip=crect(10.95,17.4,2.1,4.4),pommel=poly([[12,21.2],[13.9,22.4],[12,23.8],[10.1,22.4]]);
  return [L(bladeP),L(fuller,{cut:true}),L(ricasso),L(lugs),L(guard),L(grip),L(crect(10.6,19.3,2.8,.5),{cut:true}),L(pommel)];
};
// FIRE: a faceted flame rising through a rune circle.
icons['spec-fire']=()=>{
  const circle=ring(12,13,8.2,10.2);
  const runes=[0,1,2,3,4,5].map(i=>{const a=-90+30+i*60,c=at(12,13,9.2,a);return crect(-.55,-1.4,1.1,2.8).shift(...c).rot(a+90,...c);});
  const flame=poly([[12,.8],[15.6,6.4],[16.2,4.4],[18.2,9.4],[17.6,14.2],[14.4,18.2],[9.6,18.2],[6.4,14.2],[5.8,9.4],[7.8,5.2],[8.6,7.8]]);
  const core=poly([[12,8.2],[14.4,12.2],[13.6,16.2],[10.4,16.2],[9.6,12.2]]);
  return [L(circle,{opacity:TONE}),L(runes,{cut:true}),L(flame,{knock:GAP}),L(core,{cut:true})];
};
// WATER: a droplet with a breaking wave curling through it.
icons['spec-water']=()=>{
  const drop=new P().M(12,1).C(12,1,19.8,9.4,19.8,14.6).C(19.8,19.2,16.3,22.8,12,22.8).C(7.7,22.8,4.2,19.2,4.2,14.6).C(4.2,9.4,12,1,12,1).Z();
  const crest=new P().M(2.6,17.6).C(6.4,13.2,11.2,10.8,15,12).C(18,13,17.8,16.8,15,17.1).C(13.2,17.3,12.3,15.8,13.3,14.5);
  return [L(drop),L(crest,{cut:true,line:1.5})];
};
// LIGHTNING: a forked bolt splitting a ring.
icons['spec-lightning']=()=>{
  const bolt=poly([[16.4,.4],[6,12.9],[11.2,12.9],[8.2,23.6],[12.7,17.2],[15.9,21.4],[14.9,14.4],[18,10.9],[12.9,10.9]]);
  return [L(ring(12,12,7.2,9.4)),L(bolt,{knock:GAP})];
};
// DARKNESS: an eclipse — a void ringed by a crescent corona, a slit eye in it — trailing tendrils.
icons['spec-darkness']=()=>{
  const corona=circ(12,10.8,8.6).add(circR(13.2,9.8,6.9));
  const tendrils=[new P().M(5.2,15.8).C(4.6,18.8,3.2,20.6,.8,21.6).C(4.8,21.2,7.2,19.8,8.4,17.8).Z(),new P().M(10.6,19.2).C(11,21,10.4,22.6,8.8,23.8).C(12,23.4,13.8,21.6,14.4,19.2).Z(),new P().M(16.6,17.6).C(18,19.6,20.4,20.6,23.2,20.2).C(20.6,19.2,19.6,17.2,19.4,15.2).Z()];
  const eye=new P().M(13.2,4.6).C(14.9,7,14.9,12.6,13.2,15).C(11.5,12.6,11.5,7,13.2,4.6).Z();
  return [L(corona),L(tendrils),L(eye)];
};
// L33T H4X: a code bracket pair and slash, one band of it torn sideways like a glitch.
icons['spec-leet']=()=>{
  const lt=poly([[8.4,4.2],[10.8,6.6],[5.4,12],[10.8,17.4],[8.4,19.8],[.6,12]]),gt=lt.flipX(),slash=poly([[14,3.2],[16.6,3.2],[10,20.8],[7.4,20.8]]);
  const parts=[];for(const [y0,y1,dx] of [[0,10.2,0],[10.2,13,1.5],[13,24,0]])for(const q of [lt,gt,slash])parts.push(clipPoly(q,y0,y1).shift(dx,0));
  const bits=[crect(20.4,13.6,1.8,1.4),crect(1.4,8.6,1.6,1.4),crect(3.2,15.8,1.2,1.2)];
  return [L(parts),L([line([[0,10.2],[24,10.2]]),line([[0,13],[24,13]])],{cut:true,line:.55}),L(bits,{opacity:TONE})];
};
// EMPEROR: a crown struck through by a storm bolt.
icons['spec-emperor']=()=>{
  const crown=poly([[3,8.2],[7.6,12],[12,4.6],[16.4,12],[21,8.2],[19.4,18],[4.6,18]]),band=crect(4.6,19.2,14.8,2.6,.6);
  const jewels=[poly([[3,4.6],[4.4,6],[3,7.4],[1.6,6]]),poly([[21,4.6],[22.4,6],[21,7.4],[19.6,6]])];
  const bolt=poly([[13.8,.4],[8.6,9.6],[12,9.6],[9.6,17.6],[15.8,7.6],[12.4,7.6],[15.4,.4]]);
  return [L(crown),L(band),L(jewels),L(bolt,{knock:GAP})];
};
// ANSATSUKEN: a clenched fist, its energy aura flaring upward behind it.
icons['spec-ansatsuken']=()=>{
  const aura=poly([[4.6,19],[2.2,13.8],[1.6,8.4],[4.2,10.2],[4.4,4.8],[7.4,7.2],[8.6,1.8],[11,5.4],[12.6,.4],[14.4,5.2],[16.8,1.6],[17.6,6.8],[20.4,4.4],[20.2,9.8],[22.6,8.2],[21.8,13.8],[19.4,19]]);
  const fingers=[7.6,6.3,6.5,7.8].map((y,i)=>crect(5.4+i*3.35,y,3.05,13-y,1.1));
  const palm=poly([[5.4,11.8],[18.9,11.8],[18.9,16.8],[16.6,19.4],[8.2,19.4],[5.4,16.6]]);
  const thumb=poly([[3.8,12.8],[11.6,12.1],[14.2,13.6],[12.3,15.2],[6,15.4],[3.8,14.4]]),wrist=crect(8.4,19.4,8,3.8,.4);
  return [L(aura,{opacity:TONE}),L(palm),L(wrist,{knock:.35}),L(fingers,{knock:.5}),L(thumb,{knock:.7})];
};
// SIREN: her grin — slanted eyes over a wide jagged smile.
icons['spec-siren']=()=>{
  const eyeL=new P().M(1.6,4.6).L(10.8,8.2).C(9,11.4,3.4,10.8,1.6,4.6).Z(),eyeR=eyeL.flipX();
  const grin=new P().M(1,12.2).C(8.2,15,15.8,15,23,12.2).C(19,22.2,5,22.2,1,12.2).Z();
  const n=6,teeth=[];for(let i=0;i<=n;i++){const x=2.2+i*(19.6/n),top=12.9+2.2*Math.sin(Math.PI*i/n);teeth.push([x,i%2?top+4.8-Math.abs(i-n/2)*.5:top+.1]);}
  return [L([eyeL,eyeR]),L(grin),L(line(teeth),{cut:true,line:1.3})];
};
// Horizontal slab clip of a polygon path made by poly() (Sutherland–Hodgman against y0 <= y <= y1).
function clipPoly(pathP,y0,y1)
{
  let pts=pathP.c.filter(c=>c[0]==='M'||c[0]==='L').map(c=>c[1]);
  const clip=(pts,inside,cross)=>{const o=[];for(let i=0;i<pts.length;i++){const a=pts[i],b=pts[(i+1)%pts.length];const ia=inside(a),ib=inside(b);if(ia)o.push(a);if(ia!==ib)o.push(cross(a,b));}return o;};
  const xAt=(a,b,y)=>[a[0]+(b[0]-a[0])*(y-a[1])/(b[1]-a[1]),y];
  pts=clip(pts,p=>p[1]>=y0,(a,b)=>xAt(a,b,y0));pts=clip(pts,p=>p[1]<=y1,(a,b)=>xAt(a,b,y1));
  return pts.length>2?poly(pts):new P();
}

// ---- small drawings (22 px and below): fewer, bolder parts, wider gaps ----
const small={};
small['class-ranged']=()=>{
  const reticle=[ring(14,12,5.3,7.8),crect(12.75,1,2.5,3.6),crect(21.2,10.75,2.6,2.5),crect(12.75,19.4,2.5,3.6)];
  const round=poly([[5,9.8],[12.2,9.8],[14.6,10.6],[16.8,12],[14.6,13.4],[12.2,14.2],[5,14.2]]);
  return [L(reticle),L(round,{knock:1.5}),L(taper([4,12],[.4,12],2.4,.8))];
};
small['class-melee']=()=>{
  const a=blade(3.6,23).map(p=>p.rot(45)),b=blade(3.6,23).map(p=>p.rot(-45));
  return [L(a),L(b,{knock:1.3}),L(star(12,4.4,1.2,3.6),{knock:.8})];
};
small['class-magic']=()=>{
  const crystal=poly([[12,.6],[16.8,7.8],[12,16.4],[7.2,7.8]]);
  const collar=poly([[7.6,14.4],[12,17.6],[16.4,14.4],[16.4,16.8],[12,20],[7.6,16.8]]),staff=poly([[10.7,18.6],[13.3,18.6],[13.3,22.8],[12,24],[10.7,22.8]]);
  const lit=poly([[12,.6],[12,16.4],[7.2,7.8]]);
  return [L(crystal,{opacity:.55}),L(lit),L(line([[12,1.6],[12,15.4]]),{cut:true,line:.9}),L(collar,{knock:1}),L(staff),L(star(20,3.4,.6,2.6),{knock:.6})];
};
small['class-dev']=()=>{
  const frame=hexP(11.8).add(hexP(9));
  const die=crect(7.2,7.2,9.6,9.6,1.8),core=crect(10.1,10.1,3.8,3.8,.4);
  const pins=[crect(11,4.6,2,2.2),crect(11,17.2,2,2.2),crect(4.6,11,2.2,2),crect(17.2,11,2.2,2)];
  return [L(frame,{evenodd:true}),L(pins),L(die.add(core),{evenodd:true})];
};
// The bow at small sizes: heavier limbs, the arrow without its fletching.
small['spec-bow']=()=>{
  const limbs=ellipseBand(7.6,12,8.6,9.8,2.8,-80,80),riser=crect(14.8,8.8,3.2,6.4,.8),string=line([[9.4,2.4],[3.6,12],[9.4,21.6]]);const ar=arrowShape(3,22.6,12,1.5,3,false);
  const place=p=>p.rot(-38,12,12);return [L([limbs,riser].map(place)),L(place(string),{line:1.1,opacity:TONE}),L(ar.shaft.map(place),{knock:1.1})];
};
small['spec-rifle']=()=>{
  const body=[poly([[-15,1.4],[-14.2,-1.2],[-6.5,-2.2],[-4.5,-2.2],[-4.5,2.2],[-7.5,2.8],[-10.5,4.8],[-15,5]]),crect(-4.8,-2.4,10.2,4.6,.6),crect(5.2,-1.6,10.6,3,0),crect(-1.6,1.8,3,3.8)];
  const scope=[crect(-4.4,-7,10.4,3.6,.8)];
  const place=p=>p.scale(.82,0,0).rot(-45,0,0).shift(12.2,11.8);
  return [L(body.map(place)),L(scope.map(place),{knock:1.1})];
};
small['spec-leet']=()=>{
  const lt=poly([[8.2,3.6],[11,6.4],[5.6,12],[11,17.6],[8.2,20.4],[0,12]]),gt=lt.flipX(),slash=poly([[13.8,2.8],[16.8,2.8],[10.2,21.2],[7.2,21.2]]);
  const parts=[lt,gt];for(const [y0,y1,dx] of [[0,10.4,0],[10.4,13.6,2.2],[13.6,24,0]])parts.push(clipPoly(slash,y0,y1).shift(dx,0));
  return [L(parts)];
};
small['spec-ansatsuken']=()=>{
  const aura=poly([[4.4,19],[1.8,12.6],[1.8,6.8],[5.2,9.4],[7,2.6],[10,6.4],[12,.4],[14,6.4],[17,2.6],[18.8,9.4],[22.2,6.8],[22.2,12.6],[19.6,19]]);
  const fingers=[7.6,6.3,6.5,7.8].map((y,i)=>crect(5+i*3.55,y,3.1,13-y,1));
  const palm=poly([[5,11.8],[19.2,11.8],[19.2,16.8],[16.8,19.6],[8,19.6],[5,16.6]]),wrist=crect(8.2,19.6,8,4,.4);
  const thumb=poly([[3.4,12.8],[11.8,12],[14.6,13.6],[12.4,15.6],[5.8,15.8],[3.4,14.6]]);
  return [L(aura,{opacity:.5}),L(palm),L(wrist),L(fingers,{knock:.75}),L(thumb,{knock:.9})];
};

// ---- export ----
const CLASSES={dev:'class-dev',magic:'class-magic',melee:'class-melee',ranged:'class-ranged'};
const SPECS=Object.fromEntries(['ansatsuken','bow','daggers','darkness','emperor','fire','greatsword','leet','lightning','machinegun','pistol','rifle','shield','siren','sword','water'].map(id=>[id,'spec-'+id]));
const entries=Object.keys(icons).sort().map(id=>{const e={id,markup:markup(icons[id]())};if(small[id])e.small=markup(small[id]());return e;});
const pairs=o=>Object.entries(o).sort(([a],[b])=>a<b?-1:1).map(([id,icon])=>({id,icon}));
const data={schema:1,source:'tools/export_class_icons.mjs',note:'Original Chip Arena class and weapon artwork authored for this project as geometry; not derived from any third-party icon set.',box:24,smallPx:22,fallback:'class-dev',icons:entries,classes:pairs(CLASSES),specs:pairs(SPECS)};
const target=path.join(project,'Assets/Conversion/Resources/ClassIcons.json'),text=JSON.stringify(data)+'\n';
if(process.argv.includes('--check')){if(!fs.existsSync(target)||fs.readFileSync(target,'utf8')!==text){console.error('ClassIcons.json is stale: run node tools/export_class_icons.mjs');process.exit(1);}console.log('ClassIcons.json is current');process.exit(0);}
fs.writeFileSync(target,text);
const pi=process.argv.indexOf('--preview');
if(pi>0)
{
  // Browser previews: each knockout becomes a mask over everything drawn before it.
  const dir=path.resolve(process.argv[pi+1]);fs.mkdirSync(dir,{recursive:true});
  for(const e of entries)for(const [variant,m] of [['',e.markup],['-small',e.small]])
  {
    if(!m)continue;let body='',defs='',n=0;
    for(const tag of m.match(/<path\b[^>]*\/>/g))
    {
      if(tag.includes('data-knockout')){const id='k'+(n++);defs+=`<mask id="${id}" maskUnits="userSpaceOnUse" x="-2" y="-2" width="28" height="28"><rect x="-2" y="-2" width="28" height="28" fill="#fff"/>${tag.replace(/#fff/g,'#000').replace(' data-knockout="1"','')}</mask>`;body=`<g mask="url(#${id})">${body}</g>`;}
      else body+=tag;
    }
    fs.writeFileSync(path.join(dir,e.id+variant+'.svg'),`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24"><defs>${defs}</defs>${body}</svg>\n`);
  }
}
console.log('Class icons exported',entries.length,'icons',entries.filter(e=>e.small).length,'small drawings');
