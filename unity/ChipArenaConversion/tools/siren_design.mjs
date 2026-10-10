// The Siren's Unity finish beyond the pinned source: "The Fallen Couturiere" (owner request 2026-10-10, "make the siren
// model more detailed, a great design"). One declarative table shared by tools/export_models.mjs (the arena bank: the
// finer grin, the longer talons and one unit box per tone, which Core/NativeSirenDetail.cs places per frame),
// tools/export_cinematics.mjs (the dev-siren entrance bake: the chest-up dressing on the stage rig) and
// tools/export_siren_detail.mjs (the parity fixture Tests/siren-detail-parity.json). Core/NativeSirenDetail.cs ports the
// table piece for piece; Tests/SirenDetailChecks.cs holds the two to the same matrices.
//
// Frame: the source rig's (three.js, right-handed, +x the Siren's left, +y up, +z forward), metres, bone-relative. Every
// piece is a unit box scaled to `size`; `rot` is Euler radians in `order` ('XYZ' unless 'YXZ'). A 'node' is a hinge with no
// geometry; `parent` names the node a def hangs from (else the bone itself). Right-side defs negate pos.x, rot.y and rot.z
// (the whole subtree is mirrored, as the exporter mirrors x into Unity). Nothing here changes a joint, a hitbox or a timing.
export const SOURCE_PIECES=136;
/** Ink tones (NativeSourceLit, _Ink 1, per-part rim/flow), the glow tones (unlit, per-instance tint) and the flame box. */
export const TONES={
 plate:{ink:0x0b0910,rim:.42,flow:.05},  // stacked plates: every plate edge reads
 deep:{ink:0x060508,rim:.26,flow:.02},   // inner layers: darker, softer rim (depth between layers)
 sheen:{ink:0x151120,rim:.55,flow:.08},  // show faces (collar fronts, bodice, pauldron tops, nimbus)
 ash:{ink:0x15131b,emissive:[.07,.064,.09],rim:.42,flow:.05},// the face sculpt's edge trims (the plate part with vertex emission)
 seam:{glow:0xcfc3ff,intensity:.82},     // the glowing piping
 filament:{glow:0xf1edff,intensity:1},   // the nimbus filament and the claw rings
};
/** The shared bank part each tone draws with (seam and filament share one unit box, tinted per instance). */
export const PARTS={plate:'siren-couture-plate',deep:'siren-couture-deep',sheen:'siren-couture-sheen',ash:'siren-couture-ash',seam:'siren-couture-seam',filament:'siren-couture-seam',flame:'siren-flame-box'};
/** Tone order of Core/NativeSirenDetail.cs (Plate .. Sprite). */
export const TONE_ORDER=['plate','deep','sheen','ash','seam','filament','flame','sprite'];
/** Seam cross-section (m): ~.7 px at 15 m 1080p, ~2.5 px in the entrance and portraits. */
export const SW=.011;
/** Talons: the long claws x1.15 along the blade, the thumb hook x1.08 (baked into the claw parts). */
export const TALONS={claw:1.15,thumb:1.08};
/** The nimbus: hub on the head, ring radius, the missing and cracked slots. */
export const NIMBUS={y:.32,z:-.24,tilt:-.1,r:.33,spin:.16,gaps:[3,10,11],cracks:{4:[.034,.28],9:[.028,-.24],15:[.018,.16]}};
/** The two soft glows in the nimbus breaks (the eye-halo quad): slot, size; colour and opacity. */
export const CRACK_GLOWS=[[3,.15],[10.5,.18]],CRACK_COLOR=0xd8ceff,CRACK_OPACITY=.3;
/** Cheek lift per unit c (head-local metres / radians): the cheeks rise, come forward and turn up. */
export const CHEEK={dx:.003,dy:.014,dz:.006,turn:.14};
const L=1,R=-1;

