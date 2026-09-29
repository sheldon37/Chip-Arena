// Original, deterministic entrance stings, aligned with the authored weapon cue times.
import {writeFileSync,mkdirSync,unlinkSync} from 'node:fs';import {spawnSync} from 'node:child_process';
const styles=[['sword',1.8,62,'metal'],['daggers',2.12,69,'metal'],['greatsword',1.8,38,'heavy'],['shield',1.45,43,'heavy'],['pistol',2.12,57,'gun'],['rifle',1.8,50,'gun'],['machinegun',2.12,45,'gun'],['fire',1.8,48,'fire'],['water',2.12,65,'water'],['lightning',1.45,74,'electric'],['darkness',2.12,34,'dark']];
const rate=44100,seconds=4.8,n=rate*seconds,tau=Math.PI*2,hz=m=>440*2**((m-69)/12);mkdirSync('public/audio/intros',{recursive:true});
for(const [spec,hit,note,kind]of styles){const samples=new Float32Array(n);let seed=spec.length*19273,low=0;const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2**31-1;};const add=(at,duration,fn)=>{const start=Math.round(at*rate),count=Math.round(duration*rate);for(let j=0;j<count&&start+j<n;j++)samples[start+j]+=fn(j/rate,j/count);};
 for(const [i,interval]of [0,7,12,19].entries())add(i*.18,hit+.6,(t,u)=>Math.sin(hz(note+interval)*tau*t)*Math.sin(Math.PI*u)**2*.038);
 add(hit-.55,.55,(t,u)=>{low+=.08*(noise()-low);return (noise()*.10+low*.5)*u*u;});
 if(kind==='metal'){for(const at of [.52,1.08,hit])add(at,.65,(t,u)=>(Math.sin(hz(note+25)*tau*t)+.4*Math.sin(hz(note+25)*2.73*tau*t))*Math.exp(-t*8)*.14);}
 if(kind==='heavy')add(hit,.95,t=>(Math.sin(tau*(43*t+.9*(1-Math.exp(-t*22))))*.4+noise()*.08)*Math.exp(-t*6));
 if(kind==='gun'){for(const at of [.8,1.08,1.48])add(at,.08,t=>noise()*.22*Math.exp(-t*60));if(spec==='machinegun')for(let i=0;i<4;i++)add(1.93+i*.09,.11,t=>(noise()*.18+Math.sin(tau*70*t)*.18)*Math.exp(-t*36));else add(hit,.4,t=>Math.sin(tau*hz(note+36)*t)*.1*Math.exp(-t*12));}
 if(kind==='fire')add(.65,2.3,(t,u)=>{const x=noise();low+=.025*(x-low);return (low*.65+x*(Math.sin(t*50)>.9?.07:.012))*Math.sin(Math.PI*u);});
 if(kind==='water')for(let i=0;i<13;i++)add(.45+i*.18,.35,t=>Math.sin(tau*(500*t+110*t*t))*Math.exp(-t*14)*.085);
 if(kind==='electric')add(hit,.8,t=>(noise()*.32+Math.sin(tau*53*t)*.24)*Math.exp(-t*7));
 if(kind==='dark')add(.25,3.2,(t,u)=>(Math.sin(tau*hz(note)*t)+.4*Math.sin(tau*hz(note+.12)*t))*Math.sin(Math.PI*u)**2*.18);
 add(hit,.28,t=>Math.sin(tau*(54*t+.8*(1-Math.exp(-t*30))))*Math.exp(-t*18)*.28);
 for(const interval of [0,7,12])add(3.2,1.6,(t,u)=>Math.sin(hz(note+interval)*tau*t)*Math.min(1,t*30)*(1-u)**1.5*.07);
 const b=Buffer.alloc(44+n*2);b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(rate,24);b.writeUInt32LE(rate*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(n*2,40);for(let i=0;i<n;i++)b.writeInt16LE(Math.round(Math.tanh(samples[i]*1.5)*27000*Math.min(1,i/(rate*.01),(n-i)/(rate*.12))),44+i*2);
 const temp='/tmp/chip-weapon-intro.wav';writeFileSync(temp,b);const out=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y','-i',temp,'-codec:a','libmp3lame','-b:a','112k','-metadata','artist=Chip Arena','public/audio/intros/weapon-'+spec+'.mp3']);if(out.status!==0)throw Error(out.stderr.toString());unlinkSync(temp);
}console.log('Created eleven weapon entrance stings.');
