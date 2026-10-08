/** Echo timestamps use a monotonic client clock; server/client clocks need not agree. */
export class ConnectionMetrics {
 private pending=new Map<number,number>();private arrivals:number[]=[];private rtts:number[]=[];
 rtt=0;snapshotBytes=0;server:{loopP95Ms?:number;tickMs?:number;bufferedBytes?:number;droppedSnapshots?:number;compression?:boolean}={};
 constructor(private clock=()=>performance.now()){}
 reset(){this.pending.clear();this.arrivals=[];this.rtts=[];this.rtt=0;this.snapshotBytes=0;this.server={};}
 ping(){const now=this.clock();this.pending.set(now,now);for(const [id,at] of this.pending)if(now-at>30000)this.pending.delete(id);return {type:'ping',at:now};}
 pong(message:any){const sent=this.pending.get(message.at);if(sent===undefined)return false;this.pending.delete(message.at);this.rtt=Math.max(0,Math.round(this.clock()-sent));this.rtts.push(this.rtt);this.rtts=this.rtts.slice(-20);if(message.network&&typeof message.network==='object')this.server=message.network;return true;}
 snapshot(bytes:number){const now=this.clock();this.arrivals.push(now);this.arrivals=this.arrivals.filter(at=>now-at<=5000);this.snapshotBytes=bytes;}
 get summary(){const gaps=this.arrivals.slice(1).map((at,i)=>at-this.arrivals[i]).sort((a,b)=>a-b),rtts=this.rtts.slice().sort((a,b)=>a-b);return {rttMs:this.rtt,rttP95Ms:rtts[Math.floor((rtts.length-1)*.95)]||0,snapshotGapP95Ms:Math.round(gaps[Math.floor((gaps.length-1)*.95)]||0),snapshotDecodedKiB:+(this.snapshotBytes/1024).toFixed(1),server:this.server};}
}
