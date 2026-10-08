import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics,type Snapshot,type PlayerView} from '../src/shared/simulation.js';
import {WEAPONS} from '../src/shared/content.js';
import {SIREN_HOVER} from '../src/shared/siren-contract.js';
import {DraftStage,DRAFT_FRAME,isBossSpec,draftPose,spring} from '../src/client/draft-stage.js';
import {DRAFT_GRAB} from '../src/client/draft-timeline.js';
import {createCharacterRig,type CharacterRig} from '../src/client/character-rig.js';
import {PROBE_BONES,PROBE_RATES} from './helpers/motion-probe.js';
await initPhysics();

/** Chest-up draft close-up (draft-stage.ts): one draft pose for every non-boss pilot, bosses keep theirs, the camera frames
 * head and chest under the tray at every supported aspect, and the arm on the hovered card's side reaches for it. */
const BONES=PROBE_BONES.filter(b=>b!=='weapon'&&b!=='offWeapon');
const SPECS=Object.keys(WEAPONS).filter(s=>!isBossSpec(s));
function draftState(){
 const sim=new Simulation({sandbox:true});sim.addPlayer('me','Pilot','ranged');sim.addPlayer('other','Other','melee');sim.phase='fight';
 sim.debug({action:'draft-preview'},'me');return sim;
}
/** A snapshot of the draft with the picker's spec / cosmetics patched (dev specs and bosses included, no access involved). */
function withPicker(s:Snapshot,patch:Partial<PlayerView>):Snapshot{return {...s,players:s.players.map(p=>p.id===s.draft!.player?{...p,...patch} as PlayerView:p)};}
/** Viewport (px) layout of the overlay: tray bottom fraction and card centres, as DraftView reports them. */
interface Layout {w:number;h:number;trayBottom:number;cards:[number,number][]}
const row=(w:number,y:number,n=6)=>Array.from({length:n},(_,i)=>[w*(.14+.72*i/(n-1)),y] as [number,number]);
const LAYOUTS:Record<string,Layout>={
 '16:9':{w:1920,h:1080,trayBottom:.27,cards:row(1920,207)},
 '16:10':{w:1680,h:1050,trayBottom:.28,cards:row(1680,200)},
 '4:3 two rows':{w:1024,h:768,trayBottom:.59,cards:row(1024,190,3).concat(row(1024,370,3))},
 '21:9':{w:2560,h:1080,trayBottom:.27,cards:row(2560,207)},
 'phone':{w:390,h:844,trayBottom:.52,cards:row(390,380,2)},
 'short landscape':{w:844,h:390,trayBottom:.56,cards:row(844,130,5)},
};
function setView(l:Layout){(globalThis as any).innerWidth=l.w;(globalThis as any).innerHeight=l.h;}
/** Drive a stage like the renderer + DraftView do: card points each frame, then update. */
function run(stage:DraftStage,state:(t:number)=>Snapshot,l:Layout,from:number,to:number,hz=60,hover:(t:number)=>number=()=>-1){
 setView(l);stage.trayBottom=l.trayBottom;const dt=1/hz,n=Math.round((to-from)*hz);
 for(let i=0;i<=n;i++){const t=from+i*dt,s=state(t);stage.cards=l.cards.map(([x,y])=>stage.toWorld(x,y,new T.Vector3())!);stage.hover=hover(t);stage.update(s,l.w/l.h,dt,1,false,false);}
}
const quats=(r:CharacterRig)=>BONES.map(b=>(r[b] as T.Object3D).quaternion.clone());
const worst=(a:T.Quaternion[],b:T.Quaternion[])=>Math.max(...a.map((q,i)=>q.angleTo(b[i])));
function screen(stage:DraftStage,p:T.Vector3){const v=p.clone().project(stage.camera);return {x:(v.x+1)/2,y:(1-v.y)/2};}
const at=(o:T.Object3D,x=0,y=0,z=0)=>{o.updateWorldMatrix(true,false);return o.localToWorld(new T.Vector3(x,y,z));};

