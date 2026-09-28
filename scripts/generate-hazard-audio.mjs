// Original deterministic synthesized effects. No recordings or third-party assets.
import {mkdirSync,writeFileSync} from 'node:fs';
const rate=22050,TAU=Math.PI*2;
let seed=1234567;const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2147483648-1;};
const tone=(t,f)=>Math.sin(TAU*f*t),decay=(t,k)=>Math.exp(-t*k);
const clips={
 'saw-cut':[.52,(t,n)=>(n*.6+Math.sign(tone(t,145))* .24+tone(t,1700+Math.sin(t*39)*350)*.16)*(.45+.55*Math.sin(t*210)**2)*Math.exp(-t*4)],
 'spike-rise':[.42,(t,n)=>n*Math.exp(-t*11)*.65+tone(t,180+t*600)*Math.exp(-t*17)*.22+(t>.16?(tone(t-.16,320)*.5+n*.35)*Math.exp(-(t-.16)*30):0)],
};
mkdirSync('public/audio',{recursive:true});
for(const [name,[seconds,fn]]of Object.entries(clips)){
 const length=Math.ceil(rate*seconds),pcm=new Float32Array(length);let peak=0;
 for(let i=0;i<length;i++){const t=i/rate,fade=Math.min(1,t/.0015,(seconds-t)/.02);pcm[i]=fn(t,noise())*fade;peak=Math.max(peak,Math.abs(pcm[i]));}
 const gain=.72/Math.max(1,peak),buffer=Buffer.alloc(44+length*2);
 buffer.write('RIFF');buffer.writeUInt32LE(buffer.length-8,4);buffer.write('WAVEfmt ',8);buffer.writeUInt32LE(16,16);buffer.writeUInt16LE(1,20);buffer.writeUInt16LE(1,22);buffer.writeUInt32LE(rate,24);buffer.writeUInt32LE(rate*2,28);buffer.writeUInt16LE(2,32);buffer.writeUInt16LE(16,34);buffer.write('data',36);buffer.writeUInt32LE(length*2,40);
 for(let i=0;i<length;i++)buffer.writeInt16LE(Math.round(pcm[i]*gain*32767),44+i*2);
 writeFileSync(`public/audio/${name}.wav`,buffer);
}
console.log(`Generated ${Object.keys(clips).length} original mono WAV effects at ${rate} Hz.`);