export function table(){
 const P=[];const add=o=>{const e={order:'XYZ',rot:[0,0,0],size:[1,1,1],...o};P.push(e);return e;};
 const mirror=(o,s)=>{if(s>0)return o;const r=o.rot??[0,0,0];return {...o,id:o.id.replace(/-l(\b|-)/,'-r$1'),bone:o.bone.replace('left','right'),pos:[-o.pos[0],o.pos[1],o.pos[2]],rot:[r[0],-r[1],-r[2]],parent:o.parent?.replace(/-l(\b|-)/,'-r$1')};};

 // 1. COLLAR (chest): a pleated standing collar behind the head, open at the front (theta 0 straight back, +-104 deg) so a
 // cocked head never turns into it. 15 plates (sheen/deep pleats), each a foot and a blade on hinges flaring .10 and .34 rad,
 // .54 at the back and .24 at the front corners; a pointed crown edge with a glowing point on every other tip.
 for(let i=0;i<15;i++){const u=i/7-1,th=(-104+208*i/14)*Math.PI/180,H=.24+.30*(1-u*u)**.7,tone=i%2?'deep':'sheen',z=i%2?-.004:0;
  const base=add({id:`collar-${i}`,group:'collar',bone:'chest',tone:'node',pos:[.2*Math.sin(th),.25,-.2*Math.cos(th)],rot:[.10,Math.PI-th,0],order:'YXZ'});
  add({id:`collar-${i}-foot`,group:'collar',bone:'chest',tone,parent:base.id,size:[.082,.5665*H,.016],pos:[0,.275*H,z]});
  const hinge=add({id:`collar-${i}-h`,group:'collar',bone:'chest',tone:'node',parent:base.id,pos:[0,.55*H,0],rot:[.24,0,0]});
  add({id:`collar-${i}-blade`,group:'collar',bone:'chest',tone,parent:hinge.id,size:[.1,.4635*H,.016],pos:[0,.225*H,z]});
  add({id:`collar-${i}-tip`,group:'collar',bone:'chest',tone,parent:hinge.id,size:[.07,.07,.014],pos:[0,.45*H,z],rot:[0,0,Math.PI/4]});
  if(i%2===0)add({id:`collar-${i}-point`,group:'collar',bone:'chest',tone:'seam',parent:hinge.id,size:[.02,.02,.01],pos:[0,.45*H+.044,.006],rot:[0,0,Math.PI/4]});
 }
 // 2. PAULDRONS: a clavicle lame with an upturned tip, two arm lames below (they follow the arm).
 for(const s of [L,R]){
  add(mirror({id:'pauldron-top-l',group:'pauldron',bone:'leftShoulder',tone:'sheen',size:[.25,.04,.27],pos:[.085,.075,0],rot:[0,0,-.16]},s));
  add(mirror({id:'pauldron-tip-l',group:'pauldron',bone:'leftShoulder',tone:'sheen',size:[.08,.035,.25],pos:[.228,.102,0],rot:[0,0,.6]},s));
  add(mirror({id:'pauldron-mid-l',group:'pauldron',bone:'leftArm',tone:'plate',size:[.23,.035,.255],pos:[.045,.015,0],rot:[0,0,-.62]},s));
  add(mirror({id:'pauldron-low-l',group:'pauldron',bone:'leftArm',tone:'deep',size:[.2,.03,.235],pos:[.06,-.06,0],rot:[0,0,-.95]},s));
 }
 // 3. BODICE (chest): a plunging V of two sheen panels with glowing piping down the V; a dorsal ridge down the back.
 for(const s of [L,R]){
  const panel=add(mirror({id:'bodice-l',group:'bodice',bone:'chest',tone:'node',pos:[.11,.11,.162],rot:[0,.14,-.28]},s));
  add(mirror({id:'bodice-plate-l',group:'bodice',bone:'chest',tone:'sheen',parent:panel.id,size:[.17,.29,.02],pos:[0,0,0]},s));
  add(mirror({id:'bodice-seam-l',group:'bodice',bone:'chest',tone:'seam',parent:panel.id,size:[SW,.3,SW],pos:[-.081,0,.012]},s));
 }
 for(let k=0;k<5;k++)add({id:`ridge-${k}`,group:'bodice',bone:'chest',tone:'sheen',size:[.075-.008*k,.03,.06],pos:[0,.27-.075*k,-.168],rot:[-.62,0,0]});
 // 4. GIRDLE (spine): a cinched waist of eight short plates.
 for(let i=0;i<8;i++){const ph=(i+.5)*Math.PI/4;add({id:`girdle-${i}`,group:'girdle',bone:'spine',tone:i===0||i===7?'sheen':'plate',size:[.11,.075,.016],pos:[.19*Math.sin(ph),.03,.142*Math.cos(ph)],rot:[0,ph,0]});}
 // 5. GOWN (pelvis): bell tiers round the back and sides (theta 0 forward), each plate a narrow top and a wider bottom; the
 // front stays open so the leg wisps still show below the knees.
 const tier=(name,n,from,to,rx,rz,y,h,wTop,wBot,flare,tone)=>{const out=[];for(let i=0;i<n;i++){const th=(from+(to-from)*i/(n-1))*Math.PI/180;
   const node=add({id:`${name}-${i}`,group:'gown',bone:'pelvis',tone:'node',pos:[rx*Math.sin(th),y,rz*Math.cos(th)],rot:[-flare,th,0],order:'YXZ'});
   add({id:`${name}-${i}-top`,group:'gown',bone:'pelvis',tone,parent:node.id,size:[wTop,.52*h,.016],pos:[0,-.26*h,0]});
   add({id:`${name}-${i}-bot`,group:'gown',bone:'pelvis',tone,parent:node.id,size:[wBot,.5*h,.016],pos:[0,-.75*h,.004]});
   out.push({node,h});}return out;};
 tier('peplum',8,72,288,.255,.185,.075,.2,.13,.15,.38,'sheen');
 const skirt=tier('skirt',9,96,264,.27,.205,-.04,.46,.14,.185,.3,'plate');
 // 6. FRONT PANELS (legs): two hinged plates per thigh, the lower swung back toward vertical, their inner edges carrying the
 // glowing slit (the bodice V continued), two ash steps dissolving off the lower plate.
 for(const s of [L,R]){
  const a=add(mirror({id:'front-a-l',group:'front',bone:'leftLeg',tone:'node',pos:[.02,.02,.125],rot:[-.08,.10,0],order:'YXZ'},s));
  add(mirror({id:'front-a-plate-l',group:'front',bone:'leftLeg',tone:'sheen',parent:a.id,size:[.2,.25,.016],pos:[.06,-.125,0]},s));
  add(mirror({id:'front-a-slit-l',group:'front',bone:'leftLeg',tone:'seam',parent:a.id,size:[SW,.25,SW],pos:[-.036,-.125,.011]},s));
  const b=add(mirror({id:'front-b-l',group:'front',bone:'leftLeg',tone:'node',parent:a.id,pos:[0,-.25,0],rot:[.5,0,0]},s));
  add(mirror({id:'front-b-plate-l',group:'front',bone:'leftLeg',tone:'plate',parent:b.id,size:[.215,.22,.016],pos:[.065,-.11,.002]},s));
  add(mirror({id:'front-b-slit-l',group:'front',bone:'leftLeg',tone:'seam',parent:b.id,size:[SW,.22,SW],pos:[-.039,-.11,.013]},s));
  [.075,.05].forEach((w,k)=>add(mirror({id:`front-ash-${k}-l`,group:'hem',bone:'leftLeg',tone:'flame',parent:b.id,size:[w,.9*w,.6*w],pos:[.04+.05*k,-.25-.06*k,0],anim:{kind:'ash',i:20+k,k}},s)));
 }
 // 7. TRAIN (pelvis, animated): five long panels hinged on the back of the skirt hem, trailing the drift, a burning hem seam
 // and three dissolving ash steps under each.
 for(let i=0;i<5;i++){const th=(140+20*i)*Math.PI/180,h=.40-.04*Math.abs(i-2);
  const hinge=add({id:`train-${i}`,group:'train',bone:'pelvis',tone:'node',pos:[.31*Math.sin(th),-.46,.27*Math.cos(th)],rot:[-.5,th,0],order:'YXZ',anim:{kind:'train',i}});
  add({id:`train-${i}-panel`,group:'train',bone:'pelvis',tone:i%2?'deep':'plate',parent:hinge.id,size:[.165,h,.016],pos:[0,-h/2,0]});
  add({id:`train-${i}-hem`,group:'train',bone:'pelvis',tone:'seam',parent:hinge.id,size:[.15,SW,SW],pos:[0,-h+.006,.012]});
  [.11,.07,.045].forEach((w,k)=>add({id:`train-${i}-ash-${k}`,group:'hem',bone:'pelvis',tone:'flame',parent:hinge.id,size:[w,.9*w,.6*w],pos:[(k%2?-1:1)*.03,-h-.05-.075*k,0],anim:{kind:'ash',i,k}}));
 }
 // 8. HEM ASH under skirt plates 1, 2, 6 and 7 (the train's and the front panels' are above).
 for(const i of [1,2,6,7]){const e=skirt[i];[.08,.05].forEach((w,k)=>add({id:`skirt-${i}-ash-${k}`,group:'hem',bone:'pelvis',tone:'flame',parent:e.node.id,size:[w,.9*w,.6*w],pos:[(k?-1:1)*.025,-e.h-.045-.07*k,0],anim:{kind:'ash',i:10+i,k}}));}
 // 9. GAUNTLETS (elbow, wrist): a bell vambrace of three widening rings, a raked elbow spike, knuckle lames.
 for(const s of [L,R]){
  add(mirror({id:'vambrace-a-l',group:'gauntlet',bone:'leftElbow',tone:'plate',size:[.175,.07,.185],pos:[0,-.075,0]},s));
  add(mirror({id:'vambrace-b-l',group:'gauntlet',bone:'leftElbow',tone:'plate',size:[.19,.065,.2],pos:[0,-.14,0]},s));
  add(mirror({id:'cuff-l',group:'gauntlet',bone:'leftElbow',tone:'sheen',size:[.23,.045,.24],pos:[0,-.205,.004],rot:[.12,0,0]},s));
  [[.05,.07],[.034,.06],[.018,.055]].forEach(([w,l],k)=>add(mirror({id:`elbow-spike-${k}-l`,group:'gauntlet',bone:'leftElbow',tone:'plate',size:[w,w,l],pos:[0,.012+.02*k,-.1-.055*k],rot:[.35,0,0]},s)));
  add(mirror({id:'knuckle-a-l',group:'gauntlet',bone:'leftHand',tone:'sheen',size:[.158,.032,.12],pos:[0,-.025,.02]},s));
  add(mirror({id:'knuckle-b-l',group:'gauntlet',bone:'leftHand',tone:'plate',size:[.146,.03,.1],pos:[0,-.068,.07],rot:[.35,0,0]},s));
 }
 // 11. CLAW RINGS (claw groups: they retract with the claws): a glowing band at each long claw's root.
 for(const s of [L,R])for(const x of [-.046,0,.046])add(mirror({id:`ring-${x<0?'a':x>0?'c':'b'}-l`,group:'rings',bone:'leftClaws',tone:'filament',size:[.028,.009,.026],pos:[x,-.006,0],rot:[0,0,1.6*x]},s));
 // 12. NIMBUS (head, animated): a fractured ring of 18 ink segments (3 missing, 3 cracked out of line) with a glowing
 // filament on the inner edge, seven raked rays on the upper arc, four shards drifting in the breaks; it turns slowly.
 const N=NIMBUS,gaps=new Set(N.gaps);
 const hub=add({id:'nimbus',group:'nimbus',bone:'head',tone:'node',pos:[0,N.y,N.z],rot:[N.tilt,0,0],anim:{kind:'nimbus'}});
 for(let i=0;i<18;i++){if(gaps.has(i))continue;const a=Math.PI/2-i*Math.PI/9,[dr,tw]=N.cracks[i]??[0,0],rr=N.r+dr,len=2*Math.PI*N.r/18*.9;
  const seg=add({id:`nimbus-seg-${i}`,group:'nimbus',bone:'head',tone:'node',parent:hub.id,pos:[rr*Math.cos(a),rr*Math.sin(a),0],rot:[0,0,a-Math.PI/2+tw]});
  add({id:`nimbus-seg-${i}-ink`,group:'nimbus',bone:'head',tone:'sheen',parent:seg.id,size:[len,.028,.026],pos:[0,0,0]});
  add({id:`nimbus-seg-${i}-fil`,group:'nimbus',bone:'head',tone:'filament',parent:seg.id,size:[len*.94,.016,.012],pos:[0,-.005,.016]});
 }
 [0,1,-1,2,-2,4,-4].forEach((k,j)=>{const a=Math.PI/2-k*Math.PI/6;const ray=add({id:`nimbus-ray-${j}`,group:'nimbus',bone:'head',tone:'node',parent:hub.id,pos:[(N.r+.022)*Math.cos(a),(N.r+.022)*Math.sin(a),-.004],rot:[0,0,a-Math.PI/2]});
  let y=0;[[.034,.065],[.023,.065],[.012,.06]].slice(0,j<3?3:2).forEach(([w,h],q)=>{add({id:`nimbus-ray-${j}-${q}`,group:'nimbus',bone:'head',tone:'sheen',parent:ray.id,size:[w,h,.018],pos:[0,y+h/2,0]});y+=.92*h;});});
 [[2.1,.03],[3.35,.022],[5.5,.026],[5.9,.018]].forEach(([a,s],k)=>add({id:`nimbus-shard-${k}`,group:'nimbus',bone:'head',tone:k%2?'filament':'sheen',parent:hub.id,size:[s,.55*s,.55*s],pos:[(N.r+.012)*Math.cos(a),(N.r+.012)*Math.sin(a),.01],rot:[0,0,a+.7],anim:{kind:'shard',k,a}}));
 // 13. THE SCULPTED MASK (head; faithful-plus graft): raked two-segment brows with ash trims, a frown crease, socket plates,
 // stepped temples with a trim, jawline plates, a chin diamond and the crest. Nothing lies over the eyes or the grin.
 const face=(o,s)=>add(mirror({group:'face',bone:'head',...o},s));
 for(const s of [L,R]){
  face({id:'brow-out-l',tone:'plate',pos:[.122,.295,.171],size:[.056,.016,.022],rot:[0,0,.1]},s);
  face({id:'brow-trim-out-l',tone:'ash',pos:[.1228,.2865,.1825],size:[.05,.004,.005],rot:[0,0,.1]},s);
  face({id:'brow-in-l',tone:'plate',pos:[.066,.2965,.171],size:[.064,.016,.022],rot:[0,0,.42]},s);
  face({id:'brow-trim-in-l',tone:'ash',pos:[.0695,.2887,.1825],size:[.058,.004,.005],rot:[0,0,.42]},s);
  face({id:'crease-l',tone:'plate',pos:[.011,.262,.1665],size:[.007,.03,.008],rot:[0,0,-.45]},s);
  face({id:'socket-l',tone:'plate',pos:[.0356,.1517,.1672],size:[.036,.008,.01],rot:[0,0,-1.0491]},s);
  face({id:'temple-l',tone:'plate',pos:[.173,.215,.06],size:[.012,.14,.12]},s);
  face({id:'temple-step-l',tone:'plate',pos:[.178,.165,.04],size:[.008,.05,.08]},s);
  face({id:'temple-trim-l',tone:'ash',pos:[.1795,.139,.04],size:[.005,.004,.075]},s);
  face({id:'jawline-l',tone:'plate',pos:[.128,.004,.125],size:[.06,.02,.07],rot:[0,-.3,0]},s);
  face({id:'crest-side-l',tone:'plate',pos:[.095,.343,-.03],size:[.045,.016,.2]},s);
 }
 face({id:'chin',tone:'plate',pos:[0,-.033,.148],size:[.026,.026,.02],rot:[0,0,Math.PI/4]},L);
 face({id:'crest',tone:'plate',pos:[0,.348,-.04],size:[.055,.024,.22]},L);
 // 14. CHEEKS (look pieces, never in the head batch): the cheekbone under each outer eye corner and its ash trim, which
 // rides on it; they lift with the squint in the arena and with the smile in the entrance (c 0..1).
 for(const s of [L,R]){
  const frame=add(mirror({id:'cheek-l',group:'cheek',bone:'head',tone:'node',pos:[.163,.176,.17],rot:[0,0,.42],anim:{kind:'cheek',side:s}},s));
  add(mirror({id:'cheek-box-l',group:'cheek',bone:'head',tone:'plate',parent:frame.id,size:[.032,.024,.024],pos:[0,0,0]},s));
  // The trim at (.1625, .1865, .182) rotZ .42 in the head, expressed in the cheek's own frame.
  const dx=.1625-.163,dy=.1865-.176,c=Math.cos(.42),sn=Math.sin(.42);
  add(mirror({id:'cheek-trim-l',group:'cheek',bone:'head',tone:'ash',parent:frame.id,size:[.026,.004,.005],pos:[c*dx+sn*dy,-sn*dx+c*dy,.012]},s));
 }
 // The two soft glows in the nimbus breaks: sprites (the eye-halo quad), last so every box keeps its index.
 CRACK_GLOWS.forEach(([slot,size],k)=>{const a=Math.PI/2-slot*Math.PI/9;add({id:`nimbus-glow-${k}`,group:'nimbus',bone:'head',tone:'sprite',parent:hub.id,size:[size,size,1],pos:[N.r*Math.cos(a),N.r*Math.sin(a),.01]});});
 return P;
}
/** The drawn defs (everything but nodes), in Core/NativeSirenDetail.cs piece order (index SOURCE_PIECES + position). */
export const pieces=(defs=table())=>defs.filter(d=>d.tone!=='node');
/** Stratified flame: tongue n's step i turns about its own vertical axis and swells or thins (fixed per step). */
export const TWIST=(n,i)=>({yaw:.52*i+.9*n,k:1+.16*Math.sin(3.7*n+2.3*i)});