test('every non-boss pilot gets the same draft pose, whatever the spec, weapon or lobby pose; cosmetics still show',()=>{
 const sim=draftState(),base=sim.snapshot(),l=LAYOUTS['16:9'];let reference:T.Quaternion[]|undefined;
 assert.ok(isBossSpec('siren')&&!isBossSpec('pistol')&&!isBossSpec(undefined));
 for(const [i,spec] of SPECS.entries()){
  const pose=(['weapon','chair','sleep','crossed'] as const)[i%4],stage=new DraftStage();
  const s=withPicker(base,{spec:spec as PlayerView['spec'],cosmetics:{...base.players[0].cosmetics!,lobbyPose:pose,hat:i%2?'top':'none'}});
  // No cards on the plane: the gaze rests on the same point for everyone, so the whole skeleton must match.
  setView(l);stage.trayBottom=l.trayBottom;for(let k=0;k<90;k++)stage.update({...s,time:s.time+k/60},16/9,1/60);
  const r=stage.pilot!;assert.equal(r.weapon.visible,false,spec+' weapon is put away');assert.equal(r.offWeapon.visible,false);
  if(i%2)assert.ok(r.sockets.head.children.length>0,spec+' keeps its hat');
  const q=quats(r);if(!reference)reference=q;else assert.ok(worst(q,reference)<1e-6,`${spec}/${pose} matches the shared draft pose (${worst(q,reference)})`);
  r.group.traverse(o=>{for(const v of [o.position.x,o.position.y,o.position.z,o.quaternion.x,o.quaternion.w])assert.ok(Number.isFinite(v));});
 }
 sim.dispose();
});

test('the draft pose stands tall: chest lifted, shoulders back, arms down out of frame, and it breathes',()=>{
 const r=createCharacterRig('#ff6b35');r.group.position.y=.88;draftPose(r,0);const a=quats(r),chest=at(r.chest).y;
 draftPose(r,.95);const b=quats(r);assert.ok(worst(a,b)>.005,'breathing moves the pose');assert.ok(worst(a,b)<.08,'but only a little');
 draftPose(r,0);r.group.updateMatrixWorld(true);
 assert.ok(r.chest.rotation.x<0&&r.spine.rotation.x<0,'chest and spine lifted, not hunched');
 for(const hand of [r.leftHand,r.rightHand])assert.ok(at(hand).y<chest-.25,'hands hang below the chest-up frame');
});

test('bosses keep their own pose on the stage and are still framed chest-up',()=>{
 const sim=draftState(),base=sim.snapshot(),l=LAYOUTS['16:9'],siren=new DraftStage(),pilot=new DraftStage();
 run(siren,t=>withPicker({...base,time:t},{spec:'siren'}),l,base.time,base.time+2);run(pilot,t=>({...base,time:t}),l,base.time,base.time+2);
 const s=siren.pilot!,p=pilot.pilot!;
 assert.ok(Math.abs(s.body.position.y-SIREN_HOVER.height)<=SIREN_HOVER.bob+1e-6,'the Siren floats at its hover height');
 assert.ok(s.spine.rotation.x>.15,'the Siren keeps its hunched stance');assert.ok(worst(quats(s),quats(p))>.3,'no uniform draft pose for the boss');
 for(const stage of [siren,pilot]){
  const r=stage.pilot!,head=screen(stage,at(r.head,0,.165,0)),top=screen(stage,at(r.head,0,.34,0));
  assert.ok(top.y>l.trayBottom,'the head stays under the tray');assert.ok(head.y<.75&&head.x>.3&&head.x<.7,'head large and centred in the lower frame');
 }
 sim.dispose();
});

