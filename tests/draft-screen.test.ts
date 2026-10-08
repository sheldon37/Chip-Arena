import {test} from 'node:test';
import assert from 'node:assert/strict';
import * as T from 'three';
import {Simulation,initPhysics} from '../src/shared/simulation.js';
import {DRAFT_REVEAL_SECONDS} from '../src/shared/draft-settings.js';
import {DRAFT_GRAB,grabTimeline,draftRows,draftPerRow} from '../src/client/draft-timeline.js';
import {draftScreen,draftDetailsOpen,selectedChip,revealSeconds} from '../src/client/draft-presentation.js';
import {createCharacterRig,RIG_LENGTHS} from '../src/client/character-rig.js';
import {animateLobbyPose} from '../src/client/lobby-poses.js';
import {lookAt,reachFor,stageView} from '../src/client/draft-stage.js';
import {CHIPS} from '../src/shared/content.js';
await initPhysics();

test('offers line up horizontally: one row while they fit, two rows above that, one scrolling row on phones',()=>{
 assert.deepEqual(draftRows(0),[]);assert.deepEqual(draftRows(1),[1]);assert.deepEqual(draftRows(6),[6]);
 assert.deepEqual(draftRows(7),[4,3]);assert.deepEqual(draftRows(12),[6,6]);
 assert.deepEqual(draftRows(6,draftPerRow(960)),[3,3],'mid-width screens split six cards instead of scrolling');
 assert.deepEqual(draftRows(12,draftPerRow(390)),[12],'phones keep a single sideways-scrolling row');
 assert.deepEqual(draftRows(6,draftPerRow(844,390)),[6],'short landscape screens keep one row so the pilot stays visible');
 assert.equal(draftPerRow(1280),6);assert.equal(draftPerRow(1920),6);assert.equal(draftPerRow(960),5);assert.equal(draftPerRow(700),Infinity);
});

test('the grab finishes and the details open inside the shortest server reveal',()=>{
 assert.ok(DRAFT_GRAB.look<DRAFT_GRAB.contact&&DRAFT_GRAB.fly<DRAFT_GRAB.contact&&DRAFT_GRAB.contact<DRAFT_GRAB.details);
 assert.ok(DRAFT_GRAB.details<DRAFT_REVEAL_SECONDS-1.5,'leave the spotlight most of the reveal');
 assert.deepEqual(grabTimeline(-.3),grabTimeline(0),'a lagging snapshot never runs the grab backwards');
 let last=grabTimeline(0);
 for(let e=.02;e<=DRAFT_REVEAL_SECONDS;e+=.02){const g=grabTimeline(e);for(const k of ['reach','fly','fade','pull'] as const)assert.ok(g[k]>=last[k]-1e-12,k+' is monotonic');last=g;}
 const g=grabTimeline(DRAFT_GRAB.contact);assert.equal(g.contact,true);assert.equal(g.reach,1);assert.equal(g.fly,1);assert.equal(g.fade,1);
 assert.equal(grabTimeline(DRAFT_GRAB.details).details,true);assert.equal(grabTimeline(DRAFT_GRAB.details).pull,1);
});