// ---------------------------------------------------------------- the finer grin (faithful-plus graft)
/** 13 upper and 12 lower convex, inward-leaning fangs of varied reach, on the source grin's arcs. */
export const GRIN={halfWidth:.146,upperY:.08,upperK:2.6,lowerY:.018,lowerK:4.6,upper:13,lower:12,inset:.085};
export const UPPER_REACH=[.74,.84,.92,1.0,.86,.8,.76,.8,.86,.98,.92,.84,.74],LOWER_REACH=[.7,.78,.9,.76,.72,.68,.68,.72,.76,.9,.78,.7];
/** One tooth (grin-local): root chord A->B on its row's arc and the tip, for the rest mouth. s in [-1,1] is the slot centre. */
export function tooth(row,i){
 const w=GRIN.halfWidth,n=row==='upper'?GRIN.upper:GRIN.lower,step=2*w/n,a=-w+(row==='upper'?i:i+.5)*step,b=a+step,mid=(a+b)/2,s=mid/w;
 const up=x=>GRIN.upperY+GRIN.upperK*x*x,lo=x=>GRIN.lowerY+GRIN.lowerK*x*x,lean=-.22*s*step;
 if(row==='upper'){const reach=UPPER_REACH[i];return [[a,up(a)],[b,up(b)],[mid+lean,up(mid)-(up(mid)-lo(mid))*reach]];}
 const reach=LOWER_REACH[i];return [[a,lo(a)],[b,lo(b)],[mid+lean,lo(mid)+(up(mid)-lo(mid))*reach]];
}
/** The canonical fang outline (u, v): root u 0..1 at v 0, tip (.5, -1); inset toward its centroid; a fan of 5 triangles. */
export const TOOTH_SHAPE=(()=>{const p=[[0,0],[1,0],[.9,-.32],[.7,-.7],[.5,-1],[.33,-.66],[.12,-.3]];const cx=p.reduce((s,q)=>s+q[0],0)/p.length,cy=p.reduce((s,q)=>s+q[1],0)/p.length;return p.map(([u,v])=>[u+(cx-u)*GRIN.inset,v+(cy-v)*GRIN.inset]);})();
export const TOOTH_FAN=[[0,1,2],[0,2,6],[2,5,6],[2,3,5],[3,4,5]];
/** P(u, v) = A + u(B - A) + vD with D = A + (B - A)/2 - tip, so v = -1 lands on the tip. */
const mapTooth=([A,B,Tp],[u,v])=>{const ux=B[0]-A[0],uy=B[1]-A[1],dx=A[0]+.5*ux-Tp[0],dy=A[1]+.5*uy-Tp[1];return [A[0]+u*ux+v*dx,A[1]+u*uy+v*dy];};
/** The two grin rows' triangles (x, y, 0 per vertex), wound counter-clockwise seen from +z like buildSiren's rows. */
export function grinRow(row){
 const out=[],n=row==='upper'?GRIN.upper:GRIN.lower;
 for(let i=0;i<n;i++){const P=TOOTH_SHAPE.map(q=>mapTooth(tooth(row,i),q));
  for(const [a,b,c] of TOOTH_FAN){const cr=(P[b][0]-P[a][0])*(P[c][1]-P[a][1])-(P[c][0]-P[a][0])*(P[b][1]-P[a][1]);for(const k of cr>=0?[a,b,c]:[a,c,b])out.push(P[k][0],P[k][1],0);}}
 return out;
}

