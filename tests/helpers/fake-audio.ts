/**
 * A fake Web Audio graph for ArenaAudio tests (no browser): records every buffer source with its clip, gain, playback rate
 * and start time, ends voices on demand, and stubs the music/intro/boss/Siren players. `now` drives context.currentTime.
 */
import {ArenaAudio,AUDIO_CLIPS,type Sound} from '../../src/client/audio.js';
export interface FakeVoice{name:Sound;gain:number;rate:number;at:number;end:()=>void;ended:boolean}
export function fakeAudio(){
 const clips=new Map(AUDIO_CLIPS.map(n=>[n,{name:n} as unknown as AudioBuffer])),voices:FakeVoice[]=[],master:number[]=[],clock={now:10};
 let pending:{buffer:AudioBuffer|null;playbackRate:{value:number};onended:(()=>void)|null}|null=null;
 const node=()=>({gain:{value:1,setTargetAtTime(v:number){master.push(v);}},pan:{value:0},connect(){return this;},disconnect(){}});
 const context={state:'running',get currentTime(){return clock.now;},createStereoPanner:node,
  createGain:()=>{const level=node();if(pending){const src=pending;pending=null;const v:FakeVoice={get name(){return (src.buffer as unknown as {name:Sound}).name;},get gain(){return level.gain.value;},get rate(){return src.playbackRate.value;},at:clock.now,ended:false,end(){if(!v.ended){v.ended=true;src.onended?.();}}} as FakeVoice;voices.push(v);}return level;},
  createBufferSource:()=>{const src={buffer:null as AudioBuffer|null,playbackRate:{value:1},onended:null as (()=>void)|null,connect(){return this;},disconnect(){},start(){},stop(){}};pending=src;return src;}};
 const audio=new ArenaAudio(),stub=new Proxy({},{get:()=>()=>undefined});
 Object.assign(audio,{context,master:node(),buffers:clips,music:stub,intros:stub,boss:stub,siren:stub});
 /** End every voice older than `age` seconds (keeps the global voice cap from filling in long runs). */
 const reap=(age=.15)=>{for(const v of voices)if(!v.ended&&clock.now-v.at>age)v.end();};
 return {audio,voices,master,clock,reap,context};
}
