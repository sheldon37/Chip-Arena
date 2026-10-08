/** Tunable direct-hit enchantments. DoT ticks never call direct-hit handlers. */
export const ELEMENTS={
 fire:{name:'Burning',stat:'burn',duration:2,tick:.25,color:0xff822f,dpsPerChip:3,max:6},
 poison:{name:'Poisoned',stat:'poison',duration:4,tick:.25,color:0x9de34d,dpsPerChip:2.5,max:6},
} as const;
export type ElementId=keyof typeof ELEMENTS;
export interface DotState {dps:number;until:number;next:number;}
export function refreshDot(active:Map<string,DotState>,source:string,dps:number,now:number,element:ElementId){
 if(dps<=0)return;
 const old=active.get(source),rule=ELEMENTS[element];
 active.set(source,{dps:Math.max(dps,old&&old.until>=now?old.dps:0),until:now+rule.duration,next:old&&old.until>=now?old.next:now+rule.tick});
}
export function advanceDots(active:Map<string,DotState>,now:number,element:ElementId,damage:(source:string,amount:number)=>void){
 const tick=ELEMENTS[element].tick;
 for(const [source,dot]of active){
  // Process the final due tick before expiry, independent of frame rounding.
  while(dot.next<=now+1e-8&&dot.next<=dot.until+1e-8){damage(source,dot.dps*tick);dot.next+=tick;}
  if(now>dot.until+1e-8)active.delete(source);
 }
}
export function attackElements(burn:number,poison:number):ElementId[]{return [...(burn>0?['fire' as const]:[]),...(poison>0?['poison' as const]:[])];}
