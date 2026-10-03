// Pointer-lock mouse-look sanitising (2026-10-03 playtest: "the camera will pull another direction randomly and instantly").
import test from 'node:test';
import assert from 'node:assert/strict';
import {MouseLookFilter,MOUSE_LOOK} from '../src/client/mouse-look.js';

/** Feeds [dx,dy,ms-after-previous] events and returns the camera yaw change (px) the accepted ones produce. */
function feed(f:MouseLookFilter,events:[number,number,number][],start=1000){let t=start,x=0,y=0;const kept:boolean[]=[];
 for(const[dx,dy,gap]of events){t+=gap;const ok=f.accept(dx,dy,t);kept.push(ok);if(ok){x+=dx;y+=dy;}}return {x,y,kept,t};}
const locked=(at=0)=>{const f=new MouseLookFilter();f.lock(at);return f;};

test('ordinary aiming passes untouched at 60 Hz and 1000 Hz event rates',()=>{
 const slow=Array.from({length:240},(_,i)=>[Math.round(12*Math.sin(i/9)),Math.round(5*Math.cos(i/13)),1000/60] as [number,number,number]);
 const a=feed(locked(),[[0,0,200],...slow]);assert.ok(a.kept.every(Boolean),'60 Hz tracking kept');
 const fast=Array.from({length:2000},(_,i)=>[Math.round(3*Math.sin(i/40)),i%7===0?1:0,1] as [number,number,number]);
 assert.ok(feed(locked(),[[0,0,200],...fast]).kept.every(Boolean),'1000 Hz tracking kept');
});

test('fast flicks that ramp up are kept in full, including huge per-frame deltas',()=>{
 // A 180-degree flick at sensitivity 1 is ~1500 px; peak events of 450-600 px per 60 Hz frame.
 const flick:[number,number,number][]=[[0,0,300],[8,0,16],[40,1,16],[120,3,16],[260,4,16],[450,6,17],[600,4,16],[380,2,16],[150,0,16],[30,0,16]];
 const r=feed(locked(),flick);assert.deepEqual(r.kept,flick.map(()=>true));assert.equal(r.x,2038);
 // ...and the other way, after a pause.
 const back=feed(locked(),[[0,0,300],...flick.map(([x,y,g])=>[-x,-y,g] as [number,number,number])]);assert.ok(back.kept.every(Boolean));
});

test('a frame hitch: a coalesced large delta during steady motion is kept',()=>{
 const steady=Array.from({length:30},()=>[60,0,16] as [number,number,number]);
 const r=feed(locked(),[[0,0,300],...steady,[700,0,180],[60,0,16]]);assert.ok(r.kept.every(Boolean));
});

test('a lone spike mid-play is dropped; aiming continues',()=>{
 const before=Array.from({length:60},(_,i)=>[Math.round(6*Math.sin(i/7)),0,16] as [number,number,number]);
 const r=feed(locked(),[[0,0,300],...before,[-960,12,4],...before]);
 assert.equal(r.kept.filter(k=>!k).length,1,'exactly the spike');assert.equal(r.kept[61],false);
 // Spike and its return (opposite pair) are both dropped.
 const pair=feed(locked(),[[0,0,300],...before,[700,0,3],[-700,0,3],...before]);
 assert.deepEqual([pair.kept[61],pair.kept[62]],[false,false]);assert.equal(pair.kept.filter(k=>!k).length,2);
 // Right after the hand stops (no events for 300 ms), a spike is still a spike.
 const steady=Array.from({length:30},()=>[60,0,16] as [number,number,number]);
 assert.equal(feed(locked(),[[0,0,300],...steady,[800,0,300]]).kept.at(-1),false);
 // A vertical spike too (pitch).
 assert.equal(feed(locked(),[[0,0,300],...before,[0,540,8]]).kept.at(-1),false);
});

test('a flick that starts with one giant event loses only that event',()=>{
 const r=feed(locked(),[[0,0,500],[400,0,16],[420,0,16],[300,0,16],[100,0,16]]);
 assert.deepEqual(r.kept,[true,false,true,true,true]);
});

test('re-locking (Esc menu Resume, chip screen, alt-tab): the cursor-to-centre jump is dropped',()=>{
 const f=locked(0);feed(f,Array.from({length:30},()=>[5,0,16] as [number,number,number]),0);
 f.lock(5000);
 // Chromium reports the warp to the lock centre on the first move, sometimes a little later.
 assert.equal(f.accept(-212,87,5004),false,'first event after lock');
 assert.equal(f.accept(90,0,5040),false,'still settling');
 assert.equal(f.accept(4,1,5060),true,'small moves pass while settling');
 assert.equal(f.accept(30,0,5000+MOUSE_LOOK.settle+20),true,'normal motion after the settle window');
 // First event long after the lock (the player kept the mouse still): a big jump is still the warp.
 const g=locked(0);assert.equal(g.accept(300,0,3000),false);assert.equal(g.accept(20,0,3016),true);
 // A gentle first move is not delayed.
 const h=locked(0);assert.equal(h.accept(6,-2,900),true);
});

test('garbage values never reach the camera',()=>{
 const f=locked(0);
 for(const[x,y]of [[NaN,0],[0,Infinity],[-Infinity,3]])assert.equal(f.accept(x,y,500),false);
 assert.equal(f.accept(3,0,520),true);
});