// ---------------------------------------------------------------- applying the finish to a built source Siren
/**
 * The finish every Siren model shares: the finer grin rows, the longer talons and (stage rigs only: the arena's tongue
 * boxes are Core pieces) the stratified flame. `parts` is buildSiren's SirenParts.
 */
export function finish(T,parts,{grin=true,talons=true,twist=false}={}){
 if(grin)for(const [row,mesh] of [['upper',parts.grinUpper],['lower',parts.grinLower]]){const p=grinRow(row),g=new T.BufferGeometry();
  g.setAttribute('position',new T.Float32BufferAttribute(p,3));g.setAttribute('uv',new T.Float32BufferAttribute(new Array(p.length/3*2).fill(0),2));g.computeVertexNormals();mesh.geometry=g;}
 if(talons)for(const g of parts.claws)for(const c of g.children)if(c.name.startsWith('siren-claw'))c.scale.y*=c.name==='siren-claw-thumb'?TALONS.thumb:TALONS.claw;
 if(twist)parts.tongues.forEach((t,n)=>t.group.children.forEach((c,i)=>{const w=TWIST(n,i);c.rotation.y=w.yaw;c.scale.x*=w.k;c.scale.z*=w.k;}));
}
/** Linear colour of a tone's glow (three's sRGB hex decode) times its intensity. */
export const glowLinear=(T,tone)=>new T.Color(TONES[tone].glow).multiplyScalar(TONES[tone].intensity);
/**
 * Materials for real meshes of the table (the entrance bake, the parity rig, offline renders): ink tones as the source's
 * InkMaterial at each tone's rim and flow, the glow tones unlit at their colour, the hem ash the tongues' own material.
 * `M` is the source's siren/materials module; `tongue` the source TONGUE material (a flame step's).
 */