test('the camera frames head and chest under the tray at 16:9, 16:10, 4:3, 21:9 and phone layouts',()=>{
 const sim=draftState(),base=sim.snapshot();
 for(const [name,l] of Object.entries(LAYOUTS)){
  const stage=new DraftStage();run(stage,t=>({...base,time:t}),l,base.time,base.time+1.5);
  const r=stage.pilot!,crown=screen(stage,at(r.head,0,.36,0)),chin=screen(stage,at(r.head,0,-.02,0)),sternum=screen(stage,at(r.chest,0,.13,.16));
  const heads=[-.17,.17].map(x=>screen(stage,at(r.head,x,.165,.16)));
  assert.ok(crown.y>l.trayBottom-1e-3,`${name}: the head never overlaps the cards (crown ${crown.y.toFixed(3)})`);
  for(const h of heads)assert.ok(h.x>0&&h.x<1,`${name}: the whole head is on screen`);
  assert.ok(sternum.y>chin.y&&sternum.y<1,`${name}: the chest is in frame below the head`);
  const size=chin.y-crown.y;assert.ok(size>(l.trayBottom<.4?.17:.08),`${name}: the head is large (${(size*100).toFixed(1)}% of the height)`);
  assert.ok(Math.abs((heads[0].x+heads[1].x)/2-.5)<.06,`${name}: centred`);
 }
 sim.dispose();
});

test('the arm on the hovered card\'s side reaches towards it; the other arm stays down',()=>{
 const sim=draftState(),base=sim.snapshot(),l=LAYOUTS['16:9'];
 for(const index of [0,5]){
  const stage=new DraftStage();run(stage,t=>({...base,time:t}),l,base.time,base.time+2,60,()=>index);
  const r=stage.pilot!,cardLeft=l.cards[index][0]<l.w/2,reach=cardLeft?'right':'left',rest=cardLeft?'left':'right';
  const shoulder=at(r[`${reach}Arm`]),wrist=at(r[`${reach}Hand`]),other=at(r[`${rest}Hand`]),chest=at(r.chest).y;
  assert.ok(wrist.y>shoulder.y+.25,`card ${index}: the ${reach} hand lifts (${(wrist.y-shoulder.y).toFixed(2)} m above the shoulder)`);
  assert.ok(other.y<chest-.2,`card ${index}: the ${rest} arm stays down`);
  const s=screen(stage,shoulder),w=screen(stage,wrist),c={x:l.cards[index][0]/l.w,y:l.cards[index][1]/l.h};
  const a=Math.atan2(w.y-s.y,w.x-s.x),b=Math.atan2(c.y-s.y,c.x-s.x);assert.ok(Math.abs(a-b)<.5,`card ${index}: on screen the arm points at the card (${Math.abs(a-b).toFixed(2)} rad)`);
  const head=new T.Vector3(0,0,1).applyQuaternion(r.head.getWorldQuaternion(new T.Quaternion()));assert.ok(cardLeft?head.x<-.25:head.x>.25,`card ${index}: the head turns to the card`);
 }
 // A card near the middle: the hand rises beside the head, never across the face.
 const stage=new DraftStage();run(stage,t=>({...base,time:t}),l,base.time,base.time+2,60,()=>2);
 const r=stage.pilot!,wrist=screen(stage,at(r.rightHand)),head=[-.17,.17].map(x=>screen(stage,at(r.head,x,.165,0)));
 assert.ok(wrist.x<Math.min(head[0].x,head[1].x),'the hand stays beside the head');
 sim.dispose();
});

