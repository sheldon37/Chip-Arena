// Offline TEST BOSS input sync (2026-10-03 playtest: "my gun would shoot meters behind me, and some rubberbanding" after Esc,
// pendingInputs 63 at 7 ms RTT). Drives the real LocalRoom + HostedRoom(local) + Predictor the way main.ts does: one input
// per 60 Hz client tick through LocalRoom.send, prediction on every unpaused tick, reconcile on every snapshot, idle inputs
// while a hold or the chip screen is open. The worker double mirrors src/client/host-worker.ts (no steps while paused).
import test from 'node:test';
import assert from 'node:assert/strict';
import {initPhysics,type Snapshot} from '../src/shared/simulation.js';
import {HostedRoom} from '../src/shared/host-room.js';
import {idleInput,len,sub,type Input} from '../src/shared/config.js';
import {PVE_CONTRACT} from '../src/shared/pve/contract.js';
import {LocalRoom,type WorkerLike} from '../src/client/pve/local-room.js';
import {Predictor} from '../src/client/prediction.js';

await initPhysics();

class WorkerDouble implements WorkerLike{
 onmessage:((e:{data:any})=>void)|null=null;onerror:((e:unknown)=>void)|null=null;room:HostedRoom|null=null;paused=false;ticks=0;
 postMessage(m:any){
  if(m.type==='create'){this.room=new HostedRoom(m.code,m.profile,m.bothDraft,(target,message)=>this.onmessage?.({data:{target,message}}),m.draftChoices,true,m.skipIntros===true,m.selectionSeconds,m.settings);this.room.welcome('host');}
  // Virtual wall clock (60 Hz) for the per-second message rate limit.
  else if(m.type==='message')this.room?.receive(m.id,m.message,this.ticks*1000/60);
  else if(m.type==='pause'&&this.room?.local)this.paused=m.paused===true;
 }
 tick(){this.ticks++;if(!this.paused)this.room!.step();}
 terminate(){this.room?.dispose();this.room=null;}
}

interface Run{lag:number[];align:number[];pendingMax:number;pendingAfterResume:number[];alignMax:number;alignAfterResume:number[];holdTicks:number;chipTicks:number;pauses:number}
/** `leak`: reproduce the defect by also posting fight inputs to the paused worker (the pre-fix client path). */
function play(leak=false):Run{
 const worker=new WorkerDouble();let state:Snapshot|undefined,predictor=new Predictor(),n=0;
 const run:Run={lag:[],align:[],pendingMax:0,pendingAfterResume:[],alignMax:0,alignAfterResume:[],holdTicks:0,chipTicks:0,pauses:0};
 const predicted=new Map<number,{x:number;y:number;z:number}>();let measuring=false,afterResume=false,lastSeq=0,paused=false;
 const room=new LocalRoom({name:'Pilot',class:'ranged',color:'#70e1d0'},{pve:{boss:'siren',partyBots:3},local:true},m=>{
  if(m.type!=='snapshot')return;state=m.state as Snapshot;const me=state.players.find(p=>p.id==='host');if(!me)return;
  predictor.reconcile(state,me);
  // Alignment: the authoritative pose after input `ack` against what the client predicted for that same input.
  const mine=predicted.get(me.ack);if(measuring&&mine&&state.phase==='fight'&&!state.pve?.hold&&me.hp>0){const d=len(sub(me.pos,mine));run.alignMax=Math.max(run.alignMax,d);run.align.push(d);const now=predicted.get(lastSeq);if(now&&!paused)run.lag.push(len(sub(me.pos,now)));if(afterResume)run.alignAfterResume.push(d);}
 },()=>{},()=>{},{skipIntros:true,worker:()=>worker});
 const pve=(cmd:object)=>room.send({type:'chip',pve:{...cmd,n:++n}});
 const until=(check:()=>boolean,label:string,max=60*60)=>{for(let i=0;i<max;i++){if(check())return;worker.tick();}assert.fail('timeout: '+label);};
 try{
  pve({action:'hello',v:PVE_CONTRACT,key:'0123456789abcdef0123456789abcdef'});room.send({type:'ready',ready:true});
  until(()=>state?.phase==='specialization','specialization');room.send({type:'spec',id:'pistol'});
  until(()=>state?.phase==='fight'&&!!state.pve?.party.host,'fight');
  // Untouchable (debug god): hits and knockback are authoritative-only by design, not input sync.
  worker.room!.sim.pve!.debug('god:on');
  let seq=0,yaw=0;
  const me=()=>state!.players.find(p=>p.id==='host')!;
  // One client tick, as main.ts frame(): pause sync before stepping, idle input while paused / holding / choosing.
  const tick=(t:number)=>{
   room.pause(paused);
   const idle=paused||!!state!.pve?.hold||!!state!.pve?.party.host?.open;
   if(state!.pve?.hold)run.holdTicks++;if(state!.pve?.party.host?.open)run.chipTicks++;
   yaw+=.012;const i:Input=idle?{...idleInput(),seq:++seq,yaw}:{...idleInput(),seq:++seq,x:Math.sin(t/45),z:Math.cos(t/70),yaw,attack:t%40<6,jump:t%150===0};
   const msg={type:'inputs',epoch:`${state!.setNumber}:${state!.round}:${state!.generation||0}`,inputs:[i]};
   room.send(msg);if(leak&&paused)worker.postMessage({type:'message',id:'host',message:msg});
   if(!paused&&me().hp>0){predictor.step(i,1/60,me());predicted.set(i.seq,{...predictor.pos});lastSeq=i.seq;}
   worker.tick();
   if(state!.phase==='fight')run.pendingMax=Math.max(run.pendingMax,predictor.pending.length);
   if(afterResume)run.pendingAfterResume.push(predictor.pending.length);
  };
  let t=0;measuring=true;
  // The start pick is open (forced idle); choose later, walk and shoot, reopen with C and close again.
  for(;t<120;t++)tick(t);pve({action:'close'});for(;t<360;t++)tick(t);
  pve({action:'open'});for(;t<480;t++)tick(t);pve({action:'close'});for(;t<720;t++)tick(t);
  // Esc → game menu for 3 s (the worker pauses), then Resume.
  paused=true;run.pauses++;for(;t<900;t++)tick(t);paused=false;afterResume=true;for(;t<1500;t++)tick(t);
  // A hold: skipping to the boss plays the reveal cinematics (forced idle), then fight on.
  worker.room!.sim.pve!.debug('skip-to-boss');
  for(let k=0;k<60*30&&!(state!.pve?.stage==='boss'&&!state!.pve.hold&&!state!.pve.cinematic);k++,t++)tick(t);
  for(let k=0;k<600;k++,t++)tick(t);
  // A second pause during the boss stage.
  paused=true;run.pauses++;for(let k=0;k<120;k++,t++)tick(t);paused=false;for(let k=0;k<600;k++,t++)tick(t);
  return run;
 }finally{room.close();predictor.dispose();}
}

