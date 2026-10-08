// Seeded randomness for PvE (design §2.7). PvE code never calls sim.random(), Math.random, Date.now or crypto.
// Each part draws from its own mulberry32 stream: rng.fork(label) is seeded by hash32(runSeed,label), so a new draw in one
// part never shifts another.

/** 32-bit hash of numbers and strings (FNV-1a over the parts, then a murmur3 finalizer). Pure and stable across runs. */
export function hash32(...parts:(number|string)[]):number{
 let h=0x811c9dc5;
 const mix=(byte:number)=>{h^=byte&0xff;h=Math.imul(h,0x01000193);};
 for(const part of parts){
  if(typeof part==='number'){const n=Number.isFinite(part)?Math.floor(part)>>>0:0;mix(0x4e);mix(n);mix(n>>>8);mix(n>>>16);mix(n>>>24);}
  else{mix(0x53);for(let i=0;i<part.length;i++){const c=part.charCodeAt(i);mix(c);mix(c>>>8);}}
  mix(0x7c);
 }
 h^=h>>>16;h=Math.imul(h,0x85ebca6b);h^=h>>>13;h=Math.imul(h,0xc2b2ae35);h^=h>>>16;return h>>>0;
}
/** mulberry32: a fast 32-bit generator returning floats in [0,1). */
export function mulberry32(seed:number):()=>number{
 let a=seed>>>0;
 return ()=>{a=(a+0x6D2B79F5)>>>0;let t=a;t=Math.imul(t^t>>>15,t|1);t^=t+Math.imul(t^t>>>7,t|61);return ((t^t>>>14)>>>0)/4294967296;};
}
export class PveRng{
 private source:()=>number;
 constructor(readonly seed:number){this.source=mulberry32(seed);}
 /** Uniform float in [0,1). */
 next(){return this.source();}
 /** An independent stream seeded by hash32(this.seed,label). */
 fork(label:string){return new PveRng(hash32(this.seed,label));}
 /** Integer in [0,n). */
 int(n:number){return Math.min(n-1,Math.floor(this.next()*n));}
 pick<T>(list:readonly T[]):T{return list[this.int(list.length)];}
 /** Weighted key pick in insertion order; zero and negative weights never win. Falls back to the last positive key. */
 weighted<K extends string>(table:Partial<Record<K,number>>):K{
  const keys=(Object.keys(table) as K[]).filter(k=>(table[k]??0)>0);if(!keys.length)throw Error('weighted() needs a positive weight');
  let roll=this.next()*keys.reduce((n,k)=>n+table[k]!,0);
  for(const k of keys){roll-=table[k]!;if(roll<0)return k;}
  return keys[keys.length-1];
 }
}
