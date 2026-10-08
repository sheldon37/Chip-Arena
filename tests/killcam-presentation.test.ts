import {test} from 'node:test';
import assert from 'node:assert/strict';
import {killcamHud,killcamReturnHud,killcamCause,killcamClock,KILLCAM_EXIT} from '../src/client/killcam-hud.js';
import {replayShoulder,REPLAY_CAMERA} from '../src/client/killcam-camera.js';
import {VIEW} from '../src/shared/view.js';
import type {KillcamInfo} from '../src/client/killcam.js';

const info=(over:Partial<KillcamInfo>={}):KillcamInfo=>({deathId:'death-7',victimId:'me',killerId:'k',killerName:'Killer',cause:'direct',at:3,start:0,end:3.6,time:1,progress:1/3.6,viewId:'k',...over});

test('overlay escapes names, keeps the skip action and labels killer or environmental deaths',()=>{
 const html=killcamHud(info({killerName:'<img src=x onerror=alert(1)>"&\''}),{killerColor:'#123456',spec:'pistol',weapon:'<b>Pistol</b>',team:{name:'<i>Red</i>',color:'#ff0000'}});
 assert.ok(!html.includes('<img')&&!html.includes('<b>')&&!html.includes('<i>Red'));
 assert.ok(html.includes('&lt;img src=x onerror=alert(1)&gt;&quot;&amp;&#39;'));
 assert.ok(html.includes('data-action="killcam-skip"'));
 assert.ok(html.includes('Eliminated by')&&html.includes('Killer’s view'));
 const self=killcamHud(info({killerId:null,killerName:null,cause:'lava',viewId:'me'}));
 assert.ok(self.includes('You were eliminated')&&self.includes('>Lava<')&&self.includes('Your final moments'));
});
test('overlay rejects unsafe colours and pins the fatal-hit marker on the timeline',()=>{
 const html=killcamHud(info(),{killerColor:'red;background:url(x)'});
 assert.ok(!html.includes('url(x)')&&html.includes('--kc-accent:#ff6b35'));
 assert.ok(html.includes('--kc-mark:83.33%'));
 assert.ok(html.includes('aria-valuenow="28"'));
});
test('clock counts to the kill then the aftermath; closing state only in the final moments',()=>{
 assert.equal(killcamClock(info({time:1})),'−2.0s');assert.equal(killcamClock(info({time:3})),'0.0s');assert.equal(killcamClock(info({time:3.4})),'+0.4s');
 assert.ok(!killcamHud(info({time:3.6-KILLCAM_EXIT-.05})).includes(' out'));
 assert.ok(killcamHud(info({time:3.6-KILLCAM_EXIT/2})).includes('kc-letterbox kc-top out'));
 assert.equal(killcamCause('dagger-throw'),'Thrown dagger');assert.equal(killcamCause('mystery_beam'),'Mystery beam');
 assert.ok(killcamReturnHud('"x"').includes('data-return="&quot;x&quot;"'));
});
test('replay shoulder pivot shifts right of the aim and never into a wall',()=>{
 const pos={x:1,y:2,z:3},open=replayShoulder(pos,0);
 // yaw 0 looks along +z; the right shoulder is −x in this coordinate system.
 assert.ok(Math.abs(open.x-(1-REPLAY_CAMERA.shoulder))<1e-9&&Math.abs(open.z-3)<1e-9&&Math.abs(open.y-(2+REPLAY_CAMERA.lift))<1e-9);
 const wall=replayShoulder(pos,0,()=>.5);assert.ok(Math.abs(wall.x-(1-(.5-VIEW.collisionPadding)))<1e-9);
 const flush=replayShoulder(pos,0,()=>.05);assert.ok(Math.abs(flush.x-1)<1e-9);
});
