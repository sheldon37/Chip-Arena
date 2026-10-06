import {arenaMap,mapId} from '../shared/maps.js';
export const MUSIC_TRACKS=[['foundry-rush','Foundry Rush',138],['orange-line','Orange Line',144],['kinetic-city','Kinetic City',132],['afterburner','Afterburner',152],['night-circuit','Night Circuit',128],['critical-mass','Critical Mass',146],['jump-drive','Jump Drive',140],['neon-recoil','Neon Recoil',136],['overpass','Overpass',150],['last-set','Last Set',142],['moonlit-wilderness','Moonlit Wilderness',0],['violet-avenue','Violet Avenue',118],['cargo-pulse','Cargo Pulse',136],['skyline-run','Skyline Run',150],['isotope','Isotope',128],['dust-signal','Dust Signal',102],['stone-oath','Stone Oath',112],['cold-current','Cold Current',174],['lantern-drift','Lantern Drift',108],['cinder-engine','Cinder Engine',142]] as const;
/** Owner's PvE stage track (Siren TEST BOSS on Ward Zero until the Siren spawns). Kept out of MUSIC_TRACKS so PvP rotation is unchanged. */
export const PVE_STAGE_TRACK=['restricted-zone-pulse','Restricted Zone Pulse',0] as const;
/** One decode/stream at a time. Shuffle bag prevents repeats until all ten have played. */
export class ArenaMusic {
 private menu:HTMLAudioElement|null=null;private menuActive=false;private menuVolume=.6;
 setMenuActive(active:boolean){if(active===this.menuActive)return;this.menuActive=active;if(!active){this.menu?.pause();return;}if(!this.menu){this.menu=new Audio(`${((import.meta as any).env?.BASE_URL||'/')}music/menu-afterglow.mp3`);this.menu.loop=true;this.menu.preload='auto';}this.menu.volume=this.muted?0:this.menuVolume;void this.menu.play().catch(()=>{});}
 private bossActive=false;
 setBossActive(active:boolean){this.bossActive=active;if(active)this.setActive(false);}
 get playbackVolume(){return this.level;}
 private map='foundry';private theme:number|null=null;private audio:HTMLAudioElement|null=null;private bag:number[]=[];private current=-1;private active=false;private volume=.35;private muted=false;private failed=false;private ducked=false;
 setMap(raw:unknown){const id=mapId(raw);if(id===this.map)return;this.map=id;const music=arenaMap(id).music;this.theme=music?MUSIC_TRACKS.findIndex(t=>t[0]===music):null;this.audio?.pause();if(this.audio){this.audio.onended=null;this.audio.onerror=null;this.audio.removeAttribute('src');this.audio.load();}this.audio=null;this.current=-1;this.failed=false;if(this.active)this.next();}
 private stage:typeof PVE_STAGE_TRACK|null=null;private dip=1;
 /** Loops `track` in place of the map's music while set; a load error falls back to the rotation. */
 setStage(track:typeof PVE_STAGE_TRACK|null){if(track===this.stage)return;this.stage=track;this.audio?.pause();if(this.audio){this.audio.onended=null;this.audio.onerror=null;this.audio.removeAttribute('src');this.audio.load();}this.audio=null;this.current=-1;this.failed=false;if(this.active)this.next();}
 rewindStage(){if(this.stage&&this.audio&&this.audio.currentTime>0)this.audio.currentTime=0;}
 /** Stage track only: lowers it under intro stings without touching the boss song level. */
 setDip(dip:number){if(this.dip===dip)return;this.dip=dip;if(this.audio)this.audio.volume=this.trackLevel;}
 private get playingStage(){return this.stage&&!this.failed?this.stage:null;}
 private get trackLevel(){return this.level*(this.playingStage?this.dip:1);}
 private get level(){return this.muted?0:this.volume*.55*(this.ducked?.12:1);}
 setDucked(ducked:boolean){if(this.ducked===ducked)return;this.ducked=ducked;if(this.audio)this.audio.volume=this.trackLevel;}
 configure(volume:number,muted:boolean,menuVolume=this.menuVolume){this.menuVolume=Number.isFinite(menuVolume)?Math.max(0,Math.min(1,menuVolume)):.6;this.volume=Number.isFinite(volume)?Math.max(0,Math.min(1,volume)):.35;this.muted=muted;if(this.menu)this.menu.volume=muted?0:this.menuVolume;if(this.audio)this.audio.volume=this.trackLevel;}
 get cueVolume(){return this.muted?0:this.volume*.80;}
 get title(){return this.current===-2?this.stage?.[1]??'':this.current<0?'':MUSIC_TRACKS[this.current][1];}
 setActive(active:boolean){active=active&&!this.bossActive;if(active===this.active)return;this.active=active;if(!active){this.audio?.pause();return;}if(this.audio&&!this.audio.ended){void this.audio.play().catch(()=>{});}else this.next();}
 unlock(){if(this.menuActive&&this.menu)void this.menu.play().catch(()=>{});if(this.active&&this.audio)void this.audio.play().catch(()=>{});}
 private next(){if(!this.active)return;const stage=this.playingStage;if(stage){this.current=-2;this.audio?.pause();const a=this.audio=new Audio(`${((import.meta as any).env?.BASE_URL||'/')}music/${stage[0]}.mp3`);a.preload='auto';a.loop=true;a.volume=this.trackLevel;a.onerror=()=>{if(!this.failed){this.failed=true;this.next();}};void a.play().catch(()=>{});return;}
 if(this.theme===null&&!this.bag.length){this.bag=Array.from({length:10},(_,i)=>i);for(let i=this.bag.length-1;i>0;i--){const j=Math.floor(Math.random()*(i+1));[this.bag[i],this.bag[j]]=[this.bag[j],this.bag[i]];}if(this.bag.at(-1)===this.current)[this.bag[0],this.bag[this.bag.length-1]]=[this.bag.at(-1)!,this.bag[0]];}
 this.current=this.theme??this.bag.pop()!;this.audio?.pause();const a=this.audio=new Audio(`${((import.meta as any).env?.BASE_URL||'/')}music/${MUSIC_TRACKS[this.current][0]}.mp3`);a.preload='auto';a.loop=this.theme!==null;a.volume=this.level;a.onended=()=>this.next();a.onerror=()=>{if(!this.failed){this.failed=true;this.theme=null;this.next();}};void a.play().catch(()=>{});}
}
