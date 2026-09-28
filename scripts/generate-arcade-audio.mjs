// Original rain / 16-bit fighting-game entrance. No sampled game music or voices.
import {writeFileSync,mkdirSync,unlinkSync} from 'node:fs';import {spawnSync} from 'node:child_process';
const rate=44100,seconds=7.2,n=Math.round(rate*seconds),samples=new Float32Array(n),tau=Math.PI*2;let seed=8901;const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2**31-1;};
const add=(at,duration,fn)=>{const start=Math.floor(at*rate),count=Math.floor(duration*rate);for(let j=0;j<count&&start+j<n;j++)samples[start+j]+=fn(j/rate,j/count);};const hz=m=>440*2**((m-69)/12);
let low=0;add(0,seconds,(t,u)=>{const x=noise();low+=.1*(x-low);return (x-low)*.018*Math.min(1,t*2)*(1-u*.4);});
add(.6,2.8,(t,u)=>{low+=.035*(noise()-low);return (low+Math.sin(43*tau*t)*.08)*.3*Math.sin(Math.PI*u)**2;});
for(const note of [33,40,45,48]){const f=hz(note);add(.15,4.5,(t,u)=>Math.sin(f*tau*t)*Math.sin(Math.PI*u)*.035);}
const kick=at=>add(at,.28,t=>Math.sin(tau*(50*t+2.1*(1-Math.exp(-t*30))))*Math.exp(-t*18)*.42);
const lead=[69,72,76,81,79,76,72,71];for(let b=0;b<12;b++){const at=3.2+b*.22;const f=hz(lead[b%8]);add(at,.25,(t,u)=>(Math.sign(Math.sin(tau*f*t))*.065+Math.sin(tau*f*2*t)*.03)*Math.min(1,t*130)*(1-u)**1.8);if(b%2===0)kick(at);add(at+.11,.055,t=>noise()*.065*Math.exp(-t*55));}
kick(4.6);kick(5.45);for(const note of [33,45,52,57,60,64,69]){const f=hz(note);add(5.45,1.75,(t,u)=>(Math.sin(tau*f*t)+.2*Math.sign(Math.sin(tau*f*t)))*.065*Math.min(1,t*100)*(1-u)**1.5);}
const b=Buffer.alloc(44+n*2);b.write('RIFF');b.writeUInt32LE(b.length-8,4);b.write('WAVEfmt ',8);b.writeUInt32LE(16,16);b.writeUInt16LE(1,20);b.writeUInt16LE(1,22);b.writeUInt32LE(rate,24);b.writeUInt32LE(rate*2,28);b.writeUInt16LE(2,32);b.writeUInt16LE(16,34);b.write('data',36);b.writeUInt32LE(n*2,40);for(let i=0;i<n;i++)b.writeInt16LE(Math.round(Math.tanh(samples[i]*1.7)*30000*Math.min(1,i/(rate*.01),(n-i)/(rate*.12))),44+i*2);
mkdirSync('public/audio/intros',{recursive:true});const temp='/tmp/chip-arena-rain-warrior.wav';writeFileSync(temp,b);const out=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y','-i',temp,'-codec:a','libmp3lame','-b:a','128k','-metadata','artist=Chip Arena','-metadata','title=Rain Warrior','public/audio/intros/dev-ansatsuken.mp3']);if(out.status!==0)throw Error(out.stderr.toString());unlinkSync(temp);console.log('Original Rain Warrior intro: 7.2 seconds.');