test('hover reach blends smoothly between cards and matches at 30, 60 and 120 Hz',()=>{
 const sim=draftState(),base=sim.snapshot(),l=LAYOUTS['16:9'],start=base.draft!.readyAt;
 const schedule=(t:number)=>t<start+.5?-1:t<start+1.4?0:t<start+2.2?1:4;
 const results=PROBE_RATES.map(hz=>{const stage=new DraftStage();let last:T.Quaternion[]|undefined,jump=0;
  setView(l);stage.trayBottom=l.trayBottom;const dt=1/hz;
  for(let i=0;i<=Math.round(3.2*hz);i++){const t=start+i*dt;stage.cards=l.cards.map(([x,y])=>stage.toWorld(x,y,new T.Vector3())!);stage.hover=schedule(t);stage.update({...base,time:t},16/9,dt);
   const q=quats(stage.pilot!);if(last)jump=Math.max(jump,worst(q,last)/dt);last=q;}
  return {q:last!,jump};});
 // A pop would be a jump of a third of a radian or more in one 60 Hz frame; a deliberate reach peaks well below that.
 for(const r of results)assert.ok(r.jump<20,`no pops: the fastest joint turns at ${r.jump.toFixed(2)} rad/s`);
 // The presentation clock (breathing) runs on the same seconds at every rate; the springs are exact for held targets.
 assert.ok(worst(results[0].q,results[1].q)<.01&&worst(results[2].q,results[1].q)<.01,`frame rate independent (${worst(results[0].q,results[1].q).toFixed(4)}, ${worst(results[2].q,results[1].q).toFixed(4)})`);
 let x=0,v=0;for(let i=0;i<60;i++)[x,v]=spring(x,v,1,9,1/60);const [y]=spring(0,0,1,9,1);assert.ok(Math.abs(x-y)<1e-9,'the spring step is exact');
 sim.dispose();
});

test('the grab still reads inside the chest-up frame and keeps its authoritative timing',()=>{
 const sim=draftState(),l=LAYOUTS['16:9'];sim.time=sim.snapshot().draft!.readyAt;const offers=sim.snapshot().players.find(p=>p.id==='me')!.offers;
 const stage=new DraftStage();setView(l);stage.trayBottom=l.trayBottom;let s=sim.snapshot();
 for(let k=0;k<60;k++){stage.cards=l.cards.map(([x,y])=>stage.toWorld(x,y,new T.Vector3())!);stage.hover=1;stage.update({...s,time:s.time+k/60},16/9,1/60);}
 sim.time+=1;assert.ok(sim.selectChip('me',offers[1]));const chosenAt=sim.snapshot().draft!.chosenAt!;
 const palmAt=(e:number)=>{sim.time=chosenAt+e;s=sim.snapshot();stage.cards=l.cards.map(([x,y])=>stage.toWorld(x,y,new T.Vector3())!);stage.hover=-1;stage.taken=1;stage.update(s,16/9,1/60);const p={x:0,y:0};assert.ok(stage.palmScreen(p));return {x:p.x/l.w,y:p.y/l.h};};
 let last=palmAt(0);
 for(let e=1/60;e<=DRAFT_GRAB.details+.2;e+=1/60){const p=palmAt(e);assert.ok(p.x>0&&p.x<1&&p.y>l.trayBottom-.12&&p.y<1,`palm on screen at ${e.toFixed(2)} s`);assert.ok(Math.hypot(p.x-last.x,p.y-last.y)<.05,`the hand moves smoothly (${e.toFixed(2)} s)`);last=p;}
 const end=palmAt(DRAFT_GRAB.details);assert.ok(end.y>.6&&end.y<.98,'the chip is drawn down in front of the chest');
 assert.equal(stage.pilot!.sockets.leftHand.getObjectByName('draft-chip')?.visible??stage.pilot!.sockets.rightHand.getObjectByName('draft-chip')?.visible,true,'the chip sits in the palm');
 sim.dispose();
});

