// The Siren's entrance smile, beyond the pinned source (owner request 2026-10-10: "make the smile animation on intro more
// realistic"). client/siren/intro.ts shows the grin at the 'smile' cue and scales it in x from ~0 to 1 over .55 s, a line
// that stretches. This is the acted smile instead: a closed curved seam that pulls into the grin corner by corner (the
// Siren's left leads, a smirk that resolves), the lips lift and part, the fangs bare from the centre out, the jaw drops and
// settles, the eyes narrow from below as the cheeks push up, the head inhales, dips and rises, and a silent laugh lands on
// beats 15 and 16 of the boss song; then the held grin strains and creeps. Everything is a pure function of the intro age
// (s = age - the 'smile' cue, beat 14): no cue, duration or clock changes.
// Core/NativeSirenSmileFace.cs ports it; Tests/SirenSmileFaceChecks.cs holds the two together (tools/export_siren_smile.mjs).
// tools/export_cinematics.mjs bakes the body layer (head, neck, chest, clavicles: all at or below 4.1 Hz, safe at 30 fps)
// into the dev-siren entrance stage; the face (eyes, the 25 fangs, the cheeks and the eye halos) is drawn natively on the
// baked head track at the presentation clock's age.
// Frame: head-local source frame (three.js, right-handed, +x the Siren's left), metres and radians.
import {GRIN,UPPER_REACH,LOWER_REACH} from './siren_design.mjs';

/** Seconds after the cue: beat 15 (the 'name' cue), beat 16, the overlay's end and the stage hold's end. */
export const NAME=.4179,BEAT16=.8359,END=1.2596,HOLD_END=3.0596;
/** SIREN_INTRO cue beats (shared/siren-contract.ts): blackout and smile. */
export const BLACKOUT_BEAT=13,SMILE_BEAT=14;
/** The pressed, closed mouth (a thin curved seam of fang tips) and the rest mouth (the arena grin, tools/siren_design.mjs). */
export const CLOSED={w:.104,uy:.0535,uk:1.3,ly:.0455,lk:1.3,reach:.65};
export const REST={w:GRIN.halfWidth,uy:GRIN.upperY,uk:GRIN.upperK,ly:GRIN.lowerY,lk:GRIN.lowerK,reach:1};
/** The grin's origin on the head (buildSiren: grin.position z .1638) and the eyes' and halos' rest placement. */
export const MOUTH_Z=.1638,EYE={x:.082,y:.206,z:.1645,tilt:.12,w:.05,h:.08,lift:.0216},HALO={x:.079,y:.21,z:.2,size:.26};
/** The eye halos' opacity before the cue: .35 under the bulb, .6 from the blackout (intro.ts). */
export const HALO_LIT=.35,HALO_DARK=.6;
/** The fangs' gaps: touching slivers on the closed seam, the canonical fang's own inset once bared. */
const FANG_INSET=GRIN.inset,SEAM_INSET=.02,BARED_INSET=.15;

const clamp=(x,a=0,b=1)=>Math.min(b,Math.max(a,x)),lerp=(a,b,t)=>a+(b-a)*t;
export const ease={
 sm:x=>{x=clamp(x);return x*x*(3-2*x);},
 outCubic:x=>{x=clamp(x);return 1-(1-x)**3;},
 outQuad:x=>{x=clamp(x);return 1-(1-x)**2;},
 inOutSine:x=>{x=clamp(x);return .5-.5*Math.cos(Math.PI*x);},
 inOutCubic:x=>{x=clamp(x);return x<.5?4*x*x*x:1-(-2*x+2)**3/2;},
 /** A sharp-attack, soft-release bump on (0, 1). */
 fbump:x=>x<=0||x>=1?0:Math.sin(Math.PI*x)*(1-x)*1.6,
};
const E=ease;
/** sin^2 window over [a, b]. */
export const bump=(s,a,b)=>{const x=clamp((s-a)/(b-a));return Math.sin(Math.PI*x)**2;};
/** A glow kick at t0 of height a: 30 ms rise, .12 s decay. */
export const kick=(s,t0,a)=>s<t0?0:a*Math.exp(-(s-t0)/.12)*clamp((s-t0)/.03);
/** The silent laugh: one bump on each of beats 15 and 16. */
export const chuckle=s=>E.fbump((s-NAME)/.2)+E.fbump((s-BEAT16)/.2);

