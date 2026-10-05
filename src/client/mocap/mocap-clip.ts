/** Retargeted motion-capture clips (presentation only). Clips are produced offline by scripts/mocap (capture FBX → voxel
 * rig channels) and committed as compact strings; nothing here reads or writes simulation state. A clip is sampled at a
 * clip time; callers map the authoritative timestamps onto clip time (retime) so contact frames land on the server's
 * active window. The layout is weapon independent: each weapon spec registers its own set (mocap-sets.ts). */

/** Channel order of every clip row (scripts/mocap/mixamo-retarget.mjs CHANNELS). Rotations are rotation vectors in the rig
 * joint's parent frame; grip/hand/pole/blade vectors are chest space (x the rig's left, y up, z forward). */
export const MOCAP_CHANNELS=['yaw','bx','by','bz',
 ...['pelvis','spine','chest','neck','head','lclav','rclav','lhip','rhip','lankle','rankle'].flatMap(n=>[n+'.x',n+'.y',n+'.z']),
 'lknee','rknee',
 'grip.x','grip.y','grip.z','blade.x','blade.y','blade.z','edge.x','edge.y','edge.z','rpole.x','rpole.y','rpole.z',
 'lhand.x','lhand.y','lhand.z','lfwd.x','lfwd.y','lfwd.z','lup.x','lup.y','lup.z','lpole.x','lpole.y','lpole.z','twohand'] as const;
export const CH=Object.fromEntries(MOCAP_CHANNELS.map((n,i)=>[n,i])) as Record<typeof MOCAP_CHANNELS[number],number>;
export const CHANNEL_COUNT=MOCAP_CHANNELS.length;

/** `marks` are clip-time keys (seconds) a caller retimes onto game time: for attacks [start, contact start, contact end,
 * settled]; loops have none. `loop` clips wrap. An attack's `pace` (clip times strictly between start and contact start)
 * splits its anticipation into equal shares of the game's windup: the converter places them by the captured motion, so a
 * windup much shorter than the capture's spends its time where the pose moves (holds fold away, the fastest stretches are
 * spread out) instead of compressing every moment equally. */
export interface MocapClip {fps:number;frames:number;data:string;marks?:readonly number[];pace?:readonly number[];loop?:boolean;source?:string}

const ALPHABET='ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_',QUANT=256;
const LOOKUP=new Int8Array(128).fill(-1);for(let i=0;i<ALPHABET.length;i++)LOOKUP[ALPHABET.charCodeAt(i)]=i;
const decoded=new WeakMap<MocapClip,Float32Array>();
/** Frame-major samples (frame * CHANNEL_COUNT + channel), decoded once per clip. */
export function decodeClip(clip:MocapClip):Float32Array{
 let out=decoded.get(clip);if(out)return out;
 const n=clip.frames,s=clip.data;out=new Float32Array(n*CHANNEL_COUNT);let i=0;
 for(let c=0;c<CHANNEL_COUNT;c++){let prev=0;for(let f=0;f<n;f++){let z=0,shift=0,g:number;do{g=LOOKUP[s.charCodeAt(i++)];if(g<0)throw new Error('mocap clip data is corrupt');z|=(g&31)<<shift;shift+=5;}while(g&32);prev+=z&1?-(z+1)/2:z/2;out[f*CHANNEL_COUNT+c]=prev/QUANT;}}
 if(i!==s.length)throw new Error('mocap clip length mismatch');
 decoded.set(clip,out);return out;
}
export const clipDuration=(clip:MocapClip)=>(clip.frames-1)/clip.fps;
/** Sample every channel at clip time `t` (seconds; clamped, or wrapped for loops) with uniform Catmull-Rom between frames,
 * so a retimed (sped-up) sweep stays smooth. */
export function sampleClip(clip:MocapClip,t:number,out:Float32Array){
 const d=decodeClip(clip),n=clip.frames,dur=clipDuration(clip);
 if(clip.loop)t=((t%dur)+dur)%dur;else t=Math.min(dur,Math.max(0,t));
 const x=t*clip.fps,i=Math.min(n-2,Math.floor(x)),u=x-i,u2=u*u,u3=u2*u;
 const at=(k:number)=>clip.loop?((k%(n-1))+(n-1))%(n-1):Math.min(n-1,Math.max(0,k));
 const a=at(i-1)*CHANNEL_COUNT,b=at(i)*CHANNEL_COUNT,c=at(i+1)*CHANNEL_COUNT,e=at(i+2)*CHANNEL_COUNT;
 const w0=-.5*u3+u2-.5*u,w1=1.5*u3-2.5*u2+1,w2=-1.5*u3+2*u2+.5*u,w3=.5*u3-.5*u2;
 for(let k=0;k<CHANNEL_COUNT;k++)out[k]=w0*d[a+k]+w1*d[b+k]+w2*d[c+k]+w3*d[e+k];
 return out;
}
/** Clip time for game time `t` along retime keys (game times `at` ↔ clip times `marks`): a monotone C1 cubic whose end
 * tangents are the end segments' own rates (unlike pose-curves' zero end tangents, which would rush the middle of the
 * windup to make up for a standing start), so each segment plays at close to its constant rate and a clip's contact key
 * lands exactly on its game key without a speed step. Outside the keys the clip clamps to its first / last key. */
export function retime(at:readonly number[],marks:readonly number[],t:number){
 const n=at.length-1;if(t<=at[0])return marks[0];if(t>=at[n])return marks[n];let i=0;while(at[i+1]<t)i++;
 const slope=(k:number)=>(marks[k+1]-marks[k])/(at[k+1]-at[k]);
 const tan=(k:number)=>{if(k===0)return slope(0);if(k===n)return slope(n-1);const a=slope(k-1),b=slope(k);return a*b<=0?0:2*a*b/(a+b);};
 const h=at[i+1]-at[i],u=(t-at[i])/h,u2=u*u,u3=u2*u,m0=tan(i),m1=tan(i+1);
 return (2*u3-3*u2+1)*marks[i]+(u3-2*u2+u)*h*m0+(-2*u3+3*u2)*marks[i+1]+(u3-u2)*h*m1;
}