export function materials(T,M,tongue){
 const lin=(r,g,b)=>new T.Color().setRGB(r,g,b,T.LinearSRGBColorSpace),out={flame:tongue};
 for(const [k,t] of Object.entries(TONES)){if(t.glow!==undefined){out[k]=new T.MeshBasicMaterial({color:glowLinear(T,k)});}
  else out[k]=new M.InkMaterial(t.emissive?{color:t.ink,emissive:lin(...t.emissive),emissiveIntensity:1}:{color:t.ink},t.rim,t.flow);}
 return out;
}
/**
 * Dress a built source Siren with the table as real meshes. `groups`: the groups to build (all by default; the entrance
 * builds its chest-up set). `glowTracks`: the glow lines keep their own scene tracks so a bake can dim them (entranceGlow).
 * Each drawn mesh is named 'couture-<id>' and carries userData.nativePart (its bank part) and userData.tone. Returns
 * {objects (id -> Object3D), materials (tone -> material), update(state)}; state is {time, clock, lean, flare, squint, c?,
 * glow?: {seam, filament}} (glow scales the glow lines' colours; 1 without it).
 */
export function dress(T,rig,parts,M,{groups=null,sprites=true,glowTracks=false}={}){
 const box=new T.BoxGeometry(1,1,1),mats=materials(T,M,parts.tongues[0].group.children[0].material),objects=new Map(),rest=new Map();
 const claws={leftClaws:parts.claws[0],rightClaws:parts.claws[1]};
 for(const d of table()){
  if(groups&&!groups.includes(d.group))continue;if(d.tone==='sprite'&&!sprites)continue;
  const parent=d.parent?objects.get(d.parent):(claws[d.bone]??rig[d.bone]);if(!parent)throw Error('No parent for '+d.id);
  let o;if(d.tone==='node')o=new T.Group();else if(d.tone==='sprite'){o=new T.Sprite(M.haloMaterial(CRACK_COLOR,CRACK_OPACITY));}else{o=new T.Mesh(box,mats[d.tone]);o.userData.nativePart=PARTS[d.tone];o.userData.tone=d.tone;o.castShadow=d.tone!=='seam'&&d.tone!=='filament';}
  // bakeStatic: a scene bake (tools/export_cinematics.mjs) batches a still piece on its joint's track, whose opacity it shares.
  o.name='couture-'+d.id;o.userData.bakeStatic=!d.anim&&!(glowTracks&&(d.tone==='seam'||d.tone==='filament'));o.position.fromArray(d.pos);o.rotation.order=d.order;o.rotation.fromArray(d.rot);if(d.tone!=='node')o.scale.fromArray(d.size);
  parent.add(o);objects.set(d.id,o);rest.set(o,d);
 }
 const update=st=>{const t=st.time,c=st.clock,lean=Math.max(0,st.lean??0),flare=st.flare??0,cheek=st.c??.6*Math.min(1,Math.max(0,st.squint??0));
  for(const [o,d] of rest){const a=d.anim;if(!a)continue;
   if(a.kind==='nimbus')o.rotation.z=NIMBUS.spin*c;
   else if(a.kind==='train')o.rotation.x=d.rot[0]-.55*lean-.05*Math.sin(1.9*t+1.3*a.i)-.12*flare;
   else if(a.kind==='ash'){const f=(a.k+1)/3,m=.8+.3*Math.sin(6.4*t+2.3*a.i+1.1*a.k);o.position.set(d.pos[0]+.02*f*Math.sin(4.3*t+1.7*a.i+a.k),d.pos[1]-.012*Math.sin(5.1*t+a.i+2*a.k),d.pos[2]-.06*f*lean);o.scale.set(d.size[0]*m,d.size[1]*(.85+.3*Math.sin(5.7*t+a.i)),d.size[2]*m);}
   else if(a.kind==='shard'){const dr=.012*Math.sin(1.3*c+2.1*a.k),aa=a.a+.03*Math.sin(.9*c+a.k);o.position.set((NIMBUS.r+.012+dr)*Math.cos(aa),(NIMBUS.r+.012+dr)*Math.sin(aa),.01+.006*Math.sin(1.7*c+a.k));o.rotation.z=aa+.7+.6*c*(a.k%2?1:-1);}
   else if(a.kind==='cheek'){const s=a.side;o.position.set(d.pos[0]+s*CHEEK.dx*cheek,d.pos[1]+CHEEK.dy*cheek,d.pos[2]+CHEEK.dz*cheek);o.rotation.z=d.rot[2]+s*CHEEK.turn*cheek;}
  }
  const breath=.5+.5*Math.sin(c*2.4);for(const o of rest.keys())if(o.isSprite)o.material.opacity=CRACK_OPACITY*(.8+.2*breath);
  for(const tone of ['seam','filament'])mats[tone].color.copy(glowLinear(T,tone)).multiplyScalar(st.glow?.[tone]??1);
 };
 return {objects,materials:mats,update};
}
/**
 * The glow lines through the dev-siren entrance (client/siren/intro.ts cues, seconds of the intro clock): full while the
 * bulb shows her; in the blackout the seams almost vanish (.08) and the nimbus filament dims to .14, so the eyes lead; at
 * the smile the filament kindles on the name beat (+.45, fading over .5 s) and settles at .30. cues: {blackout, smile, name}.
 */