/** Every channel of the smile at s seconds after the cue. */
export function curves(s){
 const ch=chuckle(s),strain=E.sm((s-.75)/.35),quiver=Math.sin(2*Math.PI*4.1*s),creep=E.sm((s-1)/2),over=.006*bump(s,.40,.66);
 // 1. The seam flickers in: .65 at 20 ms, down to .25 at 35 ms, full at 60 ms.
 const vis=s<0?0:s<.02?.65*clamp(s/.02):s<.035?.65-.4*clamp((s-.02)/.015):s<.06?.25+.75*clamp((s-.035)/.025):1;
 // 2. The corners pull: the Siren's left (wPos) leads, the right follows .09 s later; overshoot, the hold's creep and the
 // left corner's 4.1 Hz strain quiver.
 const wPos=(lerp(CLOSED.w,REST.w,E.outCubic((s-.04)/.42))+over)*(1+.05*creep)*(1+.01*strain*quiver);
 const wNeg=(lerp(CLOSED.w,REST.w,E.outCubic((s-.13)/.42))+over)*(1+.05*creep);
 const lift=E.outCubic((s-.06)/.44);
 const uy=lerp(CLOSED.uy,REST.uy,lift),uk=lerp(CLOSED.uk,REST.uk,lift)+.25*creep,ly=lerp(CLOSED.ly,REST.ly,lift),lk=lerp(CLOSED.lk,REST.lk,lift);
 // The smirk: both lips tilt so the left corner rides up to 19 mm higher, resolved by .78 s; the quiver keeps it alive.
 const tilt=.065*bump(s,.06,.78)+.006*strain*quiver;
 // 3. The fangs bare from the centre out, 24 ms per tooth; the lower row from .23 s.
 const reachU=[],reachL=[];
 for(let i=0;i<GRIN.upper;i++)reachU.push(lerp(CLOSED.reach,REST.reach,E.outCubic((s-.16-.024*Math.abs(i-6))/.34)));
 for(let i=0;i<GRIN.lower;i++)reachL.push(lerp(CLOSED.reach,REST.reach,E.outCubic((s-.23-.024*Math.abs(i-5.5))/.34)));
 // 4. The jaw drops as the teeth bare (about .26 near .5 s), settles near .12 with a slow breath, twitches on each chuckle.
 const open=Math.max(0,.26*E.outCubic((s-.2)/.3)-.14*E.inOutSine((s-.5)/.45)+.012*Math.sin(2.2*Math.max(0,s-.95))+.02*ch);
 // 5. The eyes: the brow cut descends, the lower edge rises (the cheeks' push), the halos swell and kick on the name beat.
 const anger=.9*E.inOutCubic((s-.08)/.42),squint=.5*E.outQuad((s-.14)/.40)-.1*E.inOutSine((s-.6)/.4);
 const halo=.6+.12*bump(s,.2,.75)+.6*kick(s,NAME,.12);
 // 6. The cheeks lift and twitch with the laugh.
 const c=clamp(E.outCubic((s-.08)/.55)+.15*ch,0,1.15);
 // 7. Head and body (added to the intro pose, which no longer carries the source's own smile dip).
 const dip=E.inOutSine((s-.10)/.52),rise=E.inOutSine((s-.75)/.65);
 const headX=-.035*bump(s,0,.18)+.15*dip-.07*rise+.03*ch,headY=-.06*E.inOutSine((s-.1)/.6),headZ=.08*E.inOutSine((s-.12)/.58)+.03*E.sm((s-1)/2)-.012*ch;
 const neckX=.35*(.15*dip-.07*rise),chestX=-.045*bump(s,.04,.62)+.02*ch,clavicle=.07*bump(s,.06,.66)+.04*ch;
 // 8. The fangs flare on beats 15 and 16.
 const glow=vis*(1+kick(s,NAME,.28)+kick(s,BEAT16,.12)+.08*ch);
 return {s,vis,chuckle:ch,strain,wPos,wNeg,uy,uk,ly,lk,tilt,reachU,reachL,open,anger,squint,halo,c,glow,headX,headY,headZ,neckX,chestX,clavicle};
}