test('each turn pushes in from a wider shot without pops; reduced motion holds the close-up',()=>{
 const sim=draftState(),base=sim.snapshot(),l=LAYOUTS['16:9'],d=base.draft!;
 const stage=new DraftStage();setView(l);stage.trayBottom=l.trayBottom;const dist:number[]=[];
 for(let t=d.started;t<=d.started+1.2;t+=1/60){stage.update({...base,time:t},16/9,1/60);dist.push(stage.camera.position.z);}
 assert.ok(dist[0]>dist[dist.length-1]*1.12,'starts wider');assert.ok(Math.abs(dist[Math.round(DRAFT_FRAME.arrive*60)+5]-dist[dist.length-1])<1e-9,'settles when the push-in ends');
 for(let i=1;i<dist.length;i++)assert.ok(dist[i]<=dist[i-1]+1e-12&&dist[i-1]-dist[i]<.05,'a smooth, monotonic push-in');
 // The next picker's turn (new draft.started) arrives the same way.
 const next={...base,time:d.started+5,draft:{...d,started:d.started+5,player:'other',turn:2}};stage.update(next,16/9,1/60);assert.ok(stage.camera.position.z>dist[dist.length-1]*1.12,'the next turn starts wide again');
 const calm=new DraftStage();calm.trayBottom=l.trayBottom;calm.update({...base,time:d.started},16/9,1/60,1,false,true);assert.ok(Math.abs(calm.camera.position.z-dist[dist.length-1])<1e-6,'reduced motion: no push-in');
 sim.dispose();
});
test('the first layout measurement of a turn snaps the framing; later layout changes ease in',()=>{
 const sim=draftState(),base=sim.snapshot(),l=LAYOUTS['16:9'],d=base.draft!,turn=`${d.player}:${d.turn}:${d.started}`;setView(l);
 const framed=(trayBottom:number,key=turn,time=d.started+1)=>{const s=new DraftStage();s.trayBottom=trayBottom;s.layoutTurn=key;s.update({...base,time},16/9,1/60);return s.camera.position.clone();};
 // The stage's first frame of a draft runs before the overlay has measured it (a previous draft's two-row tray, or the
 // default): the first measurement for this turn snaps, so no slow reframe rides on top of the push-in.
 const stage=new DraftStage();stage.trayBottom=.5;stage.update({...base,time:d.started+1},16/9,1/60);
 stage.trayBottom=l.trayBottom;stage.layoutTurn=turn;stage.update({...base,time:d.started+1+1/60},16/9,1/60);
 assert.ok(stage.camera.position.distanceTo(framed(l.trayBottom,turn,d.started+1+1/60))<1e-9,'snapped to the measured tray');
 // A layout change later in the same turn (resize, a second row) eases instead of cutting.
 stage.trayBottom=.4;stage.update({...base,time:d.started+1+2/60},16/9,1/60);
 const from=framed(l.trayBottom).y,to=framed(.4).y,moved=(stage.camera.position.y-from)/(to-from);
 assert.ok(Math.abs(to-from)>.01&&moved>0&&moved<.2,`eases towards the new layout (${moved.toFixed(3)})`);
 // The next turn's first measurement snaps again.
 const next={...base,time:d.started+5,draft:{...d,started:d.started+5,turn:2}};stage.layoutTurn=`${d.player}:2:${d.started+5}`;stage.update(next,16/9,1/60);
 const snapped=new DraftStage();snapped.trayBottom=.4;snapped.layoutTurn=stage.layoutTurn;snapped.update(next,16/9,1/60);
 assert.ok(stage.camera.position.distanceTo(snapped.camera.position)<1e-9,'a new turn snaps to its own layout');
 sim.dispose();
});
test('holding the chip at the chest, the head glances down without drooping',()=>{
 const sim=draftState(),l=LAYOUTS['16:9'];sim.time=sim.snapshot().draft!.readyAt;const offers=sim.snapshot().players.find(p=>p.id==='me')!.offers;
 const stage=new DraftStage();setView(l);stage.trayBottom=l.trayBottom;let s=sim.snapshot();
 for(let k=0;k<60;k++){stage.cards=l.cards.map(([x,y])=>stage.toWorld(x,y,new T.Vector3())!);stage.hover=1;stage.update({...s,time:s.time+k/60},16/9,1/60);}
 sim.time+=1;assert.ok(sim.selectChip('me',offers[1]));const chosenAt=sim.snapshot().draft!.chosenAt!;let low=1;
 for(let e=0;e<=DRAFT_GRAB.details+1;e+=1/60){sim.time=chosenAt+e;s=sim.snapshot();stage.cards=l.cards.map(([x,y])=>stage.toWorld(x,y,new T.Vector3())!);stage.hover=-1;stage.taken=1;stage.update(s,16/9,1/60);
  const r=stage.pilot!,face=new T.Vector3(0,0,1).applyQuaternion(r.head.getWorldQuaternion(new T.Quaternion()));low=Math.min(low,face.y);}
 const r=stage.pilot!,face=new T.Vector3(0,0,1).applyQuaternion(r.head.getWorldQuaternion(new T.Quaternion()));
 assert.ok(face.y<-.05,`still looks down at the chip (${face.y.toFixed(3)})`);assert.ok(low>-.5,`never drops into a droop (lowest ${low.toFixed(3)})`);
 sim.dispose();
});
test('the pick keeps the tray in place: the header holds its pre-pick height and the flying copy drops the tray margin',async()=>{
 const {DraftView}=await import('../src/client/draft-view.js');const {readFileSync}=await import('node:fs');
 const sim=draftState(),pre=sim.snapshot(),d=pre.draft!;setView({w:390,h:844,trayBottom:.5,cards:[]});
 // A minimal stand-in for the overlay DOM: the header, the tray and two slots (the taken one has no card, so no fly copy).
 const box=(top:number,height:number)=>({top,bottom:top+height,height,left:20,width:160});
 let current:unknown;const root={addEventListener(){},querySelector:()=>current} as unknown as HTMLElement;
 // Each call re-renders the overlay (drawMenu replaces the markup); returns the new screen's inline custom properties.
 const overlay=(headerHeight:number,taken:boolean)=>{const props:Record<string,string>={};
  const slots=[0,1].map(i=>({dataset:{slot:pre.players.find(p=>p.id==='me')!.offers[i]},classList:{contains:(c:string)=>taken&&i===1&&c==='taken'},getBoundingClientRect:()=>box(227,183),querySelector:()=>null}));
  current={dataset:{} as Record<string,string>,style:{setProperty:(k:string,v:string)=>{props[k]=v;}},querySelector:(q:string)=>q==='.draft-top'?{getBoundingClientRect:()=>box(14,headerHeight)}:q==='.draft-tray'?{getBoundingClientRect:()=>box(217,200)}:null,querySelectorAll:()=>slots};return props;};
 const view=new DraftView(root),stage=new DraftStage(),before=overlay(183.2,false);view.update({...pre,time:d.readyAt+.5},stage,'me');
 assert.equal(stage.layoutTurn,`${d.player}:${d.turn}:${d.started}`,'the overlay tells the stage which turn it measured');
 assert.equal(before['--draft-top'],undefined,'nothing is held before the pick');
 // The pick re-renders the overlay without the hint and timer: the new header is shorter, but its pre-pick height is held.
 const chosen={...pre,time:d.readyAt+1,draft:{...d,chosen:pre.players.find(p=>p.id==='me')!.offers[1],chosenAt:d.readyAt+1}};
 const after=overlay(120.5,true);view.update(chosen,stage,'me');
 assert.equal(after['--draft-top'],'183.2px','the chosen header keeps its pre-pick height');
 // A different window size (resize during the grab) falls back to the natural layout.
 const resized=overlay(120.5,true);setView({w:844,h:390,trayBottom:.5,cards:[]});view.update(chosen,stage,'me');
 assert.equal(resized['--draft-top'],undefined,'no stale height after a resize');
 const css=readFileSync(new URL('../src/client/draft-screen.css',import.meta.url),'utf8');
 assert.match(css,/\.draft-screen\.is-chosen \.draft-top\{height:var\(--draft-top,auto\)\}/,'the chosen header uses the held height');
 assert.match(css,/\.draft-fly\.draft-tray\{[^}]*margin:0[;}]/,'the fixed-position flying copy does not inherit the tray margin');
 sim.dispose();
});