export function entranceGlow(age,cues){
 if(age<cues.blackout)return {seam:1,filament:1};const s=age-cues.smile,name=cues.name-cues.smile,sm=x=>{x=Math.min(1,Math.max(0,x));return x*x*(3-2*x);};
 const kindle=s>=name?.45*Math.exp(-(s-name)/.5)*Math.min(1,(s-name)/.04):0;return {seam:.08,filament:Math.min(1,.14+kindle+.16*sm((s-.6)/.8))};
}
/**
 * One unit box per bank part ({geometry, material}), for the arena bank's named sections in their own frames (Core places
 * every instance). No Object3D is made: tools/export_cinematics.mjs finds scene meshes by three.js object id, so an extra
 * object made before the scenes are sampled would change which pooled meshes other entrances export.
 */
export function prototypes(T,M,tongue){
 const geometry=new T.BoxGeometry(1,1,1),mats=materials(T,M,tongue),out=new Map();
 // The glow box is black and emits white: each instance's tint (NativeSirenDetail.GlowColor) is its whole colour.
 mats.seam=new T.MeshLambertMaterial({color:0x000000,emissive:0xffffff,emissiveIntensity:1});
 for(const tone of ['plate','deep','sheen','ash','seam'])out.set(PARTS[tone],{geometry,material:mats[tone]});
 return out;
}
