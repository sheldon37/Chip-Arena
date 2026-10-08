// Original procedural compositions. No samples, copyrighted melodies or external assets.
import {mkdirSync,writeFileSync,rmSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
const tracks=[['foundry-rush',138,45],['orange-line',144,48],['kinetic-city',132,43],['afterburner',152,50],['night-circuit',128,40],['critical-mass',146,47],['jump-drive',140,52],['neon-recoil',136,42],['overpass',150,46],['last-set',142,49]];
mkdirSync('public/music',{recursive:true});const rate=32000,pi=Math.PI*2;
for(let index=0;index<tracks.length;index++){
 const [id,bpm,key]=tracks[index],beat=60/bpm,duration=beat*64,n=Math.ceil(duration*rate),left=new Float32Array(n),right=new Float32Array(n);let seed=17+index*631;
 const noise=()=>{seed=(Math.imul(seed,1664525)+1013904223)>>>0;return seed/2**31-1;};
 const add=(at,length,fn,pan=0)=>{const start=Math.floor(at*rate),end=Math.min(n,start+Math.ceil(length*rate));for(let i=start;i<end;i++){const x=fn((i-start)/rate,(i-start)/(length*rate));left[i]+=x*(.8-pan*.3);right[i]+=x*(.8+pan*.3);}};
 const hz=m=>440*2**((m-69)/12),progressions=[[0,5,3,7],[0,7,5,3],[0,3,10,5],[0,5,10,7]],progression=progressions[index%4],scale=[0,2,3,5,7,9,10];
 const patterns=[[0,4,2,6,4,2,5,3],[0,2,4,6,3,5,2,4],[4,0,5,2,6,3,4,1],[0,3,4,2,5,4,6,2]];
 for(let bar=0;bar<16;bar++){
  const root=key+progression[Math.floor(bar/2)%4],start=bar*4*beat,energy=bar<2?.65:bar>13?.85:1;
  for(let b=0;b<4;b++){
   add(start+b*beat,.3,(t)=>Math.sin(pi*(48*t+4.8*(1-Math.exp(-t*35))))*Math.exp(-t*16)*.7*energy);
   if(b%2===1)add(start+b*beat,.2,t=>(noise()*.65+Math.sin(pi*175*t)*.35)*Math.exp(-t*21)*.33*energy,index%2?.2:-.2);
   for(let h=0;h<2;h++)add(start+(b+h*.5)*beat,.085,t=>noise()*Math.exp(-t*60)*.085*(h?.75:1),h?.7:-.7);
   if(bar%4===3&&b===3)for(let h=1;h<4;h++)add(start+(b+h*.25)*beat,.09,t=>noise()*Math.exp(-t*32)*.11,h%2?.5:-.5);
  }
  // Offbeat bass, arpeggio variations and a chord bed make each track its own piece.
  for(let step=0;step<8;step++){
   const note=root+(step===7?7:step%4===3?12:0),f=hz(note-12);
   add(start+(step*.5+.23)*beat,beat*.30,(t,u)=>{const env=Math.min(1,t*180)*(1-u)**1.3;return (Math.sin(pi*f*t)+.32*Math.sin(pi*f*2*t)+.13*Math.sin(pi*f*3*t))*env*.24*energy;},-.1);
   if(bar<2&&step%2)continue;const degree=patterns[index%4][(step+bar%2*2)%8],melody=hz(root+12+scale[degree%7]+(index%3===0&&step===7?12:0));
   const pan=Math.sin(step*1.7+index)*.65,at=start+step*.5*beat;
   const tone=(t,u)=>(Math.sin(pi*melody*t)+.28*Math.sin(pi*melody*(2+index*.002)*t)+.12*Math.sin(pi*melody*3*t))*Math.min(1,t*130)*Math.exp(-t*(7+index%4))*Math.max(0,1-u)*.135;
   add(at,beat*.65,tone,pan);add(at+beat*.75,beat*.65,(t,u)=>tone(t,u)*.26,-pan);add(at+beat*1.5,beat*.65,(t,u)=>tone(t,u)*.09,pan);
  }
  for(const semitone of [0,3,7]){const f=hz(root+semitone);add(start,beat*3.9,(t,u)=>(Math.sin(pi*f*t)+Math.sin(pi*f*1.004*t))*.018*Math.sin(Math.PI*u)*(.5+.5*Math.min(1,(t%beat)/.12)),semitone===0?-.4:.4);}
 }
 const buf=Buffer.alloc(44+n*4);buf.write('RIFF');buf.writeUInt32LE(buf.length-8,4);buf.write('WAVEfmt ',8);buf.writeUInt32LE(16,16);buf.writeUInt16LE(1,20);buf.writeUInt16LE(2,22);buf.writeUInt32LE(rate,24);buf.writeUInt32LE(rate*4,28);buf.writeUInt16LE(4,32);buf.writeUInt16LE(16,34);buf.write('data',36);buf.writeUInt32LE(n*4,40);
 for(let i=0;i<n;i++){const fade=Math.min(1,i/(rate*.025),(n-i)/(rate*.08));buf.writeInt16LE(Math.round(Math.tanh(left[i]*1.4)*fade*27000),44+i*4);buf.writeInt16LE(Math.round(Math.tanh(right[i]*1.4)*fade*27000),46+i*4);}
 const temp='/tmp/chip-music-'+id+'.wav';writeFileSync(temp,buf);const r=spawnSync('ffmpeg',['-hide_banner','-loglevel','error','-y','-i',temp,'-codec:a','libmp3lame','-b:a','96k','-metadata','artist=Chip Arena','-metadata','title='+id,'public/music/'+id+'.mp3']);if(r.status!==0)throw Error(r.stderr.toString());rmSync(temp);console.log(id,bpm,duration.toFixed(2)+'s');
}
