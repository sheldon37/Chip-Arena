import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import {existsSync} from 'node:fs';
import {ArenaMusic} from '../src/client/music.js';
const require=createRequire(import.meta.url),{assetPath}=require('../desktop/bundled.cjs');
test('menu track is accepted by existing desktop releases and retries blocked playback without restarting the loop',async()=>{
 const original=globalThis.Audio,created:AudioMock[]=[];
 class AudioMock{loop=false;preload='';volume=1;paused=true;currentTime=0;calls=0;blocked=true;constructor(public src:string){created.push(this);}play(){this.calls++;if(this.blocked)return Promise.reject(new Error('NotAllowedError'));this.paused=false;return Promise.resolve();}pause(){this.paused=true;}}
 globalThis.Audio=AudioMock as any;
 try{const music=new ArenaMusic();music.configure(.5,false);music.setMenuActive(true);await Promise.resolve();const track=created[0];assert.equal(assetPath('/game','arena://game'+track.src),'/game/dist/client/music/menu-afterglow.mp3');assert.ok(existsSync(new URL('../public'+track.src,import.meta.url)));assert.ok(track.loop);assert.ok(track.paused);track.blocked=false;music.unlock();assert.ok(!track.paused);track.currentTime=31;music.setMenuActive(true);assert.equal(created.length,1);music.configure(.5,true);assert.equal(track.volume,0);music.configure(.5,false);assert.ok(track.volume>0);music.setMenuActive(false);assert.ok(track.paused);const calls=track.calls;music.unlock();assert.equal(track.calls,calls);music.setMenuActive(true);assert.ok(!track.paused);assert.equal(track.currentTime,31);}finally{globalThis.Audio=original;}
});
