import type {Snapshot} from '../shared/simulation.js';
import {BLACK_SIREN_PROTOCOL as TRACK,loopedSongTime,barAt} from '../shared/boss-music.js';
/** Signed shortest drift through the loop, in seconds. */
export function songDrift(actual:number,wanted:number){let d=actual-wanted;const span=TRACK.loop.end-TRACK.loop.start;if(actual>=TRACK.loop.start&&wanted>=TRACK.loop.start){if(d>span/2)d-=span;if(d<-span/2)d+=span;}return d;}
/** Shared AudioContext, separate music gain. Never restarts for a round/generation change. */
export class BossTheme {
 private buffer?:AudioBuffer;private loading?:Promise<void>;private failed=false;
 private source?:AudioBufferSourceNode;private gain?:GainNode;private context?:AudioContext;
 private epoch?:number;private rawAtStart=0;private contextAtStart=0;private lastBar=-1;private lastBeat=-1;
 drift=0;status:'idle'|'loading'|'playing'|'unavailable'='idle';
 private load(context:AudioContext){if(this.buffer||this.loading||this.failed)return;this.status='loading';this.loading=(async()=>{try{const r=await fetch(`${(import.meta as any).env?.BASE_URL||'/'}${TRACK.file}`);if(!r.ok)throw Error('missing');this.buffer=await context.decodeAudioData(await r.arrayBuffer());}catch{this.failed=true;this.status='unavailable';}})();}
 /** Decode ahead (PvE) so the song is ready on the Siren's intro tick. */
 preload(context:AudioContext|null){if(context)this.load(context);}
 private seek(raw:number,context:AudioContext){this.source?.stop();this.source?.disconnect();this.gain??=context.createGain();if(!this.context)this.gain.connect(context.destination);this.context=context;const source=context.createBufferSource();source.buffer=this.buffer!;source.loop=true;source.loopStart=TRACK.loop.start;source.loopEnd=TRACK.loop.end;source.connect(this.gain);source.start(context.currentTime,Math.max(0,loopedSongTime(TRACK,raw)));this.source=source;this.rawAtStart=raw;this.contextAtStart=context.currentTime;this.drift=0;this.status='playing';}
 update(state:Snapshot,context:AudioContext|null,volume:number){const song=state.bossSong;if(!song||['lobby','match'].includes(state.phase)){this.stop();return;}if(!context)return;this.load(context);if(!this.buffer||context.state!=='running')return;
  const raw=Math.max(0,state.time-song.startedAt),wanted=loopedSongTime(TRACK,raw),bar=barAt(TRACK,wanted),boundary=bar.beat===1&&(this.lastBar!==bar.bar||this.lastBeat!==bar.index);
  if(!this.source||this.epoch!==song.startedAt){this.epoch=song.startedAt;this.seek(raw,context);}else{this.drift=songDrift(loopedSongTime(TRACK,this.rawAtStart+context.currentTime-this.contextAtStart),wanted);if(Math.abs(this.drift)>.04&&boundary)this.seek(raw,context);}
  this.lastBar=bar.bar;this.lastBeat=bar.index;this.gain!.gain.setTargetAtTime(Math.max(0,Math.min(1,volume)),context.currentTime,.015);
 }
 pause(){this.source?.stop();this.source?.disconnect();this.source=undefined;this.status=this.failed?'unavailable':'idle';}
 stop(){this.pause();this.epoch=undefined;this.lastBar=this.lastBeat=-1;this.drift=0;}
}
