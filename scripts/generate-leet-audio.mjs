// Original synth stings. Deterministic, with no external recordings.
import {mkdirSync,writeFileSync,unlinkSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
const rate=44100,tau=Math.PI*2,hz=m=>440*2**((m-69)/12);
mkdirSync('public/audio/intros',{recursive:true});
function wav(left,right){const n=left.length,channels=right?2:1,buffer=Buffer.alloc(44+n*2*channels);buffer.write('RIFF');buffer.writeUInt32LE(buffer.length-8,4);buffer.write('WAVEfmt ',8);buffer.writeUInt32LE(16,16);buffer.writeUInt16LE(1,20);buffer.writeUInt16LE(channels,22);buffer.writeUInt32LE(rate,24);buffer.writeUInt32LE(rate*channels*2,28);buffer.writeUInt16LE(channels*2,32);buffer.writeUInt16LE(16,34);buffer.write('data',36);buffer.writeUInt32LE(n*channels*2,40);for(let i=0;i<n;i++){const fade=Math.min(1,i/(rate*.007),(n-i)/(rate*.14));for(let c=0;c<channels;c++)buffer.writeInt16LE(Math.round(Math.tanh((c?right:left)[i]*1.2)*fade*26500),44+(i*channels+c)*2);}return buffer;}
const styles=[['dev-matrix',33,2.25,[0,12,7,15,3,19,10,24]]];
for(const [index,[id,key,impact,melody]]of styles.entries()){
 const n=Math.round(8*rate),left=new Float32Array(n),right=new Float32Array(n);let seed=501+index*117;const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2**31-1;};
 const add=(at,seconds,fn,pan=0)=>{const start=Math.round(at*rate),length=Math.round(seconds*rate);for(let j=0;j<length&&start+j<n;j++){const t=j/rate,u=j/length,x=fn(t,u);left[start+j]+=x*Math.sqrt((1-pan)/2);right[start+j]+=x*Math.sqrt((1+pan)/2);}};
 const pluck=(at,note,amp=.16,pan=0)=>{const f=hz(note),tone=(t,u)=>Math.sin(tau*f*t+Math.sin(tau*f*2*t)*2*Math.exp(-t*18))*Math.exp(-t*8)*Math.min(1,t*150)*(1-u)*amp;add(at,.6,tone,pan);add(at+.18,.6,(t,u)=>tone(t,u)*.22,-pan);};
 const kick=at=>add(at,.35,t=>Math.sin(tau*(48*t+3.7*(1-Math.exp(-t*42))))*Math.exp(-t*16)*.55);
 for(let b=0;b<16;b++){
  const at=b*.38; if(b>=2||index===1)kick(at);
  if(b%2===1)add(at,.16,t=>(noise()*.7+Math.sin(tau*185*t)*.3)*Math.exp(-t*29)*.2,.12);
  for(let h=0;h<2;h++)add(at+h*.2,.07,t=>noise()*Math.exp(-t*70)*.055,h?.65:-.65);
  const f=hz(key-12+(b%4===3?7:0));add(at+.08,.28,(t,u)=>(Math.sin(tau*f*t)+.24*Math.sin(tau*f*2*t))*Math.min(1,t*100)*(1-u)**1.6*.30);
  pluck(at,key+melody[b%8],.16,Math.sin(b*2+index)*.6);
  if(index===2||index===1)pluck(at+.2,key+melody[(b+3)%8],.09,-Math.sin(b)*.5);
 }
 // Soft harmonic rise, action accent, then a final name-reveal chord.
 add(0,impact,(t,u)=>(noise()*.04+Math.sin(tau*(170*t+380*t*t))*.04)*u*u);
 kick(impact);add(impact,.6,t=>noise()*Math.exp(-t*13)*.18);
 for(const note of [33,40,45,48,52,57]){const f=hz(note);add(0,6.1,(t,u)=>(Math.sin(tau*f*t)+.28*Math.sin(tau*f*2*t)+.18*Math.sin(tau*f*3*t))*.09*Math.min(1,t*.8)*(1-u*.4),note%2?.6:-.6);}add(5.3,.95,(t,u)=>noise()*.13*u);
 const third=3;for(const semitone of [0,third,7,12]){const f=hz(key+semitone);add(.05,3.1,(t,u)=>(Math.sin(tau*f*t)+Math.sin(tau*f*1.004*t))*.035*Math.sin(Math.PI*u),semitone%2?.4:-.4);add(6.25,1.75,(t,u)=>(Math.sin(tau*f*t)+.4*Math.sin(tau*f*2*t))*.15*Math.min(1,t*70)*(1-u)**1.5,semitone%2?.5:-.5);}
 kick(5.9);kick(6.25);pluck(6.25,key+24,.20,.25);pluck(6.65,key+19,.10,-.35);pluck(7.1,key+12,.10,.35);
 const temp='/tmp/chip-intro-'+id+'.wav';writeFileSync(temp,wav(left,right));const result=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y','-i',temp,'-codec:a','libmp3lame','-b:a','128k','-metadata','artist=Chip Arena','-metadata','title='+id,'public/audio/intros/'+id+'.mp3']);if(result.status!==0)throw Error(result.stderr.toString());unlinkSync(temp);console.log('Intro:',id,'8s');
}
