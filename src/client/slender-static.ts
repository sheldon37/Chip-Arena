// Victim-only Slenderman presentation (2026-10-04): screen static/distortion from the authoritative sanity (leetFields),
// a flash on each of his teleports, and the drone + heartbeat. Everyone else sees only the figure. DOM/CSS overlay and
// Web Audio nodes: no WebGL programs, no lights. Style writes happen only when the quantised level changes.
import type {Snapshot} from '../shared/simulation.js';

/** The stalking field aimed at `me`, if any: sanity 0..1, seen, hops (tension pages). */
export function stalkerOf(state:Snapshot,me:string){for(const f of state.leetFields||[])if(f.kind==='tree'&&f.target===me&&f.walker&&f.slender)return f;return undefined;}
/** Static level 0..1 for the victim: sanity, stronger while he is in view, a .35 s burst after each teleport. */
export function staticLevel(sanity:number,seen:boolean,hopAge:number){return Math.min(1,sanity*(seen?.95:.7)+(seen?.08:0)+(hopAge<.35?.45*(1-hopAge/.35):0));}

export class SlenderStatic{
 private el?:HTMLDivElement;private level=-1;private hops=-1;private hopAt=-Infinity;private field='';
 update(state:Snapshot,me:string){const f=state.phase==='fight'?stalkerOf(state,me):undefined;
  if(f&&(f.id!==this.field||(f.hops||0)!==this.hops)){if(f.id===this.field&&(f.hops||0)>this.hops)this.hopAt=state.time;this.field=f.id;this.hops=f.hops||0;}
  const level=f?Math.round(staticLevel(f.sanity||0,!!f.seen,state.time-this.hopAt)*50)/50:0;if(level===this.level)return;this.level=level;
  if(typeof document==='undefined')return;if(!this.el){if(!level)return;this.el=document.createElement('div');this.el.className='slender-static';this.el.setAttribute('aria-hidden','true');this.el.innerHTML='<div class="scare-static"></div>';document.body.appendChild(this.el);}
  this.el.style.opacity=String(level);this.el.style.display=level?'':'none';}
 clear(){this.el?.remove();this.el=undefined;this.level=-1;}
}

/** Drone (two detuned low oscillators), filtered static and a heartbeat that quickens with sanity. */
export class SlenderAudio{
 private drone?:GainNode;private noise?:GainNode;private beatAt=0;private noiseBuffer?:AudioBuffer;
 update(state:Snapshot,me:string,ctx:AudioContext|null,out:AudioNode|null){if(!ctx||!out)return;const f=state.phase==='fight'?stalkerOf(state,me):undefined;if(!f&&!this.drone)return;
  if(!this.drone){this.drone=ctx.createGain();this.drone.gain.value=0;const low=ctx.createBiquadFilter();low.type='lowpass';low.frequency.value=240;low.connect(this.drone);this.drone.connect(out);
   for(const [type,hz] of [['sawtooth',55],['sine',55.6],['triangle',82.4]] as const){const o=ctx.createOscillator();o.type=type;o.frequency.value=hz;o.connect(low);o.start();}
   const n=ctx.sampleRate;this.noiseBuffer=ctx.createBuffer(1,n,n);const d=this.noiseBuffer.getChannelData(0);for(let i=0;i<n;i++)d[i]=Math.random()*2-1;
   const src=ctx.createBufferSource();src.buffer=this.noiseBuffer;src.loop=true;const band=ctx.createBiquadFilter();band.type='bandpass';band.frequency.value=2600;band.Q.value=.6;this.noise=ctx.createGain();this.noise.gain.value=0;src.connect(band);band.connect(this.noise);this.noise.connect(out);src.start();}
  const sanity=f?.sanity||0,tension=f?Math.min(1,Math.max(sanity,(f.hops||0)/8)):0,now=ctx.currentTime;
  this.drone.gain.setTargetAtTime(f?.04+.22*tension:0,now,.25);this.noise!.gain.setTargetAtTime(f?sanity*(f.seen?.32:.12):0,now,.08);
  if(f&&now>=this.beatAt){const gap=1.05-.6*tension;this.beatAt=now+gap;for(const [at,v] of [[0,1],[.17,.7]] as const){const o=ctx.createOscillator(),g=ctx.createGain();o.frequency.value=52;g.gain.setValueAtTime(0,now+at);g.gain.linearRampToValueAtTime((.18+.5*tension)*v,now+at+.015);g.gain.exponentialRampToValueAtTime(.0001,now+at+.16);o.connect(g);g.connect(out);o.start(now+at);o.stop(now+at+.2);}}}
}
