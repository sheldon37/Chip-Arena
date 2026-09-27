export const MUSIC_TRACKS=[['foundry-rush','Foundry Rush',138],['orange-line','Orange Line',144],['kinetic-city','Kinetic City',132],['afterburner','Afterburner',152],['night-circuit','Night Circuit',128],['critical-mass','Critical Mass',146],['jump-drive','Jump Drive',140],['neon-recoil','Neon Recoil',136],['overpass','Overpass',150],['last-set','Last Set',142]] as const;
/** One decode/stream at a time. Shuffle bag prevents repeats until all ten have played. */
export class ArenaMusic {
 private audio:HTMLAudioElement|null=null;private bag:number[]=[];private current=-1;private active=false;private volume=.35;private muted=false;private failed=false;private ducked=false;
 private get level(){return this.muted?0:this.volume*.55*(this.ducked?.12:1);}
 setDucked(ducked:boolean){if(this.ducked===ducked)return;this.ducked=ducked;if(this.audio)this.audio.volume=this.level;}
 configure(volume:number,muted:boolean){this.volume=Number.isFinite(volume)?Math.max(0,Math.min(1,volume)):.35;this.muted=muted;if(this.audio)this.audio.volume=this.level;}
 get cueVolume(){return this.muted?0:this.volume*.80;}
 get title(){return this.current<0?'':MUSIC_TRACKS[this.current][1];}
 setActive(active:boolean){if(active===this.active)return;this.active=active;if(!active){this.audio?.pause();return;}if(this.audio&&!this.audio.ended){void this.audio.play().catch(()=>{});}else this.next();}
 unlock(){if(this.active&&this.audio)void this.audio.play().catch(()=>{});}
 private next(){if(!this.active)return;if(!this.bag.length){this.bag=Array.from({length:MUSIC_TRACKS.length},(_,i)=>i);for(let i=this.bag.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[this.bag[i],this.bag[j]]=[this.bag[j],this.bag[i]];}if(this.bag.at(-1)===this.current)[this.bag[0],this.bag[this.bag.length-1]]=[this.bag.at(-1)!,this.bag[0]];}
 this.current=this.bag.pop()!;this.audio?.pause();const a=this.audio=new Audio(`${(import.meta as any).env.BASE_URL}music/${MUSIC_TRACKS[this.current][0]}.mp3`);a.preload='auto';a.volume=this.level;a.onended=()=>this.next();a.onerror=()=>{if(!this.failed){this.failed=true;this.next();}};void a.play().catch(()=>{});}
}