function draft(local='me'){
 const sim=new Simulation({sandbox:true});sim.addPlayer('me','<b>Me</b>','ranged');sim.addPlayer('other','Other','melee');sim.phase='fight';
 sim.debug({action:'draft-preview'},'me');return sim;
}
test('own turn: real chip buttons in horizontal rows; another player sees the same cards disabled',()=>{
 const sim=draft(),s=sim.snapshot(),offers=s.players.find(p=>p.id==='me')!.offers;
 sim.time=s.draft!.readyAt;const ready=sim.snapshot();ready.players.find(p=>p.id==='me')!.name='<b>Me</b>';// the sim strips markup; the view must escape anyway
 const own=draftScreen(ready,'me'),watch=draftScreen(ready,'other');
 assert.match(own,/class="draft-screen\s+is-own"/);assert.match(own,/data-rows="1"/);assert.match(own,new RegExp(`--cols:${offers.length}`));
 for(const id of offers)assert.ok(own.includes(`data-chip="${id}"`)&&own.includes(`data-slot="${id}"`));
 assert.equal((own.match(/data-chip="[^"]+" aria-label/g)||[]).length,offers.length);
 assert.ok(!/<button[^>]*disabled[^>]*data-chip/.test(own),'cards are enabled once ready');
 assert.ok(watch.includes('is choosing')&&/<button[^>]*disabled[^>]*data-chip/.test(watch),'other viewers see the pick disabled');
 assert.ok(!own.includes('<b>Me</b>')&&own.includes('&lt;b&gt;Me&lt;/b&gt;'),'names are escaped');
 assert.ok(own.includes('id="timer"'));sim.dispose();
});
test('after the pick the chosen card stays for the grab, others shatter, and the spotlight waits for the hand',()=>{
 const sim=draft();sim.time=sim.snapshot().draft!.readyAt;const offers=sim.snapshot().players.find(p=>p.id==='me')!.offers;
 assert.ok(sim.selectChip('me',offers[1]));const picked=sim.snapshot(),chip=CHIPS.find(c=>c.id===offers[1])!;
 const early=draftScreen(picked,'me');
 assert.match(early,new RegExp(`class="draft-slot taken" data-slot="${offers[1]}"`));
 assert.equal((early.match(/class="chip-shatter"/g)||[]).length,offers.length-1);
 assert.equal(draftDetailsOpen(picked),false);assert.ok(!early.includes('selection-spotlight'),'no details before the chip is in hand');
 sim.time=picked.draft!.chosenAt!+DRAFT_GRAB.details;const later=sim.snapshot(),open=draftScreen(later,'me');
 assert.equal(draftDetailsOpen(later),true);assert.match(open,/selection-spotlight/);
 const rest=+(revealSeconds(chip)-DRAFT_GRAB.details).toFixed(3);assert.ok(open.includes(`--selection-duration:${rest}s`),'spotlight ends with the server reveal');
 assert.ok(selectedChip(chip,1,'x',1.5).includes('--selection-delay:-1.5s'),'default spotlight timing is unchanged');
 sim.dispose();
});

test('look and reach layers aim the head and hand without breaking the rig',()=>{
 const r=createCharacterRig('#ff6b35');r.group.position.y=.88;const p=draft().snapshot().players.find(q=>q.id==='me')!;
 const bones=()=>{r.group.updateMatrixWorld(true);const a=new T.Vector3(),b=new T.Vector3(),c=new T.Vector3();r.leftArm.getWorldPosition(a);r.leftElbow.getWorldPosition(b);r.leftHand.getWorldPosition(c);return [a.distanceTo(b),b.distanceTo(c)];};
 for(const target of [new T.Vector3(-1.2,.6,1.6),new T.Vector3(1.4,.3,1.6),new T.Vector3(0,1.4,3)]){
  animateLobbyPose(r,p,1,.016,'weapon');const before=bones();lookAt(r,target,1);r.group.updateMatrixWorld(true);
  const eye=new T.Vector3(0,.165,0);r.head.localToWorld(eye);const forward=new T.Vector3(0,0,1).applyQuaternion(r.head.getWorldQuaternion(new T.Quaternion()));
  const want=target.clone().sub(eye).normalize();assert.ok(forward.angleTo(want)<.12,`head faces ${target.toArray()} (${forward.angleTo(want).toFixed(3)} rad)`);
  const after=bones();after.forEach((l,i)=>assert.ok(Math.abs(l-before[i])<1e-6,'bone lengths are kept'));
 }
 animateLobbyPose(r,p,1,.016,'weapon');const card=new T.Vector3(1.5,.2,1.6);reachFor(r,true,card,1,new T.Euler(-1.45,0,0));r.group.updateMatrixWorld(true);
 const shoulder=r.leftArm.getWorldPosition(new T.Vector3()),wrist=r.leftHand.getWorldPosition(new T.Vector3());
 assert.ok(wrist.distanceTo(shoulder)<=RIG_LENGTHS.upperArm+RIG_LENGTHS.forearm+1e-6);
 const toward=card.clone().sub(shoulder).normalize(),reach=wrist.clone().sub(shoulder).normalize();assert.ok(reach.angleTo(toward)<.35,'the hand reaches towards the card');
 r.group.traverse(o=>{for(const v of [o.position.x,o.position.y,o.position.z,o.quaternion.x,o.quaternion.w])assert.ok(Number.isFinite(v));});
});
test('a picker who died last round is staged alive and idle, not in their fight state',()=>{
 const p=draft().snapshot().players.find(q=>q.id==='me')!;
 const dead={...p,hp:0,death:{id:'d',at:1} as any,knockdown:{at:1} as any,hitReaction:{at:1} as any,animationTime:3};
 const v=stageView(dead);assert.ok(v.hp>0);assert.equal(v.death,undefined);assert.equal(v.knockdown,undefined);assert.equal(v.hitReaction,undefined);assert.equal(v.animationTime,undefined);
 const a=createCharacterRig('#ff6b35'),b=createCharacterRig('#ff6b35');a.group.position.y=b.group.position.y=.88;
 animateLobbyPose(a,stageView(dead),2,.016,'weapon');animateLobbyPose(b,stageView(p),2,.016,'weapon');a.group.updateMatrixWorld(true);b.group.updateMatrixWorld(true);
 const head=(r:typeof a)=>r.head.getWorldPosition(new T.Vector3());assert.ok(head(a).distanceTo(head(b))<1e-6,'same standing pose as a living pilot');
});
