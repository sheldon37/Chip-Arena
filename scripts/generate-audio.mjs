// Deterministic synthesized effects; hit.wav is the user-supplied hitmarker recording.
import {mkdirSync,writeFileSync} from 'node:fs';
const rate=22050,TAU=Math.PI*2;
let seed=1234567;const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2147483648-1;};
const tone=(t,f)=>Math.sin(TAU*f*t),decay=(t,k)=>Math.exp(-t*k);
const clips={
 pistol:[.23,(t,n)=>n*decay(t,60)*.9+tone(t,95+120*decay(t,32))*decay(t,23)*.5],
 rifle:[.16,(t,n)=>n*decay(t,85)+tone(t,140+90*decay(t,35))*decay(t,30)*.35],
 machinegun:[.13,(t,n)=>n*decay(t,95)*.8+tone(t,76+65*decay(t,40))*decay(t,32)*.65],
 fire:[.46,(t,n)=>n*(.25+Math.sin(t*40)**2*.5)*decay(t,8)+tone(t,120-t*100)*decay(t,10)*.3],
 water:[.4,(t,n)=>n*decay(t,14)*.4+tone(t,530+210*Math.sin(t*27))*decay(t,9)*.4],
 lightning:[.21,(t,n)=>n*decay(t,29)*.4+Math.sign(tone(t,1100-t*2800))*decay(t,30)*.4],
 darkness:[.6,(t,n)=>tone(t,95-t*70)*decay(t,5)*.55+tone(t,144)*decay(t,8)*.18+n*decay(t,15)*.15],
 sword:[.3,(t,n)=>n*Math.sin(Math.min(1,t/.26)*Math.PI)**2*decay(t,5)*.65+tone(t,700)*decay(t,25)*.1],
 daggers:[.19,(t,n)=>n*Math.sin(Math.min(1,t/.15)*Math.PI)**2*decay(t,9)*.7],
 shield:[.31,(t,n)=>tone(t,130)*decay(t,12)*.65+n*decay(t,30)*.4],
 greatsword:[.48,(t,n)=>n*Math.sin(Math.min(1,t/.4)*Math.PI)**2*decay(t,4)*.7+tone(t,80)*decay(t,10)*.2],
 'reload-start':[.23,(t,n)=>n*(decay(t,65)+((t>.12)?decay(t-.12,70):0))*.6+tone(t,580)*decay(t,35)*.2],
 'reload-end':[.15,(t,n)=>n*decay(t,80)*.55+tone(t,1200)*decay(t,45)*.25],
 dash:[.27,(t,n)=>n*Math.sin(Math.min(1,t/.25)*Math.PI)*decay(t,7)*.7+tone(t,180+t*800)*decay(t,18)*.15],
 jump:[.19,(t,n)=>tone(t,150+t*950)*decay(t,20)*.35+n*decay(t,40)*.3],
 hit:[.09,(t,n)=>tone(t,1500)*decay(t,65)*.35+n*decay(t,95)*.12],
 hurt:[.22,(t,n)=>tone(t,82)*decay(t,17)*.6+n*decay(t,28)*.45],
 parry:[.5,(t,n)=>(tone(t,840)+tone(t,1314)*.4+tone(t,2100)*.2)*decay(t,10)*.4+n*decay(t,100)*.3],
 wood:[.32,(t,n)=>n*decay(t,21)*.6+tone(t,170)*decay(t,28)*.4],
 stone:[.38,(t,n)=>n*decay(t,12)*.65+tone(t,60)*decay(t,10)*.35],
 metal:[.58,(t,n)=>(tone(t,260)+tone(t,697)*.5)*decay(t,8)*.35+n*decay(t,55)*.6],
 explosion:[.7,(t,n)=>n*decay(t,7)*.75+tone(t,48+30*decay(t,10))*decay(t,6)*.45],
 'burn-tick':[.14,(t,n)=>n*decay(t,36)*.35],
 'poison-tick':[.2,(t,n)=>tone(t,510-t*1100)*decay(t,21)*.35+n*decay(t,25)*.12],
 ui:[.09,t=>tone(t,680)*decay(t,55)*.3],
 install:[.42,t=>(tone(t,440)+tone(t,660)*(t>.09?1:0)+tone(t,880)*(t>.18?1:0))*decay(t,7)*.22],
 countdown:[.14,t=>tone(t,620)*decay(t,23)*.28],
 go:[.36,t=>(tone(t,880)+tone(t,1320)*.5)*decay(t,12)*.3],
 win:[.75,t=>(tone(t,523)+tone(t,659)*(t>.13?1:0)+tone(t,784)*(t>.27?1:0))*decay(t,3)*.25],
 lose:[.7,t=>(tone(t,330)+tone(t,294)*(t>.18?1:0)+tone(t,220)*(t>.35?1:0))*decay(t,4)*.22],
};
mkdirSync('public/audio',{recursive:true});
for(const [name,[seconds,fn]]of Object.entries(clips)){
 if(name==='hit')continue; // Preserve the approved recording in assets/audio/hitmarker_2.mp3.
 const length=Math.ceil(rate*seconds),pcm=new Float32Array(length);let peak=0;
 for(let i=0;i<length;i++){const t=i/rate,fade=Math.min(1,t/.0015,(seconds-t)/.02);pcm[i]=fn(t,noise())*fade;peak=Math.max(peak,Math.abs(pcm[i]));}
 const gain=.72/Math.max(1,peak),buffer=Buffer.alloc(44+length*2);
 buffer.write('RIFF');buffer.writeUInt32LE(buffer.length-8,4);buffer.write('WAVEfmt ',8);buffer.writeUInt32LE(16,16);buffer.writeUInt16LE(1,20);buffer.writeUInt16LE(1,22);buffer.writeUInt32LE(rate,24);buffer.writeUInt32LE(rate*2,28);buffer.writeUInt16LE(2,32);buffer.writeUInt16LE(16,34);buffer.write('data',36);buffer.writeUInt32LE(length*2,40);
 for(let i=0;i<length;i++)buffer.writeInt16LE(Math.round(pcm[i]*gain*32767),44+i*2);
 writeFileSync(`public/audio/${name}.wav`,buffer);
}
console.log(`Generated ${Object.keys(clips).length} original mono WAV effects at ${rate} Hz.`);
