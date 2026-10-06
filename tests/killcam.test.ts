import {test} from 'node:test';
import assert from 'node:assert/strict';
import {Killcam,KILLCAM,sampleReplay} from '../src/client/killcam.js';
import {Simulation,initPhysics,type Snapshot,type DeathPresentation} from '../src/shared/simulation.js';
import {v} from '../src/shared/config.js';
await initPhysics();
const sim=new Simulation({sandbox:true});sim.addPlayer('me');sim.addPlayer('killer');sim.phase='fight';
const base=sim.snapshot();base.props=[];sim.dispose();
const death:DeathPresentation={id:'death-1',at:4,source:'killer',kind:'direct',announcement:{victim:'Me',killer:'Killer'},effect:'impact-ragdoll',seed:1,pos:v(),velocity:v(),direction:v(0,0,1),yaw:0};
function state(time:number,dead=time>=4):Snapshot{const s=structuredClone(base);s.time=time;s.tick=Math.round(time*60);s.players[0].hp=dead?0:100;s.players[0].death=dead?structuredClone(death):undefined;s.players[1].pos.x=time;s.players[1].yaw=time;return s;}
// Timing pins (owner 2026-10-04: the ~4 s aftermath "feels too long", use Call of Duty logic): 3 s lead-in from the killer's
// view, 1.25 s past the kill, .6 s live hold; ~4.85 s in all, so a round-ending kill fits the 5 s result screen.
// Was 3 / .6 / .6, then 2 / 4 / 1.2, then 1.5 / 4 / .6. Death at 4 s, so footage runs 1 s → 5.25 s and starts at live 4.6 s.
assert.deepEqual([KILLCAM.before,KILLCAM.after,KILLCAM.hold],[3,1.25,.6]);
assert.ok(KILLCAM.history>=KILLCAM.before+KILLCAM.hold+.5&&KILLCAM.maxFrames>=KILLCAM.history*KILLCAM.hz);
const START=4+KILLCAM.hold,START_MS=Math.round(START*1000),LAST=Math.round(START*20);
function ready(cam=new Killcam()) {for(let i=0;i<=LAST;i++)cam.observe(state(i/20),'me',i*50);const live=state(START);return {cam,live,frame:cam.sample(live,'me',START_MS)!};}
/** Live keeps running during playback (the client records after death): observe and sample in 50 ms steps. */
function play(cam:Killcam,fromMs:number,toMs:number,mutate=(_s:Snapshot)=>{}){const out:(Snapshot|undefined)[]=[];
 for(let ms=fromMs;ms<=toMs;ms+=50){const live=state(ms/1000);mutate(live);cam.observe(live,'me',ms);out.push(cam.sample(live,'me',ms));}return out;}