/** The canonical fang outline (u, v), root u 0..1 at v 0 and tip (.5, -1), before any inset; its vertex mean. */
export const FANG=[[0,0],[1,0],[.9,-.32],[.7,-.7],[.5,-1],[.33,-.66],[.12,-.3]];
export const FANG_CENTRE=[FANG.reduce((a,p)=>a+p[0],0)/FANG.length,FANG.reduce((a,p)=>a+p[1],0)/FANG.length];
/**
 * One fang (grin-local, before the jaw): root chord A->B on its row's arc and the tip, for smile channels p. siren_design.mjs
 * tooth() generalised to asymmetric half-widths (u > 0 the Siren's left), the smirk's tilt and a reach fraction r of the
 * rest table; at the rest channels it is that tooth exactly (the arena grin).
 */
export function tooth(row,i,p,r){
 const upper=row==='upper',n=upper?GRIN.upper:GRIN.lower,step=2/n,a=-1+(upper?i:i+.5)*step,b=a+step,m=(a+b)/2;
 const X=u=>u<0?u*p.wNeg:u*p.wPos,U=x=>p.uy+p.uk*x*x+p.tilt*x,L=x=>p.ly+p.lk*x*x+p.tilt*x;
 const xa=X(a),xb=X(b),xm=X(m),lean=-.22*m*(xb-xa),reach=r*(upper?UPPER_REACH:LOWER_REACH)[i],gap=U(xm)-L(xm);
 return upper?[[xa,U(xa)],[xb,U(xb)],[xm+lean,U(xm)-gap*reach]]:[[xa,L(xa)],[xb,L(xb)],[xm+lean,L(xm)+gap*reach]];
}
/** The fang's scale about its outline centre for reach fraction r: 1 when bared (the canonical fang), wider on the seam. */
export function fangScale(r){const q=clamp((.86*r-.56)/.3),inset=FANG_INSET*(SEAM_INSET+(BARED_INSET-SEAM_INSET)*q)/BARED_INSET;return (1-inset)/(1-FANG_INSET);}
/**
 * The head-local transform (12 values, row-major 3x4) that maps the unit fang mesh ((u, v, z): the FANG outline inset by
 * GRIN.inset toward FANG_CENTRE; the lower row's mesh holds -v) onto fang i of `row`: P = A + u(B - A) + vD with
 * D = A + (B - A)/2 - tip (v = -1 on the tip; the lower row's v column is -D, so no fang's transform is mirrored), scaled
 * about the outline centre by fangScale, then the jaw (upper row up .006 open; lower row x(1 + .06 open), y(1 + .5 open)
 * - .03 open, as applySirenLook moves the rows) and the grin's origin.
 */
