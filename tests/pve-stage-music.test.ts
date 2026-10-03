import {test} from 'node:test';
import assert from 'node:assert/strict';
import {existsSync} from 'node:fs';
import {ArenaAudio} from '../src/client/audio.js';
import {MUSIC_TRACKS,PVE_STAGE_TRACK} from '../src/client/music.js';
import type {Snapshot} from '../src/shared/simulation.js';
const snap=(phase:string,o:{pve?:boolean;map?:string;song?:boolean;time?:number}={}):Snapshot=>({time:o.time??10,tick:0,phase,remaining:60,round:1,setNumber:1,generation:1,winner:null,players:[],props:[],projectiles:[],events:[],options:{map:o.map??'siren',...(o.pve?{pve:{boss:'siren'}}:{})},...(o.pve?{pve:{entries:[]}}:{}),...(o.song?{bossSong:{track:'black-siren-protocol',startedAt:5}}:{})}) as unknown as Snapshot;
test('PvE Ward Zero loops the owner stage track until the Siren song starts; PvP keeps the rotation',()=>{
 const original=globalThis.Audio,created:AudioMock[]=[];
 class AudioMock{loop=false;preload='';volume=1;paused=true;currentTime=0;ended=false;onended:unknown;onerror:unknown;constructor(public src:string){created.push(this);}play(){this.paused=false;return Promise.resolve();}pause(){this.paused=true;}removeAttribute(){}load(){}}
 globalThis.Audio=AudioMock as any;
 try{
  assert.ok(!MUSIC_TRACKS.some(t=>(t[0] as string)===PVE_STAGE_TRACK[0]),'not in the PvP rotation');assert.ok(existsSync('public/music/'+PVE_STAGE_TRACK[0]+'.mp3'));
  const a=new ArenaAudio();a.music.configure(.5,false);
  a.update(snap('lobby',{pve:true}),'me',0);assert.equal(created.length,0,'lobby keeps the menu music');
  for(const phase of ['specialization','intro','countdown','fight']){a.update(snap(phase,{pve:true}),'me',0);}
  assert.equal(created.length,1);const stage=created[0];assert.match(stage.src,/music\/restricted-zone-pulse\.mp3$/);assert.ok(stage.loop);assert.ok(!stage.paused);assert.ok(stage.volume>0);assert.equal(a.music.title,'Restricted Zone Pulse');
  stage.currentTime=40;a.update(snap('fight',{pve:true,song:true}),'me',0);assert.ok(stage.paused,'boss song takes over');assert.equal(stage.currentTime,0);assert.equal(created.length,1);
  a.update(snap('match',{pve:true}),'me',0);a.update(snap('specialization',{pve:true}),'me',0);assert.ok(!stage.paused,'retry restarts the stage track');assert.equal(stage.currentTime,0);assert.equal(created.length,1);
  a.music.configure(.5,true);assert.equal(stage.volume,0);a.music.configure(.5,false);
  a.update(snap('fight',{map:'neon'}),'me',0);const pvp=created.at(-1)!;assert.notEqual(pvp,stage);assert.ok(stage.paused);assert.match(pvp.src,/violet-avenue\.mp3$/);
  const b=new ArenaAudio();b.update(snap('intro',{map:'siren'}),'me',0);assert.equal(created.at(-1),pvp,'PvP intro phase stays silent');b.update(snap('fight',{map:'siren'}),'me',0);assert.ok(MUSIC_TRACKS.slice(0,10).some(t=>created.at(-1)!.src.endsWith('/'+t[0]+'.mp3')),'PvP Ward Zero keeps the rotation');
 }finally{globalThis.Audio=original;}
});