const q=(a:number[],f:number)=>a.slice().sort((x,y)=>x-y)[Math.floor((a.length-1)*f)]??0;
const stats=(r:Run)=>({pendingMax:r.pendingMax,pendingTail:Math.max(...r.pendingAfterResume.slice(-300)),alignP95:q(r.align,.95),alignMax:r.alignMax,lagP50:q(r.lag,.5),lagP95:q(r.lag,.95),lagMax:q(r.lag,1)});
test('offline boss run: pause, holds and the chip screen leave no input backlog and prediction matches the authority',()=>{
 const r=play();
 if(process.env.SYNC_DEBUG)console.log(stats(r));
 assert.ok(r.holdTicks>60,'a hold was exercised');assert.ok(r.chipTicks>200,'the chip screen was open');assert.equal(r.pauses,2);
 // Snapshots arrive every 3 ticks with no transport delay: a few frames of pending input at most.
 assert.ok(r.pendingMax<=6,`pending inputs ${r.pendingMax}`);
 assert.ok(Math.max(...r.pendingAfterResume)<=6,`pending after resume ${Math.max(...r.pendingAfterResume)}`);
 assert.ok(r.alignAfterResume.length>100,'alignment sampled after resume');
 // Where the authority put me for my newest input vs where I predicted it (what a shot spawns from). Rare single samples
 // differ by contact with adds, party bots or the boss (the predictor has no other bodies); p95 must be exact.
 const s=stats(r);assert.ok(s.alignP95<.01,`p95 predicted vs authoritative ${s.alignP95.toFixed(4)} m`);
 assert.ok(s.lagP95<.01,`p95 lag ${s.lagP95.toFixed(4)} m`);assert.ok(s.lagMax<1,`max lag ${s.lagMax.toFixed(3)} m`);
 assert.ok(q(r.alignAfterResume,.95)<.01,'aligned after resume');
});

test('control: inputs reaching the paused worker leave a permanent backlog (the 2026-10-03 desync)',()=>{
 const r=play(true);
 if(process.env.SYNC_DEBUG)console.log(stats(r));
 const tail=r.pendingAfterResume.slice(-300);
 assert.ok(Math.min(...tail)>=60,`backlog persists long after resume: min pending ${Math.min(...tail)}`);
 // ...and the authority trails the predicted pilot by metres: shots leave from behind, corrections rubberband.
 assert.ok(stats(r).lagP50>1,`median lag ${stats(r).lagP50.toFixed(2)} m`);
});