export function toothMatrix(row,i,p,r){
 const [A,B,Tp]=tooth(row,i,p,r),ux=B[0]-A[0],uy=B[1]-A[1],dx=A[0]+.5*ux-Tp[0],dy=A[1]+.5*uy-Tp[1],k=fangScale(r),[cu,cv]=FANG_CENTRE,sv=row==='upper'?1:-1;
 // M·K: columns (B - A)k, ±Dk, z; origin A + (1 - k)(cu(B - A) + cv D).
 let m=[ux*k,sv*dx*k,0,A[0]+(1-k)*(cu*ux+cv*dx), uy*k,sv*dy*k,0,A[1]+(1-k)*(cu*uy+cv*dy), 0,0,1,0];
 const o=p.open;
 if(row==='upper')m[7]+=.006*o;else{const sx=1+.06*o,sy=1+.5*o;for(let c=0;c<4;c++){m[c]*=sx;m[4+c]*=sy;}m[7]-=.03*o;}
 m[11]+=MOUTH_Z;return m;
}
/** three.js Matrix4.compose order T·Rz·S (row-major 3x4) for the eye and halo pieces (no x or y rotation). */
const trz=(px,py,pz,rz,sx,sy,sz)=>{const c=Math.cos(rz),s=Math.sin(rz);return [c*sx,-s*sy,0,px, s*sx,c*sy,0,py, 0,0,sz,pz];};
/**
 * The whole entrance face at `age` for cue times {smile, blackout} (SIREN_INTRO, the boss song's beats 14 and 13). Before the
 * cue every channel is at its stare value by construction (the blank oval, anger 0, no squint, jaw shut, cheeks at rest, the
 * closed seam hidden); the halos glow .35 under the bulb and .6 from the blackout. Pieces: 25 fangs (13 upper then 12
 * lower), 2 eyes and 2 halos (side +1, -1), 12 values each; the cheeks are the design's cheek frames at the channels' lift c
 * (siren_design.mjs dress and CHEEK; Core/NativeSirenDetail.cs Cheeks).
 */
export function face(age,cues){
 const s=age-cues.smile,on=s>=0,p=curves(s);if(age<cues.blackout)p.halo=HALO_LIT;
 const teeth=[];for(const row of ['upper','lower']){const n=row==='upper'?GRIN.upper:GRIN.lower,r=row==='upper'?p.reachU:p.reachL;for(let i=0;i<n;i++)teeth.push(toothMatrix(row,i,p,r[i]));}
 const eyes=[1,-1].map(side=>trz(side*EYE.x,EYE.y+EYE.lift*p.squint,EYE.z,side*EYE.tilt,EYE.w*(1+.06*p.open),EYE.h*(1-.3*p.squint),1));
 const halos=[1,-1].map(side=>trz(side*HALO.x,HALO.y,HALO.z,0,HALO.size,HALO.size,1));
 return {age,s,grin:on&&p.vis>0,shape:Math.min(40,Math.max(0,Math.floor(p.anger*40+.5))),channels:p,teeth,eyes,halos};
}
/** The body layer at s (radians, added to intro.ts's pose; zero before the cue): head pitch, yaw, roll, neck, chest, clavicles. */
export function body(s){if(s<0)return {headX:0,headY:0,headZ:0,neckX:0,chestX:0,clavicle:0};const p=curves(s);return {headX:p.headX,headY:p.headY,headZ:p.headZ,neckX:p.neckX,chestX:p.chestX,clavicle:p.clavicle};}
/** intro.ts's own smile on the head (pitch .1, roll .06 over smoothstep(s / .45)), which the acted head replaces. */
export function sourceDip(s){const k=E.sm(s/.45);return {x:.1*k,z:.06*k};}
/**
 * The entrance bake's pose (tools/export_cinematics.mjs, after PlayerIntros.update): the source's eyes and grin are hidden (the
 * face is drawn natively on the head track) and the body layer replaces the source's smile dip. `rig` is the stage's
 * character rig, `parts` its SirenParts.
 */
export function stagePose(rig,parts,age,cues){
 for(const e of parts.eyes)e.visible=false;parts.grin.visible=false;
 const s=age-cues.smile;if(s<0)return;const b=body(s),d=sourceDip(s);
 rig.head.rotation.x+=b.headX-d.x;rig.head.rotation.y+=b.headY;rig.head.rotation.z+=b.headZ-d.z;rig.neck.rotation.x+=b.neckX;rig.chest.rotation.x+=b.chestX;
 rig.leftShoulder.rotation.z+=b.clavicle;rig.rightShoulder.rotation.z-=b.clavicle;
}