test('replays 3 seconds before a witnessed death, then ends without changing live snapshots',()=>{
 const {cam,live,frame}=ready();const before=JSON.stringify(live);
 assert.equal(cam.active,true);assert.equal(frame.time,1);assert.equal(frame.players[0].hp,100);assert.equal(cam.info?.killerId,'killer');assert.equal(cam.info?.viewId,'killer');
 assert.equal(cam.info?.start,1);assert.equal(cam.info?.end,5.25);assert.equal(cam.info?.at,4);
 assert.equal(cam.sample(live,'me',START_MS+3200)?.players[0].death?.id,'death-1');assert.equal(JSON.stringify(live),before);
 const end=play(cam,START_MS+50,START_MS+6100);assert.equal(end.at(-1),undefined);assert.equal(cam.active,false);
 assert.equal(cam.sample(state(11.4),'me',11400),undefined,'the same death never automatically repeats');
});
test('keeps watching briefly past the kill: the aftermath streams in while the clip plays',()=>{
 const {cam}=ready();const frames=play(cam,START_MS+50,START_MS+6100),times=frames.map(f=>f?.time);
 const last=frames.filter(f=>f).at(-1)!;
 assert.ok(last.time>=4+KILLCAM.after-.06&&last.time<=4+KILLCAM.after,`last replay frame ${last.time}`);
 assert.equal(last.players[0].hp,0);assert.equal(last.players[0].death?.id,'death-1');
 assert.ok(Math.abs(last.players[1].pos.x-last.time)<1e-6,'killer keeps moving in the recorded aftermath, not frozen at the death frame');
 assert.equal(cam.info,undefined);assert.ok(times.every((t,i)=>t===undefined||i===0||t>=times[i-1]!),'monotonic playback');
});
test('aftermath camera stays on the killer, then cuts once to the body when the killer dies or leaves',()=>{
 for(const [label,mutate] of [['dies',(s:Snapshot)=>{if(s.time>=4.6)s.players[1].hp=0;}],['leaves',(s:Snapshot)=>{if(s.time>=4.6)s.players[1].pos.x=KILLCAM.bodyRange+5;}]] as const){
  const cam=new Killcam();for(let i=0;i<=LAST;i++){const s=state(i/20);mutate(s);cam.observe(s,'me',i*50);}cam.sample(state(START),'me',START_MS);
  const views:string[]=[];for(let ms=START_MS+50;ms<=START_MS+6000;ms+=50){const s=state(ms/1000);mutate(s);cam.observe(s,'me',ms);if(cam.sample(s,'me',ms))views.push(cam.info!.viewId);}
  const cut=views.indexOf('me');assert.ok(cut>0,label);assert.ok(views.slice(0,cut).every(v=>v==='killer'),label);assert.ok(views.slice(cut).every(v=>v==='me'),label+': never cuts back');
 }
});
test('history owns nested data and stays bounded during a long match',()=>{
 const cam=new Killcam();for(let i=0;i<500;i++)cam.observe(state(i/20,false),'me',i*50);
 assert.ok(cam.bufferedFrames<=KILLCAM.maxFrames);assert.ok(cam.bufferedEntities<=KILLCAM.maxEntities);
 const early=state(25,false);cam.observe(early,'me',25000);early.players[1].pos.x=999;early.players[1].chips.vital=999;
 const fatal=state(25.1);fatal.players[0].death!.at=25.1;cam.observe(fatal,'me',25100);
 const replay=cam.sample(fatal,'me',25100+KILLCAM.waitMs+KILLCAM.hold*1000+1)!;assert.ok(replay.players[1].pos.x<30);assert.equal(replay.players[1].chips.vital,undefined);
});
test('does not trigger for a late-joining dead player, no history, or another player death',()=>{
 const cam=new Killcam();cam.observe(state(4),'me',4000);assert.equal(cam.sample(state(4.6),'me',6000),undefined);
 cam.clear();cam.observe(state(0),'me',0);cam.observe(state(.05,true),'me',50);assert.equal(cam.waiting,false);
 cam.clear();for(let i=0;i<10;i++){const s=state(i/20,false);s.players[1].hp=0;s.players[1].death=death;cam.observe(s,'me',i*50);}assert.equal(cam.waiting,false);
});
test('skip cancels both tail collection and playback without restarting on repeated death snapshots',()=>{
 for(const duringPlayback of [false,true]){const cam=new Killcam();for(let i=0;i<=(duringPlayback?LAST:80);i++)cam.observe(state(i/20),'me',i*50);
  if(duringPlayback)assert.ok(cam.sample(state(START),'me',START_MS));cam.skip();cam.observe(state(START+.1),'me',START_MS+100);
  assert.equal(cam.sample(state(START+.1),'me',START_MS+1400),undefined);assert.equal(cam.waiting,false);}
});
test('respawn, identity, map/round generation, lobby return and drafts interrupt replay',()=>{
 const changes=[(s:Snapshot)=>s.players[0].hp=100,(s:Snapshot)=>s.generation=(s.generation||0)+1,(s:Snapshot)=>s.round++,(s:Snapshot)=>s.options.map='siren',
  (s:Snapshot)=>s.phase='draft',(s:Snapshot)=>s.phase='lobby',(s:Snapshot)=>s.players[0].connected=false,(s:Snapshot)=>s.lobbyReturn={at:4,until:8} as any];
 for(const change of changes){const {cam,live}=ready();change(live);assert.equal(cam.sample(live,'me',START_MS+100),undefined);assert.equal(cam.active,false);}
 const {cam,live}=ready();assert.equal(cam.sample(live,'new-id',START_MS+100),undefined);
});
test('round-result transition preserves the death clip; a stalled connection uses a finite deadline',()=>{
 const cam=new Killcam();for(let i=0;i<=80;i++)cam.observe(state(i/20),'me',i*50);
 const live=state(4);live.phase='result';cam.observe(live,'me',4001);
 const deadline=4000+KILLCAM.waitMs+KILLCAM.hold*1000;
 assert.equal(cam.sample(live,'me',4500),undefined);assert.equal(cam.sample(live,'me',deadline-1),undefined);assert.equal(cam.waiting,true);
 assert.ok(cam.sample(live,'me',deadline+1));assert.equal(cam.info?.end,5.25);
 // No packets after 4 s: footage runs out, so the clip ends .5 s after its last frame instead of freezing to the end.
 assert.ok(cam.sample(live,'me',deadline+1+3400));assert.equal(cam.sample(live,'me',deadline+1+3600),undefined);
});
test('environmental, self-inflicted and unavailable killer deaths fall back to recorded victim camera',()=>{
 for(const source of [null,'me','absent']){const cam=new Killcam();for(let i=0;i<=LAST;i++){const s=state(i/20);if(s.players[0].death){s.players[0].death.source=source;s.players[0].death.announcement!.killer=null;}cam.observe(s,'me',i*50);}
  assert.ok(cam.sample(state(START),'me',START_MS));assert.equal(cam.info?.viewId,'me');}
});
test('snapshot interpolation never borrows future death, damage, attack state, destruction or projectile membership',()=>{
 const a=state(3.9),b=state(4);a.players[1].lastAttack=2;b.players[1].lastAttack=4;
 a.props=[{id:'cover',pos:v(),rot:{x:0,y:0,z:0,w:1},size:v(1,1,1),hp:10,maxHp:10,mass:1,dynamic:false,material:'wood',fragment:false,vel:v()}];b.props=[];
 b.projectiles=[{id:'future',pos:v(),vel:v(),owner:'killer',spec:'pistol',radius:.1,reflections:0,elements:[]}];
 const before=JSON.stringify([a,b]),middle=sampleReplay([a,b],3.95),end=sampleReplay([a,b],4);
 assert.equal(middle.players[0].hp,100);assert.equal(middle.players[0].death,undefined);assert.equal(middle.players[1].lastAttack,2);
 assert.equal(middle.props.length,1);assert.equal(middle.projectiles.length,0);assert.ok(Math.abs(middle.players[1].pos.x-3.95)<1e-8);
 assert.equal(end.players[0].hp,0);assert.equal(end.props.length,0);assert.equal(end.projectiles.length,1);assert.equal(JSON.stringify([a,b]),before);
});
test('out of order and duplicate snapshots do not rewind retained history or repeat a death',()=>{
 const {cam,live}=ready();const n=cam.bufferedFrames;cam.observe(state(3),'me',START_MS+100);cam.observe(live,'me',START_MS+200);
 assert.equal(cam.bufferedFrames,n);assert.equal(cam.active,true);assert.ok(cam.sample(live,'me',START_MS+100)!.time>1);
});
test('the replay holds on the live death for the configured pause before footage starts',()=>{
 const cam=new Killcam();for(let i=0;i<=90;i++)cam.observe(state(i/20),'me',i*50);
 assert.equal(cam.sample(state(4.5),'me',4500),undefined,'still holding live on the death');assert.equal(cam.waiting,true);assert.equal(cam.active,false);
 for(let i=91;i<=LAST;i++)cam.observe(state(i/20),'me',i*50);
 assert.ok(cam.sample(state(START),'me',START_MS));assert.equal(cam.info?.start,1);assert.equal(cam.info?.end,4+KILLCAM.after);
});
