import {writeFileSync} from 'node:fs';
// Original deterministic arcade percussion: noise slap + pitched body, or metallic parry chime.
const rate=22050;
for(const kind of ['punch','kick','heavy','parry']){
 const duration=kind==='parry'?.30:kind==='heavy'?.23:kind==='kick'?.18:.12,n=Math.floor(rate*duration),data=Buffer.alloc(44+n*2);let seed=1979,previous=0,phase=0;
 data.write('RIFF');data.writeUInt32LE(36+n*2,4);data.write('WAVEfmt ',8);data.writeUInt32LE(16,16);data.writeUInt16LE(1,20);data.writeUInt16LE(1,22);data.writeUInt32LE(rate,24);data.writeUInt32LE(rate*2,28);data.writeUInt16LE(2,32);data.writeUInt16LE(16,34);data.write('data',36);data.writeUInt32LE(n*2,40);
 for(let i=0;i<n;i++){const t=i/rate;seed=(Math.imul(seed,1664525)+1013904223)>>>0;const noise=seed/2147483648-1,high=noise-previous*.8;previous=noise;let x;
  if(kind==='parry'){x=(Math.sin(t*2*Math.PI*1740)+.45*Math.sin(t*2*Math.PI*2633)+.25*Math.sin(t*2*Math.PI*3590))*.38*Math.exp(-t*16)+high*.25*Math.exp(-t*100);}
  else{const heavy=kind==='heavy',kick=kind==='kick',freq=(heavy?150:kick?195:300)*Math.exp(-t*12)+50;phase+=freq*2*Math.PI/rate;x=Math.sin(phase)*.55*Math.exp(-t*(heavy?18:26))+high*.34*Math.exp(-t*(kick?35:65))+(kick?Math.sin(t*1800)*.12*Math.exp(-t*25):0);}
  const edge=Math.min(1,t/.0015,(duration-t)/.012);data.writeInt16LE(Math.round(Math.max(-.95,Math.min(.95,x*edge))*.72*32767),44+i*2);
 }writeFileSync('public/audio/arcade-'+kind+'.wav',data);
}
