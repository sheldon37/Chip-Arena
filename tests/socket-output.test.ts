import {test} from 'node:test';
import assert from 'node:assert/strict';
import type {WebSocket} from 'ws';
import {SocketOutput} from '../src/server/socket-output.js';
import {ConnectionMetrics} from '../src/client/connection-metrics.js';

test('slow writes discard obsolete snapshots while preserving ordered control messages',()=>{
 const sent:{packet:string;options:any;done?:Function}[]=[];
 const socket={readyState:1,bufferedAmount:0,extensions:'permessage-deflate',send:(packet:string,options:any,done?:Function)=>sent.push({packet,options,done}),close:()=>{}} as unknown as WebSocket;
 const output=new SocketOutput();assert.equal(output.send(socket,'state 1',true),true);
 for(let n=2;n<100;n++)assert.equal(output.send(socket,'state '+n,true),false);
 assert.equal(output.send(socket,'pong'),true);assert.deepEqual(sent.map(s=>s.packet),['state 1','pong']);assert.equal(sent[1].options.compress,false);
 sent[0].done!();assert.equal(output.send(socket,'state 100',true),true);assert.equal(sent[2].packet,'state 100');assert.equal(output.diagnostics(socket).droppedSnapshots,98);
});
test('socket buffer congestion cannot accumulate world states',()=>{
 let sends=0,closed=false;
 const socket={readyState:1,bufferedAmount:70000,extensions:'',send:()=>sends++,close:()=>closed=true} as unknown as WebSocket;const output=new SocketOutput();
 assert.equal(output.send(socket,'snapshot',true),false);assert.equal(sends,0);assert.equal(output.send(socket,'notice'),true);assert.equal(sends,1);
 Object.assign(socket,{bufferedAmount:300000});assert.equal(output.send(socket,'notice'),false);assert.equal(closed,true);
});
test('ping measures elapsed time without comparing clocks and rejects stale/duplicate echoes',()=>{
 let now=100;const metrics=new ConnectionMetrics(()=>now),ping=metrics.ping();now=142;
 assert.ok(metrics.pong({...ping,network:{compression:true,loopP95Ms:10}}));assert.equal(metrics.rtt,42);assert.equal(metrics.summary.server.compression,true);
 now=9000;assert.equal(metrics.pong(ping),false);assert.equal(metrics.rtt,42);
 const old=metrics.ping();metrics.reset();now+=1000;assert.equal(metrics.pong(old),false);assert.equal(metrics.rtt,0);
});
